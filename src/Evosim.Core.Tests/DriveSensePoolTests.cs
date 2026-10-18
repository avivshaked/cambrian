using System;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The drive sense's dial (RunConfig.SenseDrive, the owner's ruling of 2026-10-01): off by
    /// default, appended last, and refused in a world with no envelope to sense.
    /// </summary>
    public class DriveSensePoolTests
    {
        [Fact]
        public void TheDriveSenseIsOffUntilItsDialIsOnAndThenComesLastInThePool()
        {
            SensorChannel[] plain = new RunConfig().SensorPool();
            Assert.DoesNotContain(SensorChannel.Stroke, plain);
            Assert.DoesNotContain(SensorChannel.Hold, plain);
            Assert.DoesNotContain(SensorChannel.Ramp, plain);

            SensorChannel[] without = new RunConfig { SenseChemical = true, SenseDamage = true, SenseScent = true }.SensorPool();
            SensorChannel[] wider = new RunConfig { SenseChemical = true, SenseDamage = true, SenseScent = true, SenseDrive = true }.SensorPool();

            Assert.Equal(without.Length + 3, wider.Length);
            for (int i = 0; i < without.Length; i++) Assert.Equal(without[i], wider[i]);
            Assert.Equal(SensorChannel.Stroke, wider[without.Length]);
            Assert.Equal(SensorChannel.Hold, wider[without.Length + 1]);
            Assert.Equal(SensorChannel.Ramp, wider[without.Length + 2]);

            // Alone, after the four of every run.
            SensorChannel[] alone = new RunConfig { SenseDrive = true }.SensorPool();
            Assert.Equal(SensorChannels.DefaultPool.Length + 3, alone.Length);
        }

        [Fact]
        public void EachOfTheThreeIndexesADegreeOfFreedom()
        {
            Assert.Equal(3, SensorChannel.Stroke.IndexCount());
            Assert.Equal(3, SensorChannel.Hold.IndexCount());
            Assert.Equal(3, SensorChannel.Ramp.IndexCount());
        }

        [Fact]
        public void AWorldRefusesTheDriveSenseWithNoBendAndPowerDrive()
        {
            Assert.Throws<ArgumentException>(() => new World(new RunConfig { SenseDrive = true }, seed: 1));
        }
    }
}
