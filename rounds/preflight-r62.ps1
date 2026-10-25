# Round 62's accelerator preflight (D214): rounds/launch-r62.ps1's six worlds founded for 30 s on ILGPU's CPU
# accelerator, off the card (-Device cpu), as the grazer screen's preflight was. It proves the six settings blocks
# load and found, and each world's config.json gives the hash the pre-registration names. Worlds r62pf-<world>, so
# nothing lands on the round's names; runs under scratch\r62-launch\preflight. Logical 16-19, 4 threads. Run only on
# the coordinator's go. On 2026-10-23 the six worlds took about 2.5 min to write run.json on the accelerator, past
# card-group's default 120 s wait, and stepped 15 s of 30 in the 15 min wall: hence the 300 s wait. The hashes are
# in config.json from the start, whatever the second reached. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r62launch"
$arms = @(
    @{ name = 'r62pf-n6201'; launcher = 'env-r62-n.ps1'; seed = '6201' },
    @{ name = 'r62pf-h6201'; launcher = 'env-r62-h.ps1'; seed = '6201' },
    @{ name = 'r62pf-n6202'; launcher = 'env-r62-n.ps1'; seed = '6202' },
    @{ name = 'r62pf-h6202'; launcher = 'env-r62-h.ps1'; seed = '6202' },
    @{ name = 'r62pf-n6203'; launcher = 'env-r62-n.ps1'; seed = '6203' },
    @{ name = 'r62pf-h6203'; launcher = 'env-r62-h.ps1'; seed = '6203' })
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\r62-launch\preflight" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62pf -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 30 -Group 32 -Transport -Device cpu -Threads 4 -Mask 983040 -WallMinutes 15 -WaitForManifestSeconds 300 `
    -RunsRoot "$R\scratch\r62-launch\preflight" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
