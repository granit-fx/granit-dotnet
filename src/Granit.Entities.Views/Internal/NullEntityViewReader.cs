namespace Granit.Entities.Views.Internal;

/// <summary>
/// Default no-op reader registered by <c>AddGranitEntitiesViews</c>. Hosts that
/// omit the EF companion still boot — every lookup returns <c>null</c> / empty
/// so the renderer falls back to the compiled default collection.
/// </summary>
internal sealed class NullEntityViewReader : IEntityViewReader
{
    public Task<IReadOnlyList<EntityViewDescriptor>> ListAsync(
        string entityName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<EntityViewDescriptor>>([]);

    public Task<EntityViewDescriptor?> GetAsync(
        string entityName,
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<EntityViewDescriptor?>(null);

    public Task<EntityViewDescriptor?> GetDefaultViewAsync(
        string entityName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<EntityViewDescriptor?>(null);
}
