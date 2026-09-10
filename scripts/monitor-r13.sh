#!/usr/bin/env bash
# Arm monitor (rounds 13-16). Watch list: scratch/evosim-watch-arms.txt (space-separated arm names, re-read every cycle).
# Emits one line per event: error signatures in the Unity log, the report's **Ended:** footer,
# and a 32-min byte-size stall on the report (a wedge keeps mtime moving with zero content).
# Logs are read from scratch/logs/ (run-arm.ps1 writes there since 2026-09-03); arms launched
# before that still log to TEMP, which is read as a fallback only - nothing is written there.
ROOT=/d/Projects/experiments/evolution-simulator
RUNS=$ROOT/runs
LOGS=$ROOT/scratch/logs
OLDTMP=/c/Users/shake/AppData/Local/Temp
LIST=$ROOT/scratch/evosim-watch-arms.txt
STALL=$((32*60))
declare -A ended errored size stamp stalled headered
while true; do
  arms=$(cat "$LIST" 2>/dev/null)
  [ -z "$arms" ] && { echo "watch list empty - monitor exiting"; exit 0; }
  now=$(date +%s)
  for a in $arms; do
    rep=$RUNS/$a.md; log=$LOGS/evosim-$a.log; [ -f "$log" ] || log=$OLDTMP/evosim-$a.log
    if [ -f "$log" ] && [ -z "${errored[$a]}" ]; then
      hit=$(grep -m1 -E "error CS|could not be found|Corrupted Library|Unhandled Exception|threw exception|return code [1-9]|non-finite|Aborting batchmode|Fatal Error|Crash!!!" "$log" 2>/dev/null)
      if [ -n "$hit" ]; then errored[$a]=1; echo "ERROR $a: $hit"; fi
    fi
    if [ -f "$rep" ]; then
      if [ -z "${headered[$a]}" ] && [ "$(wc -l < "$rep")" -ge 3 ]; then headered[$a]=1; echo "HEADER $a: $(sed -n 3p "$rep")"; fi
      if [ -z "${ended[$a]}" ] && grep -q '^\*\*Ended:\*\*' "$rep"; then
        ended[$a]=1; echo "ENDED $a: $(grep -m1 '^\*\*Ended:\*\*' "$rep") | $(grep -cE '^\| *[0-9]' "$rep") rows"
      fi
      sz=$(stat -c %s "$rep")
      if [ "$sz" != "${size[$a]}" ]; then size[$a]=$sz; stamp[$a]=$now; stalled[$a]=; fi
      if [ -z "${ended[$a]}" ] && [ -z "${stalled[$a]}" ] && [ $((now-${stamp[$a]:-$now})) -ge $STALL ]; then
        stalled[$a]=1; echo "STALL? $a: report bytes unchanged for $(( (now-stamp[$a])/60 )) min (size $sz) - run the 90-s discriminator before acting"
      fi
    fi
  done
  sleep 60
done
