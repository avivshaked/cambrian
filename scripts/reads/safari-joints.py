import json,statistics as st,math
RUN=r"runs\r47-s1\2026-09-23-232448-7a7f5d41"
want={355:[],4603:[],2840:[],6117:[]}
with open(RUN+r"\poses.jsonl") as f:
    for line in f:
        r=json.loads(line)
        for b in r['bodies']:
            if b['id'] in want: want[b['id']].append((r['t'],b['q'],b['p']))
for i,rows in want.items():
    if not rows: print(i,'none'); continue
    n=len(rows[0][1])
    spans=[]
    for k in range(n):
        v=[q[k] for _,q,_ in rows if len(q)>k]
        spans.append((min(v),max(v),st.pstdev(v)))
    steps=[math.dist(rows[j][2],rows[j+1][2])/10 for j in range(len(rows)-1)]
    print(i,len(rows),'samples', 'joint dof',n,' '.join('[%.2f..%.2f sd %.2f rad]'%s for s in spans),'drift median %.3f m/s'%st.median(steps))
