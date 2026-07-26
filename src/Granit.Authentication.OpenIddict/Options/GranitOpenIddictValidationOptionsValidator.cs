using Microsoft.Extensions.Options;

namespace Granit.Authentication.OpenIddict.Options;

/// <summary>
/// Validates <see cref="GranitOpenIddictValidationOptions"/> at startup.
/// </summary>
/// <remarks>
/// Remote token validation resolves the authorization server's signing keys over the network from
/// the issuer's discovery endpoint, so the issuer must be an absolute, TLS-protected URI. Plaintext
/// http is tolerated only for loopback hosts (a local mock/proxy in development or tests) — any
/// non-loopback issuer must use https, which is what "https in production" reduces to.
/// </remarks>
internal sealed class GranitOpenIddictValidationOptionsValidator : IValidateOptions<GranitOpenIddictValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitOpenIddictValidationOptions options)
    {
        if (options.Issuer is null)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Issuer)} is required (the remote authorization server's issuer URI).");
        }

        if (!options.Issuer.IsAbsoluteUri)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Issuer)} must be an absolute URI.");
        }

        if (options.Issuer.Scheme != Uri.UriSchemeHttps && options.Issuer.Scheme != Uri.UriSchemeHttp)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Issuer)} must use http or https.");
        }

        if (options.Issuer.Scheme == Uri.UriSchemeHttp && !options.Issuer.IsLoopback)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Issuer)} must use https for non-loopback hosts "
                + "(token validation fetches signing keys from the issuer and must not do so over cleartext).");
        }

        return ValidateOptionsResult.Success;
    }
}
