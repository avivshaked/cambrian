"""Shared plumbing for the four TTS audition runners (chatterbox-en, voxcpm2, cosyvoice3, omnivoice).

Imported by each runner before any torch import. Importing this module sets the offline variables and redirects
every cache into scratch/tts-audition/cache/, so it must stay the first import. Standard library only at import
time; numpy, soundfile and torch are imported inside the functions that need them, after the environment is set.

Layout it assumes (scripts/tts/audition/common.py):
  parents[3] = project root, parents[1] = scripts/tts (paths.local.toml lives there).
Writes only under scratch/tts-audition/ (caches, trimmed reference clips) and wherever --out points.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import re
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]  # the project root
TTS_DIR = Path(__file__).resolve().parents[1]  # scripts/tts
AUDITION = ROOT / "scratch" / "tts-audition"
CACHE = AUDITION / "cache"


def external(item_id: str) -> Path:
    """The local folder of an external item, from paths.local.toml (see paths.example.toml and mlstore.toml).

    The ml-store README's function, with one change: tomllib is Python 3.11+, and the cosyvoice3 environment is
    Python 3.10, so a flat `key = 'value'` reader stands in there (the file is flat by construction).
    """
    local = TTS_DIR / "paths.local.toml"
    folders: dict = {}
    if local.is_file():
        text = local.read_text(encoding="utf-8")
        try:
            import tomllib

            folders = tomllib.loads(text)
        except ImportError:
            for line in text.splitlines():
                m = re.match(r"""^\s*([A-Za-z0-9_.-]+)\s*=\s*(?:'([^']*)'|"((?:[^"\\]|\\.)*)")\s*(?:#.*)?$""", line)
                if m:
                    folders[m.group(1)] = m.group(2) if m.group(2) is not None else m.group(3).replace("\\\\", "\\")
    folder = folders.get(item_id)
    if not isinstance(folder, str) or not Path(folder).is_dir():
        raise FileNotFoundError(
            f"{item_id}: set its folder in paths.local.toml (copy paths.example.toml). "
            "mlstore.toml lists the files to download, with their addresses and hashes."
        )
    return Path(folder)


def setup_environment() -> None:
    """Offline switches and cache redirection. Runs when this module is imported, before any torch import."""
    for sub in ("hf", "hf-hub", "torch", "xdg", "modelscope", "triton", "numba", "mpl", "inductor", "refs"):
        (CACHE / sub).mkdir(parents=True, exist_ok=True)
    os.environ.update({
        # nothing may reach the network: every model is loaded from its local folder
        "HF_HUB_OFFLINE": "1", "TRANSFORMERS_OFFLINE": "1", "HF_DATASETS_OFFLINE": "1",
        "HF_HUB_DISABLE_TELEMETRY": "1", "DO_NOT_TRACK": "1", "GRADIO_ANALYTICS_ENABLED": "False",
        "MODELSCOPE_OFFLINE": "1",  # ModelScope's own switch (only read by newer modelscope; harmless otherwise)
        # every cache inside the project
        "HF_HOME": str(CACHE / "hf"), "HF_HUB_CACHE": str(CACHE / "hf-hub"),
        "TORCH_HOME": str(CACHE / "torch"), "XDG_CACHE_HOME": str(CACHE / "xdg"),
        "MODELSCOPE_CACHE": str(CACHE / "modelscope"), "TRITON_CACHE_DIR": str(CACHE / "triton"),
        "NUMBA_CACHE_DIR": str(CACHE / "numba"), "MPLCONFIGDIR": str(CACHE / "mpl"),
        "TORCHINDUCTOR_CACHE_DIR": str(CACHE / "inductor"),
        "PYTHONDONTWRITEBYTECODE": "1", "TOKENIZERS_PARALLELISM": "false",
    })


setup_environment()

_STDOUT = sys.stdout  # the JSON line goes here; model code that prints to stdout is moved to stderr by quiet_stdout()


def quiet_stdout() -> None:
    """Send stray prints from model code to stderr so stdout carries exactly one JSON line."""
    sys.stdout = sys.stderr


def parse_args(description: str) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=description)
    p.add_argument("--ref-wav", required=True, type=Path, help="reference clip (WAV)")
    p.add_argument("--ref-text", required=True,
                   help="exact transcript of the reference clip; if --ref-seconds trims the clip, the transcript "
                        "must cover only the part kept (ignored by chatterbox-en, which uses audio only)")
    p.add_argument("--text", required=True, help="text to speak")
    p.add_argument("--out", required=True, type=Path, help="output WAV (mono)")
    p.add_argument("--seed", required=True, type=int)
    p.add_argument("--ref-seconds", type=float, default=None, help="use only the first S seconds of the reference")
    return p.parse_args()


def seed_all(seed: int) -> None:
    """Same recipe as scripts/tts/accent_ab.py: random, numpy, torch, torch.cuda."""
    import numpy as np
    import torch

    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    torch.cuda.manual_seed_all(seed)


def prepare_reference(ref_wav: Path, ref_seconds: float | None, max_seconds: float | None = None) -> tuple[Path, float]:
    """Return (path to hand the model, seconds of reference it holds).

    No trim wanted and the clip is short enough: the original file is passed untouched. Otherwise the first
    S seconds are written, mono, 16-bit, at the clip's own sample rate, to scratch/tts-audition/cache/refs/.
    max_seconds is a hard model limit (CosyVoice: 30 s); it applies on top of --ref-seconds.
    """
    import soundfile as sf

    if not ref_wav.is_file():
        raise FileNotFoundError(f"reference clip not found: {ref_wav}")
    info = sf.info(str(ref_wav))
    total = info.frames / info.samplerate
    limit = total
    for cap in (ref_seconds, max_seconds):
        if cap is not None:
            limit = min(limit, cap)
    if limit >= total - 1e-6:
        return ref_wav, round(total, 3)
    data, sr = sf.read(str(ref_wav), dtype="float32", always_2d=True, frames=int(round(limit * info.samplerate)))
    mono = data.mean(axis=1)
    key = hashlib.sha256(f"{ref_wav.resolve()}|{ref_wav.stat().st_mtime_ns}|{limit:.4f}".encode()).hexdigest()[:12]
    out = CACHE / "refs" / f"{ref_wav.stem}.{key}.{limit:.2f}s.wav"
    sf.write(str(out), mono, sr, subtype="PCM_16")
    return out, round(mono.shape[0] / sr, 3)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def write_wav(path: Path, audio, sample_rate: int) -> float:
    """Write a mono 16-bit WAV from a float array or tensor of any shape holding one channel. Returns seconds."""
    import numpy as np
    import soundfile as sf

    if hasattr(audio, "detach"):
        audio = audio.detach().cpu().numpy()
    mono = np.asarray(audio, dtype=np.float32).reshape(-1)
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(path), mono, int(sample_rate), subtype="PCM_16")
    return mono.shape[0] / sample_rate


def sync_cuda() -> None:
    import torch

    if torch.cuda.is_available():
        torch.cuda.synchronize()


def report(model: str, item_id: str, args: argparse.Namespace, ref_seconds_used: float, out_seconds: float,
           gen_seconds: float, sample_rate: int, **extra) -> None:
    """The one JSON line each runner prints."""
    row = {
        "model": model, "item": item_id, "seed": args.seed,
        "ref_seconds": ref_seconds_used, "out_seconds": round(out_seconds, 3), "gen_seconds": round(gen_seconds, 2),
        "sha256": sha256_file(args.out), "sample_rate": sample_rate, "out": str(args.out),
    }
    row.update(extra)
    print(json.dumps(row), file=_STDOUT, flush=True)


def log(msg: str) -> None:
    print(msg, file=sys.stderr, flush=True)


def timer() -> float:
    return time.perf_counter()
