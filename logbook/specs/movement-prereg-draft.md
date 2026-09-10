# Draft: the movement round (D075 item 1), to be pre-registered on the confirmed world

*Fable, 2026-09-07. Not yet a logbook entry: it becomes 0069 once 0068 confirms D077's
world and the controls' numbers are in. The movement clause of the goal rule is the
owner's to word; this draft asks the mechanism question only.*

## The question

Movement has never paid its energy cost in this world: `jointed` reads 0 at the end of
every arm since logbook/0018, and the cost side is closed (`EffectorDriver` bills the
work) while the prize side has never been measurable — a swimmer that cannot tell where
food is has nothing to swim toward. Since 0062 three senses answer — Chemical (the edible
density at each part), Energy (seconds of reserve) and Flow (the water's relative
velocity at each part) — behind `EVOSIM_SENSE_CHEMICAL|ENERGY|FLOW`, default off. Since
0065 the world has places: matter arrives at the vent's floor, the plume lifts through
one patch and returns through three, and a stomach lineage evolves where the matter is.
The round turns the senses on and asks whether jointed bodies that can sense end up in
better water than rigid ones, and whether the guild survives.

## The arms (dt 0.01 only — the fast step under-drives joints, CLAUDE.md)

The confirmed world (0068's settings) with `EVOSIM_SENSE_CHEMICAL 1`, `EVOSIM_SENSE_ENERGY
1`, `EVOSIM_SENSE_FLOW 1`, 30,000 s, five seeds: `r28-s1` … `r28-s5`. Controls: 0068's five
seeds (the same world, senses off; the `spd`/`food` columns exist there too). No other
change; one mechanism per round.

## Validity checks

V1 headers: `senses jointangle,jointrate,up,depth,chemical,energy,flow` and the three
scale constants; every other token equals 0068's. V2–V3 as 0068's.

## Predictions

A seam to state before M1 and M3 are read. Chemical sensing samples the field at the part's
own depth, feeding is priced at the creature's economic height and patch, and light is read
at the body's height; a controller can exploit only a gradient its acquisition model
resolves, so a gain in M3 is read against `mat here` and `J/m3 here`, not against what the
sensor saw. And every arm runs with added mass off (`addedMassCoefficient` 0, as in every
recorded world), unless the owner rules it on first, in which case every seed is a new
realisation and the controls are re-run.

| # | prediction | falsified by |
|---|---|---|
| M1 | **the senses are taken up**: at the last snapshot, ≥ 20% of living genomes carry at least one Chemical, Energy or Flow input, in every arm (a pool the founders and mutation can draw from is drawn from; 0062's 2,000-s validation had 8 of 930 genomes in its last snapshot, per the perception build report) | `snapshots/*.jsonl` (grep the channel names) |
| M2 | **a jointed guild persists**: `jointed inh` ≥ 10 at every sample over the last 6,000 s in ≥ 3 of 5 (controls: 0 in every arm of the record) | `jointed`, `jointed inh` |
| M3 | **movement pays where it exists**: in every arm with a jointed guild at the end, `food jnt` exceeds `food rig` by more than 20% averaged over the last 6,000 s, with `spd jnt` ≥ 0.05 m/s | `food jnt`, `food rig`, `spd jnt` |
| M4 | **the stomachs follow the matter**: the absorptive share in the plume's patch exceeds the mean of the other three over t > 10,000 in ≥ 3 of 5 (needs the per-patch absorptive instrument, queued) | per-patch columns |
| M5 | **the goal rule still holds**: D063 as amended in ≥ 4 of 5 (the senses do not cost the stomachs their clade) | `clade-score.ps1` |
| M6 | **nothing diverges** and the audit closes (the senses add reads, not forces) | `diverged`, `audit` |

## The two-sided readings

- **M1–M3 hold:** movement pays; the owner words the movement clause and the round that
  scores it. The predation proposal goes to the owner on this world.
- **M1 holds, M2 fails:** the senses are used and muscles still do not pay — the prize is
  not reachable by swimming at this cost; read `spd jnt` in the arms' early samples
  (was there a swimmer to select on?) and the ledger's cost of a stroke; the cost side
  (`EffectorDriver`'s billing, `EVOSIM_LIFT_COST`) is the next question, the owner's.
- **M1 fails:** the pool is drawn but the inputs do not persist — a sensor that reads a
  gradient the neuron cannot use; look at the squash constants (all three unmeasured).
- **M2 holds, M3 fails:** swimmers survive without eating better — a jointed body pays
  for something else (depth control, staying in the plume); read `depth` by guild
  (needs the instrument; today's columns cannot).
- **M5 fails:** the senses change the founders' draw and the world's founding; a
  different world, read against 0068 seed by seed.

## Instruments to add before launch (Sim, agent work, no world rule)

- Per-patch absorptive counts (`a0`..`a3`) and per-guild mean depth (`dep jnt`, `dep
  rig`), so M4 and M3's second reading can be read from the report.
- `mat here` in the report (today only in `stats.jsonl`).
- A snapshot-level count of sensor inputs by channel in the footer, so M1 is read without
  grepping.
