"""CosyVoice 3 (Fun-CosyVoice3-0.5B-2512) zero-shot clone: the runner for item `cosyvoice3`.

  <repo>/scratch/tts-audition/envs/cosyvoice3/Scripts/python.exe -B scripts/tts/audition/cosyvoice3.py \
      --ref-wav REF.wav --ref-text "..." --text "..." --out OUT.wav --seed 0 [--ref-seconds 10]

The code is the git checkout scratch/tts-audition/src/CosyVoice at 074ca6dc9e80 (it is not a pip package), with its
Matcha-TTS submodule at dd9105b34bf2. Source for every call below: that checkout, cosyvoice/cli/cosyvoice.py,
cosyvoice/cli/frontend.py and cosyvoice/utils/file_utils.py, line numbers in the comments.
Needs the GPU; nothing here has been run on it yet.
"""

import sys
from pathlib import Path

# This folder holds runners named like packages, so it must not shadow site-packages: take it off the front of
# sys.path (Python puts the script's folder there) and put it last, where only audition_common is found in it.
_HERE = Path(__file__).resolve().parent
sys.path[:] = [p for p in sys.path if Path(p or ".").resolve() != _HERE] + [str(_HERE)]
import audition_common as common  # noqa: E402  (sets HF_HUB_OFFLINE etc. and the cache folders; must precede torch)

ITEM = "cosyvoice3"
COSYVOICE = common.AUDITION / "src" / "CosyVoice"
MAX_REF_SECONDS = 30.0  # frontend.py line 97: assert speech.shape[1] / 16000 <= 30
# CosyVoice 3's prompt format (README): the reference transcript follows this prefix.
PROMPT_PREFIX = "You are a helpful assistant.<|endofprompt|>"


def main() -> None:
    args = common.parse_args(__doc__.splitlines()[0])
    folder = common.external(ITEM)
    ref_path, ref_used = common.prepare_reference(args.ref_wav, args.ref_seconds, max_seconds=MAX_REF_SECONDS)
    common.quiet_stdout()

    # The checkout is imported as source: cosyvoice/ at its root and matcha/ under third_party/Matcha-TTS.
    sys.path[:0] = [str(COSYVOICE), str(COSYVOICE / "third_party" / "Matcha-TTS")]
    import torch
    from cosyvoice.cli.cosyvoice import AutoModel

    # cosyvoice.py AutoModel() lines 228-237: the folder exists, so no ModelScope snapshot_download; cosyvoice3.yaml is
    # found, so CosyVoice3 (lines 189-226) is built. It reads cosyvoice3.yaml with qwen_pretrain_path overridden to
    # <folder>/CosyVoice-BlankEN (line 200), campplus.onnx and speech_tokenizer_v3.onnx (lines 204-205), then llm.pt,
    # flow.pt and hift.pt with torch.load(weights_only=True) (model.py lines 65-71). fp16=False is the default; True is an
    # option on the 4090 but changes the numbers. spk2info.pt is optional (frontend.py lines 49-52). The speech-tokenizer
    # ONNX session asks for CUDAExecutionProvider when torch sees CUDA (frontend.py lines 46-48); the plain onnxruntime
    # wheel has no such provider and falls back to CPU with a warning (checked with a tiny model on this machine).
    t0 = common.timer()
    model = AutoModel(model_dir=str(folder), fp16=False)
    common.sync_cuda()
    load_s = common.timer() - t0

    common.seed_all(args.seed)
    t0 = common.timer()
    # cosyvoice.py inference_zero_shot() lines 91-103: (tts_text, prompt_text, prompt_wav, zero_shot_spk_id='', stream=False,
    # speed=1.0, text_frontend=True). With zero_shot_spk_id empty, frontend_zero_shot (frontend.py lines 168-189) extracts
    # speech tokens, mel features and the CAMPPlus speaker embedding from the clip (load_wav, file_utils.py lines 44-50,
    # uses torchaudio.load(backend='soundfile'), so no sox or ffmpeg). text_normalize (frontend.py lines 127-160) splits the
    # text in pieces of <= 80 tokens; wetext is not installed, so English passes through inflect number spelling only. The
    # generator yields one dict per piece; the pieces are joined in order.
    pieces = [
        out["tts_speech"]  # (1, T) float32 tensor at model.sample_rate
        for out in model.inference_zero_shot(
            args.text, PROMPT_PREFIX + args.ref_text, str(ref_path), stream=False, speed=1.0)
    ]
    common.sync_cuda()
    gen_s = common.timer() - t0

    sr = int(model.sample_rate)  # 24000 for CosyVoice 3 (configs["sample_rate"], cosyvoice.py line 208)
    out_s = common.write_wav(args.out, torch.cat(pieces, dim=1), sr)
    common.report("CosyVoice 3 0.5B", ITEM, args, ref_used, out_s, gen_s, sr, load_seconds=round(load_s, 1),
                  pieces=len(pieces), ref_text_used=True, torch=torch.__version__)


if __name__ == "__main__":
    main()
