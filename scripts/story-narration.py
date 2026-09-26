#!/usr/bin/env python3
"""A story's narration: the paragraphs to speak, and the story timed from what was spoken.

    python scripts/story-narration.py segments <story folder> [--chapters]
    python scripts/story-narration.py timing   <story folder> [--lead 0.8] [--gap 1.2] [--tail 1.0] [--keep-length]
    python scripts/story-narration.py check    <story folder>

The narration stage of the story-film flow (`logbook/specs/story-narration.md`) speaks each
scene's captions as paragraphs before anything is filmed, and the lengths of the speech set the
scenes' lengths. The service that speaks them is separate; this is the flow's side of it.

- `segments` writes `narration/segments.json`: one segment a paragraph, `sNN-pK` for scene NN's
  paragraph K, its cues the paragraph's captions in order (a caption that carries
  `new_paragraph: true` opens a paragraph). `--chapters` adds each chapter's title as a segment
  `cNN`, spoken over the chapter card before scene NN. The file carries the hash of the story's
  caption texts, which the narration must match.
- `timing` reads `narration/narration.json`, the manifest the stage writes from the service's
  results (its form is below), checks that it speaks the story's captions as they stand, and
  writes into `story.json` each caption's `at` and `seconds` and each scene's `seconds`, with the
  scene's narration (segment, file, offset) under `narration`. The first run keeps the story as
  it was as `story.pre-timing.json`. A scene is: the lead, its paragraphs with the gap between
  them, the tail. A caption is on screen from its cue's start to its end plus 0.4 s, and ends
  0.1 s before the next caption. It prints each scene's length against the one before. The
  narration sets the length (the owner, 2026-09-25); a scene cut by more than a quarter is named,
  because its picture may need the time (a descent's dolly, a birth's wait), and `--keep-length`
  keeps every scene at least as long as it was. A scene's `least_seconds`, which the writer sets
  where the picture holds an event at a known second (a birth, a death), is a floor the narration
  never cuts under.
- `check` reports, without writing, whether the manifest matches the story, and every flag
  `timing` would raise.

The manifest, `narration/narration.json`:

    {"version": 1, "story_captions": "<hash from segments.json>",
     "service": {"voice_id": ..., "voice_hash": ..., "script_id": ..., "cut_no": ..., "human_approved": ...},
     "segments": [{"segment_id": "s01-p1", "file": "narration/s01-p1.wav", "sha256": ...,
                   "samples": 123456, "sample_rate": 48000,
                   "cues": [{"index": 0, "written": ..., "spoken": ..., "start_s": 0.08, "end_s": 1.62}]}]}

`start_s` and `end_s` are in the segment's own file, as the service returns them (R1 of the spec).
"""
import hashlib
import io
import json
import math
import os
import re
import shutil
import sys

LEAD, GAP, TAIL = 0.8, 1.2, 1.0
AFTER, BEFORE_NEXT = 0.4, 0.1
READABLE = 17.0      # characters a second a viewer can read a caption at
CHAPTER_CARD = 8.0   # the director's chapter card, s


def load(path):
    with io.open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def save(path, data):
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")


def scenes_of(story):
    return story["scenes"] if isinstance(story, dict) else story


def captions_of(scene):
    given = scene.get("captions") or []
    if isinstance(given, str):
        given = [given]
    return [c if isinstance(c, dict) else {"text": str(c)} for c in given]


def captions_hash(story):
    """The hash of the story's caption texts, as scripts/story-flow.py takes it."""
    items = [(s["n"], [c.get("text") for c in captions_of(s)]) for s in scenes_of(story)]
    return hashlib.sha256(json.dumps(items, ensure_ascii=False).encode("utf-8")).hexdigest()[:16]


def paragraphs(scene):
    """[[caption index, ...], ...]: the scene's captions grouped where one opens a new paragraph."""
    groups = []
    for k, c in enumerate(captions_of(scene)):
        if not groups or (k > 0 and c.get("new_paragraph")):
            groups.append([])
        groups[-1].append(k)
    return groups


def plain(text):
    return " ".join(str(text).split())


def cmd_segments(folder, chapters):
    story = load(os.path.join(folder, "story.json"))
    segments, chapter = [], 0
    for s in scenes_of(story):
        caps = captions_of(s)
        if s.get("chapter"):
            chapter += 1
            if chapters:
                segments.append({"segment_id": "c%02d" % s["n"], "scene": s["n"], "kind": "chapter",
                                 "cues": [{"index": 0, "caption": None,
                                           "text": "Chapter %d: %s." % (chapter, plain(s["chapter"]).rstrip("."))}]})
        for p, group in enumerate(paragraphs(s), 1):
            segments.append({"segment_id": "s%02d-p%d" % (s["n"], p), "scene": s["n"], "paragraph": p, "kind": "paragraph",
                             "cues": [{"index": i, "caption": k, "text": plain(caps[k].get("text", ""))}
                                      for i, k in enumerate(group)]})
    os.makedirs(os.path.join(folder, "narration"), exist_ok=True)
    path = os.path.join(folder, "narration", "segments.json")
    save(path, {"version": 1, "story_captions": captions_hash(story), "segments": segments})
    chars = [sum(len(c["text"]) for c in seg["cues"]) for seg in segments]
    print("segments: %s (%d segments, %d to %d characters, %d in all)" % (
        path, len(segments), min(chars or [0]), max(chars or [0]), sum(chars)))


def plan(folder, lead, gap, tail, keep=False):
    """(the story timed from the manifest, [(scene, old s, new s, paragraphs)], [(level, scene, message)])."""
    story = load(os.path.join(folder, "story.json"))
    manifest = load(os.path.join(folder, "narration", "narration.json"))
    flags, rows = [], []
    if manifest.get("story_captions") != captions_hash(story):
        flags.append(("ERROR", "-", "the narration was made for other captions (hash %s, the story's is %s): make the "
                      "segments again and narrate the changed paragraphs" % (manifest.get("story_captions"), captions_hash(story))))
    by_id = {seg["segment_id"]: seg for seg in manifest.get("segments", [])}
    for s in scenes_of(story):
        n, caps, groups = s["n"], captions_of(s), paragraphs(s)
        t, placed, entries, ok = lead, [None] * len(caps), [], True
        chapter = by_id.get("c%02d" % n)
        if chapter and s.get("chapter"):
            d = chapter["samples"] / float(chapter["sample_rate"])
            entries.append({"segment_id": chapter["segment_id"], "file": chapter["file"], "at": lead, "seconds": round(d, 3),
                            "on_card": True})
            if lead + d > CHAPTER_CARD - 0.5:
                flags.append(("WARN", n, "the chapter title takes %.1f s, too long for the %g s card" % (d, CHAPTER_CARD)))
        for p, group in enumerate(groups, 1):
            sid = "s%02d-p%d" % (n, p)
            seg = by_id.get(sid)
            if seg is None:
                flags.append(("ERROR", n, "no narration for %s" % sid))
                ok = False
                break
            said = [plain(c.get("written", "")) for c in seg.get("cues", [])]
            wanted = [plain(caps[k].get("text", "")) for k in group]
            if said != wanted:
                flags.append(("ERROR", n, "%s speaks other captions than the story's: %s" % (sid, said)))
                ok = False
                break
            d = seg["samples"] / float(seg["sample_rate"])
            for cue, k in zip(seg["cues"], group):
                if cue.get("start_s") is None or cue.get("end_s") is None:
                    flags.append(("ERROR", n, "%s cue %d has no time in the audio" % (sid, cue.get("index", -1))))
                    ok = False
                    continue
                placed[k] = (t + float(cue["start_s"]), t + float(cue["end_s"]))
            entries.append({"segment_id": sid, "file": seg["file"], "at": round(t, 3), "seconds": round(d, 3)})
            t += d + (gap if p < len(groups) else 0.0)
        if not ok or not groups:
            continue
        new_caps = []
        for k, c in enumerate(caps):
            a, b = placed[k]
            b += AFTER
            if k + 1 < len(caps):
                b = min(b, placed[k + 1][0] - BEFORE_NEXT)
            b = max(b, a + 0.3)
            text = plain(c.get("text", ""))
            if len(text) / (b - a) > READABLE:
                flags.append(("WARN", n, "caption %d is on screen %.1f s, too briefly to read %d characters" % (k + 1, b - a, len(text))))
            new = {key: v for key, v in c.items() if key not in ("at", "seconds")}
            new["at"], new["seconds"] = round(a, 2), round(b - a, 2)
            new_caps.append(new)
        old = float(s.get("seconds") or 0)
        seconds = math.ceil((t + tail) * 10.0) / 10.0
        least = float(s.get("least_seconds") or 0)
        if seconds < least:
            flags.append(("INFO", n, "held at %g s, the scene's least_seconds, past its narration's %g s" % (least, seconds)))
            seconds = least
        if keep:
            seconds = max(seconds, old)
        elif old and seconds < 0.75 * old:
            flags.append(("WARN", n, "the narration cuts the scene from %g s to %g s; check that the picture has its "
                          "time (--keep-length keeps it)" % (old, seconds)))
        chart = s.get("chart")
        if isinstance(chart, dict):
            if chart.get("until") is not None and float(chart["until"]) > seconds:
                flags.append(("INFO", n, "the chart's until %s is past the scene's %g s and is cut to it" % (chart["until"], seconds)))
            if chart.get("at") is not None and float(chart["at"]) > seconds - 2.0:
                flags.append(("WARN", n, "the chart starts at %s s, at the end of a %g s scene" % (chart["at"], seconds)))
        rows.append((n, old, seconds, len(groups)))
        s["captions"], s["seconds"], s["narration"] = new_caps, seconds, entries
        if isinstance(chart, dict) and chart.get("until") is not None and float(chart["until"]) > seconds:
            chart["until"] = seconds
    return story, rows, flags, manifest


def cmd_timing(folder, lead, gap, tail, write, keep):
    story, rows, flags, manifest = plan(folder, lead, gap, tail, keep)
    for level, n, message in flags:
        print("%-5s  scene %s: %s" % (level, n, message))
    errors = sum(1 for f in flags if f[0] == "ERROR")
    print("%6s %10s %10s %11s" % ("scene", "was s", "now s", "paragraphs"))
    for n, old, new, p in rows:
        print("%6d %10.1f %10.1f %11d" % (n, old, new, p))
    cards = sum(1 for s in scenes_of(story) if s.get("chapter"))
    total = sum(float(s.get("seconds") or 0) for s in scenes_of(story)) + CHAPTER_CARD * cards
    print("the film: %d min %02d s of scenes and chapter cards, the title not counted" % (total // 60, total % 60))
    if not write:
        return 1 if errors else 0
    if errors:
        print("not written: %d error(s)" % errors)
        return 1
    keep = os.path.join(folder, "story.pre-timing.json")
    if not os.path.isfile(keep):
        shutil.copyfile(os.path.join(folder, "story.json"), keep)
        print("kept the story before timing as " + keep)
    if isinstance(story, dict):
        with open(os.path.join(folder, "narration", "narration.json"), "rb") as f:
            digest = hashlib.sha256(f.read()).hexdigest()[:16]
        story["narration"] = {"padding": {"lead_s": lead, "paragraph_gap_s": gap, "tail_s": tail,
                                          "caption_after_s": AFTER, "caption_before_next_s": BEFORE_NEXT},
                              "manifest": digest, "service": manifest.get("service", {})}
    save(os.path.join(folder, "story.json"), story)
    print("timed: %s" % os.path.join(folder, "story.json"))
    return 0


def option(args, name, default):
    if name in args:
        i = args.index(name)
        value = float(args[i + 1])
        del args[i:i + 2]
        return value
    return default


def main():
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    args = sys.argv[1:]
    chapters = "--chapters" in args
    keep = "--keep-length" in args
    args = [a for a in args if a not in ("--chapters", "--keep-length")]
    lead, gap, tail = option(args, "--lead", LEAD), option(args, "--gap", GAP), option(args, "--tail", TAIL)
    if len(args) != 2 or args[0] not in ("segments", "timing", "check"):
        print("\n".join(l.strip() for l in __doc__.strip().splitlines()[2:5]))
        sys.exit(2)
    command, folder = args
    if not os.path.isfile(os.path.join(folder, "story.json")):
        sys.exit("no story.json in " + folder)
    if command == "segments":
        cmd_segments(folder, chapters)
    else:
        if not os.path.isfile(os.path.join(folder, "narration", "narration.json")):
            sys.exit("no narration/narration.json in %s: the stage writes it from the service's results" % folder)
        sys.exit(cmd_timing(folder, lead, gap, tail, command == "timing", keep))


if __name__ == "__main__":
    main()
