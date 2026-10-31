using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D268's anchoring build in Core: the cell's three new numbers (this file's first section), and the brain's hold output
    /// and two grip senses (the sections after it, as each step lands). The switch is on the cell, so a world without the
    /// cell is untouched by any of it.
    /// </summary>
    public sealed class AnchorBrainTests
    {
        private readonly ITestOutputHelper _out;

        public AnchorBrainTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------------------ the cell's numbers

        [Fact]
        public void TheDefaultsLeaveTheGripAsRoundFiftyNineBuiltIt()
        {
            var cell = new AnchoringCell();
            Assert.False(cell.BrainHold);
            Assert.Equal(-0.5f, cell.ReleaseBelow);
            Assert.Equal(5e-5f, cell.ReleaseJoulesPerNewton);
            Assert.Equal(cell.BuildJoulesPerNewton, cell.ReleaseJoulesPerNewton);
        }

        [Fact]
        public void TheNewNumbersSurviveAJsonRoundTripAndReachTheHash()
        {
            var cell = new AnchoringCell(
                tenacityNewtonsPerSquareMetre: 3e4f, buildJoulesPerNewton: 5e-8f, decaySeconds: 900f,
                reserveThresholdSeconds: 45f, reserveLagSeconds: 15f, upkeepWattsPerCubicMetre: 3f,
                brainHold: true, releaseBelow: -0.25f, releaseJoulesPerNewton: 2e-8f);
            var config = new RunConfig { CellTypes = CellTypeRegistry.Standard.Appending(cell) };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config), out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(config.Hash(), back.Hash());

            var read = (AnchoringCell)back.CellTypes.Resolve(CellTypeIds.Anchoring);
            Assert.True(read.BrainHold);
            Assert.Equal(-0.25f, read.ReleaseBelow);
            Assert.Equal(2e-8f, read.ReleaseJoulesPerNewton);
            Assert.Equal(5e-8f, read.BuildJoulesPerNewton);
        }

        [Fact]
        public void EachNewNumberMovesTheCellsHash()
        {
            string Hash(AnchoringCell c) => c.HashContribution();
            string plain = Hash(new AnchoringCell());

            Assert.NotEqual(plain, Hash(new AnchoringCell(brainHold: true)));
            Assert.NotEqual(plain, Hash(new AnchoringCell(releaseBelow: -0.4f)));
            Assert.NotEqual(plain, Hash(new AnchoringCell(releaseJoulesPerNewton: 1e-5f)));
            Assert.Equal(plain, Hash(new AnchoringCell()));

            // And through the table, which is what the config hash reads.
            string Table(AnchoringCell c) => CellTypeRegistry.Standard.Appending(c).HashContribution();
            Assert.NotEqual(Table(new AnchoringCell()), Table(new AnchoringCell(brainHold: true)));
        }

        [Theory]
        [InlineData("brainHold")]
        [InlineData("releaseBelow")]
        [InlineData("releaseJoulesPerNewton")]
        public void AMissingNewFieldIsRefusedByName(string field)
        {
            var config = new RunConfig { CellTypes = CellTypeRegistry.Standard.Appending(new AnchoringCell()) };
            string text = RunConfigJson.Write(config);

            // The anchoring cell is the table's last entry; rename the field in it alone, which leaves it missing.
            string key = "\"" + field + "\"";
            int at = text.LastIndexOf(key, StringComparison.Ordinal);
            Assert.True(at >= 0, $"no {key} in the written config");
            string cut = text.Remove(at, key.Length).Insert(at, "\"" + field + "Gone\"");

            Exception e = Assert.ThrowsAny<Exception>(() => RunConfigJson.Read(cut));
            _out.WriteLine(e.Message);
            Assert.Contains(field, e.Message);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(1.01f)]
        [InlineData(-1.01f)]
        public void AThresholdOutsideTheOutputsRangeIsRefused(float below)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(releaseBelow: below));
        }

        [Theory]
        [InlineData(-1f)]
        [InlineData(0f)]
        [InlineData(1f)]
        public void AThresholdInTheRangeIsAccepted(float below)
        {
            Assert.Equal(below, new AnchoringCell(releaseBelow: below).ReleaseBelow);
        }

        [Theory]
        [InlineData(-1e-9f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void ANegativeOrNonFiniteReleasePriceIsRefused(float price)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(releaseJoulesPerNewton: price));
        }

        [Fact]
        public void AZeroReleasePriceIsAllowedWhereAZeroBuildPriceIsNot()
        {
            var free = new AnchoringCell(releaseJoulesPerNewton: 0f);
            Assert.Equal(0f, free.ReleasePrice(210f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(buildJoulesPerNewton: 0f));
        }

        [Fact]
        public void BothPricesScaleByTheFullBreakingForce()
        {
            var cell = new AnchoringCell(buildJoulesPerNewton: 5e-8f, releaseJoulesPerNewton: 2e-8f);
            float force = cell.BreakingForce(Cube(0.06f));

            Assert.Equal(2e-8f * force, cell.ReleasePrice(force), 12);
            Assert.Equal(5e-8f * force, cell.BuildPrice(force), 12);
            Assert.Equal(0f, cell.ReleasePrice(-5f));

            // A bud twice the size costs 2^(2/3) times as much to let go of: by area, as the force is.
            float big = cell.BreakingForce(2f * Cube(0.06f));
            Assert.Equal(Math.Pow(2.0, 2.0 / 3.0), cell.ReleasePrice(big) / cell.ReleasePrice(force), 4);
        }

        private static float Cube(float edge) => edge * edge * edge;
    }
}
