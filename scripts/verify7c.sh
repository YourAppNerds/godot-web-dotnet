#!/bin/bash
# usage: verify7c.sh <project> <preset> <tag> <mode debug|release> [secs]
S="$(cd "$(dirname "$0")" && pwd)"
bash $S/export7c.sh $1 "$2" 7c-$3 --export-$4 $3 | head -3
bash $S/run7a.sh 7c-$3 step7c-run-$3.log ${5:-30} | grep -E 'WRT DONE|BENCH DONE|Hello from|exception' | head -3
python $S/sizes.py /d/source/repos/godot-web-dotnet/out/7c-$3/index.pck
