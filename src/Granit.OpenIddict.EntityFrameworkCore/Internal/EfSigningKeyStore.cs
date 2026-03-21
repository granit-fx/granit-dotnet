using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Services;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ISigningKeyStore"/>.
/// </summary>
internal sealed class EfSigningKeyStore(
    IDbContextFactory<OpenIddictDbContext> dbFactory) : ISigningKeyStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<SigningKey>> GetKeysAsync(
        params SigningKeyStatus[] statuses)
    {
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync().ConfigureAwait(false);

        return await db.SigningKeys
            .AsNoTracking()
            .Where(k => statuses.Contains(k.Status))
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SigningKey?> GetActiveKeyAsync(
        string keyType, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.SigningKeys
            .AsNoTracking()
            .Where(k => k.KeyType == keyType && k.Status == SigningKeyStatus.Active)
            .OrderByDescending(k => k.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(SigningKey key, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.SigningKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(SigningKey key, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.SigningKeys.Update(key);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> PruneRevokedAsync(
        DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.SigningKeys
            .Where(k => k.Status == SigningKeyStatus.Revoked && k.RetiredAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
