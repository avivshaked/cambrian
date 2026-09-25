#!/usr/bin/env python3
"""Join a story's clips into one film, in the story's order, with its captions set as subtitles.

    python scripts/story-assemble.py <story.json> <output.mp4> <clips folder> [<clips folder> ...]
        [--title-seconds 5] [--no-title] [--subtitles auto|burn|file|off] [--no-label]
        [--ass-only] [--fonts <folder>] [--threads N]
    python scripts/story-assemble.py --reburn <film.clean.mp4> <film.ass> <output.mp4> [--fonts <folder>] [--threads N]

The safari's story mode (`scripts/theatre-safari.ps1 <arm> -Story <story.json>`) writes one clip
per scene into each run's folder, named `story-NN-<arm>-<station>-<subject>.mp4`, where NN is the
scene's number in the story. This finds each scene's clip by its `story-NN-` prefix across the
folders given, joins the clips in the story's order, and prints a table of scene, clip and length
with every scene that has no clip marked skipped: a missing scene is never invented or filled.
The same table is written beside the film as `<output stem>.scenes.tsv`, with each scene's start
in the film.

The join is ffmpeg's concat with the streams copied when every clip shares its codec, frame size,
frame rate and pixel format, and a re-encode to the first clip's size and rate (H.264, crf 18,
each clip scaled into the frame and padded) when they do not. When two clips carry one number
(the same scene filmed twice), the newest is taken and the others are named. ffmpeg and ffprobe on
PATH, with libass for the subtitles; nothing else.

When the story has a `title`, the film opens on it: white on black for `--title-seconds` (5),
at the first clip's size and rate, drawn by ffmpeg's drawtext in IBM Plex Sans (a Windows font
when the repository's is missing; ffmpeg's own default font lookup crashes on this machine);
`--no-title` leaves it out.

The subtitles (2026-09-25; the owner, on the first film's stamped captions: "why do the subtitles
look so bad? like a really weird font"). Since that day a story's frames carry no text: the
director writes every caption's span and every take's second into `captions.tsv` beside the
clips, and this script sets them as one subtitle file for the whole film, `<output stem>.ass`,
in IBM Plex Sans from the repository's `unity/Assets/Theatre/UI/Fonts` (or `--fonts`): the
captions white with a soft dark outline and shadow, centred in the lower third within the middle
70% of the frame; the provenance label (COUSIN, the run, the world's second, ticking) small and
quiet at the top left in Plex Mono. `--no-label` leaves the label out.

- `--subtitles auto` (the default) writes the `.ass` and burns it when no joined clip carries
  stamped text, and otherwise writes it and says why it did not burn.
- `--subtitles burn` burns it whatever the clips carry (a trial on older clips).
- `--subtitles file` writes it and never burns; `--subtitles off` writes none.

A burnt film is made from a joined film without text, which is kept beside it as
`<output stem>.clean.mp4`, so the `.ass` can be edited and burnt again without filming anything:
`--reburn <clean> <ass> <output>`. The burn re-encodes the whole film (H.264, crf 18) on
`--threads` threads, a third of the machine's by default (the owner's load ruling of 2026-09-25).
`--ass-only` writes the `.ass` and the table from the clips' lengths and joins nothing.

Each scene's provenance word is read from its take rows in `captions.tsv` (B3, 2026-09-25): a
scene filmed from the farm's film windows carries its window's verdict (FAITHFUL, COUSIN or
UNVERIFIED), and a scene stepped live carries COUSIN. The label ticks say it, the table gains a
`provenance` column, and a film whose scenes do not all carry one word says so in a note and a
closing line, because a viewer reading the corner of one scene should not take it for the film's.

Where a clip's folder has no `captions.tsv` rows for it in the 2026-09-25 form (every clip filmed
before it, whose captions are stamped into its frames), its captions are placed from the story
itself as the director places them (the chapter card's 8 s, a card's title, a time scene's
crossfade), with no label and without the director's own lines (a story's birth adds one), and the
table says so.

The story is read as the director reads it: the scenes under `scenes` (or `shots`, or the file as a
list), each scene's number under `n` (or `number`), its run under `run` (or `arm`), and the rest
for the table and the captions' fallback; a scene without a number takes its place in the file.
"""
import json
import math
import os
import re
import subprocess
import sys

CLIP = re.compile(r"^story-(\d+)-(.+)\.mp4$", re.IGNORECASE)

# captions.tsv's form since 2026-09-25 (SafariHeadless.CaptionHeader in Editor/TheatreSafari.cs)
CAPTION_HEADER = ["kind", "scene", "story", "arm", "station", "take", "from_s", "to_s", "second", "text"]

# the director's constants (SafariTripBuilder, SafariCaptions)
CHAPTER_SECONDS = 8.0
FIRST_OFFSET = 0.5
ON_SCREEN_SECONDS = 4.0
CROSSFADE_SECONDS = 1.0  # theatre-safari.ps1's time scene: its last two takes cross in one second

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS_DIR = os.path.join(REPO, "unity", "Assets", "Theatre", "UI", "Fonts")
CAPTION_FONT = "IBM Plex Sans"
LABEL_FONT = "IBM Plex Mono"
TITLE_FONT_FILE = "IBMPlexSans-Regular.ttf"


def first(d, *keys):
    for k in keys:
        if isinstance(d, dict) and k in d and d[k] is not None:
            return d[k]
    return None


def whole(value):
    """A scene number: an int, or a string of digits ('7', '#7'); None otherwise."""
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return int(round(value))
    if isinstance(value, str) and re.fullmatch(r"\s*#?\d{1,5}\s*", value):
        return int(value.strip().lstrip("#"))
    return None


def number(value):
    """A number, or the first number in a string ('5,000 s'); None otherwise."""
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        m = re.search(r"-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?", value)
        if m:
            return float(m.group(0).replace(",", ""))
    return None


def text(value):
    if value is None:
        return ""
    if isinstance(value, dict):
        parts = [str(v) for k, v in value.items() if k in ("name", "clade", "root", "body", "text")]
        return " ".join(parts)
    return str(value).replace("\t", " ").replace("\n", " ").strip()


def plain(value):
    """A caption as the director keeps it when it is not stamped: line breaks and tabs made spaces."""
    return re.sub(r" {2,}", " ", str(value).replace("\t", " ").replace("\r", " ").replace("\n", " ")).strip()


def story_title(path):
    with open(path, encoding="utf-8-sig") as f:
        root = json.load(f)
    title = root.get("title") if isinstance(root, dict) else None
    return title.strip() if isinstance(title, str) and title.strip() else None


FONTS = ("georgia.ttf", "segoeui.ttf", "arial.ttf")


def escaped(path):
    return os.path.abspath(path).replace("\\", "/").replace(":", "\\:")


def title_card(title, shape, seconds, output, fonts_dir):
    """A held title, white on black, at the first clip's size and rate. (path, note)."""
    font = os.path.join(fonts_dir, TITLE_FONT_FILE)
    if not os.path.isfile(font):
        windows = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")
        font = next((os.path.join(windows, f) for f in FONTS if os.path.isfile(os.path.join(windows, f))), None)
        if font is None:
            return None, "no title card: no %s in %s and none of %s in %s" % (TITLE_FONT_FILE, fonts_dir, ", ".join(FONTS), windows)

    stem = os.path.splitext(output)[0]
    card, words = stem + ".title.mp4", stem + ".title.txt"
    with open(words, "w", encoding="utf-8") as f:
        f.write(title)

    width, height = shape["width"], shape["height"]
    size = int(max(12, min(height / 16.0, 1.7 * width / max(1, len(title)))))
    vf = ("drawtext=fontfile='%s':textfile='%s':fontcolor=white:fontsize=%d:x=(w-text_w)/2:y=(h-text_h)/2,"
          "fade=t=in:st=0:d=0.5,fade=t=out:st=%g:d=0.5,format=yuv420p" % (escaped(font), escaped(words), size, max(0.0, seconds - 0.5)))
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi",
           "-i", "color=c=black:s=%dx%d:r=%s:d=%g" % (width, height, shape["rate"], seconds),
           "-vf", vf, "-c:v", "libx264",
           "-pix_fmt", "yuv420p", "-crf", "18", "-r", "%g" % rate_value(shape["rate"]), "-movflags", "+faststart", card]
    r = subprocess.run(cmd)
    os.remove(words)
    if r.returncode != 0 or not os.path.isfile(card):
        return None, "no title card: ffmpeg's drawtext failed"
    return card, "a %g s title card from the story's title, in %s" % (seconds, os.path.basename(font))


def read_story(path):
    with open(path, encoding="utf-8-sig") as f:
        root = json.load(f)
    top_run = None
    scenes = []
    if isinstance(root, list):
        scenes = [(s, None) for s in root]
    elif isinstance(root, dict):
        top_run = first(root, "run", "arm", "seed")
        listed = first(root, "scenes", "shots", "shot_list", "shotList", "shotlist", "list", "film")
        if isinstance(listed, list):
            scenes = [(s, None) for s in listed]
        for act in root.get("acts", []) if isinstance(root.get("acts"), list) else []:
            if isinstance(act, dict):
                inner = first(act, "scenes", "shots")
                if isinstance(inner, list):
                    scenes += [(s, text(first(act, "act", "name", "title"))) for s in inner]
    out = []
    for position, (s, act) in enumerate(scenes):
        if not isinstance(s, dict):
            continue
        n = whole(first(s, "n", "number", "scene_n", "sceneNumber", "scene_number", "no", "num", "index", "order", "scene"))
        out.append({
            "n": n if n is not None else position + 1,
            "position": position,
            "run": text(first(s, "run", "arm", "seed", "run_name", "runName") or top_run),
            "act": text(first(s, "act", "chapter", "part")) or (act or ""),
            "station": text(first(s, "station", "kind", "type", "shot")),
            "subject": text(first(s, "subject", "who", "clade", "root", "body")),
            "seconds": first(s, "seconds", "length", "duration", "screen_seconds"),
            "raw": s,
        })
    out.sort(key=lambda r: (r["n"], r["position"]))
    return out


def find_clips(folders):
    """Every story clip in the folders (not their subfolders): {number: [paths]}."""
    found = {}
    for folder in folders:
        if not os.path.isdir(folder):
            print("  no folder: " + folder, file=sys.stderr)
            continue
        for name in sorted(os.listdir(folder)):
            m = CLIP.match(name)
            path = os.path.join(folder, name)
            if m and os.path.isfile(path):
                found.setdefault(int(m.group(1)), []).append(path)
    return found


def probe(path):
    r = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "v:0",
         "-show_entries", "stream=codec_name,width,height,r_frame_rate,pix_fmt:format=duration",
         "-of", "json", path],
        capture_output=True, text=True)
    if r.returncode != 0:
        return None
    data = json.loads(r.stdout or "{}")
    streams = data.get("streams") or []
    if not streams:
        return None
    s = streams[0]
    try:
        duration = float((data.get("format") or {}).get("duration", "nan"))
    except ValueError:
        duration = float("nan")
    return {
        "codec": s.get("codec_name"), "width": s.get("width"), "height": s.get("height"),
        "rate": s.get("r_frame_rate"), "pix_fmt": s.get("pix_fmt"), "duration": duration,
    }


def rate_value(rate):
    try:
        num, den = rate.split("/")
        return float(num) / float(den)
    except (ValueError, ZeroDivisionError, AttributeError):
        return 30.0


def join(clips, shapes, output):
    """Concat with the streams copied when the clips agree, a re-encode otherwise. True on success."""
    keys = [(s["codec"], s["width"], s["height"], s["rate"], s["pix_fmt"]) for s in shapes]
    same = all(k == keys[0] for k in keys)
    if same:
        listing = os.path.splitext(output)[0] + ".concat.txt"
        with open(listing, "w", encoding="utf-8") as f:
            for c in clips:
                f.write("file '" + os.path.abspath(c).replace("\\", "/").replace("'", "'\\''") + "'\n")
        cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0",
               "-i", listing, "-c", "copy", "-movflags", "+faststart", output]
        r = subprocess.run(cmd)
        os.remove(listing)
        return r.returncode == 0, "streams copied: every clip %s %dx%d at %s fps, %s" % keys[0]

    width, height = shapes[0]["width"], shapes[0]["height"]
    fps = rate_value(shapes[0]["rate"])
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
    for c in clips:
        cmd += ["-i", c]
    chains = []
    for i in range(len(clips)):
        chains.append("[%d:v:0]scale=%d:%d:force_original_aspect_ratio=decrease,pad=%d:%d:(ow-iw)/2:(oh-ih)/2,"
                      "setsar=1,fps=%s,format=yuv420p[v%d]" % (i, width, height, width, height, shapes[0]["rate"], i))
    graph = ";".join(chains) + ";" + "".join("[v%d]" % i for i in range(len(clips))) + \
        "concat=n=%d:v=1:a=0[out]" % len(clips)
    cmd += ["-filter_complex", graph, "-map", "[out]", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p",
            "-r", "%g" % fps, "-movflags", "+faststart", output]
    r = subprocess.run(cmd)
    odd = sorted({"%s %dx%d at %s, %s" % k for k in keys})
    return r.returncode == 0, "re-encoded to %dx%d at %s fps (the clips differ: %s)" % (
        width, height, shapes[0]["rate"], "; ".join(odd))


# ---------------------------------------------------------------- captions.tsv

def read_captions(folder):
    """
    The rows of a folder's captions.tsv in the 2026-09-25 form, per scene slug, from the last
    session that filmed each scene: {slug: {"fps", "captions_in_frames", "label_in_frames",
    "takes": {take: {...}}, "captions": [...]}}; and a word on what the file was.
    """
    path = os.path.join(folder, "captions.tsv")
    if not os.path.isfile(path):
        return {}, "no captions.tsv"
    with open(path, encoding="utf-8-sig") as f:
        lines = f.read().splitlines()
    if not lines or lines[0].split("\t") != CAPTION_HEADER:
        return {}, "captions.tsv in the older form (the text is stamped in the frames)"

    sessions = []
    session = None
    for line in lines[1:]:
        cells = line.split("\t")
        if len(cells) < len(CAPTION_HEADER):
            continue
        kind = cells[0]
        if kind == "format" or session is None:
            fields = dict(re.findall(r"(\w+)=(\S+)", cells[9])) if kind == "format" else {}
            session = {"fps": number(fields.get("fps")) or 30.0,
                       "captions_in_frames": fields.get("captions_in_frames", "1") == "1",
                       "label_in_frames": fields.get("label_in_frames", "1") == "1",
                       "scenes": {}}
            sessions.append(session)
            if kind == "format":
                continue
        slug = cells[1]
        entry = session["scenes"].setdefault(slug, {"takes": {}, "captions": [], "arm": cells[3], "station": cells[4]})
        take = whole(cells[5]) or 1
        row = {"take": take, "from": number(cells[6]) or 0.0, "to": number(cells[7]) or 0.0,
               "second": number(cells[8]), "text": cells[9]}
        if kind == "take":
            entry["takes"][take] = row
        elif kind == "caption":
            entry["captions"].append(row)

    out = {}
    for s in sessions:
        for slug, entry in s["scenes"].items():
            if not entry["takes"] and not entry["captions"]:
                continue
            out[slug] = dict(entry, fps=s["fps"], captions_in_frames=s["captions_in_frames"],
                             label_in_frames=s["label_in_frames"])
    return out, "captions.tsv with %d session(s)" % len(sessions)


def scene_words(entry):
    """The provenance words of one scene's take rows, in take order, each once; COUSIN for a row without one."""
    words = []
    for t in sorted(entry["takes"]):
        word = (entry["takes"][t]["text"] or "COUSIN").strip().upper()
        if word not in words:
            words.append(word)
    return words


def take_lengths(entry, folder, slug, notes):
    """Each take's length in its clip, s, from its row, else its encoded clip, else its last caption."""
    takes = sorted(set(entry["takes"]) | {c["take"] for c in entry["captions"]})
    lengths = {}
    for t in takes:
        if t in entry["takes"]:
            lengths[t] = entry["takes"][t]["to"]
            continue
        shape = probe(os.path.join(folder, slug, "take-%d.mp4" % t))
        if shape and shape["duration"] > 0:
            lengths[t] = shape["duration"]
            notes.append("%s take %d has no take row; its length is its encoded clip's" % (slug, t))
        else:
            lengths[t] = max([c["to"] for c in entry["captions"] if c["take"] == t] or [0.0])
            notes.append("%s take %d has no take row and no clip: its length is taken as its last caption's end" % (slug, t))
    return lengths


def layout(lengths, station):
    """Each take's start in the scene's clip: cut end to end, a time scene's last take one second early."""
    starts, at = {}, 0.0
    takes = sorted(lengths)
    for i, t in enumerate(takes):
        if station.lower() == "time" and len(takes) >= 2 and i == len(takes) - 1:
            at -= CROSSFADE_SECONDS
        starts[t] = max(0.0, at)
        at += lengths[t]
    return starts


def events_from_rows(entry, lengths, start, clip_length, label):
    """(captions, labels) of one scene as film seconds, from its captions.tsv rows."""
    fps = entry["fps"] or 30.0
    starts = layout(lengths, entry.get("station", ""))
    takes = sorted(lengths)

    def snap(t):
        return round(t * fps) / fps

    captions = []
    for c in entry["captions"]:
        t0 = starts.get(c["take"], 0.0)
        captions.append((start + t0 + snap(c["from"]), start + t0 + snap(c["to"]), plain(c["text"])))

    labels = []
    if label:
        for i, t in enumerate(takes):
            row = entry["takes"].get(t)
            if not row or row["second"] is None:
                continue
            t0 = starts[t]
            t1 = starts[takes[i + 1]] if i + 1 < len(takes) else min(clip_length, t0 + lengths[t])
            second0 = row["second"]
            arm = entry.get("arm") or ""
            # One event a whole second of the world's clock, which runs with the clip's.
            tick = t0
            while tick < t1 - 1e-6:
                shown = math.floor(second0 + (tick - t0) + 1e-6)
                upto = min(t1, t0 + (shown + 1 - second0))
                if upto <= tick + 1e-6:
                    upto = min(t1, tick + 1.0)
                labels.append((start + tick, start + upto, "%s \u00b7 %s \u00b7 %s s" % (row["text"] or "COUSIN", arm, format(shown, ","))))
                tick = upto
    return captions, labels


def story_captions(scene, chapter, start, clip_length):
    """
    A scene's captions as film seconds from the story alone, as the director places them: its
    captions after the chapter card when it opens a chapter, the chapter's line on the card, a
    card's title when it has no caption, a time scene's last take one second early.
    """
    raw = scene["raw"]
    station = scene["station"].strip().lower()
    new = re.match(r"^new\s*[:\-—–]?\s*([^—–:(,;]*)", station)
    card = station in ("card", "title", "chapter", "title card", "chapter card") or bool(
        new and re.search(r"\b(title|card|chapter|intertitle|credits?)\b", new.group(1)))
    length = number(first(raw, "seconds", "length", "duration", "screen_seconds", "screen_s")) or 0.0

    listed = []
    given = first(raw, "captions", "caption", "lines", "narration", "subtitles", "text")
    if isinstance(given, str):
        given = [given]
    if isinstance(given, list):
        for i, c in enumerate(given):
            slot = FIRST_OFFSET + i * (ON_SCREEN_SECONDS + 1.0)
            if isinstance(c, str):
                listed.append([slot, ON_SCREEN_SECONDS, plain(c)])
            elif isinstance(c, dict):
                words = first(c, "text", "line", "caption", "words", "say")
                if not words:
                    continue
                at = number(first(c, "at", "offset", "t", "start", "from", "time", "at_s"))
                hold = number(first(c, "for", "seconds", "duration", "length", "hold"))
                listed.append([max(0.0, at if at is not None else slot), hold if hold and hold > 0 else ON_SCREEN_SECONDS, plain(words)])

    if card and not listed:
        title = first(raw, "title", "card_title", "heading") or first(raw, "description", "desc")
        if title:
            listed.append([FIRST_OFFSET, max(ON_SCREEN_SECONDS, length - 2 * FIRST_OFFSET), plain(title)])

    lead = 0.0
    chapter_title = text(first(raw, "chapter", "chapter_title", "chapterTitle", "chapter_card"))
    if chapter_title and not card and chapter is not None:
        lead = CHAPTER_SECONDS
        for c in listed:
            c[0] += lead
        listed.insert(0, [FIRST_OFFSET, CHAPTER_SECONDS - 2 * FIRST_OFFSET,
                          plain("Chapter %d: %s." % (chapter, chapter_title.rstrip(".")))])

    end = min(clip_length, length + lead) if length > 0 else clip_length
    fade_from = lead + 0.5 * length if station == "time" and length > 0 else None

    out = []
    for at, hold, words in listed:
        t0, t1 = at, min(at + hold, end)
        if fade_from is not None and t0 >= fade_from - 1e-6:
            t0, t1 = t0 - CROSSFADE_SECONDS, t1 - CROSSFADE_SECONDS
        if t1 > t0:
            out.append((start + t0, start + t1, words))
    return out


# ---------------------------------------------------------------- the .ass

def ass_time(t):
    """h:mm:ss.cc, floored to the centisecond so an event starts and ends on the frame it names."""
    cs = int(math.floor(max(0.0, t) * 100.0 + 1e-6))
    h, cs = divmod(cs, 360000)
    m, cs = divmod(cs, 6000)
    s, cs = divmod(cs, 100)
    return "%d:%02d:%02d.%02d" % (h, m, s, cs)


def ass_text(words):
    """A caption as an event's text: braces and backslashes would be read as override codes."""
    return words.replace("\\", "\u2216").replace("{", "(").replace("}", ")")


def write_ass(path, title, width, height, captions, labels):
    """The film's subtitles: the captions in the lower third, the label at the top left."""
    scale = height / 1080.0
    lines = [
        "[Script Info]",
        "; Written by scripts/story-assemble.py: the story's captions and the provenance label.",
        "; Edit and burn again: python scripts/story-assemble.py --reburn <film>.clean.mp4 <this file> <film>.mp4",
        "Title: %s" % (title or "story"),
        "ScriptType: v4.00+",
        "WrapStyle: 0",
        "ScaledBorderAndShadow: yes",
        "YCbCr Matrix: TV.709",
        "PlayResX: %d" % width,
        "PlayResY: %d" % height,
        "",
        "[V4+ Styles]",
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, "
        "Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, "
        "MarginV, Encoding",
        # The caption: white, a soft dark outline and a drop shadow, centred at the foot, within the
        # middle 70% of the frame, 56 px at 1080 (about a nineteenth of the height, a little over libass's own default).
        "Style: Caption,%s,%d,&H00FFFFFF,&H000000FF,&H5A000000,&H8C000000,0,0,0,0,100,100,0.4,0,1,%.1f,%.1f,2,%d,%d,%d,1"
        % (CAPTION_FONT, round(56 * scale), 2.4 * scale, 1.6 * scale, round(0.15 * width), round(0.15 * width), round(62 * scale)),
        # The label: small, white at 70%, a thin outline, no shadow, at the top left.
        "Style: Label,%s,%d,&H4CFFFFFF,&H000000FF,&H7F000000,&HFF000000,0,0,0,0,100,100,0.6,0,1,%.1f,0,7,%d,%d,%d,1"
        % (LABEL_FONT, round(23 * scale), 1.2 * scale, round(32 * scale), round(32 * scale), round(26 * scale)),
        "",
        "[Events]",
        "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
    ]
    for t0, t1, words in sorted(labels):
        lines.append("Dialogue: 0,%s,%s,Label,,0,0,0,,%s" % (ass_time(t0), ass_time(t1), ass_text(words)))
    for t0, t1, words in sorted(captions):
        lines.append("Dialogue: 1,%s,%s,Caption,,0,0,0,,{\\blur1.2}%s" % (ass_time(t0), ass_time(t1), ass_text(words)))
    with open(path, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


def filter_path(path, cwd):
    """A path for a filter option: relative to ffmpeg's working folder where it can be, forward slashes, colons escaped."""
    try:
        path = os.path.relpath(path, cwd)
    except ValueError:
        path = os.path.abspath(path)
    return path.replace("\\", "/").replace(":", "\\:")


def burn(clean, ass, output, fonts_dir, threads):
    """The film with its subtitles burnt in, re-encoded. (ok, note)."""
    cwd = os.path.dirname(os.path.abspath(ass))
    vf = "subtitles=filename='%s':fontsdir='%s'" % (filter_path(ass, cwd), filter_path(fonts_dir, cwd))
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", os.path.abspath(clean), "-map", "0:v:0", "-map", "0:a?",
           "-vf", vf, "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "copy",
           "-threads", str(threads), "-movflags", "+faststart", os.path.abspath(output)]
    r = subprocess.run(cmd, cwd=cwd)
    return r.returncode == 0 and os.path.isfile(output), "subtitles burnt with libass (%s, fonts from %s, %d threads)" % (
        os.path.basename(ass), fonts_dir, threads)


# ---------------------------------------------------------------- the whole

def option(args, name, default=None, cast=str):
    if name not in args:
        return default
    i = args.index(name)
    try:
        value = cast(args[i + 1])
    except (IndexError, ValueError):
        sys.exit("%s wants a value" % name)
    del args[i:i + 2]
    return value


def flag(args, name):
    if name in args:
        args.remove(name)
        return True
    return False


def main():
    # A console that cannot print a character (an em dash in a station on cp1252) prints a
    # stand-in rather than stopping the join.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(errors="replace")
        except (AttributeError, ValueError):
            pass
    args = sys.argv[1:]
    fonts_dir = os.path.abspath(option(args, "--fonts", FONTS_DIR))
    threads = option(args, "--threads", max(2, (os.cpu_count() or 6) // 3), int)

    if flag(args, "--reburn"):
        if len(args) != 3:
            sys.exit("--reburn <film.clean.mp4> <film.ass> <output.mp4>")
        ok, how = burn(args[0], args[1], args[2], fonts_dir, threads)
        if not ok:
            sys.exit("ffmpeg failed to burn the subtitles (" + how + ")")
        print("%s: %s" % (args[2], how))
        return

    with_title = not flag(args, "--no-title")
    title_seconds = option(args, "--title-seconds", 5.0, float)
    mode = option(args, "--subtitles", "auto")
    if mode not in ("auto", "burn", "file", "off"):
        sys.exit("--subtitles is one of auto, burn, file, off")
    with_label = not flag(args, "--no-label")
    ass_only = flag(args, "--ass-only")
    if len(args) < 3:
        print("\n".join(l.strip() for l in __doc__.strip().splitlines()[2:6]), file=sys.stderr)
        sys.exit(2)
    story_path, output, folders = args[0], args[1], args[2:]
    if not os.path.isfile(story_path):
        sys.exit("no story at " + story_path)
    if mode != "off" and not os.path.isdir(fonts_dir):
        sys.exit("no fonts folder at %s (--fonts)" % fonts_dir)

    scenes = read_story(story_path)
    if not scenes:
        sys.exit("the story at %s has no scenes" % story_path)
    found = find_clips(folders)

    # The chapters are counted over every scene that carries one, as the director counts them.
    chapters, count = {}, 0
    for s in scenes:
        if text(first(s["raw"], "chapter", "chapter_title", "chapterTitle", "chapter_card")):
            count += 1
            chapters[s["n"]] = count

    rows, clips, shapes, notes = [], [], [], []
    numbers = {s["n"] for s in scenes}
    for n in sorted(set(found) - numbers):
        notes.append("clip(s) numbered %d have no scene in the story and are left out: %s"
                     % (n, ", ".join(os.path.basename(p) for p in found[n])))

    start = 0.0
    used = set()
    for s in scenes:
        n = s["n"]
        row = dict(s, clip="", path="", start="", length="", status="", subtitles="")
        candidates = found.get(n, [])
        if n in used:
            row["status"] = "skipped: its number's clip is already used by an earlier scene"
            rows.append(row)
            continue
        if not candidates:
            row["status"] = "skipped: no clip"
            rows.append(row)
            continue
        candidates = sorted(candidates, key=os.path.getmtime)
        path = candidates[-1]
        if len(candidates) > 1:
            notes.append("scene %d has %d clips; the newest is taken, %s; not taken: %s" % (
                n, len(candidates), path, ", ".join(candidates[:-1])))
        shape = probe(path)
        if shape is None or not (shape["duration"] > 0):
            row.update(clip=os.path.basename(path), path=path, status="skipped: ffprobe could not read the clip")
            rows.append(row)
            continue
        arm = CLIP.match(os.path.basename(path)).group(2)
        if "-" in s["run"] and not arm.lower().startswith(s["run"].lower() + "-"):
            notes.append("scene %d is for run '%s' and its clip says '%s'" % (n, s["run"], arm))
        used.add(n)
        row.update(clip=os.path.basename(path), path=os.path.abspath(path), start="%.3f" % start,
                   length="%.3f" % shape["duration"], status="joined", shape=shape)
        start += shape["duration"]
        clips.append(path)
        shapes.append(shape)
        rows.append(row)

    if not clips:
        print("no clip found for any scene: nothing joined")
        sys.exit(1)

    os.makedirs(os.path.dirname(os.path.abspath(output)), exist_ok=True)
    stem = os.path.splitext(output)[0]
    table = stem + ".scenes.tsv"
    ass = stem + ".ass"

    # The title first, at the first clip's size and rate, so the streams still copy.
    title = story_title(story_path) if with_title and title_seconds > 0 else None
    card = None
    title_length = 0.0
    if title and not ass_only:
        card, said = title_card(title, shapes[0], title_seconds, output, fonts_dir)
        print("  " + said)
        if card:
            shape = probe(card)
            title_length = shape["duration"]
            clips.insert(0, card)
            shapes.insert(0, shape)
    elif title:
        title_length = title_seconds
    if title_length > 0:
        for r in rows:
            if r["start"]:
                r["start"] = "%.3f" % (float(r["start"]) + title_length)
        rows.insert(0, {"n": 0, "run": "", "act": "", "station": "title", "subject": title,
                        "clip": os.path.basename(card) if card else "(the title, not made)",
                        "path": os.path.abspath(card) if card else "", "start": "0.000", "length": "%.3f" % title_length,
                        "status": "joined" if card else "not made (--ass-only)", "subtitles": ""})

    # Each joined scene's provenance, from its take rows; "-" where the folder has none (a clip
    # filmed before 2026-09-25 carries its word stamped in its frames and nowhere else).
    read = {}
    for r in rows:
        if r["status"] != "joined" or r["n"] == 0:
            continue
        folder = os.path.dirname(r["path"])
        if folder not in read:
            read[folder] = read_captions(folder)
        entry = read[folder][0].get(os.path.splitext(r["clip"])[0])
        r["provenance"] = "+".join(scene_words(entry)) if entry and entry["takes"] else "-"
    words_seen = {}
    for r in rows:
        for word in (r.get("provenance") or "-").split("+"):
            if word != "-":
                words_seen.setdefault(word, []).append(r["n"])
    mixed = len(words_seen) > 1
    if mixed:
        notes.append("the scenes do not share one provenance word: " + "; ".join(
            "%s in scene(s) %s" % (w, ", ".join(str(n) for n in ns)) for w, ns in words_seen.items()))

    # The subtitles: each joined scene's rows from its folder's captions.tsv, or the story's own.
    captions, labels = [], []
    stamped = []
    if mode != "off":
        for r in rows:
            if r["status"] != "joined" or r["n"] == 0:
                continue
            folder = os.path.dirname(r["path"])
            if folder not in read:
                read[folder] = read_captions(folder)
            by_slug, _ = read[folder]
            slug = os.path.splitext(r["clip"])[0]
            s0, length = float(r["start"]), float(r["length"])
            entry = by_slug.get(slug)
            if entry and entry["takes"]:
                lengths = take_lengths(entry, folder, slug, notes)
                c, l = events_from_rows(entry, lengths, s0, length, with_label)
                captions += c
                labels += l
                r["subtitles"] = "%d caption(s), %d label tick(s) from captions.tsv" % (len(c), len(l))
                if entry["captions_in_frames"] or entry["label_in_frames"]:
                    stamped.append(r["n"])
                    r["subtitles"] += "; its frames carry stamped %s" % (
                        "captions and label" if entry["captions_in_frames"] and entry["label_in_frames"]
                        else "captions" if entry["captions_in_frames"] else "label")
            else:
                c = story_captions(r, chapters.get(r["n"]), s0, length)
                captions += c
                stamped.append(r["n"])
                r["subtitles"] = "%d caption(s) from the story (no captions.tsv rows: %s); no label" % (len(c), read[folder][1])

        width, height = (shapes[0]["width"], shapes[0]["height"]) if shapes else (1920, 1080)
        write_ass(ass, title, width, height, captions, labels)

    burning = mode == "burn" or (mode == "auto" and not stamped)
    if mode == "auto" and stamped and not ass_only:
        notes.append("not burnt: %d scene(s) carry text stamped into their frames (%s); the .ass is written beside the film; "
                     "--subtitles burn burns it over them" % (len(stamped), ", ".join(str(n) for n in stamped)))

    print("%4s  %-8s  %-10s  %-44s  %8s  %-10s  %s" % ("n", "run", "station", "clip", "length", "provenance", "status"))
    for r in rows:
        print("%4d  %-8s  %-10s  %-44s  %8s  %-10s  %s" % (
            r["n"], r["run"][:8], r["station"][:10], (r["clip"] or "-")[:44],
            ("%.1f s" % float(r["length"])) if r["length"] else "-", r.get("provenance") or "-", r["status"]))
    for note in notes:
        print("  note: " + note)

    def write_table():
        with open(table, "w", encoding="utf-8", newline="\n") as f:
            f.write("n\trun\tact\tstation\tsubject\tclip\tstart_s\tlength_s\tstatus\tpath\tsubtitles\tprovenance\n")
            for r in rows:
                f.write("\t".join(str(x) for x in (r["n"], r["run"], r["act"], r["station"], r["subject"], r["clip"],
                                                    r["start"], r["length"], r["status"], r["path"], r.get("subtitles", ""),
                                                    r.get("provenance", ""))) + "\n")

    if ass_only:
        write_table()
        print("subtitles: %s (%d captions, %d label ticks), nothing joined (--ass-only)" % (ass, len(captions), len(labels)))
        print("table: " + table)
        return

    joined = stem + ".clean.mp4" if burning else output
    ok, how = join(clips, shapes, joined)
    if not ok:
        sys.exit("ffmpeg failed to join the clips (" + how + ")")

    if burning:
        ok, said = burn(joined, ass, output, fonts_dir, threads)
        if not ok:
            sys.exit("ffmpeg failed to burn the subtitles (" + said + "); the joined film is " + joined)
        how += "; " + said + "; the film without them is " + joined

    write_table()
    film = probe(output)
    skipped = sum(1 for r in rows if r["status"] != "joined")
    scenes_joined = sum(1 for r in rows if r["status"] == "joined" and r["n"] != 0)
    print("%s: %d of %d scenes%s, %.1f s (%s); %d skipped" % (
        output, scenes_joined, len([r for r in rows if r["n"] != 0]), " and the title" if card else "",
        film["duration"] if film else start, how, skipped))
    if mode != "off":
        print("subtitles: %s (%d captions, %d label ticks)%s" % (ass, len(captions), len(labels), " burnt in" if burning else ", not burnt"))
    if words_seen:
        print("provenance: %s%s" % (", ".join("%s %d" % (w, len(ns)) for w, ns in words_seen.items()),
                                    " (MIXED: each scene's corner names its own)" if mixed else ""))
    print("table: " + table)


if __name__ == "__main__":
    main()
