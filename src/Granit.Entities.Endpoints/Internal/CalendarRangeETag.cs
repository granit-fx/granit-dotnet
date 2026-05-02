using System.Security.Cryptography;
using System.Text.Json;
using Granit.Entities.Endpoints.Dtos;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// SHA-256 strong ETag for a calendar range response (story #1691). Same pattern
/// as <see cref="EntityManifestETag"/>: the hash includes the canonical JSON form
/// of the items so any change to the rendered list invalidates the ETag.
/// </summary>
internal static class CalendarRangeETag
{
    private static readonly JsonSerializerOptions CanonicalOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>Returns the strong ETag (e.g. <c>"a1b2…"</c>, quoted per RFC 7232).</summary>
    public static string Compute(IReadOnlyList<CalendarItemResponse> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(items, CanonicalOptions);
        byte[] hash = SHA256.HashData(json);

        return $"\"{Convert.ToHexString(hash)[..32]}\"";
    }
}
