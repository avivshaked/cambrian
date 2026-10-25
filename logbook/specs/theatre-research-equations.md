# Verified reading list: equations for marine skin patterns and forms

Compiled 2026-10-25 by a research subagent for the coordinator, at the owner's question of 2026-10-25 on whether marine looks have been described by equations. Each item was matched on at least two of Crossref, OpenAlex, Europe PMC, publisher or society pages. Semantic Scholar refused access, so it counts as no source. "Read" means the text was read; "abstract only" means abstract or secondary summary. Where an equation is not given, it was not read, and nothing is filled from memory.

## Summary table

| # | Work | Verified | What to borrow | Read? |
|---|---|---|---|---|
| 1 | Turing 1952 | Yes | The reaction-diffusion principle | Abstract only |
| 2 | Gierer & Meinhardt 1972 | Yes | Activator-inhibitor equations: short-range activation, long-range inhibition | Abstract only (closed equations in item 7) |
| 3 | Pearson 1993 | Yes | Gray-Scott (F, k) map of pattern types | Full text (arXiv copy) |
| 4 | Kondo & Asai 1995 | Yes | Angelfish stripes are a live RD wave that re-spaces as the fish grows | Abstract only |
| 5 | Kondo & Miura 2010 | Yes | Review: which interaction gives spots, stripes, labyrinths | Abstract only |
| 6 | Meinhardt, Algorithmic Beauty of Sea Shells | Yes | Shell pigment equations, parameter sets for about 20 species | Not read |
| 7 | Fowler, Meinhardt & Prusinkiewicz 1992 | Yes | Helico-spiral sweep geometry, pigment RD on the growing edge | Full text |
| 8 | Turk 1991 | Yes | RD on any surface mesh; cascaded systems (rosettes, multi-width stripes) | Full text |
| 9 | Witkin & Kass 1991 | Yes | Anisotropic diffusion: oriented stripes along a direction field | Summary only |
| 10 | Prusinkiewicz & Lindenmayer 1990 | Yes | L-systems for branching | Not read |
| 11 | Raup 1966 | Yes (no DOI) | Four-parameter shell coiling morphospace (W, D, T, S) | Abstract and summaries |
| 12a | Sharon et al. 2002 | Yes | Edge-buckling cascades: self-similar ruffled margins | Summary only |
| 12b | Sharon, Marder & Swinney 2004 | Yes | Review of 12a for leaves and flowers | Not read |
| 13 | Sims 1991 | Yes | Evolving texture and image genomes | Not read |
| 14 | Reiter et al. 2018 | Yes | Chromatophore field by lateral inhibition; few state variables drive the skin | Mostly read (PMC) |
| 15 | D'Arcy Thompson 1917 | Yes | Coordinate transformations of form | Not read |
| 16 | Nakamasu et al. 2009 | Yes | Three-variable pigment-cell RD with a full parameter table | Mostly read (PMC) |
| X1 | Alessio & Gupta 2023 | Yes | Diffusiophoresis sharpens RD edges; matches boxfish and moray | Read (PMC) |
| X2 | Sun 2006 | Yes | Multilayer interference (iridescence) in RGB renderers | Summary only |
| X3 | Belcour & Barla 2017 | Yes | Thin-film iridescence added to microfacet BRDFs | Summary only |
| X4 | Chirat, Moulton & Goriely 2013 | Yes | Mechanical morphospace of shell spines | Summary only |

No verifiable paper models nudibranch colour patterns with equations; the nearest marine sources are items 4, 5, X1 and 14.

## Details

### 1. Turing 1952
Turing, A. M. (1952). The chemical basis of morphogenesis. *Phil. Trans. R. Soc. Lond. B* 237(641): 37-72. DOI 10.1098/rstb.1952.0012. Closed access per OpenAlex. Borrow: the principle that reacting, diffusing morphogens make a pattern from a nearly uniform start.

### 2. Gierer & Meinhardt 1972
Gierer, A. & Meinhardt, H. (1972). A theory of biological pattern formation. *Kybernetik* 12(1): 30-39. DOI 10.1007/BF00289234. Green open-access copy listed at edoc.bbaw.de (not opened). Borrow: the activator-inhibitor idea; item 7 gives its concrete form.

### 3. Pearson 1993 (full text read)
Pearson, J. E. (1993). Complex patterns in a simple system. *Science* 261(5118): 189-192. DOI 10.1126/science.261.5118.189. Open via arXiv patt-sol/9304003.
- Model: U_t = Du∇²U − UV² + F(1−U); V_t = Dv∇²V + UV² − (F+k)V, with Du = 2e-5, Dv = 1e-5, a 2.5 × 2.5 periodic domain, 256² grid, forward Euler, from U=1, V=0 with a small perturbed square.
- Figures 2 and 3 map twelve pattern types over (F, k). F=0.02, k=0.059 gives spot multiplication. Only the labelled regimes should be trusted.

### 4. Kondo & Asai 1995
Kondo, S. & Asai, R. (1995). A reaction-diffusion wave on the skin of the marine angelfish Pomacanthus. *Nature* 376: 765-768. DOI 10.1038/376765a0. Closed. Abstract only: stripes rearrange to keep their spacing as the fish grows, and a Turing simulation predicts them. Licenses animating stripe splitting and re-spacing over growth.

### 5. Kondo & Miura 2010
Kondo, S. & Miura, T. (2010). Reaction-diffusion model as a framework for understanding biological pattern formation. *Science* 329(5999): 1616-1620. DOI 10.1126/science.1179047. Closed. An index to which RD family fits which pattern.

### 6. Meinhardt, The Algorithmic Beauty of Sea Shells
Springer, The Virtual Laboratory; 1995 (DOI 10.1007/978-3-662-13135-0), 1998 (10.1007/978-3-662-03617-4), 4th ed. 2009 (10.1007/978-3-540-92142-4). Paywalled. Not read; the larger parameter catalogue behind item 7.

### 7. Fowler, Meinhardt & Prusinkiewicz 1992 (full text read)
Modeling seashells. *Proc. SIGGRAPH '92*: 379-387. DOI 10.1145/133994.134096. Open PDF at algorithmicbotany.org/papers/shells.sig92.html.
- Form: a logarithmic helico-spiral swept by a generating curve; curve orientation, a Bézier shape and a periodic displacement give ribs.
- Pigment: RD runs along the 1D growing edge, each time step one row. Activator-substrate (eq. 8) and activator-inhibitor with a global hormone (eq. 9). Worked parameter sets in the captions for Amoria undulata, Volutoconus bednalli, Oliva porphyria and Conus marmoreus.
- Looks: stripes, wavy lines (sine-modulated source), annihilating travelling waves (oblique and "<" marks), branching waves. Spines and varices are poorly reproduced.

### 8. Turk 1991 (full text read)
Generating textures on arbitrary surfaces using reaction-diffusion. *Proc. SIGGRAPH '91*: 289-298. DOI 10.1145/122718.122749. Open PDF on the author's Georgia Tech page.
- Two-chemical RD with source term s(16 − a·b); s sets spot size. Cascades: a first system lays a pattern that a second refines, giving rosettes, nets and multi-width stripes.
- Surface method: relax points evenly over the surface, Voronoi neighbours, run the RD on that mesh, render by weighted sums; also usable as a bump map. Lets a pattern follow the body mesh with no UVs.

### 9. Witkin & Kass 1991
Reaction-diffusion textures. *Proc. SIGGRAPH '91*: 299-308. DOI 10.1145/122718.122750. Summary only: anisotropic, spatially varying diffusion gives stripes along a chosen direction field.

### 10. Prusinkiewicz & Lindenmayer 1990
*The Algorithmic Beauty of Plants.* Springer. DOI 10.1007/978-1-4613-8476-2. Free PDF reported at algorithmicbotany.org (unchecked). Bracketed L-systems for branching corals, sea fans, kelp, sponges.

### 11. Raup 1966
Geometric analysis of shell coiling: general problems. *Journal of Paleontology* 40(5): 1178-1190. No DOI. Four parameters, W (whorl expansion), D (distance from the axis), T (translation) and S (generating-curve shape), give most spiral shells; real taxa fill only parts of the space. The precedent for a small parameter vector mapping to form.

### 12. Sharon et al.
(a) Sharon, Roman, Marder, Shin & Swinney (2002). Buckling cascades in free sheets. *Nature* 419: 579. DOI 10.1038/419579a. Up to six generations of self-similar edge waves, each at a smaller wavelength, from a hyperbolic metric at the edge. Rule for ruffled, frilled margins.
(b) Sharon, Marder & Swinney (2004). Leaves, flowers and garbage bags: making waves. *American Scientist* 92(3): 254. DOI 10.1511/2004.3.254. Not read.

### 13. Sims 1991
Artificial evolution for computer graphics. *Proc. SIGGRAPH '91*: 319-328. DOI 10.1145/122718.122752. Not read.

### 14. Reiter et al. 2018 (mostly read)
Elucidating the control and development of skin patterning in cuttlefish. *Nature* 562: 361-366. DOI 10.1038/s41586-018-0591-3. Free via PMC6217936 (manuscript under an earlier title).
- Control: about nine dimensions explain 85% of pattern variance; chromatophores act in motor groups, so a few state variables can drive a skin.
- Development: new chromatophores are inserted where a surround-inhibition field allows; kernel J(x) = 1/(1+e^(k(|x|−s0))), with s0 shrinking with age (about 75.6 µm initially, −2.8% a day); skin grows about 2.06% a day. A point-process rule for a chromatophore field.

### 15. D'Arcy Thompson 1917
*On Growth and Form.* Cambridge University Press. BHL DOI 10.5962/bhl.title.11332. Not read. Grid deformation relating one body plan to another.

### 16. Nakamasu et al. 2009 (mostly read)
Interactions between zebrafish pigment cells responsible for the generation of Turing patterns. *PNAS* 106(21): 8429-8434. DOI 10.1073/pnas.0808622106. Open, PMC2689028. Three variables (melanophores u, xanthophores v, long-range factor w). Wild-type parameters: c1=−0.04, c2=−0.055, c3=0.37, c4=−0.05, c5=0, c6=0.25, c7=0.016, c8=−0.03, c9=0.24; cu=0.02, cv=0.025, cw=0.06; Du=Dv=0.02, Dw=0.2; U=V=W=0.5; 128² grid. Leopard changes c3 to 0.385. The equation forms were images and were not transcribed.

### X1. Alessio & Gupta 2023 (read)
Diffusiophoresis-enhanced Turing patterns. *Science Advances* 9(45): eadj2457. DOI 10.1126/sciadv.adj2457. Open, PMC10631721. Adds a diffusiophoretic advection term to Brusselator RD; Péclet about 10 to 50 best matches nature; sharper colour edges without changing spacing. Compared with the ornate boxfish and jewel moray.

### X2. Sun 2006
Rendering biological iridescences with RGB-based renderers. *ACM TOG* 25(1): 100-129. DOI 10.1145/1122501.1122506. Multilayer interference for RGB renderers; examples are butterflies and beetles, the marine use is inference.

### X3. Belcour & Barla 2017
A practical extension to microfacet theory for the modeling of varying iridescence. *ACM TOG* 36(4). DOI 10.1145/3072959.3073620. Thin-film interference with varying thickness in microfacet BRDFs; a thickness map as a shader input.

### X4. Chirat, Moulton & Goriely 2013
Mechanical basis of morphogenesis and convergent evolution of spiny seashells. *PNAS* 110(15): 6015-6020. DOI 10.1073/pnas.1220443110. A few control parameters give a large diversity of spines. Parameters not read.

## What to borrow first (coordinator's reading)
- Nested patterns: Turk's cascades (8) and the Fowler/Meinhardt edge models (7).
- Ruffled margins: the edge-buckling cascade (12a).
- One skin's patterns: Turk's surface method (8), with Witkin & Kass's oriented diffusion (9) for stripes that follow the body.
- Sharper, more natural colour edges: the diffusiophoresis term (X1).
- Colour cells: Reiter's inhibition-field placement and low-dimensional control (14).
- Genome-to-look precedent: Raup's morphospace (11).
