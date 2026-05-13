using System.Security.Cryptography;
using System.Text;

namespace Granit.Mergeable.EntityFrameworkCore.Internal;

/// <summary>
/// Canonicalises a <see cref="MergeRequest"/> and computes the keyed HMAC digest used as the
/// idempotency replay key alongside the caller-supplied <see cref="MergeRequest.IdempotencyKey"/>.
/// Two calls with the same key + same canonical body return the cached result; same key +
/// different body is rejected with a 409 (key reuse for a different intent).
/// </summary>
/// <remarks>
/// The hash is keyed (HMAC-SHA-256, RFC 2104) rather than a plain digest so an attacker who
/// gains <c>INSERT</c> rights to <c>merge_idempotency</c> cannot pre-compute a
/// matching <c>RequestHash</c> for a future legitimate request and poison the replay cache.
/// The MAC key is derived from <see cref="IMergeableSecretProvider"/> (deployment-bound,
/// non-recoverable from the DB).
/// </remarks>
internal static class MergeRequestHasher
{
    /// <summary>
    /// Returns a 64-char lowercase hex HMAC-SHA-256 digest of the canonical request body
    /// (tenant + survivor + loser + reason + ordered choices). Excludes
    /// <see cref="MergeRequest.IdempotencyKey"/> (the key itself indexes the cache) and
    /// <see cref="MergeRequest.DryRun"/> (preview vs commit must not collide — otherwise the
    /// caller could replay a dry-run as a commit through cache poisoning).
    /// </summary>
    public static string ComputeHash(MergeRequest request, Guid? tenantId, byte[] macKey)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(macKey);

        // Canonical form: pipe-delimited, sorted choice keys, invariant-culture decimals.
        var sb = new StringBuilder();
        sb.Append(tenantId?.ToString("N") ?? string.Empty);
        sb.Append('|');
        sb.Append(request.SurvivorId.ToString("N"));
        sb.Append('|');
        sb.Append(request.LoserId.ToString("N"));
        sb.Append('|');
        sb.Append(request.Reason ?? string.Empty);
        sb.Append('|');

        if (request.Choices is { Choices.Count: > 0 } choices)
        {
            foreach (KeyValuePair<string, WinnerSide> kv in choices.Choices.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                sb.Append(kv.Key).Append('=').Append((int)kv.Value).Append(';');
            }
        }

        byte[] digest = HMACSHA256.HashData(macKey, Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    /// Returns the 64-char lowercase hex HMAC-SHA-256 of <paramref name="payload"/> keyed
    /// with the deployment MAC key. Used to authenticate the encrypted <c>ResultJson</c>
    /// before deserialisation (encrypt-then-MAC).
    /// </summary>
    public static string ComputePayloadMac(string payload, byte[] macKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(macKey);

        byte[] digest = HMACSHA256.HashData(macKey, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(digest);
    }
}
