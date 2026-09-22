using System;
using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>Where the population stands on the floor plan — <c>Ecosystem.HorizontalSpread</c>.</summary>
    public struct HorizontalSpread
    {
        /// <summary>Living bodies with a place to report.</summary>
        public int Bodies;

        /// <summary>Occupied 1 m columns of the footprint.</summary>
        public int OccupiedColumns;

        /// <summary>The same count over the stomachs alone.</summary>
        public int OccupiedColumnsAbsorptive;

        /// <summary>The footprint's own columns — 0 in a tiled world, where the instrument is off.</summary>
        public int TotalColumns;

        public double XSpreadMetres;
        public double ZSpreadMetres;
    }

    /// <summary>
    /// The horizontal spread instrument — <c>Ecosystem.MeasureHorizontalSpread</c>, out of Unity.
    /// </summary>
    /// <remarks>
    /// <b>At the sample cadence, off the physics path.</b> One dictionary lookup per living
    /// creature against a root the divergence check already read this metabolic step, so the
    /// instrument makes no reading of its own. A creature conceived during this step has no body
    /// yet and is skipped; so is one whose root is not finite, which is a diverged body about to
    /// be killed rather than a position.
    /// </remarks>
    public sealed partial class Simulation
    {
        /// <param name="absorptive">
        /// Ids of the living that carry absorptive tissue, or null for none. Passed in rather
        /// than walked here, so that <c>cols abs</c> and <c>absorpt</c> in the same row cannot
        /// come from two different definitions of a stomach.
        /// </param>
        public HorizontalSpread MeasureHorizontalSpread(HashSet<long> absorptive)
        {
            var reading = new HorizontalSpread();
            if (Volume == null) return reading;

            bool tank = Volume.Shape == WorldShape.Tank;
            float length = Volume.LengthMetres;
            float width = Volume.WidthMetres;

            // Ceiling, not rounding, so a box whose side is not a whole number of metres still
            // has a column for every point in it; the last one on each axis is then a part
            // column, and the clamp below keeps a body exactly on the far face inside the array.
            int nx = Math.Max(1, (int)Math.Ceiling(length / ColumnMetres));
            int nz = Math.Max(1, (int)Math.Ceiling(width / ColumnMetres));
            int total = nx * nz;

            if (_columnHeld == null || _columnHeld.Length != total)
            {
                _columnHeld = new bool[total];
                _columnHeldAbsorptive = new bool[total];
            }
            else
            {
                Array.Clear(_columnHeld, 0, total);
                Array.Clear(_columnHeldAbsorptive, 0, total);
            }

            // The denominator is the footprint the population could be standing on, so in a tank
            // it is the columns whose own centres are in the water — the same test the grid's
            // mask makes, at the instrument's own 1 m scale. Counting the bounding square instead
            // would report a world packed into 79% of the columns as packed into 100% of them.
            int live = total;

            if (tank)
            {
                live = 0;
                for (int ix = 0; ix < nx; ix++)
                {
                    for (int iz = 0; iz < nz; iz++)
                    {
                        if (TankGeometry.Inside(
                            (ix + 0.5f) * ColumnMetres, (iz + 0.5f) * ColumnMetres,
                            Volume.TankRadiusMetres))
                        {
                            live++;
                        }
                    }
                }
            }

            double sinX = 0d, cosX = 0d, sinZ = 0d, cosZ = 0d;
            double sumX = 0d, sumXX = 0d, sumZ = 0d, sumZZ = 0d;

            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;

                Float3 root = body.LastRootPosition;
                float horizontal = root.X + root.Z;
                if (float.IsNaN(horizontal) || float.IsInfinity(horizontal)) continue;

                reading.Bodies++;

                int ix = Clamp((int)Math.Floor(root.X / ColumnMetres), 0, nx - 1);
                int iz = Clamp((int)Math.Floor(root.Z / ColumnMetres), 0, nz - 1);
                int column = iz * nx + ix;

                // The numerator has to count the same columns the denominator does. A body a few
                // centimetres inside the glass stands in a column whose centre is outside it, and
                // round 37 printed `cols 104/100` for exactly that. The body is still a body: it
                // counts in `bodies` and in the sums below. What it does not get is a column of a
                // footprint it is not standing on.
                bool inTheFootprint = !tank || TankGeometry.Inside(
                    (ix + 0.5f) * ColumnMetres, (iz + 0.5f) * ColumnMetres, Volume.TankRadiusMetres);

                if (inTheFootprint && !_columnHeld[column])
                {
                    _columnHeld[column] = true;
                    reading.OccupiedColumns++;
                }

                if (inTheFootprint && absorptive != null && absorptive.Contains(creature.Id) &&
                    !_columnHeldAbsorptive[column])
                {
                    _columnHeldAbsorptive[column] = true;
                    reading.OccupiedColumnsAbsorptive++;
                }

                // A tank has a wall where the box has a seam, so the plain deviation is the
                // honest one there; the circular statistic exists because a periodic box makes
                // x = 0.1 and x = L − 0.1 neighbours.
                if (tank)
                {
                    sumX += root.X;
                    sumXX += (double)root.X * root.X;
                    sumZ += root.Z;
                    sumZZ += (double)root.Z * root.Z;
                    continue;
                }

                double angleX = 2d * Math.PI * root.X / length;
                double angleZ = 2d * Math.PI * root.Z / width;

                sinX += Math.Sin(angleX);
                cosX += Math.Cos(angleX);
                sinZ += Math.Sin(angleZ);
                cosZ += Math.Cos(angleZ);
            }

            reading.TotalColumns = live;
            reading.XSpreadMetres = tank
                ? PlainSpread(sumX, sumXX, reading.Bodies)
                : CircularSpread(sinX, cosX, reading.Bodies, length);
            reading.ZSpreadMetres = tank
                ? PlainSpread(sumZ, sumZZ, reading.Bodies)
                : CircularSpread(sinZ, cosZ, reading.Bodies, width);

            return reading;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>
        /// Mardia's circular standard deviation, in metres on an axis of <paramref name="extent"/>.
        /// </summary>
        /// <remarks>
        /// sqrt(−2 ln R), where R is the length of the mean unit vector, converted from radians by
        /// extent / 2pi. It is unbounded as R goes to zero, which is a population spread perfectly
        /// evenly round the ring, so it is capped at one whole extent.
        /// </remarks>
        private static double CircularSpread(double sinSum, double cosSum, int count, double extent)
        {
            if (count < 2 || extent <= 0d) return 0d;

            double r = Math.Sqrt(sinSum * sinSum + cosSum * cosSum) / count;
            if (r <= 0d) return extent;

            double radians = Math.Sqrt(Math.Max(0d, -2d * Math.Log(Math.Min(1d, r))));

            return Math.Min(extent, radians * extent / (2d * Math.PI));
        }

        /// <summary>
        /// The ordinary standard deviation, in metres — the tank's, where there is no seam.
        /// </summary>
        /// <remarks>
        /// The population form rather than the sample one, so that it is comparable with
        /// <c>depth sd</c> beside it; and floored at 0 before the root, because the
        /// sum-of-squares form can go a few ulp negative on a population standing in one spot,
        /// which is the one arrangement this column exists to show.
        /// </remarks>
        private static double PlainSpread(double sum, double sumOfSquares, int count)
        {
            if (count < 2) return 0d;

            double mean = sum / count;
            double variance = sumOfSquares / count - mean * mean;

            return variance > 0d ? Math.Sqrt(variance) : 0d;
        }
    }
}
