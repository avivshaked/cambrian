"""Pure-Python checks of map_bubbles.py (the fit) and move_cut.py, on synthetic data.

    python -B scripts/tests/minirun.py scripts/channel/tests/test_map_bubbles_move_cut.py
"""
import json
import math
import sys
from pathlib import Path

import numpy as np

CHANNEL = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(CHANNEL))
import landing_io as LIO      # noqa: E402
import map_bubbles as MB      # noqa: E402
import move_cut as MC         # noqa: E402
import pytest                 # noqa: E402

THETA = math.radians(-172.5)
T = np.array([85.0, 0.25, 119.5])


def b3_path(n=60):
    f = np.arange(n)
    return np.stack([0.1 * f, 0.05 * f * np.cos(f / 7.0) + 1.0, 10.0 - 0.15 * f], axis=1)      # Blender axes


def pose_doc(eyes, cut, wms=100.0, slow=False):
    rows = []
    fwd = np.array([0.0, 0.0, 1.0])
    up = np.array([0.0, 1.0, 0.0])
    for i, e in enumerate(eyes):
        s = i / 30.0 if not slow else (i / 30.0) ** 2 / 30.0
        rows.append({"f": i, "film_s": i / 30.0, "sim_s": s, "world_s": None, "eye": list(map(float, e)),
                     "forward": fwd.tolist(), "up": up.tolist(), "vfov": 37.85})
    doc = {"format": LIO.POSES_FORMAT, "fps": 30, "cut_frame": cut, "world_minus_sim": wms, "source": "synthetic", "rows": rows}
    return MC.move_cut(doc, cut)[0] if slow else _with_world(doc, cut, wms)


def _with_world(doc, cut, wms):
    for r in doc["rows"][cut:]:
        r["world_s"] = r["sim_s"] + wms
    return doc


def camera_of(path):
    return {"frames": [{"f": i, "loc": p.tolist()} for i, p in enumerate(path)]}


def test_fit_recovers_a_known_turn_and_translation():
    path = b3_path()
    P = LIO.to_unity(path)
    Q = P @ MB.rot_y(THETA).T + T
    theta, t, res = MB.fit_turn_translation(P, Q)
    assert abs(theta - THETA) < 1e-9
    assert np.allclose(t, T, atol=1e-9)
    assert res.max() < 1e-9


def test_rot_y_is_proper_and_turns_x_towards_minus_z_for_positive_angle():
    R = MB.rot_y(0.3)
    assert abs(np.linalg.det(R) - 1.0) < 1e-12
    assert np.allclose(R @ R.T, np.eye(3))
    assert np.allclose(MB.rot_y(math.pi / 2) @ [1, 0, 0], [0, 0, 1], atol=1e-12)


def test_paired_points_stop_at_the_cut_and_need_both_files():
    path = b3_path(60)
    eyes = LIO.to_unity(path) @ MB.rot_y(THETA).T + T
    poses = pose_doc(eyes, 40)
    P, Q, fs = MB.paired_points(camera_of(path[:50]), poses)
    assert fs == list(range(40)) and len(P) == 40
    assert np.allclose(Q, eyes[:40])


def test_main_maps_and_refuses_a_bad_fit(tmp_path):
    path = b3_path()
    eyes = LIO.to_unity(path) @ MB.rot_y(THETA).T + T
    poses = pose_doc(eyes, 40, wms=100.0)
    cam_p, pose_p, bub_p, out_p = (tmp_path / n for n in ("cam.json", "poses.json", "b.json", "out.json"))
    cam_p.write_text(json.dumps(camera_of(path)))
    pose_p.write_text(json.dumps(poses))
    x0 = np.array([[1.0, -2.0, 3.0], [0.0, -1.0, 0.5]])
    v0 = np.array([[1.0, 0.0, 0.0], [0.0, -2.0, 1.0]])
    LIO.write_bubbles(str(bub_p), x0, v0, [1.0, 2.0], [0.01, 0.02], [0.2, 0.3], [0.5, 0.6], [1.0, 0.9], 100.0, "synthetic")
    argv = ["--bubbles", str(bub_p), "--camera", str(cam_p), "--poses", str(pose_p), "--out", str(out_p)]
    assert MB.main(argv) == 0
    out = LIO.read_bubbles(str(out_p))
    R = MB.rot_y(THETA)
    assert np.allclose(out["x0"], x0 @ R.T + T, atol=1e-9)
    assert np.allclose(out["v0"], v0 @ R.T, atol=1e-9)           # the turn alone
    assert out["ts"] == [1.0, 2.0] and out["flat"] == [1.0, 0.9] and out["world_minus_sim"] == 100.0
    # a world_minus_sim that differs is refused
    LIO.write_bubbles(str(bub_p), x0, v0, [1.0, 2.0], [0.01, 0.02], [0.2, 0.3], [0.5, 0.6], [1.0, 0.9], 100.5, "synthetic")
    assert MB.main(argv) == 1
    # a pose path that is not B3's (one eye moved 5 mm) is refused, and nothing is written
    bad = [dict(r) for r in poses["rows"]]
    bad[10] = dict(bad[10], eye=[bad[10]["eye"][0] + 0.005, bad[10]["eye"][1], bad[10]["eye"][2]])
    pose_p.write_text(json.dumps(dict(poses, rows=bad)))
    out_p.unlink()
    assert MB.main(argv) == 1
    assert not out_p.exists()


def test_fit_only_writes_nothing(tmp_path):
    path = b3_path()
    eyes = LIO.to_unity(path) @ MB.rot_y(THETA).T + T
    cam_p, pose_p = tmp_path / "cam.json", tmp_path / "poses.json"
    cam_p.write_text(json.dumps(camera_of(path)))
    pose_p.write_text(json.dumps(pose_doc(eyes, 40)))
    assert MB.main(["--fit-only", "--bubbles", "none", "--camera", str(cam_p), "--poses", str(pose_p)]) == 0


def test_move_cut_sets_world_s_on_the_slow_clock():
    eyes = LIO.to_unity(b3_path(60))
    doc = pose_doc(eyes, 50, wms=100.0, slow=True)             # sim_s is slow early: steps well under 1/30
    out, span, lo, hi = MC.move_cut(doc, 20)
    assert out["cut_frame"] == 20
    assert all(r["world_s"] is None for r in out["rows"][:20])
    for r in out["rows"][20:]:
        assert abs(r["world_s"] - (r["sim_s"] + 100.0)) < 1e-12
    assert lo < 1 / 30 / 4 and hi <= 1 / 30 + 1e-9
    assert span == (out["rows"][20]["world_s"], out["rows"][59]["world_s"])
    assert out["source"].endswith("cut moved to 20 by move_cut.py")
    assert out["rows"][0]["eye"] == doc["rows"][0]["eye"] and out["world_minus_sim"] == 100.0
    assert doc["cut_frame"] == 50                                # the input is not changed


def test_move_cut_refuses_a_cut_out_of_range():
    doc = pose_doc(LIO.to_unity(b3_path(30)), 20)
    for bad in (0, -3, 30, 99):
        with pytest.raises(ValueError):
            MC.move_cut(doc, bad)
    MC.move_cut(doc, 29)
    MC.move_cut(doc, 1)
