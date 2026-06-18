using AdultContentShutdownGuard.Guard.Service.Services;
using Xunit;

namespace Guard.Service.Tests;

public sealed class ManagedBrowserEndpointTokenValidatorTests
{
    [Fact]
    public void IsValid_rejects_placeholder_token_even_if_header_matches()
    {
        var valid = ManagedBrowserEndpointTokenValidator.IsValid("CHANGE_THIS_SECRET_TOKEN", "CHANGE_THIS_SECRET_TOKEN");

        Assert.False(valid);
    }

    [Fact]
    public void IsValid_accepts_exact_non_placeholder_token()
    {
        var valid = ManagedBrowserEndpointTokenValidator.IsValid("random-token", "random-token");

        Assert.True(valid);
    }

    [Fact]
    public void IsValid_rejects_wrong_or_empty_token()
    {
        Assert.False(ManagedBrowserEndpointTokenValidator.IsValid("random-token", "wrong"));
        Assert.False(ManagedBrowserEndpointTokenValidator.IsValid("random-token", ""));
        Assert.False(ManagedBrowserEndpointTokenValidator.IsValid("", "random-token"));
    }
}
