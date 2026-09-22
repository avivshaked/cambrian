#!/usr/bin/env python3
"""Read-only extraction for round 43 (logbook/0111, clauses G2, G3, G4, G6, G8, G9, G10).

Adapted from logbook/specs/r42-read/probe-and-sieve.py. The farm's stats fields for the
contact/overlap census are renamed: overlapPairs, overlapPairsPerStep, overlapPairsJointed,
overlapPairsHeld, overlapBodies; report columns `overlaps`, `ovl/body`, `ovl jnt %`,
`ovl held %`.

Run from anywhere; writes clauses.tsv beside itself. Touches nothing but reads.
"""
import json
import os
import re
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DOTNET = r"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe"
DLL = os.path.join(REPO, "artifacts", "Evosim.Overlap", "bin", "Release", "net8.0", "Evosim.Overlap.dll")
SEEDS = [1, 2, 3]
TIMES = [5000, 15000, 30000]
BUDGET_JOULES = 1500 * 100.0   # matterBudgetUnits x joulesPerUnit, both read from config


def run_dir(seed):
    base = os.path.join(REPO, "runs", "r43-s%d" % seed)
    dirs = [d for d in sorted(os.listdir(base)) if os.path.isdir(os.path.join(base, d))]
    if len(dirs) != 1:
        raise SystemExit("expected one run directory for seed %d, saw %r" % (seed, dirs))
    return os.path.join(base, dirs[0])


def stats_rows(d):
    with open(os.path.join(d, "stats.jsonl"), encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def probe(path):
    out = subprocess.run([DOTNET, DLL, path], capture_output=True, text=True).stdout
    creatures = hist = pairs = bodies = None
    for line in out.splitlines():
        s = line.strip()
        if s.startswith("creatures ") and "refused" in s:
            creatures = int(s.split()[1].rstrip(","))
        elif s.startswith("parts histogram:"):
            hist = {}
            for item in s.split(":", 1)[1].split(","):
                k, v = item.split("->")
                hist[int(k)] = int(v)
        elif s.startswith("total self-overlapping pairs:"):
            pairs = int(s.split(":")[1].split()[0])
        elif s.startswith("creatures with >=1 self-overlapping pair:"):
            bodies = int(s.split(":")[1].split()[0])
    return creatures, hist, pairs, bodies


def median_from_hist(hist, n):
    keys = sorted(hist)

    def at(rank):
        c = 0
        for k in keys:
            c += hist[k]
            if c >= rank:
                return k
        return keys[-1]

    if n % 2:
        return float(at((n + 1) // 2))
    return (at(n // 2) + at(n // 2 + 1)) / 2.0


def footer(seed):
    """x real time and wall minutes from the report's own footer line."""
    with open(os.path.join(REPO, "runs", "r43-s%d.md" % seed), encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"([\d.]+) min wall clock \(([\d.]+)x real time\)", text)
    return (float(m.group(1)), float(m.group(2))) if m else (None, None)


def main():
    rows = []
    for seed in SEEDS:
        d = run_dir(seed)
        st = stats_rows(d)
        by_t = {r["t"]: r for r in st}
        ts = sorted(by_t)
        cfg = json.load(open(os.path.join(d, "config.json"), encoding="utf-8"))
        budget = cfg["world"]["matterBudgetUnits"] * cfg["world"]["joulesPerUnit"]
        assert abs(budget - BUDGET_JOULES) < 1e-9, budget

        # G2 -- the books
        amax = max(st, key=lambda r: abs(r["auditResidual"]))
        rows.append(dict(clause="G2", seed=seed,
                         audit_absmax=abs(amax["auditResidual"]), audit_absmax_t=amax["t"],
                         matter_resid_30k=by_t[30000]["matterResidual"],
                         matter_resid_absmax=max(abs(r["matterResidual"]) for r in st),
                         diverged_total=st[-1]["diverged"]))

        # G3 -- the pace, from the report footer
        wall, xreal = footer(seed)
        rows.append(dict(clause="G3", seed=seed, wall_minutes=wall, x_real_time=xreal))

        # G4 -- treadmill and larder
        for t in (15000, 30000):
            r = by_t[t]
            rows.append(dict(clause="G4", seed=seed, t=t,
                             upt_lim_pct=100.0 * r["uptakeLimitedShare"],
                             detritus_joules=r["detritusJoules"],
                             snow_share_of_budget=r["detritusJoules"] / budget))

        # G6 -- joints
        peak = max(st, key=lambda r: r["jointedInherited"])
        end = by_t[30000]["jointedInherited"]
        peak5_20 = max((r for r in st if 5000 <= r["t"] <= 20000),
                       key=lambda r: r["jointedInherited"])
        rows.append(dict(clause="G6", seed=seed,
                         jnt_inh_peak=peak["jointedInherited"], jnt_inh_peak_t=peak["t"],
                         jnt_inh_peak_in_5k_20k=peak5_20["jointedInherited"],
                         jnt_inh_peak_in_5k_20k_t=peak5_20["t"],
                         jnt_inh_30k=end,
                         end_over_peak=end / peak["jointedInherited"] if peak["jointedInherited"] else float("nan"),
                         jointed_peak=max(r["jointed"] for r in st),
                         jointed_30k=by_t[30000]["jointed"]))

        # G8 -- the sieve, cumulative fields differenced over the three intervals
        for lo, hi in [(0, 5000), (5000, 15000), (15000, 30000)]:
            a = by_t[lo] if lo else None
            b = by_t[hi]
            bb = b["births"] - (a["births"] if a else 0)
            so = b["selfOverlapStillbirths"] - (a["selfOverlapStillbirths"] if a else 0)
            sb = b["stillbirths"] - (a["stillbirths"] if a else 0)
            rows.append(dict(clause="G8", seed=seed, t=hi, interval="%d-%d" % (lo, hi),
                             births_win=bb, self_stillb_win=so, stillb_win=sb,
                             self_stillb_share_pct=(100.0 * so / bb) if bb else float("nan")))
        last_floor = max((r["t"] for r in st if r["floorSpawnsWindow"] > 0), default=None)
        rows.append(dict(clause="G8-floor", seed=seed, last_floor_t=last_floor,
                         floor_spawns_total=st[-1]["floorSpawns"]))

        # G9 -- the overlap probe on the snapshots
        for t in TIMES:
            snap = os.path.join(d, "snapshots", "%09d.jsonl" % t)
            n, hist, pairs, bodies = probe(snap)
            ge8 = sum(v for k, v in hist.items() if k >= 8)
            rows.append(dict(clause="G9", seed=seed, t=t, alive=n,
                             at16=hist.get(16, 0), ge8=ge8, ge8_pct=100.0 * ge8 / n,
                             median_parts=median_from_hist(hist, n), max_parts=max(hist),
                             pairs=pairs, pairs_per_body=pairs / n, bodies_overlapping=bodies,
                             hist=";".join("%d->%d" % (k, hist[k]) for k in sorted(hist))))

        # G10 -- the overlap census. `overlaps` is the window's pairs per physics step
        # (overlapPairsPerStep), `ovl/body` that over alive; the two shares are the
        # cumulative counters differenced over the same 10 s window, which is what the
        # report's columns print.
        for t in TIMES:
            i = ts.index(t)
            r, prev = by_t[t], by_t[ts[i - 1]]
            dp = r["overlapPairs"] - prev["overlapPairs"]
            rows.append(dict(clause="G10", seed=seed, t=t, alive=r["alive"],
                             overlaps_per_step=r["overlapPairsPerStep"],
                             ovl_per_body=r["overlapPairsPerStep"] / r["alive"] if r["alive"] else float("nan"),
                             ovl_jnt_pct=100.0 * (r["overlapPairsJointed"] - prev["overlapPairsJointed"]) / dp if dp else float("nan"),
                             ovl_held_pct=100.0 * (r["overlapPairsHeld"] - prev["overlapPairsHeld"]) / dp if dp else float("nan"),
                             ovl_pairs_cum=r["overlapPairs"],
                             ovl_jnt_cum_pct=100.0 * r["overlapPairsJointed"] / r["overlapPairs"] if r["overlapPairs"] else float("nan")))

    cols = []
    for r in rows:
        for k in r:
            if k not in cols:
                cols.append(k)
    out = os.path.join(HERE, "clauses.tsv")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(cols) + "\n")
        for r in rows:
            f.write("\t".join("" if k not in r else str(r[k]) for k in cols) + "\n")
    print("wrote", out)
    for r in rows:
        print(r)


if __name__ == "__main__":
    main()
