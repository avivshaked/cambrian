"""The value score for the skin map's full colour (mode 3): the generalised eigenvector with the SECOND-largest eigenvalue of
hue_screen's (Q, S) problem. Same features, fit worlds, Q, S and ridge as the hue score, so it is S-orthogonal to it over the
fitting population. Unit sd over the fitting population; sign so that it correlates positively with investment.

Writes value.json (hybrid.json's shape) and expected.csv for the 36 sheet bodies, and prints the numbers the report needs.

    python -I -B scratch/skin-map/full-colour/fit_value.py
"""
import csv
import json
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_screen as S   # noqa: E402

R = 'D:/Projects/experiments/evolution-simulator'
OUTDIR = R + '/scratch/skin-map/full-colour'
HYB = json.load(open(R + '/scratch/skin-map/hue-screen/hybrid.json'))
LIST = json.load(open(R + '/scratch/skin-map/hue-dials/map2/list.json'))['items']


def average_ranks(x):
    order = np.argsort(x, kind='stable')
    r = np.empty(len(x))
    xs = np.asarray(x)[order]
    i = 0
    while i < len(x):
        j = i
        while j + 1 < len(x) and xs[j + 1] == xs[i]:
            j += 1
        r[order[i:j + 1]] = (i + j) / 2.0
        i = j + 1
    return r


def spearman(a, b):
    return float(np.corrcoef(average_ranks(a), average_ranks(b))[0, 1])


def main():
    bodies = S.load()
    assert S.LINEAGE == HYB['features'], 'feature list differs from hybrid.json'
    kin, _ = S.pairs_of(bodies, set(S.FIT_WORLDS))
    X = np.array([b['lin'] for b in bodies.values() if b['alive'] and b['world'] in S.FIT_WORLDS])
    D = np.array([c['lin'] - p['lin'] for p, c in kin])
    Sg = np.cov(X, rowvar=False)
    Q = np.cov(D, rowvar=False) / 2.0 + S.RIDGE * np.eye(len(S.LINEAGE))
    vals, vecs = np.linalg.eig(np.linalg.solve(Q, Sg))
    order = np.argsort(-vals.real)
    k1, k2 = int(order[0]), int(order[1])
    print('eigenvalues top 4', [float(vals[i].real) for i in order[:4]], 'imag max', float(np.abs(vals.imag).max()))

    # reproduce the hue score through the pipeline's own function and compare with hybrid.json
    w1, ratio1, centre = S.lineage_weights(bodies)
    assert np.allclose(w1, HYB['weights'], rtol=0, atol=1e-9), 'hue weights do not reproduce'
    assert abs(ratio1 - HYB['spread_over_parent_child']) < 1e-9
    # and the eigenvector taken here for the first, normalised the same way
    v1 = vecs[:, k1].real
    v1 = v1 / (X @ v1).std()
    if np.corrcoef(X @ v1, X[:, S.LINEAGE.index('log_parts')])[0, 1] < 0:
        v1 = -v1
    assert np.allclose(v1, HYB['weights'], rtol=0, atol=1e-9), 'my first eigenvector differs from hybrid.json'
    print('hue weights reproduce hybrid.json to 1e-9, max abs diff', float(np.abs(v1 - np.array(HYB['weights'])).max()))

    w2 = vecs[:, k2].real
    w2 = w2 / (X @ w2).std()
    inv = S.LINEAGE.index('investment')
    neu = S.LINEAGE.index('brain_log_neurons')
    c_inv = float(np.corrcoef(X @ w2, X[:, inv])[0, 1])
    c_neu = float(np.corrcoef(X @ w2, X[:, neu])[0, 1])
    print('weight on investment', float(w2[inv]), 'corr(value, investment)', c_inv, 'corr(value, brain_log_neurons)', c_neu)
    sign_by = 'investment'
    if abs(w2[inv]) < 0.01:
        sign_by = 'brain_log_neurons'
        sgn = 1 if c_neu >= 0 else -1
    else:
        sgn = 1 if c_inv >= 0 else -1
    w2 = w2 * sgn
    print('sign fixed by', sign_by, 'flipped' if sgn < 0 else 'kept')
    print('value weights', dict(zip(S.LINEAGE, [round(float(x), 4) for x in w2])))
    print('unit sd check', float((X @ w2).std()))

    alive = [b for b in bodies.values() if b['alive']]
    hue_fit = [float(b['lin'] @ v1) for b in alive]
    val_fit = [float(b['lin'] @ w2) for b in alive]
    assert len(alive) == len(HYB['fitting_scores_sorted'])
    assert np.allclose(sorted(hue_fit), HYB['fitting_scores_sorted'], rtol=0, atol=1e-9)
    print('hue fitting scores reproduce hybrid.json (%d alive)' % len(alive))
    print('Pearson hue vs value, all alive (%d)' % len(alive), float(np.corrcoef(hue_fit, val_fit)[0, 1]))
    print('Pearson hue vs value, fit worlds', float(np.corrcoef(X @ v1, X @ w2)[0, 1]))

    out = {
        'rule': 'fit_value.py second eigenvector, 2026-10-30',
        'fit_worlds': S.FIT_WORLDS, 'ridge': S.RIDGE, 'spread_over_parent_child': float(vals[k2].real),
        'features': S.LINEAGE, 'weights': [float(x) for x in w2], 'zclip': HYB['zclip'],
        'brain_shrink_links': HYB['brain_shrink_links'], 'fitting_scores_sorted': sorted(val_fit),
    }
    assert out['zclip'] == 3.0 and out['brain_shrink_links'] == S.H.K
    json.dump(out, open(OUTDIR + '/value.json', 'w', encoding='utf-8'), indent=1)

    keys = list(bodies)
    vrank = dict(zip(keys, S.rank_map([float(bodies[k]['lin'] @ w2) for k in keys], val_fit)))
    hrank = dict(zip(keys, S.rank_map([float(bodies[k]['lin'] @ v1) for k in keys], hue_fit)))

    # today's base hue (the diet logistic) from hybrid-ref.csv, which carries it for every body
    ref = {}
    with open(R + '/scratch/skin-map/hue-screen/hybrid-ref.csv', encoding='utf-8') as f:
        for r in csv.DictReader(f):
            ref[(r['world'], r['id'])] = r
    ak = [(b['world'], b['id']) for b in alive]
    print('Spearman value rank vs today base hue (diet), alive:',
          spearman([vrank[k] for k in ak], [float(ref[k]['today_base_hue']) for k in ak]))
    print('Spearman hue rank vs today base hue (diet), alive:',
          spearman([hrank[k] for k in ak], [float(ref[k]['today_base_hue']) for k in ak]))
    print('Spearman value rank vs hue rank, alive:', spearman([vrank[k] for k in ak], [hrank[k] for k in ak]))

    kin_all, strangers = S.pairs_of(bodies, set(S.H.WORLDS))

    def stats(pairs, rk):
        d = np.array([abs(rk[(a['world'], a['id'])] - rk[(b['world'], b['id'])]) for a, b in pairs])
        return float(np.median(d)), float(np.percentile(d, 95)), len(d)

    print('value rank |diff| parent-child (median, p95, n):', stats(kin_all, vrank))
    print('value rank |diff| strangers (median, p95, n):', stats(strangers, vrank))
    print('hue rank |diff| parent-child (median, p95, n):', stats(kin_all, hrank))
    print('hue rank |diff| strangers (median, p95, n):', stats(strangers, hrank))

    rows = []
    bad = 0
    for it in LIST:
        iid = it['name'].split('-', 1)[1]
        world = it['run_dir'].replace('\\', '/').split('/runs/')[1].split('/')[0]
        k = (world, iid)
        h, v = hrank[k], vrank[k]
        refh = float(ref[k]['base_hue'])
        if abs(h - refh) > 1e-6:
            bad += 1
            print('HUE RANK MISMATCH', k, h, refh)
        rows.append((world, iid, h, v, (165.0 + 360.0 * h) % 360.0, 2 * v - 1))
    with open(OUTDIR + '/expected.csv', 'w', encoding='utf-8', newline='') as f:
        wr = csv.writer(f)
        wr.writerow(['world', 'id', 'hue_rank', 'value_rank', 'wheel_hue_deg', 's_at_value_1'])
        for r in rows:
            wr.writerow([r[0], r[1], '%.9f' % r[2], '%.9f' % r[3], '%.6f' % r[4], '%.9f' % r[5]])
    print('expected.csv rows', len(rows), 'distinct ids', len({r[1] for r in rows}),
          'hue rank mismatches vs hybrid-ref.csv', bad)
    for r in rows:
        print('%-12s %6s hue %.4f val %.4f wheel %6.1f s %+.3f' % (r[0], r[1], r[2], r[3], r[4], r[5]))


if __name__ == '__main__':
    main()
