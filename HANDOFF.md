# Handoff: where to pick up

*Rewritten on 2026-10-25. The logbook says what happened. [`DECISIONS.md`](DECISIONS.md) says why it was chosen. `CLAUDE.md` and the nested files its table names say how things are done. This file says only where things stand and what is queued. It is rewritten, never appended to. The version before this rewrite, with round 58 to round 61 day by day, is `logbook/specs/handoff-archive-2026-10-25.md`; the earlier archives sit beside it.*

## Where things stand

### Grazers still cannot make a living from living food

Grazing is the owner's focus. The aim is a grazer guild that keeps itself going on living leaves in a world of separate oases (D191). Round 62's world is the open ocean: a 300 m square with its edges joined (D204), coral on the oases (D194), plants that keep their matter (D206, D207), and a current of 0.02 m/s (D209). The round ran six worlds at a mouth reach of 3 m, with and without corpse spoilage (D214). It stopped at 24,000 s under D215 and D216, because its grazers lived by scavenging and the share they got from their own kills fell. The read is `logbook/specs/r62-read/d215-table.md` on branch `r62-launch`. The fast-rot screen after it (D217, D218) failed too; its read is `logbook/specs/r62-rot-screen/` on the same branch.

Eating is now by contact (D219-D221). A biting cell feeds only on the body it touches, as it wounds it, capped at the biting part's intake rate. No corpse is made. Whatever dies uneaten stays as limp remains, which can be eaten by contact until a time window closes. The switch is `EVOSIM_CONTACT_EATING`, off by default. A screen passes on D222's bar, which is D215's three tests with test 3 changed to say living food. Three screens have run on it. Look at the joules a kill keeps: under round 62's corpse rule a kill kept about 420 J, and no contact rule has come within five times that.

| Screen | What it tried | Kept joules a kill | Intake over upkeep | Read |
|---|---|---|---|---|
| `r62ce` | contact eating, a bite takes tissue only | 24-27 | 0.19-0.20 | `logbook/specs/r62ce-read/README.md` |
| `r62cs` | a living bite also takes its share of the store (D224); remains window 60, 500 and 1,500 s | 56-89 | 0.43-0.52 | `logbook/specs/r62cs-read/README.md` |
| `r62bd` | the bite damage at 2, 4 and 16 times, intake scale 16 (D226, D227) | 61-77 | 0.57-0.68 | `logbook/specs/r62bd-read/README.md` |
| `r62-h` (round 62, corpse rule) | the control | 418-434 | 0.90-1.04 | |

Every screen fails D222. Tests 1 and 2 fail in every world and test 3 holds. Nothing has overshot, so nothing has been rolled back. The store share stays on, the remains window stays at 500 s and the bite damage stays at 1 (D225, D230). A grazer's meal is bounded by the one part it eats (D230).

**The kill gut (D232) is next.** When a body with a mouth kills, it swallows the whole dead body, tissue and store, into its gut, and no remains are left. The gut is digested at the mouth's per-step cap. Bites take their share of that cap first, and the intake waste goes to the water. The gut is built on branch `kill-gut` (`scratch/wt-killgut`, off main at `b7e69a95`), uncommitted, with `KillGutTests`. The build notes and the coordinator's readings are in `logbook/specs/d232-kill-gut-build.md` there. Its screen, `r62kg`, is drafted there too: `rounds/env-r62kg.ps1`, `check-r62kg.ps1` and `launch-r62kg.ps1`, with `logbook/specs/r62kg-prereg/predictions.md`, whose hashes and build are filled in before it is committed. The world is `r62cs-w500` with the gut on and the intake scale at 16. A 4,500 s check of seed 6201 (`r62kgck-6201`) runs first, then `r62kg-6201` and `-6202` to 24,000 s in one card group. The controls are `r62cs-w500`, `r62bd-x2` and `r62-h`, which are not run again. If the screen overshoots, the roll-back is a slower digestion rate.

The owner has delegated the grazer screens to the coordinator. The tactic is to push the dials until grazers are overpowered, then roll back between the last two settings, and to record each step as a DECISIONS entry. A new world rule stays the owner's (D224's status).

### Main carries round 62's line and four physics rules behind switches

Main holds the whole round 62 line: the open square, the coral, plants keeping their matter, the blades' rules (D196, D199, D200), spoilage, contact eating and its dials, and the mouth point. They arrived as two squash commits, `3036a257` (mouth-point) and `b7e69a95` (fin-shape). Every new rule is off by default.

- **The fin law (D229)** is `ShapedAddedMass`, on main. A stroking limb moves water by its width across the swing. On the bench the paddle travels five to seven times as far as a stick of equal volume, and it is cheaper per metre at equal distance (`logbook/specs/d228-d229-build/README.md`).
- **The mirrored joint (D228)** is `ReflectMirroredAnchors`, on main. Each part of a mirrored pair is jointed where development places it (`logbook/specs/mirrored-anchor-finding.md`).
- **Contact on the true shape (D228)** is the `ContactShape` switch. Level (a), `TrueShape`, is on main on the host and the card. It uses GJK on convex cores with margins, and the world's surfaces test the true shape too. Level (b), `TrueShapeAtPoint`, puts the force at the contact point and touches the bed at every vertex of the core. It is on main on the host only, and the card refuses it. Its card port is being built on `atpoint-card` (`scratch/wt-atpoint`), uncommitted.
- **The card identity round** passed on 2026-10-25. With every switch off, the build is D227's build bit for bit and at its pace. With each switch on (the fin law, the mirror, level (a), and all three together), groups 32 and 64 are identical. The suites passed: Core with the slow tests 1,418, Dynamics 300 with one skip, Farm 396, and the CPU-accelerator tests 37. The crowd fixture is `runs/cefix-s4/2026-10-25-014251-059fea5e`, named in `RunFixture.cs`.
- **The whole-assembly push (D231)** is `StrokePushesAssembly` (`EVOSIM_STROKE_ASSEMBLY`). A stroke pushes the jointed link and every part welded beyond it, with its leverage where the moved water sits. The host build is on `stroke-assembly` (`scratch/wt-d231`, `bb3a6814`), work in progress and untested. Its specification is section 4b of the build plan, which so far is only on `fin-shape` (`dbbbf4c2`). Main's copy of `logbook/specs/d228-d229-build-plan.md` lacks that section.
- **The twin drive (D234)** works under the mirrored-joint switch. The same brain signal moves a pair as mirror images. It is uncommitted on `twin-drive` (`scratch/wt-twin`), and its brief is `scratch/contact-eating/brief-16.md`. The owner rules on it only after seeing a film.

### The nose, fat and the shape family follow

- **The nose (D235)** is a cell type. When a world lists it, only nose parts smell, with noise that falls as the nose grows, and the nose pays upkeep by its area. The owner asked that its compute cost be watched and cut. evolution-simulator-b9 builds the host code on `nose` (`scratch/wt-nose`). The nose is built in full after D232's screen, and the nursery tests it against today's whole-body smell in the patch tank before any world carries it.
- **Fat (D229)** puts a cap on a body's reserve, and fat tissue raises the cap. Its build specification, with eight decisions for the owner, is `proposals/propose-fat.md`. It is built behind a switch and screened only after grazers pass D222.
- **The shape family (D233)** is a tapered superellipsoid, added as a fourth shape. evolution-simulator-b4 builds its maths in Core on `shape-family` (`scratch/wt-shape-family`). It joins contact as a hull of 98 points inside D228's GJK test. A bench runs before any screen, and its card change shares an identity sitting with another solver change.
- **Poison** comes after grazers pass D222 (D228). **Animal smell** comes once grazers live on living leaves (D235).

### The theatre is building one skin over each creature

evolution-simulator-b0 works on branch `theatre-coral` (`scratch/wt-coral-theatre`, `67958791`). The contact-eating worlds are filmed from `theatre-remains` (`scratch/wt-remains`, `6ebb2a59`, which is mouth-point with theatre-coral merged in). Both branches are local only. `theatre-snow-lens` (`9bf7d937`) is pushed and merged into theatre-coral. Main's theatre has none of the coral drawing, the realism dials or the remains. Every look is a dial, off by default, and pixel-identical when off. The check every change goes through is in `scratch/theatre-realism/plan.md`, and the theatre's `CLAUDE.md` on theatre-coral describes each dial. The fixed look (the fix with the back light at 0.4) is the default for films.

The owner's direction of 2026-10-25 is biological forms over geometric parts, judged against real marine photographs (`scratch/owner/realism-refs/` with its `sources.csv`; what each of our tissues lacks is in `scratch/theatre-realism/tissue-gaps.md`). The plan is `logbook/specs/theatre-skin-plan.md`, in this order:
1. **The cell view** (`EVOSIM_THEATRE_CELL_VIEW`) is built and delivered in `scratch/owner/cell-view/`. Its palette waits on batch b44's sheet (below).
2. **The one-skin spike** (`EVOSIM_THEATRE_ONE_SKIN`) draws one smooth surface over the body cells: structure, absorber, float, nerve, link and consumer. Leaf and holdfast stay specialised, and the mouth disc is drawn on the skin. b43 filmed the first pass on four many-part subjects, and b44's second pass follows it. The best take goes to the owner beside its parts version and its cell view, after the coordinator's check.
3. **Colour** (a diver's light, `EVOSIM_THEATRE_DIVER_LIGHT`) and **motion** (an edge ripple, `EVOSIM_THEATRE_RIPPLE`) are built while the spike is reviewed.
4. **The one skin in full** comes after the owner's word on the spike.
5. **Genome to skin** (`EVOSIM_THEATRE_SKIN_MAP`) and **self-similar detail** (`EVOSIM_THEATRE_FRACTAL`) are design only. The research behind them is `logbook/specs/theatre-research-equations.md`.

theatre-coral's worktree holds every dial since `67958791` uncommitted: `CELL_VIEW`, `ONE_SKIN` (`TheatreOneSkin.cs`, untracked) with its second pass, `PATTERN` (`TheatrePattern.cs`, untracked), `MOTTLE_SOFT`, the velvet and chromatophore retunes, and the `CLAUDE.md` paragraphs. theatre-remains' `MOUTH` (`TheatreMouth.cs` and `.shader`, with their `.meta` files) is uncommitted too. Each is committed after its batch's dials-off check passes, and b44's is the next. Batches run from `scratch/theatre-realism/bNN.ps1`, generated by `make_bNN.py`. Each refreshes `unity-w6` through `scratch/coral-theatre/merge-check.ps1`, proves dials-off identity against the previous batch, and obeys `HOLD` and `STOP` in `scratch/theatre-realism/`. Each batch's readings sit beside it as `bNN/readings.md`.

The realism dials so far (`ORGANIC` 1 and 2, `BLEND`, `COUNTERSHADE`, `COLOUR_RAMP`, `LEAF`, `VELVET`, `LINK`, `RIM_BAND`, `CHROMATOPHORES`, `MOTTLE_SOFT`, `PATTERN`, the coral dials and the drawn stroke) are on theatre-coral, and `MOUTH` is on theatre-remains. `CHROMATOPHORES` stays in the code at 0 and gets no more takes: a cloud strong enough to read would read as a lighting fault, so its effort moves to the skin's per-vertex tissue colour and to the relief and gloss of the skin plan's §6(f). `VELVET`'s regular lattice reads as wallpaper, and after b44 it is rebuilt as §6(a)'s irregular colony: systems of varied size round dark common openings, a hue from the genome with strong contrast, and no lattice. The recommended set and its sheets are in `scratch/owner/realism-dials/`, with work in progress in `scratch/owner/organic-shapes/`. The farm reads Core's internal `Organism.RemainsMap` through a reflection delegate, which is a recorded debt. A film window of a card run is faithful only at its first instant and a cousin after it. Every added tunable makes older builds refuse a newer config, so filming a world from a newer build (the twin film, `r62kg`) needs main merged into the theatre branch first.

### Films and records

Another session builds the create-story-video skill. Its files are uncommitted in main's tree under `.claude/skills/create-story-video/` and `scripts/tests/story-video-check/`, and are left alone. The countdown film waits for the owner's approval of `prose-13.md` in `scratch/story-video/rounds-53-57-202610061945/`. The skill's publication step waits on the owner's verdict on round 57's YouTube copy. A story film for rounds 50 onward waits for a breakthrough worth telling (the owner, 2026-10-05).

## The agents and their lanes

The owner launches every agent by hand. Ping the owner's phone when the next agent is wanted, when input is needed, or when there is something new to look at.

| Session | Lane |
|---|---|
| evolution-main-agent | The coordinator. It keeps `scratch/machine/queue.md`, gives the go for every card sitting and render batch, and decides the grazer screens at the owner's delegation |
| evolution-simulator-5d | Build and speed, and the card: `kill-gut`, `atpoint-card`, `stroke-assembly`, `twin-drive`, the nose's card work. A Sonnet subagent builds each item and 5d reviews it. Off-card tests run one job at a time on the cores the coordinator gives |
| evolution-simulator-b0 | The theatre. One Unity at a time on logical 0-1 |
| evolution-simulator-b4 | D233's shape maths on `shape-family`, light tests at 2 threads on 2-15 |
| evolution-simulator-b9 | D235's nose host code on `nose`, light tests at 2 threads on 2-15. No full Dynamics suite and no Farm build while renders run, unless the coordinator gives a slot |
| assay-d6 | Another project, Assay, sharing the machine. Its agents relay through it, so every Assay line goes to assay-d6. It is suspended now, and since its lane A (0-3) overlaps b0 and the test cores, it asks the coordinator before resuming |
| csck506-deep-learning-ac | Coursework |

## The machine

- **Heat and headroom are the bar, not load** (the owner, 2026-10-09). A card group alone peaks at about 79-88 °C. With a Unity process or a build beside it, it has reached 90-95 °C. So while a card group runs, b0 holds all Unity and nothing runs beside it but one small CPU job. Heavy jobs stay off logical 0-15 while the machine runs warm (confirmed by the owner, 2026-10-23).
- **Renders run alone.** Other agents' tests stay small beside them. A batch runs on worker 6, one Unity at a time on 0-1 at BelowNormal, with at least 5 minutes between Unity steps and a heat gate before each one. `scratch/theatre-realism/batch-heat.ps1` stops a batch at 89 °C, and the file `scratch/theatre-realism/HOLD` stops b0's chain (`night-chain.ps1`).
- **The heat watch** runs during every card run: `heat-watch.py`, armed at most 29 minutes ahead and re-armed when it expires (`logbook/specs/machine-and-tools.md`, "Heat"). `-HeatStop` stops a run on two samples in a row at or over 90 °C (D183). After a heat stop, wait an hour for the room to cool, then resume from the checkpoint (the owner, 2026-10-17).
- **Every look at a running world reads its pace** with `python -I scripts/pace-check.py`. A slowed world is stopped and probed without asking (CLAUDE.md). A card resume on the contact-eating builds does not repeat its run, so a stopped world of a screen is rerun from its start or read as a cousin (D226).
- **A card group** runs at group 32, 14 threads on logical 16-29 (mask `0x3FFF0000`), with `-HeatStop` and a memory limit of 12,000 MiB. Card runs take the thread pool's spin at 5,000, and runs off the card do not (the owner, 2026-10-09). The card launchers refuse any `dotnet.exe` and any farm process beside a pending launch (`src/Evosim.Farm.Gpu/CLAUDE.md`). Jobs meant to stay off the card run with `CUDA_VISIBLE_DEVICES=-1`.
- **The card's driver is NVIDIA 617.42.** Any driver event stops the card's work and goes to the owner.
- **The fans.** Fan Xpert's page fails with H-2004, but the board keeps the fan curves (`logbook/specs/machine-and-tools.md`).
- **Windows Update.** The owner paused updates to about 2026-10-19, and whether they are paused now is not recorded. Ask the owner before a run that must not be interrupted.
- **Times** in watch scripts are compared in UTC, since the clocks went back on 2026-10-25 (`scripts/CLAUDE.md`).
- **A timing read** is taken with nothing else running, beside a repeat of its baseline in the same sitting.

### A card sitting goes in this order

1. Send assay-d6 the card notice: lane A moves to 30-31, two threads, BelowNormal, no Unity. Wait for its clear line.
2. Hold the theatre by creating `scratch/theatre-realism/HOLD`, and confirm no Unity is running.
3. Arm the heat watch.
4. Then tell the launching agent to start.
5. When the run ends, delete `HOLD` and send assay-d6 "round ended".

## The decisions in front of the owner

Each is set out in full, with the options and what each implies, in every message that asks for it.

- **The mirrored twins' motion (D234).** Under the mirrored-joint switch, should a mirrored pair's joint drive be reflected with its geometry, so that one brain signal beats the pair as mirror images rather than in a seesaw? The coordinator recommends yes, as in every bilateral animal, so that paired fins can row together. The owner accepted the recommendation but rules only after seeing a film of one mirrored body with the switch off and on. The film needs `twin-drive` built, passed through a card identity sitting, and merged into a theatre branch. The other options are to leave the seesaw (switch off) or to give the motion a switch of its own. Each option changes only worlds that turn the switch on.
- **The cell view's palette.** The palette is Okabe-Ito's eight colours, with anchoring black, drawn unlit and outside the water's fog. On b43's sheet the consumer's orange shifted halfway toward the absorber's, because the frame's +0.4 EV exposure was applied to the cell view too; b44 compensates for it inside the cell view. b44 also films a candidate, `CELL_VIEW=2`: consumer in the set's reddish purple `CC79A7`, and neural near white `F2F2F2`, chosen over a lilac because a lilac neural sits close to buoyancy for a deuteranope. It shows level 1 against level 2 on n6202 scene 2's crowd, where absorbers and consumers stand together. The options are the corrected level 1, level 2, or another reshuffle. It changes only how the theatre draws, never the world.
- **Fat's numbers** (`proposals/propose-fat.md`, eight decisions with recommendations): the reserve cap, the joules a cubic metre of fat adds, fat's upkeep, its build price and its weight. They are world rules and go to the owner in full.
- **An unpaid grip.** A grip forms on touching rock and charges its build price at once, so a body that cannot pay dies paying. Should a grip not form when the body cannot afford it? It is not urgent while the price is a placeholder: about 0.01 J for a 6 cm bud and up to about 2 J for a part 0.8 m across. The grip comes back only as a screen arm that needs no build (D228).
- **The room**, from the owner's rulings of 2026-10-11. The first question is whether to merge `room-cap` (`6d36a4a2`, the full room suite passing). The second is how to re-step old nursery runs: on each run's own commit from `8489b8df` on, by a graft for the five older entries, or by a clean refusal (`logbook/specs/nursery-room-unloadable-recipes.md` on `nursery-room`). The genome screen's design (`design/nursery/BRIEF-genome.md`) goes to a design pass in the owner's session, and which design session takes it is the owner's choice.
- **The pushed branch `squash/2026-10-18`** is still on GitHub. Deleting it is the owner's call.

## Queued, in order

All of it is agent work unless it says otherwise. A long step runs in the background under a session, never in a subagent and never in a shell loop.

1. **The kill gut.** 5d runs `kill-gut`'s Core and Farm tests while b0 holds, then commits. The first integration branch joins `kill-gut` and `atpoint-card` (level (b)'s card port, with its accelerator tests). The crowd fixture is re-recorded once for the new tunables, and one card identity sitting checks every switch off against main bit for bit and each switch on at groups 32 and 64.
2. **The kill-meal check and screen.** `r62kgck-6201` is read for the gut's size, the meal a kill keeps and what the cap holds back. Then the pre-registration is committed with its hashes, `r62kg` runs on the card, and it is read on D222. The coordinator decides the next step at the delegation.
3. **The second integration branch** carries D231 and the twin drive. D231's host build is finished and tested, then benched in still water (a small link with a blade welded at its tip, a flat link and a stick, at powers 0.8, 0.4, 0.295 and 0.2). Then both get their card ports and share one identity sitting. The twin film for the owner follows (D234).
4. **The nose (D235)**: b9's host code, then the card port and the cost cuts (one sample per sniff, and new fields filled at the metabolic cadence), each measured against the pace without the nose. Then the nursery test in the patch tank.
5. **Fat**: the owner rules `proposals/propose-fat.md` (not urgent), step 0's checkpoint reader measures leaves' reserves, then the build behind its switch. It is screened after grazers pass D222.
6. **The shape family (D233)**: b4's Core maths, then contact by its hull, the bench (family fins against box fins, and a pace read on a crowded checkpoint) and the card change in a shared identity sitting.
7. **The swimmer curriculum (D236)** in the nursery. The tank build starts now and is gated by nothing: the configs re-saved on today's format after merging main into the nursery branch, a counter of food kept from leaves under contact eating, the switching patches, the travel measures and the blade founders. The calibration bench comes before any arm, and the arms wait for D232's read. Then the moving-oasis screen with the trained swimmers put in beside a cold start (D229). The moving-oasis rules are in `proposals/propose-doing-must-pay.md` (fountains with a lifetime, a deeper winter), and the owner still rules their details.
8. **The theatre**: b44, then the owner's sheet of the one-skin spike, then colour and motion, then the one skin in full on the owner's word. `VELVET` is rebuilt as the irregular colony after b44. After that comes genome to skin (its Python check first, on h6202's window), then self-similar detail, steps (a) and (d) first.
9. **Small items for 5d**: `r62-fix`'s two bit-neutral cell-table fixes (`427664f0`), which are not on main, and lever 2 for speed, the central brain on the card's threads (`r62-brain`, `f64580f7`), which is not on main either.
10. **Loose ends.** `proposals/propose-r62-killer-claim.md` (uncommitted, 5d's) was set aside by D219 and D232, so it is filed or removed. `propose-new-cell-types.md` and `propose-new-part-shapes.md` look fully ruled by D228, D229 and D233, so they are absorbed and deleted once checked. The stale Running lines in `scratch/machine/queue.md` are cleared. The remains delegate becomes a public accessor when the theatre branches come to main.
11. **Records owed.** A logbook entry for round 62 and its screens (`r62f`, `r62ce`, `r62cs`, `r62bd`), with theatre pictures taken first. Entries 0124, 0126 and 0128 (rounds 50 to 52) and the result entries of rounds 55 and 56. Round 60's read again in light of the kelp blades. The literature rounds: `logbook/specs/r52-lifespan-literature.md` (on `round-52`), D128's sources, pass 2 of the brain v2 review, and the nursery-curriculum review as round 8. A primer on the nursery once it works.
12. **Later, when there is room.** The theatre's four checkpoints (ckA, ckB, ckC, ckUi) re-recorded. The storage reductions that lose nothing. Round 49 seed 2's budded line, read from `logbook/specs/r49-read/` or dropped. The camera above the surface and the waterline shot, which the owner wants and which have not started.

**Held for later, each as its own question.** These are a death risk that rises with a line's crowding, harsher winters, staying power for the crossing, strands between islands, a replay of the tape, and holdfasts at the oases' edges. Also held: calm water over the shoals with a faster current between them (D209), an implicit or split drag (D199), the refractory share of matter (D212), and the seep (put to the owner again in full before it is built). From the physics read: N1 (velocity from the acceleration's gradient), N2 (a cheaper floor) and R2 (acceleration sampled less often). Removing the card's software `Atan2` waits for a measurement and the owner. The nursery on the card is not funded (the owner, 2026-10-05). There is no picture-only glow; glow waits for real bioluminescence as a world rule. From the owner's list of 2026-09-30: the friction value μ, friction between creatures, the producer threshold under D063, a tempo dial, and the older proposals in `proposals/`. Measuring reach from the centre and reading a body in one cell stay the owner's (2026-10-19; `scratch/owner/world-rules-without-dials/`).

## The nursery and the room

- **The nursery's code** is on the nursery branches (`nursery-d178` for bodies with brains and the patch tank, `nursery-patchwatch`, `nursery-d172`, `nursery-leaf`, `nursery-general`). The registry is the main checkout's `rounds/nursery/registry.jsonl`. The nursery is on round 60's config format, and its world configs are `a8cbe76c9532bc71`, `2b125f6cbda84a01` and `e80c201e54b3963f`. Main's world has moved past them, so a nursery test of the nose or the curriculum starts by merging main into its branch and re-saving the configs. Nursery branches are merged into, never rebased.
- **Where it stands.** In the patch tank the smell carries nearly all the eating: 83 J an episode against 9 J with it knocked out. It acts in a patch's last metres, onto a leaf, and does not keep bodies in patches (logbook/0146, `logbook/specs/nursery-patches-read.txt`). The leaf swimmers' ten best tops are `inocula/r59-direction-tops/`.
- **Held until the coordinator says**: the patch tank's repeat `leaf-direction-patches-s2-w5`, its founders' baseline, and any new arm.
- **Nursery runs** are pinned to the efficiency cores (16-31). A job of 30 minutes or less may use all cores, by day, under the heat watch, running alone (the owner, 2026-10-05).
- **The room** is on main (`logbook/specs/nursery-room-build.md`). Its server is down and comes back only on the owner's word. It runs on `127.0.0.1:5179` from `scripts/room-server-build.ps1` and comes down for any card run. The main checkout's `rounds/nursery/room.json` carries uncommitted edits for it.

## Git, branches and the disk

- **GitHub's main** is a squash history. A push is the owner's to approve each time, code and prose only. Local main's commits since `b7e69a95` (pushed 2026-10-25) are unpushed. The full local history before 2026-10-18 is the local branch `archive/local-2026-10-18`, never pushed.
- **Local-only branches.** The `r62-*` branches, `mouth-point`, `fin-shape`, `theatre-coral` and `theatre-remains` carry three commits of 2026-10-18 (`c32c5b71`, `dd587eb0`, `b37e2a2b`) whose author address the pre-push hook refuses. They are never merged or pushed. Their content comes to main as new squash commits with a noreply author (the owner, 2026-10-23). Pushed branches besides main are `theatre-snow-lens`, `hdrp-spike`, `bed` and `squash/2026-10-18`.
- **Main's working tree** carries other sessions' uncommitted edits: the story-video skill and its tests, the theatre's files under `unity/Assets/Theatre/` and `scripts/theatre-*.ps1`, `CLAUDE.md`, `scripts/CLAUDE.md`, research files and the room's files under `rounds/nursery/`. Commit only your own lines, by blob.
- **Worktrees** live under `scratch/wt-*`. Never `cd` into one. A round's build stays as long as its runs do, because `film.py` refuses a run whose `programPath` is gone: `scratch/wt-leafincome` holds round 50's build, and `wt-r51` to `wt-r59`, `wt-r60`, `wt-r61`, `wt-r62launch` and the others hold the rest. `wt-w0` holds the speed agent's reference build and `wt-look` holds `look-2`.
- **Nursery branches without worktrees**: `nursery-r57`, `nursery-d152` and `nursery-w5` hold commits main does not. Merge or rebuild from them when the nursery needs them. `nursery-w5` is on the hollows build that D156 set aside.
- **Three span launchers** stay in their worktrees until a line in them that names another project is scrubbed: `rounds/r60-ov-span.ps1`, `r60-det-span.ps1` and `r60-cap-span.ps1`.
- **Runs that code and tests read** stay: r42-s1, r42-s4, r45fixb-s4, r37-s1, r25-s2, r25q-s2, th-ref, uicheck, r35tsmoke3, boxdig-old, r20v-age1, pfix14, pfix15, and r48-s1's config with its 20,000 s snapshot. The run directories the record names are in main's `runs/`, with old paths in `logbook/specs/run-moves-2026-10-18.txt`.

## How the experiments are run

`CLAUDE.md` and its nested files hold the commands and the gotchas. This table says where each tool sits.

| | |
|---|---|
| a card screen | `rounds/launch-<name>.ps1` over `scripts/card/card-group.ps1`, from the tree the screen belongs to, by the card-sitting order above. The pre-registration is committed first, on a clean tree |
| identity | `rounds/identity-*.ps1` at groups 32 and 64, compared with `scripts/reads/gpu-accept.py`. A switch-off build is checked against its parent build bit for bit, and each switch on at 32 against 64 |
| a fixture | the crowd fixture `cefix-s4` (`logbook/specs/d228-d229-build/fixtures.ps1`), re-recorded on every build that adds a tunable and compared with `python -I scripts/reads/compare-run-rows.py <old> <new> --from 0` |
| the farm | `scripts/run-farm.ps1 <arm> -Launcher rounds/env-<name>.ps1 -Seed N -Seconds S`, from the round's tree. `-ResumeFrom <run> -At <s>` continues a run, and `-ResumeConfig <config.json> -TrickleOff` makes a cousin with changed dials. Stop a run with `stop-arm.ps1` |
| reading a screen | `scripts/reads/r62-read.ps1 -Worlds` on D222's tests, `scripts/reads/travel.py`, `scripts/reads/bite-cap.py`, and a screen's own reader under `scripts/reads/` (`r62bd-read.ps1`, `r62cs-read.ps1`). Outputs go under `logbook/specs/<screen>-read/` |
| checking | `Evosim.Farm.exe --verify-checkpoint <ckpt> <s> <out> <threads>` for a checkpoint member by member, and `scripts/compare-det.py` for a resume against its run |
| benches | `Evosim.SwimProbe --fin-bench` for a stroke in still water, and `--mirror-census` for mirrored pairs |
| pictures | `scripts/theatre-snap.ps1` and `scripts/theatre-film.ps1` on main, or a batch script under `scratch/theatre-realism/` for the realism work. Every render is checked on a sheet against a reference before the owner sees it |
| profiling | `scripts/profile-farm.ps1 -Name <n> -ResumeFrom <arm> -At <s>`: a share, not a pace |
| tests | `scripts/core-test.ps1` (`-All` before a change to the world), and `dotnet test` on `Evosim.Dynamics.Tests` and `Evosim.Farm.Tests` |
| the nursery | `Evosim.Nursery --recipe rounds/nursery/<name>.json [--check]` from a clean worktree, pinned to the efficiency cores, and read with `scripts/reads/nursery-exam-per-body.py` |
| heat and pace | `scripts/temp-check.ps1` for one look, `scratch/heat-watchdog/heat-watch.py` as the watch, and `python -I scripts/pace-check.py` |
| the Unity farm | idle since round 42, kept for the theatre (`unity/CLAUDE.md`) |
