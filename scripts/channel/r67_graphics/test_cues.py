"""Checks of cues.py and merge_bodies.py, on synthetic data and (where the salvage folder is on this machine) on
the real edit list.

    python -I -B scripts/tests/minirun.py scripts/channel/r67_graphics/test_cues.py
"""
import importlib.util
from pathlib import Path

GFX = Path(__file__).resolve().parent


def load(name):
    spec = importlib.util.spec_from_file_location(name, GFX / (name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


C = load("cues")
M = load("merge_bodies")

EDL = {"beats": {
    "2": {"voice_at": -13.35, "items": [["STILL", "frames-v2/tank-1.png", 11.45]]},
    "3": {"voice_at": 0, "items": [["STILL", "frames-v2/tank-2.png", 12.385], ["1.2a", 0, 7]]},
    "5": {"voice_at": 0, "items": [["1.4a", 0, 11], ["CARD", "GRAPHIC", "Four separate open seas, both teams in each", 4]]},
}}
PLAN = {"items": [{"track": "A1", "beat": 2, "at": 25.86, "in": 0.0}, {"track": "A1", "beat": 3, "at": 50.66, "in": 0.0},
                  {"track": "A1", "beat": 5, "at": 132.729, "in": 0.0}]}


def words(*pairs):
    return {"words": [{"text": t, "start": s, "end": s + 0.3} for t, s in pairs]}


ALIGNS = {2: words(("But", 13.35), ("before", 13.71), ("the", 14.11), ("sea,", 14.25), ("there", 14.81), ("tank.", 15.23)),
          3: words(("Each", 0.3), ("creature", 0.56), ("had", 1.06), ("a", 1.24), ("turn", 1.32), ("in", 1.68), ("the", 1.76), ("tank,", 1.88)),
          5: words(("into", 4.58), ("four", 4.90), ("separate", 5.45), ("open", 6.03), ("seas.", 6.34))}


def test_slots_from_the_edit_list_are_in_beat_audio_time():
    s = C.edl_slots(EDL)
    assert s["tank-1"] == {"beat": 2, "audio_in": 13.35, "audio_out": 24.8}
    assert s["tank-2"]["audio_in"] == 0.0 and s["tank-2"]["audio_out"] == 12.385
    assert s["seas-a"]["audio_in"] == 11.0 and s["seas-a"]["audio_out"] == 15.0


def test_frames_round_as_the_notes_count_them():
    out = C.build(EDL, PLAN, ALIGNS, proposed=False)
    assert out["slots"]["tank-1"]["frames"] == 344
    assert out["slots"]["tank-2"]["frames"] == 372


def test_proposed_slots_override_the_edit_list():
    out = C.build(EDL, PLAN, ALIGNS, proposed=True)
    assert out["slots"]["seas-a"]["audio_in"] == 4.40 and out["slots"]["seas-a"]["frames"] == 216
    assert out["slots"]["seas-a"]["source"] == "proposed"
    assert out["slots"]["seas-a"]["film_in"] == 137.129


def test_cue_times_are_slot_relative_and_come_from_the_words():
    out = C.build(EDL, PLAN, ALIGNS, proposed=True)
    assert out["cues"]["tank"]["t"] == 1.88 and out["cues"]["tank"]["frame"] == 56
    assert out["cues"]["each_creature"]["t"] == 0.3
    assert out["cues"]["four"]["t"] == 0.5          # 4.90 - 4.40


def test_a_scene_clock_runs_its_slots_back_to_back():
    out = C.build(EDL, PLAN, ALIGNS, proposed=True)
    tank = out["scenes"]["tank"]
    assert [r["slot"] for r in tank["slots"]] == ["tank-1", "tank-2"]
    assert tank["slots"][1]["frame_offset"] == 344 and tank["frames"] == 716
    assert abs(out["cues"]["each_creature"]["scene_t"] - (0.3 + 344 / 30)) < 1e-3


def test_a_cue_whose_words_are_absent_is_left_out():
    out = C.build(EDL, PLAN, {2: words(("Nothing", 13.4))}, proposed=False)
    assert "tank" not in out["cues"]


def test_merge_names_the_teams_and_the_founder():
    rows = [{"run": "p", "id": 13929, "snap": -1, "pool": 2, "parts": []}, {"run": "p", "id": 5, "snap": -1, "pool": 23, "parts": []},
            {"run": "p", "id": 6, "snap": -1, "pool": 50, "parts": []}]
    idx = {2: {"origin": "forager-kit-shaped", "ownEaten": "408.8", "id": "g2.13"},
           23: {"origin": "forager-kit-shaped", "ownEaten": "2449.9", "id": "g39.43"},
           50: {"origin": "forager-kit-range4-g40", "ownEaten": "411.3", "id": "g24.41"}}
    out = M.merge(rows, idx, [{"id": 166382, "parts": []}])
    assert sorted(out) == ["founder", "guz-23", "nib-50", "tp-166382"]
    assert out["nib-50"]["team"] == "nibbler" and out["guz-23"]["ownEaten"] == 2449.9


def test_the_real_edit_list_reproduces_the_plans_cue_table():
    edl, plan = C.SALVAGE + "/animatic/edl-v2.json", C.SALVAGE + "/animatic/plan-v2.json"
    if not (Path(edl).exists() and Path(plan).exists() and Path(C.SALVAGE + "/voice").exists()):
        return      # the salvage folder is local data; this machine does not have it
    out = C.load(edl, plan, C.SALVAGE + "/voice")
    want = {"tank": 1.88, "eat": 6.32, "saved": 6.42, "hungrier": 1.96, "small": 2.43, "family": 9.37, "savings": 12.40}
    for name, t in want.items():
        assert abs(out["cues"][name]["t"] - t) < 0.02, name
    assert [out["slots"][n]["frames"] for n in ("tank-1", "tank-2", "parents-1", "parents-2", "parents-3")] == [344, 372, 75, 117, 168]
    assert [out["slots"][n]["frames"] for n in ("seas-a", "winter-2", "founder", "seas-d")] == [216, 213, 411, 300]
    assert all(c["in_slot"] for c in out["cues"].values())
    assert len(out["cues"]) == len(C.CUES)
