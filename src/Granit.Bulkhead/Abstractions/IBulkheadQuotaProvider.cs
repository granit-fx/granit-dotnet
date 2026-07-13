namespace Granit.Bulkhead.Abstractions;

/// <summary>
/// Resolves the concurrency permit limit for a bulkhead policy.
/// Implementations may read from static configuration or dynamic <c>Granit.Features</c> Numeric values.
/// </summary>
public interface IBulkheadQuotaProvider
{
    /// <summary>
    /// Returns the permit limit for <paramref name="policyName"/>, or <see langword="null"/>
    /// to fall back to the static <c>BulkheadPolicyOptions.PermitLimit</c>.
    /// </summary>
    Task<int?> GetPermitLimitAsync(string policyName, CancellationToken cancellationToken = default);
}
