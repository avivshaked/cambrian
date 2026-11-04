"""v6 design probe: how heritable is each generalised eigenvector of hue_screen's (Q, S) problem beyond the first two?

For eigenvectors 1 to 6: the eigenvalue (spread over parent-child spread), the rank's parent-child |difference| (median, p95) and
the stranger median over all ten worlds, and the Spearman correlation of each rank with diet (today's base hue) and with ranks 1
and 2. Prints only; writes nothing.

    python -I -B scratch/skin-map/full-colour/probe_axes.py
"""
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map/full-colour')
import hue_screen as S   # noqa: E402
from fit_value import spearman, average_ranks  # noqa: E402


def main():
    bodies = S.load()
    kin, _ = S.pairs_of(bodies, set(S.FIT_WORLDS))
    X = np.array([b['lin'] for b in bodies.values() if b['alive'] and b['world'] in S.FIT_WORLDS])
    D = np.array([c['lin'] - p['lin'] for p, c in kin])
    Sg = np.cov(X, rowvar=False)
    Q = np.cov(D, rowvar=False) / 2.0 + S.RIDGE * np.eye(len(S.LINEAGE))
    vals, vecs = np.linalg.eig(np.linalg.solve(Q, Sg))
    order = np.argsort(-vals.real)

    alive = [k for k, b in bodies.items() if b['alive']]
    L = np.array([bodies[k]['lin'] for k in alive])
    diet = [bodies[k]['v']['today'][0] for k in alive] if 'v' in bodies[alive[0]] else None
    kin_all, strangers = S.pairs_of(bodies, set(S.H.WORLDS))
    ranks = []
    for n in range(6):
        w = vecs[:, order[n]].real
        s = L @ w
        r = average_ranks(s) / (len(s) - 1)
        rk = dict(zip(alive, r))
        ranks.append(r)

        def stats(pairs):
            d = np.array([abs(rk[(a['world'], a['id'])] - rk[(b['world'], b['id'])]) for a, b in pairs
                          if (a['world'], a['id']) in rk and (b['world'], b['id']) in rk])
            return np.median(d), np.percentile(d, 95)

        km, kp = stats(kin_all)
        sm, _ = stats(strangers)
        top = sorted(zip(S.LINEAGE, w / np.abs(w).max()), key=lambda t: -abs(t[1]))[:4]
        line = 'eig %d  ratio %6.2f  kin med %.3f p95 %.3f  strangers med %.3f' % (n + 1, vals[order[n]].real, km, kp, sm)
        if diet is not None:
            line += '  rho diet %+.2f' % spearman(r, diet)
        line += '  rho r1 %+.2f r2 %+.2f' % (spearman(r, ranks[0]), spearman(r, ranks[1]) if n > 0 else 1.0)
        print(line)
        print('        ', ', '.join('%s %+.2f' % t for t in top))


if __name__ == '__main__':
    main()
