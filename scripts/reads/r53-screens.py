# Round 53's founding screens (D132, rounds/screens-r53.ps1 on the round-53 branch): the crowd, the
# leaves' uptake and the bed's water in each screen, against round 52 seed 1 over the same seconds.
# D132's rule: depth is taken only if it beats the larger budget (r53sc-f27) on the bed's water and
# on the leaves' uptake; otherwise round 53 takes the budget.
# Usage: python scripts/reads/r53-screens.py [arm ...]
import glob, json, os, sys
import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'runs')
BREAK_EVEN = 0.44   # J/m3 at a still stomach's mouth (scripts/reads/r52-larder.py)
MARKS = (2000, 5000, 7500, 10000)
ARMS = sys.argv[1:] or ['r52-s1', 'r53sc-f27', 'r53sc-b30', 'r53sc-b25', 'r53sc-a005']

def run_dir(arm):
    return sorted(glob.glob(os.path.join(ROOT, arm, '*', 'run.json')))[-1].rsplit(os.sep, 1)[0]

def rows(d):
    out = []
    with open(os.path.join(d, 'stats.jsonl')) as f:
        for line in f:
            out.append(json.loads(line))
    return out

def at(rs, t):
    best = [r for r in rs if r['t'] <= t]
    return best[-1] if best else None

def bed_snow(d, t):
    lay = json.load(open(os.path.join(d, 'fields', 'layout.json')))
    nx, nz = lay['bed']['cellsX'], lay['bed']['cellsZ']
    bed = np.fromfile(os.path.join(d, 'fields', 'bed.f32'), '<f4').reshape(nx, nz)
    files = sorted(glob.glob(os.path.join(d, 'fields', '*.snow-floor.f32')))
    files = [f for f in files if int(os.path.basename(f)[:9]) <= t]
    if not files:
        return None
    s = np.fromfile(files[-1], '<f4')
    live = s[bed.reshape(-1) > -1e9]
    return int(os.path.basename(files[-1])[:9]), float(live.mean()), float((live >= BREAK_EVEN).mean())

print('arm         |     t | alive | photo | absorb | jointed | upt lim | corpses eaten | settled J | bed snow J/m3 (at) | bed >= 0.44 | x real')
for arm in ARMS:
    try:
        d = run_dir(arm)
    except IndexError:
        print(f'{arm:11s} | no run'); continue
    rs = rows(d)
    status = json.load(open(os.path.join(d, 'run.json'))).get('status')
    for t in MARKS:
        r = at(rs, t)
        if r is None or r['t'] < t - 100:
            continue
        b = bed_snow(d, t)
        pace = r['t'] / (r['wallTotalMs'] / 1000) if r.get('wallTotalMs') else float('nan')
        bs = f'{b[1]:.3f} ({b[0]})' if b else '-'
        bf = f'{b[2]:.0%}' if b else '-'
        print(f"{arm:11s} | {r['t']:5.0f} | {r['alive']:5d} | {r['photosynthetic']:5d} | {r['absorptive']:6d} | {r['jointed']:7d} | "
              f"{r['uptakeLimitedShare']:.3f} | {r.get('corpsesEaten', 0):13d} | {r.get('corpseJoulesSettled', 0):9.0f} | {bs:18s} | {bf:11s} | {pace:.1f}")
    print(f'{arm:11s} | status {status}, last t {rs[-1]["t"]:.0f}')
