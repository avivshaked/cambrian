# TTS survey for the narration service (read 2026-10-09)

The Sonnet researcher's final report, saved word for word on 2026-10-09. Its claims are web readings through
a summarising fetch tool. Read the LICENSE files themselves before a decision rests on them.

---

Research only: nothing was installed, downloaded or run, and I wrote no notes files. Page contents reached me through a fetch tool that summarises each page with a small model, so I double-checked licences against two sources where I could. Where only one source exists I say so. "Reading" below means my own inference, and legal readings are mine, not advice.

## 1. The service's own rules (read first)

- **Licence policy** (the narration service's repository, `docs/licences.md`, 2026-09-28, and `design.md` §18):
  - Only permissive licences are allowed.
  - Nothing non-commercial or "research only" is allowed.
  - No GPL or AGPL anywhere.
  - Qwen3-TTS (Apache-2.0) is the only speaking engine. The Kokoro adapter and Higgs/Fish are "out of scope".
- **Privacy** (design §17): nothing leaves the machine. Hosted APIs would break this. They would also break your global rule 1, because the script text would be sent out.
- **Project licence** (`COMMERCIAL.md`, `LICENSE`): the code is PolyForm Noncommercial and the prose is CC BY-NC. A use is "commercial" when it is "primarily intended for commercial advantage or monetary compensation".
- **Monetised channel** (my reading): a monetised YouTube channel plausibly meets the same test. A model's non-commercial weights licence would then likely need relaxing or a separate grant. The one exception I found is Higgs Audio v3, which carves out monetised creator content explicitly.

## 2. Leaderboard standing

**Artificial Analysis (AA) Speech Arena**, https://artificialanalysis.ai/text-to-speech/leaderboard, read 2026-10-09:
- It holds 96 models, 16 of them open-weight.
- The top is closed: ElevenLabs Eleven v4 Turbo 1329 and v4 1326, Qwen-Audio-3.1-TTS-Plus 1298 (API only), Cartesia Sonic 3.6 1281, Gemini 3.8 Flash TTS 1275.
- Open-weight models ranked on it (rank, then Elo):

| Model | Rank | Elo |
|---|---|---|
| Breeze TTS 2 | 10 | 1222 |
| Fish Audio S2 Pro | 34 | 1116 |
| Step Audio EditX | 40 | 1097 |
| Voxtral TTS | 44 | 1085 |
| Magpie-Multilingual 357M | 53 | 1065 |
| Kokoro 82M v1.0 | 55 | 1065 |
| Maya1 | 64 | 1048 |
| OpenAudio S1 Mini | 65 | 1045 |
| Chatterbox | 75 | 1028 |
| VibeVoice 1.5B | 81 | 958 |
| XTTS v2 | 88 | 917 |
| StyleTTS 2 | 90 | 895 |

- The leaderboard page has a US/UK accent filter, but no UK-only ranking was visible to me.
- Not seen in the listed open-weight rows: Qwen3-TTS, Higgs v3, OmniVoice, VoxCPM2, CosyVoice 3, F5-TTS. Whether they are unranked or simply not listed is unknown.

**TTS Arena V2** (Hugging Face): the Space page would not render, so I have no reading from it.

**Secondary July 2026 summary**, https://dev.to/lukeocodes/open-weights-are-eating-ai-here-are-the-7-best-open-tts-models-and-the-gap-nobody-mentions-13ck:
- It reports the same order with slightly different Elo values, for example Step Audio EditX 1118 and Fish S2 Pro 1110.
- Its licence column was wrong or empty ("Custom/research" for everything). I did not use that column.

**Quality caveat** (my reading): Elo from blind preference measures naturalness, not British-accent fidelity or consistency over long narration.

## 3. Model table

"UNK" means the pages I read did not say. Sources are in section 6. Everything is a card or README reading from 2026-10-09 unless noted.

| Model | British accent | Cloning | Design from text | Long-form | Word timestamps | Leaderboard (AA) | Code licence | Weights licence | Local/API | VRAM |
|---|---|---|---|---|---|---|---|---|---|---|
| **OmniVoice** (k2-fsa, 0.6B, paper 2026-04-01) | Yes, an explicit design attribute: english accents are american, british, australian, canadian, indian and others. It only applies to English text, one attribute per category, and the model "may ignore certain attributes in a combination". | Yes, from reference audio plus transcript | Yes (gender, age, pitch, accent, whisper) | UNK | UNK | Not listed | Apache-2.0 | **CC-BY-NC**, "due to constraints from its training data (e.g., Emilia)" | Local | UNK (small model; reported RTF 0.025) |
| **Higgs Audio v3 TTS 4B** (Boson, 2026-06-04) | UNK; no accent control documented | Yes, zero-shot; transcript improves it | **No** (cloning plus emotion, style and prosody tokens) | Context 8,192 tokens; no consistency doc | UNK | Not listed | Not checked | **Boson Higgs TTS 3 Research and Non-Commercial licence, with a creator grant** (section 4) | Local (SGLang-Omni/vLLM-Omni, Docker advised) or Boson API | UNK |
| **Voxtral TTS 4B** (Mistral, 2026-03-23) | 20 presets; Mistral says English includes American and British dialect variants (press, not seen on the card); cloning from 3 s "captures accent" (Mistral claim) | Yes | Not documented | Tested with 500-character inputs; UNK beyond | UNK | 44th, Elo 1085 | UNK | **CC BY-NC 4.0** | Local or Mistral API ($0.016 per 1k characters) | 16 GB minimum |
| **Fish Audio S2 Pro** (5B, 2026-03) | "strong accent" is a free-form control tag; British not demonstrated | UNK on the card read | UNK | "long-context inference" | UNK | 34th, 1116 | UNK | **Fish Audio Research Licence** (research and non-commercial; commercial needs a written licence) | Local or Fish API | UNK (RTF 0.195 on an H200) |
| **Breeze TTS 2** (BreezeBlue, 3B, 2026-08) | UNK (English and Chinese only) | Yes | Yes | UNK | UNK | **10th, 1222** (best open) | Apache-2.0 | **BreezeBlue Research and Non-Commercial licence; covers outputs from self-hosting** | Local or paid service | About 7.7 GiB eager, 14.4 GiB fast path |
| **Step-Audio-EditX** (StepFun, 3B, 2025-11) | Style and dialect tags exist; British not shown | Yes (English, Mandarin and others) | Editing tags (emotion, style) | UNK | UNK | 40th, 1097 | Apache-2.0 | **Apache-2.0** (the dev.to summary said otherwise; the card says Apache-2.0) | Local | 12 GB works, 16 GB advised |
| **VoxCPM2** (OpenBMB, 2B) | Not addressed | Yes (three modes) | **Yes** (gender, age, tone, emotion, pace) | Max sequence 8,192 tokens; "occasional instability" on very long or expressive input | UNK | Not listed | UNK | **Apache-2.0** | Local | About 8 GB |
| **Qwen3-TTS 1.7B** (the incumbent, 2026-01) | Accent control exists; your cloning from British design clips drifts American | Yes (Base) | Yes (VoiceDesign) | UNK | UNK | Open Qwen3-TTS not in the AA open list; closed Qwen-Audio-3.x are 3rd and 6th | UNK | Apache-2.0 | Local | UNK |
| **Kokoro 82M** | **Built-in British voices** (bf_emma graded B-, bf_isabella C, bm_george C, bm_fable C; others D) | No native cloning | No | Fixed voices, so consistent; per-call length UNK | UNK on the card read | 55th, 1065 | Apache-2.0 | Apache-2.0 (trained on permissive or synthetic audio) | Local, CPU-capable | About 2-3 GB (secondary source) |
| **Maya1** (3B, 2025-11) | Multi-accent English, British shown in demos | **Not documented** | **Yes** (XML description) | Training clips 1-14 s | No | 64th, 1048 | Apache-2.0 | Apache-2.0 | Local | 16 GB or more |
| **CosyVoice 3 0.5B** (2025-12) | English accents not mentioned; 18+ Chinese dialects | Yes, zero-shot | Instruct control (language, dialect, emotion, speed) | Streaming; length UNK | UNK | Not listed | Apache-2.0 | Apache-2.0 | Local | UNK |
| **IndexTTS-2 / 2.5** (bilibili, 2.5 on 2026-08-10) | UNK | Yes | Emotion and duration control | Multi-sentence demos | UNK | Not listed | Not checked | **bilibili Model Use Licence** (custom; section 4) | Local; Windows works but DeepSpeed is hard | UNK |
| **Chatterbox** (Resemble, 0.5B) | Cloned voices "may inherit the accent of the reference clip's language" (card caution) | Yes | No | UNK | UNK | 75th, 1028 | MIT | MIT; outputs carry a Perth watermark | Local | UNK |
| **GLM-TTS** (Zhipu, 2025-12) | UNK | Yes | UNK | UNK | UNK | Not listed | UNK | MIT | Local | UNK |
| **Orpheus 3B** (Canopy, 2025-03) | Not mentioned on the card | Yes | Emotion tags | UNK | UNK | Not listed | UNK | Apache-2.0 | Local | UNK |
| **Sesame CSM-1B** (2025-03) | Not fine-tuned for any voice | Context-conditioned | No | Better with conversation context | UNK | Not listed | UNK | Apache-2.0 | Local | UNK |
| **Dia 1.6B** (0626) | English only; voices vary by default | Yes (reference audio plus transcript) | No | About 2 s of audio per 256 tokens | UNK | Not listed | UNK | Apache-2.0 | Local | About 10 GB |
| **Zonos v0.1** (2B) | Not addressed | Yes | Rate, pitch, emotion | UNK | UNK | Not listed | UNK | Apache-2.0 | Local, **Linux only** | 6 GB or more |
| **Parler-TTS Large v1** (2.2B, 2024) | Accents not stated on the card | No | Yes, plus 34 named speakers for consistency | UNK | UNK | Not listed | UNK | Apache-2.0 | Local | UNK |
| **MeloTTS** | EN-BR voice exists | No | No | UNK | UNK | Not listed | MIT | MIT | Local, CPU real-time | Low |
| **StyleTTS2** | English only | Yes (LibriTTS model) | No | UNK | UNK | 90th, 895 | MIT | Pretrained models: must tell listeners the speech is synthesised, unless you have permission to clone that voice | Local | UNK |
| **F5-TTS / E2-TTS** | UNK | Yes | No | UNK | UNK | Not listed | UNK | **CC-BY-NC-4.0** (trained on Emilia) | Local | UNK |
| **XTTS-v2** (Coqui) | UNK | Yes (6 s) | No | UNK | UNK | 88th, 917 | UNK | **Coqui Public Model Licence (CPML)** | Local | UNK |
| **Spark-TTS 0.5B** | Not addressed | Yes | Gender, pitch, rate only | UNK | UNK | Not listed | UNK | **CC BY-NC-SA** | Local | UNK |
| **MaskGCT** | UNK | Yes | No | UNK | UNK | Not listed | UNK | **CC-BY-NC-4.0** | Local | UNK |
| **Llasa-3B** | UNK | Yes | No | UNK | UNK | Not listed | UNK | **CC BY-NC 4.0** | Local | UNK |
| **OpenAudio S1-mini** (0.5B) | "Not customizable" | Yes | Emotion tags | UNK | UNK | 65th, 1045 | UNK | **CC-BY-NC-SA-4.0** | Local | UNK |
| **VibeVoice 1.5B** (Microsoft) | UNK | The card says voice impersonation without recorded consent is "not intended or licensed" | No | Up to 90 minutes | UNK | 81st, 958 | UNK | MIT | Local | UNK |
| **Magpie-Multilingual 357M** (NVIDIA) | Five fixed English voices | **Removed** "for security reasons" | No | UNK | UNK | 53rd, 1065 | UNK | NVIDIA Open Model Licence | Local | UNK |
| **Piper** | en-GB voices exist (alba, northern_english_male) | No | No | UNK | UNK | Not listed | **GPL-3.0** (the service's rules ban GPL) | Per-voice licences | Local, CPU | Low |

**Not established for any local model:** word timestamps, determinism and seed behaviour, and Windows support, apart from the cases noted above. The service already does its own CTC cue alignment, so native timestamps matter less.

## 4. Licence implications for a non-commercial project whose films may earn money

All of these are my readings, not legal advice.

- **Higgs Audio v3**
  - Source: the licence file at https://huggingface.co/bosonai/higgs-audio-v3-tts-4b/blob/main/LICENSE.
  - It permits use "solely for a Research Purpose or a Non-Commercial Purpose".
  - Digital creators may "create, reproduce, publish, distribute, and monetize Creator Content" (podcasts, videos, social posts) without a separate licence.
  - Creator-content attribution is required: "Boson AI's Higgs Audio" in the audio or accompanying text.
  - Excluded: hosting the model for others as an API, SaaS or plug-in, and training other models with it.
  - You own the outputs "to the extent permitted by applicable law".
  - Cloning a real person without consent is barred.
  - Reading: a local service narrating your own films looks inside the creator grant. A narration service that other people call may be read as "making available to others". That is an open question for the owner.
- **OmniVoice**
  - The README says the pretrained model is CC-BY-NC, so the weights fall under CC BY-NC.
  - Reading: the NonCommercial test is about being primarily directed at commercial advantage or monetary compensation. A monetised channel is exposed to that test.
  - Using it would mean relaxing the service's "nothing non-commercial" rule, and a risk to accept if the channel earns.
  - No creator carve-out was found.
- **Voxtral TTS**
  - CC BY-NC 4.0, with Mistral's API as the stated commercial route.
  - Same exposure as OmniVoice.
- **Breeze TTS 2**
  - The licence says outputs from self-hosting are covered by the non-commercial licence.
  - Commercial use needs a paid BreezeBlue subscription or a licence.
  - Source: https://www.mindstudio.ai/blog/breeze-tts-2-license-commercial-use, a secondary summary, 2026-08-29. I did not read the licence text.
- **Fish Audio S2 Pro**
  - Research and non-commercial only. "Commercial Purpose" includes any use "in connection with a product or service for which You charge a fee".
  - Attribution: "Built with Fish Audio".
- **IndexTTS (bilibili licence)**
  - Royalty-free, but users above 100 million monthly active users or RMB 1 billion in annual revenue need separate permission.
  - It forbids using derivatives to improve other AI models, except non-commercial ones.
  - Reading: not NC in the usual sense, but not OSI-permissive either.
  - Source: the LICENSE file, https://raw.githubusercontent.com/index-tts/index-tts/main/LICENSE.
- **Permissive weights** (Apache-2.0, MIT)
  - Covers Kokoro, Step-Audio-EditX, VoxCPM2, Maya1, CosyVoice 3, Chatterbox and GLM-TTS.
  - No licence conflict with monetisation.
  - Misuse clauses on impersonation still apply to some of them, for example VibeVoice, and StyleTTS2's disclosure condition.
- **Code and weights differ**
  - Breeze and OmniVoice have Apache code with NC weights, so the code licence alone tells you nothing about the weights.
- **Training data**
  - Emilia-trained models (OmniVoice, F5-TTS, MaskGCT) are NC because of the data. The service's own audit left training-data effects on Apache models unknown.

**Hosted APIs**
- ElevenLabs
  - Eleven v4 / v4 Turbo are ranked 1st and 2nd.
  - The API returns character-level alignment, and `seed` is best-effort only.
  - Continuity across requests uses `previous_text`/`next_text`, or `previous_request_ids`.
  - Commercial licence is on Starter and above; Professional Voice Cloning is Creator and above, and Starter pricing was promotional.
  - Source: https://elevenlabs.io/pricing, read 2026-10-09.
- OpenAI `gpt-4o-mini-tts`
  - Accent is steerable by instructions. It has 13 built-in voices.
  - Usage policy requires disclosing the voice is AI.
  - I found no word timestamps.
- Azure
  - It has en-GB Neural and HD voices (Ryan, Sonia, Ada, Ollie).
  - Word-boundary events are provided with audio offsets.
- Google Gemini TTS
  - Accent via a style prompt. The docs only say "specific accents", British not confirmed.
  - About 655 s per call.
- Cartesia Sonic 3.6
  - 4th on the AA leaderboard (1281).
  - British voices Gemma and Archie, with instant cloning.
- All hosted APIs send the script text off the machine, which conflicts with design §17 and your global rule 1 unless you approve per instance.

## 5. Shortlist for a British (RP) narration voice

None of these has accent fidelity over long narration evidence beyond vendor claims. Every pick needs an audition with a long British clip, and the cloning drift you saw in Qwen3-TTS is a known failure type.

1. **OmniVoice**
   - The only model I found with British as an explicit, documented accent attribute for design, plus zero-shot cloning, at 0.6B and fast.
   - The attribute is its stated feature, but the docs also warn it can be ignored. Test it.
   - Licence: weights are CC-BY-NC, so it needs the owner to relax the rule, and a monetised channel is exposed. Code is Apache.
2. **Higgs Audio v3 4B**
   - The only model whose licence explicitly allows monetised video, with attribution.
   - No design from text, and accent control is not documented, so British depends on the reference clip surviving.
   - VRAM and Windows setup are unknown, and it is heavy (4B, Docker/SGLang suggested). A local service for your own films looks fine; check whether the MCP counts as "making it available to others".
3. **Voxtral TTS**
   - Mistral claims British dialect presets and accent-preserving cloning (marketing, not independently measured), and it ranks 44th among open models on AA.
   - Needs 16 GB or more.
   - CC BY NC 4.0 means the same monetisation exposure as OmniVoice, with no carve-out.
4. **Kokoro 82M** (permissive fallback)
   - Real built-in British voices (bf_emma B-, bm_george and bm_fable C), so the accent cannot drift.
   - No design or cloning, so you pick from fixed voices. Your design notes record a Kokoro bake-off already (design.md lines 131-142).
   - Apache-2.0 end to end, so no licence implication for the channel. Rank 55, Elo 1065, so quality is below the large models.
5. **VoxCPM2** (permissive audition candidate)
   - Apache-2.0, 2B, about 8 GB, with design from text and three cloning modes.
   - British is not documented, and the card warns of instability on very long input, so check long-form carefully.
   - Maya1 (Apache-2.0, British shown in demos, no cloning, 16 GB or more) is the alternative if you want description-based design only.

Why not the top-ranked open model: Breeze TTS 2 is the best-ranked open model (10th, 1222) but its licence covers self-hosted outputs as non-commercial, and I found nothing on accents or long-form.

## 6. Sources (all read 2026-10-09)

Local documents:
- The narration service's repository: `docs/licences.md` and `design.md` §17, §18.
- `D:\Projects\experiments\evolution-simulator\COMMERCIAL.md` and `LICENSE`.

Leaderboards and roundups:
- https://artificialanalysis.ai/text-to-speech/leaderboard and its `/provider-voice` view.
- https://dev.to/lukeocodes/open-weights-are-eating-ai-here-are-the-7-best-open-tts-models-and-the-gap-nobody-mentions-13ck, 2026-07-23.
- https://offlinetts.com/blog/tts-model-ranking-2026/, which removed its own numeric table on 2026-09-30.

Model cards and repositories:
- https://huggingface.co/BreezeBlue/Breeze-TTS-2
- https://www.mindstudio.ai/blog/breeze-tts-2-license-commercial-use
- https://huggingface.co/fishaudio/s2-pro and its LICENSE.md
- https://huggingface.co/fishaudio/openaudio-s1-mini
- https://huggingface.co/stepfun-ai/Step-Audio-EditX
- https://huggingface.co/mistralai/Voxtral-4B-TTS-2603
- https://mistral.ai/news/voxtral-tts
- https://huggingface.co/bosonai/higgs-audio-v3-tts-4b, its LICENSE and raw README
- https://huggingface.co/bosonai/higgs-audio-v2-generation-3B-base (the older v2/"TTS 2" card)
- https://huggingface.co/k2-fsa/OmniVoice, https://github.com/k2-fsa/OmniVoice and its `docs/voice-design.md`
- https://arxiv.org/abs/2604.00688
- https://huggingface.co/openbmb/VoxCPM2
- https://huggingface.co/maya-research/maya1
- https://huggingface.co/hexgrad/Kokoro-82M and its VOICES.md
- https://huggingface.co/FunAudioLLM/Fun-CosyVoice3-0.5B-2512
- https://huggingface.co/ResembleAI/chatterbox
- https://huggingface.co/SWivid/F5-TTS
- https://github.com/index-tts/index-tts
- https://huggingface.co/zai-org/GLM-TTS
- https://huggingface.co/canopylabs/orpheus-3b-0.1-ft
- https://huggingface.co/SparkAudio/Spark-TTS-0.5B
- https://huggingface.co/sesame/csm-1b
- https://huggingface.co/parler-tts/parler-tts-large-v1
- https://huggingface.co/nari-labs/Dia-1.6B-0626
- https://huggingface.co/Zyphra/Zonos-v0.1-transformer
- https://huggingface.co/coqui/XTTS-v2
- https://huggingface.co/microsoft/VibeVoice-1.5B
- https://huggingface.co/HKUSTAudio/Llasa-3B
- https://huggingface.co/amphion/MaskGCT
- https://huggingface.co/nvidia/magpie_tts_multilingual_357m
- https://github.com/yl4579/StyleTTS2
- https://github.com/myshell-ai/MeloTTS
- https://github.com/OHF-voice/piper1-gpl
- https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign

Hosted APIs:
- https://elevenlabs.io/docs/api-reference/text-to-speech/convert-with-timestamps
- https://elevenlabs.io/pricing
- https://developers.openai.com/api/docs/guides/text-to-speech
- https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts
- https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-speech-synthesis
- https://docs.cloud.google.com/text-to-speech/docs/gemini-tts
- https://docs.cartesia.ai/build-with-cartesia/tts-models/latest

## 7. What I could not establish

- **Direct accent evidence:** I found no independent test of how well any model holds a modern RP accent through long narration. All British claims are model-card or vendor statements. A web search for known Qwen3-TTS British-clone drift found nothing documented, so your observation stands on its own.
- **Gaps in the table:** VRAM, word timestamps, determinism and seed behaviour, and Windows support are blank for most models. Zonos is documented Linux-only; IndexTTS works on Windows with caveats.
- **Missing leaderboard views:** TTS Arena V2 would not render. The AA leaderboard has a UK-accent filter, but I could not get a UK-only ranking. I do not know where Higgs v3, OmniVoice, VoxCPM2, CosyVoice 3 or Qwen3-TTS rank.
- **Licence texts not read in full:** Breeze (secondary source only), Fish (read), Voxtral (card line only), OmniVoice (README line only), Kokoro and the Apache and MIT models (card headers only). The tool summarises pages, so a mistake is possible; one confirmed example is the dev.to roundup's wrong licence column. Read the actual LICENSE file before relying on any licence for a decision.
- **YouTube and "commercial":** I did not find any model licence that defines monetised video, apart from Higgs v3's creator grant. For the NC licences it is an unsettled legal question. I would ask the licensor before relying on it.
- **Higgs v3 scope:** I could not confirm whether it offers any accent or voice-description control. The cards I read said no.
- **Kokoro VRAM:** the 2-3 GB figure comes from a search-result snippet, not a card.
- **Piper:** its voice licences were not checked, but its GPL-3.0 code already excludes it under the service's rules.
- **Newer models:** I may have missed 2026 releases. Search results named Kugel 3, Magpie, Maya1 and others, and I covered only some of them.
