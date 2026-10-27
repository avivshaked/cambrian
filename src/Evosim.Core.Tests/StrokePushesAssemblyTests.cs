using System;
using System.IO;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D231's switch, <c>FluidConfig.StrokePushesAssembly</c>: off by default, carried by the clone, the hash and
    /// the config file, the recorded config fixtures read with it off, and a world refuses it without the fin law
    /// or under the swing push.
    /// </summary>
    public sealed class StrokePushesAssemblyTests
    {
        [Fact]
        public void ItIsOffByDefaultAndTheCloneCarriesIt()
        {
            Assert.False(new FluidConfig().StrokePushesAssembly);
            Assert.True(new FluidConfig { StrokePushesAssembly = true }.Clone().StrokePushesAssembly);
        }

        [Fact]
        public void ItMovesTheHashAndSurvivesASaveAndReload()
        {
            var off = new RunConfig();
            var on = new RunConfig();
            on.Fluid.StrokePushesAssembly = true;
            Assert.NotEqual(off.Hash(), on.Hash());

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(on), out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.True(back.Fluid.StrokePushesAssembly);
        }

        [Theory]
        [InlineData("r42-config.json")]
        [InlineData("r57-s2-config.json")]
        public void TheRecordedFixturesReadWithItOffAndTheirHashesHold(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", name);
            RunConfig config = RunConfigJson.Read(File.ReadAllText(path), out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.False(config.Fluid.StrokePushesAssembly);
        }

        [Fact]
        public void AWorldRefusesItWithoutTheFinLawAndNamesBoth()
        {
            var config = new RunConfig();
            config.Fluid.StrokePushesAssembly = true;

            var e = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
            Assert.Contains("StrokePushesAssembly", e.Message);
            Assert.Contains("ShapedAddedMass", e.Message);

            config.Fluid.ShapedAddedMass = true;
            Assert.NotNull(new World(config, seed: 1));
        }

        [Fact]
        public void AWorldRefusesItUnderTheSwingPushAndNamesIt()
        {
            var config = new RunConfig();
            config.Fluid.StrokePushesAssembly = true;
            config.Fluid.ShapedAddedMass = true;
            config.Fluid.LimbPushCoefficient = 0.3f;

            var e = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
            Assert.Contains("StrokePushesAssembly", e.Message);
            Assert.Contains("swing push", e.Message);
        }
    }
}
