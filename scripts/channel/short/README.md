# Shorts from a locked film

Two tools cut a film's Short outside Resolve, with ffmpeg, from the film's 4K clips. Round 67's Shorts were made
with them (`round-67.json`).

- `build.py` makes the Short in either shape. Given `916` it makes the 9:16 for YouTube Shorts, 1080 x 1920 from a
  third of the frame's width. Given `45` it makes the 4:5 for LinkedIn, 1080 x 1350 from half the width. Each
  segment's crop holds or pans between two centres given in the job file. The end card is the last segment, with
  "Watch the full film" on it.
- `captions.py <job.json> 45` burns word-by-word captions into the 4:5, since most LinkedIn viewers watch muted.
  The words come from the narration's own alignment, in groups of up to three. The word being spoken shows in yellow.

The job file names the segment table, the crop centres, each take's alignment file and the output folder under
`scratch/`. A row of the segment table names the clip, its first frame and its length at 30 fps. It also gives the
CDL slope the cut gives the shot, the narration take, and the second where the segment's voice starts in that take.

Three rules from round 67 hold for the next Short. Use only the film's own takes, cut at sentence ends, so nothing new
needs a fact check. Ask the film's question and keep its answer for the film. Lay no music and set no levels, since
the music and the mix are the owner's.

Needs ffmpeg with libass on the path, Pillow, and the Segoe UI Black font, or another font named as the third argument.
