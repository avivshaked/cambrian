"""Round 26 (logbook/0067) scoring from stats.jsonl: M2-M8. M1 is scripts/clade-score.ps1.
Usage: python3 scripts/reads/score-r26.py r26-s1 ... r26-s5"""
import json, glob, sys

for arm in sys.argv[1:]:
    files = glob.glob(f'runs/{arm}/*/stats.jsonl')
    if not files:
        print(arm, 'no stats.jsonl'); continue
    rows = []
    for line in open(files[0], encoding='utf-8'):
        line = line.strip()
        if not line: continue
        try: rows.append(json.loads(line))
        except Exception: pass
    if not rows:
        print(arm, 'empty'); continue
    last = rows[-1]
    def at(t): return min(rows, key=lambda r: abs(r['t'] - t))
    late = [r for r in rows if r['t'] > 10000]
    after3k = [r for r in rows if r['t'] > 3000]
    print(f"{arm}: t_last {last['t']:.0f} alive {last['alive']} absorpt {last['absorptive']} births {last['births']} diverged {last.get('diverged', 0)}")
    frac = max((r.get('aboveSurface', 0) / max(r['alive'], 1)) for r in after3k) if after3k else 0
    print(f"  M2 top/floor: max above/alive after 3000 {frac*100:.2f}% (hold if <= 0.5); max below {max(r.get('belowWorld', 0) for r in rows)}")
    if late:
        print(f"  M3 water: mean height t>10000 {sum(r['meanHeight'] for r in late)/len(late):+.1f} m (hold if < -5)")
    if last['t'] >= 30000:
        s20, s30 = at(20000), at(30000)
        print(f"  M4 stock: 20k {s20['matterStanding']:.0f} -> 30k {s30['matterStanding']:.0f} ratio {s30['matterStanding']/s20['matterStanding']:.2f} (hold if <= 1.15)")
    print(f"  M5 size: alive at end {last['alive']} (1500-5000); max {max(r['alive'] for r in rows)}")
    early = [r['alive'] for r in rows if r['t'] <= 6000]
    print(f"  M6 founding: min alive to 6000 {min(early)} (hold if >= 40)")
    # M7: crowded per window vs births per window after 15000
    prev = None; ok = None
    for r in rows:
        if prev is not None and r['t'] >= 15000:
            cw = r.get('crowdedWindow', r.get('crowded', 0) - prev.get('crowded', 0))
            bw = r['births'] - prev['births']
            if ok is None: ok = True
            if cw >= bw and bw > 0: ok = False
        prev = r
    tail = [r for r in rows if r['t'] >= 15000]
    cw_mean = sum(r.get('crowdedWindow', 0) for r in tail) / max(len(tail), 1)
    print(f"  M7 crowd: crowded/window below births/window at every sample from 15000: {ok}; mean crowded/window {cw_mean:.0f}; total crowded {last.get('crowded')} vs births {last['births']}")
    ct = [r.get('contactPairsPerStep', 0) for r in late]
    if ct:
        print(f"  M8 contact: contacts/step t>10000 min {min(ct):.0f} mean {sum(ct)/len(ct):.0f} max {max(ct):.0f} (100-5000)")
    pk = sorted(k for k in last if k.startswith('alivePerPatch'))
    if pk and late:
        v = last[pk[0]] if len(pk) == 1 else [last[k] for k in pk]
        print(f"  patches at end: {v}")
