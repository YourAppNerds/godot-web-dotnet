# PR #106125 rebase report

- Original: `aa1f5ff` "[.NET] Add C# web export support" (Raul Santos), merge-base `1cf573f` (2025-05-02)
- New base: `upstream/master` @ `06d1e7a34c`
- Result: `c81d539c9b8ec6a76aac4d20b738d137e649cc46` on branch `web-dotnet-rebased`
- Worktree: `D:\source\repos\godot-web-dotnet\godot-wt\rebase` (not pushed)
- Author kept as Raul Santos, original author date kept.

## Conflicts

### 1. `modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Godot.NET.Sdk.csproj`
- **PR:** packs the new `Sdk\Android.targets`, `Sdk\Browser.props` and `Sdk\Browser.targets`.
- **Master:** commit `1294b21c01` ("Add MSBuild property to enable Godot.NET preview") renamed `iOSNativeAOT.props/.targets` to `iOS.props/.targets` and updated the pack items.
- **Resolution:** kept master's `iOS.*` entries and added the PR's three new entries around them. The PR never touched the iOS lines; it only appeared to because the hunks were next to each other.

### 2. `.../Sdk/Sdk.props`
- **PR:** adds a "Platform-specific build properties" comment and imports `Browser.props` when `GodotTargetPlatform == web`.
- **Master:** same rename (`iOSNativeAOT.props` became `iOS.props`, in `1294b21c01`). Master already had the PR's comment line.
- **Resolution:** kept master's `iOS.props` import and added the `Browser.props` import.

### 3. `.../Sdk/Sdk.targets`
- **PR:** changes the "iOS-specific" comment to "Platform-specific" and adds imports for `Android.targets` (android) and `Browser.targets` (web).
- **Master:** same rename (`iOS.targets`). It also added the GodotSharp / Godot .NET binding mismatch checks above this block, which merged cleanly.
- **Resolution:** kept the PR's comment and its Android and Browser imports, using master's `iOS.targets` name.

### 4. `modules/mono/mono_gd/gd_mono.cpp`
There were two conflicting hunks. Both were preprocessor conditions in `GDMono::initialize()`.
- **PR:** adds `&& !defined(WEB_ENABLED)` to (a) the assemblies-directory check and (b) the `dlopen(nullptr)` handle on UNIX.
- **Master:** commit `457299449d` ("Introduce 'drivers/apple_embedded' abstract platform") replaced `IOS_ENABLED` with `APPLE_EMBEDDED_ENABLED` in both conditions.
- **Resolution:**
  - (a) became `!defined(APPLE_EMBEDDED_ENABLED) && !defined(WEB_ENABLED)`
  - (b) became `defined(UNIX_ENABLED) && !defined(MACOS_ENABLED) && !defined(APPLE_EMBEDDED_ENABLED) && !defined(WEB_ENABLED)`

  This keeps master's broader Apple condition, which now also covers visionOS, and keeps the PR's web exclusion.
- **Auto-merged regions I checked by hand:**
  - Master's new `try_get_dotnet_root_from_command_line` (commit `5a2c033914`, hostfxr resolved through the dotnet CLI) is TOOLS-only. It sits inside the PR's large `#ifndef WEB_ENABLED` block, which is correct.
  - The `#if/#endif` nesting and the braces of the restructured `load_hostfxr` / `if (!runtime_initialized)` block match up.
  - Master's .NET SDK 10 alert text is preserved.
  - `hostfxr_dll_handle` / `coreclr_dll_handle` cleanup in the destructor uses only members that still exist.
  - `Path::get_csharp_project_name()` still exists.
  - `GodotPlugins.Game.Main.InitializeFromGameProject` (4 args) still exists in `GodotPluginsInitializerGenerator.cs`.

## Semantic fixes beyond the textual conflicts
These are needed because master changed things the PR depends on without touching the same lines.

1. **`Sdk/Browser.targets`: TrimmerRootAssembly.** Master's `iOS.targets` now roots `GodotSharp` only when `EnableGodotDotNetPreview != true` and `DisableImplicitGodotSharpReferences != true`, and roots `Godot.Bindings` when preview is on (`1294b21c01`). Browser.targets rooted `GodotSharp` with no condition. I gave it the same conditions so web behaves like iOS. Without this, preview-bindings projects would root an assembly that is not referenced.
2. **`runtime/GetRuntimePack/GetRuntimePack.csproj`: `net9.0` changed to `net10.0`.**
   - Master now requires .NET 10: `GodotSharp.csproj` targets `net10.0`, `ProjectGenerator.GodotMinimumRequiredTfm` is `net10.0`, and the SDK alert says 10 (`a9618f8362`).
   - The Mono WASM runtime is linked statically into the export template. It must match the BCL (`System.Private.CoreLib`) that the game publishes. Game projects are now net10, so a net9 runtime pack would be mismatched.
   - `mono_configure.py` had `net9.0` hardcoded in the `obj/Release/<tfm>/browser-wasm/wasm/for-publish` include path. That path now comes from one `get_runtime_pack_tfm = "net10.0"` variable, with a comment tying it to the csproj.
3. **`runtime/GetRuntimePack/ManagedCallbacks.cs`: stub sync.**
   - This file mirrors every `[UnmanagedCallersOnly]` method in GodotSharp. The WASM build generates its native-to-managed wrapper tables from it, and the C++ side calls those wrappers through `GDMonoCache::ManagedCallbacks`.
   - In WASM, calling through a function pointer whose signature does not match traps at runtime.
   - I compared the stub with GodotSharp's methods using a script (method name, return type, parameter types):
     - **Added 6 missing methods:** `ScriptManagerBridge.RaiseEventSignalViaTrampoline`, `UpdateScriptTrampolines`, `CallStaticWithTrampoline`, and `CSharpInstanceBridge.CallViaTrampoline`, `SetViaTrampoline`, `GetViaTrampoline`. Master added these in its trampoline work, which also extended `gd_mono_cache.h`.
     - **Fixed `GetGlobalClassName`:** it has 6 parameters (`..., godot_bool* outIsAbstract, godot_bool* outIsTool, godot_string* outClassName`). The stub had 4. The stub was **already wrong at the PR base**: base `1cf573f` already had 6 parameters, so this was a latent bug in the original PR.
     - Parameters that are managed function pointers (`MethodTrampolineDelegate`, which is `delegate* managed<...>`, and similar) are declared as `void*` in the stub. Both are pointer-sized (i32 on wasm32), and this avoids stubbing `NativeVariantPtrArgs`.
     - The `TryAdd*TrampolineDelegate` aliases are written out inline as `delegate* unmanaged<...>`.
   - After the change, all 43 methods match by name and parameter count. The only type differences left are the intentional `void*` / inline-alias substitutions.

## Auto-merged files reviewed
- `platform/web/detect.py`: `"supported": ["mono"]` merged into `get_flags()`. `modules/mono/config.py` still gates on `env["supported"]`. `py_compile` passes.
- `platform/web/export/export_plugin.cpp`: the hard "not supported" block was replaced by an "experimental" warning, and it merged cleanly into the current `has_valid_export_configuration`. No other web/C# blocks exist upstream (grepped).
- `GodotTools/Export/ExportPlugin.cs`:
  - The platform whitelist removal, the `wasm32` arch, `embedBuildResults` for Web, and the file-extension filter all merged.
  - The filter runs before master's Android `.a` exclusion (`de392ca338`), which is fine.
  - `OS.Platforms.Web` and the `TryDeterminePlatformFromOSName` helper still exist.
- `build_scripts/mono_configure.py`:
  - `env.modules_sources`, `disable_warnings`, `add_source_files` and `AddJSLibraries` all still exist.
  - `py_compile` passes.
  - Master's web `SCsub` now adds `-fwasm-exceptions` to dlink builds. The PR adds it to `env` for all mono web builds. The flag is duplicated in dlink builds, which is harmless.
- `Android.props` / `Android.targets`: the PR moves `UseMonoRuntime` from props to targets. Master had not changed `Android.props` since the base.

## Risks (need a build or run to confirm)
1. **.NET 10 Mono runtime pack layout.** The PR hardcodes file names from the .NET 9 pack: `libmono-wasm-eh-wasm.a`, `libmono-wasm-simd.a`, `libmono-icall-table.a`, `wasm-bundled-timezones.a`, the `libmono-component-*-static.a` names, `src/{runtime,driver,corebindings,pinvoke}.c`, `src/es6/dotnet.es6.lib.js`, and `include/wasm`. Any rename or removal in .NET 10 breaks the scons configure or link step. This is the most likely failure.
2. **Generated sources in `obj/.../wasm/for-publish`** (`pinvoke-table.h` and others) may have changed name or format in .NET 10 compared with what `pinvoke.c` / `GEN_PINVOKE` expect.
3. **Emscripten version.** Master now uses Emscripten 6.0.x (`07be503875`, and 6.0.12+ is required for dlink+threads). The .NET 10 Mono WASM runtime's `.a` files are built with Microsoft's pinned emsdk (around 3.1.x). Linking LLVM bitcode or objects across very different emsdk versions can fail, or can break `dotnet.es6.lib.js` JS-library APIs. This was already a risk before the rebase, but the version gap is now much larger.
4. **`void*` stub substitution.** If the generator handles function-pointer types differently from `void*` when choosing a signature character, the wrapper signatures would not match. I expect both to map to `I`, but this needs confirming in the generated `pinvoke-table.h`.
5. **`mono_runtime_invoke` on an `[UnmanagedCallersOnly]` method** (`InitializeFromGameProject`). This comes from the original PR design and the rebase did not change it. Newer Mono may reject invoking UCO methods through reflection or runtime_invoke.
6. **Preview bindings (`EnableGodotDotNetPreview`).** It is unclear whether the `GodotPlugins.Game.Main` initializer and the stub's `AssemblyName=GodotSharp` hack work when the game uses `Godot.Bindings` instead of GodotSharp. I did not try to handle this.
7. **Wasm feature flags.** `WasmEnableThreads=false`, `WasmEnableExceptionHandling=true` and `WasmEnableSIMD=true` must match the Godot template variant. A `threads=yes` template plus a single-threaded Mono build is untested.
8. **Linker or BCL trimming under .NET 10.** Rooting `System.Private.CoreLib` in GetRuntimePack may behave differently.
