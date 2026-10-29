"""Google Gemini TTS audition (the owner's go, 2026-10-09): designed British narrators reading the round 57 film.

Four voices are designed with Gemini 3.8 Flash TTS voice design (voices.create, type "prompted", store=True),
each described in the owner's order (accent, register, age, then the rest) in one sentence as Google advises
(ai.google.dev/gemini-api/docs/voice-design). Each voice then reads three passages of the round 57 narration,
one request per passage, so the owner can hear whether one voice holds across requests. A per-line style asks
for the "more lively" the owner wanted; age, timbre and accent stay in the voice, as Google advises.

What this sends to Google: the four descriptions and about 1,900 characters of the film's script, on the account
whose key is in this folder's .env.local (or env.local), both gitignored. The key is read here and passed to the
client; it is never printed or written anywhere.

Run from the project root:
  scratch/tts-audition/envs/gemini/Scripts/python.exe -B scripts/tts/google/audition.py
Writes the clips, the design samples and index.json to scratch/owner/google-audition/, and the voice ids to
scripts/tts/google/voices.local.json (gitignored), so a rerun reuses the voices instead of designing new ones.
"""

from __future__ import annotations

import base64
import hashlib
import io
import json
import sys
import time
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
IDS = HERE / "voices.local.json"
MODEL = "gemini-3.8-flash-tts"

PASSAGES_OPENING = ("Down near the floor of this tank lives a family of over twelve hundred creatures, almost every one "
                    "of them nothing but a stomach. Small tan lumps, soaking up the snow, the dead stuff drifting down. "
                    "All from one creature that dropped in from outside. In our last film, of sixty-three creatures like "
                    "this, one bred. We called it Lucky, and its family lasted two generations.")

# Round 1 read at 96-134 words a minute and the owner found it very slow; F1 came out with a Russian accent. Round 2
# drops "unhurried" for a documentary pace (the round 57 script is timed at about 145 words a minute), drops F1 and
# adds F3, which names the accent as southern English and avoids "contralto".
ROUNDS = {
    1: {"out": "google-audition", "voices": ["M1", "M2", "F1", "F2"],
        "style": "warm and lively, unhurried, with quiet wonder, as if sharing a remarkable discovery"},
    2: {"out": "google-audition-2", "voices": ["M1", "M2", "F2", "F3"],
        "style": "a natural documentary pace, about 150 words a minute, warm and lively, with quiet wonder, "
                 "as if sharing a remarkable discovery"},
    # Round 3: the owner's own description for M3 (2026-10-09), plain "British accent" with no RP wording; F4 is
    # the same description for a woman.
    3: {"out": "google-audition-3", "voices": ["M3", "F4"],
        "style": "a natural documentary pace, about 150 words a minute, warm and lively, with quiet wonder, "
                 "as if sharing a remarkable discovery"},
    # Round 4: the owner liked M3 and F4; each reads the film's beats 1 and 2 (six reads, about 1,200 characters)
    # as ONE request, to compare with separate passages and to judge reading the whole film in one call. The
    # script's 2 s hold after the hook's question becomes Google's <long pause> tag.
    4: {"out": "google-audition-4", "voices": ["M3", "F4"],
        "style": "a natural documentary pace, about 150 words a minute, warm and lively, with quiet wonder, "
                 "as if sharing a remarkable discovery",
        "passages": {"beats-1-2": (
            "Down near the floor of this tank lives a family of over twelve hundred creatures, almost every one of "
            "them nothing but a stomach. Small tan lumps, soaking up the snow, the dead stuff drifting down. All from "
            "one creature that dropped in from outside. In our last film, of sixty-three creatures like this, one "
            "bred. We called it Lucky, and its family lasted two generations.\n\n"
            "Since then we've changed this world eight times, mostly for the eaters. The biggest eater families we "
            "know of held a dozen, then nearly two hundred. This one showed up in an experiment about plants. So what "
            "finally made a family of eaters last? <long pause>\n\n"
            "This is Cambrian, a tank of water we built inside a computer. Every creature grows from its genes, body "
            "and brain together, and what it grows is what it eats. The leaves have it easy, living on the light. The "
            "eaters don't. A stomach gets whatever snow drifts its way, and a mouth eats the dead, if it can reach "
            "them. Without a joint, a body can't bend, or swim.\n\n"
            "Staying alive costs energy every second. Earn more, and you save the extra and have young. Run out, and "
            "you're dead. All the while, newcomers trickle in from outside. And every experiment, a round, runs three "
            "tanks at once.")}},
    # Round 5: the owner asked for one more man and one more woman from the same prompts. voices.create takes no
    # seed, so M3b and F4b are M3's and F4's descriptions designed a second time, reading round 3's passages.
    5: {"out": "google-audition-5", "voices": ["M3b", "F4b"],
        "style": "a natural documentary pace, about 150 words a minute, warm and lively, with quiet wonder, "
                 "as if sharing a remarkable discovery"},
    # Round 6: the owner heard "quiet" pull the voices towards a whisper and wants energy and projection, and
    # added "conversational" so it does not sound theatrical. The four voices read round 3's passages.
    6: {"out": "google-audition-6", "voices": ["M3", "F4", "M3b", "F4b"],
        "style": "projecting, energetic and conversational, like a nature-documentary presenter speaking out to a "
                 "big audience, full of wonder and excitement, at a natural documentary pace of about 150 words a "
                 "minute"},
    # Round 7: round 6 was too much; the owner dropped "excitement" and "big audience".
    7: {"out": "google-audition-7", "voices": ["M3", "F4", "M3b", "F4b"],
        "style": "projecting, energetic and conversational, like a nature-documentary presenter, full of wonder, "
                 "at a natural documentary pace of about 150 words a minute",
        # The owner wants to find the voice first: one passage per voice.
        "passages": {"p1-opening": PASSAGES_OPENING}},
    # Round 8 (2026-10-10): two more designs from each of M3's and F4's descriptions, design samples only; nothing
    # reads the script until the owner has picked a voice.
    8: {"out": "google-audition-8", "voices": ["M3c", "M3d", "F4c", "F4d"], "style": None, "passages": {}},
}
ROUND = int(sys.argv[sys.argv.index("--round") + 1]) if "--round" in sys.argv else 1
OUT = ROOT / "scratch" / "owner" / ROUNDS[ROUND]["out"]
STYLE = ROUNDS[ROUND]["style"]

VOICES = {
    "M1": ("male", "A modern Received Pronunciation British accent, a deep, resonant bass-baritone, a man in his late "
                   "sixties: a warm, thoughtful natural-history narrator with quiet wonder."),
    "M2": ("male", "A modern Received Pronunciation British accent, a low, chesty baritone with a little gravel, a man "
                   "in his seventies: calm, wise and gently amused, like a veteran documentary narrator."),
    "F1": ("female", "A modern Received Pronunciation British accent, a deep, rich contralto, a woman in her sixties: "
                     "warm, intelligent and quietly enthusiastic, like a natural-history narrator."),
    "F2": ("female", "A modern Received Pronunciation British accent, a low, smoky alto, a woman in her late fifties: "
                     "calm and assured, with a lively spark of curiosity."),
    "F3": ("female", "A southern English accent, modern Received Pronunciation as heard on the BBC, a low, warm voice, "
                     "an Englishwoman in her sixties: an intelligent, lively natural-history narrator."),
    "M3": ("male", "British accent, a deep, resonant bass-baritone, a man in his late sixties: a warm, thoughtful "
                   "natural-history narrator, energetic and conversational. Full of wonder."),
    "F4": ("female", "British accent, a low, warm alto, a woman in her sixties: a warm, thoughtful natural-history "
                     "narrator, energetic and conversational. Full of wonder."),
}
for suffix in "bcd":
    VOICES["M3" + suffix], VOICES["F4" + suffix] = VOICES["M3"], VOICES["F4"]

PASSAGES = {
    "p1-opening": PASSAGES_OPENING,
    "p2-middle": ("And in every tank, one leaf that dropped in during the first seconds filled the place with its "
                  "descendants. In tank one, out of seven thousand four hundred bodies, eleven weren't family. Round "
                  "fifty-four went at the wall itself, with joints that push and husks that smell. Neither helped."),
    "p3-last": ("Next, round fifty-eight digs pits into the floor, starting in tank two. The food will gather in the "
                "pits, and then we'll see what gathers with it."),
}


def read_key() -> str:
    for path in (HERE / ".env.local", HERE / "env.local", HERE.parents[2] / ".env"):
        if path.is_file():
            for line in path.read_text(encoding="utf-8").splitlines():
                key, _, value = line.partition("=")
                value = value.strip().strip('"').strip("'")
                if key.strip() == "GEMINI_API_KEY" and value and not value.startswith("<"):
                    return value
    sys.exit("Narration needs a Gemini API key: add GEMINI_API_KEY to .env at the repository's root "
             "(the keys are listed in .env.example), or to scripts/tts/google/.env.local")


def save_wav(path: Path, data_b64: str) -> float:
    raw = base64.b64decode(data_b64)
    path.write_bytes(raw)
    try:
        with wave.open(io.BytesIO(raw)) as w:
            return round(w.getnframes() / w.getframerate(), 2)
    except wave.Error:
        return -1.0


def main() -> int:
    # This machine intercepts TLS, so Python's bundled certificates fail; truststore makes ssl use the Windows
    # certificate store instead. Verification stays on (the ml-store rule: fix the store, never disable it).
    import truststore

    truststore.inject_into_ssl()
    from google import genai

    client = genai.Client(api_key=read_key())
    OUT.mkdir(parents=True, exist_ok=True)
    ids = json.loads(IDS.read_text(encoding="utf-8")) if IDS.is_file() else {}
    rows: list[dict] = []

    for key, (gender, description) in ((k, VOICES[k]) for k in ROUNDS[ROUND]["voices"]):
        if key in ids:
            print(f"{key}: reusing {ids[key][:10]}...", flush=True)
            continue
        t = time.perf_counter()
        try:
            voice = client.voices.create(store=True, voice={
                "model": MODEL, "type": "prompted", "display_name": f"Cambrian narrator {key}", "gender": gender,
                "language_code": "en-GB", "accent": "British", "pitch": "low", "prompted": {"input": description},
            })
        except Exception as e:  # report and go on with the other voices
            rows.append({"voice": key, "step": "design", "error": f"{type(e).__name__}: {str(e)[:500]}"})
            print(json.dumps(rows[-1]), flush=True)
            continue
        ids[key] = voice.id
        IDS.write_text(json.dumps(ids, indent=2), encoding="utf-8")
        row = {"voice": key, "step": "design", "description": description, "seconds_wall": round(time.perf_counter() - t, 1)}
        sample = getattr(voice, "sample_audio", None)
        if sample is not None and getattr(sample, "data", None):
            path = OUT / f"{key}-0-design-sample.wav"
            row.update(file=path.name, seconds=save_wav(path, sample.data), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        rows.append(row)
        print(json.dumps(row), flush=True)

    for key in ROUNDS[ROUND]["voices"]:
        if key not in ids:
            continue
        for pkey, text in ROUNDS[ROUND].get("passages", PASSAGES).items():
            if (OUT / f"{key}-{pkey}.wav").is_file():  # a rerun sends only what is missing
                continue
            t = time.perf_counter()
            try:
                result = client.interactions.create(
                    model=MODEL,
                    input=[{"type": "user_input", "content": [{
                        "type": "text", "text": text,
                        "annotations": [{"type": "speech_metadata", "style": STYLE}],
                    }]}],
                    response_format={"type": "audio"},
                    generation_config={"speech_config": [{"voice": ids[key]}]},
                )
                path = OUT / f"{key}-{pkey}.wav"
                seconds = save_wav(path, result.output_audio.data)
                row = {"voice": key, "step": "read", "passage": pkey, "file": path.name, "seconds": seconds,
                       "seconds_wall": round(time.perf_counter() - t, 1), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
            except Exception as e:
                row = {"voice": key, "step": "read", "passage": pkey, "error": f"{type(e).__name__}: {str(e)[:500]}"}
            rows.append(row)
            print(json.dumps(row), flush=True)

    (OUT / "index.json").write_text(json.dumps({
        "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "model": MODEL, "style": STYLE,
        "round": ROUND, "voices": {k: {"gender": VOICES[k][0], "description": VOICES[k][1]} for k in ROUNDS[ROUND]["voices"]},
        "passages": ROUNDS[ROUND].get("passages", PASSAGES), "runs": rows}, indent=2, ensure_ascii=False), encoding="utf-8")
    return 0 if not any("error" in r for r in rows) else 1


if __name__ == "__main__":
    sys.exit(main())
