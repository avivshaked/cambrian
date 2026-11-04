"""The chroma and sheen scores for the skin map's v6 axes dial: the generalised eigenvectors with the THIRD and FOURTH largest
eigenvalues of hue_screen's (Q, S) problem, the same one fit_value.py takes its second from. Each is normalised to unit sd over
the fitting population (the fit worlds' living bodies), as fit_value does. SIGN: each is fixed so that its largest-magnitude weight
is positive (the third and fourth have no natural direction to fix it by, as the second has in investment).

Writes chroma.json (third) and sheen.json (fourth) in value.json's shape, with fitting_scores_sorted over every living body, as
value.json's is, and expected.csv for the 36 sheet bodies: the four ranks, and the hue, OKLab L, chroma and sheen factor the
axes dial (rules 3 to 6 of the v6 brief) gives at its defaults. L and C are the nominal targets before the sRGB gamut
bisection, which needs the body's actual colour; the brown columns are the same at EVOSIM_THEATRE_SKIN_MAP_BROWN 1.
Asserts that a fresh second eigenvector reproduces value.json's weights to 1e-9.

    python -I -B scratch/skin-map/full-colour/v6/fit_axes.py
"""
import csv
import json
import math
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_screen as S   # noqa: E402

R = 'D:/Projects/experiments/evolution-simulator'
FC = R + '/scratch/skin-map/full-colour'
OUTDIR = FC + '/v6'
HYB = json.load(open(R + '/scratch/skin-map/hue-screen/hybrid.json'))
VAL = json.load(open(FC + '/value.json'))
LIST = json.load(open(R + '/scratch/skin-map/hue-dials/map2/list.json'))['items']

# the v6 defaults (the C# dials)
ARC_START, ARC_END = 170.0, 55.0
LIGHT_DARK, LIGHT_PALE = 0.12, 0.90
CHROMA_MAX, CHROMA_POWER = 0.22, 1.5
SHEEN_LOW, SHEEN_HIGH, SHEEN_POWER = 0.5, 4.0, 2.0
BROWN_CENTRE, BROWN_HALF, BROWN_L, BROWN_C = 42.0, 30.0, 0.38, 0.075


def unit(w, X):
    w = w / (X @ w).std()
    if w[int(np.argmax(np.abs(w)))] < 0:
        w = -w
    return w


def brown_weight(h):
    d = abs(h - BROWN_CENTRE)
    return 0.5 * (1.0 + math.cos(math.pi * d / BROWN_HALF)) if d < BROWN_HALF else 0.0


def main():
    bodies = S.load()
    assert S.LINEAGE == HYB['features'] == VAL['features']
    kin, _ = S.pairs_of(bodies, set(S.FIT_WORLDS))
    X = np.array([b['lin'] for b in bodies.values() if b['alive'] and b['world'] in S.FIT_WORLDS])
    D = np.array([c['lin'] - p['lin'] for p, c in kin])
    Sg = np.cov(X, rowvar=False)
    Q = np.cov(D, rowvar=False) / 2.0 + S.RIDGE * np.eye(len(S.LINEAGE))
    vals, vecs = np.linalg.eig(np.linalg.solve(Q, Sg))
    order = np.argsort(-vals.real)
    print('eigenvalues top 4', [float(vals[i].real) for i in order[:4]], 'imag max', float(np.abs(vals.imag).max()))

    # a fresh second eigenvector, sign by investment as fit_value does, must reproduce value.json
    inv = S.LINEAGE.index('investment')
    w2 = vecs[:, order[1]].real
    w2 = w2 / (X @ w2).std()
    if np.corrcoef(X @ w2, X[:, inv])[0, 1] < 0:
        w2 = -w2
    assert np.allclose(w2, VAL['weights'], rtol=0, atol=1e-9), 'second eigenvector differs from value.json'
    print('second eigenvector reproduces value.json to 1e-9, max abs diff', float(np.abs(w2 - np.array(VAL['weights'])).max()))

    w3 = unit(vecs[:, order[2]].real, X)
    w4 = unit(vecs[:, order[3]].real, X)
    for name, w in (('chroma (3rd)', w3), ('sheen (4th)', w4)):
        print(name, 'unit sd', float((X @ w).std()), 'largest weight',
              S.LINEAGE[int(np.argmax(np.abs(w)))], float(w[int(np.argmax(np.abs(w)))]))
        print('  weights', dict(zip(S.LINEAGE, [round(float(x), 4) for x in w])))

    alive = [b for b in bodies.values() if b['alive']]
    assert len(alive) == len(VAL['fitting_scores_sorted'])
    fits = {}
    for key, w, k, eig in (('chroma', w3, 'third', vals[order[2]].real), ('sheen', w4, 'fourth', vals[order[3]].real)):
        fit = [float(b['lin'] @ w) for b in alive]
        fits[key] = (w, fit)
        out = {
            'rule': 'fit_axes.py %s eigenvector, sign by largest weight, 2026-10-30' % k,
            'fit_worlds': S.FIT_WORLDS, 'ridge': S.RIDGE, 'spread_over_parent_child': float(eig),
            'features': S.LINEAGE, 'weights': [float(x) for x in w], 'zclip': HYB['zclip'],
            'brain_shrink_links': HYB['brain_shrink_links'], 'fitting_scores_sorted': sorted(fit),
        }
        assert out['zclip'] == 3.0 and out['brain_shrink_links'] == S.H.K
        json.dump(out, open('%s/%s.json' % (OUTDIR, key), 'w', encoding='utf-8'), indent=1)

    # hue and value ranks, as fit_value computes them
    w1 = np.array(HYB['weights'])
    hue_fit = [float(b['lin'] @ w1) for b in alive]
    val_fit = [float(b['lin'] @ w2) for b in alive]
    keys = list(bodies)
    ranks = {
        'hue': dict(zip(keys, S.rank_map([float(bodies[k]['lin'] @ w1) for k in keys], hue_fit))),
        'value': dict(zip(keys, S.rank_map([float(bodies[k]['lin'] @ w2) for k in keys], val_fit))),
    }
    for key in ('chroma', 'sheen'):
        w, fit = fits[key]
        ranks[key] = dict(zip(keys, S.rank_map([float(bodies[k]['lin'] @ w) for k in keys], fit)))

    span = (ARC_END - ARC_START) % 360.0
    rows = []
    for it in LIST:
        iid = it['name'].split('-', 1)[1]
        world = it['run_dir'].replace('\\', '/').split('/runs/')[1].split('/')[0]
        k = (world, iid)
        h, v, c, s = ranks['hue'][k], ranks['value'][k], ranks['chroma'][k], ranks['sheen'][k]
        hue = ((ARC_START + span * min(1.0, max(0.0, h))) / 360.0 % 1.0) * 360.0
        L = LIGHT_DARK + (LIGHT_PALE - LIGHT_DARK) * v
        C = CHROMA_MAX * c ** CHROMA_POWER
        sheen = SHEEN_LOW + (SHEEN_HIGH - SHEEN_LOW) * s ** SHEEN_POWER
        b = brown_weight(hue)
        Lb = L + (min(L, BROWN_L) - L) * b
        Cb = C + (BROWN_C - C) * b
        rows.append((world, iid, h, v, c, s, hue, L, C, sheen, b, Lb, Cb))
    with open(OUTDIR + '/expected.csv', 'w', encoding='utf-8', newline='') as f:
        wr = csv.writer(f)
        wr.writerow(['world', 'id', 'hue_rank', 'value_rank', 'chroma_rank', 'sheen_rank', 'hue_deg', 'oklab_L', 'oklab_C',
                     'sheen_factor', 'brown_w', 'brown1_L', 'brown1_C'])
        for r in rows:
            wr.writerow([r[0], r[1]] + ['%.9f' % x for x in r[2:6]] + ['%.6f' % x for x in r[6:]])
    print('expected.csv rows', len(rows), 'distinct ids', len({r[1] for r in rows}))
    for r in rows:
        print('%-12s %6s hue %5.1f L %.3f C %.3f sheen x%.2f  brown w %.2f L %.3f C %.3f' % (
            r[0], r[1], r[6], r[7], r[8], r[9], r[10], r[11], r[12]))


if __name__ == '__main__':
    main()
