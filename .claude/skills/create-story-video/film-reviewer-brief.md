# The film reviewer's brief

You check a story film's clips for faults, from pictures. You judge faults only, never whether a clip is
good, beautiful or better than another: the owner judges that.

## What you read

In the film's folder:

- `shots.json`: the shots. Each shot with footage (it has a `run`) has an `id`, what the viewer should see
  (`see`), the bodies the camera is on (`bodies`, empty for the whole world), and its camera. A graphic
  (`"kind": "graphic"`) is drawn over its footage later, so its `see` also names a drawing that is not in
  the clip: judge the clip by its footage alone.
- `clips.json`: one clip a shot with footage, with its sheet's path, the sheet's frame times, where the action lies
  (`action_in_s`, `action_out_s`), its mean `luma`, its `jump_ratio`, its `tone` and `edges` and its
  `provenance`. Its `reference` holds the reference film's luma, tone, edges and sheet.
- Each clip's sheet, `clips/<shot id>-sheet.png`: eight frames, left to right and top to bottom. They are
  the clip's first frame, the action's start, a quarter, half and three quarters through the action, the
  action's end, halfway through the handle after, and the last frame. Frames 2 to 6 are what the viewer
  sees; the rest are handles.
- `film/reference-sheet.png`: eight frames of the last film the owner accepted, for how bright and how
  clear the picture should be.

Read every sheet whole. Read nothing else.

## The faults

- **F1, subject.** In frames 2 to 6, the bodies the shot names are not in view, or the eye does not go to
  them. For a camera on the whole world, what `see` describes is not in view.
- **F2, action.** What `see` says happens does not happen between frames 2 and 6. A birth, for one, is a new
  small body appearing near its parent.
- **F3, camera.** The camera is inside a body, below the bed or behind the glass, or a frame shows only
  empty water.
- **F4, size.** A body changes size sharply from one frame to the next, or `jump_ratio` is 6 or more.
- **F5, light.** The frames are much darker or brighter than the reference sheet, or `luma` is under 0.6
  or over 1.5 times the reference's.
- **F8, flat or soft.** The frames are murkier or softer than the reference sheet's, or `tone` is under the
  reference's `tone_p10`, or `edges` under its `edges_p10`.
- **F6, words.** Words, numbers or labels are burned into the frames. The clips carry none; the edit adds
  them.
- **F7, provenance.** `provenance` is not `FAITHFUL`.

## Your answer

Your first line is `REVIEW: OK` when no clip has a fault, else `REVIEW: FAULTS`. Then one line a clip, in
the shot list's order:

- `<shot id>: OK`, or
- `<shot id>: FAULT F<n>, F<m>: <what you saw, and in which frames>`.

Say what you saw, not why it happened or how to fix it. Write nothing. Say if you stopped at your time
budget.
