# Replay-identity probe for the shared world (the r26d-s4 / r27-s4 divergence, 2026-09-06).
# 0068's world (launch-r26.ps1 at influx 0.6, stock 0.25/m3), short budget, one seed, run twice per
# physics setting so identical inputs can be checked for identical rows. ASCII only.
#   ./rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -Name det0-a -ExpectSimHash <hash>
#
# -DigestEvery N writes runs/<Name>/<run>/digest.jsonl, one state hash every N physics steps
# (100 = one per metabolic step); -DigestDump "a,b" also writes one row per living body at
# those exact step numbers, into digest-bodies.jsonl. Both off by default, and off is the
# world every earlier run was recorded in. See logbook/specs/digest-spec.md and digest-diff.py.
#   ./rounds/launch-det.ps1 -Seed 4 -Worker 7 -Name det3-a -DigestEvery 100
#   ./rounds/launch-det.ps1 -Seed 4 -Worker 7 -Name det3-c -DigestEvery 100 -DigestDump '140000,140100'
param(
    [Parameter(Mandatory)][int]$Seed,
    [Parameter(Mandatory)][int]$Worker,
    [Parameter(Mandatory)][string]$Name,
    [string]$Influx = '0.6',
    [string]$MatterInitial = '0.25',
    [string]$Dt = '0.01',
    [int]$Seconds = 3000,
    [float]$WallMinutes = 240,
    [string]$ExpectSimHash = '',
    [int]$DigestEvery = 0,
    [string]$DigestDump = '',
    [string]$PhysicsJobs = '',
    [string[]]$UnityArgs = @()
)

$s = @{
    EVOSIM_SEED = $Seed
    EVOSIM_IRRADIANCE = 200; EVOSIM_CURRENT = 0.3; EVOSIM_MIXING = 0.2; EVOSIM_REMIN = 0
    EVOSIM_EXCRETION = 0.01; EVOSIM_AREA = 400; EVOSIM_FLOOR_CLOSES = 3000; EVOSIM_MAX_POP = 8000
    EVOSIM_SENESCENCE = 3000; EVOSIM_EXCESS_DENSITY = 0.02
    EVOSIM_MATTER_PER_TISSUE = 0.5; EVOSIM_MATTER_INITIAL = $MatterInitial; EVOSIM_FOUNDER_FLOAT = 0.5
    EVOSIM_LIFT_COST = 0.05; EVOSIM_CELLTYPE_MUTATION = 0.005; EVOSIM_NEUTRAL_VOLUME = 0.25
    EVOSIM_FOUNDER_DEPTH = 60; EVOSIM_MATTER_PER_CREATURE = 3; EVOSIM_PATCHES = 4
    EVOSIM_CURRENT_PERIOD = 6000; EVOSIM_CURRENT_CELL = 30; EVOSIM_CURRENT_ROLLS = 1
    EVOSIM_CURRENT_BLINK = 3000; EVOSIM_CURRENT_ADVECT = 1
    EVOSIM_SINK = 0.002; EVOSIM_MATTER_SINK = 0.02
    EVOSIM_CLEARANCE = 10
    EVOSIM_EXUDATION = 0.15
    EVOSIM_DT = $Dt
    EVOSIM_CONCEPTION_ORDER = 'age'
    EVOSIM_MATTER_INFLUX = $Influx
    EVOSIM_MATTER_INFLUX_AT = 'vent'
    EVOSIM_MATTER_BURIAL = 0.01
    EVOSIM_VENT = 0.05; EVOSIM_VENT_PATCH = 0; EVOSIM_VENT_DEPTH = 60; EVOSIM_VENT_LEG = 1
    EVOSIM_SHARED_SPACE = 1
    EVOSIM_SURFACE_RESTORE = 1
}

# Only set when asked for. An EVOSIM_DIGEST_EVERY of 0 and an unset one are the same world,
# but a settings hashtable that always carried the key would put it in every launch printout
# and invite the reading that the digest is part of the arm.
if ($DigestEvery -gt 0) { $s.EVOSIM_DIGEST_EVERY = $DigestEvery }
if ($DigestDump -ne '') { $s.EVOSIM_DIGEST_DUMP_STEPS = $DigestDump }

# D078. Left unset by default so the launch printout says nothing about a setting the arm did
# not choose: the code's own default is 0, which is the whole point of the change. Set it to
# reach the old fifteen-thread behaviour on purpose, and read the run header to confirm.
#   ./rounds/launch-det.ps1 -Seed 4 -Worker 7 -Name jobs15 -PhysicsJobs 15
if ($PhysicsJobs -ne '') { $s.EVOSIM_PHYSICS_JOBS = $PhysicsJobs }

$extra = @{}
if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
if ($UnityArgs.Count -gt 0) { $extra.UnityArgs = $UnityArgs }
& "$PSScriptRoot\..\scripts\run-arm.ps1" -Name $Name -Worker $Worker -Seed $Seed -Seconds $Seconds -WallMinutes $WallMinutes -Settings $s @extra
