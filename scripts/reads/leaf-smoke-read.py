"""The leaf tank's smoke, read (logbook/specs/nursery-leaf-tank-plan.md, step 7).

    python scripts/reads/leaf-smoke-read.py <run folder> [--sense <EVOSIM_LEAF_SENSE file>]

From the run folder: each exam brain's mean eaten and Net over the exam's genomes and episodes (tank-exam.jsonl, or
tank-bench.jsonl), the search's own rows if any, and the generation's wall time from nursery.log. From the sense file
(LeafSenseProbe's lines): the strength of the leaf smell at the swimmers' roots, its 10th, 50th and 90th percentiles;
the share of samples whose gradient points within 45 degrees of the nearest leaf; the nearest leaf's distance; and the
leaves alive, first and last, per episode, which tells whether leaves die within an episode.

Standard library only.
"""
import argparse
import json
import math
import os


def pct(xs, q):
    xs = sorted(xs)
    if not xs:
        return float('nan')
    at = q * (len(xs) - 1)
    lo = int(math.floor(at))
    hi = min(lo + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (at - lo)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('run')
    ap.add_argument('--sense')
    a = ap.parse_args()

    for name in ('tank-exam.jsonl', 'tank-bench.jsonl'):
        path = os.path.join(a.run, name)
        if not os.path.exists(path):
            continue
        rows = [json.loads(l) for l in open(path, encoding='utf-8') if l.strip()]
        by = {}
        for r in rows:
            by.setdefault(r['brain'], []).append(r)
        print('%s:' % name)
        for brain, rs in by.items():
            mean = lambda k: sum(r[k] for r in rs if r.get(k) is not None) / max(1, sum(1 for r in rs if r.get(k) is not None))
            print('  %-40s genomes %2d  eaten %8.2f J  net %9.2f J  path %6.1f m  turn %.3f rad/s'
                  % (brain, len(rs), mean('eaten'), mean('net'), mean('path'), mean('turn')))
    log = os.path.join(a.run, 'nursery.log')
    if os.path.exists(log):
        for line in open(log, encoding='utf-8', errors='replace'):
            if ' min' in line and ('gen ' in line or line.lstrip().startswith(('own', 'brainless', 'knockout'))):
                print('  log: ' + line.strip()[:220])

    if a.sense:
        rows = [json.loads(l) for l in open(a.sense, encoding='utf-8') if l.strip()]
        s = [r['strength'] for r in rows if r['strength'] is not None]
        ang = [r['angle'] for r in rows if r['angle'] is not None]
        near = [r['nearest'] for r in rows if r['nearest'] is not None]
        print('sense: %d samples' % len(rows))
        print('  strength p10 %.3f  p50 %.3f  p90 %.3f  (share at 0: %.3f, share above 0.95: %.3f)'
              % (pct(s, 0.1), pct(s, 0.5), pct(s, 0.9), sum(1 for v in s if v == 0) / max(1, len(s)),
                 sum(1 for v in s if v > 0.95) / max(1, len(s))))
        print('  gradient within 45 deg of the nearest leaf: %.3f of %d samples with a gradient'
              % (sum(1 for v in ang if v <= 45) / max(1, len(ang)), len(ang)))
        print('  nearest leaf p10 %.2f  p50 %.2f  p90 %.2f m' % (pct(near, 0.1), pct(near, 0.5), pct(near, 0.9)))
        eps = {}
        for r in rows:
            e = eps.setdefault(r['seed'], [r['t'], r['leaves'], r['t'], r['leaves']])
            if r['t'] < e[0]:
                e[0], e[1] = r['t'], r['leaves']
            if r['t'] > e[2]:
                e[2], e[3] = r['t'], r['leaves']
        lost = [(e[1] - e[3]) for e in eps.values()]
        print('  leaves alive per episode, first to last sample: %d episodes, lost %s (min %d, max %d)'
              % (len(eps), 'none' if not any(lost) else 'some', min(lost), max(lost)))


if __name__ == '__main__':
    main()
