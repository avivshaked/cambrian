# The anchoring build (D268): a hold output, two grip senses and cheap grips, behind one switch

*Written on 2026-10-31 for the Sonnet builder, from D268, D162, D165, D146, D147, D139 and D262 and from the code on main.
It is item 8 of `logbook/specs/build-plan-2026-10-30.md`, built off main once `phase1-overpower` has landed.*

This build lets a brain control a grip. Each anchoring part gets a hold output in the central brain, and two senses: whether it is holding, and how hard it is pulled. A grip forms on touch and holds until the hold output drops below a threshold. Forming and letting go each cost a price scaled by the grip's breaking force. Every one of these rules sits behind one switch on the anchoring cell, and it is off by default. The cell itself is already in the code (round 59) and is off in every world since. Putting the switch on the cell, and not on `RunConfig`, means no world without the anchoring cell has its config refused. So no fixture, crowd recording or nursery config needs re-recording.

## One switch on the cell carries the build

The new numbers go on `AnchoringCell`, beside its five existing ones. They go into the constructor, `WriteParameters`, `HashContribution` and `CellTypeJson`'s reader, in the same order everywhere. The cell's defaults leave the grip as it is today.

| Cell property | EnvBinding name | Default | Meaning |
|---|---|---|---|
| `BrainHold` (bool) | `EVOSIM_ANCHOR_BRAIN` | off | Anchoring parts get the hold output and the two senses |
| `ReleaseBelow` (float) | `EVOSIM_ANCHOR_RELEASE_BELOW` | −0.5 | A held grip lets go when the hold output is below this. It must be finite and between −1 and 1 |
| `ReleaseJoulesPerNewton` (float) | `EVOSIM_ANCHOR_RELEASE_PRICE` | 5 × 10⁻⁵ | The price of letting go, in J per N of the part's full breaking force. Zero is allowed, and a negative value is refused |

Add `ReleasePrice(float breakingForce)` beside `BuildPrice`. The existing `EVOSIM_ANCHOR_PRICE` (`BuildJoulesPerNewton`, 5 × 10⁻⁵) stays the forming price. It keeps its rule that it must be positive. Both prices scale by the part's full-strength breaking force, `BreakingForce(volume)`, not the decayed one, because they stand for the attachment's size.

Refuse `EVOSIM_ANCHOR_BRAIN` in `EnvBinding` when `EVOSIM_ANCHORING` is off. Refuse it in `World`'s config check, beside the per-part contact check at about line 3001, unless `BrainModel` is central. The old per-node brain has no output list to add to, and no world has run it since D146.

## Each change, by file

| File | Change |
|---|---|
| `Core/Genome/NeuronInput.cs` | Append `SensorChannel.Gripping = 17` and `SensorChannel.GripPull = 18` |
| `Core/Brain/CentralBrain.cs` | Append `BrainOutputKind.Grip = 2`. The prose calls it the hold output. The code calls it `Grip` because `SensorChannel.Hold` already names the joint envelope's sense |
| `Core/Development/PhenotypePart.cs`, `Developer.cs`, `Phenotype.cs` | Add `Grips` (internal set, default false). The developer sets it on anchoring parts only when the table's anchoring cell has `BrainHold`, following the nose's `Smells` pattern at `Developer.cs` line 121. Copy it at the three places `Phenotype.cs` copies `Smells` |
| `Core/Brain/CentralWiring.cs` | `IndicesOn` returns 1 for the two new channels on a part that `Grips` and 0 elsewhere. `Outputs` appends one `Grip` output a gripping part after every joint output, in part order, with id `BrainIds.Output(part.PathId, 0, Grip)` and drive index −1. `Effective`'s template gets a `Grip` branch: a twin's hold output, then up the chain, then any gripping part in part order. With none, the output is drawn fresh as D147 wires a founder (every pool port, LeCun scale, bias 0) |
| `Core/Brain/Brain.cs` | `ForCentral` counts `TotalDof` from the power outputs, not `outputs.Count / 2`, and exposes `GripCount`. `Step` takes an optional `float[] hold`, which `StepCentral` fills from units `2·TotalDof` to `2·TotalDof + GripCount − 1` |
| `Core/RunConfig.cs` | `SensorPool()` appends `Gripping` then `GripPull` after the leaf smell when the table's anchoring cell has `BrainHold`. The leaf smell's line writes `pool[at]` without moving `at` (line 4672), so make it `pool[at++]`. `CellTypes` becomes a property with a backing field whose setter clears `_sensorPool` |
| `Farm/EnvBinding.cs` | Add the three names beside `EVOSIM_ANCHOR_*` (line 611) and the fields on the settings object, and pass them to the cell (line 860) |
| `Dynamics/Creature.Grip.cs`, `Creature.cs` | Add `HoldSignal` (length `GripCount`), `GripOutputOf` per link (−1 for none), `GripPull` per link and `GripsLetGo`. All of them are null or zero unless `BrainHold` is on. `AdoptGripsFrom` carries the last two through a rebuild |
| `Dynamics/DynamicsWorld.cs` | `StepOne` passes `body.HoldSignal` to `Brain.Step` (line 341) |
| `Dynamics/Anchor.cs` | The step below |
| `Dynamics/CreatureSenses.cs` | The two channels, read from the last step's `GripHeld` and `GripPull`, one step stale as `BedTouch` is, and masked like every other sense |
| `Dynamics/Creature.State.cs` | A tagged `GRPL` block after `GRIP`, written only when `GripPull` is allocated, holding `GripPull` and `GripsLetGo` |
| `Farm/Report.cs` | Five grip columns (`gripsHeld`, `gripsFormed`, `gripsLetGo`, `gripsBroken`, `gripJoules`), written only when the table holds the anchoring cell, so every recorded report keeps its columns |
| `Farm.Gpu/*` | The card's port, below |

## The grip's step with the switch on

`Anchor.Apply` keeps today's order and adds the brain's test. `let` is `BrainHold && hold < ReleaseBelow`. An unwired hold output reads `tanh(0) = 0`, which is above −0.5, so a body whose brain ignores the output holds as today's grip does. Since forming needs the output at or above the threshold, a grip that was let go re-forms only once the output rises back above it. That needs no stored latch.

```text
for each anchoring link i:
    if not held:
        if not touching, or reserve factor is 0, or let: pull share 0; continue
        Form (owes BuildPrice, as today)
    if let:                       LetGo: owes ReleasePrice, GripsLetGo++, pull share 0; continue
    if reserve factor is 0:       Release, no price (today's rule); continue
    compute pull and breaking     (today's code)
    pull share = min(1, |pull| / breaking)
    if |pull| > breaking:         Release, no price, pull share 0; continue
    apply force and torque, decay strength   (today's code)
```

`LetGo` is a new helper. It clears the grip as `Release` does, but it counts in `GripsLetGo` and not in `GripsBroken`. Holding owes nothing. The pull share is written only when `GripPull` exists, so a switch-off step does no new arithmetic.

## The port order keeps every recorded genome and world

A stored brain names its outputs and ports by id, not by place (`BrainIds`), so the genome format does not change and `GenomeJson.FormatVersion` stays where it is. Under the switch, an old genome gains its hold outputs at birth through `Effective`. A genome bred with the switch on, loaded where it is off, loses them, because `Effective` keeps only the outputs the body has.

The joint outputs keep units 0 to `2·TotalDof − 1`. The drive reads those units (`2k` and `2k + 1`), and so does the card's `BrainStep`. The hold outputs come after all of them and before the hidden units. The two senses come at the end of the pool. So with the switch off, `Outputs`, `Ports` and the pool are the lists they are today, and every brain stream draws what it drew. With it on, a body with no anchoring part has the ports it had and draws the same founder weights.

## What the build refuses, and what it leaves alone

| Thing | What happens |
|---|---|
| A config with no anchoring cell (every world since round 59, `r42-config.json`, `r57-s2-config.json`, the crowd fixture in `RunFixture.cs`, the 34 nursery configs) | Loads unchanged with the same hash. Nothing is re-recorded |
| A config whose table holds the anchoring cell (round 59's runs under `runs/`, gitignored) | Refused: the cell's three new fields are missing. Its hash changes too. No committed file carries the cell (I searched for `buildJoulesPerNewton`). `ledger.ps1 -Config` on a round 59 run will throw |
| Genomes, `inocula/grazer-grip/` included | Load unchanged |
| Checkpoints | No `StateVersion` bump. The new block exists only when the switch is on, and no checkpoint before this build can have it on. Round 59's checkpoints fail at their config anyway |

I did not check that an older build refuses a newer anchoring config. I infer it from the stored hash, which would no longer match what the old build computes. The builder confirms it in the binding tests.

## Near zero is 5 × 10⁻⁸ J/N

A 6 cm bud breaks free at about 210 N. At today's 5 × 10⁻⁵ J/N, one grip costs 0.010 J, about a tenth of the bud's own tissue at 500 J/m³. With a brain in control, a worst case is a hold output flickering across the threshold every step. At 0.01 s steps, one grip and one let-go every 0.02 s comes to about 1 W, which is far beyond any bud's income.

At 5 × 10⁻⁸ J/N, a thousandth of today's price, one grip costs 1.0 × 10⁻⁵ J. That is about 0.02 s of the bud's own standing upkeep (2.5 W/m³, about 5.4 × 10⁻⁴ W). Letting go once a minute then costs nothing a body could notice, which is the overpowered start D262 asks for. The flicker still costs about twice the cell's upkeep, so selection can still see a brain that cannot decide. Zero is not an option for the forming price, because the cell refuses it (D162: a free grip is a free lever). The screen therefore sets `EVOSIM_ANCHOR_PRICE` and `EVOSIM_ANCHOR_RELEASE_PRICE` both to 5e-8.

## Tests

Each of these is filtered and fast. Core runs through its script.

```powershell
./scripts/core-test.ps1 -Filter "FullyQualifiedName~AnchorBrain|FullyQualifiedName~AnchoringCell|FullyQualifiedName~CentralBrain|FullyQualifiedName~RunConfig"
```

Dynamics runs `dotnet test src/Evosim.Dynamics.Tests --filter "FullyQualifiedName~Grip"` with the Unity SDK's `dotnet`, which `core-test.ps1` finds. Farm runs through `./scripts/card/core-offcard-test.ps1 -Project Farm -Name ab1 -Filter "FullyQualifiedName~GripCheckpoint|FullyQualifiedName~Round59Binding|FullyQualifiedName~AnchorBrain|FullyQualifiedName~TheGripOnTheCpuAccelerator"`.

| Rule | Where | The test |
|---|---|---|
| Off is bit-identical | Core, Dynamics | With the cell absent, and with the cell present and `BrainHold` off: `Outputs`, `Ports`, `SensorPool` and an `Effective` brain equal today's for a fixed genome. Today's `GripTests` pass untouched. A gripping body stepped 2,000 steps with the switch on and a hold bias of +3 matches the switch-off body in every position and in `GripOwedJoules` |
| Below releases, above re-forms only on touch | Dynamics | Drive `HoldSignal` by hand: at −0.6 a held grip lets go the same step. At −0.4 on the rock it re-forms. At +1 off the rock nothing forms. At −0.6 on the rock nothing forms |
| Prices at forming and letting go, never while holding | Dynamics | `GripOwedJoules` rises by `BuildPrice` at the form and by `ReleasePrice` at the let-go, and not at all over 500 held steps. A break under pull and a let-go at an empty reserve owe nothing |
| The senses read right | Dynamics | `Gripping` reads 1 the step after forming and 0 the step after letting go. `GripPull` equals the pull over the breaking force, clamped at 1, and reads 0 when not held. A part without the cell reads 0 on both |
| Ports come and go with the part | Core | Bud an anchoring part onto a body: one `Grip` output and two ports appear. Remove it: they and their links vanish. A mirror twin's hold output copies its twin's links, moved to its own ports |
| A founder wires the new output | Core | `Effective` with `founder: true` links the hold output from every pool port, at a spread of 1 over the square root of the port count, with bias 0 |
| Checkpoint round trip | Farm | `GripCheckpointTests` with the switch on: a resume at the midpoint equals the straight run, `GRPL` included. A switch-off checkpoint carries no `GRPL` |
| The cell's numbers | Core | A JSON round trip of the cell, every new field in `HashContribution`, and refusals of a NaN threshold and a negative release price |

The builder runs no suite beyond these filters. The coordinator runs the default suites and `./scripts/core-test.ps1 -All` in a HOLD window before the commit, as CLAUDE.md asks of a commit that touches the world.

## The card is ported, at about two and a half agent hours

I recommend porting. The card already carries the grip and the central brain (`Grip` and `BrainStep` in `gpu-kernel.template`), and the screen's world is a card world. The r64 worlds ran tens of thousands of bodies on the card. My guess, not measured, is that the host would run such a world well under the 0.5x real time at which CLAUDE.md stops a run. A clean refusal in `GpuWorld.Refuse` would take about 15 minutes, but it would leave the screen with nowhere to run.

The port's pieces are in the table. Each new column is one cell wide when the switch is off, as the stroke assembly's tables are.

| Where | Change |
|---|---|
| `SCfg` | `AnchorBrain` and `ReleaseBelow` |
| `SGlob`, by link row | `GripLetPrice` (double), `GripPull` (read back for a checkpoint), `GripSignal` (float scratch), `GripUnit` (int: `2·TotalDof` plus the part's ordinal, −1 for none) |
| `SGlob`, by global slot | `GripsLetGo` (read back) |
| `BrainStep` | Writes each gripping link's `GripSignal` after the parity flip |
| `Grip` | Takes the step above |
| `SensorRead` | Takes `gl` and the row base, and answers `SGripping = 17` and `SGripPull = 18` |
| `gpu-runner.template` | Packs `GripUnit` and the let-go price beside `_gripPrice` (line 3003) |
| `Kernels.D.g.cs`, `Kernels.F.g.cs` | Regenerated by `KernelGenerator` |
| `GpuEngineTests` | `TheGripOnTheCpuAcceleratorIsTheSolver` gets a switch-on case |

The builder checks whether the units' ceiling, `cfg.MaxN`, is computed from the bodies or fixed. Hold units add up to 16 a body.

## After the build: identity, then a short smoke

The coordinator runs these after the build, by `scripts/card/` and the card-sitting order in HANDOFF. The build itself runs nothing long.
1. On the host, switch off: the crowd fixture's digest test passes unchanged.
2. On the card, switch off: the phase-1 winner's world, founded for 1,200 s on main and on this build. `gpu-accept.py` exits 0.
3. Switch on: the CPU-accelerator founded check (`card-group.ps1 -Device cpu -Seconds 30 -MigrateEvery 10 -CheckpointEvery 15`), then its resume at 15 s, as the card's CLAUDE.md asks of a founded run.
4. Switch on, on the card, at groups 32 and 64 for 1,200 s each: `gpu-accept.py` exits 0 between the two.
5. The smoke is run 4's report. It should show grips formed, let go and re-formed, `gripJoules` close to the counts times the prices, and pace within 5% of run 2.

## The screen's two arms

These are for a later pre-registration. Each arm is the phase-1 winner's world, founded on seed 6202, for 24,000 s on the card.
- **Off** is the world as it ran.
- **On** adds `EVOSIM_ANCHORING=1`, `EVOSIM_ANCHOR_BRAIN=1`, `EVOSIM_ANCHOR_PRICE=5e-8`, `EVOSIM_ANCHOR_RELEASE_PRICE=5e-8` and `EVOSIM_ANCHOR_RELEASE_BELOW=-0.5`. The bud weight stays 1, and the other anchor numbers keep their defaults.

The world already has per-part contact, the central brain and the touch sense, which is everything the switch needs.

## Open questions for the coordinator

1. The threshold could be a world number or a gene, as the joint's power threshold is (D139). I recommend a world number, −0.5, for the first screen, and a gene only if the screen shows that brains use the output.
2. A break, or a let-go at an empty reserve, could also pay the release price. I recommend it does not. D268 prices letting go, which is the brain's act, and a starved body cannot pay.
3. D162's decay of 3,600 s stands under D268. I recommend keeping it, because a brain can now refresh a grip for 2 × 10⁻⁵ J.
4. The off arm could be the recorded run instead of a new one. I recommend that, if step 2 above passes on the same world, since it saves a card slot.
5. The positions file could carry an anchored flag when the cell is in the table, so the screen can map fixed patches. I recommend adding it in this build, with `positions-read.py` taught to accept it.

## Size

About 6 agent hours of Sonnet build: 3.5 on the host with its tests, and 2.5 for the card. Add about 1 hour of the coordinator's review. It needs two HOLD windows. The first holds the host build and its filters. The second holds the card port's Release build, the kernel regeneration, the accelerator tests, the default suites and `-All`. The identity runs and the smoke need one card sitting after that.

## The coordinator's review, 2026-10-31

The spec stands, with one change and the five open questions settled.

- **A fresh hold output starts with a bias of +2, not 0.** D147 wires a founder every sense at LeCun scale with bias 0,
  so its summed input is about one input's size. Under that rule, a fresh hold output would fall below −0.5 whenever its
  senses push it down, about a third of the time. A plant, and every recorded genome that gains the output through
  `Effective`, would then let go at random. D268 asks that a brain which never uses the output hold. At +2, the output
  falls below −0.5 only when the summed senses fall under about −2.5, which is under 1 % of the time at founder scale
  (inference, for a unit-spread normal). The links keep D147's draw. This is a dial within D268, and the tests check
  the bias.
- **On the open questions:**
  1. The threshold is a world number, −0.5.
  2. A break, or a let-go on an empty reserve, pays no release price.
  3. D162's 3,600 s decay stays.
  4. The recorded run serves as the screen's off arm if the switch-off identity check passes.
  5. The anchored flag goes into `positions.jsonl` in this build.
- **The card is ported**, as the spec recommends, since the screen's world runs on the card.
- **The build starts** once `phase1-overpower` is on main, in the first HOLD window after `r64s2` ends.
