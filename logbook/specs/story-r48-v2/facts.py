# Reads every number round 48's story (version 2) puts on screen or in a chart, from the three
# runs, and writes facts.json beside this file. Read-only over runs/. Single process, streaming.
#   python scratch/story-v2/facts.py
import json, os, statistics, math
from collections import Counter, defaultdict
from runlib import Run, rows, med, ROOT

HERE = os.path.dirname(os.path.abspath(__file__))
F = {}

STATS = ['t', 'alive', 'photosynthetic', 'absorptive', 'jointed', 'meanAge', 'meanBirthInvestment',
         'gestationBirths', 'detritusExudedTotal', 'detritusDepositedTotal', 'detritusReturnedTotal',
         'matterInfluxedTotal', 'bodiesEaten', 'corpsesEaten', 'diverged', 'auditResidual',
         'matterResidual', 'poolSpawns', 'trickleSpawns', 'floorSpawns', 'births', 'deaths']

R = {a: Run(a, STATS) for a in ('r48-s1', 'r48-s2', 'r48-s3')}
s1, s2, s3 = R['r48-s1'], R['r48-s2'], R['r48-s3']


def st(run, t, k):
    return run.stats[t][k]


def last_t(run):
    return max(run.stats)


# ------------------------------------------------------------------ the world
w = {}
for a, run in R.items():
    cfg = run.config
    area = cfg['world']['worldAreaSquareMetres']
    radius = math.sqrt(area / math.pi)
    w[a] = {
        'area_m2': area, 'radius_m': radius, 'diameter_m': 2 * radius,
        'depth_m': cfg['world']['worldDepthMetres'],
        'reefs': len(run.manifest.get('reefs') or []),
        'reef_cover': cfg['reef']['reefCover'],
        'status': run.manifest['status'], 'simulated_s': run.manifest['simulatedSeconds'],
        'ending': (run.manifest.get('ending') or '')[:160],
        'last_stats_t': last_t(run),
        'surface_irradiance': cfg['light']['surfaceIrradiance'],
        'attenuation_depth': cfg['light']['attenuationDepth'],
        'matter_budget_units': cfg['world']['matterBudgetUnits'],
        'exudation_fraction': cfg['feeding']['exudationFraction'],
        'senescence_doubling_s': cfg['world']['senescenceDoublingSeconds'],
        'overhead_floor_J': cfg['economy']['perOffspringOverheadJoules'],
        'overhead_per_tissue': cfg['economy']['perOffspringOverheadPerTissueJoule'],
        'newborn_reserve_fraction': cfg['growth']['newbornReserveFraction'],
        'endowment_s': cfg['population']['founderEndowmentSeconds'],
        'founder_energy_J': cfg['population']['founderEnergyJoules'],
        'trickle_per_s': cfg['population']['foundingTricklePerSecond'],
        'pool_share': cfg['population']['foundingTricklePoolShare'],
        'floor_closes_s': cfg['population']['floorClosesAfterSeconds'],
        'minimum_population': cfg['population']['minimumPopulation'],
        'link_photo_efficiency': [c for c in cfg['cellTypes'] if c['id'] == 'link'][0]['photosyntheticEfficiency'],
        'leaf_efficiency': [c for c in cfg['cellTypes'] if c['id'] == 'photosynthetic'][0]['efficiency'],
        'work_cost_multiplier': cfg['economy']['workCostMultiplier'],
        'snow_sink_mps': cfg['world']['nutrientSinkMetresPerSecond'],
        'remineralisation_per_s': cfg['world']['remineralisationPerSecond'],
        'corpse_decay_per_s': cfg['field']['corpseDecayPerSecond'],
        'joules_per_unit': cfg['world']['joulesPerUnit'],
        'times_real_time': run.manifest.get('timesRealTime'),
    }
F['world'] = w

# Light by depth in open water: LightModel.IrradianceAt = I0 * exp(y / attenuation), y <= 0.
I0 = w['r48-s1']['surface_irradiance']; L = w['r48-s1']['attenuation_depth']
F['light_by_depth'] = [[d, round(I0 * math.exp(-d / L), 1)] for d in (0, 3, 6, 9, 12, 15, 18, 24, 30)]

# ------------------------------------------------------------------ the tanks over time
def pop_series(run, step=250):
    ts = sorted(t for t in run.stats if t % step == 0)
    return [[t, run.stats[t]['alive']] for t in [0] + ts if t in run.stats or t == 0]

pops = {}
for a, run in R.items():
    pts = [[0, 0]] + [[t, run.stats[t]['alive']] for t in sorted(run.stats) if t % 250 == 0]
    lt = last_t(run)
    if pts[-1][0] != lt:
        pts.append([lt, run.stats[lt]['alive']])
    rs = sorted((t, d['alive']) for t, d in run.stats.items())
    pk = max((r for r in rs if r[0] <= 6000), key=lambda r: r[1])
    lo = min((r for r in rs if pk[0] < r[0] <= 6000), key=lambda r: r[1])
    pops[a] = {'points': pts, 'founding_peak': pk, 'low_after': lo,
               'fall_share': 1 - lo[1] / pk[1], 'end': [lt, run.stats[lt]['alive']]}
F['population'] = pops

# ------------------------------------------------------------------ stats at named seconds
at = {}
for a, run, ts in (('r48-s1', s1, (2500, 3000, 5000, 6000, 7500, 10000, 12500, 15000, 20000, 25000, 27500, 30000)),
                   ('r48-s2', s2, (2500, 5000, 10000, 12500, 13690)),
                   ('r48-s3', s3, (1500, 2500, 3000, 3500, 4000, 6500, 26500, 30000))):
    at[a] = {str(t): {k: run.stats[t].get(k) for k in STATS} for t in ts if t in run.stats}
F['stats_at'] = at

# Where the snow came from: cumulative joules put into the snow field by living leaves'
# leak (detritusExudedTotal), by the dead (detritusDepositedTotal: corpses and stillborn
# orphans) and by feeders' waste (detritusReturnedTotal), at each run's last row.
snow = {}
for a, run in R.items():
    lt = last_t(run)
    e = run.stats[lt]['detritusExudedTotal']; dd = run.stats[lt]['detritusDepositedTotal']; rr = run.stats[lt]['detritusReturnedTotal']
    tot = e + dd + rr
    snow[a] = {'t': lt, 'leaked_J': e, 'dead_J': dd, 'waste_J': rr,
               'leaked_share': e / tot, 'dead_share': dd / tot, 'waste_share': rr / tot,
               'leaked_over_dead': e / dd}
F['snow_sources'] = snow

# ------------------------------------------------------------------ the primer body, 201 in tank 1
p = {}
b = s1.B[201]
a201 = s1.absrows(wanted_ids={201})[201]
row2500 = [d for d in a201 if d['t'] == 2500][0]
deathrow = [d for d in a201 if d['dead']][0]
g2500 = [d for d in s1.snapshot(2500) if d['id'] == 201][0]
cfg = s1.config
inv = g2500['reproduction']['investment']; brood = g2500['reproduction']['brood']; margin = g2500['reproduction']['margin']
tissue = row2500['tissue']
share = inv * tissue / brood
rf = cfg['growth']['newbornReserveFraction']
child_body = share * (1 - rf); child_reserve = share * rf
fee = max(cfg['economy']['perOffspringOverheadJoules'], cfg['economy']['perOffspringOverheadPerTissueJoule'] * child_body)
gate = inv * tissue + brood * fee + margin * row2500['upkeepW']
kids = sorted(s1.kids[201], key=lambda k: s1.B[k]['t'])
p.update({
    'id': 201, 'born': b['t'], 'parent': b['p'], 'parent_clade_root': s1.croot[b['p']],
    'parent_clade_name': s1.card_by_founder[s1.croot[b['p']]]['name'],
    'bud': b.get('bud'), 'budx': b.get('budx'), 'flags': (b['abs'], b['jnt'], b['pho']),
    'own_line_name': s1.card_by_founder[s1.croot[201]]['name'],
    'own_line_members_ever': s1.card_by_founder[s1.croot[201]]['members_ever'],
    'own_line_extinct_at': s1.card_by_founder[s1.croot[201]]['extinct_at'],
    'died': s1.D.get(201), 'cause': s1.cause.get(201),
    'nodes': [(n['cell'], n['shape'], n['joint']) for n in g2500['nodes']],
    'row2500': row2500, 'deathrow': deathrow,
    'genome_reproduction': g2500['reproduction'],
    'children': [(k, s1.B[k]['t'], s1.B[k]['abs'], s1.B[k]['pho'], s1.D.get(k)) for k in kids],
    'price': {'share_per_child_J': share, 'child_body_J': child_body, 'child_reserve_J': child_reserve,
              'fee_per_child_J': fee, 'per_child_J': child_body + child_reserve + fee,
              'litter_J': brood * (child_body + child_reserve + fee),
              'margin_J': margin * row2500['upkeepW'], 'gate_J': gate},
    'stomach_income_share': row2500['foodW'] / (row2500['foodW'] + row2500['lightW']),
    'stomach_volume_share': row2500['absVolume'] / row2500['volume'],
})
F['primer_201'] = p

# ------------------------------------------------------------------ lines: names, founders, counts
def card(run, name):
    c = run.card_by_name[name]
    return {'guide_index': c['clade'], 'founder': c['founder'], 'founded_at': c['founded_at'],
            'founder_source': c.get('founder_source'), 'guild': c['guild'], 'alive_at_end': c['alive_at_end'],
            'extinct_at': c['extinct_at'], 'peak': c['peak'], 'members_ever': c['members_ever'],
            'parent_clade_name': c.get('parent_clade_name'), 'parent_body': c.get('parent_body')}

lines = {
    'r48-s1': {n: card(s1, n) for n in ('Frondium nisimocrax', 'Frondium marufubens', 'Gastrella cidutis III',
                                         'Natogastrum plocrele', 'Vorafrons vabecrepis', 'Gastrophylla gifigofa')},
    'r48-s2': {n: card(s2, n) for n in ('Lamina taplenegax', 'Pinnifrons plisitricrens', 'Remiphylla pototrapa')},
    'r48-s3': {n: card(s3, n) for n in ('Gastrella cidutis', 'Thallus legatribens', 'Thallus migugis')},
}
F['lines'] = lines

def clade_counts(run, names, times):
    roots = {run.card_by_name[n]['founder']: n for n in names}
    ser = run.series(lambda i: run.croot[i], roots.keys(), times)
    return {roots[r]: ser[r] for r in roots}

# The two leaf lines of tank 1, and of tank 3, every 500 s.
T = list(range(0, 30001, 500))
F['bets'] = {
    'r48-s1': {'times': T, 'counts': clade_counts(s1, ['Frondium nisimocrax', 'Frondium marufubens'], T)},
    'r48-s3': {'times': T, 'counts': clade_counts(s3, ['Thallus legatribens', 'Thallus migugis'], T)},
}
for a, run, names in (('r48-s1', s1, ['Frondium nisimocrax', 'Frondium marufubens']),
                      ('r48-s3', s3, ['Thallus legatribens', 'Thallus migugis'])):
    for n in names:
        r0 = run.card_by_name[n]['founder']
        F['bets'][a].setdefault('peak', {})[n] = run.peak(lambda i: run.croot[i], r0)

# Children's size at birth ('bf', the share of its own adult body a child is born with) and
# litter size ('brood' from the parent's genome is not on the row; the row's bf is the child's),
# by line, in the window each line's contest was read at.
def birth_sizes(run, name, lo, hi):
    r0 = run.card_by_name[name]['founder']
    bfs = [b['bf'] for i, b in run.B.items() if b['k'] == 'r' and run.croot.get(b['p']) == r0 and lo <= b['t'] < hi]
    return {'n': len(bfs), 'median_bf': med(bfs)}

F['birth_sizes'] = {
    'r48-s1': {n: birth_sizes(s1, n, 5000, 15000) for n in ('Frondium nisimocrax', 'Frondium marufubens')},
    'r48-s3': {n: birth_sizes(s3, n, 20000, 30000) for n in ('Thallus legatribens', 'Thallus migugis')},
}
F['investments'] = {
    'r48-s1_10000': {n: s1.snap_median(10000, s1.card_by_name[n]['founder']) for n in ('Frondium nisimocrax', 'Frondium marufubens')},
    'r48-s1_2500': {n: s1.snap_median(2500, s1.card_by_name[n]['founder']) for n in ('Frondium nisimocrax', 'Frondium marufubens')},
    'r48-s3_30000': {n: s3.snap_median(30000, s3.card_by_name[n]['founder']) for n in ('Thallus legatribens', 'Thallus migugis')},
}
F['leads_2500'] = {
    'r48-s1': {n: s1.clade_alive(s1.card_by_name[n]['founder'], 2500) for n in ('Frondium nisimocrax', 'Frondium marufubens')},
    'r48-s3': {n: s3.clade_alive(s3.card_by_name[n]['founder'], 2500) for n in ('Thallus legatribens', 'Thallus migugis')},
}

# ------------------------------------------------------------------ the eaters
TE = list(range(3000, 11001, 100))
e = {'times': TE}
e['r48-s3'] = clade_counts(s3, ['Gastrella cidutis'], TE)
e['r48-s1'] = clade_counts(s1, ['Gastrella cidutis III', 'Natogastrum plocrele'], TE)
e['peaks'] = {
    'Gastrella cidutis (s3)': s3.peak(lambda i: s3.croot[i], 3318),
    'Gastrella cidutis III (s1)': s1.peak(lambda i: s1.croot[i], 4185),
    'Natogastrum plocrele (s1)': s1.peak(lambda i: s1.croot[i], 3575),
}
e['last_death'] = {'Gastrella cidutis (s3)': s3.clade_last_death(3318),
                   'Gastrella cidutis III (s1)': s1.clade_last_death(4185),
                   'Natogastrum plocrele (s1)': s1.clade_last_death(3575)}
e['alive'] = {'s3_4000': s3.clade_alive(3318, 4000), 's1_6000': s1.clade_alive(4185, 6000),
              's1_7500': s1.clade_alive(4185, 7500), 's1_5000': s1.clade_alive(4185, 5000)}
# Is every member of the two Gastrella lines a stomach alone (abs 1, jnt 0, pho 0)?
e['flags'] = {'3318': {str(k): n for k, n in Counter((s3.B[i]['abs'], s3.B[i]['jnt'], s3.B[i]['pho']) for i in s3.descendants(3318)).items()},
              '4185': {str(k): n for k, n in Counter((s1.B[i]['abs'], s1.B[i]['jnt'], s1.B[i]['pho']) for i in s1.descendants(4185)).items()}}
# 3318 and 4185 at landing: what they arrived holding, against what one child costs.
def landing(run, i):
    b = run.B[i]
    pool = run.config['population']['founderEnergyJoules'] * b['bf']
    kids = sorted(run.kids[i], key=lambda k: run.B[k]['t'])
    return {'born': b['t'], 'bf': b['bf'], 'purse_J': pool, 'endow_J': b.get('endow'),
            'start_reserve_J': pool + b.get('endow', 0), 'pool_index': b.get('pool'), 'src': b.get('src'),
            'first_child': (kids[0], run.B[kids[0]]['t']) if kids else None,
            'died': run.D.get(i), 'children_ever': len(kids)}
e['landing'] = {'3318': landing(s3, 3318), '4185': landing(s1, 4185)}
ab3 = s3.absrows(wanted_pairs={(3318, 3500), (3559, 6500)})
ab1 = s1.absrows(wanted_pairs={(4185, 5000)})
e['rows'] = {'3318@3500': ab3[(3318, 3500)][0], '3559@6500': ab3[(3559, 6500)][0], '4185@5000': ab1[(4185, 5000)][0]}
e['3559'] = {'born': s3.B[3559]['t'], 'died': s3.D.get(3559), 'cause': s3.cause.get(3559),
             'line_alive_6500': [m for m in s3.descendants(3318) if s3.alive(m, 6500)]}
# 3318's upkeep at 3,500 s against 3559's at 6,500 s: both one-part copies of pool genome 0?
e['3559_founder'] = s3.founder_of(3559)
F['eaters'] = e

# The pool newcomers: lives, breeders, and whether any line rooted in one was alive at the end.
pool = {}
for a, run in R.items():
    pf = [i for i, b in run.B.items() if b.get('src') == 'pool']
    died = [run.D[i] - run.B[i]['t'] for i in pf if i in run.D]
    bred = [i for i in pf if run.kids.get(i)]
    by_index = Counter(run.B[i].get('pool') for i in bred)
    lt = run.upto
    living_rooted = [i for i in run.B if run.alive(i, lt) and run.B[run.founder_of(i)].get('src') == 'pool']
    best_line = 0
    for i in pf:
        best_line = max(best_line, sum(1 for m in run.descendants(i) if run.alive(m, lt)))
    pool[a] = {'arrived': len(pf), 'died': len(died), 'median_life_s': med(died), 'bred': len(bred),
               'bred_by_pool_index': {str(k): n for k, n in by_index.items()}, 'living_rooted_at_end': [(i, run.B[i]['k']) for i in living_rooted],
               'largest_living_descent_at_end': best_line, 'read_at': lt}
F['pool'] = pool
F['pool_r47_median_life_s'] = {'r47-s1': 46, 'r47-s2': 24, 'r47-s3': 42,
                               'source': 'logbook/0119 F1 row: round 47 read 46, 24 and 42 s'}

# ------------------------------------------------------------------ the late stomach, 48048
v = {}
b = s1.B[48048]
v['birth'] = {'t': b['t'], 'parent': b['p'], 'bud': b.get('bud'), 'budx': b.get('budx'),
              'flags': (b['abs'], b['jnt'], b['pho']), 'parent_clade_root': s1.croot[b['p']],
              'parent_clade_name': s1.card_by_founder[s1.croot[b['p']]]['name'],
              'parent_born': s1.B[b['p']]['t'], 'parent_died': s1.D.get(b['p'])}
v['died'] = s1.D.get(48048)
a48 = s1.absrows(wanted_ids={48048})[48048]
r27 = [d for d in a48 if d['t'] == 27500][0]
v['row27500'] = r27
food = sum(d['foodW'] for d in a48 if not d['dead']); light = sum(d['lightW'] for d in a48 if not d['dead'])
v['life_food_share_sampled'] = food / (food + light)
v['life_rows'] = len([d for d in a48 if not d['dead']])
v['mean_age_27500'] = s1.stats[27500]['meanAge']
desc = s1.descendants(48048)
for t in (27500, 30000):
    al = [m for m in desc if s1.alive(m, t)]
    v['desc_%d' % t] = {'alive': len(al), 'flags': {str(k): n for k, n in Counter((s1.B[m]['abs'], s1.B[m]['jnt'], s1.B[m]['pho']) for m in al).items()}}
    v['line_%d' % t] = s1.clade_alive(48048, t)
v['line_peak'] = s1.peak(lambda i: s1.croot[i], 48048)
line48 = set(desc)
fs, vs = [], []
for d in rows(os.path.join(s1.dir, 'absorptive.jsonl'), 30000):
    if d['t'] == 30000 and d['id'] in line48 and not d['dead'] and d['mixotroph']:
        vs.append(d['absVolume'] / d['volume'])
        if d['foodW'] + d['lightW'] > 0:
            fs.append(d['foodW'] / (d['foodW'] + d['lightW']))
v['end_median_stomach_volume_share'] = med(vs); v['end_median_food_share'] = med(fs); v['end_n'] = len(vs)
F['late_48048'] = v

# Budded-stomach lines per seed (M3's reading): bud roots are birth rows whose bud names
# absorptive with abs 1 and pho 1; a root's line is every descendant; the living at the end.
m3 = {}
for a, run in R.items():
    roots = [i for i, b in run.B.items() if 'absorptive' in (b.get('bud') or '') and b['abs'] == 1 and b['pho'] == 1]
    best = sorted(((sum(1 for m in run.descendants(r) if run.alive(m, run.upto)), r) for r in roots), reverse=True)[:4]
    m3[a] = {'bud_roots': len(roots), 'best': best}
F['m3'] = m3

# Buds born and grown (M1, M2).
buds = {}
for a, run in R.items():
    br = [b for b in run.B.values() if b.get('bud')]
    buds[a] = {'bud_births': len(br), 'expressed': sum(1 for b in br if (b.get('budx') or 0) >= 1)}
F['buds'] = buds

# Stomach buds from Frondium nisimocrax leaves in tank 1: how many, and how far their lines went.
nis = s1.card_by_name['Frondium nisimocrax']['founder']
sb = [i for i, b in s1.B.items() if 'absorptive' in (b.get('bud') or '') and b['abs'] == 1 and s1.croot.get(b['p']) == nis]
F['nisimocrax_stomach_buds'] = {'n': len(sb), 'first_t': min(s1.B[i]['t'] for i in sb),
                                'one_body_only': sum(1 for i in sb if len(s1.descendants(i)) == 1)}

# ------------------------------------------------------------------ gestation (G1, G2)
gest = {}
for a, run in R.items():
    lt = run.upto
    fc = run.config['population']['floorClosesAfterSeconds']
    def share(lo, hi):
        ch = [b for b in run.B.values() if b['k'] != 'f' and lo <= b['t'] < hi]
        return (sum(1 for b in ch if b.get('gm') == 1) / len(ch)) if ch else None, len(ch)
    gest[a] = {'early': share(fc, fc + 5000), 'late': share(lt - 5000, lt + 1),
               'gestationBirths_end': run.stats[last_t(run)]['gestationBirths']}
F['gestation'] = gest

# ------------------------------------------------------------------ tank 2's stop
F['tank2'] = {'status': s2.manifest['status'], 'simulated_s': s2.manifest['simulatedSeconds'],
              'ending': s2.manifest.get('ending'), 'last_row': last_t(s2),
              'alive_last_row': s2.stats[last_t(s2)]['alive'],
              'lamina_12500': s2.clade_alive(s2.card_by_name['Lamina taplenegax']['founder'], 12500),
              'alive_12500': s2.stats[12500]['alive']}

# ------------------------------------------------------------------ end-of-run verdict readings
F['verdict'] = {a: {'alive_end': run.stats[last_t(run)]['alive'], 'meanAge_end': run.stats[last_t(run)]['meanAge'],
                    'meanBirthInvestment_end': run.stats[last_t(run)]['meanBirthInvestment'],
                    'photosynthetic_end': run.stats[last_t(run)]['photosynthetic'],
                    'absorptive_end': run.stats[last_t(run)]['absorptive'],
                    'jointed_end': run.stats[last_t(run)]['jointed'],
                    'bodiesEaten_end': run.stats[last_t(run)]['bodiesEaten'],
                    'diverged_end': run.stats[last_t(run)]['diverged'],
                    'causes': dict(Counter(run.cause.values()))} for a, run in R.items()}

with open(os.path.join(HERE, 'facts.json'), 'w', encoding='utf-8') as f:
    json.dump(F, f, indent=1, default=lambda o: list(o) if isinstance(o, tuple) else str(o))
print('facts.json written')
