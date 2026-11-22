<div align="center">
  <img src="VSCodeExtension/icons/txl-language.png" alt="TXL language icon" width="108" height="108">
  <h1>TXL · Timexo Language</h1>
  <p><strong>A small language for building native Windows desktop apps.</strong><br>Write readable TXL. Compile it into a self-contained WPF application.</p>
  <p>
    <a href="https://github.com/realTimexo/TXL-framework/releases"><img src="https://img.shields.io/github/v/release/realTimexo/TXL-framework?display_name=tag&label=latest%20release" alt="Latest GitHub release"></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2f855a.svg" alt="MIT license"></a>
    <img src="https://img.shields.io/badge/.NET-9.0-512BD4.svg" alt="Requires .NET 9 SDK to build">
    <a href="https://github.com/realTimexo/TXL-framework/actions/workflows/ci.yml"><img src="https://github.com/realTimexo/TXL-framework/actions/workflows/ci.yml/badge.svg" alt="CI workflow"></a>
  </p>
  <p><a href="https://txl.wuaze.com/">Website</a> · <a href="Documentation/GRAMMAR.md">Language guide</a> · <a href="Documentation/BUILD-WINDOWS.md">Install and build</a> · <a href="CHANGELOG.md">Changelog</a> · <a href="CONTRIBUTING.md">Contribute</a></p>
</div>

TXL is a line-oriented language and compiler for native Windows desktop interfaces. The compiler emits WPF applications for .NET 9; it does **not** generate a web app. This repository includes the compiler, runtime template, VS Code extension, examples, documentation, and an installable `txl` CLI.

## Install and build

The Windows installer sets up the .NET 9 SDK in your user profile when needed, installs the latest TXL compiler as a per-user .NET global tool, and adds the tool directory to your user PATH. It does not require administrator rights. “Global” means available to your Windows user from any terminal, not installed machine-wide.

```powershell
$installer = Join-Path $env:TEMP 'install-txl.ps1'
Invoke-WebRequest 'https://raw.githubusercontent.com/realTimexo/TXL-framework/main/Installer/install.ps1' -OutFile $installer
powershell -NoProfile -ExecutionPolicy Bypass -File $installer
```

Open a new terminal, change to a TXL project folder, then run:

```powershell
txl --version
txl --update  # update the global compiler to the latest GitHub release
txl --check
txl --build
```

Pass a project path when you are elsewhere: `txl --build C:\Projects\MyApp`. The app is written to `dist\<AppName>\<AppName>.exe`. If the app uses images or videos, distribute that entire output folder, including `content`. The compiler needs the .NET 9 SDK to build; the generated app is self-contained and does not need TXL installed on the target PC.

The installer downloads the latest compiler package from [GitHub Releases](https://github.com/realTimexo/TXL-framework/releases). The CLI and installer require a published release package; see [Releases and versioning](#releases-and-versioning) for the release process.

## A TXL sample

```txl
window (
  title: Reading List
  size: 1100x760
  mode: normal
)

style (
  main:
    font = Segoe UI
    background = #172033
    color = #F4F7FB
  button:
    background = #1677D2
    color = white
    padding = 10
)

actions (
  hello(click):
    alert = Hello from TXL.
)

group = home (
  layout = vertical
  text.headline = What are you reading?
  input = Add a title
  button(hello) = Show a native message
)
```

An input's assigned text is its placeholder; it is not submitted until the user types. See the [TXL grammar guide](Documentation/GRAMMAR.md) for exact syntax, examples, and current runtime limits.

## Examples and editor support

- [`Examples/Hello`](Examples/Hello) — a minimal project.
- [`Examples/Starter`](Examples/Starter) — a small starter layout.
- [`Examples/Calculator`](Examples/Calculator) — add, subtract, multiply, and divide with bound runtime state.
- [`Examples/TodoList`](Examples/TodoList) — add, check, remove, and persist tasks locally.
- [`Examples/AllFeatures`](Examples/AllFeatures) — a documented sample covering current syntax, dialogs, state, images, position, videos, and Enter-key actions. Its small local MP4 makes the media example portable.

The VS Code extension provides highlighting, snippets, context-aware completions, hover help, formatting, a TXL file-icon theme, and **TXL: Preview Active Project**. The preview opens a native WPF window on Windows; it is not an embedded editor view. Install the `.vsix` from the release assets using **Extensions: Install from VSIX...**, then choose **TXL File Icons** in **File → Preferences → File Icon Theme** if you want the custom file icon theme.

## Build from source

Requirements: .NET SDK 9.0.200 or an allowed .NET 9 feature-band SDK. Windows is required to run and visually test WPF apps. Linux can check TXL syntax and cross-publish Windows apps when the SDK downloads the Windows reference packs.

```powershell
dotnet --version
dotnet build .\TXL.slnx
dotnet run --project .\Tests\Txl.Tests\Txl.Tests.csproj
dotnet run --project .\Compiler\Txl.Cli -- --check .\Examples\AllFeatures
dotnet run --project .\Compiler\Txl.Cli -- --build .\Examples\AllFeatures
```

The lightweight regression harness is also run by the GitHub Actions CI workflow.

## Current scope

TXL keeps the language intentionally small. `set`, `if`, `repeat`, `for`, and components expand during compilation. `state` persists simple strings and the built-in todo list. Current class events are click, hover, and basic keydown filtering. Native message boxes are simple alert, confirmation, success, and text-prompt dialogs. Media playback uses WPF and relies on local Windows codecs for format support.

There is no hot reload, embedded VS Code preview, component inspector, inline preview diagnostics, responsive layout system, or custom modal form builder. Gradients, shadows, theme switching, and visual border/radius styling are not currently implemented. The compiler reports syntax diagnostics in the terminal; UI appearance, keyboard routing, and codec behavior still need Windows testing. The [language guide](Documentation/GRAMMAR.md) distinguishes implemented features from limitations.

## Releases and versioning

The [GitHub Releases page](https://github.com/realTimexo/TXL-framework/releases) is the source for published compiler packages, VS Code `.vsix` files, and Windows sample builds. Release notes are maintained in [`CHANGELOG.md`](CHANGELOG.md). A tagged `v*` release runs the Windows release workflow, builds the CLI NuGet tool, packages the VS Code extension, compiles the AllFeatures example, and attaches those artifacts to the GitHub Release. Releases are not created merely by changing the changelog.

## Contributing

TXL is open source under the MIT license. Bug reports, documentation fixes, language proposals, examples, and code contributions are welcome. Start with [`CONTRIBUTING.md`](CONTRIBUTING.md) for the small-project workflow, expectations for syntax changes, and test commands. If you find a problem, open a GitHub issue with a minimal `.txl` example and the exact diagnostic or behavior you saw.
