# Round 59 anchoring cell literature, pass 1: search log

All searches were run on 2026-10-10, in discovery mode, for `logbook/specs/anchoring-cell-design.md`. The pool built from them is in `candidates.md` beside this file. The raw rows of the database searches are in `search-log.tsv`. The verification records, with every abstract that was read, are in `verify.json`. The helper scripts are `oa.py` (OpenAlex search and abstract), `cr.py` (CrossRef bibliographic lookup) and `verify.py` (the cross-check).

The brief named `logbook/specs/r57-lit/` as the model for this output. That folder is not on `main`. It was read from commit `1917815` on the `round-57` branch, and this log follows its shape.

## Engines

| Engine | How it was queried | What a count means |
|---|---|---|
| OpenAlex | REST API `api.openalex.org/works?search=...&filter=is_retracted:false`, sorted by relevance, top 4 to 12 records inspected | Works matching the free-text search, retracted works excluded |
| Europe PMC | REST API `ebi.ac.uk/europepmc/webservices/rest/search`, Boolean queries, top 8 records inspected | Hit count for the Boolean query, all sources |
| WebSearch | The Claude Code web search tool | No count. It returns about 10 links per query |
| CrossRef | `api.crossref.org/works?query.bibliographic=...`, top 3 records, to find the DOI of a work already named | Not a search count. CrossRef's total is every work with any matching word, so it is not logged as a count |

## Database searches with counts

| # | Date | Engine | Tag | Query | Filters / sort | Count |
|---|---|---|---|---|---|---|
| 1 | 2026-10-10 | OpenAlex | Q1a | byssal thread production energetic cost mussel | is_retracted:false, relevance | 175 |
| 2 | 2026-10-10 | OpenAlex | Q1b | barnacle cement energetic cost adhesion | is_retracted:false, relevance | 53 |
| 3 | 2026-10-10 | OpenAlex | Q1c | byssus production flow water motion Mytilus attachment strength season | is_retracted:false, relevance | 62 |
| 4 | 2026-10-10 | OpenAlex | Q1d | macroalgae holdfast attachment strength allocation cost | is_retracted:false, relevance | 33 |
| 5 | 2026-10-10 | OpenAlex | Q1e | mussel byssus energy budget carbon nitrogen cost production | is_retracted:false, relevance | 78 |
| 6 | 2026-10-10 | OpenAlex | Q1f | limpet mucus production energetic cost adhesion locomotion | is_retracted:false, relevance | 19 |
| 7 | 2026-10-10 | OpenAlex | Q1g | seasonal variation mussel attachment strength byssal thread quality | is_retracted:false, relevance | 194 |
| 8 | 2026-10-10 | OpenAlex | Q2a | mechanical limits to size wave-swept organisms drag dislodgement | is_retracted:false, relevance | 223 |
| 9 | 2026-10-10 | OpenAlex | Q2b | attachment strength scales with size dislodgement force intertidal | is_retracted:false, relevance | 347 |
| 10 | 2026-10-10 | OpenAlex | Q2c | sea star tube feet adhesion attachment force number of tube feet | is_retracted:false, relevance | 287 |
| 11 | 2026-10-10 | OpenAlex | Q2d | sea anemone pedal disc attachment tenacity detachment | is_retracted:false, relevance | 11 |
| 12 | 2026-10-10 | OpenAlex | Q2e | barnacle adhesion strength basal area shear removal | is_retracted:false, relevance | 453 |
| 13 | 2026-10-10 | OpenAlex | Q2f | kelp holdfast size dislodgement force wave exposure | is_retracted:false, relevance | 140 |
| 14 | 2026-10-10 | OpenAlex | Q2g | limpet tenacity adhesive strength foot area shell shape | is_retracted:false, relevance | 52 |
| 15 | 2026-10-10 | OpenAlex | Q3a | suspension feeding flow speed feeding rate benthic sessile energetics | is_retracted:false, relevance | 455 |
| 16 | 2026-10-10 | OpenAlex | Q3b | sea anemone prey capture flow body size | is_retracted:false, relevance | 1756 |
| 17 | 2026-10-10 | OpenAlex | Q3c | sessile versus mobile trade-off flow feeding dislodgement risk | is_retracted:false, relevance | 143 |
| 18 | 2026-10-10 | OpenAlex | Q3d | mussel growth flow velocity food supply boundary layer | is_retracted:false, relevance | 1950 |
| 19 | 2026-10-10 | OpenAlex | Q4a | mussel movement detachment reattachment byssus relocation | is_retracted:false, relevance | 2 |
| 20 | 2026-10-10 | OpenAlex | Q4b | sea anemone detachment locomotion pedal disc drift | is_retracted:false, relevance | 7 |
| 21 | 2026-10-10 | OpenAlex | Q4c | mussel movement Levy walk patterns bed formation | is_retracted:false, relevance | 69 |
| 22 | 2026-10-10 | OpenAlex | Q4d | byssal thread production rate per day mussel detached re-attachment | is_retracted:false, relevance | 63 |
| 23 | 2026-10-10 | OpenAlex | Q2h | Carrington seasonal variation attachment strength blue mussels causes consequences | is_retracted:false, relevance | 145 |
| 24 | 2026-10-10 | OpenAlex | Q2i | Bell Gosline strategies for life in flow tenacity morphometry probability of dislodgment Mytilus | is_retracted:false, relevance | 63 |
| 25 | 2026-10-10 | OpenAlex | Q2j | Babarro Carrington attachment strength Mytilus galloprovincialis habitat body size | is_retracted:false, relevance | 64 |
| 26 | 2026-10-10 | OpenAlex | Q1h | byssus production costs percent energy budget Mytilus Hawkins Bayne | is_retracted:false, relevance | 11 |
| 27 | 2026-10-10 | OpenAlex | Q4e | sea anemone voluntary detachment reattachment substrate behaviour | is_retracted:false, relevance | 2 |
| 28 | 2026-10-10 | OpenAlex | Q4f | trail following energy saving mucus marine snail | is_retracted:false, relevance | 46 |
| 29 | 2026-10-10 | OpenAlex | Q4g | sea star tube foot detachment footprint reattachment temporary adhesion review | is_retracted:false, relevance | 2 |
| 30 | 2026-10-10 | OpenAlex | Q1i | secretion of byssal threads attachment strength size food availability | is_retracted:false, relevance | 151 |
| 31 | 2026-10-10 | OpenAlex | Q3e | flow-induced energetic bounds growth sea anemone | is_retracted:false, relevance | 119 |
| 32 | 2026-10-10 | OpenAlex | Q1j | cost of attachment sessile marine invertebrates energy allocation adhesive | is_retracted:false, relevance | 28 |
| 33 | 2026-10-10 | OpenAlex | Q2k | holdfast attachment mechanics developmental stage substratum season wave exposure Hedophyllum | is_retracted:false, relevance | 31 |
| 34 | 2026-10-10 | Europe PMC | E1 | (byssus OR "byssal thread*") AND (energ* OR cost) | default | 725 |
| 35 | 2026-10-10 | Europe PMC | E2 | (dislodg* OR tenacity OR "attachment strength") AND (wave OR flow) AND (mussel* OR limpet* OR macroalga* OR kelp OR urchin* OR anemone*) | default | 937 |
| 36 | 2026-10-10 | Europe PMC | E3 | ("tube feet" OR "tube foot") AND (adhesion OR attachment OR detachment) | default | 259 |
| 37 | 2026-10-10 | Europe PMC | E4 | ("sea anemone" OR anemone*) AND ("pedal disc" OR "pedal disk") AND (detach* OR attach* OR locomotion) | default | 72 |
| 38 | 2026-10-10 | Europe PMC | E5 | (mucus) AND (gastropod* OR limpet* OR snail*) AND (energ* OR cost) AND (adhesi* OR locomotion) | default | 1070 |
| 39 | 2026-10-10 | Europe PMC | E6 | ("suspension feed*" OR "suspension-feed*") AND ("flow speed" OR "current velocity" OR "flow velocity") AND (feeding OR growth) | default | 4 |

Three OpenAlex queries failed with HTTP 429 (too many requests) and returned nothing. They are not in the table above, and nothing was taken from them: "limpet homing scar fidelity energetic cost adhesion foraging excursion", "attachment strength mussels drag lift dislodgement wave velocity model" and "trade-off attachment strength growth reproduction mussel byssus". The Europe PMC queries were ordered mostly by recency, and their top eight records were 2024 to 2026 work on adhesive proteins, genomics and pollutants. No Europe PMC record was carried into the pool from these six queries. Their counts show the size of each field. They did not feed the pool.

## WebSearch queries (no counts; about 10 links each)

| # | Date | Query | Used |
|---|---|---|---|
| W1 | 2026-10-10 | Carrington Waite Sarà Sebens "Mussels as a model system for integrative ecomechanics" Annual Review of Marine Science | Carrington et al. 2015 (bibliographic) |
| W2 | 2026-10-10 | Hawkins Bayne 1985 "Seasonal variation in the relative utilization of carbon and nitrogen by the mussel Mytilus edulis" byssus | Hawkins & Bayne 1985 |
| W3 | 2026-10-10 | Carrington 2002 "Seasonal variation in the attachment strength of blue mussels: causes and consequences" Limnology and Oceanography | Carrington 2002 L&O |
| W4 | 2026-10-10 | Denny 2000 "Limits to optimization: fluid dynamics, adhesive strength and the evolution of shape in limpet shells" Journal of Experimental Biology | Denny 2000 |
| W5 | 2026-10-10 | de Jager Weissing Herman Nolet van de Koppel 2011 "Lévy walks evolve through interaction between movement and environmental complexity" Science mussels | de Jager et al. 2011; Jansen et al. 2012 comment noted, not carried |
| W6 | 2026-10-10 | Babarro Carrington 2013 attachment strength Mytilus galloprovincialis body size byssus plaque number Journal of Experimental Marine Biology and Ecology | Babarro & Carrington 2013 (title only); Babarro et al. 2008 |
| W7 | 2026-10-10 | Denny 1987 lift as a mechanism of patch initiation in mussel beds | Not found; known only as a secondary citation. Not carried |
| W8 | 2026-10-10 | Davies Hawkins 1998 "Mucus from marine molluscs" Advances in Marine Biology energetic cost | Davies & Hawkins 1998 (title and table of contents only); Davies & Blackwell 2007 |
| W9 | 2026-10-10 | sea anemone detaches from substrate and reattaches pedal disc time hours Anthopleura OR Metridium OR Aiptasia study | Nothing with reattachment times. Leads, not carried: McFarlane 1969 (Calliactis), Ellis, Ross & Sutton 1969 (Stomphia) |
| W10 | 2026-10-10 | Koehl 1977 "Effects of sea anemones on the flow forces they encounter" Journal of Experimental Biology | Koehl 1977 |
| W11 | 2026-10-10 | energetic cost of byssus production percentage of production mussel review cost of attachment structure marine invertebrate | Confirmed Roberts et al. 2021 as the main primary source; no cross-taxon review found |
| W12 | 2026-10-10 | Roberts Carrington 2023 "Energetic scope limits growth but not byssal thread production of two mytilid mussels" abstract | Roberts & Carrington 2023 (search summary only) |
| W13 | 2026-10-10 | Widdows Lucas Brinsley Salkeld Staff 2002 "effects of current velocity on mussel feeding and mussel bed stability" annular flume abstract clearance rate | Widdows et al. 2002 (search summary of the abstract) |
| W14 | 2026-10-10 | "Attachment strength of the mussel Mytilus galloprovincialis: effect of habitat and body size" abstract tenacity thread number size | No abstract found |

## CrossRef DOI lookups (title-keyed, not counted)

On 2026-10-10 CrossRef resolved the DOIs of these works, which had been found by title in WebSearch: Carrington 2002 (10.4319/lo.2002.47.6.1723), Babarro & Carrington 2013 (10.1016/j.jembe.2013.02.035), Denny 2000 (10.1242/jeb.203.17.2603), de Jager et al. 2011 (10.1126/science.1201187), Davies & Hawkins 1998 (10.1016/s0065-2881(08)60210-2), Hawkins & Bayne 1985 (10.3354/meps025181), Carrington et al. 2015 (10.1146/annurev-marine-010213-135049) and Carrington 2002 ICB (10.1093/icb/42.4.846). The Koehl 1977 lookup hit a 429. Its DOI, 10.1242/jeb.69.1.87, was resolved instead through `api.crossref.org/works/{doi}` during verification.

## Verification (2026-10-10)

`verify.py` checked every pool DOI against four databases: CrossRef `works/{doi}` (metadata, `update-to` and `relation` for retractions), the Semantic Scholar graph API, Europe PMC (`DOI:` query) and OpenAlex `works/doi:` (with the `is_retracted` flag). All 32 matched on title, first author and year in at least two. Koehl 1977 is the thinnest, at two: CrossRef and OpenAlex. Semantic Scholar returned no record for it, and Europe PMC does not index it. No record carried a retraction notice in CrossRef, and none was flagged retracted in OpenAlex.

## Pages the fetcher could not read (not bypassed)

- ScienceDirect (`sciencedirect.com/science/article/pii/S0022098123000795`, Roberts & Carrington 2023) returned 403.
- Springer (`link.springer.com/article/10.1007/s10152-001-0100-0`, Widdows et al. 2002) redirected to a login (`idp.springer.com/authorize`). The redirect was not followed.
- The CSIC repository (`digital.csic.es/handle/10261/74441`, Babarro & Carrington 2013) sat behind an Anubis bot challenge.
- The NSF PAR record (`par.nsf.gov/biblio/10500317`, Roberts & Carrington 2023) opened, but its abstract field is empty.

## Not done in pass 1

- No forward or backward snowballing beyond the named foundational works (Denny et al. 1985, Carrington 2002, Hawkins & Bayne 1985), which were sought by name. Snowballing from Roberts et al. 2021 and Carrington et al. 2015 is the obvious first step for pass 2.
- No full text was read. No PDF was downloaded.
- Google Scholar, Web of Science and Scopus were not queried.
