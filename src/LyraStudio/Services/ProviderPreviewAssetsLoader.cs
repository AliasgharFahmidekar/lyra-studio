using System.Text;
using System.Text.RegularExpressions;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Loads package CSS and selected assets. Local font URLs are rewritten to
/// data URIs; remote resources and executable CSS are rejected.
/// </summary>
public sealed class ProviderPreviewAssetsLoader
{
    private static readonly string[] StylesheetPaths =
    {
        "assets/css/tokens.css",
        "assets/css/components.css",
        "assets/css/fonts.css"
    };

    private static readonly Regex UrlPattern = new(
        @"url\(\s*(?<quote>['""]?)(?<url>[^)'""\s]+)\k<quote>\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<ProviderPreviewAssets> LoadAsync(
        string packagePath,
        IReadOnlyCollection<string> packageFiles,
        CancellationToken cancellationToken = default)
    {
        var tokensPath = packageFiles.FirstOrDefault(path =>
            path.EndsWith("assets/css/tokens.css", StringComparison.OrdinalIgnoreCase));
        var assetPrefix = tokensPath is null
            ? ""
            : tokensPath[..^"assets/css/tokens.css".Length];

        var cssParts = new List<string>();
        foreach (var relativeCssPath in StylesheetPaths)
        {
            var cssPath = assetPrefix + relativeCssPath;
            var source = await ProviderPackageContentReader.ReadTextAsync(
                packagePath, cssPath, 2 * 1024 * 1024, cancellationToken);
            if (string.IsNullOrWhiteSpace(source))
                continue;

            var rewritten = await RewriteLocalUrlsAsync(packagePath, cssPath, source, cancellationToken);
            ValidateStylesheet(rewritten, cssPath);
            cssParts.Add(rewritten);
        }

        var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long embeddedAssetBytes = 0;
        var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".ico"
        };

        foreach (var assetPath in packageFiles
            .Where(path => path.StartsWith(assetPrefix + "assets/", StringComparison.OrdinalIgnoreCase))
            .Where(path => imageExtensions.Contains(Path.GetExtension(path)))
            .Take(100))
        {
            var remaining = 16L * 1024 * 1024 - embeddedAssetBytes;
            if (remaining <= 0) break;

            var bytes = await ProviderPackageContentReader.ReadBytesAsync(
                packagePath, assetPath, (int)Math.Min(4L * 1024 * 1024, remaining), cancellationToken);
            if (bytes is not { Length: > 0 }) continue;

            var relativeAssetPath = assetPath[assetPrefix.Length..];
            var mime = GetMimeType(Path.GetExtension(assetPath));
            assets[relativeAssetPath] = ToDataUri(mime, bytes);
            embeddedAssetBytes += bytes.Length;
        }

        var spritePath = packageFiles.FirstOrDefault(path =>
            path.StartsWith(assetPrefix + "assets/", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(path).Equals("sprite.svg", StringComparison.OrdinalIgnoreCase));
        var sprite = spritePath is null
            ? null
            : await ProviderPackageContentReader.ReadTextAsync(
                packagePath, spritePath, 256 * 1024, cancellationToken);
        if (!string.IsNullOrWhiteSpace(sprite))
        {
            var svgNamespace = " xmlns=" + (char)34 + "http://www.w3.org/2000/svg" + (char)34;
            sprite = sprite.Replace(svgNamespace, "", StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(sprite) && IsSafeSvgSprite(sprite))
            sprite = Regex.Replace(sprite, @"<svg\b", "<svg style='display:none' aria-hidden='true'", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        else
            sprite = null;

        var iconNames = !string.IsNullOrWhiteSpace(sprite)
            ? Regex.Matches(sprite, @"<symbol\s+id=['""]lyra-i-(?<name>[a-zA-Z0-9_-]+)['""]")
                .Select(match => match.Groups["name"].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new ProviderPreviewAssets
        {
            StylesheetCss = string.Join("\n\n", cssParts),
            IconSpriteMarkup = sprite,
            IconNames = iconNames,
            DataAssets = assets
        };
    }

    private static async Task<string> RewriteLocalUrlsAsync(
        string packagePath,
        string cssPath,
        string css,
        CancellationToken cancellationToken)
    {
        var cssDirectory = cssPath[..(cssPath.LastIndexOf('/') + 1)];
        var matches = UrlPattern.Matches(css);
        var builder = new StringBuilder();
        var last = 0;

        foreach (Match match in matches)
        {
            builder.Append(css, last, match.Index - last);
            var rawUrl = match.Groups["url"].Value.Trim();
            var replacement = "none";

            if (rawUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                replacement = $"url('{rawUrl}')";
            }
            else if (!rawUrl.Contains(':') && !rawUrl.StartsWith("//", StringComparison.Ordinal))
            {
                var normalized = NormalizeRelativeAssetPath(cssDirectory, rawUrl);
                if (normalized is not null)
                {
                    var bytes = await ProviderPackageContentReader.ReadBytesAsync(
                        packagePath, normalized, 4 * 1024 * 1024, cancellationToken);
                    if (bytes is { Length: > 0 })
                    {
                        var mime = GetMimeType(Path.GetExtension(normalized));
                        replacement = $"url('{ToDataUri(mime, bytes)}')";
                    }
                }
            }

            builder.Append(replacement);
            last = match.Index + match.Length;
        }

        builder.Append(css, last, css.Length - last);
        return builder.ToString();
    }

    private static string? NormalizeRelativeAssetPath(string directory, string path)
    {
        if (path.Contains('?') || path.Contains('#') || path.StartsWith("/", StringComparison.Ordinal))
            return null;

        var segments = (directory + path.Replace('\\', '/')).Split('/');
        var output = new List<string>();
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == ".")
                continue;
            if (segment == "..")
            {
                if (output.Count == 0) return null;
                output.RemoveAt(output.Count - 1);
            }
            else
            {
                output.Add(segment);
            }
        }
        return string.Join('/', output);
    }

    private static void ValidateStylesheet(string css, string path)
    {
        if (css.Length > 4 * 1024 * 1024)
            throw new InvalidDataException($"Provider stylesheet '{path}' exceeds the preview size limit.");

        if (Regex.IsMatch(css,
            @"@import\b|https?://|expression\s*\(|-moz-binding|behavior\s*:",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidDataException($"Provider stylesheet '{path}' contains external or executable CSS.");
    }

    private static bool IsSafeSvgSprite(string svg)
    {
        if (svg.Length > 256 * 1024)
            return false;

        return !Regex.IsMatch(svg,
            @"<\s*(script|foreignObject|iframe|object|image)\b|\son[a-z]+\s*=|javascript\s*:|https?://|href\s*=\s*['""]//",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".woff2" => "font/woff2",
        ".woff" => "font/woff",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".eot" => "application/vnd.ms-fontobject",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    private static string ToDataUri(string mime, byte[] bytes) =>
        $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
}
