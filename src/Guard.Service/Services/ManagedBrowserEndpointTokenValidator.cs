namespace AdultContentShutdownGuard.Guard.Service.Services;

public static class ManagedBrowserEndpointTokenValidator
{
    public static bool IsValid(string? configuredToken, string? requestToken)
    {
        return !string.IsNullOrWhiteSpace(configuredToken) &&
               !string.IsNullOrWhiteSpace(requestToken) &&
               !string.Equals(configuredToken, "CHANGE_THIS_SECRET_TOKEN", StringComparison.Ordinal) &&
               string.Equals(configuredToken, requestToken, StringComparison.Ordinal);
    }
}
