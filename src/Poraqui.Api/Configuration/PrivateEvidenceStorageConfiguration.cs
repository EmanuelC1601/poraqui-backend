namespace Poraqui.Api.Configuration;

public static class PrivateEvidenceStorageConfiguration
{
    public static string ValidateProductionRoot(string? configuredRoot, string? webRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException(
                "PrivateFiles:Root must point to persistent storage outside wwwroot when running in Production.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        if (string.IsNullOrWhiteSpace(webRootPath)) return root;

        var webRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRootPath));
        var webRootPrefix = webRoot + Path.DirectorySeparatorChar;
        if (string.Equals(root, webRoot, StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith(webRootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PrivateFiles:Root must be outside wwwroot to keep transfer evidence private.");

        return root;
    }
}
