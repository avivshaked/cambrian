using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>Knobs for <see cref="GenomeFactory"/>. Part of the config hash (DESIGN.md §7).</summary>
    public sealed class RandomGenomeOptions
    {
        [Tunable("genome")]
        public int MinNodes { get; set; } = 2;
        [Tunable("genome")]
        public int MaxNodes { get; set; } = 5;

        /// <summary>Outgoing edges per node, before terminal edges are considered.</summary>
        [Tunable("genome")]
        public int MaxEdgesPerNode { get; set; } = 2;

        [Tunable("genome")]
        public int MinRecursiveLimit { get; set; } = 1;
        [Tunable("genome")]
        public int MaxRecursiveLimit { get; set; } = 4;

        /// <summary>Half-extent range, in metres, per axis, for body cells.</summary>
        /// <remarks>
        /// Narrowed from 0.15–0.6. The spread matters more than either endpoint, because what
        /// decides whether a joint can turn is the size of a link against the size of the two
        /// body cells it separates (see <see cref="MinLinkHalfExtent"/>), and a 4x spread on
        /// bodies against a fixed link range put that ratio anywhere from 0.1x to 1.07x. Nothing
        /// here restricts what a genome may express — mutation may take a body cell outside this
        /// range, and should. It bounds the initial population only.
        /// </remarks>
        [Tunable("genome")]
        public float MinHalfExtent { get; set; } = 0.15f;
        [Tunable("genome")]
        public float MaxHalfExtent { get; set; } = 0.40f;

        /// <summary>Per-edge cumulative scale range. Below 1 so recursive chains taper rather than explode.</summary>
        [Tunable("genome")]
        public float MinEdgeScale { get; set; } = 0.6f;
        [Tunable("genome")]
        public float MaxEdgeScale { get; set; } = 1.0f;

        /// <summary>Chance an edge sets one reflection flag, giving a bilateral pair.</summary>
        [Tunable("genome")]
        public float ReflectChance { get; set; } = 0.35f;

        /// <summary>Chance an extra edge is marked terminal, giving a differentiated extremity.</summary>
        [Tunable("genome")]
        public float TerminalChance { get; set; } = 0.3f;

        /// <summary>Chance an edge tilts the child rather than attaching it square-on.</summary>
        [Tunable("genome")]
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
        [Tunable("genome")]
        public float MaxEdgeTiltDegrees { get; set; } = 50f;

        [Tunable("genome")]
        public int MinNeuronsPerNode { get; set; } = 1;
        [Tunable("genome")]
        public int MaxNeuronsPerNode { get; set; } = 3;

        [Tunable("genome")]
        public float MinOscillatorHz { get; set; } = 0.3f;
        [Tunable("genome")]
        public float MaxOscillatorHz { get; set; } = 2.5f;

        /// <summary>
        /// Joint types drawn for non-root nodes. <see cref="JointType.Fixed"/> is excluded —
        /// an unactuated creature cannot swim, and the archive would fill with driftwood.
        /// </summary>
        [Tunable("genome", "Joint types a random genome may draw")]
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
        [Tunable("genome")]
        public float MinJointLimit { get; set; } = 0.4f;
        [Tunable("genome")]
        public float MaxJointLimit { get; set; } = 1.4f;

        /// <summary>Shapes drawn for parts, weighted by repetition — DESIGN.md §4.1.</summary>
        /// <remarks>
        /// Boxes appear twice because they are the only shape that can be flat, and a flat
        /// surface is the strongest paddle available (see <see cref="BoxShape"/>). An initial
        /// population made mostly of spheres would have almost nothing able to swim, and the
        /// first thing selection did would be to rediscover boxes rather than anything
        /// interesting. Capsules are here as limbs. Nothing in this list restricts what a genome
        /// may express — mutation reaches every registered shape.
        /// </remarks>
        [Tunable("genome")]
        public string[] ShapeIdChoices { get; set; } =
        {
            ShapeIds.Box,
            ShapeIds.Box,
            ShapeIds.Capsule,
            ShapeIds.Sphere,
        };

        /// <summary>
        /// Cell types drawn for body cells. Links are not here — they are placed structurally,
        /// not sampled, because the graph's alternation depends on knowing which is which.
        /// </summary>
        /// <remarks>
        /// Weighted by repetition rather than by a parallel array of probabilities: an initial
        /// population wants mostly structure and photosynthesis, since nothing has evolved that
        /// can find nutrients or catch anything yet, and a consumer cell in generation zero is
        /// upkeep with nothing to show for it (§5A.3).
        /// </remarks>
        [Tunable("genome")]
        public string[] BodyCellTypes { get; set; } =
        {
            CellTypeIds.Structural,
            CellTypeIds.Structural,
            CellTypeIds.Photosynthetic,
            CellTypeIds.Photosynthetic,
            CellTypeIds.Absorptive,
            CellTypeIds.Consumer,
            CellTypeIds.Neural,
        };

        /// <summary>
        /// Half-extent range for link cells, in metres. Smaller than body cells on purpose.
        /// </summary>
        /// <remarks>
        /// A link is what buys a joint its clearance: a cube hinged straight onto a parent's
        /// face keeps 0.68 rad within a 10% overlap bound, and a gap of 0.2 half-extents raises
        /// that to about 1.0 rad (`JointClearanceTests`). Too small and it is a hinge pin with
        /// no clearance; too large and the creature is mostly joint. Neither bound is measured
        /// against behaviour yet — §5A.10.
        /// </remarks>
        [Tunable("genome")]
        public float MinLinkHalfExtent { get; set; } = 0.10f;
        [Tunable("genome")]
        public float MaxLinkHalfExtent { get; set; } = 0.26f;

        // A per-edge link/body size RATIO was tried here and made things worse — mean unjointed
        // overlap rose from 12.2% to 14.9% across sixty seeds. The geometry behind it was right;
        // the mechanism was not. MorphEdge.Scale is CUMULATIVE down the graph, so setting it to
        // hit an absolute ratio at one attachment multiplies the whole subtree below it, and two
        // seeds ballooned to 7.9 and 8.6 m³ of overlap. Scale is a growth rate, not a size, and
        // it cannot be borrowed to express one. Recorded because the idea is an obvious one to
        // have again.

        /// <summary>
        /// Chance an edge from a body cell goes to a link rather than straight to another body
        /// cell — DESIGN.md §5A.1.
        /// </summary>
        /// <remarks>
        /// Cells may attach directly, at any angle; they are then welded and cannot actuate
        /// against each other. That is how rigid structure gets built — shells, fins, a stiff
        /// trunk — and it is why a creature made only of body cells is a plant rather than an
        /// invalid genome.
        ///
        /// ⚠ This number, and its counterpart among the mutation operators (§4.5), decide the
        /// balance between rigid and articulated creatures in generation zero and in every
        /// generation after. Neither is measured. Too low and nothing moves; too high and every
        /// creature is mostly joint, paying link upkeep for degrees of freedom it cannot use.
        /// Listed in §5A.10 with the rest of what is still guessed.
        /// </remarks>
        [Tunable("genome")]
        public float LinkChance { get; set; } = 0.5f;

        /// <summary>Peak joint torque range for links, in newton-metres — DESIGN.md §5A.1.</summary>
        /// <remarks>
        /// Replaces the fixed 2 N·m/kg of §4.4, which gave every joint in every creature the
        /// same strength. The upper bound exists for numerical stability, which is what
        /// [K12 §2.2, p.5]'s mass scaling was actually for; what stops evolution sitting at the
        /// ceiling is that <see cref="LinkCell"/> bills for capacity even when idle.
        ///
        /// The upper bound was 120 N·m until logbook/0017 measured what that costs to own. It
        /// enters the ledger as a product with <see cref="LinkCell.IdleWattsPerNewtonMetre"/>, so
        /// a median link billed 1.24 W standing against a photosynthetic part's ~2.3 W total
        /// income — over half a leaf's earnings before the joint moved once, and nothing with a
        /// joint survived at any irradiance from 64 to 400 W/m². Nine runs across both knobs and
        /// three seeds put the affordable region well under a fifth of income; 20 N·m sits there
        /// and measured the same jointed count as cutting the coefficient fourfold (D032).
        ///
        /// The coefficient was left alone deliberately: it *is* the pressure described above, and
        /// weakening it makes capacity free again. For scale: a 250 kg part driven at 500 N·m
        /// reaches its joint stop in a fraction of a second and spends ~85% of its energy there
        /// (logbook/0008), so the useful range was always well below the old ceiling.
        /// </remarks>
        [Tunable("genome")]
        public float MinLinkPower { get; set; } = 5f;
        [Tunable("genome")]
        public float MaxLinkPower { get; set; } = 20f;

        /// <summary>Lift range for a buoyancy cell, in multiples of the sink — D049, D050.</summary>
        /// <remarks>
        /// The range a cell is drawn when it first becomes buoyant, whether at generation zero or
        /// by mutation — one source of truth for both, which is D045's rule after the retired
        /// 120 N·m ceiling survived in the mutator for three weeks.
        ///
        /// The floor is not zero. <c>PerturbPositive</c> is relative, so a cell starting at zero
        /// lift reaches 1e-4 and never climbs out; a trait that can only arrive useless is one
        /// selection never gets to see. ⚠ Both bounds unmeasured (§5A.10) — read them against
        /// <c>BuoyancyCell.WattsPerLiftUnit</c>, since what matters is whether the depth a lift
        /// buys is worth what holding it costs.
        ///
        /// <b>The range straddles 1, which is neutral</b> — D050. It used to be 0.5–5 kg/m³
        /// against a 0.02 kg/m³ sink, so every founder bladder was between 25x and 250x
        /// overpowered and neutral buoyancy — the thing a swim bladder is actually for — was
        /// not a reachable genome value at all (logbook/0034).
        /// </remarks>
        [Tunable("genome")]
        public float MinBuoyancyLift { get; set; } = 0.25f;
        [Tunable("genome")]
        public float MaxBuoyancyLift { get; set; } = 2f;

        /// <summary>
        /// Brood size range for the initial population — DESIGN.md §5A.6.
        /// </summary>
        /// <remarks>
        /// Starts narrow on purpose. Generation zero has no idea what the world rewards, and a
        /// creature that opens with a brood of thirty spends its entire reserve on offspring
        /// that each start with a thirtieth of it. Letting mutation walk this outward means the
        /// r/K axis gets explored by selection rather than handed out at random, which is the
        /// difference between measuring a strategy and seeding one.
        /// </remarks>
        [Tunable("genome")]
        public int MinBroodSize { get; set; } = 1;
        [Tunable("genome")]
        public int MaxBroodSize { get; set; } = 3;

        /// <summary>
        /// Offspring endowment range for the initial population, in joules — DESIGN.md §5A.6.
        /// </summary>
        /// <remarks>
        /// ⚠ Unmeasured, and it cannot be measured until the energy economy exists (§5A.10).
        /// The number that matters is not this one but its ratio to what a creature can earn in
        /// the time an offspring takes to become self-sufficient, and nothing here knows that
        /// yet. Treated as a placeholder that must be revisited at Milestone 5, not as a value.
        /// </remarks>
        [Tunable("genome")]
        public float MinOffspringEndowment { get; set; } = 50f;
        [Tunable("genome")]
        public float MaxOffspringEndowment { get; set; } = 400f;

        /// <summary>
        /// Cell types a founder's single body cell may be — DESIGN.md §5A.0b.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Every entry earns.</b> <see cref="CellTypeIds.Structural"/> and
        /// <see cref="CellTypeIds.Link"/> acquire nothing, so a founder made only of those has
        /// zero income against nonzero upkeep and starves with certainty, in every world, every
        /// time. Compute spent on it buys nothing but a corpse. Structure stays reachable in one
        /// mutation; it is simply not where a lineage starts.
        /// </para>
        /// <para>
        /// <b>Photosynthesis is weighted double, and the other half is meant to die.</b> At t=0
        /// there is no nutrient in the water and no corpse to bite, so an absorptive or consumer
        /// founder earns exactly nothing — it is born into a world with no food of its kind. It
        /// starves, and its tissue becomes the first nutrient anything has ever had. The doomed
        /// half of generation zero <i>is</i> the primordial soup, and it is what makes the other
        /// two strategies mean something by generation two.
        /// </para>
        /// <para>
        /// So this is not a guess about which strategy is best — that is the world's to decide,
        /// and handing it to photosynthesis outright would make "plants came first" an
        /// arrangement rather than a finding. It is only the ratio that keeps generation zero
        /// from being entirely stillborn.
        /// </para>
        /// </remarks>
        [Tunable("genome")]
        public string[] FounderCellTypes { get; set; } =
        {
            CellTypeIds.Photosynthetic,
            CellTypeIds.Photosynthetic,
            CellTypeIds.Absorptive,
            CellTypeIds.Consumer,
        };

        /// <summary>
        /// Chance a founder gets a link, making it two parts rather than one — §5A.0b.
        /// </summary>
        /// <remarks>
        /// One part is a blob that cannot move. Two is a blob with a beating appendage — the
        /// link is the tail, not a connector to something else — which is the smallest thing in
        /// this encoding that can swim.
        ///
        /// Half and half, so neither is handed the world. A blob pays less upkeep and stays
        /// where it drifts; a flagellate pays for tissue and joint capacity it may never recoup.
        /// Which is the better opening is exactly the sort of question §5A exists to be answered
        /// by rather than to answer.
        /// </remarks>
        [Tunable("genome")]
        public float FounderTailChance { get; set; } = 0.5f;

        /// <summary>
        /// Chance a founder without a tail gets a gas bladder instead — D049.
        /// </summary>
        /// <remarks>
        /// The same argument <see cref="FounderTailChance"/> makes, for the organ that is three
        /// billion years older. A bladder is reachable by mutation — a node added, then its cell
        /// type changed at <c>CellTypeChance</c> = 0.001 — and the intermediate is viable, so
        /// there is no valley to cross; but "reachable eventually" and "reachable inside a run"
        /// are different claims, and the tail is handed out at even odds precisely because the
        /// difference matters.
        ///
        /// <b>Zero by default</b>, unlike the tail. Turning it on is a per-run choice that shows
        /// in the header and the config hash, so a world where buoyancy was *given* is never
        /// confused with one where it was *found* — and which of those happened is most of what
        /// D049 is trying to measure.
        /// </remarks>
        [Tunable("genome")]
        public float FounderFloatChance { get; set; }

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
        public static Genome Random(
            Rng rng, RandomGenomeOptions options = null, SensorChannel[] sensorPool = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            options = options ?? RandomGenomeOptions.Default;

            int nodeCount = rng.Range(options.MinNodes, options.MaxNodes + 1);
            var genome = new Genome
            {
                RootIndex = 0,
                Reproduction = new ReproductionTraits
                {
                    BroodSize = rng.Range(options.MinBroodSize, options.MaxBroodSize + 1),
                    OffspringEndowment =
                        rng.Range(options.MinOffspringEndowment, options.MaxOffspringEndowment),
                },
            };

            // Body cells occupy [0, bodyCount) and links [bodyCount, bodyCount + linkCount).
            // A body's edge may go to either: to a link, and the two ends can actuate; or
            // straight to another body, and they are welded solid at whatever angle the edge
            // gives them. A link's edges always return to a body, since a joint joins two
            // things rather than chaining into another joint.
            //
            // Cycles survive either way, so the recursive encoding is unaffected. What changes
            // is that motion now costs a part: a repeated segment carries its own joint tissue,
            // and a creature with no links at all is a rigid body — which is what a plant is,
            // without "plant" having to be defined anywhere.
            int bodyCount = nodeCount;
            int linkCount = Math.Max(1, nodeCount - 1);

            for (int i = 0; i < bodyCount; i++)
            {
                genome.Nodes.Add(RandomBodyCell(rng, options, sensorPool));
            }

            for (int i = 0; i < linkCount; i++)
            {
                genome.Nodes.Add(RandomLinkCell(rng, options, sensorPool));
            }

            for (int i = 0; i < bodyCount; i++)
            {
                AddBodyEdges(rng, options, genome, i, bodyCount, linkCount);
            }

            for (int i = 0; i < linkCount; i++)
            {
                // Exactly one child: a link joins two things. A link with several children is a
                // branching joint, which has no anatomical meaning and complicates nothing
                // usefully — branching belongs on body cells, which can carry several links.
                genome.Nodes[bodyCount + i].Edges.Add(
                    RandomEdge(rng, options, rng.Range(bodyCount), rng.Range(6)));
            }

            // A root with no edges develops into a single cell, which is not worth simulating.
            if (genome.Nodes[0].Edges.Count == 0)
            {
                genome.Nodes[0].Edges.Add(
                    RandomEdge(rng, options, bodyCount + rng.Range(linkCount), rng.Range(6)));
            }

            return genome;
        }

        /// <summary>
        /// A founder: one earning cell, and half the time a tail — DESIGN.md §5A.0b.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the world's generation zero, and it is deliberately almost nothing.</b>
        /// <see cref="Random"/> builds creatures of two to five nodes developing into three to
        /// sixteen parts, with branches, recursion, bilateral pairs and several joints. That was
        /// right when a fitness function had to have something to grade on the first evaluation.
        /// Under §5A nothing grades anything, so handing evolution a body it did not build makes
        /// every subsequent claim about morphology a claim about our initial conditions.
        /// </para>
        /// <para>
        /// A founder is therefore one cell, or one cell and a beating appendage. Everything
        /// else — branching, symmetry, limbs, recursion, more than one strategy in one
        /// body — has to be discovered, priced, and kept because it paid.
        /// </para>
        /// <para>
        /// <b>Two parts is a flagellate, not a stub.</b> A link is a full part with its own
        /// tissue, upkeep and shape; it does not need a child. So one body cell with one link
        /// hanging off it is a cell with a tail — the smallest thing in this encoding that can
        /// swim — rather than an incomplete two-cell creature waiting for its other half.
        /// </para>
        /// <para>
        /// <b>Nothing here filters for viability, and that is the point.</b>
        /// <see cref="RandomViable"/> rejects creatures with no degrees of freedom, which every
        /// one-part founder has. Under §5A stillness is not a defect — a blob that sits in the
        /// light and pays its bills is a plant, and refusing to spawn it would be an exogenous
        /// judgement about what deserves to exist, which is the whole thing §5A removes.
        /// </para>
        /// </remarks>
        public static Genome Founder(
            Rng rng, RandomGenomeOptions options = null, SensorChannel[] sensorPool = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            options = options ?? RandomGenomeOptions.Default;

            var genome = new Genome
            {
                RootIndex = 0,
                Reproduction = new ReproductionTraits
                {
                    BroodSize = rng.Range(options.MinBroodSize, options.MaxBroodSize + 1),
                    OffspringEndowment =
                        rng.Range(options.MinOffspringEndowment, options.MaxOffspringEndowment),
                },
            };

            MorphNode body = RandomBodyCell(rng, options, sensorPool);

            // Drawn from the earning types only. Structural and Link acquire nothing, so a
            // founder built from them starves with certainty rather than probably — see
            // RandomGenomeOptions.FounderCellTypes.
            body.CellTypeId = rng.Pick(options.FounderCellTypes);

            // Drawn from the same bounds a mutation-born buoyancy cell gets. A founder that is
            // buoyant and holds no gas is a founder whose one distinguishing organ does nothing.
            if (body.CellTypeId == CellTypeIds.Buoyancy)
            {
                body.Lift = rng.Range(options.MinBuoyancyLift, options.MaxBuoyancyLift);
            }

            // No recursion on a founder. RecursiveLimit governs how many times a cycle in the
            // graph re-expands, and a genome with one edge and no cycle has nothing to expand —
            // but leaving a stray value here would come alive the moment mutation added the
            // edge that closes a loop, growing a body nobody selected for.
            body.RecursiveLimit = 1;
            genome.Nodes.Add(body);

            if (rng.Chance(options.FounderTailChance))
            {
                MorphNode tail = RandomLinkCell(rng, options, sensorPool);
                genome.Nodes.Add(tail);

                // One edge, square onto a face, no reflection and no tilt. A bilateral pair or a
                // tilted attachment is morphology, and morphology is what this is refusing to
                // hand out.
                genome.Nodes[0].Edges.Add(new MorphEdge
                {
                    Child = 1,
                    ParentAnchor = AxisVector(1, 1f),
                    ChildAnchor = AxisVector(1, -1f),
                    Orientation = Quat.Identity,
                    Scale = new Float3(1f, 1f, 1f),
                });
            }
            // Short-circuited on the chance itself, not left to rng.Chance(0f). Chance consumes a
            // draw whichever way it answers, so calling it unconditionally would shift the random
            // stream for every run that has this switched off — and "off changes nothing" is the
            // property that lets a new knob be added without invalidating what came before.
            else if (options.FounderFloatChance > 0f && rng.Chance(options.FounderFloatChance))
            {
                // A bladder instead of a tail — the Archean answer to the same question the tail
                // is the Cambrian answer to. Mutually exclusive with the tail rather than stacked,
                // so a founder is a blob, a flagellate, or a float, and the three are comparable.
                MorphNode bladder = RandomBodyCell(rng, options, sensorPool);
                bladder.CellTypeId = CellTypeIds.Buoyancy;
                bladder.JointType = JointType.Fixed;
                bladder.JointLimits = Array.Empty<Float2>();
                bladder.Power = 0f;
                bladder.Lift = rng.Range(options.MinBuoyancyLift, options.MaxBuoyancyLift);
                bladder.RecursiveLimit = 1;
                genome.Nodes.Add(bladder);

                genome.Nodes[0].Edges.Add(new MorphEdge
                {
                    Child = 1,
                    ParentAnchor = AxisVector(1, 1f),
                    ChildAnchor = AxisVector(1, -1f),
                    Orientation = Quat.Identity,
                    Scale = new Float3(1f, 1f, 1f),
                });
            }

            return genome;
        }

        /// <summary>
        /// A body cell: it feeds or it is structure, and it cannot move against its parent.
        /// </summary>
        private static MorphNode RandomBodyCell(
            Rng rng, RandomGenomeOptions options, SensorChannel[] sensorPool)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(
                    rng.Range(options.MinHalfExtent, options.MaxHalfExtent),
                    rng.Range(options.MinHalfExtent, options.MaxHalfExtent),
                    rng.Range(options.MinHalfExtent, options.MaxHalfExtent)),
                CellTypeId = rng.Pick(options.BodyCellTypes),
                ShapeId = rng.Pick(options.ShapeIdChoices),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = rng.Range(options.MinRecursiveLimit, options.MaxRecursiveLimit + 1),
            };

            node.Neurons = RandomNeurons(rng, options, sensorPool);
            return node;
        }

        /// <summary>
        /// A link cell: the only kind that may move, and therefore the only source of motion.
        /// </summary>
        /// <remarks>
        /// Neurons live here rather than on body cells for a reason worth stating: a neuron's
        /// output drives the joint of the part it belongs to (§4.3), so on a rigid body cell it
        /// would have nothing to drive. Putting the controller where the actuator is means
        /// recursion duplicates a segment's muscle and its nerve together, which is what makes
        /// a repeated limb arrive with a working central pattern generator instead of a dead one.
        /// </remarks>
        private static MorphNode RandomLinkCell(
            Rng rng, RandomGenomeOptions options, SensorChannel[] sensorPool)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(
                    rng.Range(options.MinLinkHalfExtent, options.MaxLinkHalfExtent),
                    rng.Range(options.MinLinkHalfExtent, options.MaxLinkHalfExtent),
                    rng.Range(options.MinLinkHalfExtent, options.MaxLinkHalfExtent)),
                CellTypeId = CellTypeIds.Link,
                ShapeId = rng.Pick(options.ShapeIdChoices),
                JointType = rng.Pick(options.JointTypes),
                Power = rng.Range(options.MinLinkPower, options.MaxLinkPower),
                RecursiveLimit = 1,
            };

            int dof = node.JointType.DofCount();
            var limits = new Float2[dof];
            for (int d = 0; d < dof; d++)
            {
                float magnitude = rng.Range(options.MinJointLimit, options.MaxJointLimit);
                limits[d] = new Float2(-magnitude, magnitude);
            }
            node.JointLimits = limits;
            node.Neurons = RandomNeurons(rng, options, sensorPool);

            return node;
        }

        private static NeuronDef[] RandomNeurons(
            Rng rng, RandomGenomeOptions options, SensorChannel[] sensorPool)
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
                    // Drawn from SensorChannels.Implemented rather than pinned to JointAngle.
                    // §4.3 describes the founder population as a pure CPG with self-only
                    // connections; that was never quite what this code did, and logbook/0018 is
                    // the argument for the departure being right — an open-loop swimmer cannot
                    // aim, so locomotion has negative expected value and the world deletes it.
                    // A founder that can read depth has something to steer by (D033).
                    inputs[a] = rng.Chance(0.5f)
                        ? SensorChannels.RandomSensor(rng, rng.Gaussian(0f, 1f), sensorPool)
                        : NeuronInput.FromNeuron(NeuronInputKind.SameNode, rng.Range(count), rng.Gaussian(0f, 1f));
                }
                neurons[i].Inputs = inputs;
            }

            return neurons;
        }

        /// <summary>
        /// Edges from a body cell. Every target is a link, which is what enforces the
        /// alternation — a body cell can never attach directly to another body cell.
        /// </summary>
        private static void AddBodyEdges(
            Rng rng, RandomGenomeOptions options, Genome genome, int nodeIndex, int bodyCount, int linkCount)
        {
            MorphNode node = genome.Nodes[nodeIndex];
            int edgeCount = rng.Range(0, options.MaxEdgesPerNode + 1);

            // Faces are handed out without replacement. Two edges sharing a face put two
            // children in the same place, which is the other way a box ends up inside a box.
            var faces = FreeFaces(rng);

            for (int e = 0; e < edgeCount; e++)
            {
                int target = rng.Chance(options.LinkChance)
                    ? bodyCount + rng.Range(linkCount)
                    : rng.Range(bodyCount);

                node.Edges.Add(RandomEdge(rng, options, target, TakeFace(faces, rng)));
            }

            // A terminal edge is only reachable if some non-terminal edge exists to exhaust.
            if (node.Edges.Count > 0 && rng.Chance(options.TerminalChance))
            {
                int terminalTarget = rng.Chance(options.LinkChance)
                    ? bodyCount + rng.Range(linkCount)
                    : rng.Range(bodyCount);

                MorphEdge terminal = RandomEdge(rng, options, terminalTarget, TakeFace(faces, rng));
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
            float maxUnjointedOverlap = 0.005f,
            SensorChannel[] sensorPool = null)
        {
            Genome last = null;
            Genome bestSoFar = null;
            int fewestBuried = int.MaxValue;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                last = Random(rng, options, sensorPool);
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
