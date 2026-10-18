"""OmniVoice voice design: speak a fixed text in voices built from attribute lists, no reference audio.

  <repo>/scratch/tts-audition/envs/omnivoice/Scripts/python.exe -B scripts/tts/design/design_omnivoice.py \
      --voices scripts/tts/design/voices-omnivoice.json [--only M1,F3] --seed 0 --out-dir scratch/tts-design/run1

OmniVoice takes a fixed attribute list, not free text (gender, age, pitch, whisper, one English accent), joined by ", ".
Every item is checked by the package's own _resolve_instruct before the model runs; a word outside the list raises.
Source for every call below: scratch/tts-audition/envs/omnivoice/Lib/site-packages/omnivoice/ (omnivoice 0.2.1),
line numbers in the comments. Needs the GPU for a design run; --validate-only does not load the model or touch it.
Nothing here has been run on the GPU yet.
"""

import re
import sys

import design_common as dc  # first: sets the offline variables and cache folders, then puts audition/ last on sys.path

common = dc.common
ITEM = "omnivoice"

# utils/voice_design.py lines 31-76: the categories, in the order the package lists them. "maker order" below follows it
# (gender, age, pitch, whisper, accent), which is also the order of the makers' own examples ("male, elderly, low
# pitch, whisper"; "female, low pitch, british accent" has accent last).
_GENDER = {"male", "female"}
_AGE = {"child", "teenager", "young adult", "middle-aged", "elderly"}
_PITCH = {"very low pitch", "low pitch", "moderate pitch", "high pitch", "very high pitch"}
_STYLE = {"whisper"}


def maker_order(instruct: str) -> str:
    """Reorder items to gender, age, pitch, style, accent. Only used with --order maker."""
    rank = lambda s: 0 if s in _GENDER else 1 if s in _AGE else 2 if s in _PITCH else 3 if s in _STYLE else 4  # noqa: E731
    items = [x for x in re.split(r"\s*,\s*", instruct.strip().lower()) if x]
    return ", ".join(sorted(items, key=rank))  # sorted is stable: nothing else changes order


def validate(descs: dict, order: str) -> dict:
    """Run each description through the model's own validator. Returns {key: normalised instruct string to send}."""
    # models/omnivoice.py _resolve_instruct lines 1492-1621 (called from _preprocess_all, lines 1064-1069): splits on
    # commas, lower-cases, REJECTS (ValueError) any item not in utils/voice_design.py's list, rejects two items of one
    # category, and joins what is left with ", ". It never drops or maps an English item silently.
    from omnivoice.models.omnivoice import _resolve_instruct

    out, bad = {}, {}
    for key, d in descs.items():
        try:
            sent = maker_order(d) if order == "maker" else d
            out[key] = _resolve_instruct(sent, use_zh=False)
            if out[key] is None:
                raise ValueError("empty instruct")
            if "british accent" not in out[key]:
                raise ValueError("no 'british accent' item")
        except ValueError as e:
            bad[key] = str(e)
    if bad:
        raise SystemExit("invalid descriptions:\n" + "\n".join(f"  {k}: {v}" for k, v in bad.items()))
    return out


def extra_args(p) -> None:
    p.add_argument("--order", choices=("owner", "maker"), default="owner",
                   help="owner: send the items as written in the file (accent first, the owner's rule); "
                        "maker: reorder to gender, age, pitch, style, accent as the makers' examples do")


def main() -> int:
    args = dc.parse_args(__doc__.splitlines()[0], extra_args)
    design_text, descs, doc = dc.load_voices(args.voices, args.only)
    sends = validate(descs, args.order)  # also proves the package imports on this machine
    attributes = doc.get("attributes", {})
    if args.validate_only:
        for k, s in sends.items():
            print(f"{k}: {s}", file=sys.stderr)
        print(f"ok: {len(sends)} descriptions valid", file=sys.stderr)
        return 0

    folder = common.external(ITEM)
    common.quiet_stdout()
    import torch
    from omnivoice import OmniVoice

    # from_pretrained() lines 333-384: a directory is used as it is (_resolve_model_path, lines 270-275); text tokenizer
    # from the folder (line 350), audio tokenizer from <folder>/audio_tokenizer (lines 352-366). float16 and "cuda:0"
    # are the README's example.
    t0 = common.timer()
    model = OmniVoice.from_pretrained(str(folder), device_map="cuda:0", dtype=torch.float16)
    common.sync_cuda()
    common.log(f"model loaded in {common.timer() - t0:.1f} s")

    def design_one(key, _desc):
        # generate() lines 584-720 with no ref_audio and no ref_text: voice-design mode (docstring lines 613-614).
        # instruct is validated again inside (line 1069) and goes into the prompt as
        # <|instruct_start|>...<|instruct_end|> (_prepare_inference_inputs, lines 1226-1228). language "en" is resolved by
        # _resolve_language (line 1472) and is what lets the accent item apply (voice_design.md: accents are English-only).
        # Generation settings stay OmniVoiceGenerationConfig's defaults (lines 176-189). Randomness: _gumbel_sample
        # (line 1632) uses torch.rand_like on the device, so seed_all() in run_voices governs it.
        audios = model.generate(text=design_text, language="en", instruct=sends[key])
        info = {"instruct_sent": sends[key], "order": args.order, "attributes": attributes.get(key)}
        return audios[0], info

    # sampling_rate: set from the audio tokenizer's feature extractor (line 371), 24000.
    return dc.run_voices(args, descs, design_one, lambda: int(model.sampling_rate), "OmniVoice")


if __name__ == "__main__":
    sys.exit(main())
