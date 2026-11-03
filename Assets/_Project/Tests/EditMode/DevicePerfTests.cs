using MafiaNight.Core;
using NUnit.Framework;

namespace MafiaNight.Tests
{
    /// <summary>The device tiers must put real phones where they belong, and the governor must only react to a steady slowness (Docs/13-performance-rules.md).</summary>
    public class DevicePerfTests
    {
        [TestCase(11084, 8, "Adreno (TM) 750", DeviceTier.High)]     // Galaxy S24 Ultra
        [TestCase(7686, 8, "Mali-G57 MC2", DeviceTier.Mid)]          // TECNO CLA6
        [TestCase(8192, 8, "Mali-G715-Immortalis MC11", DeviceTier.High)]
        [TestCase(6144, 8, "Adreno (TM) 619", DeviceTier.Mid)]
        [TestCase(3072, 8, "Adreno (TM) 750", DeviceTier.Low)]       // a strong chip with little memory is held back by the memory
        [TestCase(4096, 4, "Adreno (TM) 610", DeviceTier.Low)]       // four cores
        [TestCase(2048, 4, "Mali-T830", DeviceTier.Low)]
        [TestCase(4096, 8, "PowerVR GE8320", DeviceTier.Low)]
        [TestCase(16221, 16, "NVIDIA GeForce RTX 3060", DeviceTier.High)]
        public void A_phone_lands_in_the_tier_its_weakest_part_allows(int ramMb, int cores, string gpu, DeviceTier expected)
        {
            Assert.AreEqual(expected, DevicePerf.TierOf(ramMb, cores, gpu));
        }

        [Test]
        public void One_slow_frame_among_good_ones_is_not_slowness()
        {
            Assert.IsFalse(DevicePerfGovernor.IsOverBudget(new double[] { 6, 7, 6, 300, 7, 6, 8 }, 16.7));
        }

        [Test]
        public void A_steady_cost_over_the_budget_is_slowness()
        {
            Assert.IsTrue(DevicePerfGovernor.IsOverBudget(new double[] { 22, 25, 19, 30, 21, 24, 23 }, 16.7));
        }

        [Test]
        public void Too_few_samples_judge_nothing()
        {
            Assert.IsFalse(DevicePerfGovernor.IsOverBudget(new double[] { 50, 60 }, 16.7));
        }
    }
}
