"""Two PTX files of one kernel side by side: declared registers by class, the local depot (per-thread stack),
parameter bytes, and instruction counts by opcode, with the differences.

Usage: python -I ptx-cost.py <before.ptx> <after.ptx>
"""
import collections
import re
import sys


def read(path):
    text = open(path).read()
    regs = {m.group(1): int(m.group(2)) for m in re.finditer(r'\.reg\s+\.(\w+)\s+%\w+<(\d+)>;', text)}
    depot = [int(x) for x in re.findall(r'__local_depot\d*\[(\d+)\]', text)]
    params = len(re.findall(r'^\s*\.param\s', text, re.M))
    ops = collections.Counter()
    for line in text.splitlines():
        m = re.match(r'^\s*(?:@!?%p\d+\s+)?([a-z][\w]*(?:\.[\w]+)*)\s', line)
        if m and not line.strip().startswith('.'):
            ops[m.group(1)] += 1
    return regs, depot, params, ops


a, b = read(sys.argv[1]), read(sys.argv[2])
print("virtual registers declared (PTX, not the card's allocation):")
for k in sorted(set(a[0]) | set(b[0])):
    print(f"  {k}: {a[0].get(k, 0)} -> {b[0].get(k, 0)}")
print(f"local depot bytes: {a[1]} -> {b[1]}")
print(f"param lines: {a[2]} -> {b[2]}")
print(f"instructions: {sum(a[3].values())} -> {sum(b[3].values())}")
for op in sorted(set(a[3]) | set(b[3])):
    d = b[3][op] - a[3][op]
    if d:
        print(f"  {op}: {a[3][op]} -> {b[3][op]} ({d:+d})")
