# Review round 7, Pass 1: the candidate pool

Pass 1 of review round 7 (the owner's /literature-review of 2026-09-29 night: the nursery's
environment curriculum, and why sensing-and-steering brains are slow to evolve; mode supportive with
a deliberate hunt for counter-evidence; the result lands as a round of LITERATURE-REVIEW.md under
§3.5). Run by a Claude Sonnet subagent, read-only, on public web search and fetch; saved as it came,
condensed only in layout. **Nothing here is a claim.** Every row is provisional: found by a search in
this session, read at abstract or search-summary level at most, not cross-matched, no retraction check,
no citation counts seen. Pass 2 verifies what the owner keeps.

## Search log (PRISMA-S)

Sixty WebSearch and WebFetch calls on 2026-09-29, roughly ten results a search, first page only.
Sources: an unfiltered general search engine, and site filters on pnas.org, pubmed, semanticscholar,
arxiv.org, openreview.net and dblp.org. Google Scholar was not searched directly. The OpenReview and DBLP
filters surfaced no papers hosted there. Journal-site filters (Artificial Life, Adaptive Behavior, IEEE
TEVC) were not run. One fetch (Science Advances, "What if eye...?") returned 403, and one PDF (Sussex,
"do-not-disturb") was unreadable. The full query list is in the session record. Queries by theme:
incremental evolution and environmental complexification; the bootstrap problem; varying environments;
POET and its successors; incremental prey capture; Avida's complex features and stepping stones;
Lessin's syllabus; Harvey's incremental evolution; Avida motility, chemotaxis and foraging;
Jakobi's noise envelope; a priori objectives and novelty search; Polyworld and Framsticks foraging;
chemotaxis in neuroevolution; open-endedness conditions; station keeping in flow; curriculum learning and
its failures; vision in virtual creatures; morphological staging; foraging and resource patchiness;
predator-prey coevolution; transferability; the evolution of perception; Chromaria; Bedau's open problems;
Evosphere; sensing in random search; the costs of nervous systems.

## Strand A: environmental curricula and incremental evolution

| # | Paper | Venue | Access | Why a candidate (abstract level) |
|---|---|---|---|---|
| A1 | Mouret & Doncieux 2008, Incremental evolution of animats' behaviors as a multi-objective optimization | SAB 2008, LNCS 5040 | Springer, paywalled | incremental scheme on a light-seeking task; a search snippet lists four schemes incl. environmental complexification, not confirmed as this paper's own taxonomy |
| A2 | Mouret & Doncieux 2009, Overcoming the bootstrap problem in evolutionary robotics using behavioral diversity | IEEE CEC 2009 | ACM DL / IEEE | behavioural diversity against a multi-subgoal method on a light-seeking neuro-controller |
| A3 | Gomez & Miikkulainen 1997, Incremental evolution of complex general behavior | Adaptive Behavior 5(3-4) | SAGE, paywalled | direct evolution of prey capture hard; hand-designed gradual task change did better |
| A4 | Kashtan, Noor & Alon 2007, Varying environments can speed up evolution | PNAS 104(34) | PNAS, OA probable | varying goals speed evolution, most under modularly varying goals |
| A5 | Wang, Lehman, Clune & Stanley 2019, POET | arXiv 1901.01753 | OA | environments and agents co-evolve; solves what direct optimisation cannot |
| A6 | Wang et al. 2020, Enhanced POET | ICML 2020 | arXiv OA | follow-up to A5 |
| A7 | Dharna et al. 2022, Transfer dynamics in emergent evolutionary curricula | IEEE Trans. Games | arXiv OA | transfer between branches rare but crucial |
| A8 | Milano & Nolfi 2021, Automated curriculum learning for embodied agents | arXiv 2102.08849 / Sci. Rep. | OA | conditions matched to ability; generalisation |
| A9 | Milano, Carvalho & Nolfi 2017, Moderate environmental variation promotes the evolution of robust solutions | arXiv 1710.07913 | OA | moderate variation gives robustness; excessive variation counterproductive |
| A10 | Chaumont & Adami 2016, Evolution of sustained foraging in three-dimensional environments with physics | GPEM 17(4) | arXiv 1112.5116 | "evolutionary staging" let foraging evolve; the staging changes the fitness function. Also strand B |
| A11 | Pilat & Jacob 2010, Evolution of vision capabilities in embodied virtual creatures | GECCO 2010 | ACM DL | light-following evolved incrementally in Sims-style creatures |
| A12 | Lessin, Fussell & Miikkulainen 2013, Open-ended behavioral complexity for evolved virtual creatures | GECCO 2013 | ACM DL | a human-designed syllabus of tasks; says little beyond Sims' light following had been shown |
| A13 | Bongard 2011, Morphological change in machines accelerates the evolution of robust behavior | PNAS 108(4) | PMC OA | body-plan staging sped evolution |
| A14 | Jakobi 1997, Evolutionary robotics and the radical envelope-of-noise hypothesis | Adaptive Behavior 6(2) | SAGE, paywalled | transfer from a simplified simulation to real robots |
| A15 | Koos, Mouret & Doncieux 2013, The transferability approach | IEEE TEVC 17(1) | arXiv 1307.1870 | the reality gap and its cost |
| A16 | Bengio et al. 2009, Curriculum learning | ICML 2009 | ACM DL | origin of the curriculum idea in ML; a result says it was later withdrawn from the proceedings, verify |
| A17 | Narvekar et al. 2020, Curriculum learning for RL domains: a framework and survey | JMLR / arXiv | OA | survey; may list failures |
| A18 | Woolley & Stanley 2011, The deleterious effects of a priori objectives | GECCO 2011 | ACM DL | counter-evidence: fixed a priori objectives hard to meet |
| A19 | Lehman & Stanley 2011, Abandoning objectives | Evol. Comput. 19(2) | MIT Press | novelty search as the alternative remedy |
| A20 | Lehman & Miikkulainen 2014, Overcoming deception in evolution of cognitive behaviors | GECCO 2014 | UT Austin PDF | objective search converged on non-cognitive behaviour; novelty found cognitive |
| A21 | Lenski, Ofria, Pennock & Adami 2003, The evolutionary origin of complex features | Nature 423 | Adami lab reprint | complex functions built on simpler ones when those were also favoured; deleterious stepping stones. Held in part via [DO21] |
| A22 | Covert, Lenski, Wilke & Ofria 2013, Experiments on the role of deleterious mutations as stepping stones | PNAS 110(34) | OA probable | deleterious predecessors of beneficial mutations. The authors differ from the round-7 abstract-level report (nursery-curriculum-literature.md), which is corrected by this |
| A23 | Nolfi & Floreano 1998, Coevolving predator and prey robots | Artificial Life 4(4) | TAMU PDF | competition as a non-designed incremental process |
| A24 | Petrovic, Overview of incremental approaches to evolutionary robotics | Comenius Univ. PDF, year not seen | OA | an overview; venue unknown |
| A25 | Do not disturb: recommendations for incremental evolution | Sussex PDF (Harvey's page), authors not confirmed | OA, unreadable | title only |
| A26 | Bongard 2013, Evolutionary robotics | CACM 56(8) | UVM PDF | survey naming bootstrap and deception |
| A27 | Brant & Stanley 2017, Minimal criterion coevolution | GECCO 2017 | ACM DL | carried from the abstract-level report; not re-found this pass |

## Strand B: why sensing-and-steering evolves slowly, and what let it evolve

| # | Paper | Venue | Access | Why a candidate (abstract level) |
|---|---|---|---|---|
| B1 | Beckmann, McKinley & Ofria 2008, On the evolution of motility and intelligent tactic response | GECCO 2008 | ResearchGate | de novo motility and gradient following in Avida |
| B2 | Swain et al. 2021, Exploring the evolution of perception: an agent-based approach | Front. Ecol. Evol. | OA | resource density had the strongest effect on perceptual range; metabolic costs inhibited it |
| B3 | Bejjani et al. 2025, The emergence of complex behavior in large-scale ecological environments | arXiv 2510.18221 | OA | foraging behaviours appear only in large environments and populations |
| B4 | van der Post & Hogeweg 2011?, Local orientation and the evolution of foraging | PLoS Comput. Biol. | OA | patchy versus uniform food |
| B5 | Griffith & Yaeger 2006, Ideal free distribution in agents with evolved neural architectures | ALIFE X / arXiv 1112.3574 | OA | Polyworld-type agents distribute over patches |
| B6 | Wagner et al. 2013, Behavioral strategy chases promote the evolution of prey intelligence | arXiv 1310.1369 | OA | predator evasion repurposed for foraging in Avida |
| B7 | Moore, Clark & McKinley 2013, Evolution of station keeping as a response to flows in an aquatic robot | GECCO 2013 | Pomona PDF | holding position in flow |
| B8 | Komosinski 2000, Framsticks | Springer chapter | Springer | tool description, context only |
| B9 | Miconi 2008, Evosphere | IEEE CEC 2008 | ResearchGate | a Sims-style ecology with no fitness function |
| B10 | Soros & Stanley 2014, Identifying necessary conditions for open-ended evolution through Chromaria | ALIFE 14 | Semantic Scholar PDF | four hypothesised necessary conditions |
| B11 | Bedau et al. 2000, Open problems in artificial life | Artificial Life 6(4) | MIT Press | open problems; wording on sensing not seen |
| B12 | Hein & McKinley 2012, Sensing and decision-making in random search | PNAS 109(30) | arXiv 1202.3384 | a simple response to noisy sensing dominated random search |
| B13 | Tensen et al. 2026, Microcosmos | arXiv 2607.02954 | OA | neuroevolution and quality-diversity gave swimming and chemotaxis in 2D viscous fluid |
| B14 | Heesom-Green et al. 2025 | ALIFE 2025 | held as [HG25] | already held |
| B15 | Dolson & Ofria 2021 | Front. Ecol. Evol. | held as [DO21] | already held |
| B17 | de Bruin et al. 2024, More complex environments may be required to discover benefits of lifetime learning | arXiv 2412.16184 | OA | an easy environment hid learning's benefit |
| B18 | Auerbach & Bongard 2014, Environmental influence on the evolution of morphological complexity in machines | PLoS Comput. Biol. | OA | environmental complexity changed evolved complexity under a cost |
| B19 | Computer simulation of the evolution of foraging strategies | ResearchGate, venue not seen | unknown | not opened |
| B20 | Sims 1994, Evolving virtual creatures | SIGGRAPH 1994 | karlsims.com | phototaxis was the most complex behaviour shown; check whether held as [S94] |

## Looked for and not found

No paper tests transfer of a sensing-and-steering brain from an easier tank (still, small, dense food) to
a harder one: the gap the abstract-level report named stands. Harvey, Husbands & Cliff on incremental
evolution and Nolfi & Floreano's book were seen only as titles. No Sims reproduction was found reporting
sensing-and-steering failing in a resource-limited ecology. POET's peer-reviewed venue was not confirmed.
Kashtan & Alon 2005, Black, Bourrat & Rainey 2020 and the specification-gaming list were not re-searched.
The four-scheme taxonomy is not confirmed against A1's text.

## Paywalled, for the owner's institutional fetch

A1 (Springer LNCS), A3 and A14 (SAGE), A2 (IEEE CEC), A11 and A12 (ACM DL), and if the counter strand is
kept A18 to A20. B1 has a ResearchGate copy of unconfirmed completeness. A lead only: "What if eye...?"
(Science Advances 2025, arXiv 2501.15001).