# TXL Todo List

A simple local task list demonstrating a bound input, persistent collection, and native checkbox/remove controls.

## Run or build

From the repository root, validate with `dotnet run --project Compiler/Txl.Cli -- check Examples/TodoList` and build with `dotnet run --project Compiler/Txl.Cli -- build Examples/TodoList`. To launch and interact with the native WPF app, use Windows. Alternatively, run `build.ps1` from this directory on Windows.

Enter a task and select **Add task**. Each task can be checked or removed. The collection is saved as JSON under `%LocalAppData%\TXL\TXL Todo List` and restored on the next launch. This is a demonstration of the compiler's built-in todo-list control, not a general-purpose reactive list API.

The UI has been cross-published from Linux; visual interaction and persistence across Windows restarts still require Windows validation.
