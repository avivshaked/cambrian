"""Word and sentence times for a narration whose words are known: forced alignment of the script to the audio.

Google's TTS returns audio and no timings, so subtitles and passage times come from here. This is the align step
of the owner's other project, synthetic-signals (scripts/build-wordlevel-timeline.py, which runs WhisperX), without
its transcription: we know the words, so the script itself is aligned to the audio with the same English wav2vec2
CTC model WhisperX uses (torchaudio's WAV2VEC2_ASR_BASE_960H), on the CPU.

The model's weights must already be in torch's hub cache (wav2vec2_fairseq_base_ls960_asr_ls960.pth); the script
refuses rather than downloads. It needs torch and torchaudio 2.8 or older (forced_align left torchaudio in 2.9).

  <python> -B scripts/tts/align.py AUDIO.wav TEXT.txt OUT.json

TEXT holds the words as sent to the voice; tags such as <long pause> are dropped. A word with digits is refused,
since the voice speaks digits as words the text does not hold. OUT holds every word with its start, end and score
(the model's mean confidence over the word's letters), and every sentence with its start and end. Words scoring
under 0.5 are printed: look at those first.
"""

from __future__ import annotations

import json
import re
import sys
import wave
from pathlib import Path

LOW_SCORE = 0.5
WEIGHTS = "wav2vec2_fairseq_base_ls960_asr_ls960.pth"


def read_wav(path: Path):
    import numpy as np
    import torch

    with wave.open(str(path)) as w:
        if w.getsampwidth() != 2:
            sys.exit(f"{path.name}: expected 16-bit PCM, got {8 * w.getsampwidth()}-bit")
        rate, channels = w.getframerate(), w.getnchannels()
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
    if channels > 1:
        data = data.reshape(-1, channels).mean(axis=1)
    return torch.from_numpy(data).unsqueeze(0), rate


def words_of(text: str) -> list[str]:
    text = re.sub(r"<[^>]*>", " ", text).replace("’", "'").replace("‘", "'")
    words = text.split()
    digits = [w for w in words if re.search(r"\d", w)]
    if digits:
        sys.exit(f"words with digits cannot be aligned: {digits[:10]}")
    return words


def letters(word: str) -> str:
    return re.sub(r"[^A-Z']", "", word.upper()).strip("'") or ""


def main() -> int:
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    audio_path, text_path, out_path = (Path(a) for a in sys.argv[1:])

    import torch
    import torchaudio
    import torchaudio.functional as F

    hub = Path(torch.hub.get_dir()) / "checkpoints" / WEIGHTS
    if not hub.is_file():
        sys.exit(f"the alignment model is not cached at {hub}; it is not downloaded from here")

    words = [w for w in words_of(text_path.read_text(encoding="utf-8")) if letters(w)]
    bundle = torchaudio.pipelines.WAV2VEC2_ASR_BASE_960H
    model = bundle.get_model().eval()
    index = {c: i for i, c in enumerate(bundle.get_labels())}

    waveform, rate = read_wav(audio_path)
    if rate != bundle.sample_rate:
        waveform = F.resample(waveform, rate, bundle.sample_rate)
    seconds = waveform.size(1) / bundle.sample_rate
    with torch.inference_mode():
        emission, _ = model(waveform)
        emission = torch.log_softmax(emission, dim=-1)
    frame = seconds / emission.size(1)

    tokens, owner = [], []  # owner[i]: which word token i spells, or -1 for a word separator
    for wi, w in enumerate(words):
        if wi:
            tokens.append(index["|"])
            owner.append(-1)
        for c in letters(w):
            tokens.append(index[c])
            owner.append(wi)
    aligned, scores = F.forced_align(emission, torch.tensor([tokens], dtype=torch.int32), blank=0)
    spans = F.merge_tokens(aligned[0], scores[0].exp())
    if len(spans) != len(tokens):
        sys.exit(f"alignment returned {len(spans)} spans for {len(tokens)} tokens")

    rows = []
    for wi, w in enumerate(words):
        mine = [s for s, o in zip(spans, owner) if o == wi]
        rows.append({"text": w, "start": round(mine[0].start * frame, 3), "end": round(mine[-1].end * frame, 3),
                     "score": round(sum(s.score for s in mine) / len(mine), 3)})

    sentences, first = [], 0
    for i, r in enumerate(rows):
        if re.search(r"[.?!][\"')\]]*$", r["text"]) or i == len(rows) - 1:
            sentences.append({"text": " ".join(x["text"] for x in rows[first:i + 1]),
                              "start": rows[first]["start"], "end": r["end"]})
            first = i + 1

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps({"audio": audio_path.name, "seconds": round(seconds, 3), "model": "WAV2VEC2_ASR_BASE_960H",
                                    "words": rows, "sentences": sentences}, indent=2, ensure_ascii=False), encoding="utf-8")
    low = [f"{r['text']} {r['start']:.2f}s {r['score']}" for r in rows if r["score"] < LOW_SCORE]
    print(f"{len(rows)} words, {len(sentences)} sentences, audio {seconds:.2f} s, "
          f"first word {rows[0]['start']:.2f} s, last word ends {rows[-1]['end']:.2f} s")
    print(f"under {LOW_SCORE}: {len(low)}" + ("".join(f"\n  {x}" for x in low)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
