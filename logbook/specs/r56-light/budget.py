"""Round 56 groundwork: a leaf's night and lean season against its reserve.

Computed, not simulated. A body's net, in units of its own standing cost W, is
    net(t) = r * g(f(t)) - 1
where f(t) = (1 + As sin(2 pi t / Ps)) (1 + Ad sin(2 pi t / Pd)) is the light multiplier the
proposed build would give, r = 0.85 * (income at f = 1) / W is the body's income ratio after
exudation (measured in round 54/55's worlds at 1.5 to 2.7, mean about 2.1), and g is
    light-bound:   g(f) = f
    matter-bound:  g(f) = min(k f, 1), k = light capacity over the uptake take at f = 1 (k = 2 here)
The required reserve is the largest drawdown of the cumulative net path (max over t1 < t2 of
-integral of net), in seconds of W: what a body starting full needs never to reach zero, if it
spends nothing on breeding meanwhile. The deficit spell is the longest continuous run of net < 0.

Reserves to read it against, in seconds of W (see the report for sources):
    growing body    ~16 s   (GrowthReserveFloor 0.1 x 500 J/m3 / 3 W/m3, less the support cost)
    newborn         ~42 s   (NewbornReserveFraction 0.2: reserve/tissue = 0.25)
    adult, just bred 360-440 s  (the clade's median ReserveMargin)
    adult at gate   ~800-1300 s (margin + child cost over W, the two typical leaves)
Median lifetime of an inherited leaf ~1,250 s; median age at first birth ~800 s.
"""
import math, sys

LIFE = 1250.0
FIRST = 800.0

def path(r, Ad, Pd, As, Ps, mode, k=2.0, horizon=None, dt=None):
    if horizon is None:
        horizon = max(Ps if As > 0 else 0, Pd if Ad > 0 else 0) * (2 if As > 0 else 3)
        if horizon == 0: horizon = 1000.0
    if dt is None:
        dt = max(0.5, min(Pd if Ad > 0 else 1e9, Ps if As > 0 else 1e9) / 2000.0)
    n = int(horizon / dt)
    cum = 0.0; peak = 0.0; drawdown = 0.0
    spell = 0.0; longest = 0.0; inc = 0.0; deficit_time = 0.0
    for i in range(n):
        t = i * dt
        f = (1 + As * math.sin(2 * math.pi * t / Ps) if As > 0 else 1.0) * \
            (1 + Ad * math.sin(2 * math.pi * t / Pd) if Ad > 0 else 1.0)
        g = f if mode == 'light' else min(k * f, 1.0)
        net = r * g - 1.0
        inc += g * dt
        cum += net * dt
        peak = max(peak, cum)
        drawdown = max(drawdown, peak - cum)
        if net < 0:
            spell += dt; deficit_time += dt
            longest = max(longest, spell)
        else:
            spell = 0.0
    return dict(drawdown=drawdown, longest=longest, income=inc / (n * dt),
                deficit_share=deficit_time / (n * dt))

def verdict(need):
    for name, have in (('juvenile', 16), ('newborn', 42), ('just-bred adult', 400), ('adult at gate', 800)):
        pass
    if need < 1: return 'all'
    if need <= 16: return 'all (juveniles at the edge)'
    if need <= 42: return 'newborns and up; growing bodies die'
    if need <= 400: return 'adults only; newborns and growing die'
    if need <= 800: return 'only adults near their gate'
    if need <= 1300: return 'only the fullest adults'
    return 'none on reserve alone'

out = sys.argv[1] if len(sys.argv) > 1 else 'night-table.tsv'
with open(out, 'w', encoding='utf-8') as f:
    f.write('# Computed. Night: day cycle alone (season off). Units: seconds; reserve need in seconds of the body\'s standing cost.\n')
    f.write('# r = income ratio after exudation at the mean light; k = 2 for the matter-bound body.\n')
    f.write('day s\tamplitude\tbody\tr\tnight (P/2) / lifetime\tdeficit spell s\tspell / lifetime\treserve needed s\twho survives the night on reserve\tmean income vs no cycle\n')
    for Pd in (200, 1000, 3000, 10000):
        for Ad in (0.5, 0.8, 1.0):
            for mode, r in (('light', 2.1), ('light', 1.25), ('matter', 2.1)):
                p = path(r, Ad, Pd, 0, 1, mode)
                f.write('%d\t%.1f\t%s-bound\t%.2f\t%.2f\t%.0f\t%.2f\t%.0f\t%s\t%.3f\n' % (
                    Pd, Ad, mode, r, Pd / 2 / LIFE, p['longest'], p['longest'] / LIFE, p['drawdown'],
                    verdict(p['drawdown']), p['income']))

out2 = sys.argv[2] if len(sys.argv) > 2 else 'season-table.tsv'
with open(out2, 'w', encoding='utf-8') as f:
    f.write('# Computed. Season alone (day off), and with a day cycle on top where named.\n')
    f.write('# lean half = Ps/2 (multiplier below 1); breeds at trough = r(1-As) > 1 for the light-bound body\n')
    f.write('season s\tseason amp\tday s\tday amp\tbody\tr\tlean half s\tlean half / lifetime\ttrough multiplier\tbreeds at trough\tdeficit spell s\tspell / lifetime\treserve needed s\twho survives on reserve\tmean income vs no cycle\n')
    for Ps in (10000, 30000):
        for As in (0.3, 0.5, 0.8):
            for Pd, Ad in ((0, 0), (1000, 0.5), (1000, 0.8)):
                for mode, r in (('light', 2.1), ('light', 1.25), ('matter', 2.1)):
                    p = path(r, Ad, Pd if Pd else 1, As, Ps, mode)
                    trough = (1 - As) * (1 - Ad if Ad else 1)
                    g = trough if mode == 'light' else min(2 * trough, 1)
                    f.write('%d\t%.1f\t%s\t%s\t%s-bound\t%.2f\t%d\t%.1f\t%.2f\t%s\t%.0f\t%.2f\t%.0f\t%s\t%.3f\n' % (
                        Ps, As, Pd if Pd else '-', Ad if Ad else '-', mode, r, Ps // 2, Ps / 2 / LIFE, trough,
                        'yes' if r * g > 1 else 'no', p['longest'], p['longest'] / LIFE, p['drawdown'],
                        verdict(p['drawdown']), p['income']))
print('wrote', out, out2)
