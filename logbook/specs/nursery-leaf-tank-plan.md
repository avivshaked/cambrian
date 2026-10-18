# The swimming nursery's leaf tank: the build plan

*2026-10-12, by the nursery room's builder (evolution-simulator-25, a8), for the coordinator. It plans the nursery side
of D166 and D167: a tank whose only food is living leaves, where a hand-built mouthed swimmer's brain is bred under
two forms of the leaf sense. Nothing in it is built. Branch `nursery-leaf`, in `scratch/wt-nursery-leaf`, off
`r59-anchor`. It moves onto that branch's commit "Mouth intake per body" when it lands, after step 2b's: the leaf sense,
the no-corpse setting, the turning drag and the mouth's count are 08's world code there. File and line references are read from `r59-anchor` at
`2da6d9f0` and its uncommitted 2b work, by a read-only survey of the same day. The times are guesses.*

## The tank D167 asks for

- Living leaves drawn from a round 58 world, alive on light and on the water's spent units, with births held.
- No marine snow, no carrion at the start, and no animal but the swimmers.
- No corpse from any death but a bite, so the only food is a leaf bitten to death in the tank (2b's
  `UnbittenDeathsLeaveNoCorpse`).
- A hand-built swimmer: a mouth, one limb on one joint, no absorptive and no photosynthetic cell.
- Two arms on the same body: the leaf sense as strength only, and as strength with direction (2b's `LeafScent`).
- The turning drag on (step 1's `TurningDrag`).

## What exists and what does not

The survey found these, which set the plan's size.

- **No code places a living plant, or any second kind of body, in a tank.** The only `Inoculate` in the nursery is
  over the bred genomes (`Tank.cs:193`). `Inoculate` takes a height, and the world picks x and z.
- **No record holds a body's mouth intake.** `WorldMouth.ApplyIntake` adds a meal straight to `Energy`
  (`WorldMouth.cs:507-508` on the branch) and counts only the world's `UnitsEaten`. The nursery's `Food` is snow alone,
  and a meal shows only inside `Net`.
- **No code extracts a run's living leaves with their genomes.** Snapshots hold ids, and positions live in
  `positions.jsonl.gz`, flag 4 for photosynthetic. The one leaf genome on disk, `scratch/grazing-fable/r58s1j-median-leaf.json`,
  was pulled by hand.
- **A recipe already says "no snow" and "no carrion"** (`"snow": null`, `"carrion": {"count": 0, ...}`), and a pool of
  hand-built bodies (`"source": "pool"`). Births are held for every body in the world (`World.cs:4507`).

## The steps

### 1. The leaf pool

**What.** A script reads `r58-s3j` at one second of a year-8 summer, chosen from the light's phase in its config, and
writes its living leaves into a pool file the recipe names. A leaf is a living body with photosynthetic tissue and no
absorptive tissue, the `positions-read.py` guild of that name. The leaves are the run's own winners, taken in a depth
band around their median depth. Each row holds the body's id, its depth and its genome.

The same read gives the three numbers the tank copies from the run:
- the leaves' density per cubic metre in the band, which sets how many a tank holds;
- the spent units per cubic metre the water held in the band at that second, read from the run's `fields/`;
- each leaf's state at the second, so the tank can start it grown.

The pool and a short record of the three numbers are committed beside the nursery's recipes, so a run never reads a
run directory, which is gitignored.

**Files.**
- `scripts/reads/leaf-pool.py`, joining `positions.jsonl.gz` (the sample nearest the second) to `genomes.jsonl.gz` by id
  through `scripts/reads/runrec.py`'s readers
- `rounds/nursery/pools/leaves-<run>-<second>.tsv`, with a line in `rounds/nursery/configs/README.md`'s manner saying
  what made it

**Tests prove:** the script's own check, run on a few rows cut from the real files: every row is alive at that second,
photosynthetic and not absorptive, and its genome reads with `GenomeJson`. The nursery's pool reader reads the file.

**Time:** about half a day. **Machine:** Python on one core reading 260 MB of positions, about ten minutes, no dotnet.

### 2. Leaves in the tank

**What.** A new recipe section, `leaves`, which may be absent or null where a recipe has none. An absent section writes
nothing into the canonical text, so every recipe written before it keeps its hash. It names the pool, how many leaves a
tank holds, and the spent units the water starts with. Each episode draws the leaves from the pool, and their x and z
inside the glass, from its seed. Each leaf goes at its recorded depth. The tank lays the water's spent units, then
the leaves, then the bred bodies. Each leaf is paid its growth owed, so it starts grown, as it stood in the snapshot.
The leaves are not members: they have no score and no exam row. A recipe with `leaves` set refuses bred bodies that
carry absorptive or photosynthetic tissue, so a wrong pool cannot break the tank's single food.

Placing a leaf at the recipe's x and z needs one Core call, `World.InoculateAt(genome, point)`, since `Inoculate`
leaves x and z to the world. It refuses a point outside the water and never moves a body. Laying spent units needs a
second, `World.LaySpentMatter`, beside `LaySnow` in `WorldAssay.cs`, booked as influx.

As built, each leaf is born at its genome's own birth fraction and then paid its growth owed, as the bred bodies'
purse does, since Core has no call to start a body at a stated fraction. So the config's newborn mass floor must pass
a leaf at its birth fraction. Step 5 keeps round 58's own floor, which bred these leaves.

**Files.** `src/Evosim.Nursery/Recipe.cs` (the section, its refusals, `Canonical`), `Program.cs` (`Options`,
`ApplyRecipe`), `Tank.cs` (laying the water and the leaves, keeping the leaves out of the members), and the restep,
which reuses `TankEpisode` and so inherits it. In Core, `WorldAssay.cs` (the two calls), with their tests.

**Tests prove:**
- the section reads back to itself and moves the hash, and each fault is refused with its reason;
- a tank holds the bred bodies, the leaves and nothing else at the start: no snow, no corpse;
- the leaves are the same for one seed and differ between seeds;
- a leaf killed by anything but a bite leaves no corpse in the tank, through 2b's setting;
- the episode is identical at 1 and 4 threads, and a restep of it is EXACT.

**Time:** about a day.

### 3. What a mouth eats

**What.** A per-body count of the joules a mouth took in, net of the waste returned to the water, added in
`ApplyIntake` beside `Energy`. It goes in the nursery's records as a measure, `eaten`, beside `food` and `net`. It is
also a reading in every world, since today a meal is invisible.

This is a Core change, and the world's state must carry the count for a resume to be exact. 08 builds it on
`r59-anchor` as `Organism.MouthJoules`, in its own commit after 2b's and inside 2b's `StateVersion` 16. Its tests show
that the bodies' counts sum to the world's intake less the waste, that a mouthless world's digest is unchanged, and
that a checkpoint carries the count. This branch moves onto that commit. Version 16 grows again at 08's step 5 with the grip,
so no checkpoint written here before step 5 is one to keep. The nursery's side is this step's.

The score is the owner's: `eaten`, or today's `Net` (question 1). Either is a recipe line, since the reward
`WeightedSum` already takes any measure by name: `{"eaten": 1}`.

**Files.** In the nursery, `Reward.cs` (`EpisodeRecord`'s measures and JSON) and `Tank.cs` (`Resident`, `RecordOf`).

**Tests prove:** the nursery's records round-trip with `eaten`; an episode's `eaten` is the body's `MouthJoules` over
the episode; a restep compares it bit for bit.

**Time:** about a quarter of a day. The Core `-All` is 08's.

### 4. The swimmer

**What.** A hand-built genome in `inocula/leaf-tank/`, written as the grazer control's plates are
(`logbook/specs/grazer-control-spec.md`). It goes into a one-row pool, and eight swimmers share a tank, as in brain2.
`logbook/specs/d135-steering-checks.md` on `r59-anchor` shows that one joint can steer. A universal joint for depth is a
later arm.

**The numbers I propose**, for the coordinator to read. Every one is a guess until the plate's costs are computed, as
the grazer spec computed its own, and the drag check has watched it swim.
- **The root:** a consumer box, half-extents 0.3 by 0.15 by 0.3 m (0.11 m³, under round 58's 0.25 m³ neutral volume),
  with attack 1 and intake 1 from the start, as `grazer-plate.json`.
- **The limb:** a link capsule 0.52 m long and 0.16 m thick, on a hinge whose axis is vertical, so its bend swings in
  the horizontal plane. A capsule's long axis is its y, so its half-extents are 0.08 by 0.26 by 0.08 m: round 58's
  link limit, so that a brain bred here drives a body that world's own genome bounds allow (the coordinator,
  2026-10-13). Limits ±1.0 rad,
  inside the genome's 1.4. Drive power mid-range, at 10 W of the genome's 5 to 20, and threshold 0.1.
- **Breeding:** margin 600 s and investment 1.3, the grazer plates' values, so a swimmer never breeds unfed, with the
  other breeding genes from the plates' source genome. Births are held in the tank in any case.
- **The purse:** standing watts times one and a half episodes, so a swimmer outlives the episode unfed and a slow
  search still sees every brain swim to the end.

**Tests prove:** the plate grows to two parts and one degree of freedom, has its mouth, and has no absorptive or
photosynthetic tissue; the recipe accepts it, and refuses it with a photosynthetic part added.

**As built.** The plate is `inocula/leaf-tank/swimmer.json` (sha256 `3a937a06…6ab22` over its LF bytes), in the pool
`rounds/nursery/pools/6a27d8acada28e03.tsv`. The hinge is the build's `Hinge`, its edge turned a quarter about x so that
the child's bending axis stands vertical, as in d135's bent fin. Under r58-s3j's config the plate, with its first 0.3 m
limb, stood at 0.911 W with 54.5 J of tissue; it is planted whole, so it owes no growth. The purse is 1.5 episodes at
standing watts plus the mean work the drag check measures, so a search is never cut short by starving (the
coordinator, 2026-10-13).

**Time:** about a quarter of a day.

### 5. The world config and the two recipes

**What.** One world config for the leaf tank: `r58-s3j`'s own config, re-recorded on this build, so the leaves' cell
types and genes mean what they meant there. The nursery's overrides make its floor flat at 25 m, with no tilt and no
shore, as brain2's world, so the whole band is water. The config's own seeded matter is zeroed, so the water starts
with the recipe's spent units alone: the band's 0.0010 units per m³, as the coordinator ruled on 2026-10-12. The two arms are two recipes that differ only in the `LeafScent` override
(`Strength`, `Direction`). Their overrides set no influx of snow, the no-corpse setting on, and the turning drag at the
form and size the drag check (step 6) sets. The leaf sense keeps its defaults, 10 m, 2 m and 1 m², unless the smoke
shows the strength reading flat at 0 or 1 across the tank. Then the half-scale moves, and the record says so.

**As built so far.** The config is `rounds/nursery/configs/3fd98985cdf68008.json`: r58-s3j's own, with the sixteen keys
round 59's build added inserted as text at their defaults, as 08's step 6 made `3f74be1aae2421fe.json`. One set of
overrides serves the drag check and the arms: an area of 1,600 m²; tilt, shore and shore fade 0; no seeded matter
(`InitialMatterPerCubicMetre` and `MatterBudgetUnits` 0), and so no matter islands, which Core refuses without a
budget; no founding trickle, which the nursery refuses; `UnbittenDeathsLeaveNoCorpse` on; and no reef. Reefs belong to
the gripping track, and in a 40 m tank a reef would only add shade and a place for leaves to pile (the coordinator,
2026-10-13). The relief stays round 58's 1.5 m, as in brain2's world. `scripts/reads/leaf-recipes.py` wrote the
recipes from that one set.

**The tank and the episode.** The tank is 40 by 40 m, as brain2's world, so from a start in its middle about four
fifths of it lies beyond one leaf's 10 m cone. The episode's length follows from the leaf density step 1 reads. A
swimmer sweeping a path of width 2r at speed v meets about σ·2r·v·T leaves in T seconds, where σ is the leaves per
square metre of the band and r the mouth's reach plus a leaf's half-length. I propose the T at which a straight stroker
meets two, with v read in the drag check, and bounded to between 600 and 1,800 s. The number goes to the coordinator
with step 1's read.

**Coordination with 08's step 6.** Step 6 re-records the nursery's three existing configs and their recipes once for
every new tunable. The leaf config is added after it, on the same build, so it is recorded once. If my branch is ready
first, I record the leaf config alone, and step 6 leaves it as it is.

**Exam.** The controls are own, brainless, and a knockout of the leaf sense's channels: `LeafScent` in the strength
arm, and `LeafScent` with `LeafScentDirection` in the direction arm. So the exam shows whether the sense is used. The
knockout of a new channel must be accepted by name, which a test proves.

**Time:** about half a day.

### 6. The drag check (D161)

**What.** D161's own check, before the leaf arms: does the turning drag still the rocking without stopping the
strokes? A tank with no leaves holds single stomachs, round 58's one-part absorptive bodies, and the swimmer of step 4
under a constant stroke. It runs five times: drag off, skin at two sizes and spin at two sizes. Each body is read for
its rocking, the mean of its root's angular speed, and for its path. The nursery records path today. The rocking is a
new measure, `turn`, beside it, built with step 3: the root link's angular speed from the solver's state, summed at
each economy step while the body lives.

`path` jumps on the first economy step: before the solver reports, a body's x and z are its patch's centre, so the first
step adds the distance to where the solver put it (13.4 m in the test tank, and in the recorded fixture). The check
reads the swimmer's speed from the second step on, and leaves `path` as recorded. Starting `path` at the second step
is a later fix (below), since it moves every recorded path.

The form and the size that still the rocking and keep the stroke go to the coordinator with the numbers, and the leaf
arms then use them. The swimmer's speed under that drag gives step 5 its v.

**Files.** `Reward.cs` and `Tank.cs` (the measure), a recipe for each condition, and a short read script in
`scripts/reads/`.

**As built.** The pool is `rounds/nursery/pools/a326093a77658bdc.tsv`, written by `scripts/reads/drag-pool.py`: the
swimmer as bodies 0 and 1, and as bodies 2 to 7 six of the 53 single stomachs r58-s3j held at 44,000 s, taken evenly by
id. The recipes are `rounds/nursery/leaf-drag-off.json`, `-skin-0p05`, `-skin-0p5`, `-spin-0p05` and `-spin-0p5`: one
tank of the eight for 300 s, under each body's own drawn brain and under a constant power of 0.5. The sizes are 0.05,
the steering bench's, and ten times it (`d135-steering-checks.md`). `scripts/reads/drag-read.py` reads a restep of
each bench's constant-power draw at 10 Hz. It takes the swimmers' speed from the second frame on, and each body's spin
from its root's rotation between frames.

The first read: with the drag off, two of the six stomachs spin steadily at 1.35 and 2.52 rad/s. Either form at 0.05
brings them to 0.20 to 0.25 rad/s, and at 0.5 to 0.04. The skin form at 0.5 also brakes sliding; the spin form leaves
every path as it was. That pass started the bodies 0.5 m above the bed, as brain2 does, more than 4 m below the
leaves' band. The swimmer did not sink there: it is under round 58's neutral volume, and it stayed at the height it
started at. So the bred bodies now start in the band, `aboveBed` 11.5 (the coordinator, 2026-10-13), and the second
pass swam there (both passes in `nursery-leaf-drag-check.txt`). The swimmers' speeds are the same under every drag, 0.17
and 0.08 m/s for the two of them, and the spin form at 0.5 brings the two spinning stomachs to 0.04 rad/s. The water
in the band moves: the stomachs, which have no drive, travel 0.08 to 0.13 m/s and rise 5 to 10 m in 300 s. The
coordinator chose the spin form at 0.5 for now; the size goes into round 59's pre-registration for the owner.

The ledger read 0 for the swimmer's work and upkeep until main's 294c08a5 came onto this branch. The world writes a
body's last ledger only where it has absorptive tissue, and the swimmer has none.

**Tests prove:** a body spun and then left alone shows `turn` falling, and a body at rest reads 0.

**Time:** about half a day. **Machine:** five short runs on my slot.

### 7. A smoke run

**What.** Each arm for one generation of a small search, on the CPU, read for these:
- the leaves live through an episode on the band's spent matter, and do not starve. If they do, the coordinator hears
  it before anything else is chosen;
- a non-bite death leaves no corpse, and a swimmer's `eaten` is above 0 when it bites;
- the run is identical at 1 and 4 threads;
- the leaf sense at a 10 m and at a 3 m range: the 10th and 90th percentiles of the strength at the swimmers'
  positions, and the share of samples where the direction points within 45 degrees of the nearest leaf. At 0.0127
  leaves per m³ the mean spacing is about 4.3 m, so a 10 m cone overlaps dozens, and the strength may read flat. The
  coordinator chooses the range from these.

The smoke also times a generation, which sizes the real runs.

**As built.** The smoke's recipes are `rounds/nursery/leaf-strength-smoke.json`, `leaf-direction-smoke.json` and
`leaf-direction-smoke-r3.json`, written by `scripts/reads/leaf-recipes.py`: episodes of 1,800 s, a purse of 3,830 s of
standing watts, the spin form at 0.5, scored on `eaten` with Net beside it (the coordinator, 2026-10-13). The smell is
read by `LeafSenseProbe`, a watcher that runs only when `EVOSIM_LEAF_SENSE` names its file. It samples the world's
field at each swimmer's root every 5 s, whatever the brain reads, and its runs are byte for byte the runs without it.
`scripts/reads/leaf-smoke-read.py` reads it.

The first read, in `nursery-leaf-smoke.txt`: at 10 m the strength is near-flat, 0.86 to 0.96 from the 10th to the
90th percentile, and at 3 m it spreads from 0 to 0.45, with the gradient within 45 degrees of the nearest leaf in three
samples of four. A third or more of the leaves die in an episode: about half of age, under round 58's hazard with births
held, and most of the rest to the swimmers' bites, since one bite kills a one-part leaf whole. The trace's corpse census
found every corpse a leaf's, so `eaten` counts leaf meat alone, and the swimmers ate about a tenth of what they killed.
In the band's spent matter about one leaf in six also starved; at r58-s3j's whole-water mean, one or two in sixty. A 40 m tank of 250 leaves
costs about 6.5 minutes a generation of four tanks on four threads, and a 20 m tank of 62 about 1.8. The tank's size,
the range, the age hazard in the tank and the spent matter went to the coordinator, and the score and the corpse rule
to the owner.

**Time:** about a quarter of a day. **Machine:** two runs of a few minutes each on my slot.

### 8. The arms

The owner's rulings of 2026-10-13, through the coordinator (D169, D170):
- the age hazard is off in the tank. Core refuses the three ageing settings beside a hazard that is off, so all four
  keys are 0;
- only a leaf bitten to death leaves food, and the score is `eaten`, the energy a mouth takes from such leaves, with
  `Net` beside it;
- two arms of 30 generations, population 48, 8 draws, side by side on two groups of four cores.

The coordinator chose the rest. The tank is 20 m across and holds 62 leaves, the band's density, smelt to 3 m. Its
water holds r58-s3j's whole-water spent matter at 44,000 s, 0.004237 units per m³, so that the leaves do not starve.
The search keeps 8 elites and draws by a tournament of 3, eight swimmers share a tank, and there is no reef. `scripts/reads/leaf-recipes.py arms` writes
`rounds/nursery/leaf-strength.json` and `leaf-direction.json`. Each episode record carries the swimmer's kills, the
bodies it killed whole, and its meals, the steps in which its mouth took something in, so the generation summary shows
them beside the score.

**The corpse rule, widened.** With `UnbittenDeathsLeaveNoCorpse` on, a bite's kill leaves food only when the victim
carries photosynthetic tissue. Every other death burns, and so does a part bitten off a body without it. A bite kills
cells, and a body dies only when it cannot bear the loss (mouth spec, rules 3 and 4), so a part bitten off a leaf of
several parts is food and the leaf lives on. A bite on a one-part leaf kills it whole. Four tests in `MouthTests` hold the four cases: a leaf, a swimmer, a part of each.
The two swimmer tests were not run against the old guards to see them fail. Putting the old guards back for one run
was an in-place edit of two Core files, and the tool's permission check refused it, so the back-check rests on reading
the asserts: under the old guards a bite's kill always left a corpse, and both tests assert there is none.

At 44,000 s, between 16 and 30 percent of the living leaves in r58-s3j, r58-s1j and r58-s1ir grow to more than one
part, and 16 percent of the band the tank draws from, about ten leaves in a tank of 62. These are adult part counts,
grown from each genome with the tank's config. A leaf that had lost a part to a bite still counts as whole, since no
record keeps the loss.

**The first launch failed, and the leaves are now placed largest first.** The arms started at 14:12 on 2026-10-13 from
`f7da2acf`, and both stopped about three minutes later in generation 0, draw 3. The tank found no place for leaf 173353,
whose bounding sphere has a radius of 2.47 m. The pool's leaves are larger than the plan had assumed: the median radius
is 1.24 m, 88 percent are over 1 m and six are over 2 m. Each draw's 62 leaves cover about 300 to 350 m² of the tank's
400 m². A leaf keeps its recorded height, and only its x and z are drawn, 64 times at most, in the order the leaves were
drawn, so a large leaf drawn late could find no room. The `--check` of the first launch had read only the hashes.

A draw's leaves follow its seed alone, so every tank the run lays is known before it starts: 30 generations of 8 draws
and the exam's 32 episodes, 272 in all. `--check` now lays all of them as an episode does, leaves and then the tank's
bred bodies, and fails if any leaf or body finds no place. It takes about two seconds. In the order drawn, 5 of the 272
failed. With 512 tries, 1 still failed. Placed largest first, with 64 tries, all 272 fit and no bred body was lost. The
coordinator chose largest first on 2026-10-13. Every draw keeps the same 62 leaves at the same heights, so the mix is
untouched, and only where each leaf stands in x and z moves. No test pinned a leaf layout, so nothing was re-recorded.
The smoke's runs were laid in the old order and name their build.

That check stopped one generation short. The search swims generations 0 to 30 and then the exam, so a run has 31
generations of draws and 280 seeds, and the check's loop had left generation 30's eight out. With the bound fixed, all
280 fit on the largest-first build, so the running arms cannot lose a leaf at their last generation.

**The read.** Both arms completed on the evening of 2026-10-13, and `logbook/specs/nursery-leaf-read.txt` holds every
number below. Eating per swimmer, averaged over five generations, rose from 60 to about 230 J in the strength arm and
was flat from generation 13. The direction arm rose from 54 to 205 J and was still slowing at 30. The arms' own exam
swims one genome alone in a tank, because the exam takes each body's best and there is one body. Its numbers do not
compare with the search's tanks of eight.

Each arm's 31 saved tops were benched eight to a tank on held-out seeds (`scripts/reads/tops-pool.py`,
`leaf-recipes.py tops`, `scripts/reads/tops-bench-read.py`). With no brain a top eats 42 to 45 J, against 214 J
(strength) and 156 J (direction) under its own. With the leaf smell knocked out, strength's tops lose about 14 J and
direction's late tops lose nothing. The best tops on held-out seeds are not the highest search scores. Strength's tops
stop improving at about generation 9, and direction's keep improving to generations 20 to 30.

A re-step of one tank of each bench, with the trace on, reproduces every recorded net. Its corpse census counts 33 and
19 leaves bitten to death, holding 8,570 and 4,096 J. The mouths kept 28 and 38 percent of that, after the intake's
waste. The tank's food is therefore not what limits the eating. A leaf's corpse holds five to six times what the
leaf was worth when planted, so the leaves earn a good deal of light during the episode. Two and four of the eight swimmers
died of bites, in pairs at the same second, and their corpses burned.

### 9. The stop and the exam of the tops (D172)

The owner ruled on 2026-10-13 that a tank search stops on a stalled curve. The exam then swims every generation's top
beside the last generation's elites, each under all three brains. Both are optional in a recipe, and absent means the
rule before. Every recipe written earlier therefore reads, hashes and reruns as it did.
- `search.stop`, `{"measure": "eaten", "window": 5, "gain": 0.05, "over": 10, "floor": 20}`, with
  `search.generations` as the cap. After generation g, the population's mean eaten is averaged over g − 4 to g. The
  search stops when g is 20 or more and that average has gained no more than 5 percent of where it stood ten
  generations before. It always stops after the cap. A cap of 60 swims generations 0 to 60, as a count of 30 swims 0
  to 30 (the coordinator, 2026-10-13). An average at or below 0 ten generations back gives no share, so the rule does
  not stop on it. Each generation row carries the rule's reading, `stop: {smoothed, gain, stop}`.
- `exam.pool: "tops"`. The exam swims each generation's top, as `tank-best.jsonl` holds it, then the elites the loop
  would carry over, each genome once in the order it came. `tank-exam-pool.jsonl` gives, for each, the generations it
  topped, whether it was an elite, and every id it had.

`SearchEnd.cs` holds both rules as pure functions, which `SearchEndTests` tests on flat, climbing, falling and
jumping curves, a gain at the share, and a missing generation. A smoke of four generations stopped on the rule
and merged a top with its carried copy. `scripts/reads/tank-side-by-side.py` sets arms side by side at equal
generations. At up to 61 tops and 8 elites, the exam is about 830 tanks, about 2.3 hours an arm on four threads.

### 10. The bench of the tops

`scripts/reads/tops-pool.py` makes each finished arm's 31 saved tops into a pool, and `leaf-recipes.py tops` writes a
bench of them: the arm's world, eight to a tank, under the arm's three brains, on 32 episodes of the held-out salt
`0x686f6c64`. Both benches ran on 2026-10-13 from `83c7097c`, about an hour each on four threads, and step 8's read
gives what they found.

### 11. The second pair (D173)

The owner ruled on the evening of 2026-10-13 that both forms of the smell run again in a sparser tank with a longer
reach. `scripts/reads/leaf-recipes.py sparse` writes `rounds/nursery/leaf-strength-sparse.json` and
`leaf-direction-sparse.json`. Each is its first-pair recipe with four changes:
- 31 leaves to a tank in place of 62. They come from the same pool by the same partial shuffle, which takes the first
  31 of the rows the first pair's draw took, and they are placed largest first.
- The smell's reach is 6 m in place of 3 m. `World` hands `LeafScentRangeMetres` to the leaf field as it builds it,
  and the run record's episode config shows the value the world was built with.
- The search stops on D172's rule, with a cap of 60 generations.
- The exam swims the tops and the last elites under the same three brains.
The seed, the salts, the purse and the water's spent matter are the first pair's.

**The read.** Both arms ran from 21:39 on 2026-10-13, on 8 cores each at the owner's word. At 22:39 the CPU had
reached 92 C, so the coordinator asked for 4 cores each, and the running processes were narrowed rather than restarted.
The results do not depend on the cores. The rule stopped strength after generation 32 and direction after 33, both
just after midnight. `logbook/specs/nursery-leaf-sparse-read.txt` holds the numbers, written by
`scripts/reads/tank-side-by-side.py` and `scripts/reads/tops-exam-read.py`.

Direction overtook strength at about generation 11, and its eating per swimmer, averaged over five generations,
ended at 323 J against 192 J. In the exam, the leaf smell knocked out costs direction's genomes 92 J of 261, about
a third. It costs strength's 26 J of 176, about 15 percent. Direction's cost appears in the tops from generation 10 on
and not in the first ten. A re-step of one exam tank, with the trace on, reproduces every recorded net. Its census
counts 18 of 31 leaves bitten to death, holding 7,487 J, of which the mouths kept 51 percent. No swimmer died in it.

The pair changed the leaf count and the reach together, so it does not say which of the two made the nose pay. A pair
at 31 leaves and 3 m would separate them. The read file estimates its wall time: about 2 to 3.6 hours on 8 cores
each, and about 3.6 to 6.6 hours on 4.

### 12. The reach alone (D174)

The owner approved round 59's plan on 2026-10-14 (D174). Before the worlds take the direction form at 6 m, a third
arm separates the reach from the leaf count. `leaf-recipes.py sparse3` writes `rounds/nursery/leaf-direction-sparse3.json`,
which is `leaf-direction-sparse` with the reach at 3 m and nothing else changed. Its episode config is therefore the
first direction arm's, and differs from the 6 m arm's in the reach alone, by hash. It runs alone on logical 24-31 at
8 threads. Read against `leaf-direction-sparse`, it says whether 3 m pays as well. The worlds keep 6 m unless it does
and 6 m costs the card's pace.

**The read.** The arm ran from 12:59 to 14:47 on 2026-10-14 and stopped on the rule after generation 28.
`logbook/specs/nursery-leaf-sparse3-read.txt` holds the numbers. Its eating per swimmer, averaged over five
generations, ended at 165 J, against 323 J at 6 m. In its exam the smell knocked out costs 6.9 J of 159, with a
standard error of 10.8, against 92 J of 261 at 6 m. With the leaf count held at 31, the reach is what made the nose
pay, so the worlds keep 6 m. On the CPU, the two reaches ran at the same pace.

### 13. Bodies with brains (D178)

The owner ruled on 2026-10-14 that the nursery may evolve bodies as well as brains, scored on the energy a body nets
(D178, which amends D133). The build is on branch `nursery-d178`, cut from `nursery-d172` with main merged in. It adds
three settings to the recipe. Each is optional, and a recipe that leaves it out reads and hashes as it did before.
- **`bodies.brains: "kept"`** starts a search from a pool's genomes with their own brains. Without it, each member draws
  a fresh brain on the pool's body.
- **The reward `NetEnergy`** scores what the mouths kept, less the standing cost, the stroke's work and the cost of
  pumping the water the mouths drew (`eaten − upkeep − work − handling`). The standing cost is the ledger's
  whole upkeep: the tissue, the attack and intake prices, the buoyancy offset and the support. Its one parameter,
  `chargeDeathToEnd`, set true, charges a body that died its upkeep to the episode's end, at the rate it paid while
  alive. Without the charge, a body that eats less than it costs would score better the sooner it died. A body that died
  before paying any upkeep has no rate to carry on, so its episode scores NaN, which ranks last.
- **`search.stop.measure: "reward"`** makes D172's rule read the population's mean reward, each episode scored alone, in
  place of its mean eating (the coordinator, 2026-10-14). Once a body can shrink, its score can rise without it eating
  more. The rule now takes the gain as a share of the size of where the curve stood, because a reward net of costs can
  stand below zero. On eating, which never does, that is the share D172 ruled.

The body is mutated by `search.evolve: "kind"`, which was already in the nursery. A child is the world's
`Mutator.Mutate` of the whole genome. It must keep its parent's set of cell types, so the swimmer cannot grow
photosynthetic or absorptive tissue beside the leaves. Every node's three dimensions and the genome's adult scale can
move, so the swimmer can shrink. Attack and intake move between 0 and the cell's maximum of 1, where the tops hold them.

The brain follows the body as it does at a birth in the world (`World.Conceive`): it is wired to the new body
(`CentralWiring.Effective`), then mutated. A link survives while the port it comes from exists. A change of size or of
attributes keeps every port. A lost part takes its joint's output and the links from its ports with it. A joint the body
gains copies the nearest joint's bias and links, its own ports in place of the neighbour's, so a duplicated segment
comes with a duplicated controller (D019). Crossover needs both parents on one body, which no longer holds once
bodies part, so this arm has none.

D177's prices reach the nursery through its own config. `rounds/nursery/configs/e6bdb9c74cb48742.json` is
`3fd98985cdf68008.json` with three values changed. The consumer cell's upkeep is 2 W/m³ in place of 6, and attack and
intake cost 0.033 W a unit in place of 0.1. `scripts/reads/bodies-pool.py` writes the founders, `rounds/nursery/pools/410a70db44a4b20a.tsv`.
These are the ten genomes of `inocula/r59-direction-tops/`, each checked against its SHA-256 and set on body 0. The
population of 48 takes them in turn, so eight are seeded five times and two four times.

`leaf-recipes.py bodies --config-hash e6bdb9c74cb48742` writes `rounds/nursery/leaf-direction-bodies.json` and its smoke.
It is `leaf-direction-sparse` with five changes:
- the config at D177's prices;
- the ten tops with their own brains in place of the swimmer;
- `evolve: kind` and no crossover;
- `NetEnergy` with the death charge in place of eating;
- the stop reading that reward.

The exam is D172's, and its rows still carry eating. The arm runs alone on logical 24-31 at 8 threads.

The smoke (`leaf-direction-bodies-smoke.json`) ran two generations of 16 and a small exam on 2026-10-14, from the
uncommitted build, and ended cleanly. It is registry entry 15, marked not rerunnable. In its first generation the
reward's parts averaged 234 J eaten, 560 J of upkeep, 449 J of work and 3.5 J of handling, a net of about -780 J an
episode. The upkeep is what the body's size and D177's prices predict. The stroke's work is nearly as large, so the
arm starts well below zero, and a smaller body or a cheaper stroke can gain more than better eating. The rule read the
reward each generation and stopped where the smoke's settings said it should.

**The first arm's read.** `leaf-direction-bodies` ran from 17:05 on 2026-10-14 on logical 24-31, from 17:42 on 28-31
after the CPU reached 91 C. The coordinator had it stopped by its STOP file at 18:35, and it ended after generation 24
with no exam. The search did not find a cheap swimmer. It found a body that neither swims nor eats.
`scripts/reads/bodies-read.py` gives every generation; the population's means, in joules an episode, ran so:

| Generation | Score | Eaten | Upkeep | Work | Top's adult scale |
|---|---|---|---|---|---|
| 0 | -816 | 197 | -560 | -450 | 1.00 |
| 4 | -367 | 34 | -346 | -54 | 0.64 |
| 8 | -137 | 9 | -142 | -4 | 0.51 |
| 12 | -59 | 5 | -64 | 0 | 0.51 |
| 16 | -33 | 7 | -40 | 0 | 0.34 |
| 20 | -22 | 3 | -25 | 0 | 0.37 |
| 24 | -11 | 4 | -15 | 0 | 0.36 |

From generation 12 the stroke's work was nil and nearly every body lived to the end. The brains thinned from about 60
links to between 9 and 16. The box's own dimensions moved little; the bodies shrank mostly through the adult scale.
Between generations 21 and 24 the top genome netted a little above zero, 6 to 21 J. It ate 17 to 52 J an episode with no
stroke at all, against 11 to 32 J of upkeep, so it bit what drifted within reach and never grazed.

At D177's prices a stroke cost more than the eating it bought. Net energy could not go above zero by swimming, and its
best reachable value was to lose as little as possible. The fault is in the score; the build did what it was asked. The
owner ruled on 2026-10-14 that a body search scores the energy return instead (D179), and `leaf-direction-return` runs
on it.

**The stop rule's flaw on a signed score.** The arm's rule read the reward, which stood below zero, and took the gain as
a share of the size of where the curve stood ten generations earlier. A curve creeping up towards zero from below then
reads a large gain every generation: 73 to 90 percent here, from generation 14 on. So the rule never fires, and the
search runs to its cap. A stop on a score that can be negative needs another base, or an absolute gain, before it is
used again. `leaf-direction-return` stops on eaten, which is never negative.

`logbook/specs/nursery-bodies-read.txt` holds the read's output for every generation.

### 14. The energy return (D179)

`leaf-recipes.py return` writes `rounds/nursery/leaf-direction-return.json`. It is `leaf-direction-bodies` with two
changes: the score `EnergyReturn`, and the stop reading eaten. `EnergyReturn` is eaten over upkeep, work and handling,
each episode's ratio kept in the running mean, with a dead body charged its upkeep to the episode's end. A body that
eats nothing scores 0, and one that breaks even scores 1. The coordinator argued that in the world a lineage's time to
breed goes roughly as its costs over what it nets. The ratio then ranks bodies as the world's growth would. A ratio can
favour a body that eats a crumb at almost no cost. So the stop reads eaten, and the generation's row prints eaten
beside the score. The owner approved it on 2026-10-14 (D179). It runs from `c70c324c`, from 18:20 that
evening on logical 24-27. Its first generation, the seeded tops, ate 197 J an episode against 1,013 J spent, a mean
return of 0.19 and a best of 0.37.

**The read.** Round 59's farm and heat narrowed the arm to 28-31 at 18:47, to 30-31 at 20:08 and to logical 31 alone at
20:36. The results do not depend on the cores. The rule stopped the search after generation 20, at 20:17, with eating
smoothed over five generations at 52 J against 89 J ten generations before. The exam ended at 22:09 (registry entry 16).
`scripts/reads/bodies-read.py` gives the search by generation, and `scripts/reads/return-exam-read.py` the exam beside the
seeds. `logbook/specs/nursery-return-read.txt` holds both outputs.

In the search, eating fell to 52-58 J an episode from generation 14 on, while spending kept falling, from 1,013 J to
106 J. The population's mean return rose from 0.19 to about 0.5, and the best genome's passed 1 from generation 9. The
tops' box narrowed to 0.43 m from 0.60 m, and their adult scale to 0.56, with attack and intake left at 1.

The exam swam 22 genomes on 32 held-out episodes:

| Brain | Return, best | Return, mean | Eaten, best | Eaten, mean |
|---|---|---|---|---|
| Own | 1.75 | 0.56 | 160 J | 84 J |
| None | 0.43 | 0.11 | 71 J | 12 J |
| Smell knocked out | 0.89 | 0.27 | 125 J | 40 J |

Three genomes return 1 or more on held-out episodes, the best 1.75 while eating 89 J. On the same episodes the seeds
ate 409 J on average, in D173's exam, beside other tank-mates and at the old prices. None of these genomes eats half
that; the most is 160 J. Knocking the smell out costs 44 J of eating, standard error 5, about half, and 0.30 of return, standard error
0.05. So these are foragers that still follow the leaves' smell, with a slight stroke of 0.005 to 0.034 W against the
seeds' 0.25 W. The energy return found a cheap forager that about pays its way in this tank at D177's prices, and a
weak eater.

The arm ran on recipe seed 0x1, as nearly every nursery recipe does. The coordinator recommended on 2026-10-14 (beside
D180) that a nursery result a decision rests on is repeated once on another recipe seed before it is acted on.

**The repeat on seed 0x2.** `leaf-direction-return-s2` (`leaf-recipes.py repeat --seed 0x2`, `66b22ce3`) is the arm with
recipe seed 0x2 and nothing else changed. It ran from 22:10 on 2026-10-14 to 03:48 the next morning, from the frozen
build of `c70c324c`; no source changed between. It had one to four of logical 28-31, as round 59 and the card's heat
allowed. The rule stopped it after generation 20, as on seed 0x1. Its search went the same way, a few generations later
in eating and spending. At generation 20 the population ate 52 J an episode and spent 132 J (106 J on seed 0x1), and
its stroke was 0.025 W. The best genome first passed a return of 1 at generation 9 on both seeds. The tops shrank their
adult scale to 0.51-0.64, but their box narrowed only to 0.52-0.58 m, where seed 0x1's went to 0.43 m.

Its exam (registry entry 17), beside the first arm's:

| Own brain, held-out | Seed 0x2 | Seed 0x1 |
|---|---|---|
| Return, best | 1.02 | 1.75 |
| Return, mean | 0.43 | 0.56 |
| Genomes at or above 1 | 1 of 23 | 3 of 22 |
| Eaten, best | 181 J | 160 J |
| Eaten, mean | 94 J | 84 J |
| Eating lost with the smell knocked out | 36 J (se 7) | 44 J (se 5) |
| Eaten with no brain | 17 J | 12 J |

Neither seed bred a genome that eats half of the seeds' 409 J. What replicates is the kind of body. It is a cheap
forager that still follows the leaves' smell, with a stroke about a tenth of the seeds', and it eats a fifth to a
quarter of their diet. What does not replicate in strength is the return: on seed 0x2 one genome only just breaks even.
On two recipe seeds, then, the best of these bodies sit around break-even in this tank at D177's prices. The body search
makes the swimmer cheap; at these prices and in this tank it does not make one that is cheap and eats like the seeds.
`logbook/specs/nursery-return-read.txt` holds the repeat's reads after the first arm's.

### 15. The patchy tank (D182, D184)

On the morning of 2026-10-15 the owner ruled that the nursery stops shrinking bodies (D182). It asks instead whether the
smell pays in a tank laid out like round 60's fed islands: food in a few patches with bare water between. The energy return and the
body search stay, and each arm has one repeat on a second recipe seed. The tank's layout is a world rule of the nursery,
so it went to the owner before anything ran. The owner approved it the same morning (D184). The patches are equal, the ring is
fixed and turned each episode, and the bodies start anywhere in the tank. A two-episode pace bench runs before the arms.

**The layout.** `leaf-direction-return` in a tank of 3,400 m² (radius 32.9 m) in place of 400 m², with its 31 leaves in
3 patches of 6 m radius. Their centres lie 120 degrees apart on a ring of 21 m about the tank's axis, and each episode
turns the ring by an angle it draws. Centres are 36.4 m apart. About 24 m of bare water lies between patches. Between
each patch and the glass lies about 6 m, one smell reach, so the glass does not fence a patch in. The leaves are
D173's rows, drawn as before and placed largest first, then dealt to the patches in turn: 11, 10 and 10. Inside a patch
there are 1.2 times as many leaves to the square metre as in D173's tank.

**Why these numbers.** The fed-island screens (D175, `logbook/specs/r60-screens/summary.md`) held 2 or 3 islands a seed, of 16
to 30 m radius, with their centres 100 to 134 m apart. They covered 9 to 17 percent of the water. Here, centre spacing
over patch radius is 6, against round 60's 4 to 6. The gap between patches is 2 patch widths, against 1.5 to 2.8. The
patches cover 10 percent of the surface. One ratio is left unmatched on purpose. Round 60's islands are
3 to 5 times the 6 m smell reach across, and a patch here is one reach in radius. Inside a patch every point smells a
leaf, and between patches none does. So the smell carries the whole difference between patch and desert. The episode
stays at 1,800 s. In the return arms' exams the foragers swam 118 to 235 m an episode, three to six patch spacings.
The worst start in the tank lies about 17 m from the nearest smell.

**The build.** Commit `aa2649a7` gives the recipe's leaves section an optional `patches` field: count, radius and ring.
A recipe without it is unchanged, since the field is written only when set. A test holds the ten leaf recipes the
registry has run to the hashes it recorded. With it, the ring's turn is drawn after the rows, so a seed draws the leaves it drew without
patches. Every leaf lies wholly inside its disc. `--check` prints each seed's layout. It gives the centres and the leaves in
each patch, and the nearest gap between leaves of different patches. It gives the nearest leaf's edge to the glass, and
the share of the surface beyond the smell's reach from every leaf's centre. The --check caught one fault before anything ran: the run
record writes options as plain fields, and the patches had first been carried as one object.

`scripts/reads/leaf-recipes.py patches` writes `leaf-direction-patches`, its repeat `leaf-direction-patches-s2` on recipe
seed 0x2, and the two pace benches, `leaf-direction-patches-pace` and its pair in today's tank,
`leaf-direction-return-pace`. Each bench swims the founders under their own brains for two episodes, and nothing is
searched.

**The check.** The --check lays all 520 tanks of each arm (61 generations of 8 draws, and 32 exam episodes). On both
seeds every leaf fits, and every tank holds 11, 10 and 10.

| | nearest gap between patches | nearest leaf edge to the glass | smell-free share of the surface |
|---|---|---|---|
| seed 0x1 | 24.6 to 27.8 m | 5.9 to 7.4 m | 0.73 to 0.78 |
| seed 0x2 | 24.5 to 28.0 m | 5.9 to 7.7 m | 0.73 to 0.78 |

The smell-free share is higher than the 0.6 the layout assumed. A leaf's centre lies within 6 m less its own radius of
its patch's centre. So the water that smells a patch is narrower than a full 6 m disc plus the reach. The share is
measured across the surface from leaf centres, so it is an upper bound on the water a swimmer at a leaf's height smells
nothing in. The ring's turn is spread evenly: each 10-degree bin of the 120 holds 35 to 51 of the 520 seeds.

The pace bench runs on the coordinator's word, and the two arms on the coordinator's go after it.

## The machine

| Step | What runs | Where |
|---|---|---|
| 1 | Python reading the positions, about 10 min | One core in my slot, no dotnet |
| 2-5 | Builds and filtered suites, seconds to minutes | My slot, 4 threads, BelowNormal |
| 2 | `core-test.ps1 -All` for the two Core calls, about 30 min or more on 4 threads | My slot, once |
| 6 | The drag check's five short runs | My slot |
| 7 | Two smoke runs, minutes each | My slot |
| After | The two arms' real runs | A queue job of its own, sized by the smoke |

A line goes to the coordinator at each start and end. Nothing runs beside a card job, and the room's server comes down
for one.

## Time

About three and a half days of agent work, the real runs aside.

## What was open, and how it stands

The coordinator read the plan on 2026-10-12 and answered its ten questions. The answers are in the steps above.

- **The score** is `eaten`, with `Net` beside it (the owner, 2026-10-13, D170).
- **The drag's form and size** come from step 6 and go to the coordinator with its numbers.
- **The swimmer's numbers** (step 4) and **the episode's length** (step 5) are proposals for the coordinator to read.

## Outside this plan

- `path` counting from the first economy step, whose jump from the patch's centre inflates it (step 6). A later fix,
  re-recording the fixtures that hold a path.
- The room's tank screen draws only the members, so the leaves would not show. Drawing them is room work after this.
- The card. The leaf sense and the no-corpse setting are refused on the card until a port, and the nursery runs on the
  CPU.
