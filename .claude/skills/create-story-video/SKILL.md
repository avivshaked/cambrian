---
name: create-story-video
description: Make a story video from one, two or three experiments - edited, narrated, subtitled, with graphics, ready to share with an audience. Being built - on every invocation the skill first says which steps are implemented and which are not, and asks before running any.
---

# Create story video

The end product is an edited video, narrated, subtitled and with graphics, ready to share with an
audience. A film covers one, two or three experiments.

**This skill is being built. The table's Implemented column says which steps are implemented; every step marked no still needs to be.**

## When invoked

1. Before anything else, show the owner the table below and say which steps are implemented and
   which are not.
2. Ask the owner whether to run the implemented steps. The steps run in order, each from the files
   the steps before it hand over, so only the implemented steps before the first unimplemented one
   can run. If there are none, say so and stop.
3. Run nothing without the owner's yes, and never do an unimplemented step by hand.
4. If the steps to run include the narration, first check that this session can reach the narration
   service, as [narration.md](narration.md)'s step 1 says. If it cannot, offer the owner the three routes
   that step gives.

## The flow

| # | Step | Implemented | Who | Reads | Hands over | Checked by | Budget |
|---|---|---|---|---|---|---|---|
| 1 | [facts-gathering](facts-gathering.md) | yes | a subagent | the logbook entries or rounds given, and anything else it needs; later, any file of questions a writer or the owner asks | `facts.md` in `scratch/story-video/<name>-<yyyymmddhhmm>/`, and a section added to it for each file of questions | a source checker on 15 facts, then once more after a fix, then the owner | 30 min for a new film, 10 for the section on the project or a file of questions, 10 for each source check |
| 2 | [story](story.md) | yes | subagents and the owner | `facts.md` | `story.md` in the film's folder: the story the owner approved | a fact checker, a cold reader and a comparer, then the owner approves | 10 min for the angles, then 15 for each draft and 10 for its checks |
| 3 | [prose](prose.md) | yes | subagents and the owner | `facts.md` and `story.md` | `prose.md` in the film's folder: the narration's words, as prose the owner approved | a line editor in up to two rounds, a tool, a fact checker, a cold listener and a comparer, then the owner approves | 15 min for a draft, 5 for each editor round and 10 for each revision, then 10 for its checks |
| 4 | [script](script.md) | yes | subagents and the owner | `prose.md`, `story.md` and `facts.md` | `script.md` in the film's folder: the prose cut into passages, each with its picture, its facts and any words on screen, in the form [script-schema.md](script-schema.md) fixes, that passed its check and that the owner approved | a tool and the composer's own review, then the owner approves | 15 min for each draft |
| 5 | [narration](narration.md) | yes, run once: round 49 in a test folder, up to the owner's verdict on its first draft; `keep` has not run | a subagent, a tool and the owner | `script.md` and `prose.md`, and the skill's `narration-settings.json` and `pronunciation.json`, copied into the film's folder the first time | `narration.json` and `narration/` in the film's folder: the pieces the owner approved by ear, and each piece's, passage's and sentence's place on the film's timeline, padding included, in the form [narration-schema.md](narration-schema.md) fixes; without the service, the owner's own files or an estimate | the tool, then the owner by ear | 5 min for the tool's work, and 60 for each narrator run |
| 6 | [shot list](shot-list.md) | yes, not yet run: tested on round 49's script with an estimated narration | the shot-lister (a subagent) and a tool | `script.md`, `narration.json` and `facts.md` | `shots.json` in the film's folder: every shot on the narration's timeline, each with its footage and any graphic laid over it, in the form [shots-schema.md](shots-schema.md) fixes | the tool; the owner does not approve it (their ruling of 2026-09-27) | 15 min for each draft, three drafts before the owner rules |
| 7 | [filming](filming.md) | yes, not yet run: tested with made-up clips against round 49's record; nothing has been rendered | the filmer (a subagent), the window route's farm and theatre, and a reviewer | `shots.json` and the runs | `clips/` and `clips.json`: a clip of every shot's footage, a graphic's included, with 10 s handles | the tool, a reviewer from the sheets, then the owner on any fault | 20 min for each filmer run and 15 for the review; the render's pace is not measured yet |
| 8 | [graphics](graphics.md) | yes, not yet run: its renders tested in the real Blender and its stills with a stand-in for the theatre; the stills entry has never run | an animator (a subagent) for each graphic, a tool, the theatre for the stills, and a reviewer | `shots.json`, `facts.md` and `script.md` | `graphics/` and `graphics.json`: transparent clips of every graphic and line of words on screen, and stills of the bodies they show | the tool, a reviewer from the sheets, then the owner on any fault | 25 min for each graphic and 15 for the review |
| 9 | [edit](edit.md) | yes, not yet run: tested on a made-up film; no build in Resolve has run | the editor (a subagent), a tool and the owner | `narration.json`, `shots.json`, `clips.json` and `graphics.json` | a proof of the film, and a timeline in DaVinci Resolve that the owner finishes | the tool's layout and proof, the editor, then the owner watches the proof and approves the timeline | 15 min for each editor run, three drafts before the owner rules; minutes for a proof |

The shot list comes after the narration because every timing in it comes from the narration. The script's directing is a first pass: the narration adds each passage's span on the film's timeline, padding included, and each sentence's time except on the owner's own files, and the shot list then directs the film again with those timings, and may change a passage's picture and its words on screen, and cut a passage into several shots. The spoken words, the holds and the passage numbers stay as the script has them, since the narration speaks the words, leaves the holds as silence and is keyed on the passage numbers.

## Rules

- One job per step.
- A step's tests stand in for the farm, the theatre, Unity and Resolve, so a first real run finds what
  they cannot. Every such fault is fixed in the step's tool with a test that reproduces it, and written
  down in the step's file, so that it cannot come back.
- Nothing is rendered in full before its look is agreed on a test of a few shots or frames. The agent finds
  the best settings itself, from sheets read against the reference. It brings the owner one proposal and
  the sheet it rests on. The owner, 2026-09-28: "i want you to find the best settings. you can see the film
  in the sheet. i also want you to stop wasting time by rendering a whole bunch of things before we agree
  on the right settings".
- Each step runs in its own subagent, to protect the main session from context bloat and context
  corruption.
- Each subagent runs on the cheapest model that can do its work (the owner, 2026-09-28). Sonnet runs
  one that follows a procedure, runs tools and quotes what they print, or reads cold. Opus runs one
  that writes, checks against the facts, plans or edits, and every one that judges a picture: a
  frame, a sheet, a render or a proof. Fable runs only work that Opus has tried and could not do.
  Every step's file names each role's model, and a new role is given one by this rule.
- The facts step does all the reading of the runs and writes a checked facts file.
- The writer writes only words from that file: no runs, no code.
- Steps hand over files only, never the session's notes.
- Every step has a time budget, and the flow reports an overrun.
- A verdict comes only from a tool, a subagent's answer saved word for word, or the owner's words.
- The question and answer the owner picks stay the same to the end, in every video. A film that
  doesn't deliver what its opening promised loses the viewer's trust
  (research/story-video/compelling-video-stories.md §1.10 to 1.13).
- Evolution is told as what happened to populations, never as what creatures wanted, tried or
  learned. That language teaches the commonest misconception about evolution
  (research/story-video/compelling-video-stories.md §6.8; research/story-video/narration-writing.md §2.6).
- The prose writer, when it needs a fact `facts.md` does not hold, asks for it in a file of questions
  and stops, and so can the owner's words on a draft of the prose or the script. The main session then
  launches the facts gatherer on that file to add the answers to the end of `facts.md`, has them
  source-checked, and goes on with the step (facts-gathering.md, "Questions to answer"). The session
  does the launching because a subagent cannot launch another.
- A name we give, one `facts.md` does not hold, such as a creature's nickname, is written between
  double braces, as in {{Big Blue}}, in every file from the prose on. The owner picks it, the fact
  checks leave the name itself alone, and the braces come off only where the words reach the viewer:
  the voice, the subtitles and the screen. `script-check.py say` prints the words without them.
- Every step that writes words is checked against `facts.md`. Viewers can't tell a wrong science
  video from a right one, so the check is ours (research/story-video/compelling-video-stories.md §6.7).
- The owner judges quality. No AI is asked which version is better, because models judge that badly
  (research/story-video/human-not-llm-prose.md §1.4, §2.C).
- For now every film is one horizontal YouTube video of about 5 to 7 minutes, and there are no
  shorts: a film has to explain a world no viewer knows, which a short cannot hold. Engagement with
  course videos fell off past about six minutes, and educational videos of one to five minutes held
  viewers for over half their length (research/story-video/compelling-video-stories.md §2.1, §3.6), so
  the minutes past six have to earn their place.
