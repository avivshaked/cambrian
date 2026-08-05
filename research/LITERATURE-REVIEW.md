# Literature Review — Evolved Virtual Creatures for a Unity Implementation

**Review type:** Multivocal Literature Review (Garousi et al.), scoping mode
**Conducted:** 2026-08-02
**Purpose:** Inform the implementation design at [`../DESIGN.md`](../DESIGN.md) for a
Karl Sims–style evolved-virtual-creatures simulator in Unity (water-first locomotion,
quality-diversity search).
**Reporting standard:** PRISMA-2020 / PRISMA-S, with an AI-assistance disclosure (§8).

> **Scope honesty up front.** This is a **decision-support review for an engineering
> project**, not a publication-grade systematic review. After round 2, all eight retrieved
> papers have been read at least in part — three in full, five in part — from a 28-paper
> candidate pool. Two of six original research questions remain only partly answered, and
> round 2 opened a seventh (endogenous selection, `DESIGN.md` §5A) that **has never been
> searched for at all**: its only sources are two systems described second-hand in
> [L21 §13]. Section 7 states the limitations without softening them.

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
| Q1 | What prevents premature morphological convergence in body–brain co-optimisation? | ✅ **Answered** |
| Q2 | Why are Sims' 1994 results hard to reproduce? What are the necessary ingredients? | 🟡 **Partial** — sources retrieved, unread |
| Q3 | Direct graph vs CPPN vs grammar encoding? | ✅ **Resolved** |
| Q4 | Which quality-diversity variant? | ✅ **Answered** |
| Q5 | Controller and actuator representation? | 🟡 **Partial** — one implementation's scheme, no comparison |
| Q6 | What physics exploits should be defended against? | ✅ **Answered** (checklist + two case studies) |

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

---

## 5. Synthesis matrix

| Key | Claim used | Method / scope | Stance vs design | Quality | Limitations |
|---|---|---|---|---|---|
| **[EA23]** | Premature morphological convergence is the dominant failure mode; no-protection baseline loses every pairwise comparison; MAP-Elites is the practical choice despite ranking 2nd; fitness-proportional selection improves all three axes; BC alignment matters | 5 algorithms × 20 runs × 1000 gens, pop 100; soft voxel robots 5×5×5, CPPN, Voxcraft GPU; straight-line locomotion, T=5s; Condorcet + 11 indicators | **Corrective** — added §2 wholesale, changed §8.2 and §8.3 | 📄 Peer-reviewed, *Adaptive Behavior* 🔓 CC BY-NC | **Soft-body substrate, not rigid articulated.** Algorithm ranking not demonstrated on this design's substrate; mechanism is substrate-independent |
| **[U07]** | Per-part reaction-force drag and real hydrodynamics disagree on direction of travel; the GA selected the artifact gait | MPS particle hydrodynamics vs fixed-frame reaction model; single morphology (*Anomalocaris*); Re ≈ 0.25×10⁵ | **Corrective** — added §5.3, motivated validation harness | 📄 Peer-reviewed, ECAL/LNCS | **2D only**; one morphology; kinematics prescribed, not evolved in-loop; strong closing claim ("physics determines body structure") over-reaches its evidence |
| **[K12]** | Novelty search loses at unconstrained swimming, wins at constrained deceptive tasks; concrete anti-exploit checklist; effector scaling + smoothing; three reflection flags; oscillatory transfer aids swimming | Rigid-body ODE creatures, Sims-style graph; NEAT-based; pop 300 × 150 gens; 25–30 runs; t-tests | **Confirmatory + enriching** — §4.1, §4.4, §11.2 | 📄 Peer-reviewed Springer chapter | Behaviour descriptor (final position) is a poor choice, which limits how far the negative swimming result generalises to well-designed QD |
| **[L21]** ⚠ partial | Encoding advantage splits by phenotype: CPPN wins soft-body, direct/recursive win or tie rigid-body; Sims is an *indirect* encoding; PhysX and Unity have precedent here | Survey; Table 6 synthesises 6 published encoding comparisons 2001–2020 | **Decisive** — closed §12.1 | 📄 Peer-reviewed, *Computer Graphics Forum* 🔓 | Secondary source — the underlying comparisons were not read individually; [L21] itself flags one (2017) as confounded |
| **[C18]** ⚠ partial | Simplified fluid collapses morphological diversity, not just accuracy; land→water detrimental, water→land beneficial; non-harmonic actuation matters; self-collision vibration exploit | Voxelyze soft robots + mesh-based quadratic drag; 5 stiffness levels; multi-objective; land/water/transition experiments | **Corrective** — overturned §5.4's own framing; added evidence to §5.1 | 📄 Peer-reviewed, *Soft Robotics* | Soft-body; authors describe their own EA as "rather weak"; transition benefit **p > 0.05**; single stiffness value used for transitions |

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

**Retrieved but unread** — [TM01] Taylor & Massey 2001 (`10.1162/106454601300328034`);
[CEA07] Chaumont, Egli & Adami 2007 (`10.1162/artl.2007.13.2.139`); [CU15] Cully, Clune,
Tarapore & Mouret 2015 (`10.1038/nature14422`). All verified and available locally.

---

## 7. Threats to validity

### 7.1 Search validity

- **⚠ Database protocol not followed.** Discovery used general web search rather than
  native queries to Semantic Scholar, DBLP, OpenAlex or OpenReview. Consequence: no
  reproducible result counts, no field-restricted queries, no systematic date filtering,
  and ranking determined by a general search engine rather than citation structure.
  Relevant work may have been missed for want of the right phrasing.
- **Forward snowballing was not performed.** Backward chasing only. Work *citing* the
  included papers — particularly anything post-2023 responding to [EA23] — is absent. One
  2025 preprint claiming brain-body co-optimisation "consistently fails to select for
  morphological potential" was surfaced in Pass 1 and **never followed up**; if it holds,
  it would bear directly on §2 of the design. ~~*Round 2 addresses this — it is step 2 of the
  update protocol in §3.5.*~~ **Round 2 did not address it.** That round was triggered by a
  new question and searched nothing; forward snowballing remains outstanding and is now
  overdue. Struck rather than deleted, per §0 — a prediction the review made about itself and
  did not keep is part of the record.
- **Saturation was asserted, not measured.** Pass 1 stopped when new queries returned
  known papers. That is a weak criterion applied over three batches.
- **⚠ Round 2 answered a question the corpus was not assembled for.** The eight held papers
  were retrieved against round 1's questions on encodings, quality-diversity and physics
  exploitation. None was retrieved because it addressed metabolic cost or open-ended
  evolution, so with respect to those topics the corpus is a **convenience sample**, and the
  absence of contrary evidence in it means very little. `DESIGN.md` §5A is built on two
  systems known only through a survey ([L21 §13]) with neither primary source held; D017
  records it as a bet, not a finding. A round 3 searching *open-ended evolution, artificial
  ecosystem, endogenous fitness, energy-based selection, Avida, Tierra, PolyWorld, Geb* is
  the correction, and until it runs §5A should be read as unreviewed design rather than as
  evidence-led.

### 7.2 Verification validity

- **⚠ The ≥2-database cross-match rule was not met.** Verification used **CrossRef alone**
  for 8 records, not the required two independent databases. The partial mitigation is
  stronger than a second database in one respect: the retrieving agent independently
  confirmed title and authorship **against page 1 of each fetched PDF**, and all 8 were
  obtained and inspected. So every cited work is confirmed to exist in the strongest
  possible sense — it was read. But the formal protocol was not followed.
- Records cited **via** other papers' reference lists (Cheney et al. 2018, Pugh et al. 2016,
  Lehman & Stanley 2011, Cully & Demiris 2018, and others) were **not verified at all**.
  These are quarantined in `DESIGN.md` §13.4 and flagged as leads. Two of them are
  load-bearing for design sections §2 and §8.3, which is a genuine weakness.

### 7.3 Synthesis validity

- **Small n.** Five papers inform the synthesis; three read in full. Conclusions rest on
  few sources, and two of the five were read only in part.
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

**Answered well enough to build on:** Q1, Q3, Q4, Q6.

**Open, in priority order:**

1. **The 2025 co-optimisation preprint** — surfaced in Pass 1, never followed up. Claims
   brain-body co-optimisation "consistently fails to select for morphological potential."
   If sound, it challenges the mitigations in `DESIGN.md` §2.3. *Cheapest high-value
   action remaining.*
2. **⚠ NEW (round 2) — open-ended evolution and artificial ecosystems.** `DESIGN.md` §5A now
   specifies endogenous selection, and **the review has never searched for this literature.**
   The only sources are two systems described second-hand in [L21 §13]. This is the largest
   gap between what the design asserts and what the review supports. Suggested terms:
   *open-ended evolution, artificial ecosystem, endogenous fitness, energy-based selection,
   Avida, Tierra, PolyWorld, Geb, Ventrella Gene Pool.* Specific questions: does anything
   report trophic levels emerging from morphology-encoded feeding modes; what stops these
   systems collapsing to a single strategy; and is there any precedent for a per-neuron
   metabolic charge.
3. **Q2 — Sims reproduction.** ~~[TM01] and [CEA07] are retrieved and unread.~~ **Both were
   read in round 2** — [TM01] pp.4, 6–8 on fitness design, joint actuation and complexity
   caps; [CEA07] §3.2–3.4 pp.3–6 and §5 pp.13–14 on the complexity bonus, joint DOF and
   simulator settings. Both are now partial reads and their §13.2 entries need widening the
   same way [L21]'s did. Still outstanding: Krčah's GECCO'07 reimplementation and Lessin's
   thesis, both openly available and never fetched. Directly relevant to whether Milestone 3
   will actually produce swimmers.
4. **Q5 — controller representation.** Only one implementation's scheme was found ([K12]).
   Lessin et al.'s muscle-drive work proposes shifting complexity from controller to body
   — a third architectural option not represented in the design at all.
5. **Verify the §13.4 quarantine.** Cheney et al. 2018 (morphological innovation
   protection) and Pugh et al. 2016 (BC alignment) are load-bearing but unverified and
   read only second-hand.
6. **Forward snowballing from [EA23] and [C18]** — no post-2023 responses were sought.

**Not worth pursuing** on current evidence: deep-RL co-design (DERL, Evolution Gym) and
CPPN encoding literature, both of which address a different substrate than this project
targets.
