"""VoxCPM2 voice design: speak a fixed text in a voice described in plain words, no reference audio.

  <repo>/scratch/tts-audition/envs/voxcpm2/Scripts/python.exe -B scripts/tts/design/design_voxcpm2.py \
      --voices scripts/tts/design/voices-voxcpm2.json [--only M1,F3] --seed 0 --out-dir scratch/tts-design/run1

VoxCPM2 reads the description as ordinary text tokens in front of the words to speak: "(description)text" is the whole
interface. Nothing in the package parses, validates or rejects the description, so this runner checks it itself
(see check_description). Source for every call below: scratch/tts-audition/envs/voxcpm2/Lib/site-packages/voxcpm/
(voxcpm 2.0.3), core.py and model/voxcpm2.py, line numbers in the comments. Needs the GPU for a design run;
--validate-only loads nothing. Nothing here has been run on the GPU yet.
"""

import re
import sys

import design_common as dc  # first: sets the offline variables and cache folders, then puts audition/ last on sys.path

common = dc.common
ITEM = "voxcpm2"
# core.py _generate() lines 180-196 defaults, kept as shipped (the README example passes the same cfg_value and timesteps).
GEN = dict(cfg_value=2.0, inference_timesteps=10, max_len=4096, retry_badcase=True)
MAX_WORDS = 60  # a guess, not a documented limit: the makers' examples run 5-10 words; see prompting.md


def check_description(desc: str) -> None:
    """The rules this runner imposes, since the package imposes none."""
    if not desc.strip():
        raise ValueError("empty description")  # an empty one would send the bare text: an unconditioned voice
    if "(" in desc or ")" in desc:
        raise ValueError("a bracket inside the description would close the control group early")
    if "\n" in desc or "  " in desc:
        raise ValueError("line break or double space (core.py lines 241-242 would collapse them)")
    if re.search(r"\d", desc):
        raise ValueError("digits in the description (it is read as text; write numbers as words)")
    if not desc.lower().startswith("british accent"):
        raise ValueError("the description must lead with the accent (the owner's rule)")
    if len(desc.split()) > MAX_WORDS:
        raise ValueError(f"more than {MAX_WORDS} words")


def final_text(desc: str, design_text: str) -> str:
    # cli.py build_final_text lines 66-68, which `voxcpm design --control` uses: f"({control}){text}", no space between.
    return f"({desc.strip()}){design_text}"


def extra_args(p) -> None:
    p.add_argument("--cfg-value", type=float, default=GEN["cfg_value"], help="guidance scale (card: higher = closer to the description)")
    p.add_argument("--timesteps", type=int, default=GEN["inference_timesteps"], help="diffusion steps")


def main() -> int:
    args = dc.parse_args(__doc__.splitlines()[0], extra_args)
    design_text, descs, _doc = dc.load_voices(args.voices, args.only)
    bad = {}
    for k, d in descs.items():
        try:
            check_description(d)
        except ValueError as e:
            bad[k] = str(e)
    if bad:
        raise SystemExit("invalid descriptions:\n" + "\n".join(f"  {k}: {v}" for k, v in bad.items()))
    if args.validate_only:
        # import only: proves the package (and its tokenizer-free text path) imports on this machine; no model is built
        from voxcpm import VoxCPM  # noqa: F401
        for k, d in descs.items():
            print(f"{k}: {final_text(d, design_text)[:110]}...", file=sys.stderr)
        print(f"ok: {len(descs)} descriptions valid", file=sys.stderr)
        return 0

    folder = common.external(ITEM)
    common.quiet_stdout()
    from voxcpm import VoxCPM

    # core.py from_pretrained() lines 104-172: a directory path skips snapshot_download (line 153). load_denoiser=False keeps
    # ZipEnhancer (a ModelScope download) out; optimize=False skips torch.compile and the warm-up generation (core.py
    # lines 96-101; model/voxcpm2.py optimize() lines 273-296 needs triton, which Windows lacks). device="cuda" is
    # resolve_runtime_device's explicit form.
    t0 = common.timer()
    model = VoxCPM.from_pretrained(str(folder), load_denoiser=False, optimize=False, device="cuda")
    common.sync_cuda()
    common.log(f"model loaded in {common.timer() - t0:.1f} s")

    def design_one(_key, desc):
        # core.py _generate() lines 180-304 with no prompt_wav_path and no reference_wav_path: prompt_cache is None, so
        # model/voxcpm2.py _generate_with_prompt_cache() (lines 783-964) runs in "zero_shot" mode (lines 825-828) with the
        # whole string, description included, as text tokens (lines 836-843). There is NO seed argument in 2.0.3 (the
        # current README's seed=42 is a later release): randomness is torch.randn in the sampler (modules/locdit/
        # unified_cfm.py line 64) and the VAE decoder (modules/audiovae/audio_vae_v2.py line 169), both on the global
        # torch RNG, so seed_all() in run_voices governs it. normalize=False and denoise=False are the defaults, spelled out.
        # retry_badcase counts the description's tokens in target_text_length (line 845), so it widens the allowed length.
        text = final_text(desc, design_text)
        wav = model.generate(
            text=text, normalize=False, denoise=False,
            **{**GEN, "cfg_value": args.cfg_value, "inference_timesteps": args.timesteps},
        )  # 1-D float32 numpy at model.tts_model.sample_rate
        return wav, {"attributes": None, "cfg_value": args.cfg_value, "inference_timesteps": args.timesteps}

    # AudioVAE V2 output rate, 48000 (model/voxcpm2.py line 244)
    return dc.run_voices(args, descs, design_one, lambda: int(model.tts_model.sample_rate), "VoxCPM2")


if __name__ == "__main__":
    sys.exit(main())
