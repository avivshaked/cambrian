"""Round 41d's read (logbook/0108) at a named second, per seed, one line per prediction clause.

    python scripts/reads/r41d-read.py 15000 [r41d-s1 r41d-s2 r41d-s3]

E10's probe clause is read by hand with scripts/overlap/run.ps1 on the snapshot; this prints the
rest from stats.jsonl and run.json. E8 is the wall seconds per 1,000 simulated seconds per 1,000
living bodies over the window from 5,000 s to the named second (three arms ran throughout).
"""
import json, glob, math, sys

ROOT = 'D:/Projects/experiments/evolution-simulator'
BUDGET_UNITS = 3000  # rounds 41 to 41e; round 42 passes --budget 1500
RHO = 100
COLUMNS = 2211  # the uniform expectation's scale in E7, from 0107


def rows_of(arm):
    run = sorted(glob.glob(f'{ROOT}/runs/{arm}/2026*'))[-1]
    rows = [json.loads(l) for l in open(run + '/stats.jsonl', encoding='utf-8') if l.strip()]
    manifest = json.load(open(run + '/run.json', encoding='utf-8'))
    return rows, manifest


def window_pairs(by, t):
    r, p = by[t], by[t - 1000]
    alive = (r['alive'] + p['alive']) / 2
    return (r['contactPairs'] - p['contactPairs']) / ((t - (t - 1000)) / 0.01) / alive


def main():
    global BUDGET_UNITS
    argv = sys.argv[1:]
    if '--budget' in argv:
        i = argv.index('--budget'); BUDGET_UNITS = float(argv[i + 1]); del argv[i:i + 2]
    t = int(argv[0])
    arms = argv[1:] or ['r41d-s1', 'r41d-s2', 'r41d-s3']
    for arm in arms:
        rows, m = rows_of(arm)
        by = {int(r['t']): r for r in rows}
        if t not in by:
            print(f'{arm}: no row at {t} s (last {rows[-1]["t"]})')
            continue
        r = by[t]
        five = by.get(5000)
        alive = r['alive']
        print(f'== {arm} at {t} s: alive {alive}, status {m.get("status")} {m.get("reason") or ""}')
        print(f'  E1 alive {alive} (band 350 to 1,100 at 5,000 s; 600 to 3,600 at 30,000 s); at 5,000 s {five["alive"] if five else "?"}')
        if five:
            print(f'  E2 alive over 5,000 s: {alive / five["alive"]:.2f}x (>= 1.3 at 30,000 s); margin s {r.get("meanReserveMargin", float("nan")):.0f} (< 150 at 30,000 s)')
        print(f'  E3 upt lim {r["uptakeLimitedShare"] * 100:.0f}% (40 to 95%); mat top {r["matterSurface"]:.3f} vs deep {r["matterDeep"]:.3f} (top <= deep)')
        snow = r['detritusJoules'] / RHO / BUDGET_UNITS
        print(f'  E4 snow {snow:.2f} of budget (0.10 to 0.40)')
        inh_after = max((x.get('absorptiveInherited', 0) for x in rows if x['t'] > 5000), default=0)
        print(f'  E5 inherit max after 5,000 s {inh_after} (reaches 50 in 3 of 5); inherit now {r.get("absorptiveInherited")}')
        jointed_bs = sum(x['jointed'] * 100 for x in rows if x['t'] <= t)  # bodies x 100 s per row
        div = r.get('diverged', 0)
        print(f'  E7 diverged {div} in {jointed_bs / 1e6:.2f} M jointed body-s = {div / max(jointed_bs, 1) * 1e6:.1f} per M (0 to 10); '
              f'rim quarter {r["alivePerPatch"][3] / alive:.2f} (0.12 to 0.40); cols {r["occupiedColumns"]} vs uniform {r["totalColumns"] * (1 - math.exp(-alive / COLUMNS)):.0f} '
              f'= {r["occupiedColumns"] / (r["totalColumns"] * (1 - math.exp(-alive / COLUMNS))):.2f} (0.85 to 1.05)')
        if five and t > 5000:
            wall = (r['wallTotalMs'] - five['wallTotalMs']) / 1000
            body_s = sum(x['alive'] * 100 for x in rows if 5000 < x['t'] <= t)
            print(f'  E8 wall s per 1,000 sim s per 1,000 bodies, 5,000 to {t} s: {wall / (body_s / 1000) * 1000:.0f} (700 to 1,800); pace {(t - 5000) / wall:.2f}x')
        wins = [(k, window_pairs(by, k)) for k in range(4000, t + 1, 1000) if k in by and k - 1000 in by]
        print(f'  E9 pairs/body at {t} s window {wins[-1][1]:.2f} (< 1.0 at 15,000 s); by window ' + ' '.join(f'{k // 1000}k:{v:.2f}' for k, v in wins))
        print(f'  E10 probe by hand on snapshots/{t:09d}.jsonl: no body at 16 parts, < 1% at >= 8, self-pairs/body < 0.3')
        print(f'  recorded: jointed {r["jointed"]} ({r["jointed"] / alive * 100:.0f}%, inh {r["jointedInherited"]}), mean dof {r["dof"] / max(alive, 1):.2f}, audit {r["auditResidual"]:.1e}, mat resid {r["matterResidual"]:.1e}, wraps {r["wraps"]}')


if __name__ == '__main__':
    main()
