namespace Granit.Domain;

/// <summary>
/// Marker for entities that carry a creation audit trail (ISO 27001).
/// Implemented by the <see cref="CreationAuditedEntity"/> hierarchy so the audit
/// interceptor can populate the fields by interface rather than by base class.
/// This lets entities that cannot inherit <see cref="CreationAuditedEntity"/> —
/// e.g. an ASP.NET Identity user extending <c>IdentityUser&lt;TKey&gt;</c> — still
/// participate in the audit pipeline. Mirrors <see cref="IModificationAuditedObject"/>.
/// </summary>
public interface ICreationAuditedObject
{
    /// <summary>Creation timestamp (UTC).</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>Identifier of the user who created the entity.</summary>
    string CreatedBy { get; set; }
}
