# Conception order `Reserve` — implementation spec

Fable, 2026-09-04, after logbook/0056 and the owner's ruling on
`fable-propose-matter-economy.md` (D073). Default-preserving: a run with no new setting
must be step-for-step identical to today.

## The rule

A third value of the existing enum `ConceptionOrder { Age, Shuffled, Reserve }`
(`src/Evosim.Core/RunConfig.cs`). Under `Reserve`, `World.Reproduce()` walks the living
in **descending order of energy surplus above the breeding gate**, so when a layer's
matter covers one child, the parent with the largest reserve takes it: energy buys
fecundity. Precisely:

- surplus(parent) = `parent.Energy − parent.ReproductionThreshold(Config.PerOffspringOverheadJoules)`;
  parents whose gate is ≤ 0 or whose surplus is < 0 are not solvent and are skipped, as
  `Brood` already skips them (compute the surplus once per parent per step; do not call
  the threshold twice).
- Sort the solvent parents' indices by surplus descending, **ties by list index ascending**
  (a stable, deterministic order — use a comparison that falls back to the index, and
  `Array.Sort` with a comparer or a manual sort; do not rely on `List.Sort` stability).
  Reuse the index buffer between steps as `PermuteConceptionOrder` does.
- Walk in that order calling `Brood`. The surplus is evaluated *before* the walk; a parent
  whose energy drops during the walk (it just bred) keeps its place — this is one step's
  ranking, not a running auction. Say so in a comment.
- No Rng draw: `Reserve` must be deterministic without consuming `_conceptionRng`.
  `Age` and `Shuffled` stay exactly as they are.

`EvolutionRun.cs`: `EnvConceptionOrder` accepts `reserve`; the header token prints
`conception reserve`. Nothing else in the report changes.

## Tests (`ConceptionOrderTests.cs`, extend)

1. Reflection: the enum's third value survives the config hash and the JSON round trip
   (the existing enum branches should just work — confirm the tests still walk a
   three-member enum).
2. The contest test, third case: the same two contestants as today's test, one with a
   larger reserve (give it more income — a larger photosynthetic area or more light — so
   its surplus is reliably higher every step), matter for one child per step: under
   `Reserve` the richer parent wins every step (200 / 0), and the winner is the richer one
   regardless of which is older (run it both ways: elder richer, younger richer). Under
   `Age` the elder wins both ways — the existing assertion, kept.
3. Determinism: two `Reserve` worlds, same seed and config, 200 steps → identical living
   ids in order and identical totals.
4. Default unchanged: the existing bit-identity test for `Age` still passes untouched.

## Verify before handing back

`./scripts/core-test.ps1` green with the count; compile on `unity-w7` only (refresh it
with `new-worker.ps1 -Workers 7`, hash-check, never touch a worker with a Unity process
on it — `Get-Process Unity`); a 300-s validation arm with
`EVOSIM_CONCEPTION_ORDER=reserve` whose header carries `conception reserve` and whose
`run.json` reads `status ended`; a 300-s arm with the variable unset that is
byte-identical below the header to `runs/r20v-age1.md` if that report still exists (same
seed and settings as the agent that built the knob used — read `runs/r20v-age1/*/config.json`
and `run.json` to reproduce its settings; if it cannot be reproduced exactly, run two
unset arms and compare those). Report the arm names.

Rules: write only inside the repository (`scratch/` for temporary files; never Windows
TEMP or a session scratchpad); do not commit; do not stop or launch any Unity process
other than the validation arms on `unity-w7`. Keep the repository's comment voice.
