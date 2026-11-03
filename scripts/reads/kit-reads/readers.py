"""How many genomes of a run read D294's hunger (channel 23) and D299's gate sense (channel 26), and wire the urge.

Usage: python -I scripts/reads/kit-reads/readers.py <run dir> [...]

Both senses are ports of the root part alone (CentralWiring.IndicesOn), so their ids are BrainIds.Port(BrainIds.Root,
channel, 0), reimplemented here from src/Evosim.Core/Brain/CentralBrain.cs (splitmix64's finaliser over the pair). A
genome reads a sense when a link in its brain comes from that port with a weight other than 0. Reads the files only.
The id arithmetic is checked against the C# by the one genome it is asked to read: a port id it computes must appear
in some genome, or the read says so.
"""
import gzip
import json
import os
import sys

M = (1 << 64) - 1
PORT_TAG = 0x504F5254
ROOT_SEED = 0x524F4F5442524E31


def mix(a, b):
    z = a ^ ((b + 0x9E3779B97F4A7C15 + ((a << 6) & M) + (a >> 2)) & M)
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M
    return z ^ (z >> 31)


def nonzero(x):
    return x if x else 1


ROOT = nonzero(mix(ROOT_SEED, 1))


def port(part, channel, index):
    return nonzero(mix(mix(mix(part, PORT_TAG), channel & M), index & 0xFFFFFFFF))


CHANNELS = {'Energy': 7, 'Hunger': 23, 'GateReserve': 26}
IDS = {name: '%016x' % port(ROOT, ch, 0) for name, ch in CHANNELS.items()}

for run in sys.argv[1:]:
    seen = {name: 0 for name in IDS}
    n = 0
    path = os.path.join(run, 'genomes.jsonl.gz')
    for line in gzip.open(path, 'rt', encoding='utf-8'):
        if not line.strip():
            continue
        g = json.loads(line)
        n += 1
        froms = {l['from'] for l in (g.get('brain') or {}).get('links', []) if l.get('w', 0) != 0}
        for name, pid in IDS.items():
            if pid in froms:
                seen[name] += 1
    print(run)
    print('  genomes', n, ' '.join('%s %d' % (k, v) for k, v in seen.items()),
          '' if seen['Energy'] else '(no genome reads Energy: the id arithmetic is not checked)')
