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
    /// entirely determined by <c>(parent, rng seed, rates, genome options)</c>, so a birth can be
    /// recorded as a
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
        /// <remarks>
        /// 3 — fable-propose-growth.md (2026-09-08). <c>MutateReproduction</c> now perturbs the
        /// birth investment where it perturbed the offspring endowment, and draws once more for
        /// the adult scale, so every seed after this operator reproduces a different child from
        /// the one it reproduced before it.
        /// <para>
        /// 4 — the owner's ruling of 2026-09-24 on round 47's stomachs: a cell type is never
        /// changed in place. What <c>CellTypeChance</c> used to fire on a node now buds a small
        /// copy of it of another type (<see cref="Bud"/>), drawn after the node pass, so every
        /// seed whose birth fired the rate reproduces a different child.
        /// </para>
        /// </remarks>
        public const int CodeVersion = 4;

        /// <param name="log">
        /// Filled, when it is not null, with what this birth gained by budding: each bud's cell
        /// type and its index in the returned genome. Cleared first. The lineage row reads it
        /// (<c>World.Conceive</c>); nothing about the mutation depends on it.
        /// </param>
        public static Genome Mutate(
            Genome parent, Rng rng, MutationRates rates = null, CellTypeRegistry cellTypes = null,
            RandomGenomeOptions genome = null, SensorChannel[] sensorPool = null,
            bool buoyancyOffsetPriced = false, MutationLog log = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            rates = rates ?? MutationRates.Default;
            cellTypes = cellTypes ?? CellTypeRegistry.Standard;
            genome = genome ?? RandomGenomeOptions.Default;
            log?.Clear();

            Genome child = parent.Clone();

            MutateReproduction(child, rng, rates);

            // D081 (2026-09-07): the global brain is retired. A child is born without one, and
            // the array is emptied here, FIRST, for the reason its mutation used to run first: a
            // node's inputs are made legal against whatever `child.GlobalBrain` holds when that
            // node is repaired, so RepairInputs below turns every GlobalBrain reference into a
            // constant (its globalCount reads 0) and the genome stays valid. A stored genome
            // carrying global neurons still loads and validates; its neurons are no longer
            // stepped (Brain.For), and none of its descendants carries them.
            //
            // Until 2026-09-07 the set was mutated like a node's. It was legal, mutable, stepped
            // and billed by nothing (Metabolism prices neurons per part), and in the record three
            // genomes in five carried one or two constant-input global neurons that almost no
            // local neuron read (D019's note). D019's own argument is that thinking must have a
            // location, and a placeless brain undercut the one thing it set up.
            child.GlobalBrain = Array.Empty<NeuronDef>();

            // The owner's ruling of 2026-09-24: the per-node cell-type draw is rolled here, in the
            // node's own place in the stream, and answered by a bud after the pass. Rolled inline
            // so the rate stays a per-node rate and the expected number of type events per birth is
            // what CellTypeChance always meant; answered afterwards so a bud is neither mutated on
            // the birth it arrives nor shifts the indices the pass is walking.
            List<int> budSources = null;
            int nodesBeforeBuds = child.Nodes.Count;

            for (int n = 0; n < nodesBeforeBuds; n++)
            {
                if (MutateNode(
                        child, child.Nodes[n], rng, rates, cellTypes, genome, sensorPool,
                        buoyancyOffsetPriced))
                {
                    (budSources ??= new List<int>()).Add(n);
                }
            }

            List<MorphNode> buds = null;
            if (budSources != null)
            {
                List<int> hosts = EnterableNodes(child);

                foreach (int source in budSources)
                {
                    if (child.Nodes.Count >= rates.MaxNodes) break;

                    MorphNode bud = Bud(child, source, hosts, rng, rates, cellTypes, genome);
                    if (bud != null) (buds ??= new List<MorphNode>()).Add(bud);
                }
            }

            if (rng.Chance(rates.AddNodeChance) && child.Nodes.Count < rates.MaxNodes)
            {
                AddNode(child, rng, rates, cellTypes);
            }

            PruneVanishedNodes(child, rates);

            // After the prune, which renumbers: the log names each bud by where it now stands. A
            // bud is born above the extinction size, so it survives the prune unless a launcher
            // set NewNodeHalfExtent under NodeExtinctionHalfExtent, and then it is not logged.
            if (log != null && buds != null)
            {
                foreach (MorphNode bud in buds)
                {
                    int at = child.Nodes.IndexOf(bud);
                    if (at < 0) continue;
                    log.BudNodes.Add(at);
                    log.BudCellTypes.Add(bud.CellTypeId);
                }
            }

            IReadOnlyList<string> issues = child.Validate(cellTypes, buoyancyOffsetPriced);
            if (issues.Count > 0)
            {
                throw new InvalidOperationException(
                    "Mutation produced an invalid genome, which means an operator disturbed an " +
                    "invariant it did not repair:\n  " + string.Join("\n  ", issues));
            }

            return child;
        }

        // ---------------------------------------------------------------- reproduction

        /// <remarks>
        /// The three dials of fable-propose-growth.md (2026-09-08) — the litter, the share of the
        /// parent's body that buys it, and how big the child grows up to be — and D098's fourth,
        /// how much the parent keeps back. Each moves by a graded step under its own rate, because
        /// the whole point of them is that they are dials and not switches — this is the first
        /// place in the world where selection can climb a slope rather than jump a gap.
        ///
        /// <b>The adult scale is mutated here rather than in <see cref="MutateNode"/>, and that
        /// is what makes it worth having.</b> It is one number per genome, so it walks at one
        /// rate whatever the body plan holds; a size encoded in the nodes would be walked once
        /// per node, and a lineage could not get bigger without also getting a different shape.
        /// </remarks>
        private static void MutateReproduction(Genome g, Rng rng, MutationRates rates)
        {
            ReproductionTraits r = g.Reproduction;

            if (rng.Chance(rates.BroodSizeChance))
            {
                r.BroodSize = Math.Max(1, Math.Min(rates.MaxBroodSize,
                    r.BroodSize + (rng.Chance(0.5f) ? 1 : -1)));
            }

            if (rng.Chance(rates.InvestmentChance))
            {
                r.BirthInvestment = Step(r.BirthInvestment, rng, rates);
            }

            // D098's fourth dial, stepped exactly as the investment is. No ceiling: a margin
            // longer than the body's life is not an illegal number, it is a lineage that never
            // breeds, and selection removes that in one generation without anyone having to
            // decide what "too cautious" is. The floor is Step's own 1e-4, which at any standing
            // cost this world produces is well under a millijoule and reads as zero everywhere it
            // is spent — Genome.Validate admits a true zero, but a lineage that walks down to the
            // floor has arrived at the same place.
            if (rng.Chance(rates.MarginChance))
            {
                r.ReserveMargin = Step(r.ReserveMargin, rng, rates);
            }

            // The ruling of 2026-09-24. Both tested against zero before anything is rolled, so a
            // world that leaves them at their defaults takes exactly the draws every recorded
            // run took and its children are byte for byte what they were. The share is walked
            // before the mode is flipped, so a lineage flipping into gestation starts from the
            // share it carried; a share Step leaves above 1 is clamped.
            if (rates.GestationShareChance > 0f && rng.Chance(rates.GestationShareChance))
            {
                r.GestationShare = Math.Min(1f, Step(r.GestationShare, rng, rates));
            }

            if (rates.GestationModeChance > 0f && rng.Chance(rates.GestationModeChance))
            {
                r.Mode = r.Mode == ReproductionMode.Lump
                    ? ReproductionMode.Gestation
                    : ReproductionMode.Lump;

                // A lump genome may carry a share of 0 (one built by hand); it cannot gestate
                // on it, so it arrives at the smallest share Step would ever leave.
                if (r.Mode == ReproductionMode.Gestation && !(r.GestationShare > 0f))
                {
                    r.GestationShare = 1e-4f;
                }
            }

            g.Reproduction = r;

            if (rng.Chance(rates.AdultScaleChance))
            {
                g.AdultScale = Step(g.AdultScale, rng, rates);
            }
        }

        // ---------------------------------------------------------------- nodes

        /// <returns>
        /// True when the cell-type rate fired on this node, which asks the caller for a bud
        /// (<see cref="Bud"/>). The node's own type never changes here.
        /// </returns>
        private static bool MutateNode(
            Genome g, MorphNode node, Rng rng, MutationRates rates, CellTypeRegistry cellTypes,
            RandomGenomeOptions genome, SensorChannel[] sensorPool, bool buoyancyOffsetPriced)
        {
            node.Dimensions = new Float3(
                PerturbPositive(node.Dimensions.X, rng, rates),
                PerturbPositive(node.Dimensions.Y, rng, rates),
                PerturbPositive(node.Dimensions.Z, rng, rates));

            if (rng.Chance(rates.RecursiveLimitChance))
            {
                node.RecursiveLimit = Math.Max(0, node.RecursiveLimit + (rng.Chance(0.5f) ? 1 : -1));
            }

            MutateModuleGene(node, rng, rates);

            if (rng.Chance(rates.ShapeChance)) node.ShapeId = PickOther(
                PartShapeRegistry.Standard, node.ShapeId, rng);

            // The owner's ruling of 2026-09-24: a cell type is never changed in place, only added
            // or removed. The draw stays here, in the stream where the in-place change drew it, so
            // the rate is per node as it always was; what it asks for is a bud of another type,
            // which Mutate makes after the node pass. The node itself keeps what it is made of.
            bool budRequested = rng.Chance(rates.CellTypeChance);

            // Under the node's own type's caps, which no longer change on this birth.
            MutateAttributes(node, rng, rates, cellTypes);

            // Whether a joint is legal here depends on what the part is made of.
            if (rng.Chance(rates.JointTypeChance)) ChangeJointType(node, rng, cellTypes, genome);

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

            // D049. Perturbed like any other scalar, and bounded because the ceiling is a solver
            // limit rather than an economic one — BuoyancyCell's charge is what is meant to hold
            // it down, and if it does not, that is a finding rather than something to clamp away.
            if (node.CellTypeId == CellTypeIds.Buoyancy)
            {
                node.Lift = Math.Min(
                    BuoyancyCell.MaxLiftSinkMultiples, PerturbPositive(node.Lift, rng, rates));
            }

            // D111. Nothing is drawn in a world that does not price it, which is every recorded
            // world: the stream a birth consumes is then the one it always was, the module
            // gene's rule. Lift's rate (ScalarChance), on every node whatever it is made of,
            // since any part floats. The step is absolute, a Gaussian of ScalarStdDev on the
            // half-range, where lift's is relative: from the founders' 0 a relative step never
            // leaves the floor, MutateAttributes' argument.
            if (buoyancyOffsetPriced && rng.Chance(rates.ScalarChance))
            {
                node.BuoyancyOffset = Clamp(
                    node.BuoyancyOffset + rng.Gaussian(0f, rates.ScalarStdDev), -1f, 1f);
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

            MutateNeuronSet(node.Neurons, node, g, rng, rates, sensorPool, out NeuronDef[] neurons);
            node.Neurons = neurons;

            return budRequested;
        }

        /// <summary>
        /// D106 item 2's gene: the flip, and the ceiling it needs to be worth anything.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing is drawn at all while the rate is zero</b>, which is the default and is the
        /// whole of what lets this build replay the record: a genome mutated under a zero rate
        /// takes exactly the numbers out of the stream that it took before the gene existed. The
        /// test against zero is therefore load-bearing and not a shortcut — see
        /// <see cref="MutationRates.ModuleGeneMutationChance"/>.
        /// </para>
        /// <para>
        /// <b>The ceiling's step is a whole module, and that is a departure worth naming.</b>
        /// <see cref="PerturbPositive"/> is a relative step — the right shape for a length, a
        /// torque or a rate — and on a count of 1 a 15% step rounds back to 1 every time it
        /// fires: three standard deviations to move the gene once. So the step is taken and then
        /// rounded <i>away</i> from the value it started at, which makes the smallest move one
        /// module in whichever direction the draw went. A gene that cannot move is a gene
        /// selection never sees (<see cref="MutationRates.CellTypeChance"/>'s remarks make the
        /// same argument about a rate that is too small).
        /// </para>
        /// <para>
        /// The floor is the node's own <see cref="MorphNode.RecursiveLimit"/>, because that is
        /// the body the genome describes and the rule may only add to it; the ceiling is
        /// <see cref="DevelopmentLimits.MaxParts"/> as a backstop, since what actually stops a
        /// body from growing is development's own limit and <c>World.ApplyModuleRule</c>
        /// refusing an addition that would meet it.
        /// </para>
        /// </remarks>
        private static void MutateModuleGene(MorphNode node, Rng rng, MutationRates rates)
        {
            if (!(rates.ModuleGeneMutationChance > 0f)) return;

            if (rng.Chance(rates.ModuleGeneMutationChance))
            {
                node.Growth = node.Growth == ModuleGrowth.Indeterminate
                    ? ModuleGrowth.Determinate
                    : ModuleGrowth.Indeterminate;
            }

            // Only where it means something. A determinate node's ceiling is read by nothing,
            // and letting it drift would fill the record with a number that says nothing about
            // the body — and would cost a draw on every node of every birth.
            if (node.Growth != ModuleGrowth.Indeterminate) return;

            int from = node.MaxModules > node.RecursiveLimit ? node.MaxModules : node.RecursiveLimit;
            float stepped = PerturbPositive(from, rng, rates);

            int moved = stepped > from
                ? (int)Math.Ceiling(stepped)
                : stepped < from ? (int)Math.Floor(stepped) : from;

            if (moved < node.RecursiveLimit) moved = node.RecursiveLimit;
            if (moved > DevelopmentLimits.Default.MaxParts) moved = DevelopmentLimits.Default.MaxParts;

            node.MaxModules = moved;
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

        /// <summary>
        /// A small copy of a node, of another cell type, attached to the body: the one route by
        /// which a lineage acquires a cell type it does not have (the owner's ruling of
        /// 2026-09-24).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Additions and removals only.</b> Until this ruling the draw changed an existing
        /// node's type in place, so a leaf became a stomach whole: a full-sized organ of a new
        /// kind, with the upkeep of its whole volume, in a body whose every other part was tuned
        /// to the leaf it had been. Round 47's dissection read the result. A bud instead arrives
        /// at <see cref="MutationRates.NewNodeHalfExtent"/>, the size <see cref="AddNode"/>'s
        /// duplicate arrives at, so its upkeep is small on the birth it happens and it grows only
        /// if selection holds it; it leaves by the same door every node leaves by, shrinking
        /// under <see cref="MutationRates.NodeExtinctionHalfExtent"/>.
        /// </para>
        /// <para>
        /// <b>The repairs are the ones the in-place change made</b>, for their reasons: a lift is
        /// drawn for a buoyancy cell and zeroed on anything else (a trait that can only arrive
        /// useless is one selection never sees), and D106's four attributes are clamped down under
        /// the new type's caps with nothing handed out. <b>The joint is always welded</b>, where
        /// the in-place change welded it only for a type that disallows one: a bud is a small
        /// thing fixed to its host, a link bud earns a joint the way any jointless link does
        /// (<see cref="ChangeJointType"/>), and a welded part is what the rigid-group floors
        /// (<see cref="DevelopmentLimits.FloorsWeighRigidGroups"/>) carry on its host.
        /// </para>
        /// <para>
        /// <b>It enters once on a path</b> (recursive limit 1, determinate): a copy of a node with
        /// a limit of 0 would never be entered and a copy of an indeterminate one would arrive
        /// already modular. Its host is drawn from the nodes development can enter
        /// (<see cref="EnterableNodes"/>), not from every node as <see cref="AddNode"/> draws,
        /// because round 47's unexpressed stomachs were mostly nodes no developed part led to; a
        /// bud hung on a dormant node is a gene nobody sees. The edge is
        /// <see cref="RandomEdgeTo"/>'s, as <see cref="AddNode"/>'s is.
        /// </para>
        /// </remarks>
        /// <returns>The bud, already in the genome, or null when no other type is registered.</returns>
        private static MorphNode Bud(
            Genome g, int sourceIndex, List<int> hosts, Rng rng, MutationRates rates,
            CellTypeRegistry cellTypes, RandomGenomeOptions genome)
        {
            MorphNode source = g.Nodes[sourceIndex];

            var ids = new List<string>();
            foreach (string id in cellTypes.Ids()) if (id != source.CellTypeId) ids.Add(id);
            if (ids.Count == 0) return null;

            MorphNode bud = source.Clone();
            bud.Edges.Clear();

            bud.CellTypeId = ids[rng.Range(ids.Count)];

            bud.Lift = bud.CellTypeId == CellTypeIds.Buoyancy
                ? rng.Range(genome.MinBuoyancyLift, genome.MaxBuoyancyLift)
                : 0f;

            bud.JointType = JointType.Fixed;
            bud.JointLimits = Array.Empty<Float2>();
            bud.Power = 0f;

            bud.RecursiveLimit = 1;
            bud.Growth = ModuleGrowth.Determinate;
            bud.MaxModules = 1;

            CellType became = cellTypes.Resolve(bud.CellTypeId);
            bud.Attack = Clamp(bud.Attack, 0f, became.AttackMax);
            bud.Intake = Clamp(bud.Intake, 0f, became.IntakeMax);
            bud.Protection = Clamp(bud.Protection, 0f, became.ProtectionMax);
            bud.Toughness = Clamp(bud.Toughness, 1f, became.ToughnessMax);

            bud.Dimensions = new Float3(rates.NewNodeHalfExtent);

            g.Nodes.Add(bud);
            int host = hosts[rng.Range(hosts.Count)];
            g.Nodes[host].Edges.Add(RandomEdgeTo(g, rng, child: g.Nodes.Count - 1));

            return bud;
        }

        /// <summary>
        /// The nodes development can enter at birth: the root, and every node an edge from an
        /// enterable node leads to whose recursive limit lets it be entered at all.
        /// </summary>
        /// <remarks>
        /// Read from the graph alone, so it is cheap and needs no limits: it does not ask whether
        /// a terminal-only edge ever fires or whether a part clears the volume floor, which
        /// development alone can answer. It removes the commonest dead host in round 47's snapshots,
        /// a node with a recursive limit of 0, and every node only such a node leads to. In index
        /// order, so the draw over it is deterministic in the genome.
        /// </remarks>
        private static List<int> EnterableNodes(Genome g)
        {
            var enterable = new bool[g.Nodes.Count];
            var stack = new Stack<int>();

            enterable[g.RootIndex] = true;
            stack.Push(g.RootIndex);

            while (stack.Count > 0)
            {
                foreach (MorphEdge edge in g.Nodes[stack.Pop()].Edges)
                {
                    int child = edge.Child;
                    if (enterable[child] || g.Nodes[child].RecursiveLimit < 1) continue;
                    enterable[child] = true;
                    stack.Push(child);
                }
            }

            var result = new List<int>();
            for (int i = 0; i < enterable.Length; i++) if (enterable[i]) result.Add(i);
            return result;
        }

        /// <summary>
        /// D106 item 3's four, each a step within its cell type's own cap —
        /// <c>logbook/specs/mouth-spec.md</c> rule 1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing is drawn while the rate is zero</b>, which is the default and is what lets
        /// this build replay the record to the bit — <see cref="MutateModuleGene"/>'s rule, one
        /// gene along, and <see cref="MutationRates.AttributeMutationChance"/> states it.
        /// </para>
        /// <para>
        /// <b>The step is a fraction of the cap and not of the value, which is a departure from
        /// <see cref="PerturbPositive"/> worth naming.</b> Three of the four start at zero on every
        /// founder, by the owner's ruling, and a relative step from zero reaches
        /// <see cref="PerturbPositive"/>'s floor of 1e-4 and then walks in log space for sixty
        /// successful mutations before it is worth anything. An attribute is a <i>bounded</i> dial
        /// — it has a cap, which a length and a torque do not — so the natural step is a fraction
        /// of the range it lives in. That is the same argument <see cref="MutateModuleGene"/>
        /// makes about a count of 1: a gene that cannot move is a gene selection never sees.
        /// </para>
        /// <para>
        /// <b>A cap at the floor takes no draw at all.</b> A leaf cannot bite, so nothing is rolled
        /// for its attack — which also means the stream a genome consumes depends on what its
        /// cells are, exactly as <see cref="MorphNode.Lift"/>'s perturbation already does. That is
        /// deterministic in the genome and therefore replayable.
        /// </para>
        /// </remarks>
        private static void MutateAttributes(
            MorphNode node, Rng rng, MutationRates rates, CellTypeRegistry cellTypes)
        {
            if (!(rates.AttributeMutationChance > 0f)) return;

            CellType type = cellTypes.Resolve(node.CellTypeId);

            node.Attack = MoveAttribute(node.Attack, 0f, type.AttackMax, rng, rates);
            node.Intake = MoveAttribute(node.Intake, 0f, type.IntakeMax, rng, rates);
            node.Protection = MoveAttribute(node.Protection, 0f, type.ProtectionMax, rng, rates);
            node.Toughness = MoveAttribute(node.Toughness, 1f, type.ToughnessMax, rng, rates);
        }

        /// <summary>One attribute's step: a Gaussian of the cap's own scale, clamped to it.</summary>
        private static float MoveAttribute(
            float value, float floor, float max, Rng rng, MutationRates rates)
        {
            if (!(max > floor)) return floor;
            if (!rng.Chance(rates.AttributeMutationChance)) return value;

            return Clamp(value + rng.Gaussian(0f, rates.ScalarStdDev * (max - floor)), floor, max);
        }

        private static float Clamp(float value, float floor, float max)
        {
            float ceiling = max > floor ? max : floor;
            return value < floor ? floor : value > ceiling ? ceiling : value;
        }

        private static void ChangeJointType(
            MorphNode node, Rng rng, CellTypeRegistry cellTypes, RandomGenomeOptions genome)
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
            // Drawn from the same bounds a founder draws from. This used to be a hardcoded
            // rng.Range(5f, 120f) — the ceiling RandomGenomeOptions retired in logbook/0017,
            // left behind here when the founder path was lowered to 20. It mattered more than a
            // stale constant usually does, because this branch fires only when a node that had
            // no joint gains one: it is the single path by which an established lineage can
            // invent a muscle, and it was handing that muscle a mean 62.5 N·m against a founder
            // mean of 12.5. At LinkCell's 0.02 W/N·m that is 1.25 W standing per degree of
            // freedom before the joint moves — almost exactly the 1.24 W median link that
            // logbook/0017 measured nothing surviving, and six times what D032 found affordable.
            if (dof > 0 && node.Power <= 0f)
            {
                node.Power = rng.Range(genome.MinLinkPower, genome.MaxLinkPower);
            }
            else if (dof == 0)
            {
                node.Power = 0f;
            }
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
            SensorChannel[] sensorPool, out NeuronDef[] result)
        {
            var list = new List<NeuronDef>(neurons);

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (rng.Chance(rates.RemoveNeuronChance)) { list.RemoveAt(i); continue; }
                MutateNeuron(list[i], rng, rates, sensorPool);
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

        private static void MutateNeuron(
            NeuronDef neuron, Rng rng, MutationRates rates, SensorChannel[] sensorPool)
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

                neuron.Inputs[i] = rng.Chance(rates.RewireInputChance)
                    ? Rewire(input, constant, weight, rng, sensorPool)
                    : new NeuronInput(input.Kind, input.Index, input.Channel, constant, weight);
            }
        }

        /// <summary>
        /// Repoints one input at something else, keeping its strength — DESIGN.md §4.5.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><see cref="MutationRates.RewireInputChance"/> was declared, hashed, serialized and
        /// set to 0.9 by a test, and nothing read it</b> (logbook/0019). Weights, constants and
        /// operators mutated; the <i>topology inside a node</i> did not. Every neuron read
        /// exactly what its founder drew, for every generation of every lineage, and the only
        /// way a lineage could acquire a new kind of connection was for a whole node to be
        /// duplicated. That is the third knob here to reach nothing it configures, and the
        /// reflection tests could not catch it — they prove a tunable reaches the hash and the
        /// file, not that any code consults it.
        /// </para>
        /// <para>
        /// The new reference may be illegal in this context — a sensor on a global neuron, a
        /// <see cref="NeuronInputKind.SameNode"/> index past the end. That is deliberate:
        /// <see cref="RepairInputs"/> already exists to make exactly those legal, runs
        /// immediately afterwards, and is the single place the rules live. Duplicating them
        /// here is how the two copies start disagreeing.
        /// </para>
        /// <para>
        /// The weight survives the rewire. A mutation that repoints a connection and randomises
        /// its strength is two mutations, and the compound is almost always worse than either —
        /// which makes rewiring look harmful and get selected out before it has been tried.
        /// </para>
        /// </remarks>
        private static NeuronInput Rewire(
            NeuronInput input, float constant, float weight, Rng rng, SensorChannel[] sensorPool)
        {
            // Drawn from the kinds a neuron can still be wired to. GlobalBrain is the last
            // member of the enum and is excluded by the range (D081): a rewire onto it would be
            // repaired to a constant on the spot, a mutation spent on nothing.
            var kind = (NeuronInputKind)rng.Range(LiveKindCount);

            if (kind == NeuronInputKind.Sensor)
            {
                NeuronInput drawn = SensorChannels.RandomSensor(rng, weight, sensorPool);
                return new NeuronInput(kind, drawn.Index, drawn.Channel, constant, weight);
            }

            int index = kind == NeuronInputKind.Constant ? 0 : rng.Range(MaxRewireIndex);
            return new NeuronInput(kind, index, input.Channel, constant, weight);
        }

        /// <summary>
        /// How many input kinds a rewire may produce: every member below
        /// <see cref="NeuronInputKind.GlobalBrain"/>, which is the enum's last value and is
        /// retired (D081). <c>MutationTests</c> pins the enum's shape so that a kind added after
        /// it would be noticed rather than silently unreachable.
        /// </summary>
        private static readonly int LiveKindCount = (int)NeuronInputKind.GlobalBrain;

        /// <summary>
        /// Largest neuron index a rewire will reach for. Not a cap on how many neurons a node
        /// may have — <see cref="RepairInputs"/> clamps into whatever is actually there, so the
        /// only cost of guessing low is that a rewire onto a large node favours its earlier
        /// neurons.
        /// </summary>
        private const int MaxRewireIndex = 8;

        private static void RepairInputs(NeuronDef[] neurons, MorphNode owner, Genome g, Rng rng)
        {
            int local = neurons.Length;

            // `owner == null` meant "the set being repaired is the global brain", which no
            // caller passes since D081; the branch is kept so the function stays total. For a
            // node, `g.GlobalBrain` is empty by the time this runs (Mutate clears it first), so
            // every GlobalBrain reference below becomes a constant.
            int globalCount = owner == null ? local : g.GlobalBrain.Length;

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
                    else if (kind == NeuronInputKind.Sensor)
                    {
                        // Which index means what is per channel — a DOF for the joint channels,
                        // an axis for flow, nothing at all for depth. An out-of-range one reads
                        // zero rather than faulting, so this is tidiness rather than safety, but
                        // an index of 7 on a channel with one value is a reference that can never
                        // become meaningful no matter what the body does.
                        index = Math.Min(index, input.Channel.IndexCount() - 1);
                    }
                    else if (kind == NeuronInputKind.SameNode)
                    {
                        index = local == 0 ? 0 : Math.Min(index, local - 1);
                        if (local == 0) kind = NeuronInputKind.Constant;
                    }
                    else if (kind == NeuronInputKind.GlobalBrain)
                    {
                        if (globalCount == 0) { kind = NeuronInputKind.Constant; index = 0; }
                        else index = Math.Min(index, globalCount - 1);
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
        /// capacity, a joint's range — is meaningless or invalid at or below it, and a
        /// Gaussian step has no lower bound.
        /// </remarks>
        private static float PerturbPositive(float value, Rng rng, MutationRates rates)
        {
            float mutated = Perturb(value, rng, rates);
            return mutated > 1e-4f ? mutated : 1e-4f;
        }

        /// <summary>The same step as <see cref="PerturbPositive"/>, taken unconditionally.</summary>
        /// <remarks>
        /// For the dials that carry their own named chance. <see cref="Perturb"/> gates itself on
        /// <c>ScalarChance</c>, so a caller that had already rolled a named chance was rolling
        /// twice and moving its dial at the product of the two: the birth investment and the adult
        /// scale each advertised 0.08 per birth and delivered 0.0064, an order of magnitude below
        /// a node dimension, while the remark beside the knob said otherwise. Found in the review
        /// of fable-propose-growth.md's build (2026-09-08). A knob means the rate it names.
        /// </remarks>
        private static float Step(float value, Rng rng, MutationRates rates)
        {
            float scale = Math.Max(1e-4f, Math.Abs(value));
            float mutated = value + rng.Gaussian(0f, rates.ScalarStdDev * scale);
            return mutated > 1e-4f ? mutated : 1e-4f;
        }

        private static Float3 PerturbVector(Float3 v, Rng rng, MutationRates rates) =>
            new Float3(
                Perturb(v.X, rng, rates), Perturb(v.Y, rng, rates), Perturb(v.Z, rng, rates));
    }

    /// <summary>
    /// What one call to <see cref="Mutator.Mutate"/> gained by budding — the owner's ruling of
    /// 2026-09-24, read by the lineage row so a round can count bud births by cell type.
    /// </summary>
    public sealed class MutationLog
    {
        /// <summary>Each bud's cell type, in the order the buds were made.</summary>
        public List<string> BudCellTypes { get; } = new List<string>();

        /// <summary>Each bud's node index in the returned genome, beside its type.</summary>
        public List<int> BudNodes { get; } = new List<int>();

        public int Buds => BudNodes.Count;

        public void Clear()
        {
            BudCellTypes.Clear();
            BudNodes.Clear();
        }
    }
}
