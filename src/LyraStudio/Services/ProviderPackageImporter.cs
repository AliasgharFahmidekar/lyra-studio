using System.IO;
using System.IO.Compression;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Safely inspects a local ZIP, source folder, or legacy standalone manifest.
/// ZIP entries are inventoried in place: the package is not extracted and no
/// PHP, JavaScript, executable, or other provider code is run.
/// </summary>
public sealed class ProviderPackageImporter
{
    public const int MaximumPackageFiles = 10000;
    public const int MaximumDirectoryDepth = 32;
    public const long MaximumSingleFileBytes = 128L * 1024 * 1024;
    public const long MaximumPackageBytes = 1024L * 1024 * 1024;

    private static readonly HashSet<string> ManifestNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "lyra-studio.provider.json",
        "provider-manifest.json"
    };

    private static readonly HashSet<string> AssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".ico",
        ".woff", ".woff2", ".ttf", ".otf", ".eot"
    };

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".php", ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx", ".cs", ".py",
        ".rb", ".go", ".rs", ".java", ".kt", ".swift", ".cpp", ".h", ".sql",
        ".sh", ".ps1"
    };

    private readonly ProviderManifestLoader _manifestLoader;
    private readonly ProviderAdapterRegistry _adapterRegistry;
    private readonly ProviderPreviewAssetsLoader _previewAssetsLoader = new();

    public ProviderPackageImporter(
        ProviderManifestLoader? manifestLoader = null,
        ProviderAdapterRegistry? adapterRegistry = null)
    {
        _manifestLoader = manifestLoader ?? new ProviderManifestLoader();
        _adapterRegistry = adapterRegistry ?? new ProviderAdapterRegistry();
    }

    public async Task<ProviderPackageInspection> InspectAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Select a package ZIP, source folder, or provider manifest.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
            return await InspectDirectoryAsync(fullPath, cancellationToken);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected package was not found.", fullPath);

        var extension = Path.GetExtension(fullPath);
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return await InspectZipAsync(fullPath, cancellationToken);

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            var manifest = await _manifestLoader.LoadAsync(fullPath, cancellationToken);
            var file = new FileInfo(fullPath);
            return new ProviderPackageInspection(
                Path.GetFileNameWithoutExtension(fullPath),
                fullPath,
                manifest,
                file.Name,
                new[] { new PackageFileDescriptor(file.Name, file.Length, "Provider manifest", ".json") },
                Array.Empty<string>());
        }

        throw new InvalidDataException("Supported inputs are .zip packages, source folders, and provider .json manifests.");
    }

    private async Task<ProviderPackageInspection> InspectZipAsync(string path, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > MaximumPackageFiles)
            throw new InvalidDataException($"The package contains more than {MaximumPackageFiles:N0} entries.");

        var files = new List<PackageFileDescriptor>();
        var warnings = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            var relativePath = NormalizePackagePath(entry.FullName, isDirectory);
            if (string.IsNullOrEmpty(relativePath))
                continue;

            RejectLinkEntry(entry);
            if (isDirectory)
                continue;

            if (!seenPaths.Add(relativePath))
                throw new InvalidDataException($"The package contains duplicate paths (case-insensitive): '{relativePath}'.");

            ValidateEntrySize(entry.Length, relativePath, ref totalBytes);
            files.Add(CreateFileDescriptor(relativePath, entry.Length));
        }

        var manifestPath = SelectManifestPath(files.Select(x => x.RelativePath));
        ProviderManifest? manifest = null;
        if (manifestPath is not null)
        {
            var entry = archive.Entries.FirstOrDefault(x =>
                !x.FullName.EndsWith('/') &&
                !x.FullName.EndsWith('\\') &&
                string.Equals(x.FullName.Replace('\\', '/'), manifestPath, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"The provider manifest entry '{manifestPath}' could not be opened.");

            if (entry.Length > ProviderManifestLoader.MaximumManifestBytes)
                throw new InvalidDataException("The provider manifest exceeds the supported size limit.");

            await using var stream = entry.Open();
            manifest = await _manifestLoader.LoadAsync(stream, cancellationToken);
        }
        ProviderPreviewAssets? previewAssets = null;
        try
        {
            previewAssets = await _previewAssetsLoader.LoadAsync(path, files.Select(file => file.RelativePath).ToArray(), cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            warnings.Add("Provider preview assets were skipped: " + ex.Message);
        }

        string? adapterId = null;
        if (manifest is null)
        {
            var discovery = await _adapterRegistry.DiscoverAsync(
                path, files.Select(file => file.RelativePath).ToArray(), previewAssets, cancellationToken);
            if (discovery is not null)
            {
                manifest = discovery.Manifest;
                adapterId = discovery.AdapterId;
                warnings.AddRange(discovery.Warnings);
            }
            else
            {
                warnings.Add("No Lyra Studio provider manifest or compatible source adapter was found. The package is shown as a generic file inventory.");
            }
        }

        return new ProviderPackageInspection(
            Path.GetFileNameWithoutExtension(path), path, manifest, manifestPath, files, warnings)
        {
            PreviewAssets = previewAssets,
            AdapterId = adapterId
        };
    }

    private async Task<ProviderPackageInspection> InspectDirectoryAsync(string root, CancellationToken cancellationToken)
    {
        var rootInfo = new DirectoryInfo(root);
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The selected source folder is a link/reparse point. Select its real folder instead.");

        var files = new List<PackageFileDescriptor>();
        var warnings = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<(DirectoryInfo Directory, int Depth)>();
        pending.Push((rootInfo, 0));
        long totalBytes = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, depth) = pending.Pop();

            foreach (var childPath in Directory.EnumerateFileSystemEntries(directory.FullName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(childPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    warnings.Add($"Skipped linked item: {Path.GetRelativePath(root, childPath)}");
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (depth >= MaximumDirectoryDepth)
                        throw new InvalidDataException($"The source folder exceeds the maximum scan depth of {MaximumDirectoryDepth}.");
                    pending.Push((new DirectoryInfo(childPath), depth + 1));
                    continue;
                }

                var relativePath = NormalizePackagePath(
                    Path.GetRelativePath(root, childPath).Replace(Path.DirectorySeparatorChar, '/'), false);
                if (!seenPaths.Add(relativePath))
                    throw new InvalidDataException($"The source folder contains duplicate paths: '{relativePath}'.");
                var info = new FileInfo(childPath);
                ValidateEntrySize(info.Length, relativePath, ref totalBytes);
                files.Add(CreateFileDescriptor(relativePath, info.Length));

                if (files.Count > MaximumPackageFiles)
                    throw new InvalidDataException($"The source folder contains more than {MaximumPackageFiles:N0} files.");
            }
        }

        var manifestPath = SelectManifestPath(files.Select(x => x.RelativePath));
        ProviderManifest? manifest = null;
        if (manifestPath is not null)
        {
            var fullManifestPath = Path.GetFullPath(Path.Combine(root, manifestPath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullManifestPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The provider manifest path escapes the selected package folder.");

            manifest = await _manifestLoader.LoadAsync(fullManifestPath, cancellationToken);
        }
        ProviderPreviewAssets? previewAssets = null;
        try
        {
            previewAssets = await _previewAssetsLoader.LoadAsync(root, files.Select(file => file.RelativePath).ToArray(), cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            warnings.Add("Provider preview assets were skipped: " + ex.Message);
        }

        string? adapterId = null;
        if (manifest is null)
        {
            var discovery = await _adapterRegistry.DiscoverAsync(
                root, files.Select(file => file.RelativePath).ToArray(), previewAssets, cancellationToken);
            if (discovery is not null)
            {
                manifest = discovery.Manifest;
                adapterId = discovery.AdapterId;
                warnings.AddRange(discovery.Warnings);
            }
            else
            {
                warnings.Add("No Lyra Studio provider manifest or compatible source adapter was found. The folder is shown as a generic file inventory.");
            }
        }

        return new ProviderPackageInspection(
            rootInfo.Name, root, manifest, manifestPath, files, warnings)
        {
            PreviewAssets = previewAssets,
            AdapterId = adapterId
        };
    }

    private static string NormalizePackagePath(string rawPath, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawPath) || rawPath.Contains('\0'))
            throw new InvalidDataException("The package contains an empty or invalid path.");

        var path = rawPath.Replace('\\', '/');
        if (path.StartsWith('/') ||
            (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':'))
            throw new InvalidDataException($"The package contains an absolute path: '{rawPath}'.");

        if (isDirectory)
            path = path.TrimEnd('/');

        var segments = path.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".." || segment.Contains(':')))
            throw new InvalidDataException($"The package contains an unsafe path: '{rawPath}'.");

        return string.Join('/', segments);
    }

    private static void RejectLinkEntry(ZipArchiveEntry entry)
    {
        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        var hasReparsePoint = (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0;
        if (unixType == 0xA000 || hasReparsePoint)
            throw new InvalidDataException($"The package contains a symbolic link or reparse-point entry: '{entry.FullName}'.");
    }

    private static void ValidateEntrySize(long length, string path, ref long totalBytes)
    {
        if (length < 0 || length > MaximumSingleFileBytes)
            throw new InvalidDataException($"Package file '{path}' exceeds the per-file size limit.");
        if (length > MaximumPackageBytes - totalBytes)
            throw new InvalidDataException($"The package exceeds the {MaximumPackageBytes:N0}-byte total size limit.");
        totalBytes += length;
    }

    private static string? SelectManifestPath(IEnumerable<string> paths)
    {
        var candidates = paths
            .Where(path => ManifestNames.Contains(path.Split('/').Last()))
            .ToList();

        var rootCandidates = candidates.Where(path => !path.Contains('/')).ToList();
        if (rootCandidates.Count == 1)
            return rootCandidates[0];
        if (rootCandidates.Count > 1)
            throw new InvalidDataException("The package contains more than one root provider manifest.");

        if (candidates.Count == 1)
            return candidates[0];
        if (candidates.Count > 1)
            throw new InvalidDataException("The package contains multiple provider manifests and none is at the package root.");

        return null;
    }

    private static PackageFileDescriptor CreateFileDescriptor(string path, long size)
    {
        var extension = Path.GetExtension(path);
        var lowerPath = path.ToLowerInvariant();
        var segments = lowerPath.Split('/');
        var fileName = segments[^1];

        var category = AssetExtensions.Contains(extension) || segments.Contains("assets") || segments.Contains("media")
            ? "Asset"
            : segments.Contains("recipes") || segments.Contains("pages") || fileName.Contains("recipe") || fileName.Contains("page")
                ? "Recipe/Page data"
                : segments.Contains("templates") || fileName.Contains("template") || extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
                    ? "Template"
                    : SourceExtensions.Contains(extension)
                        ? "Source"
                        : extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
                            ? "Style"
                            : "Other";

        return new PackageFileDescriptor(path, size, category, extension);
    }
}
