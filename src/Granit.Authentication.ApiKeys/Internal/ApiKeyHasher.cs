using System.Security.Cryptography;
using System.Text;
using Granit.Authentication.ApiKeys.Options;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.ApiKeys.Internal;

/// <summary>
/// Default <see cref="IApiKeyHasher"/> implementation. Reads the pepper from
/// <see cref="ApiKeysOptions.Pepper"/> at startup and selects the hashing
/// scheme accordingly.
/// </summary>
internal sealed class ApiKeyHasher : IApiKeyHasher
{
    internal const string V1Prefix = "v1$";
    internal const string V2Prefix = "v2$";

    private readonly byte[]? _pepper;

    public ApiKeyHasher(IOptions<ApiKeysOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string? raw = options.Value.Pepper;
        _pepper = string.IsNullOrEmpty(raw) ? null : Convert.FromBase64String(raw);
    }

    public bool IsPeppered => _pepper is not null;

    public string ComputeCurrentHash(string rawKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawKey);
        return _pepper is null ? ComputeV1(rawKey) : ComputeV2(rawKey, _pepper);
    }

    public IReadOnlyList<string> ComputeCandidateHashes(string rawKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawKey);
        // v2 first when pepper is configured, then v1 fallback for keys
        // issued before the pepper was set.
        return _pepper is null
            ? [ComputeV1(rawKey)]
            : [ComputeV2(rawKey, _pepper), ComputeV1(rawKey)];
    }

    private static string ComputeV1(string rawKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return V1Prefix + Convert.ToHexStringLower(hash);
    }

    private static string ComputeV2(string rawKey, byte[] pepper)
    {
        byte[] hash = HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes(rawKey));
        return V2Prefix + Convert.ToHexStringLower(hash);
    }
}
