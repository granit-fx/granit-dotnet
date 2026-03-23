using Granit.RateLimiting.Options;
using Microsoft.Extensions.Options;

namespace Granit.RateLimiting.Internal;

/// <summary>
/// Validates <see cref="GranitRateLimitingOptions"/> at startup.
/// </summary>
internal sealed class GranitRateLimitingOptionsValidator : IValidateOptions<GranitRateLimitingOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GranitRateLimitingOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.KeyPrefix))
        {
            failures.Add($"{nameof(options.KeyPrefix)} must not be empty.");
        }

        foreach ((string policyName, RateLimitPolicyOptions policy) in options.Policies)
        {
            string prefix = $"Policy '{policyName}'";

            if (policy.PermitLimit <= 0)
            {
                failures.Add($"{prefix}: {nameof(policy.PermitLimit)} must be greater than 0.");
            }

            if (policy.Window <= TimeSpan.Zero)
            {
                failures.Add($"{prefix}: {nameof(policy.Window)} must be greater than zero.");
            }

            if (policy.Algorithm is RateLimitAlgorithm.TokenBucket)
            {
                if (policy.TokenLimit <= 0)
                {
                    failures.Add($"{prefix}: {nameof(policy.TokenLimit)} must be greater than 0 for TokenBucket.");
                }

                if (policy.TokensPerPeriod <= 0)
                {
                    failures.Add($"{prefix}: {nameof(policy.TokensPerPeriod)} must be greater than 0 for TokenBucket.");
                }

                if (policy.ReplenishmentPeriod <= TimeSpan.Zero)
                {
                    failures.Add($"{prefix}: {nameof(policy.ReplenishmentPeriod)} must be greater than zero for TokenBucket.");
                }
            }

            if (policy.Algorithm is RateLimitAlgorithm.Concurrency)
            {
                failures.Add($"{prefix}: {nameof(RateLimitAlgorithm.Concurrency)} algorithm is not yet supported. Use SlidingWindow, FixedWindow, or TokenBucket.");
            }

            if (policy.Algorithm is RateLimitAlgorithm.SlidingWindow && policy.SegmentsPerWindow <= 0)
            {
                failures.Add($"{prefix}: {nameof(policy.SegmentsPerWindow)} must be greater than 0 for SlidingWindow.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
