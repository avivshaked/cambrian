#!/usr/bin/env python3
"""Join a story's clips into one film, in the story's order.

    python scripts/story-assemble.py <story.json> <output.mp4> <clips folder> [<clips folder> ...]
        [--title-seconds 5] [--no-title]

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
PATH; nothing else. It writes the film, the table and, while it joins, a list file beside the film.

When the story has a `title`, the film opens on it: white on black for `--title-seconds` (5),
at the first clip's size and rate, drawn by ffmpeg's drawtext with a Windows font named outright
(ffmpeg's own default font lookup crashes on this machine); `--no-title` leaves it out, and a
machine with none of the fonts leaves it out with a note.

The story is read as the director reads it: the scenes under `scenes` (or `shots`, or the file as a
list), each scene's number under `n` (or `number`), its run under `run` (or `arm`), and the rest
for the table only; a scene without a number takes its place in the file.
"""
import json
import os
import re
import subprocess
import sys

CLIP = re.compile(r"^story-(\d+)-(.+)\.mp4$", re.IGNORECASE)


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


def text(value):
    if value is None:
        return ""
    if isinstance(value, dict):
        parts = [str(v) for k, v in value.items() if k in ("name", "clade", "root", "body", "text")]
        return " ".join(parts)
    return str(value).replace("\t", " ").replace("\n", " ").strip()


def story_title(path):
    with open(path, encoding="utf-8-sig") as f:
        root = json.load(f)
    title = root.get("title") if isinstance(root, dict) else None
    return title.strip() if isinstance(title, str) and title.strip() else None


FONTS = ("georgia.ttf", "segoeui.ttf", "arial.ttf")


def title_card(title, shape, seconds, output):
    """A held title, white on black, at the first clip's size and rate. (path, note)."""
    fonts = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")
    font = next((os.path.join(fonts, f) for f in FONTS if os.path.isfile(os.path.join(fonts, f))), None)
    if font is None:
        return None, "no title card: none of %s in %s" % (", ".join(FONTS), fonts)

    stem = os.path.splitext(output)[0]
    card, words = stem + ".title.mp4", stem + ".title.txt"
    with open(words, "w", encoding="utf-8") as f:
        f.write(title)

    def escaped(path):
        return os.path.abspath(path).replace("\\", "/").replace(":", "\\:")

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


def main():
    # A console that cannot print a character (an em dash in a station on cp1252) prints a
    # stand-in rather than stopping the join.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(errors="replace")
        except (AttributeError, ValueError):
            pass
    args = sys.argv[1:]
    with_title, title_seconds = True, 5.0
    if "--no-title" in args:
        args.remove("--no-title")
        with_title = False
    if "--title-seconds" in args:
        i = args.index("--title-seconds")
        try:
            title_seconds = float(args[i + 1])
        except (IndexError, ValueError):
            sys.exit("--title-seconds wants a number of seconds")
        del args[i:i + 2]
    if len(args) < 3:
        print("\n".join(l.strip() for l in __doc__.strip().splitlines()[2:4]), file=sys.stderr)
        sys.exit(2)
    story_path, output, folders = args[0], args[1], args[2:]
    if not os.path.isfile(story_path):
        sys.exit("no story at " + story_path)

    scenes = read_story(story_path)
    if not scenes:
        sys.exit("the story at %s has no scenes" % story_path)
    found = find_clips(folders)

    rows, clips, shapes, notes = [], [], [], []
    numbers = {s["n"] for s in scenes}
    for n in sorted(set(found) - numbers):
        notes.append("clip(s) numbered %d have no scene in the story and are left out: %s"
                     % (n, ", ".join(os.path.basename(p) for p in found[n])))

    start = 0.0
    used = set()
    for s in scenes:
        n = s["n"]
        row = dict(s, clip="", path="", start="", length="", status="")
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
                   length="%.3f" % shape["duration"], status="joined")
        start += shape["duration"]
        clips.append(path)
        shapes.append(shape)
        rows.append(row)

    print("%4s  %-8s  %-10s  %-44s  %8s  %s" % ("n", "run", "station", "clip", "length", "status"))
    for r in rows:
        print("%4d  %-8s  %-10s  %-44s  %8s  %s" % (
            r["n"], r["run"][:8], r["station"][:10], (r["clip"] or "-")[:44],
            ("%.1f s" % float(r["length"])) if r["length"] else "-", r["status"]))
    for note in notes:
        print("  note: " + note)

    table = os.path.splitext(output)[0] + ".scenes.tsv"
    if not clips:
        print("no clip found for any scene: nothing joined")
        sys.exit(1)

    os.makedirs(os.path.dirname(os.path.abspath(output)), exist_ok=True)

    # The title first, at the first clip's size and rate, so the streams still copy.
    title = story_title(story_path) if with_title and title_seconds > 0 else None
    card = None
    if title:
        card, said = title_card(title, shapes[0], title_seconds, output)
        print("  " + said)
        if card:
            shape = probe(card)
            clips.insert(0, card)
            shapes.insert(0, shape)
            for r in rows:
                if r["start"]:
                    r["start"] = "%.3f" % (float(r["start"]) + shape["duration"])
            rows.insert(0, {"n": 0, "run": "", "act": "", "station": "title", "subject": title, "clip": os.path.basename(card),
                            "path": os.path.abspath(card), "start": "0.000", "length": "%.3f" % shape["duration"],
                            "status": "joined"})

    ok, how = join(clips, shapes, output)
    if not ok:
        sys.exit("ffmpeg failed to join the clips (" + how + ")")

    with open(table, "w", encoding="utf-8", newline="\n") as f:
        f.write("n\trun\tact\tstation\tsubject\tclip\tstart_s\tlength_s\tstatus\tpath\n")
        for r in rows:
            f.write("\t".join(str(x) for x in (r["n"], r["run"], r["act"], r["station"], r["subject"], r["clip"],
                                                r["start"], r["length"], r["status"], r["path"])) + "\n")

    film = probe(output)
    skipped = sum(1 for r in rows if r["status"] != "joined")
    scenes_joined = sum(1 for r in rows if r["status"] == "joined" and r["n"] != 0)
    print("%s: %d of %d scenes%s, %.1f s (%s); %d skipped" % (
        output, scenes_joined, len([r for r in rows if r["n"] != 0]), " and the title" if card else "",
        film["duration"] if film else start, how, skipped))
    print("table: " + table)


if __name__ == "__main__":
    main()
