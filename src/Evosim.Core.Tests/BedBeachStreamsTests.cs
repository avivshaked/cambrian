using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The water at the shore — <c>logbook/specs/beach-spec.md</c> §3 and §5's test 2: the sloped
    /// streams with the fade, at round 45's tank and the beach's dials.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read where the fade is doing most.</b> The shipped fade is the quintic times
    /// a depth factor, <c>d/D</c> shallower than <c>0.9·D</c> and 1 deeper than <c>1.1·D</c> with a
    /// C² turnover between, so it reaches every column shallower than <c>1.1·D</c>; its sharpest
    /// part is the band between the shoal's metre and the shoal plus the fade, where the quintic
    /// and its two derivatives are all live, and most samples below are drawn there. The contour
    /// at the mean depth, inside the turnover, has a test of its own.
    /// </para>
    /// <para>
    /// <b>The same measurements D092's water was held to</b> (<see cref="BedStreamsTests"/>): the
    /// divergence by central differences, the normal velocity on the floor and at the glass, the
    /// curl of the potential against the velocity, and the analytic acceleration against a
    /// stencil on the same water. The shoal's own reading is new: the water over it is still.
    /// </para>
    /// </remarks>
    public class BedBeachStreamsTests
    {
        private readonly ITestOutputHelper _output;

        public BedBeachStreamsTests(ITestOutputHelper output) => _output = output;

        private const int Rings = 4;
        private const float Speed = 0.1f;
        private const float Period = 6000f;
        private const ulong RunSeed = 1UL;

        private static float Radius => BedBeachTests.Radius;

        private static CurrentField Streams(BedShape bed, float speed = Speed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(BedBeachTests.Area / Rings), Rings, BedBeachTests.Depth,
                Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);

            return field;
        }

        // One floor and one water for the whole class: the streams' construction walks lattices
        // over a tank of 22,000 m², and every test reads the same beach.
        private static readonly Lazy<(BedShape Bed, CurrentField Field, double Rms)> Beach =
            new Lazy<(BedShape, CurrentField, double)>(() =>
            {
                BedShape bed = BedBeachTests.Beach(RunSeed);
                CurrentField field = Streams(bed);
                return (bed, field, Rms(field, bed));
            });

        /// <summary>The RMS speed over the water that exists, weighted by each column's depth.</summary>
        private static double Rms(CurrentField field, BedShape bed)
        {
            const int Horizontal = 31;
            const int Vertical = 13;
            const int Instants = 9;

            double sum = 0d, weight = 0d;

            for (int p = 0; p < Instants; p++)
            {
                double t = p * Period * 1.7320508075688772d / Instants;

                for (int ix = 0; ix < Horizontal; ix++)
                {
                    float x = (float)((ix + 0.5) * 2d * Radius / Horizontal);

                    for (int iz = 0; iz < Horizontal; iz++)
                    {
                        float z = (float)((iz + 0.5) * 2d * Radius / Horizontal);
                        if (!TankGeometry.Inside(x, z, Radius)) continue;

                        double d = -bed.FloorY(x, z);

                        for (int iy = 0; iy < Vertical; iy++)
                        {
                            float y = -(float)((iy + 0.5) * d / Vertical);
                            Float3 v = field.VelocityAt(x, y, z, t);
                            sum += d * ((double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z);
                            weight += d;
                        }
                    }
                }
            }

            return Math.Sqrt(sum / weight);
        }

        /// <summary>
        /// A column inside the fade's band — its water depth between the shoal plus
        /// <paramref name="margin"/> and the shoal plus the fade — drawn uniformly from the disc by
        /// rejection.
        /// </summary>
        private static (float X, float Z, double FloorY) BandColumn(Rng rng, BedShape bed, double margin)
        {
            while (true)
            {
                float r = (float)((Radius - 0.5d) * Math.Sqrt(rng.NextFloat()));
                double theta = 2d * Math.PI * rng.NextFloat();

                float x = (float)(Radius + r * Math.Cos(theta));
                float z = (float)(Radius + r * Math.Sin(theta));

                double floor = bed.FloorY(x, z);
                double d = -floor;

                if (d > BedBeachTests.Shore + margin && d < BedBeachTests.Shore + BedBeachTests.Fade)
                {
                    return (x, z, floor);
                }
            }
        }

        private static (float X, float Y, float Z) BandPoint(Rng rng, BedShape bed, double inset)
        {
            (float x, float z, double floor) = BandColumn(rng, bed, 2d * inset + 0.1d);
            double y = floor + inset + rng.NextFloat() * (0d - floor - 2d * inset);
            return (x, (float)y, z);
        }

        [Fact]
        public void TheFadedStreamsAreDivergenceFree()
        {
            // Two stencils, because the band's water is not the tank's: the fade's own term runs
            // along the contours at several times the RMS (the shoal test prints how fast), so its
            // gradients are an order larger than D092's and a 5 cm difference's truncation error
            // is too. The finer stencil's reading is the one held to D092's bound, and the
            // coarse one's is printed beside it so that the ratio says the residual is the
            // stencil's and not the water's. The analytic trace, the next test, is the exact one.
            (BedShape bed, CurrentField field, double rms) = Beach.Value;

            foreach (double h in new[] { 0.05d, 0.0125d })
            {
                var rng = new Rng(7UL);
                double worst = 0d, worstGradient = 0d, worstRatio = 0d;

                for (int i = 0; i < 1000; i++)
                {
                    (float x, float y, float z) = BandPoint(rng, bed, 0.2d);
                    double t = rng.NextFloat() * 4d * Period;

                    double dudx = (V(field, x + h, y, z, t).X - V(field, x - h, y, z, t).X) / (2d * h);
                    double dvdy = (V(field, x, y + h, z, t).Y - V(field, x, y - h, z, t).Y) / (2d * h);
                    double dwdz = (V(field, x, y, z + h, t).Z - V(field, x, y, z - h, t).Z) / (2d * h);

                    double divergence = Math.Abs(dudx + dvdy + dwdz);
                    double gradient = Math.Abs(dudx) + Math.Abs(dvdy) + Math.Abs(dwdz);

                    worst = Math.Max(worst, divergence);
                    worstGradient = Math.Max(worstGradient, gradient);
                    if (gradient > 0d) worstRatio = Math.Max(worstRatio, divergence / gradient);
                }

                _output.WriteLine(
                    $"stencil {h} m: RMS {rms:0.00000} m/s over the beach's water; worst |div u''| in " +
                    $"the band {worst:0.0000e+0} /s against gradients up to {worstGradient:0.0000} /s — " +
                    $"{worst / rms:0.0000e+0} of the RMS per metre, and at worst " +
                    $"{worstRatio:0.0000e+0} of the point's own gradient");

                if (h < 0.05d) Assert.True(worst < 1e-3 * rms, $"worst divergence {worst:0.0000e+0} /s");
            }
        }

        /// <summary>The half-width of the depth factor's turnover, in <c>d/D</c>.</summary>
        internal const double Turnover = 0.1d;

        /// <summary>
        /// The depth factor <c>m(x)</c> at <c>x = d/D</c> and its slope and curvature in <c>x</c>,
        /// rebuilt from the outside as the quintic Hermite interpolant of the ruling's six end
        /// conditions (value <c>1 − w</c>, slope 1, curvature 0 at <c>1 − w</c>; value 1, slope 0,
        /// curvature 0 at <c>1 + w</c>) on the textbook basis, so a slip in the shipped
        /// polynomial's own algebra does not agree with itself here.
        /// </summary>
        internal static (double M, double Mx, double Mxx) DepthFactor(double x)
        {
            const double W = Turnover;
            if (x <= 1d - W) return (x, 1d, 0d);
            if (x >= 1d + W) return (1d, 0d, 0d);

            double L = 2d * W;
            double t = (x - (1d - W)) / L;
            double t2 = t * t, t3 = t2 * t, t4 = t3 * t, t5 = t4 * t;

            // H0, H1 carry the end values and H2 the start's slope; the other three conditions are
            // zero and their basis functions drop out.
            double h0 = 1d - 10d * t3 + 15d * t4 - 6d * t5;
            double h1 = 10d * t3 - 15d * t4 + 6d * t5;
            double h2 = t - 6d * t3 + 8d * t4 - 3d * t5;
            double h0d = -30d * t2 + 60d * t3 - 30d * t4;
            double h2d = 1d - 18d * t2 + 32d * t3 - 15d * t4;
            double h0dd = -60d * t + 180d * t2 - 120d * t3;
            double h2dd = -36d * t + 96d * t2 - 60d * t3;

            double m = (1d - W) * h0 + 1d * h1 + L * 1d * h2;
            double mx = ((1d - W) * h0d - h0d + L * h2d) / L;
            double mxx = ((1d - W) * h0dd - h0dd + L * h2dd) / (L * L);
            return (m, mx, mxx);
        }

        /// <summary>
        /// The shipped fade and its slope in <c>d</c>, rebuilt from the outside: the quintic times
        /// the depth factor (the coordinator's rulings of 2026-09-23).
        /// </summary>
        internal static (double G, double Gd) ShippedFade(
            double d, double shore = BedBeachTests.Shore, double fade = BedBeachTests.Fade,
            double depth = BedBeachTests.Depth, bool depthFactor = true)
        {
            (double mOfX, double mxOfX, _) = depthFactor ? DepthFactor(d / depth) : (1d, 0d, 0d);
            double m = mOfX;
            double md = mxOfX / depth;
            double xi = (d - shore) / fade;
            if (xi <= 0d) return (0d, 0d);
            if (xi >= 1d) return (m, md);
            double q = xi * xi * xi * (10d + xi * (-15d + 6d * xi));
            double qd = 30d * xi * xi * (1d - xi) * (1d - xi) / fade;
            return (q * m, qd * m + q * md);
        }

        private static Float3 V(CurrentField field, double x, double y, double z, double t) =>
            field.VelocityAt((float)x, (float)y, (float)z, t);

        [Fact]
        public void TheAnalyticJacobiansTraceIsZeroInTheBand()
        {
            (BedShape bed, CurrentField field, double rms) = Beach.Value;

            var rng = new Rng(43UL);
            double worst = 0d;

            for (int i = 0; i < 1000; i++)
            {
                (float x, float y, float z) = BandPoint(rng, bed, 0.2d);
                double t = rng.NextFloat() * 4d * Period;

                (Float3 velocity, Float3 _, float divergence) = field.StreamsGradientAt(x, y, z, t);
                worst = Math.Max(worst, Math.Abs(divergence));

                // The analytic route's own velocity is the sampler's.
                Float3 sampled = field.VelocityAt(x, y, z, t);
                Assert.True(
                    (velocity - sampled).Magnitude < 1e-5 * rms,
                    $"the analytic route's velocity {velocity} against the sampler's {sampled}");
            }

            _output.WriteLine(
                $"worst trace of the faded Jacobian {worst:0.0000e+0} /s, {worst / rms:0.0000e+0} " +
                "of the RMS per metre");

            Assert.True(worst < 1e-3 * rms, $"worst trace {worst:0.0000e+0} /s");
        }

        [Fact]
        public void NothingFlowsThroughTheFloorOrTheGlassAtTheShore()
        {
            (BedShape bed, CurrentField field, double rms) = Beach.Value;

            var rng = new Rng(11UL);
            double worstFloor = 0d, worstGlass = 0d, tangential = 0d;

            // On the floor, over the band and over the shoal.
            for (int i = 0; i < 1000; i++)
            {
                (float x, float z, double _) = BandColumn(rng, bed, -BedBeachTests.Shore);
                float y = (float)bed.FloorY(x, z);
                double t = rng.NextFloat() * 4d * Period;

                (double hx, double hz) = bed.Gradient(x, z);
                Float3 v = field.VelocityAt(x, y, z, t);

                double norm = Math.Sqrt(1d + hx * hx + hz * hz);
                worstFloor = Math.Max(worstFloor, Math.Abs((-hx * v.X + v.Y - hz * v.Z) / norm));
                tangential = Math.Max(tangential, v.Magnitude);
            }

            // At the glass, all the way round, the shore's arc included.
            for (int i = 0; i < 1000; i++)
            {
                double theta = 2d * Math.PI * rng.NextFloat();
                float x = (float)(Radius + Radius * Math.Cos(theta));
                float z = (float)(Radius + Radius * Math.Sin(theta));
                float y = -(float)(rng.NextFloat() * -bed.FloorY(x, z));
                double t = rng.NextFloat() * 4d * Period;

                Float3 v = field.VelocityAt(x, y, z, t);
                worstGlass = Math.Max(worstGlass, Math.Abs(v.X * Math.Cos(theta) + v.Z * Math.Sin(theta)));
            }

            _output.WriteLine(
                $"worst normal velocity at the floor {worstFloor:0.0000e+0} m/s ({worstFloor / rms:0.0000e+0} " +
                $"of the RMS), against tangential water up to {tangential:0.0000} m/s there; " +
                $"worst radial velocity at the glass {worstGlass:0.0000e+0} m/s ({worstGlass / rms:0.0000e+0} of the RMS)");

            Assert.True(worstFloor < 1e-5 * rms, $"floor {worstFloor:0.0000e+0} m/s");
            Assert.True(worstGlass < 1e-5 * rms, $"glass {worstGlass:0.0000e+0} m/s");
        }

        [Fact]
        public void TheWaterOverTheShoalIsStill()
        {
            (BedShape bed, CurrentField field, double rms) = Beach.Value;

            var rng = new Rng(13UL);
            double worst = 0d, worstAcceleration = 0d;
            int shoal = 0, tries = 0;

            while (shoal < 500 && tries < 2_000_000)
            {
                tries++;
                float r = (float)(Radius * Math.Sqrt(rng.NextFloat()));
                double theta = 2d * Math.PI * rng.NextFloat();
                float x = (float)(Radius + r * Math.Cos(theta));
                float z = (float)(Radius + r * Math.Sin(theta));

                if (bed.FloorY(x, z) != -BedBeachTests.Shore) continue;

                float y = -rng.NextFloat() * BedBeachTests.Shore;
                double t = rng.NextFloat() * 4d * Period;

                worst = Math.Max(worst, field.VelocityAt(x, y, z, t).Magnitude);
                worstAcceleration = Math.Max(worstAcceleration, field.AccelerationAt(x, y, z, t).Magnitude);
                shoal++;
            }

            // And the water over the shelf, which the spec's smoke prints: the fastest sample and
            // the RMS over the band's columns, split into its two terms. The fade's own term
            // ∇f × A' is rebuilt here from the outside — f and f' from the column's depth by the
            // quintic, ∇h from the bed, A' as the faded potential over f — so the split is the
            // field's arithmetic read back, and the other term is the velocity less it.
            double shelfFastest = 0d, shelfSquare = 0d, contourSquare = 0d, stretchSquare = 0d;
            double contourFastest = 0d;
            int shelfSamples = 0;
            float sigma = field.Speed;
            for (int i = 0; i < 4000; i++)
            {
                (float x, float y, float z) = BandPoint(rng, bed, 0.05d);
                double t = rng.NextFloat() * 4d * Period;
                Float3 v = field.VelocityAt(x, y, z, t);
                double speed = v.Magnitude;
                shelfFastest = Math.Max(shelfFastest, speed);
                shelfSquare += speed * speed;
                shelfSamples++;

                (double f, double fd) = ShippedFade(-bed.FloorY(x, z));
                (double hx, double hz) = bed.Gradient(x, z);
                Float3 p = field.PotentialAt(x, y, z, t);
                double gx = -fd * hx, gz = -fd * hz;
                double wx = -gz * p.Y / f, wy = (gz * p.X - gx * p.Z) / f, wz = gx * p.Y / f;
                double contour = Math.Sqrt(wx * wx + wy * wy + wz * wz);
                double sx = v.X - wx, sy = v.Y - wy, sz = v.Z - wz;

                contourSquare += contour * contour;
                stretchSquare += sx * sx + sy * sy + sz * sz;
                contourFastest = Math.Max(contourFastest, contour);
            }

            double shelfRms = Math.Sqrt(shelfSquare / shelfSamples);

            _output.WriteLine(
                $"over the band, the fade's contour term ∇f × A' runs at RMS " +
                $"{Math.Sqrt(contourSquare / shelfSamples):0.0000} m/s (fastest {contourFastest:0.0000}) " +
                $"and the stretched water f·u' at RMS {Math.Sqrt(stretchSquare / shelfSamples):0.0000} m/s; " +
                $"knob {sigma} m/s");

            _output.WriteLine(
                $"{shoal} points over the shoal: fastest water {worst:0.0000e+0} m/s " +
                $"({worst / rms:0.0000e+0} of the RMS), largest acceleration {worstAcceleration:0.0e+0} m/s²; " +
                $"over the band's columns the RMS is {shelfRms:0.0000} m/s ({shelfRms / rms:0.00}x the tank's) " +
                $"and the fastest sample {shelfFastest:0.0000} m/s ({shelfFastest / rms:0.00}x the RMS, " +
                $"bound {field.MaximumTransportSpeed:0.0000} m/s)");

            Assert.True(shoal >= 500, $"only {shoal} shoal points found");
            Assert.True(worst < 0.01d * rms, $"the water over the shoal runs at {worst / rms:0.0000} of the RMS");
            Assert.True(field.MaximumTransportSpeed > shelfFastest);
        }

        [Fact]
        public void TheCurlOfTheFadedPotentialIsTheFadedVelocity()
        {
            // What the grid's face fluxes rest on, in the band where the fade's own term lives.
            (BedShape bed, CurrentField field, double _) = Beach.Value;

            const double H = 0.02;
            var rng = new Rng(23UL);
            double sumSquared = 0d, sumReference = 0d, worst = 0d;
            int n = 0;

            for (int i = 0; i < 500; i++)
            {
                (float x, float y, float z) = BandPoint(rng, bed, 4d * H);
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
                $"curl of f·A' against u'' in the band: RMS error {rmsError:0.0000e+0} m/s on " +
                $"{rms:0.00000} m/s ({rmsError / rms:0.000%}), worst {worst:0.0000e+0} m/s ({worst / rms:0.00%})");

            Assert.True(rmsError < 1e-3 * rms, $"RMS error {rmsError / rms:0.000%}");
            Assert.True(worst < 1e-2 * rms, $"worst error {worst / rms:0.00%}");
        }

        [Fact]
        public void TheAccelerationAgreesWithAStencilInTheBand()
        {
            // BedStreamsTests' method on the faded field, where the fade's derivatives and the
            // bed's Hessian through ∇∇f are in every sample.
            (BedShape bed, CurrentField field, double _) = Beach.Value;

            var rng = new Rng(19UL);
            double sumSquared = 0d, sumReference = 0d, worst = 0d, worstMagnitude = 0d;
            double sumFine = 0d, sumStencil = 0d;
            int n = 0;

            for (int i = 0; i < 400; i++)
            {
                (float x, float y, float z) = BandPoint(rng, bed, 0.5d);
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
                $"in the band: RMS disagreement with the 0.05 m stencil {rmsError / rmsReference:0.000%}, " +
                $"with the 0.0125 m stencil {rmsFine / rmsReference:0.000%}, the two stencils apart by " +
                $"{rmsStencil / rmsReference:0.000%}; worst {worst / worstMagnitude:0.00%} of the largest " +
                $"reading; RMS acceleration {rmsReference:0.0000e+0} m/s²");

            // The spec's 0.1%, against the finer stencil.
            Assert.True(rmsFine < 0.001 * rmsReference, $"RMS against the finer stencil {rmsFine / rmsReference:0.000%}");
            Assert.True(worst < 0.02 * worstMagnitude, $"worst disagreement {worst / worstMagnitude:0.00%}");
        }

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
        public void AtTiltThirtyTheShoreMovesOnlyTheWaterShallowerThanTheMean()
        {
            // Round 45's own floor (tilt 30 m, water 28 to 60 m deep) with the shore on. Under the
            // plain fade this was the shore-off water to the bit, since f is 1 on every column.
            // Under the shipped form the depth factor is under 1 on every column shallower than
            // 1.1·D, so the shallow half is slowed and the renormalisation to the knob speeds the
            // whole field by one factor, the ratio of the two fields' renormalisations. In a
            // column deeper than 1.1·D the water is the shore-off water times exactly that factor,
            // to the float; in the turnover (0.9·D to 1.1·D, 40.5 to 49.5 m) it departs smoothly
            // and is only printed; shallower than 0.9·D it is slowed.
            BedShape off = new BedShape(
                Radius, BedBeachTests.Depth, BedBeachTests.Relief, 30f, BedBeachTests.Scale,
                Rng.SeedFor(RunSeed, World.BedShapeIndex));
            BedShape on = BedBeachTests.Beach(RunSeed, tilt: 30f);

            CurrentField unfaded = Streams(off);
            CurrentField faded = Streams(on);

            // The exact factor: both fields' velocities are the unit field times
            // speed·streamsScale·bedScale, and only the bed's renormalisation differs.
            var bedScale = typeof(CurrentField).GetField(
                "_bedScale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(bedScale);

            // The streams are built at their first read; read once so the scales are the built ones.
            unfaded.VelocityAt(Radius, -1f, Radius, 0d);
            faded.VelocityAt(Radius, -1f, Radius, 0d);
            double k = (double)bedScale.GetValue(faded) / (double)bedScale.GetValue(unfaded);

            double worstDeep = 0d, shallowSpread = 0d, blendSpread = 0d, measuredK = double.NaN;
            int deep = 0, shallow = 0, blend = 0;
            double deepLine = (1d + Turnover) * BedBeachTests.Depth;
            double shallowLine = (1d - Turnover) * BedBeachTests.Depth;
            var rng = new Rng(17UL);

            for (int i = 0; i < 2000; i++)
            {
                float r = (float)((Radius - 0.5d) * Math.Sqrt(rng.NextFloat()));
                double theta = 2d * Math.PI * rng.NextFloat();
                float x = (float)(Radius + r * Math.Cos(theta));
                float z = (float)(Radius + r * Math.Sin(theta));
                double d = -off.FloorY(x, z);
                float y = -(float)((0.05d + 0.9d * rng.NextFloat()) * d);
                double t = rng.NextFloat() * 4d * Period;

                Float3 a = unfaded.VelocityAt(x, y, z, t);
                Float3 b = faded.VelocityAt(x, y, z, t);
                if (a.Magnitude < 1e-3f) continue;

                double departure = (b - a * (float)k).Magnitude / a.Magnitude;

                if (d > deepLine)
                {
                    if (double.IsNaN(measuredK)) measuredK = (double)b.Magnitude / a.Magnitude;
                    worstDeep = Math.Max(worstDeep, departure);
                    deep++;
                }
                else if (d > shallowLine)
                {
                    blendSpread = Math.Max(blendSpread, departure);
                    blend++;
                }
                else
                {
                    shallowSpread = Math.Max(shallowSpread, departure);
                    shallow++;
                }
            }

            _output.WriteLine(
                $"tilt 30, shore on against off: the renormalisations' ratio is {k:0.000000} " +
                $"(one sample reads {measuredK:0.000000}); in {deep} samples deeper than 1.1·D the " +
                $"water is that times the shore-off water to {worstDeep:0.0e+0}; in {blend} in the " +
                $"turnover it departs by up to {blendSpread:0.000} of itself; in {shallow} shallower " +
                $"than 0.9·D by up to {shallowSpread:0.00}");

            Assert.True(deep > 100 && shallow > 100);
            Assert.True(worstDeep < 1e-5, $"the deep water is not the exact factor of the shore-off water: {worstDeep}");
            Assert.True(shallowSpread > 0.05, "the shallow water was not slowed");
        }

        [Fact]
        public void AcrossTheMeanDepthContour()
        {
            // The depth factor turns over from d/D to 1 between 0.9·D and 1.1·D, across the middle
            // of the tank. Under min(1, d/D) the factor's slope jumped at d = D and the velocity
            // with it, a shear sheet of 0.011 m/s on average and 0.047 m/s at worst. Under the C²
            // turnover the velocity and its Jacobian are continuous there. The jump is read by
            // Richardson: across a pair of points ±e along the slope the difference is 2e·u' plus
            // any step J, across ±3e it is 6e·u' plus J, so 1.5·(Δ_e − Δ_3e/3) cancels the smooth
            // part to O(e³) and leaves J. The same estimate taken 6 m of depth up the slope, where
            // the factor is d/D and nothing turns over, is the control, and the two must read
            // alike. The acceleration is held against the stencil shallower than the turnover,
            // inside it and deeper than it.
            (BedShape bed, CurrentField field, double rms) = Beach.Value;
            double depth = BedBeachTests.Depth;

            var rng = new Rng(61UL);
            double worstJump = 0d, sumJump = 0d, worstControl = 0d, sumControl = 0d, sumRaw = 0d;
            int jumps = 0, controls = 0;

            for (int i = 0; i < 500; i++)
            {
                double x = Radius + (rng.NextFloat() - 0.5d) * Radius;
                double z = Radius + (rng.NextFloat() - 0.5d) * Radius;

                // Newton onto the contour h = 0 along the slope.
                for (int n = 0; n < 20; n++)
                {
                    double h = bed.Height(x, z);
                    (double gx, double gz) = bed.Gradient(x, z);
                    double g2 = gx * gx + gz * gz;
                    if (g2 == 0d) break;
                    x -= h * gx / g2;
                    z -= h * gz / g2;
                }

                if (!TankGeometry.Inside(x, z, Radius - 1d)) continue;
                if (Math.Abs(bed.Height(x, z)) > 1e-6) continue;

                (double sx, double sz) = bed.Gradient(x, z);
                double norm = Math.Sqrt(sx * sx + sz * sz);
                double ux = sx / norm, uz = sz / norm;

                float y = -(float)((0.1d + 0.8d * rng.NextFloat()) * depth);
                double t = rng.NextFloat() * 4d * Period;

                (double j, double raw) = Jump(field, x, y, z, ux, uz, t);
                worstJump = Math.Max(worstJump, j);
                sumJump += j;
                sumRaw += raw;
                jumps++;

                // The control: 6 m of depth up the slope (the floor rises along +∇h), in water
                // shallower than the turnover, if it is still in the tank and above the band's end.
                double cx = x + 6d * ux / norm, cz = z + 6d * uz / norm;
                if (!TankGeometry.Inside(cx, cz, Radius - 1d)) continue;
                double cd = -bed.FloorY(cx, cz);
                if (!(cd < (1d - Turnover) * depth - 1d) || !(cd > BedBeachTests.Shore + BedBeachTests.Fade)) continue;
                float cy = -(float)((0.1d + 0.8d * rng.NextFloat()) * cd);
                (double cj, _) = Jump(field, cx, cy, cz, ux, uz, t);
                worstControl = Math.Max(worstControl, cj);
                sumControl += cj;
                controls++;
            }

            // The acceleration against the stencil: shallower than the turnover, inside it, and
            // deeper than it, each kept half a metre of depth or more off the turnover's edges.
            double worstRatio = 0d;
            var bands = new (string Name, double Low, double High, ulong Seed)[]
            {
                ("shallower than the turnover (36.5 to 40 m)", 0.9d * depth - 4d, 0.9d * depth - 0.5d, 67UL),
                ("inside the turnover (41 to 49 m)", 0.9d * depth + 0.5d, 1.1d * depth - 0.5d, 73UL),
                ("deeper than the turnover (50 to 53.5 m)", 1.1d * depth + 0.5d, 1.1d * depth + 4d, 71UL),
            };
            foreach (var band in bands)
            {
                var pick = new Rng(band.Seed);
                double sumFine = 0d, sumReference = 0d;
                int n = 0;

                while (n < 200)
                {
                    float r = (float)((Radius - 1d) * Math.Sqrt(pick.NextFloat()));
                    double theta = 2d * Math.PI * pick.NextFloat();
                    float x = (float)(Radius + r * Math.Cos(theta));
                    float z = (float)(Radius + r * Math.Sin(theta));
                    double d = -bed.FloorY(x, z);
                    if (!(d > band.Low && d < band.High)) continue;

                    float y = -(float)((0.1d + 0.8d * pick.NextFloat()) * d);
                    double t = pick.NextFloat() * 4d * Period;

                    Float3 analytic = field.AccelerationAt(x, y, z, t);
                    Float3 finer = Stencil(field, x, y, z, t, 0.0125f, 0.0025d);
                    Float3 diff = analytic - finer;

                    sumFine += (double)diff.Magnitude * diff.Magnitude;
                    sumReference += (double)finer.Magnitude * finer.Magnitude;
                    n++;
                }

                double ratio = Math.Sqrt(sumFine / sumReference);
                worstRatio = Math.Max(worstRatio, ratio);
                _output.WriteLine($"{band.Name}: analytic against the 0.0125 m stencil {ratio:0.000%} RMS");
            }

            _output.WriteLine(
                $"the velocity across the d = D contour ({jumps} points): raw difference over 4 mm " +
                $"{sumRaw / jumps:0.0e+0} m/s on average; Richardson step {sumJump / jumps:0.0e+0} m/s on " +
                $"average, {worstJump:0.0e+0} m/s at worst ({worstJump / rms:0.0e+0}x the tank RMS); the " +
                $"control 6 m up the slope ({controls} points) {sumControl / Math.Max(1, controls):0.0e+0} " +
                $"on average, {worstControl:0.0e+0} at worst");

            Assert.True(jumps > 100 && controls > 50);
            Assert.True(worstJump < 1e-4, $"the velocity steps across the mean-depth contour by {worstJump} m/s");
            Assert.True(
                sumJump / jumps < 3d * sumControl / controls + 1e-6,
                "the step across the contour reads above the stencil's rounding in smooth water");
            Assert.True(worstRatio < 0.001, $"the acceleration parts from the stencil by {worstRatio:0.000%}");
        }

        /// <summary>
        /// The step in the velocity across a point along the unit direction <c>(ux, uz)</c>, by
        /// Richardson on pairs 2 mm and 6 mm either side, and the raw difference over the 4 mm.
        /// </summary>
        private static (double Step, double Raw) Jump(
            CurrentField field, double x, float y, double z, double ux, double uz, double t)
        {
            const double E = 0.002;
            Float3 Near(double s) => field.VelocityAt((float)(x + s * ux), y, (float)(z + s * uz), t);

            Float3 inner = Near(E) - Near(-E);
            Float3 outer = Near(3d * E) - Near(-3d * E);
            Float3 step = (inner - outer * (1f / 3f)) * 1.5f;
            return (step.Magnitude, inner.Magnitude);
        }
    }
}
