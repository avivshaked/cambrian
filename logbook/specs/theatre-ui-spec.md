# Build spec: the theatre's interface (design/SPEC.md, decided 2026-09-12 night)

The design is `design/SPEC.md` and the folder it describes (`design/canvas/theatre.uss` is the
authority for every visual value; `design/canvas/Theatre UI.dc.html` is the picture;
`design/reference-frames/` are the three frames the chrome has to stay legible over). Read
`CLAUDE.md` in full, then `design/SPEC.md` in full, then `design/canvas/README.md` and
`theatre.uss` top to bottom, then `unity/Assets/Theatre/TheatreRunner.cs` (the IMGUI block
from `OnGUI` at line 638 to the end), `TheatreReplay.cs`, `RunRecord.cs`, `CreatureIdMap.cs`,
`SoloCreature.cs`, and `unity/Assets/Theatre/Editor/TheatreSnapshot.cs` (how pictures are
taken). The test plan is `logbook/specs/theatre-ui-test-spec.md`; build its harness in the
same pass.

Rules: edit only under `unity/Assets/Theatre/` (code, UXML, USS, fonts, the tests entry under
its `Editor/`) and `scripts/` (the snapshot script's new switch); **nothing under
`unity/Assets/Evosim/`**, which is the one rule the spec says not to break and this project's
gotchas say why. Scratch under `D:\Projects\experiments\evolution-simulator\scratch\theatre-ui\`
only, never the session scratchpad or TEMP. No commit. Do not touch `runs/`, any `unity-w*`
worker, or a running process; never sleep, poll or wait. You cannot run Unity: the caller
compiles and runs the harness on a worker and reads the pictures. Say in the report what is
unverified.

## The decisions the spec left open, decided

1. **"Invariant broken" is the two identities and nothing else.** The vermilion plate fires
   when the energy audit's residual or the matter residual is outside the tolerance the
   replay already uses for the audit, and never on mean depth, matter here or any other
   reading, which a healthy world moves. `WorldCensus` carries the audit and not the matter
   residual: find where the report's `mat resid` column is computed and whether
   `stats.jsonl` carries a field for it. If the row carries it, plumb it through
   `RunRecord` and `WorldCensus` and show it beside the audit. If the row does not, the
   plate reads the audit alone, the census prints `matter residual: not recorded` in the
   empty-state style, and the report says so: the stats field is a change under
   `Assets/Evosim` and waits for the next simulation build.
2. **"Faithful" always carries its coverage.** The identity line reads `N of M samples match
   through t = T`, from `TheatreReplay.IdentityLine()`, in every faithful state; a run with
   `physicsJobWorkers` above 0 is its own provenance state (the thread caveat) and is never
   shown as faithful. The five provenance states of the design plus this one.
3. **Pictures.** `H` hides every panel, and the snapshot entry (`TheatreSnapshot`) hides the
   chrome by default so the record's frames keep their own label and stay comparable with
   every earlier picture. `scripts/theatre-snap.ps1 -Chrome` (and `EVOSIM_THEATRE_CHROME=1`)
   leaves the chrome on, which is how the interface itself is photographed for review.
4. **Fonts.** IBM Plex Mono (Regular, SemiBold) and IBM Plex Sans (Regular), fetched from
   the IBM Plex GitHub release, committed under `unity/Assets/Theatre/UI/Resources/Fonts/`
   with `OFL.txt` beside them, SDF atlases generated with the twelve non-ASCII glyphs and
   U+2009 baked as the spec lists. Add a line to `LICENSE-DOCS`'s coverage list naming the
   fonts and their licence (the caller commits it with the design folder).
5. **Width.** The owner's monitor is 3840 pixels wide, so the 2240 breakpoint is built, and
   a second step at 3400 applies the same wide tokens at 1.5 times, driven from C# as the
   spec says; uniform panel scaling stays off. The 3840 case is the one the caller checks
   in the pictures first, and if the wide chrome reads small there the report says so with
   the measured pixel heights of the type.
6. **The single-creature mode** is the same strip with a different census (spec §8), and
   the retired global-brain readout goes (spec §7): `Brain.NeuronCount`.
7. **The lineage index** is streamed with `JsonlWriter.ReadRows`, built lazily on the first
   selection, and shows no cause of death (spec §5). A run whose `lineage.jsonl` is missing
   shows the inspector's unavailable state rather than an error.

## Scope

Exactly the spec's §8: the world view in its states, the transport bar and timeline, the
provenance popover, the inspector in three states, the solo mode as a mode of the same
screen. Not the charts, the run picker, the lineage view, the gallery, the snapshot
caption. Every key binding of spec §3 kept; `P` added.

Run the spec's §6 checks first, in the order given, and record each answer in the report
before building on it; where one fails, take the fallback the spec names.

## Final message

Tables: files added and changed; the §6 checks and their answers; the data contract as
wired (every on-screen field, its source, its cadence), with the matter residual's status;
the per-frame update list (spec §9 item 6 is a contract: four fields and one width); what
the harness exercises (see the test spec); everything unverified, which is everything that
needs the Editor.
