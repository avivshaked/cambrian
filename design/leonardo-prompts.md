# Leonardo prompt sheet

The prompts `scripts/leonardo-generate.py` sends, and nothing else does. They are for the
presentation layer outside the world (`logbook/specs/leonardo-survey.md`): backdrops for the
safari's arrival shot, title cards and thumbnails for the videos. None of them describes a
body, a run or a number from one; if a prompt ever should, the owner approves that prompt
first and the approval is noted under it. Outputs land under `scratch/leonardo/<slug>/` with
the request and the response beside each image.

A prompt is a level-three heading and its text. `model:`, `size:` and `count:` lines set the
model id, the picture's size (one of Leonardo's allowed sizes) and how many. The script refuses
to start a generation that would take the session past `--budget`, which the owner sets each
time.

## Backdrops

### Arrival: the dark water from outside the glass

size: 1280x720
count: 2

A wide cinematic underwater backdrop, deep still water fading from a dim teal at the top to
near black below, faint shafts of light from an unseen surface, no fish, no plants, no
creatures, no objects, no text, fine marine snow, photographic, quiet, muted, dark field
lighting.

### Title card ground

size: 1280x720
count: 2

An abstract dark teal gradient with soft caustic light patterns drifting across it, very dark
at the edges, nothing in the frame, no text, no objects, subtle film grain, cinematic.

## Thumbnails

### Thumbnail ground, wide

size: 1280x720
count: 1

A deep-sea dark blue backdrop with a single soft beam of light from the top left and a faint
haze, empty, no creatures, no text, high contrast, room on the right for a title.
