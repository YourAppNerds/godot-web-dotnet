#!/bin/bash
# usage: export7a.sh <project> <preset> <outname> <mode --export-debug|--export-release> <logtag>
W=/d/source/repos/godot-web-dotnet
mkdir -p $W/out/$3
start=$(date +%s)
$W/godot-wt/rebase/bin/godot.windows.editor.x86_64.mono.console.exe --headless --path $W/samples/$1 $4 "$2" $W/out/$3/index.html > $W/logs/step7c-export-$5.log 2>&1
echo "rc=$? t=$(( $(date +%s) - start ))s"
grep -i -E "error|fail" $W/logs/step7c-export-$5.log | head -10
