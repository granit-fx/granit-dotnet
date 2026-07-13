using Granit.Bulkhead.Abstractions;
using Granit.Bulkhead.Options;
using Microsoft.Extensions.Options;

namespace Granit.Bulkhead.Internal;

/// <summary>
/// Resolves bulkhead permit limits from static <see cref="BulkheadPolicyOptions.PermitLimit"/> configuration.
/// </summary>
internal sealed class OptionsBulkheadQuotaProvider(
    IOptionsMonitor<GranitBulkheadOptions> options) : IBulkheadQuotaProvider
{
    /// <inheritdoc/>
    public Task<int?> GetPermitLimitAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (!options.CurrentValue.Policies.TryGetValue(policyName, out BulkheadPolicyOptions? policy))
        {
            return Task.FromResult<int?>(null);
        }

        return Task.FromResult<int?>(policy.PermitLimit);
    }
}
