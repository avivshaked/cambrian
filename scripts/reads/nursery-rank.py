"""How far a change to the nursery's world reorders the same genomes (proposals/propose-speed-exploration.md).

Usage:
  python scripts/reads/nursery-rank.py floor <run of 16 draws> <run of 32 draws>
  python scripts/reads/nursery-rank.py pair <today's run> <the option's run> [<floor rho> <floor elites>]

Both modes read generation 0 of each run's tank-scores.jsonl, whose members are the same genomes in
the same order when two runs share a seed. A member's value is the mean of its episodes' rewards.

floor: draw seeds do not depend on how many draws a run makes, so a 32-draw run holds the 16-draw
run's draws and 16 more. Each member's mean over the new 16 is (n32 * v32 - n16 * v16) / (n32 - n16).
The floor is the rank correlation between the first 16 draws and the new 16, and how many of the
elites (the recipe's search.elites) stay elites: what luck alone does to the order.

pair: the same genomes, the same draws, today's world against the option's. Given the floor's two
numbers, prints the verdict the proposal writes down before the trial: within noise, small or changes
the experiment.

Exits 0 with a reading, 3 when it could not read (a missing file, members that do not line up).
"""
import json
import os
import sys

COULD_NOT_RUN = 3


def gen0(run):
    path = os.path.join(run, "tank-scores.jsonl")
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.strip():
                row = json.loads(line)
                if row["gen"] == 0:
                    return row["members"]
    raise ValueError(f"no generation 0 in {path}")


def elites(run):
    with open(os.path.join(run, "recipe.json"), encoding="utf-8") as f:
        return int(json.load(f)["search"]["elites"])


def ranks(values):
    order = sorted(range(len(values)), key=lambda i: values[i])
    r = [0.0] * len(values)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and values[order[j + 1]] == values[order[i]]:
            j += 1
        for k in range(i, j + 1):
            r[order[k]] = (i + j) / 2.0 + 1.0
        i = j + 1
    return r


def spearman(a, b):
    ra, rb = ranks(a), ranks(b)
    n = len(a)
    ma, mb = sum(ra) / n, sum(rb) / n
    cov = sum((x - ma) * (y - mb) for x, y in zip(ra, rb))
    va = sum((x - ma) ** 2 for x in ra)
    vb = sum((y - mb) ** 2 for y in rb)
    return cov / (va * vb) ** 0.5


def kept(a, b, k):
    top = lambda v: set(sorted(range(len(v)), key=lambda i: -v[i])[:k])
    return len(top(a) & top(b))


def lined_up(m1, m2):
    if len(m1) != len(m2):
        raise ValueError(f"{len(m1)} members against {len(m2)}")
    for i, (x, y) in enumerate(zip(m1, m2)):
        if x["body"] != y["body"] or x["born"] != y["born"]:
            raise ValueError(f"member {i} is body {x['body']} born {x['born']} in one run and body {y['body']} born {y['born']} in the other")


def usable(pairs):
    # A member that never landed has no value; it drops out of both sides.
    return [(a, b) for a, b in pairs if a is not None and b is not None]


def floor(run16, run32):
    m16, m32 = gen0(run16), gen0(run32)
    lined_up(m16, m32)
    pairs = []
    for x, y in zip(m16, m32):
        n16, n32 = x["episodes"], y["episodes"]
        if n32 <= n16 or x["value"] is None or y["value"] is None:
            pairs.append((None, None))
            continue
        pairs.append((x["value"], (n32 * y["value"] - n16 * x["value"]) / (n32 - n16)))
    pairs = usable(pairs)
    a, b = [p[0] for p in pairs], [p[1] for p in pairs]
    k = elites(run16)
    rho, kk = spearman(a, b), kept(a, b, k)
    print(f"floor: {len(pairs)} members; rank correlation, draws 0-15 against 16-31: {rho:.3f}; elites kept: {kk} of {k}")
    return 0


def pair(today, option, floor_rho=None, floor_kept=None):
    mt, mo = gen0(today), gen0(option)
    lined_up(mt, mo)
    pairs = usable([(x["value"], y["value"]) for x, y in zip(mt, mo)])
    a, b = [p[0] for p in pairs], [p[1] for p in pairs]
    k = elites(today)
    rho, kk = spearman(a, b), kept(a, b, k)
    mean = lambda v: sum(v) / len(v)
    print(f"pair: {len(pairs)} members; rank correlation, today against the option: {rho:.3f}; elites kept: {kk} of {k}; "
          f"mean score {mean(a):.1f} against {mean(b):.1f}")
    if floor_rho is not None:
        if rho >= floor_rho and kk >= floor_kept:
            verdict = "within noise"
        elif 1.0 - rho <= 1.5 * (1.0 - floor_rho):
            verdict = "small: to the owner with the numbers"
        else:
            verdict = "changes the experiment"
        print(f"verdict against the floor (rank correlation {floor_rho:.3f}, elites kept {floor_kept}): {verdict}")
    return 0


def main(argv):
    try:
        if len(argv) == 4 and argv[1] == "floor":
            return floor(argv[2], argv[3])
        if len(argv) in (4, 6) and argv[1] == "pair":
            if len(argv) == 6:
                return pair(argv[2], argv[3], float(argv[4]), int(argv[5]))
            return pair(argv[2], argv[3])
        print(__doc__)
        return COULD_NOT_RUN
    except (OSError, ValueError, KeyError) as e:
        print(f"COULD-NOT-RUN: {e}")
        return COULD_NOT_RUN


if __name__ == "__main__":
    sys.exit(main(sys.argv))
