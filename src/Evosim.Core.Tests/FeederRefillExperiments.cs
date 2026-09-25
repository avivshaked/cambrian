using System;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// What a feeder's own cell reads once it has started eating, in round 48's water. Round 48's
    /// pool founders drew at least 45 to 52% of their 1 m cell's snow per half-second step
    /// (logbook/0120, F4's section), so the density a body is priced at is the balance between its
    /// draw and whatever refills the cell. This asks what does the refilling: the mixing dial
    /// (0.02 m²/s in round 48), or the grid's transport of the current.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The world is round 48's own, built by <see cref="World"/> from a recorded
    /// <c>config.json</c>: the tank, the bed, the reefs, the streams, the 1 m snow grid and its
    /// stirring. Its snow is overwritten with a uniform density, and eight mouths in it each draw
    /// from their own cell at <c>density × clearance × dt</c>, the world's rule for an
    /// unsaturated mouth. After each draw the field settles, stirs and is carried as
    /// <c>World.Step</c> orders it. A third world, identical and with no mouth, is stepped beside
    /// the two with mouths, and each reading is the mouth's cell over the same cell in the
    /// control. So settling toward the bed, which empties the top layers of a uniform field,
    /// divides out.
    /// </para>
    /// <para>
    /// The mouths sit at half the tank's radius on four bearings and at 3 and 10 m, drawing at
    /// 1.6 m³/s on two bearings and 0.8 on the other two (round 48's founders' range). One field's
    /// mouths stand still and the other's drift with the water at their point. The whole is run
    /// with the world's transport off and then on, and the table prints the water's speed at each
    /// mouth's start.
    /// </para>
    /// <para>
    /// The config is read from the run directory <c>EVOSIM_EXPERIMENT_CONFIG</c> names, or from round 48 seed 1's run
    /// when that is unset. A machine without the run prints that and asserts nothing. Marked
    /// <c>Slow</c> as the other field experiments are, and run with
    /// <c>core-test.ps1 -All -Filter FeederRefillExperiments</c>.
    /// </para>
    /// </remarks>
    [Trait("Category", "Slow")]
    public sealed class FeederRefillExperiments
    {
        private readonly ITestOutputHelper _output;

        public FeederRefillExperiments(ITestOutputHelper output) => _output = output;

        private const string DefaultRun = "runs/r48-s1/2026-09-24-115115-e5a30c15";

        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        [Fact]
        public void WhatRefillsAFeedersCellInRound48sWater()
        {
            string path = Environment.GetEnvironmentVariable("EVOSIM_EXPERIMENT_CONFIG");
            if (string.IsNullOrEmpty(path))
            {
                string root = RepoRoot();
                path = root == null ? null : Path.Combine(root, DefaultRun);
            }

            if (path == null || !File.Exists(Path.Combine(path, "config.json")))
            {
                _output.WriteLine($"No config at {path ?? "(no repository root)"}; nothing measured.");
                return;
            }

            RunConfig config = RunDirectory.ReadConfig(path, out string mismatch);
            Assert.True(string.IsNullOrEmpty(mismatch), mismatch);

            // The pool of founders the trickle draws from (D117) is a founding matter: this
            // experiment founds nobody, and the fields and the water do not read it.
            config.FoundingTricklePoolShare = 0f;
            config.FoundingTricklePoolCount = 0;
            config.FoundingTricklePoolHash = "";

            const float seed = 1f;          // J/m3, the uniform snow the mouths start in
            const float dt = 0.5f;          // the metabolic step
            const int steps = 120;          // 60 s; the draw and the refill balance within seconds
            const double start = 3000d;     // a clock mid-way into the streams' 6,000 s period
            int[] marks = { 1, 10, 40, 120 };

            // Eight mouths in one field: four bearings at half the radius, two depths, clearance
            // 1.6 m3/s on two bearings and 0.8 on the other two. They start 19 m apart or 7 m
            // apart in depth, and a hole spreads a few metres in a minute, so none reads another's.
            float[] depths = { -3f, -10f };
            float[] bearings = { 0f, 0.5f * (float)Math.PI, (float)Math.PI, 1.5f * (float)Math.PI };
            float[] clearanceOf = { 1.6f, 0.8f, 1.6f, 0.8f };

            string repo = RepoRoot();
            string table = repo == null ? null : Path.Combine(repo, "scratch", "f4-probe", "refill-table.txt");
            void Say(string line)
            {
                _output.WriteLine(line);
                if (table != null) File.AppendAllText(table, line + Environment.NewLine);
            }

            if (table != null) Directory.CreateDirectory(Path.GetDirectoryName(table));
            Say($"config {path}");
            Say(FormattableString.Invariant(
                $"mixing {config.NutrientMixingDiffusivity} m2/s (h {config.HorizontalMixingDiffusivity}), ") +
                FormattableString.Invariant(
                $"snow cell {config.FieldCellMetres} m, current {config.Current.Mode} rms {config.Current.Speed} m/s"));
            Say("depth  bearing  speed m/s  transport  mouth   c m3/s  own/control at 0.5 s  5 s  20 s  60 s  refill 1/s");

            foreach (bool carry in new[] { false, true })
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var control = new World(config, 1UL);
                var still = (GridField)control.Nutrients;
                still.SeedUniform(seed);

                // One field of mouths that stand, one of mouths that drift with the water.
                var worlds = new[] { new World(config, 1UL), new World(config, 1UL) };
                var fields = new[] { (GridField)worlds[0].Nutrients, (GridField)worlds[1].Nutrients };
                foreach (GridField f in fields) f.SeedUniform(seed);

                float radius = control.TankRadiusMetres;
                int rings = still.PatchCount;
                int n = depths.Length * bearings.Length;
                var px = new float[2, n]; var py = new float[2, n]; var pz = new float[2, n];
                var cs = new float[n]; var speed0 = new float[n]; var dep = new float[n]; var bear = new float[n];
                var ratios = new double[2, n, marks.Length];
                var last = new double[2, n];

                for (int d = 0; d < depths.Length; d++)
                for (int b = 0; b < bearings.Length; b++)
                {
                    int k = d * bearings.Length + b;
                    float x = radius + 0.5f * radius * (float)Math.Cos(bearings[b]);
                    float z = radius + 0.5f * radius * (float)Math.Sin(bearings[b]);
                    for (int w = 0; w < 2; w++) { px[w, k] = x; py[w, k] = depths[d]; pz[w, k] = z; }
                    cs[k] = clearanceOf[b];
                    dep[k] = depths[d];
                    bear[k] = bearings[b] * 180f / (float)Math.PI;
                    Float3 v0 = config.Current.VelocityAt(x, depths[d], z, start);
                    speed0[k] = (float)Math.Sqrt(v0.X * v0.X + v0.Y * v0.Y + v0.Z * v0.Z);
                }

                FieldPoint At(int w, int k) =>
                    new FieldPoint(new Float3(px[w, k], py[w, k], pz[w, k]),
                                   TankGeometry.RingOf(px[w, k], pz[w, k], radius, rings));

                int mark = 0;
                for (int step = 0; step < steps; step++)
                {
                    double t = start + step * dt;

                    for (int w = 0; w < 2; w++)
                    {
                        GridField f = fields[w];
                        f.ClearDemand();
                        var want = new float[n];
                        for (int k = 0; k < n; k++)
                        {
                            want[k] = f.EdibleDensityAt(At(w, k)) * cs[k] * dt;
                            f.Demand(At(w, k), want[k]);
                        }
                        f.FreezeAvailability();
                        for (int k = 0; k < n; k++) f.Take(At(w, k), want[k] * f.ShareAt(At(w, k)));
                    }

                    foreach (GridField f in new[] { still, fields[0], fields[1] })
                    {
                        f.Settle(dt);
                        f.Mix(dt, config.NutrientMixingDiffusivity, config.HorizontalMixingDiffusivity);
                        if (carry) f.Advect(config.Current, t, dt, f.PatchWidthMetres);
                    }

                    for (int k = 0; k < n; k++)
                    {
                        Float3 v = config.Current.VelocityAt(px[1, k], py[1, k], pz[1, k], t);
                        px[1, k] += v.X * dt; py[1, k] += v.Y * dt; pz[1, k] += v.Z * dt;
                        if (py[1, k] > -0.5f) py[1, k] = -0.5f;
                    }

                    bool marking = mark < marks.Length && step + 1 == marks[mark];
                    for (int w = 0; w < 2; w++)
                    for (int k = 0; k < n; k++)
                    {
                        double c0 = still.EdibleDensityAt(At(w, k));
                        last[w, k] = c0 > 0d ? fields[w].EdibleDensityAt(At(w, k)) / c0 : double.NaN;
                        if (marking) ratios[w, k, mark] = last[w, k];
                    }
                    if (marking) mark++;
                }

                for (int w = 0; w < 2; w++)
                for (int k = 0; k < n; k++)
                {
                    // At a steady state the draw c·S equals the refill k·(A − S) per cubic metre
                    // of cell, so k = c·r / (1 − r) with r = S/A, read at the last step.
                    double r = last[w, k];
                    double refill = r < 1d ? cs[k] * r / (1d - r) : double.PositiveInfinity;
                    Say(FormattableString.Invariant(
                        $"{-dep[k],5:0}  {bear[k],7:0}  {speed0[k],9:0.000}  {(carry ? "on " : "off"),9}  {(w == 1 ? "drifts" : "still "),6}  {cs[k],6:0.0}  {ratios[w, k, 0],20:0.000}  {ratios[w, k, 1],4:0.000}  {ratios[w, k, 2],5:0.000}  {ratios[w, k, 3],5:0.000}  {refill,10:0.000}"));
                }

                Say(FormattableString.Invariant($"-- transport {(carry ? "on" : "off")}: {watch.Elapsed.TotalSeconds:0} s"));
            }
        }
    }
}
