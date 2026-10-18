"""OmniVoice accent test (owner's yes, 2026-10-09): does OmniVoice's "british accent" setting hold?

The owner found that Qwen's design clips, M9 included, drift toward American within the clip, so this test asks
OmniVoice for the accent by name:
  clone  - M9 cloned with instruct "british accent", and with "british accent, male" (the same words and the
           film paragraph); the plain clones from the clone audition are copied beside them for comparison.
  design - six voices designed from scratch with "british accent" (scripts/tts/design/voices-omnivoice.json:
           M1, M4, M7, F3, F4, F7), each speaking the design text.
Seed 0. Run from the project root with Python 3.12, after booking the GPU:
  py -3.12 scripts/tts/audition/run_omnivoice_accent.py [--dry]
Writes to scratch/owner/omnivoice-accent/.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
PY = ROOT / "scratch" / "tts-audition" / "envs" / "omnivoice" / "Scripts" / "python.exe"
OUT = ROOT / "scratch" / "owner" / "omnivoice-accent"
PRIOR = ROOT / "scratch" / "owner" / "tts-audition"
REF = ROOT / "scratch" / "owner" / "voices-2026-10-08" / "M9.wav"
VOICES = ROOT / "scripts" / "tts" / "design" / "voices-omnivoice.json"
DESIGN_KEYS = "M1,M4,M7,F3,F4,F7"

sys.path.insert(0, str(HERE))
from run_audition import DESIGN_TEXT, TEXTS  # noqa: E402  (the same texts as the clone audition)

CLONES = [("british", "british accent"), ("british-male", "british accent, male")]


def main() -> int:
    dry = "--dry" in sys.argv
    missing = [str(p) for p in (PY, REF, VOICES, HERE.parent / "paths.local.toml") if not p.exists()]
    plan = [f"clone {label} {tkey}" for label, _ in CLONES for tkey in TEXTS] + [f"design {DESIGN_KEYS}"]
    print(*plan, sep="\n")
    if missing:
        print("missing:", *missing, sep="\n  ")
        return 1
    if dry:
        return 0

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "logs").mkdir(exist_ok=True)
    shutil.copyfile(REF, OUT / "edmund-0-original-design-clip.wav")
    for tkey in TEXTS:
        shutil.copyfile(PRIOR / f"edmund-omnivoice-full-{tkey}.wav", OUT / f"clone-plain-{tkey}.wav")
    rows = []
    started = time.perf_counter()
    for label, instruct in CLONES:
        for tkey, text in TEXTS.items():
            name = f"clone-{label}-{tkey}"
            cmd = [str(PY), "-I", "-B", str(HERE / "omnivoice.py"), "--ref-wav", str(REF), "--ref-text", DESIGN_TEXT,
                   "--text", text, "--out", str(OUT / f"{name}.wav"), "--seed", "0", "--instruct", instruct]
            with (OUT / "logs" / f"{name}.log").open("w", encoding="utf-8") as log:
                proc = subprocess.run(cmd, cwd=ROOT, stdout=subprocess.PIPE, stderr=log, text=True, encoding="utf-8")
            lines = [ln for ln in proc.stdout.splitlines() if ln.strip().startswith("{")]
            row = {"file": f"{name}.wav", "instruct": instruct, "text": tkey, "exit": proc.returncode,
                   "result": json.loads(lines[-1]) if proc.returncode == 0 and lines else None}
            rows.append(row)
            print(json.dumps(row), flush=True)
    design_dir = OUT / "design"
    cmd = [str(PY), "-B", str(ROOT / "scripts" / "tts" / "design" / "design_omnivoice.py"), "--voices",
           str(VOICES), "--only", DESIGN_KEYS, "--seed", "0", "--out-dir", str(design_dir)]
    with (OUT / "logs" / "design.log").open("w", encoding="utf-8") as log:
        proc = subprocess.run(cmd, cwd=ROOT, stdout=subprocess.PIPE, stderr=log, text=True, encoding="utf-8")
    rows.append({"design": DESIGN_KEYS, "exit": proc.returncode, "stdout_tail": proc.stdout[-3000:]})
    print(proc.stdout[-3000:], flush=True)
    (OUT / "index.json").write_text(json.dumps({"made": time.strftime("%Y-%m-%dT%H:%M:%S"),
                                                "total_s": round(time.perf_counter() - started, 1), "runs": rows},
                                               indent=2, ensure_ascii=False), encoding="utf-8")
    return 0 if all(r["exit"] == 0 for r in rows) else 1


if __name__ == "__main__":
    sys.exit(main())
