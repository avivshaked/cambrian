<#
  film-guard.ps1: the heat guard beside a story film's filming pass. A copy of scratch\theatre-realism\render-guard.ps1 (the theatre
  lane's guard, which belongs to that session and is unchanged), made on 2026-11-07. Sampling, the D258 stops, -CardTerms, -PaceCheck,
  -Kill and the close watch are as there: about once a second it reads the CPU's temperature from MSI Afterburner's shared memory
  (read-only), the GPU's from nvidia-smi and the System log's nvlddmkm events, and it stops on one CPU reading of 98 C or more,
  every reading at 95 C or more for a minute (D258), GPU 83 C or more, any nvlddmkm event, or the CPU unreadable for 10 s.
  Bounded: it ends when -Done appears or at -Minutes.
  What differs from the original:
    -StopFile <path> (mandatory): a trip writes it, with the time and the reason inside. Nothing else writes a STOP.
    -Workers <regex>: the Unity processes a trip kills, and the ones the close watch spots; default the film's render worker 7,
      either slash.
    -KillTree <pid>: on a trip, before the sweeps, taskkill /PID <pid> /T /F (the pass's `film.py run` job), so no window starts again.
    -FarmExe <regex>: after the tree kill, every Evosim.Farm.exe whose ExecutablePath matches is stopped too (a process whose parent
      has gone is missed by the tree kill).
    -TestTrip: trips on the first sample with the reason 'test trip'. For the test alone.
  The order on a trip: -StopFile, tree kill, Unity by -Workers, Evosim.Farm.exe by -FarmExe, -Kill, the STOPPED line.
    film-guard.ps1 -Done <marker> -Log <file> -StopFile <path> [-KillTree <pid>] [-FarmExe <regex>] [-Workers <regex>] [-Minutes 240]
#>
#   With -PaceCheck (renders beside a running card world, coordinator 20:05) it also runs scripts\pace-check.py every
#   2 min, and stops the same way when that flags a running world as SLOW (exit 2), or when it fails (any other exit) on two
#   looks in a row, the second taken at once after the first ("guard blind"). It reads every run under runs\, unless
#   EVOSIM_GUARD_PACE_RUNS names the run folders (';' between them): a card sitting outside runs\, such as the forest run under
#   scratch\plants-work\runs, is otherwise not watched at all (the coordinator's catch, 2026-11-06).
#   Close watch (coordinator, 20:5x): every reading is logged 3 s apart for the first minute after the guard starts and after each
#   new Unity process of this session's appears. -Kill names a further command-line pattern stopped beside Unity on a trip (the
#   Core suite's dotnet processes in the theatre worktree, when the guard watches a suite).
#   -FilmTerms: the coordinator's terms for the theatre and film windows of 2026-11-07, between D258's and the card terms: two
#   readable readings in a row at 90 C or more, or one reading at 95 C or more, as well as D258's stops. An unreadable reading neither
#   adds to the run nor resets it, as for -CardTerms. -CardTerms and -FilmTerms together are refused at start (a log line, exit 2,
#   nothing watched). film.py's setting guard "film terms" passes it.
#   -TestReadings <list>: for the tests alone. A comma-separated list of CPU readings such as "70,90,-1,91" (-1 is unreadable) taken in
#   order in place of the sensor; the GPU reads 40, no nvlddmkm event is looked for, and the guard ends as if -Done had appeared when
#   the list runs out.
#   -CardTerms: a stricter stop as well, one CPU reading of 90 C or more or two readable readings in a row at 85 C or more. Not the
#   rule: the standing limits are D258's above, which are the card stop's own; this is for a run that wants a wider margin, and is off
#   unless named.
param([Parameter(Mandatory)][string]$Done, [Parameter(Mandatory)][string]$Log, [Parameter(Mandatory)][string]$StopFile, [int]$Minutes = 240, [switch]$PaceCheck, [string]$Kill = '', [switch]$CardTerms, [switch]$FilmTerms, [string]$TestReadings = '',
      [string]$Workers = 'wt-theatre-look[\\/]unity-w7', [int]$KillTree = 0, [string]$FarmExe = '', [switch]$TestTrip)
$r = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path   # the repository root, three levels up from .claude\skills\create-story-video
$start = Get-Date
if ($CardTerms -and $FilmTerms) { "$(Get-Date -Format s) guard refused: -CardTerms and -FilmTerms together; nothing is watched" | Add-Content $Log; exit 2 }
$tr = @(if ($TestReadings -ne '') { $TestReadings -split ',' | ForEach-Object { [int]$_.Trim() } }); $ti = 0
$end = $start.AddMinutes($Minutes)
Add-Type -AssemblyName System.IO.MemoryMappedFiles -ErrorAction SilentlyContinue

function CpuTemp {
    try {
        $mm = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('MAHMSharedMemory', [System.IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
        $va = $mm.CreateViewAccessor(0, 0, [System.IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
        try {
            if ($va.ReadUInt32(0) -ne 0x4D41484D) { return -1 }
            $headerSize = $va.ReadUInt32(8); $entries = $va.ReadUInt32(12); $entrySize = $va.ReadUInt32(16)
            $hottest = -1
            $bytes = New-Object byte[] 260
            for ($i = 0; $i -lt $entries; $i++) {
                $at = $headerSize + $i * $entrySize
                [void]$va.ReadArray($at, $bytes, 0, 260)
                $name = [Text.Encoding]::ASCII.GetString($bytes).Split([char]0)[0]
                if ($name -match '(?i)^CPU\d* ?temperature$|^CPU temperature') {
                    $v = $va.ReadSingle($at + 5 * 260)
                    if ($v -gt 0 -and $v -lt 150 -and $v -gt $hottest) { $hottest = $v }
                }
            }
            return [int][math]::Round($hottest, 0)
        } finally { $va.Dispose(); $mm.Dispose() }
    } catch { return -1 }
}
function GpuTemp {
    try { return [int]((& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits 2>$null | Select-Object -First 1).Trim()) } catch { return -1 }
}

"$(Get-Date -Format s) guard start, 1 s samples: CPU 98 C once or 95 C held 60 s (D258), GPU 83 C, nvlddmkm, CPU unreadable 10 s$(if ($TestTrip) { '; TEST TRIP: trips on the first sample' })$(if ($CardTerms) { '; card terms: CPU 90 C once or 85 C twice in a row' })$(if ($FilmTerms) { '; film terms: CPU 95 C once or 90 C twice in a row' })$(if ($TestReadings -ne '') { '; TEST READINGS: ' + $TestReadings })" | Add-Content $Log
$hotRun = 0; $hotRun90 = 0; $paceErr = 0
$lastPace = (Get-Date).AddMinutes(-5)
$prev = -1; $hotSince = $null; $unread = 0; $maxC = -1; $maxG = -1; $n = 0; $lastLine = Get-Date; $events = 0
$closeUntil = $start.AddSeconds(60); $lastClose = $start.AddSeconds(-10); $lastScan = $start.AddSeconds(-10); $seen = @{}
while (-not (Test-Path $Done) -and (Get-Date) -lt $end) {
    if ($TestReadings -ne '') { if ($ti -ge $tr.Count) { break }; $c = $tr[$ti]; $ti++; $g = 40 } else { $c = CpuTemp; $g = GpuTemp }
    $n++
    $now = Get-Date
    if (($now - $lastScan).TotalSeconds -ge 3) {
        $lastScan = $now
        foreach ($u in @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match $Workers })) {
            if (-not $seen.ContainsKey($u.ProcessId)) {
                $seen[$u.ProcessId] = $true; $closeUntil = $now.AddSeconds(60)
                "$(Get-Date -Format s) Unity $($u.ProcessId) started: readings 3 s apart for 60 s" | Add-Content $Log
            }
        }
    }
    if ($now -lt $closeUntil -and ($now - $lastClose).TotalSeconds -ge 3) { $lastClose = $now; "$(Get-Date -Format s) close cpu $c gpu $g" | Add-Content $Log }
    if ($TestReadings -eq '' -and $n % 2 -eq 0) { $events = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = $start } -ErrorAction SilentlyContinue).Count }
    if ($c -lt 0) { $unread++ } else { $unread = 0 }
    if ($c -gt $maxC) { $maxC = $c }; if ($g -gt $maxG) { $maxG = $g }
    $why = $null
    # D258: 95 C must hold on every readable sample for a minute; an unreadable one neither starts nor resets the minute.
    if ($c -ge 95) { if ($null -eq $hotSince) { $hotSince = $now } } elseif ($c -ge 0) { $hotSince = $null }
    if ($c -ge 98) { $why = "CPU $c C" } elseif ($null -ne $hotSince -and ($now - $hotSince).TotalSeconds -ge 60) { $why = "CPU at 95 C or more since $($hotSince.ToString('s'))" }
    if ($c -ge 85) { $hotRun++ } elseif ($c -ge 0) { $hotRun = 0 }
    if (-not $why -and $CardTerms) { if ($c -ge 90) { $why = "CPU $c C (card terms)" } elseif ($hotRun -ge 2) { $why = "CPU 85 C or more twice in a row, now $c C (card terms)" } }
    if ($c -ge 90) { $hotRun90++ } elseif ($c -ge 0) { $hotRun90 = 0 }
    if (-not $why -and $FilmTerms) { if ($c -ge 95) { $why = "CPU $c C (film terms)" } elseif ($hotRun90 -ge 2) { $why = "CPU 90 C or more twice in a row, now $c C (film terms)" } }
    if ($why) { }
    elseif ($g -ge 83) { $why = "GPU $g C" } elseif ($events -gt 0) { $why = "nvlddmkm event" } elseif ($unread -ge 10) { $why = 'CPU temperature unreadable for 10 s' }
    if (-not $why -and $PaceCheck -and ((Get-Date) - $lastPace).TotalSeconds -ge 120) {
        $lastPace = Get-Date
        # @(...) outside the if: an if's value unrolls a one-element array to a string, which splats one character per argument
        # (phase169, one folder: pace-check went to the drive root and exited 1 at every look).
        $paceArgs = @(if ($env:EVOSIM_GUARD_PACE_RUNS) { $env:EVOSIM_GUARD_PACE_RUNS -split ';' | Where-Object { $_ } } else { '--runs-root', "$r\runs" })
        $pc = (& python -I "$r\scripts\pace-check.py" @paceArgs 2>&1 | Out-String)
        $pe = $LASTEXITCODE
        # Fail closed with one retry (the coordinator, 2026-11-07): a guard that cannot see is not watching. One error logs and
        # looks again at once, so a file caught mid-write does not end a render; a second in a row trips as a SLOW does.
        if ($pe -eq 2) { $why = "pace-check flags a world: $(($pc -split "`n" | Where-Object { $_ -match 'SLOW' } | Select-Object -First 1).Trim())" }
        elseif ($pe -eq 0) { $paceErr = 0; "$(Get-Date -Format s) pace-check exit 0" | Add-Content $Log }
        else {
            $paceErr++
            "$(Get-Date -Format s) pace-check exit $pe ($paceErr in a row): $((($pc -split "`n") | Where-Object { $_.Trim() } | Select-Object -Last 1).Trim())" | Add-Content $Log
            if ($paceErr -ge 2) { $why = "guard blind: pace-check exit $pe twice" } else { $lastPace = (Get-Date).AddMinutes(-5) }
        }
    }
    if ($c -ge 85) { "$(Get-Date -Format s) cpu $c gpu $g" | Add-Content $Log }
    if ($TestTrip -and -not $why) { $why = 'test trip' }
    $prev = $c
    if ($why) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $StopFile) | Out-Null
        "$(Get-Date -Format s) $why" | Set-Content $StopFile
        if ($KillTree -gt 0) {
            # A reused pid is not the job: the root must still be `film.py ... run`, or only the sweeps below run.
            $root = Get-CimInstance Win32_Process -Filter "ProcessId=$KillTree" -ErrorAction SilentlyContinue
            if (-not $root) { "  tree kill skipped: process $KillTree is gone" | Add-Content $Log }
            elseif ($root.CommandLine -notmatch 'film\.py' -or $root.CommandLine -notmatch '\brun\b') { "  tree kill skipped: process $KillTree is not a film.py run ($($root.Name))" | Add-Content $Log }
            else {
                # Walk the descendants and stop each, deepest first, sparing this guard and its ancestors; the root then goes by taskkill if it lives.
                $all = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
                $spare = @{}; $a = $PID
                for ($k = 0; $k -lt 64 -and $a; $k++) { $spare[[int]$a] = $true; $a = ($all | Where-Object { $_.ProcessId -eq $a } | Select-Object -First 1).ParentProcessId }
                $order = New-Object System.Collections.ArrayList; $level = @($KillTree); $seenIds = @{ $KillTree = $true }
                for ($k = 0; $k -lt 32 -and $level.Count; $k++) {
                    $next = @($all | Where-Object { $level -contains $_.ParentProcessId -and -not $seenIds.ContainsKey([int]$_.ProcessId) } | ForEach-Object { [int]$_.ProcessId })
                    foreach ($x in $next) { $seenIds[$x] = $true; [void]$order.Add($x) }
                    $level = $next
                }
                for ($k = $order.Count - 1; $k -ge 0; $k--) {
                    $x = $order[$k]
                    if ($spare.ContainsKey($x)) { "  tree kill spared $x (this guard or its ancestor)" | Add-Content $Log; continue }
                    Stop-Process -Id $x -Force -ErrorAction SilentlyContinue; "  stopped tree process $x" | Add-Content $Log
                }
                if (-not $spare.ContainsKey([int]$KillTree) -and (Get-Process -Id $KillTree -ErrorAction SilentlyContinue)) {
                    $tk = ((& taskkill /PID $KillTree /T /F 2>&1 | Out-String).Trim() -split '\s*\n\s*') -join ' | '
                    "  taskkill tree $KillTree : $tk" | Add-Content $Log
                }
            }
        }
        Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match $Workers } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue; "  stopped Unity $($_.ProcessId)" | Add-Content $Log }
        if ($FarmExe -ne '') {
            Get-CimInstance Win32_Process -Filter "Name='Evosim.Farm.exe'" | Where-Object { $_.ExecutablePath -match $FarmExe } |
                ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue; "  stopped Evosim.Farm.exe $($_.ProcessId)" | Add-Content $Log }
        }
        if ($Kill -ne '') {
            Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and $_.Name -ne 'pwsh.exe' -and $_.CommandLine -match $Kill } |
                ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue; "  stopped $($_.Name) $($_.ProcessId)" | Add-Content $Log }
        }
        "$(Get-Date -Format s) STOPPED: $why" | Add-Content $Log
        return
    }
    if (((Get-Date) - $lastLine).TotalSeconds -ge 30) {
        "$(Get-Date -Format s) $n samples; hottest cpu $maxC gpu $maxG; nvlddmkm $events" | Add-Content $Log
        $maxC = -1; $maxG = -1; $n = 0; $lastLine = Get-Date
    }
    Start-Sleep -Milliseconds 700
}
"$(Get-Date -Format s) guard end" | Add-Content $Log
