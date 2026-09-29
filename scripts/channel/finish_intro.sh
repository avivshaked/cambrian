#!/usr/bin/env bash
# Words over the plate, the checks, and the encode: finish_intro.sh <plate dir> <name>
set -e
cd "$(dirname "$0")"
plates="$1"; name="$2"
python overlay.py "$plates" "$plates-ov"
python check_anim.py "$plates-ov" "out/$name-sheet.png"
ffmpeg -y -loglevel error -framerate 30 -i "$plates-ov/frame_%04d.png" -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p -movflags +faststart "out/$name.mp4"
ffprobe -v error -show_entries stream=width,height,nb_frames,r_frame_rate -show_entries format=duration -of compact "out/$name.mp4"
