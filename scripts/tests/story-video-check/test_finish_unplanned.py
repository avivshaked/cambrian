"""graphics.py's finish and check count only what the plan holds.

Round 57's shot list dropped three word cards in its fifth draft (5.1a-w1, 5.4b-w1, 6.7b-w1). Their records stayed in
graphics.json at the draft size, and finish failed on them after rendering every planned item at 4K (2026-10-10).
The edit takes only what the plan holds, so a record the plan no longer asks for is left out, and check names it.

  python -m pytest scripts/tests/story-video-check/test_finish_unplanned.py
"""

import importlib.util
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("graphics_tool", SKILL / "graphics.py")
graphics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(graphics)

PLAN = {"graphics": [{"id": "1.6a", "scene": "graphics/scenes/1.6a.py"}],"stills": [], "words": [{"id": "1.6a-w1"}]}


def records(planned_draft):
    return {"graphics": {"1.6a": {"draft": planned_draft}},
            "stills": {},
            "words": {"1.6a-w1": {"draft": planned_draft}, "5.1a-w1": {"draft": True}}}


def fake(monkeypatch, state):
    monkeypatch.setattr(graphics, "settings_of", lambda folder, lines: {"size": "3840x2160"})
    monkeypatch.setattr(graphics, "load_plan", lambda folder: PLAN)
    monkeypatch.setattr(graphics, "records_of", lambda folder: state["records"])
    monkeypatch.setattr(graphics, "stale", lambda folder, kind, item, recs: None)
    called = {"render": [], "words": 0}

    def render(folder, shot, final=False):
        called["render"].append(shot)
        state["records"]["graphics"][shot]["draft"] = False
        return "RENDER: PASS", []

    def words(folder, final=False):
        called["words"] += 1
        state["records"]["words"]["1.6a-w1"]["draft"] = False
        return "WORDS: PASS", []

    monkeypatch.setattr(graphics, "cmd_render", render)
    monkeypatch.setattr(graphics, "cmd_words", words)
    return called


def test_a_dropped_word_card_does_not_fail_the_finish(monkeypatch, tmp_path):
    state = {"records": records(True)}
    called = fake(monkeypatch, state)
    verdict, lines = graphics.cmd_finish(tmp_path)
    assert verdict == "FINISH: PASS", lines
    assert called == {"render": ["1.6a"], "words": 1}


def test_only_dropped_cards_left_at_the_draft_size_start_no_word_render(monkeypatch, tmp_path):
    state = {"records": records(False)}
    called = fake(monkeypatch, state)
    verdict, lines = graphics.cmd_finish(tmp_path)
    assert verdict == "FINISH: PASS", lines
    assert called == {"render": [], "words": 0}


def test_check_names_the_dropped_card_and_counts_no_draft(monkeypatch, tmp_path):
    state = {"records": records(False)}
    fake(monkeypatch, state)
    monkeypatch.setattr(graphics, "shots_of", lambda folder: None)
    monkeypatch.setattr(graphics, "build_plan", lambda folder, s, shots: PLAN)
    monkeypatch.setattr(graphics, "scene_draws", lambda path: ([], None))
    verdict, lines = graphics.cmd_check(tmp_path)
    assert verdict == "GRAPHICS: PASS", lines
    assert not any(x.startswith("drafts:") for x in lines), lines
    assert any(x.startswith("in graphics.json but not in the shot list") and "5.1a-w1" in x for x in lines), lines
