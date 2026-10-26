"""Where are the bodies that are not over an island? A reader for a world whose island share fell late in the run
(round 60's M3 reads that share at one point; this reads it, and where the rest of the bodies are, per bin).

Usage: python -I scripts/reads/island-guard-read.py RUN [RUN ...] [--bin 3000] [--end 24000] [--every 100]

A RUN is a run directory or an arm folder, of which the latest dated run is read; a run whose run.json carries
resumedFrom is read as one world with the run it resumed, as r60-read.py reads it (this script imports that reader's
Fed and, through it, r59-read.py's World, so the island definition is r60-read.py's and is not restated here).

The islands. As r60-read.py defines them (see its docstring): the 5 m matter columns whose top-three-layer density, in
the dump nearest 100 s, is above the mean of the water's columns, less those nearer the glass than the config's
matterIslandGlassMarginMetres. A body is "over an island" when its 5 m column (int(x // 5), int(z // 5), clamped) is an
island column. Every other body is over the desert, whether the column is water or not.

The bed. fields/layout.json's "bed" grid and fields/bed.f32 (the same file crowd_vs_bed.py reads): 1 m cells, order
ix * cellsZ + iz, the floor's y in metres, negative down. A body's bed is the cell (int(x), int(z)), clamped to the
grid, and its height above the bed is its positions-row y minus that floor. y is the body's position as the
positions row records it (the row's second number after x), so the height is that point's height and not the lowest
point of the body. In a chain of resumed runs the beds are compared byte for byte and the header says if they differ
(the first run's is used then).

What is read. The positions rows at every --every seconds (100) up to --end (24000) or the run's reach, whichever is
earlier, which are rows in bins of --bin seconds (3000): a bin labelled "a-b" holds the sampled rows with a < t <= b.
A last bin short of a whole --bin is labelled with its true end. Every body in a row counts, as r60-read.py counts
them (a remains body, flag bit 8, is counted too; the header line says how many were seen). Bodies are pooled over a
bin's rows, so a share is the bodies' share across the bin and not a mean of the rows' shares.

Guilds, from the positions row's flag bits (1 absorptive, 2 jointed, 4 photosynthetic; the row carries no mouth bit,
so there is no grazer here, and "jointed" is the nearest the row lets). The first four overlap: a body with two
flags is in two lines.
  all       every body
  leaf      flag 4 set (photosynthetic, whether or not also absorptive or jointed)
  jointed   flag 2 set
  absorb    flag 1 set
  non-leaf  flag 4 clear
  plain     none of flags 1, 2 and 4

Columns:
  n/row   bodies per sampled row in the bin
  isl     the share of the bodies over an island column (r60-read.py's "on-isl", pooled over the bin's rows)
  bed     the share within 0.5 m of the bed (height above the bed <= 0.5 m, a body below the floor included)
  h-mean  the mean height above the bed, metres
  h-med   the median height above the bed, metres
  open    the share over a desert column and more than 0.5 m above the bed
  off     the share not over an island (1 - isl)
  off:bed the share of the off-island bodies that are within 0.5 m of the bed
  off:h   the mean height above the bed of the off-island bodies, metres
The bodies fall in four cells: island and near the bed, island and above it, desert and near the bed (off * off:bed)
and desert and above it (open). The table prints isl, bed, off and open, which fix the four.

Check. For each run the last sampled row's island count is compared with r60-read.py's own Fed.crowd() on the same
row, and the line printed says whether the two counts are equal. To compare with r60-read.py's printed "on-isl" at
one point, make the last bin one row: `--end 3000 --bin 100` against `r60-read.py RUN --end 3000 --year 3000`. A
whole-bin share is a mean over the bin's rows and will not equal a point's.

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import importlib.util
import json
import os
import struct
import sys
from types import SimpleNamespace

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("r60read", os.path.join(HERE, "r60-read.py"))
r60 = importlib.util.module_from_spec(_spec)
sys.modules["r60read"] = r60
_spec.loader.exec_module(r60)
r59 = r60.r59

ABSORPTIVE, JOINTED, PHOTOSYNTHETIC, REMAINS = 1, 2, 4, 8
NEAR_BED = 0.5  # metres

GUILDS = (
    ("all", lambda f: True),
    ("leaf", lambda f: bool(f & PHOTOSYNTHETIC)),
    ("jointed", lambda f: bool(f & JOINTED)),
    ("absorb", lambda f: bool(f & ABSORPTIVE)),
    ("non-leaf", lambda f: not f & PHOTOSYNTHETIC),
    ("plain", lambda f: not f & (ABSORPTIVE | JOINTED | PHOTOSYNTHETIC)),
)


class Bed:
    """fields/bed.f32 of a world: the floor's y at 1 m cells."""

    def __init__(self, w):
        self.note = None
        paths = []
        for d, _, _ in w.segs:
            p = os.path.join(d, "fields", "bed.f32")
            if os.path.exists(p):
                paths.append((d, p))
        if not paths:
            sys.exit(f"{w.dir}: no fields/bed.f32 in the run or any run it resumed")
        d0, p0 = paths[0]
        with open(os.path.join(d0, "fields", "layout.json"), encoding="utf-8") as f:
            bl = json.load(f)["bed"]
        self.nx, self.nz, self.cell = bl["cellsX"], bl["cellsZ"], float(bl["cellMetres"])
        with open(p0, "rb") as f:
            raw = f.read()
        if len(raw) != self.nx * self.nz * 4:
            sys.exit(f"{p0}: {len(raw)} bytes, not {self.nx} x {self.nz} floats")
        self.v = struct.unpack("<%df" % (self.nx * self.nz), raw)
        for d, p in paths[1:]:
            with open(p, "rb") as f:
                if f.read() != raw:
                    self.note = f"the bed of {d} differs from {d0}'s; {d0}'s is used"

    def floor(self, x, z):
        ix = min(self.nx - 1, max(0, int(x // self.cell)))
        iz = min(self.nz - 1, max(0, int(z // self.cell)))
        return self.v[ix * self.nz + iz]


class Acc:
    def __init__(self):
        self.n = self.isl = self.bed = self.open = self.off = self.off_bed = 0
        self.sum_h = self.off_sum_h = 0.0
        self.hs = []

    def add(self, over, h):
        self.n += 1
        self.sum_h += h
        self.hs.append(h)
        near = h <= NEAR_BED
        if near:
            self.bed += 1
        if over:
            self.isl += 1
        else:
            self.off += 1
            self.off_sum_h += h
            if near:
                self.off_bed += 1
            else:
                self.open += 1


def median(xs):
    xs = sorted(xs)
    n = len(xs)
    if not n:
        return None
    return xs[n // 2] if n % 2 else 0.5 * (xs[n // 2 - 1] + xs[n // 2])


def f3(x, spec="{:.3f}"):
    return "-" if x is None else spec.format(x)


def share(a, b):
    return a / b if b else None


def read_world(path, a):
    ns = SimpleNamespace(year=6000.0, every=1500.0, end=a.end, footprint_at=100.0)
    w = r59.World(path, ns, r60.NoSwimmers())
    f = r60.Fed(w, ns)
    bed = Bed(w)
    nx, nz, c = f.nx, f.nz, f.cell
    end = w.end
    edges = []
    lo = 0.0
    while lo < end - 1e-9:
        hi = min(end, lo + a.bin)
        edges.append((lo, hi))
        lo = hi
    accs = [{g: Acc() for g, _ in GUILDS} for _ in edges]
    rows = [0] * len(edges)
    remains = 0
    last = None
    for t, line in w.seg_positions(lambda t: t <= end + 1e-9 and abs(t / a.every - round(t / a.every)) < 1e-6):
        k = next((i for i, (lo, hi) in enumerate(edges) if lo < t <= hi + 1e-9), None)
        if k is None:
            continue
        rows[k] += 1
        bodies = json.loads(line)["b"]
        last = (t, line, k, len(bodies))
        for b in bodies:
            x, y, z, fl = b[1], b[2], b[3], b[4]
            ix, iz = min(nx - 1, max(0, int(x // c))), min(nz - 1, max(0, int(z // c)))
            over = f.comp[ix][iz] >= 0
            h = y - bed.floor(x, z)
            if fl & REMAINS:
                remains += 1
            for g, test in GUILDS:
                if test(fl):
                    accs[k][g].add(over, h)
    check = None
    if last:
        t, line, k, n = last
        bodies = json.loads(line)["b"]
        theirs = f.crowd(t, bodies)
        on_theirs = round(theirs["on"] * n) if theirs["on"] is not None and n else 0
        # Recount this row's island bodies here, by the same column rule, for a like-for-like compare.
        on_mine = sum(1 for b in bodies
                      if f.comp[min(nx - 1, max(0, int(b[1] // c)))][min(nz - 1, max(0, int(b[3] // c)))] >= 0)
        check = (t, n, on_mine, on_theirs, theirs["on"])
    return w, f, bed, edges, rows, accs, remains, check


def print_world(w, f, bed, edges, rows, accs, remains, check, a):
    r60.header(w, f)
    print(f"   bed: fields/bed.f32, {bed.nx} x {bed.nz} cells of {bed.cell:g} m (floor y, metres, negative down); near the bed is "
          f"height above it <= {NEAR_BED:g} m; sampled every {a.every:g} s, bins of {a.bin:g} s; "
          f"{remains} remains bodies (flag 8) counted among the bodies read")
    if bed.note:
        print(f"   note: {bed.note}")
    if check:
        t, n, mine, theirs, share_ = check
        print(f"   check at the last sampled row, {t:g} s: {n} bodies, over an island: here {mine}, r60-read.py's Fed.crowd() "
              f"{theirs} ({share_:.4f} of the row) -> {'equal' if mine == theirs else 'DIFFER'}")
    for (lo, hi), by, nr in zip(edges, accs, rows):
        print(f"\n   {lo:g}-{hi:g} s ({nr} rows)")
        print(f"   {'guild':<9} {'n/row':>7} {'isl':>6} {'bed':>6} {'h-mean':>7} {'h-med':>6} {'open':>6} {'off':>6} {'off:bed':>7} {'off:h':>6}")
        for g, _ in GUILDS:
            x = by[g]
            print(f"   {g:<9} {f3(share(x.n, nr), '{:.1f}'):>7} {f3(share(x.isl, x.n)):>6} {f3(share(x.bed, x.n)):>6} "
                  f"{f3(share(x.sum_h, x.n), '{:.2f}'):>7} {f3(median(x.hs), '{:.2f}'):>6} {f3(share(x.open, x.n)):>6} "
                  f"{f3(share(x.off, x.n)):>6} {f3(share(x.off_bed, x.off)):>7} {f3(share(x.off_sum_h, x.off), '{:.2f}'):>6}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--bin", type=float, default=3000.0)
    ap.add_argument("--end", type=float, default=24000.0)
    ap.add_argument("--every", type=float, default=100.0)
    a = ap.parse_args()
    r59.runrec.VERBOSE = False
    print(f"== island guard: bins of {a.bin:g} s to {a.end:g} s, positions rows every {a.every:g} s")
    for d in a.runs:
        print_world(*read_world(d, a), a)


if __name__ == "__main__":
    main()
