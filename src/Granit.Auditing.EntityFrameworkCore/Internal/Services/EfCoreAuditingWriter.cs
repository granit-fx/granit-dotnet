using Granit.Auditing.Domain;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingWriter"/> for persisting explicit audit
/// log entries (login events, access denied, config changes). Routes via
/// <see cref="AuditingContextResolver"/> on <c>entry.TenantId</c>.
/// </summary>
internal sealed class EfCoreAuditingWriter(AuditingContextResolver resolver) : IAuditingWriter
{
    /// <inheritdoc/>
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using IAuditingDbContext dbContext = await resolver
            .OpenForScopeAsync(entry.TenantId, cancellationToken).ConfigureAwait(false);

        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
