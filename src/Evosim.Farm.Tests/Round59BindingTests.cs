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
    }
}
