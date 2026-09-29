# Literature on an environment curriculum for the nursery

A Sonnet research agent's report of 2026-09-29 night, saved as it came. The owner accepted the
environment curriculum (fable-propose-fast-nursery.md, ruling 1) and asked for academic grounding.
Retrieval was patchy: most sources were read at abstract or search-snippet level only, and each
says which. Nothing here is cited in DESIGN.md until it is read in full and, where it is not in
research/LITERATURE-REVIEW.md, taken through the review's update protocol (§3.5).

## 1. Short answer

(1) Environmental curriculum with fixed fitness: partly supported. The literature treats "environmental complexification" as a legitimate, named scheme of incremental evolution, distinct from fitness shaping (Mouret & Doncieux 2008, abstract level). A fixed, natural currency plus a staged or varying environment can beat direct optimisation (POET; Kashtan, Noor & Alon 2007). Caveats: (a) the literature does not say the curriculum stays "selection on the organism's own currency" in any privileged sense; it is a designer's choice, and the incremental-evolution literature names that as its cost (hand-tuned, case by case, a source of bias). (b) The strongest evidence is for tasks where the easy stage shares sub-structure with the hard one; Kashtan 2007's speed-up is largest for modularly varying goals. (c) Nothing found tests transfer of a sensing-and-steering brain from a still, dense, small tank to the full tank; the re-introduction test in the ecology is the check, and the literature would predict it is needed.

(2) Rejecting a distance-to-food proxy and a proximity tie-break: supported as a caution, not as a rule. Specification gaming and deceptive fitness show proxies get exploited, but fitness shaping is also a recognised bootstrap remedy that sometimes works. When every candidate scores zero, the bootstrap literature's standard remedy is behavioural diversity, not a proxy (Mouret & Doncieux 2009): a third option to record.

## 2. Sources

- **A. Mouret & Doncieux 2009**, "Overcoming the bootstrap problem in evolutionary robotics using behavioral diversity", IEEE CEC 2009, pp. 1161-1168. Search snippets only. If the first population performs equally poorly there is no gradient; behavioural diversity escapes it (a light-seeking neuro-controller). Bears on (2). https://dl.acm.org/doi/10.5555/1689599.1689754 . In the review only as a snowball item (§3.3, not retrieved) and in the reference lists of [EA23], [KRC12], [PU16].
- **B. Mouret & Doncieux 2008**, "Incremental evolution of animats' behaviors as a multi-objective optimization", SAB 2008, LNCS. Search snippet only. Incremental schemes fall into four categories: staged evolution, environmental complexification, fitness shaping, behavioural decomposition. The cleanest published separation of environment shaping from fitness shaping; verify the wording before quoting. https://link.springer.com/chapter/10.1007/978-3-540-69134-1_21 . Not in the review.
- **C. Gomez & Miikkulainen 1997**, "Incremental evolution of complex general behavior", Adaptive Behavior 5(3-4):317-342. Abstract level. Prey capture evolves poorly directly (mechanical strategies); a gradually harder task gives more effective, more general behaviour. Supports (1); the task change was hand-designed. https://journals.sagepub.com/doi/10.1177/105971239700500305 . Not in the review.
- **D. Lenski, Ofria, Pennock & Adami 2003**, "The evolutionary origin of complex features", Nature 423:139-144. Snippets only. The first genotype performing EQU differed from its parent by one or two mutations; some deleterious mutations were stepping stones. That complex functions evolved only when simpler ones were rewarded is UNVERIFIED by the agent (matches memory). The scaffold there is reward on simpler functions, fitness-side shaping, so it cuts partly against the distinction in (1). The review holds it as background inside [DO21]: "While evolving the ability to do the simpler tasks is useful in its own right, they can also serve as building blocks for the more complex tasks (Lenski et al., 2003)" [DO21, p.5]; and on Eco-EA, "solving complex problems by associating limited resources with simpler challenges that can be used as stepping stones" and "Resources remain plentiful until a solution to the corresponding sub-problem is discovered" [DO21, p.8], the closest digital-evolution analogue of a curriculum built from the environment. https://www.nature.com/articles/nature01568
- **E. Covert, Schneider, Grabowski, Ofria & Lenski 2013**, PNAS 110(34):E3171. Snippet only. Five of 23 populations that evolved EQU had a deleterious mutation just before it. Caveat for (1): a stage-by-stage climb may not cross a deleterious stepping stone. https://www.pnas.org/doi/10.1073/pnas.1313424110 . Not in the review.
- **F. Kashtan, Noor & Alon 2007**, "Varying environments can speed up evolution", PNAS 104(34):13711-13716. Abstract via search summary. Goals that change over time can beat a fixed goal dramatically, most for modularly varying goals, growing with difficulty. Supports and qualifies (1): it is about varying goals, not a monotone ramp. https://www.pnas.org/doi/abs/10.1073/pnas.0611630104 . Not in the review.
- **G. Kashtan & Alon 2005**, "Spontaneous evolution of modularity and network motifs", PNAS 102(39):13773-13778. Snippet only. Modularly varying goals give modular networks. https://www.pnas.org/doi/10.1073/pnas.0503610102 . Not in the review.
- **H. Wang, Lehman, Clune & Stanley 2019**, POET, arXiv:1901.01753. Section-level via an HTML extraction; quotes to be checked. Direct optimisation on three hard environments scored 17.9, 39.6 and 13.6 against a solved threshold above 230, agents freezing before obstacles; with transfer disabled "no extremely challenging environments are solved at all" (§4.3); environments admitted only if the agent scores 50 to 300 (§4.2), a minimal criterion. Strongest single citation that a curriculum of environments with a fixed reward solves what direct optimisation cannot; its ladder is machine-generated, where the nursery's is hand-set. https://ar5iv.labs.arxiv.org/html/1901.01753 . Not in the review.
- **I. Brant & Stanley 2017**, "Minimal criterion coevolution", GECCO 2017. Snippet only. Selection by a survival threshold rather than a score. Bears on (2). https://dl.acm.org/doi/10.1145/3071178.3071186 . Not in the review; [PU16] is related.
- **J. Black, Bourrat & Rainey 2020**, "Ecological scaffolding and the evolution of individuality", Nat Ecol Evol 4:426-436. Snippet only. Environmental structure can impose Darwinian properties at a higher level, later internalised. A biological precedent for an external scaffold; about individuality, not brains, so the analogy is loose. https://www.nature.com/articles/s41559-019-1086-9 . Not in the review.
- **K. Lehman et al. 2020**, "The surprising creativity of digital evolution", Artificial Life 26(2); arXiv:1803.03453. Abstract only. Evolving systems subverting the experimenter's intent. Supports the proxy risk in (2); no single anecdote to be cited unread. https://arxiv.org/abs/1803.03453
- **L. Krakovna et al. 2020**, "Specification gaming: the flip side of AI ingenuity", DeepMind blog and list. Snippet. Not peer reviewed. https://deepmindsafetyresearch.medium.com/specification-gaming-the-flip-side-of-ai-ingenuity-c85bdb0deeb4
- **M. Jakobi 1997/1998** (minimal simulations; the octopod). Snippet only. Controllers evolved in a simplified simulation with an envelope of noise transferred to a real robot, but only when the simplification was deliberately noisy or varied. Suggests randomising food density and piece size across candidates, not only ramping them. https://link.springer.com/chapter/10.1007/3-540-64957-3_63

Not retrieved, not cited: Harvey, Husbands & Cliff on incremental evolution; Nolfi & Floreano's book.

## 3. Counter-evidence and risks

- Incremental evolution needs a designed programme of change, hand-tuned case by case, which introduces bias (a paraphrase from search summaries; locate before quoting).
- Curriculum benefit depends on environment and algorithm, and failed to help in some (a search summary of arXiv:2505.22696; not read).
- Kashtan 2007: the speed-up needs sub-structure shared between stages.
- Lenski 2003 and Covert 2013: a stepping stone can be deleterious; a fitness climb may not cross it.
- POET: its curricula depend on transfer between environments; a single ramp failed on the hardest.
- The distinction in (1) is a framing, not an established category. In Avida the scaffold was reward on simpler functions, which (2) rejects. The defensible position: the score is unchanged and cannot be gamed without gaining energy, but the environment is still a design choice.

## 4. Strongest to cite, once read in full

1. Mouret & Doncieux 2008 (B), the taxonomy.
2. Wang et al. 2019, POET (H).
3. Kashtan, Noor & Alon 2007 (F), with its modularity condition.
4. Lenski et al. 2003 with [DO21, pp.5 and 8] (D), the honest counterpoint.
5. Mouret & Doncieux 2009 (A) for the bootstrap problem, with Jakobi (M) for transfer.

## 5. The project's review

Held with page headings: [DO21] (pp.5, 8 above); [PU16], "may eventually cause evolution to stagnate if all stepping stones to higher fitness require first making strides through lower-fitness space" [PU16 §4.2.4, p.7]; [KRC12] (bibliography only for the bootstrap paper). The review's Q11 planned a bootstrapping-problem search, and none of A to M are in it.