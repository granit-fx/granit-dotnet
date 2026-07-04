using System.Security.Cryptography;
using System.Text;

namespace Granit.AI.Tenancy;

/// <summary>
/// Typed cache key for SDK client caches that is safe to hold across requests.
/// </summary>
/// <remarks>
/// <para>
/// Stores a SHA-256 hash of the API key — never the plaintext — so cache key bookkeeping
/// (hash buckets, post-mortem memory dumps) does not duplicate credential material.
/// </para>
/// <para>
/// Implements structural equality manually because <see cref="byte"/>[] hash comparison
/// needs an element-wise check (default array equality is by reference).
/// </para>
/// </remarks>
public readonly struct AICacheKey : IEquatable<AICacheKey>
{
    private readonly byte[]? _apiKeyHash;
    private readonly int _precomputedHash;

    /// <summary>True when the key represents a Managed Identity credential (no API key).</summary>
    public bool UseManagedIdentity { get; }

    /// <summary>The endpoint component of the key, or <c>null</c> when the provider has none.</summary>
    public string? Endpoint { get; }

    private AICacheKey(byte[]? apiKeyHash, string? endpoint, bool useManagedIdentity)
    {
        _apiKeyHash = apiKeyHash;
        Endpoint = endpoint;
        UseManagedIdentity = useManagedIdentity;
        _precomputedHash = ComputeHash(apiKeyHash, endpoint, useManagedIdentity);
    }

    /// <summary>Builds a cache key for an API-key-authenticated client.</summary>
    public static AICacheKey ForApiKey(string apiKey, string? endpoint = null) =>
        new(HashApiKey(apiKey), endpoint, useManagedIdentity: false);

    /// <summary>Builds a cache key for an endpoint-only client (Ollama).</summary>
    public static AICacheKey ForEndpoint(string endpoint) =>
        new(apiKeyHash: null, endpoint, useManagedIdentity: false);

    /// <summary>Builds a cache key for an Azure Managed Identity client.</summary>
    public static AICacheKey ForManagedIdentity(string endpoint) =>
        new(apiKeyHash: null, endpoint, useManagedIdentity: true);

    /// <inheritdoc />
    public bool Equals(AICacheKey other)
    {
        if (UseManagedIdentity != other.UseManagedIdentity)
        {
            return false;
        }
        if (!string.Equals(Endpoint, other.Endpoint, StringComparison.Ordinal))
        {
            return false;
        }
        return BytesEqual(_apiKeyHash, other._apiKeyHash);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AICacheKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _precomputedHash;

    /// <summary>Operator overload for structural equality.</summary>
    public static bool operator ==(AICacheKey left, AICacheKey right) => left.Equals(right);

    /// <summary>Operator overload for structural inequality.</summary>
    public static bool operator !=(AICacheKey left, AICacheKey right) => !left.Equals(right);

    private static byte[] HashApiKey(string apiKey) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));

    private static bool BytesEqual(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a is null || b is null || a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static int ComputeHash(byte[]? apiKeyHash, string? endpoint, bool useManagedIdentity)
    {
        HashCode hc = default;
        hc.Add(useManagedIdentity);
        hc.Add(endpoint, StringComparer.Ordinal);
        if (apiKeyHash is not null)
        {
            // Use the first 4 bytes of the SHA-256 hash as a cheap fingerprint contribution.
            int prefix = (apiKeyHash[0] << 24) | (apiKeyHash[1] << 16) | (apiKeyHash[2] << 8) | apiKeyHash[3];
            hc.Add(prefix);
        }
        return hc.ToHashCode();
    }
}
