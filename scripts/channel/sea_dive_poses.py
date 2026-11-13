"""The pure half of sea_dive.py's --pose-file and --bubble-export: pose rows to camera states, the surface crossing, the rigid
placement of the pose file's world into the sea's frame, the theatre's sun as a Blender vector, and the bubble export's arrays.
Plain Python and numpy, no bpy and no mathutils, so the tests load it alone. Formats: scratch/theatre-lane/landing-b3/formats.md;
the conversions are landing_io's (Unity (x, y, z) is Blender (x, z, y)).

Two frames appear. The pose frame is the pose file's world in Blender axes. The sea frame is the one sea_dive.py renders in:
the pose frame moved so that the surface crossing sits at the origin (the sea disc, the probe grid and the ripples are centred
there) and turned about the vertical by `psi`, so that the pose file's sun bearing becomes the bearing the sky's sun really
has in the scene. A rigid proper rotation about the vertical, so no sense of rotation is ever converted."""
import math
import numpy as np
import landing_io as LIO

# The theatre's sun (unity/Assets/Theatre/TheatreLook.cs, SunAzimuthDegrees and SunElevationDegrees, FixedSun()): azimuth from
# +x towards +z in Unity's axes, elevation above the horizon.
THEATRE_SUN_AZ, THEATRE_SUN_EL = 44.0, 67.0


def theatre_sun_unity(az_deg=THEATRE_SUN_AZ, el_deg=THEATRE_SUN_EL):
    """TheatreLook.FixedSun(): flat = cos(el); (flat cos az, sin el, flat sin az), Unity axes, unit."""
    az, el = math.radians(az_deg), math.radians(el_deg)
    flat = math.cos(el)
    v = np.array([flat * math.cos(az), math.sin(el), flat * math.sin(az)])
    return v / np.linalg.norm(v)


def theatre_sun_blender(az_deg=THEATRE_SUN_AZ, el_deg=THEATRE_SUN_EL):
    """The same direction in Blender axes, through landing_io.to_blender."""
    return LIO.to_blender(theatre_sun_unity(az_deg, el_deg))


def sun_bearing(v):
    """The bearing of a Blender direction: atan2(y, x), radians."""
    return math.atan2(float(v[1]), float(v[0]))


def rot_z(psi):
    c, s = math.cos(psi), math.sin(psi)
    return np.array([[c, -s, 0.0], [s, c, 0.0], [0.0, 0.0, 1.0]])


def rows_to_states(rows):
    """Pose rows (Unity axes) to per-frame arrays in the pose frame (Blender axes). Keys: f, t (= sim_s), tf (= film_s), loc,
    dir (the row's forward), pitch (degrees, from dir), vel (the central difference over sim_s, one-sided at the ends, as
    path D's), speed, vfov (radians)."""
    n = len(rows)
    if n < 3:
        raise ValueError("a pose file needs at least three rows")
    f = np.array([r["f"] for r in rows], dtype=int)
    t = np.array([float(r["sim_s"]) for r in rows])
    tf = np.array([float(r["film_s"]) for r in rows])
    if np.any(np.diff(t) <= 0.0):
        raise ValueError("sim_s must rise from row to row")
    loc = LIO.to_blender([r["eye"] for r in rows])
    d = LIO.to_blender([r["forward"] for r in rows])
    d = d / np.linalg.norm(d, axis=1)[:, None]
    i0, i1 = np.maximum(np.arange(n) - 1, 0), np.minimum(np.arange(n) + 1, n - 1)
    vel = (loc[i1] - loc[i0]) / np.maximum(1e-9, t[i1] - t[i0])[:, None]
    pitch = np.degrees(np.arcsin(np.clip(d[:, 2], -1.0, 1.0)))
    vfov = np.radians([float(r["vfov"]) for r in rows])
    return {"f": f, "t": t, "tf": tf, "loc": loc, "dir": d, "pitch": pitch, "vel": vel,
            "speed": np.linalg.norm(vel, axis=1), "vfov": vfov}


def _g(loc, d, port, surf):
    """Positive while the port's centre is above the surface: the lens height, plus the port's rise along the view, less the
    surface. (Path D's test, loc.z <= surface + port * sin(-pitch), is this against zero.)"""
    return loc[2] + port * d[2] - surf


def flat_crossing(st, port):
    """The first crossing against a flat sea at 0, interpolated between the two frames: (index of the first frame at or under
    the surface, the lens position at the crossing). Used to centre the sea before the moving surface is known."""
    g = np.array([_g(st["loc"][i], st["dir"][i], port, 0.0) for i in range(len(st["t"]))])
    if g[0] <= 0.0:
        raise ValueError("the pose file starts with the port under the surface")
    under = np.nonzero(g <= 0.0)[0]
    if not len(under):
        raise ValueError("the pose file never takes the port through the surface")
    i = int(under[0])
    a = g[i - 1] / (g[i - 1] - g[i])
    return i, st["loc"][i - 1] * (1 - a) + st["loc"][i] * a


def place(st, centre_xy, psi):
    """The sea-frame states: positions moved by -centre (horizontally) and every vector turned by psi about the vertical. A new
    dict; the other keys are copied."""
    R = rot_z(psi)
    c = np.array([centre_xy[0], centre_xy[1], 0.0])
    out = dict(st)
    out["loc"] = (st["loc"] - c) @ R.T
    out["dir"] = st["dir"] @ R.T
    out["vel"] = st["vel"] @ R.T
    return out


def unplace_points(X, centre_xy, psi):
    """Sea-frame points back to the pose frame (Blender axes)."""
    c = np.array([centre_xy[0], centre_xy[1], 0.0])
    return np.asarray(X, float) @ rot_z(psi) + c


def unplace_vectors(V, psi):
    """Sea-frame vectors back to the pose frame."""
    return np.asarray(V, float) @ rot_z(psi)


def find_crossing(st, port, height):
    """The surface crossing against the moving sea. `height(t, x, y)` is the surface height; the frames are searched in order
    for the first with the port's centre at or under it, and the moment is found by bisection between that frame and the one
    before, the lens position and view direction interpolated linearly. Returns the entry dict's numbers: t, frame (the nearest
    frame), loc, v, beta (the course's angle below level, radians), dir (the course, unit)."""
    n = len(st["t"])

    def at(i, j, a):
        t = st["t"][i] * (1 - a) + st["t"][j] * a
        loc = st["loc"][i] * (1 - a) + st["loc"][j] * a
        d = st["dir"][i] * (1 - a) + st["dir"][j] * a
        return t, loc, d / np.linalg.norm(d)

    gs = []
    for i in range(n):
        gs.append(_g(st["loc"][i], st["dir"][i], port, height(st["t"][i], st["loc"][i][0], st["loc"][i][1])))
        if gs[-1] <= 0.0:
            break
    if gs[0] <= 0.0:
        raise ValueError("the pose file starts with the port under the surface")
    if gs[-1] > 0.0:
        raise ValueError("the pose file never takes the port through the surface")
    i = len(gs) - 1
    lo, hi = 0.0, 1.0
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        t, loc, d = at(i - 1, i, mid)
        if _g(loc, d, port, height(t, loc[0], loc[1])) > 0.0:
            lo = mid
        else:
            hi = mid
    t_e, loc_e, _ = at(i - 1, i, hi)
    vel = st["vel"][i - 1] * (1 - hi) + st["vel"][i] * hi
    v = float(np.linalg.norm(vel))
    course = vel / v
    return {"t": float(t_e), "frame": int(np.argmin(np.abs(st["t"] - t_e))), "loc": loc_e, "v": v,
            "beta": math.atan2(-float(course[2]), math.hypot(float(course[0]), float(course[1]))), "dir": course}


def bubble_export_arrays(x0, v0, centre_xy, psi):
    """The Bubbles' sea-frame x0 and v0 (n x 3) as the export's: back to the pose frame, then to Unity axes."""
    return LIO.to_unity(unplace_points(x0, centre_xy, psi)), LIO.to_unity(unplace_vectors(v0, psi))
