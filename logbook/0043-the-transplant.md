# 0043 — The transplant

*2026-09-01. Pre-registered before launch; results appended after. Arms `d060-s2r0`,
`d060-s2r1`, `d060-s4r0`, `d060-s4r1`. Decision under test:
[D060](../DECISIONS.md) (the invasion assay), probing [D055](../DECISIONS.md) (the seabed
refuge).*

**This is a diagnostic, not a goal attempt.** D060 ratified inoculation as a labeled
instrument: a hand of god places a proven consumer into the world, so nothing these arms
do can count toward the standing 3-of-5 goal, which requires chains that arise on their
own. What they can do is answer the question round 7 could not.

## The question round 7 left open

Round 7 ([0042](0042-the-larder-under-the-mud.md)) ended in a clean fork. Consumer chains
establish by grazing the floor pantry — the densest food anywhere — and the refuge, built
to damp their boom-bust cycle, turned out instead to be an access gate: it stopped chains
from *starting* and never got to show whether it stops them from *dying*. Every treated
establishment was strangled at birth; every untreated one boomed and busted. No arm ever
held an established chain under the refuge, so the mechanism it was designed to test went
untested.

The assay skips establishment. Take one verified absorptive genome from round 6, inject
the same small inoculum into paired worlds — one with the refuge, one without — at the
same simulated moment, and watch what each world does to a chain that already exists.

## Why seeds 2 and 4

Both worlds survive to budget in this configuration (round 7: 618 and 2,204 alive at
t=30,000) and neither ever grew a natural absorptive lineage — s2's `inherit` column is 0
at all 300 samples, s4's peaks at a single blip of 1. So after injection, **every
inherited absorptive is a descendant of the inoculum**; the readout is unconfounded by
natural arrivals. That the treatment never bound in these seeds in round 7 (both ran
byte-identical to their round-6 twins) is exactly what makes them clean assay chambers.

## The instrument, and the dose stated honestly

`World.Inoculate` develops N copies of a stored genome and admits them on the
floor-founder pattern: endowment `FounderEnergyJoules` (200 J), energy credited to
`EnergyIn` so the audit still closes, no matter debt, generation 0, no parent — which is
what makes their offspring land in the `inherit` column. Three hashed tunables
(`InoculateAtSeconds`, `InoculateCount`, `InoculateDepthMetres`) plus the genome file,
whose SHA-256 prints in the header — an arm whose inoculum did not arrive is visible in
its own report.

The inoculum: **5 creatures at t=8,000, at −50 m**, from `inocula/d056-s5-absorptive.json`
(SHA-256 `e6f8e4da1edb…`, copied byte-for-byte from round 6 s5's final snapshot at
t=22,721 — a creature that was alive in the campaign's only surviving late chain). The
genome carries three nodes but develops to a **one-part body**: a single absorptive
sphere, ~0.023 m³, brood 2 — and that is not an unlucky pick. All 50 absorptive genomes
in that snapshot of 4,927 develop to solitary absorptive blobs; development prunes
everything else for volume. The consumer this ecology actually evolved is a single-celled
filter feeder, and that is what we transplant. At t=8,000 both worlds are
alive and past the floor era (s2: 216 alive, 101.6 kJ detritus standing, 14.3 J/m³ in the
deep layers; s4: 552 alive, 55.3 kJ, 6.1 J/m³), and the injection adds ~1–2 kJ of new
energy — one to four percent of the standing larder. The assay measures **access**, not
enrichment.

## The world

Round 6's exactly, passed explicitly rather than by default: irradiance 200, area 400 m²,
mixing 0.2, current 0.05, excessDensity 0.02, senescence 10,000, floor closes 3,000,
remin 0, ceiling 8,000, excretion 0.001. Per pair: `EVOSIM_FLOOR_REFUGE` 0 (control) or 1
(treatment). Budget 20,000 s, wall 600 min. All four arms concurrent on freshly refreshed
workers — their combined population (≤ ~2,500 each, by the round-7 trajectories of these
seeds) is below what three round-7 arms carried at once.

The config hash will differ from both round-7 waves: the workers pick up the species
columns and the inoculation tunables together. This is the documented pattern
([0042](0042-the-larder-under-the-mud.md), addendum 3) — the world is the same when the
knob-off tests say so and the header tokens differ only where the treatment says they
should.

## Validity checks, before any prediction is read

| # | check | read from |
|---|---|---|
| V1 | before t=8,000 each arm replays its round-7 twin token-for-token (same seed, treatment not yet applied — refuge 0 arms also match, since round 7's refuge never bound in these seeds) | table rows vs `d057-s2.md` / `d057-s4.md` |
| V2 | within each pair, r0 and r1 stay byte-identical until the first refuge binding after injection; the divergence timestamp is the moment the treatment first mattered | row diff |
| V3 | `floor` = 0 at every sample after t=3,100 — the floor stays closed; the only hand of god is the registered one | `floor` |

A V1 or V3 failure is a build alarm before it is a finding.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| X1 | the inoculum lives: `absorpt` ≥ 1 at the first sample after t=8,000 in all four arms | `absorpt` |
| X2 | **it establishes**: `inherit` ≥ 1 by t=10,000 in at least 3 of 4 arms — a proven genome placed next to concentrated food breeds within 2,000 s | `inherit` |
| X3 | **the control busts**: in at least one r0 arm the lineage booms (`absorpt` peak ≥ 100) and then falls below 10 while the world lives — the natural cycle, reproduced on demand | `absorpt` |
| X4 | **the treatment persists**: in at least one r1 arm, `inherit` ≥ 1 for ≥ 20 consecutive samples *and* `absorpt` ≥ 10 at the last sample — the goal metric, hit by a hand-placed chain | `inherit`, `absorpt` |
| X5 | **the refuge meters**: within each pair, the r1 peak `absorpt` is below the r0 peak | `absorpt` |

## The two-sided reading, written before the answer

- **X2 and X4 hold, X3 holds:** the refuge is a persistence mechanism whose only failure
  in round 7 was the on-ramp. Round 8 becomes *establishment access + refuge* — a partial
  pantry (fractional edibility rather than zero) is the leading design.
- **X2 holds, X4 fails, X3 holds (busts everywhere):** the refuge does not rescue an
  established chain either; the cycle is deeper than floor access, and the round-8 fork
  tilts toward [D059](../DECISIONS.md)'s physical seabed or [D054](../DECISIONS.md)'s
  shelf — changing what the world *is*, not what may be eaten.
- **X2 holds in r0 only (the treatment kills the transplant):** one metre of refuge
  starves even an established consumer at these column densities — D055 rejected as a
  world rule at this dose, and the partial-pantry redesign becomes the *only* branch.
- **X1 or X2 fails everywhere:** even a proven genome cannot make a living at t=8,000
  densities. That answers the owner's standing question — *world problem, not mutation
  problem* — at a stroke, and the follow-up is a later injection time (a fuller larder),
  not a different genome.
- **X3 fails (no boom in the controls):** these seeds' worlds do not support the boom at
  all; the round-6 busts were seed-specific, and the assay needs s1 or s3's world instead.
  A quiet, honest miss — rerun, do not overread.

## Scoring

Per [D058](../DECISIONS.md): only budget-complete arms answer; an extinct arm is a
failure of its world, not of the assay; a wall- or ceiling-cut arm is censored and its
predictions are read only up to the cut. Results appended below when the arms land.

---

## Mid-round addendum — the instrument verified (t≈8,100, all arms)

All four headers carry the treatment (`inoculate 5 @ 8000 s, 50 m, genome e6f8e4da1edb`),
pairs share config hashes as they must, and the checks read:

- **V1 holds.** All 79 pre-injection rows in all four arms replay their twins
  token-for-token on the shared 36 columns (the d060 reports append seven newer columns;
  the comparison is on the common prefix). One false alarm first: the checking script cut
  the rows one field wide, keeping the new `species` column against the twin's trailing
  emptiness, and flagged every row. The instrument was right and the ruler was bent —
  worth recording because the pre-registration's own rule ("a V1 failure is a build alarm
  before it is a finding") is what forced the second look.
- **X1 holds.** `absorpt` = 5 in all four arms at t=8,000 and t=8,100 — the transplants
  are alive. `inherit` still 0 at 8,100: no descendants yet.
- **V2 in progress.** Within each seed the pair is still identical at t=8,100, and the
  first divergence from the *twins* lands exactly at the injection row (s2: 262 alive vs
  259 — five inoculants minus what their competition displaced). The hand moved the world
  at precisely the pre-registered instant and nowhere earlier.
