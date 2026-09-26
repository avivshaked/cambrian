#!/usr/bin/env python3
"""Compose a round's story film as a DaVinci Resolve timeline, from the plan the ffmpeg join uses.

    python scripts/story-resolve.py <story.json> <out folder> <clips folder> [<clips folder> ...]
        [--name "Round 48 story"] [--plan-only] [--no-title] [--title-seconds 5] [--no-label]
        [--assembler <story-assemble.py>] [--threads N] [--switch-project]
    python scripts/story-resolve.py --build <out folder>/plan.json [--switch-project]
    python scripts/story-resolve.py --check

`scripts/story-assemble.py --ass-only` decides where everything goes: each scene's clip and its
start in `story.scenes.tsv`, every caption and every tick of the provenance label in `story.ass`.
This script has the assembler write those two files into the out folder, reads them, and lays the
same film out in Resolve instead of joining it with ffmpeg:

    V1 Picture   the title card (a Text+ title), the scenes in story order, the end card
    V2 Label     the provenance label, one clip per scene the frame's size, transparent but for
                 the label, rendered here by ffmpeg and libass in IBM Plex Mono and linked to its scene
    V3 Charts    empty, kept for the chart overlays
    ST1 Captions the captions as Resolve subtitles, from an SRT written here
    A1 Music     the configured music, faded out at the film's end
    A2 Narration the story's narration, a clip a paragraph, where scripts/story-narration.py timed it

with a marker at every scene. Nothing is burned: the subtitles are burned at render (Deliver,
Video, Subtitle Settings, Burn into video) or exported as a file.

The plan step (the default, or --plan-only) needs Python, ffmpeg with libass and the assembler; it
writes `story.ass`, `story.scenes.tsv`, `captions-<hash>.srt`, `labels/label-NN.mov` and
`plan.json` into the out folder. The build step (the default, or --build) connects to a running Resolve through
its external scripting API (Preferences, System, General, External scripting using: Local) and
makes a bin and a timeline under the configured bin, named `<name> vK` with K the first number
not yet used, so it never touches an existing timeline. --check connects and reports what the
configuration points at, changing nothing.

The captions are the story's as it stands (`--captions story`, the default here), so a caption
edited in `story.json` after filming reaches the film without filming anything again: plan and
build again, and the new `vK` reuses every clip already in the bin. The SRT's name carries a hash
of its cues, because Resolve would reuse an imported SRT by its path and show the old words. A
timeline edited by hand keeps its edits only in its own `vK`; for that one, import the new SRT
by hand (the manual's chapter 59). `--captions filmed` sets what the director placed at filming.

The configuration is per machine: KEY=value lines in `.env` at the repository's root (gitignored),
any of them overridden by the environment. The keys and their defaults are in
`scripts/story-resolve.env.example`. The procedure, and what the Resolve API does that its
documentation does not say, are in `logbook/specs/story-resolve.md`.
"""
import argparse
import hashlib
import json
import math
import os
import re
import subprocess
import sys
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS_DIR = os.path.join(ROOT, "unity", "Assets", "Theatre", "UI", "Fonts")
ASSEMBLER = os.path.join(ROOT, "scripts", "story-assemble.py")
BACKSLASH = chr(92)
PLAN_VERSION = 1
RESTORE_PRESET = "story-resolve restore"
# Text+'s default title size fits about 41 characters of its default font across the frame (the
# test render of 2026-09-25, whose 53-character title ran off both edges); a longer title is
# made smaller in proportion, from 34 characters so a margin stays.
TITLE_CHARS = 34


def platform_paths():
    """Resolve's scripting module folder and library, where its installer puts them."""
    if sys.platform.startswith("win"):
        data = os.environ.get("PROGRAMDATA", "C:/ProgramData")
        programs = os.environ.get("PROGRAMFILES", "C:/Program Files")
        return (os.path.join(data, "Blackmagic Design", "DaVinci Resolve", "Support", "Developer", "Scripting"),
                os.path.join(programs, "Blackmagic Design", "DaVinci Resolve", "fusionscript.dll"))
    if sys.platform == "darwin":
        return ("/Library/Application Support/Blackmagic Design/DaVinci Resolve/Developer/Scripting",
                "/Applications/DaVinci Resolve/DaVinci Resolve.app/Contents/Libraries/Fusion/fusionscript.so")
    return "/opt/resolve/Developer/Scripting", "/opt/resolve/libs/Fusion/fusionscript.so"


API_DIR, API_LIB = platform_paths()
DEFAULTS = {
    "EVOSIM_RESOLVE_PROJECT": "",
    "EVOSIM_RESOLVE_BIN": "Stories",
    "EVOSIM_RESOLVE_TEMPLATE": "",
    "EVOSIM_RESOLVE_WIDTH": "3840",
    "EVOSIM_RESOLVE_HEIGHT": "2160",
    "EVOSIM_RESOLVE_FPS": "30",
    "EVOSIM_RESOLVE_TITLE_FONT": "",
    "EVOSIM_RESOLVE_TITLE_SECONDS": "5",
    "EVOSIM_RESOLVE_LABEL": "1",
    "EVOSIM_RESOLVE_MUSIC": "",
    "EVOSIM_RESOLVE_MUSIC_FADE": "3",
    "EVOSIM_RESOLVE_END_CARD": "",
    "RESOLVE_SCRIPT_API": API_DIR,
    "RESOLVE_SCRIPT_LIB": API_LIB,
}


# ---------------------------------------------------------------- configuration

def load_dotenv(path):
    """KEY=value lines from a .env file, or nothing when there is no file."""
    found = {}
    if not os.path.isfile(path):
        return found
    with open(path, encoding="utf-8-sig") as f:
        for raw in f.read().splitlines():
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            value = value.strip()
            if " #" in value:
                value = value.split(" #", 1)[0].rstrip()
            found[key.strip()] = value.strip('"').strip("'")
    return found


def load_config():
    """The configuration and where each value came from: default, .env or environment."""
    cfg, source = dict(DEFAULTS), {k: "default" for k in DEFAULTS}
    unknown = []
    for key, value in load_dotenv(os.path.join(ROOT, ".env")).items():
        if key in cfg:
            cfg[key], source[key] = value, ".env"
        elif key.startswith("EVOSIM_RESOLVE_"):
            unknown.append(key)
    for key in cfg:
        if os.environ.get(key):
            cfg[key], source[key] = os.environ[key], "environment"
    for key in unknown:
        print("  note: .env names %s, which this script does not read" % key, file=sys.stderr)
    return cfg, source


def repo_path(value):
    """A configured path: absolute, or relative to the repository's root."""
    value = value.strip()
    if not value:
        return ""
    return value if os.path.isabs(value) else os.path.normpath(os.path.join(ROOT, value))


def flag_on(value):
    return str(value).strip().lower() not in ("0", "off", "false", "no", "")


def fps_text(fps):
    return ("%d" % fps) if float(fps).is_integer() else ("%.6f" % fps)


# ---------------------------------------------------------------- reading the assembler's plan

def read_table(path):
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    head = lines[0].split("\t")
    return [dict(zip(head, line.split("\t"))) for line in lines[1:] if line.strip()]


def ass_seconds(stamp):
    h, m, s = stamp.strip().split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def read_ass(path):
    """The events of an .ass file: style, start and end in seconds, and the text without override codes."""
    events, fields, section = [], None, None
    with open(path, encoding="utf-8-sig") as f:
        for line in f.read().splitlines():
            if line.startswith("["):
                section = line.strip()
                continue
            if section != "[Events]":
                continue
            if line.startswith("Format:"):
                fields = [x.strip() for x in line[len("Format:"):].split(",")]
            elif line.startswith("Dialogue:") and fields:
                cells = line[len("Dialogue:"):].split(",", len(fields) - 1)
                row = dict(zip(fields, cells))
                words = re.sub(r"\{[^}]*\}", "", row.get("Text", ""))
                words = words.replace(BACKSLASH + "N", "\n").replace(BACKSLASH + "n", "\n").strip()
                events.append({"style": row.get("Style", "").strip(), "start": ass_seconds(row["Start"]),
                               "end": ass_seconds(row["End"]), "text": words})
    return events


def probe_frames(path, fps):
    """A clip's frame count: the stream's own count, else its duration at the timeline's rate."""
    r = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                        "stream=nb_frames:format=duration", "-of", "json", path], capture_output=True, text=True)
    if r.returncode != 0:
        return None
    data = json.loads(r.stdout or "{}")
    streams = data.get("streams") or [{}]
    count = streams[0].get("nb_frames")
    if count and str(count).isdigit() and int(count) > 0:
        return int(count)
    try:
        return int(round(float((data.get("format") or {}).get("duration")) * fps))
    except (TypeError, ValueError):
        return None


# ---------------------------------------------------------------- writing the captions and labels

def srt_stamp(frame, fps):
    """A frame as an SRT time, rounded up to the millisecond so any reading of it lands on that frame."""
    ms = int(math.ceil(frame * 1000.0 / fps - 1e-9))
    h, ms = divmod(ms, 3600000)
    m, ms = divmod(ms, 60000)
    s, ms = divmod(ms, 1000)
    return "%02d:%02d:%02d,%03d" % (h, m, s, ms)


def write_srt(path, cues, fps):
    """cues: (start frame, end frame, text), counted from where the file will be appended."""
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for i, (a, b, words) in enumerate(cues, 1):
            f.write("%d\n%s --> %s\n%s\n\n" % (i, srt_stamp(a, fps), srt_stamp(b, fps), words))


def ass_time(t):
    cs = int(math.floor(max(0.0, t) * 100.0 + 1e-6))
    h, cs = divmod(cs, 360000)
    m, cs = divmod(cs, 6000)
    s, cs = divmod(cs, 100)
    return "%d:%02d:%02d.%02d" % (h, m, s, cs)


def label_canvas(width, height):
    """The label's clip is the whole frame, transparent but for the label, so Resolve places it without a transform.

    A strip of the frame's top left was tried first and Resolve put it at the wrong place: its Pan and
    Tilt are not the timeline's pixels (logbook/specs/story-resolve.md). A frame of 4K with a label on
    it costs about 40 KB a second as QuickTime Animation and renders at five times real time."""
    return {"width": width, "height": height}


def write_label_ass(path, events, strip, frame_height):
    """One scene's label ticks, styled as story-assemble.py styles the label, on the whole frame."""
    sc = frame_height / 1080.0
    lines = [
        "[Script Info]",
        "; Written by scripts/story-resolve.py: one scene's provenance label, for an overlay clip.",
        "ScriptType: v4.00+",
        "WrapStyle: 0",
        "ScaledBorderAndShadow: yes",
        "PlayResX: %d" % strip["width"],
        "PlayResY: %d" % strip["height"],
        "",
        "[V4+ Styles]",
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, "
        "Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, "
        "MarginV, Encoding",
        "Style: Label,IBM Plex Mono,%d,&H4CFFFFFF,&H000000FF,&H7F000000,&HFF000000,0,0,0,0,100,100,0.6,0,1,%.1f,0,7,%d,%d,%d,1"
        % (round(23 * sc), 1.2 * sc, round(32 * sc), round(32 * sc), round(26 * sc)),
        "",
        "[Events]",
        "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
    ]
    for a, b, words in events:
        lines.append("Dialogue: 0,%s,%s,Label,,0,0,0,,%s" % (ass_time(a), ass_time(b), words))
    with open(path, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


def filter_path(path, cwd):
    """A path for an ffmpeg filter option: relative where it can be, forward slashes, colons escaped."""
    try:
        path = os.path.relpath(path, cwd)
    except ValueError:
        path = os.path.abspath(path)
    return path.replace(BACKSLASH, "/").replace(":", BACKSLASH + ":")


def render_label(ass_path, mov_path, strip, frames, fps, threads, fonts_dir):
    """The label as a clip with alpha (QuickTime Animation), the canvas's size and the scene's length."""
    cwd = os.path.dirname(os.path.abspath(ass_path))
    seconds = frames / float(fps) + 1.0
    vf = "subtitles=filename='%s':fontsdir='%s':alpha=1" % (filter_path(ass_path, cwd), filter_path(fonts_dir, cwd))
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi",
           "-i", "color=c=black@0.0:s=%dx%d:r=%s:d=%.3f,format=rgba" % (strip["width"], strip["height"], fps_text(fps), seconds),
           "-vf", vf, "-frames:v", str(frames), "-c:v", "qtrle", "-pix_fmt", "argb", "-g", "9000",
           "-threads", str(threads), os.path.abspath(mov_path)]
    r = subprocess.run(cmd, cwd=cwd)
    return r.returncode == 0 and os.path.isfile(mov_path)


# ---------------------------------------------------------------- the plan

def run_assembler(args, story, stem, title_seconds, with_label):
    assembler = os.path.abspath(args.assembler or ASSEMBLER)
    with open(assembler, encoding="utf-8") as f:
        if "--captions" not in f.read():
            sys.exit("%s has no --captions: it is the assembler from before 2026-09-25's evening, when captions became "
                     "editable after filming. Use the one beside this script." % assembler)
    cmd = [sys.executable, assembler, story, stem + ".mp4"] + [os.path.abspath(p) for p in args.folders] + ["--ass-only"]
    cmd += ["--no-title"] if title_seconds <= 0 else ["--title-seconds", "%g" % title_seconds]
    if not with_label:
        cmd.append("--no-label")
    cmd += ["--captions", args.captions]
    if args.filmed:
        cmd += ["--filmed", os.path.abspath(args.filmed)]
    print("the assembler's plan: " + " ".join('"%s"' % c if " " in c else c for c in cmd[1:]))
    if subprocess.run(cmd).returncode != 0:
        sys.exit("the assembler failed; nothing planned")


def make_plan(args, cfg):
    story = os.path.abspath(args.story)
    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    fps = float(cfg["EVOSIM_RESOLVE_FPS"])
    width, height = int(cfg["EVOSIM_RESOLVE_WIDTH"]), int(cfg["EVOSIM_RESOLVE_HEIGHT"])
    title_seconds = 0.0 if args.no_title else (args.title_seconds if args.title_seconds is not None
                                               else float(cfg["EVOSIM_RESOLVE_TITLE_SECONDS"]))
    with_label = flag_on(cfg["EVOSIM_RESOLVE_LABEL"]) and not args.no_label
    notes = []
    stem = os.path.join(out, "story")
    run_assembler(args, story, stem, title_seconds, with_label)

    rows = read_table(stem + ".scenes.tsv")
    events = read_ass(stem + ".ass") if os.path.isfile(stem + ".ass") else []
    captions = [e for e in events if e["style"] == "Caption"]
    ticks = [e for e in events if e["style"] == "Label"]
    with open(story, encoding="utf-8-sig") as f:
        root = json.load(f)
    title = (root.get("title") or "").strip() if isinstance(root, dict) else ""
    name = args.name or title.split(":")[0].strip() or "story"

    title_frames = 0
    title_row = next((r for r in rows if r["n"] == "0"), None)
    if title_row and title_seconds > 0:
        title_frames = int(round(float(title_row["length_s"]) * fps))

    strip = label_canvas(width, height)
    labels_dir = os.path.join(out, "labels")
    # The narration, where the story is timed from it (scripts/story-narration.py): each scene's
    # segments with their offsets in its own time, which starts after its chapter card.
    listed = root.get("scenes") if isinstance(root, dict) else root
    by_n = {s["n"]: s for s in listed or [] if isinstance(s, dict) and "n" in s}
    story_dir = os.path.dirname(story)
    scenes, at, missing = [], title_frames, {}
    for r in rows:
        if r["n"] == "0":
            continue
        if r["status"] != "joined":
            missing.setdefault(r["status"], []).append(r["n"])
            continue
        frames = probe_frames(r["path"], fps)
        if not frames:
            notes.append("scene %s: ffprobe could not count %s; left out" % (r["n"], r["path"]))
            continue
        planned = int(round(float(r["start_s"]) * fps))
        if abs(planned - at) > 1:
            notes.append("scene %s starts at frame %d by the clips' counts and %d by the table's seconds; the counts are used"
                         % (r["n"], at, planned))
        start_s, end_s = at / fps, (at + frames) / fps
        scene = {"n": int(r["n"]), "run": r["run"], "act": r["act"], "station": r["station"], "subject": r["subject"],
                 "clip": r["path"], "frames": frames, "record": at, "provenance": r.get("provenance", ""),
                 "captions": [c["text"] for c in captions if start_s - 1e-3 <= c["start"] < end_s - 1e-3], "label": "",
                 "narration": []}
        own = float(r.get("own_start_s") or 0.0)
        for e in (by_n.get(int(r["n"])) or {}).get("narration") or []:
            path = e["file"] if os.path.isabs(e["file"]) else os.path.join(story_dir, e["file"])
            offset = float(e.get("at", 0.0)) + (0.0 if e.get("on_card") else own)
            if not os.path.isfile(path):
                notes.append("scene %s: its narration %s is not on disk; left out" % (r["n"], path))
                continue
            if offset + float(e.get("seconds", 0.0)) > frames / fps + 1e-3:
                notes.append("scene %s: its narration %s runs %.1f s past the clip; the scene was filmed shorter than "
                             "its narration" % (r["n"], e.get("segment_id", ""), offset + float(e.get("seconds", 0.0)) - frames / fps))
            scene["narration"].append({"segment": e.get("segment_id", ""), "file": os.path.abspath(path),
                                       "record": at + int(round(offset * fps))})
        mine = [(max(0.0, e["start"] - start_s), min(end_s, e["end"]) - start_s, e["text"])
                for e in ticks if start_s - 1e-3 <= e["start"] < end_s - 1e-3]
        if with_label and mine:
            os.makedirs(labels_dir, exist_ok=True)
            base = os.path.join(labels_dir, "label-%02d" % scene["n"])
            write_label_ass(base + ".ass", mine, strip, height)
            if render_label(base + ".ass", base + ".mov", strip, frames, fps, args.threads, args.fonts):
                scene["label"] = base + ".mov"
            else:
                notes.append("scene %d: ffmpeg could not render its label" % scene["n"])
        scenes.append(scene)
        at += frames

    for status, numbers in missing.items():
        notes.append("%d scene(s) not in the film (%s): %s" % (len(numbers), status, ", ".join(numbers)))

    cues = []
    for c in captions:
        a, b = int(round(c["start"] * fps)), int(round(c["end"] * fps))
        if a < title_frames:
            notes.append("a caption starts before the title ends and is moved to its end: " + c["text"][:60])
            a = title_frames
        if b > a:
            cues.append((a - title_frames, b - title_frames, c["text"]))
    digest = hashlib.sha256(json.dumps(cues, ensure_ascii=False).encode("utf-8")).hexdigest()[:8]
    srt = os.path.join(out, "captions-%s.srt" % digest)
    if cues:
        write_srt(srt, cues, fps)

    plan = {"version": PLAN_VERSION, "made": datetime.now().isoformat(timespec="seconds"), "story": story,
            "name": name, "title": title if title_frames else "", "title_frames": title_frames,
            "fps": fps, "width": width, "height": height, "frames": at,
            "captions": srt if cues else "", "caption_count": len(cues),
            "first_caption_frame": cues[0][0] + title_frames if cues else None,
            "label_canvas": strip, "scenes": scenes, "notes": notes}
    path = os.path.join(out, "plan.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(plan, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print("%4s  %-8s  %-10s  %7s  %7s  %5s  %s" % ("n", "run", "station", "record", "frames", "caps", "label"))
    for s in scenes:
        print("%4d  %-8s  %-10s  %7d  %7d  %5d  %s" % (s["n"], s["run"][:8], s["station"][:10], s["record"], s["frames"],
                                                       len(s["captions"]), "yes" if s["label"] else "-"))
    for note in notes:
        print("  note: " + note)
    print("plan: %s (%d scenes, %d captions, %.1f s at %g fps)" % (path, len(scenes), len(cues), at / fps, fps))
    return path


# ---------------------------------------------------------------- Resolve

def connect(cfg):
    os.environ["RESOLVE_SCRIPT_API"] = cfg["RESOLVE_SCRIPT_API"]
    os.environ["RESOLVE_SCRIPT_LIB"] = cfg["RESOLVE_SCRIPT_LIB"]
    modules = os.path.join(cfg["RESOLVE_SCRIPT_API"], "Modules")
    if modules not in sys.path:
        sys.path.append(modules)
    try:
        import DaVinciResolveScript as dvr
    except ImportError as e:
        sys.exit("cannot import DaVinciResolveScript from %s (%s); set RESOLVE_SCRIPT_API and RESOLVE_SCRIPT_LIB in .env"
                 % (modules, e))
    resolve = dvr.scriptapp("Resolve")
    if not resolve:
        sys.exit("DaVinci Resolve is not reachable: is it running, with Preferences, System, General, "
                 "External scripting using set to Local?")
    return resolve


def open_project(resolve, cfg, switch):
    pm = resolve.GetProjectManager()
    project = pm.GetCurrentProject()
    want = cfg["EVOSIM_RESOLVE_PROJECT"].strip()
    have = project.GetName() if project else None
    if want and have != want:
        if not switch:
            sys.exit("Resolve has '%s' open and the configuration names '%s': open it in Resolve, or pass --switch-project"
                     % (have, want))
        project = pm.LoadProject(want)
        if not project:
            sys.exit("Resolve has no project named '%s' in its current folder" % want)
    if not project:
        sys.exit("Resolve has no project open")
    return project


def child_folder(mp, parent, name):
    for f in parent.GetSubFolderList() or []:
        if f.GetName() == name:
            return f
    made = mp.AddSubFolder(parent, name)
    if not made:
        sys.exit("Resolve would not make the bin '%s'" % name)
    return made


def timelines(project):
    return [project.GetTimelineByIndex(i) for i in range(1, project.GetTimelineCount() + 1)]


def key_of(path):
    return os.path.normcase(os.path.abspath(path))


def import_files(mp, folder, paths):
    """Import files into a bin: {normalised path: MediaPoolItem}; a file already in that bin is reused."""
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
        sys.exit("Resolve would not import: " + ", ".join(missing))
    return found


def item_frames(item, fps):
    frames = item.GetClipProperty("Frames")
    if frames and str(frames).isdigit():
        return int(frames)
    m = re.match(r"^(\d+):(\d+):(\d+)[:;](\d+)$", item.GetClipProperty("Duration") or "")
    if m:
        h, mi, s, f = (int(x) for x in m.groups())
        return int(round(((h * 60 + mi) * 60 + s) * fps)) + f
    return None


def all_items(tl):
    out = []
    for kind in ("video", "audio", "subtitle"):
        for i in range(1, tl.GetTrackCount(kind) + 1):
            out += tl.GetItemListInTrack(kind, i) or []
    return out


def font_known(resolve, family):
    """True or False when Fusion's font list answers, None when it cannot be asked."""
    try:
        listed = resolve.Fusion().FontManager.GetFontList()
    except Exception:
        return None
    if not listed:
        return None
    names = set(str(n) for n in listed.values()) | set(str(k) for k in listed.keys()) if isinstance(listed, dict) \
        else set(str(n) for n in listed)
    return any(n.lower() == family.lower() for n in names)


def insert_title(resolve, project, tl, start, frames, text, font, report):
    """The title card: a Text+ title inserted between the timeline's marks at its first frame."""
    if RESTORE_PRESET in (project.GetRenderPresetList() or []):
        project.DeleteRenderPreset(RESTORE_PRESET)
    saved = project.SaveAsNewRenderPreset(RESTORE_PRESET)
    try:
        project.SetRenderSettings({"SelectAllFrames": False, "MarkIn": start, "MarkOut": start + frames - 1})
        tl.SetCurrentTimecode(tl.GetStartTimecode())
        item = tl.InsertFusionTitleIntoTimeline("Text+")
    finally:
        if saved:
            project.LoadRenderPreset(RESTORE_PRESET)
            project.DeleteRenderPreset(RESTORE_PRESET)
    if not saved:
        report["notes"].append("the render settings could not be saved before the title's marks were set; check Deliver")
    if not item:
        report["errors"].append("Resolve inserted no title")
        return None
    where = (item.GetStart(), item.GetDuration(), list(item.GetTrackTypeAndIndex()))
    if where != (start, frames, ["video", 1]):
        report["errors"].append("the title landed at %s, not at frame %d for %d frames on V1" % (where, start, frames))
    comp = item.GetFusionCompByIndex(1)
    tool = comp.FindTool("Template") if comp else None
    if not tool:
        report["errors"].append("the title has no Template tool to take its text")
        return item
    tool.SetInput("StyledText", text)
    size = tool.GetInput("Size")
    if isinstance(size, (int, float)) and len(text) > TITLE_CHARS:
        tool.SetInput("Size", size * TITLE_CHARS / float(len(text)))
    known = font_known(resolve, font) if font else None
    if font and known is not False:
        tool.SetInput("Font", font)
        tool.SetInput("Style", "Regular")
    if font and known is False:
        report["notes"].append("the font '%s' is not installed; the title keeps Resolve's default" % font)
    return item


def make_timeline(project, mp, cfg, bin_, name, plan, report):
    """A copy of the template timeline when one is named, else an empty one; its size set; its tracks named."""
    mp.SetCurrentFolder(bin_)
    template_name = cfg["EVOSIM_RESOLVE_TEMPLATE"].strip()
    template = next((t for t in timelines(project) if t.GetName() == template_name), None) if template_name else None
    if template_name and not template:
        sys.exit("the template timeline '%s' is not in the project" % template_name)
    if template:
        if all_items(template):
            sys.exit("the template timeline '%s' has clips on it; a template is empty tracks and styles only" % template_name)
        project.SetCurrentTimeline(template)
        tl = template.DuplicateTimeline(name)
        report["notes"].append("made from the template timeline '%s'" % template_name)
    else:
        tl = mp.CreateEmptyTimeline(name)
    if not tl:
        sys.exit("Resolve would not make the timeline '%s'" % name)
    project.SetCurrentTimeline(tl)
    settings = tl.GetSettings() or {}
    want = {"timelineResolutionWidth": str(plan["width"]), "timelineResolutionHeight": str(plan["height"]),
            "timelineFrameRate": fps_text(plan["fps"])}
    if any(str(settings.get(k, "")).split(".")[0] != v.split(".")[0] for k, v in want.items()):
        ok = tl.SetSettings(dict(want, useCustomSettings="1"))
        report["notes"].append("the timeline set to %sx%s at %s fps (custom settings)%s" % (
            want["timelineResolutionWidth"], want["timelineResolutionHeight"], want["timelineFrameRate"],
            "" if ok else ": REFUSED, the project's settings stand"))
    for kind, count in (("video", 3), ("audio", 2), ("subtitle", 1)):
        while tl.GetTrackCount(kind) < count:
            if kind == "audio":
                added = tl.AddTrack(kind, "stereo" if tl.GetTrackCount(kind) == 0 else "mono")
            else:
                added = tl.AddTrack(kind)
            if not added:
                break
    for kind, index, label in (("video", 1, "Picture"), ("video", 2, "Label"), ("video", 3, "Charts"), ("audio", 1, "Music"),
                               ("audio", 2, "Narration")):
        tl.SetTrackName(kind, index, label)
    if tl.GetTrackName("subtitle", 1) in ("", "Subtitle 1", "Subtitle"):
        tl.SetTrackName("subtitle", 1, "Captions")
    return tl


def place_captions(mp, tl, media, plan, start, report):
    """The SRT, appended while the timeline ends where the title does: Resolve puts a subtitle file at the end."""
    end_now = tl.GetEndFrame() if all_items(tl) else start
    if end_now != start + plan["title_frames"]:
        report["errors"].append("before the captions the timeline ends at %d, not %d; they sit late by the difference"
                                % (end_now, start + plan["title_frames"]))
    mp.AppendToTimeline([{"mediaPoolItem": media[key_of(plan["captions"])]}])
    subs = tl.GetItemListInTrack("subtitle", 1) or []
    if len(subs) != plan["caption_count"]:
        report["errors"].append("%d captions on ST1, the plan has %d" % (len(subs), plan["caption_count"]))
    elif subs and subs[0].GetStart() != start + plan["first_caption_frame"]:
        report["errors"].append("the first caption is at frame %d, the plan puts it at %d"
                                % (subs[0].GetStart(), start + plan["first_caption_frame"]))
    if tl.GetTrackCount("subtitle") > 1:
        report["notes"].append("the timeline has %d subtitle tracks; the captions went on ST1" % tl.GetTrackCount("subtitle"))


def place_scenes(mp, tl, media, plan, start, report):
    """Each scene on V1 at its own frame and its label on V2 above it, the two linked."""
    placed = []
    for s in plan["scenes"]:
        item = media[key_of(s["clip"])]
        have = item_frames(item, plan["fps"])
        if have is not None and have != s["frames"]:
            report["notes"].append("scene %d: Resolve counts %d frames, the plan %d" % (s["n"], have, s["frames"]))
        got = mp.AppendToTimeline([{"mediaPoolItem": item, "startFrame": 0, "endFrame": s["frames"],
                                    "recordFrame": start + s["record"], "trackIndex": 1, "mediaType": 1}]) or []
        if not got or got[0].GetStart() != start + s["record"] or got[0].GetDuration() != s["frames"]:
            report["errors"].append("scene %d did not land at frame %d for %d frames" % (s["n"], start + s["record"], s["frames"]))
            continue
        placed.append(s["n"])
        if not s["label"]:
            continue
        lab = mp.AppendToTimeline([{"mediaPoolItem": media[key_of(s["label"])], "startFrame": 0, "endFrame": s["frames"],
                                    "recordFrame": start + s["record"], "trackIndex": 2}]) or []
        if not lab:
            report["errors"].append("scene %d's label did not land" % s["n"])
            continue
        tl.SetClipsLinked([got[0], lab[0]], True)
    return placed


def place_narration(mp, tl, media, plan, start, report):
    """Each scene's narration on A2 at its own frame; returns how many landed."""
    fps, placed = plan["fps"], 0
    for s in plan["scenes"]:
        for e in s.get("narration") or []:
            item = media.get(key_of(e["file"]))
            if item is None:
                report["errors"].append("the narration %s was not imported" % e["segment"])
                continue
            frames = item_frames(item, fps)
            got = mp.AppendToTimeline([{"mediaPoolItem": item, "startFrame": 0, "endFrame": frames,
                                        "recordFrame": start + e["record"], "trackIndex": 2, "mediaType": 2}]) or []
            if not got:
                report["errors"].append("the narration %s did not land" % e["segment"])
            elif abs(got[0].GetStart() - (start + e["record"])) > 1:
                report["errors"].append("the narration %s landed at frame %d, not %d" % (
                    e["segment"], got[0].GetStart(), start + e["record"]))
            else:
                placed += 1
    return placed


def place_extras(mp, tl, media, plan, cfg, start, report):
    """The end card after the last scene, then the music under the whole film with a fade at its end."""
    fps = plan["fps"]
    film_end = start + plan["frames"]
    end_card = repo_path(cfg["EVOSIM_RESOLVE_END_CARD"])
    if end_card and key_of(end_card) in media:
        item = media[key_of(end_card)]
        frames = item_frames(item, fps) or int(round(5 * fps))
        got = mp.AppendToTimeline([{"mediaPoolItem": item, "startFrame": 0, "endFrame": frames,
                                    "recordFrame": film_end, "trackIndex": 1}]) or []
        if got:
            film_end += got[0].GetDuration()
        else:
            report["errors"].append("the end card did not land")
    music = [repo_path(p) for p in cfg["EVOSIM_RESOLVE_MUSIC"].split(";") if p.strip()]
    at, last = start, None
    for p in music:
        if key_of(p) not in media or at >= film_end:
            continue
        take = min(item_frames(media[key_of(p)], fps) or 0, film_end - at)
        if take <= 0:
            continue
        got = mp.AppendToTimeline([{"mediaPoolItem": media[key_of(p)], "startFrame": 0, "endFrame": take,
                                    "recordFrame": at, "trackIndex": 1, "mediaType": 2}]) or []
        if got:
            last = got[0]
            at += take
        else:
            report["errors"].append("the music %s did not land" % os.path.basename(p))
    if last:
        last.SetFades({"FadeIn": 0, "FadeOut": int(round(float(cfg["EVOSIM_RESOLVE_MUSIC_FADE"]) * fps))})
    if music and at < film_end:
        report["notes"].append("the music ends %.1f s before the film does" % ((film_end - at) / fps))


def build(plan_path, cfg, switch):
    with open(plan_path, encoding="utf-8") as f:
        plan = json.load(f)
    if plan.get("version") != PLAN_VERSION:
        sys.exit("%s is a plan of version %s; this script reads %d" % (plan_path, plan.get("version"), PLAN_VERSION))
    report = {"plan": os.path.abspath(plan_path), "started": datetime.now().isoformat(timespec="seconds"),
              "notes": [], "errors": []}
    resolve = connect(cfg)
    project = open_project(resolve, cfg, switch)
    mp = project.GetMediaPool()

    # the bin and the timeline share one name, <name> vK, with K the first free among both
    stories = child_folder(mp, mp.GetRootFolder(), cfg["EVOSIM_RESOLVE_BIN"] or "Stories")
    taken = {t.GetName() for t in timelines(project)} | {f.GetName() for f in stories.GetSubFolderList() or []}
    k = 1
    while "%s v%d" % (plan["name"], k) in taken:
        k += 1
    name = "%s v%d" % (plan["name"], k)
    bin_ = child_folder(mp, stories, name)
    report.update(project=project.GetName(), bin="%s/%s" % (stories.GetName(), name), timeline=name)

    # the media: the scenes in a bin per run, then the labels, the captions and the configured assets
    media, by_run = {}, {}
    for s in plan["scenes"]:
        by_run.setdefault(s["run"] or "scenes", []).append(s["clip"])
    for run, paths in by_run.items():
        media.update(import_files(mp, child_folder(mp, bin_, run), paths))
    labels = [s["label"] for s in plan["scenes"] if s["label"]]
    if labels:
        media.update(import_files(mp, child_folder(mp, bin_, "labels"), labels))
    if plan["captions"]:
        media.update(import_files(mp, child_folder(mp, bin_, "captions"), [plan["captions"]]))
    wanted = [repo_path(p) for p in cfg["EVOSIM_RESOLVE_MUSIC"].split(";") if p.strip()]
    if cfg["EVOSIM_RESOLVE_END_CARD"].strip():
        wanted.append(repo_path(cfg["EVOSIM_RESOLVE_END_CARD"]))
    for p in wanted:
        if not os.path.isfile(p):
            report["notes"].append("configured asset not found, left out: " + p)
    extras = [p for p in wanted if os.path.isfile(p)]
    if extras:
        media.update(import_files(mp, child_folder(mp, bin_, "assets"), extras))
    spoken = [e["file"] for s in plan["scenes"] for e in s.get("narration") or []]
    if spoken:
        media.update(import_files(mp, child_folder(mp, bin_, "narration"), spoken))

    # the order matters: the title, then the captions while the timeline still ends at the title,
    # then everything placed at its own frame
    tl = make_timeline(project, mp, cfg, bin_, name, plan, report)
    start = tl.GetStartFrame()
    if plan["title_frames"] > 0:
        insert_title(resolve, project, tl, start, plan["title_frames"], plan["title"] or plan["name"],
                     cfg["EVOSIM_RESOLVE_TITLE_FONT"].strip(), report)
    if plan["captions"]:
        place_captions(mp, tl, media, plan, start, report)
    placed = place_scenes(mp, tl, media, plan, start, report)
    report["narration"] = place_narration(mp, tl, media, plan, start, report)
    place_extras(mp, tl, media, plan, cfg, start, report)

    # a marker at every scene, red where a chapter opens; the note carries the scene's captions
    for s in plan["scenes"]:
        if s["n"] in placed:
            tl.AddMarker(s["record"], "Red" if s["act"] else "Blue", ("%d %s: %s" % (s["n"], s["station"], s["subject"]))[:80],
                         (("%s. " % s["act"]) if s["act"] else "") + " / ".join(s["captions"])[:1500], 1, "story-%02d" % s["n"])

    fps = plan["fps"]
    report.update(finished=datetime.now().isoformat(timespec="seconds"), scenes_placed=len(placed),
                  scenes_planned=len(plan["scenes"]), v1_items=len(tl.GetItemListInTrack("video", 1) or []),
                  labels=len(tl.GetItemListInTrack("video", 2) or []),
                  captions=len(tl.GetItemListInTrack("subtitle", 1) or []), markers=len(tl.GetMarkers() or {}),
                  start_frame=start, end_frame=tl.GetEndFrame())
    out = os.path.join(os.path.dirname(os.path.abspath(plan_path)), "build.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("built '%s' in %s, bin %s: %d of %d scenes, %d labels, %d captions, %d narration clips, %d markers, %.1f s"
          % (name, report["project"], report["bin"], len(placed), len(plan["scenes"]), report["labels"],
             report["captions"], report["narration"], report["markers"], (report["end_frame"] - start) / fps))
    for note in report["notes"]:
        print("  note: " + note)
    for err in report["errors"]:
        print("  ERROR: " + err)
    print("report: " + out)
    return 1 if report["errors"] else 0


def check(cfg, source):
    print("configuration (.env at %s, then the environment):" % ROOT)
    for key in DEFAULTS:
        print("  %-30s %-12s %s" % (key, source[key], cfg[key]))
    resolve = connect(cfg)
    pm = resolve.GetProjectManager()
    project = pm.GetCurrentProject()
    print("Resolve: %s %s; open project: %s" % (resolve.GetProductName(), resolve.GetVersionString(),
                                                  project.GetName() if project else None))
    want = cfg["EVOSIM_RESOLVE_PROJECT"].strip()
    if want:
        listed = pm.GetProjectListInCurrentFolder() or []
        state = "open" if project and project.GetName() == want else "in the current folder, not open" if want in listed \
            else "NOT FOUND in the project manager's current folder"
        print("  configured project '%s': %s" % (want, state))
    else:
        print("  no project configured: a build goes into the open one")
    if project:
        names = [f.GetName() for f in project.GetMediaPool().GetRootFolder().GetSubFolderList() or []]
        print("  bin '%s': %s" % (cfg["EVOSIM_RESOLVE_BIN"], "present" if cfg["EVOSIM_RESOLVE_BIN"] in names
                                  else "made at the first build"))
        s = project.GetSetting()
        print("  the project's timelines: %sx%s at %s fps; a story is built at %sx%s at %s fps"
              % (s.get("timelineResolutionWidth"), s.get("timelineResolutionHeight"), s.get("timelineFrameRate"),
                 cfg["EVOSIM_RESOLVE_WIDTH"], cfg["EVOSIM_RESOLVE_HEIGHT"], cfg["EVOSIM_RESOLVE_FPS"]))
        tpl = cfg["EVOSIM_RESOLVE_TEMPLATE"].strip()
        if tpl:
            t = next((t for t in timelines(project) if t.GetName() == tpl), None)
            print("  template '%s': %s" % (tpl, "NOT FOUND" if not t else "has clips, refused" if all_items(t)
                                           else "empty, %d subtitle track(s)" % t.GetTrackCount("subtitle")))
        else:
            print("  no template: the captions take Resolve's default subtitle style")
    for family in [f for f in (cfg["EVOSIM_RESOLVE_TITLE_FONT"].strip(),) if f]:
        known = font_known(resolve, family)
        print("  font '%s' for Resolve: %s" % (family, {True: "installed", False: "NOT INSTALLED", None: "could not ask"}[known]))
    for path in [repo_path(p) for p in cfg["EVOSIM_RESOLVE_MUSIC"].split(";") if p.strip()] + \
            ([repo_path(cfg["EVOSIM_RESOLVE_END_CARD"])] if cfg["EVOSIM_RESOLVE_END_CARD"].strip() else []):
        print("  asset %s: %s" % (path, "present" if os.path.isfile(path) else "MISSING"))
    for tool in ("ffmpeg", "ffprobe"):
        r = subprocess.run([tool, "-hide_banner", "-version"], capture_output=True, text=True)
        print("  %s: %s" % (tool, (r.stdout.splitlines() or ["?"])[0] if r.returncode == 0 else "MISSING"))
    return 0


def main():
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(errors="replace")
        except (AttributeError, ValueError):
            pass
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("story", nargs="?")
    p.add_argument("out", nargs="?")
    p.add_argument("folders", nargs="*")
    p.add_argument("--build", metavar="PLAN", help="build from a plan.json already made")
    p.add_argument("--check", action="store_true", help="report the configuration and what it points at")
    p.add_argument("--plan-only", action="store_true")
    p.add_argument("--name", help="the bin's and the timeline's name before its vK (default: the title up to its colon)")
    p.add_argument("--no-title", action="store_true")
    p.add_argument("--title-seconds", type=float)
    p.add_argument("--no-label", action="store_true")
    p.add_argument("--captions", choices=("story", "filmed"), default="story",
                   help="the story's captions as they stand (default), or as the director placed them at filming")
    p.add_argument("--filmed", help="the story as filmed, to tell the director's own lines (default: story.filmed.json beside the story)")
    p.add_argument("--assembler", help="story-assemble.py with --ass-only (default: the one beside this script)")
    p.add_argument("--fonts", default=FONTS_DIR, help="the folder holding IBM Plex Mono for the label")
    p.add_argument("--threads", type=int, default=max(2, (os.cpu_count() or 6) // 3))
    p.add_argument("--switch-project", action="store_true", help="open the configured project when another is open")
    args = p.parse_args()
    cfg, source = load_config()
    if args.check:
        sys.exit(check(cfg, source))
    if args.build:
        sys.exit(build(args.build, cfg, args.switch_project))
    if not (args.story and args.out and args.folders):
        p.error("a story, an out folder and at least one clips folder; or --build; or --check")
    plan = make_plan(args, cfg)
    if not args.plan_only:
        sys.exit(build(plan, cfg, args.switch_project))


if __name__ == "__main__":
    main()
