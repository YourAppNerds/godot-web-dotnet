# Step 7c report: Web template and PCK size

**Result:** done, with one correction to the premise. The Mono runtime pack is **not** 28 MB of the wasm.
- It is about **3.0 MB raw** (1.9 MB of code plus about 0.7 MB of data, roughly 1 MB gzip). That is the same as the stock `dotnet.native.wasm` (2.96 MB).
- The other **~31 MB (91%) is the Godot engine itself.**
- Step 5e's "Mono libs ~28 MB" was really the whole CODE section.

Four commits on `web-dotnet-rebased` (`godot-wt/rebase`), on top of `d306e4e5fa`. Not pushed.

| Commit | Title |
|---|---|
| `d990e1edf9` | [.NET] Web: Default to invariant globalization before the SDK props |
| `f99803dd56` | [.NET] Web: Add GodotTrimGodotSharp to let the trimmer trim GodotSharp |
| `0e25a79588` | [.NET] Web: Add dotnet_web_icu option to build templates without ICU |
| `67806bd96f` | [.NET] Web: Run the trimmer again when its roots change |

## 1. Measurement: what is in the wasm

**Method:**
- A names build (temporarily dropping `-Wl,-s`, adding `--profiling-funcs -Wl,--Map=...`; not committed) of template_debug threads=no.
- Final function body sizes come from the name section, attributed to archives through the wasm-ld map.
  - Scripts: scratchpad `wasmattr.py` (final code) and `mapsum.py` (pre-wasm-opt CODE/DATA per archive).
  - Logs: `step7c-wasmattr-base.log`, `step7c-mapsum-base.log`.

Final wasm (debug, threads=no) is 34.21 MB: CODE 28.40 MB, DATA 5.2 MB, no custom sections.

| Part | Final code | Data (pre-opt) | Notes |
|---|---|---|---|
| **Mono runtime pack, total** | **2.23 MB** | **0.73 MB** | ≈ stock dotnet.native.wasm (2.96 MB) |
| libmonosgen-2.0 (VM, metadata, SGen GC, marshal, icalls) | 0.71 | 0.05 | biggest objects: icall.c, metadata.c, marshal.c, aot-runtime.c, sgen-marksweep.c |
| libmono-ee-interp (interpreter) | 0.22 | 0.04 | |
| ICU (libicui18n + libicuuc) | 0.92 | 0.38 | only used when InvariantGlobalization=false |
| libSystem.Globalization.Native | 0.02 | – | |
| wasm-bundled-timezones | – | 0.24 | tzdata, needed for TimeZoneInfo |
| System.Native, SIMD, EH, icall table, zlib, components | < 0.1 | < 0.03 | debugger/diagnostics/hot_reload are stubs (5b). No AOT or jiterpreter code is linked (jiterpreter is JS-side, off since 5e). |
| modules/mono (Godot glue) | 0.10 | 0.03 | |
| **Godot engine** | **~26 MB** | **~4.4 MB** | scene 8.10, core 3.76, servers 2.78, jolt 1.27, drivers 0.79, gdscript 0.66, physics_2d 0.64, harfbuzz 0.56, main 0.54, gltf 0.54, visual_shader 0.48, tilemap 0.47, fbx 0.43, physics_3d 0.42, Godot's own ICU 0.27 (+0.36 data), ... |

**Linking (no-ops):**
- `-Wl,-whole-archive` on the Mono libs costs nothing: `--gc-sections` drops unreferenced functions anyway. Removing it saved 1.6 KB.
- The `libicudata.a` in the pack is a 588-byte stub; the ICU data is in the PCK.

## 2. Wasm cuts tried

| Experiment | raw | gzip -9 | brotli 11 | Kept? |
|---|---|---|---|---|
| baseline (template_debug threads=no) | 34,214,019 | 10,251,147 | 7,482,191 | – |
| no `-whole-archive` for the Mono libs | −1.6 KB | – | – | no (no effect) |
| `lto=thin` (official `production=yes` uses this) | **+759 KB** (34,973,363) | +350 KB | +219 KB | no: LTO at -Os makes it bigger |
| `linkflags=-Oz` | 0 | 0 | 0 | no effect: SConstruct appends `-Os` after `linkflags`, so -Os wins |
| extra `wasm-opt -Oz` pass on the output (same features as emcc) | −941 KB | −101 KB | −17 KB | no: transfer saving is negligible; another link-time pass; risk to engine speed |
| **`dotnet_web_icu=no`** (`0e25a79588`) | **−1,324 KB** | **−532 KB** | **−400 KB** | yes, opt-in |

## 3. Commits

**`d990e1edf9` Default to invariant globalization before the SDK props.** This is a 7b bug found while measuring the PCK.
- Browser.targets defaulted `InvariantGlobalization=true`, but the workload's `WasmFeatures.props` (imported from Microsoft.NET.Sdk props) had already set it to false.
- So every Web export ran with ICU and shipped icudt.dat.
- The default now goes in Sdk.props before the Microsoft.NET.Sdk import (browser RIDs only).
- WebRuntimeTest sets the property explicitly, so it was unaffected.

**`f99803dd56` GodotTrimGodotSharp (opt-in).** Browser.targets roots GodotSharp (as iOS does upstream), so all 5.9 MB was exported.
- With `GodotTrimGodotSharp=true` the trimmer keeps the classes (the `Constructors` map references all of them) but removes unused members: GodotSharp.dll 5.87 MB -> 2.66 MB.
- Passes WebRuntimeTest (ICU release 43/0, threads invariant debug 39/0), HelloWebVisual debug/release and WebBench.
- **Why it stays opt-in:** GodotSharp isn't trim-annotated (IL2104). User code that reflects on Godot types can break.
- **Recommendation:** make it the default once more real projects have been tested.

**`0e25a79588` `dotnet_web_icu` SCons option (web only, default yes = no change).** With `no`:
- ICU and libSystem.Globalization.Native are not linked;
- driver.c gets `INVARIANT_GLOBALIZATION`;
- the P/Invoke table's 33 `GlobalizationNative_*` entries point to stubs generated from `pinvoke-table.h` (`bin/obj/modules/mono/globalization_native_stubs.<Release|Release-threads>.gen.c`, exact signatures, return 0).
- **Tests:**
  - WebRuntimeTest invariant: 39/0.
  - An ICU export (InvariantGlobalization=false) fails at startup with a clear message: `ERROR: .NET: This export template was built without ICU ('dotnet_web_icu=no') ... Set 'InvariantGlobalization' to true ...`, then Mono's "Unable to load required ICU Globalization data" abort.
- **Open decision:** official templates either ship ICU (current default) or ship without ICU and require custom templates for culture-sensitive projects. Shipping without ICU saves 0.4 MB brotli for every game.

**`67806bd96f` Run the trimmer again when its roots change.**
- `_RunILLink` is incremental on input files only. Toggling GodotTrimGodotSharp exported the stale trimmed or untrimmed GodotSharp (seen in HelloWebVisual).
- The fix adds a roots file, rewritten only when the roots change, to the target's inputs. Verified by toggling both ways, debug and release.

## 4. Size table

**Wasm** (bytes; gzip -9, brotli q11). Default templates are built from `0e25a79588`. They are byte-identical in size to the pre-7c templates, because the commits don't change the default build.

| Template | raw | gzip | brotli |
|---|---|---|---|
| debug threads=no, before 7c / default after | 34,214,019 | 10,251,139 | 7,480,240 |
| debug threads=no, `dotnet_web_icu=no` | 32,890,046 | 9,719,469 | 7,081,939 |
| release threads=no, before 7c / default after | 33,743,479 | 10,309,668 | 7,487,036 |
| release threads=no, `dotnet_web_icu=no` | 32,428,229 | 9,743,258 | 7,061,731 |
| debug threads=yes (default) | 34,591,308 | 10,341,177 | 7,523,977 |
| release threads=yes (default) | 33,570,805 | 10,340,648 | 7,491,669 |

**PCK** (HelloWebVisual; debug and release PCKs differ by < 1 KB):

| State | raw | gzip | brotli |
|---|---|---|---|
| before 7c (7b default: ICU on, icudt.dat in PCK) | 9,315,984 | 2,766,804 | 2,061,692 |
| after `d990e1edf9` (default, invariant) | 7,698,548 | 2,266,259 | 1,689,924 |
| + `GodotTrimGodotSharp=true` (`f99803dd56`) | 3,946,460 | 1,157,432 | 872,246 |
| release, default / trimmed | 7,698,036 / 3,945,948 | 2,266,099 / 1,157,330 | 1,689,086 / 872,235 |

WebRuntimeTest PCK (ICU on, explicit):
- debug: 9,457,256 raw / 2,815,056 gzip;
- release trimmed: 5,815,144 / 1,741,643.

**Total startup download, HelloWebVisual release, brotli** (wasm + pck):
- before: 9.55 MB;
- default after: 9.18 MB;
- trimmed: 8.36 MB;
- trimmed + `dotnet_web_icu=no`: 7.93 MB (−17%).

## 5. Verification (templates from `0e25a79588`, SDK from `67806bd96f`)

| Run | Result | Log |
|---|---|---|
| WRT nothreads debug, ICU | 43/0 | step7c-run-wrt-icu-debug.log |
| WRT nothreads debug, invariant | 39/0 | step7c-run-wrt-inv-debug.log |
| WRT nothreads release, ICU | 43/0 | step7c-run-wrt-icu-release.log |
| WRT nothreads release, invariant | 39/0 | step7c-run-wrt-inv-release.log |
| WRT threads debug, ICU | 43/0 | step7c-run-wrt-threads-icu-debug.log |
| WRT nothreads release, ICU, GodotSharp trimmed | 43/0 | step7c-run-wrt-icu-release-trim.log |
| WRT threads debug, invariant, trimmed | 39/0 | step7c-run-wrt-threads-inv-debug-trim.log |
| WRT nothreads debug, invariant, `dotnet_web_icu=no` | 39/0 | step7c-wrt-noicu-inv-debug.log |
| WRT ICU on `dotnet_web_icu=no` template | clear startup error (expected) | step7c-wrt-noicu-icu-debug.log |
| HelloWebVisual debug/release, default + trimmed | "Hello from C# _Ready" | step7c-run-hwv-*.log |

**WebBench** (ms, median total; old = installed pre-7c templates, same session). No regression > 10%; the differences are within run-to-run noise (single runs range from 186 to 228 ms).

| Template | runs | median total |
|---|---|---|
| debug, before | 2 | 189.8 |
| debug, after | 5 | 192.4 (+1.4%) |
| release, before | 5 | 195.6 |
| release, after | 5 | 207.1 (+5.9%) |
| release, after, trimmed | 2 | 204.2 |
| release threads, after | 1 | 197.2 |

## 6. Plan for a custom Mono runtime pack (not done: needs a dotnet/runtime rebuild)

The whole Mono part is about 3 MB raw / 1 MB gzip, so the ceiling is small.

1. **Build ICU (dotnet/icu) with `UCONFIG_NO_TRANSLITERATION`, `UCONFIG_NO_REGULAR_EXPRESSIONS`, `UCONFIG_NO_FORMATTING` and similar.**
   - .NET uses collation, casing, normalization, IDNA, locale and calendar data, but not ICU formatting, regex or transliteration.
   - Estimate: icui18n 0.51 MB -> ~0.25 MB, so **−0.25–0.4 MB raw (~−0.1 MB br)**. Only for ICU templates.
2. **Build the Mono runtime with -Oz instead of -O2, and with `ENABLE_MINIMAL` features.**
   - For example, drop reflection emit (sre.c) and perfcounters, and avoid logging strings.
   - Estimate: **−0.15–0.3 MB raw**.
   - Risk: interpreter speed (-Oz on the interpreter loop); must be checked with WebBench.
3. **Share one ICU between Godot (icu4c 78, `libicu_builtin`, 0.27 MB code + 0.36 MB data) and .NET (unversioned symbols, older ICU).**
   - This needs .NET built against the same ICU version.
   - Estimate: **−0.6 MB raw** for ICU templates.
   - Not feasible without both rebuilding dotnet/runtime and aligning versions.

**Bigger, not .NET-specific levers:**
- Godot build profiles. For C#-only games:
  - `module_gdscript_enabled=no` saves ~0.66 MB of code;
  - `disable_3d` / disabling jolt, gltf, fbx, csg and navigation saves several MB.
- Note that `production=yes` turns on `lto=thin`, which makes this wasm 0.76 MB bigger. Official mono web templates should be checked with `lto=none`.

## Templates, backups

- **Backups:**
  - scratchpad `templates-before7c/` has the four installed web zips from before 7c;
  - `bin-before7c/` has the bin zips and wasm.
- **Installed now:** all four default web zips built from `0e25a79588` (`dotnet_web_icu=yes`).
- **`dotnet_web_icu=no` zips:** scratchpad `final/{debug,release}-nothreads-noicu.zip` (not installed).
- **Nupkgs:** rebuilt from `67806bd96f` with `build_assemblies.py --push-nupkgs-local` (logs `step7c-assemblies-*.log`).
- **Scripts (scratchpad):**
  - `build7b.sh <target> <threads> [scons args]`, `buildall7c.sh`;
  - `export7c.sh`, `verify7c.sh <project> <preset> <tag> <debug|release>`;
  - `sizes.py`.
- **Build logs:** `step7c-build-*.log`.
- **Working tree:** the debug threads=no objects were last built non-LTO (the LTO experiment was rebuilt over).

## Open problems

1. **Official template policy for `dotnet_web_icu`.** Should templates ship with or without ICU, or as two sets?
2. **GodotTrimGodotSharp is opt-in.** It needs more real-project testing before it becomes the default; trimming should also eventually be applied to the editor's desktop exports.
3. **`linkflags=-O*` cannot override the platform's `-Os`,** because the order is set in SConstruct. This is a general Godot quirk.
4. **`production=yes` (`lto=thin`) enlarges the mono web template** by 0.76 MB raw.
