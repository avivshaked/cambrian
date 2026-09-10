# Replay-noise queue (logbook/0052): two more 5,000-s repeats at dt 0.01 on worker 7, one
# after the other. Detached; output to scratch/queue-noise.log; create queue-noise.stop to halt.
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$watch = "$PSScriptRoot\evosim-watch-arms.txt"
$files = 'Editor/EvolutionRun.cs','FluidEnvironment.cs','Ecosystem.cs','PhenotypeBuilder.cs'

$plan = @(
    @{ arm = 'r16dt-01c'; w = 7; next = 'r16dt-01d'; launch = { & "$PSScriptRoot\launch-r16.ps1" -Dt 0.01 -Worker 7 -Suffix d -Seconds 5000 } },
    @{ arm = 'r16dt-01d'; w = 7; next = 'r16dt-01e'; launch = { & "$PSScriptRoot\launch-r16.ps1" -Dt 0.01 -Worker 7 -Suffix e -Seconds 5000 } }
)

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

$deadline = (Get-Date).AddHours(2)
foreach ($e in $plan) {
    while ((Get-Date) -lt $deadline) {
        if (Test-Path "$PSScriptRoot\queue-noise.stop") { "stopped by request"; exit 0 }
        if ((Test-ArmEnded $e.arm) -and (Test-WorkerIdle $e.w)) { break }
        Start-Sleep -Seconds 20
    }
    if ((Get-Date) -ge $deadline) { "deadline"; exit 0 }
    "$(Get-Date -Format HH:mm) $($e.arm) ended -> $($e.next) on w$($e.w)"
    if (Test-WorkerClean $e.w) {
        & $e.launch | Select-Object -Last 1
        Update-Watch $e.arm $e.next
        "$(Get-Date -Format HH:mm) launched $($e.next); watch: $(Get-Content $watch)"
    } else {
        "w$($e.w): hash mismatch - NOT launching $($e.next)"; exit 1
    }
}
"queue done"
