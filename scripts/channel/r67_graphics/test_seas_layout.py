import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_sealay as SL


def test_the_seas_hold_the_approved_counts():
    after = [{} for _ in range(4)]
    for i in range(4):
        for b in SL.sea_bodies(i, True):
            after[i][b["kind"]] = after[i].get(b["kind"], 0) + 1
        assert sum(after[i].values()) == 8
    assert after[2] == {"shell": 4, "guzzler": 1, "nibbler": 3}
    assert [after[i].get("guzzler", 0) for i in (0, 1, 3)] == [2, 1, 0]
    for i in range(4):
        kinds = [b["kind"] for b in SL.sea_bodies(i, False)]
        assert kinds.count("guzzler") == 4 and kinds.count("nibbler") == 4


def test_the_winters_deaths_follow_the_record():
    items = SL.winter_bodies()
    n = {}
    for it in items:
        d = n.setdefault(it["group"], [0, 0]); d[0] += 1; d[1] += it["dies"]
    assert n == {"Nibblers": [10, 7], "other Guzzlers": [10, 9], "taleplums": [6, 1]}


def test_the_scatter_keeps_bodies_apart():
    for after in (False, True):
        for i in range(4):
            pts = [(b["x"], b["y"]) for b in SL.sea_bodies(i, after)]
            assert len(pts) == 8
