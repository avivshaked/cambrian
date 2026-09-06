# Proposal: predation on contact — what a bite is, in a world where bodies touch

*Fable, rewritten 2026-09-06 on D076/D077 (shared space, measured and screened: logbook
0064, 0065). Supersedes the 2026-09-05 draft, whose encounter rule the owner withdrew.
For the ruling after the fine-step confirmation (0067) and the movement round (D075's
order). Built on `scratch/predation-survey.md`, DESIGN.md §5A.3 and §4.4. Absorbed into
DECISIONS.md on ruling, then deleted.*

## What the world now has that it did not

Creatures share one volume and touch: 2,000–13,600 contact pairs per physics step at the
fast step (0065), the pairs counted through `Physics.ContactEvent` with the audit closed
and no measurable cost from the contacts themselves (0064). A stomach lineage evolved
where the matter arrives (0065). The bite machinery exists: `ConsumerCell.Acquire` has a
live-bite route reading a `TissueContact`, tiered by what it touches (carrion 0.8,
living photosynthetic 0.5, living consumer 0.2, all unmeasured), that has never executed
because the metabolic step hands it `contact: null`. `Contact` and `Damage` are specified
per-part sensors that read zero. The proposal is the seam between the two: which contacts
become bites, what a bite takes, what the bitten loses, and how it is all counted.

## The rules

1. **A mouth is a part with a `Consumer` cell.** Nothing new in the genome; founders and
   cell-type mutation already draw the type. Mouth volume is the part's consumer tissue.
2. **A bite is a contact, held.** Each metabolic step, for each mouth, the contact pairs
   of the last physics step (the pairs the `contacts` instrument already sees) are read;
   a pair between a mouth and another creature's part is a bite if it existed at the start
   of the step as well — one step of continuous contact, so a glancing collision is not a
   meal and a body pressed against another is. `Contact` reads 1 on any part in a
   creature–creature pair that step; `Damage` reads the fraction of a part's tissue taken
   in the last step, as §4.4 specifies. No proximity rule, no draw from the RNG: contact
   is physics.
3. **What a bite takes.** Up to `BiteJoulesPerSecond × V_mouth × Δt` from the bitten
   part's tissue joules, through the existing `TissueContact` → `Acquire` path with its
   tiered yields; the eater keeps the yield, the rest is waste as in every transfer. A
   part cannot bite its own creature. Several mouths on one part split the draw in mouth
   proportion. `BiteJoulesPerSecond` is a tunable (`EVOSIM_BITE`), **default 0** so every
   world replays byte for byte; the screen sets it.
4. **What the bitten loses.** The part's tissue energy and, with it, volume at
   `TissueEnergyPerCubicMetre`; a part drained below the development floor is dead
   tissue and the creature dies with `DeathCause.Eaten` (the third cause), its remainder
   carrion through the death path. No mid-life limb loss in the first cut; a bitten
   creature is whole until it is dead.
5. **Matter moves with the bite**: the taken joules carry their `MatterPerTissueJoule`
   share into the eater's locked matter; the waste share returns to the layer's free
   matter where the bite happened. The identity holds by construction; a test asserts it.
6. **The senses**: `Contact` and `Damage` wired in `CreatureSensors` and added to the pool
   behind `SenseContact` and `SenseDamage`, default off, in the perception build's pattern.
   Without them a consumer is a filter feeder that eats what the water presses on it;
   with them it can learn to hold and to flee.
7. **Instruments**: `bites` and `bite J` per window, `eaten` per window, `consumer` and
   `consumer inh` (the guild has never had a column), mean depth of consumers against
   producers, and the food chain in the footer.
8. **Out of scope**: attack and defence (§5A.3 defers them), limb loss, any change to
   the yields (the screen reads them), any change to the vent or the box.

## Why contact and not proximity

The 2026-09-05 draft proposed an encounter rule drawn from the RNG because creatures
could not meet. They can now, and the measurement removed the cost objection. Contact
keeps everything the design says predation is for — morphology as the sense organ, the
body computing a bearing, a chase through water — and it keeps the record honest: a bite
happens where two bodies are, not where a lottery says they might be. The crowd the
vent's conveyor makes (0065) is also where bites will happen first, which is where they
should: grazing in the film is the easy trophic step, exactly as §5A.3's gradient orders it.

## The round that would test it

After 0067 confirms the world and the movement round is read. A screen at 0.02, seeds 2
and 4, two doses of `BiteJoulesPerSecond` against 0, senses `Contact`/`Damage` on in one
arm per dose. Two-sided: consumers persist as an inherited line and producers are not
driven under at one dose or the other; or consumers never hold, in which case the carrion
bridge is measured as insufficient and §5A.3's second bridge (density-dependent cell-type
mutation) is the next lever. Confirmation at 0.01 under D063 as amended with a predation
clause the owner words then. `eaten` against `starved` is the first time cause of death
discriminates anything in the record.

## What the owner rules

1. A bite is a held contact between a mouth and another creature's part (rule 2).
2. The unit of loss: the bitten part's tissue; death at the development floor; no limb loss.
3. Matter moving with the bite.
4. `DeathCause.Eaten` as the third cause.
5. Yields left as coded for the screen; `BiteJoulesPerSecond` set by it.
