using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>Builds inspector controls from provider-supplied metadata.</summary>
public sealed class DynamicInspectorBuilder
{
    public void Build(Panel target, IReadOnlyList<ControlDefinition>? definitions,
        IDictionary<string, object?> values, Action onChanged)
        => BuildPanel(target, definitions, values, null, onChanged);

    private static void BuildPanel(Panel target, IReadOnlyList<ControlDefinition>? definitions,
        IDictionary<string, object?> values, string? pathPrefix, Action onChanged)
    {
        target.Children.Clear();
        if (definitions is null || definitions.Count == 0)
        {
            target.Children.Add(new TextBlock
            {
                Text = "No configurable fields were provided for this section.",
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
                item.Element.Visibility = ConditionEvaluator.Matches(
                    item.Definition.Conditions, values, pathPrefix)
                    ? Visibility.Visible : Visibility.Collapsed;
        };

        foreach (var definition in definitions)
            target.Children.Add(BuildControl(definition, pathPrefix, values,
                conditionalElements, refreshConditions, onChanged));

        refreshConditions();
    }

    private static FrameworkElement BuildControl(ControlDefinition definition, string? parentPath,
        IDictionary<string, object?> values,
        ICollection<(FrameworkElement Element, ControlDefinition Definition)> conditionalElements,
        Action refreshConditions, Action onChanged)
    {
        var key = string.IsNullOrWhiteSpace(parentPath) ? definition.Name : $"{parentPath}.{definition.Name}";
        if (!values.ContainsKey(key)) values[key] = definition.DefaultValue;

        var wrapper = new Border { Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(0, 0, 0, 2) };
        var body = new StackPanel();
        wrapper.Child = body;
        var kind = NormalizeKind(definition.Kind);

        if (kind == "group")
        {
            var nested = new StackPanel { Margin = new Thickness(10, 8, 0, 0) };
            BuildPanel(nested, definition.Children, values, key, onChanged);
            body.Children.Add(new Expander
            {
                Header = definition.Label,
                Content = nested,
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold
            });
        }
        else if (kind == "repeater")
        {
            body.Children.Add(BuildRepeater(definition, key, values, refreshConditions, onChanged));
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
                body.Children.Add(new TextBlock
                {
                    Text = definition.Description,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                });

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
                        TickFrequency = Math.Max(0.01, ReadConstraint(definition, "step", 1)),
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

                case "typography":
                case "dimensions":
                case "unknown":
                    var complexEditor = CreateTextEditor(current, multiline: true);
                    complexEditor.ToolTip = kind == "unknown"
                        ? $"Unrecognized control kind '{definition.Kind}'. Value is preserved as text/JSON."
                        : "Edit the structured value as JSON.";
                    complexEditor.LostFocus += (_, _) =>
                    {
                        if (TryParseJson(complexEditor.Text, out var parsed))
                        {
                            complexEditor.Background = Brushes.White;
                            Save(parsed);
                        }
                        else if (kind == "unknown")
                        {
                            complexEditor.Background = Brushes.MistyRose;
                            Save(complexEditor.Text);
                        }
                        else complexEditor.Background = Brushes.MistyRose;
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
                        else Save(textEditor.Text);
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

    private static FrameworkElement BuildRepeater(ControlDefinition definition, string key,
        IDictionary<string, object?> values, Action refreshConditions, Action onChanged)
    {
        var minimum = Math.Max(0, RepeaterValueHelper.ReadLimit(definition, "min_items", 0));
        var configuredMaximum = RepeaterValueHelper.ReadLimit(definition, "max_items", 100);
        var maximum = configuredMaximum <= 0 ? int.MaxValue : Math.Max(minimum, configuredMaximum);
        var items = RepeaterValueHelper.ReadItems(values[key], definition.Children);
        var root = new StackPanel();

        root.Children.Add(new TextBlock
        {
            Text = definition.Label,
            Foreground = new SolidColorBrush(Color.FromRgb(55, 63, 78)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 5)
        });
        if (!string.IsNullOrWhiteSpace(definition.Description))
            root.Children.Add(new TextBlock
            {
                Text = definition.Description,
                Foreground = Brushes.Gray,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

        var header = new DockPanel { Margin = new Thickness(0, 2, 0, 8) };
        var countLabel = new TextBlock { Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, FontSize = 10 };
        DockPanel.SetDock(countLabel, Dock.Left);
        header.Children.Add(countLabel);

        var addButton = CreateActionButton("+ Add item", "#7567E8");
        DockPanel.SetDock(addButton, Dock.Right);
        header.Children.Add(addButton);
        root.Children.Add(header);

        var itemList = new StackPanel();
        root.Children.Add(itemList);

        void CommitItems()
        {
            values[key] = items;
            refreshConditions();
            onChanged();
        }

        void RenderItems()
        {
            itemList.Children.Clear();
            countLabel.Text = maximum == int.MaxValue
                ? $"{items.Count} item(s) · min {minimum}, no maximum"
                : $"{items.Count} item(s) · min {minimum}, max {maximum}";
            addButton.IsEnabled = items.Count < maximum;

            if (items.Count == 0)
                itemList.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(225, 229, 236)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(12),
                    Child = new TextBlock
                    {
                        Text = "No items yet. Use Add item to create the first entry.",
                        Foreground = Brushes.Gray,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap
                    }
                });

            for (var index = 0; index < items.Count; index++)
            {
                var capturedIndex = index;
                var card = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(225, 229, 236)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Background = Brushes.White
                };
                var content = new StackPanel();
                var itemHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 9) };
                var itemTitle = new TextBlock
                {
                    Text = $"Item {index + 1}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(55, 63, 78)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(itemTitle, Dock.Left);
                itemHeader.Children.Add(itemTitle);

                var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var upButton = CreateActionButton("↑", "#EEF0F5");
                upButton.Foreground = new SolidColorBrush(Color.FromRgb(55, 63, 78));
                upButton.Padding = new Thickness(8, 4, 8, 4);
                upButton.Margin = new Thickness(3, 0, 0, 0);
                upButton.IsEnabled = index > 0;
                upButton.Click += (_, _) =>
                {
                    if (RepeaterValueHelper.TryMove(items, capturedIndex, capturedIndex - 1))
                    {
                        CommitItems();
                        RenderItems();
                    }
                };

                var downButton = CreateActionButton("↓", "#EEF0F5");
                downButton.Foreground = new SolidColorBrush(Color.FromRgb(55, 63, 78));
                downButton.Padding = new Thickness(8, 4, 8, 4);
                downButton.Margin = new Thickness(3, 0, 0, 0);
                downButton.IsEnabled = index < items.Count - 1;
                downButton.Click += (_, _) =>
                {
                    if (RepeaterValueHelper.TryMove(items, capturedIndex, capturedIndex + 1))
                    {
                        CommitItems();
                        RenderItems();
                    }
                };

                var removeButton = CreateActionButton("Remove", "#FCEBEC");
                removeButton.Foreground = new SolidColorBrush(Color.FromRgb(174, 54, 65));
                removeButton.Padding = new Thickness(9, 4, 9, 4);
                removeButton.Margin = new Thickness(3, 0, 0, 0);
                removeButton.IsEnabled = items.Count > minimum;
                removeButton.Click += (_, _) =>
                {
                    if (RepeaterValueHelper.TryRemove(items, capturedIndex, minimum))
                    {
                        CommitItems();
                        RenderItems();
                    }
                };

                actions.Children.Add(upButton);
                actions.Children.Add(downButton);
                actions.Children.Add(removeButton);
                DockPanel.SetDock(actions, Dock.Right);
                itemHeader.Children.Add(actions);
                content.Children.Add(itemHeader);

                var fields = new StackPanel();
                if (definition.Children is { Count: > 0 })
                    BuildPanel(fields, definition.Children, items[index], null, CommitItems);
                else
                    fields.Children.Add(new TextBlock
                    {
                        Text = "This repeater has no item schema. The item will be preserved as JSON.",
                        Foreground = Brushes.Gray,
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap
                    });

                content.Children.Add(fields);
                card.Child = content;
                itemList.Children.Add(card);
            }
        }

        addButton.Click += (_, _) =>
        {
            if (RepeaterValueHelper.TryAdd(items, definition.Children, maximum))
            {
                CommitItems();
                RenderItems();
            }
        };

        while (items.Count < minimum)
            RepeaterValueHelper.TryAdd(items, definition.Children, maximum);

        values[key] = items;
        RenderItems();
        return root;
    }

    private static Button CreateActionButton(string text, string background) => new()
    {
        Content = text,
        Background = (Brush)new BrushConverter().ConvertFromString(background)!,
        Foreground = Brushes.White,
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Padding = new Thickness(10, 5, 10, 5),
        BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static TextBox CreateTextEditor(object? value, bool multiline)
    {
        var display = value is IDictionary<string, object?> dictionary
            ? JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { WriteIndented = true })
            : ToDisplayString(value);
        return new TextBox
        {
            Text = display,
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
        "repeater" => "repeater",
        "typography" or "dimensions" => (kind ?? string.Empty).Trim().ToLowerInvariant(),
        _ => "unknown"
    };

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

    private static bool ToBoolean(object? value) =>
        value is bool boolean ? boolean : bool.TryParse(ToDisplayString(value), out var parsed) && parsed;

    private static double ToDouble(object? value, double fallback) =>
        double.TryParse(ToDisplayString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private static DateTime? ToDate(object? value) =>
        DateTime.TryParse(ToDisplayString(value), CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static double ReadConstraint(ControlDefinition definition, string key, double fallback) =>
        definition.Constraints is not null && definition.Constraints.TryGetValue(key, out var value)
            ? ToDouble(value, fallback) : fallback;
}
