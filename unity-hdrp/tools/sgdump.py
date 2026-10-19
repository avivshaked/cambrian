"""Print the objects of a Shader Graph file, or those of one type, for writing graphs by script.

    python -I sgdump.py <file.shadergraph> [TypeSuffix ...]

A .shadergraph is a run of JSON objects one after another. With no type, it prints each object's
type, id and name; with types, it prints those objects whole, and each one's slots.
"""
import json
import sys


def objects(path):
    text = open(path, encoding="utf-8").read()
    dec, i, out = json.JSONDecoder(), 0, []
    while True:
        while i < len(text) and text[i].isspace():
            i += 1
        if i >= len(text):
            return out
        o, i = dec.raw_decode(text, i)
        out.append(o)


def main(path, types):
    objs = objects(path)
    byid = {o.get("m_ObjectId"): o for o in objs}
    for o in objs:
        t = o["m_Type"].rsplit(".", 1)[-1]
        if not types:
            print(t, o.get("m_ObjectId"), o.get("m_Name", o.get("m_SerializedDescriptor", "")))
        elif t in types:
            print(json.dumps(o, indent=1))
            for s in o.get("m_Slots", []):
                print(json.dumps(byid[s["m_Id"]], indent=1))


if __name__ == "__main__":
    main(sys.argv[1], set(sys.argv[2:]))
