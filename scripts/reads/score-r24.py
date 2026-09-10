"""Round 24 (logbook/0061) scoring: the M2-M7 numbers per arm from stats.jsonl.
M1 (the goal rule) is scripts/clade-score.ps1; M7 needs scripts/reads/parent-age.py for the median.
Usage: python3 scripts/reads/score-r24.py r24-s1 r24-s2 ..."""
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
    def at(t):
        return min(rows, key=lambda r: abs(r['t'] - t))
    last = rows[-1]
    s20, s30 = at(20000), at(30000)
    late = [r for r in rows if r['t'] > 10000]
    burial = [r['matterBuriedWindow'] / max(r['matterInfluxWindow'], 1e-9) for r in rows if r['t'] > 15000]
    early = [r['alive'] for r in rows if r['t'] <= 6000]
    depth = sum(r['meanHeight'] for r in late) / max(len(late), 1)
    print(f"{arm}: t_last {last['t']:.0f} alive {last['alive']} absorpt {last['absorptive']} diverged {last.get('diverged', 0)}")
    print(f"  M2 ceiling: max alive {max(r['alive'] for r in rows)}")
    print(f"  M3 stock: standing 20k {s20['matterStanding']:.0f} -> 30k {s30['matterStanding']:.0f}  ratio {s30['matterStanding']/s20['matterStanding']:.2f}  (hold if <= 1.30)")
    print(f"  M4 depth: mean height t>10000 {depth:+.1f} m  (hold if < -5)")
    print(f"  M5 burial: mean buried/influx t>15000 {sum(burial)/max(len(burial),1):.2f}  (hold if >= 0.40)")
    print(f"  M6 founding: min alive to 6000 s {min(early)}  (hold if >= 40)")
    print(f"  matterHere mean t>10000 {sum(r['matterHere'] for r in late)/max(len(late),1):.3f}; matterDeep {sum(r['matterDeep'] for r in late)/max(len(late),1):.2f}")
