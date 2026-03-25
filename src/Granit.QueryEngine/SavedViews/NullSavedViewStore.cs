using Granit.QueryEngine.SavedViews.Domain;

namespace Granit.QueryEngine.SavedViews;

/// <summary>
/// Default implementation of <see cref="ISavedViewStoreReader"/> and <see cref="ISavedViewStoreWriter"/>.
/// Throws <see cref="NotImplementedException"/> — requires <c>Granit.QueryEngine.EntityFrameworkCore</c>.
/// </summary>
internal sealed class NullSavedViewStore : ISavedViewStoreReader, ISavedViewStoreWriter
{
    private const string Message =
        "Saved view persistence requires Granit.QueryEngine.EntityFrameworkCore. " +
        "Call builder.AddGranitQueryEngineEntityFrameworkCore() to register a concrete ISavedViewStoreReader/ISavedViewStoreWriter.";

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedView>> GetListAsync(
        string entityType, string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<SavedView?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task CreateAsync(SavedView view, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task UpdateAsync(SavedView view, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task SetDefaultAsync(Guid id, string userId, string entityType, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);
}
