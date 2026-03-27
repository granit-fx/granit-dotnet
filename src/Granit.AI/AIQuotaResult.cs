namespace Granit.AI;

/// <summary>
/// Result of an <see cref="IAIQuotaGuard"/> check.
/// </summary>
/// <param name="IsAllowed">Whether the AI call may proceed.</param>
/// <param name="Reason">
/// Human-readable reason when <see cref="IsAllowed"/> is <c>false</c>.
/// <c>null</c> when allowed.
/// </param>
/// <param name="Remaining">
/// Number of requests remaining in the current window, or <c>null</c> if unknown.
/// </param>
public sealed record AIQuotaResult(bool IsAllowed, string? Reason = null, int? Remaining = null)
{
    /// <summary>Shared instance for "allowed" results without remaining count.</summary>
    public static readonly AIQuotaResult Allowed = new(true);

    /// <summary>Creates a denied result with a reason.</summary>
    public static AIQuotaResult Denied(string reason) => new(false, reason);
}
