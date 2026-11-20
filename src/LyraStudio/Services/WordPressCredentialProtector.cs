using System.Security.Cryptography;
using System.Text;

namespace LyraStudio.Services;

/// <summary>
/// Protects WordPress Application Passwords with Windows DPAPI for the current user.
/// The application password is never persisted as plaintext.
/// </summary>
public sealed class WordPressCredentialProtector
{
    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(protectedValue);
            var unprotectedBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotectedBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new InvalidOperationException(
                "The saved WordPress Application Password cannot be decrypted for this Windows user. Enter it again in WordPress Setup.",
                ex);
        }
    }
}
