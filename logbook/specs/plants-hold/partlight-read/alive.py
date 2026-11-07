# Living bodies at each 1,500 s, and the mean after 3,000 s, for each world named (scratch/plants-work/runs/<arm>,
# its latest run). Usage: python -I alive.py ARM [ARM ...]
import json, os, sys
for arm in sys.argv[1:]:
    a = os.path.join('scratch/plants-work/runs', arm)
    d = os.path.join(a, sorted(os.listdir(a))[-1])
    at, late = {}, []
    for l in open(os.path.join(d, 'stats.jsonl'), encoding='utf-8'):
        r = json.loads(l)
        if r['t'] % 1500 == 0: at[r['t']] = r.get('alive')
        if r['t'] > 3000: late.append(r.get('alive') or 0)
    print('%s: alive at each 1,500 s %s; mean after 3,000 s %.0f' % (arm, [at.get(t) for t in range(1500, 12001, 1500)],
                                                                  sum(late) / max(1, len(late))))
