# The swimmer curriculum's tank: build notes (D236)

*Written by the nursery lane's agent on 2026-10-25, on branch `curriculum`. The nursery is the small tank where swimmers are bred on their eating before a world receives them. D236 accepted a curriculum in which food moves between two patches, so that travel pays. These notes say what was built for it, what each piece was checked against, and which numbers were chosen and by whom. Nothing here has been run as an arm.*

## What exists now

The branch adds six things to the nursery, each as its own commit:

1. The nursery's world configs on today's format, so a current build loads a nursery recipe again.
2. A count of the food each body keeps from leaves under contact eating, which the tank's eaten reads.
3. The switching patches: two discs that take turns being fed, with regrowth, withering and a new disc opened far from the old one.
4. Travel measures in the patch watcher, and a script that reads them.
5. Ten blade founders: round 59's ten tops with their round limb swapped for a flat box of the same volume.
6. Two recipes, not run: rung 1 of the curriculum and the calibration bench of its tank.

A world rule is not changed by any of them. Every new field is optional and absent by default, and every new instrument is outside the config's hash and the checkpoint.

## The pieces

### The configs load again

Today's build refused every nursery config, because Core had added 45 tunables since round 60's format. I copied the three round-60 configs with those keys inserted at their defaults, each of which leaves the world as it was. The configs folder's README lists every key. The recipes that run again have copies on the new files (commit `91795444`).

Merging main brought D232's switch, `mouth.killSwallowsBody`, and the merged build refused those configs in turn. Each was re-saved with the key at false and nothing else changed. The copies, never run, were renamed from `-w6` to `-w7` in place, and rung 1 was repointed. The bench ran on the curriculum build `fbe5b233`, on `27b08fb4062517dc`, so that config and `curriculum-bench.json` stay as they were when it ran. `curriculum-bench-w7.json` sits beside them for a rerun on the landed build. The local tag `nursery-bench-fbe5b233` keeps the bench's build reachable.

### Eaten counts only food from leaves

Under contact eating a bite's meal goes to a counter that does not say what was bitten. So I added `Organism.FoodFromLeavesJoules`, a per-body share of the world's existing total of joules kept from leaves. It is one field and one line beside that total in the bite settlement. The tank's eaten reads it when contact eating is on, so a swimmer that bites another swimmer scores nothing for it. With contact eating off, the tank reads what it always read.

There is a known gap: the gut does not feed the counter. D232's gut, on main since `e1633e99`, puts a swallowed kill into the body's gut as one number, the dead body's tissue and store together. What the gut then digests cannot be told apart by where it came from, so a leaf that a body kills and swallows is never counted as eaten. Rung 1 and the bench run with the gut off, so nothing reads wrong today. The gap must close before `KillSwallowsBody` is on in any nursery tank. The fix is a per-body share of the gut that came from leaves, kept as an instrument outside the checkpoint and the hash, and credited in proportion as the gut digests. The coordinator will ask for it when the gut joins the curriculum's eating rule.

### The patches take turns

The recipe field is `leaves.patches.switching`, with a period, a regrowth delay, a withering window and a minimum turn. The rules are the ones the coordinator agreed on 2026-10-25:

- The fed disc replaces each leaf that dies after the delay.
- At each switch the outgoing disc and the other disc's leftovers wither, leaving no food.
- A full fresh set of leaves opens at an angle at least the minimum turn from the outgoing disc.

Withering needed a death that leaves nothing behind, so Core gained `World.WitherForTank`, which no world calls. Every new draw comes from a random stream of its own.

### The watcher measures travel

In a switching tank the patch watcher takes each leaf's patch from the tank's schedule. Where the leaves happen to cluster no longer decides it. Per body it records:

- the seconds from a switch to its first smell of the new disc;
- the eating credited to the disc it was taken in;
- the share eaten in discs that opened after the body's first.

`scripts/reads/nursery-travel-read.py` summarises a batch by brain, and by founder lineage when the batch holds a `lineage.json`. For the twenty founders that file is `inocula/r59-direction-blades/lineage.json`, which counts each blade in its top's lineage, giving ten lineages. `founders-batch.py --lineage` copies it into the bench's batch.

### The blades are as flat as the link limit allows

The tops' limb is a capsule 0.52 m long and 0.16 m thick. Each blade replaces it with a box 0.52 m long, 0.52 m wide along the hinge and 0.035 m thick along the swing, about 15 to 1. Every half-extent stays within round 58's link limit of 0.26 m. The volume matches to nine parts in a trillion. A 20 to 1 paddle like the fin bench's would have needed a 0.60 m width, outside that limit. The coordinator agreed with this reading, and also checked that no mutation can take the blade to a genome the build refuses.

### Two recipes wait for the bench

Rung 1 is the patch arm's recipe with the switching field and two discs on the 21 m ring. It also carries round 62's eating rule as its screen `r62cs-w500` set it:

- contact eating, with a bite taking its share of the store;
- remains that can be eaten for 500 s;
- the intake scale at 16, as D232's screen runs it.

The fin law (D229) and the mirrored joint (D228) are on. The founders are the twenty tops and blades together. The bench swims those twenty founders on 32 episodes under five brains: their own, no brain, the smell knocked out, frozen, and full power. Both are drafts the coordinator calibrates.

## How each piece was checked

The machine was shared with the theatre's renders all day, so every test ran small and filtered. A recorded value is compared bit for bit wherever a change claims to leave something as it was:

| Claim | Evidence |
|---|---|
| A recipe without the switching field swims exactly as before | `LeafPatchesTodayTests`, recorded at `91795444` |
| The bite settlement's new line changes no world state | a digest of a biting world's state over 400 steps, contact eating on and off, recorded at `91795444` (`ContactEatingTests`) |
| The leaf counter is in neither the checkpoint nor the hash | `ContactEatingTests`, comparing checkpoint bytes with the counter at its value and at zero |
| A withered body leaves no corpse and the books close | `WitherForTankTests` |
| A tank that does not switch writes today's watcher rows | `PatchRowsTodayTests`, recorded at `48c24edd` |
| The blade's volume, orientation and pools | `BladeFoundersTests` |

`LeafTankTests.EveryRecipeOnFileIsInTheListAbove` fails on this branch as it does on main, because `tank-demo-w2.json` is missing from its list. That recipe belongs to the room, and its owner is carrying the fix.

## The disc that was too small

D236's draft gave each disc a radius of 6 m. That holds 16 leaves at about one and a half times the density of D184's patches, and `--check` showed it is too tight. It lays every seed a 60-generation search and its exam would swim, 520 of them. At 6 m, the first deal of leaves found no place for one leaf in 64 draws in 3 seeds. Six of 1,034 fresh sets lost a leaf the same way. A failed first deal stops the whole search, as it once stopped both leaf arms. A failed fresh set only delays a leaf.

I expected more draws to be enough, since a seed that fits never uses the extra ones. At 256 draws one first deal and one fresh set still failed, so the disc itself was the limit. The coordinator ruled that rung 1 takes the smallest radius that fits every seed at 64 draws, keeping the ring at 21 m and the count at 31. The two candidates read as follows:

| Disc radius | First deals that failed | Fresh sets that failed | Least margin to the glass |
|---|---|---|---|
| 6 m | 3 of 520 | 6 of 1,034 | 5.9 m |
| 6.5 m | 2 of 520 | 0 of 1,036 | 5.4 m |
| 7 m | 0 of 520 | 0 of 1,040 | 4.9 m |

So rung 1 and the bench take 7 m, and the gap between the discs at the start is 28 m. The least margin to the glass, 4.9 m, is under the smell's reach of 6 m. A leaf near the rim can therefore be smelled from beside the glass, which D184 had avoided. The bench reads whether that matters.

## What waits

The calibration bench runs first, on the efficiency cores, booked by the coordinator. The arms wait for D232's read, after which the gut joins the eating rule. D231's push and D234's mirrored drive join when they are built. The bench's batch carries its lineage from the first read, if `founders-batch.py` is given `--lineage`. A search's batch made by `patch-batch.py` still records none.

## A lesson for anyone running tests beside renders

A compile ignores a test run's thread cap and spreads over every core its process may use. My gate checked each job against a render step's pause, so one pause let a whole chain through. Four builds ran back to back on fourteen cores in one pause, and the render's heat watch stopped it at 89 °C. Jobs on this branch now run only on a lane the coordinator gives, and the gate makes one look and refuses rather than waiting.
