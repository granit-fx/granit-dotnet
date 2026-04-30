using System.Security.Cryptography;
using System.Text.Json;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Computes a SHA-256 strong ETag for a manifest payload. The hash includes the
/// canonical JSON representation of the response — every field that affects what
/// the user sees is part of the hash. Permission-filtered manifests therefore
/// produce different ETags per (user-perms-hash, culture), aligning the ETag
/// with the FusionCache key without an extra round-trip through the cache.
/// </summary>
internal static class EntityManifestETag
{
    private static readonly JsonSerializerOptions CanonicalOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>Returns the strong ETag (e.g. <c>"a1b2…"</c>, quoted per RFC 7232).</summary>
    public static string Compute<T>(T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, CanonicalOptions);
        byte[] hash = SHA256.HashData(json);

        // 32-char hex prefix is enough to make collisions astronomically unlikely
        // without bloating the header. Strong ETag → quoted, not weak.
        return $"\"{Convert.ToHexString(hash)[..32]}\"";
    }
}
