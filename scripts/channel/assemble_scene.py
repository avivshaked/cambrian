"""Assemble theatre_scene.py: the v2 look's body code with a joint rig, and its scene set-up."""
import sys
h = sys.argv[1]
body = open(h + "/theatre_scene_body.txt", encoding="utf-8").read()

# make_object: vertices relative to the pivot the object hangs from
a = "    Pb, Nb = to_blender(P), to_blender(N)\n"
assert body.count(a) == 1
body = body.replace("def make_object(name, P, N, faces, attrs, tone, props, material, parent):",
                    "def make_object(name, P, N, faces, attrs, tone, props, material, parent, offset=None):")
body = body.replace("    me.from_pydata(Pb.tolist(), [], f.tolist())\n",
                    "    if offset is not None:\n        Pb = Pb - np.asarray(offset)\n    me.from_pydata(Pb.tolist(), [], f.tolist())\n")

# build: replace wholesale with the rigged version
i = body.index("def build(key, name, location, rot_deg, scale, material, knuckle_glow):")
body = body[:i] + '''def build(key, name, location, rot_deg, scale, material, knuckle_glow):
    """One body, rigged: a pivot at each part's joint anchor, chained parent to child, so a
    rotation of a pivot turns that part and everything it carries about the joint."""
    body = BODIES[key]
    parts = body["parts"]
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    bseed, bown = body_seeds(body["id"])
    children = {i: [] for i in range(len(parts))}
    for p in parts:
        if p["parent"] >= 0:
            assert p["parent"] < p["i"], "parents come first in the developer's order"
            children[p["parent"]].append(p)
    centre = np.mean([p["pos"] for p in parts], axis=0)
    pivot_at, pivots = {}, {}
    for p in parts:
        a = np.asarray(p["anchor"] if p["parent"] >= 0 else p["pos"], float) - centre
        pivot_at[p["i"]] = to_blender(a[None, :])[0]
    for p in parts:
        e = bpy.data.objects.new("%s-joint%d" % (name, p["i"]), None)
        bpy.context.scene.collection.objects.link(e)
        e.empty_display_size = 0.02
        if p["parent"] >= 0:
            e.parent = pivots[p["parent"]]
            e.location = tuple(pivot_at[p["i"]] - pivot_at[p["parent"]])
        else:
            e.parent = root
            e.location = tuple(pivot_at[p["i"]])
        e.rotation_mode = "QUATERNION"
        pivots[p["i"]] = e

    def place(part, P, N):
        Pw = np.asarray(part["pos"], float) + qrot(part["rot"], P) - centre
        Nw = qrot(part["rot"], N)
        return Pw, Nw

    def props_for(cell, rim_scale, individual, brightness=1.0, emit=0.0):
        rim, through, bodyc = COLOURS[guild(cell)]
        return {"base": lin(tuple(c * brightness for c in bodyc)), "rim": lin(tuple(c * brightness for c in rim)),
                "trans_tint": lin(through), "trans_gain": TRANS_GAIN.get(cell, 0.4), "sheen": SHEEN.get(cell, 0.08),
                "reserve": 1.0, "rim_scale": rim_scale, "emit": emit,
                "shade": frac(individual * 17.37 + 0.41) * 2 - 1, "warmth": frac(individual * 31.91 + 0.07) * 2 - 1}

    for i, part in enumerate(parts):
        seed = frac(bseed + 0.61803399 * i)
        own = frac(bown + 0.38196601 * i)
        for vi, (kind, lp, sc) in enumerate(visuals_of(part)):
            cands = []
            if part["parent"] >= 0:
                cands.append(np.asarray(part["canchor"], float))
            for ch in children[i]:
                cands.append(np.asarray(ch["panchor"], float))
            here = [(c - lp) / sc for c in cands]
            here.sort(key=lambda x: float(np.linalg.norm(x)))
            pinches = [tuple(x) + (PINCH_REACH,) for x in here[:2]]
            leaf_anchor = (tuple((np.asarray(part["canchor"], float) - lp) / sc) + (1.0,)) if part["parent"] >= 0 else (0, 0, 0, 0)
            P, N, faces, attrs, tone = visual_geometry(kind, lp, sc, part, part["cell"], seed, own, pinches, leaf_anchor, CARVE_FRACTION)
            Pw, Nw = place(part, P, N)
            make_object("%s-p%d-%s%d" % (name, i, kind, vi), Pw, Nw, faces, attrs, tone,
                        props_for(part["cell"], 0.45 if kind == "lamina" else 1.0, own), material,
                        pivots[i], pivot_at[i])
        if part["parent"] >= 0 and part["joint"] != "Fixed":
            parent = parts[part["parent"]]
            for side, (own_p, oth_p, oa, ta) in enumerate(((part, parent, part["canchor"], part["panchor"]),
                                                          (parent, part, part["panchor"], part["canchor"]))):
                k = knuckle(own_p, oth_p, oa, ta)
                if k is None:
                    continue
                lp, sc = k
                oi = own_p["i"]
                kseed = frac(bseed + 0.61803399 * oi + 0.5)
                kown = frac(bown + 0.38196601 * oi)
                P, N, faces, attrs, tone = visual_geometry("sphere", lp, sc, own_p, own_p["cell"], kseed, kown, [], (0, 0, 0, 0), 0.5 * CARVE_FRACTION)
                Pw, Nw = place(own_p, P, N)
                make_object("%s-k%d-%d" % (name, i, side), Pw, Nw, faces, attrs, tone,
                            props_for(own_p["cell"], 1.0, kown, 0.85, knuckle_glow), material,
                            pivots[oi], pivot_at[oi])
    joints = []
    for p in parts:
        if p["parent"] >= 0 and p["joint"] != "Fixed":
            d = to_blender((np.asarray(p["pos"], float) - np.asarray(p["anchor"], float))[None, :])[0]
            ax = np.cross(d, [0.0, 0.0, 1.0])
            if np.linalg.norm(ax) < 0.3 * np.linalg.norm(d):
                ax = np.cross(d, [1.0, 0.0, 0.0])
            ax = ax / max(1e-9, np.linalg.norm(ax))
            joints.append({"pivot": pivots[p["i"]], "axis": ax, "depth": p["depth"], "joint": p["joint"]})
    root.rotation_euler = [math.radians(a) for a in rot_deg]
    root.location = location
    root.scale = (scale, scale, scale)
    return root, joints
'''

head = '''"""The theatre's creature skin in Blender, as a module: bodies (rigged at their joints), the
water, the snow, the lights and the grade of the intro look ("v2", which the owner chose on
2026-09-28). look_theatre_v2.py is the still this was taken from; intro.py animates it.

Each part is built the way the theatre builds it (TheatreMeshes, TheatrePalette,
PhenotypeBuilder, as read on the round-51-tests worker): a box drawn as a pillowed cube that the
shader tapers and bends, or as a kelp blade when it is a leaf or flat; a sphere squashed by the
genome's proportions; a capsule as a shaft and two caps, or one ellipsoid when short; a knuckle
on each side of every moving joint; every part carved inward, deeper at a joint. The vertex
stage runs in numpy (theatre_shape.py), the fragment stage as a node material
(theatre_material.py). Core's frame is Unity's, Y up; a point (x, y, z) is drawn at (x, z, y).
"""
import bpy, bmesh, json, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import theatre_shape as ts
import theatre_material as tm

BODIES = json.load(open(os.path.join(HERE, "bodies.json")))

'''

tail = '''
# ---------------------------------------------------------------- the scene around the bodies
DEEP, SHALLOW, REACH = lin((0.012, 0.032, 0.048)), lin((0.09, 0.26, 0.32)), 18.0

def water(y):
    t = math.exp(-max(0.0, -y) / REACH)
    return tuple(d + (s - d) * t for d, s in zip(DEEP, SHALLOW))

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

def setup_render(percent, samples, exposure=0.55, motion_blur=False):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4
    sc.cycles.volume_bounces = 1
    sc.render.threads_mode = "FIXED"
    sc.render.threads = 4
    sc.render.resolution_x, sc.render.resolution_y = 3840, 2160
    sc.render.resolution_percentage = percent
    sc.render.fps = 30
    sc.render.use_motion_blur = motion_blur
    sc.render.motion_blur_shutter = 0.5
    for vt in ("Khronos PBR Neutral", "Standard"):
        try:
            sc.view_settings.view_transform = vt
            break
        except Exception:
            pass
    sc.view_settings.exposure = exposure
    return sc

def make_camera(location, target, fstop=1.6, lens=59.3):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("cam")
    cd.sensor_fit = "VERTICAL"
    cd.sensor_height = 24.0
    cd.lens = lens
    cd.dof.use_dof = True
    cd.dof.aperture_fstop = fstop
    cam = bpy.data.objects.new("cam", cd)
    cam.location = location
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam

def body_material_for(cam, eye_depth=-15.0, key_gain=0.4, back_gain=3.6, rim_strength=2.3, glow_strength=0.35):
    """The theatre's key, fill and portrait back light, aimed from this camera as the theatre aims
    them from its own (TheatreSkin.Aim); the intro's dark field is the key at 0.4 and the back
    light at 1.5 times the theatre's."""
    R = cam.rotation_euler.to_matrix()
    fwd = (R @ Vector((0, 0, -1))).normalized()
    key_dir = (R @ Vector((0.345, 0.616, 0.708))).normalized()
    fill_dir = (R @ Vector((-0.202, -0.242, -0.949))).normalized()
    back_dir = (fwd + Vector((0, 0, 0.45))).normalized()
    key_col = tuple(c * 3.0 for c in lin((0.86, 0.94, 1.0)))
    fill_col = tuple(c * 0.8 for c in lin((0.30, 0.52, 0.68)))
    back_col = tuple(c * back_gain for c in lin((0.55, 0.85, 1.0)))
    ambient = (lin((0.081, 0.234, 0.288)), lin((0.020, 0.038, 0.046)), lin((0.012, 0.032, 0.048)))
    return tm.body_material("theatre-body", tuple(key_dir), key_col,
                            [(tuple(fill_dir), fill_col), (tuple(back_dir), back_col)], ambient,
                            water(eye_depth), 0.035, rim_strength=rim_strength,
                            glow_strength=glow_strength, key_gain=key_gain)

def water_world(eye_y, gain=0.9):
    sc = bpy.context.scene
    world = bpy.data.worlds.new("water")
    sc.world = world
    nt = world.node_tree
    nt.nodes.clear()
    tcw = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tcw.outputs["Generated"], sep.inputs[0])
    def m(op, a, b=None):
        n = nt.nodes.new("ShaderNodeMath"); n.operation = op
        for i, x in enumerate((a, b)):
            if x is None: continue
            if isinstance(x, (int, float)): n.inputs[i].default_value = x
            else: nt.links.new(x, n.inputs[i])
        return n.outputs[0]
    y = m("ADD", m("MULTIPLY", sep.outputs[2], 3.0 * REACH), eye_y)
    tw = m("EXPONENT", m("DIVIDE", m("MAXIMUM", m("MULTIPLY", y, -1.0), 0.0), -REACH))
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"
    nt.links.new(tw, mix.inputs["Factor"])
    mix.inputs["A"].default_value = (*DEEP, 1)
    mix.inputs["B"].default_value = (*[c * gain for c in SHALLOW], 1)
    bgn = nt.nodes.new("ShaderNodeBackground")
    nt.links.new(mix.outputs["Result"], bgn.inputs["Color"])
    wo = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(bgn.outputs[0], wo.inputs["Surface"])

def marine_snow(rng, count=900, strength=1.2, box=((-5, 5), (-3.2, 13), (-3, 3))):
    sc = bpy.context.scene
    snow_m = bpy.data.materials.new("snow")
    snt = snow_m.node_tree; snt.nodes.clear()
    e = snt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = (0.55, 0.95, 0.85, 1)
    e.inputs["Strength"].default_value = strength
    so = snt.nodes.new("ShaderNodeOutputMaterial"); snt.links.new(e.outputs[0], so.inputs["Surface"])
    bm = bmesh.new()
    for i in range(count):
        r = rng.uniform(0.002, 0.007)
        mt = Matrix.Translation((rng.uniform(*box[0]), rng.uniform(*box[1]), rng.uniform(*box[2]))) @ Matrix.Scale(r, 4)
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0, matrix=mt)
    me = bpy.data.meshes.new("snow"); bm.to_mesh(me); bm.free()
    snow = bpy.data.objects.new("snow", me); snow.data.materials.append(snow_m); sc.collection.objects.link(snow)
    return snow

def haze_and_shafts():
    sc = bpy.context.scene
    bpy.ops.mesh.primitive_cube_add(size=40, location=(0, 5, 0))
    vol = bpy.context.active_object
    vol.name = "haze"
    vm = bpy.data.materials.new("haze"); vnt = vm.node_tree; vnt.nodes.clear()
    pv = vnt.nodes.new("ShaderNodeVolumePrincipled")
    pv.inputs["Color"].default_value = (0.3, 0.75, 0.7, 1)
    pv.inputs["Density"].default_value = 0.004
    pv.inputs["Anisotropy"].default_value = 0.6
    vo = vnt.nodes.new("ShaderNodeOutputMaterial"); vnt.links.new(pv.outputs[0], vo.inputs["Volume"])
    vol.data.materials.append(vm)
    for i, x in enumerate((-2.6, -0.4, 1.8, 3.6)):
        ld = bpy.data.lights.new("shaft%d" % i, "SPOT")
        ld.energy = 30000; ld.spot_size = math.radians(4 + 1.5 * i); ld.spot_blend = 0.35
        ld.color = (0.6, 0.95, 0.9); ld.shadow_soft_size = 0.2
        lo = bpy.data.objects.new(ld.name, ld)
        lo.location = (x, 3.0 + 2.0 * i, 9.0)
        lo.rotation_euler = (Vector((x * 0.6 - 0.8, 2.0 + i, -3.0)) - Vector(lo.location)).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)

def grade(threshold=0.6, strength=0.8, size=0.7, vignette=0.35):
    sc = bpy.context.scene
    ng = bpy.data.node_groups.new("comp", "CompositorNodeTree")
    ng.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    sc.compositing_node_group = ng
    rl = ng.nodes.new("CompositorNodeRLayers")
    gl = ng.nodes.new("CompositorNodeGlare")
    go = ng.nodes.new("NodeGroupOutput")
    for i in gl.inputs:
        if i.name == "Type": i.default_value = "Bloom"
        elif i.name == "Threshold": i.default_value = threshold
        elif i.name == "Strength": i.default_value = strength
        elif i.name == "Size": i.default_value = size
    ng.links.new(rl.outputs["Image"], gl.inputs["Image"])
    ell = ng.nodes.new("CompositorNodeEllipseMask")
    for i in ell.inputs:
        if i.name == "Size": i.default_value = (0.8, 0.8)
        elif i.name == "Value": i.default_value = 1.0
    blur = ng.nodes.new("CompositorNodeBlur")
    for i in blur.inputs:
        if i.name == "Size":
            try: i.default_value = (500, 500)
            except Exception: i.default_value = 500
    ng.links.new(ell.outputs[0], blur.inputs["Image"])
    mul = ng.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = vignette
    ng.links.new(gl.outputs["Image"], mul.inputs["A"])
    ng.links.new(blur.outputs[0], mul.inputs["B"])
    ng.links.new(mul.outputs["Result"], go.inputs[0])
'''
open(h + "/theatre_scene.py", "w", encoding="utf-8", newline="\n").write(head + body + tail)
print("theatre_scene.py written,", (head + body + tail).count("\n"), "lines")
