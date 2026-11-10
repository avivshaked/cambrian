# Proposal: a film pipeline that finishes in a day

**For:** the owner. **From:** the coordinator, 2026-11-10. **Status:** a proposal, for a ruling after round 67's film
ships. Nothing here changes round 67.

## What went wrong with round 67

Round 67's film took five days, and most of that was rework the process caused. Each cause below is drawn from the
record (DECISIONS D329 to D349, HANDOFF, `logbook/specs/r67-film-look/`).

1. **The words came first, and the world was searched for pictures to fit them.** The shot list was written from the
   narration, and its moments were then hunted for in the runs. Several were not in the replays the theatre films,
   which part from their run within seconds of a checkpoint: 3.1a's cluster, 2.1a's crowd beneath a tree. Others were
   there but the camera could not frame them.
2. **The look changed during filming.** Each change to the shared look made every clip filmed before it stale.
   It changed three times on 2026-11-10 alone (D343 to D345), then again for the bubbles, the key and the flakes.
3. **The bar rose during production.** The bar for amazing came on 2026-11-07, the focus rules on 2026-11-09, the
   hero sizes after them. Each rise failed footage that had passed.
4. **An automatic camera was tuned fault by fault.** More than thirty dials were added, each one after a render and a
   review had found a fault, hours later. Seven stills rounds passed two of twelve hard shots.
5. **Renders were slow, and nothing measured it.** A log line that printed for every body on every frame took about
   5.5 of a scene's 7 minutes. Skins were built for bodies the fog hides. Both had cost hours for days before anyone
   noticed.
6. **Too many hand-offs.** Three sessions, pause files and chained scripts passed every step from one to the next by
   message.

## The proposal

Six changes, one for each cause.

### 1. Moments before words

A new step comes after the facts and before the story. A tool mines the film's runs for **moments**, each one an event
a rig can film: a birth, a bite, a crowd, a portrait, a canopy from below, a descent. It does three things:

- it records each candidate's window on the farm, as the films do now;
- it scans that window's own frames for the event's bodies;
- it runs the rig's preflight on the moment and keeps only those whose numbers pass.

The result is a catalogue of filmable moments, each with its subjects' ids, its rig spec and its numbers. The story,
the prose and the shot list choose their pictures from the catalogue and nowhere else. When a line of narration needs a
picture the catalogue lacks, the writer changes the line or asks the miner for a named moment. The camera is never
asked to find a picture that is not there.

### 2. The look is frozen before a film starts

Look work moves to its own phase between films, in the safari and the theatre, where it already belongs ("the films
follow the safari"). A film records the look's digest when it starts and films every shot on it. A change to the look
that the owner wants mid-film waits for the next film, unless the owner rules otherwise for that one change.

### 3. The bar is set at the start

At a film's start the owner approves a reference film and a short list of numeric bars: luma, tone, edges, the
hero's share, margins, and the fog. The reviewer's brief and the rig's gate read the same list. The list holds until
the film ships. A rise in the bar applies to the next film.

### 4. Every shot through a rig and its preflight

The rig built on 2026-11-10 (D347) frames named subjects directly and prints its numbers in about a minute, before any
frame renders. Every shot type gets a rig type: portrait, birth, group, canopy from below, descent, card. The
automatic director's dials are retired for films. The stills round becomes one confirming look per shot, and stops
being the search loop.

### 5. A speed budget on every theatre build

Every theatre build renders a fixed reference scene and reports minutes per scene and seconds per frame. A build over
its budget fails like a failing test. Distance culling of skins and drawing (`SKIN_CULL`) becomes the default once its
pixel proof passes.

### 6. One script runs the film

`film.py` grows into one orchestrator that runs mine, preflight, stills, review, film and collect end to end. It stops
only at a gate that needs a judgement, the owner's or a reviewer's, and messages the owner when it does. The theatre
lane builds tools between films. It no longer hands each step of a film over by message.

## What it would cost, and the target

- **Build:** about two to three days of agent work after round 67 ships. The miner and the rig types are the largest
  parts. The speed budget and the orchestrator are smaller. The film skill's steps 1 to 7 change, so its table and
  briefs are rewritten too.
- **Target:** a film in about one day of mostly unattended machine time, with the owner needed at three gates only:
  - the bars and the look at the start;
  - the moments and the story;
  - the cut.

## Rejected

- **Narration first, as now.** It is what made the camera search for pictures that were not there.
- **Hand-placed cameras for every shot.** That would be precise, but it costs an agent's or the owner's time on every
  shot of every film. A rig with named subjects gives the same precision by rule.
- **Keeping the automatic director and capping its retries.** The owner rejected a cap on 2026-11-10 (D347).

## Questions for the owner

1. Do moments come before words: the story and the narration written around a catalogue of filmable moments?
2. Is the look frozen at a film's start, with a mid-film change only by the owner's ruling for that one change?
3. Are the bar and the reference fixed at a film's start, and held until the film ships?
4. Does the build start after round 67's film ships, or alongside its last steps?
