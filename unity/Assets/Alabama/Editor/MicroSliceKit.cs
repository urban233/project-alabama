using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Alabama.Editor
{
    // Original geometry and procedural textures. No imported map or vehicle art.
    public static class MicroSliceKit
    {
        public const string Art = "Assets/Alabama/Art/MicroSlice";
        public static readonly Dictionary<string, Material> Materials = new();
        public static readonly Dictionary<string, GameObject> Prefabs = new();
        private static readonly Dictionary<string, Mesh> cubeMeshes = new();

        public static T Store<T>(T asset, string name) where T : UnityEngine.Object
        {
            string path = Art + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (old == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, old);
            UnityEngine.Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(old);
            return old;
        }

        public static void Build()
        {
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            Material("Asphalt", new Color(.19f,.20f,.19f), .12f);
            Material("Concrete", new Color(.46f,.43f,.36f), .1f);
            Material("Brick", new Color(.43f,.25f,.16f), .12f);
            Material("Metal", new Color(.22f,.24f,.23f), .13f);
            Material("Door", new Color(.44f,.43f,.37f), .16f);
            Material("Glass", new Color(.10f,.15f,.17f), .06f);
            Material("Dirt", new Color(.35f,.31f,.22f), .08f);
            Material("Wood", new Color(.32f,.25f,.17f), .06f);
            Material("Ochre", new Color(.63f,.35f,.09f), .05f);
            Material("White", new Color(.65f,.61f,.48f), .05f);
            Material("Yellow", new Color(.66f,.47f,.17f), .05f);
            Material("CarBlue", new Color(.10f,.21f,.31f), .33f);
            Material("Rubber", new Color(.035f,.04f,.039f), .04f);
            var glow = Material("Lamp", new Color(1,.72f,.32f), .08f);
            glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(1,.55f,.17f) * 1.6f);
            BuildLeafMaterial();
            Save("BLD_WORKSHOP", Workshop());
            Save("BLD_GARAGE_BAY_04", GarageBay());
            Save("BLD_WALL_PLAIN_04", WallBay());
            Save("BLD_WAREHOUSE", Warehouse());
            Save("BLD_ROWHOUSE_SHOP", Rowhouse());
            Save("PROP_JERSEY_BARRIER", Barrier());
            Save("PROP_GUARDRAIL_04", Guardrail());
            Save("PROP_FENCE_04", Fence());
            Save("PROP_STREET_LIGHT", Lamp());
            Save("PROP_CONTAINER", Container());
            Save("PROP_CRATE", Crate());
            Save("NAT_TREE_BROAD", Tree());
            Save("NAT_BOULDER", Rock());
            AssetDatabase.SaveAssets();
        }

        private static Material Material(string id, Color color, float smoothness)
        {
            string path = Art + "/MAT_" + id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", id == "Metal" ? .2f : 0);
            if (id is "Asphalt" or "Concrete" or "Brick" or "Door" or "Wood" or "Dirt")
            {
                var tex = new Texture2D(128,128,TextureFormat.RGBA32,false);
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    float noise = Mathf.PerlinNoise(x / 17f + 3, y / 17f + 7) * .14f + .86f;
                    if (id == "Brick" && (y % 12 < 1 || (x + (y / 12 % 2) * 16) % 32 < 1)) noise *= .6f;
                    if (id == "Door" && y % 8 == 0) noise *= .8f;
                    tex.SetPixel(x,y,new Color(noise,noise,noise,1));
                }
                tex.Apply();
                File.WriteAllBytes(Art + "/TEX_" + id + ".png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(Art + "/TEX_" + id + ".png");
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/TEX_" + id + ".png"));
            }
            Materials[id] = mat; EditorUtility.SetDirty(mat); return mat;
        }

        private static void BuildLeafMaterial()
        {
            var mat = Materials["Ochre"];
            var tex = new Texture2D(32,32,TextureFormat.RGBA32,false);
            for (int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float u=(x-15.5f)/15.5f,v=(y-15.5f)/15.5f;
                bool leaf = u*u + v*v < .7f && Mathf.Abs(u) < .75f*(1-Mathf.Abs(v)*.4f);
                tex.SetPixel(x,y,new Color(1,.92f,.77f,leaf ? 1 : 0));
            }
            tex.Apply(); File.WriteAllBytes(Art + "/TEX_Leaf.png",tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Art + "/TEX_Leaf.png");
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/TEX_Leaf.png"));
            mat.SetFloat("_AlphaClip",1); mat.SetFloat("_Cutoff",.5f); mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull",0); mat.renderQueue=2450;
        }

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            string material, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Materials[material];
            string key="Cube_"+size.x.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"_"+
                size.y.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"_"+
                size.z.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
            if(!cubeMeshes.TryGetValue(key,out var mapped))
            {
                mapped=UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
                var uv=new Vector2[mapped.vertexCount];
                for(int i=0;i<uv.Length;i++)
                {
                    var p=Vector3.Scale(mapped.vertices[i]+Vector3.one*.5f,size);
                    var n=mapped.normals[i];
                    uv[i]=Mathf.Abs(n.x)>.5f?new Vector2(p.z,p.y)/2:
                        Mathf.Abs(n.y)>.5f?new Vector2(p.x,p.z)/2:new Vector2(p.x,p.y)/2;
                }
                mapped.uv=uv;mapped=Store(mapped,key);cubeMeshes[key]=mapped;
            }
            go.GetComponent<MeshFilter>().sharedMesh=mapped;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static GameObject Root(string id) => new GameObject(id);
        private static void Save(string id, GameObject root)
        {
            Prefabs[id] = PrefabUtility.SaveAsPrefabAsset(root, Art + "/" + id + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }
        public static GameObject Place(string id, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[id]);
            go.transform.SetParent(parent,false); go.transform.SetPositionAndRotation(position,rotation); return go;
        }
        private static GameObject GarageBay()
        {
            var r=Root("Garage bay 4 m");
            Box(r.transform,"Left pier",new Vector3(.2f,1.7f,-.2f),new Vector3(.4f,3.4f,.4f),"Brick");
            Box(r.transform,"Right pier",new Vector3(3.8f,1.7f,-.2f),new Vector3(.4f,3.4f,.4f),"Brick");
            Box(r.transform,"Lintel",new Vector3(2,3.7f,-.2f),new Vector3(4,.6f,.4f),"Concrete");
            return r;
        }
        private static GameObject WallBay()
        {
            var r=Root("Wall bay 4 m");
            Box(r.transform,"Brick plinth",new Vector3(2,.8f,-.15f),new Vector3(4,1.6f,.3f),"Brick");
            Box(r.transform,"Metal upper",new Vector3(2,2.8f,-.15f),new Vector3(4,2.4f,.3f),"Door");
            return r;
        }
        private static GameObject Workshop()
        {
            var r=Root("Original 16 x 12 workshop");
            for(int i=0;i<4;i++)
            {
                var bay=GarageBay(); bay.transform.SetParent(r.transform,false); bay.transform.localPosition=new Vector3(-8+4*i,0,0);
                if(i<3) Box(r.transform,"Separate shutter",new Vector3(-6+4*i,1.6f,-.1f),new Vector3(3.2f,3.2f,.12f),"Door");
                Box(r.transform,"Upper siding",new Vector3(-6+4*i,4.8f,-.2f),new Vector3(4,1.6f,.4f),"Door");
                Box(r.transform,"Wall lamp",new Vector3(-6+4*i,3.85f,.15f),new Vector3(.55f,.22f,.35f),"Metal",false);
                Box(r.transform,"Lamp lens",new Vector3(-6+4*i,3.72f,.2f),new Vector3(.36f,.05f,.25f),"Lamp",false);
            }
            Box(r.transform,"Back brick",new Vector3(0,1,-11.85f),new Vector3(16,2,.3f),"Brick");
            Box(r.transform,"Back siding",new Vector3(0,3.85f,-11.85f),new Vector3(16,3.7f,.3f),"Door");
            foreach(int side in new[]{-1,1})
            {
                Box(r.transform,"Side brick",new Vector3(side*7.85f,1,-6),new Vector3(.3f,2,12),"Brick");
                Box(r.transform,"Side siding",new Vector3(side*7.85f,3.85f,-6),new Vector3(.3f,3.7f,12),"Door");
            }
            var roof=Box(r.transform,"Shallow shed roof",new Vector3(0,5.82f,-6),new Vector3(16.5f,.3f,12.5f),"Metal");
            roof.transform.localRotation=Quaternion.Euler(-2,0,0);
            Box(r.transform,"Workshop floor",new Vector3(0,-.12f,-6),new Vector3(16,.24f,12),"Concrete");
            Box(r.transform,"Roof vent",new Vector3(-4,6.25f,-7),new Vector3(1.5f,.7f,1.1f),"Metal",false);
            Box(r.transform,"Service door",new Vector3(7.99f,1.1f,-8),new Vector3(.08f,2.2f,1),"Metal");
            return r;
        }
        private static GameObject Warehouse()
        {
            var r=Root("Warehouse");
            Box(r.transform,"Brick shell",new Vector3(0,3.6f,-8),new Vector3(24,7.2f,16),"Brick");
            Box(r.transform,"Parapet",new Vector3(0,7.35f,-8),new Vector3(24.4f,.3f,16.4f),"Concrete");
            for(int i=0;i<6;i++) Box(r.transform,"Window bay",new Vector3(-10+4*i,4.6f,.02f),new Vector3(3,1.6f,.1f),"Glass",false);
            Box(r.transform,"Loading door",new Vector3(-6,1.8f,.05f),new Vector3(3.4f,3.6f,.1f),"Door",false);
            return r;
        }
        private static GameObject Rowhouse()
        {
            var r=Root("Row shops");
            Box(r.transform,"Three storey mass",new Vector3(0,6,-5),new Vector3(12,12,10),"Brick");
            Box(r.transform,"Parapet",new Vector3(0,12.3f,-5),new Vector3(12.3f,.6f,10.3f),"Concrete");
            for(int row=0;row<3;row++) for(int col=0;col<3;col++)
                Box(r.transform,row==0?"Storefront":"Window",new Vector3(-4+col*4,2+row*4,.08f),
                    new Vector3(row==0?3.2f:2.3f,row==0?2.5f:2,.12f),"Glass",false);
            return r;
        }
        private static GameObject Barrier()
        {
            var r=Root("Jersey barrier");
            Box(r.transform,"Foot",new Vector3(0,.18f,0),new Vector3(3,.36f,.6f),"Concrete");
            Box(r.transform,"Upper",new Vector3(0,.6f,0),new Vector3(3,.6f,.3f),"Concrete");
            return r;
        }
        private static GameObject Guardrail()
        {
            var r=Root("Guardrail");
            foreach(float x in new[]{-1.5f,1.5f}) Box(r.transform,"Post",new Vector3(x,.4f,0),new Vector3(.14f,.8f,.14f),"Metal");
            Box(r.transform,"Rail",new Vector3(0,.68f,0),new Vector3(4,.28f,.13f),"Metal");
            return r;
        }
        private static GameObject Fence()
        {
            var r=Root("Fence");
            foreach(float x in new[]{-2f,2f}) Box(r.transform,"Post",new Vector3(x,1.2f,0),new Vector3(.1f,2.4f,.1f),"Metal");
            Box(r.transform,"Top rail",new Vector3(0,2.3f,0),new Vector3(4,.08f,.08f),"Metal");
            // A coarse diamond lattice is enough for the greybox; no imported alpha atlas.
            for(int i=0;i<15;i++) foreach(int sign in new[]{-1,1})
            {
                var wire=Box(r.transform,"Mesh lattice",new Vector3(-1.8f+i*.26f,1.2f,0),
                    new Vector3(.018f,2.5f,.018f),"Metal",false);
                wire.transform.localRotation=Quaternion.Euler(0,0,sign*12);
            }
            Box(r.transform,"Collision panel",new Vector3(0,1.2f,0),new Vector3(4,2.4f,.08f),"Metal").GetComponent<Renderer>().enabled=false;
            return r;
        }
        private static GameObject Lamp()
        {
            var r=Root("Street light");
            Box(r.transform,"Concrete foot",new Vector3(0,.22f,0),new Vector3(.42f,.44f,.42f),"Concrete");
            Box(r.transform,"Pole",new Vector3(0,3.6f,0),new Vector3(.16f,7,.16f),"Metal");
            Box(r.transform,"Cantilever",new Vector3(1,6.8f,0),new Vector3(2.2f,.12f,.12f),"Metal",false);
            Box(r.transform,"Fixture",new Vector3(2,6.72f,0),new Vector3(.75f,.18f,.38f),"Metal",false);
            Box(r.transform,"Lens",new Vector3(2,6.62f,0),new Vector3(.6f,.04f,.28f),"Lamp",false);
            return r;
        }
        private static GameObject Container()
        {
            var r=Root("Container");
            Box(r.transform,"Shell",new Vector3(0,1.295f,0),new Vector3(6.1f,2.59f,2.44f),"Door");
            for(int i=0;i<20;i++) Box(r.transform,"Rib",new Vector3(-2.9f+i*.3f,1.3f,1.24f),new Vector3(.04f,2.4f,.04f),"Metal",false);
            return r;
        }
        private static GameObject Crate()
        {
            var r=Root("Crate"); Box(r.transform,"Timber",new Vector3(0,.6f,0),Vector3.one*1.2f,"Wood");
            foreach(float z in new[]{-.61f,.61f}) foreach(float y in new[]{.12f,1.08f})
                Box(r.transform,"Frame",new Vector3(0,y,z),new Vector3(1.2f,.14f,.1f),"Wood",false);
            return r;
        }
        private static GameObject Rock()
        {
            var r=Root("Boulder"); var g=GameObject.CreatePrimitive(PrimitiveType.Sphere); g.transform.SetParent(r.transform,false);
            g.transform.localPosition=Vector3.up*.4f; g.transform.localScale=new Vector3(1.5f,.8f,1.2f);
            g.GetComponent<Renderer>().sharedMaterial=Materials["Concrete"]; return r;
        }
        private static GameObject Tree()
        {
            var r=Root("Autumn broad tree");
            Box(r.transform,"Trunk",new Vector3(0,2.5f,0),new Vector3(.4f,5,.38f),"Wood",false);
            var random=new System.Random(431); var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<650;i++)
            {
                float x=(float)(random.NextDouble()*2-1),y=(float)(random.NextDouble()*2-1),z=(float)(random.NextDouble()*2-1);
                if(x*x+y*y+z*z>1) { i--; continue; }
                var p=new Vector3(x*3.5f,6+y*2.9f,z*3.1f);
                var rotation=Quaternion.Euler((float)random.NextDouble()*180,(float)random.NextDouble()*360,0);
                float size=.22f+(float)random.NextDouble()*.2f;
                var right=rotation*Vector3.right*size;var up=rotation*Vector3.up*size;
                int k=vertices.Count;vertices.AddRange(new[]{p-right-up,p+right-up,p-right+up,p+right+up});
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});
                triangles.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var crown=new GameObject("Broken autumn crown");crown.transform.SetParent(r.transform,false);
            crown.AddComponent<MeshFilter>().sharedMesh=Store(mesh,"TreeCrown");
            crown.AddComponent<MeshRenderer>().sharedMaterial=Materials["Ochre"];return r;
        }
    }
}
