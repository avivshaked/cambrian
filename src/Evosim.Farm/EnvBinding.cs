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

            // D127: how much of a body's first link holds no energy, m3 of the adult part.
            Num("EVOSIM_FREE_LINK", D.FreeFirstLinkCubicMetres, (s, v) => s.FreeLink = v),
            Num("EVOSIM_SELF_OVERLAP", 0f, (s, v) => s.SelfOverlap = v),
            Num("EVOSIM_MAX_REACH", 0f, (s, v) => s.MaxReach = v),

            // The owner's ruling of 2026-09-24 (buds): both floors weigh the rigid body a welded
            // part is carried in. Off is the recorded world.
            Flag("EVOSIM_RIGID_FLOORS", (s, v) => s.RigidFloors = v),

            // D199: an edge's scale lengthens once on a path and still shortens at every copy.
            // Off is the recorded world, where every copy compounds.
            Flag("EVOSIM_SCALE_LENGTHENS_ONCE", (s, v) => s.ScaleLengthensOnce = v),

            // D228: a mirrored part's joint anchors are stored in its proper frame, so a reflected
            // copy is posed where development put it. Off is the recorded world, where the solver
            // joins a mirrored part where its twin is joined.
            Flag("EVOSIM_REFLECT_MIRRORED_ANCHORS", (s, v) => s.ReflectMirroredAnchors = v),

            // D241: a scaled body's joint torque scales with the cube of its length, as muscle does
            // (force with cross-section, moment arm with length), at birth, growth and rebuild. Off is the
            // recorded world, where a juvenile has its adult's torque on smaller limbs.
            Flag("EVOSIM_JUVENILE_STRENGTH", (s, v) => s.JuvenileStrength = v),

            // D233: the shapes the world allows, comma-separated ids in their order ("box,sphere,
            // capsule,superellipsoid"). Unset is box, sphere and capsule, every recorded world. An id
            // the catalogue does not know, or one named twice, is refused here.
            Custom("EVOSIM_SHAPES", (s, env) => s.ShapeIds = ShapeIdsOf(env, "EVOSIM_SHAPES")),

            // D109: the matter seeded as islands, founders planted in them, and the light's shade
            // map from the same noise, drifting or not. Every default is the recorded world.
            Num("EVOSIM_MATTER_ISLANDS", 0f, (s, v) => s.MatterIslands = v),
            Num("EVOSIM_MATTER_ISLAND_COVER", D.MatterIslandCover, (s, v) => s.MatterIslandCover = v),
            Num("EVOSIM_MATTER_ISLAND_DEPTH", D.MatterIslandDepthMetres, (s, v) => s.MatterIslandDepth = v),
            // D185: no island column within this many metres of the glass; 0 is the recorded world.
            Num("EVOSIM_MATTER_ISLAND_MARGIN", D.MatterIslandGlassMarginMetres, (s, v) => s.MatterIslandMargin = v),
            // D192: named seeds off the world's seed, comma-separated name=value ("landscape=42");
            // unset is none, every recorded world. RunConfig refuses an unknown name or a bad value.
            Custom("EVOSIM_SEED_OVERRIDES", (s, env) => s.SeedOverrides = SeedOverridesOf(env, "EVOSIM_SEED_OVERRIDES")),
            Flag("EVOSIM_FOUNDERS_FOLLOW_MATTER", (s, v) => s.FoundersFollowMatter = v),

            // D116: the founder rule reads the field the founder's body eats. Off is the recorded
            // world, and the world refuses it beside D109's.
            Flag("EVOSIM_FOUNDERS_FOLLOW_FOOD", (s, v) => s.FoundersFollowFood = v),

            // The round 48 founding ruling (owner, 2026-09-24): a founder accepted in a column is
            // set at the richest cell of its food there, and every founder is born holding that
            // many seconds of its own standing cost. Off and 0 are the recorded world; the depth
            // rule is refused without D116's.
            Flag("EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH", (s, v) => s.FoundersFollowFoodDepth = v),

            // The round 50 founding ruling: a leaf founder set where its own income, light and
            // food together, is largest, in place of the depth rule above for a leaf. Off is the
            // recorded world; refused without the depth rule.
            Flag("EVOSIM_FOUNDERS_INCOME_DEPTH", (s, v) => s.FoundersFollowIncomeDepth = v),
            // D129: a founder with a mouth reads the corpses as its food in the lottery and, under
            // the depth rule, is set just above them. Off is the recorded world; refused without
            // D116, without corpses as objects and without a mouth's reach.
            Flag("EVOSIM_FOUNDERS_FOLLOW_CORPSES", (s, v) => s.FoundersFollowCorpses = v),
            Num("EVOSIM_FOUNDER_ENDOWMENT", 0f, (s, v) => s.FounderEndowment = v),

            // D124, the owner's ruling for round 49 (2026-09-25): a founder starts with at most
            // this fraction of its own breeding gate, purse and endowment together, after its
            // growth. 0 is the recorded world.
            Num("EVOSIM_FOUNDER_RESERVE_CAP", 0f, (s, v) => s.FounderReserveCap = v),
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

            // D159's exchange between the worlds of one --group, round 58: every this many
            // simulated seconds each world sends EVOSIM_MIGRATE_COUNT copies of living genomes to
            // the next world in the group's order. Farm settings and not tunables, by the
            // coordinator's call of 2026-10-09: they say how worlds are joined and nothing about
            // what a world is, so they reach EnvSettings and run.json and never RunConfig or its
            // hash. 0 is no exchange, and a lone run refuses anything else.
            Num("EVOSIM_MIGRATE_EVERY", 0f, (s, v) => s.MigrateEvery = v),
            Int("EVOSIM_MIGRATE_COUNT", 1f, (s, v) => s.MigrateCount = v),

            // A test hook, not a setting of the run (card-next-r57.md Part 8): a pause in the middle
            // of every checkpoint's file, so a test can kill the farm while a write is half done. It
            // moves when the bytes land and never which bytes they are, and it is bounded, recorded
            // in run.json and the header, and refused by the round launchers.
            Custom("EVOSIM_TEST_CHECKPOINT_STALL_MS", (s, env) => s.TestCheckpointStallMs = StallMs(env)),

            // Which record the run writes (logbook/specs/record-and-film-spec.md, Part A): 2, the
            // default from round 49's build, writes each genome once, slim snapshots, gzipped
            // positions, the state stream in place of poses.jsonl and compressed checkpoints; 1
            // writes the record every earlier run wrote. A recording setting like the cadences
            // above it: EnvSettings and run.json's recordFormat, never RunConfig or its hash. A
            // resume inherits its source's, as it inherits the cadences, unless this names one.
            Custom("EVOSIM_RECORD_FORMAT", (s, env) => s.RecordFormat = RecordFormatOf(env)),

            Text("EVOSIM_RESUME", (s, v) => s.ResumeFrom = v),
            Num("EVOSIM_RESUME_AT", 0f, (s, v) => s.ResumeAt = v),
            Flag("EVOSIM_ALLOW_SOURCE_MISMATCH", (s, v) => s.AllowSourceMismatch = v),
            Text("EVOSIM_RESUME_CONFIG", (s, v) => s.ResumeConfig = v),
            Flag("EVOSIM_RESUME_TRICKLE_OFF", (s, v) => s.ResumeTrickleOff = v),

            // Which engine steps the bodies, and how the gpu one runs (logbook/specs/
            // gpu-port-spec.md). Farm settings like the thread count, never tunables: an engine is
            // recorded in run.json and the header, and a run's config and its hash are the world's
            // whatever steps it. The gpu names are read on every build and applied only by one
            // compiled with the engine (EVOSIM_GPU); the Unity farm reads none of them.
            Text("EVOSIM_ENGINE", (s, v) => s.Engine = string.IsNullOrEmpty(v) ? "cpu" : v.Trim().ToLowerInvariant()),
            Text("EVOSIM_GPU_DEVICE", (s, v) => s.GpuDevice = string.IsNullOrEmpty(v) ? "cuda" : v.Trim().ToLowerInvariant()),
            Text("EVOSIM_GPU_PRECISION", (s, v) => s.GpuPrecision = string.IsNullOrEmpty(v) ? "single" : v.Trim().ToLowerInvariant()),
            Int("EVOSIM_GPU_GROUP", 32f, (s, v) => s.GpuGroup = v),
            Text("EVOSIM_GPU_MEAN", (s, v) => s.GpuMean = string.IsNullOrEmpty(v) ? "" : v.Trim().ToLowerInvariant()),
            Flag("EVOSIM_GPU_RESYNC", (s, v) => s.GpuResync = v),
            Flag("EVOSIM_GPU_CONCURRENT", (s, v) => s.GpuConcurrent = v),
            Flag("EVOSIM_GPU_TRANSPORT", (s, v) => s.GpuTransport = v),
            Flag("EVOSIM_GPU_SCENT", (s, v) => s.GpuScent = v),

            // The pace guard and the cap watch (proposals/propose-farm-pace-guard.md): farm settings that read
            // the wall clock and the counts the run keeps, and move no bit. On unless a launcher says 0. The
            // ratio's default waits on the owner's ruling. The trip is a test's, as the stall hook is.
            FlagOn("EVOSIM_PACE_GUARD", (s, v) => s.PaceGuard = v),
            Num("EVOSIM_PACE_RATIO", 0.5f, (s, v) => s.PaceRatio = v),
            Int("EVOSIM_PACE_ROWS", 2f, (s, v) => s.PaceRows = v),
            Int("EVOSIM_PACE_PROBE_ROWS", 1f, (s, v) => s.PaceProbeRows = v),
            Num("EVOSIM_PACE_WINDOW_MINUTES", 10f, (s, v) => s.PaceWindowMinutes = v),
            Num("EVOSIM_PACE_HOUR_MINUTES", 60f, (s, v) => s.PaceHourMinutes = v),
            Num("EVOSIM_PACE_WARMUP_MINUTES", 10f, (s, v) => s.PaceWarmupMinutes = v),
            Num("EVOSIM_TEST_PACE_TRIP_AT", -1f, (s, v) => s.TestPaceTripAt = v),
            FlagOn("EVOSIM_OVERFLOW_CHECKPOINT", (s, v) => s.OverflowCheckpoint = v),

            Num("EVOSIM_IDLE", 0.02f, (s, v) => s.Idle = v),
            Num("EVOSIM_MAXPOWER", RandomGenomeOptions.Default.MaxLinkPower, (s, v) => s.MaxPower = v),
            Num("EVOSIM_MINPOWER", RandomGenomeOptions.Default.MinLinkPower, (s, v) => s.MinPower = v),
            Num("EVOSIM_LINK_PHOTO", 0f, (s, v) => s.LinkPhoto = v),
            Num("EVOSIM_DAY_AMPLITUDE", 0f, (s, v) => s.DayAmplitude = v),
            Num("EVOSIM_DAY_LENGTH", 200f, (s, v) => s.DayLength = v),
            Num("EVOSIM_SEASON_AMPLITUDE", 0f, (s, v) => s.SeasonAmplitude = v),
            Num("EVOSIM_SEASON_LENGTH", 30000f, (s, v) => s.SeasonLength = v),

            // D151, round 57's light: the season's shape and its ramp, and the weather's spread,
            // day-to-day correlation and seed. Every default is the recorded world (smooth, no
            // ramp, no weather); the seed's default is LightModel's own.
            Custom("EVOSIM_SEASON_SHAPE", (s, env) => s.SeasonShape = SeasonShapeOf(env, "EVOSIM_SEASON_SHAPE")),
            Num("EVOSIM_SEASON_RAMP", 0f, (s, v) => s.SeasonRamp = v),
            Num("EVOSIM_WEATHER_SPREAD", 0f, (s, v) => s.WeatherSpread = v),
            Num("EVOSIM_WEATHER_CORRELATION", 0f, (s, v) => s.WeatherCorrelation = v),
            Custom("EVOSIM_WEATHER_SEED", (s, env) => s.WeatherSeed = StrictULong(env, "EVOSIM_WEATHER_SEED", 1UL)),
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
            // D229, D247: the rule the cap is by. Seconds is D098's trim, every recorded world.
            Custom("EVOSIM_RESERVE_CAP_FORM", (s, env) => s.ReserveCapForm = ReserveCapFormOf(env, "EVOSIM_RESERVE_CAP_FORM")),
            Num("EVOSIM_MARGIN_MIN", RandomGenomeOptions.Default.MinReserveMargin, (s, v) => s.MarginMin = v),
            Num("EVOSIM_MARGIN_MAX", RandomGenomeOptions.Default.MaxReserveMargin, (s, v) => s.MarginMax = v),
            Num("EVOSIM_MARGIN_CHANCE", MutationRates.Default.MarginChance, (s, v) => s.MarginChance = v),
            // The structural mutation chances: the operators that add or remove a node, an edge or a
            // neuron, or step a node's recursive limit. Unset, each is MutationRates' own default.
            Num("EVOSIM_ADD_NODE_CHANCE", MutationRates.Default.AddNodeChance, (s, v) => s.AddNodeChance = v),
            Num("EVOSIM_ADD_EDGE_CHANCE", MutationRates.Default.AddEdgeChance, (s, v) => s.AddEdgeChance = v),
            Num("EVOSIM_REMOVE_EDGE_CHANCE", MutationRates.Default.RemoveEdgeChance, (s, v) => s.RemoveEdgeChance = v),
            Num("EVOSIM_ADD_NEURON_CHANCE", MutationRates.Default.AddNeuronChance, (s, v) => s.AddNeuronChance = v),
            Num("EVOSIM_REMOVE_NEURON_CHANCE", MutationRates.Default.RemoveNeuronChance, (s, v) => s.RemoveNeuronChance = v),
            Num("EVOSIM_RECURSIVE_LIMIT_CHANCE", MutationRates.Default.RecursiveLimitChance, (s, v) => s.RecursiveLimitChance = v),
            Num("EVOSIM_JOINT_TYPE_CHANCE", MutationRates.Default.JointTypeChance, (s, v) => s.JointTypeChance = v),
            Num("EVOSIM_FLOOR_REFUGE", D.FloorRefugeMetres, (s, v) => s.FloorRefuge = v),
            Num("EVOSIM_REFUGE_FRACTION", D.RefugeEdibleFraction, (s, v) => s.RefugeFraction = v),
            Num("EVOSIM_SATIATION", D.SatiationWattsPerCubicMetre, (s, v) => s.Satiation = v),
            Num("EVOSIM_CLEARANCE_TOE", D.ClearanceToeDensity, (s, v) => s.ClearanceToe = v),
            Num("EVOSIM_EXUDATION", D.ExudationFraction, (s, v) => s.Exudation = v),
            Custom("EVOSIM_CONCEPTION_ORDER", (s, env) => s.ConceptionOrder = ConceptionOrderOf(env, "EVOSIM_CONCEPTION_ORDER")),
            Num("EVOSIM_MATTER_INFLUX", D.MatterInfluxPerSecond, (s, v) => s.MatterInflux = v),
            Custom("EVOSIM_MATTER_INFLUX_AT", (s, env) => s.MatterInfluxAt = MatterInfluxOf(env, "EVOSIM_MATTER_INFLUX_AT")),
            Num("EVOSIM_MATTER_BURIAL", D.MatterBurialPerSecond, (s, v) => s.MatterBurial = v),
            // D190: the desert's own burial, outside the islands' influx; below 0 is the recorded world.
            Num("EVOSIM_MATTER_BURIAL_DESERT", D.MatterBurialDesertPerSecond, (s, v) => s.MatterBurialDesert = v),
            Num("EVOSIM_DT", DefaultPhysicsStepSeconds, (s, v) => s.PhysicsDt = v),

            // D239: how many seconds of physics one world second holds. 1 is the recorded world;
            // RunConfig refuses a value below 1 or not finite, and ResolvePhysicsStep refuses a dt
            // that does not divide 0.5 s times it.
            Num("EVOSIM_PACE_FACTOR", D.PaceFactor, (s, v) => s.PaceFactor = v),
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
            // files the farm copies into the run's pool/ and pins by hash (or "@" and a weighted
            // list file, which round 55's few hundred paths need: one variable stays well under
            // Windows' environment limit), and the share of the trickle's founders drawn from it.
            // The farm's own, like EVOSIM_RUNS_ROOT: the Unity
            // entry reads neither, and a world built there has no pool.
            Text("EVOSIM_TRICKLE_POOL", (s, v) => s.TricklePool = v),
            Num("EVOSIM_TRICKLE_POOL_SHARE", 0f, (s, v) => s.TricklePoolShare = v),

            // The owner's ruling of 2026-10-04: the share of the floor's founders drawn from the
            // same pool. 0, unset, is the recorded world.
            Num("EVOSIM_FLOOR_POOL_SHARE", 0f, (s, v) => s.FloorPoolShare = v),
            Int("EVOSIM_MAX_POP", D.MaximumPopulation, (s, v) => s.MaxPopulation = v),

            // double from a float read, as EvolutionRun's own cast is: the ceiling is a double on
            // RunConfig and is read here through Env(float), so a launcher naming 1e9 gets the
            // float's answer on both sides of the port.
            Num("EVOSIM_MAX_TISSUE", (float)D.MaximumTissueJoules, (s, v) => s.MaxTissue = v),

            Num("EVOSIM_SENESCENCE", 0f, (s, v) => s.Senescence = v),

            // The round 48 ruling: 0 wears upkeep alone. Unset is 1, D038 and the recorded world,
            // which is why this switch reads its fallback as on where every other flag reads off.
            FlagOn("EVOSIM_SENESCENCE_WEARS_INTAKE", (s, v) => s.SenescenceWearsIntake = v),

            // D128: death by age, a Gompertz hazard with a median at the base; 0 is off and the
            // recorded world. The pushes and the repair cost are refused without it.
            Num("EVOSIM_AGE_BASE", D.AgeHazardBaseSeconds, (s, v) => s.AgeBase = v),
            Num("EVOSIM_AGE_DOUBLING", D.AgeHazardDoublingFraction, (s, v) => s.AgeDoubling = v),
            Num("EVOSIM_AGE_BIRTH", D.AgeingPerBirth, (s, v) => s.AgeBirth = v),
            Num("EVOSIM_AGE_WOUND", D.AgeingPerWound, (s, v) => s.AgeWound = v),
            Num("EVOSIM_AGE_REPAIR", D.AgeRepairExponent, (s, v) => s.AgeRepair = v),
            Num("EVOSIM_LIFESPAN_CHANCE", MutationRates.Default.LifespanChance, (s, v) => s.LifespanChance = v),
            Num("EVOSIM_LIFESPAN_MIN", RandomGenomeOptions.Default.MinLifespan, (s, v) => s.LifespanMin = v),
            Num("EVOSIM_LIFESPAN_MAX", RandomGenomeOptions.Default.MaxLifespan, (s, v) => s.LifespanMax = v),
            Num("EVOSIM_CELLTYPE_MUTATION", MutationRates.Default.CellTypeChance, (s, v) => s.CellTypeMutation = v),
            Num("EVOSIM_CLEARANCE", 1.0f, (s, v) => s.Clearance = v),
            Num("EVOSIM_TISSUE_ENERGY", 0f, (s, v) => s.TissueEnergy = v),
            // The owner's leaf energy (2026-10-19): the photosynthetic cell's tissue energy, times this. 1 is the
            // recorded table.
            Num("EVOSIM_LEAF_TISSUE_ENERGY", 1f, (s, v) => s.LeafTissueEnergy = v),
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
            Num("EVOSIM_LIMB_PUSH", 0f, (s, v) => s.LimbPush = v),
            // D229: a stroking limb's added mass follows its shape. Read only with the push on.
            Flag("EVOSIM_SHAPED_ADDED_MASS", (s, v) => s.ShapedAddedMass = v),
            // D231: a stroke pushes the parts welded beyond its jointed link. Needs the fin law and the power push.
            Flag("EVOSIM_STROKE_ASSEMBLY", (s, v) => s.StrokeAssembly = v),
            Num("EVOSIM_POWER_PUSH", 0f, (s, v) => s.PowerPush = v),
            Num("EVOSIM_POSE_HZ", 0f, (s, v) => s.PoseHz = v),
            Flag("EVOSIM_DRIVE_GENES", (s, v) => s.DriveGenes = v),
            Num("EVOSIM_DRIVE_GENE_CHANCE", 0f, (s, v) => s.DriveGeneChance = v),
            Num("EVOSIM_WATER_HOLD", 0f, (s, v) => s.WaterHold = v),
            Custom("EVOSIM_TURNING_DRAG", (s, env) => s.TurningDrag = TurningDragOf(env, "EVOSIM_TURNING_DRAG")),
            Num("EVOSIM_SKIN_FRICTION", 0f, (s, v) => s.SkinFriction = v),
            Num("EVOSIM_SPIN_DRAG", 0f, (s, v) => s.SpinDrag = v),
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

            // D127's corpse: its own sink rate (0 is the snow's), whether it settles, and the share
            // of its first joules below which it gives the rest and leaves (0 is the old floor).
            Num("EVOSIM_CORPSE_SINK", D.CorpseSinkMetresPerSecond, (s, v) => s.CorpseSink = v),
            Flag("EVOSIM_CORPSE_SETTLE", (s, v) => s.CorpseSettle = v),
            Num("EVOSIM_CORPSE_FLOOR", D.CorpseLeavesBelowFraction, (s, v) => s.CorpseFloor = v),
            Num("EVOSIM_CORPSE_DECAY_SETTLED", D.CorpseSettledDecayPerSecond, (s, v) => s.CorpseDecaySettled = v),
            // D212: the half-life of a corpse's food value, seconds. 0 is off, every recorded world.
            Num("EVOSIM_CORPSE_HALF_LIFE", D.CorpseHalfLifeSeconds, (s, v) => s.CorpseHalfLife = v),
            Custom("EVOSIM_SHAPE", (s, env) => s.WorldShape = WorldShapeOf(env, "EVOSIM_SHAPE")),
            Num("EVOSIM_MATTER_BUDGET", D.MatterBudgetUnits, (s, v) => s.MatterBudget = v),
            Flag("EVOSIM_DRIVE_LIMIT_ALWAYS", (s, v) => s.DriveLimitAlways = v),

            // D199: the drag limiter's law by the welded body, and its gate opened at 0.01.
            Flag("EVOSIM_DRAG_LIMIT_WELDED", (s, v) => s.DragLimitWelded = v),
            Flag("EVOSIM_DRAG_LIMIT_ALWAYS", (s, v) => s.DragLimitAlways = v),
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
            // D194's coral (logbook/specs/coral-build.md): every reef a colony of capsules and plates
            // on the islands, at EVOSIM_REEF_SHAPE coral with EVOSIM_REEF_COVER 0. Mushroom by default,
            // so a launcher that names none of these runs the reefs it always ran; each coral dial
            // defaults to RunConfig's, D194's and D198's values.
            Custom("EVOSIM_REEF_SHAPE", (s, env) => s.ReefShape = ReefShapeOf(env, "EVOSIM_REEF_SHAPE")),
            Num("EVOSIM_CORAL_ISLAND_SHARE", D.CoralIslandShare, (s, v) => s.CoralIslandShare = v),
            Num("EVOSIM_CORAL_ISLAND_ROOF", D.CoralIslandRoof, (s, v) => s.CoralIslandRoof = v),
            Int("EVOSIM_CORAL_MAX_COLONIES", D.CoralMaxColonies, (s, v) => s.CoralMaxColonies = v),
            Int("EVOSIM_CORAL_MAX_PIECES_PER_COLONY", D.CoralMaxPiecesPerColony, (s, v) => s.CoralMaxPiecesPerColony = v),
            Int("EVOSIM_CORAL_MAX_PIECES", D.CoralMaxPieces, (s, v) => s.CoralMaxPieces = v),
            Num("EVOSIM_CORAL_TRUNK_HEIGHT_MIN", D.CoralTrunkHeightMinMetres, (s, v) => s.CoralTrunkHeightMin = v),
            Num("EVOSIM_CORAL_TRUNK_HEIGHT_MAX", D.CoralTrunkHeightMaxMetres, (s, v) => s.CoralTrunkHeightMax = v),
            Num("EVOSIM_CORAL_TRUNK_RADIUS_MIN", D.CoralTrunkRadiusMinMetres, (s, v) => s.CoralTrunkRadiusMin = v),
            Num("EVOSIM_CORAL_TRUNK_RADIUS_MAX", D.CoralTrunkRadiusMaxMetres, (s, v) => s.CoralTrunkRadiusMax = v),
            Int("EVOSIM_CORAL_CHILDREN_MIN", D.CoralChildrenMin, (s, v) => s.CoralChildrenMin = v),
            Int("EVOSIM_CORAL_CHILDREN_MAX", D.CoralChildrenMax, (s, v) => s.CoralChildrenMax = v),
            Num("EVOSIM_CORAL_TILT_MEAN", D.CoralTiltMeanDegrees, (s, v) => s.CoralTiltMean = v),
            Num("EVOSIM_CORAL_TILT_SPREAD", D.CoralTiltSpreadDegrees, (s, v) => s.CoralTiltSpread = v),
            Num("EVOSIM_CORAL_LENGTH_FACTOR", D.CoralLengthFactor, (s, v) => s.CoralLengthFactor = v),
            Num("EVOSIM_CORAL_RADIUS_FACTOR", D.CoralRadiusFactor, (s, v) => s.CoralRadiusFactor = v),
            Int("EVOSIM_CORAL_MAX_DEPTH", D.CoralMaxDepth, (s, v) => s.CoralMaxDepth = v),
            Num("EVOSIM_CORAL_MIN_RADIUS", D.CoralMinRadiusMetres, (s, v) => s.CoralMinRadius = v),
            Num("EVOSIM_CORAL_PLATE_CHANCE", D.CoralPlateChance, (s, v) => s.CoralPlateChance = v),
            Num("EVOSIM_CORAL_PLATE_RADIUS_MIN", D.CoralPlateRadiusMinMetres, (s, v) => s.CoralPlateRadiusMin = v),
            Num("EVOSIM_CORAL_PLATE_RADIUS_MAX", D.CoralPlateRadiusMaxMetres, (s, v) => s.CoralPlateRadiusMax = v),
            Num("EVOSIM_CORAL_PLATE_THICKNESS", D.CoralPlateThicknessMetres, (s, v) => s.CoralPlateThickness = v),
            Num("EVOSIM_CORAL_FILLET", D.CoralFilletMetres, (s, v) => s.CoralFillet = v),
            Num("EVOSIM_CORAL_FADE", D.CoralFadeMetres, (s, v) => s.CoralFade = v),
            Num("EVOSIM_CORAL_FADE_FLOOR", D.CoralFadeFloor, (s, v) => s.CoralFadeFloor = v),
            Num("EVOSIM_CORAL_TRANSMISSION", D.CoralTransmission, (s, v) => s.CoralTransmission = v),
            Num("EVOSIM_CORAL_GRID_CELL", D.CoralGridCellMetres, (s, v) => s.CoralGridCell = v),

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

            // D233: the chance per generation that a family part steps its three form genes. 0 is
            // the recorded world, and draws nothing.
            Num("EVOSIM_FORM_CHANCE", MutationRates.Default.FormChance, (s, v) => s.FormChance = v),

            // D241: growth as a climb. T is the time to adulthood in world seconds (0 is today's growth),
            // the start, min and max are the growth-reserve gene's, and the last is the chance a birth
            // steps it. Each defaults to Core's, so a launcher that names none runs the recorded world.
            Num("EVOSIM_GROWTH_ADULT", D.GrowthAdultSeconds, (s, v) => s.GrowthAdult = v),
            Num("EVOSIM_GROWTH_RESERVE_START", D.GrowthReserveStartSeconds, (s, v) => s.GrowthReserveStart = v),
            Num("EVOSIM_GROWTH_RESERVE_MIN", D.GrowthReserveMinSeconds, (s, v) => s.GrowthReserveMin = v),
            Num("EVOSIM_GROWTH_RESERVE_MAX", D.GrowthReserveMaxSeconds, (s, v) => s.GrowthReserveMax = v),
            Num("EVOSIM_GROWTH_RESERVE_MUT", MutationRates.Default.GrowthReserveChance, (s, v) => s.GrowthReserveMutation = v),

            // D106 items 1, 3 and 4's ten. Every one of them is off or neutral by default, so a
            // launcher that names none of them runs the world it always ran: health is present
            // and nothing damages it, nothing heals, nothing reaches a corpse and no attribute
            // costs anything.
            Num("EVOSIM_HEALTH", D.HealthPerCubicMetre, (s, v) => s.Health = v),
            Num("EVOSIM_HEAL", D.HealingPerSecond, (s, v) => s.Healing = v),
            Num("EVOSIM_HEAL_COST", D.HealingJoulesPerHealth, (s, v) => s.HealingCost = v),
            Num("EVOSIM_INTAKE_REACH", D.IntakeReachMetres, (s, v) => s.IntakeReach = v),
            // D214: measure the reach from each eating part's mouth point, not the body's centre. 0 is off.
            Flag("EVOSIM_INTAKE_REACH_FROM_MOUTH", (s, v) => s.IntakeReachFromMouth = v),
            Num("EVOSIM_INTAKE_WASTE", D.IntakeWasteFraction, (s, v) => s.IntakeWaste = v),
            Num("EVOSIM_PRICE_ATTACK", D.AttackWattsPerUnit, (s, v) => s.PriceAttack = v),
            Num("EVOSIM_PRICE_INTAKE", D.IntakeWattsPerUnit, (s, v) => s.PriceIntake = v),
            // The owner's intake scale (2026-10-19). 1 is every recorded mouth.
            Num("EVOSIM_INTAKE_SCALE", 1f, (s, v) => s.IntakeScale = v),
            // D219, D220: a mouth eats only what it touches; a bite feeds as it wounds. Off is every recorded world.
            Flag("EVOSIM_CONTACT_EATING", (s, v) => s.ContactEating = v),
            // D220: how long the dead stay edible, seconds. Read only with the dial above on.
            Num("EVOSIM_REMAINS_WINDOW", D.RemainsWindowSeconds, (s, v) => s.RemainsWindow = v),
            // D224: a bite on a living body takes its share of the reserve and gestation account too. Read only with the dial above on.
            Flag("EVOSIM_BITE_TAKES_STORE", (s, v) => s.BiteTakesStore = v),
            // D226: a multiplier on the damage of a mouth's blow. Read only with the dial above on.
            Num("EVOSIM_BITE_DAMAGE_SCALE", 1f, (s, v) => s.BiteDamageScale = v),
            // D232: a kill is swallowed whole into the killer's gut and leaves no remains. Read only with contact eating on.
            Flag("EVOSIM_KILL_GUT", (s, v) => s.KillSwallowsBody = v),

            // D177: the consumer cell's upkeep, W/m3. The value already lives in every config's cell
            // table, so the knob adds no field; unset is ConsumerCell's own, the recorded world.
            Num("EVOSIM_UPKEEP_CONSUMER", new ConsumerCell().UpkeepWattsPerCubicMetre, (s, v) => s.UpkeepConsumer = v),
            Num("EVOSIM_PRICE_PROTECTION", D.ProtectionWattsPerUnit, (s, v) => s.PriceProtection = v),
            Num("EVOSIM_PRICE_TOUGHNESS", D.ToughnessWattsPerUnit, (s, v) => s.PriceToughness = v),
            Num("EVOSIM_ATTRIBUTE_MUT", MutationRates.Default.AttributeMutationChance, (s, v) => s.AttributeMutation = v),

            // D114: a contact sphere on every part rather than one a body. Off is the recorded world.
            Flag("EVOSIM_CONTACT_PER_PART", (s, v) => s.ContactPerPart = v),

            // D228: a part touches as its convex core with a margin ('trueshape') rather than the ball of its
            // half-diagonal ('spheres', the recorded world). Needs the per-part contact above.
            Custom("EVOSIM_CONTACT_SHAPE", (s, env) => s.ContactShape = ContactShapeOf(env, "EVOSIM_CONTACT_SHAPE")),

            // D204: bodies touch, and the smells reach, across the box's seams. Off is the recorded box.
            Flag("EVOSIM_READ_ACROSS_SEAMS", (s, v) => s.ReadAcrossSeams = v),

            // D206: a plant keeps the units of what it burns. Off is the recorded economy.
            Flag("EVOSIM_PLANTS_KEEP_MATTER", (s, v) => s.PlantsKeepMatter = v),
            // D207: own leaves first, and D206's share made smooth. Off is D206 as first built.
            Flag("EVOSIM_PLANTS_KEEP_OWN_FIRST", (s, v) => s.PlantsKeepOwnFirst = v),

            // D137: sliding friction at the rock and the glass. 0 is the recorded, frictionless contact.
            Num("EVOSIM_BED_FRICTION", 0f, (s, v) => s.BedFriction = v),
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
            Flag("EVOSIM_SENSE_SCENT", (s, v) => s.SenseScent = v),
            Flag("EVOSIM_SENSE_DRIVE", (s, v) => s.SenseDrive = v),
            Flag("EVOSIM_SENSE_BED_TOUCH", (s, v) => s.SenseBedTouch = v),
            Custom("EVOSIM_LEAF_SCENT", (s, env) => s.LeafScent = LeafScentOf(env, "EVOSIM_LEAF_SCENT")),
            Num("EVOSIM_LEAF_SCENT_RANGE", 10f, (s, v) => s.LeafScentRange = v),
            Num("EVOSIM_LEAF_SCENT_CELL", 2f, (s, v) => s.LeafScentCell = v),
            Num("EVOSIM_LEAF_SCENT_HALF", 1f, (s, v) => s.LeafScentHalf = v),
            Flag("EVOSIM_UNBITTEN_NO_CORPSE", (s, v) => s.UnbittenNoCorpse = v),

            // D162, D165: the anchoring cell, appended to the world's cell-type table when on, and its
            // numbers. Every number is AnchoringCell's own default, unmeasured until the nursery's
            // bench sets it.
            Flag("EVOSIM_ANCHORING", (s, v) => s.Anchoring = v),
            Num("EVOSIM_ANCHOR_TENACITY", 5.8e4f, (s, v) => s.AnchorTenacity = v),
            Num("EVOSIM_ANCHOR_PRICE", 5e-5f, (s, v) => s.AnchorPrice = v),
            Num("EVOSIM_ANCHOR_DECAY", 3600f, (s, v) => s.AnchorDecay = v),
            Num("EVOSIM_ANCHOR_RESERVE_THRESHOLD", 120f, (s, v) => s.AnchorReserveThreshold = v),
            Num("EVOSIM_ANCHOR_RESERVE_LAG", 60f, (s, v) => s.AnchorReserveLag = v),
            Num("EVOSIM_ANCHOR_UPKEEP", 2.5f, (s, v) => s.AnchorUpkeep = v),
            Num("EVOSIM_ANCHOR_BUD_WEIGHT", 1f, (s, v) => s.AnchorBudWeight = v),

            // D229, D247: the fat cell, appended to the world's cell-type table when on, and its store room. Its other
            // numbers are FatCell's own and live on the type, in the config's table.
            Flag("EVOSIM_FAT", (s, v) => s.Fat = v),
            Num("EVOSIM_FAT_ROOM", FatCell.DefaultStoreRoomJoulesPerCubicMetre, (s, v) => s.FatRoom = v),

            // D146: which brain the creatures carry. The one brain's rates stay at their defaults here.
            Custom("EVOSIM_BRAIN_MODEL", (s, env) => s.BrainModel = BrainModelOf(env, "EVOSIM_BRAIN_MODEL")),
            Num("EVOSIM_SCENT_RANGE", D.ScentRangeMetres, (s, v) => s.ScentRange = v),
            Num("EVOSIM_SCENT_SCALE", D.ScentHalfScaleJoules, (s, v) => s.ScentScale = v),
            Num("EVOSIM_CHEMICAL_HALF_SCALE", D.ChemicalHalfScaleJoulesPerCubicMetre, (s, v) => s.ChemicalHalfScale = v),
            Num("EVOSIM_ENERGY_FULL_SCALE", D.EnergyFullScaleSeconds, (s, v) => s.EnergyFullScale = v),
            // D245: which squash the energy sense reads through, and the half-scale it reads at under the second.
            Custom("EVOSIM_ENERGY_SQUASH", (s, env) => s.EnergySquash = EnergySquashOf(env, "EVOSIM_ENERGY_SQUASH")),
            Num("EVOSIM_ENERGY_HALF_SCALE", D.EnergyHalfScaleSeconds, (s, v) => s.EnergyHalfScale = v),
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
        public static float ResolvePhysicsStep(float dt, out int stepsPerMetabolicStep) =>
            ResolvePhysicsStep(dt, 1f, out stepsPerMetabolicStep);

        /// <summary>
        /// <see cref="ResolvePhysicsStep(float, out int)"/> at a pace factor k (D239): a metabolic
        /// step of 0.5 world seconds holds 0.5·k physics seconds, so the step must divide 0.5·k
        /// exactly, with the same tolerance, and the steps in a metabolic step are 0.5·k / dt.
        /// </summary>
        /// <remarks>
        /// <b>At k = 1 nothing is multiplied</b>, so every dt accepted before gives the same steps,
        /// to the bit. A k below 1 or not finite is refused by name. The refusal for a dt lists the
        /// steps that do divide 0.5·k, which are 0.01·k, 0.02·k, 0.025·k, 0.05·k, 0.1·k, 0.125·k,
        /// 0.25·k and 0.5·k (those of the 50, 25, 20, 10, 5, 4, 2 and 1 steps a metabolic step
        /// holds at k = 1), though any other whole division is accepted.
        /// </remarks>
        public static float ResolvePhysicsStep(float dt, float paceFactor, out int stepsPerMetabolicStep)
        {
            if (!(paceFactor >= 1f) || float.IsInfinity(paceFactor))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(paceFactor), paceFactor,
                    "The pace factor (EVOSIM_PACE_FACTOR) must be a finite number of at least 1.");
            }

            // The physics seconds one metabolic step holds: 0.5 at k = 1, untouched by a multiplication.
            float span = paceFactor == 1f ? MetabolicStepSeconds : MetabolicStepSeconds * paceFactor;

            if (!(dt > 0f) || dt > span)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dt), dt,
                    paceFactor == 1f
                        ? "Must be in (0, 0.5]."
                        : "Must be in (0, " + Allowed(span) + "] at a pace factor of " + Allowed(paceFactor) + ".");
            }

            float steps = span / dt;
            int rounded = (int)Math.Round(steps);
            if (rounded < 1 || Math.Abs(steps - rounded) > 1e-4f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dt), dt,
                    paceFactor == 1f
                        ? "Must divide the 0.5 s metabolic step exactly: 0.01, 0.02, 0.025, 0.05, 0.1, 0.125, 0.25 or 0.5."
                        : "Must divide 0.5 s times the pace factor (" + Allowed(paceFactor) + ", so " + Allowed(span) +
                          " s) exactly: " + Allowed(span / 50f) + ", " + Allowed(span / 25f) + ", " +
                          Allowed(span / 20f) + ", " + Allowed(span / 10f) + ", " + Allowed(span / 5f) + ", " +
                          Allowed(span / 4f) + ", " + Allowed(span / 2f) + " or " + Allowed(span) + ".");
            }

            stepsPerMetabolicStep = rounded;
            return dt;
        }

        private static string Allowed(float value) =>
            value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The <see cref="RunConfig"/> the settings build — <c>EvolutionRun.RunBody</c>'s
        /// assembly, in its order.
        /// </summary>
        public static RunConfig BuildConfig(EnvSettings s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            float physicsDt = ResolvePhysicsStep(s.PhysicsDt, s.PaceFactor, out _);

            var config = new RunConfig
            {
                Fluid = new FluidConfig
                {
                    AddedMassCoefficient = s.AddedMass,
                    FluidAccelerationCoefficient = s.FluidAccel,
                    LimbPushCoefficient = s.LimbPush,
                    ShapedAddedMass = s.ShapedAddedMass,
                    StrokePushesAssembly = s.StrokeAssembly,
                    PowerPushRadiansPerSecond = s.PowerPush,
                    PoseHertz = s.PoseHz,
                    WaterHoldSeconds = s.WaterHold,
                    TurningDrag = s.TurningDrag,
                    SkinFrictionCoefficient = s.SkinFriction,
                    SpinDragCoefficient = s.SpinDrag,
                    TissueExcessDensity = s.ExcessDensity,
                    NeutralBodyVolume = s.NeutralVolume,
                    SurfaceRestoringFraction = s.SurfaceRestore,
                },
                Light = new LightModel(s.Irradiance, s.LightReach)
                {
                    DayNightAmplitude = s.DayAmplitude,
                    DayLengthSeconds = s.DayLength,
                    SeasonAmplitude = s.SeasonAmplitude,
                    SeasonLengthSeconds = s.SeasonLength,
                    SeasonShape = s.SeasonShape,
                    SeasonRampSeconds = s.SeasonRamp,
                    WeatherSpread = s.WeatherSpread,
                    WeatherCorrelation = s.WeatherCorrelation,
                    WeatherSeed = s.WeatherSeed,
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
                    new ConsumerCell(upkeepWattsPerCubicMetre: s.UpkeepConsumer),
                    new BuoyancyCell(s.LiftCost)),
            };

            // D165: the anchoring cell joins the table at its end, so every other type keeps its
            // place and a world without it is the table it always was.
            if (s.Anchoring)
            {
                config.CellTypes = config.CellTypes.Appending(
                    new AnchoringCell(
                        s.AnchorTenacity, s.AnchorPrice, s.AnchorDecay,
                        s.AnchorReserveThreshold, s.AnchorReserveLag, s.AnchorUpkeep)
                    {
                        BudWeight = s.AnchorBudWeight,
                    });
            }

            if (s.TissueEnergy > 0f)
            {
                for (int i = 0; i < config.CellTypes.Count; i++)
                {
                    config.CellTypes.At(i).TissueEnergyPerCubicMetre = s.TissueEnergy;
                }
            }

            // The owner's leaf energy (2026-10-19), after the line above: the photosynthetic cell alone, in the
            // cell table the config carries and hashes, so a world with it replays from its own config.json.
            // A leaf costs this much more to build, paid from light, and its corpse holds this much more. At 1
            // the table is not touched.
            if (s.LeafTissueEnergy != 1f)
            {
                CellType leaf = config.CellTypes.Resolve(CellTypeIds.Photosynthetic);
                leaf.TissueEnergyPerCubicMetre = leaf.TissueEnergyPerCubicMetre * s.LeafTissueEnergy;
            }

            // D247: the fat cell joins the table at its end, after the tissue-energy overrides above so that its own
            // price stands, and every other type keeps its place: a world without it is the table it always was.
            if (s.Fat)
            {
                config.CellTypes = config.CellTypes.Appending(new FatCell(s.FatRoom));
            }

            config.PerOffspringOverheadJoules = s.Overhead;
            config.PerOffspringOverheadPerTissueJoule = s.OverheadPerTissue;
            config.Mutation.GestationModeChance = s.GestationModeChance;
            config.Mutation.GestationShareChance = s.GestationShareChance;
            config.Mutation.DriveGeneChance = s.DriveGeneChance;
            config.Genome.DrawDriveGenes = s.DriveGenes;
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
            config.ReserveCapForm = s.ReserveCapForm;

            // Ordered here rather than trusted from the launcher, as EvolutionRun orders it: a
            // minimum above the maximum is a silent empty draw.
            config.Genome.MinReserveMargin = Math.Min(s.MarginMin, s.MarginMax);
            config.Genome.MaxReserveMargin = Math.Max(s.MarginMin, s.MarginMax);
            config.Mutation.MarginChance = s.MarginChance;
            config.Mutation.AddNodeChance = s.AddNodeChance;
            config.Mutation.AddEdgeChance = s.AddEdgeChance;
            config.Mutation.RemoveEdgeChance = s.RemoveEdgeChance;
            config.Mutation.AddNeuronChance = s.AddNeuronChance;
            config.Mutation.RemoveNeuronChance = s.RemoveNeuronChance;
            config.Mutation.RecursiveLimitChance = s.RecursiveLimitChance;
            config.Mutation.JointTypeChance = s.JointTypeChance;
            config.FloorRefugeMetres = s.FloorRefuge;
            config.RefugeEdibleFraction = s.RefugeFraction;
            config.SatiationWattsPerCubicMetre = s.Satiation;
            config.ClearanceToeDensity = s.ClearanceToe;
            config.ExudationFraction = s.Exudation;
            config.ConceptionOrder = s.ConceptionOrder;
            config.MatterInfluxPerSecond = s.MatterInflux;
            config.MatterInfluxAt = s.MatterInfluxAt;
            config.MatterBurialPerSecond = s.MatterBurial;
            config.MatterBurialDesertPerSecond = s.MatterBurialDesert;
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
            config.FreeFirstLinkCubicMetres = s.FreeLink;
            config.SelfOverlapDepthFraction = s.SelfOverlap;
            config.Development.MaxBodyReachMetres = s.MaxReach;
            config.Development.FloorsWeighRigidGroups = s.RigidFloors;
            config.Development.ScaleLengthensOnce = s.ScaleLengthensOnce;
            config.Development.ReflectMirroredAnchors = s.ReflectMirroredAnchors;
            config.ShapeIds = s.ShapeIds;

            config.MatterIslandWavelengthMetres = s.MatterIslands;
            config.MatterIslandCover = s.MatterIslandCover;
            config.MatterIslandDepthMetres = s.MatterIslandDepth;
            config.MatterIslandGlassMarginMetres = s.MatterIslandMargin;
            config.SeedOverrides = s.SeedOverrides;
            config.FoundersFollowMatter = s.FoundersFollowMatter;
            config.FoundersFollowFood = s.FoundersFollowFood;
            config.FoundersFollowFoodDepth = s.FoundersFollowFoodDepth;
            config.FoundersFollowIncomeDepth = s.FoundersFollowIncomeDepth;
            config.FoundersFollowCorpses = s.FoundersFollowCorpses;
            config.FounderEndowmentSeconds = s.FounderEndowment;
            config.FounderReserveCapFraction = s.FounderReserveCap;
            config.LightShadeDepth = s.LightShade;
            config.LightShadeDriftMetresPerHour = s.LightShadeDrift;
            config.WorldAreaSquareMetres = s.Area;
            config.WorldDepthMetres = s.Depth;
            config.SharedSpace = s.SharedSpace;
            config.OffspringDispersalMetres = s.OffspringDispersal;
            config.PhysicsStepSeconds = physicsDt;
            config.PaceFactor = s.PaceFactor;
            config.FloorClosesAfterSeconds = s.FloorCloses;
            config.FoundingTricklePerSecond = s.Trickle;

            // D117's share only: the pool's count and hash are the files', and Program sets them
            // after reading EVOSIM_TRICKLE_POOL, before the config is written.
            config.FoundingTricklePoolShare = s.TricklePoolShare;
            config.FoundingFloorPoolShare = s.FloorPoolShare;
            config.MaximumPopulation = s.MaxPopulation;
            config.MaximumTissueJoules = s.MaxTissue;
            config.SenescenceDoublingSeconds = s.Senescence;
            config.SenescenceWearsIntake = s.SenescenceWearsIntake;
            config.AgeHazardBaseSeconds = s.AgeBase;
            config.AgeHazardDoublingFraction = s.AgeDoubling;
            config.AgeingPerBirth = s.AgeBirth;
            config.AgeingPerWound = s.AgeWound;
            config.AgeRepairExponent = s.AgeRepair;
            config.Mutation.LifespanChance = s.LifespanChance;
            config.Genome.MinLifespan = Math.Min(s.LifespanMin, s.LifespanMax);
            config.Genome.MaxLifespan = Math.Max(s.LifespanMin, s.LifespanMax);
            config.Mutation.CellTypeChance = s.CellTypeMutation;
            config.SenseChemical = s.SenseChemical;
            config.SenseEnergy = s.SenseEnergy;
            config.SenseFlow = s.SenseFlow;
            config.SenseContact = s.SenseContact;
            config.SenseDamage = s.SenseDamage;
            config.SenseScent = s.SenseScent;
            config.SenseDrive = s.SenseDrive;
            config.SenseBedTouch = s.SenseBedTouch;
            config.LeafScent = s.LeafScent;
            config.LeafScentRangeMetres = s.LeafScentRange;
            config.LeafScentCellMetres = s.LeafScentCell;
            config.LeafScentHalfScaleSquareMetres = s.LeafScentHalf;
            config.UnbittenDeathsLeaveNoCorpse = s.UnbittenNoCorpse;
            config.Brain.BrainModel = s.BrainModel;
            config.ScentRangeMetres = s.ScentRange;
            config.ScentHalfScaleJoules = s.ScentScale;
            config.ChemicalHalfScaleJoulesPerCubicMetre = s.ChemicalHalfScale;
            config.EnergyFullScaleSeconds = s.EnergyFullScale;
            config.EnergySenseSquash = s.EnergySquash;
            config.EnergyHalfScaleSeconds = s.EnergyHalfScale;
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
            config.CorpseSinkMetresPerSecond = s.CorpseSink;
            config.CorpseSettles = s.CorpseSettle;
            config.CorpseLeavesBelowFraction = s.CorpseFloor;
            config.CorpseSettledDecayPerSecond = s.CorpseDecaySettled;
            config.CorpseHalfLifeSeconds = s.CorpseHalfLife;
            config.WorldShape = s.WorldShape;
            config.MatterBudgetUnits = s.MatterBudget;
            config.DriveLimitAtEveryStep = s.DriveLimitAlways;
            config.DragLimitByWeldedBody = s.DragLimitWelded;
            config.DragLimitAtEveryStep = s.DragLimitAlways;

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

            config.ReefShape = s.ReefShape;
            config.CoralIslandShare = s.CoralIslandShare;
            config.CoralIslandRoof = s.CoralIslandRoof;
            config.CoralMaxColonies = s.CoralMaxColonies;
            config.CoralMaxPiecesPerColony = s.CoralMaxPiecesPerColony;
            config.CoralMaxPieces = s.CoralMaxPieces;
            config.CoralTrunkHeightMinMetres = s.CoralTrunkHeightMin;
            config.CoralTrunkHeightMaxMetres = s.CoralTrunkHeightMax;
            config.CoralTrunkRadiusMinMetres = s.CoralTrunkRadiusMin;
            config.CoralTrunkRadiusMaxMetres = s.CoralTrunkRadiusMax;
            config.CoralChildrenMin = s.CoralChildrenMin;
            config.CoralChildrenMax = s.CoralChildrenMax;
            config.CoralTiltMeanDegrees = s.CoralTiltMean;
            config.CoralTiltSpreadDegrees = s.CoralTiltSpread;
            config.CoralLengthFactor = s.CoralLengthFactor;
            config.CoralRadiusFactor = s.CoralRadiusFactor;
            config.CoralMaxDepth = s.CoralMaxDepth;
            config.CoralMinRadiusMetres = s.CoralMinRadius;
            config.CoralPlateChance = s.CoralPlateChance;
            config.CoralPlateRadiusMinMetres = s.CoralPlateRadiusMin;
            config.CoralPlateRadiusMaxMetres = s.CoralPlateRadiusMax;
            config.CoralPlateThicknessMetres = s.CoralPlateThickness;
            config.CoralFilletMetres = s.CoralFillet;
            config.CoralFadeMetres = s.CoralFade;
            config.CoralFadeFloor = s.CoralFadeFloor;
            config.CoralTransmission = s.CoralTransmission;
            config.CoralGridCellMetres = s.CoralGridCell;

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
            config.Mutation.FormChance = s.FormChance;

            config.GrowthAdultSeconds = s.GrowthAdult;
            config.JuvenileStrengthScales = s.JuvenileStrength;
            config.GrowthReserveStartSeconds = s.GrowthReserveStart;
            config.GrowthReserveMinSeconds = s.GrowthReserveMin;
            config.GrowthReserveMaxSeconds = s.GrowthReserveMax;
            config.Mutation.GrowthReserveChance = s.GrowthReserveMutation;

            config.HealthPerCubicMetre = s.Health;
            config.HealingPerSecond = s.Healing;
            config.HealingJoulesPerHealth = s.HealingCost;
            config.IntakeReachMetres = s.IntakeReach;
            config.IntakeReachFromMouth = s.IntakeReachFromMouth;
            config.IntakeWasteFraction = s.IntakeWaste;
            config.AttackWattsPerUnit = s.PriceAttack;
            config.IntakeWattsPerUnit = s.PriceIntake;
            config.IntakeScale = s.IntakeScale;
            config.ContactEating = s.ContactEating;
            config.RemainsWindowSeconds = s.RemainsWindow;
            config.BiteTakesStore = s.BiteTakesStore;
            config.BiteDamageScale = s.BiteDamageScale;
            config.KillSwallowsBody = s.KillSwallowsBody;
            config.ProtectionWattsPerUnit = s.PriceProtection;
            config.ToughnessWattsPerUnit = s.PriceToughness;
            config.Mutation.AttributeMutationChance = s.AttributeMutation;
            config.ContactPerPart = s.ContactPerPart;
            config.ContactShape = s.ContactShape;
            config.ReadAcrossSeams = s.ReadAcrossSeams;
            config.PlantsKeepMatter = s.PlantsKeepMatter;
            config.PlantsKeepOwnFirst = s.PlantsKeepOwnFirst;
            config.BedFriction = s.BedFriction;

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

        /// <summary>
        /// A whole number that refuses the launch when it will not parse, where
        /// <see cref="ULong"/> (EvolutionRun's EnvULong, transcribed) falls back silently. For a
        /// world setting with no Unity twin, so nothing constrains it to the old reader's habit.
        /// </summary>
        private static ulong StrictULong(Lookup env, string name, ulong fallback)
        {
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            if (ulong.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v))
            {
                return v;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is not a whole number of at least 0. Unset it or " +
                "give it one; a setting that fails to parse must not become its default without " +
                "anyone knowing.");
        }

        private static SeasonShape SeasonShapeOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return SeasonShape.Smooth;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "smooth": return SeasonShape.Smooth;
                case "two-season":
                case "twoseason": return SeasonShape.TwoSeason;

                default:
                    throw new ArgumentException(
                        name + " is '" + raw + "', which is not a season shape. " +
                        "Known: smooth, two-season.");
            }
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

        /// <summary>
        /// D233's shape list: comma-separated ids in their order, box, sphere and capsule when unset.
        /// Each id must be in <see cref="PartShapeRegistry.Catalogue"/> and named once, or the launch is
        /// refused here rather than at the first read of <see cref="RunConfig.Shapes"/>.
        /// </summary>
        private static string[] ShapeIdsOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return EnvSettings.StandardShapeIds();

            string[] parts = raw.Split(',');
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
                if (!PartShapeRegistry.Catalogue.Contains(parts[i]))
                {
                    throw new ArgumentException(
                        name + " names '" + parts[i] + "', which is not a shape this build knows. Known: " +
                        PartShapeRegistry.Catalogue + ".");
                }

                if (!seen.Add(parts[i]))
                {
                    throw new ArgumentException(name + " names '" + parts[i] + "' twice.");
                }
            }

            return parts;
        }

        /// <summary>D192's seed overrides: a comma-separated list, empty when unset; each entry is RunConfig's to judge.</summary>
        private static string[] SeedOverridesOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return parts;
        }

        private static LeafScentForm LeafScentOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return LeafScentForm.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "none": return LeafScentForm.None;
                case "strength": return LeafScentForm.Strength;
                case "direction": return LeafScentForm.Direction;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is none of 'none', 'strength' or 'direction'.");
        }

        private static TurningDragForm TurningDragOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return TurningDragForm.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "none": return TurningDragForm.None;
                case "skin": return TurningDragForm.Skin;
                case "spin": return TurningDragForm.Spin;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is none of 'none', 'skin' or 'spin'.");
        }

        private static EnergySenseSquash EnergySquashOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return EnergySenseSquash.Tanh;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "tanh": return EnergySenseSquash.Tanh;
                case "halfscale": return EnergySenseSquash.HalfScale;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is neither 'tanh' nor 'halfscale'.");
        }

        private static ReserveCapForm ReserveCapFormOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return ReserveCapForm.Seconds;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "seconds": return ReserveCapForm.Seconds;
                case "bodyandstore": return ReserveCapForm.BodyAndStore;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is neither 'seconds' nor 'bodyandstore'.");
        }

        private static ContactShapeForm ContactShapeOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return ContactShapeForm.Spheres;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "spheres": return ContactShapeForm.Spheres;
                case "trueshape": return ContactShapeForm.TrueShape;
                case "trueshapeatpoint": return ContactShapeForm.TrueShapeAtPoint;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is none of 'spheres', 'trueshape' or 'trueshapeatpoint'.");
        }

        private static BrainModel BrainModelOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return BrainModel.PerNode;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "per-node": return BrainModel.PerNode;
                case "central": return BrainModel.Central;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is neither 'per-node' nor 'central'.");
        }

        private static ReefShape ReefShapeOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return ReefShape.Mushroom;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "mushroom": return ReefShape.Mushroom;
                case "coral": return ReefShape.Coral;
            }

            throw new ArgumentException(name + " is '" + raw + "', which is neither 'mushroom' nor 'coral'.");
        }

        private static CurrentMode CurrentModeOf(Lookup env, string name)
        {
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return CurrentMode.Rolls;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "rolls": return CurrentMode.Rolls;
                case "transport": return CurrentMode.Transport;
                case "ocean": return CurrentMode.Ocean;
            }

            throw new ArgumentException(
                name + " is '" + raw + "', which is none of 'rolls', 'transport' and 'ocean'.");
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
                case "islands": return MatterInflux.Islands;

                default:
                    throw new ArgumentException(
                        name + " is '" + raw + "', which is not a matter influx route. " +
                        "Known: surface, vent, islands.");
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

        /// <summary>
        /// <c>EVOSIM_RECORD_FORMAT</c>: 1 or 2, and 2 when unset. Anything else refuses the launch
        /// rather than writing a record nobody asked for.
        /// </summary>
        private static int RecordFormatOf(Lookup env)
        {
            float v = Num(env, "EVOSIM_RECORD_FORMAT", RunRecordFormat.Newest);

            if (v == RunRecordFormat.Jsonl) return RunRecordFormat.Jsonl;
            if (v == RunRecordFormat.Compact) return RunRecordFormat.Compact;

            throw new ArgumentException(
                "EVOSIM_RECORD_FORMAT is '" + env("EVOSIM_RECORD_FORMAT") + "'. The records are 1 " +
                "(every genome in every snapshot, positions.jsonl and poses.jsonl) and 2 (each " +
                "genome once, slim snapshots, gzip in members, the state stream); unset is 2.");
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

        /// <summary>The checkpoint stall hook's milliseconds: 0 unset, else 1 to 10,000 or refused.</summary>
        internal static int StallMs(Lookup env)
        {
            const string name = "EVOSIM_TEST_CHECKPOINT_STALL_MS";
            string raw = env(name);
            if (string.IsNullOrEmpty(raw)) return 0;

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms) || ms < 1 || ms > 10000)
            {
                throw new ArgumentException(
                    name + " is '" + raw + "'. It is a test hook that pauses every checkpoint's write, and it " +
                    "takes a whole number of milliseconds from 1 to 10,000, so a typo cannot hang a run. Unset " +
                    "it for any run that is not that test.");
            }

            return ms;
        }
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

        /// <summary>D127's free first link, m3 of the adult part — <c>EVOSIM_FREE_LINK</c>.</summary>
        public float FreeLink;
        public float SelfOverlap;
        public float MaxReach;
        public bool RigidFloors;

        /// <summary>D199's reading three — <c>EVOSIM_SCALE_LENGTHENS_ONCE</c>.</summary>
        public bool ScaleLengthensOnce;

        /// <summary>D228 — <c>EVOSIM_REFLECT_MIRRORED_ANCHORS</c>.</summary>
        public bool ReflectMirroredAnchors;

        /// <summary>D241's juvenile strength — <c>EVOSIM_JUVENILE_STRENGTH</c>. Off is the recorded world.</summary>
        public bool JuvenileStrength;
        public float MatterIslands;
        public float MatterIslandCover;
        public float MatterIslandDepth;
        public float MatterIslandMargin;
        public string[] SeedOverrides = Array.Empty<string>();

        /// <summary>D233's shape list — <c>EVOSIM_SHAPES</c>. Box, sphere and capsule unless named.</summary>
        public string[] ShapeIds = StandardShapeIds();

        /// <summary>The recorded world's list, a fresh array each call so no caller can edit another's.</summary>
        public static string[] StandardShapeIds() =>
            new[] { Evosim.Core.ShapeIds.Box, Evosim.Core.ShapeIds.Sphere, Evosim.Core.ShapeIds.Capsule };
        public bool FoundersFollowMatter;
        public bool FoundersFollowFood;

        /// <summary>The round 48 founding ruling's depth — <c>EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH</c>.</summary>
        public bool FoundersFollowFoodDepth;

        /// <summary>The round 50 founding ruling's leaf depth — <c>EVOSIM_FOUNDERS_INCOME_DEPTH</c>.</summary>
        public bool FoundersFollowIncomeDepth;

        /// <summary>D129's mouth founders at the corpses — <c>EVOSIM_FOUNDERS_FOLLOW_CORPSES</c>.</summary>
        public bool FoundersFollowCorpses;

        /// <summary>The round 48 founding ruling's endowment, s — <c>EVOSIM_FOUNDER_ENDOWMENT</c>.</summary>
        public float FounderEndowment;

        /// <summary>D124's founder cap, a fraction of the gate — <c>EVOSIM_FOUNDER_RESERVE_CAP</c>.</summary>
        public float FounderReserveCap;
        public float LightShade;
        public float LightShadeDrift;
        public float BudgetSeconds;
        public float WallMinutes;
        public int ReportEvery;
        public ulong Seed;
        public long DigestEvery;

        /// <summary>EVOSIM_TEST_CHECKPOINT_STALL_MS: 0 in every run but the kill test's.</summary>
        public int TestCheckpointStallMs;
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
        /// D159: how often a world of a migrating group sends immigrants to the next, simulated
        /// seconds. 0 is no exchange.
        /// </summary>
        public float MigrateEvery;

        /// <summary>D159: how many genomes each world sends at every exchange.</summary>
        public int MigrateCount = 1;

        /// <summary>
        /// Which record the run writes, <c>EVOSIM_RECORD_FORMAT</c>:
        /// <see cref="RunRecordFormat.Compact"/> unless a launcher says 1.
        /// </summary>
        public int RecordFormat = RunRecordFormat.Newest;

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

        /// <summary>
        /// D201: an edited copy of the resumed run's config.json to carry on under. Null carries on
        /// under the source's own. Requires <see cref="ResumeFrom"/>.
        /// </summary>
        /// <remarks>
        /// Only the dials <see cref="CousinConfig"/> allows may differ, and a run that differs in
        /// any is a cousin of its source and says so in run.json.
        /// </remarks>
        public string ResumeConfig;

        /// <summary>
        /// D201: the founding trickle off from the checkpoint on, its rate and its pool share set
        /// to 0 and nothing else changed; the pool stays pinned and loaded. Requires
        /// <see cref="ResumeFrom"/>.
        /// </summary>
        public bool ResumeTrickleOff;

        /// <summary>
        /// Which engine steps the bodies: <c>cpu</c> (the solver, every recorded run) or
        /// <c>gpu</c> (logbook/specs/gpu-port-spec.md). A farm setting, recorded in run.json.
        /// </summary>
        public string Engine = "cpu";

        /// <summary>The gpu engine's device: <c>cuda</c>, or <c>cpu</c> for ILGPU's CPU accelerator.</summary>
        public string GpuDevice = "cuda";

        /// <summary>The gpu engine's arithmetic: <c>single</c> or <c>double</c>.</summary>
        public string GpuPrecision = "single";

        /// <summary>Threads a group on the card; 0 lets ILGPU choose.</summary>
        public int GpuGroup = 32;

        /// <summary>The contact grid's mean: <c>serial</c>, <c>chunked</c>, or empty for the precision's default.</summary>
        public string GpuMean = "";

        /// <summary>A diagnostic: every body uploaded again at every block.</summary>
        public bool GpuResync;

        /// <summary>The size classes' step kernels on a stream each, run at once: the same numbers, sooner.</summary>
        public bool GpuConcurrent;

        /// <summary>The snow's transport on the card, with the CPU's bits (GpuTransport): the same numbers, sooner.</summary>
        public bool GpuTransport;

        /// <summary>The scent's fill on the card from the world's corpses (GpuScent): the same numbers, sooner.</summary>
        public bool GpuScent;

        /// <summary>The pace guard (PaceGuard): stops a world that slows at a checkpoint, with the reason. On by default.</summary>
        public bool PaceGuard = true;

        /// <summary>The guard trips when the recent pace is under this share of the reference pace.</summary>
        public float PaceRatio = 0.5f;

        /// <summary>The fewest rows each of the guard's two windows holds before it is read.</summary>
        public int PaceRows = 2;

        /// <summary>The rows the card probe runs between the trip and the stop.</summary>
        public int PaceProbeRows = 1;

        /// <summary>The recent window of wall time, its pace read against the reference's.</summary>
        public float PaceWindowMinutes = 10f;

        /// <summary>The reference window of wall time, before the recent one.</summary>
        public float PaceHourMinutes = 60f;

        /// <summary>The wall time from the first row before the guard can trip.</summary>
        public float PaceWarmupMinutes = 10f;

        /// <summary>EVOSIM_TEST_PACE_TRIP_AT: below 0 in every run but a test's, which trips the guard at that second.</summary>
        public float TestPaceTripAt = -1f;

        /// <summary>A checkpoint at the row of the run's first overflow of a card cap (CapWatch). On by default.</summary>
        public bool OverflowCheckpoint = true;

        /// <summary>Whether this run asks for the gpu engine; anything but cpu or gpu is refused by the farm.</summary>
        public bool EngineIsGpu => string.Equals(Engine, "gpu", System.StringComparison.Ordinal);

        public float Idle;
        public float MaxPower;
        public float MinPower;
        public float LinkPhoto;
        public float DayAmplitude;
        public float DayLength;
        public float SeasonAmplitude;
        public float SeasonLength;
        public SeasonShape SeasonShape;
        public float SeasonRamp;
        public float WeatherSpread;
        public float WeatherCorrelation;
        public ulong WeatherSeed = 1UL;
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
        public float AddNodeChance;
        public float AddEdgeChance;
        public float RemoveEdgeChance;
        public float AddNeuronChance;
        public float RemoveNeuronChance;
        public float RecursiveLimitChance;
        public float JointTypeChance;
        public float FloorRefuge;
        public float RefugeFraction;
        public float Satiation;
        public float ClearanceToe;
        public float Exudation;
        public ConceptionOrder ConceptionOrder;
        public float MatterInflux;
        public MatterInflux MatterInfluxAt;
        public float MatterBurial;
        public float MatterBurialDesert;
        public float PhysicsDt;

        /// <summary>D239's pace factor k, <c>EVOSIM_PACE_FACTOR</c>; 1 is the recorded world.</summary>
        public float PaceFactor;
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

        /// <summary>
        /// D117's pool: genome files, semicolon-separated, or <c>@</c> and a weighted list file
        /// (<see cref="TricklePoolFiles.Prepare"/>) — <c>EVOSIM_TRICKLE_POOL</c>.
        /// </summary>
        public string TricklePool;

        /// <summary>D117's share of the trickle drawn from the pool — <c>EVOSIM_TRICKLE_POOL_SHARE</c>.</summary>
        public float TricklePoolShare;

        /// <summary>The share of the floor's founders drawn from the pool — <c>EVOSIM_FLOOR_POOL_SHARE</c>.</summary>
        public float FloorPoolShare;
        public int MaxPopulation;
        public double MaxTissue;
        public float Senescence;

        /// <summary>Whether senescence divides intake too — <c>EVOSIM_SENESCENCE_WEARS_INTAKE</c>, on when unset.</summary>
        public bool SenescenceWearsIntake;

        /// <summary>D128's hazard: the base, s (<c>EVOSIM_AGE_BASE</c>, 0 off), and its doubling fraction.</summary>
        public float AgeBase;
        public float AgeDoubling;

        /// <summary>D128's pushes, in doublings, for a birth and for a destroyed part.</summary>
        public float AgeBirth;
        public float AgeWound;

        /// <summary>D128's repair exponent, and the lifespan gene's mutation chance and founder range.</summary>
        public float AgeRepair;
        public float LifespanChance;
        public float LifespanMin;
        public float LifespanMax;
        public float CellTypeMutation;
        public float Clearance;
        public float TissueEnergy;
        public float LeafTissueEnergy;
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
        public float LimbPush;
        public bool ShapedAddedMass;
        public bool StrokeAssembly;
        public float PowerPush;
        public bool DriveGenes;
        public float DriveGeneChance;
        public float PoseHz;
        public float WaterHold;
        public TurningDragForm TurningDrag;
        public float SkinFriction;
        public float SpinDrag;
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

        /// <summary>D127's corpse: its sink rate, m/s (0 the snow's), whether it settles, and its floor.</summary>
        public float CorpseSink;
        public bool CorpseSettle;
        public float CorpseFloor;

        /// <summary>D131: a settled corpse's decay per second (0 the sinking rate).</summary>
        public float CorpseDecaySettled;
        public float CorpseHalfLife;
        public WorldShape WorldShape;
        public float MatterBudget;
        public bool DriveLimitAlways;

        /// <summary>D199's drag limiter by the welded body — <c>EVOSIM_DRAG_LIMIT_WELDED</c>.</summary>
        public bool DragLimitWelded;

        /// <summary>D196's drag limiter at every step — <c>EVOSIM_DRAG_LIMIT_ALWAYS</c>.</summary>
        public bool DragLimitAlways;
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

        public ReefShape ReefShape;
        public float CoralIslandShare;
        public float CoralIslandRoof;
        public int CoralMaxColonies;
        public int CoralMaxPiecesPerColony;
        public int CoralMaxPieces;
        public float CoralTrunkHeightMin;
        public float CoralTrunkHeightMax;
        public float CoralTrunkRadiusMin;
        public float CoralTrunkRadiusMax;
        public int CoralChildrenMin;
        public int CoralChildrenMax;
        public float CoralTiltMean;
        public float CoralTiltSpread;
        public float CoralLengthFactor;
        public float CoralRadiusFactor;
        public int CoralMaxDepth;
        public float CoralMinRadius;
        public float CoralPlateChance;
        public float CoralPlateRadiusMin;
        public float CoralPlateRadiusMax;
        public float CoralPlateThickness;
        public float CoralFillet;
        public float CoralFade;
        public float CoralFadeFloor;
        public float CoralTransmission;
        public float CoralGridCell;

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

        /// <summary>D233's form walk — <c>EVOSIM_FORM_CHANCE</c>. 0 draws nothing.</summary>
        public float FormChance;

        /// <summary>D241's time to adulthood, world seconds — <c>EVOSIM_GROWTH_ADULT</c>. 0 is today's growth.</summary>
        public float GrowthAdult;

        /// <summary>D241's growth-reserve gene: the start given at admission, seconds — <c>EVOSIM_GROWTH_RESERVE_START</c>.</summary>
        public float GrowthReserveStart;

        /// <summary>The gene's range floor, seconds — <c>EVOSIM_GROWTH_RESERVE_MIN</c>.</summary>
        public float GrowthReserveMin;

        /// <summary>The gene's range ceiling, seconds — <c>EVOSIM_GROWTH_RESERVE_MAX</c>.</summary>
        public float GrowthReserveMax;

        /// <summary>The chance a birth steps the gene — <c>EVOSIM_GROWTH_RESERVE_MUT</c>. 0 draws nothing.</summary>
        public float GrowthReserveMutation;
        public float Health;
        public float Healing;
        public float HealingCost;
        public float IntakeReach;

        /// <summary>D214: the reach from each eating part's mouth point — <c>EVOSIM_INTAKE_REACH_FROM_MOUTH</c>.</summary>
        public bool IntakeReachFromMouth;
        public float IntakeWaste;
        public float PriceAttack;
        public float PriceIntake;
        public float IntakeScale;

        /// <summary>D219, D220: bites feed as they wound and no corpse is founded (<c>EVOSIM_CONTACT_EATING</c>).</summary>
        public bool ContactEating;

        /// <summary>D220: seconds the dead stay edible (<c>EVOSIM_REMAINS_WINDOW</c>), read only with the dial on.</summary>
        public float RemainsWindow;

        /// <summary>D224: a bite on a living body takes its share of the store too (<c>EVOSIM_BITE_TAKES_STORE</c>), read only with the dial above on.</summary>
        public bool BiteTakesStore;

        /// <summary>D226: the multiplier on a mouth's blow (<c>EVOSIM_BITE_DAMAGE_SCALE</c>), read only with the dial above on.</summary>
        public float BiteDamageScale;

        /// <summary>D232: a kill is swallowed whole into the killer's gut (<c>EVOSIM_KILL_GUT</c>), read only with the contact-eating dial on.</summary>
        public bool KillSwallowsBody;
        public float PriceProtection;
        public float PriceToughness;
        public float AttributeMutation;
        public bool ContactPerPart;
        public ContactShapeForm ContactShape;
        public bool ReadAcrossSeams;
        public bool PlantsKeepMatter;
        public bool PlantsKeepOwnFirst;
        public float BedFriction;
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
        public bool SenseScent;
        public bool SenseDrive;
        public bool SenseBedTouch;
        public LeafScentForm LeafScent;
        public float LeafScentRange;
        public float LeafScentCell;
        public float LeafScentHalf;
        public bool UnbittenNoCorpse;
        public bool Anchoring;

        /// <summary>D247: the fat cell is appended to the cell-type table (<c>EVOSIM_FAT</c>).</summary>
        public bool Fat;

        /// <summary>D247: fat's store room, J/m³ (<c>EVOSIM_FAT_ROOM</c>).</summary>
        public float FatRoom;

        /// <summary>D229, D247: the rule the reserve cap is by (<c>EVOSIM_RESERVE_CAP_FORM</c>).</summary>
        public ReserveCapForm ReserveCapForm;

        /// <summary>D245: the energy sense's squash (<c>EVOSIM_ENERGY_SQUASH</c>).</summary>
        public EnergySenseSquash EnergySquash;

        /// <summary>D245: the half-scale under <c>halfscale</c>, seconds (<c>EVOSIM_ENERGY_HALF_SCALE</c>).</summary>
        public float EnergyHalfScale;
        public float AnchorTenacity;
        public float AnchorPrice;
        public float AnchorDecay;
        public float AnchorReserveThreshold;
        public float AnchorReserveLag;
        public float AnchorUpkeep;
        public float AnchorBudWeight;
        public float UpkeepConsumer;
        public BrainModel BrainModel;
        public float ScentRange;
        public float ScentScale;
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

        /// <summary>
        /// The report interval in simulated seconds: <see cref="ReportEvery"/> metabolic steps.
        /// </summary>
        public float ReportIntervalSeconds => Math.Max(1, ReportEvery) * MetabolicStep;

        /// <summary>
        /// Record format 2's one default: the state stream on at the report interval, in a shared
        /// world, when the launcher did not name <c>EVOSIM_POSE_EVERY</c>. True when it applied.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Format 2 does not write <c>poses.jsonl</c>, and the stream at the report interval
        /// carries what that file carried, at the same seconds, plus the body fraction
        /// (<c>logbook/specs/record-and-film-spec.md</c> A4). A shared world only, because
        /// <c>poses.jsonl</c> was written only beside <c>positions.jsonl</c>, and a tiled world's
        /// places are a lattice and not a place.
        /// </para>
        /// <para>
        /// Called after a resume has inherited its source's cadences, so a stream the source was
        /// already writing keeps its own cadence. A launcher that names the variable wins,
        /// including one that names 0 to write no stream at all; <see cref="Provided"/> is what
        /// tells that apart from the unset default.
        /// </para>
        /// </remarks>
        public bool ApplyRecordDefaults(bool sharedSpace)
        {
            if (RecordFormat != RunRecordFormat.Compact || !sharedSpace) return false;
            if (Provided.Contains("EVOSIM_POSE_EVERY") || PoseEvery > 0f) return false;

            PoseEvery = ReportIntervalSeconds;
            return true;
        }

        private const float MetabolicStep = EnvBinding.MetabolicStepSeconds;
    }
}
