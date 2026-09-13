# -*- coding: utf-8 -*-
"""Round 37b read: everything the run report can answer, keyed by column name.

Seed 5 is censored at 29,200 s (stopped, manual-stall): wherever a prediction says
30,000 s it is read at its last sample and the row says so.
Writes the brief's TSVs beside this file.
"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

B = ['r37b-s%d' % i for i in range(1, 6)]
R37 = ['r37-s%d' % i for i in range(1, 6)]
R36 = ['r36-s%d' % i for i in range(1, 6)]
TIMES = [5000, 15000, 30000]

data = {}
for a in B + R37 + R36:
    r, h = rows(a)
    data[a] = ({x['t']: x for x in r}, r)

LAST = {a: data[a][1][-1]['t'] for a in B}


def at(a, t):
    """The sample at t, or the arm's last sample if the run ended short of it."""
    d, r = data[a]
    if t in d:
        return d[t]
    return r[-1]


def mark(a, t):
    return ' (censored at %d s)' % LAST[a] if t > LAST[a] else ''


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


# ---------------------------------------------------------------- M0
m0 = ['## M0  alive at 30,000 s, round 37b against round 37, same seed',
      'seed\tr37b t\tr37b alive\tr37 alive\tratio\tholds 0.5-1.5\tnote']
for i in range(5):
    a = B[i]
    t = min(30000, LAST[a])
    v, c = num(at(a, 30000)['alive']), num(at(R37[i], 30000)['alive'])
    r = v / c
    m0.append('%d\t%d\t%d\t%d\t%.3f\t%s\t%s' % (
        i + 1, t, v, c, r, 'holds' if 0.5 <= r <= 1.5 else 'FAILS',
        'censored at 29200 s' if t != 30000 else ''))

# ---------------------------------------------------------------- M1  p3
p3 = ['## M1  p3 (the rim ring, a quarter of the disc by area) over alive',
      'seed\t' + '\t'.join('t=%d p3\talive\tshare' % t for t in TIMES) + '\tin band 15-40% at all three']
for i in range(5):
    a, cells, ok = B[i], [], True
    for t in TIMES:
        row = at(a, t)
        v, al = num(row['p3']), num(row['alive'])
        s = v / al
        ok &= 0.15 <= s <= 0.40
        cells += ['%d' % v, '%d' % al, '%.3f' % s]
    p3.append('%d\t' % (i + 1) + '\t'.join(cells) + '\t' + ('yes' if ok else 'no') +
              ('  (t=30000 read at %d s)' % LAST[a] if LAST[a] < 30000 else ''))
p3 += ['', '## M1  all four ring counts and shares at the three times',
       'seed\tt\talive\tp0\tp1\tp2\tp3\ts0\ts1\ts2\ts3\tsum(p)-alive']
for i in range(5):
    for t in TIMES:
        row = at(B[i], t)
        al = num(row['alive'])
        p = [num(row['p%d' % k]) for k in range(4)]
        p3.append('%d\t%d\t%d\t' % (i + 1, row['t'], al) + '\t'.join('%d' % v for v in p) +
                  '\t' + '\t'.join('%.3f' % (v / al) for v in p) + '\t%d' % (sum(p) - al))
p3 += ['', '## M1  p3 share every 1000 s (blank where the run had ended)',
       't\t' + '\t'.join('s%d' % (i + 1) for i in range(5))]
for t in range(1000, 30001, 1000):
    cells = []
    for i in range(5):
        d = data[B[i]][0]
        cells.append('%.3f' % (num(d[t]['p3']) / num(d[t]['alive'])) if t in d else '')
    p3.append('%d\t' % t + '\t'.join(cells))
p3 += ['', '## M1 control  r37 p3 share at the three times',
       'seed\t' + '\t'.join('t=%d' % t for t in TIMES)]
for i in range(5):
    p3.append('%d\t' % (i + 1) + '\t'.join(
        '%.3f' % (num(at(R37[i], t)['p3']) / num(at(R37[i], t)['alive'])) for t in TIMES))
write('p3-share.tsv', p3)

# ---------------------------------------------------------------- M2
m2 = ['## M2  report cols (corrected count) and x sd',
      'seed\t' + '\t'.join('t=%d cols\tcols abs\tx sd' % t for t in TIMES) +
      '\tcols>=60 at all three\tx sd in 2.0-3.4 at all three']
for i in range(5):
    a, cells, okc, oks = B[i], [], True, True
    for t in TIMES:
        row = at(a, t)
        okc &= num(row['cols']) >= 60
        sd = num(row['x sd'])
        oks &= 2.0 <= sd <= 3.4
        cells += [row['cols'], row['cols abs'], row['x sd']]
    m2.append('%d\t' % (i + 1) + '\t'.join(cells) + '\t%s\t%s' % (
        'yes' if okc else 'no', 'yes' if oks else 'no'))
m2 += ['', '## M2  cols from 5,000 s onward: minimum, and any sample over 100',
       'arm\tsamples t>=5000\tmin cols\tmin at t\tsamples over 100\tmin x sd\tmax x sd\tmean x sd']
for i in range(5):
    r = [x for x in data[B[i]][1] if x['t'] >= 5000]
    mn = min(r, key=lambda x: num(x['cols']))
    sds = [num(x['x sd']) for x in r]
    m2.append('%s\t%d\t%s\t%d\t%d\t%.2f\t%.2f\t%.2f' % (
        B[i], len(r), mn['cols'], mn['t'],
        sum(1 for x in r if num(x['cols']) > 100), min(sds), max(sds), sum(sds) / len(sds)))

# ---------------------------------------------------------------- M4
m4 = ['## M4  inherited jointed bodies at 30,000 s (seed 5 at 29,200 s)',
      'seed\tt\tr37b jnt inh\tr37b jointed\tr37 jnt inh\tr37 jointed\t>=10']
for i in range(5):
    a = B[i]
    row, c = at(a, 30000), at(R37[i], 30000)
    m4.append('%d\t%d\t%s\t%s\t%s\t%s\t%s' % (
        i + 1, row['t'], row['jnt inh'], row['jointed'], c['jnt inh'], c['jointed'],
        'yes' if num(row['jnt inh']) >= 10 else 'no'))
m4 += ['', '## M4  the joint over the run',
       'arm\tpeak jnt inh\tat t\tlast t with jnt inh>=10\tpeak jointed\tat t']
for i in range(5):
    for arm in (B[i], R37[i]):
        r = data[arm][1]
        last = [x['t'] for x in r if num(x['jnt inh']) >= 10]
        pk = max(r, key=lambda x: num(x['jnt inh']))
        pj = max(r, key=lambda x: num(x['jointed']))
        m4.append('%s\t%d\t%d\t%s\t%d\t%d' % (arm, num(pk['jnt inh']), pk['t'],
                  last[-1] if last else 'never', num(pj['jointed']), pj['t']))

# ---------------------------------------------------------------- M5
m5 = ['## M5  mean m/s against round 36, same seed (the column reads the water, not locomotion)',
      'seed\tt\tr37b\tr36\tratio\tholds 0.7-1.3']
for i in range(5):
    for t in (5000, 30000):
        row = at(B[i], t)
        v, c = num(row['mean m/s']), num(at(R36[i], t)['mean m/s'])
        r = v / c
        m5.append('%d\t%d\t%.4f\t%.4f\t%.3f\t%s' % (
            i + 1, row['t'], v, c, r, 'holds' if 0.7 <= r <= 1.3 else 'FAILS'))
m5 += ['', '## M5  run means over every sample', 'seed\tr37b mean\tr36 mean\tratio\tr37 mean\tr37b/r37']
for i in range(5):
    def mean(a):
        r = data[a][1]
        return sum(num(x['mean m/s']) for x in r) / len(r)
    mb, m6_, m7_ = mean(B[i]), mean(R36[i]), mean(R37[i])
    m5.append('%d\t%.4f\t%.4f\t%.3f\t%.4f\t%.3f' % (i + 1, mb, m6_, mb / m6_, m7_, mb / m7_))

# ---------------------------------------------------------------- M6 exposure
ex = ['## M6  divergences and the exposure that produced them',
      'round\tseed\tsamples\tinterval s\tsum jnt inh\tjnt-inh body-s\tsum jointed\tjointed body-s'
      '\tdiverged\tper 1e6 jnt-inh body-s\tper 1e6 jointed body-s']
tot = {}
for lbl, arms in (('r37b', B), ('r37', R37), ('r36', R36)):
    T = [0, 0, 0]
    for i in range(5):
        r = data[arms[i]][1]
        interval = r[1]['t'] - r[0]['t']
        s_inh = sum(num(x['jnt inh']) for x in r)
        s_all = sum(num(x['jointed']) for x in r)
        bi, ba = s_inh * interval, s_all * interval
        d = num(r[-1]['diverged'])
        T[0] += bi; T[1] += ba; T[2] += d
        ex.append('%s\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%s\t%s' % (
            lbl, i + 1, len(r), interval, s_inh, bi, s_all, ba, d,
            ('%.2f' % (d * 1e6 / bi)) if bi else 'n/a',
            ('%.2f' % (d * 1e6 / ba)) if ba else 'n/a'))
    tot[lbl] = T
ex.append('')
ex.append('round\tall five seeds: jnt-inh body-s\tjointed body-s\tdiverged\tper 1e6 jnt-inh\tper 1e6 jointed')
for lbl, T in tot.items():
    ex.append('%s\t%d\t%d\t%d\t%.2f\t%.2f' % (lbl, T[0], T[1], T[2],
              T[2] * 1e6 / T[0] if T[0] else 0, T[2] * 1e6 / T[1] if T[1] else 0))
write('exposure.tsv', ex)

# ---------------------------------------------------------------- M7 cycle
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

cy = ['## M7  local extrema of alive over t >= 5000 (a sample extreme in its +-1500 s window)',
      'seed\tkind\tt\talive\tp3 share\tdepth m\tjnt inh\tshade %']
for i in range(5):
    r = [x for x in data[B[i]][1] if x['t'] >= 5000]
    for kind, k in extrema(r):
        x = r[k]
        cy.append('%d\t%s\t%d\t%d\t%.3f\t%s\t%s\t%s' % (
            i + 1, kind, x['t'], num(x['alive']), num(x['p3']) / num(x['alive']),
            x['depth m'], x['jnt inh'], x['shade %']))
cy += ['', '## M7  peak-to-trough amplitude over t >= 5000',
       'round\tseed\tmin alive\tat t\tmax alive\tat t\tmax/min\tbelow 2.5']
for lbl, arms in (('r37b', B), ('r37', R37), ('r36', R36)):
    for i in range(5):
        r = [x for x in data[arms[i]][1] if x['t'] >= 5000]
        mn = min(r, key=lambda x: num(x['alive']))
        mx = max(r, key=lambda x: num(x['alive']))
        ratio = num(mx['alive']) / num(mn['alive'])
        cy.append('%s\t%d\t%d\t%d\t%d\t%d\t%.2f\t%s' % (
            lbl, i + 1, num(mn['alive']), mn['t'], num(mx['alive']), mx['t'], ratio,
            'yes' if ratio < 2.5 else 'no'))
write('cycle.tsv', cy)

# ---------------------------------------------------------------- M8 pace
import datetime
def manifest(arm):
    base = os.path.join(ROOT, 'runs', arm)
    run = sorted(d for d in os.listdir(base) if os.path.isdir(os.path.join(base, d)))[0]
    return json.load(open(os.path.join(base, run, 'run.json')))

def parse(ts):
    # .NET writes seven fractional digits; fromisoformat takes at most six
    ts = ts.replace('Z', '+00:00')
    if '.' in ts:
        head, rest = ts.split('.', 1)
        frac, tz = rest[:-6], rest[-6:]
        ts = head + '.' + frac[:6] + tz
    return datetime.datetime.fromisoformat(ts)

pace = ['## M8  wall-clock seconds per 1,000 simulated seconds, from the manifests',
        'round\tseed\tstartedAt\tendedAt/stoppedAt\tstatus\twall s\tsimulated s\ts per 1000 sim s'
        '\tfooter wall minutes\tfooter timesRealTime']
P = {}
for lbl, arms in (('r37b', B), ('r37', R37)):
    for i in range(5):
        m = manifest(arms[i])
        end = m.get('endedAt') or m.get('stoppedAt')
        wall = (parse(end) - parse(m['startedAt'])).total_seconds()
        sim = m.get('simulatedSeconds')
        if sim is None:
            sim = LAST[arms[i]] if arms[i] in LAST else None
        per = wall / sim * 1000.0
        P[(lbl, i)] = per
        pace.append('%s\t%d\t%s\t%s\t%s\t%.0f\t%d\t%.1f\t%s\t%s' % (
            lbl, i + 1, m['startedAt'][:19], end[:19], m['status'], wall, sim, per,
            m.get('wallClockMinutes', '(none: stopped)'),
            m.get('timesRealTime', '(none: stopped)')))
pace += ['', '## M8  ratio, r37b over r37, same seed', 'seed\tr37b s/1000\tr37 s/1000\tratio\twithin 1.5']
for i in range(5):
    r = P[('r37b', i)] / P[('r37', i)]
    pace.append('%d\t%.1f\t%.1f\t%.3f\t%s' % (i + 1, P[('r37b', i)], P[('r37', i)], r,
                'yes' if r <= 1.5 else 'no'))
write('pace.tsv', pace)

# ---------------------------------------------------------------- eaters
ea = ['## The eaters: absorptive and inherited-absorptive standing counts',
      'seed\tt\talive\tabsorpt\tinherit\tinherit/alive\tfood %\tphoto inh']
for i in range(5):
    for t in TIMES:
        row = at(B[i], t)
        ea.append('%d\t%d\t%s\t%s\t%s\t%.3f\t%s\t%s' % (
            i + 1, row['t'], row['alive'], row['absorpt'], row['inherit'],
            num(row['inherit']) / num(row['alive']), row['food %'], row['photo inh']))
ea += ['', '## The eaters: the peak of the absorptive standing count',
       'arm\tpeak absorpt\tat t\tpeak inherit\tat t\tlast t with inherit>=10']
for i in range(5):
    r = data[B[i]][1]
    pa = max(r, key=lambda x: num(x['absorpt']))
    pi = max(r, key=lambda x: num(x['inherit']))
    last = [x['t'] for x in r if num(x['inherit']) >= 10]
    ea.append('%s\t%d\t%d\t%d\t%d\t%s' % (B[i], num(pa['absorpt']), pa['t'],
              num(pi['inherit']), pi['t'], last[-1] if last else 'never'))
ea += ['', '## The passing clade of each seed, from clade-score.ps1 (see verdicts.txt)']
write('eaters.tsv', ea)

# ---------------------------------------------------------------- fields
fl = ['## Where the detritus and the corpses are',
      'seed\tt\tdet patch sd\tpatch max share\tcorpses\tdet deep\tdetritus J\tJ/m3 here'
      '\t% on floor\trefuge J\tmat here\tshade %\tdepth m\tdepth sd']
for i in range(5):
    for t in TIMES:
        row = at(B[i], t)
        fl.append('%d\t%d\t' % (i + 1, row['t']) + '\t'.join(row[c] for c in (
            'det patch sd', 'patch max share', 'corpses', 'det deep', 'detritus J',
            'J/m3 here', '% on floor', 'refuge J', 'mat here', 'shade %', 'depth m', 'depth sd')))
fl += ['', '## The same, run-wide: mean and maximum over every sample',
       'arm\tmean det patch sd\tmax det patch sd\tat t\tmean patch max share\tmax patch max share'
       '\tmean corpses\tmax corpses\tat t\tmean det deep\tmax det deep']
for i in range(5):
    r = data[B[i]][1]
    def col(c):
        return [num(x[c]) for x in r]
    ps, pm, co, dd = col('det patch sd'), col('patch max share'), col('corpses'), col('det deep')
    fl.append('%s\t%.4f\t%.4f\t%d\t%.3f\t%.3f\t%.0f\t%d\t%d\t%.3f\t%.3f' % (
        B[i], sum(ps) / len(ps), max(ps), r[ps.index(max(ps))]['t'],
        sum(pm) / len(pm), max(pm), sum(co) / len(co), max(co), r[co.index(max(co))]['t'],
        sum(dd) / len(dd), max(dd)))
fl += ['', '## Control: round 37 at the same times',
       'seed\tt\tdet patch sd\tpatch max share\tcorpses\tdet deep']
for i in range(5):
    for t in TIMES:
        row = at(R37[i], t)
        fl.append('%d\t%d\t' % (i + 1, t) + '\t'.join(row[c] for c in (
            'det patch sd', 'patch max share', 'corpses', 'det deep')))
write('fields.tsv', fl)

# ---------------------------------------------------------------- verification
v = ['## Verification (0095 V1 to V5)', 'arm\trows\tlast t\tstatus\taudit != 0.0000%'
     '\tmat resid != 0\twraps != 0\tmat orphan max\tstillb at end\tcrowded at end']
for i in range(5):
    r = data[B[i]][1]
    m = manifest(B[i])
    v.append('%s\t%d\t%d\t%s\t%d\t%d\t%d\t%s\t%s\t%s' % (
        B[i], len(r), r[-1]['t'], m['status'],
        sum(1 for x in r if x['audit'].strip() != '0.0000%'),
        sum(1 for x in r if num(x['mat resid']) != 0),
        sum(1 for x in r if num(x['wraps']) != 0),
        max(x['mat orphan'] for x in r), r[-1]['stillb'], r[-1]['crowded']))

write('report-readings.tsv', m0 + [''] + m2 + [''] + m4 + [''] + m5 + [''] + v)
print('\n'.join(m0 + [''] + m2 + [''] + m4 + [''] + m5 + [''] + v))
