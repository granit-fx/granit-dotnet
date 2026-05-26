namespace Granit.Domain;

/// <summary>
/// Marker for entities owned by a specific user — semantically distinct from
/// <see cref="IMultiTenant"/> (tenant scope) and from <c>CreatedBy</c> (immutable
/// audit identity).
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="IMultiTenant.TenantId"/>, <see cref="OwnerId"/> is <b>not</b>
/// injected automatically by the persistence interceptor. Ownership is a domain
/// decision: the caller is not always the owner (imports, transfers, service
/// accounts, GDPR reassignment flows). The aggregate factory MUST require an
/// explicit <see cref="Guid"/> owner and fail loudly when none is resolvable.
/// </para>
/// <para>
/// EF Core materialises the underlying property via the entity's <c>private set</c>;
/// the get-only interface preserves DDD encapsulation while exposing the marker
/// for the index convention applied by <c>ApplyGranitConventions</c> and for the
/// <c>OwnedByCurrentUserHandler</c> in <c>Granit.Authorization</c>.
/// </para>
/// </remarks>
public interface IOwnable
{
    /// <summary>Identifier of the user who owns this entity.</summary>
    Guid OwnerId { get; }
}
