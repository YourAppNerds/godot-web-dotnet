# godot-web-dotnet

Work log, test projects and helper scripts for getting Godot 4 .NET (C#) projects to export and run on the Web.

The engine changes are on the `web-dotnet-rebased` branch of [YAN-kyle/godot](https://github.com/YAN-kyle/godot/tree/web-dotnet-rebased). That branch is [godotengine/godot#106125](https://github.com/godotengine/godot/pull/106125) ("[.NET] Add web export support" by raulsntos) rebased onto current `master`, plus fixes. Related proposal: [godotengine/godot-proposals#13076](https://github.com/godotengine/godot-proposals/issues/13076).

## Contents

- `HANDOFF.md`: plan, status, decisions and open problems.
- `logs/step*-report.md`: one report per step, with measurements and the rationale for each fix commit.
- `samples/`: small Godot C# projects used for testing.
  - `HelloWeb`: prints from `_Ready`.
  - `HelloWebVisual`: label and frame counter driven from C#.
  - `WebRuntimeTest`: checks for exceptions, timers, crypto, time zones, globalization, interop and engine calls.
  - `WebBench`: interpreter micro-benchmarks.
- `scripts/`: build, export and size-analysis helpers.

## Status

The C# web templates build with Emscripten 6.0.12 (threads on and off, debug and release). The samples run in Chromium-based browsers. See `HANDOFF.md` for details and known gaps.

## Caveats

- Paths in the scripts and in each sample's `nuget.config` point at this author's machine (`D:\source\YAN-kyle\godot-web-dotnet`). Change them to your own checkout. The samples need the GodotSharp nupkgs built from the branch above.
- This is experimental work, not an official Godot release.
