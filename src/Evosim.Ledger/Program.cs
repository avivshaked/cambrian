using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;

namespace Evosim.Ledger
{
    /// <summary>
    /// CLI over <see cref="LedgerForecast"/>: what one stored genome's energy ledger does in
    /// isolation, at a chosen depth, nutrient density and absorptive clearance — DESIGN.md
    /// §5A.2, §5A.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Usage</b>
    /// <code>
    /// dotnet run --project src/Evosim.Ledger -- ^
    ///     --genome path\to\genome.json --config path\to\run\config.json ^
    ///     --clearance 1,5,10 --depth 0,5,10,15,20 --density 0.5,1,2,4,7,10,15 ^
    ///     [--shade 0] [--exposure 1] [--compare]
    /// </code>
    /// Or, from PowerShell: <c>scripts/ledger.ps1 -Genome ... -Config ... -Clearance 1,5,10 ...</c>
    /// (see that script's own header for its exact switches).
    /// </para>
    /// <para>
    /// <b>Loading refuses rather than defaults, per §9.</b> <c>--genome</c> and <c>--config</c>
    /// are required and must resolve to real files; <c>--clearance</c>, <c>--depth</c> and
    /// <c>--density</c> are required lists — there is no sensible default sweep for an
    /// experiment-shaped question like this one, and guessing one would produce a table that
    /// looks like an answer to a question nobody asked. Only <c>--shade</c> has a default (0,
    /// unshaded), because 0 is not a guess — it is the well-defined "nothing between this body
    /// and the light" case.
    /// </para>
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (LedgerCliException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            Options options = Options.Parse(args);
            _exposure = options.Exposure;

            string genomeText = ReadFirstLine(options.GenomePath, "genome");
            Genome genome = GenomeJson.Read(genomeText);

            string configText = ReadWholeFile(options.ConfigPath, "config");
            RunConfig config = RunConfigJson.Read(configText, out string hashMismatch);
            if (hashMismatch != null)
            {
                Console.Error.WriteLine($"warning: {hashMismatch}");
            }

            // Development does not consult config.CellTypes at all (Developer.Develop validates
            // against CellTypeRegistry.Standard and never resolves a type until Metabolism does),
            // so one phenotype per genome variant is enough — clearance is swept afterwards by
            // swapping config.CellTypes, not by re-developing anything.
            // D106 item 3's four overrides, applied to the genome before it is developed: the
            // attributes ride on the node and the developer carries them onto the part, so a
            // screen of "what would this leaf cost with a cuticle" is a screen of a different
            // genome and not of a different reading of the same one. Refused rather than clamped
            // when the value is over the cell type's cap, which is Genome.Validate's rule and the
            // rule a run would hold the same genome to.
            int overridden = ApplyAttributeOverrides(genome, config.CellTypes, options);

            if (overridden > 0)
            {
                Console.Error.WriteLine(
                    $"note: {overridden} node(s) had an attribute overridden from the command line; " +
                    "the body below is not the stored genome's.");
            }

            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            // D098's leg 1 needs a spent density, and the honest default is the water this config
            // seeds. The live volume is a property of a built world — the tank's mask cuts the
            // corners off the array — and this tool has no world, so a held budget is reported as
            // what it is and the density rule's own number is used when there is no budget. Which
            // one was used is printed rather than inferred, because the two can differ by a fifth.
            float spentDensity;
            string spentSource;

            if (options.Spent.HasValue)
            {
                spentDensity = options.Spent.Value;
                spentSource = "--spent";
            }
            else
            {
                spentDensity = config.InitialMatterPerCubicMetre;
                spentSource = config.MatterBudgetUnits > 0f
                    ? "initialMatterPerCubicMetre (the config holds a budget of " +
                      Format(config.MatterBudgetUnits) +
                      " units, whose density needs a built world's live volume)"
                    : "initialMatterPerCubicMetre";
            }


            var sb = new StringBuilder();
            sb.Append("Spent density: ").Append(Format(spentDensity))
              .Append(" units/m3, from ").Append(spentSource).Append("\n\n");

            AppendBodySummary(sb, "Body", options.GenomePath, body, config, spentDensity);

            AppendModuleTable(sb, genome, body, config, spentDensity);

            AppendMouthTable(sb, body, config, spentDensity);

            var variants = new List<(string Label, Genome Genome, Phenotype Body)>
            {
                ("as stored", genome, body),
            };

            if (options.Compare)
            {
                Genome swapped = SwapAbsorptiveAndPhotosynthetic(genome, out int swappedCount);
                if (swappedCount == 0)
                {
                    Console.Error.WriteLine(
                        "warning: --compare asked for a leaf/stomach swap, but this genome has " +
                        "no absorptive or photosynthetic nodes to swap.");
                }
                else
                {
                    Phenotype swappedBody = Developer.Develop(swapped, config.Development, null, config.Shapes);
                    AppendBodySummary(
                        sb, "Body (leaf <-> stomach swapped)", options.GenomePath, swappedBody,
                        config, spentDensity);
                    variants.Add(($"swapped ({swappedCount} node(s))", swapped, swappedBody));
                }
            }

            CellTypeRegistry baseRegistry = config.CellTypes;
            if (!baseRegistry.Contains(CellTypeIds.Absorptive))
            {
                throw new LedgerCliException(
                    $"The config at '{options.ConfigPath}' has no '{CellTypeIds.Absorptive}' cell " +
                    "type registered, so a clearance sweep has nothing to vary.");
            }

            AbsorptiveCell baseAbsorptive = (AbsorptiveCell)baseRegistry.Resolve(CellTypeIds.Absorptive);

            foreach (float clearance in options.Clearances)
            {
                CellTypeRegistry swept = WithAbsorptiveClearance(baseRegistry, baseAbsorptive, clearance);
                config.CellTypes = swept;

                foreach (var (label, variantGenome, variantBody) in variants)
                {
                    sb.Append("\n### Clearance = ").Append(Format(clearance))
                      .Append(" (").Append(label).Append(")\n\n");

                    AppendForecastTable(
                        sb, variantBody, config, variantGenome.Reproduction,
                        options.Depths, options.Densities, options.Shade, spentDensity);
                }
            }

            config.CellTypes = baseRegistry;

            Console.Out.Write(sb.ToString());
            return 0;
        }

        // ------------------------------------------------------------------ exposure

        // D110's --exposure, one factor for every part. Held here rather than threaded through
        // every table, since it is one number for the whole invocation.
        private static float _exposure = 1f;

        /// <summary>
        /// The exposure array for one body — the factor repeated a part — or null at 1, which is
        /// the orientation average and the path every screen before D110 took.
        /// </summary>
        /// <remarks>
        /// An array rather than a multiplier on the answer, so that the factor reaches the bill
        /// through Core's own per-part path (<c>Metabolism.StepAt</c>'s exposure overload) and the
        /// uptake ceiling, which is per square metre too, sees it as the world would.
        /// </remarks>
        private static float[] Exposed(Phenotype body)
        {
            if (_exposure == 1f) return null;

            var exposure = new float[body.PartCount];
            for (int i = 0; i < exposure.Length; i++) exposure[i] = _exposure;
            return exposure;
        }

        // ------------------------------------------------------------------ body summary

        private static void AppendBodySummary(
            StringBuilder sb, string heading, string genomePath, Phenotype body, RunConfig config,
            float spentDensity)
        {
            double tissue = Metabolism.TissueJoules(body, config);
            float standingWatts = Metabolism.StandingWatts(body, config);

            // D098. What this body fixes at the surface in the water the run seeds, in the unit
            // the economy is denominated in — the number the spec's table is read against.
            float surfaceIrradiance = config.Light.IrradianceAt(0f);
            float fixationWatts = Metabolism.StepAt(
                body, config, surfaceIrradiance, nutrientDensity: 0f, spentDensity: spentDensity,
                workJoules: 0f, seconds: 1f, ageSeconds: 0f, exposure: Exposed(body)).LightIncome;

            var byType = new Dictionary<string, (int Count, float Volume)>(StringComparer.Ordinal);
            foreach (PhenotypePart part in body.Parts)
            {
                byType.TryGetValue(part.CellTypeId, out var entry);
                byType[part.CellTypeId] = (entry.Count + 1, entry.Volume + part.Volume);
            }

            sb.Append("## ").Append(heading).Append(" — ").Append(genomePath).Append('\n').Append('\n');
            sb.Append("- Parts: ").Append(body.PartCount).Append('\n');
            sb.Append("- Volume: ").Append(Format(body.TotalVolume)).Append(" m3\n");
            sb.Append("- Lit area: ").Append(Format(body.TotalLitArea)).Append(" m2\n");

            // D099. Three numbers rather than one, because the difference between them is the
            // whole of what the cap does: the parts added up, the shape they actually block the
            // light with, and what this config bills. The third is what "Fixation at surface"
            // above was computed on, since Metabolism.StepAt reads the same flag.
            sb.Append("- Silhouette: ").Append(Format(body.SilhouetteArea)).Append(" m2")
              .Append(body.SilhouetteFellBackToBox > 0 ? " (hull degenerate, box used)" : "")
              .Append('\n');
            sb.Append("- Lit area (capped): ")
              .Append(Format(body.EffectiveLitArea(config.LightSilhouetteCap))).Append(" m2 (cap ")
              .Append(config.LightSilhouetteCap ? "on" : "off").Append(")\n");
            if (_exposure != 1f)
            {
                sb.Append("- Exposure: every part at ").Append(Format(_exposure))
                  .Append(" (D110; 1 is the orientation average, 2 a thin sheet lying flat), lit area ")
                  .Append(Format(body.ExposedLitArea(Exposed(body)))).Append(" m2 before the cap\n");
            }
            sb.Append("- Tissue: ").Append(Format((float)tissue)).Append(" J\n");
            sb.Append("- Standing cost: ").Append(Format(standingWatts)).Append(" W (")
              .Append(Format(standingWatts / config.JoulesPerUnit)).Append(" units/s)\n");

            // D113, beside the standing cost it is a share of. Printed at every price, the reach
            // included, because the reach a body has with the price off is what a screen of the
            // price is read against; the watts say "off" rather than 0 there.
            double reachArea = 0d, reachWeighted = 0d, farthest = 0d;
            foreach (PhenotypePart part in body.Parts)
            {
                reachArea += part.LitArea;
                reachWeighted += (double)part.LitArea * part.DistanceFromRoot;
                if (part.DistanceFromRoot > farthest) farthest = part.DistanceFromRoot;
            }

            float supportPrice = config.SupportWattsPerSquareMetrePerSquareMetre;
            double supportWatts = Metabolism.SupportWatts(body, config);

            sb.Append("- support W: ")
              .Append(supportPrice > 0f
                  ? Format((float)supportWatts) + " at " + Format(supportPrice) + " W/m2/m2, " +
                    Format(100f * (float)supportWatts / Math.Max(1e-9f, standingWatts)) +
                    "% of the standing cost"
                  : "off")
              .Append("; reach ")
              .Append(Format(reachArea > 0d ? (float)(reachWeighted / reachArea) : 0f))
              .Append(" m area-weighted, farthest part ").Append(Format((float)farthest))
              .Append(" m\n");
            sb.Append("- Fixation at surface: ").Append(Format(fixationWatts)).Append(" W (")
              .Append(Format(fixationWatts / config.JoulesPerUnit)).Append(" units/s)\n");

            // D106 item 3's rule 7, beside the income it has to be read against. Printed only
            // when it is something: at the recorded world's prices — all four at zero — the term
            // is not computed at all, and a line reading "0 W" on every screen ever taken would
            // train a reader to skip the one that does not.
            float attributeWatts = 0f;
            foreach (PhenotypePart part in body.Parts)
            {
                attributeWatts += Metabolism.AttributeWatts(part, config);
            }

            if (attributeWatts > 0f)
            {
                sb.Append("- Attribute upkeep: ").Append(Format(attributeWatts)).Append(" W, ")
                  .Append(Format(100f * attributeWatts / Math.Max(1e-9f, standingWatts)))
                  .Append("% of the standing cost and ")
                  .Append(Format(100f * attributeWatts / Math.Max(1e-9f, fixationWatts)))
                  .Append("% of fixation at the surface\n");
            }
            sb.Append("- Truncated: ").Append(body.WasTruncated).Append('\n');
            sb.Append("- Cell types:");
            foreach (var kv in byType.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.Append(' ').Append(kv.Key).Append(" x").Append(kv.Value.Count)
                  .Append(" (").Append(Format(kv.Value.Volume)).Append(" m3)");
            }
            sb.Append("\n\n");
        }

        // ------------------------------------------------------------------ the module gene

        /// <summary>
        /// What one more module of each indeterminate node costs, and how long it takes to repay
        /// itself — D106 item 2, and the screen
        /// <c>logbook/specs/module-gene-spec.md</c>'s tunables section asks for before a round.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The whole question in one column.</b> A module is bought at the tissue price out of
        /// the reserve and earns whatever the extra parts earn, so <c>module repay s</c> is the
        /// tissue it costs over the net watts it adds, at this config's surface light and the
        /// spent density the run seeds. A module that cannot repay itself within a lifetime is a
        /// rule no plant will ever use, and <c>ModuleAddReserveSeconds</c> chosen without this is
        /// a day of machine time spent finding it out.
        /// </para>
        /// <para>
        /// <b>Nothing new is computed.</b> The candidate body is the same development at one
        /// higher count, and the two readings are <c>Metabolism.TissueJoules</c> and the ledger's
        /// own step at the surface — the two numbers the body summary above already prints. The
        /// silhouette cap is in both, because <c>Metabolism.StepAt</c> reads the config's flag,
        /// which is the whole reason a spread body earns more than a packed one.
        /// </para>
        /// <para>
        /// A determinate genome prints one line saying so rather than an empty table: an empty
        /// table reads like a measurement that came back zero.
        /// </para>
        /// </remarks>
        private static void AppendModuleTable(
            StringBuilder sb, Genome genome, Phenotype body, RunConfig config, float spentDensity)
        {
            sb.Append("## Modules — the add rule's price (D106 item 2)\n\n");

            var indeterminate = new List<int>();
            for (int n = 0; n < genome.Nodes.Count; n++)
            {
                if (genome.Nodes[n].Growth == ModuleGrowth.Indeterminate) indeterminate.Add(n);
            }

            if (indeterminate.Count == 0)
            {
                sb.Append("Every node is determinate, so the module rule has nothing to add here ")
                  .Append("and `module repay s` is not a number this genome has.\n\n");
                return;
            }

            float surfaceIrradiance = config.Light.IrradianceAt(0f);
            double tissue = Metabolism.TissueJoules(body, config);
            double net = NetWattsAtSurface(body, config, surfaceIrradiance, spentDensity);

            var counts = new int[genome.Nodes.Count];
            for (int n = 0; n < counts.Length; n++) counts[n] = genome.Nodes[n].RecursiveLimit;

            sb.Append("| node | cell | count | max | parts | tissue +J | net +W | module repay s |\n");
            sb.Append("|---|---|---|---|---|---|---|---|\n");

            foreach (int n in indeterminate)
            {
                MorphNode node = genome.Nodes[n];
                int ceiling = node.MaxModules > node.RecursiveLimit
                    ? node.MaxModules
                    : node.RecursiveLimit;

                var next = (int[])counts.Clone();
                next[n]++;

                Phenotype grown = Developer.Develop(
                    genome, config.Development, null, config.Shapes, next);

                double tissueAdded = Metabolism.TissueJoules(grown, config) - tissue;
                double netAdded =
                    NetWattsAtSurface(grown, config, surfaceIrradiance, spentDensity) - net;

                string repay = netAdded > 0d && tissueAdded > 0d
                    ? Format((float)(tissueAdded / netAdded))
                    : "never";

                sb.Append('|').Append(n)
                  .Append('|').Append(node.CellTypeId)
                  .Append('|').Append(node.RecursiveLimit)
                  .Append('|').Append(ceiling)
                  .Append('|').Append(body.PartCount).Append(" -> ").Append(grown.PartCount)
                  .Append('|').Append(Format((float)tissueAdded))
                  .Append('|').Append(Format((float)netAdded))
                  .Append('|').Append(repay)
                  .Append("|\n");
            }

            sb.Append("\nA module at the ceiling adds no parts and reads 0 J and 0 W; ")
              .Append("`never` is a module whose parts earn no more than they cost to stand.\n\n");
        }

        /// <summary>
        /// A body's net watts at the surface in the water this config seeds, unfed — the ledger's
        /// own step, at age zero.
        /// </summary>
        private static double NetWattsAtSurface(
            Phenotype body, RunConfig config, float surfaceIrradiance, float spentDensity) =>
            Metabolism.StepAt(
                body, config, surfaceIrradiance, nutrientDensity: 0f, spentDensity: spentDensity,
                workJoules: 0f, seconds: 1f, ageSeconds: 0f, exposure: Exposed(body)).Net;

        // ------------------------------------------------------------------ the mouth

        /// <summary>
        /// The four readings <c>logbook/specs/mouth-spec.md</c>'s screen asks of a genome before
        /// round 45 — D106 items 1, 3 and 4.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Every number is taken against one reference part of this body: its largest.</b> An
        /// attack is a rate over a part's own area and a health pool is a part's own volume, so
        /// none of the four questions has an answer until a part is named — and the largest is the
        /// one a lineage would build a claw or a shell on, and the one a bite is most likely to
        /// find. The part's area and volume are printed beside the readings so that a reader can
        /// redo any of them.
        /// </para>
        /// <para>
        /// <b>The step is the metabolic one and not the physics one.</b> Damage is applied once
        /// per metabolic step (D106 item 4: no physics), so "steps to kill" counts half-seconds
        /// and not hundredths, whatever <c>EVOSIM_DT</c> says.
        /// </para>
        /// <para>
        /// <b>The caps come from this config's registry</b> and not from the built-in table, so a
        /// launcher that amended the table is screened on the table it will run.
        /// </para>
        /// </remarks>
        private static void AppendMouthTable(
            StringBuilder sb, Phenotype body, RunConfig config, float spentDensity)
        {
            sb.Append("## Mouth — the four attributes' prices (D106 item 3)\n\n");

            if (body.PartCount == 0)
            {
                sb.Append("This genome develops to no parts, so there is no area to price an ")
                  .Append("attribute over.\n\n");
                return;
            }

            PhenotypePart reference = body.Parts[0];
            foreach (PhenotypePart part in body.Parts)
            {
                if (part.Volume > reference.Volume) reference = part;
            }

            float area = Math.Max(0f, reference.SurfaceArea);
            float volume = Math.Max(0f, reference.Volume);

            float attackMax = CapOf(config, CellTypeIds.Structural, c => c.AttackMax);
            float cuticleMax = CapOf(config, CellTypeIds.Photosynthetic, c => c.ProtectionMax);
            float mouthMax = CapOf(config, CellTypeIds.Consumer, c => c.IntakeMax);

            float surfaceIrradiance = config.Light.IrradianceAt(0f);
            float fixationWatts = Metabolism.StepAt(
                body, config, surfaceIrradiance, nutrientDensity: 0f, spentDensity: spentDensity,
                workJoules: 0f, seconds: 1f, ageSeconds: 0f, exposure: Exposed(body)).LightIncome;

            // The lifetime the world already uses for one: senescence's doubling scale, which is
            // how long a body lasts before upkeep alone finishes it (D038). A world without
            // senescence has no lifetime of its own, and the ledger's own cap stands in.
            float lifetime = config.SenescenceDoublingSeconds > 0f
                ? config.SenescenceDoublingSeconds
                : LedgerForecast.MaxLifetimeSeconds;

            double tissue = Metabolism.TissueJoules(body, config);

            float clawWatts = config.AttackWattsPerUnit * attackMax * area;
            float cuticleWatts = config.ProtectionWattsPerUnit * cuticleMax * area;
            float mouthWatts = config.IntakeWattsPerUnit * mouthMax * area;
            float toughWatts = config.ToughnessWattsPerUnit *
                               Math.Max(0f, CapOf(config, CellTypeIds.Structural, c => c.ToughnessMax) - 1f) *
                               volume;

            const float MetabolicStepSeconds = 0.5f;

            float pool = volume * config.HealthPerCubicMetre;
            float blow = attackMax * area;
            float bare = blow * MetabolicStepSeconds;
            float armoured = Math.Max(0f, blow - cuticleMax * area) * MetabolicStepSeconds;

            double kept = tissue * (1d - config.IntakeWasteFraction);
            double emptySeconds = mouthMax * area > 0f
                ? (tissue / config.JoulesPerUnit) / (mouthMax * area)
                : double.PositiveInfinity;

            sb.Append("Reference part: the body's largest — ").Append(Format(area))
              .Append(" m2, ").Append(Format(volume)).Append(" m3, health pool ")
              .Append(Format(pool)).Append(" at toughness 1.\n\n");

            sb.Append("| reading | value |\n|---|---|\n");

            sb.Append("| claw at the structural cap (").Append(Format(attackMax)).Append(") |")
              .Append(Format(clawWatts)).Append(" W, ").Append(Format(clawWatts * lifetime))
              .Append(" J over a ").Append(Format(lifetime)).Append(" s lifetime |\n");

            sb.Append("| this body as a corpse |").Append(Format((float)tissue))
              .Append(" J, ").Append(Format((float)kept)).Append(" J kept at waste ")
              .Append(Format(config.IntakeWasteFraction)).Append(" |\n");

            sb.Append("| claw repaid by one such corpse |")
              .Append(clawWatts > 0f ? Format((float)(kept / clawWatts)) + " s of claw" : "free")
              .Append(" |\n");

            sb.Append("| cuticle at the leaf cap (").Append(Format(cuticleMax)).Append(") |")
              .Append(Format(cuticleWatts)).Append(" W, ")
              .Append(Format(fixationWatts > 0f ? 100f * cuticleWatts / fixationWatts : 0f))
              .Append("% of this body's fixation at the surface |\n");

            sb.Append("| mouth at the consumer cap (").Append(Format(mouthMax)).Append(") |")
              .Append(Format(mouthWatts)).Append(" W |\n");

            sb.Append("| shell at the structural toughness cap |").Append(Format(toughWatts))
              .Append(" W |\n");

            sb.Append("| steps to kill this part, unprotected |")
              .Append(bare > 0f ? Format((float)Math.Ceiling(pool / bare)) : "never")
              .Append(" metabolic steps (").Append(Format(MetabolicStepSeconds)).Append(" s each) |\n");

            sb.Append("| steps to kill it behind a capped cuticle |")
              .Append(armoured > 0f ? Format((float)Math.Ceiling(pool / armoured)) : "never")
              .Append(" |\n");

            sb.Append("| a capped mouth emptying a corpse of this body |")
              .Append(double.IsInfinity(emptySeconds) ? "never" : Format((float)emptySeconds) + " s")
              .Append(", against ")
              .Append(config.CorpseDecayPerSecond > 0f
                  ? Format(1f / config.CorpseDecayPerSecond) + " s of decay"
                  : "no corpse at all (decay 0)")
              .Append(" |\n\n");
        }

        /// <summary>One cell type's cap, from this config's registry — 0 where it is not registered.</summary>
        private static float CapOf(RunConfig config, string cellTypeId, Func<CellType, float> of) =>
            config.CellTypes.Contains(cellTypeId) ? of(config.CellTypes.Resolve(cellTypeId)) : 0f;

        /// <summary>
        /// Applies <c>--attack</c>, <c>--intake</c>, <c>--protection</c> and <c>--toughness</c> to
        /// a genome's nodes. Returns how many nodes were changed.
        /// </summary>
        /// <remarks>
        /// <b>A bare value reaches every node; <c>type=value</c> reaches every node of that cell
        /// type.</b> The second is what the screen actually wants — "what does a cuticle on the
        /// leaves cost" — and the first is what a one-cell body wants, which is most of the
        /// genomes a screen is run on. Over a cap it throws rather than clamping, because a
        /// clamped screen would report a price for an organ the world would refuse.
        /// </remarks>
        private static int ApplyAttributeOverrides(
            Genome genome, CellTypeRegistry cellTypes, Options options)
        {
            var touched = new HashSet<int>();

            Apply(options.Attack, "attack", (n, v) => n.Attack = v, c => c.AttackMax);
            Apply(options.Intake, "intake", (n, v) => n.Intake = v, c => c.IntakeMax);
            Apply(options.Protection, "protection", (n, v) => n.Protection = v, c => c.ProtectionMax);
            Apply(options.Toughness, "toughness", (n, v) => n.Toughness = v, c => c.ToughnessMax);

            return touched.Count;

            void Apply(
                AttributeOverride? given, string name, Action<MorphNode, float> set,
                Func<CellType, float> cap)
            {
                if (!given.HasValue) return;

                AttributeOverride over = given.Value;

                if (over.CellTypeId != null && !cellTypes.Contains(over.CellTypeId))
                {
                    throw new LedgerCliException(
                        $"--{name} names cell type '{over.CellTypeId}', which this config does " +
                        "not register: " + string.Join(", ", cellTypes.Ids()) + ".");
                }

                for (int n = 0; n < genome.Nodes.Count; n++)
                {
                    MorphNode node = genome.Nodes[n];
                    if (over.CellTypeId != null && node.CellTypeId != over.CellTypeId) continue;
                    if (!cellTypes.Contains(node.CellTypeId)) continue;

                    float ceiling = cap(cellTypes.Resolve(node.CellTypeId));

                    if (over.Value > ceiling)
                    {
                        throw new LedgerCliException(
                            $"--{name} {Format(over.Value)} is over the cap of {Format(ceiling)} " +
                            $"for a '{node.CellTypeId}' cell (node {n}). A genome above a cap is " +
                            "refused by the world, so a screen above one would price an organ " +
                            "nothing could carry.");
                    }

                    set(node, over.Value);
                    touched.Add(n);
                }
            }
        }

        // ------------------------------------------------------------------ forecast table

        private static void AppendForecastTable(
            StringBuilder sb, Phenotype body, RunConfig config, ReproductionTraits reproduction,
            IReadOnlyList<float> depths, IReadOnlyList<float> densities, float shade,
            float spentDensity)
        {
            sb.Append("| depth (m) | density (J/m3) | net W at birth | break-even (J/m3) | ")
              .Append("lifetime (s) | R0 | first child (s) | units/child |\n");
            sb.Append("|---|---|---|---|---|---|---|---|\n");

            foreach (float depth in depths)
            {
                float heightY = -depth;
                float irradiance = config.Light.IrradianceAt(heightY);

                foreach (float density in densities)
                {
                    LedgerForecastResult result = LedgerForecast.Forecast(
                        body, config, irradiance, density, spentDensity, shade, reproduction,
                        Exposed(body));

                    sb.Append('|').Append(Format(depth))
                      .Append('|').Append(Format(density))
                      .Append('|').Append(Format(result.NetWattsAtBirth))
                      .Append('|').Append(result.BreakEvenNutrientDensity.HasValue
                          ? Format(result.BreakEvenNutrientDensity.Value) : "none")
                      .Append('|').Append(Format(result.LifetimeSeconds))
                          .Append(result.DiedOfStarvation ? "" : "*")
                      .Append('|').Append(result.ChildrenProduced)
                      .Append('|').Append(result.TimeToFirstChildSeconds.HasValue
                          ? Format(result.TimeToFirstChildSeconds.Value) : "never")
                      .Append('|').Append(Format(result.UnitsPerChild))
                      .Append("|\n");
                }
            }

            sb.Append("\n*lifetime marked with an asterisk was censored at the ")
              .Append(Format(LedgerForecast.MaxLifetimeSeconds))
              .Append(" s cap rather than ending in starvation.\n\n");
        }

        // ------------------------------------------------------------------ genome variants

        /// <summary>
        /// A cloned genome with every absorptive node made photosynthetic and every
        /// photosynthetic node made absorptive — <c>--compare</c>'s "leaf and a stomach of the
        /// same shape" side by side.
        /// </summary>
        private static Genome SwapAbsorptiveAndPhotosynthetic(Genome genome, out int swappedCount)
        {
            Genome clone = genome.Clone();
            swappedCount = 0;

            foreach (MorphNode node in clone.Nodes)
            {
                if (node.CellTypeId == CellTypeIds.Absorptive)
                {
                    node.CellTypeId = CellTypeIds.Photosynthetic;
                    swappedCount++;
                }
                else if (node.CellTypeId == CellTypeIds.Photosynthetic)
                {
                    node.CellTypeId = CellTypeIds.Absorptive;
                    swappedCount++;
                }
            }

            return clone;
        }

        /// <summary>
        /// The config's registry with the absorptive cell's clearance replaced and everything
        /// else about it — upkeep, yield, tissue energy — carried over unchanged. Every other
        /// cell type instance is reused as-is: only the one knob this tool sweeps is new.
        /// </summary>
        private static CellTypeRegistry WithAbsorptiveClearance(
            CellTypeRegistry baseRegistry, AbsorptiveCell baseAbsorptive, float clearance)
        {
            var types = new List<CellType>(baseRegistry.Count);
            foreach (string id in baseRegistry.Ids())
            {
                if (id == CellTypeIds.Absorptive)
                {
                    var replacement = new AbsorptiveCell(
                        clearance, baseAbsorptive.UpkeepWattsPerCubicMetre, baseAbsorptive.Yield)
                    {
                        TissueEnergyPerCubicMetre = baseAbsorptive.TissueEnergyPerCubicMetre,
                    };
                    types.Add(replacement);
                }
                else
                {
                    types.Add(baseRegistry.Resolve(id));
                }
            }

            return new CellTypeRegistry(types.ToArray());
        }

        // ------------------------------------------------------------------ io

        private static string ReadFirstLine(string path, string what)
        {
            if (!File.Exists(path))
            {
                throw new LedgerCliException($"No {what} file at '{path}'.");
            }

            foreach (string line in File.ReadLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line)) return line;
            }

            throw new LedgerCliException($"The {what} file at '{path}' has no content.");
        }

        private static string ReadWholeFile(string path, string what)
        {
            if (!File.Exists(path))
            {
                throw new LedgerCliException($"No {what} file at '{path}'.");
            }

            return File.ReadAllText(path);
        }

        private static string Format(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ options

        private sealed class Options
        {
            public string GenomePath;
            public string ConfigPath;
            public float[] Clearances;
            public float[] Depths;
            public float[] Densities;
            public float Shade;

            /// <summary>
            /// D110's exposure factor, every part alike. 1 by default, the orientation average;
            /// 2 is a thin sheet lying flat and 0 one standing on edge.
            /// </summary>
            public float Exposure = 1f;

            /// <summary>
            /// Spent matter dissolved in the water, units/m3 — D098's leg 1. Null until
            /// <see cref="Program"/> fills it from the config, since the default is a reading of
            /// the world the config describes rather than a constant.
            /// </summary>
            public float? Spent;

            public bool Compare;

            /// <summary>
            /// D106 item 3's four, as <c>--attack 0.5</c> or <c>--attack structural=0.5</c>. Null
            /// is "leave the genome's own value alone", which is not the same as zero.
            /// </summary>
            public AttributeOverride? Attack;

            /// <summary>See <see cref="Attack"/>.</summary>
            public AttributeOverride? Intake;

            /// <summary>See <see cref="Attack"/>.</summary>
            public AttributeOverride? Protection;

            /// <summary>See <see cref="Attack"/>.</summary>
            public AttributeOverride? Toughness;

            public static Options Parse(string[] args)
            {
                var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool compare = false;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (!arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new LedgerCliException($"Unexpected argument '{arg}'.");
                    }

                    string key = arg.Substring(2);
                    if (string.Equals(key, "compare", StringComparison.OrdinalIgnoreCase))
                    {
                        compare = true;
                        continue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        throw new LedgerCliException($"--{key} needs a value.");
                    }

                    raw[key] = args[++i];
                }

                var options = new Options
                {
                    GenomePath = Require(raw, "genome"),
                    ConfigPath = Require(raw, "config"),
                    Clearances = RequireFloatList(raw, "clearance"),
                    Depths = RequireFloatList(raw, "depth"),
                    Densities = RequireFloatList(raw, "density"),
                    Shade = raw.TryGetValue("shade", out string shadeText) ? ParseFloat("shade", shadeText) : 0f,
                    Spent = raw.TryGetValue("spent", out string spentText)
                        ? ParseFloat("spent", spentText)
                        : (float?)null,
                    Compare = compare,
                    Exposure = raw.TryGetValue("exposure", out string exposureText)
                        ? ParseFloat("exposure", exposureText)
                        : 1f,
                    Attack = ParseOverride(raw, "attack"),
                    Intake = ParseOverride(raw, "intake"),
                    Protection = ParseOverride(raw, "protection"),
                    Toughness = ParseOverride(raw, "toughness"),
                };

                foreach (float depth in options.Depths)
                {
                    if (depth < 0f)
                    {
                        throw new LedgerCliException(
                            $"--depth values must be non-negative metres below the surface; got {depth}.");
                    }
                }

                foreach (float density in options.Densities)
                {
                    if (density < 0f)
                    {
                        throw new LedgerCliException($"--density values must be non-negative; got {density}.");
                    }
                }

                foreach (float clearance in options.Clearances)
                {
                    if (clearance <= 0f)
                    {
                        throw new LedgerCliException($"--clearance values must be positive; got {clearance}.");
                    }
                }

                if (options.Shade < 0f || options.Shade > 1f)
                {
                    throw new LedgerCliException($"--shade must be in [0, 1]; got {options.Shade}.");
                }

                if (!(options.Exposure >= 0f) || float.IsInfinity(options.Exposure))
                {
                    throw new LedgerCliException(
                        $"--exposure must be a finite non-negative factor; got {options.Exposure}.");
                }

                if (options.Spent.HasValue && options.Spent.Value < 0f)
                {
                    throw new LedgerCliException(
                        $"--spent must be non-negative units/m3; got {options.Spent.Value}.");
                }

                return options;
            }

            private static string Require(Dictionary<string, string> raw, string key)
            {
                if (!raw.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new LedgerCliException($"--{key} is required.");
                }
                return value;
            }

            private static float[] RequireFloatList(Dictionary<string, string> raw, string key)
            {
                string text = Require(raw, key);
                string[] parts = text.Split(',');
                var values = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    values[i] = ParseFloat(key, parts[i]);
                }
                return values;
            }

            /// <summary>
            /// <c>--attack 0.5</c> or <c>--attack structural=0.5</c>, or nothing at all.
            /// </summary>
            /// <remarks>
            /// A negative value is refused here rather than at the cap test, because the floors
            /// are 0 for three of the four and 1 for toughness and neither is a number a screen
            /// has any reason to go under: what would be asked is a body the world refuses.
            /// </remarks>
            private static AttributeOverride? ParseOverride(
                Dictionary<string, string> raw, string key)
            {
                if (!raw.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                string cellTypeId = null;
                int split = text.IndexOf('=');

                if (split >= 0)
                {
                    cellTypeId = text.Substring(0, split).Trim();
                    text = text.Substring(split + 1);

                    if (cellTypeId.Length == 0)
                    {
                        throw new LedgerCliException(
                            $"--{key} '{raw[key]}' names an empty cell type. Write " +
                            $"--{key} <value> for every node, or --{key} <cellType>=<value>.");
                    }
                }

                float value = ParseFloat(key, text);

                if (value < 0f)
                {
                    throw new LedgerCliException($"--{key} must be non-negative; got {value}.");
                }

                return new AttributeOverride(cellTypeId, value);
            }

            private static float ParseFloat(string key, string text)
            {
                if (!float.TryParse(
                        text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    throw new LedgerCliException($"--{key} value '{text}' is not a number.");
                }
                return value;
            }
        }

        /// <summary>One of D106's four attributes, set from the command line.</summary>
        /// <remarks>
        /// <see cref="CellTypeId"/> null is "every node", which is what a one-cell body wants;
        /// a named type is what the screen wants, since "a cuticle on the leaves" is a statement
        /// about the leaves and not about the whole animal.
        /// </remarks>
        private readonly struct AttributeOverride
        {
            public AttributeOverride(string cellTypeId, float value)
            {
                CellTypeId = cellTypeId;
                Value = value;
            }

            public readonly string CellTypeId;
            public readonly float Value;
        }

        private sealed class LedgerCliException : Exception
        {
            public LedgerCliException(string message) : base(message) { }
        }
    }
}
