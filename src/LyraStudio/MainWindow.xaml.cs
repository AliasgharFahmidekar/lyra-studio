using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using LyraStudio.Services;
using LyraStudio.Models;

namespace LyraStudio;

public partial class MainWindow : Window
{
    private readonly ConnectionProfileStore _profileStore = new();
    private readonly WordPressConnectionProbe _connectionProbe = new();
    private StudioConnectionProfile _profile = new();

    public MainWindow()
    {
        InitializeComponent();
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
        PreviewBrowser.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
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
            var result = await _connectionProbe.CheckAsync(_profile.SiteUrl);
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(37, 170, 112));
            ConnectionLabel.Text = "WordPress reachable";
            StatusText.Text = $"Connected to WordPress REST API · {_profile.SiteUrl}";
            PageSubtitle.Text = "WordPress is reachable. Widget discovery will be enabled when a compatible provider is available.";
            EmptyTitle.Text = "WordPress connection verified";
            EmptyDescription.Text = "The site responds to the WordPress REST API. This confirms connectivity only; it does not imply that Lyra Core exposes a Studio discovery or preview API yet.";
        }
        catch (Exception ex)
        {
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(210, 82, 82));
            ConnectionLabel.Text = "Connection failed";
            StatusText.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Connection failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Widgets_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Widget Library";
        PageSubtitle.Text = "Discover widgets from the connected provider.";
        EmptyTitle.Text = "No widget provider discovered";
        EmptyDescription.Text = "A reachable WordPress site is not enough to infer widget schemas safely. The next integration step is a read-only provider adapter that reports available widgets and their control definitions.";
        ShowWelcomePreview();
    }

    private void Recipes_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Recipe Studio";
        PageSubtitle.Text = "Browse and inspect page recipes.";
        EmptyTitle.Text = "Recipe provider not available";
        EmptyDescription.Text = "Recipe support is designed to be provider-driven. Future recipe formats and page structures can be added without changing the desktop shell.";
        ShowWelcomePreview();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Nothing to refresh yet. Connect a compatible widget provider first.";
    }
}
