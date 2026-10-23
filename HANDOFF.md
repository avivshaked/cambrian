# Handoff: where to pick up

*Rewritten 2026-10-04 at night. What happened is in the logbook, why it was chosen is in
[`DECISIONS.md`](DECISIONS.md), and how to do things is in `CLAUDE.md` and the nested files its table
names. This file says only where things stand and what is queued; it is rewritten, never appended to.
The version before this rewrite, with the nursery's build and the brain v2 runs day by day, is
`logbook/specs/handoff-archive-2026-10-04.md`; the earlier archives sit beside it.*

## Where things stand

**The speed work that gated round 58 has passed** (D158 and D160). On 2026-10-10 the day sitting ran
every build identical to `r57-ref58-card1` on the card, and the stray test passed 12 of 12. Against the
reference `c8762e9e` (5.26 times real time, stepping only), 1a gained 2.3%, the arena 4.3%, 1b nothing
measurable, and `r58-grow` 9.1% (two runs each, alternated; `logbook/specs/day-sitting-2026-10-10.md`). The kernels regenerated since the spin sitting cost nothing (`efa39d62`, +0.3%).
Card-run-check's whole-wall pace, start-up included, reads lower, at 4.5 to 4.9. **Round 58's build is
`r58-runner` at `02c448e6`**, frozen until the round ends: `r58-copies` and `r58-grow` merged onto the
runner, plus a test of a resume at an exchange's second and `scripts/reads/group-resume-compare.py`. It
passed every suite off the card (Core with the slow tests 1151 of 1151, the fluid term's timing test aside,
which fails on the base too), the founded group's checks and resumes on the CPU accelerator, and on the card
a resume identical to `r57-ref58-card1`, a founded group of three, and its resume at an exchange's second
matching the whole run (`scratch/logs/check58.log`). Its one card resume read 6.11 times real time,
stepping only, a single run and no timing claim. Later speed items land between rounds (D160). The work in
hand is moving the world onto the graphics card in single precision (D157); round 57 is the last round
run and read (`logbook/0137` on `round-58`). Round 58 is three connected worlds that trade a few genomes
and three isolated ones, all on the card (D159, the owner's ruling of 2026-10-09). Its runner is built on `r58-runner` (from `card-r57`, in `scratch/wt-card-r57`), ahead of the
remaining speed items (the owner, 2026-10-09). It has `--group` with an exchange between blocks at fixed
simulated times, migrants picked at random among the living, and an immigrant record that film windows
replay and that gives each planted arrival's guild, and `card-group.ps1` founds a group. It also carries three fixes. A trickle founder now gets its body
in the step it arrives (`logbook/specs/build-readings.md`). The card's scent fill now works in a founded
world, where before it wrote out of bounds. A card run cut by its wall now ends at a block's end with its
checkpoint, where before its manifest was left reading `running` (both in `src/Evosim.Farm.Gpu/CLAUDE.md`;
the round's build is named above). A founded trial on the card
(`r58t2`, three connected worlds of round 57's block, 3 genomes every 300 s for 3,600 s) held every check at
5.02 times real time, and no immigrant was refused for room. That trial planted more genomes a world in an
hour than D159's first value (3 a world each 6,000 s) plants in the whole round, which the screen of the
exchange rate weighed. The screen ran on 2026-10-11 and 2026-10-12 (`logbook/specs/r58-exchange-screen.md`). At 48 and 12
genomes a world a year, one world's leaf line swamped others within two years. At six and three a year no
world's immigrants passed 4.2%, so the rule picked six: three genomes a world every 3,000 s. **Round 58
is run and read.** The owner approved its pre-registration on 2026-10-12 (`logbook/0138`), and it ran that day
on the card. Its read is `logbook/0139` on `r58-runner` (`63131090`), with the reader's output and the twins'
checks in `logbook/specs/r58-read/`. Seed 3's founder line took seed 1's world by 21,000 s and seed 2's by
48,000 s through the ring. W held, V failed 2 of 3, S was unread, R failed, Y held weakly, and B1 and B2 held.
The isolated twins stopped on a heat stop at about 3,000 s and were resumed from 1,500 s as `r58-isor`,
byte-identical over the overlap, so each twin is read through `scripts/reads/r58-stitch-iso.py`. Seed 1's twin
`r58-s1ir` dropped 42,609 contact candidates on the card, all after 42,000 s, in year 8 alone
(`logbook/specs/r58-read/overflow-spans.txt`). No verdict turns on it: V fails on seed 3's pair, and Y read to
year 7 holds (`scripts/reads/r58-y-cut.py`).

Round 57's and round 58's records and the anchoring build are on main.

**Grazing is the owner's next focus** (2026-10-12). A Fable agent's advice is
`logbook/specs/grazer-control/fable-advice.md`. A positive control ran that afternoon on the owner's go
(`logbook/specs/grazer-control-spec.md`). Hand-built mouthed bodies were planted into
`r58-s1ir` resumed at 12,000 s, through `rounds/r58g-control.ps1`, with genomes in
`inocula/grazer-control/`. The mouth kills and probably eats leaves. Of 40 grazers, the only killers were bodies
holding still, on a reef cap or in the shallows, with leaves drifting onto them. Grazers that drift with the
leaves never meet one, and those on the bed scavenge carrion but starve. No sense finds a living leaf. The
scent holds only carrion, the light sense reads zero on the farm, and round 58's shading is pooled over the
whole water at each depth, so no leaf casts a shadow (D164). The owner wants grazers of both kinds, swimmers
first (D166, amending D164's order):
- **The anchoring cell and its drag on turning**, which both tracks use, are built and accepted, on main
  and on branch `r59-anchor` (`scratch/wt-r59-anchor`), to the plan `logbook/specs/r59-anchor-build-plan.md`: the turning drag, the touch of the rock and its sense, the leaf sense, the cell, the grip, the
  checkpoint (world state 17) and the settings, on the CPU and the card. Every suite and Core `-All` pass. The
  smoke is one run at 1 and 16 threads, and on the card at groups 32 and 64. Round 57's bodies keep to midwater
  and never grip, so the grip was checked in a gripping test world. The branch also carries D168, the card's
  single-precision fix (merged as `7af3193b`, measured in `logbook/specs/r59-pos64-plan.md`): it costs about 1% of
  the card's pace, and brings the card's contact counts near the CPU's. A card fill of the leaf field waits for
  a quiet-machine timing at round 59's pre-registration. The owner ruled the
  plan's two world rules on 2026-10-12 (D165): budding only, at a configurable weight that starts equal, and a
  broken grip re-forms at once at the full build price.
- **Swimming grazers, the priority.** The owner ruled the leaf sense and the score on 2026-10-12 (D167, from
  `logbook/specs/leaf-scent-lit/`). Living leaves give off a smell of their own, by living lit area, on a sense
  of its own, built in two forms behind settings: strength only, and strength with a direction. The nursery
  runs both as arms on one mouthed swimmer, scored by the energy it eats from leaves it bit (D170), in a tank
  that holds living leaves and nothing else to eat (D169: no age rule in the tank, two arms of 30 generations), and the owner rules which form the worlds carry. One limb on one joint steers under
  D135 (`logbook/specs/d135-steering-checks.md` on `r59-anchor`). The room builder built the leaf tank on branch
  `nursery-leaf` (`scratch/wt-nursery-leaf`), to `logbook/specs/nursery-leaf-tank-plan.md` there: round 58's
  seed-3 leaves in a 20 m tank with nothing else to eat, a hand-built one-limb swimmer and the eaten measure.
  Its two arms, `leaf-strength-2` and `leaf-direction-2` (registry entries 8 and 9, in that worktree's
  `runs/nursery/`), ran 31 generations each at `aa939309`. Eaten per swimmer, smoothed over 5 generations, rose
  from 60 to about 230 J in strength, flat since generation 13, and from 54 to 205 J in direction, still rising.
  A bench of each arm's 31 saved tops in tanks of 8 on held-out seeds (`leaf-strength-tops`,
  `leaf-direction-tops`, read in `logbook/specs/nursery-leaf-read.txt` on `nursery-leaf` `615f9e67`) shows the
  brain makes nearly all the eating, a fifth of it with no brain, and the smell little: knocking it out costs
  strength about 14 J of 214 (6%, about two standard errors) and direction nothing measurable. The search score
  ranks the tops poorly. Food is not the limit: traced re-steps show the mouths take 28-38% of what the leaves
  killed hold, and about half the leaves live. Swimmers bite each other to death, 19% in strength and 10% in
  direction. The owner ruled D173: a second pair in a sparser tank, 31 leaves and a 6 m reach, under D172's stop
  rule and exam. `leaf-strength-sparse` and `leaf-direction-sparse` ran at `nursery-d172` `9f3cd064` on
  2026-10-13 from 21:39 to 00:21 (8 threads on 16-31, narrowed to 4 cores each at 22:39 on heat) and stopped on
  the rule after generations 32 and 33. Smoothed eaten ended at 192 J (strength) and 323 J (direction). In the
  exam, knocking out the smell costs strength 15% (about 2.7 standard errors) and direction 35% (about 6), from
  generation 10 on. The pair changed the leaf count and the reach together, so it does not say which made the
  nose pay. The owner approved round 59's plan on 2026-10-14 (D174): the direction form at 6 m. The third arm
  at 3 m (`leaf-direction-sparse3`, `20c928e1`) stopped after generation 28 at 165 J smoothed; in its exam the
  knockout cost 7 J of 159 (under one standard error), so the nose does not pay at 3 m and 6 m stands. The
  card's timing of the leaf field at both reaches (prepared by the speed agent on `r59-leaftime`) still runs in a
  quiet window, for round 59's pace at 6 m; fresh worlds on round 57's and 58's settings with the turning drag on and the grip off; three
  isolated seeds, seed 1 the control and seeds 2 and 3 planted with the direction arm's ten best tops through
  the trickle pool. The pre-registration is drafted after the arm and the timing, for the owner. D172's stop rule and exam are built
  and tested on branch `nursery-d172` (`scratch/wt-nursery-d172`), unmerged. About
  one leaf in six to three in round 58's worlds has more than one cell, so it can lose a bitten cell and live.
- **Gripping grazers.** The control ran on 2026-10-13 (`logbook/specs/grazer-grip-control-plan.md` on
  `r59-anchor`, `rounds/r59g-control.ps1`) and is a clean null: no grip formed anywhere in round 57's world with
  round 59's pieces. Sixty planted grazers hovered with their anchoring cube 0.27 m above the cap tops, weightless in
  water that moves about 40 times slower there than in open water (the reefs' fade), and starved by 610 s; the
  8 kills were all by moving grazers. Seventy of the world's own bodies carried the cell and none touched rock.
  The owner closed the control on this read (D171): no touching plant and no weight are built.
- **Round 59 is read and merged** (logbook/0141, on main from `round-59`). One planted swimmer line took each
  planted world after 30,000-40,000 s, and the control grew grazers of its own. The grazers did not climb the
  smell in their worlds (D3), and the nursery's bench split D2: the smell knockout cost seed 3's planted lines
  2.8 standard errors of eating and seed 2's 1.6 (registry 21 and 22, read with `scripts/reads/r59-d2-read.py`
  on `nursery-patchwatch`). Card resumes before round 60's build are nearby continuations, not the unbroken run.
- **The nursery asks whether smell pays in a patchy tank (D182).** On `nursery-d178` (`scratch/wt-nursery-d178`),
  a recipe may mutate bodies with brains. Scored on the energy return (D179), `leaf-direction-return` and its
  repeat on recipe seed 0x2 (registry 16 and 17) both bred cheap foragers. Their stroke is about a tenth of the
  seeds', the smell knockout cuts their eating by a third to a half, and they sit about at break-even. They eat
  84-94 J an episode on held-out episodes, against the seeds' 409 J. The owner ruled on 2026-10-15 that the
  nursery stop shrinking bodies and test the smell in a tank laid out like round 60's fed islands, and approved
  its layout the same morning (D184): 3,400 m², D173's 31 leaves in three equal patches of radius 6 m on a ring
  of radius 21 m that turns each episode, bodies starting anywhere. An episode there costs 3.05 times one in
  the 400 m² tank (the pace bench).
  - `leaf-direction-patches` (registry 20, `nursery-d178` `aa2649a7`) stopped on the rule after generation 20.
    Its 23 examined genomes ate 83.4 J an episode under their own brain, 9.4 J with the smell knocked out and
    3.4 J with no brain, against 84.0, 40.3 and 12.3 J for the return arm in the 400 m² tank. The smell now
    carries nearly all their eating.
  - The patch watcher (`nursery-patchwatch` `de9df9b6`, `--restep --patches`) re-stepped the exam's tanks,
    every net equal to the record. Under their own brain the bodies spent 0.44 of their time in scent, against
    0.43 with the smell knocked out, and turned back within 30 s at 0.48 of exits against 0.50. Every body that
    started in bare water found scent, at a median 226 s. There was no trend over generations. So the smell
    acts in a patch's last metres, onto a leaf, and does not keep bodies in patches (inference).
  - The founders' and brainless re-steps, the baselines, run after round 59's D2 bench frees logical 16-23.
    Then the repeat `leaf-direction-patches-s2`, launched by hand.
  - The held-out seed check is on `nursery-d178` (`94933166`). A stop on a signed score needs another base
    before it is used again.
- **Round 60's matter: fed islands (D175).** The owner wants round 60's tank larger, with life in micro-biomes
  and not a soup of creatures. A design study (`logbook/specs/r60-patchiness/report.txt`, the proposal
  `proposals/propose-r60-patchy-matter.md`) found round 57's matter a closed stock about a hundred times a
  leaf's break-even, returned by the bed under every column. The owner approved building and screening fed
  islands on 2026-10-14: D074's influx placed on D109's islands, and burial at each column's floor. It is built
  on branch `r60-fed` (`scratch/wt-r60-fed`, `d55d75a4`, Core -All 1,184 of 1,184), with no new field. Burial
  on a reef stem's column takes from its one live top cell, about 1-2% of columns; burying only from cells on
  the bed is the alternative to put to the owner with the screens. `MatterInfluxedTotal` also books founders,
  so it is not the islands' feed alone. The speed agent's launcher `rounds/r60s-screens.ps1` (`r60-fed`
  `035f255a`, plan `logbook/specs/r60-screens-plan.md`) runs a check off the card, a short card step, then
  groups g1 and g2 (control, influx 1.2 with burial 0.001, the same with settling 0.02, and burial 0.003, on
  seeds 1 and 2), then solo alone with the other project held; every arm keeps round 59's D116 founder rule. They run after
  round 59 ends. The screens are read (`logbook/specs/r60-screens/summary.md`, maps in `scratch/r60s-read/fields/`).
  - At round 57's area only the faster settling (i2: influx 1.2, burial 0.001, settling 0.02) is patchy, on
    both seeds: 40-44% of 10 m squares are empty, against 1-6% elsewhere. Crowds are 656 and 1,929.
  - `r60s-big` (i2 at four times the area, seed 1) is the patchiest yet, with 92% of squares empty and the
    desert at a leaf's break-even. But its crowd fell to 381, almost all leaves, and its matter from 15,000 to
    5,000 units, still falling. Every oasis lies against the glass, which the theatre is to look at.
  - `r60s-big2` (D181: four times the area, the dose scaled to 4.8 units/s and 60,000 units) kept a crowd of
    6,617 at 10,000 s, and its stock levelled near 29,000 units. But its desert held about four times a leaf's
    break-even, the crowd's share on the islands fell to 0.42, and its oases again lay at the glass. The islands'
    noise map knows nothing of the glass.
  - The owner ruled on 2026-10-15 (D185) that no island lies within 30 m of the glass, and the margin is built on
    `r60-fed` (`05fd672e`). With it every oasis sits on an island, 34 to 81 m from the glass. On seed 1 at
    10,000 s, with founders in the islands (D181), `r60s-big3` at the scaled dose kept 3,228 alive with 0.54 of
    them on the islands and a desert at six times a leaf's break-even. `r60s-big3h` at half the dose kept 611,
    with 0.69 on the islands and the desert at three times.
  - Under D186, `r60s-big3` run on to 20,000 s grew to 11,690 and its patches dissolved: 0.24 of the squares
    empty, 0.40 on the islands, and a band of bodies at the north-east glass where no island lies. `r60s-big3m`
    at 3.6 units/s kept 1,827 alive at 10,000 s, with 0.59 empty, 0.62 on the islands and a level stock, the
    first screen to meet both bars. A card resume on this build is the unbroken run (`r60s-big3r`).
  - Under D187, `r60s-big3mc` ran `r60s-big3m` on to 20,000 s. Its patches thinned: 4,716 alive, 0.41 of the
    squares empty, 0.49 on the islands (under the bar of 0.6), the stock rising again and a band at the
    north-east glass, big3's pattern reached more slowly. `r60s-big3hc` runs the half dose on (D188).
  - **Round 60 has run and is read; its entry is not written.** It ran from 01:19 to 10:04 on 2026-10-16 (logbook/0142
    on branch `desert-burial`, `scratch/wt-burial`, launched from `22ab1d73`; runs in main's `runs/r60-s600*`). The read of
    all three seeds is `logbook/specs/r60-read/s6003.txt`, with island maps in `logbook/specs/r60-read/`. M1, M2, M3, M4 and L1 failed;
    M5, M6, B1 and B3 held; G1 and M7 held in seed 6002 alone; S1 is unread. The crowds (10,558, 16,345 and 17,325 at
    the end) spread off the islands, and one line took each tank. B2 failed: the card dropped contact candidates in
    seeds 6002 (6,257,897 queries over the cap) and 6003 (19,008). Seed 6002's spans
    (`logbook/specs/r60-read/overflow-spans.txt`) dropped nothing to 31,500 s, 24 in 31,500-33,000, 520 in 33,000-36,000, and
    the rest after 36,000. Past the cap the kept candidates are a race, so a world that overflows does not replay from
    its checkpoints: seed 6002 resumed at 46,500 s differed from its first second. Seed 6002's years 7 and 8, where G1
    and M7 rest, are one unreplayable realisation, and the entry says so. At 16,700 bodies a world ran at 0.75 times
    real time alone on the card. In seed 6002 the crossers M7 counted had neither leaf nor mouth: no grazer crossed. Still to do:
    theatre frames once the card is free, then the entry (logbook/0143, drafted in `logbook/specs/r60-read/0143-draft.md`)
    and the full result to the owner. `desert-burial` merges to main after 25 moves the three nursery configs its
    tunable refuses (a8cbe76c, 2b125f6c, e80c201e).
  - **The card's contact cap is fixed (D193, the owner's consent of 2026-10-16).** The grid enters a part in every
    cell its sphere covers, so a pair sharing cells arrived up to eight times, and the repeats filled the list of 256.
    Branch `cand-cap-det` (`scratch/wt-cap2`, `9ee72ce8`, on `cand-cap` `9e54908a`) holds 512 distinct candidates,
    refuses repeats, and past the cap keeps the lowest keys. On seed 6002's spans 31,500-33,000 and 46,500-48,000 it
    dropped nothing, matched the round where the round had not overflowed, and two runs of each were identical
    (`logbook/specs/r60-build/det-chain.log`), at 0.74 times real time on the dense span. It is on `r61` and merges to main
    with it. It shows nothing about a denser world; round 61's B2 asks.
  - The owner's aim (D191, CLAUDE.md): separate oases with swimmers that migrate between them to graze or hunt, a
    real ocean.
  - **Round 61 has run to 48,000 s and is read; D195 runs it on** (D192; pre-registration logbook/0144 on branch
    `r61`, `2f50ad5f`, `scratch/wt-r61`, approved by the owner on the evening of 2026-10-16). Round 60's world with no
    new rule, the nursery's leaf swimmers planted through the trickle from 3,000 s, landscape 1 (six islands) in every
    world, two arms on life seeds 6101 and 6102: `r61-p6101`, `r61-c6101`, `r61-p6102` and `r61-c6102` (main's `runs/`),
    one card group from 19:58 to 05:17, 9.3 h. B2 held (no contact dropped, card memory at most 12,871 MiB), and B3 held
    (`r61-p6101b` from 45,000 s matches in every record). The read is `logbook/specs/r61-read/r61-read-48000.txt`, by
    `scripts/reads/r61-read.py` on `r61`, which reproduced round 60's reader on round 60's worlds
    (`logbook/specs/r61-read/r61-reader-on-r60.txt`); round 60's reader on round 61 is `logbook/specs/r61-read/r60-clauses.txt`.
    P2, C1 and B1 held; P1, P3 to P7 and L1 failed; S1 is unread. The planted grazers killed 138 to 335 leaves a year
    in years 6 to 8, the controls none, but only 5 to 19 were alive at any year's end. One line held 99.8% and 99.9%
    of each planted world. The pairs bin apart under D195 on P3 (leaves planted over control 0.64 and 2.51), P4 (+0.01
    and -0.28) and P5 (0.00 and -0.19), so the third pair ran (`r61-p6103` and `r61-c6103`, group `r61-3`,
    2026-10-17). `r61-p6103` slowed tenfold from 45,300 s on giant parts (the giant-parts bullet below), was stopped
    at 46,050 s and resumed to 48,000 s as `r61-p6103r` on the giants fix (b81ca261), proven bit-identical by three
    gates. Read on all six worlds with `--seeds 6101,6102,6103` (`logbook/specs/r61-read/r61-read-48000-six.txt`): P1
    failed, P2 held (138-335 leaf kills a year in all three planted worlds, none in the controls), P3, P4, P5, P7 and
    L1 failed, P6 and C1 held, S1 unread, and B1 failed on `r61-p6103`'s one diverged body (79817, an 8-part rigid
    chain of the giants' family, 45,080 s). Seed 6103's bins: P3 1.23 behind, P4 -0.07 behind, P5 -0.01 no effect, so
    6102 and 6103 agree on behind for P3 and P4, and 6101 and 6103 on no effect for P5. `r61-p6103r` dropped 78,661
    contact candidates past the 512 cap in its last 1,950 s, the 37 m ribbon sweeping the crowd, so its physics there
    is degraded. The extension runs: the four ran as group `r61-x` from 48,000 s on the giants fix, were stopped at
    56,960-59,150 s when the controls slowed (mouths eating corpses, checked against every corpse each step), and
    resumed each from its own stop second at 20:50 on 2026-10-17 as group `r61-x2` (`r61-p6101x2` and so on) to 72,000
    s on the merged build 41086157 (scratch/wt-merged, branch `r61-merged`: the giants fix, the intake by columns and
    the pace guard), proven bit-identical by the gate in `logbook/specs/r61-engine-fixes.md`. Each world reads as one
    chain with its sources. `r61-x2` ended by budget at 72,000 s on 2026-10-18, with 8,141, 10,377, 9,059 and 9,203
    living. The 6103 pair runs as group `r61-x-p6103-c6103` from 48,000 s on 1d142557 (`r61-merged` with the pool
    fix), launched at 01:39 on 2026-10-18, and `r61-c6103x` ended by budget at 05:09 with 10,560 living. The pace guard stopped `r61-p6103x`
    at 49,610 s (checkpoints at 49,500 and 49,610). More rows reached 1,024 cells than the giants fix's 32 places
    (`GiantCap`), about 70 by 49,610 s, mostly 10-12 m kelp blades, and those left in the grid made the card's body
    step five times slower. `GiantCap` 1,024 (f0aef32f) left every bit in three gates on 2026-10-18
    (`scratch/r61-giants/zgate.log`): z1, `r61-p6103x` from 49,500 s against itself, at 1.45 times real time where it
    had run at 0.35; z2 and z3, `r61-p6101` from 45,000 s against g1 at giant cells 8 and at the default. It is merged
    into `r61-merged` with the launcher's lone step (00619cac), and `r61-p6103x2` resumed `r61-p6103x` alone from
    49,500 s and was stopped slow at 55,140 s by one blade whose 51 m part met more rows than a giant's list of 2,048
    and walked its cells. `GiantListCap` 32,768 (5747365b) left every bit in gates y1-y3
    (`scratch/r61-giants/ygate.log`) and is merged, and `r61-p6103x3` ran on to 70,160 s and `r61-p6103x4` from there
    to 72,000 s, ended by budget on 2026-10-18 with 2,867 living; their checks report overflow and divergence without
    stopping. The same kelp blades diverge, 20-27
    per 1,000 s, because the drag is stepped explicitly and its limiter is off at 0.01 s
    (`logbook/specs/r61-kelp-divergence.txt`). Round 62 lands a self-scale that does not compound and the drag limiter
    at every step, the force capped per link and the torque by the welded body (D196, D199, D200); the speed agent built it on `r62-blades`, the card's copy too (a0d87f9f): identical on the CPU accelerator and, founded at seed 6201, identical on the card at groups 32 and 64 with nothing diverged (`logbook/specs/r62-blades.md`). The branch predates the history squash, so it lands on main as a patch of its change since `archive/local-2026-10-18`'s tip when round 62's build is assembled, with the coral and the cousin resume. Every `r62-*` branch and `theatre-coral` carry three commits of 2026-10-18 (`c32c5b71`, `dd587eb0`, `b37e2a2b`) whose author address the pre-push hook refuses. The owner ruled on 2026-10-23 that these branches stay local: the round 62 line lands on main as new commits, never by a merge or a push of the branches. The owner ruled on 2026-10-23 that nothing else in the world changes until round 62 shows the grazers' dials are right. The mouth point is built meanwhile, off by default. Eating by contact is held as an idea (`logbook/specs/eating-by-contact-idea.md`). A world alone on the card runs at 1.6-1.7 times
    real time. In a group of four on one 8-core
    mask each got 0.7-1.1, the largest crowd least (the probe p62 in `logbook/specs/r61-engine-fixes.md`). Round 62's
    screens try a separate block of cores for each world (D198). All six worlds ended at 72,000 s and are read in
    `logbook/specs/r61-read/read-72000.txt`: P2 and C1 held, P1, P3 to P7 and L1 failed, S1 is unread, and B1 failed
    on `r61-p6103x4`'s 488 diverged bodies alone. The grazers stayed at 8 to 59 a world yet killed 127 to 335 leaves a
    year in years 6 to 8. In `r61-p6103x4` their kills rose to 1,226 a year as the leaves fell to 2,784, the one world
    where P3 and P4 held. The 1,199 mouthed bodies in `r61-c6101x2` photosynthesise and kill nothing. The first look in the theatre, drawn from the snapshots of `r61-p6103x4`, `r61-p6101x2` and `r61-c6101x2` at 72,000 s (`scratch/owner/r61-theatre-72000/`), shows the crowds in bands at the glass, the north and west rims and a mass in the south-east, with few bodies on the islands; planted and control look alike, `r61-p6103x4` is sparse in two clumps at the rim, and its side view shows kelp blades standing past the water's top. The round is written up in `logbook/0145-the-planted-grazers-starve-and-the-world-changes.md`. The owner judged its setup wrong for what the project looks for: from above, far too many creatures and no empty water. A count puts the crowd 30 to 60 m inside the glass and the outer 15 m under-filled (`logbook/specs/open-ocean-survey.md`). The pace guard runs in the merged build, and overflow and
    divergence never stop a run (D197).
    On a heat stop the work waits an hour for the room to cool, then resumes from the checkpoint (the owner, 2026-10-17). Held for later rounds, each as its own question: a death risk that rises with a line's crowding, harsher
    winters, staying power for the crossing, strands between islands, and a replay of the tape. Rounds keep 48,000 s;
    a world still developing at the end is run on from its checkpoint. Round 60's seasons were a ratchet: growth in one
    half of each year, a dip of at most 5-10% in the other.
  - **The grazers' economy (the owner's direction of 2026-10-17).** The owner reads round 61 as the dials not yet
    turned far enough: moving should be cheap and killing should pay, and the dials are pushed in favour of movement
    and grazing until they overshoot, with grazers eating the leaves down too far, then brought back between the last
    two settings. The read of round 61's planted worlds from the lineage is done (2026-10-18, `scripts/reads/r61-grazer-economy.py`, outputs and ledger screens in `logbook/specs/r61-grazer-economy/`). Nearly every living grazer is a founder from the trickle: each planted world took 1,115 to 1,188 founders and their lines bred 28, 338 and 134 grazers in 72,000 s, and only one line (p6102's 126388, 243 births, to generation 16, 47,650-62,110 s) outlived its founder's year. Founders die at a median 636-655 s, 96% starved, about when their 455 J endowment runs out at the swimmer's standing cost of 0.908 W (0.288 W of it the attributes' upkeep); the billed work is about a tenth of that. Only 36-45% of founders ever kill; a leaf corpse holds 45-56 J, so one kill eaten whole pays about 46 s of upkeep, and the mouths kept 12-46% of the joules in the corpses grazers made in the late years. The line that bred had a body of one rigid part at 0.164 W. The planted swimmers do swim: 0.054-0.081 m/s against the local drift, rigid bodies 0.004-0.046; the earlier half-a-drifter's figure averaged every jointed body. The dial search is ruled (D201): the consumer cell's upkeep, the mouth's reach, what eating keeps, then the mouth's and claw's upkeep, one at a time, each pushed until the living leaves fall below half an unchanged cousin's. Each screen is a year of cousins of all three planted worlds with the trickle off, on round 61's build, resumed before any giant blade. The resume with changed dials is built on `r61-cousin` (`scratch/wt-cousin`, 5dc51944; `run-farm.ps1 -ResumeConfig <edited config.json> -TrickleOff`, and `card-group.ps1` likewise). It allows the economy, mouth and cellTypes groups only, checks the checkpoint against the source config, and marks the run a cousin with both hashes and its changes; trickle off also sets the pool share to 0, which the world requires. The screens start at 30,000 s, where no part in any planted world passes 2 m (the giants census), and need -AllowSourceMismatch since the checkpoints were written by round 61's first build. The launcher is `rounds/r62d-screens.ps1 -Set g1|g2ab` on `r61-cousin` (eed5ba73), configs in `rounds/r62d/` there; the runs are in main's `runs/`, read per pair with `python -I scripts/reads/r62-dial-screens.py --pair runs/r62d-<dial>-610N runs/r62d-u-610N --start 30000 --end 36000`. Group 1 (the unchanged cousins `r62d-u-610N` and the consumer cell's upkeep at 3, `r62d-a1-610N`) ended at 36,000 s: the grazers died out by 33,000 s in both arms and the leaves stayed at 0.92 to 1.04 of the cousins' (`logbook/specs/r62-dial-screens/`, with the economy read and the ledger, which puts the consumer cell at 0.63 W of the body's 0.908 W). D203 then screens the upkeep at 0.75 and the mouth's reach at 1.0 m (upkeep at 6) in one group, and a dial that never overshoots goes back to its starting value. Group 2 (`r62d-a3-610N` and `r62d-b1-610N`) ended at 36,000 s: neither kept a grazer alive and the leaves stayed at 0.95 to 1.07 of the cousins', so dial A is done and goes back to 6. The next step, the reach at 2.0 m, is held: with D206 the leaves' economy changes, and whether the search goes on in round 61's build is the owner's to say. The values found are checked again in the open square on round 62's build (D205).
  - **Giant parts are kelp blades built by module adds (found 2026-10-17, at the owner's request).** In `r61-p6103`
    the largest part passed between siblings of generation 79 (80271, 10.4 m, then 82419, 18.4 m half-diagonal), and
    slowed the card tenfold until the giants fix (branch `r61-giants`, b81ca261, proven bit-identical) made it cheap.
    Neither body grew it with `World.Grow`; each added modules in life. Their genome's node 0 has a self-edge scaled
    (1.009, 1.769, 0.502), so each added copy is 1.769 times longer and half as thick until the thickness floor of
    0.010 m. The 8th copy is a photosynthetic ribbon 0.36 m wide, 36.8 m long and 2 cm thick, on a rigid chain of 8
    boxes, so the 1e6 m³ volume ceiling never bites. The line's birth-plan parts did not grow over generations. Body
    79817, which blew up at 45,080 s, was a rigid chain of the same family. The output is
    `logbook/specs/r61-kelp-divergence/gf-p6103.txt`. A census of the largest part every 1,500 s in rounds 59-61
    (`logbook/specs/r61-kelp-divergence/census/`) shows drift in every round: photosynthetic plates at the thickness floor grow
    long or wide over tens of thousands of seconds, to 2-3 m in most worlds, in planted and control alike. Two
    worlds found a chain whose self-scale compounds the length: `r61-p6103` (7.0 m by 45,000 s, then the jumps) and
    `r60-s6002` (7.36 m at 46,500 s), whose overflow after 36,000 s, and so round 60's B2 failure, is very likely
    the same mechanism; round 60's read is to be re-read with it. Such chains diverge under the explicit drag
    (`logbook/specs/r61-kelp-divergence.txt`). Three rules make these giants costly in
    compute and physics: a self-scale above 1 that module adds repeat in life, a thickness floor that keeps the volume
    small, and contact by a sphere of the part's half-diagonal (D114), so a 37 m ribbon shoves everything within 18 m.
    The owner ruled on the first two for round 62 (D196); contact by the half-diagonal is unchanged.
  - **The card's driver is NVIDIA 617.42** (installed at the owner's restart on the evening of 2026-10-19, from 617.14).
    Both its checks passed: every card test, and a 1,500 s resume of `r61-c6101` on the card that matched its recorded
    rows exactly.
  - **Round 62 waits on a grazer screen (D204 to D210).** The world is ruled: D204's open square at a current of 0.02 m/s with D206, D207 (own leaves first) and D194's coral, built as `r62-build` (`scratch/wt-build`), whose snow is carried on the card and whose ocean phases are tabled per edge column, bit-identical, a world at about 2.5x five-up (`logbook/specs/box-transport-speed.md` there). The collapse screens (`logbook/specs/r62-collapse-screens/`) found only the slow current made oases. Six worlds launched at 16:53 on 2026-10-19 (pre-registration 1465b670, `logbook/specs/r62-prereg/predictions.md`) and were stopped at about 900 s on the owner's ruling (D210): without working grazer dials the round would end as round 61 did, and the owner expects the pace to fall toward real time as crowds grow. Their directories in `runs/r62-*` are the record of the attempt, not the round. The grazer screen ran on `r62-graze` (`r62-build` plus D211's two dials): seven worlds founded at seed 6201 for 12,000 s, each drawing its trickle from `rounds/r59-pool.tsv` at share 0.5, all ended at budget with the books closed (`logbook/specs/r62-grazer-screen/README.md` there, with the read's tables). Only the mouth's reach moved the grazers. At 2.0 m they bred 165 against the control's 9, to generation 5, at a replacement of 0.43, and their kept intake rose from about a sixth of the upkeep to four fifths, by eating corpses they had not made. The capped endowment, read first as the owner ruled, did no better than the control, so a bigger start will not help. A mouth four times faster and leaves twice as rich gave nothing; the leaf confound did not lift the rich-leaf arm, so it needs no fix. The four together gained what reach gained, and the nursery's foragers killed less. No arm sustains grazers, and the oases held in all of them. D212 then ruled a spoilage clock on corpses, and D213 a screen of it against reach. The clock is built on `r62-spoil` (92b61136, from `r62-graze`): `EVOSIM_CORPSE_HALF_LIFE`, 0 and off by default, halves a corpse's joules every H seconds, the energy to heat and the matter to the spent field; detritus gets no dial, since remineralisation already spoils it (the coordinator's ruling, `logbook/specs/r62-spoil-build/README.md` there). D212's long baseline without spoilage runs as `r62gx-ctl` and `r62gx-reach`, the screen's control and 2 m worlds resumed from 12,000 s to 24,000 s (`rounds/launch-r62gx.ps1` on `r62-graze`). D213's screen, eight worlds at 24,000 s crossing reach 0.5, 2 and 3 m with half-lives of 500 and 2,000 s and a second seed (`logbook/specs/r62-spoil-screen-proposal.md`; `rounds/launch-r62s.ps1` on `r62-spoil`), launches on the coordinator's go once its hashes, an accelerator preflight and a short card identity run with the clock on are in. Round 62 then relaunches with the reach and half-life found, on an amended pre-registration. Queued for the speed agent after it, in order: two bit-neutral fixes to the cell table (the link's leaf surcharge reads the config's leaf cell; the consumer cell's unused yields are marked so, and `BiteRate` is documented as the scavenging cap), then the owner's dial for reach measured from the mouth part, defaulting to the centre, with a replay proof, then lever 2. The owner's list of the world rules without a dial is in `scratch/owner/world-rules-without-dials/`; the owner ruled on 2026-10-19 that measuring from the centre and reading a body in one cell stay theirs. A census of how far bodies span the grids (`--extent-census` on `r62-extent`) reads the screen's checkpoints. Lever 3 is settled: inside one world the host and the card form a chain, and a card group already overlaps one world's host work with another's card work, so the card is the bound and the speed work goes to lever 2 (`scratch/r62-box/lever3/overlap-note.md`). Assay keeps 30-31 until the coordinator sends it 'round ended'.
  - **Round 62: branching coral on the oases (D194, the owner's ruling of 2026-10-16).** Before the build, two pieces
    of engine work on the card: measure the reefs' share of a step on a checkpoint, and compute the current's fade
    once per link rather than twice. The survey is `logbook/specs/coral-survey.txt`. The owner asked on
    2026-10-17 for the prep to start so round 62 can follow round 61 quickly: the speed agent measures the reefs'
    share on a checkpoint once the card is free and builds the fade change on its own branch off 41086157, and the
    coordinator drafts the coral build spec (`logbook/specs/coral-build.md`). The grazer dial search goes before the
    coral and the blades' rules land with round 62 (D196, D198).
  - **The theatre on HDRP**: the spike runs (D202), in a separate project `unity-hdrp/`, with Unity only between card runs. Its first still (`scratch/owner/hdrp-first-stills/`) had none of the look ported, and the owner found it very poor. Next, in order: the bed's graph, both theatres filmed through the safari path with the agreed look, the skin, then caustics and shafts. The four moments are windows of `r62d-u-6102` at 31,500 to 31,569 s (`scratch/hdrp-spike/r62d/windows/`). Nothing reaches the owner before the reference check against round 57's film. The port waits for the owner's ruling on the four moments.
  - A later question, raised with the owner on 2026-10-16: holdfasts at the oases' edges. D171 closed the
    anchoring cell after a control in which nothing gripped, in slow water at the caps with nothing to hold on
    for. Fed islands give a body on a rock at an island's edge drifting matter to catch, as anemones, barnacles
    and kelp catch it. It reopens only as its own proposal, after round 61, and on the owner's ruling.
  - Round 60 at this size, as estimated from `r60s-big2` alone: about 4.3-6 h of card time a seed one at a
    time, or 9-11 h for three seeds as one group. A group would need 13,000-18,000 MiB of card memory, with
    the other project held throughout, and a short founding check settles it before the round.
  - Round 60's build is on `r60-fed` (`480ea8a1`, `34dd272c`).
    - The checkpoint carries each link's float contact centre and its rest (World.StateVersion 18; 17 is still
      read).
    - The birth overhead's returned matter is in double.
    - GpuSettledResumeTests guards the resume; it fails on the old restore.
    - Core -All passed 1,184 of 1,184, and the other suites passed. The Room's E2E tests failed for Edge's port
      and profile lock, outside r60-fed.
    - A card resume of a fed world from 1,500 s now reads as its whole run in every row (`r60s-fix`,
      `2a72dd7f`). A resume from tens of thousands of seconds is still to show, with round 60's B3.
  - Its pre-registration follows D180: new seeds by a rule (6001-6003), every arm on the same seeds.
- **The card is shared with another project** (the owner, 2026-10-14). Its map is drawn by a shader on the card, and
  It works in its slot on logical 0-3, one job at a time, with dotnet at 2 threads.
  - While a farm card run with `-HeatStop` runs, the other project runs only on logical 24-25, two threads at BelowNormal, nothing on 0-15 and no Unity, on the coordinator's notice (its terms of 2026-10-15): a build or
    run starting on the fast cores spikes the hottest core past 90 °C. The coordinator gives the window back
    when the run ends. A timing read holds it the same way.
  - `-HeatStop` stops on two samples in a row at or over 90 °C (D183). The speed agent builds that on
    `r60-fed`; until it lands, one sample stops a run.
  - The card launchers' memory limit is 12,000 MiB, and they pass the other project's Unity, the room server and a
    nursery run by `-AllowBeside`. A card group runs on 8 threads on logical 16-23.

The owner ruled round 58's open questions on 2026-10-10 (D159): twin seeds, arrivals' clades counted, the
screen's bars as a start, and a migration that replays. The anchoring cell, with
a sense of touching the bed or a reef, is built meanwhile and tried in the nursery for round 59. The owner
ruled its questions and its pricing on 2026-10-11 (D162, `logbook/specs/anchoring-cell-design.md`): it grips
by itself on the bed and the reefs, a strong pull breaks it, and it costs energy to build, decays, holds by
area and weakens as the reserve runs low. A drag on turning is built with it and carried by every world of
round 59 (D161), since single stomachs rock and a few spin without end
(`logbook/specs/rocking-bodies-2026-10-10.md`).

### The machine

- How much of the machine the work uses does not matter in itself (the owner's ruling, 2026-10-09). Two
  conditions hold instead: the machine must not overheat, which the heat watch checks, and it keeps a little
  headroom for other work. A farm run holds its 16 logical processors on the efficiency cores, 16 to 31,
  which leaves the fast cores, 0 to 15, for the owner and other projects. Sharing the machine between the
  projects as their work needs is the coordinator's job, and any main agent's (the owner, the same day). A
  timing read is taken with nothing else running, or it is re-taken.
- The BIOS was flashed on 2026-09-29 (1836, microcode 0x133) and the NVIDIA driver updated (617.14).
  The card carries no heavy work until about 2026-10-06, after a week of clean running. Any
  unexplained crash or clean re-run is reported the same hour.
- **Heat watch** (the owner's ruling, 2026-10-01): while any heavy job runs, a session cron runs
  `scripts/temp-check.ps1 -Log` every 30 minutes, and the work stops if the CPU or the GPU gets too hot.
  Thresholds and the stop order are in `logbook/specs/machine-and-tools.md` ("Heat"). Beside each
  nursery arm a watchdog (`scratch/heat-watchdog/watchdog.py`) reads MSI Afterburner's log every minute
  and stops the arm when it is hot. The owner ruled on 2026-10-03 that the watchdog stays out of git.
- Nursery runs are pinned to the efficiency cores (logical 16 to 31, affinity 0xFFFF0000). A job of
  30 minutes or less may use all cores, by day, under the heat watcher, and only when nothing else is
  running, so it runs alone in the queue (the owner's ruling of 2026-10-05, "All cores for nursery
  runs", below).
- The fans follow the CPU's temperature since 2026-10-05 (`logbook/specs/machine-and-tools.md`, "The
  cooling"), so heavy work is loud. On 2026-10-05 the owner approved
  the night's work as planned (the farm ensemble, then W1's trials, two farm runs at a time on the
  efficiency cores) until they say otherwise.
- Another project shares the machine; its session is named in `scratch/machine/queue.md`. It runs at low
  priority under the heat watch, and its jobs stop first if the CPU reaches 85 °C. It reads no temperature of
  its own, so the coordinator's heat watch stops them, by a message. It asks the coordinator for machine time,
  which the coordinator records in the queue. A long job of it gets logical 0 to 5 at most: on 0 to 7 its
  heaviest job touched 88 °C.
- A session's cron dies with its session. A new session runs `scripts/sweep-orphans.ps1` and arms its
  own.

### What runs now

Round 58 is run and read (above). The speed work is led by the speed agent (session evolution-simulator-08). Its plan for step 13 in
single precision is `logbook/specs/card-single-plan.md` on `nursery-speed` (`cbc27247`), checked by the
coordinator on 2026-10-08. Stage 0 measures: W0 splits the world's 25 phases on round 57's farm, and 0b
finds how much of the card's bodies' step is copying. Stage 1 ports to the card what rounds 54 to 57
added: the limb push, the bend-and-power drive, the senses and the central brain. Stage 2 moves the
world's heaviest parts onto the card, in W0's order.

Stage 1's ports are all on `card-r57` (`a0ae70e3`, worktree `scratch/wt-card-r57`): the reach guard and
the bounded walk, the limb push and the bend-and-power drive, the scent and the drive senses, and the
central brain. Each matched the CPU's solver bit for bit on ILGPU's CPU accelerator in double, through a
death and a birth (35 of 35 Gpu tests, 08:17 on 2026-10-08). The card now refuses only the bed's
friction, which round 57 leaves off. The off-switch proof passed on `card-r57` `5e37f554` (2026-10-08,
10:59 to 11:16), with one Core timing test red on this machine since its restart, main's own build
included (`card-audit-r57.md` on `nursery-speed`, an open item for the owner). **The card runs again,
carefully**, by the owner's ruling of 2026-10-08: the driver watch is confirmed running and still alive
before each card session, every card run is one go, and any driver event stops the card's work and goes
back to the owner. The cause of the morning's hang and first driver reset was ILGPU 1.5.3's double sine,
which reduces an angle by subtracting 2π in a loop; the farm now refuses double precision on the card and
the card's double helpers reduce past 1e9 rad. The second reset came while the owner was bringing the dark
monitor back, and the owner reads it as the first one's aftermath (`logbook/specs/machine-and-tools.md`,
"Two driver resets on 2026-10-08"). On `5e37f554` the twelve planted strays passed on the card itself, each
alone, and S0, r57-s2 from 40,000 s for 300 s in single at group 32, kept its books (audit 0.0013 J, matter
1e-6), diverged nothing, refused no body, and ran at 1.71 times real time with nothing beside it. W0's
0.83 for the CPU at 16 threads is not its match: W0 ran round 57's build before the scent fill's split,
which `card-r57` carries, and the plan guesses the CPU with it at about 1.05. Clause 2's soundness set
passed on `5e37f554`: r57-s2 from 47,000, 20,000 and 1,000 s for 2,000 s each, at groups 32 and 64, every
pair identical by `gpu-accept.py` (stats, digest and lineage), the books under their bars, nothing diverged
or refused. S7, a world founded on the card and carried a year, held its books with nothing diverged or
refused. Clause 3's pace read (2026-10-08, r57-s2 from 47,000 s for 300 s, card and CPU taking turns
twice in one sitting, nothing else running): the card 1.80 times real time, the CPU at 16 threads 1.17,
so the card ran 1.54 times the CPU. Then T1, the snow's transport on the card, passed on round 57's
world: its own test on the card to the bit, and r57-s2 from 47,000 s for 2,000 s with the transport on
the card identical to S1 in stats, digest and lineage (`card-r57`, 2026-10-08). The pace read again with
T1 (15:54 to 16:20): the card 1.99 times real time, the CPU 1.19 and 1.12 (a 5.6% spread between its two
runs that the sitting does not explain), so the card runs 1.73 times the CPU, 1.68 to 1.78 over the pairs.
That completes the plan's minimal finish line, under the proposed bar of 2. The owner ruled on 2026-10-08,
seeing the minimal line done in about a day against the plan's 7.5 to 12 agent-days, that the speed work
goes on and D158's hold stays, to end when the owner chooses the next round's world, at whatever pace has
been reached by then. Each item is measured as it lands, and each keeps the card's self-replay; the host
items stay bit-identical to the CPU. Read so far: two seeds as two processes ran 1.28 times one seed alone;
the per-kernel probes found the card idle while the host works; the corpses phase is split into fill,
deposits and drift; the grid's mean on the card brought the pace to 1.97. Step two (`6d08e17b`) reads
each link's values before writing any, which halved the step kernels' time at 96 registers. With it,
r57-s2 from 47,000 s for 300 s ran at 2.14 times real time, identical to r57-pacet-card1, and the card
now waits on the host for 53% of the wall. Several worlds run in one process on the card (`--group`, `card-r57` `5011f575`,
`scripts/card/card-group.ps1`), sharing one pool of 16 threads, each world identical to its run alone. With
the scent on the card, concurrent mode and the spin at 5,000, one sitting on 2026-10-09 measured one world
at 5.27 times real time. Two worlds ran at 4.03 each, 1.53 times one world's output, and three at 3.58 to
3.63 each, 2.05 times. Three worlds took 4.5 GB of the card's memory, and 4.8 GB at r57-s2's peak of 10,300
bodies a world. At three worlds the card is near its limit, at up to 94% load. The classes on a stream each (`EVOSIM_GPU_CONCURRENT=1`, built for
round 48 and left off since, with no reason recorded) are identical on round 57's world and step 1.40 times
faster: 3.53 times real time on `98aceed4`, against 2.53 with the switch off. Every card read now runs
with it on. Whether scored card runs take it is put to the owner with the next round's world. The card is
now idle 71% of the stepping wall, so the next gains are on the host (`card-next-r57.md` Parts 7 and 8 on
`nursery-speed`). W1a (`7eed0c11` on `card-r57`) queues the block's uploads and downloads without a wait, with
three explicit joins. Measured beside a repeat of its baseline in one sitting (2026-10-08, r57-s2 from 47,000 s
for 300 s, the streams on), it gains 5.7%: 3.82 times real time against 3.62, 14.6 ms of wall a simulated
second. Two runs of one build differed by about 2% across sittings, so every pace claim is read beside a
repeat of its baseline in the same sitting. C3, the scent fill on the card (`EVOSIM_GPU_SCENT`, off by
default, Part 7), matched the host's fill on round 57's corpses on the card itself, and r57-s2 with it was
identical to r57-pacet-card1 at groups 32 and 64 and to r57-card-t1 over 2,000 s; Core's `-All` passed on
its build. Whether scored runs take it is the owner's, with the next round's world. Three more items are on
`card-r57` `54678e7c` (from `card-r57-check2`), each identical to r57-pacet-card1 at groups 32
and 64:
- the second finiteness bracket narrowed to the bodies a birth, growth or reconcile touched, with no
  measurable gain on the card;
- the checkpoint's digest, compression and file on a background thread, with a test that kills a write
  midway through a test-only stall that every launcher of a real run refuses;
- the scent's fill queued on a stream of its own at the corpses' snapshot, so that the card fills while
  the host settles and mixes the snow. A test on the card checks the order across the two streams, and a
  probe run checked it over 600 blocks.
After the Windows update to 26H2, `54678e7c` rebuilt and re-run is still identical to r57-pacet-card1 at
groups 32 and 64. One sitting on 2026-10-09 then ran the three builds as before, with item 3, and with item 3
and the fill, two runs each in the order B I E E I B. That sitting gives each item its own share, with the scent
on the card:
- item 3 alone, 3.94 times real time against 3.93, which is nothing net. It takes 4.5 ms a simulated second
  off the writers, and the world step rises by about 2.4 ms. Running the writer's thread at the lowest
  priority does not bring the world step back down: four runs a side, the same day, gave 4.18 against 4.20;
- the early fill, 4.22 against 3.94, +7.0%. The grid's step falls by 15.3 ms a simulated second, and the
  world and the writers hardly move.
With the early fill, the scent on the card runs 4.17 times real time against 3.80 for the scent on the host,
+9.7% on one build in one sitting (2026-10-09). That is the figure for whether scored runs take
`EVOSIM_GPU_SCENT`. The scent on the card still raises three host passes, by about 5 ms a simulated second
together: the step's after-phase (the mirror) by 2.7 ms, the finiteness check by 1.7 ms, and metabolise by
0.7 ms. The world step's reading spreads by up to 8 ms a simulated second across runs of the same code, so a
change in it smaller than that needs several runs on each side.
**Most of the host's remaining cost is its threads waking up.** The farm reads
`DOTNET_ThreadPool_UnfairSemaphoreSpinLimit`, which sets how long the thread pool's workers spin before they
sleep. With it at 50,000, the scent on the card ran 5.50 times real time against 4.22 without it, +30.2%,
four runs a side in one sitting (2026-10-09, `card-r57` `efa39d62`, every run identical to r57-pacet-card1).
The world step fell by 34 ms a simulated second, and the scent-on rises went with it. The fill on the card
still beats the host's with the spin, 5.50 against 5.17. No bit can move: the variable changes only when
a waiting thread sleeps. Its cost is load and heat, because the farm's 16 logical processors never rest.
The machine's `% Processor Utility` stood at 78 to 89% against 17 to 26%,
and the CPU's peaks rose from 50 to 62 °C to 58 to 67 °C over 300 s runs. A gentler limit keeps most of
the gain at a fraction of the load (the card, the scent on it, two runs a limit in one sitting, 2026-10-09):

| Limit | Times real time | Gain | Total `% Processor Utility` | CPU peak |
|---|---|---|---|---|
| unset | 4.14 | | 17% | 54 to 57 °C |
| 500 | 4.51 | +8.8% | 17 to 19% | 52 to 54 °C |
| 5,000 | 5.24 | +26.5% | 30 to 32% | 49 to 51 °C |
| 50,000 | 5.55 | +34.1% | 80 to 82% | 59 and 78 °C |

The CPU farm at 16 threads, off the card, gains +55.5% at 5,000 and +55.7% at 50,000. It runs 2.01 and
1.99 times real time against 1.29, and the gain is mostly in its own solver's parallel step. There the spin
keeps the farm's 16 processors at work, so the total utility reads 76 to 81% against 42 to 47%. A boosting
core counts above 100%. The owner chose on 2026-10-09 that card runs take the spin at 5,000, and that runs
off the card do not take it. Since load in itself is no bar (under "The machine"), a higher limit is a
question of heat and headroom, and 50,000 fails headroom. An hour on the card at 50,000 (`r57-hr1`,
2026-10-09) held the machine at about 82% utility alone (CPU mean 64.6 °C, peak 79), and at about 95% with
another project's job on 0-7 beside it (peak 84, one second at 89). So card runs stay at 5,000.

**Where the card's wall goes now, and the items in hand** (probe runs on 2026-10-10, r57-s2 from 47,000 s
for 300 s; the record is `card-next-r57.md` Part 8 on `r58-runner`'s line). A simulated second costs
about 188 ms: the card's physics with its host parts about 104, the world's host phases 65 in steady
running, the harness 19 and the writers 9.5. The card is idle about 60% of the wall. Each item below held
identity on the card against `r57-ref58-card1`:

- `r58-copies` (`d2488f3c`): the copies cut to the used slots. It saves about 1.85 ms a block, about 2%.
  The download is bound by its count of copies, not its bytes.
- `r58-grow` (`3d053729` to `5e618441`): parallel passes over the world's body loops that keep the bits:
  growth, the mouth's intake, and Metabolise's shares and age step. The world step fell from 32.6 to 25.0
  ms, about 15 ms a simulated second.

The plain gain of each is measured in a day sitting, alternated (ABBA), with the solo baseline and a check
that the regenerated kernels cost nothing. Next is 1b, a dirty class uploading only its packed bodies (about
2.5 ms a block for a day). After it come fewer, larger download copies, and reuse of the phenotype's storage
in growth, which is to be reviewed before it is built. The snow's settling, mixing and remineralising could
move onto the card in double and in the CPU's order, as the transport did, keeping remineralise's serial
grand total on the host (an untested reading; `scratch/owner/snow-grid-on-card-note/`). That makes it a pace
change gated by identity, not a ruling: about 3 to 4% for 3 to 4.5 days, queued behind the arena and 1b. The card's stray test runs by day on the coordinator's go.

Four smaller leads are read, and none changes round 58. They were sittings of 2026-10-11, alone on the
machine (`card-next-r57.md` Part 9 on `nursery-speed`, `acca6d6d`):
- The mirror probe (`mirror-probe` `bc613ce4`) changes no number. B1's deferrable passes are about 1 to 1.5
  ms a block, 1 to 2% of pace, so `mirror-lean` stays parked.
- The digest's cadence is the farm's `EVOSIM_DIGEST_EVERY`, set outside the world's config. A row every 25 s in place of
  every second leaves every other output identical and gains about 1%, inside the spread of two runs. The
  card scripts hardcode 100 steps, so any change waits until after round 58 and is named in a prereg.
- Growth allocates about 10 MB a step, which sets off a gen0 collection in 60% of steps on the CPU engine.
  The server collector removes the pauses there, but gains nothing on the card and costs about 5.5 GB.
  Cutting growth's allocations in Core is a later item.
- The fluid test's swings came from tiered compilation, and the code and the machine are clear. Its fix is on main
  (`40ba8afd`): each loop warmed past the tiering delay, then the median of nine rounds.

Each sitting's script checks the built binaries against pinned hashes, and that every arm's name is unused,
before it starts, since a tree at the right commit can hold a build of other code.
The speed agent has the coordinator's standing go for off-card tests and builds, under the terms in
`scratch/machine/queue.md`. Every card sitting is short, attended, and starts on the coordinator's word. One water link's current costs
about 0.2 ms on the card, because single-precision `Atan2` has no card instruction and ILGPU runs it as a
software CORDIC in `Cap()`. Removing it would change Core's reef arithmetic and so every seed's
realisation. That waits for a measurement and the owner. The records are `card-audit-r57.md` and
`card-next-r57.md` on `nursery-speed`; the card tools are in `scripts/card/` on `card-r57`.
Jobs meant to stay off the card run with `CUDA_VISIBLE_DEVICES=-1`
and report whether they loaded `nvcuda`.

W0 is read (r57-s2 from 40,000 s for 300 s at 16 threads, alone; `nursery-speed.md`). The CPU farm ran
at 0.83 times real time: physics 63.5% of the wall, the world 33.6%. Within the world, one phase is 65%
of its step and about 22% of the run's wall. That phase is the corpses' step with the scent's fill
(`ScentField.Fill`), which clears the scent grid and re-adds every corpse's 10 m cone on one thread at
every metabolic step: 42,394 corpses, about 56 million cell visits a step. The snow's transport is the
next phase, at about 15% of the world step; the rest are small. So stage 2 starts with a bit-identical
fill on many threads, which serves both engines. The guess is 133 ms down to 10 to 15 ms a step, about
1.25 times on the CPU farm. 51 writes it on `farm-speed-r57` beside P10, F1 and F2, and its proof runs on the
coordinator's go. What loads the machine, and in which order, is
`scratch/machine/queue.md`, which the coordinator keeps. A1 and A3 came into main on 2026-10-05 (`7887fbc`, `cbc8790`). The exploration's nursery noise floor is measured (`nursery-speed` `c01ac73`,
`logbook/specs/nursery-speed.md`): a paired option is within noise at rank correlation 0.792 or more
with 6 of 8 elites kept. Its nursery options are measured (`nursery-speed` `c34ca95`, the table in
`nursery-speed.md`). B4 on 2.5 m cells is 3.2 times a generation and changes the experiment. B4 on
1.25 m cells is 1.7 times and changes the experiment too, since every body earns nearly twice as much.
B1, 16 bodies a tank, is 1.7 times and small: the best bodies score as today and the typical one 39%
less. The two together are 2.5 times. The owner adopted B1 for every nursery run from now on and
held B4 (D150). The farm's nudge switch (`EVOSIM_NUDGE`, `ed2949a`) is proven
byte-identical when off. A last-bit nudge to a body that lives through the window parts the whole
r56-s2 resume from 28,000 to 30,000 s, half the bodies elsewhere by about 29,000 to 29,600 s, so farm
options are judged by an ensemble, not by trajectory (`nursery-speed` `696f02a`). The nudged ends
differ by about 62 alive against 878 between round 56's seeds. The ensemble is base and four nudges
(4147, 4832, 3077, 5255): at 30,000 s alive 7,475 to 7,537, births 96,612 to 96,734, food 1,613 to
1,911 J. W1's three settings were measured on the farm and in the nursery overnight on 2026-10-05
(`nursery-speed.md`, "W1 on the farm" and "W1 in the nursery"), and the owner ruled D152: the
nursery holds the water 0.5 s, the farm's rounds do not, and one panel a side and no fluid acceleration
are dropped. The nursery's base recipe from now on is `rounds/nursery/brain2-w3-t16-hold.json` on
`round-57` (16 a tank and the hold); its generation 0 on round-57's build is byte-identical to the trial
the owner ruled on. The owner's D153 (2026-10-06) sets how a speed-up that changes the world is
judged: by its trade-off against the goal of very long runs of evolved bodies and brains, with an
exploit check and a full-physics reference build. 51's draft for round 58's candidates,
`proposals/propose-r58-speed.md` on `nursery-speed` (`1f05ef7`), is overtaken by D157 and D158; it is
re-framed from the step 13 plan before anything in it goes to the owner. The compute budget
(`logbook/specs/compute-budget.md`) counts the farm in generations per machine-day, about 73 a seed at
round 56's crowd, and sets each lever and each planned feature against it. The heat watcher
(`scratch/heat-watchdog/heat-watch.py`) is re-armed every half hour by the session's watch.

**A resumed brain-v2 farm run may part from the run it resumes, by the scent field alone.** The
brain-v2 members a restore leaves at zero (`Brain._portValue`, the scent, drive-sense and `BendSignal`
caches) are written in full before they are read in every physics step (`DynamicsWorld.StepOne`), and
the checker skips them. The scent field (`ScentField`, derived and not carried) is not safe:
`World.Step` fills it from the corpses, and the farm's growth step then runs after it
(`Simulation.Metabolise`), where a module drop leaves its remains as a corpse, as does a body killed
for diverging in a physics step. The live run smells those from the next metabolic step; a restore
refills from every corpse and smells them at once. The check on round 56 seed 2's 30,000 s checkpoint
(`logbook/specs/r56-build/verify-checkpoint-r56s2.log`) differs in that field alone, in every body's
reference to it, and its twin worlds agree for 100 physics steps and 2 metabolic steps. Round 56 seed
2 resumed from its 28,000 s checkpoint did reproduce the round's record to 30,000 s, byte for byte
(`nursery-speed` `696f02a`), so the gap does not bite at every checkpoint. A resume is a valid run of
the world either way; it is not always the same realisation. Resumes from one checkpoint start alike,
so the nudge ensemble and W1's trials compare fairly. Round 57's build closes the gap without
changing the world: the checkpoint carries how many corpses the last fill covered, and a restore
fills from that many (`logbook/specs/r57-build/scent-restore.md`). On it the check passes on the
screen's 1,000 s checkpoint (`logbook/specs/r57-build/verify-checkpoint-screen-1000.log`), so a
round 57 seed may be stopped and resumed. Main's build keeps the gap until round-57 merges.

### The rounds

| Round | Where it stands | What is owed |
|---|---|---|
| 50 | Run and read (D125, pre-registration 0123; `logbook/specs/r50-read/read-end.txt`). W1, M4 and S3 fail; V1 waits for its film windows | V1's windows from the round's own checkpoints, the theatre's pictures, entry 0124 |
| 51 | Run and read (0125 on `round-51`, amended as `ed4a61d`; `logbook/specs/r51-read/final.txt`). 32 clauses hold, V1 among them; B1, W1, EK5, E3 and S3 fail; O4 was not asked | The pictures and entry 0126; `round-51` and `round-51-tests` merged into main once the films of rounds 49 and 50 are done; a literature round for D128's sources |
| 52 | Run to 30,000 s (0127; seed 3 as `r52-s3j`); the read is `logbook/specs/r52-read/read-30000.txt` on `round-52`. Seed 1 extended to 50,000 s as `r52-s1z` | The pictures, entry 0128; seeds 2 and 3 to 50,000 s when a lane is free, joined with `stitch-resume.py --header-from` |
| 53 | Read in entry 0132 | Nothing |
| 54 | Read in entry 0131 | Nothing |
| 55 | Run and read: all three seeds ended on budget at 30,000 s (the control `r55-s1` at 22:57 on 2026-10-04, 4,477 alive). Pre-registration 0134 (`dcd21cc`); configHash `d573544fd7837861` for seeds 2 and 3, `54137db3752eebef` for the control. The read is `logbook/specs/r55-read/read-30000.txt`, round 54's reader `r54-reader-30000.txt`, the jointed nursery-rooted `jointed-nursery-30000.txt` (by `scripts/reads/r55-entry/jointed-nursery.py`), and the interim readings with who starves `interim-2026-10-04.md`. I1 fails (nursery-rooted 4.0% and 0.1% of the living against 5%); I2 is not read; I3 fails (jointed share 0.4% and 0.1% against the control's 5.0%); B1 holds on all three. Every jointed nursery-rooted body alive at the end is a founder or nearly (15 of 16 in seed 2, 8 of 8 in seed 3, zero generations below it); the one nursery line that lasted, seed 2's 180 rigid absorptive bodies, sits a median 19 generations down and dropped its joints. The floor's pool share barely took: D116 lands a founder only where its own food is, and at the start the nursery's matter-eaters' food sits only in the islands. The owner ruled on 2026-10-04 to let the round run as it is | The pictures; the entry, which says why the floor's share barely took and that I3 compares across seeds (seed 1 was round 54's one jointed seed too) |
| 56 | Run and read: all three seeds ended on budget at 30,000 s (8,112, 7,537 and 8,415 alive; the last at 06:54 on 2026-10-05). The read is `logbook/specs/r56-read/read-30000.txt` and round 54's reader `r54-reader-30000.txt`, each seed against round 55's same seed. G held. S1 fails on every seed: the year's fall from its first half's most to its second half's fewest was 7% and 19% (seed 1), 22% and 10% (seed 2), 18% and 19% (seed 3) against 20% in both years. S2 fails on every seed (jointed share 5.3%, 0.33% and 0.05% against 5.0%, 0.38% and 0.11%). S3 holds on seed 3 alone (the leaves' median margin 43, 116 and 167 s against 328, 422 and 131 s). B1 holds on all three. The crowd was larger in every seed (1.8, 1.5 and 1.1 times round 55's), and one leaf lineage held 99.9%, 99.8% and 99.8% of the living against 92.9%, 95.9% and 91.1%: the cycled light tightened the hold | The pictures at a summer's peak and a winter's trough of seed 2; the entry, which has to explain the larger crowd under a light that cuts income |

**Round 56.** Round 55's world with days of 1,000 s and seasons of 10,000 s, both at amplitude 0.5,
and no light sense (the owner's rulings of 2026-10-04; `proposals/propose-r56-light-cycles.md`). The
seeds keep round 55's roles, seed 1 the uninoculated control. Every seed is a full 30,000 s run, on
branch `round-56` (`scratch/wt-r56`; seasons `03bf4da`, fixtures pfix20 `52a1d4c87b6ea69c` and
`r56fix-s4`, every suite passing). The pre-registration is `logbook/0135` (`552834e`), read by
`scripts/reads/r56-read.py` (`--gate` for G). G held on seed 2 at 10,000 s: 2,407 living against round
55 seed 2's 2,031 (1.19, bar 0.25). Seed 2 ended on budget at 30,000 s at about 01:25 on 2026-10-05.

**Round 57 is two seasons a year and day-to-day weather** (D151, `proposals/propose-r57-weather.md`):
a 6,000 s year of three summer days at 1.6 and three winter days at 0.4, a log-normal weather factor
each day, no inoculation, three seeds to 48,000 s. The build is on branch `round-57` in
`scratch/wt-r57` (`logbook/specs/r57-build/build-brief.md`): the five light tunables, the scent field's
exact restore (above, under the resume) and the contact grid's reach (a body whose sphere strays out
of the grid is killed as diverged). Its suites pass (Core 1,137, all; Dynamics 174; Farm 208), and a8
migrated the nursery onto it (`-w3` recipes, nursery 212 and room 682 passing). Its launcher is
`rounds/env-r57.ps1` on the branch: every seed as round 56's control seed, weather seed 57. The
water current's 6,000 s period now equals the year. The one-year screen `r57-screen-s2` (seed 2,
6,000 s) ended on budget with 2,145 living: the founders' burst peaked at 5,159, the first winter took
the crowd from 2,554 to 1,311, and the tank refilled to about 1,840 before summer. The pre-registration
is `logbook/0136` (`1917815` on `round-57`), read by `scripts/reads/r57-read.py` (W, R, T and V on years 2
to 8, since the screen showed seed 2's year 1; B1 by round 54's reader). The literature round behind it is
review round 7 (Q12, `research/LITERATURE-REVIEW.md`), its working files `logbook/specs/r57-lit/`. The owner ruled on the
morning of 2026-10-06 that round 57 runs as built, with no world-changing speed-up (the farm's water
hold and the others are candidates for round 58, each measured first), and that it may launch once its
pre-registration is committed. It launched at 07:58 on 2026-10-06 from `scratch/wt-r57` into the main
tree's runs: `r57-s1` on logical 16 to 23 and `r57-s2` on 24 to 31, 8 threads each, every manifest naming
`1917815` with no DIRTY mark. Seed 2 replayed the screen to 6,000 s, every stats row and all 23,664 lineage rows identical. Seeds 1 and 2 ended on budget at 48,000 s on 2026-10-07: seed 2 at 01:46 with 8,438 alive and
182,150 births, seed 1 at 03:38 with 8,245 alive and 167,088 births, none diverged. `r57-s3` launched at
03:39 at 14 threads on logical 18 to 31 (run `runs/r57-s3/2026-10-07-023901-dfc0e881`, pid 287448), its
manifest naming `1917815` with no DIRTY mark, leaving 16 and 17 for builds. The coordinator stopped it
by mistake at 08:53 on 2026-10-07 (its stop checkpoint 21,710 s) and resumed it at 08:54 from that checkpoint, same
build, threads and processors: run `runs/r57-s3/2026-10-07-075434-dfc0e881`, pid 46128, by
`rounds/r57-resume-s3.ps1`; it ended on budget at 20:46 on 2026-10-07 with 6,890 alive. Its two
runs are joined at 21,710 s in `runs/derived/r57-stitch/r57-s3/2026-10-07-023901-dfc0e881-joined` by
`stitch-resume.py --header-from` (the stitch root holds junctions to the three seeds' runs, so the reader
takes `--runs-root runs/derived/r57-stitch`; read `logbook/specs/r57-read/r57-read.out`). Round 57's verdict by
`r57-read.py`: T holds 3 of 3 (the summer's first-day share of leaf births rose a little in every seed),
W 2 of 3 (seed 3's winters bit only 15% and 18% in years 2 and 3 while its crowd grew), V 2 of 3 (seed 3's
spread widened), and R 0 of 3 (the year-8 median rm fell in every seed: 58 to 44 s, 440 to 350 s and 154
to 132 s). Beyond the clauses, seed 2 grew one stomach line with no light tissue, from a one-part trickle founder at 5,178 s, to 1,252 living at the end, half of them within 2 m of the bed; it passes all four of D063's clauses as a state (`clade-score.ps1`), as do 9 small stomach lines budding off seed 3's leaf line, and seed 1 fails on stability. The entry is `logbook/0137` on `round-58` (`26bfc328`), with three pictures in the new look. Its step costs more than round 56's at
the same crowd. At about 7,500 alive, seed 2 takes 2.0 s of wall a simulated second against round 56
seed 2's 1.5. The rise sits mostly in the stats' `wallWorldMs`, outside the physics: 0.50 s against
0.22 s. Physics rose less, 1.49 s against 1.24 s. Neither run was a timing read, so read it alone
before blaming a rule; the seasons' and weather's per-step work is the first suspect. The light sense
(D154) is built on branch `round-58`, off by default, and the owner ruled on 2026-10-07 that it is
premature and not round 58's experiment. Its nursery light bench is parked (a8 told; nothing deleted).
The hollows (D155, `logbook/specs/r58-build/hollows-design.md`, built on `round-58`) failed their bar
for a material change in two screens (`logbook/specs/r58-build/screens-h12-read.txt`) and are set aside
(D156). The owner's other focus, set on 2026-10-07, is a render that feels more underwater without
losing its 4K clarity, with the world above the water shown; its choices are the coordinator's. The look: an Opus survey (`logbook/specs/underwater-look/survey.txt`) and a Sonnet survey of
underwater rendering, on the owner's suggestion that the water itself be visible. Its first batch is
on main, off by default and on with `EVOSIM_THEATRE_LOOK=3` (`unity/Assets/Theatre/CLAUDE.md`), checked
on before-and-after stills of r57-s2 and r56-s2 (`scratch/owner/underwater-look/`); the countdown film
renders with it. The first batch lifted the darks into a veil: on the creature close-up the luma spread
fell by 30% (`scripts/underwater-look/contrast.py`), as the owner suspected. On 2026-10-07 the owner asked for a
second batch, iterated until the pictures are really nice: contrast first, with the snow drifting with the
run's own current at its own sink rate and its density taken from the run's recorded snow
(`logbook/specs/underwater-look/brief-2.md`). It is on main (`d767551`), off by default and on with
`EVOSIM_THEATRE_LOOK=4`, after 22 iterations of stills and two clips (`scratch/look-build/iter/`,
`scratch/owner/underwater-look-2/`); the owner liked the clip. Round 57's snow is nearly even (0.7 to
1.3 J per cubic metre across the tank, a tenth over the year), so the drawn snow looks much the same
everywhere in it. A clip is checked frame to frame with `scripts/underwater-look/motion.py` (no frame's
change above twice the median), which found the snow's half-second jump the owner saw in the first clip.
Still weaker: the winter sky from below and the whole-tank views from outside. Not started: the camera
above the surface and the waterline shot. An open matter budget (D074's influx and
burial, sources that move) was discussed as a later stage and not ruled.

A round's farm build stays as long as its runs do, because `film.py` refuses a run whose
`programPath` is gone: `scratch/wt-leafincome` holds round 50's build, and `wt-r51` to `wt-r56` the
rest.

### The nursery

- **The code** is the recipe nursery (D148) with brain v2 (D146, D147) and the column advection, on
  `nursery-general` (`f6f01f6`, `scratch/wt-nursery-general`). A run is a recipe in `rounds/nursery/`,
  and the registry is the main checkout's `rounds/nursery/registry.jsonl`, with five entries: the pilot
  `brain2` (a reference entry, never rerun), `accept-1gen` (the room's acceptance, not rerunnable),
  `brain2-rep2`, `brain2-nodrive` and `brain2-nocross`. The record of every run and exam is
  `logbook/specs/nursery-plan-joint-drive.md`, and entry 0133 tells it. In short, body 10's brain beat
  its best steady stroke in both brain v2 runs and nothing else repeated; no brain reads the scent; the
  separation arms showed no effect of the drive sense or the crossover at one seed.
- **Round 55's inoculants** are `inocula/r55-nursery/` (341 genomes and `origins.tsv`).
- **No nursery run until its pace is understood and sped up** (the owner, 2026-10-04 evening). The
  slot of 2026-10-05 measured a generation at 300 to 307 s on 14 efficiency cores with 600 s episodes,
  with the threads busy 68% of it. `Parallel.For`'s ranges leave the last ten tank jobs to one thread,
  which runs them one after another for about a third of the generation. A1 hands the jobs out one at
  a time; 51 estimates about 1.4 times a generation from the profile, bit-identical by construction and to
  be proven by digest. Brain v2 costs 1 to 2% more than the old brain on one core. The world is 68% of a
  tank's time and the physics 31%. Ending a tank early once all its bodies are dead saves nothing, since
  no tank had all its bodies dead.
- **All cores for nursery runs.** The owner ruled on 2026-10-05, on 30-minute heat readings with A1,
  that long nursery runs stay on the efficiency cores, and that a job of 30 minutes or less may use all
  cores, by day, under the heat watcher, while the machine is otherwise free. A1 pinned takes 208 to
  213 s a generation and peaked at 88 °C, none of it at 90 °C. A1 on all cores takes 139 to 141 s and
  peaked at 94 °C, with 13 s at or above 90 °C and none at 95 °C (`logbook/specs/nursery-speed.md`).
- **The speed-up agent** is session evolution-simulator-c9, put on it by the owner on 2026-10-04: branch
  `nursery-speed` in `scratch/wt-speed`, findings to `logbook/specs/nursery-speed.md`. It reads and
  compiles at 2 processors and runs nothing else without the coordinator's go. Its bit-identical
  candidates, each to be proven by digest at 1 and N threads: a serial path when a world steps on one
  thread, folded tables in the point samplers, a fused tank point sampler, and an early-out in
  `ContactBed`. Turning the trace off in the nursery (1.3%) is left unbuilt until the slot's numbers are
  in. Its timing slot runs merged with main at `544f809`, against a reference built
  from `544f809` in `scratch/wt-speed-ref`.
- **The nursery on the card.** The owner asked on 2026-10-04 whether the nursery will use the card, and
  said yes to a plan. The plan is `logbook/specs/nursery-gpu-plan.md` (Fable, saved as it came, `dc199a5`
 ). The card refuses the nursery's world today (`FarmGpu.cs`: brain v2, the scent, the
  limb push and the bend-and-power drive are not ported). The plan runs all 192 tanks of a generation in
  lockstep, the snow's four field passes batched on the card in double and the economy kept on the host:
  about 2,900 lines, 9 to 13 agent-days on ILGPU's CPU accelerator, then half a day of card after the hold
  for the first number. It expects about 3 times a generation (1.5 to 6), at most 4 to 6 times with more
  tanks. Nothing is built. The owner ruled on 2026-10-05 not to fund it yet, and that evening ruled
  that round 57 runs before more farm speed-up, and that the card's numbers are fetched once its hold
  ends: the speed plan's step 13 measurements (stage 0a's kernels, then the floor test and a
  class-sized double step on a farm crowd, about 3 to 5 agent-days and half a card day), so the owner
  can decide the big port, in double, in single or not at all, on them. On the card the solver is
  about six times the CPU in single precision, which is a new realisation of every seed, and slower
  than sixteen cores in double. The farm's snow transport on the card (T1, queue item 7) goes ahead
  once the card's hold ends. Step 13 in full is 7 to 10 agent-days and about 6 card-hours. Its first
  part (stage 0a's kernels, the class-sized farm harness and card session 1, 3.5 to 5.5 agent-days and
  about 2 card-hours) waits for a smaller experiment, which the owner ruled on the night of 2026-10-05
  runs first; its second part (stage 0b and the nursery floor test) waits for session 1's numbers and
  goes to the owner with them. The small experiment (its CPU half done on 2026-10-06, `nursery-speed.md`
  "Round 53 on the card": the five-run ensemble from r53-s2 at 29,000 s spans 6,794 to 6,842 alive and
  −13.15 to −13.04 m at 31,000 s, against round 53's seeds' 316 alive and 1.72 m): round 53's world,
  the newest the card's engine runs whole, resumed near 29,000 s with about 7,500 bodies on round 53's
  own build (`EVOSIM_ENGINE=gpu`, `EVOSIM_GPU_PRECISION`). The plain timings use `scratch/wt-r53`'s
  exe unchanged; the nudge ensemble runs from `scratch/wt-card53` (branch `card-53`, off `round-53` at
  `340e21f` with the nudge cherry-picked), proved by an off check against `wt-r53`'s own exe, which
  nobody modifies. It times the CPU at 16
  threads, the card in double and the card in single, then reads single over 2,000 s against a CPU
  nudge ensemble from the same checkpoint and against round 53's seed spread, with its audits.
  Its card half ran on the evening of 2026-10-07, 20:48 to 21:56, with the machine cleared but for the
  owner's Unity Hub (about one core), with no driver events (`nursery-speed.md`, "Round 53 on the card",
  nursery-speed 3cd2675). At about 7,400 bodies: CPU at 16 threads 1.13 times real time, the card in
  double 0.21 (5.5 times slower), the card in single 2.0 (1.75 times faster; its physics 2.35 times, with
  the world on the CPU 31% of its wall, so this step's ceiling is about 2.5 times unless the world moves
  too). Single at groups 32 and 64 is identical. At 31,000 s single sits just above the five CPU runs'
  range on alive, births, light and food (0.3 to 0.8 of the range's width) and inside it on height: not
  settled. Of three courses put to them (A, step 13's stage 0a as planned; B, a fidelity check first; C,
  the card set aside for the CPU-side speed work) the owner chose B on 2026-10-07 at about 22:55. Its read
  was fixed before any run (nursery-speed `d0a0d0dd`, `scripts/reads/fidelity-read.py`): the card's mean
  against the CPU's at 31,000 s on alive, births, light, food and height in CPU standard deviations;
  within 1 on all five is a realisation, beyond 2 on alive, births or light a shift, anything between
  unresolved. It ran from 22:58 to 01:56 on 2026-10-08 and gave no verdict: the single-precision card
  stores positions as 32-bit floats, so a nudge of one part in 10^12 rounded away and all four card runs
  equal the un-nudged one (nursery-speed `1246ff7b`, "Round 53 on the card"). Two CPU members did not part
  either (5023 rejoined the base at 30,640 s, 3785 ended level with it). Descriptively, the one real card
  run against the seven CPU members that parted sits at z +2.2 alive, +1.7 births, +1.8 light, +1.6 food,
  0.0 height. The check is being finished as instrument work within B: a nudge-size setting
  (`EVOSIM_NUDGE_SCALE`, default unchanged) on `card-53`, the card nudged at 1e-6, a CPU member counted
  only if its end row parts from every other on alive or births (fixed after seeing the two that did not,
  and judging only whether a run parted), all committed before the runs; then four card runs and two CPU
  replacements (one at 1e-6 as a scale check), about 2 h 20 min, on the coordinator's go after H1 and H2.
  The owner ruled on 2026-10-08 (D157) that the card is a valid engine in single precision if its world
  is sound (books closed, self-replay, nothing diverged), whatever this check reads, so it reports as
  information; that no comparison mixes card and CPU runs, and an engine change lands at a round boundary
  with its own baseline; and that the work goes on moving as much of the world onto the card as makes
  sense. Step 13 is go, re-aimed at single: 51 drafts the re-ordered plan (what moves, in what order, gain
  and cost each, the ports rounds 54 to 57 need, the soundness checks), checked by the coordinator before
  any machine time.
  Round 58's hollows screens H1 (`r58h1-s2`, the pits alone) and H2 (`r58h2-s2`, the pits and the slide)
  ran from 01:57 on 2026-10-08 from `babb834` on a clean tree, seed 2, 8 threads each, and ended on budget
  at 10,000 s at 03:50. Both beds hold the same 12 pits. Read by `scripts/reads/r58-hollows-read.py` (main
  `bfff932d`, D155's bar fixed before the screens; output `logbook/specs/r58-build/screens-h12-read.txt`),
  neither meets any of the three tests. The catch, corpse joules per square metre inside the pits against
  outside, is 1.35 in H1 and 1.28 in H2 against 1.31 for H0 (r57-s2) on the same cells, bar 5. No guild
  lives near a pit floor at twice its share, and the living and guild shares stay inside round 57's range.
  The slide works within a pit: H2's pit floors hold 1.85 times the open bed's corpse joules and its walls
  0.95, against 1.38 and 1.34 in H1. Nothing outside a pit is steeper than the 30° repose, so a pit gathers
  only what lands on its own footprint, and even all of it on the floor would be about 3.5 times the open
  bed. So by that geometry (an inference, not a run) no scale of this shape, and neither of D155's
  fallbacks (H3's larger pits, option (c)'s lodging inside the rim), reaches 5. Seed 2's stomach line of
  round 57 did not appear in either screen (0 and about 4 pure stomachs over 9,000 to 10,000 s against
  about 56 in H0). The owner ruled on 2026-10-08 that pits which do not materially change their
  surroundings do not go ahead for now (D156): round 58 does not run the hollows, whose code stays on
  `round-58`, off. Round 58 is the migration design instead (D159).
- **The farm rounds' pace falls as the crowd grows.** Round 56 seed 2 ran at 3.1 times real time with
  1,500 bodies and 0.66 times with 7,500 (sharing the machine). The profile of 2026-10-05 resumed it
  from 29,710 s with about 7,500 bodies, at 8 threads with nothing beside it: 0.88 times real time. Of
  the wall, the physics was 81% (the bodies' phase 67%, building the contact grid 13%) and the world 16%.
  Sampled, the water at every link is a third of the time and the contact search a quarter
  (`ContactGrid.NeighbourLinks` 16.5% on its own); the articulated solve is under 5%
  (`logbook/specs/farm-profile-2026-10-05.md`). So a fused water sampler pays in both programs, and the
  contact search is the farm's second lever. Whether the farm's `Parallel.For` leaves the nursery's
  one-thread tail is not measured. The card refuses these rounds too, for the same unported pieces, and
  its transport check (queue item 7) is about 20% of the wall once the hold lifts (51's estimate).
- **The next nursery design**, discussed with the owner on 2026-10-04 and not proposed: a dark, leafless
  tank with food measured from the rounds' matter-eaters, bodies and brains evolving together by
  mutation alone, newborn starts, episodes of about 1,500 s, scored on time to a fixed breeding gate (the
  body's own child cost and a fixed reserve floor) with the energy banked and then survival as partial
  credit, never distance, and niches by body type. At the column kernel's pace a 1,500 s episode puts a
  generation at about 12.6 to 12.8 minutes on 14 efficiency cores, 51's estimate, to be firmed up by its
  slot.
- **The nursery room** is on main, built by a8 to `logbook/specs/nursery-room-build.md`, with the rule that a
  screen change works only when the end-to-end flows pass (`Category=E2e`). Its saved answers (`Golden/answers/`)
  are re-recorded only on purpose, with `ROOM_GOLDEN_UPDATE=1`. On main the room suite passes 681 of 686, 4 skipped,
  and its one E2e failure is Edge's locked profile folder on this machine; the nursery's passes 353 of 354. The server
  is down, as the owner asked on 2026-10-15, and comes back only on the owner's word. It runs on `127.0.0.1:5179`
  from the main checkout's Debug build (`scripts/room-server-build.ps1`), with its logs in `scratch/room-server/`,
  and comes down for any card run, since the card launcher refuses any `dotnet.exe`. The main checkout's
  `rounds/nursery/room.json` carries two uncommitted edits for it: the fresh run `scratch/nursery/tank-demo-w2` in
  `runs`, and `affinity` set to the room's slot. a8 asks the coordinator before anything heavy runs
  (`scratch/machine/queue.md`; the owner, 2026-10-04). The walks' small leftovers are built only if the owner asks.
  Builder briefs forbid going round the guard's system-path block with `rm`.
- **The swimming nursery's leaf tank (D166, D167)** is built by a8 to `logbook/specs/nursery-leaf-tank-plan.md`,
  whose steps hold each arm's read; all of it is on main. Where it stands:
  - Swimmers evolved in the 400 m² tank (D173, D174) eat almost wholly by the brain, and the direction smell at 6 m
    pays. Their ten best tops are `inocula/r59-direction-tops/`, which round 59 planted.
  - With evolving bodies, net energy (D178) breeds idlers; the energy return (D179) breeds cheap foragers that sit
    near break-even and eat less than half what the tops ate, on two seeds (the plan's step 14).
  - In D184's patchy tank (`leaf-direction-patches`, registry 20, the plan's step 15) the smell carries nearly all the
    eating, 83 J against 9 J without it. Re-stepping the exam with the patch watcher (`--restep --patches`,
    `scripts/reads/patch-*.py`, `founders-batch.py`) puts the share of time within smell of a leaf at 0.44 evolved,
    0.41 for the founders and 0.40 with no brain. The bodies keep to patches little better than drift, and the
    smell's value is in reaching a leaf.
  - Round 59's clause D2 swam the planted-descended grazers alive at 48,000 s (227 and 362 distinct genomes, pools by
    `scripts/reads/r59-d2-pool.py`) in D173's tank (`r59-d2-s2` and `-s3`, registry 21 and 22), read by
    `scripts/reads/r59-d2-read.py`. The knockout costs 2.1 J of 22.2 J eaten in seed 2 (1.6 standard errors, not
    met) and 1.6 J of 10.1 J in seed 3 (2.8, met).
  - The patch read is the plan's step 15 and logbook/0146; D2 is the plan's step 16 and logbook/0141. The reads'
    outputs are `logbook/specs/nursery-patches-read.txt`.
  - The nursery is on round 60's config format (D185). Its world configs are `a8cbe76c9532bc71` (brain2's `-w5`
    recipes), `2b125f6cbda84a01` and `e80c201e54b3963f`; recipes on older configs are refused.
  - Held until the coordinator says (the owner, 2026-10-15: fewer streams): the patchy tank's repeat
    `leaf-direction-patches-s2-w5`, its founders' baseline (`leaf-direction-patches-s2-founders-w5`), and any new arm.
  - A recipe whose exam would swim an episode its search swims is refused at load (`EpisodeSeeds.cs`). No nursery
    branch is rebased: when one needs main, main is merged into it.
- **The room's next work, as the owner ruled on 2026-10-11**, its timing agreed with the coordinator
  (evolution-simulator-01) and booked in `scratch/machine/queue.md`:
  - *The tank screen* steps only runs on today's build, since every registered run is on an older build
    whose world today's build refuses. The fresh run `tank-demo-w2` (above) gives it one. Re-stepping an
    old run with its own build waits on the owner: the nursery's `--restep` arrived at `8489b8df`
    (2026-10-02), after every registered run's commit, so those builds cannot re-step without a graft.
    The choices put to them on 2026-10-11 are re-stepping on the run's own commit for runs from
    `8489b8df` on, a graft for the five older entries, and the proposal
    `logbook/specs/nursery-room-unloadable-recipes.md` on branch `nursery-room` for a clean refusal.
  - *The cap's merge* waits on the owner: branch `room-cap` (`6d36a4a2`), the full room suite 698 passed,
    4 skipped, 0 failed. On merge, the main checkout's `rounds/nursery/room.json` takes the branch's
    keys, with the fresh run's `runs` entry put back, and the server is rebuilt and restarted.
  - *The genome screen.* Opus's design (`scratch/room-genome-design/opus-options.md`) goes to Claude
    Design for a visual pass, in the owner's session, beside the room's mockups in `design/nursery/`.
    It has three parts: a strip of what switching each sense off costs, a dial per joint saying whether
    anything can make it move, and a moment-by-moment strip of what drove each joint, which needs the
    re-step above. Nothing is built before the pass comes back. Its brief is
    `design/nursery/BRIEF-genome.md`, with the options beside it as `genome-options.md`; which design
    session takes it, the one that drew the room or a fresh one, is the owner's choice.
  - *The room's load cap* stops reading % Processor Utility, which counts a boosting core as more than
    one and so refused even the room's own one-thread STEP. It counts the jobs running in the machine's
    queue instead, with the heat check (`scripts/temp-check.ps1`) as its backstop, following the owner's
    rule of 2026-10-09 that load is no bar and heat is. The brief is
    `logbook/specs/nursery-room-queue-cap.md`; it is built on branch `room-cap` in `scratch/wt-room-cap`,
    with its tests run by `logbook/specs/nursery-room-queue-cap/pass.ps1`.

### Other work and the disk

**Git.** GitHub's main is one squash commit (8bb29ad8, merged 2026-10-18) on the history pushed before 2026-10-01, and local main matches it. The full local history is the local branch `archive/local-2026-10-18`, never pushed. A branch started before the squash (`r62-blades`, `r62-coral` and the rest) lands on main only by `git merge --squash`, or the old commits come back; the pre-push hook refuses them anyway. This repository's `user.email` is the GitHub noreply address. The pushed branch `squash/2026-10-18` is still on GitHub, and deleting it is the owner's call.

**The scratch review** (`scratch/scratch-review/review.txt`) is approved and its deletes are done (`scratch/scratch-review/delete-log.txt`). The run directories the record names are in main's `runs/`, those that are not one arm's runs under `runs/derived/`, with each old path in `logbook/specs/run-moves-2026-10-18.txt`; runs a nursery registry row names stay in their worktrees. The files the record cites are promoted to their homes (2026-10-18). Three span launchers stay in their worktrees until a line naming another project is scrubbed: `rounds/r60-ov-span.ps1`, `r60-det-span.ps1` and `r60-cap-span.ps1`. The coral's step 1 is on `r62-coral` (2559dc3f, in `scratch/wt-coral`); Core -All passed 1,238 of 1,238 on it on 2026-10-18.

Another session works on the story tools: `.claude/skills/story-resolve/`, `assets/`,
`logbook/specs/story-resolve.md` and the scripts named story-resolve are left alone. Round 48's story
film is delivered (`scratch/owner/r48-story-full/r48-story-full-v2.mp4`) with three flaws the owner
knows of. A story film for rounds 50 to 54 waits (queue item 9).

A ten-creature countdown film is being made by the video agent (session evolution-simulator-a3) with create-story-video. The
owner set it going on 2026-10-06. Its picks are `logbook/specs/creature-video-picks.md` and its
folder is `scratch/story-video/rounds-53-57-202610061945/`. It renders and films only on the
coordinator's go. The owner wants no creature in it that looks disjointed, so the theatre bridges
every joint from its anchor to both drawn surfaces (`logbook/specs/theatre-joins/brief.md`, on main).
The redrawn stills are in `scratch/theatre-joins/after2/`. r56-s2 #90944's edge blocks still read
loose, and it is out of the film. Worker 6 is refreshed from main before the film's first render. Its
narration stands at `prose-13.md` in that folder, which passed every check and waits for the owner's
approval and key-line picks; `overnight-summary.md` there sets out each decision the owner is asked for.
The prose step now writes in passes under a producer's question, and its brief and checks are uncommitted
in `.claude/skills/create-story-video/`.

A story film on round 57, following on from round 49's, is being made by the same session with
create-story-video; the owner set it going on 2026-10-08. Its folder is
`scratch/story-video/rounds-50-57-202610081356/`, where `pick.md` holds the owner's direction, angle and
amended question. Its story (`story.md`, from draft 8) and its prose (`prose.md`, from draft 7, after the
owner sent draft 4 back as a teacher's voice) are approved, and so is its script (`script.md`, from draft 1).
The film is to be released the weekend of 2026-10-10.

Its narration is read by Google's hosted Gemini 3.8 Flash TTS, on the free tier, which may train on what it is
sent; the owner chose it on 2026-10-09 and approved sending it the script. Qwen, the local service's model,
lost the British accent in every clone and drifts American in its own design clips, and four local models
auditioned through ml-store were not chosen (`logbook/specs/tts-*-2026-10-09.md`, `scripts/tts/`). The voices
are M3 (a man) and F4 (a woman), designed from one description each and read with round 3's style line; the
ids are in the gitignored `scripts/tts/google/voices.local.json` and the key in the gitignored `env.local`
there, which no agent reads. `scripts/tts/google/narrate.py` reads the script one beat a request, the owner's
choice; `scripts/tts/align.py` times every word against the known script (the align step of the owner's
synthetic-signals project, on the CPU); and `scripts/tts/google/to_narration.py` runs the beats and their
times through narration.py's own cut, lay-out and check. The free tier allows this model 10 requests a day and
3 a minute, and the day resets at midnight Pacific time.

M3 has read all nine beats (`scratch/owner/round57-narration-google/`), and the owner was very happy with it.
Narration draft 2 in the film's folder is M3's, its check passed, 51 pieces and 7:19.5 long; the owner chose on
2026-10-10 to keep that length and trim in Resolve if it feels wide. It waits for the owner's approval by ear
before `keep` makes it the film's `narration.json`, and the shot list then runs on it. F4 has read beat 1; its
other eight beats go after the quota resets on the morning of 2026-10-11, and then join the
narration as the second voice, which `to_narration.py` does not do yet: it builds one voice's draft. The film's `narration-settings.json` still names Edmund and Sienna, which nothing reads
on this route. The edit step builds only the primary voice's version; a second audio version is the
owner's to rule on.

The owner's cleanup of 2026-09-29 kept only what the videos from round 50 on need. The runs that code
and tests read stay: r42-s1, r42-s4, r45fixb-s4, r37-s1, r25-s2, r25q-s2, th-ref, uicheck, r35tsmoke3,
boxdig-old, r20v-age1, pfix14, pfix15, and r48-s1's config with its 20,000 s snapshot. A round's
checkpoints are thinned only after the owner approves its video as final (`src/Evosim.Farm/CLAUDE.md`).

## The decisions in front of the owner

Each is put to the owner in full, with the options and what each implies, in any message that asks
for it.

- **An unpaid grip**, asked on 2026-10-13. A grip forms automatically on touching rock and charges its build
  price at once, so a body whose reserve cannot pay dies paying. Should a grip not form when the body cannot
  afford it? The build plan's owner item 7 on `r59-anchor` says what the code does now. Not urgent while the
  price is a placeholder. It scales with the gripping part's size: about 0.01 J for a 6 cm bud, and up to about
  2 J for a part 0.8 m across (computed from the price's formula, `2c4ef0f4`).


- **Windows Update.** It restarted the machine by itself at 01:29 on 2026-10-09. The owner paused updates
  for one week on 2026-10-12, to about 2026-10-19, which covers round 58. A long run planned past that date
  asks the owner to pause again first.
- **The card.** The grazer screen holds it next. Any card run asks the coordinator, runs alone under the heat watch, and needs the
  room server down for its window, since the card launchers refuse any dotnet. Filming runs worker 6's Editor on logical 0-15 and any farm window recording on 16-31
  (`farm_cores`), and never beside a card farm run.
- **The nursery's branches without worktrees.** `nursery-r57`, `nursery-d152` and `nursery-w5` hold
  commits main does not: the nursery's recipes on round 57's and round 58's config formats, the D152 base
  recipe, a room test and the build spec's note on the load cap. Merge or rebuild from them when the
  nursery resumes; `nursery-w5` is on the hollows build that D156 set aside.
- **Worktrees in use.** `wt-w0` holds the speed agent's reference build and stays while it is used.
  `wt-look` holds `look-2` and stays.
- **Left for later by the owner (2026-09-30):** the friction's value μ, friction between creatures, the
  producer threshold under D063, a tempo dial, the open proposals in `proposals/` (the own solver's
  rulings, the shelf reef and turbidity, predation on contact, the soup, and the animal kit, whose rung B
  is unruled; the owner kept predation and the soup live on 2026-10-01), and the lifespan literature's
  eight papers behind Europe PMC's Cloudflare page.
- **For the owner later, from the physics read** (queue item 6): N1 (the velocity read off the
  acceleration's gradient, a new realisation, about 5%), N2 (a cheaper floor, a world rule) and R2 (the
  acceleration sampled less often, a world rule). N3, the solver on the card in single precision, is
  ruled by D157.

Ruled on 2026-10-05 by the owner. The film of rounds 50 to 56 waits for a breakthrough worth telling,
and no story film is made meanwhile. The owner's subagent model rule goes into `CLAUDE.md`. The story
flow's `*-owner.md` files stay out of git (they live under `scratch/`, which git ignores), and the lessons
in them belong in the story skills. No commit history is rewritten, pushed or not, for the paraphrase.
The nursery on the card, if it is ever built: the bodies in double, with single measured once as a
reading; the field passes in double. The paraphrase pass was
reviewed and fixed by an Opus subagent and is on main.

Ruled on 2026-10-07 by the owner: no picture-only glow in the theatre (`proposals/propose-theatre-glow.md`
declined); glow waits for real bioluminescence as a world rule. The light bench is parked, and may be
dropped later. Round 58 is round 57's world with D131's hollows. The speed focus is the farm and the
world, toward bigger and longer runs without waiting days. The underwater look shows the world above
the water both ways: the sky and surface seen from below, and shots at or above the waterline.

Ruled and in force. Rounds 55 and 56 are the worlds above. D131's places on the bed come back one a
round, in this order: hollows, then a seep that charges matter (put to the owner again in full before
it is built), then the anchoring cell (`logbook/specs/r53-larder-proposal.md`). Links catch no light
(`EVOSIM_LINK_PHOTO 0`, from round 51), the pool keeps its one-part stomach, and round 52's lifespan
values hold (the repair price 0.5, the chance 0.08, the founders 0.5 to 1.5).

## Queued, in order

All of it is agent work. Long steps run in the background under the session, never in a subagent
and never in a shell loop; a queue that must outlive a turn is started detached.

2. **The speed program for the farm and the nursery** (the owner's ruling, 2026-10-04 night: a full
   speed-up of both). Led by the speed agent (evolution-simulator-08), one subagent at a time; the coordinator starts every
   digest check and timing read. The plan is `logbook/specs/speed-plan.md` (Fable,
   read-only, saved as it came; its brief beside it). It ranks 28 items, the bit-identical ones first, and
   orders the build in 13 steps, each proven before the next: a nursery change by the digest check, a
   farm change by its build against main's on a resume of `r56-s2` as a cousin, at 1 and 8 threads, every
   stats, digest and lineage row compared (`logbook/specs/nursery-speed/farm-a3-check.ps1`, read with
   `scripts/reads/gpu-accept.py`); today's build refuses every round's config before round 56, so a
   round's own rows cannot be the reference. Its projections, believed: the nursery from
   304 s a generation to about 165 s pinned, or 100 to 110 s on all cores; the farm 1.15 to 1.3 times from
   bit-identical CPU work, plus the card's transport after the hold. 
   On main since 2026-10-05: A1 (1.46 times a generation at 14 threads pinned, 209 to 211 s against
   307 s, the threads busy 98% against 68%) and A3 (about 6% of an episode at one thread), each with every
   result byte-identical. The owner's two heat readings with A1 are in (the decision is set out with the
   owner). Left, in order:
   - The owner has seen the ranking and left the order of the CPU work to the coordinator and 51
     (2026-10-05). It is the plan's: A3, the `WORLD_PHASE_PROBE` read, S3 with S6, A2 on the nursery
     path, A7 and A8, then the farm's A2 variants, P10, P9, F1 and F2, each proven before the next.
     The next is the `WORLD_PHASE_PROBE` read. Since A1 and A3 touched `Evosim.Dynamics`, main's farm
     refuses round 56's checkpoints by source; resume one as a cousin.
   - The nursery's trace stays on: 51 and the coordinator recommend keeping it (A9 saves about 2 s of a
     210 s generation), and the owner has not ruled otherwise.
   - The owner said yes on 2026-10-05 to starting the exploration this week, the nursery first, on one
     condition: the speed work and everything else never compete for the machine without each knowing.
     So everything that loads the machine is listed in `scratch/machine/queue.md`. The coordinator owns
     its order, and nothing starts without the coordinator's go. The design is
     `proposals/propose-speed-exploration.md`, each verdict's threshold written down
     before its trial: the nursery's floors first, then B4, B1 and B3 by the genome-rank test; on the
     farm the nudge read first, then R2. Adopting any option stays the owner's ruling.
   - **The speed-ups that change results are explored alongside** (the owner's ruling of 2026-10-05
     afternoon), not left until after step 7: a simpler sea floor (N2), the velocity from the
     acceleration's gradient (N1), the snow carried every second step or on coarser cells (B3, B4), the
     acceleration sampled less often (R2), and the rest of the plan's NR and WR rows. Each is measured
     for the speed it buys and for how far it moves a result, behind a setting that is off by default so
     the default build stays byte-identical. Adopting any of them is the owner's ruling. 51 drafts the
     exploration's design as a proposal first; each trial needs machine time from the coordinator.
2b. **Branch `round-58`** (worktree `scratch/wt-r58`, from `round-57`) holds the light sense, off by
   default (D154; brief `logbook/specs/r58-build/light-sense-brief.md`): `1e52471` Core, `970e4f5`
   Dynamics, `60b729d` Farm, `84633f7` build-readings. Off and unwired, the founder digest is unchanged
   (`fe66eefd0bc1bdf7`, 50 round-57 founders). Its fixtures are recorded (`pfix22`, `r58fix-s4`;
   `0a88c11`) and all three suites pass (Core 1,152, Dynamics 179, Farm 214;
   `logbook/specs/r58-build/suites.log` there). The sense is not round 58's experiment (the owner,
   2026-10-07), and the light bench on `nursery-light` is parked. `round-58` takes every main change the
   round needs before it launches.
   - **The hollows (D155)** are on the branch: `502b667` Core, `2233d44` Dynamics tests, `51a1ce1` Farm,
     `4f86f22` theatre (`SnapshotWorld.BuildFloor` calls `World.BuildFloor`, not yet compiled in Unity),
     `3c2eabf` build-readings, `b4aa995` the fixtures (pfix23 at round 42's pinned hash
     `cb07946e72e3fb98`, the crowd `r58hfix-s4`) and `rounds/env-r58.ps1` at screen H2's values. Core
     1,168, Dynamics 181 and Farm 218 pass (`logbook/specs/r58-build/suites.log`). With everything off,
     round 57's launcher replays r57-s2's rows and lineage for 600 s byte for byte
     (`logbook/specs/r58-build/hollows-off-replay.log`). Still owed: the theatre's compile on the branch
     and a round 57 picture through the new floor builder, then screens H1 to H3 against D155's bar
     after the card's first slot.
   - **The nursery on round 58**: a8's `nursery-w5` (`42e315c`, worktree `scratch/wt-nursery-w5`, off
     `3c2eabf`) migrates the nursery straight to `-w5` (config `ed0669b1eba1939e`, seven recipes; nursery
     214 and room 682 passing), rebased as `8d57072` and merged into `round-58` at `babb834`.
   - **51's bit-identical farm speed-ups** P10, F1, F2 and the scent fill split over threads are on
     `farm-speed-r57` (`287e7e2b`, worktree `scratch/wt-farm-r57`, from round 57's build). They are proved
     byte-identical against round 57's own build on the farm (r57-s2 at 5,000 s at 1 and 8 threads, and at
     40,000 s, about 42,000 corpses, at 1, 3 and 16 threads; Core, Dynamics and Farm suites passing) and in
     the nursery (both base recipes, scent on, generations 0 and 1). F2's attribute is dropped (unsafe stays
     off). The branch goes into the next round's build when round 58's change is chosen. The card's stage 1
     is on `card-r57`, branched from it (`scratch/wt-card-r57`), and takes its later fixes by merge. No card
     run of round 57's world, of any length, happens before the reach guard and the bounded walk there have
     passed their proof on the card itself, since a body flung far out could otherwise hang the card. Their
     proof on ILGPU's CPU accelerator passed (`a5f433e8`); the card's half, planted strays alone on the
     machine under the driver watch, waits on the coordinator's go
     (`logbook/specs/card-audit-r57.md` on `nursery-speed`).
   - **Still owed from the CPU's speed plan**, each alone on the machine: P11's 1-against-8 read and P9's
     links-per-body count.
3. **The entries for rounds 55 and 56**, each with the theatre's pictures first (round 56's at a
   summer's peak and a winter's trough of seed 2). Round 56's has to explain why the crowd grew under a
   light that cuts income, and why the winning leaf lines keep thinner reserves.
5. **The entries owed**, each with the theatre's pictures taken first: 0124 (round 50, after V1's
   film windows from the round's own checkpoints), 0126 (round 51) and 0128 (round 52).
6. **The CPU's physics at a full crowd.** P1 to P3 are on main (`675ef5f`). Before any change to the
   water's own arithmetic, `scripts/profile-farm.ps1` is re-run on a free machine with the bed's
   `HeightGradientAndHessian` read as a frame of its own; by `logbook/specs/farm-physics-speed-read.md`'s
   estimate it is about 40% of a far link's water sample. The rest is the owner's (above).
7. **The card's transport check** against round 50's own record (D126's conditions), by day with
   nothing else on the machine and after the card's hold. Round 50's seeds are resumed on main's exe with
   `EVOSIM_GPU_TRANSPORT` from checkpoints near 5,000, 15,000 and 25,000 s, 1,000 s each, against the same
   windows on the CPU's transport; `logbook/specs/card-transport-check/check.ps1` runs them and
   `compare.py`. Until it has run, rounds use the CPU's transport; the first round after it is queued with
   `scripts/farm-queue.ps1 -GpuTransport`. Then the card's speed work, largest first: the snow's
   settling, remineralisation and mixing (about 8 ms a step) beside the transport; the senses taking the
   transport's snow instead of a second upload, if nothing between them changes it (unchecked); the class
   uploads sending changed rows alone (3 to 4% of the wall, an estimate).
8. **Round 52's extension**, seeds 2 and 3 to 50,000 s, when a lane is free.
9. **A story film for rounds 50 onward** (create-story-video). The owner ruled on 2026-10-05 to wait for a
   breakthrough worth telling, so nothing of the flow runs until one comes. The facts for rounds 50 to 54 passed their source check (`scratch/story-video/rounds-50-54-202610010807/`); the owner
   declined the four angles and asked for a narrative around the world changes and their failure to
   produce what was hoped. The writer runs only when the owner says. `facts.md` predates round 54's final
   read and needs a questions-file update.
10. **The storage reductions that lose nothing**: a checkpoint that points at `genomes.jsonl.gz` (a new
    `Checkpoint.Version`, with 6 still read) and the feeding log gzipped, then a measured test of a better
    codec for the moving state.
11. **The theatre's four checkpoints** re-recorded on the current build: ckA, ckB, ckC and ckUi.
12. **The records owed**: the review round filing `logbook/specs/r52-lifespan-literature.md` (on
    `round-52`) in `research/LITERATURE-REVIEW.md`; the literature round for D128's sources; pass 2 of
    the brain v2 literature (`research/brain-v2/pass1-candidates.md`, two candidates that bear against
    one brain per creature, arXiv 2206.13366 first) and of the nursery-curriculum
    review round (`research/nursery-curriculum/round7-fetch-brief.md`, drafted as round 7; it files as
    round 8, since round 7 is the bet-hedging round); a primer on the nursery once it works.
13. **Round 49 seed 2's budded line**, read for what the bud costs against M4's 2.7% from what
    `logbook/specs/r49-read/` holds, since the runs are gone; if that is not enough, it is dropped.
14. **Watch the story flow's first subagent write with the owner**, since a subagent's writes are
    untested.

The proposal most likely to follow is the feeding change: a mouth fed from a neighbourhood, or intake
by the water passing the mouth. It goes to the owner as a proposal file before anything is built.

## How the experiments are run

`CLAUDE.md` and its nested files hold the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| the farm | `scripts/run-farm.ps1 <arm> -Launcher rounds/env-rNN.ps1 -Seed N -Seconds S -Threads 16`, run from the tree the round belongs to, since the farm hashes the source above its working directory; `-ResumeFrom <run> -At <s>` continues a run from a checkpoint; stopped by `stop-arm.ps1`, which writes a `STOP` file |
| launching | `scripts/farm-queue.ps1 -Round rNN -Tree <tree> -Launcher rounds/env-rNN.ps1 -Seeds 1,2,3 [-GpuTransport]`, started detached, refusing a dirty tree; the pre-registration's record is the manifest's `gitCommit` on a clean tree. Under `-GpuTransport` it relaunches a seed the card refuses on the CPU's transport and resumes one the card ends into `<arm>c` from its last checkpoint; a read of such a seed joins the two at that checkpoint |
| reading | the round's reader `scripts/reads/rNN-read.py` (every clause, per seed, with a held line per clause); the entry's own reads under `scripts/reads/rNN-entry/`, their outputs under `logbook/specs/rNN-read/`; `scripts/analyse-arm.ps1` by column name, never positionally |
| checking | `Evosim.Farm.exe --verify-checkpoint <ckpt> <s> <out> <threads>` for a checkpoint member by member; `scripts/compare-det.py` for a resume against its run; a film window's identity rows for V1 |
| pictures | `scripts/theatre-snap.ps1 <arm> -From snapshot -At <s>` for a still from the record; `scripts/theatre-film.ps1` from a checkpoint for a clip; every render checked on a sheet against a reference before the owner sees it |
| profiling | `scripts/profile-farm.ps1 -Name <n> -ResumeFrom <arm> -At <s> [-Tree <tree>] [-AllowSourceMismatch]`: a resume under .NET's EventPipe sampler, read by method with `src/Evosim.Profile`; a share, not a pace |
| tests | `scripts/core-test.ps1` (the default set; `-All` before a change to the world), and `dotnet test` on `Evosim.Farm.Tests` and `Evosim.Dynamics.Tests`; the fixtures are `src/Evosim.Core.Tests/fixtures/r42-config.json` and the crowd named in `RunFixture.cs`, re-recorded on every build that adds a tunable |
| the nursery | `Evosim.Nursery --recipe rounds/nursery/<name>.json [--check]` from a clean worktree, pinned to the efficiency cores; `--rerun` a registered entry from a worktree at its commit; read with `scripts/reads/nursery-exam-per-body.py` |
| the card | ILGPU under `src/Evosim.Farm.Gpu`, worked by day with nothing else on the machine; kernels regenerated before a build; `EVOSIM_GPU_TRANSPORT` puts the snow's transport on it (`GpuTransport`, `GpuTransportTests`); `src/Evosim.UnityProxy` compiles the Farm package as the Editor would |
| the Unity farm | idle since round 42; its workers and caps are in `unity/CLAUDE.md`, for the theatre only |
