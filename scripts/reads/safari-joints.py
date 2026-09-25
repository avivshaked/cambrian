import json,statistics as st,math,os,sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import runrec  # either record: poses.jsonl, or the state stream where a run has none
RUN=r"runs\r47-s1\2026-09-23-232448-7a7f5d41"
want={355:[],4603:[],2840:[],6117:[]}
for r in runrec.poses(RUN):
        for b in r['bodies']:
            if b['id'] in want: want[b['id']].append((r['t'],list(b['q']),list(b['p'])))
for i,rows in want.items():
    if not rows: print(i,'none'); continue
    n=len(rows[0][1])
    spans=[]
    for k in range(n):
        v=[q[k] for _,q,_ in rows if len(q)>k]
        spans.append((min(v),max(v),st.pstdev(v)))
    steps=[math.dist(rows[j][2],rows[j+1][2])/(rows[j+1][0]-rows[j][0]) for j in range(len(rows)-1)]
    print(i,len(rows),'samples', 'joint dof',n,' '.join('[%.2f..%.2f sd %.2f rad]'%s for s in spans),'drift median %.3f m/s'%st.median(steps))
