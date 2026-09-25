using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// Which card slot each body holds (logbook/specs/gpu-port-spec.md section 2): a global slot
    /// that every body reads of every other by, and a slot in the smallest size class that holds
    /// its links and neurons, which its own step reads by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A slot is a place in memory and nothing else.</b> The kernel orders every cross-body
    /// sum by the solver list's rank (ascending id, the CPU's order), never by slot, so which
    /// slot a body lands in changes no number; the lowest free one keeps the arrays dense.
    /// </para>
    /// <para>
    /// <b>A body is uploaded when it is new to the card</b> — a birth, or a module rebuild,
    /// which the harness makes as a new object under the old id — <b>or resized</b>
    /// (<see cref="Creature.Resizes"/> moved). Every other body's state on the card is the
    /// state the last block left, which the host mirror was given at its end.
    /// </para>
    /// </remarks>
    internal sealed class GpuSlots
    {
        internal sealed class ClassSlots
        {
            public int Links, Neurons, Used;
            public readonly SortedSet<int> Free = new SortedSet<int>();
        }

        private readonly Dictionary<Creature, int> _slotOf =
            new Dictionary<Creature, int>(ReferenceEqualityComparer.Instance);

        private readonly HashSet<Creature> _refused = new HashSet<Creature>(ReferenceEqualityComparer.Instance);
        private readonly SortedSet<int> _free = new SortedSet<int>();

        public readonly ClassSlots[] Classes;

        /// <summary>Global slots in use or ever used: the launch extent of the cross-body kernels.</summary>
        public int Used;

        public Creature[] Body = new Creature[16];
        public int[] ClassOf = new int[16];
        public int[] LocalOf = new int[16];
        public long[] SeenResizes = new long[16];
        public int[] Rank = new int[16];

        /// <summary>The solver list's order: rank to global slot, -1 for a refused body.</summary>
        public int[] RankToSlot = new int[16];

        public int Count;

        /// <summary>Global slots to pack from their bodies at this block.</summary>
        public readonly List<int> Packs = new List<int>();

        /// <summary>Global slots emptied at this block, with the class slot each held.</summary>
        public readonly List<(int Global, int Class, int Local)> Freed = new List<(int, int, int)>();

        /// <summary>Bodies refused at this block: over the top class.</summary>
        public readonly List<Creature> Refused = new List<Creature>();

        public long RefusedForClass;

        public GpuSlots(int[] classLinks, int[] classNeurons)
        {
            Classes = new ClassSlots[classLinks.Length];
            for (int c = 0; c < Classes.Length; c++)
            {
                Classes[c] = new ClassSlots { Links = classLinks[c], Neurons = classNeurons[c] };
            }
        }

        /// <summary>The neurons a body's kernel carries: its parts' sets, in part order.</summary>
        public static int NeuronsOf(Creature c)
        {
            int total = 0;
            Phenotype plan = c.Phenotype;
            for (int p = 0; p < plan.PartCount; p++) total += plan.Parts[p].Neurons?.Length ?? 0;
            return total;
        }

        /// <summary>Whether any of a body's neurons takes more inputs than the kernel's three.</summary>
        public static bool OverInputs(Creature c)
        {
            Phenotype plan = c.Phenotype;
            for (int p = 0; p < plan.PartCount; p++)
            {
                NeuronDef[] set = plan.Parts[p].Neurons;
                if (set == null) continue;
                foreach (NeuronDef def in set)
                {
                    if (def.Inputs != null && def.Inputs.Length > 3) return true;
                }
            }

            return false;
        }

        /// <summary>The smallest class that holds the body, or -1.</summary>
        public int ClassFor(Creature c)
        {
            if (c.Phenotype.PartCount != c.Links || OverInputs(c)) return -1;

            int neurons = Math.Max(NeuronsOf(c), c.Brain.NeuronCount);
            for (int k = 0; k < Classes.Length; k++)
            {
                if (c.Links <= Classes[k].Links && neurons <= Classes[k].Neurons) return k;
            }

            return -1;
        }

        /// <summary>Brings the slots to the solver's list as it stands before a block.</summary>
        public void Sync(IReadOnlyList<Creature> list, bool resync)
        {
            Packs.Clear();
            Freed.Clear();
            Refused.Clear();

            int count = list.Count;
            Count = count;
            if (RankToSlot.Length < count) Array.Resize(ref RankToSlot, Math.Max(count, 2 * RankToSlot.Length));

            // 1. Who is still here, by reference: a rebuilt body is a new object.
            var here = new HashSet<Creature>(ReferenceEqualityComparer.Instance);
            for (int r = 0; r < count; r++) here.Add(list[r]);

            for (int g = 0; g < Used; g++)
            {
                Creature c = Body[g];
                if (c == null || here.Contains(c)) continue;

                int k = ClassOf[g], local = LocalOf[g];
                Freed.Add((g, k, local));
                Classes[k].Free.Add(local);
                _slotOf.Remove(c);
                Body[g] = null;
                _free.Add(g);
            }

            if (_refused.Count > 0) _refused.RemoveWhere(c => !here.Contains(c));

            // 2. The list in order: keep, pack, place or refuse.
            for (int r = 0; r < count; r++)
            {
                Creature c = list[r];

                if (_slotOf.TryGetValue(c, out int g))
                {
                    if (resync || c.Resizes != SeenResizes[g])
                    {
                        SeenResizes[g] = c.Resizes;
                        Packs.Add(g);
                    }

                    Rank[g] = r;
                    RankToSlot[r] = g;
                    continue;
                }

                if (_refused.Contains(c))
                {
                    RankToSlot[r] = -1;
                    continue;
                }

                int k = ClassFor(c);
                if (k < 0)
                {
                    _refused.Add(c);
                    Refused.Add(c);
                    RefusedForClass++;
                    RankToSlot[r] = -1;
                    continue;
                }

                g = TakeGlobal();
                int local = TakeLocal(k);

                Body[g] = c;
                ClassOf[g] = k;
                LocalOf[g] = local;
                SeenResizes[g] = c.Resizes;
                Rank[g] = r;
                _slotOf[c] = g;
                Packs.Add(g);
                RankToSlot[r] = g;
            }
        }

        private int TakeGlobal()
        {
            if (_free.Count > 0)
            {
                int g = _free.Min;
                _free.Remove(g);
                return g;
            }

            int next = Used++;
            if (next >= Body.Length)
            {
                int size = Math.Max(16, 2 * Body.Length);
                Array.Resize(ref Body, size);
                Array.Resize(ref ClassOf, size);
                Array.Resize(ref LocalOf, size);
                Array.Resize(ref SeenResizes, size);
                Array.Resize(ref Rank, size);
            }

            return next;
        }

        private int TakeLocal(int k)
        {
            ClassSlots slots = Classes[k];
            if (slots.Free.Count > 0)
            {
                int local = slots.Free.Min;
                slots.Free.Remove(local);
                return local;
            }

            return slots.Used++;
        }

        /// <summary>The global slot of a body id in the list, for an overlap entry's partner; -1 if none.</summary>
        public Dictionary<int, int> SlotsById()
        {
            var byId = new Dictionary<int, int>(Count);
            for (int g = 0; g < Used; g++)
            {
                Creature c = Body[g];
                if (c != null) byId[c.Id] = g;
            }

            return byId;
        }
    }
}
