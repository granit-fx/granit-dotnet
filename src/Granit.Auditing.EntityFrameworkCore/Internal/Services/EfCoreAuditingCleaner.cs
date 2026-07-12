using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingCleaner"/>.
/// Uses <c>ExecuteDeleteAsync</c> and <c>ExecuteUpdateAsync</c> for bulk efficiency.
/// </summary>
internal sealed partial class EfCoreAuditingCleaner(
    IDbContextFactory<AuditingDbContext> dbContextFactory,
    IOptions<AuditingOptions> options,
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

        using Activity? activity = AuditingActivitySource.Source.StartActivity(AuditingActivitySource.Pseudonymize);

        string? salt = options.Value.PseudonymizationSalt;
        if (string.IsNullOrEmpty(salt))
        {
            LogUnsaltedPseudonymization();
        }

        string hashedUserId = HashUserId(userId, salt);

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

        activity?.SetTag("audit.pseudonymized_count", count);

        LogPseudonymizationCompleted(count, hashedUserId);

        return count;
    }

    internal static string HashUserId(string userId, string? salt)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(salt + userId));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Pseudonymized {Count} audit entries for hashed user {HashedUserId} (GDPR Art. 17).")]
    private partial void LogPseudonymizationCompleted(int count, string hashedUserId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Pseudonymization hash is UNSALTED (Auditing:PseudonymizationSalt not configured) — low-entropy user ids are re-identifiable by dictionary attack. Safe only for opaque GUID user ids.")]
    private partial void LogUnsaltedPseudonymization();
}
