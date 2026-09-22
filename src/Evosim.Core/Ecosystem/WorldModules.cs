using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The module gene's rule: a body adds a part while it is rich and drops one when it has
    /// been poor for long enough — D106 item 2, <c>logbook/specs/module-gene-spec.md</c> rules
    /// 2 to 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is not in <see cref="Step"/>.</b> D087's growth is a transfer, so it is paid on
    /// every metabolic step; this is a change of <i>plan</i>, and a plan change costs the harness
    /// a rebuilt articulation. So it runs at the harness's growth cadence
    /// (<see cref="RunConfig.GrowthStepSeconds"/>), which Core does not know — the farm calls
    /// <see cref="ApplyModuleRule"/> from the same place it applies a grown body. A Core-only
    /// world calls it itself, which is how the tests drive it.
    /// </para>
    /// <para>
    /// <b>Nothing here draws from an <see cref="Rng"/>.</b> Which node is added and which is
    /// dropped are index rules, the price is arithmetic on a developed body, and every refusal is
    /// a comparison. A world in which no genome carries an indeterminate node — which is every
    /// world in the record — steps through exactly the trajectory it would have without this
    /// pass, and the regress against <c>dblB</c> is what says so.
    /// </para>
    /// <para>
    /// <b>Both books close by construction.</b> An addition moves reserve into tissue, which are
    /// the two accounts <see cref="StandingJoules"/> already sums; a drop moves tissue and a
    /// share of the reserve into a corpse or into the water, which is the transfer
    /// <see cref="Bury"/> has always made. Nothing is created and nothing leaves, so
    /// <see cref="AuditResidual"/> and <see cref="MatterResidual"/> are where they were.
    /// </para>
    /// </remarks>
    public sealed partial class World
    {
        /// <summary>Modules ever added by rule 2. Cumulative; a window is two samples differenced.</summary>
        public long ModuleAdds { get; private set; }

        /// <summary>Modules ever dropped by rule 3. Cumulative.</summary>
        public long ModuleDrops { get; private set; }

        /// <summary>
        /// Additions the rule attempted and refused, ever — rule 5's counter. Cumulative.
        /// </summary>
        /// <remarks>
        /// <b>A count of attempts, not of bodies</b>, like
        /// <see cref="ConceptionsUnderMassFloor"/>: a body refused this growth step keeps its
        /// reserve and asks again at the next one, so a handful of plants standing against
        /// D101's test produce a large number over a run. It is read against
        /// <see cref="ModuleAdds"/> in the same window and never as a level — which is exactly
        /// what round 44's H4 asks of it (logbook/0113). It is the sum of the two below.
        /// </remarks>
        public long ModuleAddsRefused { get; private set; }

        /// <summary>
        /// The refusals of rule 5's shape test: the candidate body was no larger, was cut by the
        /// part or depth limit, or stood inside itself under D101. Cumulative.
        /// </summary>
        /// <remarks>
        /// Round 44 counted 22 to 1,255 refusals per add with one counter for two reasons and
        /// could not say which sieve bound (logbook/0113's read); this and
        /// <see cref="ModuleAddsRefusedForReserve"/> are that split. A count only: the rule's
        /// choice and the trajectory are untouched.
        /// </remarks>
        public long ModuleAddsRefusedForShape { get; private set; }

        /// <summary>
        /// The refusals of rule 2's last clause: the body passed the shape test and could not
        /// pay the module's tissue from its reserve. Cumulative.
        /// </summary>
        public long ModuleAddsRefusedForReserve { get; private set; }

        /// <summary>
        /// Living indeterminate modules beyond the genome minimum, summed — rule 8's
        /// <c>modules</c>. Instantaneous, not cumulative.
        /// </summary>
        /// <remarks>
        /// O(n) over the living and over their indeterminate nodes, which is an instrument for a
        /// report row rather than for the step — <see cref="StandingJoulesInBodies"/>'s rule.
        /// Zero for the whole life of a world whose genomes are all determinate.
        /// </remarks>
        public long ModulesStanding
        {
            get
            {
                long standing = 0L;

                for (int i = 0; i < _living.Count; i++)
                {
                    Organism creature = _living[i];
                    int[] counts = creature.ModuleCounts;
                    if (counts == null) continue;

                    IReadOnlyList<MorphNode> nodes = creature.Genome.Nodes;
                    int n = counts.Length < nodes.Count ? counts.Length : nodes.Count;

                    for (int node = 0; node < n; node++)
                    {
                        if (nodes[node].Growth != ModuleGrowth.Indeterminate) continue;

                        int beyond = counts[node] - nodes[node].RecursiveLimit;
                        if (beyond > 0) standing += beyond;
                    }
                }

                return standing;
            }
        }

        /// <summary>
        /// The share of the living carrying any indeterminate node, 0 to 1 — rule 8's
        /// <c>indet %</c> over 100. 0 on an empty world.
        /// </summary>
        public float IndeterminateShare
        {
            get
            {
                if (_living.Count == 0) return 0f;

                int carrying = 0;
                for (int i = 0; i < _living.Count; i++)
                {
                    if (_living[i].IndeterminateNodes > 0) carrying++;
                }

                return (float)carrying / _living.Count;
            }
        }

        /// <summary>
        /// Runs rules 2 to 5 over the living, once. Returns how many bodies changed their plan.
        /// </summary>
        /// <param name="seconds">
        /// Simulated seconds since the last call — rule 3's clock, and nothing else. It is the
        /// harness's growth interval rather than the metabolic step, because that is how often
        /// this is asked, and a clock counted in calls would mean something different at a
        /// different cadence.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>One add or one drop per body, and the drop is asked first.</b> A body under the
        /// drop line is by definition not over the add line — the two thresholds are on the same
        /// reserve — so asking in either order gives the same answer for any sane pair of
        /// settings. It is asked first anyway, so that a launcher which crosses the two cannot
        /// produce a body that adds a part while it is starving.
        /// </para>
        /// <para>
        /// <b>A body whose plan changed carries <see cref="Organism.PlanRevision"/> forward</b>
        /// and the farm rebuilds its articulation from it (rule 7). Core does not know what an
        /// articulation is, which is why the signal is a number on the organism rather than a
        /// call.
        /// </para>
        /// </remarks>
        public int ApplyModuleRule(float seconds)
        {
            bool addOn = Config.ModuleAddReserveSeconds > 0f;
            bool dropOn = Config.ModuleDropReserveSeconds > 0f;

            // The whole rule, switched off by its own defaults. Read before the walk rather than
            // inside it: at the defaults this method is one comparison per growth step for the
            // whole run, which is what lets it be called unconditionally.
            if (!addOn && !dropOn) return 0;

            int changed = 0;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (creature.IndeterminateNodes == 0) continue;

                double watts = creature.StandingWatts;

                if (dropOn)
                {
                    bool under = creature.Energy < (double)Config.ModuleDropReserveSeconds * watts;

                    creature.ModuleStarvedSeconds = under
                        ? creature.ModuleStarvedSeconds + seconds
                        : 0f;

                    if (under &&
                        creature.ModuleStarvedSeconds >= Config.ModuleDropAfterSeconds &&
                        DropOneModule(creature))
                    {
                        changed++;
                        continue;
                    }
                }

                if (addOn && creature.Energy > (double)Config.ModuleAddReserveSeconds * watts &&
                    AddOneModule(creature))
                {
                    changed++;
                }
            }

            return changed;
        }

        // ------------------------------------------------------------------ rule 2: the addition

        /// <summary>
        /// Adds one module of the lowest indeterminate node that is under its ceiling, or refuses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The lowest index under its ceiling</b>, so the choice is a function of the genome
        /// and of the body's counts and of nothing else — no draw, no iteration order, no tie to
        /// break. It fills a node to its ceiling before it starts on the next, which is what
        /// makes <see cref="DropOneModule"/>'s "highest above its minimum" the exact inverse:
        /// the two together are a stack, and a drop removes the module the last add put on.
        /// </para>
        /// <para>
        /// <b>Refused rather than pruned</b> when the candidate would meet
        /// <see cref="DevelopmentLimits.MaxParts"/> or <see cref="DevelopmentLimits.MaxDepth"/>
        /// (rule 5). Development would happily build the body it can and drop the rest, which
        /// would charge the creature for a module and hand it a fraction of one; the counter says
        /// the addition did not happen instead.
        /// </para>
        /// </remarks>
        private bool AddOneModule(Organism creature)
        {
            IReadOnlyList<MorphNode> nodes = creature.Genome.Nodes;
            int[] counts = CountsOf(creature);

            int chosen = -1;
            for (int n = 0; n < nodes.Count && chosen < 0; n++)
            {
                if (nodes[n].Growth != ModuleGrowth.Indeterminate) continue;
                if (counts[n] < CeilingOf(nodes[n])) chosen = n;
            }

            // Nothing to attempt: every indeterminate node is already at its ceiling. Not a
            // refusal — the rule did not try — so nothing is counted.
            if (chosen < 0) return false;

            var next = (int[])counts.Clone();
            next[chosen]++;

            Phenotype adult = DevelopPlan(creature, next, out List<int[]> afterPaths);

            Phenotype was = creature.AdultPhenotype;

            // Rule 5's three refusals. The body must be larger than it was (a module development
            // pruned for its own volume is not a module), it must not have been cut short by the
            // part or depth limits any more than the body already was, and it must not stand
            // inside itself under D101's test — which is scale-free, so asking it of the adult
            // asks it of every size this body will ever be, exactly as Admit does of a newborn.
            if (adult.PartCount <= was.PartCount ||
                adult.PrunedForParts > was.PrunedForParts ||
                adult.PrunedForDepth > was.PrunedForDepth ||
                adult.PrunedForReach > was.PrunedForReach ||
                (Config.SelfOverlapDepthFraction > 0f &&
                 adult.SelfOverlappingPairs(Config.SelfOverlapDepthFraction) > 0))
            {
                ModuleAddsRefused++;
                ModuleAddsRefusedForShape++;
                return false;
            }

            double adultTissue = Metabolism.TissueJoules(adult, Config);
            Phenotype body = AtTheSameFraction(adult, creature.BodyFraction);
            double tissue = ReferenceEquals(body, adult)
                ? adultTissue
                : Metabolism.TissueJoules(body, Config);

            double spend = tissue - creature.TissueJoules;

            // Rule 2's last clause: paid in full from the reserve or not paid at all. The body
            // keeps its reserve and asks again next growth step, which is what makes a module a
            // thing a lineage saves for.
            if (spend > creature.Energy)
            {
                ModuleAddsRefused++;
                ModuleAddsRefusedForReserve++;
                return false;
            }

            creature.Energy -= spend;

            Rebuild(creature, counts, next, afterPaths, adult, adultTissue, body, tissue);

            ModuleAdds++;
            return true;
        }

        // ------------------------------------------------------------------ rule 3: the drop

        /// <summary>
        /// Drops the last-added module and leaves it as a corpse, or refuses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The highest indeterminate node above its own minimum</b> — the exact inverse of
        /// <see cref="AddOneModule"/>'s choice, so the module that goes is the module that came
        /// last, which is what rule 3 asks for without a stack having to be written down and
        /// checkpointed.
        /// </para>
        /// <para>
        /// <b>The matter is not lost</b> (D106 item 1's corpse path). What the body sheds is the
        /// tissue the module was worth plus the module's share of the one reserve, by volume, and
        /// it goes the way a death's remains go: into <see cref="Corpses"/> above
        /// <see cref="RunConfig.CorpseDecayPerSecond"/> 0 and straight into the water at 0. The
        /// body's own two accounts fall by exactly what the water or the corpse gains, so neither
        /// book moves.
        /// </para>
        /// </remarks>
        private bool DropOneModule(Organism creature)
        {
            IReadOnlyList<MorphNode> nodes = creature.Genome.Nodes;
            int[] counts = CountsOf(creature);

            int chosen = -1;
            for (int n = nodes.Count - 1; n >= 0 && chosen < 0; n--)
            {
                if (nodes[n].Growth != ModuleGrowth.Indeterminate) continue;
                if (counts[n] > nodes[n].RecursiveLimit) chosen = n;
            }

            // A body already at its genome's own plan has nothing to shed: rule 3 drops modules,
            // never the body the genome describes. It starves in the ordinary way instead.
            if (chosen < 0) return false;

            var next = (int[])counts.Clone();
            next[chosen]--;

            Phenotype adult = DevelopPlan(creature, next, out List<int[]> afterPaths);

            Phenotype was = creature.AdultPhenotype;
            Phenotype wasBody = creature.Phenotype;

            // Nothing was shed, or everything was. Neither is a drop: the first is a count that
            // development does not express and the second is a death, which is §5A.6's business
            // and not this rule's.
            if (adult.PartCount == 0 || adult.PartCount >= was.PartCount) return false;

            double adultTissue = Metabolism.TissueJoules(adult, Config);
            Phenotype body = AtTheSameFraction(adult, creature.BodyFraction);
            double tissue = ReferenceEquals(body, adult)
                ? adultTissue
                : Metabolism.TissueJoules(body, Config);

            double released = creature.TissueJoules - tissue;
            if (!(released > 0d)) return false;

            // The module's share of the one reserve, by volume — D106's "pro-rata share of the
            // body's one reserve (its volume over the body's)". Strictly under 1, because the
            // root is never a module and therefore something always remains.
            double share = wasBody.TotalVolume > 0f
                ? 1d - (double)body.TotalVolume / wasBody.TotalVolume
                : 0d;

            if (share < 0d) share = 0d;
            else if (share > 1d) share = 1d;

            double reserve = creature.Energy > 0d ? creature.Energy * share : 0d;

            // The field takes a float and these accounts are doubles, so the amount that leaves
            // the body is decided in the field's own precision first and the reserve leg is the
            // difference — which makes what the body loses and what the water gains the same
            // number, rather than two numbers a rounding apart. Bury hands its float over the
            // same way; what is new here is that the reserve, not the audit, absorbs the ulp.
            float given = (float)(released + reserve);
            double moved = given;
            double fromReserve = moved - released;

            creature.Energy -= fromReserve;
            creature.ModuleStarvedSeconds = 0f;

            Rebuild(creature, counts, next, afterPaths, adult, adultTissue, body, tissue);

            ShedRemains(creature, given);

            ModuleDrops++;
            return true;
        }

        /// <summary>
        /// Puts a dropped module's tissue and reserve share where a death would put them.
        /// </summary>
        /// <remarks>
        /// <see cref="Bury"/>'s two branches, and deliberately the same two: above
        /// <see cref="RunConfig.CorpseDecayPerSecond"/> 0 the module is an object with a place
        /// that the water carries and leaks, and at 0 it is detritus where the body stands. A
        /// third path would be a third thing for the corpse pass and the flux identity to agree
        /// with.
        /// </remarks>
        private void ShedRemains(Organism creature, float remains)
        {
            if (!(remains > 0f)) return;

            if (Config.CorpseDecayPerSecond > 0f)
            {
                _corpses.Add(new Corpse(
                    creature.Id, creature.Point.Position, creature.Patch, remains));

                return;
            }

            Nutrients.Deposit(creature.Point, remains);
            DetritusDepositedTotal += remains;
        }

        // ------------------------------------------------------------------ the shared half

        /// <summary>
        /// A module rule's plan change: the count that moved, the map from the old plan, and then
        /// <see cref="AdoptPlan"/> for everything a changed plan invalidates.
        /// </summary>
        private void Rebuild(
            Organism creature, int[] was, int[] now, List<int[]> after,
            Phenotype adult, double adultTissue, Phenotype body, double tissue)
        {
            // The map the harness rebuilds on (rule 7). The old plan is developed again with its
            // part paths, which is the only thing that identifies the same part across a count
            // change — Developer.MatchParts says why an index does not. The new plan's paths come
            // in from the caller, which already had to develop it to price the module. One extra
            // development, on an event that happens at most once per body per growth step and
            // almost never in a world of determinate lineages.
            _ = DevelopPlan(creature, was, out List<int[]> before);

            creature.ModuleCounts = now;

            AdoptPlan(
                creature, adult, adultTissue, body, tissue, Developer.MatchParts(before, after));
        }

        /// <summary>
        /// Puts a creature on a new body plan: the two phenotypes, the two tissue figures, the map
        /// the harness rebuilds on, and every cached reading a changed plan invalidates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The cell-type flags are refreshed, and that is the line that matters.</b>
        /// <see cref="Organism.HasAbsorptiveTissue"/> and its neighbours are cached at birth on
        /// the grounds that growth changes a body's size and never what it is made of — which was
        /// true until this rule existed. A module is a whole subtree of the genome and may be
        /// made of anything, so a body that has just grown one can be a mixotroph where it was a
        /// leaf, and an instrument reading a stale flag would file it under what it used to be.
        /// D106 item 1 adds the mirror case: a body that has just <i>lost</i> its only mouth to a
        /// bite is not a feeder any more.
        /// </para>
        /// <para>
        /// <b>Shared by the module rule and by the kill</b>, which is the point of it being its
        /// own method: a part added and a part bitten off are the same event to everything
        /// downstream, and two copies of this would be two chances for one of them to forget the
        /// health array or the standing cost.
        /// </para>
        /// </remarks>
        private void AdoptPlan(
            Organism creature, Phenotype adult, double adultTissue, Phenotype body, double tissue,
            int[] map)
        {
            // D106 item 3. The wounds the survivors were carrying, carried onto their new indices;
            // a part that has just appeared (map -1) is whole, which is rule 3's "full at a module
            // add". Null in, null out, so a world that has never been bitten allocates nothing.
            creature.PartHealth = Remap(creature.PartHealth, map);
            creature.PartDamage = null;
            creature.PartContact = null;

            creature.PartMapFromPreviousPlan = map;

            creature.AdultPhenotype = adult;
            creature.AdultTissueJoules = adultTissue;
            creature.Phenotype = body;
            creature.TissueJoules = tissue;

            creature.BodyFraction = adultTissue > 0d && tissue < adultTissue
                ? (float)(tissue / adultTissue)
                : 1f;

            // At this creature's own age, so a body that has just changed plan does not quietly
            // reset its senescence — Grow's rule, for Grow's reason (D038).
            creature.StandingWatts = Metabolism.StandingWatts(body, Config, creature.Age);

            creature.AbsorptiveVolume = AbsorptiveVolumeOf(body);
            creature.HasAbsorptiveTissue = HasAbsorptive(body);

            bool photosynthetic = false;
            IReadOnlyList<PhenotypePart> parts = body.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].CellTypeId == CellTypeIds.Photosynthetic) photosynthetic = true;
            }

            creature.HasPhotosyntheticTissue = photosynthetic;
            ReadAttributeFlags(creature, body);
            creature.PlanRevision++;
        }

        /// <summary>
        /// A per-part array carried onto a new plan: <c>map[i]</c> is where part <c>i</c> of the
        /// new body stood in the old one, or -1 for a part that has just appeared.
        /// </summary>
        /// <remarks>
        /// Null in is null out — a body with no wounds recorded stays a body with no wounds
        /// recorded — and a part that has just appeared reads 1, which is full. That is the one
        /// place rule 3's "full at a module add" is written down.
        /// </remarks>
        private static float[] Remap(float[] was, int[] map)
        {
            if (was == null || map == null) return null;

            var now = new float[map.Length];
            for (int i = 0; i < map.Length; i++)
            {
                int from = map[i];
                now[i] = from >= 0 && from < was.Length ? was[from] : 1f;
            }

            return now;
        }

        /// <summary>
        /// The three D106 attribute flags, read off a developed body — <see cref="Organism.HasAttack"/>.
        /// </summary>
        internal static void ReadAttributeFlags(Organism creature, Phenotype body)
        {
            bool attack = false, intake = false, protection = false;
            IReadOnlyList<PhenotypePart> parts = body.Parts;

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Attack > 0f) attack = true;
                if (parts[i].Intake > 0f) intake = true;
                if (parts[i].Protection > 0f) protection = true;
            }

            creature.HasAttack = attack;
            creature.HasIntake = intake;
            creature.HasProtection = protection;
        }

        /// <summary>
        /// Develops a creature's genome at a set of module counts and takes off whatever it has
        /// already lost — D106 items 1 and 2 in one call, with the surviving parts' paths.
        /// </summary>
        /// <param name="creature">The body whose genome, shapes and losses are being expressed.</param>
        /// <param name="counts">The module counts to develop at.</param>
        /// <param name="paths">
        /// Filled with the developer's path to each surviving part, in part order — what
        /// <see cref="Developer.MatchParts"/> matches two plans on.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Every rebuild in the world goes through here, and that is what keeps a lost part
        /// lost.</b> A module added, a module dropped and a checkpoint restored all express the
        /// genome again from nothing; without this, each of them would hand a bitten creature its
        /// severed limb back, for free, as a side effect of a rule about something else.
        /// </para>
        /// <para>
        /// <b>At no losses it is <see cref="Developer.Develop"/> and nothing more</b> — the same
        /// call with the same arguments, returning the same object — so a world in which nothing
        /// has ever been bitten, which is every world in the record, is untouched by it.
        /// </para>
        /// </remarks>
        private Phenotype DevelopPlan(Organism creature, int[] counts, out List<int[]> paths)
        {
            paths = new List<int[]>(Config.Development.MaxParts);

            Phenotype whole = Developer.Develop(
                creature.Genome, Config.Development, null, Config.Shapes, counts, paths);

            List<int[]> lost = creature.LostPartPaths;
            if (lost == null || lost.Count == 0) return whole;

            var drop = new bool[whole.PartCount];
            bool any = false;

            for (int i = 0; i < whole.PartCount; i++)
            {
                for (int l = 0; l < lost.Count; l++)
                {
                    if (!SamePath(paths[i], lost[l])) continue;
                    drop[i] = true;
                    any = true;
                    break;
                }
            }

            if (!any) return whole;

            Phenotype cut = whole.WithoutSubtrees(drop, out int[] kept);

            var survived = new List<int[]>(kept.Length);
            for (int i = 0; i < kept.Length; i++) survived.Add(paths[kept[i]]);
            paths = survived;

            return cut;
        }

        private static bool SamePath(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// A candidate adult scaled to the fraction of itself this body has already built — the
        /// adult itself at 1, which is what lets the caller skip a second tissue measurement.
        /// </summary>
        /// <remarks>
        /// The cube root because the fraction is a volume and the parts are scaled by a length,
        /// which is <see cref="Grow"/>'s arithmetic and has to stay it: a module bought at one
        /// rule and grown at another would make the body fraction stop being a readout of the
        /// energy ledger.
        /// </remarks>
        private Phenotype AtTheSameFraction(Phenotype adult, float fraction) =>
            fraction >= 1f
                ? adult
                : adult.Scaled((float)Math.Pow(fraction, 1d / 3d), Config.Shapes);

        /// <summary>This creature's counts, allocated at the genome's own minimum if it has none.</summary>
        private static int[] CountsOf(Organism creature)
        {
            IReadOnlyList<MorphNode> nodes = creature.Genome.Nodes;
            int[] counts = creature.ModuleCounts;

            if (counts != null && counts.Length == nodes.Count) return counts;

            var fresh = new int[nodes.Count];
            for (int n = 0; n < nodes.Count; n++)
            {
                fresh[n] = nodes[n].RecursiveLimit;

                // A creature restored or inherited with a shorter array keeps what it had for
                // the nodes the array covered. Nothing produces one today — a genome's node count
                // is fixed for a creature's life — and the alternative is an index that reads
                // past the end of a stored array on the day something does.
                if (counts != null && n < counts.Length && counts[n] > fresh[n]) fresh[n] = counts[n];
            }

            return fresh;
        }

        /// <summary>
        /// A node's ceiling: <see cref="MorphNode.MaxModules"/>, never below its own
        /// <see cref="MorphNode.RecursiveLimit"/> — <see cref="Developer.CountFor"/>'s rule, and
        /// the same expression, because a rule that could add a module development would not
        /// build is a charge for nothing.
        /// </summary>
        private static int CeilingOf(MorphNode node) =>
            node.MaxModules > node.RecursiveLimit ? node.MaxModules : node.RecursiveLimit;

        /// <summary>How many nodes of a genome are indeterminate — the lineage row's <c>ind</c>.</summary>
        internal static int IndeterminateNodesOf(Genome genome)
        {
            int count = 0;
            IReadOnlyList<MorphNode> nodes = genome.Nodes;

            for (int n = 0; n < nodes.Count; n++)
            {
                if (nodes[n].Growth == ModuleGrowth.Indeterminate) count++;
            }

            return count;
        }
    }
}
