# G6, the theatre look: where the lane stands

The grazer film's gate G6 (`logbook/specs/grazer-film-plan.md`) has three parts. This note says how each stands, what the theatre
lane (evolution-simulator-c8 on 2026-10-29) built for it, and what is still open.

## The gates

| Part | State |
|---|---|
| The owner approves the colour and flutter | Passed. On 2026-10-29 the owner ruled three things: the blade colour is set per world, so that the blades land on the underwater photographs' hue (102 to 108) in that world's own light; the small round producers take the darker forest green; and the flutter stays at 0.4. The owner then saw the stills in `scratch/owner/blades-per-world-forest-green-rounds/` and approved them, through the coordinator. |
| Dials-off identical, and the film-look smoke at both skins | Passed at `theatre-look-hold51` (`208f684f`) on worker 7: dials-off IDENTICAL; wide and close smokes with no shader error. |
| The family drawing matches Core's surface points | Passed on r63s-on-6202 on 2026-10-30. `EVOSIM_THEATRE_FAMILY_MESH` and `EVOSIM_THEATRE_FAMILY_SKIN` are on `theatre-look-family`. The skin's field sits 0.000 mm from Core's surface points. A tapered limb reads tapered, and a control body's own pixels stay within 2 levels between the dials off and on. Those are live portraits seeked from the run's checkpoints, labelled as cousins, since a solo still has neither the one skin nor the family mesh. The skin map is merged in at `3bfdef6a`, where dials-off is identical and both film-look smokes pass. The plan and its checks are in that branch's `logbook/specs/g6-theatre-look/family-drawing-plan.md`, with what G8 carries into the film's look. |

## What the trunk holds

- `theatre-look-hold50` (`6c316389`) adds `EVOSIM_THEATRE_ALGA_ROUND_STOP`, a target stop for the round producers alone. `ALGA_ROUND_TARGET` used to read the blades' stop, so a per-world blade stop would have moved the forest green too. Proof that it does not: with the round stop at trial d's mid-green stop, the t3 shot is byte-identical over 7 of 7 frames to the frames the owner chose from (hold49's `target-10`).
- `theatre-look-hold51` (`208f684f`) writes the rulings into the film-look paragraph of the trunk's `unity/Assets/Theatre/CLAUDE.md`, with the calibration procedure. It also records that the bloom portraits the owner liked were drawn in the wide shot's skin, `ONE_SKIN=3` with `SKIN2` off.
- `theatre-look-hold52` (`8a1ad19f`, scripts only) is the head for films. `scripts/theatre-world-stops.tsv` holds the blade stops per world, and `scripts/theatre-blade-stops.py` measures a frame's blades and gives one step of corrected stops.

## Measurements

| World | Stops | Blades: hue, saturation, lightness | Round producers' hue |
|---|---|---|---|
| r62cf, trial d (the sample) | trial d | 96, 0.28, 0.21 | 143 (with `ROUND_PHOTO` 1) |
| r62cf, the film look | r62cf's row | 105, 0.41, 0.22 | 138 |
| r63b-c, r62cf's row | r62cf's row | 98.7, 0.45, 0.22 (7 frames) | 140 |
| r63b-c, its own row | r63b-c's row | 102.0, 0.44, 0.24 (7 frames, 99 to 106) | 138 to 141 |

A stop set in one world's light does not carry to another: the bloom world's bluer, brighter water put r62cf's blades 6 degrees
yellower. The coordinator dropped a second step for r63b-c, because the film's world gets its own row once r63c's read picks it.
The round producers' forest green measured 134 in the t3 light where it was chosen. In r62cf and r63b-c it measures 138 to 141.

## The remains port

The film's world eats by contact and leaves limp remains (D219-D221). The theatre draws remains from farm film windows
(`theatre-remains`' `3d55669c` and `40bd9778`), but the writer that puts them in a window is farm code that was never on main.
Windows are recorded by the round's own frozen farm, so G7's build must carry the writer, or the film shows no remains.

- The farm half is on `remains-farm` (`8194c5e7`, off main, worktree `scratch/wt-remains-farm`). The coordinator lands it on main before G7's pre-registration.
- The theatre half, with the same farm half, is on `theatre-look-remains-port` (`65858963`, off hold51 with hold52 merged in, worktree `scratch/wt-remains-port`).
- `scratch/theatre-realism/remains-port/window.ps1` is the farm half's check, launched by `launch-window.cmd` beside it in a HOLD window on lane A. `FilmWindowDigestTests.PinnedDigest` is now taken on main's tree at `d17d2878` without the remains rows, and both branches carry the pin. The Farm tests filtered to `FilmWindow` passed 36 of 36 on remains-farm. The crowd fixture on remains-farm's Release farm (`runs/cefix-s4/2026-10-29-163618-d2298c25`) equals `runs/cefix-s4/2026-10-28-112207-d2298c25` row for row over 20,000 s: main's src has not moved since that fixture's commit `41dfcbb7`, and only the farm hash moved. The farm half is ready to land. The theatre half is compiled by phase6a.
- One change in what a window counts: a contact-eating window's bite count no longer includes remains rows.
- Open, to judge from a render with remains in the film look: the trunk's later living effects are not tied to the remains flag. Those are the pattern, the one skin's dress, the uptake marks, the thalli and the alga bladder's sheen. So in a film look that turns them on, a remains body still wears them. If remains do not read as dead, the fix is `!dead` to `!dead && !aged` at those sites, on the remains path only. A producer's remains also take the alga dress before the grey.

## Lane notes

- `scratch/theatre-realism/slot-r62kg.ps1` holds the film look as `$current`, with the rulings in it, and creates `scratch/theatre-realism/HOLD` in its `finally` block when a chain ends. Beside a card sitting the coordinator wants that file left in place.
- The render guard (`scratch/theatre-realism/render-guard.ps1`) and `batch-heat.ps1` stop at D258's limits: CPU 98 °C on one
  reading or 95 °C on every reading for a minute, GPU 83 °C, or any nvlddmkm event.
- The reference renders that the batch readings cite in older theatre trees now sit in the trunk's
  `scratch/safari/` (`coral-refs/`, `remains-refs/`, `pace-refs/`), with the old paths in `REFS-MOVED.md` beside them.
- Step 0's dials-off check moves the earlier frames' folder aside and needs captions written after its start. A Unity that exits before rendering (the package manager's helper dropping its connection, as on 2026-10-30) once let last day's frames pass as the new build's, and leaves `Temp/UnityLockfile` behind; with no Unity process running, that file is stale and is moved aside.
- A step letter in `slot-r62kg.ps1` must be new: `-cmatch` fires every step with that letter. Search the script for the
  letter before using it, since hold48's round-producer step already held `Ж`.
- Growth: `LiveWorldView.Resize` rescales the parts in place, and the one skin's bones take the growth as a scale. That matches Core's uniform growth, but no render of a growing body has checked it yet.
