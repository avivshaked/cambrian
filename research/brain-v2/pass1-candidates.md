# Brain v2: literature pass 1, candidates

Gathered 2026-10-01 night by a Sonnet subagent with web search, for the review round the proposal asks
for ([`proposals/propose-brain-v2.md`](../../proposals/propose-brain-v2.md), "The literature"). **Pass
1 only: these are candidates, not sources.** No paper was opened, and every description is a search
result's summary. Nothing here may be cited in DESIGN.md until pass 2 has verified it under
`LITERATURE-REVIEW.md` §3.5. Details marked "unconfirmed" were not shown in any result.

The queries named papers the searcher expected, so this pool confirms known work more than it
discovers new work. Q2 and Q4 most need a differently framed pass 2.

## Q1. Growing a network: NEAT and its successors

| Candidate | Venue | Id | Status |
|---|---|---|---|
| Stanley & Miikkulainen, *Evolving Neural Networks through Augmenting Topologies*, 2002 | Evolutionary Computation 10(2) 99-127 | DOI 10.1162/106365602320169811 | peer-reviewed; the key source for minimal starts, historical markings and speciation |
| Stanley, Bryant & Miikkulainen, *Real-Time Neuroevolution in the NERO Video Game*, 2005 | IEEE Trans. Evol. Comp. | DOI unconfirmed | peer-reviewed |
| Stanley, D'Ambrosio & Gauci, *A Hypercube-Based Encoding for Evolving Large-Scale Neural Networks*, 2009 | Artificial Life 15 | DOI unconfirmed | peer-reviewed |
| Risi & Stanley, *An Enhanced Hypercube-Based Encoding ...* (ES-HyperNEAT), 2012 | Artificial Life 18(4) | 10.1162/ARTL_a_00071 (from the URL) | peer-reviewed |
| *Competitive Coevolution through Evolutionary Complexification* | JAIR (unconfirmed) | arXiv 1107.0037 | authors unconfirmed |

## Q2. One brain or one per limb

| Candidate | Venue | Id | Status |
|---|---|---|---|
| Sims, *Evolving Virtual Creatures*, 1994 | SIGGRAPH '94 | DOI 10.1145/192161.192167 | peer-reviewed; local networks per part with a global node |
| *Centralized and Decentralized Control in Modular Robots and Their Effect on Morphology*, 2022 | venue unconfirmed | arXiv 2206.13366 | **read first**: by its summary, decentralised control did better when body and controller were co-optimised, and centralised control converged early |
| *Modular Controllers Facilitate the Co-Optimization of Morphology and Control in Soft Robots* | GECCO (unconfirmed) | DOI 10.1145/3583131.3590416 | only the title was seen; on topic |
| Cheney, Bongard, SunSpiral & Lipson, *Scalable co-optimization of morphology and control in embodied machines*, 2018 | J. R. Soc. Interface 15(143) | DOI unconfirmed | peer-reviewed; body changes tend to hurt control |
| Auerbach & Bongard, *Environmental Influence on the Evolution of Morphological Complexity in Machines*, 2014 | PLOS Comp. Biol. | PMC3879106 | peer-reviewed; tangential |

Two of these bear against the owner's ruling of one brain per creature, by their summaries alone.
That makes them the first to read; it is no finding yet. The agent's inference, from no reading: the
nursery's bodies are fixed, so its test lacks the co-optimisation those papers study. The world, where
bodies change, may meet their problem.

## Q3. Where the rhythm lives

| Candidate | Venue | Id | Status |
|---|---|---|---|
| Ijspeert, Hallam & Willshaw, *Evolving Swimming Controllers for a Simulated Lamprey ...*, 1999 | Adaptive Behavior 7(2) | DOI 10.1177/105971239900700202 | peer-reviewed; the closest to an evolved swimmer |
| Ijspeert, Crespi, Ryczko & Cabelguen, *From Swimming to Walking with a Salamander Robot ...*, 2007 | Science 315 | DOI 10.1126/science.1138353 | peer-reviewed |
| Ijspeert, *Central pattern generators for locomotion control in animals and robots: A review*, 2008 | Neural Networks 21(4) | DOI 10.1016/j.neunet.2008.03.014 | peer-reviewed |
| Ekeberg, *A combined neuronal and mechanical model of fish swimming*, 1993 | Biol. Cybernetics 69 | 10.1007/BF00199436 (from the URL) | peer-reviewed; stretch receptors feeding the oscillator |
| *Proprioceptive feedback amplification restores effective locomotion in ... lampreys ...*, 2023 | PNAS | URL only | title only |

## Q4. Finding food by smell with an evolved network

| Candidate | Venue | Id | Status |
|---|---|---|---|
| Beer & Gallagher, *Evolving dynamical neural networks for adaptive behavior*, 1992 | Adaptive Behavior 1(1) 91-122 | DOI unconfirmed | seen only as a citation; the abstract is needed |
| Izquierdo & Beer, *Evolution and Analysis of Minimal Neural Circuits for Klinotaxis in C. elegans*, 2010 | J. Neurosci. 30(39) | DOI unconfirmed | peer-reviewed; authorship unconfirmed |
| Izquierdo & Beer, *Connecting a Connectome to Behavior ...*, 2013 | PLOS Comp. Biol. | 10.1371/journal.pcbi.1002890 | peer-reviewed |

No odour-guided swimmer other than the worm was found.

## Q5. Starting weights and mutation steps

| Candidate | Venue | Id | Status |
|---|---|---|---|
| Salimans et al., *Evolution Strategies as a Scalable Alternative to Reinforcement Learning*, 2017 | arXiv | 1703.03864 | preprint |
| Such et al., *Deep Neuroevolution: Genetic Algorithms Are a Competitive Alternative ...*, 2017 | arXiv | 1712.06567 | preprint |

## Left out

Some results carried arXiv ids dated after the search (the 2603, 2604, 2608 and 2609 series). They were
left out unread. Among them was a hexapod paper claiming per-leg controllers beat a central one.

## Queries

Seventeen web searches on 2026-10-01, each naming the expected paper or topic. Pass 2 should add
searches framed by the question rather than by the paper.

1. `Stanley Miikkulainen 2002 Evolving Neural Networks through Augmenting Topologies Evolutionary Computation 10(2)`
2. `HyperNEAT Stanley D'Ambrosio Gauci hypercube-based encoding for evolving large-scale neural networks Artificial Life 2009`
3. `Evolving Neural Networks in Real Time rtNEAT Stanley Bryant Miikkulainen 2005 IEEE Transactions on Evolutionary Computation NERO`
4. `ES-HyperNEAT Risi Stanley 2012 enhanced hypercube-based encoding evolving neural networks Artificial Life`
5. `Sims 1994 Evolving virtual creatures SIGGRAPH neural network per part local neurons`
6. `centralized versus decentralized controller evolved modular robots comparison global neural network per-module controller`
7. `Ijspeert Crespi Ryczko Cabelguen From swimming to walking with a salamander robot driven by a spinal cord model Science 2007`
8. `Ijspeert central pattern generators for locomotion control in animals and robots: a review Neural Networks 2008`
9. `Beer Gallagher 1992 Evolving dynamical neural networks for adaptive behavior chemotaxis`
10. `C. elegans klinotaxis neural circuit model evolved chemotaxis Izquierdo Beer`
11. `Salimans Evolution Strategies as a Scalable Alternative to Reinforcement Learning arXiv 1703.03864`
12. `Such Deep Neuroevolution genetic algorithms competitive alternative training deep neural networks reinforcement learning arXiv 1712.06567`
13. `Cheney Clune Lipson Evolved electrophysiological soft robots OR "unshackling evolution" encoding; Auerbach Bongard environmental influence morphological complexity evolved virtual creatures`
14. `Ijspeert Hallam Willshaw evolving swimming controllers for a simulated lamprey with inspiration from neurobiology Adaptive Behavior 1999`
15. `Beer Gallagher 1992 Adaptive Behavior 1(1) 91-122 "Evolving dynamical neural networks for adaptive behavior" abstract chemotaxis legged locomotion`
16. `Evolving neural network controllers modular robot Evolutionary Robotics Nolfi Floreano; Cheney "Scalable co-optimization of morphology and control in embodied machines" 2018`
17. `Ekeberg a combined neuronal and mechanical model of fish swimming Biological Cybernetics 1993 OR Cohen Holmes Rand lamprey central pattern generator proprioceptive feedback swimming`
