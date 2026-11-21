using System.Diagnostics;
using System.Reflection;
using Txl;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "--help";
if (command is "help" or "--help" or "-h") { Help(); return 0; }
if (command is "version" or "--version" or "-v")
{
    var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0";
    Console.WriteLine($"TXL {version.Split('+')[0]} (Timexo Language)");
    return 0;
}
command = command switch { "--build" => "build", "--check" => "check", "--preview" => "preview", "--run" => "run", _ => command };
if (command is not ("check" or "build" or "run" or "preview"))
{
    Console.Error.WriteLine($"Unknown command: {args[0]}. Run 'txl --help' to see supported commands.");
    return 2;
}
var projectArgument = args.Skip(1).FirstOrDefault(value => !value.StartsWith("-", StringComparison.Ordinal)) ?? ".";
var project = Path.GetFullPath(projectArgument);
if (!Directory.Exists(project))
{
    Console.Error.WriteLine($"TXL project folder not found: {project}\nCreate/open a project folder or pass its path, for example: txl --build ./MyApp");
    return 2;
}

var validation = ProjectValidator.Validate(project);
if (!PrintDiagnostics(validation.Diagnostics, project)) return 1;
var info = validation.Info!;
var source = Path.Combine(project, info.Start);
var parsed = Parser.Parse(File.ReadAllText(source), source);
if (!PrintDiagnostics(parsed.Diagnostics, project)) return 1;
if (WindowCatalog.Settings.TryGetValue("icon", out var windowIcon) && !File.Exists(Path.Combine(project, windowIcon)))
{
    PrintDiagnostics(new List<Diagnostic> { new(source, 1, 1, Severity.Error, $"Window icon '{windowIcon}' does not exist. Add the icon file under the project folder or change the path in the windows block.") }, project);
    return 1;
}
if (command == "check")
{
    Console.WriteLine($"TXL project valid: {info.Name} ({parsed.Program!.Nodes.Count} top-level nodes)");
    return 0;
}

var output = Path.Combine(project, "dist");
var safeName = SafeName(info.Name);
var exePath = Path.Combine(output, safeName, safeName + ".exe");
var generated = Path.Combine(project, ".txl-generated");
try
{
    Console.WriteLine("[1/3] Resolving TXL project and assets...");
    var files = Directory.GetFiles(project, "*", SearchOption.AllDirectories).Where(file =>
    {
        var relative = Path.GetRelativePath(project, file).Replace('\\', '/');
        var parts = relative.Split('/');
        var name = Path.GetFileName(file);
        return !parts.Any(part => part is "dist" or ".txl-generated" or ".git" or "bin" or "obj")
            && !Path.GetExtension(file).Equals(".txl", StringComparison.OrdinalIgnoreCase)
            && name is not ("info.json" or "README.md" or "build.ps1" or "global.json");
    }).ToArray();
    Directory.CreateDirectory(generated);
    foreach (var file in files)
    {
        var relative = Path.GetRelativePath(project, file);
        var destination = Path.Combine(generated, "content", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(file, destination, true);
    }
    var bundledIcon = Path.Combine(AppContext.BaseDirectory, "Assets", "txl.ico");
    var contentIcon = Path.Combine(generated, "content", "txl.ico");
    Directory.CreateDirectory(Path.GetDirectoryName(contentIcon)!);
    if (File.Exists(bundledIcon)) File.Copy(bundledIcon, contentIcon, true);

    File.WriteAllText(Path.Combine(generated, "Runtime.cs"), WpfEmitter.Emit(info, parsed.Program!, project));
    File.WriteAllText(Path.Combine(generated, "GeneratedApp.csproj"), WpfEmitter.ProjectFile(info));
    if (command == "preview")
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("TXL preview launches a native WPF window, so it can run only on Windows. Use 'txl check' here, or build for Windows with 'txl --build'.");
            return 1;
        }
        var preview = new ProcessStartInfo("dotnet") { WorkingDirectory = generated, UseShellExecute = false };
        preview.ArgumentList.Add("run"); preview.ArgumentList.Add("--project"); preview.ArgumentList.Add("GeneratedApp.csproj");
        using var app = Process.Start(preview)!;
        await app.WaitForExitAsync();
        return app.ExitCode;
    }

    Console.WriteLine("[2/3] Publishing self-contained Windows application (win-x64)...");
    var publish = new ProcessStartInfo("dotnet") { WorkingDirectory = generated, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var arg in new[] { "publish", "GeneratedApp.csproj", "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true", "-p:DebugSymbols=false", "-p:DebugType=None", "-o", Path.GetDirectoryName(exePath)! }) publish.ArgumentList.Add(arg);
    using var process = Process.Start(publish)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    Console.Write(await stdout);
    Console.Error.Write(await stderr);
    if (process.ExitCode != 0)
    {
        Console.Error.WriteLine("TXL could not publish the generated WPF project. Check that the .NET SDK is installed and that package restore completed successfully.");
        return process.ExitCode;
    }
    Console.WriteLine($"[3/3] Build complete: {exePath}");
    if (command == "run")
    {
        if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        else Console.WriteLine("The app has been built; native WPF apps can be launched only on Windows.");
    }
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"TXL build failed: {error.Message}");
    if (error is System.ComponentModel.Win32Exception) Console.Error.WriteLine("Install the .NET 9 SDK and ensure 'dotnet' is available on PATH.");
    return 1;
}

static string SafeName(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

static bool PrintDiagnostics(IEnumerable<Diagnostic> diagnostics, string project)
{
    var list = diagnostics.ToList();
    if (list.Count == 0) return true;
    var errors = list.Count(d => d.Severity == Severity.Error);
    var warnings = list.Count(d => d.Severity == Severity.Warning);
    Console.Error.WriteLine($"TXL found {errors} error(s) and {warnings} warning(s):");
    foreach (var diagnostic in list)
    {
        var level = diagnostic.Severity.ToString().ToLowerInvariant();
        var code = diagnostic.Severity == Severity.Error ? "TXL100" : diagnostic.Severity == Severity.Warning ? "TXL200" : "TXL300";
        var file = Path.IsPathRooted(diagnostic.File) ? Path.GetRelativePath(project, diagnostic.File) : diagnostic.File;
        Console.Error.WriteLine($"  {level} {code}  {file}:{diagnostic.Line}:{diagnostic.Column}  {diagnostic.Message}");
        var fullPath = Path.IsPathRooted(diagnostic.File) ? diagnostic.File : Path.Combine(project, diagnostic.File);
        if (File.Exists(fullPath))
        {
            var lines = File.ReadAllLines(fullPath);
            if (diagnostic.Line > 0 && diagnostic.Line <= lines.Length)
            {
                var sourceLine = lines[diagnostic.Line - 1];
                Console.Error.WriteLine($"       {diagnostic.Line,4} | {sourceLine}");
                Console.Error.WriteLine($"            | {new string(' ', Math.Max(0, diagnostic.Column - 1))}^");
            }
        }
        var hint = diagnostic.Message switch
        {
            var message when message.Contains("Unknown command", StringComparison.OrdinalIgnoreCase) => "Check spelling; available elements include text, button, image, video, input, checkbox, list, line, and space.",
            var message when message.Contains("Cannot parse TXL statement", StringComparison.OrdinalIgnoreCase) => "TXL uses one statement per line. Elements use 'text = Hello'; blocks use 'group = name (' and close with ')'.",
            var message when message.Contains("Missing closing", StringComparison.OrdinalIgnoreCase) => "Add a standalone ')' to close the open block. Each style, actions, windows, and group block needs its own closer.",
            var message when message.Contains("escapes project root", StringComparison.OrdinalIgnoreCase) => "Use an asset path relative to the project, such as assets/photo.png; do not use '..' to leave the project folder.",
            var message when message.Contains("info.json", StringComparison.OrdinalIgnoreCase) => "Check the project folder for info.json and verify its required fields and JSON syntax.",
            _ => null
        };
        if (hint is not null) Console.Error.WriteLine("       fix: " + hint);
    }
    Console.Error.WriteLine("Fix the reported source line(s), then run 'txl --check' before building.");
    return errors == 0;
}

static void Help()
{
    Console.WriteLine("TXL — Timexo Language\nUsage: txl <command> [project]\n\nCommands (project defaults to the current folder):\n  txl --build [project]    Build a self-contained Windows x64 app\n  txl --check [project]    Check project metadata and TXL syntax\n  txl --preview [project] Run a native WPF preview (Windows only)\n  txl --version            Print installed compiler version\n  txl --help               Show this help\n\nAliases: txl build|check|preview|run [project]\n\nRequirements: .NET 9 SDK for building; Windows is required to run or visually test WPF apps.");
}
