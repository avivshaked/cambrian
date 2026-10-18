"""The round 57 film's look test, read: motion per clip, and one sheet of both fogs beside the liked clip.

  python scratch/look-check/look_check.py

For each look test folder that has a clips.json: every clip's frames go to scratch/look-check/<test>/<shot>/,
scripts/film-motion.py runs on them, and its verdict lines are printed. Then one sheet: a row a shot, its
middle frame under look1 (fog 0.014) and look2 (the look's own fog), and a last row of two frames of
scratch/owner/underwater-look-2/clip-new-look.mp4, the clip the owner liked. Writes
scratch/owner/round57-look-test/sheet.png.
"""
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
TESTS = {"look1": "fog 0.014 (round 49's)", "look2": "the look's own fog"}
OUT = ROOT / "scratch" / "owner" / "round57-look-test"
LIKED = ROOT / "scratch" / "owner" / "underwater-look-2" / "clip-new-look.mp4"
W, H = 640, 360


def frames(clip, out):
    out.mkdir(parents=True, exist_ok=True)
    if not any(out.glob("frame-*.png")):
        subprocess.run(["ffmpeg", "-loglevel", "error", "-i", str(clip), str(out / "frame-%04d.png")], check=True)
    return sorted(out.glob("frame-*.png"))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rows, shots = {}, []
    for test, label in TESTS.items():
        folder = ROOT / "scratch" / "story-video" / f"rounds-50-57-202610081356-{test}"
        data = json.loads((folder / "clips.json").read_text(encoding="utf-8")) if (folder / "clips.json").is_file() else None
        if data is None:
            print(f"{test}: no clips.json yet")
            continue
        for c in data["clips"]:
            fdir = ROOT / "scratch" / "look-check" / test / c["shot"]
            fr = frames(folder / c["file"], fdir)
            r = subprocess.run([sys.executable, str(ROOT / "scripts" / "film-motion.py"), str(fdir)],
                               capture_output=True, text=True)
            verdict = [l for l in r.stdout.splitlines() if "above" in l or "median" in l][:4]
            print(f"{test} {c['shot']}: luma {c['luma']}, tone {c['tone']}, edges {c['edges']}, jump {c['jump_ratio']}, "
                  f"look {c.get('look')}, fog {c.get('fog')}")
            for l in verdict:
                print("   ", l)
            rows.setdefault(c["shot"], {})[test] = fr[len(fr) // 2]
            if c["shot"] not in shots:
                shots.append(c["shot"])
    liked = ROOT / "scratch" / "look-check" / "liked"
    lf = frames(LIKED, liked) if LIKED.is_file() else []
    sheet = Image.new("RGB", (2 * W + 180, (len(shots) + 1) * (H + 8) + 40), (16, 16, 16))
    d = ImageDraw.Draw(sheet)
    for j, (test, label) in enumerate(TESTS.items()):
        d.text((180 + j * W + 8, 12), f"{test}: {label}", fill=(230, 230, 230))
    for i, shot in enumerate(shots + ["liked clip"]):
        y = 40 + i * (H + 8)
        d.text((8, y + H // 2), shot, fill=(230, 230, 230))
        pics = ([rows[shot].get(t) for t in TESTS] if shot in rows else
                [lf[len(lf) // 3] if lf else None, lf[2 * len(lf) // 3] if lf else None])
        for j, p in enumerate(pics):
            if p is not None:
                sheet.paste(Image.open(p).convert("RGB").resize((W, H)), (180 + j * W, y))
    sheet.save(OUT / "sheet.png")
    print(f"sheet: {OUT / 'sheet.png'}")


if __name__ == "__main__":
    main()
