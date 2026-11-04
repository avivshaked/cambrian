"""The C# hybrid rule (TheatreSkinMap.ApplyHybrid, dial 2) transcribed into Python line for line, run on each world's features.csv
and checked against hue-screen/hybrid-ref.csv. It reads the numbers from hybrid.json, as gen_constants.py does for the C#.

    python -I check_rule.py
"""
import csv
import json
import math
import sys

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_spread as H   # noqa: E402  (folder(), the pooled projection and weights, the worlds)

HY = json.load(open(H.S + '/hue-screen/hybrid.json', encoding='utf-8'))
NAMES = H.P['features_all']
MEAN, SD = H.P['mean_all'], H.P['sd_all']
ZCLIP, K = HY['zclip'], H.K
FEATURES, WEIGHTS, FITTING = HY['features'], HY['weights'], HY['fitting_scores_sorted']
HYBRID_INDEX = [NAMES.index(n) for n in FEATURES]
BIAS = [q.get('bias', 0.0) for q in H.W['parameters']]
INPUTS = [[(NAMES.index(i['feature']), i['weight']) for i in q['inputs'] if i['used']] for q in H.W['parameters']]


def vector_of(x):
    """VectorOf: z, the brain markers' shrink, the clip, the logistic; then the hybrid step when the dial is 2."""
    links = math.exp(x[NAMES.index('brain_log_links')]) - 1.0
    shrink = links / (links + K)
    z = [0.0] * len(NAMES)
    for j, name in enumerate(NAMES):
        z[j] = (x[j] - MEAN[j]) / SD[j] if SD[j] > 0.0 else 0.0
        if name.startswith('brain_w_'):
            z[j] *= shrink
        z[j] = max(-ZCLIP, min(ZCLIP, z[j]))
    v = [0.0] * len(INPUTS)
    for k in range(len(v)):
        if not INPUTS[k]:
            v[k] = 0.5
            continue
        s = BIAS[k]
        for f, w in INPUTS[k]:
            s += w * z[f]
        v[k] = 1.0 / (1.0 + math.exp(-s))
    apply_hybrid(v, z)
    return v


RANK_TOLERANCE = 1e-12


def lower_bound(sorted_, x, or_equal):
    # A score within the tolerance of a fitting score is that score: another summation order moves it by a last bit.
    x = x + RANK_TOLERANCE if or_equal else x - RANK_TOLERANCE
    low, high = 0, len(sorted_)
    while low < high:
        mid = low + (high - low) // 2
        if (sorted_[mid] <= x) if or_equal else (sorted_[mid] < x):
            low = mid + 1
        else:
            high = mid
    return low


def apply_hybrid(v, z):
    today_hue = v[0]
    score = 0.0
    for i in range(len(WEIGHTS)):
        score += WEIGHTS[i] * z[HYBRID_INDEX[i]]
    lo = lower_bound(FITTING, score, False)
    hi = lower_bound(FITTING, score, True)
    v[0] = (lo + hi) / 2.0 / len(FITTING)
    v[1] = (v[1] + today_hue) / 2.0


def main():
    ref = {}
    with open(H.S + '/hue-screen/hybrid-ref.csv', encoding='utf-8', newline='') as f:
        for r in csv.DictReader(f):
            ref[(r['world'], r['id'])] = (float(r['base_hue']), float(r['base_saturation']))
    worst = [0.0, 0.0]
    worst_at = [None, None]
    n = 0
    seen = set()
    for w in H.WORLDS:
        with open(H.folder(w) + '/features.csv', encoding='utf-8', newline='') as f:
            for r in csv.DictReader(f):
                if float(r['share_photosynthetic']) >= 0.5:
                    continue
                key = (w, r['id'])
                x = [float(r[name]) for name in NAMES]
                v = vector_of(x)
                seen.add(key)
                rh, rs = ref[key]
                for c, (a, b) in enumerate(((v[0], rh), (v[1], rs))):
                    d = abs(a - b)
                    if d > worst[c]:
                        worst[c], worst_at[c] = d, key
                n += 1
    print('%d non-producers checked; reference has %d, %d of its rows unseen' % (n, len(ref), len(set(ref) - seen)))
    print('worst base_hue difference %.3e at %s' % (worst[0], worst_at[0]))
    print('worst base_saturation difference %.3e at %s' % (worst[1], worst_at[1]))
    print('(the reference csv holds nine decimals, so a difference up to 5e-10 is its rounding)')


if __name__ == '__main__':
    main()
