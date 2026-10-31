using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Which brain a run's creatures carry — D146.
    /// </summary>
    public enum BrainModel
    {
        /// <summary>
        /// The per-part neurons of DESIGN.md §4.3, each part's first neurons driving its joint
        /// (D019, D030). Every run before D146 ran this one, and it stays the default so they
        /// replay.
        /// </summary>
        PerNode = 0,

        /// <summary>
        /// One network per creature, placeless, with every sense of every part wired in and two
        /// outputs per degree of freedom (D146, proposals/propose-brain-v2.md).
        /// </summary>
        Central = 1,
    }

    /// <summary>Which of a degree of freedom's two outputs a unit drives (D135).</summary>
    public enum BrainOutputKind
    {
        /// <summary>How hard the joint strokes. The drive reads its positive half (D139).</summary>
        Power = 0,

        /// <summary>Where in its range the joint holds.</summary>
        Bend = 1,

        /// <summary>
        /// Whether an anchoring part holds on (D268): one per anchoring part, for a world whose anchoring cell lets
        /// the brain hold. Below the cell's release threshold the grip lets go. The prose calls it the hold output;
        /// the code calls it Grip because <see cref="SensorChannel.Hold"/> already names the joint envelope's sense.
        /// It drives no joint, so its <see cref="BrainOutput.DriveIndex"/> is -1.
        /// </summary>
        Grip = 2,
    }

    /// <summary>A unit of a <see cref="CentralBrain"/>: a hidden unit or an output.</summary>
    public sealed class BrainUnit
    {
        /// <summary>
        /// The unit's identity. An output's is <see cref="BrainIds.Output"/> of its joint, a hidden
        /// unit's is <see cref="BrainIds.Hidden"/> of the link it split.
        /// </summary>
        public ulong Id { get; set; }

        public float Bias { get; set; }

        public BrainUnit Clone() => new BrainUnit { Id = Id, Bias = Bias };

        public override string ToString() => $"{BrainIds.Hex(Id)} bias {Bias}";
    }

    /// <summary>A weighted connection. <see cref="From"/> may be a port, a hidden unit or an output.</summary>
    public sealed class BrainLink
    {
        public ulong From { get; set; }

        /// <summary>A hidden unit or an output. Never a port.</summary>
        public ulong To { get; set; }

        public float Weight { get; set; }

        public BrainLink Clone() => new BrainLink { From = From, To = To, Weight = Weight };

        public override string ToString() =>
            $"{BrainIds.Hex(From)} -> {BrainIds.Hex(To)} x {Weight}";
    }

    /// <summary>
    /// One creature's brain, held in the genome beside the body and on no node — D146.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three kinds of unit, and only two are stored.</b> The input ports are a property of the
    /// body: one per sense the run offers, on every part (<see cref="CentralWiring.Ports"/>), so
    /// a body that gains a limb gains its ports and one that loses a limb loses them, as the
    /// owner ruled. The outputs, two per degree of freedom, are stored with their biases, and the
    /// hidden units, which mutation adds, with theirs.
    /// </para>
    /// <para>
    /// <b>A unit is identified by an id, never by its position.</b> The old brain's outputs were
    /// a part's first neurons, so deleting neuron 0 turned the bend into the power. Here an
    /// output's id is a hash of its part's path, its degree of freedom and its kind, and a port's
    /// is a hash of the path, the channel and the index (<see cref="BrainIds"/>), so a link keeps
    /// meaning the same sense on the same limb whatever else the body gains or loses.
    /// </para>
    /// <para>
    /// <b>A link not stored has weight 0</b>, which is what makes a large body's brain cheap: a
    /// founder may be wired densely, but nothing obliges a genome to carry a link for every
    /// port and output pair, and adding one is a mutation.
    /// </para>
    /// </remarks>
    public sealed class CentralBrain
    {
        public List<BrainUnit> Outputs { get; } = new List<BrainUnit>();

        public List<BrainUnit> Hidden { get; } = new List<BrainUnit>();

        public List<BrainLink> Links { get; } = new List<BrainLink>();

        public CentralBrain Clone()
        {
            var clone = new CentralBrain();
            foreach (BrainUnit u in Outputs) clone.Outputs.Add(u.Clone());
            foreach (BrainUnit u in Hidden) clone.Hidden.Add(u.Clone());
            foreach (BrainLink l in Links) clone.Links.Add(l.Clone());
            return clone;
        }

        /// <summary>Problems with this brain that no body could cure. Empty when well formed.</summary>
        /// <remarks>
        /// What depends on the body (a link from a port the body does not have) is not a fault
        /// here: it is a limb this genome lost, and the link reads nothing until it is pruned.
        /// </remarks>
        public List<string> Validate()
        {
            var issues = new List<string>();
            Validate(issues);
            return issues;
        }

        /// <summary>Adds this brain's problems to <paramref name="issues"/>.</summary>
        public void Validate(List<string> issues)
        {
            var units = new HashSet<ulong>();
            foreach (BrainUnit u in Outputs) Check(u, "output");
            foreach (BrainUnit u in Hidden) Check(u, "hidden unit");

            void Check(BrainUnit u, string what)
            {
                if (u == null) { issues.Add($"brain: a null {what}."); return; }
                if (u.Id == 0) issues.Add($"brain: {what} with id 0.");
                if (!units.Add(u.Id)) issues.Add($"brain: unit {BrainIds.Hex(u.Id)} appears twice.");
                if (!IsFinite(u.Bias)) issues.Add($"brain: {what} {BrainIds.Hex(u.Id)} has a non-finite bias.");
            }

            var pairs = new HashSet<(ulong, ulong)>();
            foreach (BrainLink l in Links)
            {
                if (l == null) { issues.Add("brain: a null link."); continue; }
                if (l.From == 0 || l.To == 0) issues.Add("brain: a link with an endpoint of id 0.");
                if (!units.Contains(l.To))
                {
                    issues.Add($"brain: link into {BrainIds.Hex(l.To)}, which is no unit of this brain.");
                }

                if (!pairs.Add((l.From, l.To)))
                {
                    issues.Add($"brain: two links from {BrainIds.Hex(l.From)} to {BrainIds.Hex(l.To)}.");
                }

                if (!IsFinite(l.Weight)) issues.Add($"brain: link {l} has a non-finite weight.");
            }
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public override string ToString() =>
            $"CentralBrain({Outputs.Count} outputs, {Hidden.Count} hidden, {Links.Count} links)";
    }

    /// <summary>
    /// The ids of a <see cref="CentralBrain"/>'s parts, ports and units: 64-bit hashes of what
    /// they are attached to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A part's id is its path from the root</b>, the developer's own identity
    /// (<see cref="Developer"/>'s part paths), hashed step by step: the edge followed and which
    /// mirror copy. The edge is named by <see cref="MorphEdge.Id"/> where it has one, so a part
    /// keeps its id when an earlier edge of its node is removed and the indices shift, and by its
    /// index otherwise (a genome of the old model, whose body never changes in the nursery).
    /// </para>
    /// <para>
    /// <b>Collisions are possible and not handled.</b> At 64 bits and a few thousand ids a body,
    /// the chance is of the order of 1e-12 a body. Zero is reserved as no id.
    /// </para>
    /// </remarks>
    public static class BrainIds
    {
        private const ulong RootSeed = 0x524F4F5442524E31UL;   // "ROOTBRN1"
        private const ulong PortTag = 0x504F5254UL;             // "PORT"
        private const ulong OutputTag = 0x4F555450UL;           // "OUTP"
        private const ulong HiddenTag = 0x48494444UL;           // "HIDD"
        private const ulong EdgeTag = 0x45444745UL;             // "EDGE"
        private const ulong IndexFlag = 0x8000000000000000UL;

        /// <summary>The root part's id.</summary>
        public static ulong Root => NonZero(Mix(RootSeed, 1UL));

        /// <summary>The id of a child part reached from <paramref name="parent"/> by one edge.</summary>
        /// <param name="parent">The parent part's id.</param>
        /// <param name="edgeId"><see cref="MorphEdge.Id"/>, or 0 where the edge has none.</param>
        /// <param name="edgeIndex">The edge's index in its node, used only when it has no id.</param>
        /// <param name="mirrorOrdinal">Which of the edge's mirror copies.</param>
        public static ulong Child(ulong parent, ulong edgeId, int edgeIndex, int mirrorOrdinal)
        {
            ulong key = edgeId != 0 ? edgeId : IndexFlag | (uint)edgeIndex;
            return NonZero(Mix(Mix(Mix(parent, EdgeTag), key), (ulong)(uint)mirrorOrdinal));
        }

        /// <summary>One sense of one part: a channel at one of its indices.</summary>
        public static ulong Port(ulong part, SensorChannel channel, int index) =>
            NonZero(Mix(Mix(Mix(part, PortTag), (ulong)(int)channel), (ulong)(uint)index));

        /// <summary>One of a degree of freedom's two outputs.</summary>
        public static ulong Output(ulong part, int dof, BrainOutputKind kind) =>
            NonZero(Mix(Mix(Mix(part, OutputTag), (ulong)(uint)dof), (ulong)(int)kind));

        /// <summary>
        /// The hidden unit made by splitting the link from <paramref name="from"/> to
        /// <paramref name="to"/>, the <paramref name="k"/>-th time that link was split. The same
        /// split in two lineages gives the same id, which is NEAT's historical marking without a
        /// global counter: it is what lets the nursery's crossover line two brains up.
        /// </summary>
        public static ulong Hidden(ulong from, ulong to, int k) =>
            NonZero(Mix(Mix(Mix(Mix(HiddenTag, from), to), (ulong)(uint)k), HiddenTag));

        /// <summary>An id as the genome file writes it: sixteen hex digits.</summary>
        public static string Hex(ulong id) => id.ToString("x16");

        public static ulong ParseHex(string text)
        {
            if (text == null || text.Length != 16 ||
                !ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong id))
            {
                throw new FormatException($"\"{text}\" is not a brain id (sixteen hex digits).");
            }

            return id;
        }

        // splitmix64's finaliser over a combination of the two. Not cryptographic and does not
        // need to be: the ids are labels, and what matters is that they are spread and fixed.
        private static ulong Mix(ulong a, ulong b)
        {
            ulong z = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong NonZero(ulong id) => id == 0 ? 1UL : id;
    }
}
