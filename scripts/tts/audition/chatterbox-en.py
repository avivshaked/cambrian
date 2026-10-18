"""Chatterbox (English, 0.5B) zero-shot clone: the TTS audition runner for item `chatterbox-en`.

  <repo>/scratch/tts-audition/envs/chatterbox-en/Scripts/python.exe -B scripts/tts/audition/chatterbox-en.py \
      --ref-wav REF.wav --ref-text "..." --text "..." --out OUT.wav --seed 0 [--ref-seconds 10]

Chatterbox uses the reference AUDIO only: --ref-text is accepted for the common command line and ignored. Only the
first 6 s feed the T3 prompt and the first 10 s the decoder (tts.py lines 106-108 and 182-206), so a longer clip is
wasted; --ref-seconds 10 is the useful ceiling.

Source for every call below: scratch/tts-audition/envs/chatterbox-en/Lib/site-packages/chatterbox/tts.py
(chatterbox-tts 0.1.7 from PyPI), line numbers in the comments.
Needs the GPU (device "cuda"); nothing here has been run on it yet.
"""

import re
import sys
from pathlib import Path

# This folder holds runners named after packages (omnivoice.py), so it must not shadow site-packages: take it off the
# front of sys.path (Python puts the script's folder there) and put it last, where only audition_common is found in it.
_HERE = Path(__file__).resolve().parent
sys.path[:] = [p for p in sys.path if Path(p or ".").resolve() != _HERE] + [str(_HERE)]
import audition_common as common  # noqa: E402  (sets HF_HUB_OFFLINE etc. and the cache folders; must precede torch)

ITEM = "chatterbox-en"
# Chatterbox's own defaults (tts.py generate(), lines 208-218): exaggeration 0.5, cfg_weight 0.5, temperature 0.8,
# repetition_penalty 1.2, min_p 0.05, top_p 1.0. Kept as they are so the audition hears the shipped behaviour.
GEN = dict(exaggeration=0.5, cfg_weight=0.5, temperature=0.8, repetition_penalty=1.2, min_p=0.05, top_p=1.0)
# generate() caps one call at max_new_tokens=1000 speech tokens (tts.py line 249), 25 tokens per second = 40 s, and the
# card advises a few sentences per call. Longer text is split at sentence ends into pieces of at most this many characters.
MAX_CHARS = 300
GAP_SECONDS = 0.20  # silence between pieces


def pieces(text: str) -> list[str]:
    sentences = re.split(r"(?<=[.!?])\s+", " ".join(text.split()))
    out, cur = [], ""
    for s in sentences:
        if cur and len(cur) + 1 + len(s) > MAX_CHARS:
            out.append(cur)
            cur = s
        else:
            cur = f"{cur} {s}".strip()
    if cur:
        out.append(cur)
    return out


def main() -> None:
    args = common.parse_args(__doc__.splitlines()[0])
    folder = common.external(ITEM)
    ref_path, ref_used = common.prepare_reference(args.ref_wav, args.ref_seconds)
    common.quiet_stdout()

    import numpy as np
    import torch
    from chatterbox.tts import ChatterboxTTS

    # tts.py lines 128-165: from_local(ckpt_dir, device) reads ve.safetensors, t3_cfg.safetensors, s3gen.safetensors,
    # tokenizer.json and conds.pt from the folder; no Hub call (from_pretrained, lines 167-180, is the one that downloads).
    t0 = common.timer()
    model = ChatterboxTTS.from_local(folder, "cuda")
    common.sync_cuda()
    load_s = common.timer() - t0

    common.seed_all(args.seed)
    t0 = common.timer()
    audio = []
    chunks = pieces(args.text)
    for i, chunk in enumerate(chunks):
        # tts.py lines 208-272. audio_prompt_path makes generate() call prepare_conditionals (lines 182-206), which loads
        # the clip with librosa at 24 kHz, takes the first 10 s for the decoder and the first 6 s for the T3 prompt.
        # The conditionals stay on the model, so only the first piece passes the path.
        wav = model.generate(chunk, audio_prompt_path=str(ref_path) if i == 0 else None, **GEN)  # (1, T) float32 CPU
        audio.append(wav.squeeze(0).numpy())
        if i + 1 < len(chunks):
            audio.append(np.zeros(int(GAP_SECONDS * model.sr), dtype=np.float32))
    common.sync_cuda()
    gen_s = common.timer() - t0

    sr = model.sr  # S3GEN_SR = 24000 (tts.py line 119)
    out_s = common.write_wav(args.out, np.concatenate(audio), sr)
    common.report("Chatterbox 0.5B", ITEM, args, ref_used, out_s, gen_s, sr, load_seconds=round(load_s, 1),
                  pieces=len(chunks), ref_text_used=False, ref_seconds_effective=round(min(ref_used, 10.0), 3), torch=torch.__version__)


if __name__ == "__main__":
    main()
