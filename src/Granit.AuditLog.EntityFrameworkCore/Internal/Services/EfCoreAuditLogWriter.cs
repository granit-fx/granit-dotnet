using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogWriter"/> for persisting
/// explicit audit log entries (login events, access denied, config changes).
/// </summary>
internal sealed class EfCoreAuditLogWriter(
    IDbContextFactory<AuditLogDbContext> dbContextFactory) : IAuditLogWriter
{
    /// <inheritdoc/>
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using AuditLogDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
