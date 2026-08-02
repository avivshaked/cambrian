using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>Knobs for <see cref="GenomeFactory"/>. Part of the config hash (DESIGN.md §7).</summary>
    public sealed class RandomGenomeOptions
    {
        public int MinNodes { get; set; } = 2;
        public int MaxNodes { get; set; } = 5;

        /// <summary>Outgoing edges per node, before terminal edges are considered.</summary>
        public int MaxEdgesPerNode { get; set; } = 2;

        public int MinRecursiveLimit { get; set; } = 1;
        public int MaxRecursiveLimit { get; set; } = 4;

        /// <summary>Half-extent range, in metres, per axis.</summary>
        public float MinHalfExtent { get; set; } = 0.15f;
        public float MaxHalfExtent { get; set; } = 0.6f;

        /// <summary>Per-edge cumulative scale range. Below 1 so recursive chains taper rather than explode.</summary>
        public float MinEdgeScale { get; set; } = 0.6f;
        public float MaxEdgeScale { get; set; } = 1.0f;

        /// <summary>Chance an edge sets one reflection flag, giving a bilateral pair.</summary>
        public float ReflectChance { get; set; } = 0.35f;

        /// <summary>Chance an extra edge is marked terminal, giving a differentiated extremity.</summary>
        public float TerminalChance { get; set; } = 0.3f;

        /// <summary>Chance an edge tilts the child rather than attaching it square-on.</summary>
        public float RotateChance { get; set; } = 0.4f;

        /// <summary>
        /// Largest tilt an edge may apply, in degrees.
        /// </summary>
        /// <remarks>
        /// Initially this drew uniformly from all of SO(3), which reliably produced boxes
        /// buried inside other boxes: a child is placed so its anchor meets the parent's
        /// anchor, and then rotating it far about that contact point swings its body straight
        /// through the parent. Half a turn puts it entirely inside.
        ///
        /// This bounds the initial population only. Mutation may still take an edge anywhere —
        /// nothing here is a rule about what a genome may express.
        /// </remarks>
        public float MaxEdgeTiltDegrees { get; set; } = 50f;

        public int MinNeuronsPerNode { get; set; } = 1;
        public int MaxNeuronsPerNode { get; set; } = 3;

        public float MinOscillatorHz { get; set; } = 0.3f;
        public float MaxOscillatorHz { get; set; } = 2.5f;

        /// <summary>
        /// Joint types drawn for non-root nodes. <see cref="JointType.Fixed"/> is excluded —
        /// an unactuated creature cannot swim, and the archive would fill with driftwood.
        /// </summary>
        public JointType[] JointTypes { get; set; } =
        {
            JointType.Hinge,
            JointType.Twist,
            JointType.HingeTwist,
            JointType.TwistHinge,
            JointType.Universal,
            JointType.Spherical,
        };

        /// <summary>Symmetric joint limit magnitude, in radians.</summary>
        public float MinJointLimit { get; set; } = 0.4f;
        public float MaxJointLimit { get; set; } = 1.4f;

        public static RandomGenomeOptions Default => new RandomGenomeOptions();
    }

    /// <summary>
    /// Builds random genomes for the initial population.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Neurons are drawn from <see cref="NeuronOps.MvpSet"/> with self-only connections —
    /// the pure central pattern generator of DESIGN.md §4.3. That is a
    /// <b>population constraint, not a separate system</b>: nothing here prevents the full
    /// operator set, and no code is discarded when the restriction lifts.
    /// </para>
    /// <para>
    /// Everything this produces satisfies <see cref="Genome.Validate"/>. It says nothing
    /// about whether the genome develops into a <i>good</i> creature — most will be
    /// rubbish, which is the point of having a search.
    /// </para>
    /// </remarks>
    public static class GenomeFactory
    {
        public static Genome Random(Rng rng, RandomGenomeOptions options = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            options = options ?? RandomGenomeOptions.Default;

            int nodeCount = rng.Range(options.MinNodes, options.MaxNodes + 1);
            var genome = new Genome { RootIndex = 0 };

            for (int i = 0; i < nodeCount; i++)
            {
                genome.Nodes.Add(RandomNode(rng, options));
            }

            for (int i = 0; i < nodeCount; i++)
            {
                AddEdges(rng, options, genome, i, nodeCount);
            }

            // A root with no edges develops into a single box, which is not worth simulating.
            if (genome.Nodes[0].Edges.Count == 0 && nodeCount > 1)
            {
                genome.Nodes[0].Edges.Add(
                    RandomEdge(rng, options, rng.Range(1, nodeCount), rng.Range(6)));
            }

            return genome;
        }

        private static MorphNode RandomNode(Rng rng, RandomGenomeOptions options)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(
                    rng.Range(options.MinHalfExtent, options.MaxHalfExtent),
                    rng.Range(options.MinHalfExtent, options.MaxHalfExtent),
                    rng.Range(options.MinHalfExtent, options.MaxHalfExtent)),
                JointType = rng.Pick(options.JointTypes),
                RecursiveLimit = rng.Range(options.MinRecursiveLimit, options.MaxRecursiveLimit + 1),
            };

            int dof = node.JointType.DofCount();
            var limits = new Float2[dof];
            for (int d = 0; d < dof; d++)
            {
                float magnitude = rng.Range(options.MinJointLimit, options.MaxJointLimit);
                limits[d] = new Float2(-magnitude, magnitude);
            }
            node.JointLimits = limits;
            node.Neurons = RandomNeurons(rng, options);

            return node;
        }

        private static NeuronDef[] RandomNeurons(Rng rng, RandomGenomeOptions options)
        {
            int count = rng.Range(options.MinNeuronsPerNode, options.MaxNeuronsPerNode + 1);
            var neurons = new NeuronDef[count];

            for (int i = 0; i < count; i++)
            {
                neurons[i] = new NeuronDef
                {
                    Op = rng.Pick(NeuronOps.MvpSet),
                    Frequency = rng.Range(options.MinOscillatorHz, options.MaxOscillatorHz),
                    Phase = rng.Range(0f, 6.2831853f),
                    Amplitude = rng.Range(0.5f, 1f),
                    Bias = rng.Gaussian(0f, 0.1f),
                };
            }

            // Wire inputs only after every neuron exists, so a SameNode reference is always
            // in range — Genome.Validate rejects one that is not.
            for (int i = 0; i < count; i++)
            {
                int arity = neurons[i].Op.Arity();
                if (arity == 0) continue;

                var inputs = new NeuronInput[arity];
                for (int a = 0; a < arity; a++)
                {
                    inputs[a] = rng.Chance(0.5f)
                        ? NeuronInput.FromSensor(SensorChannel.JointAngle, 0, rng.Gaussian(0f, 1f))
                        : NeuronInput.FromNeuron(NeuronInputKind.SameNode, rng.Range(count), rng.Gaussian(0f, 1f));
                }
                neurons[i].Inputs = inputs;
            }

            return neurons;
        }

        private static void AddEdges(Rng rng, RandomGenomeOptions options, Genome genome, int nodeIndex, int nodeCount)
        {
            MorphNode node = genome.Nodes[nodeIndex];
            int edgeCount = rng.Range(0, options.MaxEdgesPerNode + 1);

            // Faces are handed out without replacement. Two edges sharing a face put two
            // children in the same place, which is the other way a box ends up inside a box.
            var faces = FreeFaces(rng);

            for (int e = 0; e < edgeCount; e++)
            {
                node.Edges.Add(RandomEdge(rng, options, rng.Range(nodeCount), TakeFace(faces, rng)));
            }

            // A terminal edge is only reachable if some non-terminal edge exists to exhaust.
            if (node.Edges.Count > 0 && rng.Chance(options.TerminalChance))
            {
                MorphEdge terminal = RandomEdge(rng, options, rng.Range(nodeCount), TakeFace(faces, rng));
                terminal.TerminalOnly = true;
                node.Edges.Add(terminal);
            }
        }

        private static List<int> FreeFaces(Rng rng)
        {
            // Encoded as axis * 2 + (0 for +, 1 for -).
            var faces = new List<int> { 0, 1, 2, 3, 4, 5 };
            for (int i = faces.Count - 1; i > 0; i--)
            {
                int j = rng.Range(i + 1);
                int swap = faces[i];
                faces[i] = faces[j];
                faces[j] = swap;
            }
            return faces;
        }

        private static int TakeFace(List<int> faces, Rng rng)
        {
            if (faces.Count == 0) return rng.Range(6);

            int face = faces[faces.Count - 1];
            faces.RemoveAt(faces.Count - 1);
            return face;
        }

        private static MorphEdge RandomEdge(Rng rng, RandomGenomeOptions options, int child, int face)
        {
            // Attach a face of the child to a face of the parent, so the two boxes meet
            // surface to surface. Anchors drawn anywhere inside the box mostly produce parts
            // buried in their parent.
            int axis = face >> 1;
            float sign = (face & 1) == 0 ? 1f : -1f;

            Float3 parentAnchor = AxisVector(axis, sign);
            Float3 childAnchor = AxisVector(axis, -sign);

            float scale = rng.Range(options.MinEdgeScale, options.MaxEdgeScale);

            var edge = new MorphEdge
            {
                Child = child,
                ParentAnchor = parentAnchor,
                ChildAnchor = childAnchor,
                Orientation = rng.Chance(options.RotateChance)
                    ? Quat.FromAxisAngle(
                        rng.NextFloat3(-1f, 1f),
                        rng.Range(-options.MaxEdgeTiltDegrees, options.MaxEdgeTiltDegrees) * 0.01745329f)
                    : Quat.Identity,
                Scale = new Float3(scale, scale, scale),
            };

            if (rng.Chance(options.ReflectChance))
            {
                // The reflection axis must be the axis the child is attached along, and this
                // is not a stylistic choice.
                //
                // Mirroring about an axis moves a point only if the point has a component on
                // that axis. A child attached to the parent's +Y face sits at roughly
                // (0, d, 0); mirroring it about X maps that to (0, d, 0) — the same place. The
                // "mirrored copy" is then exactly coincident with the original, which is a box
                // inside a box, and worse, it is invisible as a bug because the creature still
                // has the right number of parts in plausible positions.
                //
                // Attached along X and mirrored about X, the copies land at +d and -d: an
                // actual bilateral pair.
                edge.Reflect = Bool3.None.WithAxis(axis, true);
            }

            return edge;
        }

        private static Float3 AxisVector(int axis, float sign)
        {
            switch (axis)
            {
                case 0: return new Float3(sign, 0f, 0f);
                case 1: return new Float3(0f, sign, 0f);
                default: return new Float3(0f, 0f, sign);
            }
        }

        /// <summary>
        /// Random genomes that also develop into at least <paramref name="minParts"/> parts,
        /// retried up to <paramref name="maxAttempts"/> times.
        /// </summary>
        /// <remarks>
        /// A genome is legal without being worth simulating — a single unactuated box is
        /// both. This exists so an initial population is not half driftwood. It returns the
        /// last attempt rather than throwing when it cannot meet the bar, so callers get a
        /// creature either way; check <see cref="Phenotype.PartCount"/> if that matters.
        /// </remarks>
        public static Genome RandomViable(
            Rng rng,
            RandomGenomeOptions options = null,
            DevelopmentLimits limits = null,
            int minParts = 3,
            int maxAttempts = 32,
            int maxBuriedPairs = 0,
            float maxUnjointedOverlap = 0.005f)
        {
            Genome last = null;
            Genome bestSoFar = null;
            int fewestBuried = int.MaxValue;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                last = Random(rng, options);
                Phenotype developed = Developer.Develop(last, limits);

                if (developed.PartCount < minParts || developed.TotalDof == 0) continue;

                // Unjointed overlap is rejected outright, and not only because two solids
                // passing through each other looks impossible. With self-collision enabled,
                // parts born inside each other are pushed apart by the solver's depenetration
                // pass, and that is momentum arriving from nowhere — a free launch at t=0 that
                // a search would learn to build creatures around (DESIGN.md §11.2).
                //
                // Overlap between a part and its own parent stays permitted (§4.2): PhysX
                // articulations do not collide directly-jointed links, so it costs nothing.
                float unjointed = PhenotypeGeometry.MeasureOverlap(developed, samplesPerAxis: 4).UnjointedFraction;

                int buried = PhenotypeGeometry.BuriedPartPairs(developed);
                if (buried <= maxBuriedPairs && unjointed <= maxUnjointedOverlap) return last;

                // Structural rules cannot rule burial out — a node with edges on opposite
                // faces places a child exactly where its own parent already sits, and only
                // developing it reveals that. So the filter is on the grown creature, and it
                // keeps the least-bad candidate rather than failing.
                int badness = buried + (unjointed > maxUnjointedOverlap ? 1 : 0);
                if (badness < fewestBuried)
                {
                    fewestBuried = badness;
                    bestSoFar = last;
                }
            }

            return bestSoFar ?? last;
        }
    }
}
