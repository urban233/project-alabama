using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Alabama.Driving;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alabama.Districts
{
    /// <summary>One persistent scene owns gameplay; only content scenes are additive.</summary>
    [DefaultExecutionOrder(-10), DisallowMultipleComponent]
    public sealed class DistrictRuntime : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController player;
        [SerializeField] private ChaseCamera chase;
        [SerializeField] private string[] initialDistrictScenes = Array.Empty<string>();
        [SerializeField] private bool requireDesignatedRoads;
        private readonly Dictionary<string, DistrictContent> districts = new Dictionary<string, DistrictContent>();
        private readonly HashSet<int> visibleGroundScenes = new HashSet<int>();
        private DistrictContent recoveryOwner;
        private float nextRecoveryUpdate;
        private bool busy;
        private int visualPreparations;
        private DistrictBoundaryGuard boundary;
        public static DistrictRuntime Instance { get; private set; }
        public ArcadeCarController Player => player;
        public ChaseCamera Chase => chase;
        public bool Ready { get; private set; }
        public bool Busy => busy || visualPreparations != 0;
        public string LastFailure { get; private set; }
        public int DistrictCount => districts.Count;
        public float MinimumRecoveryHeight => recoveryOwner != null ? recoveryOwner.MinimumRecoveryHeight : -80;
        public IReadOnlyCollection<DistrictContent> LoadedDistricts => districts.Values.ToArray();
        public event Action<DistrictContent> DistrictRegistered;
        public event Action<DistrictContent> DistrictUnregistered;
        internal IEnumerable<DrivableRoadMap> RoadMaps => FindObjectsByType<DrivableRoadMap>(FindObjectsSortMode.None)
            .Where(map => map.Available && OwnsCollision(map.gameObject.scene));
        public bool HasDesignatedRoads => RoadMaps.Any();
        internal bool RoadResetRequired => requireDesignatedRoads || HasDesignatedRoads;
        public void ConfigureRoadReset(bool required) => requireDesignatedRoads = required;
        public double InitialContentSeconds { get; private set; }
        public double InitialSceneSeconds { get; private set; }
        public double InitialVisualsSeconds { get; private set; }

        public void Configure(ArcadeCarController car, ChaseCamera camera, params string[] initialScenes)
        { player = car; chase = camera; initialDistrictScenes = initialScenes; }

        private void Awake()
        {
            if (Instance != null && Instance != this) throw new InvalidOperationException("Only one district runtime is allowed.");
            if (player == null || chase == null || player.gameObject.scene != gameObject.scene || chase.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("The runtime scene must own its player and chase camera.");
            Instance = this;
            boundary = player.GetComponent<DistrictBoundaryGuard>();
            if (boundary == null) boundary = player.gameObject.AddComponent<DistrictBoundaryGuard>();
            boundary.Configure(this);
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private IEnumerator Start()
        {
            SceneManager.SetActiveScene(gameObject.scene);
            player.Body.isKinematic = true;
            double began = Time.realtimeSinceStartupAsDouble;
            foreach (string path in initialDistrictScenes)
            {
                yield return LoadDistrict(path);
                if (LastFailure != null) { Debug.LogError(LastFailure); yield break; }
            }
            InitialSceneSeconds = Time.realtimeSinceStartupAsDouble-began;
            if (districts.Count != 0 && Recover())
            {
                Ready = false; player.Body.isKinematic = true;
                double visualsBegan = Time.realtimeSinceStartupAsDouble;
                yield return PrepareVisuals(player.Body.position);
                InitialVisualsSeconds = Time.realtimeSinceStartupAsDouble-visualsBegan;
                if (LastFailure != null) yield break;
                player.Body.isKinematic = false; Ready = true;
                InitialContentSeconds = Time.realtimeSinceStartupAsDouble-began;
            }
        }

        public IEnumerator PrepareVisuals(Vector3 position)
        {
            visualPreparations++;
            try
            {
                foreach (var content in districts.Values.ToArray())
                    foreach (var streamer in content.gameObject.scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<DistrictVisualStreamer>()))
                    {
                        yield return streamer.Prepare(position);
                        if (streamer.FailedCount != 0) LastFailure = "A required scenery cell failed to load.";
                    }
            }
            finally { visualPreparations--; }
        }

        public void Register(DistrictContent content)
        {
            if (districts.TryGetValue(content.Id ?? "", out var existing))
            { if (existing != content) LastFailure = "Duplicate district identity: " + content.Id; return; }
            var failure = content.ValidateContract();
            if (failure != null) { LastFailure = failure; return; }
            if (!content.RecoveryPoses.Any(content.Supports))
            { LastFailure = "District has no supported recovery pose: " + content.Id; return; }
            districts.Add(content.Id, content);
            var coverage = content.gameObject.scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<DistrictGroundCoverage>()).ToArray();
            if (coverage.Length != 0)
            {
                visibleGroundScenes.Add(content.gameObject.scene.handle);
                var vehicle = player.GetComponentsInChildren<Collider>();
                foreach (var ground in coverage) ground.IgnoreVehicle(vehicle);
            }
            DistrictRegistered?.Invoke(content);
            if (recoveryOwner == null) SetRecovery(content);
        }

        public void Unregister(DistrictContent content)
        {
            if (!districts.TryGetValue(content.Id ?? "", out var current) || current != content) return;
            districts.Remove(content.Id);
            visibleGroundScenes.Remove(content.gameObject.scene.handle);
            foreach (var culling in content.Culling) culling.ShowAll();
            if (recoveryOwner == content)
            {
                recoveryOwner = null;
                foreach (var retained in districts.Values) if (SetRecovery(retained)) break;
            }
            if (recoveryOwner == null && player != null && player.Body != null)
            { Ready = false; player.Body.isKinematic = true; }
            DistrictUnregistered?.Invoke(content);
        }

        // Failures are inspectable without destroying the runtime or resetting input.
        // Explicit operations are serialized; repeated loading the same path is a no-op.
        public IEnumerator LoadDistrict(string scenePath)
        {
            LastFailure = null;
            if (Busy) { LastFailure = "Another district operation is in progress."; yield break; }
            if (string.IsNullOrWhiteSpace(scenePath) || scenePath == gameObject.scene.path)
            { LastFailure = "Load requires a content scene path."; yield break; }
            if (SceneManager.GetSceneByPath(scenePath).isLoaded)
            {
                if (!districts.Values.Any(d => d.gameObject.scene.path == scenePath))
                    LastFailure = "Scene is loaded without a valid district registration: " + scenePath;
                yield break;
            }
            if (!CanLoad(scenePath))
            { LastFailure = "Content scene is absent from the player build: " + scenePath; yield break; }
            busy = true;
            try
            {
                yield return BeginLoad(scenePath);
                var scene = SceneManager.GetSceneByPath(scenePath);
                if (!districts.Values.Any(d => d.gameObject.scene == scene))
                {
                    LastFailure = LastFailure ?? "Loaded scene does not contain exactly one valid district: " + scenePath;
                    if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            finally { busy = false; }
        }

        public IEnumerator UnloadDistrict(string id, string recoverIntoDistrict = null)
        {
            LastFailure = null;
            if (Busy) { LastFailure = "Another district operation is in progress."; yield break; }
            if (!districts.TryGetValue(id, out var content)) { LastFailure = "District is not loaded: " + id; yield break; }
            if (districts.Count <= 1) { LastFailure = "The last supporting district must remain loaded."; yield break; }
            // An airborne car can still depend on the road beneath it. Use the
            // horizontal footprint as well as current wheel contacts.
            var projected = player.Body.position; projected.y = content.Bounds.center.y;
            bool near = content.Bounds.Contains(projected) || player.GetComponentsInChildren<WheelCollider>()
                .Any(w => w.GetGroundHit(out var h) && h.collider.gameObject.scene == content.gameObject.scene);
            DistrictContent destination = null;
            if (recoverIntoDistrict != null && (!districts.TryGetValue(recoverIntoDistrict, out destination) || destination == content))
            { LastFailure = "Recovery requires a retained loaded district."; yield break; }
            if (near && destination == null)
            { LastFailure = "Supporting content cannot unload without an explicit retained recovery destination."; yield break; }
            if (destination != null && !SetRecovery(destination))
            { LastFailure = "Retained district has no safe recovery support."; yield break; }
            if (recoveryOwner == content && destination == null)
            {
                destination = districts.Values.FirstOrDefault(d => d != content && d.RecoveryPoses.Any(d.Supports));
                if (destination == null || !SetRecovery(destination))
                { LastFailure = "No supported reset destination remains."; yield break; }
            }
            busy = true;
            try
            {
                if (near)
                {
                    // Destination collision already exists. Move while physics is
                    // held, then remove source support; preserve pause/input state.
                    player.Body.linearVelocity = Vector3.zero;
                    player.Body.angularVelocity = Vector3.zero;
                    player.Body.isKinematic = true;
                    player.ResetToSpawn(); chase.SnapToTarget();
                    boundary.ResetHistory();
                    player.Body.isKinematic = false;
                }
                foreach (var streamer in content.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DistrictVisualStreamer>())) yield return streamer.Release();
                Unregister(content);
                yield return SceneManager.UnloadSceneAsync(content.gameObject.scene);
            }
            finally { busy = false; }
        }

        public bool Recover()
        {
            if (recoveryOwner == null || !SetRecovery(recoveryOwner))
            {
                recoveryOwner = null;
                foreach (var content in districts.Values) if (SetRecovery(content)) break;
            }
            if (recoveryOwner == null) { Ready = false; player.Body.isKinematic = true; return false; }
            player.Body.isKinematic = false; player.ResetToSpawn(); chase.SnapToTarget();
            boundary.ResetHistory();
            if (RoadResetRequired && !boundary.TryRestoreDesignatedRoad())
            { Ready = false; player.Body.isKinematic = true; LastFailure = "No validated drivable road is available for recovery."; return false; }
            chase.SnapToTarget();
            Ready = true; return true;
        }

        public bool RecoverNearby()
        {
            if (RoadResetRequired)
            {
                if (Busy || !boundary.TryRestoreDesignatedRoad()) return false;
                chase.SnapToTarget(); return true;
            }
            if (Ready && !busy && boundary.TryRestoreNearby())
            { chase.SnapToTarget(); return true; }
            return Recover();
        }

        public bool ResetToNearestRoad()
        {
            if (!Ready || Busy || !RecoverNearby()) return false;
            if (FindObjectsByType<DistrictVisualStreamer>(FindObjectsSortMode.None)
                .Any(s => OwnsCollision(s.gameObject.scene) && s.MissingVisibleCells(chase.transform.position) != 0))
                StartCoroutine(PrepareResetVisuals());
            return true;
        }
        private IEnumerator PrepareResetVisuals()
        {
            Ready = false; player.Body.isKinematic = true;
            yield return PrepareVisuals(player.Body.position);
            if (LastFailure != null) yield break;
            player.Body.isKinematic = false; Ready = true; chase.SnapToTarget();
        }
        public bool IsOnDesignatedRoad(Vector3 position)
            => RoadMaps.Any(map => map.Sample(position-Vector3.up*.24f,.7f,out _));

        private bool SetRecovery(DistrictContent content)
        {
            if (player == null) return false;
            foreach (var pose in content.RecoveryPoses)
                if (content.Supports(pose))
                { recoveryOwner = content; player.SetRecoveryPose(pose.position, pose.Rotation); return true; }
            return false;
        }

        internal bool OwnsCollision(Scene scene)
        {
            foreach (var content in districts.Values)
                if (content != null && content.gameObject.scene == scene) return true;
            return false;
        }

        internal bool RequiresVisibleGround(Scene scene) => visibleGroundScenes.Contains(scene.handle);

        private void Update()
        {
            if (!Ready || busy || Time.unscaledTime < nextRecoveryUpdate) return;
            nextRecoveryUpdate = Time.unscaledTime + .2f;
            var wheel = player.GetComponentsInChildren<WheelCollider>().FirstOrDefault(w => w.GetGroundHit(out _));
            if (wheel == null || !wheel.GetGroundHit(out var hit)) return;
            var owner = districts.Values.FirstOrDefault(d => d.gameObject.scene == hit.collider.gameObject.scene);
            if (owner != null && recoveryOwner != owner) SetRecovery(owner);
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene) return;
            var contents = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>(true)).ToArray();
            if (contents.Length == 1) Register(contents[0]);
        }
        private static bool CanLoad(string path)
        {
#if UNITY_EDITOR
            if (Application.isEditor && System.IO.File.Exists(path)) return true;
#endif
            return Application.CanStreamedLevelBeLoaded(path);
        }
        private static IEnumerator BeginLoad(string path)
        {
#if UNITY_EDITOR
            if (Application.isEditor && !Application.CanStreamedLevelBeLoaded(path))
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                yield break;
            }
#endif
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
        }
        private void SceneUnloaded(Scene scene)
        {
            foreach (var content in districts.Values.Where(d => d == null || d.gameObject.scene == scene).ToArray())
                if (content != null) Unregister(content);
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneUnloaded -= SceneUnloaded;
            districts.Clear(); visibleGroundScenes.Clear(); if (Instance == this) Instance = null;
        }
    }
}
