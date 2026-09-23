using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Creature genomes to and from JSON — DESIGN.md §9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every field is written and every field is required on load.</b> No defaults are
    /// applied for anything absent. A genome that loads with one field silently defaulted is a
    /// different creature wearing the original's identity: it develops into a different body, is
    /// measured, and the numbers are filed against the stored genome with nothing downstream
    /// able to notice. <c>RoundTripsEveryRandomGenome</c> in the tests is the check that this
    /// stays true as the format grows — it compares the re-serialized text rather than a
    /// hand-written list of fields, so a field added to <see cref="MorphNode"/> and forgotten
    /// here fails immediately.
    /// </para>
    /// <para>
    /// Enums are written as <b>names</b>, not numbers, for the same reason
    /// <see cref="CellType.Id"/> is a string: genomes outlive code, and inserting a member into
    /// an enum renumbers everything after it, so a stored ordinal silently comes to mean
    /// something else. A name that no longer resolves fails loudly instead.
    /// </para>
    /// <para>
    /// <b>Compact by default.</b> A genome is one row of <c>lineage.jsonl</c>, so it must occupy
    /// exactly one line; pass <c>indent: true</c> when writing one out for a person to read.
    /// </para>
    /// </remarks>
    public static class GenomeJson
    {
        /// <summary>
        /// Bumped whenever this format changes in a way that stops old files loading.
        /// </summary>
        /// <remarks>
        /// Stored in every genome so that a file which cannot be read says why, rather than
        /// failing on a missing field twelve levels down. It is not a compatibility mechanism —
        /// there is no migration code — it is a diagnostic.
        /// </remarks>
        /// <remarks>
        /// 2 — D049 added <see cref="MorphNode.Lift"/>. A format-1 genome has no <c>lift</c> key,
        /// and §9's rule is that loading refuses rather than defaults: a genome that loads with
        /// one field silently zeroed is a different creature wearing the original's identity.
        /// </remarks>
        /// <remarks>
        /// 4 — the theatre's join (D075 item 2): a snapshot row may carry the organism's
        /// <c>id</c>, the same integer <c>lineage.jsonl</c> uses, as its first field. The genome
        /// itself is unchanged, so a format-3 row would in fact parse — and it is refused anyway,
        /// because the id is exactly what a reader of a snapshot now expects to be able to join
        /// on, and "the file loaded but every creature in it is anonymous" is the silent-default
        /// failure §9 exists to prevent, one level up.
        /// </remarks>
        /// <remarks>
        /// 5 — fable-propose-growth.md (2026-09-08). <c>endowment</c> is retired and
        /// <c>investment</c> and <c>adultScale</c> take its place. This one genuinely cannot be
        /// read across: a format-4 genome's endowment is joules and the field that replaced it is
        /// a fraction of a body, so the same number in the same place means something else
        /// entirely, and it carries no size at all.
        /// </remarks>
        /// <remarks>
        /// 6 — D098 §3 (2026-09-18). The breeding margin joins the reproduction object. A
        /// format-5 genome carries no margin at all, and defaulting the missing field to zero
        /// would hand every stored creature the one strategy the gene exists to let a lineage
        /// move off — a genome wearing another's identity, which is what §9 refuses.
        /// </remarks>
        /// <remarks>
        /// 7 — D106 items 2 and 6 (2026-09-22), the module gene and the four cell attributes.
        /// <c>growth</c> and <c>maxModules</c> make a node's count a bounded rule rather than a
        /// number, and <c>attack</c>, <c>intake</c>, <c>protection</c> and <c>toughness</c> are
        /// carried at their defaults for round 45's mouth so that the stored genomes are
        /// re-extracted once rather than twice. A format-6 genome carries none of the six, and
        /// defaulting them is exactly what §9 refuses: <c>growth</c> defaulted would make every
        /// stored creature determinate — which happens to be true of the record and would stop
        /// being true the first time a round 44 genome was read back — and <c>toughness</c>
        /// defaulted to zero would hand round 45 a world of bodies that die to a scratch.
        /// </remarks>
        /// <remarks>
        /// 8 — D111 (2026-09-23), the buoyancy offset. A format-7 genome says nothing about where
        /// its parts float from; every one of them floats from its centre, so the number is
        /// knowable, and it is refused anyway for §9's reason: a reader that supplies a missing
        /// field is a reader that will one day supply the wrong one without saying so. The
        /// inocula were re-extracted once, at 0 (the build's converter under
        /// <c>scratch/r46-build/</c>).
        /// </remarks>
        public const int FormatVersion = 8;

        /// <summary>Written for a row that carries no organism id.</summary>
        public const long NoId = -1;

        /// <param name="genome">The recipe to write.</param>
        /// <param name="indent">Pretty-print. A row of a <c>.jsonl</c> file must not be indented.</param>
        /// <param name="id">
        /// The organism this genome belongs to — <see cref="Evosim.Core.Organism.Id"/>, and the
        /// same integer <c>lineage.jsonl</c> carries. Written as the row's first field when it is
        /// not <see cref="NoId"/>, and omitted otherwise: a genome on its own (a founder pool
        /// entry, an inoculum, a test fixture) belongs to no organism and must not pretend to.
        /// </param>
        /// <param name="moduleCounts">
        /// The organism's per-node module counts (<see cref="Evosim.Core.Organism.ModuleCounts"/>),
        /// written as <c>moduleCounts</c> when given: the half of a body's plan the genome does
        /// not carry, so that a body drawn from the row is the body the run was stepping and not
        /// the genome's minimum. Omitted for a genome on its own, and for a body that has never
        /// moved a count. Not read by <see cref="Read"/>, which returns the genome; a reader that
        /// wants the plan asks <see cref="ReadModuleCounts"/>.
        /// </param>
        /// <param name="lostPartPaths">
        /// The developer's paths to the parts a bite has taken off this body
        /// (<see cref="Evosim.Core.Organism.LostPartPaths"/>), written as <c>lostPaths</c> when
        /// given, for the same reason; <see cref="ReadLostPartPaths"/> reads them back.
        /// </param>
        public static string Write(
            Genome genome, bool indent = false, long id = NoId,
            int[] moduleCounts = null, IReadOnlyList<int[]> lostPartPaths = null)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));

            var w = new Json.Writer(indent);
            w.BeginObject();
            if (id != NoId) w.Field("id", id);

            if (moduleCounts != null && moduleCounts.Length > 0)
            {
                w.BeginArray("moduleCounts");
                foreach (int count in moduleCounts) w.Value(count);
                w.EndArray();
            }

            if (lostPartPaths != null && lostPartPaths.Count > 0)
            {
                w.BeginArray("lostPaths");
                foreach (int[] path in lostPartPaths)
                {
                    w.BeginArray();
                    foreach (int step in path) w.Value(step);
                    w.EndArray();
                }
                w.EndArray();
            }

            w.Field("format", FormatVersion);
            w.Field("root", genome.RootIndex);

            w.Field("adultScale", genome.AdultScale);

            w.BeginObject("reproduction")
                .Field("brood", genome.Reproduction.BroodSize)
                .Field("investment", genome.Reproduction.BirthInvestment)
                .Field("margin", genome.Reproduction.ReserveMargin)
                .EndObject();

            w.BeginArray("nodes");
            foreach (MorphNode node in genome.Nodes) WriteNode(w, node);
            w.EndArray();

            w.BeginArray("globalBrain");
            foreach (NeuronDef neuron in genome.GlobalBrain) WriteNeuron(w, neuron);
            w.EndArray();

            w.EndObject();
            return w.ToString();
        }

        public static Genome Read(string text)
        {
            JsonNode root = Json.Parse(text);

            int format = root["format"].AsInt();
            if (format != FormatVersion)
            {
                throw new FormatException(
                    $"Genome is format {format}, this build reads {FormatVersion}. There is no " +
                    "migration path: re-run, or check out the revision that wrote it. " +
                    "(Format 8 added the buoyancy offset (`BuoyancyOffset`, D111): where along " +
                    "its thinnest axis a part floats from. " +
                    "Format 7 added the module gene (`Growth`, `MaxModules`) and the four cell " +
                    "attributes (`Attack`, `Intake`, `Protection`, `Toughness`) of D106, so a " +
                    "format-6 genome says nothing about whether a node's count is fixed or is a " +
                    "rule. Format 6 added the breeding margin (`ReserveMargin`): the " +
                    "reserve a parent keeps after a birth, in seconds of its own standing cost. " +
                    "Format 5 had replaced the offspring endowment in joules with a birth " +
                    "investment as a fraction of the parent's body, and added the adult scale, " +
                    "so a genome older than that carries neither a size nor a readable " +
                    "investment.)");
            }

            var genome = new Genome
            {
                RootIndex = root["root"].AsInt(),
                AdultScale = root["adultScale"].AsFloat(),
                Reproduction = new ReproductionTraits
                {
                    BroodSize = root["reproduction"]["brood"].AsInt(),
                    BirthInvestment = root["reproduction"]["investment"].AsFloat(),
                    ReserveMargin = root["reproduction"]["margin"].AsFloat(),
                },
            };

            foreach (JsonNode n in root["nodes"].Items()) genome.Nodes.Add(ReadNode(n));

            var brain = new List<NeuronDef>();
            foreach (JsonNode n in root["globalBrain"].Items()) brain.Add(ReadNeuron(n));
            genome.GlobalBrain = brain.ToArray();

            return genome;
        }

        /// <summary>
        /// The organism id on a row, or <see cref="NoId"/> when the row carries none.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Read"/> rather than returned beside the genome, because the
        /// id is not part of the genome and a <see cref="Genome"/> that carried one would be a
        /// creature rather than a recipe — the distinction §9 keeps, and the reason two creatures
        /// can share a genome at all. The row is parsed twice by a caller that wants both; a
        /// snapshot is read once, off the hot path.
        /// </remarks>
        public static long ReadId(string text)
        {
            JsonNode root = Json.Parse(text);
            return root.Has("id") ? (long)root["id"].AsDouble() : NoId;
        }

        /// <summary>
        /// The row's <c>moduleCounts</c>, or null on a row without them — every recording before
        /// 2026-09-22 night, and any body that never moved a count.
        /// </summary>
        public static int[] ReadModuleCounts(string text)
        {
            JsonNode root = Json.Parse(text);
            if (!root.Has("moduleCounts")) return null;

            JsonNode array = root["moduleCounts"];
            var counts = new int[array.Count];
            for (int i = 0; i < counts.Length; i++) counts[i] = array[i].AsInt();
            return counts;
        }

        /// <summary>The row's <c>lostPaths</c>, or null on a row without them.</summary>
        public static List<int[]> ReadLostPartPaths(string text)
        {
            JsonNode root = Json.Parse(text);
            if (!root.Has("lostPaths")) return null;

            JsonNode array = root["lostPaths"];
            var paths = new List<int[]>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                JsonNode one = array[i];
                var path = new int[one.Count];
                for (int step = 0; step < path.Length; step++) path[step] = one[step].AsInt();
                paths.Add(path);
            }
            return paths;
        }

        // ---------------------------------------------------------------- nodes

        private static void WriteNode(Json.Writer w, MorphNode node)
        {
            w.BeginObject();
            w.Field("cell", node.CellTypeId);
            w.Field("shape", node.ShapeId);
            w.Field("joint", node.JointType.ToString());
            w.Field("power", node.Power);
            w.Field("lift", node.Lift);
            w.Field("recursiveLimit", node.RecursiveLimit);

            // D106's six, format 7. The module gene first, because it is the one this build
            // reads; the four attributes are round 45's and are written so the genomes are
            // re-extracted once.
            w.Field("growth", node.Growth.ToString());
            w.Field("maxModules", node.MaxModules);
            w.Field("attack", node.Attack);
            w.Field("intake", node.Intake);
            w.Field("protection", node.Protection);
            w.Field("toughness", node.Toughness);

            // D111, format 8.
            w.Field("buoyancyOffset", node.BuoyancyOffset);

            WriteFloat3(w, "dimensions", node.Dimensions);

            w.BeginArray("jointLimits");
            foreach (Float2 limit in node.JointLimits)
            {
                w.BeginObject().Field("min", limit.X).Field("max", limit.Y).EndObject();
            }
            w.EndArray();

            w.BeginArray("edges");
            foreach (MorphEdge edge in node.Edges) WriteEdge(w, edge);
            w.EndArray();

            w.BeginArray("neurons");
            foreach (NeuronDef neuron in node.Neurons) WriteNeuron(w, neuron);
            w.EndArray();

            w.EndObject();
        }

        private static MorphNode ReadNode(JsonNode n)
        {
            var node = new MorphNode
            {
                CellTypeId = n["cell"].AsString(),
                ShapeId = n["shape"].AsString(),
                JointType = ParseEnum<JointType>(n["joint"].AsString()),
                Power = n["power"].AsFloat(),
                Lift = n["lift"].AsFloat(),
                RecursiveLimit = n["recursiveLimit"].AsInt(),
                Growth = ParseEnum<ModuleGrowth>(n["growth"].AsString()),
                MaxModules = n["maxModules"].AsInt(),
                Attack = n["attack"].AsFloat(),
                Intake = n["intake"].AsFloat(),
                Protection = n["protection"].AsFloat(),
                Toughness = n["toughness"].AsFloat(),
                BuoyancyOffset = n["buoyancyOffset"].AsFloat(),
                Dimensions = ReadFloat3(n["dimensions"]),
            };

            var limits = new List<Float2>();
            foreach (JsonNode l in n["jointLimits"].Items())
            {
                limits.Add(new Float2(l["min"].AsFloat(), l["max"].AsFloat()));
            }
            node.JointLimits = limits.ToArray();

            foreach (JsonNode e in n["edges"].Items()) node.Edges.Add(ReadEdge(e));

            var neurons = new List<NeuronDef>();
            foreach (JsonNode ne in n["neurons"].Items()) neurons.Add(ReadNeuron(ne));
            node.Neurons = neurons.ToArray();

            return node;
        }

        // ---------------------------------------------------------------- edges

        private static void WriteEdge(Json.Writer w, MorphEdge edge)
        {
            w.BeginObject();
            w.Field("child", edge.Child);
            w.Field("terminalOnly", edge.TerminalOnly);

            WriteFloat3(w, "parentAnchor", edge.ParentAnchor);
            WriteFloat3(w, "childAnchor", edge.ChildAnchor);
            WriteFloat3(w, "scale", edge.Scale);

            w.BeginObject("orientation")
                .Field("x", edge.Orientation.X).Field("y", edge.Orientation.Y)
                .Field("z", edge.Orientation.Z).Field("w", edge.Orientation.W)
                .EndObject();

            w.BeginObject("reflect")
                .Field("x", edge.Reflect.X).Field("y", edge.Reflect.Y).Field("z", edge.Reflect.Z)
                .EndObject();

            w.EndObject();
        }

        private static MorphEdge ReadEdge(JsonNode e)
        {
            JsonNode o = e["orientation"];
            JsonNode r = e["reflect"];

            return new MorphEdge
            {
                Child = e["child"].AsInt(),
                TerminalOnly = e["terminalOnly"].AsBool(),
                ParentAnchor = ReadFloat3(e["parentAnchor"]),
                ChildAnchor = ReadFloat3(e["childAnchor"]),
                Scale = ReadFloat3(e["scale"]),
                Orientation = new Quat(
                    o["x"].AsFloat(), o["y"].AsFloat(), o["z"].AsFloat(), o["w"].AsFloat()),
                Reflect = new Bool3(r["x"].AsBool(), r["y"].AsBool(), r["z"].AsBool()),
            };
        }

        // ---------------------------------------------------------------- neurons

        private static void WriteNeuron(Json.Writer w, NeuronDef neuron)
        {
            w.BeginObject();
            w.Field("op", neuron.Op.ToString());
            w.Field("frequency", neuron.Frequency);
            w.Field("phase", neuron.Phase);
            w.Field("amplitude", neuron.Amplitude);
            w.Field("bias", neuron.Bias);

            w.BeginArray("inputs");
            foreach (NeuronInput input in neuron.Inputs)
            {
                w.BeginObject()
                    .Field("kind", input.Kind.ToString())
                    .Field("index", input.Index)
                    .Field("channel", input.Channel.ToString())
                    .Field("constant", input.Constant)
                    .Field("weight", input.Weight)
                    .EndObject();
            }
            w.EndArray();

            w.EndObject();
        }

        private static NeuronDef ReadNeuron(JsonNode n)
        {
            var inputs = new List<NeuronInput>();
            foreach (JsonNode i in n["inputs"].Items())
            {
                inputs.Add(new NeuronInput(
                    ParseEnum<NeuronInputKind>(i["kind"].AsString()),
                    i["index"].AsInt(),
                    ParseEnum<SensorChannel>(i["channel"].AsString()),
                    i["constant"].AsFloat(),
                    i["weight"].AsFloat()));
            }

            return new NeuronDef
            {
                Op = ParseEnum<NeuronOp>(n["op"].AsString()),
                Frequency = n["frequency"].AsFloat(),
                Phase = n["phase"].AsFloat(),
                Amplitude = n["amplitude"].AsFloat(),
                Bias = n["bias"].AsFloat(),
                Inputs = inputs.ToArray(),
            };
        }

        // ---------------------------------------------------------------- helpers

        private static void WriteFloat3(Json.Writer w, string name, Float3 v) =>
            w.BeginObject(name).Field("x", v.X).Field("y", v.Y).Field("z", v.Z).EndObject();

        private static Float3 ReadFloat3(JsonNode n) =>
            new Float3(n["x"].AsFloat(), n["y"].AsFloat(), n["z"].AsFloat());

        /// <remarks>
        /// Throws on an unrecognised name rather than falling back to the zero member, which is
        /// what <c>Enum.TryParse</c> would leave behind. A joint type that quietly became
        /// <c>Fixed</c> would produce a creature that cannot move and no indication why.
        /// </remarks>
        private static T ParseEnum<T>(string name) where T : struct
        {
            if (!Enum.TryParse(name, out T value) || !Enum.IsDefined(typeof(T), value))
            {
                throw new FormatException(
                    $"'{name}' is not a {typeof(T).Name}. Known: " +
                    string.Join(", ", Enum.GetNames(typeof(T))) + ".");
            }
            return value;
        }
    }
}
