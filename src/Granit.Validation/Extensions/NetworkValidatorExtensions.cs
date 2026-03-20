using System.Text.RegularExpressions;
using FluentValidation;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for network and URI identifiers.
/// </summary>
public static partial class NetworkValidatorExtensions
{
    // RFC 3986 practical subset: http(s) scheme + authority + optional path/query/fragment.
    // Does not attempt to cover every corner of the RFC; designed for web URLs.
    [GeneratedRegex(
        @"^https?://[a-zA-Z0-9\-]+(\.[a-zA-Z0-9\-]+)*(:\d{1,5})?(/[^\s]*)?$",
        RegexOptions.None, 100)]
    private static partial Regex UrlRegex();

    // IPv4: 4 octets (0–255) separated by dots.
    [GeneratedRegex(
        @"^((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(25[0-5]|2[0-4]\d|[01]?\d\d?)$",
        RegexOptions.None, 100)]
    private static partial Regex Ipv4Regex();

    // MAC address: 6 hex pairs separated by colons or dashes (case-insensitive).
    [GeneratedRegex(
        @"^([0-9A-F]{2}[:\-]){5}[0-9A-F]{2}$",
        RegexOptions.IgnoreCase, 100)]
    private static partial Regex MacAddressRegex();

    /// <summary>
    /// Validates a URL with a required <c>http</c> or <c>https</c> scheme per RFC 3986.
    /// </summary>
    /// <remarks>
    /// Accepts standard web URLs (e.g. <c>https://example.com/path?q=1</c>).
    /// Rejects URLs without a scheme or with non-HTTP schemes.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Url<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && UrlRegex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Granit:Validation:InvalidUrl");

    /// <summary>
    /// Validates an IPv4 address per RFC 791.
    /// </summary>
    /// <remarks>
    /// Accepts dotted-decimal notation with four octets (0–255),
    /// e.g. <c>192.168.1.1</c> or <c>0.0.0.0</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Ipv4Address<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && Ipv4Regex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Granit:Validation:InvalidIpv4Address");

    /// <summary>
    /// Validates an IPv6 address per RFC 4291.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="System.Net.IPAddress.TryParse(string?, out System.Net.IPAddress?)"/> and verifies
    /// the address family is <see cref="System.Net.Sockets.AddressFamily.InterNetworkV6"/>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Ipv6Address<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value =>
                value != null
                && System.Net.IPAddress.TryParse(value.Trim(), out System.Net.IPAddress? ip)
                && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidIpv6Address");

    /// <summary>
    /// Validates a MAC address per IEEE 802.
    /// </summary>
    /// <remarks>
    /// Accepts 6 hexadecimal pairs separated by colons or dashes,
    /// e.g. <c>00:1A:2B:3C:4D:5E</c> or <c>00-1A-2B-3C-4D-5E</c>.
    /// Case-insensitive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> MacAddress<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && MacAddressRegex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Granit:Validation:InvalidMacAddress");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidUrl(string? value) =>
        value is not null && UrlRegex().IsMatch(value.Trim());

    internal static bool IsValidIpv4Address(string? value) =>
        value is not null && Ipv4Regex().IsMatch(value.Trim());

    internal static bool IsValidIpv6Address(string? value) =>
        value is not null
        && System.Net.IPAddress.TryParse(value.Trim(), out System.Net.IPAddress? ip)
        && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;

    internal static bool IsValidMacAddress(string? value) =>
        value is not null && MacAddressRegex().IsMatch(value.Trim());
}
