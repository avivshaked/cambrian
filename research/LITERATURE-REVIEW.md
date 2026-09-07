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
> ~~After round 3, sixteen retrieved papers are held, three read in full,~~ ↻ **after round
> 5, thirty-seven are held** (round 4 added thirteen, round 5 seven). Twenty-eight of those
> inform the synthesis and five were read in full. The rest were read in part, where "in
> part" mostly means *specific claims verified against the primary text* rather than
> cover-to-cover reading.
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
round.

| Round | Date | Trigger | Queries | Papers read | Design changes produced |
|---|---|---|---|---|---|
| **1** | 2026-08-02 | Initial — six ranked questions before implementation | 15 | 3 full, 2 partial | DESIGN.md draft 1→3: added §2 (premature convergence), changed §8.2/§8.3 (selection + multi-BC), corrected §5.4 (fluid model diversity cost), §4.4 (effector scheme), §4.1 (reflection flags), §11.2 (exploit checklist), closed §12.1 (encoding) |
| **2** | 2026-08-02 | New question, raised during implementation: **should morphological and neural complexity carry a metabolic cost?** | 0 new searches — targeted re-reading of the 8 papers already held | **0 new papers.** Extended reading of [L21] (§8.2 p.10, §13 pp.14–16) and [C18] (§2.4 p.8, §3 pp.13/17, §4 p.29); confirmatory passes on [CEA07] §3.4, [TM01] pp.6–8, [K12] §2.2, [CU15] supplementary p.24 | DESIGN.md draft 4→5: added **§5A** (energy economy, endogenous selection), superseded §5.5, demoted §8 to observatory, repurposed §6.3/§6.4. Recorded as `DECISIONS.md` D017 |
| **3** | 2026-08-29 | §9's backlog, overdue by the review's own protocol: §5A implemented and measured for a month with its literature never searched; the 2025 co-optimisation preprint never followed up; forward snowballing never done; §13.4 quarantine unverified | 4 parallel search sweeps (delegated to subagents — §3.2, §8), plus Semantic Scholar citation API for forward snowballing and CrossRef for verification | **8 new papers retrieved** ([Y94] [MC25] [CO02] [VG05] [GOY23] [ST00] [CB18] [PU16]), ~45 candidates surfaced and screened, ~13 citing works triaged; key claims verified against primary text for [Y94] [MC25] [CO02] [CB18] | **§5A.2's "no precedent for a per-neuron charge" corrected** — [Y94 p.7] is the precedent (DESIGN.md §0e). [CB18] and [PU16] verified and promoted out of §13.4. Q1 sharpened by [MC25]; **Q7 answered in part; Q8 opened and answered in principle** (§2). No DESIGN mechanism changed; the round's findings feed the next decisions rather than rewriting existing ones |
| **4** | 2026-09-01 | A design decision built on uncited inference was falsified by experiment: D055's seabed refuge — flagged ⚠ uncited in its own entry — blocked consumer establishment (logbook/0042) and starved hand-placed consumers to extinction (logbook/0043), and the pending round-8 stabiliser decision plus the owner's whole-layer-access hypothesis lean on the same untouched literature | 2 parallel sweeps (delegated — §3.7, §8), 9 declared new themes under a new question (Q9); the §3.2 re-run was skipped as vacuous (3 days since round 3) and is so logged | **13 PDFs fetched** (12 new to the corpus), **10 into the synthesis** ([HUF58] [JN97] [RMF07] [FM15] [HZ13] [KR13] [MO04] [JKT04] [DBWM05] [RC07]); ~25 relevant works recorded closed-access; 4 load-bearing claims re-verified by the reviewer against extracted text | **D055's failure is theory-predicted, not anomalous** — [KR13 p.2]: the consumer's equilibrium needs refuge *plus* accessible break-even density, so a refuge covering the feeding ground deletes the consumer (D055 addendum updated). Q9 opened and answered in theory. Three constraints delivered to the pending round-8 decision: patches must be *unequal* [HZ13 p.5], the criterion is a length-scale ratio not a dispersal rate [RMF07 p.5], and the strong refuge form is fixed-number (≡ type III response) not proportional [KR13 p.1–2]. The period-vs-generation discriminator for the bust mechanism is uncomputable from current logs — **lineage events promoted from post-goal to pre-round-8**. No existing DESIGN mechanism changed |
| **5** | 2026-09-03 | `DECISIONS.md` **D070**, ruled in principle the same day and explicitly gated on this round: the world's producers give nothing to the water while they live, so dead tissue is the detritus pool's only income, the second trophic level is fed at ~1% of primary production (0.2 W against ~17 W) and a consumer line caps out at about six individuals. The proposed world rule — a producer deposits a fraction of photosynthetic intake into the nutrient field each step — needed a *number with a citation* before it could be a rule | 11 web queries in 3 themes, plus the Semantic Scholar graph + batch APIs (10 DOIs screened for open access) and the Figshare API; **the §3.2 strings were not re-run** — 2 days since round 4, a date-filtered re-run is vacuous, logged per §3.5 rule 1 | **7 PDFs fetched, all 7 into the synthesis** ([PC95] [MCP05] [LS11] [LS13] [CH20] [PWAH07] [ED21]); every numeric claim in the tables below was located and re-read by the reviewer in the extracted `source.md`. **6 works recorded bot-gated or closed and not bypassed** — including the field's two canonical sources, Baines & Pace 1991 and Thornton 2014 | **Q10 opened and answered.** The number D070 asked for: percentage extracellular release is **10–20% of primary production as a world-ocean general range** (Nagata 2000, via [CH20 p.5]), a cross-system mean of **13%** ([BP91], reached only through three independent verified secondaries), **~20% and flat** across a 150-fold productivity range [MCP05 p.1, p.9], rising to **37–41%** in oligotrophic water [LS11 p.1; CH20 p.1]. Three corrections to what the design would otherwise have assumed: exudation does **not** scale allometrically with cell size (isometric, slope 0.95 across >7 orders of magnitude of cell volume) and does **not** differ between growth phases [LS13 p.1]; and it is **not** proportional to light — DOCp is irradiance-independent while POCp is not, so real PER is *highest where photosynthesis is lowest* [MCP05 p.1, p.8–9]. Two reference points for the 1% diagnosis: the producer→herbivore step measures **13% (11–17%)** [ED21 p.14], and Pauly & Christensen's famous 10% is measured for **TL2→4 only** [PC95 p.3], so it is the wrong number to compare a producer→consumer step against. No DESIGN mechanism changed; the fraction goes to the owner for D070 |

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
| Q1 | What prevents premature morphological convergence in body–brain co-optimisation? | ✅ **Answered — and sharpened in round 3.** [MC25], with ground truth from an exhaustively-mapped 1.3M-morphology landscape, shows the mechanism precisely: fitness under a co-evolving controller systematically *undervalues* newly-mutated bodies, so promising morphologies are eliminated before their controllers adapt — and even MAP-Elites and explicit innovation protection found near-optimal morphologies in only 17–22% of trials [MC25 p.1, p.29]. The mitigation question is less settled than round 1 concluded |
| Q2 | Why are Sims' 1994 results hard to reproduce? What are the necessary ingredients? | 🟡 **Partial** — [TM01]/[CEA07] read in part (round 2); Krčah's GECCO'07 reimplementation and Lessin's thesis still never fetched |
| Q3 | Direct graph vs CPPN vs grammar encoding? | ✅ **Resolved** |
| Q4 | Which quality-diversity variant? | ✅ **Answered** |
| Q5 | Controller and actuator representation? | 🟡 **Partial** — one implementation's scheme, no comparison |
| Q6 | What physics exploits should be defended against? | ✅ **Answered** (checklist + two case studies) |
| Q7 | *(opened round 2, searched round 3)* Does endogenous / energy-based selection have precedent, and what lets such systems hold multiple strategies and trophic structure instead of collapsing to the cheapest trade? | 🟡 **Answered in part.** Precedent is real and primary-sourced: [Y94] (PolyWorld — per-neuron and per-synapse energy charge, behaviour-priced actions), [VG05] (Gene Pool — locomotion pays because food and mates are the only routes to reproduction), [CO02] (Avida — **depletable** resources produce negative frequency-dependent selection and stable coexistence of up to nine strategies; making the same resources unlimited collapses diversity to one genotype). Trophic emergence conditions from the ecology-modelling side: adaptive prey selection [DROSSEL04 — lead], spatial structure and density thresholds [HAMM21 — lead], closed nutrient cycles [GOY23]. What no held source yet shows: trophic levels emerging from *morphology-encoded* feeding on a physically simulated body — that remains this design's own bet |
| Q8 | *(opened round 3)* What instrument distinguishes adaptive evolution from a treadmill, computable from this project's logs (births with parent ids, deaths, genomes, energy ledgers)? | 🟡 **Answered in principle, not yet implemented.** Bedau–Packard evolutionary activity with the class 1–4 taxonomy (via [BSP98 — lead, unfetched]) is the formal treadmill test; [ST00] supplies an implementation that replaces the "neutral shadow run" with a randomly-permuted shadow population, which fits a system that has no fitness function to switch off. The MODES toolbox ([DOL19 — lead, bot-gated preprint]) is the modern alternative and substitutes a lineage-persistence filter for the shadow. Both need a lineage record, which bears directly on the open `lineage.jsonl` decision |
| Q9 | *(opened round 4)* What stabilises a consumer–resource interaction against boom-and-bust — and which stabiliser fits a world whose only evolved consumer is a benthic filter feeder on a well-mixed detritus pool? | ✅ **Answered in theory; the world's own answer is the round-8 experiment.** The theory predicted this project's own result before it ran: a refuge covering the consumer's feeding ground does not stabilise, it deletes the consumer — the equilibrium needs the refuge *plus* the consumer's break-even accessible density [KR13 p.2], and increasing hidden prey ends in predator extinction (González-Olivares & Ramos-Jiliberto 2003, abstract — closed). Four stabiliser families with primary sources: **(a) refuge, strong form** — fixed-*number* not proportional, which is mathematically a type III functional response [KR13 p.1–2; Maynard Smith 1974 via KR13]; **(b) spatial structure** — works by asynchrony + limited dispersal (empirically: continuous platform dead in 120 days vs eight throttled islands persisting 393–447+ [JN97 p.7]; Huffaker's 120-position universe [HUF58 p.39–41]), but *subdivision alone is the null result* — dispersal can destabilise (Briggs & Hoopes 2004, abstract — closed), identical patches buy nothing [HZ13 p.5], and the operative criterion is a length-scale ratio: coexistence dies when the boom-bust pattern wavelength outgrows the domain [RMF07 p.5]; a growth *gradient* plus mobile grazer stabilises even type I feeding at unbounded carrying capacity [FM15 p.1, p.19] — this project's light gradient is that geometry, rotated; **(c) feeding relaxation at low density** — a type III toe (q=0.1 suffices in food webs [DBWM05 p.12]), noting an unbounded linear clearance is not even a real type I, which has a satiation plateau by definition [JKT04 p.1]; **(d) donor control** — a flux-fed detritus pool with a mass-action consumer is *globally stable* [MO04 p.7], so the observed busts imply this world's pool is not behaving as donor-controlled (closed-loop recycling feeds back — Quévreux 2021, abstract, bot-gated) and/or the cycles are cohort-structural, not dynamical (de Roos & Persson family — lead). **The discriminator (cycle period vs consumer generation time) needs lineage events, which do not exist yet** |
| Q10 | *(opened round 5)* What fraction of its photosynthetic intake does a producer release to the water while alive; what producer→consumer transfer efficiency should a world be judged against; and is the detritus/microbial route a real path for energy to consumers rather than a leak? | ✅ **Answered, with three corrections to the naive form of the rule.** **(a) The fraction.** Percentage extracellular release (PER = dissolved / [dissolved + particulate] primary production) is a normal, continuous process in all growth phases. World-ocean general range **10–20%** (Nagata 2000 via [CH20 p.5]); cross-system mean **13%**, from 16 lacustrine/marine/estuarine studies ([BP91] — **not obtained**, reached through [MCP05 p.9], [LS11 p.7] and [CH20 p.5], all three of which also flag that its glass-fibre-filter methods *underestimate* release); **22 ± 2%** measured in the Celtic Sea and **~20% flat** across a productivity range from <100 to >15,000 mg C m⁻² d⁻¹ (n = 35, r² = 0.90) [MCP05 p.1, p.9]; **~37%** in the ultraoligotrophic Mediterranean [LS11 p.1]; **40.8 ± 12.2%** (integrated range 28.6–60.1%) in the southern East China Sea [CH20 p.1, p.5]; **10 to >35%** in natural assemblages but only **<1–20%, mean ~2%,** in cultures [LS13 p.1]. **(b) Variation.** With *nutrient status*: contested inside one lab — [LS11 p.7–8] finds the oligotrophic contribution higher yet reports that on their pooled dataset "no overall inverse relationship between PER and total primary production exists", while [MCP05 p.9] finds PER flat from eutrophic to oligotrophic. With *growth phase*: no significant difference across three growth stages in 22 species [LS13 p.1]. With *cell size*: **none** — PER uncorrelated with cell size, cell-specific exudation isometric with cell volume (slope 0.95), so "general allometric models cannot be used to predict exudation" [LS13 p.1]. With *light*: DOCp is roughly constant across irradiance while POCp is strongly light-dependent, so PER rises under low light and peaks (>40–50%) at the *base* of the euphotic layer [MCP05 p.1, p.7, p.8–9]. **(c) The reference point.** Producer→herbivore transfer efficiency averages **13% (11–17%)**, and herbivore→fish **10% (7–12%)** [ED21 p.14]; the conventional ~10% [ED21 p.7] traces to [PC95], whose mean of 140 estimates across 48 trophic models is **for TL2→4 and shows no trend with TL** [PC95 p.1, p.3] — it is not a measurement of the producer→consumer step. Ecosystem-scale estimates span <1%–52% [ED21 p.17]. **(d) The detritus/microbial path is real and large.** Of 138 energy channels deconstructed from 40 community food webs, 20% originate with detritus against 63% with a primary producer, "many of which could be traced back to detritus if the description were complete" [MO04 p.3]; most organic matter available to consumers in the ocean is used and respired by bacteria [PWAH07 p.3], and the larger part of all energy captured by marine photosynthesis is ultimately consumed by microorganisms [PWAH07 p.6]. **The caveat that matters for D070:** exudation alone does not feed that loop even in the real ocean — bacterial carbon demand exceeded dissolved primary production **5- to 14-fold** in the Mediterranean [LS11 p.8] and exceeded total primary production at all non-upwelling East China Sea stations [CH20 p.1]; sloppy feeding, excretion and viral lysis supply the rest [LS11 p.8; ED21 p.10] |

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
  Successfully retrieved                                          13/14
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

### 7.3 Synthesis validity

- The n is small. ~~Eleven papers inform the synthesis (five from rounds 1–2, six added in
  round 3);~~ ~~↻ **Twenty-one after round 4** (ten added 2026-09-01).~~

  ↻ **Twenty-eight after round 5**, with seven added on 2026-09-03. Two of those seven were
  read in full, so five are read in full across the whole corpus, up from three.

  The round-3, round-4 and most round-5 additions were read at targeted passages against
  specific claims. That is honest for provenance-checking and weaker than a full read for
  anything else. A paper admitted this way could contradict this design somewhere nobody
  looked.

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

No citation in this document was generated from model memory. Every reference originates
from a tool-call result within the session, and all cited works were retrieved and read.

Page-level locators in `DESIGN.md` refer to PDF page numbers.

Each matches the `### Page N` heading in that paper's extracted `source.md`.

Retrieved PDFs and their extractions are not committed. They are copyrighted publisher
material, and two were obtained through institutional access.

[`FETCH-RESULTS.md`](FETCH-RESULTS.md) records the exact retrieval URL for each, so the set
is reconstructible by anyone with equivalent access.

## 9. Gaps and recommended next steps

Several questions are answered well enough to build on. Those are Q1, with [MC25]'s
sharpening, and Q3, Q4 and Q6. Q7 is answered in its precedent half. Q9 is answered in its
theory half, from round 4, and the world's own answer to it is experimental. Q10 is answered
by round 5, with the independence caveat in §7.3.

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
   Lessin's muscle-drive line remains unexplored.

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
