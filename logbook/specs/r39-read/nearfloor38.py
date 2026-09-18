# -*- coding: utf-8 -*-
"""E10's baseline, recomputed from round 38's own positions rather than quoted.

0102 states round 38's bodies within 2 m of the floor as "0.3 to 3.6% of the living at
15,000 s, mean 1.4%", and sets E10's bar at four times that mean.  Round 38's floor is flat
at the configured depth, so the same rule this read applies to round 39 (height above the
floor under the body, within 2.0 m) is here `y + depth <= 2` — and it reproduces the range
but not the mean.  Both readings of the bar are given, since E10's verdict is the same
under either.

Writes nearfloor38.tsv beside this file.
"""
import json, glob, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
out = ['## Round 38\'s bodies within 2 m of its flat floor, at 5,000, 15,000 and 30,000 s,'
       ' recomputed from runs/r38-s*/.../positions.jsonl by this read\'s own rule.',
       'arm\tdepth m\tt\tliving\twithin 2 m of the floor\tshare']
vals = {}
for s in range(1, 6):
    p = sorted(glob.glob(os.path.join(ROOT, 'runs', 'r38-s%d' % s, '*', 'positions.jsonl')))[-1]
    cfg = json.load(open(os.path.join(os.path.dirname(p), 'config.json'), encoding='utf-8'))
    depth = cfg['world']['worldDepthMetres']
    want = {5000: None, 15000: None, 30000: None}
    for line in open(p, encoding='utf-8'):
        if not line.startswith('{"t":'):
            continue
        t = int(line[5:line.index(',', 5)])
        if t not in want or want[t] is not None:
            continue
        r = json.loads(line)
        b = r['b']
        near = sum(1 for x in b if x[2] + depth <= 2.0)
        want[t] = (len(b), near)
    for t in (5000, 15000, 30000):
        n, near = want[t]
        out.append('r38-s%d\t%g\t%d\t%d\t%d\t%.4f' % (s, depth, t, n, near, near / n))
        vals.setdefault(t, []).append(100.0 * near / n)
for t in (5000, 15000, 30000):
    v = vals[t]
    out.append('## t=%d: %.2f%% to %.2f%%, mean %.2f%%, median %.2f%%' % (
        t, min(v), max(v), sum(v) / len(v), sorted(v)[2]))
out.append('## 0102 quotes 0.3 to 3.6 per cent, mean 1.4, at 15,000 s and sets E10 at'
           ' four times that mean, 5.6. The range reproduces; the mean does not. Four'
           ' times the recomputed mean would be %.2f per cent.'
           % (4 * sum(vals[15000]) / 5.0))
with open(os.path.join(HERE, 'nearfloor38.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out))
