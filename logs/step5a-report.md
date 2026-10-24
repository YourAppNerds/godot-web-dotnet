# Step 5a report: exported index.js no longer needs the DOTNET.setup edit

Result: **PASS.** A plain export of samples/HelloWeb runs in headless Edge and prints `Hello from C# _Ready`. No manual edit is needed.

- Commit `3affe161ca` "[.NET] Web: Drop the .NET loader's DOTNET.setup() call from the engine JS", on `web-dotnet-rebased` in `godot-wt/rebase`, on top of `7bca276d6e`. Not pushed.

## Root cause
- `modules/mono/build_scripts/mono_configure.py` links the runtime pack's `src/es6/dotnet.es6.lib.js` as an Emscripten `--js-library`. Godot needs that library for its stubs of the runtime's `mono_wasm_*` / `ds_rt_websocket_*` wasm imports.
- At the end, that library's `injectDependencies()` sets `DotnetSupportLib["$DOTNET__postset"] = "DOTNET.setup({...})"`. Emscripten emits that postset into the module factory, right after `FS.staticInit()`, so it runs while the module is being instantiated.
- `DOTNET.setup()` is written for the .NET SDK's own pipeline: `dotnet.js` loads `dotnet.runtime.js`, which sets `Module.__dotnet_runtime`, and then loads `dotnet.native.js`. The setup function reads a global `require` and calls `Module.__dotnet_runtime.initializeReplacements`, `passEmscriptenInternals` and `configureEmscriptenStartup`.
- Godot loads the module with its own `engine.js` and starts Mono from native code (`GDMono::initialize`). So none of those globals exist.
- The engine therefore throws during instantiation and never starts. This was reproduced with the step-4 template (`logs/step5a-browser-before.log`):
  ```
  [exception] ReferenceError: require is not defined
      at Object.setup (http://localhost:8062/index.js:1:101178)
  ```
  If `require` existed, the next failure would be `Module.__dotnet_runtime` being undefined.

## Fix
- New file `modules/mono/runtime/web/library_godot_dotnet.js`. It is linked immediately after `dotnet.es6.lib.js`, and the order matters. It does `mergeInto(LibraryManager.library, { $DOTNET: {}, $DOTNET__postset: '' })`:
  - Emscripten's `mergeInto` allows a later library to override these keys.
  - jsifier skips a postset that is an empty string.
  - The import stubs are kept.
- `mono_configure.py`: `AddJSLibraries` now adds both files in order.
- The fix is in the web template, so it needs a template rebuild. The editor is unchanged.
- Generated template `godot.js` after the fix:
  - 0 occurrences of `DOTNET.setup` and 0 of `__dotnet_runtime`.
  - The stubs are still there, for example `function _mono_wasm_schedule_timer(){return{runtime_idx:0}}`.

## Verification
- Template build: `logs/step4-build.sh`, see `logs/step5a-build.log`. EXIT 0 after 7m19s.
- Installed the new template as `%APPDATA%/Godot/export_templates/4.8.beta.mono/web_nothreads_debug.zip`. The step-4 zip is backed up in the scratchpad.
- Export: `MSBUILDDISABLENODEREUSE=1 bin/godot.windows.editor.x86_64.mono.console.exe --headless --path samples/HelloWeb --export-debug "Web" D:/source/repos/godot-web-dotnet/out/HelloWeb/index.html`
  - Log: `logs/step5a-export.log`.
  - It exited with 0 right after the export finished. There was no 10-minute hang.
- `out/HelloWeb/index.js` contains no `DOTNET.setup`. `index.js.orig` is gone, because out/HelloWeb was recreated.
- Served with `serve.py -p 8061`. Ran headless Edge with CDP (scratchpad `cdp.mjs`). `logs/step5a-browser.log`:
  ```
  Godot Engine v4.8.beta.mono.custom_build.7bca276d6 ...
  OpenGL API OpenGL ES 3.0 (WebGL 2.0 (OpenGL ES 3.0 Chromium)) - Compatibility ...
  Build configuration: Emscripten 6.0.12, single-threaded, no GDExtension support.
  Hello from C# _Ready
  ```
  There were no exceptions.

## New issues found
1. **The GetRuntimePack `dotnet publish` breaks when EM_CONFIG is set.**
   - SCons runs `dotnet publish` with the inherited environment. The SDK's bundled emcc 3.1.56 then reads our emsdk-6.0.12 `.emscripten`, whose paths use `$CFGDIR`, which 3.1.56 does not expand. The compile of `driver.c` fails with "$CFGDIR/upstream/bin\clang.exe" not found.
   - Step 4 did not hit this because the obj was already up to date. It got rebuilt this time, probably because the workspace moved from `C:\Users\Kyle\src` (see robocopy-move.log), which changed the paths in the obj file lists.
   - Workaround I used: run `env -u EM_CONFIG dotnet publish modules/mono/runtime/GetRuntimePack -r browser-wasm --self-contained -c Release` once, then run SCons, which finds the publish up to date.
   - Proper fix: in `mono_configure.py`, pass `env=` with EM_CONFIG (and EMSDK*) removed to the `subprocess.call`. This is a separate commit, not done.
2. **Export hang**: with `MSBUILDDISABLENODEREUSE=1` set, the headless export returned immediately (one run). This supports the leftover-MSBuild-node theory for the separate hang step.
3. **Runtime-pack JS imports are inert stubs.** All `mono_wasm_*` imports are still the `{runtime_idx:N}` placeholders, because the .NET runtime JS is never loaded to replace them. Examples: `mono_wasm_schedule_timer`, `mono_wasm_bind_js_import_ST`, `mono_wasm_invoke_js_function`, `mono_wasm_get_locale_info`, `mono_wasm_browser_entropy`.
   - This was already true before this fix (it is not a regression).
   - Features that depend on them will silently misbehave: timers, async/Task scheduling via the browser loop, JSImport/JSExport interop, entropy (Random/crypto), locale info.
   - This needs its own step.
