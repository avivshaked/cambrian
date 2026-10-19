using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;
using Evosim.Farm;

namespace Evosim.Theatre
{
    /// <summary>
    /// A farm checkpoint opened for the theatre: the file, the run it was written in, and the
    /// whole world read back out of it (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here knows the format.</b> Turning a second into a file is
    /// <c>Checkpoint.Resolve</c>, opening it and checking its trailer is
    /// <c>CheckpointReader</c>, and the state is read by the four calls <c>Program.Loop</c> makes
    /// in the order it makes them — the world, the harness, the sampler, and the loop's own four
    /// numbers between <c>LOOP</c> and <c>PEND</c>. A second reader written here would be a second
    /// definition of what a world is, and the first time the two parted the theatre would be
    /// showing a world the farm cannot produce.
    /// </para>
    /// <para>
    /// <b>What comes back is a cousin, and there is no arrangement in which it is not.</b> Even
    /// with all four digests equal, the Editor's Mono and the farm's RyuJIT do not agree on a
    /// double sum — <c>DynamicsReplayCheck</c> found them parting in <c>auditResidual</c> at the
    /// first sample, before any body had moved (CLAUDE.md, 2026-09-22). So the four hashes are
    /// read and reported, and a difference in them is a second reason rather than the reason. They
    /// refuse nothing: the owner's case for this path is a cousin watched on purpose, and a world
    /// that will not open shows nothing at all. What is not allowed is calling it faithful.
    /// </para>
    /// <para>
    /// <b>The sampler is read and thrown away.</b> Its ever-seen sets and window baselines are
    /// what separate a lineage from a standing crop in a report row, and the theatre writes no
    /// rows — but its bytes sit between the harness's and the loop's, so they are read into a
    /// sampler of their own rather than skipped by a count this class would have to know.
    /// </para>
    /// </remarks>
    public sealed class LiveCheckpoint
    {
        /// <summary>The file this was resolved to.</summary>
        public string Path { get; private set; }

        /// <summary>What the file says about the run that wrote it, before its payload.</summary>
        public CheckpointHeader Header { get; private set; }

        /// <summary>The run directory the checkpoint was written in.</summary>
        public string SourceRun { get; private set; }

        /// <summary>The simulated second the world in it stands at.</summary>
        public double Seconds => Header != null ? Header.Seconds : 0d;

        /// <summary>
        /// Which of the four source digests differ from what this Editor's packages hash to, one
        /// line each. Empty when none do, which still does not make the restore faithful.
        /// </summary>
        public IReadOnlyList<string> Differences { get; private set; } = Array.Empty<string>();

        /// <summary>The names alone of those four, which is what a label has room for.</summary>
        public IReadOnlyList<string> DifferingFields { get; private set; } = Array.Empty<string>();

        /// <summary>Bodies the solver holds after the restore.</summary>
        public int Bodies { get; private set; }

        /// <summary>Links across those bodies — what a view of them has to draw.</summary>
        public int Links { get; private set; }

        /// <summary>The metabolic step count the run had reached, from the payload's LOOP block.</summary>
        public int MetabolicSteps { get; private set; }

        /// <summary>The fastest body the run had seen, and when.</summary>
        public double BestSpeed { get; private set; }

        public double BestSpeedAtSeconds { get; private set; }

        /// <summary>Whether the run's inoculation assay had already fired.</summary>
        public bool AssayFired { get; private set; }

        private LiveCheckpoint() { }

        /// <summary>
        /// Finds the one file a continuation will read, and the run directory around it.
        /// </summary>
        /// <param name="what">
        /// A <c>.ckpt</c> file, the run directory holding a <c>checkpoints/</c>, or the arm
        /// directory above it — <c>Checkpoint.Resolve</c>'s three, so that a viewer may point at
        /// whichever of them they have to hand.
        /// </param>
        /// <param name="atSeconds">
        /// The second to continue from, or 0 for the last checkpoint there is. A second the run
        /// never wrote one at resolves to the last one at or before it, because a restore has to
        /// land on a world that existed.
        /// </param>
        public static LiveCheckpoint Find(string what, double atSeconds, out string refusal)
        {
            refusal = null;

            if (string.IsNullOrWhiteSpace(what))
            {
                refusal =
                    "No checkpoint named. Point Checkpoint Path at a run directory that holds a " +
                    "checkpoints/ directory, at the arm directory above it, or at one .ckpt file, " +
                    "or launch with EVOSIM_THEATRE_CHECKPOINT.";

                return null;
            }

            string path;
            CheckpointHeader header;

            try
            {
                path = Checkpoint.Resolve(what.Trim().Trim('"'), atSeconds);
                header = CheckpointReader.ReadHeader(path);
            }
            catch (Exception e)
            {
                refusal = e.Message;
                return null;
            }

            // checkpoints/NNNNNNNNN.ckpt, so the run directory is two levels up. The run's own
            // config.json is what the world has to be built from, exactly as a resumed farm run
            // builds it from there rather than from the launcher's environment.
            string run = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path));

            if (string.IsNullOrEmpty(run) || !File.Exists(System.IO.Path.Combine(run, "config.json")))
            {
                refusal =
                    "'" + path + "' has no run directory around it: a checkpoint is read back " +
                    "into a world built from the config.json beside its checkpoints/ directory, " +
                    "and there is none at '" + run + "'.";

                return null;
            }

            return new LiveCheckpoint { Path = path, Header = header, SourceRun = run };
        }

        /// <summary>
        /// Reads the whole world out of the file and into a replay that has just been opened.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Before a step is taken and after nothing else has been.</b> <c>Program.Loop</c> reads
        /// its checkpoint before the first row and before the first step, so that everything after
        /// it runs against the restored world; the same is true here, and a restore into a world
        /// that has already been stepped would be a world with two histories in it.
        /// </para>
        /// <para>
        /// <b>The refusals are the farm's own.</b> A checkpoint from another seed, another layout
        /// version, another space or another population is refused inside <c>World.ReadState</c>
        /// and <c>Simulation.ReadState</c> with a message that says which; none of them is
        /// repeated here, because a second copy of a refusal is a second thing to keep true.
        /// </para>
        /// </remarks>
        public void RestoreInto(TheatreDynamicsReplay replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));

            Simulation sim = replay.Sim;
            World world = sim.World;

            if (Math.Abs(Header.PhysicsStepSeconds - sim.PhysicsDt) > 1e-9f)
            {
                throw new InvalidDataException(
                    "The checkpoint was taken in a world stepping at " +
                    Header.PhysicsStepSeconds.ToString(CultureInfo.InvariantCulture) +
                    " s and this one steps at " +
                    sim.PhysicsDt.ToString(CultureInfo.InvariantCulture) +
                    " s. A per-step term changed is a different realisation and not a different " +
                    "moment in this one (logbook/0052).");
            }

            using (CheckpointReader reader = CheckpointReader.Open(Path))
            {
                BinaryReader r = reader.Reader;

                StateIo.Tag(r, "PAYL");
                world.ReadState(r);
                sim.ReadState(r);

                // Read into a sampler of its own and dropped: see the class remarks. A pool
                // world's sampler carries the pool window's baseline (D117), so the throwaway
                // has to know whether one is named or the section is misread.
                new Sampler { PoolNamed = world.Config.FoundingTricklePoolCount > 0 }.ReadState(r);

                StateIo.Tag(r, "LOOP");
                MetabolicSteps = r.ReadInt32();
                BestSpeed = r.ReadDouble();
                BestSpeedAtSeconds = r.ReadDouble();
                AssayFired = r.ReadBoolean();

                StateIo.Tag(r, "PEND");
            }

            // After the restore, from the solver itself: Simulation.ReadState has already refused
            // a checkpoint whose body count is not the rebuilt population's, so this is that count
            // and the file's at once.
            Bodies = sim.Dynamics.Creatures.Count;
            Links = sim.Dynamics.TotalLinks();

            Compare(world.Config.Hash());
        }

        /// <summary>
        /// The four digests against this Editor's, as <c>Program.RecordResume</c> asks them — and
        /// then reports rather than refuses.
        /// </summary>
        /// <remarks>
        /// The three source trees are hashed the way <c>TheatreDynamicsReplay</c> hashes them for
        /// a recording, out of the main tree the packages are consumed from; the config is hashed
        /// off the world that was actually built, not off the string in <c>run.json</c>, so what
        /// is compared is the world on screen rather than a word about it.
        /// </remarks>
        private void Compare(string configHash)
        {
            string root = BuildIdentity.RepositoryRoot();

            Differences = Header.Differences(
                configHash,
                BuildIdentity.HashSourceTree(System.IO.Path.Combine(root, "src", "Evosim.Core")),
                BuildIdentity.HashSourceTree(System.IO.Path.Combine(root, "src", "Evosim.Dynamics")),
                BuildIdentity.HashSourceTree(System.IO.Path.Combine(root, "src", "Evosim.Farm")));

            var names = new List<string>(Differences.Count);

            // Every line CheckpointHeader builds starts with the field's own name and a colon, so
            // the short form is the line's first word rather than a second comparison kept beside
            // the first.
            foreach (string line in Differences)
            {
                int colon = line.IndexOf(':');
                names.Add(colon > 0 ? line.Substring(0, colon) : line);
            }

            DifferingFields = names;
        }

        /// <summary>
        /// The one clause a label carries: where this world came from, and that it is a cousin.
        /// </summary>
        public string Label()
        {
            string differing = DifferingFields.Count == 0
                ? ""
                : "; " + string.Join(", ", DifferingFields) + " differ";

            return string.Format(
                CultureInfo.InvariantCulture,
                "continued from checkpoint at {0:0.#} s (cousin{1})", Seconds, differing);
        }

        /// <summary>The same for a console, where there is room for the digests themselves.</summary>
        public string Line()
        {
            string where = System.IO.Path.GetFileName(Path);

            string hashes = Differences.Count == 0
                ? "all four source digests are this build's"
                : "SOURCE DIFFERS: " + string.Join("; ", Differences);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} from {1} — {2} bodies, {3} links, {4} metabolic steps into the run; {5}. " +
                "It is a cousin whatever the digests say: the Editor's Mono and the farm's RyuJIT " +
                "do not agree on a double sum, so the trajectory from here is this Editor's own.",
                Label(), where, Bodies, Links, MetabolicSteps, hashes);
        }
    }
}
