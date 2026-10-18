# Leaf scent literature: search log

All searches ran on 2026-10-12, in discovery mode, for the question of whether living leaves should give off something a brain can sense. The brief is `brief.md` beside this file, with a sixth question (direction and identity) and a fourth rule shape added by the coordinator during the run. The pool built from these searches is `candidates.md`, and the answers are in `pass2.md`. The raw rows of the database searches are in `search-log.tsv` and the `out_*.txt` files. The verification records, with every abstract read, are in `verify.json`, and `abstracts.txt` is a readable dump of them. The snowball lists are under `snow/`.

The helper scripts were copied from `scratch/r59-anchor-lit/` and adapted: `oa.py` (OpenAlex search and abstract), `epmc.py` (Europe PMC search), `cr.py` (CrossRef bibliographic lookup), `verify.py` (the four-database cross-check), `check.py` (new: compares title, first author and year across the databases and reads the retraction flags), `snow.py` (CrossRef reference lists, Semantic Scholar citations) and `ft.py` (reads an open-access text in memory and keeps no copy; it now reports a failed fetch in one line).

## Engines

| Engine | How it was queried | What a count means |
|---|---|---|
| OpenAlex | REST API `api.openalex.org/works?search=...&filter=is_retracted:false`, sorted by relevance, top 8 to 10 records inspected | Works matching the free-text search, retracted works excluded |
| Europe PMC | REST API `ebi.ac.uk/europepmc/webservices/rest/search`, Boolean queries, top 8 records inspected | Hit count for the Boolean query, all sources |
| WebSearch | The Claude Code web search tool | No count. It returns about 10 links per query |
| CrossRef | `api.crossref.org/works?query.bibliographic=...`, top 2 records, to find the DOI of a work already named | Not a search count, so not logged as one |

## Database searches with counts

| # | Date | Engine | Tag | Query | Filters / sort | Count |
|---|---|---|---|---|---|---|
| 1 | 2026-10-12 | OpenAlex | Q1a | sea urchin chemoreception detection of algae at a distance foraging | is_retracted:false, relevance | 58 |
| 2 | 2026-10-12 | OpenAlex | Q1b | amphipod chemical cues locate macroalgae food choice waterborne | is_retracted:false, relevance | 12 |
| 3 | 2026-10-12 | OpenAlex | Q1c | herbivorous gastropod distance chemoreception algae attraction flume | is_retracted:false, relevance | 3 |
| 4 | 2026-10-12 | OpenAlex | Q1d | copepod chemoreception phytoplankton detection distance feeding current | is_retracted:false, relevance | 108 |
| 5 | 2026-10-12 | OpenAlex | Q1e | herbivorous fish olfaction vision food location algae | is_retracted:false, relevance | 56 |
| 6 | 2026-10-12 | OpenAlex | Q1f | isopod waterborne cues seaweed host location | is_retracted:false, relevance | 24 |
| 7 | 2026-10-12 | OpenAlex | Q1g | zooplankton detection of phytoplankton chemical cues remote detection distance | is_retracted:false, relevance | 428 |
| 8 | 2026-10-12 | OpenAlex | Q2a | dimethyl sulfide DMSP foraging cue marine grazers | is_retracted:false, relevance | 92 |
| 9 | 2026-10-12 | OpenAlex | Q2b | dissolved free amino acids exudation macroalgae attract herbivores | is_retracted:false, relevance | 20 |
| 10 | 2026-10-12 | OpenAlex | Q2c | grazing activated chemical signal phytoplankton zooplankton DMSP | is_retracted:false, relevance | 200 |
| 11 | 2026-10-12 | OpenAlex | Q2d | macroalgae exudates dissolved organic carbon release rate | is_retracted:false, relevance | 932 |
| 12 | 2026-10-12 | OpenAlex | Q2e | chemical cues damaged algae attract grazers wound-activated | is_retracted:false, relevance | 113 |
| 13 | 2026-10-12 | OpenAlex | Q2f | Lytechinus sea urchin chemosensory response kelp exudate | is_retracted:false, relevance | 1 |
| 14 | 2026-10-12 | OpenAlex | Q3a | odor plume turbulence structure chemical signal aquatic animals orientation | is_retracted:false, relevance | 146 |
| 15 | 2026-10-12 | OpenAlex | Q3b | odour-gated rheotaxis marine invertebrate upstream orientation | is_retracted:false, relevance | 20 |
| 16 | 2026-10-12 | OpenAlex | Q3c | chemotaxis gradient search benthic gastropod food odor | is_retracted:false, relevance | 22 |
| 17 | 2026-10-12 | OpenAlex | Q3d | sea urchin flume orientation to food odor upstream distance | is_retracted:false, relevance | 13 |
| 18 | 2026-10-12 | OpenAlex | Q3e | phycosphere microscale nutrient gradients chemotaxis phytoplankton bacteria | is_retracted:false, relevance | 112 |
| 19 | 2026-10-12 | OpenAlex | Q3f | diffusion boundary layer macroalgae blade concentration gradient flow | is_retracted:false, relevance | 213 |
| 20 | 2026-10-12 | OpenAlex | Q4a | scavenger carrion odor detection gastropod amino acids | is_retracted:false, relevance | 9 |
| 21 | 2026-10-12 | OpenAlex | Q4b | herbivore versus scavenger response to food odour amino acids marine snail | is_retracted:false, relevance | 5 |
| 22 | 2026-10-12 | OpenAlex | Q4c | freshwater shredder amphipod attraction conditioned leaf litter microbial odour | is_retracted:false, relevance | 3 |
| 23 | 2026-10-12 | OpenAlex | Q5a | detection threshold dissolved amino acids chemoreceptors crustacean | is_retracted:false, relevance | 104 |
| 24 | 2026-10-12 | OpenAlex | Q5b | dimethylsulfoniopropionate turnover bacterial consumption seawater half-life | is_retracted:false, relevance | 139 |
| 25 | 2026-10-12 | OpenAlex | Q5c | dissolved free amino acids turnover time seawater bacterial uptake hours | is_retracted:false, relevance | 1519 |
| 26 | 2026-10-12 | OpenAlex | Q5d | dissolved DMSP turnover rate bacterioplankton consumption coastal | is_retracted:false, relevance | 250 |
| 27 | 2026-10-12 | OpenAlex | Q6a | tropotaxis klinotaxis bilateral comparison antennules odor plume lobster crab | is_retracted:false, relevance | 2 |
| 28 | 2026-10-12 | OpenAlex | Q6b | olfactory receptor specificity crustacean aesthetasc amino acid receptor channels | is_retracted:false, relevance | 47 |
| 29 | 2026-10-12 | OpenAlex | Q6c | bilateral comparison antennules odor plume tracking lobster crayfish | is_retracted:false, relevance | 36 |
| 30 | 2026-10-12 | OpenAlex | Q6d | olfaction gustation separate pathways crustacean aesthetasc non-aesthetasc orientation food | is_retracted:false, relevance | 8 |
| 31 | 2026-10-12 | Europe PMC | E1 | ("sea urchin\*" OR echinoid\*) AND (chemorecept\* OR chemosens\*) AND (alga\* OR kelp OR seaweed) AND (attract\* OR orient\*) | default | 110 |
| 32 | 2026-10-12 | Europe PMC | E2 | (grazer\* OR herbivor\*) AND ("waterborne cue\*" OR "water-borne cue\*") AND (macroalga\* OR seaweed\*) AND (attract\* OR locat\* OR orient\*) | default | 4 |
| 33 | 2026-10-12 | Europe PMC | E3 | ("odor plume\*" OR "odour plume\*") AND (crab\* OR lobster\* OR crayfish OR snail\* OR gastropod\*) AND (orientation OR tracking OR navigation) | default | 68 |
| 34 | 2026-10-12 | Europe PMC | E4 | (chemorecept\* OR olfact\*) AND (scaveng\* OR carrion) AND (amino acid\*) AND (marine OR aquatic) | default | 758 |

The queries tagged Q6 were run after the coordinator added the sixth question. Several queries returned mostly off-topic records, and the counts show the size of a field more than its relevance: Q2d's 932 is almost all carbon-cycle work, Q5c's 1519 is bacterial nitrogen cycling, and E4's top eight were 2025–2026 aquaculture and food-science papers, none carried. E1 and E3 also returned mostly unrelated recent work; E2 returned four records, one of them (Flöthe et al. 2014, induced defence) on topic but not carried, because it concerns the seaweed's response and not a grazer's search.

## WebSearch queries (no counts; about 10 links each)

| # | Date | Query | Used |
|---|---|---|---|
| W1 | 2026-10-12 | Weissburg Zimmer-Faust "odor-gated rheotaxis" blue crab "Life and death in moving fluids" Ecology 1993 | Weissburg & Zimmer-Faust 1993 (bibliographic); archive.org copies of two Biol. Bull. papers surfaced |
| W2 | 2026-10-12 | Webster Weissburg "The hydrodynamics of chemical cues among aquatic organisms" Annual Review of Fluid Mechanics | Webster & Weissburg 2009; Weissburg 2000 named |
| W3 | 2026-10-12 | Seymour Simó Ahmed Stocker "Chemoattraction to dimethylsulfoniopropionate throughout the marine microbial food web" Science 2010 | Seymour et al. 2010 |
| W4 | 2026-10-12 | Steinke Stefels Stamhuis "Dimethyl sulfide triggers search behavior in copepods" Limnology and Oceanography 2006 | Steinke et al. 2006; Breckels et al. 2013 named |
| W5 | 2026-10-12 | sea urchin attracted to kelp waterborne odour flume distance Strongylocentrotus chemoreception study | No distance study found. Luis et al. 2021 confirmed; Vadas 1977 named in a review, not verified or carried |
| W6 | 2026-10-12 | Littorina snail attraction to Fucus algae waterborne cues Y-maze distance chemoreception herbivorous periwinkle | Nothing on attraction at a distance. Not carried |
| W7 | 2026-10-12 | Aplysia orientation upstream seaweed odor distance chemoreception rhinophores | Teyke 1992; the archive.org copy of Frings & Frings 1965 |
| W8 | 2026-10-12 | Hay 2009 "Marine chemical ecology: chemical signals and cues structure marine populations, communities, and ecosystems" Annual Review of Marine Science | Hay 2009 (author manuscript in PMC) |
| W9 | 2026-10-12 | Gonçalves Kiørboe 2015 "Perceiving the algae" feeding-current copepods detect nonmotile prey chemical distance abstract | Gonçalves & Kiørboe 2015 |
| W10 | 2026-10-12 | herbivorous amphipod or isopod attracted to seaweed waterborne odour two-choice flume Idotea Ampithoe chemical cue distance | Fernandes et al. 2022; Nicotri 1980 named, not verified or carried |
| W11 | 2026-10-12 | Derby Sorensen 2008 "Neural processing, perception, and behavioral responses to natural chemical stimuli by fish and crustaceans" Journal of Chemical Ecology | Derby & Sorensen 2008 |
| W12 | 2026-10-12 | aquatic scavengers carrion cues versus herbivore cues amino acids betaine nucleotides detection marine decomposition odour putrescine cadaverine | Ide et al. 2006; Zimmer-Faust 1987 named. Aquaculture feed pages in the results were not used |
| W13 | 2026-10-12 | Zimmer-Faust 1987 "Crustacean chemical perception: towards a theory on optimal chemoreception" Biological Bulletin amino acids ammonia ratio carrion freshness | Zimmer-Faust 1987 and its archive.org copy |
| W14 | 2026-10-12 | Paine 2021 "Rate and fate of dissolved organic carbon release by seaweeds" Journal of Phycology percentage of net primary production | Paine et al. 2021 |
| W15 | 2026-10-12 | Van Alstyne 2001 "Activated defense systems in marine macroalgae" DMSP cleavage damage urchin feeding deterrent acrylic acid | Van Alstyne et al. 2001; Van Alstyne & Houser 2003 named, not carried |
| W16 | 2026-10-12 | herbivorous fish olfaction locate algae food odour reef rabbitfish surgeonfish vision foraging chemical cues study | Nothing on how herbivorous fish find algae. Theses only. Not carried |
| W17 | 2026-10-12 | freshwater snail attracted to aquatic plant odour macrophyte chemical cues Pomacea OR Lymnaea OR Biomphalaria chemotaxis food distance | Moelzner & Fink 2015 (Oikos) named. A meeting abstract on *Lymnaea* was seen and not carried, being unreviewed |
| W18 | 2026-10-12 | Pohnert Steinke Tollrian 2007 "Chemical cues, defence metabolites and the shaping of pelagic interspecific interactions" Trends in Ecology and Evolution | Pohnert et al. 2007 (verified, not carried) |
| W19 | 2026-10-12 | Kiørboe Thygesen 2001 "Fluid motion and solute distribution around sinking aggregates" remote detection zooplankton plume length detection distance | Kiørboe & Thygesen 2001 |

## CrossRef DOI lookups (title-keyed, not counted)

On 2026-10-12 CrossRef resolved the DOIs of these works, found by title in WebSearch or named in a snowball list: Weissburg & Zimmer-Faust 1993 (10.2307/1940072), Webster & Weissburg 2009 (10.1146/annurev.fluid.010908.165240), Atema 1996 (10.2307/1543074), Moore & Crimaldi 2004 (10.1016/j.jmarsys.2003.05.005), Fernandes et al. 2022 (10.3354/meps14140), Moelzner & Fink 2015 (10.1111/oik.02069), Weissburg 2000 (10.2307/1542523), Derby & Sorensen 2008 (10.1007/s10886-008-9489-0), Kiørboe 2011 (10.1111/j.1469-185x.2010.00148.x), Seymour et al. 2017 (10.1038/nmicrobiol.2017.65) and Frings & Frings 1965 (10.2307/1539550). Two lookups hit HTTP 429 and were re-run after a pause. The DOIs of Teyke 1992 and a Marine Biology alarm-cue paper on urchins were read from CrossRef `works/{doi}` directly; the second was not carried.

## Snowball (one round, 2026-10-12)

The brief asked for one round of snowballing from the three to five strongest candidates. The seeds were the two reviews closest to the question of herbivores finding algae (Saha & Fink 2022 backward; Hay 2009 backward) and three primary papers forward (Teyke 1992, Van Alstyne et al. 2001, Moelzner & Fink 2015).

| Seed | Direction | Source | Records | Raw list |
|---|---|---|---|---|
| Saha & Fink 2022 | Backward | CrossRef reference deposit, each DOI resolved in CrossRef | 130 | `snow/saha-back.txt` |
| Hay 2009 | Backward | The same | 129 | `snow/hay-back.txt` |
| Teyke 1992 | Forward | Semantic Scholar citations API | 15 | `snow/teyke-fwd.txt` |
| Van Alstyne et al. 2001 | Forward | The same | 186 | `snow/vanalstyne-fwd.txt` |
| Moelzner & Fink 2015 | Forward | The same | 14 | `snow/moelzner-fwd.txt` |

Screen: titles had to carry a search word (attract, locat, find, forag, cue, odo, plume, navigat, orient, search, detect, infochem) and a subject word (herbivore, grazer, urchin, snail, gastropod, amphipod, isopod, copepod, zooplankton, alga, seaweed, plant, DMS, volatile, plume, navigation, orientation). Pheromone, larval settlement, toxin, antifouling, bird and insect titles were excluded. Each title that passed was read.

Taken forward and verified: Wyeth 2019 (*Olfactory navigation in aquatic gastropods*, from Teyke forward), Wyeth et al. 2006 (*Tritonia*, Teyke forward), Fink et al. 2006 (*J. Chem. Ecol.*, Saha backward), Moelzner & Fink 2014 (*J. Anim. Ecol.*, Saha backward), Jüttner et al. 2010 (Saha backward), Addis et al. 2023 (Van Alstyne forward), Atema 1995 (*PNAS*, Hay backward) and Dacey & Wakeham 1986 (Hay backward). Seen and not carried: Coleman et al. 2006 (predators attracted to grazed seaweed; it is about the grazers' enemies, and Hay 2009 already summarises it), Akakabe & Kajiwara 2008 (algal volatiles as feeding attractants, an applied paper with no behaviour at a distance in its title), Fink et al. 2006 in *Archiv für Hydrobiologie* (diatom oxylipins attract a snail; it duplicates the *J. Chem. Ecol.* paper and has no DOI in CrossRef's list) and Bowker 2013 (no DOI).

## Verification (2026-10-12)

`verify.py` checked every pool DOI against CrossRef `works/{doi}` (metadata, `update-to` and `relation`), the Semantic Scholar graph API, Europe PMC (`DOI:` query) and OpenAlex `works/doi:` (with `is_retracted`). `check.py` then compared title, first author and year across the databases. All 56 DOIs checked matched in at least three of the four, CrossRef, Semantic Scholar and OpenAlex in every case. For Luis et al. 2021 the Semantic Scholar first-author string differs in form, so it counts as CrossRef and OpenAlex only. No record carries a CrossRef `update-to` (no correction or retraction notice), and none is flagged retracted in OpenAlex. Two carry a CrossRef `has-review` relation (Seymour et al. 2010; Moelzner & Fink 2014), which links a published peer review, not a correction.

## Pages the fetcher could not read (not bypassed)

- Wiley `pdfdirect` returned 403 for Gonçalves & Kiørboe 2015, Webster et al. 2001 and the other Wiley titles. The University of Granada repository copy of Gonçalves & Kiørboe 2015 returned 504 to the script and to WebFetch. The CONICET handle returned 404.
- journals.biologists.com returned 403 (Wyeth 2019).
- royalsocietypublishing.org returned 403 (Webb et al. 2024). Its Europe PMC full text was read instead.
- int-res.com returned 401 to the script (Jackson & Kiørboe 2004). No Inter-Research full text was tried after that, so every MEPS paper stays at abstract.
- biodiversitylibrary.org returned 403; the same Biological Bulletin papers were read from their archive.org biostor copies.
- The Van Alstyne lab page's copy of the 2001 paper returned 404.

## Not done

- Google Scholar, Web of Science and Scopus were not queried.
- No forward snowball from the plume papers (Webster & Weissburg 2009, Weissburg 2000); the round went to the herbivore question, where the pool was thinnest.
- Herbivorous fish and seagrass grazers: searched (Q1e, W16) and nothing usable found.
