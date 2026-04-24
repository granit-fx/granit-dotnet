using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Options;

namespace Granit.Http.Idempotency.Internal;

/// <summary>
/// Validates <see cref="IdempotencyOptions"/> at startup (IValidateOptions pattern).
/// </summary>
internal sealed class IdempotencyOptionsValidator : IValidateOptions<IdempotencyOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, IdempotencyOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            failures.Add($"{nameof(options.HeaderName)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyPrefix))
        {
            failures.Add($"{nameof(options.KeyPrefix)} must not be empty.");
        }

        if (options.MaxBodySizeBytes <= 0)
        {
            failures.Add($"{nameof(options.MaxBodySizeBytes)} must be greater than 0.");
        }

        if (options.MaxResponseSizeBytes <= 0)
        {
            failures.Add($"{nameof(options.MaxResponseSizeBytes)} must be greater than 0.");
        }

        if (options.MaxKeyLength <= 0)
        {
            failures.Add($"{nameof(options.MaxKeyLength)} must be greater than 0.");
        }

        if (options.TombstoneTtl <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.TombstoneTtl)} must be greater than zero.");
        }

        if (options.ExecutionTimeout >= options.InProgressTtl)
        {
            failures.Add(
                $"{nameof(options.ExecutionTimeout)} ({options.ExecutionTimeout}) must be strictly less than " +
                $"{nameof(options.InProgressTtl)} ({options.InProgressTtl}) to guarantee the InProgress lock " +
                "outlives the business handler.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
