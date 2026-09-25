# Round 48's story film, first chain: seed 2's four scenes, then seed 1's ten scenes that do
# not depend on seed 3's final numbers (9, 11 and 22 wait for it). One Unity process at a time,
# on the safari worktree's worker 5. Released from the firmware hold by the owner, 2026-09-24.
$ErrorActionPreference = 'Continue'
$wt = 'D:\Projects\experiments\evolution-simulator\scratch\wt-safari2'
$story = 'D:\Projects\experiments\evolution-simulator\scratch\story\r48\story.json'
$runs = 'D:\Projects\experiments\evolution-simulator\runs'
$log = 'D:\Projects\experiments\evolution-simulator\scratch\story\r48\render-chain-1.log'
Set-Location $wt

"$(Get-Date -Format s) start r48-s2" | Out-File $log -Append
& ./scripts/theatre-safari.ps1 r48-s2 -Story $story -Worker 5 -RunsRoot $runs -Folder story-final -DeleteFrames -WallMinutes 120 *>> $log
"$(Get-Date -Format s) r48-s2 exit $LASTEXITCODE" | Out-File $log -Append

"$(Get-Date -Format s) start r48-s1 part 1" | Out-File $log -Append
& ./scripts/theatre-safari.ps1 r48-s1 -Story $story -Scenes 1,2,3,8,12,13,17,18,19,21 -Worker 5 -RunsRoot $runs -Folder story-final -DeleteFrames -WallMinutes 300 *>> $log
"$(Get-Date -Format s) r48-s1 part 1 exit $LASTEXITCODE" | Out-File $log -Append
"$(Get-Date -Format s) chain 1 done" | Out-File $log -Append
