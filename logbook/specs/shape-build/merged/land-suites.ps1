# G4's landing, step 3: the closing suites on scratch/wt-shape-land (branch shape-land), off the card by construction
# (nvcuda-guard.ps1), Release, one MSBuild node, no compiler server, two xUnit threads, BelowNormal.
#   -Set 1: the Farm default suite without GpuMeanTests (it crashes the CPU accelerator when pinned), then Dynamics default
#   -Set 2: Core default, Nursery default, then Core with the Slow trait too (-All's set)
#   -Set all: 1 then 2, for one lane
#   -Set mean: GpuMeanTests alone, for an unpinned slot the coordinator gives
# Each suite's log is land-suite-<name>.log beside this script; land-suites-<set>.log holds the exits and the heat. The
# heat is read every 30 s, and the set is stopped on one CPU reading of 90 C. Launch pinned to a free lane, e.g. lane A:
#   cmd /c start "" /b /belownormal /affinity 0xF0000 pwsh -NoProfile -File land-suites.ps1 -Set 1
#   -Set nursery: Nursery default alone, the rerun after a missing pool is copied in
#   -Set scent: GpuScentTests alone, after main's repoint to r57-s2's 48,000 s checkpoint is merged
param([ValidateSet('1', '2', '3', 'all', 'mean', 'nursery', 'scent')][string]$Set = 'all')
$root = 'D:\Projects\experiments\evolution-simulator\scratch\wt-shape-land'
$s = 'D:\Projects\experiments\evolution-simulator\scratch\shape-build'
$check = 'D:\Projects\experiments\evolution-simulator\scripts\temp-check.ps1'
$log = "$s\land-suites-$Set.log"
. (Join-Path $root 'scripts/card/nvcuda-guard.ps1')
Enter-OffCard
# The mean set takes no processor count: ILGPU's CPU accelerator crashes when it sees 4 or 8 (src/Evosim.Farm.Gpu/CLAUDE.md)
if ($Set -ne 'mean') { $env:DOTNET_PROCESSOR_COUNT = '4' } else { Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
"$(Get-Date -Format s) set $Set begins at $(git -C $root log --oneline -1)" | Set-Content $log

# name, project, filter (empty for none)
$one = @(@('farm', 'Farm', 'Category!=Slow&FullyQualifiedName!~GpuMeanTests'), @('dynamics', 'Dynamics', 'Category!=Slow'))
$two = @(@('core', 'Core', 'Category!=Slow'), @('nursery', 'Nursery', 'Category!=Slow'), @('core-all', 'Core', ''))
# Set 3: the suites that build the Farm, again, after remains-farm is merged (the landing plan's step 3b).
$three = @(@('farm-r', 'Farm', 'Category!=Slow&FullyQualifiedName!~GpuMeanTests'), @('dynamics-r', 'Dynamics', 'Category!=Slow'),
    @('nursery-r', 'Nursery', 'Category!=Slow'))
# @() keeps a single suite as one element; a bare switch unrolls it into its three strings
$suites = @(switch ($Set) { '1' { $one } '2' { $two } '3' { $three } 'all' { $one + $two } 'mean' { , @('mean', 'Farm', 'FullyQualifiedName~GpuMeanTests') }
    'nursery' { , @('nursery-r2', 'Nursery', 'Category!=Slow') }
    'scent' { , @('scent', 'Farm', 'FullyQualifiedName~GpuScentTests') } })

$stopped = $false
foreach ($t in $suites) {
    if ($stopped) { break }
    $out = "$s\land-suite-$($t[0]).log"
    $argList = @('test', "$root\src\Evosim.$($t[1]).Tests\Evosim.$($t[1]).Tests.csproj", '-c', 'Release', '--nologo', '-v', 'minimal',
        '-m:1', '-nodeReuse:false', '-p:UseSharedCompilation=false', '--logger', 'console;verbosity=normal')
    if ($t[2]) { $argList += @('--filter', $t[2]) }
    $argList += @('--', 'xUnit.MaxParallelThreads=2', 'RunConfiguration.MaxCpuCount=1')
    "$(Get-Date -Format s) $($t[0]) starts" | Add-Content $log
    $p = Start-Process $dotnet -ArgumentList $argList -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
    $deadline = (Get-Date).AddMinutes(60)
    while (-not $p.HasExited) {
        Start-Sleep -Seconds 30
        Test-OffCardTree (Get-Date).AddSeconds(20)
        $line = (& pwsh -NoProfile -File $check 2>&1 | Out-String).Trim()
        if ($line -match 'cpu (\d+)C' -and [int]$Matches[1] -ge 90) { $line | Add-Content $log; $why = 'heat'; $stopped = $true }
        elseif ((Get-Date) -gt $deadline) { $why = 'deadline'; $stopped = $true }
        if ($stopped) {
            foreach ($id in @(Get-ProcessTree $p.Id)) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
            "$(Get-Date -Format s) $($t[0]) STOPPED: $why" | Add-Content $log
            break
        }
    }
    if (-not $stopped) {
        $sum = (Get-Content $out | Select-String '^(Passed!|Failed!)|Total tests|Passed:|Failed:' | ForEach-Object { $_.Line.Trim() }) -join '; '
        "$(Get-Date -Format s) $($t[0]) exit $($p.ExitCode); $sum" | Add-Content $log
    }
}
Resolve-OffCard | Add-Content $log
& $dotnet build-server shutdown *> $null
"$(Get-Date -Format s) set $Set ends" | Add-Content $log
