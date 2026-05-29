using System.Security.Cryptography;
using System.Text;
using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingCleaner"/>. Uses <c>ExecuteDeleteAsync</c>
/// and <c>ExecuteUpdateAsync</c> for bulk efficiency. Under <c>Segregated</c>, retention
/// purges and GDPR pseudonymization fan out across every candidate context — a pseudonymized
/// user may have audit rows in either the host DB (host-admin scope) or in a tenant DB.
/// </summary>
internal sealed partial class EfCoreAuditingCleaner(
    AuditingContextResolver resolver,
    ITenantsAccessor tenantsAccessor,
    ICurrentTenant currentTenant,
    ILogger<EfCoreAuditingCleaner> logger) : IAuditingCleaner
{
    private const string PseudonymizedUserName = "[pseudonymized]";

    /// <inheritdoc/>
    public async Task<int> PurgeAsync(
        AuditCategory category,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        int total = 0;

        // Always purge in the current scope's context (matches the prior behaviour:
        // retention runs as a scheduled job under whatever tenant scope it was invoked).
        await using IAuditingDbContext dbContext = await resolver
            .OpenForScopeAsync(currentTenant.IsAvailable ? currentTenant.Id : null, cancellationToken)
            .ConfigureAwait(false);

        total += await dbContext.AuditEntries
            .Where(e => e.Category == category && e.Timestamp < cutoff)
            .OrderBy(e => e.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return total;
    }

    /// <inheritdoc/>
    public async Task<int> PseudonymizeByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        string hashedUserId = HashUserId(userId);
        int total = 0;

        if (resolver.StorageMode == DualScopeStorageMode.Segregated && !currentTenant.IsAvailable)
        {
            // GDPR Art. 17 pseudonymization initiated from host-admin scope — fan out
            // across host + every tenant so no tenant DB retains identifying audit data
            // for the targeted user.
            total += await ExecutePseudonymizationAsync(
                tenantId: null, userId, hashedUserId, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<(Guid Id, string Name)> tenants = await tenantsAccessor
                .GetAllAsync(cancellationToken).ConfigureAwait(false);
            foreach ((Guid id, string name) in tenants)
            {
                using (currentTenant.Change(id, name))
                {
                    total += await ExecutePseudonymizationAsync(
                        tenantId: id, userId, hashedUserId, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else
        {
            total += await ExecutePseudonymizationAsync(
                tenantId: currentTenant.IsAvailable ? currentTenant.Id : null,
                userId, hashedUserId, cancellationToken).ConfigureAwait(false);
        }

        LogPseudonymizationCompleted(total, hashedUserId);
        return total;
    }

    private async Task<int> ExecutePseudonymizationAsync(
        Guid? tenantId,
        string userId,
        string hashedUserId,
        CancellationToken cancellationToken)
    {
        await using IAuditingDbContext dbContext = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return await dbContext.AuditEntries
            .Where(e => e.UserId == userId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.UserId, hashedUserId)
                    .SetProperty(e => e.UserName, PseudonymizedUserName)
                    .SetProperty(e => e.IpAddress, (string?)null)
                    .SetProperty(e => e.UserAgent, (string?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static string HashUserId(string userId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Pseudonymized {Count} audit entries for hashed user {HashedUserId} (GDPR Art. 17).")]
    private partial void LogPseudonymizationCompleted(int count, string hashedUserId);
}
