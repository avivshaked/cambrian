using System;
using System.Collections.Generic;
using System.Globalization;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// The environment a run is launched with, and the <see cref="RunConfig"/> it builds —
    /// <c>EvolutionRun.RunBody</c>'s first three hundred lines, out of Unity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The acceptance is arithmetic, not judgement</b> (work package I): the same launcher
    /// must give the same <c>configHash</c>. So every knob below is transcribed from
    /// <c>unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs</c> with its own default, in the order
    /// that file reads them, and <see cref="BuildConfig"/> assembles the config in the order that
    /// file assembles it — including the two places it sorts a min/max pair rather than trusting
    /// the launcher. A default that drifted by one line here would produce a world nobody asked
    /// for, filed under a hash that says it is round 42's.
    /// </para>
    /// <para>
    /// <b>Why a table.</b> One line per variable — name, default, destination — so a reviewer can
    /// read it beside <c>EvolutionRun</c>'s <c>float x = Env("EVOSIM_X", d);</c> and see the two
    /// agree without tracing control flow. <see cref="Table"/> is the whole binding: nothing
    /// outside it reads the environment.
    /// </para>
    /// <para>
    /// <b>An unknown <c>EVOSIM_*</c> variable is ignored</b>, exactly as <c>EvolutionRun</c>
    /// ignores it: that file asks the environment for the names it knows and never enumerates it,
    /// so a typo has always been silent. This keeps the behaviour and adds a reading of it —
    /// <see cref="UnknownNames"/> lists what was set and not bound, which
    /// <see cref="Program"/> prints. Printing changes nothing about the run; it is the
    /// identical-numbers gotcha (CLAUDE.md) given a voice it never had.
    /// </para>
    /// </remarks>
    public static class EnvBinding
    {
        /// <summary>How a setting is looked up. The process environment, or a test's dictionary.</summary>
        public delegate string Lookup(string name);

        /// <summary>The process environment.</summary>
        public static readonly Lookup Process = Environment.GetEnvironmentVariable;

        /// <summary>A lookup over a dictionary — what a test binds against.</summary>
        public static Lookup Of(IReadOnlyDictionary<string, string> values) =>
            name => values != null && values.TryGetValue(name, out string v) ? v : null;

        /// <summary>
        /// The defaults <c>EvolutionRun</c> spells as <c>new RunConfig().X</c>.
        /// </summary>
        /// <remarks>
        /// One instance rather than one per knob: <c>RunConfig</c>'s constructor is pure and every
        /// <c>new RunConfig()</c> in <c>EvolutionRun</c> is read for one default and discarded, so
        /// the values are identical and the expressions below stay readable.
        /// </remarks>
        private static readonly RunConfig D = new RunConfig();

        /// <summary>The metabolic step — <c>Ecosystem.MetabolicStepSeconds</c>, and not a tunable.</summary>
        public const float MetabolicStepSeconds = 0.5f;

        /// <summary>The physics step before <c>EVOSIM_DT</c> — <c>Ecosystem.FixedDt</c>'s initial value.</summary>
        public const float DefaultPhysicsStepSeconds = 0.01f;

        // ------------------------------------------------------------------ the table
        //
        // In EvolutionRun's own order. Name, default, destination.

        private static readonly Knob[] Table =
        {
            Num("EVOSIM_IRRADIANCE", 48f, (s, v) => s.Irradiance = v),
            Num("EVOSIM_LIGHT_REACH", 12f, (s, v) => s.LightReach = v),
            Flag("EVOSIM_SILHOUETTE", (s, v) => s.SilhouetteCap = v),

            // D110: a part earns on and shades with its area in its pose. Off is the recorded world.
            Flag("EVOSIM_LIGHT_EXPOSURE", (s, v) => s.LightByExposure = v),

            // D111: the buoyancy offset's price, which is also its switch. 0 is the recorded world.
            Num("EVOSIM_BUOYANCY_OFFSET_COST", 0f, (s, v) => s.BuoyancyOffsetCost = v),

            // D113: the support cost's price, W per m² of lit area per m² of distance from the
            // root. 0 is the recorded world.
            Num("EVOSIM_SUPPORT", 0f, (s, v) => s.Support = v),
            Num("EVOSIM_SELF_OVERLAP", 0f, (s, v) => s.SelfOverlap = v),
            Num("EVOSIM_MAX_REACH", 0f, (s, v) => s.MaxReach = v),

            // The owner's ruling of 2026-09-24 (buds): both floors weigh the rigid body a welded
            // part is carried in. Off is the recorded world.
            Flag("EVOSIM_RIGID_FLOORS", (s, v) => s.RigidFloors = v),

            // D109: the matter seeded as islands, founders planted in them, and the light's shade
            // map from the same noise, drifting or not. Every default is the recorded world.
            Num("EVOSIM_MATTER_ISLANDS", 0f, (s, v) => s.MatterIslands = v),
            Num("EVOSIM_MATTER_ISLAND_COVER", D.MatterIslandCover, (s, v) => s.MatterIslandCover = v),
            Num("EVOSIM_MATTER_ISLAND_DEPTH", D.MatterIslandDepthMetres, (s, v) => s.MatterIslandDepth = v),
            Flag("EVOSIM_FOUNDERS_FOLLOW_MATTER", (s, v) => s.FoundersFollowMatter = v),

            // D116: the founder rule reads the field the founder's body eats. Off is the recorded
            // world, and the world refuses it beside D109's.
            Flag("EVOSIM_FOUNDERS_FOLLOW_FOOD", (s, v) => s.FoundersFollowFood = v),

            // The round 48 founding ruling (owner, 2026-09-24): a founder accepted in a column is
            // set at the richest cell of its food there, and every founder is born holding that
            // many seconds of its own standing cost. Off and 0 are the recorded world; the depth
            // rule is refused without D116's.
            Flag("EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH", (s, v) => s.FoundersFollowFoodDepth = v),
            Num("EVOSIM_FOUNDER_ENDOWMENT", 0f, (s, v) => s.FounderEndowment = v),
            Num("EVOSIM_LIGHT_SHADE", 0f, (s, v) => s.LightShade = v),
            Num("EVOSIM_LIGHT_SHADE_DRIFT", 0f, (s, v) => s.LightShadeDrift = v),
            Num("EVOSIM_SECONDS", 4000f, (s, v) => s.BudgetSeconds = v),
            Num("EVOSIM_WALL_MINUTES", 30f, (s, v) => s.WallMinutes = v),
            Int("EVOSIM_REPORT_EVERY", 200f, (s, v) => s.ReportEvery = v),
            Custom("EVOSIM_SEED", (s, env) => s.Seed = ULong(env, "EVOSIM_SEED", 1UL)),
            Long("EVOSIM_DIGEST_EVERY", 0f, (s, v) => s.DigestEvery = v),
            Custom("EVOSIM_DIGEST_DUMP_STEPS", (s, env) => s.DigestDumpSteps = Steps(env, "EVOSIM_DIGEST_DUMP_STEPS")),

            // Unity's job-worker count (D078). Bound so that a launcher carrying it is still read
            // rather than silently half-applied, and applied to nothing: this engine has no job
            // system, and the spike's identity check makes the thread count a pace setting rather
            // than a realisation. Program says so when it is set; EVOSIM_THREADS is the knob.
            Int("EVOSIM_PHYSICS_JOBS", 0f, (s, v) => s.RequestedJobWorkers = v),

            // The farm's own, and the only name in this table that EvolutionRun does not read:
            // how many threads step the bodies. 0 is one per processor. It decides nothing about
            // the ecology — the spike's digests are equal at 1, 4 and 16 threads — so like
            // EVOSIM_PHYSICS_JOBS it must not reach config.json or its hash, and it is recorded in
            // run.json instead.
            Int("EVOSIM_THREADS", 0f, (s, v) => s.Threads = v),

            // The third name here that EvolutionRun does not read, and a recording setting rather
            // than a world one: how often poses.bin takes a frame, in simulated seconds. 0 writes
            // no file. It is bound beside EVOSIM_REPORT_EVERY and EVOSIM_DIGEST_EVERY, and like
            // them it reaches EnvSettings and never RunConfig, so no config hash moves and every
            // recorded run reads as it did (logbook/specs/state-stream-spec.md).
            Num("EVOSIM_POSE_EVERY", 0f, (s, v) => s.PoseEvery = v),

            // The fourth, fifth, sixth and seventh names EvolutionRun does not read, and a
            // recording setting of the same kind as the one above: how often the whole world is
            // written down, where to start from instead of founding one, which second of it, and
            // whether a build mismatch is a warning rather than a refusal. None of them reaches
            // RunConfig or its hash (logbook/specs/checkpoint-spec.md).
            Num("EVOSIM_CHECKPOINT_EVERY", 0f, (s, v) => s.CheckpointEvery = v),
            Text("EVOSIM_RESUME", (s, v) => s.ResumeFrom = v),
            Num("EVOSIM_RESUME_AT", 0f, (s, v) => s.ResumeAt = v),
            Flag("EVOSIM_ALLOW_SOURCE_MISMATCH", (s, v) => s.AllowSourceMismatch = v),

            Num("EVOSIM_IDLE", 0.02f, (s, v) => s.Idle = v),
            Num("EVOSIM_MAXPOWER", RandomGenomeOptions.Default.MaxLinkPower, (s, v) => s.MaxPower = v),
            Num("EVOSIM_MINPOWER", RandomGenomeOptions.Default.MinLinkPower, (s, v) => s.MinPower = v),
            Num("EVOSIM_LINK_PHOTO", 0f, (s, v) => s.LinkPhoto = v),
            Num("EVOSIM_DAY_AMPLITUDE", 0f, (s, v) => s.DayAmplitude = v),
            Num("EVOSIM_DAY_LENGTH", 200f, (s, v) => s.DayLength = v),
            Num("EVOSIM_CURRENT", 0f, (s, v) => s.CurrentSpeed = v),
            Num("EVOSIM_MIXING", 0f, (s, v) => s.Mixing = v),
            Num("EVOSIM_SINK", D.NutrientSinkMetresPerSecond, (s, v) => s.NutrientSink = v),
            Num("EVOSIM_MATTER_SINK", D.MatterSinkMetresPerSecond, (s, v) => s.MatterSink = v),
            Num("EVOSIM_CURRENT_PERIOD", D.Current.PeriodSeconds, (s, v) => s.CurrentPeriod = v),
            Num("EVOSIM_CURRENT_CELL", D.Current.CellMetres, (s, v) => s.CurrentCell = v),
            Flag("EVOSIM_CURRENT_ROLLS", (s, v) => s.CurrentRolls = v),
            Num("EVOSIM_CURRENT_BLINK", 0f, (s, v) => s.CurrentBlink = v),
            Flag("EVOSIM_CURRENT_ADVECT", (s, v) => s.CurrentAdvect = v),
            Custom("EVOSIM_CURRENT_MODE", (s, env) => s.CurrentMode = CurrentModeOf(env, "EVOSIM_CURRENT_MODE")),
            Num("EVOSIM_VENT", 0f, (s, v) => s.Vent = v),
            Num("EVOSIM_VENT_PATCH", 0f, (s, v) => s.VentPatch = v),
            Num("EVOSIM_VENT_DEPTH", D.WorldDepthMetres, (s, v) => s.VentDepth = v),
            Num("EVOSIM_VENT_LEG", D.LightLayerMetres, (s, v) => s.VentLeg = v),
            Num("EVOSIM_REMIN", D.RemineralisationPerSecond, (s, v) => s.Remin = v),
            Num("EVOSIM_RHO", D.JoulesPerUnit, (s, v) => s.Rho = v),
            Num("EVOSIM_UPTAKE_K", D.UptakeRatePerSquareMetre, (s, v) => s.UptakeRate = v),
            Num("EVOSIM_UPTAKE_KS", D.UptakeHalfSaturation, (s, v) => s.UptakeHalf = v),
            Num("EVOSIM_HANDLING", D.HandlingCostPerJouleEaten, (s, v) => s.Handling = v),
            Num("EVOSIM_RESERVE_CAP", D.ReserveCapSeconds, (s, v) => s.ReserveCap = v),
            Num("EVOSIM_MARGIN_MIN", RandomGenomeOptions.Default.MinReserveMargin, (s, v) => s.MarginMin = v),
            Num("EVOSIM_MARGIN_MAX", RandomGenomeOptions.Default.MaxReserveMargin, (s, v) => s.MarginMax = v),
            Num("EVOSIM_MARGIN_CHANCE", MutationRates.Default.MarginChance, (s, v) => s.MarginChance = v),
            Num("EVOSIM_FLOOR_REFUGE", D.FloorRefugeMetres, (s, v) => s.FloorRefuge = v),
            Num("EVOSIM_REFUGE_FRACTION", D.RefugeEdibleFraction, (s, v) => s.RefugeFraction = v),
            Num("EVOSIM_SATIATION", D.SatiationWattsPerCubicMetre, (s, v) => s.Satiation = v),
            Num("EVOSIM_CLEARANCE_TOE", D.ClearanceToeDensity, (s, v) => s.ClearanceToe = v),
            Num("EVOSIM_EXUDATION", D.ExudationFraction, (s, v) => s.Exudation = v),
            Custom("EVOSIM_CONCEPTION_ORDER", (s, env) => s.ConceptionOrder = ConceptionOrderOf(env, "EVOSIM_CONCEPTION_ORDER")),
            Num("EVOSIM_MATTER_INFLUX", D.MatterInfluxPerSecond, (s, v) => s.MatterInflux = v),
            Custom("EVOSIM_MATTER_INFLUX_AT", (s, env) => s.MatterInfluxAt = MatterInfluxOf(env, "EVOSIM_MATTER_INFLUX_AT")),
            Num("EVOSIM_MATTER_BURIAL", D.MatterBurialPerSecond, (s, v) => s.MatterBurial = v),
            Num("EVOSIM_DT", DefaultPhysicsStepSeconds, (s, v) => s.PhysicsDt = v),
            Num("EVOSIM_SPECIES_THETA", D.SpeciesDriftThreshold, (s, v) => s.SpeciesTheta = v),
            Num("EVOSIM_PATCHES", D.HorizontalPatches, (s, v) => s.Patches = v),
            Num("EVOSIM_PATCHES_ACROSS", D.PatchesAcross, (s, v) => s.PatchesAcross = v),
            Num("EVOSIM_H_MIXING", D.HorizontalMixingDiffusivity, (s, v) => s.HorizontalMixing = v),

            // D109: the matter grid's own stirring, on every axis. It was a hard default of 2 m²/s
            // that no launcher named, a hundred times the snow's, and at that rate a 40 m island
            // is gone in about L²/D = 800 s (scratch/r45-build/runs/bigE, the first island smoke).
            Num("EVOSIM_MATTER_MIXING", D.MatterMixingDiffusivity, (s, v) => s.MatterMixing = v),
            Num("EVOSIM_DISPERSAL", D.DispersalChancePerStep, (s, v) => s.DispersalChance = v),
            Num("EVOSIM_PATCH_SHADING", D.PerPatchShading, (s, v) => s.PatchShading = v),
            Num("EVOSIM_AREA", D.WorldAreaSquareMetres, (s, v) => s.Area = v),
            Num("EVOSIM_DEPTH", D.WorldDepthMetres, (s, v) => s.Depth = v),
            Flag("EVOSIM_SHARED_SPACE", (s, v) => s.SharedSpace = v),
            Num("EVOSIM_OFFSPRING_DISPERSAL", D.OffspringDispersalMetres, (s, v) => s.OffspringDispersal = v),
            Num("EVOSIM_SURFACE_RESTORE", D.Fluid.SurfaceRestoringFraction, (s, v) => s.SurfaceRestore = v),
            Num("EVOSIM_FLOOR_CLOSES", 0f, (s, v) => s.FloorCloses = v),

            // D115: founders a second after the floor closes. A number, or a reciprocal written
            // as 1/N, which is how round 46's launcher names one per 30 s without a rounded
            // decimal in it. 0 is the recorded world.
            Custom("EVOSIM_TRICKLE", (s, env) => s.Trickle = RateOf(env, "EVOSIM_TRICKLE")),

            // D117: the trickle's pool of evolved bodies, a semicolon-separated list of genome
            // files the farm copies into the run's pool/ and pins by hash, and the share of the
            // trickle's founders drawn from it. The farm's own, like EVOSIM_RUNS_ROOT: the Unity
            // entry reads neither, and a world built there has no pool.
            Text("EVOSIM_TRICKLE_POOL", (s, v) => s.TricklePool = v),
            Num("EVOSIM_TRICKLE_POOL_SHARE", 0f, (s, v) => s.TricklePoolShare = v),
            Int("EVOSIM_MAX_POP", D.MaximumPopulation, (s, v) => s.MaxPopulation = v),

            // double from a float read, as EvolutionRun's own cast is: the ceiling is a double on
            // RunConfig and is read here through Env(float), so a launcher naming 1e9 gets the
            // float's answer on both sides of the port.
            Num("EVOSIM_MAX_TISSUE", (float)D.MaximumTissueJoules, (s, v) => s.MaxTissue = v),

            Num("EVOSIM_SENESCENCE", 0f, (s, v) => s.Senescence = v),

            // The round 48 ruling: 0 wears upkeep alone. Unset is 1, D038 and the recorded world,
            // which is why this switch reads its fallback as on where every other flag reads off.
            FlagOn("EVOSIM_SENESCENCE_WEARS_INTAKE", (s, v) => s.SenescenceWearsIntake = v),
            Num("EVOSIM_CELLTYPE_MUTATION", MutationRates.Default.CellTypeChance, (s, v) => s.CellTypeMutation = v),
            Num("EVOSIM_CLEARANCE", 1.0f, (s, v) => s.Clearance = v),
            Num("EVOSIM_TISSUE_ENERGY", 0f, (s, v) => s.TissueEnergy = v),
            Num("EVOSIM_OVERHEAD", D.PerOffspringOverheadJoules, (s, v) => s.Overhead = v),

            // The ruling of 2026-09-24. EVOSIM_OVERHEAD is the overhead's floor and stays its
            // name, because every launcher on file sets it; EVOSIM_OVERHEAD_FLOOR is the same
            // setting under the ruling's word. Either alone sets the floor, and both set to
            // different numbers is refused rather than one quietly winning.
            Custom("EVOSIM_OVERHEAD_FLOOR", (s, env) => s.Overhead = OverheadFloorOf(env, s.Overhead)),
            Num("EVOSIM_OVERHEAD_PER_TISSUE", D.PerOffspringOverheadPerTissueJoule, (s, v) => s.OverheadPerTissue = v),

            // Reproduction paid as it goes, the same ruling. Both chances are 0 by default, and 0
            // draws nothing, so a launcher that does not name them runs the world it always ran;
            // the share range is 0.5 to 0.5, which a founder takes without a draw.
            Num("EVOSIM_GESTATION_MODE_CHANCE", MutationRates.Default.GestationModeChance, (s, v) => s.GestationModeChance = v),
            Num("EVOSIM_GESTATION_SHARE_CHANCE", MutationRates.Default.GestationShareChance, (s, v) => s.GestationShareChance = v),
            Num("EVOSIM_GESTATION_SHARE_MIN", RandomGenomeOptions.Default.MinGestationShare, (s, v) => s.GestationShareMin = v),
            Num("EVOSIM_GESTATION_SHARE_MAX", RandomGenomeOptions.Default.MaxGestationShare, (s, v) => s.GestationShareMax = v),
            Num("EVOSIM_FOUNDER_EXTENT_MIN", 0f, (s, v) => s.FounderExtentMin = v),
            Num("EVOSIM_FOUNDER_EXTENT_MAX", 0f, (s, v) => s.FounderExtentMax = v),
            Num("EVOSIM_EXCESS_DENSITY", 0f, (s, v) => s.ExcessDensity = v),
            Num("EVOSIM_ADDED_MASS", 0f, (s, v) => s.AddedMass = v),
            Num("EVOSIM_FLUID_ACCEL", 0f, (s, v) => s.FluidAccel = v),
            Num("EVOSIM_WATER_HOLD", 0f, (s, v) => s.WaterHold = v),
            Num("EVOSIM_NEURON_COST", D.NeuralCostPerNeuronWatts, (s, v) => s.NeuronCost = v),
            Num("EVOSIM_CONNECTION_COST", D.NeuralCostPerConnectionWatts, (s, v) => s.ConnectionCost = v),
            Num("EVOSIM_WORK_COST", D.WorkCostMultiplier, (s, v) => s.WorkCost = v),
            Custom("EVOSIM_FIELD", (s, env) => s.FieldModel = FieldModelOf(env, "EVOSIM_FIELD")),
            Num("EVOSIM_FIELD_KERNEL", D.FieldKernelMetres, (s, v) => s.FieldKernel = v),
            Num("EVOSIM_FIELD_MATTER_KERNEL", D.FieldMatterKernelMetres, (s, v) => s.FieldMatterKernel = v),
            Num("EVOSIM_FIELD_MERGE", D.FieldMergeMetres, (s, v) => s.FieldMerge = v),
            Int("EVOSIM_FIELD_CAP", D.FieldVertexCap, (s, v) => s.FieldCap = v),
            Num("EVOSIM_FIELD_QUANTUM", D.FieldVertexJoules, (s, v) => s.FieldQuantum = v),
            Num("EVOSIM_FIELD_CELL", D.FieldCellMetres, (s, v) => s.FieldCell = v),
            Num("EVOSIM_FIELD_MATTER_CELL", D.FieldMatterCellMetres, (s, v) => s.FieldMatterCell = v),
            Num("EVOSIM_CORPSE_DECAY", D.CorpseDecayPerSecond, (s, v) => s.CorpseDecay = v),
            Custom("EVOSIM_SHAPE", (s, env) => s.WorldShape = WorldShapeOf(env, "EVOSIM_SHAPE")),
            Num("EVOSIM_MATTER_BUDGET", D.MatterBudgetUnits, (s, v) => s.MatterBudget = v),
            Flag("EVOSIM_DRIVE_LIMIT_ALWAYS", (s, v) => s.DriveLimitAlways = v),
            Num("EVOSIM_BED_RELIEF", D.BedReliefMetres, (s, v) => s.BedRelief = v),
            Num("EVOSIM_BED_TILT", D.BedTiltMetres, (s, v) => s.BedTilt = v),
            Num("EVOSIM_BED_SCALE", D.BedScaleMetres, (s, v) => s.BedScale = v),

            // The beach (logbook/specs/beach-spec.md §2), both off by default so that a launcher
            // which does not name them runs the floor it always ran.
            Num("EVOSIM_BED_SHORE", D.BedShoreDepthMetres, (s, v) => s.BedShore = v),
            Num("EVOSIM_BED_SHORE_FADE", D.BedShoreFadeMetres, (s, v) => s.BedShoreFade = v),

            // The mushroom reefs (logbook/specs/reef-spec.md §1, redesigned 2026-09-23 night):
            // off at a cover of 0, the default, so that a launcher which does not name them runs
            // the tank it always ran. EVOSIM_REEF_COUNT, _CAP_RADIUS and _STEM_RADIUS are retired
            // with the count, and are not bound: a launcher naming them is warned and gets no reef.
            Num("EVOSIM_REEF_COVER", D.ReefCover, (s, v) => s.ReefCover = v),
            Int("EVOSIM_REEF_MAX_COUNT", D.ReefMaxCount, (s, v) => s.ReefMaxCount = v),
            Num("EVOSIM_REEF_CAP_RADIUS_MIN", D.ReefCapRadiusMinMetres, (s, v) => s.ReefCapRadiusMin = v),
            Num("EVOSIM_REEF_CAP_RADIUS_MAX", D.ReefCapRadiusMaxMetres, (s, v) => s.ReefCapRadiusMax = v),
            Num("EVOSIM_REEF_ROUGHNESS", D.ReefOutlineRoughness, (s, v) => s.ReefRoughness = v),
            Num("EVOSIM_REEF_CAP_DEPTH", D.ReefCapDepthMetres, (s, v) => s.ReefCapDepth = v),
            Num("EVOSIM_REEF_CAP_DEPTH_JITTER", D.ReefCapDepthJitterMetres, (s, v) => s.ReefCapDepthJitter = v),
            Num("EVOSIM_REEF_CAP_THICKNESS", D.ReefCapThicknessMetres, (s, v) => s.ReefCapThickness = v),
            Num("EVOSIM_REEF_STEM_FRACTION", D.ReefStemRadiusFraction, (s, v) => s.ReefStemFraction = v),
            Num("EVOSIM_REEF_FADE", D.ReefFadeMetres, (s, v) => s.ReefFade = v),
            Num("EVOSIM_NEWBORN_RESERVE", D.NewbornReserveFraction, (s, v) => s.NewbornReserve = v),
            Num("EVOSIM_GROWTH_FLOOR", D.GrowthReserveFloor, (s, v) => s.GrowthFloor = v),
            Num("EVOSIM_MIN_NEWBORN_KG", D.MinNewbornPartKilograms, (s, v) => s.MinNewbornKg = v),
            Num("EVOSIM_GROWTH_STEP", D.GrowthStepSeconds, (s, v) => s.GrowthStep = v),
            Num("EVOSIM_INVEST_MIN", RandomGenomeOptions.Default.MinBirthInvestment, (s, v) => s.InvestMin = v),
            Num("EVOSIM_INVEST_MAX", RandomGenomeOptions.Default.MaxBirthInvestment, (s, v) => s.InvestMax = v),
            Num("EVOSIM_ADULT_SCALE_CHANCE", MutationRates.Default.AdultScaleChance, (s, v) => s.AdultScaleChance = v),
            Num("EVOSIM_INVEST_CHANCE", MutationRates.Default.InvestmentChance, (s, v) => s.InvestChance = v),

            // D106 item 2's four, all off by default so that a launcher which does not name them
            // runs the world it always ran.
            Num("EVOSIM_MODULE_ADD", D.ModuleAddReserveSeconds, (s, v) => s.ModuleAdd = v),
            Num("EVOSIM_MODULE_DROP", D.ModuleDropReserveSeconds, (s, v) => s.ModuleDrop = v),
            Num("EVOSIM_MODULE_DROP_AFTER", D.ModuleDropAfterSeconds, (s, v) => s.ModuleDropAfter = v),
            Num("EVOSIM_MODULE_MUT", MutationRates.Default.ModuleGeneMutationChance, (s, v) => s.ModuleMutation = v),

            // D106 items 1, 3 and 4's ten. Every one of them is off or neutral by default, so a
            // launcher that names none of them runs the world it always ran: health is present
            // and nothing damages it, nothing heals, nothing reaches a corpse and no attribute
            // costs anything.
            Num("EVOSIM_HEALTH", D.HealthPerCubicMetre, (s, v) => s.Health = v),
            Num("EVOSIM_HEAL", D.HealingPerSecond, (s, v) => s.Healing = v),
            Num("EVOSIM_HEAL_COST", D.HealingJoulesPerHealth, (s, v) => s.HealingCost = v),
            Num("EVOSIM_INTAKE_REACH", D.IntakeReachMetres, (s, v) => s.IntakeReach = v),
            Num("EVOSIM_INTAKE_WASTE", D.IntakeWasteFraction, (s, v) => s.IntakeWaste = v),
            Num("EVOSIM_PRICE_ATTACK", D.AttackWattsPerUnit, (s, v) => s.PriceAttack = v),
            Num("EVOSIM_PRICE_INTAKE", D.IntakeWattsPerUnit, (s, v) => s.PriceIntake = v),
            Num("EVOSIM_PRICE_PROTECTION", D.ProtectionWattsPerUnit, (s, v) => s.PriceProtection = v),
            Num("EVOSIM_PRICE_TOUGHNESS", D.ToughnessWattsPerUnit, (s, v) => s.PriceToughness = v),
            Num("EVOSIM_ATTRIBUTE_MUT", MutationRates.Default.AttributeMutationChance, (s, v) => s.AttributeMutation = v),

            // D114: a contact sphere on every part rather than one a body. Off is the recorded world.
            Flag("EVOSIM_CONTACT_PER_PART", (s, v) => s.ContactPerPart = v),
            Num("EVOSIM_NEUTRAL_VOLUME", 0f, (s, v) => s.NeutralVolume = v),
            Num("EVOSIM_FOUNDER_DEPTH", D.FounderDepthSpread, (s, v) => s.FounderDepth = v),
            Num("EVOSIM_MATTER_INITIAL", 1f, (s, v) => s.InitialMatter = v),
            Num("EVOSIM_FOUNDER_FLOAT", 0f, (s, v) => s.FloatChance = v),
            Num("EVOSIM_LIFT_COST", 0.05f, (s, v) => s.LiftCost = v),
            Flag("EVOSIM_SENSE_CHEMICAL", (s, v) => s.SenseChemical = v),
            Flag("EVOSIM_SENSE_ENERGY", (s, v) => s.SenseEnergy = v),
            Flag("EVOSIM_SENSE_FLOW", (s, v) => s.SenseFlow = v),

            // D106 item 5's two, after Flow for the reason RunConfig.SensorPool appends them
            // there: the pool's order is what decides which channel a draw yields.
            Flag("EVOSIM_SENSE_CONTACT", (s, v) => s.SenseContact = v),
            Flag("EVOSIM_SENSE_DAMAGE", (s, v) => s.SenseDamage = v),
            Num("EVOSIM_CHEMICAL_HALF_SCALE", D.ChemicalHalfScaleJoulesPerCubicMetre, (s, v) => s.ChemicalHalfScale = v),
            Num("EVOSIM_ENERGY_FULL_SCALE", D.EnergyFullScaleSeconds, (s, v) => s.EnergyFullScale = v),
            Num("EVOSIM_FLOW_FULL_SCALE", D.FlowFullScaleMetresPerSecond, (s, v) => s.FlowFullScale = v),
            Text("EVOSIM_INOCULATE", (s, v) => s.InoculatePath = v),
            Num("EVOSIM_INOCULATE_AT", D.InoculateAtSeconds, (s, v) => s.InoculateAt = v),
            Int("EVOSIM_INOCULATE_COUNT", D.InoculateCount, (s, v) => s.InoculateCount = v),
            Num("EVOSIM_INOCULATE_DEPTH", D.InoculateDepthMetres, (s, v) => s.InoculateDepth = v),
            Text("EVOSIM_OUT", (s, v) => s.OutPath = v),

            // The farm's own, and the second name in this table EvolutionRun does not read: the
            // directory a report and its run directory are written under. It exists because a
            // console program can be pointed somewhere the editor entry could not — a port's
            // acceptance runs must not land in the main tree's `runs/`, which is the record — and
            // because a relative EVOSIM_OUT resolved against the process's working directory is
            // the one setting whose meaning depends on how it was launched. With EVOSIM_OUT
            // absolute this changes nothing; with it relative, or unset, this is the root.
            Text("EVOSIM_RUNS_ROOT", (s, v) => s.RunsRoot = v),

            // Read by BuildManifest rather than by RunBody, and in the table for the reason
            // everything else is: this is the whole of what the environment says to a run.
            Text("EVOSIM_REPO_ROOT", (s, v) => s.RepoRoot = v),
        };

        /// <summary>Every variable this binding reads, in the order the table states them.</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                var names = new string[Table.Length];
                for (int i = 0; i < Table.Length; i++) names[i] = Table[i].Name;
                return names;
            }
        }

        /// <summary>Reads every knob in the table. Nothing else here reads the environment.</summary>
        public static EnvSettings Read(Lookup env)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));

            var s = new EnvSettings();

            for (int i = 0; i < Table.Length; i++)
            {
                // Which names the launcher actually said, beside what they bound to. A default and
                // a value that happens to equal it are the same number and not the same fact, and
                // a resume has to tell them apart: it inherits the recording cadences of the run
                // it continues except where this launcher named one (logbook/specs/checkpoint-spec.md).
                if (env(Table[i].Name) != null) s.Provided.Add(Table[i].Name);

                Table[i].Apply(s, env);
            }

            return s;
        }

        /// <summary>Reads the process environment.</summary>
        public static EnvSettings ReadProcess() => Read(Process);

        /// <summary>
        /// The <c>EVOSIM_*</c> names that are set and are not in the table — ignored, and said
        /// aloud rather than only ignored.
        /// </summary>
        public static IReadOnlyList<string> UnknownNames(IEnumerable<string> namesSet)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Table.Length; i++) known.Add(Table[i].Name);

            var unknown = new List<string>();
            if (namesSet == null) return unknown;

            foreach (string name in namesSet)
            {
                if (name != null && name.StartsWith("EVOSIM_", StringComparison.Ordinal) &&
                    !known.Contains(name))
                {
                    unknown.Add(name);
                }
            }

            unknown.Sort(StringComparer.Ordinal);
            return unknown;
        }

        /// <summary>The <c>EVOSIM_*</c> names set in this process and not bound.</summary>
        public static IReadOnlyList<string> UnknownNamesInProcess()
        {
            var names = new List<string>();
            foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            {
                names.Add(e.Key as string);
            }

            return UnknownNames(names);
        }

        // ------------------------------------------------------------------ the config

        /// <summary>
        /// The physics step, rounded the way <c>Ecosystem.ConfigurePhysicsStep</c> rounds it.
        /// </summary>
        /// <remarks>
        /// Transcribed rather than reimplemented: <c>config.PhysicsStepSeconds</c> is read back
        /// from the configured static in <c>EvolutionRun</c>, so the hash records the step the run
        /// integrates at and not the one the launcher asked for. The metabolic step is fixed at
        /// <see cref="MetabolicStepSeconds"/> and is not a tunable in either engine.
        /// </remarks>
        public static float ResolvePhysicsStep(float dt, out int stepsPerMetabolicStep)
        {
            if (!(dt > 0f) || dt > MetabolicStepSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "Must be in (0, 0.5].");
            }

            float steps = MetabolicStepSeconds / dt;
            int rounded = (int)Math.Round(steps);
            if (rounded < 1 || Math.Abs(steps - rounded) > 1e-4f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dt), dt,
                    "Must divide the 0.5 s metabolic step exactly: 0.01, 0.02, 0.025, 0.05, 0.1, 0.125, 0.25 or 0.5.");
            }

            stepsPerMetabolicStep = rounded;
            return dt;
        }

        /// <summary>
        /// The <see cref="RunConfig"/> the settings build — <c>EvolutionRun.RunBody</c>'s
        /// assembly, in its order.
        /// </summary>
        public static RunConfig BuildConfig(EnvSettings s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            float physicsDt = ResolvePhysicsStep(s.PhysicsDt, out _);

            var config = new RunConfig
            {
                Fluid = new FluidConfig
                {
                    AddedMassCoefficient = s.AddedMass,
                    FluidAccelerationCoefficient = s.FluidAccel,
                    WaterHoldSeconds = s.WaterHold,
                    TissueExcessDensity = s.ExcessDensity,
                    NeutralBodyVolume = s.NeutralVolume,
                    SurfaceRestoringFraction = s.SurfaceRestore,
                },
                Light = new LightModel(s.Irradiance, s.LightReach)
                {
                    DayNightAmplitude = s.DayAmplitude,
                    DayLengthSeconds = s.DayLength,
                },
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(),
                    new LinkCell(
                        s.Idle,
                        photosyntheticEfficiency:
                            s.LinkPhoto * PhotosyntheticCell.DefaultEfficiency),
                    new NeuralCell(),
                    new PhotosyntheticCell(),
                    new AbsorptiveCell(s.Clearance),
                    new ConsumerCell(),
                    new BuoyancyCell(s.LiftCost)),
            };

            if (s.TissueEnergy > 0f)
            {
                for (int i = 0; i < config.CellTypes.Count; i++)
                {
                    config.CellTypes.At(i).TissueEnergyPerCubicMetre = s.TissueEnergy;
                }
            }

            config.PerOffspringOverheadJoules = s.Overhead;
            config.PerOffspringOverheadPerTissueJoule = s.OverheadPerTissue;
            config.Mutation.GestationModeChance = s.GestationModeChance;
            config.Mutation.GestationShareChance = s.GestationShareChance;
            config.Genome.MinGestationShare = Math.Min(s.GestationShareMin, s.GestationShareMax);
            config.Genome.MaxGestationShare = Math.Max(s.GestationShareMin, s.GestationShareMax);
            if (s.FounderExtentMin > 0f) config.Genome.MinHalfExtent = s.FounderExtentMin;
            if (s.FounderExtentMax > 0f) config.Genome.MaxHalfExtent = s.FounderExtentMax;

            config.Genome.MaxLinkPower = s.MaxPower;
            config.Genome.MinLinkPower = Math.Min(s.MinPower, s.MaxPower);
            config.Current.Mode = s.CurrentMode;
            config.Current.Speed = s.CurrentSpeed;
            config.Current.PeriodSeconds = s.CurrentPeriod;
            config.Current.CellMetres = s.CurrentCell;
            config.Current.Rolls = s.CurrentRolls;
            config.Current.RollBlinkSeconds = s.CurrentBlink;
            config.Current.AdvectFields = s.CurrentAdvect;
            config.Current.VentSpeed = s.Vent;
            config.Current.VentPatch = (int)s.VentPatch;
            config.Current.VentDepthMetres = s.VentDepth;
            config.Current.VentLegMetres = s.VentLeg;
            config.InitialMatterPerCubicMetre = s.InitialMatter;
            config.Genome.FounderFloatChance = s.FloatChance;
            config.FounderDepthSpread = s.FounderDepth;
            config.NutrientMixingDiffusivity = s.Mixing;
            config.NutrientSinkMetresPerSecond = s.NutrientSink;
            config.MatterSinkMetresPerSecond = s.MatterSink;
            config.RemineralisationPerSecond = s.Remin;
            config.JoulesPerUnit = s.Rho;
            config.UptakeRatePerSquareMetre = s.UptakeRate;
            config.UptakeHalfSaturation = s.UptakeHalf;
            config.HandlingCostPerJouleEaten = s.Handling;
            config.ReserveCapSeconds = s.ReserveCap;

            // Ordered here rather than trusted from the launcher, as EvolutionRun orders it: a
            // minimum above the maximum is a silent empty draw.
            config.Genome.MinReserveMargin = Math.Min(s.MarginMin, s.MarginMax);
            config.Genome.MaxReserveMargin = Math.Max(s.MarginMin, s.MarginMax);
            config.Mutation.MarginChance = s.MarginChance;
            config.FloorRefugeMetres = s.FloorRefuge;
            config.RefugeEdibleFraction = s.RefugeFraction;
            config.SatiationWattsPerCubicMetre = s.Satiation;
            config.ClearanceToeDensity = s.ClearanceToe;
            config.ExudationFraction = s.Exudation;
            config.ConceptionOrder = s.ConceptionOrder;
            config.MatterInfluxPerSecond = s.MatterInflux;
            config.MatterInfluxAt = s.MatterInfluxAt;
            config.MatterBurialPerSecond = s.MatterBurial;
            config.SpeciesDriftThreshold = s.SpeciesTheta;
            config.HorizontalPatches = s.Patches;
            config.PatchesAcross = s.PatchesAcross;
            config.HorizontalMixingDiffusivity = s.HorizontalMixing;
            config.MatterMixingDiffusivity = s.MatterMixing;
            config.DispersalChancePerStep = s.DispersalChance;
            config.PerPatchShading = s.PatchShading;
            config.LightSilhouetteCap = s.SilhouetteCap;
            config.LightByExposure = s.LightByExposure;
            config.BuoyancyOffsetWattsPerCubicMetre = s.BuoyancyOffsetCost;
            config.SupportWattsPerSquareMetrePerSquareMetre = s.Support;
            config.SelfOverlapDepthFraction = s.SelfOverlap;
            config.Development.MaxBodyReachMetres = s.MaxReach;
            config.Development.FloorsWeighRigidGroups = s.RigidFloors;

            config.MatterIslandWavelengthMetres = s.MatterIslands;
            config.MatterIslandCover = s.MatterIslandCover;
            config.MatterIslandDepthMetres = s.MatterIslandDepth;
            config.FoundersFollowMatter = s.FoundersFollowMatter;
            config.FoundersFollowFood = s.FoundersFollowFood;
            config.FoundersFollowFoodDepth = s.FoundersFollowFoodDepth;
            config.FounderEndowmentSeconds = s.FounderEndowment;
            config.LightShadeDepth = s.LightShade;
            config.LightShadeDriftMetresPerHour = s.LightShadeDrift;
            config.WorldAreaSquareMetres = s.Area;
            config.WorldDepthMetres = s.Depth;
            config.SharedSpace = s.SharedSpace;
            config.OffspringDispersalMetres = s.OffspringDispersal;
            config.PhysicsStepSeconds = physicsDt;
            config.FloorClosesAfterSeconds = s.FloorCloses;
            config.FoundingTricklePerSecond = s.Trickle;

            // D117's share only: the pool's count and hash are the files', and Program sets them
            // after reading EVOSIM_TRICKLE_POOL, before the config is written.
            config.FoundingTricklePoolShare = s.TricklePoolShare;
            config.MaximumPopulation = s.MaxPopulation;
            config.MaximumTissueJoules = s.MaxTissue;
            config.SenescenceDoublingSeconds = s.Senescence;
            config.SenescenceWearsIntake = s.SenescenceWearsIntake;
            config.Mutation.CellTypeChance = s.CellTypeMutation;
            config.SenseChemical = s.SenseChemical;
            config.SenseEnergy = s.SenseEnergy;
            config.SenseFlow = s.SenseFlow;
            config.SenseContact = s.SenseContact;
            config.SenseDamage = s.SenseDamage;
            config.ChemicalHalfScaleJoulesPerCubicMetre = s.ChemicalHalfScale;
            config.EnergyFullScaleSeconds = s.EnergyFullScale;
            config.FlowFullScaleMetresPerSecond = s.FlowFullScale;
            config.NeuralCostPerNeuronWatts = s.NeuronCost;
            config.NeuralCostPerConnectionWatts = s.ConnectionCost;
            config.WorkCostMultiplier = s.WorkCost;
            config.FieldModel = s.FieldModel;
            config.FieldKernelMetres = s.FieldKernel;
            config.FieldMatterKernelMetres = s.FieldMatterKernel;
            config.FieldMergeMetres = s.FieldMerge;
            config.FieldVertexCap = s.FieldCap;
            config.FieldVertexJoules = s.FieldQuantum;
            config.FieldCellMetres = s.FieldCell;
            config.FieldMatterCellMetres = s.FieldMatterCell;
            config.CorpseDecayPerSecond = s.CorpseDecay;
            config.WorldShape = s.WorldShape;
            config.MatterBudgetUnits = s.MatterBudget;
            config.DriveLimitAtEveryStep = s.DriveLimitAlways;

            config.BedReliefMetres = s.BedRelief;
            config.BedTiltMetres = s.BedTilt;
            config.BedScaleMetres = s.BedScale;
            config.BedShoreDepthMetres = s.BedShore;
            config.BedShoreFadeMetres = s.BedShoreFade;

            config.ReefCover = s.ReefCover;
            config.ReefMaxCount = s.ReefMaxCount;
            config.ReefCapRadiusMinMetres = s.ReefCapRadiusMin;
            config.ReefCapRadiusMaxMetres = s.ReefCapRadiusMax;
            config.ReefOutlineRoughness = s.ReefRoughness;
            config.ReefCapDepthMetres = s.ReefCapDepth;
            config.ReefCapDepthJitterMetres = s.ReefCapDepthJitter;
            config.ReefCapThicknessMetres = s.ReefCapThickness;
            config.ReefStemRadiusFraction = s.ReefStemFraction;
            config.ReefFadeMetres = s.ReefFade;

            config.NewbornReserveFraction = s.NewbornReserve;
            config.GrowthReserveFloor = s.GrowthFloor;
            config.MinNewbornPartKilograms = s.MinNewbornKg;
            config.GrowthStepSeconds = s.GrowthStep;
            config.Genome.MinBirthInvestment = Math.Min(s.InvestMin, s.InvestMax);
            config.Genome.MaxBirthInvestment = Math.Max(s.InvestMin, s.InvestMax);
            config.Mutation.AdultScaleChance = s.AdultScaleChance;
            config.Mutation.InvestmentChance = s.InvestChance;

            config.ModuleAddReserveSeconds = s.ModuleAdd;
            config.ModuleDropReserveSeconds = s.ModuleDrop;
            config.ModuleDropAfterSeconds = s.ModuleDropAfter;
            config.Mutation.ModuleGeneMutationChance = s.ModuleMutation;

            config.HealthPerCubicMetre = s.Health;
            config.HealingPerSecond = s.Healing;
            config.HealingJoulesPerHealth = s.HealingCost;
            config.IntakeReachMetres = s.IntakeReach;
            config.IntakeWasteFraction = s.IntakeWaste;
            config.AttackWattsPerUnit = s.PriceAttack;
            config.IntakeWattsPerUnit = s.PriceIntake;
            config.ProtectionWattsPerUnit = s.PriceProtection;
            config.ToughnessWattsPerUnit = s.PriceToughness;
            config.Mutation.AttributeMutationChance = s.AttributeMutation;
            config.ContactPerPart = s.ContactPerPart;

            config.InoculateAtSeconds = s.InoculateAt;
            config.InoculateCount = s.InoculateCount;
            config.InoculateDepthMetres = s.InoculateDepth;

            return config;
        }

        // ------------------------------------------------------------------ the readers
        //
        // EvolutionRun's Env, EnvULong, EnvSteps and the four word readers, transcribed. A value
        // that will not parse stops the launch in every one of them: a setting that fails to parse
        // must not become its default without anyone knowing (the Astra review, 2026-09-07).

        private static float Num(Lookup env, string name, float fallback)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return fallback;

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                return v;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is not a number. Unset it or give it a number; " +
                "a setting that fails to parse must not become its default without anyone knowing.");
        }

        /// <summary>
        /// A rate per second: a number, or <c>1/N</c> for one every N seconds — D115's reader.
        /// </summary>
        /// <remarks>
        /// The reciprocal form is <c>1f / N</c> in single precision. A decimal spelling of the same
        /// rate is the same world, and the same config hash, only when it parses to that same
        /// float, so a launcher should name one form and keep it; the header prints the rate as
        /// <c>1/N</c> either way. Anything else refuses the launch, for
        /// <see cref="Num(Lookup, string, float)"/>'s reason.
        /// </remarks>
        private static float RateOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return 0f;

            string trimmed = raw.Trim();
            int slash = trimmed.IndexOf('/');

            if (slash > 0 &&
                trimmed.Substring(0, slash).Trim() == "1" &&
                float.TryParse(
                    trimmed.Substring(slash + 1), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out float every) &&
                every > 0f && !float.IsInfinity(every))
            {
                return 1f / every;
            }

            if (slash < 0 &&
                float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                return v;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is neither a number nor 1/N for one every N " +
                "seconds. Unset it or give it one; a setting that fails to parse must not become " +
                "its default without anyone knowing.");
        }

        private static ulong ULong(Lookup env, string name, ulong fallback)
        {
            string raw = env(name);

            return !string.IsNullOrEmpty(raw) &&
                   ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v)
                ? v
                : fallback;
        }

        private static List<long> Steps(Lookup env, string name)
        {
            var steps = new List<long>();
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return steps;

            foreach (string piece in raw.Split(','))
            {
                string trimmed = piece.Trim();
                if (trimmed.Length == 0) continue;

                if (!long.TryParse(
                        trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long step))
                {
                    throw new ArgumentException(
                        name + " contains '" + trimmed + "', which is not a physics step number. " +
                        "Expected a comma-separated list of integers, e.g. 140000,140100.");
                }

                steps.Add(step);
            }

            return steps;
        }

        private static MatterField FieldModelOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return MatterField.Cells;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "cells": return MatterField.Cells;
                case "vertices": return MatterField.Vertices;
                case "grid": return MatterField.Grid;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is none of 'cells', 'vertices' or 'grid'.");
        }

        private static CurrentMode CurrentModeOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return CurrentMode.Rolls;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "rolls": return CurrentMode.Rolls;
                case "transport": return CurrentMode.Transport;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is neither 'rolls' nor 'transport'.");
        }

        private static WorldShape WorldShapeOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return WorldShape.Box;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "box": return WorldShape.Box;
                case "tank": return WorldShape.Tank;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is neither 'box' nor 'tank'.");
        }

        private static ConceptionOrder ConceptionOrderOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return ConceptionOrder.Age;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "age": return ConceptionOrder.Age;
                case "shuffled": return ConceptionOrder.Shuffled;
                case "reserve": return ConceptionOrder.Reserve;

                default:
                    throw new ArgumentException(
                        name + " is '" + raw + "', which is not a conception order. " +
                        "Known: age, shuffled, reserve.");
            }
        }

        private static MatterInflux MatterInfluxOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return MatterInflux.Surface;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "surface": return MatterInflux.Surface;
                case "vent": return MatterInflux.Vent;

                default:
                    throw new ArgumentException(
                        name + " is '" + raw + "', which is not a matter influx route. " +
                        "Known: surface, vent.");
            }
        }

        // ------------------------------------------------------------------ the table's cells

        /// <summary>One row of <see cref="Table"/>: a name and what reading it does.</summary>
        private sealed class Knob
        {
            public readonly string Name;
            private readonly Action<EnvSettings, Lookup> _apply;

            public Knob(string name, Action<EnvSettings, Lookup> apply)
            {
                Name = name;
                _apply = apply;
            }

            public void Apply(EnvSettings s, Lookup env) => _apply(s, env);
        }

        /// <summary>
        /// <c>EVOSIM_OVERHEAD_FLOOR</c>, read after <c>EVOSIM_OVERHEAD</c>: unset keeps what that
        /// said, set alone replaces it, and set beside a different <c>EVOSIM_OVERHEAD</c> is refused.
        /// </summary>
        private static float OverheadFloorOf(Lookup env, float fromOverhead)
        {
            if (string.IsNullOrEmpty(env("EVOSIM_OVERHEAD_FLOOR"))) return fromOverhead;

            float floor = Num(env, "EVOSIM_OVERHEAD_FLOOR", fromOverhead);

            if (!string.IsNullOrEmpty(env("EVOSIM_OVERHEAD")) && floor != fromOverhead)
            {
                throw new ArgumentException(
                    "EVOSIM_OVERHEAD (" + fromOverhead.ToString("R", CultureInfo.InvariantCulture) +
                    ") and EVOSIM_OVERHEAD_FLOOR (" + floor.ToString("R", CultureInfo.InvariantCulture) +
                    ") name the same setting, the overhead's floor, and disagree. Set one.");
            }

            return floor;
        }

        private static Knob Num(string name, float fallback, Action<EnvSettings, float> set) =>
            new Knob(name, (s, env) => set(s, Num(env, name, fallback)));

        private static Knob Int(string name, float fallback, Action<EnvSettings, int> set) =>
            new Knob(name, (s, env) => set(s, (int)Num(env, name, fallback)));

        private static Knob Long(string name, float fallback, Action<EnvSettings, long> set) =>
            new Knob(name, (s, env) => set(s, (long)Num(env, name, fallback)));

        /// <summary>A switch: <c>Env(name, 0f) &gt; 0.5f</c>, which is how EvolutionRun spells one.</summary>
        private static Knob Flag(string name, Action<EnvSettings, bool> set) =>
            new Knob(name, (s, env) => set(s, Num(env, name, 0f) > 0.5f));

        /// <summary>
        /// A switch whose unset value is on: <c>Env(name, 1f) &gt; 0.5f</c>. For a knob whose
        /// recorded world is the true side (<c>EVOSIM_SENESCENCE_WEARS_INTAKE</c>).
        /// </summary>
        private static Knob FlagOn(string name, Action<EnvSettings, bool> set) =>
            new Knob(name, (s, env) => set(s, Num(env, name, 1f) > 0.5f));

        private static Knob Text(string name, Action<EnvSettings, string> set) =>
            new Knob(name, (s, env) => set(s, env(name)));

        private static Knob Custom(string name, Action<EnvSettings, Lookup> apply) =>
            new Knob(name, apply);
    }

    /// <summary>
    /// What the environment said, before any of it becomes a <see cref="RunConfig"/>.
    /// </summary>
    /// <remarks>
    /// One field per variable, named as <c>EvolutionRun</c>'s local is, because half of these are
    /// not config at all: the budget, the wall, the report cadence, the digest, the thread count
    /// and the inoculum's path are run settings that must never reach <c>config.json</c> or its
    /// hash. Keeping them in one object beside the tunables is what lets the manifest and the
    /// header name them without <see cref="RunConfig"/> growing a field that is not a setting.
    /// </remarks>
    public sealed class EnvSettings
    {
        /// <summary>
        /// The <c>EVOSIM_*</c> names this launcher actually set, whatever they bound to.
        /// </summary>
        /// <remarks>
        /// Only a resume reads it, and only for the recording cadences: everything else is either
        /// in the config and hashed, or is a pace setting nobody inherits.
        /// </remarks>
        public readonly HashSet<string> Provided = new HashSet<string>(StringComparer.Ordinal);

        public float Irradiance;
        public float LightReach;
        public bool SilhouetteCap;
        public bool LightByExposure;
        public float BuoyancyOffsetCost;
        public float Support;
        public float SelfOverlap;
        public float MaxReach;
        public bool RigidFloors;
        public float MatterIslands;
        public float MatterIslandCover;
        public float MatterIslandDepth;
        public bool FoundersFollowMatter;
        public bool FoundersFollowFood;

        /// <summary>The round 48 founding ruling's depth — <c>EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH</c>.</summary>
        public bool FoundersFollowFoodDepth;

        /// <summary>The round 48 founding ruling's endowment, s — <c>EVOSIM_FOUNDER_ENDOWMENT</c>.</summary>
        public float FounderEndowment;
        public float LightShade;
        public float LightShadeDrift;
        public float BudgetSeconds;
        public float WallMinutes;
        public int ReportEvery;
        public ulong Seed;
        public long DigestEvery;
        public List<long> DigestDumpSteps = new List<long>();

        /// <summary>Unity's job workers (D078): read, recorded by neither, applied to nothing here.</summary>
        public int RequestedJobWorkers;

        /// <summary>The farm's thread count. 0 is one per processor; it is not in the hash.</summary>
        public int Threads;

        /// <summary>
        /// How often the state stream takes a frame, simulated seconds. 0 writes no stream.
        /// </summary>
        public float PoseEvery;

        /// <summary>
        /// How often the whole world is written to <c>checkpoints/</c>, simulated seconds. 0
        /// writes none.
        /// </summary>
        public float CheckpointEvery;

        /// <summary>
        /// Where to start from: a <c>.ckpt</c> file, a run directory, or an arm directory. Null
        /// founds a world in the ordinary way.
        /// </summary>
        public string ResumeFrom;

        /// <summary>Which second to start from. 0 takes the last checkpoint there is.</summary>
        public float ResumeAt;

        /// <summary>
        /// Whether to carry on from a checkpoint this build did not write.
        /// </summary>
        /// <remarks>
        /// Off by default and recorded in the manifest when it is on, because what comes out of a
        /// checkpoint read by another build is a cousin of the recording rather than its
        /// continuation — the theatre's own word for the same thing, and the same rule its
        /// <c>Allow Source Mismatch</c> follows.
        /// </remarks>
        public bool AllowSourceMismatch;

        public float Idle;
        public float MaxPower;
        public float MinPower;
        public float LinkPhoto;
        public float DayAmplitude;
        public float DayLength;
        public float CurrentSpeed;
        public float Mixing;
        public float NutrientSink;
        public float MatterSink;
        public float CurrentPeriod;
        public float CurrentCell;
        public bool CurrentRolls;
        public float CurrentBlink;
        public bool CurrentAdvect;
        public CurrentMode CurrentMode;
        public float Vent;
        public float VentPatch;
        public float VentDepth;
        public float VentLeg;
        public float Remin;
        public float Rho;
        public float UptakeRate;
        public float UptakeHalf;
        public float Handling;
        public float ReserveCap;
        public float MarginMin;
        public float MarginMax;
        public float MarginChance;
        public float FloorRefuge;
        public float RefugeFraction;
        public float Satiation;
        public float ClearanceToe;
        public float Exudation;
        public ConceptionOrder ConceptionOrder;
        public float MatterInflux;
        public MatterInflux MatterInfluxAt;
        public float MatterBurial;
        public float PhysicsDt;
        public float SpeciesTheta;
        public float Patches;
        public float PatchesAcross;
        public float HorizontalMixing;
        public float MatterMixing;
        public float DispersalChance;
        public float PatchShading;
        public float Area;
        public float Depth;
        public bool SharedSpace;
        public float OffspringDispersal;
        public float SurfaceRestore;
        public float FloorCloses;
        public float Trickle;

        /// <summary>D117's pool: genome files, semicolon-separated — <c>EVOSIM_TRICKLE_POOL</c>.</summary>
        public string TricklePool;

        /// <summary>D117's share of the trickle drawn from the pool — <c>EVOSIM_TRICKLE_POOL_SHARE</c>.</summary>
        public float TricklePoolShare;
        public int MaxPopulation;
        public double MaxTissue;
        public float Senescence;

        /// <summary>Whether senescence divides intake too — <c>EVOSIM_SENESCENCE_WEARS_INTAKE</c>, on when unset.</summary>
        public bool SenescenceWearsIntake;
        public float CellTypeMutation;
        public float Clearance;
        public float TissueEnergy;
        public float Overhead;
        public float OverheadPerTissue;
        public float GestationModeChance;
        public float GestationShareChance;
        public float GestationShareMin;
        public float GestationShareMax;
        public float FounderExtentMin;
        public float FounderExtentMax;
        public float ExcessDensity;
        public float AddedMass;
        public float FluidAccel;
        public float WaterHold;
        public float NeuronCost;
        public float ConnectionCost;
        public float WorkCost;
        public MatterField FieldModel;
        public float FieldKernel;
        public float FieldMatterKernel;
        public float FieldMerge;
        public int FieldCap;
        public float FieldQuantum;
        public float FieldCell;
        public float FieldMatterCell;
        public float CorpseDecay;
        public WorldShape WorldShape;
        public float MatterBudget;
        public bool DriveLimitAlways;
        public float BedRelief;
        public float BedTilt;
        public float BedScale;
        public float BedShore;
        public float BedShoreFade;
        public float ReefCover;
        public int ReefMaxCount;
        public float ReefCapRadiusMin;
        public float ReefCapRadiusMax;
        public float ReefRoughness;
        public float ReefCapDepth;
        public float ReefCapDepthJitter;
        public float ReefCapThickness;
        public float ReefStemFraction;
        public float ReefFade;

        public float NewbornReserve;
        public float GrowthFloor;
        public float MinNewbornKg;
        public float GrowthStep;
        public float InvestMin;
        public float InvestMax;
        public float AdultScaleChance;
        public float InvestChance;
        public float ModuleAdd;
        public float ModuleDrop;
        public float ModuleDropAfter;
        public float ModuleMutation;
        public float Health;
        public float Healing;
        public float HealingCost;
        public float IntakeReach;
        public float IntakeWaste;
        public float PriceAttack;
        public float PriceIntake;
        public float PriceProtection;
        public float PriceToughness;
        public float AttributeMutation;
        public bool ContactPerPart;
        public float NeutralVolume;
        public float FounderDepth;
        public float InitialMatter;
        public float FloatChance;
        public float LiftCost;
        public bool SenseChemical;
        public bool SenseEnergy;
        public bool SenseFlow;
        public bool SenseContact;
        public bool SenseDamage;
        public float ChemicalHalfScale;
        public float EnergyFullScale;
        public float FlowFullScale;
        public string InoculatePath;
        public float InoculateAt;
        public int InoculateCount;
        public float InoculateDepth;
        public string OutPath;
        public string RepoRoot;

        /// <summary>The directory a report and its run directory land under — <c>EVOSIM_RUNS_ROOT</c>.</summary>
        public string RunsRoot;

        /// <summary>
        /// Where the report goes: <c>EVOSIM_OUT</c>, or <c>../runs/evolution.md</c> from the
        /// working directory, which is what <c>EvolutionRun</c> falls back to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The fallback is relative to the process's own directory, and a worker project's
        /// directory is one below the repository — which is why <c>run-arm.ps1</c> always sets the
        /// variable. Kept as it was so a launcher that relies on the fallback lands where it
        /// always did.
        /// </para>
        /// <para>
        /// <b><see cref="RunsRoot"/> is resolved against, never appended to.</b> An absolute
        /// <c>EVOSIM_OUT</c> wins outright, so every launcher on file lands exactly where it
        /// always did; a relative one, or none at all, resolves under the root. That is what lets
        /// an acceptance run be pointed at a scratch directory without a second way of spelling
        /// the arm's name.
        /// </para>
        /// </remarks>
        public string ResolveOutPath()
        {
            string root = string.IsNullOrEmpty(RunsRoot)
                ? null
                : System.IO.Path.GetFullPath(RunsRoot);

            if (!string.IsNullOrEmpty(OutPath))
            {
                return root != null && !System.IO.Path.IsPathRooted(OutPath)
                    ? System.IO.Path.GetFullPath(System.IO.Path.Combine(root, OutPath))
                    : OutPath;
            }

            if (root != null)
            {
                return System.IO.Path.Combine(root, "evolution.md");
            }

            return System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "..", "runs", "evolution.md"));
        }

        /// <summary>The thread count this run will use: <c>EVOSIM_THREADS</c>, or one per processor.</summary>
        public int ResolveThreads() =>
            Threads > 0 ? Threads : Environment.ProcessorCount;

        /// <summary>
        /// The cadence the state stream will actually record at: 0 when it is off, and never
        /// below the metabolic step.
        /// </summary>
        /// <remarks>
        /// A cadence under half a second is raised rather than refused, because nothing about a
        /// body changes between metabolic steps and a launcher that asks for a tenth of a second
        /// is asking for every step. The number recorded in the manifest and in the stream's own
        /// header is this one, so a reader is never told a cadence the file does not have.
        /// </remarks>
        public float ResolvePoseEvery() =>
            PoseEvery > 0f ? Math.Max(PoseEvery, MetabolicStep) : 0f;

        /// <summary>
        /// The cadence checkpoints will actually be written at: 0 when they are off, and never
        /// below the metabolic step.
        /// </summary>
        /// <remarks>
        /// Raised rather than refused for <see cref="ResolvePoseEvery"/>'s reason — nothing about
        /// the world changes between metabolic steps — and the number recorded in the manifest is
        /// this one, so a reader is never told a cadence the directory does not have.
        /// </remarks>
        public float ResolveCheckpointEvery() =>
            CheckpointEvery > 0f ? Math.Max(CheckpointEvery, MetabolicStep) : 0f;

        private const float MetabolicStep = EnvBinding.MetabolicStepSeconds;
    }
}
