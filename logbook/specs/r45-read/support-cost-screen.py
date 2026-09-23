"""The support-cost screen D107 asks for: what a price on a part's load times its distance
from the root does to four bodies, before the mechanism is built.

Bodies: round 45's reference leaf (scratch/r45-build/ledger-leaf.txt: 0.3558 m2 lit,
0.1597 m3, fixation 3.5583 W at the surface, standing 0.479 W), a two-part leaf whose second
part sits 0.5 m out, a 2 m kelp of four leaf-sized parts along a stalk, and round 44's giant
7597 at its module ceiling (scratch/logs/giant-7597.txt: seven copies, each 1.75 times the
last along one axis, the seventh 1.2 x 7.3 x 0.01 m half-extents at 14 m).

Income is the ledger's surface value, 10 W per m2 of lit area (3.5583 / 0.3558), with the
silhouette cap ignored (the fan is spread, so the cap barely binds: logbook/0108). Standing
cost is the ledger's 3 W per m3. Two forms of the support cost are screened:
  A*d   price [W per m2 per m]  x  lit area  x  distance of the part's centre from the root
  A*d^2 price [W per m2 per m2] x  lit area  x  distance squared  (a bending moment: load x lever, twice)
Mass x distance is the same shape as area x distance for a sheet of fixed thickness (1 cm
clamp), so it is not screened separately.
"""
import math

INCOME_PER_M2 = 3.5583 / 0.3558     # W/m2 at the surface
STANDING_PER_M3 = 0.479 / 0.1597    # W/m3

def box(hx, hy, hz, at):
    # lit area: the largest face, 4 * the two largest half-extents
    h = sorted([hx, hy, hz], reverse=True)
    return dict(area=4 * h[0] * h[1], volume=8 * hx * hy * hz, d=math.sqrt(sum(c * c for c in at)))

leaf = [box(0.24, 0.248, 0.045, (0, 0, 0))]  # approximate the ledger's one-part leaf: 0.238 m2 lit... scale to 0.3558
# The ledger's leaf has 0.3558 m2 lit and 0.1597 m3: use those numbers directly.
leaf = [dict(area=0.3558, volume=0.1597, d=0.0)]
two_part = [dict(area=0.3558, volume=0.1597, d=0.0), dict(area=0.3558, volume=0.1597, d=0.5)]
kelp = [dict(area=0.3558, volume=0.1597, d=0.5 * i) for i in range(4)]  # 0, 0.5, 1.0, 1.5 m
giant = [
    box(0.24, 0.248, 0.045, (0, 0, 0)),
    box(0.314, 0.436, 0.034, (-0.417, 0.244, 0.126)),
    box(0.411, 0.765, 0.025, (-0.61, 0.047, 0.959)),
    box(0.536, 1.345, 0.019, (0.017, 1.121, 1.87)),
    box(0.701, 2.364, 0.014, (-1.837, 3.044, 2.322)),
    box(0.916, 4.155, 0.011, (-4.136, 1.991, 6.357)),
    box(1.197, 7.303, 0.010, (-0.326, 5.948, 12.67)),
]

bodies = [("leaf (1 part)", leaf), ("two parts, 0.5 m", two_part), ("kelp, 4 parts to 1.5 m", kelp), ("giant 7597, 7 copies to 14 m", giant)]

def screen(form, prices):
    print(f"\n== support cost = price x lit area x {'d' if form == 1 else 'd^2'}   (W; net = income - standing - support)")
    print(f"{'body':32s} {'income':>8s} {'standing':>9s} " + " ".join(f"p={p:<6g}" for p in prices))
    for name, parts in bodies:
        income = sum(p['area'] for p in parts) * INCOME_PER_M2
        standing = sum(p['volume'] for p in parts) * STANDING_PER_M3
        cells = []
        for price in prices:
            support = sum(price * p['area'] * p['d'] ** form for p in parts)
            net = income - standing - support
            cells.append(f"{net:8.2f}")
        print(f"{name:32s} {income:8.2f} {standing:9.3f} " + " ".join(cells))
    # the marginal copy: the giant's last part alone
    last = giant[-1]
    print(f"   giant's seventh copy alone: area {last['area']:.1f} m2 at {last['d']:.1f} m, income {last['area']*INCOME_PER_M2:.0f} W; "
          + ", ".join(f"support {sum([price * last['area'] * last['d'] ** form]):.0f} W at p={price:g}" for price in prices))

print(__doc__)
for name, parts in bodies:
    print(f"{name:32s} parts {len(parts)}  lit area {sum(p['area'] for p in parts):7.2f} m2  volume {sum(p['volume'] for p in parts):7.3f} m3  "
          f"area-weighted mean distance {sum(p['area']*p['d'] for p in parts)/sum(p['area'] for p in parts):5.2f} m")
screen(1, [0.1, 0.3, 1, 2, 3])
screen(2, [0.03, 0.1, 0.3, 1])
print("\nThe reach at which a copy's own support equals its own income: A*d form d* = 10/p; A*d^2 form d* = sqrt(10/p).")
for p in (0.1, 0.3, 1, 2, 3):
    print(f"  A*d   p={p:<4g} d* = {INCOME_PER_M2/p:5.1f} m")
for p in (0.03, 0.1, 0.3, 1):
    print(f"  A*d^2 p={p:<4g} d* = {math.sqrt(INCOME_PER_M2/p):5.1f} m")
