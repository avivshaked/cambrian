#!/bin/bash
# Land every approved v2 over its original and remove the v2, in one working-tree state
# for one commit per directory. Usage: bash scripts/land-v2.sh logbook|primer|research|readmes
# Prints each replacement; refuses a pair whose original does not exist.
set -e
land() { v2="$1"; orig="$2"; [ -f "$orig" ] || { echo "NO ORIGINAL for $v2"; exit 1; }; mv -f "$v2" "$orig"; echo "landed $orig"; }
case "$1" in
  logbook) for v in logbook/[0-9][0-9][0-9][0-9]-v2-*.md; do land "$v" "${v/-v2-/-}"; done ;;
  primer)  for v in primer/[0-9][0-9]-v2-*.md; do land "$v" "${v/-v2-/-}"; done ;;
  research) for v in research/LITERATURE-REVIEW-v2.md research/FETCH-RESULTS-v2.md research/early-life/README-v2.md research/early-life/SOURCES-v2.md research/early-life/MECHANISMS-v2.md; do land "$v" "${v/-v2.md/.md}"; done ;;
  readmes) land logbook/README-v2.md logbook/README.md; land primer/README-v2.md primer/README.md ;;
  *) echo "usage: $0 logbook|primer|research|readmes"; exit 1 ;;
esac
