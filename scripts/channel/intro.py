"""The channel intro, eight seconds (five to the full title, three held for a fade in the edit): the v2 look set moving. Renders the 3D plate only; the title,
the tagline and the corner readout are drawn over it by overlay.py, so the words can change
without a re-render.

    blender -b --factory-startup --python intro.py -- <out dir> <percent> <samples> [frames]

frames: "all" (the default) renders 1..240 as frame_NNNN.png; a list like "1,40,150" renders
those alone. The motion is choreography, not the creatures' brains: a travelling wave through
each body's real joints, a slow drift and bob. The camera pushes in and racks focus from the
plant behind to the hero.
"""
import bpy, math, os, random, sys
from mathutils import Vector, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import theatre_scene as S

argv = sys.argv[sys.argv.index("--") + 1:]
OUT_DIR, PERCENT, SAMPLES = argv[0], int(argv[1]), int(argv[2])
WHICH = argv[3] if len(argv) > 3 else "all"
FPS, N = 30, 240
os.makedirs(OUT_DIR, exist_ok=True)
rng = random.Random(7)

S.clear_scene()
sc = S.setup_render(PERCENT, SAMPLES, exposure=0.55, motion_blur=True)
sc.frame_start, sc.frame_end = 1, N

# The lights are the v2 still's: aimed from its camera, then the camera moves.
cam = S.make_camera((0.0, -5.2, 0.9), (0.0, 0.0, 0.0), fstop=1.6)
MAT = S.body_material_for(cam)
KNUCKLE_GLOW = 2.2

# name, key, location, rotation (deg), scale, wave (amplitude deg, Hz, phase), drift (m/s), bob (m)
CAST = [
    ("c08", "08", (-0.85, 0.0, 0.05), (28, -8, 22), 1.35, (16, 0.42, 0.0), (0.030, 0.010, 0.008), 0.030),
    ("c20", "20", (1.35, 1.0, 0.35), (8, 12, -18), 0.72, (14, 0.50, 1.3), (-0.020, 0.0, 0.012), 0.025),
    ("c11", "11", (1.08, -0.85, -0.80), (10, 60, 35), 0.62, (18, 0.55, 2.1), (0.025, 0.0, 0.010), 0.020),
    ("c19", "19", (-0.9, 4.0, 1.1), (75, 0, 20), 1.1, (7, 0.22, 0.7), (0.0, 0.0, 0.006), 0.040),
    ("bg0", "20", (2.8, 7.5, -0.6), (20, 40, 70), 0.9, (12, 0.45, 0.4), (-0.015, 0.0, 0.0), 0.03),
    ("bg1", "11", (-3.2, 9.0, -0.9), (60, 10, 20), 1.0, (15, 0.5, 2.6), (0.02, 0.0, 0.01), 0.03),
    ("bg2", "08", (0.6, 11.0, 1.8), (50, 20, 140), 1.2, (14, 0.4, 1.9), (0.0, 0.0, -0.01), 0.03),
    ("bg3", "19", (3.6, 12.0, 1.4), (70, 0, 10), 1.3, (6, 0.2, 3.3), (0.0, 0.0, 0.0), 0.04),
]
bodies = []
for name, key, loc, rot, s, wave, drift, bob in CAST:
    root, joints = S.build(key, name, loc, rot, s, MAT, KNUCKLE_GLOW)
    for j in joints:
        j["jitter"] = rng.uniform(-0.25, 0.25)
    bodies.append({"root": root, "joints": joints, "loc": Vector(loc), "rot": [math.radians(a) for a in rot],
                   "wave": wave, "drift": Vector(drift), "bob": bob, "seed": rng.uniform(0, 2 * math.pi)})

S.water_world(-15.0 + 0.9)
snow = S.marine_snow(rng, count=900, strength=1.2)
S.haze_and_shafts()
S.grade()

def ease(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)

def smoother(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)

CAM_A, CAM_B = Vector((0.30, -5.75, 1.05)), Vector((-0.08, -4.95, 0.86))
AIM_A, AIM_B = Vector((0.05, 0.6, 0.10)), Vector((-0.02, 0.0, 0.0))
FOCUS_FAR = "c19"
RACK = (1, 42)

def hero_point(t):
    b = bodies[0]
    return b["loc"] + b["drift"] * t

def far_point(t):
    b = next(x for x in bodies if x["root"].name == FOCUS_FAR)
    return b["loc"] + b["drift"] * t

for f in range(1, N + 1):
    t = (f - 1) / FPS
    u = smoother((f - 1) / (N - 1))
    # bodies: drift, bob, a slow yaw and roll, and the wave through the joints
    for b in bodies:
        A, hz, ph = b["wave"]
        w = 2 * math.pi * hz
        r = b["root"]
        r.location = b["loc"] + b["drift"] * t + Vector((0, 0, b["bob"] * math.sin(w * t + ph - math.pi / 2)))
        r.rotation_euler = (b["rot"][0] + math.radians(2.0) * math.sin(0.35 * t + b["seed"]),
                            b["rot"][1] + math.radians(1.5) * math.sin(0.27 * t + 2 * b["seed"]),
                            b["rot"][2] + math.radians(3.0) * math.sin(0.19 * t + 3 * b["seed"]))
        r.keyframe_insert("location", frame=f)
        r.keyframe_insert("rotation_euler", frame=f)
        for j in b["joints"]:
            ang = math.radians(A) * math.sin(w * t + ph - 0.9 * j["depth"] + j["jitter"])
            j["pivot"].rotation_quaternion = Quaternion(Vector(j["axis"]), ang)
            j["pivot"].keyframe_insert("rotation_quaternion", frame=f)
    # camera: a push in, and a rack from the plant behind to the hero
    cam.location = CAM_A.lerp(CAM_B, u)
    aim = AIM_A.lerp(AIM_B, u)
    cam.rotation_euler = (aim - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.keyframe_insert("location", frame=f)
    cam.keyframe_insert("rotation_euler", frame=f)
    k = smoother((f - RACK[0]) / (RACK[1] - RACK[0]))
    d_far = (far_point(t) - cam.location).length
    d_near = (hero_point(t) - cam.location).length
    cam.data.dof.focus_distance = d_far + (d_near - d_far) * k
    cam.data.dof.keyframe_insert("focus_distance", frame=f)
    cam.data.dof.aperture_fstop = 1.1 + 0.5 * smoother((f - 1) / 50.0)
    cam.data.dof.keyframe_insert("aperture_fstop", frame=f)
    # the snow sinks and drifts
    snow.location = (0.015 * t, 0.0, -0.035 * t)
    snow.keyframe_insert("location", frame=f)

from bpy_extras.object_utils import world_to_camera_view
import json
track = {}
hero = bodies[0]
for f in range(1, N + 1):
    sc.frame_set(f)
    pts = {"root": list(world_to_camera_view(sc, cam, hero["root"].matrix_world.translation))}
    for j in hero["joints"]:
        pts[j["pivot"].name] = list(world_to_camera_view(sc, cam, j["pivot"].matrix_world.translation))
    track[f] = pts
json.dump(track, open(os.path.join(OUT_DIR, "track.json"), "w"))
sc.render.image_settings.file_format = "PNG"
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "intro.blend"))
frames = list(range(1, N + 1)) if WHICH == "all" else [int(x) for x in WHICH.split(",")]
if os.environ.get("EVOSIM_PROFILE"):
    import time
    T0 = [time.time()]
    last = [None]
    def stamp(tag):
        print("PROF %7.2f %s" % (time.time() - T0[0], tag), flush=True)
    def on_stats(st):
        key = st.split("|")[-1].strip()[:60]
        if key != last[0]:
            last[0] = key
            stamp("stats " + key)
    bpy.app.handlers.render_stats.append(on_stats)
    bpy.app.handlers.render_pre.append(lambda *a: stamp("render_pre"))
    bpy.app.handlers.render_post.append(lambda *a: stamp("render_post"))
    bpy.app.handlers.render_write.append(lambda *a: stamp("render_write"))
    print("PROF denoiser", sc.cycles.denoiser, "use_gpu", getattr(sc.cycles, "denoising_use_gpu", "?"),
          "compositor_device", getattr(sc.render, "compositor_device", "?"),
          "persistent", sc.render.use_persistent_data, "threads", sc.render.threads, flush=True)
for f in frames:
    sc.frame_set(f)
    sc.render.filepath = os.path.join(OUT_DIR, "frame_%04d.png" % f)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", f, flush=True)
print("DONE")
