using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.DataExport.Internal;

/// <summary>
/// EF Core-backed implementation of the export request tracker, parameterised by the host
/// <typeparamref name="TContext"/>. Apps that keep privacy entities on their own shared or
/// tenant-scoped DbContext (e.g. via <c>ConfigurePrivacyModule()</c>) register this store
/// against their context rather than the default <c>PrivacyDbContext</c>.
/// </summary>
internal sealed class EfExportRequestTracker<TContext>(
    IDbContextFactory<TContext> contextFactory,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider)
    : EfStoreBase<ExportRequestEntity, TContext>(contextFactory, currentTenant),
      IExportRequestTrackerReader, IExportRequestTrackerWriter
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _contextFactory = contextFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<ExportRequestStatus?> GetStatusAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        ExportRequestEntity? entity = await FindByIdAsync(requestId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : Project(entity);
    }

    public async Task<IReadOnlyList<ExportRequestStatus>> GetByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        await using TContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ExportRequestEntity> rows = await Query(db)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.RequestedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(Project);
    }

    public Task RecordRequestAsync(
        Guid requestId,
        Guid userId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        ExportRequestEntity entity = new()
        {
            Id = requestId,
            UserId = userId,
            State = ExportRequestState.Pending,
            RequestedAt = requestedAt,
        };
        return AddAsync(entity, cancellationToken);
    }

    public async Task MarkCompletedAsync(
        Guid requestId,
        ExportRequestState state,
        string? archiveBlobReferenceId,
        IReadOnlyList<string>? missingProviders,
        CancellationToken cancellationToken = default)
    {
        await using TContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ExportRequestEntity? entity = await Query(db)
            .FirstOrDefaultAsync(e => e.Id == requestId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.State = state;
        entity.CompletedAt = _timeProvider.GetUtcNow();
        entity.ArchiveBlobReferenceId = archiveBlobReferenceId;
        entity.MissingProviders = missingProviders is null ? [] : [.. missingProviders];

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ExportRequestStatus Project(ExportRequestEntity entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.State,
            entity.RequestedAt,
            entity.CompletedAt,
            entity.ArchiveBlobReferenceId,
            entity.MissingProviders);
}
