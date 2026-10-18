using System;
using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>How a route traced back from an output ended.</summary>
    public enum RouteEnd
    {
        /// <summary>No route was traced: the donor has no output neuron to start from.</summary>
        None = 0,

        /// <summary>The strongest input left was a sense.</summary>
        Sense = 1,

        /// <summary>
        /// Nothing left to follow, and nothing was skipped for being on the route: an oscillator
        /// (which reads no input), a neuron with no inputs, or one whose inputs are all constants.
        /// </summary>
        NoInput = 2,

        /// <summary>Nothing left to follow because every remaining input was a neuron already on the route.</summary>
        Loop = 3,

        /// <summary>
        /// The strongest input was a ParentNode, ChildNode or GlobalBrain input whose target does
        /// not resolve to one neuron of the genome.
        /// </summary>
        Unresolved = 4,
    }

    /// <summary>What one crossover did.</summary>
    public sealed class CrossoverRecord
    {
        /// <summary>The output the route started at, in the donor: node and neuron index.</summary>
        public int OutputNode = -1, OutputIndex = -1;

        /// <summary>The route's neurons in the donor, output first, in the order traced.</summary>
        public List<(int Node, int Index)> Route = new List<(int Node, int Index)>();

        public RouteEnd End;

        /// <summary>Neurons added to the base (route neurons at a position the base did not have).</summary>
        public int Added;

        /// <summary>Whether anything was written into the child.</summary>
        public bool Transferred;

        /// <summary>
        /// Why nothing was transferred, or null when something was: <c>no-output</c> (the donor
        /// has no power or bend neuron), <c>no-route</c> (the output has no input to follow),
        /// <c>base-output-missing</c> (the base has no neuron at the output's position),
        /// <c>limit</c> (an added neuron would pass the per-node neuron limit).
        /// </summary>
        public string Refused;

        /// <summary>
        /// Whether the input set on the base's output neuron is one its operator reads. An input
        /// appended past a fixed-arity operator's inputs (a Sigmoid's second, say) is carried and
        /// never read.
        /// </summary>
        public bool OutputInputRead;
    }

    /// <summary>
    /// propose-nursery-crossover.md: the child takes the base's brain whole and, from the donor,
    /// the route that drives one joint output hardest, traced back by the largest absolute weight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure: it reads the two genomes, returns a new one, and draws only from the
    /// <see cref="Rng"/> it is given (exactly one draw, the output, when the donor has one).
    /// </para>
    /// <para>
    /// <b>Positions.</b> In a jointed part with <i>n</i> degrees of freedom, neuron <i>d</i> drives
    /// degree <i>d</i>'s power and neuron <i>n + d</i> its bend (<see cref="Brain.Step"/>). Both
    /// genomes carry the same body, so a node index and a neuron index mean the same part and
    /// the same place in both.
    /// </para>
    /// <para>
    /// <b>What the route reads.</b> Only the inputs the neuron's operator actually reads are
    /// candidates: every input of Sum, Product, Min, Max and SumThreshold; none of an oscillator;
    /// the first <see cref="NeuronOps.Arity"/> of the rest, as <see cref="Brain"/> evaluates them.
    /// </para>
    /// <para>
    /// <b>ParentNode and ChildNode</b> are followed only when the genome names one node for them
    /// and the index is inside that node's neurons: a non-root node with exactly one distinct
    /// parent node (not itself), or a node whose edges all lead to one child node (not itself).
    /// The brain wraps an out-of-range index and reads 0 at the root and at a leaf; none of those
    /// is followed. <b>GlobalBrain</b> reads 0 since D081 and is never followed. When the
    /// strongest candidate is one of these and does not resolve, the route ends there
    /// (<see cref="RouteEnd.Unresolved"/>).
    /// </para>
    /// </remarks>
    public static class Crossover
    {
        /// <summary>
        /// The nursery's brain distance d: the brain term of D057's species distance at weight 1
        /// (<see cref="SpeciesDistance.Between"/>, which compares only the neurons and inputs that
        /// both brains hold at the same position), plus one unit for every structural difference,
        /// as D057 counts one: each neuron one node holds and the other does not (by position, past
        /// the shorter list), and each input one neuron holds and the other does not (neurons
        /// matched by position, inputs past the shorter list). Nodes are matched by index; the
        /// retired global brain (D081) is left to the Core term.
        /// </summary>
        public static float BrainDistance(Genome a, Genome b)
        {
            if (a.Brain != null || b.Brain != null) return CentralDistance(a.Brain, b.Brain);

            float d = SpeciesDistance.Between(a, b, 0f, 0f, 0f, 1f);
            int nodes = Math.Min(a.Nodes.Count, b.Nodes.Count);
            int units = 0;
            for (int i = 0; i < nodes; i++)
            {
                NeuronDef[] na = a.Nodes[i].Neurons, nb = b.Nodes[i].Neurons;
                units += Math.Abs(na.Length - nb.Length);
                int shared = Math.Min(na.Length, nb.Length);
                for (int k = 0; k < shared; k++) units += Math.Abs(na[k].Inputs.Length - nb[k].Inputs.Length);
            }
            return d + units;
        }

        /// <summary>
        /// The child of <paramref name="baseGenome"/> with one route of <paramref name="donor"/>'s
        /// copied in, or an unchanged copy of the base when nothing could be passed.
        /// </summary>
        /// <param name="maxNeuronsPerNode">A node may not be taken past this many neurons by an added
        /// one; 0 or less for no limit.</param>
        public static Genome Cross(Genome baseGenome, Genome donor, Rng rng, int maxNeuronsPerNode, out CrossoverRecord record)
        {
            if (baseGenome == null) throw new ArgumentNullException(nameof(baseGenome));
            if (donor == null) throw new ArgumentNullException(nameof(donor));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (baseGenome.Nodes.Count != donor.Nodes.Count)
                throw new ArgumentException("the base and the donor must carry the same body (node counts differ)");

            if (baseGenome.Brain != null || donor.Brain != null)
            {
                if (baseGenome.Brain == null || donor.Brain == null)
                    throw new ArgumentException("the base and the donor must carry the same model of brain");
                return CrossCentral(baseGenome, donor, rng, maxNeuronsPerNode, out record);
            }

            record = new CrossoverRecord();
            Genome child = baseGenome.Clone();

            // ------------------------------------------------ the output
            var outputs = new List<(int Node, int Index)>();
            for (int i = 0; i < donor.Nodes.Count; i++)
            {
                if (i == donor.RootIndex) continue;   // the root's joint is fixed by development
                int n = donor.Nodes[i].JointType.DofCount();
                int have = donor.Nodes[i].Neurons.Length;
                for (int k = 0; k < 2 * n && k < have; k++) outputs.Add((i, k));
            }
            if (outputs.Count == 0)
            {
                record.Refused = "no-output";
                return child;
            }
            (int on, int ok) = outputs[rng.Range(outputs.Count)];
            record.OutputNode = on;
            record.OutputIndex = ok;

            // ------------------------------------------------ the route
            var route = record.Route;
            route.Add((on, ok));
            var onRoute = new HashSet<(int, int)> { (on, ok) };
            NeuronInput? fromOutput = null;
            (int node, int index) at = (on, ok);
            while (true)
            {
                NeuronDef neuron = donor.Nodes[at.node].Neurons[at.index];
                int read = ReadCount(neuron);
                int best = -1;
                float bestWeight = -1f;
                bool skippedRoute = false;
                for (int k = 0; k < read; k++)
                {
                    NeuronInput input = neuron.Inputs[k];
                    if (input.Kind == NeuronInputKind.Constant) continue;
                    if (IsNeuron(input.Kind) && TryTarget(donor, at.node, input, out (int, int) target) && onRoute.Contains(target))
                    {
                        skippedRoute = true;
                        continue;
                    }
                    float w = Math.Abs(input.Weight);
                    if (w > bestWeight) { best = k; bestWeight = w; }
                }

                if (best < 0)
                {
                    record.End = skippedRoute ? RouteEnd.Loop : RouteEnd.NoInput;
                    break;
                }

                NeuronInput chosen = neuron.Inputs[best];
                if (route.Count == 1) fromOutput = chosen;
                if (chosen.Kind == NeuronInputKind.Sensor)
                {
                    record.End = RouteEnd.Sense;
                    break;
                }
                if (!TryTarget(donor, at.node, chosen, out (int, int) next))
                {
                    record.End = RouteEnd.Unresolved;
                    if (route.Count == 1) fromOutput = null;   // nothing to pass from the output
                    break;
                }
                route.Add(next);
                onRoute.Add(next);
                at = next;
            }

            if (fromOutput == null)
            {
                record.Refused = "no-route";
                return child;
            }
            if (baseGenome.Nodes[on].Neurons.Length <= ok)
            {
                record.Refused = "base-output-missing";
                return child;
            }

            // ------------------------------------------------ the copy
            var original = new int[child.Nodes.Count];
            var lists = new List<NeuronDef>[child.Nodes.Count];
            for (int i = 0; i < child.Nodes.Count; i++)
            {
                original[i] = child.Nodes[i].Neurons.Length;
                lists[i] = new List<NeuronDef>(child.Nodes[i].Neurons);
            }

            var adds = new int[child.Nodes.Count];
            for (int r = 1; r < route.Count; r++)
                if (route[r].Index >= original[route[r].Node]) adds[route[r].Node]++;
            if (maxNeuronsPerNode > 0)
                for (int i = 0; i < adds.Length; i++)
                    if (adds[i] > 0 && original[i] + adds[i] > maxNeuronsPerNode)
                    {
                        record.Refused = "limit";
                        return child;
                    }

            // Where each route neuron lands in the child: its own position when the base has a
            // neuron there, else the end of that part's list.
            var map = new Dictionary<(int, int), int> { [(on, ok)] = ok };
            for (int r = 1; r < route.Count; r++)
            {
                (int n, int j) = route[r];
                if (j < original[n]) map[(n, j)] = j;
                else
                {
                    map[(n, j)] = lists[n].Count;
                    lists[n].Add(null);
                    record.Added++;
                }
            }

            for (int r = 1; r < route.Count; r++)
            {
                (int n, int j) = route[r];
                NeuronDef copy = donor.Nodes[n].Neurons[j].Clone();
                var inputs = new List<NeuronInput>(copy.Inputs.Length);
                foreach (NeuronInput input in copy.Inputs)
                    if (Repoint(donor, child, n, input, map, original, out NeuronInput kept)) inputs.Add(kept);
                copy.Inputs = inputs.ToArray();
                lists[n][map[(n, j)]] = copy;
            }

            // The output: only its one input from the route, which replaces an input from the
            // same source or is added.
            Repoint(donor, child, on, fromOutput.Value, map, original, out NeuronInput set);
            NeuronDef output = lists[on][ok].Clone();
            NeuronInput[] outInputs = output.Inputs;
            int slot = -1;
            for (int k = 0; k < outInputs.Length; k++)
                if (SameSource(outInputs[k], set)) { slot = k; break; }
            if (slot >= 0)
            {
                outInputs = (NeuronInput[])outInputs.Clone();
                outInputs[slot] = set;
            }
            else
            {
                Array.Resize(ref outInputs, outInputs.Length + 1);
                slot = outInputs.Length - 1;
                outInputs[slot] = set;
            }
            output.Inputs = outInputs;
            lists[on][ok] = output;
            record.OutputInputRead = slot < ReadCount(output);

            for (int i = 0; i < child.Nodes.Count; i++) child.Nodes[i].Neurons = lists[i].ToArray();
            record.Transferred = true;
            return child;
        }

        // ------------------------------------------------------------------ the one brain (D146)

        /// <summary>
        /// D146's brain distance: over the links both brains hold (matched by their two ends), the
        /// summed absolute difference of the weights; over the units both hold (matched by id),
        /// the summed absolute difference of the biases; and one for every link or unit only one
        /// of them holds, as D057 counts a structural difference. The same yardstick as the old
        /// brain's in shape, not in size: <c>--measure-dref</c> measures it afresh.
        /// </summary>
        public static float CentralDistance(CentralBrain a, CentralBrain b)
        {
            if (a == null || b == null) return float.PositiveInfinity;

            double d = 0d;
            var biasA = new Dictionary<ulong, float>();
            foreach (BrainUnit u in a.Outputs) biasA[u.Id] = u.Bias;
            foreach (BrainUnit u in a.Hidden) biasA[u.Id] = u.Bias;
            int matchedUnits = 0;
            foreach (BrainUnit u in Units(b))
            {
                if (biasA.TryGetValue(u.Id, out float bias)) { d += Math.Abs(bias - u.Bias); matchedUnits++; }
                else d += 1d;
            }
            d += biasA.Count - matchedUnits;

            var weightA = new Dictionary<(ulong, ulong), float>(a.Links.Count);
            foreach (BrainLink l in a.Links) weightA[(l.From, l.To)] = l.Weight;
            int matchedLinks = 0;
            foreach (BrainLink l in b.Links)
            {
                if (weightA.TryGetValue((l.From, l.To), out float w)) { d += Math.Abs(w - l.Weight); matchedLinks++; }
                else d += 1d;
            }
            d += weightA.Count - matchedLinks;
            return (float)d;
        }

        private static IEnumerable<BrainUnit> Units(CentralBrain brain)
        {
            foreach (BrainUnit u in brain.Outputs) yield return u;
            foreach (BrainUnit u in brain.Hidden) yield return u;
        }

        /// <summary>
        /// propose-nursery-crossover.md on D146's brain. One output of the donor is drawn; from it
        /// the route steps back along the incoming link of the largest absolute weight, skipping a
        /// link from a unit already on the route, until it reaches a port (a sense), a unit with
        /// no incoming link, or only links back onto the route. The child is the base with the
        /// route's hidden units as the donor holds them (bias and every incoming link, added by id
        /// where the base lacks them) and, on the output, the route's one link, which replaces the
        /// base's link from the same source or is added. Ids line the two brains up, which is what
        /// the old brain's positions could not do once neurons were deleted.
        /// </summary>
        /// <remarks>
        /// The record's <see cref="CrossoverRecord.Route"/> holds (0, i) for the donor's output i
        /// and (1, i) for its hidden unit i; <see cref="CrossoverRecord.OutputNode"/> is 0 and
        /// <see cref="CrossoverRecord.OutputIndex"/> the output's index. A link from an id that is
        /// no unit of the donor is a port: the route ends at a sense. A link from another output
        /// ends the route there too (<see cref="RouteEnd.Unresolved"/>): the base keeps its own
        /// outputs, so a route traced through one would copy units that feed nothing in the child.
        /// <paramref name="maxHidden"/> above 0 refuses a crossover that would take the base past
        /// that many hidden units (<c>limit</c>).
        /// </remarks>
        private static Genome CrossCentral(Genome baseGenome, Genome donor, Rng rng, int maxHidden, out CrossoverRecord record)
        {
            record = new CrossoverRecord();
            Genome child = baseGenome.Clone();
            CentralBrain d = donor.Brain, c = child.Brain;

            if (d.Outputs.Count == 0)
            {
                record.Refused = "no-output";
                return child;
            }

            int pick = rng.Range(d.Outputs.Count);
            ulong output = d.Outputs[pick].Id;
            record.OutputNode = 0;
            record.OutputIndex = pick;

            var hiddenAt = new Dictionary<ulong, int>();
            for (int i = 0; i < d.Hidden.Count; i++) hiddenAt[d.Hidden[i].Id] = i;
            var outputAt = new Dictionary<ulong, int>();
            for (int i = 0; i < d.Outputs.Count; i++) outputAt[d.Outputs[i].Id] = i;
            bool IsUnit(ulong id) => hiddenAt.ContainsKey(id) || outputAt.ContainsKey(id);

            var incoming = new Dictionary<ulong, List<BrainLink>>();
            foreach (BrainLink l in d.Links)
            {
                if (!incoming.TryGetValue(l.To, out List<BrainLink> list)) incoming[l.To] = list = new List<BrainLink>();
                list.Add(l);
            }

            var route = new List<ulong> { output };
            var onRoute = new HashSet<ulong> { output };
            record.Route.Add((0, pick));
            BrainLink fromOutput = null;
            ulong at = output;
            while (true)
            {
                BrainLink best = null;
                bool skippedRoute = false;
                if (incoming.TryGetValue(at, out List<BrainLink> links))
                {
                    foreach (BrainLink l in links)
                    {
                        if (l.Weight == 0f) continue;
                        if (onRoute.Contains(l.From)) { skippedRoute = true; continue; }
                        if (best == null || Math.Abs(l.Weight) > Math.Abs(best.Weight)) best = l;
                    }
                }

                if (best == null)
                {
                    record.End = skippedRoute ? RouteEnd.Loop : RouteEnd.NoInput;
                    break;
                }

                if (route.Count == 1) fromOutput = best;
                if (!IsUnit(best.From))
                {
                    record.End = RouteEnd.Sense;
                    break;
                }

                if (outputAt.ContainsKey(best.From))
                {
                    record.End = RouteEnd.Unresolved;
                    break;
                }

                route.Add(best.From);
                onRoute.Add(best.From);
                record.Route.Add(hiddenAt.TryGetValue(best.From, out int h) ? (1, h) : (0, outputAt[best.From]));
                at = best.From;
            }

            if (fromOutput == null)
            {
                record.Refused = "no-route";
                return child;
            }

            if (!c.Outputs.Exists(u => u.Id == output))
            {
                record.Refused = "base-output-missing";
                return child;
            }

            // The route's hidden units, as the donor holds them (the route holds no other output:
            // it ends at one).
            var childUnits = new HashSet<ulong>();
            foreach (BrainUnit u in Units(c)) childUnits.Add(u.Id);
            if (maxHidden > 0)
            {
                int adding = 0;
                for (int r = 1; r < route.Count; r++)
                    if (hiddenAt.ContainsKey(route[r]) && !childUnits.Contains(route[r])) adding++;
                if (c.Hidden.Count + adding > maxHidden)
                {
                    record.Refused = "limit";
                    return baseGenome.Clone();
                }
            }
            var copied = new List<ulong>();
            for (int r = 1; r < route.Count; r++)
            {
                if (!hiddenAt.TryGetValue(route[r], out int h)) continue;
                BrainUnit donorUnit = d.Hidden[h];
                BrainUnit mine = c.Hidden.Find(u => u.Id == donorUnit.Id);
                if (mine == null)
                {
                    c.Hidden.Add(donorUnit.Clone());
                    childUnits.Add(donorUnit.Id);
                    record.Added++;
                }
                else
                {
                    mine.Bias = donorUnit.Bias;
                }

                copied.Add(donorUnit.Id);
            }

            foreach (ulong unit in copied)
            {
                c.Links.RemoveAll(l => l.To == unit);
                if (!incoming.TryGetValue(unit, out List<BrainLink> links)) continue;
                foreach (BrainLink l in links)
                {
                    // A link from a donor hidden unit the child does not hold would read nothing.
                    if (hiddenAt.ContainsKey(l.From) && !childUnits.Contains(l.From)) continue;
                    c.Links.Add(l.Clone());
                }
            }

            BrainLink same = c.Links.Find(l => l.From == fromOutput.From && l.To == output);
            if (same != null) same.Weight = fromOutput.Weight;
            else c.Links.Add(fromOutput.Clone());

            record.OutputInputRead = true;
            record.Transferred = true;
            return child;
        }

        /// <summary>How many of a neuron's inputs its operator reads, as <see cref="Brain"/> evaluates it.</summary>
        public static int ReadCount(NeuronDef neuron)
        {
            int count = neuron.Inputs.Length;
            switch (neuron.Op)
            {
                case NeuronOp.Sum:
                case NeuronOp.Product:
                case NeuronOp.Min:
                case NeuronOp.Max:
                case NeuronOp.SumThreshold:
                    return count;
                case NeuronOp.OscillateWave:
                case NeuronOp.OscillateSaw:
                    return 0;
                default:
                    return Math.Min(neuron.Op.Arity(), count);
            }
        }

        private static bool IsNeuron(NeuronInputKind kind) =>
            kind == NeuronInputKind.SameNode || kind == NeuronInputKind.ParentNode ||
            kind == NeuronInputKind.ChildNode || kind == NeuronInputKind.GlobalBrain;

        /// <summary>
        /// The one node a neuron input on <paramref name="owner"/> reads from, or -1 when the genome
        /// does not name one (GlobalBrain; ParentNode at the root or with several parents;
        /// ChildNode at a leaf or with several children; a recursion onto itself).
        /// </summary>
        public static int TargetNode(Genome g, int owner, NeuronInputKind kind)
        {
            switch (kind)
            {
                case NeuronInputKind.SameNode:
                    return owner;
                case NeuronInputKind.ParentNode:
                {
                    if (owner == g.RootIndex) return -1;
                    int found = -1;
                    for (int i = 0; i < g.Nodes.Count; i++)
                    {
                        bool leads = false;
                        foreach (MorphEdge e in g.Nodes[i].Edges) if (e.Child == owner) { leads = true; break; }
                        if (!leads) continue;
                        if (found >= 0 || i == owner) return -1;
                        found = i;
                    }
                    return found;
                }
                case NeuronInputKind.ChildNode:
                {
                    int found = -1;
                    foreach (MorphEdge e in g.Nodes[owner].Edges)
                    {
                        if (e.Child == owner) return -1;
                        if (e.Child < 0 || e.Child >= g.Nodes.Count) return -1;
                        if (found >= 0 && found != e.Child) return -1;
                        found = e.Child;
                    }
                    return found;
                }
                default:
                    return -1;
            }
        }

        /// <summary>The neuron an input reads, when it resolves to one neuron of the genome.</summary>
        private static bool TryTarget(Genome g, int owner, NeuronInput input, out (int, int) target)
        {
            target = (-1, -1);
            int node = TargetNode(g, owner, input.Kind);
            if (node < 0 || input.Index < 0 || input.Index >= g.Nodes[node].Neurons.Length) return false;
            target = (node, input.Index);
            return true;
        }

        /// <summary>
        /// One input of a copied neuron, as the child holds it: pointed at the copy when it reads a
        /// route neuron; kept at its index when it reads a donor neuron that was not copied and the
        /// base has a neuron there; dropped when the base has none. Constants, senses, and inputs
        /// whose node the genome does not name are kept as they are; a GlobalBrain input is kept
        /// only if the base's global brain has that index.
        /// </summary>
        private static bool Repoint(Genome donor, Genome child, int owner, NeuronInput input,
            Dictionary<(int, int), int> map, int[] original, out NeuronInput kept)
        {
            kept = input;
            switch (input.Kind)
            {
                case NeuronInputKind.Constant:
                case NeuronInputKind.Sensor:
                    return true;
                case NeuronInputKind.GlobalBrain:
                    return input.Index >= 0 && input.Index < child.GlobalBrain.Length;
            }

            int node = TargetNode(donor, owner, input.Kind);
            if (node < 0) return true;
            if (map.TryGetValue((node, input.Index), out int at))
            {
                kept = new NeuronInput(input.Kind, at, input.Channel, input.Constant, input.Weight);
                return true;
            }
            return input.Index >= 0 && input.Index < original[node];
        }

        private static bool SameSource(NeuronInput a, NeuronInput b)
        {
            if (a.Kind != b.Kind) return false;
            if (a.Kind == NeuronInputKind.Sensor) return a.Channel == b.Channel && a.Index == b.Index;
            if (a.Kind == NeuronInputKind.Constant) return false;
            return a.Index == b.Index;
        }
    }
}
