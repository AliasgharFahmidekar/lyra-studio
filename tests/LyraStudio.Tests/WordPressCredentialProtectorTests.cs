using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class WordPressCredentialProtectorTests
{
    [Fact]
    public void ProtectsAndRestoresCredentialForCurrentWindowsUser()
    {
        var protector = new WordPressCredentialProtector();
        const string password = "abcd efgh ijkl mnop";

        var protectedValue = protector.Protect(password);

        Assert.NotEqual(password, protectedValue);
        Assert.DoesNotContain(password, protectedValue);
        Assert.Equal(password, protector.Unprotect(protectedValue));
    }

    [Fact]
    public void EmptyCredentialRemainsEmpty()
    {
        var protector = new WordPressCredentialProtector();

        Assert.Equal(string.Empty, protector.Protect(string.Empty));
        Assert.Equal(string.Empty, protector.Unprotect(string.Empty));
    }
}
