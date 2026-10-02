using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Conservative visual-only distance limits from the source map configuration.</summary>
    public sealed class NfsWorldDistanceCulling : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private float[] distances;
        private float nextUpdate;

        public void Configure(Renderer[] targets, float[] limits)
        {
            renderers = targets;
            distances = limits;
        }

        public void AddTargets(Renderer[] targets, float limit)
        {
            var retained = new System.Collections.Generic.List<Renderer>();
            var limits = new System.Collections.Generic.List<float>();
            if (renderers != null)
                for (int index = 0; index < renderers.Length; index++)
                    if (renderers[index] != null && System.Array.IndexOf(targets, renderers[index]) < 0)
                    {
                        retained.Add(renderers[index]);
                        limits.Add(distances[index]);
                    }
            foreach (var target in targets)
                if (target != null && !retained.Contains(target))
                {
                    retained.Add(target);
                    limits.Add(limit);
                }
            renderers = retained.ToArray();
            distances = limits.ToArray();
        }

        public float DistanceFor(Renderer renderer)
        {
            int index = System.Array.IndexOf(renderers, renderer);
            return index < 0 ? 90000 : distances[index];
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
            var position = Camera.main.transform.position;
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].forceRenderingOff =
                        renderers[index].bounds.SqrDistance(position) > distances[index] * distances[index];
        }

        private void OnDisable()
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.forceRenderingOff = false;
        }
    }
}
