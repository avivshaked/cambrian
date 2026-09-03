using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
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
            // The pre-round-8 experiment contract's first repair: every static below is
            // process-lifetime, and -executeMethod exits after one run so it never mattered —
            // but Evosim/Run from the editor menu does not exit, and a second run in the same
            // session would otherwise start with the first run's "ever jointed" ids, floor-spawn
            // count and excretion baseline already in it.
            ResetStaticReportState();

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

            // How fast remains and dissolved matter fall. The default is a large-aggregate rate
            // (0.02 m/s is ~1,700 m/day); the remains of a 0.01 m3 body are marine snow, and
            // round 12 found rolls that stop above the floor act as a trapdoor at this speed.
            float nutrientSink = Env("EVOSIM_SINK", new RunConfig().NutrientSinkMetresPerSecond);
            float matterSink = Env("EVOSIM_MATTER_SINK", new RunConfig().MatterSinkMetresPerSecond);

            // D066. The current's own geometry and clock, swept from here for the first time — a
            // cell deeper than the photic band stirs producers into the dark for half of every
            // cycle (Sverdrup 1953), which is a real constraint and therefore one an arm has to be
            // able to vary. Both default to RunConfig's own values, so a run that sets neither is
            // the field every earlier number was measured in.
            float currentPeriod = Env("EVOSIM_CURRENT_PERIOD", new RunConfig().Current.PeriodSeconds);
            float currentCell = Env("EVOSIM_CURRENT_CELL", new RunConfig().Current.CellMetres);

            // D066's own three. Rolls turn the depth-only oscillation into convection cells over
            // D061's patches; the blink reverses their parity, which is what turns a circulation
            // into a stirrer; advect carries detritus and matter with the water rather than
            // leaving them to diffusion. All three off is round 11's world exactly.
            bool currentRolls = Env("EVOSIM_CURRENT_ROLLS", 0f) > 0.5f;
            float currentBlink = Env("EVOSIM_CURRENT_BLINK", 0f);
            bool currentAdvect = Env("EVOSIM_CURRENT_ADVECT", 0f) > 0.5f;

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

            // Arm C. Fraction of a refuge layer's density feeding may still see and take — D055's
            // hard, total-exclusion refuge generalised to a partial one, on the owner's standing
            // hypothesis that whole-layer horizontal access is the deeper distortion rather than
            // floor access itself. 0 is D055's own refuge (or, with floorRefuge also 0, no refuge
            // at all) — the world every earlier run measured.
            float refugeFraction = Env("EVOSIM_REFUGE_FRACTION", new RunConfig().RefugeEdibleFraction);

            // D062. The satiation cap and its type-III toe — the mouth's physical limit and the
            // relaxation-at-low-density stabiliser the recruitment-collapse mechanism
            // (logbook/0043) points at. Both 0 is the world every earlier run measured, in which
            // AbsorptiveCell's clearance is unbounded and linear at every density.
            float satiation = Env("EVOSIM_SATIATION", new RunConfig().SatiationWattsPerCubicMetre);
            float clearanceToe = Env("EVOSIM_CLEARANCE_TOE", new RunConfig().ClearanceToeDensity);

            // D057. Genome-distance drift threshold for species accounting — pure instrumentation,
            // read by nothing but this report. 0 is the world every earlier run measured, where
            // species machinery never runs at all and every creature reads species 0.
            float speciesTheta = Env("EVOSIM_SPECIES_THETA", new RunConfig().SpeciesDriftThreshold);

            // D061. The patchy world: horizontal cells per layer, the throttled exchange between
            // them, the metapopulation-style dispersal creatures may pay for, and the endogenous
            // shading that makes patches unequal without a painted-on constant. K=1 and every
            // other knob at 0 is the world every earlier run measured — a single, perfectly-mixed
            // column per layer, exactly D061's own "today's world" baseline.
            float patches = Env("EVOSIM_PATCHES", new RunConfig().HorizontalPatches);
            float horizontalMixing = Env("EVOSIM_H_MIXING", new RunConfig().HorizontalMixingDiffusivity);
            float dispersalChance = Env("EVOSIM_DISPERSAL", new RunConfig().DispersalChancePerStep);
            float patchShading = Env("EVOSIM_PATCH_SHADING", new RunConfig().PerPatchShading);

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

            // D064. Body volume at which tissue is neutrally buoyant, m3 — the excess density
            // above is scaled by max(0, 1 - (V0/V)^(2/3)), so a founder-sized body barely sinks
            // and a large one feels the full constant. 0 is off and reproduces every pre-D064 run
            // exactly, which is why the default is 0 rather than anything founder-shaped.
            float neutralVolume = Env("EVOSIM_NEUTRAL_VOLUME", 0f);

            // How far below the waterline founders are scattered, m. RunConfig's own default,
            // swept from here because where generation zero starts decides how much of the column
            // it ever sees — a spread narrower than the habitable band is a hidden choice about
            // which depths get to compete.
            float founderDepth = Env("EVOSIM_FOUNDER_DEPTH", new RunConfig().FounderDepthSpread);

            // D048. Matter a child's tissue costs, per joule of it, and what the column starts
            // with per cubic metre. 0 is the world as it was before D048 — producers consuming
            // nothing, no negative feedback on occupying the best depth, every run sorting to the
            // surface. Read the blocked-conception count: matter that never binds changes nothing
            // and reads identically to matter that is switched off.
            float matterPerTissue = Env("EVOSIM_MATTER_PER_TISSUE", 0f);
            float initialMatter = Env("EVOSIM_MATTER_INITIAL", 1f);

            // D065. What a body costs in matter before any of it is proportional to size. 0 is the
            // pre-D065 world, where a lineage can buy one more individual by making every
            // individual smaller and head-count has no ceiling; above 0 the population is bounded
            // by total matter / (fixed + proportional) however small bodies get.
            float matterPerCreature = Env("EVOSIM_MATTER_PER_CREATURE", 0f);

            // D049. Chance a tail-less founder is born with a gas bladder, and what holding lift
            // costs. 0 is a world where buoyancy has to be *found* by mutation rather than given
            // — which of those happened is most of what D049 is trying to measure, so it shows in
            // the header and the hash.
            float floatChance = Env("EVOSIM_FOUNDER_FLOAT", 0f);
            float liftCost = Env("EVOSIM_LIFT_COST", 0.05f);

            // D060. The invasion assay: a labeled hand that injects a verified genome once, at a
            // fixed simulated time, so a consumer lineage can be studied without waiting on the
            // world's own mutation supply to find one. Empty path is the off state — every earlier
            // run measured a world that never heard of this, and the three timing/dose knobs
            // default to RunConfig's own (0, so "never") for exactly that reason. The genome
            // itself is never a tunable — it is a file, not a number — so its identity is recorded
            // separately, in the header and run.json, rather than folded into configHash.
            string inoculatePath = Environment.GetEnvironmentVariable("EVOSIM_INOCULATE");
            float inoculateAt = Env("EVOSIM_INOCULATE_AT", new RunConfig().InoculateAtSeconds);
            int inoculateCount = (int)Env("EVOSIM_INOCULATE_COUNT", new RunConfig().InoculateCount);
            float inoculateDepth = Env("EVOSIM_INOCULATE_DEPTH", new RunConfig().InoculateDepthMetres);

            // Loaded at startup rather than when the assay fires, so a malformed or missing file
            // fails the run immediately instead of thousands of simulated seconds in — §9's
            // "loading refuses rather than defaults" applies here exactly as it does to every other
            // genome this project reads.
            Genome inoculumGenome = null;
            string inoculumHash = null;
            string inoculumHashShort = null;

            if (!string.IsNullOrEmpty(inoculatePath))
            {
                byte[] inoculumBytes = File.ReadAllBytes(inoculatePath);
                inoculumGenome = GenomeJson.Read(Encoding.UTF8.GetString(inoculumBytes));

                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] digest = sha256.ComputeHash(inoculumBytes);
                    inoculumHash = BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
                }

                inoculumHashShort = inoculumHash.Substring(0, 12);
            }
            else if (inoculateAt > 0f)
            {
                // The identical-numbers gotcha (CLAUDE.md), caught before it can happen rather
                // than after: a timing knob set with nothing to inject would silently do nothing,
                // and every column downstream would read exactly like a world that was never
                // asked to run the assay at all.
                Debug.LogWarning(
                    "EVOSIM_INOCULATE_AT is set but EVOSIM_INOCULATE names no genome file — the " +
                    "assay will not fire.");
            }

            if (inoculumGenome != null && inoculateAt <= 0f)
            {
                // The same trap mirrored: a genome named with no instant to fire at also runs a
                // world that reads exactly like one never asked to run the assay.
                Debug.LogWarning(
                    "EVOSIM_INOCULATE names a genome file but EVOSIM_INOCULATE_AT is unset or " +
                    "zero — the assay will not fire.");
            }

            bool inoculateOn = inoculumGenome != null && inoculateAt > 0f;

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
                Fluid = new FluidConfig
                {
                    TissueExcessDensity = excessDensity,
                    NeutralBodyVolume = neutralVolume,
                },
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
            config.Current.PeriodSeconds = currentPeriod;
            config.Current.CellMetres = currentCell;
            config.Current.Rolls = currentRolls;
            config.Current.RollBlinkSeconds = currentBlink;
            config.Current.AdvectFields = currentAdvect;
            config.MatterPerTissueJoule = matterPerTissue;
            config.MatterPerCreature = matterPerCreature;
            config.InitialMatterPerCubicMetre = initialMatter;
            config.Genome.FounderFloatChance = floatChance;
            config.FounderDepthSpread = founderDepth;
            config.NutrientMixingDiffusivity = mixing;
            config.NutrientSinkMetresPerSecond = nutrientSink;
            config.MatterSinkMetresPerSecond = matterSink;
            config.NutrientRemineralisationPerSecond = remin;
            config.MatterRemineralisationPerSecond = remin;
            config.ExcretionPerJoule = excretion;
            config.FloorRefugeMetres = floorRefuge;
            config.RefugeEdibleFraction = refugeFraction;
            config.SatiationWattsPerCubicMetre = satiation;
            config.ClearanceToeDensity = clearanceToe;
            config.SpeciesDriftThreshold = speciesTheta;
            config.HorizontalPatches = patches;
            config.HorizontalMixingDiffusivity = horizontalMixing;
            config.DispersalChancePerStep = dispersalChance;
            config.PerPatchShading = patchShading;
            config.WorldAreaSquareMetres = area;
            config.FloorClosesAfterSeconds = floorCloses;
            config.MaximumPopulation = maxPopulation;
            config.SenescenceDoublingSeconds = senescence;
            config.Mutation.CellTypeChance = cellTypeMutation;
            config.InoculateAtSeconds = inoculateAt;
            config.InoculateCount = inoculateCount;
            config.InoculateDepthMetres = inoculateDepth;
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
                // D066. The current is three numbers and two switches now, not one number, and a
                // header that named only the speed would describe five different worlds
                // identically — which is exactly the failure the run-header rule exists to stop.
                " · current " + currentSpeed + " m/s over " + currentPeriod + " s in " +
                currentCell + " m cells" +
                " · rolls " + (currentRolls
                    ? currentBlink > 0f ? "blink " + currentBlink + " s" : "steady"
                    : "off") +
                " · advect " + (currentAdvect ? "on" : "off") +
                " · mixing " + mixing + " m2/s" +
                " · sink " + nutrientSink + " m/s, matter " + matterSink + " m/s" +
                " · remin " + remin + " /s" +
                " · excretion " + excretion + " /J" +
                " · refuge " + floorRefuge + " m" +
                (refugeFraction > 0f ? " at " + refugeFraction + " edible" : "") +
                (satiation > 0f ? " · satiation " + satiation + " W/m3" : "") +
                (clearanceToe > 0f ? " · toe " + clearanceToe + " J/m3" : "") +
                " · speciesTheta " + speciesTheta +
                (patches > 1f
                    ? " · patches " + (int)patches + ", h-mix " + horizontalMixing + " m2/s, " +
                      "disperse " + dispersalChance + ", patchShade " + patchShading
                    : "") +
                " · area " + area + " m2" +
                (floorCloses > 0f ? " · floor closes " + floorCloses + " s" : " · floor open") +
                " · ceiling " + maxPopulation +
                " · senescence " + (senescence > 0f ? senescence + " s" : "off") +
                " · cellType mut " + cellTypeMutation +
                " · clearance " + clearance +
                " · linkPhoto " + linkPhoto +
                " · excessDensity " + excessDensity + " kg/m3" +
                " · neutralV " + neutralVolume + " m3" +
                " · founderDepth " + founderDepth + " m" +
                // D065's fixed term is rendered unconditionally, even at 0, so every header from
                // here on has the same shape and a reader never has to know whether a missing
                // token means "off" or "written before the knob existed". This changes the header
                // text of a pre-D065 configuration; the world it describes is bit-identical at 0,
                // but the configHash is not — a new tunable enters ConfigSchema and therefore
                // Hash(), as it does for every knob this project has added.
                " · matter " + matterPerTissue + "/J + " + matterPerCreature +
                " each from " + initialMatter + "/m3" +
                " · float " + floatChance + " at " + liftCost + " W/lift" +
                (inoculateOn
                    ? " · inoculate " + inoculateCount + " @ " + inoculateAt + " s, " +
                      inoculateDepth + " m, genome " + inoculumHashShort
                    : "") +
                " · configHash `" + config.Hash() + "`");
            report.AppendLine();
            report.AppendLine(Header());

            Flush(outPath, report);

            var clock = Stopwatch.StartNew();

            // Two readings of how the run ended: `ending` is the prose the markdown footer
            // prints, `terminationCode` is D058's own vocabulary (extinct / budget / wall /
            // ceiling) for run.json, where a script reads it rather than a person. Both start
            // null rather than defaulted to "budget reached" — the pre-round-8 contract's fix for
            // the footer reading "budget reached" on an arm the wall clock actually cut, which
            // made every censored arm look like a completed one to anyone skimming the footer
            // rather than the header's budget against ElapsedSeconds.
            string ending = null;
            string terminationCode = null;
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

                    // D060. Fires once — the first metabolic step whose ElapsedSeconds reaches
                    // the pre-registered instant — and never again, guarded the same way
                    // FloorClosesAfterSeconds guards its own one-shot transition. Checked before
                    // the extinction test below, so an assay that lands on an empty world rescues
                    // it by design rather than being pre-empted by the extinction break.
                    if (inoculateOn && !AssayFired && eco.World.ElapsedSeconds >= inoculateAt)
                    {
                        eco.World.Inoculate(inoculumGenome, inoculateCount, -inoculateDepth);
                        AssayFired = true;
                    }

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
                        terminationCode = "extinct";
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

                // Reached whenever the loop above finished without the extinction break — either
                // the while condition's left side failed (budget) or its right side did (wall).
                // D058: only a budget-complete arm may pass the persistence endpoint, so this is
                // not cosmetic — "wall clock reached" here is what makes a censored arm readable
                // as censored from the footer alone, rather than requiring a reader to compare
                // ElapsedSeconds against the header's budget by hand.
                if (terminationCode == null)
                {
                    if (eco.World.ElapsedSeconds >= budgetSeconds)
                    {
                        ending = "budget reached";
                        terminationCode = "budget";
                    }
                    else
                    {
                        ending = "wall clock reached";
                        terminationCode = "wall";
                    }
                }
            }
            catch (PopulationRunawayException runaway)
            {
                // D021: not a crash. It locates the generous end of the calibration exactly as
                // extinction locates the lean end, and culling to fit a compute budget would be
                // selection performed by us. D058 files this the same as a wall cut: censored,
                // never a pass.
                ending =
                    "RUNAWAY at t=" + runaway.ElapsedSeconds.ToString("0.#") + " s with " +
                    runaway.Population + " alive — light is covering upkeep so completely that " +
                    "nothing has to do anything";
                terminationCode = "ceiling";
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
                WriteRunIdentity(
                    dir, seed, budgetSeconds, wallMinutes, outPath, terminationCode,
                    inoculatePath, inoculumHash);

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

        /// <summary>World.ExcretedTotal as of the previous report row, so a row can show a flux.</summary>
        /// <remarks>Same delta trick as <see cref="LastFloorSpawns"/> and <see cref="LastMatterBlocks"/>,
        /// against <see cref="World.ExcretedTotal"/> — a cumulative counter with no cap of its own.</remarks>
        private static double LastExcretedTotal;

        /// <summary>Whether D060's assay has already fired this run — the one-shot guard.</summary>
        /// <remarks>
        /// Static for the same reason every other field here is: a second <c>Evosim/Run</c> from
        /// the editor menu in one session must not inherit the previous run's "already fired"
        /// state, so it is cleared alongside everything else in <see cref="ResetStaticReportState"/>
        /// rather than trusted to a fresh local that happens to be correct today.
        /// </remarks>
        private static bool AssayFired;

        /// <summary>
        /// Clears every static above so repeated <c>Evosim/Run</c> invocations in one editor
        /// session cannot inherit a previous run's history.
        /// </summary>
        /// <remarks>
        /// Pre-round-8 experiment contract, item 1. <c>-executeMethod</c> from the command line
        /// exits the process after one run, which is every arm this project has launched so far —
        /// so this was silently correct by accident rather than by design, and the accident breaks
        /// the moment someone runs the menu item twice without restarting Unity.
        /// </remarks>
        private static void ResetStaticReportState()
        {
            EverAbsorptive.Clear();
            EverJointed.Clear();
            EverBuoyant.Clear();
            LastFloorSpawns = 0;
            LastMatterBlocks = 0;
            LastExcretedTotal = 0;
            AssayFired = false;
        }

        /// <summary>
        /// Writes <c>run.json</c>: the identity of this run, separate from <c>config.json</c>'s
        /// resolved tunables — pre-round-8 experiment contract, item 2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why not just extend config.json.</b> <c>RunConfigJson.Write</c> is generated from
        /// <see cref="ConfigSchema"/> and runs once, at <see cref="RunDirectory.Create"/>, before
        /// a single step has been taken — every field it can carry is a <see cref="RunConfig"/>
        /// tunable, known up front. The termination reason is the opposite of that: unknowable
        /// until the run stops, and not itself a tunable. Folding it in would mean either writing
        /// config.json a second time (making it look editable when only the first write is) or
        /// growing <see cref="RunConfig"/> a field that is not a setting at all. A second
        /// hand-written file, in the same <see cref="Json"/> style config.json already uses, says
        /// what it is without either problem.
        /// </para>
        /// <para>
        /// <b>What is missing on purpose.</b> Git commit, dirty flag and worker source hash all
        /// need shelling out from inside the Editor process, which is its own small design
        /// question — does a failed shell call block the run, what counts as "the worker", does
        /// it belong here or in a preflight — and none of that is this item's job. TODO(run
        /// identity): git commit + dirty flag, worker source fingerprint — HANDOFF.md's
        /// "Run-integrity infrastructure" queue entry, deliberately deferred alongside them.
        /// </para>
        /// </remarks>
        private static void WriteRunIdentity(
            RunDirectory dir, ulong seed, float requestedSeconds, float requestedWallMinutes,
            string outPath, string terminationCode,
            string inoculatePath, string inoculumHash)
        {
            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("seed", seed);
            w.Field("unityVersion", Application.unityVersion);
            w.Field("physicsDtSeconds", Ecosystem.FixedDt);
            w.Field("metabolicStepSeconds", Ecosystem.StepsPerMetabolicStep * Ecosystem.FixedDt);
            w.Field("requestedSeconds", requestedSeconds);
            w.Field("requestedWallMinutes", requestedWallMinutes);
            // Named after the report rather than passed in explicitly: nothing upstream of this
            // arm carries a name of its own (scripts/run-arm.ps1's -Name only ever becomes the
            // output path), and RunDirectory.Create already treats this same derivation as the
            // arm's identity when it names the run directory.
            w.Field("armName", Path.GetFileNameWithoutExtension(outPath));
            w.Field("terminationReason", terminationCode);

            // D060. Null for a run that never named a genome — the timing and dose knobs already
            // reach config.json and its hash; this is the genome's own identity, which cannot,
            // because a genome is a file rather than a number.
            w.Field("inoculateGenomePath", inoculatePath);
            w.Field("inoculateGenomeHash", inoculumHash);
            w.EndObject();

            File.WriteAllText(Path.Combine(dir.Path, "run.json"), w.ToString(), Utf8NoBom);
        }

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

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

            // D059's below-world observables, pre-round-8 experiment contract item 3. Below
            // rather than at or below: a creature resting exactly at -WorldDepthMetres is on the
            // seabed D059 clamps to, not past it, and the whole point of these two counts is to
            // tell that creature apart from one that fell through a floor the clamp has not been
            // switched on for yet (D059 ships default-off).
            int belowWorld = 0, absorptiveBelowWorld = 0;

            // D052's own instrument, summed rather than read once: LockedMatter lives on each
            // organism, and StandingMatter already folds it into one number with detritus, which
            // is exactly what this column exists to pull back apart.
            double matterLocked = 0d;

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
                bool creatureAbsorptive = false;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    if (part.CellTypeId != CellTypeIds.Absorptive) continue;

                    absorptive++;
                    creatureAbsorptive = true;
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

                matterLocked += creature.LockedMatter;

                if (creature.HeightY < -world.Config.WorldDepthMetres)
                {
                    belowWorld++;
                    if (creatureAbsorptive) absorptiveBelowWorld++;
                }
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

            // Pre-round-8 experiment contract, item 3: what a mouth at the population's own depth
            // can actually reach — D055's refuge-aware reading, against detritusHere's field-truth
            // one above — and the floor-layer stock the refuge protects. Both cheap: EdibleDensityAt
            // and StockInLayer are simple array reads, not a second pass over the population.
            //
            // D061: patch 0, explicitly. These columns predate patches and read one column's
            // worth of the world rather than a population-wide aggregate; at K=1 patch 0 is the
            // whole world and nothing here changes. "det patch sd" and "patch max share" below
            // are the columns that carry the cross-patch picture.
            double edibleHere = world.Nutrients.EdibleDensityAt((float)meanDepth, 0);
            double refugeStock = world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1, 0);

            // D052's flux, not its balance: MatterInBodies and Matter.TotalJoules already show
            // what excretion moved by comparing before and after, but neither shows the rate it
            // moved at. Windowed the same way floorSpawns and conceptionsBlockedByMatter are.
            double excretedWindow = world.ExcretedTotal - LastExcretedTotal;

            // D061. The asynchrony observables — the two readings the old, patch-blind columns
            // above cannot give, because they only ever look at one column of the world (patch
            // 0). Both read 0 at K=1, where there is only one patch to compare against itself.
            int patchesForReport = Math.Max(1, (int)world.Config.HorizontalPatches);
            double detritusPatchSd = 0d;
            double patchMaxShare = 0d;

            if (patchesForReport > 1)
            {
                // Plain spatial standard deviation of deep-layer detritus density across
                // patches, population size unweighted — the same 90%-of-depth reading
                // "det deep"/"mat deep" already take, generalised sideways instead of down. A
                // world where every patch tracks together reads near 0; a world where some
                // patches are booming while others are busted reads high.
                float deepHeight = -(float)world.Config.WorldDepthMetres * 0.9f;
                var patchDensities = new double[patchesForReport];
                double meanPatchDensity = 0d;

                for (int p = 0; p < patchesForReport; p++)
                {
                    patchDensities[p] = world.Nutrients.DensityAt(deepHeight, p);
                    meanPatchDensity += patchDensities[p];
                }
                meanPatchDensity /= patchesForReport;

                double sumSquares = 0d;
                for (int p = 0; p < patchesForReport; p++)
                {
                    double d = patchDensities[p] - meanPatchDensity;
                    sumSquares += d * d;
                }
                detritusPatchSd = Math.Sqrt(sumSquares / patchesForReport);

                // The largest patch's share of the living population, 0-1 — how concentrated
                // the world currently is. 1/K is an evenly-spread population; 1 is everyone in
                // one patch.
                var patchCounts = new int[patchesForReport];
                for (int i = 0; i < world.Living.Count; i++)
                {
                    int p = world.Living[i].Patch;
                    if (p >= 0 && p < patchesForReport) patchCounts[p]++;
                }

                int maxCount = 0;
                for (int p = 0; p < patchesForReport; p++)
                {
                    if (patchCounts[p] > maxCount) maxCount = patchCounts[p];
                }

                patchMaxShare = alive > 0 ? (double)maxCount / alive : 0d;
            }

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
                .Field("detritusHere", world.Nutrients.DensityAt((float)meanDepth, 0))
                .Field("detritusOnFloor",
                    world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1, 0))
                .Field("detritusDeep",
                    world.Nutrients.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0))
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
                .Field("matterHere", world.Matter.DensityAt((float)meanDepth, 0))
                .Field("matterSurface", world.Matter.DensityAt(0f, 0))
                .Field("matterDeep", world.Matter.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0))
                .Field("matterStanding", world.StandingMatter)
                .Field("conceptionsBlockedByMatter", world.ConceptionsBlockedByMatter)
                .Field("floorSpawns", world.FloorSpawns)
                .Field("floorSpawnsWindow", world.FloorSpawns - LastFloorSpawns)
                .Field("secondsSinceFloorFired", world.SecondsSinceFloorFired)
                .Field("generationMin", genMin)
                .Field("generationMax", genMax)
                .Field("auditResidual", world.AuditResidual)
                .Field("species", speciesSeen.Count)
                // Pre-round-8 experiment contract, item 3 — appended after species per the same
                // rule species itself was added under: existing readers index by position, so
                // nothing already written may move.
                .Field("edibleDetritusHere", edibleHere)
                .Field("belowWorld", belowWorld)
                .Field("absorptiveBelowWorld", absorptiveBelowWorld)
                .Field("matterLocked", matterLocked)
                .Field("refugeJoules", refugeStock)
                .Field("excretedTotal", world.ExcretedTotal)
                .Field("excretedWindow", excretedWindow)
                // D061 — appended after excretedWindow, per the append-only column discipline.
                .Field("detritusPatchSd", detritusPatchSd)
                .Field("patchMaxShare", patchMaxShare));

            // The lineage-events instrument (pre-round-8, LITERATURE-REVIEW.md §9 item 9): drained
            // every report row, alongside stats.jsonl, and appended one row per event to
            // lineage.jsonl through the same JsonlWriter. Drained even when dir is null (no run
            // directory) so the queue in World never grows for the life of a run that has nowhere
            // to write it.
            IReadOnlyList<LineageEvent> lineageEvents = world.DrainLineageEvents();
            if (dir != null)
            {
                for (int i = 0; i < lineageEvents.Count; i++) dir.Lineage.Write(lineageEvents[i].ToJson());
            }

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
                // want opposite responses. D061: patch 0 — see the edibleHere/refugeStock remark
                // above for why these legacy columns are not redefined as a cross-patch aggregate.
                "**" + world.Nutrients.DensityAt((float)meanDepth, 0).ToString("0.####", c) + "**",
                "**" + (world.Nutrients.TotalJoules > 0d
                    ? 100d * world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1, 0) /
                      world.Nutrients.TotalJoules
                    : 0d).ToString("0.#", c) + "%**",

                // D051's return leg: detritus density in the deep water, 90% of depth — the same
                // reading the matter field takes below. The floor layer is the last 1 m; this is
                // the water above it, and the prediction under test is that this number
                // rises once remineralisation leaks matter back out of the floor.
                "**" + world.Nutrients.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0)
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
                world.Matter.DensityAt(0f, 0).ToString("0.###", c),
                world.Matter.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0)
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

                // Pre-round-8 experiment contract, item 3 — same append-only rule, six more.
                // D055's refuge-aware density at the population's own depth, against `det here`
                // above (the field-truth reading feeding never sees inside a refuge).
                edibleHere.ToString("0.####", c),
                // D059's below-world observables: count, and the same count restricted to
                // absorptive tissue. Read together with the header's floor knob — nonzero here
                // with the D059 clamp off is a world with no seabed yet; nonzero with it on is
                // the clamp failing to hold.
                belowWorld.ToString(c),
                absorptiveBelowWorld.ToString(c),
                // Matter still owed to the field by living bodies — the other half of
                // StandingMatter from `mat top`/`mat deep`'s free-field reading.
                matterLocked.ToString("0.###", c),
                // The floor layer alone, in joules — `% on floor` already reports this as a share
                // of TotalJoules; this is the same quantity a refuge-transport reading can be
                // taken against without first re-deriving it from a percentage.
                refugeStock.ToString("0.#", c),
                // D052's flux since the last row, not its running total: how much excretion moved
                // in this window, the same delta shape as `mat blk` and `floor` above.
                excretedWindow.ToString("0.######", c),

                // D061 — appended after excretedWindow, per the same append-only rule species
                // itself was added under. Both read 0 at K=1 (see the computation above).
                detritusPatchSd.ToString("0.####", c),
                patchMaxShare.ToString("0.###", c),
            };

            LastFloorSpawns = world.FloorSpawns;
            LastMatterBlocks = world.ConceptionsBlockedByMatter;
            LastExcretedTotal = world.ExcretedTotal;

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

            // Pre-round-8 experiment contract, item 3 — appended after species, per its own
            // comment above.
            "det here ed", "below world", "abs below", "mat locked", "refuge J", "excreted",

            // D061 — appended after excreted, per the same append-only rule.
            "det patch sd", "patch max share",
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
