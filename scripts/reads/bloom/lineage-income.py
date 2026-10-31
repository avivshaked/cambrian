"""Grazer income by lineage, from the death rows D264's `DeathRowIncome` writes (`kl`, `ko`, `kr`, `kg`: lifetime joules
kept from bodies with photosynthetic tissue, from other living bodies, from remains, and swallowed from kills). A grazer
is a body born with `atk` and `ink` both above 0, as cannibal.py and grazer-deaths.py read it. A lineage is the founder
a body descends from, traced by the parent field. Only bodies that died by --end are counted, so the living are left out.

Usage: python -I scripts/reads/bloom/lineage-income.py LINEAGE.JSONL [LINEAGE.JSONL ...] [--end 24000] [--min 30]

Prints the world's totals over every grazer death, then each lineage with at least --min grazer deaths, largest first:
its deaths, its share of kept bite income from leaves, others and remains, and its swallowed joules. Writes nothing.
"""
import argparse, collections, json


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--end", type=float, default=24000.0)
    ap.add_argument("--min", type=int, default=30)
    a = ap.parse_args()
    parent, grazer = {}, set()
    deaths = []
    for p in a.files:
        with open(p, encoding="utf-8") as f:
            for line in f:
                if '"e":"b"' in line[:12] or '"e":"d"' in line[:12]:
                    try:
                        r = json.loads(line)
                    except ValueError:
                        continue  # a live run's last line, half written
                if '"e":"b"' in line[:12]:
                    parent[r["id"]] = r.get("p", -1)
                    if r.get("atk", 0) > 0 and r.get("ink", 0) > 0:
                        grazer.add(r["id"])
                elif '"e":"d"' in line[:12]:
                    if r["t"] <= a.end and r["id"] in grazer and "kl" in r:
                        deaths.append(r)

    def root(i):
        seen = 0
        while parent.get(i, -1) not in (-1, None) and parent.get(i) in parent and seen < 100000:
            i = parent[i]
            seen += 1
        return i

    by = collections.defaultdict(lambda: [0, 0.0, 0.0, 0.0, 0.0])
    tot = [0, 0.0, 0.0, 0.0, 0.0]
    for r in deaths:
        row = by[root(r["id"])]
        for acc in (row, tot):
            acc[0] += 1
            acc[1] += r["kl"]; acc[2] += r["ko"]; acc[3] += r["kr"]; acc[4] += r["kg"]

    def line(name, v):
        bite = v[1] + v[2] + v[3]
        if bite <= 0:
            return f"{name:>10}  deaths {v[0]:6d}  no bite income"
        return (f"{name:>10}  deaths {v[0]:6d}  leaves {v[1] / bite:.2f}  others {v[2] / bite:.2f}  "
                f"remains {v[3] / bite:.2f}  bite kept {bite / 1e6:.3f} MJ  swallowed {v[4] / 1e6:.3f} MJ")

    print(f"grazer deaths with income fields to {int(a.end)} s: {tot[0]}")
    print(line("all", tot))
    for k, v in sorted(by.items(), key=lambda kv: -kv[1][0]):
        if v[0] >= a.min:
            print(line(str(k), v))


if __name__ == "__main__":
    main()
