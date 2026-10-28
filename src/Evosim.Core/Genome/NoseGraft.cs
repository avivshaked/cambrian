using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>What <see cref="NoseGraft.Graft"/> made and what it moved.</summary>
    public sealed class NoseGraftResult
    {
        /// <summary>The grafted genome: a copy, the original untouched.</summary>
        public Genome Genome { get; internal set; }

        /// <summary>The nose's node in <see cref="Genome"/>: the last.</summary>
        public int NoseNode { get; internal set; }

        /// <summary>The nose part's path id, which its smell ports hash.</summary>
        public ulong NosePath { get; internal set; }

        /// <summary>A mirrored pair's twin nose's path id; 0 when the graft is one nose.</summary>
        public ulong TwinPath { get; internal set; }

        /// <summary>The nose's surface area as developed, m².</summary>
        public float NoseArea { get; internal set; }

        /// <summary>The smell links taken off the body's other parts.</summary>
        public int LinksMoved { get; internal set; }

        /// <summary>The links they became on the nose: one per (port, target), weights summed.</summary>
        public int LinksMade { get; internal set; }

        /// <summary>
        /// Of <see cref="LinksMoved"/>, the direction-form links from a part other than the host, whose
        /// frame is not the nose's: their components are summed on the nose's axes as if it were.
        /// </summary>
        public int DirectionLinksReframed { get; internal set; }
    }

    /// <summary>
    /// D235's graft: a nose added to a bred body, for a film of a nose at work before any
    /// lineage has evolved one. A genome edit and nothing else, so it needs no new tunable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The body keeps every part and every port it had.</b> The nose is a new node reached by
    /// a new edge appended to the host node's edges, and a part's path id is hashed from edge ids
    /// (<see cref="BrainIds.Child"/>), which <see cref="Genome.NumberEdges"/> leaves alone on every
    /// edge that has one. So each old part keeps its path id, and each link from a joint sense or
    /// to an output still names the same thing. Appended last, the edge also leaves every
    /// earlier part's index and the host's first child alone, and the root's mouth looks along its
    /// first child (<see cref="MouthPoint"/>). A graft that would change any of that is refused.
    /// </para>
    /// <para>
    /// <b>The smell links move to the nose.</b> In a world whose table lists the nose, the old
    /// parts' smell ports read nothing (<see cref="CentralWiring.IndicesOn"/>). A link from one of
    /// them is moved onto the nose's port of the same channel and index, and links that land on one
    /// (port, target) pair are summed: a body whose parts all read one field read it as the sum of
    /// their weights, and so does the nose. That is exact for a scalar smell read at one point and
    /// approximate for one read across a body. The direction form is approximate twice over,
    /// since a limb reads its gradient in its own frame; the result counts those links.
    /// </para>
    /// <para>
    /// <b>The shape is a rhinophore's</b>: a box three times as long as it is wide, standing on
    /// the host's face along <c>axis</c>, on a fixed joint, with no claw, mouth or armour. The
    /// proportion is an inference from the reference pictures, not a measurement. Its size is set
    /// so the developed nose has <c>area</c>, at the genome's own adult scale.
    /// </para>
    /// </remarks>
    /// <summary>Where a mirrored pair's moved smell links go (<see cref="NoseGraft.Graft"/>).</summary>
    public enum NosePairLinks
    {
        /// <summary>Every moved link onto the first nose; the twin's ports are read by nothing.</summary>
        First,

        /// <summary>Each moved link onto both noses at half its weight.</summary>
        Split,
    }

    public static class NoseGraft
    {
        /// <summary>The nose's length over its width, unless the caller names another.</summary>
        public const float Slenderness = 3f;

        /// <param name="genome">A genome with a central brain (D146). Not changed.</param>
        /// <param name="cellTypes">A table that lists the nose: the world the grafted body is to live in.</param>
        /// <param name="hostNode">The node the nose stands on. It must develop exactly once.</param>
        /// <param name="parentAnchor">Where on the host, in its normalised box coordinates, on the face <paramref name="axis"/> names.</param>
        /// <param name="axis">The way the nose points, in the host's frame: one of ±x, ±y, ±z.</param>
        /// <param name="area">The developed nose's surface area, m². Zero or less is the table's reference area.</param>
        /// <param name="limits">The world's development limits. Null is the default.</param>
        /// <param name="shapes">The world's shapes. Null is the standard table.</param>
        /// <param name="slenderness">The nose's length over its width. Zero or less is <see cref="Slenderness"/>.</param>
        /// <param name="mirror">
        /// An axis of the host's frame (0, 1, 2) to reflect the nose's edge in, so development makes a mirrored pair, or -1 for
        /// one nose. The pair needs <see cref="DevelopmentLimits.ReflectMirroredAnchors"/> (D228), without which the twin is
        /// stacked on the first, and an anchor off the mirror plane, without which the two coincide.
        /// </param>
        /// <param name="pairLinks">Where a pair's moved smell links go. One nose takes them all whatever this says.</param>
        public static NoseGraftResult Graft(
            Genome genome,
            CellTypeRegistry cellTypes,
            int hostNode,
            Float3 parentAnchor,
            Float3 axis,
            float area = 0f,
            DevelopmentLimits limits = null,
            PartShapeRegistry shapes = null,
            float slenderness = 0f,
            int mirror = -1,
            NosePairLinks pairLinks = NosePairLinks.First)
        {
            if (!(slenderness > 0f)) slenderness = Slenderness;
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (cellTypes == null || !(cellTypes.Contains(CellTypeIds.Nose) && cellTypes.Resolve(CellTypeIds.Nose) is NoseCell nose))
                throw new ArgumentException("The table does not list the nose, so a nose would not develop in it.", nameof(cellTypes));
            if (genome.Brain == null)
                throw new ArgumentException("A per-part brain has no ports to move; only a central brain (D146) is grafted.", nameof(genome));
            if (hostNode < 0 || hostNode >= genome.Nodes.Count)
                throw new ArgumentOutOfRangeException(nameof(hostNode), hostNode, $"The genome has {genome.Nodes.Count} nodes.");
            foreach (MorphNode node in genome.Nodes)
            {
                if (node.CellTypeId == CellTypeIds.Nose)
                    throw new ArgumentException("The genome already carries a nose.", nameof(genome));
            }

            int along = AxisIndex(axis);
            float sign = Component(axis, along);
            if (Component(parentAnchor, along) != sign)
                throw new ArgumentException($"The anchor {parentAnchor} is not on the face {axis} names.", nameof(parentAnchor));
            for (int i = 0; i < 3; i++)
            {
                float c = Component(parentAnchor, i);
                if (!(c >= -1f && c <= 1f))
                    throw new ArgumentException($"The anchor {parentAnchor} is outside the host's box.", nameof(parentAnchor));
            }

            bool pair = mirror >= 0;
            if (mirror < -1 || mirror > 2)
                throw new ArgumentOutOfRangeException(nameof(mirror), mirror, "The mirror is an axis, 0, 1 or 2, or -1 for none.");
            if (pair && !(limits != null && limits.ReflectMirroredAnchors))
                throw new ArgumentException("A mirrored pair needs the world's development.reflectMirroredAnchors (D228); without it the twin is stacked on the first nose.", nameof(mirror));
            if (pair && mirror != along && Component(parentAnchor, mirror) == 0f)
                throw new ArgumentException($"The anchor {parentAnchor} is on the mirror plane, so the twin would stand where the first nose stands.", nameof(parentAnchor));

            if (!(area > 0f)) area = nose.ReferenceAreaSquareMetres;

            Phenotype before = Developer.Develop(genome, limits, null, shapes, cellTypes: cellTypes);
            int hostParts = 0;
            int hostPart = -1;
            foreach (PhenotypePart part in before.Parts)
            {
                if (part.SourceNode != hostNode) continue;
                hostParts++;
                hostPart = part.Index;
            }

            if (hostParts != 1)
                throw new ArgumentException($"Node {hostNode} develops {hostParts} times; a nose is grafted onto a node that develops once.", nameof(hostNode));

            Genome grafted = genome.Clone();

            // The width that gives the area for a box w by w by slenderness·w (half-extents),
            // before the adult scale. Development's scales are then corrected for by measuring.
            float halfWidth = (float)Math.Sqrt(area / (8.0 * (1.0 + 2.0 * slenderness)));
            var noseNode = new MorphNode
            {
                Dimensions = Along(along, halfWidth, halfWidth * slenderness),
                CellTypeId = CellTypeIds.Nose,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            };
            grafted.Nodes.Add(noseNode);
            int noseIndex = grafted.Nodes.Count - 1;
            grafted.Nodes[hostNode].Edges.Add(new MorphEdge
            {
                Child = noseIndex,
                ParentAnchor = parentAnchor,
                ChildAnchor = new Float3(-axis.X, -axis.Y, -axis.Z),
                Reflect = new Bool3(mirror == 0, mirror == 1, mirror == 2),
            });
            grafted.NumberEdges();

            Phenotype after = null;
            PhenotypePart nosePart = null, twinPart = null;
            for (int pass = 0; pass < 4; pass++)
            {
                after = Developer.Develop(grafted, limits, null, shapes, cellTypes: cellTypes);
                (nosePart, twinPart) = TheNoses(after, pair);
                float ratio = nosePart.SurfaceArea / area;
                if (Math.Abs(ratio - 1f) < 1e-4f) break;
                float s = (float)Math.Sqrt(area / nosePart.SurfaceArea);
                noseNode.Dimensions = new Float3(noseNode.Dimensions.X * s, noseNode.Dimensions.Y * s, noseNode.Dimensions.Z * s);
            }

            if (Math.Abs(nosePart.SurfaceArea / area - 1f) >= 1e-3f)
                throw new InvalidOperationException($"The nose develops at {nosePart.SurfaceArea} m², not {area} m²; a limit holds its size.");

            // Every old part is still there, at its index, with its path and its tissue.
            int added = pair ? 2 : 1;
            if (after.Parts.Count != before.Parts.Count + added)
                throw new InvalidOperationException($"The graft took the body from {before.Parts.Count} parts to {after.Parts.Count}; it adds {added}.");
            for (int i = 0; i < before.Parts.Count; i++)
            {
                PhenotypePart was = before.Parts[i], now = after.Parts[i];
                if (was.PathId != now.PathId || was.SourceNode != now.SourceNode || was.CellTypeId != now.CellTypeId)
                    throw new InvalidOperationException($"Part {i} is not the part it was after the graft.");
            }

            if (nosePart.Index != before.Parts.Count || (pair && twinPart.Index != before.Parts.Count + 1))
                throw new InvalidOperationException("The nose did not develop last.");
            if (pair && Math.Abs(twinPart.SurfaceArea / nosePart.SurfaceArea - 1f) >= 1e-4f)
                throw new InvalidOperationException($"The twin develops at {twinPart.SurfaceArea} m², not the first nose's {nosePart.SurfaceArea} m².");
            if (!Same(MouthPoint.Local(before, before.Parts[0].Index), MouthPoint.Local(after, after.Parts[0].Index)))
                throw new InvalidOperationException("The graft moved the root's mouth.");

            // The smell links, moved and summed.
            var smellOf = new Dictionary<ulong, (SensorChannel Channel, int Index, ulong Path)>();
            foreach (PhenotypePart part in before.Parts)
            {
                foreach (SensorChannel channel in (SensorChannel[])Enum.GetValues(typeof(SensorChannel)))
                {
                    if (!channel.IsSmell()) continue;
                    for (int i = 0; i < channel.IndexCount(); i++)
                        smellOf[BrainIds.Port(part.PathId, channel, i)] = (channel, i, part.PathId);
                }
            }

            ulong hostPath = before.Parts[hostPart].PathId;
            var result = new NoseGraftResult
            {
                Genome = grafted, NoseNode = noseIndex, NosePath = nosePart.PathId, TwinPath = pair ? twinPart.PathId : 0UL,
                NoseArea = nosePart.SurfaceArea,
            };

            // Onto the first nose at the link's weight, or onto both at half of it.
            var targets = pair && pairLinks == NosePairLinks.Split
                ? new[] { (Path: nosePart.PathId, Share: 0.5f), (Path: twinPart.PathId, Share: 0.5f) }
                : new[] { (Path: nosePart.PathId, Share: 1f) };
            var kept = new List<BrainLink>();
            var made = new List<BrainLink>();
            var byPair = new Dictionary<(ulong From, ulong To), BrainLink>();
            foreach (BrainLink link in grafted.Brain.Links)
            {
                if (!smellOf.TryGetValue(link.From, out var smell))
                {
                    kept.Add(link);
                    continue;
                }

                result.LinksMoved++;
                if (smell.Channel == SensorChannel.LeafScentDirection && smell.Path != hostPath) result.DirectionLinksReframed++;
                foreach ((ulong path, float share) in targets)
                {
                    ulong from = BrainIds.Port(path, smell.Channel, smell.Index);
                    float weight = link.Weight * share;
                    if (byPair.TryGetValue((from, link.To), out BrainLink into))
                    {
                        into.Weight += weight;
                        continue;
                    }

                    var moved = new BrainLink { From = from, To = link.To, Weight = weight };
                    byPair[(from, link.To)] = moved;
                    made.Add(moved);
                }
            }

            grafted.Brain.Links.Clear();
            grafted.Brain.Links.AddRange(kept);
            grafted.Brain.Links.AddRange(made);
            result.LinksMade = made.Count;

            IReadOnlyList<string> issues = grafted.Validate(cellTypes);
            if (issues.Count > 0)
                throw new InvalidOperationException("The grafted genome is not well formed: " + string.Join("; ", issues));

            return result;
        }

        // The nose, and a pair's twin: the one whose frame is reflected.
        private static (PhenotypePart Nose, PhenotypePart Twin) TheNoses(Phenotype body, bool pair)
        {
            var found = new List<PhenotypePart>();
            foreach (PhenotypePart part in body.Parts)
            {
                if (part.CellTypeId == CellTypeIds.Nose) found.Add(part);
            }

            if (found.Count == 0) throw new InvalidOperationException("The nose did not develop: a limit pruned it.");
            if (found.Count != (pair ? 2 : 1))
                throw new InvalidOperationException($"The graft developed {found.Count} noses, not {(pair ? 2 : 1)}.");
            if (!pair) return (found[0], null);
            if (found[0].Mirrored == found[1].Mirrored)
                throw new InvalidOperationException("The pair's two noses are both reflected or both not.");
            return found[0].Mirrored ? (found[1], found[0]) : (found[0], found[1]);
        }

        private static int AxisIndex(Float3 axis)
        {
            int index = -1;
            for (int i = 0; i < 3; i++)
            {
                float c = Component(axis, i);
                if (c == 0f) continue;
                if (index >= 0 || (c != 1f && c != -1f)) index = -2;
                else index = i;
            }

            if (index < 0) throw new ArgumentException($"The axis {axis} is not one of ±x, ±y, ±z.", nameof(axis));
            return index;
        }

        private static float Component(Float3 v, int i) => i == 0 ? v.X : i == 1 ? v.Y : v.Z;

        private static Float3 Along(int axis, float across, float length) =>
            new Float3(axis == 0 ? length : across, axis == 1 ? length : across, axis == 2 ? length : across);

        private static bool Same(Float3 a, Float3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }
}
