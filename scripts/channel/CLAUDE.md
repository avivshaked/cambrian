# The channel's title films

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before rendering the channel's intro or outro in Blender. The folder's README says how the films are made.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Gotchas

- **A Blender render launched with factory settings denoises on the processor.** `--factory-startup`
  leaves OpenImageDenoise's GPU switch off, so the channel intro's 4K frames took 42 s each, 28 of
  them denoising, with the card idle between one-second bursts (2026-09-28; the owner saw the bursts
  before the agent timed the stages). `scripts/channel/theatre_scene.py` turns it on and puts the
  compositor on the card: 12 s a frame, and no pixel more than 7 levels in 255 from the processor's.
  `EVOSIM_PROFILE=1` on `scripts/channel/intro.py` prints every stage's time; the folder's README
  has the rest of how the title films are made.
  The rendered intro and outro are not in git. They live in `assets/cards/` on the machine that
  made them, so check that they are there before a film uses them, and render them again from that
  README when they are not.
