---
name: status
description: Give the owner a status of the project in this chat - what is blocked on them, what happened since the last status, what to look at, what needs their feedback or decision, the machine, the push, the other sessions and what comes next. Use only when the owner asks for a status, an update or "where do we stand".
---

# Status

The owner asks for this a few times a day, and reads it to decide what to do next. It has to be clear and give the
context; a reader who skimmed it should still know what was found, what it means, and what is asked of them. It goes
in the chat as one message. It writes no status file: HANDOFF.md holds where the work stands, and this reads it.

## Gather

1. `cmd //c start "" //b //wait //belownormal //affinity 300000 python -I scripts/status-gather.py` from Git Bash at the
   repository root (pinned off the farm's cores; safe beside a card run). It prints the window since the last status
   and, for that window: runs running and ended, pace, heat, hardware events and the last boot, processes and committed
   memory, disk and orphans, the push and the commits, new files under `logbook/`, `proposals/` and `scratch/owner/`,
   log tails, and the queue's Running section.
2. HANDOFF.md: the decisions list and the agents table. `scratch/machine/queue.md` for what is next.
3. `ListAgents` for the sessions open on this machine. For each peer, what it is doing comes from HANDOFF's agents
   table, the queue and its last message to this session.
4. Read every new read's README in the window. A number that goes to the owner is checked against its file first, and
   one with an open caveat is settled or said to be open.
5. Look at every new picture or sheet in `scratch/owner/` yourself before naming it, and say what you saw.

## Write, in this order, every time

1. **Blocked.** The first line is either "I am blocked on <decision> from you" for each decision the next step waits
   on, or "Nothing is blocked on you."
2. **Since the last status.** In plain words: what ran, what ended and how, what each result means, what failed or
   surprised. Inference is marked as inference. Ids and paths come after the plain words, if at all.
3. **To look at.** Each new picture, sheet or film, as a markdown link relative to the repository root with its full
   Windows path beside it, what it is checked against, and what was seen.
4. **Decisions.** Every open decision, the blocking ones first, each in full in prose: the topic in plain words for
   someone who has not read the proposal, the recommendation and why, what each option implies for the record, the
   rounds, the machine and the risk, and the exact question. Never shorthand, never "unchanged from last time", never a
   pointer to a file instead of the topic. A decision that no longer needs the owner is dropped, with one line on why.
5. **Feedback and the owner's own actions.** Each ask carries a recommendation. A command for the owner to run is
   tested first in the owner's shell, which is PowerShell (`rd` there is `Remove-Item`; use `cmd /c rd /s /q "..."`),
   and is given one per line in a code block.
6. **The machine.** Heat over the window (CPU maximum, seconds at 85, 90 and 95 C, GPU maximum, and from when the log
   reads), driver errors and restarts, Windows updates and whether a restart is pending, the pace of every running world,
   committed memory and stray processes, disk and the size budget.
7. **The push.** Hours or days since the last push and the count of unpushed commits; past three days this moves to the
   first lines. What the commits hold besides code and prose. A push waits for the owner's yes each time.
8. **Sessions.** One line per session (this one and each peer): what it is doing or waiting on, and a recommendation:
   keep it, close it (its work is done or parked, and it holds context or memory the machine needs back), or open a new
   one (a lane that has work and no session, or a session near its context limit), with the pickup prompt for a new one.
9. **Next.** What this session works on meanwhile, and the queue's next steps.

## Then

Run the gather script again with `--mark`, so the next status starts its window here. Nothing else is written.
