"""The newborn ledger: why bred grazers die before they keep a meal, from one run's lineage, stats and the table
newborn-extract.py writes from its state stream.

A grazer is a body born with atk 1 and ink 1; bred means p >= 0 (grazer-tally.py). "Ate" means its death row carries
kept intake (bk, iok, iak or ic above 0) or a kill row names it as the attacker; the record does not say when in
its life it ate. Every number printed is measured (read from a record) unless its line says derived.

Each frame of the state stream gives a body's reserve J, its seconds of reserve S (J over its standing watts, so
U = J/S is the standing cost the world was charging it, measured), its body fraction bf and its funds over its
breeding gate (breed, so the gate is J/breed). Frames are 10 s apart.

Derived, from World.cs (the newborn's share split by NewbornReserveFraction 0.5, so a newborn's reserve at birth equals
its tissue at birth, bf0 x A), and Grow (every metabolic step, the reserve above GrowthReserveFloor x tissue goes to
growth): A, the adult tissue, is (J1 + U1 x age1) / (2 bf0 - bf1) at the first frame, treating the reserve spent as
standing cost only. A child's price is tissue + reserve + max(50, 2 x tissue) = 2 bf0 A + max(50, 2 bf0 A).

Usage: python -I scripts/reads/newborn-ledger.py RUN FRAMESDIR [--end 24000]
"""
import argparse
import json
import math
import os
import statistics
from collections import defaultdict

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("frames")
ap.add_argument("--end", type=float, default=24000.0)
a = ap.parse_args()
cfg = json.load(open(os.path.join(a.run, "config.json")))


def q(xs, p):
    xs = sorted(x for x in xs if x is not None and not math.isnan(x))
    if not xs:
        return float("nan")
    return xs[min(len(xs) - 1, int(p * len(xs)))]


def med(xs):
    return q(xs, 0.5)


def mqq(xs):
    xs = [x for x in xs if x is not None and not math.isnan(x)]
    return "%.3g [%.3g, %.3g] n=%d" % (q(xs, .5), q(xs, .25), q(xs, .75), len(xs))


# ---------------------------------------------------------------- lineage
births, deaths = {}, {}
kills_by = defaultdict(list)
for line in open(os.path.join(a.run, "lineage.jsonl")):
    if not line.strip():
        continue
    r = json.loads(line)
    if r.get("t", 0) > a.end:
        break
    e = r.get("e")
    if e == "b":
        births[r["id"]] = r
    elif e == "d":
        deaths[r["id"]] = r
    elif e == "k":
        kills_by[r.get("by")].append(r)


def grazer(b):
    return b is not None and b.get("atk") == 1 and b.get("ink") == 1


def kept(d):
    return sum((d.get(k) or 0.0) for k in ("bk", "iok", "iak", "ic"))


def deaths_bk(r):
    d = deaths.get(r["id"])
    return (d.get("bk") or 0.0) if d else 0.0


def ate(i):
    d = deaths.get(i)
    return (d is not None and kept(d) > 0) or len(kills_by.get(i, ())) > 0


# ---------------------------------------------------------------- frames
frames = defaultdict(list)
with open(os.path.join(a.frames, "grazer-frames.tsv"), encoding="utf-8") as f:
    f.readline()
    for line in f:
        c = line.rstrip("\n").split("\t")
        frames[int(c[0])].append(tuple(float(v) for v in c[1:11]))
# columns now: t bf J S breed x y z dleaf3 dleafh
T, BF, J, S, BR, X, Y, Z, D3, DH = range(10)
for v in frames.values():
    v.sort()


def U_of(fr):
    j, s = fr[J], fr[S]
    if j > 0 and s > 0 and not math.isinf(s) and not math.isnan(s):
        return j / s
    return float("nan")


def speeds(fl, lo=None, hi=None):
    out = []
    for p0, p1 in zip(fl, fl[1:]):
        dt = p1[T] - p0[T]
        if not (0 < dt <= 10.01):
            continue
        if lo is not None and p0[T] < lo:
            continue
        if hi is not None and p1[T] > hi:
            continue
        out.append(math.hypot(p1[X] - p0[X], p1[Z] - p0[Z]) / dt)
    return out


bred = sorted(i for i, b in births.items() if grazer(b) and b.get("p", -1) >= 0)
founders = sorted(i for i, b in births.items() if grazer(b) and b.get("p", -1) < 0)

rec = {}
for i in bred:
    b = births[i]
    t0 = b["t"]
    d = deaths.get(i)
    fl = [fr for fr in frames.get(i, ()) if fr[T] >= t0 - 1e-6]
    r = {"id": i, "t0": t0, "bf0": b.get("bf"), "p": b["p"], "dead": d is not None,
         "age": (d["t"] - t0) if d else None, "cause": d.get("c") if d else None,
         "kept": kept(d) if d else None, "kills": len(kills_by.get(i, ())), "ate": ate(i),
         "fl": fl}
    if fl:
        f1 = fl[0]
        r["age1"] = f1[T] - t0
        r["bf1"] = f1[BF]
        r["J1"] = f1[J]
        r["U1"] = U_of(f1)
        r["S1"] = f1[S]
        r["d3"] = f1[D3]
        r["dh"] = f1[DH]
        den = 2 * r["bf0"] - r["bf1"]
        u = r["U1"] if not math.isnan(r["U1"]) else 0.0
        r["A"] = (r["J1"] + u * r["age1"]) / den if den > 0.02 else float("nan")
        r["v60"] = speeds(fl, hi=t0 + 60)
    # the parent just before the birth
    pf = frames.get(b["p"], [])
    before = [fr for fr in pf if fr[T] < t0 - 1e-6]
    after = [fr for fr in pf if fr[T] >= t0 - 1e-6]
    pb = births.get(b["p"])
    r["p_grazer"] = grazer(pb)
    r["p_age"] = t0 - pb["t"] if pb else None
    r["p_founder"] = pb is not None and pb.get("p", -1) < 0
    if before:
        fb = before[-1]
        r["pJ"] = fb[J]
        r["pU"] = U_of(fb)
        r["pbreed"] = fb[BR]
        r["pgate"] = fb[J] / fb[BR] if fb[BR] and fb[BR] > 0 else float("nan")
        r["pbf"] = fb[BF]
        r["pd3"] = fb[D3]
        r["pdh"] = fb[DH]
        r["pfeed"] = (fb[J] - before[-2][J]) if len(before) >= 2 and fb[T] - before[-2][T] <= 10.01 else float("nan")
        if after and after[0][T] - fb[T] <= 10.01:
            r["pdrop"] = fb[J] - after[0][J]
    pk = kills_by.get(b["p"], [])
    r["p_kills100"] = sum(1 for k in pk if t0 - 100 <= k["t"] <= t0)
    r["p_killsall"] = sum(1 for k in pk if k["t"] <= t0)
    pd = deaths.get(b["p"])
    r["p_kept"] = kept(pd) if pd else None
    rec[i] = r

dead = [rec[i] for i in bred if rec[i]["dead"]]
never = [r for r in dead if not r["ate"]]
eaters = [r for r in dead if r["ate"]]

print("run %s, to %.0f s" % (a.run, a.end))
print("config: NewbornReserveFraction %s, GrowthReserveFloor %s, IntakeScale %s, WorkCostMultiplier %s, "
      "neural cost %s / %s W, overhead floor %s J + %s J a tissue joule, paceFactor %s" % (
          cfg["growth"]["newbornReserveFraction"], cfg["growth"]["growthReserveFloor"], cfg["mouth"]["intakeScale"],
          cfg["economy"]["workCostMultiplier"], cfg["economy"]["neuralCostPerNeuronWatts"],
          cfg["economy"]["neuralCostPerConnectionWatts"], cfg["economy"]["perOffspringOverheadJoules"],
          cfg["economy"]["perOffspringOverheadPerTissueJoule"], cfg["physics"]["paceFactor"]))
print("bred grazers born %d, dead %d; ate (bk/iok/iak/ic > 0 or a kill) %d, never ate %d"
      % (len(bred), len(dead), len(eaters), len(never)))
print("  never-ate by bk alone (grazer-tally's test): %d" % sum(
    1 for i in bred if rec[i]["dead"] and not (deaths[i].get("bk") or 0) > 0))
print("  dead without a single pose frame (died before the next 10 s frame): %d" % sum(1 for r in dead if not r["fl"]))

# ---------------------------------------------------------------- Q1
print("\n== Q1 spending at birth and over the first 60 s (median [quartiles])")
for name, grp in (("never ate", never), ("ate", eaters)):
    g = [r for r in grp if r["fl"]]
    print("-- bred, %s (with a frame): %d" % (name, len(g)))
    print("  birth body fraction bf0 (birth row)            %s" % mqq([r["bf0"] for r in g]))
    print("  first frame age s                              %s" % mqq([r["age1"] for r in g]))
    print("  body fraction at first frame bf1               %s" % mqq([r["bf1"] for r in g]))
    print("  bf1 / bf0                                      %s" % mqq([r["bf1"] / r["bf0"] for r in g if r["bf0"]]))
    print("  share at bf1 >= 0.999 (adult at first frame)   %.3f" % (sum(1 for r in g if r["bf1"] >= 0.999) / max(1, len(g))))
    print("  reserve J1 at first frame J                    %s" % mqq([r["J1"] for r in g]))
    print("  standing cost U1 at first frame W              %s" % mqq([r["U1"] for r in g]))
    print("  seconds of reserve S1 at first frame           %s" % mqq([r["S1"] for r in g]))
    print("  U1 / bf1 W (per adult-size unit)               %s" % mqq([r["U1"] / r["bf1"] for r in g if r["bf1"] > 0]))
    print("  derived adult tissue A J                       %s" % mqq([r["A"] for r in g]))
    print("  derived reserve at birth bf0*A J               %s" % mqq([r["bf0"] * r["A"] for r in g]))
    print("  derived growth spend (bf1-bf0)*A J             %s" % mqq([(r["bf1"] - r["bf0"]) * r["A"] for r in g]))
    print("  derived growth spend / reserve at birth        %s" % mqq([(r["bf1"] - r["bf0"]) / r["bf0"] for r in g]))
    print("  age at death s                                 %s" % mqq([r["age"] for r in g]))
    print("  first-frame clock: age1 + S1 s                 %s" % mqq([r["age1"] + r["S1"] for r in g]))
    by = defaultdict(list)
    for r in g:
        for fr in r["fl"]:
            k = int((fr[T] - r["t0"]) // 10) * 10
            if k <= 60:
                by[k].append(fr)
    for k in sorted(by):
        fs = by[k]
        print("    age %2d-%2d s: frames %4d, bf %s, J %s, U W %s" % (
            k, k + 10, len(fs), "%.3f" % med([x[BF] for x in fs]), "%.2f" % med([x[J] for x in fs]),
            "%.3f" % med([U_of(x) for x in fs])))

adultU, adultJ, adultS, founderU = [], [], [], []
adult_ids = set()
for i in bred + founders:
    b0 = births[i]["t"]
    for fr in frames.get(i, ()):
        if fr[BF] >= 0.999 and fr[T] - b0 >= 120:
            u = U_of(fr)
            adultU.append(u)
            adultJ.append(fr[J])
            adultS.append(fr[S])
            adult_ids.add(i)
            if births[i].get("p", -1) < 0:
                founderU.append(u)
print("-- adult grazer frames (bf >= 0.999, age >= 120 s), bred and founder: %d frames of %d bodies" % (len(adultU), len(adult_ids)))
print("  standing cost U W                              %s" % mqq(adultU))
print("  founders only U W                              %s" % mqq(founderU))
print("  reserve J                                      %s" % mqq(adultJ))
print("  seconds of reserve                             %s" % mqq(adultS))

stats = [json.loads(l) for l in open(os.path.join(a.run, "stats.jsonl")) if l.strip()]
stats = [s for s in stats if s["t"] <= a.end]
wm = cfg["economy"]["workCostMultiplier"]
wpj = [s["workJoulesPerSecond"] * wm / s["jointed"] for s in stats if s.get("jointed") and s["t"] >= 3000]
print("  derived: world billed stroke work per jointed body W (workJoulesPerSecond x WorkCostMultiplier / jointed,"
      " rows from 3000 s)  %s" % mqq(wpj))
share = [s["workJoules"] / s["spendJoules"] for s in stats if s.get("spendJoules")]
print("  measured: billed work over all spending of the living (workJoules / spendJoules)  %s" % mqq(share))

# ---------------------------------------------------------------- Q2
print("\n== Q2 income ceiling")
last = stats[-1]
blows, fin = last["mouthBlows"], last["mouthBlowsFinishing"]
print("  mouthBlows %s, finishing %s, biteKeptLeaves %.0f J, biteKeptOthers %.0f J, biteWaste %.0f J (last row; "
      "cumulative?)" % (blows, fin, last["biteKeptLeaves"], last["biteKeptOthers"], last["biteWaste"]))
mono = all(stats[k + 1]["mouthBlows"] >= stats[k]["mouthBlows"] for k in range(len(stats) - 1))
print("  mouthBlows never falls between rows: %s" % mono)
if mono:
    print("  derived: kept joules a blow on leaves and others  %.1f J" % ((last["biteKeptLeaves"] + last["biteKeptOthers"]) / max(1, blows)))
eat_rate = [r["kept"] / r["age"] for r in eaters if r["age"]]
print("  bred eaters' kept intake over life W            %s" % mqq(eat_rate))
print("  bred eaters' kept joules J                      %s" % mqq([r["kept"] for r in eaters]))
print("  bred eaters' kills (whole bodies)               %s" % mqq([r["kills"] for r in eaters]))
fe = [i for i in founders if i in deaths]
print("  founders' kept intake over life W               %s" % mqq([kept(deaths[i]) / max(1e-9, deaths[i]["t"] - births[i]["t"]) for i in fe]))

# ---------------------------------------------------------------- Q3
print("\n== Q3 distance to the nearest living leaf, and speed")
for name, grp in (("never ate", never), ("ate", eaters)):
    g = [r for r in grp if r["fl"]]
    print("-- bred, %s: at first frame" % name)
    print("  nearest leaf, root to root 3D m                %s" % mqq([r["d3"] for r in g]))
    print("  nearest leaf, horizontal m                     %s" % mqq([r["dh"] for r in g]))
    print("  share with a leaf within 1 m (3D)              %.3f" % (sum(1 for r in g if r["d3"] <= 1) / max(1, len(g))))
    print("  share with a leaf within 2 m (3D)              %.3f" % (sum(1 for r in g if r["d3"] <= 2) / max(1, len(g))))
    sp = [v for r in g for v in r.get("v60", [])]
    print("  net horizontal speed, frames within 60 s m/s   %s" % mqq(sp))
    for lo in (0, 10, 20, 30):
        band = []
        for r in g:
            fl = r["fl"]
            for p0, p1 in zip(fl, fl[1:]):
                dt = p1[T] - p0[T]
                if 0 < dt <= 10.01 and lo <= p0[T] - r["t0"] < lo + 10:
                    band.append(math.hypot(p1[X] - p0[X], p1[Z] - p0[Z]) / dt)
        print("    speed from a frame at age %2d-%2d s m/s      %s" % (lo, lo + 10, mqq(band)))
    dpar, dy = [], []
    for r in g:
        pf = [fr for fr in frames.get(r["p"], ()) if abs(fr[T] - r["fl"][0][T]) < 1e-6]
        if pf:
            f1 = r["fl"][0]
            dpar.append(math.sqrt((f1[X] - pf[0][X]) ** 2 + (f1[Y] - pf[0][Y]) ** 2 + (f1[Z] - pf[0][Z]) ** 2))
            dy.append(f1[Y] - pf[0][Y])
    print("  distance to the parent at first frame m        %s" % mqq(dpar))
    print("  height above the parent at first frame m       %s" % mqq(dy))
    jn = [r for r in grp if births[r["id"]].get("jnt") == 1]
    print("  jointed at birth (jnt 1): %d of %d" % (len(jn), len(grp)))
    slow = [r for r in g if r.get("v60") and med(r["v60"]) < 0.03]
    print("  members (with frames) whose median speed in 60 s is under 0.03 m/s: %d of %d with a speed"
          % (len(slow), sum(1 for r in g if r.get("v60"))))
    for lab, sub in (("jnt 1", [r for r in g if births[r["id"]].get("jnt") == 1]),
                     ("jnt 0", [r for r in g if births[r["id"]].get("jnt") != 1])):
        print("    %s: n %d, speed within 60 s m/s %s" % (lab, len(sub), mqq([v for r in sub for v in r.get("v60", [])])))
    causes = defaultdict(int)
    for r in grp:
        causes[r["cause"]] += 1
    print("  causes of death: %s" % dict(causes))
    print("  kills by the group's members: %s; kept by bk only (no kill) %d, kill only (bk 0) %d" % (
        mqq([r["kills"] for r in grp]), sum(1 for r in grp if r["kills"] == 0 and r["kept"] > 0),
        sum(1 for r in grp if r["kills"] > 0 and not (deaths_bk(r) > 0))))
asp = []
for i in adult_ids:
    b0 = births[i]["t"]
    fl = [fr for fr in frames[i] if fr[BF] >= 0.999 and fr[T] - b0 >= 120]
    asp += speeds(fl)
print("-- adult grazers: net horizontal speed m/s       %s" % mqq(asp))
ad = [fr[D3] for i in adult_ids for fr in frames[i] if fr[BF] >= 0.999 and fr[T] - births[i]["t"] >= 120]
print("-- adult grazers: nearest leaf 3D m              %s" % mqq(ad))
lc = [l.split("\t") for l in open(os.path.join(a.frames, "leaf-counts.tsv"), encoding="utf-8")][1:]
print("-- living leaves a frame, from 3000 s             %s" % mqq([float(c[1]) for c in lc if float(c[0]) >= 3000]))

# ---------------------------------------------------------------- Q4
print("\n== Q4 eaters against never-eaters (bred, dead), median [quartiles]")
rows4 = [
    ("birth body fraction bf0", "bf0"), ("reserve at first frame J1 J", "J1"), ("U1 W", "U1"),
    ("derived A J", "A"), ("nearest leaf at first frame 3D m", "d3"), ("parent's reserve before J", "pJ"),
    ("parent's funds over its gate", "pbreed"), ("parent's gate J (J/breed)", "pgate"),
    ("parent's body fraction", "pbf"), ("parent's age at the birth s", "p_age"),
    ("parent's reserve change over the 10 s before J", "pfeed"), ("parent's kills in the 100 s before", "p_kills100"),
    ("parent's kills before the birth", "p_killsall"), ("parent's nearest leaf 3D m", "pd3"),
    ("parent's reserve drop across the birth frame J", "pdrop"), ("parent's lifetime kept J (if dead)", "p_kept"),
    ("birth time s", "t0"),
]
for label, key in rows4:
    print("  %-48s never %-30s ate %s" % (label, mqq([r.get(key) for r in never]), mqq([r.get(key) for r in eaters])))
for label, f_ in (("parent feeding (reserve rose over the 10 s before)", lambda r: r.get("pfeed", float("nan")) > 0),
                  ("parent made a kill in the 100 s before", lambda r: r.get("p_kills100", 0) > 0),
                  ("parent is a founder", lambda r: r["p_founder"]),
                  ("parent is a grazer", lambda r: r["p_grazer"]),
                  ("leaf within 1 m at first frame", lambda r: r.get("d3", 99) <= 1),
                  ("adult size at first frame", lambda r: r.get("bf1", 0) >= 0.999)):
    n1 = sum(1 for r in never if f_(r))
    n2 = sum(1 for r in eaters if f_(r))
    print("  %-55s never %d/%d (%.2f)  ate %d/%d (%.2f)" % (label, n1, len(never), n1 / max(1, len(never)), n2,
                                                           len(eaters), n2 / max(1, len(eaters))))
# eat share by nearest-leaf band
print("  share that ate, by nearest leaf at first frame (3D):")
for lo, hi in ((0, 0.75), (0.75, 1.0), (1.0, 1.5), (1.5, 2.5), (2.5, 5), (5, 1e9)):
    g = [r for r in dead if r["fl"] and lo <= r["d3"] < hi]
    print("    %4.2f-%-6g m: %4d dead, ate %.2f" % (lo, hi, len(g), sum(1 for r in g if r["ate"]) / max(1, len(g))))
print("  share that ate, by reserve at first frame J1:")
for lo, hi in ((0, 5), (5, 10), (10, 20), (20, 40), (40, 1e9)):
    g = [r for r in dead if r["fl"] and lo <= r["J1"] < hi]
    print("    %3g-%-5g J: %4d dead, ate %.2f" % (lo, hi, len(g), sum(1 for r in g if r["ate"]) / max(1, len(g))))

# ---------------------------------------------------------------- Q5
print("\n== Q5 life history")
breedings = defaultdict(list)
for i in bred:
    breedings[(rec[i]["p"], rec[i]["t0"])].append(i)
sizes = [len(v) for v in breedings.values()]
print("  breedings that made a bred grazer: %d; children a breeding %s; share with one child %.3f" % (
    len(breedings), mqq(sizes), sum(1 for s in sizes if s == 1) / max(1, len(sizes))))
price = []
for i in bred:
    r = rec[i]
    if r.get("A") and not math.isnan(r["A"]):
        tissue = r["bf0"] * r["A"]
        over = max(cfg["economy"]["perOffspringOverheadJoules"], cfg["economy"]["perOffspringOverheadPerTissueJoule"] * tissue)
        price.append((2 * tissue, over, 2 * tissue + over))
print("  derived joules a child: tissue+reserve %s; overhead %s; price %s" % (
    mqq([p[0] for p in price]), mqq([p[1] for p in price]), mqq([p[2] for p in price])))
per_parent = defaultdict(int)
for (p, t0), v in breedings.items():
    per_parent[p] += 1
pp = []
for p in set(rec[i]["p"] for i in bred):
    if p in deaths:
        pp.append(per_parent[p])
print("  breedings in a dead parent's life (grazer children only) %s" % mqq(pp))
surv = defaultdict(lambda: [0, 0])
for (p, t0), v in breedings.items():
    for i in v:
        surv[p][0] += 1
        surv[p][1] += 1 if rec[i]["ate"] else 0
print("  children per parent that ever ate                %s" % mqq([s[1] for s in surv.values()]))
pgate = [rec[i]["pgate"] for i in bred if rec[i].get("pgate") is not None]
print("  parent's breeding gate J/breed J (frame before)  %s" % mqq(pgate))
print("  parent's reserve drop across the birth frame J   %s" % mqq([rec[i].get("pdrop") for i in bred]))
gen = defaultdict(int)
for i in bred:
    gen[births[i].get("g")] += 1
print("  bred grazers by generation: %s" % dict(sorted(gen.items())[:12]))
