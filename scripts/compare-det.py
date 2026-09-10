"""Replay-identity check between two arms: first stats.jsonl sample whose fields differ.
Usage: python3 scripts/compare-det.py det0-a det0-b"""
import json, glob, sys
def rows(a):
    f = glob.glob(f'runs/{a}/*/stats.jsonl')
    if not f: sys.exit(f'{a}: no stats.jsonl')
    return [json.loads(l) for l in open(f[0], encoding='utf-8') if l.strip()]
a, b = sys.argv[1:3]
A = rows(a); B = {r['t']: r for r in rows(b)}
same = 0; first = None
for r in A:
    q = B.get(r['t'])
    if q is None: continue
    d = {k: (r[k], q[k]) for k in r if k in q and r[k] != q[k]}
    if d:
        first = r['t']
        print(f'{a} vs {b}: identical through t={A[max(0, same-1)]["t"] if same else 0}; first difference at t={r["t"]}, {len(d)} fields, e.g. ' + ', '.join(f'{k}={v}' for k, v in list(d.items())[:4]))
        break
    same += 1
if first is None:
    print(f'{a} vs {b}: identical on all {same} shared samples (to t={A[-1]["t"]}); contacts/step at end {A[-1].get("contactPairsPerStep")}')
