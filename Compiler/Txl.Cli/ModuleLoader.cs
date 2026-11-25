using System.Text.RegularExpressions;

namespace Txl;

public static class ModuleLoader
{
    public static List<Diagnostic> LoadImports(string projectRoot, string entryFile)
        => LoadImports(projectRoot, entryFile, out _);

    public static List<Diagnostic> LoadImports(string projectRoot, string entryFile, out HashSet<string> importedFiles)
    {
        var root = Path.GetFullPath(projectRoot);
        var diagnostics = new List<Diagnostic>();
        var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var foundImports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        importedFiles = foundImports;
        Visit(entryFile, isEntry: true);
        return diagnostics;

        void Visit(string relativePath, bool isEntry)
        {
            var normalized = relativePath.Replace('\\', '/');
            if (!TryResolve(normalized, out var fullPath, out var error))
            {
                diagnostics.Add(new(relativePath, 1, 1, Severity.Error, error));
                return;
            }
            var key = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            if (loaded.Contains(key)) return;
            if (!visiting.Add(key))
            {
                diagnostics.Add(new(key, 1, 1, Severity.Error, $"Module import cycle detected at '{key}'."));
                return;
            }
            if (!File.Exists(fullPath))
            {
                diagnostics.Add(new(key, 1, 1, Severity.Error, $"Imported TXL module '{key}' was not found in the project."));
                visiting.Remove(key);
                return;
            }

            string text;
            try { text = File.ReadAllText(fullPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new(key, 1, 1, Severity.Error, $"Could not read TXL module '{key}': {exception.Message}"));
                visiting.Remove(key);
                return;
            }

            foreach (var (token, _) in Lexer.Lines(text, key, new List<Diagnostic>()))
            {
                var import = Regex.Match(token.Value.Trim(), @"^import\s*=\s*(.*)$", RegexOptions.IgnoreCase);
                if (!import.Success) continue;
                var importedPath = Unquote(import.Groups[1].Value.Trim()).Replace('\\', '/');
                Visit(importedPath, isEntry: false);
            }

            if (!isEntry)
            {
                foundImports.Add(key);
                var parsed = Parser.ParseAdditionalPage(text, key);
                diagnostics.AddRange(parsed.Diagnostics);
                if (parsed.Program is not null && parsed.Program.Nodes.Any(node => node is not ImportModule))
                    diagnostics.Add(new(key, 1, 1, Severity.Warning, "Imported modules are libraries: top-level UI and runtime statements are ignored. Put reusable functions, components, styles, and actions in the module."));
            }
            visiting.Remove(key);
            loaded.Add(key);
        }

        bool TryResolve(string relativePath, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)
                || !relativePath.EndsWith(".txl", StringComparison.OrdinalIgnoreCase)
                || relativePath.Split('/', '\\').Any(part => part is "" or "." or "..")
                || relativePath.Contains(':'))
            {
                error = "Module imports must be project-relative `.txl` paths that stay inside the project, such as `modules/helpers.txl`.";
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootedRelative = Path.GetRelativePath(root, candidate);
            if (Path.IsPathRooted(rootedRelative) || rootedRelative == ".."
                || rootedRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                error = "Module path escapes the TXL project root.";
                return false;
            }
            if (HasLinkInPath(root, rootedRelative))
            {
                error = "Module imports cannot follow a symbolic link outside the project.";
                return false;
            }
            fullPath = candidate;
            return true;
        }
    }

    private static bool HasLinkInPath(string root, string relative)
    {
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            FileSystemInfo item = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (item.Exists && item.LinkTarget is not null) return true;
        }
        return false;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;
    }
}
