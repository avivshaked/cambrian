using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// A creature genotype: a directed, possibly cyclic morphology graph whose nodes carry
    /// their own neurons. Encodes body and brain together — DESIGN.md §4.
    /// </summary>
    /// <remarks>
    /// This is an <b>indirect</b> encoding, not a direct one. [L21 §4.2, p.8] classifies
    /// Sims as <i>"an indirect representation that supports recursive structures."</i>
    /// Recursion, reflection and cumulative subtree transforms are generative machinery:
    /// a small genotype unfolds into a much larger phenotype and regularity comes for free.
    /// Draft 2 of the design called it direct, which was wrong and had knock-on effects on
    /// the CPPN comparison in §12.1.
    /// </remarks>
    public sealed class Genome
    {
        public List<MorphNode> Nodes { get; } = new List<MorphNode>();

        /// <summary>Index into <see cref="Nodes"/> at which development starts.</summary>
        public int RootIndex { get; set; }

        /// <summary>
        /// Neurons belonging to no part, addressable from any node via
        /// <see cref="NeuronInputKind.GlobalBrain"/>.
        /// </summary>
        public NeuronDef[] GlobalBrain { get; set; } = Array.Empty<NeuronDef>();

        public Genome Clone()
        {
            var clone = new Genome
            {
                RootIndex = RootIndex,
                GlobalBrain = new NeuronDef[GlobalBrain.Length],
            };

            for (int i = 0; i < GlobalBrain.Length; i++) clone.GlobalBrain[i] = GlobalBrain[i].Clone();
            for (int i = 0; i < Nodes.Count; i++) clone.Nodes.Add(Nodes[i].Clone());

            return clone;
        }

        /// <summary>
        /// Structural checks that do not depend on development. Returns an empty list for a
        /// well-formed genome.
        /// </summary>
        /// <remarks>
        /// Deliberately does not check part count, depth or volume — those are properties of
        /// the <i>phenotype</i>, cannot be known without developing the genome, and are
        /// handled by <see cref="Developer"/> against <see cref="DevelopmentLimits"/>.
        /// </remarks>
        public IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();

            if (Nodes.Count == 0)
            {
                issues.Add("Genome has no nodes.");
                return issues;
            }

            if (RootIndex < 0 || RootIndex >= Nodes.Count)
            {
                issues.Add($"RootIndex {RootIndex} is outside [0, {Nodes.Count - 1}].");
            }

            for (int n = 0; n < Nodes.Count; n++)
            {
                MorphNode node = Nodes[n];

                if (!node.Dimensions.IsFinite)
                {
                    issues.Add($"Node {n}: dimensions are not finite.");
                }
                else if (node.Dimensions.X <= 0f || node.Dimensions.Y <= 0f || node.Dimensions.Z <= 0f)
                {
                    issues.Add($"Node {n}: dimensions must be positive half-extents, got {node.Dimensions}.");
                }

                if (node.RecursiveLimit < 0)
                {
                    issues.Add($"Node {n}: RecursiveLimit {node.RecursiveLimit} is negative.");
                }

                int dof = node.JointType.DofCount();
                if (node.JointLimits.Length != dof)
                {
                    issues.Add($"Node {n}: {node.JointType} has {dof} DOF but {node.JointLimits.Length} joint limits.");
                }

                for (int i = 0; i < node.JointLimits.Length; i++)
                {
                    if (!node.JointLimits[i].IsOrderedRange)
                    {
                        issues.Add($"Node {n}: joint limit {i} is inverted: {node.JointLimits[i]}.");
                    }
                }

                for (int e = 0; e < node.Edges.Count; e++)
                {
                    MorphEdge edge = node.Edges[e];
                    if (edge.Child < 0 || edge.Child >= Nodes.Count)
                    {
                        issues.Add($"Node {n} edge {e}: child {edge.Child} is outside [0, {Nodes.Count - 1}].");
                    }

                    if (!edge.Scale.IsFinite || edge.Scale.X == 0f || edge.Scale.Y == 0f || edge.Scale.Z == 0f)
                    {
                        issues.Add($"Node {n} edge {e}: scale {edge.Scale} is degenerate or not finite.");
                    }

                    if (!edge.Orientation.IsFinite)
                    {
                        issues.Add($"Node {n} edge {e}: orientation is not finite.");
                    }
                }

                ValidateNeurons(node.Neurons, node, n, issues);
            }

            ValidateNeurons(GlobalBrain, null, -1, issues);

            return issues;
        }

        private void ValidateNeurons(NeuronDef[] neurons, MorphNode owner, int nodeIndex, List<string> issues)
        {
            string where = nodeIndex < 0 ? "Global brain" : $"Node {nodeIndex}";

            for (int i = 0; i < neurons.Length; i++)
            {
                NeuronDef neuron = neurons[i];

                foreach (NeuronInput input in neuron.Inputs)
                {
                    switch (input.Kind)
                    {
                        case NeuronInputKind.SameNode:
                            int localCount = owner != null ? owner.Neurons.Length : GlobalBrain.Length;
                            if (input.Index < 0 || input.Index >= localCount)
                            {
                                issues.Add($"{where} neuron {i}: SameNode input {input.Index} has no such neuron.");
                            }
                            break;

                        case NeuronInputKind.GlobalBrain:
                            if (input.Index < 0 || input.Index >= GlobalBrain.Length)
                            {
                                issues.Add($"{where} neuron {i}: GlobalBrain input {input.Index} has no such neuron.");
                            }
                            break;

                        case NeuronInputKind.ParentNode:
                        case NeuronInputKind.ChildNode:
                            // Resolved during development, and legitimately unresolvable at the
                            // root or a leaf — those read zero rather than being invalid.
                            if (input.Index < 0)
                            {
                                issues.Add($"{where} neuron {i}: {input.Kind} input index is negative.");
                            }
                            break;

                        case NeuronInputKind.Sensor:
                            if (input.Index < 0)
                            {
                                issues.Add($"{where} neuron {i}: sensor index is negative.");
                            }
                            if (owner == null)
                            {
                                issues.Add($"{where} neuron {i}: global neurons own no part and cannot read sensors.");
                            }
                            break;
                    }
                }
            }
        }

        public override string ToString() => $"Genome({Nodes.Count} nodes, root {RootIndex})";
    }
}
