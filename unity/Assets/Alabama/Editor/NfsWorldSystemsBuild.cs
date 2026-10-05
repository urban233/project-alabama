using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Districts;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Alabama.Editor
{
    /// <summary>Repeatable private-scene derivation; accepted source scenes are never saved.</summary>
    public static class NfsWorldSystemsBuild
    {
        public const string Root = NfsWorldArtDirection.DirectoryPath+"/Systems";
        public const string Boot = Root+"/Startup.unity";
        public const string Host = Root+"/DistrictRuntime.unity";
        public const string Core = Root+"/DowntownCore.unity";
        [Serializable] private sealed class RoadRecipe { public string[] roadDetailSources; }
        private const int RoadArea = 3;
        private const int CellSize = 256;

        public static string[] Prepare()
        {
            Directory.CreateDirectory(Root+"/Cells"); Directory.CreateDirectory(Root+"/Resources");
            AssetDatabase.Refresh();
            // Baking costs belong to the build, not to the player's first launch.
            PlayerSettings.bakeCollisionMeshes = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            var core = EditorSceneManager.OpenScene(NfsWorldArtDirection.ContentScene);
            BakeRoads(core);
            var originalCulling = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NfsWorldDistanceCulling>()).Single();
            var originalLods = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NfsWorldMeshLods>()).Single();
            var visibilityTargets = new HashSet<Renderer>(originalCulling.Targets);
            var entries = originalLods.Entries;
            var renderers = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>()).ToArray();
            var limits = renderers.ToDictionary(r => r,r => originalCulling.DistanceFor(r));
            var groups = renderers.Where(r => r.enabled && limits[r] < 600 && r.transform.childCount == 0 &&
                r.GetComponent<Collider>() == null && r.GetComponent<MeshFilter>() != null &&
                (r.name.EndsWith("_lod",StringComparison.Ordinal) || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly))
                .GroupBy(r => new Vector2Int(Mathf.FloorToInt(r.bounds.center.x/CellSize),Mathf.FloorToInt(r.bounds.center.z/CellSize)))
                .OrderBy(g => g.Key.x).ThenBy(g => g.Key.y).ToArray();
            var cells = new List<DistrictVisualStreamer.Cell>();
            foreach (var group in groups)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                var root = new GameObject("Scenery cell "+group.Key); SceneManager.MoveGameObjectToScene(root,scene);
                var targets = group.ToArray(); var bounds = targets[0].bounds;
                foreach (var renderer in targets)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.transform.SetParent(null,true);
                    SceneManager.MoveGameObjectToScene(renderer.gameObject,scene);
                    renderer.transform.SetParent(root.transform,true);
                }
                root.AddComponent<NfsWorldDistanceCulling>().Configure(targets,targets.Select(r => limits[r]).ToArray());
                var cellEntries = entries.Where(e => e.target != null && e.target.gameObject.scene == scene).ToArray();
                if (cellEntries.Length != 0) root.AddComponent<NfsWorldMeshLods>().Configure(cellEntries);
                NfsWorldSetup.Require(root.GetComponentsInChildren<Collider>(true).Length == 0,"A visual cell cannot own collision.");
                NfsWorldSetup.Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(b =>
                    b is NfsWorldDistanceCulling || b is NfsWorldMeshLods),"A visual cell cannot own gameplay.");
                string path = Root+"/Cells/cell_"+group.Key.x+"_"+group.Key.y+".unity";
                EditorSceneManager.SaveScene(scene,path);
                cells.Add(new DistrictVisualStreamer.Cell { scenePath = path,bounds = bounds,visibleDistance = targets.Max(r => limits[r]) });
            }
            var retained = renderers.Where(r => r.gameObject.scene == core).ToArray();
            var coreVisibility = retained.Where(visibilityTargets.Contains).ToArray();
            originalCulling.Configure(coreVisibility,coreVisibility.Select(r => limits[r]).ToArray());
            originalLods.Configure(entries.Where(e => e.target != null && e.target.gameObject.scene == core).ToArray());
            var descriptor = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>()).Single();
            descriptor.gameObject.AddComponent<DistrictVisualStreamer>().Configure(cells.ToArray());
            NfsWorldSetup.Require(descriptor.ValidateContract() == null,descriptor.ValidateContract());
            NfsWorldSceneryCollision.Verify(core);
            NfsWorldSetup.Require(retained.Length+groups.Sum(g => g.Count()) == renderers.Length,"Scene partition lost a renderer.");
            SceneManager.SetActiveScene(core); EditorSceneManager.SaveScene(core,Core);
            foreach (var cell in cells) EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(cell.scenePath),true);

            var host = EditorSceneManager.OpenScene(NfsWorldArtDirection.RuntimeScene);
            var runtime = Object.FindFirstObjectByType<DistrictRuntime>();
            runtime.Configure(runtime.Player,runtime.Chase,Core);
            runtime.ConfigureRoadReset(true);
            var catalog = ScriptableObject.CreateInstance<StartupPreloadCatalog>();
            var pipeline = new SerializedObject(Object.FindFirstObjectByType<NfsWorldRenderSettings>()).FindProperty("pipeline").objectReferenceValue;
            catalog.coreAssets = runtime.Player.GetComponentsInChildren<MeshFilter>().Select(f => (Object)f.sharedMesh)
                .Concat(runtime.Player.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Cast<Object>())
                .Concat(new Object[] { runtime.Player.Tuning,pipeline }).Where(a => a != null).Distinct().ToArray();
            Store(catalog,Root+"/Resources/AlabamaCoreStartup.asset");
            EditorSceneManager.SaveScene(host,Host);
            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Asynchronous game startup").AddComponent<GameStartup>().Configure(Host,"AlabamaCoreStartup");
            EditorSceneManager.SaveScene(boot,Boot);
            AssetDatabase.SaveAssets();
            var scenes = new[] { Boot,Host,Core }.Concat(cells.Select(c => c.scenePath)).ToArray();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/Systems/generated-scenes.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,JsonUtility.ToJson(new GenerationReport
            { visualCells = cells.Count,residentRenderers = retained.Length,streamedRenderers = groups.Sum(g => g.Count()),scenes = scenes },true));
            Debug.Log($"Systems scenes: {cells.Count} visual cells, {retained.Length} resident renderers; collision remains resident.");
            return scenes;
        }
        [Serializable] private sealed class GenerationReport
        { public int visualCells,residentRenderers,streamedRenderers; public string[] scenes; }

        private static void Store(Object value,string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing == null) AssetDatabase.CreateAsset(value,path);
            else { EditorUtility.CopySerialized(value,existing); Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); }
        }

        private static void BakeRoads(Scene core)
        {
            var source = EditorSceneManager.OpenScene(NfsWorldSetup.DistrictScene,OpenSceneMode.Additive);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var directions = new List<DrivableRoadMap.Direction>();
            var recipe = JsonUtility.FromJson<RoadRecipe>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/art-direction/downtown-material-recipe.json"))));
            var approvedTextures = new HashSet<string>(recipe.roadDetailSources);
            var contract = NfsWorldSetup.ReadContract("District");
            var materials = contract.materials.ToDictionary(m => m.name);
            var definitions = contract.parts.Single(p => p.category == "Roads").meshes
                .Where(m => m.visible && m.sourceName.StartsWith("VISROAD",StringComparison.OrdinalIgnoreCase) && approvedTextures.Contains(materials[m.material].texture)).ToArray();
            var filters = source.GetRootGameObjects().Single(r => r.name == "Roads")
                .GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.name);
            try
            {
                foreach (var definition in definitions)
                {
                    var filter = filters[definition.name]; var mesh = filter.sharedMesh;
                    var points = mesh.vertices.Select(filter.transform.TransformPoint).ToArray(); var indices = mesh.triangles;
                    var centre = points.Aggregate(Vector3.zero,(sum,p) => sum+p)/points.Length;
                    double xx = 0,xz = 0,zz = 0;
                    foreach (var point in points) { var d = point-centre; xx += d.x*d.x; xz += d.x*d.z; zz += d.z*d.z; }
                    float angle = .5f*(float)Math.Atan2(2*xz,xx-zz);
                    var axis = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    float length = points.Max(p => Mathf.Abs(Vector3.Dot(p-centre,axis)));
                    if (length > 1) directions.Add(new DrivableRoadMap.Direction { start = centre-axis*length,end = centre+axis*length });
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var a = points[indices[i]]; var b = points[indices[i+1]]; var c = points[indices[i+2]];
                        var normal = Vector3.Cross(b-a,c-a);
                        if (normal.sqrMagnitude < 1e-8f || Mathf.Abs(normal.normalized.y) < .8f) continue;
                        if (normal.y < 0) { var swap = b; b = c; c = swap; }
                        int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                        triangles.Add(first); triangles.Add(first+1); triangles.Add(first+2);
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(source,true); SceneManager.SetActiveScene(core); }
            NfsWorldSetup.Require(triangles.Count > 0,"No designated asphalt road geometry found.");
            var surface = new Mesh { name = "Designated asphalt road surface",indexFormat = IndexFormat.UInt32 };
            surface.SetVertices(vertices); surface.SetTriangles(triangles,0); surface.RecalculateBounds();
            Store(surface,Root+"/RoadSurface.asset");
            surface = AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/RoadSurface.asset");
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource
            { shape = NavMeshBuildSourceShape.Mesh,sourceObject = surface,transform = Matrix4x4.identity,area = RoadArea } };
            foreach (var root in core.GetRootGameObjects())
            {
                bool obstacles = root.name == NfsWorldSceneryCollision.RootName || root.name == "Walls" || root.name == "Downtown exit closures";
                if (!obstacles) continue;
                foreach (var collider in root.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || collider.isTrigger || collider.GetComponentInParent<DistrictGroundCoverage>() != null) continue;
                    if (collider is MeshCollider mesh) sources.Add(new NavMeshBuildSource
                    { shape = NavMeshBuildSourceShape.Mesh,sourceObject = mesh.sharedMesh,transform = mesh.transform.localToWorldMatrix,area = 1 });
                    if (collider is BoxCollider box) sources.Add(new NavMeshBuildSource
                    { shape = NavMeshBuildSourceShape.Box,transform = box.transform.localToWorldMatrix*Matrix4x4.Translate(box.center),size = box.size,area = 1 });
                }
            }
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 2.45f; settings.agentHeight = 1.6f; settings.agentSlope = 35; settings.agentClimb = .25f;
            settings.overrideVoxelSize = true; settings.voxelSize = .2f;
            var bounds = surface.bounds; bounds.Expand(10);
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            NfsWorldSetup.Require(data != null,"Road NavMesh bake failed.");
            data.name = "Vehicle road reset NavMesh"; Store(data,Root+"/RoadNavMesh.asset");
            var descriptor = core.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>()).Single();
            descriptor.gameObject.AddComponent<DrivableRoadMap>().Configure(AssetDatabase.LoadAssetAtPath<NavMeshData>(Root+"/RoadNavMesh.asset"),RoadArea,directions.ToArray(),settings.agentTypeID);
            Debug.Log($"Road reset bake: {definitions.Length} approved road regions, {triangles.Count/3} triangles, radius {settings.agentRadius}m.");
        }
    }
}
