# -*- coding: utf-8 -*-
"""Round 37 read: everything the run report can answer, keyed by column name."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from report_read import rows, num

R37 = ['r37-s%d' % i for i in range(1, 6)]
R36 = ['r36-s%d' % i for i in range(1, 6)]
TIMES = [5000, 15000, 30000]

data = {}
for a in R37 + R36:
    r, h = rows(a)
    data[a] = ({x['t']: x for x in r}, r)

def at(a, t):
    return data[a][0][t]

out = []
def w(s=''):
    out.append(s)

# ---------------- M0 alive at 30,000
w('## M0  alive at 30,000 s')
w('seed\tr37 alive\tr36 alive\tratio')
for i in range(5):
    a7, a6 = num(at(R37[i], 30000)['alive']), num(at(R36[i], 30000)['alive'])
    w('%d\t%d\t%d\t%.3f' % (i + 1, a7, a6, a7 / a6))

# ---------------- M1 p3 share
w()
w('## M1  p3 over alive (rim ring, a quarter of the area)')
w('seed\t' + '\t'.join('t=%d p3\talive\tshare' % t for t in TIMES))
for i in range(5):
    cells = []
    for t in TIMES:
        row = at(R37[i], t)
        p3, al = num(row['p3']), num(row['alive'])
        cells += ['%d' % p3, '%d' % al, '%.3f' % (p3 / al)]
    w('%d\t' % (i + 1) + '\t'.join(cells))
w()
w('## M1  patch shares in full (r37)')
w('seed\tt\talive\tp0\tp1\tp2\tp3\ts0\ts1\ts2\ts3\tsum-alive')
for i in range(5):
    for t in TIMES:
        row = at(R37[i], t)
        al = num(row['alive'])
        p = [num(row['p%d' % k]) for k in range(4)]
        w('%d\t%d\t%d\t' % (i + 1, t, al) + '\t'.join('%d' % v for v in p) + '\t' +
          '\t'.join('%.3f' % (v / al) for v in p) + '\t%d' % (sum(p) - al))
w()
w('## M1  p3 share every 1000 s, r37')
w('t\t' + '\t'.join('s%d' % (i + 1) for i in range(5)))
for t in range(1000, 30001, 1000):
    cells = []
    for i in range(5):
        row = at(R37[i], t)
        cells.append('%.3f' % (num(row['p3']) / num(row['alive'])))
    w('%d\t' % t + '\t'.join(cells))
w()
w('## M1 control  p3 share every 1000 s, r36 (patch 3 of a row of four square patches)')
w('t\t' + '\t'.join('s%d' % (i + 1) for i in range(5)))
for t in range(1000, 30001, 1000):
    cells = []
    for i in range(5):
        row = at(R36[i], t)
        cells.append('%.3f' % (num(row['p3']) / num(row['alive'])))
    w('%d\t' % t + '\t'.join(cells))

# ---------------- M2 report cols and x sd
w()
w('## M2  report cols and x sd, r37')
w('seed\t' + '\t'.join('t=%d cols\tx sd' % t for t in TIMES))
for i in range(5):
    cells = []
    for t in TIMES:
        row = at(R37[i], t)
        cells += [row['cols'], row['x sd']]
    w('%d\t' % (i + 1) + '\t'.join(cells))
w()
w('## M2 control  r36 report cols / x sd (circular deviation in a box)')
w('seed\t' + '\t'.join('t=%d cols\tx sd' % t for t in TIMES))
for i in range(5):
    cells = []
    for t in TIMES:
        row = at(R36[i], t)
        cells += [row['cols'], row['x sd']]
    w('%d\t' % (i + 1) + '\t'.join(cells))
w()
w('## M2  report cols above 100 (only 100 columns are live in the tank)')
for i in range(5):
    r = data[R37[i]][1]
    over = [(x['t'], num(x['cols'])) for x in r if num(x['cols']) > 100]
    w('%s\t%d of 300 samples over 100\tmax %d' % (R37[i], len(over),
      max([v for _, v in over], default=0)))

# ---------------- M4 jnt inh
w()
w('## M4  jnt inh at 30,000 s')
w('seed\tr37 jnt inh\tr36 jnt inh\tr37 jointed\tr36 jointed')
for i in range(5):
    a7, a6 = at(R37[i], 30000), at(R36[i], 30000)
    w('%d\t%s\t%s\t%s\t%s' % (i + 1, a7['jnt inh'], a6['jnt inh'], a7['jointed'], a6['jointed']))
w()
w('## M4  the joint over time')
w('arm\tlast t with jnt inh>=10\tpeak jnt inh\tat t')
for i in range(5):
    for arm in (R37[i], R36[i]):
        r = data[arm][1]
        last = [x['t'] for x in r if num(x['jnt inh']) >= 10]
        pk = max(r, key=lambda x: num(x['jnt inh']))
        w('%s\t%s\t%d\t%d' % (arm, last[-1] if last else 'never', num(pk['jnt inh']), pk['t']))

# ---------------- M5 mean m/s
w()
w('## M5  mean m/s')
w('seed\tr37 t=5000\tr36 t=5000\tratio\tr37 t=30000\tr36 t=30000\tratio')
for i in range(5):
    c = []
    for t in (5000, 30000):
        v7, v6 = num(at(R37[i], t)['mean m/s']), num(at(R36[i], t)['mean m/s'])
        c += ['%.4f' % v7, '%.4f' % v6, '%.3f' % (v7 / v6)]
    w('%d\t' % (i + 1) + '\t'.join(c))
w()
w('## M5  mean m/s averaged over all 300 samples')
w('seed\tr37 mean\tr36 mean\tratio')
for i in range(5):
    m7 = sum(num(x['mean m/s']) for x in data[R37[i]][1]) / 300
    m6 = sum(num(x['mean m/s']) for x in data[R36[i]][1]) / 300
    w('%d\t%.4f\t%.4f\t%.3f' % (i + 1, m7, m6, m7 / m6))

# ---------------- M6 diverged and exposure
w()
w('## M6  diverged (cumulative column at the last sample)')
w('seed\tr37 diverged\tr36 diverged\tbound 2*r36+2')
for i in range(5):
    d7, d6 = num(at(R37[i], 30000)['diverged']), num(at(R36[i], 30000)['diverged'])
    w('%d\t%d\t%d\t%d' % (i + 1, d7, d6, 2 * d6 + 2))
w()
w('## M6  exposure: jointed body-seconds and divergences per 1e6 of them')
w('round\tseed\tsum jnt inh\tinterval s\tjnt-inh body-s\tdiverged\tper 1e6 jnt-inh body-s'
  '\tsum jointed\tjointed body-s\tper 1e6 jointed body-s')
for lbl, arms in (('r37', R37), ('r36', R36)):
    for i in range(5):
        r = data[arms[i]][1]
        interval = r[1]['t'] - r[0]['t']
        s_inh = sum(num(x['jnt inh']) for x in r)
        s_all = sum(num(x['jointed']) for x in r)
        bs_inh, bs_all = s_inh * interval, s_all * interval
        d = num(r[-1]['diverged'])
        w('%s\t%d\t%d\t%d\t%d\t%d\t%s\t%d\t%d\t%s' % (
            lbl, i + 1, s_inh, interval, bs_inh, d,
            ('%.2f' % (d * 1e6 / bs_inh)) if bs_inh else 'n/a',
            s_all, bs_all,
            ('%.2f' % (d * 1e6 / bs_all)) if bs_all else 'n/a'))

# ---------------- the cycle
w()
w('## Cycle  local extrema of alive, r37 (a sample is an extremum of its +-1500 s window)')
w('seed\tkind\tt\talive\tp3 share\tdepth m\tjnt inh')

def extrema(r, half=15):
    vals = [num(x['alive']) for x in r]
    n = len(vals)
    res = []
    for k in range(n):
        lo, hi = max(0, k - half), min(n, k + half + 1)
        win = vals[lo:hi]
        if vals[k] == max(win) and vals[k] > min(win):
            res.append(('peak', k))
        elif vals[k] == min(win) and vals[k] < max(win):
            res.append(('trough', k))
    out2, last = [], None
    for kind, k in res:
        if last and last[0] == kind and k - last[1] <= half:
            continue
        out2.append((kind, k))
        last = (kind, k)
    return out2

for i in range(5):
    r = data[R37[i]][1]
    for kind, k in extrema(r):
        x = r[k]
        w('%d\t%s\t%d\t%d\t%.3f\t%s\t%s' % (i + 1, kind, x['t'], num(x['alive']),
          num(x['p3']) / num(x['alive']), x['depth m'], x['jnt inh']))
w()
w('## Cycle  amplitude over t >= 5000')
w('round\tseed\tmin alive\tt\tmax alive\tt\tmax/min')
for lbl, arms in (('r37', R37), ('r36', R36)):
    for i in range(5):
        r = [x for x in data[arms[i]][1] if x['t'] >= 5000]
        mn = min(r, key=lambda x: num(x['alive']))
        mx = max(r, key=lambda x: num(x['alive']))
        w('%s\t%d\t%d\t%d\t%d\t%d\t%.2f' % (lbl, i + 1, num(mn['alive']), mn['t'],
          num(mx['alive']), mx['t'], num(mx['alive']) / num(mn['alive'])))

# ---------------- extras
w()
w('## Extras  at the last sample')
w('round\tseed\tstillb (cumulative)\tcrowded (summed windows)\tmat blk\tfood %\tabsorpt\tdepth m\tdepth sd')
for lbl, arms in (('r37', R37), ('r36', R36)):
    for i in range(5):
        r = data[arms[i]][1]
        last = r[-1]
        w('%s\t%d\t%d\t%d\t%s\t%s\t%s\t%s\t%s' % (lbl, i + 1, num(last['stillb']),
          sum(num(x['crowded']) for x in r), last['mat blk'], last['food %'],
          last['absorpt'], last['depth m'], last['depth sd']))

text = '\n'.join(out) + '\n'
open(os.path.join(HERE, 'report-readings.tsv'), 'w', encoding='utf-8').write(text)
print(text)
