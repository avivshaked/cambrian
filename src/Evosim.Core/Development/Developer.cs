using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Genotype → phenotype. Depth-first traversal of the morphology graph, unfolding
    /// cycles into repeated segments and reflection flags into mirrored copies.
    /// DESIGN.md §4.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All geometric transforms are cumulative down the subtree, per [K12 §2.1, p.3]:
    /// <i>"they are applied to the entire subtree of the phenotype graph during its
    /// construction."</i>
    /// </para>
    /// <para>
    /// <b>Scale is tracked separately from the transform matrix.</b> The matrix carries
    /// rotation, reflection and translation only, so it stays orthogonal up to sign and
    /// decomposes cleanly. Accumulated scale is instead folded into part half-extents and
    /// into the anchor points derived from them. Baking scale into the matrix would make
    /// every anchor computation depend on decomposing a sheared basis, which is both slower
    /// and easier to get subtly wrong.
    /// </para>
    /// </remarks>
    public static class Developer
    {
        /// <summary>
        /// Develops <paramref name="genome"/> into a tree of parts.
        /// </summary>
        /// <param name="genome">Must pass <see cref="Genome.Validate"/>; an invalid genome throws.</param>
        /// <param name="limits">Guard rails. Defaults to <see cref="DevelopmentLimits.Default"/>.</param>
        /// <param name="rootTransform">Where to place the root. Defaults to the origin.</param>
        /// <param name="shapes">
        /// Geometry each node's shape id resolves against. Defaults to
        /// <see cref="PartShapeRegistry.Standard"/>. A run using custom shapes must pass its own,
        /// or its genomes will fail to resolve rather than silently developing as boxes.
        /// </param>
        /// <param name="moduleCounts">
        /// How many times each <see cref="ModuleGrowth.Indeterminate"/> node may occur along one
        /// path — D106 item 2, <c>Organism.ModuleCounts</c>. Null is the genome's own
        /// <see cref="MorphNode.RecursiveLimit"/> for every node, which is what every birth
        /// develops at and what every determinate genome develops at for ever.
        /// </param>
        /// <param name="partPaths">
        /// Filled, when it is not null, with one entry per part in the order the parts are added:
        /// the sequence of steps from the root that reached it, each step an edge index and a
        /// mirror ordinal. <b>It is the only thing that identifies the same part across two
        /// developments of one genome at different counts</b> — part indices do not survive,
        /// because an extra copy of a node is inserted in depth-first order and everything after
        /// it shifts. <c>World.ApplyModuleRule</c> uses it to carry a body's joint state and its
        /// brain across a rebuild. Null on every other call, and nothing is allocated for it.
        /// </param>
        public static Phenotype Develop(
            Genome genome,
            DevelopmentLimits limits = null,
            Mat4? rootTransform = null,
            PartShapeRegistry shapes = null,
            int[] moduleCounts = null,
            List<int[]> partPaths = null)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            shapes = shapes ?? PartShapeRegistry.Standard;

            IReadOnlyList<string> issues = genome.Validate();
            if (issues.Count > 0)
            {
                throw new ArgumentException(
                    "Genome is not well-formed and cannot be developed:" +
                    Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", issues),
                    nameof(genome));
            }

            limits = limits ?? DevelopmentLimits.Default;

            var phenotype = new Phenotype { Limits = limits.Clone() };
            var occurrences = new int[genome.Nodes.Count];

            occurrences[genome.RootIndex] = 1;

            // fable-propose-growth.md rule 1: the accumulated scale starts at the genome's adult
            // size rather than at one, so the plan and its size are separate things to mutate.
            // Applied here rather than to each node's dimensions because it must reach the edge
            // scales' compounding too — a subtree two edges down is this scalar times whatever
            // its edges made of it, which is what keeps a scaled body the same shape.
            var adultScale = new Float3(genome.AdultScale, genome.AdultScale, genome.AdultScale);

            Expand(
                genome,
                limits,
                shapes,
                phenotype,
                occurrences,
                moduleCounts,
                partPaths,
                partPaths == null ? null : new List<int>(limits.MaxDepth + 1),
                genome.RootIndex,
                rootTransform ?? Mat4.Identity,
                adultScale,
                parentPartIndex: -1,
                depth: 0,
                jointType: JointType.Fixed,
                jointLimits: Array.Empty<Float2>(),
                parentAnchorLocal: Float3.Zero,
                childAnchorLocal: Float3.Zero);

            // D099: the body's own shadow, once the body is whole. It cannot be accumulated part
            // by part the way lit area is — a hull is a property of the whole cloud — and it is
            // read every metabolic step, so it is measured here and carried rather than asked
            // for again.
            phenotype.MeasureSilhouette();

            return phenotype;
        }

        private static void Expand(
            Genome genome,
            DevelopmentLimits limits,
            PartShapeRegistry shapes,
            Phenotype phenotype,
            int[] occurrences,
            int[] moduleCounts,
            List<int[]> partPaths,
            List<int> path,
            int nodeIndex,
            Mat4 transform,
            Float3 accumulatedScale,
            int parentPartIndex,
            int depth,
            JointType jointType,
            Float2[] jointLimits,
            Float3 parentAnchorLocal,
            Float3 childAnchorLocal)
        {
            MorphNode node = genome.Nodes[nodeIndex];
            Float3 halfExtents = Thicken(
                Float3.Abs(node.Dimensions * accumulatedScale), limits.MinPartHalfExtent);

            // Volume is the shape's, not the bounding box's. A sphere holds about half what its
            // box does, so pruning on the box would keep parts that mass, upkeep and drag all
            // treat as half the size — three systems disagreeing with the limit that admitted
            // the part.
            PartShape shape = shapes.Resolve(node.ShapeId);
            float volume = shape.Volume(halfExtents);

            // Both tails, one counter. A part is dropped for being unrepresentably small (§4.5's
            // extinction by shrinking) or unrepresentably large (DevelopmentLimits.MaxPartVolume),
            // and in both cases the whole subtree goes with it.
            if (volume < limits.MinPartVolume || volume > limits.MaxPartVolume)
            {
                phenotype.PrunedForVolume++;
                return;
            }

            if (phenotype.PartCount >= limits.MaxParts)
            {
                phenotype.PrunedForParts++;
                return;
            }

            transform.Decompose(out Float3 position, out Quat rotation, out _, out bool mirrored);

            PhenotypePart part = phenotype.Add(new PhenotypePart
            {
                ParentIndex = parentPartIndex,
                SourceNode = nodeIndex,
                Depth = depth,
                HalfExtents = halfExtents,
                Position = position,
                Rotation = rotation,
                Mirrored = mirrored,
                CellTypeId = node.CellTypeId,
                ShapeId = node.ShapeId,
                Volume = volume,
                SurfaceArea = shape.SurfaceArea(halfExtents),
                JointType = jointType,
                JointLimits = jointLimits,
                Power = jointType == JointType.Fixed ? 0f : node.Power,

                // Only a buoyancy cell holds gas. Belt-and-braces rather than load-bearing:
                // Develop validates first and Genome.Validate already rejects lift on any other
                // cell type, so this branch is unreachable through the public path. Kept because
                // it costs nothing and states the invariant where the field is assigned.
                Lift = node.CellTypeId == CellTypeIds.Buoyancy ? node.Lift : 0f,

                // D106 item 3's four, carried onto the part unconditionally: unlike lift they are
                // legal on every cell type, and what bounds them is the type's own cap, which
                // Genome.Validate has already refused a genome for exceeding.
                Attack = node.Attack,
                Intake = node.Intake,
                Protection = node.Protection,
                Toughness = node.Toughness,
                ParentAnchorLocal = parentAnchorLocal,
                ChildAnchorLocal = childAnchorLocal,
                Neurons = node.Neurons,
            });

            // Beside the part and in the same order, so partPaths[i] is the path of Parts[i].
            partPaths?.Add(path.ToArray());

            if (depth >= limits.MaxDepth)
            {
                if (node.Edges.Count > 0) phenotype.PrunedForDepth++;
                return;
            }

            // Recursion is spent when no non-terminal edge can still be followed. Only then
            // do terminal edges fire, which is what puts a differentiated extremity at the
            // tip of a repeating chain rather than on every segment.
            bool exhausted = IsRecursionExhausted(genome, occurrences, moduleCounts, node);

            for (int e = 0; e < node.Edges.Count; e++)
            {
                MorphEdge edge = node.Edges[e];
                if (edge.TerminalOnly != exhausted) continue;

                // D099, 2026-09-19: the recursive limit binds every edge, terminal or not. A
                // terminal edge says *when* it fires, once the repeating part of the chain is
                // spent; it never said the child could be entered more often than its own limit
                // allows. Until this, a terminal-only self-edge skipped the check and unfolded to
                // MaxDepth: round 41c grew a sixteen-part ball from a node with a limit of 1 and
                // earned sixteen parts' light from one point (logbook/0107). With the check asked
                // of it, such an edge grows nothing past the node itself, which is what a
                // terminal extremity is for; a non-terminal self-edge with limit n still grows an
                // n-segment spine, unchanged.
                if (!CanEnter(genome, occurrences, moduleCounts, edge.Child)) continue;

                MorphNode childNode = genome.Nodes[edge.Child];
                Float3 childScale = accumulatedScale * edge.Scale;
                Float3 childHalfExtents = Float3.Abs(childNode.Dimensions * childScale);

                // Anchors are directions, and each shape decides where its own surface is. For a
                // box that is a point on a face, as before; for a sphere or capsule it is a
                // point on the curve. Scaling by half-extents instead would attach children to a
                // bounding box that is not there, leaving a visible gap on every round part.
                Float3 anchorOnParent = shape.SurfacePoint(edge.ParentAnchor, halfExtents);
                Float3 anchorOnChild = shapes.Resolve(childNode.ShapeId)
                    .SurfacePoint(edge.ChildAnchor, childHalfExtents);

                int mirrorOrdinal = -1;

                foreach (Bool3 mirror in edge.Reflect.MirrorCombinations())
                {
                    mirrorOrdinal++;

                    // Place the child so its own anchor lands on the parent's anchor, then
                    // mirror the whole placement about the parent's local planes.
                    Mat4 local =
                        Mat4.Mirror(mirror) *
                        Mat4.Translate(anchorOnParent) *
                        Mat4.Rotate(edge.Orientation) *
                        Mat4.Translate(-anchorOnChild);

                    Float2[] childLimits = childNode.JointLimits.Length == 0
                        ? Array.Empty<Float2>()
                        : (Float2[])childNode.JointLimits.Clone();

                    occurrences[edge.Child]++;

                    // One step of the path: which edge was followed, and which of that edge's
                    // mirror copies this is. Both are properties of the genome and of nothing
                    // else, so the same step names the same child in a development at any count.
                    // A mirror ordinal is under 8 by construction (Bool3.MirrorCombinations), an
                    // edge index is not bounded, so the step is a pair and not a packed integer.
                    path?.Add(e);
                    path?.Add(mirrorOrdinal);

                    Expand(
                        genome,
                        limits,
                        shapes,
                        phenotype,
                        occurrences,
                        moduleCounts,
                        partPaths,
                        path,
                        edge.Child,
                        transform * local,
                        childScale,
                        part.Index,
                        depth + 1,
                        childNode.JointType,
                        childLimits,
                        anchorOnParent,
                        anchorOnChild);

                    if (path != null) path.RemoveRange(path.Count - 2, 2);
                    occurrences[edge.Child]--;

                    if (phenotype.PartCount >= limits.MaxParts) return;
                }
            }
        }

        /// <summary>
        /// Raises any half-extent below <see cref="DevelopmentLimits.MinPartHalfExtent"/> to it.
        /// </summary>
        /// <remarks>
        /// Applied before volume is measured, so the volume limits see the body that will actually
        /// be built rather than the one the genome asked for. Checking the genome's figure instead
        /// would prune a wafer for being under the volume floor and then build nothing, even
        /// though the thickened part is comfortably above it.
        ///
        /// A NaN half-extent is thickened too — it fails every comparison, so it would otherwise
        /// pass both volume limits untouched and poison every quantity derived from it.
        /// </remarks>
        private static Float3 Thicken(Float3 h, float floor) =>
            new Float3(
                h.X > floor ? h.X : floor,
                h.Y > floor ? h.Y : floor,
                h.Z > floor ? h.Z : floor);

        /// <summary>
        /// A node may be entered again while it occurs fewer times on the current path than
        /// <see cref="CountFor"/> allows. A self-loop with a limit of 5 therefore yields a
        /// five-segment spine, as DESIGN.md §4.1 describes.
        /// </summary>
        private static bool CanEnter(
            Genome genome, int[] occurrences, int[] moduleCounts, int childIndex) =>
            occurrences[childIndex] < CountFor(genome, moduleCounts, childIndex);

        /// <summary>
        /// How many times a node may occur along one path: its
        /// <see cref="MorphNode.RecursiveLimit"/>, or the body's own count where the node is
        /// <see cref="ModuleGrowth.Indeterminate"/> and a count is supplied — D106 item 2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A determinate node ignores the array entirely</b>, which is what makes "develops
        /// identically with and without the counts" true by construction rather than by test —
        /// though the test exists anyway (<c>DevelopmentTests</c>), because a construction is
        /// only true until somebody edits it.
        /// </para>
        /// <para>
        /// And the count is never below the genome's own minimum. <see cref="MorphNode.MaxModules"/>
        /// and <see cref="MorphNode.RecursiveLimit"/> both mutate, so a stored count can find
        /// itself under a limit that has moved up; taking the larger means development builds
        /// the body the genome describes and the rule can only add to it.
        /// </para>
        /// </remarks>
        public static int CountFor(Genome genome, int[] moduleCounts, int nodeIndex)
        {
            MorphNode node = genome.Nodes[nodeIndex];
            if (node.Growth != ModuleGrowth.Indeterminate) return node.RecursiveLimit;
            if (moduleCounts == null || nodeIndex >= moduleCounts.Length) return node.RecursiveLimit;

            int count = moduleCounts[nodeIndex];
            return count < node.RecursiveLimit ? node.RecursiveLimit : count;
        }

        /// <summary>
        /// For each part of the second development, the index of the same part in the first, or
        /// -1 where there is none — D106 item 2's rebuild (rule 7).
        /// </summary>
        /// <param name="from">Part paths of the body as it was — <c>Develop</c>'s <c>partPaths</c>.</param>
        /// <param name="to">Part paths of the body as it now is.</param>
        /// <remarks>
        /// <para>
        /// <b>Part indices are not the map, and the obvious reading of "all the others keep their
        /// order" is wrong.</b> A module is an extra occurrence of a node, inserted where
        /// depth-first order puts it, so everything after it shifts: a genome whose root has an
        /// indeterminate child <c>A</c> and a second child <c>B</c> develops <c>R A A B</c> at two
        /// modules and <c>R A A A B</c> at three, and <c>B</c> moves from index 3 to index 4. What
        /// survives is the <i>path</i> — which edge was followed and which mirror copy this is,
        /// at every step from the root — because that is a property of the genome and a count
        /// changes only how many times a step may be taken.
        /// </para>
        /// <para>
        /// <b>And a count can change the body in more than one place.</b> A node reachable down
        /// two branches gains an occurrence in both, and a node whose recursion stops being spent
        /// stops firing its terminal-only edges — so a re-development is not in general one
        /// contiguous insertion, and a prefix-and-suffix match would be wrong on exactly the
        /// genomes that are hardest to reason about. Matching on paths is right on all of them.
        /// </para>
        /// <para>
        /// A linear scan per part rather than a dictionary: a body is at most
        /// <see cref="DevelopmentLimits.MaxParts"/> parts and a path at most
        /// <see cref="DevelopmentLimits.MaxDepth"/> steps, so this is a few hundred integer
        /// comparisons on an event that happens at most once per body per growth step.
        /// </para>
        /// </remarks>
        public static int[] MatchParts(IReadOnlyList<int[]> from, IReadOnlyList<int[]> to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            var map = new int[to.Count];

            for (int i = 0; i < to.Count; i++)
            {
                map[i] = -1;
                int[] path = to[i];

                for (int j = 0; j < from.Count; j++)
                {
                    if (!SamePath(from[j], path)) continue;
                    map[i] = j;
                    break;
                }
            }

            return map;
        }

        private static bool SamePath(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool IsRecursionExhausted(
            Genome genome, int[] occurrences, int[] moduleCounts, MorphNode node)
        {
            for (int e = 0; e < node.Edges.Count; e++)
            {
                MorphEdge edge = node.Edges[e];
                if (edge.TerminalOnly) continue;
                if (CanEnter(genome, occurrences, moduleCounts, edge.Child)) return false;
            }
            return true;
        }
    }
}
