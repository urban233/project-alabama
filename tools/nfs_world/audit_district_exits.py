"""Find actual road continuations into omitted districts using shared 3D edges.

Neighbour geometry is read only for the boundary audit, never imported into Unity.
Candidate edges still require grouping and visual review before placing barriers.
"""
import argparse
import json
from pathlib import Path

import numpy as np

from kn5 import Reader
from district_config import load


GRID = .01
ORIGIN = np.array([1978.0977783, 99.6069107, 1165.7668457])


def roads(path):
    with Reader(path) as reader:
        for mesh in reader.meshes():
            record = mesh['record']
            if not record['active'] or not record['name'].startswith('1ROAD'):
                continue
            triangles = mesh['positions'][mesh['triangles']]
            normals = np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0])
            upward = normals[:, 1] > .5 * np.linalg.norm(normals, axis=1)
            yield triangles[upward]


def keys(triangles):
    quantized = np.rint(triangles / GRID).astype(np.int32)
    a = np.concatenate([quantized[:, 0], quantized[:, 1], quantized[:, 2]])
    b = np.concatenate([quantized[:, 1], quantized[:, 2], quantized[:, 0]])
    swap = (a[:, 0] > b[:, 0]) | ((a[:, 0] == b[:, 0]) &
        ((a[:, 1] > b[:, 1]) | ((a[:, 1] == b[:, 1]) & (a[:, 2] > b[:, 2]))))
    return np.concatenate([np.where(swap[:, None], b, a), np.where(swap[:, None], a, b)], axis=1)


def signed_side(edge, point):
    delta = edge[1] - edge[0]
    offset = point - edge[0]
    return delta[0] * offset[2] - delta[2] * offset[0]


def unity(point):
    result = point - ORIGIN
    result[0] *= -1
    return result.tolist()


def surface_matches(points, batches, tolerance=.35):
    """Probe projected triangle interiors at the correct road elevation."""
    matched = np.zeros(len(points), dtype=bool)
    for triangles in batches:
        if not len(triangles):
            continue
        # Spatial bins reduce pairwise work on meshes combining distant roads.
        cell = np.floor(triangles.mean(axis=1)[:, [0, 2]] / 64).astype(np.int32)
        cells, group = np.unique(cell, axis=0, return_inverse=True)
        for index in range(len(cells)):
            patch = triangles[group == index].astype(np.float64)
            low = patch.min(axis=(0, 1)); high = patch.max(axis=(0, 1))
            candidates = np.flatnonzero(~matched & np.all(points >= low - [0, tolerance, 0], axis=1) &
                                       np.all(points <= high + [0, tolerance, 0], axis=1))
            if not len(candidates):
                continue
            a = patch[:, 0]; ab = patch[:, 1] - a; ac = patch[:, 2] - a
            denominator = ab[:, 0] * ac[:, 2] - ab[:, 2] * ac[:, 0]
            valid = abs(denominator) > 1e-8
            a, ab, ac, denominator = a[valid], ab[valid], ac[valid], denominator[valid]
            for start in range(0, len(candidates), 32):
                selection = candidates[start:start + 32]
                offset = points[selection, None, :] - a[None, :, :]
                u = (offset[:, :, 0] * ac[:, 2] - offset[:, :, 2] * ac[:, 0]) / denominator
                v = (ab[:, 0] * offset[:, :, 2] - ab[:, 2] * offset[:, :, 0]) / denominator
                elevation = a[:, 1] + u * ab[:, 1] + v * ac[:, 1]
                inside = (u >= -1e-5) & (v >= -1e-5) & (u + v <= 1 + 1e-5)
                matched[selection] |= np.any(inside & (abs(elevation - points[selection, None, 1]) <= tolerance), axis=1)
    return matched


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--config', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    config = load(root, args.config)
    output_root = root / config['artifactRoot']
    output_root.mkdir(parents=True, exist_ok=True)
    source = root / config['sourceRoot'] / f"nfs-world-{config['sourceDistrict']}-RoadsPhysical.kn5"
    triangles = np.concatenate(list(roads(source)))
    quantized = np.rint(triangles / GRID).astype(np.int32)
    order = np.lexsort((quantized[:, :, 2], quantized[:, :, 1], quantized[:, :, 0]), axis=1)
    canonical = np.take_along_axis(quantized, order[:, :, None], axis=1).reshape(-1, 9)
    _, keep = np.unique(canonical, axis=0, return_index=True)
    triangles = triangles[keep]
    edges = keys(triangles)
    unique, first, counts = np.unique(edges, axis=0, return_index=True, return_counts=True)
    centroids = np.tile(triangles.mean(axis=1), (3, 1))
    boundary = {}
    for key, index, count in zip(unique, first, counts):
        if count != 1:
            continue
        edge = key.reshape(2, 3).astype(float) * GRID
        inside = signed_side(edge, centroids[index])
        if abs(inside) > .01:
            boundary[tuple(key)] = dict(edge=edge, inside=inside, centre=centroids[index])
    print('Source upward faces', len(triangles), 'boundary edges', len(boundary), flush=True)
    matched = {}
    records = list(boundary.items())
    probes = []
    for key, record in records:
        edge = record['edge']; delta = edge[1] - edge[0]
        normal = np.cross(delta, record['centre'] - edge.mean(axis=0))
        direction = np.array([delta[2], 0, -delta[0]]) * np.sign(record['inside'])
        direction /= np.linalg.norm(direction)
        direction[1] = -(normal[0] * direction[0] + normal[2] * direction[2]) / normal[1]
        probes.append(edge.mean(axis=0) + direction)
    probes = np.asarray(probes)
    np.savez_compressed(output_root / 'road-boundary-probes.npz',
                        edges=np.asarray([entry['edge'] for _, entry in records]), probes=probes)
    owned = surface_matches(probes, [triangles])
    print('Outside-own-road probes', int((~owned).sum()), flush=True)
    paths = sorted((root / 'source-art/maps/nfs-world/neighbor-roads').rglob('*-RoadsPhysical.kn5'))
    if args.config:
        paths = [p for p in paths if p.name != source.name]
        paths.append(root / 'source-art/maps/nfs-world/original/mauleous_nfs_world/nfs-world-DowntownRockport-RoadsPhysical.kn5')
    if len(paths) != 6:
        raise ValueError('Expected six omitted districts for an exhaustive connection audit')
    for path in paths:
        candidates = np.flatnonzero(~owned)
        crossing = surface_matches(probes[candidates], roads(path))
        for index in candidates[crossing]:
            matched.setdefault(records[index][0], set()).add(path.stem)
        print(path.name, 'matched edges', len(matched), flush=True)
    result = []
    for key, neighbors in matched.items():
        edge = boundary[key]['edge']
        result.append(dict(a=unity(edge[0]), b=unity(edge[1]),
                           sourceA=edge[0].tolist(), sourceB=edge[1].tolist(),
                           neighbours=sorted(neighbors)))
    output = output_root / 'district-exits.json'
    output.write_text(json.dumps(dict(gridMetres=GRID, probeDistanceMetres=1, surfaceHeightToleranceMetres=.35,
                                     sourceBoundaryEdges=len(boundary), outsideOwnRoadProbes=int((~owned).sum()),
                                     continuationEdges=result), indent=2) + '\n')
    print('Saved', len(result), 'confirmed continuation edges', flush=True)


if __name__ == '__main__':
    main()
