# -*- coding: utf-8 -*-
"""The fresh-seed read (logbook/0098's F1 to F8), seeds 6 to 10 on round 37b's world
and build. Everything the run report and the manifests can answer, keyed by column name.

The comparison is round 37b's seeds 1 to 5, whose numbers are taken from
logbook/specs/r37b-read/ and never recomputed (seed 5 is censored at 29,200 s).
Writes the brief's TSVs beside this file.
"""
import json, os, sys, glob, datetime

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

F = ['r37b-s%d' % i for i in (6, 7, 8, 9, 10)]
C = ['r37b-s%d' % i for i in range(1, 6)]
TIMES = [5000, 15000, 30000]

data = {}
for a in F + C:
    r, h = rows(a)
    data[a] = ({x['t']: x for x in r}, r, h)
LAST = {a: data[a][1][-1]['t'] for a in F + C}


def at(a, t):
    d, r, _ = data[a]
    return d[t] if t in d else r[-1]


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


def manifest(a):
    p = sorted(glob.glob(os.path.join(ROOT, 'runs', a, '*', 'run.json')))[-1]
    return json.load(open(p, encoding='utf-8'))


def header_line(a):
    for line in open(os.path.join(ROOT, 'runs', a + '.md'), encoding='utf-8'):
        if 'configHash' in line and 'seed' in line:
            return line.strip()
    return ''


def tok(h, key):
    """The token starting with key, from the header's ' · '-separated list."""
    for part in h.split(' · '):
        p = part.strip()
        if p.startswith(key):
            return p
    return '(absent)'


# ------------------------------------------------------------------ V1 headers
V = ['## V1  the header tokens and the manifest hashes, per fresh seed. 0098 asks for the'
     ' tokens 0095\'s V1 names with fluidAccel 1, one simHash 5e164d01, one coreHash'
     ' ad5c952a, one configHash 2430e660, physicsJobWorkers 0 and a prereg.json naming'
     " 0098's commit.",
     'arm\tseed\tspace\tcurrent\tfluidAccel\tdispersal\tdriveLimit\tlinkPhoto\taddedMass\tdt'
     '\tfield\tconfigHash\tsimHash\tcoreHash\tphysicsJobWorkers\tgitCommit\tgitDirty'
     '\tprereg file\tprereg commit\tstatus\treason\tsimulated s\tworker']
for a in F:
    h = header_line(a)
    m = manifest(a)
    pr = json.load(open(os.path.join(ROOT, 'runs', a, 'prereg.json'), encoding='utf-8'))
    space = tok(h, 'space tank')
    V.append('\t'.join(str(x) for x in [
        a, m['seed'],
        space,
        tok(h, 'current'), tok(h, 'fluidAccel'),
        'dispersal=' + h.split('dispersal=')[1].split(' ·')[0] if 'dispersal=' in h else '(absent)',
        tok(h, 'driveLimit'), tok(h, 'linkPhoto'), tok(h, 'addedMass'),
        tok(h, 'dt='), tok(h, 'field'),
        m['configHash'], m['source']['simHash'][:8], m['source']['coreHash'][:8],
        m['physicsJobWorkers'], m['source']['gitCommit'][:8], m['source']['gitDirty'],
        pr['file'], pr['commit'][:8], m['status'], m['reason'], m['simulatedSeconds'],
        os.path.basename(m['source']['workerPath'])]))

V += ['', '## V2 to V5, checked on every row of every fresh seed',
      'arm\trows\tsamples\taudit rows not 0.0000%\tmat resid rows not 0\twraps rows not 0'
      '\tmax wraps\tdiverged at end\tdumps on disk\ttrace files\tmax |mat orphan|'
      '\trows with mat orphan != 0\tstatus']
for a in F:
    _, r, _ = data[a]
    bad_a = sum(1 for x in r if num(x['audit']) != 0.0)
    bad_m = sum(1 for x in r if num(x['mat resid']) != 0.0)
    bad_w = sum(1 for x in r if num(x['wraps']) != 0.0)
    mo = max(abs(num(x['mat orphan'])) for x in r)
    monz = sum(1 for x in r if num(x['mat orphan']) != 0.0)
    rd = os.path.dirname(sorted(glob.glob(os.path.join(ROOT, 'runs', a, '*', 'run.json')))[-1])
    dd = os.path.join(rd, 'diverged')
    dumps = sorted(glob.glob(os.path.join(dd, '*'))) if os.path.isdir(dd) else []
    traces = [d for d in dumps if 'trace' in os.path.basename(d).lower()]
    m = manifest(a)
    V.append('%s\t%d\t%d\t%d\t%d\t%d\t%g\t%d\t%d\t%d\t%g\t%d\t%s' % (
        a, len(r), len(r), bad_a, bad_m, bad_w,
        max(num(x['wraps']) for x in r), m['divergedTotal'], len(dumps), len(traces), mo,
        monz, m['status'] + ' / ' + m['reason']))
write('headers.tsv', V)

# ------------------------------------------------------------------ F1
f1 = ['## F1  the world stands: alive at 30,000 s between 1,250 and 2,300 in at least 4 of 5',
      'arm\tt\talive\tin 1250-2300\tbirths\tdeaths\tmean alive over the run\tmin alive after 5000 s'
      '\tmax alive after 5000 s']
n1 = 0
for a in F:
    _, r, _ = data[a]
    v = num(at(a, 30000)['alive'])
    ok = 1250 <= v <= 2300
    n1 += ok
    post = [num(x['alive']) for x in r if x['t'] >= 5000]
    f1.append('%s\t%d\t%d\t%s\t%d\t%d\t%.0f\t%d\t%d' % (
        a, LAST[a], v, 'yes' if ok else 'NO', num(at(a, 30000)['births']),
        num(at(a, 30000)['deaths']),
        sum(num(x['alive']) for x in r) / len(r), min(post), max(post)))
f1.append('count in band\t%d of 5\t(holds at 4)' % n1)
f1.append('')
f1.append('## comparison, round 37b seeds 1 to 5 at 30,000 s (seed 5 at 29,200 s):'
          ' 1674 / 1735 / 1840 / 1782 / 1764')

# ------------------------------------------------------------------ F2 p3
p3 = ['## F2  p3 (the rim ring, a quarter of the disc by area) over alive',
      'arm\t' + '\t'.join('t=%d p3\talive\tshare' % t for t in TIMES) +
      '\tin band 15-40% at all three']
n2 = 0
for a in F:
    cells, ok = [], True
    for t in TIMES:
        x = at(a, t)
        p, al = num(x['p3']), num(x['alive'])
        s = p / al
        ok = ok and 0.15 <= s <= 0.40
        cells += ['%d' % p, '%d' % al, '%.3f' % s]
    n2 += ok
    p3.append('%s\t%s\t%s' % (a, '\t'.join(cells), 'yes' if ok else 'NO'))
p3.append('count\t%d of 5\t(holds at 4)' % n2)

p3 += ['', '## F2  all four ring counts and shares at the three times',
       'arm\tt\talive\tp0\tp1\tp2\tp3\ts0\ts1\ts2\ts3\tsum(p)-alive']
for a in F:
    for t in TIMES:
        x = at(a, t)
        al = num(x['alive'])
        ps = [num(x['p%d' % k]) for k in range(4)]
        p3.append('%s\t%d\t%d\t%s\t%s\t%d' % (
            a, t, al, '\t'.join('%d' % v for v in ps),
            '\t'.join('%.3f' % (v / al) for v in ps), sum(ps) - al))

p3 += ['', '## F2  p3 share over the whole run: the extremes and how often outside the band',
       'arm\tsamples\tmin share\tat t\tmax share\tat t\tsamples above 0.40\tsamples below 0.15'
       '\tof those, samples below 0.15 at t<=4000']
for a in F:
    _, r, _ = data[a]
    sh = [(num(x['p3']) / num(x['alive']), x['t']) for x in r if num(x['alive']) > 0]
    lo, hi = min(sh), max(sh)
    above = sum(1 for s, t in sh if s > 0.40)
    below = [(s, t) for s, t in sh if s < 0.15]
    p3.append('%s\t%d\t%.3f\t%d\t%.3f\t%d\t%d\t%d\t%d' % (
        a, len(sh), lo[0], lo[1], hi[0], hi[1], above, len(below),
        sum(1 for s, t in below if t <= 4000)))

p3 += ['', '## F2  p3 share every 1000 s', 't\t' + '\t'.join(F)]
for t in range(1000, 30001, 1000):
    p3.append('%d\t' % t + '\t'.join(
        '%.3f' % (num(at(a, t)['p3']) / num(at(a, t)['alive'])) for a in F))
p3 += ['', '## comparison, round 37b seeds 1 to 5 (logbook/specs/r37b-read/p3-share.tsv):'
       ' all fifteen readings 0.194 to 0.270, no sample above 0.40']
write('p3-share.tsv', p3)

# ------------------------------------------------------------------ F3
f3 = ['## F3  the middle is filled: corrected cols at least 60 from 5,000 s, and x sd 2.0 to'
      ' 3.4 m at the three times',
      'arm\tcols at 5000\tcols at 15000\tcols at 30000\tmin cols over t>=5000\tat t'
      '\tx sd 5000\tx sd 15000\tx sd 30000\tmin x sd t>=5000\tmax x sd t>=5000\tholds']
n3 = 0
for a in F:
    _, r, _ = data[a]
    post = [(num(x['cols']), x['t']) for x in r if x['t'] >= 5000]
    mn = min(post)
    xs = [num(x['x sd']) for x in r if x['t'] >= 5000]
    three = [num(at(a, t)['x sd']) for t in TIMES]
    ok = mn[0] >= 60 and all(2.0 <= v <= 3.4 for v in three)
    n3 += ok
    f3.append('%s\t%d\t%d\t%d\t%d\t%d\t%.2f\t%.2f\t%.2f\t%.2f\t%.2f\t%s' % (
        a, num(at(a, 5000)['cols']), num(at(a, 15000)['cols']), num(at(a, 30000)['cols']),
        mn[0], mn[1], three[0], three[1], three[2], min(xs), max(xs),
        'yes' if ok else 'NO'))
f3.append('count\t%d of 5\t(holds at 4)' % n3)
f3.append('')
f3.append('## comparison, round 37b seeds 1 to 5: minimum cols 98 of 100 in every seed,'
          ' x sd 2.66 to 2.90 (2.82 is a spread disc)')

# ------------------------------------------------------------------ F5
f5 = ['## F5  the joint\'s fate: seeds holding at least 10 inherited jointed bodies at'
      ' 30,000 s (0098 predicts between 0 and 3 of 5)',
      'arm\tjnt inh at end\t>=10\tjointed at end\tpeak jnt inh\tat t\tpeak jointed\tat t'
      '\tlast t with jnt inh >= 10\tfirst t with jnt inh = 0\tsamples with jnt inh = 0']
n5 = 0
for a in F:
    _, r, _ = data[a]
    v = num(at(a, 30000)['jnt inh'])
    ok = v >= 10
    n5 += ok
    pk = max((num(x['jnt inh']), x['t']) for x in r)
    pj = max((num(x['jointed']), x['t']) for x in r)
    ge = [x['t'] for x in r if num(x['jnt inh']) >= 10]
    zr = [x['t'] for x in r if num(x['jnt inh']) == 0]
    f5.append('%s\t%d\t%s\t%d\t%d\t%d\t%d\t%d\t%s\t%s\t%d' % (
        a, v, 'yes' if ok else 'no', num(at(a, 30000)['jointed']), pk[0], pk[1],
        pj[0], pj[1], ge[-1] if ge else '(never)', zr[0] if zr else '(never)', len(zr)))
f5.append('count holding >= 10\t%d of 5\t(0098: 0 to 3 holds; 0 or 4-5 are the two-sided'
          ' readings)' % n5)
f5.append('')
f5.append('## comparison, round 37b seeds 1 to 5: 0 / 0 / 8 / 55 / 102 at the end,'
          ' so two seeds held >= 10')

write('summary.tsv', f1 + [''] + f3 + [''] + f5)

# ------------------------------------------------------------------ F6 exposure
ex = ['## F6  divergences and the exposure that produced them. A sample is 100 s, so a'
      ' column\'s standing count summed over samples times 100 is body-seconds.',
      'round\tarm\tsamples\tinterval s\tsum jnt inh\tjnt-inh body-s\tsum jointed'
      '\tjointed body-s\tdiverged\tper 1e6 jnt-inh body-s\tper 1e6 jointed body-s'
      '\tin 0 to 10 per 1e6 jointed']
tot = [0, 0, 0]
for a in F:
    _, r, _ = data[a]
    si = sum(num(x['jnt inh']) for x in r)
    sj = sum(num(x['jointed']) for x in r)
    dv = num(r[-1]['diverged'])
    bi, bj = si * 100, sj * 100
    tot[0] += bi; tot[1] += bj; tot[2] += dv
    ex.append('fresh\t%s\t%d\t100\t%d\t%d\t%d\t%d\t%d\t%.2f\t%.2f\t%s' % (
        a, len(r), si, bi, sj, bj, dv,
        1e6 * dv / bi if bi else 0, 1e6 * dv / bj if bj else 0,
        'yes' if 0 <= (1e6 * dv / bj if bj else 0) <= 10 else 'NO'))
ex.append('fresh\tall five\t-\t-\t-\t%d\t-\t%d\t%d\t%.2f\t%.2f\t-' % (
    tot[0], tot[1], tot[2], 1e6 * tot[2] / tot[0], 1e6 * tot[2] / tot[1]))
ex += ['', '## comparison, NOT recomputed (logbook/specs/r37b-read/exposure.tsv):',
       'r37b s1-s5\t-\t-\t-\t-\t11133400\t-\t11792900\t43\t3.86\t3.65\t-',
       'r37 s1-s5\t-\t-\t-\t-\t9173400\t-\t9491200\t0\t0.00\t0.00\t-',
       'r36 s1-s5\t-\t-\t-\t-\t6913800\t-\t7367200\t131\t18.95\t17.78\t-']
write('exposure.tsv', ex)

# ------------------------------------------------------------------ cycle
cy = ['## The cycle: local extrema of alive over t >= 5000 (a sample extreme in its'
      ' +-1500 s window)',
      'arm\tkind\tt\talive\tp3 share\tdepth m\tjnt inh\tshade %']
for a in F:
    _, r, _ = data[a]
    post = [x for x in r if x['t'] >= 5000]
    for i, x in enumerate(post):
        w = [y for y in post if abs(y['t'] - x['t']) <= 1500]
        v = num(x['alive'])
        kind = None
        if v == max(num(y['alive']) for y in w):
            kind = 'peak'
        elif v == min(num(y['alive']) for y in w):
            kind = 'trough'
        if kind:
            cy.append('%s\t%s\t%d\t%d\t%.3f\t%s\t%d\t%s' % (
                a, kind, x['t'], v, num(x['p3']) / v, x['depth m'],
                num(x['jnt inh']), x['shade %']))
cy += ['', '## peak-to-trough amplitude over t >= 5000 (0097 read 1.79 to 2.35 on round 37b'
       ' seeds 1 to 5 and 1.93 to 5.44 on round 37)',
       'arm\tmin alive\tat t\tmax alive\tat t\tmax/min\tbelow 2.5']
for a in F:
    _, r, _ = data[a]
    post = [(num(x['alive']), x['t']) for x in r if x['t'] >= 5000]
    lo, hi = min(post), max(post)
    cy.append('%s\t%d\t%d\t%d\t%d\t%.2f\t%s' % (
        a, lo[0], lo[1], hi[0], hi[1], hi[0] / lo[0], 'yes' if hi[0] / lo[0] < 2.5 else 'no'))
write('cycle.tsv', cy)

# ------------------------------------------------------------------ pace
pa = ['## F8  wall-clock seconds per 1,000 simulated seconds, from the manifests',
      'round\tarm\tstartedAt\tendedAt\tstatus\twall s\tsimulated s\ts per 1000 sim s'
      '\tfooter wall minutes\tfooter timesRealTime\tworker']


def parse(s):
    return datetime.datetime.strptime(s[:19], '%Y-%m-%dT%H:%M:%S')


spans = {}
for a in F:
    m = manifest(a)
    st, en = parse(m['startedAt']), parse(m['endedAt'])
    wall = (en - st).total_seconds()
    spans[a] = (st, en)
    pa.append('fresh\t%s\t%s\t%s\t%s\t%.0f\t%d\t%.1f\t%s\t%s\t%s' % (
        a, m['startedAt'][:19], m['endedAt'][:19], m['status'], wall,
        m['simulatedSeconds'], wall / (m['simulatedSeconds'] / 1000.0),
        m['wallClockMinutes'], m['timesRealTime'],
        os.path.basename(m['source']['workerPath'])))

pa += ['', '## F8  the pace normalised as pace.tsv normalises it: divided by the mean living'
       ' population, since a step costs per body',
       'round\tarm\tmean alive\ts per 1000 sim s\ts per 1000 sim s per 1000 bodies'
       '\tratio to 37b s1-s5 range (829.7-1096.5)\tin 0.7-1.4 of every one of 37b\'s five']
R37B_NORM = [930.3, 829.7, 951.6, 864.3, 1096.5]
n8 = 0
for a in F:
    _, r, _ = data[a]
    m = manifest(a)
    wall = (parse(m['endedAt']) - parse(m['startedAt'])).total_seconds()
    per = wall / (m['simulatedSeconds'] / 1000.0)
    mean = sum(num(x['alive']) for x in r) / len(r)
    norm = per / (mean / 1000.0)
    ratios = [norm / v for v in R37B_NORM]
    ok = all(0.7 <= x <= 1.4 for x in ratios)
    n8 += ok
    pa.append('fresh\t%s\t%.0f\t%.1f\t%.1f\t%s\t%s' % (
        a, mean, per, norm, ' '.join('%.3f' % x for x in ratios), 'yes' if ok else 'NO'))
pa.append('count\t%d of 5' % n8)
pa += ['', '## comparison, NOT recomputed (logbook/specs/r37b-read/pace.tsv): round 37b seeds'
       ' 1 to 5 read 1289.4 / 1274.0 / 1460.7 / 1321.4 / 1579.3 s per 1,000 simulated s and'
       ' 930.3 / 829.7 / 951.6 / 864.3 / 1096.5 per 1,000 bodies.']

pa += ['', '## the machine: how many of the five fresh arms ran at once, over each arm\'s own'
       ' life (renders are not in this count and are named in the final message)',
       'arm\tstart\tend\thours\tmean concurrent fresh arms\tmin\tmax']
for a in F:
    st, en = spans[a]
    total = (en - st).total_seconds()
    step = 60
    vals = []
    t = st
    while t < en:
        vals.append(sum(1 for b in F if spans[b][0] <= t < spans[b][1]))
        t += datetime.timedelta(seconds=step)
    pa.append('%s\t%s\t%s\t%.2f\t%.2f\t%d\t%d' % (
        a, st.strftime('%m-%d %H:%M'), en.strftime('%m-%d %H:%M'), total / 3600.0,
        sum(vals) / len(vals), min(vals), max(vals)))
write('pace.tsv', pa)

# ------------------------------------------------------------------ eaters
ea = ['## The eaters: absorptive and inherited-absorptive standing counts',
      'arm\tt\talive\tabsorpt\tinherit\tinherit/alive\tfood %\tphoto inh']
for a in F:
    for t in TIMES:
        x = at(a, t)
        al = num(x['alive'])
        ea.append('%s\t%d\t%d\t%d\t%d\t%.3f\t%s\t%d' % (
            a, t, al, num(x['absorpt']), num(x['inherit']), num(x['inherit']) / al,
            x['food %'], num(x['photo inh'])))
ea += ['', '## The eaters: the peak of the absorptive standing count',
       'arm\tpeak absorpt\tat t\tpeak inherit\tat t\tlast t with inherit >= 10'
       '\tfirst t with inherit >= 10']
for a in F:
    _, r, _ = data[a]
    pa2 = max((num(x['absorpt']), x['t']) for x in r)
    pi = max((num(x['inherit']), x['t']) for x in r)
    ge = [x['t'] for x in r if num(x['inherit']) >= 10]
    ea.append('%s\t%d\t%d\t%d\t%d\t%s\t%s' % (
        a, pa2[0], pa2[1], pi[0], pi[1], ge[-1] if ge else '(never)',
        ge[0] if ge else '(never)'))
ea += ['', '## comparison, round 37b seeds 1 to 5 (logbook/specs/r37b-read/eaters.tsv):'
       ' inherit/alive at 30,000 s 0.045 / 0.085 / 0.016 / 0.042 / 0.045;'
       ' peak absorptive 527 / 557 / 308 / 644 / 247']
write('eaters.tsv', ea)

# ------------------------------------------------------------------ stillbirths
sb = ['## Recorded and not predicted: stillbirths against births (cumulative counts)',
      'arm\t' + '\t'.join('t=%d stillb\tbirths\tper 1000 births' % t for t in TIMES) +
      '\tend stillb\tend births\tper 1000 births\tcrowded at end\tmax crowded']
for a in F:
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
sb += ['', '## comparison, round 37b seeds 1 to 5: stillbirths 12 / 196 / 449 / 201 / 181'
       ' with crowded 0 everywhere; round 37: 2 / 14 / 5 / 183 / 56.'
       ' The control r37bc-s5 (fluidAccel 0) read 0 (0097 addendum).']
write('stillbirths.tsv', sb)

# ------------------------------------------------------------------ fields
fl = ['## Where the detritus and the corpses are',
      'arm\tt\tdet patch sd\tpatch max share\tcorpses\tdet deep\tdetritus J\tJ/m3 here'
      '\t% on floor\trefuge J\tmat here\tshade %\tdepth m\tdepth sd']
for a in F:
    for t in TIMES:
        x = at(a, t)
        fl.append('%s\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
            a, t, x['det patch sd'], x['patch max share'], x['corpses'], x['det deep'],
            x['detritus J'], x['J/m3 here'], x['% on floor'], x['refuge J'], x['mat here'],
            x['shade %'], x['depth m'], x['depth sd']))
fl += ['', '## The same, run-wide: mean and maximum over every sample',
       'arm\tmean det patch sd\tmax det patch sd\tat t\tmean patch max share'
       '\tmax patch max share\tmean corpses\tmax corpses\tat t\tmean det deep\tmax det deep']
for a in F:
    _, r, _ = data[a]
    ds = [num(x['det patch sd']) for x in r]
    ms = [num(x['patch max share']) for x in r]
    cs = [(num(x['corpses']), x['t']) for x in r]
    dd = [num(x['det deep']) for x in r]
    mx = max((v, x['t']) for v, x in zip(ds, r))
    fl.append('%s\t%.4f\t%.4f\t%d\t%.3f\t%.3f\t%.0f\t%d\t%d\t%.3f\t%.3f' % (
        a, sum(ds) / len(ds), mx[0], mx[1], sum(ms) / len(ms), max(ms),
        sum(v for v, _ in cs) / len(cs), max(cs)[0], max(cs)[1],
        sum(dd) / len(dd), max(dd)))
fl += ['', '## comparison, round 37b seeds 1 to 5: mean det patch sd 0.0340 to 0.0577,'
       ' mean patch max share 0.281 to 0.286 (0.25 is even), max 0.397 to 0.463;'
       ' round 37 read det patch sd 0.08 to 1.31 and patch max share 0.45 to 0.98.']
write('fields.tsv', fl)

print('wrote headers.tsv, summary.tsv, p3-share.tsv, exposure.tsv, cycle.tsv, pace.tsv,'
      ' eaters.tsv, stillbirths.tsv, fields.tsv')
