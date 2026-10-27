using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D233's two launcher names reach the config: <c>EVOSIM_SHAPES</c>, the shapes a world allows, and
    /// <c>EVOSIM_FORM_CHANCE</c>, the family's form walk. Unset, both are the recorded world and the
    /// header says nothing new.
    /// </summary>
    public sealed class ShapeFamilyBindingTests
    {
        private static RunConfig Built(Dictionary<string, string> env) =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

        private static string Header(Dictionary<string, string> env)
        {
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(env));
            RunConfig config = EnvBinding.BuildConfig(settings);
            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(world, hasWall: true, hasFloor: true, threads: 1, inoculumHashShort: null);
            return Report.HeaderLine(settings, config, space, "9.9.9.9");
        }

        private const string AllFour = "box,sphere,capsule,superellipsoid";

        [Fact]
        public void UnsetTheWorldHasTheThreeNamedShapesNoFormWalkAndTheHeaderSaysNothingNew()
        {
            var env = new Dictionary<string, string>();
            RunConfig config = Built(env);

            Assert.Equal(new[] { ShapeIds.Box, ShapeIds.Sphere, ShapeIds.Capsule }, config.ShapeIds);
            Assert.Equal(PartShapeRegistry.Standard.HashContribution(), config.Shapes.HashContribution());
            Assert.Equal(0f, config.Mutation.FormChance);
            Assert.Equal(Built(new Dictionary<string, string> { { "EVOSIM_SHAPES", "box,sphere,capsule" } }).Hash(), config.Hash());

            string header = Header(env);
            Assert.DoesNotContain(" · shapes ", header);
            Assert.DoesNotContain(" · form mut ", header);
        }

        [Fact]
        public void TheFourShapesAndTheFormChanceReachTheConfigAndTheHeader()
        {
            var env = new Dictionary<string, string>
            {
                { "EVOSIM_SHAPES", " box, sphere ,capsule,superellipsoid " },
                { "EVOSIM_FORM_CHANCE", "0.05" },
            };
            RunConfig config = Built(env);

            Assert.Equal(new[] { ShapeIds.Box, ShapeIds.Sphere, ShapeIds.Capsule, ShapeIds.Superellipsoid }, config.ShapeIds);
            Assert.Equal(4, config.Shapes.Count);
            Assert.True(config.Shapes.Resolve(ShapeIds.Superellipsoid).TakesForm);
            Assert.Equal(0.05f, config.Mutation.FormChance);
            Assert.NotEqual(Built(new Dictionary<string, string>()).Hash(), config.Hash());

            string header = Header(env);
            Assert.Contains(" · shapes " + AllFour, header);
            Assert.Contains(" · form mut 0.05", header);
        }

        [Fact]
        public void TheOrderOfTheListIsKeptAndReachesTheHash()
        {
            RunConfig a = Built(new Dictionary<string, string> { { "EVOSIM_SHAPES", AllFour } });
            RunConfig b = Built(new Dictionary<string, string> { { "EVOSIM_SHAPES", "superellipsoid,box,sphere,capsule" } });

            Assert.Equal(ShapeIds.Superellipsoid, b.Shapes[0].Id);
            Assert.NotEqual(a.Hash(), b.Hash());
        }

        [Theory]
        [InlineData("box,sphere,cone")]
        [InlineData("box,,sphere")]
        [InlineData("Box,sphere")]
        public void AnIdTheCatalogueDoesNotKnowIsRefused(string list)
        {
            var e = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string> { { "EVOSIM_SHAPES", list } })));
            Assert.Contains("EVOSIM_SHAPES", e.Message);
            Assert.Contains("Known:", e.Message);
        }

        [Fact]
        public void AnIdNamedTwiceIsRefused()
        {
            var e = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string> { { "EVOSIM_SHAPES", "box,sphere,box" } })));
            Assert.Contains("'box' twice", e.Message);
        }

        [Fact]
        public void EachSettingsObjectHoldsItsOwnDefaultList()
        {
            EnvSettings a = EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>()));
            EnvSettings b = EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>()));
            Assert.NotSame(a.ShapeIds, b.ShapeIds);
        }
    }
}
