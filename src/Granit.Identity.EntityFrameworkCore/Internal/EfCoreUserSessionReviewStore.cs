using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Durable <see cref="IUserSessionReviewStore"/> backed by EF Core. The single-use guarantee is enforced by the
/// unique <c>(UserId, SessionId)</c> index: the first insert wins; a concurrent or repeat attempt trips the
/// constraint and is reported as "already reviewed". Survives restarts and is shared across instances.
/// </summary>
internal sealed class EfCoreUserSessionReviewStore(
    IDbContextFactory<IdentityDbContext> dbContextFactory,
    IGuidGenerator guidGenerator) : IUserSessionReviewStore
{
    public async Task<UserSessionReviewDecision?> GetDecisionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        UserSessionReviewEntity? entity = await db.UserSessionReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return entity?.Decision;
    }

    public async Task<bool> TryRecordDecisionAsync(
        string userId,
        string sessionId,
        UserSessionReviewDecision decision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.UserSessionReviews.Add(new UserSessionReviewEntity
        {
            Id = guidGenerator.Create(),
            UserId = userId,
            SessionId = sessionId,
            Decision = decision,
            ReviewedAt = reviewedAt,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // A row may already exist (repeat click / scanner prefetch / concurrent winner). Distinguish that
            // "already reviewed" case — return false — from a genuine failure, which we rethrow.
            await using IdentityDbContext check = await dbContextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            bool exists = await check.UserSessionReviews
                .AsNoTracking()
                .AnyAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                return false;
            }

            throw;
        }
    }
}
