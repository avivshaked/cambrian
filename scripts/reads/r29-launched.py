def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8', newline='\n').write(s)
def rep1(s, a, b, p):
    assert s.count(a) == 1, (p, a[:70], s.count(a)); return s.replace(a, b)

p = 'logbook/0072-the-round-that-asks-whether-moving-pays.md'; s = rd(p)
s = s.rstrip('\n') + """

## Launch

*2026-09-07, late evening.* `r29-s1` to `r29-s5` launched on workers 2 to 6 at commit
`40b16b8`, every manifest reading the launched `simHash e43b81a8…`, `coreHash 1084ee1c…`,
`physicsJobWorkers 0` and `gitDirty false`; every header carries V1's tokens. The machine
holds five arms and nothing else. Read as they land, against this entry and the scorer.
"""
wr(p, s); print('0072 ok')

p = 'HANDOFF.md'; s = rd(p)
a = s.index("**Round 28 is read (logbook/0070, 2026-09-07)")
b = s.index("\n\n", a)
s = s[:a] + """**Round 29 is running (logbook/0072, launched 2026-09-07 late evening), read as arms land.**
It is the movement round on round 28's world with the three senses on, added mass 0.5 and
the global brain retired (D081's build, commit `40b16b8`, `simHash e43b81a8…`). `r29-s1` to
`r29-s5` on workers 2 to 6, dt 0.01, 30,000 s, wall 1,200 min; round 28's arms are the
controls. Six predictions and their two-sided readings are in 0072; the scorer's verdict
line now says `CENSORED` for a run that ends short of its budget. Round 28 read 3 of 5
(0070) and is the base world by D081.""" + s[b:]
wr(p, s); print('HANDOFF ok')
