# Round 59 anchoring cell literature, pass 2: snowball and search log

Run on 2026-10-10. There was one Wohlin snowball iteration, backward and forward, from the start set C1 (Roberts et al. 2021) and C9 (Carrington et al. 2015). Its aim was costs of attachment outside mussels. A few targeted searches followed, because the snowball found none. The evidence drawn from all of this is in `pass2.md`.

## Backward

| Seed | Source | References deposited | Raw list |
|---|---|---|---|
| C1 | CrossRef reference deposit, each DOI resolved in CrossRef | 106 | `c1-back.txt` |
| C9 | The same | 135 | `c9-back.txt` |

OpenAlex returned HTTP 429 to the batch title lookup, so the titles were resolved one DOI at a time in CrossRef.

Screen: keyword filter on titles (cost, energ, budget, byss, attach, adhes, cement, holdfast, tube f, pedal, barnacle, decay, thread, tenac, dislodg, starv, respir, anchor), then a read of each title that passed.

**Outside mussels: none.** Every attachment-related reference in both lists is about mussel or bivalve byssus. The only non-mussel structural costs in C1's list are cactus spines (Crofts & Anderson 2018) and shell calcification (Sanders et al. 2018), and neither is an attachment.

Mussel references taken forward for the pricing questions: Lurman et al. 2013, Lee et al. 1990, Clarke 1999, Carrington et al. 2008, Moeser & Carrington 2006 (thread mechanics), Sebens 2002, Bell & Gosline 1996 and Schneider et al. 2004. Each one already in pass 1's pool was not re-added.

## Forward

| Seed | Source | Citing works | Raw list |
|---|---|---|---|
| C1 | Semantic Scholar citations API | 28 | `c1-fwd.txt` |
| C9 | The same | 107 | `c9-fwd.txt` |

OpenAlex's `cites:` filter returned HTTP 429, so Semantic Scholar was used. Screen: the same keyword filter on titles, then a read of each title that passed.

**Outside mussels: one candidate, which proved empty.** Roberts et al. 2025, *PLOS Climate* (*Balanus glandula* bioenergetics), was searched in full for "cement", "attach", "adhes" and "byss". Its only hits are the field method for gluing loggers and a citation of C1. It has no cement or attachment term, so it was excluded.

Mussel or bivalve works taken forward: Sivasundarampillai et al. 2023 (*Science*, release of the byssus), Vasquez et al. 2025 (pearl oyster shedding) and Zheng et al. 2022 (already C14). Roberts & Carrington 2023 is already C2.

**Saturation.** The round yielded no new non-mussel attachment cost. By the skill's rule, a round that yields nothing relevant stops the snowball for that question.

## Targeted searches (not part of the snowball)

| Tag | Engine | Query | Count | What it gave |
|---|---|---|---|---|
| S1 | Europe PMC | (barnacle) AND (cement OR adhesive) AND (energetic OR "energy budget" OR cost OR "metabolic cost") | 208 | Materials and antifouling work only. No cost of cement. |
| S2 | Europe PMC | (holdfast) AND (allocation OR biomass OR "energetic cost" OR cost) AND (kelp OR macroalga* OR seaweed) | 300 | Holdfast fauna and kelp harvesting. Nothing on cost. |
| S3 | Europe PMC | ("sea anemone" OR actinian) AND ("pedal disc" OR "pedal disk" OR locomotion OR detachment) AND (energ* OR cost OR mucus) | 340 | Nothing on the cost of adhesion. White et al. 2026 (*PeerJ*, energy reserves of pedal lacerates) is off target. |
| S4 | Europe PMC | ("tube feet" OR "tube foot" OR podia) AND (adhes*) AND (cost OR energ* OR footprint) | 229 | Adhesive proteins and forces only. No cost. |
| S5 | Europe PMC | (limpet OR Patella) AND mucus AND (energy OR "energy budget" OR cost) AND (adhesion OR clamping OR attachment OR locomotion) | 300 | Kang et al. 2020 (limpet adhesive mucus proteins), with no cost figure in its abstract. |
| W1 | WebSearch | barnacle cement production energetic cost proportion of energy budget | – | No cement cost. The summary gave a *B. glandula* budget with shell at 6–7% and no cement term, from an unnamed source. Not carried. |
| W2 | WebSearch | Davies Hawkins Jones 1990 mucus production physiological energetics Patella vulgata | – | Led to N16, verified below. |
| W3 | WebSearch | kelp holdfast biomass allocation proportion of total biomass size scaling attachment strength | – | Led to N15 (Burek et al. 2018). |
| W4 | WebSearch | sea anemone pedal disc adhesion mucus energetic cost detachment locomotion Actinia | – | No measured cost. Only qualitative detachment behaviour. Nothing carried. |
| W5 | WebSearch | macroalgae holdfast attachment strength scales with holdfast area "N m-2" breaking force holdfast size | – | Led to N13 (Xu et al. 2016, *Sargassum*). |

Europe PMC counts are for the Boolean query, sorted by relevance, with the top 12 inspected. Europe PMC's relevance ranking was poor for these queries, so a count says little about coverage.

## New papers verified in pass 2

The records are in `verify2.json`. "DBs" lists where title, first author and year matched: CR is CrossRef, S2 is Semantic Scholar, EPMC is Europe PMC and OA is OpenAlex. None carries a retraction notice or flag.

| ID | Paper | DOI | DBs | Read |
|---|---|---|---|---|
| N1 | Lurman GJ et al. 2013, Energetics of byssus attachment and feeding in the green-lipped mussel *Perna canaliculus*, *Biol. Bull.* 224:79–88 | 10.1086/BBLv224n2p79 | CR, S2, EPMC, OA | Abstract |
| N2 | Lee CY et al. 1990, The rate and strength of byssal reattachment by blue mussels, *Can. J. Zool.* 68:2005–2009 | 10.1139/z90-282 | CR, S2, OA | Abstract |
| N3 | Clarke M 1999, The effect of food availability on byssogenesis by the zebra mussel, *J. Molluscan Stud.* 65:327–333 | 10.1093/mollus/65.3.327 | CR, S2, OA | Abstract |
| N4 | Carrington E et al. 2008, Mussel attachment on rocky shores: the effect of flow on byssus production, *Integr. Comp. Biol.* 48:801–807 | 10.1093/icb/icn078 | CR, S2, EPMC, OA | Abstract |
| N5 | Moeser GM, Carrington E 2006, Seasonal variation in mussel byssal thread mechanics, *J. Exp. Biol.* 209:1996–2003 | 10.1242/jeb.02234 | CR, S2, EPMC, OA | Full text (URI DigitalCommons) |
| N6 | Roberts EA et al. 2025, Bioenergetic modeling ... *Balanus glandula*, *PLOS Climate* 4:e0000540 | 10.1371/journal.pclm.0000540 | CR, S2, OA | Keyword search of full text; excluded |
| N7 | Sivasundarampillai J et al. 2023, A strong quick-release biointerface in mussels ..., *Science* 382:829–834 | 10.1126/science.adi7401 | CR, S2, EPMC, OA | Abstract |
| N8 | Vasquez HE et al. 2025, Adaptive responses in byssal growth and shedding ... *Pteria penguin*, *J. Mar. Sci. Eng.* 13:874 | 10.3390/jmse13050874 | CR, S2, OA | Abstract |
| N9 | Sebens KP 2002, Energetic constraints, size gradients, and size limits in benthic marine invertebrates, *Integr. Comp. Biol.* 42:853–861 | 10.1093/icb/42.4.853 | CR, S2, EPMC, OA | Verified, not used |
| N10 | Bell EC, Gosline JM 1996, Mechanical design of mussel byssus: material yield enhances attachment strength, *J. Exp. Biol.* 199:1005–1017 | 10.1242/jeb.199.4.1005 | CR, EPMC, OA | Verified, not used |
| N11 | Schneider KR et al. 2004, Implications of movement behavior on mussel dislodgement, *Mar. Biol.* 146:333–343 | 10.1007/s00227-004-1446-z | CR, S2, OA | Verified, no abstract, not used |
| N13 | Xu M et al. 2016, Attachment strength of the subtidal seaweed *Sargassum horneri* ..., *J. Appl. Phycol.* 28:3679–3687 | 10.1007/s10811-016-0869-5 | CR, S2, EPMC, OA | Full text (PMC5155027) |
| N14 | Kang V et al. 2020, Molecular insights into the powerful mucus-based adhesion of limpets, *Open Biol.* 10 | 10.1098/rsob.200019 | CR, S2, EPMC, OA | Abstract; no cost figure, not used |
| N15 | Burek KE et al. 2018, Wasted effort: recruitment and persistence of kelp on algal turf, *MEPS* 600:3–19 | 10.3354/meps12677 | CR, S2, OA | Abstract |
| N16 | Davies MS, Hawkins SJ, Jones HD 1990, Mucus production and physiological energetics in *Patella vulgata*, *J. Molluscan Stud.* 56:499–503 | 10.1093/mollus/56.4.499 | CR, S2, OA | Abstract |

Seen and not carried: Jordan & Valiela 1982 and Gardner & Thomas 1987, cited inside full texts and not verified. Shang et al. 2021, cited in C14 and not verified. Kideys & Hartnoll 1991 (whelk mucus), named by a web search summary and not verified.

## Access refusals, recorded rather than worked around

- int-res.com (every MEPS paper) serves a "Bot check" page. Not attempted further.
- Wiley (C1's published version), journals.biologists.com (C5, C22), ScienceDirect (C2), eScholarship (C9, a CloudFront 403), IRIS Palermo (C9) and DTIC (the C15 report) returned 403 to the script. For eScholarship and DTIC I tried WebFetch once each. It got an empty page and a 403, and I went no further.
- URI DigitalCommons returned 403 on a third fetch, after two had succeeded. I took this as a rate limit and did not retry.
- The first version of `ft.py` sent a browser-like User-Agent with the project's identifier appended ("Mozilla/5.0 ... evosim-litreview/0.1"). It was used for the first fetches: Wiley (403), the NOAA repository (served C1's AM), Inter-Research (bot check) and DIGITAL.CSIC (a short page in place of the record). It was then changed to the plain "evosim-litreview/0.1", which DIGITAL.CSIC served normally. Every text read was served openly. None came through a bot check.
