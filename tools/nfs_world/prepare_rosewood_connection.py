"""Build private lane qualification data and an exclusive source-road seam mesh.

Downtown owns any overlapping road area. Rosewood keeps its original source model
and receives only the derived collider triangles in its scene; UV/visual assets
are independent. All calculations use source X/Z and retain Rosewood's exact grade.
"""
import argparse
import json
from pathlib import Path
import numpy as np
from audit_district_exits import roads
from district_config import SHARED_ORIGIN, load
from kn5 import Reader
from seam_collision import area, subtract


def vector(point):
    p=np.asarray(point)-np.asarray(SHARED_ORIGIN)
    return dict(x=float(-p[0]),y=float(p[1]),z=float(p[2]))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--root',type=Path,required=True)
    args=parser.parse_args(); root=args.root.resolve()
    config=load(root,Path('docs/districts/rosewood.json'))
    output=root/config['outputRoot']; evidence=root/config['artifactRoot']
    lanes=json.loads((root/config['sourceRoot']/'rosewood/data/traffic.json').read_text())['lanes']
    # Both lanes continue into unimported Downtown Palmont. Keep endpoints
    # about 20 metres inside its retained exit-4 barrier; preserve source data.
    forward=next(l for l in lanes if l['id']==807)['points'][4:67]
    reverse=next(l for l in lanes if l['id']==808)['points'][43:]
    route=dict(legs=[dict(name='downtown-to-rosewood',points=[vector(p) for p in forward]),
                     dict(name='rosewood-to-downtown',points=[vector(p) for p in reverse])],
               seamCentre=vector([1674.5,127.65,63]),seamOutward=dict(x=.9855065,y=0,z=.1696375),
               downtownConnection='downtown-exit-3',rosewoodConnection='rosewood-exit-7')
    # The source pit marker is beside the drivable road. Choose the nearest
    # supplied lane point at its elevation; Unity independently checks all tyres
    # and clearance before accepting this as a district recovery pose.
    pit=np.asarray(json.loads((output/'District/contract.json').read_text())['sourceSpawn'])
    candidates=[]
    for lane in lanes:
        points=np.asarray(lane['points'])
        for index in range(len(points)-1):
            distance=np.linalg.norm((points[index]-pit)[[0,2]])
            if distance < 50 and abs(points[index,1]-pit[1]) < 10:
                candidates.append((distance,lane['id'],index,points))
    if not candidates: raise RuntimeError('No nearby source lane for Rosewood recovery')
    _,lane_id,index,points=min(candidates,key=lambda candidate: candidate[:3])
    forward=points[index+1]-points[index];forward[1]=0;forward/=np.linalg.norm(forward)
    route.update(recoveryPosition=vector(points[index]),
                 recoveryForward=dict(x=float(-forward[0]),y=0,z=float(forward[2])),
                 recoverySourceLane=lane_id,recoverySourcePoint=index)
    (output/'qualification-route.json').write_text(json.dumps(route,indent=2)+'\n')
    # Limit the ownership cut to this reviewed connection and correct road level.
    centre=np.array([1674.5,127.65,63]); radius=np.array([35,5,35])
    downtown=root/'source-art/maps/nfs-world/original/mauleous_nfs_world/nfs-world-DowntownRockport-RoadsPhysical.kn5'
    patches=[]
    for triangles in roads(downtown):
        keep=np.all(triangles.min(axis=1)<=centre+radius,axis=1)&np.all(triangles.max(axis=1)>=centre-radius,axis=1)
        if keep.any(): patches.extend(triangles[keep].astype(float))
    bins={}
    for triangle in patches:
        low=np.floor(triangle[:,[0,2]].min(axis=0)/8).astype(int);high=np.floor(triangle[:,[0,2]].max(axis=0)/8).astype(int)
        for x in range(low[0],high[0]+1):
            for z in range(low[1],high[1]+1): bins.setdefault((x,z),[]).append(triangle)
    overrides=[]; total_removed=0.; changed_faces=0
    source=root/config['sourceRoot']/'nfs-world-Rosewood-RoadsPhysical.kn5'
    with Reader(source) as reader:
      for item in reader.meshes():
        record=item['record']
        if not record['active'] or not record['name'].startswith('1ROAD'): continue
        vertices=item['positions'].astype(float); source_indices=item['triangles']; added=[]; removed=0.; faces=0
        all_triangles=vertices[source_indices]
        normals=np.cross(all_triangles[:,1]-all_triangles[:,0],all_triangles[:,2]-all_triangles[:,0])
        eligible=(normals[:,1] > .5*np.linalg.norm(normals,axis=1)) & np.all(all_triangles.min(axis=1)<=centre+radius,axis=1) & np.all(all_triangles.max(axis=1)>=centre-radius,axis=1)
        replacements={}
        for face_index in np.flatnonzero(eligible):
            triangle=all_triangles[face_index];low=triangle.min(axis=0);high=triangle.max(axis=0)
            lower=np.floor(low[[0,2]]/8).astype(int);upper=np.floor(high[[0,2]]/8).astype(int); candidates={}
            for x in range(lower[0],upper[0]+1):
                for z in range(lower[1],upper[1]+1):
                    for candidate in bins.get((x,z),[]): candidates[id(candidate)]=candidate
            pieces=[list(triangle)]; face_removed=0.
            for owner in candidates.values():
                if owner[:,1].min()>high[1]+.05 or owner[:,1].max()<low[1]-.05: continue
                if np.any(owner[:,[0,2]].min(axis=0)>high[[0,2]]) or np.any(owner[:,[0,2]].max(axis=0)<low[[0,2]]): continue
                next_pieces=[]
                for piece in pieces:
                    kept,cut=subtract(piece,owner);next_pieces.extend(kept);face_removed+=cut
                pieces=next_pieces
                if not pieces: break
            if face_removed < 1e-7: continue
            removed+=face_removed;faces+=1;replacement=[]
            for piece in pieces:
                if area(piece)<1e-8: continue
                offset=len(vertices)+len(added); added.extend(piece)
                for i in range(1,len(piece)-1): replacement.extend([offset,offset+i,offset+i+1])
            replacements[face_index]=replacement
        if faces:
            # Deduplicate degenerate source faces exactly as the Blender importer does.
            points=np.vstack([vertices,np.asarray(added).reshape(-1,3)])
            indices=[index for face_index,original in enumerate(source_indices) for index in replacements.get(face_index,original)]
            indices=np.asarray(indices,dtype=np.int32).reshape(-1,3)
            cross=np.cross(points[indices[:,1]]-points[indices[:,0]],points[indices[:,2]]-points[indices[:,0]])
            indices=indices[np.einsum('ij,ij->i',cross,cross)>1e-16]
            used,inverse=np.unique(indices,return_inverse=True)
            overrides.append(dict(name=f"RoadsPhysical_{record['index']:05d}",sourceName=record['name'],
                worldVertices=[vector(p) for p in points[used]],indices=inverse.reshape(-1,3)[:,::-1].ravel().tolist(),
                removedOverlapSquareMetres=removed,changedFaces=faces,originalTriangles=len(source_indices),derivedTriangles=len(indices)))
            total_removed+=removed;changed_faces+=faces
    result=dict(owner='downtown',neighbour='rosewood',originalModelsChanged=False,meshes=overrides,
                removedOverlapSquareMetres=total_removed,changedFaces=changed_faces)
    (output/'seam-collision.json').write_text(json.dumps(result,indent=2)+'\n')
    (evidence/'seam-ownership.json').write_text(json.dumps({k:v for k,v in result.items() if k!='meshes'},indent=2)+'\n')
    print('Derived seam collision:',len(overrides),'meshes,',changed_faces,'faces, removed overlapping area',total_removed,'m2',flush=True)
    print('Qualification legs:',[len(l['points']) for l in route['legs']],flush=True)


if __name__=='__main__': main()
