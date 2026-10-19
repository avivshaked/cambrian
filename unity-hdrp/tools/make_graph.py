"""Write an HDRP Lit Shader Graph that hands its work to the theatre's own HLSL.

    python -I make_graph.py <spec.json>

Why a script writes the graph. The theatre's shaders are hand-written HLSL, and HDRP lights only
what reaches it through its own passes, which a hand-written shader cannot join without copying
HDRP's internals. A Shader Graph can: two Custom Function nodes carry the whole of the theatre's
code, one in the vertex stage and one in the fragment stage, and the graph is only the wiring. A
graph drawn by hand cannot be reviewed in a diff or remade when the HLSL changes; this script can.

The spec names:
  name, path           the shader is "<path>/<name>", so the theatre's Shader.Find still finds it
  template             an HDRP Lit graph whose target and block nodes are kept (Unity's iceberg)
  hlsl                 the HLSL file both functions live in, by its path under Assets/
  properties_from      a URP .shader whose Properties block becomes the graph's properties, with
                       its defaults, so the materials and the palette's property blocks still bind
  skip_properties      names left out (the blend and depth switches HDRP keeps itself)
  vertex, fragment     {function, inputs: [[name, source, size]], outputs: [[name, size, target]]}
                       source is position:<space>, normal:<space>, view:<space>, uv:<channel>,
                       color:vertex or interp:<name>; target is block:<descriptor>, interp:<name>
                       or none; vertex may be left out, and the mesh is drawn as it is
  material_type        HDRP Lit's material type (0 standard, 4 specular colour, 5 translucent)
  specular_block       true adds the Specular Color block, which the template lacks, for material
                       type 4: the bed and the rock set their specular there (TheatreDepthHdrp.hlsl)
  double_sided         0 off, 1 on

The output is <name>.shadergraph beside the HLSL. Unity reads it on the next import.
"""
import json
import os
import re
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sgdump  # noqa: E402

SLOT_TYPES = {1: "Vector1MaterialSlot", 2: "Vector2MaterialSlot", 3: "Vector3MaterialSlot",
              4: "Vector4MaterialSlot"}
SPACES = {"object": 0, "view": 1, "world": 2, "tangent": 3, "absolute": 4}


def new_id():
    return uuid.uuid4().hex


def zero(size):
    return 0.0 if size == 1 else {k: 0.0 for k in "xyzw"[:size]}


def slot(slot_id, name, size, output, stage=3, kind=None, extra=None):
    o = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph." + (kind or SLOT_TYPES[size]),
         "m_ObjectId": new_id(), "m_Id": slot_id, "m_DisplayName": name,
         "m_SlotType": 1 if output else 0, "m_Hidden": False, "m_ShaderOutputName": name,
         "m_StageCapability": stage, "m_Value": zero(size), "m_DefaultValue": zero(size),
         "m_Labels": []}
    o.update(extra or {})
    return o


def node(kind, name, slots, x, y, version=0, **fields):
    o = {"m_SGVersion": version, "m_Type": "UnityEditor.ShaderGraph." + kind, "m_ObjectId": new_id(),
         "m_Group": {"m_Id": ""}, "m_Name": name,
         "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": x, "y": y,
                                                           "width": 200.0, "height": 120.0}},
         "m_Slots": [{"m_Id": s["m_ObjectId"]} for s in slots], "synonyms": [], "m_Precision": 0,
         "m_PreviewExpanded": False, "m_DismissedVersion": 0, "m_PreviewMode": 0,
         "m_CustomColors": {"m_SerializableColors": []}}
    o.update(fields)
    return o


def edge(out_node, out_slot, in_node, in_slot):
    return {"m_OutputSlot": {"m_Node": {"m_Id": out_node["m_ObjectId"]}, "m_SlotId": out_slot},
            "m_InputSlot": {"m_Node": {"m_Id": in_node["m_ObjectId"]}, "m_SlotId": in_slot}}


PROPERTY = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*'
    r'(Color|Vector|Float|Int|Range\s*\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\))\s*\)\s*=\s*(.+?)\s*$')


def urp_properties(path, skip):
    """The Properties block of a URP shader, as (name, display, kind, default, range)."""
    text = open(path, encoding="utf-8").read()
    start = text.index("Properties")
    depth, i = 0, text.index("{", start)
    for j in range(i, len(text)):
        depth += {"{": 1, "}": -1}.get(text[j], 0)
        if depth == 0:
            block = text[i + 1:j]
            break
    out = []
    for line in block.splitlines():
        line = line.split("//")[0]
        m = PROPERTY.match(line)
        if not m or m.group(1) in skip:
            continue
        name, display, kind, lo, hi, default = m.groups()
        nums = [float(v) for v in re.findall(r"[-\d.]+(?:e-?\d+)?", default)]
        out.append((name, display, kind.split("(")[0].strip(), nums, (lo, hi)))
    return out


def graph_property(name, display, kind, nums, rng):
    base = {"m_Guid": {"m_GuidSerialized": str(uuid.uuid4())}, "m_Name": display[:60] or name,
            "m_DefaultReferenceName": "", "m_OverrideReferenceName": name,
            "m_GeneratePropertyBlock": True, "m_Precision": 0, "overrideHLSLDeclaration": False,
            "hlslDeclarationOverride": 0, "m_Hidden": False, "m_ObjectId": new_id()}
    if kind == "Color":
        r, g, b, a = (nums + [0, 0, 0, 1])[:4]
        base.update({"m_SGVersion": 2, "m_Type": "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
                     "m_Value": {"r": r, "g": g, "b": b, "a": a}, "m_ColorMode": 0})
    elif kind == "Vector":
        x, y, z, w = (nums + [0, 0, 0, 0])[:4]
        base.update({"m_SGVersion": 1, "m_Type": "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty",
                     "m_Value": {"x": x, "y": y, "z": z, "w": w}})
    else:
        ranged = kind == "Range"
        base.update({"m_SGVersion": 1, "m_Type": "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty",
                     "m_Value": nums[0] if nums else 0.0, "m_FloatType": 1 if ranged else 0,
                     "m_RangeValues": {"x": float(rng[0]) if ranged else 0.0,
                                       "y": float(rng[1]) if ranged else 1.0}})
    return base


def source_node(source, size, x, y):
    """A node that supplies one input of a custom function, and its output slot's id."""
    what, arg = source.split(":")
    if what in ("position", "normal", "view"):
        out = slot(0, "Out", 3, True)
        kind = {"position": "PositionNode", "normal": "NormalVectorNode", "view": "ViewDirectionNode"}[what]
        fields = {"m_Space": SPACES[arg]}
        if what == "position":
            fields["m_PositionSource"] = 0
        return node(kind, kind, [out], x, y, version=1 if what != "normal" else 0, **fields), [out], 0
    if what == "color":
        out = slot(0, "Out", 4, True)
        return node("VertexColorNode", "Vertex Color", [out], x, y), [out], 0
    if what == "uv":
        out = slot(0, "Out", 4, True)
        return node("UVNode", "UV", [out], x, y, m_OutputChannel=int(arg)), [out], 0
    if what == "interp":
        out = slot(0, "Out", size, True, stage=2)
        return (node("CustomInterpolatorNode", arg + " (Custom Interpolator)", [out], x, y,
                     customBlockNodeName=arg, serializedType=size), [out], 0)
    raise ValueError(source)


def build(spec_path):
    spec = json.load(open(spec_path, encoding="utf-8"))
    here = os.path.dirname(os.path.abspath(spec_path))
    assets = os.path.join(here, spec.get("assets_root", "../Assets"))
    hlsl_path = os.path.join(assets, spec["hlsl"])
    meta = open(hlsl_path + ".meta", encoding="utf-8").read()
    hlsl_guid = re.search(r"guid: (\w+)", meta).group(1)

    template = sgdump.objects(os.path.join(here, spec["template"]) if not os.path.isabs(spec["template"])
                              else spec["template"])
    byid = {o["m_ObjectId"]: o for o in template}
    graph = template[0]
    keep_types = ("HDTarget", "HDLitSubTarget", "HDLitData", "LightingData", "SystemData", "BuiltinData")
    kept = [o for o in template if o["m_Type"].endswith(keep_types)]
    blocks = {}
    for ctx in ("m_VertexContext", "m_FragmentContext"):
        for ref in graph[ctx]["m_Blocks"]:
            b = byid[ref["m_Id"]]
            blocks[b["m_SerializedDescriptor"]] = (b, [byid[s["m_Id"]] for s in b["m_Slots"]])

    # A normal in world space, so the theatre's carve hands HDRP the normal it already builds.
    if "SurfaceDescription.NormalTS" in blocks:
        b, slots = blocks.pop("SurfaceDescription.NormalTS")
        b["m_Name"] = b["m_SerializedDescriptor"] = "SurfaceDescription.NormalWS"
        slots[0].update({"m_DisplayName": "Normal (World Space)", "m_ShaderOutputName": "NormalWS", "m_Space": 2})
        blocks["SurfaceDescription.NormalWS"] = (b, slots)
    for o in kept:
        if o["m_Type"].endswith("LightingData"):
            o["m_NormalDropOffSpace"] = 2
        if o["m_Type"].endswith("HDLitData"):
            # HDRP reads the mask since 2023.1 and keeps the old field only to upgrade old graphs: one
            # bit at the type's MaterialId (MaterialExtension.cs), which orders standard and
            # subsurface scattering the other way round from the old enum.
            kind = spec.get("material_type", 0)
            o["m_MaterialType"] = kind
            o["m_MaterialTypeMask"] = 1 << {0: 1, 1: 0}.get(kind, kind)
        if o["m_Type"].endswith("SystemData"):
            o["m_DoubleSidedMode"] = spec.get("double_sided", 0)
            # Opaque and whole. The Iceberg template clips on alpha and its Alpha block holds 0
            # against a threshold of 0.5, which discards every pixel of a graph that leaves Alpha
            # unconnected: the bed and the earth drew nothing, and what showed was behind them.
            o["m_AlphaTest"] = False
            o["m_ZWrite"] = True
    if "SurfaceDescription.Alpha" in blocks:
        blocks["SurfaceDescription.Alpha"][1][0]["m_Value"] = 1.0

    # The Specular Color block, as HDRP's own graphs write it (its particle samples), for the
    # specular colour material type.
    if spec.get("specular_block"):
        grey = {"x": 0.5, "y": 0.5, "z": 0.5}
        sslot = slot(0, "Specular Color", 3, False, stage=2, kind="ColorRGBMaterialSlot",
                     extra={"m_ShaderOutputName": "Specular", "m_Value": dict(grey), "m_DefaultValue": dict(grey),
                            "m_ColorMode": 0, "m_DefaultColor": {"r": 0.5, "g": 0.5, "b": 0.5, "a": 1.0}})
        b = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.BlockNode", "m_ObjectId": new_id(),
             "m_Group": {"m_Id": ""}, "m_Name": "SurfaceDescription.Specular",
             "m_DrawState": {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": 0.0, "y": 0.0,
                                                                "width": 0.0, "height": 0.0}},
             "m_Slots": [{"m_Id": sslot["m_ObjectId"]}], "synonyms": [], "m_Precision": 0,
             "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0,
             "m_CustomColors": {"m_SerializableColors": []},
             "m_SerializedDescriptor": "SurfaceDescription.Specular"}
        blocks["SurfaceDescription.Specular"] = (b, [sslot])
        graph["m_FragmentContext"]["m_Blocks"].append({"m_Id": b["m_ObjectId"]})

    objects, edges, interps = [], [], {}
    vertex_blocks = [ref for ref in graph["m_VertexContext"]["m_Blocks"]]

    def custom_function(stage, x):
        part = spec[stage]
        slots, in_ids, y = [], [], 0.0
        sid = 0
        for name, source, size in part["inputs"]:
            s = slot(sid, name, size, False)
            slots.append(s)
            src, src_slots, out_id = source_node(source, size, x - 320.0, y)
            objects.append(src)
            objects.extend(src_slots)
            in_ids.append((src, out_id, sid))
            sid += 1
            y += 140.0
        outs = []
        for name, size, target in part["outputs"]:
            s = slot(sid, name, size, True)
            slots.append(s)
            outs.append((sid, size, target))
            sid += 1
        fn = node("CustomFunctionNode", part["function"] + " (Custom Function)", slots, x, 0.0, version=1,
                  m_SourceType=0, m_FunctionName=part["function"], m_FunctionSource=hlsl_guid,
                  m_FunctionBody="")
        fn["synonyms"] = ["code", "HLSL"]
        objects.append(fn)
        objects.extend(slots)
        for src, out_id, in_id in in_ids:
            edges.append(edge(src, out_id, fn, in_id))
        for out_id, size, target in outs:
            if target == "none":
                continue
            kind, name = target.split(":", 1)
            if kind == "block":
                b, _ = blocks[name]
                edges.append(edge(fn, out_id, b, 0))
            else:
                bslot = slot(0, name, size, False, stage=1)
                b = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.BlockNode", "m_ObjectId": new_id(),
                     "m_Group": {"m_Id": ""}, "m_Name": "VertexDescription.CustomInterpolator",
                     "m_Slots": [{"m_Id": bslot["m_ObjectId"]}], "synonyms": [], "m_Precision": 0,
                     "m_PreviewExpanded": True, "m_DismissedVersion": 0, "m_PreviewMode": 0,
                     "m_CustomColors": {"m_SerializableColors": []},
                     "m_SerializedDescriptor": f"VertexDescription.{name}#{size}"}
                objects.extend([b, bslot])
                vertex_blocks.append({"m_Id": b["m_ObjectId"]})
                edges.append(edge(fn, out_id, b, 0))

    if "vertex" in spec:
        custom_function("vertex", -900.0)
    custom_function("fragment", -400.0)

    props = [graph_property(*p) for p in
             urp_properties(os.path.join(here, spec["properties_from"]), set(spec.get("skip_properties", [])))]
    category = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.CategoryData", "m_ObjectId": new_id(),
                "m_Name": "", "m_ChildObjectList": [{"m_Id": p["m_ObjectId"]} for p in props]}

    block_objs = []
    for b, slots in blocks.values():
        block_objs.append(b)
        block_objs.extend(slots)

    graph["m_Properties"] = [{"m_Id": p["m_ObjectId"]} for p in props]
    graph["m_Keywords"], graph["m_Dropdowns"] = [], []
    graph["m_CategoryData"] = [{"m_Id": category["m_ObjectId"]}]
    graph["m_Nodes"] = ([{"m_Id": b["m_ObjectId"]} for b, _ in blocks.values()] +
                        [{"m_Id": o["m_ObjectId"]} for o in objects if o["m_Type"].endswith("Node")])
    graph["m_GroupDatas"], graph["m_StickyNoteDatas"] = [], []
    graph["m_Edges"] = edges
    graph["m_VertexContext"]["m_Blocks"] = vertex_blocks
    graph["m_Path"] = spec["path"]

    # The vertex blocks for custom interpolators go into the vertex context; the rest stay where
    # the template had them.
    out = [graph] + kept + block_objs + objects + props + [category]
    target = os.path.join(os.path.dirname(hlsl_path), spec["name"] + ".shadergraph")
    with open(target, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n\n".join(json.dumps(o, indent=4) for o in out) + "\n")
    print(f"wrote {target}: {len(props)} properties, {len(objects)} nodes and slots, {len(edges)} edges")


if __name__ == "__main__":
    build(sys.argv[1])
