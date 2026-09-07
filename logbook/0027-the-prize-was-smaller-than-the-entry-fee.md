# 0027 — The prize was smaller than the entry fee

**2026-08-28**  ·  Milestone 4

[0026](0026-nothing-was-ever-eliminated-for-swimming-badly.md) priced a joint and found
nothing could afford one. It left two things open: whether an affordable joint is any use,
and which of four knobs should move. Two probes were run to answer the first. They answered
a different question instead, and the answer was worth more.

## The probes were break-even arms, and they proved it

| arm | joint | jointed net | | outcome |
|---|---|---|---|---|
| control | 5–20 N·m, idle 0.02 | −0.296 W | insolvent | joints 0% |
| `joint-weak` | 1–4 N·m, idle 0.02 | +0.054 W | **solvent** | joints 0% by t=7,500 |
| `joint-strong` | 10–20 N·m, idle 0.002 | +0.074 W | **solvent** | joints 0% by t=5,000 |

Both probes put a joint on the right side of break-even. Both ran to generation 36 and 46.
Joints went extinct in both, and `work J/s` stayed at 0 in every arm for the whole run.

That settles something 0026 could not: cheapness is not enough. The rule in §5A.1 makes a
link's best possible case *earning nothing*. Nothing loses to a photosynthetic cell of the
same volume earning 0.96 W, at every price. The two probes moved 0.40 W of idle charge out
of a 2.22 W bill. They left the 1.30 W of forfeited income untouched, and that is the term
that decides it. Four sweeps have now all moved knobs that cannot reach the dominant cost:
D031, D032 and these two.

## Why locomotion cannot pay here, quantitatively

The prize was measured, and it is not close.

`LightField.Contribute(heightY, litArea)` bins shading by depth layer, and
`NutrientField.DensityAt` takes a height. Everything a creature can earn is a function of
its Y coordinate alone. Horizontal position is economically meaningless by construction,
which D037 records as forced by §6.3's tiling rather than chosen. So a muscle's only
possible purchase is depth.

And the population is already at the best depth. Mean height sits between −1.8 m and +3.2 m
in a 60 m column, where `IrradianceAt` clamps to full surface irradiance. Selection put them
there rather than swimming: born at the parent's depth, deep lineages die, shallow ones
breed. A static gradient is exhausted by sorting, and once sorted a muscle can only move you
somewhere worse.

```
maximum prize from perfect depth control   ~0.13 W
cost of making one of two parts a muscle    2.22 W
```

The entry fee is roughly seventeen times the prize. No controller, no thrust model and no
actuator tuning can close that, and that is the reason "joints go to zero" never yielded to
behavioural hypotheses.

## What D036 already said, and what changed

D036's third failure is this same diagnosis, three days earlier and sharper:

> *in still water doing nothing is free and optimal... station-keeping is a task with continuous
> returns from arbitrarily close to zero*

Its fix, the current, was built (D037) and is running. Checking its three failures against
the live arms:

| D036 failure | then | now |
|---|---|---|
| detritus stranded on the floor | 77.5% | **2.5%** |
| nutrient density where creatures live | 0 | **9.05 J/m³** |
| displacement from birth | 6 mm | **1.34 m** |
| swimming's share of that | ~0.03% | **work = 0** |

Two of three are fixed. The denominator moved and the numerator did not. Position is no
longer destiny, and it is the water that moves creatures rather than the creatures
themselves.

## The change, and two mistakes caught on the way

`LinkCell.PhotosyntheticEfficiency` defaults to **0**, so §5A.1 is unchanged and every
earlier number stands (D043).

Two arms are in flight, one variable each, against the control `g-c1.0-s1`.

The first is `linkearn` at 0.5. The second is `daynight` at amplitude 1, which is D035's
cycle retried in the world D037 finally gave it.

Both mistakes were caught by guards rather than by reading the code, which is the only
reason they are cheap enough to be worth recording.

The knob read 20× high. `PhotosyntheticEfficiency` was written as an absolute capture
fraction, so 1.0 meant *all incident light* while a leaf captures 0.05. That priced a
jointed creature at
**39.5 W** against a plant's 1.92 W. The test asserting "a fully photosynthetic link must still
lose to two green parts" failed on the first run, and was written for that case. It is now a
fraction *of* `PhotosyntheticCell.DefaultEfficiency`.

The first launch was not a controlled comparison. Both arms were started with only the
variable under test set, and the defaults supplied the rest:

| setting | the arms as launched | the control |
|---|---|---|
| current | `current 0` | `0.05` |
| mixing | `mixing 0` | `2` |
| senescence | `senescence off` | `3000 s` |
| joint power | `maxPower 120` | `20` |
| irradiance | `48 W/m²` | `64` |

With mixing off, detritus strands on the floor again, so `daynight` would have reproduced
D035's original failure, for ten hours of CPU, and looked like a replication. It was caught
by diffing the header each run writes about itself, and that is what the header is for.

`scripts/run-arm.ps1` now holds the invocation, because every arm before these was launched
by hand and none of them is reproducible from anything but a shell history.

## The arm answered within four hours, and said no

`linkearn` ran at a fraction of 0.5. Link tissue was gone from the population by t=3,000.
What vanished was the cell type itself rather than only the joints. The survivor snapshots
read `photo=100%` from there on, at generation 36.

The pricing says why:

```
two photosynthetic parts, no joint :  1.9239 W
one part + 20 N.m hinge, photo 0.50:  0.6999 W   solvent — and 36% of the plant
one part + 20 N.m hinge, photo 1.00:  1.6954 W   solvent — and 88% of the plant
```

Solvency was never the bar. Offspring are paid for out of surplus (§5A.6), so a creature
banking at 36% of its neighbours' rate is outbred and gone while it is comfortably alive.
This entry opened by recording that the two joint probes were break-even arms. The arm
written in response to that finding, in the same entry, on the same day, was the fourth one.

And the ceiling is structural. At fraction 1.0, muscle as good at light as a leaf, a joint
still reaches only 88% of a plant, because the idle charge does not scale away. The test
guarding the change asserts that domination explicitly. The arm's own guard guaranteed it
could not succeed, and it was written before the arm was launched.

`daynight` closed the same way from the other side. Amplitude 1 with `linkPhoto` at 0 left
its joints insolvent at −0.30 W, and they died by t=5,000 having never been affordable.
Affordability and a motive were run as two arms when they are one experiment.

What is now established is a requirement, and that is more than either arm was after.
Movement must be worth more than **12% of a creature's income**, because that is what the
best possible muscle still gives away. It is presently worth about 7% and realised at 0%.
The cost side is closed, being bounded and unable to reach, so every remaining option is on
the prize side.

`linkday` (fraction 1.0, amplitude 1) is the first run in this project's history where a
joint is both affordable and has something to buy. That one failed as well. Joints reached
0% by t=5,000 and link tissue left the population, the same shape and the same timing as
every arm before it, at generation 34.

⚠ A founder transient was briefly recorded here as a first, and was not one. `linkday`
reports `work 39.27 J/s` at t=300, and the control reports 43.21 J/s at t=250. So does every
arm, because founders are drawn with joints and actuate until they die. Work goes to zero
when the joints do, in all of them.

Nothing distinguished that opening from anyone else's. The monitor had a gate for this case,
`alive >= 500`, since the population floor is 40. The claim was made in prose, where the
gate does not apply.

⚠ And then the gate itself failed, on `lit`. It reported joints surviving reproduction at
t=700 with 1,775 alive. The counts:

```
t=100   alive=40    jointed=16   j%=40%
t=400   alive=113   jointed=10   j%=8.8%
t=700   alive=1775  jointed=7    j%=0.4%
```

The absolute count falls while the population grows forty-fold. The share collapses from the
founder rate of ~40% to 0.4%, with joints being purged as fast as ever. The gate used `alive
>= 500` as a proxy for "reproduction has had its say". At 200 W/m² a population reaches
1,775 in 700 s with its founder cohort still alive (44 deaths). The proxy was calibrated on
a world that grows slowly, and it silently stopped meaning anything in a world that does
not. It is keyed on joint *share* now, at a tenth of the founder rate, since a count stops
being a measurement when the denominator is moving.

## A joint is useful; it was never useless, only unaffordable

The question 0026 left open, and which every arm above silently assumed an answer to, took
eleven minutes to settle. `SwimSurvey` already measured it: 200 genomes, 20 s each, no
gravity, no contact, net vertical displacement signed toward the light. It had never been
read as a thrust curve.

Best net rise, founder-shaped bodies (mean 1.51 parts, 0.95 dof, the shape evolution
produces here):

| joint capacity | best rise over 20 s | sustained vertical |
|---|---|---|
| 5 N·m | 0.16 m | 0.008 m/s |
| **20 N·m** | **0.34 m** | **0.017 m/s** |
| 60 N·m | 0.59 m | 0.029 m/s |
| 120 N·m | 1.05 m | 0.052 m/s |

A hinge on a two-part body produces real directed thrust, at the capacity the world already
uses. So the question of whether joints are useless is answered, and the answer is no. Every
failure recorded in 0026 and above is an affordability failure, and none of them was ever
about swimming.

⚠ The **median** is 0.0000 m in every arm at every capacity. Only the best of 200 rises at
all, which is right for unselected random brains, since a founder is as likely to be wired
to dive as to rise. That is why this is a capability measurement rather than a fitness one:
it bounds what selection could find, from below. Blind and sensing arms are
indistinguishable here, as `SwimSurvey`'s own remarks predict for an unselected population.

That sets an upper bound. A sink rate must sit under what a joint can push against, and
0.017 m/s at 20 N·m is the ceiling. Because a partial swimmer sinks more slowly and collects
a proportionate share of the prize, the break-even crossing arrives at roughly half of it.
That is D036's "continuous returns from arbitrarily close to zero", and it is the first
mechanism proposed here whose gradient starts at zero rather than at a cliff.

## Something to lose by standing still

`FluidConfig.TissueExcessDensity` (D044) defaults to 0, so §5.2 is untouched, and the survey
at 0 returns the earlier numbers to the digit. The sink rate was calibrated against the
observable rather than derived. The derivation would have been wrong by tenfold. Predicting
from quadratic drag gave 0.011 kg/m³ for 0.01 m/s, and the measurement gives **0.1 kg/m³**.
It is linear, because bodies at hundredths of a metre per second are not in the quadratic
regime.

`sink` runs at 0.15 kg/m³ with muscle at full rate, which puts affordability and motive in
one arm, and that is the correction to running them as two. The margin was chosen
deliberately wide. A non-swimmer loses ~30% of lifetime income against a muscle costing 12%,
a ratio of 2.5, where 0.1 kg/m³ would have given 1.75. Every arm this project has run at a
ratio near 1 has failed, and today supplied four more.

The opening rows are unlike any arm before them:

```
t=100   alive=40  jointed=15  meanHeight=-12.20  work=0.649  audit=-0.0003
t=1500  alive=40  jointed=5   meanHeight=-15.80  work=5.815  audit=-0.0005
t=2200  alive=40  jointed=11  meanHeight=-19.78  work=4.984  audit=-0.0017
```

Creatures are **sinking**, with mean height near −20 m against −1.8 to +3.2 m at the surface
everywhere else. They are also doing sustained mechanical work past the founder phase, which
nothing has done here before. The audit holds at ~0.001%, so an external force has not
opened a hole in the economy. Whether any of it survives reproduction is the question, and
it is not answerable yet: `alive` is still 40, the population floor.

## The world is eight metres deep

`sink` ran its whole 40,000 s budget, fast, because the population never grew. It finished
at alive=40, generation 0, every living creature a floor spawn and nothing ever bred. Mean
lifetime expenditure ran at **241 J against 87 J of income**, 2.7x. Work was never the
problem, and work share stayed between 0.2% and 5.8%.

The measurement that explains it, and which should have preceded every arm today:

| depth | irradiance | net W |
|---|---|---|
| 0 m | 64.00 | +2.6460 |
| −8 m | 32.86 | +0.3571 |
| **−10 m** | **27.81** | **−0.0136** |
| −20 m | 12.09 | −1.1695 |

The habitable band is eight metres. Income falls exponentially and upkeep does not fall at
all, so below −10 m nothing solvent exists. `sink` put creatures at −19 m. It did not fail
to make swimming pay; it made the world uninhabitable, and a run reports both as a
population pinned at the floor.

Three things follow that were not obvious before.

- The surface sorting was never a preference. Mean height of +0.7 to +5.4 m across every arm is
  the top of an 8 m band rather than a choice.
- `FounderDepthSpread` is 20 m, so a large share of founders in *every run this project has ever
  done* are born below −10 m, and born dead.
- The muscle question is capped structurally. Eight metres is 0.67 attenuation lengths, so
  income can vary by at most 51% across the entire livable world. A descent worth 27% would
  consume most of the band.

Band depth is `12·ln(I_surface / 27.8)`, which is 8 m at 64 W/m² and 24 m at 200. And the
reason irradiance sits at 64 is runaway (§5A.2b): light covering upkeep so well that nothing
has to do anything. That is the condition a sink removes. Irradiance and buoyancy have room
together that neither has alone, and that is the first thing today that looks like a way
through rather than another knob.

## Sinking compounds down a lineage, which the arithmetic did not

`sink-lit` runs 200 W/m² for a 24 m band, with 0.13 kg/m³ for an 8 m lifetime descent, and
it is **habitable**. That much of the fix worked. Mean income runs 300–440 J against 200–366
J of expenditure, and creatures are solvent at −25 m. At −19 m, `sink` had them spending
2.7x what they earned.

It still does not work, and the reason is an error in how the sink rate was sized. Offspring
are born at their parent's depth. A descent of 8 m per lifetime is therefore not a
per-creature handicap that resets each generation. It accumulates down the lineage, 24 m
after three generations, with no reset except swimming. Every rate in D044 was computed per
lifetime, and the quantity that matters is per lineage.

The visible consequence is a population too small to search rather than death:

```
   t   alive  jointed  height  genMax
1000     113        1   -24.2      11
6400      40        0   -29.3       9
10900     40       15   -19.0       2
```

40–113 alive, oscillating, with lineages establishing and sinking out and floor spawns
replacing them. Whatever a joint would be worth here, a population of forty does not have
the mutational supply to find one. And `jointed` swinging 0–18 between samples is founder
draw rather than selection.

That is a third distinct failure mode for one mechanism, and it needs a name separate from
the other two. `sink` was **energy-limited**, below the habitable band; `sink-lit` is
**search-limited**, where the band is fine and the population is not. They report almost
identically in the summary columns.

## Half of every founder is born dead

Following the band measurement to its consequence: `World.SpawnFounders` places a founder at
`-rng.Range(0, FounderDepthSpread)`, uniform over **20 m**, and its comment says *"through
the lit zone"*. The lit zone, where a creature is solvent, is **9.75 m** at 64 W/m².
Measured:

```
irradiance 64 W/m2, habitable band 9.75 m, founder spread 20 m -> 49% of founders born solvent
```

Fifty-one per cent of every floor spawn is born below break-even. The spread was chosen for
a good reason. Starting everything at the surface would hand generation zero the best light
in the world, and make §5A.2's calibration read as more generous than it is. But 20 m was
picked when nobody knew where the bottom of the world was, and the comment describes a lit
zone twice the size of the real one. It halves the effective mutational supply of every run
this project has performed. That is the quantity that matters most when the open question is
whether a rare variant can be found at all.

It is not changed here. [D036](../DECISIONS.md#d036) rejected shrinking the spread on the
grounds that it flattens the vertical structure §5A.4 exists to provide, and that argument
survives this measurement untouched. The fix, if there is one, is to derive the spread from
the light model rather than to pick a smaller constant. What is added is the invariant that
would have caught it. `MostFoundersAreBornSomewhereTheyCanLive` fails below a quarter, on
the grounds that a floor mostly manufacturing corpses makes every run's mutational supply a
fiction.

Note this is irradiance-dependent, and the sink arms are not affected: at 200 W/m² the band
is about 24 m and a 20 m spread falls inside it.

## A worker that was never re-synced

`sink-mid` came back with a config hash byte-identical to the one `lit` had run under.

That hash is `79da5b4c218176ba`.

Its header was in the old format, with no `linkPhoto` and no `excessDensity` field at all.

`unity-w2` had never been re-synced after those variables were added to `EvolutionRun`, and
only workers 6 and 7 were.

A stale worker runs old code and says nothing about it. The arm silently ignored the one
parameter it existed to test, and would have read as a clean negative.

Two things follow from that. The first is that `sink-mid` was a duplicate of `lit`, so it
was stopped, worker 2 was re-synced, and it was relaunched.

The two arms now hash to `e85411dfd8969776` and `6588a8e7db4e0f1d`, and all three are
distinct.

The second is that `lit` itself was not what it was described as. It ran with `linkPhoto` 0
rather than 1. Its result stands: a bright world runs away without a sink. It is arguably the
cleaner brightness-only control for having no muscle change in it, but the earlier
description of it was wrong.

CLAUDE.md's rule caught this as written, that *identical numbers across a configuration
change mean the change was not applied*. The rule's stated remedy is to prove the parameter
reached the thing it configures, and that is what the run header is for. It is now the third
distinct failure the header has caught in a day, after silently-filled defaults and the
founder-transient gate.

## The current drowns the signal the sink was added to create

Both sink arms settled at −12.7 to −12.9 m *regardless of sink rate*, which cannot be the
sink setting the depth. The displacement columns say what is:

```
sink-slow (0.02)   meanRise 3.8-5.1 m per lifetime, mean age ~400 s
                   sink at 0.0018 m/s over 400 s accounts for 0.7 m of it
sink-mid  (0.05)   meanRise 25.9 m at t=2,200, depth -40.9 m, then an oscillating recovery
```

The current moves bodies 18–28x faster than they sink. `CurrentField` runs at 0.05 m/s
against sink rates of 0.0018–0.0045. Buoyancy therefore contributes under a fifth of a
creature's vertical displacement, and the rest is stirring. Selection cannot see a signal
that small against that noise, which is [D036](../DECISIONS.md#d036)'s second failure
restated with the current itself as the noise source rather than founder scatter:

> *position is inherited and effectively immutable... swimming accounts for 0.03% of the variance*

The irony is worth stating plainly. The current was added to make position mutable, and it
is now what makes the mechanism that properly mutates position unreadable.

[D037](../DECISIONS.md#d037) separates the two jobs, in that *"`CurrentField` advects
bodies; `NutrientField.Mix` diffuses detritus. One cannot do both jobs"*. So the current can
be switched off without stranding energy on the floor, which is the failure it was built
for. With buoyancy supplying both a gradient and a reason to move, body advection is no
longer doing useful work and is a noise source and nothing else.

`sink-still` runs the pair that has never been tried: still water, and tissue that sinks.
The claim in §5.2 that *"depth changes only by swimming"* becomes true again, and for the
first time there is a reason to.

## What is still open

`stats.jsonl` and `lineage.jsonl` were empty in every run to date.

`RunDirectory` opened both writers and nothing ever called them, so what survived was
markdown and genome snapshots. `stats.jsonl` is now written from the same locals as the
markdown row, so the two cannot drift.

Deliberately, `lineage.jsonl` is still unwritten. The design wants one row per creature ever
born (§9), and at the observed birth rate that is hundreds of megabytes an hour for ancestry
nothing reads.
