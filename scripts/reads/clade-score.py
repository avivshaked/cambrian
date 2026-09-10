import glob,json,sys
def rows(f):
    d={}
    for l in open(f,encoding='utf-8'):
        if l.startswith('| ') and not l.startswith('| t '):
            c=[x.strip().strip('*') for x in l.strip().strip('|').split('|')]
            if c[0].isdigit(): d[int(c[0])]=c
    return d
for a in sys.argv[1:]:
    r=rows(f'runs/{a}.md'); samples=sorted(r); last=samples[-1]
    f=glob.glob(f'runs/{a}/*/lineage.jsonl')[0]
    birth={}; death={}
    for l in open(f,encoding='utf-8',errors='ignore'):
        try: j=json.loads(l)
        except: continue
        if j.get('e')=='b': birth[j['id']]=j
        elif j.get('e')=='d': death[j['id']]=j['t']
    # clade root: absorptive birth whose parent is not absorptive (or founder)
    root={}
    for i in sorted(birth):
        b=birth[i]
        if b.get('abs')!=1: continue
        p=b['p']; pb=birth.get(p)
        if p==-1 or pb is None or pb.get('abs')!=1: root[i]=i
        else: root[i]=root.get(p, p)
    clades={}
    for i,rt in root.items(): clades.setdefault(rt,[]).append(i)
    def alive_at(members,t): return sum(1 for m in members if birth[m]['t']<=t and death.get(m,1e12)>t)
    best=None
    for rt,mem in clades.items():
        at_end=alive_at(mem,last)
        if at_end==0: continue
        # consecutive samples with >=1 alive ending at last
        cons=0
        for t in reversed(samples):
            if alive_at(mem,t)>=1: cons+=1
            else: break
        recent=sum(1 for m in mem if birth[m]['t']>last-2000 and birth[m]['p']!=-1 and birth.get(birth[m]['p'],{}).get('abs')==1)
        first_t=min(birth[m]['t'] for m in mem); rk=birth[rt].get('k')
        ge10=[t for t in samples if alive_at(mem,t)>=10]
        rec=(at_end,cons,recent,rt,rk,first_t,len(mem),ge10[0] if ge10 else None, min(alive_at(mem,t) for t in samples if ge10 and t>=ge10[0]) if ge10 else None)
        if best is None or rec[0]>best[0]: best=rec
    at_end,cons,recent,rt,rk,first_t,n,first10,minsince=best
    verdict='PASS' if (cons>=20 and at_end>=10 and recent>=1) else 'fail'
    print(f"{a}: clades with a living member at {last}: {sum(1 for m in clades.values() if alive_at(m,last)>0)} | largest: root {rt} (kind {rk}, born {first_t}), {n} members ever, {at_end} alive at end, alive-streak {cons} samples, first>=10 at {first10}, min since {minsince}, inherited births in last 20 samples {recent} -> clade {verdict} | aggregate inherit@end {r[last][14]}")
