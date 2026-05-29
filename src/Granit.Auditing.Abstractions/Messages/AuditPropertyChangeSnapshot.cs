namespace Granit.Auditing.Messages;

/// <summary>
/// Snapshot of a single property change captured from the EF Core ChangeTracker.
/// </summary>
/// <param name="PropertyName">Name of the changed property.</param>
/// <param name="OriginalValue">Original value (JSON-serialized, or <c>"***"</c> if sensitive).</param>
/// <param name="NewValue">New value (JSON-serialized, or <c>"***"</c> if sensitive).</param>
public sealed record AuditPropertyChangeSnapshot(
    string PropertyName,
    string? OriginalValue,
    string? NewValue);
