namespace Granit.Http.Security;

/// <summary>
/// Detects reserved / never-publicly-resolvable TLDs (RFC 6761, RFC 7686, mDNS).
/// </summary>
public static class ReservedTldClassifier
{
    private static readonly string[] Reserved =
    [
        "local",      // mDNS / Bonjour (RFC 6762)
        "internal",   // de-facto reserved for internal infrastructure (ICANN 2018)
        "localhost",  // RFC 6761
        "onion",      // Tor hidden services (RFC 7686)
        "test",       // RFC 6761
        "example",    // RFC 6761
        "invalid",    // RFC 6761
    ];

    /// <summary>
    /// Returns <c>true</c> when the host's last label is one of the reserved TLDs.
    /// Also matches an unqualified single-label host equal to a reserved name
    /// (e.g. <c>localhost</c> on its own).
    /// </summary>
    public static bool IsReserved(string host, out string tld)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        int lastDot = host.LastIndexOf('.');
        string label = lastDot < 0 ? host : host[(lastDot + 1)..];

        foreach (string r in Reserved)
        {
            if (string.Equals(label, r, StringComparison.OrdinalIgnoreCase))
            {
                tld = r;
                return true;
            }
        }

        tld = string.Empty;
        return false;
    }
}
