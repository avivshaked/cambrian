# 0027 — The prize was smaller than the entry fee

**2026-08-28**  ·  Milestone 4

[0026](0026-nothing-was-ever-eliminated-for-swimming-badly.md) priced a joint and found nothing
could afford one. It left two things open: whether an affordable joint is any use, and which of
four knobs should move. Two probes were run to answer the first. They answered a different question
instead, and the answer was worth more.

## The probes were break-even arms, and they proved it

| arm | joint | jointed net | | outcome |
|---|---|---|---|---|
| control | 5–20 N·m, idle 0.02 | −0.296 W | insolvent | joints 0% |
| `joint-weak` | 1–4 N·m, idle 0.02 | +0.054 W | **solvent** | joints 0% by t=7,500 |
| `joint-strong` | 10–20 N·m, idle 0.002 | +0.074 W | **solvent** | joints 0% by t=5,000 |

Both probes put a joint genuinely on the right side of break-even. Both ran to generation 36 and 46.
**Joints went extinct in both**, and `work J/s` stayed at exactly 0 in every arm for the whole run.

Which settles something 0026 could not: **cheapness is not enough.** §5A.1 makes a link's best
possible case *earning nothing*, and nothing loses to a photosynthetic cell of the same volume
earning 0.96 W — at every price. The two probes moved 0.40 W of idle charge out of a 2.22 W bill and
left the 1.30 W of forfeited income untouched, which is the term that decides it. Four sweeps —
D031, D032 and these two — have now all moved knobs that cannot reach the dominant cost.

## Why locomotion cannot pay here, quantitatively

The prize was measured, and it is not close.

`LightField.Contribute(heightY, litArea)` bins shading by depth layer; `NutrientField.DensityAt`
takes a height. **Everything a creature can earn is a function of its Y coordinate alone** —
horizontal position is economically meaningless by construction, which D037 records as forced by
§6.3's tiling rather than chosen. So a muscle's only possible purchase is depth.

And the population is already at the best depth. Mean height sits between −1.8 m and +3.2 m in a
60 m column, where `IrradianceAt` clamps to full surface irradiance — **reached by selection, not by
swimming**: born at the parent's depth, deep lineages die, shallow ones breed. A static gradient is
exhausted by sorting, and once sorted a muscle can only move you somewhere worse.

```
maximum prize from perfect depth control   ~0.13 W
cost of making one of two parts a muscle    2.22 W
```

**The entry fee is roughly seventeen times the prize.** No controller, no thrust model and no
actuator tuning can close that, which is why "joints go to zero" never yielded to behavioural
hypotheses.

## What D036 already said, and what changed

D036's third failure — *"in still water doing nothing is free and optimal... station-keeping is a
task with continuous returns from arbitrarily close to zero"* — is this same diagnosis, three days
earlier and sharper. Its fix, the current, was built (D037) and is running. Checking its three
failures against the live arms:

| D036 failure | then | now |
|---|---|---|
| detritus stranded on the floor | 77.5% | **2.5%** |
| nutrient density where creatures live | 0 | **9.05 J/m³** |
| displacement from birth | 6 mm | **1.34 m** |
| swimming's share of that | ~0.03% | **work = 0** |

Two of three are fixed. The denominator moved; the numerator did not. Position is no longer
destiny — but it is the water that moves creatures, not the creatures.

## The change, and two mistakes caught on the way

`LinkCell.PhotosyntheticEfficiency`, defaulting to **0**, so §5A.1 is unchanged and every earlier
number stands (D043). Two arms are in flight against `g-c1.0-s1`, one variable each: `linkearn` at
0.5, and `daynight` at amplitude 1 — D035's cycle, retried in the world D037 finally gave it.

Both mistakes were caught by guards rather than by reading the code, which is the only reason
they are cheap enough to be worth recording:

- **The knob read 20× high.** `PhotosyntheticEfficiency` was written as an absolute capture
  fraction, so 1.0 meant *all incident light* while a leaf captures 0.05 — a jointed creature
  priced at **39.5 W** against a plant's 1.92 W. The test asserting "a fully photosynthetic link
  must still lose to two green parts" failed on the first run and was written for exactly that.
  It is now a fraction *of* `PhotosyntheticCell.DefaultEfficiency`.
- **The first launch was not a controlled comparison.** Both arms were started with only the
  variable under test set, and defaults supplied `current 0`, `mixing 0`, `senescence off`,
  `maxPower 120` and `48 W/m²` against a control running `0.05`, `2`, `3000 s`, `20` and `64`.
  With mixing off, detritus strands on the floor again — so `daynight` would have reproduced
  D035's original failure exactly, for ten hours of CPU, and looked like a replication.
  Caught by diffing the header each run writes about itself, which is what that header is for.

`scripts/run-arm.ps1` now holds the invocation, because every arm before these was launched by hand
and none of them is reproducible from anything but a shell history.

## The arm answered within four hours, and said no

`linkearn` ran at a fraction of 0.5. **Link tissue was gone from the population by t=3,000** — not
the joints, the cell type; the survivor snapshots read `photo=100%` from there on, at generation 36.

The pricing says why:

```
two photosynthetic parts, no joint :  1.9239 W
one part + 20 N.m hinge, photo 0.50:  0.6999 W   solvent — and 36% of the plant
one part + 20 N.m hinge, photo 1.00:  1.6954 W   solvent — and 88% of the plant
```

**Solvency was never the bar.** §5A.6 pays for offspring out of surplus, so a creature banking at
36% of its neighbours' rate is outbred and gone while being comfortably alive. This entry opened by
recording that the two joint probes were break-even arms. The arm written in response to that
finding, in the same entry, on the same day, **was the fourth one.**

And the ceiling is structural: at fraction 1.0 — muscle as good at light as a leaf — a joint still
reaches only 88% of a plant, because the idle charge does not scale away. The test guarding the
change asserts that domination explicitly. **The arm's own guard guaranteed it could not succeed**,
and it was written before the arm was launched.

`daynight` closed the same way from the other side: amplitude 1 with `linkPhoto` at 0, so its joints
were insolvent at −0.30 W and died by t=5,000 having never been affordable. Affordability and a
motive were run as two arms when they are one experiment.

**What is now established is a requirement, which is worth more than either arm was:** movement must
be worth **more than 12% of a creature's income**, because that is what the best possible muscle
still gives away. It is presently worth about 7% and realised at 0%. The cost side is closed — it is
bounded and cannot reach — so every remaining option is on the prize side.

`linkday` (fraction 1.0, amplitude 1) is the first run in this project's history where a joint is
both affordable and has something to buy. **It failed too**: joints reached 0% by t=5,000 and link
tissue left the population, the same shape and the same timing as every arm before it, at
generation 34.

⚠ **A founder transient was briefly recorded here as a first, and was not one.** `linkday` reports
`work 39.27 J/s` at t=300 — but the control reports **43.21 J/s at t=250**, and so does every arm,
because founders are drawn with joints and actuate until they die. Work goes to zero when the joints
do, in all of them. Nothing distinguished `linkday`'s opening from anyone else's. The monitor had a
gate for exactly this (`alive >= 500`, since the population floor is 40); the claim was made in
prose, where the gate does not apply.

⚠ **And then the gate itself failed, on `lit`.** It reported joints surviving reproduction at
t=700 with 1,775 alive. The counts:

```
t=100   alive=40    jointed=16   j%=40%
t=400   alive=113   jointed=10   j%=8.8%
t=700   alive=1775  jointed=7    j%=0.4%
```

The absolute count falls while the population grows forty-fold, so the share collapses from the
founder rate of ~40% to 0.4% — joints being purged as fast as ever. The gate used `alive >= 500` as
a proxy for "reproduction has had its say", and at 200 W/m² a population reaches 1,775 in 700 s with
its founder cohort still alive (44 deaths). **The proxy was calibrated on a world that grows slowly
and silently stopped meaning anything in a world that does not.** Keyed on joint *share* now, at a
tenth of the founder rate; a count is not a measurement when the denominator is moving.

## A joint is useful. It was never useless — it was never affordable

The question 0026 left open, and which every arm above silently assumed an answer to, took eleven
minutes to settle. `SwimSurvey` already measured it: 200 genomes, 20 s each, no gravity, no contact,
net vertical displacement signed toward the light. It had never been read as a thrust curve.

Best net rise, founder-shaped bodies (mean 1.51 parts, 0.95 dof — what evolution actually produces):

| joint capacity | best rise over 20 s | sustained vertical |
|---|---|---|
| 5 N·m | 0.16 m | 0.008 m/s |
| **20 N·m** | **0.34 m** | **0.017 m/s** |
| 60 N·m | 0.59 m | 0.029 m/s |
| 120 N·m | 1.05 m | 0.052 m/s |

**A hinge on a two-part body produces real directed thrust**, at the capacity the world already
uses. So "are joints useless?" is answered, and the answer is no. Every failure recorded in 0026 and
above is an affordability failure, and none of them was ever about swimming.

⚠ The **median** is 0.0000 m in every arm at every capacity. Only the best of 200 rises at all,
which is exactly right for unselected random brains — a founder is as likely to be wired to dive as
to rise — and it is the reason this is a capability measurement and not a fitness one. It bounds
what selection could find, from below. Blind and sensing arms are indistinguishable here, as
`SwimSurvey`'s own remarks predict for an unselected population.

**What it sets.** A sink rate must sit under what a joint can push against: 0.017 m/s at 20 N·m is
the ceiling, and because a partial swimmer sinks more slowly and collects a proportionate share of
the prize, the break-even crossing arrives at roughly half of it. That is D036's "continuous returns
from arbitrarily close to zero", and it is the first mechanism proposed here whose gradient starts
at zero rather than at a cliff.

## Something to lose by standing still

`FluidConfig.TissueExcessDensity` (D044), defaulting to 0 so §5.2 is untouched — the survey at 0
returns the earlier numbers to the digit. The sink rate was calibrated against the observable rather
than derived, and the derivation would have been wrong by tenfold: predicting from quadratic drag
gave 0.011 kg/m³ for 0.01 m/s, and the measurement gives **0.1 kg/m³**, linear, because bodies at
hundredths of a metre per second are not in the quadratic regime.

`sink` runs at 0.15 kg/m³ with muscle at full rate — **affordability and motive in one arm**, which
is the correction to running them as two. The margin was chosen deliberately wide: ~30% of lifetime
income lost by a non-swimmer against a muscle costing 12%, a ratio of 2.5, where 0.1 kg/m³ would
have given 1.75. Every arm this project has run at a ratio near 1 has failed, and today supplied
four more.

The opening rows are unlike any arm before them:

```
t=100   alive=40  jointed=15  meanHeight=-12.20  work=0.649  audit=-0.0003
t=1500  alive=40  jointed=5   meanHeight=-15.80  work=5.815  audit=-0.0005
t=2200  alive=40  jointed=11  meanHeight=-19.78  work=4.984  audit=-0.0017
```

Creatures are **sinking** — mean height near −20 m against −1.8 to +3.2 m at the surface everywhere
else — and doing **sustained mechanical work past the founder phase**, which nothing has done here
before. The audit holds at ~0.001%, so an external force has not opened a hole in the economy.
Whether any of it survives reproduction is the question, and it is not answerable yet: `alive` is
still 40, the population floor.

## The world is eight metres deep

`sink` ran its whole 40,000 s budget — fast, because the population never grew — and finished
**alive=40, generation 0**: every living creature a floor spawn, nothing ever bred. Mean lifetime
expenditure ran at **241 J against 87 J of income**, 2.7x. Work was never the problem; work share
stayed between 0.2% and 5.8%.

The measurement that explains it, and which should have preceded every arm today:

| depth | irradiance | net W |
|---|---|---|
| 0 m | 64.00 | +2.6460 |
| −8 m | 32.86 | +0.3571 |
| **−10 m** | **27.81** | **−0.0136** |
| −20 m | 12.09 | −1.1695 |

**The habitable band is eight metres.** Income falls exponentially and upkeep does not fall at all,
so below −10 m nothing solvent exists. `sink` put creatures at −19 m: it did not fail to make
swimming pay, it made the world uninhabitable, and a run reports both as a population pinned at the
floor.

Three things follow that were not obvious before:

- **The surface sorting was never a preference.** Mean height of +0.7 to +5.4 m across every arm is
  the top of an 8 m band, not a choice.
- **`FounderDepthSpread` is 20 m**, so a large share of founders in *every run this project has ever
  done* are born below −10 m — born dead.
- **The muscle question is capped structurally.** Eight metres is 0.67 attenuation lengths, so
  income can vary by at most 51% across the entire livable world, and a descent worth 27% would
  consume most of the band.

**Band depth is `12·ln(I_surface / 27.8)`** — 8 m at 64 W/m², 24 m at 200. And the reason irradiance
sits at 64 is runaway (§5A.2b): light covering upkeep so completely that nothing has to do anything.
That is precisely the condition a sink removes. **Irradiance and buoyancy have room together that
neither has alone**, which is the first thing today that looks like a way through rather than
another knob.

## Sinking compounds down a lineage, which the arithmetic did not

`sink-lit` — 200 W/m² for a 24 m band, 0.13 kg/m³ for an 8 m lifetime descent — is **habitable**,
and that much of the fix worked: mean income runs 300–440 J against 200–366 J of expenditure, so
creatures are solvent at −25 m where `sink` had them spending 2.7x what they earned at −19 m.

It still does not work, and the reason is an error in how the sink rate was sized. **Offspring are
born at their parent's depth.** A descent of 8 m per lifetime is therefore not a per-creature
handicap that resets each generation; it accumulates down the lineage — 24 m after three
generations, with no reset except swimming. Every rate in D044 was computed per lifetime, and the
quantity that matters is per lineage.

The visible consequence is not death but **a population too small to search**:

```
   t   alive  jointed  height  genMax
1000     113        1   -24.2      11
6400      40        0   -29.3       9
10900     40       15   -19.0       2
```

40–113 alive, oscillating, with lineages establishing and sinking out and floor spawns replacing
them. Whatever a joint would be worth here, a population of forty does not have the mutational
supply to find one — and `jointed` swinging 0–18 between samples is founder draw, not selection.

That is a third distinct failure mode for one mechanism, and worth naming separately from the other
two: `sink` was **energy-limited** (below the habitable band), `sink-lit` is **search-limited** (the
band is fine, the population is not). They report almost identically in the summary columns.

## Half of every founder is born dead

Following the band measurement to its consequence: `World.SpawnFounders` places a founder at
`-rng.Range(0, FounderDepthSpread)`, uniform over **20 m**, and its comment says *"through the lit
zone"*. The lit zone — where a creature is solvent — is **9.75 m** at 64 W/m². Measured:

```
irradiance 64 W/m2, habitable band 9.75 m, founder spread 20 m -> 49% of founders born solvent
```

**Fifty-one per cent of every floor spawn is born below break-even.** The spread was chosen for a
good reason — starting everything at the surface would hand generation zero the best light in the
world and make §5A.2's calibration read as more generous than it is — but 20 m was picked when
nobody knew where the bottom of the world was, and the comment describes a lit zone twice the size
of the real one. It halves the effective mutational supply of every run this project has performed,
which is the quantity that matters most when the open question is whether a rare variant can be
found at all.

**Not changed here.** [D036](../DECISIONS.md#d036) rejected shrinking the spread on the grounds that
it flattens the vertical structure §5A.4 exists to provide, and that argument survives this
measurement untouched — the fix, if there is one, is to derive the spread from the light model
rather than to pick a smaller constant. What is added is the invariant that would have caught it:
`MostFoundersAreBornSomewhereTheyCanLive` fails below a quarter, on the grounds that a floor mostly
manufacturing corpses makes every run's mutational supply a fiction.

Note this is irradiance-dependent, and the sink arms are not affected: at 200 W/m² the band is
about 24 m and a 20 m spread falls entirely inside it.

## A worker that was never re-synced

`sink-mid` came back with a config hash **byte-identical to `lit`'s** — `79da5b4c218176ba` — and a
header in the old format, with no `linkPhoto` and no `excessDensity` field at all. `unity-w2` had
never been re-synced after those variables were added to `EvolutionRun`; only workers 6 and 7 were.
A stale worker runs old code and says nothing about it, so the arm silently ignored the one
parameter it existed to test and would have read as a clean negative.

Two things follow:

- **`sink-mid` was a duplicate of `lit`.** Stopped, worker 2 re-synced, relaunched; the hashes are
  now `e85411dfd8969776` and `6588a8e7db4e0f1d` against `lit`'s, all distinct.
- **`lit` itself was not what it was described as.** It ran with `linkPhoto` 0 rather than 1. Its
  result stands — a bright world runs away without a sink — and it is arguably the cleaner
  brightness-only control for having no muscle change in it, but the earlier description of it was
  wrong.

CLAUDE.md's rule caught this exactly as written: *identical numbers across a configuration change
mean the change was not applied.* The rule's stated remedy — prove the parameter reached the thing
it configures — is what the run header is for, and it is now the third distinct failure it has
caught in a day, after silently-filled defaults and the founder-transient gate.

## The current drowns the signal the sink was added to create

Both sink arms settled at −12.7 to −12.9 m *regardless of sink rate*, which cannot be the sink
setting the depth. The displacement columns say what is:

```
sink-slow (0.02)   meanRise 3.8-5.1 m per lifetime, mean age ~400 s
                   sink at 0.0018 m/s over 400 s accounts for 0.7 m of it
sink-mid  (0.05)   meanRise 25.9 m at t=2,200, depth -40.9 m, then an oscillating recovery
```

**The current moves bodies 18–28x faster than they sink.** `CurrentField` runs at 0.05 m/s against
sink rates of 0.0018–0.0045, so buoyancy contributes under a fifth of a creature's vertical
displacement and the rest is stirring. Selection cannot see a signal that small against that noise —
which is [D036](../DECISIONS.md#d036)'s second failure exactly, *"position is inherited and
effectively immutable... swimming accounts for 0.03% of the variance"*, restated with the current
itself as the noise source rather than founder scatter.

The irony is worth stating plainly: **the current was added to make position mutable, and it is now
what makes the mechanism that properly mutates position unreadable.**

[D037](../DECISIONS.md#d037) separates the two jobs — *"`CurrentField` advects bodies;
`NutrientField.Mix` diffuses detritus. One cannot do both jobs"* — so the current can be switched off
without stranding energy on the floor, which is the failure it was built for. With buoyancy
supplying both a gradient and a reason to move, body advection is no longer doing useful work and is
purely a noise source. `sink-still` runs the pair that has never been tried: **still water, and
tissue that sinks.** §5.2's *"depth changes only by swimming"* becomes true again, and for the first
time there is a reason to.

## What is still open

`stats.jsonl` and `lineage.jsonl` were empty in every run to date — `RunDirectory` opened both
writers and nothing ever called them, so the record survived only as markdown and genome snapshots.
`stats.jsonl` is now written from the same locals as the markdown row, so the two cannot drift.
`lineage.jsonl` is still unwritten, deliberately: §9 wants one row per creature ever born, and at the
observed birth rate that is hundreds of megabytes an hour for ancestry nothing currently reads.
