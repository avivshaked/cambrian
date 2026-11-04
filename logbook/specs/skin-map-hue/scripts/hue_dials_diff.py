"""Did a dial move any pixel? For each still of step Ӂ/Ӄ, the largest and the 99th-percentile channel difference (0-255) against
the hybrid (map2), and how many opaque pixels moved by more than 2 levels.

    python -I scratch/skin-map/hue_dials_diff.py turn1 floor deepen05
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

D = 'D:/Projects/experiments/evolution-simulator/scratch/skin-map/hue-dials'
for look in sys.argv[1:]:
    moved, total, worst = 0, 0, 0
    rows = []
    for a in sorted(glob.glob(D + '/map2/png/id-*.png')):
        b = a.replace('/map2/', '/%s/' % look)
        if not os.path.exists(b):
            continue
        x = np.asarray(Image.open(a).convert('RGBA'), dtype=np.int16)
        y = np.asarray(Image.open(b).convert('RGBA'), dtype=np.int16)
        m = (x[..., 3] > 127) | (y[..., 3] > 127)
        d = np.abs(x - y)[..., :3].max(axis=-1)[m]
        moved += int((d > 2).sum())
        total += int(m.sum())
        worst = max(worst, int(d.max()) if d.size else 0)
        rows.append((os.path.basename(a), int(d.max()) if d.size else 0, float(np.percentile(d, 99)) if d.size else 0.0))
    print('%s: %d of %d opaque pixels moved over 2 levels (%.2f%%), largest %d' % (look, moved, total, 100.0 * moved / max(total, 1), worst))
    for name, mx, p99 in sorted(rows, key=lambda r: -r[2])[:4]:
        print('   %s max %d p99 %.0f' % (name, mx, p99))
