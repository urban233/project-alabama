using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alabama.Districts;
using Alabama.Driving;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Alabama.Editor
{
    public static class NfsWorldCollisionReview
    {
        public static void Prepare()
        {
            NfsWorldArtDirection.VerifyRuntime();
            var content = SceneManager.GetSceneByPath(NfsWorldArtDirection.ContentScene);
            var roots = content.GetRootGameObjects();
            var scenery = roots.Single(r => r.name == NfsWorldSceneryCollision.RootName);
            var guardObject = new GameObject("Temporary boundary query");
            var guard = guardObject.AddComponent<DistrictBoundaryGuard>();
            var routes = new List<NfsWorldCollisionProbe.Route>();
            bool Contains(Vector3 p, Vector3 d) => guard.ContainsFootprint(p, Quaternion.LookRotation(d), scene => scene == content, true, .15f);
            var source = EditorSceneManager.OpenScene(NfsWorldSetup.DistrictScene, OpenSceneMode.Additive);
            try
            {
                Physics.SyncTransforms();
                var contract = NfsWorldSetup.ReadContract("District");
                var definitions = contract.parts.SelectMany(p => p.meshes.Select(m => (category: p.category, mesh: m)))
                    .ToDictionary(p => p.mesh.name);
                var filters = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>())
                    .Where(f => definitions.ContainsKey(f.name)).OrderBy(f => f.transform.TransformPoint(f.sharedMesh.bounds.center).sqrMagnitude).ToArray();
                foreach (string family in new[] { "sign", "building", "tree", "prop" })
                {
                    int count = 0;
                    foreach (var filter in filters)
                    {
                        var definition = definitions[filter.name]; var name = definition.mesh.sourceName;
                        if (!definition.mesh.visible) continue;
                        bool sign = Regex.IsMatch(name, "SIGN|STREETLIGHT|PARKLAMP", RegexOptions.IgnoreCase);
                        if (family == "sign" && !sign || family == "building" && (definition.category != "Buildings" || sign || name.StartsWith("VISROAD")) ||
                            family == "tree" && definition.category != "Trees" || family == "prop" && definition.category != "Props") continue;
                        var mesh = filter.sharedMesh; var vertices = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                        var indices = mesh.triangles;
                        for (int i = 0; i < indices.Length && count < 3; i += 3)
                        {
                            var a = vertices[indices[i]]; var b = vertices[indices[i+1]]; var c = vertices[indices[i+2]];
                            var normal = Vector3.Cross(b-a, c-a).normalized;
                            if (Mathf.Abs(normal.y) > .3f || normal.sqrMagnitude < .5f) continue;
                            var centre = (a+b+c)/3;
                            if (!TryGround(centre, content, out var ground)) continue;
                            float height = ground.y + .8f;
                            var slice = new List<Vector3>();
                            void Edge(Vector3 first, Vector3 second)
                            {
                                if (Mathf.Abs(second.y-first.y) < .001f || (first.y-height)*(second.y-height) > 0) return;
                                slice.Add(Vector3.Lerp(first, second, (height-first.y)/(second.y-first.y)));
                            }
                            Edge(a,b); Edge(b,c); Edge(c,a);
                            if (slice.Count < 2) continue;
                            var point = (slice[0]+slice[1])*.5f;
                            if (routes.Any(r => Vector3.Distance(r.position, point) < 15)) continue;
                            foreach (float side in new[] { 1f, -1f })
                            {
                                var direction = new Vector3(normal.x, 0, normal.z).normalized * -side;
                                var start = point - direction*8;
                                if (!TryGround(start, content, out var startGround) || Mathf.Abs(startGround.y-ground.y) > .5f) continue;
                                start.y = startGround.y+.24f;
                                if (!Contains(start, direction)) continue;
                                if (Physics.OverlapBox(start+Vector3.up*.65f, new Vector3(.9f,.45f,2.1f), Quaternion.LookRotation(direction), ~0,
                                    QueryTriggerInteraction.Ignore).Any(h => h.transform.root == scenery.transform && h.GetComponentInParent<DistrictGroundCoverage>() == null)) continue;
                                var hits = Physics.BoxCastAll(start+Vector3.up*.65f, new Vector3(.9f,.45f,2.1f), direction,
                                    Quaternion.LookRotation(direction), 12, ~0, QueryTriggerInteraction.Ignore)
                                    .Where(h => h.collider.transform.root == scenery.transform && h.collider.GetComponentInParent<DistrictGroundCoverage>() == null)
                                    .OrderBy(h => h.distance).ToArray();
                                if (hits.Length == 0 || hits[0].distance < 3 || hits[0].distance > 8 || Vector3.Distance(hits[0].point, point) > 3) continue;
                                if (!hits[0].collider.name.StartsWith(definition.category + "_")) continue;
                                routes.Add(new NfsWorldCollisionProbe.Route { label = family+"-"+(++count), position = start,
                                    direction = direction, maximumProgress = hits[0].distance+2.5f,
                                    reverse = family == "sign" && side < 0 });
                                if (family == "sign" && count == 1) continue;
                                break;
                            }
                        }
                        if (count == 3) break;
                    }
                    NfsWorldSetup.Require(count == 3, "Incomplete native impact routes for " + family);
                }
                int edges = 0;
                foreach (var filter in filters.Where(f => definitions[f.name].category == "Roads")
                    .OrderByDescending(f => f.transform.TransformPoint(f.sharedMesh.bounds.center).sqrMagnitude))
                {
                    var mesh = filter.sharedMesh; var vertices = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                    var indices = mesh.triangles;
                    for (int i = 0; i < indices.Length && edges < 6; i += 30)
                    {
                        var centre = (vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3;
                        if (!TryGround(centre, content, out var ground)) continue;
                        centre.y = ground.y+.24f;
                        for (int angle = 0; angle < 360 && edges < 6; angle += 45)
                        {
                            var direction = Quaternion.Euler(0,angle,0)*Vector3.forward;
                            if (!Contains(centre, direction)) continue;
                            float levelY = centre.y;
                            for (int distance = 1; distance <= 80; distance++)
                            {
                                var point = centre+direction*distance;
                                point.y = levelY;
                                if (TryGround(point, content, out var level))
                                { levelY = level.y+.24f; continue; }
                                // An isolated kerb/deck-height mismatch is not an exterior map edge.
                                if (Physics.RaycastAll(new Vector3(point.x,500,point.z), Vector3.down,1000,~0,QueryTriggerInteraction.Ignore)
                                    .Any(h => h.collider.gameObject.scene == content && h.normal.y>.5f &&
                                        h.collider.GetComponentInParent<DistrictGroundCoverage>() != null)) break;
                                var start = centre+direction*Mathf.Max(0,distance-7);
                                start.y = levelY;
                                if (!TryGround(start,content,out var startGround)) break;
                                start.y = startGround.y+.24f;
                                if (!Contains(start,direction) || routes.Any(r => Vector3.Distance(start,r.position)<25)) break;
                                if (Physics.BoxCastAll(start+Vector3.up*.65f, new Vector3(.9f,.45f,2.1f), direction,
                                    Quaternion.LookRotation(direction), 12, ~0, QueryTriggerInteraction.Ignore)
                                    .Any(h => h.collider.gameObject.scene == content && h.collider.GetComponentInParent<DistrictGroundCoverage>() == null && Mathf.Abs(h.normal.y)<.5f)) break;
                                routes.Add(new NfsWorldCollisionProbe.Route { label = "edge-"+(++edges), position = start,
                                    direction = direction, maximumProgress = 9, boundary = true });
                                break;
                            }
                        }
                    }
                    if (edges == 6) break;
                }
                NfsWorldSetup.Require(edges > 0, "No exposed road edge route found.");
                var signRoute = routes.First(r => r.label.StartsWith("sign-"));
                routes.Add(new NfsWorldCollisionProbe.Route { label = "sign-reverse", position = signRoute.position,
                    direction = signRoute.direction, maximumProgress = signRoute.maximumProgress, reverse = true });
                var edgeRoute = routes.First(r => r.boundary);
                routes.Add(new NfsWorldCollisionProbe.Route { label = "edge-reverse", position = edgeRoute.position,
                    direction = edgeRoute.direction, maximumProgress = edgeRoute.maximumProgress, boundary = true, reverse = true });
            }
            finally { Object.DestroyImmediate(guardObject); EditorSceneManager.CloseScene(source,true); }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/NfsWorld/Stability/collision-review-plan.json"));
            File.WriteAllText(output, JsonUtility.ToJson(new NfsWorldCollisionProbe.Plan { routes = routes.ToArray() }, true));
            Debug.Log("Prepared native collision routes: " + routes.Count);
        }

        private static bool TryGround(Vector3 point, Scene content, out Vector3 ground)
        {
            var hits = Physics.RaycastAll(point+Vector3.up*25, Vector3.down, 80, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == content && h.collider.GetComponentInParent<DistrictGroundCoverage>() != null &&
                    h.normal.y>.5f && h.point.y<=point.y+2).OrderByDescending(h=>h.point.y).ToArray();
            ground = hits.Length == 0 ? Vector3.zero : hits[0].point;
            return hits.Length != 0;
        }
    }
}
