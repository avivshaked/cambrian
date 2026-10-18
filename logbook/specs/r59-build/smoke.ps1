# Round 59's step 8 CPU smoke, one arm: round 57's world (rounds/env-r57.ps1, seed 1) with every round-59
# piece on (the turning drag in its spin form, the touch and its sense, the anchoring cell with its bud
# weight raised so grips appear within the smoke, the leaf smell's direction), founded and run 2,000 s with
# a digest every 500 steps and a checkpoint every 1,000 s, on logical 16-31. Run once at 1 thread and once
# at 16; logbook/specs/r59-build/smoke-compare.ps1 compares them. The Release farm is built from a clean tree, so
# the manifest's gitCommit names the build. Commits nothing.
param(
    [Parameter(Mandatory)][int]$Threads,
    [double]$Seconds = 2000,
    [int]$WallMinutes = 90,
    [double]$CheckpointEvery = 1000,
    # 'sink': tissue 500 kg/m3 over the water and no neutral volume, so bodies lie on the bed and
    # anchoring parts grip; round 57's own bodies keep to midwater and never touched the rock.
    [string]$Variant = ''
)
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r59-anchor"
$runs = "$R\runs"
$arm = if ($Variant) { "r59smoke-$Variant-t$Threads" } else { "r59smoke-t$Threads" }
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'; $env:MSBUILDDISABLENODEREUSE = '1'
if (git -C $w status --porcelain) { Write-Host "the worktree is not clean; nothing started"; return }
if (Test-Path "$runs\$arm") { Write-Host "$runs\$arm exists; nothing started"; return }

Write-Host "== build at $(git -C $w log --oneline -1) from $(Get-Date -Format HH:mm:ss)"
& $dotnet build "$w\src\Evosim.Farm\Evosim.Farm.csproj" -c Release -m:4 -nologo -v:q -nodeReuse:false 2>&1 | Select-Object -Last 3

$settings = @{
    EVOSIM_TURNING_DRAG = 'spin'; EVOSIM_SPIN_DRAG = 0.5
    EVOSIM_SENSE_BED_TOUCH = 1; EVOSIM_ANCHORING = 1; EVOSIM_ANCHOR_BUD_WEIGHT = 20
    EVOSIM_LEAF_SCENT = 'direction'
    EVOSIM_DIGEST_EVERY = 500
}
if ($Variant -eq 'sink') { $settings.EVOSIM_EXCESS_DENSITY = 500; $settings.EVOSIM_NEUTRAL_VOLUME = 0 }
$t0 = Get-Date
Write-Host "== $arm ($Threads threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" $arm -Launcher "$w\rounds\env-r57.ps1" -Seed 1 -Seconds $Seconds -Threads $Threads -RunsRoot $runs `
    -WallMinutes $WallMinutes -CheckpointEvery $CheckpointEvery -Exe "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -Env $settings |
    Select-String 'configHash|launched|refus|error|DIRTY|gitCommit'

# Logical 16-31, as the suites.
Get-Process Evosim.Farm -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt $t0 } |
    ForEach-Object { $_.ProcessorAffinity = [IntPtr][long]4294901760; Write-Host "affinity 16-31 on pid $($_.Id)" }

$deadline = (Get-Date).AddMinutes($WallMinutes + 5)
while ((Get-Date) -lt $deadline) {
    $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Select-Object -Last 1
    if ($m) {
        $j = Get-Content $m.FullName -Raw | ConvertFrom-Json
        if ($j.status -and $j.status -ne 'running') { Write-Host "$arm ended: $($j.status) $($j.reason) at $(Get-Date -Format HH:mm:ss)"; break }
    }
    Start-Sleep -Seconds 20
}
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
