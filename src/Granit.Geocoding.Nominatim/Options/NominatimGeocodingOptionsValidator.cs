using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Nominatim.Options;

/// <summary>
/// Validates <see cref="NominatimGeocodingOptions"/> at startup.
/// </summary>
internal sealed class NominatimGeocodingOptionsValidator : IValidateOptions<NominatimGeocodingOptions>
{
    public ValidateOptionsResult Validate(string? name, NominatimGeocodingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProviderName))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.ProviderName)} must be non-empty.");
        }

        if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.BaseAddress)} must be an absolute URI.");
        }

        if (options.BaseAddress.Scheme != Uri.UriSchemeHttps && options.BaseAddress.Scheme != Uri.UriSchemeHttp)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.BaseAddress)} must use http or https.");
        }

        // Plaintext http is rejected for real endpoints: this request carries the address (personal data, a GDPR
        // sub-processor transfer), which must not travel in cleartext. http is tolerated only for loopback hosts so
        // a local mock/proxy or a self-hosted instance can be used in development and tests.
        if (options.BaseAddress.Scheme == Uri.UriSchemeHttp && !options.BaseAddress.IsLoopback)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.BaseAddress)} must use https for non-loopback hosts (it sends the address, which "
                + "must not be transmitted in cleartext).");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Timeout)} must be a positive duration.");
        }

        if (options.MaxResponseSizeBytes <= 0)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.MaxResponseSizeBytes)} must be positive.");
        }

        // The Nominatim usage policy mandates an identifying User-Agent; requests without one are blocked.
        if (string.IsNullOrWhiteSpace(options.UserAgent))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.UserAgent)} must be set to an identifying value (the Nominatim usage policy "
                + "requires it).");
        }

        if (options.RateLimitPerSecond <= 0)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.RateLimitPerSecond)} must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
