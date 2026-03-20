using System.Text;
using System.Text.Json;
using Granit.Querying.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Querying.EntityFrameworkCore.Internal;

/// <summary>
/// Encodes and decodes opaque cursors for keyset pagination.
/// Uses Base64Url-encoded JSON.
/// </summary>
internal static class CursorEncoder
{
    /// <summary>
    /// Encodes a single cursor value to a Base64Url string.
    /// </summary>
    public static string Encode(object value)
    {
        string json = JsonSerializer.Serialize(value);
        return ToBase64Url(json);
    }

    /// <summary>
    /// Encodes a composite cursor (multiple sort field values) to a Base64Url string.
    /// </summary>
    public static string EncodeComposite(Dictionary<string, string> values)
    {
        string json = JsonSerializer.Serialize(values);
        return ToBase64Url(json);
    }

    /// <summary>
    /// Attempts to decode a cursor as a composite cursor (JSON object with field-value pairs).
    /// Returns <c>null</c> if the cursor is a legacy single-value cursor.
    /// </summary>
    public static Dictionary<string, string>? DecodeComposite(string cursor, ILogger? logger = null)
    {
        try
        {
            string json = FromBase64Url(cursor);

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
                QueryingEfCoreLog.CursorDecodeFailed(logger, cursor, ex);
            }

            return null;
        }
    }

    /// <summary>
    /// Decodes a Base64Url cursor string back to the target type.
    /// </summary>
    public static T? Decode<T>(string cursor, ILogger? logger = null)
    {
        try
        {
            string json = FromBase64Url(cursor);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                QueryingEfCoreLog.CursorDecodeFailed(logger, cursor, ex);
            }

            return default;
        }
    }

    private static string ToBase64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
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
