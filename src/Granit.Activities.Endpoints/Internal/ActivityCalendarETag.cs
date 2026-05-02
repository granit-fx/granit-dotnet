using System.Security.Cryptography;
using System.Text.Json;

namespace Granit.Activities.Endpoints.Internal;

/// <summary>
/// Computes a deterministic strong ETag for the activities calendar response
/// payload — same SHA-256-of-canonical-JSON pattern as
/// <c>EntityManifestETag</c>.
/// </summary>
internal static class ActivityCalendarETag
{
    public static string Compute<T>(T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string json = JsonSerializer.Serialize(payload, EtagJsonOptions);
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hash)[..32].ToLowerInvariant()}\"";
    }

    private static readonly JsonSerializerOptions EtagJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
