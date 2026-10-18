# Round 52: the literature behind the lifespan gene's values

*2026-09-29. The report of a read-only research subagent (Opus 5.5), briefed with D128 and the
gene as built, saved as it came except for this header. It is D128's review round's evidence and
not yet a round of research/LITERATURE-REVIEW.md, which is owed. The agent wrote no file; the web
tool cached two unreadable PDFs (the Winder bioRxiv v2 and the Houle 1996 scan) in the session's
tool-results directory outside the project, and nothing of them is kept here. Page numbers were not
visible for the PMC and Europe PMC items, so claims cite sections, tables and figures. The
screen that sets these against this world's own life history is `scripts/reads/r52-lifespan-screen.py`.*

## Summary (the agent's)

- **f = 0.2:** inside the natural range, at its shallow end (laboratory and captive species read about 0.1).
- **One doubling per parent-sized child:** about 5 to 25 times the survival cost measured in birds, depending on how "child size" maps onto a parent's investment.
- **The repair exponent a:** no source gives it; the theory requires only a > 0. Proposed band 0.25 to 1, point 0.5, a judgement to settle on the ledger.
- **c = 0.08:** at the fast edge of measured mutational variance, defensible as a deliberate speed-up.
- **Founders:** [0.5, 1.5] proposed.

## A. Sources

"EPMC" is the Europe PMC REST abstract record.

| Key | Citation | URL | What was read | Claim used (locator) |
|---|---|---|---|---|
| GOM1825 | Gompertz B (1825) Phil Trans R Soc 115:513–583, doi 10.1098/rstl.1825.0026 | royalsocietypublishing.org/rstl/article/doi/10.1098/rstl.1825.0026 | Secondary only: Kirkwood 2015 commentary, PMC4360127 | μ(x)=α·e^(βx); "a deterioration, or an increased inability to withstand destruction", attributed to p.517; humans double about every 8 y (Sacher: 3,100 days) |
| FPW1990 | Finch CE, Pike MC, Witten M (1990) Science 249:902–905 | EPMC PMID 2392680 | Abstract | Rhesus doubling about 8 y, like humans, the shorter life "mainly from higher mortality at all ages"; rodents and galliform birds shorter; "certain birds have MRDs that are as long as that of humans" |
| MMA2012 | McDonald P, Maizi BM, Arking R (2012) Exp Gerontol 48:240–249 | PMC3899347 | Full text, through a summariser | Control Ra females at 25 °C: doubling "∼4.73 days" (§3.3.1); median about 48 d (Fig 1A, 3A) |
| HH2016 | Hughes BG, Hekimi S (2016) Genetics 204:905–920 | PMC5105868 | Abstract, parts of the full text | Mouse life-extending mutations change A not G; worm mutations lower G; N2 G 0.34–0.45/d at median 18–20 d (Table 2); mouse medians 550–960 d; the mouse G's unit not stated |
| JON2014 | Jones OR et al. (2014) Nature 505:169–173 | PMC4157354 | Abstract, full text through a summariser | Increasing, constant, decreasing, humped and bowed shapes; terminal over mean mortality ">20 times" (Japanese women) to "less than half" (white mangrove); shape against pace Spearman −0.13, n.s.: "two orthogonal axes" |
| K1977 | Kirkwood TBL (1977) Nature 270:301–304 | EPMC PMID 593350 | Abstract | "energy-saving strategy of reduced error regulation in somatic cells" |
| KH1979 | Kirkwood TBL, Holliday R (1979) Proc R Soc B 205:531–546 | EPMC PMID 42059 | Abstract | Accuracy "involves the expenditure of energy"; less somatic accuracy speeds development and reproduction; longer life with slower growth and reproduction |
| KR1991 | Kirkwood TBL, Rose MR (1991) Phil Trans R Soc B 332:15–24 | EPMC PMID 1677205 | Abstract | Senescence requires "(ii) the acquisition of greater longevity usually involves some cost"; less invested in the soma "than is necessary for indefinite survival" |
| DK2005 | Drenos F, Kirkwood TBL (2005) Mech Ageing Dev 126:99–103 | EPMC PMID 15610767 | Abstract | Optimal maintenance "less than what would be required for indefinite longevity" |
| C1997 | Cichoń M (1997) Proc R Soc B 264:1383–1388 | PMC1688586 | Abstract | Low extrinsic mortality and efficient repair favour allocation to repair |
| SPK2005 | Speakman JR (2005) J Exp Biol 208:1717–1730 | EPMC PMID 15855403 | Abstract | Lifespan ∝ mass^0.15–0.3, resting metabolism ∝ mass^0.66–0.8; lifetime energy per gram not constant |
| PT-sec | Waterlow & Millward (1989), via NCBI Bookshelf NBK224633 | NBK224633 | Search snippet only (captcha) | Protein turnover about 20% of basal metabolism; other estimates 10–25% |
| R1984 | Rose MR (1984) Evolution 38:1004–1010 | EPMC PMID 28555803 | Abstract, no numbers | Postponed senescence in late-reproduced lines |
| L1984 | Luckinbill LS et al. (1984) Evolution 38:996–1003 | EPMC PMID 28555795 | Metadata | Secondary: lifespan "as much as 50%" longer |
| ZBH1995 | Zwaan B, Bijlsma R, Hoekstra RF (1995) Evolution 49:649–659 | EPMC PMID 28565142 | Abstract | Long-life lines produced fewer progeny "throughout their whole life" |
| SADK2000 | Stearns SC, Ackermann M, Doebeli M, Kaiser M (2000) PNAS 97:3309–3313 | PMC16235 | Full text, through a summariser | High adult mortality evolved higher intrinsic mortality and shorter life; early fecundity (days 13–15) 40.8 against 27.0 eggs/day; higher intrinsic mortality days 40–75 (Fig 3) |
| KC2013 | Khazaeli AA, Curtsinger JW (2013) J Gerontol A 68:546–553 | EPMC PMID 23160365 | Abstract | A QTL adding "20%" to lifespan "has no detectable effect on early fecundity"; genetic correlation about 0 |
| W2013 | Wit J et al. (2013) Exp Gerontol 48:349–357 | EPMC PMID 23353929 | Abstract | Longevity-selected lines showed no fecundity trade-off |
| P2020 | Parker GA et al. (2020) G3 10:1087–1098 | academic.oup.com/g3journal/article/10/3/1087 | Parts of the full text | "The O lines live twice as long as the B lines"; little difference in young flies' offspring |
| GPP2009 | Grandison RC, Piper MD, Partridge L (2009) Nature 462:1061–1064 | EPMC PMID 19956092 | Abstract | Methionine restores fecundity under dietary restriction without shortening life |
| JML2004 | Jenkins NL, McColl G, Lithgow GJ (2004) Proc R Soc B 271:2523–2526 | EPMC PMID 15590605 | Abstract | Long-lived daf-2 worms carry a "heavy fitness cost" |
| PF1981 | Partridge L, Farquhar M (1981) Nature 294:580–582 | doi 10.1038/294580a0 | Abstract | Male sexual activity shortens life |
| FP1989 | Fowler K, Partridge L (1989) Nature 338:760–761 | Secondary | Snippet | Mating shortens female life at equal egg output |
| F2011 | Flatt T (2011) Exp Gerontol 46:369–375 | EPMC PMID 20970491 | Abstract | Trade-offs widespread but can be uncoupled |
| BBV2020 | Boonekamp JJ, Bauch C, Verhulst S (2020) J Anim Ecol 89:1395–1407 | PMC7317873 | Full text, through a summariser | Jackdaws, lifelong brood manipulation: Gompertz b₁ 0.16 (SE 0.05) reduced against 0.25 (SE 0.06) enlarged, b₀ equal (Table 2); broods 1.86 against 5.25, 1.46 against 2.91 at day 30 (§2.2); remaining life 4.7 against 3.8 y (Fig 2, Table 2) |
| BSBDV2014 | Boonekamp et al. (2014) Ecol Lett 17:599–605 | research.rug.nl | Secondary | Actuarial senescence "threefold higher" in enlarged broods |
| WSB | Winder LA, Simons MJP, Burke T, bioRxiv 2022.05.30.493969 v4; eLife RP87018 | biorxiv.org/content/10.1101/2022.05.30.493969v4.full | Parts of the full text, through a summariser | Experimental effect "−0.05" lnOR per egg; an extra offspring "a 5%, 14% and 22% … reduction in its survival odds"; fitness landscape "flat" |
| SN2012 | Santos ESA, Nakagawa S (2012) J Evol Biol 25:1911–1917 | Secondary: McKinnon et al. 2026 Biol Rev, PMC13542356 | Secondary | Survival cost in males only (about 25%), none in females |
| GP1990 | Gustafsson L, Pärt T (1990) Nature 347:279–281 | Metadata | Secondary | Early reproductive cost lowers later clutch size, not survival |
| DDD1996 | Daan S, Deerenberg C, Dijkstra C (1996) J Anim Ecol 65:539–544 | JSTOR 5734 | Metadata | Exists; no numbers seen |
| PHC1998 | Pletcher SD, Houle D, Curtsinger JW (1998) Genetics 148:287–303 | EPMC PMID 9475740 | Abstract | Mutational variance in mortality significant early (to about 30 d), not different from zero late |
| HML1996 | Houle D, Morikawa B, Lynch M (1996) Genetics 143:1467–1483 | PMC1207413 | Abstract (the PDF is an unreadable scan) | Life-history median CV_M "more than six times" morphology's |
| CBM | Conradsen C, Blows MW, McGuigan K, bioRxiv 2021.07.07.451460; Genetics 221:iyac060 | biorxiv v3 | Parts of the full text | V_M/V_E 2.50e-5 to 1.02e-2; CV_M 0.13 to 7.32 (percent by the usual convention: inference) |
| RUBY2018 | Ruby JG et al. (2018) Genetics 210:1109–1124 | EPMC PMID 30401766 | Abstract | Human longevity heritability "well below 10%" |
| FT-sec | Finch CE, Tanzi RE (1997) Science | Secondary snippet | Snippet | Lifespan h² about 10–50% flies, 34% worms, 20–30% mice, 10–35% humans |
| A1993 | Abrams PA (1993) Evolution 47:877–887 | EPMC PMID 28567901 | Abstract | Density-independent: extrinsic mortality does not change the favoured senescence; density-dependent: any direction, Williams holds when density acts on fertility |
| MPS2019 | Moorad J, Promislow D, Silvertown J (2019) TREE 34:519–530 | PMC6746179 | Full text, through a summariser | Age-independent mortality "does not alter selection against age-specific mortality" (Box 2); Williams' model is "wrong" |
| C2007 | Caswell H (2007) TREE 22:173–174 | EPMC | Metadata | Exists |
| H1966 | Hamilton WD (1966) J Theor Biol 12:12–45 | EPMC PMID 6015424 | Metadata | Exists |
| RP2010 | Ronce O, Promislow D (2010) Proc R Soc B 277:3659–3667 | EPMC PMID 20591867 | Abstract | Limited dispersal and kin competition weaken selection on survival |
| RBRG2004 | Reznick DN et al. (2004) Nature 431:1095–1099 | EPMC PMID 15510147 | Abstract | High-mortality guppies did not senesce earlier |
| RS2002 | Ricklefs RE, Scheuerlein A (2002) J Gerontol A 57:B69–B76 | academic.oup.com | Abstract | The Gompertz rate is sensitive to initial mortality |

Medawar (1952) and Williams (1957) were not retrieved; they are known here through Moorad's account.

## B. The seven questions

**Q1, the Gompertz law and f.** Findings: humans and rhesus double about every 8 y (FPW1990); flies 4.73 d on a median of about 48 d (MMA2012); worms G 0.34–0.45/d, a doubling of 1.5–2.0 d on medians of 18–20 d (HH2016 Table 2); jackdaws b₁ 0.16/y, a doubling of 4.3 y, 2.8 y in enlarged broods (BBV2020). The rodent "0.3 y" doubling appears only in a search summary. Inference, f = doubling over median: humans 0.10, flies 0.10, worms 0.08–0.11, mice 0.11–0.20, rhesus about 0.3 (captive median unverified); wild birds confound extrinsic death (RS2002). Under Gompertz the SD of age at death is about 1.85 doublings, so f = 0.2 gives a CV of about 0.37 and f = 0.1 about 0.19. Verdict: 0.2 is natural, at the shallow end; D128 chose it to keep some long lives, a judgement and not a literature value.

**Q2, disposable soma.** Findings: the cost is energy for accuracy (K1977, KH1979); some cost of longevity is a condition of senescence (KR1991); the optimum lies below the maintenance that would give indefinite survival (KR1991, DK2005); low extrinsic mortality favours repair (C1997); protein turnover about 10–25% of resting metabolism (secondary); across species longer-lived animals do not pay more per unit time (SPK2005). Inference: the theory prescribes no power law; L^a prices every step and guarantees an interior optimum, a modelling choice. At a = 0 condition (ii) fails and L rises until age deaths are rare, when selection on it vanishes: D038's reason holds. If a repair share m of upkeep scales as L^b, a ≈ m·b near L = 1; m 0.1–0.25 and b 1–2 give 0.1–0.5, on thin evidence.

**Q3, an elasticity from selection experiments.** Costs: fewer progeny throughout life (ZBH1995); high-mortality lines laid 51% more early eggs (SADK2000). No cost: +20% lifespan, no early-fecundity effect (KC2013); no trade-off (W2013); O lines twice as long-lived with little early difference (P2020); the dietary-restriction trade-off uncoupled by methionine (GPP2009). No primary ΔL against ΔF from Rose or Luckinbill. Inference: elasticities about 0 to perhaps 0.5. With net = I − U·L^a, e = a·u/(1−u), so a = e·(1−u)/u: e 0.25 at u 0.33 gives 0.5, and at u 0.5, a = e.

**Q4, Jones et al. 2014.** Shapes range from terminal over mean mortality above 20 to below 0.5, uncorrelated with pace. Inference: a fixed f with only L heritable is a simplification; a heritable f is defensible later. Pace varies over orders of magnitude, so the founders' range is bounded by comparability with round 51, not by nature. Mouse mutants and rhesus change A and not G; this gene stretches both, the worm pattern.

**Q5, the cost of a birth.** Jackdaws: b₁ up 0.09/y (0.13 doublings a year) for +3.39 chicks a year, or +1.45 surviving at day 30; remaining life 4.7 → 3.8 y (BBV2020). Birds meta-analysis: −0.05 lnOR per egg, 5% of survival odds, or 14% and 22% under other expressions (WSB); small (SN2012). Flycatchers pay in later fecundity (GP1990). Arithmetic (inference): a parent-sized child here costs 600 s at L = 1, about 20% of the median life; jackdaws 0.04–0.09 doublings a chick (0.08–0.18 counting half a chick a parent); Winder 0.05 to 0.25 doublings, one-year effects. Verdict: nature reads about 0.05–0.25 doublings per parent-sized offspring, so one doubling is about 4 to 25 times high. Caveat: a bird's cost covers rearing to adult size, and this world's s is the size at birth, after which the child grows itself; read as egg-sized investment (5–15% of parent mass), s = 0.1 is close to the bird numbers. The mapping is the owner's.

**Q6, heritability and mutational variance.** Human longevity h² below 10% (RUBY2018); animals about 0.1–0.5 (secondary). Mutational variance in mortality early only (PHC1998). V_M/V_E 2.5e-5 to 1.02e-2, CV_M 0.13–7.32 (CBM); life-history CV_M more than six times morphology's (HML1996). Arithmetic: CV_M = 15√c %, 4.2% at c = 0.08, 2.1% at 0.02; V_M/V_E ≈ 0.0225c/(1.85f)², 0.164c at f = 0.2, 0.013 at c = 0.08, just above the measured top (starvation adds to V_E and lowers it). Design note: a Gaussian step on L, not ln L, drifts ln L down by about c·σ²/2 = 0.0009 a birth at c = 0.08, about −9% of the geometric mean per 100 generations, which weak selection must overcome.

**Q7, theory for the predictions (inference throughout).** Age-independent extrinsic death leaves selection on L unchanged (MPS2019; A1993 density-independent). This world is density-regulated through food: if food limits breeding, Williams' direction holds and more extrinsic death favours shorter L (A1993); if starvation falls on the frail or the young, it can reverse (MPS2019). Newborns near their parents raise kin competition, favouring shorter L (RP2010). The one direct experiment moved lifespan down under high adult mortality (SADK2000); guppies are a counter-case (RBRG2004). With a > 0, L < 1 saves upkeep, so the gene can fall; a plain prediction is that L drifts down or stays near 1 unless age deaths cost prolific leaves much reproduction.

## C. The agent's proposal (the owner decides)

| Value | Range supported | Point | Basis |
|---|---|---|---|
| a | 0.25–1 | 0.5 | Theory needs a > 0; energy-share arithmetic 0.1–0.5; fly elasticity 0 to about 0.5; a judgement, to screen at 0.25, 0.5 and 1 on the ledger |
| c | 0.02–0.1 | 0.08 | CV_M 4.2% inside the measured range; V_M/V_E ≈ 0.013 at its top, a deliberate speed-up |
| [Lmin, Lmax] | [0.75, 1.25] to [0.5, 2] | [0.5, 1.5] | Keeps the founders' mean at round 51's 1; [0.5, 2] biases it to 1.25; a judgement |
| f | 0.1–0.3 | keep 0.2, or 0.1 | 0.2 shallow but natural |
| birth advance | 0.05–0.25 doublings a parent-sized investment | 0.15, or keep 1 with s the size at birth if s is small | The bird data, with the mapping caveat |

## D. Not verified

The Gompertz 1825 primary text; full texts of Kirkwood 1977 and 1979, Kirkwood & Rose, Drenos & Kirkwood; the numbers in Rose 1984 and Luckinbill 1984; Fowler & Partridge's and Partridge & Farquhar's group figures; Daan 1996's survival numbers; Santos & Nakagawa's effect size beyond a second-hand figure; the Houle 1996 medians; the Waterlow protein-turnover share; the mouse G's unit; the rodent 0.3 y doubling; the rhesus captive median.