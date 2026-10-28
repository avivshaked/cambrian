"""r63b's gate-1 diagnosis, which the pre-registration asks for when A fails survival, and a look at gate 2's islands.
Usage: python -I scripts/reads/bloom/r63b-diag.py windows RUN [RUN ...] [--width 1500]
       python -I scripts/reads/bloom/r63b-diag.py leaves RUN [--islands 1,2,3]
       python -I scripts/reads/bloom/r63b-diag.py energy RUN [RUN ...] [--at 9000]
energy: of the grazers alive at --at, the share whose brain has a link from the root's energy port (its id computed as
CentralBrain.cs's BrainIds.Port does), the links per wired brain, their weights, and how many go to a hidden unit.
       python -I scripts/reads/bloom/r63b-diag.py matter RUN [RUN ...]
matter: per island of 5 columns or more, the spent-matter density over its columns (the nearest N cells to its centre,
N its column count; the top three 5 m layers) from fields/ every 100 s: its best lag and r against the bloom factor, its
mean in the factor's top and bottom quarter over the half-saturation K, the leaves' best lag and r against the matter
and against the factor, and the mean lit leaf area over the island's footprint.
windows: per window of --width seconds, the grazers' fewest and mean count, their births and their deaths, counted as
g2-read.py counts them (its RunRead, imported, so the grazer is the same body there and here).
leaves: per island, the lit leaf area against the bloom factor, from stats' bloomLeafArea and bloomFactor: the leaf
area's mean and its relative spread, Pearson's r at the best lag from 0 to 6000 s (twice gate 2's reach), and the same for
the leaf area's rate of change, which tracks the factor if the leaves integrate the influx. Reads files only.
"""
import argparse
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("g2read", os.path.join(HERE, "g2-read.py"))
g2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g2)


def run_read(path, end=24000.0):
    a = argparse.Namespace(end=end, t_from=6000.0, window=3000.0, upkeep_w=0.908, survive_from=9000.0)
    ns = argparse.Namespace(end=end, year=6000.0, every=6000.0, window=500.0, upkeep_w=0.908, t_from=6000.0, bin=3000.0)
    return g2.RunRead(path, a, ns)


def windows(paths, width):
    for p in paths:
        rr = run_read(p)
        born, deaths = rr.w.born, rr.w.deaths
        print(f"\n## {rr.name} ({os.path.basename(rr.w.dir)}), {len(rr.grazers)} grazers born")
        print(f"   {'window':>13} {'fewest':>6} {'mean':>6} {'births':>6} {'deaths':>6}")
        t0 = 0.0
        while t0 < rr.end - 1e-9:
            t1 = t0 + width
            c = [n for t, n in zip(rr.times, rr.counts) if t0 <= t < t1 or (t1 >= rr.end and t == t1)]
            b = sum(1 for i in rr.grazers if t0 < born[i][0] <= t1)
            d = sum(1 for i in rr.grazers if i in deaths and t0 < deaths[i][0] <= t1)
            if c:
                print(f"   {t0:6.0f}-{t1:<6.0f} {min(c):6d} {sum(c) / len(c):6.1f} {b:6d} {d:6d}")
            t0 = t1


def leaves(path, which):
    rr = run_read(path)
    rows = [s for s in rr.stats if "bloomFactor" in s and "bloomLeafArea" in s]
    if not rows:
        print(f"{rr.name}: no bloom columns")
        return
    step = rr.step
    n_is = len(rows[0]["bloomFactor"])
    lo = next(i for i, s in enumerate(rows) if s["t"] >= 6000.0)
    print(f"\n## {rr.name}: leaf area against the bloom factor from 6000 s, lags 0 to 6000 s")
    print(f"   {'island':>6} {'mean m2':>8} {'sd/mean':>7} {'lag s':>6} {'r':>6} {'rate lag':>8} {'rate r':>6}")
    for k in range(n_is):
        if which and k not in which:
            continue
        f = [s["bloomFactor"][k] for s in rows]
        L = [s["bloomLeafArea"][k] for s in rows]
        m = sum(L[lo:]) / len(L[lo:])
        sd = g2.sample_sd(L[lo:]) if len(L[lo:]) > 1 else 0.0
        lag, r = g2.best_lag(f, L, step, lo, len(rows), max_lag=6000.0)
        dL = [0.0] + [(L[i] - L[i - 1]) / step for i in range(1, len(L))]
        rlag, rr_ = g2.best_lag(f, dL, step, lo, len(rows), max_lag=6000.0)
        fmt = lambda x: "-" if x is None else f"{x:.3f}"
        print(f"   {k:6d} {m:8.2f} {(sd / m if m > 0 else 0):7.2f} {lag if lag is not None else '-':>6} {fmt(r):>6} "
              f"{rlag if rlag is not None else '-':>8} {fmt(rr_):>6}")


M64 = (1 << 64) - 1


def _mix(a, b):
    # CentralBrain.cs BrainIds.Mix: splitmix64's finaliser over a combination of the two
    z = (a ^ ((b + 0x9E3779B97F4A7C15 + (a << 6) + (a >> 2)) & M64)) & M64
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
    return z ^ (z >> 31)


def _nz(x):
    return x if x else 1


ROOT = _nz(_mix(0x524F4F5442524E31, 1))
ENERGY_PORT = "%016x" % _nz(_mix(_mix(_mix(ROOT, 0x504F5254), 7), 0))   # the root's Energy (channel 7), index 0


def energy(paths, at):
    """Of the grazers alive at `at`, how many brains read the root's energy port, and how strongly."""
    import gzip
    print(f"\nthe energy port's id {ENERGY_PORT}; grazers alive at {at:g} s")
    print(f"   {'run':16} {'alive':>5} {'wired':>5} {'share':>6} {'links/wired':>11} {'mean |w|':>8} {'mean w':>7} {'to hidden':>9}")
    for p in paths:
        rr = run_read(p)
        born, deaths = rr.w.born, rr.w.deaths
        alive = {i for i in rr.grazers if born[i][0] <= at and not (i in deaths and deaths[i][0] <= at)}
        wired = links = hidden = 0
        ws = []
        with gzip.open(os.path.join(rr.w.dir, "genomes.jsonl.gz"), "rt") as f:
            for line in f:
                if not alive:
                    break
                gid = int(line[6:line.index(",")])
                if gid not in alive:
                    continue
                alive.discard(gid)
                g = __import__("json").loads(line)
                b = g.get("brain") or {}
                hid = {h["id"] for h in b.get("hidden", [])}
                mine = [l for l in b.get("links", []) if l["from"] == ENERGY_PORT]
                if mine:
                    wired += 1
                    links += len(mine)
                    hidden += sum(1 for l in mine if l["to"] in hid)
                    ws += [l["w"] for l in mine]
        n = wired + 0
        tot = len({i for i in rr.grazers if born[i][0] <= at and not (i in deaths and deaths[i][0] <= at)})
        mabs = sum(abs(w) for w in ws) / len(ws) if ws else 0.0
        mw = sum(ws) / len(ws) if ws else 0.0
        print(f"   {rr.name:16} {tot:5d} {wired:5d} {wired / tot if tot else 0:6.2f} {links / n if n else 0:11.2f} "
              f"{mabs:8.3f} {mw:7.3f} {hidden:9d}")


def matter(path, layers=3):
    """Per judged island: the spent-matter density over it (fields/*.matter.f32, J per 5 m cell, the top `layers`
    layers where the islands' leaves stand), against its bloom factor and its lit leaf area."""
    import glob
    import struct
    rr = run_read(path)
    w = rr.w
    rep = os.path.join(os.path.dirname(w.dir) + ".md")
    islands = g2.parse_islands(open(rep, encoding="utf-8").read())
    lay = __import__("json").load(open(os.path.join(w.dir, "fields", "layout.json")))["matter"]
    nx, nz, cell = lay["cellsX"], lay["cellsZ"], float(lay["cellMetres"])
    jpu = float(g2.load_reads()["r61"].r59.find(w.config, "joulesPerUnit") or 100.0)
    K = float(g2.load_reads()["r61"].r59.find(w.config, "uptakeHalfSaturation"))
    vol = cell ** 3
    cells = {}
    for isl in islands:
        d = sorted(((((ix + 0.5) * cell - isl["cx"]) ** 2 + ((iz + 0.5) * cell - isl["cz"]) ** 2, ix, iz)
                    for ix in range(nx) for iz in range(nz)))
        cells[isl["k"]] = [(ix, iz) for _, ix, iz in d[:isl["columns"]]]
    by_t = {round(s["t"]): s for s in rr.stats if "bloomFactor" in s}
    files = sorted(glob.glob(os.path.join(w.dir, "fields", "*.matter.f32")))
    ts, dens = [], {k: [] for k in cells}
    for f in files:
        t = int(os.path.basename(f).split(".")[0])
        if t < 6000 or t not in by_t:
            continue
        data = open(f, "rb").read()
        v = struct.unpack("<%df" % (len(data) // 4), data)
        ts.append(t)
        for k, cs in cells.items():
            tot = sum(v[(iy * nx + ix) * nz + iz] for ix, iz in cs for iy in range(layers))
            dens[k].append(tot / jpu / (len(cs) * layers * vol))
    step = ts[1] - ts[0]
    ul = [by_t[t].get("uptakeLimitedShare") for t in ts]
    ul = [x for x in ul if x is not None]
    print(f"\n## {rr.name}: matter over each island, top {layers} layers, units/m3 (K = {K:g}); fields every {step} s "
          f"from 6000 s; uptake-limited share of leaves, whole world: mean {sum(ul) / len(ul):.2f}" if ul else "")
    print(f"   {'isl':>3} {'cols':>4} {'f->matter lag r':>15} {'peak/K':>6} {'trough/K':>8} {'pk/tr':>5} "
          f"{'matter->leaf lag r':>18} {'f->leaf lag r':>13} {'leaf/footprint':>14}")
    for isl in islands:
        k = isl["k"]
        if isl["columns"] < 5:
            continue
        f = [by_t[t]["bloomFactor"][k] for t in ts]
        L = [by_t[t]["bloomLeafArea"][k] for t in ts]
        m = dens[k]
        lag, r = g2.best_lag(f, m, step, 0, len(ts) - 1, max_lag=3000.0)
        ml, mr = g2.best_lag(m, L, step, 0, len(ts) - 1, max_lag=3000.0)
        fl, fr = g2.best_lag(f, L, step, 0, len(ts) - 1, max_lag=3000.0)
        s = int((lag or 0) / step)
        pairs = sorted((f[i], m[i + s]) for i in range(len(ts) - s))
        q = max(1, len(pairs) // 4)
        lo = sum(x for _, x in pairs[:q]) / q
        hi = sum(x for _, x in pairs[-q:]) / q
        foot = sum(L) / len(L) / (isl["columns"] * cell * cell)
        fmt = lambda a, b: f"{a if a is not None else '-':>5} {b if b is None else round(b, 2)!s:>5}"
        print(f"   {k:3d} {isl['columns']:4d} {fmt(lag, r):>15} {hi / K:6.3g} {lo / K:8.3g} {(hi / lo if lo > 0 else 0):5.2f} "
              f"{fmt(ml, mr):>18} {fmt(fl, fr):>13} {foot:14.2f}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("what", choices=("windows", "leaves", "energy", "matter"))
    ap.add_argument("--at", type=float, default=9000.0)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--width", type=float, default=1500.0)
    ap.add_argument("--islands", default="")
    a = ap.parse_args()
    if a.what == "windows":
        windows(a.runs, a.width)
    elif a.what == "energy":
        energy(a.runs, a.at)
    elif a.what == "matter":
        for p in a.runs:
            matter(p)
    else:
        which = {int(x) for x in a.islands.split(",") if x.strip()}
        for p in a.runs:
            leaves(p, which)


if __name__ == "__main__":
    sys.exit(main())
