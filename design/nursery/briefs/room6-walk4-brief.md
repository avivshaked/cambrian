# Walk brief: the nursery room after round B1b, all five screens

Read `D:\Projects\experiments\evolution-simulator\scratch\nursery-general\room6\inspect-brief.md` first.
Its rules, its way of driving the browser and its "What to do" all hold here, with these changes:
- **Write only under** `D:\Projects\experiments\evolution-simulator\scratch\room6-walk4\`. Use your own
  Edge profile there, and a debugging port other than 9333. Never write to your session scratchpad, to
  the Windows TEMP directory, to `/tmp`, or anywhere else.
- **Reuse the last walk's driver.** Copy `cdp-lib.ps1`, `q.ps1`, `walk.ps1`, `poll-all.ps1` and `steps\`
  from `D:\Projects\experiments\evolution-simulator\scratch\room6-reinspect3\` into your folder, and
  read its step files to repeat its walk. Do not edit the originals. That walk's screenshots are in its
  `shots\`, for comparison.
- **All five screens:** `registry.html`, `recipe.html`, `run.html`, `genome.html` and `tank.html`,
  through the top bar's tabs, at 1920×1080 and 1366×768.
- **Walk on the real runs the room lists first** (pilot-deep-brain2, accept-1gen, accept-stop), and on
  a synthetic run only for what no real run can show. Name which is which.
- **Never start a shell command with `cd`.** A denied or blocked command is final, even for a file of
  your own: do not reach the same end by another command or tool. Leave the file and report it.
- **Change nothing through the room.** On the tank screen, never press STEP to completion. You may
  open its reason prompt and cancel it. Never press STOP or RERUN past their prompt.
- At most 4 logical processors: one headless Edge at a time.

The server on 5179 now runs a build that claims these, on the run screen unless named:
1. Every chart redraws when its box changes size, not only when the window does: no letterboxed
   search chart at 1920 on a run with a compact exam or none.
2. The brains panel is not clipped at 1920 for a compact exam or none: the whole breeding list shows,
   with no inner scrollbar.
3. With a control chosen, the control picker and the "differs" box fit the band beside STOP and RERUN
   at 1920, and no panel below overprints a caption. The brains caption does not overlap at any
   height.
4. A control that swam none of this run's brains says so in one visible line above the comparison
   rows.
5. The band's "differs" headline is the most telling field: a reward field first, then exam controls,
   search, world, bodies, the rest. Never `description` or `name`. When only those differ it says
   "only the wording".
6. The activity tab's time column never wraps: one format, `MM-DD HH:MM`, fixed width.
7. The resource `nursery://episode/{run}/{b}/{d}/{t}` refuses a brain, draw or tank out of range on a
   valid run, in the tank page's own words. Check it through the room's MCP or HTTP resource read,
   whichever the inspect brief names, and compare with the tank page's notice for the same address.
8. A run that is only a folder shows its name once in the band.
9. With a control chosen, a plot under the exam table: each body's own-brain net, this run against
   the control, ± one standard error, labelled so it is not confused with the by-brain rows. Where
   the two runs' bodies differ, a line says so.
10. During an exam the band says "exam brains" done of planned, with the end estimate and whether
    the process is alive. A completed run shows its full count (for example "18 of 18").
13. Exam columns the genome makes uninformative (frozen or brainless, on a genome with no oscillator
    neuron) are greyed, with the reason in a note under the headings and in each title.

Items 11, 12, 14 and 15 of the earlier list were dropped and must not be reported as missing.

## Report

In your final message:
1. **Each claim above, with a verdict:** works, does not work, or partly. Give the evidence, whether
   a real or a synthetic run showed it, and a screenshot path.
2. **Every new fault,** as a table in the first inspection's form, most severe first. Name any
   regression, meaning a thing the last walk found working that does not work now, in its own
   column.
3. **One line per screen that works,** saying what you checked.
4. **The totals:** console errors and failed requests across the whole walk, each explained.

Also give the paths of your driver copy and your screenshots. No report file beyond those.
