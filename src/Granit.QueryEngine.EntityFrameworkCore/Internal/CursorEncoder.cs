using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.QueryEngine.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Encodes and decodes opaque cursors for keyset pagination.
/// Uses Base64Url-encoded JSON with HMAC-SHA256 integrity protection
/// to prevent cursor forgery (CWE-565).
/// </summary>
internal static class CursorEncoder
{
    private const char SignatureSeparator = '.';

    /// <summary>
    /// Encodes a single cursor value to a signed Base64Url string.
    /// </summary>
    public static string Encode(object value, byte[]? hmacKey = null)
    {
        string json = JsonSerializer.Serialize(value);
        string payload = ToBase64Url(json);
        return hmacKey is not null ? Sign(payload, hmacKey) : payload;
    }

    /// <summary>
    /// Encodes a composite cursor (multiple sort field values) to a signed Base64Url string.
    /// </summary>
    public static string EncodeComposite(Dictionary<string, string> values, byte[]? hmacKey = null)
    {
        string json = JsonSerializer.Serialize(values);
        string payload = ToBase64Url(json);
        return hmacKey is not null ? Sign(payload, hmacKey) : payload;
    }

    /// <summary>
    /// Attempts to decode a cursor as a composite cursor (JSON object with field-value pairs).
    /// Returns <c>null</c> if the cursor is a legacy single-value cursor.
    /// </summary>
    public static Dictionary<string, string>? DecodeComposite(
        string cursor, ILogger? logger = null, byte[]? hmacKey = null)
    {
        try
        {
            string payload = VerifyAndExtractPayload(cursor, hmacKey);
            string json = FromBase64Url(payload);

            // Composite cursors are JSON objects; legacy cursors are JSON primitives (strings)
            if (!json.StartsWith('{'))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                QueryEngineEfCoreLog.CursorDecodeFailed(logger, ex);
            }

            return null;
        }
    }

    /// <summary>
    /// Decodes a Base64Url cursor string back to the target type.
    /// </summary>
    public static T? Decode<T>(string cursor, ILogger? logger = null, byte[]? hmacKey = null)
    {
        try
        {
            string payload = VerifyAndExtractPayload(cursor, hmacKey);
            string json = FromBase64Url(payload);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                QueryEngineEfCoreLog.CursorDecodeFailed(logger, ex);
            }

            return default;
        }
    }

    private static string Sign(string payload, byte[] key)
    {
        byte[] signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return payload + SignatureSeparator + ToBase64Url(signature);
    }

    private static string VerifyAndExtractPayload(string cursor, byte[]? hmacKey)
    {
        if (hmacKey is null)
        {
            return cursor;
        }

        int separatorIndex = cursor.LastIndexOf(SignatureSeparator);
        if (separatorIndex < 0)
        {
            throw new InvalidOperationException("Cursor signature missing.");
        }

        string payload = cursor[..separatorIndex];
        string providedSignature = cursor[(separatorIndex + 1)..];

        byte[] expected = HMACSHA256.HashData(hmacKey, Encoding.UTF8.GetBytes(payload));
        string expectedSignature = ToBase64Url(expected);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(providedSignature)))
        {
            throw new InvalidOperationException("Cursor signature invalid.");
        }

        return payload;
    }

    private static string ToBase64Url(string json) =>
        ToBase64Url(Encoding.UTF8.GetBytes(json));

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string FromBase64Url(string cursor)
    {
        string padded = cursor.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        byte[] bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
