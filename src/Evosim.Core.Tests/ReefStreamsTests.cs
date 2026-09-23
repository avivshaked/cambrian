using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The streams around the reefs — <c>logbook/specs/reef-spec.md</c> §4, test 4: the curl of
    /// <c>g·A</c> is divergence-free, still in the rock and on it, agrees with the curl of the
    /// faded potential, and its closed-form acceleration agrees with a stencil; and the fastest
    /// water within a cap radius of the rock, printed at three fades in round 46's own tank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two waters.</b> The small flat tank of <see cref="ReefTank"/> exercises the flat
    /// streams' path; round 46's floor (<see cref="BedBeachTests.Beach"/>: 22,000 m², 45 m,
    /// tilt 96 m, the shore at 1 m with a 15 m fade) exercises the sloped path with the shore's
    /// fade inside the reefs'. Round 46's reef dials are the brief's: three caps 8 m across,
    /// 2 m thick at 3 m, on stems 1 m in radius, placed by the seed-1 stream.
    /// </para>
    /// <para>
    /// <b>The seams.</b> The rock's distance is C¹ and not C² on the cylinder through the cap
    /// disc's edge (<c>ρ = r_c − t/2</c>, where the flat top meets the rounded rim), so the
    /// velocity's Jacobian steps there; the acceleration is held against the stencil away from
    /// it, and the step is not read.
    /// </para>
    /// </remarks>
    public class ReefStreamsTests
    {
        private readonly ITestOutputHelper _output;

        public ReefStreamsTests(ITestOutputHelper output) => _output = output;

        private const float Period = 6000f;
        private const float R46CapRadius = 4f;
        private const float R46CapDepth = 3f;
        private const float R46Thickness = 2f;
        private const float R46Stem = 1f;

        /// <summary>Round 46's floor and water, one for the class: the streams' build is seconds.</summary>
        private static readonly Lazy<(BedShape Bed, CurrentField Field)> Round46 =
            new Lazy<(BedShape, CurrentField)>(() =>
            {
                BedShape bed = BedBeachTests.Beach(1UL);
                var field = new CurrentField
                {
                    Mode = CurrentMode.Transport,
                    Speed = 0.1f,
                    PeriodSeconds = Period,
                    AdvectFields = true,
                };

                field.SetBox(
                    (float)Math.Sqrt(BedBeachTests.Area / 4f), 4, BedBeachTests.Depth,
                    Rng.SeedFor(1UL, World.CurrentFieldIndex),
                    patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: BedBeachTests.Radius, bed: bed);

                field.VelocityAt(BedBeachTests.Radius, -1f, BedBeachTests.Radius, 0d);
                return (bed, field);
            });

        private static ReefGeometry Round46Reefs(BedShape bed, float fade)
        {
            var config = new RunConfig
            {
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = BedBeachTests.Area,
                WorldDepthMetres = BedBeachTests.Depth,
                ReefCount = 3,
                ReefCapRadiusMetres = R46CapRadius,
                ReefCapDepthMetres = R46CapDepth,
                ReefCapThicknessMetres = R46Thickness,
                ReefStemRadiusMetres = R46Stem,
                ReefFadeMetres = fade,
            };

            return ReefGeometry.Place(config, BedBeachTests.Radius, bed, Rng.SeedFor(1UL, World.ReefPlacementIndex));
        }

        /// <summary>
        /// A point near a reef, uniform in a cylinder around a random axis, with its signed
        /// distance and horizontal distance from that axis. The floor is <paramref name="bed"/>'s
        /// or flat at <paramref name="depth"/>.
        /// </summary>
        private static (double X, double Y, double Z, double S, double Rho) Near(
            Rng rng, ReefGeometry reefs, BedShape bed, float depth, double reach, double inset)
        {
            while (true)
            {
                int reef = (int)(rng.NextFloat() * reefs.Count) % reefs.Count;
                double r = reach * Math.Sqrt(rng.NextFloat());
                double theta = 2d * Math.PI * rng.NextFloat();
                double x = reefs.CentreX(reef) + r * Math.Cos(theta);
                double z = reefs.CentreZ(reef) + r * Math.Sin(theta);

                // Inside the glass: a fade reaches past it in round 46's tank, and outside the
                // tank the potential is zero while the sampler still answers.
                double radius = bed != null ? BedBeachTests.Radius : ReefTank.Radius;
                if (!TankGeometry.Inside(x, z, radius - inset)) continue;

                double floor = bed != null ? bed.FloorY(x, z) : -depth;
                if (-floor < 2d * inset + 0.1d) continue;

                double y = floor + inset + rng.NextFloat() * (-floor - 2d * inset);
                double s = reefs.SignedDistance(x, y, z);
                return (x, y, z, s, r);
            }
        }

        private static Float3 V(CurrentField field, double x, double y, double z, double t) =>
            field.VelocityAt((float)x, (float)y, (float)z, t);

        private static (CurrentField Field, ReefGeometry Reefs, BedShape Bed, float Depth, string Name)[] Waters()
        {
            ReefGeometry small = ReefTank.Reefs();
            (BedShape bed, CurrentField big) = Round46.Value;
            big.SetReefs(Round46Reefs(bed, 15f));

            return new[]
            {
                (ReefTank.Streams(small), small, (BedShape)null, ReefTank.Depth, "small flat tank, fade 3 m"),
                (big, big.Reefs, bed, BedBeachTests.Depth, "round 46's tank, fade 15 m"),
            };
        }

        [Fact]
        public void TheWaterInTheRockAndOnItIsStill()
        {
            foreach (var w in Waters())
            {
                var rng = new Rng(5UL);
                int inside = 0, surface = 0;
                double worstSurface = 0d, worstCentimetre = 0d;

                for (int i = 0; i < 20000 && (inside < 500 || surface < 500); i++)
                {
                    (double x, double y, double z, double s, _) = Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.CapRadiusMetres + 0.5d, 0.05d);
                    double t = rng.NextFloat() * 4d * Period;

                    if (s < 0d)
                    {
                        Float3 v = V(w.Field, x, y, z, t);
                        Assert.Equal(Float3.Zero, v);
                        Assert.Equal(Float3.Zero, w.Field.AccelerationAt((float)x, (float)y, (float)z, t));
                        Assert.Equal(0f, w.Field.ReefFadeAt((float)x, (float)y, (float)z));
                        inside++;
                    }
                    else if (s < 0.5d)
                    {
                        // Onto the face along the distance's gradient, then 0.1 mm and 1 cm out: the
                        // water there is of the order of ξ² times the potential over the fade.
                        w.Reefs.SignedDistance(x, y, z, out _, out ReefGeometry.Distance d);
                        double fx = x - s * d.Gx, fy = y - s * d.Gy, fz = z - s * d.Gz;
                        double onFace = w.Reefs.SignedDistance(fx, fy, fz);
                        if (Math.Abs(onFace) > 1e-6) continue;

                        worstSurface = Math.Max(
                            worstSurface, V(w.Field, fx + 1e-4 * d.Gx, fy + 1e-4 * d.Gy, fz + 1e-4 * d.Gz, t).Magnitude);
                        worstCentimetre = Math.Max(
                            worstCentimetre, V(w.Field, fx + 1e-2 * d.Gx, fy + 1e-2 * d.Gy, fz + 1e-2 * d.Gz, t).Magnitude);
                        surface++;
                    }
                }

                _output.WriteLine(
                    $"{w.Name}: {inside} points in the rock, every one still; at {surface} points on its face the " +
                    $"water 0.1 mm out runs at most {worstSurface:0.0e+0} m/s and 1 cm out {worstCentimetre:0.0e+0} m/s");

                Assert.True(inside >= 100 && surface >= 100);
                Assert.True(worstSurface < 1e-6, $"water at the rock's face runs at {worstSurface} m/s");
            }
        }

        [Fact]
        public void TheFadedStreamsAreDivergenceFreeAndTheirTraceIsZero()
        {
            foreach (var w in Waters())
            {
                var rng = new Rng(7UL);
                double worstStencil = 0d, worstTrace = 0d, speed = 0d, worstVelocity = 0d;
                const double H = 0.0125;
                int n = 0;

                double disc = w.Reefs.CapRadiusMetres - 0.5d * w.Reefs.CapThicknessMetres;

                while (n < 1000)
                {
                    (double x, double y, double z, double s, double rho) =
                        Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.CapRadiusMetres + w.Reefs.FadeMetres, 0.2d);
                    if (!(s > 0.05d && s < w.Reefs.FadeMetres)) continue;

                    // The stencil straddling the disc-edge seam reads the Jacobian's step there.
                    if (Math.Abs(rho - disc) < 0.05d) continue;

                    double t = rng.NextFloat() * 4d * Period;

                    double dudx = (V(w.Field, x + H, y, z, t).X - V(w.Field, x - H, y, z, t).X) / (2d * H);
                    double dvdy = (V(w.Field, x, y + H, z, t).Y - V(w.Field, x, y - H, z, t).Y) / (2d * H);
                    double dwdz = (V(w.Field, x, y, z + H, t).Z - V(w.Field, x, y, z - H, t).Z) / (2d * H);
                    worstStencil = Math.Max(worstStencil, Math.Abs(dudx + dvdy + dwdz));

                    (Float3 velocity, _, float divergence) = w.Field.StreamsGradientAt((float)x, (float)y, (float)z, t);
                    worstTrace = Math.Max(worstTrace, Math.Abs(divergence));

                    Float3 sampled = V(w.Field, x, y, z, t);
                    worstVelocity = Math.Max(worstVelocity, (velocity - sampled).Magnitude);
                    speed = Math.Max(speed, sampled.Magnitude);
                    n++;
                }

                _output.WriteLine(
                    $"{w.Name}: worst |div| by a 12.5 mm stencil {worstStencil:0.0e+0} /s, the analytic Jacobian's " +
                    $"trace {worstTrace:0.0e+0} /s, the analytic route's velocity off the sampler's by " +
                    $"{worstVelocity:0.0e+0} m/s, fastest water in the fade {speed:0.0000} m/s");

                Assert.True(worstTrace < 1e-4, $"trace {worstTrace}");
                Assert.True(worstStencil < 1e-3 * speed, $"stencil divergence {worstStencil}");
                Assert.True(worstVelocity < 1e-5 * Math.Max(speed, 1e-3), $"velocity {worstVelocity}");
            }
        }

        [Fact]
        public void TheCurlOfTheFadedPotentialIsTheFadedVelocity()
        {
            // What the grid's face fluxes rest on.
            foreach (var w in Waters())
            {
                const double H = 0.02;
                var rng = new Rng(23UL);
                double sumSquared = 0d, sumReference = 0d, worst = 0d;
                int n = 0;

                while (n < 500)
                {
                    (double x, double y, double z, double s, _) =
                        Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.CapRadiusMetres + w.Reefs.FadeMetres, 0.1d);
                    if (!(s > 4d * H && s < w.Reefs.FadeMetres)) continue;

                    double t = rng.NextFloat() * 4d * Period;
                    CurrentField f = w.Field;

                    Float3 ax1 = f.PotentialAt((float)(x + H), (float)y, (float)z, t);
                    Float3 ax0 = f.PotentialAt((float)(x - H), (float)y, (float)z, t);
                    Float3 ay1 = f.PotentialAt((float)x, (float)(y + H), (float)z, t);
                    Float3 ay0 = f.PotentialAt((float)x, (float)(y - H), (float)z, t);
                    Float3 az1 = f.PotentialAt((float)x, (float)y, (float)(z + H), t);
                    Float3 az0 = f.PotentialAt((float)x, (float)y, (float)(z - H), t);

                    double perMetre = 1d / (2d * H);
                    var curl = new Float3(
                        (float)(((ay1.Z - ay0.Z) - (az1.Y - az0.Y)) * perMetre),
                        (float)(((az1.X - az0.X) - (ax1.Z - ax0.Z)) * perMetre),
                        (float)(((ax1.Y - ax0.Y) - (ay1.X - ay0.X)) * perMetre));

                    Float3 v = V(f, x, y, z, t);
                    Float3 difference = curl - v;

                    sumSquared += (double)difference.Magnitude * difference.Magnitude;
                    sumReference += (double)v.Magnitude * v.Magnitude;
                    worst = Math.Max(worst, difference.Magnitude);
                    n++;
                }

                double rmsError = Math.Sqrt(sumSquared / n);
                double rms = Math.Sqrt(sumReference / n);

                _output.WriteLine(
                    $"{w.Name}: curl of g·A against the velocity, RMS error {rmsError / rms:0.000%} of " +
                    $"{rms:0.00000} m/s, worst {worst:0.0e+0} m/s");

                Assert.True(rmsError < 1e-3 * rms, $"RMS error {rmsError / rms:0.000%}");
                Assert.True(worst < 2e-2 * rms, $"worst error {worst / rms:0.00%}");
            }
        }

        [Fact]
        public void TheAccelerationAgreesWithAStencilOffTheSeams()
        {
            foreach (var w in Waters())
            {
                var rng = new Rng(19UL);
                double disc = w.Reefs.CapRadiusMetres - 0.5d * w.Reefs.CapThicknessMetres;
                double sumFine = 0d, sumReference = 0d, worst = 0d, worstMagnitude = 0d;
                int n = 0;

                while (n < 400)
                {
                    (double x, double y, double z, double s, double rho) =
                        Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.CapRadiusMetres + w.Reefs.FadeMetres, 0.5d);
                    if (!(s > 0.1d && s < w.Reefs.FadeMetres)) continue;
                    if (Math.Abs(rho - disc) < 0.1d) continue;
                    if (y > -0.5d) continue;

                    double t = rng.NextFloat() * 4d * Period;

                    Float3 analytic = w.Field.AccelerationAt((float)x, (float)y, (float)z, t);
                    Float3 finer = Stencil(w.Field, (float)x, (float)y, (float)z, t, 0.0125f, 0.0025d);
                    Float3 difference = analytic - finer;

                    sumFine += (double)difference.Magnitude * difference.Magnitude;
                    sumReference += (double)finer.Magnitude * finer.Magnitude;
                    worst = Math.Max(worst, difference.Magnitude);
                    worstMagnitude = Math.Max(worstMagnitude, finer.Magnitude);
                    n++;
                }

                double ratio = Math.Sqrt(sumFine / sumReference);

                _output.WriteLine(
                    $"{w.Name}: the analytic acceleration against the 12.5 mm stencil, {ratio:0.000%} RMS, worst " +
                    $"{worst / worstMagnitude:0.00%} of the largest; RMS acceleration {Math.Sqrt(sumReference / n):0.0e+0} m/s²");

                Assert.True(ratio < 0.001, $"RMS {ratio:0.000%}");
                Assert.True(worst < 0.02 * worstMagnitude, $"worst {worst / worstMagnitude:0.00%}");
            }
        }

        private static Float3 Stencil(CurrentField field, float x, float y, float z, double t, float h, double dt)
        {
            Float3 u = field.VelocityAt(x, y, z, t);
            float perMetre = 1f / (2f * h);

            Float3 total =
                (field.VelocityAt(x + h, y, z, t) - field.VelocityAt(x - h, y, z, t)) * (perMetre * u.X) +
                (field.VelocityAt(x, y + h, z, t) - field.VelocityAt(x, y - h, z, t)) * (perMetre * u.Y) +
                (field.VelocityAt(x, y, z + h, t) - field.VelocityAt(x, y, z - h, t)) * (perMetre * u.Z);

            return total +
                   (field.VelocityAt(x, y, z, t + dt) - field.VelocityAt(x, y, z, t - dt)) * (float)(1d / (2d * dt));
        }

        [Fact]
        public void TheOpenWaterIsTheUnfadedWaterToTheBit()
        {
            // Beyond every reef's fade the sampler is StreamsAt itself.
            ReefGeometry reefs = ReefTank.Reefs();
            CurrentField faded = ReefTank.Streams(reefs);
            CurrentField plain = ReefTank.Streams(null);

            var rng = new Rng(29UL);
            int n = 0;

            for (int i = 0; i < 4000; i++)
            {
                float x = (float)(ReefTank.Radius + (rng.NextFloat() * 2d - 1d) * 0.7d * ReefTank.Radius);
                float z = (float)(ReefTank.Radius + (rng.NextFloat() * 2d - 1d) * 0.7d * ReefTank.Radius);
                float y = -rng.NextFloat() * ReefTank.Depth;
                if (!TankGeometry.Inside(x, z, ReefTank.Radius)) continue;
                if (reefs.SignedDistance(x, y, z) < reefs.FadeMetres + 1d) continue;

                double t = rng.NextFloat() * 4d * Period;
                Assert.Equal(plain.VelocityAt(x, y, z, t), faded.VelocityAt(x, y, z, t));
                Assert.Equal(plain.AccelerationAt(x, y, z, t), faded.AccelerationAt(x, y, z, t));
                Assert.Equal(plain.PotentialAt(x, y, z, t), faded.PotentialAt(x, y, z, t));
                n++;
            }

            Assert.True(n > 1000);
        }

        [Fact]
        public void TheFastestWaterWithinACapRadiusAtThreeFades()
        {
            // The brief's reading: three caps 8 m across in round 46's tank, fades of 10, 15 and
            // 20 m. "Within a cap radius" is read as water within 4 m of the rock (0 < s ≤ r_c),
            // sampled over the whole fade's cylinder from the surface to the floor across four
            // periods. The unfaded water at the same points is printed beside it, and the tank's
            // RMS knob is 0.1 m/s.
            (BedShape bed, CurrentField field) = Round46.Value;
            ReefGeometry saved = field.Reefs;

            try
            {
                foreach (float fade in new[] { 10f, 15f, 20f })
                {
                    ReefGeometry reefs = Round46Reefs(bed, fade);
                    var rng = new Rng(31UL);

                    double fastest = 0d, fastestPlain = 0d, sumSquare = 0d, sumPlainSquare = 0d, fastestAnywhere = 0d;
                    int within = 0, anywhere = 0;
                    (double X, double Y, double Z) where = default;

                    for (int i = 0; i < 40000; i++)
                    {
                        (double x, double y, double z, double s, _) =
                            Near(rng, reefs, bed, BedBeachTests.Depth, R46CapRadius + fade, 0.05d);
                        if (!(s > 0d)) continue;

                        double t = rng.NextFloat() * 4d * Period;

                        field.SetReefs(reefs);
                        Float3 v = V(field, x, y, z, t);
                        field.SetReefs(null);
                        Float3 u = V(field, x, y, z, t);

                        fastestAnywhere = Math.Max(fastestAnywhere, v.Magnitude);
                        anywhere++;

                        if (s > R46CapRadius) continue;

                        within++;
                        sumSquare += (double)v.Magnitude * v.Magnitude;
                        sumPlainSquare += (double)u.Magnitude * u.Magnitude;
                        fastestPlain = Math.Max(fastestPlain, u.Magnitude);

                        if (v.Magnitude > fastest)
                        {
                            fastest = v.Magnitude;
                            where = (x, y, z);
                        }
                    }

                    _output.WriteLine(
                        $"fade {fade} m: within {R46CapRadius} m of the rock ({within} samples) the fastest water is " +
                        $"{fastest:0.0000} m/s (at {where.X:0.0}, {where.Y:0.0}, {where.Z:0.0}; the rock's distance there " +
                        $"{reefs.SignedDistance(where.X, where.Y, where.Z):0.00} m), RMS {Math.Sqrt(sumSquare / within):0.0000} m/s; " +
                        $"unfaded at the same points: fastest {fastestPlain:0.0000}, RMS {Math.Sqrt(sumPlainSquare / within):0.0000} m/s; " +
                        $"fastest anywhere in the fade's cylinder {fastestAnywhere:0.0000} m/s ({anywhere} samples); " +
                        $"Courant bound {field.MaximumTransportSpeed:0.0000} m/s");

                    Assert.True(within > 1000);
                }
            }
            finally
            {
                field.SetReefs(saved);
            }
        }
    }
}
