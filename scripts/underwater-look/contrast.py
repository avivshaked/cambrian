"""Contrast of the look's before-and-after stills: global (luma s.d., 5-95 spread), local (mean s.d. in
32 px blocks, mean |Laplacian|) and colour (Hasler-Suesstrunk colourfulness). Label strip at the top
excluded (top 8% of rows)."""
import sys
import numpy as np
from PIL import Image

R = r'D:\Projects\experiments\evolution-simulator\scratch\look-build'
PAIRS = [
    ('sky winter', r'stills-1\before\r57-s2-t27000-recon-sky.png', r'stills-2\after\r57-s2-t27000-recon-sky.png'),
    ('sky summer', r'stills-1\before\r57-s2-t36000-recon-sky.png', r'stills-2\after\r57-s2-t36000-recon-sky.png'),
    ('bed winter', r'stills-1\before\r57-s2-t27000-recon-bed.png', r'stills-2\after\r57-s2-t27000-recon-bed.png'),
    ('bed summer', r'stills-1\before\r57-s2-t36000-recon-bed.png', r'stills-2\after\r57-s2-t36000-recon-bed.png'),
    ('reef 0.045', r'stills-2\before\r56-s2-t29005-ckpt-reef.png', r'stills-2\after\r56-s2-t29005-ckpt-reef.png'),
    ('reef 0.03', r'stills-2\before\r56-s2-t29005-ckpt-reef.png', r'stills-3\scatter-0.03\r56-s2-t29005-ckpt-reef.png'),
    ('reef 0.02', r'stills-2\before\r56-s2-t29005-ckpt-reef.png', r'stills-3\scatter-0.02\r56-s2-t29005-ckpt-reef.png'),
    ('close', r'stills-2\before\r56-s2-t29005-ckpt-close.png', r'stills-2\after\r56-s2-t29005-ckpt-close.png'),
    ('creature', r'stills-4\before\frame-000001.png', r'stills-4\after\frame-000001.png'),
]


def metrics(path):
    a = np.asarray(Image.open(f'{R}\\{path}').convert('RGB'), dtype=np.float64)
    a = a[int(a.shape[0] * 0.08):]
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    p5, p95 = np.percentile(y, [5, 95])
    h, w = (y.shape[0] // 32) * 32, (y.shape[1] // 32) * 32
    blocks = y[:h, :w].reshape(h // 32, 32, w // 32, 32).std(axis=(1, 3)).mean()
    lap = np.abs(4 * y[1:-1, 1:-1] - y[:-2, 1:-1] - y[2:, 1:-1] - y[1:-1, :-2] - y[1:-1, 2:]).mean()
    rg, yb = r - g, 0.5 * (r + g) - b
    colour = np.hypot(rg.std(), yb.std()) + 0.3 * np.hypot(rg.mean(), yb.mean())
    return y.mean(), y.std(), p95 - p5, blocks, lap, colour


print(f"{'view':<11} {'':>6} {'mean':>6} {'sd':>6} {'5-95':>6} {'local':>6} {'edge':>6} {'colour':>6}")
for name, before, after in PAIRS:
    mb, ma = metrics(before), metrics(after)
    for tag, m in (('before', mb), ('after', ma)):
        print(f"{name:<11} {tag:>6} " + ' '.join(f'{v:6.1f}' for v in m))
    print(f"{'':<11} {'ratio':>6} " + ' '.join(f'{x / y:6.2f}' for x, y in zip(ma, mb)))
sys.stdout.flush()
