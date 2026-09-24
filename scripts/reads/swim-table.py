import csv, collections
def rows(f): return list(csv.DictReader(open(f)))
def fl(x):
    try: return float(x)
    except: return float('nan')
for s in ['s1','s2','s3']:
    B={r['id']:r for r in rows(f'{s}-bodies.csv')}
    ins=[r for r in B.values() if int(r['poseSamples'])>=20]
    a=sum(1 for r in ins if int(r['poseActiveJoints'])>0 and fl(r['aloneAmp'])>=0.05)
    b=sum(1 for r in ins if int(r['poseActiveJoints'])>0 and fl(r['aloneAmp'])<0.05)
    c=sum(1 for r in ins if int(r['poseActiveJoints'])==0 and fl(r['aloneAmp'])>=0.05)
    d=sum(1 for r in ins if int(r['poseActiveJoints'])==0 and fl(r['aloneAmp'])<0.05)
    print(f'{s} in-situ active & alone active {a}; in-situ active, alone quiet {b}; in-situ quiet, alone active {c}; both quiet {d}')
def tab(s):
    B={r['id']:r for r in rows(f'{s}-bodies.csv')}
    R=collections.defaultdict(dict); C=collections.defaultdict(dict)
    for r in rows(f'{s}-strokes.csv'): R[r['id']][r['variant']]=r
    for r in rows(f'{s}clean-strokes.csv'): C[r['id']][r['variant']]=r
    print(f'\n{s}: | grp | id | parts/joints | types | len m | in-situ amp rad | brain Hz | brain amp | S | run: brain / passive m/s | clean: brain | sineIn | sineLag90 | asym | openIn | openLag90 | clean brain mm/stroke |')
    for i,(id,d) in enumerate(R.items()):
        b=B[id]; c=C[id]
        g=lambda x: x['netSpeed'] if x['lost']!='LOST' else 'LOST'
        ps=fl(c['brain']['perStroke'])*1000
        print(f"| {d['brain']['group']} | {id} | {b['parts']}/{b['joints']} | {b['jointTypes']} | {b['lengthM']} | {b['poseAmps']} | {c['brain']['leadHz']} | {c['brain']['leadAmp']} | {c['brain']['leadAsym']} | {g(d['brain'])} / {g(d['passive'])} | {g(c['brain'])} | {g(c['servoSineIn'])} | {g(c['servoSineLag90'])} | {g(c['servoAsym'])} | {g(c['openSineIn'])} | {g(c['openSineLag90'])} | {ps:.2f} |")
tab('s1')
