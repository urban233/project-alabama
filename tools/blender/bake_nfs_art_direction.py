"""Bake only reviewed source IDs into a separate Developer B material variant."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'nfs_world'))
from art_direction_recipe import filter_albedo


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve()
    profile_path = root / 'docs/art-direction/downtown-material-recipe.json'
    profile = json.loads(profile_path.read_text())
    base = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld'
    baseline = json.loads((base / 'ArtPass/textures.json').read_text())
    output = base / 'ArtDirection/Textures'
    output.mkdir(parents=True, exist_ok=True)
    families = {source: family for family, ids in profile['families'].items() for source in ids}
    if len(families) != sum(map(len, profile['families'].values())):
        raise ValueError('A reviewed texture belongs to more than one family')
    known = {entry['source'] for entry in baseline['textures']}
    if not families.keys() <= known:
        raise ValueError('Reviewed source IDs are absent from the received baseline')
    records, textures = [], []
    for entry in baseline['textures']:
        source_id = entry['source']
        if source_id not in families:
            textures.append(dict(entry, assetPath='Assets/Alabama/Art/Maps/NfsWorld/ArtPass/Textures/' + entry['file']))
            continue
        # This explicit list was reviewed as albedo: no normals, signs or effects.
        path = base / 'Textures' / source_id
        original = bpy.data.images.load(str(path), check_existing=False)
        original.colorspace_settings.name = 'Non-Color'
        original.alpha_mode = 'STRAIGHT' if entry['alpha'] else 'NONE'
        width, height = original.size
        pixels = np.empty(width * height * 4, dtype=np.float32)
        original.pixels.foreach_get(pixels)
        rgba = pixels.reshape(height, width, 4)
        family = families[source_id]
        baked = filter_albedo(rgba, family, entry['alpha'])
        shift = float(baked[..., :3].mean() - rgba[..., :3].mean())
        if abs(shift) >= .04 or not np.isfinite(baked).all():
            raise ValueError('Palette drift or invalid bake: ' + source_id)
        image = bpy.data.images.new(source_id, width=width, height=height, alpha=True)
        image.colorspace_settings.name = 'Non-Color'
        image.alpha_mode = 'STRAIGHT'
        image.pixels.foreach_set(baked.reshape(-1))
        image.filepath_raw = str(output / source_id)
        image.file_format = 'PNG'
        image.save()
        textures.append(dict(source=source_id, file=source_id, alpha=entry['alpha']))
        records.append(dict(source=source_id, family=family, width=width, height=height,
                            alpha=entry['alpha'], meanBrightnessShift=shift,
                            sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        bpy.data.images.remove(original)
        bpy.data.images.remove(image)
    manifest = dict(recipe=profile['recipe'], textures=textures)
    (base / 'ArtDirection/textures.json').write_text(json.dumps(manifest, indent=2) + '\n')
    evidence = root / 'artifacts/NfsWorld/DeveloperBArt'
    evidence.mkdir(parents=True, exist_ok=True)
    provenance = json.dumps(dict(recipe=profile['recipe'],
        profileSha256=hashlib.sha256(profile_path.read_bytes()).hexdigest(), textures=records,
        preservedBindings=len(textures) - len(records)), indent=2) + '\n'
    (base / 'ArtDirection/texture-bake.json').write_text(provenance)
    (evidence / 'texture-bake.json').write_text(provenance)
    print('Baked', len(records), 'reviewed albedos; retained', len(textures) - len(records), 'baseline bindings', flush=True)


if __name__ == '__main__':
    main()
