import json, glob, os, sys
# The open budget's readings (logbook/0058) from stats.jsonl: influx and burial per window,
# the standing stock, the identity residual, matterHere; rows at every `every` s and means
# after `from_t`. Field names per logbook/specs/open-budget-spec.md.
every = 2000; from_t = 10000
for arm in sys.argv[1:]:
    run = sorted(glob.glob(os.path.join('runs', arm, '*')))[-1]
    rows = []
    for l in open(os.path.join(run, 'stats.jsonl'), encoding='utf-8'):
        try:
            rows.append(json.loads(l))
        except ValueError:
            continue
    cfg = json.load(open(os.path.join(run, 'config.json'), encoding='utf-8'))
    print('==', arm, 'rows', len(rows))
    print('t\talive\tabsorpt\tin/win\tburied/win\tlocked\tfree\tstanding\tmatHere\tblocked/win')
    prev_blk = 0
    for r in rows:
        t = r.get('t')
        free = r.get('matterStanding', 0) - r.get('matterLocked', r.get('matterInBodies', 0))
        if t and abs(t % every) < 1e-6:
            print('\t'.join(str(round(x, 2)) if isinstance(x, float) else str(x) for x in [
                t, r.get('alive'), r.get('absorptive'), r.get('matterInfluxWindow'), r.get('matterBuriedWindow'),
                round(r.get('matterLocked', r.get('matterInBodies', float('nan'))), 0), round(free, 0),
                round(r.get('matterStanding', float('nan')), 0), r.get('matterHere'), r.get('conceptionsBlockedByMatter')]))
    late = [r for r in rows if (r.get('t') or 0) >= from_t]
    if late:
        for k in ['matterHere', 'matterStanding', 'alive', 'matterInfluxWindow', 'matterBuriedWindow']:
            vals = [r[k] for r in late if k in r and r[k] is not None]
            if vals:
                print(f'  {k}: min {min(vals):.3f} mean {sum(vals)/len(vals):.3f} last {vals[-1]:.3f}')
    last = rows[-1] if rows else {}
    if 'matterInfluxedTotal' in last:
        print(f"  totals: influxed {last['matterInfluxedTotal']:.0f} buried {last['matterBuriedTotal']:.0f} "
              f"standing {last.get('matterStanding', float('nan')):.0f}")
    print('  matter keys:', [k for k in last if 'atter' in k])
