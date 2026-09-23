#!/usr/bin/env python3
"""A contact sheet of a film, for an agent that reads pictures and not video.

    python scripts/film-sheet.py scratch/films/r46-s1/r46-s1-t5000-orbit.mp4 [--tiles 12] [--columns 4] [--width 480]

Takes `tiles` frames spread evenly over the clip (the first and the last among them), scales
each to `width` pixels across, and tiles them in reading order into one PNG beside the clip,
named `<clip>-sheet.png`. It prints the sheet's path and the second of each tile, so a tile
can be named in a reading. ffmpeg does the work (on PATH); nothing else is written.
"""
import argparse
import json
import os
import subprocess
import sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("clip")
    ap.add_argument("--tiles", type=int, default=12)
    ap.add_argument("--columns", type=int, default=4)
    ap.add_argument("--width", type=int, default=480)
    a = ap.parse_args()
    if not os.path.isfile(a.clip):
        sys.exit("no such clip: " + a.clip)
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "v:0", "-count_frames",
         "-show_entries", "stream=nb_read_frames,r_frame_rate", "-of", "json", a.clip],
        capture_output=True, text=True, check=True)
    stream = json.loads(probe.stdout)["streams"][0]
    frames = int(stream["nb_read_frames"])
    num, den = stream["r_frame_rate"].split("/")
    fps = float(num) / float(den)
    tiles = max(2, min(a.tiles, frames))
    picks = [round(i * (frames - 1) / (tiles - 1)) for i in range(tiles)]
    rows = -(-tiles // a.columns)
    select = "+".join("eq(n\\,%d)" % p for p in picks)
    out = os.path.splitext(a.clip)[0] + "-sheet.png"
    vf = "select='%s',scale=%d:-1,tile=%dx%d:padding=4:margin=4" % (select, a.width, a.columns, rows)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", a.clip, "-vf", vf, "-frames:v", "1", out], check=True)
    print(out)
    print("tiles (reading order), seconds into the clip: " +
          ", ".join("%d: %.1f s" % (i + 1, p / fps) for i, p in enumerate(picks)))


if __name__ == "__main__":
    main()
