using Granit.Auditing.Domain;

namespace Granit.Auditing.Messages;

/// <summary>
/// Snapshot of a single entity change captured from the EF Core ChangeTracker.
/// </summary>
/// <param name="EntityType">CLR type name of the affected entity.</param>
/// <param name="EntityId">Primary key of the affected entity (serialized as string).</param>
/// <param name="ChangeType">Type of change applied.</param>
/// <param name="PropertyChanges">Property-level changes (empty when property tracking is disabled).</param>
public sealed record AuditEntityChangeSnapshot(
    string EntityType,
    string EntityId,
    AuditChangeType ChangeType,
    IReadOnlyList<AuditPropertyChangeSnapshot> PropertyChanges);
