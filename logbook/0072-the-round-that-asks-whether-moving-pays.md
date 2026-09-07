# 0072 — The round that asks whether moving pays

**2026-09-07**  ·  round 29, the movement round (D075 item 1) · pre-registered before launch

Nothing in this world has ever been selected for moving. The inherited jointed count reads
0 at the end of every arm of round 28 and never rose above 1 during any of them, and the
same is true of every round before it. The cost side of a muscle is closed: `EffectorDriver`
bills the work. The prize side has never been reachable, because a swimmer that cannot tell
where food is has nothing to swim toward, and since logbook/0062 the three senses that could
tell it have sat behind switches nobody turned on.

This round turns them on, in the world round 28 confirmed. The owner's ruling (D081) put
the question first, in their words:

> what helps us get to a point where creatures evolve brains? that's the question we should
> be asking, because at the end of the day, that's what this evolution sets out to achieve.

A brain is selected only where control changes income or survival. Movement that pays is
the first place that can happen, so it is the next change.

## What is held and what changes

Everything in `scratch/launch-r28.ps1` is held: round 18's closed world with D077's box,
wrap, placement, restoring top and real floor, single-threaded physics, dt 0.01, 30,000 s,
five seeds. Round 28 is the base world (D081), and its five arms are the controls.

Three things change, and D081 ruled all three into one build, so this entry says plainly
that it bends D079's one-change rule and why. Each is a per-step change, so any one of them
alone would make every seed a new realisation; taken together they cost no more seeds than
one would, and the movement question cannot be asked without the first two.

1. **The three senses are on**: `EVOSIM_SENSE_CHEMICAL`, `EVOSIM_SENSE_ENERGY` and
   `EVOSIM_SENSE_FLOW` at 1. Founders and mutation may now draw Chemical (the edible density
   at the part), Energy (seconds of reserve) and Flow (the water's relative velocity at the
   part). The squash scales stay at their defaults, 10 J/m³, 3,000 s and 0.3 m/s, all three
   unmeasured (§5A.10). The simulator answered all seven channels before; this changes what
   a genome can ask.
2. **Added mass is on**, `EVOSIM_ADDED_MASS 0.5`. Every world through round 28 swam on drag
   alone (CLAUDE.md's gotcha). DESIGN §5.4 promotes the term because a drag-only fluid
   collapses body plans toward the ones that cannot swim [C18 §4, p.28]. That is the wrong
   fluid to ask a movement question in. The value is the sphere's coefficient, the midpoint
   of a slender body's broadside and end-on values, and `FluidModel` is isotropic, so no
   finer choice is expressible. The ledger cannot price it: it is an inertial term and the
   ledger prices energy. What checks it is the drag limiter, which stays at 0 at dt 0.01,
   and the `diverged` column.
3. **The global brain is retired.** A child is born without `Genome.GlobalBrain`, a rewire
   never draws the input kind, and `Brain.For` builds no partless group; a stored genome
   still loads. This is D019's argument finished (DESIGN §0q) rather than a knob, so it has
   no setting and no control of its own.

The build is the one `r29smoke` ran, `simHash e43b81a8…`, `coreHash 1084ee1c…`, compiled
on worker 7 and refreshed onto workers 2 to 6 before launch. Its 200 s on seed 1 wrote the
new header tokens and the four new columns, ended on budget, and closed its audit.

## The arms

`r29-s1` to `r29-s5`, seeds 1 to 5, at dt 0.01 for 30,000 s, on workers 2 to 6, launched
with `scratch/launch-r29.ps1 -ExpectSimHash e43b81a8…`. The wall budget is 1,200 minutes,
as round 28's, which ran 549 to 764 minutes with five arms up.

The controls are round 28's five arms.

| control | alive at the end | inherited stomachs | inherited jointed | mean height | verdict |
|---|---|---|---|---|---|
| `r28-s1` | 1,769 | 28 | 0 | −6.2 m | fail (stability) |
| `r28-s2` | 1,829 | 53 | 0 | −7.5 m | pass |
| `r28-s3` | 1,775 | 75 | 0 | −6.5 m | pass |
| `r28-s4` | 1,791 | 138 | 0 | −6.9 m | pass |
| `r28-s5` | 1,794 | 20 | 0 | −11.6 m | fail (recruitment) |

## Validity checks

V1: every header carries `dt=0.01`, `space shared 4x5x5 m, depth 60, wrap, bed`, `surface
restore 1`, `physics jobs 0`, `addedMass 0.5`, `senses
jointangle,jointrate,up,depth,chemical,energy,flow` and the three scale constants; every
other token equals round 28's. V2: the floor silent after founding, the audit within a
joule, standing matter 6,000 at every sample. V3: every manifest reads `ended`, `budget`,
the launched hashes, `diverged 0`; a run that ends otherwise is censored and the scorer's
line says so.

## Predictions

Two bars apply from D081 on. *The goal* is D063's three seeds of five. *The reference* is
the base world's own count, which is round 28's three. Here the two coincide.

A seam to state before M1 and M3 are read. Chemical sensing samples the field at the part's
own depth, feeding is priced at the creature's economic height and patch, and light is read
at the body's height. A controller can exploit only a gradient its acquisition model
resolves, so a gain in M3 is read against `food jnt` and `mat here`, not against what a
sensor saw. And a number to carry: a neuron costs 0.05 W and a connection 0.01 W, while the
median stomach in round 28 nets about 0.03 W. One neuron costs more than a stomach's profit,
so a brain can only appear where it multiplies income, and a sense that is carried without
paying is a sense that mutation keeps drawing, not one selection keeps.

| # | prediction | falsified by |
|---|---|---|
| M1 | **the senses are taken up**: `sense` (living genomes with a Chemical, Energy or Flow input) is at least 20% of `alive` at every sample over the last 6,000 s, in every arm | `sense`, `alive` |
| M2 | **a jointed guild persists**: `jnt inh` ≥ 10 at every sample over the last 6,000 s in at least 3 of 5 (the controls read 0 in every arm of the record) | `jnt inh` |
| M3 | **movement pays where it exists**: in every arm with a jointed guild at the end, `food jnt` exceeds `food rig` by more than 20% averaged over the last 6,000 s, with `spd jnt` ≥ 0.05 m/s | `food jnt`, `food rig`, `spd jnt` |
| M4 | **the goal still holds**: D063 as amended in at least 3 of 5, by `clade-score.ps1` (the goal and the reference, both 3) | the scorer |
| M5 | **the world stands under added mass**: `alive` at 30,000 s within 0.75 to 1.25 of the same seed's round-28 value, and `diverged` 0, in every arm | `alive`, `diverged` |
| M6 | **the audit closes and the drag limiter never binds** (added mass changes inertia, not the ledger; the limiter engages only above dt 0.01) | `audit`, the manifest's `dragImpulsesLimited` |

## The two-sided readings

- **M1 to M3 hold:** movement pays. The owner words the movement clause of the goal rule
  and the round that scores it (HANDOFF item 9's ecological layer), and predation on
  contact is next (D081's order).
- **M1 holds, M2 fails:** the senses are carried and muscles still do not pay. The prize is
  not reachable by swimming at this cost; read `spd jnt` in the early samples (was there a
  swimmer to select on?) and the cost side (`EffectorDriver`'s billing, `EVOSIM_LIFT_COST`)
  is the next question, the owner's.
- **M1 fails:** the inputs are drawn and do not persist, a sensor that reads a gradient
  the neuron cannot use at a price it cannot pay; the squash constants and the neuron price
  are the levers, both the owner's.
- **M2 holds, M3 fails:** swimmers survive without eating better, so a jointed body pays
  for something else. `dep jnt` against `dep rig` is the first reading: a guild that holds
  a different depth is buying a position. The active-versus-clamped assay (HANDOFF item 7)
  then runs on saved members of the jointed clade. It drives the same genome by its brain
  and with its joints clamped, alone, under `SwimSurvey`'s harness, so the position is
  shown to come from the stroke rather than from the body's shape.
- **M4 fails:** the three changes cost the rule. Which one is the follow-up, and it is
  pre-registered then: senses off with added mass on is the first arm, because it separates
  the fluid from the perception.
- **M5 fails:** added mass at 0.5 is not a small change to this world, and the round is read
  as a fluid result before it is read as a movement result.

## What the round does not ask

Whether a brain evolves. A jointed guild that pays its way is the precondition, and the
ecological layer of the movement rule is the owner's to word after this reading. Nor does
it ask about predation: `EVOSIM_BITE` does not exist yet, and the consolidated proposal
waits on the owner.

## Launch

*2026-09-07, late evening.* `r29-s1` to `r29-s5` launched on workers 2 to 6 at commit
`40b16b8`, every manifest reading the launched `simHash e43b81a8…`, `coreHash 1084ee1c…`,
`physicsJobWorkers 0` and `gitDirty false`; every header carries V1's tokens. The machine
holds five arms and nothing else. Read as they land, against this entry and the scorer.
