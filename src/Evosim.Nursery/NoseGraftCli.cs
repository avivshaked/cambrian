using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// D235's graft film, its two files: a world config whose cell-type table appends the nose at its defaults, and a pool whose every genome
    /// carries a grafted nose (<see cref="NoseGraft"/>). <c>--graft-nose &lt;pool.tsv&gt; --config &lt;config.json&gt; --out &lt;dir&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is swum.</b> The tool reads the config and the pool, grafts each genome on its root (by default where the first graft stood, on
    /// the top face toward the front, pointing up; <c>--anchor</c>, <c>--axis</c>, <c>--area</c> and <c>--slenderness</c> name another; <c>--mirror x|y|z</c> makes it a mirrored pair, which needs the
    /// config's <c>development.reflectMirroredAnchors</c>, and <c>--pair-links first|split</c> says where the pair's smell links go), and writes three files into <c>--out</c>: the config, named by its hash as
    /// <c>rounds/nursery/configs/</c> names its files; the pool, named by the first 16 hex digits of its SHA-256 as <c>rounds/nursery/pools/</c>
    /// names its files, each body's index kept; and <c>graft.tsv</c>, one row a genome saying what the graft moved. A genome the graft refuses
    /// stops the tool with nothing written, so a pool is grafted whole or not at all.
    /// </para>
    /// <para>
    /// <b>The config adds no field.</b> The nose's settings are a cell type's, which the table already writes, so a build that reads today's
    /// configs reads this one; only its table differs from the config it came from.
    /// </para>
    /// </remarks>
    public static class NoseGraftCli
    {
        public const string Usage = "usage: Evosim.Nursery --graft-nose <pool.tsv> --config <config.json> --out <dir> " +
            "[--anchor x,y,z] [--axis x,y,z] [--area m2] [--slenderness s] [--mirror x|y|z] [--pair-links first|split]";

        /// <summary>Where the nose stands on the root, in its normalised box coordinates, and the way it points.</summary>
        public static readonly Float3 Anchor = new Float3(0f, 1f, -0.7f), Axis = new Float3(0f, 1f, 0f);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int Cli(string[] args) => Run(args, Console.Out, Console.Error);

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            string pool = null, config = null, outDir = null, anchorArg = null, axisArg = null, areaArg = null, slenderArg = null,
                mirrorArg = null, pairArg = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                switch (args[i])
                {
                    case "--graft-nose": if (pool != null) goto twice; pool = args[++i]; break;
                    case "--config": if (config != null) goto twice; config = args[++i]; break;
                    case "--out": if (outDir != null) goto twice; outDir = args[++i]; break;
                    case "--anchor": if (anchorArg != null) goto twice; anchorArg = args[++i]; break;
                    case "--axis": if (axisArg != null) goto twice; axisArg = args[++i]; break;
                    case "--area": if (areaArg != null) goto twice; areaArg = args[++i]; break;
                    case "--slenderness": if (slenderArg != null) goto twice; slenderArg = args[++i]; break;
                    case "--mirror": if (mirrorArg != null) goto twice; mirrorArg = args[++i]; break;
                    case "--pair-links": if (pairArg != null) goto twice; pairArg = args[++i]; break;
                    default: stderr.WriteLine("unknown " + args[i] + "\n" + Usage); return 2;
                }

                continue;
                twice:
                stderr.WriteLine(args[i] + " given twice\n" + Usage);
                return 2;
            }

            if (pool == null || config == null || outDir == null) { stderr.WriteLine(Usage); return 2; }

            string configText, configOut, poolText, poolName;
            var report = new StringBuilder("line\tbody\tnoseArea\tlinksMoved\tlinksMade\tdirectionLinksReframed\n");
            try
            {
                // Unnamed, each is what the first graft (entry 23) used: the top face toward the front, up, the reference area, 3 to 1.
                Float3 anchor = anchorArg == null ? Anchor : Vector(anchorArg, "--anchor");
                Float3 axis = axisArg == null ? Axis : Vector(axisArg, "--axis");
                float area = areaArg == null ? 0f : float.Parse(areaArg, NumberStyles.Float, Inv);
                float slenderness = slenderArg == null ? 0f : float.Parse(slenderArg, NumberStyles.Float, Inv);
                int mirror = mirrorArg == null ? -1 : mirrorArg == "x" ? 0 : mirrorArg == "y" ? 1 : mirrorArg == "z" ? 2
                    : throw new FormatException($"--mirror takes x, y or z, not {mirrorArg}");
                NosePairLinks pairLinks = pairArg == null || pairArg == "first" ? NosePairLinks.First
                    : pairArg == "split" ? NosePairLinks.Split
                    : throw new FormatException($"--pair-links takes first or split, not {pairArg}");
                if (pairArg != null && mirror < 0) throw new FormatException("--pair-links needs --mirror");

                RunConfig world = RunConfigJson.Read(File.ReadAllText(config));
                if (world.CellTypes.Contains(CellTypeIds.Nose)) throw new InvalidDataException($"{config}'s table already lists the nose");
                world.CellTypes = world.CellTypes.Appending(new NoseCell());
                configText = RunConfigJson.Write(world);
                configOut = world.Hash();
                string reread = RunConfigJson.Read(configText).Hash();
                if (reread != configOut) throw new InvalidDataException($"the nose's config reads back as {reread}, not {configOut}");

                var lines = File.ReadAllLines(pool).Where(l => l.Length > 0).ToList();
                if (lines.Count == 0) throw new InvalidDataException($"{pool} holds no genome");
                var text = new StringBuilder();
                for (int n = 0; n < lines.Count; n++)
                {
                    int tab = lines[n].IndexOf('\t');
                    if (tab < 1) throw new InvalidDataException($"{pool} line {n + 1} is not a body, a tab and a genome");
                    string body = lines[n].Substring(0, tab);
                    Genome genome = GenomeJson.Read(lines[n].Substring(tab + 1));
                    NoseGraftResult graft;
                    try
                    {
                        graft = NoseGraft.Graft(genome, world.CellTypes, genome.RootIndex, anchor, axis, area, world.Development, world.Shapes, slenderness,
                            mirror, pairLinks);
                    }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                    {
                        throw new InvalidDataException($"{pool} line {n + 1}: the graft refused: {e.Message}");
                    }

                    text.Append(body).Append('\t').Append(GenomeJson.Write(graft.Genome)).Append('\n');
                    report.Append(string.Join("\t", (n + 1).ToString(Inv), body, graft.NoseArea.ToString("R", Inv),
                        graft.LinksMoved.ToString(Inv), graft.LinksMade.ToString(Inv), graft.DirectionLinksReframed.ToString(Inv))).Append('\n');
                }

                poolText = text.ToString();
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] digest = sha.ComputeHash(new UTF8Encoding(false).GetBytes(poolText));
                    poolName = string.Concat(digest.Take(8).Select(b => b.ToString("x2", Inv)));
                }
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                stderr.WriteLine("--graft-nose: " + e.Message);
                return 2;
            }

            Directory.CreateDirectory(outDir);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(outDir, configOut + ".json"), configText, utf8);
            File.WriteAllText(Path.Combine(outDir, poolName + ".tsv"), poolText, utf8);
            File.WriteAllText(Path.Combine(outDir, "graft.tsv"), report.ToString(), utf8);
            stdout.WriteLine($"config {configOut}.json: {config} with the nose appended to its table");
            stdout.WriteLine($"pool {poolName}.tsv: {pool}, every genome grafted" +
                (mirrorArg == null ? "" : $" with a pair mirrored in {mirrorArg}, links {pairArg ?? "first"}"));
            stdout.Write(report.ToString());
            return 0;
        }

        private static Float3 Vector(string text, string option)
        {
            string[] parts = text.Split(',');
            if (parts.Length != 3) throw new FormatException($"{option} takes x,y,z, not {text}");
            return new Float3(
                float.Parse(parts[0], NumberStyles.Float, Inv), float.Parse(parts[1], NumberStyles.Float, Inv), float.Parse(parts[2], NumberStyles.Float, Inv));
        }
    }
}
