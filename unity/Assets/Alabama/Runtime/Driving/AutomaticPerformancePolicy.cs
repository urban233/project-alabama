using System;

namespace Alabama.Driving
{
    /// <summary>Slow, measured graphics decisions; never changes simulation time or vehicle tuning.</summary>
    public sealed class AutomaticPerformancePolicy
    {
        public int TargetFrameRate { get; private set; } = 60;
        public int QualityTier { get; private set; }
        private int slowWindows, fastWindows;
        private double nextChange, nextTrial = double.PositiveInfinity;

        public AutomaticPerformancePolicy(int graphicsMemoryMb, int systemMemoryMb)
        {
            // Memory is only an initial hint. Measured performance owns every later decision.
            QualityTier = graphicsMemoryMb >= 6144 && systemMemoryMb >= 16384 ? 3 : 2;
        }

        public bool Observe(double now, float frameP95Ms, float workP95Ms, bool reliableWorkTiming)
        {
            if (!float.IsFinite(frameP95Ms) || frameP95Ms <= 0) return false;
            if (now < nextChange) return false;
            bool slow = frameP95Ms > (TargetFrameRate == 60 ? 20 : 38);
            slowWindows = slow ? slowWindows + 1 : 0;
            bool spare = reliableWorkTiming && float.IsFinite(workP95Ms) && workP95Ms > 0 &&
                workP95Ms < (TargetFrameRate == 30 ? 13 : 11.5f);
            fastWindows = !slow && spare ? fastWindows + 1 : 0;
            if (slowWindows >= 2)
            {
                if (QualityTier > 0) QualityTier--;
                else if (TargetFrameRate == 60)
                { TargetFrameRate = 30; nextTrial = now + 30; }
                else { slowWindows = 0; return false; }
                nextChange = now + 4;
            }
            else if (TargetFrameRate == 30 && (fastWindows >= 3 || now >= nextTrial))
            {
                // A bounded retry also works on devices that cannot report GPU timings.
                TargetFrameRate = 60; nextTrial = double.PositiveInfinity; nextChange = now + 4;
            }
            else if (TargetFrameRate == 60 && QualityTier < 4 && fastWindows >= 5)
            { QualityTier++; nextChange = now + 20; }
            else return false;
            slowWindows = fastWindows = 0;
            return true;
        }

        public void DiscardWindow() => slowWindows = fastWindows = 0;
        public static float RenderScale(int tier) => new[] { .55f, .65f, .75f, .85f, 1f }[Math.Clamp(tier,0,4)];
        public static float ShadowDistance(int tier) => new[] { 60f, 90, 120, 150, 150 }[Math.Clamp(tier,0,4)];
    }
}
