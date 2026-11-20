using System.Windows;
using LyraStudio.Models;
using LyraStudio.Services;

namespace LyraStudio;

public partial class ConnectionWindow : Window
{
    private readonly WordPressCredentialProtector _credentialProtector = new();

    public StudioConnectionProfile Profile { get; private set; }

    public ConnectionWindow(StudioConnectionProfile current)
    {
        InitializeComponent();

        Profile = new StudioConnectionProfile
        {
            SiteUrl = current.SiteUrl,
            WordPressUsername = current.WordPressUsername,
            ProtectedApplicationPassword = current.ProtectedApplicationPassword
        };

        SiteUrlBox.Text = current.SiteUrl;
        WordPressUsernameBox.Text = current.WordPressUsername;

        if (!string.IsNullOrWhiteSpace(current.ProtectedApplicationPassword))
        {
            try
            {
                ApplicationPasswordBox.Password = _credentialProtector.Unprotect(current.ProtectedApplicationPassword);
            }
            catch (InvalidOperationException)
            {
                ClearSavedPasswordCheckBox.IsChecked = true;
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Profile.SiteUrl = SiteUrlBox.Text.Trim().TrimEnd('/');
        Profile.WordPressUsername = WordPressUsernameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(Profile.SiteUrl))
        {
            MessageBox.Show(this, "Enter your WordPress site URL.", "Missing URL",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ClearSavedPasswordCheckBox.IsChecked == true)
            Profile.ProtectedApplicationPassword = string.Empty;
        else if (!string.IsNullOrWhiteSpace(ApplicationPasswordBox.Password))
            Profile.ProtectedApplicationPassword = _credentialProtector.Protect(ApplicationPasswordBox.Password.Trim());

        DialogResult = true;
        ApplicationPasswordBox.Password = string.Empty;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ApplicationPasswordBox.Password = string.Empty;
        DialogResult = false;
    }
}
