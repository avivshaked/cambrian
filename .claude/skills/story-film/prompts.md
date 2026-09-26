# The story flow's subagent prompts

Every subagent the story flow launches is launched with one of these prompts, word for word, and
with nothing added. `python scripts/story-flow.py next <folder>` prints the prompt with its
placeholders filled (`<checkout>` the checkout that holds this file, `<folder>` the story's folder,
`<k>` the draft, `<n>` the round of the script stage), together with the agent type and the model.
The session copies what `next` prints; it never writes a prompt of its own, and a note it wants a
subagent to read is not its to write (CLAUDE.md, "A subagent that a skill launches is briefed from
the skill's files alone").

The readers (the listener, the fact checkers, the cold reader and the arc comparer) are `Explore`
agents. An `Explore` agent is given no CLAUDE.md, no memory and no git status, so what it knows
is its prompt, its brief and the files they name, the same on every day and in every session. The
cold reader and the arc comparer read one file and nothing else. The writer and the editor write
their own files, so they are `general-purpose` agents.

## writer-arc
agent: general-purpose · model: opus

```text
You are the writer of a round's story film, on its first pass.
Your brief is <checkout>\logbook\specs\story-writer-brief.md. Read it whole and follow it.
The story's folder is <folder>.
```

## writer-story
agent: general-purpose · model: opus

```text
You are the writer of a round's story film, on its second pass.
Your brief is <checkout>\logbook\specs\story-writer-brief.md. Read it whole and follow it.
The story's folder is <folder>.
```

## listener
agent: Explore · model: opus

```text
You are the listener for a story film's draft narration, on draft <k>.
Your brief is <checkout>\logbook\specs\story-listener-brief.md. Read it whole and follow it.
The draft is <folder>\drafts\<k>\page.md.
```

## facts-draft
agent: Explore · model: opus

```text
You are the fact checker of a story film's draft, on draft <k>.
Your brief is <checkout>\logbook\specs\story-facts-brief.md. Read it whole and follow its part for a draft.
The story's folder is <folder>.
```

## editor
agent: general-purpose · model: opus

```text
You are the script editor of a story film, on round <n> of the script stage.
Your brief is <checkout>\logbook\specs\story-script-brief.md. Read it whole and follow it.
The story's folder is <folder>.
```

## facts-script
agent: Explore · model: opus

```text
You are the fact checker of a story film's script, on round <n> of the script stage.
Your brief is <checkout>\logbook\specs\story-facts-brief.md. Read it whole and follow its part for a script round.
The story's folder is <folder>.
```

## cold
agent: Explore · model: sonnet

```text
You are watching a short film about a simulated tank of water, and reading its narration as it
plays. You have never seen this tank or anything about it. Read the one file named below, whole.
Open no other file and use no other tool.

<folder>\script\<n>\cold-page.md

Return, in this order:
1. A line reading STUMBLES: followed by the number of stumbles you list below it, 0 if there are
   none. Then every stumble, one a line, as scene, line and kind: a word you cannot define, a line
   you had to read twice, a line that sounds written rather than spoken, a slogan or an advert, a
   jump you cannot follow, a number you cannot picture.
2. The three lines that sound most like a machine wrote them.
3. The story in two sentences, as you understood it.
```

## arc-compare
agent: Explore · model: sonnet

```text
You compare a listener's summary of a film with the story the film was meant to tell. Read the one
file named below, whole. Open no other file and use no other tool. It holds the story's arc as its
writer set it down, and the listener's summary.

<folder>\script\<n>\arc-pair.md

The summary tells the arc back when it names the body or the line the film follows, the turn, and
an ending of the arc's kind. Answer with one line and nothing else: TURN: TOLD, or TURN: MISSED |
followed by which of the three the summary lacks.
```
