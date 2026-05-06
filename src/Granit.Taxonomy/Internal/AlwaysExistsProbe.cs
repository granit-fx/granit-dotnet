namespace Granit.Taxonomy.Internal;

/// <summary>
/// Default <see cref="ITaggableExistenceProbe"/> for taggable target types that
/// did not register a per-module probe. Reports every target as still existing,
/// which makes the orphan-sweep service a no-op for those types — the safe
/// fallback, since deleting assignments without an oracle would risk dropping
/// rows pointing at perfectly valid aggregates.
/// </summary>
internal sealed class AlwaysExistsProbe : ITaggableExistenceProbe
{
    public static readonly AlwaysExistsProbe Instance = new();

    public Task<bool> ExistsAsync(Guid? tenantId, Guid targetId, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
