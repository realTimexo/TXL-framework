using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Txl;

internal static class CliUpdater
{
    private const string ReleaseApi = "https://api.github.com/repos/realTimexo/TXL-framework/releases/latest";
    private const string ReleaseAssetPrefix = "https://github.com/realTimexo/TXL-framework/releases/download/";
    private const long MaximumPackageSize = 25 * 1024 * 1024;

    public static async Task<int> RunAsync()
    {
        var currentVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0";

        Console.WriteLine($"Checking the latest TXL release (installed version: {currentVersion})...");
        var temporaryFolder = Path.Combine(Path.GetTempPath(), "txl-update-" + Guid.NewGuid().ToString("N"));

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TXL-CLI-Updater/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var releaseResponse = await http.GetAsync(ReleaseApi);
            releaseResponse.EnsureSuccessStatusCode();
            using var releaseDocument = JsonDocument.Parse(await releaseResponse.Content.ReadAsStringAsync());
            var release = releaseDocument.RootElement;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var version = tag.StartsWith('v') ? tag[1..] : tag;
            if (!Regex.IsMatch(version, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant))
            {
                Console.Error.WriteLine($"The latest release tag '{tag}' is not a stable three-part version. No update was installed.");
                return 1;
            }

            if (Version.TryParse(currentVersion, out var current) && Version.TryParse(version, out var latest) && latest <= current)
            {
                Console.WriteLine(latest == current
                    ? $"TXL {version} is already the latest published version."
                    : $"This TXL build ({currentVersion}) is newer than the latest stable release ({version}); it was not downgraded.");
                return 0;
            }

            var expectedPackage = $"Txl.Cli.{version}.nupkg";
            var assets = release.GetProperty("assets");
            var asset = assets.EnumerateArray().FirstOrDefault(item =>
                string.Equals(item.GetProperty("name").GetString(), expectedPackage, StringComparison.Ordinal));
            if (asset.ValueKind == JsonValueKind.Undefined)
            {
                Console.Error.WriteLine($"Release {tag} does not contain {expectedPackage}; no update was installed.");
                Console.Error.WriteLine("Check the release assets at https://github.com/realTimexo/TXL-framework/releases.");
                return 1;
            }

            var assetUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!assetUrl.StartsWith(ReleaseAssetPrefix + tag + "/", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("The latest release returned an unexpected package URL; no update was installed.");
                return 1;
            }

            var expectedDigest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
            if (expectedDigest is null || !Regex.IsMatch(expectedDigest, @"^sha256:[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            {
                Console.Error.WriteLine("The latest release does not publish a valid SHA-256 package digest; no update was installed.");
                return 1;
            }

            var declaredSize = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : 0;
            if (declaredSize is <= 0 or > MaximumPackageSize)
            {
                Console.Error.WriteLine("The latest TXL package has an invalid or unexpectedly large size; no update was installed.");
                return 1;
            }

            Directory.CreateDirectory(temporaryFolder);
            var packagePath = Path.Combine(temporaryFolder, expectedPackage);
            using (var packageResponse = await http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                packageResponse.EnsureSuccessStatusCode();
                if (packageResponse.Content.Headers.ContentLength is long downloadedSize && downloadedSize != declaredSize)
                {
                    Console.Error.WriteLine("The downloaded package size did not match the GitHub release metadata; no update was installed.");
                    return 1;
                }

                await using var input = await packageResponse.Content.ReadAsStreamAsync();
                await using var output = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await input.CopyToAsync(output);
                if (output.Length != declaredSize)
                {
                    Console.Error.WriteLine("The downloaded package was incomplete; no update was installed.");
                    return 1;
                }
            }

            await using (var packageFile = File.OpenRead(packagePath))
            {
                var actualDigest = "sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(packageFile)).ToLowerInvariant();
                if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("The downloaded package failed its SHA-256 integrity check; no update was installed.");
                    return 1;
                }
            }

            Console.WriteLine($"Downloading TXL {version} from its official GitHub release...");
            var update = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
            {
                "tool", "update", "--global", "Txl.Cli", "--version", version,
                "--add-source", temporaryFolder, "--ignore-failed-sources"
            }) update.ArgumentList.Add(argument);

            using var process = Process.Start(update);
            if (process is null)
            {
                Console.Error.WriteLine("Could not start dotnet. Confirm the .NET SDK is installed and dotnet is on PATH.");
                return 1;
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Console.Write(await standardOutput);
            var errorText = await standardError;
            if (!string.IsNullOrWhiteSpace(errorText)) Console.Error.Write(errorText);
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("TXL could not update the global tool. If it is not installed yet, run the official Windows installer from the repository README.");
                return process.ExitCode;
            }

            Console.WriteLine($"TXL updated to {version}. Run 'txl --version' to verify.");
            return 0;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or IOException or InvalidOperationException or TaskCanceledException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"TXL could not check or install the latest release: {error.Message}");
            Console.Error.WriteLine("Check your internet connection, then retry 'txl --update'. You can also use the installer at https://github.com/realTimexo/TXL-framework/blob/main/Documentation/BUILD-WINDOWS.md.");
            return 1;
        }
        finally
        {
            if (Directory.Exists(temporaryFolder))
                Directory.Delete(temporaryFolder, recursive: true);
        }
    }
}
