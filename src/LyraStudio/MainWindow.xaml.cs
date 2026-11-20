using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using LyraStudio.Models;
using LyraStudio.Services;

namespace LyraStudio;

public partial class MainWindow : Window
{
    private readonly ProviderManifestLoader _manifestLoader = new();
    private readonly DynamicInspectorBuilder _inspectorBuilder = new();
    private readonly LocalWidgetPreviewBuilder _previewBuilder = new();
    private readonly ObservableCollection<WidgetDescriptor> _widgets = new();
    private readonly ObservableCollection<RecipeDescriptor> _recipes = new();

    private ProviderManifest? _manifest;
    private string? _manifestPath;
    private Dictionary<string, object?> _currentSettings = new();
    private WidgetDescriptor? _selectedWidget;
    private bool _webViewReady;
    private int _previewWidth = 1280;

    public MainWindow()
    {
        InitializeComponent();
        WidgetList.ItemsSource = _widgets;
        RecipeList.ItemsSource = _recipes;
        Loaded += async (_, _) =>
        {
            SetOfflineStatus();
            ShowWelcomePreview();
            await InitializeLocalPreviewAsync();
        };
    }

    private void SetOfflineStatus()
    {
        ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
        ConnectionLabel.Text = "Offline desktop";
        PageSubtitle.Text = "Local workspace · No WordPress server or internet connection required";
    }

    private async Task InitializeLocalPreviewAsync()
    {
        try
        {
            await PreviewBrowser.EnsureCoreWebView2Async();
            PreviewBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webViewReady = true;

            if (_selectedWidget is not null)
                RenderLocalPreview();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Local preview engine could not start · " + ex.Message;
            MessageBox.Show(this,
                "The local preview engine could not start. Run the Lyra Studio Setup package so its browser runtime is installed.\n\n" + ex.Message,
                "Preview engine unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowWelcomePreview()
    {
        CatalogState.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        PreviewScroll.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Collapsed;
        InspectorEmptyState.Visibility = Visibility.Visible;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Collapsed;
    }

    private async void ImportManifest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import local provider manifest",
            Filter = "Lyra Studio manifest (*.json)|*.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
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

            ConnectionLabel.Text = "Local manifest loaded";
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
            StatusText.Text = $"{manifest.ProviderName} {manifest.ProviderVersion} · {_widgets.Count} widgets · {_recipes.Count} recipes · local file";
            PageSubtitle.Text = $"Local provider: {manifest.ProviderName} · Manifest v{manifest.ManifestVersion}";

            if (_widgets.Count > 0)
                ShowWidgetCatalog(selectFirst: true);
            else if (_recipes.Count > 0)
                ShowRecipeCatalog(selectFirst: true);
            else
            {
                EmptyTitle.Text = "Manifest loaded — no catalog entries";
                EmptyDescription.Text = "The local provider manifest is valid, but it contains no widgets or recipes yet.";
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
            ? "Import a local provider manifest to discover widgets."
            : $"Local widgets from {_manifest.ProviderName}";
        CatalogListTitle.Text = "WIDGETS";
        EmptyTitle.Text = "No widget catalog loaded";
        EmptyDescription.Text = "Import a provider manifest stored on this computer. No server connection is required.";
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
            PreviewScroll.Visibility = Visibility.Collapsed;
            SelectionInfo.Visibility = Visibility.Visible;
            PreviewTitle.Text = "Select a widget";
            PreviewSubtitle.Text = $"{_widgets.Count} widgets discovered locally";
            PreviewMessage.Text = "Choose a widget. Its control panel and local preview are generated from the provider manifest.";
        }
    }

    private void ShowRecipeCatalog(bool selectFirst)
    {
        PageTitle.Text = "Recipe Studio";
        PageSubtitle.Text = _manifest is null
            ? "Import a local provider manifest to discover recipes."
            : $"Local recipes from {_manifest.ProviderName}";
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
            PreviewScroll.Visibility = Visibility.Collapsed;
            SelectionInfo.Visibility = Visibility.Visible;
            PreviewTitle.Text = "Select a recipe";
            PreviewSubtitle.Text = $"{_recipes.Count} recipes discovered locally";
            PreviewMessage.Text = "Recipe structure editing and local project persistence are scheduled for upcoming releases.";
        }
    }

    private void WidgetList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WidgetList.SelectedItem is WidgetDescriptor widget)
            ShowWidget(widget);
    }

    private void ShowWidget(WidgetDescriptor widget)
    {
        _selectedWidget = widget;
        EmptyState.Visibility = Visibility.Collapsed;
        PreviewScroll.Visibility = Visibility.Visible;
        SelectionInfo.Visibility = Visibility.Collapsed;

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
        StatusText.Text = $"Local widget · {widget.Name} · {(widget.Controls?.Count ?? 0)} top-level controls";
        if (_webViewReady)
            RenderLocalPreview();
    }

    private void RecipeList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RecipeList.SelectedItem is RecipeDescriptor recipe)
            ShowRecipe(recipe);
    }

    private void ShowRecipe(RecipeDescriptor recipe)
    {
        _selectedWidget = null;
        PreviewScroll.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Visible;
        PreviewTitle.Text = recipe.Name;
        PreviewSubtitle.Text = recipe.Description ?? recipe.Id;
        PreviewMessage.Text = "Recipe metadata is loaded from the local manifest. No website is contacted and no remote content is changed.";

        InspectorEmptyState.Visibility = Visibility.Collapsed;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Visible;
        RecipeInspectorTitle.Text = recipe.Name;
        RecipeInspectorDescription.Text = recipe.Description ?? recipe.Id;
        RecipeInspectorFormat.Text = $"ID: {recipe.Id}\nFormat: {recipe.Format}\nFormat version: {recipe.FormatVersion ?? "Not specified"}";
        StatusText.Text = $"Local recipe · {recipe.Name} · {recipe.Format}";
    }

    private void UpdateSettingsJson()
    {
        SettingsJsonText.Text = SettingsJsonSerializer.Serialize(_currentSettings);
        if (_webViewReady && _selectedWidget is not null)
            RenderLocalPreview();
    }

    private void RenderLocalPreview()
    {
        if (!_webViewReady || _selectedWidget is null)
            return;

        try
        {
            var html = _previewBuilder.BuildDocument(_selectedWidget, _currentSettings, _previewWidth);
            PreviewBrowser.NavigateToString(html);
            PreviewScroll.Visibility = Visibility.Visible;
            SelectionInfo.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Local preview render failed · " + ex.Message;
        }
    }

    private void PreviewDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string device)
            return;

        _previewWidth = device switch
        {
            "mobile" => 390,
            "tablet" => 768,
            _ => 1280
        };
        PreviewBrowser.Width = _previewWidth;
        StatusText.Text = $"Local preview viewport · {device} · {_previewWidth}px";
        RenderLocalPreview();
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

        StatusText.Text = "No local manifest loaded. Import a provider manifest to refresh the catalog.";
    }
}
