# The pool's four bodies on round 48's prices

*2026-09-24. The ledger (`scripts/ledger.ps1`) for the four stomach bodies of the round 47
pool (`inocula/pool-r47/`, genome format 9), read twice: on the launcher's first config, with
the overhead floor at 10 J, and on the config the round runs, with the floor at 50 J after
the count screen. Both at the config's clearance of ten volumes a second, depth 5 m and a
spent density of 0.25 units/m³. Round 47's column is 0117's table
(`logbook/specs/r46-read/ledger-stomachs.md`), since this build refuses round 47's config.*

The configs are `scratch/r48-build/runs/r48cfg` (`configHash 708fed6e3fe869b7`, the 10 J
floor) and `scratch/r48-build/runs/r48cfg50` (`configHash e5a30c15db9fc4db`, the 50 J
floor). The command, for each genome:

```powershell
./scripts/ledger.ps1 -Genome <genome> -Config <config.json> -Clearance 10 -Depth 5 `
    -Density 0.5,1,2 -Spent 0.25
```

## At the floor the round runs, 50 J

| body | break-even J/m³, r47 → r48 | net W at 0.5 | lifetime at 0.5 J/m³ | R0 at 1 J/m³ (first child), r47 → r48 | R0 at 2 (first child), r47 → r48 | units a child |
|---|---|---|---|---|---|---|
| the screens' inoculum, one part (r45s1-100) | 0.44 → 0.44 | +0.045 | 826 s | 2 (410 s) → 11 (228 s) | 12 (122 s) → 94 (78 s) | 0.69 |
| seed 1's 1182, stomach and link | 0.17 → 0.27 | +0.33 | 2,502 s | 2 (847 s) → 10 (682 s) | 16 (262 s) → 78 (247 s) | 1.32 |
| seed 3's 1513, stomach and link | 0.29 → 0.35 | +0.14 | 1,900 s | 3 (338 s) → 15 (239 s) | 15 (123 s) → 110 (90 s) | 0.68 |
| seed 3's 2156, stomach and link | 0 → 0.02 | +0.35 | 3,526 s | 2 (568 s) → 10 (513 s) | 12 (269 s) → 48 (231 s) | 0.94 |

## At the first floor, 10 J

| body | R0 at 1 J/m³ (first child) | R0 at 2 (first child) |
|---|---|---|
| the screens' inoculum, one part | 15 (179 s) | 133 (62 s) |
| seed 1's 1182 | 10 (682 s) | 78 (247 s) |
| seed 3's 1513 | 22 (199 s) | 162 (76 s) |
| seed 3's 2156 | 10 (513 s) | 48 (231 s) |

The break-even, the net watts and the lifetimes are the same at both floors, since the
overhead is paid at conception and nowhere else.

## The reading

None of the four breeds at 0.5 J/m³ on either build, and the break-even barely moves. The
rulings do not make a stomach cheaper to run. What they change is what a founder does with
water it can live in: at 1 J/m³ the four have four to seven times the R0 they had at round
47's prices, and their first child comes sooner. The two whose children cost more than
25 J of tissue pay twice that tissue at either floor, so the floor's move from 10 J to 50 J
did not touch them. The two with small children pay the floor, and lost about a quarter of
their R0 to it. The intake no longer wears with age, so the lifetime at 0.5 J/m³ runs to
826 to 3,526 s where 0117 read 420 to 3,400.

The break-even rose a little for the two-part bodies (0.17 to 0.27, 0.29 to 0.35). The
ledger prices the link's photosynthesis by exposure and by the pose, which is an inference
from the config alone and was not checked. The ledger is a lump reading. A gestating body
banks half its net and breeds from the account, which the ledger does not model.
