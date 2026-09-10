# Conception order — implementation spec

Fable, 2026-09-04, after logbook/0055. One knob, default-preserving: a run with no new
setting must be step-for-step identical to today.

## The fault

`World.Reproduce()` (`src/Evosim.Core/Ecosystem/World.cs` ~L945) walks `_living` in list
order and each solvent parent draws its child's matter from its own layer before the next
parent is considered. `_living` is birth-ordered — `Admit`/`Reproduce` append, death is
`RemoveAt(i)` — so in a matter-starved layer the *oldest* solvent creature always takes the
matter and a younger one breeds only when everyone older is dead or broke. Measured
(logbook/0056): in the reference world's plateau the median parent age at conception is
3,352–4,536 s (48–62% of births to parents older than 3,500 s) against 376–558 s during
growth. DESIGN.md never specified an order; this is the engine doing something the design
did not ask for, which CLAUDE.md classes as a fault. It is also why round 18's last
stomachs held four times a child's price and had no children.

## The knob

`RunConfig.ConceptionOrder`, an enum `ConceptionOrder { Age, Shuffled }`, `[Tunable("world")]`,
default `Age` (today's behaviour, named for what it is). Enums serialise by name (the
existing `RunConfigJson` convention); the two reflection tests (`RunConfigTests`,
`RunConfigJsonTests`) must cover it — check that `NudgeFloat`'s enum handling, if any, can
vary a two-member enum; if the tests need an enum branch, add it the way the existing
code handles enums elsewhere.

`Shuffled`: each call of `Reproduce()` walks the parents in a fresh uniformly random
permutation of `_living`'s indices (Fisher–Yates over an `int[]` of indices, reused
between steps to avoid allocation), drawn from a dedicated `Rng` owned by the world and
constructed once at world creation from `Rng.SeedFor(Seed, <a fixed reserved index>)` —
NOT from `_nextIndex`, which numbers genomes; reserve a constant that cannot collide (e.g.
`ulong.MaxValue - 1`, documented next to `_nextIndex`) and never advance `_nextIndex` for
it. Same seed, same config → same permutations → same run (§7). `_born` is appended in
walk order (that is what determines the next step's list order for children; fine, and
say so in a comment). `Age` keeps the existing loop untouched — do not route the default
through the permutation code.

`EvolutionRun.cs`: `EVOSIM_CONCEPTION_ORDER` (`age` | `shuffled`, case-insensitive,
default unset → `Age`), set on the config before `Hash()`; header token
`conception age` / `conception shuffled` appended after the `exudation X` token (tokens
are append-only). No new report columns.

## Tests (Core, `src/Evosim.Core.Tests`)

1. Default is `Age` and a world run with it is bit-identical to one run without touching
   the property (construct two worlds from the same seed/config, one with the property
   explicitly set to `Age`, step both 200 metabolic steps, compare births, deaths,
   energy totals, matter totals, and the living ids in order).
2. `Shuffled` is deterministic: two worlds, same seed and config, both `Shuffled`, 200
   steps → identical living ids in order and identical totals.
3. The contest: a world with two solvent parents of equal genome in one layer/patch and
   matter stock enough for exactly one child per step — under `Age` the older parent
   wins every step (its `children` count grows, the other's stays 0); under `Shuffled`
   over, say, 200 steps both parents have children (each ≥ 30). Use the existing test
   helpers for building a minimal world (look at `WorldTests` / `ExudationTests` for how
   they set up two organisms and matter), and set `MinimumPopulation` / floor so nothing
   else breeds. If the world's breeding gate makes "solvent" hard to arrange, give both
   parents a large `Energy` directly.
4. Reflection: the two config tests pass with the new tunable (hash covers it; JSON
   round-trips it by name; an unknown name refuses on load).

## Verify before handing back

`./scripts/core-test.ps1` green with the count; a Unity compile on a refreshed idle
worker — use `unity-w7` only (workers 2, 3, 4 are free too but leave them; never touch a
worker with a Unity process on it: `Get-Process Unity`) — zero `error CS`; a 300-s
validation arm through `run-arm.ps1` with `EVOSIM_CONCEPTION_ORDER=shuffled` whose header
carries `conception shuffled` and whose `run.json` reads `status ended`; a second 300-s
arm with the variable unset whose header carries `conception age` and whose report is
byte-identical (below the header line) to a third arm launched the same way — the
default-preserving check. Report all three arm names.

Rules: write only inside the repository (`scratch/` for temporary files; never Windows
TEMP or a session scratchpad); do not commit; do not stop or launch any Unity process
other than the validation arms on `unity-w7`.
