import glob,json,sys,statistics as st
def rows(f):
    d={}
    for l in open(f,encoding='utf-8'):
        if l.startswith('| ') and not l.startswith('| t '):
            c=[x.strip().strip('*') for x in l.strip().strip('|').split('|')]
            if c[0].isdigit(): d[int(c[0])]=c
    return d
for a in sys.argv[1:]:
    r=rows(f'runs/{a}.md'); ts=sorted(r); last=ts[-1]
    inh=[(t,int(r[t][14])) for t in ts]
    best=cur=0; bs=be=None
    for t,v in inh:
        if v>0:
            cur+=1
            if cur==1: start=t
            if cur>best: best=cur; bs=start; be=t
        else: cur=0
    mx=max(v for t,v in inh); pk=[t for t,v in inh if v==mx][0]
    f10=[t for t,v in inh if v>=10]; first10=f10[0] if f10 else None
    lin=glob.glob(f'runs/{a}/*/lineage.jsonl')[0]
    born={}; absb_last=0
    for l in open(lin,encoding='utf-8',errors='ignore'):
        try: j=json.loads(l)
        except: continue
        if 'p' in j and j.get('abs')==1:
            born[j.get('id')]=j
            if (j.get('t') or 0)>last-2000: absb_last+=1
    rf=rm=0
    for j in born.values():
        p=j.get('p')
        if p==-1 or p is None: rf+=1
        elif p not in born: rm+=1
    late=[t for t in ts if t>10000]
    W=lambda i: st.mean(float(r[t][i]) for t in late)/100
    ended='Ended' in open(f'runs/{a}.md',encoding='utf-8').read()
    c1=int(r[last][1])>0; c2=best>=20; c3=int(r[last][14])>=10; c4=absb_last>=1
    print(f"{a}: ended={ended} alive={r[last][1]} maxalive={max(int(r[t][1]) for t in ts)} | run {best} ({bs}-{be}) peak {mx}@{pk} first>=10 {first10} inherit@end {r[last][14]} absbirths last20 {absb_last} | clauses {c1} {c2} {c3} {c4} -> {'PASS' if all([c1,c2,c3,c4]) else 'fail'} | roots founders {rf} mutants {rm} | t>10000 W: in {W(45):.2f} exuded {W(47):.2f} out {W(46):.2f}; J/m3 {st.mean(float(r[t][16]) for t in late):.2f} depth {st.mean(float(r[t][19]) for t in late):.1f} | audit {set(r[t][35] for t in ts)} floor>3100 {set(r[t][32] for t in ts if t>3100)} abslogged@end {r[last][48] if len(r[last])>48 else '-'}")
    tin=sum(float(r[t][45]) for t in ts); tex=sum(float(r[t][47]) for t in ts); tout=sum(float(r[t][46]) for t in ts)
    print(f"   identity in+ex-out {tin+tex-tout:.1f} vs stock {r[last][15]}")
