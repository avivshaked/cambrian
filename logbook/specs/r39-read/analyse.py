# -*- coding: utf-8 -*-
"""The round 39 read (logbook/0102's V1 to V5 and E1 to E11), five seeds on the shaped bed
at D093's size (2,200 m2, 45 m deep, a 30 m tilt, 11,000 units of matter).

Everything the run report, stats.jsonl and the manifests can answer, keyed by column NAME.
The baseline is round 38's five seeds (0101), whose numbers are quoted from
logbook/specs/r38-read/ and never recomputed.  The map-based readings (E4, E9, E10, E11 and
E3's disc share) are pos_analyse.py's, beside this file.

Seeds 3, 4 and 5 have two run directories each: an earlier censored run and the
2026-09-17 rerun that replayed it and reached the budget.  RUNS names the one used, so
nothing here depends on a script's own choice between them.
"""
import json, os, sys, glob, datetime, math

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

A = ['r39-s%d' % i for i in range(1, 6)]
RUNS = {
    'r39-s1': '2026-09-15-213428-8eab1085',
    'r39-s2': '2026-09-15-213503-8eab1085',
    'r39-s3': '2026-09-17-085758-8eab1085',
    'r39-s4': '2026-09-17-085833-8eab1085',
    'r39-s5': '2026-09-17-085303-8eab1085',
}
TIMES = [5000, 15000, 30000]
LIVE_COLS = 2211                       # the 1 m columns whose centres are inside the disc

# Round 38, from logbook/specs/r38-read/ — quoted, not recomputed.
R38_ALIVE_END = [1597, 1602, 1641, 1642, 1633]
R38_MEAN_ALIVE_END = 1623.0
R38_DETCV_MAX = {5000: 0.34, 15000: 0.17, 30000: 0.14}
R38_PACE = [774.4, 856.1, 872.2, 993.1, 952.9]
R38_PACE_MEAN = sum(R38_PACE) / 5.0
R38_NEAR_FLOOR_15000 = 1.4             # per cent of the living within 2 m of the floor

data = {}
for a in A:
    r, h = rows(a)
    data[a] = ({x['t']: x for x in r}, r, h)


def at(a, t):
    d, r, _ = data[a]
    return d[t] if t in d else r[-1]


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


def run_dir(a):
    return os.path.join(ROOT, 'runs', a, RUNS[a])


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
    for part in h.split('·'):
        p = part.strip()
        if p.startswith(key):
            return p
    return '(absent)'


DISC_LOW_SHARE = {}                    # E3's bar, from pos_analyse.py's bed-map.tsv
for line in open(os.path.join(HERE, 'bed-map.tsv'), encoding='utf-8'):
    f = line.rstrip('\n').split('\t')
    if f[0].startswith('r39-s'):
        DISC_LOW_SHARE[f[0]] = float(f[14])

# ================================================================== V1 to V5
V = ['## V1  the header tokens, the hashes and the pre-registration, per seed. 0102 asks'
     ' for "space tank r=26.46 m (2200 m2), depth 45, wall, bed relief 1.5 m tilt 30 m'
     ' scale 17.6 m (hollows n, ridges n, range x.xx m, steepest nn deg bands nn deg,'
     ' bound binds|clear)", one simHash, one coreHash, one configHash, physicsJobWorkers'
     ' 0, the seven bed* fields, worldDepthMetres 45, and prereg.json at the entry\'s'
     ' commit.',
     'arm\tseed\trun directory used\tspace token\tarea\tmatterBudget\tfounderDepth'
     '\tcurrent\tfluidAccel\tdispersal\tcorpse\tdt\tdriveLimit\tphysics jobs\tfield'
     '\tconfigHash\tsimHash\tcoreHash\tphysicsJobWorkers\tbed* fields in run.json'
     '\tworldDepthMetres in run.json\tworldDepthMetres in config.json\tgitCommit'
     '\tgitDirty\tprereg file\tprereg commit\tstatus\treason\tsimulated s\tworker']
BEDFIELDS = ['bedRelief', 'bedTilt', 'bedScale', 'bedHollows', 'bedRidges',
             'bedRangeMetres', 'bedSteepestDegrees']
for a in A:
    h = header_line(a)
    m = manifest(a)
    cfg = json.load(open(os.path.join(run_dir(a), 'config.json'), encoding='utf-8'))
    pr = json.load(open(os.path.join(ROOT, 'runs', a, 'prereg.json'), encoding='utf-8'))
    space = h.split('space tank')[1].split(' dispersal=')[0].strip() if 'space tank' in h else '(absent)'
    V.append('\t'.join(str(x) for x in [
        a, m['seed'], RUNS[a], 'space tank ' + space, tok(h, 'area'), tok(h, 'matterBudget'),
        tok(h, 'founderDepth'), tok(h, 'current'), tok(h, 'fluidAccel'),
        'dispersal=' + h.split('dispersal=')[1].split(' ·')[0] if 'dispersal=' in h else '(absent)',
        'corpse=' + h.split('corpse=')[1].split(' ·')[0] if 'corpse=' in h else '(absent)',
        tok(h, 'dt='), tok(h, 'driveLimit'), tok(h, 'physics jobs'), tok(h, 'field'),
        m['configHash'], m['source']['simHash'][:8], m['source']['coreHash'][:8],
        m['physicsJobWorkers'],
        '%d of 7 present' % sum(1 for k in BEDFIELDS if k in m),
        m.get('worldDepthMetres', 'ABSENT'), cfg['world']['worldDepthMetres'],
        m['source']['gitCommit'][:8], m['source']['gitDirty'], pr['file'], pr['commit'][:8],
        m['status'], m['reason'], m['simulatedSeconds'],
        os.path.basename(m['source']['workerPath'])]))

V += ['', '## V2 to V5, checked on every row of every seed',
      'arm\trows\taudit rows not 0.0000%\tmat resid rows not 0\twraps rows not 0\tmax wraps'
      '\tdiverged at end\tdivergedTotal in run.json\tdumps on disk\ttrace files'
      '\tdumps naming the bed\tmax |mat orphan|\trows with mat orphan != 0\tstatus\treason'
      '\tcrowded at end\tstillb at end']
for a in A:
    _, r, _ = data[a]
    m = manifest(a)
    dd = os.path.join(run_dir(a), 'diverged')
    dumps = sorted(glob.glob(os.path.join(dd, '*.json'))) if os.path.isdir(dd) else []
    traces = [d for d in dumps if 'trace' in os.path.basename(d).lower()]
    bedd = 0
    for d in dumps:
        try:
            if 'bed' in open(d, encoding='utf-8').read().lower():
                bedd += 1
        except OSError:
            pass
    orph = [abs(num(x['mat orphan']) or 0.0) for x in r]
    V.append('%s\t%d\t%d\t%d\t%d\t%g\t%g\t%d\t%d\t%d\t%d\t%g\t%d\t%s\t%s\t%g\t%g' % (
        a, len(r),
        sum(1 for x in r if num(x['audit']) != 0.0),
        sum(1 for x in r if num(x['mat resid']) != 0.0),
        sum(1 for x in r if num(x['wraps']) != 0.0),
        max(num(x['wraps']) for x in r),
        num(r[-1]['diverged']), m['divergedTotal'], len(dumps), len(traces), bedd,
        max(orph), sum(1 for v in orph if v != 0.0), m['status'], m['reason'],
        num(r[-1]['crowded']), num(r[-1]['stillb'])))
write('rules.tsv', V)

# ================================================================== E1
e1 = ['## E1  the matter decides the crowd, not the water: alive at 30,000 s between 2,080'
      ' and 4,460 in 4 of 5, and births by 1,000 s at least 40 in every seed. Round 38 read'
      ' 1,597 to 1,642, mean 1,623.',
      'arm\talive at 30000\tin 2080-4460\tbirths by 1000 s\t>= 40\talive at 5000'
      '\tratio to r38 mean\tpeak alive\tat\tmin alive after 5000\tmax alive after 5000']
n1a = n1b = 0
for a in A:
    _, r, _ = data[a]
    end = num(at(a, 30000)['alive'])
    b1 = num(at(a, 1000)['births'])
    post = [x for x in r if x['t'] >= 5000]
    pk = max(post, key=lambda x: num(x['alive']))
    ok_a = 2080 <= end <= 4460
    ok_b = b1 >= 40
    n1a += ok_a
    n1b += ok_b
    e1.append('%s\t%d\t%s\t%d\t%s\t%d\t%.3f\t%d\t%d\t%d\t%d' % (
        a, end, 'yes' if ok_a else 'NO', b1, 'yes' if ok_b else 'NO',
        num(at(a, 5000)['alive']), end / R38_MEAN_ALIVE_END, num(pk['alive']), pk['t'],
        min(num(x['alive']) for x in post), max(num(x['alive']) for x in post)))
e1.append('count\talive in band %d of 5 (bar 4)\tbirths >= 40 %d of 5 (bar 5)' % (n1a, n1b))

e1 += ['', '## E1\'s companion reading: the matter against the births over the first'
       ' 3,000 s. mat blk is a per-window count of blocked conceptions and scales with the'
       ' population, so it is read against births in the same window (CLAUDE.md).',
       'arm\tt\tbirths in window\tmat blk in window\tblocked per birth\tmat short'
       '\tmat here\tmat cv\tmat resid']
for a in A:
    _, r, _ = data[a]
    prev = 0
    for x in r:
        if x['t'] > 3000:
            break
        bw = num(x['births']) - prev
        prev = num(x['births'])
        e1.append('%s\t%d\t%d\t%d\t%.1f\t%s\t%s\t%s\t%s' % (
            a, x['t'], bw, num(x['mat blk']),
            num(x['mat blk']) / bw if bw else float('nan'),
            x['mat short'], x['mat here'], x['mat cv'], x['mat resid']))
write('e1-population.tsv', e1)

# ================================================================== E2
e2 = ['## E2  the water makes pockets: det cv above round 38\'s five-seed maximum at the'
      ' same second (0.34 at 5,000, 0.17 at 15,000, 0.14 at 30,000) in 4 of 5; above it at'
      ' 15,000 and 30,000 in EVERY seed; above 0.5 at 15,000 or 30,000 in 3 of 5.',
      'arm\tdet cv 5000\t> 0.34\tdet cv 15000\t> 0.17\tdet cv 30000\t> 0.14'
      '\tabove 0.5 at 15000 or 30000\tmat cv 5000\tmat cv 15000\tmat cv 30000'
      '\tdet cv min after 5000\tdet cv max after 5000\tdet cv at end']
c2 = {5000: 0, 15000: 0, 30000: 0}
n2_all = n2_half = 0
for a in A:
    _, r, _ = data[a]
    v = {t: num(at(a, t)['det cv']) for t in TIMES}
    ok = {t: v[t] > R38_DETCV_MAX[t] for t in TIMES}
    for t in TIMES:
        c2[t] += ok[t]
    n2_all += ok[15000] and ok[30000]
    half = v[15000] > 0.5 or v[30000] > 0.5
    n2_half += half
    post = [num(x['det cv']) for x in r if x['t'] >= 5000]
    e2.append('%s\t%.3f\t%s\t%.3f\t%s\t%.3f\t%s\t%s\t%s\t%s\t%s\t%.3f\t%.3f\t%.3f' % (
        a, v[5000], 'yes' if ok[5000] else 'NO', v[15000], 'yes' if ok[15000] else 'NO',
        v[30000], 'yes' if ok[30000] else 'NO', 'yes' if half else 'NO',
        at(a, 5000)['mat cv'], at(a, 15000)['mat cv'], at(a, 30000)['mat cv'],
        min(post), max(post), num(r[-1]['det cv'])))
e2.append('count\t%d of 5 at 5000\t%d of 5 at 15000\t%d of 5 at 30000'
          '\tboth 15000 and 30000 %d of 5 (bar 5)\tabove 0.5 %d of 5 (bar 3)'
          % (c2[5000], c2[15000], c2[30000], n2_all, n2_half))
write('e2-fields.tsv', e2)

# ================================================================== E3
e3 = ['## E3  the sediment gathers where the floor is low: floor low % above the lowest'
      ' quarter\'s own share of the disc at 15,000 and 30,000 s in 4 of 5, and the floor'
      ' stock\'s lowest decile at least twice the highest decile\'s. The disc share is'
      ' computed from the rebuilt map (bed-map.tsv): the fraction of the 2,211 live 1 m'
      ' columns whose floor is in the lowest quarter of the floor\'s own range, which is'
      ' the same rule EvolutionRun uses on the stock.',
      'arm\tdisc lowest-quarter share\tfloor low % 15000\tabove share\tfloor low % 30000'
      '\tabove share\tE3a both times\tfloor J 15000\tfloor J 30000\t% on floor 15000'
      '\t% on floor 30000\tlowest decile 30000\thighest decile 30000\tratio low/high'
      '\tE3b ratio >= 2\tlowest decile 15000\thighest decile 15000\tratio 15000']
n3a = n3b = 0
DEC = {}
for a in A:
    st = stats(a)
    by_t = {round(s['t']): s for s in st if 't' in s}
    share = DISC_LOW_SHARE[a] * 100.0
    v15 = num(at(a, 15000)['floor low %'])
    v30 = num(at(a, 30000)['floor low %'])
    ok = v15 > share and v30 > share
    n3a += ok
    d = {}
    for t in (15000, 30000):
        s = by_t.get(t)
        d[t] = s.get('floorStockByFloorDecile') if s else None
    DEC[a] = d
    r30 = (d[30000][0] / d[30000][-1]) if d[30000] and d[30000][-1] else float('nan')
    r15 = (d[15000][0] / d[15000][-1]) if d[15000] and d[15000][-1] else float('nan')
    okb = r30 >= 2.0
    n3b += bool(okb)
    e3.append('%s\t%.2f%%\t%.1f%%\t%s\t%.1f%%\t%s\t%s\t%s\t%s\t%s\t%s\t%.4f\t%.4f\t%.2f'
              '\t%s\t%.4f\t%.4f\t%.2f' % (
                  a, share, v15, 'yes' if v15 > share else 'NO', v30,
                  'yes' if v30 > share else 'NO', 'yes' if ok else 'NO',
                  at(a, 15000)['floor J'], at(a, 30000)['floor J'],
                  at(a, 15000)['% on floor'], at(a, 30000)['% on floor'],
                  d[30000][0] if d[30000] else float('nan'),
                  d[30000][-1] if d[30000] else float('nan'), r30,
                  'yes' if okb else 'NO',
                  d[15000][0] if d[15000] else float('nan'),
                  d[15000][-1] if d[15000] else float('nan'), r15))
e3.append('count\tE3a %d of 5 (bar 4)\tE3b %d of 5' % (n3a, n3b))

e3 += ['', '## the whole decile profile, lowest tenth of the columns first (a tenth each if'
       ' the sediment ignored the floor\'s shape)', 'arm\tt\t' + '\t'.join(
           'd%d' % i for i in range(1, 11))]
for a in A:
    for t in (15000, 30000):
        d = DEC[a][t]
        e3.append('%s\t%d\t%s' % (a, t, '\t'.join('%.4f' % x for x in d) if d else '-'))

e3 += ['', '## floor low % at every 1,000 s, against each seed\'s own disc share',
       'arm\tdisc share %\t' + '\t'.join(str(t) for t in range(1000, 31000, 1000))]
for a in A:
    e3.append('%s\t%.2f\t%s' % (a, DISC_LOW_SHARE[a] * 100.0, '\t'.join(
        '%.1f' % num(at(a, t)['floor low %']) for t in range(1000, 31000, 1000))))
write('e3-floor.tsv', e3)

# ================================================================== E5
e5 = ['## E5  the boom and bust is not the floor\'s to fix: the eaters\' inherited count'
      ' peaks above 550 and falls below a sixth of its peak within 10,000 s of the peak,'
      ' in at least 3 of 5. Round 38 read peaks of 288 to 720 and troughs of 0 to 2.',
      'arm\tpeak inherit\tat\tpeak share of living\t> 550\ttrough after peak\tat'
      '\tt of first fall below peak/6\twithin 10,000 s of peak\tE5 (both)\tinherit at end'
      '\tabsorpt at end\tsecond boom after the trough']
n5 = 0
for a in A:
    _, r, _ = data[a]
    pk = max(r, key=lambda x: num(x['inherit']))
    peak = num(pk['inherit'])
    after = [x for x in r if x['t'] > pk['t']]
    below = next((x for x in after if num(x['inherit']) < peak / 6.0), None)
    trough = min(after, key=lambda x: num(x['inherit'])) if after else pk
    within = below is not None and (below['t'] - pk['t']) <= 10000
    ok = peak > 550 and within
    n5 += ok
    second = '-'
    if below is not None:
        later = [x for x in r if x['t'] > below['t']]
        if later:
            m2 = max(later, key=lambda x: num(x['inherit']))
            second = 'peak %d at %d' % (num(m2['inherit']), m2['t'])
    e5.append('%s\t%d\t%d\t%.3f\t%s\t%d\t%d\t%s\t%s\t%s\t%d\t%d\t%s' % (
        a, peak, pk['t'], peak / num(pk['alive']), 'yes' if peak > 550 else 'NO',
        num(trough['inherit']), trough['t'],
        below['t'] if below is not None else 'never', 'yes' if within else 'NO',
        'yes' if ok else 'NO', num(r[-1]['inherit']), num(r[-1]['absorpt']), second))
e5.append('count\t%d of 5 (bar 3)' % n5)

e5 += ['', '## the eaters\' course at every 1,000 s (absorpt / inherit)',
       'arm\t' + '\t'.join(str(t) for t in range(1000, 31000, 1000))]
for a in A:
    e5.append('%s\t%s' % (a, '\t'.join(
        '%d/%d' % (num(at(a, t)['absorpt']), num(at(a, t)['inherit']))
        for t in range(1000, 31000, 1000))))
write('e5-eaters.tsv', e5)

# ================================================================== E6
e6 = ['## E6  the disc stays mixed on a slope: the rim quarter 15 to 40% at 5,000, 15,000'
      ' and 30,000 s in 4 of 5; cols at least 60% of the live columns from 5,000 s. The'
      ' second bar is miscalibrated for this geometry (0102\'s own early look): n bodies'
      ' dropped at random into 2,211 columns occupy 1 - exp(-n/2211) of them, which is 58%'
      ' at 1,900 bodies, so the column count is given against its own uniform expectation.',
      'arm\tp3/alive 5000\tp3/alive 15000\tp3/alive 30000\tall three in 0.15-0.40'
      '\tcols 5000\tcols 15000\tcols 30000\tmin cols share after 5000'
      '\tcols >= 60% on every row from 5000\tcols/uniform 5000\tcols/uniform 15000'
      '\tcols/uniform 30000\tmin cols/uniform after 5000\tx sd 5000\tx sd 15000\tx sd 30000']
n6 = n6b = 0
for a in A:
    _, r, _ = data[a]
    p3 = {t: num(at(a, t)['p3']) / num(at(a, t)['alive']) for t in TIMES}
    ok = all(0.15 <= p3[t] <= 0.40 for t in TIMES)
    n6 += ok
    post = [x for x in r if x['t'] >= 5000]
    share = {t: num(at(a, t)['cols']) / LIVE_COLS for t in TIMES}
    minshare = min(num(x['cols']) / LIVE_COLS for x in post)
    okb = minshare >= 0.60
    n6b += okb

    def uni(x):
        n = num(x['alive'])
        return (num(x['cols']) / LIVE_COLS) / (1.0 - math.exp(-n / LIVE_COLS))

    e6.append('%s\t%.3f\t%.3f\t%.3f\t%s\t%.3f\t%.3f\t%.3f\t%.3f\t%s\t%.3f\t%.3f\t%.3f'
              '\t%.3f\t%s\t%s\t%s' % (
                  a, p3[5000], p3[15000], p3[30000], 'yes' if ok else 'NO',
                  share[5000], share[15000], share[30000], minshare,
                  'yes' if okb else 'NO', uni(at(a, 5000)), uni(at(a, 15000)),
                  uni(at(a, 30000)), min(uni(x) for x in post),
                  at(a, 5000)['x sd'], at(a, 15000)['x sd'], at(a, 30000)['x sd']))
e6.append('count\trim quarter %d of 5 (bar 4)\tcols >= 60%% %d of 5' % (n6, n6b))
write('e6-mixing.tsv', e6)

# ================================================================== E7
e7 = ['## E7  the rock throws nothing new: no dump whose reason names the bed in 5 of 5;'
      ' diverged between 0 and 10 per million jointed body-seconds in 4 of 5. A sample is'
      ' 100 s, so a column\'s standing count summed over samples times 100 is body-seconds.',
      'round\tarm\tsamples\tinterval s\tsum jnt inh\tjnt-inh body-s\tsum jointed'
      '\tjointed body-s\tdiverged\tdumps on disk\tbed dumps\tper 1e6 jnt-inh body-s'
      '\tper 1e6 jointed body-s\tin 0 to 10 per 1e6 jointed']
tot = [0.0, 0.0, 0.0]
n7 = 0
for a in A:
    _, r, _ = data[a]
    si = sum(num(x['jnt inh']) for x in r)
    sj = sum(num(x['jointed']) for x in r)
    dv = num(r[-1]['diverged'])
    bi, bj = si * 100, sj * 100
    tot[0] += bi
    tot[1] += bj
    tot[2] += dv
    rate = 1e6 * dv / bj if bj else 0
    n7 += 0 <= rate <= 10
    dd = os.path.join(run_dir(a), 'diverged')
    dumps = sorted(glob.glob(os.path.join(dd, '*.json'))) if os.path.isdir(dd) else []
    e7.append('r39\t%s\t%d\t100\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%.2f\t%.2f\t%s' % (
        a, len(r), si, bi, sj, bj, dv, len(dumps), 0,
        1e6 * dv / bi if bi else 0, rate, 'yes' if 0 <= rate <= 10 else 'NO'))
e7.append('r39\tall five\t-\t-\t-\t%d\t-\t%d\t%d\t-\t0\t%.2f\t%.2f\t%d of 5 in band' % (
    tot[0], tot[1], tot[2],
    1e6 * tot[2] / tot[0] if tot[0] else 0, 1e6 * tot[2] / tot[1] if tot[1] else 0, n7))
e7 += ['', '## comparison, NOT recomputed (from logbook/specs/r38-read/exposure.tsv and'
       ' the rounds before it):',
       'r38 s1-s5\t-\t-\t-\t-\t-\t-\t6800000 (0101\'s reading)\t10\t-\t-\t-\t0 to 5.5\t-',
       'r37b s1-s5\t-\t-\t-\t-\t11133400\t-\t11792900\t43\t-\t-\t3.86\t3.65\t-',
       'r37 s1-s5\t-\t-\t-\t-\t9173400\t-\t9491200\t0\t-\t-\t0.00\t0.00\t-']
write('e7-exposure.tsv', e7)

# ================================================================== E8
def parse(s):
    return datetime.datetime.strptime(s[:19], '%Y-%m-%dT%H:%M:%S')


pa = ['## E8  the pace is the grid\'s: wall seconds per 1,000 simulated seconds per 1,000'
      ' living bodies between 1,340 and 3,120 (1.5 to 3.5 of round 38\'s five-seed mean of'
      ' %.1f). Round 38 read 774.4 / 856.1 / 872.2 / 993.1 / 952.9 (r38-read/pace.tsv).'
      % R38_PACE_MEAN,
      'round\tarm\tstartedAt (UTC)\tendedAt (UTC)\tstatus\twall s\tsimulated s'
      '\ts per 1000 sim s\tmean alive\ts per 1000 sim s per 1000 bodies'
      '\tratio to r38 mean\tin 1340-3120\tfooter wall minutes\tfooter timesRealTime'
      '\twall split line in the footer\tworker']
n8 = 0
for a in A:
    _, r, _ = data[a]
    m = manifest(a)
    st, en = parse(m['startedAt']), parse(m['endedAt'])
    wall = (en - st).total_seconds()
    per = wall / (m['simulatedSeconds'] / 1000.0)
    mean = sum(num(x['alive']) for x in r) / len(r)
    norm = per / (mean / 1000.0)
    ok = 1340 <= norm <= 3120
    n8 += ok
    split = 'absent' if 'wall split' not in open(
        os.path.join(ROOT, 'runs', a + '.md'), encoding='utf-8').read() else 'present'
    pa.append('r39\t%s\t%s\t%s\t%s\t%.0f\t%d\t%.1f\t%.0f\t%.1f\t%.3f\t%s\t%s\t%s\t%s\t%s' % (
        a, m['startedAt'][:19], m['endedAt'][:19], m['status'], wall,
        m['simulatedSeconds'], per, mean, norm, norm / R38_PACE_MEAN,
        'yes' if ok else 'NO', m['wallClockMinutes'], m['timesRealTime'], split,
        os.path.basename(m['source']['workerPath'])))
pa.append('count\t%d of 5' % n8)

pa += ['', '## how many r39 arms shared the machine over each arm\'s own life (UTC'
       ' manifest windows of the five runs this read uses; the earlier censored runs of'
       ' seeds 3 to 5 are listed under them and are NOT counted, since they had ended)',
       'arm\tstart\tend\thours\tmean concurrent r39 arms\tmin\tmax']
spans = {a: (parse(manifest(a)['startedAt']), parse(manifest(a)['endedAt'])) for a in A}
for a in A:
    st, en = spans[a]
    hours = (en - st).total_seconds() / 3600.0
    n = 400
    counts = []
    for i in range(n):
        tm = st + (en - st) * (i + 0.5) / n
        counts.append(sum(1 for b in A if spans[b][0] <= tm <= spans[b][1]))
    pa.append('%s\t%s\t%s\t%.2f\t%.2f\t%d\t%d' % (
        a, st.strftime('%m-%d %H:%M'), en.strftime('%m-%d %H:%M'), hours,
        sum(counts) / len(counts), min(counts), max(counts)))

pa += ['', '## the earlier, censored runs of seeds 3 to 5 (not used by this read)',
       'arm\trun directory\tstatus\treason\tsimulated s\tstartedAt\tendedAt/stoppedAt']
for a in A:
    for d in sorted(glob.glob(os.path.join(ROOT, 'runs', a, '*', 'run.json'))):
        if os.path.basename(os.path.dirname(d)) == RUNS[a]:
            continue
        m = json.load(open(d, encoding='utf-8'))
        pa.append('%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
            a, os.path.basename(os.path.dirname(d)), m.get('status'), m.get('reason'),
            m.get('simulatedSeconds'), m.get('startedAt', '')[:19],
            (m.get('endedAt') or m.get('stoppedAt') or '')[:19]))
write('pace.tsv', pa)

# ================================================================== extras
ex = ['## Recorded and not predicted, from the table. Round 38\'s figures (0101) are in'
      ' the note on each block and are not recomputed.',
      '', '## the jointed count\'s course: jnt inh at every 1,000 s (r38 peaked 73 to 226'
      ' by 3,200 s and ended 0/0/0/1/15)',
      'arm\t' + '\t'.join(str(t) for t in range(1000, 31000, 1000))]
for a in A:
    ex.append('%s\t%s' % (a, '\t'.join(
        '%d' % num(at(a, t)['jnt inh']) for t in range(1000, 31000, 1000))))
ex += ['', 'arm\tjnt inh peak\tat\tjointed peak\tat\tjnt inh at end\tjointed at end'
       '\tjointed % at end\tseeds holding >= 10 inherited jointed at the end']
for a in A:
    _, r, _ = data[a]
    p = max(r, key=lambda x: num(x['jnt inh']))
    pj = max(r, key=lambda x: num(x['jointed']))
    ex.append('%s\t%d\t%d\t%d\t%d\t%d\t%d\t%s\t%s' % (
        a, num(p['jnt inh']), p['t'], num(pj['jointed']), pj['t'],
        num(r[-1]['jnt inh']), num(r[-1]['jointed']), r[-1]['jointed %'],
        'yes' if num(r[-1]['jnt inh']) >= 10 else 'no'))

ex += ['', '## stillbirths and corpses (r38: stillbirths 214 to 1,294 per seed, 13 to 130'
       ' per 1,000 births; corpses 488 to 2,355 standing)',
       'arm\tstillb 5000\tstillb 15000\tstillb at end\tbirths at end\tper 1000 births'
       '\tcrowded at end\tcorpses 5000\tcorpses 15000\tcorpses 30000\tmax corpses\tat']
for a in A:
    _, r, _ = data[a]
    cm = max(r, key=lambda x: num(x['corpses']))
    ex.append('%s\t%d\t%d\t%d\t%d\t%.1f\t%d\t%d\t%d\t%d\t%d\t%d' % (
        a, num(at(a, 5000)['stillb']), num(at(a, 15000)['stillb']), num(r[-1]['stillb']),
        num(r[-1]['births']), 1000.0 * num(r[-1]['stillb']) / num(r[-1]['births']),
        num(r[-1]['crowded']), num(at(a, 5000)['corpses']), num(at(a, 15000)['corpses']),
        num(at(a, 30000)['corpses']), num(cm['corpses']), cm['t']))

ex += ['', '## the water and the larder (r38: mean m/s 0.075 to 0.080, still the water\'s;'
       ' refuge J fell from 450-1,049 to 83-185)',
       'arm\tmean m/s 5000\tmean m/s 15000\tmean m/s 30000\tdetritus J 5000'
       '\tdetritus J 15000\tdetritus J 30000\tmax detritus J\tat\trefuge J 15000'
       '\trefuge J 30000\tdet deep 15000\tdet deep 30000\tdepth m 15000\tdepth m 30000'
       '\tfloor con 15000\tfloor con 30000']
for a in A:
    _, r, _ = data[a]
    dm = max(r, key=lambda x: num(x['detritus J']))
    ex.append('%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
        a, at(a, 5000)['mean m/s'], at(a, 15000)['mean m/s'], at(a, 30000)['mean m/s'],
        at(a, 5000)['detritus J'], at(a, 15000)['detritus J'], at(a, 30000)['detritus J'],
        dm['detritus J'], dm['t'], at(a, 15000)['refuge J'], at(a, 30000)['refuge J'],
        at(a, 15000)['det deep'], at(a, 30000)['det deep'],
        at(a, 15000)['depth m'], at(a, 30000)['depth m'],
        at(a, 15000)['floor con'], at(a, 30000)['floor con']))

ex += ['', '## the cycle: local extrema of alive over t >= 5,000 (a sample extreme in its'
       ' +-1500 s window). r38 read a peak-to-trough amplitude of 1.10 to 1.23.',
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
            ex.append('%s\t%s\t%d\t%d\t%.3f\t%s\t%d\t%d\t%s\t%s' % (
                a, kind, x['t'], v, num(x['p3']) / v, x['depth m'], num(x['inherit']),
                num(x['jnt inh']), x['shade %'], x['det cv']))
ex += ['', 'arm\tmax alive after 5000\tmin alive after 5000\tpeak-to-trough']
for a in A:
    _, r, _ = data[a]
    post = [num(x['alive']) for x in r if x['t'] >= 5000]
    ex.append('%s\t%d\t%d\t%.3f' % (a, max(post), min(post), max(post) / min(post)))
write('extras.tsv', ex)

# ================================================================== the timeline TSV
cols = ['alive', 'cols', 'cols abs', 'x sd', 'p0', 'p1', 'p2', 'p3', 'absorpt', 'inherit',
        'jnt inh', 'mean m/s', 'diverged', 'wraps', 'crowded', 'stillb', 'births',
        'mat blk', 'mat short', 'audit', 'mat resid', 'det cv', 'mat cv', 'det deep',
        'corpses', 'floor low %', 'floor J', '% on floor', 'floor con', 'refuge J',
        'depth m', 'detritus J', 'shade %', 'jointed', 'photo', 'photo inh']
tl = ['## The timeline at every 1,000 s. The columns are round 38\'s brief\'s, plus the'
      ' shaped bed\'s two (floor low %, floor J) and the floor readings beside them'
      ' (% on floor, floor con, refuge J). Read by NAME from the run report.',
      'arm\tt\t' + '\t'.join(cols)]
for a in A:
    for t in range(1000, 31000, 1000):
        x = at(a, t)
        tl.append('%s\t%d\t%s' % (a, t, '\t'.join(x[c] for c in cols)))
write('timeline-every1000.tsv', tl)
write('timeline-key.tsv', ['## the column name -> index map of this build\'s report table',
                           'index\tname'] +
      ['%d\t%s' % (i + 1, n) for i, n in enumerate(data[A[0]][2])])

print('wrote rules.tsv, e1-population.tsv, e2-fields.tsv, e3-floor.tsv, e5-eaters.tsv,'
      ' e6-mixing.tsv, e7-exposure.tsv, pace.tsv, extras.tsv, timeline-every1000.tsv,'
      ' timeline-key.tsv')

# ================================================================== the matter's books
mb = ['## Recorded and not predicted: where the 11,000 units of matter are at the last'
      ' sample, from stats.jsonl. matterStanding is the identity and reads 11,000.0 exactly'
      ' in every seed; matterLocked is what the living bodies hold.',
      'arm\tmatterStanding\tmatterResidual\tmatterLocked\tlocked share\tcorpseMatter'
      '\tfree in the water (standing - locked - corpses)\tmat here units/m3'
      '\tconceptionsBlockedByMatter (whole run)\tconceptionsShortOfMatter'
      '\tgrowthShortOfMatter\tmatterCv\tmatterOrphaned']
for a in A:
    s = stats(a)[-1]
    free = s['matterStanding'] - s['matterLocked'] - s['corpseMatter']
    mb.append('%s\t%.1f\t%g\t%.2f\t%.3f\t%.2f\t%.2f\t%.4f\t%d\t%d\t%d\t%.3f\t%g' % (
        a, s['matterStanding'], s['matterResidual'], s['matterLocked'],
        s['matterLocked'] / s['matterStanding'], s['corpseMatter'], free,
        s['matterHere'], s['conceptionsBlockedByMatter'], s['conceptionsShortOfMatter'],
        s['growthShortOfMatter'], s['matterCv'], s['matterOrphaned']))
write('matter.tsv', mb)
print('wrote matter.tsv')
