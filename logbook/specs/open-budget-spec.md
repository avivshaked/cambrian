# The open matter budget — implementation spec

Fable, 2026-09-04, D074 (owner: "let us see what happens when matter does not lock").
Default-preserving: with both new knobs at 0 a run is step-for-step identical to today.
Build AFTER the divergence build (`logbook/specs/divergence-spec.md`) has landed in `main`;
both touch `World.cs` and `EvolutionRun.cs`.

## The rule

Matter gets an influx and an outflow, the way energy has light in and respiration out.

`RunConfig` (all `[Tunable("world", …)]`, all default 0 / `Surface`):

- `MatterInfluxPerSecond` (`Unit = "matter/s"`): units of free matter added to the world
  per second, deposited each metabolic step (× the step's seconds).
- `MatterInfluxAt`, enum `MatterInflux { Surface, Vent }`: `Surface` spreads the deposit
  equally over the top layer (`heightY = 0`) of every patch — rivers and dust; `Vent`
  deposits it all in the vent patch at the vent's depth (the same `VentPatch`/`VentDepth`
  fields D067's plume uses — find their names in `RunConfig`), riding the upwelling.
  The enum serialises by name like `ConceptionOrder` (the enum branches in the schema,
  JSON and both reflection tests exist since that build).
- `MatterBurialPerSecond` (`Unit = "1/s"`): the fraction of each patch's floor-layer free
  matter removed from the world per second (× the step's seconds, capped at the stock).
  Matter only — never detritus, never locked matter.

`World`: apply both in the same place the matter field settles and mixes each metabolic
step (find where `Matter.Settle`/`Mix` are called; influx before, burial after, so a unit
deposited at the surface is not buried the step it arrives). Totals
`MatterInfluxedTotal`, `MatterBuriedTotal`, and `MatterInitialTotal` (the stock at
construction, which `StandingMatter` equals today). The identity, asserted in tests to the
rounding: `MatterInitialTotal + MatterInfluxedTotal − MatterBuriedTotal ==
Matter.TotalJoules + MatterInBodies`. `StandingMatter` keeps its meaning (free + locked)
and is no longer constant.

`EvolutionRun.cs`: `EVOSIM_MATTER_INFLUX` (float), `EVOSIM_MATTER_INFLUX_AT`
(`surface`|`vent`, refuse anything else, as `EnvConceptionOrder` does),
`EVOSIM_MATTER_BURIAL` (float); set on the config before `Hash()`. Header token after the
`conception …` token: `matter in X/s at surface|vent, burial Y/s` (prints `matter in 0/s`
unconditionally, D065's precedent). Report columns appended after the last existing
column (after `diverged` if the divergence build landed): `mat in`, `mat buried` — the
per-window deltas, like `det in` / `det out`; JSONL fields `matterInfluxWindow`,
`matterBuriedWindow`, `matterInfluxedTotal`, `matterBuriedTotal`. Manifest ending:
`matterInfluxedTotal`, `matterBuriedTotal`.

## Tests (Core)

1. Default bit-identical: a world with both knobs at 0 equals one built without touching
   them (the pattern in `ConceptionOrderTests`).
2. Influx lands where it should: `Surface` → the top layer of every patch gains
   `influx × dt / patches`; `Vent` → only the vent patch's vent layer gains `influx × dt`.
3. Burial takes only from the floor layer, only free matter, never more than the stock,
   and nothing when the floor is empty.
4. The identity closes after 500 steps with influx 1/s, burial 0.05/s, breeding and
   deaths happening (a world with leaves at a healthy irradiance) — to 1e-3 relative.
5. Reflection: the three tunables reach the hash and survive the JSON round trip; the
   enum refuses an unknown name.

## Verify before handing back

`./scripts/core-test.ps1` green with the count; zero `error CS` on `unity-w7` (refresh,
hash-check; never touch a worker with a Unity process on it); a 300-s arm with
`EVOSIM_MATTER_INFLUX=0.6 EVOSIM_MATTER_BURIAL=0.01` whose header carries
`matter in 0.6/s at surface, burial 0.01/s`, whose `mat in` column reads ~60 per 100-s
window and whose `run.json` reads `status ended`; and a 300-s default arm byte-identical
below the header to `runs/r20v-age1.md` (same settings; read its `config.json`). Rules:
write only inside the repository; do not commit; do not touch workers other than
`unity-w7`; `stop-arm.ps1` is the only way to stop an arm. Keep the comment voice.
