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



# ------------------------------------------------------------------ a sphere's drawn size (TheatrePalette.SphereReachAxes, D359)
def test_joined_spheres_are_one_skin_in_every_guzzler():
    # the field is over the threshold all along the segment between the centres of two sphere parts joined at a joint
    seen = 0
    for key, body in BODIES.items():
        if body.get("team") != "guzzler":
            continue
        parts = body["parts"]
        by = {p["i"]: p for p in parts}
        sk = K.plan(parts)
        centre = K.body_centre(parts)
        for p in parts:
            if p["parent"] < 0 or p["shape"] != "sphere" or by[p["parent"]]["shape"] != "sphere":
                continue
            seen += 1
            a = K.to_blender(np.asarray(by[p["parent"]]["pos"], float) - centre)
            b = K.to_blender(np.asarray(p["pos"], float) - centre)
            pts = a + np.linspace(0, 1, 400)[:, None] * (b - a)
            assert K.field_at(sk, parts, pts).min() > sk["threshold"], (key, p["i"])
    assert seen >= 20


def test_a_spheres_longest_semi_axis_is_the_ball_radius():
    # before the inset, the longest drawn semi-axis is Core's r = (|hx| + |hy| + |hz|) / 3, reach or no reach
    for key, body in BODIES.items():
        for p in body["parts"]:
            if p["shape"] != "sphere":
                continue
            r = sum(abs(x) for x in p["half"]) / 3
            assert abs(K.drawn_half(body["parts"], p).max() - r) < 1e-9, (key, p["i"])
            axes, t = K.sphere_reach(body["parts"], p["i"])
            assert 0.0 <= t <= 1.0 and abs(axes.max() - 1.0) < 1e-12


def test_every_joint_anchor_is_inside_its_spheres_drawn_ellipsoid():
    for key, body in BODIES.items():
        parts = body["parts"]
        for p in parts:
            if p["shape"] != "sphere":
                continue
            semi = K.drawn_half(parts, p)
            anchors = ([p["canchor"]] if p["parent"] >= 0 else []) + [q["panchor"] for q in parts if q["parent"] == p["i"]]
            for a in anchors:
                assert ((np.asarray(a, float) / semi) ** 2).sum() <= 1 + 1e-4, (key, p["i"], a)


def test_reach_is_zero_where_the_anchors_are_inside_already():
    # a sphere of half extents (0.1, 0.2, 0.3), r = 0.2, drawn at basis (1/3, 2/3, 1) x r = (0.0667, 0.1333, 0.2): an anchor on
    # its long axis (0, 0, 0.2) is on that ellipsoid, so t = 0 and it is drawn as Aspect draws it
    ident = [0, 0, 0, 1]
    body = [{"i": 0, "parent": -1, "shape": "sphere", "half": [0.1, 0.2, 0.3], "pos": [0, 0, 0], "rot": ident,
             "anchor": [0, 0, 0], "panchor": [0, 0, 0], "canchor": [0, 0, 0]},
            {"i": 1, "parent": 0, "shape": "sphere", "half": [0.1, 0.1, 0.1], "pos": [0, 0, 0.3], "rot": ident,
             "anchor": [0, 0, 0.2], "panchor": [0, 0, 0.2], "canchor": [0, 0, -0.1]}]
    axes, t = K.sphere_reach(body, 0)
    assert t == 0.0 and np.allclose(axes, (1 / 3, 2 / 3, 1))
    assert np.allclose(K.drawn_half(body, body[0]), (0.2 / 3, 0.4 / 3, 0.2))
    # move the child to the short axis and it is not: t rises until the anchor is on the drawn surface
    body[1]["panchor"] = [0.2, 0, 0]
    axes, t = K.sphere_reach(body, 0)
    assert 0 < t <= 1 and abs(axes[0] * 0.2 - 0.2) < 1e-5


# ------------------------------------------------------------------ a capsule's drawn shape (TheatreOneSkin.PrimitiveOf)
def _surface_along(e, parts, origin, direction, hi=2.0):
    """Distance from `origin` along unit `direction` (body frame, Blender axes) to the lone element's isolated surface."""
    lo = 0.0
    for _ in range(60):
        m = (lo + hi) / 2
        v = K.field_at({"elements": [e]}, parts, (origin + direction * m)[None, :])[0]
        if v > _T[0]:
            lo = m
        else:
            hi = m
    return lo


_T = [None]


def _capsule_frame(key, label):
    parts = parts_of(key)
    sk = K.plan(parts)
    _T[0] = sk["threshold"]
    e = next(e for e in sk["elements"] if e["label"] == label)
    owner = next(p for p in parts if p["i"] == e["owner"])
    c = K.pivot_at(owner, K.body_centre(parts)) + np.asarray(e["co"], float)
    return parts, sk, e, c, K.quat_matrix_wxyz(e["rot"])


def test_a_long_capsules_shaft_is_straight():
    # guz-23's link p1 (half extents 0.0972, 0.3122, 0.0876: r 0.0924, span 0.2198, sx 1, sz 0.901): a CAPSULE element, and
    # its drawn half width on each cross axis is the same, within 2 %, at every station along the shaft
    for key, label in (("guz-23", "p1"), ("founder", "p1"), ("nib-28", None)):
        parts = parts_of(key)
        if label is None:
            label = next("p%d" % p["i"] for p in parts if p["shape"] == "capsule" and K.capsule_drawn(p)[0])
        parts, sk, e, c, R = _capsule_frame(key, label)
        assert e["type"] == "CAPSULE"
        for axis in (0, 1):
            want = e["half"][axis]
            for s in np.linspace(-e["span"], e["span"], 9):
                got = _surface_along(e, parts, c + R[:, 2] * s, R[:, axis])
                assert abs(got - want) <= 0.02 * want, (key, axis, s, got, want)


def test_a_long_capsules_ends_are_round():
    # past the shaft's end the surface is the squashed cap, the ellipsoid (r sx - k/4, r sz - k/4, r - k/4) about the end
    # of the shaft: its pole is at span + R along the axis, and every direction between lands on that ellipsoid
    parts, sk, e, c, R = _capsule_frame("guz-23", "p1")
    semi = np.array([e["half"][0], e["half"][1], e["radius"]])
    for end in (-1.0, 1.0):
        o = c + R[:, 2] * end * e["span"]
        for ang in np.linspace(0, math.pi / 2, 7):
            for cross in (0, 1):
                local = np.zeros(3)
                local[2] = end * math.cos(ang)
                local[cross] = math.sin(ang)
                got = _surface_along(e, parts, o, R @ local)
                want = 1.0 / math.sqrt(((local / semi) ** 2).sum())
                assert abs(got - want) < 1e-6, (end, ang, cross, got, want)
    # and the pole is the collider's pole less the inset: span + R = |hy| - k/4
    hy = abs(parts[1]["half"][1])
    assert abs(e["span"] + e["radius"] - (hy - 0.25 * sk["k"])) < 1e-12


def test_a_short_capsule_is_one_ellipsoid():
    # half extents (0.1, 0.05, 0.08): r = 0.09, span = max(0, 0.05 - 0.09) = 0 < r, so the film's NearlyASphere ellipsoid
    # (r sx, max(hy, r), r sz) = (0.09, 0.09, 0.072), inset by k/4 like any part
    ident = [0, 0, 0, 1]
    body = [{"i": 0, "parent": -1, "shape": "box", "half": [0.2, 0.2, 0.2], "pos": [0, 0, 0], "rot": ident,
             "anchor": [0, 0, 0], "panchor": [0, 0, 0], "canchor": [0, 0, 0]},
            {"i": 1, "parent": 0, "shape": "capsule", "half": [0.1, 0.05, 0.08], "pos": [0, 0.25, 0], "rot": ident,
             "anchor": [0, 0.2, 0], "panchor": [0, 0.2, 0], "canchor": [0, -0.05, 0]}]
    assert not K.capsule_drawn(body[1])[0]
    assert np.allclose(K.drawn_half(body, body[1]), (0.09, 0.09, 0.072))
    sk = K.plan(body)
    e = next(e for e in sk["elements"] if e["label"] == "p1")
    i = 0.25 * sk["k"]
    assert e["type"] == "ELLIPSOID" and np.allclose(e["half"], (0.09 - i, 0.072 - i, 0.09 - i))   # Blender (x, z, y)
    # every short capsule in the bodies is drawn so too
    for key, b in BODIES.items():
        for p in b["parts"]:
            if p["shape"] == "capsule" and not K.capsule_drawn(p)[0]:
                el = next(x for x in K.plan(b["parts"])["elements"] if x["label"] == "p%d" % p["i"])
                assert el["type"] == "ELLIPSOID", key
