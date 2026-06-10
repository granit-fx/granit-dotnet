namespace Granit.Oidc.Internal;

/// <summary>
/// Base64url encoding and decoding utilities (RFC 4648 §5), delegating to the
/// allocation-free BCL <see cref="System.Buffers.Text.Base64Url"/> (.NET 9+).
/// </summary>
internal static class Base64Url
{
    /// <summary>
    /// Encodes the specified byte array to a base64url string without padding.
    /// </summary>
    internal static string Encode(byte[] data) =>
        System.Buffers.Text.Base64Url.EncodeToString(data);

    /// <summary>
    /// Decodes a base64url string (with or without padding) to a byte array.
    /// </summary>
    internal static byte[] Decode(string base64Url)
    {
        ArgumentException.ThrowIfNullOrEmpty(base64Url);
        return System.Buffers.Text.Base64Url.DecodeFromChars(base64Url);
    }
}
