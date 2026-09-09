# Contributing

**2026-09-07**  ·  what a contribution grants, and the three checks to run before sending one

Contributions are welcome: code, prose, corrections to what is written here, a paper the literature
review missed. The project is small enough that a pull request with a clear message is the
whole process. Three things to know before sending one.

## What you grant

By contributing, you license your contribution under the project's licences: the PolyForm
Noncommercial License 1.0.0 for code and CC BY-NC 4.0 for prose. You also grant Aviv Shaked
the right to license your contribution under other terms, commercial licences included.
You confirm that the contribution is yours to give. The second grant lets the project be
licensed as one work rather than a patchwork of pieces with different owners. A
contribution that arrives without it cannot be merged. No signed agreement is needed;
opening the pull request is the grant.

## Before you send it

- **Enable the pre-commit guard** with `git config core.hooksPath scripts/githooks`. It
  blocks copyrighted PDFs, secrets, Unity build output and files over 5 MB, and a finding
  is real until proven otherwise.
- **Run the tests** for anything that touches `src/Evosim.Core`, with `./scripts/core-test.ps1`,
  and `./scripts/core-test.ps1 -All` if you touched anything under the `Slow` trait (the
  experiments and the snapshot scan).
  Anything that touches the clade scorer runs `scripts/tests/clade-score/run-tests.ps1`.
- **Write prose to [`STYLE.md`](STYLE.md)** and run `python scripts/style-check.py` on the
  file. The logbook and the primer are read by people as well as agents, and they are meant
  to read as a book.

[`CLAUDE.md`](CLAUDE.md) is the operating guide for the repository, whoever or whatever is
reading it, and its gotchas were each paid for once. Simulation output under `runs/` and
the papers under `research/papers/` are never committed.
