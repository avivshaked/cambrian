"""Late matter balance over the last two lifetimes (6,000 s): standing-stock slope against
the influx, and burial against the influx. For the owner's balance ruling (review response
2026-09-06 item 8). Usage: python3 scripts/reads/late-balance.py r27-s1 ..."""
import json, glob, sys
for arm in sys.argv[1:]:
    files = glob.glob(f'runs/{arm}/*/stats.jsonl')
    if not files:
        print(arm, 'no stats'); continue
    rows = [json.loads(l) for l in open(files[0], encoding='utf-8') if l.strip()]
    last = rows[-1]['t']; win = [r for r in rows if r['t'] >= last - 6000]
    a, b = win[0], win[-1]; dt = b['t'] - a['t']
    slope = (b['matterStanding'] - a['matterStanding']) / dt
    influx = sum(r['matterInfluxWindow'] for r in win[1:]) / dt
    buried = sum(r['matterBuriedWindow'] for r in win[1:]) / dt
    print(f"{arm}: t {a['t']:.0f}->{b['t']:.0f}; standing {a['matterStanding']:.0f}->{b['matterStanding']:.0f}; "
          f"slope {slope*1000:.0f}/1000s = {slope/influx*100:.0f}% of influx {influx*1000:.0f}/1000s; "
          f"buried {buried*1000:.0f}/1000s = {buried/influx*100:.0f}% of influx")
