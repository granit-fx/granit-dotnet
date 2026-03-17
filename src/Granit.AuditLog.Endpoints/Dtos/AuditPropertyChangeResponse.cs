namespace Granit.AuditLog.Endpoints.Dtos;

/// <summary>
/// Response DTO for a property change within an entity change.
/// </summary>
/// <param name="PropertyName">Name of the changed property.</param>
/// <param name="OriginalValue">Original value (JSON-serialized, or "***" if sensitive).</param>
/// <param name="NewValue">New value (JSON-serialized, or "***" if sensitive).</param>
public sealed record AuditPropertyChangeResponse(
    string PropertyName,
    string? OriginalValue,
    string? NewValue);
