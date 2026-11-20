using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Builds inspector controls entirely from provider-supplied metadata.
/// Unknown kinds remain editable as raw text/JSON instead of being discarded.
/// </summary>
public sealed class DynamicInspectorBuilder
{
    public void Build(
        Panel target,
        IReadOnlyList<ControlDefinition>? definitions,
        IDictionary<string, object?> values,
        Action onChanged)
    {
        target.Children.Clear();
        if (definitions is null || definitions.Count == 0)
        {
            target.Children.Add(new TextBlock
            {
                Text = "This widget has not exposed configurable controls.",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 8)
            });
            return;
        }

        var conditionalElements = new List<(FrameworkElement Element, ControlDefinition Definition)>();
        Action refreshConditions = () =>
        {
            foreach (var item in conditionalElements)
                item.Element.Visibility = ConditionsMatch(item.Definition.Conditions, values)
                    ? Visibility.Visible : Visibility.Collapsed;
        };

        foreach (var definition in definitions)
            target.Children.Add(BuildControl(definition, null, values, conditionalElements, refreshConditions, onChanged));

        refreshConditions();
    }

    private static FrameworkElement BuildControl(
        ControlDefinition definition,
        string? parentPath,
        IDictionary<string, object?> values,
        ICollection<(FrameworkElement Element, ControlDefinition Definition)> conditionalElements,
        Action refreshConditions,
        Action onChanged)
    {
        var key = string.IsNullOrWhiteSpace(parentPath)
            ? definition.Name
            : $"{parentPath}.{definition.Name}";

        if (!values.ContainsKey(key))
            values[key] = definition.DefaultValue;

        var wrapper = new Border
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(0, 0, 0, 2)
        };
        var body = new StackPanel();
        wrapper.Child = body;

        var kind = NormalizeKind(definition.Kind);
        if (kind == "group")
        {
            var nested = new StackPanel { Margin = new Thickness(10, 8, 0, 0) };
            if (definition.Children is not null)
            {
                foreach (var child in definition.Children)
                    nested.Children.Add(BuildControl(child, key, values, conditionalElements, refreshConditions, onChanged));
            }

            body.Children.Add(new Expander
            {
                Header = definition.Label,
                Content = nested,
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold
            });
        }
        else
        {
            body.Children.Add(new TextBlock
            {
                Text = definition.Label,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 63, 78)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            });

            if (!string.IsNullOrWhiteSpace(definition.Description))
            {
                body.Children.Add(new TextBlock
                {
                    Text = definition.Description,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }

            var current = values[key];
            void Save(object? value)
            {
                values[key] = value;
                refreshConditions();
                onChanged();
            }

            FrameworkElement editor;
            switch (kind)
            {
                case "toggle":
                    var checkBox = new CheckBox
                    {
                        IsChecked = ToBoolean(current),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 4, 0, 4)
                    };
                    checkBox.Checked += (_, _) => Save(true);
                    checkBox.Unchecked += (_, _) => Save(false);
                    editor = checkBox;
                    break;

                case "select":
                    var combo = new ComboBox
                    {
                        MinHeight = 32,
                        Padding = new Thickness(6, 4, 6, 4),
                        ItemsSource = definition.Options ?? Array.Empty<ControlOption>(),
                        DisplayMemberPath = nameof(ControlOption.Label),
                        SelectedValuePath = nameof(ControlOption.Value)
                    };
                    combo.SelectedValue = current?.ToString();
                    combo.SelectionChanged += (_, _) =>
                    {
                        if (combo.SelectedValue is string selected) Save(selected);
                    };
                    editor = combo;
                    break;

                case "slider":
                    var min = ReadConstraint(definition, "min", 0);
                    var max = ReadConstraint(definition, "max", 100);
                    if (max <= min) max = min + 1;
                    var slider = new Slider
                    {
                        Minimum = min,
                        Maximum = max,
                        Value = Math.Clamp(ToDouble(current, min), min, max),
                        TickFrequency = ReadConstraint(definition, "step", 1),
                        IsSnapToTickEnabled = true,
                        AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft
                    };
                    slider.ValueChanged += (_, _) => Save(slider.Value);
                    editor = slider;
                    break;

                case "date":
                    var picker = new DatePicker { SelectedDate = ToDate(current) };
                    picker.SelectedDateChanged += (_, _) => Save(picker.SelectedDate);
                    editor = picker;
                    break;

                case "repeater":
                case "typography":
                case "dimensions":
                case "unknown":
                    var complexEditor = CreateTextEditor(current, multiline: true);
                    complexEditor.ToolTip = kind == "repeater"
                        ? "Edit repeater items as a JSON array in this version."
                        : kind == "unknown"
                            ? $"Unrecognized control kind '{definition.Kind}'. Value is preserved as text/JSON."
                            : "Edit the structured value as JSON.";
                    complexEditor.LostFocus += (_, _) =>
                    {
                        if (TryParseJson(complexEditor.Text, out var parsed))
                        {
                            complexEditor.Background = Brushes.White;
                            Save(parsed);
                        }
                        else
                        {
                            complexEditor.Background = Brushes.MistyRose;
                        }
                    };
                    editor = complexEditor;
                    break;

                default:
                    var textEditor = CreateTextEditor(current, multiline: kind == "textarea");
                    textEditor.TextChanged += (_, _) =>
                    {
                        if (kind == "number")
                        {
                            if (double.TryParse(textEditor.Text, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out var number))
                            {
                                textEditor.Background = Brushes.White;
                                Save(number);
                            }
                            else
                            {
                                textEditor.Background = string.IsNullOrWhiteSpace(textEditor.Text)
                                    ? Brushes.White : Brushes.MistyRose;
                            }
                        }
                        else
                        {
                            Save(textEditor.Text);
                        }
                    };
                    editor = textEditor;
                    break;
            }

            editor.ToolTip ??= definition.Required ? "Required field" : null;
            body.Children.Add(editor);
        }

        if (definition.Conditions is { Count: > 0 })
            conditionalElements.Add((wrapper, definition));

        return wrapper;
    }

    private static TextBox CreateTextEditor(object? value, bool multiline)
    {
        return new TextBox
        {
            Text = ToDisplayString(value),
            MinHeight = multiline ? 76 : 34,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            Padding = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 225, 233)),
            Background = Brushes.White
        };
    }

    private static string NormalizeKind(string? kind) => (kind ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "text" or "url" or "link" or "color" or "media" or "image" or "icon" => "text",
        "textarea" or "text_area" => "textarea",
        "number" or "integer" or "float" => "number",
        "toggle" or "switch" or "checkbox" or "boolean" => "toggle",
        "select" or "choose" => "select",
        "slider" or "range" => "slider",
        "date" or "datetime" or "date_time" => "date",
        "group" or "section" or "tab" => "group",
        "repeater" or "typography" or "dimensions" => (kind ?? string.Empty).Trim().ToLowerInvariant(),
        _ => "unknown"
    };

    private static bool ConditionsMatch(
        IReadOnlyList<ControlCondition>? conditions,
        IDictionary<string, object?> values)
    {
        if (conditions is null || conditions.Count == 0) return true;

        foreach (var condition in conditions)
        {
            if (!values.TryGetValue(condition.Field, out var actual)) return false;
            var left = ToDisplayString(actual);
            var right = ToDisplayString(condition.Value);
            var matches = condition.Operator.Trim().ToLowerInvariant() switch
            {
                "==" or "=" or "equals" or "is" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                "!=" or "not_equals" or "is_not" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
                "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
                "truthy" => ToBoolean(actual),
                "falsy" => !ToBoolean(actual),
                _ => false
            };
            if (!matches) return false;
        }

        return true;
    }

    private static bool TryParseJson(string text, out object? value)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text);
            value = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }

    private static string ToDisplayString(object? value)
    {
        if (value is null) return string.Empty;
        if (value is JsonElement element)
            return element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString();
        if (value is DateTime date) return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dateOffset) return dateOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool ToBoolean(object? value)
    {
        if (value is bool boolean) return boolean;
        return bool.TryParse(ToDisplayString(value), out var parsed) && parsed;
    }

    private static double ToDouble(object? value, double fallback) =>
        double.TryParse(ToDisplayString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private static DateTime? ToDate(object? value) =>
        DateTime.TryParse(ToDisplayString(value), CultureInfo.InvariantCulture, out var parsed)
            ? parsed : null;

    private static double ReadConstraint(ControlDefinition definition, string key, double fallback)
    {
        if (definition.Constraints is not null && definition.Constraints.TryGetValue(key, out var value))
            return ToDouble(value, fallback);
        return fallback;
    }
}
