using System.Text.Json;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>Converts and manipulates repeater items independently from WPF controls.</summary>
public static class RepeaterValueHelper
{
    public static List<Dictionary<string, object?>> ReadItems(
        object? value,
        IReadOnlyList<ControlDefinition>? itemSchema)
    {
        if (value is null) return new List<Dictionary<string, object?>>();

        JsonElement root;
        if (value is JsonElement element)
            root = element;
        else
        {
            try { root = JsonSerializer.SerializeToElement(value); }
            catch (NotSupportedException) { return new List<Dictionary<string, object?>>(); }
        }

        if (root.ValueKind != JsonValueKind.Array)
            return new List<Dictionary<string, object?>>();

        var items = new List<Dictionary<string, object?>>();
        foreach (var entry in root.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in entry.EnumerateObject())
                FlattenProperty(property.Name, property.Value, item);

            var defaults = CreateDefaultItem(itemSchema);
            foreach (var pair in defaults)
                if (!item.ContainsKey(pair.Key))
                    item[pair.Key] = pair.Value;

            items.Add(item);
        }

        return items;
    }

    public static Dictionary<string, object?> CreateDefaultItem(
        IReadOnlyList<ControlDefinition>? itemSchema)
    {
        var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        AddDefaults(itemSchema, item, null);
        return item;
    }

    public static bool TryAdd(
        IList<Dictionary<string, object?>> items,
        IReadOnlyList<ControlDefinition>? schema,
        int maximum)
    {
        if (items.Count >= maximum) return false;
        items.Add(CreateDefaultItem(schema));
        return true;
    }

    public static bool TryRemove(IList<Dictionary<string, object?>> items, int index, int minimum)
    {
        if (items.Count <= minimum || index < 0 || index >= items.Count) return false;
        items.RemoveAt(index);
        return true;
    }

    public static bool TryMove(IList<Dictionary<string, object?>> items, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= items.Count ||
            toIndex < 0 || toIndex >= items.Count || fromIndex == toIndex)
            return false;

        var item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
        return true;
    }

    public static int ReadLimit(ControlDefinition definition, string key, int fallback)
    {
        if (definition.Constraints is null ||
            !definition.Constraints.TryGetValue(key, out var value) ||
            value is null)
            return fallback;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
                return number;
            if (element.ValueKind == JsonValueKind.String &&
                int.TryParse(element.GetString(), out number))
                return number;
        }

        return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static void AddDefaults(
        IReadOnlyList<ControlDefinition>? definitions,
        IDictionary<string, object?> target,
        string? prefix)
    {
        if (definitions is null) return;

        foreach (var definition in definitions)
        {
            var key = string.IsNullOrEmpty(prefix) ? definition.Name : $"{prefix}.{definition.Name}";
            if (string.Equals(definition.Kind, "group", StringComparison.OrdinalIgnoreCase))
                AddDefaults(definition.Children, target, key);
            else if (string.Equals(definition.Kind, "repeater", StringComparison.OrdinalIgnoreCase))
                target[key] = definition.DefaultValue ?? new List<object>();
            else
                target[key] = definition.DefaultValue;
        }
    }

    private static void FlattenProperty(string name, JsonElement value, IDictionary<string, object?> target)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in value.EnumerateObject())
                FlattenProperty($"{name}.{child.Name}", child.Value, target);
            return;
        }

        target[name] = value.Clone();
    }
}
