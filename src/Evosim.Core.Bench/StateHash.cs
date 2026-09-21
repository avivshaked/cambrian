using System;
using System.Globalization;
using Evosim.Core;

namespace Evosim.Core.Bench
{
    /// <summary>
    /// One 64-bit word over everything a threaded field pass could move: every cell of both
    /// grids, the light the water is letting through at every layer, and every living body's
    /// reserve, tissue, age and place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bits, not decimals.</b> Every number goes in as the IEEE bit pattern of the double or
    /// float it is, so a difference of one unit in the last place changes the word. Rounding to a
    /// printable width would hide exactly the drift this is here to catch.
    /// </para>
    /// <para>
    /// <b>FNV-1a, because it is four lines and has no implementation to disagree with.</b> This
    /// is not a security hash; it is an equality test with a short name, and it has to give the
    /// same answer in a test project and in a console bench compiled against a different tree.
    /// </para>
    /// <para>
    /// <b>The walk order is the grid's own index order</b>, so the word is a property of the
    /// world's state and not of anything about how the state was computed.
    /// </para>
    /// </remarks>
    internal static class StateHash
    {
        public static string Of(World world)
        {
            ulong h = 14695981039346656037UL;

            Grid(ref h, world.Nutrients);
            Grid(ref h, world.Matter);

            // The canopy, layer by layer, down the whole water column plus one layer past it.
            int layers = (int)(world.Config.WorldDepthMetres / world.Field.LayerMetres) + 2;
            for (int i = 0; i < layers; i++)
            {
                float y = -i * world.Field.LayerMetres;
                Float(ref h, world.Field.IrradianceAt(y));
                Float(ref h, world.Field.ShadingAt(y));
            }

            Double(ref h, world.Field.DayFactor);

            // The population, in the world's own order.
            Long(ref h, world.Living.Count);

            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];

                Long(ref h, creature.Id);
                Float(ref h, creature.Energy);
                Float(ref h, creature.TissueJoules);
                Float(ref h, creature.Age);
                Float(ref h, creature.HeightY);
                Float(ref h, creature.X);
                Float(ref h, creature.Z);
                Long(ref h, creature.Patch);
            }

            // And the books, which every pass above reports into.
            Double(ref h, world.EnergyIn);
            Double(ref h, world.EnergyOut);
            Double(ref h, world.RemineralisedTotal);
            Double(ref h, world.DetritusTakenTotal);
            Double(ref h, world.BurntTotal);
            Long(ref h, world.Births);
            Long(ref h, world.Deaths);
            Long(ref h, world.Stillbirths);

            return h.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void Grid(ref ulong h, IMatterField field)
        {
            if (!(field is GridField grid))
            {
                Double(ref h, field.TotalJoules);
                return;
            }

            Long(ref h, grid.CellCount);

            for (int iy = 0; iy < grid.CellsY; iy++)
            {
                for (int ix = 0; ix < grid.CellsX; ix++)
                {
                    for (int iz = 0; iz < grid.CellsZ; iz++)
                    {
                        Double(ref h, grid.JoulesAt(ix, iy, iz));
                    }
                }
            }

            Double(ref h, grid.TotalJoules);
        }

        private static void Double(ref ulong h, double v) => Long(ref h, BitConverter.DoubleToInt64Bits(v));

        private static void Float(ref ulong h, float v)
        {
            // No BitConverter.SingleToInt32Bits on netstandard2.1's oldest consumers; the
            // round-trip through a one-element array is exact and this is not a hot path.
            Long(ref h, BitConverter.ToInt32(BitConverter.GetBytes(v), 0));
        }

        private static void Long(ref ulong h, long v)
        {
            ulong u = unchecked((ulong)v);

            for (int i = 0; i < 8; i++)
            {
                h ^= (u >> (i * 8)) & 0xffUL;
                h *= 1099511628211UL;
            }
        }
    }
}
