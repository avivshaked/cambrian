"""The skin map's hue, screened offline (the coordinator's order of 2026-10-30, after hue_spread.py found that base hue reads only
the three diet shares and so gives each guild one colour).

Only base hue (and, in the hybrid, base saturation) changes; the other eight parameters keep the pooled fit's rule. The palette
stays as ruled: 220 degrees plus 190 times the base hue. Animals are the non-producers (photosynthetic share under 0.5) of the
pooled fit's ten reference worlds; the fitting set is those alive at 24,000 s (2,131).

  today     the pooled fit's base hue: sigmoid of share_consumer, share_absorptive, share_photosynthetic
  lineage   (1) sigmoid of a lineage score: the weights over every feature but the three diet shares that maximise the spread
            between bodies over the spread between a parent and its child (a generalised eigenvector, ridge 0.05), fitted on five
            worlds and scaled so that two standard deviations reach 0.1 and 0.9
  rank      (2) today's diet score rank-transformed over the fitting set (ties share their middle rank)
  hybrid    (3) hue from the lineage score rank-transformed over the fitting set; diet moves base saturation instead, as the
            mean of today's saturation and today's diet sigmoid

Reported for each, over all ten worlds and over the five worlds the lineage weights were not fitted on: the band shares over bodies
and over clades (each clade its median), the palette hue's spread within each guild, the largest single 5-degree bin, gold's share,
and the parent-child and stranger distances, over the whole skin vector and over the palette hue alone. Writes hue-screen/.

    python -I scratch/skin-map/hue_screen.py
"""
import csv
import math
import os
import random
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_spread as H   # noqa: E402

OUT = H.S + '/hue-screen'
DIET = ['share_consumer', 'share_absorptive', 'share_photosynthetic']
LINEAGE = [f for f in H.P['features_passing'] if f not in DIET]
FIT_WORLDS = H.WORLDS[0::2]
RIDGE = 0.05
STRANGERS = 4000


def z_of(r, f, shrink):
    z = (float(r[f]) - H.MEAN[f]) / H.SD[f] if H.SD[f] > 0 else 0.0
    if f.startswith('brain_w_'):
        z *= shrink
    return max(-3.0, min(3.0, z))


def load():
    bodies = {}
    for w in H.WORLDS:
        with open(H.folder(w) + '/features.csv', encoding='utf-8') as f:
            for r in csv.DictReader(f):
                if float(r['share_photosynthetic']) >= 0.5:
                    continue
                n = math.expm1(float(r['brain_log_links']))
                shrink = n / (n + H.K)
                v = H.vec(r)
                diet_logit = H.W['parameters'][0].get('bias', 0.0) + sum(
                    i['weight'] * z_of(r, i['feature'], shrink) for i in H.W['parameters'][0]['inputs'] if i['used'])
                bodies[(w, r['id'])] = {
                    'world': w, 'id': r['id'], 'parent': r['parent'], 'clade': r['clade'], 'alive': r['alive'] == '1',
                    'vec': np.array(v), 'diet': diet_logit,
                    'lin': np.array([z_of(r, f, shrink) for f in LINEAGE]),
                    'guild': guild(r)}
    return bodies


def guild(r):
    c, a, p = (float(r[k]) for k in DIET)
    if p >= 0.15:
        return 'mixotroph'
    if c >= a and c > 0:
        return 'consumer'
    if a > 0:
        return 'absorber'
    return 'neither'


def pairs_of(bodies, worlds):
    kin = [(bodies[(b['world'], b['parent'])], b) for b in bodies.values()
           if b['alive'] and b['world'] in worlds and (b['world'], b['parent']) in bodies]
    alive = [b for b in bodies.values() if b['alive'] and b['world'] in worlds]
    rng = random.Random(6303)
    strangers = []
    while len(strangers) < STRANGERS:
        a, b = rng.choice(alive), rng.choice(alive)
        if a['clade'] != b['clade'] or a['world'] != b['world']:
            strangers.append((a, b))
    return kin, strangers


def other_channels():
    """The other nine parameters' logits as weights over LINEAGE (none of them reads a diet share)."""
    rows = []
    for prm in H.W['parameters'][1:]:
        a = np.zeros(len(LINEAGE))
        for i in prm['inputs']:
            if i['used']:
                a[LINEAGE.index(i['feature'])] = i['weight']
        rows.append(a)
    return np.array(rows)


def lineage_weights(bodies, orthogonal=False):
    kin, _ = pairs_of(bodies, set(FIT_WORLDS))
    X = np.array([b['lin'] for b in bodies.values() if b['alive'] and b['world'] in FIT_WORLDS])
    D = np.array([c['lin'] - p['lin'] for p, c in kin])
    S = np.cov(X, rowvar=False)
    Q = np.cov(D, rowvar=False) / 2.0 + RIDGE * np.eye(len(LINEAGE))
    # Orthogonal: w is held uncorrelated over the population with each other channel's logit, w' S a_k = 0, by solving
    # the eigenproblem inside the null space of (S A')'.
    B = np.eye(len(LINEAGE))
    if orthogonal:
        C = S @ other_channels().T
        u, sv, vt = np.linalg.svd(C.T)
        B = vt[(sv > 1e-9).sum():].T
    vals, vecs = np.linalg.eig(np.linalg.solve(B.T @ Q @ B, B.T @ S @ B))
    k = int(np.argmax(vals.real))
    w = B @ vecs[:, k].real
    s = X @ w
    w = w / s.std()
    if np.corrcoef(X @ w, X[:, LINEAGE.index('log_parts')])[0, 1] < 0:
        w = -w
    return w, float(vals[k].real), float((X @ w).mean())


def rank_map(values, fitting):
    srt = np.sort(np.array(fitting))
    n = len(srt)

    def f(x):
        lo = np.searchsorted(srt, x, 'left')
        hi = np.searchsorted(srt, x, 'right')
        return float((lo + hi) / 2.0 / n)
    return [f(x) for x in values]


def apply(bodies, w, centre, w_orth):
    fit_alive = [b for b in bodies.values() if b['alive']]
    diet_fit = [b['diet'] for b in fit_alive]
    lin_fit = [float(b['lin'] @ w) for b in fit_alive]
    keys = list(bodies)
    diet_rank = rank_map([bodies[k]['diet'] for k in keys], diet_fit)
    lin_rank = rank_map([float(bodies[k]['lin'] @ w) for k in keys], lin_fit)
    orth_rank = rank_map([float(bodies[k]['lin'] @ w_orth) for k in keys], [float(b['lin'] @ w_orth) for b in fit_alive])
    gain = math.log(0.9 / 0.1) / 2.0
    for k, dr, lr, orr in zip(keys, diet_rank, lin_rank, orth_rank):
        b = bodies[k]
        s = float(b['lin'] @ w) - centre
        today = b['vec']
        lineage = today.copy()
        lineage[0] = 1 / (1 + math.exp(-gain * s))
        rank = today.copy()
        rank[0] = dr
        hybrid = today.copy()
        hybrid[0] = lr
        hybrid[1] = (today[1] + 1 / (1 + math.exp(-b['diet']))) / 2.0
        orth = hybrid.copy()
        orth[0] = orr
        b['v'] = {'today': today, 'lineage': lineage, 'rank': rank, 'hybrid': hybrid, 'orth': orth}


def hue_deg(v):
    return 190.0 * min(1.0, max(0.0, v[0]))   # degrees along the palette from its blue end


def report(bodies, variant, worlds, kin, strangers):
    alive = [b for b in bodies.values() if b['alive'] and b['world'] in worlds]
    hues = [H.palette(b['v'][variant][0]) for b in alive]
    bands = [sum(1 for h in hues if H.band(h) == name) for name, _, _ in H.BANDS]
    clades = {}
    for b in alive:
        clades.setdefault((b['world'], b['clade']), []).append(b['v'][variant][0])
    cmed = [H.palette(sorted(v)[len(v) // 2]) for v in clades.values()]
    cbands = [sum(1 for h in cmed if H.band(h) == name) for name, _, _ in H.BANDS]
    bins = np.histogram([hue_deg(b['v'][variant]) for b in alive], bins=38, range=(0, 190))[0]
    spread = {}
    for g in ('consumer', 'absorber', 'mixotroph'):
        hs = np.array([hue_deg(b['v'][variant]) for b in alive if b['guild'] == g])
        if len(hs):
            spread[g] = (len(hs), float(np.percentile(hs, 75) - np.percentile(hs, 25)), float(np.percentile(hs, 95) - np.percentile(hs, 5)))
    dk = np.array([np.linalg.norm(p['v'][variant] - c['v'][variant]) for p, c in kin])
    ds = np.array([np.linalg.norm(a['v'][variant] - b['v'][variant]) for a, b in strangers])
    hk = np.array([abs(hue_deg(p['v'][variant]) - hue_deg(c['v'][variant])) for p, c in kin])
    hs_ = np.array([abs(hue_deg(a['v'][variant]) - hue_deg(b['v'][variant])) for a, b in strangers])
    n = len(alive)
    return {
        'n': n, 'bands': bands, 'clade_bands': cbands, 'clades': len(cmed), 'top_bin': float(bins.max()) / n,
        'gold': bands[-1] / n, 'spread': spread,
        'kin': (float(np.median(dk)), float(np.percentile(dk, 95))), 'stranger': (float(np.median(ds)), float(np.percentile(ds, 95))),
        'hue_kin': (float(np.median(hk)), float(np.percentile(hk, 95))), 'hue_stranger': (float(np.median(hs_)), float(np.percentile(hs_, 95))),
        'kin_over_stranger_95': float(np.mean(dk > np.median(ds))),
    }


def table(results, title):
    names = [b[0] for b in H.BANDS]
    lines = ['## ' + title, '', '| | ' + ' | '.join(results) + ' |', '|---|' + '---|' * len(results)]

    def row(label, fn):
        lines.append('| %s | %s |' % (label, ' | '.join(fn(r) for r in results.values())))

    row('bodies', lambda r: str(r['n']))
    for i, nm in enumerate(names):
        row('%s, bodies' % nm, lambda r, i=i: '%.0f%%' % (100.0 * r['bands'][i] / r['n']))
    for i, nm in enumerate(names):
        row('%s, clades' % nm, lambda r, i=i: '%d of %d' % (r['clade_bands'][i], r['clades']))
    row('largest 5-degree bin', lambda r: '%.0f%%' % (100 * r['top_bin']))
    for g in ('consumer', 'absorber', 'mixotroph'):
        row('%s hue IQR / 5-95 (deg)' % g, lambda r, g=g: '%.0f / %.0f' % r['spread'][g][1:] if g in r['spread'] else '-')
    row('parent-child skin, median / 95th', lambda r: '%.3f / %.3f' % r['kin'])
    row('stranger skin, median / 95th', lambda r: '%.3f / %.3f' % r['stranger'])
    row('parent-child past the stranger median', lambda r: '%.1f%%' % (100 * r['kin_over_stranger_95']))
    row('parent-child hue, median / 95th (deg)', lambda r: '%.1f / %.1f' % r['hue_kin'])
    row('stranger hue, median / 95th (deg)', lambda r: '%.1f / %.1f' % r['hue_stranger'])
    row('gold reached', lambda r: '%.1f%%' % (100 * r['gold']))
    return lines


def main():
    os.makedirs(OUT, exist_ok=True)
    bodies = load()
    w, ratio, centre = lineage_weights(bodies)
    w_orth, ratio_orth, _ = lineage_weights(bodies, orthogonal=True)
    apply(bodies, w, centre, w_orth)
    out = ['# The skin map\'s hue, screened', '',
           'Lineage score fitted on %s (ridge %.2f): spread over parent-child spread %.1f. Its weights, by size:' % (', '.join(FIT_WORLDS), RIDGE, ratio), '']
    order = np.argsort(-np.abs(w))
    out.append(', '.join('%s %+.2f' % (LINEAGE[i], w[i]) for i in order[:12]))
    out += ['', 'orth: the hybrid with the lineage score held uncorrelated over the population with the nine other channels\' logits '
            '(spread over parent-child spread %.1f). Its weights, by size:' % ratio_orth, '']
    order = np.argsort(-np.abs(w_orth))
    out.append(', '.join('%s %+.2f' % (LINEAGE[i], w_orth[i]) for i in order[:12]))
    out.append('')
    for title, worlds in (('All ten worlds', set(H.WORLDS)), ('The five worlds the lineage weights were not fitted on', set(H.WORLDS) - set(FIT_WORLDS))):
        kin, strangers = pairs_of(bodies, worlds)
        res = {v: report(bodies, v, worlds, kin, strangers) for v in ('today', 'lineage', 'rank', 'hybrid', 'orth')}
        out += table(res, title) + ['', 'Pairs: %d parent-child, %d strangers.' % (len(kin), len(strangers)), '']
    with open(OUT + '/hue-screen.md', 'w', encoding='utf-8') as f:
        f.write('\n'.join(out) + '\n')
    np.save(OUT + '/lineage-weights.npy', w)
    with open(OUT + '/lineage-weights.txt', 'w', encoding='utf-8') as f:
        f.write('centre %.6f\n' % centre + '\n'.join('%s %.6f' % (LINEAGE[i], w[i]) for i in range(len(LINEAGE))) + '\n')
    print('\n'.join(out))


if __name__ == '__main__':
    main()
