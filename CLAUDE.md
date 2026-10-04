# CLAUDE.md

## Communication
Respond to the user in **French** during sessions. This file itself is in English for clarity/consistency.
Keep responses short. Do not add end-of-task summaries unless explicitly requested.

## Project
C# solution targeting **.NET 10**. All code, comments, console/UI output, commit messages and documentation are in **English**.

- `src/VcvPatchDiagram.Core`: all logic (read .vcv, catalog, analysis, layout, exporters). No file I/O, so it runs in Blazor WASM.
  Don't use `System.Formats.Tar` there (PlatformNotSupported in the browser): `Patch/TarEntries.cs` exists for that reason.
- `src/VcvPatchDiagram.Cli`: `vcvdiagram` console app (inspect, render, catalog build).
- `src/VcvPatchDiagram.Web`: standalone Blazor WebAssembly UI, no server.
- `catalog/`: `ports.json` is generated (`make catalog`), `ports.overrides.json` and `roles.json` are hand-written.

## Development workflow
Use unit tests (xUnit, `tests/VcvPatchDiagram.Tests`) to drive development: write/update a test for new behavior or a bug before or alongside the fix, then run `dotnet test`. Don't consider a change done until the test suite passes.
Exporter output is pinned by golden files in `tests/golden`: after an intended change, `UPDATE_GOLDEN=1 dotnet test`, then review the diff.

Follow `.editorconfig`. Run `dotnet format` before considering a change done (fixes encoding/line-endings/indentation and naming). Note: `dotnet format` does **not** catch the `:silent`-severity rules (no `var` — always use explicit types; prefer expression-bodied members when the body is a single statement) — these must be respected manually when writing code.

Never commit or push without an explicit request.
