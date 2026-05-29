namespace Granit.Auditing;

/// <summary>
/// Resolves the audited child entities of an aggregate root so the parent's
/// timeline can surface their audit rows alongside its own. Solves the generic
/// parent–child gap in the audit trail: an audit on <c>PageVersion</c> belongs
/// in the timeline of its owning <c>Page</c>, on <c>OrderLine</c> in the
/// timeline of its <c>Order</c>, and so on.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the registration model of <see cref="IAuditEntityTypeAliasProvider"/>
/// — opt-in per module via <c>TryAddEnumerable</c>; the reader unions the
/// contributions of every registered provider. Hosts with no resolver
/// registered keep the pre-existing behaviour (parent-only audits).
/// </para>
/// <para>
/// Implementations run inside the caller's request scope and inherit the
/// ambient <c>ICurrentTenant</c>, so child-id lookups stay within the tenant
/// boundary without extra work.
/// </para>
/// </remarks>
public interface IAuditChildResolver
{
    /// <summary>
    /// Returns the audited children of <paramref name="parentEntityType"/>
    /// with id <paramref name="parentEntityId"/>, grouped by child CLR type.
    /// An empty result means this resolver has no children to contribute for
    /// the supplied parent — return <c>[]</c> rather than throwing.
    /// </summary>
    Task<IReadOnlyCollection<AuditChildScope>> ResolveAsync(
        string parentEntityType,
        string parentEntityId,
        CancellationToken cancellationToken = default);
}
