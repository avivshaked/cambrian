# Read `../SPEC.md` first

This folder is the design deliverable for the theatre's interface, exported from Claude Design
after two passes. **`../SPEC.md` is the build spec** — it says what this is, where the code
goes, where every number on screen comes from, and what to verify before relying on it.

The original export carried a generic handoff README telling coding agents to read the HTML in
full and recreate it pixel-perfectly in "React, Vue, native, whatever fits". That advice is
wrong for this project in three ways, so it has been replaced by this note:

1. **The target is Unity 6 UI Toolkit**, not a web framework. Nothing here ships as HTML.
2. **`theatre.uss` is the authority, not the HTML.** It is real USS, written to this
   project's constraints, and it won every disagreement between the two documents. The
   `.dc.html` is a picture; its inline styles are how a browser draws it and are not the
   design.
3. **Do not aim for pixel-perfect against the mockup.** Where the mockup and the stylesheet
   differ, the stylesheet is right. The one known case: grouped numbers render in the mockup
   with an ordinary space where the design specifies a thin space (U+2009), so the picture
   slightly overstates the separator.

## Contents

| File | What it is |
|---|---|
| `theatre.uss` | The design system. Palette, type scale, spacing, every component, plus eight documented sections covering settled values, spacing, exceptions, resolution, declaration order, the glyph set and number formatting. Read it top to bottom. |
| `Theatre UI.dc.html` | Nine artboards showing the design in every state. Its `class` attributes name rules in `theatre.uss`. |
| `assets/` | Backdrops for the artboards. The originals, with provenance, are in `../reference-frames/`. |
| `support.js` | Claude Design's own canvas runtime — generated, not this project's work, and gitignored. Present only so the `.dc.html` opens in a browser. |
