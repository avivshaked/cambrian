"""A take's tone and edges are measured at its own width, capped at 1920, against the reference at that width.

Round 67's 960x540 test takes were scaled up to 1920 before their edges were read, so they read soft beside a 4K
reference scaled down to 1920 (shot 9.1a: edges 9.58 against the reference's tenth percentile, 11.1).

  python -m pytest scripts/tests/story-video-check/test_look_width.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_look_width.py   (where pytest is not installed)
"""

import importlib.util
import json
import subprocess
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)


def make(path, w, h, seconds=2, frames=None):
    args = ["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", f"testsrc2=s={w}x{h}:r=30"]
    args += ["-frames:v", str(frames)] if frames else ["-t", str(seconds)]
    args += ["-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast", str(path)]
    r = subprocess.run(args, capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    return path


def skill_settings(**over):
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(over)
    return s


def counted(monkeypatch):
    """film.look, counting its calls and the widths asked for."""
    real, calls = film.look, []

    def counting(path, cwd, name, pre="", width=1920):
        calls.append(width)
        return real(path, cwd, name, pre, width=width)

    monkeypatch.setattr(film, "look", counting)
    return calls


# ---- look ---------------------------------------------------------------------------------------------------

def test_a_960_clip_has_stronger_edges_at_its_own_width_than_scaled_up(tmp_path):
    clip = make(tmp_path / "c.mp4", 960, 540)
    _, own = film.look(clip, tmp_path, "a.txt", "", width=960)
    _, up = film.look(clip, tmp_path, "b.txt")
    assert min(own) > max(up)


# ---- reference ----------------------------------------------------------------------------------------------

def reference_film(tmp_path, w=1920, h=1080):
    folder = tmp_path / "film"
    (folder / "film").mkdir(parents=True)
    ref = make(tmp_path / "ref.mp4", w, h, seconds=3)
    return folder, ref, {"reference": str(ref)}


def test_the_reference_at_1920_is_the_cache_and_writes_no_at(tmp_path, monkeypatch):
    folder, ref, s = reference_film(tmp_path)
    calls = counted(monkeypatch)
    first = film.reference(folder, s)
    assert calls == [1920]
    again = film.reference(folder, s, 1920)
    assert again == first and calls == [1920]
    assert "at" not in json.loads((folder / "film" / "reference.json").read_text(encoding="utf-8"))
    assert "look_width" not in first


def test_the_reference_at_a_smaller_width_is_measured_once_and_cached(tmp_path, monkeypatch):
    folder, ref, s = reference_film(tmp_path)
    top = film.reference(folder, s)
    calls = counted(monkeypatch)
    small = film.reference(folder, s, 960)
    assert calls == [960] and small["look_width"] == 960
    cached = json.loads((folder / "film" / "reference.json").read_text(encoding="utf-8"))
    assert set(cached["at"]["960"]) == {"tone", "tone_p10", "edges", "edges_p10"}
    assert {k: cached[k] for k in film.LOOK_KEYS} == {k: top[k] for k in film.LOOK_KEYS}    # the top level is as it was
    assert all(small[k] == cached["at"]["960"][k] for k in film.LOOK_KEYS) and small["luma"] == top["luma"]
    assert small["edges"] > top["edges"]                                  # the same film is sharper at the smaller width
    again = film.reference(folder, s, 960)
    assert again == small and calls == [960]                              # the cache held


def test_a_different_reference_file_clears_at(tmp_path, monkeypatch):
    folder, ref, s = reference_film(tmp_path)
    film.reference(folder, s)
    film.reference(folder, s, 960)
    other = make(tmp_path / "other.mp4", 1920, 1080, seconds=2)
    calls = counted(monkeypatch)
    film.reference(folder, {"reference": str(other)}, 960)
    cached = json.loads((folder / "film" / "reference.json").read_text(encoding="utf-8"))
    assert cached["file"] == str(other) and list(cached["at"]) == ["960"] and calls == [1920, 960]


# ---- collect ------------------------------------------------------------------------------------------------

def collect_film(tmp_path, clip_w, clip_h, ref_w=1920, ref_h=1080):
    """A pass of one real clip, collected against a real reference; returns the folder and collect's answer."""
    shot = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 100.0, "run_to_s": 104.0,
            "kind": "run"}
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    ref = make(tmp_path / "ref.mp4", ref_w, ref_h, seconds=3)
    settings = skill_settings(reference=str(ref))
    (folder / film.SETTINGS).write_text(json.dumps(settings), encoding="utf-8")
    (folder / "shots.json").write_text(json.dumps({"shots": [shot]}), encoding="utf-8")
    key = film.shot_key(shot, settings, {})
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    plan = {"pass": 1, "settings": settings, "size": f"{clip_w}x{clip_h}", "farm": None, "colour": None,
            "scenes": [{"n": 1, "shot": "1.2a", "key": key, "arm": "r67-6202", "camera": "Floor", "clip_start_s": 1.0,
                        "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
                        "handle_after_s": 1.0, "fog": None, "focus": None, "side": None}]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    make(safari / "story-01-floor.mp4", clip_w, clip_h, frames=121)
    (safari / "scenes.tsv").write_text("slug\toutcome\tprovenance\nstory-01-floor\tplayed\tFAITHFUL\n", encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {"r67-6202": str(safari)}}), encoding="utf-8")
    (out / "theatre-r67-6202.log").write_text("[Theatre] nothing", encoding="utf-8")
    verdict, lines = film.cmd_collect(folder, 1)
    clip = json.loads((folder / "clips.json").read_text(encoding="utf-8"))["clips"][0]
    return verdict, lines, clip, json.loads((folder / "film" / "reference.json").read_text(encoding="utf-8"))


def test_a_960_clip_is_measured_and_compared_at_960(tmp_path):
    verdict, lines, clip, ref = collect_film(tmp_path, 960, 540)
    assert verdict == "CLIPS: PASS", lines
    assert clip["look_width"] == 960
    assert clip["reference_look"] == ref["at"]["960"]
    assert "at 960 wide" in lines[1] and "not compared" not in lines[1]


def test_a_1920_clip_is_measured_at_1920_with_the_top_level_numbers(tmp_path):
    verdict, lines, clip, ref = collect_film(tmp_path, 1920, 1080)
    assert verdict == "CLIPS: PASS", lines
    assert clip["look_width"] == 1920
    assert clip["reference_look"] == {k: ref[k] for k in film.LOOK_KEYS}
    assert "at" not in ref and "at 1920 wide" in lines[1]


def test_a_clip_wider_than_1920_is_capped_at_1920(tmp_path):
    verdict, lines, clip, ref = collect_film(tmp_path, 2560, 1440, ref_w=2560, ref_h=1440)
    assert clip["look_width"] == 1920 and clip["reference_look"] == {k: ref[k] for k in film.LOOK_KEYS}


def test_a_reference_narrower_than_the_clip_is_not_compared(tmp_path):
    verdict, lines, clip, ref = collect_film(tmp_path, 960, 540, ref_w=640, ref_h=360)
    assert verdict == "CLIPS: PASS", lines
    assert clip["reference_look"] is None and clip["look_width"] == 960
    assert "flat" not in clip["flags"] and "soft" not in clip["flags"]
    assert "tone and edges not compared: " in lines[1] and "narrower" in lines[1]
    assert "judge them by eye against the reference sheet" in lines[1]


def test_a_reference_of_another_aspect_ratio_is_not_compared(tmp_path):
    verdict, lines, clip, ref = collect_film(tmp_path, 960, 540, ref_w=1920, ref_h=1920)
    assert clip["reference_look"] is None
    assert "flat" not in clip["flags"] and "soft" not in clip["flags"]
    assert "tone and edges not compared: " in lines[1] and "aspect ratio" in lines[1]
