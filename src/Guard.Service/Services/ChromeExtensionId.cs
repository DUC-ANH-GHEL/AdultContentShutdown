using System.Security.Cryptography;
using System.Text;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public static class ChromeExtensionId
{
    public static string FromPemPrivateKey(string pemPrivateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pemPrivateKey);
        return FromPublicKey(rsa.ExportSubjectPublicKeyInfo());
    }

    public static string FromPublicKey(byte[] subjectPublicKeyInfo)
    {
        return FromSha256Hash(SHA256.HashData(subjectPublicKeyInfo));
    }

    public static string FromSha256Hash(byte[] hash)
    {
        if (hash.Length < 16)
        {
            throw new ArgumentException("Chrome extension id requires at least 16 hash bytes.", nameof(hash));
        }

        var builder = new StringBuilder(32);
        for (var index = 0; index < 16; index++)
        {
            var current = hash[index];
            builder.Append(ToChromeAlphabet((current >> 4) & 0x0f));
            builder.Append(ToChromeAlphabet(current & 0x0f));
        }

        return builder.ToString();
    }

    private static char ToChromeAlphabet(int value)
    {
        return (char)('a' + value);
    }
}
