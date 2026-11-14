"""Round 67's graphics slots and word cues, derived from the edit list and the narration's word alignment, so a
re-cut or a re-voice regenerates every timing.

    python -I cues.py [--edl FILE] [--plan FILE] [--voice DIR] [--out cues.json] [--print]

Inputs: edl-v2.json (per beat: voice_at, and items in order: STILL, CARD or clips), plan-v2.json (the A1 rows say
where each beat's audio starts in the film) and DIR/M3-beat-N.align.json (word start and end in the beat's audio).
Defaults are the salvage folder's (scratch/r67-salvage/).

Output (cues.json), in seconds, 30 fps, frame = round(t * 30), 0-based:
  slots[name]: beat, audio_in/audio_out (beat-audio seconds), film_in/film_out, frames, source ("edl" or "proposed")
  scenes[scene]: the slots played in order, each with its frame offset on the scene's own clock
  cues[name]: slot, t (seconds from the slot's first frame), frame, t_end, scene_t (from the scene's first frame),
              in_slot (false when the words fall outside the slot)

A scene's clock runs over its slots back to back (the winter's two slots are one scene of 513 frames), and the
scene scripts' --slot option picks one slot's range from it.
"""
import argparse, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
SALVAGE = os.path.join(REPO, "scratch", "r67-salvage")
FPS = 30

# A card's text decides which graphic it is; a STILL's file stem is the graphic (tank-1, bars-2, ...).
CARD_SLOTS = [("Four separate open seas", "seas-a"), ("Through the winter", "winter-1"),
              ("The tank's scores", "founder"), ("The four seas", "seas-d")]

# The coordinator's ruling of 2026-11-14: these slots play over the range of the beat's audio given here
# (beat, audio_in, audio_out). The coordinator moves them in the edit list when the graphics come in.
PROPOSED = {
    "seas-a": (5, 4.40, 11.60),
    "winter-1": (9, 4.70, 14.70),
    "winter-2": (9, 20.90, 28.00),
    "founder": (9, 50.50, 64.20),
    "seas-d": (10, 0.00, 10.00),
}

SCENES = {
    "tank": ["tank-1", "tank-2"],
    "parents": ["parents-1", "parents-2", "parents-3"],
    "bars": ["bars-1", "bars-2", "bars-3"],
    "founder": ["founder"],
    "seas-a": ["seas-a"],
    "seas-d": ["seas-d"],
    "winter": ["winter-1", "winter-2"],
}

# (slot, cue name, the words, which edge): the cue is the words' first start, or their last end with "end".
# Times in notes/cues.md of the build plan are what these reproduce.
CUES = [
    ("tank-1", "but_before", "but before the sea", "start"),
    ("tank-1", "tank", "tank", "start"),
    ("tank-1", "small_one", "a small one", "start"),
    ("tank-1", "small_one_end", "a small one", "end"),
    ("tank-1", "bred", "we bred the creatures there", "start"),
    ("tank-1", "roses", "the way a gardener breeds roses", "start"),
    ("tank-1", "roses_end", "the way a gardener breeds roses", "end"),
    ("tank-1", "faster", "only much much faster", "start"),
    ("tank-1", "faster_end", "only much much faster", "end"),
    ("tank-2", "each_creature", "each creature had a turn in the tank", "start"),
    ("tank-2", "question", "and the tank asked it exactly one question", "start"),
    ("tank-2", "eat", "how much did you eat", "start"),
    ("tank-2", "eat_end", "how much did you eat", "end"),
    ("tank-2", "nothing_measured", "nothing else was measured", "start"),
    ("tank-2", "nothing_counted", "nothing else counted", "start"),
    ("parents-1", "best", "the best eaters became parents", "start"),
    ("parents-1", "best_word", "best", "start"),
    ("parents-1", "parents_word", "parents", "start"),
    ("parents-2", "children", "their children were copies of them", "start"),
    ("parents-2", "small", "small", "start"),
    ("parents-2", "mistake", "mistake", "start"),
    ("parents-3", "round_after_round", "and round after round", "start"),
    ("parents-3", "eating", "the creatures got better at eating", "start"),
    ("parents-3", "eating_word", "eating", "start"),
    ("bars-1", "nibblers", "the nibblers", "start"),
    ("bars-1", "ate_little", "ate little", "start"),
    ("bars-1", "spent_less", "and spent even less than they ate", "start"),
    ("bars-1", "saved_sentence", "a nibbler usually finished its turn with something saved", "start"),
    ("bars-1", "saved", "saved", "start"),
    ("bars-2", "guzzlers", "the guzzlers", "start"),
    ("bars-2", "four_times", "ate four times as much as the nibblers", "start"),
    ("bars-2", "spent_more", "and they spent more than they ate", "start"),
    ("bars-3", "hungrier_sentence", "on average a guzzler ended its turn hungrier", "start"),
    ("bars-3", "hungrier", "hungrier", "start"),
    ("seas-a", "four_seas", "into four separate open seas", "start"),
    ("seas-a", "four", "four", "start"),
    ("seas-a", "both_teams", "every sea got both teams", "start"),
    ("seas-a", "this_film", "this film follows one of the four", "start"),
    ("winter-1", "killed_sentence", "it killed most of the nibblers", "start"),
    ("winter-1", "killed", "killed", "start"),
    ("winter-1", "third_as_much", "with a third as much to eat", "start"),
    ("winter-1", "winters", "winters it turns out do not", "start"),
    ("winter-2", "other_guzzlers", "of the other guzzlers nearly nine in every ten died", "start"),
    ("winter-2", "taleplums_lost", "the taleplums lost only one in every six", "start"),
    ("winter-2", "lost_only_one", "lost only one", "start"),
    ("founder", "back_in_tank", "back in the tank the founder", "start"),
    ("founder", "founder_word", "founder", "start"),
    ("founder", "smallest", "one of the smallest eaters among the guzzlers sent out", "start"),
    ("founder", "measured_only", "and the tank which measured only eating", "start"),
    ("founder", "rated_low", "rated it low", "start"),
    ("founder", "but_in_tank", "but in the tank", "start"),
    ("founder", "founder_usually", "the founder usually ended its turn", "start"),
    ("founder", "with_savings", "with savings", "start"),
    ("founder", "savings", "savings", "start"),
    ("seas-d", "other_three", "and in the other three seas the guzzlers lost", "start"),
    ("seas-d", "this_sea", "so what happened in this sea", "start"),
    ("seas-d", "not_a_win", "was not a win for the guzzlers", "start"),
    ("seas-d", "win_one_family", "it was a win for one family", "start"),
    ("seas-d", "family", "family", "start"),
]


def norm(text):
    return re.sub(r"[^a-z0-9']+", "", text.lower())


def frames_of(seconds):
    return int(round(seconds * FPS))


def slot_name_of(item):
    if item[0] == "STILL":
        return os.path.splitext(os.path.basename(item[1]))[0]
    if item[0] == "CARD" and item[1] == "GRAPHIC":
        for key, name in CARD_SLOTS:
            if item[2].startswith(key):
                return name
    return None


def item_length(item):
    if item[0] in ("STILL", "CARD"):
        return float(item[-1])
    return float(item[2]) - float(item[1])   # a clip: [take, in, out]


def edl_slots(edl):
    """Every graphic slot of the edit list, in beat-audio seconds: a picture second t of beat b is audio second
    t - voice_at, since voice_at is where the beat's voice starts against its first picture."""
    out = {}
    for beat, spec in sorted(edl["beats"].items(), key=lambda kv: int(kv[0])):
        t = 0.0
        for item in spec["items"]:
            length = item_length(item)
            name = slot_name_of(item)
            if name:
                out[name] = {"beat": int(beat), "audio_in": round(t - spec["voice_at"], 3),
                             "audio_out": round(t - spec["voice_at"] + length, 3)}
            t += length
    return out


def beat_starts(plan):
    """Where each beat's audio starts in the film, from the plan's A1 rows."""
    starts = {}
    for row in plan["items"]:
        if row.get("track") == "A1":
            starts[int(row["beat"])] = float(row["at"]) - float(row.get("in", 0.0))
    return starts


def find_words(words, phrase, after):
    """The first run of words, at or after `after` seconds, whose text is the phrase; its (start, end)."""
    want = [norm(w) for w in phrase.split()]
    toks = [norm(w["text"]) for w in words]
    for i in range(len(words) - len(want) + 1):
        if words[i]["start"] < after:
            continue
        if toks[i:i + len(want)] == want:
            return words[i]["start"], words[i + len(want) - 1]["end"]
    return None


def build(edl, plan, aligns, proposed=True):
    """aligns: {beat number: the beat's align.json contents}."""
    slots = edl_slots(edl)
    for name, (beat, a_in, a_out) in (PROPOSED.items() if proposed else ()):
        slots[name] = {"beat": beat, "audio_in": a_in, "audio_out": a_out, "source": "proposed"}
    starts = beat_starts(plan)
    for s in slots.values():
        s.setdefault("source", "edl")
        s["length"] = round(s["audio_out"] - s["audio_in"], 3)
        s["frames"] = frames_of(s["length"])
        if s["beat"] in starts:
            s["film_in"] = round(starts[s["beat"]] + s["audio_in"], 3)
            s["film_out"] = round(starts[s["beat"]] + s["audio_out"], 3)
    scenes = {}
    for scene, names in SCENES.items():
        offset, rows = 0, []
        for n in names:
            if n not in slots:
                continue
            rows.append({"slot": n, "frame_offset": offset})
            slots[n]["scene"], slots[n]["scene_frame_offset"] = scene, offset
            offset += slots[n]["frames"]
        scenes[scene] = {"slots": rows, "frames": offset}
    cues = {}
    for slot, name, phrase, edge in CUES:
        s = slots.get(slot)
        if s is None or s["beat"] not in aligns:
            continue
        hit = find_words(aligns[s["beat"]]["words"], phrase, s["audio_in"] - 0.05)
        if hit is None:
            continue
        a_start, a_end = hit
        t = (a_end if edge == "end" else a_start) - s["audio_in"]
        cues[name] = {"slot": slot, "t": round(t, 3), "frame": frames_of(t), "t_end": round(a_end - s["audio_in"], 3),
                      "scene_t": round(t + s["scene_frame_offset"] / FPS, 3),
                      "in_slot": -0.05 <= t <= s["length"] + 0.05}
    return {"fps": FPS, "proposed": proposed, "slots": slots, "scenes": scenes, "cues": cues}


def load(edl_path, plan_path, voice_dir, proposed=True):
    edl = json.load(open(edl_path, encoding="utf-8"))
    plan = json.load(open(plan_path, encoding="utf-8"))
    aligns = {}
    for beat in edl["beats"]:
        p = os.path.join(voice_dir, "M3-beat-%s.align.json" % beat)
        if os.path.isfile(p):
            aligns[int(beat)] = json.load(open(p, encoding="utf-8"))
    return build(edl, plan, aligns, proposed)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--edl", default=os.path.join(SALVAGE, "animatic", "edl-v2.json"))
    ap.add_argument("--plan", default=os.path.join(SALVAGE, "animatic", "plan-v2.json"))
    ap.add_argument("--voice", default=os.path.join(SALVAGE, "voice"))
    ap.add_argument("--out", default=os.path.join(HERE, "cues.json"))
    ap.add_argument("--edl-slots", action="store_true", help="keep the edit list's slots, not the proposed ones")
    ap.add_argument("--print", action="store_true")
    a = ap.parse_args(argv)
    for p in (a.edl, a.plan, a.voice):
        if not os.path.exists(p):
            sys.exit("cues: missing input %s (the round's salvage folder; pass --edl, --plan, --voice)" % p)
    data = load(a.edl, a.plan, a.voice, proposed=not a.edl_slots)
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    if a.print:
        for n, s in data["slots"].items():
            print("%-10s beat %2d  %7.3f-%7.3f  %4d frames  %s" % (n, s["beat"], s["audio_in"], s["audio_out"], s["frames"], s["source"]))
        for n, c in data["cues"].items():
            print("%-18s %-10s t %6.2f  frame %4d%s" % (n, c["slot"], c["t"], c["frame"], "" if c["in_slot"] else "  (outside the slot)"))
    print("wrote %s: %d slots, %d cues" % (a.out, len(data["slots"]), len(data["cues"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
