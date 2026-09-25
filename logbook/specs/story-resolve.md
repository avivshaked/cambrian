# A round's story film composed in DaVinci Resolve

*Written 2026-09-25, the day it was built. The owner asked for the story film's parts to go
onto a Resolve timeline instead of being joined by ffmpeg, so that the film can be edited there.
That means the clips, the captions, the label and the music, and later the charts. This is the procedure and
what the Resolve API turned out to do. The film's steps up to the clips are
[`story-film.md`](story-film.md), and this replaces only its join.*

The assembler already decides where everything goes. `story-assemble.py --ass-only` writes the
scenes in order with their clips and starts, and every caption and every tick of the provenance
label with its time, and joins nothing. The Resolve builder, `scripts/story-resolve.py`, has the
assembler write those two files and lays the same film out as a timeline. So the ffmpeg film and
the Resolve film agree about every frame, because one program decided both.

It was tested on 2026-09-25 against two scenes of round 48's second story. They were scenes 1
and 3 of `story-r48-v2/story.json`, filmed into the `windows-v3` folder on the round 49 branch.
The build placed the title, both scenes, both labels, ten captions and the music where the plan
put them, with no errors. A one-second render showed the caption at its frame and the label in
the wrong place, so the label is now drawn on a full frame. The second build's render showed it
in the top left corner, reading "FAITHFUL · r48-s1 · 2,509 s" at the film's 14.5th second,
which is the world's second there. That build's title ran off both edges at Text+'s default
size, and a title longer than 34 characters is now made smaller in proportion. The third
render showed the round's 53-character title inside the frame.

## What it builds

| Track | Holds | Made from |
|---|---|---|
| V1 Picture | the title card, the scenes in story order, the end card | a Text+ title; the clips; a file from `assets/cards/` |
| V2 Label | the provenance label, one clip per scene, linked to its scene | ffmpeg and libass, IBM Plex Mono from the repository's fonts |
| V3 Charts | nothing yet | kept for the chart overlays |
| ST1 Captions | every caption as a Resolve subtitle | an SRT the builder writes |
| A1 Music | the configured tracks in order, cut and faded at the film's end | files from `assets/music/` |
| A2 Narration | the story's narration, a clip a paragraph, where the story was timed from it | `narration/` in the story's folder |

Every scene also gets a marker, red where a chapter opens, whose note carries the scene's
captions. The build makes a bin and a timeline of one name, `<name> vK`, under the configured
bin, with K the first number free. It never opens or changes an existing timeline. Inside the
bin there is one bin per run for that run's clips, and bins for the labels, the captions and the
assets.

Nothing is burned in. At render, the subtitles are burned from the Deliver page (Video, Subtitle
Settings, Export Subtitle, Burn into video), or written beside the film as a file for YouTube.

## Setting up a machine

Resolve must be running, with Preferences, System, General, External scripting using set to
Local. The builder then connects from plain Python through Resolve's own module. The Resolve
MCP server is not needed. On this machine that connection was tested with Python 3.10 against
Resolve Studio 21.1.

The settings belong to one machine and live in `.env` at the repository's root, which git
ignores. The keys, their defaults and what each does are in `scripts/story-resolve.env.example`.
On this machine the file names the project Cambrian and the bin Stories. It asks for a 3840 by
2160 timeline at 30 frames a second, with the two parts of "Submerged Wonder" as the music.

The title card and the subtitles use fonts Resolve can see, which means fonts installed in
Windows. The owner ruled on 2026-09-25 that Resolve's own fonts are fine, so the title keeps
Text+'s default and IBM Plex is not installed. A machine that wants another font names it in
the settings. The label is drawn by ffmpeg in IBM Plex Mono, read from the repository's
`unity/Assets/Theatre/UI/Fonts`, and needs nothing installed.

The subtitle style comes from a template. The API cannot set a subtitle track's font, size,
stroke or position. The manual's two routes to a style both go through the interface. One is the
Track panel of the Inspector and the other a subtitle style preset saved from it (Chapter 59,
p. 1237 and 1239). So make one empty timeline in the project, add a subtitle track, style it in the Track
panel, and name the timeline in `EVOSIM_RESOLVE_TEMPLATE`. Each build copies it, and a copy
kept the track and its name in the test. That the copy keeps the style is the manual's promise
and has not been seen yet.

```powershell
python scripts/story-resolve.py --check
```

The check connects, prints every setting and where it came from, and says whether the project,
the template, the fonts and the assets are there, changing nothing.

## Running it

```powershell
python scripts/story-resolve.py scratch/story/r49/story.json scratch/story/r49/resolve `
    <folder of seed 1's clips> <folder of seed 2's clips> <folder of seed 3's clips>
```

The first half is the plan. It runs the assembler, writes the captions as an SRT, renders a
label clip for each scene, and writes the plan the build reads. That took about a second for two
scenes, and the labels add about twelve seconds of rendering for each minute of film. The second
half is the build. It reads the plan, builds in Resolve in a few seconds, and writes a report of
every note and error beside it.

| In the out folder | What it is |
|---|---|
| `story.ass`, `story.scenes.tsv` | the assembler's plan: the scenes with their clips and starts, the captions and the label ticks |
| `captions-<hash>.srt` | the captions, timed from the end of the title; the hash is of the cues, so a changed caption is a new file Resolve imports afresh |
| `labels/label-NN.mov` | each scene's label, the frame's size, transparent but for the label |
| `plan.json` | everything the build places, frame by frame |
| `build.json` | what the build did, with its notes and errors |

| Option | Does |
|---|---|
| `--plan-only` | stops after the plan |
| `--build <plan.json>` | builds from a plan already made |
| `--name` | the name before the version; the default is the story's title up to its colon |
| `--switch-project` | opens the configured project when Resolve has another open |
| `--captions story` or `filmed` | the story's captions as they stand (the default), or as the director placed them at filming |
| `--filmed <story.filmed.json>` | the story as filmed, which tells the director's own lines apart; by default the file of that name beside the story |

A caption edited after filming reaches the film with a plan and a build: the new `vK` reuses every
clip already in the bin, and only the SRT is new. Edits made by hand in an earlier `vK` stay in it.
To carry a caption change into a timeline edited by hand, import the new SRT there by hand (the
manual's chapter 59 on subtitles).

## What the Resolve API does that its documentation does not say

These were found on 2026-09-25 by building throwaway timelines in the Cambrian project, reading
back what Resolve held, and rendering one second at a time. Resolve Studio 21.1.0.17 on Windows.

1. A subtitle file appended to a timeline lands at the timeline's current end, shifted by the
   file's own times counted from zero. The position and the track passed with it are
   ignored. A file whose first caption was at 01:00:02 went to the end plus an hour. The
   manual's placement by timecode is a Media Pool command that the API does not offer. The
   builder therefore appends the captions right after the title, while the title is all the
   timeline holds, with their times counted from the title's end.
2. A title inserted from a script is an insert edit. It goes in at the playhead, or between the
   timeline's In and Out marks when they are set, and it pushes every track after it along.
   One inserted over a clip split the clip and the clip above it. Setting the render range sets
   those marks, which the builder uses: it sets the range to the title's length and inserts the
   title on the empty timeline. It then puts the Deliver page's settings back from a preset it saved first.
3. A Fusion composition added to a clip by script does not reach the render. The nodes read
   back as built: a text over the picture on V1, and a red background on V2. Neither appeared
   in the render, even after the Fusion page had opened the clip. Text set on a
   Fusion title's own text tool does render. So the label is a clip of its own, drawn by
   ffmpeg, and not a Fusion text.
4. A clip's Fusion composition works at the clip's own resolution, as the manual says (Chapter
   11, p. 289, and Chapter 64, p. 1332). A text drawn over a 64 by 36 carrier was 64 by 36
   pixels, a speck once scaled to the frame.
5. A still is placed at most five seconds long, the standard still duration, whatever end frame
   is asked for.
6. Pan and Tilt are not the timeline's pixels. A strip 1920 by 216 set to Pan −960 and Tilt 972
   on a 3840 by 2160 timeline showed a third of the way across and halfway down. Those pixels
   would have put it in the top left corner. The manual does not give the units. The label clip
   is therefore the whole frame, transparent but for the label, and needs no transform.
7. Exporting the current frame as a still gives the clip alone, without the tracks above it.
   A render of one second is the check of what a timeline shows.
8. ffmpeg's subtitles filter draws no alpha on a transparent canvas unless its `alpha` option is
   on. With it, the label's text carries the 70% opacity its style asks for.
9. A copy of a timeline keeps its subtitle track and the track's name, and a subtitle file
   appended to the copy goes onto that track.

## Still open

- The template's style has not been seen carried into a copy.
- The charts are not built. V3 is kept for them, and a chart drawn as a clip with alpha would go
  there the way the label does.
- No whole film has been built. The test was two scenes and 62 seconds.
