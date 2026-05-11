using Granit.Auditing.Domain;

namespace Granit.Auditing.Dtos;

/// <summary>
/// Summary projection of an <see cref="AuditEntityChange"/> for list/query endpoints.
/// </summary>
/// <remarks>
/// Returned by the query engine when <c>MapGranitQuery&lt;AuditEntityChange&gt;</c> is
/// mounted with <c>AuditEntityChangeQueryDefinition</c>. Property-level diffs are
/// deliberately excluded from the list view — they are exposed via the parent
/// <c>AuditEntry</c> detail endpoint. <c>PropertyChangeCount</c> is computed
/// server-side via a SQL subquery on <c>AuditPropertyChange</c>.
/// </remarks>
public sealed record AuditEntityChangeSummaryResponse(
    Guid Id,
    Guid AuditEntryId,
    string EntityType,
    string EntityId,
    AuditChangeType ChangeType,
    int PropertyChangeCount);
