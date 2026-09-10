# Launch queue, 2026-09-03 evening: as each named arm ends and its worker is idle, refresh the
# worker, hash-check it, launch the next arm on it, and swap the names in the monitor's watch
# list. Runs detached; output to scratch/queue-next.log. Create scratch/queue-next.stop to halt.
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$watch = "$PSScriptRoot\evosim-watch-arms.txt"
$files = 'Editor/EvolutionRun.cs','FluidEnvironment.cs','Ecosystem.cs','PhenotypeBuilder.cs'

# Each entry: when <arm> has ended and worker <w> is idle, run <launch>, then swap names.
$plan = @(
    @{ arm = 'r16dt-02';  w = 7; next = 'r16dt-01';  launch = { & "$PSScriptRoot\launch-r16.ps1" -Dt 0.01 -Worker 7 } },
    @{ arm = 'r14c5-s1';  w = 3; next = 'r15i-c1';   launch = { & "$PSScriptRoot\launch-r15.ps1" -Clearance 1 -Worker 3 } },
    @{ arm = 'r14c10-s1'; w = 2; next = 'r14c10-s3'; launch = { & "$PSScriptRoot\launch-r14.ps1" -Clearance 10 -Seed 3 -Worker 2 } },
    @{ arm = 'r14c5-s2';  w = 5; next = 'r14c5-s3';  launch = { & "$PSScriptRoot\launch-r14.ps1" -Clearance 5 -Seed 3 -Worker 5 } }
)

function Worker-Clean([int]$w) {
    foreach ($f in $files) {
        $ref = Get-ChildItem -Recurse "$root\unity\Assets\Evosim" -Filter (Split-Path $f -Leaf) | Select-Object -First 1
        $p = $ref.FullName.Replace('evolution-simulator\unity\', "evolution-simulator\unity-w$w\")
        if (-not (Test-Path $p) -or (Get-FileHash $p).Hash -ne (Get-FileHash $ref.FullName).Hash) { return $false }
    }
    return $true
}
function Worker-Idle([int]$w) {
    $procs = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like "*unity-w$w*" }
    return ($null -eq $procs -or @($procs).Count -eq 0)
}
function Arm-Ended([string]$a) {
    return (Test-Path "$root\runs\$a.md") -and [bool](Select-String -Path "$root\runs\$a.md" -Pattern '^\*\*Ended' -Quiet)
}
function Swap-Watch([string]$old, [string]$new) {
    $l = @((Get-Content $watch -ErrorAction SilentlyContinue) -split ' ' | Where-Object { $_ -and $_ -ne $old }) + $new
    Set-Content -Path $watch -Value ($l -join ' ')
}

$pending = [System.Collections.ArrayList]@($plan)
$deadline = (Get-Date).AddHours(6)
while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
    if (Test-Path "$PSScriptRoot\queue-next.stop") { "stopped by request; $($pending.Count) pending"; break }
    foreach ($e in @($pending)) {
        if ((Arm-Ended $e.arm) -and (Worker-Idle $e.w)) {
            "$(Get-Date -Format HH:mm) $($e.arm) ended -> refreshing w$($e.w) for $($e.next)"
            & "$root\scripts\new-worker.ps1" -Workers $e.w 2>&1 | Select-Object -First 1
            if (Worker-Clean $e.w) {
                & $e.launch | Select-Object -Last 1
                Swap-Watch $e.arm $e.next
                "$(Get-Date -Format HH:mm) launched $($e.next) on w$($e.w); watch: $(Get-Content $watch)"
            } else {
                "w$($e.w): hash mismatch after refresh - NOT launching $($e.next)"
            }
            $pending.Remove($e)
        }
    }
    Start-Sleep -Seconds 30
}
"queue done"
