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
///   <item>Broadcast/multicast/reserved-future as <see cref="UrlSafetyViolationKind.ReservedAddress"/></item>
///   <item>IPv6 transition forms (NAT64, 6to4, Teredo, IPv4-compatible) unwrapped and re-classified;
///   if the embedded IPv4 is sensitive, reported as <see cref="UrlSafetyViolationKind.IPv6EmbeddedIPv4"/></item>
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

    // NAT64 well-known prefix (RFC 6052): 64:ff9b:: → 00 64 ff 9b 00 00 00 00 00 00 00 00 + 4 bytes IPv4.
    private static readonly byte[] Nat64Prefix =
        [0x00, 0x64, 0xff, 0x9b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

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
        // 100.64.0.0/10 — CGNAT (RFC 6598).
        // 172.16.0.0/12 — private (RFC 1918).
        // 192.168.0.0/16 — private (RFC 1918).
        // 192.0.0.0/24 — IETF protocol assignments (RFC 6890).
        // 192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24 — TEST-NET-1/2/3 (RFC 5737).
        // 198.18.0.0/15 — benchmarking (RFC 2544); often routed to internal taps.
        if (b[0] == 0
            || b[0] == 10
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)
            || (b[0] == 192 && b[1] == 0 && b[2] == 2)
            || (b[0] == 198 && b[1] == 51 && b[2] == 100)
            || (b[0] == 203 && b[1] == 0 && b[2] == 113)
            || (b[0] == 198 && (b[1] == 18 || b[1] == 19)))
        {
            kind = UrlSafetyViolationKind.PrivateNetwork;
            return true;
        }

        // 224.0.0.0/4 — multicast (RFC 5771).
        // 240.0.0.0/4 — reserved for future use (RFC 1112) including 255.255.255.255 limited broadcast.
        if (b[0] >= 224)
        {
            kind = UrlSafetyViolationKind.ReservedAddress;
            return true;
        }

        return Miss(out kind);
    }

    private static readonly byte[] IPv6LoopbackBytes = IPAddress.IPv6Loopback.GetAddressBytes();
    private static readonly byte[] IPv6UnspecifiedBytes = IPAddress.IPv6Any.GetAddressBytes();
    private static readonly byte[] Ipv6DocumentationPrefix = [0x20, 0x01, 0x0d, 0xb8]; // 2001:db8::/32

    private static bool ClassifyIPv6(byte[] b, out UrlSafetyViolationKind kind)
    {
        // ::1 — loopback.
        if (b.AsSpan().SequenceEqual(IPv6LoopbackBytes))
        {
            kind = UrlSafetyViolationKind.Loopback;
            return true;
        }

        // :: — unspecified (equivalent of 0.0.0.0).
        if (b.AsSpan().SequenceEqual(IPv6UnspecifiedBytes))
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

        // ff00::/8 — multicast (RFC 4291).
        if (b[0] == 0xff)
        {
            kind = UrlSafetyViolationKind.ReservedAddress;
            return true;
        }

        // 2001:db8::/32 — documentation (RFC 3849) — must never resolve.
        if (b.AsSpan(0, 4).SequenceEqual(Ipv6DocumentationPrefix))
        {
            kind = UrlSafetyViolationKind.ReservedAddress;
            return true;
        }

        // NAT64 well-known prefix 64:ff9b::/96 (RFC 6052) — last 4 bytes are an embedded IPv4.
        if (b.AsSpan(0, 12).SequenceEqual(Nat64Prefix))
        {
            return ClassifyEmbeddedIPv4(b.AsSpan(12, 4), out kind);
        }

        // 6to4 — 2002::/16. Bytes 2..5 carry the IPv4 (RFC 3056).
        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return ClassifyEmbeddedIPv4(b.AsSpan(2, 4), out kind);
        }

        // Teredo — 2001::/32 (RFC 4380). Bytes 12..15 are the client IPv4 obfuscated by XOR 0xff.
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00)
        {
            Span<byte> teredoClient = stackalloc byte[4];
            teredoClient[0] = (byte)(b[12] ^ 0xff);
            teredoClient[1] = (byte)(b[13] ^ 0xff);
            teredoClient[2] = (byte)(b[14] ^ 0xff);
            teredoClient[3] = (byte)(b[15] ^ 0xff);
            return ClassifyEmbeddedIPv4(teredoClient, out kind);
        }

        // ::a.b.c.d — IPv4-compatible IPv6 (deprecated, RFC 4291 §2.5.5.1). First 12 bytes are zero,
        // last 4 are the IPv4. We already matched ::1 and :: above, so any other ::/96 with a non-zero
        // tail is treated as embedded IPv4.
        if (IsZero(b.AsSpan(0, 12)) && !IsZero(b.AsSpan(12, 4)))
        {
            return ClassifyEmbeddedIPv4(b.AsSpan(12, 4), out kind);
        }

        return Miss(out kind);
    }

    private static bool ClassifyEmbeddedIPv4(ReadOnlySpan<byte> v4, out UrlSafetyViolationKind kind)
    {
        byte[] copy = [v4[0], v4[1], v4[2], v4[3]];
        if (ClassifyIPv4(copy, out UrlSafetyViolationKind embedded))
        {
            kind = embedded == UrlSafetyViolationKind.MetadataEndpoint
                ? UrlSafetyViolationKind.MetadataEndpoint
                : UrlSafetyViolationKind.IPv6EmbeddedIPv4;
            return true;
        }

        return Miss(out kind);
    }

    private static bool IsZero(ReadOnlySpan<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != 0)
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
