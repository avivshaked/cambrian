# -*- coding: utf-8 -*-
"""The round 38 read (logbook/0100's V1 to V5 and D1 to D8), seeds 1 to 5 on the dilute
tank (400 m2, 6,000 units of matter, streams at 0.1 m/s with the acceleration force).

Everything the run report, stats.jsonl and the manifests can answer, keyed by column NAME.
The comparison is round 37b's ten seeds, whose numbers are taken from
logbook/specs/r37b-read/ and logbook/specs/r37b-fresh-read/ and never recomputed.
Writes the brief's TSVs beside this file.
"""
import json, os, sys, glob, datetime

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

A = ['r38-s%d' % i for i in range(1, 6)]
TIMES = [5000, 15000, 30000]
LIVE_COLS = 402

data = {}
for a in A:
    r, h = rows(a)
    data[a] = ({x['t']: x for x in r}, r, h)
LAST = {a: data[a][1][-1]['t'] for a in A}


def at(a, t):
    d, r, _ = data[a]
    return d[t] if t in d else r[-1]


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


def run_dir(a):
    return os.path.dirname(sorted(glob.glob(os.path.join(ROOT, 'runs', a, '*', 'run.json')))[-1])


def manifest(a):
    return json.load(open(os.path.join(run_dir(a), 'run.json'), encoding='utf-8'))


def stats(a):
    out = []
    with open(os.path.join(run_dir(a), 'stats.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line.startswith('{'):
                out.append(json.loads(line))
    return out


def header_line(a):
    for line in open(os.path.join(ROOT, 'runs', a + '.md'), encoding='utf-8'):
        if 'configHash' in line and 'seed' in line:
            return line.strip()
    return ''


def tok(h, key):
    for part in h.split(' · '):
        p = part.strip()
        if p.startswith(key):
            return p
    return '(absent)'


ST = {a: stats(a) for a in A}

# ------------------------------------------------------------------ V1 to V5
V = ['## V1  the header tokens and the manifest hashes, per seed. 0100 asks for'
     ' "space tank r=11.28 m (400 m2), depth 60, wall, bed", area 400 m2, matterBudget 6000,'
     ' fluidAccel 1, current 0.1 m/s transport, dispersal=5 m, driveLimit >0.01,'
     ' linkPhoto 0.5, addedMass 0.5, dt=0.01, one simHash ecc41ec5, one coreHash 16073c69,'
     ' one configHash 30637fb0, physicsJobWorkers 0 and a prereg.json at 4cab313.',
     'arm\tseed\tspace\tarea\tmatterBudget\tcurrent\tfluidAccel\tdispersal\tdriveLimit'
     '\tlinkPhoto\taddedMass\tdt\tfield\tconfigHash\tsimHash\tcoreHash\tphysicsJobWorkers'
     '\tgitCommit\tgitDirty\tprereg file\tprereg commit\tstatus\treason\tsimulated s\tworker']
for a in A:
    h = header_line(a)
    m = manifest(a)
    pr = json.load(open(os.path.join(ROOT, 'runs', a, 'prereg.json'), encoding='utf-8'))
    V.append('\t'.join(str(x) for x in [
        a, m['seed'], tok(h, 'space tank'), tok(h, 'area'), tok(h, 'matterBudget'),
        tok(h, 'current'), tok(h, 'fluidAccel'),
        'dispersal=' + h.split('dispersal=')[1].split(' ·')[0] if 'dispersal=' in h else '(absent)',
        tok(h, 'driveLimit'), tok(h, 'linkPhoto'), tok(h, 'addedMass'), tok(h, 'dt='),
        tok(h, 'field'), m['configHash'], m['source']['simHash'][:8],
        m['source']['coreHash'][:8], m['physicsJobWorkers'], m['source']['gitCommit'][:8],
        m['source']['gitDirty'], pr['file'], pr['commit'][:8], m['status'], m['reason'],
        m['simulatedSeconds'], os.path.basename(m['source']['workerPath'])]))

V += ['', '## V2 to V5, checked on every row of every seed',
      'arm\trows\taudit rows not 0.0000%\tmat resid rows not 0\twraps rows not 0\tmax wraps'
      '\tdiverged at end\tdumps on disk\ttrace files\tmax |mat orphan|'
      '\trows with mat orphan != 0\tstatus']
for a in A:
    _, r, _ = data[a]
    m = manifest(a)
    dd = os.path.join(run_dir(a), 'diverged')
    dumps = sorted(glob.glob(os.path.join(dd, '*.json'))) if os.path.isdir(dd) else []
    traces = [d for d in dumps if 'trace' in os.path.basename(d).lower()]
    V.append('%s\t%d\t%d\t%d\t%d\t%g\t%d\t%d\t%d\t%g\t%d\t%s' % (
        a, len(r),
        sum(1 for x in r if num(x['audit']) != 0.0),
        sum(1 for x in r if num(x['mat resid']) != 0.0),
        sum(1 for x in r if num(x['wraps']) != 0.0),
        max(num(x['wraps']) for x in r), m['divergedTotal'],
        len(dumps) - len(traces), len(traces),
        max(abs(num(x['mat orphan'])) for x in r),
        sum(1 for x in r if num(x['mat orphan']) != 0.0),
        m['status'] + ' / ' + m['reason']))
write('headers.tsv', V)

# ------------------------------------------------------------------ D1
d1 = ['## D1  founding survives the dilution: births by 1,000 s at least 40 in EVERY seed,'
      ' and alive at 5,000 s at least 300 in 4 of 5',
      'arm\tbirths by 1000 s\t>=40\talive at 5000\t>=300\tbirths by 3000 s\tmat blk by 3000 s'
      '\tmat blk per birth\tmat short by 3000 s\tstillb by 3000 s\tfloor at 3000 s'
      '\tgen max at 3000 s']
n1a = n1b = 0
for a in A:
    b1 = num(at(a, 1000)['births'])
    al = num(at(a, 5000)['alive'])
    n1a += b1 >= 40
    n1b += al >= 300
    x3 = at(a, 3000)
    b3, mb = num(x3['births']), num(x3['mat blk'])
    d1.append('%s\t%d\t%s\t%d\t%s\t%d\t%d\t%.1f\t%d\t%d\t%d\t%d' % (
        a, b1, 'yes' if b1 >= 40 else 'NO', al, 'yes' if al >= 300 else 'NO',
        b3, mb, mb / b3 if b3 else 0, num(x3['mat short']), num(x3['stillb']),
        num(x3['floor']), num(x3['gen max'])))
d1.append('count\tbirths>=40: %d of 5 (needs 5)\t\talive>=300: %d of 5 (needs 4)' % (n1a, n1b))
d1 += ['', '## comparison: round 37b (0100) read births by 1,100 s about 120 with mat blk'
       ' near a hundred attempts per birth; the r38 smoke read 166 births by 600 s.',
       '## births every 500 s over the first 3,000 s', 't\t' + '\t'.join(A)]
for t in range(500, 3001, 500):
    d1.append('%d\t' % t + '\t'.join('%d' % num(at(a, t)['births']) for a in A))

# ------------------------------------------------------------------ D2
d2 = ['## D2  the world stands: alive at 30,000 s between 700 and 2,300 in 4 of 5',
      'arm\tt\talive\tin 700-2300\tbirths\tdeaths\tmean alive over the run'
      '\tmin alive after 5000 s\tat t\tmax alive after 5000 s\tat t']
n2 = 0
for a in A:
    _, r, _ = data[a]
    v = num(at(a, 30000)['alive'])
    ok = 700 <= v <= 2300
    n2 += ok
    post = [(num(x['alive']), x['t']) for x in r if x['t'] >= 5000]
    lo, hi = min(post), max(post)
    d2.append('%s\t%d\t%d\t%s\t%d\t%d\t%.0f\t%d\t%d\t%d\t%d' % (
        a, LAST[a], v, 'yes' if ok else 'NO', num(at(a, 30000)['births']),
        num(at(a, 30000)['deaths']), sum(num(x['alive']) for x in r) / len(r),
        lo[0], lo[1], hi[0], hi[1]))
d2.append('count in band\t%d of 5\t(holds at 4)' % n2)
d2 += ['', '## comparison, round 37b at 30,000 s: seeds 1 to 5 1674 / 1735 / 1840 / 1782 /'
       ' 1764 (seed 5 at 29,200 s); seeds 6 to 10 1811 / 1585 / 1831 / 1722 / 1759']
write('summary.tsv', d1 + [''] + d2)

# ------------------------------------------------------------------ D4 p3
p3 = ['## D4  p3 (the rim ring, a quarter of the disc by area) over alive, 15 to 40% at the'
      ' three times in 4 of 5',
      'arm\t' + '\t'.join('t=%d p3\talive\tshare' % t for t in TIMES) +
      '\tin band 15-40% at all three']
n4 = 0
for a in A:
    cells, ok = [], True
    for t in TIMES:
        x = at(a, t)
        p, al = num(x['p3']), num(x['alive'])
        s = p / al
        ok = ok and 0.15 <= s <= 0.40
        cells += ['%d' % p, '%d' % al, '%.3f' % s]
    n4 += ok
    p3.append('%s\t%s\t%s' % (a, '\t'.join(cells), 'yes' if ok else 'NO'))
p3.append('count\t%d of 5\t(holds at 4)' % n4)

p3 += ['', '## D4  all four ring counts and shares at the three times',
       'arm\tt\talive\tp0\tp1\tp2\tp3\ts0\ts1\ts2\ts3\tsum(p)-alive']
for a in A:
    for t in TIMES:
        x = at(a, t)
        al = num(x['alive'])
        ps = [num(x['p%d' % k]) for k in range(4)]
        p3.append('%s\t%d\t%d\t%s\t%s\t%d' % (
            a, t, al, '\t'.join('%d' % v for v in ps),
            '\t'.join('%.3f' % (v / al) for v in ps), sum(ps) - al))

p3 += ['', '## D4  p3 share over the whole run: the extremes and how often outside the band',
       'arm\tsamples\tmin share\tat t\tmax share\tat t\tsamples above 0.40'
       '\tsamples below 0.15\tof those, samples below 0.15 at t<=4000']
for a in A:
    _, r, _ = data[a]
    sh = [(num(x['p3']) / num(x['alive']), x['t']) for x in r if num(x['alive']) > 0]
    lo, hi = min(sh), max(sh)
    below = [(s, t) for s, t in sh if s < 0.15]
    p3.append('%s\t%d\t%.3f\t%d\t%.3f\t%d\t%d\t%d\t%d' % (
        a, len(sh), lo[0], lo[1], hi[0], hi[1],
        sum(1 for s, t in sh if s > 0.40), len(below),
        sum(1 for s, t in below if t <= 4000)))

p3 += ['', '## D4  p3 share every 1000 s', 't\t' + '\t'.join(A)]
for t in range(1000, 30001, 1000):
    p3.append('%d\t' % t + '\t'.join(
        '%.3f' % (num(at(a, t)['p3']) / num(at(a, t)['alive'])) for a in A))
p3 += ['', '## comparison: round 37b seeds 1 to 5 all fifteen readings 0.194 to 0.270;'
       ' seeds 6 to 10 all fifteen 0.17 to 0.24; none of the ten above 0.40 at any sample']
write('p3-share.tsv', p3)

# ------------------------------------------------------------------ D4 cols / x sd
co = ['## D4  the disc stays filled: cols at least 60% of the 402 live columns (241.2) from'
      ' 5,000 s on, at EVERY 1,000 s row; x sd between 4.0 and 6.8 m at the three times'
      ' (a spread disc of radius 11.28 m reads 5.64)',
      'arm\tcols 5000\tcols 15000\tcols 30000\tmin cols t>=5000\tat t\tmin cols share'
      '\trows t>=5000 below 241\tx sd 5000\tx sd 15000\tx sd 30000\tmin x sd t>=5000'
      '\tmax x sd t>=5000\tholds']
n4b = 0
for a in A:
    _, r, _ = data[a]
    post = [(num(x['cols']), x['t']) for x in r if x['t'] >= 5000]
    mn = min(post)
    xs = [num(x['x sd']) for x in r if x['t'] >= 5000]
    three = [num(at(a, t)['x sd']) for t in TIMES]
    ok = mn[0] >= 0.60 * LIVE_COLS and all(4.0 <= v <= 6.8 for v in three)
    n4b += ok
    co.append('%s\t%d\t%d\t%d\t%d\t%d\t%.3f\t%d\t%.2f\t%.2f\t%.2f\t%.2f\t%.2f\t%s' % (
        a, num(at(a, 5000)['cols']), num(at(a, 15000)['cols']), num(at(a, 30000)['cols']),
        mn[0], mn[1], mn[0] / LIVE_COLS,
        sum(1 for v, t in post if v < 0.60 * LIVE_COLS),
        three[0], three[1], three[2], min(xs), max(xs), 'yes' if ok else 'NO'))
co.append('count\t%d of 5\t(holds at 4)' % n4b)
co += ['', '## cols and cols abs every 1,000 s (of 402 live columns)',
       't\t' + '\t'.join('%s cols\t%s cols abs' % (a, a) for a in A)]
for t in range(1000, 30001, 1000):
    co.append('%d\t' % t + '\t'.join('%d\t%d' % (num(at(a, t)['cols']), num(at(a, t)['cols abs']))
                                     for a in A))
co += ['', '## comparison: round 37b (100 m2, 100 live columns) read 100 of 100 at the three'
       ' times in every one of the ten seeds and never below 97 after 5,000 s; x sd 2.64 to'
       ' 2.94 m on a disc that reads 2.82 spread']
write('cols.tsv', co)

# ------------------------------------------------------------------ D5 the eaters
ec = ['## D5  the eaters course, every 1,000 s: absorpt, inherit, inherit/alive, and the'
      ' leaves beside them',
      't\t' + '\t'.join('%s absorpt\t%s inherit\t%s inh/alive' % (a, a, a) for a in A)]
for t in range(1000, 30001, 1000):
    cells = []
    for a in A:
        x = at(a, t)
        cells += ['%d' % num(x['absorpt']), '%d' % num(x['inherit']),
                  '%.3f' % (num(x['inherit']) / num(x['alive']))]
    ec.append('%d\t' % t + '\t'.join(cells))
ec += ['', '## the same at the three anchors with food %, photo inh and shade %',
       'arm\tt\talive\tabsorpt\tinherit\tinherit/alive\tfood %\tphoto inh\tshade %']
for a in A:
    for t in TIMES:
        x = at(a, t)
        al = num(x['alive'])
        ec.append('%s\t%d\t%d\t%d\t%d\t%.3f\t%s\t%d\t%s' % (
            a, t, al, num(x['absorpt']), num(x['inherit']),
            num(x['inherit']) / al, x['food %'], num(x['photo inh']), x['shade %']))
write('eaters-course.tsv', ec)

# the booms: peak, trough after the peak, end, second boom
bo = ['## D5  the eaters booms, from the 100 s samples of the report',
      'arm\tpeak absorpt\tat t\tpeak inherit\tat t\tpeak inh/alive\ttrough inherit after peak'
      '\tat t\tinherit at end\tabsorpt at end\tsecond boom\tsecond peak inherit\tat t'
      '\tfirst t inherit>=10\tlast t inherit>=10\tsamples with inherit=0 after the peak']
booms = {}
for a in A:
    _, r, _ = data[a]
    inh = [(x['t'], num(x['inherit']), num(x['absorpt']), num(x['alive'])) for x in r]
    pk = max(inh, key=lambda z: z[1])
    after = [z for z in inh if z[0] > pk[0]]
    tr = min(after, key=lambda z: z[1]) if after else pk
    post = [z for z in after if z[0] > tr[0]]
    sp = max(post, key=lambda z: z[1]) if post else None
    second = sp is not None and sp[1] >= max(10, 0.1 * pk[1])
    ge = [z[0] for z in inh if z[1] >= 10]
    booms[a] = (pk, tr, sp)
    bo.append('%s\t%d\t%d\t%d\t%d\t%.3f\t%d\t%d\t%d\t%d\t%s\t%s\t%s\t%s\t%s\t%d' % (
        a, max(z[2] for z in inh), max(inh, key=lambda z: z[2])[0], pk[1], pk[0],
        pk[1] / pk[3], tr[1], tr[0], inh[-1][1], inh[-1][2],
        'yes' if second else 'no',
        '%d' % sp[1] if sp else '-', '%d' % sp[0] if sp else '-',
        '%s' % (ge[0] if ge else '(never)'), '%s' % (ge[-1] if ge else '(never)'),
        sum(1 for z in after if z[1] == 0)))
bo += ['', '## comparison, round 37b: peak absorptive 527 / 557 / 308 / 644 / 247 (seeds 1-5)'
       ' and 708 / 341 / 972 / 535 / 331 (seeds 6-10); inherit at the end at least 10 in ten'
       ' of ten, inherit/alive 0.016 to 0.085 (s1-5) and 0.032 to 0.072 (s6-10)']
write('eaters-booms.tsv', bo)

# ------------------------------------------------------------------ D6 the joint
jt = ['## D6  the joint: seeds holding at least 10 inherited jointed bodies at 30,000 s'
      ' (0100 predicts 0 to 2 of 5; 37b\'s rate is three of ten)',
      'arm\tjnt inh at end\t>=10\tjointed at end\tpeak jnt inh\tat t\tpeak jointed\tat t'
      '\tlast t with jnt inh >= 10\tfirst t with jnt inh = 0\tsamples with jnt inh = 0']
n6 = 0
for a in A:
    _, r, _ = data[a]
    v = num(at(a, 30000)['jnt inh'])
    n6 += v >= 10
    pk = max((num(x['jnt inh']), x['t']) for x in r)
    pj = max((num(x['jointed']), x['t']) for x in r)
    ge = [x['t'] for x in r if num(x['jnt inh']) >= 10]
    zr = [x['t'] for x in r if num(x['jnt inh']) == 0]
    jt.append('%s\t%d\t%s\t%d\t%d\t%d\t%d\t%d\t%s\t%s\t%d' % (
        a, v, 'yes' if v >= 10 else 'no', num(at(a, 30000)['jointed']), pk[0], pk[1],
        pj[0], pj[1], ge[-1] if ge else '(never)', zr[0] if zr else '(never)', len(zr)))
jt.append('count holding >= 10\t%d of 5\t(0100: 0 to 2 holds; 3 or more is the two-sided'
          ' reading)' % n6)
jt += ['', '## jnt inh at every 5,000 s', 't\t' + '\t'.join(A)]
for t in range(5000, 30001, 5000):
    jt.append('%d\t' % t + '\t'.join('%d' % num(at(a, t)['jnt inh']) for a in A))
jt += ['', '## jnt inh at every 1,000 s', 't\t' + '\t'.join(A)]
for t in range(1000, 30001, 1000):
    jt.append('%d\t' % t + '\t'.join('%d' % num(at(a, t)['jnt inh']) for a in A))
jt += ['', '## comparison, round 37b: seeds 1-5 held 0 / 0 / 8 / 55 / 102 at the end (two'
       ' seeds >= 10) and seeds 6-10 14 / 0 / 1 / 0 / 0 (one seed), so three of ten; every'
       ' fresh seed peaked before 3,000 s (174, 110, 77, 121, 29)']
write('joint.tsv', jt)

# ------------------------------------------------------------------ D7 exposure
ex = ['## D7  divergences and the exposure that produced them. A sample is 100 s, so a'
      ' column\'s standing count summed over samples times 100 is body-seconds.',
      'round\tarm\tsamples\tinterval s\tsum jnt inh\tjnt-inh body-s\tsum jointed'
      '\tjointed body-s\tdiverged\tper 1e6 jnt-inh body-s\tper 1e6 jointed body-s'
      '\tin 0 to 10 per 1e6 jointed']
tot = [0, 0, 0]
n7 = 0
for a in A:
    _, r, _ = data[a]
    si = sum(num(x['jnt inh']) for x in r)
    sj = sum(num(x['jointed']) for x in r)
    dv = num(r[-1]['diverged'])
    bi, bj = si * 100, sj * 100
    tot[0] += bi; tot[1] += bj; tot[2] += dv
    rate = 1e6 * dv / bj if bj else 0
    n7 += 0 <= rate <= 10
    ex.append('r38\t%s\t%d\t100\t%d\t%d\t%d\t%d\t%d\t%.2f\t%.2f\t%s' % (
        a, len(r), si, bi, sj, bj, dv, 1e6 * dv / bi if bi else 0, rate,
        'yes' if 0 <= rate <= 10 else 'NO'))
ex.append('r38\tall five\t-\t-\t-\t%d\t-\t%d\t%d\t%.2f\t%.2f\t%d of 5 in band' % (
    tot[0], tot[1], tot[2], 1e6 * tot[2] / tot[0], 1e6 * tot[2] / tot[1], n7))
ex += ['', '## comparison, NOT recomputed:',
       'r37b s1-s5\t-\t-\t-\t-\t11133400\t-\t11792900\t43\t3.86\t3.65\t-',
       'r37b s6-s10\t-\t-\t-\t-\t3508300\t-\t3791300\t0\t0.00\t0.00\t-',
       'r37 s1-s5\t-\t-\t-\t-\t9173400\t-\t9491200\t0\t0.00\t0.00\t-',
       'r36 s1-s5\t-\t-\t-\t-\t6913800\t-\t7367200\t131\t18.95\t17.78\t-']
write('exposure.tsv', ex)

# ------------------------------------------------------------------ the cycle
cy = ['## The cycle: local extrema of alive over t >= 5000 (a sample extreme in its'
      ' +-1500 s window)',
      'arm\tkind\tt\talive\tp3 share\tdepth m\tinherit\tjnt inh\tshade %\tdet cv']
for a in A:
    _, r, _ = data[a]
    post = [x for x in r if x['t'] >= 5000]
    for x in post:
        w = [y for y in post if abs(y['t'] - x['t']) <= 1500]
        v = num(x['alive'])
        kind = None
        if v == max(num(y['alive']) for y in w):
            kind = 'peak'
        elif v == min(num(y['alive']) for y in w):
            kind = 'trough'
        if kind:
            cy.append('%s\t%s\t%d\t%d\t%.3f\t%s\t%d\t%d\t%s\t%s' % (
                a, kind, x['t'], v, num(x['p3']) / v, x['depth m'], num(x['inherit']),
                num(x['jnt inh']), x['shade %'], x['det cv']))
cy += ['', '## peak-to-trough amplitude over t >= 5000 (37b s1-s5 read 1.79 to 2.35;'
       ' s6-s10 1.12 to 1.47)',
       'arm\tmin alive\tat t\tmax alive\tat t\tmax/min\tin 1.1-2.4']
for a in A:
    _, r, _ = data[a]
    post = [(num(x['alive']), x['t']) for x in r if x['t'] >= 5000]
    lo, hi = min(post), max(post)
    cy.append('%s\t%d\t%d\t%d\t%d\t%.2f\t%s' % (
        a, lo[0], lo[1], hi[0], hi[1], hi[0] / lo[0],
        'yes' if 1.1 <= hi[0] / lo[0] <= 2.4 else 'NO'))
write('cycle.tsv', cy)

# ------------------------------------------------------------------ D8 pace
def parse(s):
    return datetime.datetime.strptime(s[:19], '%Y-%m-%dT%H:%M:%S')


pa = ['## D8  wall-clock seconds per 1,000 simulated seconds, from the manifests',
      'round\tarm\tstartedAt (UTC)\tendedAt (UTC)\tstatus\twall s\tsimulated s'
      '\ts per 1000 sim s\tfooter wall minutes\tfooter timesRealTime\tworker']
spans = {}
for a in A:
    m = manifest(a)
    st, en = parse(m['startedAt']), parse(m['endedAt'])
    spans[a] = (st, en)
    wall = (en - st).total_seconds()
    pa.append('r38\t%s\t%s\t%s\t%s\t%.0f\t%d\t%.1f\t%s\t%s\t%s' % (
        a, m['startedAt'][:19], m['endedAt'][:19], m['status'], wall,
        m['simulatedSeconds'], wall / (m['simulatedSeconds'] / 1000.0),
        m['wallClockMinutes'], m['timesRealTime'],
        os.path.basename(m['source']['workerPath'])))

R37B10 = [930.3, 829.7, 951.6, 864.3, 1096.5, 829.5, 744.2, 770.9, 821.1, 1013.0]
MEAN10 = sum(R37B10) / 10.0
pa += ['', '## D8  normalised by the mean living population, since a step costs per body.'
       ' The ten-seed mean of the same statistic is %.1f s per 1,000 sim s per 1,000 bodies'
       ' (37b s1-s5 930.3 / 829.7 / 951.6 / 864.3 / 1096.5 from r37b-read/pace.tsv;'
       ' s6-s10 829.5 / 744.2 / 770.9 / 821.1 / 1013.0 from r37b-fresh-read/pace.tsv).' % MEAN10,
       'round\tarm\tmean alive\ts per 1000 sim s\ts per 1000 sim s per 1000 bodies'
       '\tratio to the ten-seed mean\tin 0.7-1.6']
n8 = 0
for a in A:
    _, r, _ = data[a]
    m = manifest(a)
    wall = (parse(m['endedAt']) - parse(m['startedAt'])).total_seconds()
    per = wall / (m['simulatedSeconds'] / 1000.0)
    mean = sum(num(x['alive']) for x in r) / len(r)
    norm = per / (mean / 1000.0)
    ratio = norm / MEAN10
    ok = 0.7 <= ratio <= 1.6
    n8 += ok
    pa.append('r38\t%s\t%.0f\t%.1f\t%.1f\t%.3f\t%s' % (
        a, mean, per, norm, ratio, 'yes' if ok else 'NO'))
pa.append('count\t%d of 5' % n8)

pa += ['', '## the machine: how many r38 arms ran at once over each arm\'s own life (UTC'
       ' manifest windows; renders are counted separately in the final message)',
       'arm\tstart\tend\thours\tmean concurrent r38 arms\tmin\tmax']
for a in A:
    st, en = spans[a]
    vals, t = [], st
    while t < en:
        vals.append(sum(1 for b in A if spans[b][0] <= t < spans[b][1]))
        t += datetime.timedelta(seconds=60)
    pa.append('%s\t%s\t%s\t%.2f\t%.2f\t%d\t%d' % (
        a, st.strftime('%m-%d %H:%M'), en.strftime('%m-%d %H:%M'),
        (en - st).total_seconds() / 3600.0, sum(vals) / len(vals), min(vals), max(vals)))
write('pace.tsv', pa)

# ------------------------------------------------------------------ stillbirths
sb = ['## Recorded and not predicted: stillbirths against births (cumulative counts)',
      'arm\t' + '\t'.join('t=%d stillb\tbirths\tper 1000 births' % t for t in TIMES) +
      '\tend stillb\tend births\tper 1000 births\tcrowded at end\tmax crowded']
for a in A:
    cells = []
    for t in TIMES:
        x = at(a, t)
        s, b = num(x['stillb']), num(x['births'])
        cells += ['%d' % s, '%d' % b, '%.2f' % (1000.0 * s / b) if b else '-']
    _, r, _ = data[a]
    xe = r[-1]
    se, be = num(xe['stillb']), num(xe['births'])
    sb.append('%s\t%s\t%d\t%d\t%.2f\t%d\t%d' % (
        a, '\t'.join(cells), se, be, 1000.0 * se / be,
        num(xe['crowded']), max(num(x['crowded']) for x in r)))
sb += ['', '## comparison: round 37b seeds 1 to 5 stillbirths 12 / 196 / 449 / 201 / 181;'
       ' seeds 6 to 10 439 / 0 / 130 / 104 / 709; crowded 0 in all ten']
write('stillbirths.tsv', sb)

# ------------------------------------------------------------------ fields
fl = ['## Where the detritus, the matter and the corpses are (report columns plus'
      ' matterStanding from stats.jsonl)',
      'arm\tt\tdet cv\tmat cv\tdetritus J\tmatterStanding\tcorpseJoules\tcorpseMatter'
      '\tcorpses\tdet deep\tJ/m3 here\t% on floor\trefuge J\tmat here\tdet patch sd'
      '\tpatch max share\tshade %\tdepth m\tdepth sd\tfood %']
SD = {a: {int(round(s['t'])): s for s in ST[a]} for a in A}
for a in A:
    for t in TIMES:
        x = at(a, t)
        s = SD[a].get(t, ST[a][-1])
        fl.append('%s\t%d\t%s\t%s\t%s\t%.1f\t%.1f\t%.3f\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s'
                  '\t%s\t%s\t%s\t%s' % (
            a, t, x['det cv'], x['mat cv'], x['detritus J'], s['matterStanding'],
            s['corpseJoules'], s['corpseMatter'], x['corpses'], x['det deep'],
            x['J/m3 here'], x['% on floor'], x['refuge J'], x['mat here'],
            x['det patch sd'], x['patch max share'], x['shade %'], x['depth m'],
            x['depth sd'], x['food %']))
fl += ['', '## run-wide, mean and extremes over every sample',
       'arm\tmean det patch sd\tmax det patch sd\tat t\tmean patch max share'
       '\tmax patch max share\tmean corpses\tmax corpses\tat t\tmean det deep\tmax det deep'
       '\tmin detritus J\tat t\tmax detritus J\tat t\tmin matterStanding\tmax matterStanding']
for a in A:
    _, r, _ = data[a]
    ds = [num(x['det patch sd']) for x in r]
    ms = [num(x['patch max share']) for x in r]
    cs = [(num(x['corpses']), x['t']) for x in r]
    dd = [num(x['det deep']) for x in r]
    dj = [(num(x['detritus J']), x['t']) for x in r]
    mst = [s['matterStanding'] for s in ST[a]]
    mx = max((v, x['t']) for v, x in zip(ds, r))
    fl.append('%s\t%.4f\t%.4f\t%d\t%.3f\t%.3f\t%.0f\t%d\t%d\t%.3f\t%.3f\t%.1f\t%d\t%.1f\t%d'
              '\t%.1f\t%.1f' % (
        a, sum(ds) / len(ds), mx[0], mx[1], sum(ms) / len(ms), max(ms),
        sum(v for v, _ in cs) / len(cs), max(cs)[0], max(cs)[1],
        sum(dd) / len(dd), max(dd), min(dj)[0], min(dj)[1], max(dj)[0], max(dj)[1],
        min(mst), max(mst)))
fl += ['', '## comparison, round 37b: mean det patch sd 0.0340 to 0.0577 (s1-5) and 0.0356'
       ' to 0.0490 (s6-10); mean patch max share 0.278 to 0.286 (0.25 is even)']
write('fields.tsv', fl)

# ------------------------------------------------------------------ field cv
fc = ['## Recorded and not predicted: det cv and mat cv, the fields\' coefficient of'
      ' variation, the first reading on any round. Every 1,000 s.',
      't\t' + '\t'.join('%s det cv\t%s mat cv' % (a, a) for a in A)]
for t in range(1000, 30001, 1000):
    fc.append('%d\t' % t + '\t'.join('%s\t%s' % (at(a, t)['det cv'], at(a, t)['mat cv'])
                                     for a in A))
fc += ['', '## the range after 5,000 s and the value at the end',
       'arm\tdet cv min t>=5000\tat t\tdet cv max t>=5000\tat t\tdet cv at end'
       '\tdet cv mean t>=5000\tsamples det cv > 0.5 after 5000'
       '\tmat cv min t>=5000\tat t\tmat cv max t>=5000\tat t\tmat cv at end'
       '\tmat cv mean t>=5000\tsamples mat cv > 0.5 after 5000\teither above 0.5 after 5000']
for a in A:
    _, r, _ = data[a]
    post = [x for x in r if x['t'] >= 5000]
    d = [(num(x['det cv']), x['t']) for x in post]
    m = [(num(x['mat cv']), x['t']) for x in post]
    nd = sum(1 for v, _ in d if v > 0.5)
    nm = sum(1 for v, _ in m if v > 0.5)
    fc.append('%s\t%.3f\t%d\t%.3f\t%d\t%s\t%.3f\t%d\t%.3f\t%d\t%.3f\t%d\t%s\t%.3f\t%d\t%s' % (
        a, min(d)[0], min(d)[1], max(d)[0], max(d)[1], r[-1]['det cv'],
        sum(v for v, _ in d) / len(d), nd,
        min(m)[0], min(m)[1], max(m)[0], max(m)[1], r[-1]['mat cv'],
        sum(v for v, _ in m) / len(m), nm, 'yes' if (nd or nm) else 'no'))
fc += ['', '## before 5,000 s: the first sample, and the maximum',
       'arm\tdet cv at 100\tdet cv max t<5000\tat t\tmat cv at 100\tmat cv max t<5000\tat t']
for a in A:
    _, r, _ = data[a]
    pre = [x for x in r if x['t'] < 5000]
    dm = max((num(x['det cv']), x['t']) for x in pre)
    mm = max((num(x['mat cv']), x['t']) for x in pre)
    fc.append('%s\t%s\t%.3f\t%d\t%s\t%.3f\t%d' % (
        a, r[0]['det cv'], dm[0], dm[1], r[0]['mat cv'], mm[0], mm[1]))
fc += ['', '## no comparison exists: round 38 is the first round whose report carries these'
       ' columns. The r38 smoke (600 s, dt 0.02, seed 3) read det cv 2.4 falling to 1.2 and'
       ' mat cv 0.17 rising to 0.47 (0100 launch section).']
write('fieldcv.tsv', fc)

print('wrote headers.tsv, summary.tsv, p3-share.tsv, cols.tsv, eaters-course.tsv,'
      ' eaters-booms.tsv, joint.tsv, exposure.tsv, cycle.tsv, pace.tsv, stillbirths.tsv,'
      ' fields.tsv, fieldcv.tsv')
