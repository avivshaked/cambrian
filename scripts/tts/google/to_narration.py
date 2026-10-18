"""Turn one Google voice's beat files and their alignments into a narration draft of a story film.

  <python> -B scripts/tts/google/to_narration.py FILM_FOLDER N BEATS_DIR VOICE

BEATS_DIR holds narrate.py's <VOICE>-beat-<B>.wav and align_beats.py's <VOICE>-beat-<B>.align.json. Each beat
is taken as one take of the narration step (create-story-video's narration.py): its sentences are the script's
sentences of that beat, each timed by the aligner's words. The step's own code then cuts the takes at every
passage boundary with a clean pause, lays the pieces on the timeline with the film's padding and holds, checks
the result against narration-schema.md, and writes draft N into the film's folder: narration-N.json,
narration-N/ and narration-N.wav, beside narration-N-plan.json and narration-N-takes-google.json, which record
what went in.

The source is "service", with Google as the service: no QA ran, so every segment's verdict is "unchecked", and
words the aligner scored under 0.5 are warnings to hear first. The voice's id is not written; its hash is.
Sends nothing and uses the standard library only.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
LOW_SCORE = 0.5


def load_narration():
    spec = importlib.util.spec_from_file_location("narration", SKILL / "narration.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def words(text: str) -> list[str]:
    # The aligner's own count (scripts/tts/align.py): tags dropped, a word counts when it holds a letter.
    text = re.sub(r"<[^>]*>", " ", text).replace("’", "'").replace("‘", "'")
    return [w for w in text.split() if re.sub(r"[^A-Z']", "", w.upper()).strip("'")]


def snap_to_silence(nar, cues, sentences, channels, width, rate, raw, min_frames=3):
    """Move each passage boundary's times into the quiet stretch of its pause.

    The aligner ends a word before its fading tail, and the voice often breathes before the next sentence, so
    the whole gap between two aligned words is seldom silent by the step's rule (narration.py's silent: every
    20 ms frame 35 dB under the take's 95th-percentile frame). The longest run of such frames in the gap, of at
    least min_frames, becomes the boundary: the sentence before ends where it starts, so it keeps its tail and
    any breath after it, and the next starts where it ends, so it keeps the breath before it. A gap with no
    such run is left alone, and the step's cut there fails as it should."""
    levels, frame = nar.frame_levels(channels, width, rate, raw)
    top = sorted(levels)[int(0.95 * (len(levels) - 1))]
    for i in range(1, len(cues)):
        if sentences[i - 1]["passage"] == sentences[i]["passage"]:
            continue
        lo = -(-round(cues[i - 1]["end_s"] * rate) // frame)
        hi = round(cues[i]["start_s"] * rate) // frame
        best, run_start = (0, None), None
        for k in range(lo, hi + 1):
            quiet = k < hi and k < len(levels) and levels[k] < top - nar.QUIET_DB
            if quiet and run_start is None:
                run_start = k
            elif not quiet and run_start is not None:
                if k - run_start > best[0]:
                    best = (k - run_start, run_start)
                run_start = None
        length, start = best
        if length >= min_frames:
            cues[i - 1]["end_s"] = round(start * frame / rate, 3)
            cues[i]["start_s"] = round((start + length) * frame / rate, 3)


def main() -> int:
    if len(sys.argv) != 5:
        sys.exit(__doc__)
    folder, n, beats_dir, voice = Path(sys.argv[1]), int(sys.argv[2]), Path(sys.argv[3]), sys.argv[4]
    nar = load_narration()
    if (folder / f"narration-{n}.json").exists() or (folder / f"narration-{n}-plan.json").exists():
        sys.exit(f"draft {n} already exists in {folder}; pick the next number")
    script = nar.load_script(folder)
    settings, _ = nar.film_settings(folder, [])
    index = json.loads((beats_dir / "index.json").read_text(encoding="utf-8"))
    ids_file = Path(__file__).resolve().parent / "voices.local.json"
    voice_id = json.loads(ids_file.read_text(encoding="utf-8"))[voice]

    by_beat: dict[int, list[dict]] = {}
    for s in script["sentences"]:
        by_beat.setdefault(int(s["passage"].split(".")[0]), []).append(s)

    plan_segments, take_segments, listen, problems = [], [], [], []
    for beat in sorted(by_beat):
        sentences = by_beat[beat]
        wav = beats_dir / f"{voice}-beat-{beat}.wav"
        aligned = json.loads((beats_dir / f"{voice}-beat-{beat}.align.json").read_text(encoding="utf-8"))["words"]
        counts = [len(words(s["text"])) for s in sentences]
        if sum(counts) != len(aligned):
            problems.append(f"beat {beat}: the script's sentences hold {sum(counts)} words, the alignment {len(aligned)}")
            continue
        sid = f"s{sentences[0]['passage']}.{sentences[0]['place']}"
        cues, flags, k = [], [], 0
        for i, (s, c) in enumerate(zip(sentences, counts)):
            mine = aligned[k:k + c]
            k += c
            cues.append({"index": i, "received": s["text"], "start_s": mine[0]["start"], "end_s": mine[-1]["end"]})
            for w in mine:
                if w["score"] < LOW_SCORE:
                    flags.append({"code": "ALIGN_LOW_SCORE", "severity": "warn", "cue": i,
                                  "word": w["text"], "score": w["score"]})
                    listen.append({"segment_id": sid, "cue": i, "reason": f"the aligner scored {w['text']!r} "
                                   f"{w['score']}", "from_s": w["start"], "to_s": w["end"]})
        channels, width, rate, raw = nar.read_wav(wav)
        snap_to_silence(nar, cues, sentences, channels, width, rate, raw)
        take_segments.append({"segment_id": sid, "attempts": [0], "take": {
            "take_id": f"google-{voice}-beat-{beat}", "analysis_id": None, "attempt": 0,
            "path": str(wav.resolve()), "sha256": nar.sha256_of(wav), "samples": len(raw) // (channels * width),
            "sample_rate": rate, "cues": cues, "verdict": "unchecked", "flags": flags, "loudness": None}})
        plan_segments.append({"id": sid, "job": "google", "attempt": 0, "attempts": [0],
                              "sentences": [{"passage": s["passage"], "place": s["place"], "text": s["text"]}
                                            for s in sentences]})
    if problems:
        sys.exit("\n".join(problems))

    voice_hash = "sha256:" + hashlib.sha256(voice_id.encode()).hexdigest()
    engine = {"id": index["model"], "hash": "sha256:" + hashlib.sha256(
        (index["model"] + "\n" + index["style"]).encode()).hexdigest()}
    takes = {"job_id": f"google-{voice}", "outcome": "done", "voice_hash": voice_hash, "engine_profile": engine,
             "measured_error": None, "segments": take_segments, "warnings": [], "listen_first": listen}
    plan = {"draft": n, "script_sha256": script["sha256"], "splits": [], "settings": settings, "jobs": ["google"],
            "segments": plan_segments,
            "source": {"service": "Google Gemini TTS", "model": index["model"], "style": index["style"],
                       "voice": voice, "beats": str(beats_dir)}}
    nar.write_json(folder / f"narration-{n}-plan.json", plan)
    nar.write_json(folder / f"narration-{n}-takes-google.json", takes)

    joined = {"jobs": [{"name": "google", "job_id": takes["job_id"], "outcome": "done"}],
              "segments": take_segments, "warnings": [], "listen_first": listen,
              "voice_hash": voice_hash, "engine_profile": engine, "measured_error": None}
    read, fmt, refusals, again, owner = nar.check_takes(plan, joined, settings["accept"])
    if refusals:
        print("NARRATION CHECK: REFUSED")
        print("\n".join(refusals + owner))
        return 1
    # The design sample stands for the clip: a designed voice has no clip of its own.
    samples = sorted((SKILL.parents[2] / "scratch" / "owner").glob(f"google-audition*/{voice}-0-design-sample.wav"))
    clip = nar.sha256_of(samples[0]) if samples else None
    pieces, segments, service, lines, split_after = nar.cut_takes(
        plan, joined, read, fmt, script["passages"], settings["accept"], clip)
    verdict, out = nar.finish(folder, n, "service", fmt, script, settings, pieces, segments, service, lines,
                              again, split_after)
    print(verdict)
    print("\n".join(out))
    return 0 if verdict.endswith("PASS") else 1


if __name__ == "__main__":
    sys.exit(main())
