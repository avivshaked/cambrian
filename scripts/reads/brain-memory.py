"""How much memory do evolved central brains carry? A read-only census of recorded runs.

Usage: python -I scripts/reads/brain-memory.py [--out FILE] [--rows FILE] [--cap STEPS] [RUN@T1,T2,... ...]
       python -I scripts/reads/brain-memory.py --self-test
       python -I scripts/reads/brain-memory.py --hand-check GENOMES.jsonl.gz ID

A RUN@TIMES argument is `label=run directory@seconds,seconds` (a label is printed in the tables). With no argument the
read takes the census of the brief: r64t-t-6202 and r64t-t-6203 (run 2026-10-31-235754-2991f19e) at 6,000, 12,000 and
24,000 s, and r64s2-d-6202 (run 2026-10-30-233542-11de0613) at its last snapshot, 46,500 s. Reads the files only, writes
the tables to --out (default scratch/brain-census/census.txt) and, with --rows, one JSON row a brain. Standard library
plus numpy. Writes nothing else, steps no world.

Where the genomes come from. The living population at time T is the set of ids in `snapshots/<T:09d>.jsonl.gz` (the
run's own record of who is alive at that sample). Each id's genome is its row in `genomes.jsonl.gz`, which holds one
row for every body ever born, with the brain as `CentralWiring.Effective` made it at birth (the world materialises it,
so a genome row is the brain the body runs). The founders are the lineage birth rows (`lineage.jsonl`, "e":"b") with
parent p < 0, in two groups by their `src`: "floor" (the first seeding, t < 120 s) and "pool" or "trickle" (founders
drawn into the world from the pool from about 3,000 s on).

The brain (src/Evosim.Core/Brain/Brain.cs StepCentral, ForCentral). Every physics step (0.01 s) each unit u, an output
or a hidden unit, takes
    value[u] = tanh(bias[u] + sum over its links of w * (a unit's value at the PREVIOUS step, or a port's value NOW))
and then all units swap together. A link whose `from` is the id of an output or hidden unit is a unit link; any other
`from` is a sense port. A link whose `to` is not a unit is left out, as ForCentral leaves it; a weight of 0 is left out
(it reads nothing). Two links with the same ends add. The units start at 0 (a fresh Brain's buffers).

Definitions, per brain.
  outputs, hidden, links     counts of the genome's lists.
  unit links                 links with a unit as `from`, weight not 0. A self-link is a unit link whose `from` is its `to`.
  self-links, big self-links self-links, and those with |w| > 1 (which can latch on their own).
  cycles                     simple cycles in the graph of unit links, of length 1 to 6; a self-link is a cycle of length 1.
  memory length              hold every port at 0 and step 2,000 steps to settle. Then, for each port that has a nonzero
                             link, set that port to 1 for one step (step 0), return it to 0, and step on. At each step k
                             compare every unit with the same brain stepped on from the settled state with no pulse (the
                             reference, which for a brain that settles to a fixed point is simply its settled value).
                             The memory of the port is the last step k >= 0 at which any unit differs from the reference
                             by more than 0.01; 0 means the brain answered at the pulse step only. The brain's memory
                             is the longest over its ports, in steps and in seconds (steps x 0.01). A brain with no
                             unit link has memory 0 by construction (the pulse reaches the units at step 0 and, with
                             nothing carried from step to step, is gone at step 1); the self-test checks the shortcut
                             against the full run. The run stops for a port when its deviation has stayed under 0.01 for
                             300 steps, or at the cap (default 20,000 steps, 200 s).
  latch                      at the cap some unit still differs from the reference by more than 0.01, and the pulsed
                             brain has stopped moving (every unit changes by under 1e-7 a step) and so has the reference:
                             the pulse moved the brain to a different fixed point and it stayed. The units that differ
                             are named. A brain still moving at the cap is reported as `unsettled` (a slow decay, or an
                             oscillation the pulse shifted in phase); its memory is the cap, and it is not a latch.
  grazer                     the body's lineage birth row has atk > 0 and ink > 0 (born mouthed). Every
                             scripts/reads/*.py that counts one (grazer-tally.py, income-split via contact-share.py,
                             genome-diff-read.py, bloom/g2-read.py's trait table) takes atk 1 and ink 1, which are the
                             only values the row carries; this read takes > 0 as the brief says. A body with no birth
                             row in the lineage (a run that resumed another) is `unknown` and counted in neither class.

Arithmetic. float64 here; the farm steps float32 with Math.Tanh. A brain whose pulse lands within float32 rounding of
the 0.01 line, or whose slow decay is near the cap, could be read one step either way. The classification of a latch
versus a slow decay near unity gain could in principle differ.
"""
import argparse
import gzip
import json
import math
import os
import re
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DT = 0.01
TOL = 0.01
SETTLE = 2000
QUIET = 300
MAXCYCLE = 6

DEFAULT_RUNS = [
    ("r64t-t-6202", "runs/r64t-t-6202/2026-10-31-235754-2991f19e", [6000, 12000, 24000]),
    ("r64t-t-6203", "runs/r64t-t-6203/2026-10-31-235754-2991f19e", [6000, 12000, 24000]),
    ("r64s2-d-6202", "runs/r64s2-d-6202/2026-10-30-233542-11de0613", [46500]),
]


# ---------------------------------------------------------------- the network

def build(brain):
    """The brain's units, unit matrix and port vectors, as StepCentral sees them.

    Returns (unit_ids, bias[U], W[U,U] with W[to, from], ports: {port id: vector[U]}, links: unit-link list)."""
    units = [u["id"] for u in brain["outputs"]] + [u["id"] for u in brain["hidden"]]
    at = {}
    for i, uid in enumerate(units):
        at[uid] = i
    n = len(units)
    bias = np.array([u["bias"] for u in brain["outputs"]] + [u["bias"] for u in brain["hidden"]], dtype=np.float64)
    W = np.zeros((n, n))
    ports = {}
    unit_links = []
    for l in brain["links"]:
        w = l["w"]
        if w == 0:
            continue
        if l["to"] not in at:
            continue
        to = at[l["to"]]
        if l["from"] in at:
            W[to, at[l["from"]]] += w
            unit_links.append((at[l["from"]], to, w))
        else:
            ports.setdefault(l["from"], np.zeros(n))[to] += w
    return units, bias, W, ports, unit_links


def count_cycles(n, unit_links, maxlen=MAXCYCLE, limit=1000000):
    """Simple directed cycles of length 1..maxlen in the graph of unit links (a self-link is length 1)."""
    out = [set() for _ in range(n)]
    for a, b, _ in unit_links:
        out[a].add(b)
    total = 0
    for s in range(n):
        # cycles whose smallest node is s
        stack = [(s, [s])]
        while stack:
            node, path = stack.pop()
            for nxt in out[node]:
                if nxt == s:
                    total += 1
                elif nxt > s and nxt not in path and len(path) < maxlen:
                    stack.append((nxt, path + [nxt]))
            if total > limit:
                return total
    return total


def settle(bias, W, steps=SETTLE):
    x = np.zeros(len(bias))
    last = x
    for _ in range(steps):
        last = x
        x = np.tanh(bias + W @ x)
    return x, float(np.max(np.abs(x - last))) if len(x) else 0.0


def memory(bias, W, ports, cap):
    """Run the pulses. Returns (memory steps, latch units (indices) or None, unsettled flag, settle residual, port)."""
    n = len(bias)
    names = list(ports)
    if n == 0 or not names:
        return 0, None, False, 0.0, None
    P = np.array([ports[k] for k in names])            # (p, n)
    x0, resid = settle(bias, W)
    ref = x0.copy()
    X = np.tanh(bias + W @ x0 + P)                     # step 0 with the port at 1
    ref = np.tanh(bias + W @ ref)
    last_dev = np.zeros(len(names), dtype=np.int64)
    quiet = np.zeros(len(names), dtype=np.int64)
    dev0 = np.max(np.abs(X - ref), axis=1)
    quiet[dev0 <= TOL] = 1
    last_dev[dev0 > TOL] = 0
    active = np.ones(len(names), dtype=bool)
    k = 0
    prevX, prevref = X, ref
    while k < cap - 1:
        k += 1
        prevX, prevref = X, ref
        X = np.tanh(bias + X @ W.T)
        ref = np.tanh(bias + W @ ref)
        dev = np.max(np.abs(X - ref), axis=1)
        over = dev > TOL
        last_dev[over] = k
        quiet = np.where(over, 0, quiet + 1)
        if np.all(quiet >= QUIET):
            break
    best = int(np.argmax(last_dev))
    mem = int(last_dev[best])
    if mem >= cap - 1:
        # at the cap: a latch if a port's pulsed state differs and both have stopped moving
        dev = np.abs(X - ref)
        movement = np.max(np.abs(X - prevX), axis=1)
        still = float(np.max(np.abs(ref - prevref))) < 1e-7
        for p in range(len(names)):
            if np.max(dev[p]) > TOL and movement[p] < 1e-7 and still:
                units = [int(i) for i in np.nonzero(dev[p] > TOL)[0]]
                return cap, units, False, resid, names[p]
        return cap, None, True, resid, names[best]
    return mem, None, False, resid, names[best]


def literal_step(bias, incoming, previous, port_value):
    """StepCentral written as it reads, for the self-test and the hand check: incoming[u] is a list of
    (source, w) with a source >= 0 a unit (previous step) and a source < 0 the port ~source (now)."""
    current = []
    for u in range(len(bias)):
        s = bias[u]
        for src, w in incoming[u]:
            s += w * (previous[src] if src >= 0 else port_value[~src])
        current.append(math.tanh(s))
    return current


def literal_network(brain):
    units = [u["id"] for u in brain["outputs"]] + [u["id"] for u in brain["hidden"]]
    at = {uid: i for i, uid in enumerate(units)}
    bias = [u["bias"] for u in brain["outputs"]] + [u["bias"] for u in brain["hidden"]]
    slot = {}
    incoming = [[] for _ in units]
    for l in brain["links"]:
        if l["w"] == 0 or l["to"] not in at:
            continue
        if l["from"] in at:
            src = at[l["from"]]
        else:
            if l["from"] not in slot:
                slot[l["from"]] = len(slot)
            src = ~slot[l["from"]]
        incoming[at[l["to"]]].append((src, l["w"]))
    return units, bias, incoming, slot


# ---------------------------------------------------------------- the census

def analyse(brain, cap):
    units, bias, W, ports, unit_links = build(brain)
    n = len(units)
    selfl = [l for l in unit_links if l[0] == l[1]]
    row = {
        "outputs": len(brain["outputs"]),
        "hidden": len(brain["hidden"]),
        "links": len(brain["links"]),
        "ports": len(ports),
        "unit_links": len(unit_links),
        "self_links": len(selfl),
        "big_self_links": sum(1 for l in selfl if abs(l[2]) > 1),
        "cycles": count_cycles(n, unit_links) if unit_links else 0,
    }
    if not unit_links:
        row.update(mem_steps=0, latch=None, unsettled=False, resid=0.0, port=None)
        return row
    mem, latch, unsettled, resid, port = memory(bias, W, ports, cap)
    row.update(mem_steps=mem, latch=None if latch is None else [units[i] for i in latch],
               unsettled=unsettled, resid=resid, port=port)
    return row


def read_snapshot(run, t):
    path = os.path.join(run, "snapshots", "%09d.jsonl.gz" % t)
    with gzip.open(path, "rt") as f:
        return [json.loads(l)["id"] for l in f if l.strip()]


def read_genomes(run, wanted):
    """One pass over genomes.jsonl.gz, parsing only the rows whose id is wanted."""
    need = set(wanted)
    out = {}
    pat = re.compile(r'\{"id":(\d+),')
    with gzip.open(os.path.join(run, "genomes.jsonl.gz"), "rt") as f:
        for line in f:
            m = pat.match(line)
            if not m or int(m.group(1)) not in need:
                continue
            g = json.loads(line)
            out[g["id"]] = g["brain"]
            if len(out) == len(need):
                break
    return out


def read_births(run):
    """id -> (grazer, parent, src, t) from lineage.jsonl birth rows."""
    births = {}
    with open(os.path.join(run, "lineage.jsonl"), "r", encoding="utf-8") as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            r = json.loads(line)
            births[r["id"]] = (r.get("atk", 0) > 0 and r.get("ink", 0) > 0, r["p"], r.get("src"), r["t"])
    return births


def pct(v, q):
    if not v:
        return float("nan")
    s = sorted(v)
    i = (len(s) - 1) * q
    lo = int(math.floor(i))
    hi = min(lo + 1, len(s) - 1)
    return s[lo] + (s[hi] - s[lo]) * (i - lo)


def table(pops, cap):
    lines = []
    head = ("population", "class", "n", "unit-link%", "cycle%", "latch%", "unsettled%", "mem med s", "mem p90 s",
            "mem max s", "mem>0 %", "hidden mean")
    lines.append("%-26s %-6s %6s %10s %7s %7s %11s %9s %9s %9s %8s %11s" % head)
    for name, rows in pops:
        for cls in ("graze", "rest", "all"):
            sel = [r for r in rows if cls == "all" or r["cls"] == cls]
            if not sel:
                continue
            n = len(sel)
            mem = [r["mem_steps"] * DT for r in sel]
            lines.append("%-26s %-6s %6d %9.1f%% %6.1f%% %6.1f%% %10.1f%% %9.2f %9.2f %9.2f %7.1f%% %11.2f" % (
                name, cls, n,
                100.0 * sum(1 for r in sel if r["unit_links"] > 0) / n,
                100.0 * sum(1 for r in sel if r["cycles"] > 0) / n,
                100.0 * sum(1 for r in sel if r["latch"]) / n,
                100.0 * sum(1 for r in sel if r["unsettled"]) / n,
                pct(mem, 0.5), pct(mem, 0.9), max(mem),
                100.0 * sum(1 for m in mem if m > 0) / n,
                sum(r["hidden"] for r in sel) / n))
    lines.append("")
    lines.append("Five longest-memory grazers per population (latched or unsettled brains sit at the cap of %.0f s and "
                 "lead the list):" % (cap * DT))
    for name, rows in pops:
        gz = [r for r in rows if r["cls"] == "graze"]
        gz.sort(key=lambda r: (-r["mem_steps"], -(1 if r["latch"] else 0), r["id"]))
        if not gz:
            continue
        lines.append("  %s" % name)
        for r in gz[:5]:
            tag = "latch" if r["latch"] else ("unsettled" if r["unsettled"] else "decays")
            lines.append("    id %-7d memory %8.2f s (%s)  hidden %d, unit links %d, cycles %d, self-links %d "
                         "(%d big), ports %d%s" % (
                             r["id"], r["mem_steps"] * DT, tag, r["hidden"], r["unit_links"], r["cycles"],
                             r["self_links"], r["big_self_links"], r["ports"],
                             ", latch units %s" % ",".join(u[:6] for u in r["latch"]) if r["latch"] else ""))
    return lines


def census(runs, cap, out, rows_out):
    cache = {}
    pops = []
    notes = []
    for label, path, times in runs:
        run = path if os.path.isabs(path) else os.path.join(ROOT, path)
        births = read_births(run)
        snaps = {t: read_snapshot(run, t) for t in times}
        founders = {"floor": [i for i, b in births.items() if b[1] < 0 and b[2] == "floor"],
                    "pool+trickle": [i for i, b in births.items() if b[1] < 0 and b[2] in ("pool", "trickle")]}
        has_floor = bool(founders["floor"])
        wanted = set()
        for ids in snaps.values():
            wanted.update(ids)
        if has_floor:
            for ids in founders.values():
                wanted.update(ids)
        else:
            founders = {}
        genomes = read_genomes(run, wanted)
        sets = [("%s @%d" % (label, t), ids) for t, ids in snaps.items()]
        sets += [("%s founders %s" % (label, k), ids) for k, ids in founders.items()]
        for name, ids in sets:
            rows = []
            unknown = missing = 0
            for i in ids:
                if i not in genomes:
                    missing += 1
                    continue
                key = (label, i)
                if key not in cache:
                    cache[key] = analyse(genomes[i], cap)
                b = births.get(i)
                if b is None:
                    unknown += 1
                    continue
                r = dict(cache[key])
                r["id"] = i
                r["cls"] = "graze" if b[0] else "rest"
                r["pop"] = name
                rows.append(r)
            pops.append((name, rows))
            if unknown or missing:
                notes.append("%s: %d bodies with no birth row, %d with no genome row (left out)" % (name, unknown, missing))
            if rows_out:
                for r in rows:
                    rows_out.write(json.dumps(r) + "\n")
            print("done", name, len(rows), file=sys.stderr, flush=True)
    lines = ["brain-memory census; cap %d steps (%.0f s), tolerance %.2f, settle %d steps" % (cap, cap * DT, TOL, SETTLE),
             "classes: graze = born atk>0 and ink>0; rest = every other body with a birth row; all = both", ""]
    lines += table(pops, cap)
    if notes:
        lines += [""] + notes
    text = "\n".join(lines) + "\n"
    with open(out, "w", encoding="utf-8") as f:
        f.write(text)
    print(text)


# ---------------------------------------------------------------- checks

def self_test():
    rng = np.random.default_rng(1)
    bad = 0
    for trial in range(60):
        nh = int(rng.integers(0, 5))
        no = int(rng.integers(1, 4))
        ids = ["o%d" % i for i in range(no)] + ["h%d" % i for i in range(nh)]
        brain = {"outputs": [{"id": i, "bias": float(rng.normal(0, 0.4))} for i in ids[:no]],
                 "hidden": [{"id": i, "bias": float(rng.normal(0, 0.4))} for i in ids[no:]],
                 "links": []}
        pn = ["p%d" % i for i in range(int(rng.integers(1, 6)))]
        for u in ids:
            for p in pn:
                if rng.random() < 0.6:
                    brain["links"].append({"from": p, "to": u, "w": float(rng.normal(0, 0.8))})
        for _ in range(int(rng.integers(0, 6))):
            brain["links"].append({"from": ids[int(rng.integers(len(ids)))], "to": ids[int(rng.integers(len(ids)))],
                                   "w": float(rng.normal(0, 1.1))})
        row = analyse(brain, 3000)
        # literal run for each port
        units, bias, incoming, slot = literal_network(brain)
        zero = [0.0] * len(slot)
        x = [0.0] * len(units)
        for _ in range(SETTLE):
            x = literal_step(bias, incoming, x, zero)
        worst = 0
        for p, s in slot.items():
            pv = [0.0] * len(slot)
            pv[s] = 1.0
            xa = literal_step(bias, incoming, x, pv)
            xr = literal_step(bias, incoming, x, zero)
            last = 0
            for k in range(1, 3000):
                xa = literal_step(bias, incoming, xa, zero)
                xr = literal_step(bias, incoming, xr, zero)
                if max(abs(a - b) for a, b in zip(xa, xr)) > TOL:
                    last = k
            worst = max(worst, last)
        if row["mem_steps"] != worst and not (row["mem_steps"] >= 2999 and worst >= 2998):
            bad += 1
            print("MISMATCH trial", trial, row["mem_steps"], worst)
        if row["unit_links"] == 0 and worst != 0:
            bad += 1
            print("SHORTCUT WRONG", trial, worst)
    print("self-test: %d mismatches in 60 random brains" % bad)
    return bad


def hand_check(path, bid):
    with gzip.open(path, "rt") as f:
        for line in f:
            if line.startswith('{"id":%d,' % bid):
                brain = json.loads(line)["brain"]
                break
        else:
            sys.exit("id not found")
    units, bias, incoming, slot = literal_network(brain)
    print("body %d: %d outputs, %d hidden, %d links; %d unit links, %d ports read" % (
        bid, len(brain["outputs"]), len(brain["hidden"]), len(brain["links"]),
        sum(1 for l in brain["links"] if l["from"] in units), len(slot)))
    # a made-up input: port j reads 0.1*((j % 5) - 2) + 0.05*step
    def ports_at(step):
        return [0.1 * ((j % 5) - 2) + 0.05 * step for j in range(len(slot))]
    names = ["out:" + u[:6] for u in units[:len(brain["outputs"])]] + ["hid:" + u[:6] for u in units[len(brain["outputs"]):]]
    print("unit order:", names)
    prev = [0.0] * len(units)
    for step in range(5):
        pv = ports_at(step)
        cur = literal_step(bias, incoming, prev, pv)
        print("step %d  ports j=0..4: %s" % (step, " ".join("%+.3f" % v for v in pv[:5])))
        print("        units:", " ".join("%+.6f" % v for v in cur))
        if step in (0, 1):
            # one unit worked by hand: the last output (it reads the three hidden units)
            u = len(brain["outputs"]) - 1
            terms = [("bias", bias[u])]
            for src, w in incoming[u]:
                v = prev[src] if src >= 0 else pv[~src]
                terms.append(("%s%s w=%+.5f x=%+.5f" % ("unit " if src >= 0 else "port ", src if src >= 0 else ~src, w, v), w * v))
            s = sum(t for _, t in terms)
            unit_terms = [(n, t) for n, t in terms if n.startswith("unit")]
            print("        by hand, %s: sum = bias %+.6f + ports %+.6f + unit links %+.6f = %+.6f, tanh = %+.6f" % (
                names[u], bias[u], sum(t for n, t in terms if n.startswith("port")), sum(t for _, t in unit_terms), s,
                math.tanh(s)))
            for n, t in unit_terms:
                print("          %s -> %+.6f" % (n, t))
        prev = cur
    # the numpy batch rule on the same input
    u_, b_, W_, p_, _ = build(brain)
    order = sorted(slot, key=lambda k: slot[k])
    x = np.zeros(len(units))
    for step in range(5):
        pv = ports_at(step)
        inp = np.zeros(len(units))
        for k, name in enumerate(order):
            inp += p_.get(name, 0) * pv[k] if name in p_ else 0
        x = np.tanh(b_ + W_ @ x + inp)
    print("numpy rule, step 4 units:", " ".join("%+.6f" % v for v in x))
    print("literal rule, step 4 units:", " ".join("%+.6f" % v for v in prev))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="*")
    ap.add_argument("--out", default=os.path.join(ROOT, "scratch", "brain-census", "census.txt"))
    ap.add_argument("--rows")
    ap.add_argument("--cap", type=int, default=20000)
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--hand-check", nargs=2, metavar=("GENOMES", "ID"))
    a = ap.parse_args()
    if a.self_test:
        sys.exit(1 if self_test() else 0)
    if a.hand_check:
        hand_check(a.hand_check[0], int(a.hand_check[1]))
        return
    runs = DEFAULT_RUNS
    if a.runs:
        runs = []
        for s in a.runs:
            label, rest = s.split("=", 1)
            path, ts = rest.rsplit("@", 1)
            runs.append((label, path, [int(x) for x in ts.split(",")]))
    rows_out = open(a.rows, "w", encoding="utf-8") if a.rows else None
    census(runs, a.cap, a.out, rows_out)
    if rows_out:
        rows_out.close()


if __name__ == "__main__":
    main()
