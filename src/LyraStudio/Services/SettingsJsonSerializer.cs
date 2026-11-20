using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyraStudio.Services;

/// <summary>
/// Turns dotted inspector paths (for example "layout.title") into nested
/// JSON objects and recursively normalizes repeater item dictionaries.
/// </summary>
public static class SettingsJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize(IDictionary<string, object?> values) =>
        JsonSerializer.Serialize(Expand(values), Options);

    private static Dictionary<string, object?> Expand(IDictionary<string, object?> source)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            var segments = pair.Key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0) continue;

            var current = result;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                if (!current.TryGetValue(segments[index], out var existing) ||
                    existing is not Dictionary<string, object?> nested)
                {
                    nested = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    current[segments[index]] = nested;
                }
                current = nested;
            }

            current[segments[^1]] = Normalize(pair.Value);
        }

        return result;
    }

    private static object? Normalize(object? value)
    {
        if (value is IDictionary<string, object?> dictionary)
            return Expand(dictionary);

        if (value is IEnumerable<Dictionary<string, object?>> items)
            return items.Select(Expand).ToList();

        if (value is JsonElement element)
            return NormalizeJsonElement(element);

        return value;
    }

    private static object? NormalizeJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(x => x.Name, x => NormalizeJsonElement(x.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.ToString()
    };
}
