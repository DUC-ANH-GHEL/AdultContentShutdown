using System.Security;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public static class ManagedBrowserUpdateManifest
{
    public static string Create(string extensionId, string crxUrl, string version)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentException("Extension id is required.", nameof(extensionId));
        }

        if (string.IsNullOrWhiteSpace(crxUrl))
        {
            throw new ArgumentException("CRX URL is required.", nameof(crxUrl));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Extension version is required.", nameof(version));
        }

        return
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gupdate xmlns=\"http://www.google.com/update2/response\" protocol=\"2.0\">" +
            $"<app appid=\"{SecurityElement.Escape(extensionId)}\">" +
            $"<updatecheck codebase=\"{SecurityElement.Escape(crxUrl)}\" version=\"{SecurityElement.Escape(version)}\" />" +
            "</app>" +
            "</gupdate>";
    }
}
