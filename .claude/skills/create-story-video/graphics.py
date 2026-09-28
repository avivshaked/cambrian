#!/usr/bin/env python3
"""The graphics step's tool, for the create-story-video skill (graphics.md).

    python graphics.py plan <folder>            list every graphic, still and word card the shot list asks for
    python graphics.py setup <folder>           say whether Blender is there, at the settings' version
    python graphics.py stills <folder>          draw the stills in the theatre, one Editor launch (Unity)
    python graphics.py words <folder>           draw the words on screen as transparent clips (Pillow, ffmpeg)
    python graphics.py render <folder> <id>     render one graphic's scene in Blender and check it
    python graphics.py check <folder>           check that every graphic, still and word card is drawn and current
    python graphics.py finish <folder>          render every graphic and word card drawn at the draft size again at
                                                the film's size, from the same scene, once the reviewer passed them

The first line each command prints is its verdict, as graphics.md reads it. The last line says how long the
command took. Exit status: 0 on READY, NOTHING TO DRAW, PASS, 1 otherwise, 2 on a wrong command. It uses the
standard library, Pillow, numpy, ffmpeg and ffprobe; `render` runs Blender headless on storyblender.py and
`stills` runs the Unity Editor on a render worker.
"""

import ast
import hashlib
import importlib.util
import json
import math
import os
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
SETTINGS = "graphics-settings.json"
NL = chr(10)
SCHEMA = "create-story-video/graphics/1"
PLAN_SCHEMA = "create-story-video/graphics-plan/1"
SHOT_ID = re.compile(r"([0-9]+[.][0-9]+)([a-z])")
SIZE = re.compile(r"([0-9]+)x([0-9]+)")
DIGITS = re.compile(r"[0-9](?:[0-9]|,(?=[0-9]))*(?:[.][0-9]+)?")      # a comma ends a number unless a digit follows
CODECS = {"qtrle": ["-c:v", "qtrle", "-pix_fmt", "argb"],
          "prores4444": ["-c:v", "prores_ks", "-profile:v", "4444", "-pix_fmt", "yuva444p10le"]}
BEAT_WINDOW_S = 0.6                # something new shows within this long after each beat
BEAT_CHANGE = 0.00005              # the share of the frame that must change for a beat to be seen: 100 pixels at 1080p
# Over footage a graphic keeps to a third of the frame and never dims it, so that the footage stays the star (the
# owner's ruling of 2026-09-28). A pixel counts as covered from an opacity of 16 in 255, which a full card's dim
# (0.17) passes everywhere; the corner card is about an eighth of the frame.
OVER_FOOTAGE_MAX = 1 / 3
COVERED_ALPHA = 16
SOLO_ENTRY = "Evosim.Theatre.EditorTools.TheatreSoloStill.Run"
SOLO_FILE = "unity/Assets/Theatre/Editor/TheatreSoloStill.cs"

_spec = importlib.util.spec_from_file_location("script_check", HERE / "script-check.py")
sc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sc)


class Refused(Exception):
    """A command that cannot go on; its lines say why."""

    def __init__(self, *lines):
        super().__init__(lines[0] if lines else "")
        self.lines = list(lines)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + chr(10), encoding="utf-8")


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def sha_of(data):
    return hashlib.sha256(json.dumps(data, sort_keys=True).encode("utf-8")).hexdigest()[:16]


def number(x):
    return isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x)


def now():
    return datetime.now().isoformat(timespec="seconds")


def repo_path(value):
    p = Path(value)
    return p if p.is_absolute() else REPO / p


def rel(path):
    """A path as the records write it: from the film's checkout when it lies inside it."""
    try:
        return Path(path).resolve().relative_to(REPO).as_posix()
    except ValueError:
        return Path(path).resolve().as_posix()


def settings_of(folder, lines):
    """The film's graphics settings, first copied from the skill's (with the style) if the film has none."""
    if not (folder / SETTINGS).is_file():
        shutil.copyfile(HERE / SETTINGS, folder / SETTINGS)
        lines.append(f"copied the skill's {SETTINGS} into the film's folder")
    s = read_json(folder / SETTINGS)
    style = s.get("style")
    if isinstance(style, str) and style and not (folder / style).is_file() and (HERE / style).is_file():
        shutil.copyfile(HERE / style, folder / style)
        lines.append(f"copied the skill's {style} into the film's folder")
    problems = []
    if not number(s.get("handles_s")) or s["handles_s"] < 0:
        problems.append("handles_s is seconds, 0 or more")
    if not isinstance(s.get("fps"), int) or not 1 <= s["fps"] <= 60:
        problems.append("fps is a whole number, 1 to 60")
    m = SIZE.fullmatch(str(s.get("size", "")))
    if not m or int(m.group(1)) % 2 or int(m.group(2)) % 2 or not 64 <= int(m.group(1)) <= 4096:
        problems.append("size is <width>x<height>, both even, 64 to 4096")
    elif int(m.group(1)) * 9 != int(m.group(2)) * 16:
        problems.append("size is 16:9, as the style's 1920x1080 frame is")
    d = SIZE.fullmatch(str(s.get("draft_size", "")))
    if "draft_size" in s and (not d or int(d.group(1)) % 2 or int(d.group(2)) % 2 or not 64 <= int(d.group(1)) <= 4096
                              or int(d.group(1)) * 9 != int(d.group(2)) * 16):
        problems.append("draft_size is <width>x<height>, both even, 64 to 4096, 16:9")
    if s.get("codec") not in CODECS:
        problems.append("codec is one of " + ", ".join(CODECS))
    if not number(s.get("fade_s")) or not 0 <= s["fade_s"] <= 2:
        problems.append("fade_s is seconds, 0 to 2")
    for key, low, high in (("render_minutes", 1, 120), ("still_wall_minutes", 5, 240), ("worker", 2, 99),
                           ("still_size", 256, 4096), ("render_threads", 1, 16)):
        if not isinstance(s.get(key), int) or not low <= s[key] <= high:
            problems.append(f"{key} is a whole number, {low} to {high}")
    for key in ("blender", "blender_version", "unity", "runs_root", "style"):
        if not isinstance(s.get(key), str) or not s[key]:
            problems.append(f"{key} is text: a path, absolute or from the checkout's root, or a version")
    if not problems and not (folder / s["style"]).is_file():
        problems.append(f"style names {s['style']}, which is not in the film's folder or the skill's")
    if problems:
        raise Refused(f"the film's {SETTINGS} is not in its form:", *("  " + p for p in problems))
    s["width"], s["height"] = int(m.group(1)), int(m.group(2))
    if "draft_size" in s:
        s["draft_width"], s["draft_height"] = int(d.group(1)), int(d.group(2))
    return s


def at_size(s, final):
    """The settings at the size a render works at: the draft size while a graphic is drawn and reviewed, the
    film's own size when finish renders it for the edit (the owner, 2026-09-28: "render a low res version for
    the reviewer, and only after approving do we render the high quality")."""
    if final or "draft_size" not in s or s["draft_size"] == s["size"]:
        return {**s, "final_size": s["size"]}
    return {**s, "width": s["draft_width"], "height": s["draft_height"], "size": s["draft_size"],
            "final_size": s["size"]}


def shots_of(folder):
    shots = read_json(folder / "shots.json")
    if shots is None:
        raise Refused("the film's folder holds no shots.json; the shot list step writes it")
    return shots


def safe(text):
    return re.sub(r"[^A-Za-z0-9._-]+", "-", str(text)).strip("-")


def still_name(still):
    return safe(f"{still['run']}-{still['body']}-{int(round(still['second']))}")


def style_sha(folder, s):
    return sha256_of(folder / s["style"])[:16]


def build_plan(folder, s, shots):
    """What the shot list asks the graphics step to draw, each with the key its drawing is filed under."""
    look = style_sha(folder, s)
    frame = {"handles_s": s["handles_s"], "fps": s["fps"], "size": s["size"], "codec": s["codec"], "style": look}
    graphics, stills, words = [], {}, []
    for shot in shots["shots"]:
        for i, t in enumerate(shot.get("text") or []):
            seconds = round(t["to_s"] - t["from_s"], 3)
            item = {"id": f"{shot['id']}-w{i + 1}", "shot": shot["id"], "words": sc.unmark(t["words"]),
                    "from_s": t["from_s"], "to_s": t["to_s"], "seconds": seconds,
                    "frames": round(seconds * s["fps"]), "file": f"graphics/words/{shot['id']}-w{i + 1}.mov"}
            item["key"] = sha_of({"words": item["words"], "seconds": seconds, "fade_s": s["fade_s"], **frame})
            words.append(item)
        if shot["kind"] != "graphic":
            continue
        names = []
        for still in shot["stills"]:
            name = still_name(still)
            names.append(name)
            stills[name] = {"name": name, "run": still["run"], "body": str(still["body"]),
                            "second": still["second"], "png": f"graphics/stills/{name}.png",
                            "key": sha_of({"run": still["run"], "body": str(still["body"]), "second": still["second"],
                                           "size": s["still_size"]})}
        seconds = round(shot["to_s"] - shot["from_s"], 3)
        item = {"id": shot["id"], "from_s": shot["from_s"], "to_s": shot["to_s"], "seconds": seconds,
                "frames": round(seconds * s["fps"]), "see": shot["see"], "places": shot["places"],
                "beats": [{"t": round(b["at_s"] - shot["from_s"], 3), "shows": b["shows"]} for b in shot["beats"]],
                "stills": names, "words_on_screen": [sc.unmark(t["words"]) for t in shot.get("text") or []],
                "over": "footage" if "run" in shot else "alone",
                "scene": f"graphics/scenes/{shot['id']}.py", "file": f"graphics/clips/{shot['id']}.mov"}
        if "alone" in shot:
            item["alone"] = shot["alone"]
        item["key"] = sha_of({k: item[k] for k in ("seconds", "see", "beats", "stills", "over")} | frame)
        graphics.append(item)
    return {"schema": PLAN_SCHEMA, "shots_sha256": sha256_of(folder / "shots.json"), "made": now(),
            "fps": s["fps"], "width": s["width"], "height": s["height"], "handles_s": s["handles_s"],
            "style": rel(folder / s["style"]), "facts": rel(folder / "facts.md"),
            "graphics": graphics, "stills": sorted(stills.values(), key=lambda x: x["name"]), "words": words}


def records_of(folder):
    r = read_json(folder / "graphics.json") or {}
    return {"schema": SCHEMA, "graphics": r.get("graphics", {}), "stills": r.get("stills", {}),
            "words": r.get("words", {})}


def save_records(folder, records):
    records["shots_sha256"] = sha256_of(folder / "shots.json")
    write_json(folder / "graphics.json", records)


def fresh_path(folder, item, stem):
    """Where a render writes before settle names it."""
    planned = folder / item["file"]
    return planned.parent / f"{stem}-new{planned.suffix}"


def settle(folder, written, stem):
    """Moves a file just written to a name taken from its content, and returns that name relative to the folder.
    A drawing is never written over another: Resolve links a file where it lies, so a new drawing under the old
    name would silently change every cut already built from it."""
    digest = sha256_of(written)
    final = written.parent / f"{stem}-{digest[:10]}{written.suffix}"
    if final.is_file() and sha256_of(final) == digest:
        written.unlink()
    else:
        os.replace(written, final)
    return final.relative_to(folder).as_posix()


def stale(folder, kind, item, records):
    """Why an item's drawing is missing or out of date, or None when it is current."""
    r = records[kind].get(item["name"] if kind == "stills" else item["id"])
    if r is None:
        return "not drawn"
    if r["key"] != item["key"]:
        return "drawn for another shot list or settings"
    path = folder / (item["png"] if kind == "stills" else r["file"])
    if not path.is_file() or sha256_of(path) != r["sha256"]:
        return "its file is missing or changed since it was checked"
    if kind == "graphics":
        scene = folder / item["scene"]
        if not scene.is_file() or sha256_of(scene) != r["scene_sha256"]:
            return "its scene changed since it was rendered"
        for name in item["stills"]:
            still = records["stills"].get(name)
            if still is None or r["stills_sha256"].get(name) != still["sha256"]:
                return f"still {name} was drawn again since it was rendered"
    return None


def load_plan(folder):
    plan = read_json(folder / "graphics" / "plan.json")
    if plan is None:
        raise Refused("the film has no graphics/plan.json; run plan first")
    if plan["shots_sha256"] != sha256_of(folder / "shots.json"):
        raise Refused("shots.json changed since the graphics were planned; run plan again")
    return plan


def cmd_plan(folder):
    lines = []
    s = settings_of(folder, lines)
    shots = shots_of(folder)
    plan = build_plan(folder, s, shots)
    write_json(folder / "graphics" / "plan.json", plan)
    for d in ("scenes", "clips", "stills", "words", "sheets"):
        (folder / "graphics" / d).mkdir(parents=True, exist_ok=True)
    records = records_of(folder)
    todo = {"graphics": 0, "stills": 0, "words": 0}
    for kind, items in (("stills", plan["stills"]), ("graphics", plan["graphics"]), ("words", plan["words"])):
        for item in items:
            why = stale(folder, kind, item, records)
            name = item["name"] if kind == "stills" else item["id"]
            if why:
                todo[kind] += 1
            if kind == "graphics":
                scene = "scene written" if (folder / item["scene"]).is_file() else "no scene yet"
                lines.append(f"graphic {name}: {item['seconds']:.2f} s, {len(item['beats'])} beats, "
                             f"{len(item['stills'])} stills; {scene}; {why or 'current'}")
            elif kind == "stills":
                lines.append(f"still {name}: body {item['body']} of {item['run']} at {item['second']} s; "
                             f"{why or 'current'}")
            else:
                lines.append(f"words {name}: '{item['words']}', {item['seconds']:.2f} s; {why or 'current'}")
    head = (f"{len(plan['graphics'])} graphics ({todo['graphics']} to render), {len(plan['stills'])} stills "
            f"({todo['stills']} to draw), {len(plan['words'])} word cards ({todo['words']} to draw)")
    verdict = "GRAPHICS PLAN: READY" if any(todo.values()) else "GRAPHICS PLAN: NOTHING TO DRAW"
    return verdict, [head, "plan: " + rel(folder / "graphics" / "plan.json")] + lines


def ffmpeg(args, cwd=None):
    done = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args], cwd=cwd,
                          capture_output=True, text=True)
    if done.returncode != 0:
        raise Refused("ffmpeg failed: " + (done.stderr.strip().splitlines() or ["no message"])[-1])


def probe(path):
    """A clip's decoded frame count, size and pixel format."""
    done = subprocess.run(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries",
                           "stream=nb_read_frames,width,height,pix_fmt", "-of", "json", str(path)],
                          capture_output=True, text=True)
    if done.returncode != 0:
        raise Refused(f"ffprobe cannot read {rel(path)}")
    st = json.loads(done.stdout)["streams"][0]
    return {"frames": int(st["nb_read_frames"]), "width": st["width"], "height": st["height"],
            "pix_fmt": st["pix_fmt"]}


def style_of(folder, s):
    return read_json(folder / s["style"])


def font_of(style, which, px):
    from PIL import ImageFont
    return ImageFont.truetype(str(repo_path(style["fonts"][which])), px)


def colour(style, name, alpha=255):
    h = style["colours"][name].lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha)


def word_card(style, text, width, height):
    """The words on screen as one transparent frame, in the film's look."""
    from PIL import Image, ImageDraw, ImageFilter
    w = style["words"]
    scale = height / 1080
    font = font_of(style, w["font"], round(w["size_px"] * scale))
    at = (round(w["x"] * width), round(w["y"] * height))
    shadow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text(at, text, font=font, anchor=w["anchor"], fill=(0, 0, 0, round(255 * w["shadow_alpha"])))
    shadow = shadow.filter(ImageFilter.GaussianBlur(w["shadow_px"] * scale))
    card = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    ImageDraw.Draw(card).text(at, text, font=font, anchor=w["anchor"], fill=colour(style, w["colour"]))
    box = card.getbbox()
    return Image.alpha_composite(shadow, card), box


def checker(size, square=16):
    """A grey checkerboard, to show transparency on a sheet."""
    from PIL import Image
    import numpy as np
    w, h = size
    y, x = np.mgrid[0:h, 0:w]
    grid = ((x // square + y // square) % 2).astype(np.uint8)
    rgb = np.where(grid[..., None] == 1, 96, 64).astype(np.uint8).repeat(3, axis=2)
    return Image.fromarray(rgb, "RGB").convert("RGBA")


def make_sheet(frames, labels, out, columns=4, tile=480):
    """A contact sheet: each frame over a checkerboard, its label in the corner."""
    from PIL import Image, ImageDraw
    first = frames[0]
    th = round(tile * first.height / first.width)
    rows = (len(frames) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * tile, rows * th), (20, 20, 20))
    for i, (frame, label) in enumerate(zip(frames, labels)):
        small = frame.convert("RGBA").resize((tile, th))
        cell = Image.alpha_composite(checker((tile, th)), small).convert("RGB")
        ImageDraw.Draw(cell).text((6, 4), label, fill=(255, 255, 0))
        sheet.paste(cell, ((i % columns) * tile, (i // columns) * th))
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)


def encode(sequence_dir, pattern, out, s, extra_filters=""):
    """A numbered PNG sequence as a transparent clip in the film's codec."""
    vf = "format=rgba" + ("," + extra_filters if extra_filters else "")
    ffmpeg(["-framerate", str(s["fps"]), "-i", str(sequence_dir / pattern), "-vf", vf, *CODECS[s["codec"]],
            str(out)])


def cmd_words(folder, final=False):
    lines, errors = [], []
    s = at_size(settings_of(folder, lines), final)
    plan = load_plan(folder)
    style = style_of(folder, s)
    records = records_of(folder)
    work = folder / "graphics" / "work" / "words"
    for item in plan["words"]:
        if stale(folder, "words", item, records) is None and not (final and records["words"][item["id"]].get("draft")):
            continue
        card, box = word_card(style, item["words"], s["width"], s["height"])
        where = f"words {item['id']}"
        if box is None or box[0] < 0.03 * s["width"] or box[2] > 0.97 * s["width"] or box[3] > 0.97 * s["height"]:
            errors.append(f"{where}: '{item['words']}' does not fit inside the frame's margins at the style's size")
            continue
        work.mkdir(parents=True, exist_ok=True)
        png = work / f"{item['id']}.png"
        card.save(png)
        out = fresh_path(folder, item, item["id"])
        out.parent.mkdir(parents=True, exist_ok=True)
        seconds, fade = item["frames"] / s["fps"], min(s["fade_s"], item["seconds"] / 3)
        vf = (f"format=rgba,fade=t=in:st=0:d={fade:.3f}:alpha=1,"
              f"fade=t=out:st={seconds - fade:.3f}:d={fade:.3f}:alpha=1")
        ffmpeg(["-loop", "1", "-framerate", str(s["fps"]), "-i", str(png), "-frames:v", str(item["frames"]),
                "-vf", vf, *CODECS[s["codec"]], str(out)])
        info = probe(out)
        if info["frames"] != item["frames"] or (info["width"], info["height"]) != (s["width"], s["height"]):
            errors.append(f"{where}: the clip holds {info['frames']} frames at {info['width']}x{info['height']} where "
                          f"{item['frames']} at {s['size']} were planned")
            continue
        file = settle(folder, out, item["id"])
        records["words"][item["id"]] = {"key": item["key"], "file": file, "sha256": sha256_of(folder / file),
                                        "size": s["size"], "draft": s["size"] != s["final_size"],
                                        "frames": info["frames"], "pix_fmt": info["pix_fmt"], "card": rel(png),
                                        "drawn": now()}
        lines.append(f"{where}: '{item['words']}', {info['frames']} frames, {info['pix_fmt']}")
    save_records(folder, records)
    cards = [item for item in plan["words"] if (work / f"{item['id']}.png").is_file()]
    if cards:
        from PIL import Image
        make_sheet([Image.open(work / f"{i['id']}.png") for i in cards], [f"{i['id']} {i['words']}" for i in cards],
                   folder / "graphics" / "sheets" / "words-sheet.png", columns=3)
        lines.append("sheet: " + rel(folder / "graphics" / "sheets" / "words-sheet.png"))
    left = [i["id"] for i in plan["words"] if stale(folder, "words", i, records)]
    if left:
        errors.append("word cards not drawn: " + ", ".join(left))
    return ("WORDS: FAIL" if errors else "WORDS: PASS"), lines + [f"ERROR {e}" for e in errors]


def plain_numbers(text):
    return re.sub(r"(?<=[0-9]),(?=[0-9])", "", text)


def given_names(folder):
    script = folder / "script.md"
    text = script.read_text(encoding="utf-8") if script.is_file() else ""
    return {sc.squash(g) for g in sc.GIVEN.findall(text)}


def scene_draws(scene):
    """A scene file's DRAWS list and whether it defines graphic(clock), read without running it."""
    tree = ast.parse(scene.read_text(encoding="utf-8"), filename=str(scene))
    draws, has_graphic = None, False
    for node in tree.body:
        if isinstance(node, ast.FunctionDef) and node.name == "graphic":
            has_graphic = True
        if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "DRAWS" for t in node.targets):
            draws = ast.literal_eval(node.value)
    return draws, has_graphic


def check_draws(folder, item, draws, errors):
    """Every label a graphic draws cites the places in facts.md that hold it, and its numbers are theirs."""
    where = f"graphic {item['id']}"
    if not isinstance(draws, list) or not draws or not all(
            isinstance(d, (list, tuple)) and len(d) == 2 and all(isinstance(x, str) for x in d) for d in draws):
        errors.append(f"{where}: the scene's DRAWS is a list of (label, places) pairs naming every word and number "
                      "it draws")
        return
    facts = (folder / "facts.md").read_text(encoding="utf-8").splitlines()
    names = None
    for label, places in draws:
        if places == "axis":
            continue
        if places == "":
            names = given_names(folder) if names is None else names
            m = sc.GIVEN.fullmatch(label)
            if not m or sc.squash(m.group(1)) not in names:
                errors.append(f"{where}: '{label}' cites no places and is not a name the script gives between braces")
            continue
        found, rest = sc.places_in(places)
        if rest or not found:
            errors.append(f"{where}: '{label}': places are written [facts.md:L<a>] or [facts.md:L<a>-<b>], "
                          "'axis' for a scale mark, or '' for a name we give")
            continue
        text = ""
        for p in found:
            if not 1 <= p["first"] <= p["last"] <= len(facts):
                errors.append(f"{where}: '{label}': [facts.md:L{p['first']}-{p['last']}] is not in facts.md, "
                              f"which has {len(facts)} lines")
            text += " ".join(facts[p["first"] - 1:p["last"]]) + " "
        text = plain_numbers(text)
        for n in DIGITS.findall(plain_numbers(label)):
            if not re.search("(?<![0-9.])" + re.escape(n) + "(?![0-9])", text):
                errors.append(f"{where}: '{label}' draws {n}, which its places do not hold")


def blender_ready(s):
    """Blender's version, or why it cannot run."""
    blender = repo_path(s["blender"])
    if not blender.is_file():
        return None, f"no Blender at {blender}; the owner installs it, and blender in {SETTINGS} names it"
    try:
        done = subprocess.run([str(blender), "-b", "--factory-startup", "--version"], capture_output=True, text=True,
                              encoding="utf-8", errors="replace", timeout=120)
    except subprocess.TimeoutExpired:
        return None, f"{blender} did not say its version within two minutes"
    m = re.search("Blender ([0-9][0-9.]*)", done.stdout)
    if done.returncode != 0 or not m:
        return None, f"{blender} did not say its version"
    return m.group(1), None


def run_blender(s, folder, item, work):
    """Render the scene's frames in Blender as a numbered, transparent PNG sequence in work/frames."""
    (work / "frames").mkdir()
    command = [str(repo_path(s["blender"])), "-b", "--factory-startup", "--python-exit-code", "1",
               "--python", str(HERE / "storyblender.py"), "--", str(folder / "graphics" / "plan.json"), item["id"],
               str(folder), str(folder / item["scene"]), str(work / "frames"), str(s["width"]), str(s["height"]),
               str(s["render_threads"])]
    try:
        done = subprocess.run(command, cwd=str(folder), capture_output=True, text=True, encoding="utf-8",
                              errors="replace", timeout=s["render_minutes"] * 60)
    except subprocess.TimeoutExpired:
        return f"the render took longer than {s['render_minutes']} minutes and was stopped"
    (work / "blender.log").write_text(done.stdout + NL + done.stderr, encoding="utf-8")
    if done.returncode != 0:
        said = [x for x in (done.stdout + NL + done.stderr).splitlines() if x.startswith("SCENE ERROR:")]
        tail = said or [x for x in (done.stdout + NL + done.stderr).splitlines() if x.strip()][-15:]
        return "Blender failed:" + NL + NL.join("  " + x for x in tail)
    return None


def rendered_frames(work):
    """The scene's frames in order, as Blender numbered them in work/frames."""
    return sorted((work / "frames").glob("f_*.png"), key=lambda p: int(p.stem[2:]))


def pixels(path):
    """A frame as an RGBA array at its own size, for the checks."""
    from PIL import Image
    import numpy as np
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8)


def changed(a, b):
    """The share of the frame whose colour or opacity moved between two frames."""
    import numpy as np
    return float((np.abs(a.astype(np.int16) - b.astype(np.int16)).max(axis=2) > 24).mean())


def shown(a):
    return float((a[..., 3] > 8).mean())


def cmd_render(folder, graphic_id, final=False):
    lines, errors = [], []
    s = at_size(settings_of(folder, lines), final)
    plan = load_plan(folder)
    item = next((g for g in plan["graphics"] if g["id"] == graphic_id), None)
    if item is None:
        raise Refused(f"the shot list holds no graphic {graphic_id}")
    scene = folder / item["scene"]
    if not scene.is_file():
        raise Refused(f"graphic {graphic_id} has no scene at {rel(scene)}")
    try:
        draws, has_graphic = scene_draws(scene)
    except (SyntaxError, ValueError) as exc:
        raise Refused(f"the scene {rel(scene)} does not parse, or its DRAWS is not a literal: {exc}")
    if not has_graphic:
        errors.append(f"graphic {graphic_id}: the scene defines no function graphic(clock)")
    check_draws(folder, item, draws, errors)
    records = records_of(folder)
    missing = [n for n in item["stills"] if stale(folder, "stills", next(x for x in plan["stills"] if x["name"] == n),
                                                  records)]
    if missing:
        raise Refused("these stills are not drawn yet: " + ", ".join(missing), "the stills command draws them")
    if errors:
        return "RENDER: FAIL", lines + [f"ERROR {e}" for e in errors]
    version, why = blender_ready(s)
    if why:
        raise Refused(why)
    work = folder / "graphics" / "work" / graphic_id
    shutil.rmtree(work, ignore_errors=True)
    work.mkdir(parents=True)
    scene_sha = sha256_of(scene)
    failed = run_blender(s, folder, item, work)
    if failed:
        return "RENDER: FAIL", lines + [f"ERROR graphic {graphic_id}: {failed}"]
    frames = rendered_frames(work)
    want, fps = item["frames"], s["fps"]
    lines.append(f"Blender {version} wrote {len(frames)} frames; the shot wants {want} ({item['seconds']:.3f} s at "
                 f"{fps} frames a second)")
    if len(frames) != want:
        return "RENDER: FAIL", lines + [f"ERROR graphic {graphic_id}: Blender wrote {len(frames)} frames where the "
                                        f"shot lasts {want}; its log is graphics/work/{graphic_id}/blender.log"]
    arrays = {}

    def at(f):
        f = min(max(f, 0), want - 1)
        if f not in arrays:
            arrays[f] = pixels(frames[f])
        return arrays[f]

    seen = []
    window = round(BEAT_WINDOW_S * fps)
    for i, beat in enumerate(item["beats"], 1):
        f0 = round(beat["t"] * fps)
        share = shown(at(f0 + window)) if f0 <= 0 else changed(at(f0 - 1), at(f0 + window))
        seen.append({"t": beat["t"], "change": round(share, 4)})
        state = "seen" if share >= BEAT_CHANGE else "NOT SEEN"
        lines.append(f"beat {i} at {beat['t']:.2f} s ({beat['shows']}): {share:.3%} of the frame changes; {state}")
        if share < BEAT_CHANGE:
            errors.append(f"graphic {graphic_id}: nothing new shows within {BEAT_WINDOW_S} s of beat {i} at "
                          f"{beat['t']:.2f} s")
    clear = [round(float((at(f)[..., 3] < 128).mean()), 3) for f in (want // 2, want - 1)]
    covered = [round(float((at(f)[..., 3] >= COVERED_ALPHA).mean()), 3) for f in (want // 2, want - 1)]
    over = item.get("over", "footage")
    lines.append(f"over {over}: the drawing covers {max(covered):.0%} of the frame at its middle and last frames")
    if over == "footage" and max(covered) > OVER_FOOTAGE_MAX:
        errors.append(f"graphic {graphic_id}: it lies over footage and covers {max(covered):.0%} of the frame; over "
                      "footage a graphic keeps to a third of the frame and never dims the picture, so that the "
                      "footage stays the star: use the corner card. A drawing that needs more stands alone, which "
                      "the shot list decides")
    import numpy as np
    last = at(want - 1)
    edge = max(2, round(0.02 * last.shape[1]))
    border = np.concatenate([last[:edge].reshape(-1, 4), last[-edge:].reshape(-1, 4), last[:, :edge].reshape(-1, 4),
                             last[:, -edge:].reshape(-1, 4)])
    if (border[:, 3] > 64).mean() > 0.01:          # above a full card's dim, which covers the edge by design
        lines.append(f"look: the last frame draws inside the outer 2% of the frame")
    if errors:
        return "RENDER: FAIL", lines + [f"ERROR {e}" for e in errors]
    sequence = work / "sequence"
    sequence.mkdir()
    for n, path in enumerate(frames, 1):
        shutil.copyfile(path, sequence / f"{n:06d}.png")
    out = fresh_path(folder, item, graphic_id)
    out.parent.mkdir(parents=True, exist_ok=True)
    h = s["handles_s"]
    pad = f"tpad=start_mode=clone:start_duration={h}:stop_mode=clone:stop_duration={h}" if h > 0 else ""
    encode(sequence, "%06d.png", out, s, pad)
    handle = round(h * fps)
    info = probe(out)
    if info["frames"] != want + 2 * handle:
        return "RENDER: FAIL", lines + [f"ERROR graphic {graphic_id}: the clip holds {info['frames']} frames where "
                                        f"{want + 2 * handle} were planned"]
    marks = sorted({0, *(min(want - 1, round(b["t"] * fps) + window) for b in item["beats"]), want // 2, want - 1})[:8]
    from PIL import Image
    make_sheet([Image.open(frames[f]) for f in marks], [f"{graphic_id} t={f / fps:.2f} s" for f in marks],
               folder / "graphics" / "sheets" / f"{graphic_id}-sheet.png")
    file = settle(folder, out, graphic_id)
    # Read the records again just before writing: two animators may render at once, and a copy read at the
    # start would drop a record the other wrote meanwhile.
    records = records_of(folder)
    records["graphics"][graphic_id] = {
        "key": item["key"], "file": file, "sha256": sha256_of(folder / file), "frames": info["frames"],
        "action_frames": want, "handle_frames": handle, "fps": fps, "width": info["width"], "height": info["height"],
        "pix_fmt": info["pix_fmt"], "scene_sha256": scene_sha, "size": s["size"],
        "draft": s["size"] != s["final_size"],
        "stills_sha256": {n: records["stills"][n]["sha256"] for n in item["stills"]},
        "draws": [list(d) for d in draws], "beats_seen": seen, "clear": clear, "covered": covered, "over": over,
        "sheet": f"graphics/sheets/{graphic_id}-sheet.png", "sheet_times_s": [round(f / fps, 2) for f in marks],
        "blender": version, "rendered": now()}
    save_records(folder, records)
    lines.append(f"clip: {file}, {info['frames']} frames ({handle} held each side) at {s['size']}"
                 f"{' (draft)' if s['size'] != s['final_size'] else ''}, {info['pix_fmt']}")
    lines.append(f"sheet: graphics/sheets/{graphic_id}-sheet.png")
    return "RENDER: PASS", lines


def film_tool():
    spec = importlib.util.spec_from_file_location("film_tool", HERE / "film.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def run_dir(runs_root, arm):
    base = runs_root / arm
    found = sorted(d for d in base.iterdir() if d.is_dir() and ((d / "run.json").is_file()
                                                                or (d / "config.json").is_file())) if base.is_dir() else []
    return found[-1] if found else None


def genome_row(rd, body, second, tries=30):
    """The body's snapshot row nearest the second, the last one at or before it first, and that snapshot's second."""
    sys.path.insert(0, str(REPO / "scripts" / "reads"))
    import runrec
    seconds = runrec.snapshot_seconds(str(rd))
    before = [x for x in seconds if x <= second][::-1]
    after = [x for x in seconds if x > second]
    order = [x for pair in zip(before, after) for x in pair] + before[len(after):] + after[len(before):]
    for at in order[:tries]:
        snap = runrec.snapshot(str(rd), at, ids={int(body)})
        if snap:
            return snap.lines[0], at
    return None, None


def launch_stills(s, folder, items, listing):
    """Draw the listed stills in the theatre: one Editor launch on the render worker. Returns why it failed, or None."""
    tool = film_tool()
    worker = REPO / f"unity-w{s['worker']}"
    entry = worker / Path(SOLO_FILE).relative_to("unity")
    if not (REPO / SOLO_FILE).is_file():
        return f"the theatre has no stills entry: {SOLO_FILE} is not in the tree"
    if not entry.is_file():
        return (f"{worker.name} has no stills entry; refresh it from PowerShell with "
                f"./scripts/new-worker.ps1 -Workers {s['worker']}")
    stale = tool.stale_worker(s)
    if stale:
        return stale
    if (worker / "Temp" / "UnityLockfile").exists():
        return f"the render worker's lock is held: {worker / 'Temp' / 'UnityLockfile'}"
    verdict, found = tool.narration_tool().cmd_machine()
    if verdict.endswith("BUSY"):
        return "the machine is busy: " + "; ".join(found)
    log = REPO / "scratch" / "logs" / f"stills-{folder.name}.log"
    log.parent.mkdir(parents=True, exist_ok=True)
    unity_args = ["-projectPath", str(worker), "-batchmode", "-executeMethod", SOLO_ENTRY, "-job-worker-count", "4",
                  "-logFile", str(log)]
    quoted = ", ".join("'" + a.replace("'", "''") + "'" for a in unity_args)
    command = (f"$p = Start-Process -FilePath '{s['unity']}' -ArgumentList @({quoted}) -NoNewWindow -PassThru -Wait; "
               "exit $p.ExitCode")
    env = dict(os.environ)
    env["EVOSIM_STILLS_LIST"] = str(listing)
    env["EVOSIM_THEATRE_WALL_MINUTES"] = str(s["still_wall_minutes"])
    env["EVOSIM_REPO_ROOT"] = str(REPO)
    try:
        done = subprocess.run(["pwsh", "-NoProfile", "-Command", command], env=env, capture_output=True, text=True,
                              timeout=(s["still_wall_minutes"] + 10) * 60)
    except subprocess.TimeoutExpired:
        return (f"the Editor ran past its wall of {s['still_wall_minutes']} minutes and ten more; it may still be "
                f"running on {worker.name}, so look for it before anything else launches there (log: {rel(log)})")
    result = read_json(listing.parent / "result.json")
    if done.returncode != 0 or result is None:
        whys = [f"{x['name']}: {x['why']}" for x in (result or {}).get("items", []) if not x.get("drawn")]
        return (f"the Editor exited with code {done.returncode}; its log is {rel(log)}"
                + ("; " + "; ".join(whys) if whys else ""))
    return None


def check_still(path, size):
    """What is wrong with a still, or None: its size, its transparency, and whether the body lies inside the frame."""
    from PIL import Image
    import numpy as np
    if not path.is_file():
        return "the theatre wrote no picture"
    im = Image.open(path)
    if im.mode != "RGBA" or im.size != (size, size):
        return f"the picture is {im.mode} at {im.size[0]}x{im.size[1]} where RGBA at {size}x{size} was asked"
    a = np.asarray(im)[..., 3]
    body = a > 8
    if body.mean() < 0.01:
        return "the picture is empty: the body covers under 1% of it"
    if (a == 0).mean() < 0.2:
        return "the background is not transparent: under 20% of the picture is clear"
    ys, xs = np.nonzero(body)
    edge = max(2, size // 100)
    if xs.min() < edge or ys.min() < edge or xs.max() >= size - edge or ys.max() >= size - edge:
        return "the body touches the picture's edge"
    return None


def cmd_stills(folder):
    lines, errors = [], []
    s = settings_of(folder, lines)
    plan = load_plan(folder)
    records = records_of(folder)
    todo = [x for x in plan["stills"] if stale(folder, "stills", x, records)]
    if not todo:
        return "STILLS: PASS", lines + [f"all {len(plan['stills'])} stills are current"]
    runs_root = repo_path(s["runs_root"])
    work = folder / "graphics" / "work" / "stills"
    shutil.rmtree(work, ignore_errors=True)
    work.mkdir(parents=True)
    listed = []
    for item in todo:
        where = f"still {item['name']}"
        rd = run_dir(runs_root, item["run"])
        if rd is None:
            errors.append(f"{where}: no run {item['run']} under {rel(runs_root)}")
            continue
        line, at = genome_row(rd, item["body"], item["second"])
        if line is None:
            errors.append(f"{where}: body {item['body']} is in none of {item['run']}'s 30 snapshots nearest "
                          f"{item['second']} s")
            continue
        genome = folder / "graphics" / "stills" / f"{item['name']}.genome.json"
        genome.parent.mkdir(parents=True, exist_ok=True)
        genome.write_text(line + chr(10), encoding="utf-8")
        png = folder / item["png"]
        if png.exists():
            png.unlink()
        listed.append({**item, "genome_file": str(genome), "run_dir": str(rd), "config": str(rd / "config.json"),
                       "out": str(png), "size": s["still_size"], "snapshot_s": at})
        lines.append(f"{where}: body {item['body']} from {rd.name}'s snapshot at {at} s")
    if errors:
        return "STILLS: FAIL", lines + [f"ERROR {e}" for e in errors]
    listing = work / "list.json"
    write_json(listing, {"schema": "create-story-video/stills-list/1", "background": "transparent",
                         "items": [{"name": x["name"], "genome_file": x["genome_file"], "run_dir": x["run_dir"],
                                    "png_file": x["out"], "size": x["size"]} for x in listed]})
    failed = launch_stills(s, folder, listed, listing)
    if failed:
        return "STILLS: FAIL", lines + [f"ERROR {failed}"]
    from PIL import Image
    for x in listed:
        png = folder / x["png"]
        why = check_still(png, s["still_size"])
        if why:
            errors.append(f"still {x['name']}: {why}")
            continue
        records["stills"][x["name"]] = {"key": x["key"], "png": x["png"], "sha256": sha256_of(png),
                                        "run_dir": rel(x["run_dir"]), "snapshot_s": x["snapshot_s"],
                                        "genome": rel(x["genome_file"]), "drawn": now()}
    save_records(folder, records)
    drawn = [x for x in plan["stills"] if (folder / x["png"]).is_file()]
    if drawn:
        make_sheet([Image.open(folder / x["png"]) for x in drawn], [x["name"] for x in drawn],
                   folder / "graphics" / "sheets" / "stills-sheet.png", columns=3)
        lines.append("sheet: " + rel(folder / "graphics" / "sheets" / "stills-sheet.png"))
    return ("STILLS: FAIL" if errors else "STILLS: PASS"), lines + [f"ERROR {e}" for e in errors]


def cmd_setup(folder):
    lines = []
    s = settings_of(folder, lines)
    version, why = blender_ready(s)
    if not version:
        return "BLENDER: MISSING", lines + [why]
    state = "READY" if version == s["blender_version"] else "WRONG VERSION"
    return f"BLENDER: {state}", lines + [f"Blender {version} at {s['blender']}; the settings ask for "
                                         f"{s['blender_version']}"]


def cmd_check(folder):
    lines, errors = [], []
    s = settings_of(folder, lines)
    plan = load_plan(folder)
    records = records_of(folder)
    current = build_plan(folder, s, shots_of(folder))
    strip = lambda p: [{k: v for k, v in x.items() if k != "made"} for x in p]
    for kind in ("graphics", "stills", "words"):
        if strip(current[kind]) != strip(plan[kind]):
            errors.append(f"the {kind} planned differ from what the shot list and settings ask now; run plan again")
    for kind, items in (("stills", plan["stills"]), ("graphics", plan["graphics"]), ("words", plan["words"])):
        for item in items:
            name = item["name"] if kind == "stills" else item["id"]
            why = stale(folder, kind, item, records)
            if why:
                errors.append(f"{kind[:-1] if kind != 'graphics' else 'graphic'} {name}: {why}")
            else:
                lines.append(f"{kind[:-1] if kind != 'graphics' else 'graphic'} {name}: current")
    for item in plan["graphics"]:
        r = records["graphics"].get(item["id"])
        if r and (folder / item["scene"]).is_file():
            draws, _ = scene_draws(folder / item["scene"])
            check_draws(folder, item, draws, errors)
    head = (f"{len(plan['graphics'])} graphics, {len(plan['stills'])} stills and {len(plan['words'])} word cards "
            f"planned; graphics.json holds {len(records['graphics'])}, {len(records['stills'])} and "
            f"{len(records['words'])}")
    drafts = sorted(i for kind in ("graphics", "words") for i, r in records[kind].items() if r.get("draft"))
    if drafts:
        lines.append(f"drafts: {len(drafts)} drawn at the draft size, which finish renders at {s['size']} before "
                     f"the final edit: " + ", ".join(drafts))
    return ("GRAPHICS: FAIL" if errors else "GRAPHICS: PASS"), [head] + lines + [f"ERROR {e}" for e in errors]


def cmd_finish(folder):
    """Every graphic and word card the reviewer passed at the draft size, rendered again at the film's size from
    the same scene. A graphic not current at any size is refused: it is drawn and reviewed first."""
    lines, errors = [], []
    s = settings_of(folder, lines)
    plan = load_plan(folder)
    records = records_of(folder)
    behind = [f"graphic {g['id']}: {why}" for g in plan["graphics"] if (why := stale(folder, "graphics", g, records))]
    behind += [f"words {w['id']}: {why}" for w in plan["words"] if (why := stale(folder, "words", w, records))]
    if behind:
        raise Refused("these are not drawn and current, so there is nothing of theirs to finish:", *behind)
    for g in plan["graphics"]:
        if not records["graphics"][g["id"]].get("draft"):
            continue
        verdict, said = cmd_render(folder, g["id"], final=True)
        lines.append(f"graphic {g['id']}: {verdict}")
        if verdict != "RENDER: PASS":
            errors += [f"graphic {g['id']}: {x}" for x in said if x.startswith("ERROR")] or [f"graphic {g['id']}: {verdict}"]
        records = records_of(folder)
    if any(r.get("draft") for r in records["words"].values()):
        verdict, said = cmd_words(folder, final=True)
        lines.append(f"words: {verdict}")
        errors += [x[6:] for x in said if x.startswith("ERROR")]
    records = records_of(folder)
    left = sorted(i for kind in ("graphics", "words") for i, r in records[kind].items() if r.get("draft"))
    if left:
        errors.append("still at the draft size: " + ", ".join(left))
    lines.append(f"{len(plan['graphics'])} graphics and {len(plan['words'])} word cards at {s['size']}")
    return ("FINISH: FAIL" if errors else "FINISH: PASS"), lines + [f"ERROR {e}" for e in errors]


COMMANDS = {"plan": (lambda f: cmd_plan(f), "GRAPHICS PLAN: REFUSED", ["folder"]),
            "setup": (lambda f: cmd_setup(f), "BLENDER: REFUSED", ["folder"]),
            "stills": (lambda f: cmd_stills(f), "STILLS: REFUSED", ["folder"]),
            "words": (lambda f: cmd_words(f), "WORDS: REFUSED", ["folder"]),
            "render": (lambda f, i: cmd_render(f, i), "RENDER: REFUSED", ["folder", "id"]),
            "check": (lambda f: cmd_check(f), "GRAPHICS: REFUSED", ["folder"]),
            "finish": (lambda f: cmd_finish(f), "FINISH: REFUSED", ["folder"])}
PASSING = ("READY", "NOTHING TO DRAW", "PASS")


def parse_args(argv):
    spec = COMMANDS.get(argv[1]) if len(argv) > 1 else None
    if spec is None:
        return None
    kinds, values = spec[2], argv[2:]
    needed = [k for k in kinds if not k.endswith("?")]
    if not len(needed) <= len(values) <= len(kinds):
        return None
    args = []
    for kind, v in zip(kinds, values):
        if kind == "folder":
            args.append(Path(v).resolve())
        elif kind == "id" and SHOT_ID.fullmatch(v):
            args.append(v)
        else:
            return None
    return spec, args


def main(argv):
    started = time.monotonic()
    parsed = parse_args(argv)
    if parsed is None:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    (command, refused, _), args = parsed
    if not args[0].is_dir():
        verdict, lines = refused, [f"cannot read the film's folder {args[0]}"]
    else:
        try:
            verdict, lines = command(*args)
        except Refused as exc:
            verdict, lines = refused, exc.lines
        except (ValueError, TypeError, KeyError, IndexError, AttributeError, OSError) as exc:
            verdict, lines = refused, [f"a file is not in its form or cannot be read: {type(exc).__name__}: {exc}"]
    print(verdict)
    for line in lines:
        print(line)
    print(f"took {time.monotonic() - started:.1f} s")
    return 0 if verdict.endswith(PASSING) else 1


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
