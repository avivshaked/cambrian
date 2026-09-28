#!/usr/bin/env python3
"""The edit step's tool, for the create-story-video skill (edit.md).

    python edit.py plan <folder> <n>      lay draft n of the edit out on one timeline, with the editor's decisions
    python edit.py proof <folder> <n>     make draft n's proof: a small film of the plan, by ffmpeg, and its sheet
    python edit.py resolve <folder>       say whether Resolve is reachable and what the settings point at
    python edit.py build <folder> <n>     build draft n in Resolve, as a new bin and timeline <name> vK

The first line each command prints is its verdict, as edit.md reads it. The last line says how long the command
took. Exit status: 0 on READY or PASS, 1 otherwise, 2 on a wrong command. `plan` and `proof` use the standard
library, Pillow, ffmpeg and ffprobe; `resolve` and `build` also use Resolve's own scripting module, whose
calls are copied from scripts/story-resolve.py and whose behaviour is in logbook/specs/story-resolve.md.
"""

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
SETTINGS = "edit-settings.json"
PLAN_SCHEMA = "create-story-video/edit-plan/1"
DECISIONS_SCHEMA = "create-story-video/edit-decisions/1"
SIZE = re.compile(r"([0-9]+)x([0-9]+)")
HEX = re.compile(r"#[0-9A-Fa-f]{6}")
STAMP = re.compile(r"-[0-9]{12}$")
BACKSLASH = chr(92)
RESOLVE_DEFAULTS = {
    "EVOSIM_RESOLVE_PROJECT": "", "EVOSIM_RESOLVE_BIN": "Stories", "EVOSIM_RESOLVE_WIDTH": "3840",
    "EVOSIM_RESOLVE_HEIGHT": "2160", "EVOSIM_RESOLVE_FPS": "30", "EVOSIM_RESOLVE_MUSIC": "",
    "EVOSIM_RESOLVE_MUSIC_FADE": "3",
    "RESOLVE_SCRIPT_API": os.path.join(os.environ.get("PROGRAMDATA", "C:/ProgramData"), "Blackmagic Design",
                                       "DaVinci Resolve", "Support", "Developer", "Scripting"),
    "RESOLVE_SCRIPT_LIB": os.path.join(os.environ.get("PROGRAMFILES", "C:/Program Files"), "Blackmagic Design",
                                       "DaVinci Resolve", "fusionscript.dll")}

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


def number(x):
    return isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x)


def now():
    return datetime.now().isoformat(timespec="seconds")


def repo_path(value):
    p = Path(value)
    return p if p.is_absolute() else REPO / p


def settings_of(folder, lines):
    """The film's edit settings, first copied from the skill's if the film has none."""
    if not (folder / SETTINGS).is_file():
        shutil.copyfile(HERE / SETTINGS, folder / SETTINGS)
        lines.append(f"copied the skill's {SETTINGS} into the film's folder")
    s = read_json(folder / SETTINGS)
    problems = []
    if not isinstance(s.get("name"), str):
        problems.append("name is the timeline's name before its vK, or \"\" for the film folder's")
    if not isinstance(s.get("label"), bool):
        problems.append("label is true or false: the provenance label over the footage")
    if not isinstance(s.get("subtitle_chars"), int) or not 20 <= s["subtitle_chars"] <= 80:
        problems.append("subtitle_chars is a whole number, 20 to 80: a subtitle line's longest")
    if s.get("subtitle_lines") not in (1, 2):
        problems.append("subtitle_lines is 1 or 2")
    if not isinstance(s.get("plate"), str) or not HEX.fullmatch(s["plate"]):
        problems.append("plate is a colour, #RRGGBB: what shows where no footage covers a graphic")
    m = SIZE.fullmatch(str(s.get("proof_size", "")))
    if not m or int(m.group(1)) % 2 or int(m.group(2)) % 2:
        problems.append("proof_size is <width>x<height>, both even")
    if not isinstance(s.get("threads"), int) or not 1 <= s["threads"] <= 16:
        problems.append("threads is a whole number, 1 to 16: ffmpeg's, at most half the machine")
    i = s.get("intro", "missing")
    if i is not None and not (isinstance(i, dict) and set(i) == {"file", "after_act"} and isinstance(i["file"], str)
                              and i["file"] and isinstance(i["after_act"], int) and i["after_act"] >= 1):
        problems.append("intro is null (none) or {file: the card's path, absolute or from the checkout's root, "
                        "after_act: the act it follows, 1 for the hook}")
    g = s.get("grade", "missing")
    if g is not None and not (isinstance(g, dict) and set(g) == {"most", "power"} and number(g["most"])
                              and 1 <= g["most"] <= 3 and number(g["power"]) and 0.5 <= g["power"] <= 1.5):
        problems.append("grade is null (no grade) or {most: 1 to 3, power: 0.5 to 1.5}: each clip's contrast "
                        "stretched towards the reference's tone by at most most, then a power")
    if not isinstance(s.get("rough"), bool):
        problems.append("rough is true or false: a draft laid out before every clip and graphic is in")
    if problems:
        raise Refused(f"the film's {SETTINGS} is not in its form:", *("  " + p for p in problems))
    s["proof_w"], s["proof_h"] = int(m.group(1)), int(m.group(2))
    if not s["name"]:
        s["name"] = STAMP.sub("", folder.name)
    return s


def load_dotenv(path):
    """KEY=value lines from a .env file (story-resolve.py's reader), or nothing when there is no file."""
    found = {}
    if not path.is_file():
        return found
    for raw in path.read_text(encoding="utf-8-sig").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        value = value.strip()
        if " #" in value:
            value = value.split(" #", 1)[0].rstrip()
        found[key.strip()] = value.strip('"').strip("'")
    return found


def resolve_config():
    """Resolve's settings for this machine: story-resolve.py's keys, from .env at the checkout's root, then the
    environment."""
    cfg = dict(RESOLVE_DEFAULTS)
    for key, value in load_dotenv(REPO / ".env").items():
        if key in cfg:
            cfg[key] = value
    for key in cfg:
        if os.environ.get(key):
            cfg[key] = os.environ[key]
    return cfg


def inputs(folder, fps, rough=False):
    """Everything the edit lays out, each checked against the shot list it was made for and the file it names. A
    rough draft leaves out what is not in yet and returns it in `left`: a clip, a graphic or a word card."""
    need = ["narration.json", "shots.json", "clips.json", "graphics.json", "graphics/plan.json"]
    missing = [n for n in need if not (folder / n).is_file()]
    if missing:
        raise Refused("the film's folder lacks " + ", ".join(missing) + "; the steps before the edit write them")
    got = {n: read_json(folder / n) for n in need}
    shots_sha = sha256_of(folder / "shots.json")
    errors, left = [], {"clip": set(), "graphic": set(), "words": set()}
    for n in ("clips.json", "graphics.json", "graphics/plan.json"):
        if got[n].get("shots_sha256") != shots_sha:
            errors.append(f"{n} was made for another shot list; its step runs again first")
    clips = {c["shot"]: c for c in got["clips.json"]["clips"]}
    drawn = got["graphics.json"]
    for shot in got["shots.json"]["shots"]:
        # Every shot's footage has its clip, and a graphic its drawing too; one that stands alone has no clip.
        wants = [("clip", clips.get(shot["id"]))] if "run" in shot else []
        if shot["kind"] == "graphic":
            wants.append(("graphic", drawn["graphics"].get(shot["id"])))
        for what, rec in wants:
            if rough and (rec is None or not (folder / rec["file"]).is_file()):
                left[what].add(shot["id"])
            elif rec is None:
                errors.append(f"shot {shot['id']} has no {what}")
            elif rec.get("fps", fps) != fps:
                errors.append(f"shot {shot['id']}'s {what} runs at {rec['fps']} frames a second and the timeline at "
                              f"{fps}")
            elif not (folder / rec["file"]).is_file():
                errors.append(f"shot {shot['id']}'s file {rec['file']} is missing")
            elif rec.get("draft") and not rough:
                errors.append(f"shot {shot['id']}'s {what} is a draft at {rec.get('size')}; graphics.py finish "
                              f"renders it at the film's size")
    for w in got["graphics/plan.json"]["words"]:
        if not rough and drawn["words"].get(w["id"], {}).get("draft"):
            errors.append(f"words {w['id']} are a draft; graphics.py finish renders them at the film's size")
        elif w["id"] not in drawn["words"] or not (folder / drawn["words"][w["id"]]["file"]).is_file():
            if rough:
                left["words"].add(w["id"])
            else:
                errors.append(f"words {w['id']} are not drawn")
    for p in got["narration.json"]["pieces"]:
        if not (folder / p["file"]["path"]).is_file():
            errors.append(f"narration piece {p['id']}'s file {p['file']['path']} is missing")
    if errors:
        raise Refused("the edit cannot be laid out:", *("  " + e for e in errors))
    for sid in left["clip"]:
        clips.pop(sid, None)
    return got, clips, left


def decisions_of(folder, n, shots):
    """The editor's decisions for draft n, from edit-<n>.json, or none."""
    d = read_json(folder / f"edit-{n}.json")
    if d is None:
        return {"cuts": {}}, []
    errors = []
    if d.get("schema") != DECISIONS_SCHEMA or set(d) - {"schema", "cuts"}:
        errors.append(f"edit-{n}.json holds schema and cuts, and its schema is {DECISIONS_SCHEMA}")
    by_id = {s["id"]: i for i, s in enumerate(shots)}
    cuts = {}
    for c in d.get("cuts", []):
        i = by_id.get(c.get("after"))
        if i is None or i + 1 >= len(shots) or "run" not in shots[i] or "run" not in shots[i + 1]:
            errors.append(f"cut after {c.get('after')}: a cut is moved only between two shots with footage")
        elif not number(c.get("move_s")) or abs(c["move_s"]) > 2:
            errors.append(f"cut after {c['after']}: move_s is seconds, -2 to 2")
        else:
            cuts[c["after"]] = c["move_s"]
    return {"cuts": cuts}, errors


def clip_room(clip, fps):
    """Frames a clip holds before its action and after it."""
    total = clip["frames"]
    a, b = round(clip["action_in_s"] * fps), round(clip["action_out_s"] * fps)
    return a, max(0, total - b)


def lay_picture(shots, clips, decisions, fps, errors):
    """V1: every shot's own footage at its shot, a graphic's included, and a cut moved where the editor says.
    A graphic that stands alone has none; the caller fills its time with the plate."""
    F = lambda t: round(t * fps)
    span = {}
    for s in shots:
        if "run" in s:
            span[s["id"]] = [F(s["from_s"]), F(s["to_s"])]
    for i, s in enumerate(shots[:-1]):
        move = decisions["cuts"].get(s["id"])
        if move is not None:
            m = F(move)
            room_after, room_before = clip_room(clips[s["id"]], fps)[1], clip_room(clips[shots[i + 1]["id"]], fps)[0]
            if m > room_after or -m > room_before:
                errors.append(f"cut after {s['id']}: moving it {move} s runs past a handle")
            else:
                span[s["id"]][1] += m
                span[shots[i + 1]["id"]][0] += m
    items = []
    for s in shots:
        if "run" not in s:
            continue
        a, b = span[s["id"]]
        c = clips[s["id"]]
        src = round(c["action_in_s"] * fps) + a - F(s["from_s"])
        if b - a < 1 or src < 0 or src + (b - a) > c["frames"]:
            errors.append(f"shot {s['id']}: frames {src} to {src + b - a} of its clip are not all in it")
            continue
        items.append({"kind": "clip", "shot": s["id"], "file": c["file"], "record": a, "frames": b - a, "src_in": src,
                      "run": c["run"], "run_s": round(c["clip_start_s"] + src / fps, 3),
                      "provenance": c.get("provenance", "")})
    return sorted(items, key=lambda x: x["record"])


def split_even(words, parts):
    """Words cut into `parts` runs of about equal length in characters, at word boundaries."""
    total = sum(len(w) + 1 for w in words)
    out, run, used = [], [], 0
    for w in words:
        run.append(w)
        used += len(w) + 1
        if len(out) < parts - 1 and used >= total * (len(out) + 1) / parts:
            out.append(run)
            run = []
    if run:
        out.append(run)
    return out


def cues_of(passages, s, fps):
    """The subtitles: each sentence as it is spoken, cut into cues of at most the settings' lines, each cue's
    time in proportion to its characters."""
    width, lines_max = s["subtitle_chars"], s["subtitle_lines"]
    cues = []
    for p in passages:
        for sent in p["sentences"]:
            if not (number(sent.get("from_s")) and number(sent.get("to_s"))):
                continue
            words = sc.unmark(sent["text"]).split()
            text = " ".join(words)
            parts = max(1, math.ceil(len(text) / (width * lines_max)))
            chunks = split_even(words, parts)
            weights = [len(" ".join(c)) for c in chunks]
            at, a, b = 0, sent["from_s"], sent["to_s"]
            for chunk, weight in zip(chunks, weights):
                start = a + (b - a) * at / sum(weights)
                at += weight
                end = a + (b - a) * at / sum(weights)
                body = " ".join(chunk)
                if lines_max == 2 and len(body) > width:
                    body = chr(10).join(" ".join(x) for x in split_even(chunk, 2))
                cues.append((round(start * fps), round(end * fps), body))
    return cues


def srt_stamp(frame, fps):
    ms = round(frame * 1000 / fps)
    return "%02d:%02d:%02d,%03d" % (ms // 3600000, ms // 60000 % 60, ms // 1000 % 60, ms % 1000)


def write_srt(folder, cues, fps):
    """The subtitles as an SRT whose name carries a hash of its cues: Resolve reuses an imported file by its path."""
    blocks = [f"{i}{chr(10)}{srt_stamp(a, fps)} --> {srt_stamp(b, fps)}{chr(10)}{text}{chr(10)}"
              for i, (a, b, text) in enumerate(cues, 1)]
    body = chr(10).join(blocks)
    path = folder / "edit" / f"subtitles-{hashlib.sha256(body.encode()).hexdigest()[:10]}.srt"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body, encoding="utf-8")
    return path


def ass_time(t):
    cs = max(0, round(t * 100))
    return "%d:%02d:%02d.%02d" % (cs // 360000, cs // 6000 % 60, cs // 100 % 60, cs % 100)


def label_events(item, fps):
    """The provenance label's ticks over one clip: the run's second, changing as each whole second passes."""
    r0, length = item["run_s"], item["frames"] / fps
    events, t = [], 0.0
    while t < length:
        second = math.floor(r0 + t + 1e-6)
        nxt = min(length, second + 1 - r0)
        events.append((t, nxt, f"{item['provenance'] or 'UNVERIFIED'} · {item['run']} · {second:,} s"))
        t = nxt
    return events


def filter_path(path, cwd):
    """A path for an ffmpeg filter option (story-resolve.py's): relative where it can be, colons escaped."""
    try:
        path = os.path.relpath(path, cwd)
    except ValueError:
        path = os.path.abspath(path)
    return str(path).replace(BACKSLASH, "/").replace(":", BACKSLASH + ":")


def render_label(folder, item, fps, width, height, threads):
    """One clip's label, drawn as story-resolve.py draws it: IBM Plex Mono at the top left of a transparent frame."""
    events = label_events(item, fps)
    key = hashlib.sha256(json.dumps([events, width, height, item["frames"], fps]).encode()).hexdigest()[:12]
    out = folder / "edit" / "labels" / f"label-{key}.mov"
    if out.is_file():
        return out
    out.parent.mkdir(parents=True, exist_ok=True)
    k = height / 1080
    style = ("Style: Label,IBM Plex Mono,%d,&H4CFFFFFF,&H000000FF,&H7F000000,&HFF000000,0,0,0,0,100,100,0.6,0,1,%.1f,"
             "0,7,%d,%d,%d,1" % (round(23 * k), 1.2 * k, round(32 * k), round(32 * k), round(26 * k)))
    lines = ["[Script Info]", "ScriptType: v4.00+", "WrapStyle: 0", "ScaledBorderAndShadow: yes",
             f"PlayResX: {width}", f"PlayResY: {height}", "", "[V4+ Styles]",
             "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, "
             "Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, "
             "MarginL, MarginR, MarginV, Encoding", style, "", "[Events]",
             "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text"]
    lines += [f"Dialogue: 0,{ass_time(a)},{ass_time(b)},Label,,0,0,0,,{words}" for a, b, words in events]
    ass = out.with_suffix(".ass")
    ass.write_text(chr(10).join(lines) + chr(10), encoding="utf-8-sig")
    fonts = REPO / "unity" / "Assets" / "Theatre" / "UI" / "Fonts"
    cwd = out.parent
    vf = f"subtitles=filename='{filter_path(ass, cwd)}':fontsdir='{filter_path(fonts, cwd)}':alpha=1"
    done = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i",
                           f"color=c=black@0.0:s={width}x{height}:r={fps}:d={item['frames'] / fps + 1:.3f},format=rgba",
                           "-vf", vf, "-frames:v", str(item["frames"]), "-c:v", "qtrle", "-pix_fmt", "argb",
                           "-threads", str(threads), out.name], cwd=cwd, capture_output=True, text=True)
    if done.returncode != 0 or not out.is_file():
        raise Refused(f"ffmpeg did not draw the label for shot {item['shot']}: " + done.stderr.strip()[-300:])
    return out


def grade_of(clip, target, g):
    """A clip's grade, as an ASC CDL (out = (in * slope + offset) ^ power, in 0 to 1): its contrast stretched
    about its own mean colour until its tone spread reaches the target, by at least 1 and at most g's most,
    then g's power. The owner chose it on round 49's proposal sheet (2026-09-28)."""
    k = min(g["most"], max(1.0, target / clip["tone"])) if clip["tone"] > 0 else g["most"]
    return {"slope": round(k, 4), "offset": [round((1 - k) * v / 255, 5) for v in clip["rgb"]], "power": g["power"]}


def act_of(shot):
    """A shot's act: the first number of its passage ("1.4" is act 1)."""
    return int(str(shot["passage"]).split(".")[0])


def intro_of(setting, shots, fps, errors):
    """The channel intro: its file and length, and the frame it starts at, straight after the last shot of the
    act it follows (the owner, 2026-09-28: "right after the hook")."""
    path = repo_path(setting["file"])
    if not path.is_file():
        errors.append(f"the intro card {path} is not there")
        return None
    info = probe(path)
    rate = info.get("fps")
    if rate is not None and abs(rate - fps) > 0.01:
        errors.append(f"the intro card runs at {rate} frames a second and the timeline at {fps}")
    before = [x for x in shots if act_of(x) <= setting["after_act"]]
    after = [x for x in shots if act_of(x) > setting["after_act"]]
    if not before or not after:
        errors.append(f"act {setting['after_act']} is not followed by another act, so the intro has no place")
        return None
    at = round(max(x["to_s"] for x in before) * fps)
    if round(min(x["from_s"] for x in after) * fps) < at:
        errors.append(f"a shot after act {setting['after_act']} starts before its last shot ends")
    return {"kind": "intro", "shot": None, "file": str(path), "record": at, "frames": info["frames"]}


def lut_of(grade):
    """The grade as ffmpeg's lutrgb, for the proof."""
    k, p = grade["slope"], grade["power"]
    return "lutrgb=" + ":".join(f"{c}='255*pow(clip(val/255*{k}+{o},0,1),{p})'" for c, o in zip("rgb", grade["offset"]))


def make_plate(folder, colour, width, height, fps, frames):
    """A plain clip of the plate's colour: what the picture shows where no footage reaches."""
    out = folder / "edit" / "plates" / f"plate-{colour[1:]}-{width}x{height}-{frames}.mp4"
    if not out.is_file():
        out.parent.mkdir(parents=True, exist_ok=True)
        ffmpeg(["-f", "lavfi", "-i", f"color=c=0x{colour[1:]}:s={width}x{height}:r={fps}", "-frames:v", str(frames),
                "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", str(out)])
    return out.relative_to(folder).as_posix()


def cmd_plan(folder, n):
    lines, errors = [], []
    s = settings_of(folder, lines)
    cfg = resolve_config()
    fps = int(float(cfg["EVOSIM_RESOLVE_FPS"]))
    got, clips, left = inputs(folder, fps, s["rough"])
    F = lambda t: round(t * fps)
    shots = sorted(got["shots.json"]["shots"], key=lambda x: x["from_s"])
    # A rough draft lays a shot whose clip is not in yet as one that stands alone, so the plate holds its time.
    shots = [{k: v for k, v in x.items() if k != "run"} if x["id"] in left["clip"] else x for x in shots]
    rough = sorted([f"shot {i}'s clip is not in yet: the plate holds its time" for i in left["clip"]]
                   + [f"graphic {i} is not drawn yet: left off" for i in left["graphic"]]
                   + [f"words {i} are not drawn yet: left off" for i in left["words"]])
    decisions, derrors = decisions_of(folder, n, shots)
    errors += derrors
    v1 = lay_picture(shots, clips, decisions, fps, errors)
    narration = got["narration.json"]
    end = max(F(narration["length_s"]), F(shots[-1]["to_s"]) if shots else 0)
    covered, at, holes = sorted((x["record"], x["record"] + x["frames"]) for x in v1), 0, []
    for a, b in covered:
        if a > at:
            holes.append((at, a))
        elif a < at:
            errors.append(f"two pieces of footage overlap at {at / fps:.2f} s")
        at = max(at, b)
    if at < end:
        holes.append((at, end))
    for a, b in holes:
        v1.append({"kind": "plate", "shot": None, "record": a, "frames": b - a})
        lines.append(f"note: the plate fills {a / fps:.2f} to {b / fps:.2f} s, where no footage reaches: "
                     "under a graphic that stands alone, or past the last shot")
    v1.sort(key=lambda x: x["record"])
    g, target = s["grade"], (got["clips.json"].get("reference") or {}).get("tone")
    if g is not None:
        if not number(target) or target <= 0:
            errors.append("clips.json's reference has no tone to grade to; the filming step's collect writes it")
        else:
            for x in v1:
                c = clips.get(x["shot"]) if x["kind"] == "clip" else None
                if c is None:
                    continue
                if not number(c.get("tone")) or not (isinstance(c.get("rgb"), list) and len(c["rgb"]) == 3):
                    errors.append(f"shot {x['shot']}'s clip has no tone or colour to grade from; collect it again")
                    continue
                x["grade"] = grade_of(c, target, g)
    first = next((c for c in got["clips.json"]["clips"]), None)
    width, height = (first["width"], first["height"]) if first else (1920, 1080)
    graphics = []
    for shot in shots:
        if shot["kind"] != "graphic" or shot["id"] in left["graphic"]:
            continue
        r = got["graphics.json"]["graphics"][shot["id"]]
        frames = F(shot["to_s"]) - F(shot["from_s"])
        if abs(frames - r["action_frames"]) > 1:
            errors.append(f"graphic {shot['id']} holds {r['action_frames']} frames where its shot lasts {frames}")
        graphics.append({"shot": shot["id"], "file": r["file"], "record": F(shot["from_s"]),
                         "frames": min(frames, r["action_frames"]), "src_in": r["handle_frames"],
                         "src_frames": r["frames"]})
    words = [{"id": w["id"], "file": got["graphics.json"]["words"][w["id"]]["file"], "record": F(w["from_s"]),
              "frames": w["frames"], "src_in": 0}
             for w in got["graphics/plan.json"]["words"] if w["id"] not in left["words"]]
    sr = narration["sample_rate"]
    pieces = [{"piece": p["id"], "file": p["file"]["path"], "record": F(p["at_s"]), "at_s": p["at_s"],
               "frames": math.ceil(p["file"]["samples"] / sr * fps)} for p in narration["pieces"]]
    cues = cues_of(narration["passages"], s, fps)
    srt = write_srt(folder, cues, fps)
    markers = [{"frame": F(p["from_s"]), "name": p["id"],
                "note": (p["sentences"][0]["text"] if p["sentences"] else "")[:300]} for p in narration["passages"]]
    intro = intro_of(s["intro"], shots, fps, errors) if s["intro"] is not None else None
    if intro is not None:
        at, span = intro["record"], intro["frames"]
        later = lambda f: f + span if f >= at else f
        for x in v1 + graphics + words:
            if x["record"] < at < x["record"] + x["frames"]:
                errors.append(f"{x.get('shot') or x.get('id') or 'the plate'} runs across the hook's end, where the intro goes")
            x["record"] = later(x["record"])
        for p in pieces:
            if p["record"] < at < p["record"] + p["frames"]:
                errors.append(f"narration piece {p['piece']} runs across the hook's end, where the intro goes")
            if p["record"] >= at:
                p["record"] += span
                p["at_s"] = round(p["at_s"] + span / fps, 6)
        if any(a < at < b for a, b, _ in cues):
            errors.append("a subtitle runs across the hook's end, where the intro goes")
        cues = [(later(a), later(b), text) for a, b, text in cues]
        srt = write_srt(folder, cues, fps)
        for m in markers:
            m["frame"] = later(m["frame"])
        end += span
        v1.append(intro)
        v1.sort(key=lambda x: x["record"])
    if not errors:
        for item in v1:
            if item["kind"] == "plate":
                item["file"] = make_plate(folder, s["plate"], width, height, fps, item["frames"])
    if s["label"] and not errors:
        for item in v1:
            if item["kind"] == "clip":
                item["label"] = render_label(folder, item, fps, width, height, s["threads"]).relative_to(folder).as_posix()
    plan = {"schema": PLAN_SCHEMA, "draft": n, "name": s["name"], "made": now(), "fps": fps,
            "width": int(cfg["EVOSIM_RESOLVE_WIDTH"]), "height": int(cfg["EVOSIM_RESOLVE_HEIGHT"]),
            "media_width": width, "media_height": height, "frames": end,
            "sources": {k: sha256_of(folder / k) for k in ("shots.json", "clips.json", "graphics.json",
                                                            "narration.json")},
            "decisions": f"edit-{n}.json" if (folder / f"edit-{n}.json").is_file() else None,
            "plate": s["plate"], "intro": intro, "grade": dict(g, tone=target) if g is not None else None, "picture": v1, "graphics": graphics, "words": words, "narration": pieces,
            "subtitles": {"file": srt.relative_to(folder).as_posix(), "count": len(cues),
                          "first_frame": cues[0][0] if cues else None},
            "markers": markers, "rough": rough}
    out = folder / "edit" / f"plan-{n}.json"
    if errors:
        return "EDIT PLAN: FAIL", lines + [f"ERROR {e}" for e in errors]
    write_json(out, plan)
    clips_n = sum(1 for x in v1 if x["kind"] == "clip")
    plate_s = sum(x["frames"] for x in v1 if x["kind"] == "plate") / fps
    head = [f"draft {n}: {end / fps:.1f} s at {fps} frames a second; {clips_n} pieces of footage, "
            f"{plate_s:.1f} s of plate, {len(graphics)} graphics, {len(words)} word cards, {len(pieces)} narration "
            f"pieces, {len(cues)} subtitles, {len(markers)} markers",
            f"plan: edit/plan-{n}.json; subtitles: {plan['subtitles']['file']}"]
    for x in v1:
        where = f"{x['record'] / fps:7.2f} s for {x['frames'] / fps:5.2f} s"
        if x["kind"] == "clip":
            lines.append(f"picture {where}: shot {x['shot']}, its clip from {x['src_in'] / fps:.2f} s "
                         f"({x['run']} at {x['run_s']:,.1f} s)"
                         + (f", graded x{x['grade']['slope']:.2f}" if x.get("grade") else ""))
        elif x["kind"] == "intro":
            lines.append(f"picture {where}: the channel intro, {os.path.basename(x['file'])}")
        else:
            lines.append(f"picture {where}: plate")
    if s["rough"]:
        head.append(f"rough: {len(rough)} items not in yet" if rough else "rough, and everything is in")
        return "EDIT PLAN: ROUGH", head + [f"rough: {r}" for r in rough] + lines
    return "EDIT PLAN: READY", head + lines


def ffmpeg(args, cwd=None):
    done = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args], cwd=cwd,
                          capture_output=True, text=True)
    if done.returncode != 0:
        raise Refused("ffmpeg failed: " + (done.stderr.strip().splitlines() or ["no message"])[-1][:400])


def probe(path):
    done = subprocess.run(["ffprobe", "-v", "error", "-count_frames", "-show_entries",
                           "stream=codec_type,nb_read_frames,width,height", "-of", "json", str(path)],
                          capture_output=True, text=True)
    streams = json.loads(done.stdout or "{}").get("streams", [])
    video = next((x for x in streams if x["codec_type"] == "video"), {})
    return {"frames": int(video.get("nb_read_frames", 0)), "width": video.get("width"),
            "audio": any(x["codec_type"] == "audio" for x in streams)}


def load_plan(folder, n):
    plan = read_json(folder / "edit" / f"plan-{n}.json")
    if plan is None:
        raise Refused(f"the film has no edit/plan-{n}.json; run plan first")
    changed = [k for k, v in plan["sources"].items() if not (folder / k).is_file() or sha256_of(folder / k) != v]
    if changed:
        raise Refused(", ".join(changed) + " changed since draft " + str(n) + " was planned; plan it again")
    return plan


def cmd_proof(folder, n):
    lines = []
    s = settings_of(folder, lines)
    plan = load_plan(folder, n)
    fps, W, H = plan["fps"], s["proof_w"], s["proof_h"]
    work = folder / "edit" / f"proof-{n}"
    shutil.rmtree(work, ignore_errors=True)
    work.mkdir(parents=True)
    video = ["-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "26", "-pix_fmt", "yuv420p", "-r", str(fps),
             "-threads", str(s["threads"])]
    listing = []
    for i, x in enumerate(plan["picture"]):
        seg = work / f"seg-{i:03d}.mp4"
        if x["kind"] == "clip":
            vf = (f"trim=start_frame={x['src_in']}:end_frame={x['src_in'] + x['frames']},setpts=PTS-STARTPTS,"
                  f"scale={W}:{H},setsar=1" + ("," + lut_of(x["grade"]) if x.get("grade") else ""))
            ffmpeg(["-i", str(folder / x["file"]), "-vf", vf, "-frames:v", str(x["frames"]), *video, str(seg)])
        else:
            ffmpeg(["-i", str(folder / x["file"]), "-vf", f"scale={W}:{H},setsar=1", "-frames:v", str(x["frames"]),
                    *video, str(seg)])
        listing.append(f"file '{seg.name}'")
    (work / "picture.txt").write_text(chr(10).join(listing) + chr(10), encoding="utf-8")
    ffmpeg(["-f", "concat", "-safe", "0", "-i", "picture.txt", "-c", "copy", "picture.mp4"], cwd=work)
    over = [(x["label"], 0, x["record"], x["frames"]) for x in plan["picture"] if x.get("label")]
    over += [(x["file"], x["src_in"], x["record"], x["frames"]) for x in plan["graphics"]]
    over += [(x["file"], 0, x["record"], x["frames"]) for x in plan["words"]]
    args, graph, base = ["-i", str(work / "picture.mp4")], [], "0:v"
    for j, (file, src, record, frames) in enumerate(over, 1):
        args += ["-i", str(folder / file)]
        graph.append(f"[{j}:v]trim=start_frame={src}:end_frame={src + frames},"
                     f"setpts=PTS-STARTPTS+{record / fps:.6f}/TB,scale={W}:{H},format=rgba[o{j}]")
        graph.append(f"[{base}][o{j}]overlay=eof_action=pass:format=auto[b{j}]")
        base = f"b{j}"
    fonts = REPO / "unity" / "Assets" / "Theatre" / "UI" / "Fonts"
    style = "FontName=IBM Plex Sans,FontSize=15,Outline=1.2,Shadow=0.8,MarginV=14"
    graph.append(f"[{base}]subtitles=filename='{filter_path(folder / plan['subtitles']['file'], work)}':"
                 f"fontsdir='{filter_path(fonts, work)}':force_style='{style}'[v]")
    first_audio = len(over) + 1
    for k, p in enumerate(plan["narration"]):
        args += ["-i", str(folder / p["file"])]
        graph.append(f"[{first_audio + k}:a]adelay=delays={round(p['at_s'] * 1000)}:all=1[a{k}]")
    if plan["narration"]:
        graph.append("".join(f"[a{k}]" for k in range(len(plan["narration"])))
                     + f"amix=inputs={len(plan['narration'])}:normalize=0:duration=longest,apad[a]")
    (work / "graph.txt").write_text(";".join(graph), encoding="utf-8")
    out = folder / "edit" / f"proof-{n}.mp4"
    audio = ["-map", "[a]", "-c:a", "aac", "-b:a", "160k"] if plan["narration"] else []
    ffmpeg([*args, "-/filter_complex", "graph.txt", "-map", "[v]", *audio, "-c:v", "libx264", "-preset", "veryfast",
            "-crf", "24", "-pix_fmt", "yuv420p", "-r", str(fps), "-frames:v", str(plan["frames"]),
            "-t", f"{plan['frames'] / fps:.3f}", "-threads", str(s["threads"]), str(out)], cwd=work)
    info = probe(out)
    errors = []
    if abs(info["frames"] - plan["frames"]) > 1:
        errors.append(f"the proof holds {info['frames']} frames where the plan has {plan['frames']}")
    if plan["narration"] and not info["audio"]:
        errors.append("the proof has no sound")
    shots = sorted(read_json(folder / "shots.json")["shots"], key=lambda x: x["from_s"])
    from PIL import Image, ImageDraw
    tiles = []
    for shot in shots:
        t = (shot["from_s"] + shot["to_s"]) / 2
        tile = work / f"tile-{shot['id']}.png"
        ffmpeg(["-ss", f"{t:.3f}", "-i", str(out), "-frames:v", "1", "-vf", "scale=480:-2", str(tile)])
        tiles.append((tile, f"{shot['id']} {shot['kind']} {t:.1f} s"))
    if tiles:
        first = Image.open(tiles[0][0])
        cols, tw, th = 4, first.width, first.height
        sheet = Image.new("RGB", (cols * tw, ((len(tiles) + cols - 1) // cols) * th), (20, 20, 20))
        for i, (tile, label) in enumerate(tiles):
            im = Image.open(tile).convert("RGB")
            ImageDraw.Draw(im).text((6, 4), label, fill=(255, 255, 0))
            sheet.paste(im, ((i % cols) * tw, (i // cols) * th))
        sheet.save(folder / "edit" / f"proof-{n}-sheet.png")
    lines += [f"proof: edit/proof-{n}.mp4, {info['frames']} frames at {W}x{H}, "
              f"{len(over)} overlays, {len(plan['narration'])} narration pieces",
              f"sheet: edit/proof-{n}-sheet.png, one frame from the middle of each shot"]
    lines += [f"rough: {r}" for r in plan.get("rough") or []]
    return ("PROOF: FAIL" if errors else "PROOF: PASS"), lines + [f"ERROR {e}" for e in errors]


def connect(cfg):
    """Resolve, through its own scripting module (story-resolve.py's connection)."""
    os.environ["RESOLVE_SCRIPT_API"] = cfg["RESOLVE_SCRIPT_API"]
    os.environ["RESOLVE_SCRIPT_LIB"] = cfg["RESOLVE_SCRIPT_LIB"]
    modules = os.path.join(cfg["RESOLVE_SCRIPT_API"], "Modules")
    if modules not in sys.path:
        sys.path.append(modules)
    try:
        import DaVinciResolveScript as dvr
    except ImportError as e:
        raise Refused(f"cannot import DaVinciResolveScript from {modules} ({e}); set RESOLVE_SCRIPT_API and "
                      "RESOLVE_SCRIPT_LIB in .env")
    resolve = dvr.scriptapp("Resolve")
    if not resolve:
        raise Refused("DaVinci Resolve is not reachable: is it running, with Preferences, System, General, "
                      "External scripting using set to Local?")
    return resolve


def open_project(resolve, cfg):
    project = resolve.GetProjectManager().GetCurrentProject()
    want = cfg["EVOSIM_RESOLVE_PROJECT"].strip()
    have = project.GetName() if project else None
    if not project:
        raise Refused("Resolve has no project open")
    if want and have != want:
        raise Refused(f"Resolve has '{have}' open and the settings name '{want}'; the owner opens it")
    return project


def child_folder(mp, parent, name):
    for f in parent.GetSubFolderList() or []:
        if f.GetName() == name:
            return f
    made = mp.AddSubFolder(parent, name)
    if not made:
        raise Refused(f"Resolve would not make the bin '{name}'")
    return made


def timelines(project):
    return [project.GetTimelineByIndex(i) for i in range(1, project.GetTimelineCount() + 1)]


def key_of(path):
    return os.path.normcase(os.path.abspath(path))


def import_files(mp, folder, paths):
    """Files into a bin, {normalised path: MediaPoolItem}; a file already in that bin is reused (story-resolve.py's)."""
    mp.SetCurrentFolder(folder)
    found = {}
    for clip in folder.GetClipList() or []:
        fp = clip.GetClipProperty("File Path")
        if fp:
            found[key_of(fp)] = clip
    wanted = [p for p in dict.fromkeys(paths) if key_of(p) not in found]
    if wanted:
        items = mp.ImportMedia([os.path.abspath(p) for p in wanted]) or []
        for item in items:
            fp = item.GetClipProperty("File Path")
            if fp:
                found[key_of(fp)] = item
        if len(wanted) == 1 and len(items) == 1 and key_of(wanted[0]) not in found:
            found[key_of(wanted[0])] = items[0]
    missing = [p for p in paths if key_of(p) not in found]
    if missing:
        raise Refused("Resolve would not import: " + ", ".join(str(m) for m in missing))
    return found


def all_items(tl):
    out = []
    for kind in ("video", "audio", "subtitle"):
        for i in range(1, tl.GetTrackCount(kind) + 1):
            out += tl.GetItemListInTrack(kind, i) or []
    return out


def cmd_resolve(folder):
    cfg = resolve_config()
    lines = [f"settings: project '{cfg['EVOSIM_RESOLVE_PROJECT'] or '(the open one)'}', bin "
             f"'{cfg['EVOSIM_RESOLVE_BIN']}', {cfg['EVOSIM_RESOLVE_WIDTH']}x{cfg['EVOSIM_RESOLVE_HEIGHT']} at "
             f"{cfg['EVOSIM_RESOLVE_FPS']} frames a second"]
    resolve = connect(cfg)
    lines.append(f"Resolve: {resolve.GetProductName()} {resolve.GetVersionString()}")
    try:
        project = open_project(resolve, cfg)
    except Refused as exc:
        return "RESOLVE: NOT READY", lines + exc.lines
    names = [t.GetName() for t in timelines(project)]
    lines.append(f"project '{project.GetName()}' is open, with {len(names)} timelines")
    music = [p for p in cfg["EVOSIM_RESOLVE_MUSIC"].split(";") if p.strip()]
    for p in music:
        lines.append(f"music {p}: {'present' if repo_path(p).is_file() else 'MISSING'}")
    return "RESOLVE: READY", lines


def place(mp, item, start_frame, end_frame, record, track, media_type, report, what):
    """One media pool item on a track at its frame, read back: where it landed, how long, and from where."""
    got = mp.AppendToTimeline([{"mediaPoolItem": item, "startFrame": start_frame, "endFrame": end_frame,
                                "recordFrame": record, "trackIndex": track, "mediaType": media_type}]) or []
    if not got:
        report["errors"].append(f"{what} did not land")
        return None
    t = got[0]
    if t.GetStart() != record or t.GetDuration() != end_frame - start_frame:
        report["errors"].append(f"{what} landed at frame {t.GetStart()} for {t.GetDuration()} frames, not at "
                                f"{record} for {end_frame - start_frame}")
    try:
        if t.GetSourceStartFrame() != start_frame:
            report["errors"].append(f"{what} starts at its frame {t.GetSourceStartFrame()}, not {start_frame}")
    except (AttributeError, TypeError):
        report["notes"].append(f"{what}: this Resolve cannot say where in its clip an item starts")
    return t


def cmd_build(folder, n):
    lines = []
    s = settings_of(folder, lines)
    plan = load_plan(folder, n)
    cfg = resolve_config()
    resolve = connect(cfg)
    project = open_project(resolve, cfg)
    mp = project.GetMediaPool()
    stories = child_folder(mp, mp.GetRootFolder(), cfg["EVOSIM_RESOLVE_BIN"] or "Stories")
    taken = {t.GetName() for t in timelines(project)} | {f.GetName() for f in stories.GetSubFolderList() or []}
    k = 1
    while f"{plan['name']} v{k}" in taken or f"{plan['name']} v{k} rough" in taken:
        k += 1
    # A rough draft says so in its bin's and timeline's name, and still takes its number.
    name = f"{plan['name']} v{k}" + (" rough" if plan.get("rough") else "")
    bin_ = child_folder(mp, stories, name)
    report = {"draft": n, "project": project.GetName(), "bin": f"{stories.GetName()}/{name}", "timeline": name,
              "started": now(), "notes": [], "errors": []}
    path = lambda rel: str(folder / rel)
    groups = {"footage": [path(x["file"]) for x in plan["picture"]],
              "labels": [path(x["label"]) for x in plan["picture"] if x.get("label")],
              "graphics": [path(x["file"]) for x in plan["graphics"]],
              "words": [path(x["file"]) for x in plan["words"]],
              "narration": [path(x["file"]) for x in plan["narration"]],
              "subtitles": [path(plan["subtitles"]["file"])]}
    music = [str(repo_path(p)) for p in cfg["EVOSIM_RESOLVE_MUSIC"].split(";") if p.strip() and repo_path(p).is_file()]
    if music:
        groups["music"] = music
    media = {}
    for group, paths in groups.items():
        if paths:
            media.update(import_files(mp, child_folder(mp, bin_, group), paths))
    mp.SetCurrentFolder(bin_)
    tl = mp.CreateEmptyTimeline(name)
    if not tl:
        raise Refused(f"Resolve would not make the timeline '{name}'")
    project.SetCurrentTimeline(tl)
    want = {"timelineResolutionWidth": str(plan["width"]), "timelineResolutionHeight": str(plan["height"]),
            "timelineFrameRate": str(plan["fps"])}
    if not tl.SetSettings(dict(want, useCustomSettings="1")):
        report["notes"].append("Resolve refused the timeline's size and rate; the project's stand")
    # Word cards that overlap in time each take the first Words track free at their frame; Resolve refuses two
    # clips on one track at once (round 49's draft 1 dropped a shot's second card, which starts inside its first).
    lanes, word_track = [], {}
    for x in sorted(plan["words"], key=lambda w: w["record"]):
        lane = next((i for i, end in enumerate(lanes) if end <= x["record"]), len(lanes))
        lanes[lane:lane + 1] = [x["record"] + x["frames"]]
        word_track[x["id"]] = 4 + lane
    for kind, count in (("video", 3 + max(1, len(lanes))), ("audio", 2), ("subtitle", 1)):
        while tl.GetTrackCount(kind) < count:
            if kind == "audio":
                added = tl.AddTrack(kind, "stereo" if tl.GetTrackCount(kind) == 0 else "mono")
            else:
                added = tl.AddTrack(kind)
            if not added:
                report["notes"].append(f"Resolve would not add a {kind} track")
                break
    for kind, index, label in (("video", 1, "Picture"), ("video", 2, "Label"), ("video", 3, "Graphics"),
                               ("video", 4, "Words"), ("audio", 1, "Music"), ("audio", 2, "Narration"),
                               ("subtitle", 1, "Subtitles")):
        tl.SetTrackName(kind, index, label)
    start = tl.GetStartFrame()
    # The subtitles first, while the timeline is empty: Resolve puts a subtitle file at the timeline's end.
    mp.AppendToTimeline([{"mediaPoolItem": media[key_of(path(plan["subtitles"]["file"]))]}])
    subs = tl.GetItemListInTrack("subtitle", 1) or []
    if len(subs) != plan["subtitles"]["count"]:
        report["errors"].append(f"{len(subs)} subtitles on ST1, the plan has {plan['subtitles']['count']}")
    elif subs and subs[0].GetStart() != start + plan["subtitles"]["first_frame"]:
        report["errors"].append(f"the first subtitle is at frame {subs[0].GetStart()}, the plan puts it at "
                                f"{start + plan['subtitles']['first_frame']}")
    for x in plan["picture"]:
        what = (f"shot {x['shot']}'s footage" if x["kind"] == "clip" else "the channel intro" if x["kind"] == "intro"
                else f"the plate at {x['record']}")
        got = place(mp, media[key_of(path(x["file"]))], x.get("src_in", 0), x.get("src_in", 0) + x["frames"],
                    start + x["record"], 1, 1, report, what)
        if got and x.get("grade"):
            gr = x["grade"]
            cdl = {"NodeIndex": "1", "Slope": " ".join([str(gr["slope"])] * 3),
                   "Offset": " ".join(str(o) for o in gr["offset"]), "Power": " ".join([str(gr["power"])] * 3),
                   "Saturation": "1"}
            if not got.SetCDL(cdl):
                report["errors"].append(f"{what}: Resolve would not take its grade")
        if got and x.get("label"):
            lab = place(mp, media[key_of(path(x["label"]))], 0, x["frames"], start + x["record"], 2, 1, report,
                        f"shot {x['shot']}'s label")
            if lab:
                tl.SetClipsLinked([got, lab], True)
    for x in plan["graphics"]:
        place(mp, media[key_of(path(x["file"]))], x["src_in"], x["src_in"] + x["frames"], start + x["record"], 3, 1,
              report, f"graphic {x['shot']}")
    for x in plan["words"]:
        place(mp, media[key_of(path(x["file"]))], 0, x["frames"], start + x["record"], word_track[x["id"]], 1, report,
              f"words {x['id']}")
    for x in plan["narration"]:
        place(mp, media[key_of(path(x["file"]))], 0, x["frames"], start + x["record"], 2, 2, report,
              f"narration {x['piece']}")
    at, last, film_end, fps = start, None, start + plan["frames"], plan["fps"]
    for p in music:
        frames = min(int(media[key_of(p)].GetClipProperty("Frames") or 0), film_end - at)
        if frames > 0:
            last = place(mp, media[key_of(p)], 0, frames, at, 1, 2, report, f"music {os.path.basename(p)}") or last
            at += frames
    if last:
        last.SetFades({"FadeIn": 0, "FadeOut": round(float(cfg["EVOSIM_RESOLVE_MUSIC_FADE"]) * fps)})
    for m in plan["markers"]:
        tl.AddMarker(m["frame"], "Blue", m["name"], m["note"], 1, "passage-" + m["name"])
    report.update(finished=now(), items=len(all_items(tl)), end_frame=tl.GetEndFrame(), start_frame=start)
    write_json(folder / "edit" / f"build-{n}-v{k}.json", report)
    lines += [f"built '{name}' in {report['project']}, bin {report['bin']}: {report['items']} items, "
              f"{(report['end_frame'] - start) / fps:.1f} s", f"report: edit/build-{n}-v{k}.json"]
    lines += ["note: " + x for x in report["notes"]] + ["ERROR " + x for x in report["errors"]]
    lines += [f"rough: {r}" for r in plan.get("rough") or []]
    return ("BUILD: FAIL" if report["errors"] else "BUILD: PASS"), lines


COMMANDS = {"plan": (cmd_plan, "EDIT PLAN: REFUSED", ["folder", "n"]),
            "proof": (cmd_proof, "PROOF: REFUSED", ["folder", "n"]),
            "resolve": (cmd_resolve, "RESOLVE: NOT READY", ["folder"]),
            "build": (cmd_build, "BUILD: REFUSED", ["folder", "n"])}
PASSING = ("READY", "PASS")


def main(argv):
    started = time.monotonic()
    spec = COMMANDS.get(argv[1]) if len(argv) > 1 else None
    if spec is None or len(argv) != 2 + len(spec[2]) or not all(
            kind == "folder" or (v.isdigit() and int(v) >= 1) for kind, v in zip(spec[2], argv[2:])):
        print(__doc__.strip(), file=sys.stderr)
        return 2
    command, refused, kinds = spec
    args = [Path(v).resolve() if kind == "folder" else int(v) for kind, v in zip(kinds, argv[2:])]
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
