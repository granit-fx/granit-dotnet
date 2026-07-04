using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.IpInfo.Options;

/// <summary>
/// Validates <see cref="IpInfoIpGeolocationOptions"/> at startup.
/// </summary>
internal sealed class IpInfoIpGeolocationOptionsValidator : IValidateOptions<IpInfoIpGeolocationOptions>
{
    public ValidateOptionsResult Validate(string? name, IpInfoIpGeolocationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProviderName))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.ProviderName)} must be non-empty.");
        }

        if (options.BaseAddress?.IsAbsoluteUri != true)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.BaseAddress)} must be an absolute URI.");
        }

        if (options.BaseAddress.Scheme != Uri.UriSchemeHttps && options.BaseAddress.Scheme != Uri.UriSchemeHttp)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.BaseAddress)} must use http or https.");
        }

        // Plaintext http is rejected for real endpoints: this request carries the client IP (personal data, a
        // GDPR sub-processor transfer) and the API token, neither of which may travel in cleartext. http is
        // tolerated only for loopback hosts so a local mock/proxy can still be used in development and tests.
        if (options.BaseAddress.Scheme == Uri.UriSchemeHttp && !options.BaseAddress.IsLoopback)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.BaseAddress)} must use https for non-loopback hosts (it sends the client IP "
                + "and API token, which must not be transmitted in cleartext).");
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
