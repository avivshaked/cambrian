using System;

namespace Evosim.Core
{
    /// <summary>
    /// One row of <c>absorptive.jsonl</c>: a creature that eats, where it was, what the water
    /// there held, and what its last metabolic step actually earned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Built for the question logbook/0050's dissection could not answer.</b> The invasion
    /// assay's stomachs were forecast to breed at R0 2–6 and did not, and nothing a run wrote down
    /// said where they were, what density they saw, or why two children died within four minutes
    /// of birth on a 101 J endowment. <c>lineage.jsonl</c> has ids, times and parentage and no
    /// physiology; <c>snapshots/</c> have genome graphs and no ids. This file is the join: the
    /// same id <c>lineage.jsonl</c> uses, carrying the ledger terms and the local larder.
    /// </para>
    /// <para>
    /// <b>A value type queued and copied, never a reference into the population.</b> A death row
    /// has to survive the creature it describes — <see cref="World"/> buffers it at the moment of
    /// death and the harness writes it at the next report row — and a reference would either keep
    /// a dead body alive or read fields the death path had already zeroed.
    /// </para>
    /// <para>
    /// <b>Pure instrumentation.</b> Nothing in the economy reads one, exactly as
    /// <see cref="LineageEvent"/> and <see cref="Organism.SpeciesId"/> are read nowhere but a
    /// report. A world whose log is never collected and one collected every step take identical
    /// trajectories.
    /// </para>
    /// </remarks>
    public readonly struct AbsorptiveSample
    {
        /// <summary>World time this row describes, s.</summary>
        public double ElapsedSeconds { get; }

        /// <summary>The creature's lineage id — the same id <c>lineage.jsonl</c> keys on.</summary>
        public long Id { get; }

        public float Age { get; }
        public int GenerationDepth { get; }

        /// <summary>D061's horizontal cell. 0 whenever <see cref="RunConfig.HorizontalPatches"/> is 1.</summary>
        public int Patch { get; }

        /// <summary>Height, m. Y is up, so this is negative below the surface.</summary>
        public float HeightY { get; }

        /// <summary>Whole-body volume, m³.</summary>
        public float Volume { get; }

        /// <summary>Volume of <see cref="CellTypeIds.Absorptive"/> tissue alone, m³.</summary>
        public float AbsorptiveVolume { get; }

        /// <summary>Lit area, m² — <see cref="Phenotype.TotalLitArea"/>.</summary>
        public float LitArea { get; }

        public int PartCount { get; }

        /// <summary>Also carries photosynthetic tissue, so it is not a pure stomach.</summary>
        public bool Mixotroph { get; }

        /// <summary>Reserve, J. Death is at zero, so a death row's is at or below it.</summary>
        public float Energy { get; }

        /// <summary>Embodied energy, J — <see cref="Organism.TissueJoules"/>.</summary>
        public float TissueJoules { get; }

        /// <summary>What this genome would give a child, J — <see cref="ReproductionTraits.OffspringEndowment"/>.</summary>
        public float Endowment { get; }

        /// <summary>
        /// The nutrient density this creature was actually fed at, J/m³ — <b>the number
        /// <c>World.Metabolise</c> handed <see cref="Metabolism.StepAt"/></b>, not the field's own
        /// reading.
        /// </summary>
        /// <remarks>
        /// Two things separate it from <see cref="NutrientField.DensityAt(float, int)"/> and both
        /// matter: a refuge layer is only <see cref="NutrientField.RefugeEdibleFraction"/> edible
        /// (D055), and a crowded layer is rationed to <see cref="Share"/> of what it holds. With
        /// no refuge and an unrationed larder — the default, and every world before D055 — this is
        /// exactly <c>DensityAt(y, patch)</c>. <b>It is already share-multiplied</b>, so a reader
        /// must not multiply by <see cref="Share"/> again.
        /// </remarks>
        public float DensityHere { get; }

        /// <summary>The demand-share the field granted this creature's layer, 0–1.</summary>
        public float Share { get; }

        /// <summary>Food income over the last step, W.</summary>
        public float FoodWatts { get; }

        /// <summary>Light income over the last step, W.</summary>
        public float LightWatts { get; }

        /// <summary>
        /// Metabolism over the last step, W — <see cref="EnergyLedger.Expenditure"/>, which is
        /// upkeep <i>plus</i> neural <i>plus</i> work.
        /// </summary>
        /// <remarks>
        /// <b>The whole expenditure under the name <c>upkeep</c>, deliberately.</b> The instrument
        /// exists so a reader sees a budget without summing windows, and a budget only closes if
        /// its terms are exhaustive: <c>light + food − upkeep − exuded = net</c> holds exactly as
        /// written here, and would not if this were <see cref="EnergyLedger.Upkeep"/> alone with
        /// neural and work missing. For a stomach with no joints the two are the same number
        /// anyway.
        /// </remarks>
        public float UpkeepWatts { get; }

        /// <summary>D070's release to the water over the last step, W. 0 with exudation off.</summary>
        public float ExudedWatts { get; }

        /// <summary>What the body kept over the last step, W — <see cref="EnergyLedger.Net"/> / step.</summary>
        public float NetWatts { get; }

        /// <summary>Children born to this creature so far.</summary>
        public int Children { get; }

        /// <summary>When the last one was born, s. NaN when there has been none.</summary>
        public double LastChildSeconds { get; }

        /// <summary>This is the creature's final row — it died on this step.</summary>
        public bool Dead { get; }

        public AbsorptiveSample(
            double elapsedSeconds, long id, float age, int generationDepth, int patch,
            float heightY, float volume, float absorptiveVolume, float litArea, int partCount,
            bool mixotroph, float energy, float tissueJoules, float endowment,
            float densityHere, float share,
            float foodWatts, float lightWatts, float upkeepWatts, float exudedWatts, float netWatts,
            int children, double lastChildSeconds, bool dead)
        {
            ElapsedSeconds = elapsedSeconds;
            Id = id;
            Age = age;
            GenerationDepth = generationDepth;
            Patch = patch;
            HeightY = heightY;
            Volume = volume;
            AbsorptiveVolume = absorptiveVolume;
            LitArea = litArea;
            PartCount = partCount;
            Mixotroph = mixotroph;
            Energy = energy;
            TissueJoules = tissueJoules;
            Endowment = endowment;
            DensityHere = densityHere;
            Share = share;
            FoodWatts = foodWatts;
            LightWatts = lightWatts;
            UpkeepWatts = upkeepWatts;
            ExudedWatts = exudedWatts;
            NetWatts = netWatts;
            Children = children;
            LastChildSeconds = lastChildSeconds;
            Dead = dead;
        }

        /// <summary>
        /// The row for one creature as of its last metabolic step.
        /// </summary>
        /// <remarks>
        /// Reads <see cref="Organism"/>'s own cached capture rather than recomputing anything: the
        /// density, the share and the ledger were all in hand inside <c>World.Metabolise</c>, and
        /// a second evaluation of <see cref="Metabolism.StepAt"/> here would be both a cost on the
        /// population loop and a second expression of a quantity the world already computed —
        /// which is how two figures obliged to agree come apart.
        /// </remarks>
        /// <param name="creature">A creature with absorptive tissue. Others are not logged.</param>
        /// <param name="elapsedSeconds">World time this row is stamped with.</param>
        /// <param name="dead">True for the one final row a death writes.</param>
        internal static AbsorptiveSample For(Organism creature, double elapsedSeconds, bool dead)
        {
            // Zero would be a plausible watt figure, so a creature the metabolic loop has not
            // reached yet — inoculated or spawned after this step's Metabolise — reports its
            // ledger terms as zero and its step as zero, and the division is guarded rather than
            // producing an infinity JSON cannot represent.
            float step = creature.LastStepSeconds;
            float perSecond = step > 0f ? 1f / step : 0f;
            EnergyLedger ledger = creature.LastLedger;

            return new AbsorptiveSample(
                elapsedSeconds,
                creature.Id,
                creature.Age,
                creature.GenerationDepth,
                creature.Patch,
                creature.HeightY,
                creature.Phenotype.TotalVolume,
                creature.AbsorptiveVolume,
                creature.Phenotype.TotalLitArea,
                creature.Phenotype.PartCount,
                creature.HasPhotosyntheticTissue,
                creature.Energy,
                creature.TissueJoules,
                creature.Genome.Reproduction.OffspringEndowment,
                creature.LastDensityHere,
                creature.LastShare,
                ledger.FoodIncome * perSecond,
                ledger.LightIncome * perSecond,
                ledger.Expenditure * perSecond,
                ledger.Exuded * perSecond,
                ledger.Net * perSecond,
                creature.Children,
                creature.LastChildSeconds,
                dead);
        }

        /// <summary>
        /// One line of <c>absorptive.jsonl</c>. Compact — one row must be one line (§9), and no
        /// genome: <see cref="JsonlWriter"/> refuses an embedded line break outright.
        /// </summary>
        public string ToJson()
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject()
                .Field("t", ElapsedSeconds)
                .Field("id", Id)
                .Field("age", Age)
                .Field("gen", GenerationDepth)
                .Field("patch", Patch)
                .Field("y", HeightY)
                .Field("volume", Volume)
                .Field("absVolume", AbsorptiveVolume)
                .Field("photoArea", LitArea)
                .Field("parts", PartCount)
                .Field("mixotroph", Mixotroph)
                .Field("energy", Energy)
                .Field("tissue", TissueJoules)
                .Field("endowment", Endowment)
                .Field("densityHere", DensityHere)
                .Field("share", Share)
                .Field("foodW", FoodWatts)
                .Field("lightW", LightWatts)
                .Field("upkeepW", UpkeepWatts)
                .Field("exudedW", ExudedWatts)
                .Field("netW", NetWatts)
                .Field("children", Children);

            // null, not a sentinel: 0 is a real birth time (the world's first step) and -1 is a
            // number a plotting script will happily average in. JSON has a way to say "there was
            // no such event" and this uses it.
            if (double.IsNaN(LastChildSeconds)) w.Field("lastChildT", (string)null);
            else w.Field("lastChildT", LastChildSeconds);

            w.Field("dead", Dead);
            w.EndObject();

            return w.ToString();
        }

        /// <summary>
        /// The row that says how many creatures this sample left out — the cap in
        /// <see cref="World.AbsorptiveLogRowCap"/>.
        /// </summary>
        /// <remarks>
        /// Written rather than left implicit because the alternative is a file that silently
        /// stops describing the population it claims to describe. A reader that averages
        /// <c>netW</c> over a bloom needs to know it is averaging the two thousand oldest
        /// stomachs and not the population.
        /// </remarks>
        public static string TruncatedRowJson(double elapsedSeconds, int truncated)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject()
                .Field("t", elapsedSeconds)
                .Field("truncated", truncated)
                .EndObject();

            return w.ToString();
        }

        public override string ToString() =>
            $"#{Id} t={ElapsedSeconds:0.#} y={HeightY:0.#} m, {DensityHere:0.###} J/m3, " +
            $"net {NetWatts:0.####} W{(Dead ? ", dead" : "")}";
    }
}
