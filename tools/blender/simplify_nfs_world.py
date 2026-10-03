"""Reduce visible city detail while retaining original collision and road surfaces."""
import argparse
import json
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np


def bounds(mesh):
    values = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', values)
    values = values.reshape(-1, 3)
    return np.array((values.min(axis=0), values.max(axis=0)))


def verify_bounds_reader():
    """A bounds query must return known extrema without mutating Blender geometry."""
    fixture = bpy.data.meshes.new('Bounds reader fixture')
    vertices = np.array(((1, 2, 3), (4, 5, 6), (-7, 8, 9)), dtype=np.float32)
    fixture.from_pydata(vertices.tolist(), [], [(0, 1, 2)])
    np.testing.assert_array_equal(bounds(fixture), ((-7, 2, 3), (4, 8, 9)))
    actual = np.empty(9, dtype=np.float32)
    fixture.vertices.foreach_get('co', actual)
    np.testing.assert_array_equal(actual.reshape(-1, 3), vertices)
    bpy.data.meshes.remove(fixture)


def simplify_planes(mesh, angle=.001, preserve_normals=True, weld_tolerance=.00001, preserve_uv_gradients=False):
    """Dissolve redundant coplanar detail, retaining UV seams and loop normals."""
    mesh.calc_loop_triangles()
    source_normals = [normal.vector.copy() for normal in mesh.corner_normals]
    bm = bmesh.new()
    bm.from_mesh(mesh)
    layer = bm.loops.layers.float_vector.new('NfsSourceNormal')
    for face in bm.faces:
        for loop in face.loops:
            loop[layer] = source_normals[loop.index]
    # Welding preserves per-loop UVs and custom data. DISSOLVE's UV/NORMAL
    # delimiters keep seams and faceted edges; open borders are not dissolved.
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=weld_tolerance)
    bm.normal_update()
    if preserve_uv_gradients:
        from uv_layout import protect_uv_gradients
        protect_uv_gradients(bm)
    # Blender's cosine threshold rounds to 1 below this tolerance, disabling
    # even exactly planar dissolves. 0.001 rad (~0.057 degrees) is conservative.
    delimit = {'MATERIAL', 'SEAM', 'UV'}
    if preserve_normals:
        delimit.add('NORMAL')
    bmesh.ops.dissolve_limit(bm, angle_limit=angle, use_dissolve_boundaries=False,
                           verts=list(bm.verts), edges=list(bm.edges),
                           delimit=delimit)
    bm.to_mesh(mesh)
    bm.free()
    normals = mesh.attributes.get('NfsSourceNormal')
    if normals is not None:
        mesh.normals_split_custom_set([item.vector for item in normals.data])
        mesh.attributes.remove(normals)
    mesh.update()


def verify_planar_reduction():
    """Prove redundant planar triangles disappear without changing UVs/normals."""
    mesh = bpy.data.meshes.new('Planar reduction fixture')
    vertices = [(x, y, 0) for y in range(3) for x in range(3)]
    faces = []
    for y in range(2):
        for x in range(2):
            a = y * 3 + x
            faces.extend([(a, a + 1, a + 4), (a, a + 4, a + 3)])
    mesh.from_pydata(vertices, [], faces)
    mesh.polygons.foreach_set('use_smooth', np.ones(len(faces), dtype=bool))
    uv = mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (point.x / 2, point.y / 2)
    mesh.normals_split_custom_set([(0, 0, 1)] * len(mesh.loops))
    simplify_planes(mesh)
    mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles) < 8, 'Coplanar fixture was not reduced'
    np.testing.assert_array_equal(bounds(mesh), ((0, 0, 0), (2, 2, 0)))
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        np.testing.assert_allclose(mesh.uv_layers.active.data[loop.index].uv,
                                   (point.x / 2, point.y / 2), atol=.00001)
        np.testing.assert_allclose(mesh.corner_normals[loop.index].vector, (0, 0, 1), atol=.00001)
    bpy.data.meshes.remove(mesh)


def main():
    verify_bounds_reader()
    verify_planar_reduction()
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--categories', nargs='+', choices=['Buildings', 'Panorama'])
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve()
    base = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld'
    contract = json.loads((base / 'District/contract.json').read_text())
    # Export outside Unity so the editor never reads an unfinished replacement.
    output = root / 'source-art/maps/nfs-world/style-export'
    sources = root / 'source-art/maps/nfs-world/blender/StyleMeshes'
    output.mkdir(parents=True, exist_ok=True)
    sources.mkdir(parents=True, exist_ok=True)
    records = []
    if args.categories:
        previous = json.loads((root / 'artifacts/NfsWorld/visual-reduction.json').read_text())
        records = [record for record in previous['meshes'] if record['category'] not in args.categories]
    for part in contract['parts']:
        category = part['category']
        if args.categories and category not in args.categories:
            continue
        print(f'Simplifying {category}', flush=True)
        if not any(mesh['visible'] for mesh in part['meshes']):
            continue
        bpy.ops.wm.open_mainfile(filepath=str(root / f'source-art/maps/nfs-world/blender/District/{category}.blend'))
        definitions = {mesh['name']: mesh for mesh in part['meshes']}
        for object_index, obj in enumerate(list(bpy.data.objects)):
            if obj.type != 'MESH':
                continue
            definition = definitions[obj.name]
            if not definition['visible']:
                bpy.data.objects.remove(obj, do_unlink=True)
                continue
            original = obj.data
            original_bounds = bounds(original)
            before = len(original.loop_triangles) if original.loop_triangles else definition['triangles']
            accepted = False
            # Roads/terrain, props and foliage retain exact source geometry.
            if category in ('Buildings', 'Panorama') and before >= 100:
                backup = original.copy()
                reduced = original.copy()
                simplify_planes(reduced)
                # Reject a reduction that moves the object's silhouette extrema > 2 cm.
                reduced_bounds = bounds(reduced) if len(reduced.vertices) else np.full((2, 3), np.nan)
                if np.isfinite(reduced_bounds).all() and np.isfinite(original_bounds).all() and np.abs(reduced_bounds - original_bounds).max() <= .02:
                    obj.data = reduced
                    accepted = True
                else:
                    obj.data = backup
                obj.modifiers.clear()
                bpy.context.view_layer.update()
            obj.data.calc_loop_triangles()
            after = len(obj.data.loop_triangles)
            records.append(dict(category=category, name=obj.name, before=before, after=after,
                                reduced=accepted, boundsErrorLimitMetres=.02))
            if object_index % 250 == 0:
                print(f'{category}: object {object_index}', flush=True)
        bpy.context.view_layer.update()
        for obj in bpy.data.objects:
            if obj.type == 'MESH':
                check = bounds(obj.data)
                if not np.isfinite(check).all():
                    raise ValueError(f'Non-finite mesh rejected before export: {obj.name}')
        bpy.ops.wm.save_as_mainfile(filepath=str(sources / f'{category}.blend'))
        bpy.ops.export_scene.fbx(filepath=str(output / f'{category}.fbx'), object_types={'MESH', 'EMPTY'},
                                global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                axis_forward='-Z', axis_up='Y', use_mesh_modifiers=False,
                                mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False)
        print(f'Style mesh export: {category}', flush=True)
    report = dict(method='coplanar dissolve with UV/normal seams and open boundaries preserved',
                  meshes=records, before=sum(m['before'] for m in records), after=sum(m['after'] for m in records),
                  collisionChanged=False, roadTerrainAndFoliageGeometryChanged=False, finiteCoordinatesValidated=True)
    (root / 'artifacts/NfsWorld/visual-reduction.json').write_text(json.dumps(report, indent=2))
    print(f"Visible triangles: {report['before']} -> {report['after']}", flush=True)


if __name__ == '__main__':
    main()
