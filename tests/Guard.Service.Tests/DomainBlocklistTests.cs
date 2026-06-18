using AdultContentShutdownGuard.Guard.Service.Services;
using Xunit;

namespace Guard.Service.Tests;

public sealed class DomainBlocklistTests
{
    [Theory]
    [InlineData("pornhub.com")]
    [InlineData("www.pornhub.com")]
    [InlineData("WWW.PORNHUB.COM.")]
    [InlineData("deep.sub.pornhub.com")]
    public void IsBlocked_matches_exact_domain_and_subdomains_case_insensitively(string host)
    {
        var blocklist = DomainBlocklist.FromDomains(new[] { "PornHub.com" });

        var blocked = blocklist.IsBlocked(host, out var matchedRule);

        Assert.True(blocked);
        Assert.Equal("pornhub.com", matchedRule);
    }

    [Theory]
    [InlineData("notpornhub.com")]
    [InlineData("pornhub.com.example")]
    [InlineData("example.com")]
    public void IsBlocked_does_not_match_partial_domain_suffixes(string host)
    {
        var blocklist = DomainBlocklist.FromDomains(new[] { "pornhub.com" });

        var blocked = blocklist.IsBlocked(host, out var matchedRule);

        Assert.False(blocked);
        Assert.Null(matchedRule);
    }

    [Fact]
    public void FromDomains_normalizes_unicode_domains_to_ascii()
    {
        var blocklist = DomainBlocklist.FromDomains(new[] { "täst.example" });

        var blocked = blocklist.IsBlocked("xn--tst-qla.example", out var matchedRule);

        Assert.True(blocked);
        Assert.Equal("xn--tst-qla.example", matchedRule);
    }

    [Fact]
    public async Task LoadAsync_splits_whitespace_separated_domains()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "xvideos.com\txnxx.com # grouped legacy line");

        try
        {
            var blocklist = await DomainBlocklist.LoadAsync(new[] { file }, CancellationToken.None);

            Assert.True(blocklist.IsBlocked("xvideos.com", out _));
            Assert.True(blocklist.IsBlocked("xnxx.com", out _));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
