using System.Security.Cryptography;
using System.Text;

namespace Lumen.Application.Common.Helpers;

internal static class AuthCrypto
{
    internal static string HashSecret(string raw)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    internal static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }
}
