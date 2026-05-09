namespace IoTSpy.Api;

public static class AssetsPaths
{
    public static string AssetsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "data", "assets");

    /// <summary>
    /// Resolves a client-supplied replacement-file path to an absolute path inside the assets
    /// directory. Rejects values containing path separators or parent-directory traversals to
    /// prevent any authenticated user from pointing a content rule at arbitrary files on the host.
    /// Returns null if the input is null/empty (caller should treat as "no replacement file"),
    /// or throws ArgumentException if the input is unsafe.
    /// </summary>
    public static string? ResolveReplacementFilePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // Accept either a bare filename or a path that is already inside AssetsDirectory.
        // Anything else is rejected.
        var assetsRoot = Path.GetFullPath(AssetsDirectory);

        // If caller passed an already-resolved path under AssetsDirectory, accept it as-is.
        try
        {
            var fullInput = Path.GetFullPath(input);
            if (fullInput.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || fullInput.Equals(assetsRoot, StringComparison.Ordinal))
            {
                // Still ensure the resolved path stays inside the assets root after canonicalisation.
                if (Path.GetDirectoryName(fullInput) == assetsRoot)
                    return fullInput;
            }
        }
        catch
        {
            // Fall through to filename-only handling.
        }

        // Otherwise treat input as a bare filename: reject anything containing separators or "..".
        if (input.Contains('/') || input.Contains('\\') || input.Contains(".."))
            throw new ArgumentException("ReplacementFilePath must be a bare filename inside the assets directory.", nameof(input));

        var fileName = Path.GetFileName(input);
        if (string.IsNullOrEmpty(fileName) || fileName != input)
            throw new ArgumentException("ReplacementFilePath must be a bare filename.", nameof(input));

        return Path.Combine(assetsRoot, fileName);
    }
}
