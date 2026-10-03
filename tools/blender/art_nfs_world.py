"""Author a separate visual mesh variant; never modify source or driving collision."""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from simplify_nfs_world import bounds, simplify_planes, verify_bounds_reader, verify_planar_reduction
from nfs_primitives import coarse_solids, verify_coarse_solids


# First Rosewood art study: the connector tunnel shell and repeated fittings.
# These are open structural meshes; the guarded planar dissolve retains UV seams
# and boundary curves. RoadsPhysical and the rest of Rosewood stay exact.
ROSEWOOD_CONNECTOR_ANGLES = {
    'Buildings_00041': 15,  # guardrail
    'Buildings_00177': 25,  # structural support
    'Buildings_00207': 35,  # repeated pipe silhouette
    'Buildings_00208': 10,  # roof and wall shell
    'Buildings_00209': 10,
    'Buildings_00210': 10,
    'Buildings_00211': 10,
    'Buildings_00213': 15,  # metalwork
}


def main():
    verify_bounds_reader()
    verify_planar_reduction()
    verify_coarse_solids()
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--config', type=Path)
    parser.add_argument('--category', choices=('Buildings', 'Props', 'Trees'))
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve()
    sys.path.insert(0, str(root / 'tools/nfs_world'))
    from district_config import load
    config = load(root, args.config)
    contract = json.loads((root / config['outputRoot'] / 'District/contract.json').read_text())
    inventory = json.loads((root / config['artifactRoot'] / 'inventory.json').read_text())
    texture_names = {t['sha256']: t['name'] for model in inventory['models'] for t in model['textures']}
    materials = {m['name']: m for m in contract['materials']}
    if config['districtId'] == 'rosewood':
        buildings = next(part for part in contract['parts'] if part['category'] == 'Buildings')
        visible = {mesh['name'] for mesh in buildings['meshes'] if mesh['visible']}
        missing = ROSEWOOD_CONNECTOR_ANGLES.keys() - visible
        if missing:
            raise ValueError('Rosewood connector shell is absent from the source contract: ' + ', '.join(sorted(missing)))
    output = root / config['outputRoot'] / 'ArtMeshes' if args.config else root / 'source-art/maps/nfs-world/art-export'
    sources = root / config['editableRoot'] / 'ArtMeshes'
    output.mkdir(parents=True, exist_ok=True)
    sources.mkdir(parents=True, exist_ok=True)
    records = []
    if args.category:
        previous = json.loads((root / config['artifactRoot'] / 'art-meshes.json').read_text())
        records = [record for record in previous['meshes'] if record['category'] != args.category]
    for part in contract['parts']:
        category = part['category']
        if category not in ('Buildings', 'Props', 'Trees'):
            continue
        if args.category and category != args.category:
            continue
        print(f'Authoring {category}', flush=True)
        bpy.ops.wm.open_mainfile(filepath=str(root / config['editableRoot'] / 'District' / f'{category}.blend'))
        definitions = {m['name']: m for m in part['meshes']}
        for index, obj in enumerate(list(bpy.data.objects)):
            if obj.type != 'MESH':
                continue
            definition = definitions[obj.name]
            if not definition['visible']:
                bpy.data.objects.remove(obj, do_unlink=True)
                continue
            original = obj.data
            original.calc_loop_triangles()
            before = len(original.loop_triangles)
            material = materials[definition['material']]
            texture_name = texture_names.get(material['texture'].removesuffix('.png'), '')
            trunk = category == 'Trees' and any(term in texture_name for term in ('TRUNK', 'BARK'))
            # Preserve thin alpha cards, lettering, signage, road/bridge geometry.
            structural_road = any(term in definition['sourceName'].lower() for term in ('visroad', 'roadfake', 'bridge', 'roadmid'))
            candidate = before >= 40 and not structural_road and (
                (category in ('Buildings', 'Props') and not material['alphaClip']) or trunk)
            changed = False
            primitives = 0
            if candidate:
                reduced = original.copy()
                if category in ('Buildings', 'Props'):
                    primitives = coarse_solids(reduced, .30 if category == 'Buildings' else .15)
                connector_study = config['districtId'] == 'rosewood' and obj.name in ROSEWOOD_CONNECTOR_ANGLES
                angle_degrees = ROSEWOOD_CONNECTOR_ANGLES[obj.name] if connector_study else 3 if category == 'Buildings' else 15
                angle = math.radians(angle_degrees)
                # Rebuilt props must leave the object's other open surfaces
                # exact. Dissolving those surfaces can move their rims.
                if not primitives or category == 'Buildings':
                    simplify_planes(reduced, angle=angle, preserve_normals=False)
                reduced.calc_loop_triangles()
                # Keep footprints/extrema exact to 2 cm, even for faceted cylinders.
                error = float(np.abs(bounds(reduced) - bounds(original)).max()) if len(reduced.vertices) else float('inf')
                if np.isfinite(error) and error <= .02 and len(reduced.loop_triangles) <= before:
                    obj.data = reduced
                    changed = True
                    if category in ('Buildings', 'Props') or trunk:
                        # Broad facets on accepted architecture and street furniture.
                        bpy.context.view_layer.objects.active = obj
                        obj.select_set(True)
                        bpy.ops.mesh.customdata_custom_splitnormals_clear()
                        obj.data.polygons.foreach_set('use_smooth', np.zeros(len(obj.data.polygons), dtype=bool))
                        obj.select_set(False)
                else:
                    bpy.data.meshes.remove(reduced)
            obj.data.calc_loop_triangles()
            records.append(dict(name=obj.name, category=category, sourceName=definition['sourceName'],
                                before=before, after=len(obj.data.loop_triangles), changed=changed,
                                boundsErrorLimitMetres=.02, trunk=trunk, primitiveComponents=primitives if changed else 0))
            if config['districtId'] == 'rosewood' and obj.name in ROSEWOOD_CONNECTOR_ANGLES:
                records[-1]['study'] = 'rosewood-connector-shell-v1'
                records[-1]['planarAngleDegrees'] = ROSEWOOD_CONNECTOR_ANGLES[obj.name]
            if index % 250 == 0:
                print(f'{category}: {index}', flush=True)
        for obj in bpy.data.objects:
            if obj.type == 'MESH' and not np.isfinite(bounds(obj.data)).all():
                raise ValueError(f'Non-finite art geometry: {obj.name}')
            if obj.type == 'MESH' and obj.data.uv_layers.active:
                coordinates = np.empty(len(obj.data.loops) * 2, dtype=np.float32)
                obj.data.uv_layers.active.data.foreach_get('uv', coordinates)
                if not np.isfinite(coordinates).all():
                    raise ValueError(f'Non-finite art UVs: {obj.name}')
        bpy.ops.wm.save_as_mainfile(filepath=str(sources / f'{category}.blend'))
        bpy.ops.export_scene.fbx(filepath=str(output / f'{category}.fbx'), object_types={'MESH', 'EMPTY'},
                                global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                axis_forward='-Z', axis_up='Y', use_mesh_modifiers=False,
                                mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False)
    report = dict(method='coarse convex solids with volume/nearest-surface guards; flat facets and limited dissolve',
                  meshes=records, collisionChanged=False, finiteCoordinatesValidated=True,
                  sourceTriangles=sum(m['before'] for m in records), candidateTriangles=sum(m['after'] for m in records))
    (root / config['artifactRoot'] / 'art-meshes.json').write_text(json.dumps(report, indent=2))
    print(f"Art candidates: {report['sourceTriangles']} -> {report['candidateTriangles']}", flush=True)


if __name__ == '__main__':
    main()
