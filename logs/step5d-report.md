# Step 5d report: implementing the .NET runtime's JS imports

Result: **PASS.**
- `samples/WebRuntimeTest` went from 8 PASS / 7 FAIL to 15 PASS / 0 FAIL in headless Edge.
- HelloWebVisual still prints `Hello from C# _Ready`.

Five commits on `web-dotnet-rebased` in `godot-wt/rebase`, on top of `6c7a5edb7f`. Not pushed.

| Commit | Title |
|---|---|
| `a13719a549` | [.NET] Web: Run .NET timers and thread pool jobs |
| `a23bc4c935` | [.NET] Web: Provide secure random bytes to the runtime |
| `320f58d202` | [.NET] Web: Set TZ from the browser's time zone |
| `cb57bdf2ff` | [.NET] Web: Print Mono runtime log messages |
| `0207be28d0` | [.NET] Web: Fail clearly when using JavaScript interop |

Files changed:
- `modules/mono/runtime/web/library_godot_dotnet.js`
- `modules/mono/mono_gd/gd_mono.cpp` (the TZ commit only)

## How the stubs behaved
- `dotnet.es6.lib.js` turns every runtime import into `function(){return{runtime_idx:N}}`.
  - `dotnet.runtime.js` (`exports-binding.ts`, `replace_linker_placeholders`) swaps these for the real functions when the module is instantiated. Godot never does that swap.
  - For a `void` import the stub is a no-op.
  - For an `i32` import, wasm coerces the returned object to **0**. So every "returns a status or pointer" import silently reported success or NULL.
- Runtime version: the pack is .NET 10.0.8, commit `94ea82652cdd`.
  - The `.js.map` files have no `sourcesContent`, so the TypeScript sources were read from `dotnet/dotnet@94ea826`, `src/runtime/src/mono/browser/runtime/*.ts`.

## Import inventory (single-threaded pack: 33 imports, `mono_wasm_threads_imports` is empty)

| # | Import | Native user | What the .NET runtime does | Before | Needed by Godot? | Now |
|---|---|---|---|---|---|---|
| 0 | `mono_wasm_schedule_timer` | libmonosgen (managed `TimerQueue`) | `setTimeout(mono_wasm_execute_timer, due)`, replacing the pending one | no-op: Timer, Task.Delay and timeouts never fire | **yes** | implemented |
| 1 | `mono_wasm_asm_loaded` | debugger component | notifies the debug proxy | no-op | no (debugger is a stub since 5b) | stub |
| 2 | `mono_wasm_debugger_log` | debugger | debugger log | no-op | no | stub |
| 3 | `mono_wasm_add_dbg_command_received` | debugger | debugger protocol | no-op | no | stub |
| 4 | `mono_wasm_fire_debugger_agent_message_with_data` | debugger | debugger protocol | no-op | no | stub |
| 5 | `..._with_data_to_pause` | debugger | debugger protocol | no-op | no | stub |
| 6 | `schedule_background_exec` | libmonosgen (thread pool, main-thread jobs) | `setTimeout(mono_background_exec, 0)`, coalesced | no-op: `Task.Run` and thread-pool work never run | **yes** | implemented |
| 7 | `mono_interp_tier_prepare_jiterpreter` | interp | JITs a hot trace to a wasm function | returns 0, so the trace is not jitted (interp continues) | no | stub (see gaps) |
| 8-12 | `mono_interp_record_interp_entry`, `jit_wasm_entry_trampoline`, `jit_wasm_jit_call_trampoline`, `invoke_wasm_jit_call_trampoline`, `flush_jitcall_queue` | interp | jiterpreter entry and jit_call thunks | no-op or 0, so the normal path is used | no | stub |
| 13 | `mono_wasm_free_method_data` | interp | frees jiterpreter data | no-op | no | stub |
| 14-15 | `mono_wasm_profiler_now`, `mono_wasm_profiler_record` | browser profiler and eventpipe | `performance.now()`, devtools marks | not linked (5b) or stub | no | stub |
| 16 | `mono_wasm_trace_logger` | driver.c (Mono log handler) | `console.*` with a `[MONO]` prefix | no-op: runtime errors lost | **yes** (diagnostics) | implemented |
| 17 | `mono_wasm_set_entrypoint_breakpoint` | corebindings (debugger) | debugger | no-op | no | stub |
| 18 | `mono_wasm_browser_entropy` | minipal random.c | `crypto.getRandomValues` | returns 0 with the buffer unfilled: zero GUIDs and zero crypto bytes (**security bug**) | **yes** | implemented |
| 19 | `mono_wasm_process_current_pid` | libmonosgen | 31-bit `performance.timeOrigin` | 0 (`Environment.ProcessId` is 42 from Emscripten `getpid`) | no | stub |
| 20 | `mono_wasm_console_clear` | `ConsolePal.Clear` icall | `console.clear()` | no-op | minor | implemented |
| 21 | `mono_wasm_release_cs_owned_object` | interop | releases a JSObject handle | no-op | no | explicit no-op (finalizer-safe) |
| 22 | `mono_wasm_bind_js_import_ST` | interop | binds a [JSImport]; returns a UTF-16 error string or NULL | NULL, so binding "succeeds" | out of scope | returns an error, so a `JSException` is thrown |
| 23 | `mono_wasm_invoke_js_function` | interop | calls a bound JS function | no-op | out of scope | throws (unreachable) |
| 24 | `mono_wasm_invoke_jsimport_ST` | interop | calls a [JSImport] | no-op, bogus results | out of scope | throws (unreachable) |
| 25 | `mono_wasm_resolve_or_reject_promise` | interop | settles a JS promise | no-op | out of scope | throws (unreachable) |
| 26 | `mono_wasm_cancel_promise` | interop | cancels a promise | no-op | out of scope | throws (unreachable) |
| 27 | `mono_wasm_get_locale_info` | `JsGlobalization.GetLocaleInfo` | `Intl.DisplayNames` | NULL with an unset length | no (InvariantGlobalization is forced) | returns an error string |
| 28-32 | `ds_rt_websocket_*` | diagnostics server | WebSocket transport | no-op | no (component is a stub) | stub |

TZ is not an import:
- The loader (`loader/icu.ts`) sets the `TZ` env var from `Intl...resolvedOptions().timeZone`.
- In Godot this is now done by `gd_mono.cpp` through a new `godot_js_dotnet_time_zone_get`, before `mono_wasm_load_runtime`.

The new JS uses Godot's conventions:
- `GodotRuntime` helpers, `p_` argument names, `__sig`, and `__proxy: 'sync'` on the Godot-style getter.
- Native callbacks (`mono_wasm_execute_timer`, `mono_background_exec`) are pulled in through `__deps`, so Emscripten exports them.

## Test sample: `samples/WebRuntimeTest`
- The project setup is copied from HelloWebVisual and renamed. The sample is outside the git repo, so it is not committed.
- `Main.cs` prints `WRT PASS|FAIL|INFO <name>: <detail>` and `WRT DONE pass=N fail=M`, and mirrors the same text in the Label.
- JS reference values come from `JavaScriptBridge.Eval`.
- Async checks time out after 5 s.

| Check | Baseline (5c template) | After |
|---|---|---|
| Task.Delay(100) | FAIL (never completes) | PASS (128 ms) |
| System.Threading.Timer | FAIL (never fires) | PASS |
| Task.Yield continuation | PASS (Godot sync context) | PASS |
| Task.Run | FAIL (never runs) | PASS |
| Random.Shared | PASS (non-crypto source, differs across runs) | PASS |
| Guid.NewGuid | FAIL `00000000-0000-4000-8000-000000000000` every time | PASS, differs across runs |
| RandomNumberGenerator.GetBytes | FAIL (all zeros) | PASS, differs across runs |
| DateTime.UtcNow vs JS Date.now() | PASS (0 ms) | PASS |
| DateTime.Now offset | FAIL (0 vs -240 min) | PASS (-240) |
| TimeZoneInfo.Local | FAIL (`UTC`) | PASS (`America/New_York`) |
| FindSystemTimeZoneById | PASS | PASS |
| CultureInfo.CurrentCulture | PASS: invariant (`""`), as expected with InvariantGlobalization | same |
| exception throw/catch | PASS | PASS |
| string.Format numbers | PASS | PASS |
| Stopwatch | PASS | PASS |
| JSHost.GlobalThis (INFO) | silently returned a bogus JSObject | `JSException: JavaScript interop ... is not supported ... Use Godot's JavaScriptBridge instead.` |
| Environment.ProcessId (INFO) | 42 | 42 |
| **Total** | **8 pass / 7 fail** | **15 pass / 0 fail** |

Logs:
- `logs/step5d-browser-baseline.log`
- `logs/step5d-browser-after.log`
- `logs/step5d-browser-after-run2.log` (second run, for the cross-run Random and Guid comparison)
- `logs/step5d-browser-hellowebvisual.log`

## Build, install and verify
- **Build.** Ran `logs/step4-build.sh`, giving template_debug with threads=no: EXIT 0 in 1m35s (incremental). Log: `logs/step5d-build.log`.
  - The template was built from the working tree identical to `0207be28d0`. Its version string still shows `custom_build.6c7a5edb7`, because the commits were made after the build.
- **Install.** The previous (5c) zip is backed up at `C:/Users/Kyle/AppData/Local/Temp/claude/d--source-repos-godot-web-dotnet/81e226c9-7366-4a18-ac6c-74873a0c5f88/scratchpad/web_nothreads_debug.5c.zip`. The new zip is installed as `%APPDATA%/Godot/export_templates/4.8.beta.mono/web_nothreads_debug.zip`.
- **Export.** Used `godot.windows.editor.x86_64.mono.console.exe --headless --path samples/<X> --export-debug "Web" out/<X>/index.html`. Each returned in seconds, and the logs (`logs/step5d-export-*.log`) contain no errors.
- **Browser.** `serve.py -p 8066`, plus headless Edge with CDP on port 9336 and a separate profile, driven by `scratchpad/run5d.sh` and `cdp.mjs`. Both were stopped afterwards. Port 8070 was not touched.

## Remaining gaps
1. **Jiterpreter is half-on.** Mono's browser defaults enable jiterpreter traces, interp_entry and jit_call. Their imports return 0 or no-op, so the interpreter keeps counting hits and asking for traces that never come.
   - It works, but it is wasted work. Pass `--jiterpreter-*-enabled=false` runtime options (the way `mono_wasm_parse_runtime_options` does) or port the jiterpreter.
   - This should be measured together with the `debug_level` issue from 5b.
2. **Timer throttling.** Background tabs throttle `setTimeout`, the same as with the stock runtime. The runtime's `prevent_timer_throttling` is only used for WebSockets and was not ported.
3. **`mono_wasm_process_current_pid` still returns 0.** It is only used internally by Mono. `Environment.ProcessId` comes from Emscripten `getpid` (42).
4. **Globalization stays invariant.** Real cultures would need ICU data loading plus a real `mono_wasm_get_locale_info`.
5. **JS interop is unsupported.** It now fails clearly. [JSExport] is not exercised. Supporting interop would mean loading or porting `dotnet.runtime.js`'s marshaller.
6. **Threads builds.** The new code follows the single-threaded runtime only. A threads=yes template uses the MT runtime pack, which has a different import set (`mono_wasm_threads_imports`) and was not checked.
7. **Untested paths.** The logger's fatal path and `Console.Clear` were not exercised by the sample.
