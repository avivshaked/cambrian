# A forest run read by island (logbook/specs/plants-hold/forest-run.md). Usage: python -I read5.py PREFIX [PREFIX ...],
# e.g. plg plw; each PREFIX-6202 and PREFIX-6203 under scratch/plants-work/runs, its latest run.
# Every 100 s sample: the gripping living bodies (positions flag 16 without 8), each assigned to the nearest island
# centre of the farm log's "island bloom" line (the islands the bloom and the coral's island test share; nearest centre
# is this read's approximation of the column test). The best sample after 3,000 s by island: the most gripping bodies
# on one island, and of those the share born after 3,000 s. Then the 3,000 s spans: grippers that began and died, and
# how. And the settling children that found no spot.
import gzip, json, math, os, re, sys, collections
ROOT = 'scratch/plants-work/runs'
SPANS = [(0, 2750), (2750, 5750), (5750, 8750), (8750, 11750), (11750, 12001)]


def islands(prefix, seed):
    for logs in ('scratch/wt-plants-build/scratch/logs',):
        p = os.path.join(logs, '%s-%s.log' % (prefix, seed))
        for line in open(p, encoding='utf-8', errors='replace'):
            if 'island bloom, ' in line and 'centre x z m' in line:
                body = line.split('centre x z m):', 1)[1]
                return [(int(m.group(1)), int(m.group(2)), float(m.group(3)), float(m.group(4)))
                        for m in re.finditer(r'(\d+): P [0-9.]+, A [0-9.]+, phase [0-9.]+, (\d+) columns, at ([0-9.]+) ([0-9.]+)', body)]
    sys.exit('no island line for %s-%s' % (prefix, seed))


for prefix in sys.argv[1:]:
    for seed in ('6202', '6203'):
        arm = os.path.join(ROOT, '%s-%s' % (prefix, seed))
        d = os.path.join(arm, sorted(os.listdir(arm))[-1])
        isl = islands(prefix, seed)
        born, parent, died, cause = {}, {}, {}, {}
        for l in open(d + '/lineage.jsonl', encoding='utf-8'):
            r = json.loads(l)
            if r.get('e') == 'b': born[r['id']] = r['t']; parent[r['id']] = r['p']
            elif r.get('e') == 'd': died[r['id']] = r['t']; cause[r['id']] = r.get('c')
        first, best, total, last_t = {}, (0, None, None, 0), {}, 0
        for l in gzip.open(d + '/positions.jsonl.gz', 'rt'):
            if not l.startswith('{"t":'): continue
            t = float(l[5:l.index(',')])
            if abs(t / 100 - round(t / 100)) > 1e-6: continue
            s = json.loads(l)
            g = [b for b in s['b'] if (b[4] & 16) and not (b[4] & 8)]
            t = round(t); last_t = t
            total[t] = len(g)
            for b in g: first.setdefault(b[0], t)
            if t <= 3000: continue
            by = collections.defaultdict(list)
            for b in g:
                k = min(isl, key=lambda i: math.hypot(b[1] - i[2], b[3] - i[3]))[0]
                by[k].append(b[0])
            for k, ids in by.items():
                if len(ids) > best[0]:
                    best = (len(ids), t, k, sum(1 for i in ids if born.get(i, 0) > 3000))
        st = {}
        for l in open(d + '/stats.jsonl', encoding='utf-8'):
            r = json.loads(l); st = r
        print('== %s-%s (%s), to %d s' % (prefix, seed, os.path.basename(d), last_t))
        n, t, k, late = best
        if n:
            i = [x for x in isl if x[0] == k][0]
            print('  most gripping on one island after 3,000 s: %d on island %d (%d columns, at %.0f, %.0f) at %d s, %d of them born after 3,000 s'
                  % (n, k, i[1], i[2], i[3], t, late))
        print('  gripping at each 1,500 s:', [total.get(m) for m in range(1500, 12001, 1500)])
        s_ok, s_no = st.get('childrenSettled', 0), st.get('childrenSettleFailed', 0)
        print('  children settled %d, found no spot %d (%.0f %% failed); anchored founders %d, refused %d' % (
            s_ok, s_no, 100.0 * s_no / max(1, s_ok + s_no), st.get('anchoredFounders', 0), st.get('anchoredFoundersRefused', 0)))
        for a, b in SPANS:
            began = [i for i, x in first.items() if a <= x < b]
            dead = [i for i in first if a <= died.get(i, 1e9) < b]
            end = total.get(max([x for x in total if x < b] or [0]), 0)
            print('  %5d-%5d s: began %3d (founders %2d), died %3d %s, gripping at its end %3d' % (
                a, min(b, 12000), len(began), sum(1 for i in began if parent.get(i) == -1), len(dead),
                dict(collections.Counter(cause[i] for i in dead)), end))
