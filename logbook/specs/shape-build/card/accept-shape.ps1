# The shape sitting's reads, by scripts/reads/gpu-accept.py on the newest run directory of each arm, in the pattern of
# scratch/nose-card/accept-nose.ps1:
#   -Part off:    each switch-off world against r62cf's prefix (identity, and the pace line);
#   -Part family: each family world at 32 against 64 (identity);
#   -Part cost:   each family world at 32 against the fin world at 32 and against its repeat, for the walls of the gate.
# Output to accept-<part>.txt beside this script.
param([ValidateSet('off', 'family', 'cost')][string]$Part = 'off')
$R = 'D:\Projects\experiments\evolution-simulator'
$o = "$R\scratch\shape-build\card"
function Newest($arm) { (Get-ChildItem $arm -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1).FullName }
function Arm($label, $seed) { Newest "$o\sit\$label\shapeid-$label-$seed" }
$seeds = 6202, 6203, 6204
$pairs = switch ($Part) {
    'off' { $seeds | ForEach-Object { , @("off $_", (Arm 'off-g32' $_), (Newest "$R\runs\r62cf-$_")) } }
    'family' { $seeds | ForEach-Object { , @("family $_", (Arm 'family-g32' $_), (Arm 'family-g64' $_)) } }
    'cost' {
        foreach ($seed in $seeds) {
            , @("family vs fin $seed", (Arm 'family-g32' $seed), (Arm 'fin-g32' $seed))
            , @("family vs fin repeat $seed", (Arm 'family-g32' $seed), (Arm 'fin-g32r2' $seed))
        }
    }
}
$out = "$o\accept-$Part.txt"
"" | Set-Content $out
foreach ($p in $pairs) {
    "== $($p[0]): $($p[1]) against $($p[2])" | Add-Content $out
    & python -I "$R\scripts\reads\gpu-accept.py" $p[1] $p[2] *>> $out
    "exit $LASTEXITCODE" | Add-Content $out
}
