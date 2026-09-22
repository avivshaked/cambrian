#!/usr/bin/env python3
"""Read-only extraction for round 42: overlap probe (F10/F12), self-overlap sieve
(F11), inherited-absorptive eaters (F5/F6) and the reserve margin (F2).

Run from the repository root. Writes probe-and-sieve.tsv beside itself.
The overlap-probe numbers are pasted in from the dll's own output (the histogram
lines), so this script does the arithmetic and none of the geometry.
"""
import json
import os
import subprocess
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DOTNET = r"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe"
DLL = os.path.join(REPO, "artifacts", "Evosim.Overlap", "bin", "Release", "net8.0", "Evosim.Overlap.dll")
SEEDS = [1, 2, 3, 4, 5]
TIMES = [5000, 15000, 30000]


def run_dir(seed):
    base = os.path.join(REPO, "runs", "r42-s%d" % seed)
    for name in sorted(os.listdir(base)):
        full = os.path.join(base, name)
        if os.path.isdir(full):
            return full
    raise SystemExit("no run directory for seed %d" % seed)


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
    # lower/upper order statistics of the n values
    def at(rank):  # rank is 1-based
        c = 0
        for k in keys:
            c += hist[k]
            if c >= rank:
                return k
        return keys[-1]
    if n % 2:
        return float(at((n + 1) // 2))
    return (at(n // 2) + at(n // 2 + 1)) / 2.0


def stats_rows(d):
    with open(os.path.join(d, "stats.jsonl"), encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def main():
    rows = []
    for seed in SEEDS:
        d = run_dir(seed)
        st = stats_rows(d)
        by_t = {r["t"]: r for r in st}
        ts = sorted(by_t)

        # F10 / F12
        for t in TIMES:
            snap = os.path.join(d, "snapshots", "%09d.jsonl" % t)
            n, hist, pairs, bodies = probe(snap)
            ge8 = sum(v for k, v in hist.items() if k >= 8)
            rows.append(dict(
                clause="F10/F12", seed=seed, t=t, alive=n,
                at16=hist.get(16, 0), ge8=ge8, ge8_pct=100.0 * ge8 / n,
                pairs=pairs, pairs_per_body=pairs / n, bodies_overlapping=bodies,
                median_parts=median_from_hist(hist, n),
                max_parts=max(hist), hist=";".join("%d->%d" % (k, hist[k]) for k in sorted(hist)),
            ))

        # F11 — cumulative fields, differenced over one 100 s sample window
        for t in TIMES:
            r = by_t[t]
            prev = by_t[ts[ts.index(t) - 1]]
            b = r["births"] - prev["births"]
            so = r["selfOverlapStillbirths"] - prev["selfOverlapStillbirths"]
            sb = r["stillbirths"] - prev["stillbirths"]
            rows.append(dict(
                clause="F11", seed=seed, t=t, births_win=b, self_stillb_win=so,
                stillb_win=sb, share_pct=(100.0 * so / b) if b else float("nan"),
                self_stillb_cum=r["selfOverlapStillbirths"], births_cum=r["births"],
                share_cum_pct=100.0 * r["selfOverlapStillbirths"] / r["births"] if r["births"] else float("nan"),
            ))
        # F11 — the same two cumulative fields differenced over the three long intervals
        for lo, hi in [(0, 5000), (5000, 15000), (15000, 30000)]:
            a = by_t[lo] if lo else None
            b = by_t[hi]
            bb = b["births"] - (a["births"] if a else 0)
            so = b["selfOverlapStillbirths"] - (a["selfOverlapStillbirths"] if a else 0)
            rows.append(dict(clause="F11-interval", seed=seed, t=hi, interval="%d-%d" % (lo, hi),
                             births_win=bb, self_stillb_win=so,
                             share_pct=(100.0 * so / bb) if bb else float("nan")))

        last_floor = max((r["t"] for r in st if r["floorSpawnsWindow"] > 0), default=None)
        rows.append(dict(clause="F11-floor", seed=seed, last_floor_t=last_floor,
                         floor_spawns_total=st[-1]["floorSpawns"]))

        # F5 / F6
        after = [r for r in st if r["t"] > 5000]
        peak = max(after, key=lambda r: r["absorptiveInherited"])
        rows.append(dict(clause="F5/F6", seed=seed,
                         abs_inh_max_after5k=peak["absorptiveInherited"], at_t=peak["t"],
                         abs_inh_at30k=by_t[30000]["absorptiveInherited"],
                         exceeded_150=peak["absorptiveInherited"] > 150,
                         abs_inh_max_whole_run=max(r["absorptiveInherited"] for r in st)))

        # F2
        rows.append(dict(clause="F2", seed=seed,
                         mean_reserve_margin_30k=by_t[30000]["meanReserveMargin"]))

    cols = []
    for r in rows:
        for k in r:
            if k not in cols:
                cols.append(k)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "probe-and-sieve.tsv")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(cols) + "\n")
        for r in rows:
            f.write("\t".join("" if k not in r else str(r[k]) for k in cols) + "\n")
    print("wrote", out)
    for r in rows:
        print(r)


if __name__ == "__main__":
    main()
