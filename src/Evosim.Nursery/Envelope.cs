using System;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// search.evolveEnvelope (the owner suggested on 2026-10-01 that the envelope belong to the
    /// nursery's experiment genome rather than stay fixed): under search.evolve brain the child
    /// keeps its parent's body, and the joint's four drive genes (D135's threshold, reaction, hold
    /// and release) are part of that body, so they never evolved. With the flag, the mutant's drive
    /// genes are kept beside its neurons on every node that is a joint in both the body and the
    /// mutant; everything else of the body stays the parent's.
    /// </summary>
    /// <remarks>
    /// The mutation is the world's own: <see cref="Mutator"/> already steps each gene at the
    /// config's <c>driveGeneChance</c> and holds it in its range, and the brain-only graft has
    /// been throwing those steps away. Keeping them draws nothing new, so the random stream is the
    /// same with the flag on or off and only what the child keeps differs. The crossover passes
    /// neurons only: a crossed child carries its base's envelope, and the brain distance that
    /// search.crossover.mutationByDistance reads does not count the envelope.
    /// </remarks>
    public static class Envelope
    {
        /// <summary>
        /// Copies <paramref name="from"/>'s drive genes into <paramref name="into"/> on every node
        /// that is a joint in both. The two must have the same node count.
        /// </summary>
        public static void Take(Genome into, Genome from)
        {
            if (into.Nodes.Count != from.Nodes.Count)
                throw new ArgumentException($"node counts differ: {into.Nodes.Count} and {from.Nodes.Count}");
            for (int i = 0; i < into.Nodes.Count; i++)
                if (into.Nodes[i].JointType.DofCount() > 0 && from.Nodes[i].JointType.DofCount() > 0)
                    into.Nodes[i].Drive = from.Nodes[i].Drive;
        }

        /// <summary>The mean drive genes over every joint of the genomes (NaN when there is none), and how many joints that was.</summary>
        public static (double threshold, double reaction, double hold, double release, int joints) Mean(System.Collections.Generic.IEnumerable<Genome> genomes)
        {
            double t = 0, r = 0, h = 0, l = 0;
            int n = 0;
            foreach (Genome g in genomes)
                foreach (MorphNode node in g.Nodes)
                {
                    if (node.JointType.DofCount() == 0) continue;
                    t += node.Drive.Threshold;
                    r += node.Drive.Reaction;
                    h += node.Drive.Hold;
                    l += node.Drive.Release;
                    n++;
                }
            return n == 0 ? (double.NaN, double.NaN, double.NaN, double.NaN, 0) : (t / n, r / n, h / n, l / n, n);
        }
    }
}
