# Step 8a notes: two standalone upstream fixes

Fact sheets only, not PR text. Godot's contribution rules forbid AI-authored code, AI-written
human-to-human text, and AI agents submitting PRs (see "Blocker" below).

## Rebased branches (local only, not pushed)

Base: upstream/master c8256a8ece (2026-10-23). Both cherry-picks applied clean. Not built or tested on master.

| Branch | Worktree | Commit | Source |
|---|---|---|---|
| dotnet-build-server-hang | godot-wt/dotnet-build-server-hang | b3a389f8a7 | 6c7a5edb7f |
| dotnet-missing-method-bind | godot-wt/dotnet-missing-method-bind | c928609745 | 63caa36557 |

## Blocker: Godot AI policy

From https://contributing.godotengine.org/en/latest/development/contribution_rules.html and the PR template:
- "AI should not be used to author code." Code completion is allowed.
- "Do not use AI-generated text in human-to-human communication."
- "Do not allow an AI agent to submit PRs on your behalf."
- Any AI use must be disclosed, with a description of how it was used.
- "Maintainers are not obliged to review AI-assisted PRs."

Both commits were written by Claude. Submitting them, or PR text drafted by Claude, breaks these rules.

## Fix 1: export hang (build servers)

- Existing issue: godotengine/godot#110101 "Godot console hangs forever when exporting project due to dotnet build server" (open). Same root cause we found.
- Maintainer position (raulsntos, 2026-09-24): does not want to shut down or disable the build server, because it speeds up later compiles and .NET 11 leans further into it. Suggests the server should not be a child of the Godot process, which is likely a dotnet CLI change.
- Our commit disables servers (`-nodeReuse:false`, `-p:UseSharedCompilation=false`) for all publishes and for headless builds. Publishes from the interactive editor lose the warm server, which conflicts with the maintainer's position.
- Narrower option: disable only in headless mode. Argument: under the Windows console wrapper, a server started by a headless run cannot outlive it without holding the console, so it gives no reuse there. On Linux/macOS (no job object), CI runs can reuse servers, so a narrower option may limit it to Windows.
- Other option: wrapper-side change in platform/windows/console_wrapper_windows.cpp (waits for JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO). Related to our open failure 12.
- Our evidence: export finished in seconds, command returned about 10 minutes later. Intermittent because an already-running server outside the job is reused.

## Fix 2: one missing method bind kills a whole class

- Existing issues: #103315 (open; disable_3d template, TypeInitializationException), #95933 and #103774 (closed; same exception).
- Maintainer positions: raulsntos (#95933): glue must match the build configuration; regenerate glue with an editor built with the same options. akien-mga (#103315): disable_3d is advanced; if 3D nodes are used and the export fails, the error "should at least make it clear where you need to modify your code".
- Gap in "regenerate glue": the editor cannot be built with disable_3d or rendering_device=no, so matching glue is not possible for those options. Web always has rendering_device=no, so C# web templates (PR #106125) always hit this (open failure 16: RenderingServer..cctor fails on get_video_adapter_type).
- Our change: lookup returns null instead of throwing; each generated icall checks the bind and throws NativeMethodBindNotFoundException only when the missing method is called. Matches godot-cpp behavior.
- Weak point: the new exception message has no method name (icalls are shared per signature). The method shows in the stack trace instead. A reviewer may want the name in the message.
- Cost: one inline compare per icall plus a no-inline throw helper. Not benchmarked.
- Our evidence: web template WRT 43/0 debug and release nothreads, incl. 4 RenderingServer checks (step7e-report.md). Not tested on desktop disable_3d.
