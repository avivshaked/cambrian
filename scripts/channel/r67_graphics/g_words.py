"""Draws a graphic's words over its rendered frames, placed every frame at the pixel their anchor projects to.

    python -I g_words.py <frames dir> <anchors.json> <out dir> [--fonts DIR] [--frames 1,300,615]

<frames dir> holds frame_NNNN.png (NNNN the scene frame, 1-based, 30 fps); <anchors.json> is what the scene script
wrote: per frame, each anchor's design pixel and its depth-of-field blur, and under "words" each word's text, anchor,
face, size, tracking, colours and opacity keys (scene seconds). Sizes are the design's at 1080p and scale by H/1080.

Why an overlay and not 3D text: a wording changes often, and in 3D each change re-renders hours of 4K; the depth
of field and the bloom would also soften small mono type below phone legibility. The word is laid down at four
times the frame's size at its sub-pixel position and reduced, so it moves smoothly with the camera (D364); it is
blurred by its anchor's circle of confusion, so it sits in the water at the depth it is anchored to; and a glow of
the design's colour at 45% is laid under it.

Fonts come from unity/Assets/Theatre/UI/Fonts (tracked): IBMPlexMono-SemiBold (face "mono-semibold") and
IBMPlexSans-Regular (face "sans").
"""
import argparse, json, os, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
FONTS = os.path.join(REPO, "unity", "Assets", "Theatre", "UI", "Fonts")
FACES = {"mono-semibold": "IBMPlexMono-SemiBold.ttf", "mono": "IBMPlexMono-Regular.ttf", "sans": "IBMPlexSans-Regular.ttf"}
SS = 4          # supersampling of the word's mask


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255.0


def opacity_at(keys, t):
    """Piecewise-linear opacity through [[seconds, value], ...], held at both ends."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1:
            return v0 + (v1 - v0) * (t - t0) / (t1 - t0) if t1 > t0 else v1
    return keys[-1][1]


def word_mask(text, font, tracking, cx, cy, W, H):
    """A float mask (H, W) of the word, middle-baseline at (cx, cy) frame pixels, drawn at SS times and reduced."""
    big = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(big)
    adv = [font.getlength(c) + tracking * SS for c in text]
    x = cx * SS - (sum(adv) - tracking * SS) / 2
    for c, a in zip(text, adv):
        d.text((x, cy * SS), c, font=font, fill=255, anchor="ls")
        x += a
    small = big.resize((W, H), Image.BOX)
    return np.asarray(small, np.float32) / 255.0


def blur(mask, sigma):
    if sigma <= 0.05:
        return mask
    img = Image.fromarray((np.clip(mask, 0, 1) * 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.GaussianBlur(sigma)), np.float32) / 255.0


def draw_words(frame_rgb, words, anchors, t, fonts_dir=FONTS):
    """frame_rgb: float (H, W, 3) 0..1. Returns the frame with every word over it."""
    H, W = frame_rgb.shape[:2]
    s = H / 1080.0
    out = frame_rgb.copy()
    for w in words.values():
        op = opacity_at(w["opacity"], t)
        if op <= 0.001 or w["anchor"] not in anchors:
            continue
        px, py, coc = anchors[w["anchor"]]
        font = ImageFont.truetype(os.path.join(fonts_dir, FACES[w["face"]]), int(round(w["size_px"] * s * SS)))
        m = word_mask(w["text"], font, w.get("tracking_px", 0) * s, px * s, py * s, W, H)
        sigma = max(0.0, coc * s / 2.0)          # a circle of confusion of diameter coc: a Gaussian of about half
        m = blur(m, sigma)
        glow = blur(m, 8.0 * s) * w.get("glow_alpha", 0.45)
        glow = np.clip(glow * 1.0, 0, 1)
        gcol, tcol = hexrgb(w.get("glow", "#9ff5e6")), hexrgb(w["color"])
        a_glow = glow * op
        out = out * (1 - a_glow[..., None]) + gcol[None, None, :] * a_glow[..., None]
        a_txt = np.clip(m, 0, 1) * op
        out = out * (1 - a_txt[..., None]) + tcol[None, None, :] * a_txt[..., None]
    return out


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("frames")
    ap.add_argument("anchors")
    ap.add_argument("out")
    ap.add_argument("--fonts", default=FONTS)
    ap.add_argument("--frames-only", dest="only")
    a = ap.parse_args(argv)
    for p in (a.frames, a.anchors):
        if not os.path.exists(p):
            sys.exit("g_words: %s does not exist" % p)
    doc = json.load(open(a.anchors))
    fps = doc.get("fps", 30)
    os.makedirs(a.out, exist_ok=True)
    only = {int(x) for x in a.only.split(",")} if a.only else None
    n = 0
    for name in sorted(os.listdir(a.frames)):
        m = re.match(r"frame_(\d+)\.png$", name)
        if not m:
            continue
        f = int(m.group(1))
        if only is not None and f not in only:
            continue
        if str(f) not in doc["frames"]:
            sys.exit("g_words: %s has no anchors for frame %d (the scene script writes them for the frames it renders)" % (a.anchors, f))
        img = np.asarray(Image.open(os.path.join(a.frames, name)).convert("RGB"), np.float32) / 255.0
        res = draw_words(img, doc["words"], doc["frames"][str(f)], (f - 1) / fps, a.fonts)
        Image.fromarray((np.clip(res, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(a.out, name))
        n += 1
    print("drew words on %d frames into %s" % (n, a.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
