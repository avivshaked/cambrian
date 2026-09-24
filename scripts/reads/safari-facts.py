"""Scratch read for the safari review: per-clade facts the cards do not carry."""
import json, os, statistics as st, collections, math, sys
RUN = r"D:\Projects\experiments\evolution-simulator\runs\r47-s1\2026-09-23-232448-7a7f5d41"
g = json.load(open(os.path.join(RUN, 'guide', 'guide.json'), encoding='utf-8'))
cards = {c['clade']: c for c in g['cards']}
byname = {c['name']: c for c in g['cards']}
# rebuild clade membership from lineage (same rule as guide.py)
births = {}; deaths = {}
with open(os.path.join(RUN, 'lineage.jsonl')) as f:
    for line in f:
        r = json.loads(line)
        if r['e'] == 'b': births[r['id']] = r
        else: deaths[r['id']] = r['t']
clade_of = {}
founder_clade = {c['founder']: c['clade'] for c in g['cards']}
for i in sorted(births):
    b = births[i]; p = b['p']
    trip = (b['abs'], b['jnt'], b['pho'])
    if p >= 0 and p in births and (births[p]['abs'], births[p]['jnt'], births[p]['pho']) == trip and p in clade_of:
        clade_of[i] = clade_of[p]
    else:
        clade_of[i] = founder_clade.get(i, -i)
children = collections.Counter(b['p'] for b in births.values() if b['p'] >= 0)
END = 30000
def life(i): return deaths.get(i, END) - births[i]['t']
all_life = [life(i) for i in births]
all_kids = [children[i] for i in births]
print('crowd: median life %.0f s, p90 %.0f; median children %d, p90 %d, max %d' % (
    st.median(all_life), sorted(all_life)[int(.9*len(all_life))], st.median(all_kids),
    sorted(all_kids)[int(.9*len(all_kids))], max(all_kids)))
names = ['Pinnifrons febofila','Pinnifrons crimisis','Lamina gosurolis','Pinnifrons filimens','Remiphylla plaguplax',
         'Pinnula vibaresa','Phagothallus balosax','Gastrophylla sefecis','Pinnifrons geperax','Lamina tripleplis']
exemplars = {'Pinnifrons febofila':355,'Pinnifrons crimisis':423,'Lamina gosurolis':248,'Pinnifrons filimens':1816,
 'Remiphylla plaguplax':6117,'Pinnula vibaresa':2840,'Phagothallus balosax':148,'Gastrophylla sefecis':10891,
 'Pinnifrons geperax':4603,'Lamina tripleplis':13078}
filmed = {'Pinnifrons febofila':2500,'Pinnifrons crimisis':1620,'Lamina gosurolis':1100,'Pinnifrons filimens':10000,
 'Remiphylla plaguplax':20000,'Pinnula vibaresa':12500,'Phagothallus balosax':2710,'Gastrophylla sefecis':25000,
 'Pinnifrons geperax':17500,'Lamina tripleplis':27500}
members = collections.defaultdict(list)
for i, c in clade_of.items(): members[c].append(i)
for n in names:
    c = byname[n]; ms = members[c['clade']]
    lives = [life(i) for i in ms]; kids = [children[i] for i in ms]
    best = max(ms, key=lambda i: children[i])
    oldest = max(ms, key=life)
    e = exemplars[n]
    print('\n%s (clade %d): %d members; member life median %.0f s max %.0f s (body %d); children median %d max %d (body %d)' % (
        n, c['clade'], len(ms), st.median(lives), life(oldest), oldest, st.median(kids), children[best], best))
    if e in births:
        b = births[e]
        print('  exemplar %d: born %.0f, died %s, age at film %.0f s, children %d, gen %d, in clade? %s' % (
            e, b['t'], deaths.get(e), filmed[n]-b['t'], children[e], b['g'], clade_of.get(e)==c['clade']))
    fl = births[c['founder']]
    print('  founder %d: life %.0f s, children %d' % (c['founder'], life(c['founder']), children[c['founder']]))
    # descendant clades split off
    kids_cl = [k for k in g['cards'] if k.get('parent_clade') == c['clade']]
    big = sorted(kids_cl, key=lambda k: -k['members_ever'])[:3]
    print('  daughter clades: %d; largest %s' % (len(kids_cl), [(k['name'], k['guild'], k['members_ever']) for k in big]))
