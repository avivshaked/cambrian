"""Texture on the near rock's shaft-free left part (creature frame): local s.d. in 16 px blocks after
removing a 65 px box blur (so the rock's broad shading and the veil's gradient do not count), absolute
and relative to the mean, plus the mean colour.  python rocktex.py label=path ..."""
import sys
import numpy as np
from PIL import Image

BOX = (1650, 1640, 2550, 1900)


def box(y, r):
    p = np.pad(y, r, mode='edge')
    c = np.cumsum(np.cumsum(p, 0), 1)
    c = np.pad(c, ((1, 0), (1, 0)))
    n = 2 * r + 1
    s = c[n:, n:] - c[:-n, n:] - c[n:, :-n] + c[:-n, :-n]
    return s / (n * n)


for arg in sys.argv[1:]:
    label, path = arg.split('=', 1)
    a = np.asarray(Image.open(path).convert('RGB').crop(BOX), dtype=np.float64)
    y = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    yb = box(y, 32)
    hp = y - yb
    h, w = (y.shape[0] // 16) * 16, (y.shape[1] // 16) * 16
    loc = hp[:h, :w].reshape(h // 16, 16, w // 16, 16).std(axis=(1, 3)).mean()
    m = a.reshape(-1, 3).mean(0)
    print(f'{label:<10} mean {y.mean():5.1f} rgb {m[0]:5.1f} {m[1]:5.1f} {m[2]:5.1f}  tex {loc:5.2f}  '
          f'tex/mean {loc / y.mean():.3f}  broad sd {yb.std():5.2f}')
