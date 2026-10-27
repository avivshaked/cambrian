"""D236's travel measures, read: the episode folders of a patch-batch.py batch of a switching recipe, by brain.

    python scripts/reads/nursery-travel-read.py <out dir> [<run folder>]
    python scripts/reads/nursery-travel-read.py <out dir>          (a founders-batch.py batch, which has no run)

The batch is the one patch-read.py reads, made with the brains named: for D236's pass bar that is own, brainless and frozen
(patch-batch.py <run> <wt> <recipe> <out> --brains own,brainless,frozen). Each episode folder's patches.jsonl holds, for a switching
tank, the travel columns PatchWatcher.Row writes after today's: arrivals and follow (a value a switch, null where there is none),
aliveAtSwitch, firstPatch, laterShare, eaten and eatenByOpening. A row without them is a tank whose patches do not switch and is left
out, with its count said. It reads only, and writes nothing but its printout.

By brain it prints
  - the time from a switch to arrival: the median and quartiles of a Kaplan-Meier curve over every (body, switch) the body was alive at,
    a switch the body never arrived at censored at its follow-up (the seconds to the body's last alive sample), so a quartile the curve
    never reaches is printed as "-";
  - the share of the body's eating taken in openings that opened after its first patch;
  - crossings (transits between openings) a body.
With <out dir>/lineage.json (a map from pool line to the founder lineage that genome descends from) it also prints the means and standard
errors by lineage, and the comparisons of D236's pass bar, own against brainless and against frozen, in standard errors of the lineage
means. The batch does not record lineage as patch-batch.py writes it; without that file the lineage parts are not printed (see the note
the script prints).

A body's time to arrival, for the lineage means, is the mean over its switches of the arrival time, a switch never arrived at counted at
its follow-up. That is a lower bound on the time, so it flatters a brain that never arrives; the arrival rate is printed beside it.
"""
import json
import math
import os
import statistics as st
import sys

SWITCH_COLUMNS = ('arrivals', 'follow', 'aliveAtSwitch', 'laterShare', 'eaten')

args = [a for a in sys.argv[1:]]
if not args or len(args) > 2:
    raise SystemExit(__doc__)
out, run = args[0], (args[1] if len(args) > 1 else None)

# ---------------------------------------------------------------- the brains' labels, as patch-read.py has them
label = {}
if run is None:
    label = dict(enumerate(json.load(open(os.path.join(out, 'brains.json'), encoding='utf-8'))))
    kind = 'founders'
else:
    kind = 'exam' if os.path.exists(os.path.join(run, 'tank-exam-draws.jsonl')) else 'bench'
    for l in open(os.path.join(run, 'tank-%s-draws.jsonl' % kind), encoding='utf-8'):
        if l.strip():
            r = json.loads(l)
            label[r['b']] = r['brain']

lineage = None
lin_path = os.path.join(out, 'lineage.json')
if os.path.exists(lin_path):
    lineage = {int(k): v for k, v in json.load(open(lin_path, encoding='utf-8')).items()}

# ---------------------------------------------------------------- the rows
rows, plain, missing = [], 0, []
ep = os.path.join(out, 'ep')
for stem in sorted(os.listdir(ep)) if os.path.isdir(ep) else []:
    folder = os.path.join(ep, stem)
    if not os.path.isdir(folder):
        continue  # the job's own .log and .err lie beside its folder
    pat = os.path.join(folder, 'patches.jsonl')
    if not os.path.exists(pat):
        missing.append(stem)
        continue
    for l in open(pat, encoding='utf-8'):
        if not l.strip():
            continue
        r = json.loads(l)
        if not all(c in r for c in SWITCH_COLUMNS):
            plain += 1
            continue
        r['brainLabel'] = label.get(r['brain'], 'brain %s' % r['brain'])
        rows.append(r)

print('%s %s: %d switching rows read from %d episodes, %d rows of tanks that do not switch left out, %d episode folders without patches.jsonl' %
      (os.path.basename(os.path.normpath(run or out)), kind, len(rows), len(set((r['episode'], r['brain'], r['tank']) for r in rows)), plain, len(missing)))
if not rows:
    raise SystemExit('nothing to read: no row carries the travel columns (is this a batch of a switching recipe, on a build with piece 4?)')


# ---------------------------------------------------------------- the measures of one body
def pairs(r):
    """(seconds, arrived) for each switch the body was alive at: the arrival, else the follow-up, which censors it."""
    ps = []
    for a, f in zip(r['arrivals'], r['follow']):
        if f is None:
            continue  # dead at the switch
        ps.append((a, True) if a is not None else (f, False))
    return ps


def body_arrival(r):
    """The body's mean time to arrival over its switches, a switch never arrived at counted at its follow-up: a lower bound."""
    ps = pairs(r)
    return st.mean(t for t, _ in ps) if ps else None


def body_rate(r):
    ps = pairs(r)
    return sum(1 for _, ok in ps if ok) / len(ps) if ps else None


def body_later(r):
    return r['laterShare']


def body_crossings(r):
    return r['transits']


def km(ps):
    """The Kaplan-Meier curve of (seconds, arrived) pairs: [(t, share not yet arrived)] at each arrival time. Arrivals count before censorings at a tie."""
    ps = sorted(ps, key=lambda p: (p[0], not p[1]))
    at_risk, s, curve, i = len(ps), 1.0, [], 0
    while i < len(ps):
        t = ps[i][0]
        d = c = 0
        while i < len(ps) and ps[i][0] == t:
            if ps[i][1]:
                d += 1
            else:
                c += 1
            i += 1
        if d:
            s *= 1.0 - d / at_risk
            curve.append((t, s))
        at_risk -= d + c
    return curve


def quantile(curve, q):
    """The first time the curve falls to 1 - q or below; None if it never does (the quantile lies beyond what was followed)."""
    for t, s in curve:
        if s <= 1.0 - q + 1e-12:
            return t
    return None


def fmt(x, nd=0, unit=''):
    return '-' if x is None else ('%.*f%s' % (nd, x, unit))


def mean_of(xs):
    xs = [x for x in xs if x is not None]
    return st.mean(xs) if xs else None


def se_of(xs):
    xs = [x for x in xs if x is not None]
    return st.stdev(xs) / math.sqrt(len(xs)) if len(xs) > 1 else None


# ---------------------------------------------------------------- by brain
brains = sorted(set(r['brainLabel'] for r in rows))
for b in brains:
    rs = [r for r in rows if r['brainLabel'] == b]
    ps = [p for r in rs for p in pairs(r)]
    curve = km(ps)
    ate = [r for r in rs if r['laterShare'] is not None]
    pooled_eaten = sum(r['eaten'] for r in rs)
    pooled_later = sum(r['laterShare'] * r['eaten'] for r in ate)
    switches = rs[0]['switches']
    print('\n%s: %d bodies, %d switches an episode, alive at %d (body, switch) pairs, %d arrived (%s)' %
          (b, len(rs), switches, len(ps), sum(1 for _, ok in ps if ok), ('%.0f%%' % (100 * sum(1 for _, ok in ps if ok) / len(ps))) if ps else '-'))
    print('  time from a switch to arrival: median %s  quartiles %s .. %s   (never-arrived censored; "-" = not reached)' %
          (fmt(quantile(curve, 0.5), 0, ' s'), fmt(quantile(curve, 0.25), 0, ' s'), fmt(quantile(curve, 0.75), 0, ' s')))
    print('  share eaten in later openings: mean %s over %d bodies that ate, of %d; pooled by joules %s of %.1f J eaten' %
          (fmt(mean_of(r['laterShare'] for r in ate), 3), len(ate), len(rs), fmt(pooled_later / pooled_eaten if pooled_eaten > 0 else None, 3), pooled_eaten))
    print('  crossings (transits between openings) a body: mean %s' % fmt(mean_of(body_crossings(r) for r in rs), 2))

# ---------------------------------------------------------------- by founder lineage
if lineage is None:
    print('\nFounder lineage is not recorded in this batch: patch-batch.py writes no lineage, and a row names only its pool line (the row\'s "pool").'
          '\nIt could be added by writing <out>/lineage.json, a map from pool line to the founder lineage that genome descends from (the line of the'
          '\ngenome file patch-batch.py copies, and the lineage from the pool\'s own record of its founders), which this script then reads.'
          '\nThe lineage means, their standard errors and the pass-bar comparisons in standard errors are not printed without it.'
          '\nThe comparisons as differences of means over bodies, with no standard error (a body is not an independent draw of a lineage):')
    own = next((b for b in brains if b.startswith('own')), None)
    for other_prefix, other_name in (('brainless', 'no brain'), ('frozen', 'frozen')):
        other = next((b for b in brains if b.startswith(other_prefix)), None)
        if own is None or other is None:
            print('  own vs %s: the batch has no %s' % (other_name, 'own brain' if own is None else other_prefix))
            continue
        for name, f, nd in (('time to arrival (s, lower bound)', body_arrival, 1), ('share eaten in later openings', body_later, 3)):
            a = mean_of(f(r) for r in rows if r['brainLabel'] == own)
            c = mean_of(f(r) for r in rows if r['brainLabel'] == other)
            print('  own vs %s, %s: %s vs %s, difference %s' %
                  (other_name, name, fmt(a, nd), fmt(c, nd), '-' if a is None or c is None else '%+.*f' % (nd, a - c)))
else:
    unknown = sorted(set(r['pool'] for r in rows if r['pool'] not in lineage))
    if unknown:
        raise SystemExit('lineage.json does not name pool line(s) %s' % unknown)
    measures = (('time to arrival (s, a never-arrived switch at its follow-up: a lower bound)', body_arrival, 1),
                ('arrival rate (share of switches arrived at)', body_rate, 2),
                ('share eaten in later openings', body_later, 3),
                ('crossings a body', body_crossings, 2))
    summary = {}  # (brain, measure name) -> (mean of lineage means, SE, lineages)
    for b in brains:
        rs = [r for r in rows if r['brainLabel'] == b]
        print('\n%s, by founder lineage:' % b)
        lines = sorted(set(lineage[r['pool']] for r in rs))
        print('  %-18s %6s %s' % ('lineage', 'bodies', ' '.join('%10s' % h for h in ('arrive (s)', 'rate', 'later', 'crossings'))))
        for ln in lines:
            ls = [r for r in rs if lineage[r['pool']] == ln]
            print('  %-18s %6d %s' % (ln, len(ls), ' '.join('%10s' % fmt(mean_of(m[1](r) for r in ls), m[2]) for m in measures)))
        for name, f, nd in measures:
            means = [mean_of(f(r) for r in rs if lineage[r['pool']] == ln) for ln in lines]
            means = [x for x in means if x is not None]
            summary[(b, name)] = (mean_of(means), se_of(means), len(means))
            print('  %s: mean of lineage means %s, SE %s over %d lineages' % (name, fmt(summary[(b, name)][0], nd + 1), fmt(summary[(b, name)][1], nd + 2), len(means)))

    print('\nD236\'s pass bar, own against brainless and against frozen (a difference in standard errors: own minus the other, over the root of the squared SEs):')

    def find(prefix):
        return next((b for b in brains if b.startswith(prefix)), None)

    own = find('own')
    for other_prefix, other_name in (('brainless', 'no brain'), ('frozen', 'frozen')):
        other = find(other_prefix)
        if own is None or other is None:
            print('  own vs %s: the batch has no %s' % (other_name, 'own brain' if own is None else other_prefix))
            continue
        for name, _, nd in measures[:3]:
            if name.startswith('arrival rate'):
                continue
            a, c = summary[(own, name)], summary[(other, name)]
            if a[0] is None or c[0] is None:
                print('  own vs %s, %s: no value' % (other_name, name))
                continue
            diff = a[0] - c[0]
            se = math.sqrt(a[1] ** 2 + c[1] ** 2) if a[1] is not None and c[1] is not None else None
            print('  own vs %s, %s: %s vs %s, difference %s%s' %
                  (other_name, name, fmt(a[0], nd + 1), fmt(c[0], nd + 1), '%+.*f' % (nd + 1, diff),
                   (' = %+.2f standard errors (SE of the difference %.*f, %d and %d lineages)' % (diff / se if se else float('nan'), nd + 2, se, a[2], c[2]))
                   if se is not None else ' (no standard error: fewer than two lineages)'))
