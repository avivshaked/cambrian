"""v6's predicted colours, before any render: the 30 sheet animals and the family of six as shaded swatches, at the design's
defaults and with the brown bias at 1. Predicted colours only. The theatre's light, tissue, pattern and rim are not drawn, so a
render will read lighter and busier than these.

Ranks: the hue rank is hybrid-ref.csv's base hue (the C#'s own); the value rank is expected.csv's (fit_value.py); the chroma
and sheen ranks are the third and fourth generalised eigenvectors of hue_screen's (Q, S) problem, each ranked among the living
bodies, with the sign set so the largest-magnitude weight is positive.

Mapping (design of 2026-10-30): hue on the arc 170 -> 360 -> 55 degrees (HSV hue of the turned guild colour, taken here as the
OKLab hue of an sRGB colour of that HSV hue); OKLab L = lerp(0.12, 0.90, value rank); chroma = 0.22 * chroma rank ^ 1.5; sheen
factor = lerp(0.5, 4.0, sheen rank ^ 2) on a base sheen of 0.1. Brown bias: weight w, a raised cosine on hue 42 +- 30 degrees;
L -> lerp(L, min(L, 0.38), w), C -> lerp(C, 0.075, w).

    python -I -B scratch/skin-map/full-colour/v6-preview/swatches.py
"""
import colorsys
import csv
import json
import math
import sys

import numpy as np
from PIL import Image, ImageDraw

R = 'D:/Projects/experiments/evolution-simulator'
SK = R + '/scratch/skin-map'
sys.path.insert(0, SK)
sys.path.insert(0, SK + '/full-colour')
import hue_screen as S  # noqa: E402
from fit_value import average_ranks  # noqa: E402
from hue_dials import BG, DIM, INK, font  # noqa: E402

OUT = SK + '/full-colour/v6-preview'
FAMILY = [36766, 38847, 40020, 43308, 43309, 42495]
# Mostly plant tissue: the theatre draws these in the producers' colours (the owner's ruling of 2026-10-29), as every render so far did.
PLANT = {'54039', '54842', '88392', '74768', '56782', '55485'}


def oklab_to_srgb(L, a, b):
    l_, m_, s_ = L + 0.3963377774 * a + 0.2158037573 * b, L - 0.1055613458 * a - 0.0638541728 * b, L - 0.0894841775 * a - 1.2914855480 * b
    l, m, s = l_ ** 3, m_ ** 3, s_ ** 3
    rgb = np.array([4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                    -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s])
    return rgb


def srgb_to_oklab_hue(r, g, b):
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in (r, g, b)]
    l = np.cbrt(0.4122214708 * lin[0] + 0.5363325363 * lin[1] + 0.0514459929 * lin[2])
    m = np.cbrt(0.2119034982 * lin[0] + 0.6806995451 * lin[1] + 0.1073969566 * lin[2])
    s = np.cbrt(0.0883024619 * lin[0] + 0.2817188376 * lin[1] + 0.6299787005 * lin[2])
    a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s
    bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s
    return math.atan2(bb, a)


def colour(L, C, h):
    """Linear sRGB in gamut, chroma bisected down where needed."""
    lo, hi = 0.0, C
    rgb = oklab_to_srgb(L, C * math.cos(h), C * math.sin(h))
    if rgb.min() < -1e-5 or rgb.max() > 1 + 1e-5:
        for _ in range(14):
            mid = (lo + hi) / 2
            t = oklab_to_srgb(L, mid * math.cos(h), mid * math.sin(h))
            if t.min() >= -1e-5 and t.max() <= 1 + 1e-5:
                lo = mid
            else:
                hi = mid
        rgb = oklab_to_srgb(L, lo * math.cos(h), lo * math.sin(h))
    return np.clip(rgb, 0, 1)


def gamma(x):
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(np.maximum(x, 0), 1 / 2.4) - 0.055)


def sphere(lin_rgb, sheen, size):
    """A lit ball: Lambert from the upper left, a dim fill, and a specular lobe scaled by the sheen."""
    y, x = np.mgrid[-1:1:size * 1j, -1:1:size * 1j]
    r2 = x * x + y * y
    inside = r2 <= 1
    z = np.sqrt(np.clip(1 - r2, 0, 1))
    n = np.stack([x, -y, z], -1)
    ldir = np.array([-0.5, 0.6, 0.62]); ldir /= np.linalg.norm(ldir)
    lam = np.clip(n @ ldir, 0, 1)
    h = ldir + np.array([0, 0, 1.0]); h /= np.linalg.norm(h)
    spec = np.clip(n @ h, 0, 1) ** 40
    col = lin_rgb[None, None, :] * (0.18 + 0.95 * lam[..., None]) + min(1.0, sheen) * 1.6 * spec[..., None]
    img = np.zeros((size, size, 3))
    img[inside] = np.clip(gamma(col[inside]), 0, 1)
    out = (np.array(BG) / 255.0) * np.ones((size, size, 3))
    out[inside] = img[inside]
    return Image.fromarray((out * 255).astype(np.uint8))


def main():
    bodies = S.load()
    kin, _ = S.pairs_of(bodies, set(S.FIT_WORLDS))
    X = np.array([b['lin'] for b in bodies.values() if b['alive'] and b['world'] in S.FIT_WORLDS])
    D = np.array([c['lin'] - p['lin'] for p, c in kin])
    Q = np.cov(D, rowvar=False) / 2.0 + S.RIDGE * np.eye(len(S.LINEAGE))
    vals, vecs = np.linalg.eig(np.linalg.solve(Q, np.cov(X, rowvar=False)))
    order = np.argsort(-vals.real)
    alive = [k for k, b in bodies.items() if b['alive']]
    Lmat = np.array([bodies[k]['lin'] for k in alive])
    rk = {}
    for name, n in (('chroma', 2), ('sheen', 3)):
        w = vecs[:, order[n]].real
        if w[np.argmax(np.abs(w))] < 0:
            w = -w
        # Middle rank among the living bodies' sorted scores, as the C#'s LowerBound does, so a body not alive at the snapshot
        # (the family's elders) is ranked too.
        srt = np.sort(Lmat @ w)
        rk[name] = {}
        for k, b in bodies.items():
            s = float(np.asarray(b['lin']) @ w)
            lo, hi = np.searchsorted(srt, s - 1e-12, 'left'), np.searchsorted(srt, s + 1e-12, 'right')
            rk[name][(k[0], str(k[1]))] = (lo + hi) / 2.0 / len(srt)

    exp = {r['id']: r for r in csv.DictReader(open(SK + '/full-colour/expected.csv'))}
    picks = json.load(open(SK + '/hue-spread/picks.json'))
    ids = [(p['world'], str(p['id'])) for p in picks] + [('r62-h6202', str(i)) for i in FAMILY]

    rows = []
    for world, i in ids:
        e = exp[i]
        hr, vr = float(e['hue_rank']), float(e['value_rank'])
        cr, sr = rk['chroma'][(world, i)], rk['sheen'][(world, i)]
        hdeg = (170 + 245 * hr) % 360
        hok = srgb_to_oklab_hue(*colorsys.hsv_to_rgb(hdeg / 360, 1, 1))
        L = 0.12 + (0.90 - 0.12) * vr
        C = 0.22 * cr ** 1.5
        sheen = 0.1 * (0.5 + 3.5 * sr ** 2)
        d = min(abs(hdeg - 42), 360 - abs(hdeg - 42))
        wb = 0.5 * (1 + math.cos(math.pi * d / 30)) if d < 30 else 0.0
        Lb, Cb = L + (min(L, 0.38) - L) * wb, C + (0.075 - C) * wb
        rows.append(dict(world=world, id=i, hue=hdeg, L=L, C=C, sheen=sheen, Lb=Lb, Cb=Cb, hok=hok,
                         rgb=colour(L, C, hok), rgbb=colour(Lb, Cb, hok)))

    with open(OUT + '/predicted.csv', 'w', newline='', encoding='utf-8') as f:
        wr = csv.writer(f)
        wr.writerow(['world', 'id', 'hue_deg', 'L', 'C', 'sheen', 'L_brown', 'C_brown'])
        for r in rows:
            wr.writerow([r['world'], r['id'], '%.1f' % r['hue'], '%.3f' % r['L'], '%.3f' % r['C'], '%.3f' % r['sheen'], '%.3f' % r['Lb'], '%.3f' % r['Cb']])

    size, pad, label, per_row, head = 120, 10, 34, 6, 120
    n30 = rows[:30]
    cell = 2 * size + 6 + pad
    sheet = Image.new('RGB', (per_row * cell + pad, head + 5 * (size + label + pad) + 70 + size + label + pad), BG)
    d = ImageDraw.Draw(sheet)
    d.text((pad, 10), 'Predicted colours for v6, not renders: each animal as a plain lit ball, left at the design\'s defaults, right with the brown bias',
           fill=INK, font=font(20))
    d.text((pad, 40), 'The theatre adds its own light, tissue, pattern, edge and maze, so the renders will read lighter and busier than these.',
           fill=DIM, font=font(15))
    d.text((pad, 62), 'A bright highlight is the sheen axis: grey with a strong highlight is the silver. The six marked in orange are mostly plant tissue and keep plant colours.',
           fill=DIM, font=font(15))
    small = font(12)
    for k, r in enumerate(n30):
        x0, y0 = pad + (k % per_row) * cell, head + (k // per_row) * (size + label + pad)
        sheet.paste(sphere(r['rgb'], r['sheen'], size), (x0, y0))
        sheet.paste(sphere(r['rgbb'], r['sheen'], size), (x0 + size + 6, y0))
        d.text((x0, y0 + size + 2), '%s %s%s' % (r['world'], r['id'], '  plant body: drawn as a plant' if r['id'] in PLANT else ''),
               fill=(230, 170, 90) if r['id'] in PLANT else DIM, font=small)
    y0 = head + 5 * (size + label + pad) + 30
    d.text((pad, y0 - 26), 'One family of r62-h6202 (grandparent, two children, three grandchildren), defaults then brown bias:', fill=INK, font=font(15))
    for j, r in enumerate(rows[30:]):
        x0 = pad + j * cell
        sheet.paste(sphere(r['rgb'], r['sheen'], size), (x0, y0))
        sheet.paste(sphere(r['rgbb'], r['sheen'], size), (x0 + size + 6, y0))
        d.text((x0, y0 + size + 2), r['id'], fill=DIM, font=small)
    sheet.save(OUT + '/v6-predicted-colours.png')
    print('wrote', OUT + '/v6-predicted-colours.png', sheet.size)
    grey = sum(r['C'] < 0.04 for r in n30)
    dark = sum(r['L'] < 0.30 for r in n30)
    pale = sum(r['L'] > 0.80 for r in n30)
    brown = sum(r['Cb'] < 0.11 and r['Lb'] < 0.45 and 15 <= r['hue'] <= 70 for r in n30)
    glossy = sum(r['sheen'] > 0.2 for r in n30)
    print('of 30: grey (C<0.04) %d, dark (L<0.30) %d, pale (L>0.80) %d, glossy (sheen>0.2) %d, brown with bias %d' % (grey, dark, pale, glossy, brown))
    print('green band 75-165:', sum(75 <= r['hue'] <= 165 for r in n30))


if __name__ == '__main__':
    main()
