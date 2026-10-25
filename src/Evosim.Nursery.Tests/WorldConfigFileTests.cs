using System;
using System.IO;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The world configs under rounds/nursery/configs/ that this build reads (README.md there): each loads, each is named by the hash Core computes
    /// for it and records that hash, and the tunables the round-62 build and integration-1 added stand at their defaults, so the move left each
    /// world as it was.
    /// </summary>
    public class WorldConfigFileTests
    {
        private static readonly string Configs = Path.Combine(Recipe.RepoRootAbove(AppContext.BaseDirectory), "rounds", "nursery", "configs");

        /// <summary>Each new file and the file it was made from (round 60's format, kept beside it).</summary>
        [Theory]
        [InlineData("b2885199f9213276", "a8cbe76c9532bc71")]
        [InlineData("cd9cc88c0054cf90", "2b125f6cbda84a01")]
        [InlineData("9fa947462638667c", "e80c201e54b3963f")]
        public void ARound62ConfigLoadsAndIsNamedByItsHash(string name, string parent)
        {
            string text = File.ReadAllText(Path.Combine(Configs, name + ".json"));
            RunConfig c = RunConfigJson.Read(text, out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(name, c.Hash());
            Assert.True(File.Exists(Path.Combine(Configs, parent + ".json")), "the parent file is kept");

            // The tunables the build added since round 60 stand at their defaults, so the world is the parent's.
            var d = new RunConfig();
            Assert.Equal(d.ReefShape, c.ReefShape);
            Assert.False(c.Development.ReflectMirroredAnchors);
            Assert.False(c.Development.ScaleLengthensOnce);
            Assert.False(c.PlantsKeepMatter);
            Assert.False(c.PlantsKeepOwnFirst);
            Assert.Equal(0f, c.CorpseHalfLifeSeconds);
            Assert.False(c.Fluid.ShapedAddedMass);
            Assert.Equal(1f, c.BiteDamageScale);
            Assert.False(c.BiteTakesStore);
            Assert.False(c.KillSwallowsBody);
            Assert.False(c.ContactEating);
            Assert.False(c.IntakeReachFromMouth);
            Assert.Equal(1f, c.IntakeScale);
            Assert.False(c.DragLimitAtEveryStep);
            Assert.False(c.DragLimitByWeldedBody);
            Assert.Equal(ContactShapeForm.Spheres, c.ContactShape);
            Assert.True(c.MatterBurialDesertPerSecond < 0f);
            Assert.False(c.ReadAcrossSeams);
            Assert.Empty(c.SeedOverrides);
        }
    }
}
