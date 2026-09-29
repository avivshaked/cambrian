# A round's story film composed in DaVinci Resolve

*Written 2026-09-25, the day it was built. The owner asked for the story film's parts to go
onto a Resolve timeline instead of being joined by ffmpeg, so that the film can be edited there.
That means the clips, the captions, the label and the music, and later the charts. This is the procedure and
what the Resolve API turned out to do. The film's steps up to the clips are
[`story-film.md`](story-film.md), and this replaces only its join.*

*From 2026-09-28 it also holds the project's notes on Resolve for any job. They are in the
section before the API's list, and CLAUDE.md points here.*

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
Settings, Export Subtitle, Burn into video), or written beside the film as a file for YouTube. The
YouTube preset has no Subtitle Settings at all. The owner rendered round 49's film with it on
2026-09-29 and got no subtitle track. Their screenshot of the YouTube 2160p preset in Resolve
Studio 21 ends at Upload directly to YouTube with no subtitle section. A user on Blackmagic's
forum reports the section showing under Custom Export (not yet seen here). So render with Custom
Export set to the same format, codec and size, with Export Subtitle ticked and As a separate file,
SRT. Or export the track alone from the Edit page (right-click the subtitle track, Export
Subtitle). A timeline that starts at 01:00:00:00 gives an SRT from the Edit page whose first cue
is an hour in. Round 49's export (2026-09-29) did that: every cue an hour late, and every caption
wrapped in `<b>` from the track's style, which YouTube would draw bold. Its text and times were
otherwise the build's own SRT to the millisecond. Shift it back an hour and strip the tags, or
upload the build's SRT, before it goes to YouTube.

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

## Working in the project outside the builder

*Added 2026-09-28, when the channel intro was filed. Whatever a session learns in Resolve goes
here or in the API's list below, with its date, so that the next session does not learn it
again.*

The project is Cambrian, in Resolve Studio 21.1. On 2026-09-28 its Media Pool held these bins,
with two of the owner's own items at the top level beside them.

| Bin | Holds |
|---|---|
| Music | the three music beds from `assets/music/` |
| First Safari | round 47's safari film and its cut |
| Stories | one bin per story build, named `<name> vK`, each with its footage, labels, graphics, words, narration and subtitles |
| Channel Branding | the channel intro's master and the two channel images, from `assets/` |

New work goes into a bin or a timeline of its own, added beside the owner's. Nothing of theirs
is changed, moved or deleted. A test is named `zz probe ...` and deleted afterwards, as the
skill says for builds.

There are two ways in. The first is plain Python through Resolve's own module, as the builder
does it, with external scripting set to Local (the setup section above). Any agent can use it.
The second is the Resolve MCP server, in a Claude session where it is configured. Its script
tool runs a short Python script in a sandbox, with the Resolve object and the current project
already set. The script has no file access, and it hands back whatever it assigns to `result`.
A script gets 10 seconds unless it asks for more, and at most 60. Call the server's what's-new
tool first: Resolve 21 came out after the model's training, and its changelog lists the calls it
added.
The server's search tool reads the API's type stubs, which is quicker than the whole stub.

A refusal that says the auto mode classifier gave no verdict is Claude Code's permission check
failing. The call never reached Resolve. On 2026-09-28 it looked like Resolve was down, and the
check had recovered by the owner's next message. Ten such refusals in a row end the agent's turn, so stop
after two or three and come back to it.

An import links the file where it lies and copies nothing. A clip's `File Path` property says
where. A file that is moved or deleted shows as offline in the bin until it is back at the same
path, or until the clip is relinked. The Media Pool's context menu does that by hand, and the
API's `RelinkClips` takes the clips and a folder. A still comes into a bin one frame long, and a
timeline places it at most five seconds long (item 5 below).

This made the Channel Branding bin, and it can be run twice without making two:

```python
mp = project.GetMediaPool()
root = mp.GetRootFolder()
before = mp.GetCurrentFolder()        # ImportMedia imports into the current folder
found = [f for f in root.GetSubFolderList() if f.GetName() == "Channel Branding"]
bin_ = found[0] if found else mp.AddSubFolder(root, "Channel Branding")
have = {c.GetName() for c in bin_.GetClipList() or []}
todo = [p for p in paths if p.split("/")[-1] not in have]   # absolute paths, forward slashes
mp.SetCurrentFolder(bin_)
items = mp.ImportMedia(todo) if todo else []
mp.SetCurrentFolder(before)           # leave the pool as the owner had it
resolve.GetProjectManager().SaveProject()
for c in bin_.GetClipList():          # the check that the right files went in
    print([c.GetClipProperty(k) for k in ("File Path", "Resolution", "Duration", "FPS", "Video Codec")])
```

Two references are on this machine. Resolve installs its API's own reference with it, under
`C:\ProgramData\Blackmagic Design\DaVinci Resolve\Support\Developer\Scripting\`. The folder
holds a README, the type stubs, examples and a changelog.

The reference manual is Blackmagic's PDF, cited above by chapter and page. It is copyrighted and
never committed, and it can be downloaded again from Blackmagic's support site. Its copy on this
machine is `scratch/owner/DaVinci Resolve.pdf`, put there on 2026-09-28 from a worktree. A text
extraction sits in `scratch/resolve-manual/`, with a table of pages to chapters and a script that
prints a range of pages. Git ignores both, and the commit hook refuses any PDF.

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
