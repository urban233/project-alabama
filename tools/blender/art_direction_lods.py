"""Build spatial visual LODs from Unity's final batches, preserving open curves and UV seams."""
import argparse
import hashlib
import json
import shutil
import struct
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
from uv_layout import preserves_uv_layout, protect_uv_gradients, verify_uv_layout


def read_mesh(path):
    data = path.read_bytes()
    version, vertices, indices = struct.unpack_from('<iii', data)
    if version != 1 or len(data) != 12 + vertices * 40 + indices * 4:
        raise ValueError('Invalid Unity mesh exchange: ' + str(path))
    return (np.frombuffer(data, dtype='<f4', count=vertices * 10, offset=12).reshape(vertices, 10).copy(),
            np.frombuffer(data, dtype='<i4', count=indices, offset=12 + vertices * 40).reshape(-1, 3).copy())


def write_mesh(path, mesh, origin, descriptors):
    mesh.calc_loop_triangles()
    values = np.empty((len(mesh.loop_triangles) * 3, 10), dtype='<f4')
    uv = mesh.uv_layers.active
    for index, triangle in enumerate(mesh.loop_triangles):
        descriptor = descriptors[mesh.polygons[triangle.polygon_index].material_index]
        for corner, loop_index in enumerate(triangle.loops):
            loop = mesh.loops[loop_index]
            row = values[index * 3 + corner]
            row[:3] = np.array(mesh.vertices[loop.vertex_index].co) + origin
            row[3:6] = mesh.corner_normals[loop_index].vector
            row[6:8] = uv.data[loop_index].uv
            row[8:10] = descriptor
    if not np.isfinite(values).all():
        raise ValueError('LOD generated non-finite attributes')
    with path.open('wb') as output:
        output.write(struct.pack('<iii', 1, len(values), len(values)))
        output.write(values.tobytes())
        output.write(np.arange(len(values), dtype='<i4').tobytes())


def validate_surface(source, result, maximum_error):
    result.calc_loop_triangles()
    source.calc_loop_triangles()
    if len(result.loop_triangles) >= len(source.loop_triangles):
        bpy.data.meshes.remove(result)
        return None, 0
    def bounds(value):
        points = np.array([v.co[:] for v in value.vertices])
        return np.array((points.min(0), points.max(0)))
    if np.max(np.abs(bounds(result) - bounds(source))) > .02:
        bpy.data.meshes.remove(result)
        return None, 0
    if not preserves_uv_layout(source, result):
        bpy.data.meshes.remove(result)
        return None, 0
    def tree(value):
        return BVHTree.FromPolygons([v.co for v in value.vertices], [list(t.vertices) for t in value.loop_triangles], all_triangles=True)
    before, after = tree(source), tree(result)
    error = 0
    for original, target in ((source, after), (result, before)):
        for vertex in original.vertices:
            distance = target.find_nearest(vertex.co)[3]
            if distance is None or distance > maximum_error:
                bpy.data.meshes.remove(result)
                return None, distance
            error = max(error, distance)
    return result, error


def simplify_planar(source, angle, maximum_error):
    mesh = source.copy()
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.001)
    bm.normal_update()
    protect_uv_gradients(bm)
    bmesh.ops.dissolve_limit(bm, angle_limit=angle, use_dissolve_boundaries=False,
        verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL', 'SEAM', 'UV'})
    bm.to_mesh(mesh); bm.free(); mesh.update()
    return validate_surface(source, mesh, maximum_error)


def simplify(source, ratio, maximum_error):
    if len(source.polygons) <= 3:
        return None, 0
    mesh = source.copy()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.001)
    bm.normal_update()
    bm.verts.index_update()
    protect_uv_gradients(bm)
    uv = bm.loops.layers.uv.active
    protected = set()
    for edge in bm.edges:
        keep = not edge.is_manifold or edge.seam
        if edge.is_manifold:
            a, b = edge.link_loops
            different_material = a.face.material_index != b.face.material_index
            seams = any((loop[uv].uv - next(other[uv].uv for other in b.face.loops if other.vert == loop.vert)).length > .0002
                        for loop in (a, a.link_loop_next))
            keep |= different_material or seams or edge.calc_face_angle(0) > .785398
        if keep:
            protected.update(edge.verts)
    for axis in range(3):
        minimum = min(v.co[axis] for v in bm.verts)
        maximum = max(v.co[axis] for v in bm.verts)
        protected.update(v for v in bm.verts if abs(v.co[axis] - minimum) < .001 or abs(v.co[axis] - maximum) < .001)
    indices = [v.index for v in protected]
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new('LOD reduction', mesh)
    bpy.context.collection.objects.link(obj)
    group = obj.vertex_groups.new(name='Open curves and UV seams')
    if indices:
        group.add(indices, 1, 'REPLACE')
    modifier = obj.modifiers.new('Distance geometry', 'DECIMATE')
    modifier.ratio = ratio
    modifier.vertex_group = group.name
    modifier.vertex_group_factor = 1000
    modifier.invert_vertex_group = True
    bpy.context.view_layer.update()
    result = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    return validate_surface(source, result, maximum_error)


def main():
    verify_uv_layout()
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--refine', action='store_true', help='Retry failed expensive groups from a saved first pass')
    parser.add_argument('--fallback-only', action='store_true', help='Add planar alternatives to an existing exchange')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = args.root.resolve()
    exchange = root / 'artifacts/NfsWorld/DeveloperBArt/LodExchange'
    records = json.loads((exchange / 'inputs.json').read_text())['meshes']
    previous = {r['name']: r for r in json.loads((exchange / 'outputs.json').read_text())['meshes']} if args.refine or args.fallback_only else {}
    results = []
    for number, record in enumerate(records):
        name = record['name']
        path = exchange / (name + '.meshbin')
        if args.refine and (record['nearTriangles'] <= 2000 or all(
                previous[name][level + 'Triangles'] < record['nearTriangles'] for level in ['middle', 'far'])):
            results.append(previous[name])
            continue
        attributes, triangles = read_mesh(path)
        origin = (attributes[:, :3].min(0) + attributes[:, :3].max(0)) / 2
        source = bpy.data.meshes.new(name)
        source.from_pydata((attributes[:, :3] - origin).tolist(), [], triangles.tolist())
        uv = source.uv_layers.new(name='Source atlas')
        for loop in source.loops:
            uv.data[loop.index].uv = attributes[loop.vertex_index, 6:8]
        source.normals_split_custom_set([attributes[loop.vertex_index, 3:6] for loop in source.loops])
        descriptors = []
        for polygon in source.polygons:
            corners = attributes[list(polygon.vertices), 8:10]
            if np.max(np.abs(corners - corners[0])) > .0001:
                raise ValueError('Input triangle crosses texture layers: ' + name)
            value = tuple(corners[0])
            if value not in descriptors:
                descriptors.append(value)
                material = bpy.data.materials.get(str(value)) or bpy.data.materials.new(str(value))
                source.materials.append(material)
            polygon.material_index = descriptors.index(value)
        result_record = dict(previous[name]) if args.refine or args.fallback_only else dict(name=name,
            sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(), nearTriangles=len(triangles))
        if result_record['sourceSha256'] != hashlib.sha256(path.read_bytes()).hexdigest():
            raise ValueError('Refinement inputs changed: ' + name)
        for level, ratio, maximum_error in [('middle', .65, .10), ('far', .35, .25)]:
            if not args.refine:
                fallback, fallback_error = simplify_planar(source, .087266 if level == 'middle' else .261799, maximum_error)
                fallback_path = exchange / (name + '.' + level + '.planar.meshbin')
                if fallback is None:
                    shutil.copyfile(path, fallback_path)
                    result_record[level + 'PlanarTriangles'] = len(triangles)
                else:
                    write_mesh(fallback_path, fallback, origin, descriptors)
                    result_record[level + 'PlanarTriangles'] = len(fallback.loop_triangles)
                    bpy.data.meshes.remove(fallback)
                result_record[level + 'PlanarMaximumError'] = fallback_error
            if args.fallback_only:
                continue
            if args.refine and result_record[level + 'Triangles'] < len(triangles):
                continue
            output = exchange / (name + '.' + level + '.meshbin')
            # A ratio that fails one silhouette cannot discard the whole batch's
            # opportunity for a gentler reduction. Back off on expensive groups.
            trials = ([.8, .92] if args.refine else [ratio, .8, .92]) if len(triangles) > 2000 else [ratio]
            result = None; error = 0
            for trial in trials:
                result, error = simplify(source, trial, maximum_error)
                if result is not None:
                    break
            if result is None:
                shutil.copyfile(path, output)
                result_record[level + 'Triangles'] = len(triangles)
            else:
                write_mesh(output, result, origin, descriptors)
                result_record[level + 'Triangles'] = len(result.loop_triangles)
                bpy.data.meshes.remove(result)
            result_record[level + 'MaximumError'] = error
        bpy.data.meshes.remove(source)
        results.append(result_record)
        if number % 100 == 0:
            print('Spatial LOD groups:', number, '/', len(records), flush=True)
    (exchange / 'outputs.json').write_text(json.dumps(dict(meshes=results), indent=2) + '\n')
    print('LOD triangle totals:', {level: sum(r[level + 'Triangles'] for r in results) for level in ['near', 'middle', 'far']}, flush=True)


if __name__ == '__main__':
    main()
