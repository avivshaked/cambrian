# The merged tree's sitting read (identity-land.ps1), by scripts/reads/gpu-accept.py on each newest run directory, as
# scratch/shape-build/card/accept-shape.ps1: off against r63c-b's 24,000 s runs, r62cf against r62cf's, and the arm "on"
# at 32 against 64. Output to scratch/shape-build/land-sit/accept-land.txt.
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\scratch\shape-build\land-sit"
function Newest($arm) { (Get-ChildItem $arm -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1).FullName }
function Arm($label, $seed) { Newest "$o\$label\landid-$label-$seed" }
$pairs = @()
foreach ($sd in 6202, 6203) { $pairs += , @("off $sd", (Arm 'off-g32' $sd), "$R\runs\r63c-b-$sd\2026-10-29-123625-716cd9ee") }
foreach ($sd in 6202, 6203, 6204) { $pairs += , @("r62cf $sd", (Arm 'r62cf-g32' $sd), (Newest "$R\runs\r62cf-$sd")) }
foreach ($sd in 6202, 6203) { $pairs += , @("on $sd", (Arm 'on-g32' $sd), (Arm 'on-g64' $sd)) }
$out = "$o\accept-land.txt"
"" | Set-Content $out
foreach ($p in $pairs) {
    "== $($p[0]): $($p[1]) against $($p[2])" | Add-Content $out
    & python -I "$R\scripts\reads\gpu-accept.py" $p[1] $p[2] *>> $out
    "exit $LASTEXITCODE" | Add-Content $out
}
