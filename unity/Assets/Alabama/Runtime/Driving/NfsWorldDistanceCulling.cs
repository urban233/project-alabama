using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Conservative visual-only distance limits from the source map configuration.</summary>
    public sealed class NfsWorldDistanceCulling : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private float[] distances;
        private float nextUpdate;
        private Bounds[] cachedBounds;
        private bool[] hidden;
        private System.Collections.Generic.Dictionary<Renderer, float> distanceLookup;

        public int TargetCount => renderers == null ? 0 : System.Array.FindAll(renderers, r => r != null).Length;

        public bool TargetsBelongTo(UnityEngine.SceneManagement.Scene scene)
        {
            if (renderers == null || distances == null || renderers.Length != distances.Length) return false;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null || renderers[i].gameObject.scene != scene ||
                    !float.IsFinite(distances[i]) || distances[i] <= 0) return false;
            return true;
        }

        public void Configure(Renderer[] targets, float[] limits)
        {
            ShowAll();
            renderers = targets;
            distances = limits;
            cachedBounds = null;
            distanceLookup = null;
        }

        public void AddTargets(Renderer[] targets, float limit)
        {
            ShowAll();
            var retained = new System.Collections.Generic.List<Renderer>();
            var limits = new System.Collections.Generic.List<float>();
            var incoming = new System.Collections.Generic.HashSet<Renderer>(targets);
            var unique = new System.Collections.Generic.HashSet<Renderer>();
            if (renderers != null)
                for (int index = 0; index < renderers.Length; index++)
                    if (renderers[index] != null && !incoming.Contains(renderers[index]) && unique.Add(renderers[index]))
                    {
                        retained.Add(renderers[index]);
                        limits.Add(distances[index]);
                    }
            foreach (var target in targets)
                if (target != null && unique.Add(target))
                {
                    retained.Add(target);
                    limits.Add(limit);
                }
            renderers = retained.ToArray();
            distances = limits.ToArray();
            cachedBounds = null;
            distanceLookup = null;
        }

        public float DistanceFor(Renderer renderer)
        {
            if (renderer == null) return 90000;
            if (distanceLookup == null)
            {
                distanceLookup = new System.Collections.Generic.Dictionary<Renderer, float>();
                if (renderers != null)
                    for (int index = 0; index < renderers.Length; index++)
                        if (renderers[index] != null && !distanceLookup.ContainsKey(renderers[index]))
                            distanceLookup.Add(renderers[index], distances[index]);
            }
            return distanceLookup.TryGetValue(renderer, out float limit) ? limit : 90000;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate || Camera.main == null) return;
            nextUpdate = Time.unscaledTime + .2f;
            Refresh();
        }

        public void Refresh()
        {
            if (renderers == null || Camera.main == null) return;
            // Only stationary map geometry is registered here. Cache native
            // bounds and update renderer state only when visibility changes.
            if (cachedBounds == null || cachedBounds.Length != renderers.Length)
            {
                cachedBounds = new Bounds[renderers.Length]; hidden = new bool[renderers.Length];
                for (int index = 0; index < renderers.Length; index++)
                    if (renderers[index] != null)
                    { cachedBounds[index] = renderers[index].bounds; hidden[index] = renderers[index].forceRenderingOff; }
            }
            var position = Camera.main.transform.position;
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                {
                    bool off = cachedBounds[index].SqrDistance(position) > distances[index] * distances[index];
                    if (hidden[index] == off) continue;
                    renderers[index].forceRenderingOff = off; hidden[index] = off;
                }
        }

        public void ShowAll()
        {
            cachedBounds = null;
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.forceRenderingOff = false;
        }

        private void OnDisable() => ShowAll();
    }
}
