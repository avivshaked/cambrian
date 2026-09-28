"""What a story film's graphic scene imports (graphics.md), and the script Blender runs to render it.

A scene file is graphics/scenes/<id>.py in the film's folder. It lists DRAWS, every word and number it draws
with the places in facts.md that hold it, and defines graphic(clock), which draws the shot and times it:

    import storyblender as sg

    DRAWS = [("One child of body 201, J", "[facts.md:L40]"), ("11.7", "[facts.md:L41]")]

    def graphic(clock):
        left, top, right, bottom = sg.box("corner")
        title = sg.text("One child of body 201, J", "sans", 32, left, top, "lt")
        sg.fade_in([sg.card("corner"), title], clock.beat(1))
        bar = sg.bar(left, top + 120, 234, 30, "ink")
        sg.grow(bar, clock.beat(2))

graphics.py render runs it headless, with this file as Blender's script:

    blender -b --factory-startup --python-exit-code 1 --python storyblender.py --
        <plan.json> <id> <film folder> <scene file> <frames dir> <width> <height> <threads>

Places and sizes are pixels of a 1920x1080 frame from its top left, as the style file writes them, whatever
the render's size. Times are seconds from the shot's start. Each thing drawn lies over everything drawn before
it, except the dim and the cards, which always lie beneath everything else, whenever they are made. Nothing
shows until its fade_in when it has one; everything else shows from the first frame.

Blender blends see-through layers in linear light, and its Standard view writes a style colour back unchanged.
It sizes a font by the font's bounding box, so text() converts a size in pixels to the em.
"""

import json
import math
import runpy
import struct
import sys
from pathlib import Path

import bmesh
import bpy

REF_W, REF_H = 1920, 1080
UNIT = 100                          # pixels of the 1920x1080 frame to a Blender unit
HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
GROW_S = 1.0                        # a bar's growth from its baseline
DRAW_S = 1.5                        # a line drawn from its first point to its last

SHOT, STYLE, FOLDER, FPS = None, None, None, 30
_state = {"layer": 0, "spans": [], "fonts": {}}


class SceneError(Exception):
    """A scene that asks for something the shot or the look does not allow."""


def _linear(hex_colour):
    """An sRGB colour as Blender's scene-linear RGBA, which the Standard view writes back unchanged."""
    h = hex_colour.lstrip("#")
    if len(h) != 6:
        raise SceneError(f"{hex_colour} is not a colour written #RRGGBB")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (*out, 1.0)


def ink(name):
    """A colour of the style by its name (ink_hi, ink, leaf, stomach...), or a colour written #RRGGBB."""
    if name.startswith("#"):
        return name
    if name not in STYLE["colours"]:
        raise SceneError(f"the style has no colour {name}; it has " + ", ".join(STYLE["colours"]))
    return STYLE["colours"][name]


def opacity(name):
    """An opacity of the style by its name: rule, mark, plate_corner, plate_full, dim_full."""
    if name not in STYLE["opacity"]:
        raise SceneError(f"the style has no opacity {name}; it has " + ", ".join(STYLE["opacity"]))
    return STYLE["opacity"][name]


def box(kind="full"):
    """A card's inside after its padding, as (left, top, right, bottom) in pixels of the frame."""
    c = STYLE["cards"][kind]
    return c["x"] + c["pad"], c["y"] + c["pad"], c["x"] + c["w"] - c["pad"], c["y"] + c["h"] - c["pad"]


def frame(t):
    """The frame a time falls on: frame 1 is the shot's start."""
    return 1 + round(t * FPS)


def _span(start, end, what):
    _state["spans"].append((start, end, what))


def _material(colour, alpha, image=None):
    """Flat colour (or an image) whose opacity is one keyframable value, named opacity."""
    m = bpy.data.materials.new("m")
    try:
        m.use_nodes = True
    except (AttributeError, TypeError):
        pass
    m.surface_render_method = "BLENDED"
    m.use_backface_culling = True
    for flag in ("use_transparency_overlap", "show_transparent_back"):
        if hasattr(m, flag):
            setattr(m, flag, False)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    value = nt.nodes.new("ShaderNodeValue")
    value.name = "opacity"
    value.outputs[0].default_value = alpha
    if image is None:
        em.inputs["Color"].default_value = _linear(colour)
        nt.links.new(value.outputs[0], mix.inputs["Fac"])
    else:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Cubic"
        image.alpha_mode = "STRAIGHT"
        times = nt.nodes.new("ShaderNodeMath")
        times.operation = "MULTIPLY"
        nt.links.new(tex.outputs["Color"], em.inputs["Color"])
        nt.links.new(tex.outputs["Alpha"], times.inputs[0])
        nt.links.new(value.outputs[0], times.inputs[1])
        nt.links.new(times.outputs[0], mix.inputs["Fac"])
    em.inputs["Strength"].default_value = 1.0
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    return m


def _place(ob, x, y, alpha, layer=None):
    """Put an object at a place of the frame, over everything drawn before it unless it is given a layer."""
    if layer is None:
        _state["layer"] += 1
        layer = _state["layer"]
    ob.location = ((x - REF_W / 2) / UNIT, (REF_H / 2 - y) / UNIT, layer * 0.001)
    ob["opacity"] = alpha
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _anchored(w, h, anchor):
    """The top left of a w x h box whose anchor (l/m/r then t/m/b) sits at the origin, in pixels, y down."""
    if len(anchor) != 2 or anchor[0] not in "lmr" or anchor[1] not in "tmb":
        raise SceneError(f"anchor {anchor!r} is two letters: l, m or r, then t, m or b")
    return -{"l": 0, "m": w / 2, "r": w}[anchor[0]], -{"t": 0, "m": h / 2, "b": h}[anchor[1]]


def _shape(outline, colour, alpha, x, y, image=None, uvs=None, layer=None):
    """A flat shape from its outline, in pixels from its origin with y down, facing the camera."""
    me = bpy.data.meshes.new("shape")
    bm = bmesh.new()
    verts = [bm.verts.new((px / UNIT, -py / UNIT, 0.0)) for px, py in outline]
    face = bm.faces.new(verts)
    bm.normal_update()
    if face.normal.z < 0:
        face.normal_flip()
    if uvs is not None:
        uv_layer = bm.loops.layers.uv.new("uv")
        at = {v: uv for v, uv in zip(verts, uvs)}
        for loop in face.loops:
            loop[uv_layer].uv = at[loop.vert]
    bm.to_mesh(me)
    bm.free()
    me.materials.append(_material(ink(colour) if image is None else "#FFFFFF", alpha, image))
    return _place(bpy.data.objects.new("shape", me), x, y, alpha, layer)


def rect(x, y, w, h, colour="ink", alpha=1.0, anchor="lt", layer=None):
    """A rectangle w x h pixels whose anchor sits at (x, y): a rule, a tick, a swatch."""
    x0, y0 = _anchored(w, h, anchor)
    return _shape([(x0, y0), (x0, y0 + h), (x0 + w, y0 + h), (x0 + w, y0)], colour, alpha, x, y, layer=layer)


def _rounded(w, h, r, steps=10):
    pts = []
    for cx, cy, a0 in ((w - r, r, -90), (w - r, h - r, 0), (r, h - r, 90), (r, r, 180)):
        for i in range(steps + 1):
            a = math.radians(a0 + 90 * i / steps)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def card(kind="full"):
    """The plate a chart sits on: round 48's rounded rectangle, at its place and opacity, beneath everything
    but the dim."""
    c = STYLE["cards"][kind]
    return _shape(_rounded(c["w"], c["h"], STYLE["cards"]["radius"]), "plate", opacity(f"plate_{kind}"), c["x"],
                  c["y"], layer=-2)


def dim():
    """The whole picture darkened under a full card, as round 48 did, beneath everything else."""
    return rect(-20, -20, REF_W + 40, REF_H + 40, "#000000", opacity("dim_full"), layer=-3)


def bar(x, y, length, thickness, colour="ink", alpha=1.0, direction="right"):
    """A bar from its baseline at (x, y): across to the right, centred on y, or up, centred on x."""
    if direction == "right":
        ob = rect(x, y, length, thickness, colour, alpha, "lm")
    elif direction == "up":
        ob = rect(x, y, thickness, length, colour, alpha, "mb")
    else:
        raise SceneError(f"a bar runs right or up, not {direction}")
    ob["direction"] = direction
    return ob


def line(points, width=4, colour="ink", alpha=1.0):
    """A line through points (x, y) in pixels, `width` pixels wide: a series, an axis drawn in, a mark."""
    if len(points) < 2:
        raise SceneError("a line needs two points or more")
    ox, oy = points[0]
    cu = bpy.data.curves.new("line", "CURVE")
    cu.dimensions = "3D"
    spline = cu.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for p, (px, py) in zip(spline.points, points):
        p.co = ((px - ox) / UNIT, -(py - oy) / UNIT, 0.0, 1.0)
    cu.bevel_depth = width / 2 / UNIT
    cu.bevel_resolution = 3
    cu.use_fill_caps = True
    cu.bevel_factor_mapping_end = "SPLINE"
    cu.materials.append(_material(ink(colour), alpha))
    return _place(bpy.data.objects.new("line", cu), ox, oy, alpha)


def _font(face):
    """The face's Blender font, and its bounding box in ems (head yMax - yMin), which Blender sizes it by."""
    if face not in STYLE["fonts"]:
        raise SceneError(f"the style has no face {face}; it has " + ", ".join(STYLE["fonts"]))
    if face not in _state["fonts"]:
        path = REPO / STYLE["fonts"][face]
        data = path.read_bytes()
        box_em = None
        for i in range(struct.unpack(">H", data[4:6])[0]):
            tag, _, offset, _ = struct.unpack(">4sIII", data[12 + 16 * i:28 + 16 * i])
            if tag == b"head":
                upem = struct.unpack(">H", data[offset + 18:offset + 20])[0]
                y_min = struct.unpack(">h", data[offset + 38:offset + 40])[0]
                y_max = struct.unpack(">h", data[offset + 42:offset + 44])[0]
                box_em = (y_max - y_min) / upem
        if box_em is None:
            raise SceneError(f"the font {path.name} has no head table")
        _state["fonts"][face] = (bpy.data.fonts.load(str(path), check_existing=True), box_em)
    return _state["fonts"][face]


def text(words, face="sans", size=26, x=0, y=0, anchor="ls", colour="ink_hi", alpha=1.0):
    """Words in one of the style's faces, `size` pixels to the em at 1080, as round 48's charts sized them.

    The anchor is l, m or r, then t (the top), m (the middle), s (the baseline) or b (the bottom)."""
    if len(anchor) != 2 or anchor[0] not in "lmr" or anchor[1] not in "tmsb":
        raise SceneError(f"a text's anchor {anchor!r} is l, m or r, then t, m, s or b")
    font, box_em = _font(face)
    cu = bpy.data.curves.new("text", "FONT")
    cu.body = words
    cu.font = font
    cu.size = size * box_em / UNIT
    cu.align_x = {"l": "LEFT", "m": "CENTER", "r": "RIGHT"}[anchor[0]]
    cu.align_y = {"t": "TOP", "m": "CENTER", "s": "TOP_BASELINE", "b": "BOTTOM"}[anchor[1]]
    cu.resolution_u = 16
    cu.materials.append(_material(ink(colour), alpha))
    return _place(bpy.data.objects.new("text", cu), x, y, alpha)


def size_of(ob):
    """What something drawn measures, (width, height) in pixels: to set a word beside another."""
    bpy.context.view_layer.update()
    return ob.dimensions.x * UNIT, ob.dimensions.y * UNIT


def still(name, height_px, x, y, anchor="mm"):
    """A still of a real body, drawn by the theatre on a clear background, `height_px` tall at 1080."""
    if name not in SHOT["stills"]:
        raise SceneError(f"graphic {SHOT['id']} has no still {name}; its stills are {SHOT['stills']}")
    image = bpy.data.images.load(str(FOLDER / "graphics" / "stills" / f"{name}.png"), check_existing=True)
    iw, ih = image.size
    w = height_px * iw / ih
    x0, y0 = _anchored(w, height_px, anchor)
    outline = [(x0, y0), (x0, y0 + height_px), (x0 + w, y0 + height_px), (x0 + w, y0)]
    return _shape(outline, "ink", 1.0, x, y, image, [(0, 1), (0, 0), (1, 0), (1, 1)])


def _objects(things):
    """One object or any nesting of lists of them, as a flat list."""
    if isinstance(things, bpy.types.Object):
        return [things]
    out = []
    for t in things:
        out.extend(_objects(t))
    return out


def _key(owner, path, t, value):
    setattr(owner, path, value)
    owner.keyframe_insert(path, frame=frame(t))


def _opacity(ob):
    return ob.active_material.node_tree.nodes["opacity"].outputs[0]


def fade_in(things, t, seconds=None):
    """Things fade in from nothing to their own opacity, starting at t, over the style's fade_s unless told."""
    s = STYLE["fade_s"] if seconds is None else seconds
    obs = _objects(things)
    for ob in obs:
        _key(_opacity(ob), "default_value", t, 0.0)
        _key(_opacity(ob), "default_value", t + s, ob["opacity"])
    _span(t, t + s, f"the fade in at {t:.2f} s")
    return things


def fade_out(things, t, seconds=None):
    """Things fade from their own opacity to nothing, starting at t, over the style's fade_s unless told."""
    s = STYLE["fade_s"] if seconds is None else seconds
    for ob in _objects(things):
        _key(_opacity(ob), "default_value", t, ob["opacity"])
        _key(_opacity(ob), "default_value", t + s, 0.0)
    _span(t, t + s, f"the fade out at {t:.2f} s")
    return things


def grow(bars, t, seconds=GROW_S):
    """Bars grow from their baselines to their length, starting at t. Anything else grows rightward from its
    anchor."""
    for ob in _objects(bars):
        axis = 1 if ob.get("direction") == "up" else 0
        small = [1.0, 1.0, 1.0]
        small[axis] = 0.0001
        _key(ob, "scale", t, small)
        _key(ob, "scale", t + seconds, (1.0, 1.0, 1.0))
    _span(t, t + seconds, f"the growth at {t:.2f} s")
    return bars


def draw(lines, t, seconds=DRAW_S):
    """Lines made by line() draw from their first point to their last, starting at t."""
    for ob in _objects(lines):
        if ob.type != "CURVE":
            raise SceneError("draw() draws lines made by line(); fade_in() shows anything else")
        _key(ob.data, "bevel_factor_end", t, 0.0)
        _key(ob.data, "bevel_factor_end", t + seconds, 1.0)
    _span(t, t + seconds, f"the line drawn at {t:.2f} s")
    return lines


class Clock:
    """The shot's time: its length in seconds and its beats."""

    def __init__(self):
        self.seconds = SHOT["seconds"]
        self.beats = [b["t"] for b in SHOT["beats"]]

    def beat(self, n):
        """Beat n's time from the shot's start, counting from 1 as the shot list does."""
        if not 1 <= n <= len(self.beats):
            raise SceneError(f"the shot has {len(self.beats)} beats, so there is no beat {n}")
        return self.beats[n - 1]


def _check_spans():
    """Every change starts at or after the shot's start and ends by its end."""
    end = SHOT["seconds"]
    wrong = [f"{what} runs {start:.2f} to {stop:.2f} s" for start, stop, what in _state["spans"]
             if start < 0 or stop > end + 0.5 / FPS]
    if wrong:
        raise SceneError(f"the shot lasts {end:.2f} s, and " + "; ".join(wrong) + "; start it earlier or make it "
                         "shorter")


def _setup(width, height, threads):
    """An empty scene: EEVEE, a clear film, the Standard view, and a camera that sees the 1920x1080 frame."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    r = scene.render
    r.resolution_x, r.resolution_y, r.resolution_percentage = width, height, 100
    r.fps, r.fps_base = FPS, 1.0
    scene.frame_start, scene.frame_end = 1, SHOT["frames"]
    r.film_transparent = True
    r.dither_intensity = 0.0       # dither's noise breaks the clip's runs of equal pixels and multiplies its size
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    try:
        scene.eevee.taa_render_samples = 16
    except AttributeError:
        pass
    r.threads_mode = "FIXED"
    r.threads = threads
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    r.image_settings.color_depth = "8"
    data = bpy.data.cameras.new("camera")
    data.type = "ORTHO"
    data.ortho_scale = REF_W / UNIT
    data.clip_end = 100
    camera = bpy.data.objects.new("camera", data)
    camera.location = (0, 0, 10)
    scene.collection.objects.link(camera)
    scene.camera = camera


def main(argv):
    global SHOT, STYLE, FOLDER, FPS
    plan_path, graphic_id, folder, scene_file, frames_dir, width, height, threads = argv
    plan = json.loads(Path(plan_path).read_text(encoding="utf-8"))
    SHOT = next(g for g in plan["graphics"] if g["id"] == graphic_id)
    STYLE = json.loads((REPO / plan["style"]).read_text(encoding="utf-8"))
    FOLDER, FPS = Path(folder), plan["fps"]
    if int(width) * REF_H != int(height) * REF_W:
        raise SceneError(f"a graphic renders at 16:9, and {width}x{height} is not")
    _setup(int(width), int(height), int(threads))
    sys.modules["storyblender"] = sys.modules[__name__]
    scene = runpy.run_path(scene_file, run_name="story_graphic")
    if not callable(scene.get("graphic")):
        raise SceneError("the scene defines no function graphic(clock)")
    scene["graphic"](Clock())
    _check_spans()
    bpy.context.scene.render.filepath = str(Path(frames_dir) / "f_")
    bpy.ops.render.render(animation=True)


if __name__ == "__main__":
    try:
        main(sys.argv[sys.argv.index("--") + 1:])
    except SceneError as exc:
        print(f"SCENE ERROR: {exc}", file=sys.stderr)
        sys.stderr.flush()
        sys.exit(1)
