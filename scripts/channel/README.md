# The channel's title films

These scripts make the channel's intro and outro in Blender. The intro is the eight seconds of
CΛMBRIΛN that play after each video's hook; the outro is the end screen. The creatures in both are
real bodies grown from our runs' genomes. They are drawn in the theatre's skin, the look of the
Unity viewer carried over into Blender. Their motion is choreographed, and the intro says so on
screen. Intro v1 was rendered on 2026-09-28 and filed with the other film assets, whose list in
`assets/README.md` gives its sources. Outro v1 followed the same evening.

## The rendered films may not be here

The rendered files are not in git. Each film's master and its two copies sit in `assets/cards/` on
the machine that rendered them, and the media in that folder are left out of git by design. So a
fresh checkout, another machine or a cleaned-out folder has none of them, and the history has no
copy to restore. Check that the file is there before a film uses it, and if it is missing, render
it again with the steps below (the outro's are at the end). The repository keeps all the render
needs: the scripts, the four bodies' parts in the bodies file and the readout's figures. The run
directories are not needed, and the type comes from the theatre's fonts, which are tracked. A new
render has the same shots and words. It may not match the old one bit for bit, since the denoiser
can differ with the card and its driver.

In DaVinci Resolve the project Cambrian holds both masters in its Channel Branding bin, linked to the
file where it lies. A missing file shows there as offline. Write the new render to the same path,
and relink the clip if it does not come back by itself. A new version gets a new name and goes in
the bin beside the old one.

## The files

| File | What it does |
|---|---|
| `intro.py` | builds the intro's scene, animates it and renders the plate, the picture without words |
| `outro.py` | the same for the outro, with every body placed by where it falls on screen |
| `theatre_scene.py` | the bodies, rigged at their joints, and the water, snow, lights and grade |
| `theatre_shape.py` | the theatre's mesh shapes and carving, in numpy |
| `theatre_material.py` | the theatre's body shading, as a Blender node material |
| `overlay.py` | draws the intro's title, tagline and corner readout over the plate |
| `outro_overlay.py` | draws the outro's words, the subscribe ring and the like chip |
| `check_anim.py` | brightness per frame, a flag on any sudden jump, and a contact sheet |
| `finish_draft.sh` | words, checks and one H.264, for a draft |
| `finish_4k.sh` | words in three parallel workers, checks, and the three final encodes |
| `bodies.json` | the bodies in the films, one entry each |
| `hud_data.json` | every figure the intro's readout prints |
| `endscreen_layout.json` | where YouTube's end-screen elements go |

## Making the intro

1. **Grow the bodies** with the exporter under `src/`. It develops every body a run's snapshots
   name, as the theatre's snapshot pictures do, and writes the parts of the larger ones:

   ```bash
   dotnet build src/Evosim.BodyExport -c Release     # Unity's bundled SDK, as core-test.ps1 finds it
   dotnet artifacts/Evosim.BodyExport/bin/Release/net8.0/Evosim.BodyExport.dll \
       --out scratch/bodies --min-parts 8 --label r50-s2 --run runs/r50-s2/<run dir>
   ```

   It is built against this tree's Core, so it reads only the runs this build reads. A chosen
   body's row from its `parts.jsonl` goes into the bodies file under a short key. Intro v1 has
   four. The hero, number 08 on the candidate sheet, is round 50 seed 2's body #23377 at 14,900 s. The others are r50-s1
   #16184 at 15,800 s, r50-s3 #7484 at 7,800 s and r51-s3 #48219 at 24,100 s.
2. **Read the figures** from the hero's run. The readout prints the hero's run, id, parts and
   joints, and its world's census at that second with the population's history before it. All of
   it comes from the run's `stats.jsonl`, so a new hero needs the figures file rebuilt from its own run.
3. **Render the plate**, a few frames first to check the layout and then all 240:

   ```bash
   blender -b --factory-startup --python scripts/channel/intro.py -- <plate dir> <percent> <samples> [frames]
   ```

   A draft at 25% and 16 samples takes about 4 seconds a frame. The final, at 100% and 128
   samples, takes about 12, so all 240 frames take about 50 minutes on the 4090.
4. **Draw the words and encode** with one of the two finishing scripts, each given the plate
   folder, an output folder and a name. The final's script writes a ProRes 422 HQ master for the
   edit, an H.264 at 4K and one at 1080.
5. **Check it before anyone sees it.** The checker's contact sheet is read against the last
   version the owner approved, and any flagged jump is looked at frame by frame.

## What will bite

- **Blender denoises on the processor** unless told otherwise. A launch with factory settings
  leaves the denoiser's GPU switch off. At 4K that took 28 of a frame's 42 seconds while the card
  sat idle. The scene script now turns it on and puts the compositor on the card too, and no pixel
  moved by more than 7 levels in 255. If the card idles through a frame again, set
  `EVOSIM_PROFILE=1` and the intro script prints the time of every stage.

- **Pillow draws text on whole pixels**, so a slow move drawn that way judders. The title and the
  tagline are drawn once and then moved by resampling, which is smooth.

- **The look is close to the theatre's** and not the same. The carve's noise runs in double precision
  where the shader's runs in single, so the dents differ in detail. Each body's two seeds are
  stand-ins for the theatre's own. There are no caustics. The lights are the theatre's three,
  aimed from the camera, with the key dimmed and the back light raised for the intro's dark field.

- **Blender 5.1 moved** parts of its API. The compositor is the scene's
  `compositing_node_group`, the mix node is `ShaderNodeMix`, and the Glare node's settings are
  input sockets.

## The outro

Outro v1 was rendered on 2026-09-28. It runs 20
seconds, the longest end screen YouTube allows. The layout file holds the zones YouTube's elements
will cover: a video tile each side, 613 by 343 pixels at 1080, and the subscribe circle between
them, 298 across. The bottom tenth is left for the player's controls. The creatures stay out of
every zone.

YouTube draws the elements over the video, so the video only marks where they go. A glowing ring
with a bell and the word SUBSCRIBE sits where the subscribe element goes, a little larger than it.
In YouTube Studio the element, the channel's avatar, is dragged into the ring, which then frames
it. Viewers who never see end screens still see the prompt; that is anyone on mobile web, or
anyone who hides them. YouTube has no like element, so a LIKE chip is drawn under the ring.

An end screen must carry at least one video or playlist. The first video's left zone therefore
takes a playlist tile. Later videos add a video tile on the right, and Studio's "Import from video"
copies the layout. The outro's overlay script outlines the zones with `--guides` and shows a
labelled mock-up of the avatar and a tile with `--mock=<png>`. The line under the ring stays
empty until the channel's handles are chosen, and filling it needs only the words redrawn.

The outro was made in three steps, and a new version is made the same way. First a preview in
Blender's fast renderer, all 600 frames at 540p in under two minutes, for the layout and the
motion; an environment setting in the script's header gives it. Then frames 1, 300 and 600 at
full quality in 1080, set beside the approved frames. Then the 4K, in two Blender processes on
alternating frames, given as `1:2` and `2:2`. One uses the card while the other does its share
on the processor. It ran at about 14 seconds a frame, two hours and
twenty minutes in all. The 4K finishing script takes the outro's words script as its fourth
argument.

The outro's overlay also needs the Material Icons font from the assets folder, which is left out
of git too. The font's row in the manifest says where to fetch it.
