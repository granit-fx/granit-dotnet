using System.Net;
using System.Net.Sockets;
using FluentValidation;
using Granit.Validation.Extensions;

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
                .WithErrorCodeAndMessage("Granit:Validation:InvalidWebhookUrl")
            .Must(NotTargetPrivateOrLocalAddress)
                .WithErrorCodeAndMessage("Granit:Validation:WebhookUrlPrivateAddress")
            .Must(NotUseBlockedTld)
                .WithErrorCodeAndMessage("Granit:Validation:WebhookUrlBlockedTld");
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

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedIPv4(ip.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsBlockedIPv6(ip.GetAddressBytes()),
            _ => false,
        };
    }

    private static bool IsBlockedIPv4(byte[] bytes) =>
        bytes[0] == 0                                         // 0.0.0.0
        || bytes[0] == 10                                     // 10.0.0.0/8
        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) // 172.16.0.0/12
        || (bytes[0] == 192 && bytes[1] == 168)               // 192.168.0.0/16
        || (bytes[0] == 169 && bytes[1] == 254);              // 169.254.0.0/16 (link-local / cloud metadata)

    private static bool IsBlockedIPv6(byte[] bytes) =>
        (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)      // fe80::/10 (link-local)
        || ((bytes[0] & 0xfe) == 0xfc);                       // fc00::/7 (unique local)

    private static bool NotUseBlockedTld(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = uri.Host;
        return !BlockedTlds.Any(tld => host.EndsWith(tld, StringComparison.OrdinalIgnoreCase));
    }
}
