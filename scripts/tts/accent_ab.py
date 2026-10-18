"""Accent A/B for the narration service's Qwen3-TTS clone (owner's yes, 2026-10-09).

Each voice (Edmund = M9, Sienna = F8) speaks two texts: the design clip's own words ("same") and one paragraph
of the round 57 film ("para"), under six setups:

  1-current         Base 1.7B, ICL, language English, non_streaming_mode False (the service's settings)
  2-nonstream       as 1, non_streaming_mode True
  3-auto            as 1, language Auto
  4-nonstream-auto  as 1, both changes
  5-xvector         as 1, x_vector_only_mode True (a control, expected worse)
  6-design          VoiceDesign 1.7B speaking the text itself from the voice's description (no clone),
                    non_streaming_mode True as the service designs; seeds 0 and 1 on "para"

Everything else is the service's engine profile (qwen3-base-1.7b.p1 / qwen3-design-1.7b.p1): bfloat16, sdpa,
the determinism switches, the ten sampling values, seed 0, the cap max(128, ceil(2.5 x chars)).
Clips are normalised to -20 dBFS RMS so loudness does not sway the ear; the raw peak is in the index.

Run with the narration worker's venv, from the project root:
  <venv>/python.exe -I -B scripts/tts/accent_ab.py --dry   (checks inputs, no torch, no GPU)
  <venv>/python.exe -I -B scripts/tts/accent_ab.py         (the run; book the card first)
Writes only under scratch/owner/accent-ab/ and scratch/tts-research/cache/.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import random
import sys
import time
from pathlib import Path

ROOT = Path(r"D:\Projects\experiments\evolution-simulator")
OUT = ROOT / "scratch" / "owner" / "accent-ab"
CACHE = ROOT / "scratch" / "tts-research" / "cache"
_models_dir = os.environ.get("NARRATION_MODELS_DIR")
if not _models_dir:
    sys.exit("NARRATION_MODELS_DIR is not set: point it at the narration service's models folder (the Hugging Face cache holding the Qwen3-TTS models).")
MODELS = Path(_models_dir)
BASE = MODELS / "models--Qwen--Qwen3-TTS-12Hz-1.7B-Base" / "snapshots" / "fd4b254389122332181a7c3db7f27e918eec64e3"
DESIGN = MODELS / "models--Qwen--Qwen3-TTS-12Hz-1.7B-VoiceDesign" / "snapshots" / "5ecdb67327fd37bb2e042aab12ff7391903235d3"

GENERATION = {
    "do_sample": True, "top_k": 50, "top_p": 1.0, "temperature": 0.9, "repetition_penalty": 1.05,
    "subtalker_dosample": True, "subtalker_top_k": 50, "subtalker_top_p": 1.0, "subtalker_temperature": 0.9,
}
PER_CHAR, FLOOR, CEILING = 2.5, 128, 8192

DESIGN_TEXT = (
    "Down on the floor of this tank lives a family of twelve hundred and fifty-two creatures, and nearly every one "
    "of them is nothing but a stomach. Each is a single box of tissue that eats the marine snow, the dead stuff "
    "drifting down. All of them come from one creature that dropped in from outside."
)
PARA = (
    "Small tan lumps, soaking up the snow, the dead stuff drifting down. All from one creature that dropped in from "
    "outside. In our last film, of sixty-three creatures like this, one bred. We called it Lucky, and its family "
    "lasted two generations."
)
TEXTS = {"same": DESIGN_TEXT, "para": PARA}

VOICES = {
    "edmund": {
        "clip": ROOT / "scratch" / "owner" / "voices-2026-10-08" / "M9.wav",
        "sha256": "a7fe367af185c9ce7d044752efcf5d2b7248ecccc7da4b7687171634dd560cb1",
        "specs": (ROOT / "logbook" / "specs" / "narrator-voices-2026-10-08.json", "M9"),
    },
    "sienna": {
        "clip": ROOT / "scratch" / "owner" / "voices-2026-10-08b" / "F8.wav",
        "sha256": "81ded65327696c81f78fe73d71ad9ce4c4997cbe618608259a7ef729cbd472d1",
        "specs": (ROOT / "logbook" / "specs" / "narrator-voices-2026-10-08b.json", "F8"),
    },
}

CLONE_SETUPS = [
    ("1-current", "English", False, False),
    ("2-nonstream", "English", True, False),
    ("3-auto", "Auto", False, False),
    ("4-nonstream-auto", "Auto", True, False),
    ("5-xvector", "English", False, True),
]
DESIGN_RUNS = [("same", 0), ("para", 0), ("para", 1)]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def cap(text: str) -> int:
    return min(CEILING, max(FLOOR, math.ceil(len(text) * PER_CHAR)))


def check_inputs() -> dict[str, dict]:
    """Every input exists and matches; returns each voice's description and transcript."""
    for snap in (BASE, DESIGN):
        if not (snap / "model.safetensors").is_file():
            sys.exit(f"missing model snapshot: {snap}")
    found = {}
    for key, v in VOICES.items():
        got = sha256(v["clip"])
        if got != v["sha256"]:
            sys.exit(f"{key}: clip sha256 {got} != {v['sha256']}")
        spec_path, label = v["specs"]
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        if spec["design_text"] != DESIGN_TEXT:
            sys.exit(f"{key}: design_text in {spec_path.name} differs from this script's")
        found[key] = {"description": spec["descriptions"][label], "transcript": spec["design_text"]}
    return found


def main() -> None:
    dry = "--dry" in sys.argv
    voices = check_inputs()
    plan = [(k, t, s[0]) for k in VOICES for t in TEXTS for s in CLONE_SETUPS]
    plan += [(k, t, f"6-design-s{seed}") for k in VOICES for t, seed in DESIGN_RUNS]
    print(f"inputs OK; {len(plan)} renders planned -> {OUT}")
    for row in plan:
        print("  ", *row)
    if dry:
        return

    OUT.mkdir(parents=True, exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    os.environ.update({
        "HF_HUB_OFFLINE": "1", "TRANSFORMERS_OFFLINE": "1", "CUBLAS_WORKSPACE_CONFIG": ":4096:8",
        "HF_HOME": str(CACHE / "hf"), "TORCH_HOME": str(CACHE / "torch"), "XDG_CACHE_HOME": str(CACHE),
        "TRITON_CACHE_DIR": str(CACHE / "triton"),
    })
    import numpy as np
    import soundfile as sf
    import torch
    from qwen_tts import Qwen3TTSModel

    torch.backends.cuda.matmul.allow_tf32 = False
    torch.backends.cudnn.allow_tf32 = False
    torch.backends.cudnn.deterministic = True
    torch.backends.cudnn.benchmark = False
    torch.use_deterministic_algorithms(True, warn_only=True)

    def seed_all(seed: int) -> None:
        random.seed(seed)
        np.random.seed(seed)
        torch.manual_seed(seed)
        torch.cuda.manual_seed_all(seed)

    index: list[dict] = []

    def save(name: str, wav, sr: int, meta: dict) -> None:
        audio = np.asarray(wav, dtype=np.float32).reshape(-1)
        peak = float(np.max(np.abs(audio))) if audio.size else 0.0
        rms = float(np.sqrt(np.mean(audio ** 2))) if audio.size else 0.0
        gain = (10 ** (-20 / 20)) / rms if rms > 0 else 1.0
        gain = min(gain, 0.98 / peak) if peak > 0 else gain
        path = OUT / f"{name}.wav"
        sf.write(str(path), audio * gain, sr, subtype="PCM_16")
        row = {"file": path.name, **meta, "seconds": round(audio.size / sr, 2), "raw_peak": round(peak, 4),
               "gain_db": round(20 * math.log10(gain), 2), "sha256": sha256(path)}
        index.append(row)
        print(json.dumps(row), flush=True)

    def load(snap: Path):
        t = time.perf_counter()
        m = Qwen3TTSModel.from_pretrained(str(snap), device_map="cuda:0", dtype=torch.bfloat16,
                                          attn_implementation="sdpa", local_files_only=True)
        torch.cuda.synchronize()
        print(f"loaded {snap.parent.parent.name} in {time.perf_counter() - t:.1f} s", flush=True)
        return m

    def suspended(fn):
        torch.use_deterministic_algorithms(False)
        try:
            return fn()
        finally:
            torch.use_deterministic_algorithms(True, warn_only=True)

    started = time.perf_counter()
    model = load(BASE)
    for key, v in VOICES.items():
        audio, sr_ref = sf.read(str(v["clip"]), dtype="float32", always_2d=False)
        if audio.ndim == 2:
            audio = audio.mean(axis=1, dtype=np.float32)
        prompts = {}
        for xvec in (False, True):
            prompts[xvec] = suspended(lambda: model.create_voice_clone_prompt(
                ref_audio=(audio, int(sr_ref)), ref_text=voices[key]["transcript"], x_vector_only_mode=xvec))
        for tkey, text in TEXTS.items():
            for label, lang, nsm, xvec in CLONE_SETUPS:
                seed_all(0)
                t = time.perf_counter()
                wavs, sr = model.generate_voice_clone(text=text, language=lang, voice_clone_prompt=prompts[xvec],
                                                      non_streaming_mode=nsm, max_new_tokens=cap(text), **GENERATION)
                save(f"{key}-{tkey}-{label}", wavs[0], sr, {
                    "voice": key, "text": tkey, "setup": label, "model": "Base 1.7B", "language": lang,
                    "non_streaming_mode": nsm, "x_vector_only_mode": xvec, "seed": 0,
                    "gen_s": round(time.perf_counter() - t, 1)})
    del model
    torch.cuda.empty_cache()

    model = load(DESIGN)
    for key in VOICES:
        for tkey, seed in DESIGN_RUNS:
            text = TEXTS[tkey]
            seed_all(seed)
            t = time.perf_counter()
            wavs, sr = model.generate_voice_design(text=text, instruct=voices[key]["description"], language="English",
                                                   non_streaming_mode=True, max_new_tokens=cap(text), **GENERATION)
            save(f"{key}-{tkey}-6-design-s{seed}", wavs[0], sr, {
                "voice": key, "text": tkey, "setup": f"6-design-s{seed}", "model": "VoiceDesign 1.7B",
                "language": "English", "non_streaming_mode": True, "x_vector_only_mode": None, "seed": seed,
                "gen_s": round(time.perf_counter() - t, 1)})
    del model
    torch.cuda.empty_cache()

    (OUT / "index.json").write_text(json.dumps({
        "made": time.strftime("%Y-%m-%dT%H:%M:%S"), "total_s": round(time.perf_counter() - started, 1),
        "texts": TEXTS, "generation": GENERATION, "clips": index}, indent=2), encoding="utf-8")
    print(f"done in {time.perf_counter() - started:.0f} s; {len(index)} clips", flush=True)


if __name__ == "__main__":
    main()
