"""The second batch's measures and sheets (logbook/specs/underwater-look/brief-2.md, "How it is judged").

    python measure.py <new-set> [--first base3] [--views creature,reef,bed36] [--sheets]

Generalised from scratch/look-build/contrast.py: for each view, the old look (the stills the brief names),
the first batch (iter/<first>) and the new set (iter/<new>), read for mean luma, luma s.d., the 5-95
spread, the darkest tenth (p10), local contrast (mean s.d. in 32 px blocks), edge (mean |Laplacian|),
colourfulness, and the edge on a 100% crop of a body. The label strip (top 8% of rows) is left out.
The bar: spread and local at least 0.95 of the old look's, the mean between the old look's and the
first batch's, the crop's edge at least the old look's. --sheets writes, per view, old | first | new
at a third of the size, and a sheet of the three 100% crops.
"""
import argparse
import os
import numpy as np
from PIL import Image

LB = r'D:\Projects\experiments\evolution-simulator\scratch\look-build'
IT = LB + r'\iter'

# view: (old path, file name in a set, crop box (x, y, w, h) on a body at 100%)
VIEWS = {
    'creature': (r'stills-4\before\frame-000001.png', 'creature.png', (2420, 600, 768, 432)),
    'close':    (r'stills-2\before\r56-s2-t29005-ckpt-close.png', 'r56-s2-t29005-ckpt-close.png', (2300, 520, 768, 432)),
    'reef':     (r'stills-2\before\r56-s2-t29005-ckpt-reef.png', 'r56-s2-t29005-ckpt-reef.png', (2250, 120, 768, 432)),
    'sky27':    (r'stills-1\before\r57-s2-t27000-recon-sky.png', 'r57-s2-t27000-recon-sky.png', (1650, 500, 768, 432)),
    'sky36':    (r'stills-1\before\r57-s2-t36000-recon-sky.png', 'r57-s2-t36000-recon-sky.png', (1650, 500, 768, 432)),
    'bed27':    (r'stills-1\before\r57-s2-t27000-recon-bed.png', 'r57-s2-t27000-recon-bed.png', (1700, 560, 768, 432)),
    'bed36':    (r'stills-1\before\r57-s2-t36000-recon-bed.png', 'r57-s2-t36000-recon-bed.png', (1700, 560, 768, 432)),
}


def load(path):
    return np.asarray(Image.open(path).convert('RGB'), dtype=np.float64)


def luma(a):
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]


def lap(y):
    return np.abs(4 * y[1:-1, 1:-1] - y[:-2, 1:-1] - y[2:, 1:-1] - y[1:-1, :-2] - y[1:-1, 2:]).mean()


def metrics(a, crop):
    body = a[int(a.shape[0] * 0.08):]
    r, g, b = body[..., 0], body[..., 1], body[..., 2]
    y = luma(body)
    p5, p10, p95 = np.percentile(y, [5, 10, 95])
    h, w = (y.shape[0] // 32) * 32, (y.shape[1] // 32) * 32
    local = y[:h, :w].reshape(h // 32, 32, w // 32, 32).std(axis=(1, 3)).mean()
    rg, yb = r - g, 0.5 * (r + g) - b
    colour = np.hypot(rg.std(), yb.std()) + 0.3 * np.hypot(rg.mean(), yb.mean())
    x0, y0, cw, ch = crop
    c = luma(a[y0:y0 + ch, x0:x0 + cw])
    return dict(mean=y.mean(), sd=y.std(), spread=p95 - p5, p10=p10, local=local, edge=lap(y), colour=colour, crop=lap(c))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('new')
    ap.add_argument('--first', default='base3')
    ap.add_argument('--views', default=','.join(VIEWS))
    ap.add_argument('--sheets', action='store_true')
    ap.add_argument('--brief', action='store_true')
    args = ap.parse_args()

    keys = ['mean', 'sd', 'spread', 'p10', 'local', 'edge', 'colour', 'crop']
    print(f"{'view':<9} {'':>6} " + ' '.join(f'{k:>7}' for k in keys) + '  bar')
    for view in args.views.split(','):
        old_rel, name, crop = VIEWS[view]
        paths = [os.path.join(LB, old_rel), os.path.join(IT, args.first, name), os.path.join(IT, args.new, name)]
        if not all(os.path.exists(p) for p in paths):
            print(f'{view:<9} missing: ' + ', '.join(p for p in paths if not os.path.exists(p)))
            continue
        imgs = [load(p) for p in paths]
        ms = [metrics(a, crop) for a in imgs]
        o, f, n = ms
        if not args.brief:
            for tag, m in zip(('old', 'first', 'new'), ms):
                print(f'{view:<9} {tag:>6} ' + ' '.join(f'{m[k]:7.1f}' for k in keys))
        lo, hi = min(o['mean'], f['mean']), max(o['mean'], f['mean'])
        checks = [
            ('spread', n['spread'] >= 0.95 * o['spread']),
            ('local', n['local'] >= 0.95 * o['local']),
            ('mean', lo <= n['mean'] <= hi),
            ('crop', n['crop'] >= o['crop']),
        ]
        ratio = ' '.join(f"{n[k] / o[k]:7.2f}" if o[k] else '      -' for k in keys)
        if args.brief:
            print(f"{view:<9} mean {n['mean']:5.1f} [{lo:5.1f}-{hi:5.1f}] spread x{n['spread'] / o['spread']:4.2f} local x{n['local'] / o['local']:4.2f} "
                  f"p10 {n['p10']:5.1f} (old {o['p10']:4.1f}, first {f['p10']:4.1f}) crop x{n['crop'] / o['crop']:4.2f}  fails: "
                  + (','.join(k for k, ok in checks if not ok) or '-'))
        else:
            print(f"{'':<9} {'new/old':>6} {ratio}  " + ' '.join(('ok ' if ok else 'FAIL ') + k for k, ok in checks))
        if args.sheets:
            sheet(view, imgs, crop, args.new)


def sheet(view, imgs, crop, new):
    out = os.path.join(IT, new, 'sheets')
    os.makedirs(out, exist_ok=True)
    small = [Image.fromarray(a.astype(np.uint8)).resize((a.shape[1] // 3, a.shape[0] // 3), Image.LANCZOS) for a in imgs]
    w, h = small[0].size
    s = Image.new('RGB', (3 * w + 8, h), (255, 255, 255))
    for i, im in enumerate(small):
        s.paste(im, (i * (w + 4), 0))
    s.save(os.path.join(out, f'sbs-{view}.png'))
    x0, y0, cw, ch = crop
    crops = [Image.fromarray(a[y0:y0 + ch, x0:x0 + cw].astype(np.uint8)) for a in imgs]
    c = Image.new('RGB', (3 * cw + 8, ch), (255, 255, 255))
    for i, im in enumerate(crops):
        c.paste(im, (i * (cw + 4), 0))
    c.save(os.path.join(out, f'crop-{view}.png'))


if __name__ == '__main__':
    main()
