"""Bake a consistent simplified albedo treatment using Blender's image buffers.

The user explicitly selected Blender Python texture baking. Source files, UV
coordinates and alpha masks are retained; lettering/marking assets are protected.
"""
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

import bpy
import numpy as np


VERSION = 'edge-preserving-albedo-v2'
PROTECTED = re.compile(r'(?:^|_)(?:SGN|ADS|SFX|HUD|UI|DECAL|SIGN)(?:_|\d)|GRAF|GRAFF|LOGO|CHEVRON|WARNING|ARROWDOWN|ARROWUP', re.I)

# Albedo atlases used by the connector tunnel's roof, wall and supports. Keep
# their dimensions, UV islands and bright lamps; only quiet neutral concrete.
ROSEWOOD_CONNECTOR_CONCRETE = {
    '09fe35c0c0d072bc969c7e3b4872e43ac5c32a9fd0c15f42b69ff069b36631c6.png',
    '8ccb9837f46a5f7f1c0d44cc48d7995c52d634283d3cd05888ce29d1363d4522.png',
    '632df24c36a5e59d50607839a5a79eb0a3b2e909d0965c8fe640b0ebd5155f7b.png',
}
ROSEWOOD_CONNECTOR_RECIPE = 'rosewood-connector-concrete-v1'


def simplify_colour(rgba, alpha_clip, foliage, road):
    original_alpha = rgba[:, :, 3].copy()
    rgb = rgba[:, :, :3].copy()
    alpha = original_alpha if alpha_clip else np.ones_like(original_alpha)
    # Edges with strong colour differences stay sharp. Alpha-weighted neighbours
    # avoid mixing invisible pixels into the edges of foliage/fence cards.
    threshold = .13 if road else .17
    for step in (1, 2, 4, 8):
        total = rgb.copy()
        weight_sum = np.ones(alpha.shape, dtype=np.float32)
        for axis, amount in ((0, step), (0, -step), (1, step), (1, -step)):
            neighbour = np.roll(rgb, amount, axis=axis)
            neighbour_alpha = np.roll(alpha, amount, axis=axis)
            distance = np.sum((neighbour - rgb) ** 2, axis=2)
            weight = np.exp(-distance / threshold ** 2) * neighbour_alpha
            total += neighbour * weight[:, :, None]
            weight_sum += weight
        rgb = total / weight_sum[:, :, None]
    levels = 16 if foliage else 40 if road else 32
    # Quantize brightness together, not each colour channel independently.
    # Independent RGB rounding introduced green/magenta bands in neutral glass.
    luminance = np.sum(rgb * np.array([.2126, .7152, .0722], dtype=np.float32), axis=2)
    difference = (np.rint(luminance * levels) / levels - luminance) * .7
    rgb = np.clip(rgb + difference[:, :, None], 0, 1)
    result = np.empty_like(rgba)
    result[:, :, :3] = rgb
    result[:, :, 3] = original_alpha if alpha_clip else 1
    return result


def quiet_connector_concrete(rgba):
    """Lift shaded neutral concrete slightly without washing lamps or paint."""
    result = rgba.copy()
    rgb = result[:, :, :3]
    luminance = np.sum(rgb * np.array([.2126, .7152, .0722], dtype=np.float32), axis=2)
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    neutral = np.clip((.18 - chroma) / .08, 0, 1)
    not_bright = np.clip((.70 - luminance) / .15, 0, 1)
    lift = .10 * np.clip(1 - luminance / .70, 0, 1) * neutral * not_bright
    rgb[:] = np.clip(rgb + lift[:, :, None] * np.array([1, .84, .68], dtype=np.float32), 0, 1)
    return result


def verify_recipe():
    rng = np.random.default_rng(19)
    source = np.ones((32, 32, 4), dtype=np.float32)
    source[:, :16, :3] = .15
    source[:, 16:, :3] = .75
    source[:, :, :3] += rng.uniform(-.025, .025, (32, 32, 3))
    source[:, :, 3] = np.tile(np.arange(32, dtype=np.float32) / 31, (32, 1))
    baked = simplify_colour(source, True, False, False)
    assert np.array_equal(baked[:, :, 3], source[:, :, 3]), 'Alpha changed'
    assert baked[:, :14, :3].std() < source[:, :14, :3].std(), 'Noise was not reduced'
    assert abs(float(baked[:, :, :3].mean() - source[:, :, :3].mean())) < .03
    assert baked[:, 15, :3].mean() < .25 and baked[:, 16, :3].mean() > .65, 'Window edge moved'
    concrete = quiet_connector_concrete(baked)
    assert np.array_equal(concrete[:, :, 3], baked[:, :, 3]), 'Connector alpha changed'
    assert concrete[:, 4, :3].mean() > baked[:, 4, :3].mean(), 'Shaded concrete was not lifted'
    assert np.array_equal(concrete[:, 24, :3], baked[:, 24, :3]), 'Bright atlas region changed'


def main():
    verify_recipe()
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--config', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve()
    sys.path.insert(0, str(root / 'tools/nfs_world'))
    from district_config import load
    config = load(root, args.config)
    base = root / config['outputRoot']
    contract = json.loads((base / 'District/contract.json').read_text())
    inventory = json.loads((root / config['artifactRoot'] / 'inventory.json').read_text())
    names = {texture['sha256']: texture['name'] for model in inventory['models'] for texture in model['textures']}
    materials = {material['name']: material for material in contract['materials']}
    used = {}
    for part in contract['parts']:
        for mesh in part['meshes']:
            if not mesh['visible']:
                continue
            material = materials[mesh['material']]
            entry = used.setdefault(material['texture'], dict(categories=set(), alpha=False))
            entry['categories'].add(part['category'])
            entry['alpha'] |= material['alphaClip']
    manifest_path = base / 'ArtPass/textures.json'
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else dict(textures=[])
    manual = [entry for entry in manifest['textures'] if not entry['file'].startswith('Baked/')]
    authored = {entry['source'] for entry in manual}
    output = base / 'ArtPass/Textures/Baked'
    output.mkdir(parents=True, exist_ok=True)
    cache_path = root / config['artifactRoot'] / 'texture-bake.json'
    cache = json.loads(cache_path.read_text()) if cache_path.exists() else {}
    previous = {entry['source']: entry for entry in cache.get('textures', [])}
    report = []
    baked_entries = []
    for index, (source_file, definition) in enumerate(sorted(used.items())):
        source_path = base / 'Textures' / source_file
        name = names.get(source_file.removesuffix('.png'), '')
        categories = sorted(definition['categories'])
        if source_file in authored or PROTECTED.search(name):
            report.append(dict(source=source_file, name=name, treatment='hand-authored' if source_file in authored else 'protected-lettering', categories=categories))
            continue
        digest = hashlib.sha256(source_path.read_bytes()).hexdigest()
        connector_study = config['districtId'] == 'rosewood' and source_file in ROSEWOOD_CONNECTOR_CONCRETE
        version = VERSION + (ROSEWOOD_CONNECTOR_RECIPE if connector_study else '')
        signature = hashlib.sha256((version + digest + str(definition['alpha']) + str(categories)).encode()).hexdigest()
        target_path = output / source_file
        prior = previous.get(source_file)
        if prior and prior.get('signature') == signature and target_path.is_file():
            record = prior
        else:
            source = bpy.data.images.load(str(source_path), check_existing=False)
            source.colorspace_settings.name = 'Non-Color'
            source.alpha_mode = 'STRAIGHT' if definition['alpha'] else 'NONE'
            width, height = source.size
            pixels = np.empty(width * height * 4, dtype=np.float32)
            source.pixels.foreach_get(pixels)
            rgba = pixels.reshape(height, width, 4)
            foliage = 'Trees' in categories
            road = any(category in categories for category in ('Roads', 'Terrain'))
            result = simplify_colour(rgba, definition['alpha'], foliage, road)
            if connector_study:
                if definition['alpha'] or categories != ['Buildings']:
                    raise ValueError('Connector concrete must be opaque architecture: ' + source_file)
                result = quiet_connector_concrete(result)
            brightness_shift = float(result[:, :, :3].mean() - rgba[:, :, :3].mean())
            allowed_shift = .08 if connector_study else .04
            if abs(brightness_shift) > allowed_shift or not np.isfinite(result).all():
                raise ValueError('Bake changed overall palette or produced invalid pixels: ' + source_file)
            baked = bpy.data.images.new('Baked ' + source_file, width=width, height=height, alpha=True)
            baked.colorspace_settings.name = 'Non-Color'
            baked.alpha_mode = 'STRAIGHT'
            baked.pixels.foreach_set(result.reshape(-1))
            baked.filepath_raw = str(target_path)
            baked.file_format = 'PNG'
            baked.save()
            record = dict(source=source_file, name=name, signature=signature, treatment='baked-albedo', categories=categories,
                          width=width, height=height, alpha=definition['alpha'], meanBrightnessShift=brightness_shift)
            if connector_study:
                record['study'] = ROSEWOOD_CONNECTOR_RECIPE
            bpy.data.images.remove(source)
            bpy.data.images.remove(baked)
        report.append(record)
        baked_entries.append(dict(source=source_file, file='Baked/' + source_file, alpha=definition['alpha']))
        if index % 50 == 0:
            print('Texture bake', index, '/', len(used), flush=True)
    manifest['textures'] = manual + baked_entries
    manifest['bakeRecipe'] = VERSION
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    cache_path.write_text(json.dumps(dict(recipe=VERSION, textures=report), indent=2) + '\n')
    print('Baked', len(baked_entries), 'textures; kept', len(manual), 'hand-authored mappings', flush=True)


if __name__ == '__main__':
    main()
