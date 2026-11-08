"""Jumps are measured within takes, and COUSIN is not a fault.

Round 67's shot 2.4b is a birth filmed as two takes, the parent and then the newborn. Its largest frame-to-frame
change, 51.9 times the median, was the planned cut between them, and every provenance on the farm route is COUSIN.

  python -m pytest scripts/tests/story-video-check/test_take_jumps.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_take_jumps.py   (where pytest is not installed)
"""

import importlib.util
import json
import subprocess
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

SLUG = "story-09-r67ev-24b-6202-birth-pinnula-paveplefa"
NEIGHBOUR = "story-19-r67ev-24b-6202-birth-pinnula-paveplefa"


def line(n, slug, frames):
    return f"[Theatre] safari: take {n} of {slug} ended: {frames} frames; the glass bound 0, ...\n"


# ---- the take lines -----------------------------------------------------------------------------------------

def test_the_right_slug_is_read_and_not_a_neighbours():
    log = line(1, SLUG, 286) + line(2, SLUG, 220) + line(1, NEIGHBOUR, 100) + line(2, NEIGHBOUR, 50) + line(3, NEIGHBOUR, 9)
    assert film.take_frames(log, SLUG) == [286, 220]
    assert film.take_frames(log, NEIGHBOUR) == [100, 50, 9]
    assert film.take_frames(log, "story-01-nothing") is None


def test_a_one_take_scene_has_one_take():
    assert film.take_frames(line(1, SLUG, 501), SLUG) == [501]


def test_the_last_set_is_taken_when_a_slug_appears_twice():
    log = line(1, SLUG, 10) + line(2, SLUG, 20) + "noise\n" + line(1, SLUG, 286) + line(2, SLUG, 220)
    assert film.take_frames(log, SLUG) == [286, 220]


# ---- which change is the cut --------------------------------------------------------------------------------

def test_the_left_out_index_is_the_change_into_the_next_takes_first_frame(tmp_path):
    # Ten black frames, then ten white: the one large change is into frame 10. Measured by ffmpeg itself, through
    # the same yavg call and [1:] that collect makes.
    clip = tmp_path / "cut.mp4"
    inputs = []
    for colour in ("black", "white"):
        inputs += ["-f", "lavfi", "-i", f"color={colour}:s=64x64:r=10:d=1,format=yuv420p"]
    subprocess.run(["ffmpeg", "-v", "error", "-y", *inputs, "-filter_complex", "[0][1]concat=n=2:v=1[o]",
                    "-map", "[o]", str(clip)], check=True)
    jumps = film.yavg(clip, tmp_path, "jump.txt", "tblend=all_mode=difference,")[1:]
    big = max(range(len(jumps)), key=lambda j: jumps[j])
    assert jumps[big] > 100
    left = film.jumps_within_takes(jumps, [10, 10])
    assert len(left) == len(jumps) - 1 and max(left) < 1     # an off-by-one would leave the cut in, or drop a neighbour
    assert [j for j in range(len(jumps)) if jumps[j] > 100] == [8]   # frame 10 is index 10 - 2


def test_no_takes_or_one_take_leaves_every_change():
    assert film.jumps_within_takes([1, 2, 3], None) == [1, 2, 3]
    assert film.jumps_within_takes([1, 2, 3], [4]) == [1, 2, 3]


# ---- collect ------------------------------------------------------------------------------------------------

def skill_settings():
    return json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))


def collect_film(tmp_path, monkeypatch, jumps, log, frames=11, provenance="FAITHFUL"):
    shot = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 100.0, "run_to_s": 104.0,
            "kind": "run"}
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    settings = skill_settings()
    (folder / film.SETTINGS).write_text(json.dumps(settings), encoding="utf-8")
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    key = film.shot_key(shot, settings, {})
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None,
            "scenes": [{"n": 9, "shot": "1.2a", "key": key, "arm": "r67-6202", "camera": "Floor", "clip_start_s": 1.0,
                        "seconds": (frames - 1) / settings["fps"], "action_in_s": 0.1, "action_out_s": 0.2,
                        "handle_before_s": 0.1, "handle_after_s": 0.1, "fog": None, "focus": None, "side": None}]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / (SLUG + ".mp4")).write_bytes(b"clip")
    (safari / "scenes.tsv").write_text(f"slug\toutcome\tprovenance\n{SLUG}\tplayed\t{provenance}\n", encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {"r67-6202": str(safari)}}), encoding="utf-8")
    (out / "theatre-r67-6202.log").write_text(log, encoding="utf-8")
    monkeypatch.setattr(film, "probe", lambda p: {"frames": frames, "width": 1920, "height": 1080})
    monkeypatch.setattr(film, "reference", lambda f, s: {"luma": 1, "tone": 1, "tone_p10": 0, "edges": 1, "edges_p10": 0})
    monkeypatch.setattr(film, "sheet", lambda *a, **k: None)

    def fake_yavg(path, cwd, name, before=""):
        return [0.0] + jumps if "tblend" in before else [50.0] * frames

    monkeypatch.setattr(film, "yavg", fake_yavg)
    monkeypatch.setattr(film, "look", lambda *a, **k: ([1.0], [1.0]))
    monkeypatch.setattr(film, "colour", lambda *a, **k: [1.0, 1.0, 1.0])
    verdict, lines = film.cmd_collect(folder, 1)
    clip = json.loads((folder / "clips.json").read_text(encoding="utf-8"))["clips"][0]
    return clip, lines


def steady(frames=11):
    """The changes after collect's [1:]: entry j is the change into frame j + 2, so an 11-frame clip has 9."""
    return [1.0] * (frames - 2)


TWO = line(1, SLUG, 6) + line(2, SLUG, 5)       # an 11-frame clip: take 1 is frames 0-5, take 2 is frames 6-10


def test_a_planned_cut_is_not_a_jump(tmp_path, monkeypatch):
    j = steady()
    j[4] = 100.0                                   # the change into frame 6, the first frame of take 2
    clip, lines = collect_film(tmp_path, monkeypatch, j, TWO)
    assert clip["takes"] == [6, 5]
    assert clip["jump_ratio"] < 6 and "jump" not in clip["flags"]
    assert not any("take boundaries were not read" in x for x in lines)


def test_a_jump_inside_a_take_is_still_flagged(tmp_path, monkeypatch):
    j = steady()
    j[7] = 100.0                                   # the change into frame 9, inside take 2
    clip, lines = collect_film(tmp_path, monkeypatch, j, TWO)
    assert clip["takes"] == [6, 5] and clip["jump_ratio"] >= 6 and "jump" in clip["flags"]


def test_a_cut_plus_a_jump_inside_a_take_is_flagged_for_the_jump_alone(tmp_path, monkeypatch):
    j = steady()
    j[4], j[7] = 100.0, 20.0
    clip, lines = collect_film(tmp_path, monkeypatch, j, TWO)
    assert clip["jump_ratio"] == 20.0


def test_takes_that_do_not_sum_to_the_frames_fall_back_to_the_whole_clip(tmp_path, monkeypatch):
    j = steady()
    j[4] = 100.0
    clip, lines = collect_film(tmp_path, monkeypatch, j, line(1, SLUG, 6) + line(2, SLUG, 9))
    assert clip["takes"] is None and "jump" in clip["flags"]
    assert any("take boundaries were not read" in x and "do not sum" in x for x in lines)


def test_no_take_line_falls_back_with_the_note(tmp_path, monkeypatch):
    clip, lines = collect_film(tmp_path, monkeypatch, steady(), "[Theatre] nothing")
    assert clip["takes"] is None
    assert any("take boundaries were not read" in x and "no take line" in x for x in lines)


# ---- provenance ---------------------------------------------------------------------------------------------

def test_cousin_and_faithful_raise_no_flag_and_the_rest_do(tmp_path, monkeypatch):
    for p, flagged in (("COUSIN", False), ("FAITHFUL", False), ("", True), ("WILD", True)):
        clip, lines = collect_film(tmp_path / (p or "none"), monkeypatch, steady(), TWO, provenance=p)
        assert ("provenance" in clip["flags"]) is flagged, p
