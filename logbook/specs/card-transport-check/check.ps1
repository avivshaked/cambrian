# D126's condition (HANDOFF queue item 3): the snow's transport on the card against round 50's own
# record, by day with nothing else on the machine. Each of round 50's seeds is resumed on main's
# exe from 5,000, 15,000 and 25,000 s for 1,000 s, once with EVOSIM_GPU_TRANSPORT (arm suffix g)
# and once without (c), alternated so that neither mode always runs second. Main is not round 50's
# build, so each resume runs under -AllowSourceMismatch; the CPU's windows are the control (a
# difference in both is the build's, in g alone the card's). compare.py then compares each window
# with round 50's own rows, lineage and checkpoint payloads and prints the paces.
param([string]$Source = 'r50', [string]$Seeds = '1,2,3', [string]$At = '5000,15000,25000', [double]$Span = 1000, [int]$Threads = 16)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$out = "$main\scratch\r51-check"
$runs = "$out\runs"
New-Item -ItemType Directory -Force $runs | Out-Null
function Say([string]$t) { Write-Host "$(Get-Date -Format 'HH:mm:ss') $t" }
function WaitEnd([string]$arm, [datetime]$since) {
    $d = (Get-Date).AddMinutes(90)
    while ((Get-Date) -lt $d) {
        $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Where-Object { $_.Directory.CreationTime -ge $since.AddMinutes(-1) } | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($m) { $j = Get-Content $m.FullName -Raw | ConvertFrom-Json; if ($j.status -ne 'running') { return $j } }
        elseif ((Get-Date) -gt $since.AddMinutes(3)) { return $null }
        Start-Sleep -Seconds 15
    }
    return $null
}
Set-Location $main
$seedList = $Seeds -split ',' | ForEach-Object { [int]$_ }
$atList = $At -split ',' | ForEach-Object { [double]$_ }
$flip = $false
foreach ($s in $seedList) {
    # round 50's own arm, reachable from compare.py's runs root
    $junction = "$runs\$Source-s$s"
    if (-not (Test-Path $junction)) { cmd /c mklink /J "$junction" "$main\runs\$Source-s$s" | Out-Null }
    foreach ($a in $atList) {
        $modes = if ($flip) { @('c', 'g') } else { @('g', 'c') }
        $flip = -not $flip
        foreach ($mode in $modes) {
            while (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 30 }
            $arm = "$($Source)ck-s$s-$([int]$a)$mode"
            $envs = @{}
            if ($mode -eq 'g') { $envs['EVOSIM_GPU_TRANSPORT'] = 1 }
            Say "== $arm from $a s"
            $since = Get-Date
            & "$main\scripts\run-farm.ps1" $arm -ResumeFrom "$main\runs\$Source-s$s" -At $a -Seconds ($a + $Span) -Threads $Threads `
                -RunsRoot $runs -WallMinutes 80 -AllowSourceMismatch -Env $envs 6>&1 | Select-String 'refus|error|resumedFrom' | ForEach-Object { Say "  $_" }
            $j = WaitEnd $arm $since
            if ($j) { Say "  $($j.status) $($j.reason), transport $($j.transport)" } else { Say "  no ending read" }
        }
    }
}
Say "== compare"
python "$main\logbook\specs\card-transport-check\compare.py" $runs --source $Source --seeds $Seeds --at $At
Say "== done"