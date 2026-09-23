using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A reading, not a gate: the shore's contour current at three fade widths and under a second
    /// form of the fade, over round 45's tank — the coordinator's follow-up to the beach build
    /// (<c>logbook/specs/beach-spec.md</c> §3). Marked <c>Slow</c>; run it with
    /// <c>core-test.ps1 -All -Filter BedBeachContour -ShowOutput</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is read.</b> The shipped fade (the coordinator's ruling of 2026-09-23) multiplies the
    /// sloped potential by <c>g = q·m</c>, the quintic <c>q</c> times a depth factor <c>m</c>
    /// that cancels the Piola stretch <c>D/d</c> on the ramp and turns over to 1 through a C²
    /// blend between 0.9·D and 1.1·D; its curl is <c>g·u' + ∇g × A'</c>,
    /// the stretched water and the contour term. The plain fade <c>q</c>, which shipped first,
    /// is no longer built into the field; it is read back from the shipped one, exactly, since
    /// <c>curl(g·A') = m·curl(q·A') + ∇m × (q·A')</c>, and both
    /// <c>curl(g·A')</c> and <c>g·A'</c> are what <see cref="CurrentField.VelocityAt(float, float, float, double)"/>
    /// and <see cref="CurrentField.PotentialAt(float, float, float, double)"/> return. The split of
    /// either into its two terms reads <c>A' = (g·A')/g</c>, with the fade and its slope rebuilt
    /// from the column's depth.
    /// </para>
    /// <para>
    /// <b>Ratios are to each form's own tank RMS</b>, so they are what the field would read after
    /// its own renormalisation. The shipped rows print the field's own bound; the plain rows' bound
    /// is an estimate, the shipped bound scaled by the ratio of the two forms' fastest samples on a
    /// polar lattice and by the scale that would bring the plain form's RMS back to the knob. The substep count is the a-priori
    /// Courant arithmetic, half a 1 m cell per substep at a 0.5 s step: <c>ceil(bound·0.5/0.5)</c>.
    /// </para>
    /// </remarks>
    public class BedBeachContourReadingTests
    {
        private readonly ITestOutputHelper _output;

        public BedBeachContourReadingTests(ITestOutputHelper output) => _output = output;

        private const int Rings = 4;
        private const float Speed = 0.1f;
        private const float Period = 6000f;
        private const ulong RunSeed = 1UL;
        private const double D = BedBeachTests.Depth;

        private static float Radius => BedBeachTests.Radius;

        private static CurrentField Streams(BedShape bed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = Speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(BedBeachTests.Area / Rings), Rings, BedBeachTests.Depth,
                Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);

            return field;
        }

        /// <summary>One sample's total, contour and stretched water under the chosen form.</summary>
        private static (double Total, double Contour, double Stretched) Sample(
            CurrentField field, BedShape bed, float fade, bool plain,
            float x, float y, float z, double t)
        {
            // The field is the shipped one, g = q·m with m the depth factor: v = curl(g·A') and
            // P = g·A'.
            Float3 v = field.VelocityAt(x, y, z, t);
            Float3 p = field.PotentialAt(x, y, z, t);

            double d = -bed.FloorY(x, z);
            (double hx, double hz) = bed.Gradient(x, z);

            double vx = v.X, vy = v.Y, vz = v.Z;
            double px = p.X, py = p.Y, pz = p.Z;
            double g, gd;

            if (plain)
            {
                // The plain fade q read back: curl(g·A') = m·curl(q·A') + ∇m × (q·A'), so
                // q·A' = P/m and curl(q·A') = (v − ∇m × P/m)/m. m is at least shore/D.
                (double m, double mOfX, _) = BedBeachStreamsTests.DepthFactor(d / D);
                double mx = -mOfX * hx / D;
                double mz = -mOfX * hz / D;

                px /= m; py /= m; pz /= m;
                vx = (vx - (-mz * py)) / m;
                vy = (vy - (mz * px - mx * pz)) / m;
                vz = (vz - (mx * py)) / m;

                (g, gd) = BedBeachStreamsTests.ShippedFade(d, fade: fade, depthFactor: false);
            }
            else
            {
                (g, gd) = BedBeachStreamsTests.ShippedFade(d, fade: fade);
            }

            // The contour term ∇g × A' = ∇g × P/g, with ∇g = −g'·(h_x, 0, h_z); the stretched
            // term is the rest.
            double cx = 0d, cy = 0d, cz = 0d;
            if (g > 0d && gd > 0d)
            {
                double gx = -gd * hx, gz = -gd * hz;
                cx = -gz * py / g;
                cy = (gz * px - gx * pz) / g;
                cz = gx * py / g;
            }

            double sx = vx - cx, sy = vy - cy, sz = vz - cz;

            return (
                Math.Sqrt(vx * vx + vy * vy + vz * vz),
                Math.Sqrt(cx * cx + cy * cy + cz * cz),
                Math.Sqrt(sx * sx + sy * sy + sz * sz));
        }

        private static double TankRms(CurrentField field, BedShape bed, float fade, bool depthForm)
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
                            double s = Sample(field, bed, fade, depthForm, x, y, z, t).Total;
                            sum += d * s * s;
                            weight += d;
                        }
                    }
                }
            }

            return Math.Sqrt(sum / weight);
        }

        /// <summary>The fastest sample on a polar lattice like the field's own bound walk.</summary>
        private static double PolarFastest(CurrentField field, BedShape bed, float fade, bool depthForm)
        {
            const int Radial = 24, Around = 32, Vertical = 20, Phases = 16;
            double fastest = 0d;

            for (int ph = 0; ph < Phases; ph++)
            {
                double t = ph * Period * 1.4142135623730951d / Phases;

                for (int ir = 0; ir < Radial; ir++)
                {
                    double r = Radius * ir / (Radial - 1.0);

                    for (int ia = 0; ia < Around; ia++)
                    {
                        double theta = 2.0 * Math.PI * ia / Around;
                        float x = (float)(Radius + r * Math.Cos(theta));
                        float z = (float)(Radius + r * Math.Sin(theta));
                        double d = -bed.FloorY(x, z);

                        for (int iy = 0; iy < Vertical; iy++)
                        {
                            float y = -(float)((iy + 0.5) * d / Vertical);
                            fastest = Math.Max(fastest, Sample(field, bed, fade, depthForm, x, y, z, t).Total);
                        }
                    }
                }
            }

            return fastest;
        }

        /// <summary>Points in columns whose water depth lies in (low, high), uniform in the disc.</summary>
        private static (double Rms, double Max, double ContourRms, double ContourMax, double StretchedRms)
            Region(CurrentField field, BedShape bed, float fade, bool depthForm, double low, double high, ulong seed)
        {
            var rng = new Rng(seed);
            double sq = 0d, max = 0d, csq = 0d, cmax = 0d, ssq = 0d;
            const int N = 4000;

            for (int i = 0; i < N; i++)
            {
                float x, z;
                double floor;
                while (true)
                {
                    float r = (float)((Radius - 0.5d) * Math.Sqrt(rng.NextFloat()));
                    double theta = 2d * Math.PI * rng.NextFloat();
                    x = (float)(Radius + r * Math.Cos(theta));
                    z = (float)(Radius + r * Math.Sin(theta));
                    floor = bed.FloorY(x, z);
                    if (-floor > low && -floor < high) break;
                }

                float y = (float)(floor + 0.05d + rng.NextFloat() * (-floor - 0.1d));
                double t = rng.NextFloat() * 4d * Period;

                (double total, double contour, double stretched) = Sample(field, bed, fade, depthForm, x, y, z, t);
                sq += total * total;
                max = Math.Max(max, total);
                csq += contour * contour;
                cmax = Math.Max(cmax, contour);
                ssq += stretched * stretched;
            }

            return (Math.Sqrt(sq / N), max, Math.Sqrt(csq / N), cmax, Math.Sqrt(ssq / N));
        }

        [Fact]
        [Trait("Category", "Slow")]
        public void TheFiveMetreMatterGridOverRound45sBeach()
        {
            // The matter half of the follow-up at the round's own numbers: which columns of the
            // 5 m matter grid are dead over the beach (floor shallower than 2.5 m), and how far
            // inward TankCellAt's walk carries a point there before it finds water.
            BedShape bed = BedBeachTests.Beach(RunSeed);

            var probe = new GridField(
                BedBeachTests.Area, 0f, BedBeachTests.Depth, 0f, 0f, Rings, 5f,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);

            int dead = 0, inside = 0, shoal = 0;
            double worst = 0d, sum = 0d;

            for (int ix = 0; ix < probe.CellsX; ix++)
            for (int iz = 0; iz < probe.CellsZ; iz++)
            {
                float cx = (ix + 0.5f) * probe.CellMetres;
                float cz = (iz + 0.5f) * probe.CellMetres;
                if (!TankGeometry.Inside(cx, cz, Radius)) continue;
                inside++;
                if (probe.ColumnIsLive(ix, iz)) continue;
                dead++;
                if (bed.FloorY(cx, cz) == -BedBeachTests.Shore) shoal++;

                var at = new FieldPoint(new Float3(cx, -0.5f, cz), 0);
                probe.Deposit(at, 1f);

                int fx = -1, fz = -1;
                for (int jx = 0; jx < probe.CellsX; jx++)
                for (int jy = 0; jy < probe.CellsY; jy++)
                for (int jz = 0; jz < probe.CellsZ; jz++)
                {
                    if (probe.JoulesAt(jx, jy, jz) > 0d) { fx = jx; fz = jz; }
                }

                Assert.Equal(1f, probe.Take(at, 1f), 6);

                double dx = (fx + 0.5d) * probe.CellMetres - cx;
                double dz = (fz + 0.5d) * probe.CellMetres - cz;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                worst = Math.Max(worst, distance);
                sum += distance;
            }

            _output.WriteLine(
                $"5 m matter cells over round 45's beach (seed {RunSeed}): {dead} of {inside} columns in " +
                $"the disc dead ({(double)dead / inside:0.0%}), {shoal} of them with their centre on the " +
                $"shoal; a point there reads the live column {(dead > 0 ? sum / dead : 0d):0.0} m inward " +
                $"on average and {worst:0.0} m at worst");
        }

        [Fact]
        [Trait("Category", "Slow")]
        public void TheContourCurrentAtThreeFadesAndTwoForms()
        {
            _output.WriteLine(
                "form            fade | tank RMS  | band RMS  band max | contour RMS  contour max  stretched RMS | " +
                "shelf 6-12 RMS  max | bound m/s (x RMS)  substeps");

            // The shipped form's rows first, with the field's own exact bound; then the plain
            // fade's rows, read back from the shipped field, as the record of the choice.
            foreach ((float fade, bool plain) in new[]
                     {
                         (15f, false), (40f, false), (15f, true), (25f, true), (40f, true),
                     })
            {
                BedShape bed = BedBeachTests.Beach(RunSeed, fade: fade);
                CurrentField field = Streams(bed);

                double tank = TankRms(field, bed, fade, plain);
                double shippedTank = plain ? TankRms(field, bed, fade, false) : tank;

                var band = Region(field, bed, fade, plain, BedBeachTests.Shore, BedBeachTests.Shore + fade, 31UL);
                var shelf = Region(field, bed, fade, plain, 6d, 12d, 37UL);

                double bound = field.MaximumTransportSpeed;
                string boundNote = " (exact)";

                if (plain)
                {
                    double shipped = PolarFastest(field, bed, fade, false);
                    double form = PolarFastest(field, bed, fade, true);

                    // The plain field would be rescaled so its RMS is the knob.
                    bound = bound * (form / shipped) * (shippedTank / tank);
                    boundNote = " (est.)";
                }

                int substeps = (int)Math.Ceiling(bound * 0.5d / 0.5d);

                _output.WriteLine(
                    $"{(plain ? "f (plain)" : "f*m(d/D) shipped   "),-20} {fade,4} | {tank,8:0.00000} | " +
                    $"{band.Rms / tank,8:0.00}x {band.Max / tank,8:0.00}x | {band.ContourRms / tank,10:0.00}x " +
                    $"{band.ContourMax / tank,11:0.00}x {band.StretchedRms / tank,13:0.00}x | " +
                    $"{shelf.Rms / tank,13:0.00}x {shelf.Max / tank,5:0.00}x | " +
                    $"{bound,6:0.000}{boundNote} ({bound / Speed,5:0.00}x the knob)  {substeps}");

                Assert.True(tank > 0d);
            }
        }
    }
}
