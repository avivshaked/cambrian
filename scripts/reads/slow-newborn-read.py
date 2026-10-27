"""Why are the slow bred-grazer newborns slow, when their structure is unchanged from their parent's?

Population: bred grazers with a first speed and structure category count 0 in the child-against-parent genome diff. The
README of logbook/specs/r62p-read/newborn/ found about 999 of them, about 191 slow. This read splits that population by
five candidate causes and prints, for each, bands with n, slow count and slow share, and the excess slow over the safest
band's rate. Every cause printed here is an ASSOCIATION in recorded data, not a demonstrated cause.

Definitions are loaded, not copied, from scripts/reads/genome-diff-read.py: this file reads that script's source up to its
"groups" section (its argument parsing, lineage and genome loading, grazer(), bred, ate(), speed() and the diff) and runs it
with the same RUN, FRAMES, --end and --window, so what a grazer is, what bred means, what ate means, the first speed
(first pair of consecutive frames, at most 10 s apart, whose first frame comes within --window s of birth; straight-line
horizontal distance in x and z over the time between them), slow (under 0.03 m/s), the exclusions (bred grazers whose own
and whose parent's genome are not both in genomes.jsonl.gz; a living grazer born in the last 60 s before --end) and the
diff categories ("structure", "brain", ...) are that script's, unchanged. Its frames dict is then replaced with one that
also holds every parent's and grandparent's frames and each frame's bf, J and dleafh, so speed() runs on them with the same
rule. Nothing else of that script is altered. If its source ever lacks the "groups" marker the whole script runs with its
output discarded, which costs a second diff pass.

Frame table columns (newborn-extract.py): id t bf J S breed x y z dleaf3 dleafh flags. t is seconds, bf the body fraction
(1 is adult size), J the reserve in joules, x y z metres (y is up), dleaf3 and dleafh metres from the body's root to the
nearest living leaf's root in 3D and horizontally. Frames are 10 s apart on the world clock, not on the creature's age.

Usage: python -I scripts/reads/slow-newborn-read.py RUN FRAMES [--end 24000] [--window 10]
"""
import argparse
import bisect
import contextlib
import io
import math
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
GDR = os.path.join(HERE, "genome-diff-read.py")
MINN = 30          # a band needs this many members to serve as the reference ("safest") band
GROUP_MARK = "groups"

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("frames")
ap.add_argument("--end", type=float, default=24000.0)
ap.add_argument("--window", type=float, default=10.0)
A = ap.parse_args()

# ------------------------------------------------------------------ load genome-diff-read.py's definitions
src = open(GDR, encoding="utf-8").read()
cut = None
pos = 0
for line in src.splitlines(keepends=True):
    s = line.strip()
    if s.startswith("# ----") and s.endswith(GROUP_MARK):
        cut = pos
        break
    pos += len(line)
head = src if cut is None else src[:cut]
ns = {"__name__": "genome_diff_read", "__file__": GDR}
argv0 = sys.argv
sys.argv = [GDR, A.run, A.frames, "--end", repr(A.end), "--window", repr(A.window)]
try:
    if cut is None:
        with contextlib.redirect_stdout(io.StringIO()):
            exec(compile(src, GDR, "exec"), ns)
    else:
        exec(compile(head, GDR, "exec"), ns)
finally:
    sys.argv = argv0

births, deaths = ns["births"], ns["deaths"]
grazer, ate, diff_genome, speed_fn = ns["grazer"], ns["ate"], ns["diff_genome"], ns["speed"]
genomes = ns["genomes"]
bred = ns["bred"]                 # bred grazers with both genomes in the file
SLOW = ns["SLOW"]
END = A.end

bred0 = [i for i, b in births.items() if grazer(b) and b.get("p", -1) >= 0]   # bred, before the genome filter


def parent_of(i):
    b = births.get(i)
    return b.get("p", -1) if b else -1


# ------------------------------------------------------------------ a wider frame dict
ids = set(bred0)
for i in list(bred0):
    p = parent_of(i)
    if p >= 0:
        ids.add(p)
        g = parent_of(p)
        if g >= 0:
            ids.add(g)
frames = {}
with open(A.frames, encoding="utf-8") as f:
    hd = f.readline().rstrip("\n").split("\t")
    col = {n: k for k, n in enumerate(hd)}
    ci, ct, cb, cj, cx, cz, ch = (col[n] for n in ("id", "t", "bf", "J", "x", "z", "dleafh"))
    for line in f:
        p = line.rstrip("\n").split("\t")
        if len(p) < len(hd):
            continue
        try:
            i = int(p[ci])
        except ValueError:
            continue
        if i not in ids:
            continue
        try:
            frames.setdefault(i, []).append((float(p[ct]), float(p[cx]), float(p[cz]), float(p[cb]), float(p[cj]),
                                             float(p[ch])))
        except ValueError:
            pass
ns["frames"] = frames          # speed() reads this global
for v in frames.values():
    v.sort()
FT, FX, FZ, FBF, FJ, FDH = range(6)

_spd = {}


def spd(i):
    """The first speed of body i by genome-diff-read.py's rule, or None."""
    if i not in _spd:
        _spd[i] = speed_fn(i) if i in births else None
    return _spd[i]


def pair_speed(i, lo, hi):
    """Speed of the first consecutive frame pair (dt in (0, 10.01]) whose first frame is at age lo <= age < hi."""
    t0 = births[i]["t"]
    fl = frames.get(i, ())
    for f0, f1 in zip(fl, fl[1:]):
        age = f0[FT] - t0
        if age < lo - 1e-6:
            continue
        if age >= hi - 1e-6:
            return None
        dt = f1[FT] - f0[FT]
        if 0 < dt <= 10.01:
            return math.hypot(f1[FX] - f0[FX], f1[FZ] - f0[FZ]) / dt
    return None


def first_frame(i):
    t0 = births[i]["t"]
    for fr in frames.get(i, ()):
        if fr[FT] >= t0 - 1e-6:
            return fr
    return None


def med(xs):
    xs = [x for x in xs if x is not None and not (isinstance(x, float) and math.isnan(x))]
    return statistics.median(xs) if xs else float("nan")


def pct(a, b):
    return a / b if b else float("nan")


# ------------------------------------------------------------------ the population
young = [i for i in bred if i not in deaths and births[i]["t"] > END - 60]
youngset = set(young)
withspeed, nospeed = [], []
for i in bred:
    if i in youngset:
        continue
    (withspeed if spd(i) is not None else nospeed).append(i)
diffs = {i: diff_genome(genomes[i], genomes[parent_of(i)]) for i in withspeed}
pop = [i for i in withspeed if diffs[i].n["structure"] == 0]
slow = {i for i in pop if spd(i) < SLOW}
NP, NS = len(pop), len(slow)

print("slow-newborn-read.py  run %s  frames %s" % (A.run, A.frames))
print("to %.0f s, window %g s; slow = first speed under %g m/s; all of it ASSOCIATION in recorded data, not cause" % (
    END, A.window, SLOW))
print("definitions are genome-diff-read.py's (loaded from its source; see this file's docstring)")
print("bred grazers (p >= 0) %d; with both genomes in the file %d; born in the last 60 s and alive, left out %d" % (
    len(bred0), len(bred), len(young)))
print("  of the rest: with a first speed %d, without %d" % (len(withspeed), len(nospeed)))
print("  structure changed %d, structure unchanged (n of the population) %d" % (len(withspeed) - NP, NP))
print("POPULATION: bred grazers with a first speed and structure count 0: n %d, slow %d, share %.3f "
      "(README: about 999, 191, 0.19)" % (NP, NS, pct(NS, NP)))
if NP == 0:
    print("no population; nothing to split. (Is the frames table empty? The fixture in scratch/newborn-ledger/test/ "
          "holds a header only.)")
    sys.exit(0)

popshare = NS / NP
RESULTS = {}      # cause name -> dict(excess, assign, risky)


def band_table(name, title, bands, rank=True):
    """bands: list of (label, [ids]). Prints n, slow, share, and the excess over the safest band's rate."""
    print("\n== %s" % title)
    rows = [(lab, len(m), sum(1 for i in m if i in slow)) for lab, m in bands if m]
    cand = [r for r in rows if r[1] >= MINN] or rows
    ref = min(cand, key=lambda r: (r[2] / r[1], -r[1]))
    rr = ref[2] / ref[1]
    print("  reference (safest band with n >= %d): %s, slow rate %.3f" % (MINN, ref[0], rr))
    print("  %-34s %6s %6s %7s %9s" % ("band", "n", "slow", "share", "excess"))
    tot = 0.0
    risky = set()
    for lab, n, s in rows:
        ex = 0.0 if lab == ref[0] else s - n * rr
        tot += ex
        if s / n > popshare:
            risky.add(lab)
        print("  %-34s %6d %6d %7.3f %9.1f%s" % (lab, n, s, s / n, ex, "  (reference)" if lab == ref[0] else ""))
    print("  excess slow = %.1f, which is %.1f%% of the population's %d slow (association only)" % (
        tot, 100 * tot / NS if NS else float("nan"), NS))
    assign = {}
    for lab, m in bands:
        for i in m:
            assign[i] = lab
    if rank:
        RESULTS[name] = {"excess": tot, "assign": assign, "risky": risky}
    return assign


def quantile_bands(vals, fmt="%.4g"):
    """vals: {id: float}. Bands at the population's quartile values; ties go to the upper band; NaN apart."""
    good = sorted(v for v in vals.values() if not math.isnan(v))
    out = []
    if good:
        e = [good[min(len(good) - 1, int(p * len(good)))] for p in (0.25, 0.5, 0.75)]
        edges = sorted(set(e))
        names = []
        for k in range(len(edges) + 1):
            if k == 0:
                names.append("v < " + fmt % edges[0])
            elif k == len(edges):
                names.append("v >= " + fmt % edges[-1])
            else:
                names.append(("%s <= v < %s" % (fmt, fmt)) % (edges[k - 1], edges[k]))
        groups = [[] for _ in names]
        for i, v in vals.items():
            if not math.isnan(v):
                groups[bisect.bisect_right(edges, v)].append(i)
        out = list(zip(names, groups))
    nan = [i for i, v in vals.items() if math.isnan(v)]
    if nan:
        out.append(("no value", nan))
    return out


# ------------------------------------------------------------------ (a) the parent
PARENT = {}
for i in pop:
    p = parent_of(i)
    v = spd(p) if p in births else None
    PARENT[i] = None if v is None else (v < SLOW)
bandsA = [("parent slow", [i for i in pop if PARENT[i] is True]),
          ("parent moving", [i for i in pop if PARENT[i] is False]),
          ("parent has no speed", [i for i in pop if PARENT[i] is None])]
assignA = band_table("parent", "(a) the parent's own first speed (same rule, same age 0-%g s after the parent's birth)" % A.window, bandsA)
nos = [i for i in bandsA[2][1]]
fo = sum(1 for i in nos if births.get(parent_of(i), {}).get("p", -1) < 0)
ng = sum(1 for i in nos if parent_of(i) in births and births[parent_of(i)].get("p", -1) >= 0 and not grazer(births[parent_of(i)]))
print("  of 'no speed': parent a founder %d, parent bred but not a grazer %d, parent a grazer without a first pair %d" % (
    fo, ng, len(nos) - fo - ng))
fosp = sum(1 for i in pop if parent_of(i) in births and births[parent_of(i)].get("p", -1) < 0 and PARENT[i] is not None)
print("  (founder parents that did get a speed by the rule, counted in the slow/moving bands: %d children)" % fosp)
ps = [i for i in pop if PARENT[i] is True]
pm = [i for i in pop if PARENT[i] is False]
cs = sum(1 for i in ps if i in slow)
cm = sum(1 for i in pm if i in slow)
print("  P(child slow | parent slow)   = %d / %d = %.3f" % (cs, len(ps), pct(cs, len(ps))))
print("  P(child slow | parent moving) = %d / %d = %.3f" % (cm, len(pm), pct(cm, len(pm))))
if pm and ps and cm:
    print("  ratio %.2f, difference %.3f" % ((cs / len(ps)) / (cm / len(pm)), cs / len(ps) - cm / len(pm)))
sp = [i for i in ps if i in slow]
gcount = {"grandparent slow": 0, "grandparent moving": 0, "grandparent has no speed": 0}
for i in sp:
    g = parent_of(parent_of(i))
    v = spd(g) if g >= 0 and g in births else None
    gcount["grandparent has no speed" if v is None else ("grandparent slow" if v < SLOW else "grandparent moving")] += 1
print("  slow children with a slow parent: %d; of those" % len(sp))
for k, v in gcount.items():
    print("    %-26s %5d  (%.3f)" % (k, v, pct(v, len(sp))))

# ------------------------------------------------------------------ (b) brain change
brain = {i: diffs[i].s["brain"] for i in pop}
print("\n(b) the child's summed relative brain change against its parent: min %.4g, median %.4g, max %.4g; "
      "share with no brain change %.3f" % (min(brain.values()), med(list(brain.values())), max(brain.values()),
                                          pct(sum(1 for v in brain.values() if v == 0), NP)))
band_table("brain", "(b) brain change (summed relative change, genome-diff-read's 'brain' category), quartiles of the population",
           quantile_bands(brain))

# ------------------------------------------------------------------ (c) birth size and reserve
ff = {i: first_frame(i) for i in pop}
bf1 = {i: (ff[i][FBF] if ff[i] else float("nan")) for i in pop}
j1 = {i: (ff[i][FJ] if ff[i] else float("nan")) for i in pop}
age1 = {i: (ff[i][FT] - births[i]["t"] if ff[i] else float("nan")) for i in pop}
print("\n(c) first frame's age after birth: median %.1f s (the first frame is on the world's 10 s grid, so J and bf depend "
      "partly on how late it came)" % med(list(age1.values())))
band_table("bf", "(c1) body fraction bf at the child's first frame (bf 1 is adult size), quartiles of the population",
           quantile_bands(bf1))
band_table("J", "(c2) reserve J (joules) at the child's first frame, quartiles of the population", quantile_bands(j1))
bf0 = {i: births[i].get("bf") for i in pop}
bf0 = {i: (float(v) if isinstance(v, (int, float)) else float("nan")) for i, v in bf0.items()}
band_table("bf0", "(c3, extra, not ranked) body fraction at birth, from the lineage birth row 'bf', quartiles",
           quantile_bands(bf0), rank=False)

# ------------------------------------------------------------------ (d) birth place
dh = {i: (ff[i][FDH] if ff[i] else float("nan")) for i in pop}
print("\n(d) dleafh is metres, horizontal, root to the nearest living leaf's root, at the child's first frame "
      "(newborn-extract.py). The README's horizontal median for slow-or-not newborns is 0.53 m, its 3D median 1.45 m, so "
      "the 1.4 m edge here, from the 3D figure, falls in the far tail of a horizontal distance. Bands are the ones asked "
      "for, in the same unit.")
bandsD = [("dleafh < 0.3 m", [i for i in pop if not math.isnan(dh[i]) and dh[i] < 0.3]),
          ("0.3 <= dleafh <= 1.4 m", [i for i in pop if not math.isnan(dh[i]) and 0.3 <= dh[i] <= 1.4]),
          ("dleafh > 1.4 m", [i for i in pop if not math.isnan(dh[i]) and dh[i] > 1.4]),
          ("no leaf in the frame (nan)", [i for i in pop if math.isnan(dh[i])])]
band_table("place", "(d) birth place: horizontal distance to the nearest living leaf at the first frame", bandsD)

# ------------------------------------------------------------------ extra: jointed at birth
band_table("jnt", "(extra, not ranked) jointed at birth (lineage birth row 'jnt')",
           [("jnt 1", [i for i in pop if births[i].get("jnt") == 1]), ("jnt 0", [i for i in pop if births[i].get("jnt") != 1])],
           rank=False)

# ------------------------------------------------------------------ (e) the measure
print("\n== (e) the measure")
print("  histogram of first speeds, m/s, population n %d (slow is < %g)" % (NP, SLOW))
hist = [0] * 11
for i in pop:
    hist[min(10, math.floor(round(spd(i) * 100, 9)))] += 1
cum = 0
for k in range(11):
    cum += hist[k]
    lab = "%.2f-%.2f" % (k / 100, (k + 1) / 100) if k < 10 else ">= 0.10"
    print("    %-10s %5d  %5.1f%%  cumulative %5.1f%%  %s" % (lab, hist[k], 100 * hist[k] / NP, 100 * cum / NP, "#" * round(60 * hist[k] / NP)))
print("  exactly 0 m/s: %d" % sum(1 for i in pop if spd(i) == 0))

sl = sorted(slow)
va = {i: pair_speed(i, 10, 30) for i in sl}
vb = {i: pair_speed(i, 30, 60) for i in sl}
print("\n  the %d slow children's later speeds, first frame pair starting at each age, by the same straight-line rule" % len(sl))
for lab, d in (("age 10-30 s", va), ("age 30-60 s", vb)):
    got = [v for v in d.values() if v is not None]
    print("    pair starting at %s: %d of %d have one; median %.4f m/s; %d at or above %g (%.3f)" % (
        lab, len(got), len(sl), med(got), sum(1 for v in got if v >= SLOW), SLOW, pct(sum(1 for v in got if v >= SLOW), len(got))))
cls = {"late starters (a pair at or above %g by 60 s)" % SLOW: [], "stayers (pairs found, all under %g)" % SLOW: [],
       "no later pair (dead or gone)": []}
ks = list(cls)
for i in sl:
    vs = [v for v in (va[i], vb[i]) if v is not None]
    if any(v >= SLOW for v in vs):
        cls[ks[0]].append(i)
    elif vs:
        cls[ks[1]].append(i)
    else:
        cls[ks[2]].append(i)
print("    %-48s %5s %6s %12s %s" % ("class", "n", "dead", "med age dead", "(age at death in seconds from birth, lineage)"))
for k in ks:
    m = cls[k]
    ages = [deaths[i]["t"] - births[i]["t"] for i in m if i in deaths]
    print("    %-48s %5d %6d %12.1f" % (k, len(m), len(ages), med(ages)))

print("\n  bred grazers with no first speed (genome and parent genome in the file, not born in the last 60 s and alive): "
      "%d (README: 554)" % len(nospeed))
ns_ate = sum(1 for i in nospeed if ate(i))
ws_ate = sum(1 for i in withspeed if ate(i))
print("    ate share, no speed: %d / %d = %.3f;  ate share, with a speed: %d / %d = %.3f" % (
    ns_ate, len(nospeed), pct(ns_ate, len(nospeed)), ws_ate, len(withspeed), pct(ws_ate, len(withspeed))))
ages_ns = [deaths[i]["t"] - births[i]["t"] for i in nospeed if i in deaths]
print("    of the no-speed: dead %d, median age at death %.1f s" % (len(ages_ns), med(ages_ns)))
bredset = set(bred)
noge = [i for i in bred0 if i not in bredset and i not in youngset]
print("    (not in the above: %d bred grazers without both genomes, %d of them with a first speed by the rule)" % (
    len(noge), sum(1 for i in noge if spd(i) is not None)))

# ------------------------------------------------------------------ overlap
print("\n== overlap of the two causes with the largest excess (ranked: parent, brain, bf, J, place; association only)")
order = sorted(RESULTS, key=lambda k: -RESULTS[k]["excess"])
for k in order:
    print("  %-8s excess slow %7.1f" % (k, RESULTS[k]["excess"]))
if len(order) >= 2:
    c1, c2 = order[0], order[1]
    print("  the excess depends on how a cause is cut into bands (4 quartile bands against 3 for the parent and place); "
          "treat the ranking as rough")
    print("  cells: a cause is 'risky' when the child's band there has a slow share above the population's (%.3f)" % popshare)
    for c in (c1, c2):
        print("    %s risky bands: %s" % (c, "; ".join(sorted(RESULTS[c]["risky"])) or "none"))
    cells = {}
    for i in pop:
        r1 = RESULTS[c1]["assign"][i] in RESULTS[c1]["risky"]
        r2 = RESULTS[c2]["assign"][i] in RESULTS[c2]["risky"]
        cells.setdefault((r1, r2), []).append(i)

    def cell(r1, r2):
        m = cells.get((r1, r2), [])
        s = sum(1 for i in m if i in slow)
        return "n %4d  slow %4d  share %s" % (len(m), s, ("%.3f" % (s / len(m))) if m else "  -  ")

    print("\n  rows: %s; columns: %s" % (c1, c2))
    print("  %-22s | %-36s | %-36s" % ("", c2 + " not risky", c2 + " risky"))
    print("  %-22s | %-36s | %-36s" % (c1 + " not risky", cell(False, False), cell(False, True)))
    print("  %-22s | %-36s | %-36s" % (c1 + " risky", cell(True, False), cell(True, True)))
else:
    print("  fewer than two causes to compare")
print("\nAll of the above is association in recorded data. Nothing here separates a cause from what travels with it.")
