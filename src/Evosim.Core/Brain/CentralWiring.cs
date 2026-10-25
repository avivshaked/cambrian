using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>One input port of a developed body: one sense of one part (D146).</summary>
    public readonly struct BrainPort
    {
        public BrainPort(ulong id, int part, SensorChannel channel, int index)
        {
            Id = id;
            Part = part;
            Channel = channel;
            Index = index;
        }

        public ulong Id { get; }
        public int Part { get; }
        public SensorChannel Channel { get; }
        public int Index { get; }
    }

    /// <summary>One output of a developed body: the power or the bend of one degree of freedom (D146).</summary>
    public readonly struct BrainOutput
    {
        public BrainOutput(ulong id, int part, int dof, BrainOutputKind kind, int driveIndex)
        {
            Id = id;
            Part = part;
            Dof = dof;
            Kind = kind;
            DriveIndex = driveIndex;
        }

        public ulong Id { get; }
        public int Part { get; }
        public int Dof { get; }
        public BrainOutputKind Kind { get; }

        /// <summary>Index into the drive and bend arrays <see cref="Brain.Step"/> fills.</summary>
        public int DriveIndex { get; }
    }

    /// <summary>
    /// Where a <see cref="CentralBrain"/> meets a body: its ports, its outputs, and the brain a
    /// body actually runs once limbs have come and gone — D146, the owner's ruling 5.
    /// </summary>
    /// <remarks>
    /// The owner ruled that every evolved limb, joints included, is wired to the brain's inputs as
    /// it evolves: a mutation that adds a limb gives it those inputs, a creature that loses a limb
    /// loses them, and the outputs follow the same rule. The ports follow from the body and need no
    /// storing. The outputs are stored, and a new joint's outputs copy the nearest joint's, which
    /// the owner accepted the same night on the agent's recommendation.
    /// </remarks>
    public static class CentralWiring
    {
        private static readonly SensorChannel[] EveryChannel =
            (SensorChannel[])Enum.GetValues(typeof(SensorChannel));

        /// <summary>
        /// How many indices <paramref name="channel"/> has on <paramref name="part"/>.
        /// </summary>
        /// <remarks>
        /// The joint channels and the drive sense have one per degree of freedom and none on a
        /// rigid part, where today's neurons may name an index past the end and read zero. The
        /// reserve is the whole body's, so it is a port of the root alone: on every part it would
        /// be one number many times over. A smell has none on a part that may not smell (D235,
        /// <see cref="PhenotypePart.Smells"/>), so in a world whose table lists the nose each nose
        /// carries its own set of smell ports and no other part carries any.
        /// </remarks>
        public static int IndicesOn(PhenotypePart part, SensorChannel channel)
        {
            if (!part.Smells && channel.IsSmell()) return 0;

            switch (channel)
            {
                case SensorChannel.JointAngle:
                case SensorChannel.JointAngularVelocity:
                case SensorChannel.Stroke:
                case SensorChannel.Hold:
                case SensorChannel.Ramp:
                    return part.JointType.DofCount();

                case SensorChannel.Energy:
                    return part.IsRoot ? 1 : 0;

                default:
                    return channel.IndexCount();
            }
        }

        /// <summary>True for the channels with one index per degree of freedom.</summary>
        public static bool PerDof(SensorChannel channel) =>
            channel == SensorChannel.JointAngle || channel == SensorChannel.JointAngularVelocity ||
            channel == SensorChannel.Stroke || channel == SensorChannel.Hold ||
            channel == SensorChannel.Ramp;

        /// <summary>
        /// The body's ports for the run's sensor pool, in part order and then pool order: what a
        /// founder is wired from and what mutation may add a link from.
        /// </summary>
        public static List<BrainPort> Ports(Phenotype body, SensorChannel[] pool)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            pool = pool ?? SensorChannels.DefaultPool;

            var ports = new List<BrainPort>();
            foreach (PhenotypePart part in body.Parts)
            {
                foreach (SensorChannel channel in pool)
                {
                    int count = IndicesOn(part, channel);
                    for (int i = 0; i < count; i++)
                    {
                        ports.Add(new BrainPort(
                            BrainIds.Port(part.PathId, channel, i), part.Index, channel, i));
                    }
                }
            }

            return ports;
        }

        /// <summary>
        /// Every port the body could have under any pool, by id: what a stored link is resolved
        /// against, so a link survives a run whose pool is narrower than the one that made it.
        /// </summary>
        public static Dictionary<ulong, BrainPort> AllPorts(Phenotype body)
        {
            var all = new Dictionary<ulong, BrainPort>();
            foreach (PhenotypePart part in body.Parts)
            {
                foreach (SensorChannel channel in EveryChannel)
                {
                    int count = IndicesOn(part, channel);
                    for (int i = 0; i < count; i++)
                    {
                        ulong id = BrainIds.Port(part.PathId, channel, i);
                        all[id] = new BrainPort(id, part.Index, channel, i);
                    }
                }
            }

            return all;
        }

        /// <summary>
        /// The body's outputs: the power and the bend of every degree of freedom, in the order of
        /// the drive array (<see cref="Brain"/>'s own: parts in order, the root contributing
        /// nothing).
        /// </summary>
        public static List<BrainOutput> Outputs(Phenotype body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            var outputs = new List<BrainOutput>();
            int cursor = 0;
            foreach (PhenotypePart part in body.Parts)
            {
                int dofs = part.JointType.DofCount();
                for (int d = 0; d < dofs; d++)
                {
                    outputs.Add(new BrainOutput(
                        BrainIds.Output(part.PathId, d, BrainOutputKind.Power), part.Index, d,
                        BrainOutputKind.Power, cursor + d));
                    outputs.Add(new BrainOutput(
                        BrainIds.Output(part.PathId, d, BrainOutputKind.Bend), part.Index, d,
                        BrainOutputKind.Bend, cursor + d));
                }

                cursor += dofs;
            }

            return outputs;
        }

        /// <summary>
        /// Turns a genome of the old model into one of the central model: no neurons on any node,
        /// none global, numbered edges and an empty brain. The body is untouched, so a founder
        /// drawn by <see cref="GenomeFactory"/> keeps the body the old model's stream drew.
        /// </summary>
        public static void Convert(Genome genome)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));

            foreach (MorphNode node in genome.Nodes) node.Neurons = Array.Empty<NeuronDef>();
            genome.GlobalBrain = Array.Empty<NeuronDef>();
            genome.Brain = new CentralBrain();
            genome.NumberEdges();
        }

        /// <summary>
        /// The brain <paramref name="body"/> runs: <paramref name="brain"/> with the outputs of
        /// joints the body no longer has dropped, the links from ports it no longer has dropped,
        /// and an output for every joint it has gained.
        /// </summary>
        /// <param name="brain">The genome's brain. Not changed.</param>
        /// <param name="body">The developed body.</param>
        /// <param name="fresh">
        /// Draws a new output's bias and links when there is nothing to copy (a founder, or a
        /// body with no other joint). Null leaves such an output silent: bias 0 and no links.
        /// </param>
        /// <param name="pool">The run's sensor pool, which a fresh output is wired from.</param>
        /// <param name="options">The run's brain settings, for a fresh output's draws.</param>
        /// <param name="founder">
        /// True draws every new output fresh, so a founder's joints are not copies of its first.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>A new joint copies the nearest joint</b>: another degree of freedom of its own part
        /// first, then a sibling grown from the same node on the same parent (a mirror twin),
        /// then its parent's joint and up the chain to the root, then any joint in part order. It copies the bias and every incoming link of the same kind of output. A link
        /// from one of the copied joint's own ports is moved to the new part's port of the same
        /// channel and index, and a per-degree-of-freedom index follows the degree of freedom. So
        /// a new segment reads its own joint as its neighbour reads its own, which is what makes
        /// a duplicated segment a duplicated controller, the property D019 kept the per-part
        /// neurons for.
        /// </para>
        /// <para>
        /// The world materialises this at birth, so the copy is the child's own to mutate. A
        /// module rebuild (D106 item 2) builds a network from it without touching the genome.
        /// </para>
        /// </remarks>
        public static CentralBrain Effective(
            CentralBrain brain, Phenotype body, Rng fresh = null, SensorChannel[] pool = null,
            CentralBrainOptions options = null, bool founder = false)
        {
            if (brain == null) throw new ArgumentNullException(nameof(brain));
            if (body == null) throw new ArgumentNullException(nameof(body));
            options = options ?? new CentralBrainOptions();

            List<BrainOutput> outputs = Outputs(body);
            Dictionary<ulong, BrainPort> ports = AllPorts(body);

            var stored = new Dictionary<ulong, BrainUnit>();
            foreach (BrainUnit u in brain.Outputs) stored[u.Id] = u;

            var result = new CentralBrain();
            var units = new HashSet<ulong>();
            foreach (BrainUnit u in brain.Hidden)
            {
                result.Hidden.Add(u.Clone());
                units.Add(u.Id);
            }

            var made = new Dictionary<ulong, BrainUnit>();
            foreach (BrainOutput o in outputs)
            {
                if (!stored.TryGetValue(o.Id, out BrainUnit unit)) continue;
                BrainUnit copy = unit.Clone();
                result.Outputs.Add(copy);
                made[o.Id] = copy;
                units.Add(o.Id);
            }

            // The links that still have both ends.
            var incoming = new Dictionary<ulong, List<BrainLink>>();
            foreach (BrainLink l in brain.Links)
            {
                if (!units.Contains(l.To)) continue;
                if (!units.Contains(l.From) && !ports.ContainsKey(l.From) && !IsNewOutput(l.From))
                {
                    continue;
                }

                BrainLink copy = l.Clone();
                result.Links.Add(copy);
                Index(incoming, copy);
            }

            // A link from an output that is about to be made is kept: it is a loop the copied
            // joint had, and the new output is named before it exists.
            bool IsNewOutput(ulong id)
            {
                foreach (BrainOutput o in outputs) if (o.Id == id) return true;
                return false;
            }

            List<BrainPort> poolPorts = null;

            foreach (BrainOutput o in outputs)
            {
                if (made.ContainsKey(o.Id)) continue;

                var unit = new BrainUnit { Id = o.Id };
                result.Outputs.Add(unit);
                made[o.Id] = unit;
                units.Add(o.Id);

                BrainOutput? template = founder ? null : Template(o, body, outputs, made);
                if (template is BrainOutput t)
                {
                    BrainUnit source = made[t.Id];
                    unit.Bias = source.Bias;

                    PhenotypePart to = body.Parts[o.Part];

                    if (incoming.TryGetValue(t.Id, out List<BrainLink> links))
                    {
                        // Two passes, so that no source is linked twice: the template's own senses
                        // (and its loop) moved to the new joint's first, then everything else it
                        // reads, unless that source is already taken. A dense founder reads every
                        // part's senses, the new part's included, so the moved link and the
                        // template's own link from the new part's port would otherwise collide; the
                        // moved one is the new joint reading itself as its template read itself, and
                        // it wins.
                        var taken = new Dictionary<ulong, BrainLink>();
                        foreach (bool own in new[] { true, false })
                        {
                            foreach (BrainLink l in links.ToArray())
                            {
                                ulong src = l.From;
                                bool moved = false;
                                if (src == t.Id)
                                {
                                    src = o.Id;
                                    moved = true;
                                }
                                else if (ports.TryGetValue(src, out BrainPort p) && p.Part == t.Part)
                                {
                                    int index = PerDof(p.Channel) && p.Index == t.Dof ? o.Dof : p.Index;
                                    if (index >= IndicesOn(to, p.Channel)) continue;
                                    src = BrainIds.Port(to.PathId, p.Channel, index);
                                    moved = true;
                                }

                                if (moved != own || taken.ContainsKey(src)) continue;
                                var copy = new BrainLink { From = src, To = o.Id, Weight = l.Weight };
                                taken[src] = copy;
                                result.Links.Add(copy);
                                Index(incoming, copy);
                            }
                        }
                    }

                }
                else if (fresh != null)
                {
                    poolPorts = poolPorts ?? Ports(body, pool);
                    unit.Bias = fresh.Gaussian(0f, options.FounderBiasStdDev);

                    float scale = poolPorts.Count == 0
                        ? 0f
                        : options.FounderWeightScale /
                          (float)Math.Sqrt(Math.Max(1.0, poolPorts.Count * options.FounderLinkShare));

                    foreach (BrainPort p in poolPorts)
                    {
                        if (!fresh.Chance(options.FounderLinkShare)) continue;
                        var copy = new BrainLink { From = p.Id, To = o.Id, Weight = fresh.Gaussian(0f, scale) };
                        result.Links.Add(copy);
                        Index(incoming, copy);
                    }
                }
            }

            return result;
        }

        private static void Index(Dictionary<ulong, List<BrainLink>> incoming, BrainLink l)
        {
            if (!incoming.TryGetValue(l.To, out List<BrainLink> list))
            {
                list = new List<BrainLink>();
                incoming[l.To] = list;
            }

            list.Add(l);
        }

        // The nearest output of the same kind that the brain already has.
        private static BrainOutput? Template(
            BrainOutput o, Phenotype body, List<BrainOutput> outputs, Dictionary<ulong, BrainUnit> made)
        {
            // Another degree of freedom of the same part, the nearest first.
            for (int step = 1; step < 3; step++)
            {
                foreach (int d in new[] { o.Dof - step, o.Dof + step })
                {
                    if (d < 0) continue;
                    BrainOutput? same = Find(o.Part, d);
                    if (same != null) return same;
                }
            }

            // A sibling grown from the same node on the same parent: a mirror twin, or the other
            // copies of a repeated edge. The proposal: "a repeated segment gets a copy of its
            // sibling's weights", and a new right fin should start as the left fin does.
            PhenotypePart self = body.Parts[o.Part];
            for (int p = 0; p < body.PartCount; p++)
            {
                if (p == o.Part) continue;
                PhenotypePart other = body.Parts[p];
                if (other.ParentIndex != self.ParentIndex || other.SourceNode != self.SourceNode) continue;
                BrainOutput? twin = Nearest(p);
                if (twin != null) return twin;
            }

            // Up the chain to the root.
            for (int p = body.Parts[o.Part].ParentIndex; p >= 0; p = body.Parts[p].ParentIndex)
            {
                BrainOutput? up = Nearest(p);
                if (up != null) return up;
            }

            // Any joint, in part order.
            for (int p = 0; p < body.PartCount; p++)
            {
                if (p == o.Part) continue;
                BrainOutput? any = Nearest(p);
                if (any != null) return any;
            }

            return null;

            BrainOutput? Nearest(int part)
            {
                int dofs = body.Parts[part].JointType.DofCount();
                if (dofs == 0) return null;
                int want = Math.Min(o.Dof, dofs - 1);
                for (int step = 0; step < dofs; step++)
                {
                    foreach (int d in new[] { want - step, want + step })
                    {
                        if (d < 0 || d >= dofs) continue;
                        BrainOutput? hit = Find(part, d);
                        if (hit != null) return hit;
                    }
                }

                return null;
            }

            BrainOutput? Find(int part, int dof)
            {
                foreach (BrainOutput c in outputs)
                {
                    if (c.Part == part && c.Dof == dof && c.Kind == o.Kind && made.ContainsKey(c.Id)) return c;
                }

                return null;
            }
        }
    }
}
