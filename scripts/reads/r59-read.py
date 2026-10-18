"""The reader for round 59's clauses P1 to P4, G1, D1, D3, D4, C1, C2 and B1 (the draft pre-registration,
logbook/specs/r59-prereg/draft.txt, "Predictions" and "The readings", as D176 and D177 ruled it): three isolated worlds
on round 57's year, every one with the leaf smell in its direction form, seeds 2 and 3 planted with the ten
swimmers of inocula/r59-direction-tops through the trickle's pool, seed 1 the control. D176 set the planting:
the swimmers take half the trickle beside round 54's four stomachs at one founder in ten, the floor draws
nothing from the pool, and the leaf smell's recorder writes leafscent.jsonl. D177 made a mouth's equipment a
third as dear; each world's header prints the prices and the consumer cell's upkeep its config carries.

Not read here: D2 (the planted lines steer by the smell) is read by the nursery's bench from genomes extracted
after the round (scripts/reads/tops-exam-read.py); B2 (the card holds) by scripts/card/card-run-check.ps1; B3 (a
world resumes) by scripts/reads/group-resume-compare.py.

Usage: python -I r59-read.py RUN [RUN ...] [--end 48000] [--year 6000] [--every 1500] [--no-positions]
                             [--smoke] [--swimmers DIR]

A RUN is a run directory (the folder holding run.json) or an arm folder, of which the latest dated run
directory is read. A run whose run.json carries resumedFrom is read as one world with the run it resumed:
the earlier run's rows to the resumed second, then its own (as scripts/reads/r58-stitch-iso.py joins them),
followed back as far as the chain goes, each earlier run found at runs/<arm>/<run> beside the arm folder.

Which world is which. A world is planted when its pool (pool/NN.json) holds at least one of the ten swimmers,
matched by SHA-256 of the pool file against the swimmers' origins.tsv (the file's bytes, one line and a
newline); where no hash matches, by the parsed genome against each swimmer file, which the header says. Every
other world is a control. The draft's design has seed 1 the control and seeds 2 and 3 planted; the header
warns when a seed's designation differs.

Lines and guilds, from lineage.jsonl. A birth with no parent roots a line (a root lineage) of its own; every
later birth belongs to its parent's root. A founder is labelled by its birth row's src (floor, trickle or
pool), a pool founder also by its pool index against pool/index.tsv and, if its genome is one of the ten, by
the swimmer's file name. A planted founder is a pool founder whose genome is a swimmer; round 54's stomachs in
the pool are pool founders but not planted. A planted line is one whose root is a planted founder, and the
planted-descended living are every living body of a planted line, the founder included. The world's own lines
are those whose root is not a pool founder. Guilds from each body's birth row (round 58's reader's rule and the
draft's definitions): a leaf has pho 1, jnt 0 and abs 0; a stomach has abs 1; a jointed body jnt 1; a mouthed
body atk 1 and ink 1; a grazer is a mouthed body with pho 0. A kill row ("e":"k") counts as one kill; its victim
is the row's id and its attacker the row's by; a leaf kill is one whose victim is a leaf. Each kill is put in the
year k for which 6,000 (k-1) < t <= 6,000 k, and its attacker in one of: a planted founder (the attacker is
itself a planted founder), a planted descendant (of a planted line, not its founder), a stomach-pool line (root
a pool founder that is not a swimmer), the world's own lines, or unknown (an attacker never born in the record).

Samples are taken from lineage.jsonl at every --every seconds and at each year's end (a sample at T counts
every row with t <= T), up to --end or the run's last stats row, whichever is earlier. A run that stops
short of --end is short, and the verdict line says so. On a short run P1 and P2 can hold (a child born, a year
held, stay so) but not fail, P3 and D1 are unread, and C1, C2 and G1 are unread for any year not reached;
a year read that misses its bar still fails. The years and seconds below are at the default --year of 6,000 s;
P2's first year and C2's third follow --year, and C1's seconds and round 58's means do not.

P1: per planted world, the planted founders with at least one birth row naming them as parent. Holds if every
    planted world given has at least one.
P2: per planted world, the planted-descended living at each sample after 6,000 s; the longest unbroken run of
    samples with at least 20, spanning its last sample minus its first. Holds if in at least one planted world
    that span is at least one year. The longest span with at least 20 living followed row by row through the
    lineage, rather than at the samples, is printed beside it as a check.
P3: per planted world at the end read, the planted-descended living over all living. Holds if at least 1% in
    at least one planted world.
P4: read only in a planted world where P2 holds, at the first year's end at or after P2's run has lasted a year
    (its first sample plus one year), or at the end read if that is earlier: the share of the planted-descended
    living that are grazers. Holds if over a half in at least one such world. Unread where P2 fails.
G1: per control world, leaf kills by grazers of the world's own lines, per year. Holds if every control given
    has at least 100 in at least 2 of years 6, 7 and 8.
D1: per world, leaf kills in years 2 to 8 (6,000 < t <= 48,000), every attacker counted. Holds if each planted
    world has at least 50 and at least twice the control's (the largest control's when several are given).
    Unread without a control. The split by attacker is printed beside it.
D3: per planted world, from leafscent.jsonl's rows with 3,000 <= t <= 48,000 (half a year to eight years):
    the grazers whose brains read the leaf smell (each row's gread) against those whose brains do not (gctl),
    each side's mean cosine weighted by the row's n, and its body-samples (n summed). Read in a world only
    where each side has at least 1,000 body-samples and the run is not short; holds there when the readers'
    mean exceeds the others' by at least 0.1. The clause holds when it holds in every planted world given.
D4: the same in each control, for the world's own grazers: the per-body rows ("b") with the grazer flag whose
    body's line is the world's own (not founded from a pool), split by the reads flag, each body-sample
    weighted one, which is the rows' n-weighting restricted to those bodies. The rows' gread and gctl, which
    also count any grazer of a stomach-pool line, are printed beside it.
C1: per world, stats.jsonl's alive at each year's end, years 2 to 8, against round 58's isolated means
    (3,792, 4,669, 4,981, 5,577, 6,249, 6,823 and 6,500 at 12,000 to 48,000 s). Holds in a world when every
    year is between half and twice the mean; the clause holds when it holds in every world given.
C2: per world, the largest root lineage's share of the living at 18,000 s, the end of year 3. Holds in a world
    above a half; the clause holds when it holds in every world given. Whether that line is planted is printed.
B1: per world, at every stats row: |auditResidual| under 0.1 J, |matterResidual| under 1e-5 units and diverged
    0; and run.json's divergedTotal 0, status "ended", reason "budget" and simulatedSeconds equal to
    requestedSeconds. The clause holds when every world given holds.

Readings, as the draft lists them: per world and year, the most living in summer, the fewest in winter, the
largest root lineage's share at the year's end, and the leaves', stomachs', jointed, mouthed and grazers'
living there; leaf kills and all kills by attacker class; partsKilled, bodiesEaten, unitsEaten and corpsesEaten
in the year (stats.jsonl's running totals differenced). The run keeps no row for a meal, so which body ate is
not in it; kills are the only per-line sign of grazing. Per planted world at every sample: swimmers admitted,
their children, the planted-descended living by guild, the planted lines alive and the largest, and the
swimmers that died in the span by cause and median age. Per swimmer genome: founders admitted, with a child,
descendants born, living at the end and the largest line. Where the grazers are (unless --no-positions): at
each year's end, from positions.jsonl, the median distance from each grazer to its nearest leaf, against as
many random points (at least 200), each at a grazer's depth drawn at random and uniform over the tank's
footprint (a disc of radius sqrt(area/pi) centred at (R, R), TankGeometry.cs; a square of side sqrt(area) for a
box world). The bed is not read, so a random point may lie under the bed's relief. With the leaf smell's
recorder (leafscent.jsonl, D176; LeafScentRecorder.cs): at each sample, n and the mean cosine between motion and
the smell's gradient for bodies whose brain reads the smell and those whose does not, over all bodies and over
grazers; per year, those means weighted by n; and in a planted world, the same for planted-descended grazers
from the per-body rows. A resumed run appends to the recorder's file, so the last row of each second is kept.

--smoke reads a short founded run (round 59's smoke, to 4,500 s) for the draft's smoke list: the pool's
genomes and the swimmers' hashes, D176's shares from the config and index.tsv's weights, every pool founder's
birth row against stats' poolSpawns, each swimmer's flags, admission, age at
death or alive, cause, reserve at death, eating where absorptive.jsonl holds a row for it, children, kill rows,
its positions samples and how far it moved, its leaf smell cosine where the recorder holds it, and the leaves'
living at the end. It counts the swimmers dead before 600 s, since D176 sends the round back to the owner if
all of them die within minutes. card-run-check.ps1 is read elsewhere; B1 is read here as for a round.

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import csv
import glob
import hashlib
import json
import math
import os
import random
import statistics
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import runrec  # noqa: E402

YEAR = 6000.0
END = 48000.0
EVERY = 1500.0
SUMMER_FRAC = (-250.0 / 6000.0, 2750.0 / 6000.0)
WINTER_FRAC = (2750.0 / 6000.0, 5750.0 / 6000.0)
SWIMMERS = os.path.join(ROOT, "inocula", "r59-direction-tops")

P2_MIN_LIVING = 20
P2_AFTER_YEARS = 1
P3_MIN_SHARE = 0.01
P4_MIN_SHARE = 0.5
G1_MIN_KILLS = 100
G1_YEARS = (6, 7, 8)
G1_YEARS_NEEDED = 2
D1_YEARS = (2, 8)
D1_MIN_KILLS = 50
D1_RATIO = 2.0
C1_R58_MEANS = {12000.0: 3792, 18000.0: 4669, 24000.0: 4981, 30000.0: 5577, 36000.0: 6249, 42000.0: 6823,
                48000.0: 6500}
C1_BAND = (0.5, 2.0)
C2_AT_YEARS = 3
C2_MIN_SHARE = 0.5
D34_FROM_YEARS = 0.5  # 3,000 s at the default year
D34_TO_YEARS = 8.0    # 48,000 s
D34_MIN_GAIN = 0.1
D34_MIN_SAMPLES = 1000
B1_AUDIT_J = 0.1
B1_MATTER = 1e-5
RANDOM_POINTS_MIN = 200
EARLY_DEATH_S = 600.0

# Guild bits from a birth row.
LEAF, STOMACH, JOINTED, MOUTHED, GRAZER = 1, 2, 4, 8, 16
GUILDS = (("leaf", LEAF), ("stomach", STOMACH), ("jointed", JOINTED), ("mouthed", MOUTHED), ("grazer", GRAZER))
CLASSES = ("planted founder", "planted descendant", "stomach-pool line", "world's own", "unknown")
STATS_KEYS = ("t", "alive", "auditResidual", "matterResidual", "diverged", "partsKilled", "bodiesEaten",
              "unitsEaten", "corpsesEaten", "poolSpawns", "trickleSpawns", "floorSpawns")


def bits_of(r):
    j, a, p = r.get("jnt") == 1, r.get("abs") == 1, r.get("pho") == 1
    m = r.get("atk") == 1 and r.get("ink") == 1
    b = 0
    if p and not j and not a:
        b |= LEAF
    if a:
        b |= STOMACH
    if j:
        b |= JOINTED
    if m:
        b |= MOUTHED
        if not p:
            b |= GRAZER
    return b


def rows_of(path):
    """A JSONL file's complete rows (plain or .gz); a live run's last line may be half written and is skipped."""
    if not os.path.exists(path) and os.path.exists(path + ".gz"):
        path += ".gz"
    if not os.path.exists(path):
        return
    import gzip
    opener = gzip.open if path.endswith(".gz") else open
    with opener(path, "rt", encoding="utf-8") as f:
        for line in f:
            if line.endswith("\n") and line.strip():
                yield json.loads(line)


def resolve(d):
    if os.path.exists(os.path.join(d, "run.json")):
        return os.path.normpath(d)
    dirs = sorted(x for x in glob.glob(os.path.join(d, "*")) if os.path.exists(os.path.join(x, "run.json")))
    if not dirs:
        sys.exit(f"no run.json in {d} or its subfolders")
    return os.path.normpath(dirs[-1])


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def fmt(x, f="{:.3f}"):
    return "-" if x is None else f.format(x)


def median(xs):
    return statistics.median(xs) if xs else None


def year_of(t, year):
    return max(1, int(math.ceil(t / year - 1e-9)))


def find(config, name):
    if isinstance(config, dict):
        for k, v in config.items():
            if k == name:
                return v
            got = find(v, name)
            if got is not None:
                return got
    return None


def close(a, b, rel=1e-6):
    """Two parsed genomes equal, numbers within rel."""
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(close(a[k], b[k], rel) for k in a)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(close(x, y, rel) for x, y in zip(a, b))
    if isinstance(a, bool) or isinstance(b, bool):
        return a == b
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return abs(a - b) <= rel * max(1.0, abs(a), abs(b))
    return a == b


class Swimmers:
    """The ten planted genomes: origins.tsv's names and hashes, and each file parsed."""

    def __init__(self, folder):
        self.folder = folder
        self.by_hash = {}
        self.parsed = {}
        path = os.path.join(folder, "origins.tsv")
        if not os.path.exists(path):
            sys.exit(f"no origins.tsv in {folder}")
        with open(path, encoding="utf-8") as f:
            for row in csv.DictReader(f, delimiter="\t"):
                name = row["file"][:-5] if row["file"].endswith(".json") else row["file"]
                self.by_hash[row["sha256"].lower()] = name
                gp = os.path.join(folder, row["file"])
                if os.path.exists(gp):
                    with open(gp, encoding="utf-8") as g:
                        self.parsed[name] = json.loads(g.read())
        self.names = sorted(self.by_hash.values())

    def match(self, path):
        """(name or None, how): by the pool file's SHA-256 (line endings as LF), else by the parsed genome."""
        with open(path, "rb") as f:
            raw = f.read()
        lf = raw.replace(b"\r\n", b"\n")
        if not lf.endswith(b"\n"):
            lf += b"\n"
        h = hashlib.sha256(lf).hexdigest()
        if h in self.by_hash:
            return self.by_hash[h], "sha256"
        try:
            g = json.loads(raw.decode("utf-8"))
        except ValueError:
            return None, "unparsed"
        for name, s in self.parsed.items():
            if close(g, s):
                return name, "parsed genome"
        return None, "-"


class World:
    def __init__(self, d, a, swimmers):
        self.dir = resolve(d)
        self.year, self.every = a.year, a.every
        self.notes = []
        self.segs = self._chain()
        self.manifest = load_json(os.path.join(self.dir, "run.json"))
        self.config = load_json(os.path.join(self.dir, "config.json"))
        m = self.manifest
        self.name = m.get("arm") or os.path.basename(os.path.dirname(self.dir))
        self.seed = m.get("seed")
        self._pool(swimmers)
        self._stats()
        self.end = min(a.end, self.reach)
        self.short = self.reach < a.end - 1e-9
        if self.short:
            self.notes.append(f"the run reaches {self.reach:g} s, short of --end {a.end:g} s; read to {self.end:g} s")
        self._lineage()
        self._years()
        self.positions_read = None
        self.scent = None

    # -- the chain of resumed runs
    def _chain(self):
        segs = [[self.dir, None, None]]  # dir, lo (exclusive), hi (inclusive)
        cur = self.dir
        seen = {cur}
        while True:
            m = load_json(os.path.join(cur, "run.json"))
            rf = m.get("resumedFrom")
            if not rf:
                break
            at = float(rf["seconds"])
            segs[0][1] = at
            prev = os.path.normpath(os.path.join(os.path.dirname(os.path.dirname(cur)), rf["arm"], rf["run"]))
            if not os.path.exists(os.path.join(prev, "run.json")) or prev in seen:
                self.notes.append(f"{os.path.basename(cur)} resumed from {rf['arm']}/{rf['run']} at {at:g} s, "
                                  f"not found at {prev}; its rows before {at:g} s are not read")
                break
            segs.insert(0, [prev, None, at])
            seen.add(prev)
            cur = prev
        return segs

    def seg_rows(self, name):
        for d, lo, hi in self.segs:
            for r in rows_of(os.path.join(d, name)):
                t = r.get("t")
                if t is None:
                    continue
                if lo is not None and t <= lo:
                    continue
                if hi is not None and t > hi:
                    continue
                yield r

    def seg_positions(self, keep):
        """(t, line) for positions rows whose t passes keep(t), across the chain."""
        for d, lo, hi in self.segs:
            if not runrec.has_positions(d):
                continue
            for line in runrec.positions_lines(d):
                if not line.startswith('{"t":'):
                    continue
                t = float(line[5:line.index(",")])
                if lo is not None and t <= lo:
                    continue
                if hi is not None and t > hi:
                    break
                if keep(t):
                    yield t, line

    # -- the pool and its swimmers
    def _pool(self, swimmers):
        last = self.segs[-1][0]
        folder = os.path.join(last, "pool")
        self.pool = {}  # index -> dict(name, how, origin cells)
        self.index_rows = []
        if os.path.exists(os.path.join(folder, "index.tsv")):
            with open(os.path.join(folder, "index.tsv"), encoding="utf-8") as f:
                self.index_rows = list(csv.DictReader(f, delimiter="\t"))
        files = sorted(glob.glob(os.path.join(folder, "*.json")))
        for p in files:
            stem = os.path.basename(p)[:-5]
            if not stem.isdigit():
                continue
            i = int(stem)
            name, how = swimmers.match(p)
            cells = self.index_rows[i] if i < len(self.index_rows) else {}
            self.pool[i] = {"swimmer": name, "how": how, "cells": cells}
        self.swimmer_pool = {i: v["swimmer"] for i, v in self.pool.items() if v["swimmer"]}
        self.planted = bool(self.swimmer_pool)

    def pool_label(self, i):
        v = self.pool.get(i)
        if v is None:
            return f"pool {i} (no pool file)"
        if v["swimmer"]:
            return v["swimmer"]
        c = v["cells"]
        extra = "/".join(str(c.get(k)) for k in ("origin", "kind", "body") if c.get(k) not in (None, "", "-"))
        return f"pool {i}" + (f" ({extra})" if extra else "")

    # -- stats
    def _stats(self):
        self.stats = []
        for r in self.seg_rows("stats.jsonl"):
            self.stats.append({k: r.get(k) for k in STATS_KEYS})
        self.reach = self.stats[-1]["t"] if self.stats else 0.0
        self.stat_t = {s["t"]: s for s in self.stats}

    def stat_at(self, t):
        if t in self.stat_t:
            return self.stat_t[t]
        best = None
        for s in self.stats:
            if s["t"] <= t:
                best = s
            else:
                break
        return best

    # -- lineage, one pass
    def _lineage(self):
        year, every, end = self.year, self.every, self.end
        marks = set()
        k = 1
        while k * every <= end + 1e-9:
            marks.add(round(k * every, 6))
            k += 1
        y = 1
        while y * year <= end + 1e-9:
            marks.add(round(y * year, 6))
            y += 1
        marks.add(round(end, 6))
        marks = sorted(marks)
        self.marks = marks

        self.born = {}        # id -> (t, parent, bits)
        self.root = {}        # id -> root id
        self.rootinfo = {}    # root id -> (label kind, src, pool index, swimmer name or None, t)
        self.planted_roots = set()
        self.swimmer_founders = []
        self.children = Counter()   # parent id -> children
        self.desc_born = Counter()  # root -> births of the line after its founder
        self.deaths = {}            # id -> (t, cause, res)
        self.kills = []             # (t, victim, attacker, root lost, parts)
        alive = {}
        alive_in = Counter()
        g_all = Counter()
        g_pl = Counter()
        n_pl = 0
        sw_admitted = 0
        sw_children = 0
        self.snap = {}
        self.p2_runs = []
        p2_start = None
        prev_t = -1.0
        mi = 0
        pool_rows_bad = []

        def take(at):
            top = max(alive_in.items(), key=lambda kv: kv[1], default=(None, 0))
            pl_lines = [(alive_in[r], r) for r in self.planted_roots if alive_in.get(r, 0) > 0]
            self.snap[at] = {"alive": len(alive), "planted": n_pl, "g": dict(g_all), "g_pl": dict(g_pl),
                             "top_root": top[0], "top": top[1],
                             "top_share": top[1] / len(alive) if alive else None,
                             "sw_admitted": sw_admitted, "sw_children": sw_children,
                             "pl_lines": len(pl_lines), "pl_largest": max(pl_lines, default=(0, None))}

        def add(bits, planted, sign):
            for _, gb in GUILDS:
                if bits & gb:
                    g_all[gb] += sign
                    if planted:
                        g_pl[gb] += sign

        for r in self.seg_rows("lineage.jsonl"):
            e = r.get("e")
            t = r["t"]
            if t < prev_t:
                self.notes.append(f"lineage out of time order at t={t}")
            prev_t = t
            while mi < len(marks) and t > marks[mi]:
                take(marks[mi])
                mi += 1
            if t > end:
                break
            if e == "k":
                self.kills.append((t, r["id"], r.get("by"), r.get("root") == 1, r.get("parts")))
                continue
            i = r["id"]
            if e == "b":
                p = r.get("p", -1)
                bits = bits_of(r)
                self.born[i] = (t, p, bits)
                if p >= 0 and p in self.root:
                    ri = self.root[p]
                    self.root[i] = ri
                    self.children[p] += 1
                    self.desc_born[ri] += 1
                    if p in self._sw_set:
                        sw_children += 1
                else:
                    if p >= 0:
                        self.notes.append(f"birth {i} names parent {p}, never born in the record; rooted at itself")
                    self.root[i] = ri = i
                    k = r.get("k")
                    src = r.get("src")
                    pidx = r.get("pool")
                    sw = None
                    if src == "pool":
                        if pidx is None:
                            pool_rows_bad.append(i)
                        else:
                            sw = self.swimmer_pool.get(pidx)
                    label = src if k == "f" and src else {"i": "inoculant", "m": "arrival", "r": "orphan"}.get(k, k)
                    self.rootinfo[i] = (label, src, pidx, sw, t)
                    if sw:
                        self.planted_roots.add(i)
                        self.swimmer_founders.append(i)
                        sw_admitted += 1
                planted = ri in self.planted_roots
                alive[i] = bits
                alive_in[ri] += 1
                n_pl += planted
                add(bits, planted, 1)
            elif e == "d":
                self.deaths[i] = (t, r.get("c"), r.get("res"))
                if i not in alive:
                    continue
                bits = alive.pop(i)
                ri = self.root[i]
                alive_in[ri] -= 1
                if alive_in[ri] == 0:
                    del alive_in[ri]
                planted = ri in self.planted_roots
                n_pl -= planted
                add(bits, planted, -1)
            else:
                continue
            # P2's row-by-row check: the planted-descended living at or over the bar
            if n_pl >= P2_MIN_LIVING and p2_start is None:
                p2_start = t
            elif n_pl < P2_MIN_LIVING and p2_start is not None:
                self.p2_runs.append((p2_start, t))
                p2_start = None
        while mi < len(marks):
            take(marks[mi])
            mi += 1
        if p2_start is not None:
            self.p2_runs.append((p2_start, end))
        if pool_rows_bad:
            self.notes.append(f"{len(pool_rows_bad)} pool founders' rows carry no pool index, first id {pool_rows_bad[0]}")
        self.alive_end = alive
        self.alive_in_end = dict(alive_in)
        self.samples = [t for t in marks if abs(t / every - round(t / every)) < 1e-9]
        worst = max(((abs(self.snap[t]["alive"] - self.stat_t[t]["alive"]), t) for t in self.marks
                     if t in self.stat_t), default=None)
        if worst and worst[0] > 0:
            self.notes.append(f"lineage's living differs from stats' alive by up to {worst[0]} (at {worst[1]:g} s)")

    @property
    def _sw_set(self):
        # the founders admitted so far; a set kept in step with swimmer_founders
        if getattr(self, "_sw_cache_n", -1) != len(self.swimmer_founders):
            self._sw_cache = set(self.swimmer_founders)
            self._sw_cache_n = len(self.swimmer_founders)
        return self._sw_cache

    def line_class(self, i):
        r = self.root.get(i)
        if r is None:
            return "unknown"
        info = self.rootinfo[r]
        if info[3]:
            return "planted founder" if i == r else "planted descendant"
        if info[1] == "pool":
            return "stomach-pool line"
        return "world's own"

    def is_leaf(self, i):
        b = self.born.get(i)
        return b is not None and bool(b[2] & LEAF)

    def is_grazer(self, i):
        b = self.born.get(i)
        return b is not None and bool(b[2] & GRAZER)

    def _years(self):
        self.years = {}
        y = 1
        while True:
            base = (y - 1) * self.year
            s0, s1 = base + SUMMER_FRAC[0] * self.year, base + SUMMER_FRAC[1] * self.year
            w0, w1 = base + WINTER_FRAC[0] * self.year, base + WINTER_FRAC[1] * self.year
            if w1 > self.end:
                break
            summer = [s["alive"] for s in self.stats if max(s0, 0) <= s["t"] < s1]
            winter = [s["alive"] for s in self.stats if w0 <= s["t"] < w1]
            ye = round(y * self.year, 6)
            self.years[y] = {"most": max(summer) if summer else None, "fewest": min(winter) if winter else None,
                             "snap": self.snap.get(ye)}
            y += 1
        # kills by year and class
        self.kill_year = defaultdict(lambda: {"all": Counter(), "leaf": Counter(), "root": 0, "leaf_own_grazer": 0})
        for t, v, by, rootlost, parts in self.kills:
            y = year_of(t, self.year)
            c = self.line_class(by)
            k = self.kill_year[y]
            k["all"][c] += 1
            k["root"] += bool(rootlost)
            if self.is_leaf(v):
                k["leaf"][c] += 1
                if c == "world's own" and self.is_grazer(by):
                    k["leaf_own_grazer"] += 1

    # -- positions: where the grazers are, at the year ends
    def read_positions(self):
        L = None
        area = find(self.config, "worldAreaSquareMetres")
        shape = find(self.config, "worldShape")
        targets = [round(y * self.year, 6) for y in self.years if y * self.year <= self.end + 1e-9]
        if not targets or area is None:
            self.positions_read = {}
            return
        tank = shape is None or str(shape).lower() == "tank"
        R = math.sqrt(area / math.pi) if tank else None
        L = math.sqrt(area)
        wanted = {}
        pending = {}
        tgt = sorted(targets)

        def keep(t):
            return any(x - 60 < t <= x for x in tgt)

        for t, line in self.seg_positions(keep):
            for x in tgt:
                if x - 60 < t <= x:
                    pending[x] = (t, line)
        rng = random.Random(5959)
        out = {}
        for x in tgt:
            if x not in pending:
                continue
            t, line = pending[x]
            row = json.loads(line)
            leaves, grazers = [], []
            for b in row["b"]:
                i = b[0]
                bb = self.born.get(i)
                if bb is None:
                    continue
                if bb[2] & LEAF:
                    leaves.append((b[1], b[2], b[3]))
                if bb[2] & GRAZER:
                    grazers.append((b[1], b[2], b[3], self.root.get(i) in self.planted_roots))
            if not leaves or not grazers:
                out[x] = {"t": t, "grazers": len(grazers), "leaves": len(leaves)}
                continue
            grid = Grid(leaves, 4.0)
            d_g = [grid.nearest(g[0], g[1], g[2]) for g in grazers]
            d_pl = [d for d, g in zip(d_g, grazers) if g[3]]
            n_rand = max(RANDOM_POINTS_MIN, len(grazers))
            d_r = []
            for _ in range(n_rand):
                yy = rng.choice(grazers)[1]
                if tank:
                    rr = R * math.sqrt(rng.random())
                    th = rng.random() * 2 * math.pi
                    px, pz = R + rr * math.cos(th), R + rr * math.sin(th)
                else:
                    px, pz = rng.random() * L, rng.random() * L
                d_r.append(grid.nearest(px, yy, pz))
            out[x] = {"t": t, "grazers": len(grazers), "leaves": len(leaves), "med": median(d_g),
                      "med_pl": median(d_pl), "n_pl": len(d_pl), "rand": median(d_r), "n_rand": n_rand}
        self.positions_read = out

    # -- the leaf smell's recorder
    def read_scent(self):
        names = [d for d, _, _ in self.segs if os.path.exists(os.path.join(d, "leafscent.jsonl"))]
        if not names:
            self.scent = None
            return
        last = {}
        for r in self.seg_rows("leafscent.jsonl"):
            if r["t"] <= self.end + 1e-9:
                last[r["t"]] = r
        rows = [last[t] for t in sorted(last)]
        per_year = defaultdict(lambda: {k: [0, 0.0] for k in ("read", "ctl", "gread", "gctl", "pgread", "pgctl")})
        lo, hi = D34_FROM_YEARS * self.year, D34_TO_YEARS * self.year
        # D3 and D4's window: the rows' grazer aggregates, and the world's own grazers from the per-body rows
        win = {k: [0, 0.0] for k in ("gread", "gctl", "ogread", "ogctl")}
        for r in rows:
            y = year_of(r["t"], self.year) if r["t"] > 0 else 1
            acc = per_year[y]
            inside = lo - 1e-9 <= r["t"] <= hi + 1e-9
            for k in ("read", "ctl", "gread", "gctl"):
                n, c = r[k][0], r[k][1]
                if n and c is not None:
                    acc[k][0] += n
                    acc[k][1] += n * c
                    if inside and k in win:
                        win[k][0] += n
                        win[k][1] += n * c
            for b in r.get("b", []):
                if b[3] != 1:
                    continue
                if self.planted and self.root.get(b[0]) in self.planted_roots:
                    k = "pgread" if b[2] == 1 else "pgctl"
                    acc[k][0] += 1
                    acc[k][1] += b[1] / 1000.0
                if inside and self.line_class(b[0]) == "world's own":
                    k = "ogread" if b[2] == 1 else "ogctl"
                    win[k][0] += 1
                    win[k][1] += b[1] / 1000.0
        self.scent = {"rows": rows, "per_year": dict(per_year), "window": (lo, hi), "win": win}


class Grid:
    """Nearest point by a horizontal grid of cell metres, searched ring by ring."""

    def __init__(self, pts, cell):
        self.cell = cell
        self.cells = defaultdict(list)
        for p in pts:
            self.cells[(int(p[0] // cell), int(p[2] // cell))].append(p)
        xs = [k[0] for k in self.cells]
        zs = [k[1] for k in self.cells]
        self.span = max(max(xs) - min(xs), max(zs) - min(zs)) + 2

    def nearest(self, x, y, z):
        cx, cz = int(x // self.cell), int(z // self.cell)
        best = None
        r = 0
        while r <= self.span + abs(cx) + abs(cz):
            for i in range(cx - r, cx + r + 1):
                for j in (range(cz - r, cz + r + 1) if i in (cx - r, cx + r) else (cz - r, cz + r)):
                    for p in self.cells.get((i, j), ()):
                        d = (p[0] - x) ** 2 + (p[1] - y) ** 2 + (p[2] - z) ** 2
                        if best is None or d < best:
                            best = d
            if best is not None and math.sqrt(best) <= r * self.cell:
                break
            r += 1
        return math.sqrt(best) if best is not None else None


# ---------------------------------------------------------------------------------------------- printing

def header(w):
    m, c = w.manifest, w.config
    src = m.get("source") or {}
    print(f"{w.name}: {w.dir}")
    if len(w.segs) > 1:
        print("   read as one world: " + " then ".join(
            f"{os.path.basename(os.path.dirname(d))}/{os.path.basename(d)}"
            + (f" to {hi:g} s" if hi is not None else f" from {lo:g} s") for d, lo, hi in w.segs))
    print(f"   seed {w.seed}, {'PLANTED' if w.planted else 'control'}; engine {m.get('engine')} "
          f"transport {m.get('transport')}; configHash {m.get('configHash')}; gitCommit "
          f"{str(src.get('gitCommit'))[:10]} gitDirty {src.get('gitDirty')} (source.gitDirty); status "
          f"{m.get('status')} reason {m.get('reason')}, {m.get('simulatedSeconds')} of {m.get('requestedSeconds')} s")
    consumer = next((ct.get("upkeepWattsPerCubicMetre") for ct in c.get("cellTypes", []) if ct.get("id") == "consumer"), None)
    print(f"   world: turningDrag {find(c, 'turningDrag')} spinDrag {find(c, 'spinDragCoefficient')}, "
          f"senseBedTouch {find(c, 'senseBedTouch')}, leafScent {find(c, 'leafScent')} range "
          f"{find(c, 'leafScentRangeMetres')} m cell {find(c, 'leafScentCellMetres')} m half "
          f"{find(c, 'leafScentHalfScaleSquareMetres')} m2; attack {find(c, 'attackWattsPerUnit')} "
          f"intake {find(c, 'intakeWattsPerUnit')} W/unit, consumer upkeep {consumer} W/m3")
    print(f"   founding: trickle {find(c, 'foundingTricklePerSecond')}/s, trickle pool share "
          f"{find(c, 'foundingTricklePoolShare')}, floor pool share {find(c, 'foundingFloorPoolShare')}, pool count "
          f"{find(c, 'foundingTricklePoolCount')}, pool hash {str(find(c, 'foundingTricklePoolHash'))[:16]}")
    if w.pool:
        sw = sorted(w.swimmer_pool.items())
        hows = Counter(w.pool[i]["how"] for i, _ in sw)
        print(f"   pool: {len(w.pool)} genomes, index.tsv {len(w.index_rows)} rows; swimmers {len(sw)}"
              + (f" (matched by {', '.join(f'{k} {n}' for k, n in hows.items())})" if sw else "")
              + "; others: " + ", ".join(w.pool_label(i) for i in sorted(w.pool) if i not in w.swimmer_pool))
        if sw:
            weights = [w.pool[i]["cells"].get("weight") for i, _ in sw]
            print("   swimmers in the pool: " + ", ".join(f"{i}:{n}" for i, n in sw)
                  + (f"; weights {sorted(set(weights))}" if any(weights) else ""))
    else:
        print("   pool: none")
    want_planted = w.seed in (2, 3)
    if w.seed in (1, 2, 3) and want_planted != w.planted:
        print(f"   WARNING: seed {w.seed} is {'planted' if w.planted else 'a control'}, against the draft's design")
    for n in w.notes:
        print(f"   NOTE: {n}")


def guild_cells(g):
    return " ".join(f"{g.get(gb, 0):>6}" for _, gb in GUILDS)


def print_years(worlds):
    print("\n== readings: per world and year (living at the year's end by guild; kills in the year; mouth totals)")
    print(f"   {'world':<10} {'yr':>2} {'summer':>6} {'winter':>6} {'top':>6} {'alive':>6} "
          + " ".join(f"{n[:6]:>6}" for n, _ in GUILDS)
          + f" {'kills':>6} {'leafK':>6} {'rootK':>6} {'parts':>6} {'eaten':>6} {'units':>9} {'corps':>6}")
    for w in worlds:
        prev = None
        for y, rec in w.years.items():
            s = rec["snap"] or {}
            st = w.stat_at(round(y * w.year, 6)) or {}
            k = w.kill_year.get(y) if y in w.kill_year else None
            kills = sum(k["all"].values()) if k else 0
            leafk = sum(k["leaf"].values()) if k else 0
            rootk = k["root"] if k else 0

            def diff(key):
                a, b = st.get(key), (prev or {}).get(key, 0) if prev is not None else 0
                return None if a is None else a - (b or 0)
            print(f"   {w.name:<10} {y:>2} {fmt(rec['most'], '{}'):>6} {fmt(rec['fewest'], '{}'):>6} "
                  f"{fmt(s.get('top_share')):>6} {s.get('alive', '-'):>6} {guild_cells(s.get('g', {}))} "
                  f"{kills:>6} {leafk:>6} {rootk:>6} {fmt(diff('partsKilled'), '{}'):>6} "
                  f"{fmt(diff('bodiesEaten'), '{}'):>6} {fmt(diff('unitsEaten'), '{:.2f}'):>9} "
                  f"{fmt(diff('corpsesEaten'), '{}'):>6}")
            prev = st
        if not w.years:
            print(f"   {w.name:<10} no full year by {w.end:g} s")
        tail = [(t, s) for t, s in w.kill_year.items() if t not in w.years]
        for y, k in sorted(tail):
            print(f"   {w.name:<10} {y:>2} (part year to {w.end:g} s) kills {sum(k['all'].values())}, "
                  f"leaf kills {sum(k['leaf'].values())}")
    print("\n== readings: kills per world and year by attacker class (all / on leaves)")
    for w in worlds:
        if not w.kills:
            print(f"   {w.name}: no kill rows to {w.end:g} s")
            continue
        for y in sorted(w.kill_year):
            k = w.kill_year[y]
            print(f"   {w.name} year {y}: " + "; ".join(f"{c} {k['all'][c]}/{k['leaf'][c]}" for c in CLASSES
                                                       if k['all'][c]) + f"; leaf kills by own-line grazers "
                  f"{k['leaf_own_grazer']}")


def print_planted(w):
    print(f"\n== readings: {w.name}, planted, at every {w.every:g} s")
    print(f"   {'t':>7} {'alive':>6} {'adm':>4} {'kids':>5} {'pl.liv':>6} {'share':>6} "
          + " ".join(f"{n[:6]:>6}" for n, _ in GUILDS) + f" {'lines':>5} {'largest':>7}  swimmers died in the span")
    deaths = sorted((w.deaths[i][0], i) for i in w.swimmer_founders if i in w.deaths)
    prev = 0.0
    for t in w.samples:
        s = w.snap[t]
        died = [i for dt, i in deaths if prev < dt <= t]
        causes = Counter(w.deaths[i][1] for i in died)
        ages = [w.deaths[i][0] - w.born[i][0] for i in died]
        lg = s["pl_largest"]
        largest = f"{lg[0]}" + (f" {w.rootinfo[lg[1]][3][-7:]}" if lg[1] is not None else "")
        print(f"   {t:>7g} {s['alive']:>6} {s['sw_admitted']:>4} {s['sw_children']:>5} {s['planted']:>6} "
              f"{(s['planted'] / s['alive'] if s['alive'] else 0):>6.1%} {guild_cells(s['g_pl'])} {s['pl_lines']:>5} "
              f"{largest:>7}  {len(died)}"
              + (f" ({', '.join(f'{c} {n}' for c, n in causes.items())}; median age {median(ages):.0f} s)" if died else ""))
        prev = t
    print(f"   by swimmer: founders admitted / with a child / descendants born / living at {w.end:g} s / largest line")
    per = defaultdict(lambda: [0, 0, 0, 0, 0])
    for i in w.swimmer_founders:
        name = w.rootinfo[i][3]
        p = per[name]
        p[0] += 1
        p[1] += w.children[i] > 0
        p[2] += w.desc_born[i]
        n = w.alive_in_end.get(i, 0)
        p[3] += n
        p[4] = max(p[4], n)
    for name in sorted(per, key=lambda k: -per[k][3]):
        print(f"   {name:<28} " + " / ".join(str(x) for x in per[name]))


def print_positions(w):
    if not w.positions_read:
        return
    print(f"\n== readings: {w.name}, where the grazers are (median nearest-leaf distance, m)")
    for x, r in sorted(w.positions_read.items()):
        if "med" not in r:
            print(f"   {x:>7g} (sample {r['t']:g}): grazers {r['grazers']}, leaves {r['leaves']}; not read")
            continue
        print(f"   {x:>7g} (sample {r['t']:g}): grazers {r['grazers']} at {r['med']:.2f}"
              + (f", of them planted {r['n_pl']} at {fmt(r['med_pl'], '{:.2f}')}" if w.planted else "")
              + f"; {r['n_rand']} random points at {r['rand']:.2f}; leaves {r['leaves']}; "
              f"ratio {r['med'] / r['rand']:.2f}")


def print_scent(w):
    if w.scent is None:
        print(f"\n== readings: {w.name}, leaf smell recorder: no leafscent.jsonl; not read")
        return
    rows = w.scent["rows"]
    print(f"\n== readings: {w.name}, leaf smell recorder, {len(rows)} rows (n, mean cos: readers | others; "
          f"grazers: readers | others)")
    grid = set(w.samples)
    for r in rows:
        if r["t"] not in grid:
            continue
        print(f"   {r['t']:>7g}  " + " | ".join(f"{r[k][0]} {fmt(r[k][1])}" for k in ("read", "ctl"))
              + "   grazers " + " | ".join(f"{r[k][0]} {fmt(r[k][1])}" for k in ("gread", "gctl"))
              + f"   skipped {r.get('skip')}")
    print("   per year, weighted by n: readers | others; grazers readers | others"
          + ("; planted grazers readers | others" if w.planted else ""))
    for y, acc in sorted(w.scent["per_year"].items()):
        def cell(k):
            n, s = acc[k]
            return f"{n} {fmt(s / n if n else None)}"
        keys = ("read", "ctl", "gread", "gctl") + (("pgread", "pgctl") if w.planted else ())
        print(f"   year {y}: " + " | ".join(cell(k) for k in keys))


# ---------------------------------------------------------------------------------------------- smoke

def smoke(w, swimmers):
    print(f"\n== smoke: {w.name}, read to {w.end:g} s")
    checks = []
    n_pool = len(w.pool)
    checks.append(("pool/index.tsv lists fourteen genomes", len(w.index_rows) == 14 and n_pool == 14,
                   f"index rows {len(w.index_rows)}, pool files {n_pool}"))
    checks.append(("the ten swimmers' hashes match origins.tsv",
                   sorted(w.swimmer_pool.values()) == swimmers.names and all(
                       w.pool[i]["how"] == "sha256" for i in w.swimmer_pool),
                   f"{len(w.swimmer_pool)} of {len(swimmers.names)} matched ("
                   + (", ".join(f"by {k} {n}" for k, n in Counter(w.pool[i]["how"] for i in w.swimmer_pool).items())
                      or "none") + ")"))
    pool_rows = [i for i, info in w.rootinfo.items() if info[1] == "pool"]
    bad = [i for i in pool_rows if w.rootinfo[i][2] is None]
    last = w.stats[-1] if w.stats else {}
    checks.append(("every pool founder's row reads src pool with a pool index", not bad,
                   f"{len(pool_rows)} pool rows, {len(bad)} without an index; stats poolSpawns {last.get('poolSpawns')}"))
    checks.append(("pool founders in lineage equal stats' poolSpawns", last.get("poolSpawns") == len(pool_rows),
                   f"{len(pool_rows)} against {last.get('poolSpawns')}"))
    c = w.config
    weights = {}
    for i, v in w.pool.items():
        try:
            weights[i] = float(v["cells"].get("weight"))
        except (TypeError, ValueError):
            pass
    tps, fps = find(c, "foundingTricklePoolShare"), find(c, "foundingFloorPoolShare")
    sw_w = sum(x for i, x in weights.items() if i in w.swimmer_pool)
    all_w = sum(weights.values())
    sw_share = tps * sw_w / all_w if tps is not None and all_w else None
    st_share = tps - sw_share if sw_share is not None else None
    checks.append(("D176's shares: swimmers half the trickle, stomachs a tenth, floor pool share 0",
                   sw_share is not None and abs(sw_share - 0.5) < 1e-6 and abs(st_share - 0.1) < 1e-6 and fps == 0,
                   f"trickle pool share {tps}, swimmers {fmt(sw_share)}, stomachs {fmt(st_share)}, floor {fps}"))
    checks.append(("config: spin drag 0.5, bed touch on, direction form at 6 m",
                   str(find(c, "turningDrag")).lower() == "spin" and find(c, "spinDragCoefficient") == 0.5
                   and find(c, "senseBedTouch") is True and str(find(c, "leafScent")).lower() == "direction"
                   and find(c, "leafScentRangeMetres") == 6,
                   f"{find(c, 'turningDrag')} {find(c, 'spinDragCoefficient')}, {find(c, 'senseBedTouch')}, "
                   f"{find(c, 'leafScent')} {find(c, 'leafScentRangeMetres')} m"))

    sws = w.swimmer_founders
    # each swimmer's row flags, from the birth rows (re-read for the full flags)
    flags = {}
    want = set(sws)
    for r in w.seg_rows("lineage.jsonl"):
        if r.get("e") == "b" and r["id"] in want:
            flags[r["id"]] = r
    flag_bad = [i for i in sws if not (flags[i].get("atk") == 1 and flags[i].get("ink") == 1 and
                                       flags[i].get("jnt") == 1 and flags[i].get("abs") == 0 and
                                       flags[i].get("pho") == 0)]
    checks.append(("swimmers' rows read atk 1, ink 1, jnt 1, abs 0, pho 0", sws and not flag_bad,
                   f"{len(sws)} swimmers, {len(flag_bad)} otherwise"))

    # positions: each swimmer seen and moving
    seen = defaultdict(list)
    if want:
        for t, line in w.seg_positions(lambda t: t <= w.end + 1e-9):
            row = json.loads(line)
            for b in row["b"]:
                if b[0] in want:
                    seen[b[0]].append((t, b[1], b[2], b[3]))
    moved = {}
    for i, pts in seen.items():
        path = sum(math.dist(pts[k][1:], pts[k + 1][1:]) for k in range(len(pts) - 1))
        moved[i] = (len(pts), path, math.dist(pts[0][1:], pts[-1][1:]) if len(pts) > 1 else 0.0)
    unseen = [i for i in sws if i not in seen]
    still = [i for i in sws if i in moved and moved[i][1] < 0.05]
    checks.append(("each swimmer appears in positions and moves", sws and not unseen and not still,
                   f"{len(unseen)} never in positions, {len(still)} moved under 5 cm over their samples"))

    # eating: absorptive.jsonl rows for the swimmers, if any
    eat = defaultdict(list)
    for d, lo, hi in w.segs:
        p = os.path.join(d, "absorptive.jsonl")
        if not os.path.exists(p) or not want:
            continue
        for r in rows_of(p):
            if r.get("id") in want:
                eat[r["id"]].append(r)
    # scent rows for the swimmers
    cos = defaultdict(list)
    if w.scent:
        for r in w.scent["rows"]:
            for b in r.get("b", []):
                if b[0] in want:
                    cos[b[0]].append((b[1] / 1000.0, b[2]))
    kills_by = defaultdict(list)
    for t, v, by, rootlost, parts in w.kills:
        kills_by[by].append((t, v, rootlost))

    print("   id      born  swimmer                  endow  fate                    res    kids kills(leaf) "
          "pos.samples path m  disp m  absorptive  cos (n, reads)")
    for i in sws:
        t0 = w.born[i][0]
        fr = flags[i]
        if i in w.deaths and w.deaths[i][0] <= w.end:
            dt, cause, res = w.deaths[i]
            fate = f"{cause} at {dt - t0:.0f} s"
        else:
            fate, res = f"alive, {w.end - t0:.0f} s", None
        ks = kills_by.get(i, [])
        mv = moved.get(i)
        ea = eat.get(i)
        cs = cos.get(i)
        print(f"   {i:<7} {t0:>5.0f}  {w.rootinfo[i][3]:<24} {fr.get('endow', 0):>6.1f}  {fate:<22} "
              f"{fmt(res, '{:.1f}'):>6} {w.children[i]:>5} {len(ks):>5}({sum(w.is_leaf(v) for _, v, _ in ks)}) "
              f"{(mv[0] if mv else 0):>11} {(mv[1] if mv else 0):>6.1f} {(mv[2] if mv else 0):>7.1f}  "
              + (f"{len(ea)} rows, foodW mean {statistics.mean(r.get('foodW', 0) for r in ea):.3f}" if ea else "no rows   ")
              + (f"  {len(cs)}, {statistics.mean(c for c, _ in cs):+.2f}, {cs[0][1]}" if cs else "  -"))
    died = [i for i in sws if i in w.deaths and w.deaths[i][0] <= w.end]
    ages = [w.deaths[i][0] - w.born[i][0] for i in died]
    causes = Counter(w.deaths[i][1] for i in died)
    print(f"   swimmers admitted {len(sws)}; died {len(died)} ("
          + ", ".join(f"{c} {n}" for c, n in causes.items()) + f"), median age at death {fmt(median(ages), '{:.0f}')} s, "
          f"under {EARLY_DEATH_S:g} s {sum(a < EARLY_DEATH_S for a in ages)}; alive {len(sws) - len(died)}; "
          f"with a child {sum(w.children[i] > 0 for i in sws)}")
    kids = [i for i, b in w.born.items() if b[1] in want]
    if kids:
        kd = [i for i in kids if i in w.deaths]
        print(f"   their children: {len(kids)} born, {len(kd)} died ("
              + ", ".join(f"{c} {n}" for c, n in Counter(w.deaths[i][1] for i in kd).items())
              + f"), median age at death {fmt(median([w.deaths[i][0] - w.born[i][0] for i in kd]), '{:.0f}')} s")
    print(f"   kill rows in the run: {len(w.kills)}, by class: "
          + (", ".join(f"{c} {n}" for c, n in Counter(w.line_class(by) for _, _, by, _, _ in w.kills).items()) or "none")
          + f"; on leaves {sum(w.is_leaf(v) for _, v, _, _, _ in w.kills)}")
    others = [i for i in pool_rows if i not in want]
    if others:
        od = [i for i in others if i in w.deaths]
        print(f"   other pool founders (stomachs): {len(others)} admitted, {len(od)} died, "
              f"{sum(w.children[i] > 0 for i in others)} with a child")
    leaves = sum(1 for b in w.alive_end.values() if b & LEAF)
    print(f"   leaves living at {w.end:g} s: {leaves} of {len(w.alive_end)} living")
    for name, ok, note in checks:
        print(f"   [{'ok' if ok else 'FAIL'}] {name}: {note}")


# ---------------------------------------------------------------------------------------------- clauses

def b1(w):
    worst_a = max((abs(s["auditResidual"]) for s in w.stats if s["auditResidual"] is not None), default=None)
    worst_m = max((abs(s["matterResidual"]) for s in w.stats if s["matterResidual"] is not None), default=None)
    div = max((s["diverged"] or 0 for s in w.stats), default=0)
    m = w.manifest
    ended = m.get("status") == "ended" and m.get("reason") == "budget" and \
        m.get("simulatedSeconds") == m.get("requestedSeconds")
    ok = (worst_a is not None and worst_a < B1_AUDIT_J and worst_m is not None and worst_m < B1_MATTER and div == 0
          and (m.get("divergedTotal") or 0) == 0 and ended)
    return ok, (f"audit max {fmt(worst_a, '{:.3g}')} J, matter max {fmt(worst_m, '{:.3g}')}, diverged {div}, "
                f"divergedTotal {m.get('divergedTotal')}, {m.get('status')}/{m.get('reason')} "
                f"{m.get('simulatedSeconds')} of {m.get('requestedSeconds')} s")


def short_any(ws):
    return any(w.short for w in ws)


def verdict(vs):
    """A clause over worlds: failed if any world failed, held if every one held, otherwise unread."""
    if "failed" in vs:
        return "failed"
    return "held" if vs and all(v == "held" for v in vs) else "unread"


def longest_run(series, ok):
    best = (None, None, None)
    start = None
    for t, v in series:
        if ok(v):
            if start is None:
                start = t
            if best[0] is None or t - start > best[0]:
                best = (t - start, start, t)
        else:
            start = None
    return best


def clauses(worlds, a):
    planted = [w for w in worlds if w.planted]
    controls = [w for w in worlds if not w.planted]
    out = []

    def sh(ws):
        return " [SHORT: " + ", ".join(f"{w.name} to {w.end:g} s" for w in ws if w.short) + "]" \
            if any(w.short for w in ws) else ""

    # P1
    print("\n== P1: a planted founder has a child, in each planted world")
    if not planted:
        print("   unread: no planted world given")
        out.append(("P1", "unread", "no planted world given"))
    else:
        oks = []
        for w in planted:
            n = sum(w.children[i] > 0 for i in w.swimmer_founders)
            oks.append(n > 0)
            print(f"   {w.name}: {len(w.swimmer_founders)} planted founders, {n} with a child "
                  f"({sum(w.children[i] for i in w.swimmer_founders)} children)")
        v = "held" if all(oks) else ("unread" if short_any(planted) else "failed")
        out.append(("P1", v, f"{sum(oks)} of {len(planted)} planted worlds" + sh(planted)))

    # P2
    after = P2_AFTER_YEARS * a.year
    print(f"\n== P2: >= {P2_MIN_LIVING} planted-descended living at every sample for a year after {after:g} s")
    p2 = {}
    if not planted:
        print("   unread: no planted world given")
        out.append(("P2", "unread", "no planted world given"))
    else:
        for w in planted:
            ser = [(t, w.snap[t]["planted"]) for t in w.samples if t > after]
            span, t1, t2 = longest_run(ser, lambda v: v >= P2_MIN_LIVING)
            peak = max((v for _, v in ser), default=0)
            cont = max(((min(e, w.end) - max(s, after), max(s, after), e) for s, e in w.p2_runs
                        if e > after), default=None)
            ok = span is not None and span >= w.year
            p2[id(w)] = (ok, t1, t2)
            print(f"   {w.name}: most {peak} after {after:g} s; longest run of samples >= {P2_MIN_LIVING} "
                  + (f"spans {span:g} s ({t1:g} to {t2:g} s)" if span is not None else "none")
                  + "; row by row " + (f"{cont[0]:g} s ({cont[1]:g} to {cont[2]:g} s)" if cont else "none")
                  + (" -> holds" if ok else ""))
        held = [w for w in planted if p2[id(w)][0]]
        out.append(("P2", "held" if held else ("unread" if short_any(planted) else "failed"),
                    (", ".join(w.name for w in held) if held else "no planted world") + sh(planted)))

    # P3
    print(f"\n== P3: planted-descended living >= {P3_MIN_SHARE:.0%} of the living at the end")
    if not planted:
        print("   unread: no planted world given")
        out.append(("P3", "unread", "no planted world given"))
    else:
        best = 0.0
        for w in planted:
            s = w.snap[w.marks[-1]]
            share = s["planted"] / s["alive"] if s["alive"] else 0.0
            best = max(best, share)
            print(f"   {w.name} at {w.end:g} s: {s['planted']} of {s['alive']}, {share:.2%}")
        v = "unread" if short_any(planted) else ("held" if best >= P3_MIN_SHARE else "failed")
        out.append(("P3", v, f"largest share {best:.2%}" + sh(planted)))

    # P4
    print(f"\n== P4: over {P4_MIN_SHARE:.0%} of the planted-descended living are grazers, where P2 holds")
    p2held = [w for w in planted if p2.get(id(w), (False,))[0]]
    if not p2held:
        print("   unread: P2 holds in no planted world")
        out.append(("P4", "unread", "P2 holds in no planted world"))
    else:
        oks = []
        for w in p2held:
            _, t1, _ = p2[id(w)]
            at = math.ceil((t1 + w.year) / w.year - 1e-9) * w.year
            at = at if at <= w.end else w.marks[-1]
            s = w.snap[round(at, 6)] if round(at, 6) in w.snap else w.snap[w.marks[-1]]
            g = s["g_pl"].get(GRAZER, 0)
            share = g / s["planted"] if s["planted"] else 0.0
            oks.append(share > P4_MIN_SHARE)
            print(f"   {w.name} at {at:g} s: {g} grazers of {s['planted']} planted-descended living, {share:.1%}")
        out.append(("P4", "held" if any(oks) else "failed", f"{sum(oks)} of {len(p2held)}"))

    # G1
    print(f"\n== G1: >= {G1_MIN_KILLS} leaf kills a year by own-line grazers in >= {G1_YEARS_NEEDED} of years "
          f"{G1_YEARS[0]} to {G1_YEARS[-1]}, in the control")
    if not controls:
        print("   unread: no control world given")
        out.append(("G1", "unread", "no control world given"))
    else:
        oks = []
        for w in controls:
            per = {y: (w.kill_year[y]["leaf_own_grazer"] if y in w.kill_year else 0) for y in G1_YEARS}
            read = [y for y in G1_YEARS if y * w.year <= w.end + 1e-9]
            n = sum(per[y] >= G1_MIN_KILLS for y in read)
            left = len(G1_YEARS) - len(read)
            v = "held" if n >= G1_YEARS_NEEDED else ("failed" if n + left < G1_YEARS_NEEDED else "unread")
            oks.append(v)
            print(f"   {w.name}: " + ", ".join(f"year {y} {per[y]}" + ("" if y in read else " (not reached)")
                                               for y in G1_YEARS) + f"; years at the bar {n} -> {v}")
        out.append(("G1", verdict(oks), f"{oks.count('held')} of {len(controls)} controls" + sh(controls)))

    # D1
    lo_y, hi_y = D1_YEARS
    print(f"\n== D1: leaf kills in years {lo_y} to {hi_y}, each planted world >= {D1_MIN_KILLS} and >= "
          f"{D1_RATIO:g}x the control's")
    tot = {}
    for w in worlds:
        c = Counter()
        for y in range(lo_y, hi_y + 1):
            if y in w.kill_year:
                c.update(w.kill_year[y]["leaf"])
        tot[id(w)] = c
        print(f"   {w.name} ({'planted' if w.planted else 'control'}): {sum(c.values())} leaf kills ("
              + (", ".join(f"{k} {c[k]}" for k in CLASSES if c[k]) or "none") + ")")
    if not planted or not controls:
        out.append(("D1", "unread", "needs a planted world and a control"))
    else:
        ctl = max(sum(tot[id(w)].values()) for w in controls)
        oks = [sum(tot[id(w)].values()) >= D1_MIN_KILLS and sum(tot[id(w)].values()) >= D1_RATIO * ctl
               for w in planted]
        out.append(("D1", "unread" if short_any(worlds) else ("held" if all(oks) else "failed"),
                    f"{sum(oks)} of {len(planted)} planted worlds; control {ctl}" + sh(worlds)))

    # D3 and D4
    def d34(name, ws, kr, kc, who):
        lo, hi = D34_FROM_YEARS * a.year, D34_TO_YEARS * a.year
        print(f"\n== {name}: {who}, {lo:g} to {hi:g} s: readers' mean cosine (n-weighted) exceeds the "
              f"others' by >= {D34_MIN_GAIN:g}, each side >= {D34_MIN_SAMPLES} body-samples")
        if not ws:
            print(f"   unread: no {'planted' if name == 'D3' else 'control'} world given")
            out.append((name, "unread", f"no {'planted' if name == 'D3' else 'control'} world given"))
            return
        vs = []
        for w in ws:
            if w.scent is None:
                print(f"   {w.name}: no leafscent.jsonl -> unread")
                vs.append("unread")
                continue
            (nr, sr), (nc, sc) = w.scent["win"][kr], w.scent["win"][kc]
            mr, mc = (sr / nr if nr else None), (sc / nc if nc else None)
            if nr < D34_MIN_SAMPLES or nc < D34_MIN_SAMPLES:
                v = "unread"
            elif w.short:
                v = "unread"
            else:
                v = "held" if mr - mc >= D34_MIN_GAIN else "failed"
            vs.append(v)
            extra = ""
            if name == "D4":
                (ar, asr), (ac, asc) = w.scent["win"]["gread"], w.scent["win"]["gctl"]
                extra = (f"; every grazer, from the rows' aggregates: readers {ar} at {fmt(asr / ar if ar else None)}, "
                         f"others {ac} at {fmt(asc / ac if ac else None)}")
            print(f"   {w.name}: readers {nr} body-samples at {fmt(mr)}, others {nc} at {fmt(mc)}, difference "
                  f"{fmt(mr - mc if mr is not None and mc is not None else None, '{:+.3f}')} -> {v}"
                  + (" (short run)" if w.short else "") + extra)
        out.append((name, verdict(vs), f"{vs.count('held')} of {len(ws)} worlds" + sh(ws)))

    d34("D3", planted, "gread", "gctl", "grazers in each planted world, the recorder's gread against gctl")
    d34("D4", controls, "ogread", "ogctl", "the world's own grazers in the control, from the per-body rows")

    # C1
    print(f"\n== C1: living at each year's end, years 2 to 8, within {C1_BAND[0]:g} to {C1_BAND[1]:g} of round 58's means")
    oks = []
    for w in worlds:
        cells, out_n, unread = [], 0, 0
        for t, mean in sorted(C1_R58_MEANS.items()):
            if t > w.end + 1e-9:
                cells.append(f"{t:g} -")
                unread += 1
                continue
            s = w.stat_at(t)
            r = s["alive"] / mean
            inside = C1_BAND[0] <= r <= C1_BAND[1]
            out_n += not inside
            cells.append(f"{t:g} {s['alive']} ({r:.2f}{'' if inside else ' OUT'})")
        v = "failed" if out_n else ("unread" if unread else "held")
        oks.append(v)
        print(f"   {w.name}: " + ", ".join(cells) + f" -> {v}")
    out.append(("C1", verdict(oks), f"{oks.count('held')} of {len(worlds)} worlds" + sh(worlds)))

    # C2
    c2_at = round(C2_AT_YEARS * a.year, 6)
    print(f"\n== C2: one root lineage over {C2_MIN_SHARE:.0%} of the living at {c2_at:g} s")
    oks = []
    for w in worlds:
        s = w.snap.get(c2_at)
        if s is None:
            print(f"   {w.name}: not reached -> unread")
            oks.append("unread")
            continue
        info = w.rootinfo.get(s["top_root"])
        ok = (s["top_share"] or 0) > C2_MIN_SHARE
        oks.append("held" if ok else "failed")
        print(f"   {w.name}: root {s['top_root']} ({info[0] if info else '-'}"
              + (f", pool {info[2]}" if info and info[2] is not None else "")
              + f"{', PLANTED ' + info[3] if info and info[3] else ''}, born {info[4] if info else '-'} s) "
              f"holds {s['top']} of {s['alive']}, {fmt(s['top_share'])}")
    out.append(("C2", verdict(oks), f"{oks.count('held')} of {len(worlds)} worlds" + sh(worlds)))

    # B1
    print("\n== B1: the books balance")
    oks = []
    for w in worlds:
        ok, note = b1(w)
        oks.append(ok)
        print(f"   {w.name}: {note}" + (" -> holds" if ok else ""))
    out.append(("B1", "held" if all(oks) else "failed", f"{sum(oks)} of {len(worlds)} worlds"))

    print("\n== clauses (D2 by the nursery's bench; B2 and B3 by the card's scripts)")
    for k, v, note in out:
        print(f"{k}: {v} ({note})")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=END)
    ap.add_argument("--year", type=float, default=YEAR)
    ap.add_argument("--every", type=float, default=EVERY)
    ap.add_argument("--no-positions", action="store_true")
    ap.add_argument("--smoke", action="store_true")
    ap.add_argument("--swimmers", default=SWIMMERS)
    a = ap.parse_args()
    runrec.VERBOSE = False
    swimmers = Swimmers(a.swimmers)
    worlds = [World(d, a, swimmers) for d in a.runs]
    for w in worlds:
        w.read_scent()
        if not a.no_positions:
            w.read_positions()

    print(f"== round 59's reader: year {a.year:g} s, samples every {a.every:g} s, end {a.end:g} s; "
          f"swimmers {len(swimmers.names)} from {os.path.relpath(a.swimmers, ROOT)}")
    for w in worlds:
        header(w)
    print_years(worlds)
    for w in worlds:
        if w.planted:
            print_planted(w)
    for w in worlds:
        print_positions(w)
        print_scent(w)
    if a.smoke:
        for w in worlds:
            smoke(w, swimmers)
    clauses(worlds, a)


if __name__ == "__main__":
    main()
