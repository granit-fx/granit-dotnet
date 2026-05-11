using System.Net;
using System.Net.Sockets;

namespace Granit.Http.Security;

/// <summary>
/// Classifies IP addresses into SSRF-relevant categories. Used by
/// <see cref="IUrlSafetyValidator"/> for URL-level validation and exposed publicly so
/// callers operating below the URL layer (e.g. <c>SocketsHttpHandler.ConnectCallback</c>
/// in <c>Granit.Webhooks</c>) can re-check resolved IPs at connect time without
/// duplicating the rule set.
/// </summary>
/// <remarks>
/// Covers:
/// <list type="bullet">
///   <item>Distinct <see cref="UrlSafetyViolationKind.MetadataEndpoint"/> for 169.254.169.254,
///   <c>fd00:ec2::254</c>, <c>fe80::a9fe:a9fe</c> (AWS IMDS / GCP / Azure)</item>
///   <item>Distinct <see cref="UrlSafetyViolationKind.Loopback"/>, <see cref="UrlSafetyViolationKind.LinkLocal"/>,
///   <see cref="UrlSafetyViolationKind.PrivateNetwork"/>, <see cref="UrlSafetyViolationKind.IPv6UniqueLocal"/></item>
///   <item>IPv4-mapped-IPv6 unwrap then re-classify</item>
/// </list>
/// </remarks>
public static class PrivateNetworkClassifier
{
    /// <summary>
    /// Returns <c>true</c> when the address falls in a non-routable / sensitive range.
    /// </summary>
    public static bool IsBlocked(IPAddress ip) => Classify(ip, out _);

    // AWS IMDSv1/v2 IPv6: fd00:ec2::254 → fd 00 0e c2 00 00 00 00 00 00 00 00 00 00 02 54.
    private static readonly byte[] AwsIPv6Metadata =
        [0xfd, 0x00, 0x0e, 0xc2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x54];

    // Azure/GCP link-local IPv6 metadata variant: fe80::a9fe:a9fe → 12 zero bytes then a9 fe a9 fe.
    private static readonly byte[] LinkLocalIPv6Metadata =
        [0xfe, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xa9, 0xfe, 0xa9, 0xfe];

    /// <summary>
    /// Returns <c>true</c> and the matching <paramref name="kind"/> when the address falls in a
    /// non-routable / sensitive range.
    /// </summary>
    public static bool Classify(IPAddress ip, out UrlSafetyViolationKind kind)
    {
        ArgumentNullException.ThrowIfNull(ip);

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => ClassifyIPv4(ip.GetAddressBytes(), out kind),
            AddressFamily.InterNetworkV6 => ClassifyIPv6(ip.GetAddressBytes(), out kind),
            _ => Miss(out kind),
        };
    }

    private static bool ClassifyIPv4(byte[] b, out UrlSafetyViolationKind kind)
    {
        // 169.254.169.254 — AWS IMDS / GCP / Azure IMDS (all three converged on this address).
        if (b[0] == 169 && b[1] == 254 && b[2] == 169 && b[3] == 254)
        {
            kind = UrlSafetyViolationKind.MetadataEndpoint;
            return true;
        }

        // 127.0.0.0/8 — loopback (RFC 1122).
        if (b[0] == 127)
        {
            kind = UrlSafetyViolationKind.Loopback;
            return true;
        }

        // 169.254.0.0/16 — link-local (RFC 3927).
        if (b[0] == 169 && b[1] == 254)
        {
            kind = UrlSafetyViolationKind.LinkLocal;
            return true;
        }

        // 0.0.0.0/8 — "this network" / unspecified (RFC 1122).
        // 10.0.0.0/8 — private (RFC 1918).
        // 100.64.0.0/10 — CGNAT (RFC 6598). 0x40 == 64, 0x7F == 127.
        // 172.16.0.0/12 — private (RFC 1918).
        // 192.168.0.0/16 — private (RFC 1918).
        if (b[0] == 0
            || b[0] == 10
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168))
        {
            kind = UrlSafetyViolationKind.PrivateNetwork;
            return true;
        }

        return Miss(out kind);
    }

    private static bool ClassifyIPv6(byte[] b, out UrlSafetyViolationKind kind)
    {
        // ::1 — loopback.
        if (IsAllZerosExceptLast(b))
        {
            kind = UrlSafetyViolationKind.Loopback;
            return true;
        }

        // fd00:ec2::254 (AWS IMDSv6) and fe80::a9fe:a9fe (Azure/GCP variant).
        if (b.AsSpan().SequenceEqual(AwsIPv6Metadata) || b.AsSpan().SequenceEqual(LinkLocalIPv6Metadata))
        {
            kind = UrlSafetyViolationKind.MetadataEndpoint;
            return true;
        }

        // fe80::/10 — link-local (RFC 4291).
        if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80)
        {
            kind = UrlSafetyViolationKind.LinkLocal;
            return true;
        }

        // fc00::/7 — unique local (RFC 4193). Includes the fd00::/8 half.
        if ((b[0] & 0xfe) == 0xfc)
        {
            kind = UrlSafetyViolationKind.IPv6UniqueLocal;
            return true;
        }

        return Miss(out kind);
    }

    private static bool IsAllZerosExceptLast(byte[] b)
    {
        if (b[15] != 1)
        {
            return false;
        }

        for (int i = 0; i < 15; i++)
        {
            if (b[i] != 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool Miss(out UrlSafetyViolationKind kind)
    {
        kind = default;
        return false;
    }
}
