# Film assets

Media the films are composed from that no run produces: music, the opening and closing screens,
logos and overlay graphics. The story films draw on it when a round's story is composed in
DaVinci Resolve (`scripts/story-resolve.py`), and anyone cutting a film by hand can take from it.

**The media is not tracked.** Git holds this manifest and the folder layout. The files stay on this
machine, because most carry someone else's licence and many are larger than the pre-commit
hook's 5 MB ceiling. Anyone with the same sources can rebuild the folder from the table below.

| Folder | Holds |
|---|---|
| `music/` | music beds, one file per track |
| `cards/` | the opening and closing screens, as video or stills |
| `graphics/` | logos, lower thirds, overlay templates |

## Manifest

Add a file here when it is added to the folder. A file with no source and licence recorded here is
not used in a film that leaves the machine.

| File | What | Source | Licence | Added |
|---|---|---|---|---|
| `music/Submerged Wonder - Part 1.mp3` | music, 4:08 | owner's download | not recorded | 2026-09-25 |
| `music/Submerged Wonder - Part 2.mp3` | music, 4:18 | owner's download | not recorded | 2026-09-25 |
| `music/The Silent Current's Secret.wav` | music, 3:25 | owner's download | not recorded | 2026-09-25 |
