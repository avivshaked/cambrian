"""The filming step's test pass: the reference file, the dark flag, the slides and the wall the plan checks.

The machine's coordinator's direction of 2026-11-07, after round 67's pass 2 filmed seven 4K takes that read as
night shots and sent 41 windows to the theatre under a 180-minute wall it could not meet.

  python -m pytest scripts/tests/story-video-check/test_test_pass.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_test_pass.py   (where pytest is not installed)
"""

import importlib.util
import json
import subprocess
import sys
import types
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

REAL_RUN = subprocess.run


def near(a, b):
    return abs(a - b) < 1e-9


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


def skill_settings(**over):
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s.update(over)
    return s


def film_folder(tmp_path, shots, **settings):
    folder = tmp_path / "film"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / film.SETTINGS).write_text(json.dumps(skill_settings(**settings)), encoding="utf-8")
    (folder / "shots.json").write_text(json.dumps({"shots": shots}), encoding="utf-8")
    return folder


# ---- the reference ----------------------------------------------------------------------------------------

def test_the_skills_reference_is_the_last_finished_episode():
    assert skill_settings()["reference"] == "assets/full-episodes/rounds-50-57 v3.mp4"


def stub_measure(monkeypatch, measured):
    monkeypatch.setattr(film, "probe", lambda p: {"duration": 10.0})
    monkeypatch.setattr(film, "ffmpeg", lambda *a, **k: None)
    monkeypatch.setattr(film, "yavg", lambda *a, **k: (measured.append(1), [65.0, 65.0])[1])
    monkeypatch.setattr(film, "look", lambda *a, **k: ([20.0, 21.0, 22.0], [5.0, 5.1, 5.2]))


def test_the_reference_is_measured_once_for_one_file(tmp_path, monkeypatch):
    folder = film_folder(tmp_path, [])
    ref_file = tmp_path / "episode.mp4"
    ref_file.write_bytes(b"x")
    s = {"reference": str(ref_file)}
    measured = []
    stub_measure(monkeypatch, measured)
    first = film.reference(folder, s)
    assert first["luma"] == 65.0 and first["file"] == str(ref_file) and len(measured) == 1
    again = film.reference(folder, s)
    assert again == first and len(measured) == 1                     # the cache held


def test_a_cached_reference_of_another_file_is_measured_again(tmp_path, monkeypatch):
    folder = film_folder(tmp_path, [])
    (folder / "film").mkdir()
    old = {"file": str(tmp_path / "round-48.mp4"), "luma": 56.6, "sheet": "film/reference-sheet.png", "tone": 30.0,
           "tone_p10": 20.0, "edges": 5.0, "edges_p10": 4.0}
    (folder / "film" / "reference.json").write_text(json.dumps(old), encoding="utf-8")
    new_file = tmp_path / "episode.mp4"
    new_file.write_bytes(b"x")
    measured = []
    stub_measure(monkeypatch, measured)
    ref = film.reference(folder, {"reference": str(new_file)})
    assert ref["file"] == str(new_file) and ref["luma"] == 65.0 and len(measured) == 1
    assert json.loads((folder / "film" / "reference.json").read_text(encoding="utf-8"))["file"] == str(new_file)


# ---- the dark flag in collect -----------------------------------------------------------------------------

def collect_film(tmp_path, monkeypatch, luma, ref_luma, station=None):
    shot = {"id": "1.2a", "run": "r67-6202", "bodies": [], "camera": "Floor", "run_from_s": 100.0, "run_to_s": 104.0,
            "kind": "run"}
    folder = film_folder(tmp_path, [shot])
    settings = skill_settings()
    key = film.shot_key(shot, settings, {})
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    plan = {"pass": 1, "settings": settings, "size": "1920x1080", "farm": None, "colour": None,
            "scenes": [{"n": 1, "shot": "1.2a", "key": key, "arm": "r67-6202", "camera": "Floor", "clip_start_s": 1.0,
                        "seconds": 4.0, "action_in_s": 1.0, "action_out_s": 3.0, "handle_before_s": 1.0,
                        "handle_after_s": 1.0, "fog": None, "focus": None, "side": None}]}
    (out / "plan.json").write_text(json.dumps(plan), encoding="utf-8")
    safari = tmp_path / "safari"
    safari.mkdir()
    (safari / "story-01-floor.mp4").write_bytes(b"clip")
    if station is None:
        table = "slug\toutcome\tprovenance\nstory-01-floor\tplayed\tFAITHFUL\n"
    else:
        table = f"slug\tstation\toutcome\tprovenance\nstory-01-floor\t{station}\tplayed\tFAITHFUL\n"
    (safari / "scenes.tsv").write_text(table, encoding="utf-8")
    (out / "done.json").write_text(json.dumps({"safari": {"r67-6202": str(safari)}}), encoding="utf-8")
    (out / "theatre-r67-6202.log").write_text("[Theatre] nothing", encoding="utf-8")
    monkeypatch.setattr(film, "probe", lambda p: {"frames": 121, "width": 1920, "height": 1080})
    monkeypatch.setattr(film, "reference", lambda f, s: {"luma": ref_luma, "tone": 1, "tone_p10": 0, "edges": 1,
                                                          "edges_p10": 0})
    monkeypatch.setattr(film, "sheet", lambda *a, **k: None)
    monkeypatch.setattr(film, "yavg", lambda *a, **k: [luma, luma, luma])
    monkeypatch.setattr(film, "look", lambda *a, **k: ([1.0], [1.0]))
    monkeypatch.setattr(film, "colour", lambda *a, **k: [1.0, 1.0, 1.0])
    return folder, film.cmd_collect(folder, 1)


def test_a_clip_under_the_references_luma_is_flagged_dark_and_still_passes(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, 40.0, 65.0)
    assert verdict == "CLIPS: PASS", lines
    assert any("dark: its luma 40 is under the reference's, 65" in x for x in lines)
    clip = json.loads((folder / "clips.json").read_text(encoding="utf-8"))["clips"][0]
    assert "dark" in clip["flags"]


def test_a_take_filmed_as_another_station_is_refused(tmp_path, monkeypatch):
    # Round 67's 6.4a: planned as a Birth, filmed by the director as an Arrival with only a warning in its log.
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, 65.0, 65.0, station="Arrival")
    assert verdict == "CLIPS: FAIL", lines
    assert any("shot 1.2a: the theatre filmed it as Arrival, not the plan's Floor" in x for x in lines)
    clips = folder / "clips.json"
    assert not clips.is_file() or json.loads(clips.read_text(encoding="utf-8"))["clips"] == []


def test_a_take_filmed_as_its_planned_station_is_collected(tmp_path, monkeypatch):
    folder, (verdict, lines) = collect_film(tmp_path, monkeypatch, 65.0, 65.0, station="Floor")
    assert verdict == "CLIPS: PASS", lines


def test_a_clip_at_or_over_the_references_luma_is_not_flagged(tmp_path, monkeypatch):
    for luma in (65.0, 70.0):
        folder, (verdict, lines) = collect_film(tmp_path / str(luma), monkeypatch, luma, 65.0)
        assert verdict == "CLIPS: PASS" and not any("dark" in x for x in lines)
        assert "dark" not in json.loads((folder / "clips.json").read_text(encoding="utf-8"))["clips"][0]["flags"]


# ---- the slides -------------------------------------------------------------------------------------------

def make_clip(path, frames=None):
    """A tiny test pattern at 64x36: a second of video, or one frame for a picture."""
    r = REAL_RUN(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=64x36:rate=30:duration=1",
                  *(["-frames:v", "1"] if frames else ["-pix_fmt", "yuv420p"]), str(path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr


def collected_film(tmp_path, ids=("1.1a", "1.2a"), collected_pass=1):
    shots = [{"id": i, "run": "r67-6202", "bodies": [], "camera": "Floor" if n % 2 == 0 else "Descent",
              "run_from_s": 100.0, "run_to_s": 104.0, "kind": "run", "see": f"the floor of {i} <lit>"}
             for n, i in enumerate(ids)]
    shots.insert(1, {"id": "text", "kind": "graphic"})                  # a shot with no footage is no slide
    folder = film_folder(tmp_path, shots)
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    (out / "plan.json").write_text(json.dumps({"pass": 1, "size": "64x36", "settings": skill_settings()}), encoding="utf-8")
    (out / "done.json").write_text("{}", encoding="utf-8")
    (folder / "clips").mkdir()
    clips = []
    for i in ids:
        make_clip(folder / "clips" / f"{i}-abc.mp4")
        make_clip(folder / "clips" / f"{i}-sheet.png", frames=1)                  # any image will do for a sheet
        clips.append({"shot": i, "key": "k", "pass": collected_pass, "file": f"clips/{i}-abc.mp4", "sheet": f"clips/{i}-sheet.png",
                      "frames": 30, "fps": 30, "width": 64, "height": 36, "run": "r67-6202", "clip_start_s": 97.0,
                      "action_in_s": 0.2, "action_out_s": 0.8, "provenance": "FAITHFUL", "luma": 40.5,
                      "jump_ratio": 1.5, "tone": 20.0, "edges": 4.0, "flags": ["dark", "soft"]})
    (folder / "clips.json").write_text(json.dumps({"schema": film.SCHEMA, "reference": {"luma": 65.0, "tone": 30.0,
                                       "edges": 5.0}, "clips": clips}), encoding="utf-8")
    return folder


def test_the_slides_hold_a_slide_a_take_a_first_slide_and_the_bar(tmp_path):
    folder = collected_film(tmp_path)
    verdict, lines = film.cmd_slides(folder, 1)
    assert verdict == "SLIDES: READY", lines
    deck = folder / "film" / "pass-1" / "slides"
    assert str(deck / "index.html") in " ".join(lines)
    html = (deck / "index.html").read_text(encoding="utf-8")
    assert html.count('<section class="slide">') == 4                   # first, two takes, the bar
    assert "Pass 1, 64x36, 2 takes" in html                             # the first slide
    for line in film.BAR:
        assert film.esc(line) in html                                   # the bar, word for word
    assert "0. Amazing, judged by eye at full size" in html
    assert html.index("1.1a &middot; Floor") < html.index("1.2a &middot; Descent")      # the shot list's order
    assert "the floor of 1.1a &lt;lit&gt;" in html                      # what the viewer should see, escaped
    assert "luma</th><td>40.5 (reference 65.0)" in html and 'class="flag">dark<' in html and 'class="flag">soft<' in html
    assert "run r67-6202" in html and "from 97 s of the run" in html
    for i in ("1.1a", "1.2a"):
        assert (deck / "img" / f"{i}-sheet.png").is_file()
        frame = deck / "img" / f"{i}-frame.png"
        assert frame.is_file() and frame.read_bytes()[:4] == b"\x89PNG"
        assert f'src="img/{i}-sheet.png"' in html and f'src="img/{i}-frame.png"' in html
    assert not any(l for l in html.splitlines() if "http://" in l or "https://" in l)    # self-contained


def test_the_frame_is_the_clips_own_size(tmp_path):
    folder = collected_film(tmp_path, ids=("1.1a",))
    film.cmd_slides(folder, 1)
    r = REAL_RUN(["ffprobe", "-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0",
                  str(folder / "film" / "pass-1" / "slides" / "img" / "1.1a-frame.png")], capture_output=True, text=True)
    assert r.stdout.strip() == "64,36"


def test_the_images_are_copies_that_survive_a_later_collect(tmp_path):
    folder = collected_film(tmp_path, ids=("1.1a",))
    film.cmd_slides(folder, 1)
    img = folder / "film" / "pass-1" / "slides" / "img" / "1.1a-sheet.png"
    before = img.read_bytes()
    (folder / "clips" / "1.1a-sheet.png").unlink()                     # a later collect writes the sheet afresh
    assert img.read_bytes() == before


def test_slides_refuse_a_pass_not_collected(tmp_path):
    folder = collected_film(tmp_path, collected_pass=2)                 # clips.json holds only pass 2
    said = refused(film.cmd_slides, folder, 1)
    assert "not collected" in said
    assert not (folder / "film" / "pass-1" / "slides").exists()
    # and a pass that was never planned
    assert "has not been planned" in refused(film.cmd_slides, folder, 3)


def test_slides_through_the_command_line_print_the_verdict(tmp_path):
    folder = collected_film(tmp_path, ids=("1.1a",))
    r = REAL_RUN([sys.executable, "-B", str(SKILL / "film.py"), "slides", str(folder), "1"], capture_output=True, text=True)
    out = r.stdout.splitlines()
    assert r.returncode == 0 and out[0] == "SLIDES: READY" and out[-1].startswith("took "), r.stdout + r.stderr
    assert any(x.startswith("deck: ") and x.endswith("index.html") for x in out)
    r = REAL_RUN([sys.executable, "-B", str(SKILL / "film.py"), "slides", str(folder), "7"], capture_output=True, text=True)
    assert r.returncode == 1 and r.stdout.splitlines()[0] == "SLIDES: REFUSED"


# ---- the wall ---------------------------------------------------------------------------------------------

def entries_of(arm, windows, frames_total, fps=30):
    """windows entries of one run whose frames add to frames_total (each entry's frames are round(seconds x fps) + 1)."""
    frames = [frames_total // windows + (1 if n < frames_total % windows else 0) for n in range(windows)]
    return [{"arm": arm, "seconds": (f - 1) / fps} for f in frames]


def test_the_cost_settings_are_in_the_skills_copy_at_their_defaults():
    s = skill_settings()
    assert (s["theatre_start_s"], s["theatre_window_s"], s["theatre_frame_s"]) == (300, 240, 0.45)


def test_round_67s_run_is_refused_at_180_minutes_and_passes_at_450():
    entries = entries_of("r67-6202", 41, 20586)
    size = "3840x2160"
    s = skill_settings(wall_minutes=180)
    frames = sum(round(e["seconds"] * s["fps"]) + 1 for e in entries)
    assert frames == 20586
    estimate = (300 + 41 * 240 + 20586 * 0.45) / 60
    lines, over = film.wall_lines(s, entries, size)
    assert len(over) == 1 and "r67-6202" in over[0] and f"about {estimate:.0f} min" in over[0]
    assert "wall_minutes" in over[0] and "split" in over[0]
    assert "41 windows, 20586 frames" in lines[0] and "against the wall of 180 min" in lines[0]
    lines, over = film.wall_lines(skill_settings(wall_minutes=450), entries, size)
    assert over == [] and len(lines) == 1


def test_a_small_run_passes():
    lines, over = film.wall_lines(skill_settings(), entries_of("a", 3, 900), "3840x2160")
    assert over == [] and "3 windows, 900 frames" in lines[0]


def test_a_960x540_pass_uses_a_quarter_of_the_frame_cost():
    s = skill_settings()
    assert near(film.frame_cost_s(s, "3840x2160"), 0.45)
    assert near(film.frame_cost_s(s, "960x540"), 0.45 / 16 * 4)   # the floor: a quarter of it
    assert near(film.frame_cost_s(s, "960x540"), 0.1125)
    assert near(film.frame_cost_s(s, "1920x1080"), 0.1125)   # a quarter of the pixels too
    assert near(film.frame_cost_s(s, "64x36"), 0.1125)   # never below a quarter
    e = entries_of("a", 10, 5000)
    big = film.run_estimates(s, e, "3840x2160")["a"][2]
    small = film.run_estimates(s, e, "960x540")["a"][2]
    assert near(big - small, 5000 * (0.45 - 0.1125) / 60)
def test_each_run_has_its_own_estimate():
    e = entries_of("a", 2, 600) + entries_of("b", 5, 1500)
    found = film.run_estimates(skill_settings(), e, "3840x2160")
    assert sorted(found) == ["a", "b"] and found["a"][:2] == (2, 600) and found["b"][:2] == (5, 1500)


def test_the_settings_are_validated_and_an_older_copy_takes_the_defaults(tmp_path):
    for bad in (-1, "300", True):
        folder = film_folder(tmp_path / str(bad), [], theatre_window_s=bad)
        assert "theatre_window_s" in refused(film.film_settings, folder, [])
    folder = film_folder(tmp_path / "ok", [], theatre_frame_s=0.3)
    assert film.film_settings(folder, [])["theatre_frame_s"] == 0.3
    # an older copy has none of the three
    old = skill_settings()
    for key in film.THEATRE_COSTS:
        del old[key]
    folder = tmp_path / "old"
    folder.mkdir()
    (folder / film.SETTINGS).write_text(json.dumps(old), encoding="utf-8")
    s = film.film_settings(folder, [])
    assert [film.theatre_cost(s, k) for k in film.THEATRE_COSTS] == [300, 240, 0.45]
    assert film.wall_lines(s, entries_of("a", 3, 900), "3840x2160")[1] == []


def rundir(root, arm):
    d = root / arm / "r1"
    d.mkdir(parents=True)
    (d / "run.json").write_text(json.dumps({"source": {}}), encoding="utf-8")
    return d


def plan_film(tmp_path, monkeypatch, shots, **settings):
    runs = tmp_path / "runs"
    for arm in {x["run"] for x in shots}:
        rundir(runs, arm)
    folder = film_folder(tmp_path, shots, runs_root=str(runs), **settings)
    monkeypatch.setattr(film, "last_second", lambda d: 1e6)
    monkeypatch.setattr(film, "lead_in", lambda d, sh, h: (sh["run_from_s"] - 3.0, None))
    monkeypatch.setattr(film, "reuse_windows", lambda folder, k, windows: 0)
    monkeypatch.setattr(film, "stale_worker", lambda s: None)

    def fake_windows(args, **kw):
        story = json.loads(Path(args[2]).read_text(encoding="utf-8"))
        out = Path(args[4])
        out.mkdir(parents=True, exist_ok=True)
        ws = [{"scene": sc["n"], "part": "main", "station": sc["station"], "start": sc["second"]}
              for sc in story["scenes"]]
        (out / "windows.json").write_text(json.dumps({"windows": ws, "skipped": []}), encoding="utf-8")
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(film.subprocess, "run", fake_windows)
    return folder


def a_shot(sid, run="r67-6202", a=100.0):
    return {"id": sid, "run": run, "bodies": [], "camera": "Floor", "run_from_s": a, "run_to_s": a + 4.0, "kind": "run"}


def test_the_plan_prints_a_line_a_run_and_passes_a_small_run(tmp_path, monkeypatch):
    folder = plan_film(tmp_path, monkeypatch, [a_shot("1.1a"), a_shot("1.2a", a=200.0)])
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    said = [x for x in lines if x.startswith("theatre r67-6202:")]
    assert len(said) == 1 and "2 windows" in said[0] and "against the wall of 180 min" in said[0]


def test_the_plan_refuses_a_run_over_the_wall_and_writes_nothing(tmp_path, monkeypatch):
    # one shot of 10 s with handles: 300 + 240 + 301 x 0.45 = 675 s, over a wall of 10 min
    folder = plan_film(tmp_path, monkeypatch, [a_shot("1.1a")], wall_minutes=10)
    said = refused(film.cmd_plan, folder, 1)
    assert "r67-6202" in said and "wall of 10 min" in said and "raise wall_minutes" in said and "split" in said
    assert not (folder / "film" / "pass-1" / "plan.json").exists()
    assert not (folder / "film" / "pass-1" / "story.json").exists()


def test_a_960x540_pass_fits_a_wall_the_4k_pass_does_not(tmp_path, monkeypatch):
    shots = [a_shot(f"1.{n}a", a=100.0 + 20 * n) for n in range(1, 41)]
    # 40 windows of 301 frames: 4K needs (300 + 9600 + 5418) s = 255 min, 960x540 (300 + 9600 + 1354) s = 188 min
    folder = plan_film(tmp_path / "big", monkeypatch, shots, wall_minutes=200)
    assert "over the wall of 200 min" in refused(film.cmd_plan, folder, 1)
    folder = plan_film(tmp_path / "small", monkeypatch, shots, wall_minutes=200, size="960x540")
    verdict, lines = film.cmd_plan(folder, 1)
    assert verdict == "FILM PLAN: READY", lines
    assert any("40 windows, 12040 frames, about 188 min" in x for x in lines)
