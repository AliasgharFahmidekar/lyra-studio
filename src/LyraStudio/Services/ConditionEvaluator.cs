using System.Text.Json;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>Evaluates provider-defined conditional visibility rules without UI dependencies.</summary>
public static class ConditionEvaluator
{
    public static bool Matches(
        IReadOnlyList<ControlCondition>? conditions,
        IReadOnlyDictionary<string, object?> values,
        string? pathPrefix = null)
    {
        if (conditions is null || conditions.Count == 0) return true;

        foreach (var condition in conditions)
        {
            var field = values.ContainsKey(condition.Field)
                ? condition.Field
                : !string.IsNullOrWhiteSpace(pathPrefix)
                    ? $"{pathPrefix}.{condition.Field}"
                    : condition.Field;

            if (!values.TryGetValue(field, out var actual)) return false;

            var left = ToText(actual);
            var right = ToText(condition.Value);
            var op = condition.Operator?.Trim().ToLowerInvariant() ?? string.Empty;
            var match = op switch
            {
                "==" or "=" or "equals" or "is" =>
                    string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                "!=" or "not_equals" or "is_not" =>
                    !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
                "truthy" => IsTruthy(actual),
                "falsy" => !IsTruthy(actual),
                _ => false
            };

            if (!match) return false;
        }

        return true;
    }

    private static bool IsTruthy(object? value)
    {
        if (value is null) return false;
        if (value is bool boolean) return boolean;
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
                JsonValueKind.Number => element.TryGetDouble(out var number) && number != 0,
                JsonValueKind.String => !string.IsNullOrEmpty(element.GetString()),
                _ => true
            };
        }

        if (value is string text) return !string.IsNullOrEmpty(text);
        if (value is int integer) return integer != 0;
        if (value is long longInteger) return longInteger != 0;
        if (value is double numberValue) return numberValue != 0;
        return true;
    }

    private static string ToText(object? value)
    {
        if (value is null) return string.Empty;
        if (value is JsonElement element)
            return element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty
                : element.ToString();
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
