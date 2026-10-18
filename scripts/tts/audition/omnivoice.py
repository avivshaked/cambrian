"""OmniVoice (k2-fsa, Qwen3-0.6B based) zero-shot voice clone: the runner for item `omnivoice`.

  <repo>/scratch/tts-audition/envs/omnivoice/Scripts/python.exe -B scripts/tts/audition/omnivoice.py \
      --ref-wav REF.wav --ref-text "..." --text "..." --out OUT.wav --seed 0 [--ref-seconds 10]

The card recommends a 3-10 s reference; the code only warns above 20 s (models/omnivoice.py lines 799-806).
Source for every call below: scratch/tts-audition/envs/omnivoice/Lib/site-packages/omnivoice/models/omnivoice.py
(omnivoice 0.2.1 from PyPI), line numbers in the comments. Needs the GPU; nothing here has been run on it yet.
"""

import sys
from pathlib import Path

# This folder holds a runner named after the package it imports (this file is omnivoice.py), so it must not shadow
# site-packages: take it off the front of sys.path (Python puts the script's folder there) and put it last.
_HERE = Path(__file__).resolve().parent
sys.path[:] = [p for p in sys.path if Path(p or ".").resolve() != _HERE] + [str(_HERE)]
import audition_common as common  # noqa: E402  (sets HF_HUB_OFFLINE etc. and the cache folders; must precede torch)

ITEM = "omnivoice"


def _pop_instruct() -> str | None:
    """Take an optional `--instruct "british accent, male"` off the command line before the shared parser sees it.

    generate() accepts instruct together with ref_audio (lines 1029-1164), and _prepare_inference_inputs puts it in the
    style tokens ahead of the text and the reference audio (lines 1220-1258). Whether the model heeds it while cloning
    is not documented; without --instruct the style is "None", as in the clone audition."""
    if "--instruct" not in sys.argv:
        return None
    i = sys.argv.index("--instruct")
    value = sys.argv[i + 1]
    del sys.argv[i : i + 2]
    return value


def main() -> None:
    instruct = _pop_instruct()
    args = common.parse_args(__doc__.splitlines()[0])
    folder = common.external(ITEM)
    ref_path, ref_used = common.prepare_reference(args.ref_wav, args.ref_seconds)
    common.quiet_stdout()

    import torch
    from omnivoice import OmniVoice

    # models/omnivoice.py from_pretrained() lines 333-384: _resolve_model_path (lines 270-275) returns a directory as it
    # is, so there is no snapshot_download. The text tokenizer is read from the folder (line 350), the audio tokenizer from
    # <folder>/audio_tokenizer (lines 352-366; the eustlb/higgs-audio-v2-tokenizer fallback is only taken when that folder
    # is missing), and no ASR model is loaded unless load_asr=True (lines 379-380). device_map and dtype go to
    # PreTrainedModel.from_pretrained (line 347); float16 and "cuda:0" are the README's example.
    t0 = common.timer()
    model = OmniVoice.from_pretrained(str(folder), device_map="cuda:0", dtype=torch.float16)
    common.sync_cuda()
    load_s = common.timer() - t0

    common.seed_all(args.seed)
    t0 = common.timer()
    # generate() lines 584-720: ref_audio + ref_text make create_voice_clone_prompt (lines 729-835) build the prompt, so
    # Whisper (lines 809-813) is never loaded. The clip is read with soundfile (utils/audio.py load_waveform, lines 40-66),
    # silence is trimmed (preprocess_prompt=True default), and the transcript gets end punctuation added (line 826).
    # Generation settings are OmniVoiceGenerationConfig's defaults (lines 176-189): 32 steps, guidance 2.0, t_shift 0.1,
    # class_temperature 0.0; text estimated longer than 30 s is chunked at 15 s inside generate (lines 698-714).
    # normalize_text=False (default) keeps the optional WeTextProcessing out. language "en" -> _resolve_language (line 1055).
    audios = model.generate(
        text=args.text, language="en", ref_audio=str(ref_path), ref_text=args.ref_text, speed=1.0, instruct=instruct,
    )  # list of 1-D float32 arrays, one per text
    common.sync_cuda()
    gen_s = common.timer() - t0

    sr = int(model.sampling_rate)  # the audio tokenizer's feature extractor rate, 24000 (line 371)
    out_s = common.write_wav(args.out, audios[0], sr)
    common.report("OmniVoice", ITEM, args, ref_used, out_s, gen_s, sr, load_seconds=round(load_s, 1),
                  ref_text_used=True, instruct=instruct, torch=torch.__version__)


if __name__ == "__main__":
    main()
