# The story writer's brief, version 3

*Written 2026-09-26 for the writer of a round's story film. Version 2, of 2026-09-25, was a brief
for subtitles: one idea a caption, under 70 characters, a number in most scenes and every term
defined where it appeared. Round 49's first draft followed it to the letter and read like a list of
findings, one short fact a line, and the owner rejected it. The film is narrated now
([`story-narration.md`](story-narration.md)), so this version asks for what a narrator says, written
as speech, and the tool cuts the subtitles from it. The procedure around the brief is
[`story-film.md`](story-film.md), and the words a film may use are in
[`story-glossary.md`](story-glossary.md).*

## What the film is for

A story film tells one round of the simulator to someone who has never seen it: a friend of the
owner, or the owner a month from now. They hear it once, at the speed of the film, while they
watch the tank. After the first film the owner asked for four things, in these words:

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

- The round's arc as the owner approved it: `arc.md` and `arc-ruling.md` in the story's folder,
  with `arc-notes.md` beside them (the first pass's reads, and where the round's settings differ
  from the glossary). The second pass tells that arc and no other.
- The round's logbook entry and its pre-registration: what the round asked and what it predicted.
- The runs, read-only: `lineage.jsonl` (every birth and death), `stats.jsonl` (one row per
  sample), `absorptive.jsonl` (every body with a stomach, every ten seconds: its income, upkeep,
  reserve and depth), `snapshots/`, `run.json`, `config.json` and `guide/guide.json` (every line,
  named). From round 49 the runs are record format 2: read snapshots, genomes and positions through
  `scripts/reads/runrec.py`.
- The readers of round 48's second film in [`story-r48-v2/`](story-r48-v2/) (`runlib.py`,
  `facts.py`, `extras.py`, `make_story.py`). They are a model for reading the runs and building the
  shot list. Their captions are the style this version replaces, so none of their sentences is a
  model.
- The fields the director reads, in `unity/Assets/Theatre/SafariStory.cs` and `SafariChart.cs`.
- `scripts/story-script.py`, whose `split_cues` cuts a paragraph into subtitles and whose `retime`
  lays a scene's subtitles in time. Import both; do not write your own.

Write your files into the story's folder (`scratch/story/<round>/`) through the shell, as a
heredoc or a Python write, and never with the Write or Edit tools (the owner, 2026-09-26). Write
nowhere else: not your session scratchpad, not the temporary directory. Read the runs one process
at a time and stream the large files. Launch nothing heavy: no Unity, no farm run, no test suite.
You cannot wait for anything, so finish when your files are written.

## The narration

The film is a narrated documentary about one round. The narration is the film's spine: a listener
who hears it with their eyes closed should follow the story. Write what the narrator says, for
someone who hears it once and has never seen the tank.

1. **Write each scene as spoken paragraphs.** Most scenes take one paragraph, and a scene takes
   two where the picture changes or a new idea begins. Write each paragraph whole, as prose, and
   read it aloud before it stands. Never write subtitle lines. The builder cuts every paragraph
   into subtitles with `split_cues`, so a paragraph's sentences can be as long as speech wants.
2. **Let the sentences connect.** A paragraph moves from one thing to the next through the words
   that join them: because, so, but, while, until, after, which. The lengths of its sentences
   vary. A run of short sentences that each state one fact is the failure this version exists to
   stop, and three in a row is already a run.
3. **Use few numbers, and say them as a person does.** A number belongs in the narration when the
   story turns on it: one or two a paragraph at most, and many paragraphs need none. Round it so a
   listener can hold it: "about a hundred and sixty joules", "nearly two thirds", "a seventh of the
   light". Give the tank's time as a listener's time, such as "in its first quarter of an hour", "a
   little over an hour after it arrived" or "near the end of the eight hours". A decimal second
   ("4,000.5 s") or an age in seconds ("3,747 s old") is never said. The exact value goes in
   `checks.tsv` beside the words shown, and the rounding has to be true of it: 44% is "nearly
   half", and "half" would be false.
4. **Explain a word inside the sentence that needs it**, as part of what is happening: "It has a
   stomach, which feeds on the fine snow of dead matter drifting through the water." Never define
   a word after a colon, and never spend a sentence on a definition alone. Bring in a word only if
   the story uses it again. Everything else is said in plain words.
5. **Say where and when only when it matters, and inside a sentence.** A scene never opens on a
   label such as "Tank 2, 4,900 s." The picture shows where we are; the narration says what is
   happening there.
6. **Tell the story.** Each paragraph says what is on screen and why it matters to the arc. A true
   fact that moves nothing goes in `story.md` and stays out of the narration.
7. **End on the story.** The last chapter says what the round found and what it leaves open, in a
   few sentences about the tank and its bodies. The table of predictions lives in `story.md` and
   may be a chart on the last card, but the narration never reads a list of verdicts aloud.
8. **Keep the voice detached.** There is no "I" and no "we". A reaction is said without a
   narrator: "Nobody had predicted that."
9. **Leave out the flourishes.** A paragraph does not end on a short sentence for effect, a scene
   does not open on a title-like phrase ("Three founders, one plan"), and there are no
   intensifiers ("simply", "just", "really", "truly") and no closing contrasts of the "X, not Y"
   kind (STYLE.md §5 has the rest).

### What round 49's rejected draft did

The same facts, before and after. The after is there to show the technique; do not reuse its
sentences.

| the rejected draft | what is wrong | the technique, shown |
|---|---|---|
| "Bodies grow from recipes, which children inherit a little changed. / Recipes that leave more children spread. Nothing else chooses." | one fact a line, and a slogan to end | "Each body grows from a recipe that its children inherit with small changes, and over time the recipes that leave more children become more common." |
| "Body 48 has a stomach, which eats snow: specks of food in the water." | a definition after a colon | "It has a stomach, which feeds on the fine snow of dead matter drifting through the water." |
| "Tank 2, 11,500 s. Body 27057 is another one-part stored eater. / It eats 0.1 J a second and pays 0.41. Its reserve is 5 J." | a label for an opening, and three numbers in a row | "This one landed about five minutes ago with nine tenths of what it needed, and it has been losing ground since, because its stomach brings in about a quarter of what it costs to stay alive." |
| "Body 48 is here too, 3,747 s old, with 81 J left." | an age in seconds, a reserve to the joule | "Body 48 is still among them, a little over an hour old now, with a fifth of what it once held." |
| "Of 34 predictions, 24 held and 6 failed. 4 were readings, with no bar." (and eight more verdict lines) | a checklist read aloud | the story's end in a few sentences, and the table in `story.md` |

### Before you hand back

1. Read every paragraph aloud, in order, as the listener hears it.
2. Go through each paragraph for the faults above. Look for a run of one-fact sentences, more
   than two numbers, a number a listener cannot hold, a definition after a colon, a label for an
   opening, a slogan for an ending, a list read aloud, an intensifier, and a word used before it is
   explained. Fix every one.
3. Run `python scripts/story-script.py render <story folder> --force` and
   `python scripts/story-script.py check <story folder>`. Fix every ERROR and read every WARN.
4. In your last message, give for each scene the count of numbers in its narration and its longest
   run of sentences under ten words.

## The truth rules

1. **Every number on screen**, in digits or in words, is read from the runs by a script you keep,
   and has a row in `checks.tsv`: the scene, the words shown, the exact value, the file and the
   query. A number with no row is not filmed.
2. **A guess** says it is a guess, in the narration as in the prose: "nobody knows why yet", "most
   likely". A cause is a guess until a reading shows it.
3. **Nothing about wanting.** A body with no leaf lives on snow; it does not choose snow. A figure
   of speech about a recipe ("it bets on small children") is allowed only when the viewer can see
   through it and the numbers behind it are on screen.
4. **The corner word says what the screen is.** A scene played from a window the farm recorded
   reads FAITHFUL and shows what happened, and every scene of round 49 is written for one. The
   narration says once, where the rounds and tanks come in, that every scene is replayed from a
   saved moment of the run. "In the run" is added after filming, and only on a scene the film
   reports as COUSIN (STYLE.md §7).
5. **A time is counted from when it is heard.** "About twenty seconds from now" is heard when the
   line is spoken, not at the scene's start. Say "is about to" or "before this scene ends", or say
   when in the run it happens.
6. **The code outranks an earlier film.** Before you repeat a claim from an earlier film or entry,
   find it in the code or the settings. `arc-notes.md` lists what changed for round 49.

## The glossary rule

1. **Every term** is explained in the narration where it first matters, inside the sentence that
   uses it. The test is the owner's: anything obvious to you is not obvious to a listener.
2. **One name per thing**, the glossary's: a body with a stomach and no leaf is an eater, every
   time.
3. **No code names**: never `bf`, `pool`, `trickle`, "clade" or "absorptive". Say "born at a sixth
   of its adult size", "a stored eater", "a newcomer", "a line", "a stomach".
4. **A new word** goes into the glossary first, with its meaning checked against the code and its
   source named. Hand the glossary rows back in your last message.
5. **Units are said in words** in the narration: joules, metres, minutes, hours. A chart's axis may
   use the symbols.

## The opening chapter: how this world works

The film opens with the world, told through one body's life, before the story needs it: about a
minute, never more than two. By its end the listener knows what a tank is and where its energy
comes from, and that a body grows from a recipe its children inherit with small changes. They know
what a leaf does, what a stomach does and what snow is. They know how a body earns, pays its upkeep
and saves into a reserve, what it takes to have a child, and what it takes to die. It is the life
of the approved arc's teaching body, told in order, and each idea arrives when that life reaches
it. It is never a tour of terms.

## The story rule

1. **Tell the approved arc**: the hope, the obstacle, the turn and the end, of the kind the owner
   ruled.
2. **Give the listener someone** to follow: the arc's body or line, met early, returned to and
   resolved. A callback, the last body echoing the first, is worth a sentence.
3. **Every chapter** moves the arc. A subplot is allowed when it feeds the main line, and the
   narration says how.
4. **Say the stake** at the start and the verdict at the end, in the round's own terms, in words a
   listener can hold.

## The chart rule

1. **A chart earns its place** when it introduces a body (its account), a concept (light by depth,
   what a child costs, where snow comes from) or a comparison the words cannot carry. One chart a
   scene at most. The narration may point at it ("the line climbing on the left is its reserve").
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
  Round 49 saved every 100 s in all three tanks.
- A Card is moving footage of the world, a slow drift in front of the densest part of the crowd.
  Its first subtitle is its title, and it never carries `chapter`.
- A scene carrying `chapter` plays an 8 s chapter card before it, with the chapter's line spoken.
- A Time scene plays two halves of its `seconds`, at `from` and at `to`.
- A scene whose picture holds an event at a known second (a birth, a death, a reserve reaching
  zero) carries `least_seconds`, the event's second into the scene plus two, and the narration
  reaches the event before the picture does.
- **A scene's length comes from its narration.** The builder cuts each paragraph with
  `split_cues`, marks the first cue of every paragraph after the first `new_paragraph: true`,
  lays the cues with `retime([], cues)`, and sets `seconds` to the larger of the laid end plus the
  tool's `TAIL`, the picture's need and `least_seconds`. The narration stage retimes every scene
  to the voice later.

## Length

The whole film, chapter cards and the 5 s title included, is about ten minutes at most, as the
builder counts it. The subtitles' reading pace is slower than speech, so aim for about 1,000 to
1,200 words of narration in all.

## What you hand back

The writer runs twice. The first pass reads the runs and writes only `arc.md`, for the owner's
ruling before any narration is written: the arc in two sentences and its kind, the stake, who the
listener follows, the chapters with one line each on what happens in them, and the predictions'
verdicts. The second pass, from the arc as ruled, writes four files into the story's folder:

- `story.md`: the story in prose for the owner, following `STYLE.md`, with the arc first, the
  chapters in order, a verdict table of the round's predictions, what earlier films got wrong,
  and what could not be checked.
- `story.json`: the shot list, with `title`, `arc`, `runs`, `screen_seconds` and the scenes, each
  scene's narration cut into its `captions`.
- `checks.tsv`: one row per number and per rule stated on screen.
- `make_story.py`, the builder that wrote the last two and reads every number from the runs. It
  checks every subtitle and chart label against the font file, that every subtitle and chart falls
  inside its scene, that no Card carries a chapter, that every `account` chart sits on a Portrait
  or a Birth and no `full` chart does, and the film's length.

Your last message is a short report: the film's length and scene count, the per-scene counts from
"Before you hand back", the glossary rows, and anything you could not make true on screen.