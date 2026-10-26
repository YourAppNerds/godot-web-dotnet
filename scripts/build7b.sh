#!/bin/bash
# Usage: build7b.sh <target> <threads yes|no> [extra scons args...]
EMSDK_DIR=/d/source/YAN-kyle/godot-web-dotnet/emsdk
export EM_CONFIG=$EMSDK_DIR/.emscripten
export PATH=$EMSDK_DIR/upstream/emscripten:$PATH
cd /d/source/YAN-kyle/godot-web-dotnet/godot-wt/rebase
emcc --version | head -1
t=$1; th=$2; shift 2
time python -m SCons platform=web target=$t module_mono_enabled=yes threads=$th -j11 "$@"
echo "EXIT: $?"
