"""Reject simplification that changes the interpolated source texture layout."""
import math

from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform


def protect_uv_gradients(bm, tolerance=.0002):
    """Keep edges separating different affine UV maps, even without a UV seam."""
    uv = bm.loops.layers.uv.active
    if uv is None:
        return
    for edge in bm.edges:
        if not edge.is_manifold or edge.seam:
            continue
        a, b = edge.link_faces
        if len(a.loops) != 3 or len(b.loops) != 3:
            edge.seam = True
            continue
        for source, target in ((a, b), (b, a)):
            points = [loop.vert.co for loop in source.loops]
            values = [Vector((*loop[uv].uv, 0)) for loop in source.loops]
            if any((barycentric_transform(loop.vert.co, *points, *values).xy - loop[uv].uv).length > tolerance
                   for loop in target.loops):
                edge.seam = True
                break


def preserves_uv_layout(source, candidate, tolerance=.0002):
    source.calc_loop_triangles()
    candidate.calc_loop_triangles()
    if not source.uv_layers.active or not candidate.uv_layers.active:
        return not source.uv_layers.active and not candidate.uv_layers.active
    if not candidate.loop_triangles:
        return False
    triangles = list(candidate.loop_triangles)
    tree = BVHTree.FromPolygons([v.co for v in candidate.vertices],
                               [list(t.vertices) for t in triangles], all_triangles=True)
    source_uv = source.uv_layers.active.data
    candidate_uv = candidate.uv_layers.active.data
    for triangle in source.loop_triangles:
        points = [source.vertices[index].co for index in triangle.vertices]
        _, _, index, _ = tree.find_nearest(sum(points, Vector()) / 3)
        if index is None:
            return False
        target = triangles[index]
        if source.polygons[triangle.polygon_index].material_index != candidate.polygons[target.polygon_index].material_index:
            return False
        target_points = [candidate.vertices[index].co for index in target.vertices]
        target_values = [Vector((*candidate_uv[index].uv, 0)) for index in target.loops]
        # UV seams alone are insufficient: removing a vertex can change a
        # continuous, piecewise-affine mapping inside an otherwise planar wall.
        for point, loop_index in zip(points, triangle.loops):
            value = barycentric_transform(point, *target_points, *target_values).xy
            if not all(math.isfinite(component) for component in value):
                return False
            if (value - source_uv[loop_index].uv).length > tolerance:
                return False
    return True


def verify_uv_layout():
    import bpy
    source = bpy.data.meshes.new('UV gradient source')
    candidate = bpy.data.meshes.new('UV gradient candidate')
    source.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (.5, .5, 0)], [],
                       [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)])
    candidate.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], [], [(0, 1, 2), (0, 2, 3)])
    try:
        for mesh in (source, candidate):
            uv = mesh.uv_layers.new(name='UVMap')
            for loop in mesh.loops:
                uv.data[loop.index].uv = mesh.vertices[loop.vertex_index].co.xy
        assert preserves_uv_layout(source, candidate)
        for loop in source.loops:
            if loop.vertex_index == 4:
                source.uv_layers.active.data[loop.index].uv = (.25, .5)
        assert not preserves_uv_layout(source, candidate), 'Continuous UV gradients must survive simplification'
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(source)
        protect_uv_gradients(bm)
        bmesh.ops.dissolve_limit(bm, angle_limit=.1, use_dissolve_boundaries=False,
                                verts=list(bm.verts), edges=list(bm.edges), delimit={'UV', 'SEAM', 'MATERIAL'})
        bm.to_mesh(candidate)
        bm.free()
        assert preserves_uv_layout(source, candidate), 'Protected gradients must survive planar simplification'
    finally:
        bpy.data.meshes.remove(source)
        bpy.data.meshes.remove(candidate)
