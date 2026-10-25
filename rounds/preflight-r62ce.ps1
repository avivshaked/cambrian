# The contact-eating screen's accelerator preflight: rounds/launch-r62ce.ps1's three worlds founded for 30 s on ILGPU's
# CPU accelerator, off the card, for their config hash (as rounds/preflight-r62.ps1). Worlds r62cepf-<seed>, runs under
# scratch\contact-eating\preflight. Logical 16-19, 4 threads, wall 15 min, 300 s for run.json. On the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
# Off the card by proof as well as by option: an accidental path to the card finds no device.
$env:CUDA_VISIBLE_DEVICES = '-1'
$arms = @(
    @{ name = 'r62cepf-6201'; launcher = 'env-r62ce.ps1'; seed = '6201' },
    @{ name = 'r62cepf-6202'; launcher = 'env-r62ce.ps1'; seed = '6202' },
    @{ name = 'r62cepf-6203'; launcher = 'env-r62ce.ps1'; seed = '6203' })
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\preflight" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62cepf -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 30 -Group 32 -Transport -Device cpu -Threads 4 -Mask 983040 -WallMinutes 15 -WaitForManifestSeconds 300 `
    -RunsRoot "$R\scratch\contact-eating\preflight" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'