"""film.py stills: a stills round, the theatre lane's stills of a shot under up to three variants of dials, measured
as collect measures a clip and made into a deck for the reviewer before a pass films anything.

Also pins cmd_slides' deck byte for byte, since the stills deck shares its page with it.

  python -m pytest scripts/tests/story-video-check/test_stills.py
  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_stills.py   (where pytest is not installed)
"""

import hashlib
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

REAL_RUN = subprocess.run
SIZE = "64x36"
REF = {"luma": 20.0, "tone": 30.0, "tone_p10": 1.0, "edges": 5.0, "edges_p10": 0.5}


def refused(fn, *a):
    with pytest.raises(film.Refused) as e:
        fn(*a)
    return " | ".join(e.value.lines)


def make_png(path, colour="0x808080", pattern=False):
    """One frame, 64x36: a flat colour, or a test pattern that has edges."""
    src = "testsrc=size=64x36:rate=30" if pattern else f"color=c={colour}:s=64x36:r=30"
    r = REAL_RUN(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", src, "-frames:v", "1", str(path)],
                 capture_output=True, text=True)
    assert r.returncode == 0, r.stderr


def shot(sid, camera="Floor", run="r67r1-6202"):
    return {"id": sid, "run": run, "bodies": [], "camera": camera, "run_from_s": 39000.0, "run_to_s": 39020.0,
            "kind": "run", "see": f"the lit water of {sid} <wide>"}


def take(shot_id, variant, n=3, run="r67r1-6202", dials=None):
    seconds = [39004.0, 39009.0, 39014.0][:n] if n > 1 else [39009.0]
    frames = [{"file": f"{shot_id}-{variant}-{i}.png", "second": s, "what": ["start", "middle", "end"][i - 1] if n == 3 else f"still {i}"}
              for i, s in enumerate(seconds, 1)]
    return {"shot": shot_id, "variant": variant, "run": run,
            "dials": {"EVOSIM_THEATRE_GROUP_NEAREST": "0.25"} if dials is None else dials, "frames": frames}


def good_manifest():
    return {"stills": 1, "head": "c7061b25", "provenance": "COUSIN",
            "takes": [take("3.1a", "A", 2, dials={}), take("2.3a", "B"), take("2.3a", "A")]}   # out of order on purpose


def stills_film(tmp_path, monkeypatch, manifest=None, ref=REF, pattern=True):
    """A film folder with two shots and a stills folder under it holding the manifest and its frames."""
    folder = tmp_path / "film"
    folder.mkdir(parents=True)
    s = json.loads((SKILL / film.SETTINGS).read_text(encoding="utf-8"))
    s["size"] = SIZE
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    shots = [shot("1.1a"), shot("2.3a", "Birth"), {"id": "text", "kind": "graphic"}, shot("3.1a", "Descent")]
    (folder / "shots.json").write_text(json.dumps({"shots": shots}), encoding="utf-8")
    src = folder / "stills-in"
    src.mkdir()
    manifest = good_manifest() if manifest is None else manifest
    for t in manifest["takes"]:
        for i, fr in enumerate(t.get("frames") or []):
            if isinstance(fr, dict) and isinstance(fr.get("file"), str):
                make_png(src / fr["file"], colour=["0x808080", "0xa0a0a0", "0xc0c0c0"][i % 3], pattern=pattern)
    (src / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    monkeypatch.setattr(film, "reference", lambda f, s, w=1920: dict(ref))
    return folder, src


# ---- the slides, pinned before the stills deck shared their page ------------------------------------------

def collected_film(tmp_path):
    shots = [{"id": i, "run": "r67-6202", "bodies": [], "camera": "Floor" if n % 2 == 0 else "Descent",
              "run_from_s": 100.0, "run_to_s": 104.0, "kind": "run", "see": f"the floor of {i} <lit>"}
             for n, i in enumerate(("1.1a", "1.2a"))]
    shots.insert(1, {"id": "text", "kind": "graphic"})
    folder = tmp_path / "film"
    out = folder / "film" / "pass-1"
    out.mkdir(parents=True)
    (folder / "shots.json").write_text(json.dumps({"shots": shots}), encoding="utf-8")
    (out / "plan.json").write_text(json.dumps({"pass": 1, "size": "64x36", "settings": {}}), encoding="utf-8")
    (out / "done.json").write_text("{}", encoding="utf-8")
    (folder / "clips").mkdir()
    clips = []
    for i in ("1.1a", "1.2a"):
        r = REAL_RUN(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=64x36:rate=30:duration=1",
                      "-pix_fmt", "yuv420p", str(folder / "clips" / f"{i}-abc.mp4")], capture_output=True, text=True)
        assert r.returncode == 0, r.stderr
        make_png(folder / "clips" / f"{i}-sheet.png", pattern=True)
        clips.append({"shot": i, "key": "k", "pass": 1, "file": f"clips/{i}-abc.mp4", "sheet": f"clips/{i}-sheet.png",
                      "frames": 30, "fps": 30, "width": 64, "height": 36, "run": "r67-6202", "clip_start_s": 97.0,
                      "action_in_s": 0.2, "action_out_s": 0.8, "provenance": "FAITHFUL", "luma": 40.5,
                      "jump_ratio": 1.5, "tone": 20.0, "edges": 4.0, "flags": ["dark", "soft"]})
    (folder / "clips.json").write_text(json.dumps({"schema": film.SCHEMA, "reference": {"luma": 65.0, "tone": 30.0,
                                       "edges": 5.0}, "clips": clips}), encoding="utf-8")
    return folder


# The sha256 of the deck cmd_slides wrote for collected_film before the shared page helpers were made, with the
# date fixed. A change to the deck's markup, style or script moves it, and the owner's decks are then not the same.
SLIDES_DECK_SHA256 = "f9a1759c182a17e0b1e2f43e812f9ad53751f04ff5dc8f2c305b6eab0eaf5093"


def test_the_slides_deck_of_a_pass_is_byte_for_byte_what_it_was(tmp_path, monkeypatch):
    folder = collected_film(tmp_path)
    monkeypatch.setattr(film, "now", lambda: "2026-11-09T00:00:00")
    film.cmd_slides(folder, 1)
    html = (folder / "film" / "pass-1" / "slides" / "index.html").read_bytes()
    assert hashlib.sha256(html).hexdigest() == SLIDES_DECK_SHA256


# ---- a good round -----------------------------------------------------------------------------------------

def test_a_good_manifest_gives_a_round_with_its_takes_in_order_and_a_deck_of_five_slides(tmp_path, monkeypatch):
    folder, src = stills_film(tmp_path, monkeypatch)
    verdict, lines = film.cmd_stills(folder, 1, src)
    assert verdict == "STILLS: READY", lines
    assert lines[0] == "stills 1: 3 takes of 2 shots, 5 slides"
    out = folder / "film" / "stills-1"
    assert lines[-1] == f"deck: {out / 'slides' / 'index.html'}"
    data = json.loads((out / "stills.json").read_text(encoding="utf-8"))
    assert data["stills"] == 1 and data["head"] == "c7061b25" and data["provenance"] == "COUSIN" and data["made"]
    assert data["reference"]["luma"] == 20.0
    assert [t["take"] for t in data["takes"]] == ["2.3a:A", "2.3a:B", "3.1a:A"]      # the shot list's order, then the letter
    first = data["takes"][0]
    assert first["shot"] == "2.3a" and first["variant"] == "A" and first["run"] == "r67r1-6202"
    assert first["dials"] == {"EVOSIM_THEATRE_GROUP_NEAREST": "0.25"} and first["provenance"] == "COUSIN"
    assert first["look_width"] == 64 and first["reference_look"]["edges_p10"] == 0.5 and first["size"] == SIZE
    assert [f["second"] for f in first["frames"]] == [39004.0, 39009.0, 39014.0]
    assert [f["what"] for f in first["frames"]] == ["start", "middle", "end"]
    for t in data["takes"]:
        assert t["luma"] is not None and t["tone"] is not None and t["edges"] is not None and isinstance(t["flags"], list)
        for f in t["frames"]:
            assert (folder / f["file"]).is_file() and f["file"].startswith("film/stills-1/")
            assert f["luma"] is not None and f["tone"] is not None and f["edges"] is not None
    assert data["takes"][2]["dials"] == {}
    # a take's numbers are the mean over its frames, rounded to two places
    for key in ("luma", "tone", "edges"):
        assert abs(first[key] - sum(f[key] for f in first["frames"]) / 3) < 0.01
    html = (out / "slides" / "index.html").read_text(encoding="utf-8")
    assert html.count('<section class="slide">') == 5                       # first, three takes, the bar
    assert "Stills 1, 64x36, 3 takes of 2 shots, head c7061b25, COUSIN" in html
    assert html.index("2.3a:A &middot; Birth") < html.index("2.3a:B &middot; Birth") < html.index("3.1a:A &middot; Descent")
    assert "the lit water of 2.3a &lt;wide&gt;" in html
    assert "dials: EVOSIM_THEATRE_GROUP_NEAREST 0.25" in html and "the film look&#x27;s own" in html.replace("'", "&#x27;")
    assert "stills at 39004, 39009, 39014 s of the run" in html and "run r67r1-6202" in html
    for line in film.BAR:
        assert film.esc(line) in html
    for name in ("2.3a-A", "2.3a-B", "3.1a-A"):
        for kind in ("sheet", "frame"):
            png = out / "slides" / "img" / f"{name}-{kind}.png"
            assert png.is_file() and png.read_bytes()[:4] == b"\x89PNG"
            assert f'src="img/{name}-{kind}.png"' in html
    assert not any(l for l in html.splitlines() if "http://" in l or "https://" in l)


def dims(path):
    r = REAL_RUN(["ffprobe", "-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0", str(path)],
                 capture_output=True, text=True)
    return tuple(int(v) for v in r.stdout.strip().split(","))


def test_the_sheet_is_the_stills_side_by_side_and_the_frame_is_the_middle_one(tmp_path, monkeypatch):
    folder, src = stills_film(tmp_path, monkeypatch)
    film.cmd_stills(folder, 1, src)
    img = folder / "film" / "stills-1" / "slides" / "img"
    assert dims(img / "2.3a-A-sheet.png") == (480 * 3, 270)             # three stills, each scaled to 480 wide
    assert dims(img / "3.1a-A-sheet.png") == (480 * 2, 270)             # two
    assert dims(img / "2.3a-A-frame.png") == (64, 36)                   # full size
    assert (img / "2.3a-A-frame.png").read_bytes() == (src / "2.3a-A-2.png").read_bytes()    # the second of three
    assert (img / "3.1a-A-frame.png").read_bytes() == (src / "3.1a-A-2.png").read_bytes()    # the second of two


def test_a_take_of_one_still_is_its_own_sheet_and_frame(tmp_path, monkeypatch):
    m = {"stills": 1, "head": "h", "provenance": "FAITHFUL", "takes": [take("1.1a", "A", 1)]}
    folder, src = stills_film(tmp_path, monkeypatch, m)
    verdict, lines = film.cmd_stills(folder, 1, src)
    assert verdict == "STILLS: READY" and lines[0] == "stills 1: 1 takes of 1 shots, 3 slides", lines
    img = folder / "film" / "stills-1" / "slides" / "img"
    assert dims(img / "1.1a-A-sheet.png") == (64, 36) and dims(img / "1.1a-A-frame.png") == (64, 36)


def test_a_dark_flat_soft_take_is_flagged_as_collect_flags_a_clip_and_still_passes(tmp_path, monkeypatch):
    m = {"stills": 1, "head": "h", "provenance": "COUSIN", "takes": [take("1.1a", "A", 2)]}
    ref = {"luma": 200.0, "tone": 30.0, "tone_p10": 99.0, "edges": 9.0, "edges_p10": 99.0}
    folder, src = stills_film(tmp_path, monkeypatch, m, ref=ref, pattern=False)     # flat grey: no tone, no edges
    verdict, lines = film.cmd_stills(folder, 1, src)
    assert verdict == "STILLS: READY", lines
    t = json.loads((folder / "film" / "stills-1" / "stills.json").read_text(encoding="utf-8"))["takes"][0]
    assert t["flags"] == ["dark", "flat", "soft"] and "jump" not in t["flags"]
    html = (folder / "film" / "stills-1" / "slides" / "index.html").read_text(encoding="utf-8")
    assert 'class="flag">dark<' in html and 'class="flag">flat<' in html and 'class="flag">soft<' in html


def test_a_bright_sharp_take_has_no_flags(tmp_path, monkeypatch):
    m = {"stills": 1, "head": "h", "provenance": "COUSIN", "takes": [take("1.1a", "A", 2)]}
    ref = {"luma": 10.0, "tone": 30.0, "tone_p10": 0.0, "edges": 5.0, "edges_p10": 0.0}
    folder, src = stills_film(tmp_path, monkeypatch, m, ref=ref)
    film.cmd_stills(folder, 1, src)
    t = json.loads((folder / "film" / "stills-1" / "stills.json").read_text(encoding="utf-8"))["takes"][0]
    assert t["flags"] == []


def test_the_reference_is_asked_for_at_the_frames_width(tmp_path, monkeypatch):
    folder, src = stills_film(tmp_path, monkeypatch)
    asked = []
    monkeypatch.setattr(film, "reference", lambda f, s, w=1920: (asked.append(w), dict(REF))[1])
    film.cmd_stills(folder, 1, src)
    assert 64 in asked


# ---- the refusals -----------------------------------------------------------------------------------------

def stills_case(tmp_path, monkeypatch, change, j=1):
    """A good round with `change` applied to its manifest (and the frames made after), and the refusal it draws."""
    m = good_manifest()
    change(m)
    folder, src = stills_film(tmp_path, monkeypatch, m)
    said = refused(film.cmd_stills, folder, j, src)
    assert not (folder / "film" / f"stills-{j}").exists()               # a refusal writes nothing
    return said, folder, src


def test_a_missing_or_unreadable_manifest_is_refused(tmp_path, monkeypatch):
    folder, src = stills_film(tmp_path, monkeypatch)
    (src / "manifest.json").unlink()
    assert "manifest.json is missing or cannot be read" in refused(film.cmd_stills, folder, 1, src)
    (src / "manifest.json").write_text("{not json", encoding="utf-8")
    assert "manifest.json is missing or cannot be read" in refused(film.cmd_stills, folder, 1, src)
    assert not (folder / "film" / "stills-1").exists()


def test_a_manifest_for_another_round_is_refused(tmp_path, monkeypatch):
    said, _, _ = stills_case(tmp_path, monkeypatch, lambda m: m.update(stills=2))
    assert "stills round 2, not 1" in said


def test_an_existing_round_is_refused_and_a_second_run_with_the_same_number_changes_nothing(tmp_path, monkeypatch):
    folder, src = stills_film(tmp_path, monkeypatch)
    assert film.cmd_stills(folder, 1, src)[0] == "STILLS: READY"
    out = folder / "film" / "stills-1"
    before = (out / "stills.json").read_bytes()
    deck = (out / "slides" / "index.html").read_bytes()
    said = refused(film.cmd_stills, folder, 1, src)
    assert "film/stills-1/ exists already" in said and "made once" in said
    assert (out / "stills.json").read_bytes() == before and (out / "slides" / "index.html").read_bytes() == deck


def test_a_take_of_a_shot_not_in_the_shot_list_is_refused(tmp_path, monkeypatch):
    said, _, _ = stills_case(tmp_path, monkeypatch, lambda m: m["takes"][0].update(shot="9.9z", frames=[]))
    assert "shot '9.9z' is not in shots.json" in said


def test_a_shot_with_no_run_is_refused(tmp_path, monkeypatch):
    m = good_manifest()
    m["takes"][0].update(shot="1.1a")
    for fr in m["takes"][0]["frames"]:
        fr["file"] = fr["file"].replace("3.1a", "1.1a")
    folder, src = stills_film(tmp_path, monkeypatch, m)
    shots = json.loads((folder / "shots.json").read_text(encoding="utf-8"))
    del shots["shots"][0]["run"]
    (folder / "shots.json").write_text(json.dumps(shots), encoding="utf-8")
    assert "shot 1.1a has no run" in refused(film.cmd_stills, folder, 1, src)
    assert not (folder / "film" / "stills-1").exists()


def test_a_take_whose_run_is_not_its_shots_is_refused(tmp_path, monkeypatch):
    said, _, _ = stills_case(tmp_path, monkeypatch, lambda m: m["takes"][1].update(run="r67r1-6203"))
    assert "its run 'r67r1-6203' is not shot 2.3a's, r67r1-6202" in said


def test_a_variant_that_is_not_one_capital_letter_is_refused(tmp_path, monkeypatch):
    for bad in ("a", "AB", "", "1", None):
        said, _, _ = stills_case(tmp_path / f"v{bad}", monkeypatch, lambda m: m["takes"][1].update(variant=bad))
        assert "the variant is one capital letter" in said


def test_a_shot_and_variant_given_twice_are_refused(tmp_path, monkeypatch):
    said, _, _ = stills_case(tmp_path, monkeypatch, lambda m: m["takes"][1].update(variant="A"))
    assert "take 2.3a:A is given twice" in said


def test_a_take_with_no_frames_or_more_than_five_is_refused(tmp_path, monkeypatch):
    said, _, _ = stills_case(tmp_path / "none", monkeypatch, lambda m: m["takes"][0].update(frames=[]))
    assert "1 to 5 are allowed" in said
    six = [{"file": f"3.1a-A-{i}.png", "second": 39001.0 + i, "what": "x"} for i in range(1, 7)]
    said, _, _ = stills_case(tmp_path / "six", monkeypatch, lambda m: m["takes"][0].update(frames=six))
    assert "it holds 6 frames, 1 to 5 are allowed" in said


def test_a_move_in_five_stills_is_one_sheet_of_five_and_its_frame_is_the_third(tmp_path, monkeypatch):
    five = take("3.1a", "A", 1)
    five["frames"] = [{"file": f"3.1a-A-{i}.png", "second": s, "what": f"{p} %"}
                      for i, (s, p) in enumerate(zip([39004.0, 39006.5, 39009.0, 39011.5, 39014.0], [0, 25, 50, 75, 100]), 1)]
    m = {"stills": 1, "head": "h", "provenance": "COUSIN", "takes": [five]}
    folder, src = stills_film(tmp_path, monkeypatch, m)
    verdict, lines = film.cmd_stills(folder, 1, src)
    assert verdict == "STILLS: READY", lines
    img = folder / "film" / "stills-1" / "slides" / "img"
    assert dims(img / "3.1a-A-sheet.png") == (480 * 5, 270)
    assert (img / "3.1a-A-frame.png").read_bytes() == (src / "3.1a-A-3.png").read_bytes()


def test_a_frame_file_that_is_missing_is_refused(tmp_path, monkeypatch):
    m = good_manifest()
    folder, src = stills_film(tmp_path, monkeypatch, m)
    (src / "2.3a-B-3.png").unlink()
    said = refused(film.cmd_stills, folder, 1, src)
    assert "take 2.3a:B, frame 3: '2.3a-B-3.png' is not a file" in said
    assert not (folder / "film" / "stills-1").exists()


def test_a_frame_that_is_not_a_png_is_refused(tmp_path, monkeypatch):
    m = good_manifest()
    folder, src = stills_film(tmp_path, monkeypatch, m)
    (src / "2.3a-B-2.png").write_bytes(b"GIF89a not a png at all, just text, long enough to read a header from")
    assert "take 2.3a:B, frame 2: 2.3a-B-2.png is not a PNG" in refused(film.cmd_stills, folder, 1, src)


def test_a_frame_not_at_the_films_size_is_refused(tmp_path, monkeypatch):
    m = good_manifest()
    folder, src = stills_film(tmp_path, monkeypatch, m)
    r = REAL_RUN(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=gray:s=128x72:r=30", "-frames:v", "1",
                  str(src / "3.1a-A-1.png")], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    assert "take 3.1a:A, frame 1: 3.1a-A-1.png is 128x72, the film's size is 64x36" in refused(film.cmd_stills, folder, 1, src)


def test_a_frame_outside_its_shots_run_window_is_refused(tmp_path, monkeypatch):
    def change(m):
        m["takes"][0]["frames"][1]["second"] = 38999.0
    said, _, _ = stills_case(tmp_path / "before", monkeypatch, change)
    assert "second 38999 lies outside shot 3.1a's 39000 to 39020 s of the run" in said

    def after(m):
        m["takes"][1]["frames"][2]["second"] = 39020.5
    said, _, _ = stills_case(tmp_path / "after", monkeypatch, after)
    assert "second 39020.5 lies outside shot 2.3a's 39000 to 39020 s of the run" in said


def test_frames_out_of_time_order_are_refused(tmp_path, monkeypatch):
    def change(m):
        m["takes"][1]["frames"][0]["second"], m["takes"][1]["frames"][1]["second"] = 39009.0, 39004.0
    said, _, _ = stills_case(tmp_path, monkeypatch, change)
    assert "take 2.3a:B, frame 2: second 39004 comes before the frame before it, 39009" in said


def test_a_provenance_that_is_neither_faithful_nor_cousin_is_refused(tmp_path, monkeypatch):
    for bad in ("FAITHFULL", "", None):
        said, _, _ = stills_case(tmp_path / f"p{bad}", monkeypatch, lambda m: m.update(provenance=bad))
        assert "provenance" in said and "not FAITHFUL or COUSIN" in said


def test_every_fault_is_named_not_only_the_first(tmp_path, monkeypatch):
    def change(m):
        m.update(provenance="x")
        m["takes"][0]["frames"][0]["second"] = 1.0
        m["takes"][1].update(run="elsewhere")
    said, _, _ = stills_case(tmp_path, monkeypatch, change)
    assert said.count(" | ") >= 2


# ---- the command line -------------------------------------------------------------------------------------

def test_through_the_command_line_the_verdict_comes_first_and_took_last(tmp_path, monkeypatch):
    folder, src = stills_film(tmp_path, monkeypatch)
    ref = tmp_path / "ref.mp4"
    r = REAL_RUN(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=10:duration=2",
                  "-pix_fmt", "yuv420p", str(ref)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    s = json.loads((folder / film.SETTINGS).read_text(encoding="utf-8"))
    s["reference"] = str(ref)
    (folder / film.SETTINGS).write_text(json.dumps(s), encoding="utf-8")
    run = REAL_RUN([sys.executable, "-B", str(SKILL / "film.py"), "stills", str(folder), "1", str(src)],
                   capture_output=True, text=True)
    out = run.stdout.splitlines()
    assert run.returncode == 0 and out[0] == "STILLS: READY", run.stdout + run.stderr
    assert out[1] == "stills 1: 3 takes of 2 shots, 5 slides" and out[-1].startswith("took ")
    assert out[-2].startswith("deck: ") and out[-2].endswith("index.html")
    again = REAL_RUN([sys.executable, "-B", str(SKILL / "film.py"), "stills", str(folder), "1", str(src)],
                     capture_output=True, text=True)
    assert again.returncode == 1 and again.stdout.splitlines()[0] == "STILLS: REFUSED"
    wrong = REAL_RUN([sys.executable, "-B", str(SKILL / "film.py"), "stills", str(folder), "1"], capture_output=True, text=True)
    assert wrong.returncode == 2
