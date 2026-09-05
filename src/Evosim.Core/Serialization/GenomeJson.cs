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
        public const int FormatVersion = 4;

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
        public static string Write(Genome genome, bool indent = false, long id = NoId)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));

            var w = new Json.Writer(indent);
            w.BeginObject();
            if (id != NoId) w.Field("id", id);
            w.Field("format", FormatVersion);
            w.Field("root", genome.RootIndex);

            w.BeginObject("reproduction")
                .Field("brood", genome.Reproduction.BroodSize)
                .Field("endowment", genome.Reproduction.OffspringEndowment)
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
                    "(Format 4 added the snapshot row's creature id, so a format-3 snapshot " +
                    "cannot be joined to lineage.jsonl and is refused rather than read blind.)");
            }

            var genome = new Genome
            {
                RootIndex = root["root"].AsInt(),
                Reproduction = new ReproductionTraits
                {
                    BroodSize = root["reproduction"]["brood"].AsInt(),
                    OffspringEndowment = root["reproduction"]["endowment"].AsFloat(),
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
