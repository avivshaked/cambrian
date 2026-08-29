# Literature Review — Evolved Virtual Creatures for a Unity Implementation

**Review type:** Multivocal Literature Review (Garousi et al.), scoping mode
**Conducted:** 2026-08-02
**Purpose:** Inform the implementation design at [`../DESIGN.md`](../DESIGN.md) for a
Karl Sims–style evolved-virtual-creatures simulator in Unity (water-first locomotion,
quality-diversity search).
**Reporting standard:** PRISMA-2020 / PRISMA-S, with an AI-assistance disclosure (§8).

> **Scope honesty up front.** This is a **decision-support review for an engineering
> project**, not a publication-grade systematic review. After round 3, sixteen retrieved
> papers are held — three read in full, the rest in part, where "in part" for the round-3
> additions mostly means *specific claims verified against the primary text* rather than
> cover-to-cover reading. ~~Round 2 opened a seventh question (endogenous selection,
> `DESIGN.md` §5A) that **has never been searched for at all**~~ **Round 3 (2026-08-29)
> searched it**: §5A is no longer unreviewed design, though the round-3 coverage is
> breadth-first and single-pass — see §7.1. Section 7 states the limitations without
> softening them.

---

## 0. Review rounds

**This is a living review, not a one-shot report.** It is extended in discrete *rounds*,
mirroring the draft-changelog pattern in [`../DESIGN.md`](../DESIGN.md) §0/§0b. Every
section below reflects the cumulative state after the most recent round; this table is the
history.

| Round | Date | Trigger | Queries | Papers read | Design changes produced |
|---|---|---|---|---|---|
| **1** | 2026-08-02 | Initial — six ranked questions before implementation | 15 | 3 full, 2 partial | DESIGN.md draft 1→3: added §2 (premature convergence), changed §8.2/§8.3 (selection + multi-BC), corrected §5.4 (fluid model diversity cost), §4.4 (effector scheme), §4.1 (reflection flags), §11.2 (exploit checklist), closed §12.1 (encoding) |
| **2** | 2026-08-02 | New question, raised during implementation: **should morphological and neural complexity carry a metabolic cost?** | 0 new searches — targeted re-reading of the 8 papers already held | **0 new papers.** Extended reading of [L21] (§8.2 p.10, §13 pp.14–16) and [C18] (§2.4 p.8, §3 pp.13/17, §4 p.29); confirmatory passes on [CEA07] §3.4, [TM01] pp.6–8, [K12] §2.2, [CU15] supplementary p.24 | DESIGN.md draft 4→5: added **§5A** (energy economy, endogenous selection), superseded §5.5, demoted §8 to observatory, repurposed §6.3/§6.4. Recorded as `DECISIONS.md` D017 |
| **3** | 2026-08-29 | §9's backlog, overdue by the review's own protocol: §5A implemented and measured for a month with its literature never searched; the 2025 co-optimisation preprint never followed up; forward snowballing never done; §13.4 quarantine unverified | 4 parallel search sweeps (delegated to subagents — §3.2, §8), plus Semantic Scholar citation API for forward snowballing and CrossRef for verification | **8 new papers retrieved** ([Y94] [MC25] [CO02] [VG05] [GOY23] [ST00] [CB18] [PU16]), ~45 candidates surfaced and screened, ~13 citing works triaged; key claims verified against primary text for [Y94] [MC25] [CO02] [CB18] | **§5A.2's "no precedent for a per-neuron charge" corrected** — [Y94 p.7] is the precedent (DESIGN.md §0e). [CB18] and [PU16] verified and promoted out of §13.4. Q1 sharpened by [MC25]; **Q7 answered in part; Q8 opened and answered in principle** (§2). No DESIGN mechanism changed; the round's findings feed the next decisions rather than rewriting existing ones |

**Round 2 note — no new retrieval.** This round searched nothing. It re-read papers already
in `research/papers/` against a question that had not been asked in round 1, and the answer
changed the design more than round 1's fifteen queries did. Three limitations of this follow,
and they are carried into §7:

- **The corpus was assembled to answer different questions.** Round 1's six questions were
  about encodings, quality-diversity and physics exploitation. Nothing was retrieved *because*
  it addressed metabolic cost, so the eight papers are a convenience sample with respect to
  this question, not a search result. A proper round 3 would search for it directly —
  candidate terms: open-ended evolution, artificial ecosystem, endogenous fitness, energy-based
  selection, Avida/Tierra/PolyWorld/Geb.
- **The two directly relevant systems are known only through a survey.** PolyWorld and
  Ventrella's Gene Pool appear in [L21 §13] as descriptions, not evaluations. Neither primary
  source is held. DESIGN.md §5A is therefore **not literature-backed**, and D017 records it as
  a bet rather than a finding.
- **[L21] is marked ⚠ partial in §13.2 of DESIGN.md**, and the sections read here (§8.2, §13)
  were outside the range that entry declares. That entry has been widened; the general point
  is that "partial" reads accumulate silently unless each extension is recorded.

### Rules for adding a round

1. **Never edit a previous round's row.** Append.
2. **Update the cumulative sections** — §2 (question status), §4 (flow counts), §5
   (synthesis matrix), §6 (bibliography), §7 (threats), §9 (gaps). A round that adds
   papers but leaves §7.3's "small n" claim untouched has made the review dishonest.
3. **Record design impact in both places.** If a finding changes `DESIGN.md`, it goes in
   that document's changelog *and* this table. They drift otherwise, and then neither can
   be trusted.
4. **A round that changes nothing is still a round.** Recording "searched, found nothing
   new" is evidence of saturation and is worth more than silence.

---

## 1. Executive synthesis

The design this review was commissioned to test was written from first principles, and the
literature contradicted it in three places — two substantive, one of them a mistake in the
design's own reasoning rather than in its facts.

**First, the design had omitted the field's dominant failure mode entirely.** Co-evolving
body and brain is pathological in a specific, well-documented way: a morphological mutation
invalidates the controller co-adapted to the previous body, so selection discards the
offspring even when the new body is superior. Morphology then stagnates within a few dozen
generations while controllers continue improving, discarding the benefit of co-evolution
altogether. Eguiarte-Morett & Aguilar [EA23] state this directly and attribute it to three
independent prior groups; their five-algorithm benchmark shows a baseline without diversity
protection losing every pairwise comparison. Some diversity-preserving mechanism is
mandatory. Their formal winner was multi-BC NSLC, but their own practical recommendation
for virtual-creature co-evolution was MAP-Elites — which the design retains, with two
corrections: parent selection must be **fitness-proportional** rather than uniform, and
descriptors must span **both** an aligned and an unaligned behaviour characterisation.
That second point is bracketed by two failures pointing in opposite directions: Krčah [K12]
showed a purely aligned descriptor (final position) causing divergent search to *lose* to
plain fitness search at swimming, while Pugh et al. (via [EA23]) warn that purely unaligned
descriptors can prevent a QD algorithm reaching viable solutions at all.

**Second, the simplified fluid model is exploitable, and its cost is not what the design
assumed.** Usami [U07] compared exactly the per-part quadratic drag scheme the design
specifies against particle-based hydrodynamics and found the two disagreed on the
*direction of travel* — and, more damningly, that the evolutionary algorithm had selected
the offending gait precisely because the cheap model was wrong. Corucci et al. [C18], using
a near-identical drag model, then extend the finding in a direction the design had not
anticipated: the simplification does not merely produce fictitious physics, it **collapses
morphological diversity**, precluding fish-like and squid-like creatures and yielding a
population of similar medusoids. The design had reasoned that a crude fluid model was
acceptable for the aesthetic goal and problematic only for the scientific one. That was
wrong in the more important direction — morphological variety *is* the aesthetic goal.
Added mass, the cheapest missing term, was accordingly promoted from a refinement to a
Milestone 3 requirement.

**Third, the encoding question resolved in the design's favour, but for a reason worth
recording.** Two of the strongest papers here use CPPN indirect encodings and report
advantages over direct ones, which appeared to threaten the design's Sims-style recursive
graph. Lai et al.'s survey [L21] synthesises every published encoding comparison, and the
result splits by phenotype: **CPPNs win on soft bodies; on rigid articulated bodies,
direct and recursive encodings win or tie**, including in the most recent four-way
comparison. The apparent threat was a substrate mismatch. The same source also corrected a
terminology error — a Sims recursive graph *is* an indirect encoding, so the regularity
CPPNs were wanted for is already present. Elsewhere the literature was confirmatory:
Krčah's implementation supplied a concrete anti-exploit checklist and an effector scheme
cheaper than the design's proposal, and Corucci supplied empirical support for the
water-before-land milestone ordering — though that particular result did not reach
statistical significance and is reported here as suggestive only.

---

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

**PICOC framing.** *Population:* evolved virtual creatures with genetically encoded 3D
morphology and control. *Intervention:* co-evolution of body and controller under
evolutionary / quality-diversity search. *Comparison:* fixed-morphology controller
evolution; direct vs generative encodings; plain GA vs QD. *Outcome:* emergence of
effective locomotion, behavioural and morphological diversity, reproducibility. *Context:*
physics-simulated 3D rigid-body articulated creatures, aquatic and terrestrial.

**Deliberate exclusions:** deep-RL locomotion on fixed morphologies; sim-to-real transfer;
soft-body/voxel work except where it speaks to encoding, QD, or fluid modelling. The
soft-body exclusion proved to be a judgement call worth revisiting — [EA23] and [C18] are
both soft-body papers and both turned out to be central.

---

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

**⚠ Deviation from protocol.** The prescribed portfolio (Semantic Scholar, arXiv,
OpenReview, DBLP, OpenAlex queried natively) was **not** followed. Discovery ran through
general web search, which surfaced results *from* those databases but without their field
control, date filters, or result counts. See §7.1.

### 3.2 Full search strategies

Batch 1 — core concepts:
```
1. "evolving virtual creatures" Karl Sims replication reproduce difficulty reimplementation
2. co-evolution body and brain morphology control premature convergence evolutionary robotics
3. MAP-Elites quality diversity evolving robot morphology illumination algorithm
4. "surprising creativity of digital evolution" physics simulator exploitation anecdotes
5. CPPN generative encoding evolving virtual creatures morphology symmetry regularity
```
Batch 2 — named-author chasing:
```
6.  Lessin Fussell Miikkulainen evolving virtual creatures behavioral complexity syllabus muscle
7.  Taylor Massey "recent developments" evolution morphologies controllers physically simulated creatures Sims
8.  Vassiliades Mouret "elite hypervolume" Iso+LineDD MAP-Elites variation operator CMA-ME
9.  central pattern generator versus neural network controller evolved locomotion torque position control comparison
10. Krcah "evolving virtual creatures revisited" GECCO Sims reimplementation
```
Batch 3 — foundations and gap-filling:
```
11. Karl Sims 1994 "Evolving Virtual Creatures" SIGGRAPH "Evolving 3D Morphology and Behavior by Competition" Artificial Life
12. Mouret Clune "Illuminating search spaces by mapping elites" Cully "Robots that can adapt like animals" Nature 2015
13. Lessin "muscle drives" evolved virtual creatures "trading control intelligence for physical intelligence"
14. evolving swimming creatures aquatic locomotion artificial life fluid drag model simulation morphology
15. Gupta "Embodied intelligence via learning and evolution" DERL "Evolution Gym" co-design benchmark
```

### 3.3 Citation searching

Backward snowballing from reference lists of the three fully-read papers. Yielded (not
retrieved): Miconi & Channon 2005/2006, Shim & Kim 2003, Hornby/Lipson/Pollack 2003,
Lipson & Pollack 2000, Bongard & Hornby 2010, Mouret & Doncieux 2009, Lassabe et al. 2007,
Pilat & Jacob 2010, Framsticks (Komosiński & Ulatowski), Terzopoulos et al. 1994,
Ventrella 1998, Joachimczak et al. 2016, Kriegman et al. 2018.

**Forward snowballing was not performed.** See §7.1.

### 3.4 Limits and deduplication

- Language: English only.
- Date: 1994 → 2026, unbounded (foundational field; Sims 1994 non-negotiable).
- Types: journal articles, conference papers, book chapters, theses, preprints.
- Deduplication: manual, by title + first author.
- Grey literature: admitted per MLR protocol; none ultimately used in the synthesis, as
  the peer-reviewed set proved sufficient.

### 3.5 Search update protocol (PRISMA-S item 12)

How to run the next round. This is the reproducible part — anyone should be able to execute
it without reconstructing intent from scratch.

**What triggers a round:**

| Trigger | Example |
|---|---|
| A milestone gate | Before Milestone 3, re-check search-algorithm literature — that is when §8 stops being theory |
| An item from §9 is picked up | The 2025 co-optimisation preprint; Q2 or Q5 |
| Implementation contradicts the design | If measured behaviour disagrees with a cited claim, the citation gets re-examined, not the measurement |
| Elapsed time | This field publishes slowly; annually is ample |

**How to execute:**

1. **Re-run the §3.2 strings unchanged**, restricted to *since the last round's date*. Log
   the date filter. Unchanged strings are what make round-to-round comparison meaningful —
   if a string needs changing, that is a new question, so add it and say so.
2. **Forward-snowball from the included set** — the gap flagged in §7.1. For each paper in
   §6, find work citing it. This matters more than new keyword searches, because the
   included papers are now known-relevant anchors.
3. **Screen against §2's questions**, not against general interest. A fascinating paper
   that answers none of the six open questions goes in §9 as a lead, not into the synthesis.
4. **Verify before citing** — the §7.2 cascade. Note that round 1 met this only partially.
5. **Update the cumulative sections**, per §0's rules.

**Where the next round is already scoped:** §9 lists five prioritised items. That is the
round-2 backlog; it does not need re-deriving.

**Retiring a claim.** If a new paper overturns something the design currently relies on,
the old claim is **struck through, not deleted**, with a pointer to what replaced it. The
design's history of being wrong is the most useful thing about it — three corrections in
round 1 are why the current draft is trustworthy.

### 3.6 Round 3 searches (2026-08-29)

Discovery was **delegated to four parallel search subagents** (see §8 for the AI-assistance
record), each with a scoped brief and a hard rule set: read-only on the web, topic keywords
only in queries, fetched content treated as untrusted data. The four sweeps:

1. **Artificial-ecosystem primaries** — PolyWorld, Gene Pool, Tierra, Avida, Geb, and any
   system with emergent (not scripted) trophic structure. Terms per §7.1's round-2 backlog:
   *open-ended evolution, artificial ecosystem, endogenous fitness, energy-based selection,
   Avida, Tierra, PolyWorld, Geb, Ventrella Gene Pool.*
2. **Open-endedness instruments** — Bedau–Packard evolutionary activity, the MODES toolbox,
   OEE definitions and necessary-condition lists, treadmill detection from lineage records.
3. **Trophic emergence, recycling, neural cost, motility economics** — conditions for
   multi-trophic food webs in evolving models; decomposer/remineralisation loops; precedent
   for per-neuron metabolic charges; when locomotion pays (chemotaxis, DVM, buoyancy
   trade-offs).
4. **Recorded debts** — the 2025 co-optimisation preprint (§9 item 1); independent
   verification of the two load-bearing §13.4 quarantine entries; forward snowballing from
   [EA23] and [C18] via the Semantic Scholar citation API.

Verification: CrossRef API for metadata; retrieval of eight open-access primaries, six of
which were then re-verified **by the reviewing model directly against the downloaded text**
at the specific passages cited (see `FETCH-RESULTS.md`, round-3 section). ⚠ The protocol
deviation of round 1 (general web search rather than native database queries) repeats here,
with the additional layer that discovery ran through subagents — §7.1.

---

## 4. PRISMA flow

*Cumulative through round 1. Later rounds add to these counts rather than replacing them —
see §0.*

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

**Verification:** 8 records cross-checked against CrossRef (title, authors, year, venue,
volume, pages, DOI, retraction status). **No retractions.** Four metadata errors in the
working candidate list were caught and corrected — Krčah's chapter is 2012 not 2011;
Eguiarte-Morett published online 2023 in the 2024 issue; two DOIs were missing; and one
record (Usami) had no author attributed at all.

**Round 3 additions (2026-08-29):**

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

Metadata notes from round 3: [CO02] has **no DOI** (checked three ways against CrossRef) and
is sometimes miscited as 2003 — the PDF header confirms Artificial Life VIII, 2002. [MC25]
is arXiv:2508.17464v2, the accepted-manuscript version for *Artificial Life*, extending an
ALIFE 2025 paper — cite the journal version once it has volume/page identity.

---

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

---

## 6. Annotated bibliography

**[EA23]** L. Eguiarte-Morett and W. Aguilar, "Premature convergence in morphology and
control co-evolution: a study," *Adaptive Behavior*, vol. 32, no. 2, pp. 137–165, 2023.
DOI: `10.1177/10597123231198497` — ✅ verified 📄 peer-reviewed 🔓 open access
*The single most useful paper for this project.* Benchmarks five algorithms under one
framework with 11 non-redundant indicators and a voting-theory adjudication, which is
unusually rigorous for this field. It supplied the entire premature-convergence section of
the design and two concrete corrections to the search algorithm. Its practical
recommendation — MAP-Elites, despite not winning the formal comparison — is stated
explicitly and is the basis for retaining it here. Read in full.

**[U07]** Y. Usami, "Re-examination of Swimming Motion of Virtually Evolved Creature Based
on Fluid Dynamics," in *Advances in Artificial Life* (ECAL 2007), LNCS 4648, pp. 183–192.
DOI: `10.1007/978-3-540-74913-4_19` — ✅ verified 📄 peer-reviewed
*The most uncomfortable paper for this design.* Short, narrow, and directly damaging to the
proposed fluid model: it demonstrates a published case where the evolutionary algorithm
selected a gait that only worked because the physics was wrong, and where correct physics
reversed the direction of travel. Its concluding claim — that water physics, not genetics,
determines swimming body plans — outruns its 2D single-morphology evidence and is not
relied on here. The core disagreement result is what matters. Read in full.

**[K12]** P. Krčah, "Solving Deceptive Tasks in Robot Body-Brain Co-evolution by Searching
for Behavioral Novelty," in *Advances in Robotics and Virtual Reality*, ISRL vol. 26,
Springer, 2012, pp. 167–186. DOI: `10.1007/978-3-642-23363-0_7` — ✅ verified 📄 peer-reviewed
*The most directly reusable engineering source.* A working rigid-body Sims-style system
described in enough implementation detail to copy: genome structure, joint types, effector
conditioning, and — most valuably — the anti-exploit machinery in §2.3, which converted a
vague risk in the design into a four-item rejection checklist. Its headline negative result
(novelty search loses at swimming) is weakened by a poorly-chosen behaviour descriptor, and
is treated here as a caution about descriptor design rather than about divergent search.
Read in full.

**[L21]** G. Lai, F. F. Leymarie, W. Latham, T. Arita, R. Suzuki, "Virtual Creature
Morphology – A Review," *Computer Graphics Forum*, vol. 40, no. 2, pp. 659–681, 2021.
DOI: `10.1111/cgf.142661` — ✅ verified 📄 peer-reviewed 🔓 open access
*The field map.* Table 6 alone settled the encoding question by aggregating six published
comparisons and revealing that the CPPN advantage is confined to soft-body phenotypes.
Table 2 supplied the engine-precedent argument. Also corrected a terminology error in the
design. Read in part (§4.1–4.2, Tables 2 and 6); the remainder — cellular automata, GRNs,
L-systems, soft-body sections — is unread and likely still holds value.

**[C18]** F. Corucci, N. Cheney, F. Giorgio-Serchi, J. Bongard, C. Laschi, "Evolving Soft
Locomotion in Aquatic and Terrestrial Environments: Effects of Material Properties and
Environmental Transitions," *Soft Robotics*, vol. 5, no. 4, pp. 475–495, 2018.
DOI: `10.1089/soro.2017.0055` — ✅ verified 📄 peer-reviewed
*The richest of the eight.* Uses an almost identical fluid model to the one proposed here,
and is unusually candid about what that model costs — including the finding that
simplification suppresses morphological variety, which overturned the design's own
cost-benefit reasoning. Also the only source testing land↔water ordering empirically. The
authors are notably frank about their own limitations (weak EA, insufficient repetitions,
non-significant transition result), which raises rather than lowers confidence in what they
do claim. Read in part (abstract, §2.1–2.2, §3.2–3.3, §4).

~~**Retrieved but unread** — [TM01], [CEA07], [CU15].~~ All three were read in part during
round 2 and are cited with page locators in `DESIGN.md` §13.2. Struck rather than deleted,
per §0.

### Round 3 additions (2026-08-29)

**[Y94]** L. Yaeger, "Computational Genetics, Physiology, Metabolism, Neural Systems,
Learning, Vision, and Behavior or PolyWorld: Life in a New Context," in *Artificial Life
III*, SFI Studies in the Sciences of Complexity XVII, Addison-Wesley, 1994, pp. 263–298.
Pre-DOI — 📄 proceedings chapter, open author self-archive.
*The precedent §5A didn't know it had.* An endogenous-selection ecology from 1994 that
charges energy linearly per neuron and per synapse, prices every behaviour, and lets
predation and mimicry emerge. The neural-charge mechanism (p.7) directly anticipates
§5A.2's neural term, which the design had believed was its own invention. Read: targeted
passages (pp.1, 7, 10–11), verified against the downloaded text.

**[MC25]** A. Mertan and N. Cheney, "Evolutionary Brain-Body Co-Optimization Consistently
Fails to Select for Morphological Potential," arXiv:2508.17464v2 (accepted manuscript,
*Artificial Life*; extends ALIFE 2025). 🔓 open access.
*The §9-item-1 debt, finally read — and it was worth the wait.* Exhaustively maps a
1.3M-morphology fitness landscape to get ground truth, then shows every tested algorithm —
including MAP-Elites and morphological innovation protection — regularly eliminates bodies
whose true fitness is higher, because fitness under a co-evolving controller systematically
undervalues the newly mutated. Sharpens Q1 from "protection works" to "protection helps and
is insufficient". Read: abstract, discussion, conclusion (pp.1, 29, 31).

**[CO02]** T. F. Cooper and C. Ofria, "Evolution of Stable Ecosystems in Populations of
Digital Organisms," in *Artificial Life VIII*, MIT Press, 2002, pp. 227–232. No DOI —
📄 proceedings, open author self-archive.
*The cleanest transferable result of the round.* Depletable resources → negative
frequency-dependent selection → stable multi-strategy coexistence, proven by the control:
the same evolved ecosystems collapse to one genotype when resources are made unlimited.
Generalises D023's finite sun into a design rule. Read: abstract and results passages
verified (pp.1, 2, 4–5).

**[VG05]** J. Ventrella, "GenePool: Exploring the Interaction Between Natural Selection and
Sexual Selection," in *Artificial Life Models in Software*, Springer, 2005, pp. 81–96.
DOI `10.1007/1-84628-214-4_4` — 📄 chapter, open author self-archive.
*Why locomotion paid there.* Food and mates both require physical approach, so movement is
the only route to reproduction; the design's own prize (depth) is reachable by buoyancy and
demography, which is the diagnostic contrast. Read in full by the round-3 search agent;
spot-checked, not fully re-read.

**[CB18]** N. Cheney, J. Bongard, V. SunSpiral, H. Lipson, "Scalable co-optimization of
morphology and control in embodied machines," *J. R. Soc. Interface* 15(143):20170937,
2018. DOI `10.1098/rsif.2017.0937` — ✅ CrossRef-verified 📄 peer-reviewed 🔓 OA.
Promoted out of §13.4: the morphological-innovation-protection mechanism §2.3/§8.4 lean on,
now read at the method section (§II.C, pp.2–3 of the arXiv copy) rather than inherited from
[EA23]'s bibliography.

**[PU16]** J. K. Pugh, L. B. Soros, K. O. Stanley, "Quality Diversity: A New Frontier for
Evolutionary Computation," *Frontiers in Robotics and AI* 3:40, 2016.
DOI `10.3389/frobt.2016.00040` — ✅ CrossRef-verified 📄 peer-reviewed 🔓 OA.
Promoted out of §13.4: confirms the §8.3 claim about unaligned behaviour characterisations,
with a nuance worth preserving — their conclusion is "not just any type of behavioral
diversity is successful", not that misalignment always fails. Read in full by the round-3
search agent; spot-checked.

**Retrieved in round 3, supporting only (not in the synthesis matrix):**
[GOY23] Goyal, Flamholz, Petroff & Murugan, "Closed ecosystems extract energy through
self-organized nutrient cycles," *PNAS* 120(52):e2309387120, 2023 (read: abstract; arXiv
copy held) — the theoretical anchor for remineralisation, to be read in full before that
mechanism's D-entry. [ST00] Standish, "An Ecolab Perspective on the Bedau Evolutionary
Statistics," *ALife VII*, 2000 (read: abstract level; arXiv copy held) — the
permuted-shadow implementation for Q8.

---

## 7. Threats to validity

### 7.1 Search validity

- **⚠ Database protocol not followed.** Discovery used general web search rather than
  native queries to Semantic Scholar, DBLP, OpenAlex or OpenReview. Consequence: no
  reproducible result counts, no field-restricted queries, no systematic date filtering,
  and ranking determined by a general search engine rather than citation structure.
  Relevant work may have been missed for want of the right phrasing.
- ~~**Forward snowballing was not performed.**~~ ↻ **Performed in round 3** (2026-08-29),
  via the Semantic Scholar citation API, and the 2025 preprint was found, read in part, and
  is now [MC25] in the synthesis. Two findings from the snowball are themselves threats
  worth recording: **[EA23] has only 3 recorded citations** — it is a niche paper, so §2
  should lean on its *mechanism* (independently attributed to three prior groups) rather
  than its algorithm ranking, which has no independent replication; and the ~46 works citing
  [C18] are almost entirely voxel/soft-body work — **the rigid-articulated aquatic corner
  this project occupies has essentially no recent citation traffic**, a field gap rather
  than a search failure. Earlier struck text kept below, per §0 — a prediction the review
  made about itself and kept late is part of the record.
  ~~*Round 2 addresses this — it is step 2 of the update protocol in §3.5.* **Round 2 did
  not address it.** That round was triggered by a new question and searched nothing;
  forward snowballing remains outstanding and is now overdue.~~
- **Saturation was asserted, not measured.** Pass 1 stopped when new queries returned
  known papers. That is a weak criterion applied over three batches.
- **⚠ Round 2 answered a question the corpus was not assembled for.** The eight held papers
  were retrieved against round 1's questions on encodings, quality-diversity and physics
  exploitation. None was retrieved because it addressed metabolic cost or open-ended
  evolution, so with respect to those topics the corpus is a **convenience sample**, and the
  absence of contrary evidence in it means very little. `DESIGN.md` §5A is built on two
  systems known only through a survey ([L21 §13]) with neither primary source held; D017
  records it as a bet, not a finding. ~~A round 3 searching *open-ended evolution, artificial
  ecosystem, endogenous fitness, energy-based selection, Avida, Tierra, PolyWorld, Geb* is
  the correction, and until it runs §5A should be read as unreviewed design rather than as
  evidence-led.~~ ↻ **Round 3 ran** (2026-08-29): both primaries are now held and read in
  part ([Y94], [VG05]), and the endogenous-selection precedent question (Q7) is answered in
  part. §5A is no longer *unreviewed*; it is *thinly reviewed* — see the round-3 items
  below.
- **⚠ Round 3 discovery ran through search subagents.** Four scoped sweeps executed by
  subagent models (§3.6, §8), then screened and synthesised by the reviewing model. This
  repeats round 1's database-protocol deviation and adds a relay: the reviewer saw the
  agents' reports, not the raw result lists, so recall is unmeasurable and pool coverage
  depends on four prompt framings. Mitigation: every claim that entered the synthesis or
  changed `DESIGN.md` was re-verified by the reviewer against the downloaded primary text
  ([Y94] [MC25] [CO02] [CB18]); claims resting on scout-only reads are marked as such in §6.
- **⚠ Several round-3 claims rest on abstract-level reads.** [GOY23], [ST00], and all of the
  trophic-conditions leads (Drossel, Hamm, Fritsch) were read at abstract level or via one
  open full text. None of these has yet informed a design mechanism; each must be read in
  full before it does — the same rule `research/early-life/` already applies to itself.

### 7.2 Verification validity

- **⚠ The ≥2-database cross-match rule was not met.** Verification used **CrossRef alone**
  for 8 records, not the required two independent databases. The partial mitigation is
  stronger than a second database in one respect: the retrieving agent independently
  confirmed title and authorship **against page 1 of each fetched PDF**, and all 8 were
  obtained and inspected. So every cited work is confirmed to exist in the strongest
  possible sense — it was read. But the formal protocol was not followed.
- Records cited **via** other papers' reference lists (Cheney et al. 2018, Pugh et al. 2016,
  Lehman & Stanley 2011, Cully & Demiris 2018, and others) were **not verified at all**.
  These are quarantined in `DESIGN.md` §13.4 and flagged as leads. ~~Two of them are
  load-bearing for design sections §2 and §8.3, which is a genuine weakness.~~ ↻ **The two
  load-bearing entries were verified in round 3** — CrossRef metadata confirmed, open copies
  retrieved and read at the load-bearing passages, promoted to the corpus as [CB18] and
  [PU16]. The remaining §13.4 entries stay quarantined.

### 7.3 Synthesis validity

- **Small n.** Eleven papers inform the synthesis (five from rounds 1–2, six added in
  round 3); still only three read in full. The round-3 additions were read at targeted
  passages against specific claims, which is honest for provenance-checking and weaker than
  full reads for anything else — a round-3 paper could contradict this design somewhere
  nobody looked.
- **⚠ Substrate mismatch is the dominant threat.** [EA23] and [C18] — the two papers
  driving the largest design changes — both use **soft voxel robots**, while the design
  targets **rigid articulated bodies**. The premature-convergence mechanism and the
  fluid-model limitations are argued to be substrate-independent, but the specific
  algorithm rankings and quantitative results are not demonstrated on this substrate. This
  is flagged inline in `DESIGN.md` §8.1 but is worth restating: *the strongest evidence
  here comes from a different kind of creature.*
- **Single-reviewer bias.** No independent screening, extraction or appraisal. Inclusion,
  interpretation and emphasis reflect one reviewer's judgement, oriented toward the
  design's open questions rather than toward the field's own priorities.
- **Confirmation risk, partly mitigated.** The review was commissioned to test a design its
  author wrote. Three of the findings ran against that design — the omitted failure mode,
  the fluid-model diversity cost, and the effector scheme — which is weak evidence against
  systematic confirmation bias, but not proof of its absence.
- **Formal appraisal checklists were not applied.** No CASP, JBI or AMSTAR instrument was
  used. Quality assessment was venue-based plus reading.
- **Grey literature admitted but unused.** The MLR protocol was chosen specifically to
  capture practitioner knowledge about reproducing Sims. Q2 remains unanswered and no grey
  literature was ultimately consulted — so the main justification for the MLR framing was
  never exercised.

---

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

**No citation in this document was generated from model memory.** Every reference
originates from a tool-call result within the session, and all cited works were retrieved
and read. Page-level locators in `DESIGN.md` refer to PDF page numbers matching the
`### Page N` headings in each paper's extracted `source.md`.

**Retrieved PDFs and their extractions are not committed** — they are copyrighted
publisher material and two were obtained through institutional access.
[`FETCH-RESULTS.md`](FETCH-RESULTS.md) records the exact retrieval URL for each, so the
set is reconstructible by anyone with equivalent access.

---

## 9. Gaps and recommended next steps

**Answered well enough to build on:** Q1 (with [MC25]'s sharpening), Q3, Q4, Q6, Q7 in its
precedent half.

**Resolved by round 3, kept for the record:**

1. ~~**The 2025 co-optimisation preprint** — surfaced in Pass 1, never followed up.~~
   Found, retrieved, read in part — now [MC25]. It does challenge §2.3's mitigations: even
   explicit innovation protection selected near-optimal morphologies in ≤22% of trials
   against ground truth. Full read still outstanding (below).
2. ~~**⚠ NEW (round 2) — open-ended evolution and artificial ecosystems** … never searched.~~
   Searched (§3.6). Precedent half of Q7 answered; the emergent-trophic-structure half has
   leads, not held sources. One specific answer: **yes, there is precedent for a per-neuron
   metabolic charge** — [Y94 p.7].
3. ~~**Verify the §13.4 quarantine** (the two load-bearing entries).~~ Done — [CB18], [PU16].
4. ~~**Forward snowballing from [EA23] and [C18]**.~~ Done, with the two field-shape findings
   recorded in §7.1.

**Open, in priority order:**

1. **Read [MC25] in full**, and decide what it means for §2.3/§8.4 *under endogenous
   selection* — whether ecological-niche protection suffers the same undervaluation
   mechanism, and how that would be measured here.
2. **Implement a Q8 instrument.** [ST00]'s permuted-shadow evolutionary activity is the
   candidate that fits the project's logs; it needs a lineage record, so this is coupled to
   the open `lineage.jsonl` decision. Read [BSP98] first (paywalled — queued) so the class
   taxonomy is taken from the source rather than from summaries.
3. **The paywalled/manual fetch queue** (institutional access, in rough priority):
   [BSP98] Bedau, Snyder & Packard 1998 (ALife VI, MIT Press); [DOL19] MODES — free but
   bot-gated at PeerJ, needs a manual browser download, or the *Artificial Life* 25(1)
   version; Bohm, Zhang & Dolson 2024 (ALIFE 2024) — the MODES critique; Channon 2019
   (*Artificial Life* 25(2), DOI `10.1162/artl_a_00285`) and Channon 2001 (ECAL, DOI
   `10.1007/3-540-44811-X_45`); Egbert, Barandiaran & Di Paolo 2012 (*Artificial Life*
   18(1), DOI `10.1162/artl_a_00047`); Taylor et al. 2016 (nominally CC BY, both mirrors
   bot-gated, DOI `10.1162/artl_a_00210`); Wilke & Chow 2005 (OUP, DOI
   `10.1093/oso/9780195188165.003.0011`); Ventrella 1998 (ALife VI, MIT Press).
4. **Read the trophic-conditions leads in full before the remineralisation D-entry**:
   [GOY23] (held), Drossel/McKane/Quince 2004 (arXiv q-bio/0401025), Hamm & Drossel 2021
   (Sci Rep, OA), Fritsch et al. 2021 (arXiv 1905.06855). These are what the mechanism's
   acceptance criteria should be checked against.
5. **Q2 — Sims reproduction.** Unchanged from round 2: Krčah's GECCO'07 reimplementation and
   Lessin's thesis, both openly available and never fetched.
6. **Q5 — controller representation.** Unchanged: only [K12]'s scheme is held; Lessin's
   muscle-drive line remains unexplored.

**Not worth pursuing** on current evidence: deep-RL co-design (DERL, Evolution Gym) and
CPPN encoding literature, both of which address a different substrate than this project
targets — a judgement the round-3 snowball reinforced, since the citation traffic around
[C18] is dominated by exactly that substrate.
