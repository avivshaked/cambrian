# Leaf scent literature, pass 2: evidence and answers

Pass 2 of the short literature round on whether living leaves should give off something a brain can sense, run on 2026-10-12. The brief is `brief.md`; the coordinator added a sixth question (direction and identity) and a fourth rule shape during the run, and both are answered here under the same rules. The pool is `candidates.md` and the searches, snowball and verification are in `search-log.md`. Full texts were read in memory with `ft.py`, which keeps no copy.

What the world has today, as the brief states it: the only smell comes from dead bodies, each emitting in proportion to its stored energy, in a cone that falls to zero at 10 m, and a separate chemical sense reads dissolved nutrients. This file gathers evidence for a ruling on living leaves. It does not make the ruling.

## How to read the status marks

- **FT** means the full text was read in this session. Locators are PDF pages of the copy read ("PDF p.N"); for the archive.org scans of the *Biological Bulletin* the PDF page and the journal page are both given where they differ. Hay 2009 was read as the author manuscript in PMC, which has no page numbers, so its locators are section headings. Webb et al. 2024 was read as Europe PMC's XML, also by section.
- **AO** means abstract only. The claim is what the abstract says, and nothing more.
- **SS** means the content is known only from a web search tool's summary. It is weaker than AO.
- **SEC** means the claim was read in another paper's full text, citing this one. It was not checked against the cited paper, and the cited paper was not verified unless it is in the pool.
- **Calc** marks arithmetic I did on published numbers. **Inference** marks my reading. Neither is a finding of any paper.

Every C-numbered paper was matched on title, first author and year in at least two of CrossRef, Semantic Scholar, Europe PMC and OpenAlex (most in three or four), and none carries a retraction notice or flag (`search-log.md`, Verification).

## Full texts reached and not reached

Read in full: C2 Frings & Frings 1965 (archive.org scan), C30 Zimmer-Faust et al. 1995 (archive.org scan), C28 Weissburg 2000 (archive.org scan), C29 Moore & Crimaldi 2004 (author's copy, searched by term), C34 Zimmer-Faust 1987 (archive.org scan, searched by term), C25 Hay 2009 (author manuscript, PMC, searched by term), C15 Yu et al. 2023 (Frontiers, searched by term), C8 Webb et al. 2024 (Europe PMC, methods and results). C26 Nocchi et al. 2017 was opened in Europe PMC, and only its introduction was reached before the term search ran out; it stays AO.

Not reached, so each stays AO or SS: every Inter-Research (MEPS) paper (int-res returned 401 to the script), every Wiley paper including C11 Gonçalves & Kiørboe 2015 (403, and its repository copies returned 504 and 404), C3 Wyeth 2019 (403), C1 Teyke et al. 1992 (closed), C27 Webster & Weissburg 2009 (closed), C33 Atema 1995 (PMC holds only the abstract as text). No access control was passed or worked around.

## Evidence table

### Q1. How aquatic herbivores locate food at a distance

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C1 | Teyke et al. 1992, *J. Comp. Physiol. A* 170 | AO; SS | In a still-water open field, *Aplysia californica* took 42 min on average to find seaweed placed no more than 30 cm away, by an undirected path; aroused animals found odourless paper as fast as seaweed. The authors propose a random search until very close, because still water gives no usable gradient beyond a few centimetres. In a still-water Y-maze they did no better than chance (AO). Per the search summary, in streaming water they chose the seaweed arm above chance only with a large stimulus (SS). | Lab tank. The abstract is cut at 250 words in Europe PMC, so the flow result rests on the summary. |
| C2 | Frings & Frings 1965, *Biol. Bull.* 128:211 | FT | Hungry *Aplysia juliana* buried in sand emerged within 10–15 s of a piece of *Ulva* being dropped in, and went "fairly directly" to it when the water was quiet or moving in one direction, but oriented poorly with an aerator running (PDF p.1, journal p.211). Water in which *Ulva* had stood was as effective as the alga; two drops in the aquarium worked, a dilution the authors estimate at 1 in 15,000,000 of dissolved material or more (PDF p.2). The receptors were on the oral tentacles; the rhinophores sensed water currents, not the cue, and an animal followed a current once its tentacle met the cue in it (PDF p.4). | Lab aquaria, a near-monophagous sea hare. Behaviour described, with no counts or statistics. |
| C8 | Webb et al. 2024, *Proc. R. Soc. B* 291 | FT (Europe PMC) | In a two-channel choice flume, herbivorous juvenile crown-of-thorns sea stars moved toward cues of coralline-algal rubble and of live coral as sole cues, and preferred coral when both were offered (Abstract; Results a). With filtered seawater only, 21 of the 23 that moved went upstream (Results a). | Juveniles about 10–13 mm across, flow 8.3 cm³ s⁻¹ per channel, under red light to exclude vision (Methods). The upstream bias without a cue shows rheotaxis on its own. |
| C7 | Fernandes et al. 2022, *MEPS* 698:85 | AO | The amphipod *Ampithoe marcuzzii* chose between the water-soluble cues of three seaweeds in a two-current flume, and avoided predator cues. | No distances in the abstract. Whether the seaweeds were intact is not stated. |
| C5 | Fink et al. 2006, *J. Chem. Ecol.* 32:1867 | AO | Volatiles from disintegrated cells of a mat-forming green alga attracted the freshwater snail *Radix ovata* without contact. A mimic of four volatiles attracted the snails; its two parts alone did not. | Freshwater biofilm alga. The assay separates odour from taste. |
| C6 | Moelzner & Fink 2014, *J. Anim. Ecol.* 83:1007 | AO | Snails distinguished high- from low-nutrient algae by odour alone; the volatile blend differed in quantity and composition with nutrient content. | Lab assays. Whether the odour came from intact or damaged algae is not stated in the abstract. |
| C9 | Addis et al. 2023, *J. Appl. Phycol.* 35:1407 | AO | Fresh, thawed and fragmented *Ulva* all stimulated *Paracentrotus lividus*, thawed most; glycine, alanine and glutamine (with a dose–response for glutamine) and glucose stimulated too. | Close-range stimulation assay ("urchinogram"), not distance. |
| C10 | Luis & Gago 2021, *Front. Mar. Sci.* 8 | AO | Echinoid chemical responses are described as sluggish and so far only qualitative. The urchin discriminated a few proteins, starch and one fatty acid; kelp and mussel flesh, but not their extracts, were perceived. | Contact stimulation on the aboral surface. |
| C11 | Gonçalves & Kiørboe 2015, *Limnol. Oceanogr.* 60:1286 | AO | Feeding-current copepods respond to phytoplankton cells only within a few prey radii of their feeding setae. Long-range chemical detection is incompatible with known algal leakage rates and sensible sensitivities; near-field chemical detection is too slow; reported detection distances fit fluid-mechanical (touch-like) detection. | Review plus new data. Plankton scale (sub-millimetre). |
| C13, C14 | Steinke et al. 2006; Breckels et al. 2013 | AO | A 1 µmol L⁻¹ DMS plume made *Temora* tail-flap, a search-linked behaviour (C13). Grazing-induced DMS did not change *Calanus* swimming time in pooled data, with large individual variation (C14). | Tethered or near-tethered copepods. |
| C3 | Wyeth 2019, *J. Exp. Biol.* 222 | AO; SEC via C15 | Aquatic gastropods likely navigate by chemotaxis, odour-gated rheotaxis, or both (AO). Per C15, Wyeth found too few still-water studies to support kinesis in snails (C15 PDF p.6). | Review. |
| C4 | Wyeth et al. 2006, *Biol. Bull.* 210:97 | AO | In the field, *Tritonia* crawled upstream, most strongly before feeding or mating, and turned downstream from an upstream predator. | Time-lapse field video. A predatory slug, used here for the mechanism. |
| C15 | Yu et al. 2023, *Front. Mar. Sci.* 9 | FT (term search) | Cites a whelk that tracks prey by smell from 1–2 m (Ferner & Weissburg 2005, SEC; PDF p.2). Chemotaxis needs two sensors or one that moves; odour-gated rheotaxis needs one chemical sensor plus a sense of flow (PDF p.6). | Review; the herbivore-specific content is thin. |

### Q2. What living algae and plants give off

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C2 | Frings & Frings 1965 | FT | The attractant from undamaged *Ulva* blades was water-soluble, non-volatile (air bubbled through *Ulva* water did nothing), heat-stable and stayed in the water phase after ether extraction (PDF p.2–4). Freshly gathered *Ulva* worked best; *Ulva* left in a crowded lab container lost its attractiveness within a few days, and rotting, however slight, reduced or abolished it (PDF p.2). The thick basal parts of the plant were not attractive and were not eaten unless the mouth was flooded with *Ulva* water (PDF p.5–6). | One alga, one herbivore. The compound was not identified. |
| C16 | Van Alstyne et al. 2001, *MEPS* 213:53 | AO | DMSP made up 0.04–1.8% of fresh mass in seven green algae and one red; crushing *Ulva* and *Polysiphonia* lost DMSP and made DMS, so damage triggers cleavage. DMSP attracted both urchin species as a feeding attractant; its product acrylic acid deterred them; an isopod was not deterred. | 39 species surveyed; feeding assays. "Attractant" here means in food offered, not at a distance. |
| C17 | Dacey & Wakeham 1986, *Science* 233:1314 | AO | DMS release from phytoplankton rises greatly under zooplankton grazing. | Foundational. |
| C18 | Moelzner & Fink 2015, *Oikos* 124:1603 | AO | Snail grazing on a biofilm alga released wound-associated volatiles; snails responded only above a threshold, and grazing-made concentrations reached it. | Freshwater; mass-balance model. |
| C19 | Jüttner et al. 2010, *MEPS* 400:63 | AO | A seagrass-epiphyte diatom released aldehydes and plant-like C6 compounds on cell disintegration; 17 meadow invertebrates reacted in species-specific ways. | Food-choice tests. |
| C20 | Paine et al. 2021, *J. Phycol.* 57:1375 | AO | Ambient DOC release 0–266 (green), 0–90 (brown), 0–41 (red) µmol C g DW⁻¹ h⁻¹; release is passive leakage plus active exudation; it rises with desiccation (up to about 90 times), high light, odd temperature or salinity, and CO₂. | Review converting published rates to one unit. |
| C21 | Haas et al. 2011, *PLoS ONE* 6:e27973 | AO | All five algae, but not the coral, exuded labile DOC; the highest photosynthesisers released most; turf released 14.0 ± 2.8 µmol h⁻¹ dm⁻², about twice the others per area; microbes drew it down within two-day incubations. | Moorea back reef. |
| C22 | Thornton 2014, *Eur. J. Phycol.* 49:20 | AO | Phytoplankton release 2–50% of fixed carbon as DOM; cell death presumably raises release. | Review. |
| C26 | Nocchi et al. 2017, *PLoS ONE* 12:e0187126 | AO | Elatol, a sesquiterpene of *Laurencia dendroidea*, attracted the associated sea hare. | Specialist host cue. |
| C25 | Hay 2009, *Annu. Rev. Mar. Sci.* 1:193 | FT (AM) | Generalists respond to primary metabolites (sugars, proteins, amino acids, respiration products, wastes), and the particular food may be read from their ratios; specialists cue on host-specific secondary metabolites; natural blends act more strongly than single peptides (section "Food"). A specialist may need a cascade: a cue to find the host from a distance, a contact cue, then a gustatory cue (section "Food"). Fish and crabs that eat snails were drawn more to seaweed grazed by snails than to ungrazed or artificially damaged seaweed (Coleman et al. 2007, SEC; section "Getting lunch without becoming lunch"). | Review. |

### Q3. The shape of the signal

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C29 | Moore & Crimaldi 2004, *J. Mar. Syst.* 49:55 | FT | Most cue molecules diffuse at about 10⁻⁹ m² s⁻¹ in water, and the time to diffuse a distance L is L²/2D, so diffusion slows sharply with distance (PDF p.2). Turbulent stirring draws a cue into thin filaments and mixing smooths them; diffusion sets the smallest gradient that can exist (PDF p.2–3). At the smallest scales gradients are small and smooth (PDF p.4). | Review with the governing physics. |
| C30 | Zimmer-Faust et al. 1995, *Biol. Bull.* 188:111 | FT | In a tidal creek, turbulent mixing coefficients were only 0.5–1.5 cm² s⁻¹ (PDF p.2, journal p.112). The time-averaged plume was Gaussian and decayed gradually downstream, measured out to 275 cm; fast electrochemical sampling showed bursts of high concentration alternating with zero (p.112). The plume edge was 2–4 cm wide, against a crab 10–15 cm across, so the gradient was steep across the stream but not along it (p.112). | Field, blue crabs and clam odour, flows to about 30 cm s⁻¹. |
| C31 | Weissburg & Zimmer-Faust 1993, *Ecology* 74:1428 | AO | Higher flow or coarser sediment raised boundary-layer turbulence and lowered crabs' success; fast flow also lowered the chance of meeting the plume. A thick viscous sublayer helped. | Flume. |
| C28 | Weissburg 2000, *Biol. Bull.* 198:188 | FT | In still water, diffusion gives an exponentially decaying, ever shallower gradient around a source (PDF p.2). Turbulent diffusivities are 10⁵–10⁶ times molecular ones (PDF p.8). Many animals, gastropods among them, show odour-conditioned rheotaxis or have trouble finding sources in quiescent water (PDF p.8). Male copepods follow a female's diffusive trail up to 10 cm, about 100 body lengths (PDF p.5). | Review. |
| C32 | Michaelis et al. 2020, *Sci. Rep.* 10 | AO | In a simulated large plume, intermittency is detectable with samples over seconds and is most informative along the plume edge. | Model, lobster framework. |
| C12 | Kiørboe & Thygesen 2001, *MEPS* 211:15 | AO | A leaking, sinking aggregate leaves a long, slender plume that a cruising copepod can cross; the fluid-mechanical signal fades too fast to explain colonisation. | Model. |
| C1, C2 | Teyke 1992; Frings 1965 | AO; FT | See Q1: in still water, no gradient beyond a few centimetres and a random search (C1); in quiet or one-way moving water, fairly direct approach, poor with turbulence from an aerator (C2). | |

### Q4. Living food against dead food

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C34 | Zimmer-Faust 1987, *Biol. Bull.* 172:10 | FT (term search) | Free amino acids are abundant in living invertebrate tissue, up to 8–10% of dry weight, and diffuse rapidly from carrion once prey dies, so they probably mark living or freshly killed prey (PDF p.2, journal p.11). Decomposing bacteria take up amino acids and release ammonia, so the amino-acid to ammonia ratio falls with carrion age (p.11). Six crustacean species searched to amino acids, not ammonia; ammonia suppressed feeding and made the bathypelagic species tail-flip away (PDF p.7–8). The ratio puts living and dead prey on one continuum (PDF p.16). | Crustacean scavenger-predators, lab assays. Animal food only. |
| C2 | Frings & Frings 1965 | FT | Rotting abolished the attractiveness of *Ulva* water, and aged lab *Ulva* lost it in days (PDF p.2). | The only plant test found. |
| C35 | Ide et al. 2006, *MEPS* 317:193 | AO | A scavenging amphipod responded to neutral and basic amino acids from fish muscle (glycine, alanine, arginine), with acidic amino acids adding synergy, at a threshold of 10⁻⁷ M. | Specialised carrion feeder. |
| C36 | Derby & Sorensen 2008, *J. Chem. Ecol.* 34:898 | AO | Fish and crustaceans detect amino acids, amines and nucleotides by both olfaction and gustation, and identify food, mates, predators and habitat from mixtures. | Review. |
| C25 | Hay 2009 | FT (AM) | Generalist cues are shared primary metabolites; specialist cues are host-specific (section "Food"). | |
| C10 | Luis & Gago 2021 | AO | Kelp flesh and mussel flesh were both perceived by an urchin the authors class as omnivorous. | |

### Q5. What bounds the cue

| # | Paper | Status | Finding with locator |
|---|---|---|---|
| C35 | Ide et al. 2006 | AO | Locomotory threshold for amino acids 10⁻⁷ M, lowered when animals were held at 8 × 10⁻⁸ M (sensory adaptation). |
| C12 | Kiørboe & Thygesen 2001 | AO | A threshold near 0.4 × 10⁻⁷ M for amino acids would explain observed copepod colonisation of aggregates. |
| C2 | Frings & Frings 1965 | FT | *Ulva* water still worked at a dilution the authors estimate at 1 in 15,000,000 of dissolved material or more (PDF p.2); the stock lost its effect after a further five- to sevenfold dilution (PDF p.3). |
| C18 | Moelzner & Fink 2015 | AO | A threshold level of algal volatiles was needed for a response. |
| C37 | Simó et al. 2000, *MEPS* 203:1 | AO | Turnover time constants of 0.7–5.4 d for total DMSP and 0.3–2.1 d for DMS in open seawater. |
| C38 | Keil & Kirchman 1991, *MEPS* 73:1 | AO | Dissolved free amino acids met about half of bacterial nitrogen demand over 36–130 h experiments. |
| C21 | Haas et al. 2011 | AO | Algal DOC was consumed by bacterioplankton in two-day incubations. |
| C34 | Zimmer-Faust 1987 | FT | Bacteria take up the amino acids leaking from carrion and release ammonia (PDF p.2). |
| C11 | Gonçalves & Kiørboe 2015 | AO | Near-field chemical detection is limited by diffusion across the sensor's own boundary layer, which is slower than observed capture times. |
| — | Costs of sensing | — | No source in the pool measures an energetic cost of chemosensing or of searching. Moelzner & Fink 2014 (C6, AO) say only that searching is costly for slow animals. |

### Q6. Direction and identity

| # | Paper | Status | Finding with locator | Methods, sample, limits |
|---|---|---|---|---|
| C28 | Weissburg 2000 | FT | A gradient can be sampled by comparison in space or in time; larger animals gain because they spread receptors wider and move faster; spatial sampling is generally better, temporal sampling wins in weak gradients needing long integration (PDF p.5). Lobsters appear to turn toward the side with more odour, and crabs track plume edges; this tropotaxis needs fairly discrete edges (PDF p.7). The spatial integration factor is the sensor span over the plume width (about the dominant eddy, 10–100 cm in crab habitats); with spans of 1–10 cm it is about 1–100 for crustaceans and 10⁻²–10⁻⁴ for moths (PDF p.10). The paper's table pairs slow, small gastropods with temporal sampling (chemotaxis) and expects spatial comparison to be of little use to them, and echinoderms with both (PDF p.11). | Nondimensional analysis using published values. The gastropod and echinoderm rows are marked with question marks in the source. |
| C40 | Webster et al. 2001, *Limnol. Oceanogr.* 46:1048 | AO | The plume centreline's direction can be estimated from two instantaneous samples only if the sensors are farther apart than the spanwise integral length scale of the concentration field. | Laser-induced fluorescence in a turbulent channel. |
| C30 | Zimmer-Faust et al. 1995 | FT | On meeting the plume, crabs turned upstream within 1.5 s; 68 of 80 walked to the source, against 4 of 29 for a control plume (PDF p.4). At the edge they turned back in 50 of 61 cases without zigzagging (PDF p.5). The authors propose a present-or-absent reading compared across legs inside and outside the plume, and note that crabs searched unsuccessfully in still water in the earlier flume work (PDF p.5). | Upstream direction comes from flow, across-stream direction from the chemical edge. |
| C41 | Keller et al. 2003, *MEPS* 261:217 | AO | Without head (antennule) chemosensors crabs could not sustain upstream progress; without leg chemosensors they failed to follow the narrowing plume near the source. | Deafferentation. |
| C42 | Kraus-Epley et al. 2015, *J. Crustac. Biol.* 35:753 | AO | Crayfish with lesioned chemoreceptors still reached food by a rheotactic search, with worse upstream headings. | |
| C33 | Atema 1995, *PNAS* 92:62 | AO | Lobster receptors resolve 4 Hz and integrate over 200 ms, matching 4 Hz sniffing; spatial gradients can be read from the timing of odour patches. | |
| C2 | Frings & Frings 1965 | FT | The current sensors (rhinophores) and the cue sensors (oral tentacles) were different organs; an animal followed a current once the cue was in it (PDF p.4). | |
| C43 | Corey et al. 2013, *PLoS ONE* 8:e60551 | AO | Lobster olfactory neurons express ionotropic receptors; ligand-specific responses suggest each cell's tuning is set by the uncommon subunits it expresses. | Molecular and calcium imaging. |
| C36 | Derby & Sorensen 2008 | AO | Olfaction is especially suited to discriminating mixtures, gustation to locating and ingesting food. | Review. |
| C25 | Hay 2009 | FT (AM) | Generalists read primary metabolites and their ratios; specialists read host-specific compounds (section "Food"). | |
| C35, C34 | Ide 2006; Zimmer-Faust 1987 | AO; FT | The scavenger's amino-acid response shows synergy and adaptation (C35); ammonia and amino acids act antagonistically, one stimulating while the other suppresses (C34 PDF p.7). | |

## Answers

### Q1. How herbivores locate food at a distance

**Moderate evidence, mostly from slow molluscs and small crustaceans; none found for fish or for vision.**

- *Chemoreception dominates, and works at a distance only with flow.* In still water a sea hare searched at random and found seaweed 30 cm away no faster than an odourless object (C1, AO). In quiet or one-way moving water another sea hare went fairly directly to its alga, and turbulence from an aerator spoiled it (C2, FT). Amphipods, juvenile sea stars and freshwater snails choose or approach algal cues delivered in flow or by odour alone (C5, C6, C7, C8). Slow molluscs orient upstream in the field (C4, AO). A slow whelk tracks prey from 1–2 m (SEC via C15).
- *Touch and contact taste take over near the food.* Plankton grazers find single algal cells only within a few prey radii, by mechanoreception (C11, AO). Urchin chemosensing is shown only at contact or close range (C9, C10). The sea hare's cue doubles as a feeding stimulant at the mouth (C2, FT), and a specialist may need a separate contact cue before it feeds (C25).
- *Distances.* Centimetres in still water (C1); a field tracking distance of 1–2 m for a slow predatory snail (SEC); crabs tracked chipped-clam plumes over about 2.5 m in the field (C30, Fig. 2). No herbivore tracking distance beyond a flume was found.
- *Vision and fish.* No source was found on grazers finding algae by sight or on herbivorous fish finding algae at all.

### Q2. What living algae give off

**Strong that healthy tissue leaks dissolved organic matter continuously; moderate that it carries an attractant; strong that damage releases a different, larger signal.**

- *Continuous release from intact tissue.* Seaweeds leak and exude DOC all the time, per gram of dry mass (C20, AO) and per unit of area (C21, AO), and phytoplankton release 2–50% of fixed carbon (C22, AO). Undamaged *Ulva* blades gave off a water-soluble, non-volatile, heat-stable attractant (C2, FT). Seaweeds' water-soluble cues let an amphipod tell species apart (C7, AO). Generalists read sugars, amino acids and the like (C25, FT). DMSP sits in the intact tissue of many green algae and attracts urchins on contact (C16, AO).
- *Damage and grazing release more, and different, compounds.* Crushing cleaves DMSP to DMS and acrylate (C16, AO), grazing raises DMS release (C17, AO), grazing or cell disintegration releases volatile aldehydes that attract snails (C5, C18, C19, AO), and thawed or fragmented *Ulva* stimulates urchins most (C9, AO). Desiccation raises DOC release up to about 90 times (C20, AO). Some damage products repel: acrylic acid deters urchins (C16).
- *Health matters.* Fresh *Ulva* attracts; aged, crowded or rotting *Ulva* does not (C2, FT). Nutrient-rich algae smell different from poor ones (C6, AO).
- *Scaling.* Release is reported per gram of dry mass (C20) and per square decimetre (C21), and the highest photosynthesisers release most (C21). No source relates the attractant itself, as opposed to bulk DOC, to the amount of tissue.

### Q3. The shape of the signal

**Strong on the physics; moderate on behaviour in the field.**

- *Still water.* Molecular diffusion (about 10⁻⁹ m² s⁻¹) is slow and its gradient shallows with time (C28, C29, FT). *Calc:* from L = √(2Dt), a cue spreads about 0.35 mm in a minute, 2.7 mm in an hour, 13 mm in a day and 42 mm in ten days. A still-water cue is therefore a centimetre-scale halo, consistent with the sea hare's failure beyond a few centimetres (C1).
- *Moving water.* Advection carries a plume downstream far faster than diffusion; turbulence stirs it into filaments, so the instantaneous signal is bursts and gaps, while the time average is a Gaussian plume that thins downstream (C29, C30, FT). The across-stream gradient at the plume edge is steep (2–4 cm wide in a creek), while the along-stream gradient is shallow (C30). More turbulence lowers tracking success (C31, AO).
- *Strategies.* Odour-gated rheotaxis (move upstream when the cue is present) is the most widely reported, in crabs, crayfish, snails, slugs and the juvenile sea star (C8, C28, C30, C4, C42). Chemotaxis by comparison across the body steers back into the plume at its edges (C30, C28). Random search until close is what a slow herbivore does in still water (C1).

### Q4. Living against dead food

**Moderate for animal food (crustaceans); thin for plants (one 1965 paper).**

- For animal food, the chemistry differs with age: amino acids mark living or fresh tissue, ammonia marks decay, and the two act antagonistically on the forager (C34, FT). Scavengers respond to the amino acids of flesh at low thresholds (C35, AO).
- For plants, the one direct test found shows a herbivore's attractant vanishes from rotting or stale algae (C2, FT). Specialists use host-specific compounds (C25, C26).
- Whether herbivores and scavengers use different receptors was not found tested directly. Generalists of both kinds read amino acids (C25, C34, C35; urchins too, C9), so the overlap is real for omnivores.

### Q5. What bounds the cue

**Moderate for thresholds and decay; nothing on cost.**

- *Thresholds*: about 10⁻⁷ M of amino acids for a scavenging amphipod (C35, AO), about 4 × 10⁻⁸ M inferred for copepods (C12, AO), and an unidentified *Ulva* factor still active at very high dilution (C2, FT). Thresholds shift with adaptation (C35).
- *Decay*: microbes consume the cues. DMSP and DMS turn over in about 0.3–5 days (C37, AO); amino acids are a main nitrogen source for bacteria (C38, AO); algal DOC is drawn down within two days (C21, AO). Carrion loses its amino acids to bacteria, which add ammonia (C34, FT).
- *Cost*: no measurement found.

### Q6. Direction and identity

**Moderate for direction (crustacean-heavy); weak to moderate for separate channels.**

- *Direction is not sensed directly; it is computed.* Three ways are reported. Comparison across the body, two antennae or many legs at once (tropotaxis), needs sharp plume edges and a span wide enough for the plume (C28, C30, C40). Comparison over time with one sensor as the animal moves (klinotaxis or temporal sampling) suits weak gradients and small, slow animals (C28, C33). Combining the cue's presence with the felt direction of flow (odour-gated rheotaxis) gives the upstream bearing and needs no gradient at all (C30, C15). Real animals mix them: crabs go upstream on flow and steer across stream on edges (C30), and different sensor sets carry different jobs (C41). Flow sensing and chemical sensing can sit in separate organs (C2).
- *Body span.* Direction from a single instantaneous comparison works only if the sensors are farther apart than the plume's spanwise length scale (C40). With spans of 1–10 cm and plume widths of 10–100 cm, crustaceans can just do it; a small, slow snail is expected to rely on temporal sampling instead (C28, FT, with the gastropod row marked as uncertain in the source). In still water a centimetre-scale halo leaves a small animal nearly no gradient across its body (C29 Calc above; C1).
- *Identity.* Crustacean olfactory neurons are individually tuned by receptor subunits (C43, AO), olfaction is built for telling mixtures apart (C36, AO), and generalists versus specialists read different compound classes (C25, FT). Animals identify foods by blends and ratios more than by single compounds (C25; C34's amino-acid to ammonia ratio; C5's four-compound mimic). Separate receptor channels for "plant" and "carrion" in one grazer were not found tested.

## What it implies for a leaf sense in the world

*Every line in this section is my inference from the evidence above, not a finding.*

### Emission

- *Inference:* the evidence fits an emission that is continuous while the leaf is alive and healthy, scaled with the amount of living tissue, and that stops at death. DOC is reported per gram and per area (C20, C21), it rises with photosynthesis (C21), and a herbivore's attractant disappears from stale or rotting algae (C2). Of the three candidates the brief names, area or a measure of photosynthetic activity is better supported than stored energy. Growth rate has no direct support.
- *Inference:* a separate, stronger pulse on damage is also well supported (C16, C17, C18, C19, C9). In the world that would mean a leaf being eaten emits more while it is eaten. This is the cue grazers follow to grazers. It is also the cue that draws the grazers' predators (C25, SEC).
- *Inference:* a living plant's cue is plausibly weaker per unit of mass than a fresh corpse's. Carrion leaks its amino acids (up to 8–10% of dry mass) rapidly (C34), while a healthy plant leaks a share of its production slowly (C20, C22) and copepods cannot smell single algal cells (C11). That supports a leaf emission smaller per unit than a corpse's, not equal to it.

### Reach

- *Calc:* take a continuous point source of strength Q on the bed in a current, with a turbulent diffusivity K the same in all directions. Its time-averaged centreline concentration is C(x) = Q / (2πKx), falling as 1/x and independent of the current speed. Set Q to turf algae's whole DOC release from 1 dm² (14 µmol C h⁻¹, C21), counted as five-carbon molecules (7.8 × 10⁻¹⁰ mol s⁻¹), and K = 1 cm² s⁻¹ (C30). Then C at 1 m is about 1.2 × 10⁻⁹ M. That falls to the thresholds of 4 × 10⁻⁸ to 10⁻⁷ M (C12, C35) at x ≈ 1–3 cm. For 1 m² of leaf (100 times the source) the reach is about 1–3 m; for 10 m², about 10–30 m. These are upper bounds, because only a fraction of DOC is any one cue, and the instantaneous signal is patchier than the average (C30).
- *Inference:* so one small leaf should be smellable only at centimetres, and a dense bed at metres. With 1/x decay, reach scales linearly with the emission, and so with leaf area if emission does. The world's corpse cone reaches 10 m whatever the body. A leaf cone of the same shape would need its reach scaled with emission, not fixed.
- *Inference:* in still water the reach is a centimetre halo that grows over hours (Q3 Calc). The anchoring round recorded the world's currents at 3–6 cm/s (`logbook/specs/r59-anchor-lit/pass2.md`, "Q3 and Q4 in brief"). That is in the range where crabs and slugs track plumes (C30, C4), so in the world a leaf plume would lie downstream of the leaf, not in a cone around it. A symmetric cone is the still-water picture without the slow spread. Microbial decay over 0.3–5 days (C37) matters little over metres at cm/s, because water crosses a metre in under a minute.

### The four shapes

1. **Living leaves give off the same smell as corpses, on the same sense.** *Inference:* the evidence fits this worst. In crustaceans the fresh-or-dead chemistry differs and the two signals act against each other (C34). A plant attractant vanishes on rotting (C2). Herbivore cues identified so far are algal volatiles, DMSP and host metabolites (C5, C16, C18, C26), not the amino acids of flesh. It fits only for generalist omnivores, which do read amino acids from both (C9, C10, C25, C35). It would also send scavengers to leaves and grazers to corpses, with no way for a brain to tell them apart.
2. **Living tissue gets a smell of its own on a separate sense.** *Inference:* the evidence fits this best for the "what". Herbivores tell algae apart, and good from poor, by odour alone (C6, C7). The cue classes differ from carrion's (above). Receptor neurons are individually tuned (C43). Generalists and specialists read different chemistry (C25).
3. **A short-range smell, a steep cue for the last metre or two.** *Inference:* the evidence fits this for the "how far". Single plants are detected only near-field (C1, C11), still water gives centimetres (Q3 Calc), and the Calc above puts a small leaf's reach at centimetres. It fits too for a contact cue that releases feeding (C2, C25). As a fixed rule ("steep, short, whatever the leaf") it conflicts with the evidence for beds: many leaves together carry metres in flow (Calc). A steep short cue then comes out of shape 2 with a small, area-scaled emission, and need not be a rule of its own.
4. **A separate sense per substance, each giving its strength and its direction.** *Inference:* the "per substance" half fits the receptor and behaviour evidence well (C25, C36, C43), and extends shape 2 to every substance. The "direction" half is an abstraction the evidence does not show animals receiving. Direction is computed, by comparing two sensors, by comparing over time, or by pairing the cue with felt flow (Q6). A sense that hands the brain a bearing skips the body-span limit. Under that limit a small body in a broad plume gets no usable left-right difference (C28, C40), and in a narrow plume or near the source it does (C30). Two more faithful options exist. One: give each substance a strength read at two or more points on the body, and let the brain compare, so direction depends on body size and plume structure as it does in animals. Two: give each substance a strength, give flow as its own sense, and let upstream movement on a cue do the rest, which is the strategy most often reported (Q3).

### Which fits best

*Inference:* the evidence fits shape 2 best, with a small emission scaled with the leaf's living area (or photosynthesis) that stops at death, a larger pulse when the leaf is damaged, and reach set by that emission rather than fixed. Shape 3 then follows for a single leaf without a rule of its own. Shape 4 is shape 2 generalised. Its per-substance channels are well supported. Its direction readout is not, and the evidence favours strength-only channels whose direction the body works out: from two sensing points, from change over time, or from the current.

## What remains thin or open

1. **Herbivorous fish and vision.** Nothing found on either. Q1 rests on molluscs, small crustaceans, a juvenile sea star and plankton.
2. **The compound from intact plants.** Only one paper (C2, 1965) tests a herbivore's attractant from intact, stale and rotting tissue, and it did not identify the compound. Bulk DOC is measured, but its sensed fraction is not.
3. **Emission scaling of the cue itself.** Per-gram and per-area rates exist for DOC only. Whether the attractant scales with area, biomass or growth is untested in the pool.
4. **Herbivore tracking distances in the field.** None found. The metre-scale numbers come from crabs, a whelk (SEC) and models.
5. **Separate receptors for plant and carrion cues in one grazer.** Not found tested. The channel argument rests on crustacean receptor tuning and on cue chemistry.
6. **Sea urchins at a distance.** Only contact-range assays (C9, C10) and a feeding attractant (C16). A claim that urchins orient upstream to distant food was seen only in a search summary.
7. **Cost of sensing.** No measurement found.
8. **Abstract only for many anchors.** C1, C3, C5, C6, C7, C11, C12, C16, C18, C20, C21, C35, C40, C41 and C43 are abstract only. The reach Calc rests on two of them (C21 and C35) and on two assumptions: all of the DOC counted as cue, and an isotropic diffusivity.
