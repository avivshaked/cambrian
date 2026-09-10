# Findings to report to the owner (prose review)

- 0007: the original says seed 7 kept **44%** in the table and **40%** in the prose of the same
  entry. Kept verbatim in v2 per STYLE.md §8.4; one of the two is wrong.
- 0026: original links `[0018](0018-the-founders-can-swim.md)`, a dead target; the file is
  `0018-nothing-to-swim-towards.md`. v2 points at the real file, so the preservation check
  reports that one token lost. Deliberate.
- 0034: the original repeats one whole paragraph ("The units error was not caught by a test...")
  twice, and the second copy has a soft hyphen inside the test name
  (`AFounderBladderStraddles­NeutralBuoyancyRatherThanBeingARocket`), which breaks the identifier.
  v2 keeps one clean copy, so the preservation check reports that mangled token lost. Deliberate.

- Line-wrapped inline code, several files (0049 once, 0051 twice, 0052 twice, 0056 once, 0057 once, 0058 once, 0060 twice, 0061 once, 0062 three times, 0065 twice, 0067 once, 0068 three times, and any later file the
  check flags the same way): the original wraps a backticked span across two lines, so the
  token the checker extracts carries a newline and no correctly written rewrite can reproduce
  it. Each v2 keeps the span on one line, and the preservation check reports the wrapped token
  as lost. Deliberate; the content is unchanged.

- List-marker digits, 0059: the original numbers its two repairs `1.` and `2.`, and the
  checker reads the bare `2` as a token. The v2 gives each repair prose paragraphs instead of
  a numbered list, so the digit has nowhere to sit and the preservation check reports it lost.
  Deliberate; no content is dropped. Primer 04 loses its `3.` the same way: the three rules of
  the economy are numbered in the original and are three plain paragraphs in the v2.

- Primer titles, all six pieces: the title line is `# 01 — A graph that grows a body`, the
  format `primer/README.md` prescribes, and its em dash is as exempt as a logbook entry's.
  The checker's exemption pattern (`ENTRY_TITLE`) matches four-digit logbook numbers only, so
  every primer v2 reports "1 in headers". STYLE.md §4 now says so. The alternative is a
  one-character change to `scripts/style-check.py` (`\d{4}` to `\d{2,4}`), left undone because
  the owner's other session has that file open (logbook/0070's launch note).

- Bold date lines, `research/early-life/MECHANISMS-v2.md`: the checker's fragment rule exempts
  a line that starts with a date (`^\*?\d{4}-\d{2}-\d{2}`), which allows one asterisk and so
  covers `*2026-08-29*` but not the bold `**2026-08-29**` the file actually used. The v2 gives
  the line the logbook's date form instead, which needs no exemption. The alternative is a
  second one-character change to `scripts/style-check.py` (`\*?` to `\*{0,2}`), left undone
  for the same reason as the primer-title change above.

- Bibliography artefacts, `research/LITERATURE-REVIEW-v2.md`: the file finishes at 0 em
  dashes in prose and 0 tokens lost, with 120 findings left. Every one of them is §6's
  annotated bibliography, where the checker reads each author initial as a one-word sentence
  ("**[EA23]** L.") and the citation that follows as an over-long one. STYLE.md §9 now says
  so. Nothing in §6 was broken to quiet the tool.

- Bold sentences across the review: the original used `**A whole claim.**` as its structural
  device, in about sixty places, and STYLE.md §4 forbids bold sentences. The v2 unbolds them
  and lets the sentence carry the claim, keeping bold for the two or three words that matter
  inside it. That is the single largest change to the file's look, and it is the one worth a
  second opinion before the v2 lands.

- Horizontal rules: the review, `FETCH-RESULTS.md` and `research/early-life/SOURCES.md` all
  used `---` to separate entries, which STYLE.md §4 forbids. The v2 files drop them and rely
  on the headings, which is how the logbook already reads.
