"""The reader for round 62's grazer dial screens (D201): a screen resumes one of round 61's planted worlds from its
30,000 s checkpoint with one economy dial changed and the trickle of planted founders off, and runs 6,000 s, to
36,000 s. Beside each screen an unchanged cousin resumes the same checkpoint with only the trickle off. A screen
overshoots when its living leaves at the end fall below half the unchanged cousin's.

Usage: python -I r62-dial-screens.py --pair SCREEN_RUN COUSIN_RUN [--pair ...] [--start 30000] [--end 36000]
                                      [--every 1500] [--year 6000] [--line-age 6000]

A RUN is a run directory or an arm folder, of which the latest dated run is read. A run whose run.json carries
resumedFrom is read as one world with the runs it resumed, back to 0 s, by round 59's World
(scripts/reads/r59-read.py), as scripts/reads/r61-read.py and r61-grazer-economy.py read it; the chain before the
resume is read for parentage and for who was alive at --start, and every count below is of the window
--start < t <= --end of the resumed run itself. The definitions are those readers', reused by import: a body's guild
bits from its birth row (a leaf has pho 1, jnt 0 and abs 0; a mouthed body atk 1 and ink 1; a nopho grazer a mouthed
body with pho 0), a kill row ("e":"k") has its victim in id and its attacker in by, a leaf kill is one whose victim
was born a leaf, and a kill moves the victim's tissue and reserve (tj + rj) into a corpse. A grazer is the
pre-registration's: a body born mouthed. The nopho grazers are counted beside it.

Printed per run: its config hash (run.json's configHash), any further hash in run.json whose key names a config hash
(the farm records the source's beside the run's own; found by key name, wherever it sits), the resumed-from run's
configHash, and the dial values that differ from the source's config.json (the two files compared key by key; a
resumed run with nothing changed prints none). Per pair also the screen's config against its cousin's.

Per pair, one table: for --start, each --every point after it and --end, one row per run (S the screen, C its
cousin). The interval columns cover (previous point, this point] and are blank on the --start row.
  leaves   living leaves (born pho 1, jnt 0, abs 0) at the point
  L/C      the screen's leaves over the cousin's, on the screen's row; OVERSHOOT is printed at --end when under 0.5
  gz       living grazers (born mouthed)
  gz>st    of them, born after --start (bred in the screen: with the trickle off every new grazer is bred)
  gz>st.br the same, counting only bodies whose birth row names a parent (differs from gz>st only when a founder
           arrived after --start, as in a run with the trickle on)
  old      of the gz>st.br, those whose line's root was born at least --line-age s before the point (a line a year old)
  np       living nopho grazers (mouthed, pho 0); np>st of them born after --start
  m+pho    living mouthed bodies that also photosynthesise (mouthed with pho 1; gz minus np)
  born     grazers born in the interval
  died     grazer deaths in the interval; st/ea/ag/dv their causes (starved/eaten/aged/diverged; other causes are
           added to dv's neighbour column as "+n")
  kills    kills made by grazers in the interval; leafK of them with a leaf victim
  corpseJ  the corpse joules those kills made (tj + rj), before intake waste
  keptJ    the world's joules kept by mouths: stats unitsEaten differenced over the interval times joulesPerUnit
           times 1 - intakeWasteFraction (every mouth's intake in the world, not only those kills')
  k/c      keptJ over corpseJ (the share of what grazers' kills founded that mouths kept; it is above 1 when mouths
           also eat corpses that other things made)
Then per pair an end line: grazers alive at --end and bred after --start, the screen against its cousin.

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import bisect
import importlib.util
import json
import math
import os
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
_spec = importlib.util.spec_from_file_location("r59read", os.path.join(HERE, "r59-read.py"))
r59 = importlib.util.module_from_spec(_spec)
sys.modules["r59read"] = r59
_spec.loader.exec_module(r59)

SWIMMERS = os.path.join(ROOT, "inocula", "r59-direction-tops")
CAUSES = ("starved", "eaten", "aged", "diverged")
OVERSHOOT_BELOW = 0.5
MAX_DIFF_LINES = 40


def fmt(x, f="{:.0f}"):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else f.format(x)


def flatten(x, p=""):
    """A parsed json value as {dotted path: leaf}; lists by index."""
    out = {}
    if isinstance(x, dict):
        for k, v in x.items():
            out.update(flatten(v, f"{p}.{k}" if p else k))
    elif isinstance(x, list):
        for i, v in enumerate(x):
            out.update(flatten(v, f"{p}[{i}]"))
        if not x:
            out[p] = []
    else:
        out[p] = x
    return out


def config_diff(a, b):
    """[(key, a's value, b's value)] for every key whose value differs or is missing on one side."""
    fa, fb = flatten(a), flatten(b)
    miss = "<absent>"
    return [(k, fa.get(k, miss), fb.get(k, miss)) for k in sorted(set(fa) | set(fb))
            if k not in fa or k not in fb or not r59.close(fa[k], fb[k])]


def hash_keys(x, p=""):
    """Every (path, value) in run.json whose key names a config hash, other than the top-level configHash."""
    out = []
    if isinstance(x, dict):
        for k, v in x.items():
            q = f"{p}.{k}" if p else k
            kl = k.lower()
            if "hash" in kl and "config" in kl and q != "configHash":
                out.append((q, v))
            elif isinstance(v, dict):
                out += hash_keys(v, q)
    return out


def window_rows(w, name, lo, hi):
    """The chain's rows of a file with lo < t <= hi, reading only the segments that can hold them."""
    for d, slo, shi in w.segs:
        if shi is not None and shi <= lo:
            continue
        for r in r59.rows_of(os.path.join(d, name)):
            t = r.get("t")
            if t is None:
                continue
            if slo is not None and t <= slo:
                continue
            if shi is not None and t > shi:
                continue
            if t <= lo:
                continue
            if t > hi:
                return
            yield r


class Screen:
    """One run read as a world, with the window's counts at the points."""

    def __init__(self, path, a, role, points):
        ns = argparse.Namespace(year=a.year, every=a.every, end=a.end)
        self.a, self.role, self.pts = a, role, points
        self.w = w = r59.World(path, ns, r59.Swimmers(SWIMMERS))
        self.end = w.end
        self.name = w.name
        cfg = w.config
        self.jpu = float(r59.find(cfg, "joulesPerUnit") or 100.0)
        self.waste = float(r59.find(cfg, "intakeWasteFraction") or 0.0)
        self.config_diff_source = None
        self.source_dir = None
        if len(w.segs) > 1:
            self.source_dir = w.segs[-2][0]
            sp = os.path.join(self.source_dir, "config.json")
            if os.path.exists(sp):
                self.config_diff_source = config_diff(r59.load_json(sp), cfg)
        self.reached = [t for t in points if t <= w.end + 1e-9]
        self._count()
        self._kills()

    def _count(self):
        w, pts, a = self.w, self.pts, self.a
        n = len(pts)
        C = [Counter() for _ in range(n)]
        self.born_n = [0] * n
        self.died_n = [0] * n
        self.cause = [Counter() for _ in range(n)]
        start = pts[0]
        for i, (bt, p, bits) in self.w.born.items():
            if bt > pts[-1] + 1e-9:
                continue
            leaf = bool(bits & r59.LEAF)
            gz = bool(bits & r59.MOUTHED)
            if not (leaf or gz):
                continue
            np_ = bool(bits & r59.GRAZER)
            new = bt > start + 1e-9
            br = new and p is not None and p >= 0
            root_t = w.born[w.root[i]][0] if i in w.root and w.root[i] in w.born else bt
            d = w.deaths.get(i)
            dt = d[0] if d and d[0] <= pts[-1] + 1e-9 else math.inf
            if gz and bt > start + 1e-9:
                k = bisect.bisect_left(pts, bt)
                if 1 <= k < n:
                    self.born_n[k] += 1
            if gz and dt != math.inf and dt > start + 1e-9:
                k = bisect.bisect_left(pts, dt)
                if 1 <= k < n:
                    self.died_n[k] += 1
                    self.cause[k][d[1]] += 1
            lo, hi = bisect.bisect_left(pts, bt), bisect.bisect_left(pts, dt)
            for j in range(lo, min(hi, n)):
                c = C[j]
                if leaf:
                    c["leaves"] += 1
                if gz:
                    c["gz"] += 1
                    if new:
                        c["gz_new"] += 1
                    if br:
                        c["gz_br"] += 1
                        if pts[j] - root_t >= a.line_age - 1e-9:
                            c["old"] += 1
                    if np_:
                        c["np"] += 1
                        if new:
                            c["np_new"] += 1
                    else:
                        c["mpho"] += 1
        self.C = C

    def _kills(self):
        w, pts = self.w, self.pts
        n = len(pts)
        self.kills = [0] * n
        self.leafk = [0] * n
        self.corpse = [0.0] * n
        for r in window_rows(w, "lineage.jsonl", pts[0], pts[-1]):
            if r.get("e") != "k":
                continue
            by = r.get("by")
            b = w.born.get(by) if by is not None else None
            if b is None or not b[2] & r59.MOUTHED:
                continue
            k = bisect.bisect_left(pts, r["t"])
            if not 1 <= k < n:
                continue
            self.kills[k] += 1
            if w.is_leaf(r["id"]):
                self.leafk[k] += 1
            self.corpse[k] += (r.get("tj") or 0.0) + (r.get("rj") or 0.0)

    def units(self, t):
        s = self.w.stat_at(t)
        return None if s is None or s.get("unitsEaten") is None else s["unitsEaten"]

    def kept(self, k):
        u0, u1 = self.units(self.pts[k - 1]), self.units(self.pts[k])
        if u0 is None or u1 is None:
            return None
        return (u1 - u0) * self.jpu * (1.0 - self.waste)

    def has(self, k):
        return self.pts[k] <= self.end + 1e-9

    def header(self):
        w, m = self.w, self.w.manifest
        chain = " <- ".join(f"{os.path.basename(os.path.dirname(d))}/{os.path.basename(d)}" for d, _, _ in reversed(w.segs))
        print(f"\n---- {self.role} {self.name}: seed {w.seed}, {'planted pool' if w.planted else 'no planted pool'}, read to "
              f"{self.end:g} s; {chain}")
        print(f"     configHash {m.get('configHash')} (config.json {w.config.get('configHash')})")
        for k, v in hash_keys(m):
            print(f"     run.json {k}: {v}")
        rf = m.get("resumedFrom")
        if rf:
            print(f"     resumedFrom {rf.get('arm')}/{rf.get('run')} at {rf.get('seconds')} s, checkpointHash "
                  f"{str(rf.get('checkpointHash'))[:16]}, sourceMismatch {rf.get('sourceMismatch')}")
            if self.source_dir:
                try:
                    sm = r59.load_json(os.path.join(self.source_dir, "run.json"))
                    print(f"     source run's configHash {sm.get('configHash')}")
                except (OSError, ValueError):
                    print("     source run's run.json not readable")
        else:
            print("     not a resumed run (no resumedFrom)")
        print(f"     joulesPerUnit {self.jpu:g}, intakeWasteFraction {self.waste:g}, trickle "
              f"{r59.find(w.config, 'foundingTricklePerSecond')}/s, pool share {r59.find(w.config, 'foundingTricklePoolShare')}")
        d = self.config_diff_source
        if d is None:
            print("     dials differing from the source's config.json: not compared (no source run)")
        elif not d:
            print("     dials differing from the source's config.json: none")
        else:
            print(f"     dials differing from the source's config.json ({len(d)}): key: source -> this run")
            for k, x, y in d[:MAX_DIFF_LINES]:
                print(f"       {k}: {x} -> {y}")
            if len(d) > MAX_DIFF_LINES:
                print(f"       ... {len(d) - MAX_DIFF_LINES} more")
        for n in w.notes:
            print(f"     note: {n}")


COLS = (f"{'t':>6} {'':1} {'leaves':>6} {'L/C':>5} | {'gz':>5} {'gz>st':>5} {'gz>st.br':>8} {'old':>4} {'np':>5} {'np>st':>5} "
        f"{'m+pho':>5} | {'born':>5} {'died':>5} {'st/ea/ag/dv':>13} | {'kills':>6} {'leafK':>6} {'corpseJ':>8} {'keptJ':>8} {'k/c':>5}")


def row(s, k, other, tag):
    t = s.pts[k]
    if not s.has(k):
        return f"{t:>6g} {tag} (not reached: run ends at {s.end:g} s)"
    c = s.C[k]
    ratio = None
    if tag == "S" and other.has(k) and other.C[k]["leaves"]:
        ratio = c["leaves"] / other.C[k]["leaves"]
    head = f"{t:>6g} {tag} {c['leaves']:>6} {fmt(ratio, '{:.3f}'):>5} | {c['gz']:>5} {c['gz_new']:>5} {c['gz_br']:>8} " \
           f"{c['old']:>4} {c['np']:>5} {c['np_new']:>5} {c['mpho']:>5} |"
    if k == 0:
        return head
    cs = s.cause[k]
    other_c = sum(v for x, v in cs.items() if x not in CAUSES)
    causes = "/".join(str(cs[x]) for x in CAUSES) + (f"+{other_c}" if other_c else "")
    kept = s.kept(k)
    kc = kept / s.corpse[k] if kept is not None and s.corpse[k] > 0 else None
    return (f"{head} {s.born_n[k]:>5} {s.died_n[k]:>5} {causes:>13} | {s.kills[k]:>6} {s.leafk[k]:>6} {s.corpse[k]:>8.0f} "
            f"{fmt(kept):>8} {fmt(kc, '{:.2f}'):>5}")


def read_pair(sp, cp, a):
    pts = [a.start]
    k = 1
    while a.start + k * a.every < a.end - 1e-9:
        pts.append(a.start + k * a.every)
        k += 1
    pts.append(a.end)
    print(f"\n==== pair: screen {sp} | cousin {cp}")
    S = Screen(sp, a, "S (screen)", pts)
    Cz = Screen(cp, a, "C (cousin)", pts)
    S.header()
    Cz.header()
    dd = config_diff(Cz.w.config, S.w.config)
    print(f"\n     screen's config against its cousin's ({len(dd)} keys differ): key: cousin -> screen")
    for key, x, y in dd[:MAX_DIFF_LINES]:
        print(f"       {key}: {x} -> {y}")
    print(f"\n{COLS}")
    for k in range(len(pts)):
        print(row(S, k, Cz, "S"))
        print(row(Cz, k, S, "C"))
    last = len(pts) - 1
    if S.has(last) and Cz.has(last):
        ls, lc = S.C[last]["leaves"], Cz.C[last]["leaves"]
        ratio = ls / lc if lc else None
        flag = ("OVERSHOOT" if ratio is not None and ratio < OVERSHOOT_BELOW else "no overshoot") if ratio is not None else "no cousin leaves"
        print(f"\n   leaves at {a.end:g} s: screen {ls}, cousin {lc}, ratio {fmt(ratio, '{:.3f}')}: {flag} (below {OVERSHOOT_BELOW:g})")
    else:
        print(f"\n   leaves at {a.end:g} s: unread (screen reaches {S.end:g} s, cousin {Cz.end:g} s)")
    for who, x in (("screen", S), ("cousin", Cz)):
        if x.has(last):
            c = x.C[last]
            print(f"   END {who}: grazers alive at {a.end:g} s {c['gz']}, bred after {a.start:g} s {c['gz_br']} "
                  f"(born after {c['gz_new']}; from lines {a.line_age:g} s old or more {c['old']}); nopho {c['np']}, "
                  f"mouthed with pho {c['mpho']}")
    sys.stdout.flush()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pair", nargs=2, action="append", required=True, metavar=("SCREEN_RUN", "COUSIN_RUN"))
    ap.add_argument("--start", type=float, default=30000.0)
    ap.add_argument("--end", type=float, default=36000.0)
    ap.add_argument("--every", type=float, default=1500.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--line-age", type=float, default=6000.0,
                    help="a line is old when its root was born at least this many seconds before the point")
    a = ap.parse_args()
    r59.runrec.VERBOSE = False
    print(f"== round 62's grazer dial screens: window {a.start:g} < t <= {a.end:g} s, every {a.every:g} s")
    for sp, cp in a.pair:
        read_pair(sp, cp, a)


if __name__ == "__main__":
    main()
