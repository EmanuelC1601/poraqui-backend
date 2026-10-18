namespace Poraqui.Api.Configuration;

public static class PersistentDataProtectionConfiguration
{
    public static string ValidateProductionPath(string? configuredPath, string? webRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException(
                "DataProtection:KeysPath must point to persistent storage outside wwwroot when running in Production.");

        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPath));
        if (string.IsNullOrWhiteSpace(webRootPath)) return path;

        var webRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRootPath));
        if (string.Equals(path, webRoot, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DataProtection:KeysPath must be outside wwwroot to keep key material private.");

        return path;
    }
}
