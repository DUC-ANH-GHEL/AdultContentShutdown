using System.Security.Cryptography;
using AdultContentShutdownGuard.Guard.Service.Services;
using Xunit;

namespace AdultContentShutdownGuard.Guard.Service.Tests;

public sealed class ManagedBrowserExtensionTests
{
    [Fact]
    public void ConvertsPublicKeyHashToChromeExtensionIdAlphabet()
    {
        var hash = new byte[]
        {
            0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef,
            0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef,
            0xff
        };

        var extensionId = ChromeExtensionId.FromSha256Hash(hash);

        Assert.Equal("abcdefghijklmnopabcdefghijklmnop", extensionId);
    }

    [Fact]
    public void ReadsExtensionIdFromPemPrivateKey()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKeyPem();
        var expected = ChromeExtensionId.FromPublicKey(rsa.ExportSubjectPublicKeyInfo());

        var extensionId = ChromeExtensionId.FromPemPrivateKey(privateKey);

        Assert.Equal(expected, extensionId);
        Assert.Matches("^[a-p]{32}$", extensionId);
    }

    [Fact]
    public void BuildsChromiumUpdateManifest()
    {
        var manifest = ManagedBrowserUpdateManifest.Create(
            "abcdefghijklmnopabcdefghijklmnop",
            "http://127.0.0.1:8765/extensions/AdultContentShutdownGuard.crx",
            "1.2.3");

        Assert.Contains("<app appid=\"abcdefghijklmnopabcdefghijklmnop\">", manifest);
        Assert.Contains("codebase=\"http://127.0.0.1:8765/extensions/AdultContentShutdownGuard.crx\"", manifest);
        Assert.Contains("version=\"1.2.3\"", manifest);
    }
}
