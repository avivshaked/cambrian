using System;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The touch of the rock's dial (RunConfig.SenseBedTouch, D162): off by default, appended
    /// last, and refused in a world without per-part contact.
    /// </summary>
    public class BedTouchPoolTests
    {
        [Fact]
        public void TheTouchIsOffUntilItsDialIsOnAndThenComesLastInThePool()
        {
            Assert.DoesNotContain(SensorChannel.BedTouch, new RunConfig().SensorPool());

            SensorChannel[] without = new RunConfig { SenseChemical = true, SenseScent = true, SenseDrive = true }.SensorPool();
            SensorChannel[] wider = new RunConfig { SenseChemical = true, SenseScent = true, SenseDrive = true, SenseBedTouch = true }.SensorPool();

            Assert.Equal(without.Length + 1, wider.Length);
            for (int i = 0; i < without.Length; i++) Assert.Equal(without[i], wider[i]);
            Assert.Equal(SensorChannel.BedTouch, wider[without.Length]);

            SensorChannel[] alone = new RunConfig { SenseBedTouch = true }.SensorPool();
            Assert.Equal(SensorChannels.DefaultPool.Length + 1, alone.Length);
            Assert.Equal(1, SensorChannel.BedTouch.IndexCount());
        }

        [Fact]
        public void AWorldRefusesTheTouchWithoutPerPartContact()
        {
            var e = Assert.Throws<ArgumentException>(() => new World(new RunConfig { SenseBedTouch = true }, seed: 1));
            Assert.Contains("ContactPerPart", e.Message);
        }
    }
}
