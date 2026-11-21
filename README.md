# TXL — Timexo Language

TXL is a small, line-oriented language for building native Windows desktop interfaces with WPF. This repository contains the .NET 9 compiler and CLI, generated-app runtime, regression tests, examples, and a Visual Studio Code extension. It does not generate a web application.

## Requirements

- .NET SDK **9.0.200** (pinned in `global.json`).
- Windows x64 to run or visually validate generated WPF applications. Linux can cross-publish them; initial publishing may download Windows Desktop reference packs.
- Node.js/npm only when rebuilding the VS Code extension package.

## Build and test the compiler

```powershell
dotnet --version
dotnet build .\TXL.slnx
dotnet run --project .\Tests\Txl.Tests\Txl.Tests.csproj
dotnet run --project .\Compiler\Txl.Cli -- check .\Examples\Hello
dotnet run --project .\Compiler\Txl.Cli -- build .\Examples\Hello
```

The compiler publishes a self-contained, single-file `win-x64` executable into the sample's `dist` folder. The Windows UI must be run on Windows for visual acceptance. `check` validates project metadata and TXL syntax without publishing.

## A small TXL project

A project directory contains `info.json` and a source file (normally `main.txl`). Example:

```txl
style (
  main:
    font = Arial
    background = #20202A
    color = #F4F7FB
  button:
    background = #0EA5E9
    color = #FFFFFF
    weight = bold
    padding = 10
)

group = welcome (
  layout = vertical
  text(hl) = Hello from TXL
  text = A native Windows desktop app
  button = Show a message
  action = success("TXL is running")
)
```

`style (` and `group = name (` open parenthesized blocks; a standalone `)` closes one. See [the grammar and runtime guide](Documentation/GRAMMAR.md).

## Runtime state and examples

The compiler supports persistent string state, input and checkbox binding, basic arithmetic actions, and a persistent todo collection. State is stored per application as JSON under `%LocalAppData%\TXL\<AppName>`. The state catalog supplies initial values; runtime changes are persisted.

- [Hello](Examples/Hello): minimal app.
- [Starter](Examples/Starter): broader control and language showcase.
- [Calculator](Examples/Calculator): two bound numeric inputs and add/subtract/multiply/divide actions.
- [TodoList](Examples/TodoList): add tasks, tick them complete, remove them, and retain the list across launches.

Build a sample on Windows from its directory with `powershell -ExecutionPolicy Bypass -File .\build.ps1`, or run `dotnet run --project .\Compiler\Txl.Cli -- build .\Examples\Calculator` from the repository root.

## CLI and VS Code

```text
txl check <project>
txl preview <project>
txl build <project>
txl run <project>
```

`preview` generates and launches a native WPF window on Windows; it does not publish an EXE and is not an embedded VS Code webview. The extension contributes **TXL: Preview Active Project** to the Command Palette and invokes the CLI. The CLI must be available as `txl` on `PATH` for that command. The extension includes syntax highlighting, snippets, completion, hover information, formatting, and the transparent TXL icon. To show the TXL icon beside `.txl` files in Explorer, choose **File Icon Theme → TXL File Icons**; this is an explicit VS Code setting.

To package the extension, run `npm install` then `npx vsce package --no-dependencies --allow-missing-repository` in `VSCodeExtension`.

## Current scope and limitations

See [the grammar guide](Documentation/GRAMMAR.md) for supported syntax. `for` blocks and reusable components expand at compile time; they are not reactive templates. `if`, `repeat`, and `set` are also compile-time constructs. Runtime dialogs are simple native message/input/confirmation dialogs, not custom modal forms or loading overlays. Theme switching, gradients, shadows, border/radius rendering, spacing tokens, a component inspector, live error overlay, and hot reload are not implemented. Arbitrary `x`/`y` coordinates are supported through absolute Canvas layout, but responsive constraints are not. Google Fonts names in completion are suggestions only; fonts are neither downloaded nor bundled. Windows visual behavior still requires testing on Windows.
