using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Produces a varied copy of a genome — DESIGN.md §4.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic in its inputs, and that is worth more than it looks.</b> An offspring is
    /// entirely determined by <c>(parent, rng seed, rates)</c>, so a birth can be recorded as a
    /// parent reference plus a seed — a couple of dozen bytes — instead of a whole genome, which
    /// measures about 5 KB. At the working estimate of 40,000 births an hour that is the
    /// difference between 200 MB and a few megabytes. §9's diff-and-keyframe storage rests on
    /// this method having no hidden state.
    /// </para>
    /// <para>
    /// It also means the property is load-bearing rather than incidental, which is why
    /// <see cref="CodeVersion"/> exists: replaying a stored seed reproduces the original
    /// offspring only while this code behaves as it did when the seed was written. Change an
    /// operator and old chains reconstruct into creatures that are plausible, valid, and not the
    /// ones that lived. Recording the version turns that from invisible into detectable, and is
    /// the reason keyframes are stored at all.
    /// </para>
    /// <para>
    /// <b>Every result is valid or the method throws.</b> Mutation is where invariants go to
    /// die: a joint type changed without its limit array, a cell type changed from link to
    /// stomach while keeping its hinge, an edge left pointing at a removed node. Each operator
    /// repairs what it disturbs, and <see cref="Mutate"/> asserts the result validates before
    /// returning it — an invalid genome escaping here would develop, run, and be measured.
    /// </para>
    /// </remarks>
    public static class Mutator
    {
        /// <summary>
        /// Bumped whenever an operator changes in a way that makes a stored seed reproduce a
        /// different offspring. Recorded per birth; see the class remarks.
        /// </summary>
        public const int CodeVersion = 1;

        public static Genome Mutate(
            Genome parent, Rng rng, MutationRates rates = null, CellTypeRegistry cellTypes = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            rates = rates ?? MutationRates.Default;
            cellTypes = cellTypes ?? CellTypeRegistry.Standard;

            Genome child = parent.Clone();

            MutateReproduction(child, rng, rates);

            for (int n = 0; n < child.Nodes.Count; n++)
            {
                MutateNode(child, child.Nodes[n], rng, rates, cellTypes);
            }

            if (rng.Chance(rates.AddNodeChance) && child.Nodes.Count < rates.MaxNodes)
            {
                AddNode(child, rng, rates, cellTypes);
            }

            PruneVanishedNodes(child, rates);

            MutateNeuronSet(child.GlobalBrain, null, child, rng, rates, out NeuronDef[] brain);
            child.GlobalBrain = brain;

            IReadOnlyList<string> issues = child.Validate(cellTypes);
            if (issues.Count > 0)
            {
                throw new InvalidOperationException(
                    "Mutation produced an invalid genome, which means an operator disturbed an " +
                    "invariant it did not repair:\n  " + string.Join("\n  ", issues));
            }

            return child;
        }

        // ---------------------------------------------------------------- reproduction

        private static void MutateReproduction(Genome g, Rng rng, MutationRates rates)
        {
            ReproductionTraits r = g.Reproduction;

            if (rng.Chance(rates.BroodSizeChance))
            {
                r.BroodSize = Math.Max(1, Math.Min(rates.MaxBroodSize,
                    r.BroodSize + (rng.Chance(0.5f) ? 1 : -1)));
            }

            if (rng.Chance(rates.EndowmentChance))
            {
                r.OffspringEndowment = PerturbPositive(r.OffspringEndowment, rng, rates);
            }

            g.Reproduction = r;
        }

        // ---------------------------------------------------------------- nodes

        private static void MutateNode(
            Genome g, MorphNode node, Rng rng, MutationRates rates, CellTypeRegistry cellTypes)
        {
            node.Dimensions = new Float3(
                PerturbPositive(node.Dimensions.X, rng, rates),
                PerturbPositive(node.Dimensions.Y, rng, rates),
                PerturbPositive(node.Dimensions.Z, rng, rates));

            if (rng.Chance(rates.RecursiveLimitChance))
            {
                node.RecursiveLimit = Math.Max(0, node.RecursiveLimit + (rng.Chance(0.5f) ? 1 : -1));
            }

            if (rng.Chance(rates.ShapeChance)) node.ShapeId = PickOther(
                PartShapeRegistry.Standard, node.ShapeId, rng);

            if (rng.Chance(rates.CellTypeChance)) ChangeCellType(node, rng, cellTypes);

            // After a possible cell-type change, because whether a joint is even legal here
            // depends on what the part is now made of.
            if (rng.Chance(rates.JointTypeChance)) ChangeJointType(node, rng, cellTypes);

            if (node.JointType.DofCount() > 0)
            {
                node.Power = PerturbPositive(node.Power, rng, rates);

                for (int i = 0; i < node.JointLimits.Length; i++)
                {
                    float magnitude = PerturbPositive(
                        Math.Max(1e-3f, Math.Abs(node.JointLimits[i].Y)), rng, rates);
                    node.JointLimits[i] = new Float2(-magnitude, magnitude);
                }
            }

            for (int e = node.Edges.Count - 1; e >= 0; e--)
            {
                if (rng.Chance(rates.RemoveEdgeChance)) { node.Edges.RemoveAt(e); continue; }
                MutateEdge(g, node.Edges[e], rng, rates);
            }

            if (rng.Chance(rates.AddEdgeChance) && g.Nodes.Count > 0)
            {
                node.Edges.Add(RandomEdgeTo(g, rng));
            }

            MutateNeuronSet(node.Neurons, node, g, rng, rates, out NeuronDef[] neurons);
            node.Neurons = neurons;
        }

        /// <summary>Picks a registered shape other than the current one.</summary>
        /// <remarks>
        /// Excluding the current shape means the operator always does something when it fires.
        /// Allowing it to redraw the same value would make the effective rate depend on how many
        /// shapes are registered, so adding a fourth shape would quietly change how often the
        /// other three mutate.
        /// </remarks>
        private static string PickOther(PartShapeRegistry shapes, string current, Rng rng)
        {
            var others = new List<string>();
            foreach (string id in shapes.Ids()) if (id != current) others.Add(id);

            return others.Count == 0 ? current : others[rng.Range(others.Count)];
        }

        /// <remarks>
        /// The awkward operator, because cell type and joint are coupled: only a link may move
        /// (§5A.1). Turning a link into a stomach must therefore also weld its joint shut and
        /// surrender its capacity — a real cost, and the right one. It is what makes this a
        /// genuine trade rather than a free relabelling.
        /// </remarks>
        private static void ChangeCellType(MorphNode node, Rng rng, CellTypeRegistry cellTypes)
        {
            var ids = new List<string>();
            foreach (string id in cellTypes.Ids()) if (id != node.CellTypeId) ids.Add(id);
            if (ids.Count == 0) return;

            node.CellTypeId = ids[rng.Range(ids.Count)];

            if (!cellTypes.Resolve(node.CellTypeId).AllowsJoint && node.JointType.DofCount() > 0)
            {
                node.JointType = JointType.Fixed;
                node.JointLimits = Array.Empty<Float2>();
                node.Power = 0f;
            }
        }

        private static void ChangeJointType(MorphNode node, Rng rng, CellTypeRegistry cellTypes)
        {
            if (!cellTypes.Resolve(node.CellTypeId).AllowsJoint) return;

            var choices = new[]
            {
                JointType.Fixed, JointType.Hinge, JointType.Twist,
                JointType.HingeTwist, JointType.TwistHinge,
                JointType.Universal, JointType.Spherical,
            };

            node.JointType = choices[rng.Range(choices.Length)];

            int dof = node.JointType.DofCount();
            var limits = new Float2[dof];
            for (int i = 0; i < dof; i++)
            {
                float magnitude = i < node.JointLimits.Length
                    ? Math.Max(1e-3f, Math.Abs(node.JointLimits[i].Y))
                    : rng.Range(0.4f, 1.4f);
                limits[i] = new Float2(-magnitude, magnitude);
            }
            node.JointLimits = limits;

            // A joint with no capacity cannot actuate, and one on a part with no joint is
            // charged for nothing. Both are invalid; both are repaired here rather than left
            // for Validate to catch after the fact.
            if (dof > 0 && node.Power <= 0f) node.Power = rng.Range(5f, 120f);
            else if (dof == 0) node.Power = 0f;
        }

        private static void AddNode(
            Genome g, Rng rng, MutationRates rates, CellTypeRegistry cellTypes)
        {
            // A copy of an existing node rather than a fresh random one. A duplicated segment is
            // immediately viable — it is how a limb becomes two limbs — where a random node
            // dropped into a working body plan is almost always noise.
            MorphNode source = g.Nodes[rng.Range(g.Nodes.Count)];
            MorphNode copy = source.Clone();
            copy.Edges.Clear();

            // Born small, just above the size at which a node stops existing. A duplication is
            // then nearly neutral on the birth it happens — it adds a part too small to change
            // much, which grows only if it turns out to be worth something. Arriving at the
            // source's full size made every duplication a large jump, and a large jump in a
            // co-adapted body is almost always worse than what it replaced (§2). This is also
            // the half of the mechanism that makes shrinking-to-extinction symmetrical: things
            // enter small and leave small, so neither direction is a discontinuity.
            copy.Dimensions = new Float3(rates.NewNodeHalfExtent);

            g.Nodes.Add(copy);
            g.Nodes[rng.Range(g.Nodes.Count - 1)].Edges.Add(
                RandomEdgeTo(g, rng, child: g.Nodes.Count - 1));
        }

        /// <summary>
        /// Removes every node that has shrunk below <see cref="MutationRates.NodeExtinctionHalfExtent"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the whole removal mechanism — there is no removal rate. A node disappears only
        /// by shrinking to nothing, and shrinking is something selection can prevent: a node
        /// doing useful work is held large, one doing nothing drifts and falls out. Removal is
        /// therefore filtered by selection instead of blind, which a per-node deletion chance
        /// could never be.
        /// </para>
        /// <para>
        /// The root is exempt. Not because losing it would be invalid — <see cref="RemoveNodeAt"/>
        /// would refuse anyway — but because a creature whose root shrank away is not a smaller
        /// creature, it is no creature, and that is a different event from a limb being lost.
        /// </para>
        /// </remarks>
        private static void PruneVanishedNodes(Genome g, MutationRates rates)
        {
            for (int i = g.Nodes.Count - 1; i >= 0; i--)
            {
                if (g.Nodes.Count <= 1 || i == g.RootIndex) continue;

                Float3 d = g.Nodes[i].Dimensions;
                if ((d.X + d.Y + d.Z) / 3f >= rates.NodeExtinctionHalfExtent) continue;

                RemoveNodeAt(g, i);
            }
        }

        /// <remarks>
        /// Removing a node renumbers everything after it, so every edge in the genome has to be
        /// remapped and every edge into the removed node dropped. Getting this wrong produces a
        /// genome that still validates — indices remain in range — while pointing at the wrong
        /// parts, which is why <c>RemovingANodeRepointsEveryEdge</c> checks the survivors rather
        /// than just that the result is valid.
        /// </remarks>
        private static void RemoveNodeAt(Genome g, int victim)
        {
            if (g.Nodes.Count <= 1 || victim == g.RootIndex) return;

            g.Nodes.RemoveAt(victim);
            if (g.RootIndex > victim) g.RootIndex--;

            foreach (MorphNode node in g.Nodes)
            {
                for (int e = node.Edges.Count - 1; e >= 0; e--)
                {
                    int child = node.Edges[e].Child;
                    if (child == victim) node.Edges.RemoveAt(e);
                    else if (child > victim) node.Edges[e].Child = child - 1;
                }
            }
        }

        // ---------------------------------------------------------------- edges

        private static void MutateEdge(Genome g, MorphEdge edge, Rng rng, MutationRates rates)
        {
            edge.ParentAnchor = PerturbVector(edge.ParentAnchor, rng, rates);
            edge.ChildAnchor = PerturbVector(edge.ChildAnchor, rng, rates);

            edge.Scale = new Float3(
                PerturbPositive(edge.Scale.X, rng, rates),
                PerturbPositive(edge.Scale.Y, rng, rates),
                PerturbPositive(edge.Scale.Z, rng, rates));

            if (rng.Chance(rates.ScalarChance)) edge.Orientation = rng.NextRotation();

            if (rng.Chance(rates.FlagChance))
            {
                edge.Reflect = rng.Chance(0.5f)
                    ? Bool3.None.WithAxis(rng.Range(3), true)
                    : Bool3.None;
            }

            if (rng.Chance(rates.FlagChance)) edge.TerminalOnly = !edge.TerminalOnly;

            if (rng.Chance(rates.ScalarChance)) edge.Child = rng.Range(g.Nodes.Count);
        }

        private static MorphEdge RandomEdgeTo(Genome g, Rng rng, int child = -1)
        {
            int axis = rng.Range(3);
            float sign = rng.Chance(0.5f) ? 1f : -1f;

            return new MorphEdge
            {
                Child = child >= 0 ? child : rng.Range(g.Nodes.Count),
                ParentAnchor = AxisVector(axis, sign),
                ChildAnchor = AxisVector(axis, -sign),
                Scale = new Float3(rng.Range(0.6f, 1f)),
                Orientation = Quat.Identity,
            };
        }

        private static Float3 AxisVector(int axis, float sign) =>
            axis == 0 ? new Float3(sign, 0f, 0f)
          : axis == 1 ? new Float3(0f, sign, 0f)
          : new Float3(0f, 0f, sign);

        // ---------------------------------------------------------------- neurons

        private static void MutateNeuronSet(
            NeuronDef[] neurons, MorphNode owner, Genome g, Rng rng, MutationRates rates,
            out NeuronDef[] result)
        {
            var list = new List<NeuronDef>(neurons);

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (rng.Chance(rates.RemoveNeuronChance)) { list.RemoveAt(i); continue; }
                MutateNeuron(list[i], rng, rates);
            }

            if (rng.Chance(rates.AddNeuronChance))
            {
                list.Add(new NeuronDef
                {
                    Op = RandomOp(rng),
                    Frequency = rng.Range(0.3f, 2.5f),
                    Phase = rng.Range(0f, 6.2831853f),
                    Amplitude = rng.Range(0.5f, 1f),
                    Inputs = new[] { NeuronInput.FromConstant(rng.Gaussian(0f, 1f)) },
                });
            }

            // Removing a neuron invalidates every input that referenced one after it. Repaired
            // by clamping into range rather than by dropping the input, so a rewired connection
            // survives as a connection — losing it silently would make neuron removal quietly
            // destructive far beyond the neuron removed.
            result = list.ToArray();
            RepairInputs(result, owner, g, rng);
        }

        private static void MutateNeuron(NeuronDef neuron, Rng rng, MutationRates rates)
        {
            neuron.Frequency = PerturbPositive(neuron.Frequency, rng, rates);
            neuron.Amplitude = Perturb(neuron.Amplitude, rng, rates);
            neuron.Phase = Perturb(neuron.Phase, rng, rates);
            neuron.Bias = Perturb(neuron.Bias, rng, rates);

            if (rng.Chance(rates.NeuronOpChance)) neuron.Op = RandomOp(rng);

            for (int i = 0; i < neuron.Inputs.Length; i++)
            {
                NeuronInput input = neuron.Inputs[i];

                float weight = rng.Chance(rates.ScalarChance)
                    ? input.Weight + rng.Gaussian(0f, rates.ScalarStdDev)
                    : input.Weight;

                float constant = rng.Chance(rates.ScalarChance)
                    ? input.Constant + rng.Gaussian(0f, rates.ScalarStdDev)
                    : input.Constant;

                neuron.Inputs[i] = new NeuronInput(
                    input.Kind, input.Index, input.Channel, constant, weight);
            }
        }

        private static void RepairInputs(NeuronDef[] neurons, MorphNode owner, Genome g, Rng rng)
        {
            int local = owner != null ? owner.Neurons.Length : g.GlobalBrain.Length;
            local = neurons.Length;

            for (int n = 0; n < neurons.Length; n++)
            {
                NeuronInput[] inputs = neurons[n].Inputs;

                for (int i = 0; i < inputs.Length; i++)
                {
                    NeuronInput input = inputs[i];
                    int index = input.Index;
                    NeuronInputKind kind = input.Kind;

                    if (kind == NeuronInputKind.Sensor && owner == null)
                    {
                        // A global neuron owns no part, so it has no sensors to read (§4.3).
                        kind = NeuronInputKind.Constant;
                        index = 0;
                    }
                    else if (kind == NeuronInputKind.SameNode)
                    {
                        index = local == 0 ? 0 : Math.Min(index, local - 1);
                        if (local == 0) kind = NeuronInputKind.Constant;
                    }
                    else if (kind == NeuronInputKind.GlobalBrain)
                    {
                        int count = g.GlobalBrain.Length;
                        if (count == 0) { kind = NeuronInputKind.Constant; index = 0; }
                        else index = Math.Min(index, count - 1);
                    }

                    if (index < 0) index = 0;

                    inputs[i] = new NeuronInput(
                        kind, index, input.Channel, input.Constant, input.Weight);
                }
            }
        }

        private static NeuronOp RandomOp(Rng rng)
        {
            Array all = Enum.GetValues(typeof(NeuronOp));
            return (NeuronOp)all.GetValue(rng.Range(all.Length));
        }

        // ---------------------------------------------------------------- scalars

        private static float Perturb(float value, Rng rng, MutationRates rates)
        {
            if (!rng.Chance(rates.ScalarChance)) return value;

            float scale = Math.Max(1e-4f, Math.Abs(value));
            return value + rng.Gaussian(0f, rates.ScalarStdDev * scale);
        }

        /// <remarks>
        /// Clamped above zero because every scalar this is used on — a half-extent, a link's
        /// capacity, an offspring's endowment — is meaningless or invalid at or below it, and a
        /// Gaussian step has no lower bound.
        /// </remarks>
        private static float PerturbPositive(float value, Rng rng, MutationRates rates)
        {
            float mutated = Perturb(value, rng, rates);
            return mutated > 1e-4f ? mutated : 1e-4f;
        }

        private static Float3 PerturbVector(Float3 v, Rng rng, MutationRates rates) =>
            new Float3(
                Perturb(v.X, rng, rates), Perturb(v.Y, rng, rates), Perturb(v.Z, rng, rates));
    }
}
