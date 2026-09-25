# Round 48's story film, second chain: waits for chain 1 (pid given as the first argument) to
# end, films seed 1's scenes 9, 11 and 22 and all of seed 3's scenes from the final story, joins
# every clip in story order and copies the film and the story to scratch/owner. One Unity process
# at a time, on the safari worktree's worker 5. Released from the firmware hold by the owner,
# 2026-09-24.
param([int]$WaitFor = 0)
$ErrorActionPreference = 'Continue'
$root = 'D:\Projects\experiments\evolution-simulator'
$wt = "$root\scratch\wt-safari2"
$story = "$root\scratch\story\r48\story.json"
$runs = "$root\runs"
$log = "$root\scratch\story\r48\render-chain-2.log"
Set-Location $wt

if ($WaitFor -gt 0) {
    "$(Get-Date -Format s) waiting for chain 1 (pid $WaitFor)" | Out-File $log -Append
    Wait-Process -Id $WaitFor -ErrorAction SilentlyContinue
}

"$(Get-Date -Format s) start r48-s3" | Out-File $log -Append
& ./scripts/theatre-safari.ps1 r48-s3 -Story $story -Worker 5 -RunsRoot $runs -Folder story-final -DeleteFrames -WallMinutes 240 *>> $log
"$(Get-Date -Format s) r48-s3 exit $LASTEXITCODE" | Out-File $log -Append

"$(Get-Date -Format s) start r48-s1 part 2" | Out-File $log -Append
& ./scripts/theatre-safari.ps1 r48-s1 -Story $story -Scenes 9,11,22 -Worker 5 -RunsRoot $runs -Folder story-final -DeleteFrames -WallMinutes 180 *>> $log
"$(Get-Date -Format s) r48-s1 part 2 exit $LASTEXITCODE" | Out-File $log -Append

$owner = "$root\scratch\owner"
New-Item -ItemType Directory -Force $owner | Out-Null
$film = "$owner\round-48-story.mp4"
"$(Get-Date -Format s) assemble" | Out-File $log -Append
& python "$wt\scripts\story-assemble.py" $story $film "$wt\scratch\safari\r48-s1\story-final" "$wt\scratch\safari\r48-s2\story-final" "$wt\scratch\safari\r48-s3\story-final" *>> $log
"$(Get-Date -Format s) assemble exit $LASTEXITCODE" | Out-File $log -Append
Copy-Item "$root\scratch\story\r48\story.md" "$owner\round-48-story.md" -Force
"$(Get-Date -Format s) chain 2 done" | Out-File $log -Append
