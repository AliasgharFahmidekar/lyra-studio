using System.IO;
using System.IO.Compression;
using System.Text;

namespace LyraStudio.Services;

/// <summary>
/// Reads bounded package files without extracting archives or following
/// links outside an inspected source directory.
/// </summary>
public static class ProviderPackageContentReader
{
    public static async Task<byte[]?> ReadBytesAsync(
        string packagePath,
        string relativePath,
        int maximumBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\0'))
            return null;

        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') ||
            normalized.Split('/').Any(x => x is "" or "." or ".." || x.Contains(':')))
            return null;

        if (File.Exists(packagePath) && Path.GetExtension(packagePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entry = archive.Entries.FirstOrDefault(x =>
                !x.FullName.EndsWith('/') &&
                !x.FullName.EndsWith('\\') &&
                string.Equals(x.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
            if (entry is null || entry.Length < 0 || entry.Length > maximumBytes)
                return null;

            await using var stream = entry.Open();
            return await ReadBoundedAsync(stream, maximumBytes, cancellationToken);
        }

        if (!Directory.Exists(packagePath))
            return null;

        var root = Path.GetFullPath(packagePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;

        var current = root.TrimEnd(Path.DirectorySeparatorChar);
        foreach (var segment in normalized.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                return null;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return null;
        }

        var info = new FileInfo(fullPath);
        if (info.Length < 0 || info.Length > maximumBytes)
            return null;

        await using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ReadBoundedAsync(file, maximumBytes, cancellationToken);
    }

    public static async Task<string?> ReadTextAsync(
        string packagePath,
        string relativePath,
        int maximumBytes = 4 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(packagePath, relativePath, maximumBytes, cancellationToken);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
                throw new InvalidDataException($"A package file exceeded the {maximumBytes:N0}-byte read limit.");
            await buffer.WriteAsync(chunk.AsMemory(), cancellationToken);
        }
        return buffer.ToArray();
    }
}
