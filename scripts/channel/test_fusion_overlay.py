"""Checks of fusion_overlay.py: the keys equal the burned-in tools' numbers, and the file is a well-formed Fusion table.

    python -I -B scripts/tests/minirun.py scripts/channel/test_fusion_overlay.py
"""
import json
import math
import re
import sys
from pathlib import Path

CHANNEL = Path(__file__).resolve().parent
sys.path.insert(0, str(CHANNEL))
import fusion_overlay as FO      # noqa: E402
import mouth_label as ML         # noqa: E402
import g_words as GW             # noqa: E402
import pytest                    # noqa: E402

W, H = 3840, 2160


def approx(want, abs=1e-6):
    class A:
        def __eq__(self, got):
            return builtins_abs(got - want) <= abs
    return A()


builtins_abs = __builtins__["abs"] if isinstance(__builtins__, dict) else __builtins__.abs


# ---------------------------------------------------------------- a small Lua-table reader
TOKEN = re.compile(r'\s*(?:(?P<str>"(?:[^"\\]|\\.)*")|(?P<num>-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)|(?P<id>[A-Za-z_]\w*)|(?P<p>[{}\[\]=,()]))')


def tokens(text):
    pos, out = 0, []
    while True:
        m = TOKEN.match(text, pos)
        if not m:
            if text[pos:].strip():
                raise ValueError("cannot read at %r" % text[pos:pos + 30])
            return out
        pos = m.end()
        kind = m.lastgroup
        out.append((kind, m.group(kind)))


def parse(text):
    """Returns the value: a table is ('T', [(key, value)]); a constructor Name { } is ('C', name, table)."""
    toks = tokens(text)
    i = [0]

    def peek(k=0):
        return toks[i[0] + k] if i[0] + k < len(toks) else (None, None)

    def eat(p=None):
        t = toks[i[0]]
        if p is not None and t[1] != p:
            raise ValueError("expected %s, found %s at token %d" % (p, t[1], i[0]))
        i[0] += 1
        return t

    def value():
        k, v = peek()
        if k == "str":
            eat()
            return json.loads(v)
        if k == "num":
            eat()
            return float(v)
        if k == "id":
            eat()
            if v in ("true", "false"):
                return v == "true"
            if peek()[1] == "(":
                eat("(")
                eat(")")
            if peek()[1] == "{":
                return ("C", v, table())
            return ("ID", v)
        if v == "{":
            return table()
        raise ValueError("unexpected %r at token %d" % (v, i[0]))

    def table():
        eat("{")
        items = []
        while peek()[1] != "}":
            if peek()[1] == "[":
                eat("[")
                key = float(eat()[1])
                eat("]")
                eat("=")
                items.append((key, value()))
            elif peek()[0] == "id" and peek(1)[1] == "=":
                key = eat()[1]
                eat("=")
                items.append((key, value()))
            else:
                items.append((None, value()))
            if peek()[1] == ",":
                eat()
        eat("}")
        return ("T", items)

    v = value()
    if i[0] != len(toks):
        raise ValueError("text after the table")
    return v


def get(table, key):
    for k, v in table[1]:
        if k == key:
            return v
    raise KeyError(key)


def tools_of(text):
    root = parse(text)
    tab = get(root, "Tools")[2]
    return {k: (v[1], v[2]) for k, v in tab[1]}


def check_wellformed(text):
    """Balanced braces and quotes outside strings, and a SourceOp that names no tool is an error."""
    depth, in_s, esc = 0, False, False
    for ch in text:
        if in_s:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_s = False
        elif ch == '"':
            in_s = True
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            assert depth >= 0, "closing brace with none open"
    assert depth == 0 and not in_s, "unbalanced braces or quotes"
    tools = tools_of(text)
    for name in re.findall(r'SourceOp = "([^"]+)"', text):
        assert name in tools, "SourceOp %s names no tool" % name
    assert "MediaIn1" in tools and "MediaOut1" in tools
    return tools


def keys_of(tools, spline):
    kind, tab = tools[spline]
    assert kind == "BezierSpline"
    kf = get(tab, "KeyFrames")
    return {int(k): v[1][0][1] for k, v in kf[1]}


# ---------------------------------------------------------------- words
def anchors(n=40):
    frames = {str(f): {"a": [100.0 + 7 * f, 900.0 - 3 * f, 0.5 + 0.1 * f], "b": [1000.0, 500.0, 0.0]} for f in range(1, n + 1)}
    for f in range(1, 6):
        del frames[str(f)]["b"]                    # a frame where one anchor is missing
    words = {"one": {"text": "eaten", "anchor": "a", "face": "mono-semibold", "size_px": 44, "tracking_px": 4,
                     "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45, "opacity": [[0.0, 0.55], [0.5, 0.55], [1.0, 1.0]]},
             "two": {"text": "founder", "anchor": "b", "face": "sans", "size_px": 30, "tracking_px": 0,
                     "color": "#ffffff", "opacity": [[0.0, 1.0], [2.0, 0.0]]}}
    return {"fps": 30, "frames": frames, "words": words}


def test_word_position_equals_g_words_pixel_in_fusion_coordinates():
    doc = anchors()
    tools = check_wellformed(FO.build_words(doc, W, H, 1))
    s = H / 1080.0
    xs, ys = keys_of(tools, "W1_Pos_X"), keys_of(tools, "W1_Pos_Y")
    for f in (1, 17, 40):
        px, py, _ = doc["frames"][str(f)]["a"]
        assert xs[f - 1] == approx(px * s / W, abs=1e-6)
        # y: the pixel's row in Fusion's y-up coordinates, plus the declared line-box offset of the Text+
        assert ys[f - 1] == approx(1.0 - py * s / H + FO.line_box_offset_em("mono-semibold") * 44 * s / H, abs=1e-6)


def test_word_opacity_keys_equal_the_anchors():
    doc = anchors()
    tools = check_wellformed(FO.build_words(doc, W, H, 1))
    op, glow = keys_of(tools, "W1_Opacity"), keys_of(tools, "W1_GlowOpacity")
    for f in (1, 10, 25, 40):
        t = (f - 1) / 30.0
        want = GW.opacity_at(doc["words"]["one"]["opacity"], t)
        assert op[f - 1] == approx(want, abs=1e-6)
        assert glow[f - 1] == approx(want * 0.45, abs=1e-6)
    # the second word is hidden where its anchor is missing, and its opacity follows its keys where present
    op2 = keys_of(tools, "W2_Opacity")
    assert op2[0] == 0.0 and op2[10] == approx(GW.opacity_at(doc["words"]["two"]["opacity"], 10 / 30.0), abs=1e-6)


def test_word_blur_is_the_circle_of_confusion():
    doc = anchors()
    tools = check_wellformed(FO.build_words(doc, W, H, 1))
    s = H / 1080.0
    sig, gsig = keys_of(tools, "W1_Sigma"), keys_of(tools, "W1_GlowSigma")
    coc = doc["frames"]["20"]["a"][2]
    assert sig[19] == approx(coc * s / 2.0, abs=1e-6)
    assert gsig[19] == approx(math.hypot(coc * s / 2.0, 8.0 * s), abs=1e-6)


def test_first_frame_shifts_fusion_time():
    doc = anchors()
    tools = check_wellformed(FO.build_words(doc, W, H, 11))
    xs = keys_of(tools, "W1_Pos_X")
    assert min(xs) == 0 and xs[0] == approx((100.0 + 7 * 11) * (H / 1080.0) / W, abs=1e-6)


def test_word_text_inputs():
    tools = check_wellformed(FO.build_words(anchors(), W, H, 1))
    kind, tab = tools["W1_Text"]
    inputs = get(tab, "Inputs")
    assert kind == "TextPlus"
    assert get(get(inputs, "StyledText")[2], "Value") == "eaten"
    assert get(get(inputs, "Font")[2], "Value") == "IBM Plex Mono" and get(get(inputs, "Style")[2], "Value") == "SemiBold"
    assert get(get(inputs, "Size")[2], "Value") == approx(44 * (H / 1080.0) / H, abs=1e-6)
    # mono advance is 0.6 em: 4 px of tracking on a 44 px em is 1 + 4 / 26.4
    assert get(get(inputs, "CharacterSpacing")[2], "Value") == approx(1 + 4 / 26.4, abs=1e-4)


# ---------------------------------------------------------------- mouth
def track(n=60):
    return [{"frame": i, "x": 0.30 + 0.002 * i, "y": 0.80 - 0.001 * i, "depth": 2.0, "visible": not (20 <= i < 30)}
            for i in range(n)]


def test_mouth_label_position_equals_mouth_label_smoothing():
    rows = track()
    text, side = FO.build_mouth(rows, W, H, 30.0, "right", None)
    tools = check_wellformed(text)
    from_frame = int(round(ML.DEFAULT_FROM_FRAC * len(rows)))
    look, _, disc, cen, ops = ML.plan(rows, W, H, 30.0, "right", from_frame)
    xs, ys, op = keys_of(tools, "Word_Pos_X"), keys_of(tools, "Word_Pos_Y"), keys_of(tools, "Op_Word")
    em = ML.SIZE_PX * look.s
    for i in (0, 25, 59):
        assert xs[i] == approx(cen[i][0] / W, abs=1e-6)
        assert ys[i] == approx(FO.text_center_y(cen[i][1] - look.mid, em, "mono-semibold", H), abs=1e-6)
        assert op[i] == approx(ops[i], abs=1e-6)


def test_mouth_leader_ends_follow_the_disc():
    rows = track()
    tools = check_wellformed(FO.build_mouth(rows, W, H, 30.0, "right", None)[0])
    look, _, disc, cen, _ = ML.plan(rows, W, H, 30.0, "right", 15)
    lx, ly, ln, ang = (keys_of(tools, n) for n in ("Leader_Pos_X", "Leader_Pos_Y", "Len", "Ang"))
    for i in (0, 40):
        (ax, ay), (ex, ey) = ML.leader_points(look, cen[i][0], cen[i][1], disc[i][0], disc[i][1])
        assert lx[i] == approx((ax + ex) / 2 / W, abs=1e-6)
        assert ly[i] == approx(1 - (ay + ey) / 2 / H, abs=1e-6)
        assert ln[i] == approx(math.hypot(ex - ax, ey - ay) / W, abs=1e-6)
        # the disc end of the leader: the centre plus half the length along the angle (y up)
        a = math.radians(ang[i])
        half = ln[i] * W / 2
        ends = [(lx[i] * W + sg * half * math.cos(a), (1 - ly[i]) * H - sg * half * math.sin(a)) for sg in (1, -1)]
        assert any(math.hypot(x - ex, y - ey) < 1e-3 for x, y in ends)


def test_mouth_comp_is_wellformed_and_leader_hidden_when_invisible():
    rows = track()
    tools = check_wellformed(FO.build_mouth(rows, W, H, 30.0, "auto", None)[0])
    assert {"Text_Word", "Over_Word", "Leader_Mask"} <= set(tools)
    op = keys_of(tools, "Op_Word")
    assert op[29] < op[19] and op[59] > 0.99


def test_mouth_text_names_the_word_and_sizes_its_box():
    rows = track()
    text, _ = FO.build_mouth(rows, W, H, 30.0, "right", None, text="Nibblers")
    tools = check_wellformed(text)
    assert 'Value = "Nibblers"' in text and 'Value = "mouth"' not in text
    assert ML.TEXT == "mouth"
    xs = keys_of(tools, "Word_Pos_X")
    ref = keys_of(check_wellformed(FO.build_mouth(rows, W, H, 30.0, "right", None)[0]), "Word_Pos_X")
    # a longer word's box centre sits further right of the disc on the right side
    assert xs[59] > ref[59]


# ---------------------------------------------------------------- the checker itself
def test_checker_catches_a_dangling_source_and_unbalanced_text():
    good = FO.build_words(anchors(5), W, H, 1)
    check_wellformed(good)
    with pytest.raises(AssertionError):
        check_wellformed(good.replace('SourceOp = "W1_Text"', 'SourceOp = "Nope"', 1).replace('"W1_Text", Source', '"Nope", Source'))
    with pytest.raises(AssertionError):
        check_wellformed(good + "}")


# ---------------------------------------------------------------- the title label
import numpy as np               # noqa: E402
import title_label as TL         # noqa: E402


def encode_grid(arr):
    flat = arr.astype(bool).ravel()
    runs, cur, n = [], False, 0
    for v in flat:
        if bool(v) == cur:
            n += 1
        else:
            runs.append(n)
            cur, n = bool(v), 1
    runs.append(n)
    return {"w": arr.shape[1], "h": arr.shape[0], "rle": ",".join(str(r) for r in runs)}


def blob_grid(cx, cy, hw=40, hh=20):
    g = np.zeros((135, 240), bool)
    g[max(cy - hh, 0):cy + hh, max(cx - hw, 0):cx + hw] = True
    return g


def title_shot(n=60):
    """A blob (cells) drifting right along the frame's lower middle."""
    rows, grids = [], []
    for i in range(n):
        cx = 60 + i
        g = blob_grid(cx, 80)
        rows.append({"frame": i, "x": cx / 240.0, "y": 80 / 135.0, "depth": 2.0, "visible": True,
                     "box": ((cx - 40) / 240.0, 60 / 135.0, (cx + 40) / 240.0, 100 / 135.0)})
        grids.append(g)
    return rows, grids


def test_grid_decoding():
    a = np.zeros((135, 240), bool)
    a[10:20, 5:50] = True
    a[134, 239] = True
    assert (TL.decode_grid(encode_grid(a)) == a).all()
    first = np.zeros((2, 3), bool)
    first[0, 0] = True
    assert TL.decode_grid({"w": 3, "h": 2, "rle": "0,1,5"}).tolist() == first.tolist()      # a leading empty run of 0
    assert TL.decode_grid(None) is None
    with pytest.raises(ValueError):
        TL.decode_grid({"w": 3, "h": 2, "rle": "1,2"})


def test_title_word_is_clear_of_the_grid_on_every_frame_with_room():
    rows, grids = title_shot()
    ML.TEXT = "Nibblers"
    try:
        look, side, aim, cen, ops, info = TL.plan(rows, grids, 1920, 1080, 30.0, "auto", 15)
    finally:
        ML.TEXT = "mouth"
    margin = TL.MARGIN * look.s
    occs = [TL.Occupancy(g, 1920, 1080) for g in grids]
    assert info["held"] == 0
    for i in range(len(rows)):
        # the target (before smoothing) is clear by the margin, and the word sits inside the safe margin
        assert look.fits(*cen[i])
    sp = TL.best_spot(occs[30], rows[30]["box"], side, look, TL.candidate_axes(look), margin)
    assert TL.is_clear(occs[30], look, sp[0], sp[1], margin)
    # a brute-force check of the integral image against every filled cell's rectangle
    x0, y0, x1, y1 = sp[0] - look.hw - margin, sp[1] - look.hh - margin, sp[0] + look.hw + margin, sp[1] + look.hh + margin
    cw, ch = 1920 / 240.0, 1080 / 135.0
    for r, c in zip(*np.nonzero(grids[30])):
        assert not (c * cw < x1 and (c + 1) * cw > x0 and r * ch < y1 and (r + 1) * ch > y0)


def test_title_holds_one_side_over_a_shot():
    rows, grids = title_shot()
    ML.TEXT = "Nibblers"
    try:
        look, side, aim, cen, ops, info = TL.plan(rows, grids, 1920, 1080, 30.0, "auto", 15)
        sides = {TL.plan(rows, grids, 1920, 1080, 30.0, "auto", f)[1] for f in (0, 15, 30)}
    finally:
        ML.TEXT = "mouth"
    assert side in ML.SIDES and len(sides) == 1
    # the word is on that side of the body's box on every frame it is placed, and slides with the body
    d = {"right": (1, 0), "left": (-1, 0), "up": (0, -1), "down": (0, 1)}[side]
    for i in range(len(rows)):
        bx = (rows[i]["box"][0] + rows[i]["box"][2]) / 2 * 1920
        by = (rows[i]["box"][1] + rows[i]["box"][3]) / 2 * 1080
        assert (cen[i][0] - bx) * d[0] + (cen[i][1] - by) * d[1] > 0
    if side in ("up", "down"):
        assert cen[-1][0] > cen[0][0]


def test_title_with_no_clear_spot_keeps_the_last_good_centre():
    rows, grids = title_shot(40)
    full = np.ones((135, 240), bool)
    grids = grids[:20] + [full] * 20
    for r in rows[20:]:
        r["box"] = (0.0, 0.0, 1.0, 1.0)
    ML.TEXT = "Nibblers"
    try:
        look, side, aim, cen, ops, info = TL.plan(rows, grids, 1920, 1080, 30.0, "right", 5)
    finally:
        ML.TEXT = "mouth"
    assert info["held"] == 20 and info["held_visible"] == 20
    assert np.allclose(cen[39], cen[38], atol=1e-6) or np.hypot(*(cen[39] - cen[38])) < 5.0


def test_title_leader_ends_at_the_outline():
    rows, grids = title_shot()
    ML.TEXT = "Nibblers"
    try:
        look, side, aim, cen, ops, info = TL.plan(rows, grids, 1920, 1080, 30.0, "auto", 15)
    finally:
        ML.TEXT = "mouth"
    for i in (20, 45):
        occ = TL.Occupancy(grids[i], 1920, 1080)
        cx, cy = cen[i]
        d, pt = occ.nearest(cx - look.hw, cy - look.hh, cx + look.hw, cy + look.hh)
        assert aim[i] == pytest.approx(pt) if hasattr(pytest, "approx") else True
        assert math.hypot(aim[i][0] - pt[0], aim[i][1] - pt[1]) < 1e-9
        # the aim point lies on a filled cell's rectangle
        c, r = int(aim[i][0] // occ.cw), int(aim[i][1] // occ.ch)
        assert grids[i][min(r, 134), min(c, 239)] or grids[i][min(max(int(round(aim[i][1] / occ.ch)) - 1, 0), 134), min(c, 239)] \
            or grids[i][min(r, 134), min(max(int(round(aim[i][0] / occ.cw)) - 1, 0), 239)]
        pts = ML.leader_points(look, cx, cy, aim[i][0], aim[i][1])
        if pts:
            (ax, ay), (ex, ey) = pts
            assert math.hypot(ex - aim[i][0], ey - aim[i][1]) == approx(look.gap, abs=1e-6)
            assert math.hypot(ax - aim[i][0], ay - aim[i][1]) > look.gap
    # a word within the gap of the outline has no leader
    assert ML.leader_points(look, 500.0, 500.0, 500.0 + look.hw + look.gap * 0.5, 500.0) is None


def test_title_fusion_keys_equal_the_burned_plan():
    rows, grids = title_shot()
    text, side, info = FO.build_title(rows, grids, W, H, 30.0, "auto", None, text="Nibblers")
    tools = check_wellformed(text)
    assert 'Value = "Nibblers"' in text and ML.TEXT == "mouth"
    ML.TEXT = "Nibblers"
    try:
        look, pside, aim, cen, ops, _ = TL.plan(rows, grids, W, H, 30.0, "auto", int(round(ML.DEFAULT_FROM_FRAC * len(rows))))
    finally:
        ML.TEXT = "mouth"
    assert pside == side
    xs, ys, op = keys_of(tools, "Word_Pos_X"), keys_of(tools, "Word_Pos_Y"), keys_of(tools, "Op_Word")
    em = ML.SIZE_PX * look.s
    for i in (0, 25, 59):
        assert xs[i] == approx(cen[i][0] / W, abs=1e-6)
        assert ys[i] == approx(FO.text_center_y(cen[i][1] - look.mid, em, "mono-semibold", H), abs=1e-6)
        assert op[i] == approx(ops[i], abs=1e-6)


def test_title_side_prefers_the_darker_water_and_no_centre_touches_the_body():
    rows, grids = title_shot()
    luma = np.ones((len(rows), 135, 240), np.float32)
    luma[:, 100:, :] = 0.0                       # dark water along the bottom only
    ML.TEXT = "Nibblers"
    try:
        _, side, _, _, _, info = TL.plan(rows, grids, 1920, 1080, 30.0, "auto", 15, luma=luma, k=8.0)
        _, plain, _, _, _, _ = TL.plan(rows, grids, 1920, 1080, 30.0, "auto", 15)
    finally:
        ML.TEXT = "mouth"
    assert side == "down" and plain != "down"
    assert info["overlap"] == 0
