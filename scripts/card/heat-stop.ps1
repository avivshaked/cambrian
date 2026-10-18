# The heat stop of a long card run (the owner's ruling of 2026-10-09: the machine is used as fully as heat allows).
# Dot-sourced by card-run.ps1 and card-group.ps1 under -HeatStop, which read the heat every turn of their wait (about
# every 15 s, measured on 2026-10-15) and stop on the first reason this gives. Run on its own with -DryCheck, which
# feeds it made-up readings and prints each verdict.
#
# The reasons, each against the limits passed in:
#   the CPU at or above -CpuStopC (90 C) on two readings in a row (D183, 2026-10-15: one boosting core's hot spot
#   of a few seconds no longer stops a run, while a real climb stops within about 15 s of its first such reading);
#   a turn with no CPU reading does not break the pair, so the next reading at or above the limit stops;
#   the CPU at or above -CpuHoldC (85 C) on every reading for -HoldSeconds (60 s);
#   the card at or above -CardStopC (80 C);
#   no CPU temperature for -BlindSeconds (120 s): MSI Afterburner gone, so the CPU's heat cannot be watched.
# The CPU's temperature is temp-check.ps1's, from Afterburner's shared memory; the card's is nvidia-smi's.
param([switch]$DryCheck)

$script:HeatHoldSince = $null
$script:HeatBlindSince = $null
$script:HeatOverFirst = $null

# One reading's verdict: '' to go on, or the reason to stop.
function Get-HeatStop($cpuC, $cardC, [datetime]$now, [int]$CpuStopC = 90, [int]$CpuHoldC = 85, [int]$HoldSeconds = 60,
                      [int]$CardStopC = 80, [int]$BlindSeconds = 120) {
    if ($null -ne $cardC -and $cardC -ge $CardStopC) { return "the card at $cardC C, the limit $CardStopC C" }
    if ($null -eq $cpuC) {
        if ($null -eq $script:HeatBlindSince) { $script:HeatBlindSince = $now }
        elseif (($now - $script:HeatBlindSince).TotalSeconds -ge $BlindSeconds) { return "no CPU temperature read since $($script:HeatBlindSince.ToString('HH:mm:ss'))" }
        return ''
    }
    $script:HeatBlindSince = $null
    if ($cpuC -ge $CpuStopC) {
        if ($null -ne $script:HeatOverFirst) {
            return "the CPU at $cpuC C, after $($script:HeatOverFirst[0]) C at $($script:HeatOverFirst[1].ToString('HH:mm:ss')): two readings in a row at or over the limit $CpuStopC C"
        }
        $script:HeatOverFirst = @($cpuC, $now)
    }
    else { $script:HeatOverFirst = $null }
    if ($cpuC -ge $CpuHoldC) {
        if ($null -eq $script:HeatHoldSince) { $script:HeatHoldSince = $now }
        elseif (($now - $script:HeatHoldSince).TotalSeconds -ge $HoldSeconds) { return "the CPU at $CpuHoldC C or more since $($script:HeatHoldSince.ToString('HH:mm:ss'))" }
    }
    else { $script:HeatHoldSince = $null }
    return ''
}

if ($DryCheck) {
    $t0 = [datetime]'2026-10-09T12:00:00'
    # Each rule in turn, at the launchers' 15 s: a minute held at 85 C stops; one reading at 91 C goes on, and a
    # reading under 90 C after it resets the pair; two in a row stop; a blind turn between two does not reset them;
    # the card at 80 C stops; two blind minutes stop.
    $cases = @(
        @(70, 50, 0), @(84, 50, 15), @(86, 50, 30), @(87, 50, 45), @(86, 50, 60), @(86, 50, 90),
        @(84, 50, 105), @(91, 50, 120), @(85, 50, 135), @(84, 50, 150), @(92, 50, 165), @(93, 50, 180),
        @(84, 50, 195), @(91, 50, 210), @($null, 50, 225), @(90, 50, 240),
        @(70, 80, 255), @($null, 50, 270), @($null, 50, 330), @($null, 50, 390), @(70, 50, 405))
    foreach ($c in $cases) {
        $now = $t0.AddSeconds($c[2])
        $r = Get-HeatStop $c[0] $c[1] $now
        "{0}  cpu {1}  card {2}  -> {3}" -f $now.ToString('HH:mm:ss'), $(if ($null -eq $c[0]) { 'n/a' } else { $c[0] }), $c[1], $(if ($r) { "STOP: $r" } else { 'go on' })
        if ($r) { $script:HeatHoldSince = $null; $script:HeatBlindSince = $null; $script:HeatOverFirst = $null }
    }
}
