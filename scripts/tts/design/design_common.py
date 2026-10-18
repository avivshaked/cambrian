"""Shared plumbing for the two voice-designer runners (design_omnivoice.py, design_voxcpm2.py).

Reuses scripts/tts/audition/audition_common.py for external(), the offline settings and cache redirection (done at its
import), seed_all(), write_wav() and sha256_file(). Import this module first in a runner: it puts the audition folder on
sys.path (last, so it cannot shadow a package) and imports audition_common before any torch import.

Writes only under --out-dir (the WAVs and designs.jsonl). Nothing here loads a model.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

_AUDITION = Path(__file__).resolve().parents[1] / "audition"
if str(_AUDITION) not in sys.path:
    sys.path.append(str(_AUDITION))  # last: audition/ holds omnivoice.py and voxcpm2.py, named like the packages
import audition_common as common  # noqa: E402,F401  (sets HF_HUB_OFFLINE etc. and the cache folders; must precede torch)


def parse_args(description: str, extra=None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=description)
    p.add_argument("--voices", required=True, type=Path, help="voices JSON: design_text and descriptions M1..M10, F1..F10")
    p.add_argument("--only", default=None, help="comma-separated keys to design, e.g. M1,F3 (default: every key in the file)")
    p.add_argument("--seed", type=int, default=0, help="seed applied before each voice, so one voice repeats on its own")
    p.add_argument("--out-dir", required=True, type=Path, help="folder for <key>.wav and designs.jsonl")
    p.add_argument("--validate-only", action="store_true",
                   help="check the voices file and every description against the model's own rules; load no model")
    if extra:
        extra(p)
    return p.parse_args()


def load_voices(path: Path, only: str | None) -> tuple[str, dict, dict]:
    """Return (design_text, {key: description}, whole document). Refuses a missing field rather than defaulting."""
    doc = json.loads(path.read_text(encoding="utf-8"))
    for field in ("design_text", "descriptions"):
        if field not in doc:
            raise KeyError(f"{path}: missing '{field}'; present: {sorted(doc)}")
    descs = doc["descriptions"]
    keys = list(descs)
    if only:
        wanted = [k.strip() for k in only.split(",") if k.strip()]
        missing = [k for k in wanted if k not in descs]
        if missing:
            raise KeyError(f"--only names keys not in {path.name}: {missing}; have {keys}")
        keys = wanted
    return doc["design_text"], {k: descs[k] for k in keys}, doc


def run_voices(args, descs: dict, design_one, sample_rate_of, model_name: str, extra_row=None) -> int:
    """Design every voice with design_one(key, description) -> (audio, info dict); write WAV and one JSON line each.

    A voice that raises is reported as a row with an "error" field and the loop goes on; the exit code is 1 then.
    """
    args.out_dir.mkdir(parents=True, exist_ok=True)
    log_path = args.out_dir / "designs.jsonl"
    failures = 0
    for key, desc in descs.items():
        row = {"model": model_name, "key": key, "description": desc, "seed": args.seed}
        try:
            common.seed_all(args.seed)  # the same seed before every voice: a voice does not depend on the ones before it
            t0 = common.timer()
            audio, info = design_one(key, desc)
            common.sync_cuda()
            gen = common.timer() - t0
            wav = args.out_dir / f"{key}.wav"
            seconds = common.write_wav(wav, audio, sample_rate_of())
            row.update(info)
            row.update(seconds=round(seconds, 3), gen_seconds=round(gen, 2), sha256=common.sha256_file(wav),
                       sample_rate=sample_rate_of(), wav=str(wav))
        except Exception as e:  # noqa: BLE001  (one bad voice must not lose the rest of a GPU session)
            failures += 1
            row.update(error=f"{type(e).__name__}: {e}")
            common.log(f"{key}: FAILED {row['error']}")
        line = json.dumps(row, ensure_ascii=False)
        print(line, file=common._STDOUT, flush=True)
        with log_path.open("a", encoding="utf-8") as f:
            f.write(line + "\n")
    return 1 if failures else 0
