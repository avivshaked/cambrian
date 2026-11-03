# Read 4: share of plant kill rows that took the root. Plant = victim whose birth row has pho==1 and abs==0 (the leaf guild).
# A kill row ("e":"k") has root=1 when the part bitten off was part 0 OR the body had only one part (WorldMouth.KillPart), and parts = parts lost
# (the whole body's part count on a root kill). So single-part victims are split from multi-part ones below.
import json, glob, collections, sys
import os as _os
REPO = _os.path.abspath(_os.path.join(_os.path.dirname(__file__), '..', '..', '..')).replace(chr(92), '/')
ROOT = REPO + '/runs'
res = {}
for arm in sys.argv[1:]:
    run = glob.glob(f'{ROOT}/{arm}/*/lineage.jsonl')[0]
    plant = set(); other = set(); kills = []; dcause = collections.Counter(); nrows = 0
    with open(run, encoding='utf-8') as f:
        for l in f:
            nrows += 1
            if l.startswith('{"e":"b"'):
                r = json.loads(l)
                (plant if (r['pho'] == 1 and r['abs'] == 0) else other).add(r['id'])
            elif l.startswith('{"e":"k"'):
                r = json.loads(l); kills.append((r['id'], r['root'], r['parts'], r.get('part', 0), r['t']))
            elif l.startswith('{"e":"d"'):
                r = json.loads(l)
                if r['id'] in plant: dcause[r['c']] += 1
    pk = [k for k in kills if k[0] in plant]
    n = len(pk)
    root = [k for k in pk if k[1]]; nonroot = [k for k in pk if not k[1]]
    single = [k for k in root if k[2] <= 1]; multi_root = [k for k in root if k[2] >= 2]
    out = {'run': run, 'rows': nrows, 'kill_rows_all': len(kills), 'kill_rows_plant_victim': n, 'kill_rows_other_victim': sum(1 for k in kills if k[0] in other), 'kill_rows_unknown_victim': sum(1 for k in kills if k[0] not in plant and k[0] not in other),
           'plant_deaths_by_cause': dict(dcause)}
    if n:
        out.update({'root_share_all_plant_kills': round(len(root)/n, 4), 'root_single_part': len(single), 'root_multi_part': len(multi_root), 'nonroot_part_kills': len(nonroot),
            'multi_part_events': len(multi_root)+len(nonroot),
            'root_share_of_multi_part_events': round(len(multi_root)/max(1, len(multi_root)+len(nonroot)), 4),
            'parts_lost_by_root_multi_total': sum(k[2] for k in multi_root), 'parts_lost_by_nonroot_total': sum(k[2] for k in nonroot),
            'share_of_blades_lost_taken_by_root_bites_multi': round(sum(k[2] for k in multi_root)/max(1, sum(k[2] for k in multi_root)+sum(k[2] for k in nonroot)), 4),
            'root_parts_hist': dict(sorted(collections.Counter(min(k[2], 6) for k in root).items())),
            'nonroot_parts_hist': dict(sorted(collections.Counter(min(k[2], 6) for k in nonroot).items())),
            'share_plant_kills_in_windows': {f'{a}-{b}': (lambda s: [len(s), round(sum(1 for k in s if k[1])/len(s), 3) if s else None])([k for k in pk if a <= k[4] < b]) for a, b in ((0, 6000), (6000, 12000), (12000, 18000), (18000, 24001))}})
    res[arm] = out; print(json.dumps({arm: out}), flush=True)
json.dump(res, open(REPO + '/scratch/kit-reads/out/read4.json', 'w'), indent=1)
