"""Round 48 snow screens: read each arm at the marks. Read-only; prints one TSV-ish table.

python scratch/r48-snow/snow-read.py <runs root> <arm> [<arm> ...] [--marks 5000,10000,15000]

Per arm and mark:
  snow tank mean  J/m3 of water: sum of column joules over sum of column water (the bed less
                  the reef's rock; scripts/reads/r47-read.py's column_water), live columns only,
                  from fields/NNNNNNNNN.snow-columns.f32 at or before the mark. Same arithmetic as
                  scratch/r47-stomachs/stomachs.py's tank_mean.
  >0.44, >1       share of live columns whose own density exceeds the value.
  top10 plant     mean column density over the densest 10% of occupied columns by photosynthetic
                  body count (positions.jsonl flag 4 at the mark), stomachs.py's definition.
  alive, photo, upt lim (uptakeLimitedShare), mat resid (matterResidual), audit (auditResidual)
                  from stats.jsonl's row at or before the mark.
  stomach founders: founders (p = -1) with the abs flag and src trickle or pool, born in the
                  5,000 s before the mark; median lifetime of those dead by the run's last
                  lineage row, and the count still alive (censored).
"""
import importlib.util, os, sys, math, bisect
import numpy as np

REPO = r"D:\Projects\experiments\evolution-simulator"
spec = importlib.util.spec_from_file_location("r47read", os.path.join(REPO, "scripts", "reads", "r47-read.py"))
R = importlib.util.module_from_spec(spec)
spec.loader.exec_module(R)


def med(xs):
    xs = sorted(xs)
    return xs[len(xs) // 2] if xs else None


def read_arm(root, arm, marks):
    d = R.run_dir(root, arm)
    config = R.load_json(os.path.join(d, "config.json"))
    manifest = R.load_json(os.path.join(d, "run.json"))
    reefs, _ = R.read_reefs(config, manifest)
    bed = R.read_bed(d, config)
    nx, nz, cell = bed["nx"], bed["nz"], bed["cell"]
    water = np.zeros(nx * nz)
    for ix in range(nx):
        for iz in range(nz):
            k = ix * nz + iz
            if bed["live"][k]:
                water[k] = max(0.0, R.column_water(bed, reefs, ix, iz))
    live = water > 0.5
    dumps = R.snow_dumps(d, 1e12)
    dts = sorted(dumps)
    stats = list(R.stream_jsonl(os.path.join(d, "stats.jsonl")))
    st_t = [r["t"] for r in stats]
    # positions rows at the marks
    want = set(marks)
    pos = {}
    for row in R.stream_jsonl(os.path.join(d, "positions.jsonl")):
        t = row["t"]
        if abs(t - round(t)) < 1e-9 and int(round(t)) in want:
            pos[int(round(t))] = row["b"]
    # lineage
    B, died, t_last = {}, {}, 0.0
    for r in R.stream_jsonl(os.path.join(d, "lineage.jsonl")):
        t_last = max(t_last, r.get("t", 0.0))
        if r["e"] == "b":
            B[r["id"]] = (r["t"], r["p"], r.get("abs", 0), r.get("pho", 0), r.get("src"))
        elif r["e"] == "d":
            died[r["id"]] = r["t"]
    out = []
    for m in marks:
        row = dict(arm=arm, mark=m, configHash=manifest.get("configHash"), status=manifest.get("status"))
        i = bisect.bisect_right(dts, m) - 1
        if i < 0 or not stats or st_t[-1] < m - 20:
            row["short"] = True
            out.append(row)
            continue
        dt = dts[i]
        cs = np.array(R.read_floats(dumps[dt]), dtype=float)
        dens = np.full(nx * nz, np.nan)
        dens[live] = cs[live] / water[live]
        ld = dens[live]
        row.update(dump=dt, tank_mean=cs[live].sum() / water[live].sum(),
                   gt044=float(np.mean(ld > 0.44)), gt1=float(np.mean(ld > 1.0)),
                   col_med=float(np.median(ld)))
        b = pos.get(m)
        if b is not None:
            cnt = {}
            for e in b:
                if e[4] & 4:
                    k = min(nx - 1, max(0, int(math.floor(e[1] / cell)))) * nz + \
                        min(nz - 1, max(0, int(math.floor(e[3] / cell))))
                    if live[k]:
                        cnt[k] = cnt.get(k, 0) + 1
            occ = sorted(cnt, key=lambda k: -cnt[k])
            top = occ[:max(1, len(occ) // 10)] if occ else []
            row["top10_plant"] = float(np.mean([dens[k] for k in top])) if top else None
        j = bisect.bisect_right(st_t, m) - 1
        s = stats[j]
        row.update(stat_t=s["t"], alive=s["alive"], photo=s["photosynthetic"],
                   absorptive=s["absorptive"], upt_lim=s["uptakeLimitedShare"],
                   mat_resid=s["matterResidual"], audit=s["auditResidual"],
                   snow_J=s["detritusJoules"], standing=s["matterStanding"])
        for src in ("trickle", "pool"):
            ids = [k for k, v in B.items() if v[1] == -1 and v[2] and v[4] == src and m - 5000 < v[0] <= m]
            lives = [died[k] - B[k][0] for k in ids if k in died]
            row[src + "_n"] = len(ids)
            row[src + "_alive"] = len(ids) - len(lives)
            row[src + "_life"] = med(lives)
        out.append(row)
    return out


def f(v, fmt):
    return "-" if v is None else fmt % v


def main():
    args = [a for a in sys.argv[1:]]
    marks = [5000, 10000, 15000]
    if "--marks" in args:
        k = args.index("--marks")
        marks = [int(x) for x in args[k + 1].split(",")]
        del args[k:k + 2]
    root, arms = args[0], args[1:]
    cols = ["arm", "mark", "snow tank J/m3", "col med", ">0.44", ">1", "top10 plant", "alive", "photo",
            "abs", "upt lim", "mat resid", "audit", "trickle stom n/alive/life s", "pool stom n/alive/life s"]
    print(" | ".join(cols))
    hashes = {}
    for arm in arms:
        for r in read_arm(root, arm, marks):
            hashes[arm] = (r["configHash"], r["status"])
            if r.get("short"):
                print("%s | %d | not reached" % (arm, r["mark"]))
                continue
            print(" | ".join([arm, "%d" % r["mark"], "%.3f" % r["tank_mean"], "%.3f" % r["col_med"],
                              "%.2f" % r["gt044"], "%.3f" % r["gt1"], f(r.get("top10_plant"), "%.2f"),
                              "%d" % r["alive"], "%d" % r["photo"], "%d" % r["absorptive"],
                              "%.1f%%" % (100 * r["upt_lim"]), "%.2e" % r["mat_resid"], "%.1e%%" % (100 * r["audit"]),
                              "%d/%d/%s" % (r["trickle_n"], r["trickle_alive"], f(r["trickle_life"], "%.0f")),
                              "%d/%d/%s" % (r["pool_n"], r["pool_alive"], f(r["pool_life"], "%.0f"))]))
    for a, (h, s) in hashes.items():
        print("%s configHash %s status %s" % (a, h, s))


main()
