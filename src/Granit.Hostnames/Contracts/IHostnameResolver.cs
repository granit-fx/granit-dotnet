namespace Granit.Hostnames.Contracts;

/// <summary>
/// Resolves an incoming request host to its owning resource for host-based routing. Tenant-agnostic
/// by design — the host IS the entry point that determines the tenant — so implementations bypass the
/// ambient tenant filter. Only <see cref="Granit.Hostnames.Domain.HostnameStatus.Active"/> hostnames
/// resolve. Cache + invalidation are layered by a sibling package.
/// </summary>
public interface IHostnameResolver
{
    /// <summary>
    /// Resolves <paramref name="host"/> (compared case-insensitively) to its owner, or <c>null</c>
    /// when no active hostname matches.
    /// </summary>
    Task<ResolvedHostname?> ResolveAsync(string host, CancellationToken cancellationToken = default);
}

/// <summary>
/// The owner a hostname resolves to. <see cref="OwnerType"/>/<see cref="OwnerId"/> are opaque to this
/// capability; the consumer interprets them (e.g. <c>"cms.site"</c> → activate that site + tenant).
/// </summary>
/// <param name="Host">The matched hostname (normalised).</param>
/// <param name="OwnerType">Owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource id.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for a global hostname.</param>
/// <param name="IsPrimary">Whether the matched hostname is the owner's canonical one.</param>
public sealed record ResolvedHostname(
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId,
    bool IsPrimary);
