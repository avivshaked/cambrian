"""Run the TTS audition: each model clones Edmund (M9.wav) reading the design clip's words and one paragraph.

Owner's ruling of 2026-10-09: four local models (chatterbox-en, voxcpm2, cosyvoice3, omnivoice), weights through
ml-store (scripts/tts/mlstore.toml, paths in scripts/tts/paths.local.toml). Same texts as the Qwen accent test
(scripts/tts/accent_ab.py), seed 0. Each model gets the full 20 s clip with its transcript; omnivoice, whose makers
recommend 3 to 10 s, also gets the first sentence alone (the clip cut at 9.0 s, in the pause after "stomach").
Chatterbox ignores the transcript and uses only the clip's first 10 s, as its code does.

Run from the project root with Python 3.12, after booking the GPU:
  py -3.12 scripts/tts/audition/run_audition.py [--only MODEL_ID] [--dry]
Writes the clips, index.json and per-run logs to scratch/owner/tts-audition/.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
ENVS = ROOT / "scratch" / "tts-audition" / "envs"
OUT = ROOT / "scratch" / "owner" / "tts-audition"
REF = ROOT / "scratch" / "owner" / "voices-2026-10-08" / "M9.wav"

DESIGN_TEXT = (
    "Down on the floor of this tank lives a family of twelve hundred and fifty-two creatures, and nearly every one "
    "of them is nothing but a stomach. Each is a single box of tissue that eats the marine snow, the dead stuff "
    "drifting down. All of them come from one creature that dropped in from outside."
)
FIRST_SENTENCE = (
    "Down on the floor of this tank lives a family of twelve hundred and fifty-two creatures, and nearly every one "
    "of them is nothing but a stomach."
)
PARA = (
    "Small tan lumps, soaking up the snow, the dead stuff drifting down. All from one creature that dropped in from "
    "outside. In our last film, of sixty-three creatures like this, one bred. We called it Lucky, and its family "
    "lasted two generations."
)
TEXTS = {"same": DESIGN_TEXT, "para": PARA}

# (model id, label, ref seconds or None, ref transcript)
REFS = {
    "chatterbox-en": [("full", None, DESIGN_TEXT)],
    "voxcpm2": [("full", None, DESIGN_TEXT)],
    "cosyvoice3": [("full", None, DESIGN_TEXT)],
    "omnivoice": [("full", None, DESIGN_TEXT), ("short9s", 9.0, FIRST_SENTENCE)],
}


def python_of(model: str) -> Path:
    return ENVS / model / "Scripts" / "python.exe"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", choices=sorted(REFS))
    ap.add_argument("--dry", action="store_true", help="print the plan and check the files; run nothing")
    args = ap.parse_args()
    models = [args.only] if args.only else list(REFS)

    plan = []
    for model in models:
        for label, seconds, ref_text in REFS[model]:
            for tkey, text in TEXTS.items():
                plan.append((model, label, seconds, ref_text, tkey, text))
    problems = [str(p) for p in [REF, HERE.parent / "paths.local.toml", *(python_of(m) for m in models)] if not p.exists()]
    print(f"{len(plan)} runs -> {OUT}")
    for model, label, seconds, _, tkey, _ in plan:
        print(f"  {model:14s} ref={label:8s} text={tkey}")
    if problems:
        print("missing:", *problems, sep="\n  ")
        return 1
    if args.dry:
        return 0

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "logs").mkdir(exist_ok=True)
    shutil.copyfile(REF, OUT / "edmund-0-original-design-clip.wav")
    index_path = OUT / "index.json"
    index = json.loads(index_path.read_text(encoding="utf-8")) if index_path.is_file() else {"runs": []}
    started = time.perf_counter()
    for model, label, seconds, ref_text, tkey, text in plan:
        name = f"edmund-{model}-{label}-{tkey}"
        cmd = [str(python_of(model)), "-I", "-B", str(HERE / f"{model}.py"), "--ref-wav", str(REF),
               "--ref-text", ref_text, "--text", text, "--out", str(OUT / f"{name}.wav"), "--seed", "0"]
        if seconds is not None:
            cmd += ["--ref-seconds", str(seconds)]
        t = time.perf_counter()
        with (OUT / "logs" / f"{name}.log").open("w", encoding="utf-8") as log:
            proc = subprocess.run(cmd, cwd=ROOT, stdout=subprocess.PIPE, stderr=log, text=True, encoding="utf-8")
        row: dict = {"file": f"{name}.wav", "model": model, "ref": label, "text": tkey,
                     "wall_s": round(time.perf_counter() - t, 1), "exit": proc.returncode}
        lines = [ln for ln in proc.stdout.splitlines() if ln.strip().startswith("{")]
        if proc.returncode == 0 and lines:
            row["result"] = json.loads(lines[-1])
        else:
            row["stdout_tail"] = proc.stdout[-2000:]
        index["runs"] = [r for r in index["runs"] if r.get("file") != row["file"]] + [row]
        index_path.write_text(json.dumps(index, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps(row, ensure_ascii=False), flush=True)
    print(f"done in {time.perf_counter() - started:.0f} s")
    return 0 if all(r["exit"] == 0 for r in index["runs"]) else 1


if __name__ == "__main__":
    sys.exit(main())
