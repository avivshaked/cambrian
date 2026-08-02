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

        /// <summary>Chance an edge applies a random rotation rather than attaching square-on.</summary>
        public float RotateChance { get; set; } = 0.4f;

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
                genome.Nodes[0].Edges.Add(RandomEdge(rng, options, child: rng.Range(1, nodeCount)));
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

            for (int e = 0; e < edgeCount; e++)
            {
                node.Edges.Add(RandomEdge(rng, options, child: rng.Range(nodeCount)));
            }

            // A terminal edge is only reachable if some non-terminal edge exists to exhaust.
            if (node.Edges.Count > 0 && rng.Chance(options.TerminalChance))
            {
                MorphEdge terminal = RandomEdge(rng, options, child: rng.Range(nodeCount));
                terminal.TerminalOnly = true;
                node.Edges.Add(terminal);
            }
        }

        private static MorphEdge RandomEdge(Rng rng, RandomGenomeOptions options, int child)
        {
            // Attach a face of the child to a face of the parent: pick an axis and a sign,
            // and put the child's opposing face against it. Random anchors inside the box
            // mostly produce parts buried in their parent.
            int axis = rng.Range(3);
            float sign = rng.Chance(0.5f) ? 1f : -1f;

            Float3 parentAnchor = AxisVector(axis, sign);
            Float3 childAnchor = AxisVector(axis, -sign);

            float scale = rng.Range(options.MinEdgeScale, options.MaxEdgeScale);

            var edge = new MorphEdge
            {
                Child = child,
                ParentAnchor = parentAnchor,
                ChildAnchor = childAnchor,
                Orientation = rng.Chance(options.RotateChance) ? rng.NextRotation() : Quat.Identity,
                Scale = new Float3(scale, scale, scale),
            };

            if (rng.Chance(options.ReflectChance))
            {
                edge.Reflect = Bool3.None.WithAxis(rng.Range(3), true);
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
            int maxAttempts = 32)
        {
            Genome last = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                last = Random(rng, options);
                Phenotype developed = Developer.Develop(last, limits);
                if (developed.PartCount >= minParts && developed.TotalDof > 0) return last;
            }
            return last;
        }
    }
}
