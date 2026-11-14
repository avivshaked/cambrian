import json, math, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_skin as K

BODIES = json.load(open(os.path.join(HERE, "bodies-r67.json"), encoding="utf-8"))
P = np.array([[1.0, 0, 0], [0, 0, 1.0], [0, 1.0, 0]])


def parts_of(key):
    return BODIES[key]["parts"]


def test_k_is_the_films_rule():
    # the founder: smallest half extents 0.15 and 0.08, median 0.115, half of it 5.75 cm, capped at 3 cm
    assert abs(K.skin_k(parts_of("founder")) - 0.03) < 1e-12
    small = [{"half": [0.02, 0.05, 0.04]}, {"half": [0.03, 0.1, 0.2]}, {"half": [0.01, 0.4, 0.4]}]
    assert abs(K.skin_k(small) - 0.5 * 0.02) < 1e-12


def test_one_element_per_part_and_one_ball_per_joint():
    for key, b in BODIES.items():
        sk = K.plan(b["parts"])
        parts = [e for e in sk["elements"] if e["kind"] == "part"]
        balls = [e for e in sk["elements"] if e["kind"] == "ball"]
        assert len(parts) == len(b["parts"]), key
        assert len(balls) == sum(1 for p in b["parts"] if p["parent"] >= 0), key
        assert len({e["label"] for e in sk["elements"]}) == len(sk["elements"])


def test_a_ball_takes_the_films_radius():
    parts = parts_of("founder")
    sk = K.plan(parts)
    ball = next(e for e in sk["elements"] if e["kind"] == "ball")
    want = min(sk["k"], 0.5 * min(0.08, 0.15))
    assert abs(ball["half"][0] - want) < 1e-12 and ball["co"] == (0.0, 0.0, 0.0)


def test_every_element_lands_its_surface_on_its_drawn_size():
    # the isolated surface of a Blender ellipsoid element is rho x size x sqrt(1 - (t/s)^(1/3)); it must be the half extent
    for key, b in BODIES.items():
        sk = K.plan(b["parts"])
        t = sk["threshold"]
        for e in sk["elements"]:
            assert 0 < e["stiff"] <= 8.0 + 1e-9, (key, e["label"], e["stiff"])
            c = math.sqrt(1 - (t / e["stiff"]) ** (1 / 3))
            assert abs(e["rho"] * c - 1.0) < 1e-9, (key, e["label"])
            # and the field reaches k beyond the surface along the thinnest axis
            a = min(e["half"])
            assert abs(a * (e["rho"] - 1) - sk["k"]) < 1e-9


def test_a_part_is_inset_by_a_quarter_of_k():
    parts = parts_of("founder")
    sk = K.plan(parts)
    e = next(e for e in sk["elements"] if e["label"] == "p1")
    h = parts[1]["half"]
    inset = 0.25 * sk["k"]
    assert np.allclose(e["half"], (h[0] - inset, h[2] - inset, h[1] - inset))      # Core (x, y, z) -> Blender (x, z, y)


def test_rotations_are_the_cores_in_blender_axes():
    for key in ("founder", "guz-10", "tp-131141"):
        parts = parts_of(key)
        sk = K.plan(parts)
        for p in parts:
            e = next(e for e in sk["elements"] if e["label"] == "p%d" % p["i"])
            R = K.quat_matrix_wxyz(e["rot"])
            assert np.allclose(R, P @ K.rot_matrix(p["rot"]) @ P, atol=1e-9)
            assert abs(np.linalg.det(R) - 1) < 1e-9
            # a part's local direction maps to the same world point whichever frame is used
            v = np.array([0.3, -0.7, 0.2])
            assert np.allclose(P @ (K.rot_matrix(p["rot"]) @ v), R @ (P @ v), atol=1e-9)


def test_quaternion_roundtrip():
    rng = np.random.default_rng(3)
    for _ in range(50):
        q = rng.normal(size=4)
        m = K.rot_matrix(q)
        w = K.quat_wxyz(m)
        assert np.allclose(K.quat_matrix_wxyz(w), m, atol=1e-9)


def test_the_neck_of_two_spheres_matches_the_smooth_minimum():
    """Two touching spheres at a/k from 1.5 to 15: the field's iso-surface against the quadratic polynomial smooth minimum of
    width k (the film's join), the radius of the profile at the neck and along it."""
    for ratio in (1.5, 3.0, 6.0, 15.0):
        a = 0.5
        k = a / ratio
        rho = 1 + K.KAPPA * k / a
        f = (1 - 1 / rho ** 2) ** 3
        t = min(K.T_MAX, K.STIFF_MAX * f)
        s = t / f

        def field(r, z):
            tot = 0.0
            for cz in (-a, a):
                d2 = (r * r + (z - cz) ** 2) / (a * a)
                tot += s * max(1 - d2 / rho ** 2, 0.0) ** 3
            return tot

        def metaball_r(z):
            lo, hi = 0.0, 3 * a
            for _ in range(60):
                r = (lo + hi) / 2
                if field(r, z) > t:
                    lo = r
                else:
                    hi = r
            return lo

        def smin_r(z):
            lo, hi = 0.0, 3 * a
            for _ in range(60):
                r = (lo + hi) / 2
                d1 = math.hypot(r, z + a) - a
                d2 = math.hypot(r, z - a) - a
                h = max(k - abs(d1 - d2), 0) / k
                g = min(d1, d2) - h * h * k / 4
                if g > 0:
                    hi = r
                else:
                    lo = r
            return lo

        for z in (0.0, 0.3 * a, 0.6 * a):
            assert abs(metaball_r(z) - smin_r(z)) < 0.03 * a, (ratio, z, metaball_r(z), smin_r(z))


def test_a_lone_element_is_its_drawn_ellipsoid():
    parts = parts_of("founder")
    sk = K.plan(parts)
    e = next(e for e in sk["elements"] if e["label"] == "p0")
    c = K.pivot_at(parts[0], K.body_centre(parts))
    R = K.quat_matrix_wxyz(e["rot"])
    for axis in range(3):
        d = np.zeros(3)
        d[axis] = 1.0
        lo, hi = 0.0, 2.0
        for _ in range(60):
            m = (lo + hi) / 2
            pt = c + np.asarray(e["co"]) + R @ (d * m)
            v = K.field_at({"elements": [e]}, parts, pt[None, :])[0]
            if v > sk["threshold"]:
                lo = m
            else:
                hi = m
        assert abs(lo - e["half"][axis]) < 1e-6


def test_basis_scale_gives_the_wanted_cell():
    assert abs(K.basis_scale(0.0025) - 0.5) < 1e-12
    assert abs(K.basis_scale(0.005) - 1.0) < 1e-12


def test_cell_follows_the_pixel_and_the_vertex_budget():
    f_px = 59.3 / 24.0 * 1080
    c = K.cell_for(8.0, f_px, 0.3)
    assert 0.0015 <= c <= 0.012
    assert K.cell_for(8.0, f_px, 0.3) <= K.cell_for(16.0, f_px, 0.3)
    assert K.cell_for(0.1, f_px, 1.0) == 0.0015 and K.cell_for(1e4, f_px, 0.01) == 0.012
    # a render pixel twice a design pixel (960 wide) allows a cell twice as coarse; 4K (half) a finer one
    assert K.cell_for(8.0, f_px, 0.3, px_ratio=2.0) >= 2 * K.cell_for(8.0, f_px, 0.3) - 1e-12
    # the budget: the biggest body at a 3 mm pixel cell would pass 120k vertices, so its cell is raised to the budget's
    area = K.surface_area(parts_of("guz-23x"))
    near = K.cell_for(8.0, f_px, 1.0, area=area, budget=120000)
    assert K.VERTS_PER_AREA * area / near ** 2 <= 120000 * 1.0001
    assert near >= K.cell_for(8.0, f_px, 1.0)


def test_surface_area_predicts_the_vertex_counts_measured_in_blender():
    # (body, vertices of the evaluated skin at a 3 mm cell, tools/skin_probe.py); the estimate is within 20 percent
    for key, verts in (("founder", 147236), ("guz-10", 261536), ("tp-131141", 71692), ("guz-23x", 460828)):
        est = K.VERTS_PER_AREA * K.surface_area(parts_of(key)) / 0.003 ** 2
        assert 0.8 < est / verts < 1.2, (key, est, verts)


def test_the_tip_mask_is_one_at_rest_on_the_tip_and_follows_the_bend():
    parts = parts_of("founder")
    centre = K.body_centre(parts)
    tip = parts[1]
    piv = K.pivot_at(tip, centre)
    ident = (1.0, 0.0, 0.0, 0.0)
    el = K.tip_element(parts, 1)
    rows, off = K.tip_rows(parts, 1, [(piv, ident)], el)
    C = K.to_blender(np.asarray(tip["pos"], float) - centre)
    assert np.allclose(rows @ C + off, 0, atol=1e-9)                       # the tip's centre is the ellipsoid's origin
    h = np.asarray(el["half"])
    R = K.quat_matrix_wxyz(el["rot"])
    for i in range(3):
        p = C + R[:, i] * h[i]
        assert abs(np.linalg.norm(rows @ p + off) - 1) < 1e-9              # its surface is at q = 1
    # bend the pivot 30 degrees about its x axis: the tip's centre has moved, and the mask finds it where it went
    ang = math.radians(30)
    q = (math.cos(ang / 2), math.sin(ang / 2), 0.0, 0.0)
    rows2, off2 = K.tip_rows(parts, 1, [(piv, q)], el)
    Rx = K.quat_matrix_wxyz(q)
    moved = piv + Rx @ (C - piv)
    assert np.allclose(rows2 @ moved + off2, 0, atol=1e-9)
    assert np.linalg.norm(rows2 @ C + off2) > 0.3                           # and the old place no longer is
    assert K.tip_share(0.5) == 1.0 and K.tip_share(2.0) == 0.0 and 0 < K.tip_share(1.1) < 1


def test_the_skin_is_continuous_across_every_joint():
    # the field at each joint's anchor is above the threshold, so the skin is one surface across it
    for key, b in BODIES.items():
        parts = b["parts"]
        sk = K.plan(parts)
        centre = K.body_centre(parts)
        for p in parts:
            if p["parent"] < 0:
                continue
            a = K.to_blender(np.asarray(p["anchor"], float) - centre)
            assert K.field_at(sk, parts, a[None, :])[0] > sk["threshold"], (key, p["i"])
