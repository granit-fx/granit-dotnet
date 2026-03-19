using System.Net;
using System.Net.Sockets;
using FluentValidation;

namespace Granit.Webhooks.Endpoints.Validators;

/// <summary>
/// Shared validation rules for webhook target URLs.
/// Enforces HTTPS, blocks private/local addresses (SSRF protection),
/// and rejects internal TLDs.
/// </summary>
public static class WebhookTargetUrlValidatorExtensions
{
    internal const int MaxUrlLength = 2048;

    private static readonly string[] BlockedTlds =
        [".local", ".internal", ".localhost", ".onion"];

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
                .WithMessage("Granit:Validation:InvalidWebhookUrl")
                .WithErrorCode("Granit:Validation:InvalidWebhookUrl")
            .Must(NotTargetPrivateOrLocalAddress)
                .WithMessage("Granit:Validation:WebhookUrlPrivateAddress")
                .WithErrorCode("Granit:Validation:WebhookUrlPrivateAddress")
            .Must(NotUseBlockedTld)
                .WithMessage("Granit:Validation:WebhookUrlBlockedTld")
                .WithErrorCode("Granit:Validation:WebhookUrlBlockedTld");
    }

    private static bool BeAValidHttpsUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
               && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool NotTargetPrivateOrLocalAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = uri.Host;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out IPAddress? ip))
        {
            return !IsBlockedIpAddress(ip);
        }

        return true;
    }

    private static bool IsBlockedIpAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();

            // 0.0.0.0
            if (bytes[0] == 0)
            {
                return true;
            }

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local / cloud metadata)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            byte[] bytes = ip.GetAddressBytes();

            // fe80::/10 (link-local)
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
            {
                return true;
            }

            // fc00::/7 (unique local)
            if ((bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NotUseBlockedTld(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = uri.Host;

        foreach (string tld in BlockedTlds)
        {
            if (host.EndsWith(tld, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
