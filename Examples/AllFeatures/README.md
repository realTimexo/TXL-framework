# TXL All Features Showcase

This project demonstrates TXL syntax and runtime behavior across `main.txl`, `business.txl`, and the imported `modules/text.txl`: the `window (` starter block, style catalogs, group layout/alignment, rounded buttons, strokes and shadows, five headline sizes, inline HTML, placeholder and prefilled inputs, bound checkboxes, runtime state and local persistence, click/hover/keyboard actions, `mailto:` links, compiled multi-page navigation, calculator operations, native prompt/confirm/success dialogs, a persistent todo list, parameterized components, compile-time `for` iteration, x/y Canvas placement, compile-time `set`/`if`/`else`/`repeat`, custom functions, JSON array/object operations, confined file access, HTTP requests, timers, and SQLite.

## Validate and run

From the repository root:

```powershell
dotnet run --project .\Compiler\Txl.Cli -- check .\Examples\AllFeatures
dotnet run --project .\Compiler\Txl.Cli -- preview .\Examples\AllFeatures
```

To publish the Windows x64 executable, use `dotnet run --project .\Compiler\Txl.Cli -- build .\Examples\AllFeatures` or run `build.ps1` from this folder. Launching and visually checking WPF requires Windows. Direct HTTPS TTF/OTF style fonts are downloaded and packaged during build; this example avoids a remote font dependency so it can be built offline after NuGet packages are restored.

## Limits

WPF layout is not responsive. HTML uses Microsoft Edge WebView2 for modern browser rendering; the WebView2 Runtime must be installed, and only embed trusted content. YouTube iframe embeds require network access. Videos depend on Windows codecs. Gradients, dark/light theme switching, custom modal forms, loading overlays, reactive iteration, embedded live-preview diagnostics, component inspection, and hot reload are not included. Compile-time loops and components are not runtime-reactive. See [the grammar guide](../../Documentation/GRAMMAR.md) for syntax and current limits.
