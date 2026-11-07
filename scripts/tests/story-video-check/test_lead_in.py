"""film.py opens a shot of a named body no earlier than two metabolic steps after the body's birth.

Round 57's look test filmed the founder's portrait (shot 1.3a, body 11630, born at 5,177.5 s) with a 1 s handle
before a stretch from 5,178 s. Its window opened at 5,177 s, before the body existed, and the director missed the
scene: "the clade of body 11630 has no member alive in this film window". The 4K pass then opened it at 5,178 s,
one step after the birth row, and missed it again: the body is first drawn at 5,178.04 s, the frame after the
step that placed it. The handle is now cut two steps after the birth.

  python -m pytest scripts/tests/story-video-check/test_lead_in.py
"""

import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SKILL = ROOT / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

RUN = ROOT / "scratch" / "tests" / "story-video-check" / "lead-in-run"


def run_with(rows):
    RUN.mkdir(parents=True, exist_ok=True)
    (RUN / "lineage.jsonl").write_text("".join(json.dumps(r, separators=(",", ":")) + "\n" for r in rows),
                                       encoding="utf-8")
    return RUN


def shot(a, bodies, camera="Portrait"):
    return {"run": "r-test", "bodies": bodies, "camera": camera, "run_from_s": a, "run_to_s": a + 4.0}


ROWS = [{"e": "b", "t": 100.0, "id": 7, "p": -1}, {"e": "b", "t": 5177.5, "id": 11630, "p": -1},
        {"e": "b", "t": 5180.0, "id": 116300, "p": 11630}]


def test_a_handle_before_the_birth_is_cut_to_two_steps_after_it():
    # Shot 6.8b: a stretch from 5,179.6 s with a 3 s handle opens at 5,178.5 s, after the body is first drawn.
    start, why = film.lead_in(run_with(ROWS), shot(5179.6, ["11630"]), 3.0)
    assert why is None and abs(start - 5178.5) < 1e-9


def test_a_stretch_one_step_after_the_birth_is_refused():
    # Shot 1.3a in the 4K pass: a window at 5,178.0 s holds no body 11630, which is first drawn at 5,178.04 s.
    start, why = film.lead_in(run_with(ROWS), shot(5178.0, ["11630"]), 0.0)
    assert why is not None and "at 5178.5 s" in why


def test_a_handle_after_the_birth_is_left_whole():
    start, why = film.lead_in(run_with(ROWS), shot(5192.0, ["11630"]), 3.0)
    assert why is None and abs(start - 5189.0) < 1e-9


def test_a_stretch_that_starts_before_the_birth_is_refused():
    start, why = film.lead_in(run_with(ROWS), shot(5177.0, ["11630"]), 3.0)
    assert why is not None and "born at 5177.5" in why


def test_the_id_is_matched_whole_not_as_a_prefix():
    # 116300's row must not be taken for 11630's, nor 11630's parent field for a birth of 11630.
    assert film.born_at(run_with(ROWS), "11630") == 5177.5
    assert film.born_at(run_with(ROWS), "1163") is None


def test_a_shot_of_no_body_keeps_its_handle():
    start, why = film.lead_in(run_with(ROWS), shot(5178.0, [], camera="Floor"), 3.0)
    assert why is None and abs(start - 5175.0) < 1e-9
