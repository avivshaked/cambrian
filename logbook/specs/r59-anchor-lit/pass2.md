# Round 59 anchoring cell literature, pass 2: evidence and answers

Pass 2 of the short literature round for `logbook/specs/anchoring-cell-design.md` and D161, run on 2026-10-10. Pass 1's pool is `../candidates.md`. The snowball and the targeted searches are in `snowball-log.md` beside this file. The raw rows are in `search2-log.tsv`, `c1-back.txt`, `c9-back.txt`, `c1-fwd.txt` and `c9-fwd.txt`, and the verification records with abstracts are in `verify2.json`. The helpers are `ft.py` (reads an open-access text in memory and keeps no copy), `snow.py`, `epmc.py` and `verify2.py`.

## How to read the status marks

- **FT** means the full text was read in this session. The version is named: the published version (VoR) or the author's accepted manuscript (AM) from a public repository. Locators are PDF pages of the copy read ("PDF p.N"), with the table, figure or section. The AM pages do not match the journal's pagination.
- **AO** means abstract only. The claim is what the abstract says, and nothing more.
- **SS** means the content is known only from a web search tool's summary. It is weaker than AO.
- **SEC** means the claim was read in another paper's full text, citing this one. It was not checked against the cited paper.
- **Calc** marks arithmetic I did on published numbers. **Inference** marks my reading. Neither is a finding of the paper.

Every paper below was matched on title, first author and year in at least two of CrossRef, Semantic Scholar, Europe PMC and OpenAlex (pass 1 for C-numbers, `verify2.json` for N-numbers). None carries a retraction notice or flag.

## Full texts reached and not reached

Read in full: C1 (AM, NOAA Institutional Repository), C6 (VoR, DIGITAL.CSIC), C18 (AM, DIGITAL.CSIC), C14 (VoR, Frontiers), C25 (VoR, VLIZ), C28 (VoR, Springer open access), C12 (PMC), N5 Moeser & Carrington 2006 thread mechanics (VoR, URI DigitalCommons), N13 Xu et al. 2016 *Sargassum* (PMC). N6 (PLOS) was searched for cement and attachment terms only.

Not reached, so each stays AO or SS:

- **C3** Hawkins & Bayne 1985, and every other Inter-Research (MEPS) paper (C7, C13, C16, C17, C24, C26, C31, N15 Burek 2018). int-res.com serves a "Bot check" page to scripts. I did not try to pass it. This matters for C3, below.
- **C9** Carrington et al. 2015. The eScholarship copy returned a CloudFront 403 to the script, and WebFetch got an empty page. The IRIS Palermo copy returned 403.
- **C15** Denny, Daniel & Koehl 1985. It is on JSTOR and not open. A DTIC technical report with the same title, from the same authors' Office of Naval Research contract (ADA134709), returned 403 to the script and to WebFetch.
- **C2** Roberts & Carrington 2023. ScienceDirect returned 403, and no abstract is indexed in CrossRef, Semantic Scholar, Europe PMC or OpenAlex. It stays at title and search summary.
- **C5** Moeser et al. 2006 (thread production) and C22 Martone & Denny 2008. journals.biologists.com returned 403 to the script.
- **C1's published version** at Wiley returned 403. The AM was read instead.

## Evidence table

### Q1. What attachment costs

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C1 | Roberts et al. 2021, *Funct. Ecol.* 35:1222 | FT (AM) | Cost per thread 1.01 ± 0.37 J (autumn) and 1.16 ± 0.39 J (spring) (Table 3, AM p.37). Byssus as a share of the energy budget: never severed 0.08 / 0.02, weekly 0.20 / 0.07, daily 0.47 / 0.23 (autumn / spring). As a share of non-reproductive cost: 0.24 / 0.06, 0.44 / 0.18 and 0.66 / 0.41 (Table 4, AM p.38). Severing raised thread production about fivefold and cut tissue growth by 70% (autumn) and 45% (spring) (Results 4.1, AM p.14). The share of non-reproductive cost saturates at 50–70%, with half the maximum at 6–8 threads a day (Results 4.2, AM p.15). Within a treatment, mussels that made more threads also grew more (AM p.14, Fig. S1). | Field cages under a dock, *Mytilus trossulus*, 2–3 cm (80–200 mg dry tissue), 11–15 per treatment, one month in each of two seasons. The cost per thread is the slope of growth against thread production, with standard errors near 35%. It is sensitive to the tissue energy conversion factor (AM p.15, p.18). The "never" group was tethered away from the cage, so its baseline is low by design (AM p.8). No waves and no predators. One size class. |
| C3 | Hawkins & Bayne 1985, *MEPS* 25:181 | AO; SEC ×3 | The abstract says shell and byssus took 8% and 44% "respectively" of carbon in total production, and 8% and 21% of nitrogen. Three full texts read here cite it differently: about 8% of both carbon and nitrogen into byssus (C1 AM p.6), up to 10% of monthly energy expenditure (C6 p.790, with Jordan & Valiela 1982), and 8–15% of total energy expenditure (C18 AM p.2). | Not resolved. The full text is behind the int-res bot check. *Inference:* the three citing papers agree on a single-digit to 15% share, and a byssus made of protein should take a larger share of nitrogen than of carbon, which the abstract's ordering would reverse. Byssus at about 8% and shell at 44% of carbon is the likelier reading, but it is unverified. |
| C6 | Babarro et al. 2008, *JMBA* 88:783 | FT (VoR) | After a week unfed, juveniles (26–30 mm) made 43% fewer threads (p.787) and held with 41% less force (p.788), with condition index down from 25.8 to 17.7% and glycogen down 21% (Table 1, p.785; p.786). Large mussels (83–91 mm), with 2–2.6 times the glycogen, showed no change (p.786–788). About 21% of the threads juveniles secreted never attached, because they moved before settling (p.787). | Lab, *M. galloprovincialis*, 12 per group in four 19 L tanks, flow about 0.1 cm/s on glass dishes, 7 days. One week of starvation is short for large animals, as the authors say (p.789). |
| C14 | Zheng et al. 2022, *Front. Mar. Sci.* 9:1040466 | FT (VoR) | Starved *M. coruscus* (9.1 cm) made no fewer threads. They made more on days 3–8 (Results, PDF p.4, Fig. 1A). Thread diameter fell from 7 days, thread volume by 21 days, and breaking force by 14 and 21 days. Shedding rose from day 6 (PDF p.4, Figs 1B–3). The foot's gland area shrank (Fig. 4H). The authors attribute the difference from Shang et al. 2021 (fewer threads at 26 °C) to temperature and size (PDF p.5). | Lab, 28 mussels per treatment in four 10 L tanks at 21 °C, up to 21 days. The effect sizes are in the figures only, not the text. |
| N1 | Lurman et al. 2013, *Biol. Bull.* 224:79 | AO | In *Perna canaliculus*, metabolic rate during byssus re-attachment was 1.9 times rest (27.9 against 17.1 µg O₂ h⁻¹ g⁻¹), and 2.2 times after feeding. C1 cites the same paper as "approximately 10%" more respiration during thread production (AM p.6). | 23 mussels, closed-chamber respirometry. The 1.9-fold figure is a peak rate, and C1's 10% may be an average over time. Neither is checked against the full text. |
| N3 | Clarke 1999, *J. Molluscan Stud.* 65:327 | AO | Starved zebra mussels kept putting organic carbon into threads, but the byssus they made had fewer threads. Below maintenance food levels, assimilated carbon went to byssus alone. The ratio of byssal carbon to soft-tissue carbon stayed roughly constant across all feeding levels. | *Dreissena polymorpha*, 21 days, five food levels. Freshwater, not marine. |
| N5 | Moeser & Carrington 2006, *J. Exp. Biol.* 209:1996 (thread mechanics) | FT (VoR) | New spring threads broke at about 0.19 N, against 0.07–0.12 N in other seasons (Results, Tables 1–2). Over four weeks in the field, breaking force fell 29% (spring) and 48% (summer), slopes −0.011 and −0.009 N per week, and extensibility fell 41–54% (Table 3). Thread lifetime was over 9 weeks in every season but summer, where it was under 6 (Table 3; Discussion). The authors call these decay rates underestimates. | *M. edulis*, Rhode Island. Byssal complexes on granite slabs were outplanted without the mussel, 11–29 threads per point. |
| C18 | Babarro & Carrington 2013, *JEMBE* 443:188 | FT (AM) | It cites a thread decay time of about 2–8 weeks (Carrington 2002; Moeser & Carrington 2006) and shell at 25–50% of energy (Gardner & Thomas 1987) (AM p.2, SEC). In a flume, thread production fell with flow from 3 to 52 cm/s: 20–25 threads a day in calm water down to 6–8 for 4–6 cm mussels, and a steady 14 ± 2 for 8 cm mussels up to 36 cm/s (Results, AM p.10–11). Flow also lowered threads' maximum load and extensibility (AM p.11). At the exposed site, mussels had a gonadal index about half the sheltered one, with a negative tenacity–gonad relation (AM p.9–10). | Field: about 100 mussels per site, 2–8 cm, one estuary, one month. Flume: 12 mussels per size class, 24 h trials. |
| N4 | Carrington et al. 2008, *Integr. Comp. Biol.* 48:801 | AO | Across four mussel species, thread formation fell above about 25 cm/s, with a critical threshold under 50 cm/s. Inside aggregations, flow was 0.1–10% of free stream. | Lab and field flow profiles. |
| N16 | Davies, Hawkins & Jones 1990, *J. Molluscan Stud.* 56:499 | AO | The pedal mucus term, which *Patella vulgata* uses mainly for adhesion and locomotion, was 23% of ingestion, against an earlier estimate of 4%. Animals at 70% relative humidity did not move but still secreted about 40% of the mucus of mobile animals at 100%. Animals in seawater secreted about 75%. | Measured in air and in seawater. Adhesion and locomotion are not separated in the 23%. |
| C12 | Davies & Blackwell 2007, *Proc. R. Soc. B* 274:1233 | FT (PMC) | Snails following fresh trails laid about 27% of the mucus that markers laid (Results). Citing others, the Discussion gives mucus as 7% (*Navanax*), 31% (*Patella vulgata*) and 68% (*Chiton pelliserpentis*) of consumed energy, and the Introduction gives mucus costs of locomotion at 35 times the metabolic cost (SEC). | *Littorina littorea*, lab trails, n = 10 per group. Mucus is assumed to be of constant composition. |
| C2 | Roberts & Carrington 2023, *JEMBE* 567:151927 | SS (title) | Per the title, energetic scope limits growth but not thread production. | Not read. |

### Q2. How strength scales with the structure

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C18 | Babarro & Carrington 2013 | FT (AM) | At the sheltered site, tenacity (force over shell planform area) did not vary with size from 2 to 8 cm, at 5.8 ± 1.8 × 10⁴ N m⁻² (Results, AM p.9). At the exposed site, tenacity fell with size: 59% above sheltered for 2–3.5 cm and 33% for 4–6 cm. Force rose with the square of thread thickness, on one line for both sites (Fig. 4B, AM p.10). | Pull normal to the rock with a spring scale, about 100 mussels per site, beds of about 400 per m². |
| C6 | Babarro et al. 2008 | FT (VoR) | Large mussels held 2.21 N against 0.78 N for juveniles (×2.8), on 6 times the shell area (27.6 against 4.6 cm²), so juveniles had twice the tenacity: 0.17 against 0.08 × 10⁴ N m⁻² (p.788). Force rose linearly with thread number, at 0.013 N per thread for juveniles and 0.048 N for large mussels (r = 0.73 both, p.788). Distal thread diameter was 0.067 and 0.193 mm (Table 1, p.785). The ×2.8 in force matches the ×2.9 in thread diameter (p.789). | 7 days in near-still water, 27–46 threads. *Calc:* per unit of thread cross-section, 0.013 N on 0.0035 mm² is about 3.7 MPa, and 0.048 N on 0.029 mm² is about 1.6 MPa. Per-thread force grew with diameter, not with cross-section, across this size range. |
| N5 | Moeser & Carrington 2006 | FT (VoR) | Single threads break at 0.07–0.20 N, depending on season (Tables 1–2). Tenacity changed seasonally through thread quality more than thread number (Discussion). | As above. |
| C25 | Hennebert et al. 2010, *J. Exp. Biol.* 213:1162 | FT (VoR) | Tube-foot tenacity on glass was 0.24 and 0.43 MPa, footprint area 0.6–0.7 mm², detachment force about 0.1 N per foot, and estimated maximum 0.15 and 0.35 N (Table 4, PDF p.8). The stem breaks at about 21–23 MPa (Table 2). Citing others, tenacity on mussel shell is about a third of that on glass, and whole sea stars hold with tens to hundreds of newtons (Discussion, PDF p.9–10, SEC). | Hand-held force gauge, 30–40 feet from 5–11 animals. The footprint left at each release is adhesive left behind (Methods). |
| N13 | Xu et al. 2016, *J. Appl. Phycol.* | FT (PMC) | In immature *Sargassum horneri*, dislodgement force was linear in holdfast basal area (r² = 0.86 in January and 0.81 in February, 0.67 pooled), and unrelated to thallus length or wet weight (Results, Figs 3, 4 and 6). In mature thalli the relation disappeared and attachment declined after reproduction (Discussion). | Field pulls with a spring scale, 20–40 thalli a month, one cove, December to May. |
| C21 | Milligan & DeWreede 2000, *JEMBE* 254:189 | AO (pass 1) | Adult kelp holdfasts held about 100 N, about 0.07 MN m⁻² per area. | Not reread. |
| C24 | Santos & Flammang 2007, *MEPS* 332:129 | AO (pass 1) | Urchin tenacity per adhesive area 0.12–0.37 MPa. More tube feet were used as waves grew. | int-res bot check. |
| C15 | Denny, Daniel & Koehl 1985, *Ecol. Monogr.* 55:69 | AO (pass 1) | Velocity forces (drag and lift) grow in proportion to structural strength as an organism grows. Acceleration forces grow faster and can limit size. | Not reached. *Inference:* both drag and strength scale with area (length squared) in that argument. Acceleration force scales with volume. |
| N15 | Burek et al. 2018, *MEPS* 600:3 | AO | Kelp attached to turf put a larger share of biomass into the holdfast, yet held less firmly than kelp on rock. | int-res bot check. It shows a holdfast's grip depends on the substrate, not only its size. |

### Q3 and Q4, briefly

| # | Paper | Status | Finding with locator |
|---|---|---|---|
| C28 | Widdows et al. 2002, *Helgol. Mar. Res.* 56:3 | FT (VoR) | Clearance rate of *M. edulis* held steady from 0.05 to 0.8 m/s and fell above 0.8 m/s, from 3.3 to 1.8 L h⁻¹ (Results, PDF p.4–5). Below 0.05 m/s, algae ran short above the mussels: the depleted layer was 5 cm deep at 0.02 m/s, 10 cm at 0.008 m/s and 15 cm at 0.004 m/s (Results, Fig. 2). Mussels buried by 6 cm of sediment were back at the surface in 1–2 days (Abstract). |
| C1 | Roberts et al. 2021 | FT (AM) | Re-attaching daily cost 23–47% of the energy budget against 2–8% undisturbed (Table 4). |
| C6 | Babarro et al. 2008 | FT (VoR) | Attached thread number levelled off between days 3 and 4 (72 h). Juveniles had 28 threads by day 2 (p.786). Unfed juveniles levelled off by 24 h, at a lower count (p.787). |
| N2 | Lee et al. 1990, *Can. J. Zool.* 68:2005 | AO | In still water, large mussels re-attach faster and with more threads. In currents of 10–20 cm/s, small mussels re-attach faster. |
| C13 | Young 1985, *MEPS* 24:261 | AO (pass 1) | Up to 15.8 threads a day under agitation. Recovery after stress took up to 2 weeks. |
| N7 | Sivasundarampillai et al. 2023, *Science* | AO | Mussels can jettison the whole byssus and rebuild one in hours. Release is controlled by cilia at the stem, under neurochemical control. |
| C14 | Zheng et al. 2022 | FT (VoR) | Shedding rose under starvation from day 6. The authors read it as letting go to move (PDF p.4, p.7). |
| N8 | Vasquez et al. 2025, *J. Mar. Sci. Eng.* 13:874 | AO | Pearl oysters trimmed every 3–5 days made more threads, and small ones shed the whole byssus more often. |
| N1 | Lurman et al. 2013 | AO | Feeding raised metabolic rate 2.2-fold, and feeding with re-attachment raised it higher still. |

## Answers per design choice

### A cost to build a grip

**Supported, strongly, for byssus.** Measured: about 1 J per thread in a 2–3 cm mussel (C1). Building is the cost that moves with the environment: re-attaching daily took 23–47% of the energy budget, against 2–8% undisturbed (C1). Re-attachment also raises metabolic rate while it happens (N1, AO). Juveniles waste about a fifth of the threads they make while they look for a place (C6).

*Calc, from C1's numbers.* C1's maintenance coefficient is 0.081 J per mg dry tissue per day in autumn and 0.158 in spring (AM Table 2). A 140 mg mussel spends about 11–22 J a day on upkeep, so one thread costs roughly 0.05–0.1 day of that upkeep. A full byssus of about 40 threads, the plateau in C6, costs about 2–4 days of it. Mixing species (C1's cost, with C6's and N5's 0.01–0.2 N per thread) puts the price at roughly 5–100 J per newton of grip. The range is wide because thread strength varies with season and size. *Inference:* a charge at the moment of gripping, scaled with the grip's breaking force, is the best-supported cost in the design.

### A cost per second held

**Not supported as a separate measured term for byssus.** Byssus is a dead structure outside the body. Nothing found measures a running cost of being attached with it. The costs found are building, re-building and replacing decayed threads. This confirms pass 1's reading.

**Partly supported for adhesive mucus.** A limpet's pedal mucus, used for adhesion and locomotion together, took 23% of ingestion (N16, AO). Limpets that did not move still secreted about 40% of the mucus of moving ones. *Inference:* that is the nearest thing found to a cost per second of holding, but it was measured in air, it mixes adhesion with crawling, and it is abstract only.

*Inference for the design.* A cost per second is defensible only as the running average of replacing a grip that decays. If the cell's standing cost per cubic metre already charges for having the cell, a per-second holding fee on top charges twice unless it stands for decay replacement. Pricing it as the build cost divided by the grip's lifetime ties it to evidence. Real threads last about 6 to more than 9 weeks (N5), so the replacement rate is about 1/40 to 1/60 of a full byssus a day. *Calc:* at C1's undisturbed production, that rate falls in the 2–8% of the budget C1 measured, which is a consistency check, not an independent measurement.

### Upkeep by decay

**Supported, moderately.** Measured: field-exposed threads lost 29–48% of their breaking force and 41–54% of their extensibility over four weeks in spring and summer. Lifetimes ran from under 6 weeks in summer to over 9 weeks in other seasons (N5, FT, one site). C18 cites 2–8 weeks (SEC). *Inference:* a grip that weakens over time unless the body pays to renew it matches the evidence better than a fixed fee. That is the design's choice if it wants one mechanism. The time scale must be compressed to the world's clock, and the evidence gives only the ratio of lifetime to building time (weeks against about 3 days for a full byssus, C6).

### Strength by area or by volume

**Area is supported. Volume has no support.** Measured:

- Mussel tenacity per shell planform area was constant from 2 to 8 cm in a sheltered population, so force grew as length squared (C18).
- Holdfast force was linear in holdfast basal area in immature *Sargassum*, and unrelated to thallus mass (N13).
- A tube foot's force is its tenacity times its adhesive area, and a sea star's grip is the sum over its feet (C25).
- A mussel's grip is linear in thread number, and rises with thread cross-section (C6, C18).

Against strict area: in near-still water, large mussels held 2.8 times the force on 6 times the area (C6), and exposed mussels lost tenacity with size (C18). Grip grew more slowly than area in both. No paper found relates breaking force to the volume of an attachment organ.

*Inference:* if the cell's size is its volume V, a breaking force proportional to V^(2/3) follows the evidence. Force proportional to V would make large anchors far stronger than any real grip. Given C15's argument, also inferred from its abstract, area scaling keeps grip in step with drag as a body grows. If the build cost scales with volume and strength with area, large anchors buy less grip per unit of energy. That is what C6's and C18's falling tenacity with size show.

Per-area values for scale (each normalised by a different area): mussels 0.8–5.8 × 10⁴ N m⁻² of shell planform (C6, C18), kelp holdfast about 7 × 10⁴ N m⁻² (C21, AO), tube feet and urchins 1.2–4.3 × 10⁵ N m⁻² of adhesive contact (C24 AO, C25).

### A weaker grip under starvation

**Supported, with a lag and a size dependence.** Measured: a week unfed cut thread production by 43% and grip by 41% in juveniles, but not in large mussels with more reserves (C6). In large starved *M. coruscus*, thread number held or rose, while threads grew thinner within 7 days and weaker by 14–21 days, and shedding rose from day 6 (C14). Starved zebra mussels kept building, but made a byssus with fewer threads (N3, AO). Mussels put byssus ahead of growth (C1; C2 by its title).

*Inference:* the real pattern is that the grip is paid for first, ahead of growth. It weakens only as the reserve runs down, sooner in small bodies, and starving animals let go more often. The design's release when the reserve reaches zero is consistent with that. A grip whose strength, or whose renewal, falls with the reserve fraction before zero is supported too. A grip that weakens at once when food is short is not supported.

## Q3 and Q4 in brief

- **When holding pays.** Measured: mussel feeding was flat from 5 to 80 cm/s, and the food supply ran short only below about 5 cm/s, worst near the bed (C28). *Inference:* the world's currents of 3–6 cm/s sit at the edge where refilling begins to matter, which is where the nursery's earlier measurement saw it (5–11% still, 20–25% moving). Building threads is inhibited above about 15–25 cm/s (C5 AO, N4 AO, C18 flume). Real anchors make less grip, not more, as flow rises, and rely on aggregations to shelter them. Paying for grip trades against reproduction across sites and seasons (C18; C4 and C7 from pass 1, AO).
- **Release and re-attachment.** Measured: a full byssus is built in about 3 days, and mussels can jettison one and start a new one within hours (C6; N7 AO). Repeating it daily costs up to about half the budget (C1). Small mussels re-attach faster in current, large ones in still water (N2, AO). Buried mussels re-emerge in 1–2 days (C28). Tube feet leave adhesive behind at each release (C25), with no energy price found. *Inference:* release itself is cheap and quick. The cost is in rebuilding.

## What remains thin

1. **Costs of attachment outside mussels.** One snowball round, backward and forward from C1 and C9, found none for barnacle cement, macroalgal holdfasts, anemone pedal discs or tube feet. The targeted searches added only limpet mucus (N16, abstract only), and a holdfast biomass-share result without energy (N15, abstract only). The only barnacle energy budget found (N6, *Balanus glandula*) has no cement term.
2. **A cost per second of holding.** None measured for byssus. Limpet mucus is the only proxy, abstract only, and mixed with crawling.
3. **C3's 44% against 8%.** Unresolved until the full text is read. It sits behind Inter-Research's bot check, which a person can pass in a browser.
4. **Volume scaling.** No evidence for volume at all.
5. **Acceleration forces and lift.** Only C15 and C23, both abstract only. The world has no waves, so this matters little.
6. **Abstract only for several anchors.** C9, C15, C2, C3, N1, N2, N3, N7, N16 and every Inter-Research paper are abstract or summary only. C1's cost per thread rests on one size class over one month, with standard errors near 35%.
7. **Time scales.** Real costs are per day and per week. The evidence gives ratios (build time against lifetime, cost against maintenance), not seconds, so the nursery still has to set the absolute values.
