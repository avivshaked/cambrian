# G4's landing, step 4: the merged tree's card sitting on the coordinator's go, by identity-land.ps1, one card group at a
# time: off at 32, r62cf at 32, the arm "on" at 32 and at 64, then accept-land.ps1. This process pins itself to logical
# 22 at BelowNormal; each card group sets its farms to 22-31 itself. A group that exits non-zero ends the chain, and the
# read is not run. Log beside this script, sit-land.log; each group's output in land-sit\<label>.out.
# -From starts the chain at that step, after a stopped chain's finished groups.
param([ValidateSet('off:32', 'r62cf:32', 'on:32', 'on:64')][string]$From = 'off:32')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-shape-land"
$g = "$R\scratch\shape-build\g5"
$o = "$R\scratch\shape-build\land-sit"
$log = "$g\sit-land.log"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]0x400000
$me.PriorityClass = 'BelowNormal'
New-Item -ItemType Directory -Force $o | Out-Null
"$(Get-Date -Format s) begins at $(git -C $b log --oneline -1)" | Set-Content $log
# Each step is "<set>:<group>": a list of one pair would flatten to its items.
$ok = $true
$all = @('off:32', 'r62cf:32', 'on:32', 'on:64')
foreach ($step in $all[([array]::IndexOf($all, $From))..($all.Count - 1)]) {
    $s = $step.Split(':')
    $label = "$($s[0])-g$($s[1])"
    "$(Get-Date -Format s) $label starts" | Add-Content $log
    & pwsh -NoProfile -File "$g\identity-land.ps1" -Set $s[0] -Group $s[1] *> "$o\$label.out"
    $code = $LASTEXITCODE
    "$(Get-Date -Format s) $label ends, exit $code" | Add-Content $log
    if ($code -ne 0) { "$(Get-Date -Format s) chain stops" | Add-Content $log; $ok = $false; break }
    # The busy check refuses a group launched as the last one's farms are still leaving.
    Start-Sleep -Seconds 20
}
if ($ok) {
    "$(Get-Date -Format s) accept-land starts" | Add-Content $log
    & pwsh -NoProfile -File "$g\accept-land.ps1" *> "$o\accept-land.out"
    "$(Get-Date -Format s) accept-land ends, exit $LASTEXITCODE" | Add-Content $log
}
"$(Get-Date -Format s) ends" | Add-Content $log
