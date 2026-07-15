using Granit.Identity.Domain;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core interceptor that keeps <see cref="User.EmailHash"/> and
/// <see cref="User.PhoneNumberHash"/> in lockstep with the encrypted plaintext
/// columns (<see cref="User.Email"/>, <see cref="User.PhoneNumber"/>) on every
/// save. The encrypted columns are non-deterministic AES-CBC; admin-grid
/// exact-match lookups index the peppered HMAC digests instead.
/// </summary>
/// <remarks>
/// Auto-wired via <see cref="IGranitAutoInterceptor"/>, so any DbContext
/// registered through <c>AddGranitDbContext</c> picks it up — same pattern as
/// <c>AuditingChangeTrackingInterceptor</c> and
/// <c>PartyCanonicalisationInterceptor</c>.
/// </remarks>
internal sealed class UserLookupHashInterceptor(IUserLookupHasher hasher)
    : SaveChangesInterceptor, IGranitAutoInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData);
        return result;
    }

    private void Apply(DbContextEventData eventData)
    {
        if (eventData.Context is null)
        {
            return;
        }

        foreach (EntityEntry entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity is User user)
            {
                ApplyToUser(user);
            }
        }
    }

    private void ApplyToUser(User user)
    {
        string? emailHash = hasher.ComputeEmailHash(user.Email);
        string? phoneHash = hasher.ComputePhoneHash(user.PhoneNumber);

        if (!string.Equals(emailHash, user.EmailHash, StringComparison.Ordinal)
            || !string.Equals(phoneHash, user.PhoneNumberHash, StringComparison.Ordinal))
        {
            user.SetLookupHashes(emailHash, phoneHash);
        }
    }
}
