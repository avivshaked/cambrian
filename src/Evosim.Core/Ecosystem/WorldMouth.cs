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

            ApplyDamage(seconds);
            ApplyIntake(seconds);
            ApplyHealing(seconds);

            _contacts = null;

            return CollectTheDead();
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

                NoteContact(a, touch.PartA);
                NoteContact(b, touch.PartB);

                Wound(
                    b, touch.PartB, hittingB, DamageOf(hittingA, hittingB, seconds),
                    a.Id, touch.PartA);
                Wound(
                    a, touch.PartA, hittingA, DamageOf(hittingB, hittingA, seconds),
                    b.Id, touch.PartB);
            }
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

        private void Wound(
            Organism creature, int partIndex, PhenotypePart part, float damage, long attackerId,
            int attackerPart)
        {
            if (!(damage > 0f)) return;

            NoteAttacker(creature, partIndex, attackerId, attackerPart);

            float pool = Metabolism.HealthPool(part, Config);

            float[] health = creature.PartHealth;
            if (health == null)
            {
                health = new float[creature.Phenotype.PartCount];
                for (int i = 0; i < health.Length; i++) health[i] = 1f;
                creature.PartHealth = health;
            }

            float[] lost = creature.PartDamage;
            if (lost == null)
            {
                lost = new float[creature.Phenotype.PartCount];
                creature.PartDamage = lost;
            }

            // A pool of zero is a part with no volume or no toughness to speak of, and any blow at
            // all finishes it. Written as a branch rather than left to a division, which would
            // hand the sense an infinity.
            float share = pool > 0f ? damage / pool : 1f;

            float was = health[partIndex];
            float now = was - share;
            health[partIndex] = now > 0f ? now : 0f;

            lost[partIndex] += was - health[partIndex];
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
            if (!(Config.IntakeReachMetres > 0f) || _corpses.Count == 0) return;

            bool anyMouth = false;
            for (int i = 0; i < _living.Count && !anyMouth; i++) anyMouth = _living[i].HasIntake;
            if (!anyMouth) return;

            double perUnit = Config.JoulesPerUnit;
            float tissueDensity = Config.CellTypes.Resolve(CellTypeIds.Structural)
                .TissueEnergyPerCubicMetre;

            var demand = new double[_corpses.Count];

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (!creature.HasIntake) continue;

                for (int c = 0; c < _corpses.Count; c++)
                {
                    if (!InReach(creature, _corpses[c], tissueDensity)) continue;
                    demand[c] += MouthRate(creature, seconds);
                }
            }

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (!creature.HasIntake) continue;

                for (int c = 0; c < _corpses.Count; c++)
                {
                    Corpse corpse = _corpses[c];
                    if (!(corpse.Joules > 0d)) continue;
                    if (!(demand[c] > 0d)) continue;
                    if (!InReach(creature, corpse, tissueDensity)) continue;

                    double wanted = MouthRate(creature, seconds);
                    double available = corpse.Joules / perUnit;

                    // The share this mouth is entitled to of what the corpse actually holds. At a
                    // corpse richer than the crowd's appetite every mouth takes what it asked for,
                    // which is what the min is: the share is a rationing and never a bonus.
                    double units = demand[c] > available
                        ? wanted * (available / demand[c])
                        : wanted;

                    if (!(units > 0d)) continue;

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

                    creature.Energy += joules - waste;
                    UnitsEaten += joules / perUnit;

                    if (corpse.Joules <= 0d) CorpsesEaten++;
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
        private bool InReach(Organism creature, Corpse corpse, float tissueDensity)
        {
            double volume = tissueDensity > 0f ? corpse.Joules / tissueDensity : 0d;
            double radius = volume > 0d ? Math.Pow(3d * volume / (4d * Math.PI), 1d / 3d) : 0d;

            double reach = radius + Config.IntakeReachMetres;

            Float3 at = corpse.Position;
            double dy = (double)creature.HeightY - at.Y;
            double dx = Separation(creature.X, at.X, Nutrients.LengthMetres);
            double dz = Separation(creature.Z, at.Z, Nutrients.WidthMetres);

            return dx * dx + dy * dy + dz * dz <= reach * reach;
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

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                float[] health = creature.PartHealth;
                if (health == null) continue;
                if (!(creature.Energy > 0d)) continue;

                IReadOnlyList<PhenotypePart> parts = creature.Phenotype.Parts;
                int n = health.Length < parts.Count ? health.Length : parts.Count;

                double wanted = 0d;

                for (int p = 0; p < n; p++)
                {
                    if (health[p] >= 1f || health[p] <= 0f) continue;

                    double share = 1d - health[p];
                    if (share > step) share = step;

                    wanted += share * Metabolism.HealthPool(parts[p], Config) *
                              Config.HealingJoulesPerHealth;
                }

                if (!(wanted > 0d)) continue;

                double afford = wanted <= creature.Energy ? 1d : creature.Energy / wanted;
                if (!(afford > 0d)) continue;

                for (int p = 0; p < n; p++)
                {
                    if (health[p] >= 1f || health[p] <= 0f) continue;

                    double share = 1d - health[p];
                    if (share > step) share = step;

                    float healed = (float)(health[p] + share * afford);
                    health[p] = healed > 1f ? 1f : healed;
                }

                double spent = wanted * afford;
                if (spent > creature.Energy) spent = creature.Energy;

                creature.Energy -= spent;
                EnergyOut += spent;
                HealingJoules += spent;

                // The one float in the transfer, and it is the field's API rather than an account
                // — World.Metabolise's burn says so at the same line, for the same reason.
                float returned = (float)(spent / Config.JoulesPerUnit);
                Matter.Deposit(creature.Point, returned);
                BurntTotal += returned;
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

                if (Config.CorpseDecayPerSecond > 0f) CorpsesFromKills++;

                // Before Bury, which zeroes both accounts and queues the death row: what the body
                // was worth is what the kill moved, and the two rows then read in the order the
                // events happened — the kill, and then the death it was.
                NoteKill(
                    creature, partIndex, by, rootLost: true, partsLost: creature.Phenotype.PartCount,
                    tissueJoules: creature.TissueJoules,
                    reserveJoules: Math.Max(0d, creature.Energy));

                Bury(creature, index, DeathCause.Eaten);
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
                if (Config.CorpseDecayPerSecond > 0f) CorpsesFromKills++;

                // The bite did not take the root and the body died of it anyway, so the row says
                // root: what it means is that the loss took the body, which is what a reader
                // watching a grazed body for the next thousand seconds has to know.
                NoteKill(
                    creature, partIndex, by, rootLost: true, partsLost: creature.Phenotype.PartCount,
                    tissueJoules: creature.TissueJoules,
                    reserveJoules: Math.Max(0d, creature.Energy));

                Bury(creature, index, DeathCause.Eaten);
                return true;
            }

            Phenotype wasBody = creature.Phenotype;

            double adultTissue = Metabolism.TissueJoules(adult, Config);
            Phenotype body = AtTheSameFraction(adult, creature.BodyFraction);
            double tissue = ReferenceEquals(body, adult)
                ? adultTissue
                : Metabolism.TissueJoules(body, Config);

            double released = creature.TissueJoules - tissue;
            if (released < 0d) released = 0d;

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

            if (given > 0f && Config.CorpseDecayPerSecond > 0f) CorpsesFromKills++;
            ShedRemains(creature, given);

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
