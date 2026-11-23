using System.Diagnostics;
using System.Reflection;
using Txl;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "--help";
if (command is "help" or "--help" or "-h") { Help(); return 0; }
if (command is "update" or "--update") return await CliUpdater.RunAsync();
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
var parsed = Parser.Parse(File.ReadAllText(source), info.Start.Replace('\\', '/'));
var pagePrograms = new Dictionary<string, ProgramAst>(StringComparer.OrdinalIgnoreCase) { [info.Start.Replace('\\', '/')] = parsed.Program ?? new ProgramAst(new List<Node>()) };
var pageDiagnostics = new List<Diagnostic>(parsed.Diagnostics);
foreach (var pageFile in Directory.GetFiles(project, "*.txl", SearchOption.AllDirectories).Where(file => !Path.GetRelativePath(project, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "dist" or ".txl-generated" or ".git" or "bin" or "obj")))
{
    var relative = Path.GetRelativePath(project, pageFile).Replace('\\', '/');
    if (relative.Equals(info.Start.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;
    var page = Parser.ParseAdditionalPage(File.ReadAllText(pageFile), relative);
    pageDiagnostics.AddRange(page.Diagnostics);
    if (page.Program is not null) pagePrograms[relative] = page.Program;
}
if (!PrintDiagnostics(pageDiagnostics, project)) return 1;
if (WindowCatalog.Settings.TryGetValue("icon", out var windowIcon) && !File.Exists(Path.Combine(project, windowIcon)))
{
    PrintDiagnostics(new List<Diagnostic> { new(source, 1, 1, Severity.Error, $"Window icon '{windowIcon}' does not exist. Add the icon file under the project folder or change the path in the window block.") }, project);
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

    await PackageRemoteFontsAsync(generated);
    File.WriteAllText(Path.Combine(generated, "Runtime.cs"), WpfEmitter.Emit(info, parsed.Program!, project, pagePrograms));
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

static async Task PackageRemoteFontsAsync(string generated)
{
    const long maxBytes = 20 * 1024 * 1024;
    var fontsDirectory = Path.Combine(generated, "content", "fonts");
    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    var packaged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var style in StyleCatalog.Styles.Values)
    {
        if (!style.TryGetValue("font", out var font) || !font.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
        if (!packaged.TryGetValue(font, out var local))
        {
            var uri = new Uri(font, UriKind.Absolute);
            await EnsurePublicHttpsHostAsync(uri);
            HttpResponseMessage? response = null;
            for (var redirects = 0; redirects <= 3; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var location = response.Headers.Location ?? throw new InvalidOperationException($"Remote font redirect from '{font}' did not include a destination.");
                    var next = location.IsAbsoluteUri ? location : new Uri(uri, location);
                    response.Dispose();
                    if (redirects == 3) throw new InvalidOperationException($"Remote font '{font}' redirected too many times.");
                    await EnsurePublicHttpsHostAsync(next);
                    uri = next;
                    continue;
                }
                break;
            }
            using (response ?? throw new InvalidOperationException($"Could not download remote font '{font}'."))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long length && length > maxBytes) throw new InvalidOperationException($"Remote font '{font}' exceeds the 20 MiB download limit.");
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var buffer = new MemoryStream();
                var chunk = new byte[64 * 1024];
                while (true)
                {
                    var read = await stream.ReadAsync(chunk);
                    if (read == 0) break;
                    if (buffer.Length + read > maxBytes) throw new InvalidOperationException($"Remote font '{font}' exceeds the 20 MiB download limit.");
                    buffer.Write(chunk, 0, read);
                }
                var bytes = buffer.ToArray();
                if (bytes.Length < 12 || !(bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0, 1, 0, 0 }) || bytes.AsSpan(0, 4).SequenceEqual("OTTO"u8) || bytes.AsSpan(0, 4).SequenceEqual("true"u8)))
                    throw new InvalidOperationException($"Remote file '{font}' is not a supported TrueType/OpenType font.");
                var extension = Path.GetExtension(new Uri(font).AbsolutePath).ToLowerInvariant();
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(font)))[..16].ToLowerInvariant();
                var fileName = hash + extension;
                Directory.CreateDirectory(fontsDirectory);
                await File.WriteAllBytesAsync(Path.Combine(fontsDirectory, fileName), bytes);
                var family = FontFamilyFromUrl(new Uri(font));
                local = $"content/fonts/{fileName}#{family}";
                packaged[font] = local;
            }
        }
        style["font"] = local;
    }
}

static string FontFamilyFromUrl(Uri uri)
{
    var stem = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
    var ignored = new HashSet<string>(new[] { "regular", "normal", "medium", "bold", "italic", "light", "thin", "black", "semibold", "extra", "variable" }, StringComparer.OrdinalIgnoreCase);
    var words = stem.Split(new[] { '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(word => !ignored.Contains(word)).ToArray();
    if (words.Length == 0) throw new InvalidOperationException($"Cannot infer a font family name from '{uri}'. Use a font URL with a descriptive filename.");
    return string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}

static async Task EnsurePublicHttpsHostAsync(Uri uri)
{
    if (uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("Remote font URLs and redirects must use HTTPS on the standard port without embedded credentials.");
    var addresses = System.Net.IPAddress.TryParse(uri.Host, out var literal) ? new[] { literal } : await System.Net.Dns.GetHostAddressesAsync(uri.Host);
    if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress)) throw new InvalidOperationException($"Remote font host '{uri.Host}' resolves to a non-public network address; refusing the download.");
}

static bool IsNonPublicAddress(System.Net.IPAddress address)
{
    if (System.Net.IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
    var b = address.GetAddressBytes();
    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        return b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127);
    return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && ((b[0] & 0xFE) == 0xFC || address.IsIPv4MappedToIPv6 && IsNonPublicAddress(address.MapToIPv4()));
}

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
            var message when message.Contains("Missing closing", StringComparison.OrdinalIgnoreCase) => "Add a standalone ')' to close the open block. Each style, actions, window, and group block needs its own closer.",
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
    Console.WriteLine("TXL — Timexo Language\nUsage: txl <command> [project]\n\nCommands (project defaults to the current folder):\n  txl --build [project]    Build a self-contained Windows x64 app\n  txl --check [project]    Check project metadata and TXL syntax\n  txl --preview [project]  Run a native WPF preview (Windows only)\n  txl --update             Install the latest published TXL CLI release\n  txl --version            Print installed compiler version\n  txl --help               Show this help\n\nAliases: txl build|check|preview|run|update [project]\n\nRequirements: .NET 9 SDK for building; internet access is required for --update; Windows is required to run or visually test WPF apps.");
}
