"""The title over the opener's sky hold, as its own layer: the timing and the geometry sea_dive.py's --layer title and
--layer sky use, and the composite that lays the words over the sea frames already rendered.

    python -I scripts/channel/title_comp.py comp  --sea DIR --words DIR --sky DIR --out DIR --frames 0-130 [--check]
    python -I scripts/channel/title_comp.py check --sea DIR --out DIR --words DIR --frames 0-130

The sea frames (8-bit, <sea-prefix>NNNN.png) are never rendered again. The words (--layer title) are 16-bit RGBA with
straight alpha, the sea a holdout; the sky matte (--layer sky) is 16-bit RGBA whose alpha is 1 where the camera sees
sky and 0 where it sees sea. Per frame, with t = frame / fps:

    words'  = gaussian_blur(words, sigma(t)), premultiplied, sigma = BLUR_PX at 1080 rows (scaled by the frame's rows)
              at t = 0, falling to 0 along CAMBRIAN's arrival curve by ARRIVE_S
    A       = alpha(words') * matte
    out     = colour(words') * matte + sea * (1 - A)             rounded to 8 bits

The matte multiplies after the blur, so the horizon edge stays sharp. Where A is 0 the output is the sea frame's
byte. The layers are read through Pillow, which keeps the high byte of a 16-bit PNG: the output is 8-bit, so nothing
visible is lost.

This module imports no Blender: sea_dive.py imports it for the curves and the placement, and the tests run it as is.
"""
import argparse
import math
import os
import sys

import numpy as np

FPS = 30
CANVAS = (1920, 1080)            # the design frames' size: every pixel position below is in this frame

# ---------------------------------------------------------------- the motion, in the film's seconds
ARRIVE_S = 1.2                   # CAMBRIAN's arrival: its size and the layer's blur settle along one smoother-step
ARRIVE_SCALE = 1.025             # CAMBRIAN starts this much larger
BLUR_PX = 5.0                    # the arrival's blur, a standard deviation in pixels at 1080 rows
NAME_RISE = (0.4, 1.4)           # s: the name rises from below the horizon between these
RISE_PX = 135.0                  # px (1080p) the name starts below its place: its ascenders (row 113) sit below the horizon (234)
LEAVE_FADE = (0.1, 0.7)          # s after the hold ends (when path D starts to tip down): the words fade as the camera carries them up

def smootherstep(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6.0 * x - 15.0) + 10.0)

def smoothstep(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)

def arrival(t):
    """0 at the start, 1 once settled: CAMBRIAN's arrival curve."""
    return smootherstep(t / ARRIVE_S)

def mark_scale(t):
    return 1.0 + (ARRIVE_SCALE - 1.0) * (1.0 - arrival(t))

def name_rise_px(t):
    """How far below its place the name stands (1080p pixels): RISE_PX until NAME_RISE[0], 0 from NAME_RISE[1]."""
    return RISE_PX * (1.0 - smootherstep((t - NAME_RISE[0]) / (NAME_RISE[1] - NAME_RISE[0])))

def word_fade(t, hold):
    """The words' opacity: 1 through the hold, 0 once the camera has carried them away."""
    return 1.0 - smoothstep((t - hold - LEAVE_FADE[0]) / (LEAVE_FADE[1] - LEAVE_FADE[0]))

def blur_sigma(t, rows):
    """The composite's blur, in pixels of a frame `rows` high."""
    return BLUR_PX * rows / 1080.0 * (1.0 - arrival(t))

# ---------------------------------------------------------------- the placement
LAMBDA = "Λ"
WORDMARK = "C" + LAMBDA + "MBRI" + LAMBDA + "N"          # CAMBRIAN, A set as Greek capital lambda
# Advance widths, font units of 1000 to the em (hmtx of IBMPlexSans-Regular.ttf); a test reads them back from the file.
PLEX_ADVANCE = {"C": 621, LAMBDA: 629, "M": 812, "B": 653, "R": 640, "I": 400, "N": 707}
MARK = {"size": 60.0, "top": 36.0, "track": 0.38, "ascent": 1025, "descent": 275}              # title.html #mark (px of 1080p)
NAME = {"size": 104.0, "top": 104.0, "ascent": 990, "descent": 310, "max_width": 1400.0}       # title.html #name
INK_MARK, INK_NAME, RIM = "#1b3a4f", "#2e5068", "#fff6e8"

def css_baseline(top, size, ascent, descent, upm=1000):
    """Where a browser puts the baseline of a one-line box with line-height 1: the half-leading may be negative."""
    return top + (size - (ascent + descent) / upm * size) / 2.0 + ascent / upm * size

def wordmark_pens(canvas_w=CANVAS[0], size=MARK["size"], track=MARK["track"], text=WORDMARK):
    """Each letter's pen position (px, the left of its advance) in the design frame. The letters are tracked by `track`
    em after each one, trailing one included, and the box has `track` em of padding on the left, as title.html sets it:
    the line is centred with the trailing space in it, so the ink stands half a tracking right of centre."""
    adv = [PLEX_ADVANCE[c] / 1000.0 * size for c in text]
    tr = track * size
    total = sum(adv) + tr * len(text)
    left = tr + (canvas_w - total) / 2.0
    pens, x = [], left
    for a in adv:
        pens.append(x)
        x += a + tr
    return pens

def _ttf_read(path, text):
    """The advance widths (font units) of `text`'s characters and the font's units per em, read from the TrueType file
    itself (cmap format 4 or 12, hmtx), with no library: Blender's Python has no fontTools. Kerning is not applied,
    which is also Blender's text. Also returns the first glyph's left bearing and the last glyph's right bearing is
    not needed: the caller wants the advance run."""
    import struct
    d = open(path, "rb").read()
    n = struct.unpack(">H", d[4:6])[0]
    tabs = {d[12 + 16 * i:16 + 16 * i].decode("latin1"): struct.unpack(">II", d[20 + 16 * i:28 + 16 * i]) for i in range(n)}
    def tab(name):
        o, l = tabs[name]
        return d[o:o + l]
    upm = struct.unpack(">H", tab("head")[18:20])[0]
    hhea = tab("hhea")
    nhm = struct.unpack(">H", hhea[34:36])[0]
    hmtx = tab("hmtx")
    cm = tab("cmap")
    best = None
    for i in range(struct.unpack(">H", cm[2:4])[0]):
        pid, eid, off = struct.unpack(">HHI", cm[4 + 8 * i:12 + 8 * i])
        fmt = struct.unpack(">H", cm[off:off + 2])[0]
        if fmt in (4, 12) and (best is None or fmt == 12 or best[2] != 12):
            best = (pid, off, fmt)
    off, fmt = best[1], best[2]
    def glyph(cp):
        if fmt == 12:
            ng = struct.unpack(">I", cm[off + 12:off + 16])[0]
            for g in range(ng):
                a, b, gid = struct.unpack(">III", cm[off + 16 + 12 * g:off + 28 + 12 * g])
                if a <= cp <= b:
                    return gid + cp - a
            return 0
        segx2 = struct.unpack(">H", cm[off + 6:off + 8])[0]
        seg = segx2 // 2
        ends = struct.unpack(">%dH" % seg, cm[off + 14:off + 14 + segx2])
        starts = struct.unpack(">%dH" % seg, cm[off + 16 + segx2:off + 16 + 2 * segx2])
        deltas = struct.unpack(">%dh" % seg, cm[off + 16 + 2 * segx2:off + 16 + 3 * segx2])
        ro_at = off + 16 + 3 * segx2
        ranges = struct.unpack(">%dH" % seg, cm[ro_at:ro_at + segx2])
        for k in range(seg):
            if ends[k] >= cp >= starts[k]:
                if ranges[k] == 0:
                    return (cp + deltas[k]) & 0xFFFF
                a = ro_at + 2 * k + ranges[k] + 2 * (cp - starts[k])
                g = struct.unpack(">H", cm[a:a + 2])[0]
                return (g + deltas[k]) & 0xFFFF if g else 0
        return 0
    gids = [glyph(ord(ch)) for ch in text]
    out = [struct.unpack(">H", hmtx[4 * min(g, nhm - 1):4 * min(g, nhm - 1) + 2])[0] for g in gids]
    return gids, out, upm, tab

def ttf_advances(path, text):
    """(advance widths in font units, units per em) of `text`, from the font file."""
    gids, adv, upm, _ = _ttf_read(path, text)
    return adv, upm

def ttf_ink_width(path, text, size_px, track=0.0):
    """The width of the ink (design pixels) of `text` set unkerned: from the first glyph's left edge to the last glyph's
    right edge, by the glyphs' own bounding boxes (glyf), to compare with what Blender draws."""
    import struct
    gids, adv, upm, tab = _ttf_read(path, text)
    loca, glyf = tab("loca"), tab("glyf")
    long_loca = struct.unpack(">h", tab("head")[50:52])[0]
    def span(g):
        a, b = ((struct.unpack(">%dI" % 2, loca[4 * g:4 * g + 8])) if long_loca else
                tuple(2 * v for v in struct.unpack(">2H", loca[2 * g:2 * g + 4])))
        if a == b:
            return None
        return struct.unpack(">h", glyf[a + 2:a + 4])[0], struct.unpack(">h", glyf[a + 6:a + 8])[0]
    pen, lo, hi = 0.0, None, None
    for g, a in zip(gids, adv):
        sp = span(g)
        if sp:
            lo = pen + sp[0] if lo is None else lo
            hi = pen + sp[1]
        pen += a + track * upm
    return (hi - lo) / upm * size_px

def run_width_px(path, text, size_px, track=0.0):
    """The advance run of `text` at `size_px` (design pixels), `track` em added after each character but the last."""
    adv, upm = ttf_advances(path, text)
    return sum(adv) / upm * size_px + track * size_px * (len(text) - 1)

def srgb_to_linear(hexcode):
    h = hexcode.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

def lens_tan_half(lens, sensor_h=24.0):
    return 0.5 * sensor_h / lens

def min_distance(height, lens, rows=2160, sensor_h=24.0):
    """The nearest distance at which a base standing on z = 0 falls within one pixel of the horizon at `rows` rows."""
    vfov = 2.0 * math.atan(lens_tan_half(lens, sensor_h))
    return height / math.tan(vfov / rows)

def plane_point(px, py, dist, height, pitch_deg, lens, size=CANVAS, sensor_h=24.0):
    """The point of the vertical plane `dist` metres ahead of the camera (horizontally, along its heading) that frame
    pixel (px, py) shows: returns (u, z), u metres to the right of the heading, z the height above the mean sea. The
    camera is `height` up, `pitch_deg` above level (negative: down), the lens vertical-fit on a `sensor_h` sensor."""
    w, h = size
    t = lens_tan_half(lens, sensor_h)
    tx = (px - w / 2.0) / (h / 2.0) * t
    ty = (h / 2.0 - py) / (h / 2.0) * t
    p = math.radians(pitch_deg)
    s = dist / (math.cos(p) - ty * math.sin(p))
    return s * tx, height + s * (math.sin(p) + ty * math.cos(p))

def metres_per_px(px, py, dist, height, pitch_deg, lens, size=CANVAS, sensor_h=24.0):
    """(horizontal, vertical) metres of the plane one pixel covers at (px, py)."""
    a = plane_point(px - 0.5, py, dist, height, pitch_deg, lens, size, sensor_h)
    b = plane_point(px + 0.5, py, dist, height, pitch_deg, lens, size, sensor_h)
    c = plane_point(px, py - 0.5, dist, height, pitch_deg, lens, size, sensor_h)
    d = plane_point(px, py + 0.5, dist, height, pitch_deg, lens, size, sensor_h)
    return b[0] - a[0], c[1] - d[1]

def horizon_row(height, pitch_deg, lens, size=CANVAS, sensor_h=24.0):
    """The row of the horizon's line (eye level) in the design frame."""
    w, h = size
    t = lens_tan_half(lens, sensor_h)
    ty = math.tan(-math.radians(pitch_deg))
    return h / 2.0 - ty / t * (h / 2.0)

# ---------------------------------------------------------------- the composite
def parse_frames(spec):
    out = []
    for part in spec.split(","):
        if "-" in part:
            a, b = part.split("-")
            out += list(range(int(a), int(b) + 1))
        else:
            out.append(int(part))
    return out

def gaussian_blur(img, sigma):
    """Separable Gaussian over the first two axes, zero outside the frame, the kernel cut at 3 sigma (so the blur
    reaches exactly ceil(3 sigma) pixels and no further)."""
    if sigma <= 0.0:
        return img
    r = int(math.ceil(3.0 * sigma))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    out = img.astype(np.float64)
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (r, r)
        padded = np.pad(out, pad)
        n = out.shape[axis]
        acc = np.zeros_like(out)
        for i, w in enumerate(k):
            sl = [slice(None)] * out.ndim
            sl[axis] = slice(i, i + n)
            acc += w * padded[tuple(sl)]
        out = acc
    return out

def composite(sea, words, matte, t):
    """sea: (H, W, 3) uint8; words: (H, W, 4) uint8, straight alpha; matte: (H, W) float 0..1; t: film seconds.
    Returns (out uint8, A float): A is the words' opacity after the blur and the matte."""
    h = sea.shape[0]
    a = words[..., 3].astype(np.float64) / 255.0
    p = words[..., :3].astype(np.float64) / 255.0 * a[..., None]
    sigma = blur_sigma(t, h)
    if sigma > 0.0:
        p = gaussian_blur(p, sigma)
        a = gaussian_blur(a, sigma)
    A = a * matte
    P = p * matte[..., None]
    out = P + sea.astype(np.float64) / 255.0 * (1.0 - A)[..., None]
    return np.clip(np.rint(out * 255.0), 0, 255).astype(np.uint8), A

def dilate(mask, r):
    """Boolean `mask` grown by r pixels in every direction (a square)."""
    out = mask.copy()
    for axis in (0, 1):
        src = out
        acc = src.copy()
        n = src.shape[axis]
        for s in range(1, min(r, n - 1) + 1):
            hi = [slice(None)] * 2
            lo = [slice(None)] * 2
            hi[axis], lo[axis] = slice(s, n), slice(0, n - s)
            acc[tuple(hi)] |= src[tuple(lo)]
            acc[tuple(lo)] |= src[tuple(hi)]
        out = acc
    return out

def untouched_outside(sea, out, words_alpha8, t):
    """Check (c): every pixel farther than the blur's reach from any pixel of the words' alpha equals the sea frame.
    Returns (pixels checked, pixels that differ, largest difference)."""
    reach = int(math.ceil(3.0 * blur_sigma(t, sea.shape[0]))) + 1
    outside = ~dilate(words_alpha8 > 0, reach)
    diff = np.abs(out.astype(int) - sea.astype(int)).max(axis=2)
    return int(outside.sum()), int((diff[outside] != 0).sum()), int(diff[outside].max()) if outside.any() else 0

def read_rgba(path):
    from PIL import Image
    return np.array(Image.open(path).convert("RGBA"))

def read_rgb(path):
    from PIL import Image
    return np.array(Image.open(path).convert("RGB"))

def write_rgb(path, arr):
    from PIL import Image
    Image.fromarray(arr, "RGB").save(path)

def run(args):
    os.makedirs(args.out, exist_ok=True)
    frames = parse_frames(args.frames)
    bad_total = 0
    for f in frames:
        sea_p = os.path.join(args.sea, "%s%04d.png" % (args.sea_prefix, f))
        wd_p = os.path.join(args.words, "%s%04d.png" % (args.words_prefix, f))
        sea = read_rgb(sea_p)
        words = read_rgba(wd_p)
        if args.cmd == "comp":
            sky_p = os.path.join(args.sky, "%s%04d.png" % (args.sky_prefix, f))
            matte = read_rgba(sky_p)[..., 3].astype(np.float64) / 255.0
        else:
            matte = None
        if words.shape[:2] != sea.shape[:2] or (matte is not None and matte.shape != sea.shape[:2]):
            raise SystemExit("frame %d: the sea %s, the words %s and the matte are not one size" % (f, sea.shape[:2], words.shape[:2]))
        dst = os.path.join(args.out, "%s%04d.png" % (args.out_prefix, f))
        if args.cmd == "comp":
            out, A = composite(sea, words, matte, f / args.fps)
            write_rgb(dst, out)
        else:
            out = read_rgb(dst)
        if args.check or args.cmd == "check":
            n, nbad, mx = untouched_outside(sea, out, words[..., 3], f / args.fps)
            bad_total += nbad
            print("CHECK frame %4d: %d pixels beyond the words' reach, %d differ from the sea (largest %d)" % (f, n, nbad, mx), flush=True)
    if args.check or args.cmd == "check":
        print("CHECK %s: %d pixels differ in all" % ("PASS" if bad_total == 0 else "FAIL", bad_total), flush=True)
        if bad_total:
            raise SystemExit(1)

def main(argv=None):
    ap = argparse.ArgumentParser(prog="title_comp.py")
    ap.add_argument("cmd", choices=("comp", "check"))
    ap.add_argument("--sea", required=True)
    ap.add_argument("--sea-prefix", default="L-")
    ap.add_argument("--words", required=True)
    ap.add_argument("--words-prefix", default="T-")
    ap.add_argument("--sky")
    ap.add_argument("--sky-prefix", default="S-")
    ap.add_argument("--out", required=True)
    ap.add_argument("--out-prefix", default="C-")
    ap.add_argument("--frames", required=True)
    ap.add_argument("--fps", type=float, default=FPS)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    if args.cmd == "comp" and not args.sky:
        ap.error("comp needs --sky")
    run(args)

if __name__ == "__main__":
    main()
