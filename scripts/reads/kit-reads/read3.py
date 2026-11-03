# Read 3: matter density at each plant child's birth cell against its parent's cell at the same time (plants spec, section 3, test 5).
# Plant child = lineage birth row k=="r", pho==1, abs==0. Birth position = the child's first positions sample (every 10 s, so up to 10 s
# after the birth), parent position = the parent's row in that same sample (skipped if the parent is not in it). Matter = fields/*.matter.f32
# (units per 5 m cell, layout order (iy*nx+ix)*nz+iz, layer iy = floor(-y/5)), nearest dump in time. Density = stock / 125 m3.
import json, os, struct, sys, glob, math, bisect
import os as _os
REPO = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), '..', '..', '..')).replace(chr(92), '/')
sys.path.insert(0, REPO + '/scripts/reads')
import runrec
ROOT = REPO + '/runs'
def q(a, p):
    if not a: return None
    a = sorted(a); i = (len(a)-1)*p; lo = int(i); hi = min(lo+1, len(a)-1); return a[lo]+(a[hi]-a[lo])*(i-lo)
res = {}
for arm in sys.argv[1:]:
    run = glob.glob(f'{ROOT}/{arm}/*/')[0].rstrip('/' + chr(92))
    lay = json.load(open(run + '/fields/layout.json'))['matter']
    nx, ny, nz, cm = lay['cellsX'], lay['cellsY'], lay['cellsZ'], float(lay['cellMetres'])
    dumps = sorted(int(f[:9]) for f in os.listdir(run + '/fields') if f.endswith('.matter.f32'))
    cache = {}
    def field(t):
        k = min(dumps, key=lambda d: abs(d - t))
        if k not in cache:
            if len(cache) > 3: cache.clear()
            b = open(f'{run}/fields/{k:09d}.matter.f32', 'rb').read()
            cache[k] = struct.unpack('<%df' % (len(b)//4), b)
        return cache[k]
    vol = cm**3
    child = {}  # id -> parent
    nplant_births = 0; nplants_all = 0
    with open(run + '/lineage.jsonl', encoding='utf-8') as f:
        for l in f:
            if l.startswith('{"e":"b"'):
                r = json.loads(l)
                if r['k'] == 'r' and r['pho'] == 1 and r['abs'] == 0:
                    child[r['id']] = r['p']; nplant_births += 1
    seen = set(); pairs = []; noparent = 0; samples = 0
    for row in runrec.positions(run):
        samples += 1
        b = row['b']; ts = row['t']
        new = [x for x in b if x[0] in child and x[0] not in seen]
        if not new: continue
        pos = {x[0]: x for x in b}
        fl = field(ts); wm = sum(fl)/len(fl)/vol
        def dens(x, y, z):
            ix = min(max(int(x // cm), 0), nx-1); iz = min(max(int(z // cm), 0), nz-1); iy = min(max(int(-y // cm), 0), ny-1)
            return fl[(iy*nx + ix)*nz + iz] / vol, (ix, iy, iz)
        # world-wide mean density of the same layer, for context, only computed lazily
        for c in new:
            seen.add(c[0]); p = pos.get(child[c[0]])
            if p is None: noparent += 1; continue
            dc, cc = dens(c[1], c[2], c[3]); dp, cp = dens(p[1], p[2], p[3])
            pairs.append((ts, dc, dp, cc != cp, math.dist((c[1], c[2], c[3]), (p[1], p[2], p[3])), c[1], c[3], wm))
    n = len(pairs)
    rat = [dc/dp for _, dc, dp, _, _, _, _, _ in pairs if dp > 0]
    out = {'run': run, 'plant_birth_rows': nplant_births, 'seen_in_positions': len(seen), 'parent_not_in_sample': noparent, 'pairs': n, 'samples': samples,
           'dumps': (dumps[0], dumps[-1], len(dumps))}
    if n:
        out['child_density_q25_med_q75'] = [round(q([p[1] for p in pairs], x), 5) for x in (.25, .5, .75)]
        out['parent_density_q25_med_q75'] = [round(q([p[2] for p in pairs], x), 5) for x in (.25, .5, .75)]
        out['diff_child_minus_parent_q25_med_q75'] = [round(q([p[1]-p[2] for p in pairs], x), 5) for x in (.25, .5, .75)]
        out['ratio_child_over_parent_q25_med_q75'] = [round(q(rat, x), 3) for x in (.25, .5, .75)]
        out['share_different_cell'] = round(sum(p[3] for p in pairs)/n, 3)
        out['share_child_denser'] = round(sum(1 for p in pairs if p[1] > p[2])/n, 3)
        out['share_child_denser_among_diffcell'] = round(sum(1 for p in pairs if p[3] and p[1] > p[2])/max(1, sum(p[3] for p in pairs)), 3)
        out['share_child_ge_2x_parent'] = round(sum(1 for r_ in rat if r_ >= 2)/len(rat), 3)
        out['share_child_le_half_parent'] = round(sum(1 for r_ in rat if r_ <= .5)/len(rat), 3)
        out['child_parent_distance_m_q25_med_q75'] = [round(q([p[4] for p in pairs], x), 2) for x in (.25, .5, .75)]
        out['world_mean_density_median'] = round(q([p[7] for p in pairs], .5), 5)
        out['share_child_above_world_mean'] = round(sum(1 for p in pairs if p[1] > p[7])/n, 3)
        out['share_parent_above_world_mean'] = round(sum(1 for p in pairs if p[2] > p[7])/n, 3)
        out['share_parent_density_zero'] = round(sum(1 for p in pairs if p[2] == 0)/n, 3)
        out['share_child_density_zero'] = round(sum(1 for p in pairs if p[1] == 0)/n, 3)
        for lo, hi in ((0, 6000), (6000, 12000), (12000, 18000), (18000, 24001)):
            s = [p for p in pairs if lo <= p[0] < hi]
            if s: out[f'window_{lo}_{hi}'] = {'n': len(s), 'child_med': round(q([p[1] for p in s], .5), 5), 'parent_med': round(q([p[2] for p in s], .5), 5),
                'ratio_med': round(q([p[1]/p[2] for p in s if p[2] > 0], .5), 3), 'share_child_denser': round(sum(1 for p in s if p[1] > p[2])/len(s), 3)}
    res[arm] = out
    print(json.dumps({arm: out}), flush=True)
json.dump(res, open(REPO + '/scratch/kit-reads/out/read3.json', 'w'), indent=1)
