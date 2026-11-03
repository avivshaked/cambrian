# Read 2: drift of the reserve-margin gene (rm) on grazer births, and gestation's share (gm), window by window.
# Grazer = birth row with atk>0 and ink>0. Births counted: k == "r" (reproduction). Founders (k "f") reported apart.
import json, glob, os, re, sys, collections
import os as _os
REPO = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), '..', '..', '..')).replace(chr(92), '/')
ROOT = REPO + '/runs'
PREF = ('r62cf-', 'r63b-', 'r63c-', 'r63d-b-', 'r63s-on-', 'r63g-', 'r63h-', 'r64-', 'r64s2-', 'r64t-', 'r65-')
W = 4000
def grp(arm):
    m = re.match(r'(r6\d[a-z]*\d*)-', arm); return m.group(1) if arm.startswith('r6') else arm
def group_of(arm):
    for p in PREF:
        if arm.startswith(p):
            return p.rstrip('-') if p not in ('r63d-b-', 'r63s-on-') else p.rstrip('-')
    return None
vals = collections.defaultdict(list); gm = collections.defaultdict(lambda: [0, 0])
fvals = collections.defaultdict(list); used = []
for d in sorted(os.listdir(ROOT)):
    if not os.path.isdir(f'{ROOT}/{d}'): continue
    g = group_of(d)
    if g is None or 'preflight' in d: continue
    for run in sorted(glob.glob(f'{ROOT}/{d}/*/lineage.jsonl')):
        if os.path.getsize(run) < 1_000_000 or '-preflight' in run: continue
        n = 0; ng = 0; tmin = 1e9; tmax = 0
        with open(run, encoding='utf-8') as f:
            for l in f:
                if not l.startswith('{"e":"b"'): continue
                r = json.loads(l)
                if not (r.get('atk', 0) > 0 and r.get('ink', 0) > 0): continue
                n += 1
                w = int(r['t'] // W)
                if r['k'] == 'r':
                    ng += 1; tmin = min(tmin, r['t']); tmax = max(tmax, r['t'])
                    vals[('ALL', w)].append(r['rm']); vals[(g, w)].append(r['rm'])
                    gm[('ALL', w)][0] += r.get('gm', 0); gm[('ALL', w)][1] += 1
                    gm[(g, w)][0] += r.get('gm', 0); gm[(g, w)][1] += 1
                elif r['k'] == 'f':
                    fvals[w].append(r['rm'])
        used.append((os.path.relpath(run, ROOT), n, ng, round(tmin), round(tmax)))
def q(a, p):
    a = sorted(a); i = (len(a)-1)*p; lo = int(i); hi = min(lo+1, len(a)-1); return round(a[lo]+(a[hi]-a[lo])*(i-lo), 1)
out = {'used': used, 'window_s': W, 'cells': []}
for (g, w) in sorted(vals, key=lambda k: (k[0] != 'ALL', k[0], k[1])):
    a = vals[(g, w)]
    out['cells'].append({'group': g, 'window': f'{w*W}-{(w+1)*W}', 'n': len(a), 'q25': q(a, .25), 'med': q(a, .5), 'q75': q(a, .75),
        'share_ge_599': round(sum(1 for x in a if x >= 599)/len(a), 3), 'gest_share': round(gm[(g, w)][0]/gm[(g, w)][1], 4)})
out['founder_rm'] = {str(w*W): {'n': len(a), 'med': q(a, .5), 'q25': q(a, .25), 'q75': q(a, .75)} for w, a in sorted(fvals.items())}
json.dump(out, open(REPO + '/scratch/kit-reads/out/read2.json', 'w'), indent=1)
print(len(used), 'run files'); 
for u in used: print(u)
