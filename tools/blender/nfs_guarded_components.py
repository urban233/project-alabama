"""Reduce connected fittings independently while preserving open boundary curves."""
from collections import Counter

import bmesh
import bpy
import numpy as np

from simplify_nfs_world import bounds, simplify_planes


def boundary_edges(mesh):
    # Match Unity's geometric edge keys across FBX UV and normal splits.
    mesh.calc_loop_triangles()
    keys = [tuple(round(vertex.co[axis] * 1000) for axis in range(3)) for vertex in mesh.vertices]
    counts = Counter()
    for triangle in mesh.loop_triangles:
        for corner in range(3):
            a = keys[triangle.vertices[corner]]
            b = keys[triangle.vertices[(corner + 1) % 3]]
            if a != b:
                counts[tuple(sorted((a, b)))] += 1
    return {edge for edge, count in counts.items() if count == 1}


def covers(segments, target, tolerance=.002):
    extra = target - segments
    if not extra:
        return True
    if not segments:
        return False
    starts = np.array([edge[0] for edge in segments], dtype=float)
    directions = np.array([edge[1] for edge in segments], dtype=float) - starts
    squared = np.sum(directions * directions, axis=1)
    endpoints = {point for edge in segments for point in edge}
    points = {point for edge in extra for point in (
        edge[0], edge[1], tuple(round((edge[0][axis] + edge[1][axis]) / 2) for axis in range(3)))}
    for point in points - endpoints:
        along = np.clip(np.sum((point - starts) * directions, axis=1) / squared, 0, 1)
        distances = np.sum((starts + along[:, None] * directions - point) ** 2, axis=1)
        if distances.min() > (tolerance * 1000) ** 2:
            return False
    return True


def simplify_components(mesh, angle):
    """Keep unsuitable pieces exact; apply only reductions with intact borders/UVs."""
    before = boundary_edges(mesh)
    original_bounds = bounds(mesh)
    source = bmesh.new()
    source.from_mesh(mesh)
    # Imported vertices at normal/UV splits can differ by submillimetre noise.
    bmesh.ops.remove_doubles(source, verts=list(source.verts), dist=.001)
    source.verts.index_update()
    tag = source.faces.layers.int.new('NfsStudyComponent')
    remaining = set(source.verts)
    components = 0
    while remaining:
        components += 1
        seed = min(remaining, key=lambda vertex: (tuple(vertex.co), vertex.index))
        remaining.remove(seed)
        stack = [seed]
        connected = set(stack)
        while stack:
            for edge in stack.pop().link_edges:
                for vertex in edge.verts:
                    if vertex in remaining:
                        remaining.remove(vertex)
                        connected.add(vertex)
                        stack.append(vertex)
        for vertex in connected:
            for face in vertex.link_faces:
                face[tag] = components
    merged = bmesh.new()
    accepted = 0
    for component in range(1, components + 1):
        piece = source.copy()
        piece_tag = piece.faces.layers.int.get('NfsStudyComponent')
        bmesh.ops.delete(piece, geom=[face for face in piece.faces if face[piece_tag] != component], context='FACES')
        if not piece.faces:
            piece.free()
            continue
        original = mesh.copy()
        piece.to_mesh(original)
        piece.free()
        original_edges = boundary_edges(original)
        candidate = original.copy()
        simplify_planes(candidate, angle=angle, preserve_normals=False)
        candidate_edges = boundary_edges(candidate)
        candidate.calc_loop_triangles()
        valid = 0 < len(candidate.loop_triangles) < len(original.loop_triangles) and \
            np.abs(bounds(candidate) - bounds(original)).max() <= .002 and \
            covers(original_edges, candidate_edges) and covers(candidate_edges, original_edges)
        merged.from_mesh(candidate if valid else original)
        accepted += int(valid)
        bpy.data.meshes.remove(original)
        bpy.data.meshes.remove(candidate)
    result = mesh.copy()
    merged.to_mesh(result)
    merged.free()
    source.free()
    after = boundary_edges(result)
    valid = np.abs(bounds(result) - original_bounds).max() <= .002 and \
        covers(before, after) and covers(after, before)
    if valid:
        final = bmesh.new()
        final.from_mesh(result)
        layer = final.faces.layers.int.get('NfsStudyComponent')
        if layer is not None:
            final.faces.layers.int.remove(layer)
        final.to_mesh(mesh)
        final.free()
        mesh.update()
    bpy.data.meshes.remove(result)
    return accepted if valid else 0


def verify_components():
    mesh = bpy.data.meshes.new('Guarded connector fixture')
    vertices = [(x, y, 0) for y in range(3) for x in range(3)]
    faces = []
    for y in range(2):
        for x in range(2):
            a = y * 3 + x
            faces.extend([(a, a + 1, a + 4), (a, a + 4, a + 3)])
    mesh.from_pydata(vertices, [], faces)
    uv = mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (point.x / 2, point.y / 2)
    before = boundary_edges(mesh)
    assert simplify_components(mesh, .1) == 1, 'Safe planar component was not reduced'
    mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles) < 8, 'Component reduction retained every source triangle'
    after = boundary_edges(mesh)
    assert covers(before, after) and covers(after, before), 'Open fixture boundary moved'
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        np.testing.assert_allclose(mesh.uv_layers.active.data[loop.index].uv, (point.x / 2, point.y / 2), atol=.00001)
    bpy.data.meshes.remove(mesh)
    raised = bpy.data.meshes.new('Connector silhouette fixture')
    vertices[4] = (1, 1, .2)
    raised.from_pydata(vertices, [], faces)
    raised.uv_layers.new(name='UVMap')
    before_bounds = bounds(raised).copy()
    assert simplify_components(raised, 1) == 0, 'A reduction that removes the raised silhouette was accepted'
    np.testing.assert_array_equal(bounds(raised), before_bounds)
    bpy.data.meshes.remove(raised)
