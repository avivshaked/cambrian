"""The landing's authoring tool (scripts/channel/landing_path.py): a synthetic B3, an end pose and a single capsule, with no
Blender, no theatre and no run.

  python -m pytest scripts/tests/landing-path/test_landing_path.py
  python -B scripts/tests/minirun.py scripts/tests/landing-path/test_landing_path.py   (where pytest is not installed)
"""
import dataclasses
import importlib.util
import json
import math
import sys
from pathlib import Path

import numpy as np
import pytest

CHANNEL = Path(__file__).resolve().parents[2] / "channel"
sys.path.insert(0, str(CHANNEL))
import landing_io  # noqa: E402

spec = importlib.util.spec_from_file_location("landing_path", CHANNEL / "landing_path.py")
lp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lp)

DT = 1 / 30
CUT = 248
J = 260
CENTRE = np.array([0.0, -5.0, 14.0])


def end_pose(t=43565.0):
    return lp.End(np.array([-7.0, -9.0, 10.5]), CENTRE.copy(), 60.0, t)


V0, K_DRAG = 2.9, 0.88


def write_b3(path, n=420, slow_from=None):
    """A straight stand-in for B3 in Blender coordinates along (0, 0.6, -0.8), at constant V0 before the cut and, from the
    cut, braking by the drag law dv/dt = -K v^2 as B3 does; it crosses the sea at about frame 200. With `slow_from` the sim
    clock runs at half speed from that frame on, as B3's does around its crossing."""
    d = np.array([0.0, 0.6, -0.8])
    frames = []
    for f in range(n):
        t = f * DT if slow_from is None or f < slow_from else slow_from * DT + (f - slow_from) * DT * 0.5
        tc = (f - CUT) / 30
        s_ = V0 * tc if tc < 0 else math.log(1 + K_DRAG * V0 * tc) / K_DRAG       # distance from the cut
        loc = np.array([0.0, 0.0, -3.71]) + d * s_
        frames.append({"f": f, "tf": f / 30, "t": t, "loc": loc.tolist(), "dir": d.tolist()})
    Path(path).write_text(json.dumps({"vfov_deg": 37.84928883210247, "frames": frames}), encoding="utf-8")
    return path


def ctx_for(tmp_path, coral=None, bodies=None, **limits):
    b3 = lp.B3(write_b3(tmp_path / "b3.json"), CUT)
    return lp.Ctx(b3=b3, end=end_pose(), centre=CENTRE, lim=lp.Limits(**limits), omega=math.radians(2), coral=coral, bodies=bodies)


def capsule(a, b, r=0.3, colony=22):
    return lp.Coral([{"kind": 0, "colony": colony, "ax": a[0], "ay": a[1], "az": a[2], "bx": b[0], "by": b[1], "bz": b[2],
                      "radius": r, "halfThickness": 0}])


def one_path(ctx, n_j=360, n_o=105, yaw=0.0, rho=4.0, alpha=-90.0, sign=1, taper=0.75):
    p = lp.build_paths(ctx, J, n_j, n_o, [math.radians(yaw)], [rho], [math.radians(alpha)], [sign], taper)
    assert p is not None and p["reach"][0]
    return lp.Params(J, n_j, n_o, yaw, rho, alpha, sign, taper), p


def test_the_pose_file_validates_and_carries_the_clock(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    rows, wms, eye, fwd, vfov = lp.make_rows(ctx, P, p)
    out = tmp_path / "x.poses.json"
    landing_io.write_poses(out, rows, CUT, wms, "test")
    doc = landing_io.read_poses(out)
    n_after = (J - CUT) + P.join_frames + P.orbit_frames
    assert len(doc["rows"]) == CUT + n_after + 1
    assert abs(doc["rows"][-1]["world_s"] - 43565.0) < 1e-6
    assert abs(doc["rows"][CUT]["world_s"] - (43565.0 - n_after / 30)) < 1e-6
    assert doc["rows"][CUT - 1]["world_s"] is None


def test_the_join_is_c2_at_j_and_at_the_orbit_start(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    rows, wms, eye, fwd, vfov = lp.make_rows(ctx, P, p)
    sim = np.array([r["sim_s"] for r in rows])
    j0, j1 = J, J + P.join_frames
    # at J: B3's frames end and the join begins; the acceleration read from three rows each side agrees, and so does the velocity
    a_before = lp.fd_accel(eye[j0 - 2:j0 + 1], sim[j0 - 2:j0 + 1])
    a_after = lp.fd_accel(eye[j0:j0 + 3], sim[j0:j0 + 3])
    assert np.linalg.norm(a_before) > 1.0                           # B3 is braking there: the test is not vacuous
    # the two stencils are centred two frames apart, so B3's own jerk (a few m/s^3) shows in the difference
    assert np.linalg.norm(a_after - a_before) < 0.6, (a_before, a_after)
    assert np.abs(a_after - p["A0"][0]).max() < 0.4
    vel_b = (eye[j0] - eye[j0 - 2]) / (2 * DT)
    vel_a = (eye[j0 + 2] - eye[j0]) / (2 * DT)
    assert np.linalg.norm(vel_a - vel_b) < 0.2 and np.abs(vel_a - p["V0"][0]).max() < 0.2
    # at the orbit start: velocity and the centripetal acceleration
    vel = (eye[j1 + 1] - eye[j1 - 1]) / (2 * DT)
    acc = (eye[j1 + 1] - 2 * eye[j1] + eye[j1 - 1]) / DT ** 2
    assert np.abs(vel - p["V1"][0]).max() < 5e-3, (vel, p["V1"][0])
    assert np.abs(acc - p["A1"][0]).max() < 0.05, (acc, p["A1"][0])
    # yaw and pitch carry their rates across J and across the orbit start
    yaw, pit = lp.yaw_pitch(fwd)
    yaw = np.unwrap(yaw)
    st = ctx.b3.state_at(J)
    assert abs((-3 * pit[j0] + 4 * pit[j0 + 1] - pit[j0 + 2]) / (2 * DT) - st["pitch_d"]) < 5e-3
    assert abs((yaw[j1 + 1] - yaw[j1 - 1]) / (2 * DT) - math.radians(2)) < 2e-3
    assert abs((pit[j1 + 1] - pit[j1 - 1]) / (2 * DT)) < 2e-3


def test_the_join_brakes_by_the_drag_law_tapers_to_a_glide_and_never_speeds_up(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    rows, wms, eye, fwd, vfov = lp.make_rows(ctx, P, p)
    prof = p["profile"]
    k_fit, rms, k_j, v0, a_t = ctx.b3.drag_fit(J)
    assert abs(k_j - K_DRAG) < 0.05 and abs(k_fit - K_DRAG) < 0.05          # the fit finds the law the stand-in follows
    sp = np.linalg.norm(np.diff(eye[J:J + P.join_frames + 1], axis=0), axis=1) * 30
    assert (np.diff(sp) <= 1e-2).all(), np.diff(sp).max()
    for t in (0.05, 0.1):                                                    # the drag law, while the taper has hardly begun
        i = int(round(t * 30))
        want = v0 / (1 + k_j * v0 * (i - 0.5) / 30)
        assert abs(sp[i - 1] - want) < 0.03 * want, (t, sp[i - 1], want)
    assert prof["taper"] == 0.75 and abs(prof["v"][-1] - p["v_o"]) < 1e-6
    # the deceleration eases to zero by the end of the taper: the acceleration along the path, from the rows, has no step
    acc = np.diff(sp) * 30
    i_t = int(0.75 * 30)
    assert abs(acc[0] + k_j * v0 ** 2) < 0.4 * k_j * v0 ** 2                 # starts at B3's braking
    assert abs(acc[i_t + 1]) < 0.15 and np.abs(np.diff(acc)).max() < 0.8     # nothing jumps by more than a fraction of a m/s^2 a frame
    # the distance the profile runs is the curve's length, so the join ends on the orbit's start
    assert abs(prof["S"] - p["curve_length"][0]) < 0.02 * prof["S"]
    assert np.allclose(eye[J + P.join_frames], p["P1"][0], atol=0.02)


def test_a_taper_that_leaves_the_glide_below_the_orbit_is_refused():
    assert lp.speed_profile(2.9, 0.88, 0.75, 0.3, 360) is not None
    assert lp.speed_profile(2.9, 0.88, 0.75, 1.5, 360) is None              # a glide of about 1.1 m/s is not above 1.5
    assert lp.speed_profile(2.9, 0.88, 11.5, 0.3, 360) is None              # no time left for the fall
    assert lp.speed_profile(2.9, 0.0, 0.75, 0.3, 360) is None               # B3 not braking


def test_the_lens_is_held_then_eases_and_finishes_three_seconds_before_the_end(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    rows, wms, eye, fwd, vfov = lp.make_rows(ctx, P, p)
    last = len(rows) - 1
    assert np.allclose(vfov[:J + 1], 37.84928883210247, atol=1e-12)           # B3's lens to J, no zoom at the join
    assert abs(vfov[J + 1] - vfov[J]) < 1e-3                                  # its rate is 0 at J
    assert np.all(np.diff(vfov[J:]) >= -1e-12)                               # one smooth curve, one way
    done = int(np.nonzero(np.abs(vfov - 60.0) < 1e-9)[0][0])
    assert (last - done) / 30 >= 3.0 - 1e-9
    assert abs(vfov[-1] - 60.0) < 1e-12
    # an orbit shorter than the lead is not searched at all
    final, rej, nsurv, near = lp.search(ctx, [J], [16.0], [2.0], [0.75], np.array([0.0]), np.array([4.0]), np.array([-90.0]), verbose=False)
    assert final == [] and nsurv == 0 and rej == {}


def test_the_orbit_never_eases_out(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    rows, wms, eye, fwd, vfov = lp.make_rows(ctx, P, p)
    k0 = J + P.join_frames
    sp = np.linalg.norm(np.diff(eye[k0:], axis=0), axis=1) * 30
    r = math.hypot(*(end_pose().eye[[0, 2]] - CENTRE[[0, 2]]))
    assert np.allclose(sp, math.radians(2) * r, atol=2e-3)                    # constant to the last frame
    assert sp[-1] > 0.2 and sp.max() < 0.485
    assert np.allclose(eye[-1], end_pose().eye, atol=1e-9)                    # the last frame is the end pose
    assert np.allclose(fwd[-1], end_pose().forward, atol=1e-9)


def test_a_capsule_on_the_path_is_a_clearance_failure(tmp_path):
    clear = ctx_for(tmp_path, coral=capsule([40, -3, 40], [40, -9, 40]))
    P, p = one_path(clear)
    assert lp.full_check(clear, P, p)["pass"]
    f = 330                                                                    # a frame in the middle of the join
    on = p["eye"][0][f - CUT]
    hit = ctx_for(tmp_path, coral=capsule(on + [0, -1, 0], on + [0, 1, 0]))
    res = lp.full_check(hit, P, p)
    assert not res["pass"] and res["coral_margin"] < 0 and res["coral_min"] < 0.6
    assert abs(res["coral_margin_f"] - f) <= 40


def test_a_piece_in_view_needs_a_metre_and_one_behind_the_camera_six_tenths(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    k = 330 - CUT
    eye, fwd = p["eye"][0][k], p["fwd"][0][k]
    point = lambda c: capsule(c, c, r=0.1)
    ahead = point(eye + fwd * 0.85)                   # 0.75 m from the eye's surface to the piece, straight ahead
    behind = point(eye - fwd * 0.85)
    one = slice(k, k + 1)                              # this frame alone: the camera moves on in the others
    lim = ctx.lim
    m_ahead, d_ahead, v_ahead, _ = ahead.clearance(p["eye"][0][one], p["fwd"][0][one], p["vfov"][one], lim.aspect, lim.coral_clear, lim.coral_view_clear)
    m_behind, d_behind, v_behind, _ = behind.clearance(p["eye"][0][one], p["fwd"][0][one], p["vfov"][one], lim.aspect, lim.coral_clear, lim.coral_view_clear)
    assert abs(d_ahead[0] - 0.75) < 1e-9 and abs(d_behind[0] - 0.75) < 1e-9
    assert m_ahead[0] < 0 and abs(v_ahead[0] - 0.75) < 1e-9                    # 0.75 m < 1 m, in view
    assert m_behind[0] > 0 and v_behind[0] == np.inf                           # 0.75 m > 0.6 m, out of view


def test_a_plate_is_a_swept_disc():
    plate = lp.Coral([{"kind": 1, "colony": 1, "ax": 0, "ay": -5, "az": 0, "bx": 0, "by": -5, "bz": 0, "radius": 2.0, "halfThickness": 0.125}])
    d = plate.closest(np.array([[1.0, -4.0, 0.0], [5.0, -5.0, 0.0], [0.0, -5.125, 0.0]]))[1][:, 0]
    assert np.allclose(d, [1.0 - 0.125, 3.0 - 0.125, 0.0], atol=1e-9)


def test_a_body_on_the_path_is_a_clearance_failure(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    f = 330 - CUT
    ws = end_pose().t - (p["eye"].shape[1] - 1 - f) * DT
    here = p["eye"][0][f] + [0.4, 0.0, 0.0]
    bodies = lp.Bodies([ws - 5, ws + 5], np.stack([here[None], here[None]]))
    res = lp.full_check(dataclasses.replace(ctx, bodies=bodies), P, p)
    assert not res["pass"] and res["body_margin"] < 0 and res["body_min"] < 0.5 + 1.0
    far = lp.Bodies([ws - 5, ws + 5], np.stack([[[500.0, -5.0, 500.0]], [[500.0, -5.0, 500.0]]]))
    assert lp.full_check(dataclasses.replace(ctx, bodies=far), P, p)["pass"]


def test_bodies_are_interpolated_between_rows():
    b = lp.Bodies([0.0, 10.0], np.array([[[0.0, 0.0, 0.0]], [[10.0, 0.0, 0.0]]]))
    assert np.allclose(b.at([2.5])[0, 0], [2.5, 0, 0])
    assert np.allclose(b.at([-5, 20])[:, 0, 0], [0.0, 10.0])


def test_b3_whose_clock_has_not_left_its_slow_motion_is_refused(tmp_path):
    path = write_b3(tmp_path / "slow.json", slow_from=250)
    with pytest.raises(ValueError):
        lp.B3(path, CUT)


def test_a_plan_outside_the_recorded_window_is_refused(tmp_path):
    ctx = ctx_for(tmp_path)
    P, p = one_path(ctx)
    rows, wms, eye, fwd, vfov = lp.make_rows(ctx, P, p)
    w0, w1 = lp.check_window(ctx, wms, rows)
    assert abs(w1 - 43565.0) < 1e-6 and abs(w0 - (rows[CUT]["world_s"] - 1.0)) < 1e-9
    for t in (43576.0, 43551.0):                      # the end after the window, and the cut's second before it
        other = dataclasses.replace(ctx, end=end_pose(t))
        rows2, wms2, *_ = lp.make_rows(other, P, p)
        with pytest.raises(ValueError):
            lp.check_window(other, wms2, rows2)


def test_the_rig_log_line_is_read():
    line = ("[Theatre] rig V1.coral22 u=1.00 t=42016.00 eye=(95.1653, -11.3147, 100.9859) at=(103.3509, -6.8090, 104.6303) lens=60.0 top=56.7 glow yes")
    end = lp.parse_rig_line("noise\n" + line + "\n", "V1.coral22")
    assert np.allclose(end.eye, [95.1653, -11.3147, 100.9859]) and np.allclose(end.at, [103.3509, -6.809, 104.6303])
    assert end.lens == 60.0 and end.t == 42016.0
    with pytest.raises(ValueError):
        lp.parse_rig_line(line, "V2.coral4")


def test_the_reef_entry_is_the_first_frame_from_which_the_crown_stays_in_frame(tmp_path):
    ctx = ctx_for(tmp_path)
    eye = np.array([[[0, -5, 14 - d] for d in (30, 25, 20, 8, 7, 8)]], float)     # straight at the crown, closing in
    fwd = np.tile(np.array([[[0.0, 0.0, 1.0]]]), (1, 6, 1))
    vis = lp.visible_crown(ctx, eye, fwd, np.full(6, 60.0))
    assert vis[0].tolist() == [False, False, False, True, True, True]
    assert lp.entry_index(vis)[0] == 3
    # it must also stay: a frame that loses it again moves the entry later
    assert lp.entry_index(np.array([[False, True, True, False, True, True]]))[0] == 4


def test_the_search_ranks_what_passes_and_reports_a_winner(tmp_path):
    ctx = ctx_for(tmp_path, coral=capsule([40, -3, 40], [40, -9, 40]), entry_min=0.0, entry_max=18.0, max_rate_dps=200.0)
    final, rej, nsurv, near = lp.search(ctx, [J], [16.0], [3.5], [0.5, 0.75, 1.0], np.arange(0, 360, 60.0), np.array([2.0, 4.0, 6.0]),
                                        np.arange(0, 360, 60.0), verbose=False)
    assert final, rej
    assert [r["score"] for r in final] == sorted((r["score"] for r in final), reverse=True)
    assert all(r["pass"] for r in final)
    assert isinstance(lp.why_won(final), str) and len(lp.candidate_table(final)) == min(10, len(final)) + 1
