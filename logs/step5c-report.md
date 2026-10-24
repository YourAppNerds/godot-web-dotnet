# Step 5c report: export hang and EM_CONFIG leak into the runtime-pack publish

Result: **PASS.** A plain export of samples/HelloWeb, with Main.cs touched so C# really compiles and no env workaround, returns in 16 s. Headless Edge prints `Hello from C# _Ready` with no exceptions (`logs/step5c-browser.log`).

Commits on `web-dotnet-rebased` in `godot-wt/rebase`, on top of `7c04c98993`. Not pushed.
- `3fef7d6ce4` [.NET] Web: Hide the host Emscripten config from the runtime pack publish
- `6c7a5edb7f` [.NET] Don't leave build servers running after exports and headless builds

## A. Export hang

### The root cause is not the pipe
The stdout-pipe theory was wrong. The cause is the console wrapper's job object combined with the Roslyn compiler server.

- **What the wrapper does.** `platform/windows/console_wrapper_windows.cpp`, which builds `godot.*.console.exe`:
  - It starts the editor in a job object (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`).
  - It then waits for `JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO`, which means it returns only after every process in the job has exited.
- **What the build leaves behind.** GodotTools runs `dotnet publish`. Its Csc task starts `VBCSCompiler.exe`, the compiler server, as a child of dotnet.exe, so it lands in the job. The server stays up until it has been idle for 10 minutes (the Roslyn default). MSBuild reuse nodes, when they get spawned, stay up for 15 idle minutes.
- **Evidence (run `before-web2`, Main.cs touched).**
  - The editor process (`godot...mono.exe`) and dotnet.exe exited about 1 minute after start.
  - `godot...console.exe` and `VBCSCompiler.exe` (parent = the export's dotnet.exe, `IsProcessInJob` = true) stayed alive.
  - The console exe returned at t=744 s. That is 10 minutes after the server's last use: the step-5c SCons runtime-pack publish reused that server at about 22:36:40, and the return came at 22:46:39.
  - `godot_exit` and `pipe_eof` came at the same moment, so the wrapper process itself was blocking, not a pipe.
  - No MSBuild nodes were spawned. HelloWeb is a single project.
- **Why it looked intermittent.** If a VBCSCompiler is already running outside the job (started by an IDE, an earlier build, and so on), csc reuses it and the export returns at once.
  - The same is true when nothing needs compiling. Run `before-web` (up to date) returned in 10 s.
  - So the 5a/5b "MSBUILDDISABLENODEREUSE=1 fixed it" observation was most likely luck. The variable affects only MSBuild nodes, not the compiler server.

### Fix
Changed `GodotTools/Build/BuildSystem.cs` with a new `AddBuildServerArguments`.
- **What it passes.** `-nodeReuse:false -p:UseSharedCompilation=false`.
- **When.** For every `dotnet publish`, which means exports from both the CLI and the GUI. Also for `dotnet build` when `DisplayServer.GetName() == "headless"` (for example `--build-solutions`).
- **Not for interactive editor builds.** They keep the warm servers, because that makes rebuilds faster. The trade-off: closing a GUI editor that was launched through console.exe can still leave the console waiting up to 10 minutes. Fixing that would need a change in the wrapper (see upstream notes).

### Timings (headless, Main.cs touched to force a compile, no env workaround)
| Run | Export done | Command returns |
|---|---|---|
| Web, before fix | ~60 s | **744 s** |
| Web, after fix | 16 s | **16 s** |
| Desktop (`--export-pack "Windows"`), before fix | ~20 s | **617 s** |
| Desktop, after fix | 8 s | **8 s** |

- After both fixed runs, no new VBCSCompiler or dotnet process was left. The surviving VBCSCompiler 405128 was from the desktop baseline, and the fixed runs didn't connect to it.
- Logs: `logs/step5c-times-*.txt`, `logs/step5c-export-*.log`.
- The desktop test used a copy of HelloWeb with a "Windows Desktop" preset, at `C:/Users/Kyle/AppData/Local/Temp/claude/d--source-repos-godot-web-dotnet/81e226c9-7366-4a18-ac6c-74873a0c5f88/scratchpad/HelloDesk`. Export-pack needs no Windows template.

### Upstream relevance
- **General Windows bug, not Web-specific.** Upstream master (`D:/source/repos/godot-web-dotnet/godot` @ `06d1e7a34c`) has the same `BuildSystem.cs` launch code (no nodeReuse or shared-compilation handling) and an identical `console_wrapper_windows.cpp`.
- So any Windows `godot.*.console.exe --export-*` of a C# project that compiles hangs for about 10 minutes after finishing, unless a compiler server is already running. This hits CI badly.
- Commit `6c7a5edb7f` has no Web dependencies and is worth sending as a separate upstream PR.
- An alternative or extra upstream option: have the wrapper wait only for the main process (or allow breakaway). That would also cover GUI sessions, but it changes the wrapper's semantics for editor restarts, so maintainers should decide.

## B. EM_CONFIG leak into the GetRuntimePack publish
- **Repro.** `rm -rf GetRuntimePack/obj/Release bin`, then with EM_CONFIG set ran `dotnet publish GetRuntimePack -r browser-wasm --self-contained -c Release`.
  - It exited 1 with `"$CFGDIR/upstream/bin\clang.exe"` errors compiling `pinvoke.c`/`driver.c`. Log: `logs/step5c-runtimepack-before.log`.
  - The SDK's emcc 3.1.56 reads our emsdk-6 `.emscripten`, whose `$CFGDIR` it doesn't expand.
- **Which vars matter.**
  - EM_CONFIG is the culprit. Without it, emcc uses the pack's own `tools/emscripten/.emscripten`, which reads the `DOTNET_EMSCRIPTEN_*` vars.
  - EM_CACHE is removed defensively. The SDK sets it whenever `WasmCachePath` is set, and the Cache pack always sets that.
  - EMSDK_PYTHON and PYTHONPATH are overridden by the SDK. PATH gets the SDK dirs prepended. EMSDK and EMSDK_NODE aren't read by the pack's config.
- **Fix.** `mono_configure.py` passes `env=` with EM_CONFIG and EM_CACHE removed to the `subprocess.call`.
- **Verified.** Cleaned the runtime-pack output, then ran `logs/step4-build.sh` (EM_CONFIG set, emcc 6.0.12).
  - The publish ran and succeeded. SCons finished with EXIT 0 in 3m33s. Log: `logs/step5c-build.log`.
  - The installed template was not replaced. It was built from the same sources (5b), and the rebuilt one only differs in build env.

## Other
- GodotTools was rebuilt with `build_assemblies.py --godot-output-dir=./bin --godot-platform=windows`. Log: `logs/step5c-assemblies.log`.
- Browser test: `serve.py -p 8065`, Edge CDP on 9335. Both were stopped afterwards.

## New issues
1. **GUI editor via console.exe.** Interactive editor builds still start the build servers in the wrapper's job, so closing the editor can leave console.exe waiting up to 10 or 15 minutes. A wrapper-level fix (upstream discussion) would be needed.
2. **The runtime-pack publish ran twice in one SCons run.** `step5c-build.log` shows the GetRuntimePack output twice, so `mono_configure` seems to be invoked twice. That costs extra build time. Not investigated, and it may predate this work.
