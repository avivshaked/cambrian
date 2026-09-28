# Film assets

Media the films are composed from that no run produces: music, the opening and closing screens,
logos and overlay graphics, and the channel's own images. The story films draw on it when a round's story is composed in
DaVinci Resolve (`scripts/story-resolve.py`), and anyone cutting a film by hand can take from it.

**The media is not tracked.** Git holds this manifest and the folder layout. The files stay on this
machine, because most carry someone else's licence and many are larger than the pre-commit
hook's 5 MB ceiling. Anyone with the same sources can rebuild the folder from the table below.

Git has no copy of any file here, so a missing one has to be made or fetched again. A rendered
file, such as the intro in `cards/`, is made again from the scripts its row names
(`scripts/channel/README.md` for the intro).
Anything else is fetched again from its source. The DaVinci Resolve project links each file where
it lies, so a missing one shows there as offline until it is back at the same path.

| Folder | Holds |
|---|---|
| `music/` | music beds, one file per track |
| `cards/` | the opening and closing screens, as video or stills |
| `graphics/` | logos, lower thirds, overlay templates |
| `channel/` | the YouTube channel's images: title art, avatar, thumbnails |

## Manifest

Add a file here when it is added to the folder. A file with no source and licence recorded here is
not used in a film that leaves the machine.

| File | What | Source | Licence | Added |
|---|---|---|---|---|
| `music/Submerged Wonder - Part 1.mp3` | music, 4:08 | owner's download | not recorded | 2026-09-25 |
| `music/Submerged Wonder - Part 2.mp3` | music, 4:18 | owner's download | not recorded | 2026-09-25 |
| `music/The Silent Current's Secret.wav` | music, 3:25 | owner's download | not recorded | 2026-09-25 |
| `channel/cambrian-title-16x9.png` | the channel's title art, 1672 × 941: "CAMBRIAN, An evolving artificial ecosystem" over drawn creatures; an artist's impression, not a theatre render, and its HUD figures (`r37-s1`, 1 842 alive, 100 µm) are invented | owner, made with ChatGPT image generation 2026-09-13 | not recorded | 2026-09-28 |
| `channel/cambrian-creature-square.png` | the title art's segmented creature alone on black, 1254 × 1254; an artist's impression, not a theatre render | owner, made with ChatGPT image generation 2026-09-13 | not recorded | 2026-09-28 |
| `cards/cambrian-intro-v1-4k-prores422hq.mov` | the channel intro, v1: 8 s, 240 frames at 30 fps, CAMBRIAN (drawn with a capital lambda for each A) over the four bodies in the theatre's skin; the motion is choreographed, not the bodies' brains, and the picture says so; the last 4 s hold for a fade; ProRes 422 HQ, 3840 x 2160, the master for the edit (856 MB) | rendered in Blender 5.1 by `scripts/channel/` (`intro.py`, `overlay.py`, `finish_4k.sh`) from four evolved bodies: r50-s2 #23377 at 14,900 s (the hero), r50-s1 #16184 at 15,800 s, r50-s3 #7484 at 7,800 s and r51-s3 #48219 at 24,100 s; the corner figures are r50-s2's `stats.jsonl` at 14,900 s | own work; the type is IBM Plex (SIL OFL 1.1) | 2026-09-28 |
| `cards/cambrian-intro-v1-4k.mp4` | the same, H.264 at 3840 x 2160, for viewing (26 MB) | rendered in Blender 5.1 by `scripts/channel/` (`intro.py`, `overlay.py`, `finish_4k.sh`) from four evolved bodies: r50-s2 #23377 at 14,900 s (the hero), r50-s1 #16184 at 15,800 s, r50-s3 #7484 at 7,800 s and r51-s3 #48219 at 24,100 s; the corner figures are r50-s2's `stats.jsonl` at 14,900 s | own work; the type is IBM Plex (SIL OFL 1.1) | 2026-09-28 |
| `cards/cambrian-intro-v1-1080.mp4` | the same, H.264 at 1920 x 1080 (5 MB) | rendered in Blender 5.1 by `scripts/channel/` (`intro.py`, `overlay.py`, `finish_4k.sh`) from four evolved bodies: r50-s2 #23377 at 14,900 s (the hero), r50-s1 #16184 at 15,800 s, r50-s3 #7484 at 7,800 s and r51-s3 #48219 at 24,100 s; the corner figures are r50-s2's `stats.jsonl` at 14,900 s | own work; the type is IBM Plex (SIL OFL 1.1) | 2026-09-28 |
| `cards/cambrian-outro-v1-4k-prores422hq.mov` | the channel outro, v1: the end screen, 20 s, 600 frames at 30 fps; the bodies drift clear of the zones YouTube's elements cover, the title and tagline at the top, a glowing ring with a bell and SUBSCRIBE where the subscribe element goes, and a LIKE chip under it; the handles line is empty; ProRes 422 HQ, 3840 x 2160, the master for the edit (2.2 GB) | rendered in Blender 5.1 by `scripts/channel/` (`outro.py`, `outro_overlay.py`, `finish_4k.sh`) from the intro's four bodies and four more copies of them far off; the zones are `endscreen_layout.json` | own work; the type is IBM Plex (SIL OFL 1.1) and the icons Material Icons (Apache 2.0) | 2026-09-28 |
| `cards/cambrian-outro-v1-4k.mp4` | the same, H.264 at 3840 x 2160, for viewing (52 MB) | rendered in Blender 5.1 by `scripts/channel/` (`outro.py`, `outro_overlay.py`, `finish_4k.sh`) from the intro's four bodies and four more copies of them far off; the zones are `endscreen_layout.json` | own work; the type is IBM Plex (SIL OFL 1.1) and the icons Material Icons (Apache 2.0) | 2026-09-28 |
| `cards/cambrian-outro-v1-1080.mp4` | the same, H.264 at 1920 x 1080 (11 MB) | rendered in Blender 5.1 by `scripts/channel/` (`outro.py`, `outro_overlay.py`, `finish_4k.sh`) from the intro's four bodies and four more copies of them far off; the zones are `endscreen_layout.json` | own work; the type is IBM Plex (SIL OFL 1.1) and the icons Material Icons (Apache 2.0) | 2026-09-28 |
| `graphics/fonts/MaterialIcons-Regular.ttf` | Google's Material Icons font (thumb_up, notifications and the rest); its code points are in `MaterialIcons-Regular.codepoints` beside it | github.com/google/material-design-icons, `font/` on `master`, fetched 2026-09-28 | Apache 2.0 (`graphics/fonts/MaterialIcons-LICENSE.txt`) | 2026-09-28 |
