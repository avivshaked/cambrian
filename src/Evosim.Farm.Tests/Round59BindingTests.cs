using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// Round 59's launcher names reach the config (r59 step 6): the turning drag (D161), the touch
    /// of the rock (D162), the leaf smell and the no-corpse setting (D167), and the anchoring cell
    /// and its numbers (D162, D165), with the header's token for each.
    /// </summary>
    public sealed class Round59BindingTests
    {
        private static RunConfig Built(Dictionary<string, string> env) =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

        [Fact]
        public void UnsetEverythingIsOffAndTheHeaderSaysNothingNew()
        {
            RunConfig config = Built(new Dictionary<string, string>());

            Assert.Equal(TurningDragForm.None, config.Fluid.TurningDrag);
            Assert.False(config.SenseBedTouch);
            Assert.Equal(LeafScentForm.None, config.LeafScent);
            Assert.False(config.UnbittenDeathsLeaveNoCorpse);
            Assert.False(config.CellTypes.Contains(CellTypeIds.Anchoring));
            Assert.Equal(CellTypeRegistry.Standard.Count, config.CellTypes.Count);
            Assert.Equal("", Report.RoundFiftyNineToken(config));
        }

        [Fact]
        public void TheAnchoringCellIsAppendedWithItsNumbers()
        {
            RunConfig config = Built(new Dictionary<string, string>
            {
                { "EVOSIM_ANCHORING", "1" },
                { "EVOSIM_ANCHOR_TENACITY", "30000" },
                { "EVOSIM_ANCHOR_PRICE", "0.0002" },
                { "EVOSIM_ANCHOR_DECAY", "900" },
                { "EVOSIM_ANCHOR_RESERVE_THRESHOLD", "45" },
                { "EVOSIM_ANCHOR_RESERVE_LAG", "15" },
                { "EVOSIM_ANCHOR_UPKEEP", "3" },
                { "EVOSIM_ANCHOR_BUD_WEIGHT", "0.5" },
            });

            Assert.Equal(CellTypeRegistry.Standard.Count + 1, config.CellTypes.Count);
            Assert.Equal(CellTypeRegistry.Standard.Count, config.CellTypes.IndexOf(CellTypeIds.Anchoring));

            var cell = (AnchoringCell)config.CellTypes.Resolve(CellTypeIds.Anchoring);
            Assert.Equal(30000f, cell.TenacityNewtonsPerSquareMetre);
            Assert.Equal(0.0002f, cell.BuildJoulesPerNewton);
            Assert.Equal(900f, cell.DecaySeconds);
            Assert.Equal(45f, cell.ReserveThresholdSeconds);
            Assert.Equal(15f, cell.ReserveLagSeconds);
            Assert.Equal(3f, cell.UpkeepWattsPerCubicMetre);
            Assert.Equal(0.5f, cell.BudWeight);

            Assert.Equal(
                " · anchoring tenacity 30000 N/m2 price 0.0002 J/N decay 900 s reserve 45/15 s upkeep 3 W/m3 bud x0.5",
                Report.RoundFiftyNineToken(config));

            // On at its defaults: AnchoringCell's own.
            var plain = (AnchoringCell)Built(new Dictionary<string, string> { { "EVOSIM_ANCHORING", "1" } })
                .CellTypes.Resolve(CellTypeIds.Anchoring);
            var reference = new AnchoringCell();
            Assert.Equal(reference.FullHashContribution(), plain.FullHashContribution());
        }

        [Fact]
        public void TheLeafSmellTheNoCorpseSettingTheTouchAndTheTurningDragReachTheConfig()
        {
            RunConfig config = Built(new Dictionary<string, string>
            {
                { "EVOSIM_LEAF_SCENT", "direction" },
                { "EVOSIM_LEAF_SCENT_RANGE", "5" },
                { "EVOSIM_LEAF_SCENT_CELL", "1.5" },
                { "EVOSIM_LEAF_SCENT_HALF", "0.25" },
                { "EVOSIM_UNBITTEN_NO_CORPSE", "1" },
                { "EVOSIM_SENSE_BED_TOUCH", "1" },
                { "EVOSIM_TURNING_DRAG", "spin" },
                { "EVOSIM_SPIN_DRAG", "0.05" },
            });

            Assert.Equal(LeafScentForm.Direction, config.LeafScent);
            Assert.Equal(5f, config.LeafScentRangeMetres);
            Assert.Equal(1.5f, config.LeafScentCellMetres);
            Assert.Equal(0.25f, config.LeafScentHalfScaleSquareMetres);
            Assert.True(config.UnbittenDeathsLeaveNoCorpse);
            Assert.True(config.SenseBedTouch);
            Assert.Equal(TurningDragForm.Spin, config.Fluid.TurningDrag);
            Assert.Equal(0.05f, config.Fluid.SpinDragCoefficient);

            Assert.Equal(
                " · leaf smell direction range 5 m cell 1.5 m half 0.25 m2 · unbitten burnt",
                Report.RoundFiftyNineToken(config));

            Assert.Throws<ArgumentException>(() => Built(new Dictionary<string, string> { { "EVOSIM_LEAF_SCENT", "taste" } }));
        }

        /// <summary>D229's fin law: <c>EVOSIM_SHAPED_ADDED_MASS</c> reaches the fluid config, and unset it is off.</summary>
        [Fact]
        public void TheShapedAddedMassSwitchReachesTheFluidConfigAndIsOffWhenUnset()
        {
            Assert.False(Built(new Dictionary<string, string>()).Fluid.ShapedAddedMass);
            Assert.True(Built(new Dictionary<string, string> { { "EVOSIM_SHAPED_ADDED_MASS", "1" } }).Fluid.ShapedAddedMass);
        }

        /// <summary>D231: <c>EVOSIM_STROKE_ASSEMBLY</c> reaches the fluid config, and unset it is off.</summary>
        [Fact]
        public void TheStrokeAssemblySwitchReachesTheFluidConfigAndIsOffWhenUnset()
        {
            Assert.False(Built(new Dictionary<string, string>()).Fluid.StrokePushesAssembly);
            Assert.True(Built(new Dictionary<string, string> { { "EVOSIM_STROKE_ASSEMBLY", "1" } }).Fluid.StrokePushesAssembly);
        }

        /// <summary>D228's mirrored joint: <c>EVOSIM_REFLECT_MIRRORED_ANCHORS</c> reaches the development limits, and unset it is off.</summary>
        [Fact]
        public void TheReflectMirroredAnchorsSwitchReachesTheDevelopmentLimitsAndIsOffWhenUnset()
        {
            Assert.False(Built(new Dictionary<string, string>()).Development.ReflectMirroredAnchors);
            Assert.True(Built(new Dictionary<string, string> { { "EVOSIM_REFLECT_MIRRORED_ANCHORS", "1" } }).Development
                .ReflectMirroredAnchors);

            // Beside its neighbour, which it does not move.
            RunConfig both = Built(new Dictionary<string, string>
            {
                { "EVOSIM_REFLECT_MIRRORED_ANCHORS", "1" }, { "EVOSIM_SCALE_LENGTHENS_ONCE", "1" },
            });
            Assert.True(both.Development.ReflectMirroredAnchors);
            Assert.True(both.Development.ScaleLengthensOnce);
            Assert.False(Built(new Dictionary<string, string> { { "EVOSIM_SCALE_LENGTHENS_ONCE", "1" } })
                .Development.ReflectMirroredAnchors);
        }

        /// <summary>
        /// D228's true shape: <c>EVOSIM_CONTACT_SHAPE</c> takes <c>spheres</c> or <c>trueshape</c>, unset is the
        /// spheres, a third value is refused, and the header says so only when it is not the spheres.
        /// </summary>
        [Fact]
        public void TheContactShapeSwitchTakesSpheresOrTrueShapeAndRefusesAThird()
        {
            Assert.Equal(ContactShapeForm.Spheres, Built(new Dictionary<string, string>()).ContactShape);
            Assert.Equal(ContactShapeForm.Spheres, Built(new Dictionary<string, string> { { "EVOSIM_CONTACT_SHAPE", "spheres" } }).ContactShape);

            var on = new Dictionary<string, string> { { "EVOSIM_CONTACT_SHAPE", "TrueShape" }, { "EVOSIM_CONTACT_PER_PART", "1" } };
            RunConfig config = Built(on);
            Assert.Equal(ContactShapeForm.TrueShape, config.ContactShape);
            Assert.True(config.ContactPerPart);

            Assert.Throws<ArgumentException>(() => Built(new Dictionary<string, string> { { "EVOSIM_CONTACT_SHAPE", "hull" } }));

            // Reaches the solver's config as well.
            Assert.Equal(ContactShapeForm.TrueShape, Evosim.Dynamics.SolverConfig.From(config, 0.01).ContactShape);
        }

        /// <summary>
        /// D228's third value: <c>EVOSIM_CONTACT_SHAPE=trueshapeatpoint</c> (any case) binds to
        /// <see cref="ContactShapeForm.TrueShapeAtPoint"/>, needs per-part contact like the second, reaches the solver's
        /// config, and the header names it in lower case.
        /// </summary>
        [Fact]
        public void TheContactShapeSwitchTakesTheThirdValueTrueShapeAtPoint()
        {
            var on = new Dictionary<string, string> { { "EVOSIM_CONTACT_SHAPE", "TrueShapeAtPoint" }, { "EVOSIM_CONTACT_PER_PART", "1" } };
            RunConfig config = Built(on);
            Assert.Equal(ContactShapeForm.TrueShapeAtPoint, config.ContactShape);
            Assert.True(config.ContactPerPart);
            Assert.Equal(ContactShapeForm.TrueShapeAtPoint, Evosim.Dynamics.SolverConfig.From(config, 0.01).ContactShape);

            // The lower-case token reads the same, and the second value is still its own.
            on["EVOSIM_CONTACT_SHAPE"] = "trueshapeatpoint";
            Assert.Equal(ContactShapeForm.TrueShapeAtPoint, Built(on).ContactShape);
            on["EVOSIM_CONTACT_SHAPE"] = "trueshape";
            Assert.Equal(ContactShapeForm.TrueShape, Built(on).ContactShape);

            // The hash separates the three values, and the first two are what they were.
            var spheres = new RunConfig { ContactPerPart = true, ContactShape = ContactShapeForm.Spheres };
            var shape = new RunConfig { ContactPerPart = true, ContactShape = ContactShapeForm.TrueShape };
            var point = new RunConfig { ContactPerPart = true, ContactShape = ContactShapeForm.TrueShapeAtPoint };
            Assert.NotEqual(shape.Hash(), point.Hash());
            Assert.NotEqual(spheres.Hash(), point.Hash());
        }
    }
}
