# Read 1: what a birth leaves a parent. Grazer = birth row with atk>0 and ink>0 (g2-read.py's reading).
# Starvation = death row c == "starved". Age at death = death t - the creature's own birth row t.
import json, sys, glob, statistics as st
import os as _os
REPO = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), '..', '..', '..')).replace(chr(92), '/')
def q(a, p):
    if not a: return None
    a = sorted(a); i = (len(a)-1)*p; lo = int(i); hi = min(lo+1, len(a)-1)
    return round(a[lo] + (a[hi]-a[lo])*(i-lo), 1)
def summ(a): return {'n': len(a), 'q25': q(a,.25), 'med': q(a,.5), 'q75': q(a,.75), 'mean': round(st.mean(a),1) if a else None}
res = {}
for arm in sys.argv[1:]:
    path = glob.glob(f'{REPO}/runs/{arm}/*/lineage.jsonl')[0]
    prev_child = {}; gaps = {}
    born = {}; graz = {}; last_child = {}; nchild = {}; mode = {}
    deaths = []  # (id, t, cause)
    rows = 0; first_t = None; kinds = {}
    with open(path, encoding='utf-8') as f:
        for l in f:
            rows += 1
            if l.startswith('{"e":"b"'):
                r = json.loads(l)
                if first_t is None: first_t = r['t']
                i = r['id']; born[i] = r['t']
                kinds[r['k']] = kinds.get(r['k'], 0) + 1
                if r.get('atk', 0) > 0 and r.get('ink', 0) > 0: graz[i] = 1
                mode[i] = r.get('gm', 0)
                if r['k'] == 'r':
                    p = r['p']; last_child[p] = r['t']; nchild[p] = nchild.get(p, 0) + 1
                    if p in prev_child: gaps.setdefault(p, []).append(r['t'] - prev_child[p])
                    elif p in born: gaps.setdefault(p, []).append(r['t'] - born[p])
                    prev_child[p] = r['t']
            elif l.startswith('{"e":"d"'):
                r = json.loads(l)
                if r['id'] in graz: deaths.append((r['id'], r['t'], r['c']))
    out = {'path': path, 'rows': rows, 'first_birth_t': first_t, 'birth_kinds': kinds, 'grazers_with_birth_row': len(graz)}
    D = {'starved': [], 'other': []}
    cause_n = {}
    sv_since = []; sv_since_by_mode = {0: [], 1: []}; sv_never_age = []; sv_bred_age = []
    all_never_age = {}; nb_sv = 0; by_cause_since = {}; ratios = []
    for i, t, c in deaths:
        cause_n[c] = cause_n.get(c, 0) + 1
        if i not in born: continue
        age = t - born[i]; bred = i in last_child
        if not bred: all_never_age.setdefault(c, []).append(age)
        if bred: by_cause_since.setdefault(c, []).append(t - last_child[i])
        if c == 'starved':
            if bred:
                d = t - last_child[i]
                if i in gaps and len(gaps[i]) >= 2: ratios.append(d / st.mean(gaps[i]))
                sv_since.append(d); sv_since_by_mode[mode[i]].append(d); sv_bred_age.append(age)
            else: sv_never_age.append(age)
    out['grazer_deaths_by_cause_with_birth_row'] = cause_n
    out['starved_bred_time_since_last_birth_s'] = summ(sv_since)
    out['starved_bred_by_mode_lump'] = summ(sv_since_by_mode[0]); out['starved_bred_by_mode_gest'] = summ(sv_since_by_mode[1])
    out['starved_bred_age_at_death_s'] = summ(sv_bred_age)
    out['starved_never_bred_age_at_death_s'] = summ(sv_never_age)
    out['never_bred_age_at_death_by_cause'] = {c: summ(a) for c, a in all_never_age.items()}
    out['bred_time_since_last_birth_by_cause'] = {c: summ(a) for c, a in by_cause_since.items()}
    out['starved_bred_since_last_over_own_mean_gap'] = summ([x*100 for x in ratios])  # x100: percent of the creature's own mean birth gap (first gap from its own birth)
    allg = [g for k, v in gaps.items() if k in graz for g in v]
    out['all_grazer_birth_gaps_s'] = summ(allg)
    n = len(sv_since)
    out['share_starved_bred_within_s'] = {str(x): round(sum(1 for d in sv_since if d <= x)/n, 3) if n else None for x in (10, 30, 60, 120, 240, 500)}
    # inter-birth interval of grazers (births by same parent), for scale: need per-parent sequence; approximated via nchild/lifetime below
    res[arm] = out
    print(json.dumps({arm: out}, indent=1), flush=True)
json.dump(res, open(REPO + '/scratch/kit-reads/out/read1.json', 'w'), indent=1)
