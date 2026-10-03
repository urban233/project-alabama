"""Verify saved Blender textures and untouched fallback bindings in B's variant."""
import hashlib
import json
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    root = Path(__file__).resolve().parents[2]
    base = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld'
    report = json.loads((base / 'ArtDirection/texture-bake.json').read_text())
    profile_path = root / 'docs/art-direction/downtown-material-recipe.json'
    profile = json.loads(profile_path.read_text())
    baseline = {e['source']: e for e in json.loads((base / 'ArtPass/textures.json').read_text())['textures']}
    bindings = json.loads((base / 'ArtDirection/textures.json').read_text())['textures']
    changed = {e['source'] for e in report['textures']}
    reviewed = {source: family for family, sources in profile['families'].items() for source in sources}
    assert {e['source']: e['family'] for e in report['textures']} == reviewed, 'Reviewed albedo selection changed since bake'
    assert report['recipe'] == profile['recipe']
    shifts = []
    for entry in report['textures']:
        source_path = base / 'Textures' / entry['source']
        assert hashlib.sha256(source_path.read_bytes()).hexdigest() == entry['sourceSha256'], 'Source changed since bake'
        source = np.asarray(Image.open(source_path).convert('RGBA'))
        baked = np.asarray(Image.open(base / 'ArtDirection/Textures' / entry['source']).convert('RGBA'))
        assert source.shape == baked.shape, entry['source'] + ': source canvas changed'
        assert (np.array_equal(source[..., 3], baked[..., 3]) if entry['alpha'] else np.all(baked[..., 3] == 255))
        shift = float(baked[..., :3].mean() - source[..., :3].mean()) / 255
        assert abs(shift) < .04 and abs(shift - entry['meanBrightnessShift']) < .004
        shifts.append(shift)
    assert len(bindings) == len(baseline) and {e['source'] for e in bindings} == baseline.keys()
    for entry in bindings:
        if entry['source'] in changed:
            assert entry['file'] == entry['source'] and not entry.get('assetPath')
            assert entry['alpha'] == baseline[entry['source']]['alpha']
            continue
        original = baseline[entry['source']]
        assert entry['assetPath'] == 'Assets/Alabama/Art/Maps/NfsWorld/ArtPass/Textures/' + original['file']
        assert entry['alpha'] == original['alpha']
    result = dict(textures=len(shifts), families=dict(Counter(e['family'] for e in report['textures'])),
                  preservedBindings=len(bindings) - len(changed), dimensionsExact=True,
                  maxSavedBrightnessShift=max(map(abs, shifts)))
    output = root / 'artifacts/NfsWorld/DeveloperBArt'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'texture-validation.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
