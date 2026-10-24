# Step 7d C2: Cookie Crunch, upgrades, achievements, golden cookie, rain, sound, save export

Repo: `D:\source\repos\godot-web-dotnet\games\cookie-crunch` (branch `main`, not pushed). The work
started from a clean `bf9accd`. Engine: the same `godot-wt/rebase` editor and nothreads release
Web template as C1. No engine changes.

## What was built

- **Upgrades** (`src/Upgrades.cs`): 21 one-time upgrades, sorted by cost.
  - Two click x2 upgrades: "Reinforced index finger" (cost 100, needs 25 clicks) and "Carpal
    tunnel cream" (cost 1,000, needs 200 clicks).
  - "Cookie sense": each click also gains 5% of cps (cost 25,000, needs 10,000 baked).
  - 18 per-building x2 upgrades that appear at 1 / 5 / 25 / 50 owned:
    - Cursor and Grandma: 4 tiers each.
    - Farm and Mine: 3 tiers each.
    - Factory, Bank, Temple and Wizard Tower: 1 tier each.
    - Cost is the building's base cost times 10 / 50 / 500 / 5000.
  - The upgrades show as a sideways-scrolling row of 84 px icon buttons above the store list.
    The icon is the building drawing plus tier pips, or "x2" / "%".
  - A tap or click selects an icon and opens an info panel (name, description, cost, and a
    72 px Buy button). Desktop also gets a hover tooltip.
  - Icons use `MOUSE_FILTER_PASS` and `ActionMode=Release`, as the store rows do, so a finger drag
    scrolls the row instead of selecting.
  - cps and the click value are recomputed only when you buy something or a frenzy starts or ends.
- **Achievements**: 21 in total.
  - 8 cookies-baked milestones, from 1 to 1e11.
  - 5 buildings-owned milestones: 1, 10, 50, 100, 200.
  - 3 click milestones: 100, 1k, 5k.
  - 3 golden cookie milestones: 1, 7, 27.
  - 2 upgrade milestones: 1, 10.
  - They are checked in the 10 Hz UI tick. Each unlock shows a toast panel at the top (from a
    queue, 3 s each; more than 3 at once collapse into "N new achievements") and plays a sound.
    They are listed in the stats panel, with locked ones dimmed.
- **Golden cookie** (`src/GoldenCookie.cs`):
  - Spawns at a random spot on the screen after 60-180 s. It fades in, drifts at about 10 px/s,
    wobbles and pulses, and disappears after 13 s.
  - The clickable area is only its circle (`_HasPoint`), so taps beside it still reach the store.
  - **Lucky** gives `min(15% of the bank, 900 s of cps) + 13`.
  - **Frenzy** gives x7 cps for 77 s. A gold "FRENZY x7 1:17" countdown appears in the header,
    and the frenzy time left is saved. Offline earnings never include the frenzy.
  - `?golden=N` (web) or `--golden=N` / env `COOKIE_GOLDEN=N` (desktop) spawns one every N to 2N s
    and alternates lucky and frenzy, so a test sees both.
- **Falling cookies and crumbs** (`src/CookieRain.cs`):
  - One `MultiMeshInstance2D` draws them all, using the cookie texture with mipmaps, a
    Transform2D and a colour per instance (12 floats each).
  - It has no `_Process`. `Game` calls `Step`, which moves a C# struct array, compacts it, and
    uploads it with one `MultiMesh.Buffer` set (the Boomstick EntityManager pattern).
  - The rate is `log10(1+cps)*4 + clickHeat*1.5` per second, capped at 90/s. `clickHeat` is a
    decaying count of recent clicks.
  - At most 300 falling cookies plus 64 crumbs. Each click throws 6 crumbs and a golden cookie
    throws 24.
  - The cookies fall behind the big cookie, the header and the store. `?rain` forces the maximum
    rate.
- **Sound** (`src/Sfx.cs`, ported from Boomstick):
  - 6 sounds (crunch, buy, upgrade, golden appear, golden click, achievement) are synthesized
    into 16-bit `AudioStreamWAV`s at start: 13-15 ms on the web.
  - An 8-voice pool, pitch jitter, and at most 4 copies of one sound at a time.
  - Nothing plays until the first key, click or tap. `_Input` sees it before the GUI does.
  - Mute is `AudioServer.SetBusMute(0)`, toggled in the stats panel and saved.
- **Stats panel** (the "Stats" button replaced "Reset" in the store bar):
  - Totals: bank, baked all time, cps (with frenzy), per click, clicks, buildings, upgrades,
    golden cookies, time played (active time) and how long ago the bakery started.
  - Options: Sound on/off, and Reset game (the confirm dialog opens above the panel).
  - Save: Export, Paste and Load buttons, a `TextEdit`, and a status line.
  - The achievement list.
  - It refreshes at 10 Hz only while open. Esc closes it.
- **Save version 2** (`src/SaveSystem.cs`): adds `Upgrades`, `Achievements`, `GoldenClicks`,
  `Muted`, `PlayTime` and `FrenzyLeft`. `Migrate()` upgrades v1 (C1) saves and sanitises the new
  fields.
- **Export and import**: export is the base64 of the UTF-8 save JSON (about 450-550 characters).
  Import strips whitespace and line breaks, also accepts raw JSON, and validates. Errors are
  shown in red: "Paste a save first." / "not base64" / "not a valid Cookie Crunch save".
  - After a successful import the save counter is raised above both stored copies, then it
    saves, so the imported state wins on the next load.
- Kept from C1: logic stays in the one `Game` node, UI texts refresh at 10 Hz, the portrait and
  landscape layouts, 72-96 px touch targets, and the localStorage save mirror.

## Commits (cookie-crunch repo)

```
037cbb1 Test hooks hide stats buttons scrolled out of view; README for C2
3d78b7c Add Godot uid files for the new scripts
c83886c Wire up upgrades, achievements, golden cookie and frenzy, rain, sound, stats and save export/import
1389231 Add upgrade icon buttons, the toast panel and the stats/save panel
35e21ba Add the MultiMesh cookie rain with click crumbs, and the golden cookie view
683fa06 Add synthesized sound effects (port of Boomstick's Sfx) with a mute switch
f06f2f2 Save version 2 with C1 migration, export/import strings, upgrade and achievement tables
```

## Sizes (release, nothreads, `scripts/export-web.sh`, export 12-13 s)

| file | bytes | gzip -9 | vs C1 |
| --- | ---: | ---: | ---: |
| index.wasm | 33,744,846 | 10,319,369 | same |
| index.pck | 8,281,132 | 2,467,543 | +115,740 (the new C# code) |
| index.js | 327,582 | 79,885 | same |
| total folder | 42.4 MB | | |

## Verification

- **Headless desktop**:
  - `--selftest` passes with exit code 0. New checks: the C1 v1 JSON migrates to v2 with all
    fields kept, the export/import round trip (including whitespace and line breaks in the
    paste), import rejecting empty, non-base64 and non-save input and a future version while
    accepting a C1 export, and the tables (unique ids, sorted costs).
  - `--fresh --autoplay` prints `AUTOPLAY PASS` for 9 checks:
    - click-1 doubles the click.
    - cursor-1 doubles cursor cps.
    - Lucky gives cookies, and frenzy gives exactly x7 cps.
    - 5 achievements unlock.
    - Export, reset, a bad import (rejected), then a good import restores everything.
  - Loading a hand-written C1 save file logs `[save] migrated v1 -> v2`, shows the offline popup,
    awards 3 achievements from the old totals, and the exit save is written as v2.
  - Logs: `logs/step7dC2-desktop-{selftest,autoplay,c1load}.log`.
- **Desktop windowed**: 1280x720 and 450x900 with `--golden=0.3 --rain`. Screenshots:
  `logs/step7dC2-desktop-{landscape,portrait}.png`.
- **Web**: `logs/step7dC2-webtest.mjs` (Edge headless over CDP, GPU), log `logs/step7dC2-web.log`.
  **Result: 42/42 PASS, `SMOKE SUMMARY exceptions=0 errors=0 warnings=0 failures=0`.**
  - Every C1 check still passes: self test, 130 clicks, buying, idle income, the IDB sync
    (28-35 ms), reload, offline popup, Esc, new tab, browser restart, portrait taps and the
    drag scroll.
  - **Upgrades**: clicking the click-1 icon opens the info panel, and Buy doubles the click
    value (1 -> 2). One click then gave +2.38 (2 plus 0.38 of idle income). cursor-1 raised cps
    from 1.1 to 1.2.
  - **Toast**: the "Achievement unlocked / Enhancer" toast was visible right after the first
    upgrade (`-toast.png`).
  - **Golden cookie** (`?golden=2`): it appeared within 4 s (`-golden.png`).
    - Click 1 was lucky: +14.4 cookies.
    - Click 2 was a frenzy: cps 1.2 -> 8.4 (exactly x7), and the buff showed "FRENZY x7 1:17",
      then 1:16 after 1.5 s (`-frenzy.png`).
    - The "Golden touch" achievement unlocked.
  - **Particles** (`?rain`): 297-300 falling cookies on screen, plus crumbs from 30 clicks
    (`-rain.png`). Numbers are in the perf table below.
  - **Stats, export, reset, import**:
    - The Stats button opens the panel (`-stats.png`).
    - Export put 540 base64 characters in the box, and the clipboard held exactly that string.
    - Reset through the confirm dialog cleared everything.
    - Paste with clipboard read denied fell back to `prompt()`. Answering "this is not a save!"
      and pressing Load showed "That is not a save string (not base64)."
    - Paste through the prompt with the real string, and Paste with clipboard permission via
      `readText`, both filled the box.
    - Load restored the state: cookies 290.5, owned 1,1, 2 upgrades, 5 achievements, "Save
      loaded." (`-imported.png`).
  - **C1-format save**: a v1 JSON written to `localStorage` (Seq 999999) loads from
    localStorage and logs `migrated v1 -> v2`. It gave cookies 5000 plus offline earnings,
    owned 3,2, cps 2.3, no upgrades, and 4 achievements from the old totals.
  - **Portrait 412x915 @2.625, touch**: tapping an upgrade icon selects it and opens the info
    panel (`-portrait-upgrade.png`), and tapping Buy buys it. Tapping Stats opens the panel
    (`-portrait-stats.png`). Close works.
  - Screenshots: `logs/step7dC2-web-*.png`. Landscape is `-landscape.png` / `-rain.png`.
    Portrait is `-portrait*.png`.

## Performance (web, Edge headless with GPU, from `[perf]` and a 3 s rAF count)

| | rAF fps (worst frame) | C# _Process ms/f | cpu frame ms | render submit ms | draw calls |
| --- | --- | --- | --- | --- | --- |
| landscape, normal (about 25 particles) | 60.0-60.2 (16.8-17.8 ms) | 0.16-0.18 | 2.4-2.7 | 2.1-2.4 | 100-103 |
| landscape, `?rain`: 300 falling + crumbs | 60.0-60.1 (16.9-17 ms) | 0.27-0.30 | 2.45-2.9 | 2.1-2.5 | 98-102 |
| portrait 412x915, stats panel closed | 58.8-59.0 | 0.19 | 2.3-2.4 | 1.9-2.0 | 112-121 |
| stats panel open (landscape / portrait) | 60 | 0.24 | 2.5-2.7 | 2.1-2.3 | 118-161 |

- 300 particles cost about **+0.12 ms/frame of C#** (moving, compacting and filling the buffer,
  on the Mono interpreter). The one MultiMesh adds **one draw call**, and render submit does not
  change.
- GC: 1 gen0 collection during the whole run, managed heap 5.6 MiB.
- Draw calls grew from about 90 (C1) to about 100. The upgrade row, header buff and toast add
  style boxes. The stats panel adds about 20 (landscape) to 60 (portrait), from the
  achievement rows.

## Clipboard findings (web)

1. **Writing works from a Godot button.** Godot queues DOM input and runs `Button.Pressed`
   in the next requestAnimationFrame. That is still inside the click's transient user
   activation (about 5 s in Chromium), so `navigator.clipboard.writeText` is allowed **without
   any permission grant**. The test read the string back afterwards with permission.
2. **The engine's `DisplayServer.ClipboardSet` also works on the web** (tested with
   `?clipengine`). It calls the same `writeText`, but it **never reports failure**: a rejection
   only logs `console.error` "Setting OS clipboard is only possible from an input callback".
   So the game uses its own JS on the web. It runs `writeText`, falls back to
   `execCommand('copy')` on a hidden textarea, and polls the result. That gives a "Copied" or
   "blocked, copy the text below" status. The string is always in the TextEdit as well.
3. **Reading through the engine is not usable as is.**
   - `DisplayServer.ClipboardGet()` returns a cached string and starts an asynchronous
     `readText()` that only updates the cache for the *next* call.
   - Ctrl+V cannot help either: the engine calls `preventDefault()` on every keydown, so the
     browser never fires the `paste` event that would refresh the cache.
   - The Paste button therefore calls `navigator.clipboard.readText()` itself. When that is
     refused (`NotAllowedError` in the test without permission; Firefox and Safari also show
     their own paste prompt), it falls back to `window.prompt()`, which always works and
     opens the on-screen keyboard on phones.
4. **TextEdit on phones**: the export preset has `html/experimental_virtual_keyboard=false`, so
   a Godot TextEdit cannot get the phone keyboard. The `prompt()` fallback covers import on
   phones. Selecting text in the TextEdit to copy it by hand on a phone is not practical, so
   on phones export relies on `writeText` working. That is likely, but it needs a check on a
   real phone over HTTPS: the clipboard API needs a secure context, and localhost counts as one.

## Engine and runtime bugs or quirks found

- No crashes, managed exceptions, console errors or warnings in the web run. `MultiMeshInstance2D`
  with `Transform2D` and colours, `MultiMesh.Buffer`, `AudioStreamWAV` synthesis, `TextEdit`,
  `_HasPoint` overrides, `JavaScriptBridge` promises and polling, and `prompt()` all work in the
  release nothreads build.
- **The web mouse wheel uses the last mousemove position**, not the wheel event's own
  coordinates. A wheel with no mousemove before it (CDP, possibly some touchpads or
  synthetic events) scrolls whatever is under the old pointer. Real browsers send mousemove
  first, so this only mattered for the test, which now sends a mousemove before each wheel.
- **Clipboard** (above): `ClipboardSet` swallows its errors, and `ClipboardGet` is one call
  stale and never refreshed by Ctrl+V, because keydown is always `preventDefault`ed.
- These are game-side or test-side issues, not engine bugs:
  - Test hooks published from the 10 Hz tick can carry stale positions for a control that has
    just been shown, because containers sort later in the frame. The test waits one more tick.
  - The page's own `pagehide` save overwrites anything a test writes to `localStorage` before
    navigating away. The test writes the C1 save from a static page on the same origin.
- `QuadMesh` in 2D draws the texture upside down (3D UVs). `Size = (1, -1)` flips it back.

## Notes

- After loading a C1 save, the stored copy stays v1 until the next save (autosave, focus out,
  pagehide, buying a mute toggle, etc.). This is expected: it is migrated again on every load.
- Sound was only checked as "synthesized and unlocked by the first gesture" (logs). Nobody
  listened to it in this run.
- Housekeeping: to stop the test web server I ran `taskkill /F /IM python.exe`. It ended two
  python.exe processes: my `http.server` and one other. If another session had a Python
  process running at about that time, it was killed and needs restarting.

## Suggestions for C3 (phone polish, title screen, GitHub Pages)

- **Deploy**: copy Boomstick's `scripts/deploy-pages.sh`. HTTPS is required for
  `navigator.clipboard` on real phones. Then test on Android Chrome and iOS Safari:
  - export with `writeText`, import with `prompt()`;
  - the localStorage mirror across tab discard and iOS `pagehide`;
  - audio unlocking on the first tap.
- **Title screen**: a "Tap to start" overlay. This is also the natural first gesture, so audio
  is unlocked before the first crunch. Add the version and a Continue/New choice when a save
  exists.
- **Phone polish**:
  - The stats panel in portrait is long. Consider tabs (Stats / Achievements / Save), or an
    achievements grid of icons with a tap-to-inspect panel like the upgrades, which also cuts
    the 60 extra draw calls.
  - Remove the blank gap under the TextEdit (the empty status label with autowrap).
  - Move the toast below the notch on phones.
  - Let the golden cookie avoid the store's scrollbar column.
- **Visible feedback**: show a "+N" floater at the golden cookie's position for Lucky, and add
  a small shine or particle on buy.
- **Performance**: the rain is cheap (+0.12 ms for 300). The main cost is still style-box
  draw calls (about 100). Merge the header and buff boxes, and draw upgrade icon frames in one
  `_Draw`.
- **Balance**: more building tiers (25/50 for the later buildings), a few more click upgrades,
  and a "cps x2 for 10 min" kind of buff.
