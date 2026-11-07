# The forest test at 12,000 s by island: gripping living bodies per island (nearest centre of the farm log's island
# list), how many born after 3,000 s, and their median depth. Usage: python -I at-end.py PREFIX [PREFIX ...]
# --seeds 6202,6204 reads those seeds in place of 6202 and 6203 (D325's screen, 2026-11-07).
import gzip, json, math, os, re, sys, collections
args = sys.argv[1:]
SEEDS = ('6202', '6203')
if '--seeds' in args:
    i = args.index('--seeds'); SEEDS = tuple(args[i + 1].split(',')); del args[i:i + 2]
for prefix in args:
    for seed in SEEDS:
        arm = 'scratch/plants-work/runs/%s-%s' % (prefix, seed)
        d = os.path.join(arm, sorted(os.listdir(arm))[-1])
        line = [l for l in open('scratch/wt-plants-build/scratch/logs/%s-%s.log' % (prefix, seed), encoding='utf-8', errors='replace')
                if 'island bloom, ' in l and 'centre x z m' in l][0]
        isl = [(int(m.group(1)), float(m.group(3)), float(m.group(4))) for m in
               re.finditer(r'(\d+): P [0-9.]+, A [0-9.]+, phase [0-9.]+, (\d+) columns, at ([0-9.]+) ([0-9.]+)', line.split('centre x z m):', 1)[1])]
        born = {}
        for l in open(d + '/lineage.jsonl', encoding='utf-8'):
            r = json.loads(l)
            if r.get('e') == 'b': born[r['id']] = r['t']
        for l in gzip.open(d + '/positions.jsonl.gz', 'rt'):
            if l.startswith('{"t":12000,') or l.startswith('{"t":12000.0,'):
                g = [b for b in json.loads(l)['b'] if (b[4] & 16) and not (b[4] & 8)]
                by = collections.defaultdict(list)
                for b in g:
                    by[min(isl, key=lambda i: math.hypot(b[1] - i[1], b[3] - i[2]))[0]].append(b)
                best = max(by.items(), key=lambda kv: len(kv[1])) if by else (None, [])
                ok = any(len(v) >= 20 and sum(1 for b in v if born.get(b[0], 0) > 3000) >= 0.8 * len(v) for v in by.values())
                print('%s-%s at 12,000 s: by island (n, born after 3,000 s): %s; test %s' % (
                    prefix, seed, {k: (len(v), sum(1 for b in v if born.get(b[0], 0) > 3000)) for k, v in sorted(by.items())},
                    'passes' if ok else 'fails'))
                break
