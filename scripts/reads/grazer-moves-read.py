"""Do grazers move between the islands, and how much of their time do they spend over open water? A reader for a
shared-world run: how often a grazer goes from one island to another, how far and how fast, and the share of grazer
fixes that lie over open water.

Usage: python -I scripts/reads/grazer-moves-read.py RUN [RUN ...] [--end 24000] [--bin 3000] [--stride 10]
                                                   [--year 3000] [--check-stride 100] [--no-check]
                                                   [--footprint-at 100]

A RUN is a run directory or an arm folder, of which the latest dated run is read; a run whose run.json carries
resumedFrom is read as one world with the run it resumed, through r59-read.py's World and its seg_positions (the
chain's positions rows in time order, each second once).

What is reused, not restated. This script imports r60-read.py (as island-guard-read.py does) and, through it,
r59-read.py. The islands are r60-read.py's Fed: the 5 m matter columns whose top-three-layer density, in the dump
nearest --footprint-at seconds (100), is above the water's mean, less those within the glass margin, joined by their
sides into islands. A body is over island k when its 5 m column, (int(x // cell), int(z // cell)) clamped, has
component k in Fed.comp, and over open water when that column has no island (comp < 0). The grazers are the bodies
born with r59.GRAZER set (a mouthed body, atk 1 and ink 1, with pho 0), in World.born, which the lineage's birth rows
build. The header is r60-read.py's own, with the lines of this script below it.

Fixes. A fix is one body in one positions row ([id, x, y, z, flags]). Only rows whose t is a multiple of --stride are
kept. The default stride is 10 s, or the run's own positions cadence when that is finer; the cadence is found from the
spacing of the first rows and printed. Where the cadence is coarser than the stride, the rows are the cadence's, and
the spacing between kept rows (their median, printed as the effective spacing) is what turns fixes into seconds. A
remains body (flag bit 8, a corpse) is no living body: it is left out of every count below, apart from the second
walker of the check line.

A move. Walk one grazer's fixes in time order. A move is a fix over island B whose last earlier fix over an island
was over island A, with B not A. Fixes over open water between them do not reset A. For each move the script keeps
  t1, x1, z1  the last fix over A
  t2, x2, z2  the first fix over B
  distance    the horizontal straight line from (x1, z1) to (x2, z2), metres. In a Box world the edges are joined
              (D204), so each axis takes the shorter way round. That a world is joined is read from the config's
              worldShape (Box is periodic on both axes, Tank is not); the side is the square root of the config's
              worldAreaSquareMetres, which the script checks against the matter grid's width. A tank takes the plain
              distance.
  crossing    t2 - t1, seconds. It includes the time the grazer sat on A after its last fix there, up to the stride;
              speed is distance over crossing, which is a floor on the swimming speed, as the line is straight
  born on A   the grazer's first kept fix was over A. The birth rows carry no position, so this is a proxy: a body's
              first fix comes up to one stride after its birth, and a grazer first fixed over open water is born on no
              island by this reading

Per run it prints:
  the header (r60-read.py's, then the cadence, the stride, the side and whether the edges are joined);
  a table per bin of --bin seconds, by the move's t2 (the fixes by the fix's own t): grazer fixes; the open-water share
    of grazer fixes; the open-water share of all living bodies' fixes; moves; distinct movers; moves per 1,000
    grazer-seconds (grazer fixes times the effective spacing); distance quartiles (q1/median/q3); crossing-time
    quartiles; speed quartiles; and the moves by a grazer born on the island it left;
  the totals, the same columns, then the share of bred grazers that ever move and the largest single move. Bred
    means born with a parent in the record (p >= 0); a grazer with none is a founder and is counted apart;
  a check line. Pass two of the same file: at --check-stride (100) seconds, each --year (3000) seconds' distinct
    grazer movers by this script's rule are printed beside the grazers r60-read.py's own Fed.migrants() counts for
    the same stride, which this script calls. A move needs only a different island from the last, so the two must
    agree. r60-read.py counts a remains body too; the check therefore runs one walker with corpses and one without,
    and the verdict compares the first with Fed.migrants(). The walker without corpses is the one the tables use.

One pass over the positions per run, reading each row once for the table and the check together; no row is kept.
Reads the files only; writes nothing. Standard library only.
"""
import argparse
import importlib.util
import json
import math
import os
import sys
from types import SimpleNamespace

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("r60read", os.path.join(HERE, "r60-read.py"))
r60 = importlib.util.module_from_spec(_spec)
sys.modules["r60read"] = r60
_spec.loader.exec_module(r60)
r59 = r60.r59

REMAINS = 8
CADENCE_ROWS = 40
DEFAULT_STRIDE = 10.0


def on_stride(t, s):
    return abs(t / s - round(t / s)) < 1e-6


def median(xs):
    s = sorted(xs)
    n = len(s)
    if not n:
        return None
    return s[n // 2] if n % 2 else 0.5 * (s[n // 2 - 1] + s[n // 2])


def quart(xs):
    """(q1, median, q3) by linear interpolation between order statistics, or None for no values."""
    if not xs:
        return None
    s = sorted(xs)
    n = len(s)

    def q(p):
        pos = p * (n - 1)
        lo = int(pos)
        hi = min(lo + 1, n - 1)
        return s[lo] + (s[hi] - s[lo]) * (pos - lo)

    return q(0.25), q(0.5), q(0.75)


def qs(xs, spec):
    v = quart(xs)
    return "-" if v is None else "/".join(spec.format(x) for x in v)


def share(a, b):
    return a / b if b else None


def f3(x, spec="{:.3f}"):
    return "-" if x is None else spec.format(x)


def cadence_of(w):
    """The positions cadence: the median gap between the first rows of the chain (seconds), or None."""
    ts = []
    for t, _ in w.seg_positions(lambda t: True):
        ts.append(t)
        if len(ts) >= CADENCE_ROWS:
            break
    gaps = [b - a for a, b in zip(ts, ts[1:]) if b - a > 1e-9]
    return round(median(gaps), 6) if gaps else None


class Walk:
    """One grazer's island history. fix() returns None, or (A, t1, x1, z1) when the fix completes a move."""

    def __init__(self, year):
        self.year = year
        self.last = {}      # id -> (island, t, x, z), the last fix over an island
        self.origin = {}    # id -> island of the first fix (-1 for open water)
        self.moved = {}     # r59 year -> set of ids with a move whose t2 is in it
        self.n_moves = 0
        self.movers = set()

    def fix(self, i, t, x, z, k):
        if i not in self.origin:
            self.origin[i] = k
        if k < 0:
            return None
        was = self.last.get(i)
        self.last[i] = (k, t, x, z)
        if was is not None and was[0] != k:
            self.moved.setdefault(r59.year_of(t, self.year), set()).add(i)
            self.movers.add(i)
            self.n_moves += 1
            return was
        return None


class Bins:
    def __init__(self, edges):
        self.edges = edges
        n = len(edges)
        self.gfix = [0] * n
        self.gopen = [0] * n
        self.afix = [0] * n
        self.aopen = [0] * n
        self.dist = [[] for _ in range(n)]
        self.dt = [[] for _ in range(n)]
        self.speed = [[] for _ in range(n)]
        self.born_a = [0] * n
        self.ids = [set() for _ in range(n)]

    def of(self, t):
        return max(0, min(len(self.edges) - 1, int(math.ceil(t / self.step - 1e-9)) - 1))


def gap(a, b, side):
    d = abs(b - a)
    if side:
        d %= side
        d = min(d, side - d)
    return d


def setup_geometry(w, f):
    """(side, joined, note). The side is sqrt(worldAreaSquareMetres) from config.json; a Box world is periodic."""
    area = r59.find(w.config, "worldAreaSquareMetres")
    shape = r59.find(w.config, "worldShape")
    joined = (shape == "Box")
    side = math.sqrt(float(area)) if area is not None else None
    note = None
    if joined:
        grid = f.side if f.side else (f.nx * f.cell, f.nz * f.cell)
        if side is None or abs(grid[0] - side) > 1e-6 * max(1.0, side) or abs(grid[1] - side) > 1e-6 * max(1.0, side):
            note = (f"worldAreaSquareMetres gives a side of {f3(side, '{:.3f}')} m but the matter grid is "
                    f"{grid[0]:g} x {grid[1]:g} m; the grid's sides are used, one per axis")
            side = None
            return grid, joined, note
        return (side, side), joined, note
    return None, joined, f"worldShape is {shape}: the edges are not joined, the plain distance is used"


def read_world(path, a):
    ns = SimpleNamespace(year=a.year, every=1500.0, end=a.end, footprint_at=a.footprint_at)
    w = r59.World(path, ns, r60.NoSwimmers())
    f = r60.Fed(w, ns)
    end = w.end
    cad = cadence_of(w)
    if cad is None:
        sys.exit(f"{w.dir}: no positions rows to read a cadence from")
    stride = a.stride if a.stride is not None else min(DEFAULT_STRIDE, cad)
    check = not a.no_check
    cs = a.check_stride
    sides, joined, geo_note = setup_geometry(w, f)

    edges = []
    lo = 0.0
    while lo < end - 1e-9:
        hi = min(end, lo + a.bin)
        edges.append((lo, hi))
        lo = hi
    bins = Bins(edges)
    bins.step = a.bin

    nx, nz, c = f.nx, f.nz, f.cell
    comp, born = f.comp, w.born
    GRAZER = r59.GRAZER
    main = Walk(a.year)
    chk_live = Walk(a.year)
    chk_all = Walk(a.year)
    seen = set()
    mrows = []
    crows = 0
    remains_skipped = 0
    biggest = None
    moves_all = 0

    def want(t):
        # The first row past the end is let through so the loop can stop there, rather than read the file to its close.
        return t > end + 1e-9 or on_stride(t, stride) or (check and on_stride(t, cs))

    for t, line in w.seg_positions(want):
        if t > end + 1e-9:
            break
        in_main = on_stride(t, stride)
        in_chk = check and on_stride(t, cs)
        bi = bins.of(t) if in_main else 0
        if in_main:
            mrows.append(t)
        if in_chk:
            crows += 1
        for b in json.loads(line)["b"]:
            i, x, z, fl = b[0], b[1], b[3], b[4]
            bt = born.get(i)
            graz = bt is not None and bool(bt[2] & GRAZER)
            ix, iz = min(nx - 1, max(0, int(x // c))), min(nz - 1, max(0, int(z // c)))
            k = comp[ix][iz]
            if fl & REMAINS:
                if in_main:
                    remains_skipped += 1
                if graz and in_chk:
                    chk_all.fix(i, t, x, z, k)
                continue
            if in_main:
                bins.afix[bi] += 1
                if k < 0:
                    bins.aopen[bi] += 1
                if graz:
                    bins.gfix[bi] += 1
                    if k < 0:
                        bins.gopen[bi] += 1
                    seen.add(i)
                    was = main.fix(i, t, x, z, k)
                    if was is not None:
                        a_isl, t1, x1, z1 = was
                        d = math.hypot(gap(x1, x, sides[0] if sides else None), gap(z1, z, sides[1] if sides else None))
                        dt = t - t1
                        bins.dist[bi].append(d)
                        bins.dt[bi].append(dt)
                        bins.speed[bi].append(d / dt if dt > 0 else 0.0)
                        bins.ids[bi].add(i)
                        borna = main.origin.get(i) == a_isl
                        bins.born_a[bi] += borna
                        moves_all += 1
                        if biggest is None or d > biggest[0]:
                            biggest = (d, i, a_isl, t1, x1, z1, k, t, x, z, borna)
            if graz and in_chk:
                chk_live.fix(i, t, x, z, k)
                chk_all.fix(i, t, x, z, k)

    spacing = median([y - x for x, y in zip(mrows, mrows[1:])]) if len(mrows) > 1 else stride
    if check:
        f.migrants(cs, end)
    return dict(w=w, f=f, end=end, cad=cad, stride=stride, spacing=spacing, mrows=mrows, crows=crows, bins=bins,
                main=main, chk_live=chk_live, chk_all=chk_all, seen=seen, biggest=biggest, sides=sides, joined=joined,
                geo_note=geo_note, remains=remains_skipped, check=check, cs=cs, moves_all=moves_all)


def table_row(label, gf, go, af, ao, dist, dt, speed, ids, nborn, spacing):
    n = len(dist)
    gsec = gf * spacing
    return (f"   {label:<12} {gf:>8} {f3(share(go, gf)):>7} {f3(share(ao, af)):>7} {n:>6} {len(ids):>7} "
            f"{f3(n / gsec * 1000 if gsec else None, '{:.3f}'):>9}  {qs(dist, '{:.0f}'):<15} {qs(dt, '{:.0f}'):<17} "
            f"{qs(speed, '{:.2f}'):<15} {nborn:>6}")


def print_world(r, a):
    w, f, bins = r["w"], r["f"], r["bins"]
    r60.header(w, f)
    print(f"   positions cadence found {r['cad']:g} s (the median gap of the chain's first rows); stride used "
          f"{r['stride']:g} s, effective spacing of the {len(r['mrows'])} kept rows {r['spacing']:g} s"
          + (f" (rows expected to {r['end']:g} s: {int(r['end'] / r['spacing'] + 1e-9)})" if r["spacing"] else "")
          + ("; the stride is finer than the cadence, so the cadence's rows are all there are" if r["stride"] < r["cad"] - 1e-9 else ""))
    if r["sides"]:
        print(f"   joined edges: worldShape Box, side sqrt(worldAreaSquareMetres) = {r['sides'][0]:g} x {r['sides'][1]:g} m; "
              f"each axis takes the shorter way round")
    if r["geo_note"]:
        print(f"   note: {r['geo_note']}")
    print(f"   grazers: bodies with r59.GRAZER set at birth; {r['remains']} remains-body fixes (flag 8) left out of every count; "
          f"'born on A' is the first kept fix being over A (the birth rows carry no position)")
    print(f"\n   moves by the move's t2 in bins of {a.bin:g} s; fixes by the fix's t")
    print(f"   {'bin':<12} {'g-fixes':>8} {'g-open':>7} {'all-op':>7} {'moves':>6} {'movers':>7} {'mv/1000gs':>9}  "
          f"{'dist m q1/med/q3':<15} {'cross s q1/med/q3':<17} {'m/s q1/med/q3':<15} {'bornA':>6}")
    sp = r["spacing"]
    for k, (lo, hi) in enumerate(bins.edges):
        print(table_row(f"{lo:g}-{hi:g}", bins.gfix[k], bins.gopen[k], bins.afix[k], bins.aopen[k], bins.dist[k],
                        bins.dt[k], bins.speed[k], bins.ids[k], bins.born_a[k], sp))
    alld = [x for v in bins.dist for x in v]
    allt = [x for v in bins.dt for x in v]
    alls = [x for v in bins.speed for x in v]
    print(table_row("total", sum(bins.gfix), sum(bins.gopen), sum(bins.afix), sum(bins.aopen), alld, allt, alls,
                    r["main"].movers, sum(bins.born_a), sp))
    born = w.born
    grazers = [i for i, (t, p, bits) in born.items() if bits & r59.GRAZER]
    bred = {i for i in grazers if born[i][1] >= 0}
    founders = len(grazers) - len(bred)
    seen, movers = r["seen"], r["main"].movers
    bred_seen = bred & seen
    bred_mv = bred & movers
    print(f"\n   grazers born by {r['end']:g} s: {len(grazers)} ({len(bred)} bred, {founders} founders); fixed at least once "
          f"in the kept rows: {len(seen)} ({len(bred_seen)} bred)")
    print(f"   bred grazers that ever move: {len(bred_mv)} of {len(bred)} born ({f3(share(len(bred_mv), len(bred)))}), "
          f"of {len(bred_seen)} fixed ({f3(share(len(bred_mv), len(bred_seen)))}); all movers {len(movers)}, "
          f"moves {r['moves_all']} ({f3(share(r['moves_all'], len(movers)), '{:.2f}')} per mover)")
    g = r["biggest"]
    if g:
        d, i, ai, t1, x1, z1, bk, t2, x2, z2, borna = g
        print(f"   largest single move: {d:.1f} m by body {i}, island #{ai} at {t1:g} s ({x1:.1f}, {z1:.1f}) to island #{bk} "
              f"at {t2:g} s ({x2:.1f}, {z2:.1f}), {t2 - t1:g} s, {d / (t2 - t1) if t2 > t1 else float('nan'):.2f} m/s, "
              f"{'born on #%d' % ai if borna else 'not born on #%d' % ai}")
    else:
        print("   largest single move: none")
    if not r["check"]:
        print("   check: skipped (--no-check)")
        return
    cs, year = r["cs"], a.year
    lw, aw = r["chk_live"], r["chk_all"]
    theirs = {y: v[2] for y, v in f.moved.items()}
    ys = sorted(set(lw.moved) | set(aw.moved) | set(theirs))
    print(f"\n   check at stride {cs:g} s ({r['crows']} rows), per r60-read year of {year:g} s: distinct grazers with a move in "
          f"the year, here and by Fed.migrants() (r60-read.py, which counts corpses too)")
    print(f"   {'year':>5} {'t':<14} {'here, living':>12} {'here, +remains':>14} {'Fed.migrants':>12}")
    ok = True
    for y in ys:
        a_ = len(lw.moved.get(y, ()))
        b_ = len(aw.moved.get(y, ()))
        c_ = theirs.get(y, 0)
        ok = ok and b_ == c_
        print(f"   {y:>5} {f'{(y - 1) * year:g}-{y * year:g}':<14} {a_:>12} {b_:>14} {c_:>12}")
    print(f"   moves at stride {cs:g}: {lw.n_moves} living, {aw.n_moves} with corpses; distinct movers over the run: "
          f"{len(lw.movers)} living, {len(aw.movers)} with corpses")
    live_same = all(len(lw.moved.get(y, ())) == len(aw.moved.get(y, ())) for y in ys)
    print(f"   -> {'AGREE' if ok else 'DIFFER'}: this script with corpses against Fed.migrants() in every year"
          + ("" if live_same else "; the living-only count differs from the corpses-included one (remains bodies carried between islands)"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=24000.0)
    ap.add_argument("--bin", type=float, default=3000.0)
    ap.add_argument("--stride", type=float, default=None,
                    help="keep rows whose t is a multiple of this; default 10 s, or the cadence when finer")
    ap.add_argument("--year", type=float, default=3000.0, help="the year of the check line (r60-read.py's --year)")
    ap.add_argument("--check-stride", type=float, default=100.0)
    ap.add_argument("--no-check", action="store_true")
    ap.add_argument("--footprint-at", type=float, default=100.0)
    a = ap.parse_args()
    r59.runrec.VERBOSE = False
    print(f"== grazer moves: bins of {a.bin:g} s to {a.end:g} s, islands as r60-read.py seeds them at {a.footprint_at:g} s")
    for d in a.runs:
        print_world(read_world(d, a), a)


if __name__ == "__main__":
    main()
