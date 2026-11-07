"""graphics.py pins each Blender render to the settings' render_cores at below-normal priority.

The machine's coordinator gave the round 57 film's graphics their go on the efficiency cores, logical 24 to 31,
beside a filming pass (2026-10-10). The animators start Blender through graphics.py, so the tool does the pinning.

  python -m pytest scripts/tests/story-video-check/test_render_cores.py
"""

import importlib.util
import os
import subprocess
import sys
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("graphics_tool", SKILL / "graphics.py")
graphics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(graphics)


def test_a_list_of_cores_is_a_mask():
    assert graphics.cores_mask("24-31") == 0xFF000000
    assert graphics.cores_mask("0-3,8") == 0x10F
    assert graphics.cores_mask(None) is None


def test_a_process_is_pinned_and_lowered():
    if os.name != "nt":
        return
    proc = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(20)"])
    try:
        graphics.pin(proc.pid, "24-31")
        read = subprocess.run(["pwsh", "-NoProfile", "-Command",
                               f"$p = Get-Process -Id {proc.pid}; \"$([int64]$p.ProcessorAffinity) $($p.PriorityClass)\""],
                              capture_output=True, text=True).stdout.split()
        assert read == [str(0xFF000000), "BelowNormal"], read
    finally:
        proc.kill()
