"""Bring an old world config forward to main's format by inserting the keys it lacks, as text.

A nursery recipe names a world config under rounds/nursery/configs/. Each build that adds a
tunable makes the build refuse the older files, and the move is always the same: copy the old file,
insert the new keys at the default that leaves the world as it was, set the configHash line to the
new file's own hash, and change nothing else. This script does the insertion.

Usage (run it with `python -I`):

    python -I scripts/nursery-config-forward.py OLD TEMPLATE OUT [--hash HHHHHHHHHHHHHHHH]

    OLD       the old config (rounds/nursery/configs/<hash>.json)
    TEMPLATE  a config in main's format with every tunable at the default that leaves the world as it
              was (src/Evosim.Core.Tests/fixtures/r42-config.json)
    OUT       where to write the new file; refused if it exists. The usual name is <hash>.json.
    --hash    the value for the configHash line (the build's own RunConfig.Hash() of the new file).
              Without it the line holds the placeholder 0000000000000000.

What it does. For every key the template has and OLD lacks, it inserts the template's lines for that
key, as text, into the same group: after the nearest earlier key (in the template's order) that OLD
has, or first in the group when there is none. A multi-line value (development.shapeIds) comes in
whole. When a key becomes the last in its group, the line before it gains a comma. Nothing else
changes: not the order of the existing keys, not their values, and not a single existing line end.
The inserted lines take the file's line end, CRLF when most of its lines end so and LF otherwise; a
file that mixes the two keeps every line as it was. It prints every key it inserted with its value.

What it refuses, naming the key: a key OLD has and the template lacks; a key whose value is an object
in one file and not in the other; an array of objects whose elements differ in their keys; a group
that is empty in OLD and gains keys; a line it cannot find the end of. After writing the new text it
reads both files back and checks that every value of OLD is unchanged (configHash aside) and that
every key the template has is present, at the template's value where OLD lacked it.

Getting the hash: write a recipe that names OUT with a wrong configHash and run the nursery's
`--check` on it; the refusal says "... and the config at <path> hashes to <H>". Then re-run this
script with --hash H, to the file named <H>.json.
"""
import argparse
import json
import os
import sys

PLACEHOLDER = "0000000000000000"


class Refusal(Exception):
    pass


class Parser:
    """A JSON parser that keeps the text span of every object member."""

    def __init__(self, text):
        self.t = text
        self.i = 0

    def ws(self):
        while self.i < len(self.t) and self.t[self.i] in " \t\r\n":
            self.i += 1

    def value(self):
        self.ws()
        c = self.t[self.i]
        if c == "{":
            return self.obj()
        if c == "[":
            return self.arr()
        start = self.i
        if c == '"':
            self.string()
        else:
            while self.i < len(self.t) and self.t[self.i] not in ",]} \t\r\n":
                self.i += 1
        return {"kind": "scalar", "start": start, "end": self.i}

    def string(self):
        self.i += 1
        while self.t[self.i] != '"':
            self.i += 2 if self.t[self.i] == "\\" else 1
        self.i += 1

    def obj(self):
        start = self.i
        self.i += 1
        members = []
        self.ws()
        if self.t[self.i] == "}":
            self.i += 1
            return {"kind": "object", "start": start, "end": self.i, "members": members}
        while True:
            self.ws()
            ks = self.i
            self.string()
            key = json.loads(self.t[ks:self.i])
            self.ws()
            if self.t[self.i] != ":":
                raise Refusal(f"not JSON near offset {self.i}")
            self.i += 1
            v = self.value()
            members.append({"key": key, "kstart": ks, "value": v, "end": v["end"]})
            self.ws()
            if self.t[self.i] == ",":
                self.i += 1
                continue
            if self.t[self.i] != "}":
                raise Refusal(f"not JSON near offset {self.i}")
            self.i += 1
            return {"kind": "object", "start": start, "end": self.i, "members": members}

    def arr(self):
        start = self.i
        self.i += 1
        items = []
        self.ws()
        if self.t[self.i] == "]":
            self.i += 1
            return {"kind": "array", "start": start, "end": self.i, "items": items}
        while True:
            items.append(self.value())
            self.ws()
            if self.t[self.i] == ",":
                self.i += 1
                continue
            if self.t[self.i] != "]":
                raise Refusal(f"not JSON near offset {self.i}")
            self.i += 1
            return {"kind": "array", "start": start, "end": self.i, "items": items}


def parse(text):
    p = Parser(text)
    v = p.value()
    p.ws()
    if p.i != len(text):
        raise Refusal("text after the top-level value")
    return v


def line_start(text, pos):
    return text.rfind("\n", 0, pos) + 1


def line_indent(text, pos):
    seg = text[line_start(text, pos):pos]
    return seg if seg.strip() == "" else ""


def after_member_line(text, pos):
    """The offset just past the end of the line that holds offset `pos`, comma and trailing blanks included."""
    k = pos
    while k < len(text) and text[k] in " \t":
        k += 1
    if k < len(text) and text[k] == ",":
        k += 1
    while k < len(text) and text[k] in " \t":
        k += 1
    if k < len(text) and text[k] == "\r":
        k += 1
    if k >= len(text) or text[k] != "\n":
        raise Refusal("a member is not followed by the end of its line")
    return k + 1


def keysets(node, text):
    """The key tuples of the elements of an array of objects; None when it is not one."""
    if node["kind"] != "array" or not node["items"] or any(x["kind"] != "object" for x in node["items"]):
        return None
    return [tuple(m["key"] for m in x["members"]) for x in node["items"]]


def check_arrays(vo, vt, path, otext, ttext):
    ko, kt = keysets(vo, otext), keysets(vt, ttext)
    if (ko is None) != (kt is None):
        raise Refusal(f"{path}: an array of objects in one file and not in the other")
    if ko is not None and not set(ko) <= set(kt):
        raise Refusal(f"{path}: the elements of this array of objects differ in their keys from the template's")


def forward(on, ot, otext, ttext, path, inserted, splices, eol):
    """Collect the splices (offset, text) that insert the template's missing keys into old object `on`."""
    old = {m["key"]: m for m in on["members"]}
    tmpl = {m["key"]: m for m in ot["members"]}
    for k in old:
        if k not in tmpl:
            raise Refusal(f"{path}{k}: the old config has it and the template lacks it")
    for m in on["members"]:
        k = m["key"]
        vo, vt = m["value"], tmpl[k]["value"]
        if (vo["kind"] == "object") != (vt["kind"] == "object"):
            raise Refusal(f"{path}{k}: an object in one file and not in the other")
        if vo["kind"] == "object":
            forward(vo, vt, otext, ttext, f"{path}{k}.", inserted, splices, eol)
        else:
            check_arrays(vo, vt, f"{path}{k}", otext, ttext)
    if not any(tm["key"] not in old for tm in ot["members"]):
        return
    if not on["members"]:
        raise Refusal(f"{path or 'top level'}: an empty group that gains keys is not handled")
    mind = line_indent(otext, on["members"][0]["kstart"])
    # group the missing keys by their anchor: the nearest earlier template key the old object has
    groups = {}
    anchor = None
    for tm in ot["members"]:
        if tm["key"] in old:
            anchor = tm["key"]
        else:
            groups.setdefault(anchor, []).append(tm)
    last_key = on["members"][-1]["key"]
    for anchor, tms in groups.items():
        lines = []
        for tm in tms:
            text = ttext[tm["kstart"]:tm["end"]]
            tind = line_indent(ttext, tm["kstart"])
            if tind != mind:
                parts = text.split("\n")
                text = "\n".join([parts[0]] + [mind + ln[len(tind):] if ln.startswith(tind) else ln for ln in parts[1:]])
            lines.append(mind + text.replace("\n", eol))
            inserted.append((f"{path}{tm['key']}", json.dumps(json.loads(ttext[tm["value"]["start"]:tm["value"]["end"]]))))
        if anchor is None:
            splices.append((line_start(otext, on["members"][0]["kstart"]), "".join(ln + "," + eol for ln in lines)))
        elif anchor != last_key:
            splices.append((after_member_line(otext, old[anchor]["end"]), "".join(ln + "," + eol for ln in lines)))
        else:
            # the old last member gains a comma; the new lines follow its line, the last without one
            splices.append((old[anchor]["end"], ","))
            splices.append((after_member_line(otext, old[anchor]["end"]), ("," + eol).join(lines) + eol))


def detect_eol(text):
    """The line end for inserted lines: CRLF when most of the file's lines end so, else LF."""
    crlf = text.count("\r\n")
    lf = text.count("\n") - crlf
    return "\r\n" if crlf > lf else "\n"


def verify(old, new, tmpl, path=""):
    for k, v in old.items():
        if k not in new:
            raise Refusal(f"{path}{k}: lost in the rewrite")
        if isinstance(v, dict):
            verify(v, new[k], tmpl[k], f"{path}{k}.")
        elif new[k] != v and not (path == "" and k == "configHash"):
            raise Refusal(f"{path}{k}: value changed in the rewrite")
    for k, v in tmpl.items():
        if k not in new:
            raise Refusal(f"{path}{k}: missing after the rewrite")
        if isinstance(v, dict):
            verify(old.get(k, {}), new[k], v, f"{path}{k}.")
        elif k not in old and new[k] != v:
            raise Refusal(f"{path}{k}: not the template's value")


def main():
    ap = argparse.ArgumentParser(description="Insert the template's missing keys into an old world config, as text.")
    ap.add_argument("old")
    ap.add_argument("template")
    ap.add_argument("out")
    ap.add_argument("--hash", default=PLACEHOLDER, help="value for the configHash line")
    a = ap.parse_args()
    if os.path.exists(a.out):
        print(f"REFUSED: {a.out} exists", file=sys.stderr)
        return 1
    try:
        with open(a.old, "rb") as f:
            otext = f.read().decode("utf-8")
        with open(a.template, "rb") as f:
            ttext = f.read().decode("utf-8").replace("\r\n", "\n")
        eol = detect_eol(otext)
        on, ot = parse(otext), parse(ttext)
        if on["kind"] != "object" or ot["kind"] != "object":
            raise Refusal("a top-level value is not an object")
        inserted, splices = [], []
        forward(on, ot, otext, ttext, "", inserted, splices, eol)
        by_pos = {}
        for pos, txt in splices:
            by_pos[pos] = by_pos.get(pos, "") + txt
        new = otext
        for pos in sorted(by_pos, reverse=True):
            new = new[:pos] + by_pos[pos] + new[pos:]
        hm = next((m for m in parse(new)["members"] if m["key"] == "configHash"), None)
        if hm is None:
            raise Refusal("configHash: no such top-level key")
        hv = hm["value"]
        new = new[:hv["start"]] + json.dumps(a.hash) + new[hv["end"]:]
        verify(json.loads(otext), json.loads(new), json.loads(ttext))
    except Refusal as e:
        print(f"REFUSED: {e}", file=sys.stderr)
        return 1
    with open(a.out, "wb") as f:
        f.write(new.encode("utf-8"))
    for k, v in inserted:
        print(f"inserted {k} = {v}")
    crlf = otext.count("\r\n")
    lf = otext.count("\n") - crlf
    print(f"{len(inserted)} keys inserted with {'CRLF' if eol == chr(13) + chr(10) else 'LF'} line ends "
          f"(the old file has {crlf} CRLF and {lf} LF-only lines, each kept as it was); configHash {a.hash}; wrote {a.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
