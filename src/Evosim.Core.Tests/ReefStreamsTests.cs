using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The streams around the reefs — <c>logbook/specs/reef-spec.md</c> §4, test 4: the curl of
    /// <c>g·A</c> is divergence-free, still in the rock and on it, agrees with the curl of the
    /// faded potential, and its closed-form acceleration agrees with a stencil; the fastest water
    /// beside the rock, and the grid's faded maximum and substeps, in round 46's own tank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three waters.</b> The small flat tank's three round reefs (<see cref="ReefTank.Reefs"/>)
    /// exercise the round cap's arithmetic; its two lobed, overlapping reefs
    /// (<see cref="ReefTank.Overlapping"/>) exercise the outline's and the product fade; round 46's
    /// floor (<see cref="BedBeachTests.Beach"/>: 22,000 m², 45 m, tilt 96 m, the shore at 1 m with a
    /// 15 m fade) at the ruled cover (a quarter of the surface in caps 6 to 16 m, 3 m ± 1, 2 m
    /// thick, stems a quarter of their caps, placed by the seed-1 stream) exercises the sloped path
    /// with the shore's fade inside the reefs'.
    /// </para>
    /// <para>
    /// <b>The seams, and the tolerances.</b> The rock's distance is C¹ and not C² on the surface
    /// where each cap's flat top meets its rim (<see cref="ReefTank.FlatEdge"/>), so the velocity's
    /// Jacobian steps there; the stencils are held away from it, as for the round reef, and every
    /// tolerance is the first build's. The outline's distance is approximate away from the rock
    /// but its derivatives are its own, so nothing about the water is looser for it.
    /// </para>
    /// </remarks>
    public class ReefStreamsTests
    {
        private readonly ITestOutputHelper _output;

        public ReefStreamsTests(ITestOutputHelper output) => _output = output;

        private const float Period = 6000f;

        /// <summary>Round 46's floor and water, one for the class: the streams' build is seconds.</summary>
        private static readonly Lazy<(BedShape Bed, CurrentField Field)> Round46 =
            new Lazy<(BedShape, CurrentField)>(() =>
            {
                BedShape bed = ReefTank.Beach(1UL);
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

        private static ReefGeometry Round46Reefs(BedShape bed, float fade) =>
            ReefGeometry.Place(
                ReefTank.Round46(fade: fade), BedBeachTests.Radius, bed, Rng.SeedFor(1UL, World.ReefPlacementIndex));

        /// <summary>
        /// A point near a reef, uniform in a cylinder of <paramref name="beyond"/> past a random
        /// reef's widest outline, with its signed distance and the distance to the nearest seam.
        /// The floor is <paramref name="bed"/>'s or flat at <paramref name="depth"/>.
        /// </summary>
        private static (double X, double Y, double Z, double S, double Seam) Near(
            Rng rng, ReefGeometry reefs, BedShape bed, float depth, double beyond, double inset)
        {
            while (true)
            {
                int reef = (int)(rng.NextFloat() * reefs.Count) % reefs.Count;
                double reach = 1.4d * reefs.CapRadius(reef) + beyond;
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
                return (x, y, z, s, ReefTank.SeamDistance(reefs, x, z));
            }
        }

        private static Float3 V(CurrentField field, double x, double y, double z, double t) =>
            field.VelocityAt((float)x, (float)y, (float)z, t);

        private static (CurrentField Field, ReefGeometry Reefs, BedShape Bed, float Depth, string Name)[] Waters()
        {
            ReefGeometry small = ReefTank.Reefs();
            ReefGeometry lobed = ReefTank.Overlapping();
            (BedShape bed, CurrentField big) = Round46.Value;
            big.SetReefs(Round46Reefs(bed, 8f));

            return new[]
            {
                (ReefTank.Streams(small), small, (BedShape)null, ReefTank.Depth, "small flat tank, three round reefs, fade 3 m"),
                (ReefTank.Streams(lobed), lobed, (BedShape)null, ReefTank.Depth, "small flat tank, two lobed reefs overlapping, fade 3 m"),
                (big, big.Reefs, bed, BedBeachTests.Depth, $"round 46's tank, {big.Reefs.Count} reefs at cover 0.25, fade 8 m"),
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

                for (int i = 0; i < 40000 && (inside < 500 || surface < 500); i++)
                {
                    (double x, double y, double z, double s, _) = Near(rng, w.Reefs, w.Bed, w.Depth, 0.5d, 0.05d);
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
                        // Onto the face by a few Newton steps along the distance's gradient (the
                        // outline's distance is first order, so one step is not quite there), then
                        // 0.1 mm and 1 cm out along the normal: the water there is of the order of
                        // ξ² times the potential over the fade.
                        double fx = x, fy = y, fz = z;
                        ReefGeometry.Distance d = default;

                        for (int k = 0; k < 4; k++)
                        {
                            double sk = w.Reefs.SignedDistance(fx, fy, fz, out _, out d);
                            double g2 = d.Gx * d.Gx + d.Gy * d.Gy + d.Gz * d.Gz;
                            fx -= sk * d.Gx / g2; fy -= sk * d.Gy / g2; fz -= sk * d.Gz / g2;
                        }

                        double onFace = w.Reefs.SignedDistance(fx, fy, fz, out _, out d);
                        if (Math.Abs(onFace) > 1e-6) continue;

                        double gl = Math.Sqrt(d.Gx * d.Gx + d.Gy * d.Gy + d.Gz * d.Gz);
                        double nx = d.Gx / gl, ny = d.Gy / gl, nz = d.Gz / gl;

                        worstSurface = Math.Max(
                            worstSurface, V(w.Field, fx + 1e-4 * nx, fy + 1e-4 * ny, fz + 1e-4 * nz, t).Magnitude);
                        worstCentimetre = Math.Max(
                            worstCentimetre, V(w.Field, fx + 1e-2 * nx, fy + 1e-2 * ny, fz + 1e-2 * nz, t).Magnitude);
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

                while (n < 1000)
                {
                    (double x, double y, double z, double s, double seam) =
                        Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.FadeMetres, 0.2d);
                    if (!(s > 0.05d && s < w.Reefs.FadeMetres)) continue;

                    // The stencil straddling the flat edge's seam reads the Jacobian's step there.
                    if (seam < 0.05d) continue;

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
                        Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.FadeMetres, 0.1d);
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
                double sumFine = 0d, sumReference = 0d, worst = 0d, worstMagnitude = 0d;
                int n = 0;

                while (n < 400)
                {
                    (double x, double y, double z, double s, double seam) =
                        Near(rng, w.Reefs, w.Bed, w.Depth, w.Reefs.FadeMetres, 0.5d);
                    if (!(s > 0.1d && s < w.Reefs.FadeMetres)) continue;
                    if (seam < 0.1d) continue;
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
            // Beyond every reef's fade the sampler is StreamsAt itself: the product fade is 1 there
            // and the faded route is never entered. Round and lobed, overlapping reefs alike.
            foreach (ReefGeometry reefs in new[] { ReefTank.Reefs(), ReefTank.Overlapping() })
            {
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
                    Assert.Equal(1d, reefs.FadeAt(x, y, z));
                    Assert.Equal(plain.VelocityAt(x, y, z, t), faded.VelocityAt(x, y, z, t));
                    Assert.Equal(plain.AccelerationAt(x, y, z, t), faded.AccelerationAt(x, y, z, t));
                    Assert.Equal(plain.PotentialAt(x, y, z, t), faded.PotentialAt(x, y, z, t));
                    n++;
                }

                Assert.True(n > 1000);
            }
        }

        [Fact]
        public void TheFastestWaterBesideTheRockAtTwoFades()
        {
            // The reading the pre-registration names: round 46's tank at the ruled cover, fades of
            // 8 and 15 m. "Beside the rock" is water within 4 m of it (0 < s ≤ 4), sampled from the
            // surface to the floor across four periods; the unfaded water at the same points is
            // printed beside it, and the tank's RMS knob is 0.1 m/s.
            (BedShape bed, CurrentField field) = Round46.Value;
            ReefGeometry saved = field.Reefs;

            try
            {
                foreach (float fade in new[] { 8f, 15f })
                {
                    ReefGeometry reefs = Round46Reefs(bed, fade);
                    var rng = new Rng(31UL);

                    double fastest = 0d, fastestPlain = 0d, sumSquare = 0d, sumPlainSquare = 0d, fastestAnywhere = 0d;
                    int within = 0, anywhere = 0;
                    (double X, double Y, double Z) where = default;

                    for (int i = 0; i < 40000; i++)
                    {
                        (double x, double y, double z, double s, _) =
                            Near(rng, reefs, bed, BedBeachTests.Depth, fade, 0.05d);
                        if (!(s > 0d)) continue;

                        double t = rng.NextFloat() * 4d * Period;

                        field.SetReefs(reefs);
                        Float3 v = V(field, x, y, z, t);
                        field.SetReefs(null);
                        Float3 u = V(field, x, y, z, t);

                        fastestAnywhere = Math.Max(fastestAnywhere, v.Magnitude);
                        anywhere++;

                        if (s > 4d) continue;

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
                        $"fade {fade} m, {reefs.Count} reefs covering {reefs.CoverGot:0.000}: within 4 m of the rock " +
                        $"({within} samples) the fastest water is {fastest:0.0000} m/s (at {where.X:0.0}, {where.Y:0.0}, " +
                        $"{where.Z:0.0}; the rock's distance there {reefs.SignedDistance(where.X, where.Y, where.Z):0.00} m), " +
                        $"RMS {Math.Sqrt(sumSquare / within):0.0000} m/s; unfaded at the same points: fastest " +
                        $"{fastestPlain:0.0000}, RMS {Math.Sqrt(sumPlainSquare / within):0.0000} m/s; fastest anywhere in the " +
                        $"fades {fastestAnywhere:0.0000} m/s ({anywhere} samples); Courant bound " +
                        $"{field.MaximumTransportSpeed:0.0000} m/s");

                    Assert.True(within > 1000);
                }
            }
            finally
            {
                field.SetReefs(saved);
            }
        }

        [Fact]
        public void TheGridsRefusalReadsTheFadedWater()
        {
            // The brief's reading: round 46's tank at cover 0.25 and a fade of 8 m, both of the
            // campaign's grids (1 m detritus, 5 m matter) at the half-second step. The faded
            // field's sampled maximum is printed against the open water's a-priori ceiling, with
            // the substep count the conservative transporter takes from its face fluxes.
            (BedShape bed, CurrentField field) = Round46.Value;
            ReefGeometry saved = field.Reefs;

            try
            {
                ReefGeometry reefs = Round46Reefs(bed, 8f);
                field.SetReefs(reefs);

                foreach (float cell in new[] { 5f, 1f })
                {
                    var grid = new GridField(
                        BedBeachTests.Area, 0f, BedBeachTests.Depth, 0f, 0f, 4, cell,
                        patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: BedBeachTests.Radius,
                        bed: bed, reefs: reefs);

                    Assert.Equal(0d, grid.FadedWaterMaximum);

                    (int substeps, double outflow, double discrete, double analytic, int faces, double worstNet) =
                        grid.MeasureFaceFluxes(field, 0d, 0.5f);

                    _output.WriteLine(
                        $"{cell} m cells, {reefs.Count} reefs covering {reefs.CoverGot:0.000}: the faded water's maximum at the " +
                        $"open faces within the fades {grid.FadedWaterMaximum:0.0000} m/s against the open water's ceiling " +
                        $"{field.MaximumTransportSpeed:0.0000} m/s; {substeps} substep(s) from the face fluxes (largest " +
                        $"outflow {outflow:0.000} of a cell), face speeds {discrete:0.0000} / {analytic:0.0000} m/s over " +
                        $"{faces} faces, worst net flux {worstNet:0.0e+0}");

                    Assert.True(grid.FadedWaterMaximum > 0d);
                    Assert.Equal(0d, worstNet);
                    Assert.InRange(substeps, 1, GridField.MaximumSubsteps);
                }

                // With no reefs the refusal reads the ceiling alone and measures nothing.
                field.SetReefs(null);
                var plain = new GridField(
                    BedBeachTests.Area, 0f, BedBeachTests.Depth, 0f, 0f, 4, 5f,
                    patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: BedBeachTests.Radius, bed: bed);
                plain.MeasureFaceFluxes(field, 0d, 0.5f);
                Assert.Equal(0d, plain.FadedWaterMaximum);
            }
            finally
            {
                field.SetReefs(saved);
            }
        }
    }
}
