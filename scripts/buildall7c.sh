#!/bin/bash
S="$(cd "$(dirname "$0")" && pwd)"
L=/d/source/yourappnerds/godot-web-dotnet/logs
B=/d/source/yourappnerds/godot-web-dotnet/godot-wt/rebase/bin
mkdir -p $S/final
run() { # target threads tag extra
  bash $S/build7b.sh $1 $2 $4 > $L/step7c-build-$3.log 2>&1
  echo "$3 $(tail -1 $L/step7c-build-$3.log)"
  th=$([ $2 = no ] && echo .nothreads)
  cp $B/godot.web.$1.wasm32$th.mono.zip $S/final/$3.zip
  cp $B/godot.web.$1.wasm32$th.mono.wasm $S/final/$3.wasm
}
run template_release no release-nothreads-noicu dotnet_web_icu=no
run template_release no release-nothreads
run template_debug no debug-nothreads
run template_debug yes debug-threads
run template_release yes release-threads
echo ALLDONE
