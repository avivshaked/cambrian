# The story writer's brief, version 2

*Written 2026-09-25 for the writer of a round's story film, after the owner watched round 48's
first film. It replaces the brief the first film was written from. The procedure around it (the
guides, the check, the render, the join) is `logbook/specs/story-film.md`. The words a film may use
are in the glossary beside this brief, [`story-glossary.md`](story-glossary.md).
It was revised the same day, after the owner asked why the captions looked so bad and why the
explanations played on a black screen. The captions are now subtitles set at the join, and a Card
is moving footage of the world.*

## What the film is for

A story film tells one round of the simulator to someone who has never seen it: a friend of the
owner, or the owner a month from now. After the first film the owner asked for four things, in
these words:

1. "we need to explain the world, and when we use a term we need to explain it. what is a
   stomach? what is a line? what does it take to have a child? what does it take to die.
   consider that anything that is obvious to you, is not obvious to anyone unfamiliar with our
   world."
2. "can we introduce graphs in unity? it would be great to be able to show some stats when
   introducing a creature, or introducing a concept."
3. The picture was too dark. That one is the theatre's work.
4. "when telling a story, we want a story, you know? i don't mean to start with 'once upon a
   time', but we do want to explain in a way a story of triumph or tragedy or something in
   between - bitter sweet."

Every rule below serves one of those four, or the older rule that nothing on screen is false.

## What you are given, and what you may touch

- The round's logbook entry and its pre-registration: what the round asked and what it predicted.
- The runs, read-only: `lineage.jsonl` (every birth and death), `stats.jsonl` (one row per
  sample), `absorptive.jsonl` (every body with a stomach, every ten seconds: its income, upkeep,
  reserve and depth), `snapshots/` (every living body's recipe at a sample), `run.json` and
  `config.json`, and `guide/guide.json` (every line, named).
- The readers of round 48's second film in [`story-r48-v2/`](story-r48-v2/): `runlib.py` (streaming
  readers, the guide's line rule, counts over time), `facts.py`, `extras.py` and `make_story.py`,
  with the story, shot list and checks they wrote. A new writer copies them into the story's folder
  and starts from them; each writes its output beside itself.
- The fields the director reads, in `unity/Assets/Theatre/SafariStory.cs` and `SafariChart.cs`.
- The glossary, [`story-glossary.md`](story-glossary.md).

Write only in the story's folder (`scratch/story/<round>/`), never in your session scratchpad,
the temporary directory or outside the repository. Read the runs one process at a time and stream
the large files. Launch nothing heavy: no Unity, no farm run, no test suite. You cannot wait for
anything, so finish when your files are written.

## The truth rules, kept from the first film

1. **Every number on screen** is read from the runs by a script you keep, and has a row in
   `checks.tsv`: the scene, the caption or chart it appears in, the number as shown, the exact
   value, the file and the query. A number with no row is not filmed.
2. **A guess** says it is a guess, on screen as in the prose: "I don't know why", "my guess".
   A cause is a guess until a reading shows it.
3. **Nothing about wanting.** A body with no leaf "lives on snow";
   it does not "choose" snow. A line's recipe "bets on small children" only as a figure of speech
   the viewer can see through, and only once the numbers behind it are on screen.
4. **The screen is a cousin**: every scene is re-run from a checkpoint and drifts from what was
   recorded. So a caption about a particular birth, death or count says "in the run", and a scene that must
   show a particular body is set at a checkpoint second with `flexible: false`.
5. **The code outranks an earlier film**: before you repeat a claim from an earlier film or entry,
   find it in the code or the settings. Round 48's first film said the dead make the snow; living
   leaves made three quarters of it. The glossary lists what else changed.

## The glossary rule

1. **Every term on screen** is explained on screen before or where it is first used, in plain
   words, in the same caption or the one before. The test is the owner's: anything obvious to you
   is not obvious to a viewer. Stop at the first word a newcomer could not define; that word is a
   defect.
2. **One name per thing**, the glossary's: a body with a stomach and no leaf is an eater, every
   time. Do not alternate "stomach body", "absorptive" and "eater".
3. **No code names on screen**: never `bf`, `pool`, `trickle`, "clade" or "absorptive".
   Say "born at 6% of its adult size", "a stored eater", "a newcomer", "a line", "a stomach".
4. **A new word** goes into the glossary first, with its meaning checked against the code and its
   source named. If the code contradicts what an earlier film said, say so in the entry.
5. **Units** go on every number, and "J a second" rather than W in a caption. A chart's axis may say W.

## The opening chapter: how this world works

The film opens with a chapter that answers the owner's questions before the story needs them.
Aim for about a minute of scenes; do not pass two. It must leave the viewer knowing:

- what a tank is and where its energy comes from (light, fading with depth);
- that a body is built from a recipe its children inherit with small changes, and that recipes
  which leave more children spread;
- what a leaf does and what a stomach does, and what snow is;
- how a body earns, pays upkeep and saves, and what its reserve is;
- what it takes to have a child: growing up first, then saving the price of the litter plus a
  cushion; the price itself (the child's body, its first reserve, the fee);
- what it takes to die: the reserve reaching zero, and why it gets there (age raises upkeep,
  income falls).

Teach through one real body with a recorded ledger (an absorptive log row every ten seconds),
alive at a checkpoint, whose life shows a birth and a death. Round 48's film used body 201 of
tank 1. Terms the story needs later (a line, a newcomer, a stored eater, a joint) are explained
in the scene where they first matter.

## The story rule

1. **Find the arc** in the data before writing a caption. It has four beats: the hope (what the round
   was for, in the owner's terms), the obstacle (what went wrong or stood in the way), the turn
   (the surprise, the unexpected success or the moment it failed), the end (where it left us).
2. **Name its kind**, triumph, tragedy or bittersweet. Write the arc in two sentences at the top
   of `story.md` and in `story.json`'s `arc`. If the data has no turn, the kind is tragedy and
   you say so; do not invent a turn.
3. **Give the viewer someone** to follow: a body or a line met early, returned to, and resolved.
   A callback (the last body echoing the first) is worth a caption.
4. **Every chapter** moves the arc. A true fact that moves nothing goes in `story.md` and stays
   off the screen. A subplot is allowed when it feeds the main line; say how in the chapter.
5. **Say the stake** at the start and the verdict at the end, in the round's own terms: which
   predictions held and which failed, in words a viewer can hold.

## Caption rules

- **Write one idea** in each caption. Two short sentences are allowed when the second finishes the first
  ("Its reserve is 9 J, and falling.").
- **Readable in its time**: a caption is held for its length over 14 characters a second, and never
  less than 4 s (a 56-character caption for 4 s, 70 for 5 s), with 1 s between captions. Keep
  captions under about 70 characters. Set a caption's `seconds` when it needs longer than 4.
- **Round on screen** and exact in the checks. 44.5 s may stay 44.5 when the half matters; 1,440.6 is
  1,441; a percentage takes no decimals unless it is under 1%. The exact value goes in
  `checks.tsv`.
- **The font** is IBM Plex Sans. Since 2026-09-25 a story's frames carry no text: the join sets
  the captions as subtitles, white with a soft dark outline, in the lower third
  (`scripts/story-assemble.py`). Write captions in mixed case, as they should read. Em dashes,
  curly quotes, `×`, `²`, `³`, arrows and accented letters all stand. The builder checks every
  caption and chart label against the font file. Emoji and scripts the font lacks are refused.
  The charts are drawn in Plex too. A story filmed with its text stamped into the frames
  (`theatre-safari.ps1 -BurnText`, the first film's look) is held to the old 5×7 bitmap font:
  capitals, digits and `.,:;-+=_/()[]%!?'\*#·`. Set `STAMPED = True` in the builder for that.
- **Plain words**, and none of the tells in `STYLE.md` §5: no closing contrasts of the "X, and
  not Y" kind, no intensifiers, no rhetorical questions.
- **No caption** runs past its scene's end, and the builder checks that.

## The chart rule

1. **A chart earns its place** when it introduces a body (its account), a concept (light by depth,
   what a child costs, where snow comes from) or a comparison the words cannot carry (two lines'
   fortunes, then against now). One chart a scene at most. Not every scene needs one.
2. **The form** goes in the scene's `chart` field:

   ```
   "chart": {"kind": "line" | "bars" | "account", "title": "...", "place": "corner" | "full",
             "at": 2.0, "until": 18.0,
             "x": {"label": "..."}, "y": {"label": "..."},
             "series": [{"name": "...", "points": [[x, y], ...], "highlight": true}],
             "bars": [{"label": "...", "value": 0.91, "highlight": true}],
             "marks": [{"x": 13700, "label": "the run stops"}]}
   ```

   `at` and `until` are seconds into the scene, counted as captions are (after the chapter card,
   which the reader adds). `until` defaults to the scene's end.
3. **A `full` chart** may go on any station but a Portrait or a Birth, whose body its card would
   cover. The world keeps moving under it, dimmed by a third. A `corner` chart goes on any station.
   A Card is the usual home for a full chart: it is a slow drift through the crowd, made to be
   talked over.
4. **An `account` chart** draws the followed body's reserve live, so it goes on a Portrait (or a Birth) that
   follows `body N` or `founder body N`. Choose a second where the reserve moves within the scene: a
   birth due in the next seconds, or a reserve running out. A flat account teaches little; a
   recorded life belongs in a `line` chart on a Card, drawn from the absorptive log.
5. **Every point, bar and mark** has its source in `checks.tsv`: the file, the query and the
   values. A line chart's points are written out in full in its row.
6. **A title** states what is drawn and its unit ("Body 201's reserve over its life, J"), and a
   highlighted series or bar is the one the captions talk about.

## The scene rules the director needs

- Stations: Arrival, Descent, Portrait, Floor, Birth, Colony, Time, Card.
- Subjects: `world`, `founder body N`, `body N`, `parent body N` (a Birth), `guide clade N` or a
  line's name. A Colony's subject may carry both, as "Gastrella cidutis (guide clade 362), founder
  body 3318".
- A scene that must show one body is set at a checkpoint second of its run with `flexible: false`.
  Round 48's tanks 1 and 2 saved every 2,500 s and tank 3 every 500 s.
- A Card is moving footage of the world: a slow sideways drift, about 0.15 m a second, in front of
  the densest part of the crowd at its second. It is filmed on the world already on screen, or at
  the checkpoint nearest its second, without stepping to it. Its first caption is its title. A
  full chart on it dims the world by a third on its own. The word "dimmed" in its `description`
  darkens a Card with no chart a little, and "black" blacks it out; neither is needed for the
  captions, which carry their own outline. Never put `chapter` on a Card: the reader drops the
  chapter's title and still counts the chapter.
- A chapter card (the 8 s before a scene carrying `chapter`) is the same slow drift, with the
  chapter's line as its caption.
- A scene carrying `chapter` plays an 8 s chapter card before it.
- A Time scene plays two halves of its `seconds`, at `from` and at `to`.

## Length

The whole film, chapter cards and the 5 s title included, is about ten minutes at most, as the
builder counts it. Round 48's second film came to 9 min 39 s over 20 scenes and 7
chapters.

## What you hand back

- `story.md`: the story in prose for the owner, following `STYLE.md`, with the arc first, the
  chapters in order, a verdict table of the round's predictions, what earlier films got wrong,
  and what could not be checked.
- `story.json`: the shot list, with `title`, `arc`, `runs`, `screen_seconds` and the scenes.
- `checks.tsv`: one row per number and per rule stated on screen.
- The builder script that wrote the last two. It checks every caption and chart label against the
  font file, the reading pace, and that every caption and chart falls inside its scene. It checks
  that no Card carries a chapter. It checks that every `account` chart sits on a Portrait or a
  Birth, and that no `full` chart does.

Before you hand back, read every caption aloud in order as a newcomer would. Stop at the first word
you could not define, and fix it.
