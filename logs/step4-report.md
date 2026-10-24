# Step 4 report: .NET web export template + HelloWeb in the browser

Result: **PASS end to end with Emscripten 6.0.12.** I did not need the 3.1.56 emsdk, so emsdk-3.1.56 was not created.
Headless Edge prints `Hello from C# _Ready` from the exported HelloWeb.

- Worktree: `godot-wt\rebase`, branch `web-dotnet-rebased`.
- New HEAD is `7bca276d6e`: 5 fix commits on top of `c81d539c9b`. Not pushed.

## Commands that worked
- **Build** (Git Bash): `logs\step4-build.sh [emsdk_dir]`. It sets up the emsdk env (EM_CONFIG and PATH, see HANDOFF Env setup) and then runs:
  `python -m SCons platform=web target=template_debug module_mono_enabled=yes threads=no -j11`
  - `threads=no` matches what the PR expects: GetRuntimePack sets `WasmEnableThreads=false`, and the prototype preset has `variant/thread_support=false`.
  - SCons runs `dotnet publish` on GetRuntimePack (browser-wasm, Release) itself.
  - Output: `bin\godot.web.template_debug.wasm32.nothreads.mono.zip` (10.6 MB). Full rebuild about 4 min; the final link takes about 15 s.
- **Install template**: copied to `%APPDATA%\Godot\export_templates\4.8.beta.mono\web_nothreads_debug.zip`, plus `version.txt`.
- **HelloWeb changes**:
  - Added `export_presets.cfg` with a "Web" preset, threads off, `dotnet/embed_build_outputs=true`.
  - Added `HelloWeb.sln`. The C# export fails without a .sln: "no solution file was found". Note that `dotnet new sln` on SDK 10 creates `.slnx`, so I used `--format sln`.
- **Export**: `bin\godot.windows.editor.x86_64.mono.console.exe --headless --path samples/HelloWeb --export-debug "Web" D:/source/repos/godot-web-dotnet/out/HelloWeb/index.html`
  - Export succeeds in about 1 min with no errors.
  - The command only returns after about 10 min. The likely cause is a leftover MSBuild/dotnet node (`dotnet.exe` pid 400540, still alive) holding the redirected stdout pipe. Try `MSBUILDDISABLENODEREUSE=1` or `dotnet build-server shutdown`.
- **Manual post-export step**: removed `DOTNET.setup({...});` from `out\HelloWeb\index.js`. The original is kept as `index.js.orig`. **Still a bug to fix later**: the exported JS should not need this edit.
- **Serve**: `python godot-wt/rebase/platform/web/serve.py -p 8060 -n --root D:/source/repos/godot-web-dotnet/out/HelloWeb`
  - Running in the background, log in `logs\step4-serve.log`.
  - Responses carry COOP `same-origin` and COEP `require-corp`.
  - URL: http://localhost:8060/index.html
- **Browser check**: `--enable-logging=stderr` in headless Edge on Windows printed nothing. I used CDP instead:
  - Edge started with `--headless=new --remote-debugging-port=9333 --user-data-dir=<scratch>`.
  - The node script `scratchpad\cdp.mjs <url> 40` collects `Runtime.consoleAPICalled`. Output is in `logs\step4-browser.log`:
    ```
    Godot Engine v4.8.beta.mono.custom_build.c81d539c9 ...
    OpenGL API OpenGL ES 3.0 (WebGL 2.0 ...) - Compatibility
    Build configuration: Emscripten 6.0.12, single-threaded, no GDExtension support.
    Hello from C# _Ready
    ```
  - The template's version hash shows c81d539c9 because I built it before committing. Its contents match HEAD 7bca276d6e.

## Fixes (one commit each)
1. **`30ac97514e`: GetRuntimePack needs `OutputType=Exe`.**
   - The WebAssembly SDK sets `IsBrowserWasmProject` only when the project is not a Library, and that is the case in 9.0.21 as well as 10.0.8.
   - Without it, the native relink is skipped and `obj/.../wasm/for-publish` (`pinvoke-table.h`, `wasm_m2n_invoke.g.h`, ...) never exists. The build then fails with `'wasm_m2n_invoke.g.h' file not found`.
   - This was a latent bug in the original PR.
2. **`15cc5dd4df`: link `libmono-profiler-browser.a`.**
   - In .NET 10 the diagnostics_tracing component references `mono_profiler_{init,fini}_browser_eventpipe`. The .NET 9 component did not.
   - The SDK's own `emcc-link.rsp` also links this library.
3. **`fb8f2593f8`: new file `modules/mono/runtime/web/wasm_eh_compat.c`.**
   - Objects built by Emscripten 3.1.56 (`libmono-wasm-eh-wasm.a`) call `_Unwind_CallPersonality`, which no longer exists in newer libunwind. It became `__gxx_wasm_personality_v0` in libc++abi, with the same semantics.
   - The new file forwards the old name to the new one. It is compiled only when `__EMSCRIPTEN_MAJOR__ >= 5`.
   - This was the only Emscripten-mismatch link error.
4. **`d66639c5b1`: web `detect.py` links through a response file (TEMPFILE), like ARCOM already does.**
   - The link command was "too long" for cmd.exe on Windows once the Mono libraries were added.
   - It also sets `TEMPFILEARGESCFUNC` to use forward slashes. Emscripten reads rsp files with POSIX rules, so backslashes were being eaten (`C:UsersKyle...`).
5. **`7bca276d6e`: `mono_wasm_load_runtime` in .NET 10 takes `(debug_level, count, keys, values)`.**
   - The PR declared and called the .NET 9 one-argument form. That signature mismatch produced a wasm that wasm-opt rejected: "popping from empty stack" in `GDMono::initialize`.
   - The call now passes the same properties as the pack's `initialize_runtime()`: `APP_CONTEXT_BASE_DIRECTORY=/` and `RUNTIME_IDENTIFIER=browser-wasm`.

## Open problems / follow-ups
- **`DOTNET.setup` edit**: the manual edit to index.js is still required (see Commands).
- **Emscripten ABI risk**: the 3.1.56 runtime objects are mixed with 6.0.12 objects. It links, and the hello path runs. Not exercised yet:
  - managed exceptions crossing into native code
  - SIMD paths
  - the timezone and ICU paths
- **Shim and threads**: `wasm_eh_compat.c` relies on the old objects' global `__wasm_lpad_context` being the same object that `_Unwind_GetWasmLPadContext()` returns. That holds without threads; in a `threads=yes` build the new libunwind makes it TLS, so it may not hold. I did not verify the exact Emscripten version where `_Unwind_CallPersonality` was removed (somewhere in 5.0.0 to 6.0.3).
- **Large .pck**: the pck is 41 MB. It embeds `.godot/mono/publish/wasm32/*`, including native `.a` archives, `wasm-props.json` and similar files that are useless at runtime. The ExportPlugin file filter for Web should drop these.
- **Full runtime components**: the template links the full debugger, diagnostics_tracing and hot_reload components, while the .NET SDK default for browser uses the stubs. Using the stubs would shrink the binary and avoid needing the profiler library.
- **Unchanged rebase risks**: risks 5 to 8 from rebase-report.md are not exercised by the hello sample (`mono_runtime_invoke` worked for `InitializeFromGameProject`, so risk 5 is OK here).
- **Untested**: a release template and a `threads=yes` template were not built or tried.
- **Edge cleanup**: while cleaning up a headless Edge I once ran `taskkill /IM msedge.exe /FI "WINDOWTITLE eq about:blank"`. It only matches windows titled about:blank, but if a normal Edge window closed around 21:45, that was the cause.
