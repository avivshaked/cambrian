"""The reader for round 61's clauses P1 to P7, L1, S1, C1 and B1 (logbook/0144, "Predictions" and "The readings"): four
worlds of round 60's fed islands on one fixed landscape, r61-p6101 and r61-p6102 planted (the nursery's best leaf
swimmers and four stomachs through the trickle), r61-c6101 and r61-c6102 their unplanted controls. The letter in a
world's name says which it is, and the number is the life seed: a planted world is paired with the control of the same
seed.

Not read here: B2 (the card holds) by scripts/card/card-run-check.ps1; B3 (a world resumes) by
scripts/reads/group-resume-compare.py.

Usage: python -I r61-read.py RUN [RUN ...] [--end 48000] [--year 6000] [--every 1500] [--footprint-at 100]
                             [--migrant-every 100] [--seeds 6101,6102] [--grazer mouth|nopho]

A RUN is a run directory or an arm folder, of which the latest dated run is read. A world is labelled from its arm's
name (r61-p6101 and so on); a run whose name is not of that form is read and printed but takes no part in a clause.
A run whose run.json carries resumedFrom is read as one world with the run it resumed, as round 59's reader joins them
(scripts/reads/r59-read.py, whose World this reader takes for the lineage, the guilds, the kills, the leaf smell's
recorder and B1). The per-world reading is round 60's (scripts/reads/r60-read.py), copied: the year-end table, the
island maps, the migrants.

The islands as seeded. The matter's columns (fields/layout.json's matter grid, 5 m) whose density over the top three
layers, in the first run of the chain's dump nearest --footprint-at seconds (100), is above the mean of the water's
columns (a column is water when its centre lies in the tank's disc); field-map.py --footprint's rule, which the
screens used. A column whose centre lies nearer the glass than the config's matterIslandGlassMarginMetres is never an
island, as Core's island plan leaves it out (D185). An island is one connected group of island columns, joined by
their sides. The desert is every other water column.

Read points: each year's end up to --end, and --end itself when it is not a year's end. At each, per world: alive and
the living by guild, from the lineage (round 59's rules: a leaf has pho 1, jnt 0 and abs 0; a stomach abs 1; a jointed
body jnt 1; a mouthed body atk 1 and ink 1; a nopho grazer a mouthed body with pho 0); empty 10 m squares (the
positions row nearest the point, t within 60 s before it; crowd_vs_bed.py's rule); the share of the living on the
islands as seeded; the living over each island, and how many islands hold at least 5% of the living; the desert's
median top-12 m column against a leaf's break-even; burial against influx over the year before the point; the largest
root line's share of the living; leaf kills in the year by a grazer as round 60 took it (attacker born mouthed with
pho 0; the table's leafkills column); migrants in the year (bodies seen over one island and later over another, from
the positions rows every --migrant-every seconds), and among them the mouthed, the nopho grazers, the leaves and the
bodies without photosynthetic tissue; root lines with living members over two islands or more.

A grazer in the clauses. The pre-registration says a grazer is a body with a mouth, so P1, P2 and P7 count bodies born
mouthed (atk 1 and ink 1; --grazer mouth, the default). --grazer nopho counts round 60's narrower grazer, a mouthed
body with pho 0, instead. Both numbers are always printed: the other one beside the verdict's own.

Clauses (a world that stops short of a point is unread there; a year read that misses its bar fails; a world that was
not given is unread):
P1: >= 100 grazers alive at every year's end from year 2 to the last of --end, in both planted worlds.
P2: >= 100 leaf kills a year by grazers in >= 2 of years 6 to 8, in both planted worlds and in at most one control.
P3: at --end each planted world's living leaves <= 0.7 of its control's.
P4: at --end each planted world has >= 0.1 more of its 10 m squares empty than its control.
P5: at --end each planted world has >= 0.1 more of its living on the islands than its control.
P6: at --end >= 3 islands each hold >= 5% of the living, in both planted worlds.
P7: >= 20 grazers a year in each of years 6, 7 and 8 seen over one island and later over another, in >= 1 planted world.
L1: at --end no root line holds over half the living, in >= 1 planted world.
S1: round 59's D3 on each planted world: from leafscent.jsonl's rows over 3,000 to 48,000 s, the grazers whose brains
    read the leaf smell (gread) against those whose do not (gctl), n-weighted mean cosines; a world is read only where
    each side has >= 1,000 body-samples and the run is not short, and holds there at a gain >= 0.1. Holds in >= 1
    planted world; failed when none holds and at least one was read; otherwise unread.
C1: in both controls, < 0.6 of the living on the islands at --end and one root line over half the living.
B1: round 59's B1 on every world (the audit under 0.1 J and the matter residual under 1e-5 at every row, nothing
    diverged, ended on its budget), in 4 of 4.

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import importlib.util
import json
import math
import os
import re
import struct
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("r59read", os.path.join(HERE, "r59-read.py"))
r59 = importlib.util.module_from_spec(_spec)
sys.modules["r59read"] = r59
_spec.loader.exec_module(r59)

BREAK_EVEN = 0.00025
ISLAND_SHARE = 0.05
PHOTOSYNTHETIC = 4
HELD, FAILED, UNREAD = "held", "failed", "unread"

P1_MIN = 100
P2_MIN, P2_YEARS, P2_YEARS_NEEDED, P2_CONTROLS_AT_MOST = 100, (6, 7, 8), 2, 1
P3_RATIO = 0.7
P4_GAIN = 0.1
P5_GAIN = 0.1
P6_ISLANDS = 3
P7_MIN, P7_YEARS = 20, (6, 7, 8)
L1_SHARE = 0.5
C1_ON_BELOW, C1_TOP_OVER = 0.6, 0.5
# A trailing letter, with a number after it, names a world resumed under a new arm (r61-p6103r after the giants
# fix, r61-p6101x for the extension, r61-p6101x2 for the extension resumed); the reader joins it with the runs it
# resumed, back along the chain, so it stands for the same world.
LABEL = re.compile(r"^r61-([pc])(\d+)(?:[a-z]\d*)?$")


class NoSwimmers:
    """The reader's clauses never ask which lines are the planted ones: a planted world's arm name says it is planted."""
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
    """The islands, the squares and the matter of one world, beside round 59's World (round 60's, copied)."""

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
        self.water = [[((ix + 0.5) * c - R) ** 2 + ((iz + 0.5) * c - R) ** 2 <= R * R for iz in range(nz)] for ix in range(nx)]
        d0 = dumps(first)
        if not d0:
            sys.exit(f"{first}: no matter dumps under fields/")
        self.f_at = min(d0, key=lambda k: abs(k - a.footprint_at))
        top3 = self.columns(floats(d0[self.f_at]), 3)
        live = [top3[ix][iz] for ix in range(nx) for iz in range(nz) if self.water[ix][iz]]
        mean = sum(live) / len(live)
        # D185: no island column within the glass margin, as Core's plan.
        self.margin = float(r59.find(w.config, "matterIslandGlassMarginMetres") or 0.0)
        clear = [[R - math.hypot((ix + 0.5) * c - R, (iz + 0.5) * c - R) >= self.margin for iz in range(nz)] for ix in range(nx)]
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
                    g = R - math.hypot((ix + 0.5) * c - R, (iz + 0.5) * c - R)
                    self.glass[k] = min(self.glass[k], g)
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
        # Per year: (crossers, mouthed crossers, nopho-grazer crossers, leaf crossers, crossers without
        # photosynthetic tissue). P7 reads the mouthed count (or the nopho-grazer count with --grazer nopho): the
        # crossers whose body was born with a mouth, counted as the crosser count is, over the same rows.
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


def pair_table(lanes):
    """Each planted world beside the control on its seed, at the read points both have."""
    for lane in lanes:
        if not (lane["p"] and lane["c"]):
            continue
        (pw, pf), (cw, cf) = lane["p"], lane["c"]
        print(f"\n== seed {lane['seed']}: {pw.name} (planted) beside {cw.name} (control); each cell planted/control")
        print(f"   {'t':>6} {'alive':>11} {'leaf':>11} {'mouth':>9} {'empty':>11} {'on-isl':>11} {'isl>=5%':>7} {'top line':>11}")
        for x in sorted(set(pf.at) & set(cf.at)):
            rp, rc = pf.at[x], cf.at[x]

            def two(key, f="{:.2f}"):
                return f"{fmt(rp.get(key), f)}/{fmt(rc.get(key), f)}"

            def gl(bit):
                return f"{rp.get('g', {}).get(bit, '-')}/{rc.get('g', {}).get(bit, '-')}"
            print(f"   {x:>6g} {two('alive', '{:.0f}'):>11} {gl(r59.LEAF):>11} {gl(r59.MOUTHED):>9} {two('empty'):>11} "
                  f"{two('on'):>11} {two('big', '{:.0f}'):>7} {two('top'):>11}")


def all_of(vs):
    """Every one must hold: failed if any failed, unread if any is unread, else held."""
    if FAILED in vs:
        return FAILED
    return UNREAD if (UNREAD in vs or not vs) else HELD


def clause(name, k, vs, what):
    """k of len(vs) worlds must hold."""
    held, unread = vs.count(HELD), vs.count(UNREAD)
    v = HELD if held >= k else (FAILED if held + unread < k else UNREAD)
    return name, v, f"{held} of {len(vs)} worlds hold, {k} needed of {len(vs)}; {what}"


def clauses(lanes, a):
    out = []
    end = round(a.end, 6)
    years = list(range(2, int(a.end // a.year + 1e-9) + 1))
    bit, other = (r59.MOUTHED, r59.GRAZER) if a.grazer == "mouth" else (r59.GRAZER, r59.MOUTHED)
    moved_ix, other_ix = (1, 2) if a.grazer == "mouth" else (2, 1)
    gname, oname = ("mouthed", "nopho grazers") if a.grazer == "mouth" else ("nopho grazers", "mouthed")

    def at_end(w, f):
        r = f.at.get(end)
        return r if r and not w.short else None

    def leaf_kills(w, y, b):
        """Kill rows in year y whose victim was born a leaf and whose attacker was born with guild bit b."""
        return sum(1 for t, vic, by, _, _ in w.kills if (y - 1) * w.year < t <= y * w.year
                   and w.is_leaf(vic) and (w.born.get(by) or (0, 0, 0))[2] & b)

    def over_lanes(name, what, sides, fn, need):
        """Run fn(lane) on each lane whose worlds in sides were given, print, and count the verdicts."""
        print(f"\n== {name}: {what}")
        vs = []
        for lane in lanes:
            missing = [s for s in sides if lane[s] is None]
            if missing:
                v, text = UNREAD, "world not given: " + ", ".join(f"{'planted' if s == 'p' else 'control'} {lane['seed']}"
                                                                  for s in missing)
            else:
                v, text = fn(lane)
            vs.append(v)
            label = "/".join(lane[s][0].name if lane[s] else f"{s}{lane['seed']}" for s in sides)
            print(f"   {label}: {text} -> {v}")
        return vs

    # ---- P1
    def p1(lane):
        w, f = lane["p"]
        cells, bad, unread = [], 0, 0
        for y in years:
            x = round(y * a.year, 6)
            r = f.at.get(x)
            if not r or x > w.reach + 1e-9 or "g" not in r:
                unread += 1
                cells.append(f"{x:g} -")
                continue
            n, o = r["g"].get(bit, 0), r["g"].get(other, 0)
            bad += n < P1_MIN
            cells.append(f"{x:g} {n} ({o} {oname})" + ("" if n >= P1_MIN else " MISS"))
        return (FAILED if bad else (UNREAD if unread or not years else HELD)), ", ".join(cells)
    vs = over_lanes("P1", f">= {P1_MIN} grazers ({gname}) alive at every year's end from year 2 to {years[-1] if years else '-'}"
                    ", in both planted worlds", ("p",), p1, 2)
    out.append(("P1", all_of(vs), f"{vs.count(HELD)} of {len(vs)} planted worlds hold, both needed"))

    # ---- P2
    def p2(w):
        per = {y: leaf_kills(w, y, bit) for y in P2_YEARS}
        oth = {y: leaf_kills(w, y, other) for y in P2_YEARS}
        read = [y for y in P2_YEARS if y * w.year <= w.end + 1e-9]
        n = sum(per[y] >= P2_MIN for y in read)
        left = len(P2_YEARS) - len(read)
        v = HELD if n >= P2_YEARS_NEEDED else (FAILED if n + left < P2_YEARS_NEEDED else UNREAD)
        return v, ", ".join(f"year {y} {per[y]} ({oth[y]} by {oname})" + ("" if y in read else " (not reached)") for y in P2_YEARS)
    print(f"\n== P2: >= {P2_MIN} leaf kills a year by grazers ({gname}) in >= {P2_YEARS_NEEDED} of years {P2_YEARS[0]} to "
          f"{P2_YEARS[-1]}, in both planted worlds and in at most {P2_CONTROLS_AT_MOST} control")
    side_v = {"p": [], "c": []}
    for side, word in (("p", "planted"), ("c", "control")):
        for lane in lanes:
            if lane[side] is None:
                v, text = UNREAD, "world not given"
                name = f"{word} {lane['seed']}"
            else:
                v, text = p2(lane[side][0])
                name = lane[side][0].name
            side_v[side].append(v)
            print(f"   {name}: {text} -> {v}")
    planted_v = all_of(side_v["p"])
    held_c, unread_c = side_v["c"].count(HELD), side_v["c"].count(UNREAD)
    control_v = FAILED if held_c > P2_CONTROLS_AT_MOST else (UNREAD if held_c + unread_c > P2_CONTROLS_AT_MOST else HELD)
    print(f"   planted worlds: {planted_v}; controls holding the same bar: {held_c} of {len(lanes)} "
          f"(at most {P2_CONTROLS_AT_MOST}) -> {control_v}")
    out.append(("P2", all_of([planted_v, control_v]),
                f"planted {side_v['p'].count(HELD)} of {len(lanes)} hold, both needed; controls {held_c} of {len(lanes)} hold, "
                f"at most {P2_CONTROLS_AT_MOST}"))

    # ---- P3, P4, P5: each planted world against the control on its seed, at --end
    def versus(what_of, ok_text):
        def fn(lane):
            (pw, pf), (cw, cf) = lane["p"], lane["c"]
            rp, rc = at_end(pw, pf), at_end(cw, cf)
            if rp is None or rc is None:
                return UNREAD, "not reached"
            got = what_of(rp, rc)
            return (UNREAD, "no reading at " + f"{end:g} s") if got is None else (HELD if got[0] else FAILED, got[1])
        return fn

    def p3_of(rp, rc):
        if "g" not in rp or "g" not in rc:
            return None
        lp, lc = rp["g"].get(r59.LEAF, 0), rc["g"].get(r59.LEAF, 0)
        return lp <= P3_RATIO * lc, f"planted {lp} living leaves, control {lc}, ratio {fmt(lp / lc if lc else None, '{:.2f}')}"
    vs = over_lanes("P3", f"at {end:g} s each planted world's living leaves <= {P3_RATIO} of its control's", ("p", "c"),
                    versus(p3_of, None), 2)
    out.append(clause("P3", 2, vs, f"living leaves at most {P3_RATIO} of the control's"))

    def p4_of(rp, rc):
        if rp.get("empty") is None or rc.get("empty") is None:
            return None
        d = rp["empty"] - rc["empty"]
        return d >= P4_GAIN, f"empty squares planted {rp['empty']:.2f}, control {rc['empty']:.2f}, difference {d:+.3f}"
    vs = over_lanes("P4", f"at {end:g} s each planted world has >= {P4_GAIN} more of its 10 m squares empty than its control",
                    ("p", "c"), versus(p4_of, None), 2)
    out.append(clause("P4", 2, vs, f"empty squares >= {P4_GAIN} above the control's"))

    def p5_of(rp, rc):
        if rp.get("on") is None or rc.get("on") is None:
            return None
        d = rp["on"] - rc["on"]
        return d >= P5_GAIN, f"on the islands planted {rp['on']:.2f}, control {rc['on']:.2f}, difference {d:+.3f}"
    vs = over_lanes("P5", f"at {end:g} s each planted world has >= {P5_GAIN} more of its living on the islands than its control",
                    ("p", "c"), versus(p5_of, None), 2)
    out.append(clause("P5", 2, vs, f"share on the islands >= {P5_GAIN} above the control's"))

    # ---- P6
    def p6(lane):
        w, f = lane["p"]
        r = at_end(w, f)
        if r is None or "big" not in r:
            return UNREAD, "not reached"
        return (HELD if r["big"] >= P6_ISLANDS else FAILED,
                f"{r['big']} islands at >= 5% (" + ", ".join(f"#{k} {s:.2f}" for s, k in r["islands"][:6]) + ")")
    vs = over_lanes("P6", f"at {end:g} s >= {P6_ISLANDS} islands each hold >= 5% of the living, in both planted worlds",
                    ("p",), p6, 2)
    out.append(clause("P6", 2, vs, f">= {P6_ISLANDS} islands each with >= 5% of the living"))

    # ---- P7
    def p7(lane):
        w, f = lane["p"]
        read = [y for y in P7_YEARS if y * w.year <= w.end + 1e-9 and y in f.moved]
        per = {y: f.moved[y][moved_ix] for y in read}
        oth = {y: f.moved[y][other_ix] for y in read}
        bad = any(per[y] < P7_MIN for y in read)
        v = FAILED if bad else (UNREAD if len(read) < len(P7_YEARS) else HELD)
        return v, (", ".join(f"year {y} {per[y]} ({oth[y]} {oname})" + ("" if per[y] >= P7_MIN else " MISS") for y in read)
                   + "".join(f", year {y} not reached" for y in P7_YEARS if y not in read))
    vs = over_lanes("P7", f">= {P7_MIN} grazers ({gname}) a year in each of years {P7_YEARS[0]} to {P7_YEARS[-1]} seen over one "
                    "island and later over another, in >= 1 planted world", ("p",), p7, 1)
    out.append(clause("P7", 1, vs, f">= {P7_MIN} grazer crossers in each of years {P7_YEARS[0]} to {P7_YEARS[-1]}"))

    # ---- L1
    def l1(lane):
        w, f = lane["p"]
        r = at_end(w, f)
        if r is None or r.get("top") is None:
            return UNREAD, "not reached"
        return HELD if r["top"] <= L1_SHARE else FAILED, f"largest root line {fmt(r['top'], '{:.3f}')} of the living"
    vs = over_lanes("L1", f"at {end:g} s no root line over {L1_SHARE} of the living, in >= 1 planted world", ("p",), l1, 1)
    out.append(clause("L1", 1, vs, f"no root line over {L1_SHARE} of the living"))

    # ---- S1
    lo, hi = r59.D34_FROM_YEARS * a.year, r59.D34_TO_YEARS * a.year

    def s1(lane):
        w, f = lane["p"]
        if w.scent is None:
            return UNREAD, "no leafscent.jsonl"
        (nr, sr), (nc, sc) = w.scent["win"]["gread"], w.scent["win"]["gctl"]
        mr, mc = (sr / nr if nr else None), (sc / nc if nc else None)
        v = UNREAD if (nr < r59.D34_MIN_SAMPLES or nc < r59.D34_MIN_SAMPLES or w.short) else \
            (HELD if mr - mc >= r59.D34_MIN_GAIN else FAILED)
        return v, (f"readers {nr} at {fmt(mr)}, others {nc} at {fmt(mc)}, difference "
                   f"{fmt(mr - mc if mr is not None and mc is not None else None, '{:+.3f}')}" + (" (short run)" if w.short else ""))
    vs = over_lanes("S1", f"grazers, {lo:g} to {hi:g} s: readers' mean cosine exceeds the others' by >= {r59.D34_MIN_GAIN:g}, "
                    f"each side >= {r59.D34_MIN_SAMPLES} body-samples, in >= 1 planted world", ("p",), s1, 1)
    s1v = HELD if HELD in vs else (FAILED if FAILED in vs else UNREAD)
    out.append(("S1", s1v, f"{vs.count(HELD)} held, {vs.count(FAILED)} failed, {vs.count(UNREAD)} unread of {len(vs)} planted worlds"))

    # ---- C1
    def c1(lane):
        w, f = lane["c"]
        r = at_end(w, f)
        if r is None or r.get("on") is None or r.get("top") is None:
            return UNREAD, "not reached"
        ok = r["on"] < C1_ON_BELOW and r["top"] > C1_TOP_OVER
        return HELD if ok else FAILED, f"{r['on']:.2f} of the living on the islands, largest root line {r['top']:.3f}"
    vs = over_lanes("C1", f"in both controls, < {C1_ON_BELOW} of the living on the islands and one root line over "
                    f"{C1_TOP_OVER} of the living at {end:g} s", ("c",), c1, 2)
    out.append(clause("C1", 2, vs, f"controls under {C1_ON_BELOW} on the islands with a root line over {C1_TOP_OVER}"))

    # ---- B1, on every world
    print("\n== B1: the books balance, in 4 of 4 worlds")
    vs = []
    for lane in lanes:
        for side in ("p", "c"):
            if lane[side] is None:
                vs.append(UNREAD)
                print(f"   {'planted' if side == 'p' else 'control'} {lane['seed']}: world not given -> unread")
                continue
            ok, note = r59.b1(lane[side][0])
            vs.append(HELD if ok else FAILED)
            print(f"   {lane[side][0].name}: {note}" + (" -> holds" if ok else ""))
    out.append(clause("B1", len(vs), vs, "audit, matter, divergence, ended on budget"))

    print("\n== clauses (B2 by card-run-check.ps1, B3 by group-resume-compare.py)")
    for k, v, note in out:
        print(f"{k}: {v} ({note})")


def label_of(w):
    """('p' or 'c', life seed) from the arm's name, else None."""
    for s in (w.name, os.path.basename(os.path.dirname(w.dir)), os.path.basename(w.dir)):
        m = LABEL.match(s or "")
        if m:
            return m.group(1), int(m.group(2))
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=48000.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--every", type=float, default=1500.0)
    ap.add_argument("--footprint-at", type=float, default=100.0)
    ap.add_argument("--migrant-every", type=float, default=100.0)
    ap.add_argument("--seeds", default="6101,6102", help="the life seeds of the round, each a planted and a control world")
    ap.add_argument("--grazer", choices=("mouth", "nopho"), default="mouth",
                    help="what P1, P2 and P7 count as a grazer: a mouthed body (the pre-registration's) or round 60's "
                         "mouthed body with pho 0")
    a = ap.parse_args()
    r59.runrec.VERBOSE = False
    seeds = [int(s) for s in a.seeds.split(",") if s.strip()]
    by = {}
    worlds = []
    for d in a.runs:
        w = r59.World(d, a, NoSwimmers())
        w.read_scent()
        f = Fed(w, a)
        pts = [round(y * a.year, 6) for y in range(1, int(a.end // a.year + 1e-9) + 1)]
        if round(a.end, 6) not in pts:
            pts.append(round(a.end, 6))
        f.read([x for x in pts if x <= w.end + 1e-9])
        f.migrants(a.migrant_every, w.end)
        lab = label_of(w)
        if lab is None:
            w.notes.append("its arm's name is not r61-p<seed> or r61-c<seed>: read here, in no clause")
        elif lab[1] not in seeds:
            w.notes.append(f"life seed {lab[1]} is not in --seeds {a.seeds}: read here, in no clause")
        elif lab in by:
            sys.exit(f"two runs are labelled {lab[0]}{lab[1]}: {by[lab][0].dir} and {w.dir}")
        else:
            by[lab] = (w, f)
            if w.seed is not None and str(w.seed) != str(lab[1]):
                w.notes.append(f"the manifest's seed {w.seed} differs from the name's {lab[1]}")
        worlds.append((w, f))
    lanes = [{"seed": s, "p": by.get(("p", s)), "c": by.get(("c", s))} for s in seeds]
    print(f"== round 61's reader: year {a.year:g} s, end {a.end:g} s, islands as seeded at {a.footprint_at:g} s, "
          f"grazer = {a.grazer}")
    for w, f in worlds:
        header(w, f)
        table(w, f)
    pair_table(lanes)
    clauses(lanes, a)


if __name__ == "__main__":
    main()
