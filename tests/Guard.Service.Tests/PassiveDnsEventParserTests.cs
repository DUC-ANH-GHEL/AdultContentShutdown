using AdultContentShutdownGuard.Guard.Service.Services;
using Xunit;

namespace Guard.Service.Tests;

public sealed class PassiveDnsEventParserTests
{
    [Fact]
    public void TryExtractDomain_reads_query_name_from_event_xml()
    {
        const string xml = """
            <Event>
              <EventData>
                <Data Name="QueryName">www.pornhub.com.</Data>
              </EventData>
            </Event>
            """;

        var extracted = PassiveDnsEventParser.TryExtractDomain(xml, out var domain);

        Assert.True(extracted);
        Assert.Equal("www.pornhub.com", domain);
    }

    [Fact]
    public void TryExtractDomain_reads_query_name_from_message_text()
    {
        const string message = "QueryName: xvideos.com; QueryType: A; QueryStatus: 0";

        var extracted = PassiveDnsEventParser.TryExtractDomain(message, out var domain);

        Assert.True(extracted);
        Assert.Equal("xvideos.com", domain);
    }

    [Fact]
    public void TryExtractDomain_ignores_invalid_or_empty_values()
    {
        var extracted = PassiveDnsEventParser.TryExtractDomain("QueryName: -", out var domain);

        Assert.False(extracted);
        Assert.Null(domain);
    }
}
