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
    public new async Task<bool> UpdateAsync(SigningKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            await base.UpdateAsync(key, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another rotation already transitioned this key (its ConcurrencyStamp moved).
            // Report the lost race to the caller so it aborts instead of minting a duplicate key.
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<int> PruneRevokedAsync(
        DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        // Deliberate interceptor bypass: revoked keys are hard-deleted. SigningKey is not
        // ISoftDeletable (no soft-delete to preserve) and pruning is a maintenance operation,
        // so ExecuteDelete (which skips SaveChanges interceptors) is the intended path.
        WriteAsync(
            db => db.SigningKeys
                .Where(k => k.Status == SigningKeyStatus.Revoked && k.RetiredAt < olderThan)
                .ExecuteDeleteAsync(cancellationToken),
            cancellationToken);
}
