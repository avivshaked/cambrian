#!/usr/bin/env bash
# The intro from its 4K plate: the words in three parallel workers, the checks, a ProRes 422 HQ
# master for the edit, an H.264 copy at 4K and one at 1080.
#   scripts/channel/finish_4k.sh <plate dir> <out dir> <name>
set -e
here="$(cd "$(dirname "$0")" && pwd)"
plates="$1"; out="$2"; name="$3"; ov="$plates-ov"
mkdir -p "$ov" "$out"
for k in 0 1 2; do
  frames=$(python -c "print(','.join(str(f) for f in range(1, 241) if f % 3 == $k))")
  python "$here/overlay.py" "$plates" "$ov" "$frames" > "$ov/worker$k.log" 2>&1 &
done
wait
n=$(ls "$ov"/frame_*.png | wc -l); echo "frames with words: $n"
[ "$n" -eq 240 ] || { echo "missing frames"; exit 1; }
python "$here/check_anim.py" "$ov" "$out/$name-sheet-a.png" 1 120
python "$here/check_anim.py" "$ov" "$out/$name-sheet-b.png" 120 240
tags="-color_primaries bt709 -color_trc bt709 -colorspace bt709"
ffmpeg -y -loglevel error -framerate 30 -i "$ov/frame_%04d.png" -c:v prores_ks -profile:v 3 -vendor apl0 -pix_fmt yuv422p10le $tags "$out/$name-prores422hq.mov"
ffmpeg -y -loglevel error -framerate 30 -i "$ov/frame_%04d.png" -c:v libx264 -preset slow -crf 12 -pix_fmt yuv420p $tags -movflags +faststart "$out/$name.mp4"
ffmpeg -y -loglevel error -framerate 30 -i "$ov/frame_%04d.png" -vf scale=1920:1080:flags=lanczos -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p $tags -movflags +faststart "$out/$name-1080.mp4"
for f in "$out/$name-prores422hq.mov" "$out/$name.mp4" "$out/$name-1080.mp4"; do
  ffprobe -v error -show_entries stream=codec_name,width,height,nb_frames,r_frame_rate -show_entries format=duration,size -of compact "$f"
done
