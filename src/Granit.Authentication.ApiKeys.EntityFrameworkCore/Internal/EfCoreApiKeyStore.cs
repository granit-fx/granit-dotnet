using Granit.Authentication.ApiKeys.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IApiKeyStore"/> using
/// <see cref="IDbContextFactory{TContext}"/> for safe concurrent access.
/// </summary>
internal sealed partial class EfCoreApiKeyStore(
    IDbContextFactory<AuthenticationApiKeysDbContext> contextFactory,
    ICurrentTenant currentTenant,
    ILogger<EfCoreApiKeyStore> logger)
    : EfStoreBase<ApiKeyEntry, AuthenticationApiKeysDbContext>(contextFactory, currentTenant), IApiKeyStore
{
    /// <inheritdoc/>
    public Task<ApiKeyEntry?> FindByHashAsync(string hashedKey, CancellationToken cancellationToken = default) =>
        ReadAsync(
            db => db.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.HashedKey == hashedKey, cancellationToken),
            cancellationToken);

    /// <inheritdoc/>
    public async Task UpdateLastUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteAsync(async db =>
            {
                await db.ApiKeys
                    .Where(k => k.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, usedAt), cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
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
