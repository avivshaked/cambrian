import csv, statistics as st, collections
def rows(f): return list(csv.DictReader(open(f)))
def fl(x):
    try: return float(x)
    except: return float('nan')
def med(v): v=[x for x in v if x==x]; return st.median(v) if v else float('nan')
V=['brain','passive','servoSineIn','servoSineLag90','servoAsym','openSineIn','openSineLag90']
for s in ['s1','s2','s3']:
    B={r['id']:r for r in rows(f'{s}-bodies.csv')}
    # why not stroking
    ns=[r for r in B.values() if fl(r['aloneAmp'])<0.05]
    sig=[fl(r['aloneSigRms']) for r in ns]
    print(f'{s} non-stroking {len(ns)}: brain drive-signal RMS on lead dof: median {med(sig):.3f}; share <0.05 {sum(x<0.05 for x in sig)/len(sig):.2f}; share >0.3 {sum(x>0.3 for x in sig)/len(sig):.2f}')
    lens=[fl(r['lengthM']) for r in B.values()]; print(f'   jointed body length m median {med(lens):.2f}, mass kg median {med([fl(r["mass"]) for r in B.values()]):.2f}')
    for env in ['','clean']:
        R=rows(f'{s}{env}-strokes.csv'); by=collections.defaultdict(dict)
        for r in R: by[(r['group'],r['id'])][r['variant']]=r
        print(f'  {s} {env or "run"} water, 25 chosen: median net horizontal speed m/s (and in body lengths/s) by variant; multi-joint bodies separately')
        for sub,filt in (('all',lambda k,d: True),('>=2 joints',lambda k,d: int(d['brain']['joints'])>=2),('1 joint',lambda k,d: int(d['brain']['joints'])==1)):
            items=[(k,d) for k,d in by.items() if filt(k,d)]
            if not items: continue
            line=f'    {sub:10s} n={len(items):2d} '
            for v in V:
                sp=[fl(d[v]['netSpeed']) for k,d in items if d[v]['lost']!='LOST']
                bl=[fl(d[v]['netSpeed'])/fl(d[v]['lengthM']) for k,d in items if d[v]['lost']!='LOST']
                line+=f'{v}={med(sp):.4f} ({med(bl):.4f}) '
            print(line)
