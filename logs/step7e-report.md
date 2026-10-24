# Step 7e report: RenderingServer from C# on Web, export failure, rebuilds

Engine branch `web-dotnet-rebased` (godot-wt/rebase), 3 new commits on top of 67806bd96f. Not pushed.

## Open failure 16: root cause

The hypothesis (different hash) was wrong. The hash is the same. The binds were never registered at all:

```cpp
// servers/rendering/rendering_server.cpp, RenderingServer::_bind_methods()
#ifdef RD_ENABLED
	ClassDB::bind_method(D_METHOD("get_video_adapter_type"), ...);
	ClassDB::bind_method(D_METHOD("get_video_adapter_api_version"), ...);
#endif
...
#ifdef RD_ENABLED
	ClassDB::bind_method(D_METHOD("get_rendering_device"), ...);
	ClassDB::bind_method(D_METHOD("create_local_rendering_device"), ...);
#endif
```

`platform/web/detect.py` sets `rendering_device=False`, so `RD_ENABLED` is not defined on Web and these 4
methods do not exist there. The guards came from upstream 3d195b099e ("Allow to disable RenderingDevice
and/or its renderers while compiling", Feb 2025). `get_video_adapter_api_version` never needed RD at all.

The C# glue is generated from the desktop editor (which has RD). Every generated class looks up all of its
method binds in static field initializers, and `ClassDB_get_method_with_compatibility` threw on a missing one.
So one missing method made `RenderingServer..cctor()` fail and the whole class was unusable
(TypeInitializationException on every later use).

**Upstream master has the same bug.** `upstream/master` (c8256a8ece) still has both `#ifdef RD_ENABLED` blocks,
and the C# lookup still throws. It hits any C# build without RenderingDevice (`rendering_device=no` custom
desktop builds) and any build with features disabled that remove binds.

### Other affected classes (static scan of `_bind_methods` under `#if`)

- Default Web build: only the 4 RenderingServer methods above. Texture2DRD / TextureLayeredRD / Texture3DRD and
  RenderingDevice + RD* classes are not registered at all without RD (whole class absent, expected).
  TOOLS_ENABLED binds are `_`-prefixed and skipped by the C# generator; DEBUG_ENABLED ones are in debugger-only
  classes (desktop release templates already work).
- Custom builds: `disable_3d` (388 binds, e.g. Viewport), `disable_physics_2d/3d` (Viewport, CSGShape3D,
  TileMapLayer, Skeleton3D), `disable_navigation_*`, `disable_xr` (Viewport, RenderingServer.viewport_set_use_xr),
  `disable_advanced_gui` (LineEdit.get_menu), `deprecated=no` (505). All of these used to kill the whole C# class.

## Commits (engine)

| Hash | Subject |
|---|---|
| 971ca635e8 | Bind RenderingServer adapter and RenderingDevice getters in every build |
| 63caa36557 | [.NET] Do not fail a whole class when one native method bind is missing |
| 11875322e9 | Fail the export when an export plugin reports an error |

- 971ca635e8: the 4 methods are always bound. Without RD they behave like the Compatibility renderer:
  type = DEVICE_TYPE_OTHER, api version = the GL string, get_rendering_device / create_local_rendering_device = null.
  Return types are unchanged, so hashes match RD builds (get_video_adapter_type = 3099547011). Upstream candidate.
- 63caa36557: general C# fix. The lookup returns IntPtr.Zero instead of throwing; every generated icall starts
  with `if (method == IntPtr.Zero) ExceptionUtils.ThrowMethodBindNotFound();` (out of line throw), so only the
  missing method throws NativeMethodBindNotFoundException when called. Same model as godot-cpp. Upstream candidate.
  Not exercised at runtime by a missing method (none remain on default Web); the check runs on every call in all
  tests below.
- 11875322e9 (open failure 19): plugins can only report errors as messages, which export_project_files ignored.
  It now returns FAILED when an error message exists after the plugins' begin step or after plugins processed a
  file. Generic, also covers "Failed to build project". Verified: HelloWeb copy without .sln -> `Save PCK: Failed
  to export project files`, `Project export for preset "Web" failed.`, exit code 1 (was 0). A 112-byte stub
  index.pck is left behind, no html/wasm. A real MSBuild compile error also showed exit 1 with "Failed to build
  project" during this step. Upstream candidate (same behavior on master; it changes the result of any
  export where a plugin adds an ERROR message, which the editor log already labels as failed).

## Rebuilds (open failure 18)

- Editor: `bin/godot.windows.editor.x86_64.mono(.console).exe` built from **11875322e9** (`--version` =
  4.8.beta.mono.custom_build.11875322e). Log `logs/step7e-editor-build.log`.
- Glue regenerated (`logs/step7e-glue.log`), GodotSharp/GodotSharpEditor/Godot.SourceGenerators nupkgs
  4.8.0-beta rebuilt and pushed to `nupkgs/` (`logs/step7e-assemblies.log`). Godot.NET.Sdk nupkg was not
  re-packed (no Sdk changes, file still Oct 23).
- Web templates (default options, threads/nothreads x debug/release) built from **11875322e9**
  (logs `logs/step7e-build-*.log`), then installed in one step (copy to `*.zip.new7e`, then rename) to
  `%APPDATA%/Godot/export_templates/4.8.beta.mono/`. Runtime banner: v4.8.beta.mono.custom_build.11875322e.
- In the same step removed `~/.nuget/packages/{godotsharp,godot.net.sdk,godot.sourcegenerators,godotsharpeditor}/4.8.0-beta`.

## Verification (Edge headless, nothreads, installed templates)

| Run | Result |
|---|---|
| WebRuntimeTest debug, invariant | WRT DONE pass=43 fail=0 (39 prior + 4 new) |
| WebRuntimeTest release, invariant | WRT DONE pass=43 fail=0 |
| HelloWebVisual debug | "Hello from C# _Ready" |
| WebBench release | BENCH DONE total=196.4 ms (single run; 7c range 180-197) |
| Export without .sln | fails, exit 1 |

New checks in `samples/WebRuntimeTest/Main.Checks7e.cs` (workspace repo, not committed):
- adapter info: name='WebKit WebGL' vendor='WebKit' type=Other api='OpenGL ES 3.0 (WebGL 2.0 ...)' driver=opengl3
- GetRenderingDevice / CreateLocalRenderingDevice return null
- MultiMesh via RenderingServer (16 instances, transforms + colors, buffer 256 floats)
- mesh surface from arrays, canvas item rect/texture rect, Texture2DCreate/Texture2DGet roundtrip

Run logs: `logs/step7e-run-*.log`, `logs/step7e-export-*.log`.

## Still open

- Threads templates were built and installed but not run in this step.
- games/boomstick can now drop its "avoid RenderingServer on web" workaround (ebf3a78); not touched here.
- Sample changes (Main.cs, Main.Checks7e.cs) in the workspace repo are uncommitted.
- 63caa36557 changes C# semantics (failure moves from type init to call time); upstream may want a warning at
  lookup time instead. Kept quiet on purpose to avoid startup noise in custom builds.
