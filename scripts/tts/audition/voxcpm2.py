"""VoxCPM2 (2B) "ultimate cloning" (reference clip plus its exact transcript): the runner for item `voxcpm2`.

  <repo>/scratch/tts-audition/envs/voxcpm2/Scripts/python.exe -B scripts/tts/audition/voxcpm2.py \
      --ref-wav REF.wav --ref-text "..." --text "..." --out OUT.wav --seed 0 [--ref-seconds 10]

Source for every call below: scratch/tts-audition/envs/voxcpm2/Lib/site-packages/voxcpm/ (voxcpm 2.0.3 from PyPI),
core.py and model/voxcpm2.py, line numbers in the comments. Needs the GPU; nothing here has been run on it yet.
"""

import sys
from pathlib import Path

# This folder holds runners named after packages (omnivoice.py), so it must not shadow site-packages: take it off the
# front of sys.path (Python puts the script's folder there) and put it last, where only audition_common is found in it.
_HERE = Path(__file__).resolve().parent
sys.path[:] = [p for p in sys.path if Path(p or ".").resolve() != _HERE] + [str(_HERE)]
import audition_common as common  # noqa: E402  (sets HF_HUB_OFFLINE etc. and the cache folders; must precede torch)

ITEM = "voxcpm2"
# core.py _generate() lines 180-196 defaults: cfg_value 2.0, inference_timesteps 10, min_len 2, max_len 4096,
# retry_badcase True (3 tries, ratio 6.0). Kept as shipped.
GEN = dict(cfg_value=2.0, inference_timesteps=10, max_len=4096, retry_badcase=True)


def main() -> None:
    args = common.parse_args(__doc__.splitlines()[0])
    folder = common.external(ITEM)
    ref_path, ref_used = common.prepare_reference(args.ref_wav, args.ref_seconds)
    common.quiet_stdout()

    import torch
    from voxcpm import VoxCPM

    # core.py from_pretrained() lines 104-172: a directory path skips snapshot_download (line 153). load_denoiser=False keeps
    # ZipEnhancer (a ModelScope download, lines 90-93) out; optimize=False skips torch.compile and the warm-up generation
    # (lines 96-101; model/voxcpm2.py optimize() lines 273-296 needs triton, which Windows lacks). device="cuda" is
    # resolve_runtime_device's explicit form (model/voxcpm2.py line 167); "cuda:0" would also work but optimize() tests == "cuda".
    # model/voxcpm2.py from_local() lines 1111-1180 reads config.json, tokenizer.json (LlamaTokenizerFast), audiovae.pth
    # (torch.load weights_only=True) and model.safetensors from the folder.
    t0 = common.timer()
    model = VoxCPM.from_pretrained(str(folder), load_denoiser=False, optimize=False, device="cuda")
    common.sync_cuda()
    load_s = common.timer() - t0

    common.seed_all(args.seed)
    t0 = common.timer()
    # core.py _generate() lines 180-304. The model card's "ultimate cloning" passes the same clip as reference_wav_path
    # (isolated reference tokens) and as prompt_wav_path + prompt_text (continuation); build_prompt_cache is
    # model/voxcpm2.py lines 681-740 (mode "ref_continuation"). Both are read with librosa at 16 kHz (line 412).
    # normalize=False and denoise=False are the defaults, spelled out: both would reach for ModelScope models.
    wav = model.generate(
        text=args.text, prompt_wav_path=str(ref_path), prompt_text=args.ref_text, reference_wav_path=str(ref_path),
        normalize=False, denoise=False, **GEN,
    )  # 1-D float32 numpy at model.tts_model.sample_rate
    common.sync_cuda()
    gen_s = common.timer() - t0

    sr = int(model.tts_model.sample_rate)  # AudioVAE V2 output rate, 48000 (model/voxcpm2.py line 244)
    out_s = common.write_wav(args.out, wav, sr)
    common.report("VoxCPM2", ITEM, args, ref_used, out_s, gen_s, sr, load_seconds=round(load_s, 1),
                  ref_text_used=True, torch=torch.__version__)


if __name__ == "__main__":
    main()
