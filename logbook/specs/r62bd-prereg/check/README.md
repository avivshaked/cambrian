# D226's check before the screen: does a deeper wound reach the meals, and where does the cap bind? (2026-10-24)

D226 asks for a short run before launch that shows each arm's wound reaching the meals and says where the bite's cap
would bind. A D226 build cannot resume an `r62cs` checkpoint, since the config refuses the new field, so
`rounds/check-r62bd.ps1` founded seed 6201 six times and ran it for 4,500 s in one card group at group 32. The
six worlds set the dial at 4, 16 and 64, each with the intake scale at 1 and at the dial's value. Everything else is
`r62cs`'s 500 s arm. The runs are under `scratch/contact-eating/check-bd`, from commit `0bf939dd` with a clean tree.
All six ended at their budget, with both books closed on every row. Bites start at about 3,000 s, so only the
3,000 to 4,500 s bin carries bites, and only founders are grazing in it.

The tables are `bite-cap.txt` (`scripts/reads/bite-cap.py`), `r62g-read.txt` and `r62-health.txt`, read to 4,500 s
with `r62cs-w500-6201` beside the six worlds.

## The wound saturates by 4 times

Three pairs of worlds are the same world bit for bit to 4,500 s. Every stats value is equal, and lineage is equal
apart from the intake-origin fields:

- `x4-i1` and `x16-i1` (`x4-i1-vs-x16-i1-rows.txt`);
- `x16-i1` and `x64-i1` (`x16-i1-vs-x64-i1-rows.txt`);
- `x16-i16` and `x64-i64` (`x16-i16-vs-x64-i64-rows.txt`).

The dial reached them: their `config.json` files read `biteDamageScale` 4, 16 and 64. `Wound` clamps a part's health
at zero and takes no more than the part had left (`src/Evosim.Core/Ecosystem/WorldMouth.cs`). So the worlds are
equal only if every mouth's blow in 4,500 s already finished the part it touched at 4 times. At a scale of 1 the
wound is not saturated: in the same bin, `r62cs-w500-6201` offered 909 J, against 1,848 J at 4 times and the intake
scale at 1. Above 4 times the dial can only matter if the leaves later evolve enough toughness or protection that a
blow at 4 times no longer finishes a part.

## The cap

The share of what bites offered that the cap cut, plus what parts without intake bit, in the 3,000 to 4,500 s bin
(an upper bound on the cap's cut):

| World | Offered J | Kept J | Cut share |
|---|---|---|---|
| r62cs-w500-6201 (scale 1) | 909 | 572 | 0.214 |
| x4, x16 and x64 at intake 1 | 1,848 | 717 | 0.515 |
| x4 at intake 4 | 2,294 | 1,753 | 0.045 |
| x16 and x64 at intake 16 and 64 | 1,965 | 1,572 | 0.000 |

At the intake scale of 1 the cap takes half of a deeper bite. At 16 it takes none.

## The meal

From `r62g-read.txt`, over the whole 4,500 s, with only founders grazing. The numbers are thin, and they give a
direction and not a measurement:

| World | Kills | Kills a grazer-second | J a kill | Intake over upkeep |
|---|---|---|---|---|
| r62cs-w500-6201 | 2 | 2.0e-4 | 286 | 0.062 |
| x4, x16 and x64 at intake 1 | 12 | 1.2e-3 | 60 | 0.078 |
| x4 at intake 4 | 12 | 1.1e-3 | 146 | 0.178 |
| x16 and x64 at intake 16 and 64 | 9 | 8.3e-4 | 175 | 0.160 |

The deeper wound raised the grazers' income mostly through kills, which came four to six times as often. The joules a
kill kept fell, from two kills against nine to twelve. With the cap out of the way the intake over upkeep was about
2.6 to 2.9 times `r62cs-w500`'s.
