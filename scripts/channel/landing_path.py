"""The landing's authoring tool: one camera from the sky to coral 22, written as a per-frame pose file.

Frames 0 to the cut are Blender's dive (B3). From the cut frame on the theatre draws the shot, and this tool writes the path it
follows: B3's own frames carried rigidly into the Unity world up to frame J, then a quintic join, then a constant-rate orbit
about the crown that runs to the last frame. The end pose (the last frame) comes from a theatre rig's log line or from
explicit numbers. The tool searches the yaw, the horizontal offset, the orbit's sign and the join's duration against the
clearance, reef-entry, rate and clock rules of D363 and the coordinator's rulings of 2026-11-12, then writes the pose file
(landing_io's format), path.png, curves.png and report.txt.

    python -I scripts/channel/landing_path.py --out <folder> --end-log <theatre log> --end-name V1.coral22 --end-t 43565

All positions are Unity world coordinates (y up, the sea at y = 0) except B3's own file, which is Blender's and goes through
landing_io.to_unity. Angles: azimuth = atan2(z, x) in Unity, so a yaw rotates in the (x, z) plane; pitch is up-positive.
"""
import argparse, bisect, gzip, json, math, os, re, sys
from dataclasses import dataclass, field

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import landing_io  # noqa: E402

FPS = landing_io.FPS
DT = 1.0 / FPS


# ---------------------------------------------------------------------------------------------------------- limits

@dataclass
class Limits:
    cut_frame: int = 248
    dur_min: float = 15.0              # cut to last frame, seconds on the world clock
    dur_max: float = 18.0
    entry_min: float = 5.0             # reef entry, seconds after the cut
    entry_max: float = 7.0
    entry_middle: float = 0.8          # the crown's centre inside the frame's middle 80%
    entry_dist: float = 10.0           # within the fog's full distance
    entry_fraction: float = 0.15       # filling at least this fraction of the frame's height
    crown_r: float = 3.0               # the crown sphere's radius for the entry test (V1's subject)
    coral_clear: float = 0.6
    coral_view_clear: float = 1.0
    body_clear: float = 0.5
    body_inflate: float = 1.0
    surface_y: float = -0.3            # the eye is never above this after the cut
    bed_margin: float = 0.5
    max_rate_dps: float = 60.0         # peak angular rate of the camera, degrees per second
    max_orbit_speed: float = 0.485
    footprint_margin: float = 2.0      # the crossing's distance from the crown's footprint
    lens_lead: float = 3.0             # the lens finishes at least this long before the last frame
    lens0: float = 37.84928883210247
    aspect: float = 16.0 / 9.0
    window: tuple = (43545.0, 43575.0)  # the recorded farm window
    match_angle_accel: bool = False    # yaw and pitch carry their accelerations across J as well as their rates (their rates always match)
    glide_min: float = 0.5             # the glide speed searched, m/s
    glide_max: float = 1.2


@dataclass
class End:
    eye: np.ndarray
    at: np.ndarray
    lens: float
    t: float

    @property
    def forward(self):
        d = np.asarray(self.at, float) - np.asarray(self.eye, float)
        return d / np.linalg.norm(d)


# ---------------------------------------------------------------------------------------------------------- maths

def rot2(x, z, g):
    """Rotate (x, z) by g radians so that the azimuth atan2(z, x) rises by g."""
    c, s = np.cos(g), np.sin(g)
    return x * c - z * s, x * s + z * c


def forward_from(yaw, pitch):
    cp = np.cos(pitch)
    return np.stack([cp * np.cos(yaw), np.sin(pitch), cp * np.sin(yaw)], axis=-1)


def yaw_pitch(f):
    f = np.asarray(f, float)
    return np.arctan2(f[..., 2], f[..., 0]), np.arcsin(np.clip(f[..., 1], -1, 1))


def wrap(a):
    return (a + math.pi) % (2 * math.pi) - math.pi


def smootherstep(s):
    s = np.clip(s, 0.0, 1.0)
    return s * s * s * (s * (6 * s - 15) + 10)


# The six quintic Hermite basis polynomials in s (ascending powers): position, velocity and acceleration at both ends.
_H = np.array([[1, 0, 0, -10, 15, -6], [0, 1, 0, -6, 8, -3], [0, 0, .5, -1.5, 1.5, -.5],
               [0, 0, 0, 10, -15, 6], [0, 0, 0, -4, 7, -3], [0, 0, 0, .5, -1, .5]], float)


def hermite_basis(n):
    """Basis matrices (n+1, 6) for the value, first and second derivative in s at s = k / n."""
    s = np.arange(n + 1) / n
    out = []
    for d in range(3):
        cols = []
        for h in _H:
            p = np.polynomial.polynomial.polyder(h, d) if d else h
            cols.append(np.polynomial.polynomial.polyval(s, p))
        out.append(np.stack(cols, axis=1))
    return out


def hermite(x0, v0, a0, x1, v1, a1, T, basis):
    """Quintic Hermite over T seconds. Endpoints are (K, D); returns value, velocity, acceleration, each (K, n+1, D)."""
    c = np.stack([x0, T * v0, T * T * a0, x1, T * v1, T * T * a1], axis=1)
    return (np.einsum("fj,kjd->kfd", basis[0], c), np.einsum("fj,kjd->kfd", basis[1], c) / T,
            np.einsum("fj,kjd->kfd", basis[2], c) / (T * T))


def quintic_at(stack, u):
    """Quintic Hermite with the six coefficient vectors stack (K, 6, D) = [x0, v0, a0, x1, v1, a1] (derivatives in u),
    evaluated at u (K, n); returns (K, n, D)."""
    pw = np.asarray(u, float)[..., None] ** np.arange(6)
    return np.einsum("knj,kjd->knd", pw @ _H.T, stack)


def quintic_scalar(x0, v0, a0, x1, v1, a1, T, tau):
    """One scalar quintic Hermite over T with its end data in time, evaluated at tau (array)."""
    s_ = np.asarray(tau, float) / T
    c = np.array([x0, T * v0, T * T * a0, x1, T * v1, T * T * a1])
    return (s_[..., None] ** np.arange(6) @ _H.T) @ c


def fd_accel(p, t):
    """Acceleration at the middle of three samples p (3, D) taken at the (not necessarily even) times t (3,)."""
    h1, h2 = t[1] - t[0], t[2] - t[1]
    return 2 * ((p[2] - p[1]) / h2 - (p[1] - p[0]) / h1) / (h1 + h2)


# ---------------------------------------------------------------------------------------------------------- inputs

class B3:
    """B3's camera in Unity coordinates: eye, forward, sim second, and the central-difference state at any frame."""

    def __init__(self, path, cut):
        with open(path, encoding="utf-8") as fh:
            doc = json.load(fh)
        fr = doc["frames"]
        self.cut = cut
        self.vfov = float(doc["vfov_deg"])
        self.t = np.array([f["t"] for f in fr], float)
        self.P = np.asarray(landing_io.to_unity(np.array([f["loc"] for f in fr], float)))
        self.fwd = np.asarray(landing_io.to_unity(np.array([f["dir"] for f in fr], float)))
        self.fwd /= np.linalg.norm(self.fwd, axis=1)[:, None]
        if [f["f"] for f in fr] != list(range(len(fr))):
            raise ValueError("B3's frames are not numbered 0, 1, 2, ... without a gap")
        if cut >= len(fr) - 2:
            raise ValueError(f"the cut frame {cut} is at the end of B3's {len(fr)} frames")
        steps = np.diff(self.t[cut:])
        if np.abs(steps - DT).max() > 1e-6:
            raise ValueError(f"B3's t does not advance 1/{FPS} a frame from frame {cut} on (off by up to "
                             f"{np.abs(steps - DT).max():.2e} s); the cut is inside B3's slow motion")
        y = self.P[:, 1]
        above = np.nonzero((y[:-1] >= 0) & (y[1:] < 0))[0]
        if len(above) == 0:
            raise ValueError("B3's eye never crosses the surface")
        i = int(above[0])
        w = y[i] / (y[i] - y[i + 1])
        self.cross_frame = i + w
        self.cross = self.P[i] + w * (self.P[i + 1] - self.P[i])

    def state_at(self, J):
        """Position, velocity, acceleration (m, m/s, m/s^2) and yaw, pitch with rates and accelerations at frame J, from the
        frames J to J+3 by forward differences (three points for the angles): B3's sim clock is even only from the cut on, so
        nothing looks back."""
        if not (self.cut <= J < len(self.t) - 3):
            raise ValueError(f"J {J} must lie from the cut {self.cut} to B3's fourth-last frame {len(self.t) - 4}")
        p = self.P[J:J + 4]            # four points for the position's derivatives: B3's braking changes fast at the cut
        out = {"P": p[0], "V": (-11 * p[0] + 18 * p[1] - 9 * p[2] + 2 * p[3]) / (6 * DT),
               "A": (2 * p[0] - 5 * p[1] + 4 * p[2] - p[3]) / (DT * DT)}
        yaw, pitch = yaw_pitch(self.fwd[J:J + 3])
        yaw = np.unwrap(yaw)
        for name, q in (("yaw", yaw), ("pitch", pitch)):
            out[name] = float(q[0])
            out[name + "_d"] = float((-3 * q[0] + 4 * q[1] - q[2]) / (2 * DT))
            out[name + "_dd"] = float((q[0] - 2 * q[1] + q[2]) / (DT * DT))
        return out

    def drag_fit(self, J, n=12):
        """Fit dv/dt = -k v^2 to B3's speed over frames J to J+n: returns (k fit, rms residual in m/s^2, k at J alone,
        v at J, tangential acceleration at J)."""
        st = self.state_at(J)
        T0 = st["V"] / np.linalg.norm(st["V"])
        v = {J: float(np.linalg.norm(st["V"]))}
        for i in range(J + 1, J + n + 2):
            v[i] = float(np.linalg.norm(self.P[i + 1] - self.P[i - 1]) / (2 * DT))
        acc = {J: float(st["A"] @ T0)}
        for i in range(J + 1, J + n + 1):
            acc[i] = (v[i + 1] - v[i - 1]) / (2 * DT)
        idx = list(range(J, J + n + 1))
        a_ = np.array([acc[i] for i in idx])
        v_ = np.array([v[i] for i in idx])
        k = float(-(a_ * v_ ** 2).sum() / (v_ ** 4).sum())
        rms = float(np.sqrt(np.mean((a_ + k * v_ ** 2) ** 2)))
        return k, rms, float(-acc[J] / v[J] ** 2), v[J], acc[J]


_RIG = re.compile(r"\[Theatre\] rig (\S+) u=1\.00 t=([\d.]+) eye=\(([^)]*)\) at=\(([^)]*)\) lens=([\d.]+)")


def parse_rig_line(text, name=None):
    """The end pose from a theatre `rig <name> u=1.00 ...` log line; the first line that matches the name (or any name)."""
    for line in text.splitlines():
        m = _RIG.search(line)
        if m and (name is None or m.group(1) == name):
            eye = [float(v) for v in m.group(3).split(",")]
            at = [float(v) for v in m.group(4).split(",")]
            return End(np.array(eye), np.array(at), float(m.group(5)), float(m.group(2)))
    raise ValueError(f"no `[Theatre] rig {name or '<name>'} u=1.00 t=... eye=(...) at=(...) lens=...` line found")


class Coral:
    """The reef's pieces: capsules (two end points and a radius) and plates (a horizontal disc of radius R about a centre,
    swept by a sphere of half-thickness h: CoralGeometry's Plate). Distances are to the surface."""

    def __init__(self, pieces, colonies=None):
        pieces = list(pieces)
        self.colony = np.array([p["colony"] for p in pieces], int)
        self.kind = np.array([p["kind"] for p in pieces], int)
        self.A = np.array([[p["ax"], p["ay"], p["az"]] for p in pieces], float)
        self.B = np.array([[p["bx"], p["by"], p["bz"]] for p in pieces], float)
        self.r = np.array([p["radius"] for p in pieces], float)
        self.h = np.array([p["halfThickness"] for p in pieces], float)
        self.colonies = colonies or []

    @classmethod
    def load(cls, path):
        with open(path, encoding="utf-8") as fh:
            doc = json.load(fh)
        return cls(doc["pieces"], doc.get("colonies"))

    def subset(self, mask):
        c = Coral.__new__(Coral)
        for k in ("colony", "kind", "A", "B", "r", "h"):
            setattr(c, k, getattr(self, k)[mask])
        c.colonies = self.colonies
        return c

    def near(self, centre, radius):
        c = np.asarray(centre, float)
        d = np.minimum(np.linalg.norm(self.A - c, axis=1), np.linalg.norm(self.B - c, axis=1))
        return self.subset(d <= radius + 6.0)

    def __len__(self):
        return len(self.kind)

    def closest(self, P):
        """For points (m, 3): the closest point on each piece's core (m, n, 3) and the distance to the surface (m, n)."""
        P = np.asarray(P, float)[:, None, :]
        AB = self.B - self.A
        den = np.maximum((AB * AB).sum(1), 1e-18)
        t = np.clip(((P - self.A) * AB).sum(-1) / den, 0.0, 1.0)
        q = self.A + t[..., None] * AB                        # capsules: the segment
        plate = self.kind == 1
        if plate.any():
            d = P[:, :, [0, 2]] - self.A[:, [0, 2]]
            n = np.sqrt((d * d).sum(-1))
            k = np.minimum(1.0, self.r / np.maximum(n, 1e-12))
            qp = np.stack([self.A[:, 0] + d[..., 0] * k, np.broadcast_to(self.A[:, 1], k.shape), self.A[:, 2] + d[..., 1] * k], -1)
            q = np.where(plate[None, :, None], qp, q)
        rad = np.where(plate, self.h, self.r)
        return q, np.sqrt(((P - q) ** 2).sum(-1)) - rad

    def clearance(self, eye, fwd, vfov, aspect, clear, view_clear):
        """Margin (>= 0 passes) and the distances for an eye path (F, 3) with its forward (F, 3) and vfov (F,) in degrees.
        A piece within `view_clear` whose nearest point is in the frame needs view_clear, any piece needs `clear`."""
        F = len(eye)
        best = np.full(F, np.inf)           # the worst margin per frame
        dmin = np.full(F, np.inf)
        dview = np.full(F, np.inf)
        which = np.zeros(F, int)
        if len(self) == 0:
            return best, dmin, dview, which
        th = np.tan(np.radians(vfov) / 2)
        up = np.stack([-np.sin(yaw_pitch(fwd)[1]) * np.cos(yaw_pitch(fwd)[0]), np.cos(yaw_pitch(fwd)[1]),
                       -np.sin(yaw_pitch(fwd)[1]) * np.sin(yaw_pitch(fwd)[0])], -1)
        for a in range(0, F, 160):
            sl = slice(a, min(F, a + 160))
            q, d = self.closest(eye[sl])
            v = q - eye[sl][:, None, :]
            z = (v * fwd[sl][:, None, :]).sum(-1)
            y = (v * up[sl][:, None, :]).sum(-1)
            x2 = np.maximum((v * v).sum(-1) - z * z - y * y, 0.0)
            zz = np.maximum(z, 1e-9)
            inview = (z > 0.05) & (np.abs(y) <= th[sl][:, None] * zz) & (np.sqrt(x2) <= th[sl][:, None] * aspect * zz)
            need = np.where(inview, view_clear, clear)
            m = d - need
            best[sl] = m.min(1)
            which[sl] = m.argmin(1)
            dmin[sl] = d.min(1)
            dview[sl] = np.where(inview, d, np.inf).min(1)
        return best, dmin, dview, which

    def footprint_distance(self, xz, colony):
        """Horizontal distance from points (m, 2) to the footprint of one colony: its capsules and discs seen from above."""
        sel = self.colony == colony
        if not sel.any():
            raise ValueError(f"no pieces in colony {colony}")
        A, B, r, h, kind = self.A[sel][:, [0, 2]], self.B[sel][:, [0, 2]], self.r[sel], self.h[sel], self.kind[sel]
        P = np.asarray(xz, float)[:, None, :]
        AB = B - A
        den = np.maximum((AB * AB).sum(1), 1e-18)
        t = np.clip(((P - A) * AB).sum(-1) / den, 0.0, 1.0)
        q = A + t[..., None] * AB
        d = np.sqrt(((P - q) ** 2).sum(-1)) - np.where(kind == 1, r + h, r)
        return d.min(1)


class Bodies:
    """Bodies' positions from rows 10 s apart, linearly interpolated between rows (a body missing from a row keeps its nearest
    known position, so a body that died is still treated as there)."""

    def __init__(self, times, pos):
        self.times = np.asarray(times, float)
        self.pos = np.asarray(pos, float)          # (rows, bodies, 3)

    @classmethod
    def load(cls, path, t_lo, t_hi, centre, cull_r):
        rows = []
        opener = gzip.open if str(path).endswith(".gz") else open
        rx = re.compile(r'^\{"t":\s*([0-9.eE+-]+)')
        with opener(path, "rt", encoding="utf-8") as fh:
            for line in fh:
                m = rx.match(line)
                if not m:
                    continue
                t = float(m.group(1))
                if t > t_hi + 10.5:
                    break
                if t >= t_lo - 10.5:
                    rows.append((t, np.array(json.loads(line)["b"], float)))
        if not rows:
            raise ValueError(f"{path} holds no row near {t_lo} to {t_hi} s")
        c = np.asarray(centre, float)
        near = set()
        for _, b in rows:
            if len(b):
                d = np.linalg.norm(b[:, 1:4] - c, axis=1)
                near.update(int(i) for i in b[d <= cull_r, 0])
        ids = sorted(near)
        col = {i: k for k, i in enumerate(ids)}
        pos = np.full((len(rows), len(ids), 3), np.nan)
        for r, (_, b) in enumerate(rows):
            for row in b:
                k = col.get(int(row[0]))
                if k is not None:
                    pos[r, k] = row[1:4]
        for k in range(len(ids)):                   # carry a gap from the nearest row that knows the body
            known = np.nonzero(~np.isnan(pos[:, k, 0]))[0]
            for r in range(len(rows)):
                if np.isnan(pos[r, k, 0]):
                    pos[r, k] = pos[known[np.abs(known - r).argmin()], k]
        return cls([t for t, _ in rows], pos)

    def at(self, ts):
        ts = np.asarray(ts, float)
        if self.pos.shape[1] == 0:
            return np.zeros((len(ts), 0, 3))
        i = np.clip(np.searchsorted(self.times, ts, side="right") - 1, 0, max(len(self.times) - 2, 0))
        if len(self.times) == 1:
            return np.repeat(self.pos[:1], len(ts), axis=0)
        w = np.clip((ts - self.times[i]) / (self.times[i + 1] - self.times[i]), 0.0, 1.0)[:, None, None]
        return self.pos[i] * (1 - w) + self.pos[i + 1] * w


# ---------------------------------------------------------------------------------------------------------- the path

@dataclass
class Ctx:
    b3: B3
    end: End
    centre: np.ndarray                  # crown centre (the orbit's axis and the entry sphere's centre)
    lim: Limits
    omega: float                        # orbit rate, rad/s
    coral: Coral = None
    colony: int = 22
    bodies: Bodies = None
    bed_y: float = -23.5
    crown_c: np.ndarray = None

    def __post_init__(self):
        if self.crown_c is None:
            self.crown_c = np.asarray(self.centre, float)


@dataclass
class Params:
    J: int
    join_frames: int
    orbit_frames: int
    yaw_deg: float
    rho: float
    alpha_deg: float
    sign: int
    glide: float = 0.8


def speed_profile(v0, k, g, v_o, n_j, blend=0.4):
    """The join's speed in time: B3's drag law, a short blend, then a glide down to the orbit's speed.

    v = v0 / (1 + k v0 t) until it reaches the handover speed g_h; then, with tau the time since,
    v = g_h - k g_h^2 E(tau) + (v_o - g) * smootherstep(tau / T_f), where E(tau) is the integral of
    (1 - smootherstep(tau / blend)). The first term keeps the drag law's acceleration -k g_h^2 at tau = 0 and takes it to zero
    over `blend` seconds (acceleration is continuous at the handover; the jerk is not), losing g_h - g of speed; the glide
    speed g is the speed once the blend is done, and the second term then carries it down to v_o by the last frame, with zero
    rate and zero acceleration there. g_h follows from g: g_h - k g_h^2 blend / 2 = g. The speed falls monotonically.

    Returns the distance run at each frame (n_j + 1,), the speed there, the handover time and the total distance, or None if
    the profile does not hold (no handover speed below v0, a glide not above the orbit's speed, or under a second for the fall)."""
    T = n_j * DT
    q = k * blend
    disc = 1.0 - 2.0 * q * g
    if k <= 0 or disc <= 0 or not (v_o < g):
        return None
    g_h = (1.0 - math.sqrt(disc)) / q
    if not (g < g_h < v0):
        return None
    t_h = (1.0 / k) * (1.0 / g_h - 1.0 / v0)
    T_f = T - t_h
    if T_f < max(1.0, 2 * blend):
        return None
    m = 8
    tt = np.arange(n_j * m + 1) / (FPS * m)
    v = v0 / (1.0 + k * v0 * tt)
    fall = tt > t_h
    tau = tt[fall] - t_h
    e_b = 1.0 - smootherstep(tau / blend)
    E = tau[0] + np.concatenate([[0.0], np.cumsum(0.5 * (e_b[1:] + e_b[:-1]) / (FPS * m))])     # 1 - smootherstep is 1 to the 5th order at 0
    v[fall] = g_h - k * g_h * g_h * E + (v_o - g) * smootherstep(tau / T_f)
    if (np.diff(v) > 1e-12).any() or v.min() < v_o - 1e-6 or abs(v[-1] - v_o) > 1e-6:
        return None
    dist = np.concatenate([[0.0], np.cumsum(0.5 * (v[1:] + v[:-1]) / (FPS * m))])
    return {"s": dist[::m], "v": v[::m], "t_h": t_h, "S": float(dist[-1]), "k": k, "g": g, "g_h": g_h}


def endpoints(ctx, J, n_o, gam, rho, alpha, sig):
    """Both ends of the join for K candidates (Unity coordinates), and B3's part from the cut to J carried the same way."""
    b3, end, c, om = ctx.b3, ctx.end, np.asarray(ctx.centre, float), ctx.omega
    gam, rho, alpha, sig = (np.atleast_1d(np.asarray(a, float)) for a in (gam, rho, alpha, sig))
    K = len(gam)
    j = np.arange(n_o + 1)
    ang = sig[:, None] * om * ((j - n_o) * DT)[None, :]
    rel = np.asarray(end.eye, float) - c
    ex, ez = rot2(rel[0], rel[2], ang)
    orb_eye = np.stack([c[0] + ex, np.full_like(ex, end.eye[1]), c[2] + ez], -1)
    f_end = end.forward
    fx, fz = rot2(f_end[0], f_end[2], ang)
    orb_fwd = np.stack([fx, np.full_like(fx, f_end[1]), fz], -1)
    P1 = orb_eye[:, 0]
    V1 = np.stack([-(P1[:, 2] - c[2]), np.zeros(K), P1[:, 0] - c[0]], -1) * (sig * om)[:, None]
    A1 = -om * om * np.stack([P1[:, 0] - c[0], np.zeros(K), P1[:, 2] - c[2]], -1)
    st = b3.state_at(J)
    nB = J - b3.cut + 1
    Q = b3.P[b3.cut:J + 1] - st["P"]
    Qx, Qz = rot2(Q[None, :, 0], Q[None, :, 2], gam[:, None])
    P0 = P1 + np.stack([rho * np.cos(alpha), np.full(K, st["P"][1]) - P1[:, 1], rho * np.sin(alpha)], -1)
    eyeB = np.stack([P0[:, None, 0] + Qx, np.broadcast_to(P0[:, None, 1] + Q[None, :, 1], Qx.shape), P0[:, None, 2] + Qz], -1)
    fb = b3.fwd[b3.cut:J + 1]
    fbx, fbz = rot2(fb[None, :, 0], fb[None, :, 2], gam[:, None])
    fwdB = np.stack([fbx, np.broadcast_to(fb[None, :, 1], fbx.shape), fbz], -1)
    vx, vz = rot2(st["V"][0], st["V"][2], gam)
    ax, az = rot2(st["A"][0], st["A"][2], gam)
    V0 = np.stack([vx, np.full(K, st["V"][1]), vz], -1)
    A0 = np.stack([ax, np.full(K, st["A"][1]), az], -1)
    cx, cz = rot2(b3.cross[0] - st["P"][0], b3.cross[2] - st["P"][2], gam)
    return {"st": st, "nB": nB, "orb_eye": orb_eye, "orb_fwd": orb_fwd, "P1": P1, "V1": V1, "A1": A1, "P0": P0, "V0": V0, "A0": A0,
            "eyeB": eyeB, "fwdB": fwdB, "gam": gam, "sig": sig,
            "cross": np.stack([P0[:, 0] + cx, P0[:, 2] + cz], -1)}


NU = 160                                            # samples of the join's curve when its length is measured


def _curve_stack(ep, c, v0n, v_o):
    """The join's curve as a quintic in u: B3's position, direction and curvature vector at J, the orbit's at its start,
    with the end tangents scaled by c (the length is tuned through c)."""
    V0, A0, V1, A1 = ep["V0"], ep["A0"], ep["V1"], ep["A1"]
    T0 = V0 / v0n
    a_t = (A0 * T0).sum(-1, keepdims=True)
    a_n = A0 - a_t * T0
    T1 = V1 / v_o
    cc = c[:, None]
    return np.stack([ep["P0"], cc * T0, cc * cc * a_n / (v0n * v0n), ep["P1"], cc * T1, cc * cc * A1 / (v_o * v_o)], axis=1)


def _curve_length(stack):
    u = np.broadcast_to(np.linspace(0.0, 1.0, NU + 1), (len(stack), NU + 1))
    q = quintic_at(stack, u)
    return q, np.linalg.norm(np.diff(q, axis=1), axis=-1).sum(-1)


def build_paths(ctx, J, n_j, n_o, gam, rho, alpha, sig, glide):
    """Eye, forward (K, F, 3) and vfov (F,) from the cut to the last frame for K candidates (arrays of equal length), with
    the join's diagnostics, or None when the glide's speed profile does not hold. F = (J - cut + 1) + n_j + n_o. gam, alpha in
    radians, rho in metres, sig = +1 / -1.

    The join keeps B3's position, velocity (speed and direction) and acceleration at J. Its speed is `speed_profile`; its
    path is a smooth curve whose end data match B3's at J (tangent and curvature) and the orbit's at its start, with the
    curve's length tuned so that the distance the speed profile runs is exactly its length. `reach` is False for a candidate
    no curve of that length joins."""
    lim, end, om = ctx.lim, ctx.end, ctx.omega
    ep = endpoints(ctx, J, n_o, gam, rho, alpha, sig)
    K = len(ep["gam"])
    st = ep["st"]
    v0 = float(np.linalg.norm(st["V"]))
    k_j = -float(st["A"] @ (st["V"] / v0)) / (v0 * v0)
    r_h = math.hypot(end.eye[0] - ctx.centre[0], end.eye[2] - ctx.centre[2])
    v_o = om * r_h
    prof = speed_profile(v0, k_j, glide, v_o, n_j)
    if prof is None:
        return None
    S = prof["S"]
    D3 = np.linalg.norm(ep["P1"] - ep["P0"], axis=-1)
    # scan the end tangents' scale for the lengths, bracket S, refine by bisection
    scale = D3[:, None] * np.geomspace(0.02, 4.0, 24)[None, :]
    Ls = np.stack([_curve_length(_curve_stack(ep, scale[:, i], v0, v_o))[1] for i in range(scale.shape[1])], axis=1)
    cross = (Ls[:, :-1] - S) * (Ls[:, 1:] - S) <= 0
    reach = cross.any(1) & (D3 > 1e-6)
    i0 = np.where(reach, cross.argmax(1), 0)
    ar = np.arange(K)
    lo, hi = scale[ar, i0], scale[ar, i0 + 1]
    Llo = Ls[ar, i0]
    for _ in range(16):
        mid = 0.5 * (lo + hi)
        Lm = _curve_length(_curve_stack(ep, mid, v0, v_o))[1]
        same = (Lm - S) * (Llo - S) > 0
        lo = np.where(same, mid, lo)
        Llo = np.where(same, Lm, Llo)
        hi = np.where(same, hi, mid)
    c = np.where(reach, 0.5 * (lo + hi), D3)
    stack = _curve_stack(ep, c, v0, v_o)
    L = _curve_length(stack)[1]
    nf = 8000 if K <= 16 else 400                   # a finer table for the positions than the one that tuned the length; the pose file's
    # path is built alone, so it gets the fine one (millimetre errors in the table would show as metres per second squared)
    ug = np.linspace(0.0, 1.0, nf + 1)
    q = quintic_at(stack, np.broadcast_to(ug, (K, nf + 1)))
    cum = np.concatenate([np.zeros((K, 1)), np.cumsum(np.linalg.norm(np.diff(q, axis=1), axis=-1), axis=1)], axis=1)
    s_k = prof["s"]
    # the table's length differs from the profile's distance by a millimetre; stretch the distances to fit it, so that the last
    # frame is the orbit's start with no jump (a jump of 0.1 mm shows as 0.1 m/s^2 in a second difference)
    u = np.stack([np.interp(s_k * (cum[i, -1] / s_k[-1]), cum[i], ug) for i in range(K)])
    pos = quintic_at(stack, u)
    # yaw and pitch: one scalar quintic each, with B3's rate (and acceleration) at J and the orbit's at its start
    T = n_j * DT
    basis = hermite_basis(n_j)
    f_end = end.forward
    sig_ = ep["sig"]
    yaw1_raw = math.atan2(f_end[2], f_end[0]) + sig_ * om * (-n_o * DT)
    yaw0 = st["yaw"] + ep["gam"]
    yaw1 = yaw0 + wrap(yaw1_raw - yaw0)
    pitch1 = math.asin(np.clip(f_end[1], -1, 1))
    z0 = np.zeros((K, 1))
    ydd = st["yaw_dd"] if lim.match_angle_accel else 0.0
    pdd = st["pitch_dd"] if lim.match_angle_accel else 0.0
    ya, yd, _ = hermite(yaw0[:, None], np.full((K, 1), st["yaw_d"]), np.full((K, 1), ydd), yaw1[:, None], (sig_ * om)[:, None], z0, T, basis)
    pa, pd_, _ = hermite(np.full((K, 1), st["pitch"]), np.full((K, 1), st["pitch_d"]), np.full((K, 1), pdd), np.full((K, 1), pitch1), z0, z0, T, basis)
    fwdJ = forward_from(ya[..., 0], pa[..., 0])
    lens = np.concatenate([np.full(ep["nB"], lim.lens0), lim.lens0 + (end.lens - lim.lens0) * smootherstep(np.arange(1, n_j + 1) / n_j),
                           np.full(n_o, end.lens)])
    eye = np.concatenate([ep["eyeB"], pos[:, 1:], ep["orb_eye"][:, 1:]], 1)
    fwd = np.concatenate([ep["fwdB"], fwdJ[:, 1:], ep["orb_fwd"][:, 1:]], 1)
    return {"eye": eye, "fwd": fwd, "vfov": lens, "nB": ep["nB"], "n_j": n_j, "n_o": n_o, "speed": prof["v"],
            "dyaw": yd[..., 0], "dpitch": pd_[..., 0], "yaw_total": yaw1 - yaw0, "pitch_total": np.full(K, pitch1 - st["pitch"]),
            "cross": ep["cross"], "P0": ep["P0"], "P1": ep["P1"], "V0": ep["V0"], "V1": ep["V1"], "A0": ep["A0"], "A1": ep["A1"],
            "reach": reach, "profile": prof, "k_j": k_j, "v_o": v_o, "curve_scale": c, "curve_length": L}


def peak_rate(fwd):
    """Per-step angular rate of the forward vector in degrees per second, (K, F-1)."""
    a, b = fwd[:, :-1], fwd[:, 1:]
    cr = np.linalg.norm(np.cross(a, b), axis=-1)
    return np.degrees(np.arctan2(cr, (a * b).sum(-1))) * FPS


def visible_crown(ctx, eye, fwd, vfov):
    """Per frame: is crown 22 clearly in the frame. The crown sphere's centre is inside the middle `entry_middle` of the frame,
    within `entry_dist` of its surface (the rig's own measure: V1's log reads 7.6 m where the centre is 10.6 m), and the sphere fills at least `entry_fraction` of the frame's height."""
    lim = ctx.lim
    v = np.asarray(ctx.crown_c, float) - eye
    d = np.linalg.norm(v, axis=-1)
    z = (v * fwd).sum(-1)
    yaw, pit = yaw_pitch(fwd)
    up = np.stack([-np.sin(pit) * np.cos(yaw), np.cos(pit), -np.sin(pit) * np.sin(yaw)], -1)
    y = (v * up).sum(-1)
    x = np.sqrt(np.maximum(d * d - z * z - y * y, 0.0))
    th = np.tan(np.radians(vfov) / 2)
    zz = np.maximum(z, 1e-9)
    frac = 2 * np.degrees(np.arcsin(np.minimum(1.0, lim.crown_r / np.maximum(d, 1e-9)))) / vfov
    return (z > 0) & (np.abs(y) <= lim.entry_middle * th * zz) & (np.abs(x) <= lim.entry_middle * th * lim.aspect * zz) \
        & (d - lim.crown_r <= lim.entry_dist) & (frac >= lim.entry_fraction)


def entry_index(vis):
    """First frame index from which the crown stays clearly in frame to the last frame; -1 where it never does. (K, F) -> (K,)."""
    tail = np.logical_and.accumulate(vis[:, ::-1], axis=1)[:, ::-1]
    first = np.where(tail.any(1), tail.argmax(1), -1)
    return first


def cheap_stage(ctx, P, paths):
    """The constraints that need no reef or bodies. Returns the stage names, a (stages, K) boolean array of each stage's
    own pass, the entry second after the cut (K,) and the peak rate (K,)."""
    lim = ctx.lim
    eye, fwd, vfov = paths["eye"], paths["fwd"], paths["vfov"]
    K = len(eye)
    y = eye[..., 1]
    ok = {}
    ok["arc length reachable"] = paths["reach"]
    ok["surface"] = y.max(1) <= lim.surface_y
    ok["bed"] = y.min(1) >= ctx.bed_y + lim.bed_margin
    tol = 1e-4
    ok["yaw one-way"] = ((paths["dyaw"] * np.sign(paths["yaw_total"])[:, None]) >= -tol).all(1) | (np.abs(paths["yaw_total"]) < 1e-9)
    ok["pitch one-way"] = ((paths["dpitch"] * np.sign(paths["pitch_total"])[:, None]) >= -tol).all(1)
    pk = peak_rate(fwd).max(1)
    ok["rate"] = pk <= lim.max_rate_dps
    ei = entry_index(visible_crown(ctx, eye, fwd, vfov))
    entry = np.where(ei >= 0, ei * DT, np.nan)
    with np.errstate(invalid="ignore"):
        ok["reef entry"] = (entry >= lim.entry_min) & (entry <= lim.entry_max)
    names = list(ok)
    return names, np.stack([ok[n] for n in names]), entry, pk


def full_check(ctx, P, paths, k=0, stride=1):
    """Clearance of candidate k against the reef and the bodies, at every `stride`-th frame, with where the minima fall."""
    lim, cut = ctx.lim, ctx.b3.cut
    eye, fwd, vfov = paths["eye"][k], paths["fwd"][k], paths["vfov"]
    N = P.J + P.join_frames + P.orbit_frames + 1
    idx = np.arange(0, len(eye), stride)
    if idx[-1] != len(eye) - 1:
        idx = np.append(idx, len(eye) - 1)
    film = cut + idx
    out = {"idx": idx}
    if ctx.coral is not None and len(ctx.coral):
        m, dmin, dview, which = ctx.coral.clearance(eye[idx], fwd[idx], vfov[idx], lim.aspect, lim.coral_clear, lim.coral_view_clear)
        a = int(m.argmin())
        out.update(coral_margin=float(m[a]), coral_min=float(dmin.min()), coral_min_f=int(film[int(dmin.argmin())]),
                   coral_view_min=float(dview.min()), coral_margin_f=int(film[a]), coral_margin_piece=int(which[a]))
    else:
        out.update(coral_margin=np.inf, coral_min=np.inf, coral_min_f=cut, coral_view_min=np.inf, coral_margin_f=cut,
                   coral_margin_piece=-1)
    if ctx.bodies is not None and ctx.bodies.pos.shape[1]:
        ws = ctx.end.t - (N - 1 - film) * DT
        bp = ctx.bodies.at(ws)
        d = np.sqrt(((bp - eye[idx][:, None, :]) ** 2).sum(-1))
        dm = d.min(1)
        a = int(dm.argmin())
        out.update(body_min=float(dm[a]), body_margin=float(dm[a] - (lim.body_clear + lim.body_inflate)), body_min_f=int(film[a]))
    else:
        out.update(body_min=np.inf, body_margin=np.inf, body_min_f=cut)
    out["bed_margin"] = float(eye[idx, 1].min() - ctx.bed_y)
    out["surface_margin"] = float(lim.surface_y - eye[idx, 1].max())
    out["pass"] = out["coral_margin"] >= 0 and out["body_margin"] >= 0
    return out


def score_terms(r):
    """The ranking: higher is better. Each term is printed so the winner's win can be explained."""
    t = {"coral margin (m, cap 2)": min(r["coral_margin"], 2.0), "bodies margin (m, cap 2, x0.5)": 0.5 * min(r["body_margin"], 2.0),
         "footprint (m, cap 4, x0.2)": 0.2 * min(r["footprint"], 4.0), "peak rate (x -0.03 per deg/s)": -0.03 * r["peak_rate"],
         "entry off 6 s (x -0.3 per s)": -0.3 * abs(r["entry"] - 6.0)}
    return t


def bodies_margin_batch(ctx, eye, stride=3, cull=30.0):
    """The worst body margin (m; >= 0 passes) per candidate for eyes (K, F, 3), at every `stride`-th frame, against bodies
    within `cull` metres of the crown centre."""
    K, F = eye.shape[:2]
    out = np.full(K, np.inf)
    if ctx.bodies is None or ctx.bodies.pos.shape[1] == 0:
        return out
    near = (np.linalg.norm(ctx.bodies.pos - np.asarray(ctx.centre, float), axis=-1) <= cull).any(0)
    need = ctx.lim.body_clear + ctx.lim.body_inflate
    idx = np.unique(np.append(np.arange(0, F, stride), F - 1))
    ws = ctx.end.t - (F - 1 - idx) * DT
    bp = ctx.bodies.at(ws)[:, near]
    for j, i in enumerate(idx):
        d = np.sqrt(((eye[:, i, None, :] - bp[j][None]) ** 2).sum(-1)).min(1) - need
        out = np.minimum(out, d)
    return out


def search(ctx, j_list, dur_list, orbit_list, glides, yaw_degs, rhos, alpha_degs, verbose=True, max_full=3000, chunk=1500,
           want=40, body_min=0.05):
    """Grid search; returns the passing candidates (dicts) ranked best first, the stage counts, the number of grid candidates
    that passed everything and the near misses on bodies. Per (J, total duration, orbit length, glide): the speed profile
    must hold; the crossing must be off the footprint; the profile's distance must reach from J's eye to the orbit's start;
    then the curve is built and the cheap rules and the bodies are applied. Survivors are checked against the reef at every
    third frame in order of preference, until `want` pass or `max_full` are checked; the finalists are re-checked at every
    frame."""
    lim = ctx.lim
    rej = {}

    def count(name, n):
        rej[name] = rej.get(name, 0) + int(n)

    survivors, near = [], []
    for J in j_list:
        for D in dur_list:
            for To in orbit_list:
                for gl in glides:
                    n_o = int(round(To * FPS))
                    n_j = int(round(D * FPS)) - (J - lim.cut_frame) - n_o
                    if n_j < FPS or To < lim.lens_lead:
                        continue
                    g, r, a, s = np.meshgrid(np.radians(yaw_degs), rhos, np.radians(alpha_degs), [1, -1], indexing="ij")
                    g, r, a, s = g.ravel(), r.ravel(), a.ravel(), s.ravel()
                    count("candidates", len(g))
                    for lo in range(0, len(g), chunk):
                        sl = slice(lo, lo + chunk)
                        gs, rs, as_, ss = g[sl], r[sl], a[sl], s[sl]
                        ep = endpoints(ctx, J, n_o, gs, rs, as_, ss)
                        st = ep["st"]
                        v0 = float(np.linalg.norm(st["V"]))
                        k_j = -float(st["A"] @ (st["V"] / v0)) / (v0 * v0)
                        r_h = math.hypot(ctx.end.eye[0] - ctx.centre[0], ctx.end.eye[2] - ctx.centre[2])
                        prof = speed_profile(v0, k_j, gl, ctx.omega * r_h, n_j)
                        if prof is None:
                            continue
                        count("speed profile holds", len(gs))
                        keep = np.ones(len(gs), bool)
                        if ctx.coral is not None and len(ctx.coral):
                            keep &= ctx.coral.footprint_distance(ep["cross"], ctx.colony) >= lim.footprint_margin
                        count("crossing off footprint", keep.sum())
                        keep &= prof["S"] >= np.linalg.norm(ep["P1"] - ep["P0"], axis=-1)
                        count("distance reaches the orbit", keep.sum())
                        idx = np.nonzero(keep)[0]
                        if not len(idx):
                            continue
                        paths = build_paths(ctx, J, n_j, n_o, gs[idx], rs[idx], as_[idx], ss[idx], gl)
                        names, ok, entry, pk = cheap_stage(ctx, None, paths)
                        alive = np.ones(len(idx), bool)
                        for n, row in zip(names, ok):
                            alive &= row
                            count(n, alive.sum())
                        live = np.nonzero(alive)[0]
                        bm = np.full(len(idx), np.inf)
                        if len(live):
                            bm[live] = np.concatenate([bodies_margin_batch(ctx, paths["eye"][live[i:i + 100]]) for i in range(0, len(live), 100)])
                            rej["_best body margin among the cheap survivors (m)"] = max(
                                rej.get("_best body margin among the cheap survivors (m)", -np.inf), float(bm[live].max()))
                        count("bodies", (alive & (bm >= body_min)).sum())
                        for k in live:
                            item = (abs(entry[k] - 6.0) + pk[k] / 60.0, Params(J, n_j, n_o, float(np.degrees(gs[idx][k])), float(rs[idx][k]),
                                    float(np.degrees(as_[idx][k])), int(ss[idx][k]), float(gl)), float(entry[k]), float(pk[k]), float(bm[k]))
                            (survivors if bm[k] >= body_min else near).append(item)
    survivors.sort(key=lambda x: x[0])
    near.sort(key=lambda x: -x[4])
    if verbose:
        print("search: survivors after each stage (cumulative within a chunk): " +
              ", ".join(f"{k} {v}" for k, v in rej.items() if not k.startswith("_")), flush=True)
    passed, checked = [], 0
    for _, P, entry, pk, _bm in survivors:
        if checked >= max_full or len(passed) >= want:
            break
        checked += 1
        paths = build_paths(ctx, P.J, P.join_frames, P.orbit_frames, [math.radians(P.yaw_deg)], [P.rho], [math.radians(P.alpha_deg)], [P.sign], P.glide)
        res = full_check(ctx, P, paths, 0, stride=3)
        if res["coral_margin"] >= 0.1 and res["body_margin"] >= 0.1:
            passed.append((P, entry, pk))
    if verbose:
        print(f"search: {checked} survivors checked against reef and bodies (every 3rd frame); {len(passed)} kept", flush=True)
    final = []
    for P, entry, pk in passed:
        paths = build_paths(ctx, P.J, P.join_frames, P.orbit_frames, [math.radians(P.yaw_deg)], [P.rho], [math.radians(P.alpha_deg)], [P.sign], P.glide)
        res = full_check(ctx, P, paths, 0, stride=1)
        if not res["pass"]:
            continue
        fp = float(ctx.coral.footprint_distance(paths["cross"], ctx.colony)[0]) if ctx.coral is not None and len(ctx.coral) else np.inf
        res.update(params=P, entry=entry, peak_rate=pk, footprint=fp)
        res["terms"] = score_terms(res)
        res["score"] = sum(res["terms"].values())
        final.append(res)
    final.sort(key=lambda r: -r["score"])
    return final, rej, len(survivors), near


# ---------------------------------------------------------------------------------------------------------- outputs

def make_rows(ctx, P, paths, k=0):
    """The pose file's rows, frames 0 to the last, and the world_minus_sim that makes the last row's world_s the end second."""
    b3, cut, lim = ctx.b3, ctx.b3.cut, ctx.lim
    eye, fwd, vfov = paths["eye"][k], paths["fwd"][k], paths["vfov"]
    gam = math.radians(P.yaw_deg)
    # B3's frames 0 to cut-1, rotated and carried with the rest of its part
    st = b3.state_at(P.J)
    p0 = paths["P0"][k]
    Q = b3.P[:cut] - st["P"]
    qx, qz = rot2(Q[:, 0], Q[:, 2], gam)
    eye_pre = np.stack([p0[0] + qx, p0[1] + Q[:, 1], p0[2] + qz], -1)
    fx, fz = rot2(b3.fwd[:cut, 0], b3.fwd[:cut, 2], gam)
    fwd_pre = np.stack([fx, b3.fwd[:cut, 1], fz], -1)
    eye_all = np.concatenate([eye_pre, eye])
    fwd_all = np.concatenate([fwd_pre, fwd])
    vfov_all = np.concatenate([np.full(cut, lim.lens0), vfov])
    N = len(eye_all)
    sim = np.concatenate([b3.t[:cut], b3.t[cut] + (np.arange(N - cut)) * DT])
    wms = ctx.end.t - sim[-1]
    rows = []
    for f in range(N):
        rows.append({"f": f, "film_s": f / FPS, "sim_s": float(sim[f]), "world_s": float(sim[f] + wms) if f >= cut else None,
                     "eye": [float(v) for v in eye_all[f]], "forward": [float(v) for v in fwd_all[f]],
                     "up": [float(v) for v in landing_io.up_for(fwd_all[f])], "vfov": float(vfov_all[f])})
    return rows, wms, eye_all, fwd_all, vfov_all


def check_window(ctx, wms, rows):
    cut = ctx.b3.cut
    w0, w1 = rows[cut]["world_s"] - 1.0, rows[-1]["world_s"]
    lo, hi = ctx.lim.window
    if w0 < lo - 1e-6 or w1 > hi + 1e-6:
        raise ValueError(f"the plan needs world seconds {w0:.2f} to {w1:.2f}, outside the recorded window {lo:.0f} to {hi:.0f}")
    return w0, w1


def make_plots(ctx, P, res, rows, eye_all, fwd_all, vfov_all, out):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    cut, lim = ctx.b3.cut, ctx.lim
    N = len(eye_all)
    f = np.arange(N)
    entry_f = cut + int(round(res["entry"] * FPS))
    jf, je = P.J, P.J + P.join_frames
    c = np.asarray(ctx.centre, float)
    fig, ax = plt.subplots(1, 2, figsize=(16, 8))
    co = ctx.coral
    for a, (ix, iy, title) in zip(ax, ((0, 2, "top view (x, z)"), (0, 1, "side view (x, y)"))):
        if co is not None:
            for i in range(len(co)):
                col = "#c0392b" if co.colony[i] == ctx.colony else "#bbbbbb"
                if co.kind[i] == 1:
                    if iy == 2:
                        a.add_patch(plt.Circle((co.A[i, 0], co.A[i, 2]), co.r[i], fill=False, ec=col, lw=0.6))
                    else:
                        a.plot([co.A[i, 0] - co.r[i], co.A[i, 0] + co.r[i]], [co.A[i, 1]] * 2, color=col, lw=2)
                else:
                    a.plot([co.A[i, ix], co.B[i, ix]], [co.A[i, iy], co.B[i, iy]], color=col, lw=max(0.5, co.r[i] * 4))
        for s, e, colr, lab in ((0, cut, "#7f8c8d", "B3 before the cut"), (cut, jf + 1, "#2471a3", "B3 in the theatre (cut to J)"),
                                (jf, je + 1, "#e67e22", "join"), (je, N, "#27ae60", "orbit")):
            a.plot(eye_all[s:e, ix], eye_all[s:e, iy], color=colr, lw=2, label=lab)
        for fr, mk, lab in ((cut, "s", "cut"), (jf, "D", "J"), (je, "^", "orbit start"), (entry_f, "*", "reef entry"), (N - 1, "o", "last frame")):
            a.plot(eye_all[fr, ix], eye_all[fr, iy], mk, color="k", ms=10 if mk != "*" else 15, mfc="yellow" if mk == "*" else "w", label=lab)
        a.plot(c[ix], c[iy], "r+", ms=14, label="crown centre")
        if ix == 0 and iy == 2:
            a.plot(*paths_cross(ctx, P, res), "kx", ms=12, label="crossing")
            a.set_aspect("equal")
        a.set_title(title)
        a.set_xlabel("x (m)")
        a.set_ylabel("z (m)" if iy == 2 else "y (m)")
        a.grid(alpha=0.3)
        lo = eye_all[:, [ix, iy]].min(0) - 12
        hi = eye_all[:, [ix, iy]].max(0) + 12
        a.set_xlim(lo[0], hi[0])
        a.set_ylim(lo[1], hi[1])
    ax[1].axhline(0, color="c", lw=1)
    ax[0].legend(loc="best", fontsize=8)
    fig.tight_layout()
    fig.savefig(os.path.join(out, "path.png"), dpi=110)
    plt.close(fig)

    t = (f - cut) / FPS
    sp = np.r_[np.linalg.norm(np.diff(eye_all, axis=0), axis=1) * FPS, np.nan]
    yaw, pit = yaw_pitch(fwd_all)
    fig, axs = plt.subplots(5, 1, figsize=(12, 14), sharex=True)
    series = ((sp, "speed (m/s)"), (np.degrees(pit), "pitch (deg)"), (np.degrees(np.unwrap(yaw)), "yaw (deg)"), (vfov_all, "vfov (deg)"),
              (np.linalg.norm(eye_all - c, axis=1), "distance to the crown centre (m)"))
    lens_done = (je - cut) / FPS
    for a, (y, lab) in zip(axs, series):
        m = f >= cut
        a.plot(t[m], y[m], color="#2c3e50")
        for x, col, ls in (((jf - cut) / FPS, "#2471a3", "--"), ((je - cut) / FPS, "#27ae60", "--"), ((entry_f - cut) / FPS, "#c0392b", "-"),
                           (lens_done, "#8e44ad", ":")):
            a.axvline(x, color=col, ls=ls, lw=1.2)
        a.set_ylabel(lab)
        a.grid(alpha=0.3)
    axs[-1].set_xlabel("film seconds after the cut (cut = 0; J blue dash, orbit start green dash, reef entry red, lens finish purple dots)")
    fig.tight_layout()
    fig.savefig(os.path.join(out, "curves.png"), dpi=100)
    plt.close(fig)


def paths_cross(ctx, P, res):
    p = build_paths(ctx, P.J, P.join_frames, P.orbit_frames, [math.radians(P.yaw_deg)], [P.rho], [math.radians(P.alpha_deg)], [P.sign], P.glide)
    return p["cross"][0]


def write_report(path, ctx, P, res, rows, wms, window, table, rej, nsurv, source, paths=None):
    cut, lim = ctx.b3.cut, ctx.lim
    N = len(rows)
    entry_f = cut + int(round(res["entry"] * FPS))
    eye = np.array([r["eye"] for r in rows])
    sp = np.linalg.norm(np.diff(eye, axis=0), axis=1) * FPS
    L = []
    L.append("landing_path report")
    L.append(f"source: {source}")
    L.append("")
    L.append("chosen")
    L.append(f"  cut frame {cut}; J {P.J}; join {P.join_frames} frames ({P.join_frames / FPS:.2f} s); orbit {P.orbit_frames} frames ({P.orbit_frames / FPS:.2f} s)")
    L.append(f"  yaw {P.yaw_deg:.1f} deg, J-eye {P.rho:.1f} m from the orbit start at bearing {P.alpha_deg:.1f} deg (horizontal), orbit sign {P.sign:+d}")
    L.append(f"  orbit rate {math.degrees(ctx.omega):.2f} deg/s about the crown centre, radius {math.hypot(*(np.asarray(ctx.end.eye)[[0, 2]] - np.asarray(ctx.centre)[[0, 2]])):.2f} m, "
             f"speed {ctx.omega * math.hypot(*(np.asarray(ctx.end.eye)[[0, 2]] - np.asarray(ctx.centre)[[0, 2]])):.3f} m/s (limit {lim.max_orbit_speed})")
    L.append(f"  total after the cut {(N - 1 - cut) / FPS:.2f} s (limits {lim.dur_min} to {lim.dur_max})")
    L.append(f"  eye speed: at J {sp[P.J]:.3f} m/s, at the orbit start {sp[P.J + P.join_frames]:.3f}, at the end {sp[-1]:.3f}; peak camera rate {res['peak_rate']:.1f} deg/s (limit {lim.max_rate_dps})")
    L.append(f"  vfov: {lim.lens0:.2f} deg to frame {P.J}, a smootherstep to {ctx.end.lens:.2f} deg finishing at frame {P.J + P.join_frames} "
             f"({(N - 1 - P.J - P.join_frames) / FPS:.2f} s before the last frame; limit {lim.lens_lead})")
    L.append("")
    L.append("the reef-entry test, as this tool defines it")
    L.append(f"  crown sphere centre {np.round(ctx.crown_c, 2).tolist()}, r {lim.crown_r} m; clearly in frame = centre inside the middle {lim.entry_middle:.0%} of the frame "
             f"both ways, within {lim.entry_dist} m of its surface (the rig's own measure), filling at least {lim.entry_fraction:.0%} of the frame height (2 asin(r/d) / vfov), "
             f"and it stays so to the last frame")
    L.append(f"  reef entry: frame {entry_f}, {res['entry']:.2f} s after the cut (window {lim.entry_min} to {lim.entry_max})")
    L.append("")
    L.append("clearance (every frame from the cut)")
    L.append(f"  coral: nearest surface {res['coral_min']:.2f} m at frame {res['coral_min_f']} (film {res['coral_min_f'] / FPS:.2f} s); "
             f"nearest in view {res['coral_view_min']:.2f} m; worst margin {res['coral_margin']:.2f} m at frame {res['coral_margin_f']} (piece {res['coral_margin_piece']})")
    L.append(f"  bodies: nearest {res['body_min']:.2f} m at frame {res['body_min_f']} (limit {lim.body_clear} + {lim.body_inflate} inflation); margin {res['body_margin']:.2f} m")
    L.append(f"  surface: highest eye {lim.surface_y - res['surface_margin']:.2f} m (limit {lim.surface_y}); bed: lowest eye {ctx.bed_y + res['bed_margin']:.2f} m (bed taken as {ctx.bed_y} m)")
    L.append(f"  crossing: {res['footprint']:.2f} m off colony {ctx.colony}'s footprint (limit {lim.footprint_margin})")
    L.append("")
    L.append("clock")
    L.append(f"  world_minus_sim {wms:.6f}; cut world second {rows[cut]['world_s']:.3f}; J {rows[P.J]['world_s']:.3f}; reef entry {rows[entry_f]['world_s']:.3f}; end {rows[-1]['world_s']:.3f}")
    L.append(f"  the theatre needs the window {window[0]:.3f} to {window[1]:.3f} s (starts 1 s before the cut; recorded {lim.window[0]:.0f} to {lim.window[1]:.0f})")
    L.append("")
    L.append("search")
    L.append("  " + "; ".join(f"{k} {v}" for k, v in rej.items() if not k.startswith("_")) + f"; grid candidates that passed everything {nsurv} (refinement of the near misses found the rest)")
    L.append(f"  rules: the join matches B3's position, velocity and acceleration at J; yaw and pitch match rate{' and acceleration' if lim.match_angle_accel else ''}")
    if paths is not None:
        prof = paths["profile"]
        kf, rms, kj, v0, at = ctx.b3.drag_fit(P.J)
        L.append("")
        L.append("the join's speed (B3's drag law, then a glide)")
        L.append(f"  drag fit over frames {P.J} to {P.J + 12}: dv/dt = -k v^2 with k = {kf:.3f} per m (rms residual {rms:.2f} m/s^2); at J alone k = {kj:.3f} "
                 f"(v {v0:.3f} m/s, tangential acceleration {at:.2f} m/s^2). The join uses k at J, so its acceleration at J is B3's")
        L.append(f"  glide speed {P.glide:.2f} m/s, reached at {prof['t_h']:.2f} s after J; then one quintic to the orbit's {paths['v_o']:.3f} m/s; "
                 f"distance run {prof['S']:.2f} m, curve length {paths['curve_length'][0]:.2f} m")
        eye_a = np.array([r["eye"] for r in rows])
        sim = np.array([r["sim_s"] for r in rows])
        sp_at = lambda t: float(np.linalg.norm(eye_a[cut + int(round(t * FPS)) + 1] - eye_a[cut + int(round(t * FPS)) - 1]) * FPS / 2) if t > 0 else \
            float(np.linalg.norm(-3 * eye_a[cut] + 4 * eye_a[cut + 1] - eye_a[cut + 2]) * FPS / 2)
        L.append("  speed after the cut (finite differences of the rows): " + ", ".join(f"{t:g} s {sp_at(t):.3f} m/s" for t in (0.5, 1, 2, 4)))
        J = P.J
        a_b = fd_accel(eye_a[J - 2:J + 1], sim[J - 2:J + 1])
        a_a = fd_accel(eye_a[J:J + 3], sim[J:J + 3])
        T0 = (eye_a[J + 1] - eye_a[J]) / np.linalg.norm(eye_a[J + 1] - eye_a[J])
        L.append(f"  acceleration at J from the rows, three points each side (sim clock): before {np.round(a_b, 2).tolist()} |a| {np.linalg.norm(a_b):.2f}, "
                 f"along the path {float(a_b @ T0):.2f}; after {np.round(a_a, 2).tolist()} |a| {np.linalg.norm(a_a):.2f}, along the path {float(a_a @ T0):.2f} m/s^2 "
                 f"(the stencils are centred at J-1 and J+1, so B3's own jerk between them shows; their mean along the path is "
                 f"{0.5 * (float(a_b @ T0) + float(a_a @ T0)):.2f} against {-paths['k_j'] * float(np.linalg.norm(paths['V0'][0])) ** 2:.2f} m/s^2 "
                 f"for the join's law at J)")
    L.append("")
    L.append("top candidates (score = sum of terms)")
    L.extend("  " + ln for ln in table)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(L) + "\n")
    return L


def candidate_table(final, n=10):
    head = f"{'#':>2} {'score':>6} {'J':>4} {'join':>5} {'orb':>5} {'yaw':>6} {'rho':>5} {'alpha':>6} {'sgn':>3} {'glide':>5} {'entry':>6} {'peak':>6} {'coral':>6} {'body':>6} {'foot':>5}"
    rows = [head]
    for i, r in enumerate(final[:n]):
        P = r["params"]
        rows.append(f"{i + 1:>2} {r['score']:>6.2f} {P.J:>4} {P.join_frames:>5} {P.orbit_frames:>5} {P.yaw_deg:>6.1f} {P.rho:>5.1f} {P.alpha_deg:>6.1f} {P.sign:>+3d} {P.glide:>5.2f} "
                    f"{r['entry']:>6.2f} {r['peak_rate']:>6.1f} {r['coral_margin']:>6.2f} {r['body_margin']:>6.2f} {r['footprint']:>5.1f}")
    return rows


def why_won(final):
    w = final[0]
    if len(final) == 1:
        return "it was the only candidate that passed every check"
    r = final[1]
    diffs = sorted(((w["terms"][k] - r["terms"][k], k) for k in w["terms"]), reverse=True)
    best = ", ".join(f"{k} {d:+.2f}" for d, k in diffs[:2] if d > 1e-9)
    return f"it scored {w['score']:.2f} against {r['score']:.2f} for the runner-up; the terms it won by: {best or 'none (a tie)'}"


# ---------------------------------------------------------------------------------------------------------- main

def parse_args(argv):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", required=True, help="folder for the pose file, path.png, curves.png and report.txt")
    ap.add_argument("--b3", default=os.path.join("scratch", "dive-shot", "crossing3", "anim", "a4", "D--camera.json"))
    ap.add_argument("--name", default="landing", help="the pose file's stem")
    g = ap.add_argument_group("the end pose, in Unity coordinates: a rig log line, or all four explicit numbers")
    g.add_argument("--end-log")
    g.add_argument("--end-name", help="the rig name on the log line, e.g. V1.coral22")
    g.add_argument("--end-eye", type=float, nargs=3)
    g.add_argument("--end-at", type=float, nargs=3)
    g.add_argument("--end-lens", type=float)
    g.add_argument("--end-t", type=float, help="the last frame's world second (overrides the log's t)")
    ap.add_argument("--crown", type=float, nargs=3, default=[103.1, -6.3, 106.0], help="crown 22's centre (the orbit's axis)")
    ap.add_argument("--crown-r", type=float, default=3.0)
    ap.add_argument("--colony", type=int, default=22, help="the coral cache's colony that is the crown")
    ap.add_argument("--coral", default=os.path.join("scratch", "wt-camera5", "scratch", "theatre-cache", "coral",
                                                    "16bb2b98317243511f2a4708afa86bcf043e85a80797bd19afb670011e627282.json"))
    ap.add_argument("--bodies", default=os.path.join("runs", "r67r1-6202", "2026-11-05-220032-029c8c74", "positions.jsonl.gz"))
    ap.add_argument("--world-depth", type=float, default=25.0)
    ap.add_argument("--bed-relief", type=float, default=1.5)
    ap.add_argument("--cut", type=int, default=248)
    ap.add_argument("--j", type=int, nargs="+", default=[248], help="the frame(s) up to which B3's path is played (searched); the join starts there")
    ap.add_argument("--glide", type=float, nargs="+", default=[0.5, 0.7, 0.9, 1.1], help="the glide speeds searched, m/s (0.5 to 1.2)")
    ap.add_argument("--dur", type=float, nargs="+", help="total seconds after the cut to search (default 15 to 18 in 1 s steps)")
    ap.add_argument("--orbit-s", type=float, nargs="+", default=[3.5, 4.5])
    ap.add_argument("--drift-dps", type=float, default=2.0)
    ap.add_argument("--yaw-step", type=float, default=15.0)
    ap.add_argument("--rho", type=float, nargs="+", default=[1, 2, 3, 4, 5, 6, 8, 10])
    ap.add_argument("--alpha-step", type=float, default=20.0)
    ap.add_argument("--max-rate", type=float, default=60.0)
    ap.add_argument("--body-inflate", type=float, default=1.0, help="metres added to the 0.5 m body clearance (the brief's 1.0)")
    ap.add_argument("--match-angle-accel", action="store_true",
                    help="match yaw's and pitch's accelerations at J too (their rates always match); B3's pitch is still speeding up there, and matching it overshoots the one-way rule")
    ap.add_argument("--window", type=float, nargs=2, default=[43545.0, 43575.0])
    return ap.parse_args(argv)


def check_end_pose(ctx):
    """Refuse an end pose that already breaks a clearance at the last frame: no path to it can pass."""
    lim, end = ctx.lim, ctx.end
    if ctx.coral is not None and len(ctx.coral):
        d = ctx.coral.closest(np.array([end.eye]))[1].min()
        if d < lim.coral_clear:
            sys.exit(f"landing_path: the end eye is {d:.2f} m from a coral piece (limit {lim.coral_clear} m); the end pose itself breaks the clearance")
    if ctx.bodies is not None and ctx.bodies.pos.shape[1]:
        bp = ctx.bodies.at([end.t])[0]
        d = np.linalg.norm(bp - np.asarray(end.eye), axis=1)
        if d.min() < lim.body_clear + lim.body_inflate:
            sys.exit(f"landing_path: the end eye is {d.min():.2f} m from a body at world second {end.t} (limit {lim.body_clear} + {lim.body_inflate} "
                     f"inflation = {lim.body_clear + lim.body_inflate} m, positions at {np.round(bp[int(d.argmin())], 2).tolist()}); the end pose itself breaks the clearance")


def main(argv=None):
    a = parse_args(argv)
    lim = Limits(cut_frame=a.cut, crown_r=a.crown_r, max_rate_dps=a.max_rate, window=tuple(a.window), body_inflate=a.body_inflate,
                 match_angle_accel=a.match_angle_accel)
    if any(not lim.glide_min - 1e-9 <= g <= lim.glide_max + 1e-9 for g in a.glide):
        sys.exit(f"landing_path: --glide must lie from {lim.glide_min} to {lim.glide_max} m/s")
    if a.end_log:
        with open(a.end_log, encoding="utf-8", errors="replace") as fh:
            end = parse_rig_line(fh.read(), a.end_name)
        if a.end_t is not None:
            end.t = a.end_t
    elif a.end_eye and a.end_at and a.end_lens and a.end_t is not None:
        end = End(np.array(a.end_eye), np.array(a.end_at), a.end_lens, a.end_t)
    else:
        sys.exit("landing_path: give --end-log (with --end-name), or all of --end-eye, --end-at, --end-lens and --end-t")
    omega = math.radians(a.drift_dps)
    centre = np.array(a.crown, float)
    rad = math.hypot(end.eye[0] - centre[0], end.eye[2] - centre[2])
    if omega * rad > lim.max_orbit_speed:
        sys.exit(f"landing_path: the orbit's speed {omega * rad:.3f} m/s exceeds {lim.max_orbit_speed}; lower --drift-dps")
    try:
        b3 = B3(a.b3, a.cut)
    except ValueError as e:
        sys.exit(f"landing_path: {e}")
    coral = Coral.load(a.coral).near(centre, 40.0)
    coral_all = Coral.load(a.coral)
    dur = a.dur or [float(d) for d in np.arange(lim.dur_min, lim.dur_max + 1e-9, 1.0)]
    for d in dur:
        if not lim.dur_min - 1e-9 <= d <= lim.dur_max + 1e-9:
            sys.exit(f"landing_path: --dur {d} is outside {lim.dur_min} to {lim.dur_max} s")
    t_lo = end.t - lim.dur_max - 2.0
    bodies = Bodies.load(a.bodies, t_lo, end.t, centre, 45.0) if a.bodies and os.path.exists(a.bodies) else None
    if bodies is None:
        print("landing_path: no bodies file found; bodies are not checked (pass --bodies)", file=sys.stderr)
    ctx = Ctx(b3=b3, end=end, centre=centre, lim=lim, omega=omega, coral=coral, colony=a.colony, bodies=bodies,
              bed_y=-a.world_depth + a.bed_relief)
    # the colony's footprint is read from the whole cache so that `near` cannot drop its far pieces
    ctx.coral = coral_all.near(centre, 40.0)
    check_end_pose(ctx)
    yaws = np.arange(0, 360, a.yaw_step)
    alphas = np.arange(0, 360, a.alpha_step)
    final, rej, nsurv, near = search(ctx, a.j, dur, a.orbit_s, a.glide, yaws, np.array(a.rho, float), alphas)
    # finer passes around the best finalists and the nearest misses, each with half the step before
    seen_seeds = set()
    for rnd in range(1 if final else 3):
        h = 0.5 ** (rnd + 1)
        seeds = [r["params"] for r in final[:8]] + [it[1] for it in near[:40]]
        if not seeds:
            break
        new_final, new_near = [], []
        for P in seeds:
            key = (P.J, P.join_frames, P.orbit_frames, round(P.yaw_deg, 3), round(P.rho, 3), round(P.alpha_deg, 3), P.sign, P.glide, rnd)
            if key in seen_seeds:
                continue
            seen_seeds.add(key)
            dd = (P.J - lim.cut_frame + P.join_frames + P.orbit_frames) / FPS
            for sg in (P.sign,):
                f2, _, _, n2 = search(ctx, [P.J], [dd], [P.orbit_frames / FPS],
                                      [max(lim.glide_min, P.glide - h * 0.2), P.glide, min(lim.glide_max, P.glide + h * 0.2)],
                                      np.array([P.yaw_deg - h * a.yaw_step, P.yaw_deg, P.yaw_deg + h * a.yaw_step]),
                                      np.array([P.rho - h * 2.0, P.rho, P.rho + h * 2.0]),
                                      np.array([P.alpha_deg - h * a.alpha_step, P.alpha_deg, P.alpha_deg + h * a.alpha_step]), verbose=False)
                new_final.extend(f2)
                new_near.extend(n2)
        final = final + new_final
        near = sorted(near + new_near, key=lambda x: -x[4])
        print(f"refine round {rnd + 1}: {len(seeds)} seeds, {len(new_final)} more passing; best remaining body miss "
              f"{near[0][4] if near else float('nan'):.2f} m", flush=True)
        final.sort(key=lambda r: -r["score"])
    seen, merged = set(), []
    for r in sorted(final, key=lambda r: -r["score"]):
        P = r["params"]
        key = (P.J, P.join_frames, P.orbit_frames, round(P.yaw_deg, 3), round(P.rho, 3), round(P.alpha_deg, 3), P.sign, round(P.glide, 3))
        if key not in seen:
            seen.add(key)
            merged.append(r)
    final = merged
    rej["passing candidates after refinement"] = len(final)
    if not final:
        print("landing_path: no candidate passed every check. Candidates left after each stage (cumulative within a chunk): " +
              ", ".join(f"{k} {v}" for k, v in rej.items() if not k.startswith("_")) + "; the best body margin among the cheap survivors is "
              f"{rej.get('_best body margin among the cheap survivors (m)', float('nan')):.2f} m. If 'distance reaches the orbit' is 0 the end pose is "
              "too far from J's eye for the drag-then-glide speed (try a shallower end, a higher --glide, or a longer --dur); "
              "otherwise read which later stage emptied.", file=sys.stderr)
        sys.exit(2)
    table = candidate_table(final)
    print("\n".join(table))
    best = final[0]
    P = best["params"]
    print("winner: " + why_won(final))
    paths = build_paths(ctx, P.J, P.join_frames, P.orbit_frames, [math.radians(P.yaw_deg)], [P.rho], [math.radians(P.alpha_deg)], [P.sign], P.glide)
    rows, wms, eye_all, fwd_all, vfov_all = make_rows(ctx, P, paths)
    try:
        window = check_window(ctx, wms, rows)
    except ValueError as e:
        sys.exit(f"landing_path: {e}")
    os.makedirs(a.out, exist_ok=True)
    source = (f"landing_path.py: B3 {os.path.basename(os.path.dirname(os.path.dirname(a.b3)))}/{os.path.basename(a.b3)} sha256 {landing_io.sha256(a.b3)[:16]}; "
              f"end eye {np.round(end.eye, 3).tolist()} at {np.round(end.at, 3).tolist()} lens {end.lens} t {end.t}; cut {a.cut} J {P.J} join {P.join_frames} "
              f"orbit {P.orbit_frames} yaw {P.yaw_deg:.1f} rho {P.rho:.1f} alpha {P.alpha_deg:.1f} sign {P.sign:+d} glide {P.glide:.2f} drift {a.drift_dps} deg/s")
    out_path = os.path.join(a.out, a.name + ".poses.json")
    landing_io.write_poses(out_path, rows, a.cut, wms, source)
    landing_io.read_poses(out_path)
    make_plots(ctx, P, best, rows, eye_all, fwd_all, vfov_all, a.out)
    lines = write_report(os.path.join(a.out, "report.txt"), ctx, P, best, rows, wms, window, table, rej, nsurv, source, paths)
    lines.append("")
    lines.append("winner: " + why_won(final))
    with open(os.path.join(a.out, "report.txt"), "a", encoding="utf-8") as fh:
        fh.write("\nwinner: " + why_won(final) + "\n")
    print("\n".join(lines[:40]))
    print(f"wrote {out_path}, path.png, curves.png, report.txt in {a.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
