using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// No-op <see cref="ITenantDbIsolator"/> for Shared database and Tenant-per-Database topologies.
/// </summary>
internal sealed class NullTenantDbIsolator : ITenantDbIsolator
{
    /// <inheritdoc/>
    public Task IsolateAsync(DbContext context, Guid tenantId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
