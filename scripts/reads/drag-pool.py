"""The drag check's pool, from a round 58 run (logbook/specs/nursery-leaf-tank-plan.md, step 6).

    python scripts/reads/drag-pool.py <run> --second 44000 --swimmer inocula/leaf-tank/swimmer.json --out rounds/nursery/pools

Writes one pool of eight bodies, the nursery's bred-pool format (the body's index, a tab, the genome's JSON, LF line
ends), named by the first 16 hex digits of its SHA-256:

- bodies 0 and 1 are the hand-built swimmer, its file's bytes on one line;
- bodies 2 to 7 are six single stomachs: living bodies with absorptive tissue, no joint and no photosynthetic tissue
  by the flags the farm writes, whose genome is one node and no edge, so one part. They are taken evenly by id from
  all such bodies at the second, so the choice draws nothing.

A record beside it (`drag-<name>.json`) says how it was made. Standard library only.
"""
import argparse
import hashlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import runrec  # noqa: E402

ABSORPTIVE = 1
JOINTED = 2
PHOTOSYNTHETIC = 4
STOMACHS = 6


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('run')
    ap.add_argument('--second', type=int, required=True)
    ap.add_argument('--swimmer', required=True, help="the swimmer plate's genome file")
    ap.add_argument('--out', required=True)
    args = ap.parse_args()

    run = runrec.run_directory(args.run)
    row = None
    for r in runrec.positions(run):
        if row is None or abs(r['t'] - args.second) < abs(row['t'] - args.second):
            row = r
        if r['t'] > args.second:
            break
    flagged = sorted(b[0] for b in row['b'] if (b[4] & ABSORPTIVE) and not (b[4] & (JOINTED | PHOTOSYNTHETIC)))
    print('positions: t %.1f, %d bodies, %d absorptive with no joint and no photosynthetic tissue'
          % (row['t'], len(row['b']), len(flagged)))

    snap = runrec.snapshot(run, args.second, ids=set(flagged))
    if snap is None:
        raise SystemExit('no snapshot at %d' % args.second)
    single = []
    for line, ident, genome in zip(snap.lines, snap.ids, snap):
        if len(genome['nodes']) == 1 and not genome.get('edges'):
            text = runrec.genome_line_of(line)
            single.append((ident, '{' + text[len('{"id":%d,' % ident):]))
    single.sort()
    print('snapshot %d: %d of them joined, %d with one node and no edge' % (args.second, len(snap.ids), len(single)))
    if len(single) < STOMACHS:
        raise SystemExit('fewer than %d single stomachs' % STOMACHS)
    step = len(single) / STOMACHS
    chosen = [single[int(k * step)] for k in range(STOMACHS)]

    swimmer = open(args.swimmer, 'rb').read().decode('utf-8').rstrip('\n')
    if '\n' in swimmer:
        raise SystemExit('the swimmer file is more than one line')
    rows = ['0\t' + swimmer, '1\t' + swimmer] + ['%d\t%s' % (2 + k, g) for k, (_, g) in enumerate(chosen)]
    data = ('\n'.join(rows) + '\n').encode('utf-8')
    name = hashlib.sha256(data).hexdigest()[:16]
    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, name + '.tsv'), 'wb') as f:
        f.write(data)
    record = {
        'run': os.path.relpath(run).replace('\\', '/'),
        'second': args.second,
        'positionsT': row['t'],
        # From inocula/ on, so the record reads the same from any checkout.
        'swimmer': args.swimmer.replace('\\', '/')[args.swimmer.replace('\\', '/').find('inocula/'):],
        'swimmerSha256': hashlib.sha256(open(args.swimmer, 'rb').read()).hexdigest(),
        'singleStomachs': len(single),
        'stomachIds': [i for i, _ in chosen],
        'pool': name + '.tsv',
    }
    with open(os.path.join(args.out, 'drag-%s.json' % name), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(record, f, indent=2)
        f.write('\n')
    print('wrote %s.tsv: swimmer as bodies 0 and 1, stomachs %s as bodies 2 to 7' % (name, record['stomachIds']))


if __name__ == '__main__':
    main()
