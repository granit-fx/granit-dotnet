using Granit.Hostnames.Domain;

namespace Granit.Hostnames.Contracts;

/// <summary>
/// Verifies a hostname's DNS configuration against its expected records.
/// The default implementation uses .NET managed DNS; adapters for external
/// providers (Cloudflare, Route 53, …) are separate packages.
/// </summary>
public interface IHostnameVerifier
{
    /// <summary>
    /// Checks live DNS for <paramref name="hostname"/> against its
    /// <see cref="ManagedHostname.ExpectedDnsRecords"/>. Returns whether verification
    /// passed and any detected conflicts.
    /// </summary>
    Task<HostnameVerificationResult> VerifyAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a single DNS verification attempt.</summary>
/// <param name="IsVerified">
/// <c>true</c> when all expected records matched and no conflicts were detected.
/// </param>
/// <param name="Conflicts">
/// Conflicts detected; empty when <see cref="IsVerified"/> is <c>true</c>.
/// </param>
public sealed record HostnameVerificationResult(
    bool IsVerified,
    IReadOnlyList<DnsConflict> Conflicts);
