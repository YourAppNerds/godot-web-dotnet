# Step 7 D2: Boomstick becomes a playable game

Boomstick (`games/boomstick`, its own git repo, branch `main`) is now a short playable level.

- **Gameplay:** three weapons, three enemy types with AI, pickups, a Doom-style status bar,
  procedural sound, death and restart, and an exit with a results screen.
- **Assets:** everything is still generated in code. There are no asset files.
- **Rendering:** enemies and projectiles are drawn with one MultiMesh per type. That costs a
  handful of draw calls for any number of enemies.
- **Overlay:** the debug overlay now reports trustworthy frame timings (see "Overlay timing
  fix").
- **Git:** nothing was pushed, and no GitHub repo was created.

## Features

- **Weapons** (`src/Weapons.cs`, `src/Defs.cs`):
  - Pistol: hitscan.
  - Shotgun: 8 hitscan pellets with spread.
  - Rocket launcher: projectile, splash damage of 110 within 3.5 m, knockback, and half
    damage to yourself.
  - Hitscan uses one physics ray against the walls, then a C# cylinder test against the
    enemies, so enemies need no physics bodies.
  - Ammo is tracked per type: bullets 200, shells 50, rockets 50 (the numbers are the maximum each).
  - The HUD weapon sprite has idle, fire with muzzle flash, and recoil/pump frames, plus walk
    bob, kick and a lower/raise animation when switching.
  - An OmniLight3D muzzle flash fires on each shot. Switch with 1-3 or the mouse wheel.
    An empty weapon clicks and switches to another weapon automatically.
- **Enemies** (`src/EntityManager.cs`, `src/Art.cs`):
  - Fiend: a melee rusher, 70 HP, fast.
  - Imp: 50 HP, throws visible fireballs and circle-strafes when close.
  - Brute: 320 HP, fires a three-shot plasma volley and also has a melee attack.
  - Each type has a 9-frame procedural atlas: idle, walk A/B, attack windup and release,
    pain, and three death frames ending in a corpse.
  - Enemies sleep until they see the player or hear a shot:
    - **Sight:** a grid DDA check first, then a physics raycast against the wall collision.
      At most 24 rays run per frame, and each enemy re-checks every 0.2-0.35 s.
    - **Sound:** a shot wakes enemies within 12 cells (pistol) or 18 cells (others) of
      *path* distance.
  - **Pathing:** a BFS distance field over the ASCII grid, with 8 neighbours and no corner
    cutting. It is recomputed only when the player changes cell, at most every 0.15 s, and
    every enemy walks down it. With line of sight they head straight for the player.
  - Enemies push apart through per-cell buckets, and there is a chance of a pain stagger.
  - Every enemy, projectile, hit effect (puff, blood, explosion) and pickup is plain C# data,
    updated by **one** `EntityManager._Process`.
- **Player** (`src/Player.cs`):
  - Health, plus armor that absorbs 1/3 of damage (green) or 1/2 (mega armor).
  - Damage feedback: a red flash that scales with damage, an "ouch" face that looks towards
    the attacker, and a hurt sound.
  - Death: the camera sinks and tilts, and the game shows "YOU DIED, PRESS R TO RESTART".
    R reloads the scene.
- **Pickups:**
  - h = stimpack +10
  - m = medikit +25
  - a = armor 100
  - A = mega armor 200
  - c = clip
  - s = shells
  - r = rockets
  - G = shotgun
  - R = rocket launcher

  Each pickup shows a message and a flash; a weapon pickup also makes the face grin. A pickup
  you can't use stays on the floor.
- **HUD** (`src/Hud.cs`, `src/HudArt.cs`, `src/Pix.cs`):
  - A 320x26 pixel-art status bar drawn at 4x, with red digits in a custom 5x7 bitmap font.
  - Sections: ammo, health %, arms (lit when owned), face (5 health tiers, look left/centre/right,
    ouch, grin, dead), armor %, an ammo table (current type highlighted), and kills x/y.
  - Also a crosshair, pickup messages, and the death and level-complete screens.
- **Sound** (`src/Sfx.cs`):
  - 18 effects are synthesized at startup into 16-bit AudioStreamWAVs: pistol, shotgun,
    pump, rocket, explosion, fireball, plasma, impact, alert, pain, death, swipe, player
    hurt and death, item, weapon pickup, empty click, and the level jingle.
  - They play through a pool of 12 AudioStreamPlayers, with distance attenuation computed in
    C#. Movie-maker recordings confirmed the audio, and I added 6 dB of headroom after it clipped.
- **Level** (`src/Level.cs`):
  - The map grows from 32x23 to 48x38.
  - Route: start room, stone hall (shotgun), an optional armory wing (rocket launcher, rockets,
    green armor), the brick room, the side room, a corridor south,
    the pillared courtyard (brute), the guard room, and the metal exit room (a second brute)
    with green `X` exit pads and a green light.
  - 20 enemies in total. A script checked that every cell is reachable from the start.
  - Each cell has a "sector" light level computed from the lamps. Sprites are unshaded and
    take this light level, which looks Doom-like and costs nothing per frame.
  - Reaching `X` shows "LEVEL COMPLETE, TIME m:ss, KILLS k / n" and freezes the enemies.
- **Kept from D1:**
  - The F3 overlay.
  - `?autostress=N`, and the desktop `-- --autostress=N --quit-when-done`.
  - F4 now spawns 50 **real enemies through the manager**, awake and hunting.
  - F6 spawns 50 D1 per-node `Dummy` sprites.
  - F5 clears both.
  - Autostress modes:
    - `stressmode=enemies` (the default): full AI.
    - `stressmode=wander`: managed entities running the D1 dummy logic, for a like-for-like
      comparison.
    - `stressmode=nodes`: the per-node dummies.
    - `enemyrender=nodes`: one Sprite3D per enemy instead of MultiMesh.
  - Autostress turns on god mode and removes the level's enemies first, so the counts are exact.
- **Coordinator requests handled during D2:**
  - **Overlay fix.** See "Overlay timing fix".
  - **Draw calls.** Enemies and projectiles are batched with MultiMeshInstance3D through
    the node API.
  - **Touch note.** On touch screens the start hint says touch controls are coming. Touch
    controls themselves are for D3.
  - **RenderingServer.** After step 7e, RenderingServer is used again: the READY line prints
    the GPU adapter name, and the overlay connects `RenderingServer.FramePostDraw`.
- **Desktop test arguments:** `--warp=x,z,yaw`, `--giveall`, `--weapon=N`, `--godmode`,
  `--autofire`. Combined with `--write-movie` they gave the screenshots used to check the art.

## Controls

| Input | Action |
| --- | --- |
| Click | Capture the mouse |
| Mouse | Look |
| LMB / Ctrl | Fire |
| 1 2 3 / wheel | Pistol / shotgun / rocket launcher |
| WASD / arrows | Move |
| Shift | Run |
| Space | Jump |
| R | Restart (after death or the exit) |
| Esc | Release the mouse |
| F3 | Overlay |
| F4 | +50 managed enemies |
| F6 | +50 per-node dummies |
| F5 | Clear stress spawns |

## Commits (games/boomstick, main, after c77055b)

```
e3949c3 Fix the web stressmode query parameter
255ca5c Update README for D2: controls, game, overlay and stress modes
c78cc11 Use RenderingServer again, add a touch note, keep build/ out of exports
255e1da Show trustworthy frame timings in the debug overlay
0b7951c Batch projectiles with MultiMesh and cache the physics space per frame
81a4f0a Wire up the game, restart, level exit and managed stress modes
21e9fc4 Add Doom-style HUD
7147107 Add weapons, health, armor, pickups and death to the player
4b7f446 Add entity manager for enemies, projectiles, effects and pickups
8650e00 Extend the level with enemies, pickups and an exit
2572c54 Add procedurally synthesized sound effects
b9e159b Add pixel canvas, bitmap font and procedural sprite and HUD art
```

Every commit ends with the Co-Authored-By line.

The new files reference each other through the shared `Game` and `Defs` types, so the first
six feature commits (b9e159b to 21e9fc4) do not compile on their own. 81a4f0a and every
later commit compile and run.

## Overlay timing fix (coordinator request)

**What the D1 overlay showed as "engine process":** it was `Performance.TIME_PROCESS`,
averaged per frame. In the engine (`main/main.cpp`, around line 5143):

```
process_ticks = OS::get_singleton()->get_ticks_usec() - process_begin;
process_max = MAX(process_ticks, process_max);
...
if (frame > 1000000) { ... performance->set_process_time(USEC_TO_SEC(process_max)); process_max = 0; }
```

So TIME_PROCESS has two problems as a per-frame figure:

1. It is the **maximum** process time over the last ~1 s window, not an average. It is
   in seconds, and the overlay converted it to ms correctly.
2. The measured span covers `MainLoop::process`, navigation, `RenderingServer::sync` **and
   `RenderingServer::draw`**. On WebGL that is the render submission, which is the expensive
   part when there are hundreds of draw calls.

That is why the browsers showed 25.5 ms "engine process" at a steady 60 fps, and 11.1 ms at
0 dummies against an 8.33 ms frame on the Pixel. One slow frame per second, such as a GC or a
hitch, sets the whole figure. That also explains the nonlinear growth in the Brave data
(2.9, 7.2, 25.5 ms). It is a worst-case-per-second number that combines script time,
render submission and any spike, so it doesn't track average cost.

**New overlay** (`src/DebugOverlay.cs`):

- **cpu frame**: from the first callback of the frame (a `FrameProbe` node at
  `int.MinValue` priority for both process and physics) to `RenderingServer.frame_post_draw`.
  This is the whole CPU frame except the vsync/idle wait. It shows the average and the max.
- **render submit**: from the overlay's `_Process` (the last process callback) to
  `frame_post_draw`. This is mostly `RenderingServer::draw`.
- **engine process max/1s**: kept, but labelled honestly.
- The `[perf]` and `STRESS` lines now also print `cpu_frame_ms`, `cpu_frame_max_ms`,
  `render_ms`, `engine_process_max_ms` and `draw_calls`.

Caveats:

- On windowed desktop GL, the buffer swap happens inside `RenderingServer::draw`, so render
  includes the vsync block there. Only its growth over the 0 baseline means anything.
- On web, the browser presents after the rAF callback, so render is pure CPU submission.
- Headless desktop does not draw, so those columns read 0 there.

**Correction to the D1 finding.**

- D1 concluded that "one C# `_Process` costs ~11 µs per node on web". That was derived from
  the misleading TIME_PROCESS average.
- With the new timers (headless Edge, `stressmode=nodes`), the process phase (cpu frame minus
  render) grows from 0.44 ms to 2.20 ms at 1000 dummies. That is about 1.8 µs per node all
  in, of which the C# body is about 0.9 µs. So the per-node dispatch costs **about 0.9 µs
  per node, not 11 µs**.
- At the same time, render submission grows from 2.5 ms to 8.3 ms (765 draw calls).
- **The real per-entity cost on the web is the draw calls, not the script dispatch.** The
  phone data says the same thing (see below).
- One manager is still the right design: the process phase drops from +1.8 ms to +0.4 ms
  per 1000 entities, and batching is only possible when the manager owns the data.

## Real-browser D1 baseline (coordinator data, D1 build, release nothreads)

These were measured with the old overlay. The "engine process" column is TIME_PROCESS, a
**1-second maximum that includes render submission**, as explained above.

Brave / Chromium, Windows desktop:

| dummies | FPS | C# _Process | engine process (max/1s) | draw calls | nodes |
|---|---|---|---|---|---|
| 0 | 60 | 0.025 ms | 2.90 ms | 39 | 232 |
| 500 | 60 | 0.444 ms | 7.20 ms | 494 | 732 |
| 1000 | 60 | 0.800 ms | 25.50 ms | 942 | 1232 |

Android, Pixel 8 Pro, Brave:

| dummies | FPS | frame ms (max) | C# _Process | _Physics | engine process (max/1s) | draw calls | GC gen0 | managed |
|---|---|---|---|---|---|---|---|---|
| 0 | 120 | 8.33 (8.3) | 0.048 | 0.165 | 11.10 | 52 | 0 | 4.50 MiB |
| 350 | 48.8 | 20.51 (27.0) | 1.223 | 0.292 | 25.85 | 401 | 0 | 4.64 MiB |
| 700 | 29.7 | 33.65 (35.2) | 2.013 | 0.388 | 41.58 | 686 | 1 | 7.73 MiB |
| 1000 | 24.3 | 41.11 (45.9) | 2.814 | 0.429 | 49.21 | 956 | 1 | 7.73 MiB |

Firefox, Windows desktop (same PC as Brave):

| dummies | FPS | frame ms (max) | C# _Process | _Physics | engine process (max/1s) | draw calls | GC |
|---|---|---|---|---|---|---|---|
| 500 | 60 | 16.67 (16.7) | 0.267 | 0.133 | 11.00 | 513 | 0 |
| 1000 | 56.3 | 17.76 (27.8) | 1.333 | 0.133 | 24.00 | 981 | 0 |

Reading:

- The phone is draw-call bound. Frame time rises about 35 µs per added draw call while C#
  stays under 3 ms.
- Firefox is the first desktop browser to drop below 60 fps at about 1000 draw calls. Its
  C# time at 1000 dummies is about 1.7 times Brave's (1.33 against 0.80 ms), so the wasm
  interpreter is slower there as well.
- The single gen0 GC on the phone at around 700 dummies caused no visible stutter.

All three point the same way as the corrected headless numbers below: cut draw calls. In D2,
any number of enemies plus projectiles costs about 6 draw calls (3 enemy types and 3
projectile kinds), so the phone's per-draw cost no longer scales with enemy count.
Projection, not yet measured on the phone: the D2 level with 50 enemies on screen adds about
3 draw calls to the level's roughly 80-125, against 350 or more for per-sprite rendering. The
D2 numbers below should be re-measured on the Pixel to confirm the 60 fps target.

## D2 performance

### Web: headless Edge, D3D11 ANGLE, real GPU, release nothreads

Logs: `logs/step7d2-web-{enemies,enemies-sprites,wander,nodes}.log`. Headless Edge is
vsync-locked (55-60 fps), so use the cpu frame column, not FPS. Times are ms per frame.

| mode | count | C# process | cpu frame (max) | render submit | draw calls | nodes | GC0 |
|---|---:|---:|---:|---:|---:|---:|---:|
| enemies, MultiMesh (full AI) | 0 | 0.15 | 2.34 (5.7) | 1.94 | 122 | 543 | 1 |
| | 250 | 1.16 | 3.31 (5.0) | 1.93 | 142 | 545 | 1 |
| | 1000 | 2.76 | 4.97 (7.6) | 1.97 | 128 | 545 | 3 |
| enemies, Sprite3D each (full AI) | 0 | 0.13 | 2.03 (5.1) | 1.69 | 122 | 540 | 1 |
| | 250 | 1.11 | 4.19 (9.5) | 2.90 | 320 | 826 | 1 |
| | 1000 | 2.63 | 8.51 (13.6) | 5.66 | 944 | 1621 | 3 |
| wander, managed + MultiMesh | 0 | 0.17 | 2.98 (6.0) | 2.52 | 122 | 543 | 1 |
| | 250 | 0.28 | 2.87 (4.4) | 2.38 | 125 | 543 | 1 |
| | 1000 | 0.67 | 3.11 (5.7) | 2.20 | 125 | 543 | 2 |
| nodes, D1 Dummy per node | 0 | 0.16 | 2.92 (6.2) | 2.48 | 122 | 543 | 1 |
| | 250 | 0.35 | 4.42 (8.5) | 3.60 | 280 | 793 | 1 |
| | 1000 | 1.05 | 10.50 (18.7) | 8.30 | 765 | 1543 | 2 |

Key comparisons at 1000 entities on web:

- **Per-node against managed, with the same trivial logic** (nodes against wander):
  - CPU frame: 10.50 against 3.11 ms, so the managed version uses about 30% of the per-node
    cost.
  - Process phase: +1.8 against +0.4 ms over the 0 baseline.
  - Render: 8.3 against 2.2 ms.
- **MultiMesh against Sprite3D, with the same full AI:**
  - CPU frame: 4.97 against 8.51 ms.
  - Draw calls: 128 against 944.
  - Render: 1.97 against 5.66 ms.
- **Full AI** costs about 2.6 µs per enemy per frame in C# on the interpreter. That covers
  movement, separation, pathing, line of sight and attacks (2.76 ms for 1000 awake enemies),
  and includes up to 24 physics rays per frame.

### Desktop: Windows, .NET 10 JIT, Radeon RX 6600

Headless (no rendering, capped at about 145 fps). Logs: `logs/step7d2-desktop-headless-*.log`.
The "engine max" column is TIME_PROCESS (max/1s); the 16.9 ms value is a one-off spike.

| mode | 0: C# / engine max | 250: C# / engine max | 1000: C# / engine max |
|---|---|---|---|
| enemies, MultiMesh | 0.018 / 1.24 | 0.337 / 16.9 (one spike) | 0.612 / 2.11 |
| enemies, Sprite3D | 0.020 / 1.57 | 0.245 / 1.93 | 0.696 / 2.80 |
| wander, managed | 0.014 / 1.44 | 0.073 / 1.04 | 0.159 / 0.82 |
| nodes, per-node | 0.015 / 1.23 | 0.074 / 1.60 | 0.207 / 2.42 |

Windowed, 1280x720. Logs: `logs/step7d2-desktop-window-*.log`.

- The window ran at about 31 fps, probably because the background window was throttled.
- Render here includes the blocking swap, so the table shows the growth of cpu frame over the
  0 baseline.

| mode | 0 | 250 | 1000 | draw calls at 1000 |
|---|---:|---:|---:|---:|
| enemies, MultiMesh | 31.77 | +0.33 | +0.98 | 128 |
| enemies, Sprite3D | 31.72 | +2.02 | +9.44 | 985 |
| nodes, per-node | 31.91 | +1.15 | +6.27 | 729 |

Desktop shows the same pattern: per-entity cost is dominated by draw calls. The C# AI for
1000 enemies costs 0.6 ms on the JIT against 2.8 ms on the web interpreter, about 4.5 times
slower on web.

### Level baseline

- With no stress spawns, the level renders 74-125 draw calls depending on the view.
- D1 was 20-52, and the difference is the bigger map and 12 lamps plus the exit light. In
  the compatibility renderer, each mesh surface is drawn again for the omni lights that touch
  it.
- This is now the biggest draw-call cost and the next target (see D3).

## Sizes (release, nothreads, web)

| File | Bytes | gzip -9 | D1 |
| --- | ---: | ---: | ---: |
| index.wasm | 33,744,846 | 10,319,369 | 33,743,479 |
| index.pck | 7,946,376 | 2,337,065 | 7,899,736 |
| index.js | 327,582 | 79,885 | 327,582 |
| total build/web | about 42.07 MB | | 42,025,566 |

The whole D2 game adds about 47 KB to the pck. The art and sound are generated at runtime.

## Verification

1. **Desktop headless.**
   - `--headless --quit-after 600`: exit 0. The only output is the banner and
     `BOOMSTICK READY (Windows, .NET 10.0.5, map 48x38, 842 open cells, 20 enemies, GPU )`.
     The GPU name is empty because headless has no adapter.
   - All four autostress modes run clean to `BOOMSTICK STRESS DONE`.
   - The exit trigger works: with `--warp=33,32` the game prints
     `LEVEL COMPLETE time=0.0s kills=0/20`.
2. **Rendered desktop checks** with `--write-movie`:
   - Combat, the death screen, the face states, the shotgun muzzle flash, batched enemies and
     fireballs, the status bar, and recorded audio (`b.wav`, non-silent).
   - Example screenshots during development: `build/shots` (not kept).
3. **Web export.** `scripts/export-web.sh` returns 0 with no ERROR lines in about 13 s.
4. **Headless Edge smoke tests:** five runs (gameplay and four stress modes). In all of them:
   - **No C# exceptions** and no errors.
   - The READY line shows the new map and `GPU WebKit WebGL`, so RenderingServer works from
     C# on web after step 7e.
   - Screenshots: `logs/step7d2-web-play.png` (level start, HUD, overlay) and
     `logs/step7d2-web-enemies.png` (1000 enemies, 133 draw calls).
   - The only warning is the expected "AudioContext was not allowed to start" (no user
     gesture in headless).

## Bugs and pitfalls found

1. **The `TIME_PROCESS` monitor is misleading as a per-frame figure** (engine behaviour, not
   a crash).
   - It is the max over about 1 s and includes `RenderingServer::draw`; see the overlay
     section for the exact lines.
   - **Minimal repro:** `Performance.GetMonitor(Performance.Monitor.TimeProcess)` in any
     project with a few hundred draw calls on web. It reads above 16.7 ms while the page holds
     60 fps.
   - Possible upstream improvements: document that it is a windowed max, or add averaged
     process and render-submit monitors.
2. **The pck grew from 7.9 MB to 97.8 MB.**
   - Cause: movie-maker PNGs written under `build/` were imported as project resources and
     exported, because the preset uses `export_filter="all_resources"`.
   - Fixed in the game: `export-web.sh` writes `build/.gdignore`, and the preset has
     `exclude_filter="build/*"`.
   - Worth applying to every test project that writes output inside its own folder.
3. **Web query parsing bug (mine, fixed in e3949c3).** `stressmode` kept its leading `=` on
   web, so every web run silently used `enemies`. The first web wander and nodes runs were
   redone.
4. **Port collision (environment).** Another `python -m http.server 8765 --bind 0.0.0.0`
   (PID 17548, not started by me) owned port 8765.
   - My server failed to bind silently, and the first smoke runs loaded an **old D1 build**
     from that server.
   - I left that process alone and moved to port 8792 with a curl check. Smoke scripts
     should check what they serve.
5. **`ObjectDB instances were leaked at exit`** (5 instances) on windowed desktop exit.
   - These are the static texture, shader and sound caches (`Art`, `HudArt`, `Sfx`,
     `EntityManager` shaders) that live for the whole process. They are harmless.
   - D3 could free them on quit to keep the log clean.
6. **RenderingServer from C# on web** (D1 bug 1): **verified fixed** with the step 7e
   templates. `RenderingServer.GetVideoAdapterName()` and the `FramePostDraw` signal both work
   in the web build.

No other engine or runtime problems showed up. MultiMesh through the node API
(`MultiMesh.Buffer` set with a `float[]` once per frame, custom data and a custom AABB),
ShaderMaterial with INSTANCE_CUSTOM, AudioStreamWAV generated at runtime, physics rays from
`_Process`, `ReloadCurrentScene` and the autoload surviving reloads all work on web.

## Suggested D3

- **Level polish:**
  - Doors (a `D` cell that slides on use) and a couple of secrets.
  - A second level or a looping episode, with a keycard and locked door.
  - Better enemy variety: 8-direction sprites, infighting.
  - Lighting: bake the per-cell light level into vertex colours and drop most OmniLights.
    That cuts the level's draw calls from about 80-125 down to the 20s, which is the main
    remaining cost on phones. Keep a few dynamic lights for the muzzle flash and explosions.
- **Touch controls for Android:**
  - A left virtual stick, drag on the right half to look, and fire, switch and jump buttons
    drawn in the pixel style.
  - Detect with `DisplayServer.IsTouchscreenAvailable()`; the hint already does.
  - Re-measure on the Pixel 8 Pro against the target of 60 fps with about 50 enemies on
    screen, with the new overlay (cpu frame and render submit).
- **Sound polish:**
  - Enemy-specific voices, footsteps and ambient hum.
  - A music track generated with AudioStreamGenerator, which also tests the web audio
    worklet under load.
  - Stereo panning from relative angle.
- **Performance:**
  - Batch pickups and hit effects too (one MultiMesh each).
  - Move LOS to a cheaper grid-only check when far away.
  - Try `GodotTrimGodotSharp` / IL trimming for a smaller pck.
- **GitHub Pages deploy:**
  - A workflow or script that publishes `build/web` (nothreads, so no COOP/COEP needed).
  - Add the `?autostress` and `?enemyrender` links to the page.
  - A "click to start" gate so the AudioContext starts on a user gesture.
