# Build the TXL Starter on Windows with .NET SDK 9.0.200

This guide uses the SDK version already selected by the kit, `9.0.200`. You do not need to install .NET 10. The ZIP contains the matching compiler, sample project, and build script.

## 1. Download and extract the kit

Download `TXL-Compile-Kit.zip`, then in File Explorer right-click it and choose **Extract All…**. Keep the folder structure intact. The extracted kit should contain:

```text
TXL-Compile-Kit\
  global.json
  Compiler\Txl.Cli\Txl.Cli.csproj
  Examples\Starter\info.json
  Examples\Starter\main.txl
  Examples\Starter\build.ps1
```

Do not copy out only the `Starter` folder; the kit also needs `Compiler`.

## 2. Check the installed SDK

Open PowerShell or Windows Terminal and run:

```powershell
dotnet --list-sdks
dotnet --version
```

The list must include `9.0.200`. If `dotnet` is not recognized, restart PowerShell. If it is still unavailable, check that the SDK is installed and `C:\Program Files\dotnet` is on `PATH`.

## 3. Open the example folder

In File Explorer, open the extracted `TXL-Compile-Kit\Examples\Starter` directory. Copy its path from the address bar, then change to it in PowerShell. For example:

```powershell
cd "$HOME\Downloads\TXL-Compile-Kit\Examples\Starter"
Get-ChildItem
dotnet --version
```

You should see `info.json`, `main.txl`, `build.ps1`, and `README.md`. The SDK version should be `9.0.200`; the `global.json` file in the kit root selects it for subdirectories.

## 4. Build the application

Run the supplied script:

```powershell
.\build.ps1
```

The script first checks the TXL project and then compiles a native Windows WPF application. On the first run, `dotnet` may need internet access to restore the Windows Desktop reference packs. Wait for the `Build complete` message.

If PowerShell blocks the script, temporarily allow scripts for this PowerShell process only:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\build.ps1
```

This setting ends when you close the PowerShell window. Alternatively, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

## 5. Launch the EXE

After a successful build, the executable is at:

```text
dist\TXL_Starter\TXL_Starter.exe
```

Launch it from PowerShell with:

```powershell
.\dist\TXL_Starter\TXL_Starter.exe
```

You can also double-click it in File Explorer. The published app is self-contained, so the target computer does not need TXL installed. The compiled interface is embedded in the EXE; source files and project metadata are not shipped. Project assets such as images remain in the `content` subfolder. If your app uses assets, distribute the entire `dist\TXL_Starter` folder, not only the EXE. The compressed single-file app bundles native WPF libraries and does not produce a PDB. Windows file/product version metadata comes from `info.json` → `version`.

## 6. Edit and rebuild

Open `main.txl` in VS Code or another text editor. The sample demonstrates styles, groups, vertical and horizontal layout, controls, buttons, class-based click/hover behavior, build-time variables, conditions, and repeats. Save your changes and rebuild from the Starter directory:

```powershell
.\build.ps1
```

Close a running `TXL_Starter.exe` before rebuilding if Windows prevents the output file from being replaced.

## Optional: run the CLI directly

From the kit root, run:

```powershell
dotnet run --project .\Compiler\Txl.Cli\Txl.Cli.csproj -- check .\Examples\Starter
dotnet run --project .\Compiler\Txl.Cli\Txl.Cli.csproj -- preview .\Examples\Starter
dotnet run --project .\Compiler\Txl.Cli\Txl.Cli.csproj -- build .\Examples\Starter
```

`check` validates the metadata and TXL syntax without creating an executable. `preview` launches a native WPF window on Windows without publishing a standalone EXE. `build` publishes the self-contained executable.

## Troubleshooting

- **`A compatible .NET SDK was not found`**: check `dotnet --list-sdks`. The kit requires SDK `9.0.200`; open a new PowerShell after installing or updating an SDK.
- **NuGet/restore error or missing Windows Desktop references**: connect to the internet and rerun ` .\build.ps1` from the Starter folder.
- **`info.json` not found**: you are probably not in `Examples\Starter`. `Get-ChildItem` should show `info.json` and `main.txl`.
- **TXL syntax error**: open `main.txl` and check the reported file and line. Groups start with `group = name (` and styles with `style (`; standalone `)` closes each block.
- **The app does not launch on another device**: this sample targets Windows x64. Other operating systems or CPU architectures need a different compiler target.

The preview and final native WPF app require Windows for launch and visual testing; cross-publishing on Linux is not a GUI test.
