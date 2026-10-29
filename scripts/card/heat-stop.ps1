# The heat stop of a long card run (the owner's ruling of 2026-10-09: the machine is used as fully as heat allows).
# Dot-sourced by card-run.ps1 and card-group.ps1 under -HeatStop, which read the heat every turn of their wait (about
# every 15 s, measured on 2026-10-15) and stop on the first reason this gives. Run on its own with -DryCheck, which
# feeds it made-up readings and prints each verdict.
#
# The reasons, each against the limits passed in (D258, 2026-10-29: the stop is for sustained heat, a cooling fault,
# and lets a boosting core's spike pass, as the chip itself does up to its 100 C limit):
#   the CPU at or above -CpuStopC (98 C) on one reading;
#   the CPU at or above -CpuHoldC (95 C) on every reading for -HoldSeconds (60 s);
#   the card at or above -CardStopC (80 C);
#   no CPU temperature for -BlindSeconds (120 s): MSI Afterburner gone, so the CPU's heat cannot be watched.
# The CPU's temperature is temp-check.ps1's, from Afterburner's shared memory; the card's is nvidia-smi's.
param([switch]$DryCheck)

$script:HeatHoldSince = $null
$script:HeatBlindSince = $null

# One reading's verdict: '' to go on, or the reason to stop.
function Get-HeatStop($cpuC, $cardC, [datetime]$now, [int]$CpuStopC = 98, [int]$CpuHoldC = 95, [int]$HoldSeconds = 60,
                      [int]$CardStopC = 80, [int]$BlindSeconds = 120) {
    if ($null -ne $cardC -and $cardC -ge $CardStopC) { return "the card at $cardC C, the limit $CardStopC C" }
    if ($null -eq $cpuC) {
        if ($null -eq $script:HeatBlindSince) { $script:HeatBlindSince = $now }
        elseif (($now - $script:HeatBlindSince).TotalSeconds -ge $BlindSeconds) { return "no CPU temperature read since $($script:HeatBlindSince.ToString('HH:mm:ss'))" }
        return ''
    }
    $script:HeatBlindSince = $null
    if ($cpuC -ge $CpuStopC) { return "the CPU at $cpuC C, the limit $CpuStopC C" }
    if ($cpuC -ge $CpuHoldC) {
        if ($null -eq $script:HeatHoldSince) { $script:HeatHoldSince = $now }
        elseif (($now - $script:HeatHoldSince).TotalSeconds -ge $HoldSeconds) { return "the CPU at $CpuHoldC C or more since $($script:HeatHoldSince.ToString('HH:mm:ss'))" }
    }
    else { $script:HeatHoldSince = $null }
    return ''
}

if ($DryCheck) {
    $t0 = [datetime]'2026-10-09T12:00:00'
    # Each rule in turn, at the launchers' 15 s: spikes to 92-97 C go on; a reading under 95 C resets the hold; a
    # minute held at 95 C or more stops; one reading at 98 C stops; the card at 80 C stops; two blind minutes stop.
    $cases = @(
        @(70, 50, 0), @(92, 50, 15), @(97, 50, 30), @(88, 50, 45), @(95, 50, 60), @(96, 50, 75), @(94, 50, 90),
        @(95, 50, 105), @(96, 50, 120), @(97, 50, 135), @(95, 50, 150), @(96, 50, 165),
        @(98, 50, 180),
        @(70, 80, 195), @($null, 50, 210), @($null, 50, 270), @($null, 50, 330), @(70, 50, 345))
    foreach ($c in $cases) {
        $now = $t0.AddSeconds($c[2])
        $r = Get-HeatStop $c[0] $c[1] $now
        "{0}  cpu {1}  card {2}  -> {3}" -f $now.ToString('HH:mm:ss'), $(if ($null -eq $c[0]) { 'n/a' } else { $c[0] }), $c[1], $(if ($r) { "STOP: $r" } else { 'go on' })
        if ($r) { $script:HeatHoldSince = $null; $script:HeatBlindSince = $null }
    }
}
