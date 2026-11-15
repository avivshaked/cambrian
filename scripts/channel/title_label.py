"""Burns a creature's name beside its body, in open water, with a leader to the body's outline.

    python -I scripts/channel/title_label.py <clip.mp4> <outline.json> <out.mp4> --text WORD [--from S]
                                             [--side auto|left|right|up|down] [--sheet out.png]

mouth_label.py puts a word a fixed offset from a point. A whole creature fills much of the frame, so the same rule puts
the word on the body. This tool places the word where the body is not.

<outline.json> is the theatre lane's body-outline file: {header, rows}, one row per clip frame with `screen_frac`, `visible`,
`tight_box_frac` ({x: [l, r], y: [t, b]}, fractions of the frame, x right, y down) and `grid`, a 240 x 135 occupancy
grid of the frame: {w, h, rle}, where rle lists run lengths row by row from the top left, alternating empty, filled,
empty, ..., starting with an empty run that may be 0, and the runs sum to w * h. A row with no grid has no body drawn.

The rule. On each frame the word's ink box, grown by MARGIN (24 px at 1920 wide, scaled with the frame), must touch no
filled cell of the grid and stay inside the SAFE margin. One side is chosen for the whole shot, from the label's
visible frames: each of left, right, up, down is a region beyond the body's tight box on that side; a side is scored
by the share of those frames on which it holds a clear spot, and among the sides within CLEAR_TOL of the best share,
the darkest under the word (mean luma over the label frames, as mouth_label's side_scores; the burned path reads the
clip, so the Fusion path takes the side from that run through --side, and with no luma falls back to distance alone),
within LUMA_TOL of which the one whose clear spots lie nearest the body (mean pixel distance from the word's box to the outline, ties within
DIST_TOL going right, left, up, down) wins. On the
chosen side the word slides with the body: each frame it takes the clear spot nearest the point MARGIN beyond the
tight box's edge, level with the box's middle; a centre whose box and margin then touch the body is moved outward
along the side's normal until clear, the correction smoothed over a few frames. A frame with no clear spot keeps the last good target. Targets then go
through mouth_label's 1-pole smoothing, and its fades. The leader runs from the word's box to the outline's nearest
point on the body, stopping LEADER_GAP short of it (mouth_label's own leader, aimed at that point); on a frame where
the word is within that gap of the body there is none. The look is mouth_label's.
"""
import argparse
import json
import math
import os
import subprocess
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                                      # -I leaves the script's folder off the path
import mouth_label as ML                                      # noqa: E402

MARGIN = 24.0       # px at 1920 wide: clear space between the word's ink box and the body
STRIDE = 4.0        # px at 1920 wide between candidate centres
DIST_TOL = 4.0     # px at 1920 wide: sides this close in mean distance from the body are a tie
LUMA_TOL = 0.03     # sides within this mean luma (0..1) of the darkest are a tie, broken by distance from the body
CLEAR_TOL = 0.05    # sides within this share of the best share of clear frames compete on distance


# ---------------------------------------------------------------- the track
def decode_grid(g):
    """{w, h, rle} to a bool array (h, w), True where filled. None gives None. The runs must sum to w * h."""
    if g is None:
        return None
    w, h = int(g["w"]), int(g["h"])
    rle = g["rle"]
    runs = [int(v) for v in rle.split(",")] if isinstance(rle, str) else [int(v) for v in rle]
    if sum(runs) != w * h:
        raise ValueError("grid runs sum to %d, not %d x %d = %d" % (sum(runs), w, h, w * h))
    flat = np.zeros(w * h, bool)
    pos = 0
    for k, r in enumerate(runs):
        if k % 2 == 1:
            flat[pos:pos + r] = True
        pos += r
    return flat.reshape(h, w)


def load_outline(path):
    """Returns (header, rows as mouth_label.load_track gives them plus `box` (l, t, r, b) fractions or None,
    grids: a list of bool arrays or None per row)."""
    header, rows = ML.load_track(path)
    doc = json.load(open(path, encoding="utf-8-sig"))
    raw = doc["rows"] if isinstance(doc, dict) else doc
    if len(raw) != len(rows):
        raise ValueError("outline rows disagree with track rows")
    grids = []
    for r, src in zip(rows, raw):
        g = decode_grid(src.get("grid"))
        grids.append(g if g is not None and g.any() else None)
        tb = src.get("tight_box_frac")
        r["box"] = (tb["x"][0], tb["y"][0], tb["x"][1], tb["y"][1]) if tb else None
    return header, rows, grids


# ---------------------------------------------------------------- clear space
class Occupancy:
    """A frame's grid, with an integral image for 'does this rectangle touch a filled cell'."""

    def __init__(self, grid, W, H):
        self.grid, self.W, self.H = grid, W, H
        self.gh, self.gw = grid.shape
        self.cw, self.ch = W / float(self.gw), H / float(self.gh)
        self.sat = np.zeros((self.gh + 1, self.gw + 1), np.int64)
        self.sat[1:, 1:] = grid.astype(np.int64).cumsum(0).cumsum(1)
        ys, xs = np.nonzero(grid)
        self.fx0, self.fy0 = xs * self.cw, ys * self.ch                 # filled cells' rectangles

    def touches(self, x0, y0, x1, y1):
        """Bool array: whether the rectangle (arrays or scalars, px) overlaps any filled cell. Cell rectangles are
        counted whole, so the answer errs towards 'touches'."""
        c0 = np.clip(np.floor(np.asarray(x0) / self.cw).astype(int), 0, self.gw)
        c1 = np.clip(np.ceil(np.asarray(x1) / self.cw).astype(int), 0, self.gw)
        r0 = np.clip(np.floor(np.asarray(y0) / self.ch).astype(int), 0, self.gh)
        r1 = np.clip(np.ceil(np.asarray(y1) / self.ch).astype(int), 0, self.gh)
        s = self.sat
        return (s[r1, c1] - s[r0, c1] - s[r1, c0] + s[r0, c0]) > 0

    def nearest(self, x0, y0, x1, y1):
        """(distance, (px, py)): the filled cell nearest to the box, and the point of that cell nearest the box."""
        cx0, cy0, cx1, cy1 = self.fx0, self.fy0, self.fx0 + self.cw, self.fy0 + self.ch
        dx = np.maximum(np.maximum(cx0 - x1, x0 - cx1), 0.0)
        dy = np.maximum(np.maximum(cy0 - y1, y0 - cy1), 0.0)
        k = int(np.argmin(dx * dx + dy * dy))
        bx = min(max((cx0[k] + cx1[k]) / 2.0, x0), x1)
        by = min(max((cy0[k] + cy1[k]) / 2.0, y0), y1)
        px = min(max(bx, cx0[k]), cx1[k])
        py = min(max(by, cy0[k]), cy1[k])
        return math.hypot(dx[k], dy[k]), (float(px), float(py))


def candidate_axes(look):
    st = max(2.0, STRIDE * look.s)
    xs = np.arange(look.mx + look.hw, look.W - look.mx - look.hw + 1e-6, st)
    ys = np.arange(look.my + look.hh, look.H - look.my - look.hh + 1e-6, st)
    return xs, ys


def best_spot(occ, box, side, look, axes, margin):
    """The clear centre on `side` of the body's tight box nearest the ideal one, or None."""
    xs, ys = axes
    X, Y = np.meshgrid(xs, ys)
    clear = ~occ.touches(X - look.hw - margin, Y - look.hh - margin, X + look.hw + margin, Y + look.hh + margin)
    l, t, r, b = box[0] * look.W, box[1] * look.H, box[2] * look.W, box[3] * look.H
    if side == "right":
        clear &= X - look.hw >= r
        ix, iy = r + margin + look.hw, (t + b) / 2.0
    elif side == "left":
        clear &= X + look.hw <= l
        ix, iy = l - margin - look.hw, (t + b) / 2.0
    elif side == "up":
        clear &= Y + look.hh <= t
        ix, iy = (l + r) / 2.0, t - margin - look.hh
    else:
        clear &= Y - look.hh >= b
        ix, iy = (l + r) / 2.0, b + margin + look.hh
    if not clear.any():
        return None
    d2 = np.where(clear, (X - ix) ** 2 + (Y - iy) ** 2, np.inf)
    k = int(np.argmin(d2))
    return float(X.flat[k]), float(Y.flat[k])


def is_clear(occ, look, cx, cy, margin):
    return not bool(occ.touches(cx - look.hw - margin, cy - look.hh - margin, cx + look.hw + margin, cy + look.hh + margin))


# ---------------------------------------------------------------- the plan
def plan(rows, grids, W, H, fps, side, from_frame, fonts=ML.GW.FONTS, luma=None, k=1.0):
    """Everything per frame, as mouth_label.plan gives it, with the leader's target in place of the disc:
    (look, side, target px (n, 2), word centres (n, 2), opacities, info). info holds `scores` (side -> (clear share,
    mean distance px)), `held` (frames with a body and no clear spot, the label's visible frames only counted in
    `held_visible`), `overlap0` and `overlap` (frames whose centre, before and after the outward correction, has its box grown by
    the margin touching the body), `luma` (side -> mean luma under the word, when luma is given) and `ok`. luma is
    (n, h, w) at 1/k of the clip's size, as mouth_label.side_scores takes it."""
    look = ML.Look(W, H, fonts)
    margin = MARGIN * look.s
    n = len(rows)
    axes = candidate_axes(look)
    occs = [Occupancy(g, W, H) if g is not None else None for g in grids]
    vis = [r["visible"] for r in rows]
    shown = [i for i in range(from_frame, n) if vis[i] and occs[i] is not None and rows[i]["box"]]
    if not shown:
        raise ValueError("no frame from %d on has a drawn, visible body" % from_frame)
    shown_set = set(shown)
    spots = {}                                    # side -> per frame (cx, cy) or None
    scores, lumas = {}, {}
    for sd in ML.SIDES:
        sp = [None] * n
        dist, lum = [], []
        for i in range(n):
            if occs[i] is None or not rows[i]["box"]:
                continue
            sp[i] = best_spot(occs[i], rows[i]["box"], sd, look, axes, margin)
            if sp[i] is not None and i in shown_set:
                cx, cy = sp[i]
                dist.append(occs[i].nearest(cx - look.hw, cy - look.hh, cx + look.hw, cy + look.hh)[0])
                if luma is not None:
                    x0, x1 = int(round((cx - look.hw) / k)), int(round((cx + look.hw) / k))
                    y0, y1 = int(round((cy - look.hh) / k)), int(round((cy + look.hh) / k))
                    lum.append(float(np.mean(luma[i][y0:max(y1, y0 + 1), x0:max(x1, x0 + 1)])))
        if lum:
            lumas[sd] = float(np.mean(lum))
        spots[sd] = sp
        share = len(dist) / float(len(shown))
        scores[sd] = (share, float(np.mean(dist)) if dist else float("inf"))
    if side == "auto":
        top = max(v[0] for v in scores.values())
        pool = [sd for sd in ML.SIDES if scores[sd][0] >= top - CLEAR_TOL]
        order = ("right", "left", "up", "down")      # distances within DIST_TOL px count as equal; ties go in reading order
        if lumas and all(sd in lumas for sd in pool):
            dark = min(lumas[sd] for sd in pool)
            pool = [sd for sd in pool if lumas[sd] <= dark + LUMA_TOL]
        side = min(pool, key=lambda sd: (round(scores[sd][1] / (DIST_TOL * look.s)), order.index(sd)))
    sp = spots[side]
    ok = [sp[i] is not None for i in range(n)]
    if not any(ok):
        raise ValueError("side %s has no clear spot on any frame" % side)
    targets = np.array([sp[i] if sp[i] is not None else (0.0, 0.0) for i in range(n)])
    cen = ML.smooth(targets, [ok[i] for i in range(n)], fps)
    cen = np.array([look.clamp_center(x, y) for x, y in cen])
    cen, overlap0 = push_clear(cen, occs, look, side, margin, shown_set)
    aim = np.zeros((n, 2))
    overlap = 0
    for i in range(n):
        cx, cy = cen[i]
        if occs[i] is None:
            aim[i] = (cx, cy)
            continue
        aim[i] = occs[i].nearest(cx - look.hw, cy - look.hh, cx + look.hw, cy + look.hh)[1]
        if i in shown_set and not is_clear(occs[i], look, cx, cy, margin):
            overlap += 1
    held = [i for i in range(n) if occs[i] is not None and rows[i]["box"] and sp[i] is None]
    info = {"scores": scores, "held": len(held), "held_visible": len([i for i in held if i in shown_set]),
            "overlap0": overlap0, "overlap": overlap, "luma": lumas, "shown": len(shown), "ok": ok}
    return look, side, aim, cen, ML.opacities(vis, fps, from_frame), info


NORMAL = {"right": (1.0, 0.0), "left": (-1.0, 0.0), "up": (0.0, -1.0), "down": (0.0, 1.0)}
SMOOTH_HALF = 5     # frames either side for the correction's smoothing


def push_clear(cen, occs, look, side, margin, shown_set):
    """Moves each centre whose box grown by `margin` touches the body outward along the side's normal until it is
    clear or the safe margin stops it. The per-frame offsets are dilated by a running maximum over SMOOTH_HALF frames
    either side and then averaged over the same window, which is at least each frame's own offset and has no jumps;
    any frame still touching afterwards takes its own offset. Returns (centres, count touching before)."""
    n = len(cen)
    nx, ny = NORMAL[side]
    st = max(1.0, look.s * 2.0)

    def need(i, c):
        if occs[i] is None:
            return 0.0
        o = 0.0
        cx, cy = c
        while not is_clear(occs[i], look, cx + nx * o, cy + ny * o, margin):
            o += st
            if not look.fits(cx + nx * o, cy + ny * o):
                return None
        return o

    raw = np.zeros(n)
    before = 0
    for i in range(n):
        o = need(i, cen[i])
        if o is None or o > 0.0:
            if i in shown_set:
                before += 1
        raw[i] = o or 0.0
    w = SMOOTH_HALF
    pad = np.pad(raw, w, mode="edge")
    dil = np.array([pad[i:i + 2 * w + 1].max() for i in range(n)])
    dp = np.pad(dil, w, mode="edge")
    off = np.array([dp[i:i + 2 * w + 1].mean() for i in range(n)])
    out = np.array(cen, np.float64)
    for i in range(n):
        c = (cen[i][0] + nx * off[i], cen[i][1] + ny * off[i])
        bad = occs[i] is not None and (not look.fits(*c) or not is_clear(occs[i], look, c[0], c[1], margin))
        if bad:
            c = (cen[i][0] + nx * raw[i], cen[i][1] + ny * raw[i])
        out[i] = c if look.fits(*c) else look.clamp_center(*c)
    return out, before


def describe(side, info):
    lm = ("; mean luma under the word: " + ", ".join("%s %.3f" % kv for kv in info["luma"].items())) if info["luma"] else ""
    return ("side scores (share of frames with a clear spot, mean px from the body): " +
            ", ".join("%s %.3f / %.0f" % (k, v[0], v[1]) for k, v in info["scores"].items()) +
            lm + "; chose %s; %d of %d label frames had no clear spot on it (%d in all); label frames with the word's box"
            " and margin touching the body: %d before the outward correction, %d after"
            % (side, info["held_visible"], info["shown"], info["held"], info["overlap0"], info["overlap"]))


# ---------------------------------------------------------------- video
def render(clip, track, out, text, from_s=None, side="auto", sheet=None):
    W, H, rate, fps, n = ML.probe(clip)
    header, rows, grids = load_outline(track)
    ML.check_counts(rows, n)
    from_frame = int(round((ML.DEFAULT_FROM_FRAC * n / fps if from_s is None else from_s) * fps))
    saved, ML.TEXT = ML.TEXT, text
    try:
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
        look, side, aim, cen, ops, info = plan(rows, grids, W, H, fps, side, from_frame, luma=luma, k=k)
        print(describe(side, info))
        print("clip %dx%d, %d frames at %s; side %s; label from frame %d" % (W, H, n, rate, side, from_frame))
        dec = subprocess.Popen(["ffmpeg", "-v", "error", "-i", clip, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                               stdout=subprocess.PIPE)
        enc = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "%dx%d" % (W, H),
                                "-r", rate, "-i", "-", "-i", clip, "-map", "0:v", "-map", "1:a?", "-c:v", "libx264",
                                "-crf", "16", "-pix_fmt", "yuv420p", "-c:a", "copy", out], stdin=subprocess.PIPE)
        keep = set(ML.sheet_frames(n)) if sheet else set()
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
                    ML.draw_label(f, look, cen[i][0], cen[i][1], aim[i][0], aim[i][1], float(ops[i]))
                    arr = (np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8)
                enc.stdin.write(arr.tobytes())
                if i in keep:
                    kept.append((i, arr.copy()))
        finally:
            enc.stdin.close()
            dec.stdout.close()
            enc.wait()
            dec.wait()
    finally:
        ML.TEXT = saved
    if enc.returncode:
        raise RuntimeError("ffmpeg encoder exited with %d" % enc.returncode)
    if sheet:
        ML.make_sheet(kept, sheet)
    print("wrote %s" % out)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("clip")
    ap.add_argument("track")
    ap.add_argument("out")
    ap.add_argument("--text", required=True)
    ap.add_argument("--from", dest="from_s", type=float, default=None)
    ap.add_argument("--side", default="auto", choices=("auto",) + ML.SIDES)
    ap.add_argument("--sheet", default=None)
    a = ap.parse_args(argv)
    for p in (a.clip, a.track):
        if not os.path.isfile(p):
            sys.exit("title_label: %s does not exist" % p)
    try:
        render(a.clip, a.track, a.out, a.text, a.from_s, a.side, a.sheet)
    except ValueError as e:
        sys.exit("title_label: %s" % e)
    return 0


if __name__ == "__main__":
    sys.exit(main())
