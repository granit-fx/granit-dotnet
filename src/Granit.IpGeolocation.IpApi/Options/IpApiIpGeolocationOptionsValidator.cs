using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.IpApi.Options;

/// <summary>
/// Validates <see cref="IpApiIpGeolocationOptions"/> at startup.
/// </summary>
internal sealed class IpApiIpGeolocationOptionsValidator : IValidateOptions<IpApiIpGeolocationOptions>
{
    public ValidateOptionsResult Validate(string? name, IpApiIpGeolocationOptions options)
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

        if (options.Timeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Timeout)} must be a positive duration.");
        }

        if (options.MaxResponseSizeBytes <= 0)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.MaxResponseSizeBytes)} must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
