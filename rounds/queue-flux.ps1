# Flux-instrument queue, 2026-09-03 night: when r14c10-s2 ends (keeping the machine at five
# arms), launch r14c10-s1's world at dt 0.02 on w4 - already refreshed to the det in / det out
# build and compile-checked - and swap the names in the monitor's watch list. Detached;
# output to scratch/queue-flux.log; create scratch/queue-flux.stop to halt.
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$watch = "$PSScriptRoot\evosim-watch-arms.txt"
$files = 'Editor/EvolutionRun.cs','FluidEnvironment.cs','Ecosystem.cs','PhenotypeBuilder.cs'

function Test-WorkerClean([int]$w) {
    foreach ($f in $files) {
        $ref = Get-ChildItem -Recurse "$root\unity\Assets\Evosim" -Filter (Split-Path $f -Leaf) | Select-Object -First 1
        $p = $ref.FullName.Replace('evolution-simulator\unity\', "evolution-simulator\unity-w$w\")
        if (-not (Test-Path $p) -or (Get-FileHash $p).Hash -ne (Get-FileHash $ref.FullName).Hash) { return $false }
    }
    return $true
}
function Test-WorkerIdle([int]$w) {
    $procs = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like "*unity-w$w*" }
    return ($null -eq $procs -or @($procs).Count -eq 0)
}
function Test-ArmEnded([string]$a) {
    return (Test-Path "$root\runs\$a.md") -and [bool](Select-String -Path "$root\runs\$a.md" -Pattern '^\*\*Ended' -Quiet)
}
function Update-Watch([string]$old, [string]$new) {
    $l = @((Get-Content $watch -ErrorAction SilentlyContinue) -split ' ' | Where-Object { $_ -and $_ -ne $old }) + $new
    Set-Content -Path $watch -Value ($l -join ' ')
}

$deadline = (Get-Date).AddHours(4)
while ((Get-Date) -lt $deadline) {
    if (Test-Path "$PSScriptRoot\queue-flux.stop") { "stopped by request"; exit 0 }
    if ((Test-ArmEnded 'r14c10-s2') -and (Test-WorkerIdle 6) -and (Test-WorkerIdle 4)) { break }
    Start-Sleep -Seconds 30
}
if ((Get-Date) -ge $deadline) { "deadline"; exit 0 }
"$(Get-Date -Format HH:mm) r14c10-s2 ended -> r14c10-s1-flux on w4"
if (Test-WorkerClean 4) {
    & "$PSScriptRoot\launch-r14.ps1" -Clearance 10 -Seed 1 -Worker 4 -Dt 0.02 -Suffix '-flux' -Seconds 15000 | Select-Object -Last 1
    Update-Watch 'r14c10-s2' 'r14c10-s1-flux'
    "$(Get-Date -Format HH:mm) launched r14c10-s1-flux; watch: $(Get-Content $watch)"
} else {
    "w4: hash mismatch - NOT launching"; exit 1
}
"queue done"
