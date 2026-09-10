import json, glob, os, sys
# Parent's age at each birth, from lineage.jsonl: is conception going to the oldest?
# Prints, per arm and per time window, the distribution of (t_birth - parent's own birth t)
# in 500-s bins, plus the median. Founders (kind f) and inoculants excluded as parents unknown.
bins = [0, 500, 1000, 1500, 2000, 2500, 3000, 3500, 99999]
for arm in sys.argv[1:]:
    runs = sorted(glob.glob(os.path.join('runs', arm, '*')))
    path = os.path.join(runs[-1], 'lineage.jsonl')
    born = {}
    births = []  # (t, parent age, child abs, parent id)
    with open(path, encoding='utf-8') as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            try:
                r = json.loads(line)
            except ValueError:
                continue  # a live file's partial last line
            born[r['id']] = r['t']
            p = r.get('p', -1)
            if r.get('k') == 'r' and p in born:
                births.append((r['t'], r['t'] - born[p], r.get('abs', 0), p))
    print('==', arm, 'regular births with a known parent:', len(births))
    for lo, hi, label in [(3000, 10000, 'growth 3,000-10,000'), (10000, 40000, 'plateau >10,000')]:
        sel = [b for b in births if lo <= b[0] < hi]
        if not sel:
            continue
        ages = sorted(b[1] for b in sel)
        med = ages[len(ages) // 2]
        hist = []
        for i in range(len(bins) - 1):
            n = sum(1 for a in ages if bins[i] <= a < bins[i + 1])
            hist.append(f'{bins[i]}-{bins[i+1] if bins[i+1] < 99999 else ""}: {100*n/len(ages):.0f}%')
        print(f'  {label}: n={len(sel)} median parent age {med:.0f} s | ' + ' '.join(hist))
        absb = [b for b in sel if b[2] == 1]
        if absb:
            a2 = sorted(b[1] for b in absb)
            print(f'    absorptive children: n={len(absb)} median parent age {a2[len(a2)//2]:.0f} s, min {a2[0]:.0f}, max {a2[-1]:.0f}')
