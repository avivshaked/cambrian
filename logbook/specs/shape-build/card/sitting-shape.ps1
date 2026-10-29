# The shape build's card sitting (D255, G4) on the coordinator's go, by identity-shape.ps1, one card group at a time:
#   -Part a: off at 32, the switch-off identity against r62cf's prefix;
#   -Part b: fin at 32, family at 32, fin at 32 again (tag r2), family at 64: the pace gate's timed pair with its
#            baseline repeated in the same sitting, then the family's identity at 32 against 64.
# This process pins itself to logical 22 at BelowNormal; each card group sets its farms to 22-31 itself. A group that
# exits non-zero ends the chain. Log beside this script, sit-<part>.log.
param([ValidateSet('a', 'b')][string]$Part = 'a')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-shape-build"
$o = "$R\scratch\shape-build\card"
$log = "$o\sit-$Part.log"
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]0x400000
$me.PriorityClass = 'BelowNormal'
New-Item -ItemType Directory -Force "$o\sit" | Out-Null
"$(Get-Date -Format s) begins at $(git -C $b log --oneline -1)" | Set-Content $log
# Each step is "<set>:<group>:<tag>": a list of one pair would flatten to its items.
$steps = if ($Part -eq 'a') { @('off:32:') } else { @('fin:32:', 'family:32:', 'fin:32:r2', 'family:64:') }
foreach ($step in $steps) {
    $s = $step.Split(':')
    $label = "$($s[0])-g$($s[1])$($s[2])"
    "$(Get-Date -Format s) $label starts" | Add-Content $log
    # An empty -Tag is not handed to a native pwsh, which would drop it and leave the parameter without its value.
    $tagArgs = if ($s[2]) { @('-Tag', $s[2]) } else { @() }
    & pwsh -NoProfile -File "$o\identity-shape.ps1" -Set $s[0] -Group $s[1] @tagArgs *> "$o\sit\$label.out"
    $code = $LASTEXITCODE
    "$(Get-Date -Format s) $label ends, exit $code" | Add-Content $log
    if ($code -ne 0) { "$(Get-Date -Format s) chain stops" | Add-Content $log; break }
}
"$(Get-Date -Format s) ends" | Add-Content $log
