"""Writes Fusion compositions that put round 67's on-screen words and the mouth label over a clip as live Fusion text.

    python -I scripts/channel/fusion_overlay.py words <anchors.json> <out.comp> [--width 3840 --height 2160] [--first-frame 1]
    python -I scripts/channel/fusion_overlay.py mouth <track.json>   <out.comp> [--width 3840 --height 2160] [--from S]
                                                      [--side auto|left|right|up|down] [--fps 30]
    python -I scripts/channel/fusion_overlay.py title <outline.json> <out.comp> --text WORD [--from S] [--side ...]
                                                      [--width 3840 --height 2160] [--fps 30]

`title` is the mouth label on a whole creature: title_label.py places the word in the clear beside the body and aims the
leader at the body's outline, and this writes the same plan as the same composition.

The burned-in tools (r67_graphics/g_words.py, mouth_label.py) draw the words as pixels; these compositions place the
same words as Text+ tools with keyed inputs, so a wording is edited on Resolve's Fusion page and nothing re-renders.
Each composition takes the clip's picture from MediaIn1 and ends at MediaOut1, so TimelineItem.ImportFusionComp(path)
loads it onto a clip.

Format. The file is Fusion's text form, a Lua table `{ Tools = ordered() { ... }, ActiveTool = "MediaOut1" }`, the same
form as the `.setting` templates under Resolve's Support/Developer/Fusion Templates and as a `.comp` that
TimelineItem.ExportFusionComp writes. Resolve's scripting README gives only `ImportFusionComp(path)` ("a Fusion
composition from given file path") and names no extension; this tool writes `.comp`.

Coordinates. Fusion's image coordinates run 0-1 across the width and 0-1 up the height, origin bottom-left, y up;
the burned-in tools work in pixels from the top-left with y down. `fusion_xy` converts: x / W, 1 - y / H.
A Text+ centres its line box on its Center, where the burned-in tools put the baseline at the anchor, so
`text_center_y` moves the key up by the line box's offset from the baseline.

Time. Fusion frame k of the composition is scene frame k + first-frame (words) or track row k (mouth); a
keyframe is written for every frame, linear between, as a BezierSpline with Flags = { Linear = true }.

Assumptions Resolve has to confirm (see the report that came with this file): Text+ Size is in fractions of the
image height with the em as the unit; Blur's size is a Gaussian sigma in pixels; a Rectangle mask's Width and Height
share one unit, the image width; Text+ centres the font's ascent-plus-descent line box on Center.
"""
import argparse
import json
import math
import os
import sys

import numpy as np
from PIL import ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "r67_graphics"))     # -I leaves the script's folder off the path
sys.path.insert(0, HERE)
import g_words as GW            # noqa: E402
import mouth_label as ML        # noqa: E402
import title_label as TL        # noqa: E402

# Fusion's face names for each of g_words' faces: (Text+ Font, Text+ Style). The files' own name tables say the same
# (PIL's font.getname()). Fusion lists the fonts the operating system has installed.
FUSION_FONTS = {"mono-semibold": ("IBM Plex Mono", "SemiBold"),
                "mono": ("IBM Plex Mono", "Regular"),
                "sans": ("IBM Plex Sans", "Regular")}
BLUR_PER_SIGMA = 1.0            # Fusion Blur size per pixel of Gaussian sigma: assumed 1
MASK_UNIT = "width"             # a Rectangle mask's Width and Height are fractions of the image width: assumed


# ---------------------------------------------------------------- coordinates
def fusion_xy(px, py, W, H):
    """Pixel (origin top-left, y down) to Fusion image coordinates (0-1, origin bottom-left, y up)."""
    return px / float(W), 1.0 - py / float(H)


def line_box_offset_em(face):
    """How far above the baseline the middle of the font's line box (ascent plus descent) sits, in ems."""
    f = ImageFont.truetype(os.path.join(GW.FONTS, GW.FACES[face]), 1000)
    asc, desc = f.getmetrics()
    return (asc - desc) / 2.0 / 1000.0


def text_center_y(baseline_py, em_px, face, H):
    """Fusion y at which a Text+ line box is centred so that its baseline lies at pixel row baseline_py."""
    return 1.0 - baseline_py / float(H) + line_box_offset_em(face) * em_px / float(H)


def mean_advance_em(text, face):
    f = ImageFont.truetype(os.path.join(GW.FONTS, GW.FACES[face]), 1000)
    return f.getlength(text) / len(text) / 1000.0


def char_spacing(text, face, em_px, tracking_px):
    """Text+ CharacterSpacing multiplies each advance: 1 is the font's own. The mono faces are exact; the sans face
    gets its mean advance over the word."""
    return 1.0 + tracking_px / (mean_advance_em(text, face) * em_px) if tracking_px else 1.0


# ---------------------------------------------------------------- Lua text
def num(v):
    v = float(v)
    if v == int(v) and abs(v) < 1e9:
        return str(int(v))
    return ("%.6f" % v).rstrip("0").rstrip(".")


def lstr(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'


class Src:
    """An input fed by another tool's output (SourceOp, Source)."""

    def __init__(self, op, source="Output"):
        self.op, self.source = op, source


class Fuid:
    def __init__(self, name):
        self.name = name


class Comp:
    def __init__(self):
        self.tools = []                       # (name, text) in order
        self.names = set()
        self._n = 0

    def _add(self, name, text):
        if name in self.names:
            raise ValueError("tool %s twice" % name)
        self.names.add(name)
        self.tools.append((name, text))

    def _pos(self):
        i = self._n
        self._n += 1
        return "ViewInfo = OperatorInfo { Pos = { %d, %d } }," % (110 * (i % 12), 55 * (i // 12))

    @staticmethod
    def _input(v):
        if isinstance(v, Src):
            return "Input { SourceOp = %s, Source = %s, }" % (lstr(v.op), lstr(v.source))
        if isinstance(v, Fuid):
            return "Input { Value = FuID { %s }, }" % lstr(v.name)
        if isinstance(v, str):
            return "Input { Value = %s, }" % lstr(v)
        return "Input { Value = %s, }" % num(v)

    def tool(self, name, ctype, inputs, extra=""):
        body = "".join("\t\t\t\t%s = %s,\n" % (k, self._input(v)) for k, v in inputs.items())
        text = "\t\t%s = %s {\n%s\t\t\tInputs = {\n%s\t\t\t},\n\t\t\t%s\n\t\t},\n" % (
            name, ctype, extra, body, self._pos())
        self._add(name, text)

    def spline(self, name, keys, color=(0, 200, 255)):
        """keys: [(frame, value)], written as a linear BezierSpline."""
        ks = "".join("\t\t\t\t[%d] = { %s, Flags = { Linear = true } },\n" % (f, num(v)) for f, v in keys)
        text = ("\t\t%s = BezierSpline {\n\t\t\tSplineColor = { Red = %d, Green = %d, Blue = %d },\n"
                "\t\t\tNameSet = true,\n\t\t\tKeyFrames = {\n%s\t\t\t},\n\t\t},\n" % (name, *color, ks))
        self._add(name, text)

    def path(self, name, xs, ys):
        """A keyed point: an XYPath over two splines. Returns the Src that feeds a Point input."""
        self.tool_raw_path(name)
        self.spline(name + "_X", xs, (255, 80, 80))
        self.spline(name + "_Y", ys, (80, 255, 80))
        # an XYPath's output is "Value" (Resolve 21.1, 2026-11-15: "Position" loaded with the point unconnected)
        return Src(name, "Value")

    def tool_raw_path(self, name):
        text = ("\t\t%s = XYPath {\n\t\t\tShowKeyPoints = false,\n\t\t\tInputs = {\n"
                "\t\t\t\tX = Input { SourceOp = %s, Source = \"Value\", },\n"
                "\t\t\t\tY = Input { SourceOp = %s, Source = \"Value\", },\n\t\t\t},\n\t\t},\n"
                % (name, lstr(name + "_X"), lstr(name + "_Y")))
        self._add(name, text)

    def render(self):
        return "{\n\tTools = ordered() {\n%s\t},\n\tActiveTool = \"MediaOut1\"\n}\n" % "".join(t for _, t in self.tools)


def start_comp():
    c = Comp()
    c._add("MediaIn1", "\t\tMediaIn1 = MediaIn {\n\t\t\t%s\n\t\t},\n" % c._pos())
    return c


def finish_comp(c, last):
    c.tool("MediaOut1", "MediaOut", {"Index": "0", "Input": Src(last)})
    return c.render()


def srgb(h):
    r, g, b = GW.hexrgb(h)
    return float(r), float(g), float(b)


def text_plus(c, name, word, face, em_px, spacing, color, center, W, H):
    font, style = FUSION_FONTS[face]
    r, g, b = srgb(color)
    c.tool(name, "TextPlus", {
        "Center": center, "UseFrameFormatSettings": 1, "Width": W, "Height": H,
        "Font": font, "Style": style, "StyledText": word, "Size": em_px / float(H), "CharacterSpacing": spacing,
        "Red1": r, "Green1": g, "Blue1": b})


def blur(c, name, src, sigma_spline):
    c.tool(name, "Blur", {"Filter": Fuid("Gaussian"), "LockXY": 1, "XBlurSize": Src(sigma_spline, "Value"),
                          "Input": Src(src)})


def merge(c, name, bg, fg, blend):
    c.tool(name, "Merge", {"Background": Src(bg), "Foreground": Src(fg), "Blend": blend})


# ---------------------------------------------------------------- words
def word_plan(doc, W, H, first_frame):
    """Per word, the per-frame keys the composition holds, as plain numbers. Fusion frame k is scene frame
    k + first_frame."""
    s = H / 1080.0
    fps = doc.get("fps", 30)
    frames = sorted(int(f) for f in doc["frames"] if int(f) >= first_frame)
    if not frames:
        raise ValueError("the anchors file has no frame at or after %d" % first_frame)
    out = {}
    for wid, w in doc["words"].items():
        if w.get("align", "middle") != "middle":
            raise ValueError("word %s has align %r; only middle is built" % (wid, w["align"]))
        face = w["face"]
        em = w["size_px"] * s
        rows = []
        last = None
        for f in frames:
            t = (f - 1) / float(fps)
            a = doc["frames"][str(f)].get(w["anchor"])
            if a is None:
                px, py, coc = last if last else (W / 2.0 / s, H / 2.0 / s, 0.0)
                op = 0.0
            else:
                px, py, coc = a
                last = (px, py, coc)
                op = GW.opacity_at(w["opacity"], t)
            x, y = fusion_xy(px * s, py * s, W, H)
            y = text_center_y(py * s, em, face, H)
            sigma = max(0.0, coc * s / 2.0)
            rows.append({"frame": f - first_frame, "x": x, "y": y, "px": px * s, "py": py * s, "sigma": sigma,
                         "glow_sigma": math.hypot(sigma, 8.0 * s), "op": op})
        out[wid] = {"word": w, "em": em, "rows": rows}
    return out


def build_words(doc, W, H, first_frame=1):
    plan = word_plan(doc, W, H, first_frame)
    c = start_comp()
    prev = "MediaIn1"
    for i, (wid, p) in enumerate(plan.items()):
        w, rows, em = p["word"], p["rows"], p["em"]
        n = "W%d" % (i + 1)
        pos = c.path(n + "_Pos", [(r["frame"], r["x"]) for r in rows], [(r["frame"], r["y"]) for r in rows])
        c.spline(n + "_Sigma", [(r["frame"], r["sigma"] * BLUR_PER_SIGMA) for r in rows])
        c.spline(n + "_GlowSigma", [(r["frame"], r["glow_sigma"] * BLUR_PER_SIGMA) for r in rows])
        c.spline(n + "_Opacity", [(r["frame"], r["op"]) for r in rows], (255, 255, 80))
        c.spline(n + "_GlowOpacity", [(r["frame"], r["op"] * w.get("glow_alpha", 0.45)) for r in rows], (80, 255, 255))
        sp = char_spacing(w["text"], w["face"], em, w.get("tracking_px", 0) * H / 1080.0)
        text_plus(c, n + "_Glow", w["text"], w["face"], em, sp, w.get("glow", "#9ff5e6"), pos, W, H)
        text_plus(c, n + "_Text", w["text"], w["face"], em, sp, w["color"], pos, W, H)
        blur(c, n + "_GlowBlur", n + "_Glow", n + "_GlowSigma")
        blur(c, n + "_TextBlur", n + "_Text", n + "_Sigma")
        merge(c, n + "_MergeGlow", prev, n + "_GlowBlur", Src(n + "_GlowOpacity", "Value"))
        merge(c, n + "_MergeText", n + "_MergeGlow", n + "_TextBlur", Src(n + "_Opacity", "Value"))
        prev = n + "_MergeText"
    return finish_comp(c, prev)


# ---------------------------------------------------------------- mouth
def mouth_plan(rows, W, H, fps, side, from_frame, planned=None):
    """Per row, what the composition keys: the word's line-box centre, the leader's rectangle and the opacities.
    The smoothing, the side choice, the fades and the leader's ends are mouth_label.py's own functions. `planned` is
    a ready (look, side, aim, centres, opacities) from title_label.plan, whose aim points are the leader's targets."""
    look, side, disc, cen, ops = planned if planned else ML.plan(rows, W, H, fps, side, from_frame)
    s = look.s
    em = ML.SIZE_PX * s
    out, ang_prev = [], []
    for i in range(len(rows)):
        cx, cy = cen[i]
        dx, dy = disc[i]
        baseline = cy - look.mid
        x, _ = fusion_xy(cx, cy, W, H)
        y = text_center_y(baseline, em, "mono-semibold", H)
        pts = ML.leader_points(look, cx, cy, dx, dy)
        row = {"frame": i, "x": x, "y": y, "cx": float(cx), "cy": float(cy), "op": float(ops[i]), "lead": 1.0 if pts else 0.0}
        if pts:
            (ax, ay), (ex, ey) = pts
            mx, my = fusion_xy((ax + ex) / 2.0, (ay + ey) / 2.0, W, H)
            row.update(lx=mx, ly=my, length=math.hypot(ex - ax, ey - ay), ang=math.atan2(-(ey - ay), ex - ax))
        out.append(row)
    # a leader out of sight keeps the last rectangle, so nothing flies about while it is off; angles are unwrapped
    # modulo 180 degrees, since a rectangle turned half a turn is the same rectangle
    first = next((r for r in out if r["lead"]), None)
    if first:
        hold = dict(first)
        for r in out:
            if r["lead"]:
                hold = r
            else:
                for k in ("lx", "ly", "length", "ang"):
                    r[k] = hold[k]
                r["length"] = 0.0
        angs = np.unwrap(np.array([r["ang"] for r in out]) * 2.0) / 2.0
        for r, a in zip(out, angs):
            r["ang"] = float(a)
    else:
        for r in out:
            r.update(lx=0.5, ly=0.5, length=0.0, ang=0.0)
    return look, side, out


def build_mouth(rows, W, H, fps=30.0, side="auto", from_s=None, text="mouth"):
    """The mouth label's composition. `text` is the label's word ("mouth", or a name such as "Nibblers" on a body
    track): mouth_label.py sizes the word's box from its module TEXT, so it is set for the plan and put back after."""
    saved, ML.TEXT = ML.TEXT, text
    try:
        return _build_mouth(rows, W, H, fps, side, from_s)
    finally:
        ML.TEXT = saved


def build_title(rows, grids, W, H, fps=30.0, side="auto", from_s=None, text="Title"):
    """The title label's composition: the mouth label's tools, keyed from title_label.plan. Returns (text, side, info)."""
    saved, ML.TEXT = ML.TEXT, text
    try:
        out = _build_mouth(rows, W, H, fps, side, from_s, grids)
        return out[0], out[1], out[2]
    finally:
        ML.TEXT = saved


def _build_mouth(rows, W, H, fps, side, from_s, grids=None):
    n = len(rows)
    from_frame = int(round((ML.DEFAULT_FROM_FRAC * n / fps if from_s is None else from_s) * fps))
    planned, info = None, None
    if grids is not None:
        pl = TL.plan(rows, grids, W, H, fps, side, from_frame)
        planned, info = pl[:5], pl[5]
        side = pl[1]
    look, side, p = mouth_plan(rows, W, H, fps, side, from_frame, planned)
    s, em = look.s, ML.SIZE_PX * look.s
    c = start_comp()
    fr = lambda key, f=lambda v: v: [(r["frame"], f(r[key])) for r in p]       # noqa: E731
    pos = c.path("Word_Pos", fr("x"), fr("y"))
    c.path("Leader_Pos", fr("lx"), fr("ly"))
    c.spline("Op_Word", fr("op"), (255, 255, 80))
    # the leader's own presence multiplies its opacity
    c.spline("Op_Leader", [(r["frame"], r["op"] * ML.LEADER_ALPHA * r["lead"]) for r in p], (255, 160, 80))
    c.spline("Lead_On", [(r["frame"], ML.LEADER_ALPHA * r["lead"]) for r in p], (160, 255, 80))
    c.spline("Op_Glow", fr("op", lambda v: v * ML.GLOW_ALPHA), (80, 255, 255))
    c.spline("Op_Halo", fr("op", lambda v: v * ML.HALO_ALPHA), (160, 80, 255))
    c.spline("Len", fr("length", lambda v: v / W))
    c.spline("Ang", fr("ang", math.degrees))
    c.spline("Sig_Glow", [(0, ML.GLOW_SIGMA * s * BLUR_PER_SIGMA)])
    c.spline("Sig_Halo", [(0, ML.HALO_SIGMA * s * BLUR_PER_SIGMA)])
    # the leader is one Rectangle mask, keyed on its centre, length and angle (four channels; a Polygon would key
    # eight), cutting three Backgrounds: the ink, the glow's source and the halo's source
    c.tool("Leader_Mask", "RectangleMask", {
        "Center": Src("Leader_Pos", "Value"), "Width": Src("Len", "Value"), "Height": ML.LEADER_W * s / W, "Angle": Src("Ang", "Value"),
        "UseFrameFormatSettings": 1, "Solid": 1, "MaskWidth": W, "MaskHeight": H}, "")
    sp = 1.0
    for nm, col in (("Word", ML.INK), ("Glow", ML.GLOW), ("Halo", ML.HALO)):
        text_plus(c, "Text_" + nm, ML.TEXT, "mono-semibold", em, sp, col, pos, W, H)
        r, g, b = srgb(col)
        c.tool("Lead_" + nm, "Background", {
            "UseFrameFormatSettings": 1, "Width": W, "Height": H, "TopLeftRed": r, "TopLeftGreen": g, "TopLeftBlue": b,
            "TopLeftAlpha": 1, "EffectMask": Src("Leader_Mask", "Mask")})
    for nm, sig in (("Glow", "Sig_Glow"), ("Halo", "Sig_Halo")):
        merge(c, "Mix_" + nm, "Text_" + nm, "Lead_" + nm, Src("Lead_On", "Value"))
        blur(c, "Blur_" + nm, "Mix_" + nm, sig)
    merge(c, "Over_Halo", "MediaIn1", "Blur_Halo", Src("Op_Halo", "Value"))
    merge(c, "Over_Glow", "Over_Halo", "Blur_Glow", Src("Op_Glow", "Value"))
    merge(c, "Over_Leader", "Over_Glow", "Lead_Word", Src("Op_Leader", "Value"))
    merge(c, "Over_Word", "Over_Leader", "Text_Word", Src("Op_Word", "Value"))
    if grids is not None:
        return finish_comp(c, "Over_Word"), side, info
    return finish_comp(c, "Over_Word"), side


# ---------------------------------------------------------------- command line
def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("kind", choices=("words", "mouth", "title"))
    ap.add_argument("src")
    ap.add_argument("out")
    ap.add_argument("--width", type=int, default=3840)
    ap.add_argument("--height", type=int, default=2160)
    ap.add_argument("--first-frame", type=int, default=1)
    ap.add_argument("--from", dest="from_s", type=float, default=None)
    ap.add_argument("--side", default="auto", choices=("auto",) + ML.SIDES)
    ap.add_argument("--fps", type=float, default=30.0)
    ap.add_argument("--text", default="mouth", help="mouth, title: the label's word")
    a = ap.parse_args(argv)
    if not os.path.isfile(a.src):
        sys.exit("fusion_overlay: %s does not exist" % a.src)
    try:
        if a.kind == "words":
            text = build_words(json.load(open(a.src, encoding="utf-8-sig")), a.width, a.height, a.first_frame)
        elif a.kind == "title":
            header, rows, grids = TL.load_outline(a.src)
            text, side, info = build_title(rows, grids, a.width, a.height, a.fps, a.side, a.from_s, a.text)
            print(TL.describe(side, info))
        else:
            header, rows = ML.load_track(a.src)
            for k, v in header.items():
                print("track %s: %s" % (k, json.dumps(v)))
            text, side = build_mouth(rows, a.width, a.height, a.fps, a.side, a.from_s, a.text)
            print("side %s" % side)
    except ValueError as e:
        sys.exit("fusion_overlay: %s" % e)
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %s (%d bytes)" % (a.out, len(text)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
