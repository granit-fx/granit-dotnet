namespace Granit.Domain;

/// <summary>
/// Abstract base class for all persisted entities.
/// Provides only the unique identifier (Guid).
/// </summary>
public abstract class Entity
{
    /// <summary>Unique identifier of the entity.</summary>
    public Guid Id { get; set; }
}
