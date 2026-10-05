using System;
using System.Collections.Generic;
using System.Linq;
using Alabama.Districts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alabama.Driving
{
    [DisallowMultipleComponent]
    public sealed class AutomaticPerformance : MonoBehaviour
    {
        private UniversalRenderPipelineAsset original, instance;
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly List<float> frames = new List<float>(256), work = new List<float>(256);
        private AutomaticPerformancePolicy policy;
        private double windowStart, warmUntil;
        private ulong lastTimestamp;
        private int previousCap;
        private bool allowBackgroundSampling;
        public int TargetFrameRate => policy?.TargetFrameRate ?? Application.targetFrameRate;
        public int QualityTier => policy?.QualityTier ?? -1;
        public int Decisions { get; private set; }
        public float FrameP95Ms { get; private set; }
        public float WorkP95Ms { get; private set; }
        public float RenderScale => instance == null ? 0 : instance.renderScale;

        public static bool AllowsAutomatic(string[] args) => !args.Any(a =>
            a == "-nfs-benchmark" || a == "-nfs-collision-probe" || a == "-nfs-stability-probe" ||
            a == "-nfs-fast-review" || a == "-nfs-map-review" || a == "-district-lifetime-probe" ||
            a == "-alabama-benchmark" || a == "-alabama-reliability");

        public bool Initialize(UniversalRenderPipelineAsset pipeline)
        {
            if (pipeline == null || instance != null) return false;
            original = pipeline;
            instance = Instantiate(original); instance.name = original.name + " (automatic runtime)";
            previousCap = Application.targetFrameRate;
            policy = new AutomaticPerformancePolicy(SystemInfo.graphicsMemorySize,SystemInfo.systemMemorySize);
            allowBackgroundSampling = Environment.GetCommandLineArgs().Contains("-nfs-auto-review");
            QualitySettings.renderPipeline = instance;
            ApplyDecision(); DiscardSamples(5);
            Debug.Log($"Automatic graphics: {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize} MB graphics memory, " +
                $"{SystemInfo.systemMemorySize} MB RAM; measuring actual frame times.");
            return true;
        }

        private void Update()
        {
            if (instance == null) return;
            var runtime = DistrictRuntime.Instance;
            if (Time.timeScale == 0 || (!Application.isFocused && !allowBackgroundSampling) ||
                (runtime != null && (!runtime.Ready || runtime.Busy)))
            { DiscardSamples(3); return; }
            FrameTimingManager.CaptureFrameTimings();
            if (Time.realtimeSinceStartupAsDouble < warmUntil) return;
            float elapsedMs = Time.unscaledDeltaTime * 1000;
            if (!float.IsFinite(elapsedMs) || elapsedMs <= 0) return;
            // Bound memory independently of frame rate. Loading/pause samples were discarded above.
            if (frames.Count < 512) frames.Add(elapsedMs);
            if (FrameTimingManager.GetLatestTimings(1,timings) > 0 &&
                timings[0].frameStartTimestamp != lastTimestamp && timings[0].gpuFrameTime > 0)
            {
                lastTimestamp = timings[0].frameStartTimestamp;
                // Active CPU threads exclude target-FPS and Present waits (Unity frame timing counters).
                double active = Math.Max(timings[0].gpuFrameTime,
                    Math.Max(timings[0].cpuMainThreadFrameTime,timings[0].cpuRenderThreadFrameTime));
                if (active > 0 && double.IsFinite(active) && work.Count < 512) work.Add((float)active);
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now-windowStart < 2 || frames.Count < 10) return;
            FrameP95Ms = Percentile(frames); WorkP95Ms = work.Count == 0 ? 0 : Percentile(work);
            bool reliable = work.Count >= frames.Count/2;
            if (policy.Observe(now,FrameP95Ms,WorkP95Ms,reliable))
            { Decisions++; ApplyDecision(); warmUntil = now+2; }
            frames.Clear(); work.Clear(); windowStart = now;
        }

        private static float Percentile(List<float> samples)
        { samples.Sort(); return samples[Mathf.FloorToInt((samples.Count-1)*.95f)]; }

        private void ApplyDecision()
        {
            Application.targetFrameRate = policy.TargetFrameRate;
            instance.renderScale = AutomaticPerformancePolicy.RenderScale(policy.QualityTier);
            instance.shadowDistance = Mathf.Min(original.shadowDistance,AutomaticPerformancePolicy.ShadowDistance(policy.QualityTier));
            Debug.Log($"Automatic graphics: target={policy.TargetFrameRate}, scale={instance.renderScale:F2}, " +
                $"shadows={instance.shadowDistance:F0}m, frame p95={FrameP95Ms:F2}ms, active work p95={WorkP95Ms:F2}ms.");
        }

        private void DiscardSamples(double seconds)
        {
            frames.Clear(); work.Clear(); policy?.DiscardWindow(); lastTimestamp = 0;
            windowStart = Time.realtimeSinceStartupAsDouble; warmUntil = windowStart+seconds;
        }

        public void Restore()
        {
            if (instance == null) return;
            if (QualitySettings.renderPipeline == instance) QualitySettings.renderPipeline = original;
            if (Application.targetFrameRate == policy.TargetFrameRate) Application.targetFrameRate = previousCap;
            Destroy(instance); instance = null;
        }
        private void OnDisable() => Restore();
    }
}
