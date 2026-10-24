# Step 7d C1: Cookie Crunch, first playable

New local repo: `D:\source\repos\godot-web-dotnet\games\cookie-crunch` (branch `main`, not pushed,
no GitHub repo). Engine: `godot-wt/rebase` editor and templates at 11875322e, nothreads release
Web template. No engine changes.

## What was built

- **Cookie**: a 320 px procedural texture (bumpy antialiased edge, toasted rim, top-left light,
  speckles, 11 chocolate chips), built from a byte array in 7-10 ms on desktop and 34-48 ms
  on the web. It has a spring scale bounce on press, a hover grow, slowly turning light rays, a
  glow, and floating "+N" texts. All of this is drawn by one `CookieView._Draw`, and there are
  no per-node `_Process` callbacks.
- **Header**: the cookie count and "per second", refreshed at 10 Hz, and also right away on a
  click.
- **Store**: eight buildings (Cursor 15 / 0.1, Grandma 100 / 1, Farm 1,100 / 8, Mine 12,000 / 47,
  Factory 130,000 / 260, Bank 1.4M / 1,400, Temple 20M / 7,800, Wizard Tower 330M / 44,000).
  - Price is `base * 1.15^owned`. x10 uses the geometric sum, and Max solves for the count in
    closed form with a boundary fix.
  - A row stays locked (dark silhouette icon, "???") until you hold 60% of its base price or own
    one. The unlocked state is saved.
  - Rows are 96 logical px tall, which is at least 53 CSS px on a 412-wide phone. The price shows
    green or red, and the owned count is shown large.
  - The x1 / x10 / Max toggle is a ButtonGroup. Locked or unaffordable rows are disabled.
- **Number formatter** (`src/NumberFormat.cs`):
  - Below 1e6: whole numbers with thousands separators.
  - Up to 1e66: a 3-decimal truncated mantissa plus the name, from million to vigintillion.
  - Above that: scientific notation, such as `1.797e308`.
  - NaN shows as 0, infinity as "Infinity", and -0 as "0".
  - `FormatRate` keeps one decimal for small rates (0.1/s).
- **Self test**: 35 explicit checks (formatter edge cases, every power of ten up to 1e308, save
  JSON round trip, rejecting a future version or garbage, price math, unix clock). It runs at
  startup in debug builds, with `-- --selftest` on headless desktop (exit code 0/1), or with
  `?selftest` on the web. It passes on desktop, headless and the web release build.
- **Layout**:
  - Base size 720x720, `canvas_items` stretch, `expand`.
  - Portrait (width < height): header, then the cookie, then the store filling the rest.
  - Landscape: header and cookie on the left 46%, store on the right.
  - It re-flows on `Viewport.SizeChanged`. Mouse and touch both work.
- **Save** (`src/SaveSystem.cs`):
  - Format: JSON through a System.Text.Json source-generated context, so it needs no reflection
    and keeps working if trimmed. Fields: `Version` (1), cookies, total baked, clicks, owned
    per building id, unlocked ids, `SavedAt` (unix s), `StartedAt`, `Seq`.
  - Saves happen every 30 s, on focus out, on close request or `_ExitTree` (desktop), and on the
    web on `visibilitychange` (hidden) and `pagehide`.
  - On the web every save is also mirrored to `localStorage`, and loading takes the copy with
    the higher `Seq`.
  - Offline earnings = cps * elapsed, capped at 24 h, shown in an in-game "While you were away"
    modal (at 3 s or more away).
  - The Reset button opens an in-game confirm modal (Cancel / Reset), and Esc closes modals.
- The F3 overlay and `Perf` were copied from boomstick (three-finger tap also toggles it).
  `?stats`, `?fresh`, `?testhooks` (publishes layout and state as `window.cookieCrunch`), and
  `--autoplay` / `--shot=` on desktop were added for testing.

## Commits (cookie-crunch repo)

```
bf9accd Save on web canvas blur, let touch drags scroll the store, add README
065d78a Add the game: cookie, store, responsive layout, autosave and offline earnings
30929fd Add building table, price math and JSON save with a localStorage mirror on web
ca8a98b Add the big number formatter with a self test
6dbb218 Add the F3 perf overlay and Perf timers from boomstick
aebca0b Project skeleton from boomstick: csproj, local NuGet feed, Web preset (nothreads), export script
```

## Sizes (release, nothreads, `scripts/export-web.sh`, export 12 s)

| file | bytes | gzip -9 |
| --- | ---: | ---: |
| index.wasm | 33,744,846 | 10,319,369 |
| index.pck | 8,165,392 | 2,428,316 |
| index.js | 327,582 | 79,885 |
| total folder | 42.3 MB | |

The wasm is the same engine and runtime as boomstick. The pck is almost all .NET assemblies,
since the game has no assets. The export log shows no ILLink or trim step, so the assemblies
are not trimmed. The source-generated JSON context is used anyway, so the save keeps working if
trimming is turned on later.

## Verification

- **Headless desktop**:
  - `--selftest`: PASS, exit 0.
  - `--autoplay`: clicks, buys two cursors, and saves on exit.
  - The next run loads the file and shows the offline popup (away 9 s, +2).
  - `--fresh` deletes the save. No errors.
- **Desktop windowed**: 1280x720 lays out as landscape and 450x900 as portrait. Screenshots:
  `logs/step7dC1-desktop-{landscape,portrait}.png`.
- **Web**: a CDP script modelled on step7d1-smoke (Edge headless, GPU/D3D11, persistent
  profile): `logs/step7dC1-webtest.mjs`, log `logs/step7dC1-web.log`.
  **Result: 18/18 PASS, `SMOKE SUMMARY exceptions=0 errors=0 warnings=0 failures=0`.**
  - The self test passes on the release web build, and the `JavaScriptBridge.CreateCallback`
    page hooks install.
  - 130 mouse clicks on the cookie give 130 cookies (130 click round trips took about 2.5 s).
    Buying a Grandma and a Cursor by clicking their rows gives cps 1.1. Idle income was +5.6
    in 5 s.
  - Focus out (canvas blur) saved, and the save reached IndexedDB **47 ms later**.
  - Reload right after 10 more clicks: everything was restored, with cookies 38.4 -> 39.6.
  - Leave to about:blank for 5 s, then come back: the offline popup shows (+7 for 6 s). Esc
    closes it.
  - Close the tab, wait 5 s, open a new tab: restored, and the popup shows.
  - `Browser.close`, then relaunch Edge with the same profile: restored (35 -> 44 including
    offline earnings).
  - Portrait 412x915 at DPR 2.625 with mobile and touch emulation: the portrait layout is used,
    and 20 touch taps give +20 cookies. A finger drag scrolls the store. At this size the list
    almost fits, so the maximum scroll is only about 7 px.
  - Screenshots: `logs/step7dC1-web-landscape.png`, `-offline-popup.png`, `-portrait.png`.
    The overlay is on because of `?stats`.
- Not covered by automation: the Reset confirm flow through a UI click (the code path is
  `AskReset` -> modal -> `ResetGame`; `?fresh` exercises the delete) and real phones.

## Performance (web, from the overlay and the `[perf]` lines)

| | fps | frame ms | C# _Process ms/f | cpu frame ms | render submit ms | draw calls | nodes | GC | managed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| landscape 1280x720 | 59.1 | 16.9 | 0.07-0.09 | 2.4-3.0 | 2.2-2.7 | 86 | 94 | 0/0/0 | 4.8 MiB |
| portrait 412x915 @2.625 | 58.3-60 | 16.7-17.2 | 0.12-0.17 | 2.5-2.8 | 2.2-2.5 | 99 | 94 | 0/0/0 | 4.8 MiB |

- Game logic is negligible. Most of the frame is render submission: about 90 draw calls, from
  labels, style boxes and the 12 ray polygons.
- The only spikes (max 120-180 ms) are in the first seconds after load: texture generation
  and the first layout.
- No GC ran during the test.

## Save persistence findings (web)

1. **When the IndexedDB sync happens.** Closing a written `user://` file sets `idb_needs_sync`
   (`OS_Web::file_access_close_callback`). The next `OS_Web::main_loop_iterate`, which is the
   next requestAnimationFrame, then starts an asynchronous `FS.syncfs`. In the test the data was
   in IndexedDB 47 ms after the save. `JavaScriptBridge.ForceFsSync()` only sets the same flag,
   so it does not help.
2. **Saves made while the page goes away never reach IndexedDB.** In `pagehide`,
   `visibilitychange` and tab close there is no next frame, so the sync never starts. Hidden
   tabs also pause rAF. In the test, `user://save.json` stayed at the last in-game save
   (seq 1) across reload, tab close and browser restart, while the page-exit saves (seq 3, 7)
   existed only in localStorage. Without the mirror, a reload right after playing would lose
   everything since the last autosave, up to 30 s.
3. **The fix that works:** write the same JSON synchronously to `localStorage` on every save,
   and on load take the newer copy by `Seq`. The C# callbacks from
   `JavaScriptBridge.CreateCallback` run synchronously inside `pagehide` and `visibilitychange`.
   This survived reload, closing the tab and opening a new one, and a full browser restart.
4. **The web platform never sends `NOTIFICATION_APPLICATION_FOCUS_OUT` or
   `NOTIFICATION_WM_CLOSE_REQUEST`.** A canvas blur arrives only as
   `NOTIFICATION_WM_WINDOW_FOCUS_OUT`. Without that handler the focus-out save never happened
   on the web.
5. **The clock is correct on the web.** `DateTimeOffset.UtcNow` unix time equals JS
   `Date.now()/1000`; both are printed in the READY line.
6. `OS.IsUserfsPersistent()` is true. The save path in IndexedDB is DB `/userfs`, store
   `FILE_DATA`, key `/userfs/godot/app_userdata/Cookie Crunch/save.json`.

## Engine and runtime bugs or quirks found

- No crashes, "function signature mismatch" errors, or managed exceptions on the web. Source-gen
  System.Text.Json, `JavaScriptBridge.Eval`, `GetInterface`, `CreateCallback`,
  `Image.CreateFromData`, `GradientTexture2D` and `RenderingServer.FramePostDraw` all work.
- Not bugs, but worth knowing:
  - The web `focus_out` mapping (finding 4 above).
  - **Touch drags cannot scroll a `ScrollContainer` through `MOUSE_FILTER_STOP` children.**
    Godot sends `ScreenTouch` and `ScreenDrag` to the control under the finger, and `STOP` ends
    the bubbling. This is upstream behaviour, not specific to the web. Fix: store rows use
    `MOUSE_FILTER_PASS`. `BaseButton` cancels its press on `NOTIFICATION_SCROLL_BEGIN`, and
    `ActionMode = Release` keeps drags from buying.
  - In the desktop build, `GetTree().Quit()` sends no close request, so the game also saves in
    `_ExitTree`.

## Suggestions for C2

- **Upgrades**: a data table like `Buildings` (click multipliers, per-building x2), shown as an
  icon grid above the store. Recompute cps only on purchase, as now. Save them as a list of ids
  and bump the save `Version` to 2 with a migration in `FromJson`.
- **Achievements**: thresholds checked in the 10 Hz UI tick, not every frame, plus a toast
  queue (the `_toast` label exists already).
- **Golden cookie**: one extra drawn sprite in `CookieView` or a sibling Control, with a timer in
  `Game._Process`. Use a hit-test like the cookie's.
- **MultiMesh falling cookies**: one `MultiMeshInstance2D` behind the UI, with the instance
  buffer filled from a C# array once per frame (the boomstick pattern). The rate should follow
  cps and clicks, with an instance cap. Reuse the cookie texture at a small mip.
- **Sound**: port boomstick's `Sfx.cs` (synthesized `AudioStreamWAV`s, unlocked by the first
  gesture) for click crunch, buy and golden cookie sounds.
- **Stats**: a modal page with total baked, clicks, time played (`StartedAt`) and building cps
  shares.
- **Save export/import**: `DisplayServer.ClipboardSet`/`ClipboardGet`, with a base64 of the JSON.
  On the web the clipboard needs a user gesture and `navigator.clipboard`; check whether
  Godot's web clipboard read works or needs a paste text field fallback.
- Reduce draw calls (about 90) if phones show render-submit cost: fewer separate style boxes,
  and draw the rays into the glow texture.
- Test on a real phone over HTTPS (GitHub Pages), including Android tab discard and the iOS
  `pagehide` behaviour for the localStorage mirror.
