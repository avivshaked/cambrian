"""The reader for round 55's own clauses (logbook/0134), built from the smoke's reader.

I1: at 30,000 s, at least 5% of the living descend from a nursery founder, in 2 of the 2 inoculated seeds.
I2: the nursery run whose founders' descendants are most numerous among the living at 30,000 s is the same
    in both inoculated seeds (read only when I1 holds in both).
I3: the jointed share of the living at 30,000 s is at least twice the control seed's, in each inoculated seed.
Readings: where the nursery's lines came from (floor or trickle founders), each run and body's founders,
children and living at 5,000, 15,000 and 30,000 s; the living's links and hidden units (bodies with
outputs only: a rigid body's brain is empty) by the run of their root; the disk per simulated hour. B1 and round 54's clauses are read by
round 54's reader (scripts/reads/r54-read.py on round-54).

Usage: python scripts/reads/r55-read.py [--runs-root R] [--control r55-s1] [--inoculated r55-s2 r55-s3]
"""
import argparse
import csv
import glob
import gzip
import json
import os
from collections import Counter, defaultdict

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
END, TIMES, FLOOR_CLOSES = 30000.0, (5000.0, 15000.0, 30000.0), 3000.0
I1_SHARE, I3_RATIO = 0.05, 2.0


def run_dir(root, arm):
    dirs = sorted(d for d in glob.glob(os.path.join(root, arm, "*")) if os.path.isdir(d))
    return dirs[-1] if dirs else None


def rows(path):
    """A JSONL file's complete rows; a live run's last line may be half written and is skipped."""
    for l in open(path, encoding="utf-8"):
        if l.endswith("\n") and l.strip():
            yield json.loads(l)


def read(d, upto):
    lineage = list(rows(os.path.join(d, "lineage.jsonl")))
    births = {r["id"]: r for r in lineage if r.get("e") == "b" and r["t"] <= upto}
    death_t = {r["id"]: r["t"] for r in lineage if r.get("e") == "d"}
    ip = os.path.join(d, "pool", "index.tsv")
    index = list(csv.DictReader(open(ip, encoding="utf-8"), delimiter="\t")) if os.path.exists(ip) else []
    return births, death_t, index


def root_of(births):
    memo = {}

    def root(i):
        path = []
        while i not in memo and births.get(i, {}).get("p", -1) >= 0:
            path.append(i)
            i = births[i]["p"]
        r = memo.get(i, i)
        for j in path:
            memo[j] = r
        memo[i] = r
        return r
    return root


def origin(b, index):
    if b.get("src") != "pool" or not index:
        return None
    o = index[b["pool"]]
    return o if o.get("origin") == "nursery" else None


def at(births, death_t, index, t):
    alive = [i for i, b in births.items() if b["t"] <= t and death_t.get(i, float("inf")) > t]
    root = root_of(births)
    by_run, by_cell, floor_trickle = Counter(), Counter(), Counter()
    nursery = 0
    for i in alive:
        r = births[root(i)]
        o = origin(r, index)
        if o is None:
            continue
        nursery += 1
        by_run[o["run"]] += 1
        by_cell[(o["run"], o["body"])] += 1
        floor_trickle["floor" if r["t"] < FLOOR_CLOSES else "trickle"] += 1
    return alive, nursery, by_run, by_cell, floor_trickle


def stats_at(d, t):
    best = None
    for r in rows(os.path.join(d, "stats.jsonl")):
        if r["t"] <= t:
            best = r
    return best


def brains(d, ids):
    want, out = set(ids), {}
    with gzip.open(os.path.join(d, "genomes.jsonl.gz"), "rt", encoding="utf-8") as f:
        while True:
            # A live run's gzip stream ends mid-block; read what is complete and stop there.
            try:
                line = f.readline()
            except (EOFError, OSError):
                break
            if not line.endswith("\n"):
                break
            g = json.loads(line)
            b = g.get("brain")
            if g["id"] in want and b is not None and b.get("outputs"):
                out[g["id"]] = (len(b.get("links", [])), len(b.get("hidden", [])))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"))
    ap.add_argument("--control", default="r55-s1")
    ap.add_argument("--inoculated", nargs="+", default=["r55-s2", "r55-s3"])
    ap.add_argument("--end", type=float, default=END)
    a = ap.parse_args()

    jointed_share, winners, i1 = {}, {}, {}
    for arm in [a.control] + a.inoculated:
        d = run_dir(a.runs_root, arm)
        if d is None:
            print(f"{arm}: absent")
            continue
        births, death_t, index = read(d, a.end)
        s = stats_at(d, a.end)
        jointed_share[arm] = s["jointed"] / s["alive"] if s and s["alive"] else None
        print(f"\n== {arm} ({d}); last stats row t={s['t'] if s else None}, jointed share {jointed_share[arm]}")
        gz, lin = os.path.getsize(os.path.join(d, "genomes.jsonl.gz")), os.path.getsize(os.path.join(d, "lineage.jsonl"))
        hours = (s["t"] if s else a.end) / 3600
        print(f"disk: genomes {gz / 1e6:.1f} MB, lineage {lin / 1e6:.1f} MB, {(gz + lin) / 1e6 / hours:.1f} MB per simulated hour")
        for t in sorted({x for x in TIMES if x <= a.end} | {a.end}):
            alive, nursery, by_run, by_cell, ft = at(births, death_t, index, t)
            share = nursery / len(alive) if alive else 0.0
            print(f"t={t:.0f}: alive {len(alive)}, nursery-rooted {nursery} ({share:.3f}); from floor {ft['floor']}, trickle {ft['trickle']}")
            for run, n in by_run.most_common():
                print(f"   {run}: {n}  " + ", ".join(f"body {b} {m}" for (r_, b), m in sorted(by_cell.items()) if r_ == run))
            if t == a.end and arm in a.inoculated:
                i1[arm] = share
                winners[arm] = by_run.most_common(1)[0][0] if by_run else None
                sample = alive[:2000]
                br = brains(d, sample)
                roots = root_of(births)
                groups = defaultdict(list)
                for i in sample:
                    o = origin(births[roots(i)], index)
                    if i in br:
                        groups[o["run"] if o else "not nursery"].append(br[i])
                for k, v in sorted(groups.items()):
                    print(f"   brains ({k}, {len(v)} of the living sampled): links {sum(x[0] for x in v) / len(v):.1f}, "
                          f"hidden {sum(x[1] for x in v) / len(v):.2f}")
        founders = Counter((origin(b, index) or {}).get("run", b.get("src", "?")) for b in births.values() if b["p"] < 0)
        print(f"founders by origin: {dict(founders)}")

    print("\n== clauses")
    if i1:
        held = [arm for arm, s in i1.items() if s >= I1_SHARE]
        print(f"I1: nursery-rooted share at {a.end:.0f} s " + ", ".join(f"{k} {v:.3f}" for k, v in i1.items())
              + f" -> {'held' if len(held) == len(a.inoculated) else 'failed'} ({len(held)} of {len(a.inoculated)})")
        if len(held) == len(a.inoculated):
            same = len(set(winners.values())) == 1
            print(f"I2: the most numerous run by seed {winners} -> {'held' if same else 'failed'}")
        else:
            print(f"I2: not read (I1 did not hold in both); the most numerous run by seed {winners}")
    c = jointed_share.get(a.control)
    if c is not None:
        for arm in a.inoculated:
            j = jointed_share.get(arm)
            if j is not None:
                print(f"I3 {arm}: jointed share {j:.3f} against the control's {c:.3f} -> "
                      f"{'held' if c > 0 and j >= I3_RATIO * c or c == 0 and j > 0 else 'failed'}")


if __name__ == "__main__":
    main()
