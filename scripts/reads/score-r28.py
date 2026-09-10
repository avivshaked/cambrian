"""Round 28 (logbook/0070) readings from stats.jsonl and the manifest; M1 is clade-score.ps1."""
import json,glob,sys
r18={1:1834,2:1722,3:1818,4:1828,5:1490}
for a in sys.argv[1:]:
    rows=[json.loads(l) for l in open(glob.glob(f'runs/{a}/*/stats.jsonl')[0]) if l.strip()]
    m=json.load(open(glob.glob(f'runs/{a}/*/run.json')[0]))
    seed=m['seed']; last=rows[-1]
    late=[r for r in rows if r['t']>10000]; after3=[r for r in rows if r['t']>3000]; after15=[r for r in rows if r['t']>15000]
    above=max(r.get('aboveSurface',0)/max(r['alive'],1) for r in after3)*100
    below=max(r.get('belowWorld',0) for r in rows)
    bad=0; prev=None
    for r in after15:
        if prev and (r['crowded']-prev['crowded'])>=(r['births']-prev['births']): bad+=1
        prev=r
    c=[r['contactPairsPerStep'] for r in late]
    print(f"{a}: alive {last['alive']} absorpt {last['absorptive']} inh {last['absorptiveInherited']} diverged {last.get('diverged',0)} wall {round(m['wallClockMinutes'])} {m['status']} {m.get('reason')} jobs {m.get('physicsJobWorkers')}")
    print(f"  M2 above max {above:.2f}% below {below} | M3 alive/r18x {last['alive']/r18[seed]:.2f} (r18x {r18[seed]}) | M4 inherited {last['absorptiveInherited']} | M5 crowd windows>=births after 15k {bad} of {len(after15)-1} | M6 contacts mean {sum(c)/len(c):.0f} min {min(c):.0f} max {max(c):.0f} | M8 founding min {min(r['alive'] for r in rows if r['t']<=6000)} | height t>10k {sum(r['meanHeight'] for r in late)/len(late):+.1f} | stock {rows[[r['t'] for r in rows].index(min([r['t'] for r in rows], key=lambda t: abs(t-20000)))]['matterStanding']:.0f}->{last['matterStanding']:.0f} | patches {last['alivePerPatch']}")
