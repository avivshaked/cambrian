#!/usr/bin/env bash
# A draft from an intro plate: the words, the checks and one H.264.
#   scripts/channel/finish_draft.sh <plate dir> <out dir> <name>
set -e
here="$(cd "$(dirname "$0")" && pwd)"
plates="$1"; out="$2"; name="$3"; ov="$plates-ov"
mkdir -p "$out"
python "$here/overlay.py" "$plates" "$ov"
python "$here/check_anim.py" "$ov" "$out/$name-sheet.png" 1 "$(ls "$ov"/frame_*.png | wc -l)"
ffmpeg -y -loglevel error -framerate 30 -i "$ov/frame_%04d.png" -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p -movflags +faststart "$out/$name.mp4"
ffprobe -v error -show_entries stream=width,height,nb_frames,r_frame_rate -show_entries format=duration -of compact "$out/$name.mp4"
