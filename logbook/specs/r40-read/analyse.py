# -*- coding: utf-8 -*-
"""The round 40 read (logbook/0106's V1 to V5 and L1 to L9), three seeds of a planned five on
round 39's world with the light's reach halved (EVOSIM_LIGHT_REACH 6 m, attenuationDepth 6).

The round was stopped by the owner at 16,400, 12,200 and 14,300 simulated seconds
(status stopped, reason manual-other), so 0106's V5 applies: every seed is censored and read
at its last sample.  A prediction that names 30,000 s can only be read as a reading at that
last sample, and one that names 15,000 s can be read outright in seed 1 alone.  Every such
call is carried in the verdict column as `not readable` with the reason, never silently
converted into a bar the round did not pre-register.

Everything the run report, stats.jsonl and the manifests can answer, keyed by column NAME.
The depth readings are pos_analyse.py's, the boom reading booms.py's, both beside this file.
Round 39's five seeds (0105, logbook/specs/r39-read/) are quoted, never recomputed.

Writes rules.tsv, timeline-every1000.tsv, shade.tsv, matter.tsv, rim.tsv, exposure.tsv,
extras.tsv and summary.tsv beside this file.
"""
import json, os, sys, glob, math, datetime

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

ARMS = ['r40-s1', 'r40-s2', 'r40-s3']
LIVE_COLS = 2211                       # the 1 m columns whose centres are inside the disc
R39_SHADE_15000_MAX = 3.5              # 0105's five-seed maxima, quoted
R39_SHADE_30000_MAX = 3.1
R39_ALIVE_MEAN_END = 2397.0
R39_PACE = {'r39-s3': 903.9, 'r39-s4': 903.3, 'r39-s5': 860.1}   # the three-arm seeds


def iso(s):
    """The manifest's timestamps carry seven fractional digits; fromisoformat takes six."""
    s = s.replace('Z', '')
    if '.' in s:
        head, frac = s.split('.')
        s = head + '.' + frac[:6]
    return datetime.datetime.fromisoformat(s)


def run_dir(arm):
    return sorted(glob.glob(os.path.join(ROOT, 'runs', arm, '*' + os.sep)))[-1]


def write(name, lines):
    with open(os.path.join(HERE, name), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')


def manifest(arm):
    return json.load(open(os.path.join(run_dir(arm), 'run.json'), encoding='utf-8'))


def config(arm):
    return json.load(open(os.path.join(run_dir(arm), 'config.json'), encoding='utf-8'))


def stats(arm):
    out = []
    with open(os.path.join(run_dir(arm), 'stats.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line.startswith('{'):
                out.append(json.loads(line))
    return out


def header_line(arm):
    for line in open(os.path.join(ROOT, 'runs', arm + '.md'), encoding='utf-8'):
        if 'configHash' in line and 'seed' in line:
            return line.strip()
    return ''


def depth_band():
    """pos_analyse.py's table, keyed (arm, t, guild)."""
    out = {}
    path = os.path.join(HERE, 'depth-band.tsv')
    for line in open(path, encoding='utf-8'):
        if line.startswith('#') or line.startswith('arm\t'):
            continue
        c = line.rstrip('\n').split('\t')
        out[(c[0], int(c[1]), c[2])] = c
    return out


DATA = {}
for a in ARMS:
    r, h = rows(a)
    DATA[a] = (r, h, {x['t']: x for x in r})
LAST = {a: DATA[a][0][-1]['t'] for a in ARMS}
MAN = {a: manifest(a) for a in ARMS}
CFG = {a: config(a) for a in ARMS}
ST = {a: stats(a) for a in ARMS}
DB = depth_band()


def at(a, t):
    return DATA[a][2].get(t)


def verdict(ok):
    return 'HOLDS' if ok else 'FAILS'


# --------------------------------------------------------------------------- V1 to V5
rl = ['## 0106\'s rules before scoring, V1 to V5.', 'rule\tarm\tresult\tthe numbers']
for a in ARMS:
    hdr = header_line(a)
    m = MAN[a]
    toks = ['light reach 6 m', 'space tank r=26.46 m (2200 m2), depth 45, wall',
            'matterBudget 11000', 'fluidAccel 1', 'dt=0.01', 'physics jobs 0']
    miss = [t for t in toks if t not in hdr]
    pj = json.load(open(os.path.join(run_dir(a), 'prereg.json'), encoding='utf-8'))
    rl.append('V1\t%s\t%s\tattenuationDepth %s; simHash %s; coreHash %s; configHash %s;'
              ' physicsJobWorkers %s; prereg %s (%s); missing header tokens: %s' % (
                  a, 'holds' if not miss else 'MISSING TOKENS',
                  CFG[a]['light']['attenuationDepth'], m['source']['simHash'][:8],
                  m['source']['coreHash'][:8], m['configHash'][:8], m['physicsJobWorkers'],
                  pj.get('commit', '?')[:7], pj.get('file', '?'),
                  ', '.join(miss) if miss else 'none'))
for a in ARMS:
    r = DATA[a][0]
    bad_audit = [x['t'] for x in r if x['audit'].strip() not in ('0.0000%',)]
    bad_mat = [x['t'] for x in r if num(x['mat resid']) not in (0.0, None)]
    s = ST[a]
    raw_res = max(abs(x['matterResidual']) for x in s)
    raw_orph = max(abs(x['matterOrphaned']) for x in s)
    rl.append('V2\t%s\t%s\t%d rows; audit off 0.0000%% on %d; mat resid nonzero on %d;'
              ' raw |matterResidual| max %.2e; raw |matterOrphaned| max %.2e' % (
                  a, 'holds' if not bad_audit and not bad_mat else 'FAILS',
                  len(r), len(bad_audit), len(bad_mat), raw_res, raw_orph))
for a in ARMS:
    r = DATA[a][0]
    bad = [x['t'] for x in r if num(x['wraps']) not in (0.0, None)]
    rl.append('V3\t%s\t%s\twraps nonzero on %d of %d rows (a tank has no seam)' % (
        a, 'holds' if not bad else 'FAILS', len(bad), len(r)))
for a in ARMS:
    d = os.path.join(run_dir(a), 'diverged')
    files = sorted(glob.glob(os.path.join(d, '*.json'))) if os.path.isdir(d) else []
    dumps = [p for p in files if not p.endswith('-trace.json')]
    col = num(DATA[a][0][-1]['diverged'])
    traced, notes = 0, []
    for p in dumps:
        tp = p[:-5] + '-trace.json'
        if not os.path.exists(tp):
            continue
        traced += 1
        j = json.load(open(tp, encoding='utf-8'))
        notes.append('body %s at %s s, %s parts, joint mass ratio %.2f, age %s s, framesHeld'
                     ' %s of which %s finite, first non-finite link %s, %s steps from that'
                     ' step to the dump' % (
                         j['creatureId'], j['t'], j['parts'], j['maxJointMassRatio'],
                         j['ageSeconds'], j['framesHeld'], j['framesFinite'],
                         j['firstNonFiniteLink'], j['stepsFromFirstNonFiniteToDump']))
    rl.append('V4\t%s\t%s\tdiverged column %s at the last sample; %d dump(s), %d with a'
              ' -trace.json. %s' % (
                  a, 'holds' if traced == len(dumps) else 'FAILS', col, len(dumps), traced,
                  '; '.join(notes) if notes else 'no dump to check'))
for a in ARMS:
    m = MAN[a]
    rl.append('V5\t%s\tcensored\tstatus %s, reason %s, stoppedAt %s; %d of %d s simulated'
              ' (%.1f%%); wall limit %s min; read at the last sample' % (
                  a, m['status'], m['reason'], m.get('stoppedAt'), LAST[a],
                  m['requestedSeconds'], 100.0 * LAST[a] / m['requestedSeconds'],
                  m['requestedWallMinutes']))
write('rules.tsv', rl)

# --------------------------------------------------------------------------- the timeline
COLS = ['alive', 'births', 'cols', 'cols abs', 'x sd', 'p0', 'p1', 'p2', 'p3', 'absorpt',
        'inherit', 'jnt inh', 'photo', 'photo inh', 'shade %', 'depth m', 'mean m/s',
        'diverged', 'wraps', 'crowded', 'stillb', 'mat blk', 'mat short', 'audit',
        'mat resid', 'det cv', 'mat cv', 'det deep', 'corpses', 'floor J', 'floor low %',
        'mat locked', 'adult scale', 'invest', 'brood', 'body frac', 'jointed']
tl = ['## The run report at every 1,000 s, by column NAME. The last row of each seed is its'
      ' own last sample (16,400 / 12,200 / 14,300 s).', 'arm\tt\t' + '\t'.join(COLS)]
for a in ARMS:
    ts = [t for t in range(1000, LAST[a] + 1, 1000)]
    if LAST[a] not in ts:
        ts.append(LAST[a])
    for t in ts:
        x = at(a, t)
        if x is None:
            continue
        tl.append('%s\t%d\t%s' % (a, t, '\t'.join(x.get(c, '-') for c in COLS)))
write('timeline-every1000.tsv', tl)

# --------------------------------------------------------------------------- L2, the shade
sh = ['## L2  shading is selected: `shade %` at every 1,000 s, against round 39\'s five-seed'
      ' maxima (3.5% at 15,000 s, 3.1% at 30,000 s, run maxima 2.7 to 4.7%).',
      'arm\t' + '\t'.join('%d' % t for t in range(1000, 17000, 1000)) + '\tlast sample'
      '\tshade at last\trun max\tat']
for a in ARMS:
    cells = []
    for t in range(1000, 17000, 1000):
        x = at(a, t)
        cells.append(('%.1f' % num(x['shade %'])) if x else '-')
    r = DATA[a][0]
    mx = max(r, key=lambda x: num(x['shade %']))
    sh.append('%s\t%s\t%d\t%.1f\t%.1f\t%d' % (a, '\t'.join(cells), LAST[a],
                                              num(r[-1]['shade %']), num(mx['shade %']),
                                              mx['t']))
write('shade.tsv', sh)

# --------------------------------------------------------------------------- L5, the matter
mt = ['## L5  more of the matter is free: matterLocked over matterStanding. The bar names'
      ' 30,000 s, which no seed reached; the last sample is the reading.',
      'arm\tt\tmatterStanding\tmatterLocked\tlocked share\tcorpseMatter\tfree in the water'
      '\tmat short\tmatterResidual']
MATTER = {}
for a in ARMS:
    s = ST[a]
    for x in [y for y in s if y['t'] in (5000, 10000, 15000)] + [s[-1]]:
        share = x['matterLocked'] / x['matterStanding']
        free = x['matterStanding'] - x['matterLocked'] - x.get('corpseMatter', 0.0)
        mt.append('%s\t%d\t%.1f\t%.1f\t%.4f\t%.1f\t%.1f\t%s\t%.2e' % (
            a, x['t'], x['matterStanding'], x['matterLocked'], share,
            x.get('corpseMatter', 0.0), free, x.get('conceptionsShortOfMatter', '-'),
            x['matterResidual']))
        if x is s[-1]:
            MATTER[a] = share
write('matter.tsv', mt)

# --------------------------------------------------------------------------- L7, the rim
rm = ['## L7  the disc stays mixed: the rim quarter p3/alive, and `cols` against the uniform'
      ' expectation 1 - e^(-n/2211) on 2,211 live 1 m columns. The bar names 5,000, 15,000'
      ' and 30,000 s.', 'arm\tt\talive\tp0\tp1\tp2\tp3\trim share\tin 0.15-0.40\tcols'
      '\tuniform expectation\tratio\tin 0.85-1.05']
RIM = {}
COLSOK = {}
for a in ARMS:
    ts = [t for t in (5000, 15000) if t <= LAST[a]] + [LAST[a]]
    for t in sorted(set(ts)):
        x = at(a, t)
        al = num(x['alive'])
        p3 = num(x['p3'])
        share = p3 / al
        c = num(x['cols'])
        exp = LIVE_COLS * (1.0 - math.exp(-al / float(LIVE_COLS)))
        rm.append('%s\t%d\t%d\t%d\t%d\t%d\t%d\t%.4f\t%s\t%d\t%.0f\t%.3f\t%s' % (
            a, t, al, num(x['p0']), num(x['p1']), num(x['p2']), p3, share,
            'yes' if 0.15 <= share <= 0.40 else 'NO', c, exp, c / exp,
            'yes' if 0.85 <= c / exp <= 1.05 else 'NO'))
        RIM.setdefault(a, []).append((t, share))
        COLSOK.setdefault(a, []).append((t, c / exp))
write('rim.tsv', rm)

# --------------------------------------------------------------------------- L8, the throws
ex = ['## L8  nothing throws: divergences per million jointed body-seconds. The jointed'
      ' body-seconds are the `jointed` column integrated over the run at its 100 s sampling.',
      'arm\tdiverged at last sample\tjointed body-seconds\tinherited-jointed body-seconds'
      '\tper million jointed\tin 0-10\tdumps on disk']
THROW = {}
for a in ARMS:
    r = DATA[a][0]
    jbs = sum(num(x['jointed']) * 100.0 for x in r)
    ibs = sum(num(x['jnt inh']) * 100.0 for x in r)
    dv = num(r[-1]['diverged'])
    per = dv / (jbs / 1e6) if jbs else float('nan')
    d = os.path.join(run_dir(a), 'diverged')
    nd = len(glob.glob(os.path.join(d, '*'))) if os.path.isdir(d) else 0
    ex.append('%s\t%d\t%.0f\t%.0f\t%.2f\t%s\t%d' % (
        a, dv, jbs, ibs, per, 'yes' if 0 <= per <= 10 else 'NO', nd))
    THROW[a] = (dv, jbs, per)
write('exposure.tsv', ex)

# --------------------------------------------------------------------------- L9, the pace
pc = ['## L9  the pace: wall seconds per 1,000 simulated seconds per 1,000 living bodies.'
      ' No seed has a footer — stop-arm.ps1 writes the manifest and kills the process, so the'
      ' report\'s "Ended:" line was never written. The wall is therefore taken two ways: the'
      ' manifest window (startedAt to stoppedAt, which includes the idle minutes after the'
      ' last sample) and the last stats row\'s cumulative wallTotalMs, which is the sim loop'
      ' itself. Round 39\'s three-arm seeds read 903.9 / 903.3 / 860.1 (r39-read/pace.tsv).',
      'arm\tstartedAt\tstoppedAt\tmanifest wall s\twallTotalMs s\tsimulated s\tmean alive'
      '\ts/1000 sim s (manifest)\tper 1000 bodies (manifest)\tper 1000 bodies (wallTotal)'
      '\tin 600-1300\tphysics %\tworld %\tharness %\twriters %\tworker']
PACE = {}
for a in ARMS:
    m = MAN[a]
    t0 = iso(m['startedAt'])
    t1 = iso(m['stoppedAt'])
    wall = (t1 - t0).total_seconds()
    s = ST[a]
    wt = s[-1]['wallTotalMs'] / 1000.0
    sim = float(LAST[a])
    mean_alive = sum(num(x['alive']) for x in DATA[a][0]) / len(DATA[a][0])
    p1 = wall / (sim / 1000.0) / (mean_alive / 1000.0)
    p2 = wt / (sim / 1000.0) / (mean_alive / 1000.0)
    tot = s[-1]['wallTotalMs'] or 1
    pc.append('%s\t%s\t%s\t%.0f\t%.0f\t%d\t%.0f\t%.1f\t%.1f\t%.1f\t%s\t%.0f%%\t%.0f%%'
              '\t%.0f%%\t%.1f%%\t%s' % (
                  a, m['startedAt'][:19], m['stoppedAt'][:19], wall, wt, sim, mean_alive,
                  wall / (sim / 1000.0), p1, p2,
                  'yes' if 600 <= p2 <= 1300 else 'NO',
                  100.0 * s[-1]['wallPhysicsMs'] / tot, 100.0 * s[-1]['wallWorldMs'] / tot,
                  100.0 * s[-1]['wallHarnessMs'] / tot, 100.0 * s[-1]['wallWritersMs'] / tot,
                  os.path.basename(m['source']['workerPath'])))
    PACE[a] = (p1, p2)
write('pace.tsv', pc)

# --------------------------------------------------------------------------- the summary
def db(a, t, guild, col):
    """A column of pos_analyse.py's depth table: 4 median, 5 q1, 6 q3, 3 n."""
    c = DB.get((a, t, guild))
    if not c or c[3] in ('0', '-'):
        return None
    try:
        return float(c[col])
    except ValueError:
        return None


su = ['## Round 40, three seeds of five, stopped by the owner at 16,400 / 12,200 / 14,300 s'
      ' (D098). Every prediction of 0106 per seed, with the number, the bar and the verdict.'
      ' A bar that names a second the seed never reached is NOT READABLE; the last-sample'
      ' number is carried beside it as a reading and never as a verdict. The round-level'
      ' "4 of 5" cannot be decided by three seeds at all: the per-seed column is what this'
      ' file reports.',
      'prediction\tbar\tarm\tnumber\tverdict\tfile']

def row(pred, bar, a, number, v, f):
    su.append('%s\t%s\t%s\t%s\t%s\t%s' % (pred, bar, a, number, v, f))


# L1
for a in ARMS:
    for t, label in [(15000, '15,000 s'), (LAST[a], 'last sample %d s' % LAST[a])]:
        med, q3 = db(a, t, 'leaf', 4), db(a, t, 'leaf', 6)
        if med is None:
            row('L1', 'leaf median < 10 m and q3 depth < 15 m at %s' % label, a, '-',
                'not readable (stopped at %d s)' % LAST[a], 'depth-band.tsv')
            continue
        ok = med < 10.0 and q3 < 15.0
        if t == 15000:
            v = verdict(ok)
        else:
            v = ('not readable (the bar names 30,000 s and no seed reached it); reading at'
                 ' the last sample: %s the bar' % ('meets' if ok else 'misses'))
        row('L1', 'leaf median < 10 m and q3 depth < 15 m at %s' % label, a,
            'median %.2f m, q3 %.2f m, n %s' % (med, q3, DB[(a, t, 'leaf')][3]), v,
            'depth-band.tsv')
# L2
for a in ARMS:
    r = DATA[a][0]
    x15 = at(a, 15000)
    if x15:
        v = num(x15['shade %'])
        row('L2', 'shade % above 3.5 at 15,000 s', a, '%.1f%%' % v,
            verdict(v > R39_SHADE_15000_MAX), 'shade.tsv')
    else:
        row('L2', 'shade % above 3.5 at 15,000 s', a, 'last sample %d s: %.1f%%'
            % (LAST[a], num(r[-1]['shade %'])),
            'not readable (stopped at %d s)' % LAST[a], 'shade.tsv')
    v = num(r[-1]['shade %'])
    mx = max(num(x['shade %']) for x in r)
    row('L2', 'shade % above 3.1 at 30,000 s', a, 'last sample %d s: %.1f%%; run max %.1f%%'
        % (LAST[a], v, mx), 'not readable (no seed reached 30,000 s); reading: %s'
        % ('above 3.1' if v > R39_SHADE_30000_MAX else 'below 3.1'), 'shade.tsv')
# L3
for a in ARMS:
    for t, label in [(15000, '15,000 s'), (LAST[a], 'last sample %d s' % LAST[a])]:
        lm, sm = db(a, t, 'leaf', 4), db(a, t, 'stomach', 4)
        n = DB.get((a, t, 'stomach'), ['', '', '', '0'])[3]
        if lm is None or sm is None:
            reason = ('not readable (stopped at %d s)' % LAST[a]) if t > LAST[a] \
                else 'not readable (no absorptive body alive at that sample)'
            row('L3', 'absorptive median at least 10 m below the leaves\' at %s' % label, a,
                'stomachs n=%s' % n, reason, 'depth-band.tsv')
            continue
        d = sm - lm
        if t == 15000:
            v = verdict(d >= 10.0)
        else:
            v = ('reading at the last sample (the bar names 15,000 s): %s'
                 % ('meets the bar' if d >= 10.0 else 'misses the bar'))
        row('L3', 'absorptive median at least 10 m below the leaves\' at %s' % label, a,
            'stomach %.2f - leaf %.2f = %+.2f m, n %s' % (sm, lm, d, n), v, 'depth-band.tsv')
# L4, from booms.py's per-seed section
for a in ARMS:
    v = '-'
    seen = False
    for line in open(os.path.join(HERE, 'booms.tsv'), encoding='utf-8'):
        if line.startswith('arm\tL4'):
            seen = True
            continue
        if seen and line.startswith(a + '\t'):
            v = line.rstrip('\n').split('\t')[1]
            break
    r = DATA[a][0]
    top = max(r, key=lambda x: num(x['inherit']))
    row('L4', 'the first boom above 550 inherited eaters busts later than 2,600 s after its'
        ' peak, or not before the budget', a,
        'inherit run maximum %d at %d s; last sample %d s' % (
            num(top['inherit']), top['t'], LAST[a]), v, 'booms.tsv')
# L5
for a in ARMS:
    row('L5', 'matterLocked / matterStanding below 0.70 at 30,000 s', a,
        'last sample %d s: %.4f' % (LAST[a], MATTER[a]),
        'not readable (no seed reached 30,000 s); reading: %s'
        % ('below 0.70' if MATTER[a] < 0.70 else 'at or above 0.70'), 'matter.tsv')
# L6
for a in ARMS:
    last = DATA[a][0][-1]
    row('L6', 'alive at 30,000 s between 1,680 and 3,600', a,
        'last sample %d s: %d' % (LAST[a], num(last['alive'])),
        'not readable (no seed reached 30,000 s); reading: %s'
        % ('in band' if 1680 <= num(last['alive']) <= 3600 else 'outside band'),
        'timeline-every1000.tsv')
    b = num(at(a, 1000)['births'])
    row('L6', 'births by 1,000 s at least 40', a, '%d' % b, verdict(b >= 40),
        'timeline-every1000.tsv')
# L7
for a in ARMS:
    if 15000 > LAST[a]:
        row('L7', 'rim quarter p3/alive 0.15 to 0.40 at 15,000 s', a, '-',
            'not readable (stopped at %d s)' % LAST[a], 'rim.tsv')
        row('L7', 'cols within 0.85 to 1.05 of 1 - e^(-n/2211) at 15,000 s', a, '-',
            'not readable (stopped at %d s)' % LAST[a], 'rim.tsv')
    for t, share in RIM[a]:
        label = '%d s' % t + (' (last sample)' if t == LAST[a] else '')
        readable = t in (5000, 15000) and t <= LAST[a]
        row('L7', 'rim quarter p3/alive 0.15 to 0.40 at %s' % label, a, '%.4f' % share,
            verdict(0.15 <= share <= 0.40) if readable
            else 'not readable (30,000 s bar); reading: %s'
                 % ('in band' if 0.15 <= share <= 0.40 else 'outside band'), 'rim.tsv')
    for t, ratio in COLSOK[a]:
        label = '%d s' % t + (' (last sample)' if t == LAST[a] else '')
        readable = t in (5000, 15000) and t <= LAST[a]
        row('L7', 'cols within 0.85 to 1.05 of 1 - e^(-n/2211) at %s' % label, a,
            '%.3f' % ratio, verdict(0.85 <= ratio <= 1.05) if readable
            else 'not readable (30,000 s bar); reading: %s'
                 % ('in band' if 0.85 <= ratio <= 1.05 else 'outside band'), 'rim.tsv')
# L8
for a in ARMS:
    dv, jbs, per = THROW[a]
    row('L8', 'diverged 0 to 10 per million jointed body-seconds', a,
        '%d over %.2f million jointed body-seconds = %.2f' % (dv, jbs / 1e6, per),
        verdict(0 <= per <= 10), 'exposure.tsv')
# L9
for a in ARMS:
    p1, p2 = PACE[a]
    row('L9', 'wall s per 1,000 simulated s per 1,000 bodies between 600 and 1,300', a,
        '%.0f (sim loop, wallTotalMs); %.0f (manifest window)' % (p2, p1),
        verdict(600 <= p2 <= 1300), 'pace.tsv')
write('summary.tsv', su)

# --------------------------------------------------------------------------- the extras
xt = ['## Recorded and not predicted.', 'item\tr40-s1\tr40-s2\tr40-s3']


def series(name, f):
    xt.append(name + '\t' + '\t'.join(str(f(a)) for a in ARMS))


series('last sample s', lambda a: LAST[a])
series('alive at the last sample', lambda a: int(num(DATA[a][0][-1]['alive'])))
series('alive, run maximum (at)', lambda a: '%d (%d s)' % (
    max(num(x['alive']) for x in DATA[a][0]),
    max(DATA[a][0], key=lambda x: num(x['alive']))['t']))
series('births total', lambda a: int(num(DATA[a][0][-1]['births'])))
series('photo inh at the last sample', lambda a: int(num(DATA[a][0][-1]['photo inh'])))
series('inherit (absorptive) at the last sample',
       lambda a: int(num(DATA[a][0][-1]['inherit'])))
series('inherit, run maximum (at)', lambda a: '%d (%d s)' % (
    max(num(x['inherit']) for x in DATA[a][0]),
    max(DATA[a][0], key=lambda x: num(x['inherit']))['t']))
series('jnt inh at the last sample', lambda a: int(num(DATA[a][0][-1]['jnt inh'])))
series('jnt inh, run maximum (at)', lambda a: '%d (%d s)' % (
    max(num(x['jnt inh']) for x in DATA[a][0]),
    max(DATA[a][0], key=lambda x: num(x['jnt inh']))['t']))
series('stillb at the last sample', lambda a: int(num(DATA[a][0][-1]['stillb'])))
series('crowded at the last sample', lambda a: int(num(DATA[a][0][-1]['crowded'])))
series('corpses at the last sample', lambda a: int(num(DATA[a][0][-1]['corpses'])))
series('floor J at the last sample', lambda a: num(DATA[a][0][-1]['floor J']))
series('det cv at the last sample', lambda a: num(DATA[a][0][-1]['det cv']))
series('mat cv at the last sample', lambda a: num(DATA[a][0][-1]['mat cv']))
series('depth m (report mean) at the last sample',
       lambda a: num(DATA[a][0][-1]['depth m']))
series('mean m/s at the last sample', lambda a: num(DATA[a][0][-1]['mean m/s']))
series('adult scale / invest / brood / body frac at the last sample', lambda a: '%s / %s / %s / %s' % (
    DATA[a][0][-1]['adult scale'], DATA[a][0][-1]['invest'], DATA[a][0][-1]['brood'],
    DATA[a][0][-1]['body frac']))
series('wall split at the last sample (physics/world/harness/writers)', lambda a: (
    '%.0f%% / %.0f%% / %.0f%% / %.1f%%' % (
        100.0 * ST[a][-1]['wallPhysicsMs'] / ST[a][-1]['wallTotalMs'],
        100.0 * ST[a][-1]['wallWorldMs'] / ST[a][-1]['wallTotalMs'],
        100.0 * ST[a][-1]['wallHarnessMs'] / ST[a][-1]['wallTotalMs'],
        100.0 * ST[a][-1]['wallWritersMs'] / ST[a][-1]['wallTotalMs'])))
write('extras.tsv', xt)

print('wrote rules.tsv, timeline-every1000.tsv, shade.tsv, matter.tsv, rim.tsv,'
      ' exposure.tsv, pace.tsv, summary.tsv, extras.tsv')
