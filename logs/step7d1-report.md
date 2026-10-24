# Step 7 D1: Boomstick, a Doom-like test game

## What was built

A new Godot 4.8 C# project, **Boomstick**, at `games/boomstick`. It is a separate git repo on
branch `main`; there is no GitHub repo and nothing was pushed. Everything is built in code and
it uses no assets.

- **Project:** `gl_compatibility` renderer, `Main.tscn` as the main scene, 1280x720 with
  `canvas_items`/`expand` stretch, and the default physics engine. The Web preset is copied
  from HelloWebVisual with `variant/thread_support=false`.
- **Player** (`src/Player.cs`):
  - A CharacterBody3D with a capsule collider and a camera at 1.55 m.
  - WASD/arrow keys to move, Shift to sprint (6 / 10 m/s), Space to jump, subtle head bob.
  - Click captures the mouse; Esc releases it.
  - On web, the click capture works because `DisplayServerWeb` flushes input events inside the
    mouse button callback (`display_server_web.cpp:315`), so the call runs inside the browser
    gesture. Key events are `preventDefault`ed, so F5 does not reload the page.
- **Level** (`src/Level.cs`, `src/ProcTex.cs`):
  - Built from a 32x23 ASCII map: a metal-walled start room, a stone hall with a pillar, a long
    corridor, a brick room with pillars, and a side room with tiles.
  - Geometry is merged into one ArrayMesh per 8x8-cell chunk, one surface per material, with
    clockwise front faces. The whole level is 20 draw calls.
  - Collision is one StaticBody3D with a shared BoxShape3D for each exposed wall cell, plus
    floor and ceiling slabs.
  - There are 7 OmniLight3D lamps (no shadows), each with an unshaded lamp tile on the ceiling.
    Ambient light is a flat color, with fog.
  - Seven 64x64 procedural textures (stone, brick, metal with hazard stripes, floor, tiles,
    ceiling, lamp) plus a 32x32 imp sprite. They use a quantized palette with
    nearest-with-mipmaps filtering.
- **Debug overlay** (`src/DebugOverlay.cs`, autoload; `src/Perf.cs`):
  - F3 toggles it; it is on by default.
  - It shows FPS, average and max frame time, C# `_Process`/`_PhysicsProcess` body time
    (Stopwatch through `Perf.Now`/`AddProcess`/`AddPhysics`), averaged engine
    `TIME_PROCESS`, node, object and draw call counts, GC counts for gen 0/1/2, and
    `GC.GetTotalMemory`.
  - `DebugOverlay.SetStat(key, value)` adds custom lines.
  - It prints a `[perf]` line every 5 seconds, and has a resettable bench accumulator
    (`ResetBench`/`BenchSummary`).
- **Stress hook** (`src/Dummy.cs`, `src/Main.cs`):
  - F4 adds 50 billboard Sprite3D dummies. They are unshaded with alpha-cut discard, and each
    has its own C# `_Process` that wanders using grid checks only, without physics.
  - F5 clears them.
  - Scripted mode: `-- --autostress[=N] [--quit-when-done]` on desktop, or
    `index.html?autostress[=N]` on web. It adds 50 dummies per step and prints one `STRESS ...`
    line per step, then `BOOMSTICK STRESS DONE`.
  - `JavaScriptBridge.Eval("window.location.search")` works from C# on web.
- `scripts/export-web.sh`:
  - Runs import, `--build-solutions` and `--export-release "Web" build/web/index.html`.
  - Fails if the log contains ERROR or if the wasm/pck files are missing.
  - Prints raw and gzip sizes.
  - The `GODOT` environment variable overrides the editor path.
- `README.md` covers what the game is, controls, the overlay and stress test, how to build (the
  YAN-kyle/godot `web-dotnet-rebased` branch plus local nupkgs), and the Doom trademark /
  unaffiliated-homage notice.

## Files (games/boomstick)

```
.gitattributes  .gitignore  Boomstick.csproj  Boomstick.sln  Main.tscn  README.md
export_presets.cfg  nuget.config  project.godot  scripts/export-web.sh
src/{DebugOverlay,Dummy,Level,Main,Perf,Player,ProcTex}.cs (+ .cs.uid)
```

## Commits (main)

```
c77055b Add solution file, web export script and README
c3367bc Report averaged engine process time in the overlay
ebf3a78 Avoid RenderingServer on web and make autostress configurable
603ad56 Add player, main scene setup and stress-test dummies
eaafcb8 Build the level from an ASCII map with procedural textures
4b6af35 Add autoloaded debug overlay and C# timing helper
1074b4a Add Boomstick project scaffold
```

The author comes from the default git config (YourAppNerds-Admin). Every commit ends with the
Co-Authored-By line. The C# compiles at every commit. Commits 1074b4a to eaafcb8 can't run yet, because
`Main.tscn` and the autoload refer to scripts that later commits add. I wrote the first four messages with a quoting mistake (a
literal `$'\n\n'`) and fixed them with `git filter-branch --msg-filter` before anything else was
built on them. The repo is local and was never pushed.

## How to run

```bash
cd /d/source/repos/godot-web-dotnet/games/boomstick
E=/d/source/repos/godot-web-dotnet/godot-wt/rebase/bin/godot.windows.editor.x86_64.mono.console.exe
$E --path .                                    # desktop, windowed
bash scripts/export-web.sh                     # web build -> build/web
python -m http.server -d build/web 8000        # open http://localhost:8000 (add ?autostress=20 for the scripted run)
```

To re-run the headless smoke test (Edge over CDP):
`node logs/step7d1-smoke.mjs "http://127.0.0.1:8765/index.html?autostress=20" out.log 150 gpu`.
It also saves `out.png`, a screenshot of the page.

## Sizes (release, nothreads, web)

| File | Bytes | gzip -9 |
| --- | ---: | ---: |
| index.wasm | 33,743,479 | 10,318,436 |
| index.pck | 7,899,736 | 2,403,877 |
| index.js | 327,582 | 79,885 |
| total build/web | 42,025,566 | |

The pck is almost entirely the .NET runtime and BCL assemblies, embedded because
`dotnet/embed_build_outputs=true` and the build is not trimmed. The game's own resources are
tiny.

## Verification

1. **Desktop headless.** `--headless --quit-after 600`: exit code 0, and the only output is the
   engine banner and `BOOMSTICK READY (Windows, .NET 10.0.5, map 32x23, 426 open cells)`. No
   errors.
2. **Desktop headless autostress (20 steps).** Clean, `BOOMSTICK STRESS DONE`. Headless desktop
   is capped at about 145 fps.

   | Dummies | engine process (ms/frame) | C# body (ms/frame) |
   | ---: | ---: | ---: |
   | 0 | 4.8 (first-frame noise) | 0.014 |
   | 500 | 1.27 | 0.12 |
   | 1000 | 1.45 | 0.19 |

   Log: `logs/step7d1-desktop-stress.log`.
3. **Web export.** `scripts/export-web.sh` returns 0 with no ERROR lines and takes about 15 s.
4. **Web smoke test** (headless Edge `--headless=new`, D3D11 ANGLE, real GPU, `?autostress=20`):
   - The console shows `BOOMSTICK READY (Web, .NET 10.0.8, map 32x23, 426 open cells)` and then
     `BOOMSTICK STRESS DONE`.
   - There are **no C# exceptions** and no errors after the fix below.
   - The renderer is WebGL 2.0 Compatibility.
   - The screenshot `logs/step7d1-web-smoke.png` shows correct walls, floor and ceiling winding,
     textures, billboards, the overlay and the hint text. Draw calls are 20 with 300 dummies in
     view.

   The numbers are rough. Headless Edge is locked to vsync, so it sits at 60 fps the whole run
   and fps can't tell the cases apart; the timing columns are more useful.

   | Dummies | fps | C# body (ms/frame) | engine process (ms/frame) | GC 0/1/2 | managed MiB |
   | ---: | ---: | ---: | ---: | --- | ---: |
   | 0 | 60.0 | 0.011 | 2.5 | 0/0/0 | 4.4 |
   | 500 | 59.8 | 0.38 | 6.3 | 0/0/0 | 4.6 |
   | 1000 | 60.0 | 0.85 | 13.1 | 1/0/0 | 8.0 |

   Log: `logs/step7d1-web-smoke.log`.

   **Key finding:** on the web interpreter, one C# `_Process` callback costs about 11 µs per
   node outside the measured C# body, and the body itself is about 0.85 µs per dummy. On
   desktop with the JIT, the whole thing costs about 1.3 µs per node. The per-node script
   dispatch costs much more than the game logic. For D2, enemies and projectiles should be
   updated from one manager node instead of one `_Process` per entity. At about 1000 per-node
   scripts, the frame budget would run out on slower machines.

   The 50-dummy step had one 137 ms hitch, which is the first run of the Dummy code and
   texture creation on the interpreter. It was not repeated.

## Engine / runtime bugs found

1. **`RenderingServer` is unusable from C# on the web template.** Exact console output:
   ```
   Unhandled Exception:
   Godot.GodotObject+NativeMethodBindNotFoundException: Unable to find the native method bind. (Method 'RenderingServer.get_video_adapter_type')
      at Godot.GodotObject.ClassDB_get_method_with_compatibility(StringName type, StringName method, UInt64 hash)
      at Godot.RenderingServer..cctor()
   ERROR: System.TypeInitializationException: TypeInitialization_Type, Godot.RenderingServer
   ```
   - **Minimal repro:** any C# script in a web export that touches any `RenderingServer` member,
     for example `GD.Print(RenderingServer.GetVideoAdapterName());` in `_Ready`. It works on
     desktop.
   - **Cause (probable, not fully confirmed):**
     - The C# glue resolves every method bind in the class's static constructor, so one missing
       bind kills the whole class.
     - `get_video_adapter_type` returns `RenderingDeviceEnums::DeviceType`. Its
       `VARIANT_ENUM_CAST_EXT(..., RenderingDevice::DeviceType)` lives in
       `servers/rendering/rendering_device.h:2045`.
     - The web build sets `"rendering_device": False` (`platform/web/detect.py:83`), so the
       bound return type metadata, and therefore the method hash, differs from the API the
       GodotSharp nupkg was generated from (desktop editor).
     - Other methods whose types depend on RenderingDevice may be affected the same way.
       `RenderingServer` is the important one, because it is needed for MultiMesh or
       low-level drawing speedups.
   - **Fix options:**
     - Bind the enum independently of `rendering_device`.
     - Or have bindings_generator resolve method binds lazily, or tolerate missing binds in
       the cctor.
   - **Workaround in game:** don't call `RenderingServer` from C#.
2. **The editor and the web templates come from different commits.** The desktop banner says
   `c81d539c9` and the web banner says `0e25a7958`. That range only contains `[.NET] Web:`
   commits, and it is not the cause of bug 1, but it is worth keeping in mind.
3. **Not a bug, but a pitfall:** without a `.sln`, the web export "succeeds" with
   `ERROR: ... no solution file was found at ... Boomstick.sln` and writes a 37 KB pck with no
   assemblies. The game then has no C# at runtime. `export-web.sh` now fails on any ERROR line.

No other issues: `async`/`ToSignal`, `JavaScriptBridge.Eval`, `Image.CreateFromData`,
SurfaceTool/ArrayMesh, physics, pointer lock and GC all worked in the web build.

## Suggested D2 scope

- **Weapons:** a hitscan shotgun (the boomstick) with a pellet spread using `PhysicsRayQueryParameters3D`; a 2D HUD weapon sprite with procedural frames, bob, and a muzzle flash light; ammo.
- **Enemies:** billboard Sprite3D with 4- or 8-direction procedural frames. A simple state
  machine: idle, chase (grid line of sight and BFS on the ASCII grid), attack (projectile),
  pain, death. Update them all from **one EnemyManager `_Process`**, not one per node, based on
  the dispatch-cost finding above.
- **Pickups:** health, ammo and armor as bobbing billboards with Area3D overlap or grid
  distance checks.
- **HUD:** a Doom-style bottom bar with health, armor, ammo and a face, plus damage and pickup
  flashes.
- **Optional:** doors (a `D` map cell sliding on use), procedural sound through
  AudioStreamGenerator to test web audio worklets, and a level exit.
- **Measurement:** extend autostress to N enemies with AI. Compare per-node `_Process` with a
  manager node, and record wasm/pck size after trimming (`GodotTrimGodotSharp`).
