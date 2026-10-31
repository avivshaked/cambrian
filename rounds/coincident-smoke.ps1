# The coincident-contact smoke (D270): a world where coincident remains really form, so the fix's own path runs in a
# world and not only in the CPU accelerator's pair test (GpuEngineTests). It comes before any round turns the switch on.
# Run only on the coordinator's card go, in a long card slot, with -Build's Release farm built from a clean tree.
#
# Founded, not resumed: a resume cannot change the contact form (CousinConfig allows only the economy and mouth groups),
# and a build with today's schema cannot load r63s's config or checkpoints. r63s's first launched twins left the world
# at 7,912 s (6202) and 10,636 s (6203), a pair each, then at 16,592 s (6202) and at 14,894 and 16,328 s (6203); its
# remains are about 785 s old when they leave (logbook/specs/r63s-read/, "The floaters"). So 17,000 s covers two or
# three pairs in each seed, and 11,000 s the first pair only.
#
#   -Set on    rounds/env-r63s-apart.ps1: r63s's arm "on" with EVOSIM_CONTACT_SHAPE=spheresapart
#   -Set off   rounds/env-r63s-on.ps1 on the same build, run only if "on" leaves r63s's rows long before its first twin
#              forms, to tell the fix from a later loss of r63s's replay
#
# Seeds 6202 and 6203 in one card group at group 32, the snow's transport on the card, checkpoints every 1,500 s,
# -Threads threads on -Mask, the heat stop on, about 105 minutes of card at r63s's 2.7x real time. Runs under
# scratch/coincident-smoke/<label>/; the read is scripts/reads/coincident-smoke-read.ps1.
param([ValidateSet('on', 'off')][string]$Set = 'on', [int]$Seconds = 17000, [long]$Mask = 65280, [int]$Threads = 8,
    [int]$WallMinutes = 170, [string]$Build = 'D:\Projects\experiments\evolution-simulator\scratch\wt-coincident')
$R = 'D:\Projects\experiments\evolution-simulator'
$launcher = @{ off = "$Build\rounds\env-r63s-on.ps1"; on = "$Build\rounds\env-r63s-apart.ps1" }[$Set]
$seeds = @('6202', '6203')
$label = "smoke-$Set"
$names = ($seeds | ForEach-Object { "coid-$label-$_" }) -join ','
$launchers = ($seeds | ForEach-Object { $launcher }) -join ','
$root = "$R\scratch\coincident-smoke\$label"
New-Item -ItemType Directory -Force $root | Out-Null
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr](1L -shl ([Convert]::ToString($Mask, 2).Length - 1))
$me.PriorityClass = 'BelowNormal'
& pwsh -NoProfile -File "$Build\scripts\card\card-group.ps1" -Name "coid-$label" -Arms $names -Launcher $launchers `
    -Seeds ($seeds -join ',') -Seconds $Seconds -Group 32 -Transport -CheckpointEvery 1500 -Threads $Threads -Mask $Mask `
    -HeatStop -WallMinutes $WallMinutes -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$Build\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
exit $LASTEXITCODE
