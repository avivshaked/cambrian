"""Finds compensated sums in a PTX file: t = a + b (add.f32 or add.rn.f32), then d = t - a, then e = d - b,
in the same order the kernel writes them, with nothing between them required. Prints each with its lines.

Usage: python -I ptx-kahan.py <file.ptx>
"""
import re
import sys

ops = []
for n, line in enumerate(open(sys.argv[1]).read().splitlines(), 1):
    m = re.match(r'^\s*(add|sub)(?:\.rn)?\.f32\s+(%f\d+),\s*(%f\d+|0f[0-9A-F]+),\s*(%f\d+|0f[0-9A-F]+);', line)
    if m:
        ops.append((n, m.group(1), m.group(2), m.group(3), m.group(4)))

found = 0
for i, (n, op, t, a, b) in enumerate(ops):
    if op != 'add':
        continue
    for x, y in ((a, b), (b, a)):
        d = next(((n2, d2) for n2, op2, d2, p, q in ops[i + 1:i + 40] if op2 == 'sub' and p == t and q == x), None)
        if not d:
            continue
        e = next(((n3, e3) for n3, op3, e3, p, q in ops[i + 1:i + 60] if op3 == 'sub' and p == d[1] and q == y), None)
        if e:
            found += 1
            print(f"line {n}: {t} = {a} + {b}; line {d[0]}: {d[1]} = {t} - {x}; line {e[0]}: {e[1]} = {d[1]} - {y}")
print(f"{found} compensated sums")
