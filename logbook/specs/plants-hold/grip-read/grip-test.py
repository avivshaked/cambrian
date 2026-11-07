# A fair test of what a grip is worth, and whether a grip ever arises on its own (the owner's question of 2026-11-07).
# For each world ARM under scratch/plants-work/runs:
# 1. Siblings. A child of a gripping parent either settles on rock (D307) or finds no spot and drifts. Its state at its
#    first 100 s sample after birth (gripping, flag 16, or not) splits the children of the same parents into settled
#    and drifting, born alike; children that die before their first sample are left out of both. For each part: how
#    many, the share alive at 1,000 s of age, the median life, and children per plant.
# 2. Grips of their own. Bodies seen gripping that are not founders and whose parent was never seen gripping: a grip
#    that no placing rule made.
# Usage: python -I grip-test.py ARM [ARM ...], from the repository root.
import collections, gzip, json, os, statistics, sys
ROOT = 'scratch/plants-work/runs'
for name in sys.argv[1:]:
    arm = os.path.join(ROOT, name)
    d = os.path.join(arm, sorted(os.listdir(arm))[-1])
    born, parent, kind, died, kids = {}, {}, {}, {}, collections.Counter()
    for l in open(d + '/lineage.jsonl', encoding='utf-8'):
        r = json.loads(l)
        if r.get('e') == 'b':
            born[r['id']] = r['t']; parent[r['id']] = r.get('p', -1); kind[r['id']] = r.get('k')
            if r.get('k') == 'r' and r.get('p', -1) >= 0: kids[r['p']] += 1
        elif r.get('e') == 'd': died[r['id']] = r['t']
    gripped, first, end = set(), {}, 0
    for l in gzip.open(d + '/positions.jsonl.gz', 'rt'):
        if not l.startswith('{"t":'): continue
        t = float(l[5:l.index(',')])
        if abs(t / 100 - round(t / 100)) > 1e-6: continue
        end = t
        for b in json.loads(l)['b']:
            if not (b[4] & 4) or (b[4] & 8): continue
            g = bool(b[4] & 16)
            if g: gripped.add(b[0])
            if b[0] not in first: first[b[0]] = g
    print('%s (%s)' % (name, os.path.basename(d)))
    sibs = {True: [], False: []}
    for i, p in parent.items():
        if kind.get(i) == 'r' and p in gripped and i in first and born[i] < end - 1000:
            sibs[first[i]].append(i)
    for g, label in ((True, 'settled'), (False, 'drifting')):
        ids = sibs[g]
        if not ids:
            print('  children of gripping parents, %-8s none' % label); continue
        life = [died.get(i, end) - born[i] for i in ids]
        print('  children of gripping parents, %-8s %5d, alive at 1,000 s of age %3.0f %%, median life %5.0f s, children per plant %.2f' % (
            label, len(ids), 100.0 * sum(1 for x in life if x >= 1000) / len(ids), statistics.median(life),
            sum(kids[i] for i in ids) / len(ids)))
    own = [i for i in gripped if kind.get(i) == 'r' and parent.get(i, -1) not in gripped]
    print('  grips of their own (not founders, parent never gripping): %d of %d gripping bodies' % (len(own), len(gripped)))
