"""The guild of each genome a world was offered, from the record alone.

A planted immigrant's id joins to positions.jsonl.gz, whose per-body flags (absorptive 1, jointed 2,
photosynthetic 4) come from the same tests as the report's absorpt, jointed and photo columns. The first
sample at or after the planting gives its guild. One that dies before any sample, and every refused offer,
has no body there, so it is counted as unseen.

    python -I immigrant-guilds.py <run directory> [<run directory> ...] [--every 1500]
"""
import collections
import gzip
import json
import os
import sys


def guild(flags):
    name = ('A' if flags & 1 else '') + ('J' if flags & 2 else '') + ('P' if flags & 4 else '')
    return name or 'none'


def read(run, every):
    offers = [json.loads(line) for line in open(os.path.join(run, 'immigrants.jsonl'), encoding='utf-8')]
    planted = {r['id']: r['t'] for r in offers if 'why' not in r}
    refused = [r for r in offers if 'why' in r]
    seen = {}
    with gzip.open(os.path.join(run, 'positions.jsonl.gz'), 'rt', encoding='utf-8') as f:
        for line in f:
            row = json.loads(line)
            t = row['t']
            for body in row['b']:
                i = body[0]
                if i in planted and i not in seen and t >= planted[i]:
                    seen[i] = body[-1]
    bins = collections.defaultdict(collections.Counter)
    for i, t in planted.items():
        b = int((t - 1e-9) // every + 1) * every
        bins[b][guild(seen[i]) if i in seen else 'unseen'] += 1
    for r in refused:
        b = int((r['t'] - 1e-9) // every + 1) * every
        bins[b]['refused'] += 1
    return len(offers), len(planted), len(seen), bins


def main(argv):
    every = 1500.0
    runs = []
    i = 0
    while i < len(argv):
        if argv[i] == '--every':
            every = float(argv[i + 1])
            i += 2
        else:
            runs.append(argv[i])
            i += 1
    for run in runs:
        offered, planted, seen, bins = read(run, every)
        print(f'{run}: {offered} offered, {planted} planted, {seen} seen in a sample')
        names = sorted({k for c in bins.values() for k in c})
        print('  up to t   ' + ''.join(f'{n:>8}' for n in names))
        total = collections.Counter()
        for b in sorted(bins):
            total.update(bins[b])
            print(f'  {b:>8.0f}  ' + ''.join(f'{bins[b][n]:>8}' for n in names))
        print('  all       ' + ''.join(f'{total[n]:>8}' for n in names))


if __name__ == '__main__':
    main(sys.argv[1:])
