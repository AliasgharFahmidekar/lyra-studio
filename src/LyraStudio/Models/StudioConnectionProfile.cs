namespace LyraStudio.Models;

public sealed class StudioConnectionProfile
{
    public string SiteUrl { get; set; } = "";
    public string WordPressUsername { get; set; } = "";
    /// <summary>DPAPI CurrentUser protected WordPress Application Password, Base64 encoded.</summary>
    public string ProtectedApplicationPassword { get; set; } = "";
}
