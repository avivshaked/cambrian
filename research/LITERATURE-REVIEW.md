# Literature review: evolved virtual creatures for a Unity implementation

| | |
|---|---|
| review type | Multivocal Literature Review (Garousi et al.), scoping mode |
| conducted | 2026-08-02 |
| purpose | to inform the implementation design at [`../DESIGN.md`](../DESIGN.md), for a Karl Sims–style evolved-virtual-creatures simulator in Unity (water-first locomotion, quality-diversity search) |
| reporting standard | PRISMA-2020 / PRISMA-S, with an AI-assistance disclosure (§8) |

> Scope honesty up front. This is a **decision-support review for an engineering project**
> rather than a publication-grade systematic review.
>
> Struck text is a claim a later round replaced, and ↻ marks the replacement that
> supersedes it. Both are kept, here and throughout.
>
> ~~After round 3, sixteen retrieved papers are held, three read in full,~~ ~~↻ after round
> 5, thirty-seven are held (round 4 added thirteen, round 5 seven). Twenty-eight of those
> inform the synthesis and five were read in full.~~ ~~↻ **after round 6, fifty-four are
> held** (round 4 added thirteen, round 5 seven, round 6 seventeen; reconciled 2026-09-12
> after the Astra review found this box stopped at round 5). Forty-five of those inform the
> synthesis and five were read in full.~~ ~~↻ **After round 7, fifty-four are still held**,
> because round 7 saved no copy of anything it read. It added ten papers to the synthesis,
> so fifty-five inform it. Ten of the fifty-five were read in full: five from rounds 1 to 5,
> and five in round 7 from open full text that was read and not saved. Round 7's other five
> were read at the abstract. The rest of the synthesis was read in part, where "in part" mostly
> means *specific claims verified against the primary text* rather than cover-to-cover
> reading. Forty-seven of the fifty-four have their PDF on disk; seven round-4 entries are
> held as extracted text only, and only the eight round-1 and round-2 packages carry an
> extraction manifest (§7.2).~~ ~~↻ **After round 8, sixty-eight are held**, with fourteen
> PDFs added from a search of 2026-09-29. Sixty-nine papers inform the synthesis matrix,
> fourteen of them from round 8, and twenty-five more round-8 sources are cited outside it.
> Ten are read in full, as before. Round 8 read its papers at the passages cited, and "in
> part" still mostly means *specific claims verified against the primary text*. Sixty-one of
> the sixty-eight have their PDF on disk. Round 8's fourteen were extracted to text at filing, with no manifest, and only
> the eight round-1 and round-2 packages carry an extraction manifest (§7.2).~~ ~~↻ **After
> round 9, eighty-eight are held**, with twenty PDFs added on 2026-09-30. Eighty-four papers
> inform the synthesis matrix, fifteen of them from round 9. Thirty supporting sources from
> rounds 8 and 9 are cited outside it. Ten are read in full, as before, and round 9 read its
> papers at the passages cited. Eighty-one of the eighty-eight have their PDF on disk. Round
> 9's twenty carry an extraction manifest, so twenty-eight packages now do (§7.2).~~
> ↻ **After round 10, eighty-eight are still held**, because round 10 kept no copy of
> anything it read. It added eight papers to the synthesis matrix, so ninety-two
> inform it, and eight more are cited outside it. None of round 10's sixteen was read in full
> from text the reviewer saw: its ten full texts were read through the fetch tool's summariser,
> and six were read at the abstract.
>
> ~~Round 2 opened a seventh question (endogenous selection, `DESIGN.md` §5A) that **has
> never been searched for at all**~~ **Round 3 (2026-08-29)** searched it. §5A is no longer
> unreviewed design, though the round-3 coverage is breadth-first and single-pass, as §7.1
> says. Section 7 states the limitations without softening them.

## 0. Review rounds

This is a living review rather than a one-shot report. It is extended in discrete
*rounds*, mirroring the draft-changelog pattern in [`../DESIGN.md`](../DESIGN.md) §0 and
§0b. Every section below reflects the cumulative state after the most recent round, and this
table is the history.

A round here is a round of this review. The simulator's experiment rounds are counted
separately and run ahead of it, so round 8 below is an experiment round rather than a review
round. In the same way, review round 7 was triggered by experiment round 57.
Review round 8 was searched on 2026-09-29, before review round 7, and filed on
2026-10-31. Rows are numbered in order of filing.

Review round 9 was searched on 2026-09-29 as well, and filed after round 8. Its working
files call it round 7 and its question Q12, numbers that went to the seasonal-light round.

| Round | Date | Trigger | Queries | Papers read | Design changes produced |
|---|---|---|---|---|---|
| **1** | 2026-08-02 | Initial — six ranked questions before implementation | 15 | 3 full, 2 partial | DESIGN.md draft 1→3: added §2 (premature convergence), changed §8.2/§8.3 (selection + multi-BC), corrected §5.4 (fluid model diversity cost), §4.4 (effector scheme), §4.1 (reflection flags), §11.2 (exploit checklist), closed §12.1 (encoding) |
| **2** | 2026-08-02 | New question, raised during implementation: **should morphological and neural complexity carry a metabolic cost?** | 0 new searches — targeted re-reading of the 8 papers already held | **0 new papers.** Extended reading of [L21] (§8.2 p.10, §13 pp.14–16) and [C18] (§2.4 p.8, §3 pp.13/17, §4 p.29); confirmatory passes on [CEA07] §3.4, [TM01] pp.6–8, [K12] §2.2, [CU15] supplementary p.24 | DESIGN.md draft 4→5: added **§5A** (energy economy, endogenous selection), superseded §5.5, demoted §8 to observatory, repurposed §6.3/§6.4. Recorded as `DECISIONS.md` D017 |
| **3** | 2026-08-29 | §9's backlog, overdue by the review's own protocol: §5A implemented and measured for a month with its literature never searched; the 2025 co-optimisation preprint never followed up; forward snowballing never done; §13.4 quarantine unverified | 4 parallel search sweeps (delegated to subagents — §3.2, §8), plus Semantic Scholar citation API for forward snowballing and CrossRef for verification | **8 new papers retrieved** ([Y94] [MC25] [CO02] [VG05] [GOY23] [ST00] [CB18] [PU16]), ~45 candidates surfaced and screened, ~13 citing works triaged; key claims verified against primary text for [Y94] [MC25] [CO02] [CB18] | **§5A.2's "no precedent for a per-neuron charge" corrected** — [Y94 p.7] is the precedent (DESIGN.md §0e). [CB18] and [PU16] verified and promoted out of §13.4. Q1 sharpened by [MC25]; **Q7 answered in part; Q8 opened and answered in principle** (§2). No DESIGN mechanism changed; the round's findings feed the next decisions rather than rewriting existing ones |
| **4** | 2026-09-01 | A design decision built on uncited inference was falsified by experiment: D055's seabed refuge — flagged ⚠ uncited in its own entry — blocked consumer establishment (logbook/0042) and starved hand-placed consumers to extinction (logbook/0043), and the pending round-8 stabiliser decision plus the owner's whole-layer-access hypothesis lean on the same untouched literature | 2 parallel sweeps (delegated — §3.7, §8), 9 declared new themes under a new question (Q9); the §3.2 re-run was skipped as vacuous (3 days since round 3) and is so logged | **13 PDFs fetched** (12 new to the corpus), **10 into the synthesis** ([HUF58] [JN97] [RMF07] [FM15] [HZ13] [KR13] [MO04] [JKT04] [DBWM05] [RC07]); ~25 relevant works recorded closed-access; 4 load-bearing claims re-verified by the reviewer against extracted text | **D055's failure is theory-predicted, not anomalous** — [KR13 p.2]: the consumer's equilibrium needs refuge *plus* accessible break-even density, so a refuge covering the feeding ground deletes the consumer (D055 addendum updated). Q9 opened and answered in theory. Three constraints delivered to the pending round-8 decision: patches must be *unequal* [HZ13 p.5], the criterion is a length-scale ratio not a dispersal rate [RMF07 p.5], and the strong refuge form is fixed-number (≡ type III response) not proportional [KR13 p.1–2]. The period-vs-generation discriminator for the bust mechanism is uncomputable from current logs — **lineage events promoted from post-goal to pre-round-8**. No existing DESIGN mechanism changed |
| **5** | 2026-09-03 | `DECISIONS.md` **D070**, ruled in principle the same day and explicitly gated on this round: the world's producers give nothing to the water while they live, so dead tissue is the detritus pool's only income, the second trophic level is fed at ~1% of primary production (0.2 W against ~17 W) and a consumer line caps out at about six individuals. The proposed world rule — a producer deposits a fraction of photosynthetic intake into the nutrient field each step — needed a *number with a citation* before it could be a rule | 11 web queries in 3 themes, plus the Semantic Scholar graph + batch APIs (10 DOIs screened for open access) and the Figshare API; **the §3.2 strings were not re-run** — 2 days since round 4, a date-filtered re-run is vacuous, logged per §3.5 rule 1 | **7 PDFs fetched, all 7 into the synthesis** ([PC95] [MCP05] [LS11] [LS13] [CH20] [PWAH07] [ED21]); every numeric claim in the tables below was located and re-read by the reviewer in the extracted `source.md`. **6 works recorded bot-gated or closed and not bypassed** — including the field's two canonical sources, Baines & Pace 1991 and Thornton 2014 | **Q10 opened and answered.** The number D070 asked for: percentage extracellular release is **10–20% of primary production as a world-ocean general range** (Nagata 2000, via [CH20 p.5]), a cross-system mean of **13%** ([BP91], reached only through three independent verified secondaries), **~20% and flat** across a 150-fold productivity range [MCP05 p.1, p.9], rising to **37–41%** in oligotrophic water [LS11 p.1; CH20 p.1]. Three corrections to what the design would otherwise have assumed: exudation does **not** scale allometrically with cell size (isometric, slope 0.95 across >7 orders of magnitude of cell volume) and does **not** differ between growth phases [LS13 p.1]; and it is **not** proportional to light — DOCp is irradiance-independent while POCp is not, so real PER is *highest where photosynthesis is lowest* [MCP05 p.1, p.8–9]. Two reference points for the 1% diagnosis: the producer→herbivore step measures **13% (11–17%)** [ED21 p.14], and Pauly & Christensen's famous 10% is measured for **TL2→4 only** [PC95 p.3], so it is the wrong number to compare a producer→consumer step against. No DESIGN mechanism changed; the fraction goes to the owner for D070 |
| **6** | 2026-09-07 | The owner's question, put while round 29 ran, was how nature solved the chicken and egg problem that movement in the primordial soup would not have been worth it without senses, and senses would not have been worth it before movement. Behind it, D082 and 0072: no round has ever selected for moving, and the owner's reading is that the world is not one in which random movement beats sitting still | Four parallel search sweeps (Claude Sonnet subagents, read-only web, scoped briefs), about thirty queries between them; **the §3.2 strings were not re-run** and no forward snowball was done, both logged in §3.9 per §3.5 | **17 PDFs fetched, all 17 into the synthesis** ([P77] [BP77] [D97] [KB96] [SL23] [W11] [TS12] [MK72] [SBB86] [SJ10] [J09] [J11] [JKG15] [KDL13] [JG21] [MZ09] [HG25]); every claim used was located by the reviewer in the extracted `source.md` at the page. 17 works recorded closed or bot-gated and not bypassed, among them the two founding texts, Fraenkel & Gunn 1940 and Berg & Brown 1972 | **Q11 opened and answered.** Nature did not solve the problem; it never had it. Blind motility pays on its own where the habitat is physically structured [W11 p.1] and the body is large enough for its own motion to refresh the water it feeds from (a radius near 20 µm for a 50% gain [KB96 p.1]; nothing below 0.6 µm [D97 p.1, p.4]; a bacterium relocates rather than stirs [P77 p.7–8; BP77 p.1]); the first sense is a scalar compared with itself a few seconds earlier, biasing a walk that was already random [MK72 p.1; SBB86 p.1; SJ10 p.1]; in animals the first sensorimotor unit was one cell, and nervous systems came to coordinate a new effector, not to link sensing to moving for the first time [JKG15 p.2–3; KDL13 p.4, p.8; J09 p.7; J11 p.1]. **Design impact: a diagnosis and a proposal, no mechanism changed.** §5A.2c's field has no per-body depletion, so undirected movement earns nothing here by construction; DESIGN §0r records it and `fable-propose-motility.md` (absorbed into D083 and deleted on ruling) puts three world rules in front of the owner |
| **7** | 2026-10-05 to 2026-10-06 | The simulator's experiment round 57 (`DECISIONS.md` D151): leaf bodies under a 6,000 s year of two seasons, three days at 1.6 and three at 0.4 of the mean light, with each day's light multiplied by a shared log-normal weather factor (log s.d. 0.3, lag-1 correlation 0.5), a stored reserve, a breeding gate and no light sense. Its pre-registration needed literature behind its predictions on reserves, breeding timing and trait variance under seasonal and stochastic light | 43 web queries on 2026-10-05 (§3.10), 31 abstract fetches through Europe PMC, OpenAlex, arXiv and PMC; verification on 2026-10-06 against CrossRef, OpenAlex, Europe PMC, Semantic Scholar and the bioRxiv API. **The §3.2 strings were not re-run** and no forward snowball was done, both logged in §3.10 | **10 papers verified and added to the synthesis**, no PDF saved. **Read in full from open text (5):** [FDT11] [PW21] [GSS14] [MK07] [BWWR15] (main text; [GSS14] and [BWWR15] supplements not read). **Abstract only (5):** [WTV18] [SK12] [VJTF09] [TGC17] (its design read in full in the same group's open 2019 paper) and [WYRW25] (journal abstract only; both preprint versions read in full). **Locators are section and figure locators, not PDF pages**, because no PDF was saved and the full texts were read as PMC, Europe PMC or bioRxiv XML and HTML, which carry no PDF pagination | **Q12 opened and answered in theory; no mechanism changed.** `DESIGN.md` §0y records the round as rounds 3, 4 and 6 were recorded (§0e, §0e2, §0r). The round backs the predictions of experiment round 57's pre-registration, `logbook/0136-winters-and-weather.md` (on the `round-57` branch at filing). Clause R (the reserve rises) rests on [FDT11] (no storage at low variability and low predictability, Results, Figs 3–4) and [VJTF09] (abstract: stores pay by allowing breeding before the feeding season). Clause T (births move to summer's first day) rests on [VJTF09], abstract only. Clause V (the reserve spread narrows) rests on [PW21] (within-population trait variance fell over time in every regime, Results, Fig. 5). Every mapping from a paper to this world is the round's inference, and none of the ten modelled this world's setting. Two papers disagree on the direction of the weather's autocorrelation effect ([MK07] against [WTV18]), recorded in §7.3 |
| **8** | 2026-09-29 (searched and appraised); filed 2026-10-31 | `DECISIONS.md` D128 ruled death by age as a Gompertz hazard on a heritable base that costs repair upkeep, with each birth and each destroyed part advancing the body's clock, and asked for a review round on its sources before its numbers were fixed. D130 put the heritable gene into experiment round 52 with its values unruled | Two passes on 2026-09-29. Pass 1 logged no queries. Pass 2 logged 63 attempts in `logbook/specs/r52-literature/search-log.tsv`: 42 OpenAlex keyword or filter queries, 5 OpenAlex forward snowballs, 3 Semantic Scholar keyword and 6 bulk queries returned results, and 7 Semantic Scholar attempts failed with HTTP 429. Metadata lookups for 50 candidate DOIs in each service, and CrossRef retraction checks on 42. **The §3.2 strings were not re-run**, logged in §3.11 | **14 PDFs retrieved and kept** (`papers/` 85 to 98). **39 sources appraised:** 14 core into the synthesis matrix ([PER19] [COL21] [JON14] [KR91] [MC19] [LEM15] [BBV20] [WIN25] [SADK00] [ZBH95] [LC85] [MR87] [CAR92] [KK16]) and 25 supporting, cited outside it. By read level: 14 PDFs at the passages cited, 9 PubMed Central article pages without page numbers, 16 abstracts. None read in full. Pass 1's 42 sources enter only where pass 2 re-read them | **Q13 opened and answered in part; no mechanism changed.** The owner ruled the gene's values on 2026-09-29 from pass 1 (logbook/0127): repair exponent 0.5, mutation chance 0.08, founders 0.5 to 1.5. D128's doubling at a fifth of the base and its birth and wound pushes stood. Pass 2 found f = 0.2 defensible [JON14; FPW90]. It found the gene acting on the rate, where the evidence puts lifespan variation in onset and early mortality [PER19 p.3; COL21 p.1, p.3]. The birth push is 3 to 25 times the cost measured in birds [BBV20 p.6; WIN25 p.3], and no source gives the repair exponent [KR91]. D132 held the values through rounds 53 and 54 and cites pass 2. `DESIGN.md` §0z records the round |
| **9** | 2026-09-29 (searched); 2026-09-30 (fetched); 2026-10-31 (appraised and filed) | `DECISIONS.md` D134 ruled the fast nursery, which breeds brains in an easier world under D133's score, and the owner asked whether academic justification could be found. A first report answered at abstract level. The round's working files call it round 7 and its question Q12; this review files it as round 9, question Q14 | First report: queries not logged. Pass 1: 60 web search and fetch calls, first page only. **The §3.2 strings were not re-run** and no forward snowball was done, logged in §3.12 | **20 PDFs retrieved and kept** (`papers/` 175 to 192, 194 and 195), ten through institutional access; Soros & Stanley 2014 not obtained. **20 appraised:** 15 core into the synthesis matrix ([MD08] [MD09] [GM97] [JAK97] [PJ10] [CA16] [LOPA03] [CLWO13] [KNA07] [WLCS19] [LST11] [GEOP08] [BB72] [SHLF21] [BEJ25]) and 5 supporting ([LFM13] [WS11] [LM14] [TEN26] [AB14]). All were read at the passages cited, none in full, with no database check | **Q14 opened and answered in part; no mechanism changed.** The literature names D134's move, environmental complexification, as a scheme apart from fitness shaping [MD08 p.2–3]. Direct evolution failed where a staged world succeeded [GM97 p.13–14; PJ10 p.5; CA16 p.15]. A single hand-built ramp failed at the hardest levels [WLCS19 p.14], and every embodied success shaped its score as well as its world [CA16 p.11; PJ10 p.4; GEOP08 p.4]. No paper moved an evolved sensing-and-steering brain from an easier tank to a harder one. `DESIGN.md` §0z2 records the round |
| **10** | 2026-10-01 (pass 1); 2026-10-31 (pass 2) | `DECISIONS.md` D146 ruled one brain per creature on 2026-10-01 and left open a review round on growing networks (NEAT) and on central against distributed controllers. D147 named LeCun's initialisation and LSUV without a source, to be confirmed here. Pass 1 (`research/brain-v2/pass1-candidates.md`) opened nothing | Pass 1: 17 web searches on 2026-10-01, each naming a work or topic. Pass 2: 14 web searches on 2026-10-31, nine naming a work and five framed by the question, plus 10 OpenAlex, Semantic Scholar and CrossRef lookups (§3.13). **The §3.2 strings were not re-run** and no forward snowball was done | **16 sources appraised, no copy kept:** 8 core into the synthesis matrix ([KGV22] [MEC23] [SM02] [WSSMK05] [HFMT23] [GAM15] [IL10] [IB13]) and 8 supporting. By read level: 10 HTML full texts, every one read through the fetch tool's summariser, and 6 abstracts. Locators are "HTML" with a section name, since no PDF was read | **Q15 opened and answered in part; no mechanism changed.** When bodies change, controllers shared across modules beat one central network, which converged early [KGV22 HTML, Results; Discussion; MEC23 abstract]. D146's copy of a parent joint's wiring at birth resembles the per-module rule, which did not beat central control at 500 generations (inference). A body's own feedback near resonance can make a swimming gait without a central oscillator [GAM15 HTML], which is precedent for D146's fourth ruling. Evolved worm circuits read a change in concentration from their sensory cells' short windows, not from network memory [IL10 HTML; IB13 HTML]. Brain v2 reads scent as a level with one step of memory. NEAT's case for minimal starts and FS-NEAT's for learned inputs were read at the abstract only [SM02; WSSMK05]. `DESIGN.md` §0z3 records the round |

Round 2 searched nothing and retrieved nothing. It re-read papers already in
`research/papers/` against a question that had not been asked in round 1. The answer changed
the design more than round 1's fifteen queries did.

Three limitations follow from that, and they are carried into §7.

- **The corpus was assembled** to answer different questions. Round 1's six questions were
  about encodings, quality-diversity and physics exploitation. Nothing was retrieved
  *because* it addressed metabolic cost, so with respect to this question the eight papers
  are a convenience sample rather than a search result. A proper round 3 would search for it
  directly, on candidate terms such as open-ended evolution, artificial ecosystem,
  endogenous fitness, energy-based selection, and Avida, Tierra, PolyWorld or Geb.

- **The two directly relevant systems** are known only through a survey. PolyWorld and
  Ventrella's Gene Pool appear in [L21 §13] as descriptions rather than evaluations. Neither
  primary source is held. DESIGN.md §5A is therefore **not literature-backed**, and
  D017 records it as a bet rather than a finding.

- **[L21] is marked ⚠ partial in §13.2 of DESIGN.md**, and the sections read here (§8.2,
  §13) were outside the range that entry declares. That entry has been widened. The
  general point is that "partial" reads accumulate silently unless each extension is
  recorded.

### Rules for adding a round

1. **Never edit** a previous round's row. Append a new one instead.

2. **Update the cumulative sections**: §2 (question status), §4 (flow counts), §5 (synthesis
   matrix), §6 (bibliography), §7 (threats), §9 (gaps). A round that adds papers but leaves
   §7.3's "small n" claim untouched has made the review dishonest.

3. **Record design impact** in both places. If a finding changes `DESIGN.md`, it goes in
   that document's changelog *and* in this table. They drift otherwise, and then neither
   can be trusted.

4. **A round that changes nothing** is still a round. Recording "searched, found nothing
   new" is evidence of saturation and is worth more than silence.

## 1. Executive synthesis

The design this review was commissioned to test was written from first principles, and
the literature contradicted it in three places. Two of the three are substantive, and
one is a mistake in the design's own reasoning rather than in its facts.

First, the design had omitted the field's dominant failure mode entirely. Co-evolving
body and brain is pathological in a specific, well-documented way. A morphological
mutation invalidates the controller co-adapted to the previous body, so selection
discards the offspring even when the new body is superior. Morphology then stagnates
within a few dozen generations while controllers continue improving, discarding the
benefit of co-evolution altogether. Eguiarte-Morett & Aguilar [EA23] state this directly
and attribute it to three independent prior groups. Their five-algorithm benchmark shows
a baseline without diversity protection losing every pairwise comparison. Some
diversity-preserving mechanism is mandatory. Their formal winner was multi-BC NSLC, and
their own practical recommendation for virtual-creature co-evolution was MAP-Elites. The
design retains MAP-Elites with two corrections. Parent selection must be
**fitness-proportional** rather than uniform, and descriptors must span **both** an
aligned and an unaligned behaviour characterisation. That second point is bracketed by
two failures pointing in opposite directions. Krčah [K12] showed a purely aligned
descriptor, final position, causing divergent search to *lose* to plain fitness search
at swimming. Pugh et al., via [EA23], warn that purely unaligned descriptors can prevent
a QD algorithm reaching viable solutions at all.

Second, the simplified fluid model is exploitable, and its cost is not what the design
assumed. Usami [U07] compared the per-part quadratic drag scheme the design specifies
against particle-based hydrodynamics, and the two disagreed on the *direction of
travel*. More damningly, the evolutionary algorithm had selected the offending gait
because the cheap model was wrong. Corucci et al. [C18], using a near-identical drag
model, then extend the finding in a direction the design had not anticipated. The
simplification does not merely produce fictitious physics. It **collapses morphological
diversity**, precluding fish-like and squid-like creatures and yielding a population of
similar medusoids. The design had reasoned that a crude fluid model was acceptable for
the aesthetic goal and problematic only for the scientific one. That was wrong in the
more important direction, because morphological variety *is* the aesthetic goal. Added
mass, the cheapest missing term, was accordingly promoted from a refinement to a
Milestone 3 requirement.

Third, the encoding question resolved in the design's favour, and the reason is worth
recording. Two of the strongest papers use CPPN indirect encodings and report advantages
over direct ones. That appeared to threaten the design's recursive graph. A CPPN, a
compositional pattern-producing network, is a small function that maps a point in space
to what should be built there. Lai et al.'s survey [L21] synthesises every published
encoding comparison, and the result splits by phenotype. **CPPNs win on soft bodies, and
on rigid articulated bodies direct and recursive encodings win or tie**, including in
the most recent four-way comparison. The apparent threat was a substrate mismatch. The
same source also corrected a terminology error. A Sims recursive graph *is* an indirect
encoding, so the regularity CPPNs were wanted for is already present. Elsewhere the
literature was confirmatory. Krčah's implementation supplied a concrete anti-exploit
checklist and an effector scheme cheaper than the design's proposal. Corucci supplied
empirical support for the water-before-land milestone ordering, though that particular
result did not reach statistical significance and is reported here as suggestive only.

## 2. Research questions

| # | Question | Status |
|---|---|---|
| Q1 | What prevents premature morphological convergence in body–brain co-optimisation? | ✅ **Answered — and sharpened in round 3.** [MC25], with ground truth from an exhaustively-mapped 1.3M-morphology landscape, shows the mechanism precisely: fitness under a co-evolving controller systematically *undervalues* newly-mutated bodies, so promising morphologies are eliminated before their controllers adapt — and even MAP-Elites and explicit innovation protection found near-optimal morphologies in only 17–22% of trials [MC25 p.1, p.29]. The mitigation question is less settled than round 1 concluded. *Scope, 2026-09-07: this answer is about search under an external fitness function; the world moved to endogenous selection with the archive demoted to an observatory (DESIGN §5A.8), and whether ecological niches supply the same protection is an open question rather than a settled transfer* |
| Q2 | Why are Sims' 1994 results hard to reproduce? What are the necessary ingredients? | 🟡 **Partial** — [TM01]/[CEA07] read in part (round 2); Krčah's GECCO'07 reimplementation and Lessin's thesis still never fetched |
| Q3 | Direct graph vs CPPN vs grammar encoding? | ✅ **Resolved** |
| Q4 | Which quality-diversity variant? | ✅ **Answered** |
| Q5 | Controller and actuator representation? | 🟡 **Partial** — ~~one implementation's scheme, no comparison~~ ↻ *round 10:* comparisons now held for central against per-module controllers [KGV22; MEC23 abstract] and for oscillator against network controllers [VSG23 abstract]; all on modular or soft robots, none on a Sims-style rigid creature. Q15 carries the detail |
| Q6 | What physics exploits should be defended against? | ✅ **Answered** (checklist + two case studies) |
| Q7 | *(opened round 2, searched round 3)* Does endogenous / energy-based selection have precedent, and what lets such systems hold multiple strategies and trophic structure instead of collapsing to the cheapest trade? | 🟡 **Answered in part.** Precedent is real and primary-sourced: [Y94] (PolyWorld — per-neuron and per-synapse energy charge, behaviour-priced actions), [VG05] (Gene Pool — locomotion pays because food and mates are the only routes to reproduction), [CO02] (Avida — **depletable** resources produce negative frequency-dependent selection and stable coexistence of up to nine strategies; making the same resources unlimited collapses diversity to one genotype). Trophic emergence conditions from the ecology-modelling side: adaptive prey selection [DROSSEL04 — lead], spatial structure and density thresholds [HAMM21 — lead], closed nutrient cycles [GOY23]. What no held source yet shows: trophic levels emerging from *morphology-encoded* feeding on a physically simulated body — that remains this design's own bet |
| Q8 | *(opened round 3)* What instrument distinguishes adaptive evolution from a treadmill, computable from this project's logs (births with parent ids, deaths, genomes, energy ledgers)? | 🟡 **Answered in principle, not yet implemented.** Bedau–Packard evolutionary activity with the class 1–4 taxonomy (via [BSP98 — lead, unfetched]) is the formal treadmill test; [ST00] supplies an implementation that replaces the "neutral shadow run" with a randomly-permuted shadow population, which fits a system that has no fitness function to switch off. The MODES toolbox ([DOL19 — lead, bot-gated preprint]) is the modern alternative and substitutes a lineage-persistence filter for the shadow. Both need a lineage record, which bears directly on the open `lineage.jsonl` decision |
| Q9 | *(opened round 4)* What stabilises a consumer–resource interaction against boom-and-bust — and which stabiliser fits a world whose only evolved consumer is a benthic filter feeder on a well-mixed detritus pool? | ✅ **Answered in theory; the world's own answer is the round-8 experiment.** The theory predicted this project's own result before it ran: a refuge covering the consumer's feeding ground does not stabilise, it deletes the consumer — the equilibrium needs the refuge *plus* the consumer's break-even accessible density [KR13 p.2], and increasing hidden prey ends in predator extinction (González-Olivares & Ramos-Jiliberto 2003, abstract — closed). Four stabiliser families with primary sources: **(a) refuge, strong form** — fixed-*number* not proportional, which is mathematically a type III functional response [KR13 p.1–2; Maynard Smith 1974 via KR13]; **(b) spatial structure** — works by asynchrony + limited dispersal (empirically: continuous platform dead in 120 days vs eight throttled islands persisting 393–447+ [JN97 p.7]; Huffaker's 120-position universe [HUF58 p.39–41]), but *subdivision alone is the null result* — dispersal can destabilise (Briggs & Hoopes 2004, abstract — closed), identical patches buy nothing [HZ13 p.5], and the operative criterion is a length-scale ratio: coexistence dies when the boom-bust pattern wavelength outgrows the domain [RMF07 p.5]; a growth *gradient* plus mobile grazer stabilises even type I feeding at unbounded carrying capacity [FM15 p.1, p.19] — this project's light gradient is that geometry, rotated; **(c) feeding relaxation at low density** — a type III toe (q=0.1 suffices in food webs [DBWM05 p.12]), noting an unbounded linear clearance is not even a real type I, which has a satiation plateau by definition [JKT04 p.1]; **(d) donor control** — a flux-fed detritus pool with a mass-action consumer is *globally stable* [MO04 p.7], so the observed busts imply this world's pool is not behaving as donor-controlled (closed-loop recycling feeds back — Quévreux 2021, abstract, bot-gated) and/or the cycles are cohort-structural, not dynamical (de Roos & Persson family — lead). **The discriminator (cycle period vs consumer generation time) needs lineage events, which do not exist yet** |
| Q10 | *(opened round 5)* What fraction of its photosynthetic intake does a producer release to the water while alive; what producer→consumer transfer efficiency should a world be judged against; and is the detritus/microbial route a real path for energy to consumers rather than a leak? | ✅ **Answered, with three corrections to the naive form of the rule.** **(a) The fraction.** Percentage extracellular release (PER = dissolved / [dissolved + particulate] primary production) is a normal, continuous process in all growth phases. World-ocean general range **10–20%** (Nagata 2000 via [CH20 p.5]); cross-system mean **13%**, from 16 lacustrine/marine/estuarine studies ([BP91] — **not obtained**, reached through [MCP05 p.9], [LS11 p.7] and [CH20 p.5], all three of which also flag that its glass-fibre-filter methods *underestimate* release); **22 ± 2%** measured in the Celtic Sea and **~20% flat** across a productivity range from <100 to >15,000 mg C m⁻² d⁻¹ (n = 35, r² = 0.90) [MCP05 p.1, p.9]; **~37%** in the ultraoligotrophic Mediterranean [LS11 p.1]; **40.8 ± 12.2%** (integrated range 28.6–60.1%) in the southern East China Sea [CH20 p.1, p.5]; **10 to >35%** in natural assemblages but only **<1–20%, mean ~2%,** in cultures [LS13 p.1]. **(b) Variation.** With *nutrient status*: contested inside one lab — [LS11 p.7–8] finds the oligotrophic contribution higher yet reports that on their pooled dataset "no overall inverse relationship between PER and total primary production exists", while [MCP05 p.9] finds PER flat from eutrophic to oligotrophic. With *growth phase*: no significant difference across three growth stages in 22 species [LS13 p.1]. With *cell size*: **none** — PER uncorrelated with cell size, cell-specific exudation isometric with cell volume (slope 0.95), so "general allometric models cannot be used to predict exudation" [LS13 p.1]. With *light*: DOCp is roughly constant across irradiance while POCp is strongly light-dependent, so PER rises under low light and peaks (>40–50%) at the *base* of the euphotic layer [MCP05 p.1, p.7, p.8–9]. **(c) The reference point.** Producer→herbivore transfer efficiency averages **13% (11–17%)**, and herbivore→fish **10% (7–12%)** [ED21 p.14]; the conventional ~10% [ED21 p.7] traces to [PC95], whose mean of 140 estimates across 48 trophic models is **for TL2→4 and shows no trend with TL** [PC95 p.1, p.3] — it is not a measurement of the producer→consumer step. Ecosystem-scale estimates span <1%–52% [ED21 p.17]. **(d) The detritus/microbial path is real and large.** Of 138 energy channels deconstructed from 40 community food webs, 20% originate with detritus against 63% with a primary producer, "many of which could be traced back to detritus if the description were complete" [MO04 p.3]; most organic matter available to consumers in the ocean is used and respired by bacteria [PWAH07 p.3], and the larger part of all energy captured by marine photosynthesis is ultimately consumed by microorganisms [PWAH07 p.6]. **The caveat that matters for D070:** exudation alone does not feed that loop even in the real ocean — bacterial carbon demand exceeded dissolved primary production **5- to 14-fold** in the Mediterranean [LS11 p.8] and exceeded total primary production at all non-upwelling East China Sea stations [CH20 p.1]; sloppy feeding, excretion and viral lysis supply the rest [LS11 p.8; ED21 p.10]. *Framing, 2026-09-07: D070's rule, a fixed fraction of current photosynthetic income, is an engineering rule that takes its magnitude from this evidence and not its shape; the evidence says release is not proportional to light and is highest where photosynthesis is lowest, and the rule does not model that* |
| Q11 | *(opened round 6)* Under what conditions is undirected movement worth its cost before any directional sense exists; how did the first sense couple to movement; and did early animals ever pass through a stage with senses and no effectors, or effectors and no senses? | ✅ **Answered.** **(a) When blind movement pays.** Only in a physically structured habitat: "for natural selection to favor motility for the acquisition of nutrients by bacteria, their environment must be physically structured", and "chemotaxis evolved in bacteria that were already capable of self-propulsion in random directions" [W11 p.1]. At bacterial size movement does not raise uptake, since "the adsorption rate for molecules of moderate size cannot be significantly enhanced by motion of the cell or by stirring" [BP77 p.1] and "this bug can't do anything by stirring its local surroundings"; what it can do is "find places where the food is better", moving "to find greener pastures" a distance of order D/v to outrun diffusion [P77 p.7–8]. The size threshold: an organism "smaller than 0.6 µm diameter is unlikely to obtain any advantage by expending energy on locomotion", and the smallest of 97 motile genera averages 0.8 µm while 18 of 94 non-motile genera are smaller [D97 p.1, p.4]. Above that, swimming or sinking raises the flux to an absorber, by about 50% at a radius near 20 µm, and "mass transfer increases with cell size and with" relative motion in every flow regime [KB96 p.1]. Encounters with particles are diffusive below a micrometre and ballistic, scaling with swimming speed, above it [SL23 p.4]. Even with a sense, directed search beats undirected only at intermediate turbulence [TS12 p.2]. **(b) The first sense.** Bacteria detect no gradient across the body: "the apparent detection of a spatial gradient by the bacteria therefore involves an actual detection of a temporal gradient experienced as a result of movement through space" [MK72 p.1]; the comparison reaches "4 sec into the past: the past second is given a positive weighting, the previous 3 sec are given a negative weighting" [SBB86 p.1]. The signal can be the cell's own energy state: "motility, combined with the sensory perception of the internal energetic conditions, is prerequisite for tactic responses" [SJ10 p.1]. **(c) Animals.** Parker's scenario had "independent effectors" arise first and early nervous systems connect them to sensors; Pantin's answer, which the field now holds beside it, is that nervous systems arose to coordinate the new effector, muscle [JKG15 p.2]; "there can be significant IO and IC function without a nervous system" [JKG15 p.3]. The problem early nervous systems solved "was not so much to act intelligently, a problem that had already been solved in various ways without a nervous system, but to act as a single multicellular unit" [KDL13 p.4], and "at microscopic scales, information processing tasks required for adaptive behavior can be accomplished without any nervous system" [KDL13 p.8]. The simplest eyespot is two cells, a photoreceptor and a pigment cell, and larvae "directly regulate ciliary beating during phototaxis by the eyespots" [J09 p.7]; "the first nervous systems consisted of combined sensory-motor neurons, directly translating sensory input into motor output" [J11 p.1]; much early sensing "was reafferent; it was responsive to the consequences of the animal's own actions" [JG21 p.1]. Neurons and brains arose more than once [MZ09 p.1]. **(d) Artificial life.** No fetched paper names the dilemma as such; the nearest are [VG05]'s finding that locomotion pays only when it is the sole route to reproduction (round 3) and [HG25 p.1]'s that energy costs drive evolved networks smaller. *The design reading, marked as this review's inference: this world's feeding draws from a cell 5 m across that every body shares, so a body neither depletes its own water nor refreshes it by moving; the physics that made blind movement pay is absent by construction, and the fix is a world rule, in front of the owner.* |
| Q12 | *(opened round 7)* When a population's light varies by season and by unpredictable weather, and its bodies cannot sense the light, what do theory and experiment predict for stored reserves, for the timing of breeding and for the variance of breeding traits? | 🟡 **Answered in theory; the transfer to this world is inference, and the world's own answer is experiment round 57.** **(a) Storage.** In a stochastic dynamic programming model where environmental energy follows a log-normal AR(1) process (a series in which each step is correlated with the one before), storing is not optimal at low variability and low predictability. The stored fraction rises as either grows, and storage pays only at intermediate environmental energy with stores not yet full [FDT11 Results, Figs 1, 3–4]. That optimum is a reaction norm on the *sensed* current environment, the model has no minimum breeding investment, and its fitness is expected lifetime reproduction, so it carries no bet-hedging term [FDT11 model conditions; Discussion]. In a seasonal model, stores pay mainly by allowing breeding before the feeding season, when an egg is worth most [VJTF09 abstract]. **(b) Bet-hedging.** Bet-hedging is the trade that [SK12]'s title names, among the mean, the variance and the correlation of fitness. Dormancy evolved upward with the frequency of unpredictable bad years in *Neurospora*, as the geometric-mean model predicts: mean final dormancy was 0.16 with no bad years and 0.30 to 0.33 at 40–50% [GSS14 Results, Table 2, Fig. 1]. Rotifers under an unpredictable regime started sex earlier and hatched fewer diapausing eggs [TGC17 abstract]. A genotype's fitness variance splits into each individual's variance and the correlation among its individuals [SK12 abstract], and in finite populations the sign of selection on a bet hedger can reverse with population size [WYRW25 abstract; preprint Results]. Which response evolves (plasticity, bet-hedging or tracking) depends on the timescale and predictability of the variation [BWWR15 Results, Fig. 2]. **(c) Autocorrelation, direction disputed.** More positive autocorrelation at fixed variance always raised the evolutionarily stable allocation to survival [MK07 Results, Fig. 2c; Discussion]. Stress tolerance was disfavoured in redder environments, through a trade-off with reproduction [WTV18 abstract]. **(d) Trait variance.** Slower cycles kept more within-population life-history variance, periodicity mattered more than random timing, and variance fell over time in every regime [PW21 Results, Figs 4–5]. **(e) Artificial life.** No study was found of evolved storage or bet-hedging under seasonal plus stochastic resource input; it may be a gap in the field or in the search (§9). *The design reading, marked as this review's inference: the weather (coefficient of variation about 0.31, e-folding time about 1.4 days) lies at or beyond the low end of [FDT11]'s range, so the season and not the weather should raise the reserve; births should bunch at the turn to summer; and the reserve's spread should narrow over the run. These are clauses R, T and V of round 57's pre-registration.* |
| Q13 | *(opened round 8)* How does mortality rise with age across species; what does a longer life cost; do reproduction and injury speed ageing; how heritable is lifespan and how fast does it respond to selection; and what do models in which lifespan evolves show? | 🟡 **Answered in part; most of the values D128 needs are judgement.** **(a) Shape.** Senescence is detected in the wild in 175 species [NUS13 HTML], but not everywhere: trajectories also stay flat, fall or hump, and terminal over mean adult mortality runs from above 20 to below 0.5 [JON14 HTML]. Across 96 mammals the rate of senescence does not slow with body size; longer lives come from later onset and lower prime-age mortality [PER19 p.3], which carry 47–62% of lifespan variance [PER19 p.6]. Within primate genera "variation in the rate of ageing … is orders of magnitude smaller than variation in pre-adult and age-independent mortality" [COL21 p.1, p.3]. A fitted Gompertz rate is confounded with initial mortality [RS02 abstract; KIR15 HTML]. **(b) The cost of a longer life.** The theory needs only that longer life "usually involves some cost" and gives no functional form [KR91 abstract]. Within species, 21 of 26 wild vertebrate studies support an early–late trade-off [LEM15 HTML], and direct selection for long life cut fecundity [ZBH95 abstract]. Others uncouple the two [MC19 p.1, p.4; COH20 abstract]. **(c) Reproduction.** Lifelong brood enlargement raised jackdaws' Gompertz slope from 0.16 to 0.25 a year, left the intercept unchanged and cut remaining life from 4.7 to 3.8 years [BBV20 p.6]. Across birds the cost is −0.05 log-odds of annual survival per egg [WIN25 p.3], with a "flat" landscape within species [WIN25 p.1]. A ten-year cricket study found no evidence of a trade-off with survival, and only weak evidence of one with senescence [RM19 p.1]. **(d) Injury.** Wing clipping shortened bumble-bee life [CAR92 abstract]. One sublethal head strike cut fly median life from 48.3 to 38.3 days, saturating at four strikes [KAT13 HTML]. No comparative source was found. **(e) Heritability and response.** Life-history heritability averages 0.262 in wild animals [MR87 p.6], and human longevity's is below 10% [RUBY18 HTML]. Late-reproduced flies rose from about 35 to over 70 days in 21 generations, at uncontrolled larval density only [LC85 p.1, p.5]. Mortality regimes diverged in 50 to 90 generations [SADK00 HTML]. **(f) Models.** Survival falling faster than exponentially emerges from inherited age-specific mutations [PEN95 p.3]. Local reproduction favours shorter lives [WER15 p.2; TRA04 abstract], and re-implementations traced such results to movement or a shorter generation time [KK16 p.13]. Ageing evolves stably and negative ageing does not [GT22 p.1]. **(g) Artificial life.** No evolvable-lifespan model from an artificial-life venue was found. *The design reading, marked as this review's inference: f = 0.2 is defensible on the measured doubling times; the gene's stretching of the doubling time runs against the within-lineage evidence; the repair exponent has no source; the birth push is 3 to 25 times the measured cost; the wound push has the right direction and a guessed size.* |
| Q14 | *(opened round 9)* Does evolving in an easier world first, with the score fixed, have support, and when does it fail? Why are sensing-and-steering brains slow to evolve in artificial life, and under what conditions do they evolve? | 🟡 **Answered in part; the nursery's own form is untested in the literature.** **(a) The scheme.** Environmental complexification is one of four incremental schemes, beside staged evolution, fitness shaping and behavioural decomposition [MD08 p.2–3; MD09 p.2]. Direct evolution found no working controller where incremental schemes did [MD08 p.7; MD09 p.5]. Prey capture stalled after about 20 generations directly, and a sequence of harder prey solved it [GM97 p.13–14]. In Sims-style creatures, light following from scratch was "overall not satisfactory", and walkers later given vision mostly evolved it [PJ10 p.5]. Foraging in 3D articulated bodies evolved as noise on food placement rose stage by stage [CA16 p.1, p.15]. Direct optimisation scored 17.9, 39.6 and 13.6 against a solved 230 on POET's environments [WLCS19 p.10]. Varying goals that share structure sped evolution, up to a thousandfold in one model [KNA07 p.3]. **(b) When it fails.** A hand-built direct-path curriculum failed at the very and extremely challenging levels [WLCS19 p.14]. Raising noise too fast left most runs "wander about randomly" [CA16 p.15]. Random goals slowed evolution [KNA07 p.2, p.5]. Variation must not be "so large that reliably fit controllers fail to evolve at all" [JAK97 p.9]. The user must design the sequence [GM97 p.20], and a late switch over-specialises [MD08 p.3]. When only the hardest function paid, none of 50 populations evolved it, against 23 of 50 when simpler functions paid too [LOPA03 p.3, p.5]. Blocking deleterious mutations cut that from 28 of 50 to 16 or fewer [CLWO13 p.2]. **(c) The score.** No paper kept a pure intake score. Every embodied success shaped it: a locomotion term [CA16 p.11], speed toward the light [PJ10 p.4], a bonus for each step closer [GEOP08 p.4]. When all score equally poorly, behavioural diversity bootstraps search [MD09 p.1, p.5]. Novelty search solved a deceptive maze in 39 of 40 runs against fitness's 3 [LST11 p.16–17], but only 5 of 100 in an unenclosed one [LST11 p.19]. **(d) Why slow.** Objective search converged on reactive behaviour and solved under 20% of cognitive tasks [LM14 p.6]. "Turning appears harder to evolve than running" [CA16 p.16], and one target and sustained foraging are "two entirely different skills" [CA16 p.23]. Sims' light following was matched but not exceeded in 19 years [LFM13 p.1–2]. **(e) Conditions.** Dense food lowers the threshold for any perception, and its cost restricts it [SHLF21 p.1, p.8–9]. Long-range gathering was more consistent in larger worlds [BEJ25 p.6]. Taxis evolved within 1,000 to 2,000 updates under a per-step reward, and memory for it could evolve but did worse [GEOP08 p.5]. A bacterium with no spatial sense lengthens its runs going up a gradient [BB72 p.1–2], the temporal comparison Q11 found [MK72 p.1; SBB86 p.1]. **(f) The gap.** No paper moved an evolved sensing-and-steering brain from an easier tank to a harder one. *The design reading, marked as this review's inference: D134's move has a name and a record, and its claim must stay modest because the embodied record shaped its scores. A single ramp is the form most often seen to fail, and a varied world with transfer the form most often seen to work.* |
| Q15 | *(opened round 10)* What should a second-generation brain for these creatures be: how a network should grow, whether one brain per body or one per limb, where the rhythm should live, what finding food by smell asks of a network, and how its founders and mutations should be scaled? | 🟡 **Answered in part; three key primaries unread at source.** **(a) Growth.** NEAT credits its efficiency to crossover aligned by structure, speciation that protects new structure, and growth from minimal structure, each shown necessary by ablation [SM02 abstract]. FS-NEAT learns a network's inputs with its topology and weights; it learned better and faster than NEAT in a racing simulation, with smaller networks and fewer inputs [WSSMK05 abstract]. The value of crossover in NEAT is questioned [SAO22 abstract], and a GA without crossover was competitive on large networks [SUCH17 HTML]. No source read gives an add-node or add-link rate. **(b) One brain or many.** When body and brain co-evolve, a controller reused across modules (two shared networks per robot, a new module taking its parent's) beat a central network, a per-module network and an open-loop sine at generation 500. The central network converged early [KGV22 HTML, Results; Discussion]. Morphological mutations cost less under modular controllers [MEC23 abstract]; body changes tend to hurt control [CB18]. Shared per-limb modules generalise to unseen bodies only with message passing; without it limbs "cannot learn anything meaningful" [HMP20 HTML, appendix]. **(c) Rhythm.** A swimmer whose muscles follow local curvature with a delay swims without a central pattern generator, but only near its resonance frequencies [GAM15 HTML]. In a lamprey model, rhythm is central and feedback restores it below a lesion [HFMT23 HTML]. An open-loop sine was not significantly worse than either CTRNN at 500 generations [KGV22 HTML, Results]; a phase-coupled oscillator beat a sine wave in 2D creatures [VSG23 abstract]. **(d) Smell.** Evolved minimal klinotaxis circuits read the change in concentration from their sensory cells' short windows (rise under 1.7 s), not from network memory [IL10 HTML, Methods; Discussion]. A connectome-constrained ensemble did the same, with 83 of 100 runs reaching a chemotaxis index of 0.5 and 27 reaching 0.75 [IB13 HTML, Results]. Trained plume trackers needed memory, short in steady wind and longer when the wind switched [SBRB23 HTML]. The bacterial differentiator of round 6 compares over 4 s [SBB86 p.1]. **(e) Founders and steps.** LeCun's scaling was not read at source. LSUV was tested on feedforward image networks only and converged more slowly with tanh [MM15 HTML]. **(f) Artificial life.** No comparison of one brain against per-part brains in a Sims-style rigid creature was found. *The design reading, marked as this review's inference: D146's copy of a parent joint's wiring at birth is [KGV22]'s per-module rule, not its winning shared rule; brain v2 reads scent as a level with one step of memory, which these circuits did not rely on; D147 wires every input from birth where FS-NEAT learns its inputs.* |

**PICOC framing** sets the review's terms. *Population:* evolved virtual creatures with genetically encoded 3D morphology and control. *Intervention:* co-evolution of body and controller under
evolutionary / quality-diversity search. *Comparison:* fixed-morphology controller
evolution; direct vs generative encodings; plain GA vs QD. *Outcome:* emergence of
effective locomotion, behavioural and morphological diversity, reproducibility. *Context:*
physics-simulated 3D rigid-body articulated creatures, aquatic and terrestrial.

Three things were deliberately excluded. They are deep-RL locomotion on fixed morphologies,
and sim-to-real transfer, and soft-body or voxel work except where it speaks to encoding, QD
or fluid modelling.

The soft-body exclusion proved to be a judgement call worth revisiting. [EA23] and [C18] are
both soft-body papers and both turned out to be central.

## 3. Search log (PRISMA-S)

### 3.1 Information sources

| Source | Access route | Date | Notes |
|---|---|---|---|
| General web search | `WebSearch` tool, US region | 2026-08-02 | 15 queries in 3 batches |
| CrossRef API | `api.crossref.org` | 2026-08-02 | 8 DOI/bibliographic verifications |
| Publisher sites | SAGE, Wiley, Springer, MIT Press, Mary Ann Liebert, Nature | 2026-08-02 | via browser agent |
| Institutional repositories | Goldsmiths Research Online, UVM MEC Lab, tim-taylor.com, UT Austin NNRG | 2026-08-02 | open-access copies |
| Internet Archive | web.archive.org | 2026-08-02 | Chaumont reprint (lab site had dropped it) |
| arXiv | arxiv.org | 2026-08-02 | Cully et al. preprint v4 |

⚠ This round deviates from the protocol. The prescribed portfolio, with Semantic Scholar,
arXiv, OpenReview, DBLP and OpenAlex queried natively, was **not** followed. Discovery ran
through general web search, which surfaced results *from* those databases without their
field control, date filters or result counts. The consequence is stated in §7.1.

### 3.2 Full search strategies

Batch 1 covered the core concepts.
```
1. "evolving virtual creatures" Karl Sims replication reproduce difficulty reimplementation
2. co-evolution body and brain morphology control premature convergence evolutionary robotics
3. MAP-Elites quality diversity evolving robot morphology illumination algorithm
4. "surprising creativity of digital evolution" physics simulator exploitation anecdotes
5. CPPN generative encoding evolving virtual creatures morphology symmetry regularity
```
Batch 2 chased named authors.
```
6.  Lessin Fussell Miikkulainen evolving virtual creatures behavioral complexity syllabus muscle
7.  Taylor Massey "recent developments" evolution morphologies controllers physically simulated creatures Sims
8.  Vassiliades Mouret "elite hypervolume" Iso+LineDD MAP-Elites variation operator CMA-ME
9.  central pattern generator versus neural network controller evolved locomotion torque position control comparison
10. Krcah "evolving virtual creatures revisited" GECCO Sims reimplementation
```
Batch 3 covered foundations and gap-filling.
```
11. Karl Sims 1994 "Evolving Virtual Creatures" SIGGRAPH "Evolving 3D Morphology and Behavior by Competition" Artificial Life
12. Mouret Clune "Illuminating search spaces by mapping elites" Cully "Robots that can adapt like animals" Nature 2015
13. Lessin "muscle drives" evolved virtual creatures "trading control intelligence for physical intelligence"
14. evolving swimming creatures aquatic locomotion artificial life fluid drag model simulation morphology
15. Gupta "Embodied intelligence via learning and evolution" DERL "Evolution Gym" co-design benchmark
```

### 3.3 Citation searching

Backward snowballing ran from the reference lists of the three fully-read papers.

It yielded thirteen works, none of them retrieved. They are Miconi & Channon 2005/2006, Shim
& Kim 2003, Hornby/Lipson/Pollack 2003, Lipson & Pollack 2000, Bongard & Hornby 2010, and
Mouret & Doncieux 2009. The rest are Lassabe et al. 2007, Pilat & Jacob 2010, Framsticks
(Komosiński & Ulatowski), Terzopoulos et al. 1994, Ventrella 1998, Joachimczak et al. 2016
and Kriegman et al. 2018.

Forward snowballing was not performed at all, and §7.1 carries the consequence.

### 3.4 Limits and deduplication

| limit | setting |
|---|---|
| language | English only |
| date | 1994 → 2026, unbounded, since the field is foundational and Sims 1994 is non-negotiable |
| types | journal articles, conference papers, book chapters, theses, preprints |
| deduplication | manual, by title and first author |
| grey literature | admitted per MLR protocol; none was ultimately used in the synthesis, since the peer-reviewed set proved sufficient |

### 3.5 Search update protocol (PRISMA-S item 12)

This is how to run the next round. It is the reproducible part, and anyone should be able to
execute it without reconstructing intent from scratch.

What triggers a round:

| Trigger | Example |
|---|---|
| A milestone gate | Before Milestone 3, re-check the search-algorithm literature, since that is when §8 stops being theory |
| An item from §9 is picked up | The 2025 co-optimisation preprint; Q2 or Q5 |
| Implementation contradicts the design | If measured behaviour disagrees with a cited claim, the citation gets re-examined rather than the measurement |
| Elapsed time | This field publishes slowly, so annually is ample |

How to execute:

1. **Re-run the §3.2 strings unchanged**, restricted to the period since the last round's
   date, and log the date filter. Unchanged strings are what make round-to-round comparison
   meaningful. If a string needs changing, that is a new question, so add it and say so.

2. **Forward-snowball from the included set**, which is the gap flagged in §7.1. For each
   paper in §6, find work citing it. This matters more than new keyword searches, because
   the included papers are now known-relevant anchors.

3. **Screen against §2's questions** rather than against general interest. A fascinating
   paper that answers none of the six open questions goes in §9 as a lead rather than into
   the synthesis.

4. **Verify before citing**, through the §7.2 cascade. Round 1 met this only partially.

5. **Update the cumulative sections**, per §0's rules.

The next round is already scoped. §9 lists five prioritised items, which are the round-2
backlog, and they do not need re-deriving.

Retiring a claim has its own rule. If a new paper overturns something the design currently
relies on, the old claim is **struck through rather than deleted**, with a pointer to what
replaced it.

The design's history of being wrong is the most useful thing about it. Three corrections in
round 1 are why the current draft is trustworthy.

### 3.6 Round 3 searches (2026-08-29)

Discovery was **delegated to four parallel search subagents**, recorded in §8's
AI-assistance section. Each had a scoped brief and a hard rule set: read-only on the web,
topic keywords only in queries, and fetched content treated as untrusted data.

There were four sweeps.

1. **Artificial-ecosystem primaries**: PolyWorld, Gene Pool, Tierra, Avida, Geb, and any
   system with emergent rather than scripted trophic structure. The terms come from §7.1's
   round-2 backlog: *open-ended evolution, artificial ecosystem, endogenous fitness,
   energy-based selection, Avida, Tierra, PolyWorld, Geb, Ventrella Gene Pool.*

2. **Open-endedness instruments**: Bedau–Packard evolutionary activity, the MODES toolbox,
   OEE definitions and necessary-condition lists, treadmill detection from lineage records.

3. Trophic emergence, recycling, neural cost, motility economics. That covers conditions
   for multi-trophic food webs in evolving models, decomposer and remineralisation loops,
   and precedent for per-neuron metabolic charges. It also covers when locomotion pays,
   through chemotaxis, diel vertical migration and buoyancy trade-offs.

4. Recorded **debts** from earlier rounds. Those are the 2025 co-optimisation preprint (§9 item 1), and independent verification of the two load-bearing §13.4 quarantine entries. The third is forward snowballing from [EA23] and [C18] through the Semantic Scholar citation API.

Verification used the CrossRef API for metadata, and retrieved eight open-access primaries.
Six of those were then re-verified **by the reviewing model directly against the downloaded
text** at the specific passages cited, as `FETCH-RESULTS.md`'s round-3 section records.

⚠ Round 1's protocol deviation, general web search rather than native database queries,
repeats here. It carries the additional layer that discovery ran through subagents, which
§7.1 states.

### 3.7 Round 4 searches (2026-09-01)

Two parallel search subagents ran this round, on Claude Opus, with scoped briefs, read-only
web access and open-access retrieval only (§8). The reviewing model screened and integrated
their results.

These are new questions, so they use new strings, declared per §3.5 rule 1. The §3.2 strings
were *not* re-run. Three days had elapsed since round 3, and a date-filtered re-run over
that window is vacuous. The skip is logged here so that it is a recorded decision rather
than an omission.

Sweep 1 covered consumer–resource stability theory.

> paradox of enrichment; Rosenzweig–MacArthur stability; prey refuge predator-prey stability
> (fixed number vs proportional); donor control food web stability; detritus food web
> dynamics; functional response type I II III stability; predator interference
> Beddington–DeAngelis

Sweep 2 covered spatial structure and persistence.

> Huffaker predator-prey dispersion; spatial rescue paradox of enrichment; diffusively
> coupled patches predator-prey; metapopulation predator-prey persistence asynchrony;
> protist microcosm persistence; Avida spatial structure well-mixed comparison; dispersal
> rate window synchrony; mobility biodiversity lattice

Two retrieval notes have teeth. Hilgardia's own PDF link has rotted, and [HUF58] was fetched
from a Wayback capture of the journal's file endpoint.

Elsevier, Wiley, bioRxiv and HAL all bot-gated requests this round, with 403, 503 and
Anubis. That cost the corpus Jansen 2001, Bonsall 2002, Dolson 2017's page-level text and
Quévreux 2021, despite three of the four being nominally free. The manual-fetch queue in §9
grew accordingly.


### 3.8 Round 5 searches (2026-09-03)

No subagents ran this round. Discovery, retrieval, extraction and verification were all done
by the reviewing model directly. That removes round 3's and round 4's relay threat (§7.1),
at the cost of a narrower search than four parallel sweeps produce.

The §3.2 strings were *not* re-run. Two days had elapsed since round 4, so a date-filtered
re-run is vacuous. The skip is logged here as a decision rather than an omission (§3.5 rule
1).

Forward snowballing was not performed this round. The round's anchors were named in the
brief, and the corpus was assembled backwards from their reference lists instead. §7.1
records it.

There were three themes and eleven queries. Web search returned 9–10 result links per query.
The tool reports links rather than database result counts, which is the §7.1 deviation,
unchanged.

Theme A asked how much producers release.
```
1. Baines Pace 1991 "percentage extracellular release" phytoplankton dissolved organic
   carbon Limnology Oceanography
2. Thornton 2014 "dissolved organic matter" phytoplankton release European Journal of
   Phycology review PDF
3. "Baines" "Pace" 1991 "production of dissolved organic matter by phytoplankton" pdf
   repository free full text
4. Marañón Cermeño Fernández Rodríguez Zabala 2004 "significance and mechanisms of
   photosynthetic production of dissolved organic carbon" Limnology Oceanography digital.csic
5. "percentage extracellular release" phytoplankton review open access Biogeosciences OR
   Frontiers OR PLOS PER 5-70% nutrient status
6. Thornton "DOM release by phytoplankton in the contemporary and future ocean" oaktrust OR
   repository OR researchgate full text pdf 2014
```
Theme B asked what one percent should be compared against.
```
7. Pauly Christensen 1995 Nature "primary production required to sustain global fisheries"
   transfer efficiency 10%
8. Eddy Bernhardt Blanchard 2021 "Energy Flow Through Marine Ecosystems: Confronting
   Transfer Efficiency" pdf repository accepted manuscript
```
Theme C asked whether the detritus and microbial route is a real path to consumers.
```
9.  Azam 1983 "ecological role of water-column microbes in the sea" pdf mirror site:edu OR
    site:org full text
10. "bacterial carbon demand" percentage "of primary production" microbial loop review open
    access "50%" OR "30-60%" pelagic
11. "m010p257" OR "Azam" "Fenchel" "Field" "Gray" "Meyer-Reil" "Thingstad" 1983 microbial
    loop pdf course reading
```

Open-access triage went by API rather than by guessing. The Semantic Scholar graph API was
queried for one title, and then in one batch for nine DOIs, reading the `openAccessPdf`
status field. The Figshare API was queried for the one GREEN record it returned.

That record turned out to be **link-only metadata pointing back at the publisher DOI**. It
is a green-OA listing with no green copy behind it, which is worth knowing about as a
retrieval failure mode.

Three BRONZE records (Wiley, Cell Press) and two publisher hosts (Taylor & Francis,
Inter-Research) returned 403 and 401 bot challenges to a non-browser client. None was
bypassed.

**Backward snowballing from the fetched set** supplied the round's most useful pointers. They are Nagata 2000's 10–20% range, Karl et al. 1998's ~30% for the North Pacific gyre, and Teira et al. 2001's 23% for the North Atlantic gyre. The other two are Joint & Pomroy 1983's 15% for the Celtic Sea, and Thomas 1971's 44% for the Sargasso.

Each of those is recorded at the point of use as *cited through* a fetched paper, never as a
primary read.

### 3.9 Round 6 searches (2026-09-07)

Four search subagents ran in parallel, as in round 3, each with a scoped brief and the same
hard rules: read-only on the web, no login, no paywall, CAPTCHA or bot-gate bypassed,
fetched content treated as data, queries made of topic keywords only, and no file written
anywhere. Each reported candidates with title, venue, DOI, the open-access route it saw and
two sentences on what the paper says, marking what it read apart from what it inferred.
Screening, retrieval, extraction and verification stayed with the reviewing model.

The §3.2 strings were not re-run. Q11 is a new question with its own vocabulary, and four
days had passed since round 5, so a date-filtered re-run is vacuous; the skip is logged per
§3.5 rule 1. Forward snowballing was not performed; the round's anchors were named in the
briefs and the corpus was assembled from them. §7.1 records both.

The four sweeps:

1. **When undirected movement pays.** Diffusion-limited uptake and the depletion halo;
   Purcell's "greener pastures"; the size threshold for useful locomotion; encounter-rate
   theory for particle feeders; nutrient flux to absorbers under fluid motion; dispersal
   from a depleted neighbourhood.
2. **How sensing first coupled to movement.** Kinesis against taxis; bacterial chemotaxis
   as temporal comparison; the biased random walk; the co-option of metabolic sensors;
   energy taxis; minimal chemotaxis agents.
3. **The first sensorimotor unit in animals.** Input-output against internal-coordination
   accounts of early nervous systems; single cells that sense and move; the division of
   labour among cell types; myoepithelia and neuroid conduction; independent origins of
   neurons; the nerve net as a pattern generator.
4. **Artificial-life precedent.** Energy-charged systems in which movement and neurons are
   priced, and whether any observed the bootstrapping problem: PolyWorld, Geb, Avida,
   Gene Pool, Framsticks, minimal cognition, evolutionary robotics.

Retrieval used Europe PMC's render endpoint for the PubMed Central records, three author or
institutional self-archives (Cambridge DAMTP for [P77], the Stocker laboratory for [TS12],
the University of Maine for [KB96], Groningen's repository for [KDL13]) and arXiv for
[HG25]. Two retrieval facts are worth recording. PubMed Central itself now answers a
non-browser client with a proof-of-work JavaScript challenge (`cloudpmc-viewer-pow`); it was
not solved, and Europe PMC's public endpoint served the same records. Europe PMC
rate-limits with 429 after a burst; the retrieval script paces at 8 s and the two records
it refused were fetched on a second pass 20 s apart.

Seventeen works were recorded closed or bot-gated and not fetched: Fraenkel & Gunn 1940,
Berg & Brown 1972, Adler 1966, Wadhams & Armitage 2004, Wuichet & Zhulin 2010 (course-hosted
copies exist and were not used), Mitchell 2002, Mitchell & Kogure 2006, Gerritsen & Strickler
1977, Visser & Kiørboe 2006, Kiørboe 2011, Fenchel 2002, Arendt 2008, Arendt et al. 2016,
Mackie 1970, Bosch et al. 2017, Egbert et al. 2012 and Elsberry et al. 2009.

### 3.10 Round 7 searches (2026-10-05 and 2026-10-06)

Round 7 ran in two passes, and this section summarises the working files that hold its
queries, its pool, its checks and its extraction. Pass 1, on 2026-10-05, built a candidate pool.
Pass 2, on 2026-10-06, verified ten of the candidates and read them. A separate session then
filed the round here from those files alone, with no new search and no new reading.

The working files are kept in `logbook/specs/r57-lit/`: `search-log.md`, `pass1-candidates.md`,
`pass2-verification.md` and `pass2-extraction.md`. They were committed with round 57's
pre-registration on the `round-57` branch, at `1917815`, and reach main when that branch is merged. The
full-text renderings that pass 2 read from were not kept, as no PDF is kept for this round. The two
renderings of [WYRW25]'s preprint that the verification record names were already missing when the
round was filed. So [WYRW25]'s preprint claims rest on the extraction alone, and cannot be re-checked
from the round's files.

The §3.2 strings were not re-run. Q12 is a new question with its own vocabulary, so the skip
is logged here per §3.5 rule 1. Forward snowballing was not performed, and §7.1 records it.

Pass 1 ran 43 queries through the web search tool, which returns about ten links a query and
no database counts. The queries fell into five themes, which the pool kept as its own
sub-questions.

1. Bet-hedging theory was sought by name. The names were Seger & Brockmann 1987, Philippi
   & Seger 1989, Cohen 1966, Lewontin & Cohen 1969, Starrfelt & Kokko 2012, Haaland et al.
   2019, Kussell & Leibler 2005 and Botero et al. 2015.
2. The experimental evolution of bet-hedging was sought by name as well. The names were
   Beaumont et al. 2009, Graham et al. 2014, Tarazona et al. 2017, Simons 2011, Childs et
   al. 2010 and Venable 2007.
3. Storage under seasonality and unpredictability had open queries on the evolution of
   energy reserves. It also had title searches for Fischer et al. 2011, Varpe et al. 2009,
   Iwasa & Kubo 1997, Chapin et al. 1990 and Blumstein et al. 2022.
4. Environmental autocorrelation covered coloured noise and extinction risk (Ruokolainen
   et al. 2009, Schwager et al. 2006, Halley 1996, Vasseur & Yodzis 2004). It also covered
   evolution under that noise (Metcalf & Koons 2007, Tufto 2015, Wieczynski et al. 2018,
   Rescan et al. 2021).
5. Artificial-life and individual-based simulation covered Avida in changing environments
   and evolved storage in agents. It also covered virtual plants under light competition
   and noise colour in artificial evolution (Grove et al. 2020 and 2021).

Thirty-one of the queries named a particular work and three more named an author, which is
the confirmation risk §7.1 states. Query 13 caught a misattribution. The title it searched
under Fischer, Taborsky and Dieckmann is Varpe et al.'s, and it led to Fischer et al. 2011
under its own title.

Abstracts were read through the Europe PMC REST search, PMC, arXiv and the OpenAlex
abstract index. Nature, Springer, Wiley, ResearchGate, PubMed and ADS refused or redirected
the fetcher, and none of those pages was read. Several pass-1 abstracts, Cohen 1966's, Varpe et
al.'s and Tarazona et al.'s among them, were known only from the search engine's summary.
Pass 2 then read Varpe's and Tarazona's through OpenAlex and Semantic Scholar.

Pass 2 checked the ten against CrossRef, OpenAlex, Europe PMC, the Semantic Scholar graph
API and, for one preprint, the bioRxiv API. Full texts came from legal open copies: PMC and
Europe PMC XML, PMC article pages, and bioRxiv HTML. No PDF was downloaded, so no package
was made under `research/papers/`. Three open copies were gated and were not bypassed.
Wiley's free PDF link for [SK12] returned 403. The Universitat de València repository copy
of [TGC17] sat behind a bot challenge. The eScholarship copy of [WYRW25] refused the fetcher
with a firewall challenge. The fetch registry, `FETCH-RESULTS.md`, records each route.

### 3.11 Round 8 searches (2026-09-29, filed 2026-10-31)

Round 8 ran in two passes on 2026-09-29, each by a Claude Opus 5.5 research subagent. It
was filed here on 2026-10-31 from its working files. It is numbered after round 7, which
was searched later and filed first.

Pass 1 was briefed with D128 and the gene as built. Its report is
`logbook/specs/r52-lifespan-literature.md`. It logged no queries and saved no copy. It read
42 sources by abstract, by section, or "through a summariser", meaning the web tool's
digest of a page. Its values went to the owner the same day (logbook/0127).

Pass 2 re-appraised pass 1 against a fresh search, under six questions that §2 holds as
Q13. Its report is `logbook/specs/r52-lifespan-literature-r7.md`, whose title calls it
round 7 because it was written before round 7 existed. Its query and retrieval logs are
in `logbook/specs/r52-literature/`, and its tools in `scripts/lit/r7/`.

Pass 1 folds into this round as its first pass. Both passes answered one commission on
one day, and pass 2 took pass 1 as its brief. Pass 1 is also the evidence the design
actually used. Its claims enter the synthesis only where pass 2 re-read them; the rest are
leads (§9 item 14).

Pass 2 searched through APIs rather than the web search tool. OpenAlex took 42 keyword or
filter queries, sorted by relevance or by citations, and 5 forward snowballs by its
`cites:` filter. Semantic Scholar took 3 keyword queries and 6 bulk queries. Seven more
keyword attempts failed with HTTP 429. Lookups then fetched metadata for 50 candidate DOIs
from each service, and CrossRef checked 42 DOIs for retractions and corrections.

The §3.2 strings were not re-run. Q13 is a new question, so the skip is logged here per
§3.5 rule 1. The forward snowball ran from five anchors only: Westendorp & Kirkwood 1998,
Boonekamp et al. 2014, [COL21], [PER19] and [ZBH95]. It is the first forward snowball
since round 3, and a partial one.

Retrieval kept 14 PDFs as `research/papers/85` to `98`. Two Glasgow repository copies,
[BBV20] and [RM19], needed a curl retry after reads were cut short. PubMed Central's PDFs
sat behind its proof-of-work gate, so the round read its records as article pages and
saved none. Cloudflare gated the Royal Society, Hindawi, Wiley, OUP and genetics.org,
biologists.com, PNAS, Groningen and Edinburgh. Duke's Anubis and HAL challenged the
fetcher, Kent returned 504, Texas Tech served an empty file and rero.ch answered 410. None
was bypassed.

The session that filed the round read no new source. It hashed the fourteen PDFs and
re-located the round's page-cited claims in thirteen of them, through the text layer. That
corrected one locator. A second check on 2026-10-31, against the text extracted at
filing, confirmed every page (§4).

### 3.12 Round 9 searches (2026-09-29, fetched 2026-09-30, filed 2026-10-31)

Round 9 ran in four steps. Its working files call it round 7 and its question Q12, because
they were written before those numbers went to the seasonal-light round (§3.10).

The first step was a report by a Claude Sonnet research agent on the night of 2026-09-29,
`logbook/specs/nursery-curriculum-literature.md`. It answered the owner's question on D134.
It logged no queries, and read thirteen sources at abstract or search-snippet level. D134
cites it.

Pass 1 was the `literature-review` skill's first pass, by a Claude Sonnet subagent the same
night. Its pool and log are `research/nursery-curriculum/pass1-candidates.md`. It made 60
web search and fetch calls, about ten results each, first page only. It used an unfiltered
search engine and site filters on pnas.org, PubMed, Semantic Scholar, arXiv, OpenReview and
DBLP. Google Scholar and the journal sites were not searched. The query list stayed in a
session record that was not kept, and the log names its themes instead.

The pool holds 46 rows. Strand A, 27 rows, covers environmental curricula and incremental
evolution. Strand B, 19 rows, covers why sensing and steering evolve slowly, and its numbering
skips B16. Two rows were already held, [HG25] and [DO21].

The owner kept twenty-one papers: twenty from the pool, and Berg & Brown 1972 from round 6's
queue. The brief is `research/nursery-curriculum/round7-fetch-brief.md`. A Claude Sonnet
session with a browser fetched them on 2026-09-30, with the owner signing in to each
publisher. Ten came through institutional access and ten by open routes. Soros & Stanley 2014
was not obtained. Each PDF was matched against its first page. The `pdf-clean-markdown`
skill then built a package with `source.md`, `manifest.json`, figures, tables and a `JART.md`.

Pass 2 read the twenty `source.md` files on 2026-10-31, at the passages that bear on Q14. It
did not rely on the `JART.md` files. It re-hashed the twenty PDFs, and every hash and size
matched the fetch record. It ran no search, database lookup or retraction check (§7.2).

The §3.2 strings were not re-run. Q14 is a new question, so the skip is logged here per §3.5
rule 1. No forward snowball was done.


### 3.13 Round 10 searches (2026-10-01 and 2026-10-31)

Round 10 ran in two passes. Pass 1, on 2026-10-01, was a Claude Sonnet subagent's web search for
candidates. Its table and its 17 queries are in `research/brain-v2/pass1-candidates.md`. It opened
nothing and saved nothing. Its own note says that every query named an expected paper. It also says
its sub-questions Q2 and Q4 most needed a pass framed by the question.

Pass 2, on 2026-10-31, was one Claude Opus 5.5 research subagent. It drafted the round for filing
and kept no copy of anything it read. Pass 1's sub-questions Q1 to Q4 became Q15's parts (a) to (d).
Its Q5, on starting weights and mutation steps, became part (e), which also answers D147's request.

The §3.2 strings were not re-run. Q15 is a new question with its own vocabulary, so the skip is
logged here per §3.5 rule 1. Forward snowballing was not done.

Pass 2 ran 14 searches through the web search tool, which returns about ten links a query and no
database counts. Nine named a work. Five were framed by the question, marked Q below.

1. `centralized versus decentralized control co-optimization morphology modular robots arXiv 2206.13366`
2. Q `evolved virtual creatures single central controller versus distributed per-limb neural controllers comparison body changes`
3. Q `evolved recurrent neural network agent chemotaxis gradient climbing memory temporal derivative minimal circuit`
4. `Modular Controllers Facilitate the Co-Optimization of Morphology and Control in Soft Robots GECCO 2023 arXiv`
5. `Cheney Bongard SunSpiral Lipson 2018 "Scalable co-optimization of morphology and control in embodied machines" Journal of the Royal Society Interface`
6. `proprioceptive feedback amplification restores effective locomotion lamprey PNAS 2023`
7. Q `swimming without a central pattern generator sensory feedback alone generates undulation neuromechanical model`
8. `Izquierdo Lockery 2010 Evolution and analysis of minimal neural circuits for klinotaxis in Caenorhabditis elegans Journal of Neuroscience PMC`
9. `Hamlet Fauci Morgan Tytell 2023 PNAS e2213302120 PMC full text lamprey neuromechanical model`
10. `Beer Gallagher 1992 "Evolving dynamical neural networks for adaptive behavior" Adaptive Behavior chemotaxis legged locomotion abstract`
11. Q `evolved neural network agent odor plume tracking turbulent source localization evolutionary algorithm recurrent memory needed`
12. Q `neuroevolution comparison growing topology from minimal versus fixed topology dense network performance study NEAT ablation replication`
13. `Whiteson Stone Stanley Miikkulainen Kohl "Automatic feature selection in neuroevolution" FS-NEAT GECCO 2005 irrelevant inputs`
14. `Huang Mordatch Pathak 2020 "One Policy to Control Them All" shared modular policies agent-agnostic control arXiv`

Queries 13 and 14 named works the reader knew of before the search, not works from pass 1. That is
the confirmation risk §7.1 records. Query 5 found that pass 1's Cheney et al. 2018 is already held
as [CB18].

Ten lookups fetched metadata and abstracts. OpenAlex took four DOIs ([SM02], Ijspeert 2008, [IHW99],
[SAO22]) and two title searches ([KGV22], and Beer & Gallagher 1992, which returned unrelated
works). Semantic Scholar took one DOI, Ijspeert 2008, whose abstract it elides. CrossRef took one
filter of ten DOIs, one bibliographic query for [WSSMK05] and one DOI for [KGV22].

Full texts were read as HTML from PubMed Central and ar5iv. Every page reached the reader through
the fetch tool, which returns a small model's answer to a prompt rather than the page's raw text.
Every quotation in this round is that tool's report of the page (§7.1).

Two arXiv PDFs, 2206.13366 and 2306.09358v1, were requested first. The tool could not read them and
stored copies outside the repository. Neither was opened. No PDF was requested after that, so every
source whose only open copy is a PDF was read at the abstract or not at all.

Several pages refused. The MIT Press page for [SM02] returned 403, and so did Stanley's NEAT users
page at cs.ucf.edu. EPFL Infoscience returned 429 twice, for Ijspeert 2008 and [IHW99]. Springer
redirected [SAO22] to a login, which was not followed. The ar5iv conversion of arXiv 1107.0037
(Stanley & Miikkulainen 2004) had failed. ar5iv holds no HTML for 2306.09358 and redirected to its
abstract. None was bypassed.

## 4. PRISMA flow

*These counts are cumulative through round 1. Later rounds add to them rather than replacing them, as §0 says.*

```
IDENTIFICATION
  Records surfaced by 15 web-search queries                       ~120 result links
  Records added by backward snowballing                            ~13
                                                                  ─────
SCREENING
  Unique candidates after dedup + title/venue screening              28
  Excluded at user checkpoint as out-of-scope                         3
     (deep-RL fixed-morphology; soft-voxel encoding adjacents)
                                                                  ─────
RETRIEVAL
  Judged likely paywalled → fetch queue                               8
  Successfully retrieved                                            8/8
     via open access                                                  6
     via institutional access (university subscription)               2
  Openly available, identified, NOT retrieved                       ~17
                                                                  ─────
APPRAISAL
  Read in full                                                        3   [K12] [U07] [EA23]
  Read in part (targeted sections)                                    2   [L21] [C18]
  Retrieved but unread                                                3   [TM01] [CEA07] [CU15]
                                                                  ─────
  INCLUDED IN SYNTHESIS                                               5
```

Verification cross-checked 8 records against CrossRef, on title, authors, year, venue,
volume, pages, DOI and retraction status. There were **no retractions**.

Four metadata errors in the working candidate list were caught and corrected. Krčah's
chapter is 2012 rather than 2011, and Eguiarte-Morett published online in 2023 in the 2024
issue. Two DOIs were missing, and one record, Usami, had no author attributed at all.

Round 3 additions (2026-08-29):

```
IDENTIFICATION
  Candidates surfaced by 4 subagent search sweeps                  ~45
  Citing works surfaced by forward snowballing ([EA23], [C18])     ~49 (3 + ~46)
SCREENING
  Screened into the working pool (reported per sweep)               ~40
  Triaged citing works retained as relevant                         ~13
RETRIEVAL
  Targeted for retrieval (open access)                                9
  Successfully retrieved                                            8/9
     (the MODES preprint is free but bot-gated — queued for manual fetch)
APPRAISAL
  Key passages verified against primary text by the reviewer          4   [Y94] [MC25] [CO02] [CB18]
  Read in part (full text by search agent, spot-checked)              2   [VG05] [PU16]
  Retrieved, abstract-level only                                      2   [GOY23] [ST00]
  ADDED TO SYNTHESIS                                                  6   [Y94] [MC25] [CO02] [VG05] [CB18] [PU16]
  Leads recorded, not retrieved (paywalled or gated)                 ~12   see §9
```

Round 3 left two metadata notes. [CO02] has **no DOI**, checked three ways against CrossRef,
and is sometimes miscited as 2003, where the PDF header confirms Artificial Life VIII, 2002.

[MC25] is arXiv:2508.17464v2, the accepted-manuscript version for *Artificial Life*,
extending an ALIFE 2025 paper. Cite the journal version once it has volume and page
identity.

Round 4 additions (2026-09-01):

```
IDENTIFICATION
  Candidates surfaced by 2 subagent search sweeps                  ~50
SCREENING
  Screened against Q9 (per-theme primaries + leads)                ~35
RETRIEVAL
  Targeted for retrieval (open access)                              14
  Successfully retrieved                                          14 (the count read 13/14 until 2026-09-07; the registry's fourteen round-4 entries were recounted)
     (Dolson et al. 2017: bioRxiv CDN refuses non-browser clients —
      abstract only, no page locators)
  Relevant, recorded closed-access, NOT fetched, NOT bypassed       ~25
     (incl. Rosenzweig 1971 itself, Jansen 1995, Briggs & Hoopes
      2004, McNair 1986, Sih 1987, Maynard Smith 1974)
APPRAISAL
  Load-bearing claims re-verified by the reviewer vs extracted text   4   [KR13] [MO04] [RMF07] [JN97]
  Read at targeted passages by sweep agents, locators checked by them 6   [HUF58] [FM15] [HZ13] [RC07] [JKT04] [DBWM05]
  Fetched, screened OUT of the synthesis                              3   (Chen 2023 GLV review; Mougi 2022; Moreno 2024)
  ADDED TO SYNTHESIS                                                 10
```

Round 4 left two metadata notes. Several theme primaries are cited here only through fetched
secondaries. Rosenzweig 1971 comes through [RC07 p.3], Maynard Smith 1974 through [KR13
p.1], and Briggs & Hoopes 2004 through its NCBI abstract. Each such use is marked at the
point of citation.

The DeAngelis-Goldstein-O'Neill 1975 title contains a published typo, "Tropic", which is
preserved here because correcting it breaks DOI lookup by title.


Round 5 additions (2026-09-03):

```
IDENTIFICATION
  Records surfaced by 11 web queries (3 themes)                    ~100 result links
  Records screened for open access via Semantic Scholar API           10 DOIs + 1 title
SCREENING
  Named anchors from the brief, screened against Q10                  ~12
  Candidates carried forward to retrieval                                9
RETRIEVAL
  Targeted for retrieval (open access or green/bronze)                   9
  Successfully retrieved                                               7/9
     (Baines & Pace 1991 and Thornton 2014 both bot-gated — see below)
  Relevant, recorded bot-gated or closed, NOT fetched, NOT bypassed      6
     (Baines & Pace 1991; Thornton 2014; Nagata 2000; Fogg 1983;
      Azam et al. 1983; Fenchel 2008 — plus Marañón et al. 2004,
      Teira et al. 2001, Moran et al. 2022, all Wiley-gated)
APPRAISAL
  Every numeric claim located and re-read by the reviewer in the
  extracted source.md before entering a table                          7   all of them
  Read in full                                                         2   [LS13] [PWAH07]
  Read at targeted passages (abstract + results + discussion)          5   [PC95] [MCP05] [LS11] [CH20] [ED21]
  ADDED TO SYNTHESIS                                                   7
  Cited but NOT obtained, flagged as such at every use                 1   [BP91]
```

Verification cross-checked all 8 round-5 records against CrossRef, on title, volume, issue,
pages, year and retraction status. There were **no retractions**, and the 7 fetched records
were also confirmed against their own title pages.

Round 5 left three metadata notes, all of them caught by reading the PDF rather than the
index.

[LS13]'s title is "Exudation of organic carbon by marine phytoplankton: dependence on taxon
and cell size". Several indexes, including the author's own site listing, transpose it to
"Organic carbon exudation in marine phytoplankton: dependence on cell size and taxon". The
PDF is authoritative.

[ED21] is the UC eScholarship accepted manuscript rather than the version of record, so its PDF pages do **not** correspond to *Trends in Ecology & Evolution* 36(1):76–86. Locators here are PDF pages of the accepted manuscript, and a claim needing the journal's pagination must be re-anchored.

[CH20] is Chen, Lai, Shiah & Gong, where a search index attributed it to Tsai.

[PWAH07] is by Pomeroy, Williams, Azam and Hobbie. Three of those are among the six authors
of the 1983 paper that named the microbial loop. That is the nearest thing to primary
provenance obtainable while Inter-Research is bot-gated.

Round 6 additions (2026-09-07):

```
IDENTIFICATION
  Records surfaced by 4 subagent sweeps (about 30 queries)           ~55 candidates reported
SCREENING
  Candidates screened against Q11 by the reviewer                       55
  Carried forward to retrieval (open-access route seen)                 19
RETRIEVAL
  Successfully retrieved                                             17/19
     (Mitchell & Kogure 2006 and Egbert et al. 2012: no PMC record, publisher closed)
  Relevant, recorded closed or bot-gated, NOT fetched, NOT bypassed     17   (§3.9 lists them)
APPRAISAL
  Every claim used located and re-read by the reviewer in the
  extracted source.md before entering a table                          17   all of them
  Read in full                                                          0
  Read at targeted passages (abstract, results, discussion)            17
  ADDED TO SYNTHESIS                                                   17
```

Verification this round was against each PDF's own title page and the passages cited, not
against CrossRef; §7.2 records the gap. Two extraction notes: PyMuPDF renders Dusenbery's
"µm" as "mm" in [D97]'s text, and the figure is 0.6 µm, as its own comparison with bacterial
lengths of 0.1 to 0.6 µm makes plain [D97 p.4]; and [KB96] is a scanned review whose text
layer is clean at the abstract and rough in the figures.

Round 7 additions (2026-10-05 and 2026-10-06):

```
IDENTIFICATION
  Records surfaced by 43 web queries (5 themes)                    ~409 result links
  Abstract fetch attempts (6 refused or redirected)                   31
SCREENING
  Candidate pool, ranked against five sub-questions                    30
  Seen and kept unranked (title only, or weaker for the round)          8
  Seen and not carried (no abstract read)                               3
     (Simons 2009; McNamara & Houston 1996; Tuljapurkar et al. 2006)
  Carried forward to verification and reading                          10
RETRIEVAL
  PDFs downloaded                                                       0
  Read in full from a legal open copy (XML or HTML), not saved          5   [FDT11] [PW21] [GSS14] [MK07] [BWWR15]
  Preprint read in full, journal version abstract only                  1   [WYRW25]
  Abstract only                                                         4   [WTV18] [SK12] [VJTF09] [TGC17]
  Open copy gated, NOT bypassed                                         3   [SK12] [TGC17] [WYRW25]
APPRAISAL
  Matched in at least two of CrossRef, OpenAlex, Europe PMC,
  Semantic Scholar, bioRxiv                                            10   all of them
  Retractions                                                           0
  ADDED TO SYNTHESIS                                                   10
```

The abstract-fetch count is the rows of the round's fetch table. Nature and Springer
redirected to a login, PubMed showed a cookie wall and ResearchGate returned 403. ADS
refused twice, so Tarazona et al. 2017 and Cohen 1966 fell back on the search summary in
pass 1. Wiley also returned 403 once, and Europe PMC served that abstract instead.

Round 7 is the first round to meet §7.2's two-database rule for every record. A record
counted as free of retraction when its CrossRef entry had no update or retraction relation
and OpenAlex reported it not retracted.

Round 7 left five metadata notes.

CrossRef gives [FDT11]'s issue date as May 2011 and OpenAlex gives 2010, the online-first
year, so this review cites it as 2011.

[GSS14]'s third author is Simons. One pass-1 query gave the third author as Hsu, and the
search result corrected it.

[WYRW25]'s preprint lists three authors and has no Yin, and the journal version adds a
*Clarkia xantiana* case that version 2 of the preprint lacks. Every claim this review takes
from the preprint may differ in the journal.

[TGC17] is *Oikos* 126(8). Its design is read from the same group's open 2019 paper in
*Scientific Reports*, whose article number was not checked, so that paper is cited by DOI
only.

CrossRef added end pages and issues to pass 1's records ([FDT11] ends at 1232; [PW21] is
24(11):2452–2463; [WTV18] is 191(6):E195–E207). It contradicted none of them.

Round 8 additions (2026-09-29, filed 2026-10-31):

```
IDENTIFICATION
  Pass 1: queries not logged; sources in its table                    42
  Pass 2: query attempts in search-log.tsv                            63
     returned results (42 OpenAlex keyword or filter,
     5 OpenAlex forward snowballs, 3 S2 keyword, 6 S2 bulk)           56
     failed, Semantic Scholar HTTP 429                                 7
  Records screened by title (the report's figure)          736 (701 unique)
SCREENING
  Screened on abstract (the report's figure)                         ~70
  Assessed (the report's figure)                                      51
  Appraised in the report's table                                     39
     core                                                             14
     supporting                                                       25
  Leads named, not appraised                                           6
  Pass-1 sources carried as unverified leads                          21
RETRIEVAL
  PDFs retrieved and kept (papers/ 85 to 98)                          14
  Read as PubMed Central article pages, not saved                      9
  Abstract only                                                       16
APPRAISAL
  DOIs matched at CrossRef and OpenAlex                               39   all included
  DOIs checked at CrossRef for updates                                42
  Retractions                                                          0
  PDFs whose page-cited claims the filer re-located                   13
  ADDED TO THE SYNTHESIS MATRIX                                       14
  Cited outside the matrix (supporting)                               25
```

Round 8's counts come from a report and from logs that do not agree. This block takes the
logs and the appraisal table over the report's prose. The report gives 49 logged queries,
35 OpenAlex queries, and Semantic Scholar failing on 7 of 12 attempts. The log holds 63
attempts, 47 OpenAlex queries and 10 Semantic Scholar keyword attempts. The report gives 13
core and 20 supporting papers, and its table lists 14 and 25. The screened, assessed and
abstract counts cannot be re-counted from the logs, which record database hit totals.

The article-page count is the appraisal table's. The retrieval log also holds pages for
[CIC97], [H92] and [MIT01], which the table cites at the abstract, so they are counted as
abstracts here.

Round 8 left five metadata notes.

[WIN25]'s PDF is the eLife version 4, DOI `10.7554/eLife.87018.4`, titled "The optimal
clutch size revisited". Pass 2 reports a retitled version of record, which the filer did not
check.

[BOO14] carries an addendum, DOI `10.1111/ele.12404`, whose results are unchanged.

Semantic Scholar and OpenAlex citation counts disagree materially for [KIR15], [SWO21] and
the [BOO14] addendum.

[MR87]'s *Drosophila* figure of 0.12 is Roff & Mousseau 1987's, quoted at [MR87 p.8]. Its
scanned text layer drops decimal points, so 0.262 reads as "0262".

The filer's check moved [PEN95]'s faster-than-exponential decline to p.3, where the report
gave p.2–3. That paper never names Gompertz.
[PER19]'s caveat that its data are zoo animals stands at p.2 and again at p.6. There the
animals "were born and died in zoological institutions".

Round 9 additions (2026-09-29, fetched 2026-09-30, filed 2026-10-31):

```
IDENTIFICATION
  First report: queries not logged; sources listed (A to M)          13
  Pass 1: web search and fetch calls                                 60
SCREENING
  Candidate pool rows (strand A 27, strand B 19)                     46
     already held ([HG25], [DO21])                                    2
  Kept by the owner for retrieval                                    21
     from the pool                                                   20
     from round 6's queue (Berg & Brown 1972)                         1
RETRIEVAL
  PDFs obtained (papers/ 175 to 192, 194, 195)                    20/21
     through institutional access                                    10
     open access or publisher page                                   10
  Not obtained (Soros & Stanley 2014)                                 1
APPRAISAL
  Read at the passages cited, in source.md                           20
  Read in full                                                        0
  Database cross-match or retraction check                            0
  Hashes re-checked against the fetch record                         20
  ADDED TO THE SYNTHESIS MATRIX                                      15
  Cited outside the matrix (supporting)                               5
```

Eight of the first report's thirteen sources were fetched. The five not fetched were Kashtan &
Alon 2005, Brant & Stanley 2017, Black et al. 2020, Lehman et al. 2020 and a specification-gaming
list that is not peer reviewed.

Round 9 left seven metadata notes.

[GEOP08]'s page 1 names Grabowski, Elsberry, Ofria & Pennock. Pass 1 and the fetch brief gave
Beckmann, McKinley & Ofria.

[CLWO13]'s page 1 names Covert, Lenski, Wilke & Ofria. The first report gave five other names.
Its "five of 23" is Covert's account of [LOPA03] [CLWO13 p.2].

[JAK97] tests a Khepera T-maze and a gantry robot. The octopod in the first report is a separate
1997 Sussex report, seen only in [JAK97]'s references.

[LST11] is the authors' 39-page layout from a course page, where the journal runs 35 pages. Its
locators are PDF pages.

[WLCS19] is arXiv version 3, and no peer-reviewed version was checked. Without transfer, no
extremely challenging environment was *generated* [WLCS19 p.19]. The first report said none was
solved.

[SHLF21]'s abstract names four best predictors: resource density, basal cost, perceptual cost and
mutation rate. The first report's "strongest effect" for density is not the paper's.

[GM97], [JAK97] and [BB72] are scans with rough OCR. [MD09] and [LST11] carry no DOI in the
record, and [BEJ25] and [TEN26] are arXiv preprints.

Round 10 additions (2026-10-01 and 2026-10-31):

```
IDENTIFICATION
  Pass 1 (2026-10-01): web searches                                   17
     candidates in its table                                           20
  Pass 2 (2026-10-31): web searches (9 named, 5 by question)          14   ~140 result links
  Pass 2: metadata and abstract lookups (OpenAlex 6, S2 1, CrossRef 3) 10
SCREENING
  Pass-1 candidates re-read in pass 2                                   9
     already held ([CB18])                                              1
  New candidates seen in pass 2's search summaries                    ~20
  Appraised                                                            16
     core                                                               8
     supporting                                                         8
  Pass-1 candidates not re-read, carried as leads                      11
RETRIEVAL
  PDFs kept                                                             0
  PDFs the fetch tool stored outside the repository, unread             2
  HTML full text read through the fetch tool's summariser             10
     of which only the first 100,000 characters                         2   [IB13] [SBRB23]
  Abstract only                                                         6
  Pages refused, rate-limited or behind a login, NOT bypassed           6
APPRAISAL
  Matched in two of CrossRef, OpenAlex and PMC                         11
     core                                                         7 of 8
  CrossRef or OpenAlex only                                             2   [WSSMK05] [SAO22]
  arXiv only, no DOI checked                                            3   [HMP20] [SUCH17] [MM15]
  Retractions or corrections                                            0
  ADDED TO SYNTHESIS                                                    8
  Cited outside the matrix (supporting)                                 8
```

The ~20 new candidates were counted from the search tool's summaries and are approximate.
The two stored PDFs were requested before the reader knew the tool could not read them.

Round 10 left four metadata notes.

[KGV22] has two arXiv versions. The search summary says version 1 compares one centralised and
three decentralised controllers, and the version read compares one centralised and two
decentralised plus a sine baseline. The version read is the one cited.

[KGV22]'s DOI, `10.1162/isal_a_00533`, came from OpenAlex and was confirmed at CrossRef. The
paper as read names no venue.

[WSSMK05]'s title is "Automatic feature selection in neuroevolution" at CrossRef, and
"Automatic Feature Selection via Neuroevolution" on the lead co-author's publication page. CrossRef's
title is used.

Pass 1's Izquierdo & Beer 2010 is Izquierdo & Lockery 2010 ([IL10]), by CrossRef and PMC.

## 5. Synthesis matrix

| Key | Claim used | Method / scope | Stance vs design | Quality | Limitations |
|---|---|---|---|---|---|
| **[EA23]** | Premature morphological convergence is the dominant failure mode; no-protection baseline loses every pairwise comparison; MAP-Elites is the practical choice despite ranking 2nd; fitness-proportional selection improves all three axes; BC alignment matters | 5 algorithms × 20 runs × 1000 gens, pop 100; soft voxel robots 5×5×5, CPPN, Voxcraft GPU; straight-line locomotion, T=5s; Condorcet + 11 indicators | **Corrective** — added §2 wholesale, changed §8.2 and §8.3 | 📄 Peer-reviewed, *Adaptive Behavior* 🔓 CC BY-NC | **Soft-body substrate, not rigid articulated.** Algorithm ranking not demonstrated on this design's substrate; mechanism is substrate-independent |
| **[U07]** | Per-part reaction-force drag and real hydrodynamics disagree on direction of travel; the GA selected the artifact gait | MPS particle hydrodynamics vs fixed-frame reaction model; single morphology (*Anomalocaris*); Re ≈ 0.25×10⁵ | **Corrective** — added §5.3, motivated validation harness | 📄 Peer-reviewed, ECAL/LNCS | **2D only**; one morphology; kinematics prescribed, not evolved in-loop; strong closing claim ("physics determines body structure") over-reaches its evidence |
| **[K12]** | Novelty search loses at unconstrained swimming, wins at constrained deceptive tasks; concrete anti-exploit checklist; effector scaling + smoothing; three reflection flags; oscillatory transfer aids swimming | Rigid-body ODE creatures, Sims-style graph; NEAT-based; pop 300 × 150 gens; 25–30 runs; t-tests | **Confirmatory + enriching** — §4.1, §4.4, §11.2 | 📄 Peer-reviewed Springer chapter | Behaviour descriptor (final position) is a poor choice, which limits how far the negative swimming result generalises to well-designed QD |
| **[L21]** ⚠ partial | Encoding advantage splits by phenotype: CPPN wins soft-body, direct/recursive win or tie rigid-body; Sims is an *indirect* encoding; PhysX and Unity have precedent here | Survey; Table 6 synthesises 6 published encoding comparisons 2001–2020 | **Decisive** — closed §12.1 | 📄 Peer-reviewed, *Computer Graphics Forum* 🔓 | Secondary source — the underlying comparisons were not read individually; [L21] itself flags one (2017) as confounded |
| **[C18]** ⚠ partial | Simplified fluid collapses morphological diversity, not just accuracy; land→water detrimental, water→land beneficial; non-harmonic actuation matters; self-collision vibration exploit | Voxelyze soft robots + mesh-based quadratic drag; 5 stiffness levels; multi-objective; land/water/transition experiments | **Corrective** — overturned §5.4's own framing; added evidence to §5.1 | 📄 Peer-reviewed, *Soft Robotics* | Soft-body; authors describe their own EA as "rather weak"; transition benefit **p > 0.05**; single stiffness value used for transitions |
| **[Y94]** ⚠ partial *(round 3)* | Per-neuron **and** per-synapse energy charge, linear against world maxima, plus per-behaviour energy prices and a fixed drain — the direct precedent for §5A.2's neural term | PolyWorld: 2D-ish simulated ecology, Hebbian networks, endogenous selection, emergent predation/mimicry | **Corrective (provenance)** — §5A.2 claimed the per-neuron charge had no precedent in the corpus; it now has one, on p.7 | 📄 Proceedings chapter (ALife III, 1994), pre-DOI | Old, descriptive rather than experimental; no ablation of the neural charge; read at targeted passages only |
| **[MC25]** ⚠ partial *(round 3)* | Co-optimisation *undervalues* newly-mutated bodies (observed fitness drop exceeds true fitness difference), so promising morphologies are eliminated; AFPO, MAP-Elites and MIP all found near-optimal morphologies in ≤22% of trials against ground truth; early rankings barely predict final ones; upside — goal-switching can beat fixed-morphology training | Exhaustive landscape: controllers trained for all 1,305,840 3×3 voxel soft robots; 3 algorithms × 100 trials × 10k generations against ground truth | **Corrective (degree)** — Q1's mitigations are weaker than round 1 concluded; strengthens the case that ecological-niche protection must be *measured*, not assumed | 📄 arXiv v2 = accepted manuscript, *Artificial Life* | Soft-body voxels, tiny 3×3 grid, fixed controller architecture; transfer to rigid articulated bodies unestablished — the same substrate caveat as [EA23] |
| **[CO02]** ⚠ partial *(round 3)* | With **depletable** resources, negative frequency-dependent selection sustains up to 9 coexisting strategies for tens of thousands of generations *with mutation off*; the same populations under non-limiting resources collapse to a single genotype | Avida; 9 computable resources; evolution phase + ecology phase (mutation off); infinite-resource control and restart control | **Confirmatory + generalising** — D023's finite sun is one instance of the general rule: every earning channel needs depletion or it collapses diversity | 📄 Proceedings (ALife VIII, 2002), no DOI | Digital organisms computing logic functions, not embodied creatures; niches are hand-provided resource types, not emergent |
| **[VG05]** ⚠ partial *(round 3)* | Locomotion pays under endogenous selection when it is the **only route to reproduction**: food bits are the sole energy inflow and mating requires physical approach; rewarding stillness bifurcated the population into sitters plus a small mobile breeding caste | Gene Pool / swimbots: 2D spring-mass swimmers, energy economy, in-world mating, no fitness function | **Diagnostic** — explains why this design's swimmers never came: depth (the prize here) is reachable without moving; Gene Pool's prizes were not | 📄 Springer chapter (2005), author self-archive | 2D, springs not rigid bodies, sexual selection entangled with the energy result; descriptive, single system |
| **[CB18]** *(round 3, verified out of §13.4)* | Morphological innovation protection: temporarily shield lineages whose morphology just changed, giving controllers generations to re-adapt; sustains morphological search longer than unprotected baselines | Soft voxel robots, CPPN encodings; protection via nested age layers | **Confirmatory** — the mechanism §2.3 and §8.4 already lean on, now independently verified rather than bibliography-inherited | 📄 Peer-reviewed, *J. R. Soc. Interface*, OA | Soft-body; and [MC25] later shows MIP alone still fails to select for morphological potential — protection helps, it does not solve |
| **[PU16]** *(round 3, verified out of §13.4)* | A behaviour characterisation unaligned with quality can actively harm search (hardest maze: 2 solutions in 20 runs, worse than plain fitness); the fix is multi-BC, aligned + unaligned | Maze navigation family of graded deception; QD-score proposed here | **Confirmatory, with nuance** — supports §8.3 as written; the paper's own claim is "not just any behavioural diversity succeeds", not "unaligned always fails" | 📄 Peer-reviewed, *Frontiers Robotics & AI*, OA | Maze domain, not embodied creatures; the specific failure threshold is domain-dependent |
| **[KR13]** *(round 4)* | Consumer equilibrium sits at refuge floor *plus* break-even accessible density (R\* = R_c + m/(λ(e−hm))), stable only above a large critical refuge; a fixed-number refuge ≡ extreme type III response; fixed-number stabilises far more than proportional (Maynard Smith 1974, cited); Gause 1936's yeast **sedimented into a depth refuge** empirically | Gause model (exponential prey growth) chosen to isolate refuge effects from other stabilisers; analytic + numeric | **Explains D055's falsification** — a refuge covering the feeding ground deletes the consumer, exactly as observed in logbook/0043 | 📄 Peer-reviewed, *J. Theor. Biol.*, author OA copy | Behavioural refuge framing; verified by reviewer at the R\* and type-III passages [p.2] |
| **[MO04]** *(round 4)* | Detritus with density-independent accrual is quintessentially donor-controlled; the two-species detritus chain with mass-action consumer is **globally asymptotically stable** — stability supplied by constant input R, not resource self-limitation; detritus chains feasible at lower input and more resilient than producer chains | Review + Jacobian analysis of detritus food-chain models; 17 authors | **Diagnostic against the design** — this world's pool matches the model's structure yet busts, so either the closed recycling loop breaks donor control or the cycles are not dynamical | 📄 Peer-reviewed, *Ecology Letters*, lab self-archive | Verified by reviewer at the stability passage [p.7]; ODE world — no individuals, no cohorts |
| **[RC07]** *(round 4)* | Rosenzweig 1971's mechanism verbatim (enrichment → growing limit cycles → extinction); the empirical record is **mostly negative** — most enrichment experiments failed to destabilise; a refuge/invulnerable class makes the interaction donor-controlled and strongly stabilising (Pimm 1982); observed *Daphnia* cycles were once misdiagnosed as enrichment cycles and were developmental-delay cohort cycles | Review, *J. Biosciences* | **Cautionary both ways** — enrichment instability is real in theory, rare in nature; and mislabeling cohort cycles as enrichment cycles is a documented field error this project could repeat | 📄 Peer-reviewed, publisher OA | Secondary source for Rosenzweig 1971 (closed); survey-level |
| **[JKT04]** *(round 4)* | A true type I response has a **satiation threshold by definition**; 814 responses reviewed, type I exclusive to filter feeders; filter feeders show type III when they relax filtration at low density | Systematic review, 235 studies | **Corrective in principle** — this design's unbounded linear clearance is not a type I response but a physical impossibility; a satiation plateau is not a stabiliser bolt-on, it is what a filter feeder *is* | 📄 Peer-reviewed, *Biol. Reviews*, lab self-archive | Empirical catalogue, not a stability analysis |
| **[DBWM05]** *(round 4)* | Slight relaxation of consumption at low resource density (q=0.1–0.25 of the way to type III) stabilised chaotic 3-species chains and eliminated extinctions in 10-species webs; refuge-seeking, interference and switching are three implementations of the same relaxation | Food-web dynamical models (Williams & Martinez family), SFI working-paper OA version | **Enabling** — the cheapest stabiliser candidate: a toe on the clearance curve, dose-tunable | 📄 Book chapter (OUP), SFI WP OA | Web-level models; the q result is from the closed EPJ B paper this chapter summarises |
| **[HUF58]** *(round 4)* | Simple universes: predator always overexploits and goes locally extinct; a 120-position universe with dispersal barriers and prey-only long-range dispersal held **three full oscillations**; refuges must be "reasonably accessible, but not too readily so"; structure sets the oscillation period more than predation intensity does | Physical microcosm, mites on oranges, 3 trays × 40 positions | **Founding empirical anchor for Q9(b)** — and for the owner's whole-layer hypothesis: the failing universes are the well-mixed limit | 📄 *Hilgardia* (OA), fetched via Wayback | Predator still died on the third crash — structure extended, did not guarantee, persistence; author flags his own barriers as double-edged [p.40] |
| **[JN97]** *(round 4)* | Same species, same lab: one continuous 90-plant system dead in 120 days; eight 10-plant islands with deliberately-throttled bridges persisted 393 and 447+ days **with fewer plants**, no island individually persistent; prey dispersal stabilises, predator dispersal destabilises (Sabelis et al., cited) | Lab metapopulation, two long replicates + perturbation | **The controlled contrast the round-8 design should replicate** — persistence from asynchrony + limited migration, not from more resource | 📄 Peer-reviewed, *Exp. Appl. Acarol.*, UvA-DARE OA | Verified by reviewer at the persistence-times passage [p.7]; two replicates diverged sharply from matched starts — per-seed stochasticity warning |
| **[RMF07]** *(round 4)* | Coexistence requires *local* interactions; a critical mobility M_c exists (≈4.5×10⁻⁴ lattice fraction/tick in their system) above which biodiversity dies; M_c is **not** universal but the critical pattern *wavelength* is — extinction comes when λ ∝ √M outgrows the domain | Lattice rock-paper-scissors, Nature 2007; arXiv author copy | **Supplies the design criterion** — "how many boom-bust wavelengths fit across the world," computable from measured crash times and drift speeds, replaces any guessed dispersal rate | 📄 Peer-reviewed, *Nature*, green OA | Verified by reviewer at the M_c passage; cyclic 3-species game, not consumer-resource — mechanism transfers, constants do not |
| **[FM15]** *(round 4)* | In a water column with a prey growth gradient and a fast grazer, **type I feeding at effectively infinite carrying capacity can be stable**; above a critical diffusion the system homogenises to Rosenzweig–MacArthur and is globally unstable; stability depends on habitat size and gradient steepness | PDE plankton model, *Bull. Math. Biol.*, arXiv copy | **The nearest theory to this exact world** — gradient + mobility as stabiliser, with the well-mixed limit explicitly the unstable case | 📄 Peer-reviewed, green OA | Vertical gradient in their geometry (light/growth over depth); transferring it horizontally is this project's inference, marked as such |
| **[HZ13]** *(round 4)* | Jansen's intermediate-dispersal asynchrony mechanism named and dated; their own high-dispersal averaging mechanism **requires patches to differ in carrying capacity** — even enrichment across identical patches destabilises regardless of dispersal | Two-patch R-M metacommunity, PLoS ONE | **The sharpest constraint on the round-8 design** — subdivision into identical tiles is the null result; patch inequality does the work | 📄 Peer-reviewed, gold OA CC BY | Two-patch numerics; patch-count claim is an extrapolation the paper itself flags |
| **[MCP05]** *(round 5)* | Percentage extracellular release averaged 22 ± 2% in the Celtic Sea and was **flat at ~20% across a productivity range from <100 to >15,000 mg C m⁻² d⁻¹** (log–log slope 0.96, not different from 1; n = 35, r² = 0.90); DOCp is roughly irradiance-**in**dependent while POCp is strongly light-dependent, so PER peaks (>40–50%) at the *base* of the euphotic layer; release is by passive diffusion from intact cells, not overflow and not grazing | ¹⁴C dissolved/particulate primary production, 10 integrated profiles, Celtic Sea summer stratification, plus P–E and 24 h light–dark kinetics; pooled with the authors' eutrophic Ría de Vigo dataset | **Sets the D070 number, and corrects its shape** — a flat fraction of intake is defensible in magnitude but wrong in its light response | 📄 Peer-reviewed, *Mar. Ecol. Prog. Ser.*, author self-archive | Two ecosystems, one lab, one method; the authors themselves ask for ultraoligotrophic replication. Reviewer-verified [p.1, p.7, p.9] |
| **[LS13]** *(round 5)* | Exudation is **not allometric**: PER uncorrelated with cell size, cell-specific exudation isometric with cell volume (mean slope 0.95) across >7 orders of magnitude; **no significant difference between growth stages**; culture PER averaged only **~2%** of total carbon fixation against 10 to >35% in natural assemblages | 22 species, 5 phyla, 3 growth stages, ¹⁴C exudation in culture | **Simplifying** — the world rule needs no size term and no growth-phase gate; and it warns that a culture-derived number would be ten-fold too low | 📄 Peer-reviewed, *Mar. Ecol. Prog. Ser.*, author self-archive | Cultures, not assemblages — which is exactly why its own 2% must not be used as the world's fraction. Read in full |
| **[LS11]** *(round 5)* | PER averaged **~37%** along a Mediterranean longitudinal transect with no clear longitudinal pattern; **bacterial carbon demand exceeded dissolved primary production 5- to 14-fold**, so exudation alone cannot feed the microbial loop; reproduces [BP91]'s 13% and its glass-fibre-filter caveat; on their own pooled dataset "no overall inverse relationship between PER and total primary production exists" | ¹⁴C DOCp/POCp, BOUM cruise, three Mediterranean regions; BCD from two published growth-efficiency models | **Caveat-supplying** — the strongest evidence that a producers-only exudation channel will still underfeed a second level, because it does in the ocean too | 📄 Peer-reviewed, *Biogeosciences*, gold OA CC BY | Summer stratification only; BCD is modelled, not measured. Reviewer-verified [p.1, p.2, p.7, p.8] |
| **[CH20]** *(round 5)* | PER averaged **40.8 ± 12.2%** (integrated range 28.6–60.1%, volumetric 24.9–62.0%), >50% at upwelling stations; carries Nagata 2000's **10–20% world-ocean general range** and [BP91]'s 13%; total primary production could not sustain bacterial carbon demand except at upwelling stations; bacterial growth efficiency 5.7 ± 1.4% | Paired dissolved + particulate primary production, bacterial production and *directly measured* respiration, southern East China Sea | **Corroborating at the high end** — an independent lab, method and ocean basin reaching the same 30–40% oligotrophic figure as [LS11] | 📄 Peer-reviewed, *Front. Mar. Sci.*, gold OA | Brief research report, one cruise; BR measured at a subset of depths and extrapolated. Reviewer-verified [p.1, p.5] |
| **[PC95]** *(round 5)* | The canonical 10% transfer efficiency: 140 estimates from 48 documented trophic models, whose mean the conventional 10% is "extremely close to", **with no trend of TE with trophic level** — but the estimates are for **TL2 → TL4**, i.e. herbivores/detritivores upward | Global fisheries catch (1988–91) apportioned into 39 groups at fractional trophic levels; Monte Carlo error propagation | **Corrective on the comparison, not on the diagnosis** — D070's "real transfer efficiencies run near 10%" is right about the number and wrong about which step it measures | 📄 Peer-reviewed, *Nature*; Sea Around Us self-archive | Fisheries-oriented; TE distribution is inherited from other modellers' Ecopath models, not measured here. Reviewer-verified [p.1, p.3] |
| **[ED21]** *(round 5)* | Producer→herbivore transfer efficiency averages **13% (range 11–17%)** and herbivore→fish **10% (7–12%)** in temperate northern-hemisphere systems; ecosystem-scale estimates span **<1%–52%**; exudation and viral lysis are explicitly non-predatory losses that divert production to detritivorous bacteria; lab feeding experiments beat wild ones partly because they *prevent* loss to the microbial loop | Review of production-based, model-based and catch-based transfer-efficiency estimates across ocean biomes | **Supplies the missing reference point** — the producer→level-2 step this world is failing at, measured, which [PC95] does not give | 📄 Peer-reviewed, *Trends Ecol. Evol.*; UC eScholarship accepted manuscript | **Locators are AM PDF pages, not journal pages.** Review, so most figures are inherited from cited work. Reviewer-verified [p.7, p.10, p.11, p.14, p.17] |
| **[PWAH07]** *(round 5)* | "Most of the organic matter available to consumers in the ocean is used and respired by bacteria"; "the larger part of all energy captured by marine photosynthesis ... is consumed ultimately by microorganisms"; in blue water, where small cells dominate, **only 1–2% of primary production may finally be assimilated by fishes**; microphages (salps, krill) short-circuit the loop and are its most efficient path upward | Narrative review by four of the field's founders, incl. Azam of the 1983 paper that named the microbial loop | **Confirms the path is real and cautions about its length** — a long microbial chain delivers ~1% to top consumers, which is what this world currently gets at level *two* | 📄 Peer-reviewed, *Oceanography* (TOS), gold OA | Deliberately non-quantitative; its numbers are all inherited citations (Ducklow et al. 1986; del Giorgio & Williams 2005). Read in full |
| **[BP91]** ⚠ **NOT OBTAINED** *(round 5)* | Cross-system mean PER of **13%**, measured PER ranging <1–75% with individual-system means 3–40%, from 16 lacustrine, marine and estuarine studies; PER approximately constant across productivity | Literature synthesis, 16 studies | **The field's default number** — and the one this round could not read | 🔒 Wiley/ASLO, 403 to non-browser clients; the Semantic Scholar GREEN record resolves to link-only Figshare metadata | **Cited only through [MCP05 p.9], [LS11 p.7] and [CH20 p.5]**, all three of which independently report the 13% *and* independently flag that its glass-fibre-filter methods underestimate release. Treat 13% as a probable floor, not a central estimate |
| **[P77]** *(round 6)* | At low Reynolds number "this bug can't do anything by stirring its local surroundings"; swimming does not raise intake, it relocates the cell "to find greener pastures", and to outrun diffusion it must go a distance of order D/v | Lecture essay, order-of-magnitude physics | **Diagnostic** — the reason blind movement pays at all is relocation out of a depleted neighbourhood, which this world's feeding model cannot express | 📄 *Am. J. Phys.* 1977, self-archived | Bacterial scale; qualitative |
| **[BP77]** *(round 6)* | "The adsorption rate for molecules of moderate size cannot be significantly enhanced by motion of the cell or by stirring of the medium by the cell"; local stirring at best doubles the diffusive current | Analytic physics of diffusion to receptors | **Diagnostic** — sets the scale below which motion cannot refresh a halo | 📄 *Biophys. J.* 1977, PMC | Micron scale; this world's bodies are a thousand times larger |
| **[D97]** *(round 6)* | No locomotion pays below about 0.6 µm diameter; smallest of 97 motile genera averages 0.8 µm, 18 of 94 non-motile genera are smaller; motility also raises the dispersal coefficient | Physical model against a survey of bacterial genera | **Confirmatory** — a size threshold exists and this world is far above it | 📄 *PNAS* 1997, PMC | Free-floating microbes only |
| **[KB96]** *(round 6)* | Flux to an absorber rises with size and with relative motion in every flow regime; a radius near 20 µm before swimming or sinking raises flux by about 50% over stagnant water; solutions do not transfer between regimes | Review with new asymptotic and numerical solutions | **Enabling** — a body-scale halo refreshed by relative flow is the physics a proposal can lean on | 📄 *Oceanogr. Mar. Biol.* 1996, author self-archive | Osmotrophs; scanned copy |
| **[SL23]** *(round 6)* | Diffusive encounters rule the sub-micrometre world; ballistic (swimming) encounters dominate from the micrometre scale up; formulas for each | Review, encounter kernels | **Enabling** — for a particle feeder, intake proportional to swept volume is the standard model | 📄 *Interface Focus* 2023, PMC | Microbial scale |
| **[W11]** *(round 6)* | "Chemotaxis evolved in bacteria that were already capable of self-propulsion in random directions"; motility is favoured only where the habitat is physically structured; motile strains outcompete non-motile in soft agar | Theory plus competition experiments | **Decisive for Q11(a)** — the order was movement first, and only in a structured habitat | 📄 *PNAS* 2011, PMC | 2D model, agar habitat |
| **[TS12]** *(round 6)* | Chemotaxis is favoured only at intermediate turbulence; below and above, undirected motion does as well | Simulation of chemotactic foragers in turbulent filaments | **Cautionary** — a sense is worth its cost only in a window of environmental structure | 📄 *Science* 2012, author self-archive | Microbial scale |
| **[MK72]** *(round 6)* | "The apparent detection of a spatial gradient by the bacteria therefore involves an actual detection of a temporal gradient experienced as a result of movement through space" | Temporal-gradient apparatus, stopped flow | **Decisive for Q11(b)** — the first sense compares a scalar with its own recent past | 📄 *PNAS* 1972, PMC | One species |
| **[SBB86]** *(round 6)* | Temporal comparison reaches 4 s into the past, the last second weighted positive and the previous three negative | Tethered-cell impulse responses | **Quantifies [MK72]** — a differentiator with a short memory | 📄 *PNAS* 1986, PMC | *E. coli* only |
| **[SJ10]** *(round 6)* | "Motility, combined with the sensory perception of the internal energetic conditions, is prerequisite for tactic responses"; energy taxis and chemotaxis are not cleanly separable | Review | **Confirmatory** — an internal energy signal is a sense that steers, and this world already has one (`Energy`) | 📄 *Arch. Microbiol.* 2010, OA | Review; the ancestral-order claim is the review's own caution |
| **[J09]** *(round 6)* | The simplest eyespot is two cells; larvae directly regulate ciliary beating by the eyespots, no integration between | Review of phototaxis across protists and larvae | **Confirmatory for Q11(c)** — the minimal sensorimotor link is direct | 📄 *Phil. Trans. B* 2009, PMC | Phototaxis only |
| **[J11]** *(round 6)* | "The first nervous systems consisted of combined sensory-motor neurons, directly translating sensory input into motor output" | Review with the *Platynereis* larva as the case | **Confirmatory** — one cell did both jobs | 📄 *Proc. R. Soc. B* 2011, PMC | Ciliary locomotion |
| **[JKG15]** *(round 6)* | Parker: independent effectors first, nervous systems then connect them to sensors; Pantin: nervous systems coordinate the new effector, muscle; "significant IO and IC function without a nervous system" | Conceptual review | **Frames Q11(c)** — the two accounts and the option space between them | 📄 *Phil. Trans. B* 2015, PMC | Conceptual |
| **[KDL13]** *(round 6)* | The problem early nervous systems solved "was not so much to act intelligently, a problem that had already been solved in various ways without a nervous system, but to act as a single multicellular unit" | Theoretical argument (the skin brain thesis) | **Decisive for Q11(c)** — the chicken-and-egg was solved at the single-cell level | 📄 *Adaptive Behavior* 2013, author self-archive | Theoretical |
| **[JG21]** *(round 6)* | "Much early sensing was reafferent; it was responsive to the consequences of the animal's own actions" | Conceptual review | **Enriching** — the first sense may have read the body's own motion | 📄 *Phil. Trans. B* 2021, PMC | Conceptual |
| **[MZ09]** *(round 6)* | Neurons and complex brains arose independently more than once | Review | **Context** — there is no single route from a sensorimotor cell to a brain | 📄 *Brain Behav. Evol.* 2009, PMC | Review |
| **[HG25]** *(round 6)* | Under energy constraints, more seasonal environments evolved smaller networks; the Expensive Brain Hypothesis over the Cognitive Buffer Hypothesis | Evolved RL agents with priced neurons | **Confirmatory for D082** — pricing neurons selects them away where they do not pay | 📄 ALIFE 2025, arXiv | Not an ecology; task-driven |
| **[FDT11]** *(round 7)* | Storage is optimal only at intermediate environmental energy with stores not yet full [Results, Fig. 1]; at low variability and low predictability (λ = 1, τ = 5) the optimum stores nothing [Results, Fig. 3]; the stored fraction rises with either, as "allocation to storage becomes optimal for an increasing range of environmental states" [Results, Figs 3–4]; storage, reproduction and maintenance can be optimal at once [Discussion]; costlier survival needs more variability and predictability before storage pays [Results, Fig. 6D]; near starvation everything goes to reproduction [Results, Fig. 2] | Stochastic dynamic programming maximising expected lifetime reproductive success; environmental energy a stationary multiplicative AR(1) process with log-normal marginal, coefficient of variation λ from 1 to 50 and autocorrelation time τ from 0 to 125 steps; state is age, environmental energy and stores | **Backs clause R of round 57, by inference.** The weather's coefficient of variation (about 0.31) is below the smallest λ plotted and its e-folding time (about 1.4 days) is near the low end of τ, so the season and not the weather should raise the reserve | 📄 Peer-reviewed, *Evolution*; author manuscript at PMC, read in full | The optimum is a reaction norm on the *sensed* current environment, and round 57's bodies sense no light; storage is free; there is no minimum breeding investment, where round 57 has a gate; fitness is expected lifetime reproduction, so no bet-hedging term; growth is determinate [Discussion] |
| **[VJTF09]** *(round 7)* | Abstract only. In a seasonal high-latitude model of herbivorous zooplankton, capital breeding (from stores) mattered most when the feeding season was early, making 7–9% of eggs but 9–30% of reproductive value; "the main benefit of capital breeding was reproduction prior to the feeding season"; stores also topped up egg production during income breeding; late-born individuals were predicted to take a two-year cycle | State-dependent life-history model optimising energy allocation and diapause by reproductive value, under early, intermediate and late feeding seasons | **Backs clauses T and R of round 57, by inference.** If a seedling's value depends on its day of birth, reserve should be carried through winter and births should bunch at the turn to summer | 📄 Peer-reviewed, *Oikos*; closed, abstract read from the OpenAlex index | Conditions and limitations not read. Clause T rests on this abstract alone; zooplankton with a diapause stage that round 57's bodies lack |
| **[PW21]** *(round 7)* | Within-population variance of maturation rate and fecundity ended much higher under slow (14-day) than fast (3-day) disturbance cycles, in simulation and experiment [Results, Fig. 4]; deterministic and random-timing versions of one period differed little, so "periodicity drove differences in intrapopulation variance much more strongly than the deterministic‐stochastic environment distinction" [Results]; trait means showed no consistent difference [Results]; variance fell over time in every regime, much faster under fast cycles [Results, Fig. 5]; proposed mechanism: under slow cycles even suboptimal genotypes keep positive growth [Discussion] | Agent-based model of the copepod *Tigriopus californicus*, 100 runs in each of four regimes for 150 steps; five-month laboratory selection experiment, eight replicate populations in each of three treatments | **Backs clause V of round 57, by inference.** Variance in breeding traits should fall over the run, and the season's period rather than the weather should set it | 📄 Peer-reviewed, *Ecology Letters*, CC BY 4.0 at PMC, read in full (supplements S1–S7 not read) | Both periods are shorter than the 20–30 day generation and runs last 5–6 generations, so results are transients; "stochastic" means random timing, not random amplitude or autocorrelation; round 57's year of about six generations is outside the tested ratio, which the authors name as untested; no stochastic fast treatment in the experiment |
| **[GSS14]** *(round 7)* | Final dormancy rose with the frequency of unpredictable lethal bad years (ANCOVA p = 0.0051; Kendall's τ = 0.23), means 0.16, 0.22, 0.29, 0.33 and 0.30 for 0–50% bad years [Results, Table 2]; observed change regressed on the change Cohen's 1966 model predicts, upward and downward, with 11 of 12 lineages positive [Fig. 1 inset]; AIC preferred the geometric-mean model over the arithmetic-mean model, relative likelihood 2.4; loss of dormancy evolved more readily than gain | *Neurospora crassa* ascospore dormancy under serial transfer, five bad-year frequencies, 12 genetic lineages, 88 independently evolved samples, 10–14 rounds of selection | **Supports a bet-hedging response to the weather, by inference.** An unspent reserve at a breeding chance plays the dormant spore; only the weather, not the periodic season, supplies unpredictability | 📄 Peer-reviewed, *Proc. R. Soc. B*, PMC, main text read in full (supplement not read) | Two consecutive bad years were forbidden and the 50% line alternated, so the sequences were not independent draws; no density dependence; fractions may not be at equilibrium; stochastic extinction of lines was high |
| **[TGC17]** *(round 7)* | Abstract only. Rotifer populations under an unpredictable regime diverged from predictable ones in under 77 days, with "an earlier initiation of sexual reproduction and a lower hatching fraction of diapausing eggs", read by the authors as bet-hedging | *Brachionus plicatilis*, predictable against unpredictable hydroperiod; the design, read in the same group's open 2019 paper, had six populations from 270 field clones, three per regime, seven growing cycles and one random sequence shared by all three unpredictable replicates | **Supports earlier insurance investment under the weather, by inference** | 📄 Peer-reviewed, *Oikos*; abstract only, open repository copy bot-gated | One particular unpredictable sequence was tested (the 2019 paper's caveat), and round 57 has one weather sequence per seed; the unpredictability is in season length, and the rotifers have a dormant stage |
| **[MK07]** *(round 7)* | "Increased temporal autocorrelation in the environment always favoured increased allocation towards survival", more steeply under slower maturation [Results, Fig. 2c; Discussion]; more variance lowered or raised survival allocation depending on how efficiently resources become fecundity [Results, Fig. 3a]; with strong negative autocorrelation (α < −0.5) there is no single evolutionarily stable strategy and some simulations branched into a dimorphism [Results, Figs 4–5] | Two-stage matrix model with a heritable allocation between survival and fecundity; environment constant, independent normal, or AR(1) with coefficient from −0.99 to 0.99; density dependence on establishment; invasion analysis on the stochastic growth rate | **Counter-weight on the weather's autocorrelation.** Read with a larger reserve as allocation to survival, it predicts more reserve under round 57's positively correlated weather, opposite to [WTV18] (inference) | 📄 Peer-reviewed, *Proc. R. Soc. B*, PMC, read in full (figures by caption only) | Semelparous; survival brings no growth benefit; outcomes depend strongly on the conversion efficiency; the AR(1) environment has no seasonal period, so a half-year lag is outside it (inference); the authors note that several generations a year see negative autocorrelation from the seasons [Discussion] |
| **[WTV18]** *(round 7)* | Abstract only. At equal total stress, stress tolerance was favoured in whiter (less autocorrelated) environments and disfavoured in redder ones, in RNA virus experiments and in models alike, through a trade-off between tolerance and reproduction; the effect's strength depends on that trade-off's shape and on mutant tolerance | Experimental evolution of RNA virus lineages under stress series differing in autocorrelation, plus theory | **Counter-prediction to [MK07].** Read with a larger reserve as tolerance, it predicts less reserve under positively correlated weather (inference) | 📄 Peer-reviewed, *Am. Nat.*; closed, abstract only | The virus, the stressor, the autocorrelation levels and the trade-off shapes were not read |
| **[SK12]** *(round 7)* | Abstract only. A genotype's fitness variance splits into the variance of its individuals' fitness and the correlation of fitness among them; conservative and diversified bet-hedging are two ends of one continuum, and within- and between-generation bet-hedging two ends of another, so "this division can be considered a false dichotomy" | Review with a general model and a simple worked example | **Frames the shared sky.** One weather series for every body puts the correlation term near its maximum (inference) | 📄 Peer-reviewed, *Biol. Rev.*; abstract only, Wiley open link returned 403 | Nothing beyond the abstract was read |
| **[WYRW25]** *(round 7)* | Journal abstract: stochasticity can reverse the sign of selection that geometric-mean fitness predicts, so a bet hedger can be deleterious at small population size and beneficial at large; stochastic models are needed to explain published bet-hedging data. Preprint: phenotypic stochasticity makes diversified bet-hedging less favourable, mostly at small N, and environmental stochasticity makes bet-hedging more favourable, mostly at large N [v1 Results]; sign-inversion crossings fell at N of 2–30 with a geometric-mean advantage and 30 to 10⁶ with a disadvantage [v2 Results]; "GMF alone does not capture the full extent to which bet hedging is adaptive" [v2 Discussion] | Wright–Fisher simulations and Markov-chain numerics of one bet-hedging mutant among N − 1 specialists, in an independent good-or-bad environment each generation, non-overlapping generations | **Qualifies the bet-hedging readings.** Round 57's populations are finite and each seed has one weather sequence, so results should be read per seed, not only as a mean over seeds (inference) | 📄 Peer-reviewed, *Am. Nat.* (journal abstract only); bioRxiv preprint versions 1 and 2 read in full | No autocorrelation, constant N, asexual, non-overlapping generations; claims are the preprint's and may differ in the journal |
| **[BWWR15]** *(round 7)* | Plasticity evolved only when individuals could anticipate the environment [Results, Fig. 1]; response modes occupy largely separate regions of predictability and timescale: when unpredictable, conservative bet-hedging at very short cycles, diversified bet-hedging at intermediate ones (for example log R = 0.5, with R in generations per cycle) and adaptive tracking at long ones [Results, Fig. 2]; crossing a boundary between modes caused rapid collapse and often extinction [Results, Fig. 3] | Individual-based simulations, 5,000 individuals, generations of five time steps, 50,000 generations, 100 replicates per cell; sinusoidal plus noise environment; seven evolving loci | **Places round 57 between diversified bet-hedging and tracking, by inference.** With R about 6 (log R about 0.78) and no light cue, it sits in the unpredictable row; a body's own reserve could still serve as a cue to the season | 📄 Peer-reviewed, *PNAS*, PMC, main text read in full (SI not read) | 50,000 generations against round 57's roughly 50; boundary positions depend on plasticity costs, fecundity and genetic architecture; the Fig. 2B boundary near round 57's R was not read numerically |
| **[PER19]** *(round 8)* | Across 96 mammals the rate of actuarial senescence did not slow with body size (log–log slope 0.006 ± 0.03); onset came later (0.19 ± 0.08) and prime-age mortality fell (−0.12 ± 0.03) [p.3]. Senescence parameters explained 35–49% of lifespan variance across mass terciles, prime-age mortality 47–62% [p.6] | Zoo records, five-parameter mortality model, variance decomposition against simulated nulls | **Corrective on D128's scaling** — the gene stretches the rate with the base, the axis this paper finds carries least lifespan variation (inference) | 📄 Peer-reviewed, *PLoS Biol.* 🔓; PDF pages | Captive data; between species, not within a population |
| **[COL21]** *(round 8)* | "Variation in the rate of ageing within genera is orders of magnitude smaller than variation in pre-adult and age-independent mortality" [p.1]; changing the ageing parameters moves a population off its genus's line, while early and age-independent mortality move it along [p.3] | 39 datasets, 7 primate genera, Bayesian Siler models | **Corrective, within lineages** — a heritable base acting through the slope is the pattern primates show least (inference) | 📄 Peer-reviewed, *Nat. Commun.* 🔓; PDF pages | Primates only; [DAS22] contests invariance in turtles |
| **[JON14]** *(round 8)* | Mortality trajectories across 46 species rise, stay constant, fall, hump or bow; terminal over mean adult mortality ran from above 20 (Japanese women) to below 0.5 (white mangrove); pace and shape are "two orthogonal axes" [HTML] | Comparative demography of standardised trajectories | **Brackets f** — at f = 0.2 a pure Gompertz gives terminal over mean about 10.5, inside the range (pass 2's arithmetic) | 📄 Peer-reviewed, *Nature*; PMC page, no PDF pages | A fixed shape with heritable pace is a simplification its orthogonality warns about |
| **[KR91]** *(round 8)* | Abstract only. Senescence requires that "the acquisition of greater longevity usually involves some cost"; less is invested in the soma than indefinite survival needs | Theory | **Requires a > 0, sets no value** — D038's reason, confirmed | 📄 Peer-reviewed, *Phil. Trans. B*; closed | No functional form; nothing beyond the abstract read |
| **[MC19]** *(round 8)* | Energy trade-offs are "increasingly challenged as studies … have uncoupled the trade-off between reproduction and longevity" [p.1]; examples [p.4] | Review | **Contests the premise of a repair price** | 📄 Peer-reviewed, *Proc. R. Soc. B*; repository copy | Argues for function-based trade-offs, which the gene does not model |
| **[LEM15]** *(round 8)* | 21 of 26 studies on 24 wild vertebrate species support an early–late life trade-off; all 8 that corrected for individual heterogeneity do [HTML] | Vote count | **Supports a cost of reproduction in ageing, in direction** | 📄 Peer-reviewed, *Proc. R. Soc. B*; PMC page | No effect size; vertebrates |
| **[BBV20]** *(round 8)* | Lifelong brood enlargement raised the Gompertz slope b₁ from 0.16 (SE 0.05) to 0.25 (SE 0.06) a year, intercepts −2.37 and −2.40, remaining life 4.7 against 3.8 years [p.6, Table 2]; broods 1.86 against 5.25 nestlings [p.3] | 320 jackdaws, 544 broods, Bayesian Gompertz; no unmanipulated control [p.3] | **Supports the birth push's form, not its size** — a clock advance per birth shows as a slope change; this world's push is 3–8 times the jackdaw figure per birth (pass 2's arithmetic) | 📄 Peer-reviewed, *J. Anim. Ecol.* 🔓 CC BY; PDF pages | Wide standard errors; one species; shares a laboratory with [BOO14] |
| **[WIN25]** *(round 8)* | Experimental effect −0.05 log-odds of annual parental survival per added egg [p.3]; 5%, 14% and 22% less survival odds per extra offspring under three scalings [p.14]; the within-species landscape "is flat" up to the edges of the between-species continuum [p.1] | Meta-analysis of brood manipulations across birds | **Sizes the birth push low** | 📄 *eLife* reviewed preprint v4 🔓; PDF pages | Birds; annual survival, not ageing rate |
| **[SADK00]** *(round 8)* | High extrinsic mortality evolved higher intrinsic mortality and higher early fecundity, 40.8 against 27.0 eggs a day [HTML, Table 1] | Experimental evolution, *Drosophila*, 50–90 generations | **Direction for the gene under extrinsic death** — it moved evolved lifespan down | 📄 Peer-reviewed, *PNAS*; PMC page | One laboratory experiment; guppies are a counter-case (pass-1 lead) |
| **[ZBH95]** *(round 8)* | Abstract only. Selection moved lifespan both ways; long-lived females had fewer progeny throughout life | Direct selection on lifespan | **Supports a within-species cost, contested** by later no-cost results that are unread leads | 📄 Peer-reviewed, *Evolution*; closed | Nothing beyond the abstract read |
| **[LC85]** *(round 8)* | At uncontrolled larval density, late-reproduced lines rose from about 35 to over 70 days in 21 generations [p.4–5]; at controlled density "little overall response" [p.1] | Experimental evolution, *Drosophila* | **Supports a fast response, conditionally** — c = 0.08 with a 0.15 step is fast and defensible as a deliberate speed-up (pass 2) | 📄 Peer-reviewed, *Heredity*; PDF pages | Response depended on larval density |
| **[MR87]** *(round 8)* | Life-history narrow-sense heritability mean 0.262 (SE 0.012), median 0.250 [p.6]; *Drosophila* 0.12 ± 0.02, from Roff & Mousseau 1987 [p.8] | Meta-analysis of 1,120 estimates in wild, outbred animals | **Sets the heritability band** behind a response of 1–3% of the base a generation (pass 2's inference) | 📄 Peer-reviewed, *Heredity*; scanned PDF | *Drosophila* excluded from its own data set; old |
| **[CAR92]** *(round 8)* | Abstract only. Clipping 18% of forewing area "significantly reduced life expectancies"; wear predicts mortality | Field experiment, eight bumble-bee colonies | **Supports the wound push's direction** | 📄 Peer-reviewed, *J. Anim. Ecol.*; closed | No effect size seen |
| **[KK16]** *(round 8)* | Re-implementations of programmed-death models failed, or death evolved "only because it granted individuals the ability to move", or because it "shortened the generation time" [p.13] | Critical review with re-implementations | **A check on any lifespan result here** — a fall in the base must be tested as a movement or generation-time effect (inference) | 📄 Peer-reviewed, *Aging Cell* 🔓 CC BY; PDF pages | Review of models, not of field data |
| **[MD08]** *(round 9)* | Incremental schemes are staged evolution, environmental complexification, fitness shaping and behavioural decomposition [p.2]; complexification tunes the task's parameters [p.3]; a late switch over-specialises [p.3]; direct evolution found no working controller, "all individuals obtaining the minimum fitness" [p.7] | Simulated wheeled robot, eight lights, feed-forward networks, ε-MOEA, 10 runs | **Names the nursery's scheme**, kept apart from fitness shaping (inference) | 📄 Peer-reviewed, SAB 2008, LNAI 5040; PDF pages | One task; its own method is multi-objective, not a curriculum |
| **[MD09]** *(round 9)* | Bootstrap problem: "if all individuals from the first randomly generated population perform equally poorly", nothing evolves [p.1]; direct evolution found no working controller [p.5]; behavioural diversity and multi-subgoal evolution bootstrapped at generations 218 and 138 (p < 0.001), with equal final fitness [p.5] | Light-seeking robot, seven lights, NSGA-2, population 200, 30 runs of 1,000 generations | **Supports D134 (4)**: diversity, not a proxy, for a population that all scores zero (inference) | 📄 Peer-reviewed, IEEE CEC 2009; PDF pages | Its behaviour label is the light vector, which encodes the task |
| **[GM97]** *(round 9)* | Direct evolution finds "mechanical" strategies [p.1–2]; prey capture stalled after about 20 generations, the mean of ten runs [p.13–14]; a sequence of harder prey solved it [p.13], with capture in 85% of test trials [p.17]; the user must design the sequence [p.20] | Grid pursuit; recurrent networks by ESP, Delta coding between tasks | **The closest precedent for a world-side ramp under one score** (inference) | 📄 Peer-reviewed, *Adaptive Behavior*; scan | A converged population "cannot easily adapt" without Delta coding [p.10]; discrete grid |
| **[JAK97]** *(round 9)* | Aspects reality lacks must be "randomly varied from trial to trial" [p.8]; real aspects vary too, but not so much that nothing evolves [p.9]; reliable noise gets used [p.10]; transferred controllers succeeded 98 and 97 times in 100 [p.39] | Theory, plus two transfers to real robots | **Backs D134's draw of carrion density per episode** (inference) | 📄 Peer-reviewed, *Adaptive Behavior*; scan | Simulation to reality, not easy tank to hard |
| **[PJ10]** *(round 9)* | Light following from random genotypes was "overall not satisfactory"; walkers given vision mostly evolved it [p.5] | Sims-style creatures; 300 clones of one of 41 walkers, weights mutated; fitness the speed toward the light [p.4]; 115 runs [p.8] | **The nursery's pattern of fixed bodies and bred brains**, under a shaped score (inference) | 📄 Peer-reviewed, GECCO 2010; PDF pages | Approach speed is a proxy D133 rejects; the comparison is qualitative |
| **[CA16]** *(round 9)* | "A notoriously difficult task" [p.1]; under the plain score "the best organisms move very little", so locomotion was added [p.11]; noise rose 0.1%, 5%, 50%, then uniform, and too fast left runs wandering [p.15]; turning is harder than running [p.16]; sustained foraging is a second skill [p.23] | 3D rigid bodies, body and brain co-evolved; staged runs seeded by founders chosen by eye [p.6] | **The nearest substrate, and the strongest warning**: its curriculum needed a shaped score and a human eye (inference) | 📄 Peer-reviewed, *GPEM* 17(4); PDF pages | One lineage; brittle high-level neurons [p.25] |
| **[LOPA03]** *(round 9)* | Complex functions built on simpler ones "provided that these were also selectively favoured" [p.1]; EQU in 23 of 50 populations [p.3], 0 of 50 when only EQU paid, 124 of 360 when one or two rewards were dropped [p.5]; a pivotal mutation was deleterious when it arose [p.3] | Avida, nine rewarded logic functions | **Supports paying partial skills in the same currency**, as small carrion does; rewards fixed, not staged (inference) | 📄 Peer-reviewed, *Nature*; PDF pages | Logic, not bodies; by D134's terms its rewards are shaping |
| **[CLWO13]** *(round 9)* | Reverting or replacing deleterious mutations lowered fitness; EQU in 28 of 50 controls against 16 or fewer in each treatment [p.2]; 13 of 20 replays under the control protocol [p.3] | Avida, four mutation treatments, 50 populations each | **Cautions against strict acceptance**, such as D134's two paired SE (inference) | 📄 Peer-reviewed, *PNAS*; PDF pages | Asexual digital organisms |
| **[KNA07]** *(round 9)* | Modularly varying goals sped evolution, more for harder goals, up to a thousandfold [p.1, p.3]; random goals mostly slowed it [p.2, p.5]; the same variants as fixed objectives gave "virtually no speedup" [p.4] | Logic circuits, networks, RNA; GA and hill-climbing | **Supports varying the world over ramping it**, if variants share structure (inference) | 📄 Peer-reviewed, *PNAS*; PDF pages | Varies the goal, which D134 holds fixed |
| **[WLCS19]** *(round 9)* | Direct optimisation scored 17.9, 39.6 and 13.6 against 230 [p.10]; children admitted at 50 to 300, "neither too hard nor too easy" [p.7, p.10]; a direct-path curriculum failed at the hardest levels [p.14]; without transfer no extremely challenging environment arose [p.19] | 2D biped, evolution strategies, co-evolved terrain | **Supports a curriculum, cautions against one ramp** (inference) | 📄 arXiv preprint v3 | Five ES runs; three runs without transfer |
| **[LST11]** *(round 9)* | Fitness as distance to goal [p.15] solved a deceptive maze in 3 of 40 runs, random 4, novelty 39 [p.16–17]; unenclosed, novelty 5 of 100, fitness 2 [p.19] | NEAT, mazes and a biped | **A distance score deceives**, as D133 holds; a behaviour label weakens in open space (inference) | 📄 Peer-reviewed, *Evol. Comput.*; authors' layout | PDF pages, not journal pages |
| **[GEOP08]** *(round 9)* | Taxis evolved within 1,000 to 2,000 updates; evolved memory did worse than hand-coded (p = 4.9 × 10⁻⁷) [p.5]; random tumbles and fixed turns did equally well [p.6]; merit grew with each step closer [p.4] | Avida, 100 × 100 grid, 1,000 organisms, 50 replicates; distance as an idealised sense | **Fast taxis under a per-step distance reward**, the score D133 rejects (inference) | 📄 Peer-reviewed, GECCO 2008; PDF pages | Idealised sense; sensing itself rewarded |
| **[BB72]** *(round 9)* | Chemotaxis "results from the suppression of directional changes"; going up a gradient "they change direction less frequently" [p.1]; wild-type runs about 0.86 s, 68° between runs [p.1, Table 1]; serine lengthens runs [p.2] | 3D tracking of single *E. coli* | **The primary source for run lengthening**, reached in Q11 only through [MK72] and [SBB86] | 📄 Peer-reviewed, *Nature*; scan | One species; OCR rough in Table 1 |
| **[SHLF21]** *(round 9)* | Density, basal cost, perceptual cost and mutation rate best predict perceptual range [p.1]; dense food lowered the threshold for any perception, and sparse food's larger ranges were "sporadic" [p.8]; cost cut perception [p.9] | Agent-based foragers with a fixed switch from random walk to approach; only range evolves; 150 steps [p.3–4] | **Supports dense food and a cheap sense** to start, as D134 and D262 hold (inference) | 📄 Peer-reviewed, *Front. Ecol. Evol.* 🔓 CC BY; PDF pages | Steering built in, not evolved |
| **[BEJ25]** *(round 9)* | Compass agents' long-range gathering was "more consistent and effective at larger environmental scales" [p.6]; extinction rarer at larger sizes [p.8]; vision agents moved less and ate more [p.9] | JAX grid world, up to 1,024² cells and 60,000 agents, reproduction only; four seeds a size [p.8] | **Cautions against a small tank** (inference) | 📄 arXiv preprint | Grid world; few seeds; p.8 says mining came sooner at small sizes and then at large |
| **[KGV22]** *(round 10)* | Under co-evolved body and control, a "copy" controller (each robot holds two CTRNNs that its modules share, a new module taking its parent's) differed significantly from the central CTRNN, the per-module CTRNN and an open-loop sine at generation 500; the central network "struggled more with early convergence"; copy and per-module controllers grew larger bodies [HTML, Results; Discussion]. Sine was not significantly different from either CTRNN at generation 500 [HTML, Results] | Simulated EMeRGE servo modules, Unity with PhysX, flat-ground locomotion; tournament GA, population 50, 500 generations, 64 runs a controller; two-sided Mann–Whitney U, Bonferroni-corrected; no diversity maintenance, by design | **Corrective for D146's second stage, by inference.** D146 copies a parent joint's wiring once, at birth, which is this paper's per-module rule; the rule that won shares one network for life | 📄 ALIFE 2022 🔓 green; ar5iv HTML through the fetch tool's summariser | Modular robots, not a Sims-style creature; the central network was sized from 100-generation pilots and set absent inputs to 0; the authors call the sensors unrealistic; locomotion only |
| **[MEC23]** *(round 10)* | Abstract only. Morphological mutations reduced locomotion less under modular controllers than under similar global ones, giving fitter offspring, more positive morphological mutations and better evolved robots; the cause named is a "fragile specialization" between controller and body plan | Simulated soft robots, GECCO 2023 | **Corrective for D146's second stage**, with [KGV22] and [CB18] | 📄 GECCO 2023, pp. 174–183; arXiv abstract | Soft-body substrate; design, runs and statistics not read |
| **[SM02]** *(round 10)* | Abstract only. NEAT's efficiency comes from a crossover between different topologies, speciation that protects structural innovation, and incremental growth from minimal structure; ablations show each necessary; it outperformed the best fixed-topology method on a reinforcement-learning benchmark | Neuroevolution of topology and weights | **Supports D146's growth by splitting connections, in principle.** The ablation numbers and mutation rates, which D146's guessed rates need, were not read | 📄 *Evolutionary Computation*; closed per OpenAlex; an author copy exists as a PDF and was not read (§3.13) | Nothing beyond the abstract; a pole-balancing benchmark, not a body |
| **[WSSMK05]** *(round 10)* | Abstract only. FS-NEAT learns "the network's inputs, topology, and weights simultaneously"; in car racing it learned better and faster than NEAT, its networks were smaller and needed fewer inputs, and it stayed robust as feature selection grew harder | One autonomous car-racing simulation | **Counter-weight to D147**, which wires every sense to every output from birth, where this line lets evolution choose the inputs (inference) | 📄 GECCO 2005, pp. 1225–1232; abstract from the co-author's publication page | How its founders are wired is not in the abstract; one task; CrossRef the only database matched |
| **[HFMT23]** *(round 10)* | Rhythm comes from two chains of coupled phase oscillators with curvature feedback; amplifying feedback below a spinal lesion restored a travelling wave, phase-locked across the lesion at 10× gain; injured swimmers spent 2 to 4 times the energy; feedback improved swimming in every condition tested [HTML, Results; Discussion] | 2D neuromechanical lamprey in a Navier–Stokes fluid, immersed boundary method | **Context for D146's ruling 4.** In a real swimmer's model the rhythm is central, and feedback shapes and couples it | 📄 PNAS, PMC HTML through the summariser | Simulation only, 2D; large gains needed; no regeneration or plasticity; feedback in uninjured swimmers is cited, not tested |
| **[GAM15]** *(round 10)* | Muscle torque following local curvature with a delay gives "a spontaneous swimming gait without the need for a central pattern generator"; such solutions exist only near the body's natural resonance frequencies; feedback strength and delay set speed and amplitude [HTML] | Theory of 2D slender inertial swimmers, compared with dace, trout and goldfish | **Precedent for D146's ruling 4.** Rhythm can live in the body and its local feedback, if the stroke sits near the body's resonance (inference) | 📄 PNAS, PMC HTML through the summariser | 2D; self-organised gaits were reached by continuation from a forced start; the paper's main model imposes a CPG-like wave |
| **[IL10]** *(round 10)* | Evolved circuits of an ON and an OFF chemosensory cell and two neck motor neurons, driven by an imposed head-sweep oscillator, produced realistic klinotaxis; the concentration change came from the sensory cells' averaging windows, with rise times under 1.7 s, not from slow network dynamics [HTML, Methods; Results; Discussion] | GA, population 10, 100 generations, 100 runs, eight parameters; fitness a chemotaxis index over 50 assays | **Diagnostic for Q15(d), by inference.** Brain v2 reads scent as a level and remembers one step through its loops, so it lacks the sensory differentiator these circuits relied on | 📄 *J. Neurosci.*, PMC HTML through the summariser | Point body, imposed oscillator, conical gradients only in evolution; the authors say networks working on other principles "almost certainly exist" |
| **[IB13]** *(round 10)* | Constrained by the *C. elegans* connectome, 83 of 100 runs reached a chemotaxis index of 0.5 and 27 reached 0.75; parameters varied widely across runs; the mechanism used sensory responses lasting about one head sweep and phase-dependent motor sensitivity, with no persistent memory [HTML, Results] | 100 runs; passive interneurons, motor self-connections, sine oscillator | **Supports [IL10]'s reading** in a second architecture, and gives a base rate: about a quarter of runs reached high performance on a wired minimal circuit | 📄 *PLoS Comput. Biol.*, PMC HTML; first 100,000 of 172,183 characters read through the summariser | Connectome incomplete; parameters not physiologically constrained; idealised body |

## 6. Annotated bibliography

**[EA23]** L. Eguiarte-Morett and W. Aguilar, "Premature convergence in morphology and
control co-evolution: a study," *Adaptive Behavior*, vol. 32, no. 2, pp. 137–165, 2023.
DOI: `10.1177/10597123231198497`, ✅ verified 📄 peer-reviewed 🔓 open access.
*This is the single most useful paper for this project*. It benchmarks five algorithms under one framework with 11 non-redundant indicators and a voting-theory adjudication, which is unusually rigorous for this field.

It supplied the entire premature-convergence section of the design and two concrete
corrections to the search algorithm. Its practical recommendation is MAP-Elites, despite
MAP-Elites not winning the formal comparison. That recommendation is stated explicitly and
is the basis for retaining it here. It was read in full.

**[U07]** Y. Usami, "Re-examination of Swimming Motion of Virtually Evolved Creature Based
on Fluid Dynamics," in *Advances in Artificial Life* (ECAL 2007), LNCS 4648, pp. 183–192.
DOI: `10.1007/978-3-540-74913-4_19`, ✅ verified 📄 peer-reviewed.
*This is the most uncomfortable paper for this design*. It is short, narrow, and directly damaging to the proposed fluid model. It demonstrates a published case where the evolutionary algorithm selected a gait that only worked because the physics was wrong, and where correct physics reversed the direction of travel.

Its concluding claim is that water physics rather than genetics determines swimming body
plans. That outruns its 2D single-morphology evidence and is not relied on here. The core
disagreement result is what matters. It was read in full.

**[K12]** P. Krčah, "Solving Deceptive Tasks in Robot Body-Brain Co-evolution by Searching
for Behavioral Novelty," in *Advances in Robotics and Virtual Reality*, ISRL vol. 26,
Springer, 2012, pp. 167–186. DOI: `10.1007/978-3-642-23363-0_7`, ✅ verified 📄 peer-reviewed.
*This is the most directly reusable engineering source*. It is a working rigid-body Sims-style system, described in enough implementation detail to copy. That covers genome structure, joint types, effector conditioning, and, most valuably, the anti-exploit machinery in §2.3. That machinery converted a vague risk in the design into a four-item rejection checklist.

Its headline negative result, that novelty search loses at swimming, is weakened by a
poorly-chosen behaviour descriptor. It is treated here as a caution about descriptor design
rather than about divergent search. It was read in full.

**[L21]** G. Lai, F. F. Leymarie, W. Latham, T. Arita, R. Suzuki, "Virtual Creature
Morphology – A Review," *Computer Graphics Forum*, vol. 40, no. 2, pp. 659–681, 2021.
DOI: `10.1111/cgf.142661`, ✅ verified 📄 peer-reviewed 🔓 open access.
*This is the field's map*. Table 6 alone settled the encoding question, by aggregating six published comparisons and revealing that the CPPN advantage is confined to soft-body phenotypes. Table 2 supplied the engine-precedent argument, and the survey also corrected a terminology error in the design.

Read in part (§4.1–4.2, Tables 2 and 6). The remainder, on cellular automata, GRNs,
L-systems and soft bodies, is unread and likely still holds value.

**[C18]** F. Corucci, N. Cheney, F. Giorgio-Serchi, J. Bongard, C. Laschi, "Evolving Soft
Locomotion in Aquatic and Terrestrial Environments: Effects of Material Properties and
Environmental Transitions," *Soft Robotics*, vol. 5, no. 4, pp. 475–495, 2018.
DOI: `10.1089/soro.2017.0055`, ✅ verified 📄 peer-reviewed.
*This is the richest of the eight*. It uses an almost identical fluid model to the one proposed here, and is unusually candid about what that model costs. That includes the finding that simplification suppresses morphological variety, which overturned the design's own cost-benefit reasoning. It is also the only source testing land-to-water ordering empirically.

The authors are frank about their own limitations, which are a weak EA, insufficient
repetitions and a non-significant transition result. That raises rather than lowers
confidence in what they do claim. Read in part (abstract, §2.1–2.2, §3.2–3.3, §4).

~~**Retrieved but unread**: [TM01], [CEA07], [CU15].~~ All three were read in part during
round 2 and are cited with page locators in `DESIGN.md` §13.2. This is struck rather than
deleted, per §0.

### Round 3 additions (2026-08-29)

**[Y94]** L. Yaeger, "Computational Genetics, Physiology, Metabolism, Neural Systems,
Learning, Vision, and Behavior or PolyWorld: Life in a New Context," in *Artificial Life
III*, SFI Studies in the Sciences of Complexity XVII, Addison-Wesley, 1994, pp. 263–298.
Pre-DOI, 📄 proceedings chapter, open author self-archive.
*This is the precedent §5A did not know it had*. It is an endogenous-selection ecology from 1994 that charges energy linearly per neuron and per synapse, prices every behaviour, and lets predation and mimicry emerge.

The neural-charge mechanism (p.7) directly anticipates §5A.2's neural term, which the design
had believed was its own invention. Read at targeted passages (pp.1, 7, 10–11), verified
against the downloaded text.

**[MC25]** A. Mertan and N. Cheney, "Evolutionary Brain-Body Co-Optimization Consistently
Fails to Select for Morphological Potential," arXiv:2508.17464v2 (accepted manuscript,
*Artificial Life*; extends ALIFE 2025). 🔓 open access.
*This is the §9-item-1 debt, finally read, and it was worth the wait*. It exhaustively measures the fitness of all 1.3M morphologies to get ground truth. It then shows every tested algorithm, MAP-Elites and morphological innovation protection included, regularly eliminating bodies whose true fitness is higher. Fitness under a co-evolving controller systematically undervalues the newly mutated.

That sharpens Q1 from "protection works" to "protection helps and is insufficient". Read at
the abstract, discussion and conclusion (pp.1, 29, 31).

**[CO02]** T. F. Cooper and C. Ofria, "Evolution of Stable Ecosystems in Populations of
Digital Organisms," in *Artificial Life VIII*, MIT Press, 2002, pp. 227–232. No DOI,
📄 proceedings, open author self-archive.
*This is the cleanest transferable result of the round*. Depletable resources give negative frequency-dependent selection, which gives stable multi-strategy coexistence. The control proves it: the same evolved ecosystems collapse to one genotype when resources are made unlimited.

That generalises D023's finite sun into a design rule. Read at the abstract and results
passages, verified (pp.1, 2, 4–5).

**[VG05]** J. Ventrella, "GenePool: Exploring the Interaction Between Natural Selection and
Sexual Selection," in *Artificial Life Models in Software*, Springer, 2005, pp. 81–96.
DOI `10.1007/1-84628-214-4_4`, 📄 chapter, open author self-archive.
*This explains why locomotion paid there.* Food and mates both require physical approach, so movement is the only route to reproduction. The design's own prize, depth, is reachable by buoyancy and demography, and that is the diagnostic contrast. It was read in full by the round-3 search agent, then spot-checked rather than fully re-read.

**[CB18]** N. Cheney, J. Bongard, V. SunSpiral, H. Lipson, "Scalable co-optimization of
morphology and control in embodied machines," *J. R. Soc. Interface* 15(143):20170937,

2018. DOI `10.1098/rsif.2017.0937`, ✅ CrossRef-verified 📄 peer-reviewed 🔓 OA. This was
promoted out of §13.4. It is the morphological-innovation-protection mechanism that §2.3 and
§8.4 lean on. It is now read at the method section (§II.C, pp.2–3 of the arXiv copy) rather
than inherited from [EA23]'s bibliography.

**[PU16]** J. K. Pugh, L. B. Soros, K. O. Stanley, "Quality Diversity: A New Frontier for
Evolutionary Computation," *Frontiers in Robotics and AI* 3:40, 2016. DOI
`10.3389/frobt.2016.00040`, ✅ CrossRef-verified 📄 peer-reviewed 🔓 OA. This was promoted out
of §13.4. It confirms the §8.3 claim about unaligned behaviour characterisations, with a
nuance worth preserving. Their conclusion is that "not just any type of behavioral diversity
is successful", which is weaker than a claim that misalignment always fails. It was read in
full by the round-3 search agent, then spot-checked.

Two more were retrieved in round 3 as supporting works, and are not in the synthesis matrix.

[GOY23] Goyal, Flamholz, Petroff & Murugan, "Closed ecosystems extract energy through
self-organized nutrient cycles," *PNAS* 120(52):e2309387120, 2023, read at abstract level
with the arXiv copy held. It is the theoretical anchor for remineralisation, and is to be
read in full before that mechanism's decision entry.

[ST00] Standish, "An Ecolab Perspective on the Bedau Evolutionary Statistics," *ALife VII*,
2000, read at abstract level with the arXiv copy held. It is the permuted-shadow
implementation for Q8.

### Round 4 additions (2026-09-01)

*These were all retrieved from open sources, with exact URLs in FETCH-RESULTS.md, and none through institutional access. Where a claim rests on a closed primary cited through one of these, that is marked at the point of use.*

**[KR13]** V. Křivan, "Behavioral refuges and predator–prey coexistence," *J. Theoretical
Biology* 339:112–121, 2013. DOI `10.1016/j.jtbi.2012.12.016`, 📄 peer-reviewed, author
self-archive. *This is the theory that predicted logbook/0043*. It carries the Maynard Smith
1974 fixed-number-versus-proportional result, and the equivalence between a refuge and a
type III response. It also carries the R\* equation showing that a refuge over the feeding
ground deletes the consumer. The reviewer verified it at [p.2].

**[MO04]** J. C. Moore et al. (17 authors), "Detritus, trophic dynamics and biodiversity,"
*Ecology Letters* 7(7):584–600, 2004. DOI `10.1111/j.1461-0248.2004.00606.x`, 📄
peer-reviewed, lab self-archive. *This is why our busts are anomalous*. The flux-fed
detritus chain with a mass-action consumer is globally stable. The reviewer verified it at
[p.7].

**[RC07]** S. Roy & J. Chattopadhyay, "The stability of ecosystems: a brief overview of
the paradox of enrichment," *J. Biosciences* 32(2):421–428, 2007. DOI
`10.1007/s12038-007-0040-1`, 📄 peer-reviewed, publisher OA. OA proxy for Rosenzweig 1971
(closed); catalogues the stabilisers and the mostly-negative empirical record.

**[JKT04]** J. M. Jeschke, M. Kopp & R. Tollrian, "Consumer-food systems: why type I
functional responses are exclusive to filter feeders," *Biological Reviews* 79(2):337–349,

2004. DOI `10.1017/S1464793103006286`, 📄 peer-reviewed, lab self-archive. A real type I
response has a satiation plateau by definition; unbounded linear clearance is not one.

**[DBWM05]** J. A. Dunne, U. Brose, R. J. Williams & N. D. Martinez, "Modeling food-web
dynamics: complexity–stability implications," in *Aquatic Food Webs* (OUP 2005),
pp.117–129, 📄 chapter, via SFI Working Paper 2004-07-021 (author-permitted OA). The
q=0.1 feeding-relaxation result; refuge-seeking, interference and switching unified as one
stabiliser.

**[HUF58]** C. B. Huffaker, "Experimental studies on predation: Dispersion factors and
predator-prey oscillations," *Hilgardia* 27(14):343–383, 1958. DOI
`10.3733/hilg.v27n14p343`, 📄 journal OA, fetched via Wayback (live link rotted). The
founding patchiness experiment. PDF page = journal page − 340.

**[JN97]** A. Janssen, E. van Gool, R. Lingeman, J. Jacas & G. van de Klashorst,
"Metapopulation dynamics of a persisting predator–prey system in the laboratory," *Exp.
Appl. Acarol.* 21:415–430, 1997. DOI `10.1023/A:1018479828913`, 📄 peer-reviewed, UvA-DARE
OA. The controlled well-mixed-vs-islands contrast. The reviewer verified it at [p.7].

**[RMF07]** T. Reichenbach, M. Mobilia & E. Frey, "Mobility promotes and jeopardizes
biodiversity in rock–paper–scissors games," *Nature* 448:1046–1049, 2007. DOI
`10.1038/nature06095`, 📄 peer-reviewed, arXiv author copy. The critical-mobility /
universal-wavelength result. The reviewer verified it.

**[FM15]** J. Z. Farkas, A. Yu. Morozov, E. G. Arashkevich & A. Nikishina, "Revisiting the
stability of spatially heterogeneous predator–prey systems under eutrophication," *Bull.
Math. Biol.* 77:1886–1908, 2015. DOI `10.1007/s11538-015-0108-2`, 📄 peer-reviewed,
arXiv:1509.03192. Gradient + mobile grazer stabilises type I feeding at unbounded K; the
well-mixed limit is the always-unstable case.

**[HZ13]** C. Hauzy et al., "Confronting the paradox of enrichment to the metacommunity
perspective," *PLoS ONE* 8(12):e82969, 2013. DOI `10.1371/journal.pone.0082969`, 📄
peer-reviewed, gold OA. Patch *inequality*, not subdivision, is what dispersal needs to
stabilise.

One work was retrieved in round 4 as supporting only, and is not in the synthesis matrix. It
is [DO21] Dolson & Ofria, "Digital Evolution for Ecology Research: A Review," *Front. Ecol.
Evol.* 9:750779, 2021, DOI `10.3389/fevo.2021.750779`. It is the field map for Q9(b)'s ALife
corner, and it confirms that the well-mixed-versus-structured persistence experiment has not
been run in digital evolution.

Three more were fetched and screened out: Chen, Wang & Liu 2023 (arXiv:2312.07737, a GLV
stability review), Mougi 2022 (*Sci. Rep.* 12:2464, interference at web level), and Moreno,
Rodriguez-Papa & Dolson 2024 (arXiv:2405.07245, phylogeny instrumentation, which is relevant
to Q8 rather than Q9).


### Round 5 additions (2026-09-03)

*These were all retrieved from open sources, with exact URLs in FETCH-RESULTS.md, through no institutional access and with no bot-gate bypassed. Page locators are PDF pages of the copy held in `research/papers/<n>-<key>/source.md`. Where a claim rests on a source this round could not fetch, it is marked* cited through *at the point of use.*

**[PC95]** D. Pauly & V. Christensen, "Primary production required to sustain global
fisheries," *Nature* 374:255–257, 1995. DOI `10.1038/374255a0`, 📄 peer-reviewed, Sea Around
Us self-archive. *This is the 10% rule's actual provenance*. Fig. 2's caption is the
load-bearing sentence. It reports 140 transfer-efficiency estimates drawn from 48 documented
trophic models, spanning **TL2 to TL4**. There is no trend across trophic level, and the
mean is "extremely close to" the conventional 10%. Read at the abstract and Fig. 2 [p.1,
p.3].

**[MCP05]** E. Marañón, P. Cermeño & V. Pérez, "Continuity in the photosynthetic production
of dissolved organic carbon from eutrophic to oligotrophic waters," *Mar. Ecol. Prog. Ser.*
299:7–17, 2005. DOI `10.3354/meps299007`, 📄 peer-reviewed, author self-archive
(em.webs.uvigo.es). *This is the best single number for D070, and the correction to its
shape*. PER is 22 ± 2%, flat at ~20% across a 150-fold productivity range. It is highest at
the dim base of the euphotic layer, because DOCp barely responds to irradiance while POCp
does. Read at the abstract, results and discussion [p.1, p.7, p.8, p.9, p.10].

**[LS13]** D. C. López-Sandoval, T. Rodríguez-Ramos, P. Cermeño & E. Marañón, "Exudation of
organic carbon by marine phytoplankton: dependence on taxon and cell size," *Mar. Ecol.
Prog. Ser.* 477:53–60, 2013. DOI `10.3354/meps10174`, 📄 peer-reviewed, author self-archive.
*This is the paper that says the rule can stay simple*. Across 22 species, 5 phyla and more
than 7 orders of magnitude of cell volume, exudation is isometric with size and indifferent
to growth stage. So "general allometric models cannot be used to predict exudation".

It also carries the culture-versus-field gap: about 2% in culture against 10 to more than
35% in assemblages. It was read in full.

**[LS11]** D. C. López-Sandoval, A. Fernández & E. Marañón, "Dissolved and particulate
primary production along a longitudinal gradient in the Mediterranean Sea,"
*Biogeosciences* 8:815–825, 2011. DOI `10.5194/bg-8-815-2011`, 📄 peer-reviewed, gold OA
CC BY. *This is the caveat with teeth*. PER is about 37% in ultraoligotrophic water, and
bacterial carbon demand is still 5–14 times the dissolved primary production. Exudation does
not feed the microbial loop on its own, even in the ocean. Read at the abstract, discussion
and conclusions [p.1, p.2, p.7, p.8].

**[CH20]** T.-Y. Chen, C.-C. Lai, F.-K. Shiah & G.-C. Gong, "Dissolved and Particulate
Primary Production and Subsequent Bacterial C Consumption in the Southern East China Sea,"
*Front. Mar. Sci.* 7:713, 2020. DOI `10.3389/fmars.2020.00713`, 📄 peer-reviewed, gold OA.
*This is independent corroboration at the high end*. It also carries Nagata 2000's 10–20% world-ocean range, which is the general figure this round recommends. PER is 40.8 ± 12.2%. Read at the abstract, results and discussion [p.1, p.5].

**[ED21]** T. D. Eddy, J. R. Bernhardt, J. L. Blanchard et al., "Energy Flow Through Marine
Ecosystems: Confronting Transfer Efficiency," *Trends in Ecology & Evolution* 36(1):76–86,

2021. DOI `10.1016/j.tree.2020.09.006`, 📄 peer-reviewed, UC eScholarship **accepted
manuscript** (locators are AM PDF pages rather than journal pages). *This is the reference
point [PC95] does not provide*. Producer to herbivore runs at 13% (11–17%), herbivore to
fish at 10% (7–12%), and the whole-ecosystem span is <1% to 52%. It also treats exudation
explicitly as a non-predatory diversion into the detrital and microbial channel. Read at the
abstract, the ecosystem-scale processes and the estimates [p.7, p.10, p.11, p.14, p.17].

**[PWAH07]** L. R. Pomeroy, P. J. leB. Williams, F. Azam & J. E. Hobbie, "The Microbial
Loop," *Oceanography* 20(2):28–33, 2007. DOI `10.5670/oceanog.2007.45`, ✅ CrossRef-verified
📄 peer-reviewed, gold OA (The Oceanography Society). *This is the microbial-loop anchor
obtainable while Inter-Research is bot-gated*, since Azam co-authored both this and the 1983
paper that named the loop. Most organic matter available to ocean consumers is respired by
bacteria, and in blue water only 1–2% of primary production reaches fishes. It was read in
full.

**[BP91]** ⚠ **NOT OBTAINED.** S. B. Baines & M. L. Pace, "The production of dissolved
organic matter by phytoplankton and its importance to bacteria: patterns across marine and
freshwater systems," *Limnology and Oceanography* 36(6):1078–1090, 1991. DOI
`10.4319/lo.1991.36.6.1078`. Wiley/ASLO returns 403 to non-browser clients. The Semantic
Scholar GREEN open-access record resolves to a **Figshare entry containing no file, only a
link back to the publisher DOI**. Its 13% cross-system mean is used in this document
**solely through three independently fetched papers**: [MCP05 p.9], [LS11 p.7] and [CH20
p.5]. They agree on the number, and each independently notes that its glass-fibre-filter
method underestimates dissolved release. It sits at the top of the round-5 manual-fetch
queue (§9).

**Sought and not obtained in round 5**, recorded rather than bypassed:

| work | why not |
|---|---|
| Thornton 2014, *Eur. J. Phycol.* 49:20–46, the field's standard review | Taylor & Francis returns 403 for both the PDF and the HTML full text to non-browser clients, so the widely-quoted "2–10% exponential phase rising to 10–60% in stationary phase" figures are **not asserted anywhere in this document** |
| Nagata 2000, a chapter in Kirchman, *Microbial Ecology of the Oceans* | closed; its 10–20% range enters via [CH20 p.5] |
| Fogg 1983, *Botanica Marina* | closed |
| Azam et al. 1983, *MEPS* 10:257, the microbial-loop paper | Inter-Research bot-gated, 401 |
| Fenchel 2008, *JEMBE* 366:99 | closed at Elsevier, no green copy |
| Marañón et al. 2004 and Teira et al. 2001, both *L&O* | Wiley-gated |
| Moran et al. 2022, *L&O* | hybrid CC BY-NC but Wiley-gated |
| Cole, Findlay & Pace 1988, *MEPS* 43:1 | Inter-Research bot-gated |

### Round 6 additions (2026-09-07)

**[P77]** E. M. Purcell, "Life at low Reynolds number," *Am. J. Phys.* 45(1):3–11, 1977.
DOI `10.1119/1.10903`, 📄 lecture essay, author self-archive (Cambridge DAMTP). Read at the
stirring and greener-pastures passages [p.7–8]. The bug "might as well wait for things to
diffuse"; swimming "does no good at all" for intake and is for finding "places where the food
is better".

**[BP77]** H. C. Berg & E. M. Purcell, "Physics of chemoreception," *Biophys. J.*
20(2):193–219, 1977. DOI `10.1016/S0006-3495(77)85544-6`, 📄 peer-reviewed, PMC. Read at the
abstract and the stirring section [p.1, p.8–9]. Motion and stirring cannot significantly
enhance adsorption at this size; local stirring gives at most twice the diffusive current.

**[D97]** D. B. Dusenbery, "Minimum size limit for useful locomotion by free-swimming
microbes," *PNAS* 94(20):10949–10954, 1997. DOI `10.1073/pnas.94.20.10949`, 📄
peer-reviewed, PMC. Read at the abstract, the dispersal section and the conclusion [p.1–2,
p.4]. The 0.6 µm limit and the genera survey.

**[KB96]** L. Karp-Boss, E. Boss & P. A. Jumars, "Nutrient fluxes to planktonic osmotrophs in
the presence of fluid motion," *Oceanogr. Mar. Biol. Annu. Rev.* 34:71–107, 1996. 📄
peer-reviewed review, author self-archive (University of Maine), scanned. Read at the
abstract [p.1]. A radius near 20 µm before swimming or sinking raises flux by about 50%;
flux rises with size and relative motion in every regime.

**[SL23]** J. Słomka, U. Alcolombri, F. Carrara, R. Foffi, F. J. Peaudecerf, M. Zbinden & R.
Stocker, "Encounter rates prime interactions between microorganisms," *Interface Focus*
13(2):20220059, 2023. DOI `10.1098/rsfs.2022.0059`, 📄 peer-reviewed, PMC. Read at the
encounter-mechanism section [p.2–5].

**[W11]** Y. Wei, X. Wang, J. Liu, I. Nemenman, A. H. Singh, H. Weiss & B. R. Levin, "The
population dynamics of bacteria in physically structured habitats and the adaptive virtue of
random motility," *PNAS* 108(10):4047–4052, 2011. DOI `10.1073/pnas.1013499108`, 📄
peer-reviewed, PMC. Read at the introduction and discussion [p.1, p.4]. *The round's clearest
statement of the order: self-propulsion first, chemotaxis after, and only in a structured
habitat.*

**[TS12]** J. R. Taylor & R. Stocker, "Trade-offs of chemotactic foraging in turbulent
water," *Science* 338(6107):675–679, 2012. DOI `10.1126/science.1219417`, 📄 peer-reviewed,
author self-archive (Stocker laboratory). Read at the abstract and results [p.2, p.4–5].

**[MK72]** R. M. Macnab & D. E. Koshland Jr., "The gradient-sensing mechanism in bacterial
chemotaxis," *PNAS* 69(9):2509–2512, 1972. DOI `10.1073/pnas.69.9.2509`, 📄 peer-reviewed,
PMC. Read at the abstract [p.1].

**[SBB86]** J. E. Segall, S. M. Block & H. C. Berg, "Temporal comparisons in bacterial
chemotaxis," *PNAS* 83(23):8987–8991, 1986. DOI `10.1073/pnas.83.23.8987`, 📄 peer-reviewed,
PMC. Read at the abstract [p.1].

**[SJ10]** T. Schweinitzer & C. Josenhans, "Bacterial energy taxis: a global strategy?"
*Arch. Microbiol.* 192(7):507–520, 2010. 📄 peer-reviewed, open access at Springer, PMC. Read
at the abstract and introduction [p.1].

**[J09]** G. Jékely, "Evolution of phototaxis," *Phil. Trans. R. Soc. B* 364(1531):2795–2808,
2009. DOI `10.1098/rstb.2009.0072`, 📄 peer-reviewed, PMC. Read at the eyespot section [p.7].

**[J11]** G. Jékely, "Origin and early evolution of neural circuits for the control of
ciliary locomotion," *Proc. R. Soc. B* 278(1707):914–922, 2011. DOI
`10.1098/rspb.2010.2027`, 📄 peer-reviewed, PMC. Read at the abstract and the larval
circuitry section [p.1, p.4].

**[JKG15]** G. Jékely, F. Keijzer & P. Godfrey-Smith, "An option space for early neural
evolution," *Phil. Trans. R. Soc. B* 370(1684):20150181, 2015. DOI `10.1098/rstb.2015.0181`,
📄 peer-reviewed, PMC. Read at the introduction and the cilia section [p.1–3, p.7].

**[KDL13]** F. Keijzer, M. van Duijn & P. Lyon, "What nervous systems do: early evolution,
input-output, and the skin brain thesis," *Adaptive Behavior* 21(2):67–85, 2013. DOI
`10.1177/1059712312465330`, 📄 peer-reviewed, author self-archive (Groningen, publisher's
version). Read at the introduction and §3.3 [p.4, p.8–9].

**[JG21]** G. Jékely & P. Godfrey-Smith, "Reafference and the origin of the self in early
nervous system evolution," *Phil. Trans. R. Soc. B* 376(1821):20190764, 2021. DOI
`10.1098/rstb.2019.0764`, 📄 peer-reviewed, PMC. Read at the abstract [p.1].

**[MZ09]** L. L. Moroz, "On the independent origins of complex brains and neurons," *Brain
Behav. Evol.* 74(3):177–190, 2009. DOI `10.1159/000258665`, 📄 peer-reviewed, PMC. Read at
the abstract and introduction [p.1].

**[HG25]** S. Heesom-Green, J. Shock & G. Nitschke, "Energy costs and neural complexity
evolution in changing environments," ALIFE 2025, arXiv:2511.20018. 📄 conference paper,
arXiv. Read at the abstract and results [p.1, p.5].

**Sought and not obtained in round 6**, recorded rather than bypassed: Fraenkel & Gunn
1940 (book; the kinesis vocabulary enters through [SJ10] and the sweep's reading of
secondary sources); Berg & Brown 1972, *Nature*; Adler 1966, *Science*; Wadhams & Armitage
2004 and Arendt 2008, Arendt et al. 2016, *Nature Reviews*; Wuichet & Zhulin 2010, *Science
Signaling*; Mitchell 2002, *Am. Nat.*, and Mackie 1970, *Q. Rev. Biol.* (University of
Chicago); Gerritsen & Strickler 1977; Visser & Kiørboe 2006 (Springer; DTU's record carries
no copy); Kiørboe 2011 (Wiley); Mitchell & Kogure 2006 (no PMC record); Fenchel 2002;
Bosch et al. 2017 (repository copy restricted); Egbert et al. 2012 (MIT Press); Elsberry et
al. 2009 (mirrors only).

### Round 7 additions (2026-10-06)

*Round 7 saved no copy of anything it read. Its five full texts were read as XML or HTML from
PubMed Central, Europe PMC and bioRxiv, and those carry no PDF pages. So the locators in this
round name a section and a figure ([FDT11 Results, Fig. 3]) where the citation convention
asks for a PDF page. A claim can be re-checked by fetching the open copy that
`FETCH-RESULTS.md` names. Every record was matched in at least two databases and none is
retracted. "Abstract only" means nothing beyond the abstract was read, and the entry claims
nothing beyond it.*

**[FDT11]** B. Fischer, U. Dieckmann & B. Taborsky, "When to store energy in a stochastic
environment," *Evolution* 65(5):1221–1232, 2011 (online 2010). DOI
`10.1111/j.1558-5646.2010.01198.x`, ✅ CrossRef, OpenAlex and Europe PMC 📄 peer-reviewed,
author manuscript at PMC. It was read in full. *This is the model nearest to round 57's reserve.* It
finds no storage at low variability and low predictability, and more storage as either rises
[Results, Figs 3–4]. Its optimum responds to a sensed environment and assumes no minimum
breeding investment [model conditions; Discussion]. Round 57's bodies sense no light and
must reach a breeding gate.

**[VJTF09]** Ø. Varpe, C. Jørgensen, G. A. Tarling & Ø. Fiksen, "The adaptive value of
energy storage and capital breeding in seasonal environments," *Oikos* 118(3):363–370, 2009.
DOI `10.1111/j.1600-0706.2008.17036.x`, ✅ CrossRef, OpenAlex and Semantic Scholar 📄
peer-reviewed, closed. Abstract only, read from the OpenAlex index. Its main finding is that
stores pay by allowing breeding before the feeding season, when an egg is worth most. Round
57's clause on the timing of births rests on this abstract alone, so it heads the fetch queue
(§9).

**[PW21]** J. S. Park & J. T. Wootton, "Slower environmental cycles maintain greater
life-history variation within populations," *Ecology Letters* 24(11):2452–2463, 2021. DOI
`10.1111/ele.13867`, ✅ CrossRef, OpenAlex and Europe PMC 📄 peer-reviewed 🔓 CC BY 4.0. Read in
full; supplements S1–S7 not read. A model and an experiment agree that slower cycles keep
more trait variance, and that the variance falls over time in every regime [Results, Figs
4–5]. The authors themselves ask for work on the ratio of cycle period to generation time,
and round 57's ratio lies outside the range they tested.

**[GSS14]** J. K. Graham, M. L. Smith & A. M. Simons, "Experimental evolution of bet hedging
under manipulated environmental uncertainty in *Neurospora crassa*," *Proc. R. Soc. B*
281(1787):20140706, 2014. DOI `10.1098/rspb.2014.0706`, ✅ CrossRef, OpenAlex and PMC 📄
peer-reviewed, PMC. Main text read in full; supplement not read. A direct test of Cohen's
1966 dormancy model, with dormancy evolving up and down with the frequency of bad years
[Results, Table 2, Fig. 1].

**[TGC17]** E. Tarazona, E. M. García-Roger & M. J. Carmona, "Experimental evolution of bet
hedging in rotifer diapause traits as a response to environmental unpredictability," *Oikos*
126(8):1162–1172, 2017. DOI `10.1111/oik.04186`, ✅ CrossRef, OpenAlex and Semantic Scholar
📄 peer-reviewed. Abstract only; the open repository copy is behind a bot challenge that was
not bypassed. Its design is read in the same group's open paper, Tarazona et al. 2019,
*Scientific Reports*, DOI `10.1038/s41598-019-56100-y` (PMC6927961), Methods and
Introduction, read in full. That paper is a supporting source and is not in the synthesis
matrix.

**[MK07]** C. J. E. Metcalf & D. N. Koons, "Environmental uncertainty, autocorrelation and
the evolution of survival," *Proc. R. Soc. B* 274(1622):2153–2160, 2007. DOI
`10.1098/rspb.2007.0561`, ✅ CrossRef, OpenAlex and PMC 📄 peer-reviewed, PMC. Read in full,
with the figures seen by caption only. Not to be confused with [MK72]. It holds one side of
the autocorrelation dispute in §7.3.

**[WTV18]** D. J. Wieczynski, P. E. Turner & D. A. Vasseur, "Temporally autocorrelated
environmental fluctuations inhibit the evolution of stress tolerance," *Am. Nat.*
191(6):E195–E207, 2018. DOI `10.1086/697200`, ✅ CrossRef, OpenAlex, Europe PMC and Semantic
Scholar 📄 peer-reviewed, closed. Abstract only; no preprint was found, and the authors'
Zenodo record holds data only. It holds the other side of the dispute.

**[SK12]** J. Starrfelt & H. Kokko, "Bet-hedging—a triple trade-off between means, variances
and correlations," *Biological Reviews* 87(3):742–755, 2012. DOI
`10.1111/j.1469-185X.2012.00225.x`, ✅ CrossRef, OpenAlex, Europe PMC and Semantic Scholar 📄
peer-reviewed. Abstract only; the free link Europe PMC lists at Wiley returned 403.

**[WYRW25]** M. Weissman, Z. Yin, Y. Raynes & D. Weinreich, "Beyond the (geometric) mean:
stochastic models undermine deterministic predictions of bet hedger evolution," *Am. Nat.*
205(6):572–589, 2025. DOI `10.1086/735690`, ✅ CrossRef, OpenAlex, Europe PMC and bioRxiv 📄
peer-reviewed. Journal version abstract only; its open eScholarship copy refused the fetcher.
The bioRxiv preprint, DOI `10.1101/2023.07.11.548608`, was read in full in version 1
(2023-07-12) and version 2 (2024-01-11). The preprint has three authors and no *Clarkia*
case, so a claim taken from it may differ in the journal.

**[BWWR15]** C. A. Botero, F. J. Weissing, J. Wright & D. R. Rubenstein, "Evolutionary
tipping points in the capacity to adapt to environmental change," *PNAS* 112(1):184–189,
2015. DOI `10.1073/pnas.1408589111`, ✅ CrossRef, OpenAlex and PMC 📄 peer-reviewed 🔓 PNAS open
access option. Main text read in full; SI not read.

### Round 8 additions (2026-09-29, filed 2026-10-31)

*Round 8 kept fourteen PDFs. Their text was extracted on 2026-10-31, at filing, into a
`source.md` for each with `### Page N` headings, and every page locator here matches one. Each
page-cited claim was checked against those pages the same day. "HTML" marks a PubMed
Central article page, which carries no page numbers. "Abstract only" means nothing beyond
the abstract was read. Every DOI was matched at CrossRef and OpenAlex, and none is retracted.
Twenty-five supporting sources are listed after the core entries, outside the synthesis
matrix, as round 7's Tarazona 2019 is.*

**[PER19]** G. Péron, J.-F. Lemaître, V. Ronget, M. Tidière & J.-M. Gaillard, "Variation in
actuarial senescence does not reflect life span variation across mammals," *PLoS Biology*
17:e3000432, 2019. DOI `10.1371/journal.pbio.3000432`, ✅ 📄 🔓. PDF [85], read at p.3 and
p.6. *The sharpest evidence against the gene's scaling.*

**[COL21]** F. Colchero et al., "The long lives of primates and the 'invariant rate of
ageing' hypothesis," *Nature Communications* 12:3666, 2021. DOI
`10.1038/s41467-021-23894-3`, ✅ 📄 🔓. PDF [86], read at p.1 and p.3. The within-lineage
counterpart of [PER19].

**[JON14]** O. R. Jones et al., "Diversity of ageing across the tree of life," *Nature*
505:169–173, 2014. DOI `10.1038/nature12789`, ✅ 📄. Read as the PMC4157354 article page,
because the PDF sat behind PubMed Central's gate.

**[KR91]** T. B. L. Kirkwood & M. R. Rose, "Evolution of senescence: late survival
sacrificed for reproduction," *Phil. Trans. R. Soc. B* 332:15–24, 1991. DOI
`10.1098/rstb.1991.0028`, ✅ 📄, closed. Abstract only. It is the disposable-soma anchor
D128 names; Kirkwood 1977 was not retrieved.

**[MC19]** A. A. Maklakov & T. Chapman, "Evolution of ageing as a tangle of trade-offs:
energy versus function," *Proc. R. Soc. B* 286:20191604, 2019. DOI
`10.1098/rspb.2019.1604`, ✅ 📄. PDF [87], University of East Anglia repository, read at
p.1 and p.4. The round's main dissent from an energy-priced lifespan.

**[LEM15]** J.-F. Lemaître et al., "Early-late life trade-offs and the evolution of ageing
in the wild," *Proc. R. Soc. B* 282:20150209, 2015. DOI `10.1098/rspb.2015.0209`, ✅ 📄.
Read as the PMC4426628 article page.

**[BBV20]** J. J. Boonekamp, C. Bauch & S. Verhulst, "Experimentally increased brood size
accelerates actuarial senescence and increases subsequent reproductive effort in a wild
bird population," *J. Anim. Ecol.* 89:1395–1407, 2020. DOI `10.1111/1365-2656.13186`, ✅ 📄
🔓 CC BY. PDF [88], Glasgow repository, read at p.3 and p.6. *The one measurement of a
birth's effect on the Gompertz slope.*

**[WIN25]** L. A. Winder, M. J. P. Simons & T. Burke, "The optimal clutch size revisited:
separating individual quality from the parental survival costs of reproduction," *eLife*
12:RP87018, 2025, reviewed preprint version 4. DOI `10.7554/eLife.87018`, ✅ 📄 🔓. PDF [89],
read at p.1, p.3 and p.14. §4 records its title note.

**[SADK00]** S. C. Stearns, M. Ackermann, M. Doebeli & M. Kaiser, "Experimental evolution of
aging, growth, and reproduction in fruitflies," *PNAS* 97:3309–3313, 2000. DOI
`10.1073/pnas.060289597`, ✅ 📄. Read as the PMC16235 article page.

**[ZBH95]** B. Zwaan, R. Bijlsma & R. F. Hoekstra, "Direct selection on life span in
*Drosophila melanogaster*," *Evolution* 49:649–659, 1995. DOI
`10.1111/j.1558-5646.1995.tb02301.x`, ✅ 📄, closed. Abstract only.

**[LC85]** L. S. Luckinbill & M. J. Clare, "Selection for life span in *Drosophila
melanogaster*," *Heredity* 55:9–18, 1985. DOI `10.1038/hdy.1985.66`, ✅ 📄. PDF [91], read
at p.1, p.4 and p.5.

**[MR87]** T. A. Mousseau & D. A. Roff, "Natural selection and the heritability of fitness
components," *Heredity* 59:181–197, 1987. DOI `10.1038/hdy.1987.113`, ✅ 📄. PDF [92], a
scan, read at p.6 and p.8.

**[CAR92]** Cartar, "Morphological senescence and longevity … wing wear and life span in
foraging wild bumble bees," *J. Anim. Ecol.* 61:225–231, 1992. DOI `10.2307/5525`, ✅ 📄,
closed. Abstract only.

**[KK16]** A. Kowald & T. B. L. Kirkwood, "Can aging be programmed? A critical literature
review," *Aging Cell* 15:986–998, 2016. DOI `10.1111/acel.12510`, ✅ 📄 🔓 CC BY. PDF [95],
Copenhagen repository, read at p.13. *The check this world's lifespan results need.*

Supporting sources, outside the matrix:

| key | citation | DOI | read |
|---|---|---|---|
| [FPW90] | Finch, Pike & Witten 1990, "Slow mortality rate accelerations during aging…", *Science* 249:902–905 | `10.1126/science.2392680` | abstract |
| [KIR15] | Kirkwood 2015, "Deciphering death: a commentary on Gompertz (1825)", *Phil. Trans. B* 370:20140379 | `10.1098/rstb.2014.0379` | HTML |
| [REI22] | Reinke et al. 2022, "Diverse aging rates in ectothermic tetrapods…", *Science* 376:1459–1466 | `10.1126/science.abm0151` | abstract |
| [DAS22] | da Silva et al. 2022, "Slow and negligible senescence among testudines…", *Science* 376:1466–1470 | `10.1126/science.abl7811` | abstract |
| [RS02] | Ricklefs & Scheuerlein 2002, "Biological implications of the Weibull and Gompertz models of aging", *J. Gerontol. A* 57:B69–B76 | `10.1093/gerona/57.2.b69` | abstract |
| [NUS13] | Nussey, Froy, Lemaître, Gaillard & Austad 2013, "Senescence in natural populations of animals", *Ageing Res. Rev.* 12:214–225 | `10.1016/j.arr.2012.07.004` | HTML |
| [SPK05] | Speakman 2005, "Body size, energy metabolism and lifespan", *J. Exp. Biol.* 208:1717–1730 | `10.1242/jeb.01556` | abstract |
| [SWO21] | Swovick et al. 2021, "Interspecies differences in proteome turnover kinetics…", *Mol. Cell. Proteomics* 20:100041 | `10.1074/mcp.ra120.002301` | HTML |
| [COH20] | Cohen et al. 2020, "Are trade-offs really the key drivers of ageing and life span?", *Funct. Ecol.* 34:153–166 | `10.1111/1365-2435.13444` | abstract |
| [CIC97] | Cichoń 1997, "Evolution of longevity through optimal resource allocation", *Proc. R. Soc. B* 264:1383–1388 | `10.1098/rspb.1997.0192` | abstract |
| [BOO14] | Boonekamp et al. 2014, "Reproductive effort accelerates actuarial senescence in wild birds", *Ecol. Lett.* 17:599–605 | `10.1111/ele.12263` | abstract |
| [SN12] | Santos & Nakagawa 2012, "The costs of parental care: a meta-analysis…", *J. Evol. Biol.* 25:1911–1917 | `10.1111/j.1420-9101.2012.02569.x` | abstract |
| [RM19] | Rodríguez-Muñoz et al. 2019, "Testing the effect of early-life reproductive effort on age-related decline in a wild insect", *Evolution* 73:317–328 | `10.1111/evo.13679` | PDF [90], p.1 |
| [HAM10] | Hamel et al. 2010, "Fitness costs of reproduction depend on life speed…", *Ecol. Lett.* 13:915–935 | `10.1111/j.1461-0248.2010.01478.x` | abstract |
| [H92] | Houle 1992, "Comparing evolvability and variability of quantitative traits", *Genetics* 130:195–204 | `10.1093/genetics/130.1.195` | abstract |
| [RUBY18] | Ruby et al. 2018, "Estimates of the heritability of human longevity are substantially inflated…", *Genetics* 210:1109–1124 | `10.1534/genetics.118.301613` | HTML |
| [KAT13] | Katzenberger et al. 2013, "A *Drosophila* model of closed head traumatic brain injury", *PNAS* 110:E4152–E4159 | `10.1073/pnas.1316895110` | HTML |
| [MIT01] | Mitnitski, Mogilner & Rockwood 2001, "Accumulation of deficits as a proxy measure of aging", *Sci. World J.* 1:323–336 | `10.1100/tsw.2001.58` | abstract |
| [PEN95] | Penna 1995, "A bit-string model for biological aging", *J. Stat. Phys.* 78:1629–1633 | `10.1007/bf02180147` | PDF [93], p.3 |
| [TRA04] | Travis 2004, "The evolution of programmed death in a spatially structured population", *J. Gerontol. A* 59:B301–B305 | `10.1093/gerona/59.4.b301` | abstract |
| [WER15] | Werfel, Ingber & Bar-Yam 2015, "Programed death is favored by natural selection in spatial systems", *Phys. Rev. Lett.* 114:238103 | `10.1103/physrevlett.114.238103` | PDF [94], p.2 |
| [RP10] | Ronce & Promislow 2010, "Kin competition, natal dispersal and the moulding of senescence…", *Proc. R. Soc. B* 277:3659–3667 | `10.1098/rspb.2010.1095` | HTML |
| [ROG24] | Roget, MacMurray, Jolivet, Méléard & Rera 2024, "A scenario for an evolutionary selection of ageing", *eLife* 13:RP92914 | `10.7554/eLife.92914` | PDF [97], p.1, p.3–5 |
| [MRR19] | Méléard, Rera & Roget 2019, "A birth-death model of ageing…", *J. Math. Biol.* 79:901–939 | `10.1007/s00285-019-01382-z` | PDF [96], no claim at a page |
| [GT22] | Giaimo & Traulsen 2022, "The selection force weakens with age because ageing evolves and not vice versa", *Nat. Commun.* 13:686 | `10.1038/s41467-022-28254-3` | PDF [98], p.1 |

### Round 9 additions (2026-09-29, fetched 2026-09-30, filed 2026-10-31)

*Round 9 kept twenty PDFs, each extracted at fetch into a `source.md` with `### Page N`
headings and a manifest. Every locator here matches one, and pass 2 located each claim on
2026-10-31. No DOI was matched at a database and no retraction check was run (§7.2). Five
supporting sources follow the core entries, outside the synthesis matrix.*

**[MD08]** J.-B. Mouret & S. Doncieux, "Incremental evolution of animats' behaviors as a
multi-objective optimization," SAB 2008, LNAI 5040, pp. 210–219. DOI
`10.1007/978-3-540-69134-1_21`, 📄. PDF [175], read at p.2–3, p.7 and p.9. *The taxonomy D134 leans on.*

**[MD09]** J.-B. Mouret & S. Doncieux, "Overcoming the bootstrap problem in evolutionary
robotics using behavioral diversity," IEEE CEC 2009, pp. 1161–1168, IEEE Xplore document
4983077, DOI not recorded, 📄. PDF [176], read at p.1–2 and p.5–6.

**[GM97]** F. Gomez & R. Miikkulainen, "Incremental evolution of complex general behavior,"
*Adaptive Behavior* 5(3–4):317–342, 1997. DOI `10.1177/105971239700500305`, 📄. PDF [177],
a scan, read at p.1–2, p.10, p.13–14, p.17 and p.20.

**[JAK97]** N. Jakobi, "Evolutionary robotics and the radical envelope-of-noise hypothesis,"
*Adaptive Behavior* 6(2), 1997. DOI `10.1177/105971239700600205`, 📄. PDF [178], a scan,
read at p.8–10 and p.39–40.

**[PJ10]** Pilat & Jacob, "Evolution of vision capabilities in embodied virtual creatures,"
GECCO 2010. DOI `10.1145/1830483.1830502`, 📄. PDF [179], read at p.4–5 and p.8.

**[CA16]** N. Chaumont & C. Adami, "Evolution of sustained foraging in three-dimensional
environments with physics," *GPEM* 17(4):359–390, 2016. DOI `10.1007/s10710-016-9270-z`, 📄.
PDF [184], read at p.1, p.6, p.11, p.14–16, p.23 and p.25. *The nearest substrate to this project.*

**[LOPA03]** R. E. Lenski, C. Ofria, R. T. Pennock & C. Adami, "The evolutionary origin of
complex features," *Nature* 423:139–144, 2003. DOI `10.1038/nature01568`, 📄. PDF [185], read
at p.1, p.3 and p.5.

**[CLWO13]** A. W. Covert III, R. E. Lenski, C. O. Wilke & C. Ofria, "Experiments on the role
of deleterious mutations as stepping stones in adaptive evolution," *PNAS* 110(34):E3171–E3178,
2013. DOI `10.1073/pnas.1313424110`, 📄. PDF [190], read at p.1–3.

**[KNA07]** N. Kashtan, E. Noor & U. Alon, "Varying environments can speed up evolution,"
*PNAS* 104(34):13711–13716, 2007. DOI `10.1073/pnas.0611630104`, 📄. PDF [187], read at p.1–5.

**[WLCS19]** R. Wang, J. Lehman, J. Clune & K. O. Stanley, "Paired open-ended trailblazer
(POET)," arXiv:1901.01753 v3, 2019. PDF [188], read at p.3, p.7, p.10, p.13–14 and p.18–19.

**[LST11]** J. Lehman & K. O. Stanley, "Abandoning objectives: evolution through the search
for novelty alone," *Evolutionary Computation* 19(2):189–223, 2011, DOI not recorded, 📄. PDF
[183], the authors' layout, read at p.12 and p.15–19.

**[GEOP08]** L. M. Grabowski, W. R. Elsberry, C. Ofria & R. T. Pennock, "On the evolution of
motility and intelligent tactic response," GECCO 2008. DOI `10.1145/1389095.1389129`, 📄. PDF
[182], read at p.4–7.

**[BB72]** H. C. Berg & D. A. Brown, "Chemotaxis in *Escherichia coli* analysed by
three-dimensional tracking," *Nature* 239:500, 1972. DOI `10.1038/239500a0`, 📄. PDF [186], a
scan, read at p.1–2. *Round 6's founding text, now held.*

**[SHLF21]** Swain, Hoffman, Leyba & Fagan, "Exploring the evolution of perception: an
agent-based approach," *Front. Ecol. Evol.* 9:698041, 2021. DOI `10.3389/fevo.2021.698041`, 📄
🔓 CC BY. PDF [191], read at p.1, p.3–4 and p.8–9.

**[BEJ25]** J. Bejjani et al., "The emergence of complex behavior in large-scale ecological
environments," arXiv:2510.18221, 2025. PDF [192], read at p.1–2, p.6 and p.8–9.

Supporting sources, outside the matrix:

| key | citation | DOI | read |
|---|---|---|---|
| [LFM13] | Lessin, Fussell & Miikkulainen 2013, "Open-ended behavioral complexity for evolved virtual creatures", GECCO 2013 | `10.1145/2463372.2463411` | PDF [180], p.1–4; a hand-designed syllabus of fitness goals, no control |
| [WS11] | Woolley & Stanley 2011, "On the deleterious effects of a priori objectives on evolution and representation", GECCO 2011 | `10.1145/2001576.2001707` | PDF [181], p.1, p.4–5; Picbreeder images re-evolved as targets: three of six failed in all 20 runs |
| [LM14] | Lehman & Miikkulainen 2014, "Overcoming deception in evolution of cognitive behaviors", GECCO 2014 | `10.1145/2576768.2598300` | PDF [189], p.1–2, p.6–7; objective search under 20% on T-maze memory tasks |
| [TEN26] | Tensen et al. 2026, "Microcosmos: reimagining artificial life for the GPU era", arXiv:2607.02954 | — | PDF [194], p.4, p.6; chemotaxis shown under displacement plus energy, no figures in the text |
| [AB14] | Auerbach & Bongard 2014, "Environmental influence on the evolution of morphological complexity in machines", *PLoS Comput. Biol.* 10(1):e1003399 | `10.1371/journal.pcbi.1003399` | PDF [195], p.1, p.4; complex environments gave complex bodies under a cost |

### Round 10 additions (2026-10-31)

*Round 10 kept no copy of anything it read. Its full texts were PubMed Central and ar5iv HTML
pages, read through the fetch tool, which returns a small model's answer to a prompt rather than
the page's text. "HTML" with a section name is the locator, as in rounds 7 and 8. Quotations are
the tool's report of the page. "Abstract only" means nothing beyond the abstract was read. Every
core DOI but one was matched in two of CrossRef, OpenAlex and PMC, and none carries a retraction or
correction. Eight supporting sources follow the core entries, outside the matrix.*

**[KGV22]** M.-K. Kvalsund, K. Glette & F. Veenstra, "Centralized and Decentralized Control in
Modular Robots and Their Effect on Morphology," ALIFE 2022. DOI `10.1162/isal_a_00533`, ✅
CrossRef and OpenAlex 📄 🔓 arXiv 2206.13366. Read as ar5iv HTML. *The one controlled comparison
of central against shared control under a changing body.*

**[MEC23]** A. Mertan & N. Cheney, "Modular Controllers Facilitate the Co-Optimization of
Morphology and Control in Soft Robots," GECCO 2023, pp. 174–183. DOI `10.1145/3583131.3590416`,
✅ CrossRef and OpenAlex 📄. Abstract only, from arXiv 2306.09358v1.

**[SM02]** K. O. Stanley & R. Miikkulainen, "Evolving Neural Networks through Augmenting
Topologies," *Evolutionary Computation* 10(2):99–127, 2002. DOI `10.1162/106365602320169811`, ✅
CrossRef and OpenAlex 📄, closed. Abstract only, from OpenAlex. *The source D146's growth rests
on, and the one this round most needs read in full* (§9).

**[WSSMK05]** S. Whiteson, P. Stone, K. O. Stanley, R. Miikkulainen & N. Kohl, "Automatic feature
selection in neuroevolution," GECCO 2005, pp. 1225–1232. DOI `10.1145/1068009.1068210`, ✅
CrossRef 📄. Abstract only, from Stone's publication page.

**[HFMT23]** C. Hamlet, L. Fauci, J. R. Morgan & E. D. Tytell, "Proprioceptive feedback
amplification restores effective locomotion in a neuromechanical model of lampreys with spinal
injuries," *PNAS* 120(11):e2213302120, 2023. DOI `10.1073/pnas.2213302120`, ✅ CrossRef and PMC
📄. Read as the PMC10089168 article page.

**[GAM15]** M. Gazzola, M. Argentina & L. Mahadevan, "Gait and speed selection in slender
inertial swimmers," *PNAS* 112(13):3874–3879, 2015. DOI `10.1073/pnas.1419335112`, ✅ CrossRef and
PMC 📄. Read as the PMC4386394 article page.

**[IL10]** E. J. Izquierdo & S. R. Lockery, "Evolution and Analysis of Minimal Neural Circuits for
Klinotaxis in *Caenorhabditis elegans*," *J. Neurosci.* 30(39):12908–12917, 2010. DOI
`10.1523/JNEUROSCI.2606-10.2010`, ✅ CrossRef and PMC 📄. Read as the PMC3422662 article page.
Pass 1 gave the second author as Beer.

**[IB13]** E. J. Izquierdo & R. D. Beer, "Connecting a Connectome to Behavior: An Ensemble of
Neuroanatomical Models of *C. elegans* Klinotaxis," *PLoS Comput. Biol.* 9(2):e1002890, 2013. DOI
`10.1371/journal.pcbi.1002890`, ✅ CrossRef and PMC 📄 🔓. Read as the PMC3567170 article page,
the first 100,000 of 172,183 characters.

Supporting sources, outside the matrix:

| key | citation | DOI or id | read | claim used |
|---|---|---|---|---|
| [HMP20] | Huang, Mordatch & Pathak 2020, "One Policy to Control Them All: Shared Modular Policies for Agent-Agnostic Control" | arXiv 2007.04976 | HTML | per-limb modules with shared weights and message passing generalise to unseen bodies; without messages limbs learn nothing useful [appendix]. Deep RL, so outside the PICOC; cited for the architecture only |
| [DRC18] | Denham, Ranner & Cohen 2018, "Signatures of proprioceptive control in *Caenorhabditis elegans* locomotion", *Phil. Trans. B* 373:20180208 | `10.1098/rstb.2018.0208` | HTML | a model with no CPG reproduces the worm's frequency and wavelength from curvature feedback; the authors leave the CPG question open |
| [SBRB23] | Singh, van Breugel, Rao & Brunton 2023, "Emergent behaviour and neural dynamics in artificial agents tracking odour plumes", *Nat. Mach. Intell.* 5:58–70 | `10.1038/s42256-022-00599-w` | HTML, first 100,000 of 131,533 characters | 64-unit RNNs beat feedforward agents on every plume task; most integration timescales were about 0.5 s or less; longer memory mattered when the wind switched. Trained by RL, not evolved |
| [VSG23] | Veenstra, Szorkovszky & Glette 2023, "Decentralized Control and Morphological Evolution of 2D Virtual Creatures", ALIFE 2023 | `10.1162/isal_a_00656` | abstract | a phase-coupled oscillator beat a sine wave under both encodings, a neural network fell between them |
| [IHW99] | Ijspeert, Hallam & Willshaw 1999, "Evolving Swimming Controllers for a Simulated Lamprey with Inspiration from Neurobiology", *Adaptive Behavior* 7(2):151–172 | `10.1177/105971239900700202` | abstract (OpenAlex) | a GA evolved CPG architectures for a simulated lamprey that covered wider frequency, phase-lag and speed ranges than Ekeberg's hand-built model |
| [SAO22] | Sarti, Adair & Ochoa 2022, "Recombination and Novelty in Neuroevolution: A Visual Analysis", *SN Comput. Sci.* 3(3) | `10.1007/s42979-022-01064-6` | abstract (OpenAlex index) | on mazes, NEAT's runs with and without crossover question the usefulness of recombination |
| [SUCH17] | Such et al. 2017, "Deep Neuroevolution: Genetic Algorithms Are a Competitive Alternative…" | arXiv 1712.06567 | HTML | Gaussian mutation of every weight with σ from 0.002 to 0.005 on networks of millions of weights, no crossover, populations of 1,000 to 20,000 |
| [MM15] | Mishkin & Matas 2015, "All you need is a good init" | arXiv 1511.06422 | HTML | LSUV rescales each layer to unit output variance; tested on feedforward CNNs only; slower convergence with tanh |

## 7. Threats to validity

### 7.1 Search validity

- ⚠ **The database protocol** was not followed. Discovery used general web search rather
  than native queries to Semantic Scholar, DBLP, OpenAlex or OpenReview.

  The consequence is no reproducible result counts, no field-restricted queries, no
  systematic date filtering, and ranking determined by a general search engine rather than
  by citation structure. Relevant work may have been missed for want of the right phrasing.

- ~~**Forward snowballing was not performed.**~~ ↻ **Performed in round 3** (2026-08-29),
  through the Semantic Scholar citation API. The 2025 preprint was found, read in part, and
  is now [MC25] in the synthesis.

  Two findings from the snowball are themselves threats worth recording. **[EA23] has only 3
  recorded citations.** It is a niche paper, so §2 should lean on its *mechanism*,
  independently attributed to three prior groups. Its algorithm ranking has no independent
  replication.

  And the ~46 works citing [C18] are almost entirely voxel and soft-body work, so **the
  rigid-articulated aquatic corner this project occupies has almost no recent citation
  traffic**. That is a field gap rather than a search failure.

  The earlier struck text is kept below, per §0. A prediction the review made about itself
  and kept late belongs in the document.

  ~~*Round 2 addresses this, as step 2 of the update protocol in §3.5.*~~

  ~~Round 2 did not address it. That round was triggered by a new question and searched
  nothing. Forward snowballing remains outstanding and is now overdue.~~

- **Saturation was asserted** rather than measured. Pass 1 stopped when new queries returned
  known papers, which is a weak criterion applied over three batches.

- ⚠ **Round 2 answered** a question the corpus was not assembled for. The eight held papers
  were retrieved against round 1's questions on encodings, quality-diversity and physics
  exploitation. None was retrieved because it addressed metabolic cost or open-ended
  evolution. With respect to those topics the corpus is a **convenience sample**, and the
  absence of contrary evidence in it means very little.

  `DESIGN.md` §5A is built on two systems known only through a survey ([L21 §13]), with
  neither primary source held, and D017 records it as a bet rather than a finding.

  ~~A round 3 searching *open-ended evolution, artificial ecosystem, endogenous fitness,
  energy-based selection, Avida, Tierra, PolyWorld, Geb* is the correction. Until it runs,
  §5A should be read as unreviewed design rather than as evidence-led.~~ ↻ **Round 3 ran**
  (2026-08-29). Both primaries are now held and read in part ([Y94], [VG05]), and the
  endogenous-selection precedent question (Q7) is answered in part. §5A is no longer
  *unreviewed*. It is *thinly reviewed*, as the round-3 items below say.

- ⚠ **Round 3 discovery** ran through search subagents. Four scoped sweeps were executed by
  subagent models (§3.6, §8), then screened and synthesised by the reviewing model.

  That repeats round 1's database-protocol deviation and adds a relay. The reviewer saw the
  agents' reports rather than the raw result lists, so recall is unmeasurable and pool
  coverage depends on four prompt framings.

  The mitigation is that every claim that entered the synthesis or changed `DESIGN.md` was
  re-verified by the reviewer against the downloaded primary text ([Y94] [MC25] [CO02]
  [CB18]). Claims resting on scout-only reads are marked as such in §6.

- ⚠ **Several round-3 claims** rest on abstract-level reads. That covers [GOY23],
  [ST00], and all of the trophic-conditions leads, which are Drossel, Hamm and Fritsch.
  Each was read at abstract level or through one open full text.

  None of these has yet informed a design mechanism, and each must be read in full before it
  does. That is the same rule `research/early-life/` already applies to itself.

- ⚠ **Round 4 repeats** the subagent relay, with a stronger mitigation. Two scoped sweeps
  (§3.7) executed discovery, retrieval and first-pass extraction. The reviewer again saw
  reports rather than raw result lists, so recall is again unmeasurable.

  The mitigation is that the four claims carrying the round's design impact were re-located
  and re-read by the reviewer in the extracted `source.md` files. Those are [KR13]'s R\*,
  [MO04]'s stability verdict, [RMF07]'s critical mobility and wavelength, and [JN97]'s
  persistence times. The sweep agents' own syntheses are reproduced in the session record,
  and only reviewer-checked claims entered this document's tables.

- **Round 5 dropped the relay** and narrowed the search. Discovery, retrieval and extraction
  were done by the reviewing model directly, so no agent report stands between the tables
  and the raw results.

  Eleven queries by one reader is a thinner net than four or two parallel sweeps. The round
  also leaned heavily on anchors *named in the commissioning brief*, which is a form of
  confirmation risk. The brief listed Baines & Pace, Nagata, Thornton, Fogg, Teira, Marañón,
  Azam, Fenchel, Lindeman and Pauly & Christensen, and the round largely went and got what
  it could of that list. Nothing was found that the brief had not anticipated, except [ED21]
  and the [PC95] trophic-level correction.

- ⚠ **Forward snowballing** was not performed in round 5. Backward snowballing from the
  fetched set was, and is logged in §3.8. The papers most likely to have been missed are
  post-2015 syntheses citing [BP91], which is the layer a forward snowball would
  surface.

- ⚠ **Round 5's two canonical sources** are both unread. The number this review now
  carries into a design decision, 13% and the 10–20% range, originates in [BP91] and
  Nagata 2000, and **neither was obtained**.

  Three fetched papers agree on the 13%, which is good triangulation on the *value* and no
  check at all on its *derivation*. The underlying 16 studies, their methods and their
  spread are known here only as three sentences of secondary summary.

- ↻ **Round 6 brought the relay back**, four Sonnet sweeps under the round-3 rules, and
  with it round 3's threat: every candidate was screened from a subagent's report, and
  the reviewer read only the seventeen it retrieved. The §3.2 strings were not re-run and
  no forward snowball was done, both logged in §3.9. The physics half of the corpus is
  old (1972 to 1997) and the animal half is one school (Jékely, Keijzer, Godfrey-Smith,
  four of the seventeen); a dissenting account of early nervous systems was not sought.

- ⚠ **Round 7 searched for what it already knew of.** Thirty-one of its 43 queries named a
  particular work. The pool therefore leaned on anchors known before the search, which is
  round 5's confirmation risk again. The §3.2 strings were not re-run and no forward
  snowball was done (§3.10). The search tool reports links rather than database counts,
  as in every round since round 1.

  The artificial-life sub-question came back empty. No study was found of evolved storage
  or bet-hedging under seasonal plus stochastic resource input. Pass 1 could not tell a gap
  in the field from a gap in the search.

- ⚠ **Round 7 was filed at one remove.** The session that filed it here saw only the
  round's working files, and none of the raw results, sources or full texts. It added no
  claim that the files do not hold. It also had no way to re-read a passage and check a
  claim against it. That is round 3's relay threat in a new place, between the reader and
  the filer.

- ⚠ **Round 8's keyword search leaned on one database.** Semantic Scholar refused 7 of its
  10 keyword attempts with HTTP 429. Discovery therefore ran mostly through OpenAlex's
  relevance and citation-count sorting, and recall is unmeasured. The §3.2 strings were not
  re-run, and the forward snowball reached five anchors only (§3.11).

- ⚠ **Round 8's second pass was briefed by its first.** Pass 2 re-appraised pass 1, which is
  round 5's confirmation risk in a new form. Twelve of its sources were new to the round:
  [COL21], [PER19], [KK16], [WER15], [KAT13], [CAR92], [RM19], [SWO21], [LC85], [MR87],
  [GT22] and [ROG24]. Pass 1 read several full texts through a summarising tool, which is a
  relay between source and reader. Its unverified claims stay out of the synthesis.

- ⚠ **Round 8 was filed a month late, at one remove.** The filer saw the two reports, the
  logs and the fourteen PDFs, and none of the raw API responses or article pages. It
  re-located the page-cited claims in thirteen PDFs. Claims from article pages and
  abstracts could not be re-checked.

- **Round 8 found no artificial-life precedent.** No evolvable-lifespan model from an
  artificial-life venue turned up, so Q13's model evidence is theoretical biology and
  statistical physics. Human trauma epidemiology beyond one query, plant and clonal
  senescence, and sex differences in ageing were not searched.

- ⚠ **Round 9 searched the web at one remove.** Pass 1 went back to the web search tool,
  first page only, with no counts, and its query list was not kept. Its first report read
  snippets. The owner chose the twenty-one from a supportive brief that asked for a hunt for
  counter-evidence. The machine-learning curriculum papers were pooled and not fetched, so
  the counter-evidence on curricula is thinner than the support. The §3.2 strings were not
  re-run and no forward snowball was done (§3.12).

- **Round 9 found no transfer study.** No paper moved an evolved sensing-and-steering brain
  from an easier tank to a harder one. The nearest are [CA16]'s rising noise on one lineage,
  [PJ10]'s walkers given vision, and [JAK97]'s transfer to real robots.

- ⚠ **Round 10 read through a summariser.** Every full text reached the reader as the fetch
  tool's answer to a prompt, not as the page's own text. That is round 8's pass-1 relay again.
  Quotations and numbers in round 10's rows are the tool's report. A claim can be re-checked only
  by reading the open copy that `FETCH-RESULTS.md` names.

- ⚠ **Round 10 could not read PDFs.** The tool stored the first two it fetched and could not read
  them, so no PDF was requested after that. NEAT's 2002 paper [SM02], FS-NEAT [WSSMK05], Ijspeert's
  2008 review, Sims 1994 and LeCun's initialisation all have open PDF copies, and none was read.
  The sources D146 and D147 most rest on are therefore at the abstract or unread.

- ⚠ **Round 10 searched for what it knew.** Nine of its fourteen queries named a work, two of them
  from the reader's own knowledge rather than pass 1. Five were framed by the question, as pass 1
  asked for Q15(b) and (d). The §3.2 strings were not re-run and no forward snowball was done.

- **Round 10 found no Sims-style comparison.** No study compared one brain with per-part brains in
  a rigid articulated creature. The comparisons held are on servo modules, soft robots and 2D
  creatures. How animals divide control between a centre and their limbs was not searched.

### 7.2 Verification validity

- ⚠ **The two-database cross-match rule** was not met. Verification used **CrossRef
  alone** for 8 records, rather than the required two independent databases.

  The partial mitigation is stronger than a second database in one respect. The retrieving
  agent independently confirmed title and authorship **against page 1 of each fetched PDF**,
  and all 8 were obtained and inspected. So every cited work is confirmed to exist in the
  strongest sense available, which is that it was read. The formal protocol was still not
  followed.

- **Records cited through** other papers' reference lists were **not verified at all**.
  Those are Cheney et al. 2018, Pugh et al. 2016, Lehman & Stanley 2011, Cully & Demiris
  2018 and others, quarantined in `DESIGN.md` §13.4 and flagged as leads.

  ~~Two of them are load-bearing for design sections §2 and §8.3, which is a genuine
  weakness.~~ ↻ The two load-bearing entries were verified in round 3. CrossRef metadata was
  confirmed, and open copies were retrieved and read at the load-bearing passages. Both were
  promoted to the corpus as [CB18] and [PU16]. The remaining §13.4 entries stay
  quarantined.

- ↻ **Round 5 met this rule** better than any previous round, and still not fully. All eight
  round-5 records, the seven fetched plus the unfetchable [BP91], were cross-checked against
  **CrossRef** on title, volume, issue, pages, year and retraction status, with **no
  retractions**. The seven fetched ones were also independently confirmed against their own
  title pages. That is still one database rather than the required two.

  The cross-check earned its keep in both directions. CrossRef confirmed [PWAH07]'s DOI and
  pagination, which had been inferred. The PDFs corrected two search-index errors, both in
  §4's metadata notes: a transposed [LS13] title and a misattributed [CH20] first author.

- ⚠ **Round 6 did not run CrossRef.** Each record was checked against its own PDF's title
  page and read at the passage cited, which catches a wrong paper and not a wrong volume
  or a retraction. The next round that touches these keys should run the batch.

- ↻ **Round 7 met the two-database rule** for all ten records, which no earlier round did.
  Each was matched in at least two of CrossRef, OpenAlex, Europe PMC, Semantic Scholar and
  the bioRxiv API, and none carries a retraction, correction or expression of concern.

- ⚠ **Round 7 kept no copy.** No PDF was saved, so the round has no hash, no `source.md`
  and no page mapping. Its locators are sections and figures (§6). A claim can be
  re-checked only by fetching an open copy again, and that copy may move or close. The
  claims taken from [WYRW25] are the preprint's and may differ in the journal. The design
  attributed to [TGC17] is read in the same group's 2019 paper, since the 2017 paper itself
  was read only at the abstract.

- ⚠ **Most packages have no extraction manifest** (recorded 2026-09-12, from the Astra
  review). Eight of the fifty-four packages, the round-1 and round-2 corpus, carry a
  `manifest.json` naming the tool, its version and the command that turned the PDF into
  `source.md`; the forty-six from rounds 3 to 6 do not. The PDF hashes in
  `FETCH-RESULTS.md` identify the papers and do not reproduce the extracted text or its
  page mapping, so a page-anchored claim is verifiable against the PDF and not against a
  re-extraction. Seven round-4 packages have no PDF on disk at all and are held as
  extracted text only. Each package gains a manifest when it is next touched; until then
  this is a limit on the page mapping, not on the paper's identity.

- ↻ **Round 8 met the two-database rule.** CrossRef and OpenAlex matched every included
  DOI, and CrossRef's update records show no retraction. They show an addendum to [BOO14],
  whose results are unchanged.

- ⚠ **Round 8's text was extracted only at filing.** Each of the fourteen PDFs got its
  `source.md` on 2026-10-31, a month after the round read them, and no extraction manifest.
  The check against it corrected one locator (§4). The nine article-page reads carry no
  pages at all.

- ⚠ **Round 9 ran no database check.** Each record was matched against its own first page,
  as in round 6. No DOI was matched at a database and no retraction check was run. Two of
  the core papers, [WLCS19] and [BEJ25], are preprints, and [LST11] is the authors' layout.

- ↻ **Round 9's packages carry an extraction manifest.** All twenty were built by
  `pdf-clean-markdown` with a `manifest.json`, so twenty-eight packages now have one. Three are
  scans whose OCR is rough: [GM97], [JAK97] and [BB72].

- ↻ **Round 10 met the two-database rule for seven of its eight core records.** Each was matched
  in two of CrossRef, OpenAlex and PMC, and CrossRef lists no update for any of them. [WSSMK05]
  was matched at CrossRef only. Among the supporting sources, [HMP20], [SUCH17] and [MM15] are
  arXiv records with no DOI checked, and [SAO22] was matched at OpenAlex only.

- ⚠ **Round 10 kept no copy**, as round 7 did. Its locators are HTML sections, and two pages were
  read only to 100,000 characters ([IB13], [SBRB23]). A claim outside the read part of either
  page is not this review's.

### 7.3 Synthesis validity

- The n is small. ~~Eleven papers inform the synthesis (five from rounds 1–2, six added in
  round 3);~~ ~~↻ **Twenty-one after round 4** (ten added 2026-09-01).~~

  ~~↻ **Twenty-eight after round 5**, with seven added on 2026-09-03. Two of those seven were
  read in full, so five are read in full across the whole corpus, up from three.~~ ~~↻
  **Forty-five after round 6**, with seventeen added on 2026-09-07 and none of them read in
  full, so five are still read in full across the whole corpus (the bullet below carries
  the round's own note; this one was left unstruck until 2026-09-12).~~ ~~↻ **Fifty-five
  after round 7**, with ten added on 2026-10-06. Five of the ten were read in full, so ten
  are read in full across the whole corpus. Round 7's other five were read at the
  abstract.~~ ~~↻ **Sixty-nine after round 8**, with fourteen added to the matrix on
  2026-10-31 from a search of 2026-09-29. None of the fourteen was read in full, so ten are
  still read in full across the whole corpus. Eight were read at cited PDF pages, three as
  article pages and three at the abstract. Twenty-five supporting sources are cited outside
  the matrix, thirteen of them at the abstract only.~~ ~~↻ **Eighty-four after round 9**, with
  fifteen added to the matrix on 2026-10-31 from a search of 2026-09-29. All fifteen were read
  at cited PDF pages and none in full, so ten are still read in full across the corpus.
  Thirty supporting sources are cited outside the matrix, five of them from round 9.~~
  ↻ **Ninety-two after round 10**, with eight added to the matrix on 2026-10-31.
  None of the eight was read in full from text the reviewer saw. Five were read as HTML full
  texts through the fetch tool's summariser and three at the abstract, so ten
  are still read in full across the whole corpus. Eight supporting sources are cited outside the
  matrix, three of them at the abstract only.

  The round-3, round-4 and most round-5 additions were read at targeted passages against
  specific claims. That is honest for provenance-checking and weaker than a full read for
  anything else. A paper admitted this way could contradict this design somewhere nobody
  looked. Round 7's five abstract-only papers are weaker still. Nothing beyond the abstract
  was read, so their conditions and limitations are unknown here.

- ⚠ **Round 4's key theme primaries** are cited through secondaries. Rosenzweig 1971,
  Maynard Smith 1974, Briggs & Hoopes 2004, McNair 1986 and Sih 1987 are all closed-access
  and enter only via [RC07], [KR13] and an NCBI abstract. The load-bearing *mechanisms*
  are carried by fetched papers, but the historical attributions are second-hand.

- ⚠ **Two known counterexamples** to Q9's spatial answer were surfaced and could not be
  fetched. Saxer, Doebeli & Travisano 2009 (*Proc. R. Soc. B* 276:2065) found spatial
  structure *reducing* diversity where coexistence rests on cross-feeding. Blasius et al.
  2020 (*Nature* 577:226) ran a **well-mixed** chemostat predator–prey system that persisted
  about 2,000 days.

  Both are recorded as threats. Well-mixed is not automatically fatal, and structure is not
  automatically benign. Any round-8 design leaning on Q9(b) should state both.

- ⚠ **McNair 1986's dissent** on refuges is unread. Its abstract claims some refuge
  formulations *create* large-amplitude oscillations in systems that would otherwise damp.
  If a refuge-family knob survives into round 8, this paper moves up the fetch queue.

- ⚠ **Round 5's central quantity** is contested inside the corpus, and the disagreement
  is not noise. Whether PER rises under nutrient limitation is asserted by [LS13 p.1]
  and by [LS11]'s own cruise data. It is denied by [MCP05 p.9] across a 150-fold
  productivity range. It is denied again by [LS11 p.7] on its own pooled dataset, in the
  same paragraph that affirms it for the cruise.

  And **three of the four PER papers obtained come from one laboratory**, Marañón's group at
  Vigo ([MCP05], [LS11], [LS13]). That is a real independence problem for a range this
  review is about to hand to a design decision.

  [CH20] is the only fully independent field confirmation, and it lands at the high end,
  40.8%. The unread [BP91] sits at the low end, 13%.

  The recommended range is therefore bracketed by the two sources this round is *least* able
  to vouch for.

- ⚠ **A culture number** and a field number differ ten-fold and both are correct. [LS13
  p.1] measures ~2% in culture; the field assemblages measure 20–40%. Which one a simulated
  producer resembles is a modelling judgement this review cannot make. A world rule citing
  "the literature says 10–20%" is citing the *field* number.

- ⚠ **The 1% comparison** in D070 was made against the wrong step. Pauly & Christensen's
  10% is TL2→4 [PC95 p.3], and the producer→consumer step is 13% [ED21 p.14]. The
  diagnosis survives, since 1% against 13% is worse rather than better. The citation as
  originally reached for would not have supported it.

- ⚠ **Substrate mismatch** is the dominant threat. [EA23] and [C18] are the two papers
  driving the largest design changes. Both use **soft voxel robots**, while the design
  targets **rigid articulated bodies**.

  The premature-convergence mechanism and the fluid-model limitations are argued to be
  substrate-independent. The specific algorithm rankings and quantitative results are not
  demonstrated on this substrate. `DESIGN.md` §8.1 flags this inline, and it bears
  restating. *The strongest evidence here comes from a different kind of creature.*

- **One reviewer did everything**, so there is single-reviewer bias. There was no
  independent screening, extraction or appraisal. Inclusion, interpretation and emphasis
  reflect one reviewer's judgement, oriented toward the design's open questions rather
  than toward the field's own priorities.

- **Confirmation risk** is partly mitigated. The review was commissioned to test a
  design its author wrote. Three of the findings ran against that design: the omitted
  failure mode, the fluid-model diversity cost, and the effector scheme. That is weak
  evidence against systematic confirmation bias rather than proof of its absence.

- **Formal appraisal checklists** were not applied. No CASP, JBI or AMSTAR instrument
  was used, and quality assessment was venue-based plus reading.

- **Grey literature** was admitted and then went unused. The MLR protocol was chosen to
  capture practitioner knowledge about reproducing Sims. Q2 remains unanswered and no
  grey literature was ultimately consulted, so the main justification for the MLR
  framing was never exercised.

- ↻ **Forty-five after round 6**, with seventeen added on 2026-09-07, none of them read in
  full. The round's answer rests on passages, and on passages that agree across four
  independent literatures, which is the mitigation.
- ⚠ **Round 6's artificial-life finding is an absence.** No fetched paper names the
  movement-before-sensing dilemma as its thesis; the framing is this project's own and is
  marked as inference wherever it is used.

- ⚠ **None of round 7's ten papers modelled round 57's world.** Every link from a paper to a
  prediction is the round's inference, and two differences recur. Round 57's bodies cannot
  sense the light, while [FDT11]'s optimum responds to a sensed environment and
  [BWWR15]'s plasticity needs a cue. Round 57 also imposes a breeding gate, which is a
  minimum investment that [FDT11] assumed away.

- ⚠ **One of round 57's clauses rests on an abstract.** Clause T, that births move to the
  first day of summer, rests on [VJTF09], and only its abstract was read. Clause R leans on
  it too, beside [FDT11].

- ⚠ **The weather's autocorrelation has a contested direction.** More positive
  autocorrelation always raised allocation to survival in [MK07] [Results, Fig. 2c;
  Discussion]. [WTV18]'s abstract finds stress tolerance disfavoured in redder
  environments. If a larger reserve is read as survival or tolerance, the two predict
  opposite effects of round 57's lag-1 correlation of 0.5. Both rest on a trade-off with
  reproduction, so the answer may turn on round 57's own trade-off between the gate's
  height and the breeding rate. That last step is the round's inference.

- ⚠ **The timescales do not match.** The runs of [PW21] last five to six generations and
  describe transients. The fractions in [GSS14] may not have reached equilibrium. The runs
  of [BWWR15] last 50,000 generations. Round 57 runs about fifty generations and may
  reach none of the equilibria the theory describes. The "stochastic" regime of [PW21]
  also randomises only the timing of disturbances. Its finding that periodicity outweighs
  stochasticity therefore speaks to a different kind of noise from round 57's weather.

- ⚠ **Round 8's numbers for the design are its own arithmetic.** The doubling fractions,
  about 0.10 for humans and 0.25 for the mouse, rest on a laboratory median pass 2 assumed.
  The birth push's multiple and the gene's mutational spread are pass 2's own unit
  conversions. None is a measured value for a world of this kind.

- ⚠ **The gene acts on the axis where the evidence finds least lifespan variation.** Between
  mammal species and within primate genera, lifespan varies through onset and early or
  age-independent mortality [PER19 p.3; COL21 p.1, p.3]. D128 stretches the rate with the
  base. The comparative evidence is between species while the gene varies within a
  population, and [COL21] is the nearest within-lineage test.

- ⚠ **The birth push rests on birds and one laboratory.** [BOO14] and [BBV20] share a design
  and are not independent, and [BBV20]'s standard errors are wide. Across birds the cost is
  small and the within-species landscape flat [WIN25 p.1, p.3]. A cricket study found no
  trade-off with survival and a weak one with senescence [RM19 p.1].

- ⚠ **The wound push has no comparative source.** [CAR92]'s abstract gives no effect size.
  In [KAT13] the effect saturated at four strikes, where this world's pushes add without
  bound. The effect may also be acute rather than an advance of age [BOO14 abstract].

- ⚠ **No source gives the repair exponent.** The theory needs it above zero [KR91
  abstract]. Its value of 0.5 leans on pass 1's fly elasticities, whose sources pass 2 did
  not re-read (§9 item 14). [MC19 p.1] and [COH20] contest the energy trade-off it prices.

- ⚠ **The shape evidence mixes captive and wild data.** [PER19] and [COL21] lean on zoo and
  captive records. A fitted Gompertz rate is also confounded with initial mortality [RS02
  abstract; KIR15 HTML].

- ⚠ **No paper tested the nursery's move.** None bred a sensing-and-steering brain in an easier
  tank under an intake score and moved it to a harder one. Every link from Q14's papers to the
  nursery is the round's inference.

- ⚠ **Every embodied success shaped its score.** [CA16] added a locomotion term, [PJ10] scored
  speed toward the light and [GEOP08] paid each step closer. D134's line between shaping the
  world and shaping the score is therefore untested on bodies. [LOPA03]'s simpler rewards are
  shaping by D134's terms too.

- ⚠ **The round's papers come from a few groups.** Stanley or Lehman co-wrote [LST11],
  [WLCS19], [LM14], [WS11] and [TEN26]. Miikkulainen co-wrote [GM97], [LFM13] and [LM14], and
  Ofria co-wrote [GEOP08], [LOPA03] and [CLWO13]. The case against fixed objectives is mostly
  one school's.

- ⚠ **Replicates are few and substrates far.** [WLCS19] ran five ES runs a comparison, [BEJ25]
  four seeds a size, and [CA16] chose founders by eye. Most substrates are grids, logic
  circuits or 2D. Only [CA16], [PJ10] and [LFM13] used 3D articulated bodies.

- ⚠ **Q15's controller evidence comes from other bodies.** [KGV22] used servo modules, [MEC23]
  soft voxel robots, [VSG23] 2D creatures and [HMP20] planar MuJoCo agents trained by
  reinforcement learning. The substrate threat above applies, and none tested a body that keeps
  one brain while limbs come and go.

- ⚠ **The nursery's fixed bodies made Q15(b) look deferrable.** D146 staged the identity problem
  on the premise that the nursery's bodies never change. D178 later let the nursery evolve bodies
  as well as brains. Whether that premise still holds is the coordinator's to check; this review
  read no run.

- ⚠ **The klinotaxis circuits move by an imposed oscillator.** [IL10] and [IB13] sample space by a
  sine-wave head sweep, which this world's bodies do not have. Their lesson about where the change
  in concentration is computed transfers; their success rates may not.

- ⚠ **Three of the eight core papers are abstracts.** [SM02], [MEC23] and [WSSMK05] each bear
  directly on a ruled choice in D146 or D147, and none was read past the abstract.

## 8. AI assistance disclosure (PRISMA-trAIce)

| Stage | Performed by | Human oversight |
|---|---|---|
| Question framing (PICOC), review-type selection | Claude Opus 5 | User set priorities and approved scope |
| Search string construction and execution | Claude Opus 5 | User approved the six ranked questions |
| Candidate screening (120 → 28) | Claude Opus 5 | User reviewed the candidate pool at checkpoint |
| Metadata verification (CrossRef) | Claude Opus 5 | — |
| Paper retrieval | Separate browser-capable agent, on the user's authenticated institutional session | User supplied credentials and ran the session |
| Text and figure extraction | PyMuPDF via `pdf-clean-markdown` | — |
| Reading, claim extraction, synthesis | Claude Opus 5 | User directed effort allocation at three decision points |
| This report | Claude Opus 5 | — |
| **Round 3** (2026-08-29): search execution | 4 parallel Claude Sonnet subagents (scoped briefs; read-only web; Semantic Scholar + CrossRef public APIs) | User authorised the round and its autonomy level |
| Round 3: screening, primary-text verification, synthesis, this update | Claude Fable 5 | Screening checkpoints recorded in-document rather than user-gated, at the user's request; paywalled retrievals queued for the user rather than attempted |
| Round 3: paper retrieval | Claude Fable 5, open-access sources only — **no institutional access used** | — |
| **Round 4** (2026-09-01): search + retrieval + first-pass extraction | 2 parallel Claude Opus subagents (scoped briefs; read-only web; open-access only; bot-gates respected, not bypassed) | Round run autonomously under the standing goal; the triggering hypothesis (whole-layer access) is the owner's, raised in discussion |
| Round 4: screening, load-bearing-claim re-verification, synthesis, this update | Claude Fable 5 | Four design-impacting claims re-verified against extracted text before entering any table |
| **Round 5** (2026-09-03): search, retrieval, extraction, verification, synthesis, this update | Claude Fable 5.1 via subagent — **no search subagents**, one model end to end; open-access only, bot-gates recorded and not bypassed; no institutional access | Round commissioned by the owner with the question fixed by `DECISIONS.md` D070 and the fraction reserved as an owner decision; the review reports a range, it does not set the world rule |
| **Round 6** (2026-09-07): search | 4 parallel Claude Sonnet subagents (scoped briefs; read-only web; no login, no gate bypassed; no file written) | Round asked for by the owner in their own words; the question and the sweeps' themes were the reviewer's framing of it |
| Round 6: screening, retrieval, extraction, page verification, synthesis, this update | Claude Fable 5.1 (the reviewing model); Europe PMC's public endpoints and author self-archives only, no institutional access | The design consequence is a proposal to the owner (`fable-propose-motility.md` (absorbed into D083 and deleted on ruling)), not a change |
| **Round 7** (2026-10-05 and 06): search, screening, verification, reading, extraction | An AI agent session; the round's working files do not name the model. Web search tool; Europe PMC, CrossRef, OpenAlex, Semantic Scholar and bioRxiv public APIs; legal open copies only, gates recorded and not bypassed, no institutional access, no PDF saved | Commissioned to back the predictions of experiment round 57's pre-registration; the extraction marks every mapping to round 57 as the agent's inference |
| Round 7: filing into this review (§0, §2, §3.10, §4, §5, §6, §7, §8, §9) and `FETCH-RESULTS.md` | Claude Opus 5.5, from the round's working files alone; no search, fetch or reading of sources | — |
| **Round 8** (2026-09-29): search, retrieval and appraisal, in two passes | Two Claude Opus 5.5 research subagents. Pass 1: web fetch and Europe PMC, nothing saved. Pass 2: OpenAlex, Semantic Scholar and CrossRef public APIs with no key; open copies only, gates recorded and not bypassed; no institutional access | Commissioned for D128's values and D130; the owner ruled the values from pass 1 the same day |
| Round 8: filing into this review (§0, §2, §3.11, §4, §5, §6, §7, §8, §9), `FETCH-RESULTS.md` and `DESIGN.md` §0z | Claude Opus 5.5, from the round's reports and logs and the fourteen saved PDFs; no web search or fetch | — |
| **Round 9** (2026-09-29): first report, search and candidate pool | Claude Sonnet research agents, web search and fetch; nothing saved | Commissioned by the owner's question on D134; the owner kept twenty-one of the pool |
| Round 9 (2026-09-30): retrieval and extraction | A Claude Sonnet session with a browser; the owner signed in to each publisher, and no credential was seen or stored; no gate bypassed; `pdf-clean-markdown` | The owner approved one third-party course copy, [LST11] |
| Round 9 (2026-10-31): pass 2 reading and appraisal, and a draft of this filing | Claude Opus 5.5 subagent, from the twenty `source.md` files and the round's working files; no web search, fetch or database lookup | — |
| Round 9 (2026-10-31): filing into this review (§0, §2, §3.12, §4, §5, §6, §7, §8, §9), `FETCH-RESULTS.md` and `DESIGN.md` §0z2 | Claude Opus 5.5, from pass 2's draft and the round's files; the fetch block reordered and renumbered; no web search or fetch | — |
| **Round 10, pass 1** (2026-10-01): candidate search | A Claude Sonnet subagent with web search; nothing opened, nothing saved | Commissioned for D146's open literature round |
| **Round 10, pass 2** (2026-10-31): search, screening, verification, reading, extraction and this draft | One Claude Opus 5.5 research subagent. Web search; a fetch tool that returns a small model's answer about a page; OpenAlex, Semantic Scholar and CrossRef public APIs with no key; open copies only, gates recorded and not bypassed, no institutional access; no copy kept on purpose, though the fetch tool stored two unread PDFs outside the repository | Commissioned by the project coordinator; the session that files the draft saves it. One OpenAlex response carried text addressed to an AI agent; it was not acted on |
| Round 10 (2026-10-31): filing into this review (§0, §2, §3.13, §4, §5, §6, §7, §8, §9), `FETCH-RESULTS.md` and `DESIGN.md` §0z3 | Claude Opus 5.5, from pass 2's draft; its counts recounted and its readings of D146 marked as inference; no web search or fetch | — |

No citation in this document was generated from model memory. Every reference originates
from a tool-call result within the session. ~~Every cited work was retrieved and read except
[BP91], marked NOT OBTAINED where it is cited and used only through three fetched papers
that report its figure, and the works of §13.4, seen only inside other papers'
bibliographies (corrected 2026-09-07).~~ ↻ Every cited work was retrieved and read except
[BP91], marked NOT OBTAINED where it is cited and used only through three fetched papers
that report its figure, and the works of §13.4, seen only inside other papers'
bibliographies (corrected 2026-09-07). Round 7 adds two exceptions. Five of its papers were
read at the abstract only, and each is marked so where it is cited. Its full texts were read
and not saved. Its references came from tool-call results recorded in the round's working
files, and the session that filed them here made no tool call to any source.

Round 8 adds three more. Sixteen of its sources were read at the abstract only and nine as
article pages without page numbers, each marked so where cited. Twenty-one pass-1 sources
were never re-read and stay leads.

Round 9 adds two more. Soros & Stanley 2014 was not obtained and is a lead only. The first
report's snippet-level claims enter only where pass 2 re-read them in a fetched paper.

Round 10 adds two more. Every full text it read reached the reader through a summarising fetch
tool, so its quotations are that tool's report. Six sources were read at the abstract only, each
marked so where cited.

Page-level locators in `DESIGN.md` refer to PDF page numbers.

Each matches the `### Page N` heading in that paper's extracted `source.md`.

Retrieved PDFs and their extractions are not committed. They are copyrighted publisher
material, and ~~two were obtained through institutional access~~ ↻ twelve were obtained
through institutional access, two in round 1 and ten in round 9.

[`FETCH-RESULTS.md`](FETCH-RESULTS.md) records the exact retrieval URL for each, so the set
is reconstructible by anyone with equivalent access.

## 9. Gaps and recommended next steps

Several questions are answered well enough to build on. Those are Q1, with [MC25]'s
sharpening, and Q3, Q4 and Q6. Q7 is answered in its precedent half. Q9 is answered in its
theory half, from round 4, and the world's own answer to it is experimental. Q10 is answered
by round 5, with the independence caveat in §7.3. Q11 is answered by round 6, across four
literatures that agree, and its consequence for the design is a proposal rather than a rule.
Q12 is answered in theory by round 7. Its transfer to this world is inference, and
experiment round 57 is the world's own answer.

Q13 is answered in part by round 8. Most of its values are judgement, and the world's own
answer is experiment round 52, whose two seeds moved the gene in opposite directions (D132).

Q14 is answered in part by round 9. The literature names the nursery's move and records it
working, but always with a shaped score. No paper tests the transfer the nursery makes.

Q15 is answered in part by round 10. Its controller evidence is on other bodies, and the sources
D146 and D147 most rest on were read at the abstract or not at all.

Four items were resolved by round 3, and are kept here rather than deleted.

1. ~~**The 2025 co-optimisation preprint** was surfaced in Pass 1 and never followed up.~~
   It was found, retrieved and read in part, and is now [MC25]. It does challenge §2.3's
   mitigations: even explicit innovation protection selected near-optimal morphologies in
   22% of trials or fewer against ground truth. A full read is still outstanding, below.

2. ~~**⚠ NEW in round 2: open-ended evolution and artificial ecosystems**, never searched.~~
   Now searched (§3.6). The precedent half of Q7 is answered, and the
   emergent-trophic-structure half has leads rather than held sources. One specific answer
   came back, in [Y94 p.7]. **Yes, there is precedent** for a per-neuron metabolic charge.

3. ~~**Verify the §13.4 quarantine**, meaning its two load-bearing entries.~~ Done, as
   [CB18] and [PU16].

4. ~~**Forward snowballing from [EA23] and [C18]**.~~ Done, with the two field-shape
   findings recorded in §7.1.

The list was re-prioritised on 2026-09-06. The items below keep their numbers, because other
sections of this document and `FETCH-RESULTS.md` cite them by number, so this paragraph sets
the order the next round reads them in.

Three questions are in front of the project, and each one sends the round to items already
on the list. No paper was added, no round row was touched, and no claim changed.

- **The first question** is the open-flow matter balance. Matter enters the world at a
  vent and leaves by burial (DECISIONS.md D074), and no round has yet watched the
  standing stock level off and stay level. D079 now returns that budget to the world one
  change at a time.

  From the literature, the round needs to know what sets the equilibrium between a source
  and a sink, and how long a system takes to reach one. It also needs to know what a late
  equilibrium looks like in the field. Item 4 holds the nearest sources, and item 8's
  recycling entries sit beside them.

  Item 4's own trigger is gone. It asked to be read before the remineralisation decision,
  which was ruled and then measured redundant (D051, logbook/0036). The mechanism that
  replaced it was decided on this review's round 5 (D070). Read item 4 for the balance
  question instead.

- **The second question** is contact feeding. Creatures share one box and touch (D077), and
  predation on contact is a proposal in front of the owner.

  The round needs consumer–resource coexistence when eating requires contact, what handling
  time does to it, and how much of a prey's tissue a predator actually gets. Item 7's
  cohort-cycle theme is the closest held line, and item 8's Kerr 2002 plate-versus-flask
  contrast is the closest empirical one.

- **The third question** is movement-assisted foraging. Movement has never paid its
  energy cost here, and the prize side is the next scored question after the goal
  (DECISIONS.md D075).

  The round needs to know what a mover earns over a drifter when the food is patchy. Items 5
  and 6 cover the encoding half. The ecology half has no held source yet, so it is the one
  place the next round will have to search rather than read.

Items 1, 2, 3, 9, 10, 11 and 12 keep their standing and are read as the three questions
need them.

Added 2026-09-07 from the outside review of 2026-08-31 (logbook/0071): D051 leans on [GOY23]
harder than an abstract-level appraisal supports and is still cited in DESIGN §5A's
remineralisation paragraph, though the mechanism is built and off; a full read of [GOY23]
joins the queue, as does a search on stress-induced mutation and self-adaptation, which D056
names without a review behind it.

Open, in priority order:

1. **Read [MC25]** in full, and decide what it means for §2.3 and §8.4 *under endogenous
   selection*. The question is whether ecological-niche protection suffers the same
   undervaluation mechanism, and how that would be measured here.

2. Implement a Q8 instrument. [ST00]'s permuted-shadow evolutionary activity is the
   candidate that fits the project's logs. It needs a lineage record, so this is coupled to
   the open `lineage.jsonl` decision. Read [BSP98] first, which is paywalled and queued, so
   that the class taxonomy is taken from the source rather than from summaries.

3. **The paywalled and manual fetch queue**, which needs institutional access, in rough
   priority.

   | work | note |
   |---|---|
   | [BSP98] Bedau, Snyder & Packard 1998 | ALife VI, MIT Press |
   | [DOL19] MODES | free but bot-gated at PeerJ, so it needs a manual browser download, or the *Artificial Life* 25(1) version |
   | Bohm, Zhang & Dolson 2024 | ALIFE 2024, the MODES critique |
   | Channon 2019, Channon 2001 | *Artificial Life* 25(2), DOI `10.1162/artl_a_00285`; and ECAL, DOI `10.1007/3-540-44811-X_45` |
   | Egbert, Barandiaran & Di Paolo 2012 | *Artificial Life* 18(1), DOI `10.1162/artl_a_00047` |
   | Taylor et al. 2016 | nominally CC BY, both mirrors bot-gated, DOI `10.1162/artl_a_00210` |
   | Wilke & Chow 2005 | OUP, DOI `10.1093/oso/9780195188165.003.0011` |
   | Ventrella 1998 | ALife VI, MIT Press |

4. **Read the trophic-conditions leads** in full before the remineralisation decision
   entry. They are [GOY23], which is held, Drossel/McKane/Quince 2004 (arXiv
   q-bio/0401025), Hamm & Drossel 2021 (Sci Rep, OA), and Fritsch et al. 2021 (arXiv
   1905.06855). These are what the mechanism's acceptance criteria should be checked
   against.

   *2026-09-06: the remineralisation decision was ruled and then measured redundant (D051,
   logbook/0036). This item's trigger is now the open-flow matter balance above, and it is
   the first thing the next round reads.*

5. **Q2, on Sims reproduction**, is unchanged from round 2. Krčah's GECCO'07
   reimplementation and Lessin's thesis are both openly available and were never
   fetched.

6. **Q5, on controller representation**, is unchanged. Only [K12]'s scheme is held, and
   ~~Lessin's muscle-drive line remains unexplored~~ ↻ Lessin's muscles are held in [LFM13]
   but were read for Q14 only, not appraised for Q5. ↻ Round 10 added comparisons of central
   against per-module control and of oscillator against network controllers, all on other
   bodies (Q15).

7. NEW in round 4: cohort cycles as a full theme. The de Roos & Persson size- and
   stage-structured line describes single-generation cycles, in which one dominant cohort
   grazes the resource to its own starvation. That is the closest published description of
   this project's observed bust, and it was surfaced only as a lead.

   The entry points are Ten Brink & de Roos 2018 (*JTB*, OA at PMC6497215) and Persson et
   al. 1998 (*TPB* 54:270). Promote it to a searched theme before any stabiliser is declared
   to have failed. If the cycles are cohort-driven, neither refuges nor patches address the
   mechanism.

8. NEW in round 4: the Q9 manual-fetch queue, in rough priority.

   | work | note |
   |---|---|
   | Quévreux, Barot & Thébault 2021 | *Oikos*, green OA at HAL `hal-02570657`, bot-gated; **top of the queue** for the balance question, since nutrient recycling reproducing enrichment instability is the nearest theory to this closed world |
   | Jansen 2001 | *TPB* 59:119, nominally bronze at Elsevier, 403 to non-browsers |
   | McNair 1986 | the refuge dissent, see §7.3 |
   | Kerr et al. 2002 | *Nature* 418:171, the empirical plate-versus-flask contrast |
   | Saxer 2009 and Blasius 2020 | the two counterexamples in §7.3 |
   | Bonsall, French & Hassell 2002; Ellner et al. 2001 | |

9. NEW in round 4: the discriminating measurement is blocked on instrumentation. Cycle
   period against consumer generation time, and amplitude against enrichment, separates
   enrichment cycles from cohort cycles, on [RC07 p.4]'s *Daphnia* precedent.

   Neither is computable from current logs. `lineage.jsonl` is empty, and snapshots carry
   bare genomes with no birth metadata. The lineage-events build is therefore a *pre-round-8
   instrument* rather than post-goal housekeeping, and it is this round's concrete
   engineering demand.

10. NEW in round 5: the exudation manual-fetch queue, in priority order.

    | work | why it is wanted |
    |---|---|
    | **[BP91] Baines & Pace 1991**, *L&O* 36:1078, DOI `10.4319/lo.1991.36.6.1078` | the 13% and its 16 underlying studies are currently second-hand; Wiley returns 403 to non-browser clients and the "GREEN" Figshare record is empty |
    | **Thornton 2014**, *Eur. J. Phycol.* 49:20–46, DOI `10.1080/09670262.2013.875596` | the field's standard review, bronze OA at Taylor & Francis and 403 to non-browser clients on both PDF and HTML; it is the source of the growth-phase figures this document deliberately does **not** assert |
    | **Nagata 2000**, chapter 5 in Kirchman, *Microbial Ecology of the Oceans* | the 10–20% range this round recommends comes from it through [CH20 p.5] and has never been read at source |
    | **Azam et al. 1983**, *MEPS* 10:257, and **Cole, Findlay & Pace 1988**, *MEPS* 43:1 | both behind Inter-Research's bot check |
    | **Fenchel 2008** | closed at Elsevier |
    | **Marañón et al. 2004**, *L&O* 49:1652 | the eutrophic half of [MCP05]'s pooled dataset, so the 35-point regression is presently half-verified |
    | **Teira et al. 2001**, *L&O* 46:1370; **Karl et al. 1998** | |
    | **Moran et al. 2022**, *L&O* | hybrid CC BY-NC, Wiley-gated |

11. NEW in round 5: the light response is an open design question rather than a caveat.
    [MCP05 p.1, p.8–9] shows dissolved release roughly independent of irradiance.
    Particulate production is strongly light-dependent.

    A rule that exudes a fixed fraction of *intake* therefore has the wrong depth profile.
    In the ocean, PER is highest at the dim base of the euphotic layer, which is the depth
    band this project's consumers occupy.

    The literature-faithful alternative is a per-biomass release rate that is *independent*
    of instantaneous photosynthesis. Neither form has been tested here, and the choice
    belongs with the world rule.

12. NEW in round 5: exudation is one of at least four inputs to the real DOM pool. Both
    [LS11 p.8] and [CH20 p.1] find bacterial demand exceeding dissolved primary production.
    The gap is closed by sloppy feeding, excretion and viral lysis.

    If D070's screen fails at a literature-faithful fraction, that is the next family to
    search. It also maps onto mechanisms this project already has in adjacent form, since
    D052's excretion moves matter rather than energy.

Two lines are **not worth pursuing** on current evidence. They are deep-RL co-design,
meaning DERL and Evolution Gym, and the CPPN encoding literature. Both address a different
substrate from the one this project targets.

The round-3 snowball reinforced that judgement, since the citation traffic around [C18] is
dominated by that same substrate.

5. **Round 6's manual-fetch queue and one check.** ~~Fraenkel & Gunn 1940 and Berg & Brown
   1972 are the two founding texts of Q11(b) and both are read here only through others;~~
   ↻ Berg & Brown 1972 was obtained in round 9 as [BB72]. Fraenkel & Gunn 1940 is still read
   only through others;
   Wuichet & Zhulin 2010 is the evolutionary evidence for the co-option of metabolic sensors
   and was not fetched. And the "sessile trap" of evolutionary robotics, populations that
   converge on standing still because it is cheaper, surfaced in sweep 4 as a pattern
   without a citation; one verified source for it would turn an inference into a finding.

13. NEW in round 7: the fetch queue for Q12, and its leads. All five in the first table
    were read only at the abstract, and the first of them bears on clause T of round 57's
    pre-registration.

    | work | why it is wanted |
    |---|---|
    | **[VJTF09] Varpe et al. 2009**, *Oikos* 118:363 | clause T rests on its abstract alone; closed |
    | **[WTV18] Wieczynski et al. 2018**, *Am. Nat.* 191:E195 | one side of the autocorrelation dispute; closed, no preprint found |
    | **[TGC17] Tarazona et al. 2017**, *Oikos* 126:1162 | the nearest experimental design to round 57; open copy at the Universitat de València repository (hdl 10550/92697, CC BY-NC) behind a bot challenge |
    | **[SK12] Starrfelt & Kokko 2012**, *Biol. Rev.* 87:742 | Wiley's listed free link returned 403 |
    | **[WYRW25]** journal version, *Am. Nat.* 205:572 | to check the preprint's claims; eScholarship copy (CC BY-NC-ND) behind a firewall challenge |

    Twenty ranked candidates were screened and not carried to verification. Each is a lead,
    read at most at the abstract, and none is in the synthesis. Eighteen are in the table
    below. The other two are Seger & Brockmann 1987 and Philippi & Seger 1989, seen by title
    only. Secondary sources name them as the origin of the conservative and diversified
    kinds of bet-hedging. Eight more were seen and judged weaker for the round. They are
    Venable 2007, Vasseur & Yodzis 2004, Halley 1996, Chapin et al. 1990, Hartmann &
    Trumbore 2016, Paniw et al. 2018, Canino-Koning et al. 2016 and Bornhofen & Lattaud.

    | theme | lead | identifier |
    |---|---|---|
    | bet-hedging theory | Cohen 1966 | *J. Theor. Biol.* 12:119 |
    | bet-hedging theory | Lewontin & Cohen 1969 | DOI `10.1073/pnas.62.4.1056` |
    | bet-hedging theory | Haaland et al. 2019 | DOI `10.1111/evo.13659` |
    | bet-hedging theory | Libby & Ratcliff 2019 | DOI `10.1086/701786` |
    | bet-hedging theory | Kussell & Leibler 2005 | DOI `10.1126/science.1114383` |
    | experiments | Beaumont et al. 2009 | DOI `10.1038/nature08504` |
    | experiments | Simons 2011 | DOI `10.1098/rspb.2011.0176` |
    | experiments | Childs et al. 2010 | DOI `10.1098/rspb.2010.0707` |
    | storage | Iwasa & Kubo 1997 | DOI `10.1023/A:1018483429029` |
    | storage | Blumstein et al. 2022 | DOI `10.1111/nph.18213` |
    | storage | Kivelä et al. 2016 | DOI `10.1002/ece3.2310` |
    | autocorrelation | Ruokolainen et al. 2009 | DOI `10.1016/j.tree.2009.04.009` |
    | autocorrelation | Schwager et al. 2006 | DOI `10.1086/503609` |
    | autocorrelation | Tufto 2015 | DOI `10.1111/evo.12716` |
    | autocorrelation | Rescan et al. 2021 | DOI `10.1371/journal.pgen.1009611` |
    | artificial life | Grove et al. 2020 | DOI `10.1162/isal_a_00284` |
    | artificial life | Grove et al. 2021 | DOI `10.1162/artl_a_00354` |
    | artificial life | Batta & Stephens 2023 | DOI `10.1038/s41598-023-33139-6` |

    The artificial-life gap of §7.1 stands. A study of evolved storage or bet-hedging under
    seasonal plus stochastic resource input would be the nearest precedent for round 57, and
    none was found.

14. NEW in round 8: the fetch queue for Q13, and its leads.

    | work | why it is wanted |
    |---|---|
    | **[KR91] Kirkwood & Rose 1991**, and Kirkwood 1977 | the theory D128 prices, read at the abstract only; no open copy |
    | **[CAR92] Cartar 1992** | the one field experiment on wear and lifespan; no effect size seen |
    | **[BOO14] Boonekamp et al. 2014** | the birth push's second source; the Groningen copy is behind Cloudflare |
    | **[ZBH95] Zwaan et al. 1995** | the within-species cost of long life; no open copy |
    | **[RS02]**, **[SPK05]**, **[COH20]** | OUP, biologists.com and Wiley returned 403 |
    | **[REI22] Reinke et al. 2022** | Kent returned 504, HAL challenged, the Texas Tech copy was empty |
    | Jones et al. 2008; Flatt 2011 | Durham returned 403; rero.ch answered 410 |
    | **[FPW90]**, **[HAM10]**, **[TRA04]** | no open copy found |

    Six leads were named and not appraised: Westendorp & Kirkwood 1998 (*Nature*, DOI
    `10.1038/25519`), Maginnis 2006, Mangel 2008, Dytham & Travis 2006, Jones et al. 2008,
    and Tarkhov et al. 2017 on the Strehler–Mildvan correlation as a fitting artefact.

    Twenty-one pass-1 sources were not re-read and are leads too. They are McDonald 2012,
    Hughes & Hekimi 2016, Khazaeli & Curtsinger 2013, Wit 2013, Parker 2020, Grandison 2009,
    Jenkins 2004, Partridge & Farquhar 1981, Fowler & Partridge 1989, Flatt 2011 and
    Gustafsson & Pärt 1990. The rest are Daan 1996, Pletcher 1998, Houle 1996, Conradsen
    2022, Abrams 1993, Moorad 2019, Reznick 2004, Drenos & Kirkwood 2005, Kirkwood & Holliday
    1979 and Waterlow.

    Two of them carry the theory for any prediction about the gene. Abrams 1993 and Moorad
    et al. 2019 say when extrinsic death changes selection on lifespan, and neither was
    re-read. Read both before such a prediction enters a pre-registration.

    The repair exponent of 0.5 has no source. logbook/0127 set it from fly elasticities in
    three pass-1 sources, Khazaeli & Curtsinger 2013, Wit 2013 and Parker 2020, which pass 2
    never re-read. Any change to the exponent waits on reading those three, or on a source
    that measures what a longer life costs in upkeep.

    The founding texts were named and not retrieved: Gompertz 1825, Kirkwood 1977, Williams
    1957, Hamilton 1966 and Medawar 1952.

15. NEW in round 9: the fetch queue for Q14, and its leads.

    | work | why it is wanted |
    |---|---|
    | **[SS14] Soros & Stanley 2014**, ALIFE 14, pp. 793–800 | conditions for open-ended evolution; the UCF record held no file and a Semantic Scholar mirror answered with a bot check |
    | [WLCS19]'s peer-reviewed version | the preprint was read |
    | a database check of all twenty | none was run (§7.2) |

    Twenty-four pool rows were not kept and are leads, read at most at the abstract. Strand A
    holds Wang et al. 2020 (Enhanced POET), Dharna et al. 2022, Milano & Nolfi 2021, Milano,
    Carvalho & Nolfi 2017, Bongard 2011, Koos, Mouret & Doncieux 2013, Bengio et al. 2009,
    Narvekar et al. 2020, Nolfi & Floreano 1998, Petrovic, a Sussex report on incremental
    evolution, Bongard 2013 and Brant & Stanley 2017. Strand B holds van der Post & Hogeweg,
    Griffith & Yaeger 2006, Wagner et al. 2013, Moore, Clark & McKinley 2013, Komosinski 2000,
    Miconi 2008, Bedau et al. 2000, Hein & McKinley 2012, de Bruin et al. 2024, an untitled
    foraging paper and Sims 1994.

    The first report adds Kashtan & Alon 2005, Black, Bourrat & Rainey 2020, Lehman et al.
    2020, arXiv:2505.22696 on curricula that failed, and Harvey, Husbands & Cliff. Read the
    machine-learning curriculum papers first, since they hold most of the unread
    counter-evidence.

    The transfer gap of §7.1 stands. A study that evolves a sensing-and-steering brain in an
    easier world and moves it to a harder one would be the nearest precedent for the nursery.

16. NEW in round 10: the fetch queue for Q15, and its leads. Each open copy below is a PDF the
    round's tool could not read.

    | work | why it is wanted |
    |---|---|
    | **[SM02] Stanley & Miikkulainen 2002** | D146's growth rests on it; its ablation numbers and its add-node and add-link rates are what D146's guessed rates need. The NNRG lists an author copy (nn.cs.utexas.edu, PubID 114) |
    | **[WSSMK05] Whiteson et al. 2005** | whether its founders start with few inputs, which bears on D147; open copy at gpbib.cs.ucl.ac.uk (GECCO 2005, p1225) |
    | **[MEC23] Mertan & Cheney 2023** | its design and statistics; arXiv 2306.09358 and an NSF PAR copy |
    | Ijspeert 2008, *Neural Networks* 21(4):642–653, DOI `10.1016/j.neunet.2008.03.014` | the CPG review pass 1 named; Infoscience record 125126 rate-limited the fetcher; abstract not read |
    | [IHW99] Ijspeert, Hallam & Willshaw 1999 | read at the abstract; Infoscience record 58486 |
    | [S94] Sims 1994 | still in `DESIGN.md` §13.4; its per-part networks with a global node are pass 1's anchor for Q15(b) |
    | LeCun et al. 1998, "Efficient BackProp" | D147's initialisation source, unread |
    | Beer & Gallagher 1992, *Adaptive Behavior* 1(1):91–122 | CTRNNs evolved for chemotaxis; DOI `10.1177/105971239200100105` from a search summary, unverified; abstract not read |

    Leads named and not appraised: Stanley & Miikkulainen 2004 (*JAIR*, arXiv 1107.0037), Stanley,
    Bryant & Miikkulainen 2005 (rtNEAT), Stanley, D'Ambrosio & Gauci 2009 (HyperNEAT), Risi &
    Stanley 2012 (ES-HyperNEAT, DOI `10.1162/ARTL_a_00071` from a URL), Ijspeert et al. 2007
    (*Science*, DOI `10.1126/science.1138353`), Ekeberg 1993 (*Biol. Cybern.*, DOI
    `10.1007/BF00199436` from a URL), Salimans et al. 2017 (arXiv 1703.03864), Mazzapioda,
    Cangelosi & Nolfi 2009 (IEEE CEC, per-part neural controllers), Whitman, Travers & Choset 2023
    (*IEEE T-RO*, DOI `10.1109/tro.2023.3284362`), and Alonso & Kirkegaard 2024 (*PNAS Nexus*, DOI
    `10.1093/pnasnexus/pgae235`). HyperNEAT and ES-HyperNEAT are CPPN encodings, which this
    section already judges not worth pursuing for rigid bodies.

    Not searched: how animals divide control between a centre and their limbs, and the dynamics
    of small CTRNNs, which bear on whether units without a time constant can hold a trace.
