# Step 7b report: SIMD, globalization, exceptions/time zones, Emscripten EH rename

Result: **PASS.** SIMD already works. Globalization can now be non-invariant (opt-in). Exceptions and time zones work. Exception tests found a crash class in GodotSharp's native calls on wasm (any engine error print killed the page); fixed.

Five commits on `web-dotnet-rebased` (`godot-wt/rebase`), on top of `9c83382ba5`. Not pushed.

| Commit | Title |
|---|---|
| `02293c7363` | [.NET] Web: Add GodotSharp's native call signatures to the runtime's m2n table |
| `9098b9808b` | [.NET] Web: Return Error as long from native functions |
| `0ba9d16a78` | [.NET] Web: Make single-scalar interop structs single-scalar in C |
| `2755e27e3c` | [.NET] Web: Only enable the Wasm EH shim from Emscripten 6.0.6 |
| `d306e4e5fa` | [.NET] Web: Support non-invariant globalization |

## WebRuntimeTest

- New file `samples/WebRuntimeTest/Main.Checks7b.cs` (SIMD, globalization, native-call, exception, time zone checks); `Main.cs` calls `RunStep7bChecks()`.
- `WebRuntimeTest.csproj` now sets `InvariantGlobalization=false` + `WasmIncludeFullIcuData=true`.
  - Env `GODOT_WRT_INVARIANT=1` at export: invariant mode.
  - Env `GODOT_WRT_SHARDS=1`: `WasmIcuDataFileName=icudt_EFIGS.dat`.
- In invariant mode the 4 culture checks report INFO (what users get) instead of FAIL.

| Run (template from HEAD) | Result | Log |
|---|---|---|
| nothreads debug, ICU full | 43/0 | step7b-wrt-icu-debug.log |
| nothreads debug, invariant | 39/0 | step7b-wrt-inv-debug.log |
| nothreads debug, EFIGS only | 40/3 (expected: sv, tr, ja/ar not in EFIGS) | step7b-wrt-shards-debug.log |
| threads debug, ICU full | 43/0 | step7b-wrt-threads-icu-debug.log |
| threads debug, invariant | 39/0 | step7b-wrt-threads-inv-debug.log |
| nothreads release, ICU full | 43/0 | step7b-wrt-icu-release.log |
| threads release, ICU full | 43/0 | step7b-wrt-threads-icu-release.log |

- Baseline before fixes (installed 7a template): crashed after 32 lines, `RuntimeError: function signature mismatch` (step7b-wrt-base-debug.log).
- All 15 old checks still pass.

## Task 1: SIMD

**Result: works, nothing to fix.**
- `Vector128.IsHardwareAccelerated=True`, `Vector.IsHardwareAccelerated=True`, `Vector<int>.Count=4`, `PackedSimd.IsSupported=True`. `Vector64` and `Vector256` are false (expected on wasm).
- These checks pass:
  - Vector128 float and int arithmetic, shuffle, ExtractMostSignificantBits, Sqrt, Dot, Sum;
  - a `Vector<T>` loop;
  - PackedSimd intrinsics;
  - vectorized BCL (span IndexOf/SequenceEqual/Count, string IndexOf, base64);
  - System.Numerics Matrix4x4/Vector3/Vector4;
  - Godot Basis math.
- **Build flags:**
  - Godot `platform/web/detect.py` adds `-msimd128` (`wasm_simd`, default True).
  - `mono_configure.py` links `libmono-wasm-simd.a` (not `-nosimd`).
  - Browser.targets and GetRuntimePack set `WasmEnableSIMD=true`, and `wasm-props.json` defaults to true.
  - The pack's interpreter SIMD is used (`interp_opts` keeps simd).
- **Open, minor:** a template built with `wasm_simd=no` still links `libmono-wasm-simd.a`, so it would still need browser SIMD. It should link `libmono-wasm-nosimd.a` when `wasm_simd` is off. Not changed.

## Task 2: Globalization

**What users got before:** invariant mode always (Browser.targets forced it).
- `new CultureInfo("de-DE")` throws `CultureNotFoundException: Argument_CultureNotSupportedInInvariantMode Arg_ParamName_Name, name`. Messages are resource keys; see open problems.
- `CultureInfo.CurrentCulture` is invariant (`Name=""`).
- `ToUpper` / Compare are invariant.

**Implemented (`d306e4e5fa`), opt-in with `<InvariantGlobalization>false</InvariantGlobalization>`:**
- **ICU was already statically linked in the template.**
  - `driver.c` is compiled without `INVARIANT_GLOBALIZATION`, so `mono_wasm_link_icu_shim` pulls in `libicuuc/i18n`.
  - `mono_wasm_load_icu_data` / `udata_setCommonData` are in the wasm.
  - So **no wasm size cost** (+1.4 KB for the JS function).
- Browser.targets only defaults InvariantGlobalization, and keeps exactly one ICU file in the publish output:
  - `icudt.dat` by default;
  - or the file named by `WasmIcuDataFileName`.
  - The runtime pack always publishes all four. The previous 5b filter dropped them from the PCK.
- ExportPlugin's web filter keeps `icudt*.dat`.
- `gd_mono.cpp`:
  - `load_icu_data()` finds `icudt*.dat` in the PCK's assemblies dir and copies it to a 16-byte aligned, never-freed buffer.
  - It then calls `mono_wasm_load_icu_data` before `mono_wasm_load_runtime`.
- **Second failure after data loaded:** every culture lookup (even `CultureInfo.CurrentCulture`, DateTime, string.Format) threw `Exception: Locale info requires the .NET JavaScript runtime...`.
  - CoreLib's browser CultureData calls the JS import `mono_wasm_get_locale_info` (`Interop/JsGlobalization::GetLocaleInfo`) for language and region display names.
  - Implemented in `library_godot_dotnet.js`, ported from dotnet/runtime release/10.0 `src/mono/browser/runtime/globalization-locale.ts` (Intl.DisplayNames).
- **Current culture:** comes from the browser, because Emscripten sets `LANG=<navigator.language>.UTF-8` (en-US in the test).

**Results:**
- de-DE: `N2="1.234.567,89"`, `D="Samstag, 9. März 2024"`, `C="12,50 €"`, and it parses.
- sv-SE: ä sorts after z.
- tr-TR: `"istanbul".ToUpper(tr)="İSTANBUL"`.
- ja-JP: month "1月".
- ar-SA: RTL, with UmAlQuraCalendar.

**Size (WebRuntimeTest debug PCK):**

| Mode | PCK raw | PCK gzip -9 | Delta (gzip) |
|---|---|---|---|
| invariant | 7.84 MB | 2.31 MB | — |
| ICU full (icudt.dat) | 9.46 MB | 2.80 MB | +0.50 MB |
| ICU EFIGS | 8.48 MB | 2.54 MB | +0.24 MB |
| all 4 shards (first attempt, rejected) | 10.55 MB | — | — |

- **Shard design:** the .NET loader downloads one shard for the browser's locale on demand. A PCK has to carry every shard it might use, so all shards (2.6 MB) cost more than the full data (1.5 MB).
  - Default is therefore the full data, or one explicit shard.
  - Gotcha: cultures outside the shard silently fall back to root data (no exception). For example, ja month = "January", and tr casing is invariant.
- **HybridGlobalization:** removed for browser in .NET 10. The 10.0.8 WebAssembly SDK targets have no HybridGlobalization; only 9.0.21 does. It also needs `dotnet.runtime.js` (Intl-based managed paths). Not applicable.
- **Windows time zone IDs:** `FindSystemTimeZoneById("Eastern Standard Time")` throws `TimeZoneNotFoundException: TimeZoneNotFound_MissingData` in both modes. IANA IDs work. Documented, not fixed.

## Task 3: Exceptions and time zones

**Results:**
- These all pass:
  - nested try/catch/finally ordering;
  - filters (two-pass order f1,f2,finally,caught);
  - rethrow (6 `Recurse` frames);
  - runtime exceptions (NRE, DivideByZero, InvalidCast, Overflow, IndexOutOfRange);
  - AggregateException.
- **Across the Godot boundary:**
  - An exception thrown in a signal handler is logged by Godot. EmitSignal returns Ok, the other handler still runs, and the `finally` runs.
  - The same holds for Callable.Call, Object.Call, and CallDeferred: the error is logged and the next deferred call runs.
- **Stack trace text:** `at Main.Recurse(Int32 n)`, with no file or line numbers. `StackFrame.GetFileName()` is null even with `dotnet/include_debug_symbols=true`, because Mono doesn't load the PDBs (debug_level 0, debugger stub).
- **Time zones:**
  - tzdata is present (`wasm-bundled-timezones.a`): GetSystemTimeZones=313.
  - ConvertTime is correct for NY summer/winter, Berlin, Tokyo, and Lord Howe (+11 DST). The NY DST gap is invalid. Local adjustment rules match JS for Jul/Jan.

**Bug found:** the first engine error print with C# code on the stack trapped and killed the page. Error:
- `RuntimeError: function signature mismatch` at `do_icall <- mono_interp_exec_method <- wasm_native_to_interp_..._DebuggingUtils_GetCurrentStackInfo <- CSharpLanguage::debug_get_current_stack_info <- ScriptServer::capture_script_backtraces <- _err_print_error`.
- That first print can come from GD.PushError/PushWarning, an uncaught exception in any Godot-invoked C# callback, or an ERR_FAIL inside an engine method called from C#.
- **Diagnosis:** a names build (temporarily dropping `-Wl,-s` and adding `--profiling-funcs`; not committed). GodotSharp `UnmanagedCallbacks` signatures were checked with a reflection tool (scratchpad `sigtool/`).
- Three separate causes:
  1. `9098b9808b`: the Mono interpreter (`interp_type_as_ptr`, mono/mini/interp/transform.c, release/10.0) treats *every* enum as pointer-sized. Functions returning the 64-bit `Error` enum were called as i32-returning. These are the only three: `godotsharp_stack_info_vector_resize`, `godotsharp_array_resize`, `godotsharp_internal_signal_awaiter_connect` (await ToSignal). Fix: declare them as `long` and cast.
  2. `02293c7363`: once (1) is fixed, these go through the m2n table. `LII` (and F, FI, LIIII, VIFFFF, VILII) weren't in the table generated from GetRuntimePack (only BCL P/Invokes), which gives `[MONO] aot-runtime-wasm.c:188` (abort). Fix: never-called `DllImport` declarations with those signatures in `GetRuntimePack/NativeFuncsSignatures.cs`. Verified both are needed: an (1)-only build aborts.
  3. `0ba9d16a78`: C `godot_string`, `godot_string_name`, `godot_node_path` and `godot_rid` were `uint8_t[N]` wrappers. Wasm C ABI returns them via sret, but Mono returns the single-field C# structs directly. So `Variant.AsString()` on non-strings, `AsStringName`, `AsNodePath` and `AsRid` trapped. Fix: single-scalar C members (same size; native ABIs unchanged).
- Covered by new checks:
  - "engine error print";
  - "Variant -> String/StringName/NodePath/Rid";
  - Array.Resize;
  - GD.Randf / Color OK HSL;
  - Compress/Decompress;
  - await ToSignal.
- **Maintenance risk:** the signature list in (2) is hand-kept. A new NativeFunc with a signature that no BCL P/Invoke uses will abort on web. A generated check (sigtool-style, comparing against `wasm_m2n_invoke.g.h`) would be better.

## Task 4: `_Unwind_CallPersonality` rename (open failure 6)

- **LLVM commit** `1d4e04f7a7c2`, "[WebAssembly] Call EH personality function instead of `Unwind_CallPersonality`", llvm/llvm-project#209282, 2026-07-28.
  - It moved `_Unwind_CallPersonality` into libcxxabi as `__gxx_wasm_personality_v0`, and changed the personality ABI to `int personality(void*)`.
- **Emscripten commit** `d212b13bed0b`, "[EH] Apply Call EH personality fix from LLVM", emscripten-core/emscripten#27445, 2026-07-30.
  - GitHub compare: 6.0.5 is "behind" it, 6.0.6 is "ahead". So the **first release is Emscripten 6.0.6** (ChangeLog 6.0.6, 08/05/26).
- The shim was guarded by `__EMSCRIPTEN_MAJOR__ >= 5`. On 5.0.0–6.0.5, where libunwind still defines `_Unwind_CallPersonality` and `__gxx_wasm_personality_v0` doesn't exist, it would define the symbol twice and reference a missing one.
  - Godot's minimum is 6.0.1, so 6.0.1–6.0.5 were affected.
  - `2755e27e3c`: the guard is now `>= 6.0.6`, with the cites in the shim's comment.
  - Verified building on 6.0.12. Not tested on 6.0.1–6.0.5 (not installed).

## Release

- template_release nothreads and threads were rebuilt from HEAD `d306e4e5fa` (1m58s and 1m44s incremental) and installed (`web_nothreads_release.zip`, `web_release.zip`).
- WebRuntimeTest export-release, ICU full: **43/0 nothreads, 43/0 threads** (step7b-wrt-icu-release.log, step7b-wrt-threads-icu-release.log).

## Templates, backups, builds

- **Backups:**
  - scratchpad `templates-before7b/` has all four installed web zips from before 7b;
  - `bin-before7b/` has the original nothreads debug bin zip;
  - `nupkgs-before7b/`.
- **Installed now:** all four web zips (`web_nothreads_{debug,release}.zip`, `web_{debug,release}.zip`) built from HEAD `d306e4e5fa`.
- **Build command:** scratchpad `build7b.sh <target> <yes|no> [scons args]`. Export: `export7b.sh` (logs `step7b-export-*.log`).
- **Build logs:** `step7b-build-*.log`. Each incremental nothreads debug build took about 1m20s.
- GodotSharp, GodotTools and the nupkgs were rebuilt with `build_assemblies.py ... --push-nupkgs-local` (logs `step7b-assemblies-*.log`), and `~/.nuget/packages/godot.net.sdk/4.8.0-beta` was cleared.

## Open problems

1. **No file/line in C# stack traces on web.** PDBs are exported but not loaded.
2. **Exception messages show resource keys**, for example `Argument_CultureNotSupportedInInvariantMode Arg_ParamName_Name, name` and `TimeZoneNotFound_MissingData`. The trimmed BCL presumably uses `UseSystemResourceKeys`. The fix would be to set `UseSystemResourceKeys=false` for ExportDebug (size cost to be measured).
3. **Windows time zone IDs are not found**, even with ICU.
4. **m2n signature list is hand-kept** (see task 3). The editor/desktop side is unaffected.
5. **`wasm_simd=no` templates still link the SIMD Mono lib.**
6. **EFIGS and other single shards fall back silently** for cultures outside the shard.
7. **Not tested:** the EH shim guard on Emscripten 6.0.1–6.0.5.
