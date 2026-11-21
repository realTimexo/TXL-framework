# TXL All Features Showcase

This project demonstrates the TXL features currently supported by the compiler and runtime in one source file: style catalogs, groups and layouts, placeholder and prefilled inputs, bound checkboxes, runtime state and local persistence, class-based click/hover actions, calculator operations, native prompt/confirm/success dialogs, the persistent todo-list control, parameterized components, compile-time `for` iteration, x/y Canvas placement, and the earlier compile-time `set`/`if`/`repeat` constructs.

## Validate and run

From the repository root:

```powershell
dotnet run --project .\Compiler\Txl.Cli -- check .\Examples\AllFeatures
dotnet run --project .\Compiler\Txl.Cli -- preview .\Examples\AllFeatures
```

To publish the Windows x64 executable, use `dotnet run --project .\Compiler\Txl.Cli -- build .\Examples\AllFeatures` or run `build.ps1` from this folder. Launching and visually checking WPF requires Windows.

Some requested ideas remain unsupported: gradients, shadows, border/radius, dark/light theme switching, custom modal forms, loading overlays, reactive component iteration, embedded live preview diagnostics, inspector, and hot reload. Compile-time loops/components are not runtime-reactive. See [the grammar guide](../../Documentation/GRAMMAR.md) for precise limits.
