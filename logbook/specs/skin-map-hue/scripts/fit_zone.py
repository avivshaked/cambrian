"""The zone and family scores for v7, the reef look: the generalised eigenvectors with the FIFTH and SIXTH largest eigenvalues of
hue_screen's (Q, S) problem, the one fit_value.py and v6/fit_axes.py take the second to fourth from. Each is normalised to unit sd
over the fitting population and signed so its largest-magnitude weight is positive, as fit_axes does.

Before either is used, its heritability is read here the same way as the others': the eigenvalue (spread over parent-child
spread), and the rank gap between parent and child (median and 95th percentile) against the rank gap between strangers (median),
over every living body's fitting rank. Prints a table of all six axes so the new ones sit beside the old. Writes zone.json (fifth)
and family.json (sixth) in value.json's shape into this folder; nothing reads them until the lane decides to use them.

    python -I -B scratch/skin-map/full-colour/v7/fit_zone.py
"""
import json
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_screen as S   # noqa: E402

R = 'D:/Projects/experiments/evolution-simulator'
FC = R + '/scratch/skin-map/full-colour'
OUTDIR = FC + '/v7'
HYB = json.load(open(R + '/scratch/skin-map/hue-screen/hybrid.json'))
VAL = json.load(open(FC + '/value.json'))
CHR = json.load(open(FC + '/v6/chroma.json'))
SHE = json.load(open(FC + '/v6/sheen.json'))


def unit(w, X):
    w = w / (X @ w).std()
    return w if w[int(np.argmax(np.abs(w)))] > 0 else -w


def rank_of(sorted_fit, s):
    """The middle rank of score s among the sorted fitting scores, in [0, 1], as the C# computes a body's rank."""
    lo = np.searchsorted(sorted_fit, s, side='left')
    hi = np.searchsorted(sorted_fit, s, side='right')
    return (lo + hi) / 2.0 / len(sorted_fit)


def main():
    bodies = S.load()
    assert S.LINEAGE == HYB['features'] == VAL['features'] == CHR['features'] == SHE['features']
    kin, _ = S.pairs_of(bodies, set(S.FIT_WORLDS))
    X = np.array([b['lin'] for b in bodies.values() if b['alive'] and b['world'] in S.FIT_WORLDS])
    D = np.array([c['lin'] - p['lin'] for p, c in kin])
    Sg = np.cov(X, rowvar=False)
    Q = np.cov(D, rowvar=False) / 2.0 + S.RIDGE * np.eye(len(S.LINEAGE))
    vals, vecs = np.linalg.eig(np.linalg.solve(Q, Sg))
    order = np.argsort(-vals.real)
    print('eigenvalues top 8', [round(float(vals[i].real), 3) for i in order[:8]], 'imag max', float(np.abs(vals.imag).max()))

    w3 = unit(vecs[:, order[2]].real, X)
    assert np.allclose(w3, CHR['weights'], rtol=0, atol=1e-9), 'third eigenvector differs from chroma.json'
    w5 = unit(vecs[:, order[4]].real, X)
    w6 = unit(vecs[:, order[5]].real, X)

    alive = [b for b in bodies.values() if b['alive']]
    rng = np.random.default_rng(20261030)
    axes = [('hue', np.array(HYB['weights']), None), ('value', np.array(VAL['weights']), None),
            ('chroma', np.array(CHR['weights']), vals[order[2]].real), ('sheen', np.array(SHE['weights']), vals[order[3]].real),
            ('zone (5th)', w5, vals[order[4]].real), ('family (6th)', w6, vals[order[5]].real)]
    print('\n| axis | spread over parent-child | kin rank gap, median | kin rank gap, p95 | stranger rank gap, median |')
    print('|---|---|---|---|---|')
    out_fits = {}
    for name, w, eig in axes:
        fit = np.sort(np.array([float(b['lin'] @ w) for b in alive]))
        if name.startswith('zone'):
            out_fits['zone'] = (w, fit, eig)
        if name.startswith('family'):
            out_fits['family'] = (w, fit, eig)
        gaps = np.array([abs(rank_of(fit, float(c['lin'] @ w)) - rank_of(fit, float(p['lin'] @ w))) for p, c in kin])
        ranks = np.array([rank_of(fit, float(b['lin'] @ w)) for b in alive])
        a, b = rng.integers(0, len(ranks), 20000), rng.integers(0, len(ranks), 20000)
        stranger = np.abs(ranks[a] - ranks[b])
        print('| %s | %s | %.3f | %.3f | %.3f |' % (name, '%.2f' % eig if eig is not None else 'see value.json', float(np.median(gaps)),
                                                  float(np.percentile(gaps, 95)), float(np.median(stranger))))

    for name, w in (('zone', w5), ('family', w6)):
        print('%s weights' % name, dict(zip(S.LINEAGE, [round(float(x), 4) for x in w])))
        print('  correlation with hue, value, chroma, sheen scores:',
              [round(float(np.corrcoef(X @ w, X @ np.array(o))[0, 1]), 3) for o in (HYB['weights'], VAL['weights'], CHR['weights'], SHE['weights'])])

    for key, k in (('zone', 'fifth'), ('family', 'sixth')):
        w, fit, eig = out_fits[key]
        out = {
            'rule': 'fit_zone.py %s eigenvector, sign by largest weight, 2026-10-30' % k,
            'fit_worlds': S.FIT_WORLDS, 'ridge': S.RIDGE, 'spread_over_parent_child': float(eig),
            'features': S.LINEAGE, 'weights': [float(x) for x in w], 'zclip': HYB['zclip'],
            'brain_shrink_links': HYB['brain_shrink_links'], 'fitting_scores_sorted': [float(x) for x in fit],
        }
        json.dump(out, open('%s/%s.json' % (OUTDIR, key), 'w', encoding='utf-8'), indent=1)
        print('wrote %s/%s.json' % (OUTDIR, key))


if __name__ == '__main__':
    main()
