using System.Net;

namespace Granit.Http.Security;

/// <summary>
/// Result of an <see cref="IUrlSafetyValidator"/> check.
/// </summary>
/// <param name="IsValid"><c>true</c> when the URL passed every rule.</param>
/// <param name="Violation">The first violation encountered, or <c>null</c> when valid.</param>
/// <param name="ResolvedAddresses">
/// Addresses resolved for the host. Callers SHOULD pin the connecting socket to one of these
/// (or re-validate) to defeat DNS rebinding between validation and use.
/// </param>
public readonly record struct UrlSafetyResult(
    bool IsValid,
    UrlSafetyViolation? Violation,
    IReadOnlyList<IPAddress> ResolvedAddresses)
{
    /// <summary>Creates a successful result with the resolved addresses.</summary>
    public static UrlSafetyResult Valid(IReadOnlyList<IPAddress> ips) =>
        new(true, null, ips);

    /// <summary>Creates a failure result with the given violation.</summary>
    public static UrlSafetyResult Invalid(UrlSafetyViolation v) =>
        new(false, v, []);
}
