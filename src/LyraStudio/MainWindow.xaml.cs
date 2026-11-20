using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly WordPressCredentialProtector _credentialProtector = new();
    private readonly PreviewBridgeClient _previewBridgeClient = new();
    private readonly ObservableCollection<WidgetDescriptor> _widgets = new();
    private readonly ObservableCollection<RecipeDescriptor> _recipes = new();
    private readonly DispatcherTimer _previewUpdateTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(650)
    };

    private StudioConnectionProfile _profile = new();
    private ProviderManifest? _manifest;
    private string? _manifestPath;
    private Dictionary<string, object?> _currentSettings = new();
    private WordPressSiteCapabilities? _siteCapabilities;
    private PreviewBridgeCapabilities? _bridgeCapabilities;
    private WidgetDescriptor? _selectedWidget;
    private BridgePreviewSession? _activePreview;
    private string? _activePreviewWidgetId;
    private bool _previewUpdateInProgress;
    private bool _previewUpdatePending;
    private bool _finalCloseRequested;

    public MainWindow()
    {
        InitializeComponent();
        WidgetList.ItemsSource = _widgets;
        RecipeList.ItemsSource = _recipes;
        _previewUpdateTimer.Tick += async (_, _) =>
        {
            _previewUpdateTimer.Stop();
            await UpdateActivePreviewAsync();
        };
        Closing += MainWindow_Closing;

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
        CreatePreviewButton.IsEnabled = false;
        CreatePreviewButton.Visibility = Visibility.Collapsed;
        PreviewHint.Text = "Connect a site with the optional Preview Bridge to render this widget.";
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionWindow(_profile) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        await CloseActivePreviewAsync(showStatus: false);
        _profile = dialog.Profile;
        await _profileStore.SaveAsync(_profile);
        StatusText.Text = "Checking WordPress REST API…";

        try
        {
            var capabilities = await _connectionProbe.CheckAsync(_profile.SiteUrl);
            _siteCapabilities = capabilities;
            _bridgeCapabilities = null;

            var bridgeMessage = await InspectPreviewBridgeAsync(capabilities.RestApiRoot);
            ShowWelcomePreview();

            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
            ConnectionLabel.Text = _bridgeCapabilities?.ElementorReady == true
                ? "Preview Bridge ready"
                : "REST index inspected";
            StatusText.Text = $"Read-only WordPress discovery · {capabilities.DiscoveredRouteCount} routes";
            PageSubtitle.Text = $"REST discovery completed · {capabilities.Namespaces.Count} namespaces";
            EmptyTitle.Text = "WordPress capabilities inspected";
            EmptyDescription.Text = capabilities.ToDisplayText() + Environment.NewLine + Environment.NewLine + bridgeMessage;
        }
        catch (Exception ex)
        {
            _siteCapabilities = null;
            _bridgeCapabilities = null;
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(210, 82, 82));
            ConnectionLabel.Text = "Connection failed";
            StatusText.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Connection failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<string> InspectPreviewBridgeAsync(Uri restApiRoot)
    {
        if (string.IsNullOrWhiteSpace(_profile.WordPressUsername) ||
            string.IsNullOrWhiteSpace(_profile.ProtectedApplicationPassword))
        {
            return "Preview Bridge: enter a WordPress username and a dedicated Application Password in WordPress Setup to enable authenticated preview requests.";
        }

        try
        {
            var password = _credentialProtector.Unprotect(_profile.ProtectedApplicationPassword);
            var bridge = await _previewBridgeClient.GetCapabilitiesAsync(
                restApiRoot, _profile.WordPressUsername, password);

            _bridgeCapabilities = bridge;
            if (!bridge.ElementorReady)
                return $"Preview Bridge {bridge.BridgeVersion} is installed, but Elementor is not ready on this site.";

            return $"Preview Bridge {bridge.BridgeVersion} is ready · Elementor {bridge.ElementorVersion ?? "version unknown"} · {bridge.WidgetTypes.Count} widget types reported.";
        }
        catch (Exception ex)
        {
            _bridgeCapabilities = null;
            return "Preview Bridge is unavailable: " + ex.Message;
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
            CreatePreviewButton.IsEnabled = false;
            PreviewTitle.Text = "Select a widget";
            PreviewSubtitle.Text = $"{_widgets.Count} discovered widgets";
            PreviewMessage.Text = "Selecting a widget generates its inspector from the imported control schema.";
            PreviewHint.Text = "A live render requires the optional WordPress Preview Bridge.";
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
            CreatePreviewButton.IsEnabled = false;
            PreviewTitle.Text = "Select a recipe";
            PreviewSubtitle.Text = $"{_recipes.Count} discovered recipes";
            PreviewMessage.Text = "Recipe descriptors are discovered from the provider manifest. Editing page structure is scheduled for v0.4.0.";
        }
    }

    private void WidgetList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WidgetList.SelectedItem is WidgetDescriptor widget)
            ShowWidget(widget);
    }

    private void ShowWidget(WidgetDescriptor widget)
    {
        if (_activePreview is not null && !string.Equals(_activePreviewWidgetId, widget.Id, StringComparison.Ordinal))
            _ = CloseActivePreviewAsync(showStatus: false);

        _selectedWidget = widget;
        PreviewBrowser.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Visible;
        PreviewTitle.Text = widget.Name;
        PreviewSubtitle.Text = string.IsNullOrWhiteSpace(widget.Category) ? widget.Id : $"{widget.Category} · {widget.Id}";
        PreviewMessage.Text = "Widget metadata loaded. Lyra Studio will ask the connected WordPress site to render this exact registered Elementor widget in a temporary draft.";

        var widgetRegistered = _bridgeCapabilities?.ElementorReady == true &&
            _bridgeCapabilities.WidgetTypes.Contains(widget.Id, StringComparer.OrdinalIgnoreCase);
        CreatePreviewButton.Visibility = Visibility.Visible;
        CreatePreviewButton.IsEnabled = widgetRegistered;
        PreviewHint.Text = widgetRegistered
            ? "Preview opens a temporary draft. The WebView may ask you to sign in to WordPress."
            : _bridgeCapabilities?.ElementorReady == true
                ? $"The connected Elementor site did not report widget type '{widget.Id}'. Check the provider manifest widget ID."
                : "Connect WordPress with the optional Lyra Studio Preview Bridge and an Application Password.";

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
        if (_activePreview is not null)
            _ = CloseActivePreviewAsync(showStatus: false);

        _selectedWidget = null;
        PreviewBrowser.Visibility = Visibility.Collapsed;
        SelectionInfo.Visibility = Visibility.Visible;
        PreviewTitle.Text = recipe.Name;
        PreviewSubtitle.Text = recipe.Description ?? recipe.Id;
        PreviewMessage.Text = "Recipe descriptor loaded. No page is rendered or modified. Import/export and visual structure editing are planned for v0.4.0.";
        CreatePreviewButton.IsEnabled = false;
        CreatePreviewButton.Visibility = Visibility.Collapsed;

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
        SettingsJsonText.Text = SettingsJsonSerializer.Serialize(_currentSettings);
        if (_activePreview is not null && _selectedWidget is not null &&
            string.Equals(_activePreviewWidgetId, _selectedWidget.Id, StringComparison.Ordinal))
        {
            _previewUpdateTimer.Stop();
            _previewUpdateTimer.Start();
        }
    }

    private async void CreatePreview_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null || _siteCapabilities is null || _bridgeCapabilities?.ElementorReady != true)
            return;

        if (string.IsNullOrWhiteSpace(_profile.WordPressUsername) ||
            string.IsNullOrWhiteSpace(_profile.ProtectedApplicationPassword))
        {
            MessageBox.Show(this, "Open WordPress Setup and enter a dedicated Application Password first.",
                "Preview credentials required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await CloseActivePreviewAsync(showStatus: false);
            var password = _credentialProtector.Unprotect(_profile.ProtectedApplicationPassword);
            var session = await _previewBridgeClient.CreatePreviewAsync(
                _siteCapabilities.RestApiRoot,
                _profile.WordPressUsername,
                password,
                _selectedWidget.Id,
                _selectedWidget.Name,
                _currentSettings);

            _activePreview = session;
            _activePreviewWidgetId = _selectedWidget.Id;
            PreviewBrowser.Visibility = Visibility.Visible;
            SelectionInfo.Visibility = Visibility.Collapsed;
            ClosePreviewButton.IsEnabled = true;
            PreviewBrowser.Source = session.GetSafePreviewUri();
            StatusText.Text = $"Temporary Elementor draft #{session.PostId} created · expires {session.ExpiresAt.LocalDateTime:g}. Sign in inside the preview browser if prompted.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Preview could not be created · " + ex.Message;
            MessageBox.Show(this, ex.Message, "Elementor preview failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ClosePreview_Click(object sender, RoutedEventArgs e)
    {
        await CloseActivePreviewAsync(showStatus: true);
    }

    private async Task CloseActivePreviewAsync(bool showStatus)
    {
        var session = _activePreview;
        if (session is null) return;

        _previewUpdateTimer.Stop();
        _activePreview = null;
        _activePreviewWidgetId = null;
        _previewUpdatePending = false;

        try
        {
            if (_siteCapabilities is not null &&
                !string.IsNullOrWhiteSpace(_profile.WordPressUsername) &&
                !string.IsNullOrWhiteSpace(_profile.ProtectedApplicationPassword))
            {
                var password = _credentialProtector.Unprotect(_profile.ProtectedApplicationPassword);
                await _previewBridgeClient.DeletePreviewAsync(
                    _siteCapabilities.RestApiRoot,
                    _profile.WordPressUsername,
                    password,
                    session.SessionId);
            }

            if (showStatus)
                StatusText.Text = $"Temporary preview session {session.SessionId} was closed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "The bridge could not confirm preview deletion. Its server-side expiry cleanup will retry: " + ex.Message;
            if (showStatus)
                MessageBox.Show(this, ex.Message + Environment.NewLine + "The draft is protected by its server-side expiry cleanup.",
                    "Preview cleanup pending", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            PreviewBrowser.Source = new Uri("about:blank");
            PreviewBrowser.Visibility = Visibility.Collapsed;
            SelectionInfo.Visibility = Visibility.Visible;
            ClosePreviewButton.IsEnabled = false;
        }
    }

    private async Task UpdateActivePreviewAsync()
    {
        if (_previewUpdateInProgress)
        {
            _previewUpdatePending = true;
            return;
        }

        if (_activePreview is null || _selectedWidget is null || _siteCapabilities is null ||
            !string.Equals(_activePreviewWidgetId, _selectedWidget.Id, StringComparison.Ordinal))
            return;

        _previewUpdateInProgress = true;
        try
        {
            do
            {
                _previewUpdatePending = false;
                var session = _activePreview;
                if (session is null) break;

                var password = _credentialProtector.Unprotect(_profile.ProtectedApplicationPassword);
                var updated = await _previewBridgeClient.UpdatePreviewAsync(
                    _siteCapabilities.RestApiRoot,
                    _profile.WordPressUsername,
                    password,
                    session.SessionId,
                    _selectedWidget.Id,
                    _selectedWidget.Name,
                    _currentSettings);

                if (_activePreview?.SessionId == session.SessionId)
                {
                    _activePreview = updated;
                    PreviewBrowser.Source = updated.GetSafePreviewUri();
                    StatusText.Text = $"Preview draft #{updated.PostId} updated · {updated.ExpiresAt.LocalDateTime:g}";
                }
            }
            while (_previewUpdatePending && _activePreview is not null);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Live preview update failed · " + ex.Message;
        }
        finally
        {
            _previewUpdateInProgress = false;
        }
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

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_finalCloseRequested || _activePreview is null) return;

        e.Cancel = true;
        _finalCloseRequested = true;
        await CloseActivePreviewAsync(showStatus: false);
        Close();
    }
}
