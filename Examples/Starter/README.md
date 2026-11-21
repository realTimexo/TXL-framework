# TXL Starter — compile this example

This folder is a complete TXL project. It contains the required `info.json`, the app source `main.txl`, and `assets/app.ico`, which is selected as the Windows program icon by the `icon` field. The declared app version is `1.0.1`.

## Windows quick start

1. Use the installed .NET SDK 9.0.200. No .NET 10 installation is needed.
2. Extract the complete `TXL-Compile-Kit` folder, keeping the `Compiler` and `Examples` folders together.
3. Open PowerShell in this `Starter` folder.
4. Run:

   ```powershell
   .\build.ps1
   ```

The script validates the project, then compiles a self-contained Windows x64 WPF app. The output will be `dist\TXL_Starter\TXL_Starter.exe`. The compiled UI is embedded: the executable does not need the TXL source or `info.json` beside it. The sample's logo is copied to the adjacent `content` directory, so distribute the whole output folder.

If PowerShell blocks scripts, invoke it for this process only with `powershell -ExecutionPolicy Bypass -File .\build.ps1`.

## Manual commands

From the kit root, run:

```powershell
dotnet run --project .\Compiler\Txl.Cli\Txl.Cli.csproj -- check .\Examples\Starter
dotnet run --project .\Compiler\Txl.Cli\Txl.Cli.csproj -- preview .\Examples\Starter
dotnet run --project .\Compiler\Txl.Cli\Txl.Cli.csproj -- build .\Examples\Starter
```

`preview` starts a native WPF window on Windows without publishing a standalone EXE. The compiler targets .NET 9 and the generated application targets Windows x64. Building may need internet access on the first run to restore the .NET Windows Desktop reference packs. The produced app itself is self-contained.

## Try the app

- **Show a message** demonstrates `alert(...)`.
- **Open example website** demonstrates the safe HTTP(S)-only `open(...)` action.
- The intro and preferences blocks demonstrate nested TXL layout and native WPF text/input/checkbox/separator controls.
- The `actionLink` class shows hover styling and an HTTP(S) click action.
- The final section demonstrates build-time variable substitution, a conditional and three repeated rows. These are generated at compile time, not live mutable state.

Edit `main.txl` and run `build.ps1` again to test your changes.
