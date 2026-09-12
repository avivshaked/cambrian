# Theatre UI — build spec

*How to turn the contents of this folder into a working interface. Written 2026-09-12, after
two design passes. For any agent picking this up cold: read this file first, then
`canvas/theatre.uss`. You do not need to have been part of the design conversation.*

*The open points below (§5's residual, §6's checks, the width, the fonts, the pictures)
were decided on 2026-09-12 night in `logbook/specs/theatre-ui-spec.md`, and the way the
build is tested is `logbook/specs/theatre-ui-test-spec.md`; a builder reads those two after
this file. Its place in the work is in `HANDOFF.md` under "The theatre's interface".*

---

## 1. What you are building

The **theatre** is this project's viewer: it reads a recorded run from `runs/<arm>/<run>/` and
replays it with rendering on, so a person can watch a world of evolved creatures and check
whether anything is wrong. It already exists and works. Its *visuals* are good — dark-field
water, light shafts, carved bodies. Its *interface* is a raw immediate-mode text block in the
top-left corner, and that is what you are replacing.

What the interface is for, in one line each: **is this the run or a lookalike**, **what does
the world look like**, **is anything broken**, **what is this creature I clicked**.

### The folder

| Path | What it is |
|---|---|
| `canvas/theatre.uss` | **The authority.** Every visual value — palette, type scale, spacing, every component. Written as real USS, with eight documented sections at the end covering settled values, spacing, exceptions, resolution, declaration order, the glyph set and number formatting. |
| `canvas/Theatre UI.dc.html` | The picture. Nine artboards showing the design in every state. Its `class` attributes name rules in `theatre.uss`; its inline styles are only how a browser draws it. **Never port a value from here** — the stylesheet won every disagreement between the two. |
| `canvas/assets/` | The three backdrops the artboards sit on. |
| `reference-frames/` | The same three frames as originals from the theatre, with provenance. What the UI must stay legible over. |

Read `theatre.uss` top to bottom. It is commented as a document, not as a stylesheet, and the
sections after the rules answer most of what you would otherwise ask.

## 2. Where the code goes, and the one rule you must not break

**Everything lands in `unity/Assets/Theatre/`. Nothing goes in `unity/Assets/Evosim/`.**

`simHash` is a digest of every `.cs` file under `Assets/Evosim`, and it is what tells a viewer
whether a replay is faithful. A UI label placed there changes the hash, and every run ever
recorded then refuses to replay as itself. This has happened once already. The theatre sits
beside the simulation for exactly this reason, and the `Evosim.Theatre` asmdef enforces the
one-way reference.

UXML and USS are not `.cs`, so they could technically live anywhere — put them in
`Assets/Theatre/UI/` with the code that loads them.

## 3. What you are replacing

`unity/Assets/Theatre/TheatreRunner.cs`, the IMGUI block — `OnGUI()` at line 638 through
`Shorten()` at the end of the file. Five methods:

| Method | Becomes |
|---|---|
| `OnGUI()` | a `UIDocument` with a `PanelSettings`, built once |
| `WorldOverlay()` | the strip, the census panel, the warnings panel, the bottom bar |
| `SoloOverlay()` | the same strip with a different census — see §8 |
| `SelectionLine()` | the inspector panel, three states |
| `Keys()` | key hints distributed into the strip and bar, not a block of text |

Keep every existing key binding: `Space` `[` `]` `K` `C` `F` `R` `Esc` `H`, `WASD`+`QE`,
right-drag, wheel, `Shift`; `T` and `G` in solo mode. The design adds `P` for the provenance
popover. Panels must not swallow the mouse — a left-click in the water selects a creature and
right-drag looks around, so the centre of the screen stays clear by design.

## 4. Setting up UI Toolkit

**Fonts.** IBM Plex Mono (Regular, SemiBold) and IBM Plex Sans (Regular), both SIL OFL and
freely redistributable. Import as Unity font assets and generate SDF atlases. The stylesheet
references them through `resource()` in custom properties, so they must sit under a
`Resources/Fonts/` folder.

A third Sans asset is needed with **line spacing 1.5 baked in** — the stylesheet calls it
`--font-sans-prose` and uses it wherever the web mockup used `line-height`, because USS has
no such property.

**The glyph set must be baked deliberately.** An SDF atlas contains only what you put in it,
and the design uses twelve non-ASCII characters:

```
·  U+00B7   separator — 75 uses, carries the whole identity line
—  U+2014   em dash            ×  U+00D7   multiplication and mismatch
←  U+2190   ancestry chain     →  U+2192   seek target
Σ  U+03A3   cumulative title   −  U+2212   true minus, depth only
≠  U+2260   cousin badges      …  U+2026   elided ancestry
–  U+2013   en dash, ranges    §  U+00A7   prose refs, mockup only
U+2009 THIN SPACE — the thousands separator. Appears in no file as a literal
       character (both documents write it as a code point or a plain space),
       so it is easy to miss and everything numeric depends on it.
```

**Resolution.** USS has no media queries. The stylesheet's answer: one stylesheet, chrome
fixed in pixels, a **breakpoint at 2240** — not 2560, so that a large window which is not
maximised does not get wide chrome. Drive the handful of changed tokens from C# at that
breakpoint. Do not use Unity's uniform panel scaling: it would scale the type, which the
design explicitly rules out ("the water gains the area, not the chrome").

## 5. The data contract

This is the part the designer could not write, because it needs the codebase. Every field on
screen, and where it comes from.

### Already available — wire it up

| On screen | Source | Cadence |
|---|---|---|
| arm, seed, dt, config hash | `RunRecord.ArmName` / `.Seed` / `.PhysicsDtSeconds` / `.ConfigHash` (first 10 chars) | once |
| physics jobs, "as recorded" | `TheatreReplay.PhysicsJobWorkers`, `.ThreadCaveat` | once |
| provenance state | `.Faithful`, `.SourceDifference`, `.ThreadCaveat`, `.FirstMismatch`, `.ElapsedSeconds` vs `.RecordedThroughSeconds`, `CreatureIdMap.Reliable` | per sample |
| "24 of 24 samples match" | `TheatreReplay.IdentityLine()` | per sample |
| clock `t` | `WorldCensus.T` | **per frame** |
| pace, paused | `Rate`, `_measuredPace`, `Paused` | **per frame** |
| seeking, target, ETA | `_seeking`, `_seekTarget`, `Remaining()` | **per frame** |
| alive, births, deaths, jointed, absorptive, photosynthetic, diverged | `WorldCensus` | per sample |
| audit %, matter here, mean depth | `WorldCensus.AuditPercent` / `.MatterHere` / `.MeanHeight` | per sample |
| the three warnings | `RunRecord.ConfigHashMismatch`, `.StepDisagreement`, `.SamplesNote` | once |
| past-the-record warning | `ElapsedSeconds > RecordedThroughSeconds` | per sample |
| creature id, generation, parent, guild, reserve | `CreatureIdMap.Find()` → `Organism` | per sample |
| creature speed | root child's `ArticulationBody.linearVelocity.magnitude` | per frame |

**The cadence split is a performance contract, not a detail.** Four text fields and one width
update per frame; everything else is touched only when a sample arrives, roughly once per
hundred simulated seconds. The theatre often runs beside five simulation processes. The
stylesheet marks the per-frame elements `.is-per-frame` — it is the one class with no rule,
deliberately, because it is a marker for you rather than a visual.

### Needs new plumbing

| On screen | What it needs |
|---|---|
| timeline: record-end mark | `RecordedThroughSeconds` — available, just never drawn |
| timeline: peak-population mark | max `RunSample.Alive` over loaded samples. `RunSample` already carries `Alive`, so this is free |
| timeline: snapshot marks | list `snapshots/*.jsonl` and read `t` from the filenames |
| inspector: died at, lived, children, ancestry | **`lineage.jsonl`, which nothing in the theatre reads today.** `RunRecord` deliberately loads only `config.json` and `stats.jsonl` |

**The lineage index.** The dead-creature panel shows `DIED t=25 040`, `lived 3 120 s` and
`children 4`, and the glyph set includes `←` for an ancestry chain. All of it is real data:
`lineage.jsonl` carries birth rows `{"e":"b","t","id","p","g","abs","jnt","pho",…}` and death
rows `{"e":"d","t","id","c"}`. So death time is stored, lifespan is death minus birth, children
is a count of birth rows whose `p` is this id, and ancestry is a walk up `p`.

Three constraints on building that index:

1. **Stream it, never load it.** A run's `lineage.jsonl` reaches hundreds of megabytes — the
   r37-s1 sample used here held ~17,600 births and ~16,900 deaths in its first stretch alone.
2. **Use `JsonlWriter.ReadRows`, not `File.ReadAllLines`.** The latter opens with
   `FileShare.Read` and throws a sharing violation against a live run's writer.
3. Build it lazily, on first selection, not at load.

**Do not show cause of death.** Every death in this world reads `starved`, because `Starved`
is the only `DeathCause` implemented, so the field discriminates nothing. The design correctly
omits it; keep it omitted.

## 6. Verify these before building on them

The stylesheet is a careful reading of what USS can do, but several of its answers are
untested against a real Unity build. Each is sound reasoning that could still be wrong, and
each is cheap to check now and expensive to discover after seven panels are built on it.
**Spend an hour on these first.**

| # | Assumption | What depends on it | If it fails |
|---|---|---|---|
| 1 | Strike-through rebuilt without `text-decoration` (USS has neither that nor pseudo-elements) | Provenance state 5's whole story — "selection is struck through wherever it appears" — and the dead creature's id | An overlaid 1 px `VisualElement`, or a rich-text tag if UI Toolkit's label supports one |
| 2 | Line spacing baked into a font asset in place of `line-height` | Every multi-line prose block: inspector copy, the popover note, empty states | Per-line labels in a column, spaced with margins |
| 3 | The `.lead` scheme replacing `gap` — margin on the *following* sibling, zeroed on first children, since USS has no `:first-child` | The entire spacing rhythm; 19 uses | Set the margin from C# when building the hierarchy |
| 4 | A custom property can hold `resource(...)` for a font | Every text rule in the stylesheet | Name the font asset directly in each rule |
| 5 | Play and pause glyphs as bordered boxes — a zero-sized box with transparent borders is a browser technique | The transport state indicator | A text glyph (▶ ‖) or `Painter2D` |
| 6 | The 2240 breakpoint driven from C# | The 2560 layout | Ship the 1920 chrome at all sizes; it is correct, just tighter |
| 7 | `rotate: 45deg` on the cousin marker, given a 9 px box in a 12 px slot | One of the five provenance states | Draw the diamond as a glyph |

Charts are out of scope here, but note for later that UI Toolkit has no charting library and
no SVG: the `.chart-frame*` rules are scaffolding, and the plot area is drawn in C# with
`Painter2D`.

## 7. One bug to fix in passing

`SoloOverlay()` prints `{_solo.Genome.GlobalBrain.Length} global neurons`. **`Genome.GlobalBrain`
was retired by D081 on 2026-09-07** — a child is born without one, a rewire never draws the
input kind, and `Brain.For` builds no partless group. Genomes recorded before that build still
carry one or two global neurons that this build never evaluates, so the readout is a count of
neurons that do not run. Use `Brain.NeuronCount` (`src/Evosim.Core/Brain/Brain.cs:123`), which
is the body's count. Do not carry the old line across.

## 8. Scope

**Designed and ready to build:** the world view (§5.1 of the original brief) in every state —
faithful, faithful-but-unverified, cousin, diverged, ids-unverifiable, seeking, paused,
playing, one-warning, invariant-broken; the transport bar and timeline; the provenance popover;
the inspector in its three states — nothing selected, selected and dead, selection unavailable.

**Not designed, deliberately:** the single-creature mode, the run picker and world-spec panel,
the charts, the lineage view, the gallery, and the snapshot caption. These come in a later
pass, informed by what building the above teaches.

The design's own argument, which we accepted: the strip and bar are built to carry all of
them, and **the single-creature view should be a mode of the same screen rather than a second
design**. So when you replace `SoloOverlay()`, give it the same strip with a different census
rather than a parallel layout. Its fields are `_solo.Source`, `Phenotype.PartCount`,
`Instance.TotalDof`, `.Speed`, `.Travelled`, `.Depth`, `.MeanJointRate()`, `.ReserveSeconds`,
`.Starving`, `.UseTestSine`, `.SmellDensity`, and the seven sensor channels from
`ReadSensors()` — joint angle, joint rate, up, depth, chemical, energy, flow.

## 9. Done looks like

1. Every key binding in §3 still works, and `H` still hides all chrome for clean pictures.
2. The five provenance states are distinguishable **in a greyscale screenshot**. Status carries
   no hue at all; exactly one hue exists in the whole interface, a vermilion plate reserved for
   a broken invariant, and it is never text. Green, orange, grey and pink belong to the
   creatures and must appear nowhere in the chrome.
3. A number gaining a digit does not move anything. `.numeric` is a fixed 104 px column;
   quantities group with a thin space from 1 000 up, identifiers never group.
4. Nothing reflows when a warning appears or clears — warnings grow downward on the left over
   open water, and the census does not move.
5. The centre of the screen is never covered, and a click there still selects a creature.
6. Per-frame updates are the four fields in §5 and nothing else.
7. It is legible over all three frames in `reference-frames/` — the busy near-black crowd, the
   bright pale surface, and the near-empty wide shot. The bright one is the hard case.
8. `simHash` is unchanged. Check it against a run's `run.json` before and after; if it moved,
   something landed in `Assets/Evosim` that should not have.
