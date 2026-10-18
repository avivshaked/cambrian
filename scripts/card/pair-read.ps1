# Two seeds on the card at once against one alone, read in one sitting with nothing else on the machine. Every
# run is a card-run.ps1 resume of -ResumeFrom at -At for -Seconds, single precision at group 32, with the snow's
# transport on the card, each with its own driver watch and alive check. The slots, in order:
#   solo16  one run alone, 16 farm threads on logical 16-31
#   pair1   two runs at once: A with 8 threads on 16-23, then B with 8 threads on 24-31 beside A (-Beside), started
#           once A's farm is pinned and its run.json read, so the two overlap but for B's few seconds of checks
#   solo8   one run alone, 8 threads on 16-23: what halving the host threads costs
#   pair2   the pair again
# Between slots a pause of -PauseMinutes, longer until the heat reads OK and the card is under 60 °C (20 minutes
# more at most). Any run that does not end with "card run ends" stops the sitting; a pair whose B is refused
# stops A (stop-arm) and the sitting. At the end, reads/pace-calc.py prints every run's pace (its docstring
# defines it). The throughput of a pair is the sum of its two paces over solo16's.
# Arms <Tag>-solo16, <Tag>-pair1a, <Tag>-pair1b, <Tag>-solo8, <Tag>-pair2a, <Tag>-pair2b under -RunsRoot.
# Log: scratch/logs/pair-read-<Tag>.log, last line "pair read ends" or "pair read STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$ResumeFrom,
    [Parameter(Mandatory)][string]$Tag,
    [int]$At = 47000,
    [int]$Seconds = 600,
    [int]$PauseMinutes = 3,
    [int]$WallMinutes = 20,
    [string]$Exe = '',
    [string]$RunsRoot = 'scratch\card\runs')
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runs = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $root $RunsRoot }
$logs = Join-Path $root 'scratch\logs'
$log = Join-Path $logs "pair-read-$Tag.log"
if (Test-Path $log) { Write-Host "$log exists; choose another -Tag"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
$cardRun = Join-Path $PSScriptRoot 'card-run.ps1'
$all16 = 4294901760; $low8 = 16711680; $high8 = 4278190080   # logical 16-31, 16-23, 24-31 (decimal)

# The arguments of one run, typed as an array: a splatted string passes its characters one by one.
function ArgsOf([string]$arm, [int]$threads, [int64]$mask, [string]$beside) {
    [string[]]$a = @('-NoProfile', '-File', $cardRun, '-Arm', $arm, '-At', $At, '-Seconds', $Seconds, '-ResumeFrom', $ResumeFrom,
        '-Group', 32, '-WallMinutes', $WallMinutes, '-Transport', '-Threads', $threads, '-Mask', $mask, '-RunsRoot', $runs)
    if ($Exe) { $a += @('-Exe', $Exe) }
    if ($beside) { $a += @('-Beside', $beside) }
    return , $a
}
function Lines([string]$arm) { $f = Join-Path $logs "card-run-$arm.log"; if (Test-Path $f) { Get-Content $f } else { @() } }
function Report([string]$arm) {
    Lines $arm | Where-Object { $_ -match 'ended \(|STOPPED|card: |books over|pinned|beside' } | ForEach-Object { Say "  $_" }
    return [bool](Lines $arm | Where-Object { $_ -match 'card run ends$' })
}
function Pause-Cool {
    $until = (Get-Date).AddMinutes($PauseMinutes)
    $giveUp = (Get-Date).AddMinutes($PauseMinutes + 20)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Say 'pair read STOPPED: the machine did not cool within 20 minutes of the pause'; exit 2 }
        if ($now -ge $until) {
            $card = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
            & pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') | Out-Null
            if ($LASTEXITCODE -eq 0 -and $card -lt 60) { return }
        }
        Start-Sleep -Seconds 10
    }
}
function Solo([string]$arm, [int]$threads, [int64]$mask) {
    Say "== $arm alone, $threads threads on mask $mask"
    $a = ArgsOf $arm $threads $mask ''
    & pwsh @a *>&1 | Out-Null
    if (-not (Report $arm)) { Say "pair read STOPPED: $arm did not end"; exit 3 }
}
function Pair([string]$armA, [string]$armB) {
    Say "== $armA (8 threads, 16-23) and $armB (8 threads, 24-31) together"
    $aA = ArgsOf $armA 8 $low8 ''
    $pA = Start-Process pwsh -ArgumentList $aA -WindowStyle Hidden -PassThru
    # B starts once A's farm is pinned and A has read its run.json, so B's busy refusal finds A's farm and watch.
    $d = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $d -and -not (Lines $armA | Where-Object { $_ -match "transport in run.json|card run STOPPED" })) {
        if ($pA.HasExited) { break }
        Start-Sleep -Seconds 1
    }
    if (-not (Lines $armA | Where-Object { $_ -match 'transport in run.json' }) -or (Lines $armA | Where-Object { $_ -match 'STOPPED' })) {
        [void](Report $armA); Say "pair read STOPPED: $armA did not start"; exit 3
    }
    Say "  $armA running; $armB starts beside it"
    $aB = ArgsOf $armB 8 $high8 $armA
    $pB = Start-Process pwsh -ArgumentList $aB -WindowStyle Hidden -PassThru
    $d = (Get-Date).AddMinutes($WallMinutes + 6)
    while ((Get-Date) -lt $d -and -not ($pA.HasExited -and $pB.HasExited)) { Start-Sleep -Seconds 5 }
    $okA = Report $armA; $okB = Report $armB
    if (-not $okB -and -not $pA.HasExited) {
        & (Join-Path $root 'scripts\stop-arm.ps1') $armA -RunsRoot $runs -Reason manual-other 2>&1 | ForEach-Object { Say "  stop: $_" }
    }
    if (-not ($okA -and $okB)) { Say "pair read STOPPED: $(if (-not $okA) { $armA } else { $armB }) did not end"; exit 3 }
}

Say "pair read begins: $Tag, from $At s for $Seconds s each, transport on the card, group 32"
Solo "$Tag-solo16" 16 $all16
Pause-Cool
Pair "$Tag-pair1a" "$Tag-pair1b"
Pause-Cool
Solo "$Tag-solo8" 8 $low8
Pause-Cool
Pair "$Tag-pair2a" "$Tag-pair2b"
$arms = @("$Tag-solo16", "$Tag-pair1a", "$Tag-pair1b", "$Tag-solo8", "$Tag-pair2a", "$Tag-pair2b")
python -I (Join-Path $root 'scripts\reads\pace-calc.py') $runs @arms 2>&1 | ForEach-Object { Say "  $_" }
Say 'pair read ends'
