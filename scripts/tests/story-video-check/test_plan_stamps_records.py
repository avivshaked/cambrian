"""graphics.py's plan stamps graphics.json with the shot list when nothing is left to draw.

Only a render stamped graphics.json with the shot list's sha256. Round 57's shot list went to draft 7 for one
footage shot (6.9a), no graphic changed, nothing was drawn, and the edit refused graphics.json as "made for
another shot list" (2026-10-11).

  python -m pytest scripts/tests/story-video-check/test_plan_stamps_records.py
"""

import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SKILL = ROOT / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("graphics_tool", SKILL / "graphics.py")
graphics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(graphics)

FOLDER = ROOT / "scratch" / "tests" / "story-video-check" / "plan-stamps"
PLAN = {"graphics": [], "stills": [], "words": [{"id": "1.6a-w1", "words": "12", "seconds": 4.0}]}


def folder_with(stale_why):
    FOLDER.mkdir(parents=True, exist_ok=True)
    (FOLDER / "shots.json").write_text('{"shots": ["draft 7"]}', encoding="utf-8")
    (FOLDER / "graphics.json").write_text(json.dumps({"schema": graphics.SCHEMA, "graphics": {}, "stills": {},
                                                      "words": {"1.6a-w1": {"key": "k"}},
                                                      "shots_sha256": "draft 6's"}), encoding="utf-8")
    return FOLDER


def fake(monkeypatch, why):
    monkeypatch.setattr(graphics, "settings_of", lambda folder, lines: {})
    monkeypatch.setattr(graphics, "shots_of", lambda folder: None)
    monkeypatch.setattr(graphics, "build_plan", lambda folder, s, shots: PLAN)
    monkeypatch.setattr(graphics, "stale", lambda folder, kind, item, records: why)


def test_nothing_to_draw_stamps_the_records(monkeypatch):
    folder = folder_with(None)
    fake(monkeypatch, None)
    verdict, lines = graphics.cmd_plan(folder)
    assert verdict == "GRAPHICS PLAN: NOTHING TO DRAW", lines
    saved = json.loads((folder / "graphics.json").read_text(encoding="utf-8"))
    assert saved["shots_sha256"] == graphics.sha256_of(folder / "shots.json")
    assert saved["words"] == {"1.6a-w1": {"key": "k"}}


def test_something_to_draw_leaves_the_records_alone(monkeypatch):
    folder = folder_with("drawn for another shot list or settings")
    fake(monkeypatch, "drawn for another shot list or settings")
    verdict, lines = graphics.cmd_plan(folder)
    assert verdict == "GRAPHICS PLAN: READY", lines
    saved = json.loads((folder / "graphics.json").read_text(encoding="utf-8"))
    assert saved["shots_sha256"] == "draft 6's"
