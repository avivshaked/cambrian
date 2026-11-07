"""shots.py refuses a shot that plays into the last second of its run, which filming cannot record.

Round 57's first shot list passed its check with three shots ending at 48,000 s, the run's last second, and
filming's plan refused them: story-windows.py records 1 s past a stretch's end (film.py's TAIL_S). The check now
holds a stretch to end by the run's last second less that.

  python -m pytest scripts/tests/story-video-check/test_run_end.py
"""

import importlib.util
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("shots_tool", SKILL / "shots.py")
shots = importlib.util.module_from_spec(spec)
spec.loader.exec_module(shots)


def errors_for(run_to_s, end=48000.0):
    shots._ENDS["r-test"] = end
    shot = {"run": "r-test", "bodies": [], "camera": "Floor", "run_from_s": run_to_s - 4.0, "run_to_s": run_to_s,
            "from_s": 10.0, "to_s": 14.0}
    errors = []
    shots.check_footage(shot, "shot 1.1a", "the run r-test", errors)
    return errors


def test_a_stretch_into_the_runs_last_second_is_refused():
    assert any("ends by 47999" in e for e in errors_for(48000.0))


def test_a_stretch_that_leaves_the_tail_passes():
    assert errors_for(47999.0) == []


def test_a_run_not_on_this_machine_is_left_to_filming():
    shots._ENDS["r-test"] = None
    shot = {"run": "r-test", "bodies": [], "camera": "Floor", "run_from_s": 47996.0, "run_to_s": 48000.0,
            "from_s": 10.0, "to_s": 14.0}
    errors = []
    shots.check_footage(shot, "shot 1.1a", "the run r-test", errors)
    assert errors == []
