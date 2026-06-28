using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Options;

/// <summary>
/// Validates <see cref="GranitGeocodingOptions"/> to catch misconfigurations that would otherwise be applied
/// silently — a non-positive cache duration would be forwarded verbatim as the FusionCache entry lifetime.
/// </summary>
internal sealed class GranitGeocodingOptionsValidator : IValidateOptions<GranitGeocodingOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitGeocodingOptions options)
    {
        List<string> failures = [];

        if (options.SuccessCacheDuration <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.SuccessCacheDuration)} must be a positive duration.");
        }

        if (options.FailureCacheDuration <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.FailureCacheDuration)} must be a positive duration.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
