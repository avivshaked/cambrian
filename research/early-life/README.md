# Early-life mechanics

**What early life and the early ocean actually did, and which of it this world is missing.**

## Why this is separate from `../LITERATURE-REVIEW.md`

That review is a PRISMA-style systematic review of the **evolved-virtual-creatures methodology**
literature — encodings, quality-diversity search, controller representation, physics exploitation.
It answers *how do you build one of these*. It has an update protocol (§3.5) and a question set,
and appending a paper to it means running that protocol.

This area answers a different question: *what does the biology say the world should contain*.
The corpus is limnology, geobiology and marine microbial ecology; the purpose is design input, not
methodology. Folding it into the review would break the non-overlap rule CLAUDE.md sets for the six
documents, and would silently corrupt that review's "small n" honesty claim in its §7.3.

Nothing here is a source of truth for the simulator. `DESIGN.md` is. This area is where a claim
gets checked **before** it becomes a `DECISIONS.md` entry, and it is the place to look when asking
why a mechanism was chosen over a plausible-sounding alternative.

## Citation convention — different from the review's, deliberately

The review cites `[KEY §section, p.N]` where `p.N` is a page in a committed-nowhere PDF extracted to
`research/papers/`. **These sources are web-retrieved and mostly not PDFs**, so that locator does not
apply and pretending it does would be a false precision. The convention here is:

- `[KEY]` in the text, resolving to a row in [`SOURCES.md`](SOURCES.md).
- Every row carries the exact URL, the retrieval date, and whether the claim came from the
  **full text**, an **abstract**, or an **encyclopaedia summary**. A claim read only from an
  abstract says so at the point of use.
- **Anything not traceable to a source is marked `⚠ inference`**, in the text, and the reasoning
  is given so it can be argued with.

## Files

| File | Holds |
|---|---|
| [`MECHANISMS.md`](MECHANISMS.md) | The survey: what the evidence says, what this world has, what is missing, ranked by what it would change |
| [`SOURCES.md`](SOURCES.md) | Retrieval record — URL, date, access route, and how much of each source was actually read |

Prose, so CC BY 4.0 under [`LICENSE-DOCS`](../../LICENSE-DOCS), like the rest of `research/`.
