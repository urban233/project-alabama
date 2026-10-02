"""Verify saved Blender bakes against source PNGs without altering either."""
import json
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    root = Path(__file__).resolve().parents[2]
    base = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld'
    report = json.loads((root / 'artifacts/NfsWorld/texture-bake.json').read_text())
    shifts = []
    alpha_count = 0
    for entry in report['textures']:
        if entry['treatment'] != 'baked-albedo':
            continue
        with Image.open(base / 'Textures' / entry['source']) as image:
            source = np.asarray(image.convert('RGBA'))
        with Image.open(base / 'ArtPass/Textures/Baked' / entry['source']) as image:
            baked = np.asarray(image.convert('RGBA'))
        assert source.shape == baked.shape, entry['source'] + ': dimensions changed'
        if entry['alpha']:
            assert np.array_equal(source[:, :, 3], baked[:, :, 3]), entry['source'] + ': alpha changed'
            alpha_count += 1
        else:
            assert np.all(baked[:, :, 3] == 255), entry['source'] + ': opaque alpha invalid'
        shift = float(baked[:, :, :3].mean() - source[:, :, :3].mean()) / 255
        assert abs(shift) < .04, entry['source'] + ': colour space or brightness drift'
        assert abs(shift - entry['meanBrightnessShift']) < .004, entry['source'] + ': Blender save changed brightness'
        shifts.append(shift)
    counts = dict(Counter(entry['treatment'] for entry in report['textures']))
    coverage = json.loads((root / 'artifacts/NfsWorld/art-surface-coverage.json').read_text())
    treatments = {entry['source']: entry['treatment'] for entry in report['textures']}
    area_by_treatment = Counter()
    for entry in coverage['textures']:
        area_by_treatment[treatments.get(entry['texture'], 'untreated')] += entry['area']
    assert 'untreated' not in area_by_treatment, 'Audited district surfaces missing from bake'
    # Includes transparent card rectangles, not unique ground area or screen-space visibility.
    result = dict(counts=counts, cutoutAlphaMasksExact=alpha_count,
                  maxSavedBrightnessShift=max(map(abs, shifts)), dimensionsExact=True,
                  auditedSurfaceAreaFraction={key: value / coverage['surfaceArea'] for key, value in area_by_treatment.items()})
    (root / 'artifacts/NfsWorld/texture-bake-validation.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
