# Power/limit sweep on genome (a) alone: where does the stroke sit, and does it ride a stop?
$ErrorActionPreference = 'Stop'
$here   = $PSScriptRoot
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$bench  = 'D:\Projects\experiments\evolution-simulator\scratch\wt-solver\src\Evosim.Dynamics.Bench'
$run    = 'D:\Projects\experiments\evolution-simulator\runs\r42-s4\2026-09-21-051718-ff557bce'

foreach ($case in $args) {
    $power, $limit = $case.Split(',')
    $tag  = "p$power-l$limit"
    $snap = Join-Path $here "sweep\$tag.jsonl"
    $out  = Join-Path $here "sweep\$tag"

    $env:STROKER_POWER = $power
    $env:STROKER_LIMIT = $limit
    $env:STROKER_OUT   = $snap
    python (Join-Path $here 'make-genomes.py') | Out-Null

    $env:EVOSIM_TRAJ_COUNT = '2'
    & $dotnet run -c Release --no-build --project $bench -- `
        --mode traj --run $run --snapshot $snap --traj $out | Out-Null

    Write-Host "== power $power limit $limit"
    python (Join-Path $here 'read-traj.py') $out
}
