# Step 5b report: smaller web export (PCK filter, Mono stub components)

Result: **PASS.** The plain export of samples/HelloWeb runs in headless Edge and prints `Hello from C# _Ready`. There were no exceptions.

Two commits on `web-dotnet-rebased` in `godot-wt/rebase`, on top of `3affe161ca`. Not pushed.
- `c9790251b2` [.NET] Web: Only export managed assemblies from the publish output
- `7c04c98993` [.NET] Web: Link stub debugger, diagnostics and hot reload components

## Sizes (HelloWeb, export-debug, web_nothreads_debug template)

| File | Before (5a) | After A only | After A+B |
|---|---|---|---|
| index.pck raw | 41,410,916 | 7,695,468 | 7,695,468 |
| index.pck gzip -9 | 15,575,222 | 2,257,024 | 2,257,024 |
| index.wasm raw | 34,485,730 | (unchanged) | 34,212,357 (-273 KB) |
| index.wasm gzip -9 | 10,349,346 | (unchanged) | 10,252,158 (-97 KB) |

- Wasm code section: 81,560 functions / 28.65 MB before, 80,815 functions / 28.40 MB after.
- The wasm gain is small. Most of the 34 MB is the debug template itself plus the Mono runtime.
- A template_release build was not built or measured.

## A. What the PCK no longer contains, and why
- **What the runtime reads.** On Web (`!TOOLS_ENABLED`, `WEB_ENABLED`), the runtime reads only two things from `res://.godot/mono/publish/wasm32`:
  - `godotsharp_dirs.cpp`: `.dotnet-publish-manifest`, to check whether the cache copy is current. It then `copy_dir`s the whole folder to `OS::get_cache_path()/data_<proj>_web_wasm32`.
  - `gd_mono.cpp` `load_assembly_from_pck`, the assembly preload hook: `[<culture>/]<name>.dll`.
- **What it does not read.**
  - The hostfxr/runtimeconfig code (`GodotPlugins.runtimeconfig.json`) is inside `#ifndef WEB_ENABLED`.
  - `mono_wasm_load_runtime` gets its two properties from code, not from files.
  - ICU `.dat` files are only loaded by the .NET JS runtime, which Godot does not load. `Browser.targets` also forces InvariantGlobalization.
  - The `.a` archives are link inputs for the template. They can't be loaded at runtime (no dlopen of side modules).
- **Change.** In ExportPlugin, for `platform == Web`, only `.dll` and `.pdb` files are packed. A new helper `IsWebRuntimeFile` does this. The manifest is still generated, and it lists only the packed files.
  - `.pdb` is kept on purpose. With `debug_level=1`, Mono may use symbols for stack traces, so I can't prove it is unused.
- **Removed from the HelloWeb PCK:**
  - all `.a` files (29.5 MB): libmonosgen, ICU, brotli, components, profilers, timezones, and so on
  - `icudt*.dat` (4.1 MB)
  - `wasm-props.json`, `package.json`, `HelloWeb.deps.json`, `HelloWeb.runtimeconfig.json`
- The file count went from 63 to 26. First-run extraction to the cache is also much smaller.

## B. Component choice
- All three components are now stubs, in both debug and release templates: `debugger`, `diagnostics_tracing` and `hot_reload`. `marshal-ilgen` stays full, because it is needed for non-blittable P/Invoke and delegate marshalling.
- **Why debug templates get stubs too.** The real components are dead code under Godot's loader:
  - Debugger: it talks to the browser debug proxy through `mono_wasm_fire_debugger_agent_message_with_data[_to_pause]`.
  - Diagnostics: it uses the diagnostic server over `ds_rt_websocket_*`.
  - Hot reload: it needs deltas pushed in by dotnet-watch through the JS runtime.
  - All of these are JS imports implemented by `dotnet.runtime.js`, which Godot never loads. In our build they are inert `{runtime_idx:N}` stubs from `dotnet.es6.lib.js` (see step5a issue 3).
  - So the full debugger can't be attached even in a debug template. If debugger support is ever wired up (loading the JS runtime pieces plus BrowserDebugProxy), switch back to full components for `template_debug` only.
- **Same as the .NET SDK.** The SDK's browser-wasm Release default also uses stubs.
- **Profiler library dropped.** `libmono-profiler-browser.a` is no longer linked. Only the full diagnostics_tracing component referenced `mono_profiler_init_browser_eventpipe`. It linked cleanly without it.

## Verification
- **GodotTools.** Rebuilt with `python modules/mono/build_scripts/build_assemblies.py --godot-output-dir=./bin --godot-platform=windows` (no nupkg push, the SDK is unchanged). Log: `logs/step5b-assemblies.log`.
- **Export A on the old template.**
  - Exported with the new GodotTools and the step-5a template to `out/HelloWeb-A`.
  - Browser result: `Hello from C# _Ready`. Log: `logs/step5b-browser-A.log`.
- **Template.** Ran `env -u EM_CONFIG dotnet publish modules/mono/runtime/GetRuntimePack -r browser-wasm --self-contained -c Release`, then `logs/step4-build.sh`.
  - EXIT 0 in 8m17s. Log: `logs/step5b-build.log`.
  - Installed as `%APPDATA%/Godot/export_templates/4.8.beta.mono/web_nothreads_debug.zip`.
  - The 5a zip is backed up at `C:/Users/Kyle/AppData/Local/Temp/claude/d--source-repos-godot-web-dotnet/81e226c9-7366-4a18-ac6c-74873a0c5f88/scratchpad/web_nothreads_debug.5a.zip`.
- **Final export.**
  - Command: `MSBUILDDISABLENODEREUSE=1 bin/godot.windows.editor.x86_64.mono.console.exe --headless --path ../../samples/HelloWeb --export-debug "Web" D:/source/repos/godot-web-dotnet/out/HelloWeb/index.html`
  - EXIT 0 in 9 s, with no hang (the 5a A-export also returned in about 5 s). Log: `logs/step5b-export.log`.
- **Browser.**
  - Setup: `serve.py -p 8064`, headless Edge with CDP on port 9334, `cdp.mjs`.
  - `logs/step5b-browser.log` shows the template `custom_build.c9790251b`, Emscripten 6.0.12, `Hello from C# _Ready`, and no exceptions.
  - The first 4 lines of that log are the leftover A tab.
- **Cleanup.** The servers and Edge were stopped afterwards.

## New issues
1. **`debug_level` is hardcoded to 1.** `gd_mono.cpp` calls `mono_wasm_load_runtime(1, ...)`, even in release templates. `debug_level != 0` makes runtime.c call `mono_wasm_enable_debugging` and set interp opts to `-all,simd`, which disables interpreter optimizations. It should pass 0 in release (and arguably in debug too, since the debugger can't attach). This is likely a noticeable perf win. Separate commit.
2. **runtimeconfig.json is never applied on Web.** Runtime knobs from the csproj (InvariantGlobalization, System.GC.*, feature switches) only take effect when ILLink substitutes them at trim time. Properties that aren't trimmed in are silently ignored. Consider passing the runtimeconfig properties to `mono_wasm_load_runtime`, or generating a `runtimeconfig.bin`.
3. **Extraction copies the whole publish folder.** First run copies the publish folder from the PCK to the cache path (IDBFS-backed `user://`?), though the preload hook could read the `res://` path directly, as Android does. Not needed for correctness, but it adds load time and storage.
4. **Release template not built.** A template_release was not built, so its size is not measured.
