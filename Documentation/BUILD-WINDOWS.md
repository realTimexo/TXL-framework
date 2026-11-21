# Install TXL and build a Windows app

TXL compiles `.txl` source into native WPF desktop apps for Windows x64. The compiler can check TXL syntax on other operating systems, but only Windows can run and visually validate the generated UI.

## Install the global `txl` command

The recommended installer is a per-user .NET global tool. It does not need administrator privileges and adds the tool folder to your user PATH. After the `v0.6.0` GitHub Release is available, open PowerShell and run:

```powershell
$installer = Join-Path $env:TEMP 'install-txl.ps1'
Invoke-WebRequest 'https://raw.githubusercontent.com/realTimexo/TXL-framework/main/Installer/install.ps1' -OutFile $installer
powershell -NoProfile -ExecutionPolicy Bypass -File $installer
```

The script checks for the .NET 9 SDK and, if it is missing, installs the SDK into `%USERPROFILE%\.dotnet`. It then downloads the latest `Txl.Cli` NuGet package attached to the TXL GitHub Release, installs or updates the .NET global tool, and adds `%USERPROFILE%\.dotnet\tools` to the user PATH. Open a new terminal after installation. It downloads code from the repository's GitHub Releases and the official .NET installer endpoints.

“Global” here means available to your Windows user from any terminal folder; it is not a machine-wide installation and does not require administrator access. A system-wide machine install would require a separate elevated installer. The generated app is self-contained and does not require TXL on the target PC. The developer machine still needs the .NET 9 SDK to build projects.

You can also install the CLI from a local NuGet package with `dotnet tool install --global Txl.Cli --add-source <folder-containing-nupkg>`. To update, use `dotnet tool update --global Txl.Cli --add-source <folder-containing-nupkg>`.

## Build a project from anywhere

Change to a TXL project folder (it contains `info.json` and the source file named by its `start` field), then run:

```powershell
cd 'C:\Projects\MyTXLApp'
txl --version
txl --check
txl --build
```

You can pass a path from any current directory instead:

```powershell
txl --check 'C:\Projects\MyTXLApp'
txl --build 'C:\Projects\MyTXLApp'
```

The executable is written under `MyTXLApp\dist\<safe-app-name>\<safe-app-name>.exe`. When your app uses images, videos, or other content files, distribute the entire folder, including its `content` subfolder. The EXE itself is self-contained, but local media assets are copied alongside it.

The `txl` command also accepts `--help`, `--version`, `--check`, `--preview`, `--build`, `check`, `preview`, `build`, and `run`. `preview` opens the native WPF window on Windows without producing the standalone EXE. It is a launched native window, not an embedded VS Code preview. `check` validates `info.json` and TXL source without publishing.

## Use the project source and SDK directly

To build from a cloned repository without installing the global tool, install the .NET 9 SDK and run these commands from the repository root:

```powershell
dotnet --version
dotnet build .\TXL.slnx
dotnet run --project .\Tests\Txl.Tests\Txl.Tests.csproj
dotnet run --project .\Compiler\Txl.Cli -- --check .\Examples\AllFeatures
dotnet run --project .\Compiler\Txl.Cli -- --build .\Examples\AllFeatures
```

The repository's `global.json` pins SDK `9.0.200` with a .NET 9 feature-band roll-forward policy. The .NET 9 SDK may need internet access to restore WPF reference packs and NuGet dependencies. Keep project assets in their project folder and use project-relative paths such as `assets/logo.png`; the compiler copies those assets into the generated content folder.

## Troubleshooting

- If PowerShell says `txl` is not recognized, open a new terminal and check that `%USERPROFILE%\.dotnet\tools` is on your user PATH. Restart VS Code after installing if its terminal cannot find the command.
- If the installer says a GitHub release or `Txl.Cli.<version>.nupkg` is missing, the release has not yet been published or the package asset was omitted. Use the latest release page at `https://github.com/realTimexo/TXL-framework/releases`.
- If the SDK cannot be found, run `dotnet --list-sdks`. Building requires a .NET 9 SDK; downloading only the runtime is not enough.
- If package restore or a Windows Desktop reference fails, connect to the internet and run `txl --build` again.
- TXL diagnostics include the source file, line, column, source excerpt, and a focused hint when available. Fix the first reported syntax error and run `txl --check` again.
- If a generated app is missing an image or video, distribute the full output folder and confirm the file path is relative to the TXL project root.
- WPF media formats and remote streams depend on Windows codecs and networking. Validate them on the Windows versions you plan to support.
