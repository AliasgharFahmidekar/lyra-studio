using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using LyraStudio.Models;
using LyraStudio.Services;

namespace LyraStudio;

public partial class MainWindow : Window
{
    private readonly ConnectionProfileStore _profileStore = new();
    private readonly WordPressConnectionProbe _connectionProbe = new();
    private readonly ProviderManifestLoader _manifestLoader = new();
    private readonly DynamicInspectorBuilder _inspectorBuilder = new();
    private readonly ObservableCollection<WidgetDescriptor> _widgets = new();
    private readonly ObservableCollection<RecipeDescriptor> _recipes = new();
    private StudioConnectionProfile _profile = new();
    private ProviderManifest? _manifest;
    private string? _manifestPath;
    private Dictionary<string, object?> _currentSettings = new();

    public MainWindow()
    {
        InitializeComponent();
        WidgetList.ItemsSource = _widgets;
        RecipeList.ItemsSource = _recipes;

        Loaded += async (_, _) =>
        {
            _profile = await _profileStore.LoadAsync();
            if (!string.IsNullOrWhiteSpace(_profile.SiteUrl))
                StatusText.Text = $"Saved site: {_profile.SiteUrl} · Not connected";
            ShowWelcomePreview();
        };
    }

    private void ShowWelcomePreview()
    {
        CatalogState.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        SelectionInfo.Visibility = Visibility.Collapsed;
        InspectorEmptyState.Visibility = Visibility.Visible;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Collapsed;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionWindow(_profile) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _profile = dialog.Profile;
        await _profileStore.SaveAsync(_profile);
        StatusText.Text = "Checking WordPress REST API…";
        try
        {
            await _connectionProbe.CheckAsync(_profile.SiteUrl);
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
            ConnectionLabel.Text = "WordPress reachable";
            StatusText.Text = $"WordPress REST API reachable · {_profile.SiteUrl}";
            PageSubtitle.Text = "Connectivity verified. Widget discovery requires a compatible provider manifest or adapter.";
            EmptyTitle.Text = "WordPress connection verified";
            EmptyDescription.Text = "The site responds to the WordPress REST API. This confirms connectivity only; it does not imply that the site exposes a Lyra Studio widget catalog or live preview capability.";
        }
        catch (Exception ex)
        {
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(210, 82, 82));
            ConnectionLabel.Text = "Connection failed";
            StatusText.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Connection failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ImportManifest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import provider manifest",
            Filter = "Lyra Studio manifest (*.json)|*.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;
        await LoadManifestAsync(dialog.FileName);
    }

    private async Task LoadManifestAsync(string path)
    {
        try
        {
            var manifest = await _manifestLoader.LoadAsync(path);
            _manifest = manifest;
            _manifestPath = path;

            _widgets.Clear();
            foreach (var widget in manifest.Widgets) _widgets.Add(widget);
            _recipes.Clear();
            foreach (var recipe in manifest.Recipes) _recipes.Add(recipe);

            ConnectionLabel.Text = "Manifest loaded";
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
            StatusText.Text = $"{manifest.ProviderName} {manifest.ProviderVersion} · {_widgets.Count} widgets · {_recipes.Count} recipes";
            PageSubtitle.Text = $"Provider: {manifest.ProviderName} · Manifest v{manifest.ManifestVersion}";

            if (_widgets.Count > 0)
                ShowWidgetCatalog(selectFirst: true);
            else if (_recipes.Count > 0)
                ShowRecipeCatalog(selectFirst: true);
            else
            {
                EmptyTitle.Text = "Manifest loaded — no catalog entries";
                EmptyDescription.Text = "The provider manifest is valid, but it contains no widgets or recipes yet.";
                ShowWelcomePreview();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Manifest import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = "Manifest import failed · " + ex.Message;
        }
    }

    private void Widgets_Click(object sender, RoutedEventArgs e) => ShowWidgetCatalog(selectFirst: false);

    private void Recipes_Click(object sender, RoutedEventArgs e) => ShowRecipeCatalog(selectFirst: false);

    private void ShowWidgetCatalog(bool selectFirst)
    {
        PageTitle.Text = "Widget Library";
        PageSubtitle.Text = _manifest is null
            ? "Import a provider manifest to discover widgets."
            : $"Widgets from {_manifest.ProviderName}";
        CatalogListTitle.Text = "WIDGETS";
        EmptyTitle.Text = "No widget catalog loaded";
        EmptyDescription.Text = "Import a provider manifest containing widget descriptors. The desktop app does not assume a fixed list of Lyra widgets.";
        RecipeList.Visibility = Visibility.Collapsed;
        WidgetList.Visibility = Visibility.Visible;

        if (_widgets.Count == 0)
        {
            ShowWelcomePreview();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        CatalogState.Visibility = Visibility.Visible;
        if (selectFirst && WidgetList.SelectedIndex < 0)
            WidgetList.SelectedIndex = 0;
        else if (WidgetList.SelectedItem is WidgetDescriptor widget)
            ShowWidget(widget);
        else
        {
            SelectionInfo.Visibility = Visibility.Visible;
            PreviewTitle.Text = "Select a widget";
            PreviewSubtitle.Text = $"{_widgets.Count} discovered widgets";
            PreviewMessage.Text = "Selecting a widget generates its inspector from the imported control schema. A real preview needs a compatible render provider.";
        }
    }

    private void ShowRecipeCatalog(bool selectFirst)
    {
        PageTitle.Text = "Recipe Studio";
        PageSubtitle.Text = _manifest is null
            ? "Import a provider manifest to discover recipes."
            : $"Recipes from {_manifest.ProviderName}";
        CatalogListTitle.Text = "RECIPES";
        EmptyTitle.Text = "No recipe catalog loaded";
        EmptyDescription.Text = "Import a provider manifest containing recipe descriptors. Recipe formats remain provider-specific.";
        WidgetList.Visibility = Visibility.Collapsed;
        RecipeList.Visibility = Visibility.Visible;

        if (_recipes.Count == 0)
        {
            ShowWelcomePreview();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        CatalogState.Visibility = Visibility.Visible;
        if (selectFirst && RecipeList.SelectedIndex < 0)
            RecipeList.SelectedIndex = 0;
        else if (RecipeList.SelectedItem is RecipeDescriptor recipe)
            ShowRecipe(recipe);
        else
        {
            SelectionInfo.Visibility = Visibility.Visible;
            PreviewTitle.Text = "Select a recipe";
            PreviewSubtitle.Text = $"{_recipes.Count} discovered recipes";
            PreviewMessage.Text = "Recipe descriptors are discovered from the provider manifest. Editing page structure is scheduled for a later release.";
        }
    }

    private void WidgetList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WidgetList.SelectedItem is WidgetDescriptor widget)
            ShowWidget(widget);
    }

    private void ShowWidget(WidgetDescriptor widget)
    {
        SelectionInfo.Visibility = Visibility.Visible;
        PreviewTitle.Text = widget.Name;
        PreviewSubtitle.Text = string.IsNullOrWhiteSpace(widget.Category) ? widget.Id : $"{widget.Category} · {widget.Id}";
        PreviewMessage.Text = "Widget metadata loaded. This is not a rendered preview. The live preview surface will be enabled only when a compatible provider can request actual WordPress + Elementor output.";

        InspectorEmptyState.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Collapsed;
        WidgetInspector.Visibility = Visibility.Visible;
        InspectorTitle.Text = widget.Name;
        InspectorDescription.Text = string.IsNullOrWhiteSpace(widget.Description)
            ? $"{widget.Id} · {widget.Version ?? "Version not specified"}"
            : $"{widget.Description}\n{widget.Id} · {widget.Version ?? "Version not specified"}";

        _currentSettings = new Dictionary<string, object?>();
        _inspectorBuilder.Build(InspectorFields, widget.Controls, _currentSettings, UpdateSettingsJson);
        UpdateSettingsJson();
        StatusText.Text = $"Selected widget · {widget.Name} · {(widget.Controls?.Count ?? 0)} top-level controls";
    }

    private void RecipeList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RecipeList.SelectedItem is RecipeDescriptor recipe)
            ShowRecipe(recipe);
    }

    private void ShowRecipe(RecipeDescriptor recipe)
    {
        SelectionInfo.Visibility = Visibility.Visible;
        PreviewTitle.Text = recipe.Name;
        PreviewSubtitle.Text = recipe.Description ?? recipe.Id;
        PreviewMessage.Text = "Recipe descriptor loaded. No page is rendered or modified. Import/export and visual structure editing are planned for v0.4.0.";

        InspectorEmptyState.Visibility = Visibility.Collapsed;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Visible;
        RecipeInspectorTitle.Text = recipe.Name;
        RecipeInspectorDescription.Text = recipe.Description ?? recipe.Id;
        RecipeInspectorFormat.Text = $"ID: {recipe.Id}\nFormat: {recipe.Format}\nFormat version: {recipe.FormatVersion ?? "Not specified"}";
        StatusText.Text = $"Selected recipe · {recipe.Name} · {recipe.Format}";
    }

    private void UpdateSettingsJson()
    {
        SettingsJsonText.Text = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private void CopySettings_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SettingsJsonText.Text)) return;
        Clipboard.SetText(SettingsJsonText.Text);
        StatusText.Text = "Widget settings JSON copied to clipboard.";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_manifestPath))
        {
            await LoadManifestAsync(_manifestPath);
            return;
        }

        StatusText.Text = "No manifest loaded. Import a provider manifest to refresh the catalog.";
    }
}
