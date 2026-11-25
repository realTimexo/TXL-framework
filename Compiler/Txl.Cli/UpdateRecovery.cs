namespace Txl;

internal static class UpdateRecovery
{
    internal static bool IsWindowsToolStoreAccessDenied(string error, bool isWindows) =>
        isWindows
        && error.Contains(".store", StringComparison.OrdinalIgnoreCase)
        && error.Contains("txl.cli", StringComparison.OrdinalIgnoreCase)
        && ((error.Contains("Access to the path", StringComparison.OrdinalIgnoreCase)
                && error.Contains("denied", StringComparison.OrdinalIgnoreCase))
            || (error.Contains("Zugriff auf den Pfad", StringComparison.OrdinalIgnoreCase)
                && (error.Contains("verweigert", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("nicht zulässig", StringComparison.OrdinalIgnoreCase))));
}
