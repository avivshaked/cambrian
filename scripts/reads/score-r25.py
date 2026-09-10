"""Round 25 (logbook/0065) scoring from stats.jsonl: M1-M5, M7, M8. M6 is clade-score.ps1.
Usage: python3 scripts/reads/score-r25.py r25-s2 r25-s4 r25h-s2 r25h-s4 r25q-s2"""
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
    keys = set(last)
    def at(t): return min(rows, key=lambda r: abs(r['t'] - t))
    late = [r for r in rows if r['t'] > 10000]
    after3k = [r for r in rows if r['t'] > 3000]
    print(f"{arm}: t_last {last['t']:.0f} alive {last['alive']} absorpt {last['absorptive']} births {last['births']} diverged {last.get('diverged',0)}")
    ab = [r.get('aboveSurface', r.get('above', 0)) for r in after3k]
    bw = [r.get('belowWorld', 0) for r in after3k]
    print(f"  M1 top/bottom: max above after 3000 {max(ab) if ab else '-'}; max below {max(bw) if bw else '-'}")
    if late:
        print(f"  M2 film: mean height t>10000 {sum(r['meanHeight'] for r in late)/len(late):+.1f} m (hold if < -5)")
    early = [r['alive'] for r in rows if r['t'] <= 6000]
    print(f"  M3 founding: min alive to 6000 {min(early)} (hold if >= 40)")
    if last['t'] >= 20000:
        s15, s20 = at(15000), at(20000)
        print(f"  M4 stock: standing 15k {s15['matterStanding']:.0f} -> 20k {s20['matterStanding']:.0f} ratio {s20['matterStanding']/s15['matterStanding']:.2f}")
    pk = sorted(k for k in keys if k.startswith('patchAlive') or (len(k) == 2 and k[0] == 'p' and k[1].isdigit()))
    if pk and late:
        means = {k: sum(r[k] for r in late)/len(late) for k in pk}
        print(f"  M5 patches (mean alive t>10000): { {k: round(v) for k, v in means.items()} }")
    cr = last.get('crowded', last.get('crowdedStillbirths', None))
    ct = [r.get('contacts', r.get('contactPairsPerStep', 0)) for r in late] if late else []
    print(f"  M7 crowded {cr} ({(cr / max(last['births'],1) * 100) if isinstance(cr,(int,float)) else '?'}% of births); contacts mean t>10000 {sum(ct)/len(ct) if ct else '-'}")
    print(f"  M8 alive max {max(r['alive'] for r in rows)}; matterHere mean t>10000 {sum(r['matterHere'] for r in late)/len(late) if late else float('nan'):.3f}")
    if arm == sys.argv[1]:
        print('  keys:', sorted(k for k in keys if any(x in k.lower() for x in ('above','below','wrap','crowd','contact','patch'))))
