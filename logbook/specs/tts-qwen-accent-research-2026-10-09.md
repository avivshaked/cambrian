# Qwen3-TTS clone accent loss: what others found (read 2026-10-09)

The Sonnet researcher's final report, saved word for word on 2026-10-09. Its claims are web readings through
a summarising fetch tool, and Reddit could not be searched.

---

## Verdict

This is a known, documented direction of failure, but I found no reported fix that worked, and no report of exactly your case.

- **Documented tendency.** Several third-party sources say Qwen3-TTS defaults to American (or Chinese-accented) English. They also say Qwen provides no accent control. None of these shows audio or numbers.
- **Qwen is silent on it.** The README, both model cards and the technical report say nothing about British or American English, and nothing about whether cloning can carry a rare accent. The only accent-like feature is Chinese dialects.
- **Nobody has reported your exact case.** That case is a designed British clip, cloned by Base 1.7B in ICL mode (reference transcript conditioning), losing the accent on the same words. I found no issue, discussion or post about it. I did not find a single reported setting that fixes it.
- **One piece of counter-evidence (inference).** An mlx-audio user reported that a British voice clone kept its accent in v0.3.0 (0.6B-Base 8-bit). That suggests the Base model can carry the accent, so something in your pipeline may be at fault. Treat that as a hypothesis, not a finding.
- **Your owner's finding.** The accent is already gone on the first clone take with identical text. That rules out drift over long text and unfamiliar words. It makes the clone step itself the place to test.

The search tools could not reach Reddit, so r/LocalLLaMA and r/TextToSpeech are unsearched. Search coverage was shallow (see the last section).

## Sources, with evidence level

**A. Accent loss, or accent default**

1. mlx-audio issue 439: https://github.com/Blaizzy/mlx-audio/issues/439 (opened 2026-01-26, closed 2026-01-31)
   - **Claim:** a British-English clone on mlx-community/Qwen3-TTS-12Hz-0.6B-Base-8bit "lost its accent and reverted to an American English accent" after two commits (PRs 435 and 444). Reverting to v0.3.0 restored it.
   - **Maintainer statement (Blaizzy):** "The stream decode was hardcoded to 25 tokens and that affected the voice." He also advised `streaming_interval` of 4, or no streaming. The fix in PR 445 only partly worked and PR 461 closed the issue.
   - **Evidence level:** reporter attached audio (zip files). Maintainer diagnosis. This is a bug in the MLX port's chunked audio decoding, not the PyTorch `qwen-tts` package.
   - **Relevance:** low for the mechanism, because `qwen-tts` decodes the reference codes plus the generated codes in one pass (`qwen3_tts_model.py` lines 612-627). It does show the accent is carried by the ICL pipeline in at least one port. I read this only through a summarising fetch tool, not the raw thread.

2. Qwen3-TTS issue 134, "When I clone a voice in another language keep original accent": https://github.com/QwenLM/Qwen3-TTS/issues/134
   - Opened 2026-01-29, closed "not planned" 2026-05-06. No maintainer reply.
   - **Claim:** English voices cloned into Italian come out with "a strong American accent". Comments report the same for fine-tuning on an African language (tobiusaolo, 3 hours of audio) and for German.
   - **chrisoutwright, 2026-02-19:** "only cloning voice in same language seems adequate in base 1.7b model."
   - **Evidence level:** anecdote, no audio in the thread.
   - **Relevance:** cross-language only. It shows Base falls back to an American-like accent for non-English targets. Your case is same-language.

3. Qwen3-TTS issue 323 (https://github.com/QwenLM/Qwen3-TTS/issues/323, 2026-05-24, closed): fine-tuning Base 1.7B on 7 hours of Bengali gave "Foreign/Hindi accent bias". I read only the title and a one-line summary. Anecdote. Peripheral.

4. koboldcpp discussion 2143: https://github.com/LostRuins/koboldcpp/discussions/2143
   - **Claim (commenter Michael-chipmates):** the model "tends to fall back to its dominant English distribution unless it's conditioned on a real example of the target language." Their fix was to clone from a 5-10 s native-speaker clip, and they said it works best with "genuinely native and natural-sounding" clips, "not TTS-generated samples".
   - **Evidence level:** anecdote or assertion, no test. Date not captured.
   - **Relevance:** the "not TTS-generated samples" point matters, because your reference is a synthetic VoiceDesign clip.

5. ocdevel guide, 2026-03-02: https://ocdevel.com/blog/20260302-qwen-tts-voice-cloning
   - **Claims:** "Accent is conspicuously absent from the official dimensions table." The model "was trained on data that skews heavily toward Chinese and standard American English." Specific accents (British, Australian, Southern US) default "toward standard American or Chinese-accented English." Cloning with real British or Australian reference audio is "far more reliable than text descriptions" (it advises 10-15 s clips).
   - **Evidence level:** assertion in a blog. It cites mlx-audio 439 and no tests of its own. The training-data claim has no source I could check.
   - **Relevance:** it supports the lean toward American English but is not evidence of a mechanism. It also implies a designed British clip is the weak input, though no source tested that.

6. mintlify.wiki Qwen3-TTS "languages" page: https://mintlify.wiki/QwenLM/Qwen3-TTS/concepts/languages
   - **Claim:** "models default to neutral/American accent" and British English is "supported but may sound slightly American-influenced."
   - **Evidence level:** unofficial, probably machine-generated documentation. Not Qwen's. Do not treat it as authoritative. It also recommends an explicit language over Auto.

**B. Qwen's own documentation**

7. README: https://raw.githubusercontent.com/QwenLM/Qwen3-TTS/main/README.md
   - Lists 10 languages "as well as multiple dialectal voice profiles". The only examples are Chinese: Dylan (Beijing) and Eric (Chengdu).
   - Describes Voice Design then Clone as useful for "a consistent character voice across many lines".
   - Says `x_vector_only_mode=True` keeps only the speaker embedding and "cloning quality may be reduced".
   - Mentions `language="Auto"`.
   - Says nothing about British or American English, about accents, or about the Base model's training data.
   - Maintainer statement level, but it is silence on your question.

8. Model cards for Base and VoiceDesign (https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-Base and https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign). I read them only as summaries from a fetch tool, not directly.
   - Same language list and dialect wording as the README. No training data composition. No accent guidance.
   - Base card: evaluation used bfloat16, `max_new_tokens=2048`, and the generate_config.json defaults.

9. Technical report, arXiv 2601.15621, submitted 2026-01-22: https://arxiv.org/abs/2601.15621. I read it via the HTML version, summarised by the fetch tool.
   - More than 5 million hours of multilingual data, in three pre-training stages.
   - Voice cloning by ICL, a text-speech pair that "better preserves prosody" (section 3.3).
   - Speaker similarity measured with a WavLM verification model (section 4.1.2). That metric is why 0.98 to 0.99 can coexist with a lost accent: it checks speaker identity, not accent.
   - A claim of more than 10 minutes of fluent long-form speech (section 4.2.6).
   - No discussion of accent, dialect, or British versus American English. No limitations section.

**C. Settings and the Voice Design route**

10. Qwen3-TTS issue 239 and PR 362
   - Issue: https://github.com/QwenLM/Qwen3-TTS/issues/239. PR: https://github.com/QwenLM/Qwen3-TTS/pull/362 (open, 2026-08-28).
   - **PR 362 claim:** `generate_voice_clone` defaults to `non_streaming_mode=False`, which only simulates streaming text input. In that mode the speaking rate climbs because text tokens enter faster than speech consumes them, and a long reference transcript removes the brake.
   - **Tested numbers in the PR (same ~150-word text, pinned parameters):**
     - Streaming mode, 12.9 s reference: +16.7% rate drift.
     - Streaming mode, 4 s reference: -2.3% drift.
     - Non-streaming mode: 0.0% drift.
   - Commenter sherifhanna700, 2026-08-28, proposed three workarounds:
     - Use non-streaming mode.
     - Keep streaming references under about 5 s.
     - Limit each call to 30 s of audio or less.
   - **Evidence level:** tested for speaking rate only. The PR contains nothing about accent or voice identity.
   - **Relevance:** your call uses `non_streaming_mode=False` with an 18-24 s reference, which is exactly the setup the PR says rushes the output. That is a documented defect of your configuration, though not an accent defect. It is the cheapest test, but there is no evidence it affects accent.
   - Local code (`modeling_qwen3_tts.py` lines 1968-2013) confirms the mechanism. With `non_streaming_mode=False`, ICL feeds reference text plus target text token-by-token against the reference codec frames. With True, all text goes in first and the reference codec frames follow.

11. Local code, language handling (`modeling_qwen3_tts.py` lines 2108-2150). I read this myself.
   - With `language="English"`, a think-token prefill (`codec_think`, `codec_think_bos`, language id, `codec_think_eos`) is inserted. With `"Auto"`, a `codec_nothink` prefill is used instead and no language id is set.
   - There is no British or American language id. Only `english`, plus Chinese dialect ids.
   - Inference: explicit English may push the model toward its default English distribution (American). Auto may leave the accent to the reference codes. This is a hypothesis from reading the code, untested and unreported anywhere.

12. Issue 4, Voice Design reuse (https://github.com/QwenLM/Qwen3-TTS/issues/4, 2026-01-23). Maintainer wangxiongts: refer to the README's voice design and clone examples, and sample VoiceDesign multiple times to choose a voice. Community answer (TanJie7): use the clip as a clone reference. Maintainer statement, partial.
   - HF VoiceDesign discussion 3, "Re-use a designed voice" (https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign/discussions/3, 2026-01-24): no maintainer answer. The asker concluded "Voice Design then Clone" was the route.
   - Together these indicate that a VoiceDesign voice changes on every generation, which is why Qwen recommends cloning it.

13. Narration consistency
   - The Medium article "High-Quality Long-Form TTS with Qwen3 Open-Weight Models" returned 403 and I could not read it. A search snippet claimed that batching all cues in one `generate_voice_design` call keeps timbre consistent, and that a fixed reference utterance was used to anchor the speaker x-vector. Both are unverified secondary summaries, anecdote level.
   - Technical report: 10+ minutes of fluent speech (section 4.2.6). It does not claim consistent voice across independent calls.

## Fixes, ranked by evidence

Almost nothing here is proven for accent. Order is by plausibility plus any evidence.

1. **`non_streaming_mode=True`.**
   - Evidence: tested for speaking-rate drift (PR 362), none for accent.
   - Cost: one argument, so an A/B on one reference clip. No re-design needed.
   - Likelihood (inference): a possible effect, since ICL conditioning is laid out differently. Still check it.

2. **`language="Auto"` instead of `"English"`.**
   - Evidence: none reported. Only the code reading (item 11) and a third-party wiki that recommends an explicit language for quality.
   - Cost: one argument. Low risk of mispronunciation, because the text is English.

3. **Clone from a real British recording** instead of a TTS-generated clip.
   - Evidence: assertion in two sources (ocdevel, the koboldcpp comment). Items 4 and 5 are about real native-speaker clips and cross-language cloning. No test of a synthetic British clip.
   - Cost: needs a licensed or recorded British clip. It breaks the "design a voice from text" flow, so it is the biggest change.

4. **Narrate directly with the VoiceDesign model.**
   - Evidence: none for accent and none for consistency. The maintainer view (item 12) is that design voices vary per generation, and the Medium consistency tip is unverified.
   - Cost: moderate. The service would need a design-direct path, a fixed seed, and a check of the 1.7B VoiceDesign output for accent and voice drift across paragraphs. It avoids the clone step, so it avoids the loss, but you lose reusability. (The Medium tip is to batch cues in one call; unverified.)

5. **`x_vector_only_mode=True`.**
   - Evidence: none. README says quality may be reduced.
   - Cost: one argument. Inference: this drops the reference codec frames, so it probably carries less accent. I expect it to be worse for accent, not better. It is useful as a control experiment.

6. **Sampling changes** (temperature, top_p). No source ties sampling to accent. Cost is low but there is no basis for expecting an effect.

7. **Fine-tuning Base on British speech.** Evidence: issue 134 and 323 show fine-tuning on small datasets still gave American or foreign accents. Cost: high. Last resort.

8. **Different checkpoint.** The 0.6B-Base is the only other Base model I saw reported. The mlx-audio British report used 0.6B-Base (8-bit), but there is no comparison against 1.7B. Cost: a download and a separate run. Untested.

## What I could not find

- Any report of Voice Design then Clone specifically losing British, Australian or other non-American accents in same-language ICL. No GitHub issue or HF discussion matches. The search tools may have missed it: GitHub search via the API turned up very little for "British", and the HF discussion lists are small and I read only their titles.
- Reddit (r/LocalLLaMA, r/TextToSpeech): the search tool refused it. Not covered.
- Any Qwen statement on what accents the Base model saw, or whether cloning can carry a rare accent. The report has 5 million hours total but no accent breakdown.
- Any evidence that `language="Auto"`, `non_streaming_mode=True`, temperature or top_p affect accent preservation. I found no test of any of them for accent.
- Any independent test of VoiceDesign narration consistency across many paragraphs, or any VoiceDesign accent instruction test.
- Details of two items I could only skim via summaries: the arXiv report body and the HF model cards. A direct read of the report's section 3.3 and 4.2 could be done if needed.
- The Medium long-form article (403), the schmelczer.dev `synth.py` (403), and anything I did not search beyond the first results.
- The Australian drift at design time (your second voice): no source mentions it.

I wrote no files, installed nothing and used no GPU. I stopped one background `find` that I had started in error. The local package code I read is in the narration service's repository, under `workers/qwen3tts/.venv/Lib/site-packages/qwen_tts/` (inference/qwen3_tts_model.py and core/models/modeling_qwen3_tts.py). I did not read the service design doc beyond a grep for "accent", which found only a note on line 508 that accent is among the things the numbers miss.
