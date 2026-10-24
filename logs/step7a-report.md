# Step 7a report: threads=yes web templates

Result: **PASS.**
- The threads=yes C# web templates (template_debug and template_release) build with Emscripten 6.0.12 and the .NET 10 **multithreaded** runtime pack (`Microsoft.NETCore.App.Runtime.Mono.multithread.browser-wasm` 10.0.8, which NuGet downloads; it isn't part of the wasm-tools workload).
- The wasm_eh_compat.c shim and the 5d `mono_wasm_*` JS import stubs held with threads. They needed no changes, and there were no link errors.
- WebRuntimeTest passes 15/15 with threads (debug and release) and without threads (debug, rebuilt from HEAD).
- In WebBench, threaded is the same speed as non-threaded.

Four commits on `web-dotnet-rebased` in `godot-wt/rebase`, on top of `9b0e7cad13`. Not pushed.

| Commit | Title |
|---|---|
| `48feebb0f3` | [.NET] Web: Use the multithreaded runtime pack for threads=yes builds |
| `fff008344b` | [.NET] Web: Publish with the runtime pack matching the export's thread support |
| `a27e85b39a` | [.NET] Web: Don't set read-only jiterpreter options in threaded builds |
| `9c83382ba5` | [.NET] Web: Require a larger pthread pool for threaded C# exports |

## Failures found, shortest error lines, fixes

1. **The template used the single-threaded pack.** `GetRuntimePack.csproj` hardcoded `WasmEnableThreads=false`.
   - Fix `48feebb0f3`: `mono_configure.py` passes `-p:WasmEnableThreads=<threads>`.
   - Threaded publishes also get their own `obj/Release-threads` dir, and the include path follows it.
2. **The export published single-threaded assemblies.** Error:
   - `[MONO] icall.c:6989 <disabled>` / `Your mono runtime and class libraries are out of sync. The out of sync library is: System.Private.CoreLib.dll`
   - followed by `RuntimeError: function signature mismatch`
   - The internal calls differ between the packs:
     - MT CoreLib has LowLevelLifoSemaphore.* and Thread.WarnAboutBlockingWait.
     - ST CoreLib has TimerQueue.MainThreadScheduleTimer and ThreadPool.MainThreadScheduleBackgroundJob.
   - Fix `fff008344b`:
     - GodotTools passes `WasmEnableThreads` to `dotnet publish` from the preset's `threads` feature (the "Thread Support" option). `BuildManager.PublishProjectBlocking` gains a `customProperties` parameter.
     - `Browser.targets` only defaults the property to false.
     - `Browser.props` sends threaded builds to `obj/<Config>/threads/` and `bin/<Config>/threads/`.
3. **Stale trimmed assemblies.** Root cause of the first failure seen after fix 2 (the 7 s export).
   - The runtime pack files are dated 2026-04-29, older than the previous build's `linked/` outputs. So ILLink's timestamp check skipped trimming, and the export kept the ST CoreLib.
   - Covered by the separate directories in fixes 1 and 2.
   - **Verified by toggling:** threads export, then non-threads export, then threads export again. Each one ran 15/15.
4. **Read-only jiterpreter options.** Error: `Unable to set option 'no-jiterpreter-interp-entry-enabled' as it's read-only.`, and then the same error for `jit-call`.
   - The MT runtime already turns these off.
   - Fix `a27e85b39a`: pass them only when `!THREADS_ENABLED`.
5. **pthread pool too small: deadlock.** Error: `Tried to spawn a new thread, but the thread pool is exhausted.` WRT then never finished.
   - With the default `threads/emscripten_pool_size=8`:
     - two spawns hit an empty pool;
     - the page hung.
   - With 10: one warning, then 15/15.
   - With 12 and 16: clean.
   - Fix `9c83382ba5`: the C# export reports an error when the pool size is below 12.
   - **Caveat:** errors thrown in GodotTools' `_ExportBegin` are only logged, and the export still writes files. This is the existing GodotTools behaviour, so the check is a loud error, not a hard stop.

## Templates (where they live and how they're picked)
- **Installed in `%APPDATA%\Godot\export_templates\4.8.beta.mono`:**
  - `web_debug.zip` and `web_release.zip`: threaded, new. Built from the working tree with all four fixes (version string `9b0e7cad1`).
  - `web_nothreads_debug.zip`: rebuilt from HEAD in 7a.
  - `web_nothreads_release.zip`: unchanged from 5e. Its threads=no code is identical, because the only gd_mono change is behind `#ifndef THREADS_ENABLED`.
- **How the export picks a template.** The Web export's "Thread Support" option (`variant/thread_support`) selects `web_*.zip` when on and `web_nothreads_*.zip` when off. The names differ, so nothing was overwritten.
- **Backups** (in the scratchpad `C:\Users\Kyle\AppData\Local\Temp\claude\d--source-repos-godot-web-dotnet\5ae04542-54a1-455e-928b-e828349f8d1e\scratchpad\`):
  - `web_nothreads_debug.5e.zip` and `web_nothreads_release.5e.zip`;
  - `nupkgs-before7a\`.
- **Build outputs** are in `godot-wt/rebase/bin/godot.web.template_{debug,release}.wasm32.mono.zip`.
- **Build command:** `scratchpad\build7a.sh <target> <yes|no>`. It is `python -m SCons platform=web target=<t> module_mono_enabled=yes threads=<yes|no> -j11` with emsdk 6.0.12 on PATH.

| Build | Time | Log |
|---|---|---|
| threads debug (first) | 8m50s | step7a-build-debug-threads.log |
| threads debug (rebuilds) | 1m20s-1m35s | -threads2/3 |
| threads release | 6m54s | step7a-build-release-threads.log |
| nothreads debug | 1m43s | step7a-build-debug-nothreads.log |

- **GodotTools and the nupkgs were rebuilt:** `build_assemblies.py --godot-output-dir=./bin --godot-platform=windows --push-nupkgs-local D:/source/repos/godot-web-dotnet/nupkgs`.
  - Then `~/.nuget/packages/godot.net.sdk/4.8.0-beta` was deleted, so the new `Browser.props` and `Browser.targets` are picked up.
  - Logs: `step7a-assemblies*.log`.

## Samples
- `samples/WebRuntimeTest` and `samples/WebBench` each have a new preset, `"Web Threads"` (preset.1), with:
  - `variant/thread_support=true`
  - `threads/emscripten_pool_size=12`
- Export: `godot...mono.console.exe --headless --path samples/<P> --export-debug|--export-release "Web Threads" out/<dir>/index.html` (`scratchpad\export7a.sh`).
- Run: `scratchpad\run7a.sh`, which is the 5e runner using serve.py (COOP/COEP headers) on port 8077 and headless Edge with CDP on 9347.

## Results

| Check | threads debug | threads release | nothreads debug |
|---|---|---|---|
| WebRuntimeTest | 15/15 | 15/15 | 15/15 |
| WebBench total ms (2 runs) | 196.8 / 178.0 | 179.6 / 183.9 | 190.3 / 194.5 |
| index.wasm raw / gzip -9 | 34.59 / 10.34 MB | 33.57 / 10.35 MB | 34.21 / 10.25 MB |

- 5e reference numbers: nothreads debug 191.6 / 186.9 ms, release 190.1 / 188.8 ms.
- **Threads don't change single-thread C# speed.** All of it runs in the Mono interpreter on the main thread.
- **Logs:**
  - `step7a-wrt-*.log`
  - `step7a-bench-*.log`
  - `step7a-export-*.log`
  - `step7a-wrt-threads-debug-pool{10,12,16}.log` (the pool-size probe)

## Open problems
1. **JS interop error in the MT build.** `JSHost.GlobalThis` throws `NullReferenceException` here, not the clear `JSException` from 5d. The MT CoreLib/JS interop goes through different icalls and imports (`InvokeJSImportSync`, `BindJSImport` without `ST`), which the 5d shim doesn't cover. It's still unsupported either way, only the message is worse.
2. **The pool-size check doesn't stop the export** (see failure 5).
   - Alternatives: a mono-specific default of 12+ in `platform/web`, or `proxy_to_pthread`, so that the browser main thread never blocks.
   - The real need may be more than 12 under heavier thread-pool use. Only WRT and WebBench were tested.
3. **Untested on the threaded build:**
   - real multi-threaded C# workloads (`Parallel.For`, many `Task.Run` workers, blocking waits on the main thread);
   - GC under contention;
   - the finalizer thread. `mono_wasm_init_finalizer_thread` is never called, and it isn't known whether finalizers run.
4. **Emscripten's warning `-pthread + ALLOW_MEMORY_GROWTH may run non-wasm code slowly`** applies to the Godot web build in general.
5. **The pre-fix export dirs of WebRuntimeTest were polluted** (their `obj/ExportDebug/browser-wasm` was deleted by hand once). Any user project exported with threads before `fff008344b` may need `.godot/mono/temp/obj` cleared once.
