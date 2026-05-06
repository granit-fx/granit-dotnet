namespace Granit.Taxonomy;

/// <summary>
/// Probe consulted by the nightly orphan-assignment sweep job (T5.2): given a
/// <c>(tenantId, targetId)</c> pair, returns <c>true</c> when the underlying
/// aggregate still exists in its owning module.
/// </summary>
/// <remarks>
/// One probe is registered per taggable target type via
/// <c>services.AddTaggableExistenceProbe&lt;TAggregate, TProbe&gt;()</c>. Probes are
/// keyed by the same <c>typeof(TAggregate).FullName</c> string used as the
/// <c>TargetType</c> discriminator on assignment rows. When no probe is registered
/// for a target type, the sweep service falls back to an "always exists" default
/// — the safe option, since deleting assignments for a type with no oracle would
/// silently lose user data.
/// </remarks>
public interface ITaggableExistenceProbe
{
    /// <summary>
    /// Returns <c>true</c> when the aggregate identified by
    /// <paramref name="targetId"/> (within <paramref name="tenantId"/> when the
    /// aggregate is multi-tenant) is still persisted; <c>false</c> when it has
    /// been hard-deleted or no longer satisfies the module's visibility rules.
    /// </summary>
    Task<bool> ExistsAsync(Guid? tenantId, Guid targetId, CancellationToken cancellationToken);
}
