using Granit.Auditing.Abstractions;
using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingCleaner"/>.
/// Deletes expired audit entries using <c>ExecuteDeleteAsync</c> for bulk efficiency.
/// </summary>
internal sealed class EfCoreAuditingCleaner(
    IDbContextFactory<AuditingDbContext> dbContextFactory) : IAuditingCleaner
{
    /// <inheritdoc/>
    public async Task<int> PurgeAsync(
        AuditCategory category,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.AuditEntries
            .Where(e => e.Category == category && e.Timestamp < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
