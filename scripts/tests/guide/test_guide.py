"""guide.py counts the living and names pool founders, and calls no unknown source random.

Since D220 a positions sample holds a row for each body's remains as well as for each living body,
flagged with bit 8 (PositionsRow.RemainsBit). The guide counted them all, so round 67's last sample
read 28,050 bodies where stats.jsonl had 24,988 alive. And a founder whose lineage row says `src`
`pool` came from the nursery's pool (the run's pool/index.tsv maps its pool index to a genome and a
nursery run), yet the card called it a random founder with no parent. The fixtures here are built in
a tmp_path, because runs are gitignored.

  python -I -m pytest scripts/tests/guide/test_guide.py
"""

import importlib.util
import json
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("guide_tool", SCRIPTS / "guide.py")
guide = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guide)

EATER, LEAF, REMAINS = 1, 4, 8


def write_run(tmp_path, samples):
    """A record format 1 run directory holding only positions.jsonl; a sample is (t, [rows])."""
    with open(tmp_path / "positions.jsonl", "w", encoding="utf-8", newline="\n") as f:
        for t, rows in samples:
            f.write(json.dumps({"t": t, "n": len(rows), "b": rows}) + "\n")
    return str(tmp_path)


def read(run_dir, clade_of, n_clades):
    return guide.read_positions(run_dir, clade_of, n_clades, None, None)


def test_remains_are_left_out_of_a_samples_count_and_a_clades_count(tmp_path):
    # Clade 0 holds bodies 1 to 3 and clade 1 holds body 4. At 100 s body 2 and body 4 have died
    # and left remains, which keep the body's id and gain bit 8.
    run = write_run(tmp_path, [
        (50.0, [[1, 5.0, -2.0, 5.0, LEAF], [2, 6.0, -2.0, 5.0, LEAF], [3, 7.0, -2.0, 5.0, LEAF],
                [4, 8.0, -4.0, 5.0, EATER]]),
        (100.0, [[1, 5.0, -2.0, 5.0, LEAF], [3, 7.0, -2.0, 5.0, LEAF],
                 [2, 6.0, -9.0, 5.0, LEAF | REMAINS], [4, 8.0, -9.0, 5.0, EATER | REMAINS]]),
    ])
    pos = read(run, {1: 0, 2: 0, 3: 0, 4: 1}, 2)
    assert pos["samples"] == [(50.0, 4), (100.0, 2)]
    assert pos["last_counts"] == {0: 2}
    assert pos["peak"] == [3, 1]
    assert pos["peak_living"] == [4, 4]
    assert pos["series"][0] == [(50.0, 3), (100.0, 2)]
    assert 1 not in dict(pos["series"][1][1:])
    assert pos["last_seen"][1] == 50.0
    # The remains lie at 9 m and no depth, place or share is read from them.
    assert sorted(pos["depth_h"][0]) == [20]
    assert pos["unknown_ids"] == 0


def test_a_run_with_no_remains_bit_counts_as_it_did(tmp_path):
    rows = [[1, 5.0, -2.0, 5.0, LEAF], [2, 6.0, -2.5, 5.0, EATER | 2], [3, 7.0, -3.0, 5.0, 0]]
    run = write_run(tmp_path, [(50.0, rows), (100.0, rows[:2])])
    pos = read(run, {1: 0, 2: 0, 3: 1}, 2)
    assert pos["samples"] == [(50.0, 3), (100.0, 2)]
    assert pos["last_counts"] == {0: 2}
    assert pos["peak"] == [2, 1]
    assert pos["peak_living"] == [3, 3]
    assert guide.living_bodies(rows) == rows
    assert guide.living_bodies([[1, 0.0, 0.0, 0.0]]) == [[1, 0.0, 0.0, 0.0]]  # a row with no flags cell


def pool_run(tmp_path, with_exam=False):
    (tmp_path / "pool").mkdir()
    header = "pool\tweight\torigin\trun\tid\tkind\townEaten" + ("\texam" if with_exam else "")
    rows = ["0\t45\tkit-a\tkit-a\tg0.22\tforager\t243.8", "2\t45\tkit-b\tkit-b\tg2.13\tforager\t408.8"]
    if with_exam:
        rows = [r + "\tthe forager exam" for r in rows]
    (tmp_path / "pool" / "index.tsv").write_text("\n".join([header] + rows) + "\n", encoding="utf-8")
    return str(tmp_path)


def test_a_pool_founder_is_named_with_its_genome_and_nursery_run(tmp_path):
    index = guide.read_pool_index(pool_run(tmp_path))
    assert index[2] == {"genome": "g2.13", "run": "kit-b", "exam": None}
    how = guide.founder_how("pool", index[2])
    assert "nursery pool" in how and "g2.13" in how and "kit-b" in how
    assert "random" not in how
    card = {"kind": "founder", "founder": 13929, "founded_at": 11447.0, "founder_source": "pool",
            "founder_pool": index[2], "split": None}
    sentence = " ".join(guide.split_sentences(card, {}))
    assert "g2.13" in sentence and "kit-b" in sentence and "random" not in sentence
    fact = guide.founder_fact_how("pool", index[2])
    assert "g2.13" in fact and "kit-b" in fact and "random" not in fact


def test_a_pool_founders_exam_is_named_only_when_the_index_says_it(tmp_path):
    index = guide.read_pool_index(pool_run(tmp_path, with_exam=True))
    assert "whose exam was the forager exam" in guide.founder_how("pool", index[2])
    assert "exam" not in guide.founder_how("pool", {"genome": "g2.13", "run": "kit-b", "exam": None})


def test_a_pool_founder_the_index_does_not_hold_says_so(tmp_path):
    assert guide.read_pool_index(str(tmp_path)) == {}
    how = guide.founder_how("pool", None)
    assert "nursery pool" in how and "random" not in how


def test_an_unknown_source_is_not_called_random():
    for source in (None, "", "somewhere"):
        for how in (guide.founder_how(source, None), guide.founder_fact_how(source, None)):
            assert "random" not in how
    assert "does not record" in guide.founder_how(None, None)
    assert "somewhere" in guide.founder_how("somewhere", None)


def test_the_floor_and_the_trickle_keep_their_wording():
    assert guide.founder_how("floor", None) == guide.FOUNDER_SOURCES["floor"]
    assert guide.founder_how("trickle", None).startswith("a random founder with no parent")
