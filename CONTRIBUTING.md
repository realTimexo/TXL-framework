# Contributing to TXL

Thanks for helping improve TXL. Issues and pull requests are welcome for the compiler, generated WPF runtime, VS Code extension, documentation, examples, and website.

## Before opening a pull request

- Search existing issues and releases first. For a bug report, include a minimal project (`info.json` plus `.txl` source), the exact command, the output, and your .NET SDK version.
- For language proposals, show the syntax you want to write, how it should behave, and at least one invalid example with the expected diagnostic. Keep new syntax consistent with the line-oriented grammar and its block delimiters.
- Keep documentation and examples in English, and mark unsupported or Windows-only behavior clearly.
- Use the compiler's existing style and avoid adding dependencies unless they materially simplify maintenance.

## Build and test

From the repository root with a .NET 9 SDK:

```powershell
dotnet build .\TXL.slnx
dotnet run --project .\Tests\Txl.Tests\Txl.Tests.csproj
dotnet run --project .\Compiler\Txl.Cli -- --check .\Examples\AllFeatures
```

The AllFeatures project includes a small local MP4 and repository-owned TXL logo for asset-path tests. A sandbox/Linux build cannot verify WPF rendering, keyboard routing, Windows dialogs, or media codec playback. If a change affects one of these behaviors, state which Windows versions/codecs you tested; otherwise say that Windows validation remains outstanding.

For VS Code extension changes, validate the JSON files and JavaScript, then package the VSIX from `VSCodeExtension` with `npm ci` and `npx vsce package --no-dependencies`.

## Pull requests

Make one focused pull request per coherent change. Explain the user-facing behavior, link its issue if there is one, list relevant commands/tests, and include screenshots only for a visible UI change that has been checked on Windows. Keep release notes in `CHANGELOG.md`; do not create a public GitHub release as a substitute for a code review.

By contributing, you agree that your contributions are distributed under this repository's MIT License.
