using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Driving;
using Alabama.MicroSlice;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace Alabama.Editor
{
    public static class MicroSliceSetup
    {
        public const string ScenePath = MicroSliceKit.Art + "/SafehouseMicroSlice.unity";
        public const string ReferenceBase = "docs/art-direction/2026-10-04-original-map-kit/";
        public static string RootPath => Directory.GetParent(Application.dataPath).Parent.FullName;
        [Serializable] public sealed class RoadDefinition { public bool closed; public Vector3[] knots; public float[] widths, banks; }
        [Serializable] public sealed class VolumeDefinition { public Vector3 position, center, size, origin; }
        [Serializable] public sealed class GateDefinition { public int index; public Vector3 position, forward; public float width; }
        [Serializable] public sealed class Plan
        {
            public string name;
            public int targetLapSeconds;
            public RoadDefinition main, shortcut, garageEntry;
            public VolumeDefinition garage, courtyard, terrain;
            public GateDefinition[] gates;
            public Vector3[] curbOpenings;
        }
        public static Plan ReadPlan() => JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(RootPath,"docs/micro-slice/layout.json")));

        [MenuItem("Alabama/Micro Slice/Set Up")]
        public static void Setup()
        {
            Require(!EditorApplication.isPlaying,"Stop Play Mode before regenerating the demo.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var plan=ReadPlan();
            Require(UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.supportsHDR,"An HDR URP pipeline is required.");
            foreach(string layer in new[]{"MicroRoad","MicroCurbs","MicroStructure","MicroBreakable","MicroTrigger","MicroVehicle"}) EnsureLayer(layer);
            MicroSliceKit.Build();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var roads=new GameObject("Editable spline roads");
            var main=MakeRoad("Main circuit",plan.main,roads.transform,true,plan.curbOpenings);
            var shortcut=MakeRoad("Drive-through escape alley",plan.shortcut,roads.transform,false,null);
            // Flat courtyard and graded driveway join the top road through a dropped curb.
            var drive=MakeRoad("Garage entry",plan.garageEntry,roads.transform,false,null);
            var mainSamples=MicroSliceRoadBuilder.SampleRoad(main.GetComponent<SplineContainer>(),main.GetComponent<SplineRoadProfile>());
            var alleySamples=MicroSliceRoadBuilder.SampleRoad(shortcut.GetComponent<SplineContainer>(),shortcut.GetComponent<SplineRoadProfile>());
            var driveSamples=MicroSliceRoadBuilder.SampleRoad(drive.GetComponent<SplineContainer>(),drive.GetComponent<SplineRoadProfile>());
            var allSamples=mainSamples.Concat(alleySamples).Concat(driveSamples).ToList();
            Terrain(plan,allSamples);
            var kit=new GameObject("Original reference kit instances");
            var court=MicroSliceKit.Box(kit.transform,"Safehouse courtyard",plan.courtyard.center-Vector3.up*.1f,plan.courtyard.size,"Concrete");
            Layer(court,"MicroRoad");
            var garage=MicroSliceKit.Place("BLD_WORKSHOP",kit.transform,plan.garage.position+Vector3.forward*12,Quaternion.identity);
            Layer(garage,"MicroStructure");
            MicroSliceKit.Box(kit.transform,"Courtyard screening wall west",new Vector3(-111,25.5f,68),new Vector3(34,3,.5f),"Brick");
            MicroSliceKit.Box(kit.transform,"Courtyard screening wall east",new Vector3(-70,25.5f,68),new Vector3(14,3,.5f),"Brick");
            MicroSliceKit.Box(kit.transform,"Inner line-of-sight baffle",new Vector3(-94,25.5f,62),new Vector3(.5f,3,9),"Brick");
            var observer=new GameObject("Street line-of-sight fixture");observer.transform.position=new Vector3(-72,26,87);
            var props=new List<BreakableStructure>();
            Dress(plan,kit.transform,mainSamples,alleySamples,props);
            Bridge(kit.transform,mainSamples);
            var car=CreateCar(mainSamples[0]);
            var tracker=new GameObject("Main circuit progress").AddComponent<RouteProgress>();
            Set(tracker,"vehicle",car.transform);
            SetRoute(tracker,mainSamples.Select(s=>s.position).ToArray());
            var alternate=new GameObject("Shortcut circuit progress").AddComponent<RouteProgress>();
            Set(alternate,"vehicle",car.transform);
            SetRoute(alternate,ShortcutCentreline(mainSamples,alleySamples,driveSamples,plan.gates[0].position));
            alternate.gameObject.SetActive(false);
            var zoneGo=new GameObject("Courtyard cooldown sanctuary");zoneGo.transform.position=new Vector3(-110,25.8f,55);
            var zoneCollider=zoneGo.AddComponent<BoxCollider>();zoneCollider.isTrigger=true;zoneCollider.size=new Vector3(23,4,18);
            Layer(zoneGo,"MicroTrigger");
            var zone=zoneGo.AddComponent<CooldownSanctuary>();zone.Configure(car,observer.transform,1<<EnsureLayer("MicroStructure"));
            var session=new GameObject("Micro-slice lap and fixtures").AddComponent<MicroSliceSession>();session.Configure(car,zone,props.ToArray());
            foreach(var gate in plan.gates)
            {
                var go=new GameObject("Checkpoint "+gate.index);go.transform.position=gate.position+Vector3.up*2;
                go.transform.rotation=Quaternion.LookRotation(gate.forward.normalized,Vector3.up);
                var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(gate.width,5,2);
                go.AddComponent<MicroSliceCheckpoint>().Configure(session,gate.index);Layer(go,"MicroTrigger");
            }
            new GameObject("Optional automatic driving probe").AddComponent<MicroSliceDriveProbe>().Configure(car,tracker,alternate,session);
            var telemetry=new GameObject("Driving overlay").AddComponent<DriveTelemetry>();Set(telemetry,"target",car);
            Lighting(car);
            foreach(var root in scene.GetRootGameObjects())
                foreach(var child in root.GetComponentsInChildren<Transform>(true))
                    if(child.gameObject.layer==0 && child.GetComponent<Collider>()!=null && child.GetComponent<Collider>().isTrigger==false)
                        child.gameObject.layer=EnsureLayer("MicroStructure");
            AssetDatabase.SaveAssets();
            Require(EditorSceneManager.SaveScene(scene,ScenePath),"Unable to save micro-slice.");
            WriteReport(mainSamples,plan);
            Verify();
        }

        private static GameObject MakeRoad(string name,RoadDefinition def,Transform parent,bool sidewalks,Vector3[] openings)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var container=go.AddComponent<SplineContainer>();
            container.Spline=new Spline(def.knots.Select(p=>(float3)p),TangentMode.AutoSmooth,def.closed);
            var profile=go.AddComponent<SplineRoadProfile>();
            profile.knotWidths=def.widths;profile.bankDegrees=def.banks;profile.sidewalks=sidewalks;profile.curbOpenings=openings;
            BakeRoad(container,profile);
            return go;
        }

        [MenuItem("Alabama/Micro Slice/Rebuild Selected Road")]
        public static void RebuildSelectedRoad()
        {
            Require(!EditorApplication.isPlaying,"Stop Play Mode before rebuilding roads.");
            var go=Selection.activeGameObject;
            Require(go!=null && go.GetComponent<SplineContainer>()!=null,"Select an editable spline-road object.");
            foreach(string id in new[]{"Asphalt","Concrete","Dirt","Yellow","White"})
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(MicroSliceKit.Art+"/MAT_"+id+".mat");
                Require(mat!=null,"Run micro-slice setup before rebuilding roads.");
                MicroSliceKit.Materials[id]=mat;
            }
            BakeRoad(go.GetComponent<SplineContainer>(),go.GetComponent<SplineRoadProfile>());
            var allSamples=Object.FindObjectsByType<SplineContainer>(FindObjectsSortMode.None)
                .Where(s=>s.GetComponent<SplineRoadProfile>()!=null)
                .SelectMany(s=>MicroSliceRoadBuilder.SampleRoad(s,s.GetComponent<SplineRoadProfile>())).ToList();
            var terrain=Object.FindFirstObjectByType<UnityEngine.Terrain>();
            if(terrain!=null) Object.DestroyImmediate(terrain.gameObject);
            Terrain(ReadPlan(),allSamples);
            var main=GameObject.Find("Main circuit");
            var mainSamples=MicroSliceRoadBuilder.SampleRoad(main.GetComponent<SplineContainer>(),main.GetComponent<SplineRoadProfile>());
            var roots=go.scene.GetRootGameObjects();
            SetRoute(roots.Single(r=>r.name=="Main circuit progress").GetComponent<RouteProgress>(),mainSamples.Select(s=>s.position).ToArray());
            var alley=GameObject.Find("Drive-through escape alley");var drive=GameObject.Find("Garage entry");
            SetRoute(roots.Single(r=>r.name=="Shortcut circuit progress").GetComponent<RouteProgress>(),ShortcutCentreline(mainSamples,
                MicroSliceRoadBuilder.SampleRoad(alley.GetComponent<SplineContainer>(),alley.GetComponent<SplineRoadProfile>()),
                MicroSliceRoadBuilder.SampleRoad(drive.GetComponent<SplineContainer>(),drive.GetComponent<SplineRoadProfile>()),ReadPlan().gates[0].position));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static void SetRoute(RouteProgress route,Vector3[] samples)
        {
            var serialized=new SerializedObject(route);
            var points=serialized.FindProperty("centreline");points.arraySize=samples.Length;
            for(int i=0;i<samples.Length;i++) points.GetArrayElementAtIndex(i).vector3Value=samples[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // A second closed centreline exists only for the opt-in physics probe; normal play is free driving.
        public static Vector3[] ShortcutCentreline(List<MicroSliceRoadBuilder.Sample> main,List<MicroSliceRoadBuilder.Sample> alley,
            List<MicroSliceRoadBuilder.Sample> drive,Vector3 start)
        {
            int last=main.Count-1;
            int Nearest(Vector3 p) => Enumerable.Range(0,last).OrderBy(i=>(main[i].position-p).sqrMagnitude).First();
            int begin=Nearest(start),exit=Nearest(alley[0].position),join=Nearest(drive[^1].position);
            var result=new List<Vector3>();
            void Append(Vector3 p) { if(result.Count==0 || Vector3.Distance(result[^1],p)>.05f) result.Add(p); }
            void MainSpan(int from,int to)
            { for(int i=from;;i=(i+1)%last) { Append(main[i].position);if(i==to) break; } }
            MainSpan(begin,exit);
            foreach(var s in alley) Append(s.position);
            foreach(var s in drive) Append(s.position);
            MainSpan(join,begin);
            result[^1]=result[0];
            return result.ToArray();
        }

        private static void BakeRoad(SplineContainer source,SplineRoadProfile profile)
        {
            var samples=MicroSliceRoadBuilder.SampleRoad(source,profile);
            Require(MicroSliceRoadBuilder.IsContinuous(samples,source.Spline.Closed),"Road has an invalid seam.");
            var old=source.transform.Find("Generated loft");
            if(old!=null) Object.DestroyImmediate(old.gameObject);
            var loft=new GameObject("Generated loft");loft.transform.SetParent(source.transform,false);
            // Loft vertices are converted from world samples into the source's local frame.
            loft.transform.localPosition=Vector3.zero;loft.transform.localRotation=Quaternion.identity;loft.transform.localScale=Vector3.one;
            string id=source.name.Replace(" ","_");
            MeshObject("Carriageway",MicroSliceRoadBuilder.Ribbon(samples,s=>new Vector2(-s.width/2,s.width/2)),
                id+"_Surface",loft.transform,profile.sidewalks?"Asphalt":source.name=="Garage entry"?"Concrete":"Dirt","MicroRoad",true);
            if(profile.sidewalks)
            {
                Func<MicroSliceRoadBuilder.Sample,bool> clear=s=>profile.curbOpenings==null ||
                    profile.curbOpenings.All(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(s.position.x,s.position.z))>9);
                foreach(int side in new[]{-1,1})
                {
                    MeshObject("Sidewalk "+side,MicroSliceRoadBuilder.Ribbon(samples,s=>side<0?new Vector2(-s.width/2-2,-s.width/2):new Vector2(s.width/2,s.width/2+2),.15f,clear),
                        id+"_Sidewalk_"+side,loft.transform,"Concrete","MicroCurbs",true);
                    MeshObject("Curb face "+side,MicroSliceRoadBuilder.Curb(samples,side,clear),
                        id+"_Curb_"+side,loft.transform,"Concrete","MicroCurbs",true);
                }
                foreach(float offset in new[]{-.12f,.12f})
                    MeshObject("Centre line",MicroSliceRoadBuilder.Ribbon(samples,s=>new Vector2(offset-.045f,offset+.045f),.016f),
                        id+"_Centre_"+offset,loft.transform,"Yellow","MicroRoad",false);
                foreach(int side in new[]{-1,1})
                    MeshObject("Arterial lane line",MicroSliceRoadBuilder.Ribbon(samples,s=>new Vector2(side*s.width/4-.06f,side*s.width/4+.06f),.018f,s=>s.width>14 && (int)(s.distance/6)%2==0),
                        id+"_Lane_"+side,loft.transform,"White","MicroRoad",false);
            }
        }
        private static GameObject MeshObject(string name,Mesh mesh,string id,Transform parent,string material,string layer,bool collision)
        {
            mesh.vertices=mesh.vertices.Select(v=>parent.InverseTransformPoint(v)).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=MicroSliceKit.Store(mesh,id);
            go.AddComponent<MeshRenderer>().sharedMaterial=MicroSliceKit.Materials[material];
            if(collision) go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
            Layer(go,layer);return go;
        }

        private static void Terrain(Plan plan,List<MicroSliceRoadBuilder.Sample> samples)
        {
            var data=new TerrainData {heightmapResolution=257,size=plan.terrain.size};
            var heights=new float[257,257];var origin=plan.terrain.origin;
            for(int z=0;z<257;z++) for(int x=0;x<257;x++)
            {
                float wx=origin.x+x*data.size.x/256,wz=origin.z+z*data.size.z/256;
                float hill=3+25*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-100,135,wz));
                hill+=Mathf.PerlinNoise(wx*.009f+5,wz*.009f+7)*2;
                float best=float.PositiveInfinity;MicroSliceRoadBuilder.Sample nearest=default;
                foreach(var s in samples)
                {
                    float dx=wx-s.position.x,dz=wz-s.position.z,d=dx*dx+dz*dz;
                    if(d<best) {best=d;nearest=s;}
                }
                float distance=Mathf.Sqrt(best),edge=nearest.width/2+2.4f;
                float side=Vector3.Dot(new Vector3(wx-nearest.position.x,0,wz-nearest.position.z),nearest.right);
                float roadHeight=nearest.position.y+nearest.right.y*side-.4f;
                float h=Mathf.Lerp(roadHeight,hill,Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge,edge+18,distance)));
                if(wx>-128 && wx<-60 && wz>29 && wz<68 && distance>edge) h=23.75f;
                if(wx<-120 && wz>-86 && wz<-54) h=-2;
                heights[z,x]=Mathf.Clamp01((h-origin.y)/data.size.y);
            }
            data.SetHeights(0,0,heights);
            var layer=new TerrainLayer {tileSize=new Vector2(5,5)};
            var diffuse=new Texture2D(64,64,TextureFormat.RGB24,false);
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
                diffuse.SetPixel(x,y,new Color(.34f,.31f,.23f)*(Mathf.PerlinNoise(x*.15f,y*.15f)*.15f+.85f));
            diffuse.Apply();File.WriteAllBytes(MicroSliceKit.Art+"/TerrainDirt.png",diffuse.EncodeToPNG());Object.DestroyImmediate(diffuse);
            AssetDatabase.ImportAsset(MicroSliceKit.Art+"/TerrainDirt.png");
            layer.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(MicroSliceKit.Art+"/TerrainDirt.png");
            data.terrainLayers=new[]{MicroSliceKit.Store(layer,"TerrainDirtLayer")};
            data=MicroSliceKit.Store(data,"CarvedTerrain");
            var terrain=UnityEngine.Terrain.CreateTerrainGameObject(data);terrain.name="Road cut hillside terrain";terrain.transform.position=origin;Layer(terrain,"MicroRoad");
            var material=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            terrain.GetComponent<UnityEngine.Terrain>().materialTemplate=MicroSliceKit.Store(material,"TerrainMaterial");
            terrain.GetComponent<UnityEngine.Terrain>().heightmapPixelError=8;
        }

        private static void Dress(Plan plan,Transform parent,List<MicroSliceRoadBuilder.Sample> samples,List<MicroSliceRoadBuilder.Sample> alley,List<BreakableStructure> props)
        {
            foreach(var p in new[]{new Vector3(218,2,-40),new Vector3(220,2,-100),new Vector3(90,2,-190),new Vector3(10,2,-190)})
            { var go=MicroSliceKit.Place("BLD_WAREHOUSE",parent,p,Quaternion.Euler(0,p.x>200?-90:0,0));Layer(go,"MicroStructure"); }
            foreach(var p in new[]{new Vector3(-145,24,100),new Vector3(-125,26,118),new Vector3(-70,28,120)})
            { var go=MicroSliceKit.Place("BLD_ROWHOUSE_SHOP",parent,p,Quaternion.Euler(0,180,0));Layer(go,"MicroStructure"); }
            for(int i=0;i<8;i++)
            {
                var pb=ShapeGenerator.GenerateCube(PivotLocation.Center,new Vector3(14,10+i%3*2,18));
                pb.name="Editable ProBuilder building volume";
                pb.transform.SetParent(parent,false);pb.transform.position=new Vector3(-140+i*38,7,-201);
                pb.GetComponent<Renderer>().sharedMaterial=MicroSliceKit.Materials["Concrete"];
                if(pb.GetComponent<Collider>()==null) pb.gameObject.AddComponent<BoxCollider>();
                Layer(pb.gameObject,"MicroStructure");
            }
            for(int i=18;i<samples.Count-18;i+=22)
            {
                var s=samples[i];var point=s.position-s.right*(s.width/2+6);
                if(Vector3.Distance(point,new Vector3(-95,24,55))<50) continue;
                var tree=MicroSliceKit.Place("NAT_TREE_BROAD",parent,point,Quaternion.Euler(0,i*37%360,0));
                // Terrain outside the carved road rises; seat each tree on its actual hillside.
                var terrain=Object.FindFirstObjectByType<UnityEngine.Terrain>();
                tree.transform.position=new Vector3(point.x,GroundHeight(terrain,point),point.z);
                if(i%44==18)
                {
                    var lamp=MicroSliceKit.Place("PROP_STREET_LIGHT",parent,s.position+s.right*(s.width/2+1.4f),
                        Quaternion.LookRotation(s.right,Vector3.up));
                    props.Add(Yielding(lamp,12));
                }
            }
            float lastFence=-100;
            foreach(var s in alley)
            {
                if(s.distance<35 || s.distance>alley[^1].distance-20 || s.distance-lastFence<4) continue;
                lastFence=s.distance;
                var tangent=Vector3.Cross(s.right,s.up);tangent.y=0;
                foreach(int side in new[]{-1,1})
                {
                    var fence=MicroSliceKit.Place("PROP_FENCE_04",parent,s.position+s.right*(side*(s.width/2+.5f)),
                        Quaternion.FromToRotation(Vector3.right,tangent.normalized));
                    props.Add(Yielding(fence,8));
                }
            }
            for(int i=100;i<270;i+=16)
            {
                var s=samples[i];
                var barrier=MicroSliceKit.Place("PROP_JERSEY_BARRIER",parent,s.position-s.right*(s.width/2+2.7f),
                    Quaternion.FromToRotation(Vector3.right,Vector3.Cross(s.right,Vector3.up)));
                Layer(barrier,"MicroStructure");
            }
            for(int i=0;i<8;i++)
            {
                var p=new Vector3(-213+i*12,0,-96);
                var terrain=Object.FindFirstObjectByType<UnityEngine.Terrain>();p.y=GroundHeight(terrain,p);
                MicroSliceKit.Place("NAT_BOULDER",parent,p,Quaternion.Euler(0,i*37,0));
            }
            foreach(var p in new[]{new Vector3(-122,24,48),new Vector3(-117,24,47),new Vector3(-113,24,47)})
                MicroSliceKit.Place("PROP_CRATE",parent,p,Quaternion.identity);
            MicroSliceKit.Place("PROP_CONTAINER",parent,new Vector3(-102,2,-148),Quaternion.identity);
            // Split lot shells leave a real, 5 m passage to the alley.
            foreach(float x in new[]{-97f,-53f})
            { var warehouse=MicroSliceKit.Place("BLD_WAREHOUSE",parent,new Vector3(x,2,-123),Quaternion.identity);Layer(warehouse,"MicroStructure"); }
            var scaffold=new GameObject("Collapsible lot scaffold");scaffold.transform.SetParent(parent,false);scaffold.transform.position=new Vector3(-75,2,-143);
            var parts=new List<Rigidbody>();
            foreach(float x in new[]{-3.5f,3.5f})
            {
                var post=MicroSliceKit.Box(scaffold.transform,"Scaffold support",new Vector3(x,2.6f,0),new Vector3(.16f,5.2f,.2f),"Metal");
                var body=post.AddComponent<Rigidbody>();body.mass=25;body.isKinematic=true;parts.Add(body);
            }
            for(int i=0;i<3;i++)
            {
                var beam=MicroSliceKit.Box(scaffold.transform,"Falling platform",new Vector3(0,5.2f,-.6f+i*.6f),new Vector3(8,.25f,.5f),"Wood");
                var body=beam.AddComponent<Rigidbody>();body.mass=40;body.isKinematic=true;parts.Add(body);
            }
            var trigger=scaffold.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,1.3f,0);trigger.size=new Vector3(6,2.5f,3);
            var breaker=scaffold.AddComponent<BreakableStructure>();breaker.Configure(parts.ToArray());Layer(scaffold,"MicroBreakable");props.Add(breaker);
        }

        private static BreakableStructure Yielding(GameObject go,float mass)
        {
            Layer(go,"MicroBreakable");
            var body=go.AddComponent<Rigidbody>();body.mass=mass;body.isKinematic=true;
            var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;
            var bounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var collider in go.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger))
            {
                var renderer=collider.GetComponent<Renderer>();
                if(renderer==null) continue;
                var local=renderer.localBounds;
                foreach(int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) foreach(int z in new[]{-1,1})
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3(x,y,z));
                    bounds.Encapsulate(go.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner)));
                }
            }
            trigger.center=bounds.center;trigger.size=bounds.size+Vector3.one*.8f;
            var breaker=go.AddComponent<BreakableStructure>();breaker.Configure(new[]{body});return breaker;
        }

        private static void Bridge(Transform parent,List<MicroSliceRoadBuilder.Sample> samples)
        {
            MicroSliceKit.Box(parent,"River surface",new Vector3(-180,-1.25f,-70),new Vector3(125,.1f,28),"Glass",false);
            foreach(var s in samples.Where(s=>s.position.x<-170 && s.position.z>-91 && s.position.z<-44).Where((s,i)=>i%3==0))
            {
                foreach(int side in new[]{-1,1})
                {
                    var rail=MicroSliceKit.Place("PROP_GUARDRAIL_04",parent,s.position+s.right*side*4.6f,
                        Quaternion.FromToRotation(Vector3.right,Vector3.Cross(s.right,Vector3.up)));
                    Layer(rail,"MicroStructure");
                }
            }
            foreach(float z in new[]{-83f,-60f})
            {
                var closest=samples.OrderBy(s=>(new Vector2(s.position.x+185,s.position.z-z)).sqrMagnitude).First();
                float height=closest.position.y+2-.3f;
                MicroSliceKit.Box(parent,"Bridge pier",new Vector3(closest.position.x,-2+height/2,z),new Vector3(3,height,2),"Concrete");
            }
        }

        private static ArcadeCarController CreateCar(MicroSliceRoadBuilder.Sample spawn)
        {
            var car=new GameObject("Original handling calibration coupe");
            MicroSliceKit.Box(car.transform,"Lower coupe",new Vector3(0,.64f,0),new Vector3(1.8f,.76f,4.35f),"CarBlue");
            MicroSliceKit.Box(car.transform,"Cabin",new Vector3(0,1.18f,-.1f),new Vector3(1.55f,.55f,1.9f),"Glass",false);
            var wheels=new WheelCollider[4];var visuals=new Transform[4];
            for(int i=0;i<4;i++)
            {
                var position=new Vector3(i%2==0?-.84f:.84f,.35f,i<2?1.3f:-1.3f);
                var visual=GameObject.CreatePrimitive(PrimitiveType.Cylinder);visual.name="Wheel visual "+i;Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(car.transform,false);visual.transform.localPosition=position;
                // Rotation belongs to a child mesh; controller controls the unrotated pivot.
                var pivot=new GameObject("Wheel pivot "+i).transform;pivot.SetParent(car.transform,false);pivot.localPosition=position;
                visual.transform.SetParent(pivot,false);visual.transform.localPosition=Vector3.zero;
                visual.transform.localRotation=Quaternion.Euler(0,0,90);visual.transform.localScale=new Vector3(.66f,.12f,.66f);
                visual.GetComponent<Renderer>().sharedMaterial=MicroSliceKit.Materials["Rubber"];visuals[i]=pivot;
                var physics=new GameObject("Wheel physics "+i);physics.transform.SetParent(car.transform,false);physics.transform.localPosition=position;
                wheels[i]=physics.AddComponent<WheelCollider>();
            }
            var tuning=AssetDatabase.LoadAssetAtPath<VehicleTuning>(MicroSliceKit.Art+"/MicroSliceTuning.asset");
            if(tuning==null) { tuning=ScriptableObject.CreateInstance<VehicleTuning>();AssetDatabase.CreateAsset(tuning,MicroSliceKit.Art+"/MicroSliceTuning.asset"); }
            car.AddComponent<Rigidbody>();
            var controller=car.AddComponent<ArcadeCarController>();Set(controller,"tuning",tuning);
            string[] fields={"frontLeft","frontRight","rearLeft","rearRight"};
            for(int i=0;i<4;i++) {Set(controller,fields[i],wheels[i]);Set(controller,fields[i]+"Visual",visuals[i]);}
            car.AddComponent<VehicleInput>();Layer(car,"MicroVehicle");
            var forward=Vector3.Cross(spawn.right,spawn.up).normalized;
            car.transform.SetPositionAndRotation(spawn.position-forward*4+spawn.right*2+Vector3.up*.35f,Quaternion.LookRotation(forward,Vector3.up));
            return controller;
        }

        private static void Lighting(ArcadeCarController car)
        {
            var sun=new GameObject("18 degree autumn sunlight").AddComponent<Light>();sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(18,145,0);sun.color=new Color(1,.87f,.68f);sun.intensity=1.65f;sun.shadows=LightShadows.Hard;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.49f,.53f,.58f);RenderSettings.ambientEquatorColor=new Color(.34f,.33f,.3f);RenderSettings.ambientGroundColor=new Color(.19f,.17f,.13f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.57f,.57f,.51f);RenderSettings.fogDensity=.0015f;
            var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_AtmosphereThickness",1.1f);sky.SetColor("_SkyTint",new Color(.48f,.51f,.54f));
            RenderSettings.skybox=MicroSliceKit.Store(sky,"AutumnSky");
            DynamicGI.UpdateEnvironment();
            var volume=new GameObject("Micro-slice autumn global volume").AddComponent<Volume>();volume.isGlobal=true;volume.priority=30;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.value=TonemappingMode.Neutral;
            profile.Add<WhiteBalance>(true).temperature.value=14;
            var color=profile.Add<ColorAdjustments>(true);color.contrast.value=8;color.saturation.value=-7;
            // Volume components must be subassets, otherwise saved profiles lose their overrides.
            string path=MicroSliceKit.Art+"/AutumnVolume.asset";
            var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(existing!=null)
            {
                foreach(var component in existing.components.ToArray()) Object.DestroyImmediate(component,true);
                existing.components.Clear();foreach(var component in profile.components) {AssetDatabase.AddObjectToAsset(component,existing);existing.components.Add(component);}
                Object.DestroyImmediate(profile);profile=existing;
            }
            else { AssetDatabase.CreateAsset(profile,path);foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile); }
            EditorUtility.SetDirty(profile);volume.sharedProfile=profile;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=700;camera.nearClipPlane=.1f;camera.fieldOfView=68;
            camera.gameObject.AddComponent<AudioListener>();
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1;
            var chase=camera.gameObject.AddComponent<ChaseCamera>();Set(chase,"target",car);
            camera.transform.position=car.transform.TransformPoint(new Vector3(0,1.9f,-6));camera.transform.LookAt(car.transform.TransformPoint(new Vector3(0,1.2f,12)));
        }
        public static int EnsureLayer(string name)
        {
            int layer=LayerMask.NameToLayer(name);if(layer>=0) return layer;
            var serialized=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=serialized.FindProperty("layers");
            for(int i=8;i<32;i++) if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {layers.GetArrayElementAtIndex(i).stringValue=name;serialized.ApplyModifiedPropertiesWithoutUndo();return i;}
            throw new InvalidOperationException("No free layer for "+name);
        }
        public static void Layer(GameObject go,string name)
        { int layer=EnsureLayer(name);foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer; }
        public static void Set(Object target,string name,Object value)
        {var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static void Require(bool condition,string message)
        {if(!condition) throw new InvalidOperationException(message);}
        private static float GroundHeight(UnityEngine.Terrain terrain,Vector3 point)
        {
            var local=point-terrain.transform.position;
            return terrain.terrainData.GetInterpolatedHeight(local.x/terrain.terrainData.size.x,local.z/terrain.terrainData.size.z)+terrain.transform.position.y;
        }

        [MenuItem("Alabama/Micro Slice/Verify")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var source=GameObject.Find("Main circuit").GetComponent<SplineContainer>();
            var samples=MicroSliceRoadBuilder.SampleRoad(source,source.GetComponent<SplineRoadProfile>());
            Require(source.Spline.Closed && MicroSliceRoadBuilder.IsContinuous(samples,true),"Circuit does not close cleanly.");
            Require(samples[^1].distance>800 && samples[^1].distance<1400,"Circuit is outside the micro-slice scale.");
            Require(Object.FindObjectsByType<MicroSliceCheckpoint>(FindObjectsSortMode.None).Length==5,"Ordered checkpoint/shortcut gates are missing.");
            Require(Object.FindFirstObjectByType<CooldownSanctuary>()!=null && Object.FindFirstObjectByType<MicroSliceSession>()!=null,"Cooldown/lap fixtures missing.");
            var car=Object.FindFirstObjectByType<ArcadeCarController>();Require(car!=null && car.GetComponentsInChildren<WheelCollider>().Length==4,"Drive car missing.");
            Require(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing,"Camera post-processing missing.");
            var profile=Object.FindFirstObjectByType<Volume>().sharedProfile;Require(profile.TryGet<WhiteBalance>(out var white) && white.temperature.overrideState,"Warm volume override missing.");
            foreach(var collider in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)) Require(collider.sharedMesh!=null,"Empty mesh collider.");
            Debug.Log("Micro-slice verified: "+samples[^1].distance.ToString("0.0")+" m, 5 gates, original kit, four WheelColliders, HDR URP.");
        }
        private static void WriteReport(List<MicroSliceRoadBuilder.Sample> samples,Plan plan)
        {
            Directory.CreateDirectory(Path.Combine(RootPath,"artifacts/MicroSliceSetup"));
            float maximumGrade=0;
            for(int i=1;i<samples.Count;i++)
            {
                var delta=samples[i].position-samples[i-1].position;
                maximumGrade=Mathf.Max(maximumGrade,Mathf.Abs(delta.y)/Mathf.Max(.001f,new Vector2(delta.x,delta.z).magnitude));
            }
            File.WriteAllText(Path.Combine(RootPath,"artifacts/MicroSliceSetup/geometry.json"),
                "{\"routeMetres\":"+samples[^1].distance.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+
                ",\"maximumGrade\":"+maximumGrade.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+
                ",\"targetLapSeconds\":"+plan.targetLapSeconds+",\"originalKitPrefabs\":"+MicroSliceKit.Prefabs.Count+"}");
        }
    }
}
