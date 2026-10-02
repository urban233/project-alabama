"""Rebuild suitable closed solids as coarse convex forms, retaining UV colour data."""
import itertools
import math

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform


def coarse_solids(mesh, maximum_error):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00001)
    uv = bm.loops.layers.uv.active
    unseen = set(bm.verts)
    components = []
    while unseen:
        first = unseen.pop()
        vertices = {first}
        pending = [first]
        while pending:
            vertex = pending.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other)
                    vertices.add(other)
                    pending.append(other)
        faces = {face for vertex in vertices for face in vertex.link_faces}
        edges = {edge for vertex in vertices for edge in vertex.link_edges}
        # Closed manifold genus-zero solids only: keep openings, tubes, windows,
        # thin signs and the many open façade surfaces in their source topology.
        if len(faces) < 40 or not all(edge.is_manifold for edge in edges):
            continue
        if len(vertices) - len(edges) + len(faces) != 2:
            continue
        span = [max(v.co[axis] for v in vertices) - min(v.co[axis] for v in vertices) for axis in range(3)]
        if min(span) < .04:
            continue
        components.append((vertices, faces))

    rebuilt = 0
    for vertices, face_set in components:
        points = [v.co.copy() for v in vertices]
        indices = {vertex: index for index, vertex in enumerate(vertices)}
        faces = list(face_set)
        polygons = [[indices[loop.vert] for loop in face.loops] for face in faces]
        if not all(len(face) == 3 for face in polygons):
            continue
        # Use a local origin for volume calculations far from the scene origin.
        origin = sum(points, Vector()) / len(points)
        local = [point - origin for point in points]
        source_volume = abs(sum(local[t[0]].dot(local[t[1]].cross(local[t[2]])) / 6 for t in polygons))
        if source_volume < .000001:
            continue
        directions = [Vector(v) for v in itertools.product((-1, 1), repeat=3)]
        directions += [Vector(v) for v in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1))]
        support = {max(range(len(points)), key=lambda i: local[i].dot(direction)) for direction in directions}
        hull = bmesh.new()
        for index in support:
            hull.verts.new(points[index])
        try:
            bmesh.ops.convex_hull(hull, input=list(hull.verts), use_existing_faces=False)
            if not hull.faces or not all(edge.is_manifold for edge in hull.edges):
                continue
            bmesh.ops.triangulate(hull, faces=list(hull.faces))
            if len(hull.faces) >= len(faces) * .8:
                continue
            # Translation avoids cancellation for small props several km away.
            for vertex in hull.verts:
                vertex.co -= origin
            hull.normal_update()
            volume = abs(hull.calc_volume(signed=True))
            for vertex in hull.verts:
                vertex.co += origin
            if not .55 <= volume / source_volume <= 1.08:
                continue
            tree = BVHTree.FromPolygons(points, polygons, all_triangles=True)
            samples = [v.co for v in hull.verts]
            samples += [(e.verts[0].co + e.verts[1].co) / 2 for e in hull.edges]
            samples += [f.calc_center_median() for f in hull.faces]
            if any(tree.find_nearest(point)[3] is None or tree.find_nearest(point)[3] > maximum_error for point in samples):
                continue
            new_vertices = {v: bm.verts.new(v.co) for v in hull.verts}
            for face in hull.faces:
                target = bm.faces.new([new_vertices[loop.vert] for loop in face.loops])
                _, _, face_index, _ = tree.find_nearest(face.calc_center_median())
                source = faces[face_index]
                p = [loop.vert.co for loop in source.loops]
                values = [Vector((loop[uv].uv.x, loop[uv].uv.y, 0)) for loop in source.loops]
                for loop in target.loops:
                    value = barycentric_transform(loop.vert.co, *p, *values)
                    loop[uv].uv = value.xy
            bmesh.ops.delete(bm, geom=list(face_set), context='FACES')
            rebuilt += 1
        finally:
            hull.free()
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return rebuilt


def verify_coarse_solids():
    import bpy
    mesh = bpy.data.meshes.new('Coarse solid fixture')
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.2)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    uv = bm.loops.layers.uv.new('UVMap')
    for face in bm.faces:
        for loop in face.loops:
            loop[uv].uv = ((loop.vert.co.x + .2) / .4, (loop.vert.co.y + .2) / .4)
    bm.to_mesh(mesh)
    bm.free()
    mesh.calc_loop_triangles()
    before = len(mesh.loop_triangles)
    assert coarse_solids(mesh, .15) == 1
    mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles) < before / 4
    assert all(math.isfinite(value) for loop in mesh.uv_layers.active.data for value in loop.uv)
    check = bmesh.new()
    check.from_mesh(mesh)
    assert all(edge.is_manifold for edge in check.edges)
    check.free()
    bpy.data.meshes.remove(mesh)
