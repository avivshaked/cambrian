"""Round 48 snow screens: one arm's larder over time. Read-only.

python scratch/r48-snow/snow-timeline.py <runs root> <arm> [--every 1000]

Every mark: the snow's tank mean (J/m3 of water, snow-read.py's arithmetic), the share of live
columns over 0.44 and over 1 J/m3, the bed layer's snow (fields/*.snow-floor.f32: each column's
lowest live 1 m cell, J; summed, and over the live column count as J/m3 of that layer), the
stats' floorStockJoules (Row.cs: patch 0's refuge stock, a joules total and not a density),
the living absorptive bodies' densityHere (absorptive.jsonl rows within 5 s of the mark, dead
rows excluded) and the column snow under absorptive bodies (positions.jsonl flag 1), alive,
photo, absorptive count, upt lim.
"""
import importlib.util, os, sys, math, bisect
import numpy as np

REPO = r"D:\Projects\experiments\evolution-simulator"
spec = importlib.util.spec_from_file_location("r47read", os.path.join(REPO, "scripts", "reads", "r47-read.py"))
R = importlib.util.module_from_spec(spec)
spec.loader.exec_module(R)


def q(xs):
    xs = sorted(xs)
    if not xs:
        return "n=0"
    n = len(xs)
    return "n=%d %.2f [%.2f-%.2f]" % (n, xs[n // 2], xs[n // 4], xs[(3 * n) // 4])


def main():
    args = sys.argv[1:]
    every = 1000
    if "--every" in args:
        k = args.index("--every"); every = int(args[k + 1]); del args[k:k + 2]
    root, arm = args
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
    stats = list(R.stream_jsonl(os.path.join(d, "stats.jsonl")))
    st_t = [r["t"] for r in stats]
    t_end = st_t[-1]
    marks = [m for m in range(every, int(t_end) + 1, every) if m in dumps]
    want = set(marks)
    absrows = {}
    for r in R.stream_jsonl(os.path.join(d, "absorptive.jsonl")):
        if "id" not in r or r.get("dead"):
            continue
        for m in (int(round(r["t"] / every)) * every,):
            if m in want and abs(r["t"] - m) <= 5:
                absrows.setdefault(m, {})[r["id"]] = r["densityHere"]
    pos = {}
    for row in R.stream_jsonl(os.path.join(d, "positions.jsonl")):
        t = row["t"]
        if abs(t - round(t)) < 1e-9 and int(round(t)) in want:
            pos[int(round(t))] = [(e[1], e[3]) for e in row["b"] if e[4] & 1]
    print("%s  configHash %s  status %s  last stats t %g" % (arm, manifest.get("configHash"), manifest.get("status"), t_end))
    print("t | tank mean J/m3 | >0.44 | >1 | bed layer J (sum) | bed layer J/m3 | floorStockJoules | eaters densityHere med[q1-q3] | column snow at eaters | alive | photo | abs | upt lim")
    for m in marks:
        cs = np.array(R.read_floats(dumps[m]), dtype=float)
        dens = np.full(nx * nz, np.nan); dens[live] = cs[live] / water[live]
        ld = dens[live]
        fp = dumps[m].replace("snow-columns", "snow-floor")
        fl = np.array(R.read_floats(fp), dtype=float) if os.path.exists(fp) else None
        s = stats[bisect.bisect_right(st_t, m) - 1]
        at = []
        for x, z in pos.get(m, []):
            k = min(nx - 1, max(0, int(math.floor(x / cell)))) * nz + min(nz - 1, max(0, int(math.floor(z / cell))))
            if live[k]:
                at.append(dens[k])
        print(" | ".join([
            "%d" % m, "%.3f" % (cs[live].sum() / water[live].sum()), "%.2f" % np.mean(ld > 0.44), "%.3f" % np.mean(ld > 1.0),
            "%.0f" % fl[live].sum() if fl is not None else "-", "%.3f" % (fl[live].sum() / live.sum()) if fl is not None else "-",
            "%.1f" % s.get("floorStockJoules", float("nan")), q(list(absrows.get(m, {}).values())), q(at),
            "%d" % s["alive"], "%d" % s["photosynthetic"], "%d" % s["absorptive"], "%.1f%%" % (100 * s["uptakeLimitedShare"])]))


main()
