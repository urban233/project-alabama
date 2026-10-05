using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace Alabama.Districts
{
    /// <summary>Visual scene cells only; collision and designated roads remain in the core scene.</summary>
    public sealed class DistrictVisualStreamer : MonoBehaviour
    {
        [Serializable] public struct Cell
        { public string scenePath; public Bounds bounds; public float visibleDistance; }
        [SerializeField] private Cell[] cells = Array.Empty<Cell>();
        private readonly Dictionary<int,Scene> resident = new Dictionary<int,Scene>();
        private readonly Dictionary<int,float> loadedAt = new Dictionary<int,float>();
        private readonly HashSet<int> failed = new HashSet<int>();
        private bool operating, stopping;
        private float nextUpdate, observedLoadSeconds = 1;
        private Vector3 previousPosition;
        private float previousTime;
        private static readonly ProfilerMarker Demand = new ProfilerMarker("Alabama.Streaming.Demand");
        public int ResidentCount => resident.Count;
        public int FailedCount => failed.Count;
        public int LoadCount { get; private set; }
        public float MaximumLoadSeconds { get; private set; }
        public int CellCount => cells.Length;
        public int MissingVisibleCells(Vector3 position)
        {
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
                if (!resident.ContainsKey(i) && Required(cells[i],position,position,0)) count++;
            return count;
        }
        public void Configure(Cell[] value) => cells = value;

        public static bool Required(Cell cell, Vector3 current, Vector3 predicted, float lead)
        {
            float radius = cell.visibleDistance+lead;
            return Mathf.Min(cell.bounds.SqrDistance(current),cell.bounds.SqrDistance(predicted)) <= radius*radius;
        }

        public IEnumerator Prepare(Vector3 position)
        {
            while (operating) yield return null;
            operating = true;
            try
            {
                while (!stopping)
                {
                    int next = FindNext(position,position,100);
                    if (next < 0) break;
                    yield return Load(next);
                }
            }
            finally { operating = false; }
        }

        private int FindNext(Vector3 current, Vector3 predicted, float lead)
        {
            int nearest = -1; float score = float.PositiveInfinity;
            using (Demand.Auto())
                for (int i = 0; i < cells.Length; i++)
                {
                    if (resident.ContainsKey(i) || failed.Contains(i) || !Required(cells[i],current,predicted,lead)) continue;
                    float distance = Mathf.Min(cells[i].bounds.SqrDistance(current),cells[i].bounds.SqrDistance(predicted));
                    if (distance < score) { score = distance; nearest = i; }
                }
            return nearest;
        }

        private IEnumerator Load(int index)
        {
            double began = Time.realtimeSinceStartupAsDouble;
            var path = cells[index].scenePath;
            // Never take ownership of a scene loaded by someone else.
            if (SceneManager.GetSceneByPath(path).isLoaded)
            { failed.Add(index); Debug.LogError("Visual cell is already owned elsewhere: "+path); yield break; }
            if (!Application.CanStreamedLevelBeLoaded(path))
            { failed.Add(index); Debug.LogError("Visual cell is missing from the build: "+path); yield break; }
            var request = SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            if (request == null) { failed.Add(index); yield break; }
            // Unity async operations outlive the coroutine when their owner is destroyed.
            request.completed += _ =>
            {
                if (this != null) return;
                var orphan = SceneManager.GetSceneByPath(path);
                if (orphan.isLoaded) SceneManager.UnloadSceneAsync(orphan);
            };
            yield return request;
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded) { failed.Add(index); yield break; }
            // The build validates cells contain no collision or gameplay owners.
            resident[index] = scene; loadedAt[index] = Time.unscaledTime; LoadCount++;
            float elapsed = (float)(Time.realtimeSinceStartupAsDouble-began);
            MaximumLoadSeconds = Mathf.Max(MaximumLoadSeconds,elapsed);
            observedLoadSeconds = Mathf.Clamp(Mathf.Max(elapsed,observedLoadSeconds*.95f),.5f,5);
            if (stopping) { yield return SceneManager.UnloadSceneAsync(scene); resident.Remove(index); }
        }

        private void Update()
        {
            var runtime = DistrictRuntime.Instance;
            if (stopping || operating || runtime == null || !runtime.Ready || runtime.Busy ||
                Time.timeScale == 0 || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime+.1f;
            var position = runtime.Chase.transform.position;
            float dt = Time.unscaledTime-previousTime;
            var velocity = dt > .001f && dt < 1 && (position-previousPosition).sqrMagnitude < 625 ?
                Vector3.ClampMagnitude((position-previousPosition)/dt,80) : Vector3.zero;
            previousPosition = position; previousTime = Time.unscaledTime;
            float lead = Mathf.Clamp(80+velocity.magnitude*(observedLoadSeconds+.5f),100,480);
            var predicted = position+velocity*2;
            int next = FindNext(position,predicted,lead);
            if (next >= 0) { StartCoroutine(Operate(next)); return; }
            foreach (var pair in resident)
                if (Time.unscaledTime-loadedAt[pair.Key] > 10 && !Required(cells[pair.Key],position,predicted,lead+150))
                { StartCoroutine(Unload(pair.Key)); break; }
        }
        private IEnumerator Operate(int index)
        { operating = true; try { yield return Load(index); } finally { operating = false; } }
        private IEnumerator Unload(int index)
        {
            operating = true;
            try { yield return SceneManager.UnloadSceneAsync(resident[index]); resident.Remove(index); loadedAt.Remove(index); }
            finally { operating = false; }
        }
        public IEnumerator Release()
        {
            stopping = true;
            while (operating) yield return null;
            foreach (var scene in resident.Values) if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            resident.Clear(); loadedAt.Clear();
        }
        private void OnDestroy()
        {
            stopping = true;
            foreach (var scene in resident.Values) if (scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
