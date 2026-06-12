using System.Text.Json;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Durable <see cref="IUserSessionRiskStore"/> backed by EF Core. Verdicts survive restarts and are shared across
/// instances, keeping the risk shown on the session surfaces stable.
/// </summary>
internal sealed class EfCoreUserSessionRiskStore(
    IDbContextFactory<IdentityDbContext> dbContextFactory,
    IGuidGenerator guidGenerator) : IUserSessionRiskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public async Task SetAsync(
        string userId,
        string sessionId,
        UserSessionRiskVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        string reasonsJson = JsonSerializer.Serialize(verdict.Reasons, JsonOptions);

        // Upsert with one retry: two concurrent assessments of the same (userId, sessionId) both miss the
        // existing row and both insert, tripping the unique index. On that conflict, re-read and update the
        // row the winner created instead of surfacing the DbUpdateException. Provider-agnostic (no ON CONFLICT).
        for (int attempt = 0; ; attempt++)
        {
            await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            UserSessionRiskEntity? existing = await db.UserSessionRisks
                .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Level = verdict.Level;
                existing.ReasonsJson = reasonsJson;
                existing.AssessedAt = verdict.AssessedAt;
            }
            else
            {
                db.UserSessionRisks.Add(new UserSessionRiskEntity
                {
                    Id = guidGenerator.Create(),
                    UserId = userId,
                    SessionId = sessionId,
                    Level = verdict.Level,
                    ReasonsJson = reasonsJson,
                    AssessedAt = verdict.AssessedAt,
                });
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Lost an insert race; loop once to re-read and update the winner's row.
            }
        }
    }

    public async Task<UserSessionRiskVerdict?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        UserSessionRiskEntity? entity = await db.UserSessionRisks
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyDictionary<string, UserSessionRiskVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> sessionIds,
        CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0)
        {
            return new Dictionary<string, UserSessionRiskVerdict>();
        }

        List<string> ids = [.. sessionIds];

        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UserSessionRiskEntity> rows = await db.UserSessionRisks
            .AsNoTracking()
            .Where(e => e.UserId == userId && ids.Contains(e.SessionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.SessionId, Map);
    }

    private static UserSessionRiskVerdict Map(UserSessionRiskEntity entity) =>
        new(
            entity.Level,
            JsonSerializer.Deserialize<List<string>>(entity.ReasonsJson, JsonOptions) ?? [],
            entity.AssessedAt);
}
