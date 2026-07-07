using Granit.BackgroundJobs.Options;
using Microsoft.Extensions.Options;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Validates <see cref="BackgroundJobsOptions"/> at startup.
/// </summary>
internal sealed class BackgroundJobsOptionsValidator : IValidateOptions<BackgroundJobsOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        if (options.FailureAlertThreshold < 1)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.FailureAlertThreshold)} must be at least 1 " +
                $"(got {options.FailureAlertThreshold}).");
        }

        return ValidateOptionsResult.Success;
    }
}
