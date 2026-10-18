# The card's pace against the CPU's, read in one sitting with nothing else on the machine: card, CPU, card, CPU,
# each a card-run.ps1 run that resumes -ResumeFrom at -At for -Seconds (the card in single at group 32, the CPU
# on the dynamics engine off the card; 16 host threads on logical 16-31 at BelowNormal), with its own driver
# watch and alive check. -Transport puts the snow's transport on the card in the card runs; the CPU runs keep it
# on the host. Between runs a pause of -PauseMinutes, longer until the heat reads OK and the card is under 60 °C
# (20 minutes more at most). Any run that does not end with "card run ends", or a CPU run whose nvcuda line is
# not "loaded by none", stops the sitting. At the end, reads/pace-calc.py prints the paces; its docstring
# defines them. Paces from different sittings are not compared (src/Evosim.Farm.Gpu/CLAUDE.md).
# Arms <Tag>-card1, -cpu1, -card2, -cpu2 under -RunsRoot.
# Log: scratch/logs/pace-read-<Tag>.log, last line "pace read ends" or "pace read STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$ResumeFrom,
    [Parameter(Mandatory)][string]$Tag,
    [int]$At = 47000,
    [int]$Seconds = 300,
    [int]$PauseMinutes = 3,
    [switch]$Transport,
    [string]$Exe = '',
    [string]$RunsRoot = 'scratch\card\runs')
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runs = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $root $RunsRoot }
$log = Join-Path $root "scratch\logs\pace-read-$Tag.log"
if (Test-Path $log) { Write-Host "$log exists; choose another -Tag or rename the last sitting's log"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }

$plan = @(@("$Tag-card1", 'gpu'), @("$Tag-cpu1", 'cpu'), @("$Tag-card2", 'gpu'), @("$Tag-cpu2", 'cpu'))
Say "pace read begins, the card's transport $(if ($Transport) { 'on' } else { 'off' }): $(($plan | ForEach-Object { "$($_[0]) ($($_[1]))" }) -join ', ')"
$i = 0
foreach ($r in $plan) {
    $arm, $engine = $r
    if ($i -gt 0) {
        $until = (Get-Date).AddMinutes($PauseMinutes)
        $giveUp = (Get-Date).AddMinutes($PauseMinutes + 20)
        while ($true) {
            $now = Get-Date
            if ($now -ge $giveUp) { Say 'pace read STOPPED: the machine did not cool within 20 minutes of the pause'; exit 2 }
            if ($now -ge $until) {
                $card = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
                & pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') | Out-Null
                if ($LASTEXITCODE -eq 0 -and $card -lt 60) { break }
            }
            Start-Sleep -Seconds 10
        }
    }
    $i++
    Say "== $arm on $engine"
    # Typed as an array: a one-element array returned from an if unrolls to a string, and a splatted string
    # passes its characters one by one (a sitting stopped on it at argument binding, 2026-10-08).
    [string[]]$more = @('-Arm', $arm, '-At', $At, '-Seconds', $Seconds, '-ResumeFrom', $ResumeFrom, '-Group', 32,
        '-WallMinutes', 15, '-Engine', $engine, '-RunsRoot', $runs)
    if ($Exe) { $more += @('-Exe', $Exe) }
    if ($Transport -and $engine -eq 'gpu') { $more += '-Transport' }
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'card-run.ps1') @more *>&1 | Out-Null
    $runLog = Join-Path $root "scratch\logs\card-run-$arm.log"
    $lines = if (Test-Path $runLog) { Get-Content $runLog } else { @() }
    $lines | Where-Object { $_ -match 'ended \(|STOPPED|nvcuda|card: |books over|engine dynamics' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Say "pace read STOPPED: $arm did not end"; exit 3 }
    if ($engine -eq 'cpu' -and -not ($lines | Where-Object { $_ -match 'nvcuda: loaded by none' })) { Say "pace read STOPPED: $arm's nvcuda line is not clean"; exit 3 }
}
python -I (Join-Path $root 'scripts\reads\pace-calc.py') $runs @($plan | ForEach-Object { $_[0] }) 2>&1 | ForEach-Object { Say "  $_" }
Say 'pace read ends'
