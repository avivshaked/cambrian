# What a grip is worth to a plant (the owner's question of 2026-11-07, after D323's screen): for each world PREFIX-SEED
# under scratch/plants-work/runs, the plants (positions flag 4 without 8) that were ever seen gripping (flag 16) at a
# 100 s sample against those never seen gripping: how many, their median height (y) over all their samples, their
# median life, and their children by breeding per plant. A grip that pays shows as more children per plant.
# Usage: python -I grip-worth.py ARM [ARM ...], e.g. pldr-6202 plro-6202, from the repository root.
import collections, gzip, json, os, statistics, sys
ROOT = 'scratch/plants-work/runs'
for name in sys.argv[1:]:
    arm = os.path.join(ROOT, name)
    d = os.path.join(arm, sorted(os.listdir(arm))[-1])
    born, died, kids = {}, {}, collections.Counter()
    for l in open(d + '/lineage.jsonl', encoding='utf-8'):
        r = json.loads(l)
        if r.get('e') == 'b':
            born[r['id']] = r['t']
            if r.get('k') == 'r' and r.get('p', -1) >= 0: kids[r['p']] += 1
        elif r.get('e') == 'd': died[r['id']] = r['t']
    plant, gripped, ys = set(), set(), collections.defaultdict(list)
    end = 0
    for l in gzip.open(d + '/positions.jsonl.gz', 'rt'):
        if not l.startswith('{"t":'): continue
        t = float(l[5:l.index(',')])
        if abs(t / 100 - round(t / 100)) > 1e-6: continue
        end = t
        for b in json.loads(l)['b']:
            if (b[4] & 4) and not (b[4] & 8):
                plant.add(b[0]); ys[b[0]].append(b[2])
                if b[4] & 16: gripped.add(b[0])
    print('%s (%s)' % (name, os.path.basename(d)))
    for label, ids in (('ever gripping', plant & gripped), ('never gripping', plant - gripped)):
        ids = [i for i in ids if i in born]
        if not ids:
            print('  %-15s none' % label); continue
        life = [died.get(i, end) - born[i] for i in ids]
        k = [kids[i] for i in ids]
        print('  %-15s %5d plants, median y %.1f m, median life %.0f s, children per plant %.2f, share with any %.0f %%' % (
            label, len(ids), statistics.median(statistics.median(ys[i]) for i in ids), statistics.median(life),
            sum(k) / len(k), 100.0 * sum(1 for x in k if x) / len(k)))
