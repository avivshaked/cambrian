using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// How far one genome has drifted from another — the metric D057's species boundary is
    /// measured against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A weighted sum of three terms, NEAT-shaped and adapted to this graph.</b> NEAT's
    /// compatibility distance (Stanley &amp; Miikkulainen 2002) counts excess and disjoint genes
    /// by a historical marking (innovation numbers) that this encoding does not have —
    /// <see cref="MorphNode"/> carries
    /// no birth-order identity, and <see cref="Mutator"/> never invents one. What it does have
    /// is a matching rule that is almost always right without one: <c>Mutator.Mutate</c> clones
    /// the parent and edits nodes and edges <i>in place, by index</i> — <c>AddNode</c> only ever
    /// appends, and <c>RemoveNodeAt</c> is the one operator that renumbers anything. So two
    /// genomes on the same line of descent agree on what index <c>i</c> means far more often
    /// than not, and <b>positional index is this metric's entire matching rule</b> — for nodes,
    /// for a matched node's edges, and for a matched node's neurons and their inputs. Nothing
    /// here tries to re-align a genome whose nodes were removed and renumbered against one whose
    /// were not; that shows up as topology distance instead, which is the honest reading of it —
    /// a removal really did change what index <c>i</c> refers to.
    /// </para>
    /// <para>
    /// <b>Four terms, and where each field lands.</b> D057 asked for three; a fourth was split
    /// out of the parameter term for brain weights specifically — see the third and fourth items
    /// below for why continuous body parameters and continuous brain parameters are not the same
    /// question here, whatever they share mathematically.
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Cell type</b> (<see cref="RunConfig.SpeciesCellTypeWeight"/>) — the multiset symmetric
    /// difference of <see cref="MorphNode.CellTypeId"/> over every node, <i>not</i> position-
    /// matched: D057 asks for a difference "in the multiset of cell types over nodes", and a
    /// multiset comparison is exactly that, independent of where in either genome a type sits.
    /// One node changing trade moves the multiset by exactly one type lost and one type gained,
    /// so this term returns exactly 1.0 for a single cell-type mutation regardless of body size —
    /// which is what lets the weight be read directly against θ
    /// (<see cref="RunConfig.SpeciesDriftThreshold"/>): setting it at or above θ makes one
    /// cell-type change alone exceed the boundary, which is D057's one deliberate commitment.
    /// </item>
    /// <item>
    /// <b>Topology</b> (<see cref="RunConfig.SpeciesTopologyWeight"/>) — presence and
    /// connectivity, position-matched as described above: node count mismatch, root-index
    /// mismatch, per matched node a <see cref="MorphNode.ShapeId"/> or
    /// <see cref="MorphNode.JointType"/> swap (which part class or which joint class this
    /// position holds — discrete, not a continuous field), edge-count mismatch, and for matched
    /// edges whether <see cref="MorphEdge.Child"/>, <see cref="MorphEdge.TerminalOnly"/> or
    /// <see cref="MorphEdge.Reflect"/> differ (what an edge connects to and how many copies it
    /// makes — the graph's actual shape). The local brain and the global brain get the same
    /// treatment: neuron-count mismatch, matched neurons' <see cref="NeuronOp"/> swaps,
    /// input-count mismatch, and matched inputs' <see cref="NeuronInputKind"/>/
    /// <see cref="SensorChannel"/>/index mismatches — a rewired connection is a topology change
    /// in the brain graph for the same reason a re-pointed edge is one in the body graph. This
    /// stays with topology rather than moving to the fourth term: it is still "does a connection
    /// exist", the same question the body-graph half of this term asks, not "how strong is it".
    /// </item>
    /// <item>
    /// <b>Body parameters</b> (<see cref="RunConfig.SpeciesParameterWeight"/>) — continuous,
    /// non-neural fields on matched nodes and edges: <see cref="MorphNode.Dimensions"/>,
    /// <see cref="MorphNode.Power"/>, <see cref="MorphNode.Lift"/>,
    /// <see cref="MorphNode.RecursiveLimit"/>, joint-limit magnitudes, an edge's
    /// anchors/scale/orientation, and the genome-level <see cref="ReproductionTraits"/>. Each
    /// field is normalised into approximately <c>[0, 1]</c> before it is summed, so a 120 N·m
    /// link and a 0.15 m half-extent contribute on the same footing and neither field can
    /// dominate just by living on a larger scale.
    /// </item>
    /// <item>
    /// <b>Brain parameters</b> (<see cref="RunConfig.SpeciesBrainWeight"/>, default 0) — every
    /// matched <see cref="NeuronDef"/>'s frequency/phase/amplitude/bias and every matched
    /// <see cref="NeuronInput"/>'s weight and constant, on both a node's local brain and the
    /// genome's global one, normalised the same way as the body-parameter term.
    /// <b>Split out and defaulted off deliberately:</b> movement has never paid its energy cost
    /// and perception is four of ten channels (CLAUDE.md's own state-of-play), so a brain in this
    /// world is currently close to selectively neutral — nothing is holding a weight anywhere in
    /// particular, and an unselected continuous value does a random walk. Folded into a
    /// founder-anchored threshold, that walk would eventually cross it on drift alone and call
    /// the crossing speciation, which is not a boundary, it is a stopwatch on noise. The weight
    /// exists to be turned on <i>between</i> rounds, once brains are worth having and holding —
    /// at which point this term is what would let two lineages sharing one body plan but
    /// controlling it differently (cryptic species) show up as more than one.
    /// </item>
    /// </list>
    /// <para>
    /// <b>Deterministic and symmetric by construction.</b> No RNG is touched, matching is by
    /// list index rather than by any traversal order that could differ between calls, and every
    /// per-field comparison — multiset counts, index equality, <c>|a - b|</c> — is already
    /// symmetric in its two arguments. <c>Between(a, b)</c> therefore equals <c>Between(b, a)</c>
    /// and returns the same value on every call for the same two genomes, which is what lets
    /// D057's assignment replay exactly from <c>(genome, seed, configHash)</c>: this function
    /// contributes nothing that could vary between a run and its replay.
    /// </para>
    /// <para>⚠ Project inference (DECISIONS.md D057): a synthesis of NEAT's compatibility
    /// distance (Stanley &amp; Miikkulainen 2002), microbiology's OTU thresholds and
    /// phylogeny-tracking-with-coarsening (Dolson &amp; Ofria): none of the three has been read
    /// primarily by the literature review, and the NEAT paper is the one a review round should
    /// verify before the weights below are leaned on quantitatively.</para>
    /// </remarks>
    public static class SpeciesDistance
    {
        /// <summary>
        /// Distance between two genomes — D057's species boundary is "founder-distance exceeds
        /// θ", and this is the founder-distance.
        /// </summary>
        /// <param name="cellTypeWeight">
        /// Distance per unit of cell-type-multiset difference — <see cref="RunConfig.SpeciesCellTypeWeight"/>.
        /// </param>
        /// <param name="topologyWeight">
        /// Distance per unit of node/edge/brain-connectivity difference — <see cref="RunConfig.SpeciesTopologyWeight"/>.
        /// </param>
        /// <param name="parameterWeight">
        /// Distance per unit of normalised continuous body-field difference — <see cref="RunConfig.SpeciesParameterWeight"/>.
        /// </param>
        /// <param name="brainWeight">
        /// Distance per unit of normalised continuous brain-field difference — <see cref="RunConfig.SpeciesBrainWeight"/>.
        /// Default 0; see the class remarks for why brain drift is excluded until a run turns it on.
        /// </param>
        /// <remarks>
        /// A weight of zero skips that term's computation entirely rather than computing it and
        /// multiplying by zero — the fast path <see cref="RunConfig.SpeciesDriftThreshold"/>'s
        /// own doc comment promises (no distance computations at all when the feature is off)
        /// only holds one level up, in <see cref="World"/>, but the same discipline here is what
        /// lets a test isolate one term by zeroing the other three, and is why
        /// <paramref name="brainWeight"/> defaulting to 0 on <see cref="RunConfig"/> actually
        /// costs nothing rather than merely reading as nothing.
        /// </remarks>
        public static float Between(
            Genome a, Genome b,
            float cellTypeWeight, float topologyWeight, float parameterWeight, float brainWeight)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            float distance = 0f;

            if (cellTypeWeight > 0f) distance += cellTypeWeight * CellTypeUnits(a, b);
            if (topologyWeight > 0f) distance += topologyWeight * TopologyUnits(a, b);
            if (parameterWeight > 0f) distance += parameterWeight * ParameterUnits(a, b);
            if (brainWeight > 0f) distance += brainWeight * BrainUnits(a, b);

            return distance;
        }

        // ---------------------------------------------------------------- cell type

        /// <summary>
        /// Half the multiset symmetric difference of <see cref="MorphNode.CellTypeId"/> over all
        /// nodes. One node changing type moves exactly one count down and one up, so this is
        /// exactly 1.0 per cell-type mutation — see the class remarks.
        /// </summary>
        private static float CellTypeUnits(Genome a, Genome b)
        {
            // Sorted rather than a plain Dictionary, so the summation order — and therefore the
            // exact float result — cannot depend on hash-bucket layout. The sum of absolute
            // values is mathematically order-independent, but floating-point addition is not,
            // and D057 replays this exactly (class remarks).
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (MorphNode node in a.Nodes) Bump(counts, node.CellTypeId, +1);
            foreach (MorphNode node in b.Nodes) Bump(counts, node.CellTypeId, -1);

            int sumAbs = 0;
            foreach (KeyValuePair<string, int> kv in counts) sumAbs += Math.Abs(kv.Value);

            return sumAbs * 0.5f;
        }

        private static void Bump(SortedDictionary<string, int> counts, string key, int delta)
        {
            counts.TryGetValue(key, out int current);
            counts[key] = current + delta;
        }

        // ---------------------------------------------------------------- topology

        private static float TopologyUnits(Genome a, Genome b)
        {
            float units = Math.Abs(a.Nodes.Count - b.Nodes.Count);

            if (a.RootIndex != b.RootIndex) units += 1f;

            int minNodes = Math.Min(a.Nodes.Count, b.Nodes.Count);
            for (int i = 0; i < minNodes; i++)
            {
                MorphNode na = a.Nodes[i], nb = b.Nodes[i];

                if (na.ShapeId != nb.ShapeId) units += 1f;
                if (na.JointType != nb.JointType) units += 1f;

                units += Math.Abs(na.Edges.Count - nb.Edges.Count);

                int minEdges = Math.Min(na.Edges.Count, nb.Edges.Count);
                for (int e = 0; e < minEdges; e++)
                {
                    MorphEdge ea = na.Edges[e], eb = nb.Edges[e];

                    if (ea.Child != eb.Child) units += 1f;
                    if (ea.TerminalOnly != eb.TerminalOnly) units += 1f;
                    if (!ea.Reflect.Equals(eb.Reflect)) units += 1f;
                }

                units += NeuronTopologyUnits(na.Neurons, nb.Neurons);
            }

            units += NeuronTopologyUnits(a.GlobalBrain, b.GlobalBrain);

            return units;
        }

        /// <summary>
        /// Presence and connectivity of one matched pair of neuron sets — a node's local brain,
        /// or the genome's global one. Shared by both call sites in <see cref="TopologyUnits"/>.
        /// </summary>
        private static float NeuronTopologyUnits(NeuronDef[] a, NeuronDef[] b)
        {
            float units = Math.Abs(a.Length - b.Length);
            int minNeurons = Math.Min(a.Length, b.Length);

            for (int i = 0; i < minNeurons; i++)
            {
                if (a[i].Op != b[i].Op) units += 1f;

                NeuronInput[] ia = a[i].Inputs, ib = b[i].Inputs;
                units += Math.Abs(ia.Length - ib.Length);

                int minInputs = Math.Min(ia.Length, ib.Length);
                for (int k = 0; k < minInputs; k++)
                {
                    if (ia[k].Kind != ib[k].Kind)
                    {
                        units += 1f;
                    }
                    else if (ia[k].Kind == NeuronInputKind.Sensor)
                    {
                        if (ia[k].Channel != ib[k].Channel) units += 1f;
                    }
                    else if (ia[k].Index != ib[k].Index)
                    {
                        units += 1f;
                    }
                }
            }

            return units;
        }

        // ---------------------------------------------------------------- body parameters

        private static float ParameterUnits(Genome a, Genome b)
        {
            float units =
                RelativeDiff(a.Reproduction.BroodSize, b.Reproduction.BroodSize) +
                RelativeDiff(a.Reproduction.OffspringEndowment, b.Reproduction.OffspringEndowment);

            int minNodes = Math.Min(a.Nodes.Count, b.Nodes.Count);
            for (int i = 0; i < minNodes; i++)
            {
                MorphNode na = a.Nodes[i], nb = b.Nodes[i];

                units += RelativeDiff(na.Dimensions.X, nb.Dimensions.X);
                units += RelativeDiff(na.Dimensions.Y, nb.Dimensions.Y);
                units += RelativeDiff(na.Dimensions.Z, nb.Dimensions.Z);
                units += RelativeDiff(na.Power, nb.Power);
                units += RelativeDiff(na.Lift, nb.Lift);
                units += RelativeDiff(na.RecursiveLimit, nb.RecursiveLimit);

                int minLimits = Math.Min(na.JointLimits.Length, nb.JointLimits.Length);
                for (int d = 0; d < minLimits; d++)
                {
                    // Symmetric limits (Genome.Validate does not require it, but the mutator only
                    // ever produces them — see MutateNode) so the magnitude carries the field.
                    units += RelativeDiff(na.JointLimits[d].Y, nb.JointLimits[d].Y);
                }

                int minEdges = Math.Min(na.Edges.Count, nb.Edges.Count);
                for (int e = 0; e < minEdges; e++)
                {
                    MorphEdge ea = na.Edges[e], eb = nb.Edges[e];

                    units += BoundedDiff(ea.ParentAnchor, eb.ParentAnchor);
                    units += BoundedDiff(ea.ChildAnchor, eb.ChildAnchor);
                    units += RelativeDiff(ea.Scale.X, eb.Scale.X);
                    units += RelativeDiff(ea.Scale.Y, eb.Scale.Y);
                    units += RelativeDiff(ea.Scale.Z, eb.Scale.Z);
                    units += AngularDiff(ea.Orientation, eb.Orientation);
                }
            }

            return units;
        }

        // ---------------------------------------------------------------- brain parameters

        /// <summary>
        /// Continuous brain fields, position-matched exactly like <see cref="ParameterUnits"/> —
        /// see the class remarks for why this is its own term rather than folded into that one.
        /// </summary>
        private static float BrainUnits(Genome a, Genome b)
        {
            float units = NeuronParameterUnits(a.GlobalBrain, b.GlobalBrain);

            int minNodes = Math.Min(a.Nodes.Count, b.Nodes.Count);
            for (int i = 0; i < minNodes; i++)
            {
                units += NeuronParameterUnits(a.Nodes[i].Neurons, b.Nodes[i].Neurons);
            }

            return units;
        }

        private static float NeuronParameterUnits(NeuronDef[] a, NeuronDef[] b)
        {
            float units = 0f;
            int minNeurons = Math.Min(a.Length, b.Length);

            for (int i = 0; i < minNeurons; i++)
            {
                units += RelativeDiff(a[i].Frequency, b[i].Frequency);
                units += AngularDiff(a[i].Phase, b[i].Phase);
                units += RelativeDiff(a[i].Amplitude, b[i].Amplitude);
                units += RelativeDiff(a[i].Bias, b[i].Bias);

                NeuronInput[] ia = a[i].Inputs, ib = b[i].Inputs;
                int minInputs = Math.Min(ia.Length, ib.Length);
                for (int k = 0; k < minInputs; k++)
                {
                    units += RelativeDiff(ia[k].Weight, ib[k].Weight);
                    units += RelativeDiff(ia[k].Constant, ib[k].Constant);
                }
            }

            return units;
        }

        /// <summary>
        /// Symmetric relative difference, bounded to <c>[0, 1]</c> for any two same-signed
        /// values — the "no single field dominates" normalisation D057 asks for. A Canberra-style
        /// ratio rather than a fixed divisor, because nothing here knows in advance what scale a
        /// mutated field will drift to.
        /// </summary>
        private static float RelativeDiff(float x, float y)
        {
            float denom = Math.Abs(x) + Math.Abs(y);
            return denom < 1e-9f ? 0f : Math.Abs(x - y) / denom;
        }

        /// <summary>
        /// Mean per-axis difference of two vectors already confined to <c>[-1, 1]</c> —
        /// <see cref="MorphEdge.ParentAnchor"/> and <see cref="MorphEdge.ChildAnchor"/>. The
        /// largest possible gap per axis is 2, so dividing by <c>3 axes * 2</c> keeps this on the
        /// same <c>[0, 1]</c> footing as <see cref="RelativeDiff"/> without needing a ratio that
        /// a zero anchor would make undefined.
        /// </summary>
        private static float BoundedDiff(Float3 a, Float3 b) =>
            (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z)) / 6f;

        /// <summary>
        /// Angle between two rotations, normalised to <c>[0, 1]</c> by dividing by π —
        /// <see cref="MorphEdge.Orientation"/>. <c>|dot|</c> rather than <c>dot</c>, because a
        /// quaternion and its negation represent the same rotation and must read as identical.
        /// </summary>
        private static float AngularDiff(Quat a, Quat b)
        {
            float dot = Math.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);
            if (dot > 1f) dot = 1f; // guards Acos against rounding past the domain
            return (2f * (float)Math.Acos(dot)) / (float)Math.PI;
        }

        /// <summary>
        /// Angular difference between two phases in radians, normalised to <c>[0, 1]</c> by the
        /// shortest way around the circle — <see cref="NeuronDef.Phase"/>. <see cref="RelativeDiff"/>
        /// would read a phase near 0 and one near 2π as maximally different when they are the
        /// same oscillator a mutation barely touched.
        /// </summary>
        private static float AngularDiff(float phaseA, float phaseB)
        {
            const float TwoPi = 6.28318530718f;
            float delta = Math.Abs(phaseA - phaseB) % TwoPi;
            if (delta > TwoPi * 0.5f) delta = TwoPi - delta;
            return delta / (float)Math.PI;
        }
    }
}
