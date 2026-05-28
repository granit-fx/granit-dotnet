namespace Granit.Auditing;

/// <summary>
/// A set of audited child entities of one CLR type that belong to a parent
/// aggregate. Returned by <see cref="IAuditChildResolver"/> implementations so
/// the batch read path can include the children's audit rows in the parent's
/// timeline without separate round-trips per child type.
/// </summary>
/// <param name="ChildEntityType">
/// CLR type name the audit log stamps on rows for this child (matches
/// <c>AuditEntityChange.EntityType</c>).
/// </param>
/// <param name="ChildEntityIds">
/// Audit-store identifiers (<c>AuditEntityChange.EntityId</c>) for every
/// child of the parent. Empty when the parent has no children of this type.
/// </param>
public sealed record AuditChildScope(
    string ChildEntityType,
    IReadOnlyCollection<string> ChildEntityIds);
