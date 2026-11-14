"""Draws the word "mouth", with a thin leader to the mouth disc, over an eating shot.

    python -I scripts/channel/mouth_label.py <clip.mp4> <track.json> <out.mp4> [--from S] [--side auto|left|right|up|down] [--sheet out.png]

Viewers read the creatures' round mouth discs as eyes, so each eating shot gets this label.
<track.json> is the theatre lane's per-shot file: one row per clip frame with `frame`, the disc centre's screen
fraction `x` (right) and `y` (down), `depth` in metres and `visible`. Any other top-level field is a header and is
printed. A track whose row count differs from the clip's frame count is refused.

The look is the graphics' words (r67_graphics/g_words.py): IBM Plex Mono SemiBold, 44 px at 1920 wide scaled to the
clip, #e6fbf6 with a #9ff5e6 glow at 45%, laid down at four times the size at its sub-pixel position and reduced.
The leader is 1.5 px at 1920, in the word's colour at 70%, from the word's nearest edge to 10 px short of the disc
centre. The word sits 150 px (at 1920) from the disc on the chosen side and follows it through a 1-pole smoothing of
0.25 s; it always stays inside a 5% safe margin. The label fades in over 0.4 s from --from (default 25% into the
clip), and fades out over 0.2 s where `visible` is false and back in over 0.2 s when it turns true again.
Sizes below are at 1920 wide and scale by W/1920.
"""
import argparse
import json
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "r67_graphics"))    # -I leaves the script's folder off the path
import g_words as GW                                       # noqa: E402

TEXT = "mouth"
FONT_FILE = GW.FACES["mono-semibold"]
SIZE_PX = 44.0
INK, GLOW = "#e6fbf6", "#9ff5e6"
GLOW_ALPHA, GLOW_SIGMA = 0.45, 8.0
LEADER_W, LEADER_ALPHA, LEADER_GAP = 2.5, 0.70, 10.0
OFFSET = 230.0      # disc centre to the word box's nearest edge
HALO, HALO_ALPHA, HALO_SIGMA, HALO_GAIN = "#06141a", 0.55, 3.0, 2.5
SAFE = 0.05
TAU = 0.25
FADE_IN, FADE_VIS = 0.4, 0.2
DEFAULT_FROM_FRAC = 0.25
SIDES = ("left", "right", "up", "down")
DIRS = {"left": (-1.0, 0.0), "right": (1.0, 0.0), "up": (0.0, -1.0), "down": (0.0, 1.0)}
LSS = 8         # supersampling of the leader


# ---------------------------------------------------------------- the track
def load_track(path):
    """Returns (header dict, rows list of dicts with frame, x, y, depth, visible)."""
    doc = json.load(open(path, encoding="utf-8-sig"))
    header, rows = {}, None
    if isinstance(doc, list):
        rows = doc
    elif isinstance(doc, dict):
        for k, v in doc.items():
            if rows is None and isinstance(v, list) and v and isinstance(v[0], dict) and ("x" in v[0] or "screen_frac" in v[0]):
                rows = v
            elif k == "header" and isinstance(v, dict):
                header.update(v)
            else:
                header[k] = v
    if rows is None:
        raise ValueError("track %s has no list of rows (each with frame, x, y, depth, visible)" % path)
    out = []
    for i, r in enumerate(rows):
        if "screen_frac" in r:
            x, y = r["screen_frac"]
        elif "x" in r and "y" in r:
            x, y = r["x"], r["y"]
        else:
            raise ValueError("track row %d has no x/y or screen_frac (has %s)" % (i, sorted(r)))
        out.append({"frame": r.get("frame", i), "x": float(x), "y": float(y),
                    "depth": r.get("depth", r.get("depth_m")), "visible": bool(r.get("visible", True))})
    return header, out


def check_counts(rows, n_frames):
    if len(rows) != n_frames:
        raise ValueError("track has %d rows but the clip has %d frames" % (len(rows), n_frames))


# ---------------------------------------------------------------- geometry
class Look:
    """The word's font and its ink box at a clip size."""

    def __init__(self, W, H, fonts=GW.FONTS):
        self.W, self.H = W, H
        self.s = W / 1920.0
        self.font = ImageFont.truetype(os.path.join(fonts, FONT_FILE), int(round(SIZE_PX * self.s * GW.SS)))
        l, t, r, b = self.font.getbbox(TEXT, anchor="ls")
        self.hw = (r - l) / GW.SS / 2.0                      # half width of the ink
        self.top, self.bot = t / GW.SS, b / GW.SS            # relative to the baseline (top negative)
        self.hh = (self.bot - self.top) / 2.0
        self.mid = (self.top + self.bot) / 2.0               # box centre relative to the baseline
        self.mx, self.my = SAFE * W, SAFE * H
        self.edge = OFFSET * self.s
        self.gap = LEADER_GAP * self.s

    def clamp_center(self, cx, cy):
        return (min(max(cx, self.mx + self.hw), self.W - self.mx - self.hw),
                min(max(cy, self.my + self.hh), self.H - self.my - self.hh))

    def target(self, side, dx, dy):
        cx, cy = self.raw_center(side, dx, dy)
        return self.clamp_center(cx, cy)

    def raw_center(self, side, dx, dy):
        """The word's box centre with its nearest edge `edge` px from the disc centre, before any clamping."""
        ux, uy = DIRS[side]
        return dx + ux * (self.edge + self.hw * abs(ux)), dy + uy * (self.edge + self.hh * abs(uy))

    def fits(self, cx, cy):
        return (self.mx <= cx - self.hw and cx + self.hw <= self.W - self.mx and
                self.my <= cy - self.hh and cy + self.hh <= self.H - self.my)


def disc_px(rows, W, H):
    return np.array([[r["x"] * W, r["y"] * H] for r in rows], np.float64)


def choose_side(rows, from_frame, look):
    """The side with the most room, in pixels beyond what the word needs, over the label's visible frames."""
    W, H = look.W, look.H
    pts = [(r["x"] * W, r["y"] * H) for r in rows[from_frame:] if r["visible"]] or \
          [(r["x"] * W, r["y"] * H) for r in rows]
    need_h, need_v = look.edge + look.hw, look.edge + look.hh
    room = {
        "right": min(W - look.mx - x for x, y in pts) - need_h,
        "left": min(x - look.mx for x, y in pts) - need_h,
        "up": min(y - look.my for x, y in pts) - need_v,
        "down": min(H - look.my - y for x, y in pts) - need_v,
    }
    return max(("right", "left", "up", "down"), key=lambda k: room[k])     # ties go to the first listed


def side_scores(rows, from_frame, look, luma, k=1.0):
    """Mean luma (0..1) of the clip under the word box, over the label's visible frames, for each side that keeps the
    word inside the safe margin on every one of those frames. luma is (n, h, w) at 1/k of the clip's size."""
    frames = [i for i in range(from_frame, len(rows)) if rows[i]["visible"]] or list(range(len(rows)))
    scores = {}
    for side in SIDES:
        tot, ok = 0.0, True
        for i in frames:
            cx, cy = look.raw_center(side, rows[i]["x"] * look.W, rows[i]["y"] * look.H)
            if not look.fits(cx, cy):
                ok = False
                break
            x0, x1 = int(round((cx - look.hw) / k)), int(round((cx + look.hw) / k))
            y0, y1 = int(round((cy - look.hh) / k)), int(round((cy + look.hh) / k))
            tot += float(np.mean(luma[i][y0:max(y1, y0 + 1), x0:max(x1, x0 + 1)]))
        if ok:
            scores[side] = tot / len(frames)
    return scores


def pick_by_luma(scores):
    """The darkest allowed side; ties go to the order right, left, up, down."""
    return min(sorted(scores, key=lambda k: ("right", "left", "up", "down").index(k)), key=lambda k: scores[k])


def smooth(targets, visible, fps, tau=TAU):
    """1-pole smoothing of (n, 2) targets; a frame where the disc is not visible holds the last target."""
    n = len(targets)
    out = np.zeros((n, 2), np.float64)
    a = 1.0 - math.exp(-1.0 / (fps * tau))
    first = next((i for i in range(n) if visible[i]), 0)
    held = np.array(targets[first], np.float64)
    state = held.copy()
    for i in range(n):
        if visible[i]:
            held = np.array(targets[i], np.float64)
        state = state + a * (held - state)
        out[i] = state
    return out


def opacities(visible, fps, from_frame):
    """Per-frame opacity: the 0.4 s fade-in from from_frame times the visibility fade (0.2 s each way)."""
    n = len(visible)
    out = np.zeros(n)
    step = 1.0 / (fps * FADE_VIS)
    v = 1.0 if visible[0] else 0.0
    for i in range(n):
        goal = 1.0 if visible[i] else 0.0
        if goal > v:
            v = min(goal, v + step)
        elif goal < v:
            v = max(goal, v - step)
        ramp = min(max((i - from_frame) / (fps * FADE_IN), 0.0), 1.0)
        out[i] = ramp * v
    return out


def leader_points(look, cx, cy, dx, dy):
    """(start, end) of the leader, or None: the word box's point nearest the disc, to 10 px short of the disc."""
    px = min(max(dx, cx - look.hw), cx + look.hw)
    py = min(max(dy, cy - look.hh), cy + look.hh)
    d = math.hypot(px - dx, py - dy)
    if d <= look.gap + 0.5:
        return None
    ux, uy = (px - dx) / d, (py - dy) / d
    return (px, py), (dx + ux * look.gap, dy + uy * look.gap)


# ---------------------------------------------------------------- drawing
def _blend(region, color, alpha):
    return region * (1 - alpha[..., None]) + color[None, None, :] * alpha[..., None]


def draw_label(frame, look, cx, cy, dx, dy, op):
    """frame float (H, W, 3) 0..1, drawn in place; the word's box centre is (cx, cy). Under word and leader go a dark
    halo and the glow, so the label reads over a bright body and over dark water alike."""
    if op <= 0.001:
        return
    W, H, s = look.W, look.H, look.s
    ink, glow, halo = GW.hexrgb(INK), GW.hexrgb(GLOW), GW.hexrgb(HALO)
    pad = int(math.ceil(3 * GLOW_SIGMA * s + 6))
    pts = leader_points(look, cx, cy, dx, dy)
    # one tile holds the word and the leader
    bx0, by0, bx1, by1 = cx - look.hw, cy - look.hh, cx + look.hw, cy + look.hh
    if pts:
        for px, py in pts:
            bx0, by0, bx1, by1 = min(bx0, px), min(by0, py), max(bx1, px), max(by1, py)
    ox, oy = int(math.floor(bx0)) - pad, int(math.floor(by0)) - pad
    tw, th = int(math.ceil(bx1)) + pad - ox, int(math.ceil(by1)) + pad - oy
    word = GW.word_mask(TEXT, look.font, 0.0, cx - ox, cy - look.mid - oy, tw, th)
    lead = np.zeros((th, tw), np.float32)
    if pts:
        (ax, ay), (ex, ey) = pts
        big = Image.new("L", (tw * LSS, th * LSS), 0)
        wpx = max(1, int(round(LEADER_W * s * LSS)))
        ImageDraw.Draw(big).line([((ax - ox) * LSS, (ay - oy) * LSS), ((ex - ox) * LSS, (ey - oy) * LSS)], fill=255, width=wpx)
        lead = np.asarray(big.resize((tw, th), Image.BOX), np.float32) / 255.0
    both = np.maximum(word, lead * LEADER_ALPHA)
    h_a = np.clip(GW.blur(both, HALO_SIGMA * s * 1.0) * HALO_GAIN, 0, 1) * HALO_ALPHA * op
    g_a = np.clip(GW.blur(both, GLOW_SIGMA * s) * GLOW_ALPHA, 0, 1) * op
    t_a = np.clip(word, 0, 1) * op
    l_a = lead * (LEADER_ALPHA * op)
    x0, y0, x1, y1 = max(ox, 0), max(oy, 0), min(ox + tw, W), min(oy + th, H)
    if x1 <= x0 or y1 <= y0:
        return
    sl = (slice(y0 - oy, y1 - oy), slice(x0 - ox, x1 - ox))
    reg = frame[y0:y1, x0:x1]
    reg = _blend(reg, halo, h_a[sl])
    reg = _blend(reg, glow, g_a[sl])
    reg = _blend(reg, ink, l_a[sl])
    frame[y0:y1, x0:x1] = _blend(reg, ink, t_a[sl])


def plan(rows, W, H, fps, side, from_frame, fonts=GW.FONTS, luma=None, k=1.0):
    """Everything per frame: the side used, the word centres, the disc px and the opacities."""
    look = Look(W, H, fonts)
    if side == "auto":
        scores = side_scores(rows, from_frame, look, luma, k) if luma is not None else {}
        if scores:
            side = pick_by_luma(scores)
            print("side scores (mean luma under the word, darker is better): " +
                  ", ".join("%s %.3f" % (s_, v) for s_, v in scores.items()) + "; chose %s" % side)
        else:
            side = choose_side(rows, from_frame, look)
            print("no side keeps the word inside the safe margin on every frame, or no luma given; chose %s by room" % side)
    disc = disc_px(rows, W, H)
    vis = [r["visible"] for r in rows]
    targets = np.array([look.target(side, x, y) for x, y in disc])
    cen = smooth(targets, vis, fps)
    # smoothing a convex combination of in-bounds targets stays in bounds; clamp only against rounding
    cen = np.array([look.clamp_center(x, y) for x, y in cen])
    return look, side, disc, cen, opacities(vis, fps, from_frame)


# ---------------------------------------------------------------- video
def probe(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                          "stream=width,height,r_frame_rate,nb_frames,duration", "-of", "json", path],
                         capture_output=True, text=True, check=True).stdout
    st = json.loads(out)["streams"][0]
    num, den = st["r_frame_rate"].split("/")
    fps = float(num) / float(den)
    n = st.get("nb_frames")
    if not n or n == "N/A":
        cnt = subprocess.run(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries",
                              "stream=nb_read_frames", "-of", "json", path], capture_output=True, text=True,
                             check=True).stdout
        n = json.loads(cnt)["streams"][0]["nb_read_frames"]
    return int(st["width"]), int(st["height"]), st["r_frame_rate"], fps, int(n)


def sheet_frames(n, k=6):
    return sorted({int(round(i * (n - 1) / (k - 1))) for i in range(k)}) if n > 1 else [0]


def make_sheet(frames, path):
    """frames: list of (index, uint8 RGB array). A 3-wide grid, each tile numbered."""
    tw = 640
    tiles = []
    for idx, arr in frames:
        im = Image.fromarray(arr)
        im = im.resize((tw, int(round(im.height * tw / im.width))), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        f = ImageFont.truetype(os.path.join(GW.FONTS, GW.FACES["mono"]), 22)
        d.rectangle([0, 0, 120, 30], fill=(0, 0, 0))
        d.text((6, 4), "f %d" % idx, font=f, fill=(255, 255, 255))
        tiles.append(im)
    cols = 3
    rws = (len(tiles) + cols - 1) // cols
    th = tiles[0].height
    sheet = Image.new("RGB", (cols * tw, rws * th), (0, 0, 0))
    for i, im in enumerate(tiles):
        sheet.paste(im, ((i % cols) * tw, (i // cols) * th))
    sheet.save(path)


def render(clip, track, out, from_s=None, side="auto", sheet=None):
    W, H, rate, fps, n = probe(clip)
    header, rows = load_track(track)
    for k, v in header.items():
        print("track %s: %s" % (k, json.dumps(v)))
    check_counts(rows, n)
    from_frame = int(round((DEFAULT_FROM_FRAC * n / fps if from_s is None else from_s) * fps))
    luma, k = None, 1.0
    if side == "auto":
        k = float(max(1, W // 480))
        sw, sh = int(W // k), int(H // k)
        raw = subprocess.run(["ffmpeg", "-v", "error", "-i", clip, "-vf", "scale=%d:%d:flags=area" % (sw, sh), "-f",
                              "rawvideo", "-pix_fmt", "gray", "-"], capture_output=True, check=True).stdout
        luma = np.frombuffer(raw, np.uint8).reshape(-1, sh, sw).astype(np.float32) / 255.0
        if len(luma) != n:
            raise ValueError("luma pass read %d frames, clip has %d" % (len(luma), n))
        k = W / float(sw)
    look, side, disc, cen, ops = plan(rows, W, H, fps, side, from_frame, luma=luma, k=k)
    print("clip %dx%d, %d frames at %s; side %s; label from frame %d" % (W, H, n, rate, side, from_frame))
    dec = subprocess.Popen(["ffmpeg", "-v", "error", "-i", clip, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                           stdout=subprocess.PIPE)
    enc = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "%dx%d" % (W, H),
                            "-r", rate, "-i", "-", "-i", clip, "-map", "0:v", "-map", "1:a?", "-c:v", "libx264",
                            "-crf", "16", "-pix_fmt", "yuv420p", "-c:a", "copy", out],
                           stdin=subprocess.PIPE)
    keep = set(sheet_frames(n)) if sheet else set()
    kept = []
    size = W * H * 3
    try:
        for i in range(n):
            buf = dec.stdout.read(size)
            if len(buf) < size:
                raise RuntimeError("clip decoded only %d of %d frames" % (i, n))
            arr = np.frombuffer(buf, np.uint8).reshape(H, W, 3)
            if ops[i] > 0.001:
                f = arr.astype(np.float32) / 255.0
                draw_label(f, look, cen[i][0], cen[i][1], disc[i][0], disc[i][1], float(ops[i]))
                arr = (np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8)
            enc.stdin.write(arr.tobytes())
            if i in keep:
                kept.append((i, arr.copy()))
    finally:
        enc.stdin.close()
        dec.stdout.close()
        enc.wait()
        dec.wait()
    if enc.returncode:
        raise RuntimeError("ffmpeg encoder exited with %d" % enc.returncode)
    if sheet:
        make_sheet(kept, sheet)
    print("wrote %s" % out)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("clip")
    ap.add_argument("track")
    ap.add_argument("out")
    ap.add_argument("--from", dest="from_s", type=float, default=None)
    ap.add_argument("--side", default="auto", choices=("auto",) + SIDES)
    ap.add_argument("--sheet", default=None)
    a = ap.parse_args(argv)
    for p in (a.clip, a.track):
        if not os.path.isfile(p):
            sys.exit("mouth_label: %s does not exist" % p)
    try:
        render(a.clip, a.track, a.out, a.from_s, a.side, a.sheet)
    except ValueError as e:
        sys.exit("mouth_label: %s" % e)
    return 0


if __name__ == "__main__":
    sys.exit(main())
