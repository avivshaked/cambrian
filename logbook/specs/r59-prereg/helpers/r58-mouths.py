"""Counts births with attack (atk) and intake (ink) flags per year in a run's lineage. Usage: python -I r58-mouths.py <run dir>..."""
import json, sys, os
for d in sys.argv[1:]:
    c = {}
    with open(os.path.join(d, 'lineage.jsonl'), encoding='utf-8') as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            r = json.loads(line)
            y = int(r['t'] // 6000) + 1
            k = c.setdefault(y, [0, 0, 0, 0])
            k[0] += 1
            k[1] += r.get('atk') == 1
            k[2] += r.get('ink') == 1
            k[3] += r.get('k') == 'f'
    print(d)
    print('  year births atk ink founders')
    for y in sorted(c):
        print('  ', y, *c[y])
