# Prompting the two voice designers (2026-10-09)

What each model accepts as a voice description, how its code turns that into model input, what its makers advise,
and the format chosen for the 20-voice test. Read from the installed packages (omnivoice 0.2.1, voxcpm 2.0.3) and from
the makers' pages (fetched 2026-10-09; treated as data). Nothing has run on the GPU. KNOWN means read in code or in a
maker's text; BELIEVE means inferred; UNKNOWN means not established.

The owner's rule for the descriptions: lead with the accent, then the voice register, then the age, then the description
of the voice; keep the accent wording simple. Both sets follow it, with one exception forced by OmniVoice (below).

## OmniVoice (k2-fsa)

**1. The input is a fixed attribute list, not free text.** KNOWN (`omnivoice/utils/voice_design.py` lines 31-76,
docs/voice-design.md):

| Category | Values |
|---|---|
| gender | `male`, `female` |
| age | `child`, `teenager`, `young adult`, `middle-aged`, `elderly` |
| pitch | `very low pitch`, `low pitch`, `moderate pitch`, `high pitch`, `very high pitch` |
| style | `whisper` |
| English accent | `american accent`, `british accent`, `australian accent`, `canadian accent`, `indian accent`, `chinese accent`, `korean accent`, `portuguese accent`, `russian accent`, `japanese accent` |
| Chinese dialect | twelve, ending in 话 (Chinese speech only) |

British English is `british accent`, lower-case with a space (matching is case-insensitive). There is no Received
Pronunciation value and no regional British value. Items are separated by `, `. At most one item per category. No length
limit exists in the code beyond the number of categories. The makers' examples: `female, low pitch, british accent`;
`male, elderly, low pitch, whisper`; CLI `male, British accent`.

**2. Code path.** `generate(instruct=...)` (`models/omnivoice.py` line 599) calls `_preprocess_all` (lines 1064-1069),
which calls `_resolve_instruct` (lines 1492-1621). It splits on commas, lower-cases, and raises `ValueError` for any item
outside the list above (with a "did you mean" hint); it raises on two items of one category and on mixing a dialect with
an accent. Nothing is silently dropped or mapped for English (`baritone`, `contralto`, `a man in his sixties`, "warm",
"hushed" are all rejected; run on 2026-10-09 with the card hidden). The surviving string goes into the prompt as
`<|instruct_start|>...<|instruct_end|>` (`_prepare_inference_inputs`, lines 1226-1228). The accent only has an effect
if the text is English: the call also passes `language="en"`. `_resolve_instruct` is importable without loading the
model, which is how the voices were checked.

**3. What the makers say.** "Combine freely across categories"; "Omit attributes you don't care about — the model fills
in the rest"; "Due to training data limitations, some attribute combinations may not work well — the model may ignore
certain attributes" (docs/voice-design.md). The README adds that the model "is primarily trained on the voice cloning
task, so voice cloning is the most stable mode" and that voice design is trained on Chinese and English only. Nothing is
said about order, so order is UNKNOWN. The makers' examples put gender first and accent last.

**4. Seeds.** The only randomness in generation is `torch.rand_like` in `_gumbel_sample` (line 1632), used because
`position_temperature` defaults to 5.0 (line 183); `class_temperature` is 0.0, so token choice is greedy. A fixed
`torch.manual_seed` / `cuda.manual_seed_all` should give the same voice on the same machine (BELIEVE: bitwise
repeatability on the GPU was not run; float16 kernels can differ between runs). With no seed, the same attribute list
gives a different speaker each time (BELIEVE, from the sampling).

**Format chosen.** `british accent, <pitch>, <age>, <gender>`: accent first as the owner rules, then register, then age.
Because the list has no register names (`baritone`, `alto`) and no character words, the owner's register maps to pitch
(bass/deep baritone to `very low pitch`, baritone to `low pitch`, light baritone to `moderate pitch`, tenor to
`high pitch`; contralto, alto, mezzo and soprano likewise), and age maps to `middle-aged` (forties-fifties) or
`elderly` (sixties-seventies). The "description of the voice" step does not exist for this model. Ten voices per gender
are ten distinct combinations: four pitches by two ages, plus two with the age left out (the model chooses). Gender is
last, not first, to keep the accent first; the runner's `--order maker` re-sorts to gender, age, pitch, style, accent as
the makers' examples do, so the two orders can be compared on the GPU. `very high pitch`, `whisper` and `child`,
`teenager` and `young adult` are unused (a narrator in the forties to seventies). Different speakers inside one
combination would come from different seeds; the runner takes one seed per run and seeds each voice alike.

## VoxCPM2 (OpenBMB)

**1. The input is free text.** KNOWN (model card, README, docs site): "put the description in parentheses at the start
of `text`", for example `"(A young woman, gentle and sweet voice)Hello, welcome to VoxCPM2!"`. The docs site says the
instruction may be in Chinese or English. Described content: "gender, age, tone, emotion, pace". No attribute names, no
accent value, no documented length limit. The CLI example is `Young female voice, warm and gentle, slightly smiling`.
Accent and dialect handling for voice design is not documented anywhere I could reach, so a British accent is a request
the model may or may not honour (UNKNOWN).

**2. Code path.** Nothing in the package parses the description. `voxcpm design --control` builds
`f"({control}){text}"` (`cli.py` lines 66-68). `VoxCPM.generate` (`core.py` line 173) collapses line breaks and runs of
whitespace (lines 241-242), then, with no reference audio, `_generate_with_prompt_cache` (`model/voxcpm2.py` lines
783-964) runs in "zero_shot" mode (line 826) and tokenises the whole string, description included, as ordinary text
tokens (lines 836-843). The model learned during training to treat the bracketed prefix as control. Consequences
(KNOWN from code): the description counts toward `max_len` and the bad-case ratio (lines 845, 921), so a long description
loosens the length limit; a bracket inside the description would end it early; digits are not normalised
(`normalize=False`), so write numbers as words. The description being spoken aloud is a possible failure (BELIEVE, not
documented; the makers' examples are short, 5-10 words).

**3. What the makers say.** "Voice Design and Controllable Voice Cloning results can vary between runs — you may try to
generate 1~3 times to obtain the desired voice" (card and README). `cfg_value`: higher follows the description more
closely but may sound worse; `inference_timesteps`: higher is better and slower. With the "Hi-Fi" clone mode on, the
instruction is ignored (not our mode). Nothing on order, length or what to leave out.

**4. Seeds.** Installed 2.0.3 has no `seed` argument: the current README's `generate(..., seed=42)` is a later release,
and passing it to 2.0.3 would raise `TypeError`. The noise comes from `torch.randn` in the sampler
(`modules/locdit/unified_cfm.py` line 64) and the decoder (`modules/audiovae/audio_vae_v2.py` line 169), both on the
global torch RNG, so `torch.manual_seed` before each call should repeat a voice on the same machine (BELIEVE; not run).
The bad-case retry draws more noise, but deterministically.

**Format chosen.** `(British accent, <register> voice, a man/woman in his/her <decade>, <character>, calm and
unhurried, quietly authoritative, with a subtle sense of wonder)` followed directly by the design text. Accent first,
then register, then age, then character, as the owner rules, in the makers' comma style. Accent is the owner's plain
`British accent`, not a Received Pronunciation phrase; if the GPU run shows a non-British voice, the next try is
`British Received Pronunciation accent` (UNKNOWN whether that helps). The tail is the delivery brief of the owner's
earlier descriptions cut to one clause, because the originals (about 90 words) are far longer than anything the makers
show and risk being read aloud. Register, decade and character for the 20 voices are the owner's 08b set unchanged, so a
listener can compare each slot with its Qwen3-TTS twin. The runner refuses a description with a bracket, digit, double
space, more than 60 words (a guess, not a documented limit) or no leading `British accent`.

## What neither page settles

- Whether `british accent` (OmniVoice) or `British accent` (VoxCPM2) yields Received Pronunciation rather than a generic
  British voice. OmniVoice has no finer value. Judge by ear.
- Whether order matters to either model.
- Whether either model repeats a voice bit for bit on the GPU.
- Whether VoxCPM2 speaks any of its description aloud.
