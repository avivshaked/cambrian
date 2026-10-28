using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The world exceeded a runaway ceiling. DESIGN.md §5A.7, D021, and fable-propose-growth.md
    /// rule 9's tissue ceiling.
    /// </summary>
    /// <remarks>
    /// Its own type so a sweep harness can catch it and record "this configuration exploded" as
    /// a result rather than as a crash. A runaway is a measurement: it locates one end of the
    /// transition in §5A.6b just as precisely as extinction locates the other.
    /// <para>
    /// Two ceilings can throw this, and <see cref="Ceiling"/> says which one did. Growth made
    /// the count ceiling alone unreliable: a child is born at a median third of its adult body,
    /// so 5,000 bodies can be a third of the biomass that count was calibrated against, and the
    /// same count no longer means the same photosynthetic mat it used to (the owner's ruling of
    /// 2026-09-09, after logbook/0081). <see cref="Population"/> and <see cref="TissueJoules"/>
    /// are both always populated, whichever ceiling fired, so a catcher never has to re-read the
    /// world to get the reading the message did not need.
    /// </para>
    /// </remarks>
    public sealed class PopulationRunawayException : Exception
    {
        public int Population { get; }

        /// <summary>Standing tissue joules across the living at the moment this was thrown.</summary>
        public double TissueJoules { get; }

        public double ElapsedSeconds { get; }

        /// <summary>Which ceiling fired: <c>"population"</c> or <c>"tissue"</c>.</summary>
        public string Ceiling { get; }

        public PopulationRunawayException(
            string message, int population, double tissueJoules, double elapsedSeconds, string ceiling)
            : base(message)
        {
            Population = population;
            TissueJoules = tissueJoules;
            ElapsedSeconds = elapsedSeconds;
            Ceiling = ceiling;
        }
    }

    /// <summary>
    /// The ecosystem loop: creatures earn, spend, breed and starve — DESIGN.md §5A.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here scores anything.</b> There is no fitness function, no selection step and
    /// no ranking. A creature persists while it is solvent and stops when it is not, and that is
    /// the entire selective mechanism (§5A.0, D017).
    /// </para>
    /// <para>
    /// <b>No physics, deliberately, and this is what makes the design testable at all.</b>
    /// Photosynthetic income depends on lit area and depth; upkeep depends on tissue; neither
    /// needs a solver. Height and mechanical work are the only physical quantities in §5A.2's
    /// ledger and both arrive through <see cref="Observe"/>, so the calibration question §5A.2
    /// calls the knob that decides everything can be swept in milliseconds instead of stepped
    /// through PhysX at 6.4 ms per step (§5A.9).
    /// </para>
    /// <para>
    /// <b>A world with nothing calling <see cref="Observe"/> is a world of stationary
    /// organisms for whom swimming is free</b>, and that is what every number in §5A.2b was
    /// measured against. It remains a legitimate configuration — it is the fast sweep — but it is
    /// a different world from the embodied one, and results from the two are not interchangeable.
    /// </para>
    /// <para>
    /// <b>The population floor is the only endogenous thing that creates a creature from
    /// nothing</b> (D021), including at t=0. There is no separate seeding path, so the mechanism
    /// that repopulates a collapsing world is the same one exercised on the very first step —
    /// tested continuously rather than once. <see cref="Inoculate"/> is the one deliberate
    /// exception, and it is exogenous by design: D060's invasion assay is a labeled hand that
    /// builds an experimental condition at a chosen instant, never something the world does on
    /// its own.
    /// </para>
    /// </remarks>
    public sealed partial class World
    {
        private readonly List<Organism> _living = new List<Organism>();
        private readonly List<Organism> _dead = new List<Organism>();
        private readonly List<Organism> _born = new List<Organism>();

        /// <summary>
        /// Bodies that have died and not yet finished leaking into the water. Rule 6 of
        /// <c>fable-propose-grid.md</c>. Always empty at
        /// <see cref="RunConfig.CorpseDecayPerSecond"/> 0, which is every run on file.
        /// </summary>
        private readonly List<Corpse> _corpses = new List<Corpse>();

        // DepositCorpses' scratch on a grid: each corpse's cell, instalment and whether it leaves.
        private int[] _depositCell;
        private double[] _depositAmount;
        private bool[] _depositLast;

        // D129's corpse columns (RunConfig.FoundersFollowCorpses): the joules the corpses hold,
        // summed by the snow grid's column, with the richest single corpse of each column and its
        // height, and the fullest column's sum. Summed at the first mouth founder's question after
        // any change to the corpses (CorpsesChanged) and kept until the next one, so a read is
        // always the corpses as they stand. Derived state, never written to a checkpoint: a sum
        // taken again is the same sum, in the list's order.
        private double[] _corpseColumnJoules;
        private double[] _corpseColumnRichest;
        private float[] _corpseColumnHeight;
        private double _corpseColumnsMax;
        private double _corpseColumnsTotal;
        private bool _corpseColumnsFresh;

        /// <summary>
        /// Births and deaths since the last <see cref="DrainLineageEvents"/> — pure
        /// instrumentation for <c>lineage.jsonl</c> (see <see cref="LineageEvent"/>). Swapped out
        /// rather than copied-and-cleared on drain, so a report interval with nothing to report
        /// costs nothing but reading a reference.
        /// </summary>
        private List<LineageEvent> _lineageEvents = new List<LineageEvent>();

        /// <summary>
        /// Genomes admitted since the last <see cref="DrainAdmittedGenomes"/>, filled only while
        /// <see cref="QueueAdmittedGenomes"/> is on. Pure instrumentation, like the lineage queue
        /// beside it, and not state: a checkpoint is written after the farm drains both.
        /// </summary>
        private List<AdmittedGenome> _admittedGenomes = new List<AdmittedGenome>();

        // One log reused across conceptions (Conceive runs on the world's one thread); cleared by
        // every Mutate call that is handed it. The owner's ruling of 2026-09-24, for the lineage row.
        private readonly MutationLog _mutationLog = new MutationLog();

        /// <summary>This step's ledgers, parallel to <c>_living</c>. Reused, never reallocated.</summary>
        private EnergyLedger[] _ledgers = Array.Empty<EnergyLedger>();

        /// <summary>
        /// Each living body's effective lit area for the step, filled before any is contributed
        /// to the light field. Reused rather than reallocated, as <c>_ledgers</c> is.
        /// </summary>
        private float[] _litAreas = Array.Empty<float>();

        /// <summary>
        /// Final rows for absorptive creatures that have died since the last
        /// <see cref="CollectAbsorptiveLog"/> — the one place a dead creature's terminal budget
        /// survives long enough to be written.
        /// </summary>
        /// <remarks>
        /// A queue rather than a write, for <see cref="LineageEvent"/>'s reason: §6.1 forbids
        /// <c>UnityEngine</c> here and nothing in this assembly touches disk. Values, not
        /// references — a reference would either keep a dead body alive or read fields the death
        /// path has already zeroed.
        /// </remarks>
        private readonly List<AbsorptiveSample> _absorptiveDeaths = new List<AbsorptiveSample>();

        /// <summary>Death rows dropped because the buffer was full — see <see cref="AbsorptiveLogRowCap"/>.</summary>
        private int _absorptiveDeathsDropped;

        private long _nextId;
        /// <summary>
        /// Counter behind every per-creature seed. Mixed with <see cref="Seed"/> rather than used
        /// raw — see <see cref="Rng.SeedFor"/> for why consecutive seeds are not independent runs.
        /// </summary>
        private ulong _nextIndex;

        /// <summary>
        /// The index <see cref="_conceptionRng"/>'s seed is drawn at — reserved, and never handed
        /// to <see cref="_nextIndex"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="_nextIndex"/> numbers genomes: a founder, an offspring, an inoculum and a
        /// patch draw each take the next one. The conception-order stream is not a genome and must
        /// not advance that counter, because a world that drew one extra index at construction
        /// would hand every creature after it a different seed and stop replaying the record.
        /// Reserved at the far end of the range rather than at 0, which is a real genome index on
        /// the first step of every run.
        /// </remarks>
        private const ulong ConceptionOrderIndex = ulong.MaxValue - 1UL;

        /// <summary>
        /// The index D077's placement stream is seeded at — reserved beside
        /// <see cref="ConceptionOrderIndex"/> and never handed to <see cref="_nextIndex"/>.
        /// </summary>
        /// <remarks>
        /// Declared here rather than where it is used because this is the register of reserved
        /// indices: the stream itself belongs to <c>Evosim.Sim</c>'s <c>Ecosystem</c>, which owns
        /// the coordinates a placement is drawn in, and two files each picking their own "far end
        /// of the range" is how two streams end up being one. Placement draws from its own stream
        /// for <see cref="ConceptionOrderIndex"/>'s reason: where a body lands must not change
        /// which genome anything else is given.
        /// </remarks>
        public const ulong PlacementIndex = ulong.MaxValue - 2UL;

        /// <summary>
        /// The streams behind D083's two vertex fields — where an emitted vertex lands, which
        /// floor vertex is buried, and every step of the mixing walk. Their own, so a field knob
        /// perturbs no other draw in the world; constructed for every vertex world and drawn from
        /// by nothing in a cell world.
        /// </summary>
        public const ulong DetritusFieldIndex = ulong.MaxValue - 3UL;
        public const ulong MatterFieldIndex = ulong.MaxValue - 4UL;

        /// <summary>
        /// The stream behind <see cref="CurrentMode.Transport"/>'s phases. Its own, so that the
        /// water a seed gets is decided by the seed and by nothing a knob elsewhere perturbs; it
        /// takes no draw at all in a <see cref="CurrentMode.Rolls"/> world, because the field is
        /// never built there.
        /// </summary>
        public const ulong CurrentFieldIndex = ulong.MaxValue - 5UL;

        /// <summary>
        /// The stream behind the sea floor's shape — D092. Its own, so every seed of a round gets
        /// its own floor and one seed's floor is unmoved by any other knob; no draw at all in a
        /// world whose bed is flat, which is every world before round 39.
        /// </summary>
        public const ulong BedShapeIndex = ulong.MaxValue - 6UL;

        /// <summary>
        /// The seed slot of D109's noise map, the one the matter islands and the light's shade
        /// are both drawn from. Built only in a world that asks for it, so every other world's
        /// streams are untouched.
        /// </summary>
        public const ulong IslandMapIndex = ulong.MaxValue - 7UL;

        /// <summary>
        /// The seed slot of D115's founding trickle: how many founders arrive on each step after
        /// the floor has closed. Its own, so the count a step draws moves no genome, no placement
        /// and no conception order; constructed for every world and drawn from by none whose
        /// trickle is off, which is every recorded world.
        /// </summary>
        public const ulong TrickleIndex = ulong.MaxValue - 8UL;

        /// <summary>
        /// The seed slot of round 47's reef placement (<c>logbook/specs/reef-spec.md</c> §1). Its
        /// own, and drawn from only by a world with reefs, so no stream a recorded world draws from
        /// moves; a reef's place is the seed's and nothing else's, as the bed's is.
        /// </summary>
        public const ulong ReefPlacementIndex = ulong.MaxValue - 9UL;

        /// <summary>
        /// D128's seed slot: each body's threshold for dying of age, an exponential draw from this
        /// slot and the body's id. Stateless, so a body's threshold is the same whenever it is
        /// asked and a checkpoint carries nothing for it, and no other stream moves.
        /// </summary>
        public const ulong AgeDeathIndex = ulong.MaxValue - 10UL;

        /// <summary>
        /// The seed slot of the floor's pool share (<see cref="RunConfig.FoundingFloorPoolShare"/>,
        /// the owner's ruling of 2026-10-04): whether a floor founder is a pool copy, and which.
        /// Stateless, as <see cref="AgeDeathIndex"/> is: each attempt's draws come from this slot
        /// and <see cref="FloorSpawns"/> before the attempt, which the checkpoint already carries,
        /// so the state's layout does not move. Drawn from by no world whose share is 0.
        /// </summary>
        public const ulong FloorPoolIndex = ulong.MaxValue - 11UL;

        /// <summary>
        /// The stream behind <see cref="ConceptionOrder.Shuffled"/> — D072. Constructed for every
        /// world and drawn from by none but a shuffled one.
        /// </summary>
        /// <remarks>
        /// Its own stream rather than a share of the per-creature seeds, so that turning the knob
        /// on changes the walk order and nothing else: under <see cref="ConceptionOrder.Age"/> not
        /// a single draw is taken from it, and a default run is step for step what it always was.
        /// </remarks>
        private readonly Rng _conceptionRng;

        /// <summary>
        /// The stream behind D115's founding trickle, seeded at <see cref="TrickleIndex"/>. Built
        /// for every world and drawn from only while <see cref="FoundersStillArriving"/>; the
        /// founders it counts draw their genomes from <c>_nextIndex</c> as a floor founder does.
        /// </summary>
        private readonly Rng _trickleRng;

        /// <summary>
        /// Indices into <c>_living</c>, permuted each step under
        /// <see cref="ConceptionOrder.Shuffled"/>. Reused rather than reallocated, in the manner
        /// of <c>_ledgers</c> — this runs once per metabolic step for the life of a run.
        /// </summary>
        private int[] _conceptionOrder = Array.Empty<int>();

        /// <summary>
        /// Each living body's energy surplus above its breeding gate, filled once per step under
        /// <see cref="ConceptionOrder.Reserve"/> — D073, logbook/0057.
        /// </summary>
        /// <remarks>
        /// Indexed by position in <c>_living</c> rather than by rank, because the comparer reads it
        /// by the index it is handed. Kept and reused for <see cref="_conceptionOrder"/>'s reason.
        /// </remarks>
        private double[] _conceptionSurplus = Array.Empty<double>();

        /// <summary>
        /// <see cref="ConceptionOrder.Reserve"/>'s ordering. Held rather than built each step, in
        /// the manner of everything else this walk touches.
        /// </summary>
        private readonly IComparer<int> _byReserve;

        /// <summary>
        /// Species registry — D057. Founding genome and founding time, keyed by
        /// <see cref="Organism.SpeciesId"/>. Empty for the life of a run whose
        /// <see cref="RunConfig.SpeciesDriftThreshold"/> is 0.
        /// </summary>
        private readonly Dictionary<uint, SpeciesFounder> _species = new Dictionary<uint, SpeciesFounder>();

        /// <summary>
        /// Counter behind every species id, assigned in the same world-step order as everything
        /// else here — D057 — so that <c>(genome, seed, configHash)</c> replays it exactly.
        /// </summary>
        private uint _nextSpeciesId;

        /// <summary>The seed this world was constructed with. Every creature's seed derives from it.</summary>
        public ulong Seed { get; }

        /// <summary>
        /// The seed the landscape was drawn from: the bed, the reefs, the islands and the current
        /// (D192). The <c>landscape</c> entry of <see cref="RunConfig.SeedOverrides"/> when it names one,
        /// otherwise <see cref="Seed"/>.
        /// </summary>
        public ulong LandscapeSeed { get; }

        public RunConfig Config { get; }

        /// <summary>How much light reaches each depth — <see cref="RunConfig.Light"/>.</summary>
        /// <remarks>
        /// Read from the config rather than accepted alongside it. Passing it separately let a
        /// world run at an irradiance its own <c>configHash</c> knew nothing about, which is §7's
        /// exact failure and went unnoticed through the whole §5A.2b sweep (logbook/0013). One
        /// source, so the two cannot disagree.
        /// </remarks>
        public LightModel Light => Config.Light;

        /// <summary>
        /// How this step's light was divided — §5A.2b. Rebuilt every step.
        /// </summary>
        /// <remarks>
        /// The world's carrying capacity lives here rather than in a population number: the sun's
        /// aperture is finite, so <see cref="LightField.IncidentWatts"/> bounds total income no
        /// matter how many creatures there are. Exposed because a sweep wants to report how much
        /// of the incident power the population is actually capturing, which is the honest measure
        /// of how full a world is.
        /// </remarks>
        public LightField Field { get; }

        /// <summary>
        /// D109's map: the gradient noise the matter was seeded by and the light is shaded by, or
        /// null in a world with neither.
        /// </summary>
        public GradientNoise IslandMap => _islandMap;

        private readonly GradientNoise _islandMap;

        /// <summary>The map value above which a column was seeded as an island; 0 with no islands.</summary>
        public float IslandThreshold { get; }

        /// <summary>
        /// D175: where an <see cref="MatterInflux.Islands"/> influx lands — the island seeding's
        /// cells and shares, made from the config and the seed at construction. Null unless that
        /// influx is on.
        /// </summary>
        /// <remarks>
        /// Made by <see cref="GridField.PlanIslands"/> on the same map, cover and depth the
        /// seeding used, and never read from the live field. A restore builds the world from the
        /// same config and seed before it reads the checkpoint, so a resumed run holds the same
        /// plan with nothing written for it (WorldState.cs's "reconstruct, then overwrite").
        /// </remarks>
        public GridField.IslandPlan IslandInflux => _islandInflux;

        private readonly GridField.IslandPlan _islandInflux;

        /// <summary>The shade map's mean over the live columns, 1 with no map. For the header.</summary>
        public float MeanShade { get; } = 1f;

        /// <summary>
        /// The shade map at a column, in (0, 1]: 1 on every island column (the map at or above
        /// <see cref="IslandThreshold"/>) and <c>1 − depth × (threshold − n) / (threshold − min)</c>
        /// below it, so the darkest desert is <c>1 − depth</c>; sampled at an offset that drifts
        /// with the clock when <see cref="RunConfig.LightShadeDriftMetresPerHour"/> is above 0.
        /// 1 with no map.
        /// </summary>
        /// <remarks>
        /// The first cut shaded by the raw map, <c>1 − depth × (1 − n) / 2</c>, and an island
        /// column near the threshold read 0.72 to 0.8: a fifth less light than round 44's, on a
        /// leaf whose surplus over its standing cost is a few percent of its income, and the
        /// founding ran at a third of round 44's pace (scratch/r45-build/runs/bigH). The islands
        /// are the lit water by design, so the map saturates there and shades the deserts only.
        /// </remarks>
        public float ShadeAt(float x, float z)
        {
            if (_islandMap == null || !(Config.LightShadeDepth > 0f)) return 1f;

            float drift = (float)(Config.LightShadeDriftMetresPerHour * ElapsedSeconds / 3600d);
            float offset = drift * 0.70710678f;

            float n = _islandMap.Fbm(x + offset, z + offset);
            float span = IslandThreshold - _islandMapMin;
            float below = span > 0f ? (IslandThreshold - n) / span : 0f;
            if (below < 0f) below = 0f; else if (below > 1f) below = 1f;

            float factor = 1f - Config.LightShadeDepth * below;
            return factor < 0f ? 0f : factor > 1f ? 1f : factor;
        }

        /// <summary>The map's lowest value over the live columns at construction: the darkest desert.</summary>
        private readonly float _islandMapMin;

        /// <summary>
        /// D109's founder rule, or D116's, handed to the placer once there is one. Called before
        /// every founding, every trickle founder and every inoculation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>D109</b>: the column's spent matter over the fullest column's, for every body. The
        /// delegate takes the body since D116 and ignores it here, and the arithmetic is the
        /// arithmetic it was, so a world under D109 asks the placer the same question with the
        /// same answer and draws its stream as the recorded world did.
        /// </para>
        /// <para>
        /// <b>D116</b> (<c>logbook/specs/founding-trickle-spec.md</c> §2): the same share, of the
        /// field the founder's developed adult eats. A body with an absorptive part eats the
        /// snow (<see cref="Nutrients"/>), one with a photosynthetic part takes up the dissolved
        /// matter (<see cref="Matter"/>), a body with both takes the larger of its two shares,
        /// and a body with neither is accepted anywhere. Each share is D109's, empty field
        /// included: a field holding nothing accepts every spot, which is what the snow does
        /// until the first deaths have fed it.
        /// </para>
        /// <para>
        /// <b>D129</b> (<see cref="RunConfig.FoundersFollowCorpses"/>): with it on, a body with a
        /// mouth also takes the corpses' share of its column, and the depth rule sets a body
        /// whose corpse share is its largest just above the richest corpse there. Off, neither
        /// delegate asks anything it did not ask before, so the recorded stream is untouched.
        /// </para>
        /// </remarks>
        private void EnsureFounderAcceptance()
        {
            if (Placement == null) return;
            if (Placement.FounderAcceptance != null) return;

            if (Config.FoundersFollowMatter)
            {
                if (!(Matter is GridField grid)) return;

                Placement.FounderAcceptance = (body, x, z) => ShareOfRichestColumn(grid, x, z);
                return;
            }

            if (Config.FoundersFollowFood)
            {
                // Refused at construction without a grid, so both casts hold here.
                var snow = (GridField)Nutrients;
                var matter = (GridField)Matter;
                bool followCorpses = Config.FoundersFollowCorpses;

                Placement.FounderAcceptance = (body, x, z) =>
                {
                    FoodOf(body, out bool eatsSnow, out bool eatsMatter);
                    bool eatsCorpses = followCorpses && HasMouth(body);

                    if (!eatsSnow && !eatsMatter && !eatsCorpses) return 1f;

                    float share = 0f;
                    if (eatsSnow) share = Math.Max(share, ShareOfRichestColumn(snow, x, z));
                    if (eatsMatter) share = Math.Max(share, ShareOfRichestColumn(matter, x, z));
                    if (eatsCorpses) share = Math.Max(share, CorpseShareOfRichestColumn(snow, x, z));
                    return share;
                };

                // The round 48 founding ruling: the accepted column's richest cell of the same
                // food, the field chosen as the acceptance chose it (a mixotroph takes the larger
                // share, the snow on a tie). Null, and the drawn depth kept, for a body that eats
                // nothing and for a column whose food field holds nothing there. Off, nothing is
                // handed over and the placer takes no draw for it.
                if (Config.FoundersFollowFoodDepth)
                {
                    Placement.FounderDepth = (body, x, z) =>
                    {
                        FoodOf(body, out bool eatsSnow, out bool eatsMatter);
                        bool eatsCorpses = followCorpses && HasMouth(body);

                        if (!eatsSnow && !eatsMatter && !eatsCorpses) return null;

                        // D129: a mouth whose corpse share is the largest of its shares is set
                        // just above the richest corpse in the column; the other foods win a tie,
                        // so a mixotroph in a world with no corpse and no snow keeps the rules
                        // below. A body that eats only corpses always takes this branch.
                        if (eatsCorpses)
                        {
                            float other = 0f;
                            if (eatsSnow) other = Math.Max(other, ShareOfRichestColumn(snow, x, z));
                            if (eatsMatter) other = Math.Max(other, ShareOfRichestColumn(matter, x, z));

                            if (!(eatsSnow || eatsMatter) || CorpseShareOfRichestColumn(snow, x, z) > other)
                            {
                                return CorpseDepth(snow, x, z);
                            }
                        }

                        // The round 50 founding ruling: a leaf where its own income, light and
                        // food together, is largest.
                        if (eatsMatter && Config.FoundersFollowIncomeDepth)
                        {
                            return IncomeDepth(body, snow, matter, x, z);
                        }

                        GridField field = eatsSnow && eatsMatter
                            ? ShareOfRichestColumn(snow, x, z) >= ShareOfRichestColumn(matter, x, z)
                                ? snow
                                : matter
                            : eatsSnow ? snow : matter;

                        int layer = field.RichestLayerInColumn(x, z);
                        if (layer < 0) return null;

                        float cell = field.CellMetres;
                        return (-layer * cell, -(layer + 1) * cell);
                    };
                }
            }
        }

        /// <summary>
        /// Which of the two fields a developed body eats — D116's reading: the snow for an
        /// absorptive part, the dissolved matter for a photosynthetic one, both for a mixotroph.
        /// </summary>
        private static void FoodOf(Phenotype body, out bool eatsSnow, out bool eatsMatter)
        {
            eatsSnow = false;
            eatsMatter = false;

            if (body == null) return;

            IReadOnlyList<PhenotypePart> parts = body.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                string type = parts[i].CellTypeId;
                if (type == CellTypeIds.Absorptive) eatsSnow = true;
                else if (type == CellTypeIds.Photosynthetic) eatsMatter = true;
            }
        }

        /// <summary>
        /// The cell of the column under (x, z) where a body's intake, light and food together, is
        /// largest, as a height span; null when every cell prices at nothing. The round 50
        /// founding ruling (<see cref="RunConfig.FoundersFollowIncomeDepth"/>).
        /// </summary>
        /// <remarks>
        /// The column is walked on the finer of the two grids, shallowest first, and each live
        /// cell is priced at its centre by the call the metabolic pass bills a body with: the
        /// shaded irradiance there, the snow's edible density and the dissolved matter's density,
        /// read through the same field doors. The coarser grid is read at the same point, so a
        /// 1 m cell inside a 5 m matter cell reads that cell's matter and its own light. Priced
        /// without a pose (the orientation average) even under light by exposure, since the body
        /// has none until the solver places it. A read: nothing is moved, and no draw is taken.
        /// </remarks>
        private (float Top, float Bottom)? IncomeDepth(
            Phenotype body, GridField snow, GridField matter, float x, float z)
        {
            GridField fine = snow.CellMetres <= matter.CellMetres ? snow : matter;
            float cell = fine.CellMetres;
            int patch = PatchOfXZ(x, z);

            int best = -1;
            double most = 0d;

            for (int iy = 0; iy < fine.LayerCount; iy++)
            {
                if (!fine.IsLiveInColumn(x, z, iy)) continue;

                float y = -(iy + 0.5f) * cell;
                var at = new FieldPoint(new Float3(x, y, z), patch);

                EnergyLedger ledger = Metabolism.StepAt(
                    body, Config, Field.IrradianceAt(y, patch, x, z),
                    snow.EdibleDensityAt(at), matter.DensityAt(at), 0f, 1f);

                double income = ledger.LightIncome + (double)ledger.FoodIncome;
                if (income > most)
                {
                    most = income;
                    best = iy;
                }
            }

            if (best < 0) return null;
            return (-best * cell, -(best + 1) * cell);
        }

        /// <summary>
        /// The stock in the column under (x, z) over the fullest live column's, or 1 when the
        /// field holds nothing — D109's acceptance, written once for both rules.
        /// </summary>
        private static float ShareOfRichestColumn(GridField grid, float x, float z)
        {
            double max = grid.MaxColumnStock();
            return max > 0d ? (float)(grid.ColumnStockAt(x, z) / max) : 1f;
        }

        /// <summary>
        /// Whether a developed body has a mouth, a part whose <c>Intake</c> is above 0: the test
        /// the mouth's own pass reads a body by (<see cref="Organism.HasIntake"/>), and so D129's
        /// reading of who eats a corpse.
        /// </summary>
        private static bool HasMouth(Phenotype body)
        {
            if (body == null) return false;

            IReadOnlyList<PhenotypePart> parts = body.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Intake > 0f) return true;
            }

            return false;
        }

        /// <summary>
        /// D129: the corpse joules in the column under (x, z) over the fullest column's, or 1 when
        /// no corpse holds anything, which is <see cref="ShareOfRichestColumn"/>'s rule for a
        /// third food.
        /// </summary>
        private float CorpseShareOfRichestColumn(GridField grid, float x, float z)
        {
            SumCorpseColumns(grid);

            double max = _corpseColumnsMax;
            return max > 0d ? (float)(_corpseColumnJoules[grid.ColumnIndexAt(x, z)] / max) : 1f;
        }

        /// <summary>
        /// D129: a span from the height of the richest corpse in the column under (x, z) to the
        /// mouth's reach above it, or null when no corpse there holds anything, and the drawn
        /// depth is kept. The placer then lifts the founder clear of the bed and any reef.
        /// </summary>
        private (float Top, float Bottom)? CorpseDepth(GridField grid, float x, float z)
        {
            SumCorpseColumns(grid);

            int column = grid.ColumnIndexAt(x, z);
            if (!(_corpseColumnRichest[column] > 0d)) return null;

            float y = _corpseColumnHeight[column];
            return (y + Config.IntakeReachMetres, y);
        }

        /// <summary>
        /// Sums the corpses by column, unless nothing has changed since the last sum. The list's
        /// order, so the sum and the richest corpse (the first on a tie) are the same numbers
        /// however many times they are taken.
        /// </summary>
        private void SumCorpseColumns(GridField grid)
        {
            if (_corpseColumnsFresh) return;

            int columns = grid.ColumnCount;
            if (_corpseColumnJoules == null || _corpseColumnJoules.Length != columns)
            {
                _corpseColumnJoules = new double[columns];
                _corpseColumnRichest = new double[columns];
                _corpseColumnHeight = new float[columns];
            }
            else
            {
                Array.Clear(_corpseColumnJoules, 0, columns);
                Array.Clear(_corpseColumnRichest, 0, columns);
                Array.Clear(_corpseColumnHeight, 0, columns);
            }

            double max = 0d, total = 0d;
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse corpse = _corpses[i];
                if (!(corpse.Joules > 0d)) continue;
                total += corpse.Joules;

                Float3 at = corpse.Position;
                int column = grid.ColumnIndexAt(at.X, at.Z);

                double sum = _corpseColumnJoules[column] + corpse.Joules;
                _corpseColumnJoules[column] = sum;
                if (sum > max) max = sum;

                if (corpse.Joules > _corpseColumnRichest[column])
                {
                    _corpseColumnRichest[column] = corpse.Joules;
                    _corpseColumnHeight[column] = at.Y;
                }
            }

            _corpseColumnsMax = max;
            _corpseColumnsTotal = total;
            _corpseColumnsFresh = true;
        }

        /// <summary>
        /// Marks D129's corpse columns stale. Called wherever the corpse list or a corpse's joules
        /// or place changes: the pass that carries and leaks them, a death, a lost part, a mouth's
        /// meal and a restore.
        /// </summary>
        private void CorpsesChanged() => _corpseColumnsFresh = false;

        /// <summary>
        /// Whether the lifespan gene can differ between bodies in this world: it mutates, or the
        /// founders draw it from a range other than 1 (D128). Only then does a birth row carry
        /// <c>ls</c> and a queued row carry the gene through a checkpoint.
        /// </summary>
        internal bool LifespanRecorded =>
            Config.Mutation.LifespanChance > 0f ||
            Config.Genome.MinLifespan != 1f || Config.Genome.MaxLifespan != 1f;

        /// <summary>
        /// Horizontal cells per layer, K ≥ 1 — <see cref="RunConfig.HorizontalPatches"/>, clamped
        /// the same way the D060/D061-era knobs are (cast to int, floored at 1 so a stray
        /// fractional or non-positive config value cannot construct a zero-patch field). D061.
        /// </summary>
        private int PatchCount => Math.Max(1, (int)Config.HorizontalPatches);

        /// <summary>
        /// Patches across z, A ≥ 1 — <see cref="RunConfig.PatchesAcross"/>, clamped the way
        /// <see cref="PatchCount"/> is. fable-propose-box.md; the constructor refuses an A that
        /// does not divide K.
        /// </summary>
        private int PatchesAcross => Math.Max(1, (int)Config.PatchesAcross);

        /// <summary>Patches along x, <c>K / A</c>.</summary>
        private int PatchesAlong => PatchCount / PatchesAcross;

        /// <summary>
        /// The tank's radius, m — <c>sqrt(area/π)</c>, and 0 in a <see cref="WorldShape.Box"/>.
        /// </summary>
        /// <remarks>
        /// <b>Derived once, here, and handed to everything that needs it</b> — the two fields, the
        /// water and (through <c>Ecosystem</c>) the placer and the wall. The alternative is four
        /// square roots of the same area, which is four chances to disagree about where the glass
        /// is; the same discipline that makes <see cref="IMatterField.PatchWidthMetres"/> the one
        /// answer to how wide a patch is. <see cref="TankGeometry"/> carries the arithmetic.
        /// </remarks>
        public float TankRadiusMetres { get; }

        /// <summary>
        /// The box's extent along x and along z, m: <c>W·K/A</c> and <c>W·A</c>, with
        /// <c>W = sqrt(area/K)</c> in float as the fields take it. 0 in a tank.
        /// </summary>
        /// <remarks>
        /// D204's square reads its seams from here: the bed's waves are fitted to join at them and
        /// the contact reads across them. The placer computes the same two products from the
        /// fields' patch width, and the farm holds the two equal.
        /// </remarks>
        public float BoxLengthMetres { get; }

        /// <inheritdoc cref="BoxLengthMetres"/>
        public float BoxWidthMetres { get; }

        /// <summary>
        /// The sea floor's shape, or null when the bed is flat — D092,
        /// <c>logbook/specs/bed-spec.md</c>. Null in every world before round 39.
        /// </summary>
        /// <remarks>
        /// <b>Built once, here, for <see cref="TankRadiusMetres"/>'s reason and more sharply.</b>
        /// The grid masks its cells against this floor, the water is pulled through a map built
        /// from it, the placer puts a body above it and Unity's collider is a mesh of it: four
        /// readers of one function, where two floors built from one seed by two constructors would
        /// be a body standing in rock or water flowing through it. Drawn from
        /// <see cref="BedShapeIndex"/>, so the floor is the seed's and nothing else's.
        /// </remarks>
        public BedShape Bed { get; }

        /// <summary>
        /// The mushroom reefs, or null — every recorded world. <c>logbook/specs/reef-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// Built once, here, after the bed and from <see cref="ReefPlacementIndex"/>, and handed to
        /// every reader of the rock: both grids' masks, the light, the streams, and (through
        /// <c>SolverConfig</c> and the placer) the contacts, the divergence guard and the placement.
        /// One object, for the bed's reason: two rocks built from one seed by two constructors would
        /// be a body standing in stone or water flowing through it.
        /// </remarks>
        public ReefGeometry Reefs { get; }

        /// <summary>
        /// D194's coral, as every reader of the rock reads it, or null in every world at
        /// <see cref="ReefShape.Mushroom"/>. Placed from <see cref="ReefPlacementIndex"/> and built
        /// once, as <see cref="Reefs"/> is and for its reason: one object for every reader.
        /// </summary>
        public CoralRock Coral { get; }

        /// <summary>Dead matter in the water, and what feeds on it — §5A.2c.</summary>
        public IMatterField Nutrients { get; }

        /// <summary>The world's stock of matter, by depth layer — D048.</summary>
        /// <remarks>
        /// <para>
        /// A <see cref="NutrientField"/> by construction because the mechanics are identical —
        /// depth layers, sinking, mixing — and a second copy of that arithmetic is how two things
        /// obliged to agree drift apart. <b>Its unit is matter, not joules.</b> The type's own
        /// vocabulary says joules throughout; here every such quantity is matter, and the two are
        /// never added.
        /// </para>
        /// <para>
        /// <b>Deliberately absent from <see cref="StandingJoules"/>.</b> §5A.2's audit is a hard
        /// equality over energy, and matter is not energy — folding this in would make the books
        /// balance by counting a different substance, which is precisely the failure the audit
        /// exists to catch.
        /// </para>
        /// </remarks>
        public IMatterField Matter { get; }

        /// <summary>
        /// Every unit of matter the world holds, spent and charged — D098. Units.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One substance in two states, so one sum.</b> <see cref="Matter"/> holds the spent
        /// units directly; every joule anywhere else — marine snow, a body's tissue, a body's
        /// reserve, a corpse — is a charged unit, and <see cref="StandingJoules"/> already sums
        /// all of them. So the world's matter is the spent stock plus the standing joules over
        /// <see cref="RunConfig.JoulesPerUnit"/>, and there is no third place for a unit to hide.
        /// </para>
        /// <para>
        /// <b>D206 adds the third place</b>, and only with <see cref="RunConfig.PlantsKeepMatter"/>
        /// on: the spent units a plant or its corpse holds, <see cref="HeldUnits"/>. Off, they are
        /// 0 and the sum is the number it always was.
        /// </para>
        /// <para>
        /// Before D098 this added a body's <c>LockedMatter</c> and a corpse's own matter account
        /// to the field, because matter and energy were separate substances that happened to
        /// travel together. They are not, and the accounts that made them look separate are gone.
        /// </para>
        /// </remarks>
        public double StandingMatterUnits =>
            Config.PlantsKeepMatter
                ? Matter.TotalJoules + StandingJoules / Config.JoulesPerUnit + HeldUnits
                : Matter.TotalJoules + StandingJoules / Config.JoulesPerUnit;

        /// <summary>The same number under the name the reports have always used.</summary>
        public double StandingMatter => StandingMatterUnits;

        /// <summary>Charged units held by the living — tissue and reserve together, in units.</summary>
        /// <remarks>
        /// <b>What the table prints as <c>mat locked</c>.</b> A body is charged matter and nothing
        /// else, so what it "locks" is what it is worth: no separate store, no orphan, no
        /// invariant between two numbers that have to agree. O(n), an instrument for a report row
        /// rather than for the step.
        /// </remarks>
        public double StandingJoulesInBodies
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _living.Count; i++)
                {
                    // The gestation account (2026-09-24) is charged matter the body holds. It is
                    // exactly 0 on every lump breeder, and x + 0 is x, so a world without a
                    // gestating body sums to the same bits it always did.
                    sum += _living[i].Energy + _living[i].TissueJoules + _living[i].GestationJoules;

                    // D232: a swallowed body held in the gut is charged matter the body carries. Exactly 0 with the
                    // switch off, and x + 0 is x.
                    sum += _living[i].GutJoules;
                }
                return sum;
            }
        }

        /// <summary>
        /// How far the matter identity is from closing, in units. Should be ~0 — D098.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A Core property for the first time, and that is half the point.</b> Until D098 this
        /// arithmetic was written out by hand in four places — the harness's report, the theatre's
        /// census, a round reader and about six tests — and four copies of one identity is how
        /// they come to disagree about which leg they include.
        /// </para>
        /// <para>
        /// It is <see cref="AuditResidual"/>'s twin read in the other unit: every leg that books
        /// <c>EnergyOut</c> deposits the same over ρ into the spent field in the same step, and
        /// every leg that books <c>EnergyIn</c> takes the same out of it. Keeping both catches a
        /// leg that did one half and not the other, which is the fault neither book can see alone.
        /// </para>
        /// </remarks>
        public double MatterResidual =>
            StandingMatterUnits - MatterInitialTotal - MatterInfluxedTotal + MatterBuriedTotal;

        /// <summary>
        /// Bodies that have died and are still handing their tissue and matter back to the water,
        /// oldest first. Rule 6 of <c>fable-propose-grid.md</c>. Read-only from outside: a corpse
        /// is founded by <see cref="Bury"/> and emptied by the world's own pass, and nothing else
        /// may move a joule of it without the audit noticing.
        /// </summary>
        /// <remarks>
        /// Empty for the whole life of a run with <see cref="RunConfig.CorpseDecayPerSecond"/> at
        /// 0, which is every run on file: at 0 a death deposits at once and founds nothing.
        /// </remarks>
        public IReadOnlyList<Corpse> Corpses => _corpses;

        /// <summary>
        /// The smell of the corpses, <see cref="SensorChannel.Scent"/>; null unless
        /// <see cref="RunConfig.SenseScent"/>. Refilled after the corpses move at every step and on
        /// every restore, so it is derived and not state.
        /// </summary>
        public ScentField Scent { get; }

        /// <summary>
        /// How many corpses, from the front of <see cref="Corpses"/>, the last fill of
        /// <see cref="Scent"/> covered (<c>logbook/specs/r57-build/scent-restore.md</c>).
        /// </summary>
        /// <remarks>
        /// The farm's growth step runs after the fill and can append a module's remains, so a
        /// checkpoint written at the end of the metabolic step holds corpses the live field has not
        /// smelt yet. Between a fill and a checkpoint the list only grows at its end, so the
        /// checkpoint carries this count and a restore fills from that prefix, and the restored
        /// field is the live one cell for cell. Set where the fill is, in a world with the scent or
        /// without it.
        /// </remarks>
        public int ScentFilledCorpses => _scentFilledCorpses;

        private int _scentFilledCorpses;

        /// <summary>
        /// D167's smell of living leaves; null unless <see cref="RunConfig.LeafScent"/>. Filled
        /// once a metabolic step, beside <see cref="Scent"/>, from the sources
        /// <see cref="GatherLeafSources"/> takes then.
        /// </summary>
        /// <remarks>
        /// <b>Its sources are state, and the field is derived from them.</b> The farm's growth
        /// step runs after the fill and can change a leaf's area or add a body, so a field
        /// refilled from the living at a restore would not be the field the live run reads until
        /// its next fill. The checkpoint carries the sources the last fill took, and the restore
        /// fills from them (the carrion field's rule, <c>logbook/specs/r57-build/scent-restore.md</c>).
        /// </remarks>
        public ScentField LeafScent { get; }

        /// <summary>How many sources the last leaf fill took. Four floats each in the sources.</summary>
        public int LeafSourceCount => _leafSourceCount;

        /// <summary>A copy of the last leaf fill's sources: x, y, z and area, for each.</summary>
        public float[] CopyLeafSources()
        {
            var copy = new float[4 * _leafSourceCount];
            Array.Copy(_leafSources, copy, copy.Length);
            return copy;
        }

        private float[] _leafSources = Array.Empty<float>();
        private int _leafSourceCount;

        /// <summary>
        /// Takes every living body with photosynthetic tissue as a source: its point, and the lit
        /// area of that tissue as the light pass charges it (the silhouette cap and, where light
        /// goes by exposure, each part's exposure included). A body with none adds nothing.
        /// </summary>
        private void GatherLeafSources()
        {
            bool byExposure = Config.LightByExposure;
            bool capOn = Config.LightSilhouetteCap;
            CellTypeRegistry types = Config.CellTypes;

            if (_leafSources.Length < 4 * _living.Count)
            {
                Array.Resize(ref _leafSources, Math.Max(4 * _living.Count, 2 * _leafSources.Length));
            }

            int n = 0;
            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                float area = PhotosyntheticLitArea(creature, types, byExposure, capOn, Config.ContactEating);
                if (!(area > 0f)) continue;

                Float3 p = creature.Point.Position;
                _leafSources[4 * n] = p.X;
                _leafSources[4 * n + 1] = p.Y;
                _leafSources[4 * n + 2] = p.Z;
                _leafSources[4 * n + 3] = area;
                n++;
            }

            _leafSourceCount = n;
        }

        /// <summary>
        /// A body's photosynthetic lit area, m²: what <c>Metabolism</c> hands each light-fixing
        /// part as its lit area, summed over those parts.
        /// </summary>
        /// <param name="byHealth">
        /// D219, contact eating: each part's area scaled by its health, as <see cref="Metabolism"/> bills it.
        /// False, the default and every recorded world, leaves the area as it was.
        /// </param>
        public static float PhotosyntheticLitArea(
            Organism creature, CellTypeRegistry types, bool byExposure, bool capOn, bool byHealth = false)
        {
            Phenotype body = creature.Phenotype;
            float[] exposure = byExposure ? creature.CurrentExposure : null;
            float litFactor = exposure == null
                ? body.LitAreaFactor(capOn)
                : body.LitAreaFactor(exposure, creature.UpInBody, capOn);

            float[] health = byHealth ? creature.PartHealth : null;

            float area = 0f;
            foreach (PhenotypePart part in body.Parts)
            {
                if (!types.Resolve(part.CellTypeId).FixesLight) continue;
                if (health != null)
                {
                    area += Metabolism.LitAreaAtHealth(part, exposure, litFactor, health);
                    continue;
                }

                area += exposure == null
                    ? part.LitArea * litFactor
                    : part.LitArea * exposure[part.Index] * litFactor;
            }

            return area;
        }

        private void FillLeafScent()
        {
            if (LeafScent == null) return;
            GatherLeafSources();
            LeafScent.FillSources(_leafSources, _leafSourceCount);
        }

        /// <summary>
        /// D167's leaf tank: tissue that leaves a body by anything but a bite is burnt as a reserve
        /// is, its joules out and its units into the water's spent stock at the body's point.
        /// </summary>
        private void BurnUnbitten(Organism creature, double joules)
        {
            if (!(joules > 0d)) return;
            EnergyOut += joules;
            double units = joules / Config.JoulesPerUnit;
            Matter.DepositExact(creature.Point, units);
            BurntTotal += units;
        }

        // An engine that fills the scent itself (the card engine, card-next-r57.md Part 7): handed
        // the corpses and the prefix at each point the host would fill, and asked to bring its grid
        // back into Scent before a host reader reads it. Null for every other engine, which leaves
        // the host's fill as it was.
        private Action<IReadOnlyList<Corpse>, int> _scentFillElsewhere;
        private Action<ScentField> _scentCopyBack;

        /// <summary>
        /// Hands the scent's fill to an engine that fills its own grid: from here on the world calls
        /// <paramref name="fill"/> with the corpses and the prefix at every point it would have
        /// filled <see cref="Scent"/>, and once now for the prefix it last filled. <see cref="Scent"/>
        /// keeps its last host fill until <see cref="BringScentHome"/> asks the engine for its grid
        /// through <paramref name="copyBack"/>. The sums and their order are the engine's to keep
        /// equal to the host's.
        /// </summary>
        public void FillScentElsewhere(Action<IReadOnlyList<Corpse>, int> fill, Action<ScentField> copyBack)
        {
            if (Scent == null) return;
            _scentFillElsewhere = fill ?? throw new ArgumentNullException(nameof(fill));
            _scentCopyBack = copyBack ?? throw new ArgumentNullException(nameof(copyBack));
            fill(_corpses, _scentFilledCorpses);
        }

        /// <summary>Whether an engine fills the scent (<see cref="FillScentElsewhere"/>).</summary>
        public bool ScentFilledElsewhere => _scentFillElsewhere != null;

        /// <summary>
        /// Brings the engine's grid into <see cref="Scent"/> before a host reader reads it: the
        /// steering recorder. Nothing to do when the host fills it.
        /// </summary>
        public void BringScentHome() => _scentCopyBack?.Invoke(Scent);

        // The step's fill and the restore's: the host's, or the engine's.
        private void FillScent()
        {
            if (_scentFillElsewhere != null) _scentFillElsewhere(_corpses, _scentFilledCorpses);
            else Scent?.Fill(_corpses, _scentFilledCorpses);
        }

        /// <summary>Tissue still held by the dead, J. A third standing account beside the water and the living.</summary>
        public double CorpseJoules
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _corpses.Count; i++) sum += _corpses[i].Joules;
                return sum;
            }
        }

        /// <summary>
        /// What <see cref="RunConfig.InitialMatterPerCubicMetre"/> seeded the world with at construction —
        /// D074. The whole of <see cref="StandingMatter"/> before anything happened.
        /// </summary>
        /// <remarks>
        /// Recorded rather than recomputed, because the seeding loop is the only moment it can be
        /// read cleanly: one step later the fields have settled and mixed, and while both of those
        /// conserve, reading a "starting stock" off a world that has already run is the kind of
        /// inference the audit exists to make unnecessary.
        /// </remarks>
        public double MatterInitialTotal { get; }

        /// <summary>
        /// Every unit of matter D074's influx has ever added to the world. Cumulative and never
        /// decremented; 0 for the whole life of a run with
        /// <see cref="RunConfig.MatterInfluxPerSecond"/> at 0, which is every run before it.
        /// </summary>
        /// <remarks>
        /// <b>With <see cref="MatterBuriedTotal"/> this is the matter audit.</b>
        /// <c>MatterInitialTotal + MatterInfluxedTotal − MatterBuriedTotal ==
        /// Matter.TotalJoules + MatterInBodies</c> at every step, because everything else matter
        /// does — settling, mixing, advection, remineralisation, conception locking it away and
        /// death giving it back — moves it between cells and bodies without creating or destroying
        /// any. These two counters are the only holes in that wall, which is exactly why they are
        /// counted separately rather than folded into a single net figure: a net number that
        /// happens to be right cannot tell a doubled influx from a doubled burial.
        /// </remarks>
        public double MatterInfluxedTotal { get; private set; }

        /// <summary>
        /// Every unit of matter D074's burial has ever removed from the world. Cumulative; see
        /// <see cref="MatterInfluxedTotal"/> for the identity the two close.
        /// </summary>
        public double MatterBuriedTotal { get; private set; }

        /// <summary>
        /// Every joule a dead body has ever put into <see cref="Nutrients"/> — cumulative and
        /// never decremented; a report reads it as a rate by differencing two samples.
        /// </summary>
        /// <remarks>
        /// Added for the detritus-flux instrument (logbook/0050's closing, the
        /// <c>fable-propose-detritus-flux</c> proposal): round 14's lines ate a stock whose income
        /// could only be inferred from the slope of <see cref="NutrientField.TotalJoules"/> with no
        /// grazer on it. With <see cref="DetritusExudedTotal"/> and
        /// <see cref="DetritusTakenTotal"/> the three are a measurement, and
        /// <c>DetritusDepositedTotal + DetritusExudedTotal - DetritusTakenTotal ==
        /// Nutrients.TotalJoules</c> at every step, because settling, mixing, advection and
        /// remineralisation all conserve.
        /// <para>
        /// With <see cref="RunConfig.CorpseDecayPerSecond"/> above 0 this counts the instalments a
        /// corpse pays and not the death that founded it, which is what keeps that identity true:
        /// crediting the water with a whole body while the corpse still holds it would open the
        /// identity for as long as the corpse lasted.
        /// </para>
        /// </remarks>
        public double DetritusDepositedTotal { get; private set; }

        /// <summary>
        /// Every joule a *living* body has ever released into <see cref="Nutrients"/> — D070's
        /// exudation. Cumulative; see <see cref="DetritusDepositedTotal"/>.
        /// </summary>
        /// <remarks>
        /// <b>Its own counter, not folded into the deposits.</b> D070 exists because dead tissue
        /// alone feeds the second trophic level at about 1% of primary production, and the whole
        /// question the first arm has to answer is how much of the field's income the new route
        /// supplies. One combined counter would show the field's income rise and say nothing about
        /// which half rose. Zero for the whole life of a run with
        /// <see cref="RunConfig.ExudationFraction"/> at 0, which is every run before this one.
        /// </remarks>
        public double DetritusExudedTotal { get; private set; }

        /// <summary>
        /// Every joule feeding has ever taken out of <see cref="Nutrients"/> — its only outflow.
        /// Cumulative; see <see cref="DetritusDepositedTotal"/>.
        /// </summary>
        public double DetritusTakenTotal { get; private set; }

        /// <summary>
        /// Takes that returned less than the ledger asked for, running total. Zero by
        /// construction while availability is frozen for the consumption pass and every
        /// rationed draw is capped at its share; a nonzero count means the allocation has a
        /// hole, and the creature was credited what it got rather than what it planned.
        /// </summary>
        public long PoolShortTakes { get; private set; }

        /// <summary>
        /// Every spent unit burning has ever put back into <see cref="Matter"/> — D098's leg 2.
        /// Cumulative; a report differences two samples to read it as a rate.
        /// </summary>
        /// <remarks>
        /// <b>The world's one outflow of energy and inflow of spent matter, counted once.</b>
        /// Upkeep, neural cost, work, handling and a birth's overhead all arrive here, because
        /// under one substance they are the same event: a charged unit spent and returned to the
        /// water empty. It is <c>EnergyOut</c>'s burning share over
        /// <see cref="RunConfig.JoulesPerUnit"/>, and the table's <c>burnt</c> column.
        /// </remarks>
        public double BurntTotal { get; private set; }

        /// <summary>
        /// Every joule D098's leg 8 has ever moved out of <see cref="Nutrients"/> and into
        /// <see cref="Matter"/> as spent units. Cumulative.
        /// </summary>
        /// <remarks>
        /// The bacteria's heat. It leaves the world as <c>EnergyOut</c> and arrives as
        /// <c>moved / ρ</c> units in the spent field, so it is the one leg that moves matter
        /// between the two states without a body being involved at all.
        /// </remarks>
        public double RemineralisedTotal { get; private set; }

        /// <summary>
        /// Joules D212's clock has spoiled out of the corpses, cumulative: each left as heat
        /// (<see cref="EnergyOut"/>) and went to the spent field as <c>joules / JoulesPerUnit</c> units.
        /// Stays 0 at <see cref="RunConfig.CorpseHalfLifeSeconds"/> 0, and is checkpointed only above it.
        /// </summary>
        public double CorpseSpoiledTotal { get; private set; }

        /// <summary>
        /// Every joule a feeder has ever returned to <see cref="Nutrients"/> as faeces — D098's
        /// leg 3. Cumulative.
        /// </summary>
        /// <remarks>
        /// <b>What <c>EnergyLedger.Wasted</c> used to book as an outflow.</b> A mouth draws
        /// <c>PoolDrawn</c> and keeps <c>FoodIncome</c>; the rest was destroyed before D098 and is
        /// now put back where the feeder is, still charged. The transfer loss that shortens a food
        /// chain is unchanged in size and no longer leaves the world.
        /// </remarks>
        public double DetritusReturnedTotal { get; private set; }

        /// <summary>
        /// Body-steps at which fixation was bound by uptake rather than by light — D098's leg 1.
        /// Cumulative.
        /// </summary>
        /// <remarks>
        /// <b>Read against <see cref="PhotosyntheticSteps"/> and never alone.</b> It is a count of
        /// bodies times steps, so one producer held short for a thousand steps and a thousand
        /// producers held short once read the same — the share is what says whether the water is
        /// stripped, and the share is what the table prints as <c>upt lim</c>.
        /// </remarks>
        public long UptakeLimitedSteps { get; private set; }

        /// <summary>
        /// Body-steps taken by a body with photosynthetic capacity — the denominator of
        /// <see cref="UptakeLimitedSteps"/>. Cumulative.
        /// </summary>
        public long PhotosyntheticSteps { get; private set; }

        /// <summary>
        /// Fixation takes that returned less than the ledger asked for, running total — D098's
        /// leg 1, and <see cref="PoolShortTakes"/>'s twin on the spent field.
        /// </summary>
        /// <remarks>
        /// Zero by construction while the spent field's availability is frozen and every rationed
        /// draw is capped at its share; a nonzero count means the allocation has a hole, and the
        /// producer was credited what the water gave rather than what it planned.
        /// </remarks>
        public long FixationShortTakes { get; private set; }

        /// <summary>
        /// Every joule <see cref="RunConfig.ReserveCapSeconds"/> has ever trimmed off a living
        /// body's reserve and put into <see cref="Nutrients"/>. Cumulative.
        /// </summary>
        /// <remarks>
        /// Counted inside <see cref="DetritusExudedTotal"/> as well as here, because to the
        /// charged field's own flux identity a trim is a living body releasing charged matter and
        /// is indistinguishable from an exudation. Its own counter so that the lever can be read
        /// apart from D070's, which is the reason <see cref="DetritusExudedTotal"/> is separate
        /// from <see cref="DetritusDepositedTotal"/> in the first place. Zero for the whole life
        /// of a run at <see cref="RunConfig.ReserveCapSeconds"/> 0, which is the base round.
        /// </remarks>
        public double ReserveTrimmedTotal { get; private set; }

        /// <summary>
        /// Conceptions refused because a newborn part would have been lighter than
        /// <see cref="RunConfig.MinNewbornPartKilograms"/> — rule 3.
        /// </summary>
        /// <remarks>
        /// Not a stillbirth and not a crowded refusal: the genome is fine, the world has room, and
        /// the parent could pay. What it cannot do is put a body that light into the solver, and
        /// every divergence on record was a newborn. A lineage whose investment over its litter
        /// sits under the floor is childless, which is a selection pressure with a column of its
        /// own so that it cannot be mistaken for the world simply being poor.
        /// </remarks>
        public long ConceptionsUnderMassFloor { get; private set; }

        /// <summary>
        /// Conceptions refused because the parent's reserve would not clear the price and its
        /// genome's <see cref="ReproductionTraits.ReserveMargin"/> together — D098 §3.
        /// </summary>
        /// <remarks>
        /// <b>A count of attempts, not of parents</b>, like
        /// <see cref="ConceptionsUnderMassFloor"/>: a parent held back this step keeps its reserve
        /// and asks again next step, so a few dozen bodies standing at their own margin produce
        /// millions of these over a run, and the number is read against births in the same window
        /// rather than as a level. What it separates is a world too poor to breed from a
        /// population that has evolved to wait — the two look identical in the birth rate and
        /// nowhere else. At margin 0 it counts the plainly insolvent, which is the same refusal
        /// this line has always made and simply never counted.
        /// </remarks>
        public long ConceptionsUnderMargin { get; private set; }

        /// <summary>
        /// Floor draws refused because the founder's body would have been under
        /// <see cref="RunConfig.MinNewbornPartKilograms"/> — rule 3 applied to rule 7.
        /// </summary>
        /// <remarks>
        /// Its own counter rather than a share of <see cref="ConceptionsUnderMassFloor"/>, because
        /// the two say different things about the world. A conception refused is a lineage that
        /// cannot breed; a floor draw refused is the founding lottery rejecting a ticket, and it
        /// counts against <see cref="FloorSpawns"/> rather than against any parent. Reading the
        /// two as one number would hide a founder draw that had become mostly refusals behind a
        /// population that was breeding perfectly well.
        /// </remarks>
        public long FoundersUnderMassFloor { get; private set; }

        /// <summary>
        /// Founders admitted with their start cut by D124's cap
        /// (<see cref="RunConfig.FounderReserveCapFraction"/>), running total. 0 with the cap off,
        /// and a founder already under its cap is not counted.
        /// </summary>
        /// <remarks>
        /// Admitted founders only: a stillborn founder has no start, so it cannot have been cut.
        /// Carried by a checkpoint only in a world whose config turns the cap on, as D117's pool
        /// count is, so every other world writes the bytes it wrote before.
        /// </remarks>
        public long FoundersCapped { get; private set; }

        /// <summary>
        /// The joules D124's cap took from founders' starts, running total: purse and endowment
        /// that were never created. 0 with the cap off. See <see cref="FoundersCapped"/>.
        /// </summary>
        public double FounderJoulesCapped { get; private set; }

        /// <summary>
        /// D241: the tissue joules <see cref="MakeAdult"/> has given, booked as income in both books as a founder's start
        /// is. Zero in any world nothing calls it in.
        /// </summary>
        public double AdultGiftJoules { get; private set; }

        /// <summary>Mean <see cref="Genome.AdultScale"/> over the living — rule 9. NaN when empty.</summary>
        /// <remarks>
        /// <b>The three dials and the body scale, computed on demand rather than tracked.</b> They
        /// are read once per report row and a running mean would have to be maintained on every
        /// birth, death and growth step — four accumulators kept in step with three events, for a
        /// number nothing in the economy reads. NaN rather than 0 on an empty world: 0 is a real
        /// value for none of these, and a reader will average whatever it is given.
        /// </remarks>
        public float MeanAdultScale => MeanOverLiving(c => c.Genome.AdultScale);

        /// <summary>Mean <see cref="ReproductionTraits.BirthInvestment"/> over the living. NaN when empty.</summary>
        public float MeanBirthInvestment => MeanOverLiving(c => c.Genome.Reproduction.BirthInvestment);

        /// <summary>Mean <see cref="ReproductionTraits.BroodSize"/> over the living. NaN when empty.</summary>
        public float MeanBroodSize => MeanOverLiving(c => c.Genome.Reproduction.BroodSize);

        /// <summary>
        /// Mean <see cref="ReproductionTraits.ReserveMargin"/> over the living, in seconds —
        /// D098 §3. NaN when empty.
        /// </summary>
        /// <remarks>
        /// The gene's own column. In seconds rather than joules so that it can be read beside a
        /// lifetime and a drought without a body size in the way: a population whose mean margin
        /// is 400 s is one that has learnt to carry most of a senescence doubling in hand.
        /// </remarks>
        public float MeanReserveMargin => MeanOverLiving(c => c.Genome.Reproduction.ReserveMargin);

        /// <summary>Mean <see cref="Organism.BodyFraction"/> over the living. NaN when empty.</summary>
        /// <remarks>
        /// How grown the population is, which is the reading rule 9 asks for beside the dials: a
        /// world of adults and a world of perpetual juveniles have the same population count and
        /// the same birth rate, and only this tells them apart.
        /// </remarks>
        public float MeanBodyFraction => MeanOverLiving(c => c.BodyFraction);

        private float MeanOverLiving(Func<Organism, float> of)
        {
            if (_living.Count == 0) return float.NaN;

            double sum = 0.0;
            for (int i = 0; i < _living.Count; i++) sum += of(_living[i]);
            return (float)(sum / _living.Count);
        }

        /// <summary>Simulated seconds since the world began.</summary>
        public double ElapsedSeconds { get; private set; }

        public IReadOnlyList<Organism> Living => _living;

        /// <summary>
        /// Who decides whether a body has room to exist — D077. Null in every tiled world, and in
        /// every test that has no coordinates to offer.
        /// </summary>
        /// <remarks>
        /// Read only when <see cref="RunConfig.SharedSpace"/> is on, so a world that has one
        /// attached and the switch off is the world on file, unchanged. See
        /// <see cref="IBodyPlacement"/> for why the world asks rather than knows.
        /// </remarks>
        public IBodyPlacement Placement { get; set; }

        /// <summary>
        /// Births refused for want of room — D077's crowded stillbirth. 0 unless
        /// <see cref="RunConfig.SharedSpace"/> is on.
        /// </summary>
        /// <remarks>
        /// A separate count from <see cref="Stillbirths"/>, which is a development failure: this
        /// one is a world that is full, and the two would be read for completely different things.
        /// The parent keeps its energy and its matter — nothing was built — so a crowded refusal
        /// is a birth that did not happen rather than one that failed, and no lineage row is
        /// written for it.
        /// </remarks>
        public long CrowdedStillbirths { get; private set; }

        /// <summary>Every species ever founded, oldest first by id — D057. See <see cref="SpeciesFounder"/>.</summary>
        public IReadOnlyDictionary<uint, SpeciesFounder> Species => _species;

        /// <summary>Creatures ever created by the floor, and ever born to a parent — D021.</summary>
        public long FloorSpawns { get; private set; }
        public long Births { get; private set; }
        public long Deaths { get; private set; }

        /// <summary>
        /// D128: deaths of age, ever. Cumulative, and a subset of <see cref="Deaths"/>; 0 in a
        /// world whose hazard is off.
        /// </summary>
        public long DeathsByAge { get; internal set; }

        /// <summary>
        /// Children conceived from a gestation account rather than from the reserve — the owner's
        /// ruling of 2026-09-24. Cumulative, and a subset of <see cref="Births"/>; a window is two
        /// rows differenced, and 0 in every world without a gestating lineage.
        /// </summary>
        public long GestationBirths { get; private set; }

        /// <summary>
        /// Joules ever moved from reserves into gestation accounts. Cumulative; an instrument for
        /// reading how much of a gestating lineage's income is banked, and not a leg of either
        /// book (it is a transfer inside one body).
        /// </summary>
        public double GestatedTotal { get; private set; }

        /// <summary>Joules standing in the living's gestation accounts. O(n), for a report row.</summary>
        public double GestationJoulesInBodies
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _living.Count; i++) sum += _living[i].GestationJoules;
                return sum;
            }
        }

        /// <summary>
        /// Creatures killed by <see cref="KillDiverged"/> — bodies the solver blew up. Included in
        /// <see cref="Deaths"/>, and 0 for every healthy run.
        /// </summary>
        /// <remarks>
        /// An instrument, not a demography. A run whose <c>diverged</c> column ever leaves 0 has
        /// had a creature removed by arithmetic rather than by selection, and every trait share
        /// computed after it is missing that lineage for a reason nothing ecological explains. It
        /// is reported per run precisely so that "one body in seventeen hundred, once" and "the
        /// physics is unstable at this step" cannot be mistaken for each other.
        /// </remarks>
        public long Diverged { get; private set; }

        /// <summary>
        /// Creatures ever created by <see cref="Inoculate"/> — D060's invasion assay. Zero for the
        /// life of a run that never calls it.
        /// </summary>
        public long Inoculated { get; private set; }

        /// <summary>
        /// Immigrants admitted by <see cref="Immigrate"/> — D159's exchange. Zero for the life of
        /// a world outside a migrating group.
        /// </summary>
        public long Immigrated { get; private set; }

        /// <summary>
        /// Immigrants <see cref="Immigrate"/> refused: under the newborn mass floor, with no room
        /// to place them, or stillborn. Zero for the life of a world outside a migrating group.
        /// </summary>
        /// <remarks>
        /// Counted here and in none of the founders' refusal counters, so the founding lottery's
        /// columns still read the lottery alone. A stillborn immigrant is in
        /// <see cref="Stillbirths"/> as well, which counts every body development refused.
        /// </remarks>
        public long ImmigrantsRefused { get; private set; }

        /// <summary>
        /// Why the last call to <see cref="Immigrate"/> refused its genome, or
        /// <see cref="ImmigrantRefusal.None"/> when it planted it. Read by the caller straight after
        /// the call, for the record of offers; it is no part of the world's state and moves nothing.
        /// </summary>
        public ImmigrantRefusal LastImmigrantRefusal { get; private set; }

        /// <summary>Simulated seconds since the floor last had to intervene.</summary>
        /// <remarks>
        /// Reported alongside generation depth rather than instead of it. On its own it is nearly
        /// binary; its value is that it dates the moment a world stopped needing us.
        /// </remarks>
        public double SecondsSinceFloorFired { get; private set; }

        /// <summary>Total energy that has entered the world as light, and left as metabolism.</summary>
        /// <remarks>
        /// §5A.2's audit: sun in, metabolism out, everything else conserved. Kept as doubles
        /// because a run accumulates these over millions of steps and a float would stop
        /// registering small additions long before the run ended — which is the failure mode
        /// where an energy audit silently becomes decorative.
        /// </remarks>
        public double EnergyIn { get; private set; }
        public double EnergyOut { get; private set; }

        /// <summary>
        /// Everything the world holds right now: reserves, bodies and detritus, in joules.
        /// </summary>
        /// <remarks>
        /// The middle term of §5A.2's audit, which is a hard equality rather than a plausibility
        /// check: <c>EnergyIn − EnergyOut == Standing</c>, always, to floating-point. Sunlight and
        /// founders are the only sources; metabolism and reproductive overhead the only sinks;
        /// everything else — endowment, tissue, feeding, death — moves energy between the three
        /// accounts below without changing the total. A creature that finds free energy in the
        /// physics or in our arithmetic breaks this and nothing else has to notice it.
        /// </remarks>
        public double StandingJoules
        {
            get
            {
                double sum = Nutrients.TotalJoules;
                for (int i = 0; i < _living.Count; i++)
                {
                    // The gestation account is standing like the reserve: see
                    // StandingJoulesInBodies for why adding it moves no recorded world.
                    sum += _living[i].Energy + _living[i].TissueJoules + _living[i].GestationJoules;

                    // D232: the gut is standing energy like the reserve, from the kill that fills it to the
                    // digestion that empties it into the reserve or the death that sends it to the remains. Exactly 0
                    // with the switch off, so the sum is the number it always was.
                    sum += _living[i].GutJoules;
                }

                // The fourth account, and only ever nonzero where a corpse exists: a body's
                // tissue is standing energy from the death that founds the corpse to the step the
                // last of it is deposited. The loop runs zero times at CorpseDecayPerSecond 0, so
                // this line adds nothing at all to a world on file.
                sum += CorpseJoules;

                // D220: the remains standing hold charged matter too, their tissue and their store, from the death or
                // the cut that founds them to the step that burns them. The list is empty with the dial off, so the
                // loop runs zero times and the sum is the number it always was.
                for (int i = 0; i < _remains.Count; i++)
                {
                    sum += _remains[i].Energy + _remains[i].TissueJoules + _remains[i].GestationJoules;
                }

                return sum;
            }
        }

        /// <summary>How far §5A.2's books are from balancing, in joules. Should be ~0.</summary>
        public double AuditResidual => EnergyIn - EnergyOut - StandingJoules;

        /// <summary>
        /// The living bodies' tissue alone, in joules. <see cref="StandingJoules"/> without the
        /// reserve energy or the detritus account.
        /// </summary>
        /// <remarks>
        /// Read by <see cref="EnforceCeiling"/> against <see cref="RunConfig.MaximumTissueJoules"/>.
        /// Growth made a body count stop measuring biomass (rule 9): a newborn's tissue is a
        /// fraction of its adult target, so this reads what the count ceiling was built to read
        /// and no longer does on its own.
        /// </remarks>
        public double StandingTissueJoules
        {
            get
            {
                double sum = 0d;
                for (int i = 0; i < _living.Count; i++) sum += _living[i].TissueJoules;
                return sum;
            }
        }

        /// <param name="config">The world's settings, every tunable of the run.</param>
        /// <param name="seed">The run's seed, from which every stream the world draws is derived.</param>
        /// <param name="tricklePool">
        /// D117's pool: the genomes a trickle founder may be an exact copy of, in the order the
        /// run directory's <c>pool/NN.json</c> names them. Required, with exactly
        /// <see cref="RunConfig.FoundingTricklePoolCount"/> genomes, when
        /// <see cref="RunConfig.FoundingTricklePoolShare"/> or
        /// <see cref="RunConfig.FoundingFloorPoolShare"/> is above 0; may be null or empty
        /// otherwise. Validated once, here (<see cref="ValidateTricklePool"/>).
        /// </param>
        /// <param name="tricklePoolWeights">
        /// The pool's weights, one for each genome in the same order, from the farm's list file
        /// (<c>pool/index.tsv</c>); null, every recorded world, picks a pool genome uniformly.
        /// Each finite and above 0. Validated once, here.
        /// </param>
        public World(
            RunConfig config, ulong seed = 1, IReadOnlyList<Genome> tricklePool = null,
            IReadOnlyList<double> tricklePoolWeights = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));

            if (config.Light == null)
            {
                throw new ArgumentException(
                    "RunConfig.Light is null, so the world has no primary energy input and " +
                    "nothing in it can live.", nameof(config));
            }

            ValidateSeasons(config.Light);
            ValidateFounding(config);
            ValidateAgeing(config);
            ValidateGrowthClimb(config);
            _tricklePool = ValidateTricklePool(config, tricklePool);
            _tricklePoolWeights = ValidateTricklePoolWeights(_tricklePool, tricklePoolWeights, out _tricklePoolCumulative);

            // D061: PatchCount reads Config.HorizontalPatches, so it is valid from this point on
            // (Config was just assigned above) and every field below is built with the same K.
            int patchCount = PatchCount;

            // fable-propose-box.md. The layout, and the three worlds it cannot describe. A is
            // read once here so that every field, the placer and the current below are built with
            // one answer, the way K already is.
            int patchesAcross = PatchesAcross;

            if (patchCount % patchesAcross != 0)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"PatchesAcross is {patchesAcross} and HorizontalPatches is {patchCount}, ") +
                    FormattableString.Invariant(
                        $"which leaves {patchCount % patchesAcross} patches over. A layout is a ") +
                    "whole number of rows: four patches lie one by four or two by two, and nine " +
                    "lie one by nine or three by three.",
                    nameof(config));
            }

            if (patchesAcross > 1 && config.FieldModel == MatterField.Cells)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"PatchesAcross is {patchesAcross} and FieldModel is Cells. The cell ") +
                    "field mixes and advects across a one-dimensional ring of patches, wrapping " +
                    "from K−1 back to 0, which is not the geometry of a layout more than one " +
                    "patch deep: patch 0 and patch K−1 are not neighbours in it. Run a grid or a " +
                    "vertex field, or leave the box a row.",
                    nameof(config));
            }

            // fable-propose-aquarium.md ruling 1, logbook/specs/tank-spec.md. The tank is a third
            // container beside the box rather than a rewrite of it, and these are the four worlds
            // it cannot describe. Each is refused rather than quietly reinterpreted, for the
            // reason every refusal in this constructor exists: a world that ran one geometry under
            // a config naming another would be filed under settings it did not have.
            if (config.WorldShape == WorldShape.Tank)
            {
                if (config.FieldModel == MatterField.Cells)
                {
                    throw new ArgumentException(
                        "WorldShape is Tank and FieldModel is Cells. The cell field mixes and " +
                        "advects along a one-dimensional ring of patches, wrapping from K−1 back " +
                        "to 0; a tank's patches are concentric rings, where patch 0 is the axis " +
                        "and patch K−1 is against the glass and the two are as far apart as the " +
                        "world goes. Run a grid.",
                        nameof(config));
                }

                // Not on the spec's list, and refused for the list's own reason. The vertex field
                // holds positions in a box, wraps them on two rings and knows nothing about a
                // mask; a tank built on it would place its quanta over the bounding square, walk
                // them through the glass and read its patches as a row. That is the failure the
                // Cells refusal above exists to stop, arriving through the other door. The build
                // is grid-only (logbook/specs/tank-spec.md), so nothing is lost by saying so.
                if (config.FieldModel == MatterField.Vertices)
                {
                    throw new ArgumentException(
                        "WorldShape is Tank and FieldModel is Vertices. The vertex field is a " +
                        "set of positions in a periodic box: it wraps a quantum at a seam the " +
                        "tank does not have, has no mask to keep one out of the glass, and reads " +
                        "its patches as a row rather than as rings. Run a grid.",
                        nameof(config));
                }

                if (patchesAcross > 1)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"WorldShape is Tank and PatchesAcross is {patchesAcross}. A layout ") +
                        "is the box's: the tank's patches are rings of equal area about one axis, " +
                        "so there are no rows to lay them in. Leave PatchesAcross at 1.",
                        nameof(config));
                }

                if (config.DispersalChancePerStep > 0f)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"WorldShape is Tank and DispersalChancePerStep is ") +
                        FormattableString.Invariant($"{config.DispersalChancePerStep}. ") +
                        "D061's lottery walks one patch ahead or one behind modulo K, which on a " +
                        "set of rings is a creature teleporting between the axis and the glass. " +
                        "The tank moves a body by carrying it.",
                        nameof(config));
                }

                if (config.Current != null &&
                    config.Current.Mode == CurrentMode.Rolls && config.Current.Speed > 0f)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"WorldShape is Tank and the current is Rolls at {config.Current.Speed} m/s. ") +
                        "The rolls are a field over the box's row of patches — patch k rising " +
                        "while k+1 sinks — and a tank has no such row. Its water is the streams, " +
                        "which is selected by the shape: set CurrentMode.Transport to say so, or " +
                        "the speed to 0 for still water.",
                        nameof(config));
                }

                if (config.Current != null &&
                    config.Current.Mode == CurrentMode.Ocean && config.Current.Speed > 0f)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"WorldShape is Tank and the current is Ocean at {config.Current.Speed} m/s. ") +
                        "The open ocean's current is the water of a box that joins at both seams " +
                        "(D204), and a tank's water is the streams: set CurrentMode.Transport to " +
                        "say so, or the speed to 0 for still water.",
                        nameof(config));
                }
            }

            if (patchesAcross > 1 && config.DispersalChancePerStep > 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"PatchesAcross is {patchesAcross} and DispersalChancePerStep is ") +
                    FormattableString.Invariant($"{config.DispersalChancePerStep}. ") +
                    "D061's lottery walks the same ring the cell field does — one patch ahead or " +
                    "one behind, modulo K — and on a layout that is not a ring it would move a " +
                    "creature diagonally across the box as often as sideways.",
                    nameof(config));
            }

            // After the refusals and before anything is built with it, so that one radius reaches
            // the fields, the water and the placer — see TankRadiusMetres.
            TankRadiusMetres = config.WorldShape == WorldShape.Tank
                ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres)
                : 0f;

            if (config.WorldShape != WorldShape.Tank)
            {
                int count = Math.Max(1, (int)config.HorizontalPatches);
                int across = Math.Min(Math.Max(1, (int)config.PatchesAcross), count);
                float patch = (float)Math.Sqrt(config.WorldAreaSquareMetres / count);
                BoxLengthMetres = patch * Math.Max(1, count / across);
                BoxWidthMetres = patch * across;
            }

            // D092, logbook/specs/bed-spec.md, and D204. The box's bed has waves that join at both
            // seams (BedShape.ForBox) and no tilt, since a plane cannot join. Refused rather than
            // ignored, for this constructor's usual reason: a run that recorded a tilt and ran a
            // level floor would be filed under settings it did not have.
            if (config.WorldShape != WorldShape.Tank && config.BedTiltMetres > 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"WorldShape is {config.WorldShape} and the bed asks for a tilt of {config.BedTiltMetres} m. ") +
                    "The box is periodic on both horizontal axes, and a tilt is a plane, which cannot " +
                    "meet itself at the seams (D204). Its relief joins; leave the tilt at 0.",
                    nameof(config));
            }

            ValidateShore(config);

            // D192. The landscape's own seed: the bed, the reefs, the islands and the current draw
            // from it, and everything alive from the world's. Unnamed in RunConfig.SeedOverrides,
            // it is the world's seed, which is every recorded world.
            ulong landscape = config.TrySeedOverride("landscape", out ulong named) ? named : seed;
            LandscapeSeed = landscape;

            // Its own refusals — a negative or non-finite dial, a floor that would reach the
            // surface with the shore off, and a shore without its fade — are BedShape's, stated at
            // the numbers rather than at the config. The beach is logbook/specs/beach-spec.md.
            Bed = config.WorldShape == WorldShape.Tank &&
                  (config.BedReliefMetres > 0f || config.BedTiltMetres > 0f)
                ? new BedShape(
                    TankRadiusMetres, config.WorldDepthMetres, config.BedReliefMetres,
                    config.BedTiltMetres, config.BedScaleMetres, Rng.SeedFor(landscape, BedShapeIndex),
                    config.BedShoreDepthMetres, config.BedShoreFadeMetres)
                : config.WorldShape != WorldShape.Tank && config.BedReliefMetres > 0f
                    ? BedShape.ForBox(
                        BoxLengthMetres, BoxWidthMetres, config.WorldDepthMetres, config.BedReliefMetres,
                        config.BedScaleMetres, Rng.SeedFor(landscape, BedShapeIndex),
                        config.BedShoreDepthMetres, config.BedShoreFadeMetres)
                    : null;

            // Round 47's reefs (logbook/specs/reef-spec.md, redesigned 2026-09-23 night). Refused
            // in full — a reef in a box, a cap through the surface, a cap no tank can hold — before
            // a single draw, and drawn from their own stream to the cover only when there is one:
            // at ReefCover 0 this is one pass over the dials' defaults and a null.
            ReefGeometry.Refuse(config, TankRadiusMetres);

            // Round 62's coral (logbook/specs/coral-build.md). At ReefShape Mushroom, every recorded
            // world, this is one pass over the coral dials' defaults, draws nothing and leaves Coral
            // null. Otherwise the coral is grown from the reef stream, which the mushrooms then leave
            // alone (a coral world has ReefCover 0), and read through one CoralRock by every reader of
            // the rock, as the reefs are.
            CoralFootprint coralFootprint = config.WorldShape == WorldShape.Tank
                ? CoralFootprint.Tank(TankRadiusMetres)
                : CoralFootprint.Box(BoxLengthMetres, BoxWidthMetres);
            CoralGeometry coral = CoralGeometry.PlaceFromLandscape(config, coralFootprint, Bed, landscape);
            Coral = coral != null && coral.Count > 0 ? new CoralRock(coral, coralFootprint, config) : null;

            Reefs = config.ReefCover > 0f
                ? ReefGeometry.Place(config, TankRadiusMetres, Bed, Rng.SeedFor(landscape, ReefPlacementIndex))
                : null;

            // D111. A negative price is a part paid to float off its centre, which is a free
            // energy source; a non-finite one is an arithmetic hole in every bill.
            if (float.IsNaN(config.BuoyancyOffsetWattsPerCubicMetre) ||
                float.IsInfinity(config.BuoyancyOffsetWattsPerCubicMetre) ||
                config.BuoyancyOffsetWattsPerCubicMetre < 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"BuoyancyOffsetWattsPerCubicMetre is {config.BuoyancyOffsetWattsPerCubicMetre}. ") +
                    "It is a price, finite and at or above 0; 0 turns the offset off.",
                    nameof(config));
            }

            // D113, on the same argument: a negative support price pays a body for reach.
            if (float.IsNaN(config.SupportWattsPerSquareMetrePerSquareMetre) ||
                float.IsInfinity(config.SupportWattsPerSquareMetrePerSquareMetre) ||
                config.SupportWattsPerSquareMetrePerSquareMetre < 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"SupportWattsPerSquareMetrePerSquareMetre is {config.SupportWattsPerSquareMetrePerSquareMetre}. ") +
                    "It is a price, finite and at or above 0; 0 turns the support cost off.",
                    nameof(config));
            }

            ValidateVent(config, patchCount);
            ValidateMatterInflux(config, patchCount);

            Field = new LightField(
                Light, config.WorldAreaSquareMetres, config.LightLayerMetres,
                patchCount, config.PerPatchShading > 0f);

            // The caps' shadow, on the column each covers (LightField.Reefs' remarks for why not
            // through the pooled canopy). Null, and untouched, in every recorded world.
            Field.Reefs = Reefs;
            Field.Coral = Coral;

            if (config.FieldModel == MatterField.Vertices)
            {
                // D083. A vertex is somewhere, so the bodies must be too: the tiled world has no
                // horizontal coordinates for a body to feed at, and defaulting them would put
                // every creature at its patch's centre and call that a position.
                if (!config.SharedSpace)
                {
                    throw new ArgumentException(
                        "FieldModel is Vertices but SharedSpace is false. The vertex field reads " +
                        "a body's position, which only the shared volume has.",
                        nameof(config));
                }

                Nutrients = new VertexField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.NutrientSinkMetresPerSecond, config.WorldDepthMetres,
                    config.FloorRefugeMetres, config.RefugeEdibleFraction, patchCount,
                    config.FieldKernelMetres, config.FieldMergeMetres, config.FieldVertexCap,
                    config.FieldVertexJoules, Rng.SeedFor(seed, DetritusFieldIndex), patchesAcross);

                // Its own reach: the cell's volume, so the matter gate binds where it bound in
                // the base world (RunConfig.FieldMatterKernelMetres).
                Matter = new VertexField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.MatterSinkMetresPerSecond, config.WorldDepthMetres,
                    0f, 0f, patchCount,
                    config.FieldMatterKernelMetres, config.FieldMergeMetres, config.FieldVertexCap,
                    config.FieldVertexJoules, Rng.SeedFor(seed, MatterFieldIndex), patchesAcross);
            }
            else if (config.FieldModel == MatterField.Grid)
            {
                // fable-propose-grid.md, and D083's reason unchanged: a cell of the grid is a
                // place, so the bodies must have places. The tiled world gives a body a depth and
                // a patch index and nothing else, and defaulting the rest would put every creature
                // at its patch's centre and call that a position.
                if (!config.SharedSpace)
                {
                    throw new ArgumentException(
                        "FieldModel is Grid but SharedSpace is false. The grid field reads a " +
                        "body's position, which only the shared volume has.",
                        nameof(config));
                }

                // D084 said the sideways rate equals the vertical one because the walk is the
                // same in every direction, and a cubic cell has no axis to prefer. The trap this
                // refusal closes is a provenance one rather than a physical one: GridField would
                // accept a horizontal rate of 0 and then stir sideways at its vertical rate
                // anyway, so a run header could record h-mix 0 for a world that mixes sideways at
                // 0.02. Refusing here keeps the header's h-mix token equal to what the detritus
                // actually does on every axis.
                if (config.HorizontalMixingDiffusivity != config.NutrientMixingDiffusivity)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FieldModel is Grid, but HorizontalMixingDiffusivity is ") +
                        FormattableString.Invariant(
                            $"{config.HorizontalMixingDiffusivity} m2/s against a vertical ") +
                        FormattableString.Invariant(
                            $"NutrientMixingDiffusivity of {config.NutrientMixingDiffusivity} m2/s. ") +
                        "A grid's cells are cubes and D084 ruled the two axes equal, so the two " +
                        "knobs must agree or the run header would name a sideways rate the world " +
                        "does not run.",
                        nameof(config));
                }

                Nutrients = new GridField(
                    config.WorldAreaSquareMetres, config.NutrientSinkMetresPerSecond,
                    config.WorldDepthMetres, config.FloorRefugeMetres, config.RefugeEdibleFraction,
                    patchCount, config.FieldCellMetres, patchesAcross,
                    config.WorldShape, TankRadiusMetres, Bed, Reefs, Coral);

                // Its own, coarser cell: matter is drawn in whole conceptions rather than grazed,
                // and a metre of water cannot afford a child (RunConfig.FieldMatterCellMetres).
                // The same floor, on its own coarser cells: the two grids mask against one
                // BedShape, so the matter and the detritus stop at the same rock (D092).
                Matter = new GridField(
                    config.WorldAreaSquareMetres, config.MatterSinkMetresPerSecond,
                    config.WorldDepthMetres, 0f, 0f, patchCount, config.FieldMatterCellMetres,
                    patchesAcross, config.WorldShape, TankRadiusMetres, Bed, Reefs, Coral);
            }
            else
            {
                Nutrients = new NutrientField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.NutrientSinkMetresPerSecond, config.WorldDepthMetres,
                    config.FloorRefugeMetres, config.RefugeEdibleFraction, patchCount);

                // No refuge: nobody grazes matter, it is drawn at conception rather than eaten — D055.
                Matter = new NutrientField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.MatterSinkMetresPerSecond, config.WorldDepthMetres,
                    refugeMetres: 0f, refugeEdibleFraction: 0f, patchCount: patchCount);
            }

            if (config.SenseScent)
            {
                // D204: round the seams when the world reads across them.
                Scent = new ScentField(Nutrients.LengthMetres, Nutrients.WidthMetres,
                    config.WorldDepthMetres, config.ScentRangeMetres, ScentField.CellMetres, config.ReadAcrossSeams);
            }

            if (config.LeafScent != LeafScentForm.None)
            {
                LeafScent = new ScentField(Nutrients.LengthMetres, Nutrients.WidthMetres,
                    config.WorldDepthMetres, config.LeafScentRangeMetres, config.LeafScentCellMetres,
                    config.ReadAcrossSeams);
            }

            // Seeded uniformly — across every patch as well as every layer, D061 — and never
            // created again. Everything after this is redistribution: reproduction takes it out
            // of a cell, death puts it back into one. Deposit's 3-arg (patch-explicit) overload
            // is called directly rather than the pre-D061 one, so this loop needs no guard of its
            // own: it is correct at K=1 (one patch, same total deposited as before D061 existed)
            // and at K>1 alike.
            // fable-propose-aquarium.md ruling 2. At a budget of 0 this is
            // InitialMatterPerCubicMetre unchanged, which is every run on file; above 0 the
            // density is whatever spreads the budget over the water that exists, so the area dial
            // and the matter budget come apart. The divisor is the live volume rather than the
            // box's, which in a tank on a grid is the cells the mask calls live: a budget that
            // included the dry corners would seed less than it says.
            float seedDensity = config.MatterBudgetUnits > 0f
                ? (float)(config.MatterBudgetUnits / LiveVolumeOf(Matter, config))
                : config.InitialMatterPerCubicMetre;

            // D185. The margin keeps islands off the glass, so without islands it has nothing to do.
            if (config.MatterIslandGlassMarginMetres > 0f && !(config.MatterIslandWavelengthMetres > 0f))
            {
                throw new ArgumentException(
                    "A glass margin for the islands (MatterIslandGlassMarginMetres > 0) needs islands " +
                    "(MatterIslandWavelengthMetres > 0).",
                    nameof(config));
            }

            if (Matter is VertexField matterVertices)
            {
                // D083. A lattice holding the same total the cells would, spaced for one quantum
                // per vertex; nothing here when the density is 0, exactly like the cells.
                matterVertices.SeedUniform(seedDensity);
            }
            else if (Matter is GridField matterGrid)
            {
                if (config.MatterIslandWavelengthMetres > 0f)
                {
                    // D109. The budget placed as islands by the seed's own map; a density rule
                    // has no total to place, so the islands need the budget.
                    if (!(config.MatterBudgetUnits > 0f))
                    {
                        throw new ArgumentException(
                            "Matter islands (MatterIslandWavelengthMetres > 0) need a matter budget " +
                            "(MatterBudgetUnits > 0): the density rule has no total to place in them.",
                            nameof(config));
                    }

                    _islandMap = new GradientNoise(
                        Rng.SeedFor(landscape, IslandMapIndex), config.MatterIslandWavelengthMetres);

                    IslandThreshold = matterGrid.SeedIslands(
                        config.MatterBudgetUnits, _islandMap.Fbm, config.MatterIslandCover,
                        config.MatterIslandDepthMetres, config.MatterIslandGlassMarginMetres);
                }
                else
                {
                    // The same total again, one share per cell. The grid's cells tile the box exactly
                    // (GridField refuses a cell size that does not), so this is the cells' own seed
                    // read at a finer scale and not an approximation of it.
                    matterGrid.SeedUniform(seedDensity);
                }
            }
            else if (config.MatterIslandWavelengthMetres > 0f)
            {
                throw new ArgumentException(
                    "Matter islands (MatterIslandWavelengthMetres > 0) need a grid field " +
                    "(FieldModel Grid): the vertex and cell fields have no columns to seed by.",
                    nameof(config));
            }

            else if (seedDensity > 0f)
            {
                float perCell = seedDensity * Matter.LayerVolume;
                for (int i = 0; i < Matter.LayerCount; i++)
                {
                    float depth = -((i + 0.5f) * Matter.LayerMetres);
                    for (int patch = 0; patch < patchCount; patch++)
                    {
                        // The cell field's own signature: this branch is the cells' alone.
                        ((NutrientField)Matter).Deposit(depth, perCell, patch);
                    }
                }
            }

            // D175. The islands' influx lands where the islands were seeded, in the seeding's
            // shares: the plan is made again from the same map, cover and depth, and nothing of the
            // stock just laid is read. ValidateMatterInflux has refused a world with no grid or no
            // islands; a map that put no column above its threshold is refused here, since the
            // seeding fell back to uniform water and there is no island to feed.
            if (config.MatterInfluxPerSecond > 0f && config.MatterInfluxAt == MatterInflux.Islands)
            {
                _islandInflux = ((GridField)Matter).PlanIslands(
                    _islandMap.Fbm, config.MatterIslandCover, config.MatterIslandDepthMetres,
                    config.MatterIslandGlassMarginMetres);

                if (_islandInflux == null || !(_islandInflux.WeightSum > 0d))
                {
                    throw new ArgumentException(
                        "MatterInfluxAt is Islands, but this seed's island map put no live column above " +
                        "its threshold, so the matter was seeded as uniform water and there is no island " +
                        "for the influx to land on.",
                        nameof(config));
                }
            }

            // D190. The desert is the columns outside the influx's plan, so a desert rate needs one.
            if (config.MatterBurialDesertPerSecond >= 0f && _islandInflux == null)
            {
                throw new ArgumentException(
                    "MatterBurialDesertPerSecond is " + config.MatterBurialDesertPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ", but the desert is the water outside the islands' influx, and this world has none " +
                    "(it needs MatterInfluxAt Islands and MatterInfluxPerSecond > 0). Set it below 0 to bury " +
                    "every column at MatterBurialPerSecond.",
                    nameof(config));
            }

            if (config.LightShadeDepth > 0f)
            {
                // D109's second half: the same map shades the light, column by column, and the
                // pooled canopy receives the map's mean. Refused without the islands rather than
                // given a map of its own, so that one seed has one landscape.
                if (_islandMap == null)
                {
                    throw new ArgumentException(
                        "A light shade map (LightShadeDepth > 0) takes its noise from the matter " +
                        "islands, so it needs MatterIslandWavelengthMetres > 0.",
                        nameof(config));
                }

                var matterGridForMean = (GridField)Matter;

                // The darkest desert first, since the shade at a column is read against it.
                float min = float.MaxValue;
                for (int ix = 0; ix < matterGridForMean.CellsX; ix++)
                {
                    float cx = (ix + 0.5f) * matterGridForMean.CellMetres;
                    for (int iz = 0; iz < matterGridForMean.CellsZ; iz++)
                    {
                        if (!matterGridForMean.ColumnIsLive(ix, iz)) continue;
                        float n = _islandMap.Fbm(cx, (iz + 0.5f) * matterGridForMean.CellMetres);
                        if (n < min) min = n;
                    }
                }
                _islandMapMin = min < float.MaxValue ? min : 0f;

                double sum = 0d;
                int columns = 0;
                for (int ix = 0; ix < matterGridForMean.CellsX; ix++)
                {
                    float cx = (ix + 0.5f) * matterGridForMean.CellMetres;
                    for (int iz = 0; iz < matterGridForMean.CellsZ; iz++)
                    {
                        float cz = (iz + 0.5f) * matterGridForMean.CellMetres;
                        if (!matterGridForMean.ColumnIsLive(ix, iz)) continue;
                        sum += ShadeAt(cx, cz);
                        columns++;
                    }
                }

                MeanShade = columns > 0 ? (float)(sum / columns) : 1f;
                Field.MeanColumnFactor = MeanShade;
                Field.ColumnFactor = ShadeAt;
            }
            else if (config.LightShadeDriftMetresPerHour > 0f)
            {
                throw new ArgumentException(
                    "A shade drift (LightShadeDriftMetresPerHour > 0) needs a shade map (LightShadeDepth > 0).",
                    nameof(config));
            }

            // D074. The stock the matter identity is measured against, read here because here is
            // the only place it is unambiguous — see MatterInitialTotal. Before D074 this number
            // was StandingMatter for the whole life of the run.
            MatterInitialTotal = Matter.TotalJoules;

            // D067. The vent's legs are defined by a volume flux and need no width, but the drag a
            // creature feels in one is a velocity and does. The field is told once, here, from the
            // same geometry the fields themselves were built with — sqrt(area / K) — so a world
            // cannot end up with two patch widths that disagree.
            // D086's grid and the transport field want the whole box, not only the width: the
            // length is the width times the patch count and the floor is at minus the depth, and
            // the seed is what makes one round's five seeds five draws of the water as well as of
            // the genome. Same geometry the fields were built with, for the same reason.
            // And the shape, which is what selects the streams: the tank's water is not the rolls or
            // the transport field, and the field has to be told the container rather than inferring
            // one from a mode (fable-propose-aquarium.md ruling 1).
            config.Current?.SetBox(
                Nutrients.PatchWidthMetres, patchCount, config.WorldDepthMetres,
                Rng.SeedFor(landscape, CurrentFieldIndex), patchesAcross,
                config.WorldShape, TankRadiusMetres, Bed);

            // The reefs' fade on the streams (logbook/specs/reef-spec.md §2), after SetBox, which
            // clears it: a current handed to a second world must not keep the first world's rock.
            config.Current?.SetReefs(Reefs);
            config.Current?.SetCoral(Coral);

            Seed = seed;

            // Built for every world, drawn from only by a shuffled one — see ConceptionOrderIndex.
            // Constructing an Rng takes no draw from anything else, so an Age world is unchanged
            // by its existence.
            _conceptionRng = new Rng(Rng.SeedFor(seed, ConceptionOrderIndex));

            // D115, on the same terms: an object for every world, a draw for none whose trickle
            // is off, so a recorded world's streams are untouched by its existence.
            _trickleRng = new Rng(Rng.SeedFor(seed, TrickleIndex));

            // Likewise for every world and used by none but a Reserve one — an object, not a draw.
            _byReserve = new ReserveComparer(this);
        }

        /// <summary>
        /// Descending energy surplus, ties by list index ascending — <see cref="ConceptionOrder.Reserve"/>'s
        /// order (D073, logbook/0057).
        /// </summary>
        /// <remarks>
        /// The fallback to the index is not tidiness. It makes the comparison a *total* order over
        /// distinct indices, and a total order has exactly one sorted arrangement whichever sort
        /// produced it — where two equal surpluses left to tie would be arranged by whatever
        /// <see cref="Array.Sort{T}(T[], int, int, IComparer{T})"/>'s introsort happens to do this
        /// runtime. That is the same hazard <see cref="Rng"/> exists for: an algorithm nobody
        /// promised not to change, standing between a seed and a run.
        /// </remarks>
        private sealed class ReserveComparer : IComparer<int>
        {
            private readonly World _world;

            public ReserveComparer(World world) => _world = world;

            public int Compare(int a, int b)
            {
                int bySurplus = _world._conceptionSurplus[b].CompareTo(_world._conceptionSurplus[a]);
                return bySurplus != 0 ? bySurplus : a.CompareTo(b);
            }
        }

        /// <summary>
        /// The beach's refusals that need the rest of the config — <c>logbook/specs/beach-spec.md</c>
        /// §2. BedShape refuses what it can see at its own numbers (a shore at or past the depth, a
        /// shore without a fade and a fade without a shore); these are the ones only the world can.
        /// </summary>
        /// <remarks>
        /// All skipped with both dials at 0, which is every recorded config: a world that never
        /// asked for a shore cannot be refused because of one.
        /// </remarks>
        private static void ValidateShore(RunConfig config)
        {
            float shore = config.BedShoreDepthMetres;
            float fade = config.BedShoreFadeMetres;
            if (!(shore > 0f) && !(fade > 0f)) return;

            // The bed is the tank's, and a shore is a clamp on the bed.
            if (config.WorldShape != WorldShape.Tank)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"WorldShape is {config.WorldShape} and the bed asks for a shore at {shore} m ") +
                    FormattableString.Invariant($"with a fade of {fade} m. The shore is the tank's ") +
                    "bed raised to the surface on one side, and the box has no bed to raise. " +
                    "logbook/specs/beach-spec.md.",
                    nameof(config));
            }

            // A shore on a floor with no relief and no tilt would clamp nothing: the header would
            // name a beach the world does not have.
            if (!(config.BedReliefMetres > 0f) && !(config.BedTiltMetres > 0f))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The bed asks for a shore at {shore} m on a flat floor (relief and tilt 0), ") +
                    "where it would clamp nothing. Raise the tilt until the plane reaches the " +
                    "surface, or leave the shore at 0. logbook/specs/beach-spec.md.",
                    nameof(config));
            }

            // A shoal thinner than the detritus cell has no live cell over it: the mask keeps a
            // cell whose centre is above the floor, and a one-metre shoal under two-metre cells
            // would be rock to the grid and water to the physics.
            if (config.FieldModel == MatterField.Grid && shore > 0f && shore < config.FieldCellMetres)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The shoal is {shore} m deep and the detritus cell is {config.FieldCellMetres} m. ") +
                    "A shoal thinner than a cell has no live cell over it, so the snow could not " +
                    "settle on it and the water over it would not be in the grid. " +
                    "logbook/specs/beach-spec.md.",
                    nameof(config));
            }
        }

        /// <summary>
        /// The three things about a vent that only the world can check — D067.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A <see cref="CurrentField"/> is handed heights and a patch index and nothing else</b>,
        /// so it cannot discover where the floor is, how many patches there are or how thick a
        /// layer is. Each of those is load-bearing for the vent: a plume that stops above the floor
        /// is the trapdoor D067 exists to close, an out-of-range vent patch would silently be some
        /// other patch, and a leg that is not a whole number of layers makes the flux across a face
        /// a fraction of a cell that does not exist. So the config states them and the world refuses
        /// to be built when they disagree, rather than running a different experiment from the one
        /// the file names.
        /// </para>
        /// <para>
        /// <b>All three are skipped entirely while the vent is off</b>, which is every run before
        /// D067: the defaults are then simply unread, and a config that never asked for a vent
        /// cannot be refused because of one.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Refuses the pools D117 cannot describe, and validates each pool genome once, as
        /// <see cref="Inoculate"/> would (the genome's own invariants against this world's
        /// registry, and D111's offset in a world that does not price it). Returns the pool the
        /// world keeps: a copy of the list, empty when none was handed.
        /// </summary>
        /// <remarks>
        /// Nothing is asked of a world with no pool named and a share of 0, which is every
        /// recorded world. The share's range, the count's and the hash's shape are the setters';
        /// these are the facts about two numbers, or about a number and the genomes handed in.
        /// </remarks>
        private static IReadOnlyList<Genome> ValidateTricklePool(
            RunConfig config, IReadOnlyList<Genome> pool)
        {
            float share = config.FoundingTricklePoolShare;
            int count = config.FoundingTricklePoolCount;
            bool hashed = !string.IsNullOrEmpty(config.FoundingTricklePoolHash);
            int handed = pool?.Count ?? 0;

            if (share > 0f)
            {
                if (count == 0 || !hashed)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingTricklePoolShare is {share} and the pool is not named ") +
                        FormattableString.Invariant(
                            $"(FoundingTricklePoolCount {count}, FoundingTricklePoolHash ") +
                        (hashed ? "set" : "empty") + "). A share of the trickle's founders drawn " +
                        "from a pool needs the pool's size and the hash that pins its bytes (D117); " +
                        "name the pool with EVOSIM_TRICKLE_POOL, or set the share to 0.",
                        nameof(config));
                }

                if (!(config.FoundingTricklePerSecond > 0f))
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingTricklePoolShare is {share} and FoundingTricklePerSecond is 0. ") +
                        "The pool is a share of the trickle's founders (D117), and a world with " +
                        "no trickle would never draw one; a header naming a pool that never " +
                        "admits anyone would describe a world the run did not have.",
                        nameof(config));
                }

                if (handed != count)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingTricklePoolShare is {share} and the config names a pool of ") +
                        FormattableString.Invariant(
                            $"{count} genomes, but the world was handed {handed}. The pool the ") +
                        "world draws from has to be the pool the config pins (D117).",
                        nameof(config));
                }
            }

            // The floor's share (the owner's ruling of 2026-10-04) draws from the same pool, so it
            // is refused on the same facts, with a floor that can found in place of a trickle that
            // runs.
            float floorShare = config.FoundingFloorPoolShare;
            if (floorShare > 0f)
            {
                if (count == 0 || !hashed)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingFloorPoolShare is {floorShare} and the pool is not named ") +
                        FormattableString.Invariant(
                            $"(FoundingTricklePoolCount {count}, FoundingTricklePoolHash ") +
                        (hashed ? "set" : "empty") + "). A share of the floor's founders drawn " +
                        "from the pool needs the pool's size and the hash that pins its bytes; " +
                        "name the pool with EVOSIM_TRICKLE_POOL, or set the share to 0.",
                        nameof(config));
                }

                if (config.MinimumPopulation <= 0)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingFloorPoolShare is {floorShare} and MinimumPopulation is ") +
                        FormattableString.Invariant($"{config.MinimumPopulation}, so the floor never ") +
                        "founds anyone and a header naming its pool share would describe a world " +
                        "the run did not have.",
                        nameof(config));
                }

                if (handed != count)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingFloorPoolShare is {floorShare} and the config names a pool of ") +
                        FormattableString.Invariant(
                            $"{count} genomes, but the world was handed {handed}. The pool the ") +
                        "world draws from has to be the pool the config pins (D117).",
                        nameof(config));
                }
            }

            if (count > 0 && !hashed)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"FoundingTricklePoolCount is {count} and FoundingTricklePoolHash is empty. ") +
                    "A pool named without the hash of its bytes is a pool configHash cannot pin " +
                    "(D117).",
                    nameof(config));
            }

            if (count == 0 && hashed)
            {
                throw new ArgumentException(
                    "FoundingTricklePoolHash is set and FoundingTricklePoolCount is 0. A hash of " +
                    "no pool names nothing (D117); clear the hash or set the count.",
                    nameof(config));
            }

            if (handed > 0 && handed != count)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The world was handed a pool of {handed} genomes and the config names ") +
                    FormattableString.Invariant($"{count}. The pool the world holds has to be ") +
                    "the pool the config pins (D117).",
                    nameof(config));
            }

            if (handed == 0) return Array.Empty<Genome>();

            bool priced = config.BuoyancyOffsetWattsPerCubicMetre > 0f;
            bool central = config.Brain != null && config.Brain.BrainModel == BrainModel.Central;
            var kept = new Genome[handed];

            for (int i = 0; i < handed; i++)
            {
                Genome genome = pool[i] ?? throw new ArgumentException(
                    FormattableString.Invariant($"Trickle pool genome {i:00} is null (D117)."),
                    nameof(pool));

                // Inoculate's two questions, asked once: the genome's own invariants against
                // this world's registry, and D111's refusal of an offset nothing prices. A pool
                // genome is named deliberately, as an inoculant is, so a body the world cannot
                // admit is a mistake in the launcher and not a founder to count as refused.
                IReadOnlyList<string> issues = genome.Validate(config.CellTypes, priced, config.Shapes);
                if (issues.Count > 0)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"Trickle pool genome {i:00} (pool/{i:00}.json) is not a body this ") +
                        "world can admit (D117; an offset in a world that does not price it is " +
                        "D111's refusal): " + string.Join(" ", issues),
                        nameof(pool));
                }

                // The owner's ruling of 2026-10-04: a pool founder runs the world's brain. A Central
                // world converts an old per-node genome at arrival (SpawnPoolFounder); the reverse
                // has no conversion, and a central founder in a per-node world would breed a
                // central lineage the config does not describe. Unlike Inoculate, which runs either
                // brain in either world because an assay names its body by hand.
                if (genome.Brain != null && !central)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"Trickle pool genome {i:00} (pool/{i:00}.json) carries a central brain ") +
                        "and the world's BrainModel is PerNode. A pool founder " +
                        "runs the world's brain, and a central body has no per-node neurons to fall " +
                        "back to; run the world Central (EVOSIM_BRAIN_MODEL central), or name a " +
                        "per-node genome.",
                        nameof(pool));
                }

                kept[i] = genome;
            }

            return kept;
        }

        /// <summary>
        /// Refuses weights the pool cannot carry and returns the ones the world keeps, with their
        /// running sums for the pick (<see cref="WeightedPoolIndex"/>): null and null when none
        /// were handed, which is every recorded world.
        /// </summary>
        private static IReadOnlyList<double> ValidateTricklePoolWeights(
            IReadOnlyList<Genome> pool, IReadOnlyList<double> weights, out double[] cumulative)
        {
            cumulative = null;
            if (weights == null) return null;

            if (weights.Count != pool.Count)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The world was handed {weights.Count} trickle pool weights and a pool of ") +
                    FormattableString.Invariant($"{pool.Count} genomes. A weight belongs to one ") +
                    "pool genome, in the pool's order, and a pool with no genomes has none.",
                    nameof(weights));
            }

            var kept = new double[weights.Count];
            var sums = new double[weights.Count];
            double total = 0d;

            for (int i = 0; i < weights.Count; i++)
            {
                double w = weights[i];
                if (!(w > 0d) || double.IsInfinity(w))
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"Trickle pool weight {i:00} is {w}; a weight is finite and above 0. ") +
                        "A genome the trickle should never draw is left out of the pool, not " +
                        "weighted 0.",
                        nameof(weights));
                }

                kept[i] = w;
                total += w;
                sums[i] = total;
            }

            if (double.IsInfinity(total))
            {
                throw new ArgumentException(
                    "The trickle pool's weights sum past the largest double. Scale them down; only " +
                    "their ratios matter.",
                    nameof(weights));
            }

            cumulative = sums;
            return kept;
        }

        /// <summary>
        /// Refuses a day cycle and a season cycle whose product does not average to 1: the cross
        /// term vanishes only when the season is a whole number (at least 2) of days long, and
        /// under the two-season shape (D151) an even number, so that each half holds whole days.
        /// Refuses too a two-season ramp of half a season or more, and weather without a day cycle.
        /// </summary>
        private static void ValidateSeasons(LightModel light)
        {
            bool twoSeason = light.SeasonShape == SeasonShape.TwoSeason;

            // Half a season is a quarter of the year. A longer ramp would leave less than half of
            // each season at its own level, and at half the year the two ramps would overlap.
            if (twoSeason && !(light.SeasonRampSeconds < 0.25 * light.SeasonLengthSeconds))
            {
                throw new ArgumentException(
                    "A two-season ramp must be shorter than half a season (a quarter of " +
                    "SeasonLengthSeconds); got SeasonRampSeconds " + light.SeasonRampSeconds +
                    " s in a year of " + light.SeasonLengthSeconds + " s.",
                    "config");
            }

            if (light.WeatherSpread > 0f && !(light.DayNightAmplitude > 0f))
            {
                throw new ArgumentException(
                    "Weather is one factor a day, blended across each midnight, so it needs a day " +
                    "cycle; got WeatherSpread " + light.WeatherSpread + " with DayNightAmplitude " +
                    light.DayNightAmplitude + ".",
                    "config");
            }

            if (!(light.SeasonAmplitude > 0f) || !(light.DayNightAmplitude > 0f)) return;

            double ratio = (double)light.SeasonLengthSeconds / light.DayLengthSeconds;
            double nearest = Math.Round(ratio);
            if (nearest < 2d || Math.Abs(ratio - nearest) > 1e-9 * nearest)
            {
                throw new ArgumentException(
                    "A day cycle and a season cycle together preserve the light's mean only when " +
                    "SeasonLengthSeconds is a whole number (at least 2) of DayLengthSeconds; got " +
                    light.SeasonLengthSeconds + " s over " + light.DayLengthSeconds + " s, a ratio of " +
                    ratio.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ".",
                    "config");
            }

            if (twoSeason && Math.IEEERemainder(nearest, 2d) != 0d)
            {
                throw new ArgumentException(
                    "A two-season year with a day cycle must be an even number of days, so that " +
                    "summer and winter each hold whole days and switch at a midnight; got " +
                    light.SeasonLengthSeconds + " s over " + light.DayLengthSeconds + " s, " +
                    nearest.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " days.",
                    "config");
            }
        }

        /// <summary>
        /// Refuses the founding worlds D115 and D116 cannot describe
        /// (<c>logbook/specs/founding-trickle-spec.md</c> §§1–2), before the first step.
        /// </summary>
        /// <remarks>
        /// Nothing is asked of a world with both rules off, which is every recorded world. The
        /// trickle's own bound (at most one founder a metabolic step on average) is the setter's,
        /// because it is a fact about the one number; these are facts about two.
        /// </remarks>
        private static void ValidateAgeing(RunConfig config)
        {
            bool hazard = config.AgeHazardBaseSeconds > 0f;

            if (config.AgeHazardBaseSeconds < 0f || float.IsNaN(config.AgeHazardBaseSeconds) ||
                float.IsInfinity(config.AgeHazardBaseSeconds))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"AgeHazardBaseSeconds is {config.AgeHazardBaseSeconds}; it is a median age ") +
                    "at death in seconds, finite and at least 0 (0 is no death by age).",
                    nameof(config));
            }

            if (hazard && config.SenescenceDoublingSeconds > 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"AgeHazardBaseSeconds is {config.AgeHazardBaseSeconds} and ") +
                    FormattableString.Invariant(
                        $"SenescenceDoublingSeconds is {config.SenescenceDoublingSeconds}. ") +
                    "D128 turns D038's wear off while the hazard is on: they are two accounts of " +
                    "ageing, and the wear burns the reserve an old body's corpse is meant to keep.",
                    nameof(config));
            }

            if (hazard && !(config.AgeHazardDoublingFraction > 0f &&
                            !float.IsInfinity(config.AgeHazardDoublingFraction)))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"AgeHazardDoublingFraction is {config.AgeHazardDoublingFraction}; the ") +
                    "hazard doubles over this share of a body's base, so it is finite and above 0.",
                    nameof(config));
            }

            if (config.AgeingPerBirth < 0f || config.AgeingPerWound < 0f ||
                float.IsNaN(config.AgeingPerBirth) || float.IsNaN(config.AgeingPerWound))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"AgeingPerBirth is {config.AgeingPerBirth} and AgeingPerWound ") +
                    FormattableString.Invariant($"{config.AgeingPerWound}; ") +
                    "each is how far an event pushes the ageing clock ahead, at least 0.",
                    nameof(config));
            }

            if (!hazard && (config.AgeingPerBirth > 0f || config.AgeingPerWound > 0f ||
                            config.AgeRepairExponent != 0f))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"AgeingPerBirth is {config.AgeingPerBirth}, AgeingPerWound ") +
                    FormattableString.Invariant(
                        $"{config.AgeingPerWound} and AgeRepairExponent {config.AgeRepairExponent} ") +
                    "with AgeHazardBaseSeconds at 0. Each of them works on the ageing hazard " +
                    "(D128), and a header naming a rule the world does not run would describe " +
                    "a world the run did not have.",
                    nameof(config));
            }
        }

        /// <summary>
        /// D241: the growth climb's two refusals. T is a time to adulthood, finite and at least 0 (0
        /// is today's growth); with T above 0 the gene's range has to hold its start, which is how
        /// <see cref="Admit"/> judges every genome that carries the gene.
        /// </summary>
        private static void ValidateGrowthClimb(RunConfig config)
        {
            float t = config.GrowthAdultSeconds;
            if (t < 0f || float.IsNaN(t) || float.IsInfinity(t))
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"GrowthAdultSeconds is {t}; it is the time to ") +
                    "adulthood in world seconds, finite and at least 0 (0 is growth without the " +
                    "climb, D241).",
                    nameof(config));
            }

            if (t == 0f) return;

            float min = config.GrowthReserveMinSeconds;
            float start = config.GrowthReserveStartSeconds;
            float max = config.GrowthReserveMaxSeconds;
            if (!(min > 0f && min <= start && start <= max) || float.IsInfinity(max))
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"GrowthAdultSeconds is {t} and the growth reserve's ") +
                    FormattableString.Invariant($"range is [{min}, {max}] with a start of {start}. ") +
                    "The gene steps on its logarithm inside a finite range that holds its start, so " +
                    "0 < GrowthReserveMinSeconds <= GrowthReserveStartSeconds <= " +
                    "GrowthReserveMaxSeconds (D241).",
                    nameof(config));
            }
        }

        private static void ValidateFounding(RunConfig config)
        {
            if (config.FoundingTricklePerSecond > 0f)
            {
                if (config.FloorSpawnsPerStep <= 0)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingTricklePerSecond is {config.FoundingTricklePerSecond} and ") +
                        "FloorSpawnsPerStep is 0. The trickle spawns through the floor's own path " +
                        "(D115), and a world that turned the floor's spawning off has not said it " +
                        "wants founders at all.",
                        nameof(config));
                }

                if (!(config.FloorClosesAfterSeconds > 0f))
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FoundingTricklePerSecond is {config.FoundingTricklePerSecond} and ") +
                        "FloorClosesAfterSeconds is 0, so the floor never closes and the trickle, " +
                        "which runs only after it has (D115), would never add anyone. Close the " +
                        "floor, or set the trickle to 0; a header naming a trickle that never runs " +
                        "would describe a world the run did not have.",
                        nameof(config));
                }
            }

            if (config.FoundersFollowFood)
            {
                if (config.FoundersFollowMatter)
                {
                    throw new ArgumentException(
                        "FoundersFollowFood and FoundersFollowMatter are both on. They are one rule " +
                        "asked two ways (D109 reads the dissolved matter for every founder, D116 " +
                        "the field its body eats), and a world runs one or the other.",
                        nameof(config));
                }

                if (config.FieldModel != MatterField.Grid)
                {
                    throw new ArgumentException(
                        "FoundersFollowFood is on and FieldModel is " + config.FieldModel + ". The " +
                        "rule reads the stock in the column under a spot, and only the grid field " +
                        "has columns; run a grid, or turn the rule off.",
                        nameof(config));
                }
            }

            // The round 48 ruling's depth refines D116's column, so it has no column to refine
            // without it; refused rather than ignored, for the trickle's reason above.
            if (config.FoundersFollowFoodDepth && !config.FoundersFollowFood)
            {
                throw new ArgumentException(
                    "FoundersFollowFoodDepth is on and FoundersFollowFood is off. The depth rule " +
                    "sets a founder at the richest cell of its food in the column D116 accepted it " +
                    "in, so it needs D116's rule on; a header naming it would describe a world the " +
                    "run did not have.",
                    nameof(config));
            }

            // Round 50's income depth replaces the round 48 depth for a leaf, so it has nothing
            // to replace without it; refused rather than ignored, for the same reason.
            if (config.FoundersFollowIncomeDepth && !config.FoundersFollowFoodDepth)
            {
                throw new ArgumentException(
                    "FoundersFollowIncomeDepth is on and FoundersFollowFoodDepth is off. The income " +
                    "rule sets a leaf founder at the cell of its accepted column where its own intake " +
                    "is largest, in place of the round 48 depth rule, so it needs that rule on; a " +
                    "header naming it would describe a world the run did not have.",
                    nameof(config));
            }

            // D131's settled rate needs corpses that exist and settle; with either off no corpse
            // would ever read it, and a header naming it would describe a world the run did not have.
            if (config.SenseScent && !(config.CorpseDecayPerSecond > 0f))
            {
                throw new ArgumentException(
                    "SenseScent is on and corpses are not objects (CorpseDecayPerSecond 0), so there " +
                    "is nothing to smell.", nameof(config));
            }

            // D146: the one brain is free by the owner's ruling that thinking be free for now, and the
            // neural price is charged on node neurons, which a central genome has none of. A price set
            // here would be charged to nobody, so it is refused rather than ignored.
            if (config.Brain != null && config.Brain.BrainModel == BrainModel.Central &&
                (config.NeuralCostPerNeuronWatts > 0f || config.NeuralCostPerConnectionWatts > 0f))
            {
                throw new ArgumentException(
                    "BrainModel is Central and the neural price is above 0 (NeuralCostPerNeuronWatts " +
                    "or NeuralCostPerConnectionWatts). The price reads node neurons, which the one brain " +
                    "does not have, so it would charge nothing; set both to 0.", nameof(config));
            }

            // The drive sense reads the bend-and-power drive's envelope; a world without it has none.
            if (config.SenseDrive && !(config.Fluid != null && config.Fluid.PowerPushRadiansPerSecond > 0f))
            {
                throw new ArgumentException(
                    "SenseDrive is on and the world does not run the bend-and-power drive " +
                    "(Fluid.PowerPushRadiansPerSecond 0), so there is no envelope to sense.", nameof(config));
            }

            // D229, D247: the body-and-store cap is s seconds of standing cost above the litter and the growth, so at
            // s of 0 it would read as a cap that is not there; and the rule that draws its numbers is the dial's.
            if (config.ReserveCapForm == ReserveCapForm.BodyAndStore && !(config.ReserveCapSeconds > 0f))
            {
                throw new ArgumentException(
                    "ReserveCapForm is BodyAndStore and ReserveCapSeconds is 0. Under the body-and-store form s is the " +
                    "seconds of standing cost above the litter and the growth, and 0 means no cap under either form; a " +
                    "header naming the form would describe a cap the world did not have (D247).",
                    nameof(config));
            }

            // D231: a stroke pushes its welded assembly by the fin law's strips, so it needs the fin law; and only
            // the power push has a fixed stroke direction to take them along, so the swing push is refused.
            if (config.Fluid != null && config.Fluid.StrokePushesAssembly)
            {
                if (!config.Fluid.ShapedAddedMass)
                {
                    throw new ArgumentException(
                        "StrokePushesAssembly is on and ShapedAddedMass is off. D231 integrates D229's plate " +
                        "formula in strips along the assembly, so it needs the fin law (EVOSIM_SHAPED_ADDED_MASS).",
                        nameof(config));
                }

                if (config.Fluid.LimbPushCoefficient > 0f && !(config.Fluid.PowerPushRadiansPerSecond > 0f))
                {
                    throw new ArgumentException(
                        "StrokePushesAssembly is on and the limb push is the swing push (LimbPushCoefficient above 0 " +
                        "without the bend-and-power drive, PowerPushRadiansPerSecond 0). The swing's direction changes " +
                        "every step and D231's strips are taken along a fixed stroke, so only the power push is built.",
                        nameof(config));
                }
            }

            // D162: the grip forms on each part's own contact sphere, as the touch is read.
            if (config.CellTypes.Contains(CellTypeIds.Anchoring) && !config.ContactPerPart)
            {
                throw new ArgumentException(
                    "The cell-type table holds the anchoring cell and ContactPerPart is off. The grip " +
                    "forms on the touch of each part's own contact sphere, which only per-part contact " +
                    "builds (D162).", nameof(config));
            }

            // D228: the true shape is the per-part contact's narrowphase; the body sphere has no parts to test.
            if (config.ContactShape != ContactShapeForm.Spheres && !config.ContactPerPart)
            {
                throw new ArgumentException(
                    "ContactShape is " + config.ContactShape + " and ContactPerPart is off. The true shape is " +
                    "the narrowphase of per-part contact (D228); a body sphere has no parts to test.",
                    nameof(config));
            }

            // The touch of the rock is read on each part's own contact sphere (D114).
            if (config.SenseBedTouch && !config.ContactPerPart)
            {
                throw new ArgumentException(
                    "SenseBedTouch is on and ContactPerPart is off. The touch of the rock is read on " +
                    "each part's own contact sphere, which only per-part contact builds.", nameof(config));
            }

            // D207 amends D206's form; without D206 no body holds anything for its leaves to take first.
            if (config.PlantsKeepOwnFirst && !config.PlantsKeepMatter)
            {
                throw new ArgumentException(
                    "PlantsKeepOwnFirst is on and PlantsKeepMatter is off. D207 amends D206's form: without " +
                    "D206 no body holds its spent matter, so there is nothing for its own leaves to take first.",
                    nameof(config));
            }

            // D204: the seams a contact reads across are the box's; a tank has glass and no seam.
            if (config.ReadAcrossSeams && config.WorldShape == WorldShape.Tank)
            {
                throw new ArgumentException(
                    "ReadAcrossSeams is on in a tank. The tank's boundary is its glass and it has no " +
                    "seam to touch across; the setting belongs to the box (D204).", nameof(config));
            }

            if (config.CorpseSettledDecayPerSecond > 0f &&
                !(config.CorpseDecayPerSecond > 0f && config.CorpseSettles))
            {
                throw new ArgumentException(
                    "CorpseSettledDecayPerSecond is above 0 and corpses do not settle " +
                    "(CorpseDecayPerSecond 0 or CorpseSettles off), so no corpse would ever decay at it.",
                    nameof(config));
            }

            if (config.CorpseHalfLifeSeconds > 0f && !(config.CorpseDecayPerSecond > 0f))
            {
                throw new ArgumentException(
                    "CorpseHalfLifeSeconds is above 0 and CorpseDecayPerSecond is 0. At 0 a death " +
                    "deposits at once and no corpse ever lies anywhere, so there is no food to spoil (D212).",
                    nameof(config));
            }

            if (config.IntakeReachFromMouth && !(config.IntakeReachMetres > 0f))
            {
                throw new ArgumentException(
                    "IntakeReachFromMouth is on and IntakeReachMetres is 0. At 0 no mouth eats from a " +
                    "corpse, so there is no reach to measure from a mouth point (D214).",
                    nameof(config));
            }

            if (config.BiteTakesStore && !config.ContactEating)
            {
                throw new ArgumentException(
                    "BiteTakesStore is on and ContactEating is off. A bite takes the store only where bites take " +
                    "tissue as they wound, so there is no bite for it to read; a header naming it would describe " +
                    "a world the run did not have (D224).",
                    nameof(config));
            }

            if (!(config.BiteDamageScale > 0f) || float.IsInfinity(config.BiteDamageScale))
            {
                throw new ArgumentException(
                    "BiteDamageScale is " + config.BiteDamageScale + ". It multiplies the damage of a mouth's blow, so it " +
                    "is finite and above 0 (D226).",
                    nameof(config));
            }

            if (config.BiteDamageScale != 1f && !config.ContactEating)
            {
                throw new ArgumentException(
                    "BiteDamageScale is not 1 and ContactEating is off. The scale deepens the wound of a bite that " +
                    "feeds, so without contact eating there is nothing for it to read; a header naming it would " +
                    "describe a world the run did not have (D226).",
                    nameof(config));
            }

            if (config.KillSwallowsBody && !config.ContactEating)
            {
                throw new ArgumentException(
                    "KillSwallowsBody is on and ContactEating is off. A kill is swallowed only where bites feed as " +
                    "they wound and a body that dies leaves remains for the swallow to replace, so there is no kill " +
                    "for it to read; a header naming it would describe a world the run did not have (D232).",
                    nameof(config));
            }

            // D129 adds the corpses to D116's lottery as a mouth's food, so it has no lottery to
            // join without D116, no corpse to read unless corpses are objects, and no mouth that
            // could eat one without a reach. Each refused rather than ignored, for the same reason.
            if (config.FoundersFollowCorpses)
            {
                if (!config.FoundersFollowFood)
                {
                    throw new ArgumentException(
                        "FoundersFollowCorpses is on and FoundersFollowFood is off. D129 reads the " +
                        "corpses as a mouth's food in D116's founder lottery, so it needs that rule " +
                        "on; a header naming it would describe a world the run did not have.",
                        nameof(config));
                }

                if (!(config.CorpseDecayPerSecond > 0f))
                {
                    throw new ArgumentException(
                        "FoundersFollowCorpses is on and CorpseDecayPerSecond is 0. At 0 a death " +
                        "deposits at once and no corpse ever lies anywhere, so there is nothing for a " +
                        "mouth founder to follow.",
                        nameof(config));
                }

                if (!(config.IntakeReachMetres > 0f))
                {
                    throw new ArgumentException(
                        "FoundersFollowCorpses is on and IntakeReachMetres is 0. At 0 no mouth eats " +
                        "from a corpse, so setting mouth founders beside the corpses would feed nobody.",
                        nameof(config));
                }
            }
        }

        private static void ValidateVent(RunConfig config, int patchCount)
        {
            CurrentField current = config.Current;
            if (current == null || !(current.VentSpeed > 0f)) return;

            if (current.VentPatch >= patchCount)
            {
                throw new ArgumentException(
                    $"Current.VentPatch is {current.VentPatch} but there are only {patchCount} " +
                    "patches, so the plume would rise in a patch this world does not have. It is " +
                    "validated rather than wrapped, because a vent that quietly relocates when " +
                    "HorizontalPatches changes makes two configs name the same world.",
                    nameof(config));
            }

            if (current.VentDepthMetres != config.WorldDepthMetres)
            {
                throw new ArgumentException(
                    $"Current.VentDepthMetres is {current.VentDepthMetres} m and WorldDepthMetres " +
                    $"is {config.WorldDepthMetres} m. The vent draws from the floor and the field " +
                    "cannot see where the floor is, so the two have to be stated to agree — a " +
                    "plume that stops short of the bottom is the trapdoor D067 exists to close.",
                    nameof(config));
            }

            double layers = current.VentLegMetres / (double)config.LightLayerMetres;
            if (!(layers >= 1d) || Math.Abs(layers - Math.Round(layers)) > 1e-6d)
            {
                throw new ArgumentException(
                    $"Current.VentLegMetres is {current.VentLegMetres} m, which is not a whole " +
                    $"number of {config.LightLayerMetres} m layers. Discrete continuity holds only " +
                    "when a leg is made of whole cells: the flux across a face is a fraction of a " +
                    "cell's volume, and a leg that ends halfway through a layer makes that " +
                    "fraction describe nothing. A leg of no layers at all is not a circulation.",
                    nameof(config));
            }
        }

        /// <summary>
        /// Refuses a D074 influx aimed at a vent this world has no room for, before the first step
        /// rather than at the deposit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="MatterInflux.Vent"/> reads <see cref="CurrentField.VentPatch"/> and
        /// <see cref="CurrentField.VentDepthMetres"/> off the same fields D067's plume uses, so a
        /// config with no <see cref="RunConfig.Current"/> at all has nowhere to name, and a patch
        /// index past <c>K</c> would either throw inside the deposit or — worse, if anything ever
        /// wrapped it — put the world's whole matter income in a patch nobody asked for.
        /// <see cref="ValidateVent"/>'s own patch check does not cover this: it returns early
        /// while the plume is off, and an influx at the vent's coordinates is a perfectly sensible
        /// world with no plume in it (a cold seep).
        /// </para>
        /// <para>
        /// Nothing is checked while the influx is 0 or lands at the surface, which is every run
        /// before D074: a world that never asked for a vent influx cannot be refused because of
        /// one, the same rule <see cref="ValidateVent"/> follows.
        /// </para>
        /// </remarks>
        private static void ValidateMatterInflux(RunConfig config, int patchCount)
        {
            if (!(config.MatterInfluxPerSecond > 0f)) return;

            // D175. The islands' tops are D109's columns, so a world without them has nowhere to
            // name: refused rather than fed at the surface, which would be another world wearing
            // this one's header. The budget is not asked for here: the islands themselves refuse
            // a world without one, before the influx is planned.
            if (config.MatterInfluxAt == MatterInflux.Islands)
            {
                if (config.FieldModel != MatterField.Grid)
                {
                    throw new ArgumentException(
                        "MatterInfluxAt is Islands and MatterInfluxPerSecond is " +
                        $"{config.MatterInfluxPerSecond}, but the matter field is {config.FieldModel}: " +
                        "the islands' influx lands in D109's island columns, which only the grid field " +
                        "(FieldModel Grid) has.",
                        nameof(config));
                }

                if (!(config.MatterIslandWavelengthMetres > 0f))
                {
                    throw new ArgumentException(
                        "MatterInfluxAt is Islands and MatterInfluxPerSecond is " +
                        $"{config.MatterInfluxPerSecond}, but no islands are seeded " +
                        "(MatterIslandWavelengthMetres is 0), so there is no island for the influx to " +
                        "land on. Seed D109's islands, or choose surface or vent.",
                        nameof(config));
                }

                return;
            }

            if (config.MatterInfluxAt != MatterInflux.Vent) return;

            CurrentField current = config.Current;
            if (current == null)
            {
                throw new ArgumentException(
                    "MatterInfluxAt is Vent and MatterInfluxPerSecond is " +
                    $"{config.MatterInfluxPerSecond}, but RunConfig.Current is null, so there is " +
                    "no vent patch or vent depth to deposit at. The influx borrows D067's " +
                    "coordinates rather than carrying its own.",
                    nameof(config));
            }

            if (current.VentPatch >= patchCount)
            {
                throw new ArgumentException(
                    $"MatterInfluxAt is Vent and Current.VentPatch is {current.VentPatch}, but " +
                    $"there are only {patchCount} patches. The world's entire matter income would " +
                    "be deposited in a patch this world does not have.",
                    nameof(config));
            }
        }

        /// <summary>
        /// Reports where a creature is and what it spent moving — DESIGN.md §5A.2, §10 M4.
        /// </summary>
        /// <param name="creature">A living organism of this world.</param>
        /// <param name="heightY">Height of its centre of mass, metres. Y is up.</param>
        /// <param name="workJoules">
        /// Mechanical work done at its joints since the last call. Accumulated, not replaced —
        /// physics steps many times per metabolic step.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>This is the entire seam between the physics and the economy, and it points one
        /// way.</b> §6.1 forbids <c>UnityEngine</c> in this assembly, so the world cannot reach
        /// into PhysX to ask where anything is; the simulator pushes both measurements in and the
        /// world never knows a solver exists. The same world runs with nothing calling this, which
        /// is what every calibration in §5A.2b was measured against — a population that cannot
        /// move and for which swimming is free.
        /// </para>
        /// <para>
        /// <b>Work is added rather than assigned</b> because the two clocks differ: physics
        /// integrates at 0.01 s and the economy steps far more slowly, so one metabolic step is
        /// the sum of many strokes. <c>Metabolise</c> drains it.
        /// </para>
        /// <para>
        /// Negative work is refused. <see cref="EffectorDriver"/> reports the unsigned integral
        /// precisely because a joint driven <i>by</i> the water is doing negative work at the
        /// actuator, and billing that as income would be a free-energy source of exactly the kind
        /// §11.2 exists to catch — the creature would evolve to be pushed around.
        /// </para>
        /// </remarks>
        public void Observe(Organism creature, float heightY, float workJoules)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            if (!HeightIsInTheWorld(heightY, Config.WorldDepthMetres))
            {
                string what = float.IsNaN(heightY) || float.IsInfinity(heightY)
                    ? "a non-finite height"
                    : $"a height of {heightY:g4} m in a world {Config.WorldDepthMetres:0.#} m deep";
                throw new ArgumentOutOfRangeException(
                    nameof(heightY), heightY,
                    $"Creature {creature.Id} has {what}, so the solver has already diverged " +
                    "and every income derived from depth would be meaningless.");
            }

            if (workJoules < 0f || float.IsNaN(workJoules))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(workJoules), workJoules,
                    "Mechanical work must be unsigned. A negative cost is an income, and an " +
                    "income for being moved by the water is a free-energy source (§11.2).");
            }

            creature.HeightY = heightY;
            creature.PendingWorkJoules += workJoules;
        }

        /// <summary>
        /// Hands the world what a body's grips owe (D162): the anchoring cell's build price for each
        /// grip the solver formed since the last hand-over. Charged to the reserve at the next
        /// metabolic step, as a burn is, in both books.
        /// </summary>
        public void OweGrip(Organism creature, double joules)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (!(joules >= 0d) || double.IsInfinity(joules))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(joules), joules, "A grip's price is a cost, finite and not negative.");
            }

            creature.PendingGripJoules += joules;
        }

        /// <summary>Joules paid for grips across the world (D162), within <see cref="EnergyOut"/>.</summary>
        public double GripJoulesTotal { get; private set; }

        /// <summary>
        /// Spent units a body's burn puts back, in <c>Matter</c>'s units: the share it keeps
        /// (D206, <see cref="Metabolism.HeldShare"/>) goes to its own <see cref="Organism.SpentUnits"/>
        /// and <see cref="HeldTotal"/>, and the rest into the spent field where the body is and
        /// <see cref="BurntTotal"/>. With <see cref="RunConfig.PlantsKeepMatter"/> off it is the
        /// deposit and the count it always was.
        /// </summary>
        private void ReturnSpent(Organism creature, double returned)
        {
            if (Config.PlantsKeepMatter && creature.HasPhotosyntheticTissue)
            {
                bool ownFirst = Config.PlantsKeepOwnFirst;
                float share = ownFirst
                    ? Metabolism.SmoothHeldShare(creature.Phenotype, Config)
                    : Metabolism.HeldShare(creature.Phenotype, Config);

                // D207: the body's own leaves first, up to one step's light capacity less what it holds.
                // A body that holds the whole burn holds it in one addition, as D206 does.
                if (ownFirst && share < 1f)
                {
                    double room = creature.LastLedger.LightCapacity / (double)Config.JoulesPerUnit - creature.SpentUnits;
                    if (room > 0d)
                    {
                        double first = returned < room ? returned : room;
                        creature.SpentUnits += first;
                        HeldTotal += first;
                        returned -= first;
                        if (!(returned > 0d)) return;
                    }
                }

                if (share > 0f)
                {
                    double held = share >= 1f ? returned : returned * share;
                    creature.SpentUnits += held;
                    HeldTotal += held;
                    returned -= held;
                    if (!(returned > 0d)) return;
                }
            }

            Matter.DepositExact(creature.Point, returned);
            BurntTotal += returned;
        }

        /// <summary>Whether D206's rule reaches this body: it is on, and the body has photosynthetic tissue.</summary>
        private bool Keeps(Organism creature) => Config.PlantsKeepMatter && creature.HasPhotosyntheticTissue;

        /// <summary>
        /// D206: the joules a body's own spent units can be charged with this step, up to what the
        /// light offers (<see cref="EnergyLedger.LightCapacity"/>). The water's uptake does not bound
        /// them: the units are already in the body.
        /// </summary>
        private float OwnFixation(Organism creature, in EnergyLedger ledger)
        {
            if (!(creature.SpentUnits > 0d) || !(ledger.LightCapacity > 0f)) return 0f;
            float own = (float)(creature.SpentUnits * Config.JoulesPerUnit);
            return own < ledger.LightCapacity ? own : ledger.LightCapacity;
        }

        /// <summary>
        /// D206: what a keeping body's fixation asks of the water, J: the bill's fixation from the
        /// water, up to the capacity its own units leave.
        /// </summary>
        private float WaterFixation(Organism creature, in EnergyLedger ledger)
        {
            float room = ledger.LightCapacity - OwnFixation(creature, ledger);
            if (!(room > 0f)) return 0f;
            return ledger.LightIncome < room ? ledger.LightIncome : room;
        }

        /// <summary>
        /// D206's fixation for a body that keeps its matter: its own units charged first, then the
        /// water's for the capacity they leave, under the partial-take rule (D098's leg 1). Returns
        /// the ledger with the whole fixation and its exudation.
        /// </summary>
        private EnergyLedger FixKept(Organism creature, EnergyLedger ledger)
        {
            float own = OwnFixation(creature, ledger);
            float water = WaterFixation(creature, ledger);

            if (water > 0f)
            {
                float wanted = water / Config.JoulesPerUnit;
                float tookUnits = Matter.Take(creature.Point, wanted);
                if (tookUnits < wanted)
                {
                    FixationShortTakes++;
                    water = tookUnits * Config.JoulesPerUnit;
                }
            }

            if (own > 0f)
            {
                // All of the store when the light can charge all of it, so the store empties to
                // exactly zero rather than to a rounding of the float joules.
                double units = own < ledger.LightCapacity
                    ? creature.SpentUnits
                    : Math.Min(creature.SpentUnits, own / (double)Config.JoulesPerUnit);
                creature.SpentUnits -= units;
                RechargedTotal += units;
            }

            float total = own + water;
            return total == ledger.LightIncome ? ledger : ledger.WithFixation(total, Config.ExudationFraction);
        }

        /// <summary>
        /// Spent units D206's plants have kept from their burns, cumulative: the share of the burn
        /// that <see cref="BurntTotal"/> does not count. Zero with <see cref="RunConfig.PlantsKeepMatter"/> off.
        /// </summary>
        public double HeldTotal { get; private set; }

        /// <summary>
        /// Spent units D206's bodies took back by fixation from their own store, cumulative: the
        /// fixation that drew nothing from the water.
        /// </summary>
        public double RechargedTotal { get; private set; }

        /// <summary>
        /// Spent units D206's store has returned to the water, cumulative: at a death with no
        /// corpse, and with a corpse's instalments. So the store's books close:
        /// <see cref="HeldUnits"/> = <see cref="HeldTotal"/> − <see cref="RechargedTotal"/> −
        /// this, to the rounding of the sums.
        /// </summary>
        public double HeldReleasedTotal { get; private set; }

        /// <summary>
        /// Spent units the living and the corpses hold, units — D206's store. O(n), an instrument
        /// for a report row and the matter books.
        /// </summary>
        public double HeldUnits
        {
            get
            {
                if (!Config.PlantsKeepMatter) return 0d;
                double sum = 0d;
                for (int i = 0; i < _living.Count; i++) sum += _living[i].SpentUnits;
                for (int i = 0; i < _corpses.Count; i++) sum += _corpses[i].SpentUnits;
                return sum;
            }
        }

        /// <summary>
        /// Charges what the body's grips owe, from its reserve, as a burn: the joules leave as heat
        /// and the same over ρ arrives in the spent field where the body is. A body that cannot pay
        /// in full is left at exactly zero and dies at the check that follows, as a body short of
        /// its bill does.
        /// </summary>
        private void ChargeGrips(Organism creature)
        {
            double owed = creature.PendingGripJoules;
            creature.PendingGripJoules = 0d;

            double paid = Math.Min(owed, Math.Max(0d, creature.Energy));
            creature.Energy = paid < owed ? 0d : creature.Energy - paid;
            if (!(paid > 0d)) return;

            EnergyOut += paid;
            ReturnSpent(creature, paid / Config.JoulesPerUnit);

            GripJoulesTotal += paid;
            creature.GripJoules += paid;
        }

        /// <summary>
        /// Whether a height is one the sea could hold, or one only a diverged solver produces —
        /// the bound <see cref="Observe(Organism, float, float)"/> refuses and the harness's
        /// divergence check kills at.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Finite is not enough.</b> <c>r31-s3</c> ended at 11,533.5 s with an
        /// <c>ArgumentOutOfRangeException</c> out of <see cref="LightField.Contribute"/>: a root
        /// the solver had thrown to a finite height so large that the layer index
        /// <c>(int)(-heightY / LayerMetres)</c> overflowed passed the non-finite test here and in
        /// the harness, and took the arm down where a NaN body would have been dumped and killed
        /// as a counted death (logbook/0077). The bound is the world's box with room: above, the
        /// world's depth past the surface — D050 stops upward net force at y = 0, so a body above
        /// it is coasting on momentum and never gets far; below, twice the depth past the floor,
        /// which has been a collider since D077's bed (round 5b's sinkers at −131 m in a 60 m
        /// world, logbook/0040, are inside it). A body outside that box is not somewhere in the
        /// sea, and a body that tunnelled through the bed and kept sinking is a solver fault too.
        /// </para>
        /// <para>NaN fails both comparisons, so the non-finite case is inside this one.</para>
        /// </remarks>
        public static bool HeightIsInTheWorld(float heightY, float worldDepthMetres) =>
            heightY <= worldDepthMetres && heightY >= -3f * worldDepthMetres;

        /// <summary>
        /// <see cref="Observe(Organism, float, float)"/> with the whole centre of mass — D083.
        /// The vertex field feeds a body where it is; the cell field reads only the height.
        /// </summary>
        public void Observe(Organism creature, Float3 centre, float workJoules)
        {
            Observe(creature, centre.Y, workJoules);

            if (float.IsNaN(centre.X) || float.IsInfinity(centre.X) ||
                float.IsNaN(centre.Z) || float.IsInfinity(centre.Z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(centre), centre,
                    $"Creature {creature.Id} has a non-finite horizontal position, so the solver " +
                    "has already diverged.");
            }

            creature.X = centre.X;
            creature.Z = centre.Z;
        }

        /// <summary>Advances the world by one step.</summary>
        /// <param name="seconds">Step length. Large steps are fine — this is not a solver.</param>
        public void Step(float seconds)
        {
            if (!(seconds > 0f)) throw new ArgumentOutOfRangeException(nameof(seconds));

            WorldPhaseProbe.Begin();
            ElapsedSeconds += seconds;
            SecondsSinceFloorFired += seconds;

            // D077. Before anything reads a patch — before the light is advanced, before feeding,
            // before conception — because in a shared volume a patch is a region and a creature is
            // in whichever one its body is in. Nothing is drawn from the RNG stream here: this
            // replaces two lotteries (Disperse, AdvectBodies) with a read of where things already
            // are, which is the whole of D077's second rule.
            ReadPatchesFromPositions();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Patches);

            // Before anything reads the light, and from the absolute clock rather than a delta —
            // a sun advanced by accumulating steps drifts out of phase with the world that is
            // paying for it, and would present as a slow trend nobody chose (§5A.4).
            Field.Advance(ElapsedSeconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Light);

            // The ruling of 2026-09-24. The reserve each gestating body starts the step with, so
            // Gestate can read the step's net after the mouth. Touches nothing on a lump breeder.
            MarkReserveAtStepStart();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Reserve);

            Metabolise(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Metabolise);

            // D106 items 1, 3, 4 and 5 (WorldMouth.cs). After Metabolise, so a repair is paid out
            // of the reserve this step actually left the body and a body that could not pay its
            // upkeep is already dead; before Grow, so a body that has just lost a limb invests in
            // the body it now has. At the defaults — nothing armed, nothing with a mouth, no
            // healing and no wound anywhere — it is three walks of the living and no arithmetic,
            // which is what lets it be called unconditionally. The third, from D123, zeroes the
            // contact and damage records so the senses read this step's alone.
            ApplyMouth(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Mouth);

            // The ruling of 2026-09-24: a gestating body banks its share of this step's net, after
            // everything that earns or spends on the step (light, eating, upkeep, repair, a bite)
            // and before growth, so the account and the body draw on the same surplus in a fixed
            // order. A no-op on every lump breeder, which is every recorded world.
            Gestate();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Gestate);

            // fable-propose-growth.md rule 5. After feeding and upkeep, so a body invests what
            // this step actually left it; before Reproduce, so growth has first claim on the
            // reserve and a creature below its adult size almost never clears the breeding gate.
            // Before the fields' own transport passes for the reason feeding is: what a body draws
            // this step comes out of the water as it stood when the step began.
            Grow(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Grow);

            // D061. After Metabolise, so this step's feeding and shading were priced at each
            // creature's patch as it stood when the step began; before Reproduce, so an
            // offspring inherits the patch its parent ends this step in rather than the one it
            // started it in. Skips its own RNG draw entirely when there is nowhere to disperse to
            // or nothing asks for it — see Disperse's own remarks for the K=1 bit-identity guard.
            Disperse();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Disperse);

            // D066. Beside Disperse and under the same rules: after Metabolise so this step was
            // priced where the creature stood, before Reproduce so an offspring is born in the
            // patch its parent ends the step in. Draws nothing at all when the rolls are off.
            AdvectBodies(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.AdvectBodies);

            // D074. Before the field settles, so a unit deposited at the surface starts sinking on
            // the step it arrives rather than a step later — and, with burial after the whole
            // transport pass below, so nothing that arrives at the surface can be buried in the
            // same step it entered the world.
            DepositMatterInflux(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Influx);

            // Rule 6 of fable-propose-grid.md. Before the fields' own passes, so a joule a corpse
            // hands over this step sinks, mixes and drifts on the same step it arrives. That
            // is the rule DepositMatterInflux is placed by above, and the rule a death has
            // always obeyed, since Metabolise runs before all of this. Returns before touching
            // anything when there is no corpse, which is every world at CorpseDecayPerSecond 0.
            StepCorpses(seconds);
            _scentFilledCorpses = _corpses.Count;
            FillScent();
            FillLeafScent();
            WorldPhaseProbe.Mark(WorldPhaseProbe.ScentFill);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Corpses);

            Nutrients.Settle(seconds);
            Matter.Settle(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Settle);

            // D098's leg 8, and it replaces D051's two floor leaks. Charged matter anywhere in
            // the water decays into spent matter in the same place: the joules leave the world as
            // the bacteria's heat, and the same over ρ arrives in the spent field. Before the
            // stirring, as the floor leak was, so what is released this step is mixed this step.
            double remineralised = Nutrients.Remineralise(
                Matter, seconds, Config.RemineralisationPerSecond, Config.JoulesPerUnit);

            if (remineralised > 0d)
            {
                EnergyOut += remineralised;
                RemineralisedTotal += remineralised;
            }

            WorldPhaseProbe.Mark(WorldPhaseProbe.Remineralise);

            // Stirred after it sinks, in the same step. The two are opposed — one carries detritus
            // down and the other spreads it back through the column — and whether the world has a
            // nutrient gradient or a line on the floor is the balance between them (D036). D061
            // adds a horizontal pass alongside the vertical one, throttled by its own knob — see
            // NutrientField.Mix's remarks for why it is a separate, far slower rate.
            Nutrients.Mix(seconds, Config.NutrientMixingDiffusivity, Config.HorizontalMixingDiffusivity);
            WorldPhaseProbe.Mark(WorldPhaseProbe.MixSnow);

            // The matter grid stirs at its own rate on every axis. HorizontalMixingDiffusivity is
            // one knob for two substances, and it belongs to the detritus: it is what the run
            // header's h-mix token names, and World's Grid branch refuses a world where it
            // disagrees with the vertical detritus rate. A cube has no preferred axis, so the
            // matter field's sideways rate is its own vertical one, and that number is stated in
            // RunConfig.MatterMixingDiffusivity rather than in a second knob nobody sets. The cell
            // and vertex fields keep the shared knob exactly as they always had it.
            Matter.Mix(
                seconds, Config.MatterMixingDiffusivity,
                Matter is GridField ? Config.MatterMixingDiffusivity : Config.HorizontalMixingDiffusivity);
            WorldPhaseProbe.Mark(WorldPhaseProbe.MixMatter);

            // D066. Carried after it is stirred, in the same step and against the same clock the
            // bodies feel — diffusion is now the residual and advection the transport. A no-op
            // unless CurrentField.AdvectFields is on, so every run before D066 is untouched. The
            // patch width is the field's own, sqrt(area / K), which is what the horizontal Mix
            // pass above already diffuses across: one geometry, not two.
            Nutrients.Advect(Config.Current, ElapsedSeconds, seconds, Nutrients.PatchWidthMetres);
            WorldPhaseProbe.Mark(WorldPhaseProbe.AdvectSnow);
            Matter.Advect(Config.Current, ElapsedSeconds, seconds, Matter.PatchWidthMetres);
            WorldPhaseProbe.Mark(WorldPhaseProbe.AdvectMatter);

            // D074. After everything that moves matter within the world, so what the floor holds
            // when burial is charged is what settling, mixing and advection actually left there —
            // and after the influx above, so the deposit is not buried on arrival.
            BuryMatter(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Bury);

            // D083. After every pass that moves or removes a vertex, so what a body reads next
            // step is a field whose drifted-together vertices are one and whose count fits its
            // budget. A cell field has nothing to do here.
            Nutrients.Cull();
            Matter.Cull();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Cull);

            Reproduce();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Reproduce);
            EnforceFloor();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Floor);

            // D115. After the floor, which has returned by the time the trickle runs (the trickle
            // runs only once the floor has closed), and before the ceiling, so a founder that
            // arrives this step is counted by it as a floor founder always has been.
            EnforceTrickle(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.Trickle);

            EnforceCeiling();
            WorldPhaseProbe.Mark(WorldPhaseProbe.Ceiling);
            WorldPhaseProbe.End(Living.Count);
        }

        /// <summary>
        /// D074's influx: one step's worth of free matter into the world, at
        /// <see cref="RunConfig.MatterInfluxAt"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Returns before touching anything at an influx of 0</b>, which is every run before
        /// D074 — no draw, no deposit, no counter moved, so those worlds are bit-identical. The
        /// same shape D052's excretion and D070's exudation are written in.
        /// </para>
        /// <para>
        /// <b><see cref="MatterInflux.Surface"/> is a total, not a total per patch.</b> The knob
        /// says what the world receives, so K patches share one deposit rather than each getting
        /// one — otherwise raising <c>HorizontalPatches</c> would silently raise the world's
        /// matter income, and D061's patch count is meant to divide a world rather than multiply
        /// it.
        /// </para>
        /// <para>
        /// <b><see cref="MatterInflux.Vent"/> reads D067's coordinates rather than carrying its
        /// own.</b> A second pair of "where is the vent" fields is a second thing to keep in
        /// agreement with <see cref="RunConfig.WorldDepthMetres"/>, and
        /// <see cref="ValidateMatterInflux"/> has already refused the world in which those
        /// coordinates name nothing.
        /// </para>
        /// <para>
        /// <b><see cref="MatterInflux.Islands"/> lands by the seeding's plan</b> (D175,
        /// <see cref="IslandInflux"/>): the top of D109's island columns, each column in proportion
        /// to its stock at t = 0. The grid alone carries it; the construction refused any other
        /// field.
        /// </para>
        /// </remarks>
        private void DepositMatterInflux(float seconds)
        {
            float rate = Config.MatterInfluxPerSecond;
            if (!(rate > 0f)) return;

            double amount = (double)rate * seconds;

            // D083. Vertices are founded rather than joules deposited: whole quanta, at random
            // positions inside the plume's bottom layer or along the surface, from the field's
            // own stream. What is counted is what was emitted; the remainder waits in the
            // field's bank and is not yet in the world.
            if (Matter is VertexField vertices)
            {
                float width = Matter.PatchWidthMetres;
                float half = 0.5f * Matter.LayerMetres;
                float emitted;

                if (Config.MatterInfluxAt == MatterInflux.Vent)
                {
                    CurrentField plume = Config.Current;
                    emitted = vertices.Emit(
                        amount,
                        new Float3(
                            PatchCentreX(plume.VentPatch), -plume.VentDepthMetres + half,
                            PatchCentreZ(plume.VentPatch)),
                        new Float3(0.5f * width, half, 0.5f * width));
                }
                else
                {
                    // The whole surface, which is the field's own box rather than a length
                    // derived from a width here (fable-propose-box.md).
                    float length = Matter.LengthMetres;
                    float across = Matter.WidthMetres;
                    emitted = vertices.Emit(
                        amount,
                        new Float3(0.5f * length, 0f, 0.5f * across),
                        new Float3(0.5f * length, 0f, 0.5f * across));
                }

                MatterInfluxedTotal += emitted;
                return;
            }

            // The same two boxes the vertex branch emits into, landing on the cells they cover
            // rather than founding quanta inside them. Every joule handed in is in the world on
            // the step it arrives, so what is counted is exactly what was added. The surface case
            // spreads over the whole top layer rather than one column per patch: the vertex field
            // scatters its quanta across the whole surface box, and a grid that dropped the same
            // influx into K centre cells would be a different world wearing the same knob.
            if (Matter is GridField grid)
            {
                // D175. In the island seeding's cells and shares; what is counted is what was laid.
                if (Config.MatterInfluxAt == MatterInflux.Islands)
                {
                    MatterInfluxedTotal += grid.DepositIslands(_islandInflux, amount);
                    return;
                }

                float gridWidth = Matter.PatchWidthMetres;
                float gridHalf = 0.5f * Matter.LayerMetres;

                if (Config.MatterInfluxAt == MatterInflux.Vent)
                {
                    CurrentField plume = Config.Current;
                    MatterInfluxedTotal += grid.DepositBox(
                        amount,
                        new Float3(
                            PatchCentreX(plume.VentPatch), -plume.VentDepthMetres + gridHalf,
                            PatchCentreZ(plume.VentPatch)),
                        new Float3(0.5f * gridWidth, gridHalf, 0.5f * gridWidth));
                    return;
                }

                float gridLength = Matter.LengthMetres;
                float gridAcross = Matter.WidthMetres;
                MatterInfluxedTotal += grid.DepositBox(
                    amount,
                    new Float3(0.5f * gridLength, -gridHalf, 0.5f * gridAcross),
                    new Float3(0.5f * gridLength, gridHalf, 0.5f * gridAcross));
                return;
            }

            if (Config.MatterInfluxAt == MatterInflux.Vent)
            {
                CurrentField vent = Config.Current;
                float all = (float)amount;
                if (!(all > 0f)) return;

                ((NutrientField)Matter).Deposit(-vent.VentDepthMetres, all, vent.VentPatch);
                MatterInfluxedTotal += all;
                return;
            }

            int patches = PatchCount;

            // The float the field is actually handed, not the double it was derived from: what is
            // counted has to be what was deposited, or the identity in MatterInfluxedTotal's
            // remarks drifts by a rounding per step and the audit stops being able to catch a real
            // fault. Deposit's 3-arg overload for D061's reason — correct at K=1 and at K>1 alike.
            float per = (float)(amount / patches);
            if (!(per > 0f)) return;

            // Through the interface, so the cells and the grid share this line: both put one
            // patch's share into that patch at the surface, the grid into the patch's centre
            // column. The same call the cast used to make, so the cell worlds on file are
            // untouched.
            for (int patch = 0; patch < patches; patch++) Matter.Deposit(0f, per, patch);

            MatterInfluxedTotal += (double)per * patches;
        }

        /// <summary>
        /// Carries every corpse one step and takes its decay out of it. Rule 6 of
        /// <c>fable-propose-grid.md</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Returns before touching anything when there is no corpse</b>, which is every world
        /// at <see cref="RunConfig.CorpseDecayPerSecond"/> 0: nothing is founded there, so the
        /// list is empty and this method is a length check. The same shape D052's excretion,
        /// D070's exudation and D074's influx are written in, and the reason the cell, vertex and
        /// grid worlds on file replay bit for bit.
        /// </para>
        /// <para>
        /// <b>It moves like the water and not like a body.</b> A corpse sinks at the detritus
        /// field's own <see cref="IMatterField.SinkMetresPerSecond"/> and rides
        /// <see cref="RunConfig.Current"/> under exactly the condition
        /// <see cref="VertexField.Advect"/> uses: a current that exists and has
        /// <see cref="CurrentField.AdvectFields"/> on. It rides because it is detritus that has
        /// not dissolved yet, and detritus that drifted differently from the water around it
        /// would be a second transport model nobody chose. It wraps on x and z round D077's
        /// rings and clamps to the surface and the floor, which is what a vertex does.
        /// </para>
        /// <para>
        /// <b>The horizontal leg needs a world with positions.</b> A tiled world never calls
        /// <see cref="Observe(Organism, Float3, float)"/>, so a body's x and z stay wherever
        /// <c>Admit</c> put them and nothing in that world reads them. Drifting a corpse across
        /// them there would move it between patches on the strength of a coordinate the world
        /// does not use, so a tiled world's corpse keeps the patch its body died in and only
        /// sinks. The grid and the vertex fields both refuse a tiled world anyway, so this is
        /// only ever a cell world someone turned the knob on in.
        /// </para>
        /// <para>
        /// <b>Decay is a fraction of what is left, so the knob is a half-life</b>
        /// (<c>ln 2 / rate</c>: 0.005/s is 139 s). Clamped at 1 for a step long enough to ask for
        /// more than the corpse holds, the same clamp <see cref="BuryMatter"/> makes. What is
        /// handed over is a float, and the corpse's own account is reduced by exactly that float,
        /// so the transfer is exact in both books rather than exact in one and rounded in the
        /// other.
        /// </para>
        /// <para>
        /// <b>The last crumb is deposited rather than left to halve for ever.</b> Below 1e-6 J the
        /// remainder goes in whole and the corpse is removed. A corpse is one stock since D098, so
        /// the joules are the only test. Without a floor a corpse would live for the length of the
        /// run, and the standing account would carry thousands of objects holding nothing.
        /// </para>
        /// <para>
        /// <b><see cref="DetritusDepositedTotal"/> counts the instalments, not the death.</b> Its
        /// own identity is <c>deposited + exuded − taken == Nutrients.TotalJoules</c>, so counting
        /// a whole body at the death would credit the water with joules the corpse still holds
        /// and break it for as long as the corpse lasted.
        /// </para>
        /// </remarks>
        private void StepCorpses(float seconds)
        {
            CorpsesChanged();

            if (_corpses.Count == 0) return;

            // D127: a corpse sinks at its own rate when one is set, and at the snow's, as every
            // corpse always has, when it is 0.
            float sink = Config.CorpseSinkMetresPerSecond > 0f
                ? Config.CorpseSinkMetresPerSecond * seconds
                : Nutrients.SinkMetresPerSecond * seconds;
            CurrentField current = Config.Current;
            bool drifts = Config.SharedSpace && current != null && current.AdvectFields;
            double floor = Config.CorpseLeavesBelowFraction;

            double sinkingFraction = (double)Config.CorpseDecayPerSecond * seconds;
            if (sinkingFraction > 1.0) sinkingFraction = 1.0;

            // D131: a settled corpse decays at its own rate when one is set. At 0 it is the same
            // double as the sinking one, so every recorded world takes the same instalments.
            double settledFraction = Config.CorpseSettledDecayPerSecond > 0f
                ? (double)Config.CorpseSettledDecayPerSecond * seconds
                : sinkingFraction;
            if (settledFraction > 1.0) settledFraction = 1.0;

            // Where every corpse is carried this step, before any of them hands anything over.
            // The two passes are independent: the water's velocity reads no field and a deposit
            // moves no corpse. So the drift runs corpse by corpse across Core's threads and the
            // deposits follow in the list's order, the order they were always made in. The pin is
            // what lets several threads sample the water at once, as in the farm's water pass: it
            // selects the slot an unpinned call would have selected, so every velocity is the same
            // bits, and it is a no-op for water it has nothing to fill for.
            if (drifts) current.PinInstant(ElapsedSeconds);

            try
            {
                Parallelism.ForRanges(_corpses.Count, (from, to) =>
                {
                    for (int i = from; i < to; i++) Drift(_corpses[i], seconds, sink, drifts);
                });
            }
            finally
            {
                if (drifts) current.UnpinInstant();
            }
            WorldPhaseProbe.Mark(WorldPhaseProbe.CorpseDrift);

            if (Config.CorpseHalfLifeSeconds > 0f) SpoilCorpses(seconds);

            // D206: a corpse's held spent units leave with its instalment, before the instalment
            // pass reads the corpse and compacts the list. Off, no corpse holds any.
            if (Config.PlantsKeepMatter) ReleaseHeld(sinkingFraction, settledFraction, floor);

            double deposited = DetritusDepositedTotal;
            DepositCorpses(_corpses, Nutrients, sinkingFraction, settledFraction, floor, ref deposited,
                ref _depositCell, ref _depositAmount, ref _depositLast);
            DetritusDepositedTotal = deposited;
            WorldPhaseProbe.Mark(WorldPhaseProbe.CorpseDeposits);
        }

        /// <summary>
        /// D212: the clock on a corpse's food, run between the drift and the instalments. Each corpse, sinking
        /// or settled, loses <c>Joules × (1 − 2^(−dt / H))</c> with H <see cref="RunConfig.CorpseHalfLifeSeconds"/>,
        /// so what it still holds halves every H seconds whether or not anything eats it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The energy is heat and the matter goes to the spent field.</b> The bacteria that spoil carrion
        /// respire what they take, so the joules leave as <see cref="EnergyOut"/>, as a burn's do; the units
        /// they were charged on are not respired, and go to the spent field at the corpse's point as
        /// <see cref="BurnUnbitten"/> and the reserve's return send theirs. Both books close in the same
        /// line, so the audit and the matter residual have nothing new to explain.
        /// </para>
        /// <para>
        /// <b>Serial, in the list's order,</b> because the grid's total is order-sensitive. A corpse's held
        /// spent units (D206's <c>SpentUnits</c>) are not touched: they still leave with its instalments.
        /// </para>
        /// <para>
        /// <b>Side effects, left as they fall.</b> A spoiled corpse holds fewer joules, so its mouth reach
        /// (<c>WorldMouth.CorpseReach</c>, from joules), its scent and <see cref="RunConfig.CorpseLeavesBelowFraction"/>'s
        /// test against <c>FirstJoules</c> all shrink with it, and it leaves sooner than the instalments alone
        /// would send it. The age it carries is already checkpointed, and the clock needs no state of its own.
        /// </para>
        /// </remarks>
        private void SpoilCorpses(float seconds)
        {
            double fraction = 1.0 - Math.Pow(2.0, -(double)seconds / Config.CorpseHalfLifeSeconds);
            if (!(fraction > 0d)) return;

            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse corpse = _corpses[i];
                double spoiled = corpse.Joules * fraction;
                if (!(spoiled > 0d)) continue;

                corpse.Joules -= spoiled;
                EnergyOut += spoiled;
                Matter.DepositExact(corpse.Point, spoiled / Config.JoulesPerUnit);
                CorpseSpoiledTotal += spoiled;
            }
        }

        /// <summary>
        /// <see cref="StepCorpses"/>' second pass: every corpse's instalment to <paramref name="field"/>, in
        /// the list's order, each added to <paramref name="total"/> in that order, and the corpses that leave
        /// compacted out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>On a grid the cells are found first, across Core's threads</b> (C2, card-next-r57.md). Each
        /// corpse's instalment and cell depend on that corpse alone, and the grid's cell lookup reads its
        /// shape and no stock, so a parallel pass computes them; a serial pass then makes the same additions
        /// to the same stocks and totals in the list's order, so every double is the one the serial loop
        /// made. A point the grid refuses is handed to the field's own door in that pass, which throws at
        /// the same corpse as before. Any other field takes the serial loop.
        /// </para>
        /// <para>
        /// <paramref name="cell"/>, <paramref name="amount"/> and <paramref name="last"/> are the batched
        /// path's scratch, grown here and kept by the caller.
        /// </para>
        /// </remarks>
        internal static void DepositCorpses(
            List<Corpse> corpses, IMatterField field, double sinkingFraction, double settledFraction, double floor,
            ref double total, ref int[] cell, ref double[] amount, ref bool[] last)
        {
            if (!(field is GridField grid))
            {
                DepositCorpsesSerially(corpses, field, sinkingFraction, settledFraction, floor, ref total);
                return;
            }

            int n = corpses.Count;
            if (cell == null || cell.Length < n)
            {
                int size = n + n / 4 + 16;
                cell = new int[size];
                amount = new double[size];
                last = new bool[size];
            }

            int[] cells = cell;
            double[] amounts = amount;
            bool[] lasts = last;
            Parallelism.ForRanges(n, (from, to) =>
            {
                for (int i = from; i < to; i++)
                {
                    Corpse corpse = corpses[i];
                    double given = Instalment(corpse, sinkingFraction, settledFraction, floor, out bool leaves);
                    amounts[i] = given;
                    lasts[i] = leaves;
                    cells[i] = given > 0d && grid.TryCellOf(corpse.Point, out int c) ? c : -1;
                }
            });

            int kept = 0;
            for (int i = 0; i < n; i++)
            {
                Corpse corpse = corpses[i];
                double given = amounts[i];
                bool leaves = lasts[i];
                if (given > 0d)
                {
                    if (cells[i] >= 0) grid.AddToCell(cells[i], given);
                    else if (leaves) grid.DepositExact(corpse.Point, given);
                    else grid.Deposit(corpse.Point, (float)given);

                    if (leaves) corpse.Joules = 0d;
                    else corpse.Joules -= given;
                    total += given;
                }

                if (leaves)
                {
                    corpse.Joules = 0d;
                    continue;
                }

                corpses[kept++] = corpse;
            }

            if (kept < n) corpses.RemoveRange(kept, n - kept);
        }

        /// <summary>
        /// D206: each corpse's held spent units, released into the spent field in the share its
        /// instalment this step takes of its joules, and all of them on the step it leaves (the
        /// same test <see cref="Instalment"/> makes). Reads the corpse's joules and writes only its
        /// held units, so the instalment pass that follows meets the corpse it would have met.
        /// </summary>
        private void ReleaseHeld(double sinkingFraction, double settledFraction, double floor)
        {
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse corpse = _corpses[i];
                if (!(corpse.SpentUnits > 0d)) continue;

                Instalment(corpse, sinkingFraction, settledFraction, floor, out bool last);
                double fraction = corpse.Settled ? settledFraction : sinkingFraction;
                double units = last || fraction >= 1.0 ? corpse.SpentUnits : corpse.SpentUnits * fraction;
                if (!(units > 0d)) continue;

                Matter.DepositExact(corpse.Point, units);
                corpse.SpentUnits = last ? 0d : corpse.SpentUnits - units;
                HeldReleasedTotal += units;
            }
        }

        /// <summary>
        /// One corpse's instalment this step, as the serial loop takes it: everything it holds when it leaves
        /// (<paramref name="last"/>), otherwise its share as the float the field takes, held here as that
        /// float's double; 0 when it hands over nothing. Reads the corpse and writes nothing.
        /// </summary>
        private static double Instalment(Corpse corpse, double sinkingFraction, double settledFraction, double floor, out bool last)
        {
            double fraction = corpse.Settled ? settledFraction : sinkingFraction;
            last = fraction >= 1.0 || corpse.Joules < 1e-6;
            if (!last && floor > 0d &&
                corpse.Joules - corpse.Joules * fraction < floor * corpse.FirstJoules)
            {
                last = true;
            }

            if (!(corpse.Joules > 0d)) return 0d;
            if (last) return corpse.Joules;
            float given = (float)(corpse.Joules * fraction);
            return given > 0f ? given : 0d;
        }

        /// <summary><see cref="DepositCorpses"/> on a field that is not a grid: the loop as it always ran.</summary>
        internal static void DepositCorpsesSerially(
            List<Corpse> corpses, IMatterField field, double sinkingFraction, double settledFraction, double floor,
            ref double total)
        {
            int kept = 0;
            for (int i = 0; i < corpses.Count; i++)
            {
                Corpse corpse = corpses[i];

                // Settled is set by the drift above, so a corpse takes the settled rate from the
                // step it lands.
                double fraction = corpse.Settled ? settledFraction : sinkingFraction;

                // The last instalment: everything that is left, in one go. One stock since D098,
                // so one test — a corpse is charged matter and nothing else. A step long enough
                // to ask for the whole remainder is the same case.
                bool last = fraction >= 1.0 || corpse.Joules < 1e-6;

                // D127's floor: an instalment that would leave the corpse under its share of what it
                // was is the last one, so a standing corpse always holds at least that share. Off
                // at 0, where this reads nothing and the test above is the one it always was.
                if (!last && floor > 0d &&
                    corpse.Joules - corpse.Joules * fraction < floor * corpse.FirstJoules)
                {
                    last = true;
                }

                FieldPoint at = corpse.Point;

                if (corpse.Joules > 0d)
                {
                    if (last)
                    {
                        // The last instalment goes through the field's exact door, so the corpse
                        // leaves nothing behind. Until round 52's build it went as a float, and
                        // the half ulp it dropped was zeroed with the corpse
                        // (logbook/specs/r51-read/b1-probe.txt).
                        double rest = corpse.Joules;
                        field.DepositExact(at, rest);
                        corpse.Joules = 0d;
                        total += rest;
                    }
                    else
                    {
                        // An instalment on the way is the float the field takes, booked as the
                        // same float, so the two halves of it cannot disagree.
                        float given = (float)(corpse.Joules * fraction);
                        if (given > 0f)
                        {
                            field.Deposit(at, given);
                            corpse.Joules -= given;
                            total += given;
                        }
                    }
                }

                if (last)
                {
                    // The corpse leaves, released rather than carried by an object that would
                    // otherwise halve for ever.
                    corpse.Joules = 0d;
                    continue;
                }

                corpses[kept++] = corpse;
            }
            if (kept < corpses.Count) corpses.RemoveRange(kept, corpses.Count - kept);
        }

        /// <summary>
        /// One corpse's step of age, sinking and carriage: <see cref="StepCorpses"/>' first pass.
        /// Writes the corpse and nothing else, and reads no field.
        /// </summary>
        private void Drift(Corpse corpse, float seconds, float sink, bool drifts)
        {
            CurrentField current = Config.Current;
            float length = Nutrients.LengthMetres;
            float width = Nutrients.WidthMetres;
            float depth = Config.WorldDepthMetres;
            bool tank = Config.WorldShape == WorldShape.Tank;

            corpse.AgeSeconds += seconds;

            // D127: a corpse at rest on a reef's cap or the bed stays where it lies.
            if (corpse.Settled) return;

            Float3 p = corpse.Position;
            float x = p.X;
            float y = p.Y - sink;
            float z = p.Z;

            if (drifts)
            {
                // A corpse carries a position, so under CurrentMode.Transport and the ocean it
                // rides the water where it actually is rather than the water at its patch's
                // centre. The rolls take the patch, which is all that field is a function of, so
                // every run in the record drifts on the same arithmetic it always did.
                Float3 v = current.Mode != CurrentMode.Rolls || tank
                    ? current.VelocityAt(p.X, p.Y, p.Z, ElapsedSeconds)
                    : current.VelocityAt(p.Y, ElapsedSeconds, corpse.Patch, PatchCount);

                y += v.Y * seconds;

                if (tank)
                {
                    // The glass, not a seam. The streams have no radial flow at the wall, so a
                    // corpse is not carried into it; what this catches is the step's own
                    // arithmetic — an explicit half-second of a flow that curves — and it
                    // catches it by sliding the parcel back onto the rim rather than by
                    // wrapping it to the far side of the same tank.
                    KeepInTheWater(x + v.X * seconds, z + v.Z * seconds, out x, out z);
                }
                else
                {
                    x = WrapAxis(x + v.X * seconds, length);
                    z = WrapAxis(z + v.Z * seconds, width);
                }
            }

            if (y > 0f) y = 0f;

            // D127: the first solid surface under the corpse, a reef's cap it was above at the
            // step's start or the bed, stops it for good. The bed bounds it and the world's depth
            // does not: the depth is the bed's mean, and a tilted bed's deep side lies well below
            // it. Round 51's smoke held 621 corpses at exactly -45 m over a bed down to -93 m, never
            // settled and still drifting, because the depth's clamp ran first and the bed was never
            // reached. The grid reaches the deepest column's floor (D092), so the deposit there is
            // in the water. A world whose corpses do not settle keeps the clamp it always had, so
            // every recorded run replays.
            if (Config.CorpseSettles)
            {
                float rest = RestingHeight(x, z, p.Y);
                if (y <= rest)
                {
                    y = rest;
                    corpse.Settled = true;
                }
            }
            else if (y < -depth)
            {
                y = -depth;
            }

            corpse.Position = new Float3(x, y, z);
            if (drifts) corpse.Patch = PatchOfXZ(x, z);
        }

        /// <summary>
        /// D127: the height of the first solid surface under a point at <paramref name="fromY"/>:
        /// the highest reef cap whose outline covers the point and whose top is at or below it,
        /// else the bed (the flat floor at the world's depth when there is no shaped bed).
        /// Reads the world and writes nothing, so the drift's threads may share it.
        /// </summary>
        internal float RestingHeight(float x, float z, float fromY)
        {
            double rest = Bed != null ? Bed.FloorY(x, z) : -Config.WorldDepthMetres;

            if (Reefs != null)
            {
                for (int r = 0; r < Reefs.Count; r++)
                {
                    double top = Reefs.CapTopY(r);
                    if (top > rest && top <= fromY && Reefs.InsideOutline(r, x, z)) rest = top;
                }
            }

            // The coral's plates (coral-build.md, the readers): the highest plate top over the
            // point's column, when it is under the point. Branches let a corpse pass to the bed.
            if (Coral != null)
            {
                double top = Coral.PlateTop(x, z);
                if (top > rest && top <= fromY) rest = top;
            }

            return (float)rest;
        }

        /// <summary>
        /// The water a field actually holds, m³ — what <see cref="RunConfig.MatterBudgetUnits"/>
        /// divides a total by.
        /// </summary>
        /// <remarks>
        /// A grid knows (<see cref="GridField.LiveVolumeCubicMetres"/>), because it is the one
        /// representation whose cells can be outside the water. Everything else fills the box, so
        /// the answer is the box's own volume — and a tank is only ever run on a grid, since
        /// <see cref="World"/>'s constructor refuses the cell field there.
        /// </remarks>
        private static double LiveVolumeOf(IMatterField field, RunConfig config) =>
            field is GridField grid
                ? grid.LiveVolumeCubicMetres
                : (double)config.WorldAreaSquareMetres * config.WorldDepthMetres;

        /// <summary>
        /// The patch a horizontal position falls in: <c>iz · (K / A) + ix</c>, numbered along x
        /// first — D077's rule as fable-propose-box.md's clause 3 lays it out, and
        /// <c>floor(x / W) mod K</c> term for term at A = 1.
        /// </summary>
        /// <remarks>
        /// <b>In a tank it is the ring of equal area the point falls in</b>, 0 at the axis and
        /// K−1 against the glass — <see cref="TankGeometry.RingOf"/>.
        /// </remarks>
        private int PatchOfXZ(float x, float z)
        {
            if (Config.WorldShape == WorldShape.Tank)
            {
                return TankGeometry.RingOf(x, z, TankRadiusMetres, PatchCount);
            }

            float width = Nutrients.PatchWidthMetres;
            int along = PatchesAlong;

            int ix = (int)Math.Floor(x / width);
            ix %= along;
            if (ix < 0) ix += along;

            if (PatchesAcross == 1) return ix;

            int iz = (int)Math.Floor(z / width);
            iz %= PatchesAcross;
            if (iz < 0) iz += PatchesAcross;

            return iz * along + ix;
        }

        /// <summary>
        /// The x of a patch's centre, m. <c>(patch + ½)·W</c> at A = 1; in a tank, the ring's
        /// mid-radius on the <c>θ = 0</c> ray, which is the ray every other reader of this
        /// geometry picks.
        /// </summary>
        private float PatchCentreX(int patch) =>
            Config.WorldShape == WorldShape.Tank
                ? TankRadiusMetres + TankGeometry.MidRadiusOf(patch, TankRadiusMetres, PatchCount)
                : (patch % PatchesAlong + 0.5f) * Nutrients.PatchWidthMetres;

        /// <summary>
        /// The z of a patch's centre, m. Half the box's width at A = 1; the axis in a tank.
        /// </summary>
        private float PatchCentreZ(int patch) =>
            Config.WorldShape == WorldShape.Tank
                ? TankRadiusMetres
                : (patch / PatchesAlong + 0.5f) * Nutrients.PatchWidthMetres;

        /// <summary>
        /// A horizontal position kept inside the glass: itself when it is in the water, and the
        /// nearest point of the rim when it is not.
        /// </summary>
        /// <remarks>
        /// The tank's answer to <see cref="WrapAxis"/>, and deliberately a different one: a wrap
        /// is a boundary with nothing on the other side of it, and this boundary has a wall.
        /// </remarks>
        private void KeepInTheWater(float x, float z, out float insideX, out float insideZ)
        {
            float radius = TankRadiusMetres;
            double dx = x - radius;
            double dz = z - radius;
            double r = Math.Sqrt(dx * dx + dz * dz);

            if (!(r > radius))
            {
                insideX = x;
                insideZ = z;
                return;
            }

            insideX = (float)(radius + dx * radius / r);
            insideZ = (float)(radius + dz * radius / r);
        }

        /// <summary>A coordinate folded back onto a ring of the given extent. <see cref="GridField"/>'s own.</summary>
        private static float WrapAxis(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float folded = v - extent * (float)Math.Floor(v / extent);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
        }

        /// <summary>
        /// D074's burial: a fraction of the floor's free matter, out of the world for good. The
        /// floor is the last layer on the cell field and each column's lowest live cell on the
        /// grid (D175).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Spent matter on the floor and nothing else.</b> Not the charged field — that is
        /// <see cref="Nutrients"/>, the same substance in the other state, which reaches this one
        /// only through <c>Remineralise</c> — and not the charged matter a living body is made
        /// of, because burying a creature out from under itself would open the matter identity
        /// with no way to describe what happened.
        /// </para>
        /// <para>
        /// <b>Counted as the field's own before-and-after</b>, not as the draw's own return.
        /// The cell field's <c>Take</c> subtracted a double and handed back a float copy of it, so
        /// a floor whose stock is smaller than the request would lose slightly more than the
        /// counter recorded — a rounding, and exactly the kind of rounding an identity asserted
        /// "to the rounding" stops being able to distinguish from a leak. The draw now goes
        /// through <see cref="IMatterField.TakeFromLayer"/> so that a third representation needs
        /// no branch here; the float <c>wanted</c> and the before-and-after count are unchanged,
        /// which is what keeps every cell world on file replaying bit for bit.
        /// </para>
        /// <para>
        /// The fraction is clamped at 1 for a step long enough to ask for more than the floor
        /// holds; <c>Take</c> caps at the stock anyway, but a clamp says what a whole-floor step
        /// means rather than leaving it to the cap.
        /// </para>
        /// </remarks>
        private void BuryMatter(float seconds)
        {
            float rate = Config.MatterBurialPerSecond;

            // D190. The desert at its own rate, column by column: a floor cell gives its
            // column's fraction of its own stock. The constructor has refused a desert rate
            // without the islands' influx plan.
            if (Config.MatterBurialDesertPerSecond >= 0f)
            {
                double island = Math.Min(1d, Math.Max(0d, (double)rate * seconds));
                double desert = Math.Min(1d, (double)Config.MatterBurialDesertPerSecond * seconds);
                if (island > 0d || desert > 0d)
                    MatterBuriedTotal += ((GridField)Matter).BuryFloorByIslands(_islandInflux, island, desert);
                return;
            }

            if (!(rate > 0f)) return;

            double fraction = (double)rate * seconds;
            if (fraction > 1d) fraction = 1d;
            if (!(fraction > 0d)) return;

            // D083. Each vertex resting on the floor leaves whole with this probability: the
            // same expected rate as the cells' fraction, one vertex at a time.
            if (Matter is VertexField vertices)
            {
                MatterBuriedTotal += vertices.BuryFloor(fraction);
                return;
            }

            // D175. On the grid the floor is each column's lowest live cell, where the snow
            // actually rests on a sloped bed; the old reading, the array's last layer, is water
            // only under the deepest hollow there. On a flat grid the two are the same cells read
            // in the same order, so this branch gives the old numbers to the bit. Still a draw
            // per patch spread over its cells by what each may give: at Edible = stock (no refuge)
            // every cell gives the same fraction of its own, which is the per-column rule, and
            // keeping the patch loop keeps the flat grid's rounding and its counter's sums.
            if (Matter is GridField grid)
            {
                for (int patch = 0; patch < PatchCount; patch++)
                {
                    double before = grid.FloorStock(patch);
                    if (!(before > 0d)) continue;

                    float wanted = (float)(before * fraction);
                    if (!(wanted > 0f)) continue;

                    grid.TakeFromFloor(patch, wanted);
                    MatterBuriedTotal += before - grid.FloorStock(patch);
                }

                return;
            }

            int floor = Matter.LayerCount - 1;

            for (int patch = 0; patch < PatchCount; patch++)
            {
                double before = Matter.StockInLayer(floor, patch);
                if (!(before > 0d)) continue;

                float wanted = (float)(before * fraction);
                if (!(wanted > 0f)) continue;

                Matter.TakeFromLayer(floor, patch, wanted);
                MatterBuriedTotal += before - Matter.StockInLayer(floor, patch);
            }
        }

        /// <remarks>
        /// §5A.7's photosynthetic mat, caught rather than culled — D021. Every step past the
        /// ceiling costs more than the last, so a loop that only noticed would still be a loop
        /// that never returned; and culling to fit a budget would be selection performed by us,
        /// hiding a calibration failure behind a population number we chose.
        /// <para>
        /// Two ceilings, both checked every step. <see cref="RunConfig.MaximumPopulation"/> is the
        /// original one; <see cref="RunConfig.MaximumTissueJoules"/> is rule 9's addition,
        /// because growth broke the count as a biomass proxy. A newborn is grown from a fraction
        /// of its adult body, so a world of 5,000 newborns can hold a third of the tissue a world
        /// of 5,000 adults did. The tissue ceiling defaults to 0, off, so a config written before
        /// this rule still describes the world it ran, and at 0 the tissue sum below is skipped
        /// rather than taken and compared against zero every step.
        /// </para>
        /// </remarks>
        private void EnforceCeiling()
        {
            bool overPopulation = _living.Count > Config.MaximumPopulation;

            // Summed only when the tissue ceiling is on, or the population ceiling has already
            // fired and the run is ending anyway. Every config on file before rule 9 has this
            // off, and this runs once a step: summing every living body's tissue on top of the
            // count check would be a real cost across thousands of creatures for an instrument
            // almost nothing reads.
            bool tissueCeilingOn = Config.MaximumTissueJoules > 0d;
            double tissueJoules = tissueCeilingOn || overPopulation ? StandingTissueJoules : 0d;
            bool overTissue = tissueCeilingOn && tissueJoules > Config.MaximumTissueJoules;

            if (!overPopulation && !overTissue) return;

            string message = overPopulation
                ? FormattableString.Invariant($"Population reached {_living.Count}, above the ceiling of ") +
                  FormattableString.Invariant(
                      $"{Config.MaximumPopulation}, at t={ElapsedSeconds:0.#} s after {Births} births. ") +
                  "This is §5A.7's photosynthetic mat: light is covering upkeep, so nothing has to " +
                  "do anything and every creature can afford to breed. The ratio in §5A.2 is too " +
                  "generous — lower the surface irradiance or raise cell upkeep. It is not culled, " +
                  "because culling to fit a compute budget is selection performed by us and would " +
                  "hide this behind a population number we chose."
                : FormattableString.Invariant($"Standing tissue reached {tissueJoules:0} J, above the ") +
                  FormattableString.Invariant(
                      $"ceiling of {Config.MaximumTissueJoules:0} J, at t={ElapsedSeconds:0.#} s after ") +
                  FormattableString.Invariant($"{Births} births, with {_living.Count} bodies alive. ") +
                  "This is the same photosynthetic mat §5A.7 names, read by biomass rather than by " +
                  "count. Growth means a body count alone can miss it: a world of small, fast-growing " +
                  "bodies can stay under the population ceiling while still holding more tissue than " +
                  "the light or matter budget was calibrated for. It is not culled, for the same " +
                  "reason the count ceiling is not.";

            throw new PopulationRunawayException(
                message, _living.Count, tissueJoules, ElapsedSeconds,
                overPopulation ? "population" : "tissue");
        }

        /// <summary>
        /// Wall-clock ticks (<see cref="System.Diagnostics.Stopwatch.Frequency"/> a second) spent
        /// inside <c>Metabolism.StepAt</c> by <c>Metabolise</c>, both the appetite pass and the rationed re-price.
        /// Cumulative since the world was built or restored; the farm's <c>wallLedgerMs</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The ledger, because the support term is not a call of its own.</b> D113's term is
        /// one multiply-add per part inside <c>Metabolism.Bill</c>'s walk, beside the cell's own
        /// intake and upkeep, the four D106 prices, D111's offset price and the neural cost;
        /// timing it alone would take a timestamp pair per part inside that loop, which would
        /// cost more than the term. So the timer brackets the whole bill, and a reading of the
        /// support term's cost is the ledger's time with the price on against the same with it off
        /// (round 46's K10).
        /// </para>
        /// <para>
        /// <b>A clock and nothing else.</b> Nothing reads it back inside the world, nothing
        /// branches on it and no checkpoint carries it, so a world timed and one not timed step
        /// the same trajectory; a restored world starts it at 0, as the farm's own timers do.
        /// </para>
        /// </remarks>
        public long LedgerTicks { get; private set; }

        /// <remarks>
        /// <para>
        /// <b>Three passes, because both resources are finite and shared</b> (§5A.2b, §5A.2c).
        /// Every creature's shadow must be known before anyone's income can be, and every
        /// creature's appetite before anyone is fed. A single pass would give whoever the list
        /// happened to walk first the undiminished sun and an unemptied larder, making income
        /// depend on iteration order — the kind of fault that produces a perfectly plausible
        /// number.
        /// </para>
        /// <para>
        /// The appetite pass costs a second evaluation of the metabolic step per creature, since
        /// what a body would take is exactly what <see cref="Metabolism"/> says it takes at the
        /// unrationed density. Estimating it more cheaply would mean a second expression of the
        /// same quantity, and two expressions of one quantity is how they come to disagree.
        /// </para>
        /// </remarks>
        private void Metabolise(float seconds)
        {
            bool byExposure = Config.LightByExposure;

            Field.Clear();
            Nutrients.ClearDemand();

            // D098's leg 1. The producers draw on the spent field exactly as the feeders draw on
            // the charged one, so it needs the same three passes: what everybody would take, what
            // each cell can actually give, and then the takes. One extra ClearDemand a step is
            // the whole of the cost.
            Matter.ClearDemand();

            // D099's cap, and the same number Metabolism bills on below. A body's shadow is what
            // it earns on, so the two have to be one quantity: shade it casts but does not collect
            // would be light destroyed, and light it collects but does not cast would be light
            // created, and the audit would see either.
            //
            // D110 keeps the rule in a pose: the same exposure array reaches Bill below, or
            // neither side reads it. Off, the array is null and this is the recorded call.
            //
            // The areas first, body by body across Core's threads, and then the contributions in
            // the living's order. An area is a function of its own body alone; only the sum into
            // a layer is shared, and it is taken in the order it always was, so the split moves
            // no number.
            if (_litAreas.Length < _living.Count)
            {
                Array.Resize(ref _litAreas, Math.Max(_living.Count, 2 * _litAreas.Length));
            }

            float[] litAreas = _litAreas;
            bool capOn = Config.LightSilhouetteCap;

            // D219: a grazed part casts the shadow of its health, as the leaf bills the light of it. A body nothing has
            // wounded has no health array and casts what it always did, so the dial off is the recorded call.
            bool shadeByHealth = Config.ContactEating;

            Parallelism.ForRanges(_living.Count, (from, to) =>
            {
                for (int i = from; i < to; i++)
                {
                    Organism creature = _living[i];
                    float[] exposure = byExposure ? creature.CurrentExposure : null;
                    litAreas[i] = shadeByHealth
                        ? ShadowAtHealth(creature, byExposure, capOn)
                        : exposure == null
                            ? creature.Phenotype.EffectiveLitArea(capOn)
                            : creature.Phenotype.EffectiveLitArea(exposure, creature.UpInBody, capOn);
                }
            });

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                Field.Contribute(creature.HeightY, litAreas[i], creature.Patch);
            }

            // D220: the dead shade the living until they close. Remains cast their shadow at their health, after the
            // living in the field's order, and earn nothing on it.
            if (shadeByHealth)
            {
                for (int i = 0; i < _remains.Count; i++)
                {
                    Organism body = _remains[i];
                    Field.Contribute(body.HeightY, ShadowAtHealth(body, byExposure, capOn), body.Patch);
                }
            }
            WorldPhaseProbe.Mark(WorldPhaseProbe.LightContribute);
            Field.Solve();
            WorldPhaseProbe.Mark(WorldPhaseProbe.LightSolve);

            // Appetite. Priced at the full local density, so this is what each creature would eat
            // if it were alone — which is the quantity a proportional share has to be taken of.
            // Kept, because it is also the answer whenever the larder turns out to be full.
            if (_ledgers.Length < _living.Count)
            {
                Array.Resize(ref _ledgers, Math.Max(_living.Count, 2 * _ledgers.Length));
            }

            EnergyLedger[] ledgers = _ledgers;

            // The bills, then the demands. A bill reads its own body, the fields' stock and the
            // solved light, and writes its own slot and its own body; a demand writes the fields'
            // demand arrays, which no bill reads. So the bills can run body by body across Core's
            // threads and the demands follow in the living's order, the order each cell's sum was
            // always taken in, and the split moves no number. On a grid only: the grid's reads are
            // array reads, and the older fields' have not been checked for scratch they share.
            //
            // LedgerTicks' bracket: at one thread a clock read either side of each bill, as it
            // always was. On several it is the parallel pass's wall, the density reads included,
            // since a bracket per bill on several threads would add up the threads' time.
            if (Parallelism.On && Nutrients is GridField && Matter is GridField)
            {
                long passStarted = System.Diagnostics.Stopwatch.GetTimestamp();

                Parallelism.ForRanges(_living.Count, (from, to) =>
                {
                    for (int i = from; i < to; i++)
                    {
                        ledgers[i] = BillAtFullDensity(_living[i], seconds, byExposure, false);
                    }
                });

                LedgerTicks += System.Diagnostics.Stopwatch.GetTimestamp() - passStarted;
            }
            else
            {
                for (int i = 0; i < _living.Count; i++)
                {
                    ledgers[i] = BillAtFullDensity(_living[i], seconds, byExposure, true);
                }
            }

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                EnergyLedger ledger = ledgers[i];

                Nutrients.Demand(creature.Point, ledger.PoolDrawn);

                // What fixation would take out of the water, in units: the joules it fixed over
                // what a charged unit carries. A body that fixed nothing asks for nothing, which
                // is every feeder and every producer in the dark.
                if (ledger.LightIncome > 0f)
                {
                    // D206: a body that keeps its matter asks the water only for what its own
                    // units leave of the light's capacity.
                    float fromWater = Keeps(creature) ? WaterFixation(creature, ledger) : ledger.LightIncome;
                    if (fromWater > 0f) Matter.Demand(creature.Point, fromWater / Config.JoulesPerUnit);
                }
            }

            // Every share and every rationed price in the pass below is taken from what the
            // cells hold now, not from what earlier meals in the same walk leave behind.
            WorldPhaseProbe.Mark(WorldPhaseProbe.Ledgers);
            Nutrients.FreezeAvailability();
            Matter.FreezeAvailability();
            WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseFreeze);

            bool hazard = Config.AgeHazardBaseSeconds > 0f;

            // The pure parts of every body's turn, taken before the walk and across Core's threads
            // (card-next-r57.md Part 8): the two shares and the age hazard's arithmetic. Each reads
            // only what the walk does not change before that body's turn, and writes only its own
            // slot, so the walk below meets the same numbers it would have computed itself.
            //  - The shares read the frozen availability and the demand, which nothing writes
            //    during the walk; only a field whose ShareAt reads frozen arrays alone is asked
            //    ahead (FrozenShares). A vertex field gathers into a scratch of its own and reads
            //    the live stock, so the walk asks it itself, as before.
            //  - The hazard is the age step's arithmetic with nothing written on the body (AgeStep).
            //    The clock advances and the hazard is added only for a body still solvent after its
            //    reserve update, and the walk keeps exactly that: it applies these numbers at that
            //    point (AgedAt), after the update, and only for a solvent body. Nothing
            //    in the walk moves a body's clock or hazard before its own turn, so the pre-pass
            //    reads the same AgeClock the walk would.
            int count = _living.Count;
            EnsureTurnScratch(count);
            bool sharesAhead = FrozenShares(Nutrients) && FrozenShares(Matter);
            if (sharesAhead || hazard)
            {
                double k = hazard ? AgeHazardScale() : 0d;
                Parallelism.ForRanges(count, (from, to) =>
                {
                    for (int j = from; j < to; j++)
                    {
                        Organism body = _living[j];
                        if (sharesAhead)
                        {
                            _turnShare[j] = Nutrients.ShareAt(body.Point);
                            _turnSpentShare[j] = Matter.ShareAt(body.Point);
                        }

                        if (hazard)
                        {
                            _turnClock[j] = AgeStep(body, seconds, k, out double added);
                            _turnHazard[j] = added;
                            _turnThreshold[j] = AgeThreshold(body);
                        }
                    }
                });
            }

            WorldPhaseProbe.Mark(WorldPhaseProbe.MetabolisePrePass);

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism creature = _living[i];

                // Read before it advances, because the recompute below has to price the same
                // step the loop above priced. Ageing a creature mid-step would make the short-
                // larder branch a different creature from the full-larder one.
                float age = creature.Age;
                creature.Age += seconds;

                float share = sharesAhead ? _turnShare[i] : Nutrients.ShareAt(creature.Point);
                float spentShare = sharesAhead ? _turnSpentShare[i] : Matter.ShareAt(creature.Point);
                EnergyLedger ledger = _ledgers[i];

                // Recomputed only when one of the larders is short. Scaling the stored ledger
                // instead would assume intake is linear in density, which it is for a filter
                // feeder and is not for anything with a bite rate that saturates — nor for a
                // leaf, whose uptake saturates at UptakeHalfSaturation.
                if (share < 1f || spentShare < 1f)
                {
                    WorldPhaseProbe.Count(WorldPhaseProbe.Rebilled, 1);
                    float rationed = share < 1f
                        ? Nutrients.FrozenEdibleDensityAt(creature.Point) * share
                        : Nutrients.FrozenEdibleDensityAt(creature.Point);

                    // D098. The same re-price on the other field, in one call rather than two:
                    // a mixotroph short of both would otherwise be priced twice and the second
                    // answer would overwrite the first.
                    float rationedSpent = spentShare < 1f
                        ? Matter.FrozenEdibleDensityAt(creature.Point) * spentShare
                        : Matter.FrozenEdibleDensityAt(creature.Point);

                    // The same work, not more: this replaces the ledger rather than adding to it.
                    float[] exposure = byExposure ? creature.CurrentExposure : null;

                    long billStarted = System.Diagnostics.Stopwatch.GetTimestamp();

                    ledger = exposure == null
                        ? Metabolism.StepAt(
                            creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z),
                            rationed, rationedSpent, creature.PendingWorkJoules, seconds, age)
                        : Metabolism.StepAt(
                            creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z),
                            rationed, rationedSpent, creature.PendingWorkJoules, seconds, age,
                            exposure, creature.UpInBody);

                    // D219: a grazed leaf fixes less light. Its own statement, so the recorded calls above are
                    // the only ones that run with the dial off.
                    if (Config.ContactEating && creature.PartHealth != null)
                    {
                        ledger = Metabolism.StepAtHealth(
                            creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z),
                            rationed, rationedSpent, creature.PendingWorkJoules, seconds, age,
                            exposure, creature.UpInBody, creature.PartHealth);
                    }

                    LedgerTicks += System.Diagnostics.Stopwatch.GetTimestamp() - billStarted;

                    // A share is a fraction of the demand, and scaling the density delivers
                    // exactly that only for an intake linear in density. A saturating mouth
                    // (the satiation cap, D062) re-saturates at the rationed density and would
                    // draw its whole demand whatever its share, so the cell would be overdrawn
                    // and the last feeders' takes would come up short. The allowance is the
                    // bound the demand pass promised, and no feeder draws past it.
                    float allowance = share * _ledgers[i].PoolDrawn;
                    if (ledger.PoolDrawn > allowance) ledger = ledger.WithPoolDrawn(allowance);

                    // The same bound on the other field: uptake re-saturates at the rationed
                    // spent density, so a producer in a stripped cell would otherwise plan to
                    // fix more than its share of that cell.
                    // D206: a body that keeps its matter demanded only the water's part.
                    float fixationAllowance = Keeps(creature)
                        ? spentShare * WaterFixation(creature, _ledgers[i])
                        : spentShare * _ledgers[i].LightIncome;
                    if (ledger.LightIncome > fixationAllowance)
                    {
                        ledger = ledger.WithLightIncome(fixationAllowance);
                    }

                    // What the world actually fed it, replacing the appetite pass's unrationed
                    // reading. Already share-multiplied — AbsorptiveSample.DensityHere says so,
                    // because a reader that multiplied again would halve a scarce world twice.
                    if (creature.HasAbsorptiveTissue) creature.LastDensityHere = rationed;
                }

                WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseShares);

                if (ledger.PoolDrawn > 0f)
                {
                    float taken = Nutrients.Take(creature.Point, ledger.PoolDrawn);
                    DetritusTakenTotal += taken;

                    // Credited what it got. Take is a partial-take API and returns what was
                    // there; a ledger that kept its planned income when the water gave less
                    // was creating energy (the Astra review's R1). Unreachable while the
                    // allowance above holds, and counted so that a hole shows rather than hides.
                    if (taken < ledger.PoolDrawn)
                    {
                        PoolShortTakes++;
                        ledger = ledger.WithPoolDrawn(taken);
                    }
                }

                // D098's leg 1, the other half of the transfer. Light does not create energy any
                // more; it charges a spent unit, and the unit has to actually be there. The same
                // partial-take rule the charged field has had since the Astra review's R1: the
                // producer is credited what the water gave.
                if (Keeps(creature))
                {
                    ledger = FixKept(creature, ledger);
                }
                else if (ledger.LightIncome > 0f)
                {
                    float wanted = ledger.LightIncome / Config.JoulesPerUnit;
                    float tookUnits = Matter.Take(creature.Point, wanted);

                    if (tookUnits < wanted)
                    {
                        FixationShortTakes++;
                        ledger = ledger.WithLightIncome(tookUnits * Config.JoulesPerUnit);
                    }
                }

                // Read against the capacity the cells recorded, which is what the light offered
                // before uptake bound it. Counted per body-step rather than per part, because the
                // share the table prints is a share of bodies that were held short.
                if (creature.HasPhotosyntheticTissue || ledger.LightCapacity > 0f)
                {
                    PhotosyntheticSteps++;
                    if (ledger.UptakeLimited) UptakeLimitedSteps++;
                }

                if (creature.HasAbsorptiveTissue)
                {
                    creature.LastShare = share;
                    creature.LastLedger = ledger;
                    creature.LastStepSeconds = seconds;
                }
                else if (Config.PlantsKeepOwnFirst && creature.HasPhotosyntheticTissue)
                {
                    // D207: every burn reads its leaves' capacity from here, the side burns between
                    // metabolic steps included. Saved with the body, so a resume reads the same.
                    creature.LastLedger = ledger;
                }

                // Drained here and nowhere else. Both branches above priced the same joules, so
                // this is the one point at which they stop being owed.
                creature.PendingWorkJoules = 0f;

                // Refreshed from the ledger rather than left at its birth value, because under
                // senescence the cost of doing nothing is not a property of the body alone
                // (D038). Free: upkeep and neural are exactly what StandingWatts recomputes, and
                // they were just computed. Without this a creature's SecondsOfReserve — and
                // §4.4's Energy sensor, when it exists — would grow more optimistic the closer it
                // came to starving.
                creature.StandingWatts = (ledger.Upkeep + ledger.Neural) / seconds;

                creature.Lifetime += ledger;

                // D098's leg 2, and it replaces `creature.Energy += ledger.Net`. A body spends
                // charged units and returns them spent, so what it spends is bounded by what it
                // holds: there is no such thing as burning a joule the body does not have, and
                // the reserve that used to go negative for one step before the death check is
                // now clamped at zero by the arithmetic rather than by the check. A body that
                // could not pay in full dies this step, which is the same death §5A.6 always had.
                double before = creature.Energy;
                double burnable = Math.Max(0d, before + ledger.Income - ledger.Exuded);
                double burnt = Math.Min(ledger.Expenditure, burnable);
                // Applied as one small delta on the reserve, never as `before + income - burnt`
                // through two roundings of a 200 J intermediate. That shape was written when the
                // reserve was a float and one rounding was all that could be afforded; in double
                // it is simply the form that keeps the delta and the books the same expression.
                // A body that could not pay is set to exactly zero, so the death check below
                // never sees a rounding crumb.
                creature.Energy = burnt < ledger.Expenditure
                    ? 0d
                    : before + (ledger.Income - ledger.Exuded - burnt);

                // D070. What the body released to the water this step, put where the body is.
                // The reserve update above already carried the deduction, so this is the other
                // half of a transfer that is complete only once the field holds it — and it
                // happens *before* the death check below, so a creature that exudes and then
                // starves in the same step gives the water both: this step's release, and its
                // tissue.
                if (ledger.Exuded > 0f)
                {
                    Nutrients.Deposit(creature.Point, ledger.Exuded);
                    DetritusExudedTotal += ledger.Exuded;
                }

                // Only fixation is new energy, and it was taken out of the spent field a few
                // lines above: a joule in is a charged unit, never a joule from nowhere.
                EnergyIn += ledger.LightIncome;

                // The burn, in both books at once. The joules leave the world as heat and the
                // same over ρ arrives in the spent field where the body is — the whole of D052's
                // excretion leg, at exactly 1 / ρ and with no knob of its own.
                if (burnt > 0d)
                {
                    EnergyOut += burnt;

                    // Through the field's exact door (round 52's build). Until then this was a
                    // float, and the water gained half an ulp less or more than the reserve gave
                    // up. In round 51 those halves kept one sign for thousands of seconds and were
                    // 98% of B1's matter residual (logbook/specs/r51-read/b1-probe.txt).
                    // D206: a plant keeps the units of what it burns (ReturnSpent). Off, this is
                    // the deposit and the count it always was.
                    ReturnSpent(creature, burnt / Config.JoulesPerUnit);
                }

                // D162: the grips formed since the last metabolic step, charged as a burn is.
                if (creature.PendingGripJoules > 0d) ChargeGrips(creature);

                // D098's leg 3. What the mouth tore up and did not keep is charged matter still,
                // and it goes back into the water where the feeder is rather than leaving the
                // world. The transfer loss that shortens a food chain is unchanged in size; what
                // changed is that it is now somebody else's food.
                if (ledger.Wasted > 0d)
                {
                    // Deposited as the float the field takes and booked as the same float, so the
                    // water's account and the flux counter cannot disagree by a rounding.
                    float faeces = (float)ledger.Wasted;
                    Nutrients.Deposit(creature.Point, faeces);
                    DetritusReturnedTotal += faeces;
                }

                // D098's leg 10. A hoard above the cap is released to the water as charged
                // matter, where a hoard burnt as senescence upkeep would have left the world as
                // heat (logbook/0101). Counted with exudation, because to the charged field's
                // flux identity it is the same event: a living body putting charged matter in.
                // Off at ReserveCapSeconds 0, which is every world before this one.
                //
                // A gestating body (2026-09-24) is trimmed in Gestate instead, after it has banked
                // its share: trimmed here, a body at its cap would clear nothing on the step, bank
                // nothing, and never breed. Every lump breeder is trimmed here as it always was.
                WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseBooks);
                if (!creature.Gestates) TrimReserve(creature);

                if (creature.Energy > 0d)
                {
                    // D128: a solvent body can still die of age. Its reserve goes to its corpse.
                    bool aged = hazard && AgedAt(creature, i);
                    WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseAge);
                    if (aged)
                    {
                        DeathsByAge++;
                        Bury(creature, i, DeathCause.Aged);
                        WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseBurials);
                    }

                    continue;
                }

                WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseAge);

                // §5A.6 kills at exactly zero energy and nothing else — the ecology has one cause
                // of death, and senescence (D038) raises upkeep until this fires sooner rather
                // than opening a second way to die. DeathCause.Diverged is not a second way
                // either: it is the solver failing, and it enters through KillDiverged below.
                Bury(creature, i, DeathCause.Starved);
                WorldPhaseProbe.Mark(WorldPhaseProbe.MetaboliseBurials);
            }
        }

        /// <summary>
        /// D098's leg 10 for one body: a reserve above <see cref="RunConfig.ReserveCapSeconds"/> of
        /// its standing cost is released to the water as charged matter.
        /// </summary>
        /// <summary>
        /// One body's bill at the full local density: <see cref="Metabolise"/>'s appetite, what
        /// the body would eat if it were alone.
        /// </summary>
        /// <param name="creature">The body.</param>
        /// <param name="seconds">The step.</param>
        /// <param name="byExposure">Whether the light is priced by the pose (D110).</param>
        /// <param name="timed">Whether to add the bill's own wall to <see cref="LedgerTicks"/>:
        /// only on one thread, where nothing else writes it.</param>
        private EnergyLedger BillAtFullDensity(
            Organism creature, float seconds, bool byExposure, bool timed)
        {
            float density = Nutrients.EdibleDensityAt(creature.Point);
            float spent = Matter.DensityAt(creature.Point);

            float[] exposure = byExposure ? creature.CurrentExposure : null;

            // LedgerTicks' bracket: a clock read either side, nothing the bill reads.
            long billStarted = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            EnergyLedger ledger = exposure == null
                ? Metabolism.StepAt(
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z),
                    density, spent, creature.PendingWorkJoules, seconds, creature.Age)
                : Metabolism.StepAt(
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z),
                    density, spent, creature.PendingWorkJoules, seconds, creature.Age,
                    exposure, creature.UpInBody);

            // D219: a grazed leaf fixes less light. Its own statement; with the dial off it never runs.
            if (Config.ContactEating && creature.PartHealth != null)
            {
                ledger = Metabolism.StepAtHealth(
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch, creature.X, creature.Z),
                    density, spent, creature.PendingWorkJoules, seconds, creature.Age,
                    exposure, creature.UpInBody, creature.PartHealth);
            }

            if (timed) LedgerTicks += System.Diagnostics.Stopwatch.GetTimestamp() - billStarted;

            // The absorptive log's capture, taken where the number is — one field write, on the
            // pass that already read it, and only for the creatures the file records
            // (AbsorptiveSample). It has to be here rather than at report time: the field is
            // emptied by Take, settled, mixed and advected between this instant and the next
            // sample, so asking again later would produce a plausible density that is not the one
            // this creature was priced against. The rationed branch below overwrites it with what
            // it actually re-priced.
            if (creature.HasAbsorptiveTissue) creature.LastDensityHere = density;

            return ledger;
        }

        /// <summary>
        /// The cap on this body's reserve now, J, or <see cref="double.PositiveInfinity"/> where none binds: a world
        /// with <see cref="RunConfig.ReserveCapSeconds"/> at 0, or a body with no standing cost.
        /// </summary>
        /// <remarks>
        /// Under <see cref="ReserveCapForm.Seconds"/> it is D098's <c>s × W</c>, the expression the trim always read.
        /// Under <see cref="ReserveCapForm.BodyAndStore"/> it is D229's, taken off the body as it stands and never
        /// held between steps: <c>P + R + s × W + Σ room × volume</c>. P is the next litter's price as the breeding
        /// gate computes it (<see cref="ReproductionTraits.CostJoules(double, RunConfig)"/> at the body's tissue,
        /// the expression <see cref="Organism.BreedingGate(RunConfig)"/> and <see cref="Organism.LumpGate"/> add
        /// the margin to), R is the adult tissue less the tissue now and not below 0, W is
        /// <see cref="Organism.StandingWatts"/>, and each part adds its type's
        /// <see cref="CellType.StoreRoomJoulesPerCubicMetre"/> times its volume. The margin gene is left out on
        /// purpose, so a lineage cannot raise its store by evolving caution.
        /// </remarks>
        public double ReserveCapJoules(Organism creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (!(Config.ReserveCapSeconds > 0f && creature.StandingWatts > 0f)) return double.PositiveInfinity;

            return Config.ReserveCapForm == ReserveCapForm.BodyAndStore
                ? BodyAndStoreCap(creature)
                : (double)Config.ReserveCapSeconds * creature.StandingWatts;
        }

        private double BodyAndStoreCap(Organism creature)
        {
            double tissue = creature.TissueJoules;
            double price = creature.Genome.Reproduction.CostJoules(tissue, Config);
            double growth = creature.AdultTissueJoules - tissue;
            if (!(growth > 0d)) growth = 0d;

            double cap = price + growth + (double)Config.ReserveCapSeconds * creature.StandingWatts;

            IReadOnlyList<PhenotypePart> parts = creature.Phenotype.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                PhenotypePart part = parts[i];
                float room = Config.CellTypes.Resolve(part.CellTypeId).StoreRoomJoulesPerCubicMetre;
                if (room > 0f) cap += (double)room * Math.Max(0f, part.Volume);
            }

            return cap;
        }

        /// <summary>
        /// D247, for a stats row: the living read against the reserve cap and the fat in them. O(parts of the living),
        /// taken at a sample and never in a step.
        /// </summary>
        /// <remarks>
        /// <see cref="ReserveCapCensus.NearCap"/> counts the living whose reserve is within 1% of their cap; a body
        /// with no cap, in a world with none, is never near it. <see cref="ReserveCapCensus.MarginAboveCap"/> counts
        /// the lump breeders whose margin gene's seconds exceed <see cref="RunConfig.ReserveCapSeconds"/>, which the
        /// body-and-store cap leaves unable to reach their own gate; 0 under the other form.
        /// </remarks>
        public ReserveCapCensus CensusReserveCap()
        {
            var census = new ReserveCapCensus();
            bool store = Config.ReserveCapForm == ReserveCapForm.BodyAndStore;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                double cap = ReserveCapJoules(creature);
                if (!double.IsPositiveInfinity(cap) && creature.Energy >= 0.99d * cap) census.NearCap++;

                if (store && creature.Genome.Reproduction.Mode == ReproductionMode.Lump &&
                    creature.Genome.Reproduction.ReserveMargin > Config.ReserveCapSeconds)
                {
                    census.MarginAboveCap++;
                }

                bool fat = false;
                IReadOnlyList<PhenotypePart> parts = creature.Phenotype.Parts;
                for (int p = 0; p < parts.Count; p++)
                {
                    double volume = Math.Max(0f, parts[p].Volume);
                    census.Volume += volume;
                    if (parts[p].CellTypeId == CellTypeIds.Fat)
                    {
                        census.FatVolume += volume;
                        if (volume > 0d) fat = true;
                    }
                }

                if (fat) census.FatCarriers++;
            }

            return census;
        }

        private void TrimReserve(Organism creature)
        {
            if (!(Config.ReserveCapSeconds > 0f && creature.StandingWatts > 0f)) return;

            double cap = Config.ReserveCapForm == ReserveCapForm.BodyAndStore
                ? BodyAndStoreCap(creature)
                : (double)Config.ReserveCapSeconds * creature.StandingWatts;
            if (!(creature.Energy > cap)) return;

            // The trim is the float the water is handed, and the reserve gives up exactly that
            // rather than being set to the cap: what leaves one account is what arrives in the
            // other, and the reserve ends within an ulp of the cap instead of the transfer ending
            // within an ulp of exact.
            float excess = (float)(creature.Energy - cap);
            if (excess > 0f)
            {
                creature.Energy -= excess;
                Nutrients.Deposit(creature.Point, excess);
                DetritusExudedTotal += excess;
                ReserveTrimmedTotal += excess;
            }
        }

        /// <summary>Fills <see cref="Organism.ReserveAtStepStart"/> for every gestating body.</summary>
        private void MarkReserveAtStepStart()
        {
            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (creature.Gestates) creature.ReserveAtStepStart = creature.Energy;
            }
        }

        /// <summary>
        /// Moves each gestating body's share of this step's positive net from its reserve into its
        /// gestation account — the owner's ruling of 2026-09-24.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The net is the reserve's own change over the step</b>, from
        /// <see cref="MarkReserveAtStepStart"/> to here: light fixed and food eaten, less the
        /// burn, the exudation, the reserve cap's trim, a repair and a bite. That is what the
        /// body actually cleared, whatever route the income took, so an eater's meal counts the
        /// way a leaf's light does. A step that cleared nothing banks nothing, and the account is
        /// never drawn back into the reserve: upkeep does not touch it.
        /// </para>
        /// <para>
        /// <b>A transfer inside one body, so neither book moves.</b> Both accounts are in
        /// <see cref="StandingJoules"/>; the reserve can only shrink toward what it began the
        /// step at, which is above zero for any body alive to reach this pass.
        /// </para>
        /// <para>
        /// <b>The reserve cap bites here for a gestating body</b>, after the banking, so a body
        /// standing at its cap still banks its share of what it cleared; the step's trim is part
        /// of this pass and not of the net it reads.
        /// </para>
        /// </remarks>
        private void Gestate()
        {
            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (!creature.Gestates) continue;

                double net = creature.Energy - creature.ReserveAtStepStart;

                if (net > 0d)
                {
                    double moved = net * creature.Genome.Reproduction.GestationShare;
                    if (moved > creature.Energy) moved = creature.Energy;

                    if (moved > 0d)
                    {
                        creature.Energy -= moved;
                        creature.GestationJoules += moved;
                        GestatedTotal += moved;
                    }
                }

                // The reserve cap, after the banking: see Metabolise's leg 10 for why a gestating
                // body is trimmed here. The account is not trimmed; it is the litter's.
                TrimReserve(creature);
            }
        }

        /// <summary>
        /// One step of growth for every body below its adult size — fable-propose-growth.md
        /// rule 5 (2026-09-08).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Growth is a transfer, and both books close because it is only ever a transfer.</b>
        /// Energy moves out of <see cref="Organism.Energy"/> and into
        /// <see cref="Organism.TissueJoules"/>, and §5A.2's audit sums both, so nothing is created
        /// or destroyed and <see cref="AuditResidual"/> never learns that growth happened. Since
        /// D098 that is the whole of it: a body's reserve and its tissue are the same charged
        /// matter, so moving one into the other draws on no field at all and
        /// <see cref="MatterResidual"/> never learns either.
        /// </para>
        /// <para>
        /// <b>Two bounds, and the smaller wins.</b> What the reserve can spare above
        /// <see cref="RunConfig.GrowthReserveFloor"/> of the body it already has, and what is left
        /// of the adult. The third — what the water within reach would sell — went with D098's
        /// matter price; a body that cannot grow now is a body that cannot earn.
        /// </para>
        /// <para>
        /// <b>The body is authoritative and the ledger is charged what the body cost.</b> The new
        /// tissue figure is re-measured with <see cref="Metabolism.TissueJoules"/> on the scaled
        /// phenotype rather than accumulated from the increments, so the number the world holds
        /// and the number the body is worth cannot drift apart over ten thousand steps of
        /// rounding — and the reserve is debited exactly that difference, so the transfer is still
        /// a transfer.
        /// </para>
        /// <para>
        /// <b>Growth is not a rate, which is why this takes no step length</b>, with
        /// <see cref="RunConfig.GrowthAdultSeconds"/> at 0. A body invests everything above the
        /// floor every time this runs, so how fast it reaches its adult size
        /// is set by what it can earn and what the water will sell it, not by a growth constant
        /// nobody would know how to choose. The consequence is that the step length does change
        /// how many bites the same growth is taken in, and that is the honest situation: rule 5
        /// gave the world an appetite, not a schedule.
        /// </para>
        /// <para>
        /// <b>D241: with <see cref="RunConfig.GrowthAdultSeconds"/> above 0 growth is a climb, and
        /// it does take the step's world seconds.</b> <paramref name="seconds"/> is
        /// <see cref="Step"/>'s own, the world seconds of one metabolic step, whatever the
        /// physics did inside it, so the climb is the same at any pace factor. Each juvenile is
        /// then offered the rate law's allowance (<see cref="ClimbInvestment"/>); at 0 the climb is
        /// never entered and the pass is the one above, statement for statement.
        /// </para>
        /// <para>
        /// <b>Nothing here draws from an <see cref="Rng"/>.</b> Growth is arithmetic on a body and
        /// a cell, so a world with growth switched off by every creature already being adult steps
        /// through exactly the trajectory it would have without this pass.
        /// </para>
        /// </remarks>
        private void Grow(float seconds)
        {
            float reserveFloor = Config.GrowthReserveFloor;

            // D241. The climb's one factor for the step: exp(-dt / tau). Tau is the same for every
            // body (one T), so it is taken once and not once a juvenile. Off, nothing here is read
            // and no exponential is taken.
            bool climb = Config.GrowthAdultSeconds > 0f;
            double climbDecay = climb
                ? Math.Exp(-(double)seconds / ((double)Config.GrowthAdultSeconds / ClimbTimeConstants))
                : 0d;

            // The juveniles first, in living order. They are the newest bodies and sit at the
            // list's tail, so slabs over the whole list would leave one thread most of the work.
            _juveniles.Clear();
            for (int i = 0; i < _living.Count; i++)
            {
                if (_living[i].BodyFraction < 1f) _juveniles.Add(i);
            }

            WorldPhaseProbe.Count(WorldPhaseProbe.Juveniles, _juveniles.Count);

            // Each body's growth reads its own state, the config and registries nothing writes,
            // and writes its own fields alone: no sum, no field, no list, no draw. So the bodies
            // grow in any order, on any thread, to the same bits.
            Parallelism.ForRanges(_juveniles.Count, (from, to) =>
            {
                for (int j = from; j < to; j++) GrowOne(_living[_juveniles[j]], reserveFloor, climb, climbDecay);
            });
        }

        // The juveniles' indices into _living, Grow's own scratch.
        private readonly List<int> _juveniles = new List<int>();

        /// <summary>
        /// D241: the number of time constants a body takes to climb from 0.65 of its adult's tissue to
        /// 0.95 of it, <c>ln((1 - 0.65^(1/4)) / (1 - 0.95^(1/4)))</c>, about 2.081. With
        /// <c>r = (m/M)^(1/4)</c>, 1 - r falls exponentially at the rate law's time constant, so
        /// <c>tau = T / ClimbTimeConstants</c> makes <see cref="RunConfig.GrowthAdultSeconds"/> the
        /// time a body born at 0.65 takes to reach 0.95 at the law's full rate.
        /// </summary>
        private static readonly double ClimbTimeConstants =
            Math.Log((1d - Math.Pow(0.65d, 0.25d)) / (1d - Math.Pow(0.95d, 0.25d)));

        /// <summary>The fraction of its adult's tissue at which the climb takes the rest in one step (D241).</summary>
        private const double ClimbSnapFraction = 0.95d;

        /// <summary>
        /// D241: what a body may invest in one step, in joules: the smaller of the rate law's allowance
        /// and what the reserve can spare above the gene's <c>keep</c>, capped by what is left of the
        /// adult. A body that cannot spare anything above <c>keep</c> gets 0 or less and does not grow.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The law, and its exact step.</b> West, Brown and Enquist's ontogenetic growth,
        /// <c>dm/dt = a m^(3/4) (1 - (m/M)^(1/4))</c>, with <c>m</c> the body's tissue joules and
        /// <c>M</c> its adult's. With <c>r = (m/M)^(1/4)</c> it is <c>dr/dt = (a / 4 M^(1/4)) (1 - r)</c>,
        /// so <c>1 - r</c> falls exponentially with the time constant <c>tau = T / 2.081</c>
        /// (<see cref="ClimbTimeConstants"/>). Taken exactly rather than by Euler, a step of length
        /// <c>dt</c> lands on <c>r' = 1 - (1 - r) exp(-dt / tau)</c>, so the allowance
        /// <c>M r'^4 - m</c> matches the closed form at any step length. It is computed in double,
        /// from the intact tissue the caller measured (D219); <paramref name="decay"/> is
        /// <c>exp(-dt / tau)</c>, taken once a step.
        /// </para>
        /// <para>
        /// <b>The snap.</b> The law approaches the adult without reaching it. A step that would carry
        /// the body to <see cref="ClimbSnapFraction"/> of the adult (<c>r'^4 &gt;= 0.95</c>) is
        /// allowed everything that remains, so adulthood is reached at T and not merely 95 % of the
        /// way; the reserve above <c>keep</c> still has to pay for it.
        /// </para>
        /// <para>
        /// <b>keep.</b> <c>max(GrowthReserveFloor x m, G x StandingWatts)</c>: the tissue floor of
        /// rule 5, and the creature's own gene in seconds of its standing cost. Every genome admitted
        /// at T above 0 carries the gene (<see cref="Admit"/>).
        /// </para>
        /// </remarks>
        private double ClimbInvestment(
            Organism creature, double adultTissue, double tissueNow, double remaining,
            float reserveFloor, double decay)
        {
            float? gene = creature.Genome.GrowthReserveSeconds;
            if (!gene.HasValue)
            {
                throw new InvalidOperationException(
                    "Organism " + creature.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " carries no growth-reserve gene in a world with GrowthAdultSeconds above 0. " +
                    "Admission gives every body one (D241).");
            }

            double r = Math.Pow(Math.Max(tissueNow, 0d) / adultTissue, 0.25d);
            double rNext = 1d - (1d - r) * decay;
            double rNext4 = rNext * rNext * rNext * rNext;

            double allowance = rNext4 >= ClimbSnapFraction
                ? remaining
                : adultTissue * rNext4 - tissueNow;

            double keep = Math.Max(
                (double)reserveFloor * tissueNow,
                (double)gene.Value * creature.StandingWatts);

            double invest = Math.Min(allowance, creature.Energy - keep);
            return Math.Min(invest, remaining);
        }

        private void GrowOne(Organism creature, float reserveFloor, bool climb, double climbDecay)
        {
            double adultTissue = creature.AdultTissueJoules;

            // D219: a bitten body is measured intact, the account plus what has been eaten of it, so a bitten
            // adult is not mistaken for a juvenile and re-scaled smaller. Off, the account is the body.
            double tissueNow = creature.TissueJoules;
            if (Config.ContactEating) tissueNow = IntactTissue(creature);

            double remaining = adultTissue - tissueNow;
            if (!(remaining > 0d)) return;

            // What the reserve can spare. The floor is a fraction of the body this creature
            // already has, so a large body keeps a large buffer and a newborn keeps a small
            // one — the same proportion of the same thing at every size.
            //
            // D241: with GrowthAdultSeconds above 0 the amount is the climb's instead (ClimbInvestment),
            // and from the line after this the arithmetic is the same.
            double target;
            if (climb)
            {
                target = ClimbInvestment(creature, adultTissue, tissueNow, remaining, reserveFloor, climbDecay);
            }
            else
            {
                target = creature.Energy - (double)reserveFloor * tissueNow;
                if (target > remaining) target = remaining;
            }

            if (!(target > 0d)) return;

            double fraction = (tissueNow + target) / adultTissue;
            if (fraction > 1d) fraction = 1d;

            // D241. The snap takes the whole remainder, and tissueNow + (adultTissue - tissueNow) can land one
            // ulp under the adult; the body is then adult, exactly, and not a juvenile a hair short of it.
            if (climb && target >= remaining) fraction = 1d;

            // The cube root, because the fraction is a volume and the parts are scaled by a
            // length. Phenotype.Scaled says why the adult is the thing scaled from.
            Phenotype grown = fraction >= 1d
                ? creature.AdultPhenotype
                : creature.AdultPhenotype.ScaledFor(
                    Config, (float)Math.Pow(fraction, 1d / 3d));

            double actual = fraction >= 1d ? adultTissue : Metabolism.TissueJoules(grown, Config);
            double spend = actual - creature.TissueJoules;

            // D219: the account the body will hold at the new size is the tissue of the parts it holds, and the
            // reserve pays only for that rise: the hole an eaten part leaves grows with the body for nothing,
            // so what the reserve pays is what the account gains and no joule goes missing.
            double account = actual;
            if (Config.ContactEating && creature.PartHealth != null)
            {
                account = UneatenTissue(grown, creature.PartHealth);
                spend = account - creature.TissueJoules;
            }

            // The one case this refuses: a body whose re-measured tissue costs a hair more
            // than the reserve holds. It is reachable only when the increment is already down
            // in the rounding, and growing anyway would take a creature to a negative reserve
            // — a death by bookkeeping rather than by starvation, which §5A.6 does not have.
            if (spend > creature.Energy) return;

            WorldPhaseProbe.Count(WorldPhaseProbe.Grown, 1);
            creature.Energy -= spend;
            creature.TissueJoules = account;
            creature.Phenotype = grown;
            creature.BodyFraction = actual >= adultTissue ? 1f : (float)(actual / adultTissue);

            // The two cached readings of a body that has just changed size. The standing cost
            // is asked at this creature's own age so that growing does not quietly reset its
            // senescence (D038); the absorptive volume is instrumentation, and a mouth that
            // grew must be measured as the mouth it now is.
            creature.StandingWatts = Metabolism.StandingWatts(grown, Config, creature.Age);
            creature.AbsorptiveVolume = AbsorptiveVolumeOf(grown);
        }

        /// <summary>
        /// D241: puts a living body at its adult size at once, with no growth step and no joule moved
        /// through its reserve. For a caller that places a body meaning "grown", as the nursery's tanks
        /// do; under the climb, paying such a body its owed tissue as reserve would buy a climb of up to
        /// 1.58 T, not a grown body.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it writes is what a growth step's last line writes for an adult</b>: the tissue
        /// account, the phenotype (the adult itself, not a copy), the body fraction, and the two cached
        /// readings. The tissue is a gift, booked as income in the energy audit and the matter identity as
        /// a founder's start is, and counted in <see cref="AdultGiftJoules"/>.
        /// </para>
        /// <para>
        /// Refused for a body that is not among the living, and for one whose plan has changed since it
        /// was admitted (a bite or the module rule), since its adult is then not the body the plan
        /// describes. Under D219 the account is the adult's uneaten tissue, as a growth step's is.
        /// </para>
        /// </remarks>
        public void MakeAdult(Organism creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            if (!_living.Contains(creature))
            {
                throw new InvalidOperationException(
                    "Organism " + creature.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " is not among the living, so it cannot be made adult (D241).");
            }

            if (creature.PlanRevision != 0)
            {
                throw new InvalidOperationException(
                    "Organism " + creature.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " has a plan changed since its admission, so its adult is not the body its plan " +
                    "describes and it cannot be made adult (D241).");
            }

            Phenotype adult = creature.AdultPhenotype;
            double account = creature.AdultTissueJoules;
            if (Config.ContactEating && creature.PartHealth != null)
            {
                account = UneatenTissue(adult, creature.PartHealth);
            }

            // The gift is created, as a founder's start is (Admit's leg 9), so both books take it as
            // income: the energy audit and the matter identity close over it, and the record can say
            // how much the placements gave.
            double gift = account - creature.TissueJoules;
            EnergyIn += gift;
            MatterInfluxedTotal += gift / Config.JoulesPerUnit;
            AdultGiftJoules += gift;

            creature.TissueJoules = account;
            creature.Phenotype = adult;
            creature.BodyFraction = 1f;
            creature.StandingWatts = Metabolism.StandingWatts(adult, Config, creature.Age);
            creature.AbsorptiveVolume = AbsorptiveVolumeOf(adult);
        }

        /// <summary>Volume of <see cref="CellTypeIds.Absorptive"/> tissue in a body, m³.</summary>
        /// <remarks>
        /// One walk, two callers — <c>Admit</c> at birth and <see cref="Grow"/> whenever the body
        /// changes size. Written once because the two must agree about what a mouth is, which is
        /// the reason <see cref="HasAbsorptive"/> exists in this file rather than in two.
        /// </remarks>
        private static float AbsorptiveVolumeOf(Phenotype phenotype)
        {
            float volume = 0f;
            IReadOnlyList<PhenotypePart> parts = phenotype.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].CellTypeId == CellTypeIds.Absorptive) volume += parts[i].Volume;
            }
            return volume;
        }

        // Metabolise's pre-pass, parallel to _living at the walk's start. A burial removes the
        // body at the walk's own index, which moves only the bodies the walk has already passed,
        // so an index still names the body the pre-pass read. Reused, never reallocated smaller.
        private float[] _turnShare = Array.Empty<float>(), _turnSpentShare = Array.Empty<float>();
        private double[] _turnClock = Array.Empty<double>(), _turnHazard = Array.Empty<double>(),
            _turnThreshold = Array.Empty<double>();

        private void EnsureTurnScratch(int count)
        {
            if (_turnShare.Length >= count) return;
            int size = Math.Max(count, 2 * _turnShare.Length);
            _turnShare = new float[size];
            _turnSpentShare = new float[size];
            _turnClock = new double[size];
            _turnHazard = new double[size];
            _turnThreshold = new double[size];
        }

        // Whether a field's ShareAt reads only the arrays FreezeAvailability froze, and so may be
        // asked for every body ahead of the walk and on any thread.
        private static bool FrozenShares(IMatterField field) => field is GridField || field is NutrientField;

        /// <summary>
        /// D128: a solvent body's ageing over one metabolic step, written on the body at the walk's
        /// point with the numbers Metabolise's pre-pass took (<see cref="AgeStep"/>): the clock
        /// advanced, the hazard the step accumulated added, and whether it has passed the body's
        /// threshold.
        /// </summary>
        private bool AgedAt(Organism creature, int i)
        {
            creature.AgeClock = _turnClock[i];
            creature.AgeHazard += _turnHazard[i];
            return creature.AgeHazard >= _turnThreshold[i];
        }

        /// <summary>
        /// D128: one metabolic step of a body's ageing, computed and not applied: the clock it
        /// advances to, returned, and the hazard the step adds. <paramref name="k"/> is
        /// <see cref="AgeHazardScale"/>. Reads the body and writes nothing.
        /// </summary>
        private double AgeStep(Organism creature, float seconds, double k, out double added)
        {
            double doubling = AgeDoublingSeconds(creature);
            double c0 = creature.AgeClock;
            double c1 = c0 + seconds;

            // K (2^(c1/D) - 2^(c0/D)): the hazard accumulated over the step, exactly, whatever the
            // step's length. A clock pushed past a thousand doublings overflows both powers, and a
            // body that old is dead: an overflow or its NaN reads as an infinite hazard.
            added = k * (Math.Pow(2d, c1 / doubling) - Math.Pow(2d, c0 / doubling));
            if (!(added < double.PositiveInfinity)) added = double.PositiveInfinity;

            return c1;
        }

        /// <summary>
        /// D128: a body's hazard doubling time, s: the world's doubling fraction of the body's own
        /// base, which is the world's base stretched by the body's lifespan gene.
        /// </summary>
        internal double AgeDoublingSeconds(Organism creature) =>
            (double)Config.AgeHazardDoublingFraction * Config.AgeHazardBaseSeconds *
            creature.Genome.LifespanScale;

        /// <summary>
        /// D128: <c>K = ln 2 / (2^(1/f) - 1)</c>, the scale that puts the median age at death at a
        /// body's base: the accumulated hazard <c>K (2^(c/D) - 1)</c> reaches <c>ln 2</c> at
        /// <c>c = D / f</c>, the base, for any doubling fraction <c>f</c>.
        /// </summary>
        internal double AgeHazardScale() =>
            Math.Log(2d) / (Math.Pow(2d, 1d / Config.AgeHazardDoublingFraction) - 1d);

        /// <summary>
        /// D128: the hazard at which this body dies of age, an exponential draw with mean 1 from
        /// <see cref="AgeDeathIndex"/> and the body's id. Stateless: asked every step and the
        /// same every time, so nothing is stored and a restore needs nothing.
        /// </summary>
        public double AgeThreshold(Organism creature)
        {
            var rng = new Rng(Rng.SeedFor(Rng.SeedFor(Seed, AgeDeathIndex), (ulong)creature.Id));
            double u = (rng.NextUInt() + 0.5d) / 4294967296d;
            return -Math.Log(u);
        }

        /// <summary>
        /// D128, the owner's additions: push a body's ageing clock ahead by
        /// <paramref name="doublings"/> of its hazard's doubling time. Under Gompertz's law this
        /// multiplies the hazard from here on by <c>2^doublings</c> and adds none at the push.
        /// </summary>
        internal void PushAgeClock(Organism creature, double doublings)
        {
            if (!(doublings > 0d)) return;
            creature.AgeClock += doublings * AgeDoublingSeconds(creature);
        }

        /// <summary>
        /// Removes a creature from the population and settles its books — the only place a body
        /// leaves the world.
        /// </summary>
        /// <param name="creature">The body leaving the population.</param>
        /// <param name="index">Its position in <c>_living</c>, which the caller already knows.</param>
        /// <param name="cause">What the lineage row will say.</param>
        /// <remarks>
        /// <b>One copy of the deposit logic, reached by both causes.</b> Starvation walks the
        /// population and finds this at the bottom of the metabolic loop;
        /// <see cref="KillDiverged"/> arrives from outside <see cref="Step"/> with a body the
        /// physics has already destroyed. A second copy of "deposit what the body was, count the
        /// death" for the second caller is exactly how the two would come to disagree about the
        /// audit, and the audit is the one thing here that cannot be allowed to drift.
        /// <para>
        /// <b>D098: a corpse is the tissue and the reserve, and nothing leaves the world here.</b>
        /// Until D098 a death booked the body's whole reserve as an outflow — heat — so a
        /// diverged body's savings were discarded and a senescent body's had already been burnt
        /// as upkeep (logbook/0101). A reserve is charged matter like any other, so it goes into
        /// the water with the tissue and feeds somebody.
        /// </para>
        /// </remarks>
        private void Bury(Organism creature, int index, DeathCause cause, long killedBy = -1L)
        {
            // The absorptive log's final row, taken before the death path zeroes anything:
            // this is the terminal budget, and the reserve it records is the (negative)
            // overdraft that killed the creature rather than the 0 the next line writes.
            // Starvation is the only cause the ecology has, so cause of death discriminates
            // nothing among these rows — this row is what does.
            if (creature.HasAbsorptiveTissue) BufferAbsorptiveDeath(creature);

            // Round 49's two readings for the death row, taken before the lines below zero both
            // accounts: the gestation account the body died holding (ga) and the reserve of a body
            // that died solvent (res). Each is written only when above 0, so a lump breeder's
            // starvation row is the row it always was.
            double accountAtDeath = creature.GestationJoules;
            double reserveAtDeath = creature.Energy;

            // What the body is worth, in one account: the tissue it grew and whatever reserve it
            // still held. A starved body's reserve is 0 by leg 2, so a starvation corpse is the
            // tissue exactly as it always was; a diverged body is generally solvent, and this is
            // the line that stopped throwing its savings away.
            double remains = creature.TissueJoules + Math.Max(0d, creature.Energy);
            creature.Energy = 0d;

            // The gestation account (2026-09-24) goes with the reserve, because it is the same
            // charged matter set aside. Added in its own statement so that at 0 — every lump
            // breeder — the corpse is the same number it always was.
            if (creature.GestationJoules > 0d) remains += creature.GestationJoules;
            creature.GestationJoules = 0d;

            // D206: the spent units a plant kept. A corpse takes them and lets them go with its
            // instalments; any other death puts them in the spent field where the body lay. Zero
            // for every body with PlantsKeepMatter off, so nothing below moves.
            double held = creature.SpentUnits;
            creature.SpentUnits = 0d;
            bool heldToCorpse = false;

            // Rule 6 of fable-propose-grid.md: above zero the body leaves a corpse instead, and
            // the deposit below happens in instalments from wherever the corpse has drifted to.
            // Nothing else about the death changes, and the lineage row is the same row. Guarded
            // on the knob rather than written as a general path, so at 0 the deposit below runs
            // exactly as it always has and every world on file replays.
            if (cause == DeathCause.Withered)
            {
                // WitherForTank: the nursery's switching tank withers a leaf. Burnt as BurnUnbitten burns, whatever the
                // config says of corpses and remains: no corpse is founded, no remains are cut, and nothing is left to eat.
                // A plant's held units go to the spent field below, as any death without a corpse sends them.
                BurnUnbitten(creature, remains);
                creature.TissueJoules = 0d;
            }
            else if (Config.ContactEating)
            {
                // D219, D220: no corpse is founded. The body itself becomes remains, keeping its tissue account and
                // taking its reserve and gestation account as one store (choice 4). A diverged body's geometry cannot
                // be trusted, so it is burnt and founds nothing (choice 7). A plant's held units went to the spent
                // field above, as a death without a corpse sends them (choice 8).
                double store = (reserveAtDeath > 0d ? reserveAtDeath : 0d) + (accountAtDeath > 0d ? accountAtDeath : 0d);

                if (Config.KillSwallowsBody)
                {
                    // D232: what the body still held in its gut goes with it, as part of its store: into its own
                    // remains, or burnt with a diverged body, or, if a bite killed it and its killer is a living body,
                    // into that killer's gut with the rest.
                    store += creature.GutJoules;
                    creature.GutJoules = 0d;

                    Organism swallower = cause == DeathCause.Eaten ? SwallowerOf(creature, killedBy) : null;
                    if (swallower != null)
                    {
                        SwallowWhole(swallower, creature, creature.TissueJoules, store);
                    }
                    else
                    {
                        LeaveRemains(
                            creature, creature.TissueJoules, store, wholeBody: true, killedBy: killedBy,
                            diverged: cause == DeathCause.Diverged);
                    }
                }
                else
                {
                    LeaveRemains(
                        creature, creature.TissueJoules, store, wholeBody: true, killedBy: killedBy,
                        diverged: cause == DeathCause.Diverged);
                }
            }
            else if (Config.UnbittenDeathsLeaveNoCorpse && (cause != DeathCause.Eaten || !creature.HasPhotosyntheticTissue))
            {
                // D167's leaf tank: only a leaf a bite killed leaves food. Every other death burns, a body without
                // photosynthetic tissue killed by a bite among them (the owner's ruling of 2026-10-13: the tank feeds on
                // leaves alone).
                BurnUnbitten(creature, remains);
                creature.TissueJoules = 0d;
            }
            else if (Config.CorpseDecayPerSecond > 0f && remains > 0d)
            {
                // A body with nothing to give founds nothing: a floor founder that never paid for
                // itself and developed into no tissue would otherwise leave an empty object for
                // the pass to carry and drop. A corpse's account is a double like the body's, so
                // this branch is exact — what the body was worth is what the corpse holds.
                CorpsesChanged();
                _corpses.Add(new Corpse(
                    creature.Id, creature.Point.Position, creature.Patch, remains)
                {
                    Body = creature.Phenotype,
                    ModuleCounts = creature.ModuleCounts,
                    LostPartPaths = creature.LostPartPaths,
                    ScaledBy = creature.Phenotype.ScaledBy,
                    SpentUnits = held,
                    KilledBy = killedBy,
                });
                heldToCorpse = true;

                // No counterpart line for either account: StandingJoules reads a living body's
                // tissue and reserve and a corpse's joules into the same total, and the body's
                // own are zeroed here.
                creature.TissueJoules = 0d;
            }
            else
            {
                // The body becomes detritus where it died — §5A.2c. This is the whole reason
                // anything other than a plant can live, and the reason the doomed half of
                // generation zero is the world's first food rather than merely a waste of seeds.
                // HeightY is the last height Observe accepted, and Observe refuses a non-finite one
                // — so this is the last *finite* depth even when the body's own transform is NaN.
                // The field takes a float and the body's accounts are doubles, so what is handed
                // over is quantised once and booked as the same number — half an ulp of a body's
                // worth per death, against the ulp of a whole reserve the float account was
                // losing every step.
                float given = (float)remains;
                Nutrients.Deposit(creature.Point, given);
                if (given > 0f) DetritusDepositedTotal += given;

                creature.TissueJoules = 0d;
            }

            if (held > 0d && !heldToCorpse)
            {
                Matter.DepositExact(creature.Point, held);
                HeldReleasedTotal += held;
            }

            _living.RemoveAt(index);
            _dead.Add(creature);
            Deaths++;

            _lineageEvents.Add(LineageEvent.Death(
                ElapsedSeconds, creature.Id, cause, accountAtDeath, reserveAtDeath,
                creature.MouthJoulesOwnKill, creature.MouthJoulesOtherKill, creature.MouthJoulesCarrion,
                creature.BiteKeptJoules));
        }

        /// <summary>
        /// Kills a creature whose articulation has diverged, as a death rather than as a crash —
        /// the divergence spec after logbook/0056.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why a death and not an exception.</b> <c>r20q-s1</c> lost 15,345 simulated seconds
        /// of a 20,000 s arm because one body's articulation exploded: PhysX refused
        /// <c>{NaN, NaN, NaN}</c> forces for its three parts nine steps running, and then
        /// <see cref="Observe"/> saw the non-finite height and took the run down. One creature in
        /// seventeen hundred is not a reason to censor an arm — but it is also not something to
        /// swallow silently, which is why the count is reported and the harness dumps the body's
        /// last finite state before calling this.
        /// </para>
        /// <para>
        /// <b>The books still close.</b> The tissue and the reserve are deposited at
        /// <see cref="Organism.HeightY"/> — the last height <see cref="Observe"/> accepted, since
        /// it refuses a non-finite one — so §5A.2's audit and the matter identity see exactly what
        /// a starvation of the same body would have moved. The physics is what failed; the
        /// economy is not allowed to lose track of a joule over it.
        /// </para>
        /// <para>
        /// Called from outside <see cref="Step"/>, between physics steps, which is safe for the
        /// same reason <see cref="Observe"/> is: it touches no <see cref="Rng"/> stream and takes
        /// no branch any other creature's step depends on.
        /// </para>
        /// </remarks>
        public void KillDiverged(Organism creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            int index = _living.IndexOf(creature);
            if (index < 0)
            {
                throw new ArgumentException(
                    $"Creature {creature.Id} is not living in this world, so there is nothing to " +
                    "kill. A body that diverged after it had already died means the scene and " +
                    "the population have come apart.",
                    nameof(creature));
            }

            Diverged++;
            Bury(creature, index, DeathCause.Diverged);
        }

        /// <summary>
        /// Withers a living body: the nursery's switching leaf tank (D236) ends a leaf that nothing killed, leaving no food.
        /// <b>No world calls this.</b> <see cref="Step"/> never does, and the farm has no use for it; only the nursery's switching
        /// tank (<c>leaves.patches.switching</c>) calls it, between steps.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The body's tissue, reserve and gestation account are burnt as <see cref="BurnUnbitten"/> burns them: booked as
        /// <see cref="EnergyOut"/>, the units to the spent field where the body lies and into <see cref="BurntTotal"/>, and a plant's held
        /// units sent to the spent field as a death without a corpse sends them. It founds no corpse and no remains whatever
        /// <see cref="RunConfig.ContactEating"/> says, takes the body out of the living, counts the death and writes the lineage row with
        /// <see cref="DeathCause.Withered"/>. <see cref="Diverged"/> is not counted: nothing failed.
        /// </para>
        /// <para>
        /// Called from outside <see cref="Step"/>, between steps, as <see cref="KillDiverged"/> is, and for the same reason it is safe: it
        /// touches no <see cref="Rng"/> stream and takes no branch any other creature's step depends on. A solver learns the body has
        /// gone as it learns of any death, from <see cref="Deaths"/> changing.
        /// </para>
        /// </remarks>
        public void WitherForTank(Organism creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            int index = _living.IndexOf(creature);
            if (index < 0)
            {
                throw new ArgumentException(
                    $"Creature {creature.Id} is not living in this world, so there is nothing to wither.",
                    nameof(creature));
            }

            Bury(creature, index, DeathCause.Withered);
        }

        /// <summary>
        /// Moves creatures between adjacent patches — D061. A metapopulation-style throttle
        /// rather than continuous advection (D061's rejected alternative): each living creature
        /// draws once, in list order, and the whole method is skipped whenever there is nowhere
        /// to disperse to or nothing asks for it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Guarded behind both halves so K=1 stays bit-identical.</b> Even a positive
        /// <see cref="RunConfig.DispersalChancePerStep"/> draws nothing when
        /// <see cref="PatchCount"/> is 1 — there is no adjacent patch to move to, and the method
        /// returns before touching <see cref="Rng"/> at all — so turning that knob on alone
        /// cannot perturb a K=1 run's trajectory. Symmetrically, K&gt;1 with the chance at its
        /// default of 0 draws nothing either: patches that never exchange creatures is a
        /// legitimate D061 configuration in its own right (isolated columns), not an oversight.
        /// </para>
        /// <para>
        /// <b>One seed per creature, one draw, split three ways.</b> Each living creature — walked
        /// in <c>_living</c>'s own order, which is deterministic for a given population state —
        /// draws <c>Rng.SeedFor(Seed, _nextIndex++)</c> exactly as a founder or an offspring does,
        /// and spends a single <see cref="Rng.NextFloat"/> on it: the bottom
        /// <see cref="RunConfig.DispersalChancePerStep"/>/2 of [0, 1) moves the creature to the
        /// patch behind it, the next equal-sized slice moves it to the patch ahead, and the
        /// remainder — everything from the chance upward — leaves it where it was. One float
        /// split three ways is exactly as unbiased as a chance draw followed by a direction draw
        /// and costs half the RNG stream.
        /// </para>
        /// <para>
        /// <b>The ring, both directions</b> — <c>(patch + 1) % PatchCount</c> and
        /// <c>(patch - 1 + PatchCount) % PatchCount</c>, the same wraparound
        /// <see cref="NutrientField.Mix"/>'s horizontal pass uses, so a creature can reach every
        /// patch by a sequence of single steps and no patch is architecturally an edge.
        /// </para>
        /// <para>
        /// Run after <see cref="Metabolise"/> and before <see cref="Reproduce"/>: this step's
        /// feeding and shading were already priced at the patch each creature held when the step
        /// began, and an offspring conceived this step inherits the patch its parent ends the
        /// step in.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Sets every living creature's patch from where its body is — D077's second rule.
        /// </summary>
        /// <remarks>
        /// A whole-population pass once per metabolic step, which is 2 Hz against the physics'
        /// 100, and one dictionary lookup per creature inside the placer. It costs what
        /// <see cref="Disperse"/> cost and draws nothing, where <see cref="Disperse"/> drew once
        /// per creature per step.
        /// </remarks>
        private void ReadPatchesFromPositions()
        {
            if (!Config.SharedSpace || Placement == null) return;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                creature.Patch = Placement.PatchOf(creature);
            }

            // D220: remains lie in the region they drifted to, and are bitten and burnt there.
            for (int i = 0; i < _remains.Count; i++) _remains[i].Patch = Placement.PatchOf(_remains[i]);
        }

        private void Disperse()
        {
            // D077. Retired, not merely disabled: with the box literal a creature's patch is read
            // from where its root is, and a lottery that moved the index without moving the body
            // would put a creature in one patch's water while it swam in another's. Returned from
            // before the draw, so nothing is taken from the RNG stream — the same guard the K=1
            // case below makes, for the same reason.
            if (Config.SharedSpace) return;

            if (!(Config.DispersalChancePerStep > 0f) || PatchCount <= 1) return;

            float chance = Config.DispersalChancePerStep;
            float half = chance * 0.5f;
            int patches = PatchCount;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                ulong seed = Rng.SeedFor(Seed, _nextIndex++);
                float draw = new Rng(seed).NextFloat();

                if (draw < half)
                {
                    creature.Patch = (creature.Patch - 1 + patches) % patches;
                }
                else if (draw < chance)
                {
                    creature.Patch = (creature.Patch + 1) % patches;
                }
            }
        }

        /// <summary>
        /// Carries creatures sideways with the roll they are in — D066's body half, the
        /// counterpart of <see cref="NutrientField.Advect"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Off is off.</b> Without <see cref="CurrentField.Rolls"/>, more than one patch and a
        /// positive speed, this returns before touching <see cref="Rng"/> at all — so every run
        /// before D066 replays bit for bit, the same guarantee <see cref="Disperse"/> makes for
        /// D061 and for the same reason: a knob that perturbs the RNG stream when it is off
        /// invalidates every result on file.
        /// </para>
        /// <para>
        /// <b>One extra draw per creature per step when it is on</b>, split three ways exactly as
        /// <see cref="Disperse"/> splits its own — the bottom slice crosses to the patch behind,
        /// the next to the patch ahead, the rest stays. That is the cost of the mechanism and it is
        /// the same order as dispersal's; it is documented rather than avoided because the
        /// alternative, reusing dispersal's draw, would make two independent mechanisms correlated
        /// in a way nobody could see in the output.
        /// </para>
        /// <para>
        /// <b>Both boundaries, and only outward.</b> A creature leaves through the face the water
        /// is leaving through. Its own right-hand face carries it to <c>k+1</c> when
        /// <see cref="CurrentField.CrossingDirection"/> there is +1; its left-hand face — the one
        /// its neighbour <c>k-1</c> owns — carries it to <c>k-1</c> when the direction there is
        /// -1. Under the roll's alternating parity those two are the same statement: a patch that
        /// is an up-leg exports at the surface through both faces, and a down-leg imports through
        /// both, which is exactly what <see cref="NutrientField.Advect"/> does with the stock
        /// beside it. Asking only about the creature's own face instead would have carried bodies
        /// one way while the detritus went the other, and D066's whole claim is that the two travel
        /// together.
        /// </para>
        /// </remarks>
        private void AdvectBodies(float seconds)
        {
            // D077. Retired for Disperse's reason: in a shared volume the water already carries
            // bodies — FluidEnvironment applies the current's horizontal drag to every part every
            // physics step — so a second, index-level transport would be the same flow counted
            // twice, once as a force and once as a lottery. Before any draw.
            if (Config.SharedSpace) return;

            CurrentField current = Config.Current;
            if (current == null || PatchCount <= 1) return;

            // D067. The vent moves bodies along its legs on exactly the same terms the roll moves
            // them along its own, so the guard asks whether *either* flow is running rather than
            // only the roll. Speed belongs to the roll and VentSpeed to the vent — a world with a
            // vent and still water still advects, and a world with neither still draws nothing.
            bool rolls = current.Rolls && current.Speed > 0f;
            if (!rolls && !current.VentActive(PatchCount)) return;

            int patches = PatchCount;
            float width = Nutrients.PatchWidthMetres;
            if (!(width > 0f)) return;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                int patch = creature.Patch;
                int behind = (patch - 1 + patches) % patches;

                // Outward through the right-hand face, and outward through the left-hand one,
                // which belongs to the patch behind.
                double ahead = current.CrossingDirection(creature.HeightY, ElapsedSeconds, patch, patches) > 0
                    ? current.HorizontalCrossingFraction(
                        creature.HeightY, ElapsedSeconds, patch, patches, seconds, width)
                    : 0d;

                double back = current.CrossingDirection(creature.HeightY, ElapsedSeconds, behind, patches) < 0
                    ? current.HorizontalCrossingFraction(
                        creature.HeightY, ElapsedSeconds, behind, patches, seconds, width)
                    : 0d;

                ulong seed = Rng.SeedFor(Seed, _nextIndex++);
                float draw = new Rng(seed).NextFloat();

                if (draw < back) creature.Patch = behind;
                else if (draw < back + ahead) creature.Patch = (patch + 1) % patches;
            }
        }

        /// <remarks>
        /// <para>
        /// <b>A brood is truncated rather than refused</b> — §5A.2c. An offspring's body has to be
        /// built out of the parent's reserve, and what a body costs is not known until the mutated
        /// genome has been developed, so the affordable prefix of the brood is born and the rest
        /// is not. Refusing the whole brood instead would make a slightly-too-expensive mutation
        /// cost a lineage every offspring rather than one, which is a selection pressure invented
        /// by the accounting.
        /// </para>
        /// <para>
        /// The threshold gate is still checked first, on the part of the cost that <i>is</i> known
        /// in advance. Without it every solvent creature would mutate and develop a genome on
        /// every step just to discover it could not pay for it — the same work, at the cost of
        /// most of the run.
        /// </para>
        /// <para>
        /// <b>The order of this walk is a world rule, and until D072 it was an accident.</b>
        /// <see cref="Conceive"/> draws a child's matter from the parent's own layer at the moment
        /// that parent is walked, so when a layer's stock covers fewer children than there are
        /// solvent parents, whoever is walked first takes it. <c>_living</c> is birth-ordered, so
        /// that was always the oldest — a queue nothing in DESIGN.md asked for, selecting for
        /// outliving it rather than for fecundity (logbook/0056).
        /// <see cref="RunConfig.ConceptionOrder"/> names the walk;
        /// <see cref="ConceptionOrder.Age"/> is the queue and the default, so the record replays.
        /// </para>
        /// <para>
        /// <b>And the walk is where the energy economy gets its grip, or fails to</b> — D073,
        /// logbook/0057. Whoever is walked first is the only place a parent's reserve can decide
        /// anything about its fecundity: the gate below is a threshold, not a ranking, so two
        /// solvent bodies breed alike however far apart their books are.
        /// <see cref="ConceptionOrder.Reserve"/> walks them richest first, which is what the
        /// queue was doing by accident, with age standing in for income.
        /// </para>
        /// </remarks>
        private void Reproduce()
        {
            // Collected first and appended after, so an offspring cannot itself reproduce on the
            // step it was born — which it could if the list were grown while being walked, and
            // which would make brood size compound within a single step.
            _born.Clear();

            switch (Config.ConceptionOrder)
            {
                case ConceptionOrder.Shuffled:
                {
                    PermuteConceptionOrder();

                    for (int i = 0; i < _living.Count; i++) Brood(_living[_conceptionOrder[i]]);
                    break;
                }

                case ConceptionOrder.Reserve:
                {
                    // Only the solvent are ranked, so this walk is shorter than the other two —
                    // the ones it leaves out are the ones Brood would have turned away anyway.
                    int solvent = RankConceptionOrderByReserve();

                    for (int i = 0; i < solvent; i++) Brood(_living[_conceptionOrder[i]]);
                    break;
                }

                default:
                {
                    // Age, and every run before D072: _living's own birth order, untouched.
                    for (int i = 0; i < _living.Count; i++) Brood(_living[i]);
                    break;
                }
            }

            // Appended in walk order, so under Shuffled and Reserve this step's ordering also
            // decides the order the children sit in for the next one. That is not a second
            // decision to make: _living's order is only ever read by this walk, and the next step
            // orders whatever it finds afresh.
            for (int i = 0; i < _born.Count; i++)
            {
                _living.Add(_born[i]);
                Births++;
            }
        }

        /// <summary>
        /// Whether any part of a body is lighter than <see cref="RunConfig.MinNewbornPartKilograms"/>
        /// — fable-propose-growth.md rule 3.
        /// </summary>
        /// <remarks>
        /// Mass is <see cref="RunConfig.PartDensityKilogramsPerCubicMetre"/> times volume, which is
        /// the mass the harness gives the articulation body. Asked of the newborn and never of the
        /// adult: what the solver has to carry is the body that will exist.
        /// <para>
        /// Under <see cref="DevelopmentLimits.FloorsWeighRigidGroups"/> the floor weighs each rigid
        /// group instead of each part: the root or a jointed part, with every part welded to it by
        /// a joint of no degree of freedom summed in. That is the body the farm's solver carries
        /// (a welded part's inertia is folded into its host's), and it is what lets a bud born
        /// small be born at all (the owner's ruling of 2026-09-24). Off, each part alone, as every
        /// recorded world weighed it.
        /// </para>
        /// </remarks>
        private bool IsUnderTheMassFloor(Phenotype body)
        {
            float floor = Config.MinNewbornPartKilograms;
            if (!(floor > 0f)) return false;

            float minVolume = floor / RunConfig.PartDensityKilogramsPerCubicMetre;
            IReadOnlyList<PhenotypePart> parts = body.Parts;

            if (Config.Development.FloorsWeighRigidGroups)
            {
                return RigidGroupsUnder(parts, minVolume);
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Volume < minVolume) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether any rigid group of a body holds less than <paramref name="minVolume"/>: each
        /// part's volume is summed into the group of the nearest part at or above it that is the
        /// root or carries a joint with a degree of freedom. Summed in double, in part order.
        /// </summary>
        public static bool RigidGroupsUnder(IReadOnlyList<PhenotypePart> parts, float minVolume)
        {
            var groupVolume = new double[parts.Count];

            for (int i = 0; i < parts.Count; i++)
            {
                int head = i;
                while (parts[head].ParentIndex >= 0 && parts[head].JointType.DofCount() == 0)
                {
                    head = parts[head].ParentIndex;
                }

                groupVolume[head] += parts[i].Volume;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                bool isHead = parts[i].ParentIndex < 0 || parts[i].JointType.DofCount() > 0;
                if (isHead && groupVolume[i] < minVolume) return true;
            }

            return false;
        }

        /// <summary>One parent's turn: the solvency gate, then the brood behind it.</summary>
        /// <remarks>
        /// <para>
        /// Lifted out of <see cref="Reproduce"/> so the two orders share one body and cannot drift
        /// apart — the walk is what D072 varies, and nothing else is.
        /// </para>
        /// <para>
        /// <b>The gate is the investment, since fable-propose-growth.md rule 2.</b> A parent breeds
        /// when its reserve holds <see cref="ReproductionTraits.BirthInvestment"/> of its own
        /// tissue value plus the litter's overhead, and it spends exactly that. The litter no
        /// longer multiplies what the event costs, only how many ways it is divided — which is
        /// what makes brood size a decision about the size of a child rather than about the price
        /// of a reproduction.
        /// </para>
        /// </remarks>
        private void Brood(Organism parent)
        {
            if (!IsSolvent(parent, out _)) return;

            for (int n = 0; n < parent.Genome.Reproduction.BroodSize; n++)
            {
                // A sibling refused under the mass floor is skipped, not the end of the litter.
                // The floor is a property of the body that was drawn, and the next sibling is a
                // fresh draw of a fresh mutation, so a brood of three with one dwarfed sibling
                // yields two children rather than one. An energy or matter shortfall is different
                // in kind: it is a property of the parent and is still true for every sibling
                // behind this one, so it ends the brood as it always has.
                if (Conceive(parent) == Conception.Refused) break;
            }
        }

        /// <summary>
        /// Whether a parent clears its breeding gate, and by how much — the one expression
        /// <see cref="Brood"/> and <see cref="RankConceptionOrderByReserve"/> both read.
        /// </summary>
        /// <remarks>
        /// A lump breeder is asked of its reserve against <see cref="Organism.ReproductionThreshold(RunConfig)"/>
        /// (the price plus its margin); a gestating one of its account against
        /// <see cref="Organism.GestationThreshold"/> (the price alone). At the per-tissue factor
        /// of 0 the first is the recorded gate bit for bit.
        /// </remarks>
        private bool IsSolvent(Organism parent, out double surplus)
        {
            // The gate is Organism.BreedingGate, the one expression D124's founder cap also reads
            // (AdmitFounder), so the cap is asked of the gate this check applies.
            if (BirthsHeld) { surplus = 0d; return false; }

            // D241: with the climb on, a body breeds only once adult. Growth no longer takes the
            // reserve down to the floor each step, so the gate alone would let a juvenile whose
            // intake beats its allowance breed small. Off, nothing here is read.
            if (Config.GrowthAdultSeconds > 0f && parent.BodyFraction < 1f)
            {
                surplus = 0d;
                return false;
            }

            double gate = parent.BreedingGate(Config);
            double funds = parent.Gestates ? parent.GestationJoules : parent.Energy;

            surplus = funds - gate;
            return gate > 0d && funds >= gate;
        }

        /// <summary>
        /// Fills <see cref="_conceptionOrder"/> with a fresh uniformly random permutation of
        /// <c>_living</c>'s indices — D072, logbook/0056.
        /// </summary>
        /// <remarks>
        /// Fisher–Yates, from <see cref="_conceptionRng"/> alone, so the permutation is a function
        /// of <c>(seed, config, step count)</c> and a shuffled run replays exactly like every other
        /// run here (§7). The array grows and is kept; it is never shrunk, because a world that
        /// halves its population and grows back would otherwise reallocate on every recovery.
        /// </remarks>
        private void PermuteConceptionOrder()
        {
            int count = _living.Count;
            if (_conceptionOrder.Length < count) _conceptionOrder = new int[count];

            for (int i = 0; i < count; i++) _conceptionOrder[i] = i;

            for (int i = count - 1; i > 0; i--)
            {
                int j = _conceptionRng.Range(i + 1);

                int swap = _conceptionOrder[i];
                _conceptionOrder[i] = _conceptionOrder[j];
                _conceptionOrder[j] = swap;
            }
        }

        /// <summary>
        /// Fills the front of <see cref="_conceptionOrder"/> with the solvent parents' indices,
        /// richest first, and returns how many there are — D073, logbook/0057.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Energy buys fecundity.</b> A body's surplus is what it holds above the price of the
        /// child it is asking for, and the layer's matter goes down that list until it runs out.
        /// The gate is the one <see cref="Brood"/> applies, computed here once per body and used
        /// both to reject the insolvent and to rank the rest — the same rule read twice would be
        /// two rules waiting to disagree.
        /// </para>
        /// <para>
        /// <b>One step's ranking, not a running auction.</b> Every surplus is read before the walk
        /// begins, so a parent that has just bred — and is now poorer than the body behind it —
        /// keeps its place for the rest of the step. Re-ranking after each birth would be a
        /// different world rule and a sort per birth; D073 asked for this one.
        /// </para>
        /// </remarks>
        private int RankConceptionOrderByReserve()
        {
            int count = _living.Count;
            if (_conceptionOrder.Length < count) _conceptionOrder = new int[count];
            if (_conceptionSurplus.Length < count) _conceptionSurplus = new double[count];

            int solvent = 0;

            for (int i = 0; i < count; i++)
            {
                Organism parent = _living[i];

                if (!IsSolvent(parent, out double surplus)) continue;

                _conceptionSurplus[i] = surplus;
                _conceptionOrder[solvent++] = i;
            }

            Array.Sort(_conceptionOrder, 0, solvent, _byReserve);

            return solvent;
        }

        /// <summary>How one attempt at one offspring ended.</summary>
        /// <remarks>
        /// Three outcomes rather than a bool, because two of the failures mean opposite things to
        /// the litter behind them. <see cref="Refused"/> is the parent's own shortfall and ends
        /// the brood; <see cref="UnderFloor"/> is a fact about the body just drawn and ends only
        /// this sibling (fable-propose-growth.md rule 3, as amended in review 2026-09-08).
        /// </remarks>
        private enum Conception
        {
            /// <summary>A child was admitted, or a stillbirth was counted and settled.</summary>
            Born,

            /// <summary>The parent could not afford it. The rest of the brood is abandoned.</summary>
            Refused,

            /// <summary>This body was under the mass floor. The next sibling still draws.</summary>
            UnderFloor,
        }

        /// <summary>
        /// Makes one offspring if the parent can afford it and the body it drew can be built.
        /// </summary>
        private Conception Conceive(Organism parent)
        {
            ulong seed = Rng.SeedFor(Seed, _nextIndex++);

            Genome childGenome = Mutator.Mutate(
                parent.Genome, new Rng(seed), Config.Mutation, Config.CellTypes, Config.Genome,
                Config.SensorPool(),
                buoyancyOffsetPriced: Config.BuoyancyOffsetWattsPerCubicMetre > 0f,
                log: _mutationLog, shapes: Config.Shapes,
                growthReserveMin: Config.GrowthReserveMinSeconds,
                growthReserveMax: Config.GrowthReserveMaxSeconds);

            // D146: a central genome numbers any edge the mutation copied or made, so no two parts
            // share a path id, before the body that the brain is wired against is developed.
            if (childGenome.Brain != null) childGenome.NumberEdges();

            Phenotype body = Developer.Develop(
                childGenome, Config.Development, null, Config.Shapes, cellTypes: Config.CellTypes);

            if (childGenome.Brain != null)
            {
                GrowBrain(childGenome, body, seed, wireFresh: childGenome.Brain.Outputs.Count == 0, mutate: true);
            }

            // The owner's ruling of 2026-09-24: what this birth gained by budding, for the lineage
            // row — each bud's cell type, and how many of the buds the body built. Read off the
            // adult development, which is the body the newborn is a scaled copy of, so a bud built
            // here is a bud the newborn carries. Instrumentation: nothing branches on it.
            string budCells = null;
            int budsExpressed = 0;
            if (_mutationLog.Buds > 0)
            {
                budCells = string.Join("+", _mutationLog.BudCellTypes);
                foreach (int budNode in _mutationLog.BudNodes)
                {
                    IReadOnlyList<PhenotypePart> built = body.Parts;
                    for (int i = 0; i < built.Count; i++)
                    {
                        if (built[i].SourceNode != budNode) continue;
                        budsExpressed++;
                        break;
                    }
                }
            }

            // A body of no parts is a stillbirth (§4.5's extinction-by-shrinking; Admit counts it
            // and settles the energy). Read here rather than further down because everything
            // between this line and the birth has to know that there is no body to size, price in
            // matter or place.
            bool stillborn = body.PartCount == 0;

            // fable-propose-growth.md rules 2 and 3. The parent spends a fraction of its own body
            // value on the litter, each child gets an equal share, and the share is the child's
            // whole start: the body it is born with plus its first reserve, split by a world
            // constant so that no lineage can set its children's reserve to zero.
            ReproductionTraits traits = parent.Genome.Reproduction;
            double share = traits.BirthInvestment * parent.TissueJoules / traits.BroodSize;

            double adultTissue = Metabolism.TissueJoules(body, Config);

            // The whole share is capped, not only the body it buys (rule 3, corrected in the
            // review of 2026-09-08). A parent may not buy a creature larger than its genome
            // describes, so the ceiling is the share that exactly fills the adult body once the
            // reserve has been taken out of it. Capping only the body left a child whose adult
            // scale had mutated well below its parent's born full grown and holding a reserve
            // sized to the parent's body: a windfall paid for being small, which is a pressure
            // nobody chose. Everything above the capped share stays with the parent. The other
            // end is not clamped. A share worth almost nothing makes an almost-nothing child,
            // and the mass floor below is what refuses that.
            float reserveFraction = Config.NewbornReserveFraction;

            if (!stillborn && adultTissue > 0d && reserveFraction < 1f)
            {
                double cap = adultTissue / (1d - reserveFraction);
                if (share > cap) share = cap;
            }

            double reserve = share * reserveFraction;
            double bodyValue = share - reserve;

            double fraction = adultTissue > 0d ? bodyValue / adultTissue : 1d;
            if (fraction > 1d) fraction = 1d;

            Phenotype newborn = stillborn || fraction >= 1d
                ? body
                : body.ScaledFor(Config, (float)Math.Pow(fraction, 1d / 3d));

            double tissue = stillborn || fraction >= 1d
                ? adultTissue
                : Metabolism.TissueJoules(newborn, Config);

            // Rule 3's other edge, and it is a refusal rather than a smaller child. Every
            // divergence on record was a newborn and the lightest link among them weighed
            // 0.143 kg, so a body the physics cannot carry must not be built at all. A genome
            // whose investment over its litter sits under the floor therefore never breeds, which
            // is something selection can see. The parent keeps its reserve: nothing was spent and
            // no lineage row is written, exactly as for a crowded refusal. The sibling behind
            // this one still draws, because the next mutation may not be under the floor.
            if (!stillborn && IsUnderTheMassFloor(newborn))
            {
                ConceptionsUnderMassFloor++;
                return Conception.UnderFloor;
            }

            // In double, so that what the parent gives up is exactly what the child carries plus
            // what the overhead burns. In float this sum rounded against a price of order the
            // overhead — 100 J against a 0.4 J body — and the missing joule was in neither book.
            //
            // The overhead scales with the child above a floor since the owner's ruling of
            // 2026-09-24: max(floor, per-tissue x the child's tissue at birth). It is quantised to
            // the float the field's door takes once, here, and that one number is what the parent
            // pays, what EnergyOut books and what returns to the water over rho, so the two books
            // see the same joules. At a per-tissue factor of 0 it is the float floor itself and
            // every recorded price is the price it was.
            float overheadPaid = (float)Config.OverheadFor(tissue);
            double overhead = overheadPaid;
            double price = tissue + reserve + overhead;

            // The ruling of 2026-09-24: a gestating parent pays from its account and nothing else,
            // so its reserve and its margin are not asked. A shortfall is counted with the margin's
            // refusals, which is the counter for "the parent could not pay", and the account is
            // kept, as a refused lump keeps the reserve.
            bool gestating = parent.Gestates;

            if (gestating)
            {
                if (parent.GestationJoules < price)
                {
                    ConceptionsUnderMargin++;
                    return Conception.Refused;
                }
            }

            // D098 §3. The price plus what the genome insists on keeping — the same expression
            // Organism.ReproductionThreshold applies, so a parent that got here has already
            // cleared its margin once and this is the exact check rather than a second rule. The
            // margin is read off the parent's current standing cost, not the one it was born
            // with, because a body that has grown is a body with more to keep back.
            else if (parent.Energy <
                price + (double)parent.Genome.Reproduction.ReserveMargin * parent.StandingWatts)
            {
                ConceptionsUnderMargin++;
                return Conception.Refused;
            }

            // D098 retired the second price. A child is charged matter given by its parent and
            // nothing is drawn from any field, so the one gate above is the whole solvency test:
            // the two matter gates, the take, the refund and Organism.LockedMatter all went with
            // MatterPerTissueJoule and MatterPerCreature.

            // D077. The last gate, and deliberately after every solvency check and before the
            // first thing that is spent: a body with nowhere to go costs the parent nothing, so
            // the order is "can it be afforded, then can it be placed", never the reverse. A
            // refusal here is a crowded stillbirth — counted, and otherwise as though the parent
            // had simply not bred this step. Last also so that a successful reservation is always
            // followed by a Commit or a Release: nothing between here and Admit can refuse.
            //
            // Skipped for a body of no parts: Admit is about to call that stillborn for the older
            // reason (§4.5's extinction-by-shrinking), and reserving room for a body that will
            // never exist would leave a reservation to be released again for nothing.
            int childPatch = parent.Patch;

            // The depth the child is admitted at. Its parent's, as it has always been, and the
            // placer may raise it — a parent resting on a solid sea bed breeds beside itself, not
            // into the rock (logbook/specs/floor-spec.md rule 2). A local rather than the expression
            // inline at Admit so that the height the body is built at and the height the economy
            // charges are the same number; in the tiled world nothing touches it and the
            // expression is parent.HeightY exactly, as before.
            float childHeight = parent.HeightY;

            // The adult and not the newborn, reversed 2026-09-10. What the placer is asked for
            // used to be room for the body that is about to exist, on the reasoning that a child
            // born at a fifth of its adult volume needs a little over half the clearance and that
            // asking for the adult's would refuse births into water a child fits in perfectly
            // well. Since D087 that is the wrong end of the creature's life: the child does not
            // stay that size. It grows into its adult body in place, up to twentyfold, inside a
            // spot reserved for the body it was born as, and its neighbours grow towards it at the
            // same time. Round 33's seed 2 threw three bodies to NaN in one contact cluster at
            // 3,066.5 s, and the step-after-resize instrument peaked at 25 cm of engine
            // displacement in that arm: bodies being shoved on a growth step (logbook/0082). So
            // the world reserves the room the lineage will actually need. In a crowded
            // neighbourhood that refuses more births, and those refusals are counted as crowded
            // stillbirths, which is the honest reading: the room was not there.
            bool shared = Config.SharedSpace && Placement != null && newborn.PartCount > 0;

            if (shared && !Placement.TryReserveOffspring(parent, body, ref childHeight, out childPatch))
            {
                CrowdedStillbirths++;
                return Conception.Refused;
            }

            if (gestating) parent.GestationJoules -= price;
            else parent.Energy -= price;

            // D128, the owner's addition: a birth ages the parent, one doubling of its hazard for
            // a child whose tissue is the parent's adult tissue, in proportion below that.
            if (Config.AgeingPerBirth > 0f && parent.AdultTissueJoules > 0d)
            {
                PushAgeClock(parent, Config.AgeingPerBirth * (tissue / parent.AdultTissueJoules));
            }

            // The child's body and its first reserve are transferred and stay in the world; the
            // overhead is burnt. It is paid per offspring, so it does not by itself tell one
            // brood of four from four broods of one (corrected 2026-09-07); the gate above does,
            // by asking the parent to hold the whole litter's investment at once. What the
            // overhead does is make an offspring cost more than the energy it carries (§5A.6).
            //
            // D098's leg 2, at the parent's own point: the overhead is a charged unit burnt like
            // any other, so the joules leave the world and the spent unit returns to the water
            // where the parent is. It is the largest single burn this world makes.
            if (overheadPaid > 0f)
            {
                EnergyOut += overhead;

                // Through the field's exact door, in double, as the burn has gone since round 52's
                // build (round 60's). Until then the units were a float, and a planted line breeding
                // near-identical children rounded the same way at every birth: round 59's B1 drift,
                // about 2e-5 units by 48,000 s in its two planted seeds (scratch/r59-read/b1-drift.txt).
                // D206: a plant parent keeps the units of the overhead too, as of every burn.
                ReturnSpent(parent, overhead / Config.JoulesPerUnit);
            }

            Organism child = Admit(
                childGenome, newborn, BirthKind.Reproduction, seed, parent.Id,
                parent.GenerationDepth + 1, reserve, tissue, childHeight, parent,
                patch: childPatch, adultPhenotype: body, adultTissue: adultTissue,
                budCells: budCells, budsExpressed: budsExpressed);

            // D077. The reservation belongs to a creature now, or to nobody. Admit could not
            // refuse a body with parts until the self-overlap rule of 2026-09-19, and this
            // branch was written as a belt rather than a brace for that reason; it is now the
            // path a refused knot actually takes, which is what keeping the invariant true by
            // construction rather than by reading Admit is worth.
            if (shared)
            {
                if (child != null) Placement.Commit(child.Id);
                else Placement.Release();
            }

            if (child != null)
            {
                _born.Add(child);

                // Realised fecundity, counted where a birth actually happens rather than
                // reconstructed later from lineage.jsonl's parent column — the absorptive log
                // has to carry it per row, and a stillbirth (Admit returning null) is not a
                // child however much energy it cost. Pure instrumentation: nothing branches on it.
                parent.Children++;
                parent.LastChildSeconds = ElapsedSeconds;

                if (gestating) GestationBirths++;
            }

            return Conception.Born;
        }

        /// <remarks>
        /// Fresh founders rather than descendants of survivors, and a trickle rather than a cohort
        /// — D021. Choosing who repopulates would be selection performed by us, and a cohort
        /// spawned together tends to die together, manufacturing a boom-and-bust that is an
        /// artefact of this method rather than a property of the world.
        /// </remarks>
        private void EnforceFloor()
        {
            // Config.FloorClosesAfterSeconds (0 = never) — D021 wants the founding cohort but
            // nothing after it. This runs before the population check so a world that has already
            // crashed to zero past the threshold is left at zero rather than rescued.
            if (Config.FloorClosesAfterSeconds > 0f && ElapsedSeconds >= Config.FloorClosesAfterSeconds)
            {
                return;
            }

            if (_living.Count >= Config.MinimumPopulation) return;

            SecondsSinceFloorFired = 0.0;

            int wanted = Math.Min(
                Config.MinimumPopulation - _living.Count,
                Math.Max(1, Config.FloorSpawnsPerStep));

            EnsureFounderAcceptance();

            float share = Config.FoundingFloorPoolShare;

            for (int i = 0; i < wanted; i++)
            {
                // The owner's ruling of 2026-10-04: a share of the floor's founders are pool copies,
                // so that the pool's bodies arrive while the world still has room to breed in. The
                // two uniforms come from a stream keyed by this attempt's number and nothing else,
                // and only with a share above 0, so a world at 0 takes the floor's draws to the bit
                // and the trickle's stream is untouched whatever the share. The pick is the
                // trickle's: weighted on the running sums when the world has weights, the recorded
                // uniform expression otherwise.
                if (share > 0f)
                {
                    var rng = new Rng(Rng.SeedFor(Rng.SeedFor(Seed, FloorPoolIndex), (ulong)FloorSpawns));
                    if (rng.NextFloat() < share)
                    {
                        int pool = _tricklePool.Count;
                        int index = _tricklePoolCumulative == null
                            ? Math.Min(pool - 1, (int)(rng.NextFloat() * pool))
                            : WeightedPoolIndex(_tricklePoolCumulative, rng.NextFloat());
                        SpawnPoolFounder(index, atFloor: true);
                        continue;
                    }
                }

                SpawnFounder(FounderSource.Floor);
            }
        }

        /// <summary>
        /// Whether the founding trickle is adding founders now — D115: a rate above 0 and a floor
        /// that has closed. The farm's <c>extinct</c> ending is not taken while this holds,
        /// because an empty world is still being founded.
        /// </summary>
        public bool FoundersStillArriving =>
            Config.FoundingTricklePerSecond > 0f &&
            Config.FloorClosesAfterSeconds > 0f &&
            ElapsedSeconds >= Config.FloorClosesAfterSeconds;

        /// <summary>
        /// Founders the trickle has spawned, running total — D115. Every attempt, as
        /// <see cref="FloorSpawns"/> counts every floor attempt: a founder refused for want of
        /// room or under the newborn mass floor is still one the trickle drew.
        /// </summary>
        public long TrickleSpawns { get; private set; }

        /// <summary>
        /// The founders that were copies from D117's pool, running total — every attempt, refused
        /// ones included, as the trickle counts: of <see cref="TrickleSpawns"/>, and of
        /// <see cref="FloorSpawns"/> too in a world with a floor share
        /// (<see cref="RunConfig.FoundingFloorPoolShare"/>). 0 in every world whose two pool shares
        /// are 0.
        /// </summary>
        public long PoolSpawns { get; private set; }

        /// <summary>
        /// D117's pool, in the order of the run directory's <c>pool/NN.json</c>: the genomes a
        /// trickle founder may be an exact copy of. Empty when none was handed to the world.
        /// </summary>
        public IReadOnlyList<Genome> TricklePool => _tricklePool;

        private readonly IReadOnlyList<Genome> _tricklePool;

        /// <summary>
        /// The pool's weights in the pool's order, as the world was handed them; null when the
        /// pool is picked from uniformly, which is every world whose pool was not named by a
        /// weighted list.
        /// </summary>
        public IReadOnlyList<double> TricklePoolWeights => _tricklePoolWeights;

        private readonly IReadOnlyList<double> _tricklePoolWeights;

        // The weights' running sums, the pick's search table; null with no weights.
        private readonly double[] _tricklePoolCumulative;

        /// <summary>
        /// The pool genome a weighted pick lands on for the uniform <paramref name="u"/> in [0, 1):
        /// the first index whose running sum of weights exceeds <c>u</c> times their total, so
        /// genome <c>i</c> takes a share of [0, 1) proportional to its weight. Public so the search
        /// can be tested alone.
        /// </summary>
        /// <param name="cumulative">The weights' running sums, in the pool's order.</param>
        /// <param name="u">The trickle's second uniform, the one the unweighted pick reads.</param>
        public static int WeightedPoolIndex(IReadOnlyList<double> cumulative, float u)
        {
            if (cumulative == null) throw new ArgumentNullException(nameof(cumulative));

            int n = cumulative.Count;
            double target = u * cumulative[n - 1];

            int lo = 0;
            int hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (target < cumulative[mid]) hi = mid;
                else lo = mid + 1;
            }

            return lo;
        }

        /// <summary>
        /// D115's founding trickle (<c>logbook/specs/founding-trickle-spec.md</c> §1): after the
        /// floor has closed, a Poisson count of founders a step at
        /// <see cref="RunConfig.FoundingTricklePerSecond"/>, each spawned through the floor's own
        /// path.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Returns before any draw while the trickle is off or the floor still open</b>, so a
        /// world at rate 0 never advances <see cref="_trickleRng"/> and is step for step the
        /// recorded world. A world whose floor never closes cannot reach here with a rate: the
        /// constructor refuses it (<see cref="ValidateFounding"/>), because the trickle would
        /// never run there.
        /// </para>
        /// <para>
        /// <b>It adds at its rate whatever the count.</b> It does not top up a crash and does not
        /// stop for a crowd; a world that empties under it is refounded at the trickle's pace. The
        /// count comes from the trickle's own stream and each founder's genome from
        /// <c>_nextIndex</c> as the floor's does, so the trickle moves which genomes later births
        /// are given only by the founders it actually adds.
        /// </para>
        /// </remarks>
        private void EnforceTrickle(float seconds)
        {
            if (!FoundersStillArriving) return;

            double mean = (double)Config.FoundingTricklePerSecond * seconds;
            if (mean > 1d)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"The founding trickle at {Config.FoundingTricklePerSecond}/s over a ") +
                    FormattableString.Invariant($"{seconds} s step adds {mean:0.###} founders a ") +
                    "step on average, and the trickle is bounded at one (rate · dt ≤ 1, D115). " +
                    "Step the world at the 0.5 s metabolic step, or lower the rate.");
            }

            int count = TrickleCount(_trickleRng, mean);
            if (count == 0) return;

            EnsureFounderAcceptance();

            float share = Config.FoundingTricklePoolShare;

            for (int i = 0; i < count; i++)
            {
                // D117. Two more uniforms from the trickle's own stream, and only with a share
                // above 0: the first decides pool or lottery, the second (taken only for the
                // pool) which genome. At a share of 0 neither is drawn, so the stream, the
                // genome seeds and the placer's draws are the trickle build's to the bit. A
                // weighted pool reads the same second uniform through its running sums, so weights
                // change which genome arrives and never how many draws are taken; without them the
                // expression is the one every recorded pool world drew with.
                if (share > 0f && _trickleRng.NextFloat() < share)
                {
                    int pool = _tricklePool.Count;
                    int index = _tricklePoolCumulative == null
                        ? Math.Min(pool - 1, (int)(_trickleRng.NextFloat() * pool))
                        : WeightedPoolIndex(_tricklePoolCumulative, _trickleRng.NextFloat());
                    SpawnPoolFounder(index, atFloor: false);
                    continue;
                }

                SpawnFounder(FounderSource.Trickle);
            }
        }

        /// <summary>
        /// A Poisson count of mean <paramref name="mean"/>, drawn from <paramref name="rng"/> by
        /// Knuth's product of uniforms — D115's draw, public so the draw can be tested alone.
        /// </summary>
        /// <remarks>
        /// Takes no draw at a mean of 0 or below, which is what keeps a trickle at rate 0 off the
        /// stream entirely. At the means the trickle allows (at most 1) the product needs about
        /// <c>mean + 1</c> uniforms, so the stream advances by one or two draws a step.
        /// </remarks>
        public static int TrickleCount(Rng rng, double mean)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!(mean > 0d)) return 0;

            double limit = Math.Exp(-mean);
            double product = 1d;
            int count = -1;

            do
            {
                count++;
                product *= rng.NextFloat();
            }
            while (product > limit);

            return count;
        }

        /// <summary>
        /// D146: wires a central genome's brain to the body it developed and, for a birth, mutates
        /// it. An empty brain is wired fresh (<paramref name="wireFresh"/>); otherwise a new joint
        /// copies its nearest joint (<see cref="CentralWiring.Effective"/>). The draws come from a
        /// stream of their own, derived from <paramref name="seed"/>, so no other draw of the world
        /// moves.
        /// </summary>
        /// <remarks>
        /// The result is validated here, where it is made: a brain that broke a rule would
        /// otherwise be refused only at the creature's next mutation or rebuild, far from its cause.
        /// </remarks>
        private void GrowBrain(Genome genome, Phenotype body, ulong seed, bool wireFresh, bool mutate)
        {
            var rng = new Rng(Rng.SeedFor(seed, BrainStream));
            SensorChannel[] pool = Config.SensorPool();

            genome.Brain = CentralWiring.Effective(
                genome.Brain, body, rng, pool, Config.Brain, founder: wireFresh);

            if (mutate) CentralMutation.Mutate(genome.Brain, body, pool, rng, Config.Brain);

            List<string> issues = genome.Brain.Validate();
            if (issues.Count > 0)
            {
                throw new InvalidOperationException(
                    "D146: a grown brain broke a rule: " + string.Join("; ", issues));
            }

            body.Brain = genome.Brain;
        }

        // "BRAN": the stream index of D146's brain draws at a birth.
        private const ulong BrainStream = 0x4252414EUL;

        // "INOC": the seed an empty-brained inoculant's brain is wired from.
        private const ulong InoculantBrainStream = 0x494E4F43UL;

        /// <summary>
        /// One founder, drawn, placed, endowed and admitted — the floor's own spawn path, shared
        /// with D115's trickle so that the two sources cannot drift apart. The counter the
        /// attempt lands in is the source's.
        /// </summary>
        private void SpawnFounder(FounderSource source)
        {
            ulong seed = Rng.SeedFor(Seed, _nextIndex++);
            var rng = new Rng(seed);

            // The registry is handed over for D106 item 3's one founder rule: a consumer node
            // is drawn at this run's own intake cap, not at the built-in table's.
            Genome genome = GenomeFactory.Founder(
                rng, Config.Genome, Config.SensorPool(), Config.CellTypes);

            // D146: the founder's body is the one the old model's stream drew, and its neurons are
            // dropped for the one brain, which AdmitFounder wires once the body is developed.
            if (Config.Brain.BrainModel == BrainModel.Central) CentralWiring.Convert(genome);

            // Placed through the lit zone rather than at the surface. Starting everything at
            // depth zero would hand generation zero the best light in the world and make the
            // §5A.2 calibration read as more generous than it is.
            //
            // The draw runs to the full spread, which in the reference world is the world's
            // own depth (EVOSIM_FOUNDER_DEPTH 60 in a 60 m world), so a founder can be drawn
            // at the very bottom. The placer raises it clear of a solid sea bed when there is
            // one — see IBodyPlacement.TryReserveFounder — rather than the draw being narrowed
            // here, because how much room a body needs is a fact about the body and the
            // world's geometry, neither of which Core has.
            float height = -rng.Range(0f, Config.FounderDepthSpread);

            AdmitFounder(genome, seed, height, source, poolIndex: -1);
        }

        /// <summary>
        /// One of D117's pool founders: an exact copy of pool genome <paramref name="index"/>,
        /// with a fresh seed from the world's stream, admitted through the floor's own path
        /// (<see cref="AdmitFounder"/>) so that it is endowed, placed and counted as a trickle
        /// founder is.
        /// </summary>
        /// <remarks>
        /// The seed labels the birth and draws the founder's depth the way a lottery founder's own
        /// stream does after its genome; nothing mutates the genome, for
        /// <see cref="Inoculate"/>'s reason: the point is the body the launcher named.
        /// <para>
        /// In a Central world a pool genome with no central brain (an old per-node genome) is
        /// converted on arrival exactly as a lottery founder is (D146: the neurons dropped, the
        /// empty brain wired all to all once the body is developed), by the owner's ruling of
        /// 2026-10-04 that a pool founder runs the world's brain. The wiring draws from the brain
        /// stream of this founder's own seed, which nothing else draws, so no other draw of the
        /// world moves; a pool genome that already carries a central brain keeps it unmutated, and
        /// a per-node world takes the pool genome as it is (a central one is refused at
        /// construction).
        /// </para>
        /// <para>
        /// <paramref name="atFloor"/> marks a floor founder drawn from the pool
        /// (<see cref="RunConfig.FoundingFloorPoolShare"/>): it is counted in
        /// <see cref="FloorSpawns"/> rather than <see cref="TrickleSpawns"/>, and its lineage row
        /// is the trickle's pool row (<c>"src":"pool"</c>, its pool index), told apart only by its
        /// time, which is before <see cref="RunConfig.FloorClosesAfterSeconds"/>; the trickle runs
        /// only after it.
        /// </para>
        /// </remarks>
        private void SpawnPoolFounder(int index, bool atFloor)
        {
            ulong seed = Rng.SeedFor(Seed, _nextIndex++);
            float height = -new Rng(seed).Range(0f, Config.FounderDepthSpread);

            Genome genome = _tricklePool[index];
            if (genome.Brain == null && Config.Brain.BrainModel == BrainModel.Central)
            {
                // A copy: the pool's genome is drawn from again and stays the file's body.
                genome = genome.Clone();
                CentralWiring.Convert(genome);
            }

            AdmitFounder(genome, seed, height, FounderSource.Pool, index, atFloor);
        }

        /// <summary>
        /// Plants one immigrant: a copy of <paramref name="genome"/>, the genome of creature
        /// <paramref name="fromId"/> of the world at place <paramref name="fromWorld"/> in this
        /// world's group — D159's exchange. Placed, endowed and admitted through the founders'
        /// own path (<see cref="AdmitFounder"/>), so it lands where a founder would and starts
        /// with what a founder would.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A founder's placement and a founder's start, by D159.</b> Its depth is drawn over
        /// <see cref="RunConfig.FounderDepthSpread"/> from a seed of the world's own stream, as a
        /// pool founder's is, and the placer and the founders' acceptance rules then treat it as
        /// a founder. It is born at its genome's birth fraction with the founder's purse, the
        /// endowment (<see cref="FounderEndowmentFor"/>) and D124's cap, and not with
        /// <see cref="Inoculate"/>'s fixed depth and bare purse. Both books take it as income, as
        /// they take a founder, because the creature it was copied from stays in its own world.
        /// </para>
        /// <para>
        /// <b>Refused quietly and counted.</b> A body under the newborn mass floor, one with no
        /// room to be placed and a stillbirth all return -1 and count in
        /// <see cref="ImmigrantsRefused"/>; nothing is thrown, because the exchange picks among
        /// the living of a world with this one's rules and a refusal is a fact about the body.
        /// </para>
        /// <para>
        /// <b>Not mutated.</b> The genome is copied, so the source world's creature and this one
        /// share nothing, and the copy is planted as it is: the point is the body that lived
        /// there. In a Central world a genome without a central brain is converted as a pool
        /// founder's is; a central genome is refused in a per-node world, which a group of one
        /// config cannot produce.
        /// </para>
        /// <para>
        /// <b>Its generation depth is the one it carried</b>, so a world's deepest lineage says how
        /// many births made the body, wherever they happened. Its row's parent is -1, and the
        /// row's <c>from</c> and <c>fid</c> say where the chain goes on.
        /// </para>
        /// </remarks>
        /// <returns>The immigrant's id in this world, or -1 when it was refused.</returns>
        /// <exception cref="InvalidOperationException">
        /// Species accounting is on (<see cref="RunConfig.SpeciesDriftThreshold"/> above 0). See
        /// the remark in the body: D159's species rule is not built yet.
        /// </exception>
        public long Immigrate(Genome genome, int fromWorld, long fromId, int generationDepth)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (fromWorld < 0) throw new ArgumentOutOfRangeException(nameof(fromWorld), fromWorld, "A world's place in its group is 0 or above.");
            if (fromId < 0) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, "A creature's id is 0 or above.");
            if (generationDepth < 0) throw new ArgumentOutOfRangeException(nameof(generationDepth), generationDepth, "A generation depth is 0 or above.");

            // The owner's ruling of 2026-10-09 (D159): an immigrant keeps its home species, tagged
            // with the world it came from. The tag carries the original home: an immigrant's child
            // that migrates again is still of the species it was founded in, tagged with that
            // first world, not with the world it left last. Building it means a species registry
            // that holds another world's founders, and round 58 runs with species off, where every
            // body is species 0 and the ruling is inert. So a world with species on refuses an
            // immigrant rather than found a new species for it, which is what AssignSpecies would
            // do with a parentless body and what the ruling forbids.
            if (Config.SpeciesDriftThreshold > 0f)
            {
                throw new InvalidOperationException(
                    "An immigrant cannot be planted in a world with species accounting on " +
                    FormattableString.Invariant($"(SpeciesDriftThreshold {Config.SpeciesDriftThreshold}). ") +
                    "D159's ruling is that an immigrant keeps its home species, tagged with the world " +
                    "it came from, and this build has no tagged species path; planting it would found " +
                    "a new species instead. Run the exchange with species off, or build the tagged path.");
            }

            bool central = Config.Brain.BrainModel == BrainModel.Central;
            if (genome.Brain != null && !central)
            {
                throw new ArgumentException(
                    "An immigrant with a central brain cannot be planted in a per-node world; the " +
                    "worlds of one group run one brain model.", nameof(genome));
            }

            EnsureFounderAcceptance();
            LastImmigrantRefusal = ImmigrantRefusal.None;

            genome = genome.Clone();
            if (genome.Brain == null && central) CentralWiring.Convert(genome);

            ulong seed = Rng.SeedFor(Seed, _nextIndex++);
            float height = -new Rng(seed).Range(0f, Config.FounderDepthSpread);

            Organism planted = AdmitFounder(
                genome, seed, height, FounderSource.None, poolIndex: -1,
                fromWorld: fromWorld, fromId: fromId, generationDepth: generationDepth);

            return planted != null ? planted.Id : -1;
        }

        /// <summary>
        /// The half of a founder's spawn that follows its genome and depth: patch, development,
        /// birth fraction, the placer, <see cref="Admit"/> and the count — shared by the lottery's
        /// founders (<see cref="SpawnFounder"/>) and D117's pool (<see cref="SpawnPoolFounder"/>),
        /// so the two cannot drift apart — and by D159's immigrants (<see cref="Immigrate"/>), so
        /// that an immigrant is placed and endowed exactly as a founder is.
        /// </summary>
        /// <remarks>
        /// An immigrant (<paramref name="fromWorld"/> 0 or above) differs from a founder in what
        /// is recorded and counted, and nowhere else: its row is <see cref="BirthKind.Immigration"/>
        /// with the world and the creature it came from and the generation depth it carried, and
        /// its admission and every refusal are counted in <see cref="Immigrated"/> and
        /// <see cref="ImmigrantsRefused"/> and in none of the founders' counters. A founder's call
        /// passes none of the three and is the call it always was.
        /// </remarks>
        /// <returns>The admitted creature, or null when it was refused or stillborn.</returns>
        private Organism AdmitFounder(
            Genome genome, ulong seed, float height, FounderSource source, int poolIndex,
            bool atFloor = false,
            int fromWorld = -1, long fromId = -1, int generationDepth = 0)
        {
            bool immigrant = fromWorld >= 0;

            // D061. A second, independent seed slot, drawn only when there is more than one
            // patch to land in — the CLAUDE.md guard: any new Rng draw on a path that runs at
            // K=1 breaks bit-identity, so this is skipped entirely rather than drawn and
            // discarded. A separate draw rather than one more call against `rng` above: reusing
            // it would make where a founder lands depend on how many draws GenomeFactory.Founder
            // happened to make, coupling two things D061 wants independent of each other.
            // D077. The draw is retired in a shared volume — a founder's patch is wherever
            // the placement below put it, and drawing an index as well would give the world
            // two answers to the same question. Written as one condition rather than nested,
            // so the tiled branch is the same expression it has always been.
            int patch = 0;
            if (!Config.SharedSpace && PatchCount > 1)
            {
                ulong patchSeed = Rng.SeedFor(Seed, _nextIndex++);
                patch = new Rng(patchSeed).Range(PatchCount);
            }

            // D146: a central founder's brain is wired to its own body. A copy, since a pool founder
            // is the pool's genome itself and the pool is drawn from again.
            if (genome.Brain != null) genome = genome.Clone();

            Phenotype adult = Developer.Develop(
                genome, Config.Development, null, Config.Shapes, cellTypes: Config.CellTypes);

            // A pool founder's brain is the pool's, already wired: it is checked against the body
            // and never mutated, by SpawnPoolFounder's rule that the point is the body the
            // launcher named. Only an empty brain (a lottery founder's) is wired fresh.
            if (genome.Brain != null)
            {
                GrowBrain(genome, adult, seed, wireFresh: genome.Brain.Outputs.Count == 0, mutate: false);
            }

            // fable-propose-growth.md rule 7: a founder is born as a child is, at its own
            // genome's birth fraction, so the founding lottery runs under the same rule as
            // every birth rather than seeding the world with adults nobody paid for. The
            // reserve the floor gives it is scaled the same way, so a founder placed at a
            // fifth of its adult body arrives with a fifth of the purse — otherwise the floor
            // would hand the smallest bodies the largest head starts.
            bool admissible = NewbornFrom(
                genome, adult, out Phenotype body, out double tissue, out float birthFraction);

            // Rule 3 applies to a founder's body too, and the floor's answer is simply to
            // draw again next step. Counted rather than retried here, for the same reason a
            // stillborn founder is counted: a floor that redrew until something fitted would
            // be selecting for viability instead of sampling the genome space, and rule 3
            // exists because every divergence on record was a newborn.
            if (!admissible)
            {
                // D159: an immigrant is refused quietly and counted, never thrown. The exchange
                // picks among the living of a world with this world's rules, so a refusal here is
                // a fact about the body and not a mistake in the run.
                if (immigrant)
                {
                    ImmigrantsRefused++;
                    LastImmigrantRefusal = ImmigrantRefusal.UnderMassFloor;
                    return null;
                }

                FoundersUnderMassFloor++;
                CountFounderAttempt(source, atFloor);
                return null;
            }

            bool shared = Config.SharedSpace && Placement != null && body.PartCount > 0;

            // D077. A world with no room left refuses a founder exactly as it refuses a
            // birth, and the attempt is still counted — for the trickle's sake, per the
            // remark below, and because a floor that retried until something fitted would be
            // packing the world rather than sampling it.
            if (shared && !Placement.TryReserveFounder(adult, ref height, out patch))
            {
                if (immigrant)
                {
                    ImmigrantsRefused++;
                    LastImmigrantRefusal = ImmigrantRefusal.NoRoom;
                    return null;
                }

                CrowdedStillbirths++;
                CountFounderAttempt(source, atFloor);
                return null;
            }

            // The round 48 founding ruling (RunConfig.FounderEndowmentSeconds): seconds of the
            // newborn body's own standing cost, at age 0, on top of the floor's purse. Created
            // where the purse is — Admit credits the whole start to EnergyIn and, over ρ, to
            // MatterInfluxedTotal (D098's leg 9) — so both books close with no new term. At 0
            // nothing is computed and the purse is the recorded expression, bit for bit.
            double endowment = FounderEndowmentFor(body);

            // The adult's tissue, measured once here for the cap below and for Admit. A pure
            // function of the adult and the config, so hoisting it moves no number.
            double adultTissue = Metabolism.TissueJoules(adult, Config);

            // D124, the owner's ruling for round 49: the start, purse and endowment together, is
            // at most f of the founder's own breeding gate plus what its growth will cost, so that
            // after it has grown it holds at most f of its gate and has to earn the rest. The cut
            // comes out of the endowment first and then the purse, and is made here, before Admit,
            // so that what is created is what Admit credits to EnergyIn and, over ρ, to the matter
            // influx: both books close with no new term. At 0 nothing is computed and the start is
            // the recorded expression, bit for bit.
            double capCut = 0d;
            double start = endowment > 0d
                ? Config.FounderEnergyJoules * birthFraction + endowment
                : Config.FounderEnergyJoules * birthFraction;

            if (Config.FounderReserveCapFraction > 0f)
            {
                double cap = FounderStartCap(genome, adult, adultTissue, tissue);

                if (start > cap)
                {
                    capCut = start - cap;
                    endowment = capCut < endowment ? endowment - capCut : 0d;
                    start = cap;
                }
            }

            // Round 49's landing point: the reservation's x and z at the height the placer handed
            // back, which is the height the founder is admitted at. Read here, with the
            // reservation outstanding, because nothing in Core holds a founder's x and z until
            // the harness has built its body; a placer that keeps no coordinates says so and the
            // row carries no landing reading. A read of the slot, so nothing moves.
            Float3? landing = null;
            if (shared && Placement.TryReservedPosition(out Float3 reserved))
            {
                landing = new Float3(reserved.X, height, reserved.Z);
            }

            Organism founder = Admit(
                genome, body, immigrant ? BirthKind.Immigration : BirthKind.Floor, seed,
                parentId: -1, generationDepth: generationDepth,
                energy: start,
                tissue: tissue, heightY: height, parent: null,
                patch: patch, adultPhenotype: adult,
                adultTissue: adultTissue,
                founderSource: source, poolIndex: poolIndex, endowment: endowment,
                landing: landing, capCut: capCut,
                immigrantFromWorld: fromWorld, immigrantFromId: fromId);

            if (shared)
            {
                if (founder != null) Placement.Commit(founder.Id);
                else Placement.Release();
            }

            if (immigrant)
            {
                // D124's cut is on the immigrant's row; the two counters stay the founders'.
                if (founder != null)
                {
                    Immigrated++;
                    _living.Add(founder);
                }
                else
                {
                    ImmigrantsRefused++;
                    LastImmigrantRefusal = ImmigrantRefusal.Stillborn;
                }

                return founder;
            }

            // D124's two counters, of admitted founders only: a stillborn founder was given
            // nothing, so nothing was cut from it.
            if (founder != null && capCut > 0d)
            {
                FoundersCapped++;
                FounderJoulesCapped += capCut;
            }

            // A stillborn founder is still an attempt, and counting it keeps the floor's
            // trickle a trickle. Not counting it would let a step retry until something
            // developed, which is the floor quietly selecting for viability.
            CountFounderAttempt(source, atFloor);
            if (founder != null) _living.Add(founder);
            return founder;
        }

        /// <summary>
        /// A founder's endowment in joules — <see cref="RunConfig.FounderEndowmentSeconds"/> times
        /// <see cref="Metabolism.StandingWatts"/> of the body it is born with, at age 0. Zero, and
        /// nothing computed, with the endowment off.
        /// </summary>
        public double FounderEndowmentFor(Phenotype newborn)
        {
            if (newborn == null) throw new ArgumentNullException(nameof(newborn));

            float seconds = Config.FounderEndowmentSeconds;
            if (!(seconds > 0f)) return 0d;

            return Math.Max(0d, (double)seconds * Metabolism.StandingWatts(newborn, Config));
        }

        /// <summary>
        /// The most a founder may start with under D124's cap, J:
        /// <c>f × G + (adultTissue − newbornTissue)</c>, with <c>f</c>
        /// <see cref="RunConfig.FounderReserveCapFraction"/>. Infinite with the cap off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><c>G</c> is the gate the founder will meet once grown</b>:
        /// <see cref="Organism.BreedingGate(ReproductionTraits, double, float, RunConfig)"/> at
        /// the adult's tissue and the adult's <see cref="Metabolism.StandingWatts"/> at age 0,
        /// which is the expression <see cref="IsSolvent"/> reads off a living body. For a lump
        /// breeder that is the litter's price plus the margin; for a gestating one the price
        /// alone, asked of its account. Age 0 is the unworn cost, so the gate read here is never
        /// above the one the grown body meets.
        /// </para>
        /// <para>
        /// <b>The growth term is what <see cref="Grow"/> takes from the reserve in all</b>: each
        /// step debits the re-measured tissue less the tissue before it, and the steps telescope
        /// to the adult's tissue less the newborn's, whether the body grows in one step or ten.
        /// Nothing else is charged for growing: no field is drawn since D098, and the buffer
        /// <see cref="RunConfig.GrowthReserveFloor"/> keeps is a pace and not a price.
        /// </para>
        /// </remarks>
        public double FounderStartCap(
            Genome genome, Phenotype adult, double adultTissue, double newbornTissue)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (adult == null) throw new ArgumentNullException(nameof(adult));

            float fraction = Config.FounderReserveCapFraction;
            if (!(fraction > 0f)) return double.PositiveInfinity;

            double gate = Organism.BreedingGate(
                genome.Reproduction, adultTissue, Metabolism.StandingWatts(adult, Config), Config);

            double growth = Math.Max(0d, adultTissue - newbornTissue);

            return (double)fraction * gate + growth;
        }

        /// <summary>
        /// One founder attempt in its source's running total: <see cref="FloorSpawns"/> or
        /// <see cref="TrickleSpawns"/>, never both, so the table's <c>floor</c> column still
        /// reads the floor alone once the trickle is on.
        /// </summary>
        /// <remarks>
        /// A D117 pool founder is counted where it arrived, the floor or the trickle, and also in
        /// <see cref="PoolSpawns"/>, so <c>floor</c> and <c>trickle</c> still read every founder
        /// each drew and <c>pool</c> the ones of either that came from the pool. A world whose
        /// floor share is 0 counts no pool founder at the floor, so there <c>pool</c> is still a
        /// share of <c>trickle</c>.
        /// </remarks>
        private void CountFounderAttempt(FounderSource source, bool atFloor)
        {
            if (source == FounderSource.Trickle) TrickleSpawns++;
            else if (source == FounderSource.Pool)
            {
                if (atFloor) FloorSpawns++;
                else TrickleSpawns++;
                PoolSpawns++;
            }
            else FloorSpawns++;
        }

        /// <summary>
        /// Injects <paramref name="count"/> copies of <paramref name="genome"/> at
        /// <paramref name="heightY"/> — D060's invasion assay. A hand that builds the experimental
        /// condition, labeled as such: the assay can never answer the endogenous question of
        /// whether the world's own mutation supply finds a consumer, only whether one persists
        /// once placed. Call it once, at whatever simulated time a pre-registration names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Follows <see cref="EnforceFloor"/>'s accounting exactly, because an inoculant is a
        /// second way energy enters the world from nothing — not a third.</b> Each copy is
        /// admitted with <see cref="RunConfig.FounderEnergyJoules"/>, credited to
        /// <see cref="MatterInfluxedTotal"/> over rho like a floor founder's (D098's leg 9);
        /// generation depth 0 and no parent, so it founds its own species
        /// under D057 exactly as a floor founder does (<see cref="AssignSpecies"/> branches on
        /// <c>parent == null</c>, not on <see cref="BirthKind"/>).
        /// </para>
        /// <para>
        /// <b>The genome itself is never mutated.</b> Every copy develops the identical stored
        /// genome — the point is to introduce a verified lineage rather than wait for one to arrive
        /// by chance, so a mutated copy would not be the genome the caller verified.
        /// </para>
        /// <para>
        /// <b>Seeds derive from the world's own seed stream</b>, the same
        /// <c>Rng.SeedFor(Seed, _nextIndex++)</c> a floor spawn draws, so a run replays identically
        /// from <c>(genome, seed, configHash, inoculation)</c> — nothing here reaches for an
        /// independent source of randomness. The seed labels the birth (<see
        /// cref="Organism.BirthSeed"/>) rather than driving anything: there is no mutation to seed
        /// and <paramref name="heightY"/> is fixed rather than drawn, unlike a floor founder's
        /// scattered depth.
        /// </para>
        /// </remarks>
        /// <param name="genome">Copied verbatim into every inoculant. Not mutated.</param>
        /// <param name="count">How many copies to admit. Stillbirths still consume a seed and are
        /// still counted in <see cref="Inoculated"/>, matching the floor's own accounting.</param>
        /// <param name="heightY">World height, metres, every copy is placed at.</param>
        /// <exception cref="ArgumentException">
        /// The genome's newborn body would be under <see cref="RunConfig.MinNewbornPartKilograms"/>
        /// (rule 3). Thrown before anything is admitted, so the world is untouched.
        /// </exception>
        public void Inoculate(Genome genome, int count, float heightY)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            EnsureFounderAcceptance();

            genome = InoculantReady(genome, count > 0);

            for (int i = 0; i < count; i++)
            {
                ulong seed = Rng.SeedFor(Seed, _nextIndex++);

                // D061. Same second-seed-slot pattern as EnforceFloor, for the same reason: drawn
                // only when there is more than one patch to land in, so a K=1 assay is untouched
                // and the D060 assay's own guarantees (same seed stream, replays identically) are
                // extended rather than disturbed.
                // D077. Retired in a shared volume for EnforceFloor's reason, and written the
                // same way so the two paths cannot drift apart.
                int patch = 0;
                if (!Config.SharedSpace && PatchCount > 1)
                {
                    ulong patchSeed = Rng.SeedFor(Seed, _nextIndex++);
                    patch = new Rng(patchSeed).Range(PatchCount);
                }

                Phenotype adult = Developer.Develop(
                    genome, Config.Development, null, Config.Shapes, cellTypes: Config.CellTypes);

                // fable-propose-growth.md rule 7, exactly as EnforceFloor applies it: an
                // inoculant has no parent either, so it is born at its own genome's birth
                // fraction with the founder's purse scaled the same way. An assay that placed
                // adults into a world of growing bodies would be measuring a hand rather than a
                // lineage.
                // Refused above, once, before the loop: the preflight and this call see the
                // same genome and the same config, so this cannot come back false here.
                NewbornFrom(genome, adult, out Phenotype body, out double tissue,
                    out float birthFraction);

                bool shared = Config.SharedSpace && Placement != null && body.PartCount > 0;

                // Per copy, not per call: the placer may raise one inoculant clear of the sea bed
                // and the next must still be asked for the depth the caller named. The assay's
                // own guarantee is that every copy is put at heightY, and a copy that cannot be
                // is a fact about the floor rather than a new depth for the whole cohort.
                float placedHeight = heightY;

                if (shared && !Placement.TryReserveFounder(adult, ref placedHeight, out patch))
                {
                    CrowdedStillbirths++;
                    Inoculated++;
                    continue;
                }

                Organism creature = Admit(
                    genome, body, BirthKind.Inoculation, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules * birthFraction,
                    tissue: tissue, heightY: placedHeight, parent: null,
                    patch: patch, adultPhenotype: adult,
                    adultTissue: Metabolism.TissueJoules(adult, Config));

                if (shared)
                {
                    if (creature != null) Placement.Commit(creature.Id);
                    else Placement.Release();
                }

                // A stillborn inoculant is still an attempt — see EnforceFloor's identical remark.
                Inoculated++;
                if (creature != null) _living.Add(creature);
            }
        }

        /// <summary>
        /// The genome an inoculant is admitted with, and the refusals asked of it before anything is
        /// spent: <see cref="Inoculate"/>'s and <see cref="InoculateAt"/>'s, written once so that the
        /// two doors cannot drift apart. <paramref name="admitting"/> false (no copy asked for) skips
        /// rule 3, as <see cref="Inoculate"/> always has.
        /// </summary>
        private Genome InoculantReady(Genome genome, bool admitting)
        {
            // D146: an inoculant runs the brain it carries, either model in either world; an assay
            // names its genome deliberately (the nursery's hand-wired controls are old-model brains
            // in a central world). A central genome whose brain is empty is wired fresh to its body,
            // on a stream of its own so that no draw of the world moves.
            if (genome.Brain != null && genome.Brain.Outputs.Count == 0)
            {
                genome = genome.Clone();
                genome.NumberEdges();
                Phenotype wired = Developer.Develop(
                    genome, Config.Development, null, Config.Shapes, cellTypes: Config.CellTypes);
                GrowBrain(genome, wired, Rng.SeedFor(Seed, InoculantBrainStream), wireFresh: true, mutate: false);
            }

            // D111, before anything is spent: an inoculant floating off its centre in a world
            // that does not price it would carry a trait nothing charges for. Loud, for rule 3's
            // reason below — an assay names its genome deliberately.
            // Only this rule is asked here; everything else Validate says is Develop's to say, as
            // it always was, against the registry it always used.
            if (!(Config.BuoyancyOffsetWattsPerCubicMetre > 0f))
            {
                for (int n = 0; n < genome.Nodes.Count; n++)
                {
                    if (genome.Nodes[n].BuoyancyOffset == 0f) continue;

                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"inoculant's node {n} floats off its centre (BuoyancyOffset ") +
                        FormattableString.Invariant($"{genome.Nodes[n].BuoyancyOffset}) in a world ") +
                        "that does not price it (BuoyancyOffsetWattsPerCubicMetre 0), D111. " +
                        "Set the price, or bring the genome's offsets to 0.",
                        nameof(genome));
                }
            }

            // Rule 3, asked once and before anything is spent. Every copy is the same genome
            // developed the same way, so the answer is the same for all of them; asking inside
            // the loop would burn a seed and leave the world half inoculated on the way to the
            // same exception. Loud rather than quiet, unlike the floor's own draw: an assay names
            // one genome deliberately, so a body the physics cannot carry is a mistake in the
            // pre-registration, and silently placing nothing would leave an experiment reporting
            // an inoculation that never happened.
            if (admitting && !NewbornFrom(
                    genome,
                    Developer.Develop(genome, Config.Development, null, Config.Shapes, cellTypes: Config.CellTypes),
                    out Phenotype preflight, out _, out float preflightFraction))
            {
                throw new ArgumentException(
                    "inoculant is under the newborn mass floor: its lightest part weighs " +
                    FormattableString.Invariant(
                        $"{LightestPartKilograms(preflight):0.####} kg at its birth fraction of ") +
                    FormattableString.Invariant($"{preflightFraction:0.###}, and the floor is ") +
                    FormattableString.Invariant(
                        $"{Config.MinNewbornPartKilograms:0.####} kg ") +
                    "(fable-propose-growth.md rule 3). Raise the genome's birth investment, " +
                    "lower its brood size, or lower MinNewbornPartKilograms.",
                    nameof(genome));
            }

            return genome;
        }

        /// <summary>Stillbirths — genomes development refused, for any of its reasons.</summary>
        /// <remarks>
        /// Worth counting rather than discarding silently. A lineage reaches this by drifting off
        /// either end of the size range (§4.5, <see cref="DevelopmentLimits.MaxPartVolume"/>), so
        /// a rising stillbirth rate says mutation is pushing bodies past what development will
        /// build — which looks, in a population count, exactly like ordinary mortality.
        ///
        /// Every such body until 2026-09-19 had no parts at all. Since the owner's ruling of that
        /// day a body grown into itself is refused too, and this is the total of both;
        /// <see cref="SelfOverlapStillbirths"/> says how many were the second, and reads 0 with
        /// <see cref="RunConfig.SelfOverlapDepthFraction"/> off, which is every run on file.
        /// </remarks>
        public long Stillbirths { get; private set; }

        /// <summary>
        /// Stillbirths refused for standing inside themselves — the owner's ruling of 2026-09-19.
        /// Included in <see cref="Stillbirths"/>, and 0 unless
        /// <see cref="RunConfig.SelfOverlapDepthFraction"/> is above 0.
        /// </summary>
        /// <remarks>
        /// Counted apart as well as together because the two say different things about what
        /// mutation is doing. A body of no parts is a lineage shrinking out of existence; a body
        /// grown into itself is a plan that folds, which is what round 41c's knots and round
        /// 41d's sprawl were (logbook/0107). A rising share of this one is the rule biting, and
        /// whether it bites hard enough to be selection rather than hygiene is read from it
        /// against <see cref="Births"/> in the same window.
        /// </remarks>
        public long SelfOverlapStillbirths { get; private set; }

        /// <summary>
        /// The body a parentless creature is born with — fable-propose-growth.md rule 7.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A founder and an inoculant have no parent to take a share from, so they take it from
        /// themselves.</b> The birth fraction is the one every child of this genome would get,
        /// <c>BirthInvestment / BroodSize</c> less the newborn reserve, capped at the whole adult —
        /// so a lineage that starts small starts small however it entered the world, and the
        /// founding lottery is run under the rule the world actually has rather than under the old
        /// one where everything arrived grown.
        /// </para>
        /// <para>
        /// A stillbirth passes straight through: a body of no parts has no size to be born at, and
        /// <c>Admit</c> is about to refuse it for the older reason.
        /// </para>
        /// </remarks>
        /// <param name="birthFraction">
        /// What was realised, not what was asked for. The reserve is scaled by this, so it has to
        /// be the fraction the body actually came out at.
        /// </param>
        /// <returns>
        /// False when the body would arrive under <see cref="RunConfig.MinNewbornPartKilograms"/>.
        /// Rule 3 is a fact about what the solver can carry, and the solver does not care whether
        /// a body had a parent, so a founder and an inoculant are held to it exactly as a child
        /// is (added in the review of 2026-09-08; rule 7 already said founders are born as
        /// children are, and the first build applied the floor only to conceptions). A stillbirth
        /// passes as true: there is no body to weigh, and <c>Admit</c> refuses it for the older
        /// reason a line below.
        /// </returns>
        private bool NewbornFrom(
            Genome genome, Phenotype adult,
            out Phenotype newborn, out double tissue, out float birthFraction)
        {
            double adultTissue = Metabolism.TissueJoules(adult, Config);
            ReproductionTraits traits = genome.Reproduction;

            float wanted = traits.BirthInvestment / traits.BroodSize *
                           (1f - Config.NewbornReserveFraction);

            if (adult.PartCount == 0)
            {
                newborn = adult;
                tissue = adultTissue;
                birthFraction = 1f;
                return true;
            }

            if (!(wanted > 0f) || wanted >= 1f)
            {
                newborn = adult;
                tissue = adultTissue;
                birthFraction = 1f;
            }
            else
            {
                newborn = adult.ScaledFor(Config, (float)Math.Pow(wanted, 1d / 3d));
                tissue = Metabolism.TissueJoules(newborn, Config);
                birthFraction = adultTissue > 0d ? (float)(tissue / adultTissue) : 1f;
            }

            return !IsUnderTheMassFloor(newborn);
        }

        /// <summary>
        /// The mass of the lightest part of a body, kilograms. For refusal messages only.
        /// </summary>
        private static float LightestPartKilograms(Phenotype body)
        {
            IReadOnlyList<PhenotypePart> parts = body.Parts;
            if (parts.Count == 0) return 0f;

            float lightest = float.MaxValue;
            for (int i = 0; i < parts.Count; i++)
            {
                float mass = parts[i].Volume * RunConfig.PartDensityKilogramsPerCubicMetre;
                if (mass < lightest) lightest = mass;
            }

            return lightest;
        }

        /// <summary>
        /// Develops a genome and turns it into a creature, or refuses it. Null means stillborn.
        /// </summary>
        /// <remarks>
        /// <b>A body of no parts would otherwise be immortal and free.</b> With nothing to price,
        /// its income and its upkeep are both exactly zero, so its energy never moves and the
        /// death-at-zero rule in §5A.6 never fires — a creature that costs nothing, does nothing
        /// and cannot die, occupying a slot against the population floor forever. It is reachable
        /// today: §4.5's extinction-by-shrinking prunes the root as readily as any other node.
        /// </remarks>
        /// <param name="parent">
        /// The parent, for <see cref="AssignSpecies"/> — null for a floor founder or an inoculant
        /// (<see cref="Inoculate"/>), neither of which has one. Distinct from
        /// <paramref name="parentId"/> (which both also set to -1) because species assignment
        /// needs the actual organism, not just its id, to read its current
        /// <see cref="Organism.SpeciesId"/>.
        /// </param>
        /// <param name="patch">
        /// The horizontal cell this creature is born into — D061. The parent's own patch for a
        /// reproduction (offspring inherit it, drawn nowhere), and a uniform draw from the
        /// world's seed stream for a floor founder or an inoculant, guarded behind
        /// <see cref="PatchCount"/> &gt; 1 at each call site.
        /// </param>
        /// <param name="adultPhenotype">
        /// The body this creature grows towards — fable-propose-growth.md rule 4. The same object
        /// as <paramref name="phenotype"/> for anything born at full size, and the unscaled
        /// development for anything born smaller.
        /// </param>
        /// <param name="adultTissue">
        /// <see cref="Metabolism.TissueJoules"/> of <paramref name="adultPhenotype"/>. Passed
        /// rather than measured here because every caller has just computed it to decide how big
        /// the newborn is, and measuring it twice is a walk over every part for nothing.
        /// </param>
        /// <param name="founderSource">
        /// Which door a <see cref="BirthKind.Floor"/> founder came through, floor or trickle
        /// (D115), carried to its lineage row; <see cref="FounderSource.None"/> for everything
        /// else.
        /// </param>
        /// <param name="poolIndex">
        /// For a <see cref="FounderSource.Pool"/> founder, the index of the pool genome it copies
        /// (D117), carried to its lineage row; -1 for everything else.
        /// </param>
        /// <param name="endowment">
        /// The part of <paramref name="energy"/> that is the founder's endowment
        /// (<see cref="RunConfig.FounderEndowmentSeconds"/>), in joules, carried to its lineage
        /// row as <c>endow</c>. Already inside <paramref name="energy"/>, and booked with it; 0
        /// for everything else.
        /// </param>
        /// <param name="landing">
        /// For a founder, where it was placed (<see cref="IBodyPlacement.TryReservedPosition"/>,
        /// at the admitted height), for round 49's landing readings on its lineage row; null for
        /// everything else and for a founder whose placer keeps no coordinates.
        /// </param>
        /// <param name="capCut">
        /// For a founder, the joules D124's cap took from its start
        /// (<see cref="RunConfig.FounderReserveCapFraction"/>), carried to its lineage row as
        /// <c>capcut</c>. Never created, so not inside <paramref name="energy"/>; 0 for
        /// everything else and for a founder the cap did not cut.
        /// </param>
        private Organism Admit(
            Genome genome, Phenotype phenotype, BirthKind kind, ulong seed, long parentId,
            int generationDepth, double energy, double tissue, float heightY, Organism parent,
            int patch, Phenotype adultPhenotype, double adultTissue,
            FounderSource founderSource = FounderSource.None, int poolIndex = -1,
            double endowment = 0d,
            string budCells = null, int budsExpressed = 0,
            Float3? landing = null, double capCut = 0d,
            int immigrantFromWorld = -1, long immigrantFromId = -1)
        {
            // D241. The single door for the growth-reserve gene, asked before anything else so that
            // a refusal leaves the world as it was. In a world with the climb a body whose genome
            // carries no gene gets one, on a clone: a pool genome is shared between founders and the
            // caller's genome is never written to. A gene outside the world's range, or any gene in a
            // world without the climb, is refused by name. Resume does not come through here; a
            // checkpoint's genomes already carry what they carried.
            bool climbing = Config.GrowthAdultSeconds > 0f;
            if (climbing)
            {
                if (!genome.GrowthReserveSeconds.HasValue)
                {
                    genome = genome.Clone();
                    genome.GrowthReserveSeconds = Config.GrowthReserveStartSeconds;
                }
                else
                {
                    float gene = genome.GrowthReserveSeconds.Value;
                    if (!(gene >= Config.GrowthReserveMinSeconds && gene <= Config.GrowthReserveMaxSeconds))
                    {
                        throw new InvalidOperationException(
                            FormattableString.Invariant(
                                $"Genome carries GrowthReserveSeconds {gene}, outside this world's range ") +
                            FormattableString.Invariant(
                                $"[{Config.GrowthReserveMinSeconds}, {Config.GrowthReserveMaxSeconds}] ") +
                            "(GrowthReserveMinSeconds, GrowthReserveMaxSeconds; D241).");
                    }
                }
            }
            else if (genome.GrowthReserveSeconds.HasValue)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Genome carries GrowthReserveSeconds {genome.GrowthReserveSeconds.Value}, but ") +
                    "this world has no climb (GrowthAdultSeconds is 0), so nothing would read the gene " +
                    "and it would be recorded on no row (D241).");
            }

            // The owner's ruling of 2026-09-19: a body that would grow into itself is not born.
            // Asked here rather than at each of the three call sites so that a founder and an
            // inoculant are held to it exactly as a child is — Admit is the one door all three
            // come through — and so that the refusal settles down the same path a body of no
            // parts already settles down, with both books closing as they always did.
            //
            // The test is scale-free (BoxOverlap.DepthThreshold), so asking it of the newborn is
            // asking it of the adult it will grow into.
            bool selfOverlapping =
                phenotype.PartCount > 0 &&
                Config.SelfOverlapDepthFraction > 0f &&
                phenotype.SelfOverlappingPairs(Config.SelfOverlapDepthFraction) > 0;

            if (selfOverlapping) SelfOverlapStillbirths++;

            if (phenotype.PartCount == 0 || selfOverlapping)
            {
                Stillbirths++;

                // The matter still has to balance, and since D098 it balances by arriving rather
                // than by leaving. A floor spawn's and an inoculation's start was never created
                // (see the EnergyIn credit below), so nothing is owed; an offspring's was already
                // deducted from its parent — the reserve and the tissue both, since the price is
                // tissue + reserve + overhead — and it is charged matter that has to go
                // somewhere. It goes into the water at the point the body would have been born
                // at, which is the parent's. The overhead stays burnt: it was spent, not given.
                if (kind == BirthKind.Reproduction)
                {
                    // Quantised once at the field's door and booked as the same number — Bury's
                    // rule, for the same reason.
                    float orphaned = (float)(energy + tissue);
                    if (orphaned > 0f)
                    {
                        FieldPoint at = parent != null
                            ? new FieldPoint(new Float3(parent.X, heightY, parent.Z), patch)
                            : FieldPoint.At(heightY, patch);

                        Nutrients.Deposit(at, orphaned);
                        DetritusDepositedTotal += orphaned;
                    }
                }

                return null;
            }

            var creature = new Organism
            {
                Id = _nextId++,
                ParentId = parentId,
                GenerationDepth = generationDepth,
                BirthSeed = seed,
                Genome = genome,
                Phenotype = phenotype,
                AdultPhenotype = adultPhenotype,
                AdultTissueJoules = adultTissue,

                // Derived from the two tissue figures rather than passed alongside them, so the
                // body fraction is a readout of the energy ledger and cannot disagree with it.
                BodyFraction = adultTissue > 0d && tissue < adultTissue
                    ? (float)(tissue / adultTissue)
                    : 1f,
                Energy = energy,
                TissueJoules = tissue,
                HeightY = heightY,
                BirthHeightY = heightY,
                Patch = patch,

                // D083. Beside the parent until the simulator reports where the body actually
                // is, and at the patch's centre for a body with no parent — the cell field never
                // reads these, and in a shared volume the next Observe overwrites them.
                X = parent != null ? parent.X : PatchCentreX(patch),
                Z = parent != null ? parent.Z : PatchCentreZ(patch),
                StandingWatts = Metabolism.StandingWatts(phenotype, Config),
            };

            // One pass over the parts, at the one moment a body plan is built. Growth changes a
            // body's size and never what it is made of (fable-propose-growth.md rule 4), so the
            // two flags below are fixed from birth to death; only the volume moves, and
            // <see cref="Grow"/> refreshes that where it changes it. The alternative is the
            // per-creature per-step loop the absorptive log would otherwise need just to decide
            // whether to record a creature at all.
            bool photosynthetic = false;
            bool fat = false;
            IReadOnlyList<PhenotypePart> admitted = phenotype.Parts;
            for (int i = 0; i < admitted.Count; i++)
            {
                if (admitted[i].CellTypeId == CellTypeIds.Photosynthetic) photosynthetic = true;
                if (admitted[i].CellTypeId == CellTypeIds.Fat) fat = true;
            }

            creature.AbsorptiveVolume = AbsorptiveVolumeOf(phenotype);
            creature.HasAbsorptiveTissue = HasAbsorptive(phenotype);
            creature.HasPhotosyntheticTissue = photosynthetic;

            // D106 item 2. A property of the genome, so it is read once here beside the other
            // cached facts about a body — and it is 0 for every founder and for every body in
            // the record, because the gene enters by mutation alone.
            creature.IndeterminateNodes = IndeterminateNodesOf(genome);

            // D106 item 3's three flags, read off the body for the reason the cell-type flags are:
            // the mouth's damage pass asks "is anything in this world armed" before it builds an
            // index of the living, and the report asks the three shares at every sample. Unlike
            // the cell-type flags they are refreshed on a plan change (AdoptPlan), because a body
            // can lose its only claw to a bite.
            ReadAttributeFlags(creature, phenotype);

            // Endowment and body are transferred from the parent, and a founder's, an inoculant's
            // or an immigrant's are created out of nothing, so only those three are income the
            // world has to account for. Conflating any of this with reproduction would let a
            // population manufacture energy by breeding. An immigrant is created here because
            // the creature it was copied from stays where it is: D159 trades genomes, not bodies.
            if (kind == BirthKind.Floor || kind == BirthKind.Inoculation ||
                kind == BirthKind.Immigration)
            {
                // D098's leg 9. The experimenter's hand is a source in both books, and the books
                // say so: the joules are created and the same over ρ is credited as an influx of
                // matter, so the matter identity closes over a founding lottery that takes
                // nothing out of the water.
                EnergyIn += energy + tissue;
                MatterInfluxedTotal += (energy + tissue) / Config.JoulesPerUnit;
            }

            // D057. After the stillbirth check, not before: a genome that never became a creature
            // has no species to found or inherit, and computing one would be wasted work on top
            // of the mutate-and-develop pass that already found it unviable.
            AssignSpecies(creature, parent);

            // The lineage-events instrument (pre-round-8, LITERATURE-REVIEW.md §9 item 9). After
            // AssignSpecies, not before, so the row carries the species the birth actually landed
            // in rather than a default. Stillbirths never reach here — the check above returns
            // null first — so this is exactly "an id was assigned", which is what a lineage row
            // means. The photosynthetic flag is the same local the pass above computed for
            // Organism.HasPhotosyntheticTissue, passed rather than recomputed, so a row and the
            // creature it describes can never disagree about what the body is made of.
            //
            // Round 49's landing readings go on the same row: for a founder placed where the
            // placer can say, the food its body eats at that point and its column's mean, read
            // now, before the body has fed once. The flags are the row's own abs and pho.
            float landingSnow = float.NaN, landingSnowColumn = float.NaN;
            float landingMatter = float.NaN, landingMatterColumn = float.NaN;

            float landingCorpse = float.NaN, landingCorpseMean = float.NaN;

            if (landing.HasValue && founderSource != FounderSource.None)
            {
                ReadLanding(
                    landing.Value, patch, creature.HasAbsorptiveTissue, photosynthetic,
                    out landingSnow, out landingSnowColumn,
                    out landingMatter, out landingMatterColumn);

                // D129's own reading, for a mouth in a world whose mouths follow the corpses. A
                // read of the column sums the placer's acceptance already takes, so nothing moves.
                if (Config.FoundersFollowCorpses && HasMouth(phenotype) && Nutrients is GridField corpseGrid)
                {
                    SumCorpseColumns(corpseGrid);
                    int landed = corpseGrid.ColumnIndexAt(landing.Value.X, landing.Value.Z);
                    landingCorpse = (float)_corpseColumnJoules[landed];
                    landingCorpseMean = (float)(_corpseColumnsTotal / Math.Max(1, corpseGrid.LiveColumnCount));
                }
            }

            _lineageEvents.Add(LineageEvent.Birth(
                ElapsedSeconds, creature.Id, parentId, kind, generationDepth, creature.SpeciesId,
                HasAbsorptive(phenotype), phenotype.TotalDof > 0, photosynthetic, patch,
                creature.BodyFraction, genome.AdultScale, genome.Reproduction.ReserveMargin,
                creature.IndeterminateNodes,
                CarriesAttribute(genome, n => n.Attack),
                CarriesAttribute(genome, n => n.Intake),
                CarriesAttribute(genome, n => n.Protection),
                founderSource, poolIndex, endowment, budCells, budsExpressed,
                genome.Reproduction.Mode, genome.Reproduction.GestationShare,
                landingSnow, landingSnowColumn, landingMatter, landingMatterColumn,
                capCut, landingCorpse, landingCorpseMean,
                LifespanRecorded ? genome.LifespanScale : float.NaN,
                immigrantFromWorld, immigrantFromId,
                climbing ? genome.GrowthReserveSeconds.Value : float.NaN,
                hasFat: fat));

            // Record format 2's genome file, beside the lineage row and under the same guarantee:
            // exactly one per id assigned. A reference, not a copy, and nothing is read or drawn,
            // so a world that queues and one that does not step the same trajectory.
            if (QueueAdmittedGenomes) _admittedGenomes.Add(new AdmittedGenome(creature.Id, genome));

            return creature;
        }

        /// <summary>
        /// Round 49's landing readings for a founder at <paramref name="at"/>: the edible density
        /// of each food its body eats at the point (<c>fsnow</c>, <c>fmat</c>) and the mean over
        /// the point's column (<c>fcol</c>, <c>fmcol</c>), each NaN when the body does not eat that
        /// food or the field is not a grid.
        /// </summary>
        /// <remarks>
        /// Reads only. It is called between the placer's reservation and the admission of the
        /// body, and the step's field passes have all run by then (founders are spawned at the end
        /// of <see cref="Step"/>), so the field it reads is the one the founder rule read when it
        /// chose the spot. The body's first meal is taken in the next step, from the same field
        /// plus whatever a body the solver killed in between left in it, at wherever the physics
        /// has moved the body by then.
        /// </remarks>
        private void ReadLanding(
            Float3 at, int patch, bool eatsSnow, bool eatsMatter,
            out float snowHere, out float snowColumn, out float matterHere, out float matterColumn)
        {
            snowHere = snowColumn = matterHere = matterColumn = float.NaN;

            var point = new FieldPoint(at, patch);

            if (eatsSnow && Nutrients is GridField snow)
            {
                snowHere = snow.EdibleDensityAt(point);
                snowColumn = snow.MeanEdibleDensityInColumn(at.X, at.Z);
            }

            if (eatsMatter && Matter is GridField matter)
            {
                matterHere = matter.EdibleDensityAt(point);
                matterColumn = matter.MeanEdibleDensityInColumn(at.X, at.Z);
            }
        }

        /// <summary>
        /// Whether any part of a developed body is <see cref="CellTypeIds.Absorptive"/> — the same
        /// test <c>EvolutionRun.cs</c>'s report already applies per creature, reused here rather
        /// than reinvented so the two never disagree about what "absorptive" means.
        /// </summary>
        private static bool HasAbsorptive(Phenotype phenotype)
        {
            IReadOnlyList<PhenotypePart> parts = phenotype.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].CellTypeId == CellTypeIds.Absorptive) return true;
            }
            return false;
        }

        /// <summary>
        /// Hands over every <see cref="LineageEvent"/> queued since the last call, and forgets
        /// them — the pre-round-8 instrument's drain API, meant to be called once per report row
        /// alongside <see cref="TakeDead"/>.
        /// </summary>
        /// <remarks>
        /// Swaps the list out for a fresh one rather than copying into a new list and clearing the
        /// old one (<see cref="TakeDead"/>'s pattern): draining is on the hot report path and most
        /// intervals have plenty of events, so a swap avoids copying every element for no reason.
        /// A quiet interval costs nothing but a reference read via <see cref="Array.Empty{T}"/>.
        /// <b>Never called by anything in <see cref="World"/> itself</b> — draining only empties a
        /// list, never touches <see cref="Rng"/>, <see cref="ElapsedSeconds"/> or any economy
        /// state, so a world stepped with this called every report row and one where it is never
        /// called at all step through bit-identical trajectories.
        /// </remarks>
        public IReadOnlyList<LineageEvent> DrainLineageEvents()
        {
            if (_lineageEvents.Count == 0) return Array.Empty<LineageEvent>();

            List<LineageEvent> taken = _lineageEvents;
            _lineageEvents = new List<LineageEvent>();
            return taken;
        }

        /// <summary>
        /// Whether each admitted body's genome is queued for <see cref="DrainAdmittedGenomes"/> —
        /// record format 2's <c>genomes.jsonl.gz</c> (<c>logbook/specs/record-and-film-spec.md</c>
        /// A1). Off unless the console farm turns it on.
        /// </summary>
        /// <remarks>
        /// <b>Off by default, because a queue nobody drains is a leak.</b> The Unity farm drains
        /// the lineage and never this, so with the queue always on it would hold a reference to
        /// every genome the world ever admitted. Not written into a checkpoint: the farm drains
        /// the queue beside the lineage before it writes one, and a resumed run writes its living
        /// roster's genomes first, so nothing a checkpoint would carry is missing.
        /// </remarks>
        public bool QueueAdmittedGenomes { get; set; }

        /// <summary>
        /// Hands over every genome admitted since the last call and forgets them —
        /// <see cref="DrainLineageEvents"/>' pattern, and called beside it.
        /// </summary>
        public IReadOnlyList<AdmittedGenome> DrainAdmittedGenomes()
        {
            if (_admittedGenomes.Count == 0) return Array.Empty<AdmittedGenome>();

            List<AdmittedGenome> taken = _admittedGenomes;
            _admittedGenomes = new List<AdmittedGenome>();
            return taken;
        }

        /// <summary>
        /// Most rows one call to <see cref="CollectAbsorptiveLog"/> will produce for the living,
        /// and the depth of the death-row buffer.
        /// </summary>
        /// <remarks>
        /// <b>A cap, not a knob.</b> A stomach bloom of tens of thousands would make this file the
        /// largest thing a run writes — one row per eater per sample against
        /// <c>lineage.jsonl</c>'s one row per creature ever — and the reading the instrument
        /// exists for (where were they, what did they see, what did they earn) is served by two
        /// thousand of them as well as by all of them. It is deliberately not a
        /// <see cref="RunConfig"/> tunable: it changes nothing about the world, so it has no
        /// business in <c>configHash</c>.
        /// </remarks>
        public const int AbsorptiveLogRowCap = 2000;

        /// <summary>
        /// Buffers one creature's final <see cref="AbsorptiveSample"/> — called from the death
        /// path in <c>Metabolise</c>, before anything is zeroed.
        /// </summary>
        private void BufferAbsorptiveDeath(Organism creature)
        {
            if (_absorptiveDeaths.Count >= AbsorptiveLogRowCap)
            {
                _absorptiveDeathsDropped++;
                return;
            }

            _absorptiveDeaths.Add(AbsorptiveSample.For(creature, ElapsedSeconds, dead: true, Config));
        }

        /// <summary>
        /// Living creatures whose developed phenotype carries at least one photosynthetic part —
        /// the producers, counted the way the eaters already are.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The report could count the stomachs and not the leaves</b> (the Sol/GPT review of
        /// 2026-09-03, finding 2). Every trophic reading this project takes is a ratio with
        /// producers in the denominator — how many eaters a standing crop of producers supports,
        /// whether a bloom is the crop growing or the grazers failing — and the denominator was
        /// never written down. <c>alive</c> is not it: a world can be all leaves, all stomachs or
        /// a mixture, and those three read identically in every column the report had.
        /// </para>
        /// <para>
        /// <b>Phenotype, not genome.</b> <see cref="Organism.HasPhotosyntheticTissue"/> is set
        /// once, at <c>Admit</c>, from the parts a body actually developed — so a genome carrying
        /// a photosynthetic node whose subtree was pruned below <c>minPartVolume</c> is counted
        /// here as what it grew into rather than what it encodes. That is the exact gap
        /// logbook/0048's dissection found between <c>snapshots/</c> and the living population.
        /// </para>
        /// <para>
        /// <b>Pure instrumentation</b>, like <see cref="CollectAbsorptiveLog"/>: one pass over
        /// <c>_living</c> reading a flag, no <see cref="Rng"/>, no clock, no economy state. A
        /// world whose producers are counted every sample and one where this is never called take
        /// bit-identical trajectories.
        /// </para>
        /// </remarks>
        public int CountPhotosynthetic()
        {
            int count = 0;
            for (int i = 0; i < _living.Count; i++)
            {
                if (_living[i].HasPhotosyntheticTissue) count++;
            }

            return count;
        }

        /// <summary>
        /// Appends one row per living creature with absorptive tissue, then every death row
        /// buffered since the last call, and returns how many rows were left out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The living are enumerated, the dead are drained</b>, and the asymmetry is the whole
        /// shape of the instrument: a living creature can be re-read at the next sample, and a
        /// dead one has exactly one moment at which its terminal budget exists. Meant to be called
        /// once per report row, beside <see cref="DrainLineageEvents"/> and
        /// <see cref="TakeDead"/>.
        /// </para>
        /// <para>
        /// <b>The first <see cref="AbsorptiveLogRowCap"/> by id.</b> <c>_living</c> is appended to
        /// and removed from in place and ids come from a monotonic counter, so list order is id
        /// order and taking the head of the list is taking the oldest eaters — the ones with a
        /// history worth reading — rather than an arbitrary slice.
        /// </para>
        /// <para>
        /// <b>Pure instrumentation.</b> Reads world state and empties one buffer; touches no
        /// <see cref="Rng"/>, no clock and no economy state, so a world whose log is collected
        /// every sample and one where this is never called take bit-identical trajectories.
        /// </para>
        /// </remarks>
        /// <param name="into">
        /// Rows are appended; the list is not cleared first. A creature born since the last
        /// metabolic step has no ledger yet and is left out rather than written as zeros — see the
        /// comment on the skip.
        /// </param>
        /// <returns>
        /// Living creatures past the cap, plus death rows dropped because the buffer was full. Zero
        /// in every run that has never had <see cref="AbsorptiveLogRowCap"/> eaters at once.
        /// </returns>
        public int CollectAbsorptiveLog(List<AbsorptiveSample> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));

            int written = 0;
            int truncated = _absorptiveDeathsDropped;
            _absorptiveDeathsDropped = 0;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (!creature.HasAbsorptiveTissue) continue;

                // Born since the last metabolic step, so it has no ledger and no density yet.
                // Skipped rather than written as zeros: 0 J/m³ is a real density and 0 W is a
                // real budget, and a row of them would be indistinguishable from a starving
                // creature in empty water — the exact trap `flt m`'s em-dash was added after. It
                // appears at the next sample, one step old, with everything real.
                if (creature.LastStepSeconds <= 0f) continue;

                if (written >= AbsorptiveLogRowCap)
                {
                    truncated++;
                    continue;
                }

                into.Add(AbsorptiveSample.For(creature, ElapsedSeconds, dead: false, Config));
                written++;
            }

            for (int i = 0; i < _absorptiveDeaths.Count; i++) into.Add(_absorptiveDeaths[i]);
            _absorptiveDeaths.Clear();

            return truncated;
        }

        /// <summary>
        /// Assigns <see cref="Organism.SpeciesId"/> at birth — D057. The only place this project
        /// writes that property; nothing else may read it but a report (Organism's own remarks).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Three cases, in the order D057 states them.</b> The threshold off is the fast path
        /// its own doc comment promises — species 0, no registry touched, no distance computed.
        /// A floor founder has no lineage to measure from and simply founds its own. Everyone
        /// else is compared once against their parent's species' founding genome: within θ,
        /// inherit; past it, found — the child's own genome becomes the new reference, exactly as
        /// D057 specifies.
        /// </para>
        /// <para>
        /// Nothing here touches <see cref="Rng"/> or reads <see cref="ElapsedSeconds"/> for
        /// anything but a founding timestamp, so it changes no draw any other system depends on —
        /// the whole reason D057 can be pure instrumentation rather than a second thing to keep in
        /// sync with the mutation stream.
        /// </para>
        /// </remarks>
        private void AssignSpecies(Organism creature, Organism parent)
        {
            if (Config.SpeciesDriftThreshold <= 0f)
            {
                creature.SpeciesId = 0;
                return;
            }

            if (parent == null)
            {
                FoundSpecies(creature);
                return;
            }

            if (!_species.TryGetValue(parent.SpeciesId, out SpeciesFounder founder))
            {
                // Every species that can be read while the threshold is on was itself founded by
                // this method, so a miss here is a bookkeeping bug — a creature carrying a species
                // id nothing registered — rather than a data condition callers should absorb.
                throw new InvalidOperationException(
                    $"Creature {parent.Id} carries species {parent.SpeciesId}, which has no " +
                    "founder on record.");
            }

            float distance = SpeciesDistance.Between(
                creature.Genome, founder.Genome,
                Config.SpeciesCellTypeWeight, Config.SpeciesTopologyWeight,
                Config.SpeciesParameterWeight, Config.SpeciesBrainWeight);

            if (distance > Config.SpeciesDriftThreshold)
            {
                FoundSpecies(creature);
            }
            else
            {
                creature.SpeciesId = parent.SpeciesId;
            }
        }

        private void FoundSpecies(Organism creature)
        {
            uint id = _nextSpeciesId++;
            creature.SpeciesId = id;
            _species[id] = new SpeciesFounder(creature.Genome, ElapsedSeconds);
        }

        /// <summary>Creatures that have died, oldest first. Cleared by <see cref="TakeDead"/>.</summary>
        public IReadOnlyList<Organism> Dead => _dead;

        /// <summary>Hands over the dead and forgets them, so a long run does not grow without bound.</summary>
        public List<Organism> TakeDead()
        {
            var taken = new List<Organism>(_dead);
            _dead.Clear();
            return taken;
        }
    }

    /// <summary>The living against the reserve cap and the fat in them — <see cref="World.CensusReserveCap"/>, D247.</summary>
    public sealed class ReserveCapCensus
    {
        /// <summary>Living bodies whose reserve is within 1% of their cap.</summary>
        public int NearCap;

        /// <summary>Lump breeders whose margin gene's seconds exceed the cap's s, under the body-and-store cap.</summary>
        public int MarginAboveCap;

        /// <summary>Living bodies with any fat tissue.</summary>
        public int FatCarriers;

        /// <summary>Fat tissue's volume over the living, m³.</summary>
        public double FatVolume;

        /// <summary>All tissue's volume over the living, m³.</summary>
        public double Volume;

        /// <summary>Fat's share of the living tissue volume; 0 with no tissue.</summary>
        public double FatVolumeShare => Volume > 0d ? FatVolume / Volume : 0d;
    }
}
