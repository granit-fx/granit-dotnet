using Microsoft.Extensions.Options;

namespace Granit.Imaging.AI.Options;

/// <summary>
/// Validates <see cref="ImagingAIOptions"/> at startup (fail-fast on an unusable timeout).
/// </summary>
internal sealed class ImagingAIOptionsValidator : IValidateOptions<ImagingAIOptions>
{
    private const int MaxTimeoutSeconds = 300;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ImagingAIOptions options)
    {
        if (options.TimeoutSeconds is < 1 or > MaxTimeoutSeconds)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.TimeoutSeconds)} must be between 1 and {MaxTimeoutSeconds} seconds.");
        }

        if (options.MaxImageBytes < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.MaxImageBytes)} must be >= 0 (0 = disabled).");
        }

        return ValidateOptionsResult.Success;
    }
}
