using System.Net;
using FluentValidation;
using Granit.Http.Security;
using Granit.Validation.Extensions;

namespace Granit.Webhooks.Endpoints.Validators;

/// <summary>
/// Shared validation rules for webhook target URLs.
/// Enforces HTTPS, blocks private/local addresses (SSRF protection),
/// and rejects internal TLDs.
/// </summary>
/// <remarks>
/// IP-range, TLD, and metadata-endpoint classification is delegated to
/// <see cref="PrivateNetworkClassifier"/> / <see cref="ReservedTldClassifier"/> in
/// <c>Granit.Http.Security</c>. Webhook validation keeps a thin, sync-only wrapper here
/// so FluentValidation rule chains stay synchronous; the full DNS-rebinding-resistant
/// pipeline (<see cref="IUrlSafetyValidator"/>) is applied at delivery time by
/// <c>WebhookSsrfConnectCallback</c>.
/// </remarks>
public static class WebhookTargetUrlValidatorExtensions
{
    internal const int MaxUrlLength = 2048;

    /// <summary>
    /// Adds target URL validation rules: HTTPS only, no private/local IPs, no blocked TLDs.
    /// </summary>
    public static IRuleBuilderOptions<T, string> IsValidWebhookTargetUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .MaximumLength(MaxUrlLength)
            .Must(BeAValidHttpsUrl)
                .WithErrorCodeAndMessage("Validation:InvalidWebhookUrl")
            .Must(NotTargetPrivateOrLocalAddress)
                .WithErrorCodeAndMessage("Validation:WebhookUrlPrivateAddress")
            .Must(NotUseBlockedTld)
                .WithErrorCodeAndMessage("Validation:WebhookUrlBlockedTld");
    }

    private static bool BeAValidHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private static bool NotTargetPrivateOrLocalAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = uri.Host;

        // Reject any reserved / internal-only hostname (localhost, *.local, *.internal, *.corp, …).
        // Catches `localhost`, `ip6-localhost`, `*.home.arpa`, etc. — anything that NotUseBlockedTld
        // would also reject, but applied here pre-IP so that hostnames pointing at private
        // addresses via /etc/hosts trip on the literal name too.
        if (ReservedTldClassifier.IsReserved(host, out _))
        {
            return false;
        }

        return !IPAddress.TryParse(host, out IPAddress? ip)
            || !PrivateNetworkClassifier.IsBlocked(ip);
    }

    private static bool NotUseBlockedTld(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return !ReservedTldClassifier.IsReserved(uri.Host, out _);
    }
}
