using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The mouth: damage, healing, intake and the kill — D106 items 1, 3, 4 and 5, and
    /// <c>logbook/specs/mouth-spec.md</c> rules 3 to 7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where it runs.</b> One pass at the end of <see cref="Step"/>'s metabolic half, after
    /// <c>Metabolise</c> and before <c>Grow</c>: after, because healing is paid out of the reserve
    /// this step left a body and a body that could not pay its upkeep is already dead; before,
    /// because a body that has just lost a limb should invest in the body it now has rather than
    /// in the one it had a moment ago.
    /// </para>
    /// <para>
    /// <b>The order within it is the spec's, read backwards from the kill.</b> Damage first, so a
    /// step's wound is a wound; then intake, so a scavenger has this step's meal before it is
    /// asked to pay for repairs; then healing; then the kills, which rule 3 puts at the end of the
    /// step in as many words. A part that a bite takes to zero therefore gets one chance to heal
    /// out of it, which is what makes <see cref="RunConfig.HealingPerSecond"/> a defence rather
    /// than a cosmetic.
    /// </para>
    /// <para>
    /// <b>Nothing here draws from an <see cref="Rng"/>.</b> Every rule is arithmetic on a body, a
    /// corpse and a contact list; the sharing of a corpse is a proportional split and not a
    /// lottery. A world in which nothing is armed, nothing has a mouth, nothing heals and nothing
    /// is wounded — which is every world in the record — steps through exactly the trajectory it
    /// would have without this pass, and the regress against <c>mt0</c> is what says so.
    /// </para>
    /// <para>
    /// <b>Both books close by construction.</b> Damage moves nothing. A kill moves tissue and a
    /// share of the reserve out of a body and into a corpse or into the water, which is the
    /// transfer <see cref="Bury"/> has always made. Intake moves charged matter out of a corpse
    /// and into a body's reserve, less a share deposited as marine snow — three accounts
    /// <see cref="StandingJoules"/> already sums. Healing is the one term that leaves: it is
    /// metabolism, so it is burnt exactly as D098's leg 2 burns everything else, out through
    /// <see cref="EnergyOut"/> and back into the spent field at 1 / ρ.
    /// </para>
    /// </remarks>
    public sealed partial class World
    {
        private IReadOnlyList<CreatureContact> _contacts;
        private readonly Dictionary<long, Organism> _byId = new Dictionary<long, Organism>();

        // The intake's scratch, filled before it is read on every step that has a mouth: the mouths in
        // living order, what each would take, and each corpse's summed demand.
        private readonly List<Organism> _mouths = new List<Organism>();
        private readonly List<double> _mouthRates = new List<double>();
        private double[] _intakeDemand = new double[0];

        /// <summary>
        /// Who last hurt which part, within one <see cref="ApplyMouth"/> pass — the kill row's
        /// <c>by</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One pass's worth and no more.</b> The contact list is this step's, the wound it
        /// causes is this step's, and a part taken to zero is collected at the end of the same
        /// pass, so nothing here has to survive a step — which is why it is a scratch map and not
        /// state on the body: nothing to checkpoint, nothing to restore, nothing a resumed run
        /// could disagree with. It is cleared at the top of every pass so that a kill can never be
        /// attributed to a step that is over.
        /// </para>
        /// <para>
        /// <b>Keyed on the part index, which a rebuild invalidates.</b> Taking a part off remaps
        /// the health array and the phenotype together, so the body's entry is dropped the moment
        /// that happens and a second loss in the same step is recorded with no attacker rather
        /// than with the wrong one. Read and written only here; no order of iteration is ever
        /// taken from it, so it decides nothing about the trajectory.
        /// </para>
        /// <para>
        /// <b>The attacker's part rides with its id</b> (the kill row's <c>byPart</c>, round 46's
        /// K9b): the index of the attacker's part in this step's contact record, in the attacker's
        /// body as it stood at the blow, so the two are written and dropped together.
        /// </para>
        /// </remarks>
        private readonly Dictionary<long, (long By, int Part)[]> _lastAttacker =
            new Dictionary<long, (long By, int Part)[]>();

        /// <summary>Parts ever taken off a living body by a bite — rule 4's counter. Cumulative.</summary>
        public long PartsKilled { get; private set; }

        /// <summary>Bodies whose root was bitten off, ever — <see cref="DeathCause.Eaten"/>. Cumulative.</summary>
        /// <remarks>
        /// <b>Not the same as <see cref="PartsKilled"/> on a one-part body, and the difference is
        /// the point.</b> A one-part body's only part is its root, so its death is counted here and
        /// not there: what <see cref="PartsKilled"/> measures is grazing a body survives, and what
        /// this measures is predation it does not.
        /// </remarks>
        public long BodiesEaten { get; private set; }

        /// <summary>Corpses founded by a kill rather than by a death, ever. Cumulative.</summary>
        public long CorpsesFromKills { get; private set; }

        /// <summary>Charged units ever taken from a corpse by a mouth — rule 6. Cumulative.</summary>
        public double UnitsEaten { get; private set; }

        /// <summary>
        /// Joules mouths have kept (after <see cref="RunConfig.IntakeWasteFraction"/>) from corpses their own
        /// bite made, ever. Cumulative. The instrument for how much of a grazer's intake is its own kills; it
        /// changes no number of the world and is not a tunable.
        /// </summary>
        public double IntakeOwnKillJoules { get; private set; }

        /// <summary>Joules mouths have kept from corpses another body's bite made, ever. Cumulative.</summary>
        public double IntakeOtherKillJoules { get; private set; }

        /// <summary>Joules mouths have kept from corpses no bite made (age, starvation, shed parts), ever. Cumulative.</summary>
        public double IntakeCarrionJoules { get; private set; }

        /// <summary><see cref="IntakeOwnKillJoules"/> over eaters that carry attack as well as intake. Cumulative.</summary>
        public double MouthedIntakeOwnKillJoules { get; private set; }

        /// <summary><see cref="IntakeOtherKillJoules"/> over eaters that carry attack as well as intake. Cumulative.</summary>
        public double MouthedIntakeOtherKillJoules { get; private set; }

        /// <summary><see cref="IntakeCarrionJoules"/> over eaters that carry attack as well as intake. Cumulative.</summary>
        public double MouthedIntakeCarrionJoules { get; private set; }

        /// <summary>
        /// The simulated second the intake's origin books begin at: 0 for a world founded on this build, and
        /// the checkpoint's own second for a world restored from layout 19 or older, which carries no corpse
        /// killer and no origin totals. From that second on the books are exact; before it they hold nothing,
        /// and a corpse already standing then reads as made by no bite (carrion) whatever made it. A read
        /// that sums the six totals over a run restored from an older layout is therefore a sum over the part
        /// of the world after this second. Written into layout 20, so it survives further resumes.
        /// </summary>
        public double IntakeOriginBooksFromSeconds { get; private set; }

        /// <summary>Corpses a mouth has emptied to nothing, ever. Cumulative.</summary>
        public long CorpsesEaten { get; private set; }

        /// <summary>Joules ever burnt on repairs — rule 3's price. Cumulative.</summary>
        public double HealingJoules { get; private set; }

        /// <summary>The share of the living carrying attack above zero on any part, 0 to 1.</summary>
        /// <remarks>
        /// Read off <see cref="Organism.HasAttack"/>, which is cached at birth and refreshed on a
        /// plan change, so this is a walk over the living and not over their parts —
        /// <see cref="ModulesStanding"/>'s rule about an instrument for a report row.
        /// </remarks>
        public float AttackShare => ShareOfLiving(c => c.HasAttack);

        /// <summary>The share of the living carrying intake above zero on any part, 0 to 1.</summary>
        public float IntakeShare => ShareOfLiving(c => c.HasIntake);

        /// <summary>The share of the living carrying protection above zero on any part, 0 to 1.</summary>
        public float ProtectionShare => ShareOfLiving(c => c.HasProtection);

        private float ShareOfLiving(Func<Organism, bool> test)
        {
            if (_living.Count == 0) return 0f;

            int carrying = 0;
            for (int i = 0; i < _living.Count; i++) if (test(_living[i])) carrying++;

            return (float)carrying / _living.Count;
        }

        /// <summary>
        /// Whether any node of a genome carries one of D106's attributes above zero — the lineage
        /// row's <c>atk</c>, <c>ink</c> and <c>prt</c>.
        /// </summary>
        /// <remarks>
        /// Of the genome and not of the developed body, which is what
        /// <see cref="LineageEvent.HasAttack"/>'s remark argues for: a node whose part was pruned
        /// still carries the gene, and the generations in which a lineage is carrying a trait
        /// without expressing it are exactly the ones a birth row is read for.
        /// </remarks>
        internal static bool CarriesAttribute(Genome genome, Func<MorphNode, float> of)
        {
            IReadOnlyList<MorphNode> nodes = genome.Nodes;
            for (int n = 0; n < nodes.Count; n++) if (of(nodes[n]) > 0f) return true;
            return false;
        }

        /// <summary>
        /// Hands the mouth the step's contacts — the farm's list, valid for this step only.
        /// </summary>
        /// <remarks>
        /// <b>Held by reference and cleared when it is read</b>, so a list that is not renewed
        /// cannot be applied twice. A Core-only world never calls this and its bodies never touch,
        /// which is what lets the tests drive a fight by hand: they build the list themselves and
        /// step the world.
        /// </remarks>
        public void SetContacts(IReadOnlyList<CreatureContact> contacts) => _contacts = contacts;

        /// <summary>
        /// Runs rules 3 to 6 over the living, once. Returns how many parts were taken off.
        /// </summary>
        /// <param name="seconds">The metabolic step this pass covers.</param>
        public int ApplyMouth(float seconds)
        {
            _lastAttacker.Clear();

            // D123. The two records the senses read are this step's and no older one's, so the
            // last step's are forgotten before the damage pass writes this one's.
            ForgetWhatWasFelt();
            WorldPhaseProbe.Mark(WorldPhaseProbe.MouthForget);

            ApplyDamage(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.MouthDamage);
            ApplyIntake(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.MouthIntake);
            ApplyHealing(seconds);
            WorldPhaseProbe.Mark(WorldPhaseProbe.MouthHeal);

            _contacts = null;

            int killed = CollectTheDead();

            // D220. The remains lose the parts eaten whole as a living body does, and then every body whose window has
            // passed closes. After the mouth pass, so a window is whole until the step that closes it.
            if (Config.ContactEating)
            {
                CollectTheDeadRemains();
                CloseExpiredRemains();
            }

            return killed;
        }

        /// <summary>
        /// Zeroes every living body's contact and damage record, so that what the pass after it
        /// writes is this step's alone — D123.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The ruling of 2026-09-25 ("fix for 49").</b> <see cref="SensorChannel.Contact"/>
        /// reads whether a part touched another body's part on the last metabolic step and
        /// <see cref="SensorChannel.Damage"/> the health it lost on that step over its pool, as
        /// D106 item 5 and the mouth's specification asked. Until round 49 nothing cleared either
        /// record but a plan change, so a brain read "touched since the body last changed shape"
        /// and "hurt since then".
        /// </para>
        /// <para>
        /// <b>At the top of the pass, and not after the harness has read them.</b> The farm hands
        /// the records to a body's senses right after <see cref="Step"/> and the physics steps of
        /// the next interval read them; the next <see cref="Step"/> comes here before it writes
        /// anything. So a brain on every physics step of step n+1 reads what happened on step n,
        /// and a checkpoint written between the two carries what those steps will read.
        /// </para>
        /// <para>
        /// <b>Zeroed in place, never dropped.</b> The farm hands a body's senses these very arrays
        /// by reference (<c>Metabolise.HandBackWhatWasFelt</c>), and a restore wires them back by
        /// reference; an array replaced here would leave a sense reading the old one until the
        /// next hand-back. The only things that replace one are its first allocation, on a body's
        /// first touch or wound, and <c>AdoptPlan</c>, which carries both onto the new plan's
        /// indices because the old ones named the old plan's parts. The farm's hand-back and its
        /// rebuild of a body on a new plan both follow. A world in which nothing ever touches
        /// still allocates nothing, and this is one walk of the living with two null tests a
        /// body.
        /// </para>
        /// </remarks>
        private void ForgetWhatWasFelt()
        {
            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                bool[] touched = creature.PartContact;
                if (touched != null) Array.Clear(touched, 0, touched.Length);

                float[] lost = creature.PartDamage;
                if (lost != null) Array.Clear(lost, 0, lost.Length);
            }
        }

        // ------------------------------------------------------------------ rule 5: the damage

        /// <remarks>
        /// <para>
        /// <b>A fight is symmetric and is settled in one visit.</b> Each side's named part damages
        /// the other's, net of that part's own armour, out of the same record — so a claw against
        /// a claw hurts both, and neither ordering of the pair changes the answer.
        /// </para>
        /// <para>
        /// <b>The index of the living is built only when something is armed or something is
        /// watching.</b> A world of plants with the contact sense off does one walk of the living
        /// per metabolic step and nothing else, which is the cost of the module rule's own guard.
        /// </para>
        /// </remarks>
        private void ApplyDamage(float seconds)
        {
            IReadOnlyList<CreatureContact> contacts = _contacts;
            if (contacts == null || contacts.Count == 0) return;

            bool armed = false;
            for (int i = 0; i < _living.Count && !armed; i++) armed = _living[i].HasAttack;

            // Nothing can be hurt and nothing is listening. The list is dropped unread, which is
            // what makes a world of plants pay one walk for the whole mechanism.
            if (!armed && !Config.SenseContact && !Config.SenseDamage) return;

            _byId.Clear();
            for (int i = 0; i < _living.Count; i++) _byId[_living[i].Id] = _living[i];

            bool bitesFeed = Config.ContactEating;

            // D226: the dial on a mouth's blow, read once a pass. It is 1 unless contact eating is on.
            float biteScale = Config.BiteDamageScale;

            // D220: a contact's ids are looked up in the living and the remains together. A remains part is wounded
            // and bitten exactly as a living part is.
            if (bitesFeed) for (int i = 0; i < _remains.Count; i++) _byId[_remains[i].Id] = _remains[i];

            for (int i = 0; i < contacts.Count; i++)
            {
                CreatureContact touch = contacts[i];

                if (!_byId.TryGetValue(touch.BodyA, out Organism a)) continue;
                if (!_byId.TryGetValue(touch.BodyB, out Organism b)) continue;

                IReadOnlyList<PhenotypePart> partsA = a.Phenotype.Parts;
                IReadOnlyList<PhenotypePart> partsB = b.Phenotype.Parts;

                // A list taken before a plan change names a part that is no longer there. It is
                // skipped rather than clamped: an index that fell off the end is not evidence
                // about a different part.
                if (touch.PartA < 0 || touch.PartA >= partsA.Count) continue;
                if (touch.PartB < 0 || touch.PartB >= partsB.Count) continue;

                PhenotypePart hittingA = partsA[touch.PartA];
                PhenotypePart hittingB = partsB[touch.PartB];

                // Remains feel nothing: they have no brain to read the record.
                if (!a.IsRemains) NoteContact(a, touch.PartA);
                if (!b.IsRemains) NoteContact(b, touch.PartB);

                // D226: with the dial at 1 the damage handed to Wound is DamageOf's own expression, as it always was.
                float blowOnB = DamageOf(hittingA, hittingB, seconds);
                if (biteScale != 1f && hittingA.Intake > 0f && !a.IsRemains) blowOnB *= biteScale;

                // D227: a mouth's blow is counted from the health before and after; the health is only read.
                bool mouthOnB = blowOnB > 0f && hittingA.Intake > 0f && !a.IsRemains;
                float healthB0 = mouthOnB ? HealthOf(b, touch.PartB) : 0f;

                float tookFromB = Wound(
                    b, touch.PartB, hittingB, blowOnB,
                    a.Id, touch.PartA);

                if (mouthOnB) CountMouthBlow(healthB0, b, touch.PartB);

                // D219: the wound takes its share of the bitten part's tissue and offers it to the biting part.
                if (bitesFeed) OfferBite(a, touch.PartA, hittingA, b, touch.PartB, tookFromB, seconds);

                float blowOnA = DamageOf(hittingB, hittingA, seconds);
                if (biteScale != 1f && hittingB.Intake > 0f && !b.IsRemains) blowOnA *= biteScale;

                bool mouthOnA = blowOnA > 0f && hittingB.Intake > 0f && !b.IsRemains;
                float healthA0 = mouthOnA ? HealthOf(a, touch.PartA) : 0f;

                float tookFromA = Wound(
                    a, touch.PartA, hittingA, blowOnA,
                    b.Id, touch.PartB);

                if (mouthOnA) CountMouthBlow(healthA0, a, touch.PartA);

                if (bitesFeed) OfferBite(b, touch.PartB, hittingB, a, touch.PartA, tookFromA, seconds);
            }

            // D219: every offer of the pass, settled in the order the wounds were made.
            if (bitesFeed) SettleBites();
        }

        /// <summary>A part's health as <see cref="Wound"/> will find it: a body with no health array is whole.</summary>
        private static float HealthOf(Organism creature, int partIndex)
        {
            float[] health = creature.PartHealth;
            return health == null ? 1f : health[partIndex];
        }

        /// <summary>
        /// D227: counts one mouth's blow that did damage, and counts it again as finishing when the part had health
        /// before it and has none now. Reads only; the world's arithmetic is not touched.
        /// </summary>
        private void CountMouthBlow(float healthBefore, Organism bitten, int partIndex)
        {
            MouthBlows++;
            if (healthBefore > 0f && !(HealthOf(bitten, partIndex) > 0f)) MouthBlowsFinishing++;
        }

        /// <summary>
        /// What one part does to another over a step, in health — rule 5's arithmetic.
        /// </summary>
        /// <remarks>
        /// Attack and protection are both rates over their own part's area, so a big claw hits
        /// harder and a big flank absorbs more, and the difference is what reaches the health
        /// pool. Floored at zero rather than allowed to heal the defender: armour stops a blow, it
        /// does not turn one into a meal.
        /// </remarks>
        private static float DamageOf(PhenotypePart attacker, PhenotypePart defender, float seconds)
        {
            if (!(attacker.Attack > 0f)) return 0f;

            float blow = attacker.Attack * Math.Max(0f, attacker.SurfaceArea);
            float armour = defender.Protection * Math.Max(0f, defender.SurfaceArea);
            float net = blow - armour;

            return net > 0f ? net * seconds : 0f;
        }

        /// <summary>
        /// Lowers one part's health by a blow. Returns the share of the part's health it took — D219 reads
        /// it as the share of the part's tissue that was bitten off; every other caller ignores it.
        /// </summary>
        private float Wound(
            Organism creature, int partIndex, PhenotypePart part, float damage, long attackerId,
            int attackerPart)
        {
            if (!(damage > 0f)) return 0f;

            NoteAttacker(creature, partIndex, attackerId, attackerPart);

            float pool = Metabolism.HealthPool(part, Config);

            float[] health = creature.PartHealth;
            if (health == null)
            {
                health = new float[creature.Phenotype.PartCount];
                for (int i = 0; i < health.Length; i++) health[i] = 1f;
                creature.PartHealth = health;
            }

            // D220: remains have no brain to sense the damage, so nothing is recorded for one.
            float[] lost = null;
            if (!creature.IsRemains)
            {
                lost = creature.PartDamage;
                if (lost == null)
                {
                    lost = new float[creature.Phenotype.PartCount];
                    creature.PartDamage = lost;
                }
            }

            // A pool of zero is a part with no volume or no toughness to speak of, and any blow at
            // all finishes it. Written as a branch rather than left to a division, which would
            // hand the sense an infinity.
            float share = pool > 0f ? damage / pool : 1f;

            float was = health[partIndex];
            float now = was - share;
            health[partIndex] = now > 0f ? now : 0f;

            // Summed over this step's blows only: ForgetWhatWasFelt zeroed the array at the top of
            // the pass (D123). Health is a share of the pool, so the loss already is one, and the
            // step's sum cannot pass 1 because healing comes after this pass.
            if (lost != null) lost[partIndex] += was - health[partIndex];

            return was - health[partIndex];
        }

        /// <summary>Remembers whose blow this was, for as long as this pass lasts.</summary>
        private void NoteAttacker(
            Organism creature, int partIndex, long attackerId, int attackerPart)
        {
            if (!_lastAttacker.TryGetValue(creature.Id, out (long By, int Part)[] by) ||
                by.Length != creature.Phenotype.PartCount)
            {
                by = new (long By, int Part)[creature.Phenotype.PartCount];
                for (int i = 0; i < by.Length; i++) by[i] = (-1L, -1);
                _lastAttacker[creature.Id] = by;
            }

            if (partIndex >= 0 && partIndex < by.Length) by[partIndex] = (attackerId, attackerPart);
        }

        /// <summary>
        /// Who took this part to zero and with which of its parts, or (-1, -1) when this pass
        /// cannot say.
        /// </summary>
        private (long By, int Part) AttackerOf(Organism creature, int partIndex)
        {
            if (!_lastAttacker.TryGetValue(creature.Id, out (long By, int Part)[] by))
                return (-1L, -1);
            if (partIndex < 0 || partIndex >= by.Length) return (-1L, -1);

            return by[partIndex];
        }

        /// <summary>
        /// Flags a part as touched on this step — the <see cref="SensorChannel.Contact"/> record,
        /// cleared at the top of every pass by <see cref="ForgetWhatWasFelt"/> (D123).
        /// </summary>
        private void NoteContact(Organism creature, int partIndex)
        {
            if (!Config.SenseContact) return;

            bool[] touching = creature.PartContact;
            if (touching == null || touching.Length != creature.Phenotype.PartCount)
            {
                touching = new bool[creature.Phenotype.PartCount];
                creature.PartContact = touching;
            }

            touching[partIndex] = true;
        }

        // ------------------------------------------------------------------ rule 6: the intake

        /// <remarks>
        /// <para>
        /// <b>Two passes, so a corpse is shared and not raced for.</b> What every mouth in reach
        /// would take is summed first and each is then granted its share of what is actually
        /// there — the rule the nutrient field has used since the Astra review, applied to a
        /// particle instead of a cell. A first-come walk would make the answer depend on the order
        /// of <c>_living</c>, which is a property of the population's history.
        /// </para>
        /// <para>
        /// <b>Only a body with a mouth costs anything.</b> The outer walk skips on
        /// <see cref="Organism.HasIntake"/>, which is cached, so a world with no mouth in it pays
        /// one walk of the living and touches no corpse.
        /// </para>
        /// </remarks>
        private void ApplyIntake(float seconds)
        {
            // D219: a mouth eats only what it touches, so the corpse meal never runs.
            if (Config.ContactEating) return;

            if (!(Config.IntakeReachMetres > 0f) || _corpses.Count == 0) return;

            bool anyMouth = false;
            for (int i = 0; i < _living.Count && !anyMouth; i++) anyMouth = _living[i].HasIntake;
            if (!anyMouth) return;

            double perUnit = Config.JoulesPerUnit;
            float tissueDensity = Config.CellTypes.Resolve(CellTypeIds.Structural)
                .TissueEnergyPerCubicMetre;

            // A meal takes joules out of a corpse, so D129's column sums are stale after it.
            CorpsesChanged();

            if (_intakeDemand.Length < _corpses.Count) _intakeDemand = new double[Math.Max(_corpses.Count, 2 * _intakeDemand.Length)];
            double[] demand = _intakeDemand;
            WorldPhaseProbe.Count(WorldPhaseProbe.CorpsesAtIntake, _corpses.Count);

            // The mouths in living order, and what each would take: a body's rate reads its own
            // phenotype, which nothing changes in this pass.
            _mouths.Clear();
            _mouthRates.Clear();
            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (!creature.HasIntake) continue;
                _mouths.Add(creature);
                _mouthRates.Add(MouthRate(creature, seconds));
            }

            // D214. With the tunable on, a body handed no points this step is measured from its centre
            // and counted, and the columns' margin is the widest a mouth point lies from its body's
            // centre across x and z. Off, nothing here runs and `margin` stays 0.
            double margin = Config.IntakeReachFromMouth ? MouthMargin() : 0d;

            WorldPhaseProbe.Count(WorldPhaseProbe.Mouths, _mouths.Count);

            // Each corpse's reach, once, and the largest: the columns are sized from it. A corpse's
            // reach depends on its joules, which no mouth changes before the second pass.
            int mouths = _mouths.Count;
            int corpses = _corpses.Count;
            if (_intakeReach.Length < corpses) _intakeReach = new double[Math.Max(corpses, 2 * _intakeReach.Length)];
            double[] reachOf = _intakeReach;
            double largest = 0d;
            for (int c = 0; c < corpses; c++)
            {
                double r = CorpseReach(_corpses[c], tissueDensity);
                reachOf[c] = r;
                if (r > largest) largest = r;
            }

            // A mouth point lies up to `margin` from its body's centre, which the columns key on, so the
            // columns are widened by it: a point within a corpse's reach puts its centre within reach plus
            // margin. Off the margin is 0 and the argument is the largest reach itself, unchanged.
            bool columns = IntakeByColumns && SetIntakeColumns(margin > 0d ? largest + margin * (1d + 1e-6) : largest);
            if (columns)
            {
                FillColumns(corpses, i => _corpses[i].Position.X, i => _corpses[i].Position.Z,
                    ref _corpseColumnKeys, ref _corpseColumnItems, out _corpseColumnCount);
                FillColumns(mouths, i => _mouths[i].X, i => _mouths[i].Z,
                    ref _mouthColumnKeys, ref _mouthColumnItems, out _mouthColumnCount);
            }

            // Each corpse's demand, summed over the mouths in reach in living order from zero:
            // the same additions in the same order as a walk of the mouths, so the same bits. A
            // corpse writes its own entry alone, so the corpses are summed on any thread. With the
            // columns, the mouths a corpse sums are those in the columns beside its own, sorted
            // into living order: every mouth in reach is among them, and a mouth out of reach adds
            // nothing in either walk.
            Parallelism.ForRanges(corpses, (from, to) =>
            {
                List<int> near = columns ? new List<int>() : null;
                int[] xs = columns ? new int[3] : null, zs = columns ? new int[3] : null;
                for (int c = from; c < to; c++)
                {
                    Corpse corpse = _corpses[c];
                    double reach = reachOf[c];
                    double sum = 0d;
                    if (columns)
                    {
                        near.Clear();
                        Gather(corpse.Position.X, corpse.Position.Z, _mouthColumnKeys, _mouthColumnItems,
                            _mouthColumnCount, near, xs, zs);
                        near.Sort();
                        for (int k = 0; k < near.Count; k++)
                        {
                            int m = near[k];
                            if (Within(_mouths[m], corpse, reach)) sum += _mouthRates[m];
                        }
                    }
                    else
                    {
                        for (int m = 0; m < mouths; m++)
                        {
                            if (Within(_mouths[m], corpse, reach)) sum += _mouthRates[m];
                        }
                    }

                    demand[c] = sum;
                }
            });

            // The mouths in living order, each walking the corpses in list order: with the columns,
            // the corpses in the columns beside its own, sorted into list order. A meal only lowers
            // a corpse's joules, so its reach within this pass never passes the one the columns
            // were sized from, and every corpse a mouth can take from is among them.
            List<int> nearCorpses = columns ? new List<int>() : null;
            int[] cxs = columns ? new int[3] : null, czs = columns ? new int[3] : null;
            for (int m = 0; m < mouths; m++)
            {
                Organism creature = _mouths[m];
                if (columns)
                {
                    nearCorpses.Clear();
                    Gather(creature.X, creature.Z, _corpseColumnKeys, _corpseColumnItems, _corpseColumnCount,
                        nearCorpses, cxs, czs);
                    nearCorpses.Sort();
                    for (int k = 0; k < nearCorpses.Count; k++)
                        Take(creature, nearCorpses[k], demand, seconds, perUnit, tissueDensity);
                }
                else
                {
                    for (int c = 0; c < corpses; c++) Take(creature, c, demand, seconds, perUnit, tissueDensity);
                }
            }
        }

        /// <summary>One mouth's meal of one corpse: the second pass's step, the same with or without the columns.</summary>
        private void Take(Organism creature, int c, double[] demand, float seconds, double perUnit, float tissueDensity)
        {
            Corpse corpse = _corpses[c];
            if (!(corpse.Joules > 0d)) return;
            if (!(demand[c] > 0d)) return;
            if (!InReach(creature, corpse, tissueDensity)) return;

            double wanted = MouthRate(creature, seconds);
            double available = corpse.Joules / perUnit;

            // The share this mouth is entitled to of what the corpse actually holds. At a
            // corpse richer than the crowd's appetite every mouth takes what it asked for,
            // which is what the min is: the share is a rationing and never a bonus.
            double units = demand[c] > available
                ? wanted * (available / demand[c])
                : wanted;

            if (!(units > 0d)) return;

            double joules = units * perUnit;
            if (joules > corpse.Joules) joules = corpse.Joules;

            corpse.Joules -= joules;

            // Rule 6's waste, and it is D098's transfer loss in the shape D098 leaves for
            // it: what the mouth tore up and did not keep is charged matter still, so it
            // goes back into the water where the corpse is rather than out of the world.
            // Quantised once at the field's door and booked as the same number, which is
            // Bury's rule and StepCorpses' rule.
            float waste = (float)(joules * Config.IntakeWasteFraction);
            if (waste > 0f)
            {
                Nutrients.Deposit(corpse.Point, waste);
                DetritusDepositedTotal += waste;
            }

            // What the mouth keeps, once: the very value both lines below added before this instrument, so
            // nothing in the world's arithmetic moves. The origin books sum it where it is handed over.
            double kept = joules - waste;

            creature.Energy += kept;
            creature.MouthJoules += kept;
            UnitsEaten += joules / perUnit;

            NoteOrigin(creature, corpse, kept);

            if (corpse.Joules <= 0d) CorpsesEaten++;
        }

        /// <summary>
        /// Books the joules a mouth kept by whose bite made the corpse it took them from: its own, another's,
        /// or none (carrion). A sum of the amount <see cref="Take"/> hands over, taken where it hands it over;
        /// no rule of the world reads any of the six totals or the body's three.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Classes.</b> The corpse's <see cref="Corpse.KilledBy"/> is the eater's own id: own kill. Another
        /// body's id: other kill. -1, which is every corpse a bite did not make: carrion. The three add to
        /// <see cref="Organism.MouthJoules"/> for a body and to the kept intake for the world, to within the
        /// rounding of adding the same amounts in a different order.
        /// </para>
        /// <para>
        /// <b>The mouthed totals</b> are the same sums over eaters that carry attack as well as intake
        /// (<see cref="Organism.HasAttack"/> and <see cref="Organism.HasIntake"/>).
        /// </para>
        /// </remarks>
        private void NoteOrigin(Organism creature, Corpse corpse, double kept)
        {
            long killer = corpse.KilledBy;
            bool mouthed = creature.HasAttack && creature.HasIntake;

            if (killer >= 0L && killer == creature.Id)
            {
                creature.MouthJoulesOwnKill += kept;
                IntakeOwnKillJoules += kept;
                if (mouthed) MouthedIntakeOwnKillJoules += kept;
            }
            else if (killer >= 0L)
            {
                creature.MouthJoulesOtherKill += kept;
                IntakeOtherKillJoules += kept;
                if (mouthed) MouthedIntakeOtherKillJoules += kept;
            }
            else
            {
                creature.MouthJoulesCarrion += kept;
                IntakeCarrionJoules += kept;
                if (mouthed) MouthedIntakeCarrionJoules += kept;
            }
        }

        // ------------------------------------------------------------------ the intake's columns

        /// <summary>
        /// Whether the intake walks only the mouths and corpses in neighbouring columns (the default) or every
        /// pair. The same bits either way; the full walk is kept for the test that says so.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Round 61's c6101x slowed from 0.92 to 0.55 times real time between 48,000 and 55,800 s
        /// (2026-10-17) because its mouths rose from 65 to 398 among about 60,000 corpses, and both passes
        /// walked every pair: the intake grew from 25 to 156 ms a step while the rest of the world step held.
        /// </para>
        /// <para>
        /// <b>Why it is exact.</b> The columns tile x and z at a width of at least the step's largest
        /// reach, with a margin for rounding, so a mouth and a corpse within reach of each other are in the
        /// same column or the next one along each axis, across a seam where the box wraps (as
        /// <see cref="Separation"/> measures). Each pass still applies its own test to every pair it
        /// visits, in the order the full walk would have, so it visits a superset of the pairs that act,
        /// in the same order, and the pairs it skips are those whose test fails. The columns are a
        /// sorted array of keys, rebuilt each step, so nothing about them is carried in a checkpoint.
        /// </para>
        /// </remarks>
        public static bool IntakeByColumns { get; set; } = true;

        private double[] _intakeReach = new double[0];
        private long[] _corpseColumnKeys = new long[0], _mouthColumnKeys = new long[0];
        private int[] _corpseColumnItems = new int[0], _mouthColumnItems = new int[0];
        private int _corpseColumnCount, _mouthColumnCount;
        private bool _columnWrapX, _columnWrapZ;
        private int _columnsX, _columnsZ;
        private double _columnWidthX, _columnWidthZ;

        /// <summary>
        /// The columns for a step whose largest reach is <paramref name="largest"/>; false when there is no
        /// finite reach to size them from, and the full walk is taken.
        /// </summary>
        private bool SetIntakeColumns(double largest)
        {
            if (!(largest > 0d) || double.IsInfinity(largest)) return false;

            double width = largest * (1d + 1e-6) + 1e-4;
            bool periodic = Config.SharedSpace && Config.WorldShape != WorldShape.Tank;
            float lx = Nutrients.LengthMetres, lz = Nutrients.WidthMetres;

            _columnWrapX = periodic && lx > 0f;
            _columnsX = _columnWrapX ? Math.Max(1, (int)Math.Floor(lx / width)) : 0;
            _columnWidthX = _columnWrapX ? (double)lx / _columnsX : width;

            _columnWrapZ = periodic && lz > 0f;
            _columnsZ = _columnWrapZ ? Math.Max(1, (int)Math.Floor(lz / width)) : 0;
            _columnWidthZ = _columnWrapZ ? (double)lz / _columnsZ : width;
            return true;
        }

        /// <summary>A coordinate's column on one axis: wrapped into the box where it wraps, and bounded where not.</summary>
        private static int Column(double v, bool wrap, int count, double width, double extent)
        {
            if (wrap)
            {
                double m = v - extent * Math.Floor(v / extent);
                int c = (int)Math.Floor(m / width);
                return c < 0 ? 0 : c >= count ? count - 1 : c;
            }

            // Bounded so the neighbours' arithmetic cannot overflow; the bound is monotone, so two
            // coordinates in neighbouring columns stay in neighbouring or the same columns.
            double f = Math.Floor(v / width);
            if (f > 1e9) f = 1e9;
            if (f < -1e9) f = -1e9;
            return (int)f;
        }

        /// <summary>A point's column key, or false for a point that is not finite and so in reach of nothing.</summary>
        private bool ColumnKey(double x, double z, out int cx, out int cz)
        {
            cx = 0;
            cz = 0;
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(z) || double.IsInfinity(z)) return false;

            cx = Column(x, _columnWrapX, _columnsX, _columnWidthX, Nutrients.LengthMetres);
            cz = Column(z, _columnWrapZ, _columnsZ, _columnWidthZ, Nutrients.WidthMetres);
            return true;
        }

        private static long Key(int cx, int cz) => ((long)cx << 32) | (uint)cz;

        /// <summary>The items' column keys, sorted, beside their indices.</summary>
        private void FillColumns(int count, Func<int, float> x, Func<int, float> z, ref long[] keys, ref int[] items,
            out int filled)
        {
            if (keys.Length < count) keys = new long[Math.Max(count, 2 * keys.Length)];
            if (items.Length < count) items = new int[Math.Max(count, 2 * items.Length)];

            filled = 0;
            for (int i = 0; i < count; i++)
            {
                if (!ColumnKey(x(i), z(i), out int cx, out int cz)) continue;
                keys[filled] = Key(cx, cz);
                items[filled] = i;
                filled++;
            }

            Array.Sort(keys, items, 0, filled);
        }

        /// <summary>One axis's neighbouring columns, each once: all of them where a wrapped axis has three or fewer.</summary>
        private static int Neighbours(int c, bool wrap, int count, int[] into)
        {
            if (wrap && count <= 3)
            {
                for (int k = 0; k < count; k++) into[k] = k;
                return count;
            }

            if (wrap)
            {
                into[0] = c == 0 ? count - 1 : c - 1;
                into[1] = c;
                into[2] = c == count - 1 ? 0 : c + 1;
                return 3;
            }

            into[0] = c - 1;
            into[1] = c;
            into[2] = c + 1;
            return 3;
        }

        /// <summary>The items in the columns beside a point's own, appended unsorted.</summary>
        private void Gather(double x, double z, long[] keys, int[] items, int filled, List<int> near, int[] xs, int[] zs)
        {
            if (filled == 0 || !ColumnKey(x, z, out int cx, out int cz)) return;

            int nx = Neighbours(cx, _columnWrapX, _columnsX, xs);
            int nz = Neighbours(cz, _columnWrapZ, _columnsZ, zs);
            for (int a = 0; a < nx; a++)
            {
                for (int b = 0; b < nz; b++)
                {
                    long key = Key(xs[a], zs[b]);
                    int at = Array.BinarySearch(keys, 0, filled, key);
                    if (at < 0) continue;
                    while (at > 0 && keys[at - 1] == key) at--;
                    for (; at < filled && keys[at] == key; at++) near.Add(items[at]);
                }
            }
        }

        /// <summary>What one body's mouths would take this step, in charged units.</summary>
        private double MouthRate(Organism creature, float seconds)
        {
            double rate = 0d;
            IReadOnlyList<PhenotypePart> parts = creature.Phenotype.Parts;

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Intake > 0f)
                {
                    rate += (double)parts[i].Intake * Math.Max(0f, parts[i].SurfaceArea);
                }
            }

            // The owner's intake scale (RunConfig.IntakeScale). At 1 nothing is multiplied.
            if (Config.IntakeScale != 1f) rate *= Config.IntakeScale;

            return rate * seconds;
        }

        /// <summary>
        /// Whether a body is close enough to a corpse to take from it — rule 6's distance test.
        /// </summary>
        /// <remarks>
        /// <b>Surface to centre, which is what "in reach of a corpse's surface" can mean here.</b>
        /// The corpse's own radius comes from the volume its joules are worth at the tissue price,
        /// so a big corpse is reachable from further out; the body's side of it is its centre,
        /// because that is the only world position Core holds for a body
        /// (<see cref="RunConfig.IntakeReachMetres"/> says why, and says that the reach has to
        /// cover a body's own half-length in consequence).
        /// </remarks>
        private bool InReach(Organism creature, Corpse corpse, float tissueDensity) =>
            Within(creature, corpse, CorpseReach(corpse, tissueDensity));

        /// <summary>How far from a corpse's centre a body's centre may be and still take from it.</summary>
        private double CorpseReach(Corpse corpse, float tissueDensity)
        {
            double volume = tissueDensity > 0f ? corpse.Joules / tissueDensity : 0d;
            double radius = volume > 0d ? Math.Pow(3d * volume / (4d * Math.PI), 1d / 3d) : 0d;

            return radius + Config.IntakeReachMetres;
        }

        /// <summary>
        /// Mouth points the world has handed a body this step that were missing, per body per intake pass,
        /// with <see cref="RunConfig.IntakeReachFromMouth"/> on: the body was measured from its centre.
        /// Cumulative, a diagnostic that is not checkpointed. Stays 0 with the tunable off.
        /// </summary>
        public long MouthPointFallbacks { get; private set; }

        /// <summary>
        /// Hands the world the posed mouth points of one body's eating parts, in Core's frame, for this
        /// step's intake — D214. The harness poses them from the solver's links, with
        /// <see cref="MouthPoint.Local"/> and <see cref="MouthPoint.Posed"/>; Core poses nothing.
        /// </summary>
        /// <param name="creature">The body.</param>
        /// <param name="points">3 floats per point: x, y, z. Only the first <c>3 * count</c> are read.</param>
        /// <param name="count">How many points. 0 is none, and the body is measured from its centre.</param>
        /// <remarks>
        /// A no-op with <see cref="RunConfig.IntakeReachFromMouth"/> off, so nothing is allocated or kept.
        /// A non-finite point is refused as <see cref="Observe(Organism, Float3, float)"/> refuses a
        /// non-finite centre: the solver has diverged.
        /// </remarks>
        public void ObserveMouths(Organism creature, float[] points, int count)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (!Config.IntakeReachFromMouth) return;

            if (count < 0 || (count > 0 && (points == null || points.Length < 3 * count)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count), count, "The points array must hold 3 floats for each of the " + count + " points.");
            }

            for (int i = 0; i < 3 * count; i++)
            {
                if (float.IsNaN(points[i]) || float.IsInfinity(points[i]))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(points), points[i],
                        $"Creature {creature.Id} has a non-finite mouth point, so the solver has already diverged.");
                }
            }

            if (count == 0)
            {
                creature.MouthPointCount = 0;
                return;
            }

            float[] own = creature.MouthPoints;
            if (own == null || own.Length < 3 * count)
            {
                own = new float[3 * count];
                creature.MouthPoints = own;
            }

            Array.Copy(points, own, 3 * count);
            creature.MouthPointCount = count;
        }

        /// <summary>
        /// With the tunable on: counts the mouths handed no points and returns the largest distance across
        /// x and z (the seam's shorter arc) from a mouth's centre to one of its points.
        /// </summary>
        private double MouthMargin()
        {
            double widest = 0d;

            for (int m = 0; m < _mouths.Count; m++)
            {
                Organism creature = _mouths[m];
                int count = creature.MouthPointCount;
                if (count <= 0 || creature.MouthPoints == null)
                {
                    MouthPointFallbacks++;
                    continue;
                }

                float[] p = creature.MouthPoints;
                for (int k = 0; k < count; k++)
                {
                    double dx = Separation(creature.X, p[3 * k], Nutrients.LengthMetres);
                    double dz = Separation(creature.Z, p[3 * k + 2], Nutrients.WidthMetres);
                    double d = Math.Sqrt(dx * dx + dz * dz);
                    if (d > widest) widest = d;
                }
            }

            return widest;
        }

        private bool Within(Organism creature, Corpse corpse, double reach)
        {
            // D214. Each eating part from its own point; a body with none is measured from its centre.
            if (Config.IntakeReachFromMouth && creature.MouthPointCount > 0 && creature.MouthPoints != null)
            {
                return WithinOfAMouth(creature, corpse, reach);
            }

            Float3 at = corpse.Position;
            double dy = (double)creature.HeightY - at.Y;
            double dx = Separation(creature.X, at.X, Nutrients.LengthMetres);
            double dz = Separation(creature.Z, at.Z, Nutrients.WidthMetres);

            return dx * dx + dy * dy + dz * dz <= reach * reach;
        }

        /// <summary><see cref="Within"/> for a body with mouth points: any one of them in reach is enough.</summary>
        private bool WithinOfAMouth(Organism creature, Corpse corpse, double reach)
        {
            Float3 at = corpse.Position;
            float[] p = creature.MouthPoints;
            int count = creature.MouthPointCount;

            for (int k = 0; k < count; k++)
            {
                double dy = (double)p[3 * k + 1] - at.Y;
                double dx = Separation(p[3 * k], at.X, Nutrients.LengthMetres);
                double dz = Separation(p[3 * k + 2], at.Z, Nutrients.WidthMetres);

                if (dx * dx + dy * dy + dz * dz <= reach * reach) return true;
            }

            return false;
        }

        /// <summary>
        /// How far apart two coordinates are on one axis: across the shorter arc in a periodic
        /// box, plainly in a tank or a tiled world.
        /// </summary>
        /// <remarks>
        /// D077's box wraps, and a mouth at one seam and a corpse at the other are neighbours —
        /// the current carries a body across a seam about once per hundred seconds (CLAUDE.md's
        /// <c>wraps</c> note), so the plain difference would put a body's own meal a box-width
        /// away. A tank has a wall and no seam, and <c>WrapAxis</c> is not applied there for the
        /// same reason <c>StepCorpses</c> does not apply it there.
        /// </remarks>
        private double Separation(float a, float b, float extent)
        {
            double d = Math.Abs((double)a - b);

            if (!Config.SharedSpace || Config.WorldShape == WorldShape.Tank || !(extent > 0f))
            {
                return d;
            }

            return d > 0.5d * extent ? extent - d : d;
        }

        // ------------------------------------------------------------------ rule 3: the healing

        /// <remarks>
        /// <para>
        /// <b>Paid in full or in proportion, never on credit.</b> A body that cannot afford every
        /// part's repair buys the same fraction of each, which keeps the rule independent of the
        /// order the parts happen to be in; a body with nothing left buys none, which is rule 3's
        /// "not at all when the reserve is empty".
        /// </para>
        /// <para>
        /// <b>Burnt, not moved</b>, and that is what keeps the audit closed: the joules leave the
        /// world as heat through <see cref="EnergyOut"/> and the same over ρ arrives in the spent
        /// field where the body is, which is the transfer every other expenditure in D098 makes.
        /// </para>
        /// </remarks>
        private void ApplyHealing(float seconds)
        {
            if (!(Config.HealingPerSecond > 0f)) return;

            double step = (double)Config.HealingPerSecond * seconds;
            if (!(step > 0d)) return;

            // D219: healing also re-buys the tissue the restored health stands for.
            bool rebuys = Config.ContactEating;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                float[] health = creature.PartHealth;
                if (health == null) continue;
                if (!(creature.Energy > 0d)) continue;

                IReadOnlyList<PhenotypePart> parts = creature.Phenotype.Parts;
                int n = health.Length < parts.Count ? health.Length : parts.Count;

                double wanted = 0d;
                double wantedTissue = 0d;
                int freeLink = rebuys && Config.FreeFirstLinkCubicMetres > 0f
                    ? Metabolism.FreeLinkIndex(creature.Phenotype)
                    : -1;

                for (int p = 0; p < n; p++)
                {
                    if (health[p] >= 1f || health[p] <= 0f) continue;

                    double share = 1d - health[p];
                    if (share > step) share = step;

                    wanted += share * Metabolism.HealthPool(parts[p], Config) *
                              Config.HealingJoulesPerHealth;

                    if (rebuys) wantedTissue += share * Metabolism.PartTissueJoules(creature.Phenotype, parts[p], Config, freeLink);
                }

                // The health's price and the tissue's together: a body that cannot pay both heals in proportion
                // to what it can pay. With the dial off wantedTissue is 0 and this is `wanted` to the bit.
                double wantedAll = wanted + wantedTissue;
                if (!(wantedAll > 0d)) continue;

                double afford = wantedAll <= creature.Energy ? 1d : creature.Energy / wantedAll;
                if (!(afford > 0d)) continue;

                double rebought = 0d;

                for (int p = 0; p < n; p++)
                {
                    if (health[p] >= 1f || health[p] <= 0f) continue;

                    double share = 1d - health[p];
                    if (share > step) share = step;

                    float before = health[p];
                    float healed = (float)(health[p] + share * afford);
                    health[p] = healed > 1f ? 1f : healed;

                    if (rebuys)
                    {
                        rebought += (double)(health[p] - before) *
                                    Metabolism.PartTissueJoules(creature.Phenotype, parts[p], Config, freeLink);
                    }
                }

                double spent = wanted * afford;
                if (spent > creature.Energy) spent = creature.Energy;

                creature.Energy -= spent;
                EnergyOut += spent;
                HealingJoules += spent;

                if (rebuys)
                {
                    // Charged matter staying charged: from the reserve into the tissue account, nothing burnt.
                    if (rebought > creature.Energy) rebought = creature.Energy > 0d ? creature.Energy : 0d;
                    creature.Energy -= rebought;
                    creature.TissueJoules += rebought;
                    HealTissueJoules += rebought;
                }

                // The one float in the transfer, and it is the field's API rather than an account
                // — World.Metabolise's burn says so at the same line, for the same reason.
                // D206: a plant keeps the units of its healing, as of every burn. Off, the
                // float deposit below is the recorded one.
                if (Config.PlantsKeepMatter && creature.HasPhotosyntheticTissue)
                {
                    ReturnSpent(creature, spent / Config.JoulesPerUnit);
                }
                else
                {
                    float returned = (float)(spent / Config.JoulesPerUnit);
                    Matter.Deposit(creature.Point, returned);
                    BurntTotal += returned;
                }
            }
        }

        // ------------------------------------------------------------------ rule 4: the kill

        /// <summary>
        /// Takes off every part that reached zero health, and buries every body that lost its
        /// root or fell under the mass floor. Returns the parts taken off.
        /// </summary>
        /// <remarks>
        /// <b>Backwards over the living, because a kill can bury a body.</b>
        /// <see cref="Bury"/> removes by index, so a forward walk would skip the creature that
        /// moved down into the hole. One body may lose several parts in a step — a crowd can hit
        /// it from several sides — so each body is re-scanned until nothing on it is at zero, and
        /// the scan is bounded by the part count because every pass removes at least one part.
        /// </remarks>
        private int CollectTheDead()
        {
            int killed = 0;

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism creature = _living[i];
                if (creature.PartHealth == null) continue;

                for (int guard = 0; guard <= DevelopmentLimits.Default.MaxParts; guard++)
                {
                    int dead = FirstDeadPart(creature);
                    if (dead < 0) break;

                    // A root, or the only part, is the body and not a part of it — the counters'
                    // own remarks: PartsKilled measures grazing a body survives and BodiesEaten
                    // measures predation it does not, and a one-part body counted in both would
                    // make the first unreadable.
                    bool wasTheBody = dead == 0 || creature.Phenotype.PartCount <= 1;

                    if (!wasTheBody)
                    {
                        killed++;
                        PartsKilled++;
                    }

                    if (KillPart(creature, i, dead)) break;
                }
            }

            return killed;
        }

        /// <summary>
        /// Queues one <c>kill</c> row — the instrument round 45's J3 and J7 read, and the only
        /// record anywhere of a body that lost a limb and lived.
        /// </summary>
        /// <remarks>
        /// <b>A recording and nothing else.</b> It appends to the same queue the births and the
        /// deaths use, reads no field it does not already have in hand, draws nothing from
        /// <see cref="Rng"/> and changes no account, so a world stepped with the queue drained and
        /// one where it is never drained run the same trajectory — <see cref="LineageEvent"/>'s own
        /// argument, unchanged by there being a third kind of row in it.
        /// </remarks>
        private void NoteKill(
            Organism creature, int partIndex, (long By, int Part) attacker, bool rootLost,
            int partsLost, double tissueJoules, double reserveJoules)
        {
            _lineageEvents.Add(LineageEvent.Kill(
                ElapsedSeconds, creature.Id, attacker.By, rootLost, partsLost,
                tissueJoules, reserveJoules, creature.IndeterminateNodes,
                partIndex, attacker.By >= 0L ? attacker.Part : -1));
        }

        private static int FirstDeadPart(Organism creature)
        {
            float[] health = creature.PartHealth;
            if (health == null) return -1;

            int n = health.Length < creature.Phenotype.PartCount
                ? health.Length
                : creature.Phenotype.PartCount;

            for (int p = 0; p < n; p++) if (health[p] <= 0f) return p;

            return -1;
        }

        /// <summary>
        /// Takes one part and everything hanging from it off a living body, as a corpse — D106
        /// item 1. True when the body itself died.
        /// </summary>
        /// <param name="creature">The body losing a part.</param>
        /// <param name="index">Its index in the living, for <see cref="Bury"/>.</param>
        /// <param name="partIndex">The part that reached zero health.</param>
        /// <remarks>
        /// <para>
        /// <b>The killer gains nothing</b>, which is D106's ruling in as many words: what the dead
        /// part was worth goes into a corpse at the place it stood, and whether anybody eats it is
        /// rule 6's business and a separate event. That is what makes one organ a predator, a
        /// grazer and a scavenger instead of three.
        /// </para>
        /// <para>
        /// <b>The reserve share is by volume</b> — "its volume over the body's" — for the reason
        /// D106 rejected per-part reserves: a body has one account, and a limb's share of it is
        /// the small change with the same effect for the purpose of being eaten.
        /// </para>
        /// <para>
        /// <b>The root's death is the body's.</b> There is nothing to rebuild and nothing left to
        /// live on, so it goes down <see cref="Bury"/>'s own path with the whole body as the
        /// corpse — and a body left under the newborn mass floor by the loss dies
        /// <see cref="DeathCause.Starved"/>, because what killed it is that it cannot be a body,
        /// not that something bit it.
        /// </para>
        /// </remarks>
        private bool KillPart(Organism creature, int index, int partIndex)
        {
            (long By, int Part) by = AttackerOf(creature, partIndex);

            if (partIndex == 0 || creature.Phenotype.PartCount <= 1)
            {
                BodiesEaten++;

                if (Config.CorpseDecayPerSecond > 0f && !Config.ContactEating) CorpsesFromKills++;

                // Before Bury, which zeroes both accounts and queues the death row: what the body
                // was worth is what the kill moved, and the two rows then read in the order the
                // events happened — the kill, and then the death it was.
                NoteKill(
                    creature, partIndex, by, rootLost: true, partsLost: creature.Phenotype.PartCount,
                    tissueJoules: creature.TissueJoules,
                    reserveJoules: Math.Max(0d, creature.Energy) + creature.GestationJoules);

                Bury(creature, index, DeathCause.Eaten, killedBy: by.By);
                return true;
            }

            // The path is what a lost part is remembered by — Organism.LostPartPaths says why an
            // index is not. It is read off a development of the plan the body is standing on now,
            // which is the plan the part index refers to.
            int[] counts = CountsOf(creature);
            _ = DevelopPlan(creature, counts, out List<int[]> before);

            if (partIndex >= before.Count)
            {
                // The body and its plan have come apart, which nothing in this world can do: the
                // health array is remapped with the plan on every change. Refuse rather than kill
                // the wrong part.
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature {creature.Id}: part {partIndex} reached zero health but its ") +
                    FormattableString.Invariant($"plan develops to {before.Count} parts. ") +
                    "A body's health array and its phenotype are remapped together, so this is a " +
                    "plan change that did not go through AdoptPlan.");
            }

            // D220: the paths the body had lost before this part, which the remains cut off it were developed with.
            List<int[]> lostBefore = creature.LostPartPaths;

            var lost = creature.LostPartPaths != null
                ? new List<int[]>(creature.LostPartPaths)
                : new List<int[]>(1);

            lost.Add(before[partIndex]);
            creature.LostPartPaths = lost;

            Phenotype adult = DevelopPlan(creature, counts, out List<int[]> after);

            // Everything went. The developer pruned more than the bite did — a subtree that loses
            // its only surviving anchor — and a body of no parts is a death, not a body.
            if (adult.PartCount == 0)
            {
                BodiesEaten++;
                if (Config.CorpseDecayPerSecond > 0f && !Config.ContactEating) CorpsesFromKills++;

                // The bite did not take the root and the body died of it anyway, so the row says
                // root: what it means is that the loss took the body, which is what a reader
                // watching a grazed body for the next thousand seconds has to know.
                NoteKill(
                    creature, partIndex, by, rootLost: true, partsLost: creature.Phenotype.PartCount,
                    tissueJoules: creature.TissueJoules,
                    reserveJoules: Math.Max(0d, creature.Energy) + creature.GestationJoules);

                Bury(creature, index, DeathCause.Eaten, killedBy: by.By);
                return true;
            }

            Phenotype wasBody = creature.Phenotype;

            // Whether the bitten body was a leaf, read before the new plan is adopted: a limb that held its only
            // photosynthetic tissue is still a leaf's limb (D167's leaf tank, ShedRemains).
            bool wasLeaf = creature.HasPhotosyntheticTissue;

            double adultTissue = Metabolism.TissueJoules(adult, Config);
            Phenotype body = AtTheSameFraction(adult, creature.BodyFraction);
            double tissue = ReferenceEquals(body, adult)
                ? adultTissue
                : Metabolism.TissueJoules(body, Config);

            double released = creature.TissueJoules - tissue;

            // D219: the account holds the parts still held, so what leaves with the limbs is the account
            // before less the account the survivors hold on the new plan. The intact figure `tissue` is
            // compared with the intact body (account plus what was eaten), which is the same difference:
            // the eaten share of the part that died left the account when it was bitten.
            LimbCut limb = null;
            if (Config.ContactEating)
            {
                int[] dialMap = Developer.MatchParts(before, after);
                double survivors = UneatenTissue(body, Remap(creature.PartHealth, dialMap));
                released = creature.TissueJoules - survivors;

                // D220: what the body was, taken before the plan changes, for the remains cut off it.
                limb = new LimbCut
                {
                    Old = wasBody,
                    OldHealth = creature.PartHealth == null ? null : (float[])creature.PartHealth.Clone(),
                    NewToOld = dialMap,
                    PriorPending = creature.PartMapFromPreviousPlan,
                    DeadPart = partIndex,
                    Counts = creature.ModuleCounts == null ? null : (int[])creature.ModuleCounts.Clone(),
                    LostPaths = lostBefore == null ? null : new List<int[]>(lostBefore),
                };
            }

            if (released < 0d) released = 0d;

            // D128, the owner's addition: a destroyed part ages the body, one doubling of its
            // hazard for the whole body's tissue, in proportion to the share that went. Read
            // before the new plan is adopted, while the account is still the whole body's.
            if (Config.AgeingPerWound > 0f && creature.TissueJoules > 0d)
            {
                PushAgeClock(creature, Config.AgeingPerWound * (released / creature.TissueJoules));
            }

            // The share of the one reserve that went with the limb, by volume. Strictly under 1,
            // because the root is never what is taken here and something always remains.
            double share = wasBody.TotalVolume > 0f
                ? 1d - (double)body.TotalVolume / wasBody.TotalVolume
                : 0d;

            if (share < 0d) share = 0d;
            else if (share > 1d) share = 1d;

            double reserve = creature.Energy > 0d ? creature.Energy * share : 0d;

            // Quantised once at the field's door and booked as the same number, so what the body
            // loses and what the corpse or the water gains are one number — DropOneModule's rule,
            // and its remark carries the argument.
            float given = (float)(released + reserve);
            double moved = given;
            double fromReserve = moved - released;

            creature.Energy -= fromReserve;
            if (creature.Energy < 0d) creature.Energy = 0d;

            int partsLost = wasBody.PartCount - body.PartCount;

            AdoptPlan(
                creature, adult, adultTissue, body, tissue, Developer.MatchParts(before, after));

            // The body it was is gone and every part index with it, so an attribution keyed on
            // one is worthless from here: the entry goes, and a second part lost in this same
            // pass is recorded with no attacker rather than with a stale one.
            _lastAttacker.Remove(creature.Id);

            // tj and rj as the corpse will hold them: `given` is the one quantisation, so the two
            // sum to exactly what ShedRemains is about to move. The row goes before the transfer
            // for no reason but reading order — nothing below queues an event of its own except
            // the mass-floor burial, which is a death and belongs after this.
            NoteKill(creature, partIndex, by, rootLost: false, partsLost, released, fromReserve);

            if (given > 0f && Config.CorpseDecayPerSecond > 0f && !Config.ContactEating) CorpsesFromKills++;

            // D219: no corpse. The limbs' tissue and their share of the reserve go to the remains.
            if (Config.ContactEating)
            {
                LeaveRemains(creature, released, fromReserve, wholeBody: false, killedBy: by.By, cut: limb);
            }
            else
            {
                ShedRemains(creature, given, bitten: true, leaf: wasLeaf, killedBy: by.By);
            }

            // The owner's rule of 2026-09-19, applied to what is left: a body whose smallest part
            // is under the newborn mass floor is not a body this world builds, and it dies of the
            // ordinary cause rather than of the bite.
            if (IsUnderTheMassFloor(body))
            {
                Bury(creature, index, DeathCause.Starved);
                return true;
            }

            return false;
        }
    }
}
