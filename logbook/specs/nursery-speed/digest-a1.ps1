# A1's digest check: speed-small-b2 at 2 threads on the efficiency cores, from nursery-speed (switch off,
# then on), compared byte for byte with the reference's d-ref of the slot (main at 544f809). Each wait has a deadline.
$root   = 'D:\Projects\experiments\evolution-simulator'
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$tree   = "$root\scratch\wt-speed"
$runs   = "$root\scratch\nursery-speed\runs"
foreach ($x in @(@('a1-off', ''), @('a1-on', '1'))) {
    $name = $x[0]; $env:EVOSIM_NURSERY_PROFILE = $x[1]
    if (Test-Path "$runs\$name") { throw "$runs\$name exists" }
    $argv = @('artifacts\Evosim.Nursery\bin\Release\net8.0\Evosim.Nursery.dll', '--recipe', 'scratch\nursery-speed\recipes\speed-small-b2.json',
              '--out', "$runs\$name", '--threads', '2')
    $p = Start-Process -FilePath $dotnet -WorkingDirectory $tree -ArgumentList $argv -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput "$runs\$name.stdout.txt" -RedirectStandardError "$runs\$name.stderr.txt"
    $p.ProcessorAffinity = [IntPtr][int64]4294901760
    if (-not $p.WaitForExit(300000)) { Stop-Process -Id $p.Id -Force; throw "$name passed 5 minutes; stopped" }
    "$name exit $($p.ExitCode)"
}
$env:EVOSIM_NURSERY_PROFILE = ''
& python "$root\scratch\nursery-speed\digests.py" "$runs\d-ref" "$runs\a1-off" "$runs\a1-on"
