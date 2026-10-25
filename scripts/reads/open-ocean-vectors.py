"""The open ocean's 25 wave vectors, by a stated rule (logbook/specs/open-ocean-current.md).

Radii geometric from 2 to 10 over 25 steps; directions by the golden angle over the half-plane;
each target takes the nearest unused integer vector in the half-plane (m > 0, or m = 0 and n > 0),
and below |k| = 5 only off-axis vectors are eligible. Prints the table and its spread.
"""
import math

N = 25
phi = (math.sqrt(5) - 1) / 2
cands = [(m, n) for m in range(0, 11) for n in range(-10, 11)
         if (m > 0 or (m == 0 and n > 0)) and 2 <= math.hypot(m, n) <= 10]
used = []
for i in range(N):
    r = 2 * 5 ** (i / (N - 1))
    th = (i * phi % 1.0) * math.pi
    tx, tz = r * math.cos(th), r * math.sin(th)
    best = None
    for (m, n) in cands:
        if (m, n) in used:
            continue
        if r < 5 and (m == 0 or n == 0):
            continue
        # the half-plane's direction folds at 180 degrees: compare against the target and its negative
        d = min(math.hypot(m - tx, n - tz), math.hypot(-m - tx, -n - tz))
        if best is None or d < best[0]:
            best = (d, m, n)
    used.append((best[1], best[2]))

used.sort(key=lambda v: (math.hypot(*v), math.atan2(v[1], v[0])))
for m, n in used:
    print(f"({m:3d},{n:3d})  |k| {math.hypot(m, n):5.2f}  dir {math.degrees(math.atan2(n, m)) % 180:6.1f}")
dirs = sorted(math.degrees(math.atan2(n, m)) % 180 for m, n in used)
gaps = [b - a for a, b in zip(dirs, dirs[1:])] + [dirs[0] + 180 - dirs[-1]]
print("largest direction gap", round(max(gaps), 1), "deg; axis vectors", sum(1 for m, n in used if m == 0 or n == 0))
print("ms =", [m for m, n in used])
print("ns =", [n for m, n in used])
