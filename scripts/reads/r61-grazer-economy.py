"""Why round 61's grazers do not grow: births, lifespans, deaths, the energy a kill yields against upkeep, and whether
the planted swimmers swim. A read of round 61's six worlds (logbook/0144) to 72,000 s, not a clause reader.

Usage: python -I r61-grazer-economy.py RUN [RUN ...] [--end 72000] [--year 6000] [--upkeep-w 0.908]
                                       [--windows 5700:6000,...] [--no-speed]

A RUN is a run directory or an arm folder, of which the latest dated run is read. A run whose run.json carries
resumedFrom is read as one world with the runs it resumed, back to 0 s, by round 59's World
(scripts/reads/r59-read.py), as scripts/reads/r61-read.py reads it. Everything below is that reader's definitions,
reused by import: a body's guild bits from its birth row (a leaf has pho 1, jnt 0 and abs 0; a stomach abs 1; a
jointed body jnt 1; a mouthed body atk 1 and ink 1; a nopho grazer a mouthed body with pho 0), a line is the root of
its parent chain, a kill row ("e":"k") has its victim in id and its attacker in by, a leaf kill is one whose victim
was born a leaf, a year k holds 6,000 (k-1) < t <= 6,000 k. A grazer here is the pre-registration's: a body born
mouthed (--grazer mouth in r61-read.py); the nopho grazers are printed beside it. A planted founder is round 59's:
a trickle founder drawn from the pool (src pool) whose pool genome is one of the ten swimmers of
inocula/r59-direction-tops, matched by r59-read.py's Swimmers (SHA-256 of the pool file, else the parsed genome);
the four stomachs in the pool are pool founders but not planted. A planted line is one whose root is a planted
founder. The controls' pool holds the stomachs alone, so they have no planted founder.

What each section reads:

1 Births. Per year, the mouthed births by origin: planted founders; bred of a mouthed parent (planted line or
  not); bred of a parent born without a mouth; other founders born mouthed; and how many of the bred were born
  jointed (jnt 1). The mouthed living at each year's end,
  founders against bred, and those of a planted line whose founder arrived at least a year earlier (a line that
  has outlived its founder's own year). Per birth-year cohort: mean children and mean mouthed children per mouthed
  body (the replacement number; a cohort's children are counted to --end, so the last year is censored), split
  founders and bred. Per planted line: births after the founder, deepest generation, living at the end. The
  trickle's pool admissions per year from stats.jsonl's poolSpawns, differenced.
2 Lifespans and deaths. Per guild (exclusive, in this order: nopho grazer, mouthed with pho, stomach, leaf, other
  jointed, other) and per year of death: deaths, median and mean age at death (death t less birth t), and the death
  rows' causes (c: starved, eaten, aged, diverged). The grazers are split planted founder / bred. Bodies alive at
  --end are censored and counted apart.
3 Energy. A kill moves the victim's tissue (tj) and reserve (rj) into a corpse (LineageEvent.cs); a mouth then takes
  joules from any corpse in reach at its own rate and keeps 1 - intakeWasteFraction of them (WorldMouth.Take). No
  file records a meal per body: absorptive.jsonl logs only bodies with absorptive tissue, and Organism.MouthJoules is
  written only into checkpoints. So per grazer this reads the kills it made and the corpse joules those kills
  founded, kept at 1 - waste: an upper bound on what its kills could have fed it, since the corpse is shared with
  any mouth in reach and decays. Against it, the upkeep: --upkeep-w watts (the ledger's standing cost of a planted
  founder, scripts/ledger.ps1) times the grazer's life. Per birth-year cohort and origin: grazers, the share with
  at least one kill and one leaf kill, mean and median kills and leaf kills per life, mean life, the kills' corpse
  joules kept per second of life, and the share whose kills' kept joules cover upkeep times life. World-level and
  measured: per year, stats.jsonl's unitsEaten differenced times joulesPerUnit, kept at 1 - waste (every mouth's
  intake in the world), the corpse joules founded by mouthed attackers' kills, and the body-seconds lived by bodies
  born with intake (ink 1) and by mouthed bodies, from the lineage; the kept joules over the ink body-seconds is the
  mean kept income of a body with a mouth. Beside it, stats.jsonl's workJoulesPerSecond (the joints' work over the
  row's metabolic step, Farm/Metabolise.cs) times the config's workCostMultiplier over its jointed count, averaged
  over the year's rows: the mechanical work billed per jointed body, every jointed body counted.
4 The ledger is run apart (scripts/ledger.ps1); this reader prints, per swimmer pool genome, the planted founders
  admitted, the mouthed births of their lines and their living at --end, which picks the genome to screen.
5 Swimming (unless --no-speed). From positions.jsonl.gz (one row every 10 s; each body's id, x, y, z and its flags,
  1 absorptive, 2 jointed, 4 photosynthetic), in each window lo:hi seconds: for every body present in two
  consecutive rows 10 s apart, its displacement over the gap, three-dimensional and horizontal, as a speed; per
  class (jointed bodies of a planted line, other jointed bodies, rigid bodies, and rigid leaves, flags 4, among
  them), the mean and median speed over body-samples. And per body present in the window's first and last rows,
  its net horizontal displacement over the window's span, as a speed (travel, not wiggle). Beside it, stats.jsonl's
  speedJointed and speedRigid at the nearest row at or before hi. And relative to the local drift (rel): a body's
  displacement over the step less the mean displacement of the other rigid bodies within 5 m of it (at least 3), as
  a speed; for every jointed body of a planted line, and for one rigid body in 20 (by id) as the baseline, which
  measures how much a body that cannot swim departs from its neighbours' drift.

Sampling: the lineage and stats are read whole; positions only inside the windows (default: the last 300 s of each
year, and 23,000:28,000, where an earlier look found the planted swimmers at half a drifter's speed).

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import importlib.util
import json
import math
import os
import statistics
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
_spec = importlib.util.spec_from_file_location("r59read", os.path.join(HERE, "r59-read.py"))
r59 = importlib.util.module_from_spec(_spec)
sys.modules["r59read"] = r59
_spec.loader.exec_module(r59)

SWIMMERS = os.path.join(ROOT, "inocula", "r59-direction-tops")
JOINTED_FLAG, PHOTO_FLAG = 2, 4
GUILD_ORDER = ("grazer (nopho mouthed)", "mouthed with pho", "stomach", "leaf", "other jointed", "other")
CAUSES = ("starved", "eaten", "aged", "diverged")
DRIFT_CELL, DRIFT_MIN, RIGID_EVERY = 5.0, 3, 20


def fmt(x, f="{:.1f}"):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else f.format(x)


def med(xs):
    return statistics.median(xs) if xs else None


def mean(xs):
    return sum(xs) / len(xs) if xs else None


def guild(bits):
    if bits & r59.GRAZER:
        return GUILD_ORDER[0]
    if bits & r59.MOUTHED:
        return GUILD_ORDER[1]
    if bits & r59.STOMACH:
        return GUILD_ORDER[2]
    if bits & r59.LEAF:
        return GUILD_ORDER[3]
    if bits & r59.JOINTED:
        return GUILD_ORDER[4]
    return GUILD_ORDER[5]


class Economy:
    """One world: round 59's World for the chain, the lineage's guilds, lines and kills, plus a pass for what World
    does not keep (the intake flag, the generation, a founder's endowment, a kill's corpse joules)."""

    def __init__(self, d, a):
        self.a = a
        self.w = w = r59.World(d, a, r59.Swimmers(SWIMMERS))
        self.year = a.year
        self.end = w.end
        self.ink = set()
        self.gen = {}
        self.endow = {}
        self.kill_j = []  # (t, victim, attacker, tj + rj)
        for r in w.seg_rows("lineage.jsonl"):
            t = r["t"]
            if t > self.end + 1e-9:
                break
            e = r.get("e")
            if e == "b":
                i = r["id"]
                if r.get("ink") == 1:
                    self.ink.add(i)
                self.gen[i] = r.get("g", 0)
                if r.get("p", -1) < 0 and r.get("endow") is not None:
                    self.endow[i] = r["endow"]
            elif e == "k":
                self.kill_j.append((t, r["id"], r.get("by"), (r.get("tj") or 0.0) + (r.get("rj") or 0.0)))
        want = ("t", "alive", "jointed", "poolSpawns", "trickleSpawns", "unitsEaten", "corpsesEaten", "corpsesFromKills",
                "workJoulesPerSecond", "speedJointed", "speedRigid",
                "biteKeptLeaves", "biteKeptOthers", "biteKeptRemains")
        self.stats = [{k: r.get(k) for k in want} for r in w.seg_rows("stats.jsonl")]
        cfg = w.config
        self.jpu = float(r59.find(cfg, "joulesPerUnit") or 100.0)
        self.waste = float(r59.find(cfg, "intakeWasteFraction") or 0.0)
        # D219-D221: with contact eating on, a mouth's food is what its bites kept (the stats' biteKept columns, joules
        # already net of the waste), and unitsEaten (corpse meals) stays 0. keptJ is the cumulative kept intake under
        # either rule, so a reader differences it into a window whichever rule the run had; keptLivingJ is the part
        # of it bitten off the living (None under the corpse rule, where the split does not exist).
        self.contact = bool(r59.find(cfg, "contactEating"))
        keep = 1.0 - self.waste
        for s in self.stats:
            if self.contact:
                living = (s.get("biteKeptLeaves") or 0.0) + (s.get("biteKeptOthers") or 0.0)
                s["keptLivingJ"] = living
                s["keptJ"] = living + (s.get("biteKeptRemains") or 0.0)
            else:
                u = s.get("unitsEaten")
                s["keptLivingJ"] = None
                s["keptJ"] = u * self.jpu * keep if u is not None else None
        self.years = list(range(1, int(self.end // self.year + 1e-9) + 1))

    # ---- helpers
    def bits(self, i):
        b = self.w.born.get(i)
        return b[2] if b else 0

    def mouthed(self, i):
        return bool(self.bits(i) & r59.MOUTHED)

    def life(self, i):
        """(birth t, end of life t, died) clipped to --end."""
        bt = self.w.born[i][0]
        d = self.w.deaths.get(i)
        if d and d[0] <= self.end + 1e-9:
            return bt, d[0], True
        return bt, self.end, False

    def origin(self, i):
        """A mouthed body's origin."""
        w = self.w
        _, p, _ = w.born[i]
        if i in w.planted_roots:
            return "planted founder"
        if p is not None and p >= 0 and p in w.born:
            if self.mouthed(p):
                return "bred, planted line" if w.root.get(i) in w.planted_roots else "bred, other line"
            return "bred of an unmouthed parent"
        return "other founder"

    def yr(self, t):
        return r59.year_of(t, self.year)

    def stat_at(self, t):
        best = None
        for s in self.stats:
            if s["t"] <= t + 1e-9:
                best = s
            else:
                break
        return best

    # ---- header
    def header(self):
        w = self.w
        chain = " <- ".join(f"{os.path.basename(os.path.dirname(d))}/{os.path.basename(d)}" for d, _, _ in reversed(w.segs))
        print(f"\n######## {w.name}: seed {w.seed}, {'PLANTED' if w.planted else 'control'}, configHash "
              f"{w.manifest.get('configHash')}, read to {self.end:g} s; {chain}")
        c = w.config
        print(f"   joulesPerUnit {self.jpu:g}, intakeWasteFraction {self.waste:g}, corpseDecayPerSecond "
              f"{r59.find(c, 'corpseDecayPerSecond')}, attackWattsPerUnit {r59.find(c, 'attackWattsPerUnit')}, "
              f"intakeWattsPerUnit {r59.find(c, 'intakeWattsPerUnit')}, workCostMultiplier {r59.find(c, 'workCostMultiplier')}, "
              f"ageHazardBaseSeconds {r59.find(c, 'ageHazardBaseSeconds')}, trickle {r59.find(c, 'foundingTricklePerSecond')}/s, "
              f"pool share {r59.find(c, 'foundingTricklePoolShare')}")
        if w.pool:
            print("   pool: " + ", ".join(f"{i}:{w.pool_label(i)}" for i in sorted(w.pool)))
        for n in w.notes:
            print(f"   note: {n}")

    # ---- 1 births
    def births(self):
        w, Y = self.w, self.year
        origins = ("planted founder", "bred, planted line", "bred, other line", "bred of an unmouthed parent", "other founder")
        per = defaultdict(Counter)
        nopho = Counter()
        bred_jnt = Counter()
        mouthed_ids = [i for i in w.born if self.mouthed(i) and w.born[i][0] <= self.end + 1e-9]
        for i in mouthed_ids:
            y = self.yr(w.born[i][0])
            per[y][self.origin(i)] += 1
            nopho[y] += bool(self.bits(i) & r59.GRAZER)
            bred_jnt[y] += bool(w.born[i][1] is not None and w.born[i][1] >= 0 and self.bits(i) & r59.JOINTED)
        deaths = Counter(self.yr(w.deaths[i][0]) for i in mouthed_ids if i in w.deaths and w.deaths[i][0] <= self.end + 1e-9)
        pool_adm = {}
        prev = 0
        for y in self.years:
            s = self.stat_at(y * Y)
            v = (s or {}).get("poolSpawns") or 0
            pool_adm[y] = v - prev
            prev = v
        # living mouthed at each year's end, by origin, and those of a planted line whose founder came >= a year before
        print(f"\n== 1. mouthed births per year by origin; deaths of mouthed bodies; pool admissions (stats poolSpawns)")
        print(f"   {'yr':>3} {'pl.fnd':>6} {'bred-pl':>7} {'bred-oth':>8} {'bred-unm':>8} {'oth.fnd':>7} {'total':>6} {'nopho':>6} "
              f"{'bred jnt':>8} {'deaths':>6} {'pool adm':>8} | living at year end: {'mouthed':>7} {'founders':>8} {'bred':>5} "
              f"{'old pl.line':>11} {'max gen':>7}")
        for y in self.years:
            T = y * Y
            alive = [i for i in mouthed_ids if w.born[i][0] <= T + 1e-9 and not (i in w.deaths and w.deaths[i][0] <= T + 1e-9)]
            fnd = sum(1 for i in alive if w.born[i][1] is None or w.born[i][1] < 0)
            old = sum(1 for i in alive if w.root.get(i) in w.planted_roots
                      and T - w.born[w.root[i]][0] >= Y - 1e-9)
            mg = max((self.gen.get(i, 0) for i in alive), default=None)
            c = per[y]
            print(f"   {y:>3} {c['planted founder']:>6} {c['bred, planted line']:>7} {c['bred, other line']:>8} "
                  f"{c['bred of an unmouthed parent']:>8} {c['other founder']:>7} {sum(c.values()):>6} {nopho[y]:>6} "
                  f"{bred_jnt[y]:>8} {deaths[y]:>6} {pool_adm[y]:>8} | {len(alive):>27} {fnd:>8} {len(alive) - fnd:>5} {old:>11} {fmt(mg, '{}'):>7}")
        tot = Counter()
        for y in per:
            tot.update(per[y])
        print("   all years: " + ", ".join(f"{k} {tot[k]}" for k in origins) + f"; total {sum(tot.values())}")

        # replacement per birth-year cohort
        kids = Counter()
        mkids = Counter()
        for i, (t, p, b) in w.born.items():
            if t > self.end + 1e-9 or p is None or p < 0:
                continue
            kids[p] += 1
            if b & r59.MOUTHED:
                mkids[p] += 1
        print("\n   replacement per birth-year cohort of mouthed bodies (children counted to the end; late cohorts censored):")
        print(f"   {'yr':>3} | {'founders n':>10} {'kids/body':>9} {'mouthed kids/body':>17} {'with a kid':>10} | "
              f"{'bred n':>6} {'kids/body':>9} {'mouthed kids/body':>17} {'with a kid':>10}")
        for y in self.years:
            cells = []
            for fnd in (True, False):
                ids = [i for i in mouthed_ids if self.yr(w.born[i][0]) == y
                       and ((w.born[i][1] is None or w.born[i][1] < 0) == fnd)]
                n = len(ids)
                cells.append(f"{n:>10} {fmt(mean([kids[i] for i in ids]), '{:.3f}'):>9} "
                             f"{fmt(mean([mkids[i] for i in ids]), '{:.3f}'):>17} "
                             f"{fmt(sum(1 for i in ids if kids[i]) / n if n else None, '{:.3f}'):>10}")
            print(f"   {y:>3} | {cells[0]} | {cells[1]}")
        allm = [i for i in mouthed_ids]
        print(f"   all mouthed bodies: {len(allm)}, mean children {fmt(mean([kids[i] for i in allm]), '{:.3f}')}, "
              f"mean mouthed children {fmt(mean([mkids[i] for i in allm]), '{:.3f}')}")

        # planted lines
        if w.planted_roots:
            lines = defaultdict(lambda: {"births": 0, "mouthed": 0, "jnt": 0, "gen": 0})
            for i, (t, p, b) in w.born.items():
                r = w.root.get(i)
                if r in w.planted_roots and i != r and t <= self.end + 1e-9:
                    L = lines[r]
                    L["births"] += 1
                    L["mouthed"] += bool(b & r59.MOUTHED)
                    L["jnt"] += bool(b & r59.JOINTED)
                    L["gen"] = max(L["gen"], self.gen.get(i, 0))
            # each line's span: founder birth to the last death or birth of a member (or the end, if alive)
            span_end = defaultdict(float)
            for i, (t, p, b) in w.born.items():
                r = w.root.get(i)
                if r in w.planted_roots:
                    _, e, _ = self.life(i)
                    span_end[r] = max(span_end[r], e)
            n = len(w.planted_roots)
            with_kid = sum(1 for r in w.planted_roots if lines[r]["births"] > 0)
            alive_end = {r: w.alive_in_end.get(r, 0) for r in w.planted_roots}
            spans = sorted(((span_end[r] - w.born[r][0], r) for r in w.planted_roots), reverse=True)
            print(f"\n   planted lines: {n} founders admitted to {self.end:g} s; {with_kid} with any descendant; "
                  f"descendant births {sum(lines[r]['births'] for r in w.planted_roots)} "
                  f"(mouthed {sum(lines[r]['mouthed'] for r in w.planted_roots)}); lines alive at {self.end:g} s: "
                  f"{sum(1 for v in alive_end.values() if v)} holding {sum(alive_end.values())} bodies")
            top = sorted(w.planted_roots, key=lambda r: -lines[r]["births"])[:5]
            print("   largest planted lines (founder id, admitted s, swimmer, descendant births, mouthed, jointed, deepest gen, "
                  "span s, living at end): " + "; ".join(
                      f"{r} {w.born[r][0]:g} {w.rootinfo[r][3]} {lines[r]['births']} {lines[r]['mouthed']} {lines[r]['jnt']} "
                      f"{lines[r]['gen']} {span_end[r] - w.born[r][0]:.0f} {alive_end[r]}" for r in top))
            print("   longest-lived planted lines (span s, founder id, admitted s, descendant births, deepest gen, living): "
                  + "; ".join(f"{s:.0f} {r} {w.born[r][0]:g} {lines[r]['births']} {lines[r]['gen']} {alive_end[r]}"
                              for s, r in spans[:5]))
            # per swimmer genome, for the ledger's pick
            per_sw = defaultdict(lambda: [0, 0, 0, 0])
            for r in w.planted_roots:
                k = w.rootinfo[r][2]
                v = per_sw[k]
                v[0] += 1
                v[1] += lines[r]["births"]
                v[2] += lines[r]["mouthed"]
                v[3] += alive_end[r]
            print("   per swimmer pool genome (pool index: name, founders, descendant births, mouthed descendants, living at end): "
                  + "; ".join(f"{k}: {w.swimmer_pool.get(k)} {v[0]} {v[1]} {v[2]} {v[3]}"
                              for k, v in sorted(per_sw.items(), key=lambda kv: -kv[1][1])))

    # ---- 2 lifespans and deaths
    def deaths(self):
        w, Y = self.w, self.year
        rows = defaultdict(lambda: {"ages": [], "c": Counter()})
        cens = defaultdict(list)
        for i, (t, p, b) in w.born.items():
            if t > self.end + 1e-9:
                continue
            g = guild(b)
            keys = [g]
            if b & r59.MOUTHED:
                keys.append("  mouthed: planted founder" if i in w.planted_roots else
                            ("  mouthed: other founder" if (p is None or p < 0) else "  mouthed: bred"))
            d = w.deaths.get(i)
            if d and d[0] <= self.end + 1e-9:
                y = self.yr(d[0])
                for k in keys:
                    rows[(k, y)]["ages"].append(d[0] - t)
                    rows[(k, y)]["c"][d[1]] += 1
                    rows[(k, 0)]["ages"].append(d[0] - t)
                    rows[(k, 0)]["c"][d[1]] += 1
            else:
                for k in keys:
                    cens[k].append(self.end - t)
        keys = list(GUILD_ORDER) + ["  mouthed: planted founder", "  mouthed: bred", "  mouthed: other founder"]
        print(f"\n== 2. age at death (s) and cause, per guild and year of death (year 0 = all years)")
        print(f"   {'guild':<28} {'yr':>3} {'deaths':>7} {'median':>7} {'mean':>7} " + " ".join(f"{c:>9}" for c in CAUSES) + "  other")
        for k in keys:
            for y in [0] + self.years:
                r = rows.get((k, y))
                if not r or not r["ages"]:
                    continue
                n = len(r["ages"])
                other = sum(v for c, v in r["c"].items() if c not in CAUSES)
                print(f"   {k:<28} {y:>3} {n:>7} {fmt(med(r['ages']), '{:.0f}'):>7} {fmt(mean(r['ages']), '{:.0f}'):>7} "
                      + " ".join(f"{r['c'][c] / n:>9.3f}" for c in CAUSES) + f"  {other}")
        print("   alive at the end (censored): " + "; ".join(f"{k.strip()} {len(cens[k])} (median age {fmt(med(cens[k]), '{:.0f}')} s)"
                                                       for k in keys if cens.get(k)))

    # ---- 3 energy
    def energy(self):
        w, Y = self.w, self.year
        keep = 1.0 - self.waste
        kills_by = defaultdict(list)
        for t, vic, by, j in self.kill_j:
            if by is not None:
                kills_by[by].append((t, vic, j))
        mouthed_ids = [i for i in w.born if self.mouthed(i) and w.born[i][0] <= self.end + 1e-9]
        U = self.a.upkeep_w
        print(f"\n== 3. energy per mouthed body: kills it made, the corpse joules they founded kept at 1 - waste "
              f"({keep:g}; an upper bound on what its kills fed it), against upkeep "
              + (f"{U:g} W (--upkeep-w) times its life" if U else "(no --upkeep-w given)"))
        print("   per birth-year cohort and origin; a body alive at the end has its life to the end")
        print(f"   {'yr':>3} {'origin':<12} {'n':>5} {'>=1 kill':>8} {'>=1 leaf':>8} {'kills mean':>10} {'median':>6} "
              f"{'leafK mean':>10} {'median':>6} {'life mean s':>11} {'kept W':>7} {'kept J/kill':>11} "
              f"{'endow J':>7} {'covers upkeep':>13}")
        groups = defaultdict(list)
        for i in mouthed_ids:
            y = self.yr(w.born[i][0])
            fnd = w.born[i][1] is None or w.born[i][1] < 0
            o = "planted fnd" if i in w.planted_roots else ("other fnd" if fnd else "bred")
            groups[(y, o)].append(i)
            groups[(0, o)].append(i)
            groups[(0, "all")].append(i)
        for y in [0] + self.years:
            for o in ("planted fnd", "bred", "other fnd", "all"):
                ids = groups.get((y, o))
                if not ids:
                    continue
                n = len(ids)
                ks = [len(kills_by.get(i, ())) for i in ids]
                lk = [sum(1 for _, v, _ in kills_by.get(i, ()) if w.is_leaf(v)) for i in ids]
                lives = []
                kept = []
                for i in ids:
                    bt, et, _ = self.life(i)
                    lives.append(et - bt)
                    kept.append(keep * sum(j for _, _, j in kills_by.get(i, ())))
                life_tot = sum(lives)
                kw = sum(kept) / life_tot if life_tot > 0 else None
                per_kill = sum(kept) / sum(ks) if sum(ks) else None
                en = [self.endow[i] for i in ids if i in self.endow]
                cov = (sum(1 for k, L in zip(kept, lives) if k >= U * L) / n) if U else None
                print(f"   {y if y else 'all':>3} {o:<12} {n:>5} {sum(1 for k in ks if k) / n:>8.3f} {sum(1 for k in lk if k) / n:>8.3f} "
                      f"{mean(ks):>10.2f} {fmt(med(ks), '{:g}'):>6} {mean(lk):>10.2f} {fmt(med(lk), '{:g}'):>6} "
                      f"{mean(lives):>11.0f} {fmt(kw, '{:.3f}'):>7} {fmt(per_kill, '{:.1f}'):>11} "
                      f"{fmt(mean(en), '{:.0f}'):>7} {fmt(cov, '{:.3f}'):>13}")
        # victims
        vic = Counter()
        for t, v, by, j in self.kill_j:
            if by is not None and self.mouthed(by):
                vic[guild(self.bits(v)) if v in w.born else "unknown"] += 1
        lj = [j for t, v, by, j in self.kill_j if by is not None and self.mouthed(by) and w.is_leaf(v)]
        print(f"   kills by mouthed attackers by victim guild: " + ", ".join(f"{k} {n}" for k, n in vic.most_common())
              + f"; a leaf kill's corpse: mean {fmt(mean(lj), '{:.1f}')} J, median {fmt(med(lj), '{:.1f}')} J (tj + rj, before waste)")

        # world level, per year
        print(f"\n   world level per year (measured): mouths' intake from stats unitsEaten x {self.jpu:g} J, kept at {keep:g}; "
              f"corpse J founded by kills of mouthed attackers; body-seconds with intake (ink 1) and mouthed; mean kept W per "
              f"intake body; work billed per jointed body (stats workJoulesPerSecond x workCostMultiplier / jointed, mean over the "
              f"year's rows; every jointed body, not only the mouthed)")
        print(f"   {'yr':>3} {'kills':>6} {'leafK':>6} {'kill corpse J':>13} {'kept J':>9} {'kept/corpse':>11} {'ink body-s':>11} "
              f"{'mouthed body-s':>14} {'kept W/ink':>10} {'mean ink alive':>14} {'work W/jnt':>10} {'corpsesEaten':>12} "
              f"{'corpsesFromKills':>16}")
        ink_s = Counter()
        m_s = Counter()
        for i in self.ink | set(mouthed_ids):
            if i not in w.born or w.born[i][0] > self.end + 1e-9:
                continue
            bt, et, _ = self.life(i)
            for y in self.years:
                lo, hi = (y - 1) * Y, y * Y
                o = max(0.0, min(hi, et) - max(lo, bt))
                if o > 0:
                    if i in self.ink:
                        ink_s[y] += o
                    if self.mouthed(i):
                        m_s[y] += o
        prev = {}
        wcm = float(r59.find(w.config, "workCostMultiplier") or 1.0)
        for y in self.years:
            s = self.stat_at(y * Y) or {}
            d = {k: (s.get(k) or 0) - (prev.get(k) or 0) for k in ("unitsEaten", "corpsesEaten", "corpsesFromKills")}
            prev = s
            ks = [(v, j) for t, v, by, j in self.kill_j if self.yr(t) == y and by is not None and self.mouthed(by)]
            cj = sum(j for _, j in ks)
            kept_j = d["unitsEaten"] * self.jpu * keep
            wr = [(r.get("workJoulesPerSecond") or 0.0) * wcm / r["jointed"] for r in self.stats
                  if (y - 1) * Y < r["t"] <= y * Y + 1e-9 and r.get("jointed")]
            wj = mean(wr)
            print(f"   {y:>3} {len(ks):>6} {sum(1 for v, _ in ks if w.is_leaf(v)):>6} {cj:>13.0f} {kept_j:>9.0f} "
                  f"{fmt(kept_j / cj if cj else None, '{:.2f}'):>11} {ink_s[y]:>11.0f} {m_s[y]:>14.0f} "
                  f"{fmt(kept_j / ink_s[y] if ink_s[y] else None, '{:.3f}'):>10} {ink_s[y] / Y:>14.1f} {fmt(wj, '{:.4f}'):>10} "
                  f"{d['corpsesEaten']:>12} {d['corpsesFromKills']:>16}")

    # ---- 5 swimming
    def speed(self, windows):
        w = self.w
        planted = w.planted_roots
        acc = {}
        for lo, hi in windows:
            acc[(lo, hi)] = {"prev": None, "first": None, "last": None,
                             "sp": defaultdict(list), "hs": defaultdict(list), "rel": defaultdict(list)}

        def cls(i, flags):
            out = []
            if flags & JOINTED_FLAG:
                out.append("jointed, planted line" if w.root.get(i) in planted else "jointed, other")
            else:
                out.append("rigid")
                if flags == PHOTO_FLAG:
                    out.append("rigid leaf")
            return out

        def keep(t):
            return any(lo - 1e-9 <= t <= hi + 1e-9 for lo, hi in windows)

        for t, line in w.seg_positions(keep):
            row = json.loads(line)
            cur = {b[0]: (b[1], b[2], b[3], b[4]) for b in row["b"]}
            for (lo, hi), A in acc.items():
                if not (lo - 1e-9 <= t <= hi + 1e-9):
                    continue
                if A["first"] is None:
                    A["first"] = (t, cur)
                A["last"] = (t, cur)
                if A["prev"] is not None:
                    pt, prow = A["prev"]
                    dt = t - pt
                    if 0 < dt <= 10.0 + 1e-6:
                        grid = defaultdict(list)  # rigid bodies' displacements, by DRIFT_CELL cubes
                        for i, (x, y, z, f) in cur.items():
                            q = prow.get(i)
                            if q is None:
                                continue
                            dx, dy, dz = x - q[0], y - q[1], z - q[2]
                            s3 = math.sqrt(dx * dx + dy * dy + dz * dz) / dt
                            sh = math.sqrt(dx * dx + dz * dz) / dt
                            for c in cls(i, f):
                                A["sp"][c].append(s3)
                                A["hs"][c].append(sh)
                            if not f & JOINTED_FLAG:
                                grid[(int(x // DRIFT_CELL), int(y // DRIFT_CELL), int(z // DRIFT_CELL))].append(
                                    (i, x, y, z, dx, dy, dz))
                        # Relative to the local drift: a body's displacement less the mean displacement of the other
                        # rigid bodies within DRIFT_CELL metres of it (at least DRIFT_MIN of them), as a speed. Every
                        # planted-line jointed body; one rigid body in RIGID_EVERY as the baseline.
                        for i, (x, y, z, f) in cur.items():
                            q = prow.get(i)
                            if q is None:
                                continue
                            if f & JOINTED_FLAG:
                                c = "jointed, planted line" if w.root.get(i) in planted else None
                            else:
                                c = "rigid" if i % RIGID_EVERY == 0 else None
                            if c is None:
                                continue
                            gx, gy, gz = int(x // DRIFT_CELL), int(y // DRIFT_CELL), int(z // DRIFT_CELL)
                            n, mx, my, mz = 0, 0.0, 0.0, 0.0
                            for u in (gx - 1, gx, gx + 1):
                                for v in (gy - 1, gy, gy + 1):
                                    for k in (gz - 1, gz, gz + 1):
                                        for (j, px, py, pz, ex, ey, ez) in grid.get((u, v, k), ()):
                                            if j != i and (px - x) ** 2 + (py - y) ** 2 + (pz - z) ** 2 <= DRIFT_CELL ** 2:
                                                n += 1
                                                mx += ex
                                                my += ey
                                                mz += ez
                            if n >= DRIFT_MIN:
                                dx, dy, dz = x - q[0] - mx / n, y - q[1] - my / n, z - q[2] - mz / n
                                A["rel"][c].append(math.sqrt(dx * dx + dy * dy + dz * dz) / dt)
                A["prev"] = (t, cur)
        print(f"\n== 5. speed from positions (m/s): per 10 s step, 3-D and horizontal, over body-samples; net = horizontal "
              f"displacement over the window per body present at both ends, over the span")
        classes = ("jointed, planted line", "jointed, other", "rigid", "rigid leaf")
        print(f"   {'window':>13} {'class':<22} {'samples':>8} {'mean 3D':>8} {'med 3D':>7} {'mean h':>7} {'med h':>7} "
              f"{'bodies':>6} {'net med':>7} {'net mean':>8} {'rel n':>7} {'rel med':>7} {'rel mean':>8} "
              f"| stats speedJointed / speedRigid")
        for (lo, hi), A in acc.items():
            s = self.stat_at(hi) or {}
            st = f"{fmt(s.get('speedJointed'), '{:.3f}')} / {fmt(s.get('speedRigid'), '{:.3f}')} (row {fmt(s.get('t'), '{:g}')})"
            if A["first"] is None:
                print(f"   {lo:>6g}:{hi:<6g} no positions rows")
                continue
            (t0, r0), (t1, r1) = A["first"], A["last"]
            net = defaultdict(list)
            if t1 > t0:
                for i, (x, y, z, f) in r1.items():
                    q = r0.get(i)
                    if q is None:
                        continue
                    v = math.hypot(x - q[0], z - q[2]) / (t1 - t0)
                    for c in cls(i, f):
                        net[c].append(v)
            for c in classes:
                sp, hs = A["sp"].get(c, []), A["hs"].get(c, [])
                if not sp:
                    continue
                print(f"   {lo:>6g}:{hi:<6g} {c:<22} {len(sp):>8} {mean(sp):>8.4f} {med(sp):>7.4f} {mean(hs):>7.4f} "
                      f"{med(hs):>7.4f} {len(net[c]):>6} {fmt(med(net[c]), '{:.4f}'):>7} {fmt(mean(net[c]), '{:.4f}'):>8} "
                      f"{len(A['rel'].get(c, [])):>7} {fmt(med(A['rel'].get(c, [])), '{:.4f}'):>7} "
                      f"{fmt(mean(A['rel'].get(c, [])), '{:.4f}'):>8} | {st}")
                st = ""


def parse_windows(text, year, end):
    if text:
        out = []
        for part in text.split(","):
            lo, hi = part.split(":")
            out.append((float(lo), float(hi)))
        return out
    out = [(y * year - 300.0, y * year) for y in range(1, int(end // year + 1e-9) + 1)]
    out.append((23000.0, 28000.0))
    return sorted(out)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=72000.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--upkeep-w", type=float, default=None,
                    help="a planted founder's standing cost, watts, from scripts/ledger.ps1")
    ap.add_argument("--windows", default=None, help="speed windows lo:hi,lo:hi seconds")
    ap.add_argument("--no-speed", action="store_true")
    a = ap.parse_args()
    a.every = a.year  # World's samples: the year ends only
    r59.runrec.VERBOSE = False
    print(f"== round 61's grazer economy: year {a.year:g} s, end {a.end:g} s, upkeep {a.upkeep_w} W")
    for d in a.runs:
        e = Economy(d, a)
        e.header()
        e.births()
        e.deaths()
        e.energy()
        if not a.no_speed:
            e.speed(parse_windows(a.windows, a.year, e.end))
        sys.stdout.flush()


if __name__ == "__main__":
    main()
