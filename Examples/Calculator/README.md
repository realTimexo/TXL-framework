# TXL Calculator

A compact calculator example demonstrating persistent runtime state, input binding, and arithmetic actions.

## Run or build

From the repository root, validate with `dotnet run --project Compiler/Txl.Cli -- check Examples/Calculator` and build with `dotnet run --project Compiler/Txl.Cli -- build Examples/Calculator`. To launch the published WPF app, use Windows. You can also run `build.ps1` from this directory on Windows.

The two number fields bind to `left` and `right`; operation buttons calculate into `result`. Invalid numbers and division by zero display a helpful message rather than crashing. The bound values are saved locally with the app's runtime state.

The UI has been cross-published from Linux; visual interaction has not yet been validated on Windows.
