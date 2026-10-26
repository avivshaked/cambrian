"""Is a slow bred-grazer newborn's genome changed from its parent's in a way the eaters' are not?

Reads RUN/lineage.jsonl (births, deaths, kills), RUN/genomes.jsonl.gz (one genome per row, with its creature's id) and
the newborn ledger's grazer-frames.tsv (columns id t bf J S breed x y z dleaf3 dleafh flags; see newborn-extract.py).

A grazer is a body born mouthed (atk 1 and ink 1 at birth), as grazer-tally.py defines it. Bred is a grazer with a
parent in the record (p >= 0); the read keeps the bred grazers whose own genome and whose parent's genome are both in
the file. A grazer "ate" if bk > 0 at its death row OR it is the attacker (by) of any kill row; a grazer with no death
row by --end counts as ate (the same rule as grazer-tally.py's, plus the living), except that a living grazer born in
the last 60 s before --end is left out, its fate still open. Rows after --end are not read.

Speed is newborn-ledger.py's "speed from a frame at age 0-10 s": the straight-line horizontal distance (x and z, y is
up) between the first pair of consecutive frames at most 10 s apart whose first frame comes within --window seconds
of birth, over the time between them. A grazer with no such pair has no speed and is left out of the four groups (it
is counted in the header). Slow is under 0.03 m/s.

The diff is child against parent, by category. Each is a count of changed scalars and the sum of |a-b| / max(|a|,
|b|, 1e-9) over them (a changed name, such as a cell type, a joint type or a neuron op, counts 1 and adds 1.0).
Nodes are matched by their index in "nodes", which is how GenomeJson keeps them and how an edge's "child" refers to
them; an edge by its "id" when the genomes carry one (format 12) and otherwise by its position in the node's edge
list; a neuron and its inputs by position; a central brain's unit by its id and its link by its (from, to) pair.
What has no partner is counted in "structure" and the matched part alone is compared. Categories (GenomeJson.cs):

  brain      node and global neurons' frequency, phase, amplitude, bias, input constant, weight and index, and their
             op and input kind and channel (lines 514-561); a central brain's unit biases and link weights (459-510)
  drive      a joint's drive genes threshold, reaction, hold, release (330-335), and any other number the drive object
             carries (pose or hertz genes, should the format grow them), and the joint's power (327)
  limits     jointLimits min and max (354-359), the joint type (326), and each edge's orientation quaternion, which
             sets the joint's axes (426-429)
  body       dimensions (352), buoyancyOffset (350), lift (336), each edge's anchors, scale, reflect and terminalOnly
             (420-424, 431-433), and the shape id (325)
  cell       the cell type id (324) and its four attributes attack, intake, protection, toughness (344-347)
  structure  nodes added or removed, edges added or removed, neurons, inputs, units and links added or removed,
             recursiveLimit, growth, maxModules (337, 342-343), the root index and, in a central genome, an edge
             whose child index changed
  dials      adultScale, lifespan and the reproduction object: brood, investment, margin, mode, gestation (172-181)
  other      any scalar in a node or the genome the lists above do not name (none today)

Output is plain text: a header, then per group (slow and never ate; slow and ate; moving and never ate; moving and
ate) the count, and per category the share with any change, the median and the mean of its summed relative change
over all the group's members (members with no change count 0); then a table of identical to the parent in every
category against any change, for each group and for slow against moving and ate against never ate.

Usage: python -I scripts/reads/genome-diff-read.py RUN FRAMES [--end 24000] [--window 10]
"""
import argparse
import gzip
import json
import math
import os
import statistics

CATS = ["brain", "drive", "limits", "body", "cell", "structure", "dials", "other"]
SLOW = 0.03

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("frames")
ap.add_argument("--end", type=float, default=24000.0)
ap.add_argument("--window", type=float, default=10.0)
a = ap.parse_args()

# ---------------------------------------------------------------- lineage
births, deaths, killers = {}, {}, set()
with open(os.path.join(a.run, "lineage.jsonl"), encoding="utf-8") as f:
    for line in f:
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
            killers.add(r.get("by"))


def grazer(b):
    return b.get("atk") == 1 and b.get("ink") == 1


bred = [i for i, b in births.items() if grazer(b) and b.get("p", -1) >= 0]
bred_all = len(bred)
need = set(bred) | {births[i]["p"] for i in bred}

# ---------------------------------------------------------------- genomes (only those needed)
genomes = {}
with gzip.open(os.path.join(a.run, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
    for line in f:
        if not line.strip():
            continue
        r = json.loads(line)
        i = r.get("id")
        if i in need and i not in genomes:
            genomes[i] = r
            if len(genomes) == len(need):
                break
bred = [i for i in bred if i in genomes and births[i]["p"] in genomes]

# ---------------------------------------------------------------- frames
frames = {}
want = set(bred)
with open(a.frames, encoding="utf-8") as f:
    head = f.readline().rstrip("\n").split("\t")
    col = {n: k for k, n in enumerate(head)}
    for line in f:
        p = line.rstrip("\n").split("\t")
        if len(p) < len(head):
            continue
        i = int(p[col["id"]])
        if i not in want:
            continue
        try:
            frames.setdefault(i, []).append((float(p[col["t"]]), float(p[col["x"]]), float(p[col["z"]])))
        except ValueError:
            pass


def speed(i):
    t0 = births[i]["t"]
    # As newborn-ledger.py's "speed from a frame at age 0-10 s": the first pair of consecutive frames, at most 10 s
    # apart, whose first frame comes within --window seconds of birth. The state stream's frames are 10 s apart.
    fl = sorted(frames.get(i, ()))
    for f0, f1 in zip(fl, fl[1:]):
        if f0[0] < t0 - 1e-6:
            continue
        if f0[0] - t0 >= a.window:
            return None
        dt = f1[0] - f0[0]
        if 0 < dt <= 10.01:
            return math.hypot(f1[1] - f0[1], f1[2] - f0[2]) / dt
        return None
    return None


def ate(i):
    d = deaths.get(i)
    return d is None or (d.get("bk") or 0.0) > 0 or i in killers


# ---------------------------------------------------------------- the diff
class Diff:
    def __init__(self):
        self.n = {c: 0 for c in CATS}
        self.s = {c: 0.0 for c in CATS}

    def add(self, cat, k=1, rel=None):
        # an added or removed part counts 1.0 of relative change each, as a changed name does
        self.n[cat] += k
        self.s[cat] += k * 1.0 if rel is None else rel

    def num(self, cat, x, y):
        if isinstance(x, bool) or isinstance(y, bool) or not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
            self.name(cat, x, y)
            return
        if x != y:
            self.add(cat, 1, abs(x - y) / max(abs(x), abs(y), 1e-9))

    def name(self, cat, x, y):
        if x != y:
            self.add(cat, 1, 1.0)

    def any(self):
        return any(self.n[c] for c in CATS)


def xyz(d, cat, p, q, keys=("x", "y", "z")):
    for k in keys:
        d.num(cat, p.get(k), q.get(k))


def diff_neurons(d, ch, pa):
    if not isinstance(ch, list) or not isinstance(pa, list):
        return
    for k in range(min(len(ch), len(pa))):
        c, p = ch[k], pa[k]
        for key in ("frequency", "phase", "amplitude", "bias"):
            d.num("brain", c.get(key), p.get(key))
        d.name("brain", c.get("op"), p.get("op"))
        ci, pi = c.get("inputs", []), p.get("inputs", [])
        for m in range(min(len(ci), len(pi))):
            for key in ("index", "constant", "weight"):
                d.num("brain", ci[m].get(key), pi[m].get(key))
            d.name("brain", ci[m].get("kind"), pi[m].get("kind"))
            d.name("brain", ci[m].get("channel"), pi[m].get("channel"))
        d.add("structure", abs(len(ci) - len(pi)))
    d.add("structure", abs(len(ch) - len(pa)))


def diff_central(d, ch, pa):
    if ch is None and pa is None:
        return
    if ch is None or pa is None:
        d.add("structure", 1, 1.0)
        return
    for part in ("outputs", "hidden"):
        cm = {u["id"]: u for u in ch.get(part, [])}
        pm = {u["id"]: u for u in pa.get(part, [])}
        for k in cm.keys() & pm.keys():
            d.num("brain", cm[k].get("bias"), pm[k].get("bias"))
        d.add("structure", len(cm.keys() ^ pm.keys()))
    cm = {(l["from"], l["to"]): l for l in ch.get("links", [])}
    pm = {(l["from"], l["to"]): l for l in pa.get("links", [])}
    for k in cm.keys() & pm.keys():
        d.num("brain", cm[k].get("w"), pm[k].get("w"))
    d.add("structure", len(cm.keys() ^ pm.keys()))


def diff_edges(d, ch, pa):
    if ch and pa and all("id" in e for e in ch) and all("id" in e for e in pa):
        cm = {e["id"]: e for e in ch}
        pm = {e["id"]: e for e in pa}
        pairs = [(cm[k], pm[k]) for k in cm.keys() & pm.keys()]
        d.add("structure", len(cm.keys() ^ pm.keys()))
    else:
        n = min(len(ch), len(pa))
        pairs = [(ch[k], pa[k]) for k in range(n)]
        d.add("structure", abs(len(ch) - len(pa)))
    for c, p in pairs:
        for key in ("parentAnchor", "childAnchor", "scale"):
            xyz(d, "body", c.get(key, {}), p.get(key, {}))
        xyz(d, "body", c.get("reflect", {}), p.get("reflect", {}))
        d.name("body", c.get("terminalOnly"), p.get("terminalOnly"))
        xyz(d, "limits", c.get("orientation", {}), p.get("orientation", {}), ("x", "y", "z", "w"))
        d.num("structure", c.get("child"), p.get("child"))


NODE_KNOWN = {"cell", "shape", "joint", "power", "drive", "lift", "recursiveLimit", "growth", "maxModules", "attack",
              "intake", "protection", "toughness", "buoyancyOffset", "dimensions", "jointLimits", "edges", "neurons"}
TOP_KNOWN = {"id", "moduleCounts", "lostPaths", "format", "root", "nextEdgeId", "adultScale", "lifespan",
             "reproduction", "nodes", "globalBrain", "brain"}


def diff_node(d, c, p):
    d.name("cell", c.get("cell"), p.get("cell"))
    for key in ("attack", "intake", "protection", "toughness"):
        d.num("cell", c.get(key), p.get(key))
    d.name("body", c.get("shape"), p.get("shape"))
    d.name("limits", c.get("joint"), p.get("joint"))
    d.num("drive", c.get("power"), p.get("power"))
    cd, pd = c.get("drive") or {}, p.get("drive") or {}
    for key in cd.keys() | pd.keys():
        d.num("drive", cd.get(key), pd.get(key))
    d.num("body", c.get("lift"), p.get("lift"))
    d.num("body", c.get("buoyancyOffset"), p.get("buoyancyOffset"))
    xyz(d, "body", c.get("dimensions", {}), p.get("dimensions", {}))
    d.num("structure", c.get("recursiveLimit"), p.get("recursiveLimit"))
    d.name("structure", c.get("growth"), p.get("growth"))
    d.num("structure", c.get("maxModules"), p.get("maxModules"))
    cl, pl = c.get("jointLimits", []), p.get("jointLimits", [])
    for k in range(min(len(cl), len(pl))):
        d.num("limits", cl[k].get("min"), pl[k].get("min"))
        d.num("limits", cl[k].get("max"), pl[k].get("max"))
    d.add("structure", abs(len(cl) - len(pl)))
    diff_edges(d, c.get("edges", []), p.get("edges", []))
    diff_neurons(d, c.get("neurons", []), p.get("neurons", []))
    for key in (c.keys() | p.keys()) - NODE_KNOWN:
        if not isinstance(c.get(key, p.get(key)), (dict, list)):
            d.num("other", c.get(key), p.get(key))


def diff_genome(c, p):
    d = Diff()
    cn, pn = c.get("nodes", []), p.get("nodes", [])
    for k in range(min(len(cn), len(pn))):
        diff_node(d, cn[k], pn[k])
    d.add("structure", abs(len(cn) - len(pn)))
    d.num("structure", c.get("root"), p.get("root"))
    diff_neurons(d, c.get("globalBrain", []), p.get("globalBrain", []))
    diff_central(d, c.get("brain"), p.get("brain"))
    for key in ("adultScale", "lifespan"):
        d.num("dials", c.get(key), p.get(key))
    cr, pr = c.get("reproduction") or {}, p.get("reproduction") or {}
    for key in cr.keys() | pr.keys():
        d.num("dials", cr.get(key), pr.get(key))
    for key in (c.keys() | p.keys()) - TOP_KNOWN:
        if not isinstance(c.get(key, p.get(key)), (dict, list)):
            d.num("other", c.get(key), p.get(key))
    return d


# ---------------------------------------------------------------- groups
GROUPS = ["slow, never ate", "slow, ate", "moving, never ate", "moving, ate"]
members = {g: [] for g in GROUPS}
nospeed = 0
young = 0
for i in bred:
    # A grazer born in the last 60 s before --end and still alive has not had the time to starve; its fate is open.
    if i not in deaths and births[i]["t"] > a.end - 60:
        young += 1
        continue
    v = speed(i)
    if v is None:
        nospeed += 1
        continue
    g = ("slow" if v < SLOW else "moving") + (", ate" if ate(i) else ", never ate")
    members[g].append(diff_genome(genomes[i], genomes[births[i]["p"]]))


def med(xs):
    return statistics.median(xs) if xs else float("nan")


def mean(xs):
    return sum(xs) / len(xs) if xs else float("nan")


print(f"run {a.run}, to {a.end:.0f} s, window {a.window:g} s, slow under {SLOW} m/s")
print(f"bred grazers {bred_all}; with both their genome and their parent's in the file {len(bred)}; "
      f"with a speed {len(bred) - nospeed - young}; no speed, left out {nospeed}; born in the last 60 s and alive, left out {young}")
print("groups: " + "; ".join(f"{g} {len(members[g])}" for g in GROUPS))
print("per category: share of the group with any change / median / mean of the summed relative change over all members")

for g in GROUPS:
    ms = members[g]
    print(f"\n== {g}  (n {len(ms)})")
    print(f"  {'category':10s} {'changed':>8s} {'share':>7s} {'median':>9s} {'mean':>9s} {'scalars/member':>15s}")
    for c in CATS:
        if not ms:
            print(f"  {c:10s} {'-':>8s}")
            continue
        sums = [m.s[c] for m in ms]
        k = sum(1 for m in ms if m.n[c])
        sc = mean([m.n[c] for m in ms])
        print(f"  {c:10s} {k:8d} {k / len(ms):7.1%} {med(sums):9.4f} {mean(sums):9.4f} {sc:15.2f}")

print("\n== identical to the parent in every category, against any change")
print(f"  {'group':20s} {'n':>5s} {'identical':>10s} {'any change':>11s} {'share any':>10s} {'med total':>10s} {'mean total':>11s}")
rows = [(g, members[g]) for g in GROUPS]
rows += [("slow (both)", members[GROUPS[0]] + members[GROUPS[1]]),
         ("moving (both)", members[GROUPS[2]] + members[GROUPS[3]]),
         ("never ate (both)", members[GROUPS[0]] + members[GROUPS[2]]),
         ("ate (both)", members[GROUPS[1]] + members[GROUPS[3]])]
for name, ms in rows:
    if not ms:
        print(f"  {name:20s} {0:5d}")
        continue
    anyc = sum(1 for m in ms if m.any())
    tot = [sum(m.s.values()) for m in ms]
    print(f"  {name:20s} {len(ms):5d} {len(ms) - anyc:10d} {anyc:11d} {anyc / len(ms):10.1%} {med(tot):10.4f} {mean(tot):11.4f}")

print("\nper-category share with any change, pooled: slow against moving, never ate against ate")
pools = [("slow", rows[4][1]), ("moving", rows[5][1]), ("never ate", rows[6][1]), ("ate", rows[7][1])]
print(f"  {'category':10s} " + " ".join(f"{n:>10s}" for n, _ in pools))
for c in CATS:
    print(f"  {c:10s} " + " ".join(
        (f"{sum(1 for m in ms if m.n[c]) / len(ms):10.1%}" if ms else f"{'-':>10s}") for _, ms in pools))
