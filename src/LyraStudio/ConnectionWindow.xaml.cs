using System.Windows;
using LyraStudio.Models;

namespace LyraStudio;

public partial class ConnectionWindow : Window
{
    public StudioConnectionProfile Profile { get; private set; }

    public ConnectionWindow(StudioConnectionProfile current)
    {
        InitializeComponent();
        Profile = new StudioConnectionProfile { SiteUrl = current.SiteUrl };
        SiteUrlBox.Text = current.SiteUrl;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Profile.SiteUrl = SiteUrlBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(Profile.SiteUrl))
        {
            MessageBox.Show(this, "Enter your WordPress site URL.", "Missing URL", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
