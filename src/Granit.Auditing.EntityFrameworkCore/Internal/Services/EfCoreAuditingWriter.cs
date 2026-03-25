using Granit.Auditing.Abstractions;
using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingWriter"/> for persisting
/// explicit audit log entries (login events, access denied, config changes).
/// </summary>
internal sealed class EfCoreAuditingWriter(
    IDbContextFactory<AuditingDbContext> dbContextFactory) : IAuditingWriter
{
    /// <inheritdoc/>
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
