using System.Collections.Concurrent;
using Granit.Oidc.DPoP;

namespace Granit.Oidc.TokenManagement.DPoP.Internal;

/// <summary>
/// Singleton <see cref="IDPoPKeyStore"/> holding one stable EC P-256 proof key per
/// named client for the lifetime of the process.
/// </summary>
internal sealed class DPoPKeyStore(IDPoPProofService proofService) : IDPoPKeyStore
{
    private readonly ConcurrentDictionary<string, string> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string GetOrCreateKey(string clientName)
    {
        ArgumentNullException.ThrowIfNull(clientName);
        return _keys.GetOrAdd(clientName, _ => proofService.GenerateKeyPair());
    }
}
