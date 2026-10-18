# The narrator's voice

The films' narration is spoken by one canon voice, designed and measured with the narration service (the
local MCP server `narration`, `.claude/skills/create-story-video/narration.md`). The skill takes that voice
as given; this file is where it is chosen. It holds the brief every candidate is designed from, the accent,
and, once the owner picks one, the chosen clip.

## The brief

The owner gave this description on 2026-10-08 as the base for every candidate. A designed voice's
description is this brief with one or two qualities varied (below), and the words are the owner's. The
owner ruled on 2026-10-18 that it stays in their words, as the designer's exact input, against the record's
rule of paraphrase:

> A mature British narrator with a warm, resonant voice and clear, natural diction. Calm and quietly
> authoritative, with genuine curiosity and a subtle sense of wonder. Speak at an unhurried pace, using
> varied sentence rhythms and thoughtful pauses that give the viewer time to absorb the images. Convey
> emotion through delicate shifts in tone and emphasis. The delivery feels intimate and conversational, as
> though sharing a remarkable discovery with one person.

The service's designer takes only positive descriptions: name the qualities wanted, never those unwanted.
A description is at most 600 characters, so the brief leaves room for about two short added qualities.

## The accent

Modern Received Pronunciation, the standard southern British English of BBC natural-history narration. The
owner asked on 2026-10-08 for an accent that would work in BBC documentary narration and agreed that this
is it, and that every candidate carries it. Every description names it in so many words ("a modern
Received Pronunciation British English accent"), since the designer follows what is named. The form meant
is the modern, softer one; the older clipped one sounds dated.

Naming the accent is not enough if it is buried. The first audition's descriptions named it in the middle
of the second sentence, after the variation and among a dozen other qualities, and the owner heard only one
of the twenty voices (M9) as British. The owner ruled on 2026-10-08 how a description is ordered from then
on. The accent comes first, in two words ("British accent."), so the model cannot miss it. The voice's
register follows (baritone, alto and so on), then the age, then the description of the voice, which is the
brief.

## What a set of candidates varies

Only qualities that change the voice's character, one or two at a time, the accent held:

- age, from the forties to the seventies;
- register: deep baritone to lighter tenor for a man, alto to brighter mezzo for a woman;
- texture: smooth, or a little grain or breath;
- pace and warmth: slower and more hushed, or a touch brisker and brighter.

Every candidate in a set speaks the same words, so the owner compares voices and not texts. A candidate
the service flags `WER_HIGH` does not say its transcript and cannot be measured; it is designed again.

## Auditions

| Date | Set | Where the clips are |
|---|---|---|
| 2026-10-08 | ten male, ten female, from the brief above, each speaking the first two paragraphs of the round 57 film's hook (prose draft 4, 296 characters); the twenty descriptions and the text are in `narrator-voices-2026-10-08.json`. The owner heard only M9 as British, and named M9 the best of the twenty and the voice to beat | `scratch/owner/voices-2026-10-08/`, with `index.md` beside them giving each clip's description, seed, sha256 and transcript |
| 2026-10-08, evening | ten male, ten female again, the same text, each description in the owner's order (accent, register, age, then the brief); the first man (M1) follows M9's qualities; the descriptions are in `narrator-voices-2026-10-08b.json` | `scratch/owner/voices-2026-10-08b/`, with M9's clip beside them as `M9-reference.wav` and `index.md` giving each clip's description, seed, sha256 and pitch |

An audition runs in two stages, as the owner agreed on 2026-10-08. First every candidate is designed on a
short text, since the designer takes at most 400 characters and a clip of at most 30 s. Then the owner's
shortlist, three or four voices, is measured and reads the whole hook (801 characters, about a minute)
through `submit_job`, so the owner hears how each carries a full passage. The hook's third paragraph holds
a nickname in braces, which needs a name before the second stage.

## What the narration keeps of a voice

Neither chosen voice speaks British through the narration. On 2026-10-09 the owner heard Edmund and
Sienna both come out American in the round 57 film's full narration. Both voices also came out American in
the service's own calibration takes, which speak the design clip's own words (reported by the video agent the
same day). The right clips and transcripts were sent, and their sha256s match. The service's check measures
the voice's timbre and not its accent, so nothing flagged it.

The accent does not hold in the design clips either. Later the same day the owner heard the original
designed clips drift toward American within the clip, before any cloning. So a clone of one of them has no
steady British accent to keep. A test of whether a cloner keeps an accent needs a reference that stays
British from start to end. The four-model clone audition of that day (Chatterbox, VoxCPM2, CosyVoice 3 and
OmniVoice cloning Edmund) cannot answer it, and the owner gave it no verdict.

Both voices were chosen by hearing the designer's clips, never a clone's. An audition is therefore judged on
what the narration model makes of a clip, a clone speaking a passage. It also checks that the accent holds
over the whole of the design clip.

No setting of this model keeps Edmund British. On 2026-10-09 his clip was cloned on the service's engine at
seed 0. Five setups were tried:
- the service's own settings;
- `non_streaming_mode` on;
- the language set to Auto in place of English;
- both of those;
- `x_vector_only_mode`, as a control.
The voice-design model also read the passage directly. The owner heard none of the clones as British, and
the direct reading as not Edmund. Sienna was not heard, since nothing else had changed. The owner concluded
that Qwen3-TTS cannot carry Edmund through the narration, and another model is to be chosen. The
measurements below hold only for this model, for each voice's timbre and length limits.

## Voices designed on Google's hosted model

On 2026-10-09 the owner heard two voices designed on Google's Gemini 3.8 Flash TTS as great (reported by
the video agent the same day). The model runs in Google's cloud and uses no time on this machine's card.
The owner approved sending the script's text to Google. The research is in `tts-google-2026-10-09.md` and
`tts-hosted-2026-10-09.md` beside this file, and the script is `scripts/tts/google/audition.py`.

- **M3**: "British accent, a deep, resonant bass-baritone, a man in his late sixties: a warm, thoughtful
  natural-history narrator, energetic and conversational. Full of wonder."
- **F4**: the same description for a woman, with "a low, warm alto, a woman in her sixties" in place of
  the man's register and age.

Each is stored in the owner's Google project. Their ids are in `scripts/tts/google/voices.local.json`, which
is kept out of git. Each read three of the round 57 film's passages, one request a passage, at 129 to 155
words a minute. An earlier round styled "unhurried" read at 96 to 134, and the owner found it too slow. An
earlier woman's design named "contralto" (F1) came out with a Russian accent. The project runs on Google's
free tier.

Two things are still open. One is whether a voice holds across a whole beat read in one request (beats 1
and 2, about 90 s each). The other is subtitle timing: Google returns no word times, so a forced aligner is
needed.

## The chosen voice

**Edmund**, M9 of the first audition. On 2026-10-08 the owner asked for M9 to be registered with the
service so that it can narrate the round 57 film, and named it Edmund. The name is the project's own: the
service keeps no names and knows a voice only by its clip's sha256 and its transcript.

- Clip: `scratch/owner/voices-2026-10-08/M9.wav`, until it has a durable home outside `scratch/`.
- sha256: `a7fe367af185c9ce7d044752efcf5d2b7248ecccc7da4b7687171634dd560cb1`.
- Transcript: the first audition's `design_text` in `narrator-voices-2026-10-08.json`, character for
  character.
- Description: M9's in the same file.
- Voice hash: `sha256:d504835d9368272cc6b0aaa7581dd83192798962076076f0f5097f75870b0f83`.
- Measurement: `job_01M4EQ1D6TSZPNXAP756F83TNP`, measured on 2026-10-08 at 22:48 under
  `qwen3-base-1.7b.p1`. The engine profile hash is
  `sha256:ec4e45f08357dfdf345ee89b34e495b245c92c340e6341cf7d1f694147b973a8`, and the measurement key is
  `sha256:931126025f4c4afd68cfcc4dbf21b410f53807c120b407784841854150e5373d`. The transcript check heard no
  errors.
- Limits: `max_segment_chars` 301 and `max_segment_seconds` 17.86. His pace is 19.1 characters a second.

The service's own copy of a clip expires after 30 days, so the clip the films use is the one kept here.

**The round 57 film is narrated twice, once by Edmund and once by Sienna** (the owner, 2026-10-08), so
that two audio versions of the same film can be made and compared. The picture is cut once. Each stretch
of narration takes the longer of the two voices' readings, so both fit.

**Sienna, F8 of the second audition, is pencilled in beside Edmund.** On 2026-10-08 the owner asked for
her to be measured too, and named her Sienna. The owner hears her as a young Australian, deep but with a lot of breath, which makes her ring
in the high frequencies. That is not the voice her description asked for (British, a woman in her sixties,
mezzo-soprano, rich and warm).

- Clip: `scratch/owner/voices-2026-10-08b/F8.wav`.
- sha256: `81ded65327696c81f78fe73d71ad9ce4c4997cbe618608259a7ef729cbd472d1`.
- Transcript: the same design text, from `narrator-voices-2026-10-08b.json`.
- Voice hash: `sha256:21f88788d3e8bf7f093846cd337ee4be9484ac24d6b01ce97b6400a5cb716f90`.
- Measurement: `job_01M4EQA58X5PQRQEYRRZHSTCS8`, measured on 2026-10-08 at 23:00 under the same engine
  profile as Edmund. The measurement key is
  `sha256:cb8a291dcab788d19a5cf51d838857a3334520b3ff1387c5f04ef80b26e49256`. The transcript check heard no
  errors.
- Limits: `max_segment_chars` 301 and `max_segment_seconds` 18.3. Her pace is 16.9 characters a second, a
  little slower than Edmund's.
