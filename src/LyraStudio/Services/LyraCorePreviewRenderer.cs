using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Local renderers for the Lyra Core widgets found in plugin source. These
/// renderers consume extracted settings and package CSS/assets; they never
/// execute the PHP render() method or require WordPress/Elementor.
/// </summary>
public sealed class LyraCorePreviewRenderer
{
    private static readonly Regex SafeCssToken = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.CultureInvariant);

    public bool CanRender(string? rendererId) =>
        !string.IsNullOrWhiteSpace(rendererId) &&
        (rendererId.StartsWith("lyra-core.elementor/", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(rendererId, "elementor.generic", StringComparison.OrdinalIgnoreCase));

    public string BuildWidgetDocument(
        WidgetDescriptor widget,
        IReadOnlyDictionary<string, object?> settings,
        int viewportWidth,
        ProviderPreviewAssets? assets)
    {
        using var document = JsonDocument.Parse(SettingsJsonSerializer.Serialize(
            settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)));
        var fragment = RenderWidgetFragment(widget.Id, document.RootElement, assets);
        return BuildDocument(widget.Name, fragment, viewportWidth, assets);
    }

    public string BuildRecipeDocument(
        RecipeDescriptor recipe,
        int viewportWidth,
        ProviderPreviewAssets? assets)
    {
        var fragment = recipe.PreviewHtml ?? MissingRecipeFragment(recipe);
        return BuildDocument(recipe.Name, fragment, viewportWidth, assets);
    }

    public string RenderRecipeElements(
        object? elements,
        IReadOnlyDictionary<string, WidgetDescriptor> widgets,
        ProviderPreviewAssets? assets)
    {
        if (elements is not IEnumerable<object?> list)
            return "<div class='lyra-preview-unsupported'>No static page elements were found in this recipe.</div>";

        return string.Join("", list.Select(element => RenderRecipeElement(element, widgets, assets)));
    }

    private string RenderRecipeElement(
        object? element,
        IReadOnlyDictionary<string, WidgetDescriptor> widgets,
        ProviderPreviewAssets? assets)
    {
        var node = AsDictionary(element);
        if (node is null)
            return UnsupportedNode("Unknown recipe node");

        var type = GetString(node, "elType");
        var settings = AsDictionary(GetValue(node, "settings")) ?? new Dictionary<string, object?>();
        var children = GetValue(node, "elements");

        if (string.Equals(type, "container", StringComparison.OrdinalIgnoreCase))
        {
            var contentWidth = GetString(settings, "content_width");
            var maxWidth = contentWidth == "boxed"
                ? SafeCssDimension(GetNestedValue(settings, "boxed_width", "size"), "1140px")
                : "100%";
            var direction = GetString(settings, "flex_direction") == "row" ? "row" : "column";
            var gap = SafeCssDimension(GetNestedValue(settings, "flex_gap", "size"), "24px");
            var padding = ReadDimensions(GetValue(settings, "padding"));
            var className = SafeClassList(GetString(settings, "css_classes"));

            var style = $"max-width:{maxWidth};flex-direction:{direction};gap:{gap};padding:{padding};";
            var renderedChildren = children is IEnumerable<object?> childList
                ? string.Join("", childList.Select(child => RenderRecipeElement(child, widgets, assets)))
                : "";

            return $"<section class='lyra-recipe-container {className}' style='{WebUtility.HtmlEncode(style)}'><div class='lyra-recipe-container__inner'>{renderedChildren}</div></section>";
        }

        if (string.Equals(type, "widget", StringComparison.OrdinalIgnoreCase))
        {
            var widgetId = GetString(node, "widgetType") ?? "unknown-widget";
            using var settingsDocument = JsonDocument.Parse(JsonSerializer.Serialize(settings));
            return RenderWidgetFragment(widgetId, settingsDocument.RootElement, assets);
        }

        return UnsupportedNode(type ?? "Unmapped element");
    }

    public string RenderWidgetFragment(
        string widgetId,
        JsonElement settings,
        ProviderPreviewAssets? assets)
    {
        var localId = widgetId.StartsWith("lyra-", StringComparison.OrdinalIgnoreCase)
            ? widgetId
            : widgetId;

        return localId switch
        {
            "lyra-section-heading" => RenderSectionHeading(settings),
            "lyra-feature-grid" => RenderFeatureGrid(settings, assets),
            "lyra-site-header" => RenderSiteHeader(settings, assets),
            "lyra-site-footer" => RenderSiteFooter(settings, assets),
            "lyra-bottom-nav" => RenderBottomNav(settings, assets),
            _ => RenderGenericWidget(widgetId, settings)
        };
    }

    private static string RenderSectionHeading(JsonElement settings)
    {
        var eyebrow = GetString(settings, "eyebrow");
        var title = GetString(settings, "title") ?? "Section title";
        var tag = GetString(settings, "title_tag");
        if (tag is not ("h1" or "h2" or "h3" or "h4" or "div")) tag = "h2";
        var description = GetString(settings, "description");
        var align = GetString(settings, "align");
        if (align is not ("start" or "center" or "end")) align = "start";

        var html = $"<div class='lyra-widget lyra-section-heading' style='text-align:{align}'>";
        if (!string.IsNullOrWhiteSpace(eyebrow))
            html += $"<span class='lyra-section-heading__eyebrow'>{E(eyebrow)}</span>";
        html += $"<{tag} class='lyra-section-heading__title'>{E(title)}</{tag}>";
        if (!string.IsNullOrWhiteSpace(description))
            html += $"<p class='lyra-section-heading__description'>{E(description)}</p>";

        if (GetString(settings, "show_button") == "yes" && !string.IsNullOrWhiteSpace(GetString(settings, "button_text")))
            html += $"<a class='lyra-btn' href='#'>{E(GetString(settings, "button_text"))}</a>";

        return html + "</div>";
    }

    private static string RenderFeatureGrid(JsonElement settings, ProviderPreviewAssets? assets)
    {
        var items = GetArray(settings, "items");
        var columns = GetString(settings, "columns");
        if (!int.TryParse(columns, NumberStyles.Integer, CultureInfo.InvariantCulture, out var columnCount))
            columnCount = 3;
        columnCount = Math.Clamp(columnCount, 1, 4);

        var cards = new List<string>();
        foreach (var item in items)
        {
            var title = GetString(item, "title");
            var text = GetString(item, "text");
            var link = GetNestedString(item, "link", "url");
            var content = "<div class='lyra-feature-grid__item'>";
            var iconValue = GetNestedString(item, "icon", "value") ?? "";
            var iconName = iconValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "star";
            if (iconName.StartsWith("fa-", StringComparison.OrdinalIgnoreCase))
                iconName = iconName[3..];
            if (!SafeCssToken.IsMatch(iconName))
                iconName = "star";
            content += "<span class='lyra-feature-grid__icon'>" + Icon(iconName, assets) + "</span>";
            if (!string.IsNullOrWhiteSpace(title))
            {
                content += "<h3 class='lyra-feature-grid__title'>";
                content += !string.IsNullOrWhiteSpace(link)
                    ? $"<a href='#'>{E(title)}</a>"
                    : E(title);
                content += "</h3>";
            }
            if (!string.IsNullOrWhiteSpace(text))
                content += $"<p class='lyra-feature-grid__text'>{E(text)}</p>";
            content += "</div>";
            cards.Add(content);
        }

        if (cards.Count == 0)
            cards.Add("<div class='lyra-feature-grid__item'><h3 class='lyra-feature-grid__title'>Feature title</h3><p class='lyra-feature-grid__text'>Add items in the Inspector to preview this grid.</p></div>");

        return $"<div class='lyra-widget lyra-feature-grid'><div class='lyra-feature-grid__list' style='--lyra-preview-columns:{columnCount}'>{string.Join("", cards)}</div></div>";
    }

    private static string RenderSiteHeader(JsonElement settings, ProviderPreviewAssets? assets)
    {
        var brand = GetString(settings, "brand_name") ?? "LEO";
        var subtitle = GetString(settings, "brand_subtitle") ?? "COLLECTION";
        var logo = ResolveLogo(GetNestedString(settings, "logo", "url"), assets);
        var searchLabel = GetString(settings, "search_label") ?? "جستجو در کالکشن";
        var cartLabel = GetString(settings, "cart_label") ?? "سبد خرید";

        var logoHtml = string.IsNullOrWhiteSpace(logo)
            ? ""
            : $"<img class='lyra-site-header__logo' src='{E(logo)}' alt='' width='36' height='36' />";

        return $"""
            <header class="lyra-site-header lyra-accessible">
              <div class="lyra-site-header__inner">
                <a class="lyra-site-header__action lyra-icon-button" href="#" aria-label="{E(searchLabel)}">{Icon("search", assets)}</a>
                <a class="lyra-site-header__brand" href="#">
                  {logoHtml}
                  <span class="lyra-site-header__wordmark">
                    <span class="lyra-site-header__name">{E(brand)}</span>
                    <span class="lyra-site-header__subtitle">{E(subtitle)}</span>
                  </span>
                </a>
                <a class="lyra-site-header__action lyra-icon-button lyra-cart-link" href="#" aria-label="{E(cartLabel)}">{Icon("shopping_bag", assets)}<span class="lyra-cart-count">0</span></a>
              </div>
            </header>
            """;
    }

    private static string RenderBottomNav(JsonElement settings, ProviderPreviewAssets? assets)
    {
        var label = GetString(settings, "nav_label") ?? "ناوبری اصلی";
        var items = GetArray(settings, "items");
        var links = new List<string>();

        foreach (var item in items)
        {
            var itemLabel = GetString(item, "label");
            if (string.IsNullOrWhiteSpace(itemLabel)) continue;

            var iconName = GetString(item, "icon") ?? "home";
            var route = GetString(item, "route");
            var activeClass = route == "home" ? " is-active" : "";
            var cartCount = route == "cart" ? "<span class='lyra-cart-count'>0</span>" : "";
            links.Add($"<a class='lyra-bottom-nav__item{activeClass}' href='#'><span class='lyra-bottom-nav__icon'>{Icon(iconName, assets)}</span>{cartCount}<span class='lyra-bottom-nav__label'>{E(itemLabel)}</span></a>");
        }

        if (links.Count == 0)
            return "";

        return $"<nav class='lyra-bottom-nav lyra-accessible' aria-label='{E(label)}'><div class='lyra-bottom-nav__bar'>{string.Join("", links)}</div></nav>";
    }

    private static string RenderSiteFooter(JsonElement settings, ProviderPreviewAssets? assets)
    {
        var logo = ResolveLogo(GetNestedString(settings, "logo", "url"), assets);
        var brand = GetString(settings, "brand_name") ?? "LEO COLLECTION";
        var tagline = GetString(settings, "tagline");
        var description = GetString(settings, "description");
        var heading = GetString(settings, "quick_links_heading") ?? "دسترسی سریع";
        var copyright = (GetString(settings, "copyright") ?? "© {year} Leo Collection")
            .Replace("{year}", DateTime.Now.Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        var logoHtml = string.IsNullOrWhiteSpace(logo)
            ? ""
            : $"<img class='lyra-site-footer__logo' src='{E(logo)}' alt='{E(brand)}' width='64' height='64' />";

        var links = GetArray(settings, "links")
            .Select(item => GetString(item, "label"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"<li><a href='#'>{E(value)}</a></li>")
            .ToList();

        var social = GetArray(settings, "social_links")
            .Select(item => GetString(item, "label"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"<a class='lyra-icon-button lyra-site-footer__social-link' href='#' aria-label='{E(value)}'>{Icon("share", assets)}</a>")
            .ToList();

        var html = "<footer class='lyra-site-footer lyra-accessible'><div class='lyra-site-footer__inner'><div class='lyra-site-footer__brand'>";
        html += logoHtml;
        html += $"<p class='lyra-site-footer__name'>{E(brand)}</p>";
        if (!string.IsNullOrWhiteSpace(tagline))
            html += $"<p class='lyra-site-footer__tagline'>{E(tagline)}</p>";
        if (!string.IsNullOrWhiteSpace(description))
            html += $"<p class='lyra-site-footer__description'>{E(description)}</p>";
        html += "</div>";

        if (links.Count > 0)
            html += $"<nav class='lyra-site-footer__links' aria-label='{E(heading)}'><h2 class='lyra-site-footer__heading'>{E(heading)}</h2><ul>{string.Join("", links)}</ul></nav>";
        if (social.Count > 0)
            html += $"<nav class='lyra-site-footer__social' aria-label='شبکه‌های اجتماعی'>{string.Join("", social)}</nav>";
        if (!string.IsNullOrWhiteSpace(copyright))
            html += $"<p class='lyra-site-footer__copyright'>{E(copyright)}</p>";

        return html + "</div></footer>";
    }

    private static string RenderGenericWidget(string widgetId, JsonElement settings)
    {
        var rows = new List<string>();
        if (settings.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in settings.EnumerateObject())
            {
                if (property.Name.StartsWith("__", StringComparison.Ordinal)) continue;
                var value = DisplayValue(property.Value);
                if (string.IsNullOrWhiteSpace(value)) continue;
                rows.Add($"<div class='lyra-generic-preview__field'><span>{E(property.Name)}</span><strong>{E(value)}</strong></div>");
            }
        }

        return $"""
            <section class="lyra-widget lyra-generic-preview">
              <div class="lyra-generic-preview__eyebrow">Elementor widget · Structural preview</div>
              <h2>{E(widgetId)}</h2>
              <div class="lyra-generic-preview__fields">{string.Join("", rows)}</div>
              <p class="lyra-generic-preview__note">A provider-specific renderer is not available for this widget. The settings and repeaters are still discovered and editable.</p>
            </section>
            """;
    }

    private static string BuildDocument(
        string title,
        string fragment,
        int viewportWidth,
        ProviderPreviewAssets? assets)
    {
        var width = Math.Clamp(viewportWidth, 320, 1920);
        var css = assets?.StylesheetCss ?? "";
        css += """
            html,body { margin:0; min-height:100%; background:var(--lyra-background,#FAF8F4); color:var(--lyra-text,#2D3B1E); font-family:"Peyda",Tahoma,Arial,sans-serif; }
            body { padding:0; }
            .lyra-preview-root { width:100%; min-height:100vh; overflow:hidden; }
            .lyra-recipe-container { display:flex; width:100%; margin-inline:auto; }
            .lyra-recipe-container__inner { width:100%; display:flex; flex-direction:inherit; gap:inherit; }
            .lyra-recipe-container__inner > * { min-width:0; }
            .lyra-feature-grid__list { grid-template-columns:repeat(var(--lyra-preview-columns,3),minmax(0,1fr)); }
            .lyra-generic-preview { max-width:760px; margin:40px auto; padding:24px; border:1px dashed #C9A97A; border-radius:18px; background:#fff; }
            .lyra-generic-preview__eyebrow { color:#8A6A3B; font-size:12px; }
            .lyra-generic-preview__fields { display:grid; gap:8px; margin-top:20px; }
            .lyra-generic-preview__field { display:flex; justify-content:space-between; gap:16px; padding:12px; border:1px solid #E5E2D9; border-radius:10px; }
            .lyra-generic-preview__field span { color:#5F6656; }
            .lyra-generic-preview__field strong { font-weight:600; }
            .lyra-generic-preview__note { color:#7B806F; font-size:13px; }
            .lyra-preview-unsupported { margin:20px; padding:20px; border:1px dashed #C9A97A; border-radius:12px; color:#5F6656; background:#fff; }
            @media (max-width:767px) { .lyra-feature-grid__list { grid-template-columns:repeat(1,minmax(0,1fr)); } }
            @media (min-width:768px) and (max-width:1024px) { .lyra-feature-grid__list { grid-template-columns:repeat(2,minmax(0,1fr)); } }
            """;

        var sprite = assets?.IconSpriteMarkup ?? "";
        return $$"""
            <!doctype html>
            <html lang="fa" dir="rtl">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:; script-src 'none'; object-src 'none'; base-uri 'none';">
              <title>{{E(title)}} · Local Preview</title>
              <style>* { box-sizing:border-box; } {{css}}</style>
            </head>
            <body>
              {{sprite}}
              <main class="lyra-preview-root" data-viewport-width="{{width}}">{{fragment}}</main>
            </body>
            </html>
            """;
    }

    private static string Icon(string name, ProviderPreviewAssets? assets = null)
    {
        if (!SafeCssToken.IsMatch(name)) name = "help_outline";
        if (assets?.IconNames.Count > 0 && !assets.IconNames.Contains(name))
            name = assets.IconNames.Contains("star") ? "star" : assets.IconNames.First();
        return $"<svg class='lyra-icon' viewBox='0 0 24 24' aria-hidden='true'><use href='#lyra-i-{E(name)}'></use></svg>";
    }

    private static string FallbackStarIcon(ProviderPreviewAssets? assets = null) =>
        Icon("star", assets);

    private static string ResolveLogo(string? reference, ProviderPreviewAssets? assets)
    {
        if (!string.IsNullOrWhiteSpace(reference) &&
            !reference.StartsWith("http:", StringComparison.OrdinalIgnoreCase) &&
            !reference.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = assets?.ResolveAsset(reference);
            if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
        }
        return assets?.ResolveAsset("assets/demo/leo-logo-reference.jpg") ?? "";
    }

    private static string MissingRecipeFragment(RecipeDescriptor recipe) =>
        $"<div class='lyra-preview-unsupported'><h2>{E(recipe.Name)}</h2><p>Recipe metadata was found, but a safe local document preview could not be built.</p></div>";

    private static string UnsupportedNode(string type) =>
        $"<div class='lyra-preview-unsupported'>Unsupported element: {E(type)}. Its original data is kept in the source package.</div>";

    private static string ReadDimensions(object? value)
    {
        var dimensions = AsDictionary(value);
        if (dimensions is null) return "0";
        var top = SafeCssDimension(PhpSourceAnalysis.GetValue(dimensions, "top"), "0");
        var right = SafeCssDimension(PhpSourceAnalysis.GetValue(dimensions, "right"), "0");
        var bottom = SafeCssDimension(PhpSourceAnalysis.GetValue(dimensions, "bottom"), "0");
        var left = SafeCssDimension(PhpSourceAnalysis.GetValue(dimensions, "left"), "0");
        var unit = GetString(dimensions, "unit") ?? "px";
        if (unit is not ("px" or "%" or "em" or "rem" or "vw" or "vh")) unit = "px";
        return $"{top.Replace("px", "", StringComparison.Ordinal)}{unit} {right.Replace("px", "", StringComparison.Ordinal)}{unit} {bottom.Replace("px", "", StringComparison.Ordinal)}{unit} {left.Replace("px", "", StringComparison.Ordinal)}{unit}";
    }

    private static string SafeCssDimension(object? value, string fallback)
    {
        var raw = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return $"{number.ToString(CultureInfo.InvariantCulture)}px";
        if (Regex.IsMatch(raw, @"^\d+(\.\d+)?(px|%|em|rem|vw|vh)$", RegexOptions.CultureInvariant))
            return raw;
        return fallback;
    }

    private static string SafeClassList(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes)) return "";
        return string.Join(" ", classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => SafeCssToken.IsMatch(token)));
    }

    private static string DisplayValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
        JsonValueKind.Array => $"({value.GetArrayLength()} items)",
        JsonValueKind.Object => "{ ... }",
        _ => ""
    };

    private static IReadOnlyList<JsonElement> GetArray(JsonElement obj, string name)
    {
        var value = GetProperty(obj, name);
        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : Array.Empty<JsonElement>();
    }

    private static JsonElement GetProperty(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object)
            foreach (var property in obj.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
        return default;
    }

    private static string? GetString(JsonElement obj, string name)
    {
        var value = GetProperty(obj, name);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => null
        };
    }

    private static string? GetNestedString(JsonElement obj, string parent, string child) =>
        GetString(GetProperty(obj, parent), child);

    private static string? GetString(IDictionary<string, object?> obj, string name)
    {
        var value = PhpSourceAnalysis.GetValue(obj, name);
        return value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static object? GetValue(IDictionary<string, object?> obj, string name) =>
        PhpSourceAnalysis.GetValue(obj, name);

    private static object? GetNestedValue(IDictionary<string, object?> obj, string parent, string child)
    {
        var nested = AsDictionary(GetValue(obj, parent));
        return nested is null ? null : GetValue(nested, child);
    }

    private static Dictionary<string, object?>? AsDictionary(object? value)
    {
        if (value is IDictionary<string, object?> dictionary)
            return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
        return null;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
