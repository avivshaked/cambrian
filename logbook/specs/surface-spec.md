# Build spec: the surface (round 24's hole), and the instrument that sees it

Fable-written, 2026-09-05, for an implementing agent **after the owner rules** on which
option below is the world rule, and **after the shared-space spike's matrix has finished
on the machine** (no Unity process before then). Read CLAUDE.md first; the gotchas "the
world has no top" and "PhysX replays bit for bit, every per-step change is a butterfly"
are the ones this build lives inside.

## The fault

Round 24 (logbook/0061): at dt 0.01 every population of the vent world sits at −0.6 to
+1.5 m mean height, where the closed world sits at −12 to −15 m. Reading the code: the
vent's plume lifts bodies at `VentSpeed` in its patch (`CurrentField.VentVertical`,
special-cased to exactly zero at depth ≤ 0 and ≥ the vent depth); a body arriving at
y = 0 with upward momentum crosses on its own; above the surface nothing acts on it —
`FluidEnvironment` zeroes a buoyant body's net vertical force at `position.y >= 0` (D050:
`if (netDensity < 0f && body.transform.position.y >= 0f) netDensity = 0f;`), and the
return flow's downward push is zero at depth ≤ 0. The surface is a ratchet: up in the
vent's patch, over the top, never back. This is an inference from the code; the
instrument below turns it into a measurement before the fix is judged.

## Part 1 — the instrument (agent work, build first, default-identical)

Two columns in the run report and `stats.jsonl`, computed once per sample from root
positions already read for `CheckFinite`:

| column | field | meaning |
|---|---|---|
| `above` | `aboveSurface` | living creatures whose root is at y > 0 |
| `above m` | `aboveSurfaceMeanHeight` | their mean root height, m (em-dash / count 0 when none) |

Cost: one comparison per creature per sample. Not per step. Bit-identical by
construction (it reads; it does not act). Also add `above` to the manifest's end-of-run
facts. With this in, the four r24 recordings can be replayed through the theatre's Mode B
or a headless rerun to read how many bodies were above the waterline and when they got
there — a rerun of one seed for 10,000 s at 0.01 on the new build (identity to the
original rows on every existing column is the check) gives the measurement without
waiting for a new round.

## Part 2 — the fix (the owner's rule; two options, one recommended)

**Option A, recommended — the region above the waterline restores.** In the buoyancy
clamp, a body at y ≥ 0 keeps a downward net force when its net density is negative:
instead of zeroing it, apply the sink-side magnitude the same body would feel if its
excess density were +|netDensity| capped at the founder sink (i.e. above the surface a
buoyant body falls back at no more than the rate a heavy one sinks). Below y = 0 nothing
changes. A body exactly at the surface with zero velocity has zero net force (continuity
with today's clamp), so a floating body still floats; only a body *above* is pulled
under. Tunable `SurfaceRestoringFraction` (`EVOSIM_SURFACE_RESTORE`, 0–1 of the sink
rate), **default 0 = today's clamp, bit-identical**; the screen sets it to 1. Why this
option: it closes the hole for every upward push at once (the plume today, an effector
channel or a new current tomorrow), which is the question D050 asked and the vent did
not answer; and it is one line in the one place lift enters the model.

**Option B — the plume stops short of the surface.** `VentVertical` ramps to zero over
the top `VentCapMetres` of the column (linear from full speed at that depth to zero at
0) so a body carried up arrives at the surface with no vertical momentum from the
plume. Tunable `VentCapMetres` (`EVOSIM_VENT_CAP`), default 0 = today, bit-identical. It
fixes the vent's push and nothing else; a body's own buoyant rise still crosses the
line and stays. Not recommended alone; it may be wanted *with* A if the plume's
delivery to the film matters ecologically (matter arriving at the surface layer is the
leaves' food).

Both options: the tunable reaches `RunConfig.Hash()` and JSON (the reflection tests),
an `EVOSIM_*` env var in `EvolutionRun.cs`, a header token, a unit test on the clamp's
sign and continuity at y = 0, and the identity check at the default (a 2,000-s replay
of any 0.01 recording, rows byte-identical on every existing column, modulo the
appended `above` columns).

## Part 3 — the screen, pre-registered separately

Not this build. The round after the ruling: the corrected world with the dose the owner
chooses (burial 0.02 or influx 0.3), at 0.02 first on seeds 2 and 4 (surface question
read within one step: `above` = 0 after t = 3,000; mean height below −5 m), then the
0.01 confirmation on five seeds under D063 as amended. The record's expectation: with
the hole closed the vent's populations sit under the film in the plume's patch and in
the deep in the return patches, and the stomachs' share stops depending on the seed.

## Constraints

Worker 6 only; one Unity process at a time; nothing outside the repository; commit on
`main`, do not push; the two identity checks are not optional; report what ran with
numbers and what did not.
