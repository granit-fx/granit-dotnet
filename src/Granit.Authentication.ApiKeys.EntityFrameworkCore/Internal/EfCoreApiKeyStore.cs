using Granit.Authentication.ApiKeys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IApiKeyStore"/> using
/// <see cref="IDbContextFactory{TContext}"/> for safe concurrent access.
/// </summary>
internal sealed partial class EfCoreApiKeyStore(
    IDbContextFactory<AuthenticationApiKeysDbContext> contextFactory,
    ILogger<EfCoreApiKeyStore> logger) : IApiKeyStore
{
    /// <inheritdoc/>
    public async Task<ApiKeyEntry?> FindByHashAsync(string hashedKey, CancellationToken cancellationToken = default)
    {
        await using AuthenticationApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.HashedKey == hashedKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateLastUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AuthenticationApiKeysDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            await db.ApiKeys
                .Where(k => k.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, usedAt), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Fire-and-forget update — log but don't propagate
            LogLastUsedUpdateFailed(logger, id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update LastUsedAt for API key {ApiKeyId}.")]
    private static partial void LogLastUsedUpdateFailed(ILogger logger, Guid apiKeyId, Exception exception);
}
