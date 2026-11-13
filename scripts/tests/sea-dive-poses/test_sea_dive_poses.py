"""Pure-Python checks of sea_dive_poses.py (the part of sea_dive.py's --pose-file and --bubble-export that needs no bpy).

    python -B scripts/tests/minirun.py scripts/tests/sea-dive-poses/test_sea_dive_poses.py
"""
import math
import sys
from pathlib import Path

import numpy as np

CHANNEL = Path(__file__).resolve().parents[2] / "channel"
sys.path.insert(0, str(CHANNEL))
import landing_io as LIO          # noqa: E402
import sea_dive_poses as PZ       # noqa: E402


def line_rows(n=40, speed=3.0, wms=100.0):
    """A straight line in Unity axes at `speed` m/s, 30 fps, sim_s = film_s - 1, forward (1, -0.5, 0.3) normalised."""
    fwd = np.array([1.0, -0.5, 0.3])
    fwd /= np.linalg.norm(fwd)
    d = np.array([0.6, -0.3, 0.74])
    d = d / np.linalg.norm(d)
    rows = []
    for i in range(n):
        s = i / 30.0 - 1.0
        eye = np.array([2.0, 1.5, -4.0]) + speed * s * d
        up = LIO.up_for(fwd)
        rows.append({"f": i, "film_s": i / 30.0, "sim_s": s, "world_s": None if i < 10 else s + wms,
                     "eye": eye.tolist(), "forward": fwd.tolist(), "up": up.tolist(), "vfov": 37.85 + 0.1 * i})
    return rows, d, fwd


def test_rows_to_states_straight_line():
    rows, d, fwd = line_rows()
    st = PZ.rows_to_states(rows)
    vb = LIO.to_blender(3.0 * d)
    assert np.allclose(st["vel"][5], vb, atol=1e-9)            # central difference of a line is exact
    assert np.allclose(st["vel"][0], vb, atol=1e-9)            # and so is the one-sided end
    assert abs(st["speed"][7] - 3.0) < 1e-9
    fb = LIO.to_blender(fwd)
    assert np.allclose(st["dir"][3], fb)
    assert abs(st["pitch"][3] - math.degrees(math.asin(fwd[1]))) < 1e-9   # Blender z is Unity y
    assert np.allclose(st["t"], np.arange(40) / 30.0 - 1.0)
    assert np.allclose(st["tf"], np.arange(40) / 30.0)
    assert abs(math.degrees(st["vfov"][4]) - 38.25) < 1e-9
    assert np.allclose(st["loc"][0], LIO.to_blender(rows[0]["eye"]))


def test_central_difference_follows_sim_clock_not_film_clock():
    rows, _, _ = line_rows(n=5)
    for r, s in zip(rows, (0.0, 0.01, 0.02, 0.5, 1.0)):          # a slow-motion stretch, then a jump
        r["sim_s"] = s
        r["eye"] = [s * 2.0, 0.0, 0.0]
    for r in rows:
        r["world_s"] = None
    st = PZ.rows_to_states(rows)
    assert np.allclose(st["vel"][:, 0], 2.0)                      # a line at 2 m/s on the sim clock, whatever the spacing


def test_flat_crossing_and_find_crossing():
    # descend through z = 0 at a known place: lens at height 1.0 at t = 0, falling 2 m/s, looking 30 degrees down
    n = 20
    t = np.arange(n) / 30.0
    loc = np.stack([0.5 * t, 0.1 * t, 1.0 - 2.0 * t], -1)
    d = np.tile(np.array([math.cos(math.radians(30)), 0.0, -math.sin(math.radians(30))]), (n, 1))
    st = {"t": t, "tf": t, "loc": loc, "dir": d, "vel": np.tile([0.5, 0.1, -2.0], (n, 1)), "f": np.arange(n),
          "pitch": np.full(n, -30.0), "vfov": np.full(n, 0.6), "speed": np.full(n, math.hypot(2.0, 0.51))}
    port = 0.12
    # the port's centre is at lens.z + port * d.z = 1 - 2t - 0.06: it meets z = 0 at t = 0.47
    i, c = PZ.flat_crossing(st, port)
    assert abs(c[2] - 0.06) < 1e-9 and i == 15      # the lens is 0.06 m up when the port's centre is at 0
    e = PZ.find_crossing(st, port, lambda t_, x, y: 0.0)
    assert abs(e["t"] - 0.47) < 1e-6, e["t"]
    assert e["frame"] == int(round(0.47 * 30))
    assert abs(e["v"] - math.sqrt(0.5 ** 2 + 0.1 ** 2 + 2.0 ** 2)) < 1e-9
    assert abs(e["beta"] - math.atan2(2.0, math.hypot(0.5, 0.1))) < 1e-9
    assert abs(np.linalg.norm(e["dir"]) - 1) < 1e-12
    # a surface that stands 0.2 m up meets the camera sooner, by 0.2 / 2 = 0.1 s
    e2 = PZ.find_crossing(st, port, lambda t_, x, y: 0.2)
    assert abs(e2["t"] - 0.37) < 1e-6
    # never crossing, or already under
    for h, msg in ((lambda t_, x, y: -50.0, "never"), (lambda t_, x, y: 50.0, "starts")):
        try:
            PZ.find_crossing(st, port, h)
        except ValueError as ex:
            assert msg in str(ex)
        else:
            raise AssertionError("expected ValueError")


def test_place_and_unplace_round_trip_and_sun_frame():
    rows, _, _ = line_rows()
    st = PZ.rows_to_states(rows)
    sun_b = PZ.theatre_sun_blender()
    psi = math.radians(77.0) - PZ.sun_bearing(sun_b)             # the scene's sun at 77 deg, say
    centre = (3.0, -2.0)
    pl = PZ.place(st, centre, psi)
    assert np.allclose(PZ.unplace_points(pl["loc"], centre, psi), st["loc"])
    assert np.allclose(PZ.unplace_vectors(pl["dir"], psi), st["dir"])
    assert np.allclose(pl["loc"][:, 2], st["loc"][:, 2])         # the vertical is untouched
    turned = PZ.rot_z(psi) @ sun_b
    assert abs(PZ.sun_bearing(turned) - math.radians(77.0)) < 1e-12
    assert abs(turned[2] - sun_b[2]) < 1e-12                     # elevation kept


def test_theatre_sun_direction():
    # TheatreLook.FixedSun(): flat = cos 67, (flat cos 44, sin 67, flat sin 44) in Unity axes
    u = PZ.theatre_sun_unity()
    flat = math.cos(math.radians(67))
    assert np.allclose(u, [flat * math.cos(math.radians(44)), math.sin(math.radians(67)), flat * math.sin(math.radians(44))])
    b = PZ.theatre_sun_blender()
    # arithmetic: unity (0.28107, 0.92050, 0.27142) -> blender (x, z, y) = (0.28107, 0.27142, 0.92050)
    assert np.allclose(b, [0.28107, 0.27142, 0.92050], atol=1e-5), b.tolist()
    assert abs(math.degrees(PZ.sun_bearing(b)) - 44.0) < 1e-9     # the swap relabels y and z; the bearing is atan2(unity z, unity x)
    assert abs(math.degrees(math.asin(b[2])) - 67.0) < 1e-9
    assert abs(np.linalg.norm(b) - 1) < 1e-12


# ---- the bubble export against Bubbles.positions

def bubbles_positions_blender(x0, v0, ts, rise, tau, r, t, surf=0.0):
    """sea_dive.py's Bubbles.positions, lines 1563 to 1574 of the file before this change (the class's `self.` dropped; the
    sea's height `surf` a constant). tau is (n, 1) as the class holds it."""
    n = len(x0)
    tau_ = t - ts                                               # tau = t - self.ts
    live = tau_ >= 0
    tau_ = np.maximum(tau_, 0.0)[:, None]
    w = np.zeros((n, 3))
    w[:, 2] = rise
    e = np.exp(-tau_ / tau)
    X = x0 + w * tau_ + (v0 - w) * tau * (1 - e)
    live &= X[:, 2] < surf - r
    return X, live


def test_bubble_export_matches_bubbles_positions(tmp_path):
    rng = np.random.default_rng(5)
    n = 300
    x0 = np.stack([rng.uniform(-1, 1, n), rng.uniform(-1, 1, n), -rng.uniform(0.03, 0.5, n)], -1)   # Blender, sea frame
    v0 = rng.normal(0, 3.0, (n, 3))
    ts = rng.uniform(-0.2, 0.3, n)
    r = np.exp(rng.uniform(math.log(4e-4), math.log(2e-2), n))
    rise = np.minimum(0.25, 0.136 * (r / 2.5e-4) ** 2)
    tau = np.where(rng.random(n) < 0.5, 0.22, 0.16)[:, None]
    flat = np.where(r > 2e-3, 0.75, 1.0)
    centre, psi, wms = (4.5, -7.25), math.radians(-33.0), 43547.123
    ux0, uv0 = PZ.bubble_export_arrays(x0, v0, centre, psi)
    path = tmp_path / "t.bubbles.json"
    LIO.write_bubbles(path, ux0, uv0, ts, r, rise, tau, flat, wms, "test")
    doc = LIO.read_bubbles(path)
    assert doc["count"] == n and len(doc["tau"]) == n and doc["world_minus_sim"] == wms
    for sim in (-0.1, 0.0, 0.25, 0.7, 1.6):
        Xb, live_b = bubbles_positions_blender(x0, v0, ts, rise, tau, r, sim)
        Xu, live_u = LIO.bubble_positions(doc, sim + wms)
        # the export's world is the pose frame, in Unity axes; the sea frame's points go there by unplace then the swap
        want = LIO.to_unity(PZ.unplace_points(Xb, centre, psi))
        assert np.allclose(Xu, want, atol=1e-9), sim
        assert (live_u == live_b).all(), sim
    # the axes are swapped, as the brief says: a bubble rising in Blender z rises in Unity y
    assert np.allclose(np.asarray(doc["x0"])[:, 1], x0[:, 2])
    assert np.allclose(np.asarray(doc["v0"])[:, 1], v0[:, 2])
