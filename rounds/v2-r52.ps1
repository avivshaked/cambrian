# Round 52's V2 (logbook/0127): seed 1's checkpoint at 15,000 s checked member by member, and a
# resume from it to 16,000 s compared with the recording. Writes `verify-checkpoint exit N` and
# `compare-det exit N`, which scripts/reads/r52-read.py --v2-log reads. rounds/queue-r52.ps1 has
# farm-queue.ps1 run it with -After once the last seed has ended; its output goes to
# scratch/logs/r52-v2.log.
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$w = "$main\scratch\wt-r52"
$runs = "$main\runs"
$out = "$main\scratch\r52-v2"; New-Item -ItemType Directory -Force $out | Out-Null
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$deadline = (Get-Date).AddHours(16)
while ((Get-Date) -lt $deadline -and (Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 60 }
& {
    $run = Get-ChildItem "$runs\r52-s1" -Directory | Sort-Object Name | Select-Object -Last 1
    $ck = Get-ChildItem "$($run.FullName)\checkpoints" | Where-Object { $_.Name -like '000015000*' } | Select-Object -First 1
    if (-not $ck) { Write-Host "no 15,000 s checkpoint under $($run.FullName)"; return }
    Write-Host "== V2 at $(Get-Date -Format HH:mm): $($ck.FullName)"
    & $exe --verify-checkpoint $ck.FullName 30 "$out\v2-verify" 8; Write-Host "verify-checkpoint exit $LASTEXITCODE"
    Set-Location $w
    & "$w\scripts\run-farm.ps1" r52-s1R -ResumeFrom $run.FullName -At 15000 -Seconds 16000 -Threads 16 -RunsRoot $runs -WallMinutes 90 | Select-String 'configHash|launched|refus|error|resumed'
    Start-Sleep -Seconds 60
    $d = (Get-Date).AddMinutes(100)
    while ((Get-Date) -lt $d) {
        $m = Get-ChildItem "$runs\r52-s1R\*\run.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($m -and ((Get-Content $m.FullName -Raw | ConvertFrom-Json).status -ne 'running')) { break }
        Start-Sleep -Seconds 30
    }
    Set-Location $main
    python "$w\scripts\compare-det.py" r52-s1 r52-s1R --allow-partial --skip wall harnessBodySteps fluidLinkSteps; Write-Host "compare-det exit $LASTEXITCODE"
    Write-Host "== V2 done at $(Get-Date -Format HH:mm)"
} *> "$main\scratch\logs\r52-v2.log"
