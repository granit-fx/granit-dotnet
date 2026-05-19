namespace Granit.Oidc.Internal;

/// <summary>
/// Base64url encoding and decoding utilities (RFC 4648 §5).
/// </summary>
internal static class Base64Url
{
    /// <summary>
    /// Encodes the specified byte array to a base64url string without padding.
    /// </summary>
    internal static string Encode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>
    /// Decodes a base64url string to a byte array, adding padding as needed.
    /// </summary>
    internal static byte[] Decode(string base64Url)
    {
        ArgumentException.ThrowIfNullOrEmpty(base64Url);

        string padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
