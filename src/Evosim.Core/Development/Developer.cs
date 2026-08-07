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
        public static Phenotype Develop(
            Genome genome,
            DevelopmentLimits limits = null,
            Mat4? rootTransform = null,
            PartShapeRegistry shapes = null)
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
            Expand(
                genome,
                limits,
                shapes,
                phenotype,
                occurrences,
                genome.RootIndex,
                rootTransform ?? Mat4.Identity,
                Float3.One,
                parentPartIndex: -1,
                depth: 0,
                jointType: JointType.Fixed,
                jointLimits: Array.Empty<Float2>(),
                parentAnchorLocal: Float3.Zero,
                childAnchorLocal: Float3.Zero);

            return phenotype;
        }

        private static void Expand(
            Genome genome,
            DevelopmentLimits limits,
            PartShapeRegistry shapes,
            Phenotype phenotype,
            int[] occurrences,
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
            Float3 halfExtents = Float3.Abs(node.Dimensions * accumulatedScale);

            // Volume is the shape's, not the bounding box's. A sphere holds about half what its
            // box does, so pruning on the box would keep parts that mass, upkeep and drag all
            // treat as half the size — three systems disagreeing with the limit that admitted
            // the part.
            PartShape shape = shapes.Resolve(node.ShapeId);
            float volume = shape.Volume(halfExtents);

            if (volume < limits.MinPartVolume)
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
                JointType = jointType,
                JointLimits = jointLimits,
                Power = jointType == JointType.Fixed ? 0f : node.Power,
                ParentAnchorLocal = parentAnchorLocal,
                ChildAnchorLocal = childAnchorLocal,
                Neurons = node.Neurons,
            });

            if (depth >= limits.MaxDepth)
            {
                if (node.Edges.Count > 0) phenotype.PrunedForDepth++;
                return;
            }

            // Recursion is spent when no non-terminal edge can still be followed. Only then
            // do terminal edges fire, which is what puts a differentiated extremity at the
            // tip of a repeating chain rather than on every segment.
            bool exhausted = IsRecursionExhausted(genome, occurrences, node);

            for (int e = 0; e < node.Edges.Count; e++)
            {
                MorphEdge edge = node.Edges[e];
                if (edge.TerminalOnly != exhausted) continue;
                if (!exhausted && !CanEnter(genome, occurrences, edge.Child)) continue;

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

                foreach (Bool3 mirror in edge.Reflect.MirrorCombinations())
                {
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
                    Expand(
                        genome,
                        limits,
                        shapes,
                        phenotype,
                        occurrences,
                        edge.Child,
                        transform * local,
                        childScale,
                        part.Index,
                        depth + 1,
                        childNode.JointType,
                        childLimits,
                        anchorOnParent,
                        anchorOnChild);
                    occurrences[edge.Child]--;

                    if (phenotype.PartCount >= limits.MaxParts) return;
                }
            }
        }

        /// <summary>
        /// A node may be entered again while it occurs fewer times on the current path than
        /// its <see cref="MorphNode.RecursiveLimit"/>. A self-loop with a limit of 5 therefore
        /// yields a five-segment spine, as DESIGN.md §4.1 describes.
        /// </summary>
        private static bool CanEnter(Genome genome, int[] occurrences, int childIndex) =>
            occurrences[childIndex] < genome.Nodes[childIndex].RecursiveLimit;

        private static bool IsRecursionExhausted(Genome genome, int[] occurrences, MorphNode node)
        {
            for (int e = 0; e < node.Edges.Count; e++)
            {
                MorphEdge edge = node.Edges[e];
                if (edge.TerminalOnly) continue;
                if (CanEnter(genome, occurrences, edge.Child)) return false;
            }
            return true;
        }
    }
}
