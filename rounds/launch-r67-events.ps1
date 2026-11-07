# Round 67's three event shots, as the owner ruled on 2026-11-07 for the film (scratch/story-video/round-67-202611061206,
# shots-2-owner.md): the card resumes a world from the checkpoint before each shot and writes checkpoints every 30 s,
# so the film's host window can start from the run's own state a few seconds before each event. One card group of
# three worlds, since a group holds two worlds or more and -Seconds is one length for all of them; checkpoints fall on
# whole multiples of -CheckpointEvery, and 30 s puts one inside each event's window:
#   r67ev-24b-6202: r67r1-6202 from 19,500 s, a checkpoint at 20,400 s, before the birth at 20,425.5 s (shot 2.4b);
#   r67ev-64a-6202: r67-6202 from 7,500 s, the state r67r1-6202 began from, a checkpoint at 8,550 s, before the birth at
#     8,572 s (shot 6.4a);
#   r67ev-68a-6202: r67r1-6202 from 10,500 s, a checkpoint at 11,430 s, before the portrait of 13929 at 11,448 s (6.8a).
# Each runs 1,055 s, so the longest reaches 8,555 s. They run as round 67's worlds did: wt-card-kit's Release farm (its
# Core, Dynamics, Farm and Farm.Gpu unchanged since e79fa0fd, the build r67r1 ran on), the snow's transport on the
# card, the scent on the host, group 32, no exchange. A resume repeats a card run only on the card; each one's stats
# are compared with its source's over the span before the film uses it. Runs under runs/, beside their sources, since
# the film finds every shot's run under one root. Start it detached and hidden, on the coordinator's go.
$R = 'D:\Projects\experiments\evolution-simulator'
$kit = "$R\scratch\wt-card-kit"
$exe = "$kit\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$r1 = "$R\runs\r67r1-6202\2026-11-05-220032-029c8c74"
$r0 = "$R\runs\r67-6202\2026-11-05-210155-029c8c74"
$g = @('-NoProfile', '-File', "$kit\scripts\card\card-group.ps1", '-Name', 'r67ev',
       '-Arms', 'r67ev-24b-6202,r67ev-64a-6202,r67ev-68a-6202', '-At', '19500,7500,10500', '-Seconds', 1055,
       '-ResumeFrom', "$r1,$r0,$r1", '-CheckpointEvery', 30, '-Group', 32, '-WallMinutes', 90, '-Transport',
       '-Threads', 12, '-Mask', 4293918720, '-HeatStop', '-WaitForManifestSeconds', 600, '-RunsRoot', "$R\runs",
       '-Exe', $exe)
& pwsh @g
if ($LASTEXITCODE -ne 0) { "group r67ev ended with exit $LASTEXITCODE"; exit $LASTEXITCODE }
"group r67ev ended"
