"""Why stomachs do not take hold in round 47: a read of lineage, absorptive log, positions,
snow column dumps and snapshots. Read-only. Writes only scratch/r47-stomachs/<seed>.json and
prints tables.

python scratch/r47-stomachs/stomachs.py <arm> <run dir>
"""
import importlib.util, json, math, os, sys, glob, re
from collections import defaultdict, Counter
import numpy as np

REPO = r"D:\Projects\experiments\evolution-simulator"
OUT = os.path.join(REPO, "scratch", "r47-stomachs")
spec = importlib.util.spec_from_file_location("r47read", os.path.join(REPO, "scripts", "reads", "r47-read.py"))
R = importlib.util.module_from_spec(spec)
spec.loader.exec_module(R)

BE = 0.44   # ledger break-even, logbook/specs/r46-read/ledger-stomachs.md
BREED = 1.0


def q(xs):
    xs = sorted(x for x in xs if x is not None and not (isinstance(x, float) and math.isnan(x)))
    if not xs:
        return dict(n=0)
    n = len(xs)
    return dict(n=n, q1=xs[n // 4], med=xs[n // 2], q3=xs[(3 * n) // 4], min=xs[0], max=xs[-1])


def main():
    arm, d = sys.argv[1], sys.argv[2]
    config = R.load_json(os.path.join(d, "config.json"))
    manifest = R.load_json(os.path.join(d, "run.json"))
    reefs, why = R.read_reefs(config, manifest)
    bed = R.read_bed(d, config)
    nx, nz = bed["nx"], bed["nz"]
    water = np.zeros(nx * nz)
    for ix in range(nx):
        for iz in range(nz):
            k = ix * nz + iz
            if bed["live"][k]:
                water[k] = max(0.0, R.column_water(bed, reefs, ix, iz))
    live = water > 0.5
    dumps = R.snow_dumps(d, 1e12)
    dump_ts = sorted(dumps)
    snow = {}   # t -> density per column (J/m3 of water), NaN off the water
    floorc = {}
    tank_mean = {}
    for t in dump_ts:
        cs = np.array(R.read_floats(dumps[t]), dtype=float)
        dens = np.full(nx * nz, np.nan)
        dens[live] = cs[live] / water[live]
        snow[t] = dens
        tank_mean[t] = cs[live].sum() / water[live].sum()
        fp = dumps[t].replace("snow-columns", "snow-floor")
        if os.path.exists(fp):
            floorc[t] = np.array(R.read_floats(fp), dtype=float)
    last_dump = dump_ts[-1]

    def col(x, z):
        ix = min(nx - 1, max(0, int(math.floor(x / bed["cell"]))))
        iz = min(nz - 1, max(0, int(math.floor(z / bed["cell"]))))
        return ix * nz + iz

    def dump_at(t):
        import bisect
        i = bisect.bisect_right(dump_ts, t) - 1
        return dump_ts[max(0, i)]

    def reef_info(x, z):
        if reefs is None:
            return None, None
        dist = min(math.hypot(x - reefs.xs[i], z - reefs.zs[i]) for i in range(reefs.count))
        inside = bool(reefs.inside_any_outline(x, z))
        return dist, inside

    # ---- lineage
    B = {}
    died = {}
    children = defaultdict(list)
    births_total = 0
    t_end = 0.0
    for r in R.stream_jsonl(os.path.join(d, "lineage.jsonl")):
        t_end = max(t_end, r.get("t", 0))
        if r["e"] == "b":
            births_total += 1
            B[r["id"]] = dict(t=r["t"], p=r["p"], abs=r.get("abs", 0), pho=r.get("pho", 0),
                              jnt=r.get("jnt", 0), ink=r.get("ink", 0), src=r.get("src"),
                              pool=r.get("pool"), g=r.get("g"))
            if r["p"] != -1:
                children[r["p"]].append(r["id"])
        elif r["e"] == "d":
            died[r["id"]] = r["t"]
    root_cache = {}

    def root(i):
        path = []
        while i not in root_cache and B.get(i, {}).get("p", -1) != -1:
            path.append(i)
            i = B[i]["p"]
        rr = root_cache.get(i, i)
        for p in path:
            root_cache[p] = rr
        return rr

    def source(i):
        b = B[i]
        if b["p"] == -1:
            return b["src"] or "floor"
        pb = B.get(b["p"])
        if pb is None:
            return "child:?"
        if pb["abs"]:
            return "child:stomach" if not pb["pho"] else "child:mixo"
        if pb["pho"]:
            return "child:photo"
        return "child:other"

    stom = [i for i, b in B.items() if b["abs"]]
    stom_set = set(stom)
    # children of stomachs (any flags) for Q3
    kids_of_stom = set(c for i in stom for c in children.get(i, []))

    # ---- absorptive log
    AL = defaultdict(list)
    for r in R.stream_jsonl(os.path.join(d, "absorptive.jsonl")):
        if "id" not in r:
            continue
        AL[r["id"]].append(r)

    # ---- positions pass
    want_first = {i: B[i]["t"] for i in stom_set | kids_of_stom}
    first_pos = {}
    parent_pos_at_birth = {}
    track = defaultdict(list)   # stomach id -> [(t, x, y, z)] at every 100 s
    snap_t = [5000, 15000, 30000]
    photo_at = {}
    flags_at = {}
    alive_at5000 = None
    last_pos_t = None
    need_by_t = sorted(want_first.items(), key=lambda kv: kv[1])
    ptr = 0
    pending = {}
    for row in R.stream_jsonl(os.path.join(d, "positions.jsonl")):
        t = row["t"]
        last_pos_t = t
        while ptr < len(need_by_t) and need_by_t[ptr][1] <= t:
            pending[need_by_t[ptr][0]] = need_by_t[ptr][1]
            ptr += 1
        is100 = abs(t / 100.0 - round(t / 100.0)) < 1e-9
        is_snap = int(round(t)) in snap_t and abs(t - round(t)) < 1e-9
        if not pending and not is100 and not is_snap:
            continue
        m = {b[0]: b for b in row["b"]}
        for i in list(pending):
            bt = pending[i]
            if t > bt + 10.0:
                del pending[i]
                continue
            if i in m:
                first_pos[i] = (t, m[i][1], m[i][2], m[i][3])
                p = B[i]["p"]
                if p in m:
                    parent_pos_at_birth[i] = (m[p][1], m[p][2], m[p][3])
                del pending[i]
        if is100:
            for i, b in m.items():
                if i in stom_set:
                    track[i].append((t, b[1], b[2], b[3]))
        if is_snap:
            tt = int(round(t))
            photo_at[tt] = [(b[1], b[2], b[3], b[4]) for b in row["b"]]
            flags_at[tt] = {b[0]: b[4] for b in row["b"]}
            if tt == 5000:
                alive_at5000 = set(m)
    last_t = int(last_pos_t // 100 * 100)
    if last_t not in photo_at:
        # fall back to the last whole-100 row: read again only that row
        for row in R.stream_jsonl(os.path.join(d, "positions.jsonl")):
            if abs(row["t"] - last_t) < 1e-9:
                photo_at[last_t] = [(b[1], b[2], b[3], b[4]) for b in row["b"]]
                flags_at[last_t] = {b[0]: b[4] for b in row["b"]}
                break

    # ---- per-stomach records
    recs = []
    for i in stom + sorted(kids_of_stom - stom_set):
        b = B[i]
        rec = dict(id=i, t=b["t"], p=b["p"], src=source(i), pho=b["pho"], abs=b["abs"],
                   jnt=b["jnt"], ink=b["ink"], g=b["g"], root=root(i),
                   rootsrc=B[root(i)]["src"] if root(i) in B else None,
                   died=died.get(i), nkids=len(children.get(i, [])),
                   kids=children.get(i, []),
                   kid_stomachs=sum(1 for c in children.get(i, []) if B[c]["abs"]))
        rec["life"] = (died[i] - b["t"]) if i in died else None
        rec["alive_end"] = i not in died
        fp = first_pos.get(i)
        if fp:
            _, x, y, z = fp
            k = col(x, z)
            dt = dump_at(b["t"])
            rec.update(x=x, y=y, z=z, depth=-y, col_snow=float(snow[dt][k]),
                       floor_cell=float(floorc[dt][k]) if dt in floorc else None,
                       bed_y=float(bed["floor"][k]))
            dist, inside = reef_info(x, z)
            rec.update(reef_dist=dist, under_cap=inside)
            pp = parent_pos_at_birth.get(i)
            if pp:
                rec["dist_parent"] = math.dist((x, y, z), pp)
                rec["parent_col_snow"] = float(snow[dt][col(pp[0], pp[2])])
        al = AL.get(i, [])
        if al:
            f = al[0]
            rec.update(dh0=f["densityHere"], dh0_t=f["t"], food0=f["foodW"], upk0=f["upkeepW"],
                       net0=f["netW"], light0=f["lightW"], absVol=f["absVolume"], vol=f["volume"],
                       mixo=f["mixotroph"], e0=f["energy"])
            live_rows = [r for r in al if not r["dead"]]
            if live_rows:
                rec["dh_med_life"] = float(np.median([r["densityHere"] for r in live_rows]))
                rec["net_med_life"] = float(np.median([r["netW"] for r in live_rows]))
                rec["food_med_life"] = float(np.median([r["foodW"] for r in live_rows]))
                rec["upk_med_life"] = float(np.median([r["upkeepW"] for r in live_rows]))
                rec["e_max"] = max(r["energy"] for r in live_rows)
                rec["dh_min_life"] = min(r["densityHere"] for r in live_rows)
                below = [r["t"] for r in live_rows if r["densityHere"] < BE]
                rec["first_dh_below_be"] = below[0] if below else None
        tr = track.get(i, [])
        if tr:
            rec["track_col"] = [(t, float(snow[t][col(x, z)]) if t in snow else None) for t, x, y, z in tr]
            if fp:
                k0 = col(fp[1], fp[3])
                rec["birthcol_series"] = [(t, float(snow[t][k0])) for t, *_ in tr if t in snow]
        recs.append(rec)

    # ---- plant sheet
    sheet = {}
    for tt in sorted(photo_at):
        bodies = photo_at[tt]
        ph = [(x, y, z) for x, y, z, f in bodies if f & 4]
        dt = dump_at(tt)
        dens = snow[dt]
        cnt = Counter(col(x, z) for x, y, z in ph)
        occupied = [k for k in cnt if not np.isnan(dens[k])]
        top = sorted(occupied, key=lambda k: -cnt[k])[:max(1, len(occupied) // 10)]
        body_w = [float(dens[col(x, z)]) for x, y, z in ph if not np.isnan(dens[col(x, z)])]
        live_d = dens[live]
        fl = floorc.get(dt)
        # living absorptive densityHere at the nearest absorptive-log sample
        dh = []
        for i, rows in AL.items():
            for r in rows:
                if abs(r["t"] - tt) < 5 and not r["dead"]:
                    dh.append(r["densityHere"])
        sheet[tt] = dict(
            dump=dt, n_photo=len(ph), n_all=len(bodies),
            photo_depth=q([-y for x, y, z in ph]),
            tank_mean=tank_mean[dt],
            col_q=q(list(live_d)),
            share_cols_above_be=float(np.mean(live_d > BE)),
            share_cols_above_1=float(np.mean(live_d > 1.0)),
            under_photo_bodyweighted=q(body_w),
            top10pct_photo_cols_mean=float(np.mean([dens[k] for k in top])) if top else None,
            unoccupied_cols_mean=float(np.nanmean([dens[k] for k in np.where(live)[0] if k not in cnt])),
            floor_share=(float(fl[live].sum() / (dens[live] * water[live]).sum()) if fl is not None else None),
            floor_cell_q=(q(list(fl[live])) if fl is not None else None),
            abs_densityHere=q(dh),
        )

    out = dict(arm=arm, run=d, t_end=t_end, last_pos_t=last_pos_t, births_total=births_total,
               n_trickle=sum(1 for b in B.values() if b["p"] == -1 and b["src"] == "trickle"),
               n_pool=sum(1 for b in B.values() if b["p"] == -1 and b["src"] == "pool"),
               n_floor=sum(1 for b in B.values() if b["p"] == -1 and (b["src"] in (None, "floor"))),
               n_photo_parent_births=sum(1 for b in B.values() if b["p"] != -1 and B.get(b["p"], {}).get("pho") and not B.get(b["p"], {}).get("abs")),
               n_stomach_parent_births=sum(1 for b in B.values() if b["p"] != -1 and B.get(b["p"], {}).get("abs")),
               n_mixo=sum(1 for b in B.values() if b["abs"] and b["pho"]),
               tank_mean_series={t: tank_mean[t] for t in dump_ts[::10]},
               sheet=sheet, recs=recs,
               alive5000_stomach_ids=sorted(i for i in (alive_at5000 or set()) if i in stom_set),
               reef_reason=why)

    # ---- Q6 snapshot genomes
    snaps = {}
    for tt in sorted(flags_at):
        sp = os.path.join(d, "snapshots", "%09d.jsonl" % tt)
        if not os.path.exists(sp):
            continue
        fl = flags_at[tt]
        tot = has_abs_reach = has_abs_any = expressed = unexpressed = 0
        unexpressed_ids = []
        for r in R.stream_jsonl(sp):
            tot += 1
            nodes = r["nodes"]
            seen = set()
            st = [r["root"]]
            while st:
                n = st.pop()
                if n in seen:
                    continue
                seen.add(n)
                for e in nodes[n]["edges"]:
                    st.append(e["child"])
            any_abs = any(n["cell"] == "absorptive" for n in nodes)
            reach_abs = any(nodes[n]["cell"] == "absorptive" for n in seen)
            has_abs_any += any_abs
            has_abs_reach += reach_abs
            f = fl.get(r["id"])
            if f is not None and f & 1:
                expressed += 1
            if reach_abs and f is not None and not (f & 1):
                unexpressed += 1
                unexpressed_ids.append(r["id"])
        snaps[tt] = dict(genomes=tot, abs_node_any=has_abs_any, abs_node_reachable=has_abs_reach,
                         abs_expressed_positions=expressed, reachable_but_unexpressed=unexpressed,
                         unexpressed_ids=unexpressed_ids[:20])
    out["snap_q6"] = snaps
    with open(os.path.join(OUT, arm + ".json"), "w") as f:
        json.dump(out, f)
    print("wrote", arm, len(recs), "records")


main()
