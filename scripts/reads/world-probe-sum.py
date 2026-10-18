"""Sum a world-probe build's lines (Core's WorldPhaseProbe, -p:WorldPhaseProbe=1) over a whole run.

Each line prints milliseconds a step over a block of steps; this weights them by the block and
prints each phase's total, its milliseconds a world step and its share.
Usage: python scripts/reads/world-probe-sum.py <stderr file>
"""
import re
import sys

totals, steps = {}, 0
for line in open(sys.argv[1], encoding="utf-8"):
    m = re.match(r"world-probe, ms a step over (\d+) steps at ([\d.]+) bodies: ([\d.]+)(.*)", line.strip())
    if not m:
        continue
    n = int(m.group(1))
    steps += n
    totals["TOTAL"] = totals.get("TOTAL", 0) + float(m.group(3)) * n
    for name, ms in re.findall(r", ([^,]+?) ([\d.]+)", m.group(4)):
        totals[name] = totals.get(name, 0) + float(ms) * n

print(f"world steps {steps}")
whole = totals.get("TOTAL", 1)
for name, ms in sorted(totals.items(), key=lambda x: -x[1]):
    print(f"| {name} | {ms / 1000:.2f} s | {ms / steps:.2f} ms | {100 * ms / whole:.1f}% |")
