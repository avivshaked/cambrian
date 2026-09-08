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

## Results

*2026-09-08, small hours.* All five arms ended on budget at 30,000 s after about nine hours
each, at 0.9 times real time with five on the machine. V1 to V3 held: every header carried
the round's tokens, every manifest reads `ended budget` with `diverged 0`, the audit read
0.0000% at every sample and neither impulse limiter bound once. The readings come from
`scripts/clade-score.ps1` and from the named columns over the last 6,000 s
(`scratch/r29-read.py`); the sense channels come from each arm's last snapshot, counting
genomes that carry an input of that kind, expressed or not.

| arm | M1 sense share | M2 `jnt inh` | M4 scorer | M5 alive against round 28 | M6 |
|---|---|---|---|---|---|
| `r29-s1` | 0.9–1.2%, failed | 0 | fail, stability: a clade born at 13,044 s, 29 at the end, minimum 3 | 1,780 against 1,769 (1.01), held | held |
| `r29-s2` | 36–40%, held; the energy sense, 810 of 1,777 genomes | 0 | **pass**: a clade of 153, minimum 15, 67 recruits in the last window | 1,777 against 1,829 (0.97), held | held |
| `r29-s3` | 1.4–2.3%, failed | 0 | fail: the line collapsed to 4 with 211 kJ of exudate standing | 1,781 against 1,775 (1.00), held | held |
| `r29-s4` | 55–57%, held; the chemical sense, 1,144 of 1,783 | 0 | **pass**: a clade of 72, minimum 45, 27 recruits | 1,783 against 1,791 (1.00), held | held |
| `r29-s5` | 28–29%, held; the chemical sense, 435 of 1,830 | 0 | fail, stability: a clade born at 13,106 s, 21 at the end, minimum 3 | 1,830 against 1,794 (1.02), held | held |

**M1** held in three arms and failed as written, since it asked for every arm. Where it held
it held well: a third to a half of the population carried a sense through the last two
lifetimes, and each world settled on one channel. Seed 2 carries the energy sense, a body
reading its own reserve; seeds 4 and 5 carry the chemical sense. Seeds 1 and 3 carry
almost none, at 1 to 2%. The seam stated before the predictions applies: a carried input is
not a used one, and this round cannot say which of the three worlds selected its sense and
which merely afforded it.

**M2** failed in every arm. No arm held an inherited jointed body at any sample of its last
6,000 s, which is the record's reading again, now with the senses on and the fluid pushing
back. **M3** therefore does not apply. Seed 3 printed a jointed feeding rate in its last
window, 12% above the rigid guild's at 0.061 m/s, and it came from bodies whose joints were
not inherited, so it is founder noise and not a reading.

**M4** is two of five, against a goal and a reference of three. Seeds 1 and 5 failed the way
round 28's seed 1 did: a stomach clade founded after 13,000 s that reached ten members only
in the last 5,000 s, so its minimum through the last two lifetimes is 3. Seed 3's line
collapsed. Round 28 read three of five on the same seeds, and the difference is one seed.
The three changes are per-step terms, so every seed of this round is a new realisation of
its world (logbook/0052's wingspan), and one seed either way is inside what a realisation
moves. The round cannot say whether the changes cost the rule a seed, and I am not going to
read it as though it could.

**M5** held in every arm, with the population at the end within 3% of round 28's same seed.
Added mass at 0.5 leaves this world where it was, and the round is read as a movement result
and not a fluid one. **M6** held.

One reading outside the predictions. The exudate stood at 34 kJ in seed 4 and 211 kJ in
seed 3 at the end, in order of how thin the stomach line was, which is round 27's finding
again: the loop closes only where the stomachs are. The vertex-world screens of the same
night stood at 5 to 7 kJ at 10,000 s (logbook/0074), and round 30's M4 reads that.

## Verdict

The branch is "M1 holds, M2 fails" in three seeds and "M1 fails" in two. The senses can be
carried at today's price by a third to a half of a world, and the muscles still do not pay.
The entry's reading for both branches is that the price is the lever, and the owner had
already pulled it: D082, ruled by question the evening the round launched, brings the neuron
and its inputs down about tenfold and the stroke's work to a fraction, with this round as
the control at today's price. The M4 branch's pre-registered follow-up, senses off with added
mass on, is not run. D083 moved the base to the vertex world the same night, and separating
the senses from the fluid in the cell world would answer a question about a world the
campaign has left; it is queued in `HANDOFF.md` for the owner to revive if round 30's world
control says the fluid mattered after all. Round 30 is pre-registered in logbook/0075.

