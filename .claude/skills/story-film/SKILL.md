---
name: story-film
description: Make a round's story film for the owner end to end, or pick one up where it stopped - guides, the arc, the writer, the draft review, the script edit, narration, the owner's reviews, filming, the Resolve timeline and delivery. Use when the owner asks for a round's story or a film of how a round went, or to resume a story film in progress.
---

# Story film

Any session that runs this flow has to reach the same film, so no step of it is left to the
session's judgment (CLAUDE.md, "A subagent that a skill launches is briefed from the skill's files
alone"). Every verdict comes from a tool, from a subagent launched with a fixed prompt whose answer
is saved as it came, or from the owner's words saved as they wrote them, and `flow.json` in the
story's folder records each one against a hash of what it was about. The procedure and the stage
table are in `logbook/specs/story-film.md`; read it before a story's first session.

One command runs the flow:

```
python scripts/story-flow.py next scratch/story/<round>
```

It does the next mechanical step itself (a check, an apply, a page for a reader, a package for the
owner) and prints what comes after. `story-flow.py status <folder>` prints the same table and
changes nothing.

## The session's part

1. **Start a story**, once every seed of the round has ended:
   `story-flow.py init scratch/story/<round> --round N --runs <arms...>`, then
   `story-flow.py set <folder> entry <the logbook entry that reports the round>` and
   `story-flow.py set <folder> prereg <the entry that pre-registered it>`.
2. **Run `next`, and do what it prints**, then run it again, until it says the film is delivered:
   - **agents**: launch each subagent it prints, as the agent type and on the model it names, in the
     background, with its prompt word for word and nothing added. When a reader's task ends (the
     listener, a fact checker, the cold reader, the arc comparer), run
     `story-flow.py save <folder> <its agent id>`, which copies its answer from its transcript and
     records its verdict. When the writer's or the editor's task ends, run `next`.
   - **run**: run the command it names, when the machine allows it (HANDOFF's load ruling: a heavy
     job never starts beside another).
   - **owner**: the owner rules. Lead with "I am blocked on ... from you", give them the full path of
     the file `next` wrote under `scratch/owner/`, and set out the decision in full, copying any
     choices from that file as they stand. Save their reply whole and word for word to a file in the
     story's folder, through the shell, and run
     `story-flow.py owner <folder> <stage> <that file> --verdict <the verdict they gave>`. A reply
     with no verdict gets a question back, never a guess.
   - **wait**: nothing to do until what it names has happened.

## What the session never does

- Judge a draft, a script round, a reader's answer or a picture. The verdicts are the tools', the
  readers' and the owner's.
- Write or change a word of the story or the script, or a note a subagent reads. The writer writes
  the story, the editor edits the script, and a subagent reads only its brief, the story's files,
  tools' output, other subagents' answers and the owner's words.
- Launch a subagent the flow does not name, or change a prompt. The prompts are in `prompts.md`
  beside this file, and `next` prints them filled.
- Edit `flow.json`, a verdict file or an owner file by hand.

## Changes after a stage is done

- **The owner asks for changes** at the script or the timeline review: `--verdict changes`. That
  opens an owner's round of the script stage, in which the editor makes every change the page can
  carry and lists the rest for the writer; the writer makes those from `changes.md`, and the draft
  review and the script stage run again on what it hands back. The script approval is asked for
  again, and `status` names every scene to film again.
- **An approval goes stale** when what was approved changes. `status` says so, and the owner sees
  the change before approving again.

## Filming, assembly and delivery

`next` names each step with its command: the director's check per run (`theatre-safari.ps1 -Check`,
then `story-flow.py mark <folder> check <arm> --log <its editor log>`), the film by the window route
of `logbook/specs/story-film.md` section 5, the Resolve timeline by the `story-resolve` skill, the
owner's timeline review, the glossary rows (`story-flow.py glossary <folder>`), and the delivery to
`scratch/owner/`.

## Rules

- A heavy job (a render, a narration job, a farm run) never starts beside another. Read the
  machine first, under HANDOFF's current ruling.
- A subagent waits on nothing. The writer and the editor write their own files through the shell (a
  heredoc or a Python write, never the Write or Edit tools), and only inside the story's folder.
  The readers write nothing.
- Every owner review is a blocking ruling: lead with "I am blocked on ... from you", set the
  decision out in full, and keep working on what is not gated.
