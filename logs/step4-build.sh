#!/bin/bash
# Usage: step4-build.sh <emsdk_dir> <logname>
EMSDK_DIR=${1:-/d/source/repos/godot-web-dotnet/emsdk}
export EM_CONFIG=$EMSDK_DIR/.emscripten
export PATH=$EMSDK_DIR/upstream/emscripten:$PATH
cd /d/source/repos/godot-web-dotnet/godot-wt/rebase
emcc --version | head -1
time python -m SCons platform=web target=template_debug module_mono_enabled=yes threads=no -j11
echo "EXIT: $?"
