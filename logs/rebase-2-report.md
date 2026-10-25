# Rebase 2 report: web-dotnet-rebased-2 verification

Branch `web-dotnet-rebased-2`, worktree `godot-wt/rebase-2`, HEAD 554a1e420f. Not pushed. Date 2026-10-24.

## Rebase

- Base: upstream/master c8256a8ece (was 06d1e7a34c for `web-dotnet-rebased`); 24 new upstream commits (incl. merges).
- 34 commits, same count as `web-dotnet-rebased` (c81d539c9b..11875322e9).
- `git range-diff 06d1e7a34c..web-dotnet-rebased upstream/master..HEAD`: all 34 pairs are `=` (patch-identical).
  So the rebase had **no conflicts and no fixes**; only hashes changed (e.g. 11875322e9 -> 554a1e420f).
- No build fix was needed; no new commits on rebase-2. Worktree clean.

## Builds (all exit 0)

| Item | Log |
|---|---|
| Editor (windows, mono, d3d12=no), `--version` 4.8.beta.mono.custom_build.554a1e420 | logs/rebase2-editor-build.log (15m54s) |
| Glue regenerated | logs/rebase2-glue.log |
| Assemblies + nupkgs (GodotSharp, GodotSharpEditor, Godot.SourceGenerators, Godot.NET.Sdk 4.8.0-beta) | logs/rebase2-assemblies.log |
| Web templates nothreads debug/release (Emscripten 6.0.12) | logs/rebase2-build-web_nothreads_{debug,release}.log |
| Web templates threads debug/release (bonus, built by the earlier agent) | logs/rebase2-build-web_{debug,release}.log |

Nupkgs were pushed to an isolated folder (earlier agent's scratchpad
`C:\Users\Kyle\AppData\Local\Temp\claude\C--Users-Kyle\38332d55-998c-4315-98e7-4bc60360da09\scratchpad\nupkgs2`)
with a private NUGET_PACKAGES cache (`...\scratchpad\nugetcache`). The shared `nupkgs/` folder and `~/.nuget/packages`
were **not** changed. Samples were tested from copies in `...\scratchpad\samples` (identical sources; nuget.config
points at nupkgs2).

## Templates install / restore

- Backup of installed templates (step 7e, 11875322e9): `%APPDATA%\Godot\export_templates_backup\4.8.beta.mono-2026-10-24-pre-rebase2\`
  (4 zips + version.txt + SHA256SUMS).
- Installed all 4 rebase-2 zips into `%APPDATA%\Godot\export_templates\4.8.beta.mono\`, ran the tests below with
  `custom_template` blank (installed templates, as in step 7e).
- **Restored** the step 7e templates afterwards; `sha256sum -c` OK for all 4. Installed state = before this run.
  The backup folder was left in place.

## Verification (Edge headless, nothreads, installed templates)

Runtime banner in all runs: v4.8.beta.mono.custom_build.554a1e420.

| Run | Result | step 7e baseline |
|---|---|---|
| WebRuntimeTest debug, invariant (GODOT_WRT_INVARIANT=1, as 7e) | WRT DONE pass=43 fail=0 | 43/0 |
| WebRuntimeTest release, invariant | WRT DONE pass=43 fail=0 | 43/0 |
| WebRuntimeTest debug, ICU (sample default) | WRT DONE pass=47 fail=0 | - (4 culture checks pass instead of INFO) |
| WebRuntimeTest release, ICU | WRT DONE pass=47 fail=0 | - |
| HelloWeb debug | "Hello from C# _Ready" | same |
| HelloWeb release | "Hello from C# _Ready" | same |

Invariant runs report the same set of check names/outcomes as step 7e (diff empty). [error] line count is the same
(110, the expected GD.PushWarning/PushError checks). All exports rc=0.

Logs: logs/rebase2-export-*.log, logs/rebase2-run-{wrt-nt-debug,wrt-nt-release,wrt-nt-inv-debug,wrt-nt-inv-release,hw-debug,hw-release}.log.

## Size deltas (template zips, step 7e -> rebase-2)

| Template | zip | wasm | js |
|---|---|---|---|
| web_nothreads_debug | 10,530,137 -> 10,537,323 (+7.2 KB) | 34,215,353 -> 34,231,746 (+16.4 KB) | 408,099 (=) |
| web_nothreads_release | 10,569,242 -> 10,572,944 (+3.7 KB) | 33,744,846 -> 33,760,993 (+16.1 KB) | 327,582 (=) |
| web_debug (threads) | 10,631,932 -> 10,638,504 (+6.6 KB) | 34,592,693 -> 34,609,078 (+16.4 KB) | 457,947 (=) |
| web_release (threads) | 10,609,480 -> 10,615,409 (+5.9 KB) | 33,572,196 -> 33,588,339 (+16.1 KB) | 366,250 (=) |

~+16 KB wasm everywhere comes from the 24 upstream commits (our patches are identical). Export pck sizes this run:
WRT invariant 7.86 MB, WRT ICU 9.48 MB, HelloWeb 7.71 MB.

## Regressions

None found. Not run: threads templates (same as 7e), WebBench, HelloWebVisual, games.
