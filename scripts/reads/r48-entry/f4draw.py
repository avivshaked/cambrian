"""Round 48's F4 witness: how much of its own 1 m snow cell a pool founder draws in a step.

For each pool founder (a founder row with `src` "pool" in lineage.jsonl), its first
absorptive.jsonl row gives `densityHere`, the edible snow density of the cell it was priced
against (World's appetite pass reads it before the take), and `foodW`, the food income per
second. The pool draw is at least the food income, since the rest of it is waste
(`EnergyLedger.Wasted`). So one 0.5 s metabolic step draws at least `foodW * 0.5` joules from
a cell holding `densityHere * cell^3` joules, and the ratio is a lower bound on the share of the
cell a founder empties per step. The cell is `fieldCellMetres` from the run's config.

It also prints, over each founder's first eight rows, the second and fourth reading against the
first, which is how fast the reading keeps falling once it is already the refill's.

    python scripts/reads/r48-entry/f4draw.py r48-s1 r48-s3
"""
import glob
import json
import os
import statistics
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


def run_dir(arm):
    runs = sorted(glob.glob(os.path.join(REPO, "runs", arm, "*", "run.json")))
    if not runs:
        raise SystemExit("no run under runs/%s" % arm)
    return os.path.dirname(runs[-1])


def config_value(run, name):
    with open(os.path.join(run, "config.json"), encoding="utf-8") as f:
        text = f.read()
    k = text.find('"%s"' % name)
    if k < 0:
        raise SystemExit("%s: no %s in config.json" % (run, name))
    rest = text[text.find(":", k) + 1:]
    return float(rest.split(",")[0].split("\n")[0].strip())


def quantile(xs, p):
    return xs[min(len(xs) - 1, int(p * len(xs)))]


def main(arms):
    for arm in arms:
        run = run_dir(arm)
        cell = config_value(run, "fieldCellMetres")
        step = 0.5

        pool = set()
        with open(os.path.join(run, "lineage.jsonl"), encoding="utf-8") as f:
            for line in f:
                if '"src":"pool"' in line:
                    pool.add(json.loads(line)["id"])

        rows = {}
        with open(os.path.join(run, "absorptive.jsonl"), encoding="utf-8") as f:
            for line in f:
                k = line.find('"id":')
                if k < 0:
                    continue
                i = int(line[k + 5:line.find(",", k)])
                if i in pool and len(rows.setdefault(i, [])) < 8:
                    rows[i].append(json.loads(line))

        shares, second, fourth = [], [], []
        for rs in rows.values():
            d0 = rs[0]["densityHere"]
            if not d0 > 0:
                continue
            shares.append(rs[0]["foodW"] * step / (d0 * cell ** 3))
            if len(rs) > 1:
                second.append(rs[1]["densityHere"] / d0)
            if len(rs) > 3:
                fourth.append(rs[3]["densityHere"] / d0)
        shares.sort()

        print("%s  cell %g m  pool founders %d, read %d" % (arm, cell, len(pool), len(shares)))
        print("  share of the cell drawn per %.1f s step, at least: min %.2f  q25 %.2f  median %.2f"
              "  q75 %.2f  max %.2f" % (step, shares[0], quantile(shares, 0.25),
                                        statistics.median(shares), quantile(shares, 0.75), shares[-1]))
        print("  left of the landing stock after ten steps with no refill, at the median share: %.1e"
              % ((1 - statistics.median(shares)) ** 10))
        print("  reading 2 over reading 1: median %.2f (n %d); reading 4 over reading 1: median %.2f (n %d)"
              % (statistics.median(second), len(second), statistics.median(fourth), len(fourth)))


if __name__ == "__main__":
    main(sys.argv[1:] or ["r48-s1", "r48-s2", "r48-s3"])
