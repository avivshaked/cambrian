"""Fast tests of scripts/reads/r62-health.py's two measures, on a hand-made world. Run: python -I test_health.py"""
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
READS = os.path.normpath(os.path.join(HERE, "..", "..", "reads"))
sys.path.insert(0, READS)
spec = importlib.util.spec_from_file_location("r62health", os.path.join(READS, "r62-health.py"))
h = importlib.util.module_from_spec(spec)
spec.loader.exec_module(h)
M = h.r61.r59.MOUTHED


class W:
    def __init__(self, born, deaths):
        self.born, self.deaths, self.name = born, deaths, "fake"


class E:
    """The members of r61-grazer-economy's Economy the two measures read."""

    def __init__(self, born, deaths, kills, stats, end=24000.0):
        self.w = W(born, deaths)
        self.kill_j, self.stats, self.end = kills, stats, end
        self.jpu, self.waste = 100.0, 0.2

    def mouthed(self, i):
        b = self.w.born.get(i)
        return bool(b and b[2] & M)

    def stat_at(self, t):
        best = None
        for s in self.stats:
            if s["t"] <= t + 1e-9:
                best = s
        return best


def world():
    born = {
        1: (0.0, -1, M),      # a founder, alive throughout
        2: (100.0, 1, M),     # born of 1, dies at 13000
        3: (200.0, 99, M),    # its parent is not in the record: founded
        4: (300.0, 1, 0),     # unmouthed: not a grazer
        5: (12500.0, 2, M),   # born after 12000
        6: (50.0, 4, M),      # mouthed child of an unmouthed parent: born in the world
    }
    deaths = {2: (13000.0,), 6: (11000.0,)}
    kills = [(12100.0, 4, 1, 10.0), (14000.0, 4, 2, 5.0), (16000.0, 4, 4, 100.0), (17000.0, 4, None, 7.0)]
    stats = [{"t": 12000.0, "unitsEaten": 10.0}, {"t": 15000.0, "unitsEaten": 11.0}, {"t": 18000.0, "unitsEaten": 13.0}]
    return E(born, deaths, kills, stats, end=18000.0)


def test_born_share():
    e = world()
    assert h.born_share(e, 12000.0) == (3, 2, 1), h.born_share(e, 12000.0)   # 1, 2, 3 living; 1 and 3 founded
    assert h.born_share(e, 14000.0) == (3, 2, 1), h.born_share(e, 14000.0)   # 2 dead, 5 born
    assert h.born_share(e, 11000.0) == (3, 2, 1)                             # 6 died at 11000 exactly


def test_own_kill_bins():
    e = world()
    rows = h.own_kill_bins(e, 12000.0, e.end, 3000.0)
    assert [(r[0], r[1]) for r in rows] == [(12000.0, 15000.0), (15000.0, 18000.0)]
    t0, t1, kills, kill_j, intake_j, share, kgs, gs = rows[0]
    assert kills == 2 and abs(kill_j - 15.0 * 0.8) < 1e-9          # grazers 1 and 2; not the unmouthed 4
    assert abs(intake_j - 1.0 * 100.0 * 0.8) < 1e-9
    assert abs(share - 12.0 / 80.0) < 1e-12
    assert abs(gs - (3000.0 + 1000.0 + 3000.0 + 2500.0)) < 1e-9      # 1, 2 to 13000, 3, 5 from 12500
    t0, t1, kills, kill_j, intake_j, share, kgs, gs = rows[1]
    assert kills == 0 and kill_j == 0.0 and abs(intake_j - 160.0) < 1e-9 and share == 0.0


if __name__ == "__main__":
    test_born_share()
    test_own_kill_bins()
    print("r62-health: 2 tests passed")
