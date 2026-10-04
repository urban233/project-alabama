"""Rebuild suitable closed solids as coarse convex forms, retaining UV colour data."""
import itertools
import math

import numpy as np

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform


def facet_round_props(mesh, sides=12, maximum_displacement=.015, weld_tolerance=.001):
    """Project round, elongated components onto moderate polygonal radial profiles.

    Intended for explicitly reviewed barrel/hydrant/bin meshes, including open
    shells. Keep every face and its UV corners; do not close openings or replace
    a cylinder with a convex hull. PCA follows tilted source instances as well.
    """
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=weld_tolerance)
    unseen = set(bm.verts)
    changed = 0
    while unseen:
        first = unseen.pop()
        pending, vertices = [first], {first}
        while pending:
            vertex = pending.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other); vertices.add(other); pending.append(other)
        ordered = sorted(vertices, key=lambda v: v.index)
        if len(ordered) < 40:
            continue
        points = np.array([v.co[:] for v in ordered], dtype=np.float64)
        centre = points.mean(axis=0)
        values, axes = np.linalg.eigh(np.cov((points-centre).T))
        # Round cross-section and a distinct long axis; reject boxes, planes,
        # fittings, thin cards and irregular connected source geometry.
        if values[0] < .001 or values[1]/values[0] > 1.3 or values[2]/values[1] < 1.35:
            continue
        local = (points-centre) @ axes
        radial = np.linalg.norm(local[:, :2], axis=1)
        angle = np.arctan2(local[:, 1], local[:, 0])
        step = 2*math.pi/sides
        folded = (angle + step/2) % step - step/2
        factor = math.cos(step/2)/np.cos(folded)
        shift = np.minimum(radial*(1-factor), maximum_displacement)
        projected = local.copy()
        projected[:, :2] *= (1-shift/np.maximum(radial, 1e-8))[:, None]
        proposed = projected @ axes.T + centre
        if np.max(np.abs(np.array([points.min(0), points.max(0)])-
                         np.array([proposed.min(0), proposed.max(0)]))) > .015:
            continue
        if np.max(np.linalg.norm(proposed-points, axis=1)) < .001:
            continue
        old_normals = {face: face.normal.copy() for v in vertices for face in v.link_faces}
        for vertex, point in zip(ordered, proposed): vertex.co = point
        bm.normal_update()
        if any(face.normal.dot(normal) < .5 for face, normal in old_normals.items()):
            for vertex, point in zip(ordered, points): vertex.co = point
            bm.normal_update()
            continue
        changed += 1
    if changed:
        bm.to_mesh(mesh)
    bm.free()
    return changed


def verify_round_props():
    """An open cylinder stays open and retains every face/UV corner."""
    import bpy
    points = [(math.cos(i*math.pi/12)*.4, math.sin(i*math.pi/12)*.4, z)
              for z in (0, 1.5) for i in range(24)]
    faces = [(i, (i+1)%24, (i+1)%24+24, i+24) for i in range(24)]
    mesh = bpy.data.meshes.new('round prop guard fixture')
    mesh.from_pydata(points, [], faces)
    uv = mesh.uv_layers.new()
    for i, loop in enumerate(uv.data): loop.uv = (i/len(uv.data), (i%4)/3)
    expected = [loop.uv.copy() for loop in uv.data]
    topology = [tuple(face.vertices) for face in mesh.polygons]
    try:
        # The fixture has 48 vertices and distinct longitudinal variance.
        assert facet_round_props(mesh) == 1
        assert [tuple(face.vertices) for face in mesh.polygons] == topology
        assert all((loop.uv-value).length < 1e-7 for loop, value in zip(mesh.uv_layers.active.data, expected))
        assert all((vertex.co-Vector(point)).length <= .015001 for vertex, point in zip(mesh.vertices, points))
        bm = bmesh.new(); bm.from_mesh(mesh)
        assert sum(edge.is_boundary for edge in bm.edges) == 48
        bm.free()
    finally:
        bpy.data.meshes.remove(mesh)


def coarse_solids(mesh, maximum_error, weld_tolerance=.00001, maximum_uv_error=None, preserve_topology=False):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=weld_tolerance)
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
            if preserve_topology:
                # Project the existing surface onto the coarse form. Every face
                # and per-corner UV stays attached to its original vertices.
                hull_vertices = list(hull.verts)
                hull_indices = {vertex: index for index, vertex in enumerate(hull_vertices)}
                hull_tree = BVHTree.FromPolygons([v.co for v in hull_vertices],
                    [[hull_indices[loop.vert] for loop in face.loops] for face in hull.faces], all_triangles=True)
                projected = {vertex: hull_tree.find_nearest(vertex.co) for vertex in vertices}
                if any(value[0] is None or value[3] > maximum_error for value in projected.values()):
                    continue
                if any((face.verts[1].co - face.verts[0].co).cross(face.verts[2].co - face.verts[0].co).dot(
                    (projected[face.verts[1]][0] - projected[face.verts[0]][0]).cross(
                     projected[face.verts[2]][0] - projected[face.verts[0]][0])) < -1e-8 for face in faces):
                    continue
                if max((vertex.co - value[0]).length for vertex, value in projected.items()) < .001:
                    continue
                for vertex, value in projected.items():
                    vertex.co = value[0]
                rebuilt += 1
                continue
            # A hull can match the surface while spanning several atlas islands.
            # Validate the proposed affine UV maps before replacing any faces.
            # The nearest source triangle alone cannot establish UV continuity.
            hull_faces = list(hull.faces)
            mappings = []
            for face in hull_faces:
                _, _, face_index, _ = tree.find_nearest(face.calc_center_median())
                source = faces[face_index]
                mappings.append(([loop.vert.co.copy() for loop in source.loops],
                                 [Vector((loop[uv].uv.x, loop[uv].uv.y, 0)) for loop in source.loops]))
            if maximum_uv_error is not None:
                hull_vertices = list(hull.verts)
                hull_indices = {vertex: index for index, vertex in enumerate(hull_vertices)}
                hull_tree = BVHTree.FromPolygons([v.co for v in hull_vertices],
                    [[hull_indices[loop.vert] for loop in face.loops] for face in hull_faces], all_triangles=True)
                valid_uvs = True
                for source in faces:
                    _, _, index, _ = hull_tree.find_nearest(source.calc_center_median())
                    points_uv, values_uv = mappings[index]
                    if any((barycentric_transform(loop.vert.co, *points_uv, *values_uv).xy - loop[uv].uv).length
                           > maximum_uv_error for loop in source.loops):
                        valid_uvs = False
                        break
                if not valid_uvs:
                    continue
            new_vertices = {v: bm.verts.new(v.co) for v in hull.verts}
            for face, (p, values) in zip(hull_faces, mappings):
                target = bm.faces.new([new_vertices[loop.vert] for loop in face.loops])
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


def verify_coarse_uv_guard():
    """Surface-identical atlas changes must prevent closed-solid rebuilding."""
    import bpy
    for split_atlas in (False, True):
        mesh = bpy.data.meshes.new('Hull UV continuity fixture')
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=2)
        bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=3, use_grid_fill=True)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        uv = bm.loops.layers.uv.new('UVMap')
        for index, face in enumerate(bm.faces):
            for loop in face.loops:
                loop[uv].uv = ((loop.vert.co.x + 1) / 2 + (.3 if split_atlas and index == 0 else 0),
                               (loop.vert.co.y + 1) / 2)
        bm.to_mesh(mesh)
        bm.free()
        try:
            rebuilt = coarse_solids(mesh, .01, maximum_uv_error=.0002)
            assert rebuilt == (0 if split_atlas else 1), 'Hull must retain the source UV layout'
        finally:
            bpy.data.meshes.remove(mesh)


def verify_solid_projection():
    """Coarse silhouettes can retain every source UV and triangle corner."""
    import bpy
    mesh = bpy.data.meshes.new('Solid projection fixture')
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.2)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    uv = bm.loops.layers.uv.new('UVMap')
    for face in bm.faces:
        for loop in face.loops:
            loop[uv].uv = loop.vert.co.xy
    bm.to_mesh(mesh)
    bm.free()
    positions = [v.co.copy() for v in mesh.vertices]
    uvs = [loop.uv.copy() for loop in mesh.uv_layers.active.data]
    faces = [tuple(face.vertices) for face in mesh.polygons]
    try:
        assert coarse_solids(mesh, .15, preserve_topology=True) == 1
        assert faces == [tuple(face.vertices) for face in mesh.polygons]
        assert uvs == [loop.uv for loop in mesh.uv_layers.active.data]
        assert max((a - b.co).length for a, b in zip(positions, mesh.vertices)) > .01
    finally:
        bpy.data.meshes.remove(mesh)
