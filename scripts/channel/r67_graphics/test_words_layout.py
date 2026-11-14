"""Checks of g_words.py and g_layout.py (numpy and Pillow only, no Blender).

    python -I -B scripts/tests/minirun.py scripts/channel/r67_graphics/test_words_layout.py
"""
import importlib.util
import json
from pathlib import Path

import numpy as np

GFX = Path(__file__).resolve().parent


def load(name):
    spec = importlib.util.spec_from_file_location(name, GFX / (name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


W = load("g_words")
LY = load("g_layout")

WORD = {"text": "eaten", "anchor": "a", "face": "mono-semibold", "size_px": 44, "tracking_px": 4, "color": "#e6fbf6",
        "glow": "#9ff5e6", "glow_alpha": 0.45, "opacity": [[0.0, 0.55], [1.0, 0.55], [2.0, 1.0]]}


def test_opacity_keys_hold_and_interpolate():
    k = WORD["opacity"]
    assert W.opacity_at(k, -1) == 0.55 and W.opacity_at(k, 1.5) == 0.775 and W.opacity_at(k, 9) == 1.0


def centroid(img):
    lum = img.mean(axis=2)
    ys, xs = np.mgrid[0:lum.shape[0], 0:lum.shape[1]]
    return (lum * xs).sum() / lum.sum(), (lum * ys).sum() / lum.sum()


def test_a_word_sits_at_its_anchor_and_moves_in_sub_pixels():
    bg = np.zeros((540, 960, 3), np.float32)
    a = W.draw_words(bg, {"e": WORD}, {"a": [480.0, 300.0, 0.0]}, 5.0)
    b = W.draw_words(bg, {"e": WORD}, {"a": [480.6, 300.0, 0.0]}, 5.0)
    ca, cb = centroid(a), centroid(b)
    assert abs(ca[0] - 240.0) < 3 and abs(ca[1] - (150.0 - 7)) < 8  # the frame is half the design's size
    assert 0.1 < (cb[0] - ca[0]) < 0.5                              # 0.6 design px = 0.3 frame px, resolved


def test_depth_blur_softens_the_edges():
    bg = np.zeros((540, 960, 3), np.float32)
    sharp = W.draw_words(bg, {"e": WORD}, {"a": [480.0, 300.0, 0.0]}, 5.0)
    soft = W.draw_words(bg, {"e": WORD}, {"a": [480.0, 300.0, 14.0]}, 5.0)
    assert soft.max() < sharp.max() and abs(soft.sum() - sharp.sum()) / sharp.sum() < 0.1


def test_a_word_at_zero_opacity_changes_nothing():
    bg = np.full((540, 960, 3), 0.2, np.float32)
    w = dict(WORD, opacity=[[0.0, 0.0], [9.0, 0.0]])
    assert np.array_equal(W.draw_words(bg, {"e": w}, {"a": [480.0, 300.0, 0.0]}, 1.0), bg)


def test_the_camera_round_trips_a_pixel():
    cam = LY.Camera(85.0, (0.0, -7.65, 0.0), (3374.0, -1110.0))
    p = cam.to_world(1000, 600, 7.85)
    px, py = cam.to_pixel(p)
    assert abs(px - 1000) < 1e-6 and abs(py - 600) < 1e-6
    assert abs(p[1] - (-7.65 + 7.85)) < 1e-9


def test_a_perspective_box_shrinks_its_back_face_about_the_principal_point():
    cam = LY.Camera(85.0, (0.0, -7.65, 0.0), (3374.0, -1110.0))
    front = cam.to_world(660, 330, 7.65)
    back = (front[0], front[1] + 0.3, front[2])
    bx, by = cam.to_pixel(back)
    s = 7.65 / 7.95
    assert abs(bx - (3374 + s * (660 - 3374))) < 1e-6 and abs(by - (-1110 + s * (330 + 1110))) < 1e-6


def test_the_blur_is_zero_at_focus_and_grows_with_distance_from_it():
    cam = LY.Camera(85.0, (0, 0, 0), fstop=2.2, focus=7.85)
    assert cam.coc_px(7.85) < 1e-9
    assert 0 < cam.coc_px(9.0) < cam.coc_px(14.0) < cam.coc_px(20.0)


def test_compare_flags_a_miss_and_honours_only_at():
    lay = {"tolerance_px": 19, "anchors": {"a": [100, 100], "level": [0, 0]}, "only_at": {"level": [9.0]}}
    rows, ok = LY.compare({"a": (110, 100), "level": (500, 500)}, lay, at=3.0)
    assert ok and len(rows) == 1
    rows, ok = LY.compare({"a": (110, 100), "level": (500, 500)}, lay, at=9.0)
    assert not ok


def test_the_lcg_is_water_js():
    r = LY.lcg(11)
    first = r()
    assert 0 <= first < 1 and abs(first - ((11 * 1664525 + 1013904223) % 4294967296) / 4294967296.0) < 1e-12


def test_every_layout_file_is_well_formed():
    for p in (GFX / "layout").glob("*.json"):
        d = json.load(open(p))
        assert d["anchors"] and d["tolerance_px"] == 19 and d["check_s"]
        assert all(len(v) == 2 for v in d["anchors"].values())
