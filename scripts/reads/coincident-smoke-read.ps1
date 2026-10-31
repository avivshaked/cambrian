# The read of rounds/coincident-smoke.ps1 (D271). For each seed: scripts/reads/gpu-accept.py of the smoke's "on" run
# against r63s's own run (identical until the first coincident pair forms, then apart: the first second they differ is
# the reading), against the smoke's "off" run where there is one, and "off" against r63s; then
# scripts/reads/shape/r63s-floaters.py over every run. r63s's floaters later than the smoke's last second are left out
# of the comparison by hand, from their seconds.
#
# It passes when "on" loses no remains through the top (no diverged records, or none of them twins) where r63s lost a
# pair or more in the same span, no living body diverges, and "on" leaves r63s's rows no earlier than the step its first
# twin formed. It fails on twins still launched, a living body diverged, or rows parting long before any twin.
# Output to scratch/coincident-smoke/read.txt.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\scratch\coincident-smoke"
function Newest($arm) { (Get-ChildItem $arm -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1).FullName }
$out = "$o\read.txt"
"" | Set-Content $out
$runs = @()
foreach ($sd in 6202, 6203) {
    $on = Newest "$o\smoke-on\coid-smoke-on-$sd"
    $off = Newest "$o\smoke-off\coid-smoke-off-$sd"
    $ref = "$R\runs\r63s-on-$sd\2026-10-29-220442-f73b299d"
    foreach ($p in @(@("on v r63s $sd", $on, $ref), @("on v off $sd", $on, $off), @("off v r63s $sd", $off, $ref))) {
        if (-not $p[1] -or -not $p[2]) { continue }
        "== $($p[0]): $($p[1]) against $($p[2])" | Add-Content $out
        & python -I "$R\scripts\reads\gpu-accept.py" $p[1] $p[2] *>> $out
        "exit $LASTEXITCODE" | Add-Content $out
    }
    $runs += @($on, $off, $ref) | Where-Object { $_ }
}
"== floaters" | Add-Content $out
& python -I "$R\scripts\reads\shape\r63s-floaters.py" @($runs | Select-Object -Unique) *>> $out
"exit $LASTEXITCODE" | Add-Content $out
