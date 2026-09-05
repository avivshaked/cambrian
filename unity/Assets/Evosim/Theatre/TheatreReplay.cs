using System;
using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// The census the HUD and the identity check both read, taken once per metabolic step.
    /// </summary>
    /// <remarks>
    /// Every field comes from the same accessor <c>EvolutionRun.Row</c> builds its column from,
    /// so the HUD and the run's own table agree by construction rather than by inspection. Taken
    /// once per metabolic step rather than per frame: at 2 Hz of simulated time the walk over the
    /// population is nothing, and per frame it would be the most expensive thing the viewer does.
    /// </remarks>
    public struct WorldCensus
    {
        public double T;
        public int Alive;
        public long Births;
        public long Deaths;
        public int Absorptive;
        public int Photosynthetic;
        public int Jointed;
        public double MeanHeight;
        public double AuditPercent;
        public double AuditResidual;
        public double MatterHere;
        public long Diverged;
    }

    /// <summary>
    /// Mode B: a recorded run, rebuilt from its own directory and stepped again — D075 item 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a re-run, not a playback.</b> Nothing about a creature's position was ever
    /// stored (survey §2: snapshots hold genome graphs, `lineage.jsonl` holds no geometry), so
    /// there is nothing to play back. What there is instead is determinism: same machine, same
    /// build, same <c>(config, seed, step)</c> replays bit for bit (logbook/0052), so running the
    /// world again <i>is</i> watching the run. The identity check below is what turns that from a
    /// claim into a reading.
    /// </para>
    /// <para>
    /// <b>It writes nothing into the run directory.</b> A viewer that could modify the record is
    /// a viewer whose recording cannot be trusted afterwards.
    /// </para>
    /// <para>
    /// <b>The physics scene is configured exactly as <c>EvolutionRun</c> configures it</b> —
    /// script simulation mode, zero gravity, self-collision on, the depenetration cap — and
    /// restored on dispose. Unity's own defaults are not neutral (CLAUDE.md), and a replay that
    /// forgot one of them would differ from the record for a reason no column would name.
    /// </para>
    /// </remarks>
    public sealed class TheatreReplay : IDisposable
    {
        public RunRecord Record { get; private set; }
        public Ecosystem Eco { get; private set; }

        /// <summary>True when this build's source matches the source the run recorded.</summary>
        public bool Faithful { get; private set; }

        /// <summary>How this build differs from the recorded one, or null.</summary>
        public string SourceDifference { get; private set; }

        /// <summary>The last census taken, refreshed every metabolic step.</summary>
        public WorldCensus Census;

        /// <summary>Samples compared against the record so far.</summary>
        public int SamplesMatched { get; private set; }

        /// <summary>Samples in the record that the replay stepped past without landing on.</summary>
        public int SamplesSkipped { get; private set; }

        /// <summary>The first disagreement with the record, or null while there is none.</summary>
        public string FirstMismatch { get; private set; }

        /// <summary>Simulated seconds elapsed in the replay.</summary>
        public double ElapsedSeconds => Eco?.World.ElapsedSeconds ?? 0d;

        /// <summary>The last sample time the record holds — where a full replay ends.</summary>
        public double RecordedThroughSeconds =>
            Record.Samples.Count > 0 ? Record.Samples[Record.Samples.Count - 1].T : 0d;

        private int _nextSample;
        private SimulationMode _previousMode;
        private Vector3 _previousGravity;
        private bool _sceneConfigured;

        // Drained at each sample and discarded, exactly where EvolutionRun drains them. Not
        // instrumentation the theatre wants — the point is that the queues inside World stay
        // bounded, and that the replay calls the same methods at the same moments the recording
        // did. A world that accumulated a hundred megabytes of undrained lineage rows would be
        // this viewer's own doing.
        private readonly List<AbsorptiveSample> _absorptiveScratch = new List<AbsorptiveSample>();

        private TheatreReplay() { }

        /// <summary>
        /// Loads a run and builds its world. Returns null and sets <paramref name="refusal"/>
        /// when the run cannot be replayed faithfully and <paramref name="allowMismatch"/> is
        /// false.
        /// </summary>
        /// <param name="runDirectory">A run directory, or the arm directory above it.</param>
        /// <param name="allowMismatch">
        /// Play a run recorded by different source anyway. What comes out is a plausible world
        /// rather than this run, and the caller must say so on screen.
        /// </param>
        /// <param name="refusal">Why the run was refused, or null.</param>
        public static TheatreReplay Open(string runDirectory, bool allowMismatch, out string refusal)
        {
            refusal = null;

            var replay = new TheatreReplay();

            try
            {
                replay.Record = RunRecord.Load(runDirectory);
            }
            catch (Exception e)
            {
                refusal = e.Message;
                return null;
            }

            replay.SourceDifference =
                BuildIdentity.Difference(replay.Record.CoreHash, replay.Record.SimHash);
            replay.Faithful = replay.SourceDifference == null;

            if (!replay.Faithful && !allowMismatch)
            {
                refusal =
                    "This build is not the build that recorded the run, so what it produced " +
                    "would not be that run: " + replay.SourceDifference +
                    ". Tick 'Allow Source Mismatch' to watch it anyway — it plays with a banner " +
                    "saying it is not a faithful replay.";
                return null;
            }

            // Before the Ecosystem, not after: Ecosystem and EffectorDriver both read the step at
            // construction, and a replay at the wrong step is a different chaotic realisation.
            Ecosystem.ConfigurePhysicsStep(replay.Record.PhysicsDtSeconds);

            replay._previousMode = Physics.simulationMode;
            replay._previousGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);
            replay._sceneConfigured = true;

            // Parented to nothing, exactly as EvolutionRun builds it. A parent transform would be
            // tidier in the hierarchy and would put one more matrix between development and the
            // solver; identity is worth more than tidiness.
            replay.Eco = new Ecosystem(replay.Record.Config, replay.Record.Seed);

            replay.Refresh();
            return replay;
        }

        /// <summary>
        /// One physics step. Returns true on the steps the economy ran, where the census is
        /// refreshed and the record is checked.
        /// </summary>
        /// <param name="map">
        /// Kept in step with the population, if selection is wanted. Bracketed around the step
        /// rather than driven from inside it, because what the map has to observe is the
        /// difference the step makes — see <see cref="CreatureIdMap"/>.
        /// </param>
        public bool Step(CreatureIdMap map = null)
        {
            if (Eco == null) return false;

            map?.BeforeStep(Eco.World);
            bool metabolic = Eco.Step();
            map?.AfterStep();

            if (!metabolic) return false;

            Refresh();
            Compare();
            return true;
        }

        /// <summary>The world as the run's own report would have counted it.</summary>
        private void Refresh()
        {
            World world = Eco.World;
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
            };
        }

        /// <summary>
        /// The honesty check: at every recorded sample time, is this world the recorded world?
        /// </summary>
        /// <remarks>
        /// <para>
        /// Compared exactly, including the two doubles. <c>Json.Writer</c> writes them with "R",
        /// the shortest string that parses back to the same bits, so a stored sample and a live
        /// one are comparable to the last bit — and on a build that replays bit for bit, anything
        /// less than exact equality is a real difference rather than a rounding artefact.
        /// </para>
        /// <para>
        /// The queue drains sit here, at the sample, because that is where <c>EvolutionRun</c>
        /// does them. They are discarded: the theatre writes nothing.
        /// </para>
        /// </remarks>
        private void Compare()
        {
            World world = Eco.World;

            world.DrainLineageEvents();
            _absorptiveScratch.Clear();
            world.CollectAbsorptiveLog(_absorptiveScratch);
            Eco.DrainMotility(out _, out _, out _, out _);

            IReadOnlyList<RunSample> samples = Record.Samples;
            if (_nextSample >= samples.Count) return;

            double t = world.ElapsedSeconds;

            // A sample the replay has stepped past without landing on. Both clocks advance by the
            // metabolic step from zero, so this should not happen; if it does, the run's samples
            // are on a different grid and saying so is more use than silently skipping them.
            while (_nextSample < samples.Count && samples[_nextSample].T < t - 1e-6)
            {
                SamplesSkipped++;
                _nextSample++;
            }

            if (_nextSample >= samples.Count) return;

            RunSample recorded = samples[_nextSample];
            if (Math.Abs(recorded.T - t) > 1e-6) return;

            _nextSample++;

            string column = FirstDifference(recorded);
            if (column == null)
            {
                SamplesMatched++;
                return;
            }

            if (FirstMismatch == null)
            {
                FirstMismatch = $"t={recorded.T:0.#}: {column}";
            }
        }

        private string FirstDifference(RunSample recorded)
        {
            if (recorded.Alive != Census.Alive)
            {
                return $"alive {recorded.Alive} recorded, {Census.Alive} here";
            }

            if (recorded.Births != Census.Births)
            {
                return $"births {recorded.Births} recorded, {Census.Births} here";
            }

            if (recorded.Deaths != Census.Deaths)
            {
                return $"deaths {recorded.Deaths} recorded, {Census.Deaths} here";
            }

            if (recorded.AuditResidual != Census.AuditResidual)
            {
                return $"audit {recorded.AuditResidual:R} recorded, {Census.AuditResidual:R} here";
            }

            if (recorded.MeanHeight != Census.MeanHeight)
            {
                return $"mean depth {recorded.MeanHeight:0.####} recorded, " +
                       $"{Census.MeanHeight:0.####} here";
            }

            return null;
        }

        /// <summary>One line for a HUD or a log: what the identity check currently says.</summary>
        public string IdentityLine()
        {
            if (Record.Samples.Count == 0)
            {
                return "identity: no recorded samples to check against";
            }

            if (FirstMismatch != null)
            {
                return $"identity: MISMATCH — {FirstMismatch} ({SamplesMatched} matched before it)";
            }

            if (SamplesMatched == 0)
            {
                return $"identity: no sample reached yet (first at t={Record.Samples[0].T:0.#})";
            }

            string skipped = SamplesSkipped > 0 ? $", {SamplesSkipped} skipped" : "";
            return $"identity: {SamplesMatched} of {Record.Samples.Count} samples match{skipped}";
        }

        public void Dispose()
        {
            Eco?.DestroyAll();
            Eco = null;

            if (!_sceneConfigured) return;

            Physics.simulationMode = _previousMode;
            Physics.gravity = _previousGravity;
            _sceneConfigured = false;
        }
    }
}
