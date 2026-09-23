using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;

namespace Evosim.Theatre
{
    /// <summary>
    /// Mode B for a run the console farm recorded: the same world, stepped again inside the
    /// Editor by <c>Evosim.Dynamics</c> rather than by PhysX.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It runs the farm's own loop and not a second copy of it.</b>
    /// <see cref="Evosim.Farm.Simulation"/> is the harness — reconcile, the divergence check, the
    /// seam wrap, the solver's step, the metabolic hand-off, growth, the placer — and it is
    /// consumed here as a package, exactly as <c>Evosim.Core</c> and <c>Evosim.Dynamics</c> are.
    /// A transcription of that loop into <c>Assets/Theatre</c> would be a second definition of
    /// what the world does, and the first time the two parted the theatre would be showing a
    /// cousin while saying it was the run. The only things the console farm does that are not in
    /// that class are the writers, the manifest and the report, and none of them touches a
    /// trajectory: <c>Program.Loop</c> is <c>while (...) sim.Step()</c> with rows written beside
    /// it. So the loop below is that <c>while</c>, minus everything that writes.
    /// </para>
    /// <para>
    /// <b>Nothing here touches Unity's physics, and that is the point.</b> No scene, no
    /// <c>ArticulationBody</c>, no simulation mode to set and restore, no job-worker count: the
    /// solver is managed C# in doubles, one creature at a time. <see cref="TheatreReplay"/>'s
    /// whole opening dance with <c>Physics.simulationMode</c>, gravity and
    /// <c>JobsUtility.JobWorkerCount</c> is absent because there is nothing in this engine for
    /// those settings to reach. What replaces it is the thread count, which the farm records and
    /// which is pace and not a realisation — <c>DynamicsWorld.Step</c> takes one
    /// <c>Parallel.For</c> path at every count, one included.
    /// </para>
    /// <para>
    /// <b>The one thing the Editor cannot control is the JIT.</b> Every .NET project that reports
    /// a digest sets <c>TieredCompilation=false</c>, because quick-JITted and optimised loops give
    /// different floating-point results on a rounding edge; Unity's Mono has no such switch. Mono
    /// is single-tier, so the expectation is that it does not matter — but it is an expectation
    /// and not a guarantee, and the identity check below is what turns it into a reading. A
    /// mismatch that appears here and nowhere else is the first place to look.
    /// </para>
    /// <para>
    /// <b>It writes nothing into the run directory.</b> The divergence dumps are given no path,
    /// the digest is not enabled, and no writer is opened. A viewer that could modify the record
    /// is a viewer whose recording cannot be trusted afterwards.
    /// </para>
    /// </remarks>
    public sealed class TheatreDynamicsReplay : IDisposable, ITheatreFrame
    {
        /// <summary>The word in <c>run.json</c> this replay is for.</summary>
        public const string EngineName = "dynamics";

        public RunRecord Record { get; private set; }

        /// <summary>The farm's harness, stepping Core's world with the project's own solver.</summary>
        public Simulation Sim { get; private set; }

        /// <summary>True when this build's source matches the source the run recorded.</summary>
        public bool Faithful { get; private set; }

        /// <summary>How this build differs from the recorded one, or null.</summary>
        public string SourceDifference { get; private set; }

        /// <summary>Threads the body pass is running on — the recording's own, where it said.</summary>
        public int Threads { get; private set; }

        /// <summary>The last census taken, refreshed every metabolic step.</summary>
        public WorldCensus Census;

        /// <summary>The per-sample comparison against the recording.</summary>
        public ReplayIdentity Identity { get; private set; }

        /// <summary>
        /// The checkpoint this world was carried on from, or null when it was founded from t=0.
        /// </summary>
        public LiveCheckpoint ContinuedFrom { get; private set; }

        /// <summary>Simulated seconds elapsed in the replay.</summary>
        public double ElapsedSeconds => Sim?.World.ElapsedSeconds ?? 0d;

        /// <summary>The last sample time the record holds — where a full replay ends.</summary>
        public double RecordedThroughSeconds =>
            Record.Samples.Count > 0 ? Record.Samples[Record.Samples.Count - 1].T : 0d;

        /// <summary>Physics steps taken, for a log line that wants one.</summary>
        public long Steps => Sim?.Steps ?? 0L;

        // Drained at each sample and discarded, exactly where Program.Loop's sampler drains them.
        // Not instrumentation the theatre wants — the point is that the queues inside World stay
        // bounded, and that the replay calls the same methods at the same moments the recording
        // did.
        private readonly List<AbsorptiveSample> _absorptiveScratch = new List<AbsorptiveSample>();

        private TheatreDynamicsReplay() { }

        /// <summary>
        /// Loads a farm-recorded run and builds its world. Returns null and sets
        /// <paramref name="refusal"/> when it cannot be replayed faithfully and
        /// <paramref name="allowMismatch"/> is false.
        /// </summary>
        public static TheatreDynamicsReplay Open(
            string runDirectory, bool allowMismatch, out string refusal)
        {
            refusal = null;

            var replay = new TheatreDynamicsReplay();

            try
            {
                replay.Record = RunRecord.Load(runDirectory);
            }
            catch (Exception e)
            {
                refusal = e.Message;
                return null;
            }

            if (!string.Equals(replay.Record.Engine, EngineName, StringComparison.Ordinal))
            {
                refusal =
                    "run.json says this run was stepped by '" + replay.Record.Engine +
                    "', and this replay steps Evosim.Dynamics. The two are different engines and " +
                    "neither can produce the other's trajectory.";
                return null;
            }

            // As the PhysX replay refuses it, and for the same reason: the assay fires from the
            // batch runner at a set time and this step path has no such event, so a replay would
            // be that world only until the injection.
            if (replay.Record.Config.InoculateAtSeconds > 0f)
            {
                refusal =
                    "This run injected an inoculum at t=" +
                    replay.Record.Config.InoculateAtSeconds.ToString("0.#") +
                    " s, and the theatre does not replay injections yet; from that moment what it " +
                    "produced would not be this run.";
                return null;
            }

            replay.SourceDifference = DifferenceFromRecording(replay.Record);
            replay.Faithful = replay.SourceDifference == null;

            if (!replay.Faithful && !allowMismatch)
            {
                refusal =
                    "This build is not the build that recorded the run, so what it produced would " +
                    "not be that run: " + replay.SourceDifference +
                    ". Allow a source mismatch to watch it anyway — it plays with a banner saying " +
                    "it is not a faithful replay.";
                return null;
            }

            float dt = replay.Record.PhysicsDtSeconds;

            // The farm's own arithmetic, through the farm's own method, so a step this replay
            // accepts is a step the recording's launcher would have accepted and the steps per
            // metabolic step are the same integer on both sides.
            int stepsPerMetabolic;
            try
            {
                dt = EnvBinding.ResolvePhysicsStep(dt, out stepsPerMetabolic);
            }
            catch (Exception e)
            {
                refusal = "run.json's physics step cannot be replayed: " + e.Message;
                return null;
            }

            if (replay.Record.MetabolicStepSeconds > 0d &&
                Math.Abs(replay.Record.MetabolicStepSeconds - EnvBinding.MetabolicStepSeconds) > 1e-9)
            {
                refusal =
                    "run.json says the economy stepped every " +
                    replay.Record.MetabolicStepSeconds.ToString("0.###") + " s and this build's is " +
                    EnvBinding.MetabolicStepSeconds.ToString("0.###") +
                    " s. Every sample time in the recording is denominated in that step, so a " +
                    "replay at another one is not off by a rounding — it is a different economy.";
                return null;
            }

            replay.Threads = replay.Record.Threads.HasValue && replay.Record.Threads.Value > 0
                ? replay.Record.Threads.Value
                : 1;

            // Program.Run's line, in Program.Run's place: Core's own per-cell loops take the same
            // count as the body pass, and the grid's numbers are the same bits at any count.
            Parallelism.Threads = replay.Threads;

            // D117's pool comes from the record's own pool/ directory, hash-checked against the
            // config, so the world drawn from here is the world the run drew from; a run without
            // a pool named loads null and the World takes an empty pool, which is the recorded
            // world. The refusals (a missing file, a changed byte) are TricklePoolFiles' own.
            IReadOnlyList<Genome> pool;
            try
            {
                pool = TricklePoolFiles.Load(replay.Record.Path, replay.Record.Config)?.Genomes;
            }
            catch (Exception e)
            {
                refusal = e.Message;
                return null;
            }

            var world = new World(replay.Record.Config, replay.Record.Seed, pool);

            // No run directory, so no divergence dumps: the theatre writes nothing into a record.
            // A body that diverges in the replay still dies as a counted Diverged death, which is
            // what the identity check would see, and the post-mortem stays the farm's to write.
            replay.Sim = new Simulation(
                world, replay.Record.Seed, dt, stepsPerMetabolic, replay.Threads, null);

            replay.Identity = new ReplayIdentity(replay.Record.Samples);

            replay.Refresh();
            return replay;
        }

        /// <summary>
        /// Opens a farm-recorded run at one of its checkpoints and carries it on from there.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The run comes from the checkpoint and not the other way round.</b> What a viewer
        /// names is a second and a place to find it; the world is then built from the
        /// <c>config.json</c> beside that checkpoint's own <c>checkpoints/</c> directory, which is
        /// the rule a resumed farm run follows and for the same reason — an environment or a
        /// directory differing by one knob would otherwise build a world the checkpoint cannot be
        /// put into, and say so only after everything else had been set up.
        /// </para>
        /// <para>
        /// <b>A source mismatch is a caveat here and never a refusal.</b> <see cref="Open"/> is
        /// called with the mismatch allowed, as the live mode always calls it, and the four
        /// digests the checkpoint carries are read on top of that and reported. The restored world
        /// is a cousin in every case — see <see cref="LiveCheckpoint"/> — so
        /// <see cref="Faithful"/> is false from here on whatever the hashes say, and the label and
        /// the console both carry the word.
        /// </para>
        /// </remarks>
        public static TheatreDynamicsReplay Continue(
            string what, double atSeconds, out string refusal)
        {
            LiveCheckpoint checkpoint = LiveCheckpoint.Find(what, atSeconds, out refusal);
            if (checkpoint == null) return null;

            TheatreDynamicsReplay replay = Open(checkpoint.SourceRun, true, out refusal);
            if (replay == null) return null;

            try
            {
                checkpoint.RestoreInto(replay);
            }
            catch (Exception e)
            {
                refusal =
                    "The checkpoint at " +
                    checkpoint.Seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                    " s could not be read back into this world: " + e.Message;

                replay.Dispose();
                return null;
            }

            replay.ContinuedFrom = checkpoint;

            // Whatever the digests said. The trajectory from the first step on is this runtime's,
            // and a viewer who is told "faithful" will believe it.
            replay.Faithful = false;

            replay.SourceDifference = replay.SourceDifference == null
                ? checkpoint.Label()
                : checkpoint.Label() + "; " + replay.SourceDifference;

            // The census, at the second the checkpoint stands at rather than at t=0: everything
            // that reads this replay — the label, the console, the identity check — asks it for
            // where the world is, and the restore has just moved it.
            replay.Refresh();

            return replay;
        }

        /// <summary>
        /// How this build differs from the source the run recorded, or null when it does not.
        /// </summary>
        /// <remarks>
        /// <b>Three hashes and not two, and none of them is <c>simHash</c>.</b> A run the console
        /// farm recorded never touched <c>Assets/Evosim</c>, so its manifest carries no
        /// <c>simHash</c> and comparing one would report a difference on every run forever. What
        /// decides its trajectory is <c>src/Evosim.Core</c>, <c>src/Evosim.Dynamics</c> and the
        /// harness in <c>src/Evosim.Farm</c>, and the manifest records a digest of each. The
        /// Editor's own <c>Assets/Evosim</c> is not in the answer because nothing under it runs
        /// here — which is exactly why this replay can be faithful on a worker whose
        /// <c>simHash</c> has moved.
        /// </remarks>
        public static string DifferenceFromRecording(RunRecord record)
        {
            string root = BuildIdentity.RepositoryRoot();

            string core = BuildIdentity.HashSourceTree(Path.Combine(root, "src", "Evosim.Core"));
            string dynamics = BuildIdentity.HashSourceTree(Path.Combine(root, "src", "Evosim.Dynamics"));
            string farm = BuildIdentity.HashSourceTree(Path.Combine(root, "src", "Evosim.Farm"));

            var sb = new System.Text.StringBuilder();

            Note(sb, "coreHash", record.CoreHash, core);
            Note(sb, "dynamicsHash", record.DynamicsHash, dynamics);
            Note(sb, "farmHash", record.FarmHash, farm);

            return sb.Length == 0 ? null : sb.ToString();
        }

        private static void Note(System.Text.StringBuilder sb, string name, string recorded, string here)
        {
            // Missing on either side is a difference, not a match — BuildIdentity's own rule.
            if (!string.IsNullOrEmpty(recorded) && !string.IsNullOrEmpty(here) &&
                string.Equals(recorded, here, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (sb.Length > 0) sb.Append("; ");

            sb.Append(name).Append(' ').Append(BuildIdentity.Short(recorded))
              .Append(" recorded, ").Append(BuildIdentity.Short(here)).Append(" here");
        }

        /// <summary>
        /// One physics step. Returns true on the steps the economy ran, where the census is
        /// refreshed and the record is checked.
        /// </summary>
        /// <param name="difference">
        /// The column that disagreed at this sample, or null — which is also what it reads when
        /// this step landed on no recorded sample at all. <paramref name="compared"/> is what
        /// separates the two.
        /// </param>
        /// <param name="compared">True when the record held a sample at this instant.</param>
        /// <param name="recorded">The sample compared against, when one was.</param>
        public bool Step(out bool compared, out string difference, out RunSample recorded)
        {
            compared = false;
            difference = null;
            recorded = default;

            if (Sim == null) return false;
            if (!Sim.Step()) return false;

            Refresh();

            // Where Program.Loop's sampler drains them, and discarded: the theatre writes nothing.
            World world = Sim.World;
            world.DrainLineageEvents();
            _absorptiveScratch.Clear();
            world.CollectAbsorptiveLog(_absorptiveScratch);
            Sim.DrainMotility(out _, out _, out _, out _);

            compared = Identity.Offer(Census, out difference, out recorded);
            return true;
        }

        /// <summary>One physics step, for a caller with nothing to say about the samples.</summary>
        public bool Step() => Step(out _, out _, out _);

        /// <summary>The world as the run's own report would have counted it.</summary>
        /// <remarks>
        /// Term for term <see cref="TheatreReplay"/>'s, and therefore term for term
        /// <c>Sampler.Write</c>'s: <c>meanHeight</c> is the mean of every living creature's
        /// <c>HeightY</c> in <c>World.Living</c> order, summed as a double, which is the sum the
        /// recording wrote. The order matters and the accumulator's type matters, because this is
        /// compared to the recorded double exactly.
        /// </remarks>
        private void Refresh()
        {
            World world = Sim.World;
            IReadOnlyList<Organism> living = world.Living;

            double height = 0d;
            int absorptive = 0, jointed = 0;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                height += creature.HeightY;

                bool hasAbsorptive = false;
                int dof = 0;

                for (int p = 0; p < creature.Phenotype.Parts.Count; p++)
                {
                    PhenotypePart part = creature.Phenotype.Parts[p];
                    if (part.CellTypeId == CellTypeIds.Absorptive) hasAbsorptive = true;
                    dof += part.JointType.DofCount();
                }

                if (hasAbsorptive) absorptive++;
                if (dof > 0) jointed++;
            }

            int alive = living.Count;
            double meanHeight = alive > 0 ? height / alive : 0d;

            Census = new WorldCensus
            {
                T = world.ElapsedSeconds,
                Alive = alive,
                Births = world.Births,
                Deaths = world.Deaths,
                Absorptive = absorptive,
                Photosynthetic = world.CountPhotosynthetic(),
                Jointed = jointed,
                MeanHeight = meanHeight,
                AuditResidual = world.AuditResidual,
                AuditPercent = world.EnergyIn > 0d ? 100d * world.AuditResidual / world.EnergyIn : 0d,
                MatterHere = world.Matter.DensityAt((float)meanHeight, 0),
                Diverged = world.Diverged,
                MatterStanding = world.StandingMatterUnits,
                MatterResidual = world.MatterResidual,
            };
        }

        /// <summary>One line for a HUD or a log: what the identity check currently says.</summary>
        public string IdentityLine() => Identity.Line();

        // ---------------------------------------------------------------- the camera's world

        public BedShape Bed => Sim?.World?.Bed;

        public int BodyCount => Sim?.World?.Living?.Count ?? 0;

        public Vector3 PositionOf(int index)
        {
            Organism creature = Sim.World.Living[index];
            return new Vector3(creature.X, creature.HeightY, creature.Z);
        }

        public Phenotype PhenotypeOf(int index) => Sim.World.Living[index].Phenotype;

        public bool AbsorptiveAt(int index) => Sim.World.Living[index].HasAbsorptiveTissue;

        public bool PhotosyntheticAt(int index) => Sim.World.Living[index].HasPhotosyntheticTissue;

        /// <summary>The one line a replay's picture carries — and which engine drew it.</summary>
        /// <remarks>
        /// A continuation says where it was picked up from and that it is a cousin, which is
        /// strictly more than <c>NOT A FAITHFUL REPLAY</c> says and replaces it: a viewer looking
        /// at the frame a week later needs the second as much as the warning.
        /// </remarks>
        public string LabelFor(string view, string look)
        {
            string arm = Record.ArmName ?? "run";

            string provenance =
                ContinuedFrom != null ? "  " + ContinuedFrom.Label() :
                Faithful ? "" : "  NOT A FAITHFUL REPLAY";

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}  t={1:0.#}s  alive {2}  {3}  {5}  dynamics{4}",
                arm, Census.T, Census.Alive, view, provenance, look);
        }

        public void Dispose()
        {
            Sim?.Dispose();
            Sim = null;
        }
    }
}
