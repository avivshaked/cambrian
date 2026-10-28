"""D235's nose bench, read: the batches founders-batch.py made and patch-run.ps1 swam, one batch for N0 and one for each nose setting.

    python -I scripts/reads/nose-bench-read.py <batch dir>... [--seconds 1800]
    python -I scripts/reads/nose-bench-read.py n0=<dir> n1-a100-z050=<dir> ...

A batch is the folder founders-batch.py wrote with --lineage: brains.json (the label of each exam control by its index), lineage.json (pool
line to founder lineage), req/<stem>.json (a job's episode, tank and brain) and ep/<stem>/ holding what the job wrote. A batch is named
by its folder's name or by a `label=dir` argument, and the label must contain `n0` or `n1-a<area>-z<noise>`, for instance
`nose-bench-n1-a200-z050` (a the snout's area factor in per cent of the reference snout, z the leaf noise factor in per cent of the default).

What it reads in each episode folder, and what it does when a file lacks the column:
  patches.jsonl   a row a body: episode, brain, tank, slot, pool, alive (seconds alive, to the episode's end for a survivor), died, and,
                  in a tank that switches its leaves only, eaten (joules kept from bites on leaves).
  result.json     a member a slot: pool, net (the holdings' change in joules, NOT the reward), survived. A build that also writes a member's
                  eaten, work, upkeep, handling and diedAt (RestepMemberOutcome has all five) lets the read score the energy return.
  episode.json    with frames.bin, only where the batch was swum without --patches: the alive flag of each member by frame gives the
                  seconds alive where there is no patches.jsonl.
The columns it will use for the two measures the marks need:
  eaten           result.json's member "eaten", else patches.jsonl's "eaten"; "-" where neither is written.
  energy return   as EnergyReturn.Of computes an episode with chargeDeathToEnd: eaten over (standing + work + handling), where standing is
                  upkeep, and for a body that died upkeep times seconds over diedAt. From result.json's members only. "-" where the build
                  does not write them. A body whose costs are not above 0 has no ratio and is left out of the mean.
Neither is the `net` column, which is joules gained or lost and is not what the recipe's reward scores; net is printed beside them.

Per batch and brain it prints: bodies, the median seconds alive (pooled over bodies; and the mean of the ten lineage medians), the mean
seconds alive, eaten, and the energy return, each of the last three as the mean of the lineage means with its standard error over
lineages. Then the three marks, each with its numbers and PASS or FAIL (or NO DATA where a column is missing):
  life     the median seconds alive under own is at least 600 s, in N0 and in each N1 a100 batch.
  size     at each noise z, eaten under own at a200 minus at a050, paired by lineage (a lineage's mean of the differences over the bodies
           the two batches share by episode, tank and slot; a lineage the batches do not share a body in falls back to the difference of
           its means, and the read says how often); mean and SE over lineages. The chosen noise is the largest z at which the difference is
           at least 2 SE, or "none".
  parity   at the chosen noise, N1 a100 under own against N0 under own, on the energy return, paired the same way: pass when N1's mean is at
           least N0's minus 2 SE of the difference. With --parity-at Z, at noise Z instead (0 for a nose that reads its field exactly,
           which has no size pair).
And, not marks, the knockout's cost under N0 and under each N1 a100: own minus knockout on eaten and energy return, paired the same way.
Reads only, and writes nothing but its printout. Python 3 standard library.
"""
import argparse
import array
import json
import math
import os
import re
import statistics as st
import sys

LIFE_FLOOR = 600.0
SE_BAR = 2.0

ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
ap.add_argument('batches', nargs='+', help='batch dirs, or label=dir')
ap.add_argument('--seconds', type=float, default=1800.0, help="the episode's length, for a body that died (default 1800)")
ap.add_argument('--parity-at', type=int, default=None, metavar='Z',
                help='the parity mark at this noise (z in per cent, 0 for none) instead of the chosen one, for a noise with no size pair')
args = ap.parse_args()

NAN = float('nan')


def isnum(x):
    return x is not None and not (isinstance(x, float) and math.isnan(x))


def mean_se(xs):
    xs = [x for x in xs if isnum(x)]
    if not xs:
        return None, None, 0
    m = st.mean(xs)
    se = st.stdev(xs) / math.sqrt(len(xs)) if len(xs) > 1 else None
    return m, se, len(xs)


def fmt(m, se=None, nd=1):
    if m is None:
        return '-'
    return ('%.*f' % (nd, m)) if se is None else ('%.*f +- %.*f' % (nd, m, nd, se))


# ---------------------------------------------------------------- one batch
def energy_return(m, seconds):
    """EnergyReturn.Of for one member of result.json, with chargeDeathToEnd; None where the build does not write the columns."""
    if not all(k in m for k in ('eaten', 'work', 'upkeep', 'handling')):
        return None
    eaten, work, upkeep, handling, died = m['eaten'], m['work'], m['upkeep'], m['handling'], m.get('diedAt')
    if not all(isnum(x) for x in (eaten, work, upkeep, handling)):
        return None
    if not isnum(died):
        standing = upkeep
    elif upkeep > 0 and died > 0:
        standing = upkeep * (seconds / died)
    else:
        return None
    spent = standing + work + handling
    return eaten / spent if spent > 0 else None


def frames_alive(folder, slots):
    """Seconds alive of each slot from frames.bin and episode.json (a batch swum without --patches), or {} where there are none."""
    epj, fb = os.path.join(folder, 'episode.json'), os.path.join(folder, 'frames.bin')
    if not (os.path.exists(epj) and os.path.exists(fb)):
        return {}
    ep = json.load(open(epj, encoding='utf-8'))
    names = [c['name'] for c in ep['channels']]
    width = len(names)
    a = array.array('f')
    with open(fb, 'rb') as f:
        a.frombytes(f.read())
    if sys.byteorder != 'little':
        a.byteswap()
    rate = float(ep['sampleRate'])
    out = {}
    for s in slots:
        name = 'm%d.alive' % s
        if name in names:
            col = a[names.index(name)::width]
            out[s] = sum(1 for v in col if v == 1) / rate
    return out


def read_batch(label, path):
    """-> {brain label: [body dict]}, a body {key, pool, lineage, alive, eaten, er, net, died}."""
    brains = {i: l for i, l in enumerate(json.load(open(os.path.join(path, 'brains.json'), encoding='utf-8')))}
    lin_path = os.path.join(path, 'lineage.json')
    lineage = {int(k): v for k, v in json.load(open(lin_path, encoding='utf-8')).items()} if os.path.exists(lin_path) else {}
    out, folders, missing = {}, 0, []
    ep_dir = os.path.join(path, 'ep')
    for stem in sorted(os.listdir(ep_dir)) if os.path.isdir(ep_dir) else []:
        folder = os.path.join(ep_dir, stem)
        if not os.path.isdir(folder):
            continue
        res_path = os.path.join(folder, 'result.json')
        if not os.path.exists(res_path):
            missing.append(stem)
            continue
        folders += 1
        req = json.load(open(os.path.join(path, 'req', stem + '.json'), encoding='utf-8'))
        brain = brains[req['brain']]
        res = json.load(open(res_path, encoding='utf-8'))
        members = {m['slot']: m for m in res['members']}
        rows = {}
        pat = os.path.join(folder, 'patches.jsonl')
        if os.path.exists(pat):
            for l in open(pat, encoding='utf-8'):
                if l.strip():
                    r = json.loads(l)
                    rows[r['slot']] = r
        fa = {} if rows else frames_alive(folder, members.keys())
        for slot, m in sorted(members.items()):
            if not m.get('landed', True):
                continue
            r = rows.get(slot, {})
            alive = r.get('alive', fa.get(slot))
            eaten = m['eaten'] if isnum(m.get('eaten')) else r.get('eaten')
            net = float(m['net']) if isnum(m.get('net')) and m['net'] is not None else None
            seconds = float(m['seconds']) if isnum(m.get('seconds')) else args.seconds
            out.setdefault(brain, []).append({
                'key': (req['episode'], req['tank'], slot), 'pool': m['pool'], 'lineage': lineage.get(m['pool'], 'line %d' % m['pool']),
                'alive': float(alive) if isnum(alive) else None, 'eaten': float(eaten) if isnum(eaten) else None,
                'er': energy_return(m, seconds), 'net': net,
            })
    return out, folders, missing


def parse_name(label):
    s = label.lower()
    m = re.search(r'n1[-_]a(\d+)[-_]z(\d+)', s)
    if m:
        return ('n1', int(m.group(1)), int(m.group(2)))
    if re.search(r'n0', s):
        return ('n0', None, None)
    raise SystemExit('cannot tell what batch %r is: its name needs n0 or n1-a<area>-z<noise>; give it as label=dir' % label)


batches = {}  # ('n0',None,None) or ('n1', a, z) -> {brain: [bodies]}
for arg in args.batches:
    label, path = arg.split('=', 1) if '=' in arg and not os.path.isdir(arg) else (os.path.basename(os.path.normpath(arg)), arg)
    ident = parse_name(label)
    if ident in batches:
        raise SystemExit('two batches are %s' % (ident,))
    bodies, folders, missing = read_batch(label, path)
    batches[ident] = bodies
    print('%s: %d episode folders read%s' % (label, folders, (', %d without result.json (%s ...)' % (len(missing), missing[0])) if missing else ''))


def title(ident):
    return 'N0' if ident[0] == 'n0' else 'N1 a%03d z%03d' % (ident[1], ident[2])


def brain_rows(ident, kind):
    """The bodies of the batch under the exam control whose label starts with kind (own, brainless, knockout)."""
    for label, rows in batches[ident].items():
        if label.startswith(kind):
            return rows
    return []


def lineage_means(rows, field):
    by = {}
    for r in rows:
        if isnum(r[field]):
            by.setdefault(r['lineage'], []).append(r[field])
    return {k: st.mean(v) for k, v in by.items()}


def lineage_medians(rows, field):
    by = {}
    for r in rows:
        if isnum(r[field]):
            by.setdefault(r['lineage'], []).append(r[field])
    return {k: st.median(v) for k, v in by.items()}


# ---------------------------------------------------------------- the per batch and brain table
print('\nBy batch and brain (lineage means: the mean of the lineages\' own means, with its SE over lineages)')
print('  %-15s %-34s %6s %10s %16s %16s %18s %18s %14s' % (
    'batch', 'brain', 'bodies', 'alive med', 'alive med (lin)', 'alive mean', 'eaten (J)', 'energy return', 'net (J)'))
for ident in sorted(batches, key=lambda i: (i[0] != 'n0', i[1] or 0, -(i[2] or 0))):
    for label in batches[ident]:
        rows = batches[ident][label]
        alive = [r['alive'] for r in rows if isnum(r['alive'])]
        med = st.median(alive) if alive else None
        med_lin = mean_se(lineage_medians(rows, 'alive').values())
        a = mean_se(lineage_means(rows, 'alive').values())
        e = mean_se(lineage_means(rows, 'eaten').values())
        er = mean_se(lineage_means(rows, 'er').values())
        n = mean_se(lineage_means(rows, 'net').values())
        print('  %-15s %-34s %6d %10s %16s %16s %18s %18s %14s' % (
            title(ident), label[:34], len(rows), fmt(med, None, 0), fmt(med_lin[0], med_lin[1], 0), fmt(a[0], a[1], 0),
            fmt(e[0], e[1], 1), fmt(er[0], er[1], 3), fmt(n[0], n[1], 0)))
    bad = [(l, sum(1 for r in rs if r['eaten'] is None), len(rs)) for l, rs in batches[ident].items()]
    gaps = ['%s: no eaten for %d of %d' % b for b in bad if b[1]]
    if gaps:
        print('  %-15s (%s)' % (title(ident), '; '.join(gaps)))


# ---------------------------------------------------------------- pairing
def paired(a_rows, b_rows, field):
    """a minus b on field, paired by lineage: -> dict(mean, se, lineages, matched, fallback) or None where nothing can be paired.

    A lineage's difference is the mean, over the bodies the batches share by (episode, tank, slot, pool), of a - b; a lineage with no shared
    body falls back to the difference of its two means (counted in fallback). The mean and SE are over the lineages."""
    ka = {(r['key'], r['pool']): r for r in a_rows if isnum(r[field])}
    kb = {(r['key'], r['pool']): r for r in b_rows if isnum(r[field])}
    shared = sorted(set(ka) & set(kb))
    by = {}
    for k in shared:
        by.setdefault(ka[k]['lineage'], []).append(ka[k][field] - kb[k][field])
    diffs = {l: st.mean(v) for l, v in by.items()}
    fallback = 0
    ma, mb = lineage_means(a_rows, field), lineage_means(b_rows, field)
    for l in sorted(set(ma) & set(mb)):
        if l not in diffs:
            diffs[l] = ma[l] - mb[l]
            fallback += 1
    if not diffs:
        return None
    m, se, n = mean_se(diffs.values())
    return {'mean': m, 'se': se, 'lineages': n, 'matched': len(shared), 'of': min(len(ka), len(kb)), 'fallback': fallback}


def show(p, nd=1):
    if p is None:
        return 'no data'
    sigma = ('%.1f SE' % (p['mean'] / p['se'])) if p['se'] else 'SE n/a'
    return '%+.*f +- %s (%s) over %d lineages; %d of %d bodies paired by episode, tank and slot%s' % (
        nd, p['mean'], ('%.*f' % (nd, p['se'])) if p['se'] is not None else 'n/a', sigma, p['lineages'], p['matched'], p['of'],
        (', %d lineages by their means' % p['fallback']) if p['fallback'] else '')


verdict = {}


def mark(name, status, lines):
    verdict[name] = status
    print('\n%s: %s' % (name, status))
    for l in lines:
        print('  ' + l)


# ---------------------------------------------------------------- life
lines, results = [], []
needed = [i for i in sorted(batches, key=lambda i: (i[0] != 'n0', i[1] or 0, -(i[2] or 0))) if i[0] == 'n0' or i[1] == 100]
for ident in needed:
    alive = [r['alive'] for r in brain_rows(ident, 'own') if isnum(r['alive'])]
    if not alive:
        lines.append('%s: no seconds alive under own' % title(ident))
        results.append(None)
        continue
    med = st.median(alive)
    ok = med >= LIFE_FLOOR
    results.append(ok)
    lines.append('%s: median %.0f s alive under own over %d bodies, floor %.0f s: %s' % (title(ident), med, len(alive), LIFE_FLOOR, 'PASS' if ok else 'FAIL'))
mark('life (median seconds alive under own >= %.0f s, in N0 and each N1 a100)' % LIFE_FLOOR,
     'NO DATA' if not results or None in results else ('PASS' if all(results) else 'FAIL'), lines)
if ('n0', None, None) not in batches:
    print('  (no N0 batch given)')

# ---------------------------------------------------------------- size
lines, chosen, anydata = [], None, False
zs = sorted({i[2] for i in batches if i[0] == 'n1'}, reverse=True)
for z in zs:
    hi, lo = ('n1', 200, z), ('n1', 50, z)
    if hi not in batches or lo not in batches:
        lines.append('z%03d: needs both a200 and a050 batches' % z)
        continue
    p = paired(brain_rows(hi, 'own'), brain_rows(lo, 'own'), 'eaten')
    if p is None or p['se'] is None:
        lines.append('z%03d: eaten under own, a200 - a050: %s' % (z, show(p)))
        continue
    anydata = True
    ok = p['mean'] > 0 and p['mean'] >= SE_BAR * p['se']
    lines.append('z%03d: eaten under own, a200 - a050: %s: %s' % (z, show(p), 'PASS' if ok else 'FAIL'))
    if ok and chosen is None:
        chosen = z
lines.append('chosen noise (the largest z at which the difference is at least %g SE): %s' % (SE_BAR, 'z%03d' % chosen if chosen else 'none'))
mark('size (eaten under own at a200 minus at a050, at least %g SE)' % SE_BAR,
     'NO DATA' if not anydata else ('PASS' if chosen else 'FAIL'), lines)

# ---------------------------------------------------------------- parity
lines = []
if args.parity_at is not None:
    chosen, anydata = args.parity_at, True
    lines.append('at z%03d, as --parity-at names, not the chosen noise' % chosen)
if not anydata:
    status = 'NO DATA'
    lines.append('no size result, so no chosen noise')
elif chosen is None:
    status = 'FAIL'
    lines.append('no noise passed the size mark, so there is none to compare at')
else:
    n1, n0 = ('n1', 100, chosen), ('n0', None, None)
    if n1 not in batches or n0 not in batches:
        status = 'NO DATA'
        lines.append('needs the N0 batch and N1 a100 z%03d' % chosen)
    else:
        p = paired(brain_rows(n1, 'own'), brain_rows(n0, 'own'), 'er')
        if p is None or p['se'] is None:
            status = 'NO DATA'
            lines.append('energy return under own, N1 a100 z%03d - N0: %s (the energy return needs eaten, work, upkeep, handling and diedAt '
                         'in result.json)' % (chosen, show(p, 3)))
        else:
            ok = p['mean'] >= -SE_BAR * p['se']
            status = 'PASS' if ok else 'FAIL'
            lines.append('energy return under own, N1 a100 z%03d - N0: %s; the bar is no lower than -%g SE (%.3f): %s' % (
                chosen, show(p, 3), SE_BAR, -SE_BAR * p['se'], status))
mark('parity (N1 a100 at the chosen noise against N0 under own, energy return, within %g SE)' % SE_BAR, status, lines)

# ---------------------------------------------------------------- the knockout's cost, not a mark
print('\nThe knockout\'s cost (not a mark): own minus knockout, paired as above')
for ident in sorted([i for i in batches if i[0] == 'n0' or i[1] == 100], key=lambda i: (i[0] != 'n0', -(i[2] or 0))):
    own, ko = brain_rows(ident, 'own'), brain_rows(ident, 'knockout')
    if not ko:
        print('  %s: no knockout brain in the batch' % title(ident))
        continue
    for field, nd, name in (('eaten', 1, 'eaten (J)'), ('er', 3, 'energy return')):
        print('  %-15s %-14s %s' % (title(ident), name, show(paired(own, ko, field), nd)))

print('\nmarks: ' + '; '.join('%s %s' % (k.split(' (')[0], v) for k, v in verdict.items()))
