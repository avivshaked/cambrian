using System;
using System.IO;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D229's fin law switch, <c>FluidConfig.ShapedAddedMass</c>: off by default, carried by the clone,
    /// the hash and the config file, and the recorded config fixtures read with it off.
    /// </summary>
    public sealed class ShapedAddedMassTests
    {
        [Fact]
        public void ItIsOffByDefaultAndTheCloneCarriesIt()
        {
            Assert.False(new FluidConfig().ShapedAddedMass);
            Assert.True(new FluidConfig { ShapedAddedMass = true }.Clone().ShapedAddedMass);
        }

        [Fact]
        public void ItMovesTheHashAndSurvivesASaveAndReload()
        {
            var off = new RunConfig();
            var on = new RunConfig();
            on.Fluid.ShapedAddedMass = true;
            Assert.NotEqual(off.Hash(), on.Hash());

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(on), out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.True(back.Fluid.ShapedAddedMass);
        }

        [Theory]
        [InlineData("r42-config.json")]
        [InlineData("r57-s2-config.json")]
        public void TheRecordedFixturesReadWithItOffAndTheirHashesHold(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", name);
            RunConfig config = RunConfigJson.Read(File.ReadAllText(path), out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.False(config.Fluid.ShapedAddedMass);
        }
    }
}
