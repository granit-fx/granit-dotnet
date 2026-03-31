using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Services;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ISigningKeyStore"/>.
/// </summary>
internal sealed class EfSigningKeyStore(
    IDbContextFactory<OpenIddictDbContext> dbFactory)
    : EfStoreBase<SigningKey, OpenIddictDbContext>(dbFactory), ISigningKeyStore
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<SigningKey>> GetKeysAsync(
        SigningKeyStatus[] statuses,
        CancellationToken cancellationToken = default) =>
        ReadAsync(
            async db => (IReadOnlyList<SigningKey>)await db.SigningKeys
                .AsNoTracking()
                .Where(k => statuses.Contains(k.Status))
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<SigningKey>> GetKeysAsync(
        params SigningKeyStatus[] statuses) =>
        GetKeysAsync(statuses, CancellationToken.None);

    /// <inheritdoc/>
    public Task<SigningKey?> GetActiveKeyAsync(
        string keyType, CancellationToken cancellationToken = default) =>
        ReadAsync(
            db => db.SigningKeys
                .AsNoTracking()
                .Where(k => k.KeyType == keyType && k.Status == SigningKeyStatus.Active)
                .OrderByDescending(k => k.ActivatedAt)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);

    /// <inheritdoc/>
    public Task CreateAsync(SigningKey key, CancellationToken cancellationToken = default) =>
        AddAsync(key, cancellationToken);

    /// <inheritdoc/>
    public new Task UpdateAsync(SigningKey key, CancellationToken cancellationToken = default) =>
        base.UpdateAsync(key, cancellationToken);

    /// <inheritdoc/>
    public Task<int> PruneRevokedAsync(
        DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        ReadAsync(
            db => db.SigningKeys
                .Where(k => k.Status == SigningKeyStatus.Revoked && k.RetiredAt < olderThan)
                .ExecuteDeleteAsync(cancellationToken),
            cancellationToken);
}
