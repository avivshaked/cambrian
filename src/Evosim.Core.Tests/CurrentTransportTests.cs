using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="CurrentMode.Transport"/>: the three-dimensional field the owner ruled for on
    /// 2026-09-10, held to the four properties that make it water rather than a pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Divergence-free, periodic on both rings, closed at the surface and the bed, and at the
    /// speed the knob says.</b> The first is what stops the field creating or destroying the stuff
    /// it carries; the second is what stops a seam in a world D077 gave no edges; the third is what
    /// stops the slow invisible lift that carried a whole population six metres into the air in
    /// 2,500 s (logbook/0022); the fourth is what makes the knob mean something a launcher can set.
    /// </para>
    /// <para>
    /// <b>And one property the rolls deliberately do not have</b>: a parcel is carried away rather
    /// than returned. D059's standing waves return every particle every cycle on purpose, which is
    /// what made round 33's clades columns around the spot their founder landed on (logbook/0083).
    /// The test below asks for the opposite and measures it.
    /// </para>
    /// </remarks>
    public class CurrentTransportTests
    {
        private readonly ITestOutputHelper _output;

        public CurrentTransportTests(ITestOutputHelper output) => _output = output;

        // The campaign's own box: 100 m2 over four patches is 20 m along x, 5 m across z, 60 m
        // deep. Reading a field's shape at the geometry the runs use rather than at a tidy cube,
        // because the anisotropy of a box three times deeper than it is long is a real property of
        // the water and not a rounding of it.
        private const float Width = 5f;
        private const int Patches = 4;
        private const float Length = Width * Patches;
        private const float Depth = 60f;
        private const float Speed = 0.3f;
        private const float Period = 600f;
        private const ulong Seed = 20260910UL;

        private static CurrentField Transport(float speed = Speed, ulong seed = Seed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(Width, Patches, Depth, seed);
            return field;
        }

        [Fact]
        public void TheFieldIsDivergenceFree()
        {
            CurrentField field = Transport();

            // A step small enough that the second-order truncation of a central difference is well
            // under the tolerance, and large enough that the float the sampler returns is not the
            // whole of the answer. At 5 cm the two are three orders apart.
            const double H = 0.05;

            var rng = new Rng(7UL);
            double worst = 0d;
            double worstGradient = 0d;

            for (int i = 0; i < 4000; i++)
            {
                // Interior only: outside the box the sampler reads the nearest face on purpose,
                // so the field is constant in y there and a difference across the waterline is
                // measuring the clamp rather than the water.
                double x = rng.NextFloat() * Length;
                double z = rng.NextFloat() * Width;
                double y = -1d - rng.NextFloat() * (Depth - 2d);
                double t = rng.NextFloat() * 4d * Period;

                double dudx = (Vx(field, x + H, y, z, t) - Vx(field, x - H, y, z, t)) / (2d * H);
                double dvdy = (Vy(field, x, y + H, z, t) - Vy(field, x, y - H, z, t)) / (2d * H);
                double dwdz = (Vz(field, x, y, z + H, t) - Vz(field, x, y, z - H, t)) / (2d * H);

                double divergence = Math.Abs(dudx + dvdy + dwdz);
                double gradient = Math.Abs(dudx) + Math.Abs(dvdy) + Math.Abs(dwdz);

                if (divergence > worst) worst = divergence;
                if (gradient > worstGradient) worstGradient = gradient;
            }

            _output.WriteLine(
                $"worst |div v| {worst:0.0000e+0} /s over 4,000 points, against gradients of up " +
                $"to {worstGradient:0.0000} /s");

            // Absolute, in inverse seconds, and three orders below the gradients the same points
            // carry. A field that was divergence-free only where a test happened to look would sit
            // at the size of one of those terms rather than at the size of the arithmetic.
            Assert.True(worst < 1e-3, $"worst divergence {worst:0.0000e+0} /s");
        }

        [Fact]
        public void TheFieldIsPeriodicOnBothRings()
        {
            CurrentField field = Transport();

            var rng = new Rng(11UL);
            double worst = 0d;

            for (int i = 0; i < 2000; i++)
            {
                float x = rng.NextFloat() * Length;
                float z = rng.NextFloat() * Width;
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * 4d * Period;

                Float3 here = field.VelocityAt(x, y, z, t);
                Float3 alongX = field.VelocityAt(x + Length, y, z, t);
                Float3 alongZ = field.VelocityAt(x, y, z + Width, t);
                Float3 back = field.VelocityAt(x - 3f * Length, y, z - 2f * Width, t);

                worst = Math.Max(worst, (here - alongX).Magnitude);
                worst = Math.Max(worst, (here - alongZ).Magnitude);
                worst = Math.Max(worst, (here - back).Magnitude);
            }

            _output.WriteLine($"worst wrap mismatch {worst:0.0000e+0} m/s at {Speed} m/s RMS");

            // D077 gave the world two rings and no edges. A field that did not close on them would
            // put a wall in the water at x = 0, which is exactly where every column of the grid's
            // seam sits.
            Assert.True(worst < 1e-5, $"worst mismatch {worst:0.0000e+0} m/s");
        }

        [Fact]
        public void TheVerticalVelocityIsExactlyZeroAtTheSurfaceAndTheFloor()
        {
            CurrentField field = Transport();

            var rng = new Rng(13UL);

            for (int i = 0; i < 500; i++)
            {
                float x = rng.NextFloat() * Length;
                float z = rng.NextFloat() * Width;
                double t = rng.NextFloat() * 10d * Period;

                // Exactly, not nearly. Math.Sin(-Math.PI) is -1.2e-16 and a vertical velocity of
                // 1.2e-16 at the waterline is still a velocity: integrated over a run it is the
                // one-directional lift of logbook/0022, which is invisible until the population is
                // above the water.
                Assert.Equal(0f, field.VelocityAt(x, 0f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Depth, z, t).Y);

                // And outside the box, where a body that overshot the surface or a parcel that
                // reached the bed reads the nearest face.
                Assert.Equal(0f, field.VelocityAt(x, 3f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Depth - 12f, z, t).Y);
            }
        }

        [Fact]
        public void TheRmsSpeedIsTheKnob()
        {
            foreach (float speed in new[] { 0.05f, 0.3f, 1.2f })
            {
                CurrentField field = Transport(speed);

                // A different lattice from the one the field measured itself on, and coprime with
                // it, so this is a second opinion rather than a restatement.
                const int Points = 17;
                const int Instants = 31;

                double sum = 0d;
                double sumX = 0d, sumY = 0d, sumZ = 0d;
                double fastest = 0d;
                int taken = 0;

                for (int it = 0; it < Instants; it++)
                {
                    double t = 13d * Period * it / Instants;

                    for (int ix = 0; ix < Points; ix++)
                    {
                        float x = (ix + 0.5f) * Length / Points;

                        for (int iy = 0; iy < Points; iy++)
                        {
                            float y = -((iy + 0.5f) * Depth / Points);

                            for (int iz = 0; iz < Points; iz++)
                            {
                                float z = (iz + 0.5f) * Width / Points;

                                Float3 v = field.VelocityAt(x, y, z, t);
                                sum += (double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z;
                                sumX += (double)v.X * v.X;
                                sumY += (double)v.Y * v.Y;
                                sumZ += (double)v.Z * v.Z;
                                if (v.Magnitude > fastest) fastest = v.Magnitude;
                                taken++;
                            }
                        }
                    }
                }

                double rms = Math.Sqrt(sum / taken);

                _output.WriteLine(
                    $"knob {speed} m/s: RMS {rms:0.0000} m/s over {taken:n0} samples, " +
                    $"fastest sample {fastest:0.0000} m/s ({fastest / rms:0.00}x the RMS), " +
                    $"bound {field.MaximumTransportSpeed:0.0000} m/s " +
                    $"({field.MaximumTransportSpeed / fastest:0.00}x the fastest sample); " +
                    $"per axis x {Math.Sqrt(sumX / taken):0.0000}, y {Math.Sqrt(sumY / taken):0.0000}, " +
                    $"z {Math.Sqrt(sumZ / taken):0.0000} m/s");

                Assert.Equal((double)speed, rms, 0.05 * speed);

                // The owner's ruling of 2026-09-10: "up and down, left and right, in all
                // directions really". The first build read x 0.027, y 0.297, z 0.027 at this knob,
                // which is a lift and a fall and almost nothing sideways, because a mode with
                // wavelengths of the order of the box on every axis is a tall thin eddy. The
                // vertical mode number is chosen per mode to make each eddy round instead, and
                // this is the check that it worked.
                double rmsX = Math.Sqrt(sumX / taken);
                double rmsY = Math.Sqrt(sumY / taken);
                double rmsZ = Math.Sqrt(sumZ / taken);

                double most = Math.Max(rmsX, Math.Max(rmsY, rmsZ));
                double least = Math.Min(rmsX, Math.Min(rmsY, rmsZ));

                Assert.True(
                    most < 1.5 * least,
                    $"the axes are {most / least:0.00}x apart: x {rmsX:0.0000}, y {rmsY:0.0000}, z {rmsZ:0.0000} m/s");

                // The Courant check GridField.Advect makes is against this, and it has to be a
                // ceiling: a bound below the true maximum would pass a step the fastest water
                // crosses more than half a cell in.
                Assert.True(field.MaximumTransportSpeed > rms, "the bound is under the RMS");
            }
        }

        [Fact]
        public void AParcelIsCarriedAwayRatherThanReturned()
        {
            CurrentField field = Transport();

            const double Step = 0.5;
            const double Cell = 1.0;

            var rng = new Rng(17UL);
            double smallest = double.MaxValue;
            double meanY = 0d;
            double meanSquareY = 0d;
            int parcels = 0;

            for (int i = 0; i < 64; i++)
            {
                double x0 = rng.NextFloat() * Length;
                double z0 = rng.NextFloat() * Width;
                double y0 = -2d - rng.NextFloat() * (Depth - 4d);

                (double x, double y, double z) = Carry(field, x0, y0, z0, Period, Step);

                double moved = Math.Sqrt(
                    (x - x0) * (x - x0) + (y - y0) * (y - y0) + (z - z0) * (z - z0));

                if (moved < smallest) smallest = moved;

                meanY += y - y0;
                meanSquareY += (y - y0) * (y - y0);
                parcels++;
            }

            meanY /= parcels;
            double rmsY = Math.Sqrt(meanSquareY / parcels);

            _output.WriteLine(
                $"64 parcels over one {Period} s period: the least-moved travelled " +
                $"{smallest:0.00} m; mean vertical displacement {meanY:0.000} m against an RMS of " +
                $"{rmsY:0.000} m");

            // The rolls return a parcel to where they found it by design, which is what made a
            // clade a column (logbook/0083). This field must not.
            Assert.True(smallest > Cell, $"a parcel came back to within {smallest:0.00} m");

            // And it must not be a conveyor either, which is the opposite failure and the one that
            // put a whole population above the waterline (logbook/0022). A mean displacement well
            // under the spread is dispersion; a mean of the order of the spread is a tow.
            Assert.True(
                Math.Abs(meanY) < 0.5 * rmsY,
                $"mean vertical displacement {meanY:0.000} m against an RMS of {rmsY:0.000} m");
        }

        [Fact]
        public void TwoSeedsGetDifferentWater()
        {
            CurrentField one = Transport(seed: 1UL);
            CurrentField two = Transport(seed: 2UL);

            var rng = new Rng(19UL);
            double worst = 0d;

            for (int i = 0; i < 500; i++)
            {
                float x = rng.NextFloat() * Length;
                float z = rng.NextFloat() * Width;
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * Period;

                worst = Math.Max(
                    worst, (one.VelocityAt(x, y, z, t) - two.VelocityAt(x, y, z, t)).Magnitude);
            }

            _output.WriteLine($"seeds 1 and 2 differ by up to {worst:0.000} m/s at {Speed} m/s RMS");

            // Otherwise five seeds of a round would be five draws of the genome and one draw of the
            // water, which is not five replicates.
            Assert.True(worst > 0.1 * Speed, "two seeds got the same field");
        }

        [Fact]
        public void TheSameSeedGetsTheSameWater()
        {
            CurrentField one = Transport(seed: 4242UL);
            CurrentField two = Transport(seed: 4242UL);

            var rng = new Rng(23UL);

            for (int i = 0; i < 500; i++)
            {
                float x = rng.NextFloat() * Length;
                float z = rng.NextFloat() * Width;
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * Period;

                // §7: a seed means a sequence, and it has to mean a field too.
                Assert.Equal(one.VelocityAt(x, y, z, t), two.VelocityAt(x, y, z, t));
            }
        }

        [Fact]
        public void ATransportFieldWithNoBoxRefusesRatherThanGuessing()
        {
            var field = new CurrentField { Mode = CurrentMode.Transport, Speed = Speed };

            // Loading refuses rather than defaults, and so does this: a box guessed from nothing
            // would put the floor somewhere the world's floor is not, and the vertical velocity
            // would not vanish at the bed that actually exists.
            Assert.Throws<InvalidOperationException>(() => field.VelocityAt(0f, -5f, 0f, 3d));
        }

        [Fact]
        public void TheDefaultIsTheRollsAndTheyAreUntouched()
        {
            Assert.Equal(CurrentMode.Rolls, new CurrentField().Mode);
            Assert.Equal(CurrentMode.Rolls, new RunConfig().Current.Mode);

            var rolling = new CurrentField
            {
                Speed = 0.3f, CellMetres = 30f, PeriodSeconds = 600f, Rolls = true,
            };

            var same = new CurrentField
            {
                Speed = 0.3f, CellMetres = 30f, PeriodSeconds = 600f, Rolls = true,
            };

            // SetBox does everything SetPatchWidth did and hands the field three numbers the rolls
            // never read, so a rolls world through the new entry point is the old one to the bit.
            same.SetBox(Width, Patches, Depth, 99UL);
            rolling.SetPatchWidth(Width);

            for (int patch = 0; patch < Patches; patch++)
            {
                for (double t = 0d; t < 1200d; t += 7d)
                {
                    for (float y = 0f; y >= -Depth; y -= 1.5f)
                    {
                        Assert.Equal(
                            rolling.VelocityAt(y, t, patch, Patches),
                            same.VelocityAt(y, t, patch, Patches));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ the grid under it

        // GridFieldTests' own box, which 3 m cells divide on all three axes: 144 m2 over four
        // patches is 24 m by 6 m, 24 m deep. Small enough that a few thousand advection steps run
        // in seconds and every one of them samples the field per cell.
        private const float GridArea = 144f;
        private const float GridWidth = 6f;
        private const float GridDepth = 24f;

        [Fact]
        public void TheGridConservesUnderTransport()
        {
            const float Cell = 3f;
            const float Dt = 0.5f;
            const int Steps = 4000;

            var field = new GridField(GridArea, 0f, GridDepth, 0f, 0f, Patches, Cell);

            var current = new CurrentField
            {
                Mode = CurrentMode.Transport, Speed = 0.3f, PeriodSeconds = 600f, AdvectFields = true,
            };

            current.SetBox(GridWidth, Patches, GridDepth, Seed);

            // A lump in one cell and a sheet across a layer, so the walk has both a point source
            // to smear and a plane to tilt.
            field.Deposit(new FieldPoint(new Float3(10.5f, -10.5f, 1.5f), 1), 500f);
            for (int ix = 0; ix < field.CellsX; ix++)
            {
                for (int iz = 0; iz < field.CellsZ; iz++)
                {
                    field.Deposit(
                        new FieldPoint(
                            new Float3((ix + 0.5f) * Cell, -4.5f, (iz + 0.5f) * Cell),
                            (int)((ix + 0.5f) * Cell / GridWidth) % Patches),
                        3f);
                }
            }

            double before = field.Recount();

            for (int step = 0; step < Steps; step++)
            {
                field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
            }

            double after = field.Recount();

            double least = double.MaxValue;
            int occupied = 0;
            for (int ix = 0; ix < field.CellsX; ix++)
            {
                for (int iy = 0; iy < field.CellsY; iy++)
                {
                    for (int iz = 0; iz < field.CellsZ; iz++)
                    {
                        double joules = field.JoulesAt(ix, iy, iz);
                        if (joules < least) least = joules;
                        if (joules > 1e-9) occupied++;
                    }
                }
            }

            _output.WriteLine(
                $"{Steps:n0} steps of {Dt} s on {field.CellCount} cells: {before:0.000000} J -> " +
                $"{after:0.000000} J, drift {after - before:0.0000e+0} J; least cell {least:0.0000e+0} J; " +
                $"{occupied} of {field.CellCount} cells hold something");

            // To the last joule, on every operator, as the cell field and the vertex field are.
            Assert.Equal(before, after, 9);

            // Upwind takes from the upstream cell and gives all of it to the downstream one, so
            // nothing can go negative whatever the Courant number. A negative cell would be the
            // clamp having failed.
            Assert.True(least >= 0d, $"a cell went to {least:0.0000e+0} J");

            // And the whole point: a lump and a sheet in a three-dimensional field spread through
            // the box rather than sitting in the column they were put in.
            Assert.True(occupied > field.CellCount / 2, $"only {occupied} cells hold anything");
        }

        [Fact]
        public void TheGridConservesWhenTheStepHasToBeSplit()
        {
            const float Cell = 3f;
            const float Dt = 0.5f;
            const int Steps = 2000;

            var field = new GridField(GridArea, 0f, GridDepth, 0f, 0f, Patches, Cell);

            // Fast enough that one step would carry the fastest water more than half a cell, which
            // is the condition Advect answers by splitting the step rather than by refusing it.
            var current = new CurrentField
            {
                Mode = CurrentMode.Transport, Speed = 1f, PeriodSeconds = 600f, AdvectFields = true,
            };

            current.SetBox(GridWidth, Patches, GridDepth, Seed);

            double courant = current.MaximumTransportSpeed * Dt / Cell;
            int substeps = (int)Math.Ceiling(2d * courant);

            _output.WriteLine(
                $"1 m/s RMS bounds at {current.MaximumTransportSpeed:0.000} m/s: Courant " +
                $"{courant:0.000} on a {Cell} m cell in {Dt} s, so {substeps} substeps of " +
                $"{Dt / substeps:0.000} s");

            Assert.InRange(substeps, 2, 3);

            field.Deposit(new FieldPoint(new Float3(10.5f, -10.5f, 1.5f), 1), 500f);
            double before = field.Recount();

            double least = double.MaxValue;
            for (int step = 0; step < Steps; step++)
            {
                field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
            }

            for (int ix = 0; ix < field.CellsX; ix++)
            {
                for (int iy = 0; iy < field.CellsY; iy++)
                {
                    for (int iz = 0; iz < field.CellsZ; iz++)
                    {
                        double joules = field.JoulesAt(ix, iy, iz);
                        if (joules < least) least = joules;
                    }
                }
            }

            double after = field.Recount();

            _output.WriteLine(
                $"{Steps:n0} split steps: {before:0.000000} J -> {after:0.000000} J, drift " +
                $"{after - before:0.0000e+0} J; least cell {least:0.0000e+0} J");

            // A substep is a shorter step, so the books close on each one exactly as they do on a
            // whole step, and the total cannot depend on how many there were.
            Assert.Equal(before, after, 9);
            Assert.True(least >= 0d, $"a cell went to {least:0.0000e+0} J");
        }

        [Fact]
        public void TheGridRefusesAStepTheFastestWaterOutrunsRatherThanClampingIt()
        {
            const float Cell = 3f;

            var field = new GridField(GridArea, 0f, GridDepth, 0f, 0f, Patches, Cell);

            var current = new CurrentField
            {
                Mode = CurrentMode.Transport, Speed = 20f, PeriodSeconds = 600f, AdvectFields = true,
            };

            current.SetBox(GridWidth, Patches, GridDepth, Seed);

            _output.WriteLine(
                $"20 m/s RMS bounds at {current.MaximumTransportSpeed:0.00} m/s, which crosses " +
                $"{current.MaximumTransportSpeed * 5f / Cell:0.00} of a {Cell} m cell in 5 s, " +
                $"{(int)Math.Ceiling(2d * current.MaximumTransportSpeed * 5f / Cell)} substeps " +
                $"against a ceiling of {GridField.MaximumSubsteps}");

            // Splitting the step answers a Courant number a little over a half. It does not answer
            // a config asking for water faster than the grid is a description of: past the ceiling
            // the refusal names the numbers so the fix is arithmetic, rather than the run quietly
            // costing sixteen times what its launcher implies.
            ArgumentException thrown = Assert.Throws<ArgumentException>(
                () => field.Advect(current, 0d, 5f, field.PatchWidthMetres));

            Assert.Contains("Courant", thrown.Message);
        }

        // ------------------------------------------------------------------ helpers

        private static double Vx(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).X;

        private static double Vy(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Y;

        private static double Vz(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Z;

        /// <summary>
        /// Where the water takes a parcel released at a place, integrated with the midpoint rule.
        /// </summary>
        /// <remarks>
        /// Midpoint rather than forward Euler, for <see cref="CurrentField.DriftOf"/>'s reason: on
        /// an oscillating field Euler accumulates a bias of its own, and a displacement measured
        /// with it would be measuring the integrator.
        /// </remarks>
        private static (double X, double Y, double Z) Carry(
            CurrentField field, double x, double y, double z, double seconds, double step)
        {
            for (double t = 0d; t < seconds; t += step)
            {
                Float3 v = field.VelocityAt((float)x, (float)y, (float)z, t);

                double hx = x + 0.5 * step * v.X;
                double hy = y + 0.5 * step * v.Y;
                double hz = z + 0.5 * step * v.Z;

                Float3 mid = field.VelocityAt((float)hx, (float)hy, (float)hz, t + 0.5 * step);

                x += step * mid.X;
                y += step * mid.Y;
                z += step * mid.Z;
            }

            return (x, y, z);
        }
    }
}
