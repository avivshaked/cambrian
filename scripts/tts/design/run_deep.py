"""OmniVoice deep-voice round (the owner, 2026-10-09: British, older, deep, and a bit more lively).

Each description in scripts/tts/design/voices-omnivoice-deep.json is designed with seeds 0-7 at OmniVoice's default
class_temperature 0.0 (greedy token choice), and with seeds 0-3 at class_temperature 0.8, as a guess at a livelier
delivery: OmniVoice has no attribute for liveliness, and sampling the tokens instead of always taking the most likely
one may vary the delivery (inference, untested). Every take's median pitch is measured afterwards (run on the CPU).

Run in the omnivoice environment from the project root, after booking the GPU:
  scratch/tts-audition/envs/omnivoice/Scripts/python.exe -B scripts/tts/design/run_deep.py
Writes <gender>-t<temp>-s<seed>.wav and designs.jsonl to scratch/owner/omnivoice-deep/.
"""

import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.append(str(HERE.parent / "audition"))
import audition_common as common  # noqa: E402  (offline variables and cache folders; must precede torch)

ROOT = HERE.parents[2]
VOICES = HERE / "voices-omnivoice-deep.json"
OUT = ROOT / "scratch" / "owner" / "omnivoice-deep"
RUNS = [(0.0, range(8)), (0.8, range(4))]


def main() -> None:
    doc = json.loads(VOICES.read_text(encoding="utf-8"))
    folder = common.external("omnivoice")
    OUT.mkdir(parents=True, exist_ok=True)

    import torch
    from omnivoice import OmniVoice
    from omnivoice.models.omnivoice import OmniVoiceGenerationConfig

    model = OmniVoice.from_pretrained(str(folder), device_map="cuda:0", dtype=torch.float16)
    sr = int(model.sampling_rate)
    with (OUT / "designs.jsonl").open("w", encoding="utf-8") as log:
        for key, instruct in doc["descriptions"].items():
            for temp, seeds in RUNS:
                # models/omnivoice.py: generate(..., generation_config=) line 602; class_temperature line 182, used at
                # lines 1442-1448 (0 = argmax, >0 = Gumbel sampling over the top 10%).
                config = OmniVoiceGenerationConfig(class_temperature=temp)
                for seed in seeds:
                    common.seed_all(seed)
                    audios = model.generate(text=doc["design_text"], language="en", instruct=instruct,
                                            generation_config=config)
                    path = OUT / f"{key}-t{temp:.1f}-s{seed}.wav"
                    seconds = common.write_wav(path, audios[0], sr)
                    row = {"file": path.name, "instruct": instruct, "class_temperature": temp, "seed": seed,
                           "seconds": seconds, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                    log.write(json.dumps(row) + "\n")
                    print(json.dumps(row), flush=True)


if __name__ == "__main__":
    main()
