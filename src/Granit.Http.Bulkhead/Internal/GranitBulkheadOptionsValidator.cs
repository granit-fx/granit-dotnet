using Granit.Http.Bulkhead.Options;
using Microsoft.Extensions.Options;

namespace Granit.Http.Bulkhead.Internal;

/// <summary>
/// Validates <see cref="GranitBulkheadOptions"/> at startup.
/// </summary>
internal sealed class GranitBulkheadOptionsValidator : IValidateOptions<GranitBulkheadOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GranitBulkheadOptions options)
    {
        if (options.IdleTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("IdleTimeout must be a positive duration.");
        }

        if (options.CleanupInterval <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("CleanupInterval must be a positive duration.");
        }

        foreach ((string policyName, BulkheadPolicyOptions policy) in options.Policies)
        {
            if (policy.PermitLimit < 1)
            {
                return ValidateOptionsResult.Fail($"Policy '{policyName}': PermitLimit must be at least 1.");
            }

            if (policy.QueueLimit < 0)
            {
                return ValidateOptionsResult.Fail($"Policy '{policyName}': QueueLimit must be non-negative.");
            }

            if (policy.QueueLimit > 0 && policy.QueueTimeout <= TimeSpan.Zero)
            {
                return ValidateOptionsResult.Fail($"Policy '{policyName}': QueueTimeout must be positive when QueueLimit > 0.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
