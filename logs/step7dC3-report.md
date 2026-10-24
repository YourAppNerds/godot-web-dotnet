# Step 7d C3: Cookie Crunch, title screen, phone polish, release prep

Repo: `D:\source\repos\godot-web-dotnet\games\cookie-crunch`, branch `main`. Started from a clean
`037cbb1`. Nothing was pushed. No GitHub changes and no engine changes. Engine: the same
`godot-wt/rebase` editor and nothreads release Web template as C1/C2.

**Local gh-pages branch: `0feec48531b44e05e9c6c1db25b1207cdcbb0e24`** ("Deploy Cookie Crunch web build
from 3148890"). It is an orphan branch with one commit. It holds the tested build plus an empty `.nojekyll` and
`.gitattributes` (`* -text`). The repo has **no remote yet**: add `origin` before running
`git -C games/cookie-crunch push origin gh-pages` (and `main`), then point Pages at `gh-pages` (root).

## What was built

- **Title screen** (`src/TitleScreen.cs`, all drawn in code):
  - It shows a turning cookie with rays and a wavy gold "COOKIE CRUNCH". The title is one line
    in landscape and two in portrait. Under it are "an idle bakery", a blinking "Tap to start" or
    "Click to start" (chosen by `IsTouchscreenAvailable`), and `v1.0.0` (from the new
    `application/config/version`).
  - A tap, a click (on release), Enter or Space anywhere starts the game. That first gesture
    unlocks web audio, and the start plays the upgrade jingle.
  - **With a save**, it shows a 320x100 **Continue** button, a summary ("4,979 cookies, 5
    buildings") and "or tap anywhere".
  - No golden cookies spawn while the title is up. Esc cannot close the offline popup hidden
    behind it.
  - `?notitle` / `--notitle` skips it.
  - On short landscape phones the subtitle gives way to the summary, and the title size is also
    limited by the screen height.
- **Safe area**:
  - The page uses `viewport-fit=cover`.
  - `window.ccSafeArea()` (in the head include) reads `env(safe-area-inset-*)` through a
    hidden probe div and returns `top,right,bottom,left,canvasCssWidth`.
  - `Game` reads it on every resize and polls it once a second. It turns the insets into a safe
    rectangle in viewport units.
  - Header, cookie, store, stats panel, toasts, title and golden-cookie spawns stay inside that
    rectangle. Backgrounds and falling cookies fill the whole screen.
  - `?safe=T,R,B,L` fakes insets for tests.
- **Page settings** (`export_presets.cfg`):
  - Viewport: `viewport-fit=cover`, scale locked between 1 and 1, `user-scalable=no`.
  - Apple web-app metas, with a black-translucent status bar.
  - `html`/`body` are fixed with overflow hidden, `overscroll-behavior: none` (no
    pull-to-refresh) and `touch-action: none`.
  - No text selection, long-press callout, tap highlight or text-size-adjust.
  - The kept JS blocks touchmove, gesturestart, dblclick and contextmenu.
  - The loading screen is brown instead of #242424, and the boot splash background matches.
  - No fullscreen (skipped as asked).
  - Both orientations are supported: `handheld/orientation=6` (sensor), nothing is locked, and
    the layout reflows.
- **Hit targets**:
  - Buttons are at least 80 px: the x1/x10/Max/Stats bar, upgrade Buy, modal buttons, stats
    Close, Sound, Reset, Export, Paste and Load.
  - Store rows are 96 px and upgrade icons 84 px.
  - At 412x915 the stretch scale is 0.572, so 80 px is about 46 CSS px.
- **No hover-only UI**: store rows now show "+N/s each" next to the price; before, this was only
  in a tooltip. Upgrades already had a tap-to-open panel.
- **C2 suggestions fixed**:
  - The blank gap under the save TextEdit is gone (the status label is hidden while empty).
  - The toast stays below the notch through the safe area.
  - Golden cookies keep 64 px clear of the store scrollbar on the right.
  - A Lucky golden cookie shows a "+N" floater where it was.
  - Not done (not cheap): stats tabs, an achievement icon grid, and merging the style boxes.
- **Sound levels**: balanced from measured numbers, and checked in the self test (no clipping,
  played rms50 spread under 6 dB, peak at most -3 dBFS).
- **Release prep**:
  - `scripts/deploy-pages.sh`, adapted from Boomstick's: it exports, commits to a local
    gh-pages branch through a temporary worktree, adds `.nojekyll`, and pushes only with
    `--push`. It also checks that each committed file matches `build/web` byte for byte.
  - README rewritten with the play link, controls, web notes (including the save and clipboard
    findings), build and deploy steps, and a note on the custom 4.8.0-beta packages from
    https://github.com/YourAppNerds/godot-web-dotnet.
  - MIT `LICENSE`, with the copyright holder line from Boomstick ("Copyright (c) 2026 YourAppNerds").
  - `export-web.sh` no longer has an absolute editor path. It uses a workspace-relative default,
    then `godot` on PATH.

## Commits (main)

```
3148890 README for release: play link, controls, web notes, build and deploy steps
3472262 Add MIT license
03f93f9 export-web.sh: no absolute editor path, fall back to godot on PATH
c028a26 Store row rate on the price line; title fits short landscape phones
5bca1ae Add scripts/deploy-pages.sh (local gh-pages branch, push only with --push)
00be397 Web page: viewport-fit=cover, safe-area probe, no zoom/scroll/selection, brown loading screen
ac34b27 Title screen with Continue, safe-area layout and phone polish
9033e7e Balance sound levels from measured peak and RMS, check them in the self test
```

The secrets and paths check (`git grep`): the only absolute path left is the local nupkgs folder
in `nuget.config`, which the README explains. There are no tokens, keys, e-mail addresses or user paths.

## Sizes (release nothreads; also on gh-pages)

| file | bytes | gzip -9 |
| --- | ---: | ---: |
| index.wasm | 33,744,846 | 10,319,369 |
| index.pck | 8,307,448 | 2,470,862 |
| index.js | 327,582 | 79,885 |
| index.html | 8,253 | |
| total | 42,437,458 | |

The pck grew by 26 KB compared with C2 (the title screen, safe area and level-check code). The
export took about 12 s.

## Sound levels (16-bit buffers, 22,050 Hz; rms50 = loudest 50 ms RMS)

| sound | length | buffer peak | buffer rms50 | mix (was) | played peak | played rms50 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Crunch | 130 ms | -5.9 | -19.7 | -1 (-4) | -6.9 | -20.7 |
| Buy | 160 ms | -17.2 | -19.6 | -2 (-8) | -19.2 | -21.6 |
| Upgrade | 420 ms | -13.2 | -18.9 | -1 (-7) | -14.2 | -19.9 |
| GoldenAppear | 700 ms | -13.9 | -20.6 | 0 (-8) | -13.9 | -20.6 |
| GoldenClick | 550 ms | -11.7 | -17.0 | -1 (-5) | -12.7 | -18.0 |
| Achievement | 900 ms | -13.7 | -19.0 | 0 (-6) | -13.7 | -19.0 |

All values are dBFS. No sample clips.

- **Before**: the sounds played at -22 to -28.6 dBFS rms50, a spread of 6.6 dB. Buy and the
  golden-cookie appear sound were the quietest, so they were easy to miss on a phone speaker.
- **Now**: -18 to -21.6 dBFS, a spread of 3.6 dB. The frequent crunch and buy sit about 21 dB
  down, and the rarer rewards are 1-3 dB louder.
- **Headroom**: the loudest peak is -6.9 dBFS (crunch), with at most 4 crunch voices at once.
  4 uncorrelated voices add about 6 dB of RMS, so even fast tapping stays out of clipping.
- The web self test printed the same numbers.

## Verification

- **Desktop headless**:
  - `--selftest` passes with exit code 0, including the new sound level check
    (`step7dC3-desktop-selftest.log`).
  - `--fresh --autoplay` prints AUTOPLAY PASS (`step7dC3-desktop-autoplay.log`).
  - The build has 0 errors.
- **Desktop windowed** screenshots:
  - Title at 1280x720: `step7dC3-desktop-title.png` (new game) and `-title-continue.png`.
  - 450x900: `-title-portrait.png`, and the game with rain and golden cookies in `-portrait.png`.
- **Web**: `logs/step7dC3-webtest.mjs`, run against the extracted **gh-pages tree** served on
  127.0.0.1:8123. Before the run, curl `cmp` checked index.html, js, wasm and pck against
  `build/web`, and the deploy script's own check found the same.
  - **Result: 56/56 PASS, `SMOKE SUMMARY exceptions=0 errors=0 warnings=0 failures=0`**
    (`step7dC3-web.log`).
  - All C2 checks still pass, with every load now going through the title.
  - **New checks**:
    - Fresh start shows the title without Continue, and the safe probe answers.
    - A click starts a new game and logs `audio unlocked=True`. That click does not reach the
      game.
    - After a reload the title shows Continue, and the Continue button starts the game.
    - Portrait 412x915 touch: tapping the title starts it.
    - Portrait with `safe=47,0,34,0`: the store lies inside y 47..881 CSS px.
    - Landscape phone 915x412 touch with `safe=0,47,21,47`:
      - the title and the tap to start work;
      - the store's right edge is at 860 or less (limit 868) and its bottom at 383 or less
        (limit 391);
      - the cookie centre is at x 235 or more (it must be more than 47);
      - 10 cookie taps all count;
      - Stats opens with a tap.
    - Back to desktop 1280x720.
  - **Screenshots** (`step7dC3-web-*.png`):
    - `title`, `title-continue`, `landscape`, `landscape-final`;
    - `portrait-title`, `portrait`, `portrait-upgrade`, `portrait-stats`, `portrait-safe-title`,
      `portrait-safe`;
    - `phone-landscape-title`, `phone-landscape`, `phone-landscape-stats`;
    - plus the C2 set (toast, golden, frenzy, rain, stats, imported, offline-popup).
  - **Load time**: ready 3.1 s after navigation on a cold cache, then 0.6-1.0 s. The game was
    ready 140-213 ms after .NET started.

## Performance (web, Edge headless with GPU)

| | rAF fps | C# _Process ms/f | cpu frame ms | render ms | draw calls |
| --- | --- | --- | --- | --- | --- |
| landscape 1280x720 | 60 (worst 17 ms) | 0.19 | 2.66 | 2.31 | 97 |
| `?rain` 298 particles | 60.1 (worst 17 ms) | 0.32 | 2.66 | 2.21 | 95 |
| portrait 412x915 | 58.4 (perf) | 0.20 | 2.51 | 2.07 | 117 |
| landscape phone 915x412 | 57.4 (perf, 5 s after loading) | 0.26 | 3.15 | 2.49 | 98 |
| title screen up | 58-60 | 0.28-0.37 | 3.2-4.3 | 2.9-3.9 | 153 |

- The title screen costs about 55 draw calls (12 rays, plus one DrawString per letter with 2
  outlines) and about 0.1 ms of C#. It only runs until the first tap.
- GC: 1 gen0 collection, 5.6-5.9 MiB managed.

## Real-phone checklist (cannot be done headless)

1. **Android Chrome and iOS Safari** over HTTPS (Pages):
   - The title appears.
   - The first tap starts the game, and the crunch is audible right away.
   - Check the volume balance by ear.
2. **iPhone with a notch**:
   - Portrait and landscape: nothing under the notch or home bar.
   - Also add the game to the Home Screen (standalone mode, black-translucent status bar),
     where the insets are largest.
   - The `[safe] insets` console line shows the values.
3. Rotate the phone while playing: the layout should reflow within about 1 s, including the
   insets.
4. Fast tapping on the cookie must not zoom, select text, pop a callout or pull to refresh.
5. **Export**: `writeText` should report "Copied". **Import**: Paste should show the browser's
   paste prompt (iOS) or the `prompt()` fallback, then Load.
6. **Saves**: background the tab or kill the app, then reopen. The title should show Continue
   and the offline popup.

## Notes and open items

- In the portrait stats screenshot an achievement toast appears on top of the stats panel. This
  is cosmetic: toasts sit above every panel.
- The test uses `?stats`, so the performance overlay shows in the web screenshots. It is off for
  players.
- The test server was started by me and stopped by its own PIDs. No other processes were
  touched. The servers of other sessions on 8765 and 8766 were left running.
