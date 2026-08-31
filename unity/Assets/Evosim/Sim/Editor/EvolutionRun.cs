using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// A long embodied run: does selection find a swimmer and keep one? — DESIGN.md §10 M4, §5A.6b.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question left open by logbook/0016.</b> A random genome swims at 0.485 m/s about one
    /// time in two hundred, so the mechanism works; what is unknown is whether two hundred
    /// simulated seconds was simply too little search, or whether the jointed share falls to zero
    /// however long the run goes. That is a measurement, not a design question, and the only way to
    /// take it is to run until thousands of creatures have been born.
    /// </para>
    /// <para>
    /// <b>Parameterised by environment rather than by code</b>, because Unity's
    /// <c>-executeMethod</c> takes no arguments and a sweep means running one build a dozen times
    /// with a single number changed. Everything that varies is read from the environment and
    /// written into the header of the output, so a result is never separated from the settings that
    /// produced it — the same reason §7 keeps a config hash.
    /// </para>
    /// <para>
    /// <b>Written incrementally, and a runaway is a result.</b> Rows are flushed as they are
    /// produced, so a killed run still leaves everything it measured — what §9 requires of
    /// <c>stats.jsonl</c>, for the same reason. <see cref="PopulationRunawayException"/> is caught
    /// and recorded rather than propagated: D021 makes it a measurement, locating the generous end
    /// of the calibration as precisely as extinction locates the lean end.
    /// </para>
    /// </remarks>
    public static class EvolutionRun
    {
        [MenuItem("Evosim/Run — long evolution run")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            float irradiance = Env("EVOSIM_IRRADIANCE", 48f);
            float budgetSeconds = Env("EVOSIM_SECONDS", 4000f);
            float wallMinutes = Env("EVOSIM_WALL_MINUTES", 30f);
            int reportEvery = (int)Env("EVOSIM_REPORT_EVERY", 200f);
            ulong seed = EnvULong("EVOSIM_SEED", 1UL);

            // The two halves of what a joint costs to own before it does anything. §5A.10 marks
            // both unmeasured, and LinkCell's own documentation names the failure at each end:
            // "too low and capacity is effectively free again, too high and nothing can afford to
            // move". A calibration sweep found nothing with a joint alive at any irradiance from
            // 64 to 400 W/m2, so which end we are on is the question these make askable.
            float idle = Env("EVOSIM_IDLE", 0.02f);
            // Defaulted from RandomGenomeOptions rather than to a literal. It was 120f — the
            // ceiling D032 retired — so every arm that did not set EVOSIM_MAXPOWER silently
            // overrode the design default of 20 with the old one, and the two disagreed in
            // opposite directions depending on whether a run happened to name the knob.
            float maxPower = Env("EVOSIM_MAXPOWER", RandomGenomeOptions.Default.MaxLinkPower);

            // The FLOOR of the capacity draw, and the knob D031 and D032 both left alone while
            // sweeping the ceiling. It is the one that mattered: a survivor-sized creature is
            // insolvent carrying any hinge above about 5 N·m, and MinLinkPower is 5 — so every
            // jointed creature ever born started at or past break-even and died of arithmetic
            // before its swimming was ever tested (D042).
            float minPower = Env("EVOSIM_MINPOWER", RandomGenomeOptions.Default.MinLinkPower);

            // Muscle that also earns. The 1.30 W a two-part flagellate forfeits by making one of
            // its parts a link dominates the 0.51 W upkeep and 0.40 W idle charge together
            // (logbook/0026), and no setting of those two can reach it. 0 is §5A.1 unchanged.
            // Expressed as a fraction of green tissue's capture rate, not as an absolute
            // efficiency: 0.5 means "half as good at light as a photosynthetic cell".
            float linkPhoto = Env("EVOSIM_LINK_PHOTO", 0f);

            // The day/night cycle (D035). Mean-preserving, so amplitude 0 is exactly the acyclic
            // world every earlier number was measured in and the arms of a sweep stay comparable.
            float dayAmplitude = Env("EVOSIM_DAY_AMPLITUDE", 0f);
            float dayLength = Env("EVOSIM_DAY_LENGTH", 200f);

            // Moving water, and the stirring that gives the world a return path for its own
            // energy (D036). Both default to off, so a run with neither set is the still world
            // every earlier number here was measured in.
            float currentSpeed = Env("EVOSIM_CURRENT", 0f);
            float mixing = Env("EVOSIM_MIXING", 0f);

            // D051. The floor's return leg: a first-order rate constant, s⁻¹, decaying the floor
            // layer's stock back into the layer above it. One knob for both currencies — the
            // cycle needs energy and matter both to return, and a run comparing them separately
            // is not this decision's question. 0 is the world every earlier number was measured
            // in, where the floor only ever accumulates.
            float remin = Env("EVOSIM_REMIN", 0f);

            // D052. Matter a living body returns per joule of upkeep it pays, at its own depth —
            // turnover, rather than the only-at-death return remineralisation gives the floor.
            // The default is RunConfig's own (0), so a run that does not name this is the world
            // every earlier number here was measured in, where nothing a living creature took
            // ever came back until it died.
            float excretion = Env("EVOSIM_EXCRETION", new RunConfig().ExcretionPerJoule);

            // D055. Metres of seabed no mouth can reach — the consumer-resource damping fix: the
            // floor's detritus still arrives, piles and leaks back exactly as before, but feeding
            // cannot price it. 0 is the world every earlier run measured, where the whole field was
            // grazeable including the floor layer.
            float floorRefuge = Env("EVOSIM_FLOOR_REFUGE", new RunConfig().FloorRefugeMetres);

            // D057. Genome-distance drift threshold for species accounting — pure instrumentation,
            // read by nothing but this report. 0 is the world every earlier run measured, where
            // species machinery never runs at all and every creature reads species 0.
            float speciesTheta = Env("EVOSIM_SPECIES_THETA", new RunConfig().SpeciesDriftThreshold);

            // D053. The world's footprint — the aperture the sun shines through, the volume of
            // every layer, and the denominator of shading all at once, so halving it halves the
            // world's total income and stock at identical per-creature margins. The rescale knob:
            // logbook/0040 found that irradiance cannot do this job, because it scales what one
            // creature earns, not how many the world holds.
            float area = Env("EVOSIM_AREA", new RunConfig().WorldAreaSquareMetres);

            // D021's "never again", enforced directly rather than only measured. 0 keeps the
            // floor open forever — today's behaviour, and every earlier run's. A positive value
            // closes it after that many simulated seconds (RunConfig.FloorClosesAfterSeconds), so
            // anything alive past that point got there on its own; a world that crashes to zero
            // after closing is left at zero rather than rescued.
            float floorCloses = Env("EVOSIM_FLOOR_CLOSES", 0f);

            // The population ceiling (D021) is an instrument limit, not a world mechanism: a run
            // that reaches it ends as a runaway and is censored. Exposed so a world that is
            // merely generous can be run past 5,000 without pretending that number is biology.
            // The default is RunConfig's own, so a run that does not name it is unchanged.
            int maxPopulation = (int)Env("EVOSIM_MAX_POP", new RunConfig().MaximumPopulation);

            // Ageing (D038). Seconds of life after which a body costs twice as much to keep and
            // converts half as much of what it takes. 0 is the immortal world every earlier run
            // measured, in which 92% of everything ever born was still alive.
            float senescence = Env("EVOSIM_SENESCENCE", 0f);

            // How often a birth changes a part's trade (§5A.3). Exposed to make one specific
            // question askable: the trophic niche opens at t≈9,500 s and absorptive arrivals run
            // at one per 5,128 births, so a run that ends shortly after cannot tell an
            // arrival-limited world from an establishment-limited one. Raising this delivers
            // arrivals on demand and separates the two (logbook/0024).
            float cellTypeMutation = Env("EVOSIM_CELLTYPE_MUTATION", MutationRates.Default.CellTypeChance);

            // What a cubic metre of filter can strain per second (D041). Raised 0.5 → 1.0 because
            // converting a spread-out photosynthesiser costs it 9.2× its income, and swept from
            // here because §5A.10 says an unmeasured claim must be one a run can vary.
            float clearance = Env("EVOSIM_CLEARANCE", 1.0f);

            // How much denser than water tissue is, kg/m3. 0 is §5.2's neutral buoyancy, in which
            // a creature stays exactly where it was born and doing nothing is optimal. The
            // ceiling is what a joint can push against — 0.017 m/s for a founder body at 20 N.m
            // (logbook/0027) — and above it nothing holds station.
            float excessDensity = Env("EVOSIM_EXCESS_DENSITY", 0f);

            // D048. Matter a child's tissue costs, per joule of it, and what the column starts
            // with per cubic metre. 0 is the world as it was before D048 — producers consuming
            // nothing, no negative feedback on occupying the best depth, every run sorting to the
            // surface. Read the blocked-conception count: matter that never binds changes nothing
            // and reads identically to matter that is switched off.
            float matterPerTissue = Env("EVOSIM_MATTER_PER_TISSUE", 0f);
            float initialMatter = Env("EVOSIM_MATTER_INITIAL", 1f);

            // D049. Chance a tail-less founder is born with a gas bladder, and what holding lift
            // costs. 0 is a world where buoyancy has to be *found* by mutation rather than given
            // — which of those happened is most of what D049 is trying to measure, so it shows in
            // the header and the hash.
            float floatChance = Env("EVOSIM_FOUNDER_FLOAT", 0f);
            float liftCost = Env("EVOSIM_LIFT_COST", 0.05f);

            string outPath = Environment.GetEnvironmentVariable("EVOSIM_OUT");
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(), "..", "runs", "evolution.md"));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var config = new RunConfig
            {
                Fluid = new FluidConfig { TissueExcessDensity = excessDensity },
                Light = new LightModel(irradiance, 12f)
                {
                    DayNightAmplitude = dayAmplitude,
                    DayLengthSeconds = dayLength,
                },
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(),
                    new LinkCell(
                        idle,
                        photosyntheticEfficiency:
                            linkPhoto * PhotosyntheticCell.DefaultEfficiency),
                    new NeuralCell(),
                    new PhotosyntheticCell(),
                    new AbsorptiveCell(clearance),
                    new ConsumerCell(),
                    new BuoyancyCell(liftCost)),
            };

            config.Genome.MaxLinkPower = maxPower;
            config.Genome.MinLinkPower = Math.Min(minPower, maxPower);
            config.Current.Speed = currentSpeed;
            config.MatterPerTissueJoule = matterPerTissue;
            config.InitialMatterPerCubicMetre = initialMatter;
            config.Genome.FounderFloatChance = floatChance;
            config.NutrientMixingDiffusivity = mixing;
            config.NutrientRemineralisationPerSecond = remin;
            config.MatterRemineralisationPerSecond = remin;
            config.ExcretionPerJoule = excretion;
            config.FloorRefugeMetres = floorRefuge;
            config.SpeciesDriftThreshold = speciesTheta;
            config.WorldAreaSquareMetres = area;
            config.FloorClosesAfterSeconds = floorCloses;
            config.MaximumPopulation = maxPopulation;
            config.SenescenceDoublingSeconds = senescence;
            config.Mutation.CellTypeChance = cellTypeMutation;
            var eco = new Ecosystem(config, seed);

            // Named after the report rather than timestamped, so a run's table and its creatures
            // sit side by side and an A/B arm's genomes are findable by the arm's own name.
            RunDirectory dir = null;
            try
            {
                dir = RunDirectory.Create(
                    Path.Combine(
                        Path.GetDirectoryName(outPath),
                        Path.GetFileNameWithoutExtension(outPath)),
                    config, DateTime.UtcNow);
            }
            catch (Exception e)
            {
                // A run that cannot save its creatures is still a run worth having, and losing
                // the report as well would be the worse outcome.
                Debug.LogWarning("no genome directory: " + e.Message);
            }

            var report = new StringBuilder();
            report.AppendLine("# Evolution run — " + irradiance.ToString("0") + " W/m2");
            report.AppendLine();
            report.AppendLine(
                "Unity " + Application.unityVersion + " · dt=" + Ecosystem.FixedDt +
                " · metabolic step " + (Ecosystem.StepsPerMetabolicStep * Ecosystem.FixedDt) +
                " s · seed " + seed + " · idle " + idle + " W/N·m · power " + minPower + "-" + maxPower +
                " · day ±" + dayAmplitude + " over " + dayLength + " s" +
                " · current " + currentSpeed + " m/s · mixing " + mixing + " m2/s" +
                " · remin " + remin + " /s" +
                " · excretion " + excretion + " /J" +
                " · refuge " + floorRefuge + " m" +
                " · speciesTheta " + speciesTheta +
                " · area " + area + " m2" +
                (floorCloses > 0f ? " · floor closes " + floorCloses + " s" : " · floor open") +
                " · ceiling " + maxPopulation +
                " · senescence " + (senescence > 0f ? senescence + " s" : "off") +
                " · cellType mut " + cellTypeMutation +
                " · clearance " + clearance +
                " · linkPhoto " + linkPhoto +
                " · excessDensity " + excessDensity + " kg/m3" +
                " · matter " + matterPerTissue + "/J from " + initialMatter + "/m3" +
                " · float " + floatChance + " at " + liftCost + " W/lift" +
                " · configHash `" + config.Hash() + "`");
            report.AppendLine();
            report.AppendLine(Header());

            Flush(outPath, report);

            var clock = Stopwatch.StartNew();
            string ending = "budget reached";
            int metabolicSteps = 0;
            double bestSpeedEver = 0d;
            double bestSpeedAt = 0d;

            try
            {
                while (eco.World.ElapsedSeconds < budgetSeconds &&
                       clock.Elapsed.TotalMinutes < wallMinutes)
                {
                    if (!eco.Step()) continue;

                    metabolicSteps++;

                    // Checked every step, not only at a report row: with FloorClosesAfterSeconds
                    // set (or any other way the floor can fail to refill), an empty world would
                    // otherwise sit doing nothing for up to reportEvery more steps before anyone
                    // noticed. A crash to zero is a real outcome (RunConfig.FloorClosesAfterSeconds),
                    // so it ends the run through the same finishing path as a normal one — one last
                    // row is written first so the final state is not lost.
                    if (eco.World.Living.Count == 0)
                    {
                        ending =
                            "extinct at t=" + eco.World.ElapsedSeconds.ToString("0.#") +
                            " s, and the floor could not refill it";
                        report.AppendLine(Row(eco, dir));
                        Flush(outPath, report);
                        break;
                    }

                    // When, not only how much. A best that only ever occurs in the opening
                    // seconds is a transient; one that recurs late is a creature.
                    if (eco.MaxSpeed > bestSpeedEver)
                    {
                        bestSpeedEver = eco.MaxSpeed;
                        bestSpeedAt = eco.World.ElapsedSeconds;
                    }

                    if (metabolicSteps % reportEvery != 0) continue;

                    report.AppendLine(Row(eco, dir));
                    Flush(outPath, report);

                    // Every tenth report: often enough that a killed run keeps something recent,
                    // rare enough that a population of thousands is not serialised every sample.
                    if (metabolicSteps % (reportEvery * 10) == 0) Snapshot(dir, eco);
                }
            }
            catch (PopulationRunawayException runaway)
            {
                // D021: not a crash. It locates the generous end of the calibration exactly as
                // extinction locates the lean end, and culling to fit a compute budget would be
                // selection performed by us.
                ending =
                    "RUNAWAY at t=" + runaway.ElapsedSeconds.ToString("0.#") + " s with " +
                    runaway.Population + " alive — light is covering upkeep so completely that " +
                    "nothing has to do anything";
            }

            clock.Stop();

            report.AppendLine();
            report.AppendLine("**Ended:** " + ending + ".");
            report.AppendLine();
            report.AppendLine(
                eco.Steps + " physics steps · " +
                eco.World.ElapsedSeconds.ToString("0.#") + " simulated seconds · " +
                eco.World.Births + " births · " +
                clock.Elapsed.TotalMinutes.ToString("0.#") + " min wall clock (" +
                (eco.World.ElapsedSeconds / Math.Max(1e-9, clock.Elapsed.TotalSeconds)).ToString("0.#") +
                "x real time).");
            report.AppendLine();
            report.AppendLine(
                "**Fastest creature seen at any point: " + bestSpeedEver.ToString("0.####") +
                " m/s, at t=" + bestSpeedAt.ToString("0.#") + " s.**");

            Flush(outPath, report);
            Debug.Log(report.ToString());

            Snapshot(dir, eco);
            if (dir != null)
            {
                report.AppendLine();
                report.AppendLine("Genomes: `" + dir.Path + "`");
                Flush(outPath, report);
                dir.Dispose();
            }

            eco.DestroyAll();
            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }

        /// <summary>
        /// Every creature ever seen carrying an absorptive part. Ids only, never released.
        /// </summary>
        /// <remarks>
        /// <b>The instrument that separates a lineage from a standing crop</b>, which nothing else
        /// here can do. Raising the cell-type mutation rate twentyfold produced thirteen absorptive
        /// creatures where there had been one, while the arrival rate stayed flat — strong evidence
        /// of reproduction, and not proof, because nutrient density was climbing over the same
        /// window and a longer-lived standing crop draws the same curve (logbook/0024). A creature
        /// whose <i>parent</i> was absorptive settles it: that one was born into the trade rather
        /// than mutating into it.
        /// </remarks>
        private static readonly HashSet<long> EverAbsorptive = new HashSet<long>();

        /// <summary>Ids of every creature ever seen carrying a joint.</summary>
        /// <remarks>
        /// The same trick as <see cref="EverAbsorptive"/>, for the same reason and a worse
        /// problem. The population floor (<see cref="RunConfig.MinimumPopulation"/>) trickles
        /// fresh generation-zero founders in whenever the world falls below it, and founders
        /// are jointed about two times in five — so in a world that spends its life at the
        /// floor, the jointed *share* is largely a readout of the founder draw rather than of
        /// anything selection did. Counting the ones whose parent was also jointed separates
        /// "joints keep arriving" from "joints are being kept".
        /// </remarks>
        private static readonly HashSet<long> EverJointed = new HashSet<long>();

        /// <summary>Floor spawns as of the previous report row, so a row can show a rate.</summary>
        /// <remarks>
        /// D021 built the population floor as an instrument and stated its success condition
        /// exactly: it "fires at t=0 and never again". A floor that keeps firing means the world
        /// is not sustaining life, we are — and the run still shows a stable population, births,
        /// deaths and accumulating lineages, every figure consistent with a working ecosystem and
        /// every one of them propped up.
        ///
        /// The reporting D021 specified lives in <c>lineage.jsonl</c>, which is not written. So
        /// the instrument was designed and never built, and a whole day of arms was read as data
        /// without anyone able to tell a living world from a life-supported one — <c>gen min = 0</c>
        /// cannot separate "founders from t=0 are still alive" from "the floor fires every step".
        /// The cumulative count answers that and the window count dates it.
        /// </remarks>
        private static long LastFloorSpawns;

        /// <summary>Matter-blocked conceptions as of the previous row, so a row shows a rate.</summary>
        private static long LastMatterBlocks;

        /// <summary>Ids of every creature ever seen holding lift — D049, same trick as EverJointed.</summary>
        private static readonly HashSet<long> EverBuoyant = new HashSet<long>();


        /// <summary>
        /// Write every living creature's genome to <c>snapshots/&lt;t&gt;.jsonl</c>, one per line.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Until this existed, every creature this project ever evolved was discarded at
        /// process exit.</b> Runs left a markdown table and nothing else — so the first
        /// detritivore lineage to inherit its trade, sixty-two generations deep, survives only as
        /// the row saying it happened (logbook/0025). <c>RunDirectory</c> and <c>GenomeJson</c>
        /// had been built and tested for this and were wired to nothing.
        /// </para>
        /// <para>
        /// <b>Survivors, not lineage.</b> §9's <c>lineage.jsonl</c> is one row per creature ever
        /// born — at the observed birth rate that is tens of thousands of rows and hundreds of
        /// megabytes an hour. What a future run actually needs is a founder pool, and for that the
        /// living population at a moment is the whole answer: they are precisely the genomes that
        /// were solvent in this world. So this writes survivors periodically and at the end, and
        /// leaves the full lineage to whenever something needs ancestry.
        /// </para>
        /// <para>
        /// Written at the end <i>and</i> periodically, because a run that hits its wall-clock
        /// budget or explodes is exactly the run whose creatures are most worth having, and it is
        /// the one that never reaches an orderly shutdown.
        /// </para>
        /// </remarks>
        private static void Snapshot(RunDirectory dir, Ecosystem eco)
        {
            if (dir == null) return;

            // .jsonl, not RunDirectory's .json: the file holds one genome per line, and a
            // reader that trusts the extension would fail on the second line rather than the
            // first — which is the shape of bug that gets blamed on the data.
            string path = Path.ChangeExtension(
                dir.SnapshotPath(eco.World.ElapsedSeconds), ".jsonl");

            using (var writer = new JsonlWriter(path, flushEachRow: false))
            {
                foreach (Organism creature in eco.World.Living) writer.WriteGenome(creature.Genome);
            }
        }

        /// <summary>
        /// One sample: the markdown table row, and — when a run directory exists — the matching
        /// <c>stats.jsonl</c> row.
        /// </summary>
        /// <remarks>
        /// <b>Both are built here, from the same locals, deliberately.</b> §9 specifies
        /// <c>stats.jsonl</c> and <see cref="RunDirectory"/> has opened the writer since it was
        /// built, but nothing ever called it: every run this project has produced has an empty
        /// <c>stats.jsonl</c> and exists only as a markdown table a human can read and nothing can
        /// plot. Writing it from a second pass over the population would let the two drift, and a
        /// stats file that disagrees with the report is worse than no stats file.
        /// </remarks>
        private static string Row(Ecosystem eco, RunDirectory dir)
        {
            World world = eco.World;

            double spend = 0d, workSpend = 0d, depth = 0d, light = 0d, food = 0d;
            double travelled = 0d, age = 0d;
            int jointed = 0, jointedInherited = 0, dof = 0, absorptive = 0, inherited = 0;
            int buoyant = 0, buoyantInherited = 0;
            double liftHeld = 0d, buoyantDepth = 0d;
            int genMin = int.MaxValue, genMax = 0;

            // D057. Distinct species IDs among the living — pure instrumentation, read nowhere
            // but here. Among the living rather than World.Species.Count, which also counts
            // species nobody alive still belongs to.
            var speciesSeen = new HashSet<uint>();

            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];
                speciesSeen.Add(creature.SpeciesId);

                spend += creature.Lifetime.Expenditure;
                workSpend += creature.Lifetime.Work;
                depth += creature.HeightY;

                // The two incomes, separately. Light is shallow and detritus sinks, so a moving
                // optimum — and therefore any reason to migrate — exists only to the extent that
                // both are worth having. If food income is a rounding error then the best depth is
                // the surface at every hour and a day/night cycle changes when creatures earn, not
                // where they should be (D035).
                light += creature.Lifetime.LightIncome;
                food += creature.Lifetime.FoodIncome;

                // How far a creature has actually moved from where it was born, against the
                // spread it was born into. Selection can only see swimming through this ratio: a
                // trait worth a tenth of a metre in a population scattered over twenty is a trait
                // whose signal is two orders of magnitude under the noise, and no number of
                // generations recovers it.
                travelled += Math.Abs(creature.HeightY - creature.BirthHeightY);
                age += creature.Age;

                // Counted, because "food income is 0%" has two completely different causes and
                // the share cannot tell them apart: nothing is trying to eat detritus, or plenty
                // is trying and there is nothing to eat. Founders draw absorptive one time in
                // four (RandomGenomeOptions.FounderCellTypes), so the first should be false — and
                // an assumption is exactly what wants checking here.
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    if (part.CellTypeId != CellTypeIds.Absorptive) continue;

                    absorptive++;
                    EverAbsorptive.Add(creature.Id);

                    // Born into the trade rather than mutated into it. Counted against the ids
                    // ever seen rather than against the living, because a parent that has already
                    // died is exactly the case that matters — it means the lineage outlived its
                    // founder.
                    if (EverAbsorptive.Contains(creature.ParentId)) inherited++;
                    break;
                }

                int creatureDof = 0;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    creatureDof += part.JointType.DofCount();
                }

                if (creatureDof > 0)
                {
                    jointed++;
                    EverJointed.Add(creature.Id);
                    if (EverJointed.Contains(creature.ParentId)) jointedInherited++;
                }
                dof += creatureDof;

                // Counted the same way and for the same reason as joints and feeding: a share is
                // contaminated by whatever the founder draw happens to be, and only the inherited
                // count separates "buoyancy keeps arriving" from "buoyancy is being kept"
                // (logbook/0029). Total lift as well, because a lineage that holds a bladder and
                // lets its lift decay to nothing is a lineage abandoning the organ while still
                // being counted as having it.
                float creatureLift = 0f;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    if (part.CellTypeId == CellTypeIds.Buoyancy) creatureLift += part.Lift;
                }

                if (creatureLift > 0f)
                {
                    buoyant++;
                    liftHeld += creatureLift;

                    // Where they are, separately from where everyone is. A mean over the whole
                    // population cannot answer the only question D049 asks — does the organ buy
                    // a position? — because the organ is held by a minority and the majority
                    // sets the mean. The first probe reported a population rising to +12.8 m
                    // with 4% of it buoyant, which says nothing about the 4%.
                    buoyantDepth += creature.HeightY;

                    EverBuoyant.Add(creature.Id);
                    if (EverBuoyant.Contains(creature.ParentId)) buoyantInherited++;
                }

                // §5A.6b's instrument: a minimum generation depth above zero means no living
                // creature is a floor spawn, which is the definition of a world running itself.
                if (creature.GenerationDepth < genMin) genMin = creature.GenerationDepth;
                if (creature.GenerationDepth > genMax) genMax = creature.GenerationDepth;
            }

            int alive = world.Living.Count;
            if (alive == 0) genMin = 0;

            // Spread, not only the mean, and it is the statistic a migration would show up in.
            // A population that has settled at one good depth and a population sloshing up and
            // down with the sun have the same mean at the moment you sample them and completely
            // different spreads — which is the same lesson as the mean speed that hid a 78x tail
            // (logbook/0016). Sampled across the population rather than over time, so one row is
            // one snapshot of how vertically spread the world is.
            double meanDepth = alive > 0 ? depth / alive : 0d;
            double variance = 0d;

            for (int i = 0; i < world.Living.Count; i++)
            {
                double d = world.Living[i].HeightY - meanDepth;
                variance += d * d;
            }

            double depthSd = alive > 1 ? Math.Sqrt(variance / (alive - 1)) : 0d;

            double workShare = spend > 0d ? workSpend / spend : 0d;
            double residual = world.EnergyIn > 0d ? 100d * world.AuditResidual / world.EnergyIn : 0d;
            double seconds = Ecosystem.StepsPerMetabolicStep * Ecosystem.FixedDt;

            // The same sample, as data. Raw numbers and no percentages: a reader can divide, and
            // a stored percentage loses the denominator that says whether it means anything —
            // "food 100%" over two joules and over two hundred thousand are the same column.
            dir?.Stats.WriteRow(w => w
                .Field("t", world.ElapsedSeconds)
                .Field("alive", alive)
                .Field("births", world.Births)
                .Field("deaths", world.Deaths)
                .Field("jointed", jointed)
                .Field("jointedInherited", jointedInherited)
                .Field("dof", dof)
                .Field("meanSpeed", eco.MeanSpeed)
                .Field("maxSpeed", eco.MaxSpeed)
                .Field("workJoulesPerSecond", eco.WorkThisStep / seconds)
                .Field("spendJoules", spend)
                .Field("workJoules", workSpend)
                .Field("lightJoules", light)
                .Field("foodJoules", food)
                .Field("absorptive", absorptive)
                .Field("absorptiveInherited", inherited)
                .Field("detritusJoules", world.Nutrients.TotalJoules)
                .Field("detritusHere", world.Nutrients.DensityAt((float)meanDepth))
                .Field("detritusOnFloor",
                    world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1))
                .Field("detritusDeep",
                    world.Nutrients.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f))
                .Field("meanHeight", meanDepth)
                .Field("heightSd", depthSd)
                .Field("meanRise", alive > 0 ? travelled / alive : 0d)
                .Field("meanAge", alive > 0 ? age / alive : 0d)
                .Field("dayFactor", world.Field.DayFactor)
                .Field("shading", 1d - world.Field.ShadingAt((float)meanDepth))
                .Field("buoyant", buoyant)
                .Field("buoyantInherited", buoyantInherited)
                .Field("liftHeld", liftHeld)
                .Field("buoyantDepth", buoyant > 0 ? buoyantDepth / buoyant : 0d)
                .Field("matterHere", world.Matter.DensityAt((float)meanDepth))
                .Field("matterSurface", world.Matter.DensityAt(0f))
                .Field("matterDeep", world.Matter.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f))
                .Field("matterStanding", world.StandingMatter)
                .Field("conceptionsBlockedByMatter", world.ConceptionsBlockedByMatter)
                .Field("floorSpawns", world.FloorSpawns)
                .Field("floorSpawnsWindow", world.FloorSpawns - LastFloorSpawns)
                .Field("secondsSinceFloorFired", world.SecondsSinceFloorFired)
                .Field("generationMin", genMin)
                .Field("generationMax", genMax)
                .Field("auditResidual", world.AuditResidual)
                .Field("species", speciesSeen.Count));

            var c = CultureInfo.InvariantCulture;

            // Built column by column rather than through a positional format string. That string
            // had reached twenty-five indices and desynchronised from its argument list the moment
            // two more measurements were added — a FormatException at the first row, which is the
            // benign version; the malign one is two columns swapping and every number staying
            // plausible. Pairing each header with its own value makes that impossible to express.
            var row = new List<string>
            {
                world.ElapsedSeconds.ToString("0", c),
                alive.ToString(c),
                world.Births.ToString(c),
                world.Deaths.ToString(c),
                "**" + jointed.ToString(c) + "**",
                (alive > 0 ? 100d * jointed / alive : 0d).ToString("0.#", c) + "%",
                "**" + jointedInherited.ToString(c) + "**",
                (alive > 0 ? (double)dof / alive : 0d).ToString("0.##", c),
                eco.MeanSpeed.ToString("0.####", c),
                eco.MaxSpeed.ToString("0.####", c),
                (eco.WorkThisStep / seconds).ToString("0.##", c),
                (100d * workShare).ToString("0.#", c) + "%",
                "**" + (light + food > 0d ? 100d * food / (light + food) : 0d).ToString("0.##", c) + "%**",
                "**" + absorptive.ToString(c) + "**",
                "**" + inherited.ToString(c) + "**",
                "**" + world.Nutrients.TotalJoules.ToString("0.#", c) + "**",

                // Density where the creatures actually are, and how much of the world's detritus
                // has already fallen past them. Total joules cannot tell "there is no food" from
                // "the food is forty metres below everything that could eat it", and those two
                // want opposite responses.
                "**" + world.Nutrients.DensityAt((float)meanDepth).ToString("0.####", c) + "**",
                "**" + (world.Nutrients.TotalJoules > 0d
                    ? 100d * world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1) /
                      world.Nutrients.TotalJoules
                    : 0d).ToString("0.#", c) + "%**",

                // D051's return leg: detritus density in the deep water, 90% of depth — the same
                // reading the matter field takes below. The floor layer is the last 1 m; this is
                // the water above it, and the prediction under test is that this number
                // rises once remineralisation leaks matter back out of the floor.
                "**" + world.Nutrients.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f)
                    .ToString("0.####", c) + "**",

                meanDepth.ToString("0.#", c),
                "**" + depthSd.ToString("0.##", c) + "**",
                "**" + (alive > 0 ? travelled / alive : 0d).ToString("0.####", c) + "**",
                (alive > 0 ? age / alive : 0d).ToString("0.#", c),
                world.Field.DayFactor.ToString("0.##", c),

                // How much of the sun the population is actually intercepting where it lives.
                // D023 made light finite and competed for, and that is the world's carrying
                // capacity — but a population too sparse to shade itself is not yet feeling it,
                // and a runaway that looks like a calibration failure may simply be a world with
                // room left. 0% is an empty world; 100% is a closed canopy.
                (100d * (1d - world.Field.ShadingAt((float)meanDepth))).ToString("0.#", c) + "%",
                // D021: "fires at t=0 and never again". Anything but 0 after the first row says
                // the world is being kept alive rather than staying alive, and every other
                // number in the row is propped up by it.
                // Surface against deep is the gradient D048 exists to create, and the pair says
                // more than either alone: equal numbers mean matter is not binding anywhere.
                // Count, inherited count, and mean lift among those that hold any. The share alone
                // repeats logbook/0029's mistake; the mean separates a lineage that keeps the
                // organ from one that keeps the label.
                "**" + buoyant.ToString(c) + "**",
                "**" + buoyantInherited.ToString(c) + "**",
                // Em-dash and not 0 when nobody is buoyant: 0.0 is a real depth and a real lift,
                // and a column that prints one for "no such creature" is the shape of trap this
                // whole entry was written after. A reader scanning `flt m` alone must not be able
                // to mistake an empty set for a population at the waterline.
                buoyant > 0 ? (liftHeld / buoyant).ToString("0.##", c) : "—",
                buoyant > 0 ? (buoyantDepth / buoyant).ToString("0.#", c) : "—",
                world.Matter.DensityAt(0f).ToString("0.###", c),
                world.Matter.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f)
                    .ToString("0.###", c),
                // Conceptions refused for want of matter rather than energy. Zero means the
                // mechanism is on and doing nothing, which looks like off in every other column.
                "**" + (world.ConceptionsBlockedByMatter - LastMatterBlocks).ToString(c) + "**",
                "**" + (world.FloorSpawns - LastFloorSpawns).ToString(c) + "**",
                genMin.ToString(c),
                genMax.ToString(c),
                residual.ToString("0.0000", c) + "%",

                // D057. Appended at the end, per every other column here: existing awk scripts
                // index columns by position, so nothing already written may move.
                speciesSeen.Count.ToString(c),
            };

            LastFloorSpawns = world.FloorSpawns;
            LastMatterBlocks = world.ConceptionsBlockedByMatter;

            if (row.Count != Columns.Length)
            {
                throw new InvalidOperationException(
                    $"{row.Count} values against {Columns.Length} headers. A column was added at " +
                    "one end and not the other, and every row after it would be mislabelled.");
            }

            return "| " + string.Join(" | ", row) + " |";
        }

        /// <summary>Column headers. The single source of the table's shape — see <c>Row</c>.</summary>
        private static readonly string[] Columns =
        {
            "t (s)", "alive", "births", "deaths", "**jointed**", "jointed %", "**jnt inh**", "mean dof",
            "mean m/s", "max m/s", "work J/s", "work share", "**food %**", "**absorpt**", "**inherit**",
            "**detritus J**", "**J/m3 here**", "**% on floor**", "**det deep**", "depth m", "**depth sd**",
            "**rise m**", "age s", "sun", "**shade %**",
            "**float**", "**flt inh**", "lift", "**flt m**",
            "mat top", "mat deep", "**mat blk**", "**floor**", "gen min", "gen max", "audit",
            "species",
        };

        private static string Header() =>
            "| " + string.Join(" | ", Columns) + " |" + Environment.NewLine +
            "|" + string.Concat(System.Linq.Enumerable.Repeat("---|", Columns.Length)) + "|";

        private static void Flush(string path, StringBuilder report)
        {
            try
            {
                File.WriteAllText(path, report.ToString());
            }
            catch (IOException)
            {
                // A locked output file must not take the run down with it — the run is the
                // expensive part and the numbers are still in the log.
            }
        }

        private static float Env(string name, float fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrEmpty(raw) &&
                   float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }

        // A float loses exactness above 2^24, and a seed is exactly the kind of value where a
        // silently-rounded high bit changes which sequence Rng produces without anyone noticing —
        // so the seed gets its own parse straight to ulong rather than going through Env(float).
        private static ulong EnvULong(string name, ulong fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrEmpty(raw) &&
                   ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v)
                ? v
                : fallback;
        }
    }
}
