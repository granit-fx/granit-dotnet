namespace Granit.AuditLog.Endpoints.Dtos;

/// <summary>
/// Response DTO for an entity change within an audit log entry.
/// </summary>
/// <param name="EntityType">CLR type name of the affected entity.</param>
/// <param name="EntityId">Primary key of the affected entity.</param>
/// <param name="ChangeType">Type of change (Created, Modified, Deleted, SoftDeleted).</param>
/// <param name="PropertyChanges">Property-level changes.</param>
public sealed record AuditEntityChangeResponse(
    string EntityType,
    string EntityId,
    string ChangeType,
    IReadOnlyList<AuditPropertyChangeResponse> PropertyChanges);
