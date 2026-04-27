using System.Security.Cryptography;
using System.Text;

namespace Granit.Mergeable.EntityFrameworkCore.Internal;

/// <summary>
/// Canonicalises a <see cref="MergeRequest"/> into a stable SHA-256 hex digest used as the
/// idempotency replay key alongside the caller-supplied <see cref="MergeRequest.IdempotencyKey"/>.
/// Two calls with the same key + same canonical body return the cached result; same key +
/// different body is rejected with a 409 (key reuse for a different intent).
/// </summary>
internal static class MergeRequestHasher
{
    /// <summary>
    /// Returns a 64-char lowercase hex SHA-256 digest of the canonical request body. Excludes
    /// <see cref="MergeRequest.IdempotencyKey"/> (the key itself indexes the cache) and
    /// <see cref="MergeRequest.DryRun"/> (preview vs commit must match the same intent —
    /// otherwise the caller could replay a dry-run as a commit through cache poisoning).
    /// </summary>
    public static string ComputeHash(MergeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Canonical form: pipe-delimited, sorted choice keys, invariant-culture decimals.
        var sb = new StringBuilder();
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

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(digest);
    }
}
