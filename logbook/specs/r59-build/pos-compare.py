"""Two runs' positions.jsonl.gz at the same sample times: per body id, how far apart they are, and
the deepest bodies on each side (the ones that can be at the bed).

Usage: python pos-compare.py <runA dir> <runB dir> [t,t,...]
"""
import gzip
import json
import math
import sys


def rows(run):
    with gzip.open(run + "/positions.jsonl.gz", "rt") as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def at(run, times):
    out = {}
    for r in rows(run):
        if r["t"] in times:
            out[r["t"]] = {b[0]: b[1:4] for b in r["b"]}
        if len(out) == len(times):
            break
    return out


def main():
    a_dir, b_dir = sys.argv[1:3]
    times = [float(x) for x in (sys.argv[3] if len(sys.argv) > 3 else "1010,1020,1060").split(",")]
    a, b = at(a_dir, set(times)), at(b_dir, set(times))
    for t in times:
        if t not in a or t not in b:
            print(f"t={t}: missing in {'a' if t not in a else 'b'}")
            continue
        pa, pb = a[t], b[t]
        common = sorted(set(pa) & set(pb))
        d = sorted(math.dist(pa[i], pb[i]) for i in common)
        n = len(d)
        print(f"t={t}: a {len(pa)} bodies, b {len(pb)}, common {n}; separation median {d[n // 2]:.4g} m, "
              f"90% {d[int(0.9 * n)]:.4g}, max {d[-1]:.4g}; over 1 cm {sum(x > 0.01 for x in d)}")
        for name, p, q in (("a", pa, pb), ("b", pb, pa)):
            deep = sorted(p, key=lambda i: p[i][1])[:8]
            print(f"  deepest in {name}: " + ", ".join(
                f"{i} y={p[i][1]:.2f}" + (f" (other {q[i][1]:.2f})" if i in q else " (absent)") for i in deep))


main()
