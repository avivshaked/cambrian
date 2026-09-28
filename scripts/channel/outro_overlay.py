"""Draws the outro's words over its plate, and with --guides the end screen's zones, so the
layout can be judged before YouTube Studio places the real elements.

    python outro_overlay.py <plate dir> <out dir> [frames] [--guides] [--mock=<tile thumbnail png>]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import overlay as O

HERE = os.path.dirname(os.path.abspath(__file__))
LAYOUT = json.load(open(os.path.join(HERE, "endscreen_layout.json")))
N = 600
ICONS = os.path.normpath(os.path.join(HERE, "..", "..", "assets", "graphics", "fonts", "MaterialIcons-Regular.ttf"))
THUMB_UP, BELL = chr(0xE8DC), chr(0xE7F4)
T_CTA = (40, 26)                       # the like and subscribe chips fade in
SWEEPS = [(90, 34), (390, 34)]         # a soft light passes across them, twice
BOTTOM_LINES = []          # the channel's handles go here once the owner gives them

def draw_cta(f, W, H, layer, glows):
    """Two outlined chips under the subscribe circle, LIKE with a thumb and SUBSCRIBE with a bell.
    They are drawn into the picture: YouTube has no like element, and viewers on mobile web or
    with end screens hidden never see the real subscribe button."""
    a = O.smoother((f - T_CTA[0]) / T_CTA[1])
    if a <= 0:
        return
    font = ImageFont.truetype(O.MONO_SB, int(round(0.0195 * H)))
    icon = ImageFont.truetype(ICONS, int(round(0.028 * H)))
    track = 0.16 * font.size
    chips = [(THUMB_UP, "LIKE")]
    pad, gap_icon, gap = 0.014 * W, 0.006 * W, 0.016 * W
    h = 0.05 * H
    widths = []
    for ic, word in chips:
        tw = sum(font.getlength(c) for c in word) + track * (len(word) - 1)
        widths.append(pad + icon.getlength(ic) + gap_icon + tw + pad)
    total = sum(widths) + gap * (len(widths) - 1)
    cx, cy = W / 2, (LAYOUT["circle"][1] + RING * LAYOUT["circle"][2]) * H + 0.045 * H
    x = cx - total / 2
    th = max(2, int(round(2 * H / 1080)))
    line = Image.new("L", (W, H), 0)
    ink = ImageDraw.Draw(line)
    back = Image.new("L", (W, H), 0)
    bd = ImageDraw.Draw(back)
    for (ic, word), w in zip(chips, widths):
        box = [x, cy - h / 2, x + w, cy + h / 2]
        bd.rounded_rectangle(box, radius=h / 2, fill=255)
        ink.rounded_rectangle(box, radius=h / 2, outline=255, width=th)
        ink.text((x + pad, cy), ic, font=icon, fill=255, anchor="lm")
        tx = x + pad + icon.getlength(ic) + gap_icon
        for c in word:
            ink.text((tx, cy), c, font=font, fill=255, anchor="lm")
            tx += font.getlength(c) + track
        x += w + gap
    layer.add(back, (0.0, 0.03, 0.04), 0.45 * a)
    layer.add(line, O.INK, 0.95 * a)
    glows.append((line.filter(ImageFilter.GaussianBlur(0.006 * H)), O.GLOW, 0.35 * a))
    for start, dur in SWEEPS:
        k = (f - start) / dur
        if 0 < k < 1:
            x0 = cx - total / 2 - 0.1 * W + (total + 0.2 * W) * O.smoother(k)
            xs = np.arange(W, dtype=np.float32)
            band = np.exp(-((xs - x0) / (0.035 * W)) ** 2)[None, :]
            m = np.asarray(line, np.float32) / 255.0 * band
            glows.append((Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.004 * H)), (0.8, 1.0, 0.95), 0.9 * a))

RING = 1.08     # the baked ring's radius over the subscribe element's

def draw_ring(f, W, H, layer, glows):
    """The subscribe ring, drawn where YouTube's subscribe element goes and a little larger than it.
    Placed in Studio, the element (the channel avatar) covers its inside and the ring frames it;
    where end screens do not show, its bell and word are the prompt."""
    a = O.smoother((f - T_CTA[0]) / T_CTA[1])
    if a <= 0:
        return
    cx, cy, r = LAYOUT["circle"][0] * W, LAYOUT["circle"][1] * H, LAYOUT["circle"][2] * H
    R = RING * r
    th = max(2, int(round(2 * H / 1080)))
    back = Image.new("L", (W, H), 0)
    ImageDraw.Draw(back).ellipse([cx - R, cy - R, cx + R, cy + R], fill=255)
    line = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(line)
    d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=255, width=th)
    icon = ImageFont.truetype(ICONS, int(round(0.06 * H)))
    d.text((cx, cy - 0.028 * H), BELL, font=icon, fill=255, anchor="mm")
    font = ImageFont.truetype(O.MONO_SB, int(round(0.0195 * H)))
    word = "SUBSCRIBE"
    track = 0.16 * font.size
    tw = sum(font.getlength(c) for c in word) + track * (len(word) - 1)
    x = cx - tw / 2
    for c in word:
        d.text((x, cy + 0.045 * H), c, font=font, fill=255, anchor="lm")
        x += font.getlength(c) + track
    layer.add(back, (0.0, 0.03, 0.04), 0.45 * a)
    layer.add(line, O.INK, 0.95 * a)
    glows.append((line.filter(ImageFilter.GaussianBlur(0.006 * H)), O.GLOW, 0.35 * a))
    glows.append((line.filter(ImageFilter.GaussianBlur(0.02 * H)), O.GLOW, 0.25 * a))
    for start, dur in SWEEPS:
        k = (f - start) / dur
        if 0 < k < 1:
            x0 = cx - R - 0.1 * W + (2 * R + 0.2 * W) * O.smoother(k)
            xs = np.arange(W, dtype=np.float32)
            band = np.exp(-((xs - x0) / (0.035 * W)) ** 2)[None, :]
            m = np.asarray(line, np.float32) / 255.0 * band
            glows.append((Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.004 * H)), (0.8, 1.0, 0.95), 0.9 * a))

def draw_mock(W, H, layer, thumbnail):
    """What YouTube adds on top, for judging only and never rendered into the outro: the avatar
    in the subscribe element and one playlist tile, labelled as a mock."""
    cx, cy, r = LAYOUT["circle"][0] * W, LAYOUT["circle"][1] * H, LAYOUT["circle"][2] * H
    av = Image.open(os.path.join(HERE, "..", "..", "assets", "channel", "cambrian-creature-square.png")).convert("RGB")
    av = av.resize((int(2 * r), int(2 * r)), Image.LANCZOS)
    m = Image.new("L", av.size, 0)
    ImageDraw.Draw(m).ellipse([0, 0, av.size[0] - 1, av.size[1] - 1], fill=255)
    full = Image.new("RGB", (W, H)); fm = Image.new("L", (W, H), 0)
    full.paste(av, (int(cx - r), int(cy - r))); fm.paste(m, (int(cx - r), int(cy - r)))
    layer.add(fm, np.asarray(full, np.float32) / 255.0)
    x0, y0, w, h = LAYOUT["tiles"][0]
    thumb = Image.open(thumbnail).convert("RGB")
    thumb = thumb.resize((int(w * W), int(h * H)), Image.LANCZOS)
    tm = Image.new("L", (W, H), 0); tf = Image.new("RGB", (W, H))
    tf.paste(thumb, (int(x0 * W), int(y0 * H))); ImageDraw.Draw(tm).rectangle([x0 * W, y0 * H, (x0 + w) * W - 1, (y0 + h) * H - 1], fill=255)
    layer.add(tm, np.asarray(tf, np.float32) / 255.0)
    bar = Image.new("L", (W, H), 0)
    ImageDraw.Draw(bar).rectangle([(x0 + w * 0.62) * W, y0 * H, (x0 + w) * W, (y0 + h) * H], fill=255)
    layer.add(bar, (0.0, 0.0, 0.0), 0.7)
    t = Image.new("L", (W, H), 0)
    td = ImageDraw.Draw(t)
    lab = ImageFont.truetype(O.SANS, int(round(0.022 * H)))
    td.text(((x0 + w * 0.81) * W, (y0 + h * 0.45) * H), "PLAYLIST", font=lab, fill=255, anchor="mm")
    td.text(((x0 + w * 0.81) * W, (y0 + h * 0.56) * H), "1 video", font=lab, fill=200, anchor="mm")
    big = ImageFont.truetype(O.MONO_SB, int(round(0.03 * H)))
    td.text((0.5 * W, 0.965 * H), "MOCK-UP: the avatar and the tile are what YouTube adds; not in the video", font=lab, fill=255, anchor="mm")
    layer.add(t, (1.0, 1.0, 1.0), 0.95)

def draw(f, W, H, layer, glows, guides):
    a = O.smoother((f - 1) / 30.0)
    em = int(round(0.066 * H))
    font = ImageFont.truetype(O.SANS, em)
    cap_h = -font.getbbox("H", anchor="ls")[1]
    track = 0.40 * em
    adv = [font.getlength(c) for c in O.TITLE]
    total = sum(adv) + track * (len(O.TITLE) - 1)
    yc = LAYOUT["title_y"] * H
    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)
    x = W / 2 - total / 2
    for i, c in enumerate(O.TITLE):
        d.text((x, yc + cap_h / 2), c, font=font, fill=int(round(255 * a)), anchor="ls")
        x += adv[i] + track
    ys = np.clip((np.arange(H, dtype=np.float32) - (yc - cap_h / 2)) / cap_h, 0, 1)[:, None, None]
    colour = np.asarray(O.TITLE_TOP, np.float32) * (1 - ys) + np.asarray(O.TITLE_BOTTOM, np.float32) * ys
    layer.add(mask, colour)
    glows.append((mask.filter(ImageFilter.GaussianBlur(0.014 * H)), O.GLOW, 0.8))
    glows.append((mask.filter(ImageFilter.GaussianBlur(0.045 * H)), O.GLOW, 0.3))
    tf = ImageFont.truetype(O.MONO, int(round(0.026 * H)))
    tt = 0.24 * tf.size
    tadv = [tf.getlength(c) for c in O.TAGLINE]
    ttotal = sum(tadv) + tt * (len(O.TAGLINE) - 1)
    tm = Image.new("L", (W, H), 0)
    td = ImageDraw.Draw(tm)
    b = O.smoother((f - 12) / 30.0)
    x = W / 2 - ttotal / 2
    for i, c in enumerate(O.TAGLINE):
        td.text((x, LAYOUT["tagline_y"] * H), c, font=tf, fill=int(round(255 * b)), anchor="lm")
        x += tadv[i] + tt
    layer.add(tm, O.INK, 0.9)
    draw_ring(f, W, H, layer, glows)
    draw_cta(f, W, H, layer, glows)
    if BOTTOM_LINES:
        bm = Image.new("L", (W, H), 0)
        bd = ImageDraw.Draw(bm)
        bf = O.hud_font(H)
        for k, line in enumerate(BOTTOM_LINES):
            bd.text((W / 2, LAYOUT["bottom_text_y"] * H + k * 0.03 * H), line, font=bf, fill=255, anchor="mm")
        layer.add(bm, O.INK, 0.9 * O.smoother((f - 24) / 30.0))
    if guides:
        gm = Image.new("L", (W, H), 0)
        gd = ImageDraw.Draw(gm)
        th = max(2, int(round(2 * H / 1080)))
        lab = ImageFont.truetype(O.MONO, int(round(0.02 * H)))
        for k, (x0, y0, w, h) in enumerate(LAYOUT["tiles"]):
            gd.rectangle([x0 * W, y0 * H, (x0 + w) * W, (y0 + h) * H], outline=255, width=th)
            gd.text(((x0 + w / 2) * W, (y0 + h / 2) * H), "VIDEO %d" % (k + 1), font=lab, fill=255, anchor="mm")
        cx, cy, r = LAYOUT["circle"]
        gd.ellipse([cx * W - r * H, cy * H - r * H, cx * W + r * H, cy * H + r * H], outline=255, width=th)
        gd.text((cx * W, cy * H), "SUBSCRIBE", font=lab, fill=255, anchor="mm")
        gd.rectangle([0, 0.9 * H, W - 1, H - 1], outline=255, width=th)
        gd.text((W / 2, 0.95 * H), "player controls", font=lab, fill=255, anchor="mm")
        if not BOTTOM_LINES:
            gd.text((W / 2, LAYOUT["bottom_text_y"] * H), "[ handles / links, to come ]", font=lab, fill=255, anchor="mm")
        layer.add(gm, (1.0, 0.85, 0.2), 0.8)

def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    guides = "--guides" in sys.argv
    mock = next((a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--mock=")), None)
    plates, outdir = args[0], args[1]
    which = args[2] if len(args) > 2 else "all"
    os.makedirs(outdir, exist_ok=True)
    frames = range(1, N + 1) if which == "all" else [int(x) for x in which.split(",")]
    for f in frames:
        p = os.path.join(plates, "frame_%04d.png" % f)
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert("RGB")
        W, H = im.size
        base = np.asarray(im, np.float32) / 255.0
        layer = O.Layer(W, H)
        glows = []
        draw(f, W, H, layer, glows, guides)
        out = base * (1 - layer.a[..., None]) + layer.rgb * layer.a[..., None]
        for g, c, k in glows:
            out = out + (np.asarray(g, np.float32) / 255.0 * k)[..., None] * np.asarray(c, np.float32) * (1 - out)
        if mock:
            m = O.Layer(W, H)
            draw_mock(W, H, m, mock)
            out = out * (1 - m.a[..., None]) + m.rgb * m.a[..., None]
        Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(outdir, "frame_%04d.png" % f))
    print("outro overlay done", outdir)

if __name__ == "__main__":
    main()