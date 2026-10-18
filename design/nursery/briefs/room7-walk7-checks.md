# Walk 7: the list of checks, and its faults

The walk (2026-10-05) ran against the room on 5179, `scratch/wt-nursery-room` at `e824ed4`. Its
driver, step templates, probes and outputs are in
`D:\Projects\experiments\evolution-simulator\scratch\room7-walk\` ("RW"). Its screenshots are in
`RW\shots\`.

In the Data column, R is real data and S is synthetic: a fetch wrapper in the browser only
(`RW\syn-inject.js`, `syn-git.js`, `syn-step.js`). "Real-only" means a check needs the actual runs and
cannot be repeated on runs a test makes.

| # | Screen and size | Data | What was done | What was measured, and how | Passes if |
|---|---|---|---|---|---|
| C1 | registry, 1920 and 1366 | R, all 6 registry items. Real-only. | Loaded `/`, took a shot and compared it with walk 6's (`imgdiff.py`) | The fraction of pixels changed, and which grid cells hold them. DOM: `a.base` text on rows 5 and 4, and whether OPEN RECIPE is `.disabled` | Differences only in the machine strip and the footer clock, plus rows 5 and 4, where BASE shows "brain2" and OPEN RECIPE is enabled |
| C2 | run (1366, and 1920 on the default run #5), genome | R | The same pixel comparison against walk 6 | The same | Only the machine strip and the footer clock differ |
| C3 | tank, 1920 and 1366 | R, run 5 (brain2-nocross). Real-only. | Reached through the TANK tab with run 5 selected | `document.title`, the `main` text, and the absence of "No exam plan to read" | The tank screen renders (title "tank · run 5"), not the refusal |
| C4 | run, 1920 and 1366: `?id=4&vs=1`, then `vs=3`, `vs=5`, `vs=2`, `vs=accept-stop` | R, run 4. Real-only. | Loaded each URL and counted | `th.off` and `td.off` in `tbody:not(.vs)`, `tbody.vs tr` and `tr.pool`, and the `.title` of every `td.off` (`RW\steps\real4.tpl`, `real5.tpl`) | Only KO Stroke+2 is greyed. Every `td.off` has a non-empty title ending in "by construction". In `tbody.vs`, only "this run, pooled" and "differs from the control" carry `.off`. The count of `td.ctlcell.off` is 0 |
| C5 | run, 1920 and 1366: `?id=3&vs=4`, `?id=5&vs=4`, `?id=1&vs=4` | R. Real-only. | Loaded with run #4 as the control | The same counts | The `.off` count is 0 on every page |
| C6 | run, 1920 and 1366: `?id=4&vs=accept-stop` | R. Real-only. | Loaded | The count and title of `tbody:not(.vs) tr.pool td.off` | Exactly 1 greyed pooled cell, with a titled reason. The note says "The exam is not set beside the control's" |
| C7 | run, 1920: `?id=1` with `sessionStorage syn=unin-some`, then `unin-all`, with `vs=3` | S (`syn-inject.js`, run 1 only) | Loaded and listed the greyed cells per genome row, by display column | The greyed td indexes per row, the pooled `td.off`, and the note texts | Each genome's cells match the injected lists in display order. A column is greyed in the heading and the pooled row only when all 6 genomes list it. The note says "in N of its 6 genomes" only for partial columns. `vs` cells are greyed only on this run's side |
| C8 | run, 1920 and 1366: `?id=1&vs=2` and `?id=4&vs=2` | R. Real-only. | Loaded | The `#b-exam .note` text, and the `.plate--alarm` text | The line "NO BRAIN IN COMMON" lists every brain of this run's exam other than own, by its heading's title. Then it says "the control, #2 accept-1gen, swam own", then "Its rows below are empty" |
| C9 | recipe, 1920 and 1366 | R, 7 recipes. Real-only for the 7 names. | Reached through the tab (storage cleared) and the picker, then ran `probe7-titles.js` | For each `.row label`, whether `title === label + " · " + code`. The counts of rows, titled, exact and wrong, and of labels cut on screen whose title lacks them (`labelTrunc`) | `exact === rows`, `wrongTitleN === 0`, `cutNoTitleN === 0` |
| C10 | recipe, 1920 and 1366 | R, brain2, committed | Clicked LAUNCH | The HTML of `form.ask[data-ask="launch"] .ask__git`, and the label of `button[type=submit]` | The plate says "COMMITTED", there is no what-it-will-be sentence, and the submit button says "LAUNCH" |
| C11 | recipe, 1920 and 1366 | S: `git-changed`, `git-untracked`, `git-outside`, `git-unreadable` and `git-null` on brain2 (registered) | Set `sessionStorage syn`, clicked LAUNCH, then cancelled | The dialog's text | It contains "This launch goes ahead and is entered in the registry, but its record will show the recipe was not committed, so it does not count as a pre-registered run" (see fault 1) |
| C12 | recipe, 1920 and 1366 | S: `git-stale` on brain2 | Clicked LAUNCH after the first reading succeeded and later ones threw | The dialog's text | The plate is kept, with "(as last read; reading it again failed)", and there is no what-it-will-be sentence |
| C13 | recipe, 1920 and 1366 | R, s1-pilot-exam (an untracked trial) | Clicked LAUNCH AS TRIAL | The dialog's text, `[data-op="run.launch"][data-trial]`, and whether the plain LAUNCH is `disabled` | The dialog says "A trial is never a pre-registration, so it launches whatever the state." LAUNCH is disabled, and the submit button says "LAUNCH AS TRIAL" |
| C14 | recipe, 1920 and 1366 | S: `git-changed`, `git-unreadable` and `git-null` on the trial | The same | The same | The same sentence, with the matching plate (or none for null) |
| C15 | recipe, 1920 and 1366 | S: `git-null` | Loaded the recipe | The `#git-plate` text, `dataset.state`, class and title | "COMMITTED? NOT READ", state "none", a class containing `plate--alarm`, and the title "the room gave no reading of whether this file is committed" |
| C16 | all five screens, 1920 and 1366 | R | Reached each through the top bar's tabs from `/` | Console errors, exceptions, failed requests, 4xx and 5xx; the DOM audit's `bad` and `clipped`; the scroll width | 0 errors, 0 exceptions and 0 failed requests. No NaN or [object Object] shown. `scrollWidth <= innerWidth` |
| C17 | run, 1920 and 1366: `?id=4` | R. Real-only. | Opened RERUN, then cancelled | The `form.ask` text; whether RERUN is `.disabled`; STOP's `.disabled` and title | RERUN is enabled for entry #4, and its dialog opens and closes. STOP is disabled, titled "this run is completed, so there is nothing to stop" |
| C18 | run, 1920: `?id=accept-stop` | R. Real-only. | Read the buttons | RERUN's `.disabled` and title | RERUN is disabled: "only a registry entry can be rerun …" |
| C19 | tank, 1920 and 1366: `?run=1` | R (a cap refusal). Real-only. | Read | `#step.disabled`, `#stepwhy`, and `/api/room`'s `stepRefusal` | STEP is disabled, with the reason in its title and in `#stepwhy`, while load plus 1 exceeds the cap |
| C20 | tank, 1920 and 1366 | S (`syn-step.js`: `stepRefusal` null, load 20%) | Clicked STEP, then cancelled | The `form.ask` text | The dialog opens and cancels, and STEP stays enabled |
| C21 | registry, 1920 and 1366 | R | Keyboard and filter steps (`RW\steps\regkeys.tpl`) | The selected `data-key` after ↓, ↑, ↑; the visible row keys after the stopped chip, after typing "nodrive", and after Esc | `5`, `4`, then `5`; `accept-stop`; `4`; then 6 rows again. Enter opens the run, and R opens the recipe |
| C22 | genome, tank and run, 1920 and 1366 | R, run 4 | Changed each picker to another option (`RW\steps\sel.tpl`) | The URL and the title after the change | The URL changes and the page title follows (genome g100.89 → g3.80; tank brain 0 → 2, draw 0 → 2; run vs → 5) |
| C23 | all five screens, both sizes | R | Left each open for 66 s (`RW\steps\poll-*.tpl`) | The DOM node count, scrollTops, the selection, and the requests per resource | The node count is unchanged across polls, scrollTop and the selection are kept, and the polled resources are each read twice in 66 s |

## The walk's faults, most severe first (none breaks anything)

1. **recipe, LAUNCH dialog.** For the git states outside, unreadable and null, the note says "its
   record will show the recipe was not committed", while the dialog's plate says the state is not
   known. It should say the recipe is not known to be committed. The likely cause is one `what()`
   string in `recipe.js` `fillLaunchGit` for every state but committed.
2. **The LAUNCH dialog** says "so at most 1 threads fit". The likely cause is that `machineText`
   in `common.js` has no plural.
3. **The RERUN dialog's machine line** ends at "(22 processors)", without the "so at most N threads
   fit" the LAUNCH dialog gives. The likely cause is that `machineText(r)` is called without its
   second argument (`run.js`, about line 921).
4. **The run screen, run #4 vs #2.** The KO Stroke+2 "differs from the control" cell is both "—"
   (did not swim) and greyed. This is harmless.
5. **The RECIPE tab**, with storage left over from a visit, opens the last recipe viewed, not the
   selected entry's. This is low severity.
6. **The tank screen** scrolls at 1366×768 (scrollHeight 1242). This is probably by design.

Known leftovers that were not reported again: the dots under greyed columns drawn in full colour,
panels cut off at 1920×700, the tank screen finding a recipe only through the registry, "not in
the base → null" on a trial's diff, and the labels births held, purse and population floor.

From the readload look (2026-10-05): the WORLD NOT LOADED refusal lists Core's whole "Present: …"
field list, which is long.
