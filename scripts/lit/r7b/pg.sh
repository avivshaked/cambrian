#!/bin/sh
# usage: pg.sh file regex [context]
awk -v re="$2" -v c="${3:-0}" 'BEGIN{IGNORECASE=1} /^### Page/{p=$3} {L[NR]=$0; P[NR]=p} END{for(i=1;i<=NR;i++) if(L[i] ~ re){for(j=i-c;j<=i+c;j++) if(j>0) print "p"P[j]": "L[j]; print "--"}}' "$1"
