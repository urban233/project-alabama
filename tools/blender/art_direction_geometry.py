"""District-wide derived geometry: substantial forms with retained source openings."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from simplify_nfs_world import bounds, simplify_planes, verify_bounds_reader, verify_planar_reduction
from nfs_primitives import coarse_solids, facet_round_props, verify_round_props, verify_coarse_solids, verify_coarse_uv_guard, verify_solid_projection
from uv_layout import preserves_uv_layout, verify_uv_layout


def main():
    verify_bounds_reader()
    verify_planar_reduction()
    verify_coarse_solids()
    verify_coarse_uv_guard()
    verify_uv_layout()
    verify_solid_projection()
    verify_round_props()
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--category', choices=('Buildings', 'Props', 'Trees', 'Terrain', 'Panorama'))
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve()
    base = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld'
    contract_path = base / 'District/contract.json'
    contract = json.loads(contract_path.read_text())
    inventory = json.loads((root / 'artifacts/NfsWorld/inventory.json').read_text())
    names = {t['sha256']: t['name'] for model in inventory['models'] for t in model['textures']}
    materials = {m['name']: m for m in contract['materials']}
    output = root / 'source-art/maps/nfs-world/art-direction-geometry'
    sources = root / 'source-art/maps/nfs-world/blender/ArtDirection'
    output.mkdir(parents=True, exist_ok=True)
    sources.mkdir(parents=True, exist_ok=True)
    records = []
    if args.category:
        records = [r for r in json.loads((output/'geometry.json').read_text())['meshes'] if r['category'] != args.category]
    for part in contract['parts']:
        category = part['category']
        if category not in ('Buildings', 'Props', 'Trees', 'Terrain', 'Panorama'):
            continue
        if args.category and category != args.category:
            continue
        source_path = root / f'source-art/maps/nfs-world/blender/District/{category}.blend'
        source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
        bpy.ops.wm.open_mainfile(filepath=str(source_path))
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
            source_name = definition['sourceName'].lower()
            texture_name = names.get(material['texture'].removesuffix('.png'), '').upper()
            structural = any(t in source_name for t in ('visroad', 'roadfake', 'bridge', 'roadmid'))
            protected = any(t in texture_name for t in ('SGN_', 'SGN-', 'SFX_', 'ADS_', 'GRAF', 'LOGO', 'CHEVRON', 'WARNING'))
            trunk = category == 'Trees' and any(t in texture_name for t in ('TRUNK', 'BARK'))
            rock = category in ('Terrain', 'Panorama') and any(t in source_name for t in ('rock', 'cliff'))
            candidate = before >= 40 and not structural and not protected and (
                (category in ('Buildings', 'Props') and not material['alphaClip']) or trunk or
                (rock and not material['alphaClip']))
            primitives = 0
            reshaped = 0
            radial = 0
            changed = False
            if candidate:
                reduced = original.copy()
                # KN5's triangulated vertices differ by fractions of a millimetre.
                # A 1 mm weld reconnects the actual solids without dropping UV loops.
                # Rebuild only closed genus-zero components; holes and cards stay open.
                if category == 'Props' or trunk:
                    reshaped = coarse_solids(reduced, .08, .001, preserve_topology=True)
                    if obj.name in ('Props_00050', 'Props_00062', 'Props_00063'):
                        radial = facet_round_props(reduced, sides=12, maximum_displacement=.015)
                        reshaped += radial
                elif category == 'Buildings':
                    primitives = coarse_solids(reduced, .08 if category == 'Props' else .10, .001,
                                               maximum_uv_error=.0002)
                uv_reference = reduced.copy() if reshaped else original
                simplify_planes(reduced, angle=math.radians(5 if category == 'Buildings' else 12),
                                preserve_normals=False, weld_tolerance=.001, preserve_uv_gradients=True)
                if reshaped and not preserves_uv_layout(uv_reference, reduced):
                    bpy.data.meshes.remove(reduced)
                    reduced = uv_reference.copy()
                reduced.calc_loop_triangles()
                error = float(np.abs(bounds(reduced) - bounds(original)).max()) if len(reduced.vertices) else float('inf')
                if (np.isfinite(error) and error <= .02 and (len(reduced.loop_triangles) < before or reshaped)
                        and preserves_uv_layout(uv_reference, reduced)):
                    obj.data = reduced
                    changed = True
                    bpy.context.view_layer.objects.active = obj
                    obj.select_set(True)
                    bpy.ops.mesh.customdata_custom_splitnormals_clear()
                    obj.data.polygons.foreach_set('use_smooth', np.zeros(len(obj.data.polygons), dtype=bool))
                    obj.select_set(False)
                else:
                    bpy.data.meshes.remove(reduced)
                if uv_reference != original:
                    bpy.data.meshes.remove(uv_reference)
            obj.data.calc_loop_triangles()
            if not np.isfinite(bounds(obj.data)).all():
                raise ValueError('Non-finite derived geometry: ' + obj.name)
            if obj.data.uv_layers.active:
                uv = np.empty(len(obj.data.loops) * 2, dtype=np.float32)
                obj.data.uv_layers.active.data.foreach_get('uv', uv)
                if not np.isfinite(uv).all():
                    raise ValueError('Non-finite derived UVs: ' + obj.name)
            records.append(dict(name=obj.name, category=category, sourceName=definition['sourceName'],
                                before=before, after=len(obj.data.loop_triangles), changed=changed,
                                primitiveComponents=primitives if changed else 0, boundaryToleranceMetres=.02,
                                reshapedComponents=reshaped if changed else 0,
                                radialComponents=radial if changed else 0,
                                sourceBlendSha256=source_hash))
            if index % 250 == 0:
                print(category, index, flush=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(sources / f'{category}.blend'))
        bpy.ops.export_scene.fbx(filepath=str(output / f'{category}.fbx'), object_types={'MESH', 'EMPTY'},
                                global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                axis_forward='-Z', axis_up='Y', use_mesh_modifiers=False,
                                mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False)
    report = dict(schemaVersion=1, recipe='balanced-downtown-geometry-v5',
                  method='UV-preserving closed prop/trunk form projection, guarded coplanar retopology and 1 mm seam welding',
                  hullUvTolerance=.0002,
                  maximumPropDisplacementMetres=.08,
                  maximumRadialDisplacementMetres=.015, roundPropSides=12,
                  meshes=records, collisionChanged=False, finiteCoordinatesValidated=True,
                  sourceTriangles=sum(m['before'] for m in records), candidateTriangles=sum(m['after'] for m in records))
    (output / 'geometry.json').write_text(json.dumps(report, indent=2) + '\n')
    evidence = root / 'artifacts/NfsWorld/DeveloperBArt'
    (evidence / 'geometry-bake.json').write_text(json.dumps(report, indent=2) + '\n')
    print('Derived geometry:', report['sourceTriangles'], '->', report['candidateTriangles'], flush=True)


if __name__ == '__main__':
    main()
