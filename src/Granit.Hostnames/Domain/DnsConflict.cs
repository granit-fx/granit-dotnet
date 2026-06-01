namespace Granit.Hostnames.Domain;

/// <summary>
/// A DNS conflict detected during verification.
/// Stored as JSON on <see cref="ManagedHostname.Conflicts"/>.
/// </summary>
/// <param name="ConflictType">Category of the conflict.</param>
/// <param name="Details">Human-readable detail (e.g. the actual value found vs. expected).</param>
public sealed record DnsConflict(DnsConflictType ConflictType, string Details);
