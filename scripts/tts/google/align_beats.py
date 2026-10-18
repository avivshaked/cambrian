"""Align every beat narrate.py wrote in a folder: word and sentence times for each <VOICE>-beat-<N>.wav.

  <python with torch and torchaudio <= 2.8> -B scripts/tts/google/align_beats.py OUT_DIR

Reads OUT_DIR/index.json for each beat's text, runs scripts/tts/align.py on every beat file that has no
<VOICE>-beat-<N>.align.json yet, and prints one line per file. Runs on the CPU and sends nothing.
"""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ALIGN = Path(__file__).resolve().parents[1] / "align.py"


def main() -> int:
    out = Path(sys.argv[1])
    texts = {b["beat"]: b["text"] for b in json.loads((out / "index.json").read_text(encoding="utf-8"))["beats"]}
    failed = False
    for wav in sorted(out.glob("*-beat-*.wav")):
        target = wav.with_suffix(".align.json")
        if target.is_file():
            continue
        beat = int(wav.stem.rsplit("-", 1)[1])
        text = out / f"beat-{beat}.txt"
        text.write_text(texts[beat], encoding="utf-8")
        r = subprocess.run([sys.executable, "-B", str(ALIGN), str(wav), str(text), str(target)],
                           capture_output=True, text=True)
        print(f"{wav.name}: " + (r.stdout.strip().replace("\n", " | ") or r.stderr.strip()[-300:]), flush=True)
        failed |= r.returncode != 0
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
