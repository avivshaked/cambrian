# The kill-meal screen at 24,000 s: the gut does not make grazers pay

*Written 2026-10-27 by the speed agent, from the read outputs beside this file, which `scripts/reads/r62-read.ps1`
wrote on 2026-10-26 (`read.log`). The screen is `r62kg`, pre-registered in `logbook/specs/r62kg-prereg/predictions.md`:
`r62cs`'s 500 s world with the kill gut on (D232, `killSwallowsBody`), on seeds 6201 and 6202, from `scratch/wt-r62kg`
at `8aae2320`. The runs are `runs/r62kg-6201/2026-10-26-151251-277612f7` and `runs/r62kg-6202/` of the same name.*

## The verdict

**The screen fails, by the letter of its own tests.** It passes only when tests 1 and 2 hold together in both worlds and
test 3 holds in both. Tests 1 and 2 fail in both worlds. Test 3 passes in both.

| World | Born share | Best cohort | Chain | Lowest summer-start share | Test 3 | Intake over upkeep | J per kill | Grazers at most |
|---|---|---|---|---|---|---|---|---|
| `r62kg-6201` | 0.067 | 0.63 | 2 | 0.35 | 0.826 | 0.54 | 78.8 | 20 |
| `r62kg-6202` | 0.176 | 0.68 | 2 | 0.35 | 0.811 | 0.57 | 80.3 | 23 |

The bars are a born share of 0.8, a best cohort of 1 and a chain of 10 for test 1, a summer-start share of 0.9 for
test 2, and a living share of 0.5 for test 3. "Grazers at most" is the largest count alive at a 500 s window's end.

**The forecast held.** The pre-registration forecast that the screen would fail tests 1 and 2 in both worlds, as
`r62bd` did, with J per kill near 85 to 90 J rather than 350 to 450 J. It read 79 to 80 J. As the check before the screen
found, a dead body holds almost nothing, so swallowing it changes where about 30 J goes, not how much.

**Under D244 it would fail outright.** D244 (2026-10-27) applies from `r62cf` on, so it does not judge this screen. But
neither world ever held 30 grazers, so under it both would fail at the first check.

**Lines do not keep themselves going.** The grazers that live are founders, at a median of about 675 s. Their bred
children starve young. In `r62kg-6201`, 208 of the 215 bred grazers that died did so within 60 s, at a median of
18.5 s, and 206 of those had never eaten. `r62kg-6202` is the same, at 297 within 60 s, 294 of them unfed. So the
mouthed chain stops at 2.

## Sources

| Claim | Source |
|---|---|
| born share, test 3 | `r62-health.txt` |
| best cohort | `r62g-cohorts.txt` |
| chain, intake over upkeep, J per kill | `r62g-read.txt` |
| summer-start shares, grazers at most | `r62g-windows.txt`, `r62g-seasons.txt` |
| deaths within 60 s, never ate, age at death | `grazer-tally-*.txt` |
| the forecast and the pass rule | `logbook/specs/r62kg-prereg/predictions.md` |
