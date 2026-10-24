# Step 5e report: Mono debug level, jiterpreter, release template

Result: **PASS.**
- C# CPU work is about 3.4x faster on both templates.
- The first template_release (threads=no) is built and installed.
- `samples/WebRuntimeTest` passes 15/15 on both templates.
- HelloWebVisual prints `Hello from C# _Ready` on both templates.

Two commits on `web-dotnet-rebased` in `godot-wt/rebase`, on top of `0207be28d0`. Not pushed.

| Commit | Title |
|---|---|
| `3204c5e473` | [.NET] Web: Start Mono with debugging disabled |
| `9b0e7cad13` | [.NET] Web: Disable the jiterpreter |

Both commits change only `modules/mono/mono_gd/gd_mono.cpp`.

## A. debug_level
- **What debug_level does.** The runtime pack's `src/runtime.c` (`mono_wasm_load_runtime_common`) treats any `debug_level != 0` as "debugging on". It then:
  - starts the interpreter with `interp_opts = "-all,simd"`, which turns all interpreter optimizations off;
  - calls `mono_wasm_enable_debugging`.
- **Why debugging can't work anyway.** The debugger component is linked as a stub (5b, `mono_configure.py`). The browser debug-proxy transport lives in `dotnet.runtime.js`, which Godot doesn't load, and the debugger's wasm imports are stubs. So debug_level=1 gave no debugging, only slower code, in debug templates too.
- **Decision.** `0` for all templates. This matches the .NET SDK's default for browser-wasm Release builds. The code comment says why, so that a nonzero level gets reconsidered if a real debugger is ever linked.

## B. Jiterpreter
- **Option names.** Confirmed from the strings in the pack's `libmonosgen-2.0.a`: `jiterpreter-traces-enabled`, `jiterpreter-interp-entry-enabled` and `jiterpreter-jit-call-enabled`, all on by default in browser builds.
- **How they're set.**
  - The options go through `mono_wasm_parse_runtime_options` (driver.c, which calls `mono_jit_parse_options`) as `--no-<option>`. The call is made before `mono_wasm_load_runtime`, the same way the .NET loader passes `runtimeOptions`.
  - The function is declared `extern "C"` next to `mono_wasm_load_runtime`.
- **Evidence.** Export copies with counters on the six jiterpreter stub imports (`scratchpad/instr.sh`), running WebBench:

| Build | Jiterpreter stub calls |
|---|---|
| Before both fixes | 4500 `mono_interp_record_interp_entry` |
| After A only | 4662 `record_interp_entry` + 96 `mono_interp_tier_prepare_jiterpreter` |
| After A+B | none (`JITC null`) |

- No warnings appeared in the browser console.

## C. Templates
- **Backups** (in the scratchpad):
  - `C:/Users/Kyle/AppData/Local/Temp/claude/d--source-repos-godot-web-dotnet/81e226c9-7366-4a18-ac6c-74873a0c5f88/scratchpad/web_nothreads_debug.5d.zip` is the installed debug template from 5d.
  - `C:/Users/Kyle/AppData/Local/Temp/claude/d--source-repos-godot-web-dotnet/81e226c9-7366-4a18-ac6c-74873a0c5f88/scratchpad/web_nothreads_release.before5e.zip` is the release template built from `0207be28d0`. There was no release zip installed before this step.
- **Build script.** `scratchpad/build5e.sh <target>`, which is `step4-build.sh` with a target parameter.

| Build | Time | Log |
|---|---|---|
| release from `0207be28d0` (first, full) | 6m55s | `step5e-build-release-before.log` |
| release, A only | 1m43s | `step5e-build-release-A.log` |
| release, A+B | 1m47s | `step5e-build-release-AB.log` |
| final release from HEAD | 1m46s | `step5e-build-release.log` |
| final debug from HEAD | 1m20s | `step5e-build-debug.log` |

- All builds returned EXIT 0.
- **Installed.** `web_nothreads_debug.zip` and `web_nothreads_release.zip` in `%APPDATA%/Godot/export_templates/4.8.beta.mono`, both built from `9b0e7cad13`. The engine version line is `custom_build.9b0e7cad1`.

## D. Sizes: HelloWebVisual, final templates

| File | debug raw | debug gzip -9 | release raw | release gzip -9 |
|---|---|---|---|---|
| index.wasm | 34,212,663 | 10,252,050 | 33,741,531 | 10,318,294 |
| index.pck | 7,700,596 | 2,258,750 | 7,700,084 | 2,258,523 |
| index.js | 406,007 | 93,777 | 325,564 | 79,114 |

- **Release saves almost nothing.**
  - The wasm is 471 KB (1.4%) smaller raw, and slightly *larger* gzipped.
  - Both templates use `optimize=size` (-Os), the web default.
  - The wasm is dominated by the prebuilt Mono and runtime-pack libraries: the CODE section is 28 MB and DATA is 5 MB, with no name section.
  - The .pck is the same size in both.
- **Size work should target the wasm instead.** Options include dropping unused runtime-pack libs (ICU, brotli, etc.), `wasm-opt`, and checking what Mono pulls in. The release template doesn't help here.

## D. Benchmark: samples/WebBench
- **The sample.** New, outside git. The project setup is copied from HelloWebVisual and renamed.
- **The workloads.** Eight of them: int-math, double-math, list-sort, dictionary, string-builder, linq, vector2 and virtual-calls.
- **How it measures.** Each workload runs once to warm up, then is timed with `Stopwatch`, best of 3. Results are printed as `BENCH ...` and shown in the Label.
- **Where it ran.** Headless Edge, run by `scratchpad/run5e.sh` (serve.py on port 8067, CDP on 9337, separate profile). Both were stopped after each run.

Times in ms. The final rows are from two runs each.

| Template | int | double | list-sort | dict | sb | linq | vec2 | virt | **total** |
|---|---|---|---|---|---|---|---|---|---|
| debug, before A/B (5d) | 55.1 | 15.1 | 220.4 | 49.9 | 118.9 | 62.4 | 81.4 | 32.5 | **635.7** / 640.6 |
| release, before A/B | 41.7 | 13.7 | 238.8 | 52.9 | 133.9 | 62.3 | 77.8 | 29.5 | **650.7** / 643.2 |
| release, A only | 22.5 | 10.1 | 61.0 | 21.2 | 39.8 | 34.0 | 35.5 | 18.8 | **243.0** / 236.6 / 250.3 |
| release, A+B (pre-final build) | 17.4 | 6.5 | 45.6 | 15.2 | 26.4 | 30.0 | 34.1 | 19.2 | **194.4** / 216.1 / 195.3 |
| **release, final** | 17.7 | 6.5 | 42.1 | 14.3 | 24.1 | 32.2 | 34.5 | 18.7 | **190.1** / 188.8 |
| **debug, final** | 16.8 | 6.4 | 42.6 | 15.1 | 24.1 | 32.2 | 32.6 | 21.6 | **191.6** / 186.9 |

- **A gives about 2.6-2.7x.** Turning on interpreter optimizations matters most.
- **B gives about another 15-20%.**
- **Overall: 640 -> 190 ms (3.4x)** on both templates.
- **The template type makes no difference to C# speed.** All C# runs in the Mono interpreter, and engine debug checks don't matter for this workload.

## Verification (final templates, `9b0e7cad13`)

| Check | debug template | release template (`--export-release`) |
|---|---|---|
| WebRuntimeTest | `WRT DONE pass=15 fail=0` | `WRT DONE pass=15 fail=0` |
| HelloWebVisual | `Hello from C# _Ready` | `Hello from C# _Ready` |

- **Logs:**
  - `logs/step5e-wrt-{debug,release}.log`
  - `logs/step5e-hellowebvisual-{debug,release}.log`
  - `logs/step5e-bench-*.log`
  - `logs/step5e-export-*.log` (no errors)
- **Export directories.** All new: `out/WebBench-*`, `out/HelloWebVisual-5e-{debug,release}` and `out/WebRuntimeTest-5e-{debug,release}`.
  - The `*-instr` directories are the instrumented copies, used only for counting.
- **Port 8070 and `out/HelloWebVisual` were not touched.** The user's server on 8070 is serving the old 5d export.

## New issues / notes
1. **Release template barely smaller** (see sizes). Wasm size reduction needs work on the runtime-pack libraries, not the template target.
2. **Workload sizes.** The benchmark workloads are small (6-45 ms each after the fixes). That's fine for relative comparisons, but they should be scaled up for finer profiling.
3. **Restoring the jiterpreter would need porting its JS** (the wasm code generator in `dotnet.runtime.js`). It would be the next big CPU win, but is a large effort.
4. **Threads builds unchecked.** With threads=yes, the option names are the same, but this wasn't checked.
