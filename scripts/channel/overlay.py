"""Draws the intro's words over its rendered plate: the title, the tagline and the corner readout.

    python overlay.py <plate dir> <out dir> [frames]

The plate is intro.py's frame_NNNN.png and track.json (where the hero's joints are on screen).
Every figure in the readout is read from hud_data.json, which is round 50 seed 2's stats.jsonl
at 14,900 s; nothing on screen is invented. Sizes are fractions of the frame height, so the same
script draws the 1080 preview and the 4K final.
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "Theatre", "UI", "Fonts"))
SANS = os.path.join(FONTS, "IBMPlexSans-Regular.ttf")
MONO = os.path.join(FONTS, "IBMPlexMono-Regular.ttf")
MONO_SB = os.path.join(FONTS, "IBMPlexMono-SemiBold.ttf")
N = 240
DATA = json.load(open(os.path.join(HERE, "hud_data.json")))

LAMBDA, DOT, THIN = chr(0x39B), chr(0xB7), chr(0x2009)
TITLE = "C" + LAMBDA + "MBRI" + LAMBDA + "N"
TAGLINE = "An evolving artificial ecosystem"

def clamp01(x):
    return min(1.0, max(0.0, x))

def smoother(x):
    x = clamp01(x)
    return x * x * x * (x * (6 * x - 15) + 10)

def grouped(n):
    return "{:,}".format(n).replace(",", THIN)

# ------------------------------------------------------------------ timing (frames at 30 fps)
T_TITLE = (14, 42)        # the word comes into focus over this start and length
TITLE_TRACK = 0.38        # letter spacing, in ems, fixed
TITLE_SETTLE = 0.035      # how much larger it starts
TITLE_BLUR = 0.02         # how soft it starts, as a fraction of the frame height
T_TAG = (40, 26)
T_RULES = (46, 32)
T_SPEC = 58               # the specimen block types in from here
T_STATS = 66
T_NOTE = 80
T_LEADER = (74, 18)
T_SPARK = (80, 30)

INK = (0.70, 0.87, 0.85)
TITLE_TOP, TITLE_BOTTOM = (0.95, 0.99, 0.98), (0.66, 0.87, 0.83)
GLOW = (0.35, 0.95, 0.85)

class Layer:
    """A float RGBA layer the size of the frame; masks are drawn in white and tinted as added."""
    def __init__(self, W, H):
        self.W, self.H = W, H
        self.rgb = np.zeros((H, W, 3), np.float32)
        self.a = np.zeros((H, W), np.float32)

    def add(self, mask, colour, alpha=1.0):
        m = np.asarray(mask, np.float32) / 255.0 * alpha
        c = np.asarray(colour, np.float32)
        if c.ndim == 1:
            c = c[None, None, :]
        self.rgb = self.rgb * (1 - m[..., None]) + c * m[..., None]
        self.a = self.a + m * (1 - self.a)

def typed(text, f, start, rate=2.2):
    n = int(max(0.0, (f - start) * rate))
    return text[:n]

_TITLE = {}

def title_mask(W, H):
    """The title at rest, drawn once per frame size: fixed spacing, the colour ramp beside it."""
    if (W, H) in _TITLE:
        return _TITLE[(W, H)]
    em = int(round(0.088 * H))
    font = ImageFont.truetype(SANS, em)
    cap_h = -font.getbbox("H", anchor="ls")[1]
    track = TITLE_TRACK * em
    adv = [font.getlength(c) for c in TITLE]
    total = sum(adv) + track * (len(TITLE) - 1)
    x = W / 2 - total / 2
    yc = 0.748 * H
    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)
    for i, c in enumerate(TITLE):
        d.text((x, yc + cap_h / 2), c, font=font, fill=255, anchor="ls")
        x += adv[i] + track
    ys = np.clip((np.arange(H, dtype=np.float32) - (yc - cap_h / 2)) / cap_h, 0, 1)[:, None, None]
    colour = np.asarray(TITLE_TOP, np.float32) * (1 - ys) + np.asarray(TITLE_BOTTOM, np.float32) * ys
    _TITLE[(W, H)] = (mask, colour, W / 2, yc, cap_h)
    return _TITLE[(W, H)]

def draw_title(f, W, H, layer, glow_acc):
    """The word comes into focus as a whole: out of a soft blur, settling from a touch larger,
    with sub-pixel resampling so a slow move does not step a pixel at a time."""
    mask, colour, cx, cy, cap_h = title_mask(W, H)
    k = smoother((f - T_TITLE[0]) / T_TITLE[1])
    if k <= 0:
        return cy + cap_h / 2
    sc = 1.0 + TITLE_SETTLE * (1 - k)
    m = mask.transform((W, H), Image.AFFINE, (1 / sc, 0, cx - cx / sc, 0, 1 / sc, cy - cy / sc),
                       resample=Image.BICUBIC)
    blur = TITLE_BLUR * H * (1 - k) ** 1.5
    if blur > 0.25:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    a = smoother((f - T_TITLE[0]) / (0.7 * T_TITLE[1]))
    layer.add(m, colour, a)
    glow_acc.append((m.filter(ImageFilter.GaussianBlur(0.016 * H)), GLOW, 0.9 * a))
    glow_acc.append((m.filter(ImageFilter.GaussianBlur(0.05 * H)), GLOW, 0.35 * a))
    return cy + cap_h / 2

def draw_tagline(f, W, H, layer, title_bottom):
    em = int(round(0.029 * H))
    font = ImageFont.truetype(MONO, em)
    track = 0.24 * em
    adv = [font.getlength(c) for c in TAGLINE]
    total = sum(adv) + track * (len(TAGLINE) - 1)
    a = smoother((f - T_TAG[0]) / T_TAG[1])
    rise = (1 - a) * 0.008 * H
    yc = title_bottom + 0.062 * H
    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)
    x0 = W / 2 - total / 2
    x = x0
    if a > 0:
        for i, c in enumerate(TAGLINE):
            d.text((x, yc), c, font=font, fill=int(round(255 * a)), anchor="lm")
            x += adv[i] + track
    if rise > 0.01:
        mask = mask.transform((W, H), Image.AFFINE, (1, 0, 0, 0, 1, -rise), resample=Image.BICUBIC)
    layer.add(mask, INK, 0.92)
    # the rules either side, growing outward
    r = smoother((f - T_RULES[0]) / T_RULES[1])
    if r > 0:
        gap, L = 0.02 * W, 0.055 * W * r
        th = max(1, int(round(H / 1080)))
        rule_mask = Image.new("L", (W, H), 0)
        rule_draw = ImageDraw.Draw(rule_mask)
        y = int(round(yc))
        rule_draw.rectangle([x0 - gap - L, y, x0 - gap, y + th - 1], fill=255)
        rule_draw.rectangle([x0 + total + gap, y, x0 + total + gap + L, y + th - 1], fill=255)
        layer.add(rule_mask, INK, 0.55 * r)

def hud_font(H, bold=False):
    return ImageFont.truetype(MONO_SB if bold else MONO, int(round(0.0165 * H)))

def draw_block(d, x, y, lines, f, start, H, stagger=6):
    """Lines of (text, bold); each types in after the one before. Returns the line boxes."""
    lh = 0.0165 * H * 1.55
    boxes = []
    for k, (text, bold) in enumerate(lines):
        font = hud_font(H, bold)
        shown = typed(text, f, start + stagger * k)
        if shown:
            d.text((x, y + k * lh), shown, font=font, fill=255, anchor="lm")
        boxes.append((x, y + k * lh, x + font.getlength(text), lh, len(shown) == len(text)))
    return boxes

def draw_hud(f, W, H, layer, track):
    s = H / 1080.0
    th = max(1, int(round(s)))
    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)
    sp = DATA["specimen"]
    fig = DATA["figures"]
    # the specimen, top left, with a leader to its body
    x0, y0 = 0.058 * W, 0.12 * H
    spec = [("SPECIMEN  " + DATA["run"] + " " + DOT + " #" + str(sp["id"]), True),
            ("%d parts %s %d joints" % (sp["parts"], DOT, sp["joints"]), False),
            ("t = %s s" % grouped(sp["snap"]), False)]
    boxes = draw_block(d, x0, y0, spec, f, T_SPEC, H)
    if f >= T_SPEC + 4:
        lab = hud_font(H, True).getlength("SPECIMEN")
        u = smoother((f - T_SPEC - 4) / 10)
        yy = int(round(y0 + 0.013 * H))
        d.rectangle([x0, yy, x0 + lab * u, yy + th - 1], fill=255)
    lk = smoother((f - T_LEADER[0]) / T_LEADER[1])
    if lk > 0 and track:
        pts = track[str(f)]
        joints = [v for k, v in pts.items() if k != "root" and 0 < v[0] < 1 and 0 < v[1] < 1]
        tx, ty = min(((v[0] * W, (1 - v[1]) * H) for v in joints), key=lambda p: p[0] + p[1])
        bx, by, bend, bh, _ = boxes[0]
        sx, sy = bend + 0.008 * W, by
        kx, ky = sx + 0.02 * W, sy
        seg1 = math.hypot(kx - sx, ky - sy)
        seg2 = math.hypot(tx - kx, ty - ky)
        L = (seg1 + seg2) * lk
        if L <= seg1:
            d.line([(sx, sy), (sx + L, sy)], fill=255, width=th)
        else:
            q = (L - seg1) / seg2
            d.line([(sx, sy), (kx, ky), (kx + (tx - kx) * q, ky + (ty - ky) * q)], fill=255, width=th, joint="curve")
        if lk >= 1:
            r = 0.0055 * H
            d.rectangle([tx - r, ty - r, tx + r, ty + r], outline=255, width=th)
    # the world's census at that second, top right, with the population's line
    x1, y1 = 0.585 * W, 0.12 * H
    stats = [("ROUND 50 " + DOT + " SEED 2", True),
             ("%6s  alive" % grouped(fig["alive"]), False),
             ("%6s  jointed" % grouped(fig["jointed"]), False),
             ("%6s  photosynthetic" % grouped(fig["photosynthetic"]), False),
             ("%6s  absorptive" % grouped(fig["absorptive"]), False)]
    draw_block(d, x1, y1, stats, f, T_STATS, H, stagger=4)
    lh = 0.0165 * H * 1.55
    top, bottom = y1 - 0.5 * lh, y1 + (len(stats) + 3.2) * lh
    v = smoother((f - T_STATS) / 16)
    if v > 0:
        xr = x1 - 0.012 * W
        d.rectangle([xr, top, xr + th - 1, top + (bottom - top) * v], fill=255)
    sk = smoother((f - T_SPARK[0]) / T_SPARK[1])
    if sk > 0:
        ser = DATA["series"]
        ts, al = ser["t"], ser["alive"]
        if ts[-1] < DATA["at"]:
            ts, al = ts + [DATA["at"]], al + [fig["alive"]]
        sw, shh = 0.105 * W, 0.045 * H
        sy0 = y1 + (len(stats) + 1.9) * lh
        mx = max(al)
        pts = [(x1 + sw * t / DATA["at"], sy0 - shh * a / mx) for t, a in zip(ts, al) if t / DATA["at"] <= sk]
        if len(pts) > 1:
            d.line(pts, fill=255, width=th, joint="curve")
        if sk >= 1:
            small = ImageFont.truetype(MONO, int(round(0.0135 * H)))
            d.text((x1, sy0 + 0.022 * H), "alive, 0 to %s s" % grouped(DATA["at"]), font=small, fill=200, anchor="lm")
    # what is real and what is not, bottom left
    x2, y2 = 0.058 * W, 0.905 * H
    note = [("BODIES EVOLVED IN THE SIMULATION", False), ("MOTION CHOREOGRAPHED FOR THIS TITLE", False)]
    draw_block(d, x2, y2, note, f, T_NOTE, H, stagger=8)
    halo = mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(0.006 * H))
    layer.add(halo, (0.0, 0.02, 0.03), 0.55)
    layer.add(mask, INK, 0.9)

def frame(plate, f, track):
    im = Image.open(plate).convert("RGB")
    W, H = im.size
    base = np.asarray(im, np.float32) / 255.0
    layer = Layer(W, H)
    glows = []
    tb = draw_title(f, W, H, layer, glows)
    draw_tagline(f, W, H, layer, tb)
    draw_hud(f, W, H, layer, track)
    out = base * (1 - layer.a[..., None]) + layer.rgb * layer.a[..., None]
    for g, c, k in glows:
        out = out + (np.asarray(g, np.float32) / 255.0 * k)[..., None] * np.asarray(c, np.float32) * (1 - out)
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8))

def main():
    plates, outdir = sys.argv[1], sys.argv[2]
    which = sys.argv[3] if len(sys.argv) > 3 else "all"
    os.makedirs(outdir, exist_ok=True)
    tp = os.path.join(plates, "track.json")
    track = json.load(open(tp)) if os.path.exists(tp) else None
    frames = range(1, N + 1) if which == "all" else [int(x) for x in which.split(",")]
    for f in frames:
        p = os.path.join(plates, "frame_%04d.png" % f)
        if not os.path.exists(p):
            continue
        frame(p, f, track).save(os.path.join(outdir, "frame_%04d.png" % f))
    print("overlay done", outdir)

if __name__ == "__main__":
    main()