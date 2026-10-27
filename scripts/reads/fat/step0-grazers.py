"""Fat step 0: the reserve of mouthed grazers alone, from a reserve census joined to its run's lineage.

    python -I scripts/reads/fat/step0-grazers.py <census.tsv> <run dir> [<census.tsv> <run dir> ...]

A grazer is a body born mouthed, as r61-grazer-economy.py reads it: its birth row has atk 1 and ink 1. The nopho
grazers (pho 0) are printed beside all of them. For each, the count, the median, p90 and max of seconds_of_reserve,
and the share above a cap at s = 100, 300 and 1,000 s in two forms: plain seconds (energy > s x W) and D247's
(energy > P + max(R, 0) + s x W). Exits 2 on a missing file.
"""
import csv
import json
import os
import sys

S = [100, 300, 1000]


def fail(m):
    print(m, file=sys.stderr)
    sys.exit(2)


def mouthed(run):
    ids, nopho = set(), set()
    path = os.path.join(run, 'lineage.jsonl')
    if not os.path.isfile(path):
        fail(path + ': missing')
    with open(path) as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            r = json.loads(line)
            if r.get('atk') == 1 and r.get('ink') == 1:
                ids.add(r['id'])
                if r.get('pho') == 0:
                    nopho.add(r['id'])
    return ids, nopho


def q(xs, p):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * (len(xs) - 1) + 0.5))]


args = sys.argv[1:]
if not args or len(args) % 2:
    fail(__doc__)
for census, run in zip(args[0::2], args[1::2]):
    if not os.path.isfile(census):
        fail(census + ': missing')
    ids, nopho = mouthed(run)
    rows = list(csv.DictReader(open(census), delimiter='\t'))
    print('==', os.path.basename(census), 'living', len(rows))
    for name, keep in (('mouthed', ids), ('nopho mouthed', nopho)):
        g = [r for r in rows if int(r['creature']) in keep]
        if not g:
            print('  %-14s 0' % name)
            continue
        sec = [float(r['seconds_of_reserve']) for r in g]
        line = '  %-14s %4d  median %7.1f s  p90 %7.1f s  max %7.1f s' % (name, len(g), q(sec, .5), q(sec, .9), max(sec))
        for s in S:
            plain = sum(1 for r in g if float(r['energy_j']) > s * float(r['standing_w']))
            d247 = sum(1 for r in g if float(r['energy_j']) > float(r['litter_price_j'])
                       + max(0.0, float(r['adult_tissue_j']) - float(r['tissue_j'])) + s * float(r['standing_w']))
            line += '  s=%d plain %.0f%% D247 %.0f%%' % (s, 100.0 * plain / len(g), 100.0 * d247 / len(g))
        print(line)
        print('  %-14s share holding at least 100 s %.0f%%, at least 230 s %.0f%%'
              % ('', 100.0 * sum(1 for x in sec if x >= 100) / len(g), 100.0 * sum(1 for x in sec if x >= 230) / len(g)))
