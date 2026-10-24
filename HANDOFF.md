# Godot C# Web Export — Handoff

## Goal
Make Godot 4 .NET (C#) projects export to Web. Build on raulsntos/godot4-web-dotnet-prototype (demo project) and the engine PR godotengine/godot#106125 ("[.NET] Add web export support", branch raulsntos:dotnet/mono-static-linking). Personal fork is the work branch; small upstream PRs or comments on proposal #13076 come later, each change with a written rationale.

## Plan
## Steps
1. **Toolchain check + install guide** (sonnet). Check Python 3, SCons, Git, .NET SDK, emsdk (version pinned in Godot web build docs and prototype README), MSVC Build Tools. Install what is missing (user confirms installs). Create workspace layout + `HANDOFF.md`.
2. **Fork + clone** (sonnet). User forks on GitHub (`gh repo fork godotengine/godot`, user confirms). Clone fork + prototype. Record commit hashes.
3. **Desktop .NET editor build** (sonnet). `scons platform=windows module_mono_enabled=yes`; generate glue; `build_assemblies.py`. Confirm editor opens a C# project.
4. **Reproduce prototype** (Opus). Build prototype's web template per its README; run demo in Chrome via COOP/COEP server. Record every failure in HANDOFF.
5. **Gap analysis** (Opus). Diff prototype branch vs current `master`; list port pieces, ordered small-first. Manager reviews, writes step list for phase 2.
6. **Port loop** (Opus, one agent per piece, own worktree). Port piece, build web template, run `samples\HelloWeb`. Commit on branch.
7. **Hardening** (Opus). Single-threaded export, exceptions, reflection/trimming, startup size. Report results.
8. **Upstream** (manager + user). Draft #13076 comment / PR text for user to review. Nothing posted without user OK.

## Status
- 2026-10-22: Step 1b done (toolchain installed, see ## Toolchain, ## Env setup).
- 2026-10-22: Step 2 done. Fork https://github.com/YAN-kyle/godot. Details logs/step2-report.md.
- 2026-10-22: Rebase done: PR #106125 onto upstream/master 06d1e7a34c = commit c81d539c on branch web-dotnet-rebased, worktree godot-wt/rebase. Not pushed. Rationale + risks: logs/rebase-report.md.
- Key risk: .NET 10 browser-wasm runtime pack built with Emscripten 3.1.56; Godot master pins 6.0.12.
- 2026-10-22: Step 3 done. Editor builds (d3d12=no, vcvars64 wrapper logs/step3-build.bat), nupkgs 4.8.0-beta in nupkgs/, samples/HelloWeb prints on desktop. Details logs/step3-report.md.
- 2026-10-22: Step 4 DONE — MILESTONE. C# web template builds (Emscripten 6.0.12, threads=no, template_debug); HelloWeb exports and prints "Hello from C# _Ready" in headless Edge. 5 fix commits on web-dotnet-rebased, HEAD 7bca276d6e, not pushed. Template installed in %APPDATA%\Godot\export_templates\4.8.beta.mono. Export at out/HelloWeb. Details logs/step4-report.md.
- PAUSED after step 4 for user review (user opened folder in VS Code).
- 2026-10-22: Step 5a DONE. DOTNET.setup fixed via modules/mono/runtime/web/library_godot_dotnet.js (template-side). Commit 3affe161ca, not pushed. Plain export runs in headless Edge. Details logs/step5a-report.md.
- 2026-10-22: Step 5b DONE. c9790251b2 (pck: only .dll/.pdb on Web), 7c04c98993 (stub debugger/diagnostics/hot-reload, debug+release). HelloWeb debug pck 41.4 MB -> 7.7 MB (gzip 15.6 -> 2.3 MB); wasm 34.5 -> 34.2 MB. Verified. Details logs/step5b-report.md.
- 2026-10-22: Step 5c DONE. 3fef7d6ce4 (hide host EM_CONFIG/EM_CACHE from runtime pack publish), 6c7a5edb7f (no build servers left after exports/headless builds; root cause = console wrapper job object waits for VBCSCompiler, 10 min idle). Export 744 s -> 16 s Web, 617 s -> 8 s desktop. 6c7a5edb7f is general Windows bug, master same code: separate upstream PR candidate. Details logs/step5c-report.md.
- 2026-10-22: Step 5d DONE. 5 commits a13719a549..0207be28d0: timers/thread-pool jobs, crypto.getRandomValues entropy (was zero-fill SECURITY bug: constant Guid), TZ from browser, runtime log + Console.Clear, JS interop throws JSException. samples/WebRuntimeTest 8/15 -> 15/15 pass. Installed template_debug built before commits (version string 6c7a5edb7). Details logs/step5d-report.md.
- 2026-10-22: Step 5e DONE. 3204c5e473 (debug_level 0 all templates), 9b0e7cad13 (jiterpreter off). WebBench 640 -> 190 ms (3.4x) both templates. template_release built first time; debug+release installed from HEAD. Release barely smaller (wasm 33.7 vs 34.2 MB; Mono libs ~28 MB dominate). WebRuntimeTest 15/15 both. Details logs/step5e-report.md.
- 2026-10-22: Added samples/HelloWebVisual (Label + frame counter from C#) for visual check.
- 2026-10-23: Step 7a DONE. threads=yes debug+release templates build (MT runtime pack from NuGet; EH shim + JS stubs held). 4 commits 48feebb0f3..9c83382ba5 (MT pack, export passes WasmEnableThreads + separate obj dirs, read-only jiterpreter opts, pool size >= 12). WRT 15/15 threads debug/release + nothreads debug; WebBench ~180-197 ms threaded = nothreads. Installed web_debug.zip/web_release.zip; samples have "Web Threads" preset. Details logs/step7a-report.md.
- 2026-10-23: Step 7b DONE. 5 commits 02293c7363..d306e4e5fa: GodotSharp native calls trapped on wasm ("function signature mismatch": any engine error print/GD.PushError, Array.Resize, await ToSignal, Variant.AsString) fixed via m2n signatures + Error-as-long + single-scalar C interop structs; EH shim guard >= Emscripten 6.0.6 (rename = LLVM 1d4e04f7a7c2 / emscripten d212b13bed0b); opt-in ICU globalization (InvariantGlobalization=false, +0.5 MB gzip PCK). SIMD already works. WRT 43/0 ICU, 39/0 invariant, threads + nothreads. Details logs/step7b-report.md.
- 2026-10-23: Step 7c DONE. 4 commits d990e1edf9..67806bd96f: invariant default really applies now (7b bug: WasmFeatures.props set false first; PCK -1.6 MB), opt-in GodotTrimGodotSharp (PCK 7.7 -> 3.9 MB), opt-in SCons dotnet_web_icu=no (wasm -1.3 MB raw / -0.4 MB br), trimmer reruns on root change. Mono is only ~3 MB of the 34 MB wasm; ~31 MB is Godot engine. WRT 43/0 ICU, 39/0 inv (debug, release, threads). Details logs/step7c-report.md.
- 2026-10-24: Backup. Engine branch pushed to origin/web-dotnet-rebased (YAN-kyle/godot, 31 commits, HEAD 67806bd96f). Workspace repo (HANDOFF, reports, samples, scripts) public at https://github.com/YourAppNerds/godot-web-dotnet. Raw .log files not tracked. Next: step 7d real-game test (game type TBD with user).
- Step 1 (toolchain check): DONE 2026-10-22. Workspace dirs created (logs, samples). godot\ and prototype\ left for step 2. Missing: SCons, emsdk, MSVC Build Tools, wasm-tools workload, .NET 9+ SDK.

## Toolchain
| Tool | Found | Required | Action |
|---|---|---|---|
| git | 2.56.0.windows.1 | any | none |
| gh | 2.87.0, logged in as YAN-kyle | any | none |
| Python | 3.14.3 (python, py) | 3.10+ (3.14 has zstd built in) | none |
| SCons | DONE: 4.11.1 (user site; `scons` NOT on PATH) | 4.4+ | use `python -m SCons` |
| .NET SDK | DONE: 10.0.201 + 9.0.319 side by side. wasm-tools on both: SDK10 manifest 10.0.108 (packs 10.0.8), SDK9 manifest 9.0.21. Emscripten pack for both = 3.1.56 (dotnet/runtime release/10.0 Versions.props EmsdkVersion) | Godot docs: 8.0+; prototype: 9.0+ with wasm-tools workload | dotnet workload install wasm-tools (check wasm-tools-net9 / SDK 10 compat; may need SDK 9) |
| emsdk/emcc | DONE: emsdk\ cloned; 6.0.12 installed+activated (godotengine/godot master web_builds.yml EM_VERSION=6.0.12; detect.py min 6.0.1). PR #106125 branch pin was 3.1.64 (min 3.1.62); installed first but 6.0.12 replaced emsdk\upstream, reinstall to switch. RISK: .NET 10 browser-wasm pack uses emsdk 3.1.56 vs Godot 6.0.12 (orig: not found (not on PATH, no C:\emsdk or ~\emsdk) | Godot docs page says "6.0.12+" (looks like typo; check platform/web in the godot tree for real pin, likely 4.x) | clone emsdk (step 2/3), install pinned version |
| MSVC Build Tools | DONE: BuildTools 17.14.42 + VCTools, MSVC 14.44.35207 (needed winget --force --passive: Build Tools already installed without workload) | needed for desktop build (step 3) | winget install Microsoft.VisualStudio.2022.BuildTools with C++ workload |
| Disk C: | 369 GB free of 931 GB | ~50+ GB | none |

## Env setup
- emsdk (not on permanent PATH). Git Bash: `export EM_CONFIG=/d/source/repos/godot-web-dotnet/emsdk/.emscripten; export PATH=/d/source/repos/godot-web-dotnet/emsdk/upstream/emscripten:$PATH; emcc --version` -> 6.0.12. (`source emsdk_env.sh` did not put emcc on PATH under MSYS UCRT64.)
- PowerShell: `& D:\source\repos\godot-web-dotnet\emsdk\emsdk_env.ps1`; cmd: `emsdk_env.bat`.
- SCons: `python -m SCons ...` (scons.exe in %APPDATA%\Python\Python314\Scripts, not on PATH).
- dotnet 9 vs 10 chosen by global.json (`{"sdk":{"version":"9.0.0","rollForward":"latestFeature"}}`); default is 10.0.201.
- MSVC: `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat`.
- No reboot needed.

## Prototype notes
- The prototype repo (default branch master, last push 2025-05-06) is only a DEMO PROJECT (Roslyn compiles C# at runtime, runs via Mono interpreter). The engine work is in godotengine/godot PR #106125 (open, state CONFLICTING with master, updated 2026-10-11, head aa1f5ff, branch raulsntos/godot:dotnet/mono-static-linking, base master).
- Needs a Godot editor + web template built from that PR, .NET SDK 9.0+ with wasm-tools workload.
- Demo needs multithreading: set WasmEnableThreads=true in the csproj to use the threaded (experimental) Mono runtime.
- Export: godot --headless --export-release "Web" "out/index.html"; then manually edit out index.js to remove the DOTNET.setup call (known caution).
- Serve with python serve.py --root out (COOP/COEP headers).
- PR notes: Mono runtime fetched via GetRuntimePack C# project (needs wasm-tools); invariant globalization always; some BCL APIs (crypto) missing since dotnet.runtime.js stubs not replaced.
- Godot web build docs: python 3.10+, scons 4.4+, emcc on PATH; scons platform=web target=template_release|template_debug (threads=no for single-threaded). Docs C# page: .NET 8+; scons module_mono_enabled=yes, run editor .mono binary with --headless --generate-mono-glue modules/mono/glue, then modules/mono/build_scripts/build_assemblies.py --godot-output-dir ./bin --push-nupkgs-local <dir> --godot-platform=windows.
- Docs URLs that work: docs.godotengine.org/en/latest/engine_details/development/compiling/compiling_for_web.html and compiling_with_dotnet.html.

## Decisions
- 2026-10-22: Use Emscripten 6.0.12 (Godot master pin) with .NET 10 runtime pack built on 3.1.56; gap bridged by shim modules/mono/runtime/web/wasm_eh_compat.c. emsdk-3.1.56 not needed.
- 2026-10-22: Fix commits stay one-fix-per-commit with rationale in body (may go upstream). Nothing pushed without user OK.
- 2026-10-22: Work branch = web-dotnet-rebased (master requires .NET 10; PR moved to net10.0).
- 2026-10-22: User pre-approved all tool installs.
- 2026-10-22: Engine base = godotengine/godot PR #106125 (raulsntos:dotnet/mono-static-linking, head aa1f5ff). Step 2 fetches PR branch + master. Fork needs explicit user OK (public action).
- Step 2 should fork godotengine/godot and fetch the PR branch (raulsntos:dotnet/mono-static-linking) as the base, not only master.
- 2026-10-22: Project moved to D:/source/repos/godot-web-dotnet.
## Open failures
1. FIXED 3affe161ca (DOTNET.setup).
2. FIXED c9790251b2 (pck filter).
3. FIXED 7c04c98993 (stub components).
4. DONE 7b: exceptions (incl. across signals/Callable/CallDeferred), SIMD, IANA time zones pass. Open: no file/line in C# stack traces (PDBs not loaded); exception messages are resource keys; Windows TZ ids not found; m2n signature list (GetRuntimePack/NativeFuncsSignatures.cs) hand-kept. threads=yes: DONE 7a; open: JSHost throws NRE (not JSException) on MT, pool-size check only logs error, real multithreaded C# workloads/finalizer thread untested.
5. FIXED 6c7a5edb7f (export hang).
6. DONE 7b (2755e27e3c): rename = LLVM 1d4e04f7a7c2 (#209282), Emscripten d212b13bed0b (#27445), first in 6.0.6; shim guard fixed to >= 6.0.6 (was >= 5). Untested on 6.0.1-6.0.5.
7. FIXED 3fef7d6ce4 (EM_CONFIG).
12. GUI editor launched via console exe can still leave console waiting up to 15 min after close (wrapper-side fix).
13. Runtime-pack publish seems to run twice per SCons build.
9. FIXED 3204c5e473. Was: gd_mono.cpp passes debug_level=1 to mono_wasm_load_runtime even in release: disables interpreter optimizations.
10. runtimeconfig.json never applied on Web.
11. First run copies whole publish folder to cache; could read PCK directly.
8. FIXED 5d (mono_wasm_* imports). Globalization: opt-in ICU DONE 7b (d306e4e5fa; default stays invariant; one icudt file in PCK; HybridGlobalization gone in .NET 10). Left: [JSImport] interop unsupported (throws).
14. FIXED 9b0e7cad13. Was: Jiterpreter on by default with stub imports: wasted interpreter work; disable via runtime options.
15. 7c: decide official template policy for dotnet_web_icu (ICU vs not) and whether GodotTrimGodotSharp becomes default; production=yes (lto=thin) makes web mono wasm +0.76 MB.
- None yet.

## Next brief
Resume as manager: read only ## Status, ## Decisions, ## Open failures. Then run, one fresh subagent each (Opus; each in its own worktree branched from web-dotnet-rebased, or sequentially in godot-wt/rebase):
- 5a: DONE.
- 5b: Fix open failure 2 (export filter) + 3 (stub components); report .pck/.wasm size before/after.
- 5c: DONE.
- 5d: DONE.
- 5e: DONE.
- 7a: DONE.
- 7b: DONE.
- 7c: DONE (Mono is ~3 MB, not 28; custom runtime pack plan in step7c-report.md). Later idea: port jiterpreter JS side (large).
- Note: samples/ not in git; decide where test samples live before upstream.
- Then step 7 hardening (open failure 4, 6): exceptions/SIMD/timezones test scene, threads=yes and template_release builds.
Ask user before pushing any branch or posting upstream.

## Reports
### Step 1 (sonnet)
Toolchain check done. Missing: SCons, emsdk, MSVC Build Tools, wasm-tools workload, .NET 9 SDK (only 10.0.201 present). Key surprise: prototype is just a demo; engine changes live in PR #106125 (conflicting with master). Disk 369 GB free.

### Step 1b (sonnet)
All installed, no reboot. SCons 4.11.1 (python -m SCons). MSVC 14.44 via winget --force (plain install failed: Build Tools already present; vs_installer modify needs elevation). .NET 9.0.319 + 10.0.201, wasm-tools on both. emsdk 6.0.12 active (upstream master pin); 3.1.64 = PR pin. .NET 10 runtime pack emsdk = 3.1.56 (mismatch risk). Logs in logs\.

### Move to D: (2026-10-22)
Copied via robocopy (55661 files, ~6.97 GB, matched); git worktree repaired, emsdk re-activated (emcc 6.0.12), text files/nuget.config path-fixed, HelloWeb obj/bin/.godot/mono removed and rebuilt. Smoke: build-solutions exit 0, "Hello from C# _Ready". Old .log files keep old paths. Source folder deleted (empty dir may remain, held by a session cwd). Log: logs/robocopy-move.log.
