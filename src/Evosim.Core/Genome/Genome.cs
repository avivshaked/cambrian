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
        /// <see cref="NeuronInputKind.GlobalBrain"/>. <b>Retired by D081 (2026-09-07):</b> a
        /// genome recorded before then may carry them, and it still loads, clones, validates and
        /// compares (<see cref="SpeciesDistance"/>); they are not stepped (<see cref="Brain"/>),
        /// mutation empties the array in every child (<see cref="Mutator"/>), and a reference
        /// to one reads zero. Kept as a property so that no stored genome is refused.
        /// </summary>
        public NeuronDef[] GlobalBrain { get; set; } = Array.Empty<NeuronDef>();

        /// <summary>How surplus energy is turned into offspring — DESIGN.md §5A.6.</summary>
        public ReproductionTraits Reproduction { get; set; } =
            new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
                Mode = ReproductionMode.Lump, GestationShare = 0.5f,
            };

        /// <summary>
        /// How big this body plan grows up to be: one scalar the developer multiplies into every
        /// node's dimensions — fable-propose-growth.md rule 1 (2026-09-08).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The plan and its size are separate things to mutate, and that is the whole reason
        /// this is not just another dimension.</b> Scaling a body through its nodes takes as many
        /// mutations as the genome has nodes and passes through every intermediate shape on the
        /// way, so a lineage cannot get bigger without also getting a different shape. One scalar
        /// can be walked by a graded step, which makes size the first trait in this world that
        /// selection can climb rather than jump. Every trait the world had selected on before this
        /// was a switch: a joint or not, a stomach or not.
        /// </para>
        /// <para>
        /// <b>It multiplies the developer's accumulated scale, so it reaches anchors and volumes
        /// for free.</b> <see cref="Developer"/> already folds accumulated scale into half-extents
        /// and into the anchors derived from them, and the small-part pruning rule judges what
        /// comes out — so a genome scaled to nothing loses its parts and is a stillbirth, exactly
        /// as one whose nodes shrank to nothing already was.
        /// </para>
        /// </remarks>
        public float AdultScale { get; set; } = 1f;

        public Genome Clone()
        {
            var clone = new Genome
            {
                RootIndex = RootIndex,
                GlobalBrain = new NeuronDef[GlobalBrain.Length],
                Reproduction = Reproduction.Clone(),
                AdultScale = AdultScale,
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
        /// <param name="cellTypes">
        /// Registry to resolve <see cref="MorphNode.CellTypeId"/> against.
        /// Defaults to <see cref="CellTypeRegistry.Standard"/>.
        /// </param>
        /// <param name="buoyancyOffsetPriced">
        /// Whether the world this genome is to live in charges for
        /// <see cref="MorphNode.BuoyancyOffset"/> (<see cref="RunConfig.BuoyancyOffsetWattsPerCubicMetre"/>
        /// above 0). False refuses any nonzero offset; null asks only the range, which is the
        /// question a genome on its own can answer (development, a file being read).
        /// </param>
        public IReadOnlyList<string> Validate(
            CellTypeRegistry cellTypes = null, bool? buoyancyOffsetPriced = null)
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

            // A brood of zero is a lineage that ends, which is a thing a creature may not
            // express — dying childless has to be something the world does to it, not something
            // the genome declares. A negative investment would let a parent gain energy by
            // reproducing, which is a free-energy source of exactly the kind §11.2 exists for.
            if (Reproduction.BroodSize < 1)
            {
                issues.Add($"Brood size {Reproduction.BroodSize} must be at least 1.");
            }

            if (float.IsNaN(Reproduction.BirthInvestment) ||
                float.IsInfinity(Reproduction.BirthInvestment) ||
                Reproduction.BirthInvestment <= 0f)
            {
                issues.Add(
                    $"Birth investment {Reproduction.BirthInvestment} must be finite and " +
                    "positive. An offspring born with nothing is dead on arrival, and one born " +
                    "with less than nothing pays its parent to make it.");
            }

            // Zero is a creature that breeds the moment it can pay, which is the world as it
            // stood before D098 and a strategy rather than a fault. Below zero is not a bolder
            // strategy: it lowers the gate beneath the price, so a parent would be admitted to a
            // birth it cannot fund and the shortfall would have to come from somewhere.
            if (float.IsNaN(Reproduction.ReserveMargin) ||
                float.IsInfinity(Reproduction.ReserveMargin) ||
                Reproduction.ReserveMargin < 0f)
            {
                issues.Add(
                    $"Reserve margin {Reproduction.ReserveMargin} must be finite and " +
                    "non-negative. It is seconds of the parent's own standing cost held back " +
                    "after a birth, and a negative span of time is not caution.");
            }

            // The ruling of 2026-09-24. A share above 1 would bank more than the step earned, and
            // a gestating parent that banks nothing never breeds; a lump breeder carries its
            // share inert, so zero is legal there and a genome built by hand need not name one.
            if (float.IsNaN(Reproduction.GestationShare) ||
                Reproduction.GestationShare < 0f || Reproduction.GestationShare > 1f)
            {
                issues.Add(
                    $"Gestation share {Reproduction.GestationShare} must lie in [0, 1]: it is " +
                    "the share of a step's positive net income moved into the gestation account.");
            }

            if (Reproduction.Mode == ReproductionMode.Gestation && !(Reproduction.GestationShare > 0f))
            {
                issues.Add(
                    "A gestating genome with a gestation share of 0 banks nothing and can never " +
                    "breed; the share must be above 0 in Gestation mode.");
            }

            if (Reproduction.Mode != ReproductionMode.Lump &&
                Reproduction.Mode != ReproductionMode.Gestation)
            {
                issues.Add($"Reproduction mode {(int)Reproduction.Mode} is not a mode this build knows.");
            }

            // A body plan with no size is not a small creature, it is an arithmetic hole: every
            // half-extent, every anchor and every volume in the phenotype is this number times
            // something, so a zero develops into nothing and a negative one reflects the whole
            // body through the origin without recording that it did.
            if (float.IsNaN(AdultScale) || float.IsInfinity(AdultScale) || AdultScale <= 0f)
            {
                issues.Add(
                    $"Adult scale {AdultScale} must be finite and positive. It multiplies every " +
                    "node's dimensions, so nothing below zero describes a body.");
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

                // D106 item 2. Negative only, not "below the recursive limit": the two genes
                // mutate apart and Developer.CountFor takes the larger of them, so a ceiling
                // that has fallen under the floor is a body the genome still describes rather
                // than an invalid genome.
                if (node.MaxModules < 0)
                {
                    issues.Add($"Node {n}: MaxModules {node.MaxModules} is negative.");
                }

                int dof = node.JointType.DofCount();
                if (node.JointLimits.Length != dof)
                {
                    issues.Add($"Node {n}: {node.JointType} has {dof} DOF but {node.JointLimits.Length} joint limits.");
                }

                // Cell type, and the rule that only a link may move (DESIGN.md §5A.1). Checked
                // here rather than trusted as a convention: a genome whose stomach is also its
                // elbow would develop, run and be scored, and nothing downstream could tell it
                // was never meant to be legal.
                if (!PartShapeRegistry.Standard.Contains(node.ShapeId))
                {
                    issues.Add(
                        $"Node {n}: unknown shape '{node.ShapeId}'. " +
                        $"Registered: {string.Join(", ", PartShapeRegistry.Standard.Ids())}.");
                }

                CellTypeRegistry registry = cellTypes ?? CellTypeRegistry.Standard;
                if (!registry.Contains(node.CellTypeId))
                {
                    issues.Add(
                        $"Node {n}: unknown cell type '{node.CellTypeId}'. " +
                        $"Registered: {string.Join(", ", registry.Ids())}.");
                }
                else if (dof > 0 && !registry.Resolve(node.CellTypeId).AllowsJoint)
                {
                    issues.Add(
                        $"Node {n}: cell type '{node.CellTypeId}' has a {node.JointType} joint, " +
                        $"but only '{CellTypeIds.Link}' may move. Two parts cannot actuate " +
                        "against each other without a link between them (§5A.1).");
                }

                // Power is charged for, so it must not sit unread on a part that cannot use it:
                // a rigid cell carrying power would pay nothing and mean nothing, and a link
                // with none is a joint that cannot move but is billed as though it could.
                if (float.IsNaN(node.Power) || float.IsInfinity(node.Power) || node.Power < 0f)
                {
                    issues.Add($"Node {n}: Power {node.Power} must be finite and non-negative.");
                }
                else if (dof > 0 && node.Power <= 0f)
                {
                    issues.Add(
                        $"Node {n}: a {node.JointType} joint with Power {node.Power} cannot " +
                        "actuate. Give it capacity or make it Fixed.");
                }
                else if (dof == 0 && node.Power != 0f)
                {
                    issues.Add(
                        $"Node {n}: Power {node.Power} on a part with no joint. Nothing reads it, " +
                        "and nothing charges for it.");
                }

                // Lift, for the same reason and with the same shape as Power above: a cell type
                // nothing reads it on must not carry it, or the genome records a trait the
                // phenotype cannot express and selection cannot see.
                if (float.IsNaN(node.Lift) || float.IsInfinity(node.Lift) || node.Lift < 0f)
                {
                    issues.Add($"Node {n}: Lift {node.Lift} must be finite and non-negative.");
                }
                else if (node.Lift > 0f && node.CellTypeId != CellTypeIds.Buoyancy)
                {
                    issues.Add(
                        $"Node {n}: Lift {node.Lift} on a '{node.CellTypeId}' cell. Only a " +
                        $"'{CellTypeIds.Buoyancy}' cell holds gas, and nothing charges for this.");
                }
                else if (node.Lift > BuoyancyCell.MaxLiftSinkMultiples)
                {
                    issues.Add(
                        $"Node {n}: Lift {node.Lift} exceeds the {BuoyancyCell.MaxLiftSinkMultiples}x " +
                        "sink bound, past which the solver rather than the economy decides what happens.");
                }

                // D111. The range always, because the solver's arm is this times a half-extent
                // and a value past 1 puts the buoyancy centre outside the part. The price only
                // where the caller knows the world: Lift's rule, a trait nothing charges for is
                // one selection cannot see, so a world at price 0 carries none.
                if (float.IsNaN(node.BuoyancyOffset) || node.BuoyancyOffset < -1f ||
                    node.BuoyancyOffset > 1f)
                {
                    issues.Add(
                        $"Node {n}: BuoyancyOffset {node.BuoyancyOffset} must lie in [-1, 1], a " +
                        "fraction of the part's thinnest half-extent.");
                }
                else if (buoyancyOffsetPriced == false && node.BuoyancyOffset != 0f)
                {
                    issues.Add(
                        $"Node {n}: BuoyancyOffset {node.BuoyancyOffset} in a world that does not " +
                        "price it (BuoyancyOffsetWattsPerCubicMetre 0). Nothing would charge for it.");
                }

                // D106 item 3's four, against the cell type's own caps
                // (logbook/specs/mouth-spec.md rule 2). Refused and not clamped, for §9's reason:
                // a genome clamped on load is a different creature wearing the stored one's
                // identity — a leaf whose attack was quietly taken away would be measured, scored
                // and filed as the armed leaf it was written as. The floors are 0 for the three
                // that a body may simply not have and 1 for toughness, which is the neutral
                // multiplier on a health pool and the number D106's pricing is written against.
                if (registry.Contains(node.CellTypeId))
                {
                    CellType type = registry.Resolve(node.CellTypeId);

                    CheckAttribute(issues, n, "Attack", node.Attack, 0f, type.AttackMax, node.CellTypeId);
                    CheckAttribute(issues, n, "Intake", node.Intake, 0f, type.IntakeMax, node.CellTypeId);
                    CheckAttribute(
                        issues, n, "Protection", node.Protection, 0f, type.ProtectionMax, node.CellTypeId);
                    CheckAttribute(
                        issues, n, "Toughness", node.Toughness, 1f, type.ToughnessMax, node.CellTypeId);
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

        /// <summary>
        /// One of D106's four attributes against its floor and its cell type's cap.
        /// </summary>
        /// <remarks>
        /// A cap below the floor is a legal table entry and means the attribute cannot move: a
        /// leaf's toughness cap is 1, which is also its floor, so a leaf is neutral tissue and
        /// nothing else. The test is written so that such a type admits exactly the floor and
        /// nothing else, rather than refusing every genome of that type.
        /// </remarks>
        private static void CheckAttribute(
            List<string> issues, int node, string name, float value, float floor, float max,
            string cellTypeId)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                issues.Add($"Node {node}: {name} {value} must be finite.");
                return;
            }

            if (value < floor)
            {
                issues.Add(
                    $"Node {node}: {name} {value} is below the floor of {floor}. " +
                    "An attribute below its floor is a body the economy has no price for.");
                return;
            }

            if (value > max && value > floor)
            {
                issues.Add(
                    $"Node {node}: {name} {value} exceeds the cap of {max} for a " +
                    $"'{cellTypeId}' cell (D106 item 3). A genome above a cap is refused rather " +
                    "than clamped — a clamped genome is a different creature wearing this one's " +
                    "identity.");
            }
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
