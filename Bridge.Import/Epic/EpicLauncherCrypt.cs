using System.Security.Cryptography;
using System.Text;

namespace Bridge.Import.Epic;

/// AES-256-ECB decrypt for Epic launcher RememberMe payloads (same key Legendary uses).
internal static class EpicLauncherCrypt
{
    internal const string DefaultDataKey = "A09C853C9E95409BB94D707EADEFA52E";

    internal static string DecryptToJson(string key, byte[] encrypted)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.ASCII.GetBytes(key);
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        var text = Encoding.UTF8.GetString(decrypted).TrimEnd('\0').Trim();
        if (text.Length == 0)
            throw new InvalidDataException("Decrypted Epic launcher session was empty.");

        return text;
    }
}
