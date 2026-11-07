"""film.py's snow setting reaches the theatre and the shots' keys.

On 2026-10-11 round 57's 4K film showed almost no snow near the bed, and the owner asked for the snow a touch
brighter and more of it, with water thin in snow still showing some. The theatre took three dials for it;
the film's settings name them, the job sets them for every theatre, a shot's key holds them so changing them
films the shots again, and collect refuses a clip whose theatre log does not show them.

  python -m pytest scripts/tests/story-video-check/test_snow_setting.py
"""

import importlib.util
from pathlib import Path

SKILL = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video"
spec = importlib.util.spec_from_file_location("film_tool", SKILL / "film.py")
film = importlib.util.module_from_spec(spec)
spec.loader.exec_module(film)

SHOT = {"id": "1.2a", "run": "r57-s2", "bodies": [], "camera": "Floor", "run_from_s": 46870.0, "run_to_s": 46874.0,
        "kind": "run"}
BASE = {"handles_s": 1, "fps": 30, "size": "1920x1080", "theatre_look": 4}

# The theatre's look line as TheatreLook.Describe writes it, cut to the parts the check reads.
LOG = ("[Theatre] underwater look: ...; march 48 steps at 0.03; shell gain 1.5; second batch: ..., "
       "run snow on (reference 1 J/m3, floor 2 J, dust 0.35, quantity x2, least 0.3, ambient 0.25), snow drift on (alive 0.25)")


def test_a_film_without_snow_keeps_its_keys():
    assert film.shot_key(SHOT, dict(BASE)) == film.shot_key(SHOT, dict(BASE, snow=None))


def test_setting_the_snow_changes_every_key_and_each_value_counts():
    plain = film.shot_key(SHOT, dict(BASE))
    a = film.shot_key(SHOT, dict(BASE, snow={"gain": 1.5, "quantity": 2, "least": 0.3}))
    b = film.shot_key(SHOT, dict(BASE, snow={"gain": 1.5, "quantity": 2, "least": 0.5}))
    assert len({plain, a, b}) == 3


def test_the_check_reads_each_dial_from_the_log():
    snow = {"gain": 1.5, "quantity": 2, "least": 0.3, "ambient": 0.25}
    assert all(__import__("re").search(p, LOG) for p in film.snow_patterns(snow))


def test_the_skin_at_its_defaults_keeps_the_keys():
    plain = film.shot_key(SHOT, dict(BASE))
    assert film.shot_key(SHOT, dict(BASE, glass=True, earth=True, caustics=1)) == plain
    assert film.shot_key(SHOT, dict(BASE, caustics=None)) == plain


def test_the_skin_changes_the_keys_and_sets_the_theatre():
    s = dict(BASE, glass=False, caustics=0.6)
    assert film.shot_key(SHOT, s) != film.shot_key(SHOT, dict(BASE))
    assert film.skin_env(s) == {"EVOSIM_THEATRE_GLASS": "0", "EVOSIM_THEATRE_CAUSTIC_SCALE": "0.6"}


def test_the_skin_check_reads_the_theatre_line():
    import re
    line = "[Theatre] skin: glass off, earth on, caustic scale x0.6"
    assert re.search(film.skin_pattern(dict(BASE, glass=False, caustics=0.6)), line)
    assert not re.search(film.skin_pattern(dict(BASE, glass=False, caustics=0.65)), line)
    assert not re.search(film.skin_pattern(dict(BASE, glass=False, earth=False, caustics=0.6)), line)


def test_the_check_refuses_a_log_with_another_value():
    import re
    # quantity 2 is not quantity 2.5, and gain 1 is not gain 1.5
    assert not all(re.search(p, LOG) for p in film.snow_patterns({"quantity": 2.5}))
    assert not all(re.search(p, LOG) for p in film.snow_patterns({"gain": 1}))
    assert not all(re.search(p, LOG.replace("least 0.3", "least 0")) for p in film.snow_patterns({"least": 0.3}))
    assert not all(re.search(p, LOG) for p in film.snow_patterns({"ambient": 0.5}))


def test_the_ambient_share_sets_the_theatre():
    a = film.shot_key(SHOT, dict(BASE, snow={"ambient": 0.1}))
    b = film.shot_key(SHOT, dict(BASE, snow={"ambient": 0.25}))
    assert a != b
    assert film.SNOW_ENV["ambient"] == "EVOSIM_THEATRE_SNOW_AMBIENT"
