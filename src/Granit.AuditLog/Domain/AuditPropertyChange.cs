using Granit.Domain;

namespace Granit.AuditLog.Domain;

/// <summary>
/// Records a single property change within an <see cref="AuditEntityChange"/>.
/// </summary>
public class AuditPropertyChange : Entity
{
    /// <summary>Foreign key to the parent <see cref="AuditEntityChange"/>.</summary>
    public Guid AuditEntityChangeId { get; set; }

    /// <summary>Name of the changed property.</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Original value before the change (JSON-serialized).
    /// <c>null</c> for newly created entities. Masked as <c>"***"</c> for
    /// properties decorated with <see cref="Attributes.AuditSensitiveAttribute"/>.
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// New value after the change (JSON-serialized).
    /// <c>null</c> for deleted entities. Masked as <c>"***"</c> for
    /// properties decorated with <see cref="Attributes.AuditSensitiveAttribute"/>.
    /// </summary>
    public string? NewValue { get; set; }
}
