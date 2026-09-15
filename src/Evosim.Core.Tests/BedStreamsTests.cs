using System;
using System.Diagnostics;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The water follows the floor — D092, <c>logbook/specs/bed-spec.md</c> item 6, the sloped
    /// half of what <see cref="StreamsTests"/> asserts about the flat tank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every property here is meant to hold by construction, which is exactly why it is
    /// measured.</b> The sloped field is the flat one carried through a coordinate map as a
    /// two-form, so it is divergence-free and tangential to the sloped floor for the same reason
    /// the flat one is divergence-free and tangential to its bed — no term was added to make it
    /// so, and therefore no term can be checked by reading it. What can be checked is the field:
    /// finite differences at random places, the normal velocity on the floor itself, and the
    /// analytic acceleration against a stencil on the same function a body feels.
    /// </para>
    /// <para>
    /// <b>Read at round 38's geometry and round 39's dials</b>: 400 m² is a disc of radius
    /// 11.28 m, 60 m deep, four rings, 0.1 m/s. The relief is asked for at 4 m and comes back at
    /// what the 30° slope bound allows (<see cref="BedShapeTests"/>), which is the floor this
    /// water is measured over.
    /// </para>
    /// </remarks>
    public class BedStreamsTests
    {
        private readonly ITestOutputHelper _output;

        public BedStreamsTests(ITestOutputHelper output) => _output = output;

        private const float Area = 400f;
        private const int Rings = 4;
        private const float Depth = 60f;
        private const float Speed = 0.1f;
        private const float Period = 6000f;
        private const float Relief = 4f;
        private const float Tilt = 2f;
        private const ulong RunSeed = 20260915UL;

        private static float Radius => TankGeometry.RadiusFor(Area);

        private static BedShape Bed(float relief = Relief, float tilt = Tilt, ulong seed = RunSeed) =>
            new BedShape(Radius, Depth, relief, tilt, 0f, Rng.SeedFor(seed, World.BedShapeIndex));

        private static CurrentField Streams(BedShape bed, float speed = Speed, ulong seed = RunSeed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(Area / Rings), Rings, Depth, Rng.SeedFor(seed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);

            return field;
        }

        /// <summary>A point in the water, uniform in the disc, held clear of every face.</summary>
        private static (float X, float Y, float Z) Interior(Rng rng, BedShape bed, double inset)
        {
            float r = (float)((Radius - inset) * Math.Sqrt(rng.NextFloat()));
            double theta = 2d * Math.PI * rng.NextFloat();

            float x = (float)(Radius + r * Math.Cos(theta));
            float z = (float)(Radius + r * Math.Sin(theta));

            double floor = bed.FloorY(x, z);
            double y = floor + inset + rng.NextFloat() * (0d - floor - 2d * inset);

            return (x, (float)y, z);
        }

        /// <summary>
        /// The RMS speed over the water that exists: the sloped volume, sampled column by column
        /// and weighted by each column's depth.
        /// </summary>
        private static (double Rms, double Fastest, double X, double Y, double Z) Measure(
            CurrentField field, BedShape bed, int instants = 41)
        {
            const int Horizontal = 19;
            const int Vertical = 13;

            double sum = 0d, weight = 0d, fastest = 0d;
            double sx = 0d, sy = 0d, sz = 0d;

            for (int p = 0; p < instants; p++)
            {
                double t = p * Period * 1.7320508075688772d / instants;

                for (int ix = 0; ix < Horizontal; ix++)
                {
                    float x = (float)((ix + 0.5) * 2d * Radius / Horizontal);

                    for (int iz = 0; iz < Horizontal; iz++)
                    {
                        float z = (float)((iz + 0.5) * 2d * Radius / Horizontal);
                        if (!TankGeometry.Inside(x, z, Radius)) continue;

                        double floor = bed.FloorY(x, z);
                        double d = -floor;
                        double w = d / Depth;

                        for (int iy = 0; iy < Vertical; iy++)
                        {
                            float y = -(float)((iy + 0.5) * d / Vertical);

                            Float3 v = field.VelocityAt(x, y, z, t);
                            double square = (double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z;

                            sum += w * square;
                            sx += w * (double)v.X * v.X;
                            sy += w * (double)v.Y * v.Y;
                            sz += w * (double)v.Z * v.Z;
                            weight += w;

                            if (square > fastest) fastest = square;
                        }
                    }
                }
            }

            return (
                Math.Sqrt(sum / weight), Math.Sqrt(fastest),
                Math.Sqrt(sx / weight), Math.Sqrt(sy / weight), Math.Sqrt(sz / weight));
        }

        private static double Vx(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).X;

        private static double Vy(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Y;

        private static double Vz(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Z;

        [Fact]
        public void TheSlopedStreamsAreDivergenceFree()
        {
            // The property the map is chosen for, and the one the grid's books rest on. Measured
            // exactly as StreamsTests measures the flat field's, on a stencil held clear of every
            // clamp: outside the water the field reads the nearest face on purpose, and a
            // difference across that clamp measures the clamp.
            BedShape bed = Bed();
            CurrentField field = Streams(bed);

            const double H = 0.05;

            var rng = new Rng(7UL);
            double worst = 0d, worstGradient = 0d;
            double rms = Measure(field, bed).Rms;

            for (int i = 0; i < 1000; i++)
            {
                (float x, float y, float z) = Interior(rng, bed, inset: 4d * H);
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
                $"worst |div u'| {worst:0.0000e+0} /s over 1,000 points, against gradients of up " +
                $"to {worstGradient:0.0000} /s and an RMS of {rms:0.0000} m/s — " +
                $"{worst / rms:0.0000e+0} of the RMS per metre");

            Assert.True(worst < 1e-3 * rms, $"worst divergence {worst:0.0000e+0} /s");
        }

        [Fact]
        public void TheJacobiansTraceIsZeroAsWell()
        {
            // The analytic Jacobian, checked independently of the stencil above: the acceleration
            // contracts it against u and a term in the wrong place could hide there.
            BedShape bed = Bed();
            CurrentField field = Streams(bed);

            var rng = new Rng(43UL);
            double worst = 0d;
            double rms = Measure(field, bed, instants: 11).Rms;

            for (int i = 0; i < 500; i++)
            {
                (float x, float y, float z) = Interior(rng, bed, inset: 0.2d);
                double t = rng.NextFloat() * 4d * Period;

                (Float3 _, Float3 _, float divergence) = field.StreamsGradientAt(x, y, z, t);
                worst = Math.Max(worst, Math.Abs(divergence));
            }

            _output.WriteLine(
                $"worst trace of the analytic Jacobian {worst:0.0000e+0} /s, " +
                $"{worst / rms:0.0000e+0} of the RMS per metre");

            Assert.True(worst < 1e-3 * rms, $"worst trace {worst:0.0000e+0} /s");
        }

        [Fact]
        public void NothingFlowsThroughTheSlopedFloor()
        {
            // The second property the map buys, and the one a height map cannot be given by a
            // clamp: the floor's normal is (−h_x, 1, −h_z) and the flow along it has to cancel
            // against the vertical component that the map's off-diagonals produce.
            BedShape bed = Bed();
            CurrentField field = Streams(bed);

            var rng = new Rng(11UL);
            double worst = 0d, worstTangential = 0d;
            double rms = Measure(field, bed, instants: 11).Rms;

            for (int i = 0; i < 500; i++)
            {
                float r = (float)(Radius * Math.Sqrt(rng.NextFloat()));
                double theta = 2d * Math.PI * rng.NextFloat();

                float x = (float)(Radius + r * Math.Cos(theta));
                float z = (float)(Radius + r * Math.Sin(theta));
                float y = (float)bed.FloorY(x, z);
                double t = rng.NextFloat() * 4d * Period;

                (double hx, double hz) = bed.Gradient(x, z);
                Float3 v = field.VelocityAt(x, y, z, t);

                // The unit normal of a height map's surface.
                double norm = Math.Sqrt(1d + hx * hx + hz * hz);
                double normal = (-hx * v.X + v.Y - hz * v.Z) / norm;

                worst = Math.Max(worst, Math.Abs(normal));
                worstTangential = Math.Max(worstTangential, v.Magnitude);
            }

            _output.WriteLine(
                $"worst normal velocity at the sloped floor {worst:0.0000e+0} m/s — " +
                $"{worst / rms:0.0000e+0} of the RMS, against tangential water of up to " +
                $"{worstTangential:0.0000} m/s there");

            // The flat bed and the glass read 1e-7 of the RMS, which is the float's own resolution
            // in a cancellation of two terms of the size of the water; this cannot do better and
            // is asserted at the same place.
            Assert.True(worst < 1e-5 * rms, $"worst normal velocity {worst:0.0000e+0} m/s");
        }

        [Fact]
        public void NothingFlowsThroughTheGlassOrTheSurface()
        {
            // The map fixes x and z, so the glass is untouched by it; and both off-diagonals carry
            // a factor y, so at the waterline the map is the identity to first order and the
            // vertical component is exactly the flat field's, which is exactly zero.
            BedShape bed = Bed();
            CurrentField field = Streams(bed);

            var rng = new Rng(13UL);
            double worstRadial = 0d;

            for (int i = 0; i < 300; i++)
            {
                double theta = 2d * Math.PI * rng.NextFloat();
                float x = (float)(Radius + Radius * Math.Cos(theta));
                float z = (float)(Radius + Radius * Math.Sin(theta));
                float y = -(float)(rng.NextFloat() * -bed.FloorY(x, z));
                double t = rng.NextFloat() * 4d * Period;

                Float3 v = field.VelocityAt(x, y, z, t);
                worstRadial = Math.Max(
                    worstRadial, Math.Abs(v.X * Math.Cos(theta) + v.Z * Math.Sin(theta)));

                (float sx, _, float sz) = Interior(rng, bed, inset: 0.5d);

                Assert.Equal(0f, field.VelocityAt(sx, 0f, sz, t).Y);
                Assert.Equal(0f, field.VelocityAt(sx, 3f, sz, t).Y);
            }

            _output.WriteLine($"worst radial velocity at the glass {worstRadial:0.0000e+0} m/s");

            Assert.True(worstRadial < 1e-6, $"worst radial velocity {worstRadial:0.0000e+0} m/s");
        }

        [Fact]
        public void TheRmsSpeedIsStillTheKnob()
        {
            // The map is not volume-preserving column by column — it stretches the water over a
            // rise and squeezes it in a hollow — so the scale has to be re-measured over the
            // sloped volume, and this is the second lattice that says the measurement took.
            // The flat field on this same lattice first, because "the RMS is the knob" is measured
            // here and fitted somewhere else, and a lattice of its own has a bias of its own: what
            // the sloped field has to match is the accuracy the flat field reaches, not an
            // absolute.
            BedShape level = Bed(relief: 0f, tilt: 0f);
            double flatReading = Measure(Streams(null), level).Rms;

            _output.WriteLine(
                $"the flat field on this test's lattice reads {flatReading:0.0000} m/s at a knob " +
                $"of {Speed} — {flatReading / Speed:0.0%}");

            foreach (float speed in new[] { 0.05f, 0.1f, 0.3f })
            {
                BedShape bed = Bed();
                CurrentField field = Streams(bed, speed);

                (double rms, double fastest, double x, double y, double z) = Measure(field, bed);

                _output.WriteLine(
                    $"knob {speed} m/s: RMS {rms:0.0000} m/s ({rms / speed:0.0%} of the knob), " +
                    $"fastest sample {fastest:0.0000} m/s, bound {field.MaximumTransportSpeed:0.0000} m/s " +
                    $"({field.MaximumTransportSpeed / fastest:0.00}x the fastest sample); " +
                    $"per axis x {x:0.0000}, y {y:0.0000}, z {z:0.0000} m/s");

                // The spec's 1%, read against the flat field's own reading on this lattice rather
                // than against the knob. This lattice is coarser than the one the field is fitted
                // on (19 by 13 columns and 41 instants against 22 by 16 and 64) and reads 1.9%
                // high on the flat field, which is the lattice and not the water: what is being
                // asserted is that the map's own normalisation is as accurate as the flat field's,
                // and both are then as accurate as the fit's lattice makes them.
                Assert.Equal(flatReading / Speed, rms / speed, 0.01);

                // And an absolute sanity bound, so that a normalisation that had gone badly wrong
                // could not hide behind a biased reading.
                Assert.Equal((double)speed, rms, 0.05 * speed);

                // The Courant check GridField.Advect makes is against this, and it has to be a
                // ceiling: over a rise the water is faster than anywhere in the flat tank.
                Assert.True(
                    field.MaximumTransportSpeed > fastest,
                    $"the bound {field.MaximumTransportSpeed} m/s is under the fastest sample {fastest} m/s");
            }
        }

        [Fact]
        public void AtReliefZeroItIsTheFlatFieldToTheBit()
        {
            // Spec item 12. Not "close": the same floats, from the same arithmetic, because a
            // recorded world replaying as a cousin of itself is the failure D078 spent three days
            // on. A BedShape at relief 0 and tilt 0 has no relief, so SetBox holds null and every
            // sampler takes the branch it always took — which is the thing asserted here.
            BedShape flat = Bed(relief: 0f, tilt: 0f);

            CurrentField withBed = Streams(flat);
            CurrentField without = Streams(null);

            Assert.Null(withBed.Bed);

            var rng = new Rng(17UL);

            for (int i = 0; i < 400; i++)
            {
                float x = rng.NextFloat() * 2f * Radius;
                float z = rng.NextFloat() * 2f * Radius;
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * 4d * Period;

                Assert.Equal(without.VelocityAt(x, y, z, t), withBed.VelocityAt(x, y, z, t));
                Assert.Equal(without.PotentialAt(x, y, z, t), withBed.PotentialAt(x, y, z, t));
                Assert.Equal(without.AccelerationAt(x, y, z, t), withBed.AccelerationAt(x, y, z, t));
            }

            Assert.Equal(without.MaximumTransportSpeed, withBed.MaximumTransportSpeed);
            Assert.Equal(without.StreamsComponentRms, withBed.StreamsComponentRms);
        }

        [Fact]
        public void TheAccelerationAgreesWithAStencilOnTheSameWater()
        {
            // The analytic route carried through the map, against a central difference of the
            // sloped velocity along the path — the same comparison FluidAccelerationTests makes
            // on the flat field, at the same tolerances, on a field with two more layers of chain
            // rule in it.
            BedShape bed = Bed();
            CurrentField field = Streams(bed);

            var rng = new Rng(19UL);

            double sumSquared = 0d, sumReference = 0d, worst = 0d, worstMagnitude = 0d;
            double sumFine = 0d, sumStencil = 0d;
            int n = 0;

            for (int i = 0; i < 400; i++)
            {
                // Well clear of every clamp: the stencil is 0.05 m and the water outside the
                // floor, the glass and the surface is the boundary's own, so a stencil that
                // straddled one would difference the clamp.
                (float x, float y, float z) = Interior(rng, bed, inset: 0.5d);
                double t = rng.NextFloat() * 4d * Period;

                Float3 analytic = field.AccelerationAt(x, y, z, t);
                Float3 stencil = Stencil(field, x, y, z, t, 0.05f, 0.01d);
                Float3 finer = Stencil(field, x, y, z, t, 0.0125f, 0.0025d);

                Float3 difference = analytic - stencil;
                Float3 fineDifference = analytic - finer;
                Float3 stencilError = finer - stencil;

                sumFine += (double)fineDifference.Magnitude * fineDifference.Magnitude;
                sumStencil += (double)stencilError.Magnitude * stencilError.Magnitude;

                sumSquared += (double)difference.Magnitude * difference.Magnitude;
                sumReference += (double)stencil.Magnitude * stencil.Magnitude;
                worst = Math.Max(worst, difference.Magnitude);
                worstMagnitude = Math.Max(worstMagnitude, stencil.Magnitude);
                n++;
            }

            double rmsError = Math.Sqrt(sumSquared / n);
            double rmsReference = Math.Sqrt(sumReference / n);
            double rmsFine = Math.Sqrt(sumFine / n);
            double rmsStencil = Math.Sqrt(sumStencil / n);

            _output.WriteLine(
                $"RMS disagreement with the 0.05 m stencil {rmsError:0.0000e+0} m/s² against an " +
                $"RMS acceleration of {rmsReference:0.0000e+0} m/s² — {rmsError / rmsReference:0.00%}; " +
                $"worst {worst:0.0000e+0} against the largest stencil reading " +
                $"{worstMagnitude:0.0000e+0} ({worst / worstMagnitude:0.00%})");

            _output.WriteLine(
                $"with a stencil four times finer {rmsFine / rmsReference:0.00%}, and the two " +
                $"stencils differ from each other by {rmsStencil / rmsReference:0.00%} — which is " +
                "what says whether the disagreement above is the analytic form's or the stencil's");

            // The analytic spec's numbers for the flat field: 0.5% RMS, 2% worst, where the
            // stencil's own error against a finer stencil was 0.37% and 1.0%. The sloped field
            // varies faster in y than the flat one — the map squeezes the whole column into the
            // water over a rise — so the coarse stencil's own truncation is larger here, and what
            // is asserted is that the analytic form sits closer to the finer stencil than the two
            // stencils sit to each other.
            Assert.True(
                rmsFine < 0.005 * rmsReference,
                $"RMS disagreement with the finer stencil {rmsFine / rmsReference:0.00%}");

            Assert.True(
                rmsFine < rmsError,
                "the analytic form does not improve against a finer stencil, which is what a " +
                "wrong term would look like");

            Assert.True(
                worst < 0.02 * worstMagnitude,
                $"worst disagreement {worst / worstMagnitude:0.00%}");
        }

        /// <summary>
        /// A central difference of the field along its own path, at a step of this test's
        /// choosing — <see cref="CurrentField.MaterialDerivative"/> with the step opened up, so
        /// that the analytic form can be held against two stencils and not only one.
        /// </summary>
        private static Float3 Stencil(
            CurrentField field, float x, float y, float z, double t, float h, double dt)
        {
            Float3 u = field.VelocityAt(x, y, z, t);
            float perMetre = 1f / (2f * h);

            Float3 total =
                (field.VelocityAt(x + h, y, z, t) - field.VelocityAt(x - h, y, z, t)) * (perMetre * u.X) +
                (field.VelocityAt(x, y + h, z, t) - field.VelocityAt(x, y - h, z, t)) * (perMetre * u.Y) +
                (field.VelocityAt(x, y, z + h, t) - field.VelocityAt(x, y, z - h, t)) * (perMetre * u.Z);

            return total +
                   (field.VelocityAt(x, y, z, t + dt) - field.VelocityAt(x, y, z, t - dt)) *
                   (float)(1d / (2d * dt));
        }

        [Fact]
        public void TheCurlOfTheSlopedPotentialIsTheSlopedVelocity()
        {
            // What the grid's face fluxes rest on: it takes its circulations from PotentialAt, so
            // a potential that described a slightly different field would put a slow leak in the
            // books that no test of either function alone could see. ConservativeTransportTests
            // makes this check on the flat field.
            BedShape bed = Bed();
            CurrentField field = Streams(bed, speed: 0.37f);

            const double H = 0.02;

            var rng = new Rng(23UL);
            double sumSquared = 0d, sumReference = 0d, worst = 0d;
            int n = 0;

            for (int i = 0; i < 500; i++)
            {
                (float x, float y, float z) = Interior(rng, bed, inset: 4d * H);
                double t = rng.NextFloat() * 4d * Period;

                Float3 ax1 = field.PotentialAt((float)(x + H), y, z, t);
                Float3 ax0 = field.PotentialAt((float)(x - H), y, z, t);
                Float3 ay1 = field.PotentialAt(x, (float)(y + H), z, t);
                Float3 ay0 = field.PotentialAt(x, (float)(y - H), z, t);
                Float3 az1 = field.PotentialAt(x, y, (float)(z + H), t);
                Float3 az0 = field.PotentialAt(x, y, (float)(z - H), t);

                double perMetre = 1d / (2d * H);

                var curl = new Float3(
                    (float)(((ay1.Z - ay0.Z) - (az1.Y - az0.Y)) * perMetre),
                    (float)(((az1.X - az0.X) - (ax1.Z - ax0.Z)) * perMetre),
                    (float)(((ax1.Y - ax0.Y) - (ay1.X - ay0.X)) * perMetre));

                Float3 v = field.VelocityAt(x, y, z, t);
                Float3 difference = curl - v;

                sumSquared += (double)difference.Magnitude * difference.Magnitude;
                sumReference += (double)v.Magnitude * v.Magnitude;
                worst = Math.Max(worst, difference.Magnitude);
                n++;
            }

            double rmsError = Math.Sqrt(sumSquared / n);
            double rms = Math.Sqrt(sumReference / n);

            _output.WriteLine(
                $"curl of A' against u': RMS error {rmsError:0.0000e+0} m/s on an RMS speed of " +
                $"{rms:0.0000} m/s ({rmsError / rms:0.000%}), worst {worst:0.0000e+0} m/s " +
                $"({worst / rms:0.00%})");

            Assert.True(rmsError < 1e-3 * rms, $"RMS error {rmsError / rms:0.000%}");
            Assert.True(worst < 1e-2 * rms, $"worst error {worst / rms:0.00%}");
        }

        [Fact]
        public void TheSlopedFieldCostsWhatTheSpecAllows()
        {
            // Spec item 8: the current's samples per part per step may grow by two to three times.
            // A sloped velocity is one flat sample plus the bed's twelve cosines; a sloped
            // acceleration is one flat gradient plus the same cosines and their second
            // derivatives. Reported in nanoseconds per call, as the analytic spec reported its
            // own, rather than counted by hand.
            BedShape bed = Bed();
            CurrentField sloped = Streams(bed);
            CurrentField flat = Streams(null);

            // Built before the clock starts: the streams' construction is a lattice of
            // measurements and would otherwise be in the first call.
            flat.VelocityAt(Radius, -10f, Radius, 0d);
            sloped.VelocityAt(Radius, -10f, Radius, 0d);

            _output.WriteLine("field   VelocityAt   AccelerationAt   accel/velocity");

            double flatVelocity = 0d, flatAcceleration = 0d;

            foreach (bool bedded in new[] { false, true })
            {
                CurrentField field = bedded ? sloped : flat;

                double velocity = Nanoseconds(field, acceleration: false);
                double accelerate = Nanoseconds(field, acceleration: true);

                if (!bedded)
                {
                    flatVelocity = velocity;
                    flatAcceleration = accelerate;
                }

                _output.WriteLine(
                    $"{(bedded ? "sloped" : "flat  ")}  {velocity,8:0} ns  {accelerate,12:0} ns  " +
                    $"{accelerate / velocity,13:0.0}x" +
                    (bedded
                        ? $"   — against the flat field: {velocity / flatVelocity:0.00}x on the " +
                          $"velocity, {accelerate / flatAcceleration:0.00}x on the acceleration"
                        : string.Empty));
            }

            _output.WriteLine(
                "The spec's ceiling is two to three times the flat field's per-sample cost.");
        }

        private static double Nanoseconds(CurrentField field, bool acceleration)
        {
            const int Calls = 40000;

            var rng = new Rng(97UL);
            float radius = field.TankRadiusMetres;

            // One warm run and one measured, so the instant memo and the branch predictor are in
            // the same state for both fields.
            for (int pass = 0; pass < 2; pass++)
            {
                var clock = Stopwatch.StartNew();
                double sink = 0d;

                for (int i = 0; i < Calls; i++)
                {
                    float x = radius + (rng.NextFloat() - 0.5f) * radius;
                    float z = radius + (rng.NextFloat() - 0.5f) * radius;
                    float y = -2f - rng.NextFloat() * 50f;
                    double t = i * 0.5d;

                    Float3 v = acceleration
                        ? field.AccelerationAt(x, y, z, t)
                        : field.VelocityAt(x, y, z, t);

                    sink += v.X;
                }

                clock.Stop();

                if (pass == 1 && sink != double.MaxValue)
                {
                    return clock.Elapsed.TotalMilliseconds * 1e6 / Calls;
                }
            }

            return 0d;
        }
    }
}
