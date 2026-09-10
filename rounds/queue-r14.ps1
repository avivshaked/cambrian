# Round 14 queue: as each round-13 worker ends cleanly, hash-check it and launch the next round-14 arm on it.
# Runs in the background; prints one line per action. Stop it by deleting scratch/queue-r14.stop's absence... i.e. create scratch/queue-r14.stop.
$root = Split-Path $PSScriptRoot -Parent
$watch = "$PSScriptRoot\evosim-watch-arms.txt"
$queue = New-Object System.Collections.Queue
foreach ($p in @(@(5,1),@(10,1),@(5,2),@(10,2),@(5,3),@(10,3),@(5,4),@(10,4),@(5,5),@(10,5))) { $queue.Enqueue($p) }
$busy = @{ 2='r13a-s1'; 3='r13b-s1'; 4='r13a-s2'; 5='r13b-s2'; 6='r13a-s3' }
$files = 'Editor/EvolutionRun.cs','FluidEnvironment.cs','Ecosystem.cs','PhenotypeBuilder.cs'
function Worker-Clean([int]$w) {
    foreach ($f in $files) {
        $ref = Get-ChildItem -Recurse "$root/unity/Assets/Evosim" -Filter (Split-Path $f -Leaf) | Select-Object -First 1
        $p = $ref.FullName.Replace('evolution-simulator\unity\', "evolution-simulator\unity-w$w\")
        if (-not (Test-Path $p) -or (Get-FileHash $p).Hash -ne (Get-FileHash $ref.FullName).Hash) { return $false }
    }
    return $true
}
function Worker-Idle([int]$w) {
    $procs = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like "*unity-w$w*" }
    return ($null -eq $procs -or @($procs).Count -eq 0)
}
while ($queue.Count -gt 0) {
    if (Test-Path "$PSScriptRoot/queue-r14.stop") { "queue stopped by request; $($queue.Count) arms unlaunched"; break }
    foreach ($w in @($busy.Keys)) {
        $arm = $busy[$w]
        $rep = "$root/runs/$arm.md"
        if ((Test-Path $rep) -and (Select-String -Path $rep -Pattern '^\*\*Ended:\*\*' -Quiet) -and (Worker-Idle $w)) {
            if (-not (Worker-Clean $w)) { "w${w}: $arm ended but the worker's files do not match unity/ - NOT launching; refresh it by hand"; $busy.Remove($w); continue }
            if ($queue.Count -eq 0) { $busy.Remove($w); break }
            $p = $queue.Dequeue()
            $name = "r14c$($p[0])-s$($p[1])"
            "$(Get-Date -Format HH:mm) w${w}: $arm ended -> launching $name"
            & "$PSScriptRoot/launch-r14.ps1" -Clearance $p[0] -Seed $p[1] -Worker $w | Select-Object -Last 1
            $list = (Get-Content $watch -ErrorAction SilentlyContinue) -split ' ' | Where-Object { $_ }
            Set-Content -Path $watch -Value (($list + $name) -join ' ')
            $busy[$w] = $name
        }
    }
    Start-Sleep -Seconds 120
}
"queue done"
