using System.Net;

namespace Granit.Http.Security;

/// <summary>
/// Result of an <see cref="IUrlSafetyValidator"/> check. Construct via
/// <see cref="Valid"/> or <see cref="Invalid"/> — those factories guarantee that
/// <see cref="IsValid"/>, <see cref="Violation"/>, and <see cref="ResolvedAddresses"/>
/// are mutually consistent.
/// </summary>
public readonly record struct UrlSafetyResult
{
    private UrlSafetyResult(bool isValid, UrlSafetyViolation? violation, IReadOnlyList<IPAddress> resolvedAddresses)
    {
        IsValid = isValid;
        Violation = violation;
        ResolvedAddresses = resolvedAddresses;
    }

    /// <summary><c>true</c> when the URL passed every rule.</summary>
    public bool IsValid { get; }

    /// <summary>The first violation encountered, or <c>null</c> when valid.</summary>
    public UrlSafetyViolation? Violation { get; }

    /// <summary>
    /// Addresses resolved for the host. Callers SHOULD pin the connecting socket to one of
    /// these (or re-validate) to defeat DNS rebinding between validation and use.
    /// </summary>
    public IReadOnlyList<IPAddress> ResolvedAddresses { get; }

    /// <summary>Creates a successful result with the resolved addresses.</summary>
    public static UrlSafetyResult Valid(IReadOnlyList<IPAddress> ips) =>
        new(true, null, ips);

    /// <summary>Creates a failure result with the given violation.</summary>
    public static UrlSafetyResult Invalid(UrlSafetyViolation v) =>
        new(false, v, []);
}
