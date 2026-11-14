"""Checks of mouth_label.py on synthetic tracks.

    python -I -B scripts/tests/minirun.py scripts/channel/test_mouth_label.py
"""
import json
import subprocess
import sys
from pathlib import Path

import numpy as np

CHANNEL = Path(__file__).resolve().parent
sys.path.insert(0, str(CHANNEL))
import mouth_label as ML      # noqa: E402
import pytest                 # noqa: E402

W, H = 1920, 1080


def rows_at(pts, vis=None):
    return [{"frame": i, "x": x, "y": y, "depth": 3.0, "visible": True if vis is None else vis[i]}
            for i, (x, y) in enumerate(pts)]


def test_track_parsing_dict_and_list(tmp_path):
    rows = [{"frame": 0, "x": 0.5, "y": 0.4, "depth": 2.0, "visible": True},
            {"frame": 1, "x": 0.6, "y": 0.4, "depth": 2.1, "visible": False}]
    p = tmp_path / "a.json"
    p.write_text(json.dumps({"shot": "S2", "fps": 30, "rows": rows}))
    header, got = ML.load_track(str(p))
    assert header == {"shot": "S2", "fps": 30}
    assert [r["visible"] for r in got] == [True, False] and got[1]["x"] == 0.6
    p.write_text(json.dumps(rows))
    header, got = ML.load_track(str(p))
    assert header == {} and len(got) == 2


def test_track_without_rows_is_refused(tmp_path):
    p = tmp_path / "b.json"
    p.write_text(json.dumps({"shot": "S2"}))
    with pytest.raises(ValueError):
        ML.load_track(str(p))


def test_count_mismatch_says_both_counts():
    with pytest.raises(ValueError) as e:
        ML.check_counts(rows_at([(0.5, 0.5)] * 89), 90)
    assert "89" in str(e.value) and "90" in str(e.value)
    ML.check_counts(rows_at([(0.5, 0.5)] * 90), 90)


def test_side_choice_follows_room():
    look = ML.Look(W, H)
    left_edge = rows_at([(0.10, 0.5)] * 10)
    assert ML.choose_side(left_edge, 0, look) == "right"
    right_edge = rows_at([(0.90, 0.5)] * 10)
    assert ML.choose_side(right_edge, 0, look) == "left"
    # near the right edge, mid height, and the left is far: left wins; a disc at the bottom-left corner: up or right
    corner = rows_at([(0.08, 0.92)] * 10)
    assert ML.choose_side(corner, 0, look) in ("right", "up")
    # only the frames from the label's start count
    drift = rows_at([(0.9, 0.5)] * 5 + [(0.1, 0.5)] * 5)
    assert ML.choose_side(drift, 5, look) == "right"
    assert ML.choose_side(drift, 0, look) in ("left", "right", "up", "down")


def test_smoothing_is_one_pole_of_quarter_second():
    n = 120
    targets = np.array([[0.0, 0.0]] * 10 + [[100.0, 0.0]] * (n - 10))
    out = ML.smooth(targets, [True] * n, 30.0)
    assert out[9][0] == 0.0
    # after 0.25 s (7.5 frames) the step has covered 1 - 1/e, within a frame's discretisation
    assert 0.55 < out[17][0] / 100.0 < 0.70
    assert out[-1][0] > 99.0
    assert np.all(np.diff(out[:, 0]) >= 0)            # monotone, no overshoot
    # an invisible frame holds the last target
    t2 = np.array([[0.0, 0.0], [10.0, 0.0], [999.0, 0.0], [999.0, 0.0]])
    o2 = ML.smooth(t2, [True, True, False, False], 30.0)
    assert o2[3][0] < 10.0


def test_smoothing_removes_jitter():
    rng = np.random.default_rng(1)
    t = np.stack([500 + rng.normal(0, 3, 200), np.full(200, 300.0)], 1)
    out = ML.smooth(t, [True] * 200, 30.0)
    assert np.std(np.diff(out[:, 0])) < 0.5 * np.std(np.diff(t[:, 0]))


def test_word_stays_inside_safe_margin():
    n = 90
    # discs that run into every edge and corner
    for pts in ([(0.01, 0.5)] * n, [(0.99, 0.5)] * n, [(0.5, 0.01)] * n, [(0.5, 0.99)] * n,
                [(0.0, 0.0)] * n, [(1.0, 1.0)] * n,
                [(0.02 + 0.96 * i / (n - 1), 0.02 + 0.96 * (1 - i / (n - 1))) for i in range(n)]):
        for side in ("auto", "left", "right", "up", "down"):
            look, used, disc, cen, ops = ML.plan(rows_at(pts), W, H, 30.0, side, 20)
            assert used in ML.SIDES
            assert np.all(cen[:, 0] - look.hw >= look.mx - 1e-6)
            assert np.all(cen[:, 0] + look.hw <= W - look.mx + 1e-6)
            assert np.all(cen[:, 1] - look.hh >= look.my - 1e-6)
            assert np.all(cen[:, 1] + look.hh <= H - look.my + 1e-6)


def test_word_edge_sits_230px_on_the_named_side():
    look, used, disc, cen, ops = ML.plan(rows_at([(0.5, 0.5)] * 200), W, H, 30.0, "left", 0)
    assert used == "left"
    assert abs((cen[-1][0] + look.hw) - (960 - 230)) < 0.5 and abs(cen[-1][1] - 540) < 0.5


def test_luma_side_choice_prefers_dark_and_skips_unsafe_sides():
    n = 10
    look = ML.Look(W, H)
    rows = rows_at([(0.5, 0.5)] * n)
    luma = np.full((n, H, W), 0.5, np.float32)
    luma[:, :, :960] = 0.9           # bright left half, so right is darker
    sc = ML.side_scores(rows, 0, look, luma)
    assert ML.pick_by_luma(sc) == "right" and sc["left"] > sc["right"]
    luma[:] = 0.5
    luma[:, 540 + 100:, :] = 0.05    # dark below: down wins
    sc = ML.side_scores(rows, 0, look, luma)
    assert ML.pick_by_luma(sc) == "down"
    # a disc near the right edge: right does not keep the word in the margin, so it is not scored
    rows = rows_at([(0.9, 0.5)] * n)
    luma = np.full((n, H, W), 0.5, np.float32)
    luma[:, :, 1700:] = 0.0
    sc = ML.side_scores(rows, 0, look, luma)
    assert "right" not in sc and ML.pick_by_luma(sc) != "right"


def test_lane_track_format(tmp_path):
    doc = {"header": {"shot": "S2", "f": 0.85},
           "rows": [{"frame": 0, "screen_frac": [0.3, 0.8], "depth_m": 2.5, "visible": True, "facing_cos": 0.9},
                    {"frame": 1, "screen_frac": [0.31, 0.8], "depth_m": 2.5, "visible": False}]}
    p = tmp_path / "l.json"
    p.write_text(json.dumps(doc))
    header, rows = ML.load_track(str(p))
    assert header["shot"] == "S2" and rows[0]["x"] == 0.3 and rows[1]["y"] == 0.8
    assert rows[0]["depth"] == 2.5 and rows[1]["visible"] is False


def test_leader_stops_short_of_the_disc():
    look = ML.Look(W, H)
    (ax, ay), (bx, by) = ML.leader_points(look, 700.0, 540.0, 960.0, 540.0)
    assert abs(bx - (960 - look.gap)) < 1e-6 and by == 540.0
    assert ax == 700.0 + look.hw
    assert ML.leader_points(look, 950.0, 540.0, 960.0, 540.0) is None


def test_opacity_fade_in_and_visibility():
    vis = [True] * 60 + [False] * 30 + [True] * 30
    ops = ML.opacities(vis, 30.0, 15)
    assert ops[15] == 0.0 and ops[14] == 0.0
    assert ops[27] == 1.0                                        # 0.4 s = 12 frames
    assert 0 < ops[20] < 1.0
    assert ops[59] == 1.0
    assert ops[66] < 1e-9                                        # gone within 0.2 s = 6 frames
    assert ops[89] < 1e-9
    assert 0 < ops[93] < 1.0 and ops[119] == 1.0                 # back in over 0.2 s, then steady (no flicker)
    assert all(ops[i] == 1.0 for i in range(96, 120))


def test_draw_label_marks_pixels_and_leaves_disc():
    look = ML.Look(640, 360)
    f = np.zeros((360, 640, 3), np.float32)
    ML.draw_label(f, look, 400.3, 100.7, 300.0, 100.0, 1.0)
    assert f.max() > 0.8
    # the pixel at the disc centre is untouched, and so is its 10 px (scaled) neighbourhood on the leader side
    assert f[100, 300].max() < 0.2
    z = np.zeros((360, 640, 3), np.float32)
    ML.draw_label(z, look, 400.3, 100.7, 300.0, 100.0, 0.0)
    assert z.max() == 0.0


def test_end_to_end_small_clip(tmp_path):
    clip = tmp_path / "c.mp4"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=0x103040:s=320x180:r=30:d=1",
                    "-f", "lavfi", "-i", "sine=f=440:d=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac",
                    "-shortest", str(clip)], check=True)
    n = ML.probe(str(clip))[4]
    trk = tmp_path / "t.json"
    trk.write_text(json.dumps({"shot": "x", "rows": rows_at([(0.3 + 0.002 * i, 0.5) for i in range(n)])}))
    out = tmp_path / "o.mp4"
    ML.render(str(clip), str(trk), str(out), sheet=str(tmp_path / "s.png"))
    w, h, rate, fps, n2 = ML.probe(str(out))
    assert (w, h, n2) == (320, 180, n)
    assert (tmp_path / "s.png").stat().st_size > 0
    streams = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=codec_type", "-of", "csv=p=0", str(out)],
                             capture_output=True, text=True).stdout.split()
    assert "audio" in streams
    bad = tmp_path / "bad.json"
    bad.write_text(json.dumps(rows_at([(0.3, 0.5)] * (n - 1))))
    with pytest.raises(ValueError):
        ML.render(str(clip), str(bad), str(tmp_path / "o2.mp4"))
