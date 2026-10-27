using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.SwimProbe
{
    /// <summary>
    /// The brain-at-birth bench (<c>--brain-birth</c>). In r62p-t3-6201 about a fifth of the bred grazers born with
    /// their parent's body plan are slow at birth: they hang still in open water with their joints barely moving
    /// (logbook/specs/r62p-read/newborn-followup/README.md). Every one has a central brain (D146), and a newborn's
    /// brain starts from a fresh, zero state. For each child in an ids file this asks, from genomes alone and with
    /// each body held still:
    /// <list type="number">
    /// <item>does the child's brain, started fresh as birth starts it, drive its joints, or settle silent;</item>
    /// <item>does its parent's brain, started fresh, drive;</item>
    /// <item>(reads only, not world rules) the child's brain started from its parent's running state, and the
    /// child's brain started fresh plus a small random kick.</item>
    /// </list>
    ///
    /// <para>
    /// <b>The body is held, not simulated.</b> The world's physics step is never called: no fluid, no contact, no
    /// solve. Each step is <c>Senses.Sample()</c>, <c>Brain.Step(dt, DriveSignal, Senses, bend)</c> and
    /// <c>Drive.Drive(DriveSignal, BendSignal)</c>, which is the order <c>DynamicsWorld.StepOne</c> runs them in and
    /// the first two are what the probe's <c>Variant.BrainLoop</c> runs. The joints stay at q = 0, qd = 0.
    /// </para>
    /// <para>
    /// <b>Senses mode, "held-sampled":</b> <c>Senses.Sample()</c> runs every step on the body at rest, so the
    /// channels a brain reads are the real ones of a body that does not move. Joint angle and rate read 0 (the body
    /// never bends), flow reads 0 (the water at every link is 0 and the body is not moving), depth and up read what
    /// the placement gives (root at y = -20 m, as developed, the probe's default when it has no pose), chemical and
    /// energy read <c>SolverConfig.ConstantChemicalAndEnergy</c> (no field, no organism), and scent, contact, damage
    /// and the bed read 0. The drive-envelope senses (stroke, hold, ramp) are live: <c>Drive.Drive</c> is called each
    /// step, and it moves only the drive's own envelope (its torque lands in <c>Fext</c>, which is cleared and never
    /// read). Nothing a brain reads about the world is varied, so a child that stays silent here may still wake to
    /// a different input in the water. That is the bench's limit.
    /// </para>
    /// <para>
    /// <b>Difference from the probe's <c>BrainLoop</c>:</b> the bend array is passed to <c>Brain.Step</c> and to
    /// <c>Drive.Drive</c> when the drive is bend-and-power, as <c>DynamicsWorld.StepOne</c> does (the probe passes
    /// none). The drive channel measured is <c>DriveSignal</c>, the power output, which does not depend on it.
    /// </para>
    /// <para>
    /// <b>Body size.</b> A child is scaled to the birth fraction its <c>lineage.jsonl</c> birth row records
    /// (<c>bf</c>, a volume fraction) by the cube root, as <c>World.AtTheSameFraction</c> does
    /// (<c>Phenotype.ScaledFor</c>). A row with no <c>bf</c> is built at 1. A parent is built at 1 (its own fraction
    /// when it bred is not looked up). The brain is compiled from the genome and does not depend on the scale, so
    /// size changes only the body the held brain sits in.
    /// </para>
    /// <para>
    /// <b>Plan.</b> The genome rows of <c>genomes.jsonl.gz</c> carry no module counts, so a body is developed at
    /// the genome's own counts, as the probe develops a row with none.
    /// </para>
    /// <para>
    /// <b>Drive measures</b> are taken on each entry of <c>DriveSignal</c> (one per actuated degree of freedom) over
    /// the last half of the run: the mean, the peak-to-peak range, the RMS about the mean and the rate of sign
    /// changes about the mean. That rate is reported as a frequency: sign changes divided by twice the window, so a
    /// sinusoid of f Hz reads f. A channel is active when its peak-to-peak range is at least <c>--eps</c>; a body is
    /// silent when no channel is active; it is settled when every channel's largest change between consecutive steps
    /// over the last 1 s is under 1e-5.
    /// </para>
    /// <para>
    /// Deterministic: rows are written in the ids file's order and nothing depends on the thread count or the clock.
    /// </para>
    /// </summary>
    internal static class BrainBirth
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const double StartHeight = -20.0;          // root height in metres, the probe's default
        private const double SettledChange = 1e-5;          // a channel moves less than this between steps
        private const double SettledWindowSeconds = 1.0;

        private static readonly string[] VariantNames = { "parent", "fresh", "fromParent", "kick" };
        // 0 and 1 run with --swim; 2 and 3 only with --swim --passive (the same bodies, brain off). Entries of a run
        // without --passive stay null in RowResult.Swims.
        private static readonly string[] SwimNames = { "parentSwim", "childSwim", "parentPassive", "childPassive" };
        private const int ParentSwim = 0, ChildSwim = 1, ParentPassive = 2, ChildPassive = 3;

        private const double SlowSpeed = 0.03;              // m/s, under this a swim counts as slow

        public static int Run(
            string run, string idsFile, string outDir, double seconds, double parentWarm, double kick, ulong seed,
            double eps, string configPath, double dt, int threads, bool swim = false, bool passive = false,
            bool birthHeight = false)
        {
            if (run == null || idsFile == null || outDir == null)
            {
                Console.Error.WriteLine("--brain-birth needs --run, --ids and --out");
                return 2;
            }

            if (!(dt > 0.0 && dt <= 0.5) || !(seconds >= 4.0 * dt) || parentWarm < 0.0 || kick < 0.0 || !(eps > 0.0))
            {
                Console.Error.WriteLine("--brain-birth: need 0 < --dt <= 0.5, --seconds >= 4 steps, --parent-warm >= 0, --kick >= 0, --eps > 0");
                return 2;
            }

            Directory.CreateDirectory(outDir);

            RunConfig config = RunConfigJson.Read(
                File.ReadAllText(configPath ?? Path.Combine(run, "config.json")), out string mismatch);
            SolverConfig solver = SolverConfig.From(config, dt);
            solver.TankRadiusMetres = 0;       // alone: no glass
            solver.CreatureContact = false;    // alone: nobody to touch

            var cx = new Context
            {
                Config = config,
                Solver = solver,
                Dt = dt,
                Steps = (int)Math.Round(seconds / dt),
                WarmSteps = (int)Math.Round(parentWarm / dt),
                Kick = kick,
                Seed = seed,
                Eps = eps,
                Swim = swim,
                Passive = swim && passive,
                BirthHeight = swim && birthHeight,
            };

            // --swim: Program.Swim reads the probe's statics, which Main has not set on this path. Set them from this
            // bench's config, solver and step, once, before any thread starts; they are only read afterwards.
            if (swim) Program.ConfigureForBench(config, solver, dt, birthHeight ? -0.05 : -2.0);

            List<IdRow> rows = ReadIds(idsFile);

            var wantGenomes = new HashSet<long>();
            var wantBirths = new HashSet<long>();
            foreach (IdRow row in rows)
            {
                wantGenomes.Add(row.Id);
                wantGenomes.Add(row.Parent);
                wantBirths.Add(row.Id);
            }

            var notes = new List<string>();
            Dictionary<long, string> genomeRows = RecordFiles.GenomeRows(run, wantGenomes, notes);
            Dictionary<long, float> birthFractions = ReadBirthFractions(run, wantBirths);

            Console.WriteLine($"=== brain-birth bench: {run}");
            Console.WriteLine(
                $"  config {config.Hash()} {(mismatch == null ? "(matches)" : "(recorded " + mismatch + ")")}");
            Console.WriteLine(FormattableString.Invariant(
                $"  dt {dt} s, seconds {seconds} ({cx.Steps} steps, measured over the last half), parent-warm {parentWarm} s ({cx.WarmSteps} steps), eps {eps}, kick {kick}, seed {seed}"));
            Console.WriteLine(FormattableString.Invariant(
                $"  senses: held-sampled. The body is held at rest and the physics step is never called; Senses.Sample() runs each step, joint angle and rate, flow, scent, contact, damage and bed read 0, depth and up read the placement (root at y {StartHeight} m), chemical and energy read the constant {solver.ConstantChemicalAndEnergy}, and the drive-envelope senses are live (Drive.Drive each step)"));
            Console.WriteLine(
                $"  ids {rows.Count} rows; genomes found {genomeRows.Count} of {wantGenomes.Count}; birth fractions found {birthFractions.Count} of {wantBirths.Count}");
            foreach (string note in notes) Console.WriteLine("  note: " + note);
            if (swim)
            {
                Console.WriteLine(FormattableString.Invariant(
                    $"  swim: parentSwim (the parent at size 1) and childSwim (the child at its birth fraction) each swim alone in still water for {seconds} s ({cx.Steps} steps) under their own brain, with physics on (the probe's Swim, Variant.BrainWorld, no glass, no contact). netSpeed, pathSpeed and horizontal are over the last half of the swim; early is the root's net horizontal speed over 0 to 10 s"));
                if (seconds < 10.0) Console.WriteLine("  note: --seconds is under 10, so early and dyFirst10 are blank and the 2x2 uses netSpeed");
                if (cx.Passive)
                {
                    Console.WriteLine(
                        "  passive: parentPassive and childPassive are the same bodies at the same start, stepped through the probe's Swim with Variant.Passive (the brain is not stepped, the drive signal is zero; drag, contact and joint torques act as in Swim's hand-written loop)");
                }

                if (cx.BirthHeight)
                {
                    int have = 0;
                    foreach (IdRow row in rows) if (!double.IsNaN(row.Y0)) have++;
                    Console.WriteLine(FormattableString.Invariant(
                        $"  start: --birth-height. Each swim and passive body starts at its row's y0 (the parent at its child's y0), {have} of {rows.Count} rows have one; a row without y0 keeps {StartHeight} m and says so in its note. Swim clamps the start to [-40, -2] m, and says so in the note when it does"));
                }
                else
                {
                    Console.WriteLine(FormattableString.Invariant(
                        $"  start: root at y {StartHeight} m for every swim and passive body (no --birth-height)"));
                }

                Console.WriteLine(
                    "  columns after early: totalMass kg (effective, added mass folded in) and totalVolume m3 of the Creature; excessDensity kg/m3, the tissue excess density Fluid.Apply uses for the body (TissueExcessDensity x ExcessDensityFactor); netBuoyancy N, up positive, the vertical force of one Fluid.Apply on the body at its start, at rest in still water; dyFirst10 m, the root's vertical displacement over 0 to 10 s, up positive; bodyLength m, the probe's measure on the body as built");
            }

            var results = new RowResult[rows.Count];
            try
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) };
                Parallel.For(0, rows.Count, options, k =>
                {
                    results[k] = Process(cx, rows[k], genomeRows, birthFractions);
                });
            }
            catch (AggregateException ex)
            {
                foreach (Exception inner in ex.Flatten().InnerExceptions)
                {
                    Console.Error.WriteLine("brain-birth failed: " + inner.GetType().Name + ": " + inner.Message);
                }

                return 3;
            }

            WriteTables(outDir, rows, results);
            Summarise(rows, results);
            if (swim) SummariseSwim(rows, results);
            return 0;
        }

        // ================================================================ one child

        private sealed class Context
        {
            public RunConfig Config;
            public SolverConfig Solver;
            public double Dt;
            public int Steps, WarmSteps;
            public double Kick, Eps;
            public ulong Seed;
            public bool Swim, Passive, BirthHeight;
        }

        /// <summary>A failure that stops the whole bench instead of becoming a skipped row.</summary>
        private sealed class FatalBench : Exception
        {
            public FatalBench(string message) : base(message) { }
        }

        private sealed class IdRow
        {
            public long Id, Parent;
            public string Group;
            public string FirstSpeed;
            public double Y0 = double.NaN;   // the child's root y at its first frame; NaN when the ids file has no y0 column or a blank
        }

        private sealed class Stats
        {
            public int Dof, ActiveCount;
            public double MaxP2P = double.NaN, MedianHz = double.NaN;
            public bool Silent = true, Settled = true;
            public double[] Mean = new double[0], P2P = new double[0], Rms = new double[0], Hz = new double[0];
            public bool[] Active = new bool[0];
        }

        private sealed class VariantResult
        {
            public string Name;
            public Stats Stats;       // a held variant's measures; null when skipped or for a swim
            public Program.SwimAloneResult Swim;   // a swim variant's measures; null for a held variant
            public string Note = "";
            public bool Skipped => Stats == null && Swim == null;
        }

        private sealed class RowResult
        {
            public VariantResult[] Variants = new VariantResult[VariantNames.Length];
            public VariantResult[] Swims = new VariantResult[SwimNames.Length];   // entries stay null without --swim
        }

        /// <summary>A body held at rest, its brain run on the senses of that body.</summary>
        private sealed class Held
        {
            public readonly Creature Body;
            public readonly int Dof;
            private readonly bool _bend;

            public Held(Creature body)
            {
                Body = body;
                Dof = Math.Min(body.Brain.TotalDof, body.DriveSignal.Length);
                _bend = body.Drive.BendAndPower;
            }

            public void Step(double dt)
            {
                Body.Senses.Sample();
                Body.Brain.Step((float)dt, Body.DriveSignal, Body.Senses, _bend ? Body.BendSignal : null);
                Array.Clear(Body.Fext, 0, Body.Fext.Length);
                Array.Clear(Body.Tau, 0, Body.Tau.Length);
                Body.Drive.Drive(Body.DriveSignal, Body.BendSignal);
            }
        }

        private static RowResult Process(
            Context cx, IdRow row, Dictionary<long, string> genomeRows, Dictionary<long, float> birthFractions)
        {
            var result = new RowResult();
            genomeRows.TryGetValue(row.Id, out string childRow);
            genomeRows.TryGetValue(row.Parent, out string parentRow);
            float fraction = birthFractions.TryGetValue(row.Id, out float bf) ? bf : 1f;

            Held parentHeld = null, childHeld = null;
            string parentWhy = parentRow == null ? "skip: the parent's genome is not in genomes.jsonl.gz" : null;
            string childWhy = childRow == null ? "skip: the child's genome is not in genomes.jsonl.gz" : null;

            if (parentWhy == null)
            {
                try { parentHeld = Build(cx, parentRow, 1f); }
                catch (Exception ex) when (!(ex is FatalBench)) { parentWhy = "skip: " + Describe(ex); }
            }

            if (childWhy == null)
            {
                try { childHeld = Build(cx, childRow, fraction); }
                catch (Exception ex) when (!(ex is FatalBench)) { childWhy = "skip: " + Describe(ex); }
            }

            // parent: the parent's brain, fresh
            result.Variants[0] = parentHeld != null ? Measure(cx, "parent", parentHeld, "") : Skip("parent", parentWhy);

            // fresh: the child's brain, fresh, as birth makes it
            string bfNote = float.IsNaN(fraction) ? "" : FormattableString.Invariant($"bf {fraction:0.####}");
            result.Variants[1] = childHeld != null ? Measure(cx, "fresh", childHeld, bfNote) : Skip("fresh", childWhy);

            // fromParent: step the parent's held brain, then carry its state into a fresh child, then step the child
            if (parentHeld == null) result.Variants[2] = Skip("fromParent", parentWhy);
            else if (childHeld == null) result.Variants[2] = Skip("fromParent", childWhy);
            else if (parentHeld.Body.Links != childHeld.Body.Links)
            {
                result.Variants[2] = Skip(
                    "fromParent",
                    FormattableString.Invariant(
                        $"skip: part counts differ (child {childHeld.Body.Links}, parent {parentHeld.Body.Links})"));
            }
            else
            {
                try
                {
                    Held warm = Build(cx, parentRow, 1f);
                    Held child = Build(cx, childRow, fraction);
                    for (int s = 0; s < cx.WarmSteps; s++) warm.Step(cx.Dt);

                    var identity = new int[child.Body.Links];
                    for (int p = 0; p < identity.Length; p++) identity[p] = p;
                    child.Body.Brain.CarryStateFrom(warm.Body.Brain, identity);

                    result.Variants[2] = Measure(cx, "fromParent", child, "");
                }
                catch (Exception ex) when (!(ex is FatalBench))
                {
                    result.Variants[2] = Skip("fromParent", "skip: " + Describe(ex));
                }
            }

            // kick: the child fresh, every neuron output a small random draw
            if (childHeld == null) result.Variants[3] = Skip("kick", childWhy);
            else
            {
                try
                {
                    Held child = Build(cx, childRow, fraction);
                    KickOutputs(child.Body.Brain, cx.Kick, cx.Seed ^ (ulong)row.Id);
                    result.Variants[3] = Measure(cx, "kick", child, "");
                }
                catch (Exception ex) when (!(ex is FatalBench))
                {
                    result.Variants[3] = Skip("kick", "skip: " + Describe(ex));
                }
            }

            // --swim: the parent at size 1 and the child at its birth fraction, each alone in still water under its own
            // brain with physics on. A failure to build above (parentWhy, childWhy) is the same failure here.
            if (cx.Swim)
            {
                // The start: -20 m, or with --birth-height the child's y0 for both bodies (a row without one keeps -20 m
                // and says so). The same start and the same bodies for the passive runs.
                double start = StartHeight;
                string startNote = "";
                if (cx.BirthHeight)
                {
                    if (double.IsNaN(row.Y0)) startNote = FormattableString.Invariant($"y0 nan, start {StartHeight} m");
                    else start = row.Y0;
                }

                for (int s = 0; s < SwimNames.Length; s++)
                {
                    bool passive = s == ParentPassive || s == ChildPassive;
                    if (passive && !cx.Passive) continue;
                    bool parent = s == ParentSwim || s == ParentPassive;
                    string why = parent ? parentWhy : childWhy;
                    result.Swims[s] = why != null
                        ? Skip(SwimNames[s], why)
                        : SwimOne(
                            cx, SwimNames[s], parent ? parentRow : childRow, parent ? 1f : fraction,
                            JoinNote(parent ? "" : bfNote, startNote), passive, start);
                }
            }

            return result;
        }

        private static string JoinNote(string a, string b) =>
            a.Length == 0 ? b : b.Length == 0 ? a : a + "; " + b;

        private static VariantResult SwimOne(
            Context cx, string name, string genomeRow, float fraction, string note, bool passive, double start)
        {
            try
            {
                Program.SwimAloneResult swum = Program.SwimAlone(DevelopBody(cx, genomeRow, fraction), cx.Steps, passive, start);
                string why = swum.NoJoints ? "no actuated degree of freedom" : swum.Lost ? "lost: the body went non-finite" : "";
                if (Math.Abs(swum.StartUsed - start) > 1e-9)
                {
                    why = JoinNote(FormattableString.Invariant($"start {start:0.###} m clamped to {swum.StartUsed:0.###} m"), why);
                }

                return new VariantResult { Name = name, Swim = swum, Note = JoinNote(note, why) };
            }
            catch (Exception ex) when (!(ex is FatalBench))
            {
                return Skip(name, "skip: " + Describe(ex));
            }
        }

        /// <summary>The developed body, scaled to the birth fraction by the cube root as the world does.</summary>
        private static Phenotype DevelopBody(Context cx, string genomeRow, float fraction)
        {
            Genome genome = GenomeJson.Read(genomeRow);
            Phenotype adult = Developer.Develop(
                genome, cx.Config.Development, null, cx.Config.Shapes, null, null, cx.Config.CellTypes);

            return fraction > 0f && fraction < 1f
                ? adult.ScaledFor(cx.Config, (float)Math.Pow(fraction, 1.0 / 3.0))
                : adult;
        }

        private static Held Build(Context cx, string genomeRow, float fraction)
        {
            Phenotype body = DevelopBody(cx, genomeRow, fraction);

            var creature = new Creature(0, body, cx.Solver, cx.Config.Shapes);
            creature.PlaceAsDeveloped(new Vec3(0, StartHeight, 0), body);
            return new Held(creature);
        }

        private static VariantResult Skip(string name, string why) =>
            new VariantResult { Name = name, Note = why ?? "skip" };

        private static VariantResult Measure(Context cx, string name, Held held, string note)
        {
            float[][] series = Record(held, cx.Steps, cx.Dt);
            return new VariantResult { Name = name, Stats = Analyse(series, cx.Steps, cx.Dt, cx.Eps), Note = note };
        }

        private static string Describe(Exception ex)
        {
            string text = ex.GetType().Name + ": " + ex.Message;
            text = text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            return text.Length <= 160 ? text : text.Substring(0, 160);
        }

        /// <summary>Steps a held body and keeps every drive channel at every step: [channel][step].</summary>
        private static float[][] Record(Held held, int steps, double dt)
        {
            var series = new float[held.Dof][];
            for (int d = 0; d < series.Length; d++) series[d] = new float[steps];

            for (int s = 0; s < steps; s++)
            {
                held.Step(dt);
                for (int d = 0; d < series.Length; d++) series[d][s] = held.Body.DriveSignal[d];
            }

            return series;
        }

        // ================================================================ the measures

        private static Stats Analyse(float[][] series, int steps, double dt, double eps)
        {
            int dof = series.Length;
            var stats = new Stats
            {
                Dof = dof,
                Mean = new double[dof],
                P2P = new double[dof],
                Rms = new double[dof],
                Hz = new double[dof],
                Active = new bool[dof],
            };

            int from = steps / 2;
            int n = steps - from;
            int lastSteps = Math.Max(2, (int)Math.Round(SettledWindowSeconds / dt));
            int settledFrom = Math.Max(1, steps - lastSteps + 1);

            var activeHz = new List<double>();
            double maxP2P = 0.0;
            bool settled = true;

            for (int d = 0; d < dof; d++)
            {
                float[] x = series[d];
                double sum = 0.0, lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                for (int s = from; s < steps; s++)
                {
                    double v = x[s];
                    sum += v;
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }

                double mean = sum / n;
                double sq = 0.0;
                int changes = 0, last = 0;
                for (int s = from; s < steps; s++)
                {
                    double c = x[s] - mean;
                    sq += c * c;
                    int sign = c > 0.0 ? 1 : c < 0.0 ? -1 : 0;
                    if (sign == 0) continue;
                    if (last != 0 && sign != last) changes++;
                    last = sign;
                }

                stats.Mean[d] = mean;
                stats.P2P[d] = hi - lo;
                stats.Rms[d] = Math.Sqrt(sq / n);
                stats.Hz[d] = changes / (2.0 * n * dt);
                if (stats.P2P[d] > maxP2P) maxP2P = stats.P2P[d];

                if (stats.P2P[d] >= eps)
                {
                    stats.Active[d] = true;
                    stats.ActiveCount++;
                    activeHz.Add(stats.Hz[d]);
                }

                for (int s = settledFrom; s < steps; s++)
                {
                    if (Math.Abs((double)x[s] - x[s - 1]) >= SettledChange) { settled = false; break; }
                }
            }

            stats.MaxP2P = dof == 0 ? double.NaN : maxP2P;
            stats.Silent = stats.ActiveCount == 0;
            stats.Settled = settled;
            if (activeHz.Count > 0)
            {
                activeHz.Sort();
                int mid = activeHz.Count / 2;
                stats.MedianHz = activeHz.Count % 2 == 1 ? activeHz[mid] : 0.5 * (activeHz[mid - 1] + activeHz[mid]);
            }

            return stats;
        }

        // ================================================================ the kick

        /// <summary>
        /// Sets every neuron output of a fresh brain to a uniform draw in [-amount, amount], without touching Core:
        /// the state is written out with <c>Brain.WriteState</c>, the floats of the two output buffers are changed
        /// in the bytes, and it is read back with <c>Brain.ReadState</c>. The memory buffer is left at zero. A
        /// neuron gets one draw, written to both buffers, in neuron order.
        /// </summary>
        /// <remarks>
        /// Byte layout, Brain.cs:710-717 with StateIo.cs:29-35 and :82-86 (little-endian, BinaryWriter):
        /// "BRAN" 4 bytes; ElapsedSeconds double, 8; then three runs, each an int32 count and that many float32:
        /// _previous (count at 12, floats from 16), _current (count at 16 + 4n, floats from 20 + 4n) and _memory
        /// (count at 20 + 8n, floats from 24 + 8n; length 0 for a central brain), n the brain's NeuronCount.
        /// </remarks>
        private static void KickOutputs(Brain brain, double amount, ulong stream)
        {
            byte[] before = Dump(brain);

            // The round trip: write, read, write again, and the bytes must match before anything is changed.
            using (var ms = new MemoryStream(before, false))
            using (var reader = new BinaryReader(ms))
            {
                brain.ReadState(reader);
            }

            byte[] again = Dump(brain);
            if (!before.SequenceEqual(again))
            {
                throw new FatalBench("Brain.WriteState, ReadState, WriteState did not give the same bytes; the kick's layout cannot be trusted.");
            }

            int n = brain.NeuronCount;
            if (before.Length < 24 + 8 * n ||
                before[0] != (byte)'B' || before[1] != (byte)'R' || before[2] != (byte)'A' || before[3] != (byte)'N')
            {
                throw new FatalBench("Brain state does not open with BRAN and two runs of NeuronCount floats.");
            }

            int countPrevious = BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(12, 4));
            int countCurrent = BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(16 + 4 * n, 4));
            int countMemory = BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(20 + 8 * n, 4));
            if (countPrevious != n || countCurrent != n || before.Length != 24 + 8 * n + 4 * countMemory)
            {
                throw new FatalBench(FormattableString.Invariant(
                    $"Brain state layout differs from the one this bench reads: counts {countPrevious}, {countCurrent}, {countMemory} for {n} neurons, {before.Length} bytes."));
            }

            byte[] kicked = (byte[])before.Clone();
            var rng = new Rng(stream);
            float a = (float)amount;
            for (int i = 0; i < n; i++)
            {
                float v = rng.Range(-a, a);
                BinaryPrimitives.WriteSingleLittleEndian(kicked.AsSpan(16 + 4 * i, 4), v);
                BinaryPrimitives.WriteSingleLittleEndian(kicked.AsSpan(20 + 4 * n + 4 * i, 4), v);
            }

            using (var ms = new MemoryStream(kicked, false))
            using (var reader = new BinaryReader(ms))
            {
                brain.ReadState(reader);
            }
        }

        private static byte[] Dump(Brain brain)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    brain.WriteState(writer);
                    writer.Flush();
                }

                return ms.ToArray();
            }
        }

        // ================================================================ inputs

        private static List<IdRow> ReadIds(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0) throw new InvalidDataException("the ids file is empty: " + path);

            string[] head = lines[0].Split('\t');
            int ci = Array.IndexOf(head, "id"), cp = Array.IndexOf(head, "parent"), cg = Array.IndexOf(head, "group");
            int cs = Array.IndexOf(head, "firstSpeed");
            int cy = Array.IndexOf(head, "y0");   // optional: an ids file written before it existed stays readable
            if (ci < 0 || cp < 0 || cg < 0)
            {
                throw new InvalidDataException("the ids file needs columns id, parent and group; it has " + lines[0]);
            }

            var rows = new List<IdRow>();
            for (int l = 1; l < lines.Length; l++)
            {
                if (lines[l].Length == 0) continue;
                string[] p = lines[l].Split('\t');
                if (p.Length <= Math.Max(ci, Math.Max(cp, cg))) continue;
                if (!long.TryParse(p[ci], NumberStyles.AllowLeadingSign, Inv, out long id) ||
                    !long.TryParse(p[cp], NumberStyles.AllowLeadingSign, Inv, out long parent))
                {
                    continue;
                }

                rows.Add(new IdRow
                {
                    Id = id,
                    Parent = parent,
                    Group = p[cg],
                    FirstSpeed = cs >= 0 && cs < p.Length ? p[cs] : "",
                    Y0 = cy >= 0 && cy < p.Length &&
                         double.TryParse(p[cy], NumberStyles.Float, Inv, out double y0) &&
                         !double.IsNaN(y0) && !double.IsInfinity(y0)
                        ? y0
                        : double.NaN,
                });
            }

            return rows;
        }

        /// <summary>
        /// The <c>bf</c> of each wanted creature's birth row in <c>lineage.jsonl</c>. Read as text, line by line and
        /// shared with a writer: only the id and the fraction are taken from a row.
        /// </summary>
        private static Dictionary<long, float> ReadBirthFractions(string run, HashSet<long> want)
        {
            var found = new Dictionary<long, float>();
            string path = Path.Combine(run, "lineage.jsonl");
            if (!File.Exists(path) || want.Count == 0) return found;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("{\"e\":\"b\"", StringComparison.Ordinal)) continue;

                    int at = line.IndexOf("\"id\":", StringComparison.Ordinal);
                    if (at < 0) continue;
                    int start = at + 5, end = start;
                    while (end < line.Length && (line[end] == '-' || (line[end] >= '0' && line[end] <= '9'))) end++;
                    if (!long.TryParse(line.Substring(start, end - start), NumberStyles.AllowLeadingSign, Inv, out long id)) continue;
                    if (!want.Contains(id) || found.ContainsKey(id)) continue;

                    int b = line.IndexOf("\"bf\":", StringComparison.Ordinal);
                    if (b < 0) continue;
                    int vs = b + 5, ve = vs;
                    while (ve < line.Length && line[ve] != ',' && line[ve] != '}') ve++;
                    if (float.TryParse(line.Substring(vs, ve - vs), NumberStyles.Float, Inv, out float bf) &&
                        !float.IsNaN(bf) && !float.IsInfinity(bf))
                    {
                        found[id] = bf;
                    }

                    if (found.Count == want.Count) break;
                }
            }

            return found;
        }

        // ================================================================ outputs

        private static string F(double v, string format = "0.######") =>
            double.IsNaN(v) ? "nan" : v.ToString(format, Inv);

        private static void WriteTables(string outDir, List<IdRow> rows, RowResult[] results)
        {
            var main = new StringBuilder();
            // The swim columns come after note, so every earlier column keeps its place. They are blank for the four
            // held variants, and the held measures are "-" for the two swim variants.
            main.Append("id\tparent\tgroup\tvariant\tdof\tactive\tmaxP2P\tmedianHz\tsilent\tsettled\tnote");
            main.Append("\tnetSpeed\tpathSpeed\thorizontal\tdominantHz\tdominantAmp\tearly");
            main.Append("\ttotalMass\ttotalVolume\texcessDensity\tnetBuoyancy\tdyFirst10\tbodyLength\n");
            const string NoSwim = "\t\t\t\t\t\t\t\t\t\t\t\t";   // twelve empty swim columns (six swim, six size and buoyancy)

            var channels = new StringBuilder();
            channels.Append("id\tvariant\tchannel\tmean\tp2p\trms\thz\tactive\n");

            for (int k = 0; k < rows.Count; k++)
            {
                IdRow row = rows[k];
                foreach (VariantResult v in results[k].Variants)
                {
                    main.Append(row.Id.ToString(Inv)).Append('\t').Append(row.Parent.ToString(Inv)).Append('\t')
                        .Append(row.Group).Append('\t').Append(v.Name).Append('\t');

                    if (v.Skipped)
                    {
                        main.Append("-\t-\t-\t-\t-\t-\t").Append(v.Note).Append(NoSwim).Append('\n');
                        continue;
                    }

                    Stats s = v.Stats;
                    string note = s.Dof == 0 ? (v.Note.Length == 0 ? "no actuated degree of freedom" : v.Note + "; no actuated degree of freedom") : v.Note;
                    main.Append(s.Dof.ToString(Inv)).Append('\t').Append(s.ActiveCount.ToString(Inv)).Append('\t')
                        .Append(F(s.MaxP2P)).Append('\t').Append(F(s.MedianHz, "0.###")).Append('\t')
                        .Append(s.Silent ? "1" : "0").Append('\t').Append(s.Settled ? "1" : "0").Append('\t')
                        .Append(note).Append(NoSwim).Append('\n');

                    for (int d = 0; d < s.Dof; d++)
                    {
                        channels.Append(row.Id.ToString(Inv)).Append('\t').Append(v.Name).Append('\t')
                            .Append(d.ToString(Inv)).Append('\t').Append(F(s.Mean[d])).Append('\t')
                            .Append(F(s.P2P[d])).Append('\t').Append(F(s.Rms[d])).Append('\t')
                            .Append(F(s.Hz[d], "0.###")).Append('\t').Append(s.Active[d] ? "1" : "0")
                            .Append('\n');
                    }
                }

                foreach (VariantResult v in results[k].Swims)
                {
                    if (v == null) continue;   // no --swim

                    main.Append(row.Id.ToString(Inv)).Append('\t').Append(row.Parent.ToString(Inv)).Append('\t')
                        .Append(row.Group).Append('\t').Append(v.Name).Append('\t');

                    if (v.Skipped)
                    {
                        main.Append("-\t-\t-\t-\t-\t-\t").Append(v.Note).Append(NoSwim).Append('\n');
                        continue;
                    }

                    // a swim that has no number (no joints, or the body was lost) leaves its six swim columns blank.
                    // The buoyancy and size columns are read before any step, so a lost body keeps them; a body with no
                    // joint was not measured and leaves them blank; dyFirst10 needs the swim itself.
                    Program.SwimAloneResult w = v.Swim;
                    bool has = !w.NoJoints && !w.Lost;
                    bool sized = !w.NoJoints;
                    main.Append(w.Dof.ToString(Inv)).Append("\t-\t-\t-\t-\t-\t").Append(v.Note).Append('\t')
                        .Append(has ? F(w.NetSpeed, "0.#####") : "").Append('\t')
                        .Append(has ? F(w.PathSpeed, "0.#####") : "").Append('\t')
                        .Append(has ? F(w.Horizontal, "0.####") : "").Append('\t')
                        .Append(has ? F(w.DominantHz, "0.##") : "").Append('\t')
                        .Append(has ? F(w.DominantAmp, "0.###") : "").Append('\t')
                        .Append(has && !double.IsNaN(w.Early) ? F(w.Early, "0.#####") : "").Append('\t')
                        .Append(sized ? F(w.TotalMass, "0.######") : "").Append('\t')
                        .Append(sized ? F(w.TotalVolume, "0.########") : "").Append('\t')
                        .Append(sized ? F(w.Excess, "0.####") : "").Append('\t')
                        .Append(sized ? F(w.NetBuoyancy, "0.#######") : "").Append('\t')
                        .Append(has && !double.IsNaN(w.Dy10) ? F(w.Dy10, "0.#####") : "").Append('\t')
                        .Append(sized ? F(w.BodyLength, "0.#####") : "").Append('\n');
                }
            }

            int perRow = VariantNames.Length;
            if (rows.Count > 0) foreach (VariantResult v in results[0].Swims) if (v != null) perRow++;
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(outDir, "brain-birth.tsv"), main.ToString(), utf8);
            File.WriteAllText(Path.Combine(outDir, "brain-birth-channels.tsv"), channels.ToString(), utf8);
            Console.WriteLine("  wrote brain-birth.tsv (" + (rows.Count * perRow).ToString(Inv) +
                              " rows) and brain-birth-channels.tsv, per channel mean, range, RMS and rate");
        }

        private static void Summarise(List<IdRow> rows, RowResult[] results)
        {
            var groups = new List<string> { "standstill", "slow", "moving" };
            foreach (IdRow row in rows) if (!groups.Contains(row.Group)) groups.Add(row.Group);

            foreach (string group in groups)
            {
                var members = new List<int>();
                for (int k = 0; k < rows.Count; k++) if (rows[k].Group == group) members.Add(k);
                if (members.Count == 0) continue;

                Console.WriteLine();
                Console.WriteLine($"== {group} (children {members.Count})");
                Console.WriteLine("  variant       n   silent  settled  skipped");

                for (int v = 0; v < VariantNames.Length; v++)
                {
                    int n = 0, silent = 0, settled = 0, skipped = 0;
                    foreach (int k in members)
                    {
                        VariantResult r = results[k].Variants[v];
                        if (r.Skipped) { skipped++; continue; }
                        n++;
                        if (r.Stats.Silent) silent++;
                        if (r.Stats.Settled) settled++;
                    }

                    Console.WriteLine(
                        $"  {VariantNames[v],-11} {n,5} {Pct(silent, n),8} {Pct(settled, n),8} {skipped,8}");
                }

                // silent under fresh while the parent (fresh) is not silent, then what the two candidates do to them
                int target = 0, fromParentActive = 0, fromParentSkipped = 0, kickActive = 0, kickSkipped = 0;
                foreach (int k in members)
                {
                    VariantResult parent = results[k].Variants[0];
                    VariantResult fresh = results[k].Variants[1];
                    if (parent.Skipped || fresh.Skipped) continue;
                    if (!fresh.Stats.Silent || parent.Stats.Silent) continue;

                    target++;
                    VariantResult carried = results[k].Variants[2];
                    VariantResult kicked = results[k].Variants[3];
                    if (carried.Skipped) fromParentSkipped++; else if (!carried.Stats.Silent) fromParentActive++;
                    if (kicked.Skipped) kickSkipped++; else if (!kicked.Stats.Silent) kickActive++;
                }

                Console.WriteLine($"  silent under fresh with a parent that is not silent: {target}");
                Console.WriteLine($"    made active by fromParent: {fromParentActive} of {target} (skipped {fromParentSkipped})");
                Console.WriteLine($"    made active by kick: {kickActive} of {target} (skipped {kickSkipped})");
            }
        }

        private static bool HasSwimNumber(VariantResult r) =>
            r != null && !r.Skipped && !r.Swim.NoJoints && !r.Swim.Lost;

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return double.NaN;
            values.Sort();
            int mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : 0.5 * (values[mid - 1] + values[mid]);
        }

        /// <summary>
        /// The swim table per group (standstill, slow, moving, then any other), and the 2x2 of children: slow or
        /// moving in the world against slow or not alone. A swim counts toward n when it has a number: a skipped
        /// row, a body with no joint and a lost body are counted apart. "Slow alone" is <c>early</c> under
        /// <see cref="SlowSpeed"/>, and <c>netSpeed</c> for a row whose <c>early</c> is blank.
        /// </summary>
        private static void SummariseSwim(List<IdRow> rows, RowResult[] results)
        {
            var groups = new List<string> { "standstill", "slow", "moving" };
            foreach (IdRow row in rows) if (!groups.Contains(row.Group)) groups.Add(row.Group);

            foreach (string group in groups)
            {
                var members = new List<int>();
                for (int k = 0; k < rows.Count; k++) if (rows[k].Group == group) members.Add(k);
                if (members.Count == 0) continue;

                Console.WriteLine();
                Console.WriteLine($"== swim alone, {group} (children {members.Count})");
                Console.WriteLine(FormattableString.Invariant(
                    $"  variant         n  median netSpeed  median early  netSpeed<{SlowSpeed}  median netBuoyancy N  median dyFirst10 m  median bodyLength m  no-number (skipped/nojoint/lost)"));

                // childSwim first, then parentSwim, then the passive pair beside them when --passive ran
                foreach (int s in new[] { ChildSwim, ParentSwim, ChildPassive, ParentPassive })
                {
                    var net = new List<double>();
                    var early = new List<double>();
                    var buoy = new List<double>();
                    var dy = new List<double>();
                    var length = new List<double>();
                    int n = 0, slow = 0, skipped = 0, noJoint = 0, lost = 0;
                    foreach (int k in members)
                    {
                        VariantResult r = results[k].Swims[s];
                        if (r == null) continue;
                        if (r.Skipped) { skipped++; continue; }
                        if (r.Swim.NoJoints) { noJoint++; continue; }
                        if (r.Swim.Lost) { lost++; continue; }

                        n++;
                        net.Add(r.Swim.NetSpeed);
                        if (!double.IsNaN(r.Swim.Early)) early.Add(r.Swim.Early);
                        if (r.Swim.NetSpeed < SlowSpeed) slow++;
                        buoy.Add(r.Swim.NetBuoyancy);
                        if (!double.IsNaN(r.Swim.Dy10)) dy.Add(r.Swim.Dy10);
                        length.Add(r.Swim.BodyLength);
                    }

                    if (n + skipped + noJoint + lost == 0) continue;   // a variant this run did not make (no --passive)

                    Console.WriteLine(
                        $"  {SwimNames[s],-13} {n,5} {F(Median(net), "0.#####"),16} {F(Median(early), "0.#####"),13} {Pct(slow, n),15} {F(Median(buoy), "0.#####"),19} {F(Median(dy), "0.#####"),18} {F(Median(length), "0.#####"),19}  {skipped}/{noJoint}/{lost}");
                }

                // each child's own-brain speed minus its passive speed, over the children that have both numbers
                var diffs = new List<double>();
                int diffNet = 0;
                foreach (int k in members)
                {
                    double d = OwnOverPassive(results[k], out bool viaNet);
                    if (double.IsNaN(d)) continue;
                    diffs.Add(d);
                    if (viaNet) diffNet++;
                }

                if (results[members[0]].Swims[ChildPassive] != null)
                {
                    Console.WriteLine(FormattableString.Invariant(
                        $"  child own brain minus child passive, early m/s: n {diffs.Count}, median {F(Median(diffs), "0.#####")}, quartiles {F(Quantile(diffs, 0.25), "0.#####")} to {F(Quantile(diffs, 0.75), "0.#####")} (netSpeed in place of early for {diffNet})"));
                }
            }

            // the 2x2 for the children: in the world (standstill or slow, against moving) by alone (slow, or not)
            int slowSlow = 0, slowFast = 0, movingSlow = 0, movingFast = 0, usedNet = 0, otherGroup = 0, noNumber = 0;
            for (int k = 0; k < rows.Count; k++)
            {
                VariantResult r = results[k].Swims[1];
                if (!HasSwimNumber(r)) { noNumber++; continue; }

                bool inWorldSlow;
                if (rows[k].Group == "standstill" || rows[k].Group == "slow") inWorldSlow = true;
                else if (rows[k].Group == "moving") inWorldSlow = false;
                else { otherGroup++; continue; }

                double alone = r.Swim.Early;
                if (double.IsNaN(alone)) { alone = r.Swim.NetSpeed; usedNet++; }
                bool aloneSlow = alone < SlowSpeed;

                if (inWorldSlow) { if (aloneSlow) slowSlow++; else slowFast++; }
                else { if (aloneSlow) movingSlow++; else movingFast++; }
            }

            Console.WriteLine();
            Console.WriteLine(FormattableString.Invariant(
                $"== children, in the world against alone (alone slow = {(usedNet == 0 ? "early" : "early, netSpeed where early is blank")} under {SlowSpeed} m/s)"));
            Console.WriteLine("                          slow alone   not slow alone");
            Console.WriteLine($"  slow in the world       {slowSlow,10}   {slowFast,14}   (groups standstill and slow)");
            Console.WriteLine($"  moving in the world     {movingSlow,10}   {movingFast,14}   (group moving)");
            Console.WriteLine(
                $"  measure: early for {slowSlow + slowFast + movingSlow + movingFast - usedNet} rows, netSpeed for {usedNet}; left out: {noNumber} with no swim number (skipped, no joint or lost), {otherGroup} in another group");

            // The same 2x2 on the difference, own brain over passive: alone slow = the child's own-brain speed less its
            // passive speed (early, or netSpeed for both where either early is blank) under SlowSpeed. Printed with --passive.
            if (rows.Count == 0 || results[0].Swims[ChildPassive] == null) return;

            int diffSlowSlow = 0, diffSlowFast = 0, diffMovingSlow = 0, diffMovingFast = 0, diffUsedNet = 0, diffOther = 0, diffNone = 0;
            for (int k = 0; k < rows.Count; k++)
            {
                double diff = OwnOverPassive(results[k], out bool viaNet);
                if (double.IsNaN(diff)) { diffNone++; continue; }

                bool inWorldSlow;
                if (rows[k].Group == "standstill" || rows[k].Group == "slow") inWorldSlow = true;
                else if (rows[k].Group == "moving") inWorldSlow = false;
                else { diffOther++; continue; }

                if (viaNet) diffUsedNet++;
                bool overSlow = diff < SlowSpeed;
                if (inWorldSlow) { if (overSlow) diffSlowSlow++; else diffSlowFast++; }
                else { if (overSlow) diffMovingSlow++; else diffMovingFast++; }
            }

            Console.WriteLine();
            Console.WriteLine(FormattableString.Invariant(
                $"== children, in the world against own brain over passive (slow alone = own-brain speed minus passive speed under {SlowSpeed} m/s)"));
            Console.WriteLine("                          slow alone   not slow alone");
            Console.WriteLine($"  slow in the world       {diffSlowSlow,10}   {diffSlowFast,14}   (groups standstill and slow)");
            Console.WriteLine($"  moving in the world     {diffMovingSlow,10}   {diffMovingFast,14}   (group moving)");
            Console.WriteLine(
                $"  measure: early for {diffSlowSlow + diffSlowFast + diffMovingSlow + diffMovingFast - diffUsedNet} rows, netSpeed for {diffUsedNet}; left out: {diffNone} where the own-brain or the passive swim has no number, {diffOther} in another group");
        }

        /// <summary>
        /// A child's own-brain speed less its passive speed, m/s: early less early, or netSpeed less netSpeed (and
        /// <paramref name="viaNet"/> true) where either early is blank. NaN when either swim has no number or the row
        /// has no passive swim.
        /// </summary>
        private static double OwnOverPassive(RowResult result, out bool viaNet)
        {
            viaNet = false;
            VariantResult own = result.Swims[ChildSwim], passive = result.Swims[ChildPassive];
            if (!HasSwimNumber(own) || !HasSwimNumber(passive)) return double.NaN;

            if (!double.IsNaN(own.Swim.Early) && !double.IsNaN(passive.Swim.Early))
            {
                return own.Swim.Early - passive.Swim.Early;
            }

            viaNet = true;
            return own.Swim.NetSpeed - passive.Swim.NetSpeed;
        }

        /// <summary>The p-th quantile by linear interpolation between order statistics (type 7); NaN when empty.</summary>
        private static double Quantile(List<double> values, double p)
        {
            if (values.Count == 0) return double.NaN;
            var sorted = new List<double>(values);
            sorted.Sort();
            double at = p * (sorted.Count - 1);
            int lo = (int)Math.Floor(at);
            int hi = Math.Min(sorted.Count - 1, lo + 1);
            return sorted[lo] + (at - lo) * (sorted[hi] - sorted[lo]);
        }

        private static string Pct(int k, int n) =>
            n == 0 ? "-" : (100.0 * k / n).ToString("0.0", Inv) + "%";
    }
}
