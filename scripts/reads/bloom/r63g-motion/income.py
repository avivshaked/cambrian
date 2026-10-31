"""What grazers keep from bites, a second, against their standing cost: stats.jsonl's cumulative biteKept* columns
differenced over --from..--end, over the grazer-seconds lived in that span (lineage births and deaths).

Usage: python -I scripts/reads/bloom/r63g-motion/income.py RUN LABEL [--from 6000] [--end 24000] [--window 6000]

The bite columns are the world's: every biting body's kept joules (D220's contact eating), so laying them all on the
grazers (atk > 0 and ink > 0 at birth) is an upper bound on a grazer's income; the bodies born with intake (ink > 0) are
printed as a second denominator. Standard library only.
"""
import argparse
import bisect
import json
import os

ap = argparse.ArgumentParser()
ap.add_argument('run')
ap.add_argument('label')
ap.add_argument('--from', dest='t0', type=float, default=6000.0)
ap.add_argument('--end', type=float, default=24000.0)
ap.add_argument('--window', type=float, default=6000.0)
a = ap.parse_args()

born, kind = {}, {}
died = {}
with open(os.path.join(a.run, 'lineage.jsonl'), encoding='utf-8') as f:
    for line in f:
        if line.startswith('{"e":"b"'):
            r = json.loads(line)
            if r['t'] > a.end:
                break
            born[r['id']] = r['t']
            g = r.get('atk', 0) > 0 and r.get('ink', 0) > 0
            kind[r['id']] = 'g' if g else ('i' if r.get('ink', 0) > 0 else 'o')
        elif line.startswith('{"e":"d"'):
            r = json.loads(line)
            if r['t'] > a.end:
                break
            died[r['id']] = r['t']


def seconds_lived(lo, hi, k):
    s = 0.0
    for i, b in born.items():
        if kind[i] not in k:
            continue
        d = died.get(i, a.end)
        x0, x1 = max(lo, b), min(hi, d)
        if x1 > x0:
            s += x1 - x0
    return s


rows = {}
with open(os.path.join(a.run, 'stats.jsonl'), encoding='utf-8') as f:
    for line in f:
        r = json.loads(line)
        if a.t0 - 1 <= r['t'] <= a.end + 1:
            rows[round(r['t'])] = r

print(f'== {a.label}  {a.run}')
print(f'{"span":>13} | {"grazer-s":>10} {"ink-s":>10} | {"kept leaves J":>13} {"others J":>10} {"remains J":>10} | '
      f'{"W/grazer":>9} {"leaf W/g":>9} {"W/ink body":>10}')
edges = []
t = a.t0
while t < a.end - 1:
    edges.append((t, min(a.end, t + a.window)))
    t += a.window
edges.append((a.t0, a.end))
for lo, hi in edges:
    r0, r1 = rows.get(round(lo)), rows.get(round(hi))
    if r0 is None or r1 is None:
        continue
    dl = r1['biteKeptLeaves'] - r0['biteKeptLeaves']
    do = r1['biteKeptOthers'] - r0['biteKeptOthers']
    dr = r1['biteKeptRemains'] - r0['biteKeptRemains']
    gs = seconds_lived(lo, hi, ('g',))
    ins = seconds_lived(lo, hi, ('g', 'i'))
    tot = dl + do + dr
    print(f'{lo:6.0f}-{hi:<6.0f} | {gs:10.0f} {ins:10.0f} | {dl:13.0f} {do:10.0f} {dr:10.0f} | {tot/gs:9.4f} {dl/gs:9.4f} '
          f'{tot/ins:10.4f}')
