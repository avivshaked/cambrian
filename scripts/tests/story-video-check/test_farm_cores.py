"""film.py pins the farm's window recordings to the settings' farm_cores, and their children inherit the pin.

On 2026-10-10 the farm and the theatre, both on logical 0-15, took the CPU to 92 °C, and the machine's coordinator
moved the farm to the efficiency cores, 16-31. film.py starts the recorder pinned, so each farm it starts is too.

  python -m pytest scripts/tests/story-video-check/test_farm_cores.py
"""

import importlib.util
import os
import subprocess
import sys
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)


def test_a_list_of_cores_is_a_mask():
    assert film.cores_mask("16-31") == 0xFFFF0000
    assert film.cores_mask("0-3,8") == 0x10F
    assert film.cores_mask(None) is None


def test_a_launched_child_and_its_own_child_carry_the_pin():
    if os.name != "nt":
        return
    # The child sleeps a moment so the pin lands before it starts its own child, as pwsh does before the farm.
    child = ("import subprocess, sys, time; time.sleep(0.5); "
             "print(subprocess.run(['pwsh', '-NoProfile', '-Command', '[int64](Get-Process -Id $PID).ProcessorAffinity'], "
             "capture_output=True, text=True).stdout.strip())")
    out = subprocess.run([sys.executable, "-c",
                          "import importlib.util, sys; spec = importlib.util.spec_from_file_location('f', sys.argv[1]); "
                          "f = importlib.util.module_from_spec(spec); spec.loader.exec_module(f); "
                          f"sys.exit(f.launch([sys.executable, '-c', {child!r}], cores='16-31'))",
                          str(SKILL / "film.py")], capture_output=True, text=True)
    assert out.stdout.strip() == str(0xFFFF0000), out.stdout + out.stderr
