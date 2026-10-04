"""Adapt the user-supplied memoov E46; run in Blender 4.4 with --disable-autoexec.

The original ZIP/GLB are never modified. Output is staged outside Unity first;
pass --install only with Unity closed, after reviewing the staged model.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
import zipfile
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'artifacts/CarReplacement'
SOURCE = ROOT / 'source-art/vehicles/supplied-e46'
STAGE = WORK / 'export'
DEST = ROOT / 'unity/Assets/Alabama/Art/Vehicles/E46'
ZIP_SHA256 = 'cab9e032dbb6dde49752287facccb164d4d637bdc60f485f828f1f344ce2dc74'
FBX_SHA256 = 'bbead0a8c581696324a8969bd0cb692ede51f483aaa043503152b12ecbfaf372'

# Linear colors, metallic, roughness, optional original texture.
MATERIALS = {
    'E46_Paint': ((.78,.81,.84,1), .12,.72, 'Material.016_Base_color.png'),
    'E46_TailLens': ((.65,.65,.65,1), .05,.55, 'BMWM3GTRE46_KIT00_BRAKELIGHT_OFF.jpg'),
    'E46_Trim': ((.018,.021,.024,1), .02,.8, None),
    'E46_Rubber': ((.014,.016,.018,1), 0,.88, None),
    'E46_Alloy': ((.30,.32,.34,1), .40,.72, None),
    'E46_Brake': ((.12,.13,.14,1), .45,.72, None),
    'E46_Caliper': ((.15,.035,.022,1), .12,.73, None),
    'E46_Glass': ((.045,.063,.078,.80), .05,.34, None),
    'E46_Lens': ((.35,.40,.44,.12), .02,.32, None),
    'E46_WhiteLight': ((.72,.72,.69,1), .15,.52, 'BMWM3GTRE46_KIT00_HEADLIGHT_ON.jpg'),
    'E46_Interior': ((.40,.40,.40,1), 0,.86, 'interior_2.png'),
    'E46_Detail': ((.57,.60,.63,1), .08,.74, 'misc2.png'),
    'E46_Plate': ((.65,.65,.65,1), 0,.8, 'R_(1).png'),
}
WHEEL_PARTS = {'KIT00_FRONT_TIRE_A.001','KIT00_FRONT_TIRE_A.003','KIT00_FRONT_TIRE_A.005',
               'brake_disk_1_metal_1_brake_disk_0.001','brake_disk_1_metal_1_brake_disk_0.002',
               'brembo.001','caliper front'}
REDUCE = {'brake_disk_1_metal_1_brake_disk_0.001':.035,
          'brake_disk_1_metal_1_brake_disk_0.002':.035,
          'caliper front':.018, 'brembo.001':.10, 'Sphere':.05,
          'KIT00_FRONT_TIRE_A.003':.22,
          'KIT00_FRONT_TIRE_A.001':.45,
          'KIT00_BODY_A.040':.14,
          'KIT00_BODY_A.027':.22,
          'KIT00_BODY_A.030':.20,
          'KIT00_BODY_A.012':.15,
          'KIT00_BODY_A.050':.20,
          'KIT00_LEFT_HEADLIGHT_A':.30,
          'KIT00_LEFT_BRAKELIGHT_A':.30}

def facet_panels(obj):
    # Merge nearly coplanar triangles into broad panels before flat shading.
    # UV/material boundaries retain the livery and structural trim divisions.
    if obj.name=='KIT00_BODY_A.040':
        bm=bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.dissolve_limit(bm,angle_limit=math.radians(1),
            use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),
            delimit={'UV','MATERIAL','SHARP'})
        bm.to_mesh(obj.data); bm.free()
    for face in obj.data.polygons: face.use_smooth=False
    if obj.data.has_custom_normals:
        # Zero vectors request the computed polygon normals instead of the
        # imported smooth normals, which otherwise hide the reduced geometry.
        obj.data.normals_split_custom_set([(0,0,0)]*len(obj.data.loops))

def family(obj, material):
    name = material.name if material else ''
    if obj.name=='KIT00_BODY_A.040': return 'E46_Paint'
    if obj.name=='KIT00_FRONT_TIRE_A.001': return 'E46_Rubber'
    if obj.name in ('KIT00_FRONT_TIRE_A.003','KIT00_FRONT_TIRE_A.005'): return 'E46_Alloy'
    if obj.name.startswith('brake_disk'): return 'E46_Brake'
    if obj.name in ('brembo.001','caliper front'): return 'E46_Caliper'
    if obj.name.startswith('KIT00_LEFT_BRAKELIGHT'): return 'E46_TailLens'
    if name=='GL_basic_glass': return 'E46_Lens' if 'HEADLIGHT' in obj.name else 'E46_Glass'
    if 'HEADLIGHT' in obj.name: return 'E46_WhiteLight' if name!='Material.007' else 'E46_Trim'
    if obj.name=='KIT00_BODY_A.007': return 'E46_Plate'
    if obj.name.startswith('KIT00_INTERIOR') or obj.name=='Sphere': return 'E46_Interior'
    if 'CHROME' in name or name=='Material.003': return 'E46_Alloy'
    if name in ('caroceria','Material.013'): return 'E46_Alloy'
    if name.startswith('DULLPLASTIC_MISC') or name in ('REJA COFRE','TUBOS LAT'): return 'E46_Detail'
    return 'E46_Trim'

def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)

def empty(name, parent=None, location=(0,0,0)):
    obj=bpy.data.objects.new(name,None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent=parent
    obj.location=location
    return obj

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--install',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for p in (WORK,SOURCE,STAGE): p.mkdir(parents=True,exist_ok=True)
    archive=ROOT/'bmw-m3-need-for-speed-most-wanted.zip'
    original=SOURCE/'original'
    original.mkdir(exist_ok=True)
    for path in (archive,ROOT/'bmw_m3_need_for_speed_most_wanted.glb'):
        if not path.is_file(): continue
        target=original/path.name
        if target.exists() and hashlib.sha256(target.read_bytes()).digest()!=hashlib.sha256(path.read_bytes()).digest():
            raise ValueError('Preserved original differs: '+str(target))
        if not target.exists(): shutil.copy2(path,target)
    extracted=SOURCE/'input'
    if archive.is_file():
        if hashlib.sha256(archive.read_bytes()).hexdigest()!=ZIP_SHA256:
            raise ValueError('The supplied ZIP differs from the inspected source.')
        with zipfile.ZipFile(archive) as z:
            for member in z.infolist():
                if not (extracted/member.filename).resolve().is_relative_to(extracted.resolve()):
                    raise ValueError('Unsafe archive path')
            z.extractall(extracted)
    if hashlib.sha256((extracted/'source/lp.fbx').read_bytes()).hexdigest()!=FBX_SHA256:
        raise ValueError('Restore the inspected FBX from the private asset pack.')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(extracted/'source/lp.fbx'))
    sources=list(bpy.data.objects)
    nonmeshes=[o for o in sources if o.type!='MESH']
    meshes=[o for o in sources if o.type=='MESH' and len(o.data.polygons)]
    report={'sourceTriangles':sum(triangles(o) for o in meshes),'parts':[],
            'sourceZipSha256':ZIP_SHA256,
            'forward':'-Y','up':'+Z','license':'CC-BY-4.0 as declared in supplied GLB',
            'creator':'memoov','style':'docs/art-direction/2026-10-02-nfsmw/balanced/',
            'geometryRevision':2,'shading':'flat panels with 1 degree planar dissolve'}
    materials={}
    contract=[]
    for name,(color,metal,rough,texture) in MATERIALS.items():
        m=bpy.data.materials.new(name)
        m.diffuse_color=color
        m.use_nodes=True
        bsdf=m.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value=color
        bsdf.inputs['Metallic'].default_value=metal
        bsdf.inputs['Roughness'].default_value=rough
        bsdf.inputs['Alpha'].default_value=color[3]
        if texture:
            src=extracted/'textures'/texture
            if name=='E46_Paint':
                # Reviewed atlas edit is a private source asset, not regenerated here.
                src=SOURCE/'style/E46_Paint.png'
                if not src.is_file():
                    raise FileNotFoundError('Restore the reviewed private paint atlas: '+str(src))
            dest=STAGE/(name+src.suffix)
            shutil.copy2(src,dest)
            tex=m.node_tree.nodes.new('ShaderNodeTexImage')
            tex.image=bpy.data.images.load(str(dest))
            tint=m.node_tree.nodes.new('ShaderNodeMixRGB')
            tint.blend_type='MULTIPLY'
            tint.inputs[0].default_value=1
            tint.inputs[2].default_value=color
            m.node_tree.links.new(tex.outputs['Color'],tint.inputs[1])
            m.node_tree.links.new(tint.outputs[0],bsdf.inputs['Base Color'])
        materials[name]=m
        contract.append(dict(name=name,linearColor=color,metallic=metal,smoothness=1-rough,
                             texture=(name+Path(texture).suffix) if texture else None))
    root=empty('E46_Race')
    root['attribution']='BMW M3 Need for speed Most Wanted by memoov; CC-BY-4.0 per supplied GLB; adapted for Project Alabama'
    root['source_url']='https://sketchfab.com/3d-models/bmw-m3-need-for-speed-most-wanted-b6a04764e3ea42cc9021327fdccd45ab'
    groups={'Body':empty('Body',root)}
    # Source front is -Y. Centre the two axles, put tyre contact on Z=0,
    # and use the original 4.56 m silhouette at 4.65 m overall length.
    scale=4.65/4.5596
    ycentre=(-1.611+1.119)/2
    ground=-.0757
    def point(p): return Vector((p.x*scale,(p.y-ycentre)*scale,(p.z-ground)*scale))
    for axle,y in [('F',-1.611),('R',1.119)]:
        for side,sign in [('L',1),('R',-1)]:
            name='Wheel_'+axle+side
            groups[name]=empty(name,root,point(Vector((sign*.848,y,.245))))
    for source in meshes:
        before=triangles(source)
        source.data=source.data.copy()
        source.data.transform(source.matrix_world)
        source.parent=None
        source.matrix_world=Matrix.Identity(4)
        for v in source.data.vertices: v.co=point(v.co)
        mapped=[materials[family(source,m)] for m in source.data.materials]
        if not mapped: mapped=[materials['E46_Trim']]
        indices=[p.material_index for p in source.data.polygons]
        source.data.materials.clear()
        for m in mapped: source.data.materials.append(m)
        for p,i in zip(source.data.polygons,indices): p.material_index=min(i,len(mapped)-1)
        ratio=REDUCE.get(source.name)
        if ratio is None and source.name.startswith('KIT00_INTERIOR') and before>=100:
            ratio=.18
        elif ratio is None and before>=100:
            ratio=.35
        if ratio is not None:
            bpy.context.view_layer.objects.active=source
            mod=source.modifiers.new('Silhouette and panel reduction','DECIMATE')
            mod.ratio=ratio
            bpy.ops.object.modifier_apply(modifier=mod.name)
        facet_panels(source)
        report['parts'].append(dict(name=source.name,before=before,after=triangles(source),materialFamilies=list(set(m.name for m in mapped))))
        if source.name in WHEEL_PARTS:
            for name,group in groups.items():
                if name=='Body': continue
                copy=source.copy(); copy.data=source.data.copy()
                bpy.context.scene.collection.objects.link(copy)
                bm=bmesh.new(); bm.from_mesh(copy.data)
                left=name.endswith('L'); front='_F' in name
                bad=[v for v in bm.verts if (v.co.x>0)!=left or (v.co.y<0)!=front]
                bmesh.ops.delete(bm,geom=bad,context='VERTS')
                bm.to_mesh(copy.data); bm.free()
                if not copy.data.polygons:
                    bpy.data.objects.remove(copy,do_unlink=True); continue
                copy.data.transform(Matrix.Translation(-group.location))
                copy.parent=group
                copy.matrix_parent_inverse=Matrix.Identity(4)
                copy.location=(0,0,0)
            bpy.data.objects.remove(source,do_unlink=True)
        else:
            source.parent=groups['Body']
    for obj in nonmeshes:
        if obj.name in bpy.data.objects: bpy.data.objects.remove(obj,do_unlink=True)
    for obj in list(bpy.data.objects):
        if obj.type=='MESH' and not obj.data.polygons: bpy.data.objects.remove(obj,do_unlink=True)
    for name,group in groups.items():
        children=[o for o in group.children if o.type=='MESH']
        bpy.ops.object.select_all(action='DESELECT')
        for o in children: o.select_set(True)
        bpy.context.view_layer.objects.active=children[0]
        bpy.ops.object.join()
        bpy.context.object.name=name+'_Mesh'
        # Resolve source n-gons here rather than letting Unity discard
        # self-intersecting polygons with a different tessellation.
        triangulate=bpy.context.object.modifiers.new('Explicit export triangles','TRIANGULATE')
        if hasattr(triangulate,'keep_custom_normals'):
            triangulate.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        bm=bmesh.new(); bm.from_mesh(bpy.context.object.data)
        degenerate=[f for f in bm.faces if f.calc_area()<1e-8]
        bmesh.ops.delete(bm,geom=degenerate,context='FACES')
        bm.to_mesh(bpy.context.object.data); bm.free()
        mesh=bpy.context.object.data
        mesh.update()
        normals=[(0,0,0)]*len(mesh.loops)
        for face in mesh.polygons:
            face.use_smooth=False
            a,b,c=(mesh.vertices[i].co for i in face.vertices)
            normal=tuple((b-a).cross(c-a).normalized())
            for i in face.loop_indices: normals[i]=normal
        # Assign normals after final tessellation; inherited n-gon normals can
        # point away from the exported triangles and obscure the new facets.
        mesh.normals_split_custom_set(normals)
    empty('FrontMarker',root,(0,-2.5,.4))
    report['triangles']=sum(triangles(o) for o in root.children_recursive if o.type=='MESH')
    report['bodyTriangles']=triangles(bpy.data.objects['Body_Mesh'])
    body=bpy.data.objects['Body_Mesh'].data
    report['paintTriangles']=sum(len(p.vertices)-2 for p in body.polygons
        if body.materials[p.material_index].name=='E46_Paint')
    report['wheelPivotsBlender']={n:list(o.location) for n,o in groups.items() if n!='Body'}
    report['wheelRadius']=.321*scale
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for o in root.children_recursive: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(STAGE/'E46_Race.fbx'),use_selection=True,
        object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        bake_space_transform=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
    (STAGE/'E46_Materials.json').write_text(json.dumps({'materials':contract},indent=2)+'\n')
    (SOURCE/'adaptation-report.json').write_text(json.dumps(report,indent=2)+'\n')
    bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=True,do_recursive=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Supplied_E46_Adapted.blend'))
    (SOURCE/'LICENSE.md').write_text('''# Supplied car attribution

BMW M3 Need for speed Most Wanted by memoov (https://sketchfab.com/movartD).
Source: https://sketchfab.com/3d-models/bmw-m3-need-for-speed-most-wanted-b6a04764e3ea42cc9021327fdccd45ab
The supplied GLB declares CC-BY-4.0: https://creativecommons.org/licenses/by/4.0/
This records the embedded declaration; it does not independently establish rights
to underlying game content or BMW branding. Keep source and runtime assets private.

Adapted by Project Alabama contributors: strongly simplified, faceted body panels;
selective rim, tyre, grille,
lamp, trim and brake reduction,
metre scale, four independent wheel pivots, repaired texture references and
restrained URP material families matching the balanced art direction.
Original FBX/ZIP/GLB and textures are preserved; UVs and body silhouette retained.
''')
    if args.install:
        backup=WORK/'baseline/runtime'
        if not backup.exists(): shutil.copytree(DEST,backup)
        for p in STAGE.iterdir(): shutil.copy2(p,DEST/p.name)
    print(json.dumps(report,indent=2))

if __name__=='__main__': main()
