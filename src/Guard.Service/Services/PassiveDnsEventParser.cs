using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public static partial class PassiveDnsEventParser
{
    public static bool TryExtractDomain(string? eventText, out string? domain)
    {
        domain = null;
        if (string.IsNullOrWhiteSpace(eventText))
        {
            return false;
        }

        if (TryExtractFromXml(eventText, out domain) || TryExtractFromMessage(eventText, out domain))
        {
            domain = Normalize(domain);
            return !string.IsNullOrWhiteSpace(domain);
        }

        return false;
    }

    private static bool TryExtractFromXml(string text, out string? domain)
    {
        domain = null;
        try
        {
            var document = XDocument.Parse(text);
            var queryName = document.Descendants()
                .Where(element => element.Name.LocalName == "Data")
                .FirstOrDefault(element => string.Equals((string?)element.Attribute("Name"), "QueryName", StringComparison.OrdinalIgnoreCase));

            if (queryName is null)
            {
                return false;
            }

            domain = queryName.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractFromMessage(string text, out string? domain)
    {
        domain = null;
        var match = QueryNameRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        domain = match.Groups["domain"].Value;
        return true;
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim().TrimEnd('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "-")
        {
            return null;
        }

        return normalized;
    }

    [GeneratedRegex(@"QueryName\s*[:=]\s*(?<domain>[A-Za-z0-9._-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QueryNameRegex();
}
