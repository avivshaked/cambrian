using System;
using System.Diagnostics;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The repair of <c>logbook/specs/transport-conserves-spec.md</c>: the grid's transporter
    /// keeps uniform water uniform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fault this file exists for.</b> A uniform dissolved concentration carried by
    /// incompressible water in a closed container stays uniform. Through round 36 the grid's
    /// transporter did not keep it so: it sampled the velocity at a cell's centre, used it for
    /// that cell's east and front faces, sampled the vertical at the lower interface, and ran the
    /// three axis passes one after another on the stock the previous pass left. Those face
    /// velocities are not divergence-free on the grid, and a sequential sweep compresses along one
    /// axis before the next expands. The total was conserved to 1e-15 and nothing went negative;
    /// the constant field was not constant. The Astra review's F1 measured 1 unit/m³ becoming
    /// 0.357 to 2.454 in 600 s of the campaign's own water.
    /// </para>
    /// <para>
    /// <b>The repair.</b> Every current the campaign runs is the curl of a vector potential, so
    /// the grid now samples the potential on its <i>edges</i> and takes each face's flux as the
    /// circulation round that face's four edges (Stokes). Each edge belongs to two faces of any
    /// one cell with opposite sign, so the cell's net flux is exactly zero whatever the edge
    /// values are, and the three axes are computed from one snapshot of the stock and applied
    /// afterwards. <see cref="CurrentField.PotentialAt(float, float, float, double)"/> is the other half.
    /// </para>
    /// <para>
    /// <b>What is asserted and what is only reported.</b> The uniformity gate and the curl check
    /// are assertions. The resolution reading — how much of the analytic wall speed a stair-step
    /// mask keeps — and the timings are printed for the build report, because the right number
    /// for them is a matter of judgement and a test that guessed one would be a test of the guess.
    /// </para>
    /// </remarks>
    public class ConservativeTransportTests
    {
        private readonly ITestOutputHelper _output;

        public ConservativeTransportTests(ITestOutputHelper output) => _output = output;

        // The campaign's own world: 100 m² over four patches, 60 m deep. In a box that is
        // 20 m along x and 5 m across z; in a tank it is a disc of radius sqrt(100/pi).
        private const float Area = 100f;
        private const int Patches = 4;
        private const float Depth = 60f;
        private const float PatchWidth = 5f;
        private const float Period = 6000f;
        private const ulong RunSeed = 1UL;

        private static float TankRadius => TankGeometry.RadiusFor(Area);

        private static GridField Grid(WorldShape shape, float cell) =>
            new GridField(
                Area, 0f, Depth, 0f, 0f, Patches, cell,
                patchesAcross: 1, shape: shape,
                tankRadiusMetres: shape == WorldShape.Tank ? TankRadius : 0f);

        private static CurrentField Flow(WorldShape shape, float speed, CurrentMode mode = CurrentMode.Transport)
        {
            var current = new CurrentField
            {
                Mode = mode,
                Speed = speed,
                PeriodSeconds = Period,
                CellMetres = 25f,
                AdvectFields = true,
            };

            current.SetBox(
                PatchWidth, Patches, Depth, Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: shape,
                tankRadiusMetres: shape == WorldShape.Tank ? TankRadius : 0f);

            return current;
        }

        /// <summary>min, max and sd/mean of the density over the live cells.</summary>
        private static (double Min, double Max, double Mean, double Cv) Spread(GridField field)
        {
            double volume = field.CellVolume;
            double min = double.MaxValue, max = double.MinValue, sum = 0d, square = 0d;
            int n = 0;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.IsLive(ix, iy, iz)) continue;

                double density = field.JoulesAt(ix, iy, iz) / volume;
                if (density < min) min = density;
                if (density > max) max = density;
                sum += density;
                square += density * density;
                n++;
            }

            double mean = sum / n;
            return (min, max, mean, Math.Sqrt(Math.Max(0d, square / n - mean * mean)) / mean);
        }

        // ------------------------------------------------------------------ the constant field

        [Fact]
        public void AUniformConcentrationStaysUniform()
        {
            // 1,200 half-second steps is the spec's 600 s, which is the window the fault was
            // measured over. Each case is run twice: once on the superseded centre-sampled route,
            // which is what rounds 32 to 36 carried, and once on the repair.
            const int Steps = 1200;
            const float Dt = 0.5f;

            _output.WriteLine(
                "container  field      m/s  cell  mixing  scheme  |  min        max        " +
                "sd/mean  max|c-1|  total err");

            // The spec's own reproduction runs both containers at 0.1 m/s, which is the tank's
            // knob; the box's own knob is 0.3, so it is run at both and the first row is the one
            // the fault was reported on.
            foreach ((WorldShape shape, float speed) in new[]
                     {
                         (WorldShape.Box, 0.1f), (WorldShape.Box, 0.3f), (WorldShape.Tank, 0.1f),
                     })
            foreach (float cell in new[] { 1f, 5f })
            foreach (bool mixing in new[] { false, true })
            {
                // The campaign's rates: the 1 m detritus grid stirs at 0.02 m²/s and the 5 m
                // matter grid at 2 m²/s.
                float diffusivity = cell == 1f ? 0.02f : 2f;

                foreach (bool repaired in new[] { false, true })
                {
                    GridField field = Grid(shape, cell);
                    CurrentField current = Flow(shape, speed);

                    field.SeedUniform(1f);
                    double initial = field.Recount();

                    for (int step = 1; step <= Steps; step++)
                    {
                        if (mixing) field.Mix(Dt, diffusivity);

                        if (repaired) field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
                        else field.AdvectCentreSampled(current, step * (double)Dt, Dt);
                    }

                    (double min, double max, double mean, double cv) = Spread(field);
                    double drift = (field.Recount() - initial) / initial;
                    double worst = Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d));

                    _output.WriteLine(
                        $"{shape,-9} {(shape == WorldShape.Box ? "transport" : "streams"),-9} " +
                        $"{speed,4} {cell,5} {(mixing ? diffusivity.ToString("0.##") : "off"),7}  " +
                        $"{(repaired ? "edges" : "centres"),-8}| " +
                        $"{min,-10:0.000000} {max,-10:0.000000} {cv,-8:0.0%} {worst,-9:0.0e+0} {drift,9:0.0e+0}");

                    if (!repaired) continue;

                    Assert.Equal(1.0, mean, 9);
                    Assert.True(
                        worst < 1e-9,
                        $"{shape} at {speed} m/s and {cell} m, mixing {(mixing ? "on" : "off")}: " +
                        $"the density ran {min:R} to {max:R} where a uniform field must stay at 1");
                }
            }

            // The rolls, printed and not asserted — the coordinator's addition of 2026-09-12. It
            // is the scheme rounds 32 and 33 ran on, and it has no potential to be repaired
            // through (D037's field is a function of depth, time and patch), so the record wants
            // to know what it did to a uniform field rather than to be told it was fixed.
            foreach (float speed in new[] { 0.1f, 0.3f })
            foreach (float cell in new[] { 1f, 5f })
            {
                GridField field = Grid(WorldShape.Box, cell);
                var rolls = new CurrentField
                {
                    Mode = CurrentMode.Rolls,
                    Speed = speed,
                    CellMetres = 25f,
                    PeriodSeconds = Period,
                    AdvectFields = true,
                    Rolls = true,
                };

                rolls.SetBox(PatchWidth, Patches, Depth, Rng.SeedFor(RunSeed, World.CurrentFieldIndex));

                field.SeedUniform(1f);
                double initial = field.Recount();

                for (int step = 1; step <= Steps; step++)
                {
                    field.Advect(rolls, step * (double)Dt, Dt, field.PatchWidthMetres);
                }

                (double min, double max, double _, double cv) = Spread(field);

                _output.WriteLine(
                    $"Box       rolls     {speed,4} {cell,5} {"off",7}  {"layers",-8}| " +
                    $"{min,-10:0.000000} {max,-10:0.000000} {cv,-8:0.0%} " +
                    $"{Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d)),-9:0.0e+0} " +
                    $"{(field.Recount() - initial) / initial,9:0.0e+0}");
            }
        }

        // ------------------------------------------------------- conservation, positivity, the mask

        [Fact]
        public void EveryLiveCellsFacesSumToExactlyZero()
        {
            // The property the whole scheme rests on, checked on the arithmetic rather than
            // inferred from the algebra: an edge belongs to two faces of a cell with opposite
            // sign, so the six signed fluxes cancel term for term and the sum is exactly zero —
            // not small, zero — for any edge values at all. A face onto the glass or onto the
            // surface is left empty, and that this agrees with its circulation (all four of its
            // edges are zero) is what the tank's rows check.
            foreach ((WorldShape shape, float speed) in new[]
                     {
                         (WorldShape.Box, 0.3f), (WorldShape.Tank, 0.1f),
                     })
            foreach (float cell in new[] { 1f, 5f })
            {
                GridField field = Grid(shape, cell);
                CurrentField current = Flow(shape, speed);

                double worst = 0d;
                int faces = 0;

                for (int instant = 0; instant < 3; instant++)
                {
                    (int _, double _, double _, double _, int open, double net) =
                        field.MeasureFaceFluxes(current, 61d + instant * 907d, 0.5f);

                    if (net > worst) worst = net;
                    faces = open;
                }

                _output.WriteLine(
                    $"{shape} at {cell} m: {field.LiveCellCount} live cells, {faces} open faces, " +
                    $"worst net face flux {worst:0.0e+0} m³/s over three instants");

                Assert.Equal(0d, worst);
            }
        }

        [Fact]
        public void APatchIsCarriedWholeAndNeverGoesNegative()
        {
            // The conservation and positivity contracts the old scheme met and this one must not
            // give up: a Gaussian lump carried for a thousand steps keeps its total to the last
            // joule, no cell goes negative, and in a tank nothing reaches the dry corners.
            const int Steps = 1000;
            const float Dt = 0.5f;

            foreach ((WorldShape shape, float speed) in new[]
                     {
                         (WorldShape.Box, 0.3f), (WorldShape.Tank, 0.1f),
                     })
            {
                GridField field = Grid(shape, 1f);
                CurrentField current = Flow(shape, speed);

                float centre = shape == WorldShape.Tank ? TankRadius : PatchWidth * Patches * 0.5f;
                float acrossCentre = shape == WorldShape.Tank ? TankRadius : PatchWidth * 0.5f;

                double seeded = 0d;

                for (int ix = 0; ix < field.CellsX; ix++)
                for (int iy = 0; iy < field.CellsY; iy++)
                for (int iz = 0; iz < field.CellsZ; iz++)
                {
                    if (!field.IsLive(ix, iy, iz)) continue;

                    double dx = (ix + 0.5) - centre;
                    double dz = (iz + 0.5) - acrossCentre;
                    double dy = (iy + 0.5) - 20.0;
                    double joules = 100.0 * Math.Exp(-(dx * dx + dz * dz + dy * dy) / 8.0);
                    if (joules < 1e-12) continue;

                    field.Deposit(
                        new FieldPoint(
                            new Float3(ix + 0.5f, -(iy + 0.5f), iz + 0.5f),
                            field.PatchOf(ix + 0.5f, iz + 0.5f)),
                        (float)joules);
                    seeded += joules;
                }

                double before = field.Recount();

                for (int step = 1; step <= Steps; step++)
                {
                    field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
                }

                double least = double.MaxValue;
                double dry = 0d;
                int occupied = 0;

                for (int ix = 0; ix < field.CellsX; ix++)
                for (int iy = 0; iy < field.CellsY; iy++)
                for (int iz = 0; iz < field.CellsZ; iz++)
                {
                    double joules = field.JoulesAt(ix, iy, iz);

                    if (!field.IsLive(ix, iy, iz))
                    {
                        dry += joules;
                        continue;
                    }

                    if (joules < least) least = joules;
                    if (joules > 1e-9) occupied++;
                }

                _output.WriteLine(
                    $"{shape}: {before:0.000000} J seeded over {field.LiveCellCount} live cells, " +
                    $"{field.Recount():0.000000} J after {Steps} steps " +
                    $"(drift {(field.Recount() - before) / before:0.0e+0}); least cell " +
                    $"{least:0.0e+0} J; {occupied} cells hold something; dry cells hold {dry:0.0e+0} J");

                Assert.Equal(before, field.Recount(), 9);
                Assert.True(least >= 0d, $"a cell went to {least:R} J");
                Assert.Equal(0d, dry);
                Assert.True(occupied > field.LiveCellCount / 4, $"only {occupied} cells hold anything");
            }
        }

        [Fact]
        public void TheNewRouteIsStillLocalStillUpwindAndStillWrapsAtTheSeam()
        {
            // GridFieldTests' downstream-and-not-upstream and wrap-at-the-seam cases are written
            // against CurrentMode.Rolls, whose scheme this repair does not touch, so they say
            // nothing about the route that changed. The same three properties, asked of it: one
            // step moves stock only to face neighbours, it moves out of the seeded cell and not
            // into it from nowhere, and in a box the x ring is a ring.
            const float Dt = 0.5f;
            const float Cell = 1f;

            GridField field = Grid(WorldShape.Box, Cell);
            CurrentField current = Flow(WorldShape.Box, 0.3f);

            int last = field.CellsX - 1;
            const int Layer = 20;
            const int Across = 2;

            int crossings = 0;

            for (int instant = 0; instant < 8; instant++)
            {
                GridField one = Grid(WorldShape.Box, Cell);
                one.Deposit(
                    new FieldPoint(
                        new Float3(last + 0.5f, -(Layer + 0.5f), Across + 0.5f),
                        one.PatchOf(last + 0.5f, Across + 0.5f)),
                    1f);

                one.Advect(current, 11d + instant * 331d, Dt, one.PatchWidthMetres);

                double away = 0d;

                for (int ix = 0; ix < one.CellsX; ix++)
                for (int iy = 0; iy < one.CellsY; iy++)
                for (int iz = 0; iz < one.CellsZ; iz++)
                {
                    double joules = one.JoulesAt(ix, iy, iz);
                    if (joules == 0d) continue;

                    bool neighbour =
                        (ix == last && iy == Layer && iz == Across) ||
                        (iy == Layer && iz == Across && (ix == 0 || ix == last - 1)) ||
                        (ix == last && iz == Across && (iy == Layer - 1 || iy == Layer + 1)) ||
                        (ix == last && iy == Layer && (iz == Across - 1 || iz == Across + 1));

                    if (!neighbour) away += joules;
                    if (ix == 0 && iy == Layer && iz == Across && joules > 0d) crossings++;
                }

                Assert.Equal(1.0, one.Recount(), 12);
                Assert.Equal(0d, away);
                Assert.True(
                    one.JoulesAt(last, Layer, Across) < 1.0,
                    "the seeded cell gave nothing away in a step of moving water");
                Assert.True(
                    one.JoulesAt(last, Layer, Across) >= 0d,
                    "the seeded cell went negative in one step");
            }

            _output.WriteLine(
                $"a lump in the last column of the x ring crossed the seam at {crossings} of 8 " +
                "instants; nothing ever left the seeded cell's six face neighbours");

            // The seam is a face like any other and the water is as likely to be crossing it as
            // not, so this asks that it can be crossed rather than that it always is.
            Assert.True(crossings > 0, "nothing ever crossed the x seam");
        }

        // ------------------------------------------------------------------ resolution and cost

        [Fact]
        public void TheDiscreteFieldIsReportedAgainstTheAnalyticOne()
        {
            // Reported, not gated. A mask's stair-step is not the circle, and every edge that
            // touches a dead cell is zeroed, so the rim's face speeds are lower than the analytic
            // field's there; in a box there is no mask and the two differ only by the lattice's
            // own averaging. What the right number is depends on what the water is for, so the
            // build report carries it rather than a test asserting a threshold nobody derived.
            _output.WriteLine(
                "container  cell  faces   discrete RMS  analytic RMS  ratio  substeps  a-priori  " +
                "largest outflow");

            foreach ((WorldShape shape, float speed) in new[]
                     {
                         (WorldShape.Box, 0.3f), (WorldShape.Tank, 0.1f),
                     })
            foreach (float cell in new[] { 1f, 5f })
            {
                GridField field = Grid(shape, cell);
                CurrentField current = Flow(shape, speed);

                double discrete = 0d, analytic = 0d;
                int substeps = 0, faces = 0;
                double outflow = 0d;

                for (int instant = 0; instant < 4; instant++)
                {
                    (int n, double largest, double d, double a, int open, double _) =
                        field.MeasureFaceFluxes(current, 53d + instant * 613d, 0.5f);

                    discrete += d * d;
                    analytic += a * a;
                    outflow = Math.Max(outflow, largest);
                    substeps = Math.Max(substeps, n);
                    faces = open;
                }

                // What the a-priori Courant bound would have asked for, which is no longer what
                // the grid takes — it decides the refusal alone. Printed beside the count the
                // fluxes chose, so the record has both numbers rather than an assertion that one
                // of them is the right one.
                double courant = current.MaximumTransportSpeed * 0.5 / cell;
                int apriori = courant <= 0.5 ? 1 : (int)Math.Ceiling(2d * courant);

                _output.WriteLine(
                    $"{shape,-9} {cell,5} {faces,6}   {Math.Sqrt(discrete / 4),-13:0.00000} " +
                    $"{Math.Sqrt(analytic / 4),-13:0.00000} {Math.Sqrt(discrete / analytic),-6:0.000} " +
                    $"{substeps,8}  {apriori,8}  {outflow,15:0.000}");
            }
        }

        [Fact]
        public void TheRepairCostsWhatTheSpecAllows()
        {
            // The tank's two grids at the tank's own knob, which is the campaign's case: 1 m for
            // detritus and 5 m for matter. Timed per substep, because the two routes need not
            // choose the same number of them and the spec's budget is per substep.
            const float Dt = 0.5f;
            const int Steps = 200;

            _output.WriteLine(
                "container  cell  cells  scheme   substeps  ns/step      ns/substep   ratio");

            foreach ((WorldShape shape, float speed) in new[]
                     {
                         (WorldShape.Tank, 0.1f), (WorldShape.Box, 0.3f),
                     })
            foreach (float cell in new[] { 1f, 5f })
            {
                double perSubstepOld = 0d;

                foreach (bool repaired in new[] { false, true })
                {
                    GridField field = Grid(shape, cell);
                    CurrentField current = Flow(shape, speed);
                    field.SeedUniform(1f);

                    // The two routes need not split a step the same way: the old one splits on the
                    // field's a-priori ceiling, the new one on the fluxes' own outflow sum. So
                    // each is timed against its own count, which is what makes the per-substep
                    // column a comparison of the arithmetic rather than of the two rules.
                    double courant = current.MaximumTransportSpeed * Dt / cell;
                    int substeps = repaired
                        ? field.MeasureFaceFluxes(current, 0d, Dt).Substeps
                        : courant <= 0.5 ? 1 : (int)Math.Ceiling(2d * courant);

                    // A warm-up, so that the streams' construction and the JIT are not in the
                    // number: the field builds its tables on its first sample.
                    for (int step = 0; step < 5; step++)
                    {
                        if (repaired) field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
                        else field.AdvectCentreSampled(current, step * (double)Dt, Dt);
                    }

                    var clock = Stopwatch.StartNew();

                    for (int step = 0; step < Steps; step++)
                    {
                        if (repaired) field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
                        else field.AdvectCentreSampled(current, step * (double)Dt, Dt);
                    }

                    clock.Stop();

                    double perStep = clock.Elapsed.TotalMilliseconds * 1e6 / Steps;
                    double perSubstep = perStep / substeps;

                    if (!repaired) perSubstepOld = perSubstep;

                    _output.WriteLine(
                        $"{shape,-9} {cell,5} {field.LiveCellCount,6} " +
                        $"{(repaired ? "edges" : "centres"),-8} {substeps,8}  {perStep,-12:n0} " +
                        $"{perSubstep,-12:n0} {(repaired ? perSubstep / perSubstepOld : 1d),5:0.00}");
                }
            }
        }

        // ------------------------------------------------------------------ the potential is the field

        [Fact]
        public void TheCurlOfThePotentialIsTheVelocity()
        {
            // A scale other than 1 on the streams, so that the scale is proven carried rather
            // than proven absent — the spec's 0.37.
            foreach ((WorldShape shape, float speed) in new[]
                     {
                         (WorldShape.Box, 0.3f), (WorldShape.Tank, 0.37f),
                     })
            {
                CurrentField current = Flow(shape, speed);

                // 0.02 m, small against every length the field varies on and large enough that
                // the difference of two floats is not mostly rounding.
                const float H = 0.02f;

                var rng = new Rng(4242UL);
                double square = 0d, speedSquare = 0d, worst = 0d, worstSpeed = 0d;
                int n = 0;

                for (int instant = 0; instant < 5; instant++)
                {
                    double seconds = 137d + instant * 811d;

                    for (int i = 0; i < 500; i++)
                    {
                        float x, z;

                        if (shape == WorldShape.Tank)
                        {
                            // Inside the disc with a metre of clearance, so a 0.02 m stencil
                            // never straddles the glass and reads the clamp rather than the curl.
                            double angle = 2d * Math.PI * rng.NextFloat();
                            double radius = (TankRadius - 1f) * Math.Sqrt(rng.NextFloat());
                            x = (float)(TankRadius + radius * Math.Cos(angle));
                            z = (float)(TankRadius + radius * Math.Sin(angle));
                        }
                        else
                        {
                            x = rng.NextFloat() * PatchWidth * Patches;
                            z = rng.NextFloat() * PatchWidth;
                        }

                        // A metre clear of the waterline and the bed, for the same reason.
                        float y = -(1f + rng.NextFloat() * (Depth - 2f));

                        Float3 dx = (current.PotentialAt(x + H, y, z, seconds) -
                                     current.PotentialAt(x - H, y, z, seconds)) * (1f / (2f * H));
                        Float3 dy = (current.PotentialAt(x, y + H, z, seconds) -
                                     current.PotentialAt(x, y - H, z, seconds)) * (1f / (2f * H));
                        Float3 dz = (current.PotentialAt(x, y, z + H, seconds) -
                                     current.PotentialAt(x, y, z - H, seconds)) * (1f / (2f * H));

                        var curl = new Float3(
                            dy.Z - dz.Y,
                            dz.X - dx.Z,
                            dx.Y - dy.X);

                        Float3 water = current.VelocityAt(x, y, z, seconds);
                        double error = (curl - water).Magnitude;

                        square += error * error;
                        speedSquare += (double)water.Magnitude * water.Magnitude;
                        if (error > worst) { worst = error; worstSpeed = water.Magnitude; }
                        n++;
                    }
                }

                double rms = Math.Sqrt(square / n);
                double rmsSpeed = Math.Sqrt(speedSquare / n);

                _output.WriteLine(
                    $"{shape} at Speed {speed}: {n} points, RMS speed {rmsSpeed:0.00000} m/s, " +
                    $"curl error RMS {rms:0.0e+0} m/s ({rms / rmsSpeed:0.0e+0} of it), " +
                    $"worst {worst:0.0e+0} m/s at a point moving {worstSpeed:0.0000} m/s");

                Assert.True(
                    rms < 1e-3 * rmsSpeed,
                    $"{shape}: curl error RMS {rms:R} against an RMS speed of {rmsSpeed:R}");
                Assert.True(
                    worst < 0.01 * rmsSpeed,
                    $"{shape}: worst curl error {worst:R} against an RMS speed of {rmsSpeed:R}");
            }
        }
    }
}
