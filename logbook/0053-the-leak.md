# 0053 — The leak

**2026-09-04**  ·  screen for D070 · pre-registered before launch, results appended after

The nutrient field in this world had only ever been fed by corpses, and the corpses were
getting smaller. This screen let living producers leak a fraction of the light they fix
straight into the water. Both treated seeds grew an absorptive line that passed ten and was
still there two lifetimes later, which no world had managed before. The control, the same
seed with the leak off, never had one inherited stomach at any sample.

The screen is D070's, and it tests producers that release a fraction of what they fix into
the water while alive.

## Where round 14 left it

Round 14 (0050) closed at 0 of 8. Clearance 10 grew the campaign's first absorptive lines
past twenty, and both ate their stock and crashed, while the richest seed grew no line at
all.

The detritus-flux instrument, built the same night, measured why. The nutrient field's only
income is dead tissue. That income is a founding pulse, and it decays to a few tenths of a
watt as the world selects for small bodies. A corpse carried 87 J at founding and 4.7 J by
t=15,000. A few tenths of a watt hold about six clearance-10 stomachs at replacement, and
the goal asks for ten.

The review's round 5 put the ocean's number beside ours. Phytoplankton release 10–20% of
primary production as dissolved organic matter, and the measured producer-to-herbivore
transfer step is 13%. This world ran at about 1%.

D070 rules exudation in principle and pre-registers this screen, and both gate readings were
met on 2026-09-04. The knob is `EVOSIM_EXUDATION`, which sets `RunConfig.ExudationFraction`.
It takes a fraction of a producer's post-wear light intake and deposits it into the field at
the producer's own height and patch each step, off its net. It rides on intake rather than
on death, so it does not shrink with the body the way a corpse does.

## The arms

Three, all at the 0.02 screening step (0052), at clearance 10, for 20,000 s, so that a line
is read past two lifetimes:

| arm | seed | exudation | worker | role |
|---|---|---|---|---|
| `r17x-s1` | 1 | 0.15 | w4 | the world that grew a line of 22 at 0.01 |
| `r17x-s2` | 2 | 0.15 | w5 | the world that grew a line of 48 at 0.01 |
| `r17x0-s2` | 2 | 0 | w2 | control: seed 2 at 0.02 with the leak off (seed 1's control is `r14c10-s1-flux`, 15,000 s) |

Everything else is round 14's world, from `scratch/launch-r14.ps1`'s block. That is sink
0.002 on both fields, vent off, rolls and four patches, with the floor closing at 3,000,
senescence 3,000 and cell-type mutation 0.005. The launcher is `scratch/launch-r17.ps1`.

Per 0052, a 0.02 arm is a different chaotic realisation of its seed from the 0.01 arm. So
the comparison is against the 0.02 control and against the distribution round 14
established, rather than row for row against `r14c10-sN`.

A note on config hashes. `ExudationFraction` is a new tunable and enters `Hash()`, so every
run from this build carries a different hash from the same settings before it. Compare
headers token by token, not by hash, across this boundary.

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

- If M3 holds, the flux was the constraint and the leak lifts it. Next is a confirmation
  round at 0.01 under D063 as written, over five seeds and scored, and DESIGN.md takes the
  rule with the review's fraction and caveats.

- If M1 and M2 hold and M3 fails, the income rose and the stomachs still cannot hold ten.
  Then something between the field and the child is binding that the ledger does not see.
  The dissection of `r14c10-s4` already found stomachs that should breed and did not, with
  no output recording where they were. The per-creature absorptive ledger log
  (`scratch/absorptive-log-spec.md`) is the next instrument, before any further world
  change. The review's caveat (iii) applies: in the ocean too, exudation alone does not
  close the consumers' demand.

- If M1 fails, the exuded joules do not show up as income at the producers' depth. Either
  they sink or mix away faster than the stomachs eat them, or the producer economy is
  smaller than the 17 W estimated. Read `det exuded` against the light columns before
  touching the fraction.

- If M4 fails and producers collapse, the 15% tax is too much for this world's producers,
  which would be a surprise given the review's numbers. The bracket 0.05 / 0.13 is next.

- If M2 holds by a bloom and the ceiling is reached, the leak fed a runaway. Censor the
  round, read which guild ran, and the lever is the fraction, downward.

## Launch

Three arms on w2, w4 and w5, all refreshed to commit `0d15e6f` and hash-checked, with the
monitor's watch list carrying them. Headers are verified before any arm is believed, and
results are appended below.

## Results

All three arms reached budget on 2026-09-04, in 39.5, 54.4 and 53.5 min of wall clock at
6–8× real time. The treated arms limited 122 and 201 drag impulses.

V1 held on every header. V2 held, since the two seed-2 arms differ at t=500. V3 held, with
`floor` 0 from 3,100 in all three.

V4 held as well, with the audit at 0.0000% every row. Summed over the run,
`det in + det exuded − det out` equals the final `detritus J` to the tenth of a joule in all
three arms, at 5,524.0, 10,491.9 and 15,486.8.

| arm | exudation | alive at 20,000 | inherited line | ≥ 10 from | at two lifetimes after | never below | at 20,000 | absorptive births in last 20 samples | D063's clauses |
|---|---|---|---|---|---|---|---|---|---|
| `r17x-s1` | 0.15 | 1,571 | every sample from t=2,400 (177 of 177) | 4,100 | **77** (t=10,100) | 10 | **46** | 18 | ✓ ✓ ✓ ✓ |
| `r17x-s2` | 0.15 | 1,801 | every sample from t=500 (196 of 196) | 1,600 | **83** (t=7,600) | 12 | **133** | 19 | ✓ ✓ ✓ ✓ |
| `r17x0-s2` | 0 | 1,789 | none, ever | — | — | — | 0 | 0 | ✓ ✗ ✗ ✗ |

The flux at t > 10,000, in watts per arm:

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

With the leak on, the stomachs the founding lottery drew never died out. In seed 2 an
inherited line existed from the fifth sample and passed ten at t=1,600. In seed 1 it existed
from t=2,400 and passed ten at t=4,100. Both were before the floor closed, and the lines
then ran unbroken to the end without it.

Seed 1's line fell to ten at t=8,000 while its field read 1.8 J/m³. It then climbed to 77 by
t=10,000, as the field was eaten back to a tenth of a joule. That is a consumer-resource
oscillation with a floor, the oscillating equilibrium the owner asked about on 2026-09-03,
seen for the first time. Seed 2's line grew almost monotonically to 133, and the control on
the same build and seed had no inherited stomach at any sample.

Two things the result is not. It is not a mutant invasion. The lines descend from
founder-era absorptive founders, at 39 roots in seed 1 and 26 in seed 2, against two mutant
roots each. The leak fed the founders' stomachs from the first sample and they bred. D063
does not say how a line arises, and its floor-closed clause is met. The floor shut at 3,000
s, and the lines ran 16,000–18,500 s without it. So this scores, and the confirmation round
will be read with that in view. The invasion assay (0051) at 0.15 would show whether a
*late* stomach can now establish too.

It is also a screening pass rather than a pass. The step 0.02 is for screening (0052), and
D063 scores full runs at 0.01, which is the confirmation round D070 pre-registered.

Here is what the leak did to the world. Both treated populations live in the film, at −0.6
and −1.4 m, as the control does at +1.8. So the leak lands in the film, and that is where
the stomachs are: the round-13 world with a second trophic level living in its surface. The
producers paid 15% of intake and kept their numbers.

The standing field is thinner than in any round-14 arm, and the second level is larger than
in any of them. The income was the constraint, as D070 said, and the standing stock was
never the measure of it.

Here is one number to carry. A leak of 15% of ~100 W is 14–20 W. Stomachs of 0.002–0.003 m³
at clearance 10, 46 to 133 of them, ate all of it at 0.2–0.9 J/m³. The bracket 0.05 / 0.13
is now the interesting direction rather than 0.37, because a smaller leak would show where
the line's size stops tracking the flux.

## Verdict

D070's screen passes on both seeds. The confirmation round is logbook/0054: five seeds at dt
0.01, 30,000 s, exudation 0.15 and clearance 10, scored under D063 as amended.
