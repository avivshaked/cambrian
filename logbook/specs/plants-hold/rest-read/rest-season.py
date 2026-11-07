# D319's rest by season (logbook/specs/plants-hold/forest-run.md, "B, rest"), for each world PREFIX-6202 and
# PREFIX-6203 under scratch/plants-work/runs:
# - the stats row's rest counters: plants resting (mean and most), rests begun and wakes in each season;
# - the gripping bodies (positions flag 16 without 8) at each season's end, and how many of them rest (flag 32);
# - plant deaths (pho 1) in each season, by cause, with their median age;
# - the rest delay gene ("restDelay" in genomes.jsonl.gz) of the gripping bodies alive at 12,000 s.
# A world without the rest has no counters, no flag 32 and no gene, and prints the rest as absent.
# Seasons follow D151's two-season shape. Run from the repository root.
# Usage: python -I rest-season.py PREFIX [PREFIX ...]
import collections
import gzip
import json
import os
import statistics
import sys

SPANS = [(0, 2750, 'summer 1'), (2750, 5750, 'winter 1'), (5750, 8750, 'summer 2'), (8750, 11750, 'winter 2'),
         (11750, 12000.5, 'summer 3')]
ENDS = (2750, 5750, 8750, 11750, 12000)


def season(t):
    for lo, hi, name in SPANS:
        if lo <= t < hi:
            return name
    return None


def median(xs):
    return statistics.median(xs) if xs else float('nan')


def rows(path):
    with open(path, encoding='utf-8') as src:
        for line in src:
            line = line.strip()
            if not line:
                continue
            try:
                yield json.loads(line)
            except ValueError:
                continue


for prefix in sys.argv[1:]:
    for seed in ('6202', '6203'):
        arm = 'scratch/plants-work/runs/%s-%s' % (prefix, seed)
        if not os.path.isdir(arm):
            print('%s-%s: no world' % (prefix, seed))
            continue
        d = os.path.join(arm, sorted(os.listdir(arm))[-1])
        print('%s-%s (%s)' % (prefix, seed, d))

        rest = collections.defaultdict(list)
        first, last = {}, {}
        for r in rows(d + '/stats.jsonl'):
            name = season(r.get('t', -1))
            if name is None or 'plantsResting' not in r:
                continue
            rest[name].append(r['plantsResting'])
            first.setdefault(name, (r['restsBegun'], r['wakes']))
            last[name] = (r['restsBegun'], r['wakes'])
        if not rest:
            print('  rest counters: absent')
        prev = (0, 0)
        for _, _, name in SPANS:
            if name not in rest:
                continue
            began = last[name][0] - prev[0]
            woke = last[name][1] - prev[1]
            prev = last[name]
            print('  %-9s resting mean %.1f, most %d; rests begun %d, wakes %d' % (
                name, sum(rest[name]) / len(rest[name]), max(rest[name]), began, woke))

        born, plant, died = {}, set(), {}
        deaths = collections.defaultdict(list)
        for r in rows(d + '/lineage.jsonl'):
            if r.get('e') == 'b':
                born[r['id']] = r['t']
                if r.get('pho') == 1:
                    plant.add(r['id'])
            elif r.get('e') == 'd' and r['id'] in plant:
                died[r['id']] = r['t']
                name = season(r['t'])
                if name:
                    deaths[name].append((r.get('c', '?'), r['t'] - born.get(r['id'], r['t'])))
        for _, _, name in SPANS:
            ds = deaths.get(name, [])
            causes = collections.Counter(c for c, _ in ds)
            print('  %-9s plant deaths %d %s, median age %.0f s' % (
                name, len(ds), dict(causes.most_common()), median([a for _, a in ds])))

        at_end = []
        for line in gzip.open(d + '/positions.jsonl.gz', 'rt', encoding='utf-8'):
            head = line[:24]
            t = None
            for e in ENDS:
                if head.startswith('{"t":%d,' % e) or head.startswith('{"t":%d.0,' % e):
                    t = e
            if t is None:
                continue
            bodies = json.loads(line)['b']
            grip = [b for b in bodies if (b[4] & 16) and not (b[4] & 8)]
            resting = sum(1 for b in grip if b[4] & 32)
            print('  at %5d s: gripping %d, of them resting %d' % (t, len(grip), resting))
            if t == 12000:
                at_end = [b[0] for b in grip]

        if at_end:
            want = set(at_end)
            delays = []
            with gzip.open(d + '/genomes.jsonl.gz', 'rt', encoding='utf-8') as src:
                for line in src:
                    try:
                        g = json.loads(line)
                    except ValueError:
                        continue
                    if g.get('id') in want and 'restDelay' in g:
                        delays.append(g['restDelay'])
            if delays:
                q = statistics.quantiles(delays, n=4) if len(delays) > 1 else [delays[0]] * 3
                print('  rest delay of the %d gripping at 12,000 s: median %.0f s, quartiles %.0f-%.0f s' % (
                    len(delays), q[1], q[0], q[2]))
            else:
                print('  rest delay of the gripping at 12,000 s: absent')
