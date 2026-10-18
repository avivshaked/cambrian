# Qwen-Audio 3.1 and ElevenLabs, compared with Gemini 3.8 Flash TTS (read 2026-10-09)

This is the Sonnet researcher's report, saved word for word on 2026-10-09. It was asked for when the owner
weighed hosted services for the round 57 narrator. It reads vendor pages through a summarising fetch tool.
Nothing was called, signed up for or sent. Re-read prices and terms on the vendors' own pages before
relying on them. Google's own report is `tts-google-2026-10-09.md`.

---

All pages below were read on 2026-10-09 through a page-summarising fetch tool, not raw HTML. Pages the tool returned as 404 or 403 are listed under "What I could not establish". No API was called, nothing was signed up for, and no file was written. Third-party pages are marked (3P). Anything marked "inference" is mine.

## 1. Short comparison

| | Qwen-Audio-3.1-TTS-Plus | ElevenLabs Eleven v4 (and v4 Turbo) | Gemini 3.8 Flash TTS |
|---|---|---|---|
| Quality rank (Artificial Analysis TTS leaderboard, vote-based Elo) | #3, Elo 1296 ±179 | v4 Turbo #1 (1328), v4 #2 (1320) | #5, Elo 1273 ±159 |
| AA price per 1M characters | $19.3 | v4 $80, v4 Turbo $40 | $16.5 |
| British accent | Weak spot. Only one British female system voice found on the official 3.0 list. A British male voice is listed for 3.1 Flash on a 3P page. | Strong. The Voice Library has many British narrators, and Voice Design takes accent words. | Already researched by you (voice design into a saved voice ID). |
| Voice design from a description | Not documented for Qwen-Audio-TTS. Description-based design exists only on separate Qwen3-TTS and CosyVoice models. | Yes. Prompt of 20 to 1000 characters, three previews, saved to My Voices. Works with v4. | Yes (your research). |
| Saved voices | Cloned voices get a voice ID. Unused for one year, they are deleted. | Saved in My Voices. | Yes. |
| Cost, one 7,000-character film | About $0.14 | See section 4. Plan cost $22 per month, or about $0.56 per film on the pay-as-you-go API at list price. | About $0.12 (inference, from AA's $16.5 per 1M characters). |
| Four films a month, 50% retakes (42,000 characters) | About $0.82 | $22 on Creator, $0.92 to $3.36 on the API (section 4) | About $0.70 (inference) |
| Data terms | Business data not used for training without consent. Prompts and responses kept up to 30 days. Zero-retention only for eligible enterprise workspaces. | Retains text and audio by default. Zero Retention Mode is Enterprise only. Training terms for paid self-serve plans not established. | Already researched by you. |

My reading (inference):
- ElevenLabs is the safest route to a British, older, deep, lively narrator. It has ready-made British science and documentary narrators, and Voice Design is documented to work with v4.
- Qwen-Audio 3.1 Plus is far cheaper and ranked well. But a British narrator from it would probably have to come from instruction or accent control, or from cloning a British reference clip you supply. I found no documented way to design a voice and no confirmed British male voice on the Plus tier.
- Whatever the vendor, at these volumes the voice's accent consistency matters far more than price. Qwen costs under $1 a month and ElevenLabs costs $6 to $22.

## 2. Qwen-Audio (Alibaba Cloud Model Studio / QwenCloud) details

| Item | Finding | Source |
|---|---|---|
| Model ids and status | `qwen-audio-3.0-tts-plus` and `qwen-audio-3.0-tts-flash` are on Alibaba's own pages. `qwen-audio-3.1-tts-flash` appears on the official price-reduction notice and in the Chinese-site Flash doc, which lists it as active. `qwen-audio-3.1-tts-plus` appears only on the AA leaderboard. I found no Alibaba doc page for it. The Alibaba URLs I guessed for it returned 404. A 3P page also mentions a separate "TTS-Next" model for multi-speaker audio. | [help.aliyun 3.1 flash](https://help.aliyun.com/en/model-studio/qwen-audio-3-1-tts-flash), [Alibaba 3.0 plus](https://www.alibabacloud.com/help/en/model-studio/qwen-audio-3-0-tts-plus), [price notice](https://www.alibabacloud.com/en/notice/model_studio_price_reduction_notice_for_selected_audio_models_891), [AA leaderboard](https://artificialanalysis.ai/text-to-speech/leaderboard) |
| Regions and sign-up | 3.0 Plus is offered in China (Beijing) and Singapore (international). The price notice covers the "International Site - Singapore". The sign-up process was not read. | same pages |
| Built-in voices | Official 3.0 list: Plus has only 2 system voices (Long An Ling Xin, female 25, and Long An Lu Feng, male 25, both Mandarin and English). Flash has 13, of which only one is British: `loongmary`, female, age 20, "warm British accent". The same page says 500+ base voices exist, listed in downloadable Excel files that I did not see. A 3P reseller page says 3.1 Flash has 68 system voices, including British Emily, Luna, Eric (`Eric_v3.1`) and Luca. It never confirmed that these work with Plus. | [Alibaba voice list](https://help.aliyun.com/en/model-studio/qwen-audio-tts-voice-list), [QwenCloud voice list](https://docs.qwencloud.com/api-reference/speech-synthesis/qwen-audio-tts/voice-list), 3P: [EvoLink](https://evolink.ai/docs/en/api-manual/audio-series/qwen-audio-tts/qwen-audio-3.1-tts-flash-voices) and a search snippet |
| Voice design from a description | The voice-design doc lists only CosyVoice (design in Beijing only) and Qwen-TTS (`qwen3-tts-vd-*`, 2,048-character description, English or Chinese descriptions only, $0.20 per voice) as supporting it. Qwen-Audio-TTS is not listed. One QwenCloud summary page does say Qwen-Audio-TTS supports "custom voice (cloning and design)", which conflicts with that. Designed voices (on those other models) get a voice ID and are deleted after one year of non-use. Limit 1,000 voices per model family. | [voice design](https://docs.qwencloud.com/developer-guides/speech/voice-design.md), [Alibaba voice design (Indonesian page)](https://www.alibabacloud.com/help/id/model-studio/qwen-tts-voice-design), [tts-models](https://docs.qwencloud.com/developer-guides/speech/tts-models.md) |
| Voice cloning | Yes, with model `voice-enrollment`, targeting 3.0 Plus and Flash. Sample 10 to 20 seconds (60 maximum, at least 5 seconds of clear speech, WAV, MP3 or M4A, 10 MB or less, 16 kHz or more). It produces an instant voice ID and creation is free. 3.1 is said to clone more robustly from noisy audio, per a 3P page and Alibaba's own feature summary. The terms say the recording must be the user's own voice. | [voice cloning](https://docs.qwencloud.com/developer-guides/speech/voice-cloning.md), [Alibaba cloning guide](https://www.alibabacloud.com/help/en/model-studio/voice-cloning-user-guide) |
| Accent and style control | Natural-language instructions for speed, emotion and style, plus "fine-grained tag control". 3P pages say 3.1 adds accent and timbre in instructions, with 86 inline tags. The official instruction doc I read covers Qwen3-TTS, which limits instructions to Chinese and English. | [tts-models](https://docs.qwencloud.com/developer-guides/speech/tts-models.md), [3.0 plus](https://www.alibabacloud.com/help/en/model-studio/qwen-audio-3-0-tts-plus), 3P: [ai-tldr](https://ai-tldr.dev/models/qwen-audio-3-1-tts/) |
| Length limit per request | Not stated on any official page I could read. A 3P page says 3.1 does "one-pass synthesis up to 3 minutes". Your passages are about 300 characters, so this probably does not matter (inference). | [ai-tldr (3P)](https://ai-tldr.dev/models/qwen-audio-3-1-tts/) |
| Timestamps | Not mentioned on any page I read. | |
| Output format | Not stated for Qwen-Audio. The Qwen3-TTS doc says 24 kHz WAV/PCM, either a URL valid 24 hours or streamed base64 chunks. That may not apply to Qwen-Audio. | [QwenCloud tts](https://docs.qwencloud.com/developer-guides/speech/tts.md) |
| Pricing | 3.0 Plus: $0.20 per 10,000 characters in Singapore ($19.25 per 1M in Beijing). 3.0 Flash: $0.15 per 10,000 characters. 3.1 Flash moved to token billing on 2026-09-22 (Singapore: $0.23 per 1M input tokens, $1.87 per 1M output tokens). I found no Alibaba price for 3.1 Plus. AA lists it at $19.3 per 1M characters. Chinese characters count as 2 and all others as 1. Output is not charged (3.0 docs). Rate limit 180 requests per minute (Plus). | [Alibaba model pricing](https://www.alibabacloud.com/help/en/model-studio/model-pricing), [price notice](https://www.alibabacloud.com/en/notice/model_studio_price_reduction_notice_for_selected_audio_models_891), [AA](https://artificialanalysis.ai/text-to-speech/leaderboard) |
| Free quota | Singapore only: 10,000 characters for 3.0 Plus and 10,000 for 3.0 Flash, valid 90 days from activation. None in other regions. This is the 3.0 pricing page, not 3.1. | [Alibaba model pricing](https://www.alibabacloud.com/help/en/model-studio/model-pricing) |
| Data: logging, retention, training | Standard: prompts and responses from inference are kept no longer than 30 days. Alibaba states it will not use business data (inputs, outputs, files) to train or improve any foundation model without separate consent. An enterprise Zero Data Retention option exists for non-Mainland-China regions (Singapore, US, Germany, Hong Kong, Tokyo), and it explicitly covers the `qwen-audio-3.1-tts-*` series. | [data retention policy](https://www.alibabacloud.com/help/en/model-studio/data-retention-policy) |
| Commercial use of output | Not clearly established. The service terms restrict content "generated through model trials" to evaluating the model, with no commercial use. That is in the Trial services section, so it appears to cover trial or free use only. The terms say nothing explicit either way about paid calls. They also require AI-generated content to be clearly labelled. | [service terms](https://help.aliyun.com/en/model-studio/bailian-service-notes) |
| Watermark | None mentioned in the terms I read. | |
| Country restrictions | Not addressed on the pages read. | |

Note on rank: the AA leaderboard page I read shows Qwen-Audio-3.1-TTS-Plus at #3 in the top-12 list. AA's own model page says "rank 27 of 100 in Speech Arena" (Elo 1298), so the ranking depends on which filter or list is used. A 3P page also says there was no independent benchmark yet at the time of the 2026-09-23 release.

## 3. ElevenLabs details

| Item | Finding | Source |
|---|---|---|
| Current models | `eleven_v4` (10,000-character limit, 90+ languages, "most emotive, high quality"). `eleven_v4_turbo` (about 100 ms real-time model, limit not stated). Also `eleven_v3` (5,000), `eleven_v3_conversational`, `eleven_multilingual_v2` (10,000, "most stable on long-form"), `eleven_flash_v2_5` (40,000) and `eleven_flash_v2` (30,000). | [models](https://elevenlabs.io/docs/overview/models), [v4 page](https://elevenlabs.io/v4) |
| Plans (price, credits per month) | Free $0 / 10,000. Starter $6 / 30,000. Creator $22 ($11 the first month) / 121,000. Pro $99 / 600,000. Scale $299 / 1.8M. Business $990 / 6M. Enterprise is custom. A "3x credits on Creator+" promotion runs until 2026-10-12. | [pricing](https://elevenlabs.io/pricing) |
| Credits per character | Not stated for v4 on any page the tool read. The pricing page says 1 credit per character for Multilingual v2, and 0.5 to 1 for Flash and Turbo v2 and v2.5. A search snippet from the vendor's own results says v4 costs one credit per character with the same credit pricing as other models, but I could not open the page behind it. A 3P page (eesel) also says about 1 credit per character in the app. | [pricing](https://elevenlabs.io/pricing), [eesel (3P)](https://www.eesel.ai/blog/eleven-v4-pricing) |
| API pricing per 1,000 characters | v4 $0.08 list, v4 Turbo $0.04. Both are discounted 72% (v4 $0.022, Turbo $0.011) until 2026-10-12. v3 and Multilingual v2 are $0.08. Flash is $0.04. The page shows billing in dollars, not credits. | [API pricing](https://elevenlabs.io/pricing/api) |
| Commercial use | "Commercial License" is listed from Starter up. Free is not listed. | [pricing](https://elevenlabs.io/pricing) |
| Voice Design | Prompt of 20 to 1000 characters plus preview text. It returns three previews and charges the preview text once. Chosen voices are saved to My Voices. It works with v4 and v3, but designed voices "may be less performative" on v4. The page calls the feature experimental and says Professional clones are more consistent. Accent prompts should be specific, e.g. "thick French accent". The vendor lists Voice Design under the Free plan features, so I read it as available on every plan (inference). | [Voice Design](https://elevenlabs.io/docs/eleven-creative/voices/voice-design), [voices overview](https://elevenlabs.io/docs/overview/capabilities/voices) |
| British voices | The Voice Library has 17,000+ voices with an accent filter. The British accent page names Christopher (British Storyteller), Edward (British, Dark, Low), Bradford (Articulated Storyteller) and Frederick Surrey (Science Storyteller). A search snippet describes Frederick Surrey as "a professional, calm, well spoken British narrator full of intrigue and wonder" for documentaries, with a "smooth & velvety tone". Whether library voices carry the plan's commercial licence was not found. | [British accent page](https://elevenlabs.io/text-to-speech/british-accent), [voice library](https://elevenlabs.io/voice-library) |
| Cloning | Instant clone from Starter up. Professional clone from Creator up. | [pricing](https://elevenlabs.io/pricing) |
| Timestamps | The `/v1/text-to-speech/{voice_id}/with-timestamps` endpoint gives character-level start and end times, and a normalised-text alignment as well. | [with-timestamps](https://elevenlabs.io/docs/api-reference/text-to-speech/convert-with-timestamps) |
| Consistency across requests | `previous_text` and `next_text`, `previous_request_ids` and `next_request_ids` (up to 3), and `seed` (best-effort determinism, not guaranteed). The docs say results are best when the same model is used throughout. The v4 page mentions "context stitching" for long content. Voice settings (stability, similarity, style, speed) are also available. I did not see whether request stitching works on v4. The v4 page only says a single generation allows up to 10,000 characters. | [convert](https://elevenlabs.io/docs/api-reference/text-to-speech/convert), [with-timestamps](https://elevenlabs.io/docs/api-reference/text-to-speech/convert-with-timestamps), [v4](https://elevenlabs.io/v4) |
| Output formats | MP3, Opus, PCM, WAV and others, 8 to 48 kHz. MP3 at 192 kbps needs Creator or above. | [with-timestamps](https://elevenlabs.io/docs/api-reference/text-to-speech/convert-with-timestamps) |
| Data use and retention | Zero Retention Mode is Enterprise only, API only, and is meant mainly for healthcare and banking customers. Without it, ElevenLabs keeps text, audio and email addresses from API requests. Deleted items persist in backups up to 30 days. By default it does not train on Enterprise customers' data. Whether text from self-serve paid plans is used for training was not established. The help page that answers this returned 403. | [Zero Retention Mode](https://elevenlabs.io/docs/eleven-api/resources/zero-retention-mode) and a search snippet; help article: [403, unread](https://help.elevenlabs.io/hc/en-us/articles/29952728805393-Is-my-data-used-to-improve-ElevenLabs-AI-models) |

## 4. Cost workings (inference, from the figures above)

**One 7,000-character film**
- Qwen-Audio-3.1-TTS-Plus: 7,000 × $19.3 per 1M = $0.135. Taking Alibaba's 3.0 Plus rate of $20 per 1M gives $0.14.
- Gemini 3.8 Flash TTS: 7,000 × $16.5 per 1M = $0.12 (AA price, not your researched token price).
- ElevenLabs v4 on the app, assuming 1 credit per character: 7,000 credits, which is 23% of Starter's 30,000.
- ElevenLabs v4 on the pay-as-you-go API: $0.56 at list price, $0.154 at the promotional price (promotion ends 2026-10-12).

**Four films a month with 50% retakes (42,000 characters)**
- Qwen: 42,000 × $19.3 per 1M = $0.81. Gemini: about $0.69.
- ElevenLabs on an app plan: 42,000 credits is more than Starter's 30,000. Starter would cover four films with only about 7% retakes (28,000 credits for four plus 2,000 spare). The cheapest commercial plan that covers 50% retakes is Creator at $22 per month, with 121,000 credits, which leaves room for about 79,000 more credits (Voice Design previews, extra retakes). The first month is $11. I did not find whether Starter can buy extra credits.
- ElevenLabs on the API, pay as you go: 42,000 × $0.08 per 1,000 = $3.36 per month at list price, or $0.92 at the promotional price. I did not read the pay-as-you-go plan terms, so the minimum charge and licence there are unconfirmed.
- Voice Design is a small extra on ElevenLabs. The cost is the preview text's characters, charged once for three options.

## 5. What I could not establish

- No Alibaba documentation page for `qwen-audio-3.1-tts-plus` exists in what I could read. Its official price, voice list, free quota and limits are unknown. I used the AA price ($19.3 per 1M characters) and 3.0 Plus documentation as stand-ins.
- Whether any British male voice, or any older deep voice, is available on Qwen-Audio 3.1 Plus. The only British voices I found are one female voice on 3.0 Flash (official) and Eric, Emily, Luna and Luca on 3.1 Flash (3P only).
- Whether Qwen-Audio supports voice design from a description. Official pages point to different model families, and one summary page claims support. This needs a direct test or a question to Alibaba.
- Qwen's per-request character limit, whether it offers timestamps (word or character), and its output format. All are undocumented on the pages I read.
- Qwen's sign-up steps, any country restriction on the account holder, and the written commercial-use terms for paid output (only the trial restriction was found).
- The exact credit cost per character for ElevenLabs v4 and v4 Turbo. The only "1 credit per character" statements are a search snippet and a 3P page, and Flash and Turbo v2 are lower. The credit allotments in 3P material suggest v4 may be billed in API-dollar terms on some plans, which would change the Starter and Creator numbers.
- Whether Starter can buy extra credits, and the post-promotion plan allotments after 2026-10-12.
- Whether ElevenLabs request stitching (`previous_request_ids`) works with v4, and whether Voice Library voices are covered by the plan's commercial licence.
- Whether text from ElevenLabs self-serve paid plans is used for training. The page that would say so was inaccessible.
- Gemini figures are only what AA lists. I did not re-research Gemini.
- Page summaries came through a fetch tool that rewrites page text, so exact wording on the key claims (commercial use, retention, the ZDR coverage of 3.1) should be re-read on the vendor pages before relying on them.

## 6. Sources

Vendor:
- https://www.alibabacloud.com/help/en/model-studio/qwen-audio-3-0-tts-plus
- https://help.aliyun.com/en/model-studio/qwen-audio-3-1-tts-flash
- https://help.aliyun.com/en/model-studio/qwen-audio-tts-voice-list
- https://www.alibabacloud.com/help/en/model-studio/model-pricing
- https://www.alibabacloud.com/en/notice/model_studio_price_reduction_notice_for_selected_audio_models_891
- https://www.alibabacloud.com/help/en/model-studio/voice-cloning-user-guide
- https://www.alibabacloud.com/help/id/model-studio/qwen-tts-voice-design
- https://www.alibabacloud.com/help/en/model-studio/data-retention-policy
- https://help.aliyun.com/en/model-studio/bailian-service-notes
- https://docs.qwencloud.com/llms.txt and, under it, the pages /developer-guides/speech/tts-models.md, /tts.md, /voice-cloning.md, /voice-design.md, /developer-guides/getting-started/pricing.md, /developer-guides/security-compliance/data-security.md, /api-reference/speech-synthesis/qwen-audio-tts/voice-list
- https://elevenlabs.io/pricing
- https://elevenlabs.io/pricing/api
- https://elevenlabs.io/v4
- https://elevenlabs.io/docs/overview/models
- https://elevenlabs.io/docs/overview/capabilities/text-to-speech
- https://elevenlabs.io/docs/overview/capabilities/voices
- https://elevenlabs.io/docs/eleven-creative/voices/voice-design
- https://elevenlabs.io/docs/api-reference/text-to-speech/convert
- https://elevenlabs.io/docs/api-reference/text-to-speech/convert-with-timestamps
- https://elevenlabs.io/docs/eleven-api/resources/zero-retention-mode
- https://elevenlabs.io/text-to-speech/british-accent
- https://elevenlabs.io/docs/overview/administration/data-residency

Independent benchmark:
- https://artificialanalysis.ai/text-to-speech/leaderboard (also https://artificialanalysis.ai/text-to-speech/models/qwen-audio-3-1-tts-plus)

Third party (3P):
- https://ai-tldr.dev/models/qwen-audio-3-1-tts/
- https://evolink.ai/docs/en/api-manual/audio-series/qwen-audio-tts/qwen-audio-3.1-tts-flash-voices
- https://www.orcarouter.ai/blog/qwen-audio-3-1-vs-qwen-audio-3-0-tts
- https://www.eesel.ai/blog/eleven-v4-pricing
- Search-result snippets, used only where marked.

Pages that returned 404, 403, or no content: help/en/model-studio/qwen-audio-3-1-tts-plus and /qwen-audio-tts (404), /data-security-and-privacy (404), elevenlabs.io/docs/overview/capabilities/voice-design (404), and help.elevenlabs.io articles 27562020846865, 29952728805393 and 13313564601361 (403).
