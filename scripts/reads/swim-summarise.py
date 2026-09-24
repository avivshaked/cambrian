import csv, statistics as st, collections, sys
def rows(f): return list(csv.DictReader(open(f)))
def fl(x):
    try: return float(x)
    except: return float('nan')
def med(v): v=[x for x in v if x==x]; return st.median(v) if v else float('nan')
def pct(v,p): v=sorted(x for x in v if x==x); return v[min(len(v)-1,int(p*len(v)))] if v else float('nan')
for s in ['s1','s2','s3']:
    B=rows(f'{s}-bodies.csv'); C=rows(f'{s}clean-bodies.csv')
    print(f'==== {s}: jointed bodies alive at 30000 = {len(B)}')
    withp=[r for r in B if int(r['poseSamples'])>=20]
    print(f'  with >=20 pose samples in [28000,30000]: {len(withp)}')
    print('  joints per body:', dict(sorted(collections.Counter(int(r['joints']) for r in B).items())))
    jt=collections.Counter(t for r in B for t in r['jointTypes'].split('+')); print('  joint types:', dict(jt))
    print('  ACTIVE joints (pose amp >=0.05 rad) per body:', dict(sorted(collections.Counter(int(r['poseActiveJoints']) for r in withp).items())))
    amps=[float(a) for r in withp for a in r['poseAmps'].split()]
    print(f'  pose joint amplitude (sqrt2*sd) rad: median {med(amps):.3f} p75 {pct(amps,.75):.3f} p90 {pct(amps,.9):.3f} max {max(amps):.3f}; share >=0.05: {sum(a>=0.05 for a in amps)/len(amps):.2f}')
    cls=collections.Counter(); bodycls=collections.Counter()
    for r in withp:
        if int(r['poseActiveJoints'])<2: continue
        kinds=[]
        for p in r['pairs(a-b:chain:corr)'].split():
            a,ch,c=p.split(':'); c=float(c)
            k='in' if c>0.7 else 'anti' if c<-0.7 else 'lag'
            cls[(ch,k)]+=1; kinds.append(k)
        bodycls['any lag' if 'lag' in kinds else 'all in/anti']+=1
    print('  pose pairs (c=parent-child, s=other) by corr class:', dict(cls)); print('  multi-active bodies:', dict(bodycls))
    # alone brain
    ok=[r for r in B if r['lost']!='LOST']
    print(f'  alone brain: lost {len(B)-len(ok)}')
    hz=[fl(r['aloneHz']) for r in ok if fl(r['aloneAmp'])>=0.05]
    print(f'  alone active (lead amp>=0.05): {len(hz)} of {len(ok)}; Hz median {med(hz):.2f} p10 {pct(hz,.1):.2f} p90 {pct(hz,.9):.2f}')
    print('  alone active joints per body:', dict(sorted(collections.Counter(int(r['aloneActive']) for r in ok).items())))
    lags=collections.Counter(); lagb=collections.Counter()
    for r in ok:
        if int(r['aloneActive'])<2: continue
        ks=[]
        for p in r['alonePairLags'].split():
            a,ch,d=p.split(':'); d=float(d); k='in' if d<45 else 'anti' if d>135 else 'lag'; lags[(ch,k)]+=1; ks.append(k)
        lagb['any lag' if 'lag' in ks else 'all in/anti']+=1
    print('  alone pair lags:', dict(lags), dict(lagb))
    for lab,R in (('run water',ok),('clean water',[r for r in C if r['lost']!='LOST'])):
        act=[r for r in R if fl(r['aloneAmp'])>=0.05]; ina=[r for r in R if fl(r['aloneAmp'])<0.05]
        for nm,S in (('stroking',act),('not stroking',ina)):
            v=[fl(r['aloneNetSpeed']) for r in S]; ps=[fl(r['alonePerStroke']) for r in S]
            if v: print(f'  {lab:11s} {nm:12s} n={len(S):4d}  net horiz speed m/s median {med(v):.5f} p90 {pct(v,.9):.5f} max {max(v):.5f}; per stroke median {med(ps):.5f} m')
        asy=[fl(r['aloneAsym']) for r in act]
        if asy: print(f'  {lab} stroke asymmetry S (lead joint) median {med(asy):.3f} p90 |S| {pct([abs(a) for a in asy],.9):.3f}')
