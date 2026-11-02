# The mouth screen (logbook/specs/r66-prereg/predictions.md), on the coordinator's go only, after the identity sitting
# (rounds/identity-r66.ps1), from this worktree at the pre-registration's commit with its own Release farm. Two card groups
# of three founded for 24,000 s, one a seed, 6202 first: M, Mk and M4k on rounds/env-r66-m.ps1, -mk.ps1 and -m4k.ps1. The
# control is r65's C. Group 32, the snow's transport on the card, the heat stop on (D258), checkpoints every 1,500 s, a
# 600 s wait for run.json, logical 20-29 at 10 threads, off a render's 16-19. -Seed runs one group alone (to resume or rerun
# it). Between the groups it waits, up to 15 minutes, for the first group's driver watch and farm to be gone, since r65's
# second group was refused by the first group's watch. Start it detached and hidden, with the worktree as the working
# directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([string]$Worktree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-mouth',
      [ValidateSet('both', '6202', '6203')][string]$Seed = 'both')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = $Worktree
$launchers = "$b\rounds\env-r66-m.ps1,$b\rounds\env-r66-mk.ps1,$b\rounds\env-r66-m4k.ps1"
$seeds = if ($Seed -eq 'both') { @('6202', '6203') } else { @($Seed) }
$first = $true
foreach ($s in $seeds) {
    if (-not $first) {
        $d = (Get-Date).AddMinutes(15)
        while ((Get-Date) -lt $d) {
            $left = @(Get-CimInstance Win32_Process | Where-Object {
                $_.Name -match '^Evosim\.Farm\.exe$' -or ($_.Name -eq 'pwsh.exe' -and "$($_.CommandLine)" -match 'driver-watch') })
            if (-not $left) { break }
            Start-Sleep -Seconds 15
        }
    }
    $first = $false
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r66-$s" -Arms "r66-m-$s,r66-mk-$s,r66-m4k-$s" `
        -Launcher $launchers -Seeds "$s,$s,$s" -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 `
        -Mask 1072693248 -HeatStop -WallMinutes 600 -WaitForManifestSeconds 600 `
        -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
    if ($LASTEXITCODE -ne 0) { "group r66-$s ended with exit $LASTEXITCODE; the next group is not started"; exit $LASTEXITCODE }
}
