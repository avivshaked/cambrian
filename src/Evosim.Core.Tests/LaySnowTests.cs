using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="World.LaySnow"/>, the nursery tank's larder (fable-propose-world-nursery.md, R6):
    /// snow laid on the bed is booked in both books as a placed corpse is, lands in the lowest
    /// live cells and nowhere else, and is then any other snow.
    /// </summary>
    public class LaySnowTests
    {
        private static RunConfig Tank(FieldModelChoice model = FieldModelChoice.Grid)
        {
            var config = new RunConfig
            {
                Light = new LightModel(1e-6f, 1f),
                MinimumPopulation = 0,
                WorldDepthMetres = 10f,
                RemineralisationPerSecond = 0f,
            };
            if (model == FieldModelChoice.Grid)
            {
                config.SharedSpace = true;
                config.WorldShape = WorldShape.Tank;
                config.FieldModel = MatterField.Grid;
                config.WorldAreaSquareMetres = 100f;
                config.HorizontalPatches = 1;
                config.FieldCellMetres = 1f;
                config.FieldMatterCellMetres = 2f;
                config.NutrientMixingDiffusivity = 0.02f;
                config.HorizontalMixingDiffusivity = 0.02f;
                config.MatterMixingDiffusivity = 0.02f;
                config.MatterBudgetUnits = 100f;
            }
            return config;
        }

        public enum FieldModelChoice { Grid, Default }

        [Fact]
        public void SnowIsLaidOnTheBedAndBookedAsInflux()
        {
            var world = new World(Tank(), seed: 1);
            var grid = (GridField)world.Nutrients;
            int columns = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
                for (int iz = 0; iz < grid.CellsZ; iz++)
                    if (grid.ColumnIsLive(ix, iz)) columns++;
            Assert.True(columns > 50, $"live columns {columns}");

            double inBefore = world.EnergyIn, heldBefore = grid.TotalJoules;
            double bedBefore = grid.StockInLayer(grid.LayerCount - 1), aboveBefore = grid.StockInLayer(grid.LayerCount - 3);

            double laid = world.LaySnow(2f, 2);

            Assert.Equal(2d * 2 * columns * grid.CellVolume, laid, 6);
            Assert.Equal(laid, world.EnergyIn - inBefore, 9);
            Assert.Equal(laid, grid.TotalJoules - heldBefore, 6);
            Assert.Equal(laid / 2d, grid.StockInLayer(grid.LayerCount - 1) - bedBefore, 6);
            Assert.Equal(aboveBefore, grid.StockInLayer(grid.LayerCount - 3), 9);

            for (int i = 0; i < 200; i++) world.Step(0.5f);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
        }

        [Fact]
        public void SnowLaidInIslandsFillsTheCoverAndIsBookedAsInflux()
        {
            var world = new World(Tank(), seed: 1);
            var grid = (GridField)world.Nutrients;
            double inBefore = world.EnergyIn, heldBefore = grid.TotalJoules;

            double laid = world.LaySnowIslands(2f, 7UL, 3f, 0.25f);

            Assert.Equal(2d * grid.LiveVolumeCubicMetres, laid, 6);
            Assert.Equal(laid, world.EnergyIn - inBefore, 9);
            Assert.Equal(laid, grid.TotalJoules - heldBefore, 6);

            // The snow lies in about a quarter of the live columns, and every layer of a snowy
            // column holds it.
            int live = 0, snowy = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
                for (int iz = 0; iz < grid.CellsZ; iz++)
                {
                    if (!grid.ColumnIsLive(ix, iz)) continue;
                    live++;
                    if (grid.JoulesAt(ix, 0, iz) > 1e-9) snowy++;
                }
            double share = (double)snowy / live;
            Assert.True(share > 0.15 && share < 0.3, $"snowy share {share:0.000} of {live}");

            for (int i = 0; i < 200; i++) world.Step(0.5f);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
        }

        [Fact]
        public void NothingIsLaidAtZeroAndANegativeDensityIsRefused()
        {
            var world = new World(Tank(), seed: 1);
            double before = world.EnergyIn;
            Assert.Equal(0d, world.LaySnow(0f, 1));
            Assert.Equal(before, world.EnergyIn);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.LaySnow(-1f, 1));
        }

        [Fact]
        public void SnowOffAGridIsRefused()
        {
            var world = new World(Tank(FieldModelChoice.Default), seed: 1);
            Assert.Throws<InvalidOperationException>(() => world.LaySnow(1f, 1));
        }
    }
}