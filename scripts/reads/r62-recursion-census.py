"""D196's census: among one snapshot's living bodies, how many grow by a recursive edge whose scale is not 1.

For each living body's genome: its self-edges (an edge back to its own node) that the node's recursive limit lets
repeat (limit 2 or more), and its longer cycles (a node reached again through other nodes). An edge's scale is a
per-axis factor; a repeat multiplies by it each copy today. "Grows" is any axis at 1.1 or more, "shrinks" any at
0.9 or less, and the largest per-axis factor is reported. Reads only.

    python -I scripts/reads/r62-recursion-census.py <run dir> <snapshot second> <genome run dir>...
"""
import gzip
import json
import sys
import zlib
from collections import Counter

run, second, gdirs = sys.argv[1], int(sys.argv[2]), sys.argv[3:]


def lines(path):
    # A live run's gzip may end mid-block; read what is whole.
    out = []
    try:
        with gzip.open(path, 'rt', encoding='utf-8') as f:
            for line in f:
                if line.strip():
                    out.append(line)
    except (EOFError, zlib.error, OSError):
        pass
    return out


alive = []
for line in lines(f'{run}/snapshots/{second:09d}.jsonl.gz'):
    alive.append(json.loads(line)['id'])
alive_set = set(alive)
genomes = {}
for d in gdirs:
    for line in lines(f'{d}/genomes.jsonl.gz'):
        j = json.loads(line)
        if j.get('id') in alive_set:
            genomes[j['id']] = j

tally = Counter()
biggest = []
for gid in alive:
    g = genomes.get(gid)
    if g is None:
        tally['no genome'] += 1
        continue
    nodes = g['nodes']
    selfs = []
    for i, n in enumerate(nodes):
        for e in n['edges']:
            if e['child'] == i and n['recursiveLimit'] >= 2:
                s = e['scale']
                selfs.append((s['x'], s['y'], s['z'], n['recursiveLimit']))
    # Longer cycles: node i reaches itself again through at least one other node.
    adj = {i: {e['child'] for e in n['edges'] if e['child'] != i} for i, n in enumerate(nodes)}
    cyc = False
    for i in adj:
        seen, stack = set(), list(adj[i])
        while stack:
            k = stack.pop()
            if k == i:
                cyc = True
                break
            if k in seen:
                continue
            seen.add(k)
            stack.extend(adj.get(k, ()))
        if cyc:
            break
    grows = any(max(x, y, z) >= 1.1 for x, y, z, _ in selfs)
    shrinks = any(min(x, y, z) <= 0.9 for x, y, z, _ in selfs)
    tally['bodies'] += 1
    tally['a repeating self-edge'] += bool(selfs)
    tally['a self-edge that grows an axis'] += grows
    tally['a self-edge that shrinks an axis'] += shrinks
    tally['a self-edge within 10% of 1 on every axis'] += bool(selfs) and not grows and not shrinks
    tally['a cycle through two or more nodes'] += cyc
    if selfs:
        biggest.append(max(max(x, y, z) for x, y, z, _ in selfs))

print(f'{run}, snapshot {second} s: {len(alive)} living bodies, {len(genomes)} genomes found')
for k, v in tally.items():
    print(f'  {k}: {v}')
if biggest:
    biggest.sort()
    q = lambda f: biggest[min(len(biggest) - 1, int(f * len(biggest)))]
    print(f'  the largest per-axis self-scale, over bodies with one: median {q(0.5):.2f}, 90th percentile {q(0.9):.2f}, max {biggest[-1]:.2f}')
