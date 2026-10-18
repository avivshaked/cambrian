"""Round 57 groundwork: round 56's leaf budget (logbook/specs/r56-light/budget.py) with deeper seasons and weather.

Computed, not simulated. A body's net in units of its own standing cost W is
    net(t) = r * g(f(t) * w(t)) - 1
with f the season-times-day multiplier of LightModel, and w the proposed weather: one factor a day,
log-normal with mean 1 (log sd sigma), each day's log following the last with correlation rho, blended
smoothly across the day boundary. g is light-bound (g = x) or matter-bound (g = min(k x, 1), k = 2).
The required reserve is the largest drawdown of the cumulative net over one year, in seconds of W.
Weather is drawn N times; the table gives the median and the 90th percentile over those years.
Usage: python budget57.py [out.tsv]
"""
import math
import random
import sys

LIFE = 1250.0
YEAR = 10000.0
DAY = 1000.0
AD = 0.5


def weather(days, sigma, rho, rng):
    if sigma <= 0:
        return [1.0] * (days + 1)
    mu = -0.5 * sigma * sigma
    x = rng.gauss(0, sigma)
    out = []
    for _ in range(days + 1):
        out.append(math.exp(mu + x))
        x = rho * x + math.sqrt(1 - rho * rho) * rng.gauss(0, sigma)
    return out


def season_factor(t, As, shape):
    s = math.sin(2 * math.pi * t / YEAR)
    if shape == 'square':  # summer then winter, each half a year at its level
        return 1 + As if s >= 0 else 1 - As
    return 1 + As * s


def year(r, As, sigma, rho, mode, rng, k=2.0, dt=2.0, shape='sine'):
    w = weather(int(YEAR / DAY) + 1, sigma, rho, rng)
    cum = peak = draw = 0.0
    spell = longest = 0.0
    inc = 0.0
    n = int(YEAR / dt)
    for i in range(n):
        t = i * dt
        d = int(t // DAY)
        u = (t - d * DAY) / DAY
        s = 0.5 - 0.5 * math.cos(math.pi * u)  # smooth blend from day d to d+1
        wt = w[d] * (1 - s) + w[d + 1] * s
        f = season_factor(t, As, shape) * (1 + AD * math.sin(2 * math.pi * t / DAY)) * wt
        g = f if mode == 'light' else min(k * f, 1.0)
        net = r * g - 1.0
        inc += g * dt
        cum += net * dt
        peak = max(peak, cum)
        draw = max(draw, peak - cum)
        spell = spell + dt if net < 0 else 0.0
        longest = max(longest, spell)
    return draw, longest, inc / YEAR


def verdict(need):
    if need <= 16: return 'all'
    if need <= 42: return 'newborns and up'
    if need <= 360: return 'adults only'
    if need <= 800: return 'adults near their gate'
    if need <= 1300: return 'only the fullest adults'
    return 'none on reserve alone'


def pct(xs, p):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * (len(xs) - 1) + 0.5))]


rng = random.Random(57)
N = 200
rows = []
if len(sys.argv) > 2 and sys.argv[2] == 'square':
    # Two seasons a year at their levels (the owner's question of 2026-10-05), year given in argv[3].
    YEAR = float(sys.argv[3])
    grid, shape = [(As, s) for As in (0.3, 0.4, 0.5, 0.6, 0.7) for s in (0.0, 0.3)], 'square'
else:
    grid, shape = [(As, s) for As in (0.5, 0.65, 0.7, 0.75, 0.8) for s in (0.0, 0.2, 0.3)], 'sine'
for As, sigma in grid:
    if True:
        for mode, r in (('light', 2.1), ('light', 1.25), ('matter', 2.1)):
            runs = [year(r, As, sigma, 0.5, mode, rng, shape=shape) for _ in range(N if sigma > 0 else 1)]
            need = [x[0] for x in runs]
            spell = [x[1] for x in runs]
            inc = [x[2] for x in runs]
            rows.append((As, sigma, mode, r, pct(need, 0.5), pct(need, 0.9), pct(spell, 0.5), pct(spell, 0.9),
                         sum(inc) / len(inc), verdict(pct(need, 0.9))))
out = sys.argv[1] if len(sys.argv) > 1 else 'season-weather-table.tsv'
with open(out, 'w', encoding='utf-8') as f:
    f.write('# Computed (budget57.py). Day 1,000 s at 0.5, year %.0f s, season %s. Weather rho 0.5. '
            'Reserve and spell in s of W.\n' % (YEAR, shape))
    f.write('season amp\tweather sigma\tbody\tr\treserve median\treserve p90\tdeficit spell median\tspell p90\t'
            'mean income vs no cycle\twho survives a p90 year on reserve\n')
    for x in rows:
        f.write('%.2f\t%.1f\t%s-bound\t%.2f\t%.0f\t%.0f\t%.0f\t%.0f\t%.3f\t%s\n' % x)
        print('%.2f  %.1f  %-6s %.2f  need %5.0f / %5.0f  spell %5.0f / %5.0f  inc %.3f  %s' % x)
