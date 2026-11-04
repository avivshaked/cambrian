"""v6 multicolour, predicted, not rendered: each animal as a schematic of three parts (trunk at the root, a limb halfway, a tip at
the end), coloured by the design of 2026-10-30, with spots on the trunk in the pattern's second colour. Left figure: the pattern half
the arc away (about the complement, never green); right: 0.08 of the arc with the lightness flip, as Chromodoris's dark stripe on purple.

Per part (PART_COLOUR 1): L += dL * 0.45 * t, hue moved along the green-free arc by dH * 0.6 * t of it (reflected at its ends), t = 0, 0.5, 1 along the body; dL = +1 for a dark body
(value rank under 0.5), else -1; dH = +1 if the chroma rank is at least 0.5, else -1. The node's own term (elongation) is left at
its middle here, since a schematic has no real parts. Pattern tint: hue turned, L flipped by 0.45 across the middle, chroma at
least 0.10. Body colours as swatches.py predicts (lightness, chroma, sheen axes; hue on the arc 170 -> 55).

    python -I -B scratch/skin-map/full-colour/v6-preview/multicolour.py
"""
import colorsys
import csv
import math
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map/full-colour/v6-preview')
from hue_dials import BG, DIM, INK, font  # noqa: E402
from swatches import PLANT, colour, gamma, srgb_to_oklab_hue  # noqa: E402

D = 'D:/Projects/experiments/evolution-simulator/scratch/skin-map/full-colour/v6-preview'
V6 = 'D:/Projects/experiments/evolution-simulator/scratch/skin-map/full-colour/v6/expected.csv'


def ball(img, cx, cy, rx, ry, lin, sheen, spots=None):
    """A lit ellipse drawn into the float image, with optional spots of a second colour."""
    h, w, _ = img.shape
    y, x = np.mgrid[0:h, 0:w]
    nx, ny = (x - cx) / rx, (y - cy) / ry
    r2 = nx * nx + ny * ny
    inside = r2 <= 1
    z = np.sqrt(np.clip(1 - r2, 0, 1))
    ldir = np.array([-0.5, 0.6, 0.62]); ldir /= np.linalg.norm(ldir)
    lam = np.clip(-0.5 * nx + 0.6 * -ny + 0.62 * z, 0, 1) / np.linalg.norm([-0.5, 0.6, 0.62]) * np.linalg.norm(ldir)
    hv = ldir + np.array([0, 0, 1.0]); hv /= np.linalg.norm(hv)
    spec = np.clip(nx * hv[0] - ny * hv[1] + z * hv[2], 0, 1) ** 40
    base = np.broadcast_to(lin, (h, w, 3)).copy()
    if spots is not None:
        tint, seed = spots
        rng = np.random.default_rng(seed)
        for _ in range(9):
            sx, sy = rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6)
            m = ((nx - sx) ** 2 + (ny - sy) ** 2) < 0.045
            base[m & inside] = tint
    col = base * (0.18 + 0.95 * lam[..., None]) + min(1.0, sheen) * 1.6 * spec[..., None]
    img[inside] = np.clip(gamma(col[inside]), 0, 1)


def main():
    rows = list(csv.DictReader(open(V6, encoding='utf-8')))
    key = {k.lower(): k for k in rows[0]}
    print('columns', list(rows[0])[:14])

    def g(r, *names):
        for n in names:
            if n in key:
                return float(r[key[n]])
        raise KeyError(names)

    figs = []
    for r in rows:
        hr = g(r, 'hue_rank')
        hdeg, L, C = g(r, 'hue_deg', 'hue'), g(r, 'oklab_l'), g(r, 'oklab_c')
        vr, cr = g(r, 'value_rank'), g(r, 'chroma_rank')
        sheen = 0.1 * g(r, 'sheen_factor', 'sheen_x', 'sheen')
        dL = 1 if vr < 0.5 else -1
        dH = 1 if cr >= 0.5 else -1
        parts = []
        for t in (0.0, 0.5, 1.0):
            pp = hr + dH * 0.6 * t
            pp = -pp if pp < 0 else (2 - pp if pp > 1 else pp)
            hp = 170 + 245 * pp
            hok = srgb_to_oklab_hue(*colorsys.hsv_to_rgb((hp % 360) / 360, 1, 1))
            parts.append(colour(min(0.95, max(0.05, L + dL * 0.45 * t)), C, hok))
        tints = []
        for share in (0.5, 0.08):
            hok = srgb_to_oklab_hue(*colorsys.hsv_to_rgb(((170 + 245 * ((hr + share) % 1.0)) % 360) / 360, 1, 1))
            Lt = min(0.95, L + 0.45) if L < 0.5 else max(0.05, L - 0.45)
            tints.append(colour(Lt, max(C, 0.10), hok))
        figs.append(dict(id=r[key['id']], world=r[key['world']] if 'world' in key else '', parts=parts, tints=tints, sheen=sheen))

    fw, fh, pad, label, per_row, head = 230, 92, 10, 30, 3, 110
    cell = 2 * fw + 12 + pad
    n = len(figs)
    rows_n = (n + per_row - 1) // per_row
    W, H = per_row * cell + pad, head + rows_n * (fh + label + pad) + pad
    canvas = np.ones((H, W, 3)) * (np.array(BG) / 255.0)
    for k, f in enumerate(figs):
        x0, y0 = pad + (k % per_row) * cell, head + (k // per_row) * (fh + label + pad)
        for j, tint in enumerate(f['tints']):
            sub = canvas[y0:y0 + fh, x0 + j * (fw + 12):x0 + j * (fw + 12) + fw]
            ball(sub, 50, fh / 2, 44, 36, f['parts'][0], f['sheen'], spots=(tint, int(f['id'])))
            ball(sub, 122, fh / 2, 36, 18, f['parts'][1], f['sheen'])
            ball(sub, 186, fh / 2, 24, 13, f['parts'][2], f['sheen'])
    img = Image.fromarray((canvas * 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    d.text((pad, 10), 'Predicted multicolour for v6, not renders: each animal as trunk, limb and tip, with spots on the trunk in the pattern\'s second colour',
           fill=INK, font=font(19))
    d.text((pad, 38), 'Left of each pair: the pattern half the colour range away. Right: a small turn of hue with dark and light swapped, like the black stripe on Chromodoris.',
           fill=DIM, font=font(15))
    d.text((pad, 60), 'Real bodies have their own number and shape of parts; colour runs from the root to the tips the same way. Orange labels: plant bodies, drawn as plants.',
           fill=DIM, font=font(15))
    for k, f in enumerate(figs):
        x0, y0 = pad + (k % per_row) * cell, head + (k // per_row) * (fh + label + pad)
        plant = f['id'] in PLANT
        d.text((x0, y0 + fh + 2), '%s %s%s' % (f['world'], f['id'], '  plant body' if plant else ''),
               fill=(230, 170, 90) if plant else DIM, font=font(12))
    img.save(D + '/v6-predicted-multicolour.png')
    print('wrote', D + '/v6-predicted-multicolour.png', img.size)


if __name__ == '__main__':
    main()
