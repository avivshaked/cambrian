"""collect's window drops check: a film window that left bodies undrawn at its first frame fails its take.

Round 67's pass 21 drew no gripping plant in any plant window, because the theatre developed bodies without the run's
cell table; the only sign was one unread warning. From a4ee1687 every film window writes one drops line at its first
frame, and collect reads it for the scene whose clip starts at that second.

  python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_window_drops.py
"""

import importlib.util
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

FAULT = ("[Theatre] window drops at the first frame: 31 of 670 bodies at 1501.0 s; by reason no-genome-row 29, "
         "remains 2; by flags 20 29 (bodies not drawn)")
CLEAN = ("[Theatre] window drops at the first frame: 3 of 670 bodies at 7501.0 s; by reason remains 2, place-refused 1; "
         "by flags 8 2")
NONE = "[Theatre] window drops at the first frame: 0 of 412 bodies at 20418.0 s; by reason none; by flags none"


def test_a_fault_reason_fails_the_window_at_its_second():
    fault, note = film.drops_log_fault(FAULT + "\n" + CLEAN, 1501.0)
    assert fault is not None and "1501.0 s" in fault and "no-genome-row 29" in fault and "remains" not in fault
    assert note is None


def test_remains_and_place_refused_are_not_faults():
    assert film.drops_log_fault(FAULT + "\n" + CLEAN, 7501.0) == (None, None)
    assert film.drops_log_fault(NONE, 20418.0) == (None, None)


def test_a_log_without_the_line_is_unchecked_not_passed():
    fault, note = film.drops_log_fault("take 1 of story-02-plf-6202-main ended: 400 frames", 1501.0)
    assert fault is None and "unchecked" in note


def test_another_windows_fault_does_not_fail_this_one():
    fault, note = film.drops_log_fault(FAULT, 7501.0)
    assert fault is None and "unchecked" in note
