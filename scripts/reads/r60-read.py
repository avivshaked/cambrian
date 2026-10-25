"""The reader for round 60's clauses M1 to M7, L1, G1, S1 and B1 (the draft pre-registration,
logbook/specs/r60-prereg/0142-draft.md, "Predictions" and "The readings"): three worlds of r60s-big3's fed islands at the
round's dose, seeds 6001, 6002 and 6003, nothing planted, no control.

Not read here: B2 (the card holds) by scripts/card/card-run-check.ps1; B3 (a world resumes) by
scripts/reads/group-resume-compare.py.

Usage: python -I r60-read.py RUN [RUN ...] [--end 48000] [--year 6000] [--every 1500] [--footprint-at 100]
                             [--migrant-every 100]

A RUN is a run directory or an arm folder, of which the latest dated run is read. A run whose run.json carries
resumedFrom is read as one world with the run it resumed, as round 59's reader joins them (scripts/reads/r59-read.py,
whose World this reader takes for the lineage, the guilds, the kills, the leaf smell's recorder and B1).

The islands as seeded. The matter's columns (fields/layout.json's matter grid, 5 m) whose density over the top three
layers, in the first run of the chain's dump nearest --footprint-at seconds (100), is above the mean of the water's
columns (a column is water when its centre lies in the tank's disc); field-map.py --footprint's rule, which the
screens used. A column whose centre lies nearer the glass than the config's matterIslandGlassMarginMetres is never an
island, as Core's island plan leaves it out (D185): matter that drifts there by --footprint-at does not make the glass
an island. An island is one connected group of island columns, joined by their sides. The desert is every other
water column.

Read points: each year's end up to --end, and --end itself when it is not a year's end (the founding check reads
--end 1500). At each, per world:
  alive and the living by guild, from the lineage (round 59's rules: a leaf has pho 1, jnt 0 and abs 0; a stomach
    abs 1; a jointed body jnt 1; a mouthed body atk 1 and ink 1; a grazer a mouthed body with pho 0);
  empty 10 m squares: the positions row nearest the point (t within 60 s before it), the 10 m squares of the tank
    whose four corner metres lie in the disc, and the share of them holding no living body (crowd_vs_bed.py's rule);
  on the islands: the share of the living whose column is an island column as seeded;
  per island, the living over it, and how many islands hold at least 5% of the living;
  the desert's median column in its top 12 m, units/m3 (the top two 5 m layers and 0.4 of the third, over 12 m times
    the column's area; screen_read's rule), against a leaf's break-even of 0.00025, from the matter dump at the point;
  burial against influx over the year before the point (over the run so far before the first year's end), the
    standing stock, from stats.jsonl's running totals;
  the largest root line's share of the living;
  leaf kills by grazers in the year (a kill row whose victim was born a leaf and whose attacker was born a grazer);
  migrants in the year: the bodies seen over one island and, later in the year, over another, from the positions
    rows every --migrant-every seconds (100), with the island a body was last seen over carried across years; and of
    them the mouthed (hunters: atk 1 and ink 1), the grazers (mouthed, pho 0) and the leaves, and the bodies without
    photosynthetic tissue (m-nph: the positions row's photosynthetic flag, 4, clear in the row that sees the crossing);
  root lines with living members over two islands or more, at the point's positions row, beside the lines with any.

Clauses, each over the worlds given, with a seed not given counted as unread:
M1: >= 1,000 alive (stats.jsonl) at every year's end from year 2 to the last of --end, in 3 of 3.
M2: >= 0.3 of the 10 m squares empty at every year's end from year 2 to the last of --end, in >= 2 of 3.
M3: >= 0.6 of the living on the islands at --end, in >= 2 of 3.
M4: >= 3 islands each holding >= 5% of the living at --end, in >= 2 of 3.
M5: burial between 0.8 and 1.2 times the influx over the year ending at --end, in 3 of 3.
M6: the desert's median top-12 m column below 0.00025 units/m3 at --end, in >= 1 of 3.
M7: >= 20 crossers without photosynthetic tissue (m-nph) in each of years 6, 7 and 8, and >= 1 root line with living
    members over 2 or more islands at --end, in >= 1 of 3.
L1: no root line over half the living at --end, in >= 2 of 3.
G1: >= 100 leaf kills by grazers in >= 2 of years 6, 7 and 8, in >= 1 of 3.
S1: round 59's D3 on every world: from leafscent.jsonl's rows over 3,000 to 48,000 s, the grazers whose brains read
    the leaf smell (gread) against those whose do not (gctl), n-weighted mean cosines; a world is read only where
    each side has >= 1,000 body-samples and the run is not short, and holds there at a gain >= 0.1. Holds in >= 1 of
    3; failed when no world holds and at least one was read; otherwise unread.
B1: round 59's B1 on every world (the audit under 0.1 J and the matter residual under 1e-5 at every row, nothing
    diverged, ended on its budget), in 3 of 3.
A world that stops short of a point is unread there; a year read that misses its bar fails.

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import importlib.util
import json
import math
import os
import struct
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("r59read", os.path.join(HERE, "r59-read.py"))
r59 = importlib.util.module_from_spec(_spec)
sys.modules["r59read"] = r59
_spec.loader.exec_module(r59)

SEEDS = 3
BREAK_EVEN = 0.00025
ISLAND_SHARE = 0.05
M1_ALIVE, M2_EMPTY, M3_ON, M4_ISLANDS, M5_BAND, L1_SHARE = 1000, 0.3, 0.6, 3, (0.8, 1.2), 0.5
G1_MIN, G1_YEARS, G1_NEEDED = 100, (6, 7, 8), 2
M7_MIN, M7_YEARS = 20, (6, 7, 8)
PHOTOSYNTHETIC = 4


class NoSwimmers:
    """Round 60 plants nothing: no pool genome is a swimmer, so every world reads as unplanted."""
    names = []

    def match(self, path):
        return None, "-"


def fmt(x, f="{:.3f}"):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else f.format(x)


def floats(path):
    with open(path, "rb") as f:
        data = f.read()
    return struct.unpack("<%df" % (len(data) // 4), data[:len(data) // 4 * 4])


def dumps(d):
    out = {}
    fd = os.path.join(d, "fields")
    if os.path.isdir(fd):
        for n in os.listdir(fd):
            if n.endswith(".matter.f32"):
                out[int(n.split(".")[0])] = os.path.join(fd, n)
    return out


class Fed:
    """The islands, the squares and the matter of one world, beside round 59's World."""

    def __init__(self, w, a):
        self.w = w
        first = w.segs[0][0]
        with open(os.path.join(first, "fields", "layout.json"), encoding="utf-8") as f:
            lay = json.load(f)
        self.m = m = lay["matter"]
        for d, _, _ in w.segs[1:]:
            with open(os.path.join(d, "fields", "layout.json"), encoding="utf-8") as f:
                if json.load(f).get("matter") != m:
                    sys.exit(f"{d}: its matter grid is not {first}'s")
        self.nx, self.ny, self.nz, self.cell = m["cellsX"], m["cellsY"], m["cellsZ"], float(m["cellMetres"])
        area = float(r59.find(w.config, "worldAreaSquareMetres"))
        self.R = math.sqrt(area / math.pi)
        # The squares' disc, as crowd_vs_bed.py takes it: half the bed grid's width in 1 m cells.
        self.Rb = lay["bed"]["cellsX"] / 2.0 if "bed" in lay else self.R
        nx, nz, c, R = self.nx, self.nz, self.cell, self.R
        # D204's open square: every column is water, and the margin and the "glass" distance run from the nearest
        # edge (the matter grid tiles the square, so its sides are the square's). Every tank reads as it always did.
        self.box = r59.find(w.config, "worldShape") == "Box"
        L, W = nx * c, nz * c
        if self.box:
            self.water = [[True] * nz for _ in range(nx)]
        else:
            self.water = [[((ix + 0.5) * c - R) ** 2 + ((iz + 0.5) * c - R) ** 2 <= R * R for iz in range(nz)] for ix in range(nx)]

        def edge(ix, iz):
            x, z = (ix + 0.5) * c, (iz + 0.5) * c
            return min(x, L - x, z, W - z) if self.box else R - math.hypot(x - R, z - R)

        self.side = (L, W) if self.box else None
        d0 = dumps(first)
        if not d0:
            sys.exit(f"{first}: no matter dumps under fields/")
        self.f_at = min(d0, key=lambda k: abs(k - a.footprint_at))
        top3 = self.columns(floats(d0[self.f_at]), 3)
        live = [top3[ix][iz] for ix in range(nx) for iz in range(nz) if self.water[ix][iz]]
        mean = sum(live) / len(live)
        # D185: no island column within the glass margin, as Core's plan.
        self.margin = float(r59.find(w.config, "matterIslandGlassMarginMetres") or 0.0)
        clear = [[edge(ix, iz) >= self.margin for iz in range(nz)] for ix in range(nx)]
        self.isl = [[self.water[ix][iz] and clear[ix][iz] and top3[ix][iz] > mean for iz in range(nz)] for ix in range(nx)]
        self.n_water = len(live)
        # Islands: groups of island columns joined by their sides.
        self.comp = [[-1] * nz for _ in range(nx)]
        self.sizes = []
        for ix in range(nx):
            for iz in range(nz):
                if self.isl[ix][iz] and self.comp[ix][iz] < 0:
                    k = len(self.sizes)
                    stack, n = [(ix, iz)], 0
                    self.comp[ix][iz] = k
                    while stack:
                        x, z = stack.pop()
                        n += 1
                        for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                            u, v = x + dx, z + dz
                            if 0 <= u < nx and 0 <= v < nz and self.isl[u][v] and self.comp[u][v] < 0:
                                self.comp[u][v] = k
                                stack.append((u, v))
                    self.sizes.append(n)
        # Each island's nearest column centre to the glass, metres.
        self.glass = [math.inf] * len(self.sizes)
        for ix in range(nx):
            for iz in range(nz):
                k = self.comp[ix][iz]
                if k >= 0:
                    self.glass[k] = min(self.glass[k], edge(ix, iz))
        self.stats = list(w.seg_rows("stats.jsonl"))

    def columns(self, v, layers):
        nx, nz = self.nx, self.nz
        out = [[0.0] * nz for _ in range(nx)]
        for iy in range(min(layers, self.ny)):
            base = iy * nx * nz
            for ix in range(nx):
                row, col = base + ix * nz, out[ix]
                for iz in range(nz):
                    col[iz] += v[row + iz]
        return out

    def stat_at(self, t):
        best = None
        for s in self.stats:
            if s["t"] <= t + 1e-9:
                best = s
            else:
                break
        return best

    def dump_at(self, t):
        for d, lo, hi in self.w.segs:
            if (lo is not None and t <= lo) or (hi is not None and t > hi):
                continue
            ds = dumps(d)
            if ds:
                k = min(ds, key=lambda s: abs(s - t))
                if abs(k - t) <= 100:
                    return k, ds[k]
        return None, None

    def read(self, points):
        want = sorted(points)
        last = {}
        for t, line in self.w.seg_positions(lambda t: any(x - 60 < t <= x + 1e-9 for x in want)):
            for x in want:
                if x - 60 < t <= x + 1e-9:
                    last[x] = (t, line)
        self.at = {}
        for x in want:
            r = {"t": x}
            if x in last:
                pt, line = last[x]
                bodies = json.loads(line)["b"]
                r.update(self.crowd(pt, bodies))
            k, p = self.dump_at(x)
            if p:
                r["dump"] = k
                v = floats(p)
                cm = self.cell
                desert = sorted((v[ix * self.nz + iz] + v[(self.nx + ix) * self.nz + iz]
                                 + 0.4 * v[(2 * self.nx + ix) * self.nz + iz]) / (12 * cm * cm)
                                for ix in range(self.nx) for iz in range(self.nz)
                                if self.water[ix][iz] and not self.isl[ix][iz])
                r["desert"] = desert[len(desert) // 2] if desert else None
                r["desert_below"] = sum(d < BREAK_EVEN for d in desert) / len(desert) if desert else None
            # The year before the point, or the run so far when no row lies a year back.
            s, s0 = self.stat_at(x), self.stat_at(x - self.w.year)
            if s:
                inf = s["matterInfluxedTotal"] - (s0["matterInfluxedTotal"] if s0 else 0)
                bur = s["matterBuriedTotal"] - (s0["matterBuriedTotal"] if s0 else 0)
                r.update(alive=s["alive"], standing=s["matterStanding"], influx=inf, buried=bur,
                         burial=bur / inf if inf else None, stat_t=s["t"])
            sn = self.w.snap.get(round(x, 6))
            if sn:
                r.update(living=sn["alive"], g=sn["g"], top=sn["top_share"])
            y = r59.year_of(x, self.w.year)
            r["leaf_kills"] = sum(1 for t, vic, by, _, _ in self.w.kills
                                  if (y - 1) * self.w.year < t <= min(y * self.w.year, x) + 1e-9
                                  and self.w.is_leaf(vic) and self.w.is_grazer(by))
            self.at[x] = r

    def crowd(self, pt, bodies):
        nx, nz, c, Rb = self.nx, self.nz, self.cell, self.Rb
        sq = Counter()
        on = 0
        per = Counter()
        lines = {}
        for b in bodies:
            x, z = b[1], b[3]
            sq[(int(x // 10), int(z // 10))] += 1
            ix, iz = min(nx - 1, max(0, int(x // c))), min(nz - 1, max(0, int(z // c)))
            k = self.comp[ix][iz]
            if k >= 0:
                on += 1
                per[k] += 1
                lines.setdefault(self.w.root.get(b[0]), set()).add(k)
        cells = []
        for i in range(int(2 * Rb // 10)):
            for j in range(int(2 * Rb // 10)):
                if all((i * 10 + p + 0.5 - Rb) ** 2 + (j * 10 + q + 0.5 - Rb) ** 2 <= Rb * Rb for p in (0, 9) for q in (0, 9)):
                    cells.append(sq.get((i, j), 0))
        n = len(bodies)
        shares = sorted(((v / n, k) for k, v in per.items()), reverse=True) if n else []
        return {"pos_t": pt, "bodies": n, "empty": sum(1 for x in cells if x == 0) / len(cells) if cells else None,
                "squares": len(cells), "on": on / n if n else None, "islands": shares,
                "big": sum(1 for s, _ in shares if s >= ISLAND_SHARE),
                "lines_on": len(lines), "lines_multi": sum(1 for v in lines.values() if len(v) >= 2)}

    def migrants(self, stride, end):
        """Per year: the bodies seen over one island and later over another, and the mouthed and grazers among them."""
        nx, nz, c = self.nx, self.nz, self.cell
        last = {}
        moved = {}
        bare = {}
        for t, line in self.w.seg_positions(lambda t: t <= end + 1e-9 and abs(t / stride - round(t / stride)) < 1e-6):
            y = r59.year_of(t, self.w.year)
            got = moved.setdefault(y, set())
            nph = bare.setdefault(y, set())
            for b in json.loads(line)["b"]:
                ix, iz = min(nx - 1, max(0, int(b[1] // c))), min(nz - 1, max(0, int(b[3] // c)))
                k = self.comp[ix][iz]
                if k < 0:
                    continue
                i = b[0]
                was = last.get(i)
                if was is not None and was != k:
                    got.add(i)
                    if not (b[4] & PHOTOSYNTHETIC):
                        nph.add(i)
                last[i] = k
        self.moved = {}
        for y, ids in moved.items():
            bits = [self.w.born.get(i, (0, 0, 0))[2] for i in ids]
            self.moved[y] = (len(ids), sum(1 for b in bits if b & r59.MOUTHED), sum(1 for b in bits if b & r59.GRAZER),
                             sum(1 for b in bits if b & r59.LEAF), len(bare.get(y, ())))


def header(w, f):
    m = w.manifest
    chain = " <- ".join(f"{os.path.basename(os.path.dirname(d))}/{os.path.basename(d)}" for d, _, _ in reversed(w.segs))
    print(f"\n== {w.name}: seed {w.seed}, configHash {m.get('configHash')}, commit {str((m.get('source') or {}).get('gitCommit'))[:8]}"
          f", {m.get('status')}/{m.get('reason')}, reach {w.reach:g} s; {chain}")
    print(f"   dose: influx {r59.find(w.config, 'matterInfluxPerSecond')} units/s, budget {r59.find(w.config, 'matterBudgetUnits')} units")
    big = sorted(range(len(f.sizes)), key=lambda k: -f.sizes[k])
    print(f"   islands as seeded (dump at {f.f_at} s): {sum(f.sizes)} of {f.n_water} water columns ({sum(f.sizes) / f.n_water:.2f}) "
          f"in {len(f.sizes)} islands; the largest, in columns: " + ", ".join(f"#{k} {f.sizes[k]}" for k in big[:8])
          + f"; nearest island column centre to the glass {min(f.glass, default=float('nan')):.1f} m (margin {f.margin:g} m)")
    for note in w.notes:
        print(f"   note: {note}")


def table(w, f):
    print(f"   {'t':>6} {'alive':>6} {'leaf':>5} {'stom':>5} {'jnt':>5} {'mouth':>5} {'graz':>5} {'empty':>6} {'on-isl':>6} "
          f"{'isl>=5%':>7} {'top islands':<22} {'desert':>8} {'<b-e':>5} {'standing':>9} {'bur/inf':>7} {'top line':>8} {'leafkills':>9} "
          f"{'migr':>5} {'m-mth':>5} {'m-grz':>5} {'m-leaf':>6} {'m-nph':>5} {'lines2+':>7}")
    for x, r in sorted(f.at.items()):
        g = r.get("g", {})
        isl = " ".join(f"{s:.2f}" for s, _ in r.get("islands", [])[:4])
        print(f"   {x:>6g} {r.get('alive', '-'):>6} {g.get(r59.LEAF, 0):>5} {g.get(r59.STOMACH, 0):>5} {g.get(r59.JOINTED, 0):>5} "
              f"{g.get(r59.MOUTHED, 0):>5} {g.get(r59.GRAZER, 0):>5} {fmt(r.get('empty'), '{:.2f}'):>6} {fmt(r.get('on'), '{:.2f}'):>6} "
              f"{r.get('big', '-'):>7} {isl:<22} {fmt(r.get('desert'), '{:.5f}'):>8} {fmt(r.get('desert_below'), '{:.2f}'):>5} "
              f"{fmt(r.get('standing'), '{:.0f}'):>9} {fmt(r.get('burial'), '{:.2f}'):>7} {fmt(r.get('top'), '{:.2f}'):>8} {r['leaf_kills']:>9} "
              + "{:>5} {:>5} {:>5} {:>6} {:>5} {:>7}".format(*f.moved.get(r59.year_of(x, w.year), ('-', '-', '-', '-', '-')),
                                                 f"{r.get('lines_multi', '-')}/{r.get('lines_on', '-')}"))
    if any("pos_t" in r and abs(r["pos_t"] - x) > 1e-6 for x, r in f.at.items()):
        print("   positions rows: " + ", ".join(f"{x:g} from {r['pos_t']:g}" for x, r in sorted(f.at.items()) if "pos_t" in r))


def clause(name, k, vs, note):
    held, unread = vs.count("held"), vs.count("unread") + max(0, SEEDS - len(vs))
    v = "held" if held >= k else ("failed" if held + unread < k else "unread")
    return name, v, f"{held} of {len(vs)} worlds hold, {k} needed of {SEEDS}; {note}"


def clauses(pairs, a):
    out = []
    end = round(a.end, 6)
    years = [y for y in range(2, int(a.end // a.year + 1e-9) + 1)]

    def every_year(name, ok, k, what):
        print(f"\n== {name}: {what} at every year's end from year 2 to {years[-1] if years else '-'}")
        vs = []
        for w, f in pairs:
            cells, bad, unread = [], 0, 0
            for y in years:
                x = round(y * a.year, 6)
                r = f.at.get(x)
                val = ok(r) if r and x <= w.reach + 1e-9 else None
                if val is None:
                    unread += 1
                    cells.append(f"{x:g} -")
                else:
                    bad += not val[0]
                    cells.append(f"{x:g} {val[1]}{'' if val[0] else ' MISS'}")
            v = "failed" if bad else ("unread" if unread or not years else "held")
            vs.append(v)
            print(f"   {w.name}: " + ", ".join(cells) + f" -> {v}")
        out.append(clause(name, k, vs, what))

    def at_end(name, ok, k, what):
        print(f"\n== {name}: {what} at {end:g} s")
        vs = []
        for w, f in pairs:
            r = f.at.get(end)
            val = ok(r) if r and not w.short else None
            v = "unread" if val is None else ("held" if val[0] else "failed")
            vs.append(v)
            print(f"   {w.name}: {val[1] if val else 'not reached'} -> {v}")
        out.append(clause(name, k, vs, what))

    every_year("M1", lambda r: (r["alive"] >= M1_ALIVE, str(r["alive"])) if "alive" in r else None, 3,
               f">= {M1_ALIVE} alive")
    every_year("M2", lambda r: (r["empty"] >= M2_EMPTY, f"{r['empty']:.2f}") if r.get("empty") is not None else None, 2,
               f">= {M2_EMPTY} of the 10 m squares empty")
    at_end("M3", lambda r: (r["on"] >= M3_ON, f"{r['on']:.2f} on the islands") if r.get("on") is not None else None, 2,
           f">= {M3_ON} of the living on the islands")
    at_end("M4", lambda r: (r["big"] >= M4_ISLANDS, f"{r['big']} islands at >= 5% ("
                            + ", ".join(f"#{k} {s:.2f}" for s, k in r["islands"][:6]) + ")") if "big" in r else None, 2,
           f">= {M4_ISLANDS} islands each with >= 5% of the living")
    at_end("M5", lambda r: (r["burial"] is not None and M5_BAND[0] <= r["burial"] <= M5_BAND[1],
                            f"buried {r['buried']:.0f} of {r['influx']:.0f} influxed over the year, {fmt(r['burial'], '{:.3f}')}")
           if "influx" in r else None, 3, f"burial {M5_BAND[0]} to {M5_BAND[1]} times the influx over the last year")
    at_end("M6", lambda r: (r["desert"] < BREAK_EVEN, f"desert median {r['desert']:.6f} units/m3 (dump {r['dump']} s), "
                            f"{r['desert_below']:.2f} of desert columns below") if r.get("desert") is not None else None, 1,
           f"the desert's median top-12 m column below {BREAK_EVEN}")
    print(f"\n== M7: >= {M7_MIN} crossers without photosynthetic tissue in each of years {M7_YEARS[0]} to {M7_YEARS[-1]}, "
          f"and a root line over 2 or more islands at {end:g} s")
    vs = []
    for w, f in pairs:
        read = [y for y in M7_YEARS if y * w.year <= w.end + 1e-9]
        per = {y: f.moved.get(y, (0, 0, 0, 0, 0))[4] for y in read}
        r = f.at.get(end)
        multi = r.get("lines_multi") if r and not w.short else None
        bad = any(per[y] < M7_MIN for y in read) or multi == 0
        v = "failed" if bad else ("unread" if len(read) < len(M7_YEARS) or multi is None else "held")
        vs.append(v)
        print(f"   {w.name}: " + ", ".join(f"year {y} {per[y]}" + ("" if per[y] >= M7_MIN else " MISS") for y in read)
              + "".join(f", year {y} not reached" for y in M7_YEARS if y not in read)
              + f"; root lines over 2+ islands at {end:g} s: {'-' if multi is None else multi} -> {v}")
    out.append(clause("M7", 1, vs, "crossers without photosynthetic tissue, and a root line over 2+ islands"))
    at_end("L1", lambda r: (r["top"] is not None and r["top"] <= L1_SHARE, f"largest root line {fmt(r['top'], '{:.3f}')} of the living")
           if "top" in r else None, 2, f"no root line over {L1_SHARE} of the living")

    print(f"\n== G1: >= {G1_MIN} leaf kills by grazers in >= {G1_NEEDED} of years {G1_YEARS[0]} to {G1_YEARS[-1]}")
    vs = []
    for w, f in pairs:
        per = {y: sum(1 for t, vic, by, _, _ in w.kills if (y - 1) * w.year < t <= y * w.year
                      and w.is_leaf(vic) and w.is_grazer(by)) for y in G1_YEARS}
        read = [y for y in G1_YEARS if y * w.year <= w.end + 1e-9]
        n = sum(per[y] >= G1_MIN for y in read)
        left = len(G1_YEARS) - len(read)
        v = "held" if n >= G1_NEEDED else ("failed" if n + left < G1_NEEDED else "unread")
        vs.append(v)
        print(f"   {w.name}: " + ", ".join(f"year {y} {per[y]}" + ("" if y in read else " (not reached)") for y in G1_YEARS) + f" -> {v}")
    out.append(clause("G1", 1, vs, "leaf kills by grazers"))

    lo, hi = r59.D34_FROM_YEARS * a.year, r59.D34_TO_YEARS * a.year
    print(f"\n== S1: grazers, {lo:g} to {hi:g} s: readers' mean cosine exceeds the others' by >= {r59.D34_MIN_GAIN:g}, "
          f"each side >= {r59.D34_MIN_SAMPLES} body-samples")
    vs = []
    for w, f in pairs:
        if w.scent is None:
            print(f"   {w.name}: no leafscent.jsonl -> unread")
            vs.append("unread")
            continue
        (nr, sr), (nc, sc) = w.scent["win"]["gread"], w.scent["win"]["gctl"]
        mr, mc = (sr / nr if nr else None), (sc / nc if nc else None)
        v = "unread" if (nr < r59.D34_MIN_SAMPLES or nc < r59.D34_MIN_SAMPLES or w.short) else \
            ("held" if mr - mc >= r59.D34_MIN_GAIN else "failed")
        vs.append(v)
        print(f"   {w.name}: readers {nr} at {fmt(mr)}, others {nc} at {fmt(mc)}, difference "
              f"{fmt(mr - mc if mr is not None and mc is not None else None, '{:+.3f}')} -> {v}" + (" (short run)" if w.short else ""))
    s1 = "held" if "held" in vs else ("failed" if "failed" in vs else "unread")
    out.append(("S1", s1, f"{vs.count('held')} held, {vs.count('failed')} failed, {vs.count('unread')} unread of {len(vs)} worlds"))

    print("\n== B1: the books balance")
    vs = []
    for w, f in pairs:
        ok, note = r59.b1(w)
        vs.append("held" if ok else "failed")
        print(f"   {w.name}: {note}" + (" -> holds" if ok else ""))
    out.append(clause("B1", 3, vs, "audit, matter, divergence, ended on budget"))

    print("\n== clauses (B2 by card-run-check.ps1, B3 by group-resume-compare.py)")
    for k, v, note in out:
        print(f"{k}: {v} ({note})")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=48000.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--every", type=float, default=1500.0)
    ap.add_argument("--footprint-at", type=float, default=100.0)
    ap.add_argument("--migrant-every", type=float, default=100.0)
    a = ap.parse_args()
    r59.runrec.VERBOSE = False
    pairs = []
    for d in a.runs:
        w = r59.World(d, a, NoSwimmers())
        w.read_scent()
        f = Fed(w, a)
        pts = [round(y * a.year, 6) for y in range(1, int(a.end // a.year + 1e-9) + 1)]
        if round(a.end, 6) not in pts:
            pts.append(round(a.end, 6))
        f.read([x for x in pts if x <= w.end + 1e-9])
        f.migrants(a.migrant_every, w.end)
        pairs.append((w, f))
    print(f"== round 60's reader: year {a.year:g} s, end {a.end:g} s, islands as seeded at {a.footprint_at:g} s")
    for w, f in pairs:
        header(w, f)
        table(w, f)
    clauses(pairs, a)


if __name__ == "__main__":
    main()
