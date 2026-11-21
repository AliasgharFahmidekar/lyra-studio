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
    private readonly ProviderPackageImporter _packageImporter = new(_manifestLoader);
    private readonly DynamicInspectorBuilder _inspectorBuilder = new();
    private readonly LocalWidgetPreviewBuilder _previewBuilder = new();
    private readonly ObservableCollection<WidgetDescriptor> _widgets = new();
    private readonly ObservableCollection<RecipeDescriptor> _recipes = new();
    private readonly ObservableCollection<PackageFileDescriptor> _packageFiles = new();

    private ProviderManifest? _manifest;
    private ProviderPackageInspection? _packageInspection;
    private string? _packagePath;
    private Dictionary<string, object?> _currentSettings = new();
    private WidgetDescriptor? _selectedWidget;
    private bool _webViewReady;
    private int _previewWidth = 1280;

    public MainWindow()
    {
        InitializeComponent();
        WidgetList.ItemsSource = _widgets;
        RecipeList.ItemsSource = _recipes;
        PackageFilesList.ItemsSource = _packageFiles;
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

    private async void ImportPackage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import local plugin/provider package",
            Filter = "ZIP packages (*.zip)|*.zip|Provider manifests (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            await LoadPackageAsync(dialog.FileName);
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Import local plugin/provider source folder"
        };

        if (dialog.ShowDialog(this) == true)
            await LoadPackageAsync(dialog.FolderName);
    }

    private async Task LoadPackageAsync(string path)
    {
        try
        {
            var inspection = await _packageImporter.InspectAsync(path);
            _packageInspection = inspection;
            _packagePath = path;
            _packageFiles.Clear();
            foreach (var file in inspection.Files)
                _packageFiles.Add(file);

            if (inspection.Manifest is not null)
            {
                LoadManifestIntoWorkspace(inspection.Manifest);
                var warningSuffix = inspection.Warnings.Count == 0
                    ? ""
                    : $" · {inspection.Warnings.Count} scan warning(s)";
                StatusText.Text =
                    $"{inspection.PackageName} · {inspection.FileCount} files scanned · " +
                    $"{inspection.WidgetCount} widgets · {inspection.RecipeCount} recipes{warningSuffix}";
            }
            else
            {
                _manifest = null;
                _widgets.Clear();
                _recipes.Clear();
                ShowPackageCatalog();
                StatusText.Text = $"{inspection.PackageName} · {inspection.FileCount} files scanned · no provider manifest";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Package import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = "Package import failed · " + ex.Message;
        }
    }

    private void LoadManifestIntoWorkspace(ProviderManifest manifest)
    {
        _manifest = manifest;
        _widgets.Clear();
        foreach (var widget in manifest.Widgets)
            _widgets.Add(widget);
        _recipes.Clear();
        foreach (var recipe in manifest.Recipes)
            _recipes.Add(recipe);

        ConnectionLabel.Text = "Local provider loaded";
        ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
        PageSubtitle.Text = $"Local provider: {manifest.ProviderName} · Manifest v{manifest.ManifestVersion}";

        WidgetList.SelectedIndex = -1;
        RecipeList.SelectedIndex = -1;
        if (_widgets.Count > 0)
            ShowWidgetCatalog(selectFirst: true);
        else if (_recipes.Count > 0)
            ShowRecipeCatalog(selectFirst: true);
        else
        {
            EmptyTitle.Text = "Provider loaded — no catalog entries";
            EmptyDescription.Text = "The provider manifest is valid, but it contains no widgets or recipes yet.";
            ShowWelcomePreview();
        }
    }

    private void Widgets_Click(object sender, RoutedEventArgs e) => ShowWidgetCatalog(selectFirst: false);
    private void Recipes_Click(object sender, RoutedEventArgs e) => ShowRecipeCatalog(selectFirst: false);
    private void Packages_Click(object sender, RoutedEventArgs e) => ShowPackageCatalog();

    private void ShowWidgetCatalog(bool selectFirst)
    {
        PageTitle.Text = "Widget Library";
        PageSubtitle.Text = _manifest is null
            ? "Import a local provider package or manifest to discover widgets."
            : $"Local widgets from {_manifest.ProviderName}";
        CatalogListTitle.Text = "WIDGETS";
        EmptyTitle.Text = "No widget catalog loaded";
        EmptyDescription.Text = "Import a provider package or manifest stored on this computer. No server connection is required.";
        RecipeList.Visibility = Visibility.Collapsed;
        PackageFilesList.Visibility = Visibility.Collapsed;
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
            PreviewMessage.Text = "Choose a widget. Its control panel and local preview are generated from provider metadata.";
        }
    }

    private void ShowRecipeCatalog(bool selectFirst)
    {
        PageTitle.Text = "Recipe Studio";
        PageSubtitle.Text = _manifest is null
            ? "Import a local provider package or manifest to discover recipes."
            : $"Local recipes from {_manifest.ProviderName}";
        CatalogListTitle.Text = "RECIPES";
        EmptyTitle.Text = "No recipe catalog loaded";
        EmptyDescription.Text = "Import a provider package containing recipe descriptors. Recipe formats remain provider-specific.";
        WidgetList.Visibility = Visibility.Collapsed;
        PackageFilesList.Visibility = Visibility.Collapsed;
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
            PreviewMessage.Text = "Recipe structure parsing and editing are scheduled for a later document-model milestone.";
        }
    }

    private void ShowPackageCatalog()
    {
        PageTitle.Text = "Package Explorer";
        PageSubtitle.Text = _packageInspection is null
            ? "Import a plugin ZIP or source folder to inspect its local files."
            : $"{_packageInspection.PackageName} · {_packageInspection.FileCount} files · read-only scan";
        CatalogListTitle.Text = "PACKAGE FILES";
        WidgetList.Visibility = Visibility.Collapsed;
        RecipeList.Visibility = Visibility.Collapsed;
        PackageFilesList.Visibility = Visibility.Visible;

        if (_packageInspection is null)
        {
            ShowWelcomePreview();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        CatalogState.Visibility = Visibility.Visible;
        PreviewScroll.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Visible;
        InspectorEmptyState.Visibility = Visibility.Visible;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Collapsed;
        _selectedWidget = null;

        var summary = string.Join(" · ", _packageInspection.Files
            .GroupBy(file => file.Category)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: {group.Count()}"));

        PreviewTitle.Text = _packageInspection.HasProviderManifest
            ? "Provider package inspected"
            : "No provider manifest found";
        PreviewSubtitle.Text = $"{_packageInspection.FileCount} files scanned · {_packageInspection.WidgetCount} widgets · {_packageInspection.RecipeCount} recipes";
        PreviewMessage.Text = _packageInspection.HasProviderManifest
            ? $"The package manifest was found at '{_packageInspection.ManifestEntryPath}'.\n\n{summary}\n\nWidget and recipe catalogs are available in their workspace sections."
            : $"The package was scanned without extracting or executing its files.\n\n{summary}\n\nThis is a generic file inventory, not semantic widget discovery. To expose native widgets and recipes, the package needs a Lyra Studio manifest or a provider-specific discovery adapter.";
        StatusText.Text = _packageInspection.Warnings.Count == 0
            ? "Read-only package inventory"
            : string.Join(" · ", _packageInspection.Warnings);
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
        PreviewMessage.Text = "Recipe metadata is loaded from the local provider package. Page/template parsing and editing are a later milestone.";

        InspectorEmptyState.Visibility = Visibility.Collapsed;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Visible;
        RecipeInspectorTitle.Text = recipe.Name;
        RecipeInspectorDescription.Text = recipe.Description ?? recipe.Id;
        RecipeInspectorFormat.Text = $"ID: {recipe.Id}\nFormat: {recipe.Format}\nFormat version: {recipe.FormatVersion ?? "Not specified"}";
        StatusText.Text = $"Local recipe · {recipe.Name} · {recipe.Format}";
    }

    private void PackageFilesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PackageFilesList.SelectedItem is not PackageFileDescriptor file || _packageInspection is null)
            return;

        _selectedWidget = null;
        PreviewScroll.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Visible;
        PreviewTitle.Text = file.RelativePath;
        PreviewSubtitle.Text = $"{file.Category} · {file.SizeBytes:N0} bytes";
        PreviewMessage.Text = "This item is listed as inert package data. Lyra Studio has not opened, extracted, or executed its source content.";
        InspectorEmptyState.Visibility = Visibility.Visible;
        WidgetInspector.Visibility = Visibility.Collapsed;
        RecipeInspector.Visibility = Visibility.Collapsed;
        StatusText.Text = $"Package file · {file.Category} · {file.RelativePath}";
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
        if (!string.IsNullOrWhiteSpace(_packagePath))
        {
            await LoadPackageAsync(_packagePath);
            return;
        }

        StatusText.Text = "No local package loaded. Import a plugin ZIP, source folder, or provider manifest to refresh.";
    }
}
