using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// The traits of every grazer a run ever bore, one row a genome
    /// (<c>--grazer-traits &lt;run dir&gt; &lt;out tsv&gt;</c>): the read of G2's evolution gate
    /// (<c>logbook/specs/bloom-build/g2-read-spec.md</c>) that no record file holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read-only, and steps no world.</b> It reads the run's <c>config.json</c>,
    /// <c>lineage.jsonl</c> and <c>genomes.jsonl.gz</c> (a run that resumed another is read as one
    /// world with it, the way <c>scripts/reads/r59-read.py</c> reads it) and writes one TSV, which
    /// may not lie inside the run directory.
    /// </para>
    /// <para>
    /// <b>A grazer</b> is a body whose birth row has <c>atk</c> 1 and <c>ink</c> 1: born mouthed,
    /// the definition <c>scripts/reads/r61-grazer-economy.py</c> reads by (<see cref="IsGrazerBirth"/>).
    /// </para>
    /// <para>
    /// <b>The body is the world's own.</b> Every body is built by <see cref="Developer.Develop"/>
    /// with the run's development limits, shape registry and cell table, the call
    /// <c>World.AdmitFounder</c> and <c>World.Conceive</c> make, and nothing here re-derives a
    /// geometry. Counts, mass and the limb push come off that <see cref="Phenotype"/>.
    /// </para>
    /// <para>
    /// <b>The drives run the creature's own brain with no physics.</b> <see cref="Brain.For"/>
    /// builds it from the developed body and <see cref="Brain.Step"/> takes a sense field, so a
    /// brain steps open-loop at the world's physics step for 10 s with a fixed field
    /// (<see cref="Senses"/>). The spec's fallback (the mean over joint-driving neurons of
    /// <c>Amplitude</c> × (frequency &gt; 0) + |<c>Bias</c>|) is not needed and not built; the TSV's
    /// header line says which was used.
    /// </para>
    /// </remarks>
    public static class GrazerTraits
    {
        public const string Usage = "Usage: Evosim.Farm --grazer-traits <run dir> <out tsv>";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>How long a brain runs open-loop for each drive, world seconds.</summary>
        public const float DriveSeconds = 10f;

        /// <summary>The leaf-smell strength the fed state reads, on every part's port.</summary>
        public const float FedLeafSmell = 0.8f;

        /// <summary>The energy sense the fed state reads, on the root's port.</summary>
        public const float FedEnergy = 0.5f;

        /// <summary>
        /// The columns, in order. <c>adult_scale</c> is the genome's <see cref="Genome.AdultScale"/>,
        /// which the lineage row also carries as <c>as</c>. It is written beside the traits because
        /// <c>mass_kg</c> already has it folded in (see <see cref="MassKilograms"/>).
        /// </summary>
        public static readonly string[] Columns =
        {
            "id", "born_t", "parent", "src", "parts", "jointed", "mass_kg", "limb_push", "drive_rest", "drive_fed",
            "adult_scale",
        };

        /// <summary>One grazer's row.</summary>
        public sealed class Row
        {
            public long Id;
            public double BornT;
            public long Parent;
            public string Src;
            public int Parts;
            public int Jointed;
            public double MassKg;
            public double LimbPush;
            public double DriveRest;
            public double DriveFed;
            public float AdultScale;
        }

        /// <summary>A sense field that reads one number on every part's leaf-smell port and one on the energy port.</summary>
        public sealed class Senses : ISensorField
        {
            private readonly float _leaf;
            private readonly float _energy;

            public Senses(float leaf, float energy)
            {
                _leaf = leaf;
                _energy = energy;
            }

            /// <summary>Every sense 0.</summary>
            public static Senses AtRest => new Senses(0f, 0f);

            /// <summary>The leaf smell at <see cref="FedLeafSmell"/>, the energy at <see cref="FedEnergy"/>, nothing else.</summary>
            public static Senses Fed => new Senses(FedLeafSmell, FedEnergy);

            public float Read(int partIndex, SensorChannel channel, int index)
            {
                if (channel == SensorChannel.LeafScent) return _leaf;
                if (channel == SensorChannel.Energy) return _energy;
                return 0f;
            }
        }

        public static int Run(string[] args)
        {
            if (args == null || args.Length != 3 || args[0] != "--grazer-traits")
            {
                Console.Error.WriteLine(Usage);
                return 1;
            }

            try
            {
                Measure(args[1], args[2], Console.Out);
                return 0;
            }
            catch (Exception e) when (
                e is ArgumentException || e is InvalidOperationException || e is IOException ||
                e is InvalidDataException || e is FormatException)
            {
                Console.Error.WriteLine("grazer-traits: " + e.Message);
                return 1;
            }
        }

        /// <summary>True for a birth row of a body born mouthed: <c>"e":"b"</c> with <c>atk</c> 1 and <c>ink</c> 1.</summary>
        public static bool IsGrazerBirth(JsonNode row)
        {
            if (row == null || !row.Has("e") || row["e"].AsString() != "b") return false;
            return row.Has("atk") && row["atk"].AsDouble() == 1d && row.Has("ink") && row["ink"].AsDouble() == 1d;
        }

        /// <summary>
        /// The mass of the body in kilograms: <see cref="RunConfig.PartDensityKilogramsPerCubicMetre"/>
        /// times the sum of the parts' volumes, the mass the harness gives the articulation body
        /// (World.IsUnderTheMassFloor's own expression).
        /// </summary>
        /// <remarks>
        /// <b>This is the adult body, and the genome's adult scale is already in it.</b>
        /// <see cref="Developer.Develop"/> starts the accumulated scale at <see cref="Genome.AdultScale"/>
        /// and folds it into every half-extent, so the sum of volumes is already the adult scale cubed
        /// times the unit-scale body's. The newborn is this body scaled down to its birth fraction
        /// (<c>Organism.AdultPhenotype</c> is this body; <c>NewbornFrom</c> scales it), and its mass is
        /// not the one wanted. Multiplying this by <c>adultScale</c>³ again would count the scale twice.
        /// </remarks>
        public static double MassKilograms(Phenotype body)
        {
            // Developer.Develop starts its accumulated scale at genome.AdultScale and folds it into every half-extent, so these
            // volumes already carry adultScale cubed. The mass is NOT multiplied by adultScale^3 again.
            double volume = 0d;
            for (int i = 0; i < body.PartCount; i++) volume += body.Parts[i].Volume;
            return RunConfig.PartDensityKilogramsPerCubicMetre * volume;
        }

        /// <summary>The parts with a joint that has a degree of freedom, the root excluded (its joint is forced fixed).</summary>
        public static int JointedParts(Phenotype body)
        {
            int jointed = 0;
            for (int i = 0; i < body.PartCount; i++)
            {
                if (body.Parts[i].JointType.DofCount() > 0) jointed++;
            }

            return jointed;
        }

        /// <summary>The sum of the source node's <see cref="MorphNode.Power"/> gene over the developed body's jointed parts.</summary>
        public static double LimbPush(Genome genome, Phenotype body)
        {
            double push = 0d;
            for (int i = 0; i < body.PartCount; i++)
            {
                PhenotypePart part = body.Parts[i];
                if (part.JointType.DofCount() > 0) push += genome.Nodes[part.SourceNode].Power;
            }

            return push;
        }

        /// <summary>
        /// The brain's mean drive: |output| averaged over every output that drives a joint, over
        /// <paramref name="seconds"/> of steps of <paramref name="dt"/>, with the sense field fixed.
        /// </summary>
        /// <param name="bendAndPower">
        /// The world runs the bend-and-power drive (<c>Fluid.PowerPushRadiansPerSecond</c> above 0),
        /// where each degree of freedom's bend output drives its joint beside its power output. Off,
        /// only the power outputs drive.
        /// </param>
        /// <returns>0 for a body whose brain has no joint output.</returns>
        public static double MeanDrive(Phenotype body, bool bendAndPower, float dt, float seconds, ISensorField senses)
        {
            Brain brain = Brain.For(body);
            int dof = brain.TotalDof;
            if (dof == 0) return 0d;

            var drive = new float[dof];
            var bend = bendAndPower ? new float[dof] : null;
            int steps = (int)Math.Round(seconds / dt);
            double sum = 0d;

            for (int s = 0; s < steps; s++)
            {
                brain.Step(dt, drive, senses, bend);

                for (int k = 0; k < dof; k++)
                {
                    sum += Math.Abs(drive[k]);
                    if (bend != null) sum += Math.Abs(bend[k]);
                }
            }

            return steps == 0 ? 0d : sum / ((double)steps * dof * (bend != null ? 2 : 1));
        }

        /// <summary>One genome's row.</summary>
        public static Row Describe(
            RunConfig config, float dt, long id, double bornT, long parent, string src, Genome genome)
        {
            Phenotype body = Developer.Develop(
                genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes);

            bool bendAndPower = config.Fluid.PowerPushRadiansPerSecond > 0f;

            return new Row
            {
                Id = id,
                BornT = bornT,
                Parent = parent,
                Src = src,
                Parts = body.PartCount,
                Jointed = JointedParts(body),
                MassKg = MassKilograms(body),
                LimbPush = LimbPush(genome, body),
                DriveRest = MeanDrive(body, bendAndPower, dt, DriveSeconds, Senses.AtRest),
                DriveFed = MeanDrive(body, bendAndPower, dt, DriveSeconds, Senses.Fed),
                AdultScale = genome.AdultScale,
            };
        }

        private sealed class Segment
        {
            public string Dir;
            public double Lo = double.NegativeInfinity;
            public double Hi = double.PositiveInfinity;
        }

        /// <summary>
        /// The run and the runs it resumed, oldest first, each with the span of seconds it owns
        /// (<c>scripts/reads/r59-read.py</c>'s chain: the run's rows after the resume second, the
        /// earlier run's up to it).
        /// </summary>
        private static List<Segment> Chain(string runDirectory, List<string> notes)
        {
            var segments = new List<Segment> { new Segment { Dir = runDirectory } };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(runDirectory) };
            string current = runDirectory;

            while (true)
            {
                string manifest = Path.Combine(current, "run.json");
                if (!File.Exists(manifest)) break;

                JsonNode root = Json.Parse(File.ReadAllText(manifest));
                if (!root.Has("resumedFrom") || root["resumedFrom"].Kind != JsonNode.NodeKind.Object) break;

                JsonNode from = root["resumedFrom"];
                double at = from["seconds"].AsDouble();
                segments[0].Lo = at;

                string trimmed = current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string arms = Path.GetDirectoryName(Path.GetDirectoryName(trimmed));
                string previous = arms == null
                    ? null
                    : Path.GetFullPath(Path.Combine(arms, from["arm"].AsString(), from["run"].AsString()));

                if (previous == null || !File.Exists(Path.Combine(previous, "config.json")) || !seen.Add(previous))
                {
                    notes.Add(
                        Path.GetFileName(trimmed) + " resumed from " + from["arm"].AsString() + "/" +
                        from["run"].AsString() + " at " + at.ToString("0.###", Inv) +
                        " s, which is not found; its earlier grazers are not in this table");
                    break;
                }

                segments.Insert(0, new Segment { Dir = previous, Hi = at });
                current = previous;
            }

            return segments;
        }

        /// <summary>
        /// Writes the TSV for <paramref name="runPath"/> and returns the rows written.
        /// </summary>
        /// <param name="select">
        /// Which birth rows are grazers. Null is <see cref="IsGrazerBirth"/>; a test hands in a
        /// wider rule, because a short world founds nothing mouthed.
        /// </param>
        public static int Measure(string runPath, string outFile, TextWriter log, Func<JsonNode, bool> select = null)
        {
            select = select ?? IsGrazerBirth;

            string runDirectory = CensusRead.ResolveRunDirectory(runPath, Usage);
            string outPath = Path.GetFullPath(outFile);
            CensusRead.RefuseTheRunDirectory(runDirectory, outPath);

            // A config this build's loader refuses is said as the loader says it, and the read stops.
            RunConfig config = RunDirectory.ReadConfig(runDirectory, out string edited);
            if (edited != null) log.WriteLine("grazer-traits: warning: config.json " + edited);

            float dt = PhysicsStep(runDirectory, config);
            var notes = new List<string>();
            List<Segment> chain = Chain(runDirectory, notes);

            // The grazers, in the order the lineage bore them, each with the segment its genome is read from.
            var wanted = new List<(long id, double t, long parent, string src, int segment)>();
            var ids = new HashSet<long>();

            for (int s = 0; s < chain.Count; s++)
            {
                Segment seg = chain[s];
                string lineage = Path.Combine(seg.Dir, "lineage.jsonl");
                if (!File.Exists(lineage)) throw new FileNotFoundException("No lineage.jsonl in " + seg.Dir + ".", lineage);

                foreach (string line in JsonlWriter.ReadRows(lineage))
                {
                    if (line.IndexOf("\"e\":\"b\"", StringComparison.Ordinal) < 0) continue;

                    JsonNode row = Json.Parse(line);
                    double t = row["t"].AsDouble();
                    if (t <= seg.Lo || t > seg.Hi) continue;
                    if (!select(row)) continue;

                    long id = (long)row["id"].AsDouble();
                    if (!ids.Add(id)) continue;

                    wanted.Add((
                        id, t,
                        row.Has("p") ? (long)row["p"].AsDouble() : -1L,
                        row.Has("src") ? row["src"].AsString() : "-",
                        s));
                }
            }

            // Each id's genome row from the newest segment that has one, oldest first on a tie: the same
            // genome either way, since a resumed run writes its roster again beside its own births.
            var genomes = new Dictionary<long, string>();
            var remaining = new HashSet<long>(ids);

            for (int s = chain.Count - 1; s >= 0 && remaining.Count > 0; s--)
            {
                Dictionary<long, string> found = RecordFiles.GenomeRows(chain[s].Dir, remaining, notes);
                foreach (KeyValuePair<long, string> pair in found)
                {
                    genomes[pair.Key] = pair.Value;
                    remaining.Remove(pair.Key);
                }
            }

            var rows = new List<Row>(wanted.Count);
            int undeveloped = 0;

            foreach (var w in wanted)
            {
                if (!genomes.TryGetValue(w.id, out string text)) continue;

                try
                {
                    rows.Add(Describe(config, dt, w.id, w.t, w.parent, w.src, GenomeJson.Read(text)));
                }
                catch (ArgumentException)
                {
                    undeveloped++;
                }
            }

            var lines = new List<string>
            {
                "# grazer-traits: run " + runDirectory + ", config hash " + config.Hash(),
                "# drive: open-loop brain. Brain.For(Developer.Develop(genome)) stepped with no physics at dt " +
                    dt.ToString("0.#####", Inv) + " s for " + DriveSeconds.ToString("0.#", Inv) +
                    " s, |output| averaged over every output that drives a joint" +
                    (config.Fluid.PowerPushRadiansPerSecond > 0f
                        ? " (power and bend: the world runs the bend-and-power drive)"
                        : " (power only: the world runs the torque drive)") +
                    "; drive_rest reads every sense 0, drive_fed reads the leaf smell " +
                    FedLeafSmell.ToString("0.0#", Inv) + ", the energy " + FedEnergy.ToString("0.0#", Inv) +
                    " and every other sense 0. The spec's fallback was not used.",
                "# mass_kg: " + RunConfig.PartDensityKilogramsPerCubicMetre.ToString("0.#", Inv) +
                    " kg/m3 times the sum of part volumes of Developer.Develop(genome), the adult body; the genome's " +
                    "adult scale is folded into the volumes already, and adult_scale is written beside it.",
                "# grazers in the lineage: " + wanted.Count + "; genomes found: " + rows.Count + "; no genome row: " +
                    (wanted.Count - rows.Count - undeveloped) + "; not developable: " + undeveloped,
            };

            foreach (string note in notes) lines.Add("# note: " + note);
            lines.Add(string.Join("\t", Columns));

            foreach (Row r in rows)
            {
                lines.Add(string.Join("\t",
                    r.Id.ToString(Inv), r.BornT.ToString("R", Inv), r.Parent.ToString(Inv), r.Src,
                    r.Parts.ToString(Inv), r.Jointed.ToString(Inv), r.MassKg.ToString("R", Inv),
                    r.LimbPush.ToString("R", Inv), r.DriveRest.ToString("R", Inv), r.DriveFed.ToString("R", Inv),
                    r.AdultScale.ToString("R", Inv)));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllLines(outPath, lines, new UTF8Encoding(false));
            log.WriteLine("grazer-traits: " + rows.Count + " grazers written to " + outPath);
            return rows.Count;
        }

        /// <summary>
        /// The world's physics step: the manifest's <c>physicsDtSeconds</c> where the run has one,
        /// else the config's, resolved the way a launch resolves it.
        /// </summary>
        private static float PhysicsStep(string runDirectory, RunConfig config)
        {
            string manifest = Path.Combine(runDirectory, "run.json");

            if (File.Exists(manifest))
            {
                JsonNode root = Json.Parse(File.ReadAllText(manifest));
                if (root.Has("physicsDtSeconds") && root["physicsDtSeconds"].Kind == JsonNode.NodeKind.Number)
                {
                    float recorded = (float)root["physicsDtSeconds"].AsDouble();
                    if (recorded > 0f && !float.IsInfinity(recorded)) return recorded;
                }
            }

            return EnvBinding.ResolvePhysicsStep(config.PhysicsStepSeconds, config.PaceFactor, out _);
        }
    }
}
