import json, glob, os, sys
# The bid distribution at the last logged sample: stomach energy above (endowment + tissue),
# and the world's mean energy per living body from stats.jsonl, per arm.
for arm in sys.argv[1:]:
    run = sorted(glob.glob(os.path.join('runs', arm, '*')))[-1]
    stats = [json.loads(l) for l in open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') if l.strip()]
    last = stats[-1]
    ekeys = [k for k in last if 'nerg' in k.lower() or k.lower().endswith('joules')]
    print('==', arm, 't', last.get('t'), 'alive', last.get('alive'))
    print('  stats energy keys:', {k: round(last[k], 1) if isinstance(last[k], float) else last[k] for k in ekeys})
    cfg = json.load(open(os.path.join(run, 'config.json'), encoding='utf-8'))
    overhead = None
    for k, v in cfg.items():
        if 'overhead' in k.lower():
            overhead = v
    print('  perOffspringOverhead', overhead)
    rows = []
    tmax = None
    for l in open(os.path.join(run, 'absorptive.jsonl'), encoding='utf-8'):
        try:
            r = json.loads(l)
        except ValueError:
            continue
        rows.append(r)
    if not rows:
        continue
    tmax = max(r['t'] for r in rows)
    lastrows = [r for r in rows if r['t'] == tmax and not r.get('dead')]
    surplus = sorted(r['energy'] - (r['endowment'] + r['tissue'] + (overhead or 0)) for r in lastrows)
    energies = sorted(r['energy'] for r in lastrows)
    n = len(surplus)
    if n:
        print(f'  stomachs at t={tmax}: n={n}, energy median {energies[n//2]:.0f} J, max {energies[-1]:.0f}; '
              f'surplus above gate median {surplus[n//2]:.0f} J, share solvent {sum(1 for s in surplus if s > 0)/n:.0%}, '
              f'mean children {sum(r["children"] for r in lastrows)/n:.2f}')
