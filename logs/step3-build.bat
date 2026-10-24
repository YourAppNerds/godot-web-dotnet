@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
cd /d D:\source\repos\godot-web-dotnet\godot-wt\rebase
python -m SCons platform=windows target=editor module_mono_enabled=yes d3d12=no -j11
