using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// A node in the morphology graph — DESIGN.md §4.1. One node can become many parts.
    /// </summary>
    public sealed class MorphNode
    {
        /// <summary>Box half-extents, before any accumulated scale from the path that reached this node.</summary>
        public Float3 Dimensions { get; set; } = new Float3(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// What this node's parts are made of — <see cref="CellType.Id"/>, DESIGN.md §5A.1.
        /// </summary>
        /// <remarks>
        /// A string rather than an enum so genomes survive new types being added; resolved
        /// against a <see cref="CellTypeRegistry"/>, which fails loudly on an unknown id rather
        /// than substituting a default.
        ///
        /// Defaults to structural — inert, cheapest, and unable to carry a joint. A default that
        /// fed itself would quietly give every genome written before this field existed a free
        /// energy source.
        /// </remarks>
        public string CellTypeId { get; set; } = CellTypeIds.Structural;

        /// <summary>Geometry of the parts grown from this node — <see cref="PartShape.Id"/>.</summary>
        /// <remarks>
        /// Independent of <see cref="CellTypeId"/>: what a part is made of and what shape it is
        /// are separate traits, so a photosynthetic sheet and a photosynthetic ball are both
        /// reachable and evolution picks between them on their merits.
        /// </remarks>
        public string ShapeId { get; set; } = ShapeIds.Box;

        /// <summary>
        /// Joint connecting a part grown from this node to its parent. Ignored at the root,
        /// which has no parent. Mutable, with limits resampled to the new DOF count (§4.1).
        /// </summary>
        public JointType JointType { get; set; } = JointType.Hinge;

        /// <summary>Min/max per DOF, in radians. Length should match <see cref="JointType"/>'s DOF count.</summary>
        public Float2[] JointLimits { get; set; } = Array.Empty<Float2>();

        /// <summary>
        /// Peak torque this node's joint may exert, in newton-metres — links only, DESIGN.md §5A.1.
        /// </summary>
        /// <remarks>
        /// Evolvable, and paid for: <see cref="LinkCell"/> charges a standing cost proportional
        /// to it, per degree of freedom, whether or not the joint is moving. Zero on anything
        /// that is not a link, and <c>Genome.Validate</c> enforces that so a rigid cell cannot
        /// carry meaningful data in a field nothing reads.
        ///
        /// This replaces the fixed <c>TorqueScale</c> of §4.4, which applied the same strength
        /// to every joint in every creature. The mass-scaling that scheme inherited from
        /// [K12 §2.2, p.5] existed for numerical stability rather than realism; bounds on this
        /// field serve that purpose now, and the metabolic cost does the rest.
        /// </remarks>
        public float Power { get; set; }

        /// <summary>
        /// Weight this node's tissue cancels, in kg/m³ of displaced water — buoyancy cells only,
        /// DESIGN.md §5A.1, D049.
        /// </summary>
        /// <remarks>
        /// Evolvable and paid for, exactly as <see cref="Power"/> is:
        /// <see cref="BuoyancyCell"/> charges a standing cost proportional to it whether or
        /// not it is holding the creature anywhere useful. Zero on anything that is not a
        /// buoyancy cell, and <c>Genome.Validate</c> enforces that so a cell type nothing reads
        /// it on cannot carry meaningful data.
        ///
        /// Lift rather than density: being heavier than water is already free (§5.2 and D044's
        /// <c>TissueExcessDensity</c>), so the thing that needs an organ, a price and a genome
        /// field is going up.
        ///
        /// <b>In multiples of that sink, not in kg/m³</b> — D050. 1 is neutral buoyancy, 2 rises
        /// as fast as a bare body falls. Absolute units made the field's meaning depend on a
        /// world constant §5.2 flags as unmeasured, and the two were 25x to 250x apart, so the
        /// weakest expressible bladder was already a runaway (logbook/0034).
        /// </remarks>
        public float Lift { get; set; }

        /// <summary>
        /// How many times this node may occur along one path before its recursion is
        /// considered spent. At that point only <see cref="MorphEdge.TerminalOnly"/> edges
        /// are followed (DESIGN.md §4.2).
        /// </summary>
        public int RecursiveLimit { get; set; } = 1;

        /// <summary>
        /// Whether this node's count is fixed at development or is a bounded rule on the body's
        /// reserve — D106 item 2, <c>logbook/specs/module-gene-spec.md</c> rule 2.
        /// </summary>
        /// <remarks>
        /// <b>Determinate on every founder and on every body in the record.</b> An indeterminate
        /// node still develops to <see cref="RecursiveLimit"/> at birth — a body is born with the
        /// plan it would have had — and only then does <c>World.ApplyModuleRule</c> move the
        /// count, between <see cref="RecursiveLimit"/> and <see cref="MaxModules"/>.
        /// </remarks>
        public ModuleGrowth Growth { get; set; } = ModuleGrowth.Determinate;

        /// <summary>
        /// The ceiling on an indeterminate node's count — rule 2. Drawn at
        /// <see cref="RecursiveLimit"/> and mutable upward.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Read as <c>max(RecursiveLimit, MaxModules)</c> and never below the minimum.</b>
        /// <see cref="RecursiveLimit"/> mutates too, so the two can cross; a ceiling under the
        /// floor would otherwise be a node that development builds and the rule immediately wants
        /// to unbuild. The rule takes the larger and says so where it reads it.
        /// </para>
        /// <para>
        /// Meaningless on a <see cref="ModuleGrowth.Determinate"/> node, where the count is
        /// <see cref="RecursiveLimit"/> and nothing consults this. Carried on every node anyway,
        /// because a gene that exists only sometimes is a genome whose fields depend on another
        /// field's value, and <see cref="GenomeJson"/> writes every field unconditionally.
        /// </para>
        /// </remarks>
        public int MaxModules { get; set; } = 1;

        /// <summary>
        /// Damage this node's parts do per second to a part of another body in held contact —
        /// D106 item 3. <b>Carried at zero and read by nothing until round 45.</b>
        /// </summary>
        /// <remarks>
        /// The four attributes are in the genome from round 44's build (format 7) and turned on
        /// by round 45's, which is D106 item 6's one format bump for both rounds: the alternative
        /// is re-extracting every stored genome twice in a fortnight. The mutator does not touch
        /// them here, so a round 44 world carries four constants.
        /// </remarks>
        public float Attack { get; set; }

        /// <summary>
        /// Charged units this node's parts take per second from a corpse in reach — D106 item 4.
        /// Carried at zero; see <see cref="Attack"/>.
        /// </summary>
        public float Intake { get; set; }

        /// <summary>
        /// Damage per second absorbed before health suffers — D106 item 3. Carried at zero; see
        /// <see cref="Attack"/>.
        /// </summary>
        public float Protection { get; set; }

        /// <summary>
        /// Health per unit of volume — D106 item 3. Carried at <b>1</b> rather than at 0, unlike
        /// the other three.
        /// </summary>
        /// <remarks>
        /// A part's health pool is its volume times this, so a default of 0 would give every
        /// stored genome a body that dies to the first scratch on the day round 45 turns health
        /// on — a genome wearing another's identity, which is what §9's refuse-rather-than-default
        /// rule exists to stop, arriving through the default rather than through the file. 1 is
        /// the neutral multiplier and the number D106's pricing is written against.
        /// </remarks>
        public float Toughness { get; set; } = 1f;

        /// <summary>The node's local brain. Duplicated with the node — DESIGN.md §4.3.</summary>
        public NeuronDef[] Neurons { get; set; } = Array.Empty<NeuronDef>();

        /// <summary>Outgoing edges.</summary>
        public List<MorphEdge> Edges { get; } = new List<MorphEdge>();

        public MorphNode Clone()
        {
            var clone = new MorphNode
            {
                Dimensions = Dimensions,
                CellTypeId = CellTypeId,
                ShapeId = ShapeId,
                JointType = JointType,
                JointLimits = (Float2[])JointLimits.Clone(),
                Power = Power,
                Lift = Lift,
                RecursiveLimit = RecursiveLimit,
                Growth = Growth,
                MaxModules = MaxModules,
                Attack = Attack,
                Intake = Intake,
                Protection = Protection,
                Toughness = Toughness,
                Neurons = new NeuronDef[Neurons.Length],
            };

            for (int i = 0; i < Neurons.Length; i++) clone.Neurons[i] = Neurons[i].Clone();
            for (int i = 0; i < Edges.Count; i++) clone.Edges.Add(Edges[i].Clone());

            return clone;
        }

        /// <summary>
        /// Resizes <see cref="JointLimits"/> to match the current joint type, keeping
        /// existing entries where possible and filling new ones with
        /// <paramref name="fill"/>. Called after a joint-type mutation (DESIGN.md §4.5).
        /// </summary>
        public void ResampleJointLimits(Float2 fill)
        {
            int dof = JointType.DofCount();
            if (JointLimits.Length == dof) return;

            var resized = new Float2[dof];
            for (int i = 0; i < dof; i++)
            {
                resized[i] = i < JointLimits.Length ? JointLimits[i] : fill;
            }
            JointLimits = resized;
        }

        public override string ToString() =>
            $"{JointType} {Dimensions} rec={RecursiveLimit} edges={Edges.Count} neurons={Neurons.Length}";
    }
}
