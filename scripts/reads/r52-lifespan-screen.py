"""Round 52's lifespan screen, part 1: round 51's life history, per seed.

For bodies born between 5,000 and 20,000 s (so a life of 10,000 s fits before the end), read from
lineage.jsonl: the share that bred, children per breeder, the age at first birth, the interval
between births, the child's size at birth (bf), and each death's cause and age. From stats.jsonl,
the light the living captured and what they spent, over 10,000 to 30,000 s.

    python scripts/reads/r52-lifespan-screen.py history [arm ...]
"""
import glob, json, math, sys

RUNS = 'runs'


def run_dir(arm):
    return sorted(glob.glob(f'{RUNS}/{arm}/2026*'))[-1]


def med(xs):
    xs = sorted(xs)
    return xs[len(xs) // 2] if xs else float('nan')


def quant(xs, q):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(q * len(xs)))] if xs else float('nan')


def history(arm, lo=5000.0, hi=20000.0):
    d = run_dir(arm)
    born, par, bf, died, cause = {}, {}, {}, {}, {}
    with open(f'{d}/lineage.jsonl', encoding='utf-8') as f:
        for line in f:
            if line.startswith('{"e":"b"'):
                r = json.loads(line)
                born[r['id']] = r['t']; par[r['id']] = r.get('p', -1); bf[r['id']] = r.get('bf', float('nan'))
            elif line.startswith('{"e":"d"'):
                r = json.loads(line)
                died[r['id']] = r['t']; cause[r['id']] = r.get('c', '?')
    kids = {}
    for i, p in par.items():
        if p is not None and p >= 0 and p in born:
            kids.setdefault(p, []).append(born[i] - born[p])
    cohort = [i for i in born if lo <= born[i] <= hi and par.get(i, -1) >= 0]
    breeders = [i for i in cohort if kids.get(i)]
    firsts = [min(kids[i]) for i in breeders]
    gaps = []
    for i in breeders:
        ks = sorted(kids[i])
        gaps += [b - a for a, b in zip(ks, ks[1:])]
    dead = [i for i in cohort if i in died]
    aged = [i for i in dead if cause[i] == 'aged']
    starved = [i for i in dead if cause[i] == 'starved']
    kid_bf = [bf[c] for c, p in par.items() if p in set(breeders) and not math.isnan(bf.get(c, float('nan')))]
    print(f'{arm}: cohort {len(cohort)} (inherited births {lo:.0f}-{hi:.0f} s), dead by the end {len(dead)}')
    print(f'  bred: {len(breeders)} ({100 * len(breeders) / len(cohort):.0f}%), children per breeder median {med([len(kids[i]) for i in breeders])}, mean {sum(len(kids[i]) for i in breeders) / max(1, len(breeders)):.2f}')
    print(f'  age at first birth: median {med(firsts):.0f} s (10% {quant(firsts, .1):.0f}, 90% {quant(firsts, .9):.0f}); interval between births median {med(gaps):.0f} s')
    print(f'  child size at birth (bf) median {med(kid_bf):.2f}')
    print(f'  causes: aged {len(aged)} ({100 * len(aged) / max(1, len(dead)):.0f}%), starved {len(starved)}, other {len(dead) - len(aged) - len(starved)}')
    print(f'  age at death: aged median {med([died[i] - born[i] for i in aged]):.0f} s; starved median {med([died[i] - born[i] for i in starved]):.0f} s; starved 10% {quant([died[i] - born[i] for i in starved], .1):.0f}, 90% {quant([died[i] - born[i] for i in starved], .9):.0f}')
    bdead = [i for i in breeders if i in died]
    print(f'  breeders: aged {sum(1 for i in bdead if cause[i] == "aged")} of {len(bdead)} dead; age at death median {med([died[i] - born[i] for i in bdead]):.0f} s')
    nb = [i for i in cohort if not kids.get(i) and i in died]
    print(f'  non-breeders: aged {sum(1 for i in nb if cause[i] == "aged")} of {len(nb)} dead; age at death median {med([died[i] - born[i] for i in nb]):.0f} s')
    rows = []
    with open(f'{d}/stats.jsonl', encoding='utf-8') as f:
        for line in f:
            r = json.loads(line)
            rows.append((r['t'] if 't' in r else r.get('seconds', 0), r.get('lightJoules', 0), r.get('spendJoules', 0)))
    a = min(rows, key=lambda x: abs(x[0] - 10000)); b = rows[-1]
    light, spend = b[1] - a[1], b[2] - a[2]
    print(f'  books {a[0]:.0f}-{b[0]:.0f} s: light {light / 1e3:.0f} kJ, spent {spend / 1e3:.0f} kJ, spent/light {spend / max(light, 1):.2f}')


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'history':
        for arm in sys.argv[2:] or ['r51-s1', 'r51-s2', 'r51-s3']:
            history(arm)

# ---------------------------------------------------------------------------------------------
# Part 2: the price screen. A breeder's life in continuous time, the mean-field approximation.
#
#   income 1; upkeep u * L**a; surplus s(L) = 1 - u * L**a (no breeding at or below 0)
#   maturity at t_m(L) = t_m1 * s(1) / s(L)            (the first child is paid from the surplus)
#   birth rate after maturity beta(L) = beta1 * s(L) / s(1)
#   ageing clock C: dC/dt = 1 + beta * bf * D(L), D(L) = f * B * L   (D128: one doubling per child
#     of the parent's size, scaled by the child's size bf)
#   hazard h = mu + G(C), G Gompertz with doubling D(L) and median L * B absent anything else
#   R0(L) = integral of l(t) beta(t) dt over the adult life (juvenile survival is a common factor
#     and cancels in every ratio below); r(L) from Euler-Lotka with the same l and m.
#
# beta1 and mu are calibrated at L = 1 so that a breeder has `kids` children on average and a share
# `starved` of breeders' deaths are starvation (round 51: 2.6 and 0.23). Everything else is D128's
# as built: B 3,000 s, f 0.2. Inference throughout: a mean-field sketch, not the world.

B_BASE, F_DOUBLING = 3000.0, 0.2
DT, T_MAX = 5.0, 80000.0


def life(L, a, u, beta1, mu, bf, t_m1, want_r=False):
    s1 = 1.0 - u
    s = 1.0 - u * L ** a
    if s <= 0:
        return 0.0, 0.0, 0.0, float('nan')
    t_m = t_m1 * s1 / s
    beta = beta1 * s / s1
    D = F_DOUBLING * B_BASE * L
    Bk = math.log(2) / D
    A = Bk * math.log(2) / (math.exp(Bk * B_BASE * L) - 1)   # G's cumulative is ln 2 at C = L * B
    t, C, logl, R0, kids_t, deaths_age, deaths_mu = 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
    lotka = []
    while t < T_MAX:
        l = math.exp(logl)
        if l < 1e-7:
            break
        breeding = t >= t_m
        b = beta if breeding else 0.0
        g = A * math.exp(Bk * C) if Bk * C < 700 else 1e300
        R0 += l * b * DT
        if want_r:
            lotka.append((t, l * b * DT))
        deaths_age += l * min(1.0, g * DT)
        deaths_mu += l * mu * DT
        logl -= (g + mu) * DT
        C += (1.0 + b * bf * D) * DT
        t += DT
    r = float('nan')
    if want_r and R0 > 0:
        import numpy as np
        tt = np.array([x[0] for x in lotka]); ww = np.array([x[1] for x in lotka])
        lo, hi = -1e-2, 1e-2
        for _ in range(60):
            mid = (lo + hi) / 2
            v = float(np.sum(np.exp(-mid * tt) * ww))
            lo, hi = (mid, hi) if v > 1 else (lo, mid)
        r = (lo + hi) / 2
    share_mu = deaths_mu / max(1e-12, deaths_mu + deaths_age)
    return R0, share_mu, t_m, r


def calibrate(u, bf, t_m1, kids=2.6, starved=0.23):
    beta1, mu = 2e-3, 1e-4
    for _ in range(60):
        R0, sh, _, _ = life(1.0, 0.0, u, beta1, mu, bf, t_m1)
        beta1 *= (kids / R0) ** 0.7
        mu *= (starved / max(sh, 1e-9)) ** 0.7 if sh > 0 else 2
    return beta1, mu


def screen(us=(0.25, 0.35, 0.45), bfs=(0.2, 0.3), t_m1=1000.0):
    Ls = [round(0.25 * k, 2) for k in range(1, 25)] + [7, 8, 10]
    aa = [0.0, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0]
    for u in us:
        for bf in bfs:
            beta1, mu = calibrate(u, bf, t_m1)
            R01, sh1, _, r1 = life(1.0, 0.0, u, beta1, mu, bf, t_m1, want_r=True)
            print(f'u {u:.2f}, bf {bf:.2f}: calibrated beta1 {beta1 * 1e3:.3f}/ks, adult starvation {mu * 1e3:.3f}/ks '
                  f'(mean adult life to it {1 / mu:.0f} s); at L 1: R0 {R01:.2f}, starved share {sh1:.2f}, r {r1 * 1e3:.3f}/ks')
            for a in aa:
                res = [(L,) + life(L, a, u, beta1, mu, bf, t_m1, want_r=True) for L in Ls]
                bestR0 = max(res, key=lambda x: x[1])
                bestr = max((x for x in res if not math.isnan(x[4])), key=lambda x: x[4])
                R = {x[0]: x[1] for x in res}
                eps = 0.05
                g = (math.log(life(1 + eps, a, u, beta1, mu, bf, t_m1)[0]) - math.log(life(1 - eps, a, u, beta1, mu, bf, t_m1)[0])) / (math.log(1 + eps) - math.log(1 - eps))
                edge = ' (the edge of the grid)' if bestR0[0] == Ls[-1] else ''
                print(f'   a {a:4.2f}: best L by R0 {bestR0[0]:5.2f}{edge}, by r {bestr[0]:5.2f}; '
                      f'dlnR0/dlnL at 1 {g:+.3f}; R0 at L 0.5/2/3 over L 1: '
                      f'{R[0.5] / R[1.0]:.2f} {R[2.0] / R[1.0]:.2f} {R[3.0] / R[1.0]:.2f}')


def neutral_a(u, bf, t_m1=1000.0, starved=0.23):
    """The exponent at which L = 1 is the R0 optimum (the gradient at 1 is zero)."""
    beta1, mu = calibrate(u, bf, t_m1, starved=starved)
    def grad(a):
        eps = 0.05
        return (math.log(life(1 + eps, a, u, beta1, mu, bf, t_m1)[0]) - math.log(life(1 - eps, a, u, beta1, mu, bf, t_m1)[0]))
    lo, hi = 0.0, 5.0
    for _ in range(40):
        mid = (lo + hi) / 2
        lo, hi = (mid, hi) if grad(mid) > 0 else (lo, mid)
    return (lo + hi) / 2


if __name__ == '__main__' and len(sys.argv) > 1 and sys.argv[1] == 'screen':
    screen()
    print('the exponent at which L = 1 is the R0 optimum (bf 0.25), by the upkeep share u and the share')
    print('of breeding deaths that are starvation (round 51: 0.23):')
    print('   u     ' + ''.join(f'starved {st:.2f}  ' for st in (0.1, 0.23, 0.4)))
    for u in (0.25, 0.35, 0.4, 0.45, 0.5, 0.55):
        print(f'   {u:.2f}  ' + ''.join(f'a = {neutral_a(u, 0.25, starved=st):.2f}      ' for st in (0.1, 0.23, 0.4)))
    print('the best L by R0 at a = 1 and at a = 0.5, by u (bf 0.25, starved 0.23):')
    Ls = [round(0.05 * k, 2) for k in range(10, 121)]
    for u in (0.25, 0.35, 0.4, 0.45, 0.5, 0.55):
        beta1, mu = calibrate(u, 0.25, 1000.0)
        best = {a: max(Ls, key=lambda L: life(L, a, u, beta1, mu, 0.25, 1000.0)[0]) for a in (0.5, 1.0)}
        print(f'   u {u:.2f}: a 1 -> L {best[1.0]:.2f}; a 0.5 -> L {best[0.5]:.2f}')