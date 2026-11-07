"""film.py's plan reuses the windows an earlier pass of the same film recorded, so a re-render records nothing.

The owner asked on 2026-10-11 that a pass changing only the render (the snow, the glass, the caustics, a camera)
not record its windows again: the farm's recordings hold the bodies, and nothing in the render changes them.

  python -m pytest scripts/tests/story-video-check/test_reuse_windows.py
"""

import importlib.util
import json
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

RUN = "D:/runs/r57-s2/2026-10-06-065827-dfc0e881"


def recorded(folder, k, name, frm, to, fps=30, run="2026-10-06-065827-dfc0e881", finished=True):
    d = folder / "film" / f"pass-{k}" / "windows" / name
    d.mkdir(parents=True)
    (d / "film.poses.bin").write_bytes(b"poses")
    rows = ['{"frame": 0}']
    if finished:
        rows.append(json.dumps({"verdict": "faithful", "run": run, "from": frm, "to": to, "fps": fps}))
    (d / "identity.jsonl").write_text("\n".join(rows) + "\n", encoding="utf-8")
    return d


def plan(*windows):
    return {"windows": [{"scene": i + 1, "part": "main", "run": RUN, "from": f, "to": t, "fps": 30.0,
                         "out": f"story-{i + 1:02d}-r57-s2-main"} for i, (f, t) in enumerate(windows)]}


def test_a_window_recorded_over_the_same_span_is_linked_under_the_new_name(tmp_path):
    recorded(tmp_path, 2, "story-07-r57-s2-main", 46867, 46880.36)
    assert film.reuse_windows(tmp_path, 8, plan((46867.0, 46880.36))) == 1
    linked = tmp_path / "film" / "pass-8" / "windows" / "story-01-r57-s2-main"
    assert (linked / "film.poses.bin").read_bytes() == b"poses"
    assert film.window_verdict(linked)["from"] == 46867


def test_another_span_another_run_or_an_unfinished_window_is_recorded_again(tmp_path):
    recorded(tmp_path, 2, "story-01-r57-s2-main", 46867, 46880.0)                        # another end
    recorded(tmp_path, 3, "story-01-r57-s2-main", 46867, 46880.36, run="2026-10-01-x")   # another run
    recorded(tmp_path, 4, "story-01-r57-s2-main", 46867, 46880.36, finished=False)       # the farm stopped
    recorded(tmp_path, 5, "story-01-r57-s2-main", 46867, 46880.36, fps=24)               # another frame rate
    assert film.reuse_windows(tmp_path, 8, plan((46867.0, 46880.36))) == 0
    assert not (tmp_path / "film" / "pass-8" / "windows" / "story-01-r57-s2-main").exists()


def test_a_pass_never_reuses_its_own_windows_and_keeps_what_it_holds(tmp_path):
    own = recorded(tmp_path, 8, "story-01-r57-s2-main", 46867, 46880.36)
    recorded(tmp_path, 2, "story-09-r57-s2-main", 46867, 46880.36)
    assert film.reuse_windows(tmp_path, 8, plan((46867.0, 46880.36))) == 0
    assert (own / "film.poses.bin").read_bytes() == b"poses"


if __name__ == "__main__":
    import tempfile
    for name, f in list(globals().items()):
        if name.startswith("test_"):
            with tempfile.TemporaryDirectory(dir=Path(__file__).parent) as d:
                f(Path(d))
            print("ok", name)
