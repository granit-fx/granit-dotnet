namespace Granit.Entities.Views;

/// <summary>
/// Thrown by <see cref="IEntityViewWriter"/> when the targeted view does not exist
/// in the current tenant. Endpoints translate this to HTTP 404 (RFC 7807).
/// </summary>
public sealed class EntityViewNotFoundException : Exception
{
    public EntityViewNotFoundException(Guid id)
        : base($"EntityView '{id}' not found in the current tenant.")
    {
        Id = id;
    }

    public Guid Id { get; }
}
