"""Round 61's landscape pick (the coordinator's rule, 2026-10-16): among the map-check candidates, those with nearest
island centres at least 40 m apart and the largest island at most 0.4 of the island columns, ranked by islands of
10+ columns, then by the largest island's share (smaller first). Reads every scratch/logs/r61-mapcheck*.log in the r61
checkout. Standard library only."""
import glob
import re
import sys

LOGS = sys.argv[1] if len(sys.argv) > 1 else r"D:\Projects\experiments\evolution-simulator\scratch\wt-r61\scratch\logs"
pat = re.compile(r"maps: landscape (\d+) \((\S+)\): (\d+) islands of 10\+ columns, of (\d+); (\d+) island columns "
                 r"\(([\d.]+) of the water\); largest ([\d.]+) of them; nearest centres (\S+) m apart; (.*)$")
rows = {}
for path in sorted(glob.glob(LOGS + r"\r61-mapcheck*.log")):
    if "preflight" in path:
        continue
    for line in open(path, encoding="utf-8"):
        m = pat.search(line.rstrip())
        if m:
            seed = int(m.group(1))
            gap = float(m.group(8)) if m.group(8) != "nan" else float("nan")
            rows[seed] = (int(m.group(3)), float(m.group(7)), gap, int(m.group(5)), m.group(9))
ok = {s: r for s, r in rows.items() if r[2] >= 40 and r[1] <= 0.4}
rank = sorted(ok, key=lambda s: (-ok[s][0], ok[s][1]))
print(f"{len(rows)} landscapes read ({min(rows)}-{max(rows)}); {len(ok)} pass gap >= 40 m and largest <= 0.4")
for s in rank:
    n, big, gap, cols, isl = ok[s]
    print(f"landscape {s}: {n} islands, largest {big:.2f}, nearest centres {gap:.0f} m, {cols} island columns; {isl}")
