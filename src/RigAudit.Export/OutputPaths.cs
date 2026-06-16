namespace RigAudit.Export;

public static class OutputPaths
{
    public static string Root =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RigAuditPro",
            "Outputs");

    public static string CreateOutputDirectory()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(Root);

        var basePath = Path.Combine(Root, timestamp);
        Directory.CreateDirectory(basePath);
        return EnsureOutputDirectoryIsConstrained(basePath, requireExisting: true);
    }

    public static string SnapshotPath(string outputDir)
        => Path.Combine(EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true), "RigSnapshot.json");

    public static string ExportSnapshotPath(string outputDir)
        => Path.Combine(EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true), "RigSnapshot.export.json");

    public static string LogPath(string outputDir)
        => Path.Combine(EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true), "scan.log");

    public static string DebugLogPath(string outputDir)
        => Path.Combine(EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true), "debug.log");

    public static string CompareReportPath(string outputDir)
        => Path.Combine(EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true), "SnapshotCompare.txt");

    public static string EnsureOutputDirectoryIsConstrained(string outputDir, bool requireExisting)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDir));

        var rootFullPath = Path.GetFullPath(Root);
        var fullPath = Path.GetFullPath(outputDir);
        var rootPrefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Output path is outside the allowed root: {fullPath}");
        }

        ValidateNoReparsePoints(rootFullPath, fullPath);

        if (requireExisting && !Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Output directory was not found: {fullPath}");

        return fullPath;
    }

    private static void ValidateNoReparsePoints(string rootFullPath, string fullPath)
    {
        if (Directory.Exists(rootFullPath))
            ThrowIfReparsePoint(rootFullPath);

        var relativePath = Path.GetRelativePath(rootFullPath, fullPath);
        if (relativePath == ".")
            return;

        var currentPath = rootFullPath;
        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!Directory.Exists(currentPath))
                return;

            ThrowIfReparsePoint(currentPath);
        }
    }

    private static void ThrowIfReparsePoint(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"Reparse points are not allowed in output paths: {path}");
    }
}
