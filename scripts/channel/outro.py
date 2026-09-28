"""The channel outro's plate, twenty seconds: the intro's water and bodies, calmer, arranged around
the zones YouTube's end screen will cover (endscreen_layout.json). Words are drawn by
outro_overlay.py, so the handles and the call to action can change without a re-render.

    blender -b --factory-startup --python outro.py -- <out dir> <percent> <samples> [frames]
"""
import bpy, math, os, random, sys
from mathutils import Vector, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import theatre_scene as S

argv = sys.argv[sys.argv.index("--") + 1:]
OUT_DIR, PERCENT, SAMPLES = argv[0], int(argv[1]), int(argv[2])
WHICH = argv[3] if len(argv) > 3 else "all"
FPS, N = 30, 600
os.makedirs(OUT_DIR, exist_ok=True)
rng = random.Random(11)

S.clear_scene()
sc = S.setup_render(PERCENT, SAMPLES, exposure=0.55, motion_blur=True)
sc.frame_start, sc.frame_end = 1, N

CAM_A, CAM_B = Vector((-0.18, -6.2, 0.30)), Vector((0.18, -6.0, 0.26))
AIM_A, AIM_B = Vector((-0.10, 0.0, 0.12)), Vector((0.10, 0.0, 0.10))
cam = S.make_camera(tuple((CAM_A + CAM_B) / 2), tuple((AIM_A + AIM_B) / 2), fstop=2.2)
MAT = S.body_material_for(cam)
KNUCKLE_GLOW = 2.2

# Where each body sits is given on screen, (u across, v down, metres along the view ray from the
# camera's middle pose), so the layout is read against endscreen_layout.json and not guessed in the
# world. The zones the end screen covers hold only distant, soft bodies; the near ones sit in the
# top corners beside the title and in the band between the row and the player's controls.
R = cam.rotation_euler.to_matrix()
FWD, RIGHT, UP = R @ Vector((0, 0, -1)), R @ Vector((1, 0, 0)), R @ Vector((0, 1, 0))
TV = 12.0 / cam.data.lens
TH = TV * 16.0 / 9.0

def on_screen(u, v, d):
    ray = (FWD + RIGHT * ((u - 0.5) * 2 * TH) + UP * ((0.5 - v) * 2 * TV)).normalized()
    return tuple(cam.location + ray * d)

# name, key, (u, v, distance), rotation (deg), scale, wave (amplitude deg, Hz, phase), drift (m/s), bob (m)
CAST = [
    ("c08", "08", (0.17, 0.80, 6.3), (28, -8, 22), 0.65, (16, 0.42, 0.0), (0.010, 0.0, 0.002), 0.020),
    ("c20", "20", (0.12, 0.17, 6.3), (8, 12, -18), 0.45, (14, 0.50, 1.3), (0.004, 0.0, -0.002), 0.015),
    ("c11", "11", (0.87, 0.16, 6.3), (10, 60, 35), 0.72, (18, 0.55, 2.1), (-0.006, 0.0, -0.002), 0.015),
    ("c19", "19", (0.85, 0.87, 6.8), (75, 0, 20), 0.52, (7, 0.22, 0.7), (-0.006, 0.0, 0.003), 0.025),
    ("bg0", "20", (0.03, 0.55, 20.0), (20, 40, 70), 0.9, (12, 0.45, 0.4), (0.010, 0.0, 0.0), 0.03),
    ("bg1", "11", (0.97, 0.60, 20.0), (60, 10, 20), 1.0, (15, 0.5, 2.6), (-0.010, 0.0, 0.01), 0.03),
    ("bg2", "08", (0.33, 0.05, 22.0), (50, 20, 140), 1.2, (14, 0.4, 1.9), (0.008, 0.0, -0.005), 0.03),
    ("bg3", "19", (0.64, 0.97, 22.0), (70, 0, 10), 1.1, (6, 0.2, 3.3), (0.0, 0.0, 0.0), 0.04),
]
bodies = []
for name, key, (u, v, d), rot, s, wave, drift, bob in CAST:
    loc = on_screen(u, v, d)
    root, joints = S.build(key, name, loc, rot, s, MAT, KNUCKLE_GLOW)
    for j in joints:
        j["jitter"] = rng.uniform(-0.25, 0.25)
    bodies.append({"root": root, "joints": joints, "loc": Vector(loc), "rot": [math.radians(a) for a in rot],
                   "wave": wave, "drift": Vector(drift), "bob": bob, "seed": rng.uniform(0, 2 * math.pi)})

S.water_world(-24.0)   # the intro looks down into deeper water; a level camera needs the depth set to match
snow = S.marine_snow(rng, count=1100, strength=1.2, box=((-5, 5), (-3.5, 13), (-4, 4)))
S.haze_and_shafts()
S.grade()

def smoother(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)

for f in range(1, N + 1):
    t = (f - 1) / FPS
    u = smoother((f - 1) / (N - 1))
    for b in bodies:
        A, hz, ph = b["wave"]
        w = 2 * math.pi * hz
        r = b["root"]
        r.location = b["loc"] + b["drift"] * t + Vector((0, 0, b["bob"] * math.sin(w * t + ph - math.pi / 2)))
        r.rotation_euler = (b["rot"][0] + math.radians(2.0) * math.sin(0.35 * t + b["seed"]),
                            b["rot"][1] + math.radians(1.5) * math.sin(0.27 * t + 2 * b["seed"]),
                            b["rot"][2] + math.radians(4.0) * math.sin(0.12 * t + 3 * b["seed"]))
        r.keyframe_insert("location", frame=f)
        r.keyframe_insert("rotation_euler", frame=f)
        for j in b["joints"]:
            ang = math.radians(A) * math.sin(w * t + ph - 0.9 * j["depth"] + j["jitter"])
            j["pivot"].rotation_quaternion = Quaternion(Vector(j["axis"]), ang)
            j["pivot"].keyframe_insert("rotation_quaternion", frame=f)
    cam.location = CAM_A.lerp(CAM_B, u)
    aim = AIM_A.lerp(AIM_B, u)
    cam.rotation_euler = (aim - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.keyframe_insert("location", frame=f)
    cam.keyframe_insert("rotation_euler", frame=f)
    cam.data.dof.focus_distance = 6.3
    cam.data.dof.keyframe_insert("focus_distance", frame=f)
    snow.location = (0.012 * t, 0.0, -0.03 * t)
    snow.keyframe_insert("location", frame=f)

sc.render.image_settings.file_format = "PNG"
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "outro.blend"))
frames = list(range(1, N + 1)) if WHICH == "all" else [int(x) for x in WHICH.split(",")]
for f in frames:
    sc.frame_set(f)
    sc.render.filepath = os.path.join(OUT_DIR, "frame_%04d.png" % f)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", f, flush=True)
print("DONE")