"""One-off: 0077's Results and Verdict, the D085 index row, HANDOFF status."""
import os
os.chdir(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

p = 'logbook/0077-the-water-stirred-less.md'
s = open(p, encoding='utf-8').read()
assert s.rstrip().endswith("arms.")
s = s.rstrip() + """

## Results

*2026-09-09, morning.* All five ended on budget at 30,000 s in 9 to 12 hours of wall
clock, `physicsJobWorkers 0`, no impulse limiter bound. Seeds 1, 2, 4 and 5 on
`coreHash 73047e8e…`; seed 3 is `r31-s3b` on the widened guard, `coreHash d0f27c7b…`, with
its seven counted divergences at 11,533.5 s and nothing after. V1 to V3 hold, with that one
caveat on seed 3. Read against round 30's same seed, `scripts/reads/r31-read.py`.

| arm | alive (r30) | scorer | stomach clade at end (r30 `inherit`) | sense share | `food rig` last 6,000 s (r30) | `J/m3 here` (r30) | `det deep` (r30) | floor max | detritus kJ (r30) |
|---|---|---|---|---|---|---|---|---|---|
| `r31-s1` | 1,757 (1,743) | fail, stability 5 | 20 (38) | 1% | 42.0 (6.2) | 5.98 (3.11) | 2.59 (0.72) | 3.6% | 23.1 (7.5) |
| `r31-s2` | 1,824 (1,727) | pass, 132 | 132 (82) | 26% | 5.4 (4.2) | 0.54 (1.93) | 2.99 (0.43) | 6.0% | 27.7 (7.0) |
| `r31-s3b` | 1,768 (1,770) | pass, 40 | 44 (69) | 5% | 8.4 (5.0) | 1.18 (2.48) | 0.88 (0.29) | 4.9% | 8.4 (8.8) |
| `r31-s4` | 1,706 (1,840) | pass, 126 | 134 (30) | 17% | 3.7 (18.4) | 0.65 (7.85) | 2.09 (2.92) | 5.7% | 6.8 (25.8) |
| `r31-s5` | 1,772 (1,776) | pass, 49 | 53 (58) | 55% | 13.6 (5.3) | 1.10 (2.47) | 1.81 (0.63) | 6.9% | 11.4 (11.3) |

- **M0 held.** Every arm within 0.93 to 1.06 of round 30's population, and D063 as amended
  holds in 4 of 5. Seed 1's line thinned from 20 at 15,000 s to 5 in the last two lifetimes
  and fails on stability alone; its exudate piled to 23 kJ as the line went.
- **M1 failed, and in the other direction.** The prediction was a hole at the sitter: its
  intake down, the water at large not down by as much. What happened is the reverse. The
  water at large emptied, 52 to 92% below round 30 in four seeds, and the sitting stomachs
  ate the same or more in four seeds. Less stirring did not starve the sitter. It stopped
  carrying the exudate away from the producer the sitter sits beside, and the world at
  large went hungry instead. The one seed where the sitters ate less, seed 4, is the one
  whose round 30 realisation was unusually rich, 18 J per body against 4 to 6 elsewhere.
- **M2 failed.** No jointed guild in any seed. The count of bodies born with a joint peaked
  at 7 in seed 3 at 2,300 s, at 0 to 2 in the rest, all before 5,300 s, and read 0 at every
  sample of the last 6,000 s in every arm. M3 does not apply.
- **M4 held in three.** Out over exuded is 1.06 to 1.09 in every seed; the standing detritus
  is under three times round 30's in three, and over it in seeds 1 and 2, where the stomach
  lines thinned late and the exudate piled.
- **M5 failed.** The deep layer holds more than round 30's in four seeds, three to seven
  times, and the floor's share is over 5% in four. The larder went deep after all. 0076's
  column profile was one lifetime; over ten the sink wins where the stirring no longer
  returns what fell.
- **M6 held in four**, with seed 3's seven divergences as its caveat; identities at zero on
  every row of every arm, the vertex count under half the cap.
- **Not predicted, and worth a line.** The share of bodies carrying a sense is 17, 26 and
  55% in seeds 4, 2 and 5, where round 30's same seeds read 2 to 3%. Round 30 carried a
  sense in one seed of five. Whether any is used stays the inoculated round's question.

## Verdict

The world can pay the price of stirring less: the goal rule holds in four seeds of five,
against five in round 30, with the population unchanged. The prize the round was built for
did not appear where it was predicted. The hole is not at the sitter. The producer beside
every stomach refills it, and what the stirring had been doing was carrying the exudate
away from those pairs to the water at large. With less stirring the pairs ate better and
the world elsewhere emptied. That is 0077's second reading, "M0 holds, M1 fails", and its
lever is the exudate fraction. No swimmer lived to be tested, because no joint survived
founding in any seed, which is the bottleneck this round names and cannot move. The
larder went deep, and the pre-registered reading says remineralisation goes on in the
round after. Two of my predictions were wrong: the hole's location, and the depth
profile's persistence.

The round after is not on this water. The owner's argument about where a hole lives
replaced the vertex field with a grid the same night (logbook/0078), so round 32 is the
grid's base round at these prices and this mixing, with the floor's return held off as
the control, and the deep larder is read there from the grid's own `det deep` before the
remineralisation lever is pulled. This is the vertex world's last word.
"""
open(p, 'w', encoding='utf-8', newline='').write(s + "\n"); print('0077 ok')

p = 'DECISIONS.md'
s = open(p, encoding='utf-8').read()
old = "| [D085](#d085) |"
i = s.index(old); j = s.index("\n", i)
row = s[i:j]
assert row.count("|") >= 4
cells = row.split("|")
# last non-empty cell is the status
k = len(cells) - 2
cells[k] = " ruled (owner: \"proceed with your recommendations\"); read 2026-09-09 (logbook/0077): 4 of 5, the world stands at 0.02, the hole is not at the sitter but in the water at large, no swimmer, the larder went deep "
s = s[:i] + "|".join(cells) + s[j:]
open(p, 'w', encoding='utf-8', newline='').write(s); print('DECISIONS ok', row[:80])

p = 'HANDOFF.md'
s = open(p, encoding='utf-8').read()
old = "0.02 on both axes, screened at the fast step first; it runs overnight, seed 3 rerun on the\nwidened divergence guard (0077)."
new = "0.02 on both axes, read 2026-09-09 (0077): 4 of 5 under D063 as amended, the population\nunchanged, the hole in the water at large rather than at the sitter, no joint surviving\nfounding in any seed, the larder gone deep; the vertex world's last word."
assert s.count(old) == 1; s = s.replace(old, new)
old = "   the round is running: `r31-s1..s5` at dt 0.01 on workers 2 to 6, launched 2026-09-08\n   night (`rounds/launch-r31.ps1`), landing in about ten hours; read against 0077's\n   predictions and the scorer."
new = "   the round ran at dt 0.01 on workers 2 to 6 (`rounds/launch-r31.ps1`) and is read\n   (0077 Results and Verdict): M0 held, M1 failed in reverse, M2 failed, M5 failed, M4 in\n   three, M6 in four with seed 3's caveat."
assert s.count(old) == 1; s = s.replace(old, new)
open(p, 'w', encoding='utf-8', newline='').write(s); print('HANDOFF ok')
