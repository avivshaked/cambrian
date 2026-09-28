#!/usr/bin/env python3
"""The narration step's tool, for the create-story-video skill (narration.md).

    python narration.py request <folder> <n>    build draft n's request to the narration service
    python narration.py machine                  say whether the machine is free for the voice
    python narration.py collect <folder> <n>    check draft n's takes, cut the pieces, build the timeline
    python narration.py own <folder> <n>        build draft n from the owner's own files
    python narration.py estimate <folder>       write narration.json from an estimate, with no sound
    python narration.py check <narration json>  check a narration file against narration-schema.md
    python narration.py sheet <narration json>  print one line a passage, for the owner
    python narration.py keep <folder> <n>       copy approved draft n into narration/ and narration.json

The first line each command prints is its verdict, as narration.md reads it. The last line says how long
the command took. Exit status: 0 on READY, FREE, PASS, WRITTEN or DONE, 1 otherwise, 2 on a wrong command.
It uses the standard library only, and reads the script through script-check.py's own parser.
"""

import contextlib
import hashlib
import importlib.util
import io
import json
import math
import re
import shutil
import struct
import subprocess
import sys
import time
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("script_check", HERE / "script-check.py")
sc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sc)

SCHEMA = "create-story-video/narration/1"
SOURCES = ("service", "own files", "estimate")
SETTINGS, HINTS = "narration-settings.json", "pronunciation.json"
ESTIMATE_RATE = 48000            # an estimate's timeline is still in whole samples
FILM_MIN, FILM_MAX = 5 * 60, 7 * 60
# The service's own limits (narrator-mcp: narration.example.toml's [limits] and contracts/schemas.py).
MAX_SEGMENTS, MAX_CUES, MAX_CUE_CHARS = 200, 40, 600
MAX_SEGMENT_CHARS, MAX_JOB_CHARS, MAX_HINTS, MAX_ATTEMPT = 1200, 60000, 500, 99
# The service asks for about 8 to 10 segments a job, since a larger job has results too long to read in one
# answer. So a draft is one job a beat, cut into jobs of at most this many segments.
JOB_SEGMENTS = 10
SEGMENT_ID = re.compile(r"[a-z0-9][a-z0-9._-]{0,63}")
SHA256 = re.compile(r"[0-9a-f]{64}")
FLAG_CODE = re.compile(r"[A-Z][A-Z0-9_]*")
# The silence reading mirrors the service's pause finder (narrator-mcp: src/narration/align/snap.py):
# 20 ms frames, silent when more than 35 dB under the take's 95th-percentile frame level.
FRAME_S, QUIET_DB = 0.02, 35.0
NARRATION_KEYS = {"schema", "source", "script_sha256", "draft", "sample_rate", "length_s", "padding", "cut",
                  "service", "segments", "pieces", "passages"}
EPS = 0.0015                     # seconds: every time is rounded to the millisecond


class Refused(Exception):
    """A command that cannot go on; its lines say why."""

    def __init__(self, *lines):
        super().__init__(lines[0] if lines else "")
        self.lines = list(lines)


# ---------------------------------------------------------------- files

def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None


def write_json(path, data):
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def read_wav(path):
    """A PCM WAV's (channels, sample width in bytes, rate, raw frames). Reads plain and extensible PCM."""
    data = Path(path).read_bytes()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError(f"{path} is not a WAV file")
    fmt = raw = None
    at = 12
    while at + 8 <= len(data):
        kind, size = data[at:at + 4], struct.unpack("<I", data[at + 4:at + 8])[0]
        body = data[at + 8:at + 8 + size]
        if kind == b"fmt ":
            tag, channels, rate = struct.unpack("<HHI", body[:8])
            bits = struct.unpack("<H", body[14:16])[0]
            if tag == 0xFFFE and len(body) >= 26:
                tag = struct.unpack("<H", body[24:26])[0]
            if tag != 1:
                raise ValueError(f"{path} is not PCM (format {tag})")
            fmt = (channels, bits // 8, rate)
        elif kind == b"data":
            raw = body
        at += 8 + size + (size & 1)
    if fmt is None or raw is None:
        raise ValueError(f"{path} has no fmt or data chunk")
    channels, width, rate = fmt
    frame = channels * width
    return channels, width, rate, raw[:len(raw) // frame * frame]


def write_wav(path, channels, width, rate, raw):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(width)
        w.setframerate(rate)
        w.writeframes(raw)


def frame_levels(channels, width, rate, raw):
    """Each 20 ms frame's level in dB of full scale, read from every fourth sample of the first channel."""
    step, frame = channels * width, max(1, round(FRAME_S * rate))
    count = len(raw) // step
    full = float(2 ** (8 * width - 1))
    levels = []
    for start in range(0, count - frame + 1, frame):
        acc = n = 0
        for i in range(start, start + frame, 4):
            o = i * step
            v = raw[o] - 128 if width == 1 else int.from_bytes(raw[o:o + width], "little", signed=True)
            acc += v * v
            n += 1
        rms = math.sqrt(acc / n) if n else 0.0
        levels.append(20 * math.log10(rms / full) if rms > 0 else -200.0)
    return levels, frame


def silent(levels, frame, a, b):
    """Whether every whole frame between samples a and b is quiet, and there is at least one."""
    if not levels:
        return False
    top = sorted(levels)[int(0.95 * (len(levels) - 1))]
    inside = range(-(-a // frame), b // frame)
    return len(inside) > 0 and all(levels[i] < top - QUIET_DB for i in inside if i < len(levels))


# ---------------------------------------------------------------- the script and the settings

def load_script(folder):
    """The approved script's passages and sentences, each sentence with its prose paragraph."""
    path = folder / "script.md"
    if not path.is_file():
        raise Refused(f"cannot read {path}")
    printed = io.StringIO()
    with contextlib.redirect_stdout(printed):
        status = sc.check_script(path)
    if status != 0:
        raise Refused("script.md does not pass script-check.py script:",
                      *("  " + line for line in printed.getvalue().splitlines()))
    script, _ = sc.parse_script(sc.read(path))
    prose, _ = sc.parse_prose(sc.read(folder / "prose.md"))
    passages = [{"id": p["id"], "say": [sc.squash(sc.unmark(s)) for s in p["say"]], "hold": float(p["hold"] or 0)}
                for beat in script["beats"] for p in beat["passages"]]
    paragraph = [(b["number"], i) for b in prose["beats"] for i, para in enumerate(b["paragraphs"], 1) for _ in para]
    sentences = [{"passage": p["id"], "place": k, "text": t} for p in passages for k, t in enumerate(p["say"], 1)]
    if len(paragraph) != len(sentences):
        raise Refused("prose.md and script.md hold different numbers of sentences")
    for s, key in zip(sentences, paragraph):
        s["paragraph"] = key
    given = sorted({sc.squash(g) for beat in script["beats"] for p in beat["passages"] for s in p["say"]
                    for g in sc.GIVEN.findall(s)})
    return {"passages": passages, "sentences": sentences, "given": given, "sha256": sha256_of(path)}


def film_settings(folder, lines):
    """The film's settings and pronunciations, first copied from the skill's defaults if the film has none."""
    for name in (SETTINGS, HINTS):
        if not (folder / name).is_file():
            shutil.copyfile(HERE / name, folder / name)
            lines.append(f"copied the skill's {name} into the film's folder")
    settings, hints = read_json(folder / SETTINGS), read_json(folder / HINTS)
    problems = []
    pad = settings.get("padding") or {}
    for key in ("lead_s", "between_s", "within_s", "end_s"):
        if not isinstance(pad.get(key), (int, float)) or pad[key] < 0:
            problems.append(f"padding.{key} is a number of seconds, 0 or more")
    if not problems and pad["lead_s"] > pad["between_s"]:
        problems.append("padding.lead_s must not exceed padding.between_s, or a span would start before the "
                        "passage before it has finished")
    cut = settings.get("cut") or {}
    for key in ("min_pause_s", "edge_s"):
        if not isinstance(cut.get(key), (int, float)) or cut[key] < 0:
            problems.append(f"cut.{key} is a number of seconds, 0 or more")
    if not problems:
        for key, times in (("lead_s", 1), ("end_s", 1), ("between_s", 2), ("within_s", 2)):
            if pad[key] < times * cut["edge_s"]:
                problems.append(f"padding.{key} must be at least {'twice ' if times == 2 else ''}cut.edge_s, or "
                                "a piece's kept silence could overlap the next piece or the narration's ends")
    if settings.get("takes") not in (1, 2, 3):
        problems.append("takes is 1, 2 or 3")
    if settings.get("max_retakes") not in (0, 1, 2, 3):
        problems.append("max_retakes is 0 to 3")
    accept = settings.get("accept")
    if not isinstance(accept, list) or any(not isinstance(c, str) or not FLAG_CODE.fullmatch(c) for c in accept):
        problems.append("accept is a list of the service's flag codes the owner ruled do not fail a take, [] for none")
    if not isinstance(hints, list) or any(not isinstance(h, dict) or not h.get("term") for h in hints):
        problems.append(f"{HINTS} is a list of the service's hints, each with its term")
    if problems:
        raise Refused(f"the film's {SETTINGS} or {HINTS} is not in its form:", *("  " + p for p in problems))
    return settings, hints


def canon_voice(settings):
    voice = settings.get("voice")
    if not voice:
        raise Refused("the settings name no canon voice; the owner locks one in outside this skill")
    problems = []
    for key in ("path", "transcript", "engine_profile_hash"):
        if not isinstance(voice.get(key), str) or not voice[key]:
            problems.append(f"voice.{key} is missing")
    if not SHA256.fullmatch(str(voice.get("sha256", ""))):
        problems.append("voice.sha256 is 64 lower-case hex digits")
    limit = voice.get("max_segment_chars")
    if not isinstance(limit, int) or not 0 < limit <= MAX_SEGMENT_CHARS:
        problems.append(f"voice.max_segment_chars is the voice's measured limit, 1 to {MAX_SEGMENT_CHARS}")
    if problems:
        raise Refused("the canon voice in the settings is not in its form:", *("  " + p for p in problems))
    return voice

def fail_codes(flags):
    """The codes of a take's flags that fail it: those of severity fail or error."""
    return {f.get("code") for f in flags or [] if f.get("severity") in ("fail", "error")}


def accepted(take, accept):
    """The codes that failed a take, when the film's settings accept every one of them; else []."""
    codes = fail_codes(take.get("flags"))
    if take.get("verdict") != "fail" or not codes or not codes <= set(accept):
        return []
    return sorted(codes)

# ---------------------------------------------------------------- the timeline

def needs_silence(passages, pid):
    """Whether the boundary after a spoken passage needs a pause the edit can widen: it has a hold, or a
    passage with no Say: comes next."""
    at = [p["id"] for p in passages].index(pid)
    return passages[at]["hold"] > 0 or (at + 1 < len(passages) and not passages[at + 1]["say"])


def lay_out(passages, pieces, padding, rate):
    """Place the pieces on the timeline, in samples. Each piece has 'words': one (passage, place, a, b) a
    sentence, with a and b in samples from the piece's start, in order. Sets each piece's 'at' and returns
    the timeline's length."""
    s = lambda seconds: round(seconds * rate)
    lead, between, within, end = (s(padding[k]) for k in ("lead_s", "between_s", "within_s", "end_s"))
    order = [p["id"] for p in passages]
    index = {pid: i for i, pid in enumerate(order)}
    hold = {p["id"]: s(p["hold"]) for p in passages}
    quiet = [not p["say"] for p in passages]

    def silent_holds(lo, hi):
        return sum(hold[order[i]] for i in range(lo + 1, hi) if quiet[i])

    last_word = last_passage = None
    for piece in pieces:
        pid, place, a, _ = piece["words"][0]
        if last_word is None:
            first_word = lead + silent_holds(-1, index[pid])
        elif place == 1:
            first_word = (last_word + between + hold[last_passage]
                          + silent_holds(index[last_passage], index[pid]))
        else:
            first_word = last_word + within
        piece["at"] = first_word - a
        last_passage, _, _, b = piece["words"][-1]
        last_word = piece["at"] + b
    return last_word + hold[last_passage] + silent_holds(index[last_passage], len(order)) + end


def spans(passages, pieces, padding, rate, length):
    """Each passage's span and speech, in samples: {id: [start, end, speech_from, speech_to]}."""
    lead = round(padding["lead_s"] * rate)
    first_of_piece = {(p["words"][0][0], p["words"][0][1]) for p in pieces}
    speech = {}
    for piece in pieces:
        for pid, place, a, b in piece["words"]:
            lo, hi = piece["at"] + a, piece["at"] + b
            f, t = speech.get(pid, (lo, hi))
            speech[pid] = (min(f, lo), max(t, hi))
    starts, before = {}, None
    for p in passages:
        if not p["say"]:
            continue
        f, t = speech[p["id"]]
        if (p["id"], 1) in first_of_piece or before is None:
            starts[p["id"]] = f - lead
        else:
            starts[p["id"]] = f - min(lead, (f - before) // 2)
        before = t
    out, following = {}, length
    for p in reversed(passages):
        start = starts[p["id"]] if p["say"] else following - round(p["hold"] * rate)
        f, t = speech.get(p["id"], (None, None))
        out[p["id"]] = [start, following, f, t]
        following = start
    out[passages[0]["id"]][0] = 0
    return out


def sec(samples, rate):
    return None if samples is None else round(samples / rate, 3)


def timeline(source, draft, rate, script, settings, pieces, segments, service, files):
    """The narration file's data, in the form narration-schema.md fixes. files maps a piece's id to its
    file: its path relative to the film's folder, its sha256 and its samples."""
    padding, passages = settings["padding"], script["passages"]
    length = lay_out(passages, pieces, padding, rate)
    length = round(-(-length * 1000 // rate) * rate / 1000)
    span = spans(passages, pieces, padding, rate, length)
    where = {}
    for piece in pieces:
        for pid, place, a, b in piece["words"]:
            where[(pid, place)] = (piece["id"], piece["at"] + a, piece["at"] + b)
    out = []
    for p in passages:
        start, end, f, t = span[p["id"]]
        sentences = []
        for place, text in enumerate(p["say"], 1):
            if source == "own files":
                pid, lo, hi = where[(p["id"], 1)][0], None, None
            else:
                pid, lo, hi = where[(p["id"], place)]
            sentences.append({"place": place, "text": text, "piece": None if source == "estimate" else pid,
                              "from_s": sec(lo, rate), "to_s": sec(hi, rate)})
        out.append({"id": p["id"], "from_s": sec(start, rate), "to_s": sec(end, rate),
                    "speech_from_s": sec(f, rate), "speech_to_s": sec(t, rate), "hold_s": p["hold"],
                    "sentences": sentences})
    return {
        "schema": SCHEMA, "source": source, "script_sha256": script["sha256"], "draft": draft,
        "sample_rate": rate, "length_s": sec(length, rate), "padding": dict(padding),
        "cut": dict(settings["cut"]), "service": service, "segments": segments,
        "pieces": [] if source == "estimate" else [
            {"id": p["id"], "file": files[p["id"]], "segment": p.get("segment"),
             "take_from_sample": p.get("from"), "take_to_sample": p.get("to"),
             "at_sample": p["at"], "at_s": sec(p["at"], rate),
             "passages": list(dict.fromkeys(w[0] for w in p["words"]))} for p in pieces],
        "passages": out,
    }


def write_draft(folder, n, data, pieces, fmt):
    """Write draft n's joined file, for listening. fmt is (channels, width, rate)."""
    channels, width, rate = fmt
    frame = channels * width
    length = round(data["length_s"] * rate)
    with wave.open(str(folder / f"narration-{n}.wav"), "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(width)
        w.setframerate(rate)
        cursor = 0
        for piece in sorted(pieces, key=lambda p: p["at"]):
            if piece["at"] < cursor:
                raise Refused(f"piece {piece['id']} overlaps the piece before it")
            w.writeframes(b"\0" * ((piece["at"] - cursor) * frame))
            w.writeframes(piece["raw"])
            cursor = piece["at"] + len(piece["raw"]) // frame
        if cursor > length:
            raise Refused("the last piece runs past the narration's end")
        w.writeframes(b"\0" * ((length - cursor) * frame))


def fresh_dir(path):
    """An empty folder for a draft's pieces. A folder this tool made before holds only its WAV files."""
    if path.exists():
        if any(f.suffix != ".wav" for f in path.iterdir()):
            raise Refused(f"{path} holds files this tool did not write; the owner decides what becomes of them")
        shutil.rmtree(path)
    path.mkdir()


def minutes(seconds):
    t = round(seconds, 1)
    return f"{int(t // 60)}:{t % 60:04.1f}"


def length_line(seconds):
    where = "under" if seconds < FILM_MIN else "over" if seconds > FILM_MAX else "inside"
    return f"length {minutes(seconds)}, {where} the film's five to seven minutes"

# ---------------------------------------------------------------- the check

def check_narration(data, folder, script):
    """Every way a narration file departs from narration-schema.md, as lines."""
    if not isinstance(data, dict) or set(data) != NARRATION_KEYS:
        return ["the file does not hold exactly the fields of the form"]
    errors = []
    source, rate, length = data["source"], data["sample_rate"], data["length_s"]
    if data["schema"] != SCHEMA:
        errors.append(f"schema is {data['schema']!r}, not {SCHEMA!r}")
    if source not in SOURCES:
        return errors + [f"source is {source!r}, not one of {', '.join(SOURCES)}"]
    if data["script_sha256"] != script["sha256"]:
        errors.append("script_sha256 is not the current script.md's: this narration times other words")
    if not isinstance(rate, int) or rate <= 0 or not isinstance(length, (int, float)):
        return errors + ["sample_rate is a whole number of samples a second, and length_s a number"]
    if source == "estimate":
        if data["pieces"] or data["segments"] or data["service"] is not None or data["draft"] is not None:
            errors.append("an estimate has no pieces, segments, service or draft")
    elif source == "own files":
        if data["segments"] or data["service"] is not None:
            errors.append("the owner's own files have no segments and no service")
    elif not data["segments"] or not isinstance(data["service"], dict):
        errors.append("a narration from the service names its segments and the service")
    allowed = None
    for seg in data["segments"]:
        taken = seg.get("accepted")
        if not isinstance(taken, list):
            errors.append(f"segment {seg.get('id')}: accepted is a list of flag codes")
        elif taken:
            if allowed is None:
                allowed = set((read_json(folder / SETTINGS) or {}).get("accept") or [])
            if seg.get("verdict") != "fail" or set(taken) != fail_codes(seg.get("flags")):
                errors.append(f"segment {seg.get('id')}: accepted names other codes than its take's failures")
            elif not set(taken) <= allowed:
                errors.append(f"segment {seg.get('id')}: the film's settings no longer accept "
                              + ", ".join(sorted(set(taken) - allowed)))
        elif seg.get("verdict") == "fail":
            errors.append(f"segment {seg.get('id')}: the service's QA failed its take")

    passages = data["passages"]
    if [p.get("id") for p in passages] != [p["id"] for p in script["passages"]]:
        return errors + ["the passages are not the script's, in its order"]
    timed, previous_to = source != "own files", None
    for p, q in zip(passages, script["passages"]):
        where = f"passage {p['id']}"
        if [s.get("text") for s in p["sentences"]] != q["say"]:
            errors.append(f"{where}: its sentences are not the script's, braces off")
        if [s.get("place") for s in p["sentences"]] != list(range(1, len(p["sentences"]) + 1)):
            errors.append(f"{where}: its sentences are not numbered from 1 in order")
        if abs((p.get("hold_s") or 0) - q["hold"]) > EPS:
            errors.append(f"{where}: hold_s is not the script's hold")
        if not q["say"]:
            if p["speech_from_s"] is not None or p["speech_to_s"] is not None:
                errors.append(f"{where}: a passage with no Say: has no speech")
            if p["to_s"] - p["from_s"] < p["hold_s"] - EPS:
                errors.append(f"{where}: its hold falls outside its span")
            continue
        for s in p["sentences"]:
            f, t = s.get("from_s"), s.get("to_s")
            if not timed:
                if f is not None or t is not None:
                    errors.append(f"{where}, sentence {s['place']}: the owner's files time no sentence")
                continue
            if f is None or t is None:
                errors.append(f"{where}, sentence {s['place']}: no time")
                continue
            if f >= t:
                errors.append(f"{where}, sentence {s['place']}: it ends before it starts")
            if previous_to is not None and f < previous_to - EPS:
                errors.append(f"{where}, sentence {s['place']}: it starts before the sentence before it ends")
            previous_to = t
        if p["speech_from_s"] is None or p["speech_to_s"] is None:
            errors.append(f"{where}: no speech")
            continue
        if timed and all(s.get("from_s") is not None for s in p["sentences"]) and (
                abs(p["speech_from_s"] - p["sentences"][0]["from_s"]) > EPS
                or abs(p["speech_to_s"] - p["sentences"][-1]["to_s"]) > EPS):
            errors.append(f"{where}: its speech is not its first sentence's start to its last one's end")
        if p["speech_from_s"] < p["from_s"] - EPS or p["speech_to_s"] + p["hold_s"] > p["to_s"] + EPS:
            errors.append(f"{where}: its words or its hold fall outside its span")

    if passages and abs(passages[0]["from_s"]) > EPS:
        errors.append("the first span does not start at 0")
    for a, b in zip(passages, passages[1:]):
        if abs(a["to_s"] - b["from_s"]) > EPS:
            errors.append(f"passages {a['id']} and {b['id']}: their spans do not meet")
    if passages and abs(passages[-1]["to_s"] - length) > EPS:
        errors.append("the last span does not end at the narration's length")
    for p in passages:
        if p["to_s"] < p["from_s"] - EPS:
            errors.append(f"passage {p['id']}: its span ends before it starts")

    if source == "service":
        spoken = [(i, p) for i, p in enumerate(passages) if p["sentences"]]
        for (i, a), (j, b) in zip(spoken, spoken[1:]):
            quiet_between = any(not passages[k]["sentences"] for k in range(i + 1, j))
            if (a["hold_s"] > 0 or quiet_between) and a["sentences"][-1]["piece"] == b["sentences"][0]["piece"]:
                errors.append(f"passage {a['id']} shares a piece with passage {b['id']}, so the pause it "
                              "needs cannot be made")

    named = {s.get("piece") for p in passages for s in p["sentences"]} - {None}
    if named - {piece.get("id") for piece in data["pieces"]}:
        errors.append("a sentence names a piece the file does not hold")
    end, cursor = round(length * rate), 0
    for piece in sorted(data["pieces"], key=lambda x: x.get("at_sample") or 0):
        where = f"piece {piece.get('id')}"
        f = piece.get("file") or {}
        path = folder / str(f.get("path", ""))
        try:
            channels, width, prate, raw = read_wav(path)
        except (OSError, ValueError) as exc:
            errors.append(f"{where}: cannot read {path} ({exc})")
            continue
        frames = len(raw) // (channels * width)
        if prate != rate:
            errors.append(f"{where}: its sample rate is {prate}, not the narration's {rate}")
        if frames != f.get("samples") or sha256_of(path) != f.get("sha256"):
            errors.append(f"{where}: its samples or sha256 differ from its file on disk")
        at = piece.get("at_sample")
        if not isinstance(at, int) or at < cursor:
            errors.append(f"{where}: it starts before 0 or overlaps the piece before it")
            continue
        if abs((piece.get("at_s") or 0) - at / rate) > EPS:
            errors.append(f"{where}: at_s is not at_sample in seconds")
        cursor = at + frames
    if cursor > end:
        errors.append("the last piece runs past the narration's length")
    return errors

# ---------------------------------------------------------------- request and machine

def segments_of(sentences, splits, limit):
    """Paragraphs, split after each passage in splits, then cut to the voice's limit: at the last passage
    end that fits, or inside a passage at the last sentence end that fits."""
    chars = lambda group: len(" ".join(s["text"] for s in group))
    groups, current = [], []
    for s in sentences:
        if current and (s["paragraph"] != current[-1]["paragraph"]
                        or (current[-1]["passage"] in splits and s["passage"] != current[-1]["passage"])):
            groups.append(current)
            current = []
        current.append(s)
    if current:
        groups.append(current)
    segments, problems = [], []
    for group in groups:
        while group:
            if chars(group) <= limit:
                segments.append(group)
                break
            ends = [i for i in range(1, len(group))
                    if group[i]["passage"] != group[i - 1]["passage"] and chars(group[:i]) <= limit]
            if not ends:
                ends = [i for i in range(1, len(group)) if chars(group[:i]) <= limit]
            if not ends:
                problems.append(f"passage {group[0]['passage']}, sentence {group[0]['place']}: "
                                f"{chars(group[:1])} characters, over the voice's {limit}; only the prose "
                                "step can shorten it")
                segments.append(group[:1])
                group = group[1:]
                continue
            segments.append(group[:max(ends)])
            group = group[max(ends):]
    return segments, problems


def jobs_of(planned):
    # One job a beat, cut into even jobs of at most JOB_SEGMENTS segments. Sets each segment's job and
    # returns [(job name, its segments)].
    beats = {}
    for seg in planned:
        beats.setdefault(seg["sentences"][0]["passage"].split(".")[0], []).append(seg)
    jobs = []
    for beat, segs in beats.items():
        count = 1
        while len(segs) > count * JOB_SEGMENTS:
            count += 1
        size = next(k for k in range(1, len(segs) + 1) if k * count >= len(segs))
        for k in range(count):
            name = f"b{beat}" + (f"-{k + 1}" if count > 1 else "")
            part = segs[k * size:(k + 1) * size]
            for seg in part:
                seg["job"] = name
            jobs.append((name, part))
    return jobs


def names_in(script):
    # The names in the words: every word with a capital inside a sentence, and every name given between
    # double braces, each without a possessive s.
    found = set(script["given"])
    for s in script["sentences"]:
        for w in re.findall(r"(?<=\s)[A-Z][\w\u0027\u2019-]*", s["text"]):
            w = re.sub(r"[\u0027\u2019]s$", "", w).rstrip("\u0027\u2019-")
            if w and not re.fullmatch(r"I(?:[\u0027\u2019]\w+)?", w):
                found.add(w)
    return sorted(found)


def occurs(term, text):
    return re.search(r"(?<!\w)" + re.escape(term) + r"(?!\w)", text) is not None


def cmd_request(folder, n):
    notes = []
    script = load_script(folder)
    settings, hints = film_settings(folder, notes)
    voice = canon_voice(settings)
    spoken = {p["id"] for p in script["passages"] if p["say"]}

    last_plan, highest, splits, again = None, {}, set(), set()
    for m in range(1, n):
        plan = read_json(folder / f"narration-{m}-plan.json")
        same = bool(plan) and plan["script_sha256"] == script["sha256"]
        if plan:
            last_plan = plan
            for seg in plan["segments"]:
                highest[seg["id"]] = max(highest.get(seg["id"], -1), *seg["attempts"])
        if same:
            splits |= set(plan.get("splits", []))
        for path in sorted(folder.glob(f"narration-{m}-takes-*.json")):
            for seg in (read_json(path) or {}).get("segments", []):
                for a in seg.get("attempts") or []:
                    highest[seg["segment_id"]] = max(highest.get(seg["segment_id"], -1), a)
        if m == n - 1 and same:
            follow = read_json(folder / f"narration-{m}-next.json") or {}
            splits |= set(follow.get("split_after", []))
            again |= set(follow.get("new_attempts", []))
    redo_path = folder / f"narration-{n}-redo.txt"
    redo = [line.strip() for line in redo_path.read_text(encoding="utf-8").splitlines()
            if line.strip()] if redo_path.is_file() else []
    unknown = [r for r in redo if r not in spoken]
    if unknown:
        raise Refused(f"{redo_path.name} names passages that have no words: {', '.join(unknown)}")

    segments, problems = segments_of(script["sentences"], splits, voice["max_segment_chars"])
    previous = {seg["id"]: seg for seg in (last_plan or {}).get("segments", [])}
    planned = []
    for group in segments:
        first = group[0]
        sid = f"s{first['passage']}.{first['place']}"
        if not SEGMENT_ID.fullmatch(sid):
            problems.append(f"{sid} is not a segment id the service takes")
        shape = [(s["passage"], s["place"], s["text"]) for s in group]
        before = previous.get(sid)
        same = before is not None and [(s["passage"], s["place"], s["text"]) for s in before["sentences"]] == shape
        if sid in again or any(s["passage"] in redo for s in group):
            attempt = highest.get(sid, -1) + 1
        elif same:
            attempt = before["attempt"]
        else:
            attempt = 0
        attempts = list(range(attempt, attempt + settings["takes"]))
        if attempts[-1] > MAX_ATTEMPT:
            problems.append(f"segment {sid} has used every attempt the service allows")
        if len(group) > MAX_CUES:
            problems.append(f"segment {sid} holds {len(group)} sentences; the service takes {MAX_CUES}")
        for s in group:
            if len(s["text"]) > MAX_CUE_CHARS:
                problems.append(f"passage {s['passage']}, sentence {s['place']}: over the service's "
                                f"{MAX_CUE_CHARS} characters a cue")
        planned.append({"id": sid, "job": None, "attempt": attempt, "attempts": attempts,
                        "sentences": [{"passage": s["passage"], "place": s["place"], "text": s["text"]}
                                      for s in group]})
    jobs = jobs_of(planned)
    terms = {h["term"] for h in hints}
    names = names_in(script)
    requests, sent_hints = [], set()
    for name, segs in jobs:
        text = " ".join(s["text"] for seg in segs for s in seg["sentences"])
        chars = sum(len(s["text"]) for seg in segs for s in seg["sentences"])
        used = [h for h in hints if occurs(h["term"], text)]
        used += [{"term": w} for w in names if w not in terms and occurs(w, text)]
        sent_hints |= {h["term"] for h in used}
        if len(segs) > MAX_SEGMENTS:
            problems.append(f"job {name}: {len(segs)} segments; the service takes {MAX_SEGMENTS} a job")
        if chars > MAX_JOB_CHARS:
            problems.append(f"job {name}: {chars} characters; the service takes {MAX_JOB_CHARS} a job")
        if len(used) > MAX_HINTS:
            problems.append(f"job {name}: {len(used)} hints; the service takes {MAX_HINTS} a job")
        requests.append({"name": name, "arguments": {
            "voice": {"path": voice["path"], "sha256": voice["sha256"], "transcript": voice["transcript"]},
            "expect_engine_profile": voice["engine_profile_hash"],
            "hints": used,
            "segments": [{"segment_id": seg["id"], "cues": [{"text": s["text"]} for s in seg["sentences"]],
                          "attempts": seg["attempts"]} for seg in segs],
            "options": {"takes": settings["takes"], "max_retakes": settings["max_retakes"],
                        "idempotency_key": f"{folder.name}-n{n}-{name}"[-64:]},
        }})
    total = sum(len(s["text"]) for s in script["sentences"])
    if problems:
        raise Refused(*problems)

    write_json(folder / f"narration-{n}-request.json", {"jobs": requests})
    write_json(folder / f"narration-{n}-plan.json", {
        "draft": n, "script_sha256": script["sha256"], "splits": sorted(splits), "settings": settings,
        "jobs": [name for name, _ in jobs], "segments": planned})

    size = lambda seg: len(" ".join(s["text"] for s in seg["sentences"]))
    longest = max(planned, key=size)
    lines = [f"draft {n}: {len(jobs)} jobs, {len(planned)} segments, {len(script['sentences'])} sentences, {total} characters",
             f"longest segment {longest['id']}: {size(longest)} characters of the voice's "
             f"{voice['max_segment_chars']}"] + notes
    new = [seg["id"] for seg in planned
           if seg["attempt"] > 0 and (previous.get(seg["id"]) or {}).get("attempt") != seg["attempt"]]
    if new:
        lines.append("new attempts: " + ", ".join(new))
    if splits:
        lines.append("split after passages: " + ", ".join(sorted(splits)))
    if sent_hints:
        lines.append("hints sent: " + ", ".join(sorted(sent_hints)))
    return "NARRATION REQUEST: READY", lines


def cmd_machine():
    try:
        shell = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             "Get-CimInstance Win32_Process | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress"],
            capture_output=True, timeout=120)
        found = json.loads(shell.stdout.decode("utf-8", "replace") or "null")
        processes = found if isinstance(found, list) else [found] if found else []
        if shell.returncode != 0 or not processes:
            raise OSError("PowerShell listed no processes")
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        return "MACHINE: BUSY", [f"the processes could not be read ({exc}), so the machine is taken as busy"]
    reasons = []
    by_pid = {p.get("ProcessId"): p for p in processes}
    for p in processes:
        name, line = (p.get("Name") or "").lower(), p.get("CommandLine") or ""
        if name == "evosim.farm.exe":
            reasons.append(f"a farm run, process {p['ProcessId']}")
        elif name == "unity.exe" and "-batchmode" in line.lower():
            reasons.append(f"a batchmode Unity, process {p['ProcessId']}")
        elif name == "ffmpeg.exe":
            reasons.append(f"ffmpeg, process {p['ProcessId']}")
    try:
        # nvidia-smi's table types every process: under Windows every desktop program that draws is C+G, and
        # only a pure compute job, such as a CUDA farm run or the voice's own worker, is C.
        gpu = subprocess.run(["nvidia-smi"], capture_output=True, timeout=60)
        if gpu.returncode != 0:
            raise OSError(gpu.stderr.decode("utf-8", "replace").strip() or "nvidia-smi failed")
        for row in gpu.stdout.decode("utf-8", "replace").splitlines():
            m = re.match(r"\|\s+\d+\s+\S+\s+\S+\s+(\d+)\s+(C\+G|C|G)\s+(.*?)\s+\S+\s*\|$", row)
            if m and m.group(2) == "C":
                pid = int(m.group(1))
                line = (by_pid.get(pid) or {}).get("CommandLine") or ""
                if "narration.daemon" not in line and "narration_worker" not in line:
                    reasons.append(f"a compute process on the GPU, process {pid}: "
                                   f"{(by_pid.get(pid) or {}).get('Name') or m.group(3)}")
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        reasons.append(f"the GPU's processes could not be read ({exc})")
    if reasons:
        return "MACHINE: BUSY", reasons
    return "MACHINE: FREE", [f"{len(processes)} processes read, none that the voice must wait for"]

# ---------------------------------------------------------------- collect, own and estimate

def cmd_collect(folder, n):
    script = load_script(folder)
    plan = read_json(folder / f"narration-{n}-plan.json")
    if plan is None:
        raise Refused(f"draft {n} has no plan: narration-{n}-plan.json")
    if plan["script_sha256"] != script["sha256"]:
        raise Refused(f"script.md changed after draft {n}'s request; the draft needs a new request")
    takes, missing, differ = {"jobs": [], "segments": [], "warnings": [], "listen_first": []}, [], set()
    for job in plan["jobs"]:
        part = read_json(folder / f"narration-{n}-takes-{job}.json")
        if part is None:
            missing.append(f"narration-{n}-takes-{job}.json")
            continue
        takes["jobs"].append({"name": job, "job_id": part.get("job_id"), "outcome": part.get("outcome")})
        for key in ("segments", "warnings", "listen_first"):
            takes[key] += part.get(key) or []
        for key in ("voice_hash", "engine_profile", "measured_error"):
            if key not in takes:
                takes[key] = part.get(key)
            elif takes[key] != part.get(key):
                differ.add(key)
    if missing:
        raise Refused("the narrator wrote no takes for these jobs: " + ", ".join(missing))
    if differ:
        raise Refused("the jobs differ in " + ", ".join(sorted(differ)) + ", so one voice under one engine "
                      "did not read them all")
    settings, passages = plan["settings"], script["passages"]
    # The owner may rule on a flag after hearing a draft, so the codes they accept are read as they stand now.
    accept = film_settings(folder, [])[0]["accept"]
    refusals, again, owner = [], set(), []
    by_id = {seg.get("segment_id"): seg for seg in takes.get("segments", [])}
    planned = {seg["id"] for seg in plan["segments"]}
    for sid in sorted(planned ^ set(by_id)):
        refusals.append(f"segment {sid} is in {'the plan' if sid in planned else 'the takes'} only")
    read, fmt = {}, None
    for seg in plan["segments"]:
        if seg["id"] not in by_id:
            continue
        take = by_id[seg["id"]].get("take")
        if take is None:
            refusals.append(f"segment {seg['id']}: no take")
            again.add(seg["id"])
            continue
        cues = take.get("cues") or []
        if [c.get("received") for c in cues] != [s["text"] for s in seg["sentences"]]:
            refusals.append(f"segment {seg['id']}: its cues are not the plan's sentences")
            continue
        unplaced = False
        for c, s in zip(cues, seg["sentences"]):
            if c.get("start_s") is None or c.get("end_s") is None:
                unplaced = True
                if any(f.get("code") == "CUE_UNALIGNED" and f.get("cue") == c.get("index")
                       and f.get("reason") == "no_alignable_words" for f in take.get("flags") or []):
                    owner.append(f"passage {s['passage']}, sentence {s['place']}: the service can never place "
                                 "it (no_alignable_words); only new words can")
                else:
                    again.add(seg["id"])
                refusals.append(f"segment {seg['id']}: passage {s['passage']}, sentence {s['place']} is unplaced")
        if unplaced:
            continue
        path = Path(take.get("path", ""))
        try:
            channels, width, rate, raw = read_wav(path)
        except (OSError, ValueError) as exc:
            refusals.append(f"segment {seg['id']}: cannot read its take ({exc})")
            continue
        frames = len(raw) // (channels * width)
        if sha256_of(path) != take.get("sha256") or frames != take.get("samples") or rate != take.get("sample_rate"):
            refusals.append(f"segment {seg['id']}: its take on disk does not match its sha256, samples or rate")
            continue
        if fmt not in (None, (channels, width, rate)):
            refusals.append(f"segment {seg['id']}: its take's channels, width or rate differ from the others'")
            continue
        fmt = (channels, width, rate)
        read[seg["id"]] = (take, raw)
        if take.get("verdict") == "fail" and not accepted(take, accept):
            again.add(seg["id"])
    if refusals:
        write_json(folder / f"narration-{n}-next.json", {"new_attempts": sorted(again), "split_after": []})
        lines = refusals + owner
        if again:
            lines.append("for the next draft, new attempts: " + ", ".join(sorted(again)))
        return "NARRATION CHECK: REFUSED", lines

    channels, width, rate = fmt
    frame, cut = channels * width, settings["cut"]
    # Until the service has measured its alignment error, the thresholds of its aligners are placeholders, and
    # their disagreement alone does not fail a cut.
    measured = takes.get("measured_error") or {}
    benchmarked = isinstance(measured, dict) and measured.get("p50_s") is not None
    pieces, made, failed, split_after = [], 0, [], set()
    for seg in plan["segments"]:
        take, raw = read[seg["id"]]
        cues, sentences = take["cues"], seg["sentences"]
        places = [(round(c["start_s"] * rate), round(c["end_s"] * rate)) for c in cues]
        levels, lframe = frame_levels(channels, width, rate, raw)
        edge = round(cut["edge_s"] * rate)
        cuts = []
        for i in range(1, len(cues)):
            before, after = sentences[i - 1], sentences[i]
            if before["passage"] == after["passage"]:
                continue
            why = []
            codes = {f.get("code") for f in take.get("flags") or []
                     if f.get("cue") == cues[i - 1].get("index") and f.get("next_cue") == cues[i].get("index")}
            if "CUE_BOUNDARY_NO_PAUSE" in codes:
                why.append("the service found no pause there")
            if "CUE_ALIGNMENT_DISAGREE" in codes and benchmarked:
                why.append("the service's two aligners disagree on this boundary")
            gap = cues[i]["start_s"] - cues[i - 1]["end_s"]
            if gap < cut["min_pause_s"]:
                why.append(f"the pause is {gap:.2f} s, under {cut['min_pause_s']} s")
            if not why and not silent(levels, lframe, places[i - 1][1], places[i][0]):
                why.append("the samples there are not silent")
            if why:
                failed.append(f"after passage {before['passage']}: " + "; ".join(why))
                if needs_silence(passages, before["passage"]):
                    split_after.add(before["passage"])
                continue
            keep = min(edge, (places[i][0] - places[i - 1][1]) // 2)
            cuts.append((i, places[i - 1][1] + keep, places[i][0] - keep))
        made += len(cuts)
        starts = [(0, max(0, places[0][0] - edge))] + [(i, b) for i, _, b in cuts]
        stops = [a for _, a, _ in cuts] + [min(len(raw) // frame, places[-1][1] + edge)]
        nexts = [i for i, _, _ in cuts] + [len(cues)]
        for (first, start), stop, nxt in zip(starts, stops, nexts):
            head = sentences[first]
            pieces.append({
                "id": f"p{head['passage']}" + (f".{head['place']}" if head["place"] > 1 else ""),
                "segment": seg["id"], "from": start, "to": stop, "raw": raw[start * frame:stop * frame],
                "words": [(sentences[k]["passage"], sentences[k]["place"], places[k][0] - start,
                           places[k][1] - start) for k in range(first, nxt)]})

    service = {"job_ids": [j["job_id"] for j in takes["jobs"]], "voice_hash": takes.get("voice_hash"),
               "clip_sha256": (settings.get("voice") or {}).get("sha256"),
               "engine_profile": takes.get("engine_profile"), "measured_error": takes.get("measured_error")}
    segments = []
    for seg in plan["segments"]:
        take = read[seg["id"]][0]
        segments.append({"id": seg["id"], "take_id": take.get("take_id"), "analysis_id": take.get("analysis_id"),
                         "attempt": take.get("attempt"), "take_sha256": take.get("sha256"),
                         "take_samples": take.get("samples"), "verdict": take.get("verdict"),
                         "accepted": accepted(take, accept),
                         "flags": take.get("flags") or [], "loudness": take.get("loudness")})
    lines = [f"{made} cuts made in {len(plan['segments'])} segments, {len(failed)} failed"]
    lines += [f"cut failed {line}" for line in failed]
    lines += [f"accepted: segment {seg['id']}'s take failed QA on {', '.join(seg['accepted'])}, which the film's "
              "settings accept" for seg in segments if seg["accepted"]]
    lines += [f"warning: {w.get('code')} in segment {w.get('segment_id')}, cue {w.get('cue')}"
              for w in takes.get("warnings") or []]
    lines += [f"warning: {f.get('code')} in segment {seg['id']}, cue {f.get('cue')}"
              for seg in segments for f in seg["flags"] if f.get("severity") == "warn"]
    lines += [f"hear first: segment {x.get('segment_id')}, cue {x.get('cue')}: {x.get('reason')} "
              f"({x.get('from_s')} to {x.get('to_s')} s into its take)" for x in takes.get("listen_first") or []]
    return finish(folder, n, "service", fmt, script, settings, pieces, segments, service, lines, again, split_after)


def finish(folder, n, source, fmt, script, settings, pieces, segments, service, lines, again, split_after):
    """Write draft n's pieces, its timeline and its joined file; check them; say what the next draft needs."""
    channels, width, rate = fmt
    out = folder / f"narration-{n}"
    fresh_dir(out)
    files = {}
    for piece in pieces:
        path = out / f"{piece['id']}.wav"
        if "source_path" in piece:
            shutil.copyfile(piece["source_path"], path)
        else:
            write_wav(path, channels, width, rate, piece["raw"])
        files[piece["id"]] = {"path": f"narration-{n}/{path.name}", "sha256": sha256_of(path),
                              "samples": len(piece["raw"]) // (channels * width)}
    data = timeline(source, n, rate, script, settings, pieces, segments, service, files)
    write_json(folder / f"narration-{n}.json", data)
    write_draft(folder, n, data, pieces, fmt)
    errors = check_narration(data, folder, script)
    if source == "service":
        write_json(folder / f"narration-{n}-next.json",
                   {"new_attempts": sorted(again), "split_after": sorted(split_after)})
    head = [f"draft {n}: {len(pieces)} pieces, " + length_line(data["length_s"]),
            f"listen to {folder / f'narration-{n}.wav'}"]
    if again:
        lines.append("for the next draft, new attempts: " + ", ".join(sorted(again)))
    if split_after:
        lines.append("for the next draft, split after passages: " + ", ".join(sorted(split_after)))
    verdict = "NARRATION CHECK: FAIL" if errors or again or split_after else "NARRATION CHECK: PASS"
    return verdict, head + lines + [f"ERROR {e}" for e in errors]


def cmd_own(folder, n):
    notes = []
    script = load_script(folder)
    settings, _ = film_settings(folder, notes)
    own = folder / "narration-own"
    wanted = {f"{p['id']}.wav": p for p in script["passages"] if p["say"]}
    present = {f.name for f in own.iterdir()} if own.is_dir() else set()
    problems = [f"missing: narration-own/{name}" for name in sorted(set(wanted) - present)]
    problems += [f"not a passage's file: narration-own/{name}" for name in sorted(present - set(wanted))]
    pieces, fmt = [], None
    for name, p in wanted.items():
        if name not in present:
            continue
        try:
            channels, width, rate, raw = read_wav(own / name)
        except (OSError, ValueError) as exc:
            problems.append(f"cannot read narration-own/{name}: {exc}")
            continue
        if fmt not in (None, (channels, width, rate)):
            problems.append(f"narration-own/{name}: its channels, sample width or rate differ from the others'")
            continue
        fmt = (channels, width, rate)
        frames = len(raw) // (channels * width)
        if frames == 0:
            problems.append(f"narration-own/{name} holds no sound")
            continue
        pieces.append({"id": f"p{p['id']}", "words": [(p["id"], 1, 0, frames)], "raw": raw,
                       "source_path": own / name})
    if problems:
        raise Refused(*problems)
    return finish(folder, n, "own files", fmt, script, settings, pieces, [], None, notes, set(), set())


def cmd_estimate(folder):
    notes = []
    script = load_script(folder)
    settings, _ = film_settings(folder, notes)
    target = folder / "narration.json"
    there = read_json(target)
    if there is not None and there.get("source") != "estimate":
        raise Refused("narration.json already holds a narration that is not an estimate; the owner decides "
                      "what becomes of it")
    rate = ESTIMATE_RATE
    within = round(settings["padding"]["within_s"] * rate)
    pieces = []
    for p in script["passages"]:
        if p["say"]:
            words, t = [], 0
            for place, text in enumerate(p["say"], 1):
                d = round(sc.word_count(text) / sc.PACE * 60 * rate)
                words.append((p["id"], place, t, t + d))
                t += d + within
            pieces.append({"id": f"p{p['id']}", "words": words})
    data = timeline("estimate", None, rate, script, settings, pieces, [], None, {})
    errors = check_narration(data, folder, script)
    if errors:
        return "NARRATION ESTIMATE: REFUSED", [f"ERROR {e}" for e in errors]
    write_json(target, data)
    return "NARRATION ESTIMATE: WRITTEN", [f"wrote {target}: an estimate at {sc.PACE} words a minute, "
                                           "with no sound", length_line(data["length_s"])] + notes


# ---------------------------------------------------------------- check, sheet and keep

def cmd_check(path):
    data = read_json(path)
    if data is None:
        raise Refused(f"cannot read {path}")
    errors = check_narration(data, path.parent, load_script(path.parent))
    lines = [f"{path.name}: {data.get('source')}, " + length_line(data.get("length_s") or 0)]
    return ("NARRATION CHECK: FAIL" if errors else "NARRATION CHECK: PASS"), lines + [f"ERROR {e}" for e in errors]


def cmd_sheet(path):
    data = read_json(path)
    if data is None:
        raise Refused(f"cannot read {path}")
    lines = []
    for p in data["passages"]:
        words = (p["sentences"][0]["text"] if p["sentences"] else "(no words: a hold)").split()
        lines.append(f"{p['id']:>6}  {minutes(p['from_s']):>7}  {p['to_s'] - p['from_s']:5.1f} s  "
                     + " ".join(words[:8]) + (" ..." if len(words) > 8 else ""))
    return f"NARRATION SHEET: {path.name}, {data['source']}, {minutes(data['length_s'])}", lines


def cmd_keep(folder, n):
    data = read_json(folder / f"narration-{n}.json")
    if data is None:
        raise Refused(f"cannot read narration-{n}.json")
    script = load_script(folder)
    errors = check_narration(data, folder, script)
    if errors:
        raise Refused(f"draft {n} does not pass its check:", *(f"ERROR {e}" for e in errors))
    there = read_json(folder / "narration.json")
    if there is not None and there.get("source") != "estimate":
        raise Refused("narration.json already holds a narration; the owner decides what becomes of it")
    target = folder / "narration"
    if target.exists() and any(target.iterdir()):
        raise Refused("narration/ already holds files; the owner decides what becomes of them")
    target.mkdir(exist_ok=True)
    for piece in data["pieces"]:
        source = folder / piece["file"]["path"]
        shutil.copyfile(source, target / source.name)
        if sha256_of(target / source.name) != piece["file"]["sha256"]:
            raise Refused(f"the copy of {source.name} does not match its sha256")
        piece["file"]["path"] = f"narration/{source.name}"
    errors = check_narration(data, folder, script)
    if errors:
        raise Refused("the kept narration does not pass its check:", *(f"ERROR {e}" for e in errors))
    write_json(folder / "narration.json", data)
    return "NARRATION KEEP: DONE", [f"copied {len(data['pieces'])} pieces into {target}",
                                    f"wrote {folder / 'narration.json'}, " + length_line(data["length_s"])]


COMMANDS = {  # name: (function, its refusal's first line, its arguments: 0, a path, or a folder and a draft)
    "request": (cmd_request, "NARRATION REQUEST: REFUSED", 2),
    "machine": (cmd_machine, "MACHINE: BUSY", 0),
    "collect": (cmd_collect, "NARRATION CHECK: REFUSED", 2),
    "own": (cmd_own, "NARRATION CHECK: REFUSED", 2),
    "estimate": (cmd_estimate, "NARRATION ESTIMATE: REFUSED", 1),
    "check": (cmd_check, "NARRATION CHECK: FAIL", 1),
    "sheet": (cmd_sheet, "NARRATION SHEET: REFUSED", 1),
    "keep": (cmd_keep, "NARRATION KEEP: REFUSED", 2),
}
PASSING = ("READY", "FREE", "PASS", "WRITTEN", "DONE")


def main(argv):
    started = time.monotonic()
    if len(argv) < 2 or argv[1] not in COMMANDS or len(argv) != 2 + COMMANDS[argv[1]][2] \
            or (COMMANDS[argv[1]][2] == 2 and not (argv[3].isdigit() and int(argv[3]) >= 1)):
        print(__doc__.strip(), file=sys.stderr)
        return 2
    command, refused, count = COMMANDS[argv[1]]
    args = ([Path(argv[2])] if count else []) + ([int(argv[3])] if count == 2 else [])
    if count and argv[1] not in ("check", "sheet") and not args[0].is_dir():
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
    passed = verdict.endswith(PASSING) or (argv[1] == "sheet" and verdict != refused)
    return 0 if passed else 1


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))