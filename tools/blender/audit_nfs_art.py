"""Measure visible surface coverage to prioritize district-wide asset treatment."""
import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path

import bpy
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = args.root.resolve()
contract = json.loads((root / 'unity/Assets/Alabama/Art/Maps/NfsWorld/District/contract.json').read_text())
inventory = json.loads((root / 'artifacts/NfsWorld/inventory.json').read_text())
names = {texture['sha256']: texture['name'] for model in inventory['models'] for texture in model['textures']}
materials = {material['name']: material for material in contract['materials']}
coverage = defaultdict(lambda: dict(area=0., meshes=0, triangles=0, categories=[]))
for part in contract['parts']:
    category = part['category']
    if category not in ('Buildings', 'Props', 'Trees', 'Walls'):
        continue
    bpy.ops.wm.open_mainfile(filepath=str(root / f'source-art/maps/nfs-world/blender/District/{category}.blend'))
    for definition in part['meshes']:
        if not definition['visible']:
            continue
        obj = bpy.data.objects[definition['name']]
        scale = obj.matrix_world.to_scale()
        if max(scale) - min(scale) > .00001:
            raise ValueError('Surface audit expects uniformly scaled source objects: ' + obj.name)
        areas = np.empty(len(obj.data.polygons), dtype=np.float64)
        obj.data.polygons.foreach_get('area', areas)
        texture = materials[definition['material']]['texture']
        record = coverage[texture]
        record['area'] += float(areas.sum()) * scale.x ** 2
        record['meshes'] += 1
        record['triangles'] += definition['triangles']
        if category not in record['categories']:
            record['categories'].append(category)
        record['sourceName'] = names.get(texture.removesuffix('.png'), '')
        record['alphaClip'] = materials[definition['material']]['alphaClip']
records = [dict(texture=texture, **record) for texture, record in coverage.items()]
records.sort(key=lambda record: record['area'], reverse=True)
report = dict(surfaceArea=sum(record['area'] for record in records), textures=records)
(root / 'artifacts/NfsWorld/art-surface-coverage.json').write_text(json.dumps(report, indent=2))
print('Audited', len(records), 'texture surfaces;', report['surfaceArea'], 'square metres', flush=True)
