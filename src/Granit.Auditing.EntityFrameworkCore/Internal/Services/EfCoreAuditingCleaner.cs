using System.Security.Cryptography;
using System.Text;
using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingCleaner"/>.
/// Uses <c>ExecuteDeleteAsync</c> and <c>ExecuteUpdateAsync</c> for bulk efficiency.
/// </summary>
internal sealed partial class EfCoreAuditingCleaner(
    IDbContextFactory<AuditingDbContext> dbContextFactory,
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
        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await dbContext.AuditEntries
            .Where(e => e.Category == category && e.Timestamp < cutoff)
            .OrderBy(e => e.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> PseudonymizeByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        string hashedUserId = HashUserId(userId);

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        int count = await dbContext.AuditEntries
            .Where(e => e.UserId == userId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.UserId, hashedUserId)
                    .SetProperty(e => e.UserName, PseudonymizedUserName)
                    .SetProperty(e => e.IpAddress, (string?)null)
                    .SetProperty(e => e.UserAgent, (string?)null),
                cancellationToken)
            .ConfigureAwait(false);

        LogPseudonymizationCompleted(count, hashedUserId);

        return count;
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
