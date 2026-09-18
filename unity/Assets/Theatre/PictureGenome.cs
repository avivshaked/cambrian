using System;
using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// A snapshot row read for a picture and for nothing else — §11 of
    /// <c>logbook/specs/snapshot-render-spec.md</c> (the owner's ruling, 2026-09-18).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a second reader exists at all.</b> <see cref="GenomeJson"/> refuses any format but
    /// the current one, and it is right to: a format-5 genome has no breeding margin and a
    /// format-4 one has no size at all, so a creature read across a bump would be a genome
    /// wearing another's identity. None of that reaches a drawing. What a body looks like is
    /// decided by the nodes' body fields and the adult scale, and those have not changed since
    /// format 4.
    /// </para>
    /// <para>
    /// <b>It is tried second and never instead.</b> <see cref="SnapshotWorld"/> reads every row
    /// with <see cref="GenomeJson"/> first, so a run this build recorded develops through the
    /// same code it always did, and a row read here marks the picture (<c>OLD-RUN READ</c>) and
    /// the log.
    /// </para>
    /// <para>
    /// <b>What it will not do.</b> A format below 4 is refused, naming the format: those rows
    /// carry no organism id, and a body with no id cannot be joined to a place. A node missing
    /// any field that decides its geometry is refused, naming the node and the field, rather than
    /// drawn at some size nobody chose. Everything the developer does not read is ignored
    /// outright: the brains, the sensors, the global brain, and the reproduction traits, which
    /// are filled with the blandest values <see cref="Genome.Validate"/> accepts so that the
    /// recipe is well formed. Those three numbers are not the creature's and nothing here reads
    /// them back.
    /// </para>
    /// <para>
    /// <b>Format 4 has no adult scale</b>, because the field arrived with format 5 (D087). A
    /// format-4 body is its nodes' dimensions, which is a scale of one, and that is what it is
    /// given.
    /// </para>
    /// </remarks>
    public static class PictureGenome
    {
        /// <summary>The oldest format that carries an organism id, and so the oldest joinable one.</summary>
        public const int OldestFormat = 4;

        /// <summary>The organism id on a row, or -1 — the same field <see cref="GenomeJson.ReadId"/> reads.</summary>
        public static long ReadId(string row)
        {
            JsonNode root = Json.Parse(row);
            return root.Has("id") ? (long)root["id"].AsDouble() : GenomeJson.NoId;
        }

        /// <summary>
        /// The body a row describes, at its adult size.
        /// </summary>
        /// <param name="row">One line of a <c>snapshots/*.jsonl</c> file.</param>
        /// <param name="format">The format the row was written in, for the log.</param>
        public static Genome Read(string row, out int format)
        {
            JsonNode root = Json.Parse(row);

            if (!root.Has("format"))
            {
                throw new FormatException(
                    "The row carries no format, so nothing says how to read it.");
            }

            format = root["format"].AsInt();

            if (format < OldestFormat)
            {
                throw new FormatException(
                    "The row is format " + format + ", and a picture reads " + OldestFormat +
                    " and up. A format-" + format + " row carries no organism id, so there is " +
                    "no way to say which body in positions.jsonl it is.");
            }

            if (!root.Has("root")) throw Missing("root", "the row");
            if (!root.Has("nodes")) throw Missing("nodes", "the row");

            var genome = new Genome
            {
                RootIndex = root["root"].AsInt(),

                // Format 4 predates the field (D087): its body is its nodes' dimensions, which is
                // a scale of one.
                AdultScale = root.Has("adultScale") ? root["adultScale"].AsFloat() : 1f,

                // Not the creature's, and never read for a picture: the blandest values
                // Genome.Validate accepts, so that the recipe is well formed for the developer.
                Reproduction = new ReproductionTraits
                {
                    BroodSize = 1,
                    BirthInvestment = 1f,
                    ReserveMargin = 0f,
                },

                GlobalBrain = Array.Empty<NeuronDef>(),
            };

            int index = 0;
            foreach (JsonNode node in root["nodes"].Items()) genome.Nodes.Add(ReadNode(node, index++));

            return genome;
        }

        private static MorphNode ReadNode(JsonNode n, int index)
        {
            string where = "node " + index;

            var node = new MorphNode
            {
                CellTypeId = Text(n, "cell", where),
                ShapeId = Text(n, "shape", where),
                JointType = Joint(Text(n, "joint", where), where),
                RecursiveLimit = Int(n, "recursiveLimit", where),
                Dimensions = Float3Of(n, "dimensions", where),

                // Read rather than ignored although §11 does not list them: Genome.Validate
                // refuses a joint with no power and a rigid part that carries some, so a
                // defaulted pair would refuse a body the run drew perfectly well.
                Power = Number(n, "power", where),
                Lift = Number(n, "lift", where),

                // Nothing here is drawn, and an empty list is not a default: the developer copies
                // the array onto the part and nothing in a still frame reads it.
                Neurons = Array.Empty<NeuronDef>(),
            };

            var limits = new List<Float2>();

            if (!n.Has("jointLimits")) throw Missing("jointLimits", where);

            foreach (JsonNode l in n["jointLimits"].Items())
            {
                limits.Add(new Float2(Number(l, "min", where + " joint limit"),
                                      Number(l, "max", where + " joint limit")));
            }

            node.JointLimits = limits.ToArray();

            if (!n.Has("edges")) throw Missing("edges", where);

            int edge = 0;
            foreach (JsonNode e in n["edges"].Items()) node.Edges.Add(ReadEdge(e, where + " edge " + edge++));

            return node;
        }

        private static MorphEdge ReadEdge(JsonNode e, string where)
        {
            if (!e.Has("orientation")) throw Missing("orientation", where);
            if (!e.Has("reflect")) throw Missing("reflect", where);

            JsonNode o = e["orientation"];
            JsonNode r = e["reflect"];

            return new MorphEdge
            {
                Child = Int(e, "child", where),
                TerminalOnly = Bool(e, "terminalOnly", where),
                ParentAnchor = Float3Of(e, "parentAnchor", where),
                ChildAnchor = Float3Of(e, "childAnchor", where),
                Scale = Float3Of(e, "scale", where),
                Orientation = new Quat(
                    Number(o, "x", where + " orientation"), Number(o, "y", where + " orientation"),
                    Number(o, "z", where + " orientation"), Number(o, "w", where + " orientation")),
                Reflect = new Bool3(
                    Bool(r, "x", where + " reflect"), Bool(r, "y", where + " reflect"),
                    Bool(r, "z", where + " reflect")),
            };
        }

        private static JointType Joint(string name, string where)
        {
            if (!Enum.TryParse(name, false, out JointType joint))
            {
                throw new FormatException(
                    where + ": '" + name + "' is not a joint type. They are " +
                    string.Join(", ", Enum.GetNames(typeof(JointType))) + ".");
            }

            return joint;
        }

        private static Float3 Float3Of(JsonNode n, string key, string where)
        {
            if (!n.Has(key)) throw Missing(key, where);

            JsonNode v = n[key];
            return new Float3(
                Number(v, "x", where + " " + key), Number(v, "y", where + " " + key),
                Number(v, "z", where + " " + key));
        }

        private static float Number(JsonNode n, string key, string where)
        {
            if (!n.Has(key)) throw Missing(key, where);

            return n[key].AsFloat();
        }

        private static int Int(JsonNode n, string key, string where)
        {
            if (!n.Has(key)) throw Missing(key, where);

            return n[key].AsInt();
        }

        private static bool Bool(JsonNode n, string key, string where)
        {
            if (!n.Has(key)) throw Missing(key, where);

            return n[key].AsBool();
        }

        private static string Text(JsonNode n, string key, string where)
        {
            if (!n.Has(key)) throw Missing(key, where);

            return n[key].AsString();
        }

        private static FormatException Missing(string key, string where) =>
            new FormatException(
                where + " has no '" + key + "', and it decides what the body looks like. A " +
                "picture may read an old row, and it may not invent one.");
    }
}
