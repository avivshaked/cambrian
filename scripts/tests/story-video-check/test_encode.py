"""film.py encode: a pass whose theatre ended at its own wall, before theatre-safari.ps1 reached its encode.

Round 67's pass 5 reached the theatre's wall with about 30 scenes filmed as PNG takes and none encoded. `encode`
does the script's encode block in Python, once the frames on disk are shown to match the theatre's own take lines.

  python -m pytest scripts/tests/story-video-check/test_encode.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_encode.py   (where pytest is not installed)
"""

import importlib.util
import json
import subprocess
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

ARM = "r-1"
FPS = 10
HEAD = "index\tslug\tstation\tsubject\tat\tplayed_to\toutcome\ttakes\tplan\tprovenance\n"


def make_takes(scene, counts, start=1):
    for i, n in enumerate(counts, start):
        d = scene / f"take-{i}"
        d.mkdir(parents=True)
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "color=c=gray:s=64x36:r=10",
                        "-frames:v", str(n), "-start_number", "0", str(d / "frame-%06d.png")], check=True)


def row(i, slug, station, outcome="played"):
    return f"{i}\t{slug}\t{station}\tx\t0\t1\t{outcome}\t1\tp\tCOUSIN\n"


def build(tmp_path, monkeypatch, scenes, rows=None, log=None, tsv=True):
    """scenes: slug -> (station, take counts). rows default to one played row a scene; log to one line a take."""
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    safari = tmp_path / "safari"
    safari.mkdir()
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(fps=FPS, theatre_cores=None)
    (out / "plan.json").write_text(json.dumps({"pass": 1, "settings": s, "scenes": []}), encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {ARM: str(safari)}}), encoding="utf-8")
    text = "" if log is None else log
    for i, (slug, (station, counts)) in enumerate(scenes.items(), 1):
        make_takes(safari / slug, counts)
        if log is None:
            text += "".join(f"[Theatre] safari: take {n} of {slug} ended: {c} frames; x\n" for n, c in enumerate(counts, 1))
    (out / f"theatre-{ARM}.log").write_text(text, encoding="utf-8")
    if tsv:
        body = rows if rows is not None else "".join(row(i, slug, st) for i, (slug, (st, _)) in enumerate(scenes.items(), 1))
        (safari / "scenes.tsv").write_text(HEAD + body, encoding="utf-8")
    monkeypatch.setattr(film, "film_sheet", lambda clip, s: None)
    return folder, out, safari


def frames_of(clip):
    r = subprocess.run(["ffprobe", "-v", "error", "-count_packets", "-select_streams", "v:0", "-show_entries",
                        "stream=nb_read_packets", "-of", "csv=p=0", str(clip)], capture_output=True, text=True)
    return int(r.stdout.strip())


def left(scene):
    return len(list(scene.glob("take-*/frame-*.png")))


def test_one_take_becomes_a_clip_and_its_frames_go(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [12])})
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: DONE"
    assert frames_of(safari / "story-01-floor.mp4") == 12
    assert left(safari / "story-01-floor") == 0
    assert any("story-01-floor.mp4" in x and "1 take(s)" in x for x in lines)
    assert (out / "encode.log").read_text(encoding="utf-8").splitlines()[0] == "ENCODE: DONE"


def test_two_takes_of_another_station_are_joined(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-02-birth": ("Birth", [8, 7])})
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: DONE"
    assert frames_of(safari / "story-02-birth.mp4") == 15
    assert left(safari / "story-02-birth") == 0


def test_a_time_scene_is_crossfaded(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-03-time": ("Time", [25, 25])})
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: DONE"
    n = frames_of(safari / "story-03-time.mp4")
    assert 50 - FPS - 2 <= n <= 50 - FPS + 2, n


def test_skipped_and_other_outcomes_are_listed_not_encoded(tmp_path, monkeypatch):
    rows = row(1, "story-01-floor", "Floor") + row(2, "story-02-gone", "Floor", "skipped: no window") + row(3, "story-03-x", "Floor", "failed")
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6])}, rows=rows)
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: DONE"
    assert any("story-02-gone" in x and "skipped: no window" in x for x in lines)
    assert any("story-03-x" in x and "failed" in x for x in lines)
    assert not (safari / "story-02-gone.mp4").exists()


def test_a_clip_that_exists_is_passed_over(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6])})
    assert film.cmd_encode(folder, 1)[0] == "ENCODE: DONE"
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: NOTHING TO ENCODE"
    assert (out / "encode.log").read_text(encoding="utf-8").splitlines()[0] == "ENCODE: NOTHING TO ENCODE"


def test_no_scenes_tsv_is_named_and_passed_over(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6])}, tsv=False)
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: NOTHING TO ENCODE"
    assert any("no scenes.tsv" in x for x in lines)
    assert left(safari / "story-01-floor") == 6


def test_a_failed_take_keeps_its_frames(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6]), "story-02-bad": ("Floor", [6])})
    for f in (safari / "story-02-bad" / "take-1").glob("frame-*.png"):
        f.write_bytes(b"not a png")
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: FAILED"
    assert any("story-02-bad" in x for x in lines)
    assert left(safari / "story-02-bad") == 6
    assert (safari / "story-01-floor.mp4").is_file()          # the other scene still encoded
    assert not (safari / "story-02-bad.mp4").exists()


def test_a_scene_with_no_row_is_refused(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6]), "story-02-lost": ("Floor", [6])},
                                rows=row(1, "story-01-floor", "Floor"))
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: FAILED"
    assert any("story-02-lost" in x and "no row in scenes.tsv" in x for x in lines)
    assert left(safari / "story-02-lost") == 6
    assert (safari / "story-01-floor.mp4").is_file()


def test_a_take_short_of_the_log_is_refused(tmp_path, monkeypatch):
    log = ("[Theatre] safari: take 1 of story-01-floor ended: 6 frames; x\n"
           "[Theatre] safari: take 2 of story-01-floor ended: 9 frames; x\n")
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6, 5])}, log=log)
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: FAILED"
    assert any("take 2: 5 frames on disk, the log says 9" in x for x in lines)
    assert left(safari / "story-01-floor") == 11
    assert not (safari / "story-01-floor.mp4").exists()


def test_a_take_folder_the_log_does_not_name_is_refused(tmp_path, monkeypatch):
    log = "[Theatre] safari: take 1 of story-01-floor ended: 6 frames; x\n"
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6, 4])}, log=log)
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: FAILED"
    assert any("2 take folders with frames, the log names 1" in x for x in lines)
    assert any("take 2: 4 frames on disk, the log does not name it" in x for x in lines)
    assert left(safari / "story-01-floor") == 10


def test_no_take_lines_in_the_log_is_refused(tmp_path, monkeypatch):
    log = "[Theatre] safari: take 1 of story-09-other ended: 6 frames; x\n"
    folder, out, safari = build(tmp_path, monkeypatch, {"story-01-floor": ("Floor", [6])}, log=log)
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: FAILED"
    assert any("no take lines" in x for x in lines)
    assert left(safari / "story-01-floor") == 6


def test_a_slug_that_is_not_a_story_scene_is_refused(tmp_path, monkeypatch):
    folder, out, safari = build(tmp_path, monkeypatch, {"floor-01": ("Floor", [6])})
    verdict, lines = film.cmd_encode(folder, 1)
    assert verdict == "ENCODE: FAILED"
    assert left(safari / "floor-01") == 6
