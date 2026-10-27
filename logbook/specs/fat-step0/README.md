# Fat step 0: the reserves of r62cf's living bodies

*2026-10-27, the speed agent. This is the step 0 of D247 and of `logbook/specs/fat-build-spec.md`: a read of every
living body's reserve from recorded checkpoints, before the screen. The census is `--reserve-census` on branch
`fat-step0` (`5ad0577c`, at r62cf's own build `6e310858`, because main's newer tunables refuse r62cf's config). It
ran on `runs/r62cf-6202` and `runs/r62cf-6204` at 12,000 s and 24,000 s, read-only, pinned to logical 22-31. The core
and dynamics hashes matched the runs'. The farm hash differs only because the census adds a farm file.*

## What it found

**D247's cap barely binds in r62cf's world at s = 1,000, and not at all at 3,000.** The cap is
`P + R + s × W`: P is the next litter's price, R the growth still to come, W the standing watts, and there is no fat
in these worlds. It binds 0.0 to 0.1% of the living at s = 1,000 and none at 3,000 (`bind-all.txt`). D098's plain
seconds would bind 2.9 to 8.0% at 1,000 s.

| s | D247's cap binds | Source |
|---|---|---|
| 0 | 19 to 27% | `low-s.txt` |
| 100 | 6 to 18% | `low-s.txt` |
| 300 | 1.7 to 8.2% | `low-s.txt` |
| 600 | 0.0 to 0.4% | `low-s.txt` |
| 1,000 | 0.0 to 0.1% | `bind-all.txt` |

**The reason is small reserves, not the litter term.** Leaves are 96 to 97% of the living. Their median reserve is 28
to 42 J at 24,000 s, against a cap near 180 J at s = 1,000. P is about 0.55 to 0.66 of 1,000 s of a leaf's standing
cost (`p-vs-sw.txt`).

**Living grazers hold enough to cross, and are not capped** (`grazers.txt`). A grazer is a body born mouthed, with
`atk` 1 and `ink` 1 in its birth row, as `r61-grazer-economy.py` reads it.

| World, second | Grazers | Median | p90 | Max | Hold 230 s or more | D247 binds at 100 / 300 / 1,000 s |
|---|---|---|---|---|---|---|
| r62cf-6202, 12,000 s | 32 | 404 s | 640 s | 710 s | 59% | 56% / 44% / 0% |
| r62cf-6202, 24,000 s | 141 | 163 s | 668 s | 909 s | 45% | 43% / 27% / 0% |
| r62cf-6204, 12,000 s | 23 | 293 s | 571 s | 724 s | 61% | 52% / 26% / 0% |
| r62cf-6204, 24,000 s | 112 | 162 s | 557 s | 783 s | 42% | 37% / 20% / 0% |

A crossing takes a median of 140 to 230 s (`logbook/specs/r62cf-read/24000/`). So two-fifths to three-fifths of the
living grazers already hold a median crossing's worth of reserve.

At s = 1,000 no grazer is capped. At s = 300 the cap would trim a fifth to two-fifths of them.

Nearly every grazer is one without photosynthesis. Only 3 of the 112 at r62cf-6204's 24,000 s are not.

## What it means

These are inferences.

- **In this world fat cannot be what makes a crossing pay.** Fat adds room above a cap. At s = 1,000 the living
  grazers are under it, and most of them already hold what a crossing costs. The limit on migration is not how much a
  grazer can store. More likely it is behaviour, with nothing that makes a fed grazer leave its oasis, or the deaths of
  the young before they could cross. A grazer's median life is 60 to 67 s.
- **The census sees only the living, at two instants.** It is weighted to the survivors. The young that die within a
  minute appear in it only while they live.
- **Arms A' and B' at s = 300 would trim many grazers.** D250 added them because D247's cap binds there. B' then shows
  whether a cap that trims a fifth to two-fifths of grazers costs D222. The 4,500 s check runs them first.
- **The tissue-above-adult bodies are float noise.** 6 to 99 bodies a census have tissue above adult tissue, by at
  most 1.7e-14 of it. The build floors R at 0.

## Sources

| Claim | Source |
|---|---|
| per-guild reserves and bind rates at 600 to 3,000 s | `bind-all.txt`, from `scripts/reads/fat/step0-bind.py` |
| D247's cap at small s | `low-s.txt`, from `scripts/reads/fat/step0-low-s.py` |
| leaves' P against s × W, R's float noise | `p-vs-sw.txt`, from `scripts/reads/fat/step0-p-vs-sw.py` |
| grazers' reserves and bind rates | `grazers.txt`, from `scripts/reads/fat/step0-grazers.py` |
| the census itself | `--reserve-census` on `fat-step0` (`5ad0577c`). Its TSVs are not committed; they come back from the runs' checkpoints in about 90 s each |
| a crossing's median, a grazer's median life | `logbook/specs/r62cf-read/24000/README.md` |
| the limit is behaviour or the young's deaths | inference |
