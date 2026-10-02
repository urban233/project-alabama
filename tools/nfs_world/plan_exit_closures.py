"""Group audited road frontiers and prepare local, reviewable barrier spans."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from audit_district_exits import ORIGIN
from kn5 import Reader


def main():
    root = Path(__file__).resolve().parents[2]
    output = root / 'artifacts/NfsWorld'
    audit = json.loads((output / 'district-exits.json').read_text())
    source = np.load(output / 'road-boundary-probes.npz')
    source_edges = source['edges']; source_mid = source_edges.mean(axis=1)
    probes = source['probes']
    outward = probes - source_mid
    # The northern connector ends in the supplied geometry without a neighbour
    # crossing. Its terminal edge is the west-facing frontier of the top stub.
    north = (source_mid[:, 2] > 3650) & (source_mid[:, 0] < 2460) & (outward[:, 0] < -.7)
    if north.sum() < 3:
        raise ValueError('Northern terminal frontier requires inspection')
    lookup = {tuple(np.rint(edge.reshape(-1) / .01).astype(int)): index for index, edge in enumerate(source_edges)}
    records = audit['continuationEdges']
    centres = np.array([(np.array(entry['sourceA']) + np.array(entry['sourceB'])) / 2 for entry in records])
    remaining = set(range(len(records))); groups = []
    while remaining:
        group = {remaining.pop()}; queue = list(group)
        while queue:
            index = queue.pop()
            linked = {other for other in remaining if np.linalg.norm(centres[other][[0, 2]] - centres[index][[0, 2]]) < 30
                      and abs(centres[other][1] - centres[index][1]) < 2}
            remaining -= linked; group |= linked; queue.extend(linked)
        indices = [lookup[tuple(np.rint(np.array([records[index]['sourceA'], records[index]['sourceB']]).reshape(-1) / .01).astype(int))]
                   for index in group]
        neighbours = sorted({name for index in group for name in records[index]['neighbours']})
        groups.append((indices, ', '.join(name.removeprefix('nfs-world-').removesuffix('-RoadsPhysical') for name in neighbours), 'neighbour-road surface probes'))
    groups.append((np.flatnonzero(north).tolist(), 'Northern connector', 'supplied-road terminal; no neighbouring surface continuation'))
    closures = []
    for indices, name, evidence in groups:
        edges = source_edges[indices]
        length = np.linalg.norm(edges[:, 1] - edges[:, 0], axis=1)
        forward = np.average(outward[indices], axis=0, weights=length)
        forward[1] = 0; forward /= np.linalg.norm(forward)
        right = np.array([-forward[2], 0, forward[0]])
        points = edges.reshape(-1, 3); centre = points.mean(axis=0)
        projection = (points - centre) @ right
        # Place the wall five metres inside the retained road, with curb overlap.
        centre -= forward * 5
        a = centre + right * (projection.min() - .75)
        b = centre + right * (projection.max() + .75)
        def unity(point):
            result = point - ORIGIN; result[0] *= -1
            return result.tolist()
        direction = forward.copy(); direction[0] *= -1
        closures.append(dict(name=name, start=unity(a), end=unity(b), outward=direction.tolist(),
                             evidence=evidence, auditedEdges=len(indices), sourceStart=a.tolist(), sourceEnd=b.tolist()))
    destination = root / 'unity/Assets/Alabama/Art/Maps/NfsWorld/ArtPass/exits.json'
    previous = json.loads(destination.read_text()) if destination.exists() else {}
    # Re-running identical inputs retains their inspection; changed spans need a
    # fresh geometry/road-level review before the Unity importer accepts them.
    reviewed = previous.get('reviewed', False) and previous.get('closures') == closures
    manifest = dict(reviewed=reviewed, sourceCollisionChanged=False, closures=closures)
    destination.write_text(json.dumps(manifest, indent=2) + '\n')
    # A diagnostic geometry diagram, not a game texture.
    canvas = Image.new('RGB', (1800, 1800), '#17222c'); draw = ImageDraw.Draw(canvas)
    path = root / 'source-art/maps/nfs-world/original/mauleous_nfs_world/nfs-world-DowntownRockport-Roads.kn5'
    triangles = []
    with Reader(path) as reader:
        for mesh in reader.meshes():
            if mesh['record']['active'] and mesh['record']['name'].startswith('VISROAD'):
                triangles.append(mesh['positions'][mesh['triangles']])
    triangles = np.concatenate(triangles)
    low = triangles.min(axis=(0, 1)); high = triangles.max(axis=(0, 1))
    scale = 1640 / max(high[0] - low[0], high[2] - low[2])
    def project(point):
        return (80 + (point[0] - low[0]) * scale, 1720 - (point[2] - low[2]) * scale)
    for triangle in triangles:
        draw.polygon([project(point) for point in triangle], fill='#939895')
    for index, closure in enumerate(closures):
        a, b = np.array(closure['sourceStart']), np.array(closure['sourceEnd'])
        draw.line([project(a), project(b)], fill='#f5ae35', width=7)
        u, v = project((a + b) / 2)
        draw.text((u + 12, v - 8), str(index + 1) + ': ' + closure['name'], fill='#ffd26d')
    draw.text((40, 25), 'Downtown Rockport - six prototype exit closures (source geometry, source X/Z)', fill='white')
    canvas.save(output / 'exit-closure-plan.png')
    print(json.dumps(closures, indent=2))


if __name__ == '__main__':
    main()
