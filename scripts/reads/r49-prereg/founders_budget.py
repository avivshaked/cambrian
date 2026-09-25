"""Round 49 prereg helper (not for the record): founders' first child by source in rounds 47 and 48,
and a counterfactual for option (b) from round 48's one-part pool stomachs' own food income.
Reads lineage.jsonl and absorptive.jsonl only. Usage: python founders_budget.py"""
import collections
import glob
import json
import os
import statistics

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'runs')


def newest(arm):
    return sorted(glob.glob(os.path.join(ROOT, arm, '*', '')))[-1]


def lineage(d):
    births, deaths, kids = {}, {}, collections.defaultdict(list)
    with open(os.path.join(d, 'lineage.jsonl'), encoding='utf-8') as f:
        for line in f:
            try:
                r = json.loads(line)
            except Exception:
                continue
            if r.get('e') == 'b':
                births[r['id']] = r
                if r.get('p', -1) >= 0:
                    kids[r['p']].append(r['t'])
            elif r.get('e') == 'd':
                deaths[r['id']] = r['t']
    return births, deaths, kids


def med(xs):
    return statistics.median(xs) if xs else None


def founders_table(arm):
    d = newest(arm)
    births, deaths, kids = lineage(d)
    out = []
    by = collections.defaultdict(list)
    for i, r in births.items():
        if r.get('k') == 'f':
            by[(r.get('src', '?'), r.get('pool', '-'))].append(r)
    for key in sorted(by, key=str):
        rows = by[key]
        bred = [r for r in rows if kids.get(r['id'])]
        lags = [min(kids[r['id']]) - r['t'] for r in bred]
        w1 = sum(1 for x in lags if x <= 1.0)
        w10 = sum(1 for x in lags if x <= 10.0)
        ages = [deaths[r['id']] - r['t'] for r in rows if r['id'] in deaths]
        out.append('%-8s %-18s n=%4d bred=%3d w1s=%3d w10s=%3d  median age at death %s (dead %d)'
                   % (arm, key, len(rows), len(bred), w1, w10,
                      ('%.1f' % med(ages)) if ages else '-', len(ages)))
    return out


def pool0_counterfactual(arm, gap=64.0, span=180.0):
    """For each one-part pool stomach (pool 0): its mean foodW over its logged rows in the first
    `span` s, and whether food income over the span would reach `gap` J (the reserve's gap to the
    adult gate under option (b), with the endowment paying upkeep). Inference, not measurement:
    round 48's founders had a child beside them and died early."""
    d = newest(arm)
    births, deaths, kids = lineage(d)
    pool0 = {i: r for i, r in births.items() if r.get('src') == 'pool' and r.get('pool') == 0}
    rows = collections.defaultdict(list)
    with open(os.path.join(d, 'absorptive.jsonl'), encoding='utf-8') as f:
        for line in f:
            k = line.find('"id":')
            if k < 0:
                continue
            try:
                i = int(line[k + 5:line.find(',', k)])
            except ValueError:
                continue
            if i not in pool0:
                continue
            r = json.loads(line)
            rows[i].append((r['t'] - pool0[i]['t'], r.get('foodW') or 0.0, r.get('densityHere') or 0.0,
                            r.get('energy'), r.get('dead')))
    means, reach, dens = [], 0, []
    for i, rs in rows.items():
        early = [x for x in rs if x[0] <= span and not x[4]]
        if not early:
            means.append(0.0)
            continue
        m = sum(x[1] for x in early) / len(early)
        means.append(m)
        dens.append(sum(x[2] for x in early) / len(early))
        if m * span >= gap:
            reach += 1
    means.sort()
    q = lambda p: means[int(p * (len(means) - 1))] if means else None
    return ('%-8s pool-0 founders %d with rows %d: mean foodW over first %g s: q25 %.3f median %.3f q75 %.3f max %.3f;'
            ' mean densityHere median %.3f; reach %g J in %g s at that mean: %d'
            % (arm, len(pool0), len(rows), span, q(0.25), q(0.5), q(0.75), means[-1],
               med(dens) or 0, gap, span, reach))


if __name__ == '__main__':
    for arm in ['r47-s1', 'r47-s2', 'r47-s3', 'r48-s1', 'r48-s2', 'r48-s3']:
        for line in founders_table(arm):
            print(line)
        print()
    for arm in ['r48-s1', 'r48-s2', 'r48-s3']:
        print(pool0_counterfactual(arm))
