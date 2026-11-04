"""The hybrid hue's fixed numbers for the theatre's port (EVOSIM_THEATRE_SKIN_MAP=2), from hue_screen.py's rule exactly:

  lineage score  s = sum over LINEAGE of w_i z_i, z the pooled projection's z of the feature clipped to [-3, 3], a brain_w_ feature's
                 z times the brain shrink n / (n + K) with n = expm1(brain_log_links); w fitted on hue_screen.FIT_WORLDS
  base hue       the middle rank of s among the fitting set's sorted scores: (lo + hi) / 2 / n, lo and hi the left and right
                 insertion points of s in the sorted array (so ties share their middle rank)
  base saturation  (today's base saturation + sigmoid(today's base-hue logit)) / 2

Writes hue-screen/hybrid.json (features, weights, the sorted fitting scores) and hue-screen/hybrid-ref.csv (world, id, base hue,
base saturation for every non-producer of the ten worlds' features files), for gen_constants.py and the parity check.
"""
import csv
import json
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_screen as S   # noqa: E402


def main():
    bodies = S.load()
    w, ratio, centre = S.lineage_weights(bodies)
    w_orth, _, _ = S.lineage_weights(bodies, orthogonal=True)
    S.apply(bodies, w, centre, w_orth)
    scores = sorted(float(b['lin'] @ w) for b in bodies.values() if b['alive'])
    out = {
        'rule': 'hue_screen.py hybrid, 2026-10-30',
        'fit_worlds': S.FIT_WORLDS, 'ridge': S.RIDGE, 'spread_over_parent_child': ratio,
        'features': S.LINEAGE, 'weights': [float(x) for x in w], 'zclip': 3.0,
        'brain_shrink_links': S.H.K, 'fitting_scores_sorted': scores,
    }
    with open(S.OUT + '/hybrid.json', 'w', encoding='utf-8') as f:
        json.dump(out, f, indent=1)
    with open(S.OUT + '/hybrid-ref.csv', 'w', encoding='utf-8', newline='') as f:
        wr = csv.writer(f)
        wr.writerow(['world', 'id', 'alive', 'base_hue', 'base_saturation', 'today_base_hue', 'today_base_saturation'])
        for b in bodies.values():
            v, t = b['v']['hybrid'], b['v']['today']
            wr.writerow([b['world'], b['id'], int(b['alive']), '%.9f' % v[0], '%.9f' % v[1], '%.9f' % t[0], '%.9f' % t[1]])
    print('fitting scores', len(scores), 'features', len(S.LINEAGE), 'rows', len(bodies))


if __name__ == '__main__':
    main()
