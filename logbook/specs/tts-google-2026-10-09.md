# Google's text-to-speech models (read 2026-10-09)

This is the Sonnet researcher's report, saved word for word on 2026-10-09. It was asked for when the owner
considered Google TTS for the round 57 narrator. It reads Google's own documentation through a summarising
fetch tool. Nothing was called, signed up for or sent. Confirm prices and terms on Google's own pages
before relying on them.

---

## Summary

All pages were read on 2026-10-09. Nothing was called, signed up for or sent. I wrote no notes.

**Best fit: Gemini 3.8 Flash TTS (`gemini-3.8-flash-tts`, GA) on the paid Gemini API, with a voice made by Voice design.** It is the only Google option that takes a description of the narrator and keeps that voice for a whole film.
- Google's Voice design page gives a near-identical example persona: "A warm, thoughtful astronomer in his late 60s with a gentle British accent, speaking with quiet wonder." (https://ai.google.dev/gemini-api/docs/voice-design)
- The page says to fix age, timbre and regional accent in the voice description, and to keep `speech_metadata.style` for per-line emotion only. It also says a 1-2 sentence description gives "cleaner, more consistent results".
- The result is a stored `voice_...` ID, and you get a sample clip back to audition. Voice design is not a clone of anyone's voice, so the UK block on voice replication does not apply to it.
- Google's launch post claims "minimal speaker drift" across hours of audio and #1 on Hume's accent-modelling score. These are Google's own claims; I found no independent test of a 7-minute British read.
- There is no word or sentence timestamp feature. Plan to get cue times by aligning the audio yourself.
- Runner-up: Gemini 3.8 Flash-Lite TTS. It has the same voice design at a lower price and a lower leaderboard rank.
- The Cloud Chirp 3 HD en-GB voices are cheap and steady, but they are fixed named voices you cannot describe. None is documented as older or deep.

**Data and terms.** On the free tier Google trains on your text and humans may read it. On the paid tier it does not train on it. Send the script only on a billing-enabled project.

**Not yet audited.** None of this has been listened to: whether the result is modern RP, older and deep, and holds over 25 to 30 passages of 300 characters is untested. It needs an audition of a few passages through AI Studio's Voice design studio, and that sends text to Google, so it needs your approval first.

## Table of model and voice families

| Family | British accent (how) | Voice cloning | Style control | Max length per request | Timestamps | Status | Price |
|---|---|---|---|---|---|---|---|
| Gemini 3.8 Flash TTS `gemini-3.8-flash-tts` (Gemini API / AI Studio) | Voice design by prompt (documented British example). Extended Voice Library has an `accent` filter that accepts "British". | Voice replication from a 10-30 s reference plus a consent clip. Blocked in the UK, EEA, Switzerland, India, Illinois and Texas (see Terms). | `speech_metadata.style` per turn; inline tags such as `<short pause>`, `<laugh>`, `<sigh>`; 2 speakers. | Input 8,192 tokens, output 16,384 tokens (model page). Words per request not documented. | None documented | Stable/GA, 22-23 Sep 2026 | Paid: input $0.50 per 1M text tokens, output $9.00 per 1M audio tokens until 31 Dec 2026; $1.00 and $18.00 from 1 Jan 2027. Free tier exists. |
| Gemini 3.8 Flash-Lite TTS `gemini-3.8-flash-lite-tts` | Same as above | Same as above | Same as above | Not separately documented | None documented | Stable/GA, 22-23 Sep 2026 | Input $0.50, output $6.00 (to 31 Dec 2026); $1.00 and $12.00 from 1 Jan 2027 |
| Gemini 3.1 Flash TTS `gemini-3.1-flash-tts-preview` | Prebuilt voices plus prompt, no custom-voice features | No | Style field and tags; 2 speakers | Not documented | None documented | Preview, labelled legacy; Google recommends moving to 3.8 | $1.00 input, $20.00 output |
| Gemini 2.5 Flash/Pro TTS `gemini-2.5-*-preview-tts` | Prompt only | No | Prompt, 2 speakers | Not documented | None documented | Preview | Flash $0.50 input, $10.00 output. Pro: I found the $1.00 input and $20.00 output figure only for Cloud TTS, via a search snippet. |
| Gemini-TTS in Cloud Text-to-Speech (Cloud TTS API, 3.1 Flash / 2.5 Flash / 2.5 Flash Lite / 2.5 Pro) | Prompt; en-GB listed in Preview; no accent example on the page | No | Prompt field: style, accent, pace, tone; tags `[sigh]`, `[laughing]`, `[whispering]` | Text 4,000 bytes, prompt 4,000 bytes, 8,000 combined; about 655 s of audio | Not documented for these models | Mixed, mostly Preview | Search snippets: 2.5 Flash $0.50 and $10; 3.1 Flash $1 and $20; 2.5 Pro $1 and $20 per 1M tokens. The Cloud pricing page would not render for me. |
| Chirp 3: HD (Cloud TTS) | en-GB locale: 30 named voices (Achernar, Charon, Fenrir, Algieba, Orus, Schedar and others, each marked M or F). No accent prompt. | Instant Custom Voice (below) | SSML (`<say-as>`, `<phoneme>`, `<break>`, `<prosody>`, `<voice>`), pace 0.25x-2.0x, `[pause short]`, `[pause long]`, `[pause]`, IPA custom pronunciation. SSML is not available when streaming. | Not found for batch | `<mark>` timepoints exist in Cloud TTS, but the docs do not say which voices support them | GA | $30 per 1M characters (search snippet of Google's pricing page) |
| Chirp 3 Instant Custom Voice | Via the reference voice; en-GB is a supported language | Yes. One reference clip of up to 10 s, plus a spoken consent statement. Access is allowlist only, through Google sales. | Pause tags, pace, custom pronunciation | Same as Chirp 3 HD | Not documented | Allowlisted | $60 per 1M characters (search snippet) |
| Chirp-HD (older), Studio, Neural2, WaveNet, News, Standard en-GB (Cloud TTS) | Fixed named voices: Chirp-HD D, F, O; Studio B and C; Neural2 A, B, C, D, F, N, O; WaveNet A, B, C, D, F, N, O | No | SSML | Not found | `<mark>` timepoints exist, voice support not stated | GA | Not read. I did not find a first-party number. |

The en-GB voice names come from a small-model summary of the Cloud voices page. Only the first 100,000 of 259,000 characters were read, and the list had small inconsistencies (for example, the News and WaveNet counts do not match the letters listed). Treat the exact letters as approximate.

## Data and terms, product by product

**1. Gemini API, free tier or Google AI Studio unpaid use** (https://ai.google.dev/gemini-api/terms, https://ai.google.dev/gemini-api/docs/pricing)
- Google uses your content and the generated responses "to provide, improve, and develop Google products". The pricing page says the same under "Data use".
- Human reviewers "may read, annotate, and process your API input and output". Google also says not to submit sensitive, confidential or personal information.
- The same applies to any reference audio you upload.
- **Meaning for a film script:** do not send it on the free tier. This includes auditioning in the AI Studio Voice design studio unless it is on a billing-enabled project (I am not sure how AI Studio treats billing there). The script, once sent, is available to Google for product development and human review.

**2. Gemini API, paid tier (billing-enabled)** (same terms, plus https://ai.google.dev/gemini-api/docs/logs-policy)
- Google "doesn't use your prompts... or responses to improve our products".
- Prompts and responses are logged briefly, solely to detect and prevent Prohibited Use Policy violations, and kept as the law requires. No exact abuse-monitoring period was stated.
- The optional logs feature (billing-enabled projects only) keeps logs 7, 14, 28 or 55 days (default 55). These logs are not used for product improvement unless you opt in to sharing a dataset.
- **Meaning:** script text is kept for a limited time but not used for training. This is acceptable if you accept that Google briefly holds the text.

**3. Vertex AI / Gemini Enterprise Agent Platform** (https://docs.cloud.google.com/vertex-ai/generative-ai/docs/data-governance)
- Google "won't use your data to train or fine-tune any AI/ML models without your prior permission or instruction".
- Zero data retention needs you to turn off request-response logging (off by default), set `store = false`, disable the in-memory cache (24-hour TTL, on by default), and possibly request an abuse-monitoring exception.
- Availability of the 3.8 TTS models here is unclear. The Cloud TTS Gemini-TTS page says the 3.8 models with voice design and voice replication "are available in Preview through the Gemini Enterprise API only" and not in the Cloud TTS API. A search snippet suggests Vertex availability, but I did not confirm it. The Google blog says Gemini Enterprise is "coming soon".

**4. Cloud Text-to-Speech, including Chirp 3 HD and Instant Custom Voice** (https://docs.cloud.google.com/text-to-speech/docs/data-logging)
- "Google does not log any customer Cloud TTS text or audio data." The service is described as stateless, and customer text and audio are not kept for training or product improvement.
- I did not read the Cloud service terms for TTS. The Service Specific Terms page, first 100,000 of 244,000 characters, had no TTS section.
- **Meaning:** this is the most private route, but it has no controllable British narrator except the fixed Chirp 3 HD voices or an allowlisted Instant Custom Voice.

**Commercial use and YouTube**
- The Gemini API terms say "Google won't claim ownership over that content", and that Google "may generate the same or similar content for others". You are responsible for how you use it, and the law may require attribution.
- The terms do not mention a YouTube or monetisation restriction. They bar using the Services to build competing models.
- The terms do not require you to disclose that audio is AI-generated.
- Google's Generative AI Prohibited Use Policy (https://policies.google.com/terms/generative-ai/use-policy) bars impersonating a real person without disclosure in order to deceive, and misrepresenting generated content as made solely by a human in order to deceive. It does not name voice cloning.
- Inference, not read from a source: YouTube has its own altered/synthetic-content disclosure rules, which I did not check. A synthetic narrator that sounds realistic may need that label. Check YouTube's policy separately.

**Watermarks**
- Google's blog says generated audio carries an imperceptible SynthID watermark. Replicated voices also get C2PA credentials. Neither the API speech page nor the terms mention this, so I only have it from the blog and secondary coverage.

**Voice replication region block**
- The Google blog says voice replication is unavailable in Illinois, Texas, the EEA, the UK, Switzerland and India. If the account holder is in one of those places, cloning is out. Voice design is not mentioned in that restriction.
- Replication needs 10-30 s of clean speech from the same adult speaker plus a verbatim spoken consent statement, in 24 kHz mono. The consent phrase is in 30 languages, including en-GB. The replication page itself lists no region limits.
- Voice replication needs no allowlist. Instant Custom Voice does.
- Replicating a voice other than your own, or a stock actor's without a license, would be off the table anyway.

## Quality evidence

- **Artificial Analysis TTS leaderboard**, read 2026-10-09 (page headed September 2026), https://artificialanalysis.ai/text-to-speech/leaderboard: 1 Eleven v4 Turbo 1328; 2 Eleven v4 1320; 3 Qwen-Audio-3.1-TTS-Plus 1296; 4 Sonic 3.6 1280; **5 Gemini 3.8 Flash TTS 1273**; **8 Gemini 3.8 Flash-Lite TTS 1243**; **14 Gemini 3.1 Flash TTS 1210**. No Chirp or Cloud voices appear in the top 25. I got this from a small-model summary of the page, so the figures could be off.
- Search snippets from other pages put 3.8 Flash at #2 or #3 with Elo around 1260-1267. These were taken at different times and may be earlier versions of the board.
- **TTS Arena V2** (Hugging Face): the page loaded without its leaderboard, so I have no Google rank from it.
- **Hume AI** (via Google's own blog): Voice Design Benchmark #1 at 71.4, accent modelling 60.8; Overall Quality Index #1 and #2 for the two 3.8 models. These are vendor-reported.
- Leaderboards rate general naturalness. They do not rate an older, deep British voice held over many passages.

## Sources

Google:
- https://ai.google.dev/gemini-api/docs/speech-generation (voices, controls, 2-speaker limit, 24 kHz mono WAV, 200 custom voices per project, 1-year TTL, `voicekey_` keys with a 7-day TTL)
- https://ai.google.dev/gemini-api/docs/voice-design
- https://ai.google.dev/gemini-api/docs/voice-replication
- https://ai.google.dev/gemini-api/docs/models and https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash-tts
- https://ai.google.dev/gemini-api/docs/changelog (3.8 GA 22 Sep 2026; 3.1 Flash TTS preview 15 Apr 2026; 2.5 TTS 20 May 2025)
- https://ai.google.dev/gemini-api/docs/pricing
- https://ai.google.dev/gemini-api/terms
- https://ai.google.dev/gemini-api/docs/logs-policy
- https://ai.google.dev/gemini-api/docs/available-regions
- https://blog.google/innovation-and-ai/models-and-research/gemini-models/gemini-3-8-text-to-speech/ (the post is dated 23 Sep 2026)
- https://policies.google.com/terms/generative-ai/use-policy
- https://docs.cloud.google.com/text-to-speech/docs/gemini-tts
- https://docs.cloud.google.com/text-to-speech/docs/chirp3-hd
- https://docs.cloud.google.com/text-to-speech/docs/chirp3-instant-custom-voice
- https://docs.cloud.google.com/text-to-speech/docs/voices
- https://docs.cloud.google.com/text-to-speech/docs/data-logging
- https://docs.cloud.google.com/text-to-speech/docs/ssml
- https://docs.cloud.google.com/vertex-ai/generative-ai/docs/data-governance
- https://cloud.google.com/terms/service-terms (first 100,000 characters only)

Third party (gap-fillers):
- https://artificialanalysis.ai/text-to-speech/leaderboard
- https://www.philschmid.de/gemini-3-8-tts (reference clip 15-20 s; the Google page says 10-30 s)
- Search-result snippets: Cloud TTS pricing (cloud.google.com/text-to-speech/pricing search listing, plus diyai.io and costbench.com), thenextweb, tbreak, fonearena, orcarouter

## What I could not establish

- The Cloud TTS pricing page rendered truncated, so Chirp 3 HD at $30 and Instant Custom Voice at $60 per 1M characters, and the Studio, Neural2 and WaveNet prices, are not confirmed from a first-party page.
- Whether the 3.8 models are generally available on Vertex AI or Cloud TTS. The Cloud TTS docs say Preview, Enterprise API only.
- Any timestamp feature for any Gemini TTS model, and which Cloud voices support `<mark>` timepoints.
- A maximum text length in words or characters for the Gemini API. The only limits are 8,192 input tokens and 16,384 output tokens per request.
- Voice design price, and whether voice design or the Extended Voice Library is blocked in the UK or EEA.
- The exact abuse-monitoring retention period for the paid Gemini API.
- Whether the Gemini voice design result is modern RP and stays accent-stable across about 30 separate requests. Only an audition can show this.
- Independent listening tests of Gemini British voices. The TTS Arena leaderboard did not load.
- The Cloud Service Specific Terms for TTS and Vertex, and YouTube's synthetic-content policy.
- The en-GB voice list is partly unread, and the Studio, Neural2 and WaveNet letters are approximate.
- The Google-published date for 3.8 GA is 22 Sep (changelog) or 23 Sep (blog).
