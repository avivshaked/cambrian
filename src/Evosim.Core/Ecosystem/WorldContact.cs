using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// D219 and D220, contact eating: a mouth eats only what it touches, and a bite feeds as it wounds.
    /// Everything here runs behind <see cref="RunConfig.ContactEating"/> and nothing here is called with it
    /// off. The specification is <c>logbook/specs/contact-eating-spec.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The invariant.</b> With the dial on, a body's tissue account is the tissue of the parts it still
    /// holds: <c>TissueJoules = Σ health_p × T_p</c> over its parts, T_p the part's tissue at its current
    /// size (<see cref="Metabolism.PartTissueJoules(Phenotype, PhenotypePart, RunConfig, int)"/>). A wound that
    /// takes a share <c>s</c> of a part's health takes <c>s × T</c> out of the account; healing a share <c>h</c>
    /// puts <c>h × T</c> back from the reserve; a plan change or a growth step sets the account to the sum
    /// afresh. The <i>intact</i> tissue, <c>TissueJoules + Σ (1 − health_p) × T_p</c>, is what the body would
    /// hold unbitten: the figure growth and a plan change compare with the phenotype's own tissue.
    /// </para>
    /// <para>
    /// <b>Where the matter goes.</b> A bite moves charged matter out of the bitten body's account to the biter's
    /// reserve (what its biting part's intake can carry, less the waste) and to Nutrients (the rest); healing
    /// moves charged matter from the reserve into the account; a death or a cut-off limb is left as remains by
    /// <see cref="LeaveRemains"/> (D220), bodies of tissue and store that <see cref="StandingJoules"/> sums and a
    /// mouth can bite, until their window closes and they are burnt. Every leg either moves charged matter between
    /// accounts <see cref="StandingJoules"/> sums or books <see cref="EnergyOut"/> and the spent field in one line.
    /// </para>
    /// </remarks>
    public sealed partial class World
    {
        /// <summary>
        /// Joules biting parts have kept from bodies with photosynthetic tissue, ever (D219). Cumulative; 0
        /// and not checkpointed with <see cref="RunConfig.ContactEating"/> off.
        /// </summary>
        public double BiteKeptFromLeavesJoules { get; private set; }

        /// <summary>Joules biting parts have kept from bodies with no photosynthetic tissue, ever (D219). Cumulative.</summary>
        public double BiteKeptFromOthersJoules { get; private set; }

        /// <summary>
        /// Joules of bitten tissue that went to Nutrients as waste, ever (D219): the share of each kept bite
        /// the biter's reserve did not receive, and every joule a part without intake, or over its cap, bit.
        /// Cumulative.
        /// </summary>
        public double BiteWasteJoules { get; private set; }

        /// <summary>Joules moved from reserves into tissue by healing, ever (D219): the tissue re-bought. Cumulative.</summary>
        public double HealTissueJoules { get; private set; }

        /// <summary>
        /// Joules a bite on a living body took from its reserve and its gestation account, ever (D224): the share of
        /// each that the tissue share named. They joined the offer and were kept or wasted with it, so they are
        /// already in the kept and waste totals above. Cumulative; 0 and not checkpointed unless
        /// <see cref="RunConfig.BiteTakesStore"/> is on.
        /// </summary>
        public double BiteStoreTakenJoules { get; private set; }

        /// <summary>
        /// A mouth's blows that did damage, ever (D227): a blow whose attacking part carries intake and whose attacker
        /// is not remains, with damage above zero, counted whatever the value of <see cref="RunConfig.BiteDamageScale"/>.
        /// Cumulative from the founding. Checkpointed only when the dial is not 1.
        /// </summary>
        public long MouthBlows { get; private set; }

        /// <summary>
        /// Those of <see cref="MouthBlows"/> that took the bitten part from above zero health to zero (D227). Cumulative;
        /// checkpointed only when the dial is not 1.
        /// </summary>
        public long MouthBlowsFinishing { get; private set; }

        /// <summary>
        /// Bodies whose death was a kill that went into a killer's gut, ever (D232): a body a bite killed whose killer
        /// was a living body, with something to swallow. Cumulative; 0 and not checkpointed unless
        /// <see cref="RunConfig.KillSwallowsBody"/> is on.
        /// </summary>
        public long KillsSwallowed { get; private set; }

        /// <summary>
        /// Joules that went into guts at kills, ever (D232): the dead body's tissue and store, and the gut it carried.
        /// Cumulative; 0 and not checkpointed unless <see cref="RunConfig.KillSwallowsBody"/> is on.
        /// </summary>
        public double SwallowedJoules { get; private set; }

        /// <summary>
        /// Joules taken out of guts by digestion, ever (D232): each step's amount before the intake waste, which is the
        /// share of the mouth's per-step cap the bites left. Cumulative; checkpointed only with the switch on.
        /// </summary>
        public double GutDigestedJoules { get; private set; }

        /// <summary>
        /// Of <see cref="GutDigestedJoules"/>, the intake waste, which went to Nutrients at the digester's point (D232):
        /// the same route as a bite's waste, booked in <see cref="DetritusDepositedTotal"/> and not in
        /// <see cref="BiteWasteJoules"/>. Cumulative; checkpointed only with the switch on.
        /// </summary>
        public double GutWasteJoules { get; private set; }

        /// <summary>
        /// Joules of bites' offers that a mouth's per-step cap cut, ever (D232): what the bites offered above the cap,
        /// which went to the water as waste with the intake waste of the rest. Counted only with the switch on, so the
        /// screen reads how hard the cap binds on bites and, beside it, on the gut. Cumulative; checkpointed only
        /// with the switch on.
        /// </summary>
        public double BiteCapCutJoules { get; private set; }

        /// <summary>
        /// Joules the per-step cap held back in guts, summed over the body-steps on which it bound (D232): for each
        /// body with a gut that the cap left more of than it let out, what stayed. Divide by
        /// <see cref="GutCapBound"/> for the mean held back on a binding step. Cumulative; checkpointed only with the
        /// switch on.
        /// </summary>
        public double GutCapCutJoules { get; private set; }

        /// <summary>The body-steps on which the cap held something back in a gut, ever (D232). Cumulative; checkpointed only with the switch on.</summary>
        public long GutCapBound { get; private set; }

        /// <summary>The joules the living hold in their guts now (D232). O(n), for a report row.</summary>
        public double GutJoulesHeld
        {
            get
            {
                double sum = 0d;
                for (int i = 0; i < _living.Count; i++) sum += _living[i].GutJoules;
                return sum;
            }
        }

        // What the bites of this step took of each biting body's cap, in offered joules, by body id (D232). Filled by
        // SettleBites and read and cleared by DigestGuts in the same pass, so nothing of it survives a step and
        // nothing is checkpointed. Looked up and never iterated, so no order is taken from it.
        private readonly Dictionary<long, double> _gutBiteUse = new Dictionary<long, double>();

        // ------------------------------------------------------------------ the tissue of a body, part by part

        /// <summary>
        /// The tissue a body holds in the parts it still has: <c>Σ health × T</c>. A body with no health
        /// array is whole, and holds <see cref="Metabolism.TissueJoules(Phenotype, RunConfig)"/>.
        /// </summary>
        private double UneatenTissue(Phenotype body, float[] health)
        {
            if (health == null) return Metabolism.TissueJoules(body, Config);

            IReadOnlyList<PhenotypePart> parts = body.Parts;
            int freeLink = Config.FreeFirstLinkCubicMetres > 0f ? Metabolism.FreeLinkIndex(body) : -1;

            double total = 0d;
            for (int p = 0; p < parts.Count; p++)
            {
                double tissue = Metabolism.PartTissueJoules(body, parts[p], Config, freeLink);
                float h = p < health.Length ? health[p] : 1f;
                total += h < 1f ? (h > 0f ? h * tissue : 0d) : tissue;
            }

            return total;
        }

        /// <summary>The share of a body's tissue that has been eaten and not yet re-bought: <c>Σ (1 − health) × T</c>.</summary>
        private double EatenTissue(Phenotype body, float[] health)
        {
            if (health == null) return 0d;

            IReadOnlyList<PhenotypePart> parts = body.Parts;
            int freeLink = Config.FreeFirstLinkCubicMetres > 0f ? Metabolism.FreeLinkIndex(body) : -1;

            double total = 0d;
            int n = health.Length < parts.Count ? health.Length : parts.Count;
            for (int p = 0; p < n; p++)
            {
                if (health[p] >= 1f) continue;
                double tissue = Metabolism.PartTissueJoules(body, parts[p], Config, freeLink);
                total += (1d - Math.Max(0f, health[p])) * tissue;
            }

            return total;
        }

        /// <summary>The tissue a body would hold unbitten: its account plus what has been eaten of it.</summary>
        private double IntactTissue(Organism creature) =>
            creature.TissueJoules + EatenTissue(creature.Phenotype, creature.PartHealth);

        // ------------------------------------------------------------------ the remains

        private readonly List<Organism> _remains = new List<Organism>();

        // The foundings since the farm last drained them: pure instrumentation for the farm's solver, which poses a
        // new body from the old one's links. State all the same, in that a checkpoint is not a drain and so carries
        // what stands in it, as the lineage queue does.
        private List<RemainsFounding> _remainsFoundings = new List<RemainsFounding>();

        /// <summary>
        /// Joules biting parts have kept from remains, ever (D220): the bites on a dead body or a cut-off limb,
        /// their tissue and the share of the store that went with it. Split from the two totals above, which count
        /// the living bitten alone. Cumulative; 0 and not checkpointed with <see cref="RunConfig.ContactEating"/> off.
        /// </summary>
        public double BiteKeptFromRemainsJoules { get; private set; }

        /// <summary>Remains that have closed, ever (D220): the window passed, or nothing of them was left. Cumulative.</summary>
        public long RemainsClosed { get; private set; }

        /// <summary>
        /// Remains founded ever (D220): new bodies of remains cut off another, a limb or a part of remains eaten
        /// whole. A whole body that dies is counted in <see cref="Deaths"/> and not here. The farm's reconcile reads
        /// it, with <see cref="RemainsClosed"/>, as a reason to look at the solver's bodies. Cumulative; 0 and not
        /// checkpointed with <see cref="RunConfig.ContactEating"/> off.
        /// </summary>
        public long RemainsFounded { get; private set; }

        /// <summary>Joules burnt when remains closed, ever (D220): their tissue and store, out as heat and into the spent field. Cumulative.</summary>
        public double RemainsClosedJoules { get; private set; }

        /// <summary>
        /// The dead bodies and cut-off limbs lying in the water, in ascending id order (D220). Empty with
        /// <see cref="RunConfig.ContactEating"/> off. Nothing that walks <see cref="Living"/> walks these: they
        /// pay no upkeep, are not grown, healed, sensed or counted, and have no brain.
        /// </summary>
        public IReadOnlyList<Organism> Remains => _remains;

        /// <summary>How many remains lie in the water.</summary>
        public int RemainsStanding => _remains.Count;

        /// <summary>The joules the remains standing hold: tissue plus store. O(n), for a report row.</summary>
        public double RemainsStandingJoules
        {
            get
            {
                double sum = 0d;
                for (int i = 0; i < _remains.Count; i++) sum += _remains[i].TissueJoules + _remains[i].Energy + _remains[i].GestationJoules;
                return sum;
            }
        }

        /// <summary>
        /// Hands over every founding since the last call, and forgets them: <see cref="DrainLineageEvents"/>'
        /// pattern, and called beside it. A founding is a new body of remains cut off another, and carries the
        /// map the farm poses it by. A whole body that dies keeps its id and its solver body and is not one.
        /// </summary>
        public IReadOnlyList<RemainsFounding> DrainRemainsFoundings()
        {
            if (_remainsFoundings.Count == 0) return Array.Empty<RemainsFounding>();

            List<RemainsFounding> taken = _remainsFoundings;
            _remainsFoundings = new List<RemainsFounding>();
            return taken;
        }

        /// <summary>Burns <paramref name="joules"/> at a body's point: out as heat, the units into the spent field, booked in <see cref="BurntTotal"/>.</summary>
        private void BurnJoules(Organism at, double joules)
        {
            if (!(joules > 0d)) return;

            EnergyOut += joules;
            double units = joules / Config.JoulesPerUnit;
            Matter.DepositExact(at.Point, units);
            BurntTotal += units;
        }

        private void InsertRemains(Organism body)
        {
            int low = 0, high = _remains.Count;
            while (low < high)
            {
                int middle = (low + high) >> 1;
                if (_remains[middle].Id < body.Id) low = middle + 1;
                else high = middle;
            }

            _remains.Insert(low, body);
        }

        /// <summary>
        /// What a body that lost a limb was, taken before the plan changed: the body, its health, which of its parts
        /// the survivors are, and how it was developed (D220).
        /// </summary>
        private sealed class LimbCut
        {
            public Phenotype Old;
            public float[] OldHealth;
            public int[] NewToOld;
            public int DeadPart;
            public int[] Counts;
            public List<int[]> LostPaths;

            /// <summary>
            /// The body's <see cref="Organism.PartMapFromPreviousPlan"/> before this cut changed its plan: the map
            /// from the plan the farm's solver body stands on to the plan this cut starts from, or null where they
            /// are the same plan.
            /// </summary>
            public int[] PriorPending;
        }

        /// <summary>
        /// Where a death and a cut-off limb go with the dial on (D220): into remains a mouth can bite, or into the
        /// burn.
        /// </summary>
        /// <param name="creature">The body that died, or the one (living or remains) that lost the limb.</param>
        /// <param name="tissueJoules">The tissue that left the body's account.</param>
        /// <param name="reserveJoules">The reserve (and gestation account) that left with it.</param>
        /// <param name="wholeBody">True for a death, false for a limb.</param>
        /// <param name="killedBy">The body whose bite took it, or -1.</param>
        /// <param name="diverged">
        /// A body whose physics diverged is burnt and founds nothing: its geometry cannot be trusted.
        /// </param>
        /// <param name="cut">For a limb, what the body was before it lost it; without it the limb is burnt.</param>
        /// <remarks>
        /// <para>
        /// <b>A whole body that dies is itself the remains.</b> It leaves <c>_living</c> for <c>_remains</c> with its
        /// id, phenotype, part health and point; its tissue account stays as it was and its reserve and gestation
        /// account become its store, one number in <see cref="Organism.Energy"/>. Nothing leaves the world.
        /// </para>
        /// <para>
        /// <b>A limb is cut into one remains for each child subtree of the part that died</b> (and for each subtree
        /// a module drop or the developer's prune cut off), each taking its parts' health, the tissue at that health,
        /// and its share of the reserve by volume. The remains are new bodies with ids of their own. What no subtree
        /// can hold (the dead part's own share, a rounding) is burnt, so every joule given is placed or burnt.
        /// </para>
        /// </remarks>
        private void LeaveRemains(
            Organism creature, double tissueJoules, double reserveJoules, bool wholeBody, long killedBy,
            bool diverged = false, LimbCut cut = null)
        {
            if (wholeBody)
            {
                double joules = tissueJoules + reserveJoules;

                if (diverged || !(joules > 0d))
                {
                    creature.TissueJoules = 0d;
                    creature.Energy = 0d;
                    creature.GestationJoules = 0d;
                    BurnJoules(creature, joules);
                    return;
                }

                creature.TissueJoules = tissueJoules;
                creature.Energy = reserveJoules > 0d ? reserveJoules : 0d;
                creature.GestationJoules = 0d;
                creature.IsRemains = true;
                creature.RemainsSince = ElapsedSeconds;
                creature.RemainsMap = null;
                InsertRemains(creature);
                return;
            }

            if (cut == null)
            {
                BurnJoules(creature, tissueJoules + reserveJoules);
                return;
            }

            FoundLimbRemains(creature, cut, tissueJoules, reserveJoules);
        }

        /// <summary>
        /// Cuts <paramref name="tissueJoules"/> and <paramref name="reserveJoules"/> of a body into the remains of
        /// the subtrees it lost (<see cref="LeaveRemains"/>). The split is by what each subtree holds: tissue in
        /// proportion to its parts' tissue at their health, the store in proportion to its volume; the last takes
        /// the remainder, so the sums are exact.
        /// </summary>
        private void FoundLimbRemains(Organism from, LimbCut cut, double tissueJoules, double reserveJoules)
        {
            Phenotype old = cut.Old;
            IReadOnlyList<PhenotypePart> parts = old.Parts;
            int n = parts.Count;

            var kept = new bool[n];
            for (int i = 0; i < cut.NewToOld.Length; i++)
            {
                int from0 = cut.NewToOld[i];
                if (from0 >= 0 && from0 < n) kept[from0] = true;
            }

            var roots = new List<int>();
            for (int c = 0; c < n; c++)
            {
                if (kept[c] || c == cut.DeadPart) continue;

                int parent = parts[c].ParentIndex;
                if (parent < 0) continue;
                if (parent == cut.DeadPart || kept[parent]) roots.Add(c);
            }

            double tissueLeft = tissueJoules > 0d ? tissueJoules : 0d;
            double storeLeft = reserveJoules > 0d ? reserveJoules : 0d;

            if (roots.Count > 0)
            {
                // What each lost part holds at its health, priced as the body priced it: its free first link and all.
                int freeLink = Config.FreeFirstLinkCubicMetres > 0f ? Metabolism.FreeLinkIndex(old) : -1;
                var weight = new double[n];
                for (int k = 0; k < n; k++)
                {
                    float h = cut.OldHealth != null && k < cut.OldHealth.Length ? cut.OldHealth[k] : 1f;
                    if (!(h > 0f)) continue;

                    double tissue = Metabolism.PartTissueJoules(old, parts[k], Config, freeLink);
                    weight[k] = h < 1f ? h * tissue : tissue;
                }

                var maps = new int[roots.Count][];
                var bodies = new Phenotype[roots.Count];
                var weights = new double[roots.Count];
                var volumes = new double[roots.Count];
                double weightSum = 0d, volumeSum = 0d;

                for (int r = 0; r < roots.Count; r++)
                {
                    bodies[r] = old.SubtreeAt(roots[r], out maps[r]);
                    for (int k = 0; k < maps[r].Length; k++)
                    {
                        weights[r] += weight[maps[r][k]];
                        volumes[r] += Math.Max(0f, parts[maps[r][k]].Volume);
                    }

                    weightSum += weights[r];
                    volumeSum += volumes[r];
                }

                double tissueGiven = tissueLeft;
                double storeGiven = storeLeft;
                double since = from.IsRemains ? from.RemainsSince : ElapsedSeconds;

                // The remainder of each split goes to the last subtree that holds anything, so the shares sum to the
                // whole to the bit.
                int tissueLast = -1;
                for (int r = 0; r < roots.Count; r++) if (weights[r] > 0d) tissueLast = r;

                for (int r = 0; r < roots.Count; r++)
                {
                    bool last = r == roots.Count - 1;

                    double tissue = 0d;
                    if (weights[r] > 0d)
                    {
                        tissue = r == tissueLast ? tissueLeft : tissueGiven * weights[r] / weightSum;
                        if (tissue > tissueLeft) tissue = tissueLeft;
                        if (tissue < 0d) tissue = 0d;
                    }

                    double store = volumeSum > 0d
                        ? (last ? storeLeft : storeGiven * volumes[r] / volumeSum)
                        : (last ? storeLeft : storeGiven / roots.Count);
                    if (store > storeLeft) store = storeLeft;
                    if (store < 0d) store = 0d;

                    tissueLeft -= tissue;
                    storeLeft -= store;

                    // Nothing in it to eat, and nothing to burn: not a body the water keeps.
                    if (!(tissue + store > 0d)) continue;

                    var found = new Organism
                    {
                        Id = _nextId++,
                        ParentId = from.Id,
                        GenerationDepth = from.GenerationDepth,
                        SpeciesId = from.SpeciesId,
                        Genome = from.Genome,
                        ModuleCounts = cut.Counts == null ? null : (int[])cut.Counts.Clone(),
                        LostPartPaths = cut.LostPaths == null ? null : new List<int[]>(cut.LostPaths),
                        IndeterminateNodes = from.IndeterminateNodes,
                        Phenotype = bodies[r],
                        AdultPhenotype = bodies[r],
                        BodyFraction = 1f,
                        Age = from.Age,
                        HeightY = from.HeightY,
                        BirthHeightY = from.BirthHeightY,
                        X = from.X,
                        Z = from.Z,
                        Patch = from.Patch,
                        PartHealth = cut.OldHealth == null ? null : Remap(cut.OldHealth, maps[r]),
                        TissueJoules = tissue,
                        Energy = store,
                        IsRemains = true,
                        RemainsSince = since,
                        RemainsMap = from.IsRemains && from.RemainsMap != null ? Compose(from.RemainsMap, maps[r]) : maps[r],
                    };

                    found.HasAbsorptiveTissue = HasAbsorptive(bodies[r]);
                    found.AbsorptiveVolume = AbsorptiveVolumeOf(bodies[r]);
                    found.HasPhotosyntheticTissue = HasPhotosynthetic(bodies[r]);
                    ReadAttributeFlags(found, bodies[r]);

                    InsertRemains(found);
                    _remainsFoundings.Add(new RemainsFounding(found.Id, from.Id, maps[r], cut.PriorPending));
                    RemainsFounded++;
                    _lineageEvents.Add(LineageEvent.RemainsFoundedFrom(ElapsedSeconds, found.Id, from.Id));
                }
            }

            // What no subtree held. For a body with no subtree to lose it is the whole of it.
            BurnJoules(from, tissueLeft + storeLeft);
        }

        private static bool HasPhotosynthetic(Phenotype body)
        {
            IReadOnlyList<PhenotypePart> parts = body.Parts;
            for (int i = 0; i < parts.Count; i++) if (parts[i].CellTypeId == CellTypeIds.Photosynthetic) return true;
            return false;
        }

        /// <summary>
        /// Takes the parts of remains that reached zero health off them, a body at a time, until none is at zero
        /// (choice 9). Each part's child subtrees become new remains that keep the death second of the body they were
        /// cut from, so no bite renews a window.
        /// </summary>
        private void CollectTheDeadRemains()
        {
            for (int pass = 0; pass < 1000; pass++)
            {
                bool again = false;

                for (int i = _remains.Count - 1; i >= 0; i--)
                {
                    Organism body = _remains[i];
                    int dead = FirstDeadPart(body);
                    if (dead < 0) continue;

                    ShedFromRemains(body, i, dead);
                    again = true;
                }

                if (!again) return;
            }
        }

        private void ShedFromRemains(Organism body, int index, int dead)
        {
            Phenotype old = body.Phenotype;
            IReadOnlyList<PhenotypePart> parts = old.Parts;
            int n = parts.Count;
            float[] health = body.PartHealth;

            var region = new bool[n];
            region[dead] = true;
            for (int i = dead + 1; i < n; i++)
            {
                int parent = parts[i].ParentIndex;
                if (parent >= dead && region[parent]) region[i] = true;
            }

            int survivors = 0;
            for (int i = 0; i < n; i++) if (!region[i]) survivors++;

            var keep = new int[survivors];
            int at = 0;
            for (int i = 0; i < n; i++) if (!region[i]) keep[at++] = i;

            // What leaves with the region: its parts' tissue at their health out of the account, and its volume's
            // share of the store.
            int freeLink = Config.FreeFirstLinkCubicMetres > 0f ? Metabolism.FreeLinkIndex(old) : -1;
            double all = 0d, inRegion = 0d, volume = 0d, volumeInRegion = 0d;
            for (int k = 0; k < n; k++)
            {
                float h = health != null && k < health.Length ? health[k] : 1f;
                double tissue = h > 0f ? Metabolism.PartTissueJoules(old, parts[k], Config, freeLink) * (h < 1f ? h : 1f) : 0d;
                all += tissue;
                double v = Math.Max(0f, parts[k].Volume);
                volume += v;
                if (region[k])
                {
                    inRegion += tissue;
                    volumeInRegion += v;
                }
            }

            double movedTissue = all > 0d ? body.TissueJoules * inRegion / all : 0d;
            if (movedTissue > body.TissueJoules) movedTissue = body.TissueJoules;
            double movedStore = volume > 0d ? body.Energy * volumeInRegion / volume : 0d;
            if (movedStore > body.Energy) movedStore = body.Energy;

            var cut = new LimbCut
            {
                Old = old,
                OldHealth = health == null ? null : (float[])health.Clone(),
                NewToOld = keep,
                DeadPart = dead,
                Counts = body.ModuleCounts,
                LostPaths = body.LostPartPaths,
                PriorPending = body.PartMapFromPreviousPlan,
            };

            int[] source = body.RemainsMap;

            body.TissueJoules -= movedTissue;
            body.Energy -= movedStore;
            _lastAttacker.Remove(body.Id);

            FoundLimbRemains(body, cut, movedTissue, movedStore);

            if (survivors == 0)
            {
                // The root went: the body is its subtrees now, and what is left of it closes.
                CloseRemains(body, _remains.IndexOf(body));
                return;
            }

            Phenotype rest = old.Select(keep);

            body.PartHealth = Remap(health, keep);
            body.PartDamage = RemapFelt(body.PartDamage, keep);
            body.PartContact = RemapFelt(body.PartContact, keep);
            body.PartExposure = null;
            body.MouthPointCount = 0;

            int[] pending = body.PartMapFromPreviousPlan;
            body.PartMapFromPreviousPlan = pending != null ? Compose(pending, keep) : keep;

            body.Phenotype = rest;
            body.AdultPhenotype = rest;
            body.RemainsMap = source != null ? Compose(source, keep) : keep;
            body.AbsorptiveVolume = AbsorptiveVolumeOf(rest);
            body.HasAbsorptiveTissue = HasAbsorptive(rest);
            body.HasPhotosyntheticTissue = HasPhotosynthetic(rest);
            ReadAttributeFlags(body, rest);
            body.PlanRevision++;
        }

        /// <summary>
        /// Closes every remains body whose window has passed or that holds no parts (D220, choice 5): its tissue and
        /// its store are burnt, the joules into <see cref="EnergyOut"/> and the units into the spent field at its
        /// point, booked in <see cref="BurntTotal"/>. The window is whole until it closes; nothing decays before.
        /// </summary>
        private void CloseExpiredRemains()
        {
            double window = Config.RemainsWindowSeconds;

            for (int i = _remains.Count - 1; i >= 0; i--)
            {
                Organism body = _remains[i];
                if (body.Phenotype.PartCount > 0 && ElapsedSeconds - body.RemainsSince < window) continue;

                CloseRemains(body, i);
            }
        }

        /// <summary>
        /// Closes remains whose solver body diverged (D220, choice 7): what they hold is burnt at once, as a diverged
        /// living body's is, and they leave the list. For the farm; a body that is not standing remains is left alone.
        /// </summary>
        public void CloseDivergedRemains(Organism remains)
        {
            if (remains == null) throw new ArgumentNullException(nameof(remains));

            int index = _remains.IndexOf(remains);
            if (index < 0) return;

            CloseRemains(remains, index);
        }

        private void CloseRemains(Organism body, int index)
        {
            double joules = Math.Max(0d, body.TissueJoules) + Math.Max(0d, body.Energy) + Math.Max(0d, body.GestationJoules);

            body.TissueJoules = 0d;
            body.Energy = 0d;
            body.GestationJoules = 0d;

            BurnJoules(body, joules);
            RemainsClosed++;
            RemainsClosedJoules += joules;
            _lineageEvents.Add(LineageEvent.RemainsClosedAt(ElapsedSeconds, body.Id, joules));

            _lastAttacker.Remove(body.Id);
            if (index >= 0) _remains.RemoveAt(index);
        }

        /// <summary>
        /// A shadow at health, m²: the parts' lit area, each scaled by its part's health as the lit area a leaf bills
        /// is (D219, section 4). A body with no health array is whole and casts what it always did.
        /// </summary>
        public static float ShadowAtHealth(Organism creature, bool byExposure, bool capOn)
        {
            Phenotype body = creature.Phenotype;
            float[] exposure = byExposure ? creature.CurrentExposure : null;
            float[] health = creature.PartHealth;

            if (health == null)
            {
                return exposure == null
                    ? body.EffectiveLitArea(capOn)
                    : body.EffectiveLitArea(exposure, creature.UpInBody, capOn);
            }

            float litFactor = exposure == null
                ? body.LitAreaFactor(capOn)
                : body.LitAreaFactor(exposure, creature.UpInBody, capOn);

            float area = 0f;
            foreach (PhenotypePart part in body.Parts) area += Metabolism.LitAreaAtHealth(part, exposure, litFactor, health);
            return area;
        }

        // ------------------------------------------------------------------ the bite

        private struct BiteOffer
        {
            public Organism Biter;
            public Organism Bitten;
            public int Slot;
            public double Joules;
        }

        // One pass's scratch, cleared at the top of every damage pass: the offers in the order the wounds
        // were made, and per biting part that carries intake its summed offer and its cap. The dictionary is
        // looked up and never iterated, so no order is taken from it.
        private readonly List<BiteOffer> _biteOffers = new List<BiteOffer>();
        private readonly Dictionary<(long Id, int Part), int> _biteSlots = new Dictionary<(long Id, int Part), int>();
        private readonly List<double> _biteTotals = new List<double>();
        private readonly List<double> _biteCaps = new List<double>();

        /// <summary>
        /// A wound that took <paramref name="taken"/> of a part's health takes that share of the part's tissue
        /// out of the bitten body's account, and offers it to the biting part (spec section 1). Nothing moves
        /// to the biter here: every offer of the pass is settled together by <see cref="SettleBites"/>.
        /// </summary>
        private void OfferBite(
            Organism biter, int biterPart, PhenotypePart biting, Organism bitten, int bittenPart,
            float taken, float seconds)
        {
            if (!(taken > 0f)) return;
            if (bittenPart < 0 || bittenPart >= bitten.Phenotype.PartCount) return;

            double tissue = Metabolism.PartTissueJoules(bitten.Phenotype, bitten.Phenotype.Parts[bittenPart], Config);
            double offer = taken * tissue;

            // The account is the tissue of the parts still held, so a wound cannot take more than it has; a
            // rounding that would is cut here rather than left to drive the account below zero.
            double accountBefore = bitten.TissueJoules;
            if (offer > bitten.TissueJoules) offer = bitten.TissueJoules > 0d ? bitten.TissueJoules : 0d;
            if (!(offer > 0d)) return;

            bitten.TissueJoules -= offer;

            // D220, choice 4: remains give the store by the same share. A bite that takes a share of the tissue
            // account takes that share of the store, kept or wasted with the tissue.
            if (bitten.IsRemains && accountBefore > 0d && bitten.Energy > 0d)
            {
                double fromStore = bitten.Energy * (offer / accountBefore);
                if (fromStore > bitten.Energy) fromStore = bitten.Energy;
                bitten.Energy -= fromStore;
                offer += fromStore;
            }
            else if (Config.BiteTakesStore && !bitten.IsRemains)
            {
                // D224: a bite on a living body takes the same share of its reserve and its gestation account. The
                // share is the tissue's, taken before either joins the offer; a reserve not above zero gives nothing.
                double share = offer / accountBefore;
                double fromStore = 0d;

                if (bitten.Energy > 0d)
                {
                    double fromReserve = bitten.Energy * share;
                    if (fromReserve > bitten.Energy) fromReserve = bitten.Energy;
                    bitten.Energy -= fromReserve;
                    fromStore += fromReserve;
                }

                if (bitten.GestationJoules > 0d)
                {
                    double fromGestation = bitten.GestationJoules * share;
                    if (fromGestation > bitten.GestationJoules) fromGestation = bitten.GestationJoules;
                    bitten.GestationJoules -= fromGestation;
                    fromStore += fromGestation;
                }

                offer += fromStore;
                BiteStoreTakenJoules += fromStore;
            }

            // Remains have no brain and so no mouth that works: their part's blow lands as a claw's would, but what
            // the wound takes from the body it wounded is all waste, since nothing is kept.
            int slot = -1;
            if (biting.Intake > 0f && !biter.IsRemains)
            {
                (long Id, int Part) key = (biter.Id, biterPart);
                if (!_biteSlots.TryGetValue(key, out slot))
                {
                    // Intake times surface area, times the scale, times the step, at the price of a unit.
                    double rate = (double)biting.Intake * Math.Max(0f, biting.SurfaceArea);
                    if (Config.IntakeScale != 1f) rate *= Config.IntakeScale;

                    slot = _biteCaps.Count;
                    _biteSlots[key] = slot;
                    _biteCaps.Add(rate * seconds * Config.JoulesPerUnit);
                    _biteTotals.Add(0d);
                }

                _biteTotals[slot] += offer;
            }

            _biteOffers.Add(new BiteOffer { Biter = biter, Bitten = bitten, Slot = slot, Joules = offer });
        }

        /// <summary>
        /// Settles the pass's bites in the order the wounds were made. Each offer keeps
        /// <c>min(1, cap / total offered) × offer</c> if its biting part carries intake and nothing if it does
        /// not; of what is kept the biter's reserve gets <c>1 − IntakeWasteFraction</c>, and the rest of the
        /// offer goes to Nutrients at the bitten body's point, quantised once at the field's door as
        /// <see cref="Take"/> does it and booked in <see cref="DetritusDepositedTotal"/>.
        /// </summary>
        private void SettleBites()
        {
            if (_biteOffers.Count == 0) return;

            double keptShare = 1d - Config.IntakeWasteFraction;
            bool gutOn = Config.KillSwallowsBody;

            for (int i = 0; i < _biteOffers.Count; i++)
            {
                BiteOffer o = _biteOffers[i];

                double keep = 0d;
                if (o.Slot >= 0)
                {
                    double total = _biteTotals[o.Slot];
                    double cap = _biteCaps[o.Slot];
                    double fraction = total > cap ? cap / total : 1d;
                    keep = fraction * o.Joules * keptShare;

                    if (gutOn)
                    {
                        // D232: the share of the cap this offer used, in offered joules, is what the gut finds gone
                        // when it digests; and what the cap cut off the offer is the screen's instrument.
                        double underCap = fraction * o.Joules;
                        _gutBiteUse.TryGetValue(o.Biter.Id, out double usedBefore);
                        _gutBiteUse[o.Biter.Id] = usedBefore + underCap;
                        if (fraction < 1d) BiteCapCutJoules += o.Joules - underCap;
                    }
                }

                // The field takes a float and these are doubles. The water's share is decided in the field's
                // precision first and the biter's reserve takes the difference, so what the bitten body loses
                // and what the reserve and the water gain are one number (Take's rule). A bite that keeps
                // nothing gives the biter nothing.
                float waste = (float)(o.Joules - keep);
                double toReserve = keep > 0d ? o.Joules - waste : 0d;
                if (toReserve < 0d) toReserve = 0d;

                if (waste > 0f)
                {
                    Nutrients.Deposit(o.Bitten.Point, waste);
                    DetritusDepositedTotal += waste;
                    BiteWasteJoules += waste;
                }

                if (toReserve > 0d)
                {
                    o.Biter.Energy += toReserve;
                    o.Biter.BiteKeptJoules += toReserve;

                    if (o.Bitten.IsRemains) BiteKeptFromRemainsJoules += toReserve;
                    else if (o.Bitten.HasPhotosyntheticTissue)
                    {
                        BiteKeptFromLeavesJoules += toReserve;
                        o.Biter.FoodFromLeavesJoules += toReserve;
                    }
                    else BiteKeptFromOthersJoules += toReserve;
                }
            }

            _biteOffers.Clear();
            _biteSlots.Clear();
            _biteTotals.Clear();
            _biteCaps.Clear();
        }

        // ------------------------------------------------------------------ the gut (D232)

        /// <summary>
        /// The living body that swallows <paramref name="victim"/>, or null (D232).
        /// </summary>
        /// <param name="victim">The body a bite killed, still in the living when this is asked.</param>
        /// <param name="killedBy">
        /// The id <see cref="Wound"/> last recorded against the part whose death was the body's (<see cref="AttackerOf"/>):
        /// whichever blow of the pass reached that part last, in the order of the contact list, whether or not it was
        /// the one that took the last of the part's health, and whether its part carries a mouth or a claw alone.
        /// </param>
        /// <remarks>
        /// The recorded killer swallows, and only when it is a living body with a mouth at this moment: a killer whose
        /// mouths take nothing (a claw alone), a body of remains, an id no
        /// one holds, no attacker at all and a killer that died earlier in the same collection (the living are walked
        /// from the back, and a killer is buried by its own turn) all leave remains as they do with the switch off. If
        /// two bodies kill each other in one step the one the walk reaches first is swallowed and the other, reached
        /// next, finds its killer gone and leaves remains holding what it had swallowed.
        /// </remarks>
        private Organism SwallowerOf(Organism victim, long killedBy)
        {
            if (killedBy < 0L) return null;

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism candidate = _living[i];
                if (candidate.Id != killedBy) continue;

                // Swallowing is a mouth's act (the coordinator's reading of D232): a killer whose mouths take nothing
                // leaves remains, as a claw-only kill does with the switch off.
                if (ReferenceEquals(candidate, victim) || !(MouthRate(candidate, 1f) > 0d)) return null;
                return candidate;
            }

            return null;
        }

        /// <summary>
        /// A kill goes into a gut (D232): the dead body's tissue and its store (which already holds what its own gut
        /// held) become the killer's gut, and the body leaves without remains. Charged matter moves from one body's
        /// accounts to another's, which <see cref="StandingJoules"/> sums, so nothing is booked.
        /// </summary>
        private void SwallowWhole(Organism killer, Organism victim, double tissueJoules, double storeJoules)
        {
            double joules = (tissueJoules > 0d ? tissueJoules : 0d) + (storeJoules > 0d ? storeJoules : 0d);

            victim.TissueJoules = 0d;
            victim.Energy = 0d;
            victim.GestationJoules = 0d;
            victim.GutJoules = 0d;

            if (!(joules > 0d)) return;

            killer.GutJoules += joules;
            KillsSwallowed++;
            SwallowedJoules += joules;
        }

        /// <summary>
        /// Each body with a gut digests it into its reserve, at most what its mouth's per-step cap left after its bites
        /// (D232). The cap is the one a bite meets, <c>intake × area × IntakeScale × step × joules a unit</c> summed over
        /// the body's biting parts (<see cref="MouthRate"/>); the bites of the pass used theirs first and
        /// <see cref="SettleBites"/> left the amount in <c>_gutBiteUse</c>, so the gut fills what is left. A body whose
        /// cap is 0 digests nothing and keeps its gut. Of what is digested the intake waste goes to Nutrients at the
        /// body's point and the rest into its reserve, as a kept bite's share does; the gut loses what both receive.
        /// </summary>
        /// <remarks>
        /// One walk of the living in list order, each body touching only its own accounts and the field's deposit, so
        /// the order decides nothing but the order of the deposits, which is the list's and so the same at any thread
        /// count (nothing here runs on a thread of its own).
        /// </remarks>
        private void DigestGuts(float seconds)
        {
            double keptShare = 1d - Config.IntakeWasteFraction;
            double perUnit = Config.JoulesPerUnit;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism body = _living[i];
                double gut = body.GutJoules;
                if (!(gut > 0d)) continue;

                double cap = MouthRate(body, seconds) * perUnit;
                double room = cap;
                if (_gutBiteUse.TryGetValue(body.Id, out double used)) room -= used;
                if (!(room > 0d)) room = 0d;

                double digest = gut < room ? gut : room;

                if (digest < gut)
                {
                    GutCapCutJoules += gut - digest;
                    GutCapBound++;
                }

                if (!(digest > 0d)) continue;

                // The water's share is decided in the field's precision first and the reserve takes the difference,
                // as SettleBites does, so what the gut loses and what the reserve and the water gain are one number.
                // Unlike a bite's, the difference is not floored at 0: a rounding of the waste up past the digest (only
                // at a waste fraction of 1) must not leave the books a float's error short.
                double keep = digest * keptShare;
                float waste = (float)(digest - keep);
                double toReserve = digest - waste;

                body.GutJoules = gut - digest;
                body.Energy += toReserve;
                GutDigestedJoules += digest;

                if (waste > 0f)
                {
                    Nutrients.Deposit(body.Point, waste);
                    DetritusDepositedTotal += waste;
                    GutWasteJoules += waste;
                }
            }

            _gutBiteUse.Clear();
        }
    }

    /// <summary>
    /// A new body of remains cut off another (D220): the id it was given, the body it was cut from (a living body or
    /// remains), and for each of its parts the index of the part in that body. The farm poses the new body from the
    /// old one's links with it. A whole body that dies keeps its id and is not a founding.
    /// </summary>
    public sealed class RemainsFounding
    {
        public RemainsFounding(long remainsId, long fromBodyId, int[] map, int[] sourcePrior = null)
        {
            RemainsId = remainsId;
            FromBodyId = fromBodyId;
            Map = map;
            SourcePrior = sourcePrior;
        }

        /// <summary>
        /// The source body's own pending map when the cut was made: for each part of the plan the cut started from,
        /// its index in the plan the source's solver body was last built on (or, for a source founded in the same
        /// step, in the plan it was founded on); null where the two are the same plan. Read it with
        /// <see cref="Map"/>: part <c>k</c> of the new body is link <c>SourcePrior[Map[k]]</c> of the source's solver
        /// body, or <c>Map[k]</c> where this is null. A cut after another in one step is the case it exists for.
        /// </summary>
        public int[] SourcePrior { get; }

        /// <summary>The new remains' id.</summary>
        public long RemainsId { get; }

        /// <summary>The id of the body it was cut from.</summary>
        public long FromBodyId { get; }

        /// <summary>For each part of the new body, its index in the body it was cut from. Ascending.</summary>
        public int[] Map { get; }
    }
}
