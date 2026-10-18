# Round 57 literature, Pass 2: extraction

Read 2026-10-06. What was read for each paper, and where, is in `pass2-verification.md`. Everything under "Question", "Method", "Findings", "Conditions" and "Limitations" comes from a page fetched in this session. Where only the abstract was read, the entry says so and claims nothing beyond it. The line marked **Inference** under each paper is mine, not the paper's.

**The round, as briefed.** Leaf bodies; light has a season (summer 1.6, winter 0.4 of the mean, three 1,000 s days each in a 6,000 s year) and weather (each day multiplied by a log-normal factor with mean 1, log s.d. 0.3, lag-1 correlation 0.5, one sky for all bodies). Bodies store a reserve, breed when it passes a gate, have no light sense. A generation is about 1,000 s; a 48,000 s run is about 50 generations and 8 winters.

Two conversions used in the inferences below, both mine. A log-normal factor with log s.d. 0.3 has a coefficient of variation of √(e^0.09 − 1) ≈ 0.31. A lag-1 correlation of 0.5, if it decays exponentially, has an e-folding time of −1/ln 0.5 ≈ 1.4 days, so about 1.4 generations.

---

## 1. Fischer, Dieckmann & Taborsky 2011, *Evolution* (full text)

**Question.** How should an organism split energy among storage, reproduction and maintenance when the energy the environment supplies varies stochastically, rather than being constant or deterministically seasonal?

**Method.** Stochastic dynamic programming that maximises expected lifetime reproductive success (R0). Environmental energy follows a stationary multiplicative AR(1) process with log-normal marginal distribution; its coefficient of variation λ measures variability and its autocorrelation time τ measures predictability. The individual's state is (age, environmental energy, stored energy), so the optimal strategy is a reaction norm that responds to the current environment.

**Findings.**
- Storage is optimal only where environmental energy is intermediate and stores are not yet full (Results, Fig. 1).
- At low variability and low predictability, the optimum stores nothing and splits energy between reproduction and maintenance (Results, Fig. 3 top left: λ = 1, τ = 5).
- The fraction stored rises with τ or λ. Higher predictability shifts storage toward higher environmental energy; higher variability widens the range of states where storing pays (Results, Figs 3–4).
- Simultaneous allocation to all three functions can be optimal, unlike in deterministic models where storage and reproduction alternate (Discussion).
- With τ = 0, energy from the environment and energy from stores are interchangeable, so the strategy is symmetric in the two; the symmetry breaks as τ grows (Results, Fig. 4).
- Near starvation, putting everything into reproduction ("terminal investment") is optimal; reproductive allocation is U-shaped in environmental energy (Results, Fig. 2).
- Costlier survival (larger e½) requires more variability and predictability before storage pays, and very costly survival removes storage almost entirely (Results, Fig. 6D).

**Conditions.** λ from 1 to 50 and τ from 0 to 125 time steps in the figures; e½ from 1 to 125. Storage is free and stored energy can be used for anything later. Growth is determinate (post-maturation only). Reproductive success is counted in energy, with no minimum investment. The organism knows the current environmental energy. Fitness is expected R0, not a geometric mean, so the model has no bet-hedging term.

**Limitations stated (Discussion).** Storage and its mobilisation are assumed cost-free, and a large reserve may carry predation or mass costs; intake is not capped by the phenotype; growth is determinate and juveniles are not modelled; reproduction is measured in energy rather than offspring, so there is no minimum clutch.

**Quote.** "Allocation to storage becomes optimal for an increasing range of environmental states" (Results).

**Inference (mine).** r57's weather has CV ≈ 0.31, below the smallest variability Fischer et al. plotted (λ = 1), and its e-folding time of about 1.4 days sits near the low end of their τ range, where they found storage not optimal. Taken alone, the weather is too weak and too short-lived to select for storage on this model; the season, a deterministic fourfold swing, is the stronger driver of reserve, which matches the deterministic-storage theory they cite. Two differences limit the transfer: r57 bodies cannot sense light, so they cannot run Fischer's reaction norm on current energy (their reserve level is the only state they carry), and r57 imposes a breeding gate, which is a minimum investment that Fischer et al. assumed away. Test: compare the evolved gate or mean reserve at breeding across arms with season only, weather only and both, against a constant-light control. Fischer predicts season-only arms rise above control and a weather-only arm at CV 0.31 barely moves.

---

## 2. Wieczynski, Turner & Vasseur 2018, *American Naturalist* (abstract only)

**According to the abstract.** The authors combine experiments and theory to ask how the temporal autocorrelation ("colour") of a fluctuating stress changes the evolution of stress tolerance when the total stress over time is held fixed. RNA virus lineages were evolved under treatments differing in autocorrelation. Experiment and models agreed: tolerance was favoured in whiter (less autocorrelated) environments and disfavoured in redder ones. The mechanism is an interaction between autocorrelation and a trade-off between tolerance and reproductive ability. How strong the effect is depends on the shape of that trade-off and on the tolerance of new mutants.

**Conditions, limitations.** Not available from the abstract. The virus, the stressor, the autocorrelation levels and the trade-off shapes were not read.

**Quote.** "long-term evolutionary dynamics depend not only on the overall strength of selection" (Abstract).

**Inference (mine).** If a large reserve is read as stress tolerance bought at the cost of reproduction, this paper predicts that positively autocorrelated weather (r57's lag-1 0.5) favours a smaller reserve than white weather of the same variance would. That runs against Metcalf & Koons (below). Test: two arms with the same log s.d. 0.3 and lag-1 correlation 0 versus 0.5; compare the evolved gate and mean reserve. Wieczynski predicts the reserve is lower at 0.5.

---

## 3. Park & Wootton 2021, *Ecology Letters* (full text)

**Question.** Does the period of a cyclical environment, separately from its randomness, change how much life-history variation a population keeps?

**Method.** An agent-based model parameterised for the tidepool copepod *Tigriopus californicus* (maturation rate traded against fecundity, density-dependent mortality, a disturbance that kills 30% of juveniles), run 100 times in each of four regimes: disturbance every 3 days ("Fast") or every 14 days ("Slow"), each deterministic or with the same number of events at random times. A five-month laboratory selection experiment with eight replicate populations each of deterministic Fast, deterministic Slow and stochastic Slow.

**Findings.**
- Within-population variance of both maturation rate and fecundity was much higher at the end under Slow than under Fast cycles, in simulation and in the experiment (Results, Fig. 4).
- Deterministic and random-timing versions of the same mean period differed little; periodicity outweighed stochasticity (Results). In the experiment, stochastic Slow did not differ from deterministic Slow.
- Trait means showed no consistent difference between treatments in the experiment (Results).
- Variance fell over time in every regime (purifying selection), much faster under Fast; a 300-step run confirmed Slow variance declines too, only slowly (Results, Fig. 5).
- The spread among simulation runs was larger under Fast, which the authors attribute to drift (Discussion).
- The proposed mechanism: under slower cycles even suboptimal genotypes have positive growth and persist, rather than selection being weaker (Discussion).

**Conditions.** Both periods are shorter than the copepod's 20–30 day generation; runs lasted 150 time steps (5–6 generations), so the results describe transients. "Stochastic" means random disturbance timing with the same count, not random amplitude or autocorrelation. Inheritance carried a small normal error.

**Limitations stated.** No stochastic Fast treatment in the experiment, for logistical reasons; clutch size was not measured and a global mean was assumed; differences between simulation and experiment are put down to handling and founder effects (Methods, Discussion). The authors call for work on the ratio of fluctuation period to generation time and on amplitude.

**Quote.** "periodicity drove differences in intrapopulation variance much more strongly than the deterministic‐stochastic environment distinction" (Results, experimental corroboration).

**Inference (mine).** r57's year is about six generations, far slower relative to generation time than Park & Wootton's Slow regime (14 days against a 20–30 day generation), so their result does not transfer directly; they name that ratio as untested. Read at face value, it predicts that what sets the variance of breeding traits is the season's period rather than the weather, and that variance should decline across the run. Test: track the within-population variance of the gate (or reserve at breeding, or age at first breeding) per sample over the 48,000 s, in arms with weather and without; Park predicts the season-only and season-plus-weather arms track each other closely, and variance falls over time.

---

## 4. Starrfelt & Kokko 2012, *Biological Reviews* (abstract only)

**According to the abstract.** A review with a general model of how fitness variance affects evolution. The model splits a genotype's variance in fitness into the variance of individuals' fitness and the correlation of fitness among individuals of that genotype. Seen this way, conservative and diversified bet-hedging are two ends of one continuum rather than two kinds, and within- and between-generation bet-hedging are two ends of another. A simple example illustrates the model.

**Conditions, limitations.** Not available from the abstract.

**Quote.** "this division can be considered a false dichotomy" (Abstract).

**Inference (mine).** r57's sky is the same for every body, so the weather makes all bodies' fortunes move together: the correlation term in this decomposition is near its maximum. On the abstract's framing, a genotype could lower its fitness variance either by lowering each body's variance (a larger reserve, conservative) or by lowering the correlation among its bodies (bodies of one genotype breeding at different reserves or times, diversified). Whether r57's genome can express the second depends on whether developmental noise can spread a genotype's gate or timing; if it cannot, only the conservative end is open. Test: within a clade, compare the spread of breeding times in arms with shared weather against an arm with independent per-body weather of the same variance, if one can be run.

---

## 5. Graham, Smith & Simons 2014, *Proc. R. Soc. B* (full text, main text)

**Question.** A direct test of Cohen's 1966 dormancy model: does selection maximise geometric-mean fitness or arithmetic-mean fitness?

**Method.** *Neurospora crassa* ascospore dormancy was allowed to evolve under serial transfer with five frequencies of unpredictable lethal "bad years" (0, 25, 30, 40, 50%), in 12 genetic lineages, giving 88 independently evolved samples. Dormancy was not selected directly; transfers carried dormant and germinated spores in proportion to their success. Final and initial dormancy fractions were assayed together.

**Findings.**
- Final dormancy rose with bad-year frequency (ANCOVA covariate p = 0.0051; Kendall's τ = 0.23), with no lineage effect (Results, Table 2).
- Mean final dormancy: 0.16, 0.22, 0.29, 0.33, 0.30 for 0–50% bad years (Table 2); a slight counter-trend between 40 and 50%.
- Observed change regressed on the change Cohen's model predicts, upwards and downwards (Fig. 1 inset); 11 of 12 lineages had positive slopes.
- AIC preferred the geometric-mean model, with relative likelihood 2.4 over the arithmetic-mean model.
- Loss of dormancy evolved more readily than gain.

**Conditions.** 10–14 rounds of selection in a year of lab time. Two consecutive bad years were forbidden, and the 50% line simply alternated, so the sequences were not independent draws. No density dependence (short growth periods). Germination not cued by conditions.

**Limitations stated.** Whether the evolved fractions are optimal is not asked, and they may not have reached equilibrium; lab results cannot say how common bet-hedging is in nature; density-dependent competition was excluded though it interacts with unpredictability in plants; stochastic extinction of lines during transfer was high, though it did not differ by treatment.

**Quote.** "selection results in adaptation to variable rather than to expected environments" (Abstract).

**Inference (mine).** This shows a bet-hedging trait can respond to unpredictability within about a dozen selection events, which is close to r57's eight winters, but the response was modest and needed 88 lines to detect. In r57 terms, an unspent reserve at a breeding opportunity is the analogue of a dormant spore. The prediction is that the reserve held back rises with the frequency of bad stretches; but r57's winters are periodic, not unpredictable, so on Cohen's logic the season would select tracking or storage, and only the weather supplies unpredictability. Test: across seeds, regress the evolved gate or post-breeding reserve on the number of bad weather runs (for example days below 0.6 of mean light) each seed experienced. Graham et al. suggest the effect will be small next to seed-to-seed noise with the few seeds a round has.

---

## 6. Tarazona, García-Roger & Carmona 2017, *Oikos* (abstract only; design from Tarazona et al. 2019)

**According to the abstract.** Laboratory populations of the facultatively sexual rotifer *Brachionus plicatilis* were evolved under a predictable or an unpredictable regime of environmental fluctuation and diverged within a short span (under 77 days). The unpredictable regime evolved earlier initiation of sex (a proxy for earlier production of diapausing eggs) and a lower hatching fraction of diapausing eggs (a proxy for longer diapause). The authors read both as bet-hedging.

**Design, as described by the same group in Tarazona et al. 2019, *Sci. Rep.* (full text read).** Six populations founded from 270 field clones; three per regime. Seven growing cycles separated by dry phases that only diapausing eggs survive. The predictable regime had a regular hydroperiod length, the unpredictable a random one, and the same random sequence was used for all three unpredictable replicates. Some unpredictable cycles were too short for new diapausing eggs, so populations relied on the egg bank. The whole experiment lasted 392 days including diapause.

**Limitations (stated in the 2019 paper about the 2017 design).** One particular unpredictable sequence was tested, so the response is to that pattern and not to unpredictability in general; field selection may differ from the lab; hydroperiod was the only factor varied.

**Quote.** "an earlier initiation of sexual reproduction and a lower hatching fraction of diapausing eggs" (2017 Abstract).

**Inference (mine).** The design is the closest of the eight to r57's contrast, with two differences: the rotifers' unpredictability is in the length of the good season, and they have a dormant stage that r57 bodies lack. Mapped loosely, the result predicts that under unpredictable variation bodies begin "insurance" investment earlier and spend it less readily: a gate reached earlier in the season but a larger share of reserve kept back. Test: compare the timing of first breeding within the summer, and the reserve left after breeding, between an arm with weather and one with seasons only. Their single-sequence caveat applies to r57 too: with a few seeds per arm, each seed is one weather sequence.

---

## 7. Metcalf & Koons 2007, *Proc. R. Soc. B* (full text)

**Question.** In a density-dependent, variable environment, how do the variance and the temporal autocorrelation of environmental quality change the evolutionarily stable allocation of resources to survival rather than fecundity in a semelparous life history?

**Method.** A two-stage (juvenile, adult) matrix model; a heritable allocation π splits a fixed resource pool between survival (logistic in π and environment) and fecundity (exponential in π and environment). Environment is constant, i.i.d. normal with variance σ², or AR(1) with coefficient α from −0.99 to 0.99, normalised to unit variance. Density dependence acts on seedling establishment. ES strategies found by invasion analysis with the stochastic growth rate; pairwise invasibility plots and simulation where no ES exists.

**Findings.**
- Constant environment: better quality lowers the ES allocation to survival (Results, Fig. 2a).
- More variance lowered ES allocation to survival with the default fecundity efficiency (c2 = 6), but raised it when c2 was low; the direction depends on how efficiently resources become fecundity (Results, Fig. 3a). With additive fecundity, more variance never lowered survival allocation.
- More positive autocorrelation at fixed variance always raised ES allocation to survival, more steeply when maturation was slower, across the c1 and c2 values tried (Results, Fig. 2c; Discussion).
- With strong negative autocorrelation (α < −0.5) there is no single ES strategy; evolutionary branching to a dimorphism (high and low survival allocation) occurred in some simulations (Results, Figs 4–5).

**Conditions.** Default c1 = 8, c2 = 6, c3 = 5; maturation probability γ = 0.6, 0.8 or 0.99; T = 10,000 steps with 1,000 discarded. Semelparity; no benefit of survival through growth.

**Limitations stated.** Survival brings no accumulating growth benefit, which suits some species and not others; outcomes depend strongly on the conversion efficiency c2, likely to vary across taxa (Discussion). The authors note that species with several generations per year experience negative autocorrelation from the seasons (Discussion).

**Quote.** "increased temporal autocorrelation in the environment always favoured increased allocation towards survival" (Discussion).

**Inference (mine).** If a larger reserve is read as allocation to survival, Metcalf & Koons predict that r57's positively autocorrelated weather favours a larger reserve than white weather of the same variance would, the opposite of Wieczynski et al. (2018). Both rest on a reproduction–tolerance or reproduction–survival trade-off, so which way r57 goes may depend on the shape of its own trade-off between gate height and breeding rate. On the season: r57 has about six generations per year. At day scale the season's own lag-1 correlation is positive (my calculation for a three-up, three-down square wave: +1/3), and it is −1 at a lag of three days, so the negative-autocorrelation dimorphism Metcalf & Koons found would need the half-year lag to matter, which their AR(1) model does not represent. Test: the same white-versus-0.5 contrast as for Wieczynski; and look for bimodality in the gate within a clade under seasons.

---

## 8. Weissman et al. 2025, *American Naturalist* (journal: abstract only; preprint v1 and v2: full text)

**Question.** Does geometric-mean fitness (GMF) correctly predict whether a bet-hedging mutant is favoured once reproduction, the environmental sequence and (for diversified hedgers) the offspring phenotype are all stochastic?

**Method (preprint).** Wright–Fisher simulations and Markov-chain numerics of one bet-hedging mutant invading N − 1 wild-type specialists, in an environment that is good or bad each generation (probability 0.5, i.i.d.), with non-overlapping generations. The sign of selection is read from the probability of fixation normalised by the neutral 1/N. Phenotypic and environmental stochasticity are switched on and off separately. Population sizes span 1 to 10^6 (the HTML renders it "100 to 106"; I read that as 10^0 to 10^6, consistent with the stated crossing at N ≈ 10).

**Findings (preprint, and the journal abstract agrees on the first three).**
- Stochasticity can reverse the sign of selection that GMF predicts.
- "Sign inversion": a bet hedger can be deleterious at small N and beneficial at large N, for conservative and diversified hedgers.
- Applied to published data (*Papaver dubium* germination and *Salmonella* persistence, and in the journal *Clarkia xantiana* seed banking), stochastic models are needed to explain the observed bet-hedging.
- Phenotypic stochasticity makes diversified bet-hedging less favourable, mostly at small N; environmental stochasticity makes bet-hedging more favourable, mostly at large N. Both act through Jensen's inequality (v1 Results).
- Conservative bet hedgers show sign inversion only when their GMF is below the wild type's (v1 Results, Fig. 4).
- With GMF advantage > 0, inversion crossings fell at N between 2 and 30; with GMF disadvantage, between 30 and 10^6 (v2 Results).
- v1 Discussion: conservative bet-hedging is predicted to be more adaptive than diversified when effective population size is small.

**Conditions.** Two environmental states, uncorrelated in time; constant N; asexual; non-overlapping generations; GMF differences from −0.15 to +0.125.

**Limitations stated.** Spatial variation is out of scope; phylogenetic history is not modelled (the specialist is assumed ancestral); the conservative phenotype's side-effects are not modelled; the largest crossing population sizes approach the surveyed maximum, so inversion may extend further (v1 and v2 Results, Discussion).

**Quote.** "GMF alone does not capture the full extent to which bet hedging is adaptive" (preprint v2, Discussion).

**Inference (mine).** r57's populations are finite and its sky is one shared sequence per seed, so the deterministic geometric-mean argument that a larger reserve should win under weather may not hold seed by seed. The paper predicts that a reserve-heavy (conservative) strategy with a slight geometric-mean disadvantage can still rise at large N through environmental luck, and that any genotype-level spreading of breeding (diversified) is penalised at small N. Its model has no autocorrelation and non-overlapping generations, so magnitudes do not transfer. Test: report the fate of the gate allele or gate value per seed, not just the mean over seeds; relate it to each seed's realised weather sequence and its population size.

---

## 9. Varpe, Jørgensen, Tarling & Fiksen 2009, *Oikos* (abstract only)

**According to the abstract.** A state-dependent life-history model of herbivorous zooplankton in a seasonal high-latitude environment, optimising energy allocation and diapause over the year by reproductive value, compares capital breeding (from stores) with income breeding (from current food) under early, intermediate and late feeding seasons. Capital breeding mattered most when the season was early. It made up 7–9% of eggs but 9–30% of reproductive value, because early eggs are worth more. Its main benefit was breeding before the feeding season, when an egg's reproductive value peaks; stores also topped up egg production during income breeding. Late-born individuals were predicted to take a two-year cycle. The authors stress judging capital and income breeding over the whole life cycle, and knowing how offspring value varies through the season.

**Conditions, limitations.** Not available from the abstract.

**Quote.** "The main benefit of capital breeding was reproduction prior to the feeding season" (Abstract).

**Inference (mine).** In r57, if an offspring's value depends on when in the year it is born (a seedling born at the start of summer has three bright days ahead; one born at the start of winter has three dull ones), Varpe et al. predict that selection favours carrying reserve through winter to breed at, or just before, the turn to summer. Test: the distribution of births by day of the year, against a seasons-off control; Varpe predicts births concentrated at the winter-to-summer boundary, and reserve at the end of winter higher than at the end of summer.

---

## 10. Botero, Weissing, Wright & Rubenstein 2015, *PNAS* (full text, main text)

**Question.** Which response to environmental variation (reversible plasticity, irreversible plasticity, conservative or diversified bet-hedging, adaptive tracking) evolves for a given timescale and predictability of variation, and what happens when those change?

**Method.** Individual-based simulations: 5,000 individuals, non-overlapping generations of L = 5 time steps, 50,000 generations, 100 replicates per cell. Environment E = A·sin(2πt/LR) + B·ε, where R is generations per cycle; predictability P is the correlation between a cue and the coming environment. Seven evolving loci encode plasticity, its reversibility, two reaction norms and the probability of using each. Fitness decays with the summed mismatch between phenotype and environment. A second version ties fecundity to absolute fitness to measure extinction when P or R change.

**Findings.**
- With stochastic or deterministic variation alike, plasticity evolved only when individuals could anticipate the environment; how the environment varies matters less than how predictable it is (Results, Fig. 1).
- Response modes occupy largely non-overlapping regions of (P, R): reversible plasticity when predictable and fast; irreversible plasticity as R grows; when unpredictable, adaptive tracking at large R, conservative bet-hedging at very small R, and diversified bet-hedging (two insulation phenotypes produced at random) at intermediate R, for example log R = 0.5 (Results, Fig. 2).
- Moving across a boundary between modes caused rapid collapse and often extinction, more than moving the same distance within a mode (Results, Fig. 3); the bet-hedging region had high baseline extinction, especially at low fecundity q.

**Conditions.** L = 5, kd = 0.02, ka = 0.01, τ = 0.25, q = 2.2; most runs A = 1, B = 0. Results were robust to density- and frequency-dependence and to other genotype–phenotype maps (SI, not read).

**Limitations stated.** Locations of tipping points depend on plasticity costs, life history (low q is more vulnerable) and genetic architecture (Discussion). The model assumes symmetric heat and cold stress and linear reaction norms (Methods).

**Quote.** "fundamentally different adaptive responses consistently evolve under different timescales and predictabilities of environmental variation" (Discussion).

**Inference (mine).** With a 6,000 s year and 1,000 s generations, r57's R ≈ 6 (log10 R ≈ 0.78), and with no light sense its predictability from any cue is near zero, which puts it in Botero's unpredictable row between the diversified bet-hedging seen at log R = 0.5 and adaptive tracking at large R. The exact boundary is in Fig. 2B, which I did not read numerically. Two cautions: Botero's runs lasted 50,000 generations against r57's 50, and an r57 body's own reserve level may serve as a cue to the season, which would move it toward plasticity. Test: look within clades for two distinct gate values (a diversified signature) versus one value that shifts over the year across generations (tracking).

---

## Round predictions the papers bear on

All of this section is my inference. "Supports" means the paper's result, mapped onto r57 as described above, points the same way; none of these papers modelled r57's setting.

1. **The season, not the weather, is what raises the reserve.** Supported by Fischer et al. 2011 (storage not optimal at low variability and low predictability; r57's weather has CV ≈ 0.31 and an e-folding time of about 1.4 days, both at or beyond the low end of their range) and Varpe et al. 2009 (abstract: stores pay by enabling breeding before the good season). Quantity: mean reserve, or gate height, in season-only and season-plus-weather arms against a constant-light control.

2. **Under the season, reserve is carried through winter and births bunch at the turn to summer.** Supported by Varpe et al. 2009 (abstract). Quantity: births by day of the year; reserve at the end of winter against the end of summer.

3. **Unpredictable weather adds bet-hedging: a higher gate, or reserve kept back after breeding, and earlier insurance investment.** Supported by Graham et al. 2014 (dormancy rose with bad-year frequency) and Tarazona et al. 2017 (abstract: earlier sex and lower hatching under unpredictability). Qualified by Weissman et al. 2025 (preprint: the sign can depend on population size and on each seed's luck) and Graham's small effect size. Quantity: gate and post-breeding reserve, weather-on against weather-off, per seed.

4. **Positive autocorrelation in the weather changes the reserve, direction disputed.** Metcalf & Koons 2007 predict a larger reserve (more allocation to survival with redder noise); Wieczynski et al. 2018 (abstract) predict a smaller one (tolerance disfavoured in redder environments, through a tolerance–reproduction trade-off). Fischer et al. 2011 sides with Metcalf but needs a sensed environment. **Wieczynski et al. argues against the "redder means more reserve" reading.** Quantity: gate and reserve at lag-1 0 against 0.5 with the same log s.d.

5. **Within-population variance in breeding traits is set by the season's period more than by the weather, and declines over the run.** Supported by Park & Wootton 2021, with the caution that r57's period-to-generation ratio (about 6) lies outside their tested range. Quantity: variance of the gate or age at first breeding over time, season-only against season-plus-weather.

6. **Shared weather favours conservative over diversified bet-hedging when populations are small.** Supported by Starrfelt & Kokko 2012 (abstract: correlation among individuals is a separate axis of fitness variance) and Weissman et al. (preprint: diversified hedging is penalised at small N). Botero et al. 2015 points the other way for r57's timescale: diversified bet-hedging evolved at log R = 0.5 without a cue. Quantity: bimodality of the gate within a clade, and the spread of breeding times within a genotype.

7. **Fifty generations may not reach any of these equilibria.** Supported by Park & Wootton (variance still falling after 5–6 and 10–12 generations), Graham et al. (evolved fractions may not be at equilibrium), and Botero et al. (50,000 generations). Quantity: whether the gate's trajectory has flattened by the last two years of the run.
