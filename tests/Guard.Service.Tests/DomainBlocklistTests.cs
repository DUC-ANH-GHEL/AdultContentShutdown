using AdultContentShutdownGuard.Guard.Service.Services;
using AdultContentShutdownGuard.Guard.Service.Models;
using System.IO.Compression;
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

    [Fact]
    public async Task LoadAsync_reads_gzip_hosts_file_and_ignores_non_domains()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt.gz");
        try
        {
            await using (var output = File.Create(file))
            await using (var gzip = new GZipStream(output, CompressionMode.Compress))
            await using (var writer = new StreamWriter(gzip))
            {
                await writer.WriteLineAsync("0.0.0.0 example.porn # adult host entry");
                await writer.WriteLineAsync("127.0.0.1 localhost");
                await writer.WriteLineAsync("not-a-domain");
            }

            var blocklist = await DomainBlocklist.LoadAsync(new[] { file }, CancellationToken.None);

            Assert.True(blocklist.IsBlocked("cdn.example.porn", out var matchedRule));
            Assert.Equal("example.porn", matchedRule);
            Assert.False(blocklist.IsBlocked("localhost", out _));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("javhdz.red", "heuristic:jav-hd")]
    [InlineData("CDN.JAVHDZ.RED.", "heuristic:jav-hd")]
    [InlineData("javsub.blog", "heuristic:jav-sub")]
    [InlineData("cdn.javsub.blog", "heuristic:jav-sub")]
    [InlineData("s.sexheo.blog", "heuristic:sexheo")]
    [InlineData("freeporn.example", "heuristic:porn")]
    [InlineData("watch-hentai.example", "heuristic:hentai")]
    [InlineData("example.xxx", "heuristic:xxx")]
    [InlineData("example.sex", "heuristic:adult-tld")]
    [InlineData("example.adult", "heuristic:adult-tld")]
    [InlineData("sex-video.example", "heuristic:sex-media")]
    public void HostnameHeuristics_blocks_high_confidence_variants(string host, string expectedRule)
    {
        var classifier = new HostnameHeuristicClassifier(new HostnameHeuristicsOptions());

        var blocked = classifier.IsBlocked(host, out var matchedRule);

        Assert.True(blocked);
        Assert.Equal(expectedRule, matchedRule);
    }

    [Theory]
    [InlineData("java.com")]
    [InlineData("javelin.example")]
    [InlineData("javassist.org")]
    [InlineData("example.com")]
    [InlineData("newsxxx.example")]
    [InlineData("essex-video.example")]
    [InlineData("sexeducation.example")]
    [InlineData("sussex.cam")]
    [InlineData("cambridge-video.example")]
    public void HostnameHeuristics_avoids_low_confidence_or_non_adult_hosts(string host)
    {
        var classifier = new HostnameHeuristicClassifier(new HostnameHeuristicsOptions());

        var blocked = classifier.IsBlocked(host, out var matchedRule);

        Assert.False(blocked);
        Assert.Null(matchedRule);
    }

    [Fact]
    public void HostnameHeuristics_respects_domain_allowlist()
    {
        var classifier = new HostnameHeuristicClassifier(new HostnameHeuristicsOptions
        {
            AllowedDomains = ["javhdz.red"]
        });

        var blocked = classifier.IsBlocked("cdn.javhdz.red", out var matchedRule);

        Assert.False(blocked);
        Assert.Null(matchedRule);
    }
}
