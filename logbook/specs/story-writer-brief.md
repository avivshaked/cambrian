# The story writer's brief

*The rules for the writer of a round's story film. The `story-film` skill launches the writer with a
fixed prompt that names only this brief and the story's folder; everything else the writer needs is
in this brief, in that folder and in the files named below. Paths without a root are in the
checkout that holds this brief.*

## What the film is for

A story film tells one round of the simulator to someone who has never seen it: a friend of the
owner, or the owner a month later. They hear it once, at the speed of the film, while they watch
the tank. The owner's requirements, in their words:

1. "we need to explain the world, and when we use a term we need to explain it. what is a
   stomach? what is a line? what does it take to have a child? what does it take to die.
   consider that anything that is obvious to you, is not obvious to anyone unfamiliar with our
   world."
2. "can we introduce graphs in unity? it would be great to be able to show some stats when
   introducing a creature, or introducing a concept."
3. "when telling a story, we want a story, you know? i don't mean to start with 'once upon a
   time', but we do want to explain in a way a story of triumph or tragedy or something in
   between - bitter sweet."

Every rule below serves one of those, or the rule that nothing on screen is false.

## What you are given, and what you may touch

- **The story's folder.** Read only these fields of its `flow.json`: `runs`, `runs_root`, `entry`
  (the round's logbook entry) and `prereg` (its pre-registration). Each run is the newest directory
  holding a `run.json` under `<runs_root>/<arm>/`. The rest of the folder, where it exists:
  - `arc.md`, `arc-reads.md` and `arc-choices.md`, the first pass's files;
  - `owner/`, the owner's words, word for word, one file a ruling, named
    `<stage>-<K>-<verdict>.md`. The owner's words rule. On the arc, where they say nothing about
    one of `arc-choices.md`'s choices, that choice's recommendation stands;
  - `superseded/arc-<K>/`, an arc the owner rejected, with their words on it as `owner.md`;
  - `drafts/<K>/`, the review of each draft you handed back, K counting from 1: the page as it read
    (`page.md`), the tool's check (`check.txt`), the listener's faults (`listen.md`), the fact
    checker's rows (`facts.md`) and, where the owner ruled on the draft, their words (`owner.md`);
  - `changes.md`, changes the owner asked for after the script stage that only you may make.
- **The runs, read-only**:
  - `lineage.jsonl`, every birth and death;
  - `stats.jsonl`, one row per sample;
  - `absorptive.jsonl`, every body with a stomach, every ten seconds: its income, upkeep, reserve
    and depth;
  - `snapshots/`, `run.json`, `config.json`;
  - `guide/guide.json`, every line, named.

  `run.json`'s `recordFormat` says how the snapshots, genomes and positions are stored. Read them
  through `scripts/reads/runrec.py`, which knows every format. Its `checkpointEverySeconds` is the
  cadence of the saved moments a scene can be set at.
- **The glossary**, `logbook/specs/story-glossary.md`, and the style guide, `STYLE.md`.
- **Readers from an earlier film** in `logbook/specs/story-r48-v2/` (`runlib.py`, `facts.py`,
  `extras.py`, `make_story.py`). They are a model for reading the runs and building the shot list,
  and nothing more: their captions are not a model for the narration.
- **The fields the director reads**, in `unity/Assets/Theatre/SafariStory.cs` and `SafariChart.cs`.
- **`scripts/story-script.py`**, whose `split_cues` cuts a paragraph into subtitles, whose `retime`
  lays a scene's subtitles in time, and whose `render` and `check` show and check the page. Import
  it with `importlib`, since its name has a hyphen, and do not write your own cutter or timer.

Write your files into the story's folder through the shell, as a heredoc or a Python write, and
never with the Write or Edit tools. Write nowhere else, neither your session scratchpad nor the
temporary directory. Read the runs one process at a time and stream the large files. Launch
nothing heavy: no Unity, no farm run, no test suite. You cannot wait for anything, so finish when
your files are written.

## The narration

The film is a narrated documentary about one round. The narration is its spine: a listener who
hears it with their eyes closed should follow the story. Write what the narrator says, for someone
who hears it once and has never seen the tank.

1. **Write each scene as spoken paragraphs.** Most scenes take one paragraph, and a scene takes
   two where the picture changes or a new idea begins. Write each paragraph whole, as prose, and
   read it aloud before it stands. Never write subtitle lines. The builder cuts every paragraph
   into subtitles with `split_cues`, so a sentence can be as long as speech wants.
2. **Let the sentences connect.** A paragraph moves from one thing to the next through the words
   that join them: because, so, but, while, until, after, which. The lengths of its sentences
   vary. Three short sentences in a row that each state one fact are a list, and the narration is
   never a list.
3. **Use few numbers, and say them as a person does.** A number belongs in the narration when the
   story turns on it: one or two a paragraph at most, and many paragraphs need none. Round it so a
   listener can hold it: "about a hundred and sixty joules", "nearly two thirds", "a seventh of the
   light". Give the tank's time as a listener's time: "in its first quarter of an hour", "a little
   over an hour after it arrived", "near the end of the run". A decimal second or an age in seconds
   is never said. The exact value goes in `checks.tsv` beside the words shown, and the rounding is
   true of it: 44% is "nearly half", and "half" would be false.
4. **Explain a word inside the sentence that needs it**, as part of what is happening. Never define
   a word after a colon, and never spend a sentence on a definition alone. Bring in a word only if
   the story uses it again, and say everything else in plain words.
5. **Say where and when only when it matters, and inside a sentence.** A scene never opens on a
   label naming its tank and second. The picture shows where we are; the narration says what is
   happening there.
6. **Tell the story.** Each paragraph says what is on screen and why it matters to the arc. A true
   fact that moves nothing goes in `story.md` and stays out of the narration.
7. **End on the story.** The last chapter says what the experiment found and what it leaves open, in a
   few sentences about the tank and its bodies. The table of predictions lives in `story.md` and
   may be a chart on the last card, but the narration never reads a list of verdicts aloud.
8. **Keep the voice detached.** There is no "I" and no "we". A reaction is said without a
   narrator: "Nobody had predicted that."
9. **Leave out the flourishes.** A paragraph does not end on a short sentence for effect, a scene
   does not open on a title-like phrase, and there are no intensifiers ("simply", "just",
   "really", "truly") and no closing contrasts of the "X, not Y" kind. `STYLE.md` §5 lists the
   rest.
10. **Name an experiment by what it asked, never by its number.** The project numbers its rounds,
    and a listener has no use for the numbers: none is said in the narration, and none appears in
    the title, a chapter, a subtitle or a chart. Where the story needs to name this experiment,
    name it by the question it set out to answer or the change it made, if that works in the
    story; an earlier one is "the experiment before this one", or is named the same way. The word
    "round" means nothing to a listener either, so the narration says "experiment", or finds
    another way to say it, and never calls an experiment a round. `story-script.py check` refuses
    a round's number anywhere on screen.

### Faults, and the technique that fixes them

The examples are invented to show a technique. Their bodies and numbers belong to no run, and
their sentences are not to be reused.

| fault | looks like | the technique |
|---|---|---|
| a list of one-fact sentences | "Body 7 has a leaf. The leaf catches light. Light is energy. The energy fills its reserve." | "Body 7 carries a leaf that turns the light falling on it into energy, and whatever it does not spend goes into its reserve." |
| a definition after a colon | "Its reserve: the energy it has saved." | "…goes into its reserve, the energy it keeps for later." |
| a label for an opening | "Tank 3, 12,400 s. Body 7 is a newcomer." | "Well into the fourth hour, a newcomer drifts in from outside." |
| numbers a listener cannot hold | "It is 2,318.5 s old, with 63.2 J left." | "It is about forty minutes old, and most of its reserve is gone." |
| a slogan for an ending | "Nothing else decides." | end on what the story needs next |
| a list read aloud | "The first prediction held. The second held. The third failed." | "Two of the three predictions held, and the third failed because the bodies it was about lived far longer than expected." |

### Before you hand back

1. Read every paragraph aloud, in order, as the listener hears it.
2. Go through each paragraph for the faults above. Look for a run of one-fact sentences, more
   than two numbers, a number a listener cannot hold, a definition after a colon, a label for an
   opening, a slogan for an ending, a list read aloud, an intensifier, and a word used before it is
   explained. Fix every one.
3. Run `python scripts/story-script.py check <story folder>`. It counts what can be counted of
   the rules above (numbers a paragraph, runs of short sentences, units, decimals, colons, labels,
   intensifiers, a number with no `checks.tsv` row whose `shown` cell holds its words), and every
   finding it prints as ERROR fails the draft. Fix every ERROR and read every WARN.
4. Put in `writer-report.md`, for each scene, the count of numbers in its narration and its longest
   run of sentences under ten words.

## The truth rules

1. **Every number on screen**, in digits or in words, is read from the runs by a script you keep,
   and has a row in `checks.tsv`: the scene, the words shown, the exact value, the file and the
   query. A number with no row is not filmed.
2. **A guess** says it is a guess, in the narration as in the prose: "nobody knows why yet", "most
   likely". A cause is a guess until a reading shows it.
3. **Nothing about wanting.** A body with no leaf lives on snow; it does not choose snow. A figure
   of speech about a recipe ("it bets on small children") is allowed only when the listener can see
   through it and the numbers behind it are on screen.
4. **The corner word says what the screen is.** A scene played from a window the farm recorded
   reads FAITHFUL and shows what happened; write every scene for one. The narration says once,
   where the experiment and its tanks come in, that every scene is replayed from a saved moment of the
   run. "In the run" is added after filming, and only on a scene the film reports as COUSIN.
5. **A time is counted from when it is heard.** "About twenty seconds from now" is heard when the
   line is spoken, not at the scene's start. Say "is about to" or "before this scene ends", or say
   when in the run it happens.
6. **The code outranks an earlier film.** Before you repeat a claim from an earlier film or entry,
   find it in the code or the settings. The glossary names each word's source, and it may have been
   written for another round's settings; check every entry you use against the round's
   `config.json`.

## The glossary rule

1. **Every term** is explained in the narration where it first matters, inside the sentence that
   uses it. Anything obvious to you is not obvious to a listener.
2. **One name per thing**, the glossary's: a body with a stomach and no leaf is an eater, every
   time.
3. **No code names**: never `bf`, `pool`, `trickle`, "clade" or "absorptive". Say "born at a sixth
   of its adult size", "a stored eater", "a newcomer", "a line", "a stomach".
4. **A new word** is checked against the code, with its source named, and handed back as a
   glossary row in `glossary-rows.md`.
5. **Units are said in words** in the narration: joules, metres, minutes, hours. A chart's axis may
   use the symbols.

## The opening chapter: how this world works

The film opens with the world, told through one body's life, before the story needs it: about a
minute, never more than two. By its end the listener knows what a tank is and where its energy
comes from, and that a body grows from a recipe its children inherit with small changes. They know
what a leaf does, what a stomach does and what snow is. They know how a body earns, pays its upkeep
and saves into a reserve, what it takes to have a child, and what it takes to die. It is the life
of the arc's teaching body, told in order, and each idea arrives when that life reaches it. It is
never a tour of terms.

## The story rule

1. **Find the arc** in the data before writing a word of narration: the hope, the obstacle, the
   turn and the end. Name its kind: triumph, tragedy or bittersweet. With no turn in the data, the
   kind is tragedy, and no turn is invented.
2. **Give the listener someone** to follow: a body or a line, met early, returned to and resolved.
   A callback, the last body echoing the first, is worth a sentence.
3. **Every chapter** moves the arc. A subplot is allowed when it feeds the main line, and the
   narration says how.
4. **Say the stake** at the start and the verdict at the end, in the experiment's own terms, in words a
   listener can hold.

## The chart rule

1. **A chart earns its place** when it introduces a body (its account), a concept (light by depth,
   what a child costs, where snow comes from) or a comparison the words cannot carry. One chart a
   scene at most. The narration may point at it.
2. **The form** goes in the scene's `chart` field:

   ```
   "chart": {"kind": "line" | "bars" | "account", "title": "...", "place": "corner" | "full",
             "at": 2.0, "until": 18.0,
             "x": {"label": "..."}, "y": {"label": "..."},
             "series": [{"name": "...", "points": [[x, y], ...], "highlight": true}],
             "bars": [{"label": "...", "value": 0.91, "highlight": true}],
             "marks": [{"x": 13700, "label": "the run stops"}]}
   ```

   `at` and `until` are seconds into the scene, counted as the subtitles are (after the chapter
   card). `until` defaults to the scene's end.
3. **A `full` chart** goes on any station but a Portrait or a Birth, whose body its card would
   cover. A `corner` chart goes on any station. A Card is the usual home for a full chart.
4. **An `account` chart** draws the followed body's reserve live, so it goes on a Portrait or a
   Birth that follows `body N` or `founder body N`, at a second where the reserve moves within the
   scene.
5. **Every point, bar and mark** has its source in `checks.tsv`, a line chart's points in full.
6. **A title** states what is drawn and its unit, in plain words.

## The scene rules the director needs

- Stations: Arrival, Descent, Portrait, Floor, Birth, Colony, Time, Card.
- Subjects: `world`, `founder body N`, `body N`, `parent body N` (a Birth), `guide clade N` or a
  line's name. A Colony's subject may carry both.
- A scene that must show one body is set at a checkpoint second of its run with `flexible: false`.
- A Card is moving footage of the world, a slow drift in front of the densest part of the crowd.
  Its first subtitle is its title, and it never carries `chapter`.
- A scene carrying `chapter` plays an 8 s chapter card before it, with the chapter's line spoken.
- A Time scene plays two halves of its `seconds`, at `from` and at `to`.
- A scene whose picture holds an event at a known second (a birth, a death, a reserve reaching
  zero) carries `least_seconds`, the event's second into the scene plus two, and the narration
  reaches the event before the picture does.
- **A scene's length comes from its narration.** The builder cuts each paragraph with
  `split_cues`, marks the first cue of every paragraph after the first `new_paragraph: true`,
  lays the cues with `retime([], cues)`, and sets `seconds` to the largest of the laid end plus the
  tool's `TAIL`, the picture's need and `least_seconds`.

## Length

The whole film, chapter cards and the 5 s title included, is about ten minutes at most, as the
builder counts it. The subtitles' reading pace is slower than speech, so aim for about 1,000 to
1,200 words of narration in all.

## What you hand back

The writer runs twice. Write your files into the story's folder through the shell, and end with a
short message saying which files you wrote.

**The first pass** reads the runs and writes three files, for the owner's ruling before any
narration is written:

- `arc.md`: the arc in two sentences and its kind, the stake, who the listener follows (with the
  run and the checkpoint seconds where it can be filmed), the opening chapter's teaching body, the
  chapters with one line each on what happens in them, the predictions' verdicts in words a
  listener can hold, what earlier films or the glossary got wrong for this round, and what could
  not be checked.
- `arc-reads.md`: the read behind each number in `arc.md` (the file, the query and the value), and
  every glossary entry whose value or meaning differs in this round's settings.
- `arc-choices.md`: every choice the owner should rule on with the arc, numbered from 1. Each
  choice gives its options, what each option means for the film, and a line starting
  `Recommendation:` naming the option you recommend and why. The owner reads this file whole and
  answers it by number.

If the folder holds `superseded/arc-<K>/`, the owner rejected an arc: read the newest one's files
and the owner's words beside them, and write an arc that answers them.

**The second pass** tells the arc as `arc.md`, `arc-choices.md` and the owner's words in `owner/`
have it, and writes six files:

- `story.md`: the story in prose for the owner, following `STYLE.md`, with the arc first, the
  chapters in order, a verdict table of the round's predictions, what earlier films got wrong,
  and what could not be checked.
- `story.json`: the shot list, with `title`, `arc` (two sentences), `arc_parts`
  (`{"followed": the body or line the film follows, "turn": the turn in one sentence, "kind":
  "triumph", "tragedy" or "bittersweet"}`), `runs`, `screen_seconds` and the scenes, each scene's
  narration cut into its `captions`.
- `checks.tsv`: one row per number and per rule stated on screen, with the columns `scene`, `item`,
  `shown`, `exact`, `source` and `how_read`. `shown` holds the words the viewer hears for the
  number, as they are spoken ("nearly two thirds", "about forty minutes"), since the check matches
  the narration's number words against it. A chart's row has an `item` starting `chart` and an
  `exact` holding every value the chart draws. Every row has a `source`.
- `make_story.py`, the builder that wrote `story.json` and `checks.tsv` and reads every number from
  the runs. It checks every subtitle and chart label against the font file, that every subtitle and
  chart falls inside its scene, that no Card carries a chapter, that every `account` chart sits on a
  Portrait or a Birth and no `full` chart does, and the film's length.
- `writer-report.md`: the film's length and scene count, the per-scene counts from "Before you
  hand back", and anything that could not be made true on screen.
- `glossary-rows.md`: a row for the glossary's table for every new word, in its form (`| word |
  what the viewer is told | its source in the code or the settings |`), or the line `none`.

When the folder holds `drafts/`, read the newest `drafts/<K>/` before writing: your last draft
failed its review there. Fix every ERROR in `check.txt`, every fault `listen.md` names and every
row `facts.md` marks FALSE, and do what `owner.md` asks where it exists. Write the whole draft
again; the listener hears it fresh.

When the folder holds `changes.md`, the script stage has already edited `story.json`. Make every
change `changes.md` asks for, take every other scene's narration from `story.json` as it stands,
and hand back the six files as above.
