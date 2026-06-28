using AdultContentShutdownGuard.Guard.Service.Models;
using AdultContentShutdownGuard.Guard.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guard.Service.Tests;

public sealed class ContentViolationServiceTests
{
    [Fact]
    public async Task HandleViolationAsync_ignores_keyword_only_false_positive_from_analytics()
    {
        var service = CreateService();
        var request = new ViolationRequest
        {
            Url = "https://analytics.google.com/analytics/web/#/p123/reports/intelligenthome",
            Host = "analytics.google.com",
            Title = "Analytics",
            Reason = "adult content score threshold reached",
            MatchedRules =
            [
                "url keywords: anal",
                "title keywords: anal"
            ],
            DetectedAt = DateTimeOffset.UtcNow
        };

        var handled = await service.HandleViolationAsync(request, CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task HandleViolationAsync_accepts_keyword_only_violation_when_keywords_are_delimited()
    {
        var service = CreateService();
        var request = new ViolationRequest
        {
            Url = "https://example.invalid/videos/anal-scene",
            Host = "example.invalid",
            Title = "Anal video",
            Reason = "adult content score threshold reached",
            MatchedRules =
            [
                "url keywords: anal",
                "title keywords: anal"
            ],
            DetectedAt = DateTimeOffset.UtcNow
        };

        var handled = await service.HandleViolationAsync(request, CancellationToken.None);

        Assert.True(handled);
    }

    private static ContentViolationService CreateService()
    {
        var options = Options.Create(new GuardOptions
        {
            DryRun = true,
            LogDirectory = Path.Combine(Path.GetTempPath(), "acsg-tests", Guid.NewGuid().ToString("N"))
        });
        var fileLogger = new FileLogger(options);
        var shutdownService = new ShutdownService(options, fileLogger, NullLogger<ShutdownService>.Instance);
        var guardEventService = new GuardEventService(options, fileLogger, shutdownService);
        return new ContentViolationService(options, fileLogger, guardEventService);
    }
}
