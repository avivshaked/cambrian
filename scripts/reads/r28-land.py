def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8', newline='\n').write(s)
def rep1(s, a, b, p):
    assert s.count(a) == 1, (p, a[:70], s.count(a)); return s.replace(a, b)

# ---------------- logbook/0070: Results and Verdict
p = 'logbook/0070-the-good-world-with-one-change.md'; s = rd(p)
s = s.rstrip('\n') + """

## Results

*2026-09-07.* All five arms ended on their budget of 30,000 s, in 549 to 764 minutes of
wall clock each, with the machine carrying four or five arms throughout. The replay probe
ended earlier in the day. The readings are in `logbook/specs/r28-results.md`; the verdicts come
from `scripts/clade-score.ps1` as fixed the same afternoon (its recruitment window is now
bounded at the last sample, which changes nothing for a completed run) and the rest from
`stats.jsonl`.

The validity checks held. V1's ten tokens were checked at every launch. V2: the floor was
silent after 3,100 s in every arm, the energy audit stayed within a quarter of a joule on
worlds that turned over millions, and standing matter read 6,000 at every sample of every
seed. V3: every manifest reads `ended`, `budget`, the launched `simHash`, `physicsJobWorkers
0` and `diverged 0`. The one token that reads otherwise is `gitDirty`, true in all five for
the reason the launch section gives: an uncommitted edit outside the simulation's source.

| # | reading | verdict |
|---|---|---|
| M1 | 3 of 5. `r28-s2` passes (clade of 30 at the end, minimum 29), `r28-s3` (75, minimum 51) and `r28-s4` (128, minimum 106, 28 recruits in the last window). `r28-s1` fails on stability, a late clade of 3, as its round-18 counterpart did. `r28-s5` fails on recruitment: a clade of 17, stable through the last two lifetimes, whose last inherited birth was at 21,408 s | **fails** at the bar of 4 |
| M2 | `above` at most 0.06% to 0.17% of `alive` after 3,000 s; `below` 0 in every arm | holds |
| M3 | `alive` at 30,000 s is 0.96, 1.06, 0.98, 0.98 and 1.20 of the same seed's round-18 value | holds |
| M4 | the scored clade holds 10 or more at the last sample in 4 of 5 (30, 75, 128, 17; seed 1's holds 3) | holds |
| M5 | `crowded` per window at or above the window's births in 35, 43, 30, 46 and 42 of the 149 windows after 15,000 s, in seeds 1 to 5 | **fails as written**, in every seed |
| M6 | contacts per physics step over t > 10,000: means of 972, 1,344, 914, 1,163 and 1,216; the lowest window 287, the highest 2,606 | holds |
| M7 | `r28p-s1` identical to `r28-s1` on all 10,001 digests to step 1,000,000, at about 1,700 bodies | holds |
| M8 | `alive` never below 40 to 6,000 s in any arm | holds |

Read seed by seed against round 18, the three passes and the seed-1 failure match, and
seed 5 does not. In round 18 that seed carried 221 inherited stomachs at the end and passed
with the widest margin of the five. Here it carries 20. Its stomach line grew to 439
members ever and was stable at 17 through the last 6,000 s, and it stopped breeding at
21,408 s. The two inherited absorptive births after 28,000 s belong to a younger clade of
fewer than ten. That is a sterile cohort of survivors, the shape the recruitment clause was
written against in round 8 (logbook/0044), and the clause did its job. This is one
realisation of one seed. The butterfly's spread (0052) is wide enough that a single seed's
reversal cannot be laid at contact's door on its own, and the same caution cut the other way
in round 27, whose seed 4 turned on one bit (0069).

M5 fails as written in every seed, in a fifth to a third of the late windows, while the
patches stay within 12% of each other in every arm and the populations sit at round 18's.
Bodies pack, and the packing refuses two to six conceptions per birth over a run, without
starving the world of births. Depth tells the same story: every population sat 6 to 12 m
below the surface after 10,000 s, where round 18's spread from 0.4 m above the water to
14.4 m below it. The lid explains seed 3, which lived above the waterline in round 18 and
lives at 6.5 m under it here, and M3 says the compression changed the size of nothing.

## Verdict

As pre-registered: M1 fails while M2, M3, M4, M6, M7 and M8 hold, and M5 fails as written.
The package costs the rule at round 18's bar of four seeds in five. Under D063's own bar of
three it holds, and the difference between the two bars is now a concrete question rather
than a naming one; it is on the owner's list from the Astra review of the same day.

The two-sided readings say what follows, and both of the things they name are the owner's.
The first follow-up candidate is this box with creature collisions switched off, which
separates contact from the rest of the package (the lid, the bed, the wrap, the placement).
And the width of the box goes to the owner, because M5 says bodies pack at area 100 and a
wider box is a different light budget. D079's second rule holds either way: a change that
costs the rule is read, not tuned around, so the open budget does not return to this world
yet and the movement pre-registration waits on the ruling.

What the round did settle is the instrument. A shared world of 1,800 bodies in contact
replays bit for bit to a million steps on one physics thread, the box holds its top and
its bed, and contact costs the population nothing it can be measured to cost. The rule
moved by one seed, and that seed's stomach line died of old age with its numbers intact.
"""
wr(p, s); print('0070 ok')

# ---------------- DECISIONS.md: D079's index row and a dated note
p = 'DECISIONS.md'; s = rd(p)
s = rep1(s, """| 2026-09-06 | ruled (owner: "agreed. proceed with that idea") · first round pre-registered as logbook/0070 |""",
"""| 2026-09-06 | ruled (owner: "agreed. proceed with that idea") · first round pre-registered as logbook/0070 · **round 28 read 2026-09-07 (logbook/0070): 3 of 5 under D063 as amended, below round 18's bar of 4; seeds 2, 3, 4 pass and seed 1 fails as in round 18; seed 5's stomach line stable at 17 and sterile from 21,408 s; M5 (no crowd) fails in every seed; the collisions-off control and the box's width are the owner's rulings** |""", p)
s = rep1(s, """**Note, 2026-09-07 (from the outside review of 2026-09-06, logbook/0071).** When the open
budget returns to this world,""",
"""**Round 28 read, 2026-09-07 (logbook/0070).** The first change, shared space under
single-threaded physics, met D063 as amended in three seeds of five against round 18's four.
The three passes and the seed-1 failure match round 18 seed for seed; seed 5's stomach line
held 17 members through the last two lifetimes and stopped breeding at 21,408 s, a sterile
cohort. The no-crowd prediction failed as written in every seed while populations and patch
balance matched round 18's. By rule 2 the change is read, not tuned around: the open budget
does not return yet. The pre-registered follow-ups are both the owner's: the same box with
creature collisions off, which separates contact from the lid, the bed, the wrap and the
placement; and the box's width. The bar itself is also in front of the owner (D063's note
of the same date): at D063's three of five the round holds.

**Note, 2026-09-07 (from the outside review of 2026-09-06, logbook/0071).** When the open
budget returns to this world,""", p)
wr(p, s); print('DECISIONS ok')

# ---------------- HANDOFF.md
p = 'HANDOFF.md'; s = rd(p)
a_start = s.index("**Round 28 is running (logbook/0070), read as arms land.**")
a_end = s.index("\n\n", a_start)
s = s[:a_start] + """**Round 28 is read (logbook/0070, 2026-09-07): 3 of 5, below round 18's bar of 4.** Seeds
2, 3 and 4 pass (clades of 30, 75 and 128 at the end) and seed 1 fails on stability as it did
in round 18; seed 5, round 18's widest pass, fails on recruitment with a stable clade of 17
that stopped breeding at 21,408 s. Every other prediction held except M5: bodies pack in a
fifth to a third of the late windows in every seed, with patches even and populations at
round 18's. The replay probe matched its arm on all 10,001 digests. Nothing runs on the
machine; workers 2 to 7 are free, and 7 carries the 2026-09-07 build (the others are
refreshed before the next launch, and the next launch reports a new `simHash`). **What
follows is the owner's ruling**, as 0070's two-sided readings say: the same box with
creature collisions off (separates contact from the rest of the package), the width of the
box (M5), and which bar a round is held to (D063's three of five, at which this round holds,
or round 18's four). Until then no arm is launched; the movement pre-registration
(`logbook/specs/movement-prereg-draft.md`) waits on the same ruling.""" + s[a_end:]
s = rep1(s, """Raised 2026-09-07 by the Astra review (its response file has the measurements):
""",
"""Raised 2026-09-07 by round 28's reading (logbook/0070):

- **The collisions-off control**: the same box, lid, bed, wrap and placement with
  creature-creature collisions off, five seeds at dt 0.01. It is the pre-registered first
  follow-up to an M1 failure and separates contact from the rest of D077's package.
- **The width of the box.** M5 failed in every seed at area 100: a wider box is a different
  light budget, so the width is a world rule.

Raised 2026-09-07 by the Astra review (its response file has the measurements):
""", p)
wr(p, s); print('HANDOFF ok')

# ---------------- readings file and the companion
p = 'logbook/specs/r28-results.md'; s = rd(p)
row = "| r28-s5 | 1,794 | 23 (20) | 441 born 1,989 / 17 | FAIL (recruitment: clade of 17 stable, last inherited birth 21,408; r18x-s5 passed with 221) | 0.17% | 1.20 of 1,490 (hold) | 20 at end, clade 17 (hold) | 42 of 149 (fail as written) | 1,216 (415, 1,939) | 40 | held / held (clade 1,767) | 0 | 597 |"
i = s.index("| r28-s4 |"); j = s.index("\n", i)
s = s[:j+1] + row + "\n" + s[j+1:]
s = s.rstrip("\n") + "\nNotes r28-s5: height t>10,000 -11.6 m (r18x-s5 -2.0); stock flat 6,000; 4 living clades, 0 pass; the two inherited absorptive births after 28,000 belong to a young clade (root 3778) under ten; patches [420, 454, 476, 444]; ended on budget at 597 min wall.\n\nROUND 28: 3 of 5 (s2, s3, s4). M1 FAILS at the bar of 4. M2, M3, M4, M6, M7, M8 hold. M5 fails as written in every seed (30-46 of 149 windows). Read 2026-09-07; 0070 has the Results and Verdict.\n"
wr(p, s); print('results ok')

p = 'gpt-astra-2026-09-07-1316-review-response.md'; s = rd(p)
s = rep1(s, "| 1 | in progress | `logbook/specs/r28-results.md`, logbook/0070 |", "| 1 | done | round 28 read 2026-09-07: 3 of 5, below the bar of 4; logbook/0070's Results and Verdict |", p)
wr(p, s); print('companion ok')
