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
    ///     [--shade 0] [--compare]
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
            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            var sb = new StringBuilder();
            AppendBodySummary(sb, "Body", options.GenomePath, body, config);

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
                    AppendBodySummary(sb, "Body (leaf <-> stomach swapped)", options.GenomePath, swappedBody, config);
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
                        options.Depths, options.Densities, options.Shade);
                }
            }

            config.CellTypes = baseRegistry;

            Console.Out.Write(sb.ToString());
            return 0;
        }

        // ------------------------------------------------------------------ body summary

        private static void AppendBodySummary(
            StringBuilder sb, string heading, string genomePath, Phenotype body, RunConfig config)
        {
            float tissue = Metabolism.TissueJoules(body, config);
            float standingWatts = Metabolism.StandingWatts(body, config);

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
            sb.Append("- Tissue: ").Append(Format(tissue)).Append(" J\n");
            sb.Append("- Standing cost: ").Append(Format(standingWatts)).Append(" W\n");
            sb.Append("- Truncated: ").Append(body.WasTruncated).Append('\n');
            sb.Append("- Cell types:");
            foreach (var kv in byType.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.Append(' ').Append(kv.Key).Append(" x").Append(kv.Value.Count)
                  .Append(" (").Append(Format(kv.Value.Volume)).Append(" m3)");
            }
            sb.Append("\n\n");
        }

        // ------------------------------------------------------------------ forecast table

        private static void AppendForecastTable(
            StringBuilder sb, Phenotype body, RunConfig config, ReproductionTraits reproduction,
            IReadOnlyList<float> depths, IReadOnlyList<float> densities, float shade)
        {
            sb.Append("| depth (m) | density (J/m3) | net W at birth | break-even (J/m3) | ")
              .Append("lifetime (s) | R0 | first child (s) | matter/child |\n");
            sb.Append("|---|---|---|---|---|---|---|---|\n");

            foreach (float depth in depths)
            {
                float heightY = -depth;
                float irradiance = config.Light.IrradianceAt(heightY);

                foreach (float density in densities)
                {
                    LedgerForecastResult result = LedgerForecast.Forecast(
                        body, config, irradiance, density, shade, reproduction);

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
                      .Append('|').Append(Format(result.MatterPricePerChild))
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
            public bool Compare;

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
                    Compare = compare,
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

        private sealed class LedgerCliException : Exception
        {
            public LedgerCliException(string message) : base(message) { }
        }
    }
}
