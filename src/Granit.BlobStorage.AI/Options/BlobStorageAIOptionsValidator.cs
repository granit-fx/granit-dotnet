using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AI.Options;

/// <summary>
/// Validates <see cref="BlobStorageAIOptions"/> at startup (fail-fast on misconfiguration).
/// </summary>
internal sealed class BlobStorageAIOptionsValidator : IValidateOptions<BlobStorageAIOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BlobStorageAIOptions options)
    {
        if (options.TimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.TimeoutSeconds)} must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
