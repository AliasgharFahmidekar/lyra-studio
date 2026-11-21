namespace LyraStudio.Models;

public sealed record WidgetDescriptor(
    string Id,
    string Name,
    string? Category = null,
    string? Description = null,
    string? Version = null,
    IReadOnlyList<ControlDefinition>? Controls = null,
    WidgetPreviewDefinition? Preview = null)
{
    /// <summary>Optional local renderer key selected by a provider source adapter.</summary>
    public string? PreviewRendererId { get; init; }
}

/// <summary>
/// Static, local HTML/CSS template supplied by a provider. Scripts and remote
/// resources are deliberately not supported by the manifest preview contract.
/// </summary>
public sealed record WidgetPreviewDefinition
{
    public string Html { get; init; } = "";
    public string Css { get; init; } = "";
}

public sealed record ControlDefinition
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string Kind { get; init; } = "unknown";
    public string? Description { get; init; }
    public bool Required { get; init; }
    public object? DefaultValue { get; init; }
    public IReadOnlyList<ControlOption>? Options { get; init; }
    public IReadOnlyList<ControlDefinition>? Children { get; init; }
    public IReadOnlyList<ControlCondition>? Conditions { get; init; }
    public IReadOnlyDictionary<string, object?>? Constraints { get; init; }
    public bool IsResponsive { get; init; }
}

public sealed record ControlOption(string Label, string Value);
public sealed record ControlCondition(string Field, string Operator, object? Value);

public enum ControlKind
{
    Unknown, Text, TextArea, Number, Toggle, Select, Color, Url, Media, Icon,
    Group, Repeater, Typography, Dimensions, Slider, DateTime
}
