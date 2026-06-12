using System.Text.Json;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.UserSessions.EntityFrameworkCore.Internal;

/// <summary>
/// Durable <see cref="IUserSessionRiskStore"/> backed by EF Core. Verdicts survive restarts and are shared across
/// instances, keeping the risk shown on the session surfaces stable.
/// </summary>
internal sealed class EfCoreUserSessionRiskStore(
    IDbContextFactory<UserSessionRiskDbContext> dbContextFactory,
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

        await using UserSessionRiskDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        UserSessionRiskEntity? existing = await db.UserSessionRisks
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        string reasonsJson = JsonSerializer.Serialize(verdict.Reasons, JsonOptions);

        if (existing is not null)
        {
            existing.Level = verdict.Level.ToString();
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
                Level = verdict.Level.ToString(),
                ReasonsJson = reasonsJson,
                AssessedAt = verdict.AssessedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserSessionRiskVerdict?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using UserSessionRiskDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
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

        await using UserSessionRiskDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
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
            Enum.TryParse(entity.Level, out UserSessionRiskLevel level) ? level : UserSessionRiskLevel.None,
            JsonSerializer.Deserialize<List<string>>(entity.ReasonsJson, JsonOptions) ?? [],
            entity.AssessedAt);
}
