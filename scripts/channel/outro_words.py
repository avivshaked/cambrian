"""Draws the end screen's words over a folder of filmed frames, to match the designer's outro.html.

    python -I outro_words.py <in dir> <out dir> [--line TEXT] [--guides] [--frames 0,300,599] [--fonts DIR]

Input is *.png sorted by name, 16:9 at any size, 30 fps, frame 0 at 0 s; output keeps the names.
--frames picks positions in the sorted list (not file numbers). Sizes below are at 1080p and scale by H/1080.
CAMBRIAN settles over 1.2 s (starts 2.5% larger, 5 px blur, 0.7 opacity); the line rises 14 px and fades
in over 1.0-2.4 s; everything is fixed from 2.4 s. --guides adds the check overlay of outro-3.
"""
import argparse, json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)  # -I leaves the script's folder off the path
import overlay as O

REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
DEFAULT_FONTS = os.path.join(REPO, "scratch", "owner", "r67-graphics-design-brief", "design", "fonts")
LAYOUT = json.load(open(os.path.join(HERE, "endscreen_layout.json")))
FPS = 30
MARK = "C" + chr(0x39B) + "MBRI" + chr(0x39B) + "N"
DEFAULT_LINE = "The sea is still evolving."
WATER = (3, 24, 33)
FONT_FILES = {"sans": "IBMPlexSans-Regular.ttf", "serif": "InstrumentSerif-Italic.ttf",
              "mono": "IBMPlexMono-Regular.ttf"}


def load_font(folder, key, size):
    path = os.path.join(folder, FONT_FILES[key])
    if not os.path.isfile(path):
        sys.exit("outro_words: font file missing: %s (looked in %s; pass --fonts DIR)" % (FONT_FILES[key], folder))
    return ImageFont.truetype(path, size)


# ---- layers are premultiplied float RGBA, shape (H, W, 4) ----
def over(base, top):
    return top + base * (1.0 - top[..., 3:4])


def flat(W, H, color, mask):
    """A solid colour through an 8-bit-or-float mask in 0..1, premultiplied."""
    m = np.asarray(mask, np.float32)[..., None]
    out = np.empty((H, W, 4), np.float32)
    out[..., :3] = np.asarray(color, np.float32) / 255.0 * m
    out[..., 3:] = m
    return out


def blurred(mask_l, sigma):
    return np.asarray(mask_l.filter(ImageFilter.GaussianBlur(sigma)), np.float32) / 255.0 if sigma > 0 \
        else np.asarray(mask_l, np.float32) / 255.0


def shifted(a, dy):
    if dy == 0:
        return a
    out = np.zeros_like(a)
    if dy > 0:
        out[dy:] = a[:-dy]
    else:
        out[:dy] = a[-dy:]
    return out


def text_mask(W, H, font, text, top, track, indent):
    """Plex-style tracked centred line, CSS-like: each glyph followed by `track`, line indented by `indent`."""
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    adv = [font.getlength(c) + track for c in text]
    total = indent + sum(adv)
    x = (W - total) / 2 + indent
    base = top + font.getmetrics()[0]
    for c, a in zip(text, adv):
        d.text((x, base), c, font=font, fill=255, anchor="ls")
        x += a
    return m


def element(W, H, mask, ink, glow, shadow, s):
    """CSS text-shadow stack: glow over shadow, text on top. glow/shadow = (rgb, alpha, blur_px, dy_px)."""
    layer = np.zeros((H, W, 4), np.float32)
    for (rgb, alpha, blur, dy) in (shadow, glow):
        b = blurred(mask, blur * s / 2.0) * alpha
        layer = over(layer, flat(W, H, rgb, shifted(b, int(round(dy * s)))))
    return over(layer, flat(W, H, ink, np.asarray(mask, np.float32) / 255.0))


def shade_alpha(W, H, s):
    """The darkened ellipse of the HTML: left 260, top 60, 1400x330, 0.5 -> 0, blur 30."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    cx, cy = (260 + 700) * s, (60 + 165) * s
    r = np.sqrt(((xx - cx) / (700 * s)) ** 2 + ((yy - cy) / (165 * s)) ** 2)
    a = np.clip(1 - r, 0, 1) * 0.5
    m = Image.fromarray(np.uint8(np.round(a * 510)), "L").filter(ImageFilter.GaussianBlur(30 * s))
    return np.asarray(m, np.float32) / 510.0


def transform_about(arr, cx, cy, scale):
    """Scale a premultiplied RGBA array about (cx, cy)."""
    H, W = arr.shape[:2]
    chans = [Image.fromarray(arr[..., i], "F").transform(
        (W, H), Image.AFFINE, (1 / scale, 0, cx - cx / scale, 0, 1 / scale, cy - cy / scale), Image.BICUBIC)
        for i in range(4)]
    return np.stack([np.asarray(c) for c in chans], -1)


def gauss_np(arr, sigma):
    r = int(np.ceil(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2).astype(np.float32)
    k /= k.sum()
    out = arr
    for axis in (0, 1):
        pad = [(0, 0)] * 4
        pad[axis] = (r, r)
        p = np.pad(out, pad[:3] if out.ndim == 3 else pad, mode="constant")
        acc = np.zeros_like(out)
        n = out.shape[axis]
        for i, w in enumerate(k):
            sl = [slice(None)] * 3
            sl[axis] = slice(i, i + n)
            acc += w * p[tuple(sl)]
        out = acc
    return out


class Words:
    def __init__(self, W, H, fonts, line):
        self.W, self.H, self.s = W, H, H / 1080.0
        s = self.s
        sans = load_font(fonts, "sans", 84 * s)
        serif = load_font(fonts, "serif", 64 * s)
        em = 84 * s
        self.mark = element(W, H, text_mask(W, H, sans, MARK, 140 * s, 0.38 * em, 0.38 * em),
                            (0xe6, 0xfb, 0xf6), ((159, 245, 230), 0.45, 18, 0), ((3, 24, 33), 0.9, 26, 2), s)
        asc, desc = sans.getmetrics()
        self.mark_c = (W / 2, 140 * s + (asc + desc) / 2)
        self.line = element(W, H, text_mask(W, H, serif, line, 246 * s, 0, 0),
                            (0xd9, 0xfb, 0xf4), ((159, 245, 230), 0.3, 14, 0), ((3, 24, 33), 0.95, 22, 2), s)
        a = shade_alpha(W, H, s)
        self.shade = a[..., None]
        self.cache = None

    def overlay(self, f):
        t = f / FPS
        if t >= 2.4 and self.cache is not None:
            return self.cache
        k = O.smoother(t / 1.2)
        m = self.mark
        scale = 1.025 - 0.025 * k
        if k < 1:
            if scale != 1:
                m = transform_about(m, self.mark_c[0], self.mark_c[1], scale)
            m = gauss_np(m, 5 * self.s * (1 - k))
            m = m * (0.7 + 0.3 * k)
        la = O.smoother((t - 1.0) / 1.4)
        l = shifted(self.line, int(round(14 * self.s * (1 - la)))) * la if la < 1 else self.line
        out = over(m, l)
        if t >= 2.4:
            self.cache = out
        return out

    def apply(self, plate, f, guides):
        a = self.shade
        base = np.asarray(plate, np.float32) / 255.0
        base = base * (1 - a) + np.array(WATER, np.float32) / 255.0 * a
        ov = self.overlay(f)
        res = ov[..., :3] + base * (1 - ov[..., 3:4])
        img = Image.fromarray(np.uint8(np.clip(np.round(res * 255), 0, 255)), "RGB")
        return img


def dashed(d, p0, p1, dash, gap, width, fill):
    (x0, y0), (x1, y1) = p0, p1
    n = max(abs(x1 - x0), abs(y1 - y0))
    if n == 0:
        return
    ux, uy = (x1 - x0) / n, (y1 - y0) / n
    pos = 0
    while pos < n:
        e = min(pos + dash, n)
        d.line([(x0 + ux * pos, y0 + uy * pos), (x0 + ux * e, y0 + uy * e)], fill=fill, width=width)
        pos += dash + gap


def draw_guides(img, fonts):
    W, H = img.size
    s = H / 1080.0
    pink, mint = (255, 111, 174), (159, 245, 230)
    over_l = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(over_l)
    sw = max(1, int(round(3 * s)))
    rects = [(x * W, y * H, (x + w) * W, (y + h) * H) for x, y, w, h in LAYOUT["tiles"]]
    rects.append((0, 0.9 * H, W, H))
    for r in rects:
        d.rectangle(r, fill=pink + (31,))
    cx, cy, rad = 0.5 * W, 0.530 * H, 149 / 1920 * W
    d.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], fill=pink + (31,))
    for r in rects:
        x0, y0, x1, y1 = r
        for p, q in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
            dashed(d, p, q, 14 * s, 8 * s, sw, pink + (255,))
    # circle outline
    import math
    steps = 120
    pts = [(cx + rad * math.cos(2 * math.pi * i / steps), cy + rad * math.sin(2 * math.pi * i / steps))
           for i in range(steps + 1)]
    for i in range(0, steps, 2):
        d.line([pts[i], pts[i + 1]], fill=pink + (255,), width=sw)
    band = (40 / 1920 * W, 40 * s, 1880 / 1920 * W, 380 * s)
    bx0, by0, bx1, by1 = band
    for p, q in (((bx0, by0), (bx1, by0)), ((bx1, by0), (bx1, by1)), ((bx1, by1), (bx0, by1)), ((bx0, by1), (bx0, by0))):
        dashed(d, p, q, 4 * s, 8 * s, max(1, int(round(2 * s))), mint + (255,))
    mono = load_font(fonts, "mono", 26 * s)
    d.text((130 / 1920 * W, 436 * s), "video tile", font=mono, fill=pink + (255,), anchor="ls")
    d.text((1207 / 1920 * W, 436 * s), "video tile", font=mono, fill=pink + (255,), anchor="ls")
    d.text((cx, 580 * s), "subscribe", font=mono, fill=pink + (255,), anchor="ms")
    d.text((30 / 1920 * W, 1030 * s), "player controls", font=mono, fill=pink + (255,), anchor="ls")
    d.text((56 / 1920 * W, 72 * s), "words", font=mono, fill=mint + (255,), anchor="ls")
    return Image.alpha_composite(img.convert("RGBA"), over_l).convert("RGB")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("indir")
    ap.add_argument("outdir")
    ap.add_argument("--line", default=DEFAULT_LINE)
    ap.add_argument("--guides", action="store_true")
    ap.add_argument("--frames", default=None, help="comma list of positions in the sorted list")
    ap.add_argument("--fonts", default=DEFAULT_FONTS)
    a = ap.parse_args()
    names = sorted(n for n in os.listdir(a.indir) if n.lower().endswith(".png"))
    if not names:
        sys.exit("outro_words: no *.png in %s" % a.indir)
    picks = range(len(names)) if a.frames is None else [int(x) for x in a.frames.split(",")]
    os.makedirs(a.outdir, exist_ok=True)
    W = H = words = None
    for i in picks:
        if i < 0 or i >= len(names):
            sys.exit("outro_words: frame %d out of range (0..%d)" % (i, len(names) - 1))
        plate = Image.open(os.path.join(a.indir, names[i])).convert("RGB")
        if words is None or plate.size != (W, H):
            W, H = plate.size
            words = Words(W, H, a.fonts, a.line)
        img = words.apply(plate, i, a.guides)
        if a.guides:
            img = draw_guides(img, a.fonts)
        img.save(os.path.join(a.outdir, names[i]))
    print("wrote %d frames to %s" % (len(list(picks)), a.outdir))


if __name__ == "__main__":
    main()
