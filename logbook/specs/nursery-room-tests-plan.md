# The nursery room's tests: a final walk, end-to-end flows and saved answers

Written 2026-10-04 and agreed by the owner the same evening, every step as written and option A
for the conflict. The room is at `e824ed4` on
`nursery-room`. Its suite has 657 tests. Most check the server's logic. The rest search the page
scripts' source for strings, which catches a deleted line but not a broken screen. Every real fault
on a screen so far was found by an agent driving headless Edge: the empty RECIPE tab, the
letterboxed chart, the clipped brains panel. Each such walk costs a few hundred thousand tokens and
repeats much of the last one.

The plan has five steps. Steps 1 and 2 come first because they settle where the room stands today.
Steps 3 and 4 turn what the walks check into tests that run with the suite. Step 5 writes the rule
down.

## 1. Hand the merge's conflict to 6e

This needs the owner's choice. Merging `nursery-general` into `nursery-room` (`e824ed4`) met one
conflict, at the end of the search in `Tank.cs`. Both sides were kept: the population file is
written first, then the exam runs on its finalists by their ids. The owner wants the coordinator,
whose change it was, to handle that conflict. There are two ways:
- **A. Keep `e824ed4`, and 6e reviews it** (recommended). Nothing is rewritten. If 6e disagrees, it
  fixes the conflict in a new commit. Both suites pass on the merge.
- **B. Undo the merge, and 6e merges again.** `nursery-room` goes back to `4d36925` by one local
  reset. It can be recovered from the reflog and was never pushed, but it is a history rewrite,
  which only the owner may approve.

Done when 6e has the conflict and has said so.

## 2. One full walk of the room as it stands

A Sonnet agent walks all five screens in headless Edge, at 1920×1080 and 1366×768, on the real
runs first. The brief is the last full walk's (`design/nursery/briefs/room6-walk4-brief.md`),
with its list of claims replaced by everything built since that walk.

That covers changes no walk has seen:
- the merge
- the greyed cells' titles
- the control's side left ungreyed
- the recipe labels' hover text
- the launch dialog's wording

The walk also writes down each check it makes as a step: what was done, what was measured, and what
counts as passing. Step 3 builds its flows from that list.

Faults go to a Sonnet fix round, which an Opus agent reviews, and then a short walk of what changed.
The leftovers already known stay out unless the owner asks for them:
- dots drawn in full colour under a greyed column
- the cut-off panels at 1920×700
- the tank screen finding a recipe through the registry only
- "not in the base → null" on a trial's diff
- the jargon labels

Done when a walk finds nothing new.

## 3. End-to-end flows in the room's test project

**How.** A small C# helper drives headless Edge over its DevTools protocol, using
`ClientWebSocket`, as the walks' PowerShell driver does. It adds no dependency to the project.
Playwright would do the same job but is a package to bring in, and Edge is already on the machine.
- Each test hosts the room in its own process on a free port, through `Program.Create`, with the
  fake launcher. This is how `RoomSmokeServer` already does it.
- The data is runs and recipes that the test makes, as the fixtures already do. The real runs live in
  `scratch/` and are not in git.
- Where Edge is missing, a flow is skipped and says why.
- A flow never writes through the room.

**Six flows:**
1. **Registry.** The registry lists the fixture runs, and opening one reaches its run screen.
2. **Run screen with a control.**
   - The exam table, a greyed column with its reason, and the by-body plot are drawn.
   - At 1920 the band is one row.
   - Each chart is drawn at its box's size.
   - The brains panel has no inner scrollbar.
3. **Recipe screen.** The committed plate matches a test repository's git state. The LAUNCH dialog
   opens and cancels with the right note, and nothing is posted.
4. **Tank screen.** An out-of-range address is refused in the tank page's own words.
5. **Genome screen.** A genome loads with its body and brain.
6. **Every screen at both sizes.** No console error, no exception, no failed request, no page wider
   than the window, and no text running out of its box.

**Layout is checked by measuring.** The flows measure the page, as the walks did: a chart's drawn
size against its box's, a panel's scroll height against its client height, and boxes that overlap.
Layout is not checked against screenshots.

**Cost.** The flows run with the default suite, one Edge at a time. The target is under a minute
added to the suite's 11 seconds. If they come out slower, they go behind a trait that the default
run includes and `--filter` can leave out.

Done when the six flows pass on `nursery-room`. Each must also fail on purpose once, against a
fault put in by hand: the old letterboxed chart, and a removed greyed reason.

## 4. Saved answers for the resources agents read

Agents read the room through its MCP, so a change in a resource's answer can break an agent without
breaking any screen.
- **What is saved.** For each of these, one answer on the fixture data is saved as a file beside the
  tests:
  - `nursery://registry`
  - `nursery://run/{id}`
  - `nursery://recipe/{path}`
  - `nursery://exam-compare/{a}/vs/{b}`
  - one refused read
- **Masking.** Fields that change from run to run are masked before the comparison: the clock, ages,
  file times, the machine's load, process ids and the test repository's hashes.
- **On a mismatch.** The test fails and shows the difference.
- **Re-recording.** An answer is saved again only when someone sets `ROOM_GOLDEN_UPDATE=1` on
  purpose, and the commit that changes a saved answer says why.
- **Screenshots are not saved.** Fonts, anti-aliasing and live values change between runs, so pixel
  comparisons fail for no reason. Images would also press on the 5 MB commit limit.

Done when the five saved answers pass. A change to one field's name must make its test fail.

## 5. The rule, in `nursery-room-build.md`

A change to a screen is not called working until the six flows pass. A browser walk by an agent is
still needed for a new screen or a new panel, and for anything judged by eye. Every walk ends by
adding its new checks to the flows, so the same fault is never paid for twice.

## Order, people and machine

| Step | Who | Gate |
|---|---|---|
| 1 | the owner chooses; a8 tells 6e | the owner's choice |
| 2 | a Sonnet walk; a Sonnet fix round if needed; Opus review | none |
| 3 | a Sonnet builder; Opus review | step 2's list of checks |
| 4 | the same builder, a second brief; Opus review | none |
| 5 | a8 | steps 3 and 4 |

- **Machine.** Builds and tests stay at 4 logical processors or fewer while round 56 runs. Edge runs
  one at a time.
- **Asking first.** The owner ruled on 2026-10-04 that the coordinator (6e), who runs the rounds,
  gives permission before anything heavy runs on the machine.
  - Heavy covers every browser walk, every build round by a subagent, every full test suite and
    every nursery run.
  - Each is asked for in one line before it starts: what it is, how many processors it takes, and
    how long it should last.
  - It starts only after the coordinator says yes.
  - A single filtered test while editing is not heavy.
- **Tokens.** By the walks and builds so far, each step costs roughly:
  - step 2: 200k to 350k for the walk, more if a fix round is needed
  - step 3: 250k to 400k for the build, and about 50k for the review
  - step 4: about half of step 3
- **Payback.** After this, a change to the room usually needs no walk, and the flows catch a fault
  in seconds.
- **Commits.** Everything is committed on `nursery-room`, locally. Merging into main waits for the
  owner's later ruling, as before.

## Changes agreed on 2026-10-05

Step 1 ended with 6e keeping the merge. Step 2's walk ran on 2026-10-05. It passed every item it
was given and found six small faults. Its list of checks is in
`design/nursery/briefs/room7-walk7-checks.md`. The owner then agreed these changes:
- **The room is on main.** The coordinator merged `nursery-room` into main's merge on 2026-10-04.
  On 2026-10-05 the owner said they support the room staying on main unless the coordinator has a
  strong objection. Steps 3 and 4 build on branch `room-e2e` from main, which the coordinator
  merges once its suites pass.
- **Step 3 carries five of the walk's faults:** the LAUNCH note for a git state that is not known,
  "1 threads", the RERUN dialog's thread line, a "—" cell drawn greyed, and the RECIPE tab not
  following the registry's selection. Step 3 then closes step 2, with a short look at what changed
  in place of a second walk.
- **Accepted as by design:** the tank screen scrolls at 1366×768. The known leftovers stay out for
  now, since it is too early to know which will matter.
- **The activity log is committed whole.** The owner ruled that every row of
  `rounds/nursery/activity.jsonl` is committed, the reasons typed on the screen included. Every
  reason box says that what is typed there is kept in git and may be published.

## Where it ended, 2026-10-05

All five steps are done. Step 2 was closed by step 3's fixes and a short look in Edge. Step 3's flows
are on main (`b751dc5`), step 4's saved answers are on `room-golden`, and step 5's rules are in
`nursery-room-build.md` under "Rules for the build". What was built, and the faults left by choice,
are in that file's "What was built".
