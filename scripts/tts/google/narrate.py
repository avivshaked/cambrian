"""Narrate a story film's script with a Google-designed voice, one request per beat (the owner's choice, 2026-10-09).

  <gemini python> -B scripts/tts/google/narrate.py SCRIPT.md OUT_DIR --build
  <gemini python> -B scripts/tts/google/narrate.py SCRIPT.md OUT_DIR --send VOICE [VOICE ...] --style "..."

--build sends nothing: it writes OUT_DIR/beats.txt, every beat's text exactly as it would be sent, for the owner to
read first. A beat is a "## N." section of the script; its Say: lines are joined in order, a blank line between
passages, the {{braces}} taken off names. A Hold: inside a beat becomes Google's <long pause> tag; a hold that ends
a beat is left to the edit, which lays exact silence between the beats' files.

--send reads every beat in each named voice (ids from voices.local.json beside this script) with the given style
line, writing OUT_DIR/<VOICE>-beat-<N>.wav and index.json. A beat whose file exists is not sent again. The key is
read from env.local or .env.local here and never printed.

The free tier allows Gemini 3.8 Flash TTS 10 requests a day and 3 a minute (AI Studio's rate-limit page, project
CambrianProject, 2026-10-10). The day resets at midnight Pacific time. So one voice's
nine beats fit a day, a second voice's do not, and --gap (seconds between requests, 21 by default) keeps a run
under the minute's limit.
"""

from __future__ import annotations

import base64
import hashlib
import io
import json
import re
import sys
import time
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
MODEL = "gemini-3.8-flash-tts"


def beats(script: Path) -> list[dict]:
    out, cur, holds_pending = [], None, False
    for line in script.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^## (\d+)\. (.+)$", line)
        if m:
            cur = {"beat": int(m.group(1)), "title": m.group(2), "paragraphs": [], "end_hold": None}
            out.append(cur)
            holds_pending = False
            continue
        if line.startswith("## "):
            cur = None
            continue
        if cur is None:
            continue
        if line.startswith("### "):
            cur["paragraphs"].append([])
            continue
        if line.startswith("Say: "):
            if holds_pending:
                cur["paragraphs"][-1].append("<long pause>")
                holds_pending = False
            cur["paragraphs"][-1].append(re.sub(r"\{\{([^}]*)\}\}", r"\1", line[5:].strip()))
            cur["end_hold"] = None
        elif line.startswith("Hold: "):
            cur["end_hold"] = float(line[6:])
            holds_pending = True
    for b in out:
        # A hold followed by more passages became a tag at the start of the next one; one that ends the beat did not.
        paras = [" ".join(p) for p in b["paragraphs"] if p]
        text = "\n\n".join(paras)
        text = re.sub(r"\n\n<long pause> ", " <long pause>\n\n", text)
        if re.search(r"\d", re.sub(r"<[^>]*>", "", text)):
            sys.exit(f"beat {b['beat']} has digits, which the voice would read as it likes: fix the script")
        b["text"] = text
        del b["paragraphs"]
    return out


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


def main() -> int:
    script, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    bs = beats(script)
    if "--build" in sys.argv:
        lines = []
        for b in bs:
            hold = f", then {b['end_hold']:g} s of silence in the edit" if b["end_hold"] else ""
            lines.append(f"=== Beat {b['beat']}: {b['title']} ({len(b['text'])} characters{hold}) ===\n\n{b['text']}\n")
        (out / "beats.txt").write_text("\n".join(lines), encoding="utf-8")
        print(f"{len(bs)} beats, {sum(len(b['text']) for b in bs)} characters -> {out / 'beats.txt'}")
        return 0

    i = sys.argv.index("--send")
    voices = []
    for a in sys.argv[i + 1:]:
        if a.startswith("--"):
            break
        voices.append(a)
    style = sys.argv[sys.argv.index("--style") + 1]
    gap = float(sys.argv[sys.argv.index("--gap") + 1]) if "--gap" in sys.argv else 21.0
    ids = json.loads((HERE / "voices.local.json").read_text(encoding="utf-8"))

    import truststore

    truststore.inject_into_ssl()
    from google import genai
    from google.genai._gaos.utils import BackoffStrategy, RetryConfig

    client = genai.Client(api_key=read_key())
    index_path = out / "index.json"
    index = json.loads(index_path.read_text(encoding="utf-8")) if index_path.is_file() else {"runs": []}
    index.update(model=MODEL, script=str(script), style=style, beats=bs)
    failed, sent = False, 0
    for v in voices:
        for b in bs:
            path = out / f"{v}-beat-{b['beat']}.wav"
            if path.is_file():
                continue
            if sent:
                time.sleep(gap)
            sent += 1
            t = time.perf_counter()
            try:
                result = client.interactions.create(
                    model=MODEL,
                    input=[{"type": "user_input", "content": [{
                        "type": "text", "text": b["text"],
                        "annotations": [{"type": "speech_metadata", "style": style}],
                    }]}],
                    response_format={"type": "audio"},
                    generation_config={"speech_config": [{"voice": ids[v]}]},
                    # The client retries a 429 by itself and sleeps for whatever Retry-After Google sends, which
                    # under the free tier's daily limit is hours: F4's beats sat silent for 13 minutes and more on
                    # 2026-10-10. With no retries a limit is reported at once, as an error row.
                    retries=RetryConfig("none", BackoffStrategy(0, 0, 1, 0), False),
                    timeout=180,
                )
                raw = base64.b64decode(result.output_audio.data)
                path.write_bytes(raw)
                with wave.open(io.BytesIO(raw)) as w:
                    seconds = round(w.getnframes() / w.getframerate(), 2)
                words = len(re.sub(r"<[^>]*>", "", b["text"]).split())
                row = {"voice": v, "beat": b["beat"], "file": path.name, "seconds": seconds,
                       "wpm": round(words / seconds * 60), "seconds_wall": round(time.perf_counter() - t, 1),
                       "sha256": hashlib.sha256(raw).hexdigest()}
            except Exception as e:
                failed = True
                row = {"voice": v, "beat": b["beat"], "error": f"{type(e).__name__}: {str(e)[:300]}"}
            index["runs"].append(row)
            index_path.write_text(json.dumps(index, indent=2, ensure_ascii=False), encoding="utf-8")
            print(json.dumps(row), flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
