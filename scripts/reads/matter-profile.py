import json, glob, sys, os
# Matter readings from stats.jsonl (matterHere is not in the report table): per arm, rows at
# every `every` seconds plus min/mean after `from_t`.
every = 2000; from_t = 10000
for arm in sys.argv[1:]:
    runs = sorted(glob.glob(os.path.join('runs', arm, '*')))
    path = os.path.join(runs[-1], 'stats.jsonl')
    rows = []
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    print('==', arm, 'rows', len(rows))
    print('t\talive\tabsorpt\tdepth\tmatHere\tmatSurf\tmatDeep\tstanding\tblocked')
    keys = ['t', 'alive', 'absorptive', 'meanDepth', 'matterHere', 'matterSurface', 'matterDeep', 'matterStanding', 'conceptionsBlockedByMatter']
    for r in rows:
        t = r.get('t', r.get('time'))
        if t is not None and abs(t % every) < 1e-6 and t > 0:
            print('\t'.join(str(round(r.get(k, float('nan')), 3)) if isinstance(r.get(k), float) else str(r.get(k)) for k in keys))
    late = [r for r in rows if (r.get('t', r.get('time')) or 0) >= from_t]
    if late:
        for k in ['matterHere', 'matterSurface', 'matterDeep', 'matterStanding', 'alive']:
            vals = [r[k] for r in late if k in r]
            if vals:
                print(f'  {k}: min {min(vals):.3f} mean {sum(vals)/len(vals):.3f} last {vals[-1]:.3f}')
    if rows:
        print('  keys:', ', '.join(sorted(rows[0].keys()))[:600])
