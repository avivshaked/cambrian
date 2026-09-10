# Migration: what leaves `scratch/` (owner's rule, 2026-09-10 evening)

The owner's rule: anything prose points to, or any tool that will be reused, must not live in
`scratch/` (gitignored); output that only gave validation or understanding stays there. Read
`CLAUDE.md` (Conventions: the paragraph on `scratch/`; the licence paragraph: code is
PolyForm NC, prose CC BY-NC, `LICENSE-DOCS` names which directories are which), `.gitignore`
(line ~161 ignores `scratch/`), and `scripts/githooks/pre-commit` (blocks files over 5 MB,
secrets, stray emails).

Rules: never poll, sleep or wait; do not run Unity; do not touch `unity-w*`, `runs/`, or any
running process; no commit (the caller commits); nothing written outside the repository; plain
file moves (the sources are untracked, so `mv` then `git add`; never `git mv`); no em-dashes in
anything you write. Do not delete anything under `scratch/`: propose a cleanout list in the
report instead.

## Moves

1. **Round launchers**, code, to a new top-level `rounds/`: `rounds/launch-r13.ps1` through
   `launch-r35.ps1`, plus `launch-det.ps1`, `launch-fp.ps1`, and the four `queue-*.ps1`.
   Each launcher reaches `scripts/run-arm.ps1` and `scripts/new-worker.ps1` by a relative path;
   read how (`$PSScriptRoot`, `..`), and make sure it still resolves from `rounds/` (same depth
   as `scratch/`, so it should). Keep every byte otherwise, BOM included (verify `EF BB BF` on
   each before and after). Add `rounds/README.md`, short: what a launcher is, that its
   defaults are the round's pre-registered world, that the record cites them by round, that a
   launcher is never edited after its round lands except to fix a header comment, and the
   command shape `./rounds/launch-r35.ps1 -Seed 3 -Worker 7`.
2. **Per-round reads and analysis tools**, code, to `scripts/reads/`: `r29-read.py`, `r30-read.py`,
   `r31-read.py`, `r31q-read.py`, `r31-results.py`, `r32-read.py`, `r33-read.py`, `r28-land.py`,
   `r29-launched.py`, `score-r18.py`, `score-r24.py`, `score-r25.py`, `score-r26.py`,
   `score-r28.py`, `rebuild-report.py`, `matter-budget.py`, `matter-profile.py`, `parent-age.py`,
   `clade-score.py`, `late-balance.py`, `checksums.py`, `worker-hash-check.py`, `compare-rows.py`,
   `compare-rows-by-name.py`, `bids.py`. Fix any path inside them that assumes `scratch/` as its
   own location (they mostly take an arm name and resolve `runs/`; check each with a grep for
   `scratch`, `__file__`, `os.path`). Add `scripts/reads/README.md`, three sentences.
3. **Hash tools**, code, to `scripts/`: `simhash.py`, `simhash.ps1`, `tree-hash.ps1`,
   `theatre-check.ps1`, `close-snap.ps1` (read `close-snap.ps1` first; if it is superseded by
   `scripts/theatre-snap.ps1`, leave it in scratch and say so).
4. **Build specs, build reports, surveys and prereg drafts the record cites**, prose, to
   `logbook/specs/`: every `*-spec.md`, `*-build-report.md`, `*-survey.md`,
   `*-prereg-draft.md`, `clade-score-rescore.md`, `price-ledger.md`, `sol-gpt-gap-map.md`,
   `review-2026-09-06-response.md`, `prose-review-notes.md`, `d087-entry.md`, `d087-changelog.md`,
   `d088-entry.md`, `r27-results.md`, `r28-results.md`, `r32-results.md`, `r33-results.md`,
   `r12y-s3-absorptive-line.md`, `footprint-survey.md`, `absorptive-log-spec.md`,
   `scratch-migration-spec.md` (this file). Add `logbook/specs/README.md`: these are the briefs
   builds were made from and the reports they returned, kept because entries cite them, never
   edited after the fact, not a source of truth. Add a row to `logbook/README.md`'s reader's key
   naming the folder.
5. **Cited genomes and configs**, data, to `inocula/`: `inoculum-r13a-s2-t17000.json`,
   `s4-stomach-1.json`, `s4-stomach-2.json`, `s4-stomach-3.json`, `s4-mixo-2node.json`,
   `s4-config-patched.json`, `r28s4-stomach.json`, `r28s4-stomach2.json`, `growth-ledger-config.json`,
   `growth-ledger-genome.json`, `spike-reference-config.json`. Read `inocula/README.md` if it exists
   and add a line per file saying which entry cites it and what it is; if there is no README,
   write one. The `cfg-*.json` sweep and the `r12y-s3-*-copy.*` stay in scratch (uncited, or copies
   of run output).
6. **Cited pictures**, data, to `logbook/images/`: only the PNGs that logbook 0086 and 0088 name or
   describe: `snaps/r35v-s3/r35v-s3-t3000-top.png`, `snaps/r35v-s3/r35v-s3-t1500-side.png`,
   `snaps/r35old-s3b/r35old-s3b-t3000-top.png`, `snaps/r35-s3/r35-s3-t3000-top.png`,
   `snaps/r35-s3/r35-s3-t3000-side.png`, `positions/r35-s5/r35-s5-t0002000-clades.png`. Copy, do
   not move (the theatre's snapshot directory keeps its full sets). Check each is under 5 MB. Add
   markdown image links to them at the right place in 0086 and 0088 with a one-line caption.

## Citations

Rewrite every reference to a moved file in `logbook/*.md`, `DECISIONS.md`, `DESIGN.md`,
`CLAUDE.md`, `HANDOFF.md`, `README.md`, `CONTRIBUTING.md`, `LICENSE-DOCS`, `scripts/*.ps1`,
`scripts/*.py` and the moved files themselves (`grep -rn "scratch/"` before and after; the
remaining `scratch/` references must be only to logs, snaps, positions, or genuinely
transient things). A logbook entry's text is historical: change the path and nothing else.
`CLAUDE.md`'s Conventions paragraph on `scratch/` gains two sentences: the owner's rule, and
where launchers (`rounds/`), reads (`scripts/reads/`) and specs (`logbook/specs/`) live.
`LICENSE-DOCS` names `logbook/specs/` and `logbook/images/` as prose (CC BY-NC) and `rounds/`
as code (PolyForm), one line each, in the shape it already uses.

## Checks

`powershell -NoProfile -Command "Get-Command ./rounds/launch-r35.ps1 | Out-Null"` style parse
checks on every moved `.ps1` (or `[System.Management.Automation.Language.Parser]::ParseFile`),
`python -m py_compile` on every moved `.py`, `python scripts/style-check.py` on every new
README, `python scripts/tests/clade-score/run-tests.ps1`-style suites untouched (do not run
Unity), and `git status --short` showing only intended additions and modifications.

## Report

The file list moved, grouped as above; the citation rewrite count per document; the checks'
verdicts; the proposed cleanout list for what stays in `scratch/` (one-off editing scripts,
stale copies, `__pycache__`, review captures), with sizes, for the owner to rule on; anything
that did not fit.
