using Alabama.Driving;
using NUnit.Framework;

namespace Alabama.Tests
{
    public sealed class AutomaticPerformanceTests
    {
        [Test]
        public void SustainedLoadReducesGraphicsBeforeChoosingThirtyAndDoesNotOscillate()
        {
            var policy = new AutomaticPerformancePolicy(2048,8192);
            Assert.That(policy.Observe(2,26,24,true), Is.False);
            Assert.That(policy.Observe(4,26,24,true), Is.True);
            Assert.That(policy.QualityTier, Is.EqualTo(1));
            Assert.That(policy.TargetFrameRate, Is.EqualTo(60));
            Assert.That(policy.Observe(6,26,24,true), Is.False);
            policy.Observe(8,26,24,true); policy.Observe(10,26,24,true);
            Assert.That(policy.QualityTier, Is.Zero);
            policy.Observe(14,26,24,true); policy.Observe(16,26,24,true);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(30));
            for (int time = 20; time <= 40; time += 2) policy.Observe(time,33.34f,24,true);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(30));
            Assert.That(policy.QualityTier, Is.Zero);
        }

        [Test]
        public void RecoveringDeviceReturnsToSixtyAndStrongDeviceCanReachFullResolution()
        {
            var policy = new AutomaticPerformancePolicy(2048,8192);
            for (int time = 2; time <= 16; time += 2) policy.Observe(time,28,26,true);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(30));
            policy.Observe(20,33.34f,10,true); policy.Observe(22,33.34f,10,true);
            Assert.That(policy.Observe(24,33.34f,10,true), Is.True);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(60));
            var strong = new AutomaticPerformancePolicy(8192,32768);
            for (int time = 2; time <= 10; time += 2) strong.Observe(time,16.67f,8,true);
            Assert.That(strong.QualityTier, Is.EqualTo(4));
            Assert.That(AutomaticPerformancePolicy.RenderScale(strong.QualityTier), Is.EqualTo(1));
        }

        [Test]
        public void MissingTimingRetriesSixtyWithoutMistakingCapWaitsForHeadroom()
        {
            var policy = new AutomaticPerformancePolicy(2048,8192);
            for (int time = 2; time <= 16; time += 2) policy.Observe(time,28,0,false);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(30));
            for (int time = 20; time <= 44; time += 2) policy.Observe(time,33.34f,0,false);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(30));
            Assert.That(policy.Observe(46,33.34f,0,false), Is.True);
            Assert.That(policy.TargetFrameRate, Is.EqualTo(60));
        }

        [Test]
        public void LoadingAndSingleHitchesDoNotChangeQualityOrOverrideReviewCaps()
        {
            var policy = new AutomaticPerformancePolicy(2048,8192);
            policy.Observe(2,150,140,true); policy.DiscardWindow();
            policy.Observe(4,16.67f,10,true);
            Assert.That(policy.QualityTier, Is.EqualTo(2));
            Assert.That(AutomaticPerformance.AllowsAutomatic(new[] { "Alabama.exe" }), Is.True);
            Assert.That(AutomaticPerformance.AllowsAutomatic(new[] { "-nfs-auto-review" }), Is.True);
            Assert.That(AutomaticPerformance.AllowsAutomatic(new[] { "-nfs-benchmark" }), Is.False);
            Assert.That(AutomaticPerformance.AllowsAutomatic(new[] { "-district-lifetime-probe" }), Is.False);
        }
    }
}
