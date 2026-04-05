namespace Granit.MultiTenancy;

/// <summary>
/// Immutable data for a tenant resolved by an <see cref="Resolvers.ITenantResolver"/>.
/// </summary>
public sealed record TenantInfo(
    Guid? Id,
    string? Name = null,
    string? Identifier = null,
    string? Jurisdiction = null) : ITenantInfo;
