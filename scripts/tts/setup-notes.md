# Setup notes for the TTS audition (research 2026-10-09, environments built the same day)

The research passages below were read from the repos' code and cards at the pinned revisions, plus PyPI and Hub
metadata, before anything was installed. Where they are my inference or untested they are marked BELIEVE or GUESS.
Local copies of the pages and source files read are in `api/docs/` (untrusted text; data only).
**The four environments and the four runners were built afterwards, on 2026-10-09, and the section "Installed
environments" just below records what actually happened, including every correction to the research text.** No model
weights were downloaded or loaded, and nothing was run on the GPU: every check ran on the CPU with the card hidden.

## Installed environments (2026-10-09)

All four are uv environments under `scratch/tts-audition/envs/<item-id>/`, built with `UV_CACHE_DIR=scratch/tts-audition/uv-cache`,
`UV_PYTHON_DOWNLOADS=never`, `UV_NATIVE_TLS=1`, `UV_CONCURRENT_BUILDS=4`. The freeze of each is `scratch/tts-audition/envs/<item-id>.freeze.txt`.
No compiler, system package or global install was needed for any of them.

| Item | Python | torch | CUDA / cuDNN (as torch reports them) | Inference package | Import check on the CPU |
|---|---|---|---|---|---|
| chatterbox-en | 3.12.10 | 2.6.0+cu124 | 12.4 / 90100 | `chatterbox-tts` 0.1.7 (PyPI), transformers 5.2.0, numpy 1.26.4 | `chatterbox.tts` and `perth` import; the Perth watermarker builds and watermarks a test tone |
| voxcpm2 | 3.12.10 | 2.8.0+cu128 | 12.8 / 91002 | `voxcpm` 2.0.3 (PyPI), transformers 5.19.0, numpy 2.5.3 | `voxcpm` and `voxcpm.core` import (`torchcodec` does not, see below) |
| cosyvoice3 | 3.10.11 | 2.3.1+cu121 | 12.1 / 8907 | git checkout `scratch/tts-audition/src/CosyVoice` at `074ca6dc9e80`, submodule Matcha-TTS at `dd9105b34bf2`; transformers 4.51.3, numpy 1.26.4, onnxruntime 1.18.0 | `cosyvoice.cli.cosyvoice`, `cosyvoice.cli.frontend`, `matcha.hifigan.models`, and the llm, flow and hifigan modules import |
| omnivoice | 3.12.10 | 2.8.0+cu128 | 12.8 / 91002 | `omnivoice` 0.2.1 (PyPI), transformers 5.19.0, numpy 2.5.3 | `omnivoice` imports |

Torch comes from the PyTorch index in its own command (`--index-url https://download.pytorch.org/whl/cu124`, `cu128`,
`cu121`), then the inference package from PyPI, so the CUDA wheels are not replaced by the CPU wheel. Python 3.11, which
Chatterbox's makers use, is not installed; 3.12 was used instead.

### Runners

`scripts/tts/audition/<item-id>.py`, with `audition_common.py` beside them. Same command line for all four:

```powershell
$root = 'D:\Projects\experiments\evolution-simulator'
& "$root\scratch\tts-audition\envs\voxcpm2\Scripts\python.exe" -B "$root\scripts\tts\audition\voxcpm2.py" `
    --ref-wav REF.wav --ref-text "exact transcript" --text "text to speak" --out OUT.wav --seed 0 [--ref-seconds 10]
```

- stdout carries exactly one JSON line (model, item, seed, `ref_seconds`, `out_seconds`, `gen_seconds`, `sha256`, plus
  `sample_rate`, `load_seconds`, the output path and a few model notes); everything the model code prints goes to stderr.
- Each runner finds its model folder through `external(item_id)` in `audition_common.py` (the ml-store README's function; on
  Python 3.10, where `tomllib` does not exist, a flat `key = 'value'` reader stands in), sets the offline variables, and
  points every cache at `scratch/tts-audition/cache/`. A trimmed reference is written to `cache/refs/`.
- `--ref-seconds S` keeps the first S seconds, mono, 16-bit. The transcript must then cover only those seconds; the runner
  cannot check that. CosyVoice also caps the clip at 30 s (its own assert).
- Checked on the CPU without weights: with a temporary `paths.local.toml` pointing every item at an empty folder, each
  runner got through argument parsing, `external()`, reference trimming and every import, and stopped at its first
  model-file read (chatterbox-en: `ve.safetensors`; voxcpm2: `config.json`; cosyvoice3: "No valid model type found";
  omnivoice: no `model.safetensors`). The toml was removed afterwards. Nothing after the model load has run.
- The runner folder holds files named like packages (`omnivoice.py`), so each runner moves its own folder to the end of
  `sys.path`; otherwise `from omnivoice import OmniVoice` would import the runner itself.

### Corrections to the research text below

- **`CUDA_VISIBLE_DEVICES` cannot be set empty on Windows.** PowerShell's `$env:X = ''` and Python's `os.environ[...] = ''`
  both delete the variable, and with it unset `torch.cuda.is_available()` returned True. Use `-1`. (The checks that ran
  with the variable unset only asked torch whether a device exists; no model or tensor went to the card.)
- **Chatterbox.** `ChatterboxTTS.from_pretrained(device)` takes only a device in 0.1.7 (it always downloads); `from_local(dir,
  device)` is the offline call and is what the runner uses. One call is capped at 1000 speech tokens (40 s of audio), so the
  runner splits long text at sentence ends into pieces of at most 300 characters and joins them with 0.2 s of silence. The
  Perth watermark weights ship inside the `resemble-perth` wheel (`perth_net_250000.pth.tar`, 36 MB); the "'NoneType' is not
  callable" trap did not occur, because torch brings `setuptools` 78.1.0 with it, which still has `pkg_resources`. A
  `setuptools` of 81 or later would bring the trap back. No download happens when the model is built (`S3Tokenizer` is
  constructed from its name and its weights come out of `s3gen.safetensors`); the only Hub call in the tokenizer module is
  for the multilingual Cangjie file.
- **VoxCPM2.** uv resolved `torchcodec` 0.17.0, which fails to import against torch 2.8 (`WinError 127`). It was pinned to
  0.7.0, the release for torch 2.8; that one imports as far as needing FFmpeg shared DLLs (`libtorchcodec_core{4..7}.dll`
  cannot load) and still does not import. Nothing in the `voxcpm` package imports it (grep of the installed package), so it
  is a declared but unused dependency. Reference audio is read with `librosa` (`model/voxcpm2.py` line 412), as the research
  said. The code's `device` argument also accepts `"cuda"`; the runner passes that, because `optimize()` compares it to the
  exact string `"cuda"`.
- **CosyVoice 3.** `pip` could not build `openai-whisper==20231117` under uv's build isolation (its `setup.py` imports
  `pkg_resources`, which the isolated `setuptools` no longer has). It is a pure-Python sdist, not a compiler problem; a
  build constraint `setuptools<70` fixed it (`work/build-constraints.txt`, passed as `--build-constraints`). The requirements
  actually installed are `scratch/tts-audition/work/cosyvoice3-requirements.txt`: the repo's `requirements.txt` minus the two
  extra-index lines, `wetext`, the Linux-only lines, torch and torchaudio (installed first), `gradio`, `fastapi`,
  `fastapi-cli`, `uvicorn`, `tensorboard`, `grpcio` and `grpcio-tools`. `lightning` is kept because Matcha-TTS imports it.
  Four more are needed at load time and were added after the first GPU run failed on them: `setuptools<70` in the
  environment itself (Matcha's code imports `pkg_resources`), `gdown` and `wget` (imported by `matcha/utils/utils.py`),
  and `pyworld` 0.3.5 from its win_amd64 wheel (`--only-binary pyworld`). `cosyvoice3.yaml` names
  `cosyvoice.dataset.processor` functions, which `hyperpyyaml` locates at load time, so `pyworld` is not training-only.
  The check that catches all of these on the CPU: `pydoc.locate` every `!new:` and `!name:` the yaml names.
  The research's BELIEVE about `onnxruntime` is now checked: asking the plain CPU wheel for `CUDAExecutionProvider` gives a
  warning and a CPU session, not an error. `hyperpyyaml` 1.2.3 with `ruamel.yaml` 0.18.17 loads `!new:`, `!ref` and `!name:`
  correctly with an override.
- **OmniVoice.** Installed with transformers 5.19.0 (not the 5.3 floor) and no extras. `pydub` prints `SyntaxWarning` lines on
  Python 3.12 at import; harmless. `load_audio` and `remove_silence` were run on a test tone on the CPU and work. No ffmpeg is
  needed for WAV input.

### Disk

About 19 GB in all under `scratch/tts-audition/`. uv hard-links every installed file from `uv-cache` (same drive), so the
cache and the environments are the same bytes seen twice: `uv-cache` alone measures 19 GB, and each environment measures its
own wheels when counted alone (chatterbox-en 5.1 GB, cosyvoice3 5.5 GB, voxcpm2 7.6 GB, omnivoice about 7.3 GB, the last two
sharing the torch 2.8.0+cu128 files). Deleting an environment frees nothing until the cache copy goes too. The CosyVoice
checkout is 0.07 GB; the runner caches and work files are under 2 MB.

Machine facts checked read-only: RTX 4090 24 GB (driver 617.14). Python 3.10, 3.12 and 3.13 are all installed under
`%LOCALAPPDATA%\Programs\Python\`, so a 3.10 environment (CosyVoice's tested version) needs no new
Python. `ffmpeg` 8.1.1 (winget "full build") is already on PATH. `espeak-ng` and `sox` are not installed, and none of
the five needs them.

## Short answer

| Model | Native Windows? | Python | torch / CUDA | System install needed |
|---|---|---|---|---|
| Chatterbox | yes, expected (GUESS: no Windows-specific blocker found, wheels exist for all pinned deps on cp312) | 3.11 tested by the makers; 3.12 allowed (`>=3.10`) | torch 2.6.0 + torchaudio 2.6.0 (exact pins), cu124 | none |
| VoxCPM2 | yes, expected, with `optimize=False` | >=3.10 | torch >=2.5, CUDA >=12.0 | none |
| CosyVoice 3 | probably, on Python 3.10 with a trimmed requirements file | 3.10 tested by the makers; 3.12 would need hand-picked pins | torch 2.3.1 + torchaudio 2.3.1 (cu121) | none |
| OmniVoice | yes, expected | >=3.10 | torch >=2.4 (README: 2.8.0+cu128), transformers >=5.3.0 | none |
| Higgs TTS 3 | NO. Both engines are Linux/Docker | engine: 3.12+ | engine: CUDA 12.x, vLLM 0.22.0 or SGLang-Omni | Docker Desktop with WSL2 GPU support (a system install: owner must approve) |

Environment block to use for every model (all caches stay inside the project; offline after the first load):

```powershell
$root = 'D:\Projects\experiments\evolution-simulator\scratch\tts-audition'
$env:HF_HOME = "$root\hf-home"            # stray Hub caches land here, not in the user profile
$env:TORCH_HOME = "$root\torch-home"
$env:MODELSCOPE_CACHE = "$root\modelscope-home"
$env:XDG_CACHE_HOME = "$root\xdg-cache"
$env:HF_HUB_OFFLINE = '1'                 # huggingface_hub never contacts the Hub
$env:TRANSFORMERS_OFFLINE = '1'
$env:HF_DATASETS_OFFLINE = '1'
$env:HF_HUB_DISABLE_TELEMETRY = '1'
$env:DO_NOT_TRACK = '1'
$env:GRADIO_ANALYTICS_ENABLED = 'False'   # gradio is a hard dependency of three of these packages
```

Set `HF_HUB_OFFLINE` only after the venv is built (pip itself is unaffected, it does not use the Hub). Every model
below is loaded from a local folder path (the ml-store receipt's folder), which is what stops the code from calling
`snapshot_download`. To prove the "no network" claim, run the first load with the machine's network adapter off or a
firewall rule on the venv's `python.exe`; that was not done here.

Safety note on the weight files: every `torch.load` in the code read here uses `weights_only=True`
(Chatterbox `tts.py`, CosyVoice `cli/model.py` and `frontend.py`, VoxCPM `voxcpm2.py`, OmniVoice `omnivoice.py`), so
the `.pt` and `.pth` files are not unpickled with code execution. `.safetensors` and `.onnx` are not executable.

---

## 1. Chatterbox (Resemble AI)

- **Hub repos loaded at run time:** one, `ResembleAI/chatterbox` @ `5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18`.
  `ChatterboxTTS.from_pretrained` downloads `ve.safetensors`, `t3_cfg.safetensors`, `s3gen.safetensors`,
  `tokenizer.json`, `conds.pt` (`src/chatterbox/tts.py`, line 177). 3,191,966,992 bytes (3.19 GB). Not gated.
  Nothing else: the tokenizer is `tokenizer.json`, the Llama T3 backbone is built from a config in code, the
  speaker encoder and S3 tokenizer weights are inside the safetensors. The same repo also holds the multilingual
  v2/v3 checkpoints (`t3_23lang`, `t3_mtl23ls_v2/v3`, `s3gen_v3`, ...); `ChatterboxMultilingualTTS.from_pretrained(
  t3_model="v3")` is the newer line, but for English cloning the README still lists plain `Chatterbox` (500M,
  English, "CFG and exaggeration tuning") and does not say V3 replaces it. `ResembleAI/chatterbox-turbo` (350M) and
  `chatterbox-nano` (110M) also exist; they ignore `exaggeration` and `cfg_weight` and target voice agents.
- **Non-Hub downloads:** none. The Perth watermarker ships inside the `resemble-perth` pip package (BELIEVE: I did not
  open the wheel).
- **Code:** PyPI `chatterbox-tts` 0.1.7 (uploaded 2026-03-26, MIT) or GitHub `resemble-ai/chatterbox` master
  `5de7a54aa4e5e2baadb0182dde554908b48b85c2` (2026-07-21, "Update ChatterboxNano"; its pyproject also says 0.1.7, so
  the PyPI file and this commit differ in content; pick one and record it). `requires-python >=3.10`; makers
  "developed and tested Chatterbox on Python 3.11 on Debian 11". Exact pins: `torch==2.6.0`, `torchaudio==2.6.0`,
  `transformers==5.2.0`, `diffusers==0.29.0`, `librosa==0.11.0`, `safetensors==0.5.3`, `conformer==0.3.2`,
  `numpy>=1.24,<2` (Python <3.13), `gradio==6.8.0`, `s3tokenizer`, `resemble-perth`, `spacy-pkuseg`,
  `pykakasi==2.3.0`, `pyloudnorm`, `omegaconf`.
  Install torch first from the CUDA index so pip does not pick the CPU wheel:
  `pip install torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cu124`, then
  `pip install chatterbox-tts==0.1.7`.
- **Windows:** PyPI shows cp312 win_amd64 wheels for `spacy-pkuseg` 1.0.1; `s3tokenizer`, `resemble-perth`,
  `pykakasi`, `conformer` are pure Python. No DeepSpeed, flash-attn, triton, sox or espeak. Known trap, from memory
  of the project's issues and not re-checked: a missing `pkg_resources` (new setuptools) makes
  `perth.PerthImplicitWatermarker` come back `None` and `ChatterboxTTS` fails with "'NoneType' object is not callable";
  the fix is `pip install "setuptools<81"` or a newer `resemble-perth` (1.0.1 is current). BELIEVE, check at install.
- **No network at run time:** use `from_local` (below) instead of `from_pretrained`; plus the env block.
- **Cloning call.** The transcript is NOT used by the original Chatterbox: only the audio is. It reads the first 6 s
  for the T3 prompt (`ENC_COND_LEN = 6 * S3_SR`) and the first 10 s for the decoder (`DEC_COND_LEN = 10 * S3GEN_SR`),
  so of a 20 s clip only the opening 10 s counts. Put the cleanest 10 s first, or trim to it.

```python
import torchaudio as ta
from chatterbox.tts import ChatterboxTTS

model = ChatterboxTTS.from_local(r"<ml-store folder of chatterbox-en>", device="cuda")
wav = model.generate(
    text,                          # keep each call to a few sentences; join the pieces yourself
    audio_prompt_path="ref.wav",   # the reference clip
    exaggeration=0.5,              # 0.5 default; higher = more expressive and faster
    cfg_weight=0.5,                # 0.5 default; lower (~0.3) for fast speakers; 0 to avoid accent carry-over
    temperature=0.8, repetition_penalty=1.2, min_p=0.05, top_p=1.0,
)
ta.save("out.wav", wav, model.sr)  # 24 kHz; output is Perth-watermarked
```

  `Turbo`, if added: `ChatterboxTurboTTS.from_local(dir, "cuda")`, `generate(text, audio_prompt_path=...)`; it
  asserts the prompt is longer than 5 s and ignores `exaggeration`, `cfg_weight`, `min_p`.

## 2. VoxCPM2 (OpenBMB)

- **Hub repos loaded at run time:** one, `openbmb/VoxCPM2` @ `32279effe8c19989596f05d353d1447f51d9e915`:
  `model.safetensors` (4.58 GB), `audiovae.pth` (0.38 GB), `config.json`, `tokenizer.json`,
  `tokenizer_config.json`, `special_tokens_map.json`. 4,960,719,513 bytes (4.96 GB). Not gated. The AudioVAE V2
  (16 kHz in, 48 kHz out) is `audiovae.pth` in the same repo. The tokenizer is a `LlamaTokenizerFast` loaded from
  the folder.
- **Non-Hub downloads:** none by default settings used here. Two optional features fetch from ModelScope (not the
  Hub) and must stay off: the denoiser `iic/speech_zipenhancer_ans_multiloss_16k_base` (`load_denoiser=True`, the
  default!) and, via `wetext`, text normalisation (`normalize=True`; default `False`). Their sizes/hashes were not
  looked up.
- **Code:** PyPI `voxcpm` 2.0.3 (2026-05-11, Apache-2.0) = GitHub tag `2.0.3`, commit
  `19b6bf7590025418821a86dcb817504e0ad7e5df`. GitHub main is newer (`cce58a5b59303c9bd63d12f23afe6a49a4a80c59`,
  2026-10-08). `requires-python >=3.10`; `torch>=2.5.0`, `torchaudio>=2.5.0`, `transformers>=4.36.2`, CUDA >=12
  (README). Hard dependencies are heavy: `gradio<7,>=6`, `funasr`, `modelscope>=1.22`, `datasets<4,>=3`,
  `torchcodec`, `matplotlib`, `spaces`, `wetext`, `inflect`, `addict`, `argbind`. Install torch cu128 first
  (`torch==2.8.0 torchaudio==2.8.0 --index-url https://download.pytorch.org/whl/cu128`).
- **Windows:** README does not mention Windows. The one real hazard is `optimize=True` (the default), which wraps the
  model in `torch.compile(mode="reduce-overhead")` and needs `triton`; the code (`voxcpm2.py` lines 278-294) catches
  a missing triton and prints "torch.compile disabled", but pass `optimize=False` and skip it. The warm-up
  generation also only happens when `optimize=True`. Audio is loaded with `librosa`, not torchaudio, so
  `torchcodec`/ffmpeg DLLs are not on the load path (BELIEVE; `torchcodec` is only listed as a dependency).
  `kaldifst` (needed by `wetext` 0.1.8), `pyahocorasick` and `editdistance` all have cp312 win_amd64 wheels.
- **No network at run time:** pass a folder as `hf_model_id` (`os.path.isdir` short-circuits `snapshot_download`),
  `load_denoiser=False`, `normalize=False`, and give `prompt_text` so no ASR is wanted.
- **Cloning call** ("ultimate cloning": reference audio plus its exact transcript; the card says to pass the same
  clip to both `reference_wav_path` and `prompt_wav_path`). Output is 48 kHz.

```python
from voxcpm import VoxCPM
import soundfile as sf

model = VoxCPM.from_pretrained(r"<ml-store folder of voxcpm2>", load_denoiser=False, optimize=False)
wav = model.generate(
    text="...",
    prompt_wav_path="ref.wav",
    prompt_text="exact transcript of ref.wav",
    reference_wav_path="ref.wav",
    cfg_value=2.0,                 # guidance scale
    inference_timesteps=10,        # diffusion steps; more = slower
    max_len=4096, retry_badcase=True,
)
sf.write("out.wav", wav, model.tts_model.sample_rate)
```

  Voice-design style prompts `"(slightly faster, cheerful tone)text"` also work with `reference_wav_path` alone.
  Reference-length limit: not found in the code read; the card says "Accepts 16kHz reference". UNKNOWN for 20 s.

## 3. CosyVoice 3 (FunAudioLLM)

- **Hub repos loaded at run time:** one, `FunAudioLLM/Fun-CosyVoice3-0.5B-2512` @
  `29e01c4e8d000f4bcd70751be16fa94bf3d85a18`. Needed: `cosyvoice3.yaml`, `CosyVoice-BlankEN/*` (Qwen LLM folder,
  `qwen_pretrain_path` in the yaml), `llm.pt` (2.02 GB), `flow.pt` (1.33 GB), `hift.pt` (0.08 GB), `campplus.onnx`
  (0.03 GB), `speech_tokenizer_v3.onnx` (0.97 GB). 5,427,029,103 bytes (5.43 GB). Not gated. Not needed here:
  `llm.rl.pt` (+2.02 GB, RL variant: README table shows lower WER, 1.68 vs 2.24 English, but lower speaker
  similarity, 69.5 vs 71.8), `flow.decoder.estimator.fp32.onnx` (+1.33 GB, TensorRT), `speech_tokenizer_v3.batch.onnx`.
  `spk2info.pt` is not in the repo and the code treats it as optional. No ASR model is used.
  The README also names `FunAudioLLM/CosyVoice-ttsfrd` (optional Linux-only text frontend wheels, `cp310 linux_x86_64`):
  skip it; the code then uses `wetext` or nothing.
- **Non-Hub downloads:** none if the model folder exists (`AutoModel` calls ModelScope `snapshot_download` only when
  the path is missing). `wetext` 0.0.4 (the pinned version, a 10 KB wheel) very likely fetches its FST files from
  ModelScope on first use (BELIEVE, from the wheel size; not confirmed). Do NOT install `wetext`: `frontend.py`
  wraps its import in `try/except` and falls back to no normaliser, after which English text is still number-spelled
  with `inflect` and split into <=80-token pieces (`text_normalize`), which is what a paragraph needs.
- **Code:** GitHub `FunAudioLLM/CosyVoice` (HTTP-redirects to `QwenAudio/CosyVoice`; same repository id 823430322),
  commit `074ca6dc9e80a2f424f1f74b48bdd7d3fea531cc` (2026-05-25). It is not on PyPI. It needs the submodule
  `third_party/Matcha-TTS` = `shivammehta25/Matcha-TTS` commit `dd9105b34bf2be2230f4aa1e4769fb586a3c824e` (MIT), which
  the code imports at inference (`matcha.models.components.*`, `matcha.hifigan.models`). Get both with
  `git clone` + `git checkout <sha>` + `git submodule update --init` (an inbound pull from public sources; GitHub's
  archive zips send no Content-Length, so I could not size or hash them for ml-store).
  README: Python 3.10; `requirements.txt` pins `torch==2.3.1`, `torchaudio==2.3.1` (cu121 index), `numpy==1.26.4`,
  `transformers==4.51.3`, `onnxruntime==1.18.0` (the win32 marker picks the CPU build), `conformer==0.3.2`,
  `diffusers==0.29.0`, `hydra-core`, `HyperPyYAML`, `x-transformers`, `openai-whisper==20231117`, `modelscope==1.20.0`,
  `lightning`, `gradio==5.4.0`, `pyworld==0.3.4`, `grpcio==1.57.0`, `tensorboard`, `wetext==0.0.4` ...
- **Windows:** On Python 3.10 every pinned wheel I checked has a win_amd64 build (`pyworld` 0.3.4, `grpcio` 1.57.0,
  `onnx` 1.16.0, `numpy` 1.26.4, `matplotlib` 3.7.5, `pyarrow` 18.1.0). On Python 3.12, `pyworld==0.3.4` and
  `grpcio==1.57.0` have NO cp312 win wheel and would try to compile (needs MSVC Build Tools: a system install), but
  neither is imported at inference, so on 3.12 use a hand-trimmed list. Recommended: Python 3.10 venv, and filter
  `requirements.txt` first: drop the two `--extra-index-url` lines (put the cu121 index on the command line for torch
  only), drop `wetext`, and drop the `deepspeed`, `onnxruntime-gpu`, `tensorrt*` lines (already Linux-only by marker).
  `deepspeed` and `tensorrt` are imported only by training/TensorRT code (`utils/train_utils.py`, `utils/executor.py`,
  `load_trt=True`) which inference does not load. `openai-whisper` builds from an sdist (pure Python; used only for
  `whisper.log_mel_spectrogram`; its mel filters ship in the package). `torchaudio.load` is called with
  `backend='soundfile'`, so no sox or ffmpeg. The speech-tokenizer ONNX session requests
  `CUDAExecutionProvider` when torch sees CUDA; with plain `onnxruntime` it falls back to CPU (BELIEVE: a warning, not an
  error; slower, fine for one clip). `onnxruntime-gpu` on Windows would need CUDA/cuDNN DLLs on PATH: skip it.
- **No network at run time:** pass `model_dir=<local folder>`; do not install `wetext`; env block.
- **Cloning call.** The prompt text MUST start with `You are a helpful assistant.<|endofprompt|>` (CosyVoice 3's
  format); the reference wav must be <=30 s (assert in `frontend.py`; 20 s is fine); the target text is spoken in
  pieces, so concatenate the yielded chunks. Output sample rate is `cosyvoice.sample_rate` (24 kHz).

```python
import sys
sys.path.append(r"<CosyVoice checkout>"); sys.path.append(r"<CosyVoice checkout>\third_party\Matcha-TTS")
import torch, torchaudio
from cosyvoice.cli.cosyvoice import AutoModel

cv = AutoModel(model_dir=r"<ml-store folder of cosyvoice3>", fp16=False)   # fp16=True is an option on the 4090
prompt = "You are a helpful assistant.<|endofprompt|>" + "exact transcript of ref.wav"
chunks = [j["tts_speech"] for j in cv.inference_zero_shot(text, prompt, "ref.wav", stream=False, speed=1.0)]
torchaudio.save("out.wav", torch.cat(chunks, dim=1), cv.sample_rate)
```

  A synthesis piece shorter than half the prompt text logs a "too short than prompt text" warning; with a 20 s clip
  (long transcript) short sentences trigger it. `inference_instruct2(text, "<instruction><|endofprompt|>", wav)` is the
  alternative mode for style instructions.

## 4. Higgs Audio v3 TTS (Boson AI)

- **Hub repos:** one, `bosonai/higgs-tts-3-4b` @ `239f63fb7b02b1aa085f98d9efae5e35cc5523e8`. The repo named in the task,
  `bosonai/higgs-audio-v3-tts-4b`, answers the API with `307 Temporary Redirect` to this id (the model card's Usage
  text uses the new id; the AGENTS.md and SGLang cookbook still print the old one). `model.safetensors` (9.31 GB,
  index lists 927 tensors in one shard), `model.safetensors.index.json`, `config.json`, `tokenizer.json`,
  `tokenizer_config.json`, `chat_template.jinja`: 9,321,366,076 bytes (9.32 GB). Not gated. The codec/vocoder weights
  are bundled in `model.safetensors` (SGLang-Omni `audio_codec.py`: "Codec weights are bundled inside the TTS
  ``model.safetensors``"), so there is no second Hub repo. The repo contains **no inference code**.
- **Non-Hub downloads:** none found.
- **Code:** third-party engines only. (a) SGLang-Omni: PyPI `sglang-omni==0.1.7` (the install page: "Docker is
  recommended for NVIDIA CUDA - UCX, flash-attn, SGLang, and CUDA are prebuilt"; `docker pull
  hongccc/sglang-omni:dev`, `uv venv -p 3.12`, `sgl-omni serve --model-path ... --port 8000`), repo commit
  `64d9b96bc4e927ef64463be42b3373d58d18cc93`. (b) vLLM-Omni recipe: Linux, Python 3.12+, CUDA 12.x, vLLM 0.22.0,
  `vllm serve ... --trust-remote-code --omni`, "1xH100 80GB minimum". (c) MLX-Audio on Apple Silicon; (d) a community
  ComfyUI node (`Saganaki22/Higgs_v3-TTS-ComfyUI`, third party, untrusted). The model's AGENTS.md says 24 GB "reported
  to work, not officially verified; expect to lower concurrency / `max_new_tokens`"; 40 GB (A100) is the known-good floor.
- **Windows:** not supported by any path found. Neither engine's docs mention Windows or WSL. `trust-remote-code` means
  vLLM-Omni would run the engine's modelling code. Realistic route: Docker Desktop with the WSL2 GPU backend, which is
  a system-level install the owner must approve, and 24 GB is below the stated floor. I judge it unlikely to run
  natively on Windows and not worth the install for an audition unless the owner wants Higgs specifically.
- **No network at run time:** `hf download` once to a folder, then `sgl-omni serve --model-path <folder>`; the local
  HTTP server binds `localhost:8000`.
- **Cloning call** (HTTP to the local server, fields from the model card and cookbook; `text` is the transcript, and
  the card says it "materially improves cloning fidelity"):

```python
import requests
r = requests.post("http://localhost:8000/v1/audio/speech", json={
    "input": "text to speak",
    "references": [{"audio_path": "ref.wav", "text": "exact transcript of ref.wav"}],
    "temperature": 0.8, "top_k": 50, "max_new_tokens": 1024,
})
open("out.wav", "wb").write(r.content)    # 24 kHz
```

  Inline control tags (`<|emotion:...|>`, `<|prosody:pause|>` ...) go inside `input`; unknown tags are read aloud.
  `audio_path` may be a local path only if the server was started with `--allowed-local-media-path <dir>`.

## 5. OmniVoice (k2-fsa)

- **Hub repos loaded at run time:** one, `k2-fsa/OmniVoice` @ `c5fdb5ccb189668d56333f77ba2629f4cd7535f4`: root
  `model.safetensors` (2.45 GB, fp32, a Qwen3-0.6B-based LM), `config.json`, `tokenizer.json`,
  `tokenizer_config.json`, `chat_template.jinja`, and `audio_tokenizer/` (`model.safetensors` 0.81 GB, `config.json`,
  `preprocessor_config.json`, `LICENSE`, `README.md`). 3,267,459,266 bytes (3.27 GB). Not gated. The code loads
  `audio_tokenizer/` from the folder; only if that folder is missing does it fall back to the Hub repo
  `eustlb/higgs-audio-v2-tokenizer` (not needed). With `ref_text` given, the Whisper ASR (`openai/whisper-large-v3-turbo`,
  loaded only by `load_asr_model()` when `ref_text` is omitted) is never touched.
- **Non-Hub downloads:** none. Optional extra `omnivoice[tn]` (WeTextProcessing, `num2words`) is for
  `normalize_text=True`; skip it.
- **Code:** PyPI `omnivoice` 0.2.1 (2026-07-16, Apache-2.0); GitHub `k2-fsa/OmniVoice` master
  `08be0b4ccbac3e13e374e86fbfead4b4cac343e2` (2026-08-24, newer: FlashInfer and LoRA additions). `requires-python
  >=3.10`; `torch>=2.4`, `torchaudio>=2.4`, `transformers>=5.3.0`, `accelerate`, `pydub`, `gradio`, `tensorboardX`,
  `webdataset`, `numpy`, `soundfile`, `librosa`. README install: `pip install torch==2.8.0+cu128 torchaudio==2.8.0+cu128
  --extra-index-url https://download.pytorch.org/whl/cu128`, then `pip install omnivoice`.
- **Windows:** README names macOS MPS and Intel XPU, not Windows; nothing in the code needs triton, DeepSpeed, sox or
  espeak. FlashInfer (`omnivoice_flashinfer.py`) is optional and a Linux wheel index: skip it. `pydub` needs ffmpeg
  only for non-WAV formats; ffmpeg is already installed, and the reference loader tries `soundfile` first.
- **No network at run time:** pass the folder path to `from_pretrained` (`_resolve_model_path` returns directories
  as they are); give `ref_text`; env block.
- **Cloning call.** The card says to use a 3-10 s reference ("Longer audio slows down inference and may degrade
  cloning quality"). The code only warns above 20.0 s (`ref_duration > 20.0`), and does not trim when `ref_text` is
  given, so a 20 s clip is allowed; test a 3-10 s excerpt with its matching transcript beside it. Cross-language
  prompts carry the reference's accent.

```python
import torch, soundfile as sf
from omnivoice import OmniVoice

model = OmniVoice.from_pretrained(r"<ml-store folder of omnivoice>", device_map="cuda:0", dtype=torch.float16)
audio = model.generate(
    text="...", language="en",
    ref_audio="ref.wav", ref_text="exact transcript of ref.wav",
    speed=1.0,                     # >1 faster; or duration=<seconds>
    # generation_config=OmniVoiceGenerationConfig(num_step=32, guidance_scale=2.0, t_shift=0.1,
    #                                              position_temperature=5.0, class_temperature=0.0)
)                                  # list of float32 arrays at 24 kHz
sf.write("out.wav", audio[0], 24000)
```

  The clone can be cached: `prompt = model.create_voice_clone_prompt(ref_audio=..., ref_text=...)`,
  `prompt.save("voice.pt")`, then `generate(text=..., voice_clone_prompt=prompt)`.

---

## System-wide installs: summary

- None for Chatterbox, VoxCPM2, CosyVoice 3 or OmniVoice, given the settings above.
- Higgs: Docker Desktop plus WSL2 GPU support (or a Linux machine). Owner approval needed.
- Possibly, only if a pip package has to compile from source (not expected with the choices above): MSVC Build Tools.
  This arises for CosyVoice's `pyworld==0.3.4` and `grpcio==1.57.0` on Python 3.12 only; avoided on 3.10.
- `ffmpeg` (present) is optional; `espeak-ng` and `sox` are not used by any of the five.

## What I could not establish

- (Settled afterwards: pip resolves and the packages import on the CPU for all four, see "Installed environments". Not settled: whether any model loads or runs on the GPU.)
- Whether `wetext` 0.0.4 and VoxCPM's ZipEnhancer fetch from ModelScope on first use, and their sizes and hashes
  (both avoided by the settings above).
- The CC-BY-NC version for OmniVoice's weights; the Meta Llama 3 Community License and Acceptable Use Policy text
  that the bundled Higgs Audio 2 tokenizer licence incorporates; Boson's Acceptable Use Policy; Qwen's licence for
  the `CosyVoice-BlankEN` folder.
- VoxCPM2's and Higgs's behaviour with a 20 s reference (no limit found in the code read for VoxCPM2).
- Whether Higgs fits and runs in 24 GB (the model's own AGENTS.md says "reported ... not officially verified").
- Which of PyPI 0.1.7 and GitHub master is the better Chatterbox pin (they differ although both say 0.1.7).
