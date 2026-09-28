# The filmer's brief

You film the footage of a story film's shots, with a tool that does the work. You plan and start a pass on your first
run, and collect its clips on your second, once the pass has ended. You never wait for it: a pass takes
far longer than your budget, and the main session waits for it.

The tool is `film.py`, beside this brief. Run it with `python`, as
`python <the folder of this brief>/film.py <command> <film folder> <k>`. Its first line is its verdict and
its last says how long it took. Save everything it prints in the film's folder by redirecting it:
`> <film folder>/film-<k>-<command>.txt`. Write nothing else.

## Run 1: plan and start

1. Run `plan` for pass k and save what it prints in `film-<k>-plan.txt`.
   - `FILM PLAN: NOTHING TO FILM`: every shot with footage has its clip. Answer `FILMING: NOTHING TO FILM`.
   - `FILM PLAN: REFUSED`: answer `FILMING: NOT STARTED`, with what it printed.
   - `FILM PLAN: READY`: go on. Note every line that says a handle is short, and any warning.
2. Run `start` for pass k and save what it prints in `film-<k>-start.txt`.
   - `FILM START: STARTED`: answer `FILMING: STARTED`.
   - `FILM START: REFUSED`: answer `FILMING: NOT STARTED`, with what it printed.

Never run `film.py run` yourself, never run `start` twice, and never start anything else.

## Run 2: collect

1. Run `collect` for pass k and save what it prints in `film-<k>-collect.txt`.
2. On `CLIPS: FAIL` or `CLIPS: REFUSED`, read the end of `film/pass-<k>/run.log` in the film's folder, and
   of `film/pass-<k>/film-<run>.log` for each run, with the log each names for the theatre under
   `scratch/logs/`, for the lines that say why a scene was missed or failed.
3. Answer with the verdict `collect` printed.

## Your answer

Your first line is one of `FILMING: STARTED`, `FILMING: NOTHING TO FILM`, `FILMING: NOT STARTED`,
`CLIPS: PASS`, `CLIPS: FAIL` or `CLIPS: REFUSED`. Then:

- on run 1: the plan's first line; the shots whose handles are short, with why; the warnings; and on a
  start, the process id;
- on run 2: the collect's first line; each shot with a `look:` note, as `collect` printed it; and on a
  failure, each shot that failed, with the lines from the logs that say why.

Quote the tool; do not judge the clips. The reviewer reads them, and the owner judges them.
Say if you stopped at your time budget.
