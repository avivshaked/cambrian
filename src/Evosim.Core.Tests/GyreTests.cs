using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The tank's water — <c>logbook/specs/tank-spec.md</c>, <c>fable-propose-aquarium.md</c>
    /// ruling 1: a gyre about the axis, held to the properties that make it water in a walled
    /// cylinder rather than a pattern drawn inside one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four properties and a reading.</b> Divergence-free, so it neither creates nor destroys
    /// the stock it carries; no radial flow at the glass, so nothing piles against the wall or is
    /// asked to pass through it; no vertical flow at the surface or the bed, so the lift of
    /// logbook/0022 cannot recur; and the RMS at the knob, so a launcher's number means what it
    /// says. The reading is the dead-pocket fraction — the share of live cells where the water is
    /// under a tenth of its own RMS — which is the proposal's check 2 and decides whether a body
    /// can find still water to sit in.
    /// </para>
    /// <para>
    /// <b>Measured on a lattice of this test's own</b>, coprime with the one the field balances
    /// itself on, so these are second opinions rather than restatements.
    /// </para>
    /// </remarks>
    public class GyreTests
    {
        private readonly ITestOutputHelper _output;

        public GyreTests(ITestOutputHelper output) => _output = output;

        // The aquarium's first round: 100 m2 of footprint, which is a radius of 5.642 m, 60 m
        // deep, four rings of equal area. Read at the geometry the runs use rather than at a tidy
        // cylinder, because a tank ten times deeper than it is wide is a real property of this
        // water and not a rounding of it.
        private const float Area = 100f;
        private const int Rings = 4;
        private const float Depth = 60f;
        private const float Speed = 0.3f;
        private const float Period = 6000f;
        private const ulong Seed = 20260911UL;

        private static float Radius => TankGeometry.RadiusFor(Area);

        private static CurrentField Gyre(float speed = Speed, ulong seed = Seed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(Area / Rings), Rings, Depth, seed,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius);

            return field;
        }

        /// <summary>A point inside the circle, uniform in area, at an interior depth.</summary>
        private static (float X, float Y, float Z) Interior(Rng rng, double inset = 0d)
        {
            float r = (float)((Radius - inset) * Math.Sqrt(rng.NextFloat()));
            double theta = 2d * Math.PI * rng.NextFloat();

            return (
                (float)(Radius + r * Math.Cos(theta)),
                -1f - rng.NextFloat() * (Depth - 2f),
                (float)(Radius + r * Math.Sin(theta)));
        }

        [Fact]
        public void TheGyreIsDivergenceFree()
        {
            CurrentField field = Gyre();

            // A step small enough that a central difference's truncation is well under the
            // tolerance and large enough that the float the sampler returns is not the whole of the
            // answer, and set in far enough from the glass that the stencil stays in the water:
            // outside the wall the field reads the water at the wall on purpose, and a difference
            // across that clamp measures the clamp.
            const double H = 0.05;

            var rng = new Rng(7UL);
            double worst = 0d;
            double worstGradient = 0d;
            double rms = RmsOf(field);

            for (int i = 0; i < 1000; i++)
            {
                (float x, float y, float z) = Interior(rng, inset: 4d * H);
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
                $"worst |div v| {worst:0.0000e+0} /s over 1,000 points, against gradients of up " +
                $"to {worstGradient:0.0000} /s and an RMS of {rms:0.0000} m/s");

            // The spec's bound: under 1e-3 of the RMS per metre. A field that was divergence-free
            // only where a test happened to look would sit at the size of one of those gradients
            // rather than at the size of the arithmetic.
            Assert.True(worst < 1e-3 * rms, $"worst divergence {worst:0.0000e+0} /s");
        }

        [Fact]
        public void NothingFlowsThroughTheGlass()
        {
            CurrentField field = Gyre();

            var rng = new Rng(11UL);
            double worst = 0d;

            for (int i = 0; i < 200; i++)
            {
                double theta = 2d * Math.PI * rng.NextFloat();
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * 4d * Period;

                float x = (float)(Radius + Radius * Math.Cos(theta));
                float z = (float)(Radius + Radius * Math.Sin(theta));

                Float3 v = field.VelocityAt(x, y, z, t);

                // The radial component: the wall's own normal. The azimuthal and vertical ones are
                // free to be anything, because the glass is a vertical cylinder and water sliding
                // along it is water in a tank.
                double radial = v.X * Math.Cos(theta) + v.Z * Math.Sin(theta);
                worst = Math.Max(worst, Math.Abs(radial));
            }

            _output.WriteLine($"worst radial velocity at the glass {worst:0.0000e+0} m/s");

            Assert.True(worst < 1e-6, $"worst radial velocity {worst:0.0000e+0} m/s");
        }

        [Fact]
        public void NothingFlowsThroughTheSurfaceOrTheBed()
        {
            CurrentField field = Gyre();

            var rng = new Rng(13UL);

            for (int i = 0; i < 200; i++)
            {
                (float x, _, float z) = Interior(rng);
                double t = rng.NextFloat() * 10d * Period;

                // Exactly, not nearly: Math.Sin(-Math.PI) is -1.2e-16 and a vertical velocity of
                // 1.2e-16 at the waterline is still a velocity, integrated over a run into the
                // lift that put a whole population six metres into the air (logbook/0022).
                Assert.Equal(0f, field.VelocityAt(x, 0f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Depth, z, t).Y);

                // And outside the water, where a body that overshot the surface or a parcel at the
                // bed reads the nearest face.
                Assert.Equal(0f, field.VelocityAt(x, 3f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Depth - 12f, z, t).Y);
            }
        }

        [Fact]
        public void TheRmsSpeedIsTheKnobAndTheAxesAreBalanced()
        {
            foreach (float speed in new[] { 0.05f, 0.3f, 1.2f })
            {
                CurrentField field = Gyre(speed);

                (double rms, double rmsX, double rmsY, double rmsZ, double fastest, double dead) =
                    Measure(field);

                (float swirl, float overturning, float eddies) = field.GyreComponentRms;

                _output.WriteLine(
                    $"knob {speed} m/s: RMS {rms:0.0000} m/s, fastest sample {fastest:0.0000} m/s " +
                    $"({fastest / rms:0.00}x the RMS), bound {field.MaximumTransportSpeed:0.0000} m/s " +
                    $"({field.MaximumTransportSpeed / fastest:0.00}x the fastest sample); " +
                    $"per axis x {rmsX:0.0000}, y {rmsY:0.0000}, z {rmsZ:0.0000} m/s; " +
                    $"parts swirl {swirl:0.0000}, overturning {overturning:0.0000}, " +
                    $"eddies {eddies:0.0000} m/s; dead pockets {dead:0.000}");

                // The spec's 1%, which is tighter than the transport field's 5% because this scale
                // is measured rather than derived, and a measurement that disagreed with a second
                // lattice by more than that would mean the first lattice was too coarse for the
                // shapes it was measuring.
                Assert.Equal((double)speed, rms, 0.01 * speed);

                // The owner's ruling of 2026-09-10 applied to the tank's water: up and down, left
                // and right, in all directions really.
                double most = Math.Max(rmsX, Math.Max(rmsY, rmsZ));
                double least = Math.Min(rmsX, Math.Min(rmsY, rmsZ));

                Assert.True(
                    most < 1.2 * least,
                    $"the axes are {most / least:0.00}x apart: x {rmsX:0.0000}, y {rmsY:0.0000}, z {rmsZ:0.0000} m/s");

                // The Courant check GridField.Advect makes is against this, and it has to be a
                // ceiling: a bound under the true maximum would pass a step the fastest water
                // crosses more than half a cell in.
                Assert.True(
                    field.MaximumTransportSpeed > fastest,
                    $"the bound {field.MaximumTransportSpeed} m/s is under the fastest sample {fastest} m/s");
            }
        }

        [Fact]
        public void TheDeadPocketFractionIsReported()
        {
            CurrentField field = Gyre();

            (double rms, _, _, _, double fastest, double dead) = Measure(field);

            _output.WriteLine(
                $"dead pockets: {dead:0.000} of the live volume moves slower than a tenth of the " +
                $"RMS ({0.1 * rms:0.0000} m/s of {rms:0.0000}), fastest {fastest:0.0000} m/s " +
                $"— the proposal's check 2, at {Area} m2 and {Depth} m over {Rings} rings.");

            // Not an assertion about the number, which is the owner's to read: an assertion that
            // the field is not mostly still, which would mean the tank had a current in a ring
            // round its rim and nothing anywhere else.
            Assert.True(dead < 0.5, $"{dead:0.000} of the tank is dead water");
        }

        [Fact]
        public void TwoSeedsAreTwoDrawsOfTheWaterAndOneSeedReplays()
        {
            CurrentField first = Gyre(seed: 1UL);
            CurrentField second = Gyre(seed: 2UL);
            CurrentField again = Gyre(seed: 1UL);

            var rng = new Rng(19UL);
            double apart = 0d;
            double same = 0d;

            for (int i = 0; i < 500; i++)
            {
                (float x, float y, float z) = Interior(rng);
                double t = rng.NextFloat() * 4d * Period;

                apart = Math.Max(apart, (first.VelocityAt(x, y, z, t) - second.VelocityAt(x, y, z, t)).Magnitude);
                same = Math.Max(same, (first.VelocityAt(x, y, z, t) - again.VelocityAt(x, y, z, t)).Magnitude);
            }

            _output.WriteLine($"two seeds differ by up to {apart:0.0000} m/s; one seed repeats to {same:0.0000e+0} m/s");

            // Five seeds of a round have to be five draws of the water as well as of the genome,
            // or they are not replicates (SetBox's remarks).
            Assert.True(apart > 0.01 * Speed, "two seeds drew the same water");
            Assert.Equal(0d, same);
        }

        private static double RmsOf(CurrentField field) => Measure(field).Rms;

        /// <summary>
        /// The field's RMS, its per-axis RMS, its fastest sample and its dead-pocket fraction, on a
        /// lattice of this test's own.
        /// </summary>
        /// <remarks>
        /// Coprime with the field's construction lattice — 21 and 19 against its 28 and 24 — and at
        /// different phases, so no sample lands where the field measured itself. A test that
        /// re-took the same points would confirm the arithmetic and nothing else.
        /// </remarks>
        private static (double Rms, double X, double Y, double Z, double Fastest, double Dead) Measure(
            CurrentField field)
        {
            const int Horizontal = 21;
            const int Vertical = 19;
            const int Instants = 11;

            double sumX = 0d, sumY = 0d, sumZ = 0d, fastest = 0d;
            int taken = 0;
            int still = 0;

            var speeds = new double[Horizontal * Horizontal * Vertical * Instants];

            for (int it = 0; it < Instants; it++)
            {
                double t = 13d * Period * it / Instants;

                for (int iy = 0; iy < Vertical; iy++)
                {
                    float y = -(float)((iy + 0.5) * Depth / Vertical);

                    for (int ix = 0; ix < Horizontal; ix++)
                    {
                        float x = (float)((ix + 0.5) * 2d * Radius / Horizontal);

                        for (int iz = 0; iz < Horizontal; iz++)
                        {
                            float z = (float)((iz + 0.5) * 2d * Radius / Horizontal);
                            if (!TankGeometry.Inside(x, z, Radius)) continue;

                            Float3 v = field.VelocityAt(x, y, z, t);

                            sumX += (double)v.X * v.X;
                            sumY += (double)v.Y * v.Y;
                            sumZ += (double)v.Z * v.Z;

                            double speed = v.Magnitude;
                            speeds[taken] = speed;
                            if (speed > fastest) fastest = speed;

                            taken++;
                        }
                    }
                }
            }

            double rms = Math.Sqrt((sumX + sumY + sumZ) / taken);

            for (int i = 0; i < taken; i++)
            {
                if (speeds[i] < 0.1 * rms) still++;
            }

            return (
                rms,
                Math.Sqrt(sumX / taken),
                Math.Sqrt(sumY / taken),
                Math.Sqrt(sumZ / taken),
                fastest,
                (double)still / taken);
        }

        private static double Vx(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).X;

        private static double Vy(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Y;

        private static double Vz(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Z;
    }
}
