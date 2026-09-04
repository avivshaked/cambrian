# 0053 — The leak

*2026-09-04. Pre-registered before launch. The screen for D070: producers that release a
fraction of what they fix into the water while alive.*

## Where round 14 left it

Round 14 (0050) closed at 0 of 8. Clearance 10 grew the campaign's first absorptive lines
past twenty and both ate their stock and crashed; the richest seed grew no line at all.
The detritus-flux instrument, built the same night, measured why: the nutrient field's
only income is dead tissue, that income is a founding pulse decaying to a few tenths of a
watt as the world selects for small bodies (87 J per corpse at founding, 4.7 J by
t=15,000), and a few tenths of a watt hold about six clearance-10 stomachs at
replacement. The goal asks for ten. The review's round 5 put the ocean's number beside
ours: phytoplankton release 10–20% of primary production as dissolved organic matter,
and the measured producer→herbivore transfer step is 13%. This world ran at about 1%.

D070 rules exudation in principle and pre-registers this screen; both gate readings were
met on 2026-09-04. The knob is `EVOSIM_EXUDATION` (`RunConfig.ExudationFraction`): a
fraction of a producer's post-wear light intake, deposited into the field at its own
height and patch each step, taken off its net. It rides on intake, not on death, so it
does not shrink with the body the way a corpse does.

## The arms

Three, all at the 0.02 screening step (0052), clearance 10, 20,000 s so a line is read
past two lifetimes:

| arm | seed | exudation | worker | role |
|---|---|---|---|---|
| `r17x-s1` | 1 | 0.15 | w4 | the world that grew a line of 22 at 0.01 |
| `r17x-s2` | 2 | 0.15 | w5 | the world that grew a line of 48 at 0.01 |
| `r17x0-s2` | 2 | 0 | w2 | control: seed 2 at 0.02 with the leak off (seed 1's control is `r14c10-s1-flux`, 15,000 s) |

Everything else is round 14's world (`scratch/launch-r14.ps1`'s block): sink 0.002 both
fields, vent off, rolls, four patches, floor closes 3,000, senescence 3,000, cell-type
mutation 0.005. The launcher is `scratch/launch-r17.ps1`. Per 0052, a 0.02 arm is a
different chaotic realisation of its seed from the 0.01 arm, so the comparison is against
the 0.02 control and against the distribution round 14 established, not row-for-row
against `r14c10-sN`.

A note on config hashes: `ExudationFraction` is a new tunable and enters `Hash()`, so
every run from this build carries a different hash from the same settings before it.
Compare headers token by token, not by hash, across this boundary.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `exudation 0.15` (absent on the control), `dt=0.02`, `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals round 14's | header line 3 |
| V2 | the two seed-2 arms differ at the t=500 row (the knob reached the world) | row diff |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | audit 0.0000% every sample; `det in + det exuded − det out` summed equals `detritus J` to the rounding | `audit`, the flux columns |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the leak is the income**: at t > 10,000, `det exuded` per window exceeds `det in` by ≥ 3× in both treated arms (15% of a ~17 W producer economy against a few tenths of a watt of corpses) | the flux columns |
| M2 | **the field rises**: `J/m3 here` and `det deep` at t > 10,000 are higher in `r17x-s2` than in `r17x0-s2`, and `detritus J` grows through the run rather than plateauing | the field columns |
| M3 | **the D070 prediction**: an inherited absorptive line reaches ≥ 10 alive and is still ≥ 10 at 6,000 s after its first inherited sample (two lifetimes), in at least one of the two treated arms | `inherit`, lineage |
| M4 | **producers pay and persist**: `alive` at t=20,000 in the treated arms is within 30% of the control's and ≥ 1,000; no arm reaches the 8,000 ceiling | `alive`, `**Ended:**` |
| M5 | **the audit closes** with the new transfer running | `audit` |

## The two-sided readings

- **M3 holds:** the flux was the constraint and the leak lifts it. Next is a confirmation
  round at 0.01 under D063 as written — five seeds, scored — and DESIGN.md takes the rule
  with the review's fraction and caveats.
- **M1 and M2 hold, M3 fails:** the income rose and the stomachs still cannot hold ten.
  Then something between the field and the child is binding that the ledger does not
  see — the dissection of `r14c10-s4` already found stomachs that should breed and did
  not, with no output recording where they were. The per-creature absorptive ledger log
  (`scratch/absorptive-log-spec.md`) is the next instrument, before any further world
  change; the review's caveat (iii) applies: in the ocean too, exudation alone does not
  close the consumers' demand.
- **M1 fails:** the exuded joules do not show up as income at the producers' depth —
  they sink or mix away faster than the stomachs eat them, or the producer economy is
  smaller than the 17 W estimated. Read `det exuded` against the light columns before
  touching the fraction.
- **M4 fails (producers collapse):** the 15% tax is too much for this world's producers,
  which would be a surprise given the review's numbers; the bracket 0.05 / 0.13 is next.
- **M2 holds by a bloom (ceiling reached):** the leak fed a runaway — censor, read which
  guild ran, and the lever is the fraction, downward.

## Launch

Three arms on w2, w4, w5, all refreshed to commit `0d15e6f` and hash-checked, with the
monitor's watch list carrying them. Headers verified before any arm is believed. Results
appended below.

## Results

*2026-09-04, all three arms to budget (39.5 / 54.4 / 53.5 min wall at 6–8× real time; 122 /
201 drag impulses limited on the treated arms). V1 held on every header; V2 — the two
seed-2 arms differ at t=500; V3 — `floor` 0 from 3,100 in all three; V4 — audit 0.0000% at
every row, and `det in + det exuded − det out` summed over the run equals the final
`detritus J` to the tenth of a joule in all three (5,524.0 / 10,491.9 / 15,486.8).*

| arm | exudation | alive at 20,000 | inherited line | ≥ 10 from | at two lifetimes after | never below | at 20,000 | absorptive births in last 20 samples | D063's clauses |
|---|---|---|---|---|---|---|---|---|---|
| `r17x-s1` | 0.15 | 1,571 | every sample from t=2,400 (177 of 177) | 4,100 | **77** (t=10,100) | 10 | **46** | 18 | ✓ ✓ ✓ ✓ |
| `r17x-s2` | 0.15 | 1,801 | every sample from t=500 (196 of 196) | 1,600 | **83** (t=7,600) | 12 | **133** | 19 | ✓ ✓ ✓ ✓ |
| `r17x0-s2` | 0 | 1,789 | none, ever | — | — | — | 0 | 0 | ✓ ✗ ✗ ✗ |

The flux, t > 10,000, watts per arm:

| arm | corpses (`det in`) | leak (`det exuded`) | eaten (`det out`) | field at the population's depth | population's depth |
|---|---|---|---|---|---|
| `r17x-s1` | 0.46 | **13.9** | 14.7 | 0.19 J/m³ | −0.6 m |
| `r17x-s2` | 0.27 | **20.1** | 20.3 | 0.94 | −1.4 |
| `r17x0-s2` | 0.39 | 0 | 0.01 | 0.79 | +1.8 |

| # | prediction | verdict |
|---|---|---|
| M1 | the leak is the income, ≥ 3× the corpses at t > 10,000 | **held**, by 30–75× — and the producer economy behind it is ~90–135 W of light intake, not the ~17 W the proposal estimated, so 15% is a far larger leak than planned |
| M2 | the field rises against the control | **falsified as worded, and instructively**: the standing field at the population's depth is *lower* in the treated arms (0.19 and 0.94 J/m³ against 0.79) and the stock plateaus at 5–10 kJ rather than growing, because the stomachs eat the leak as fast as it lands (`det out` ≈ `det exuded` in both). High flux, low standing stock — the chemostat, not the larder. The prediction confused the two; the reading that matters is the flux row above |
| M3 | **D070's prediction**: an inherited line ≥ 10 alive two lifetimes after it first reached ten, in at least one treated arm | **held in both** — 77 and 83 at that mark, minima 10 and 12 since, 46 and 133 at the end |
| M4 | producers pay and persist: alive within 30% of the control, ≥ 1,000, no ceiling | **held** — 1,571 and 1,801 against 1,789; no arm near 8,000 |
| M5 | the audit closes with the new transfer running | **held** — 0.0000% at every row of every arm |

**What happened.** With the leak on, the stomachs the founding lottery drew never died
out. In seed 2 an inherited line existed from the fifth sample and passed ten at t=1,600;
in seed 1 from t=2,400 and ten at t=4,100 — before the floor closed in both, and the
lines then ran unbroken to the end without it. Seed 1's line fell to exactly ten at
t=8,000 while its field read 1.8 J/m³, then climbed to 77 by t=10,000 as the field was
eaten back to a tenth of a joule: a consumer–resource oscillation with a floor, the
oscillating equilibrium the owner asked about on 2026-09-03, seen for the first time.
Seed 2's line grew almost monotonically to 133. The control on the same build and seed
had no inherited stomach at any sample.

Two things the result is not. It is **not a mutant invasion**: the lines descend from
founder-era absorptive founders (39 roots in seed 1, 26 in seed 2, against two mutant
roots each); the leak fed the founders' stomachs from the first sample and they bred.
D063 does not say how a line arises and its floor-closed clause is met (the floor shut
at 3,000 s and the lines ran 16,000–18,500 s without it), so this scores — but the
confirmation round will be read with that in view, and the invasion assay (0051) at
0.15 would show whether a *late* stomach can now establish too. And it is **a screening
pass, not a pass**: 0.02 is the screening step (0052), and D063 scores full runs at
0.01 — which is the confirmation round D070 pre-registered.

**What the leak did to the world.** Both treated populations live in the film (−0.6 and
−1.4 m), as the control does (+1.8), so the leak lands in the film and that is where the
stomachs are — the round-13 world with a second trophic level living in its surface.
The producers paid 15% of intake and kept their numbers. The standing field is thinner
than in any round-14 arm, and the second level is larger than in any of them: the income
was the constraint, as D070 said, and the standing stock was never the measure of it.

**A number to carry.** 15% of ~100 W is 14–20 W of leak, and 46–133 stomachs of 0.002–0.003
m³ at clearance 10 ate it all at 0.2–0.9 J/m³. The bracket 0.05 / 0.13 is now the
interesting direction, not 0.37: a smaller leak would show where the line's size stops
tracking the flux.

## Verdict

D070's screen passes on both seeds. The confirmation round — five seeds at dt 0.01,
30,000 s, exudation 0.15, clearance 10, scored under D063 as amended — is logbook/0054.
